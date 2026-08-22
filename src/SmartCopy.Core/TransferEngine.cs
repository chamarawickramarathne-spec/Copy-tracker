using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace SmartCopy.Core;

public sealed class TransferEngine
{
    public const int DefaultBufferSize = 1024 * 1024;
    private const int ProgressMinIntervalMs = 60;

    private readonly int _bufferSize;
    private readonly int _parallelLimit;

    public TransferEngine(int bufferSize = DefaultBufferSize, int parallelLimit = 4)
    {
        _bufferSize = Math.Max(8192, bufferSize);
        _parallelLimit = Math.Max(1, parallelLimit);
    }

    public async Task<IReadOnlyList<string>> CopyAsync(
        IReadOnlyList<TransferItem> items,
        bool isMove = false,
        IProgress<TransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (items.Count == 0) return Array.Empty<string>();

        string[] duplicates = items.GroupBy(i => i.DestinationPath, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToArray();
        if (duplicates.Length > 0)
        {
            throw new ArgumentException(
                "Multiple sources would write to the same destination: " + string.Join(", ", duplicates));
        }

        long total = items.Sum(i => TryGetLength(i.SourcePath));
        var aggregator = new ProgressAggregator(total);
        var completed = new ConcurrentQueue<string>();
        var copiedSources = new ConcurrentQueue<string>();
        var failures = new ConcurrentQueue<(TransferItem Item, Exception Error)>();
        var reporter = progress is null ? null : new ThrottledProgress(progress);
        using var throttle = new SemaphoreSlim(_parallelLimit);
        int done = 0;

        var tasks = items.Select(async item =>
        {
            await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await CopyFileAsync(item, aggregator,
                    () => Volatile.Read(ref done),
                    () => Interlocked.Increment(ref done),
                    items.Count, reporter, cancellationToken).ConfigureAwait(false);
                copiedSources.Enqueue(item.SourcePath);
                completed.Enqueue(item.DestinationPath);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failures.Enqueue((item, ex));
            }
            finally
            {
                throttle.Release();
            }
        });

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            reporter?.Report(aggregator.Snapshot(string.Empty, 0, 0, done, items.Count, cancelled: true));
            throw;
        }

        if (!failures.IsEmpty)
        {
            reporter?.Report(aggregator.Snapshot(string.Empty, 0, 0, done, items.Count, failed: true));
            var errors = failures.Select(f => new IOException(
                $"'{f.Item.SourcePath}' -> '{f.Item.DestinationPath}': {f.Error.Message}", f.Error));
            throw new AggregateException("One or more files failed to copy.", errors);
        }

        if (isMove)
        {
            foreach (string src in copiedSources)
            {
                try { File.Delete(src); }
                catch { /* copy succeeded — source delete is best-effort */ }
            }
        }

        reporter?.Report(aggregator.Snapshot(string.Empty, 0, 0, done, items.Count, completed: true));
        return completed.ToArray();
    }

    private async Task CopyFileAsync(
        TransferItem item,
        ProgressAggregator aggregator,
        Func<int> filesDone,
        Action fileDone,
        int filesTotal,
        ThrottledProgress? progress,
        CancellationToken cancellationToken)
    {
        string? dir = Path.GetDirectoryName(item.DestinationPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        byte[] buffer = ArrayPool<byte>.Shared.Rent(_bufferSize);
        long fileLength = TryGetLength(item.SourcePath);
        try
        {
            using var source = new FileStream(item.SourcePath, FileMode.Open, FileAccess.Read,
                FileShare.Read, _bufferSize, FileOptions.Asynchronous);
            using var target = new FileStream(item.DestinationPath, FileMode.Create, FileAccess.Write,
                FileShare.None, _bufferSize, FileOptions.Asynchronous);

            long copied = 0;
            int read;
            while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                copied += read;
                progress?.Report(aggregator.Snapshot(item.SourcePath, read, fileLength, filesDone(), filesTotal));
            }

            await target.FlushAsync(cancellationToken).ConfigureAwait(false);
            fileDone();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static long TryGetLength(string path)
    {
        try { return new FileInfo(path).Length; }
        catch { return 0; }
    }

    /// <summary>
    /// Passes progress through at most every <see cref="ProgressMinIntervalMs"/> so fast disks
    /// (thousands of chunks per second) don't flood the UI thread with dispatcher posts.
    /// Final states (completed/cancelled/failed) always pass through.
    /// </summary>
    private sealed class ThrottledProgress : IProgress<TransferProgress>
    {
        private readonly IProgress<TransferProgress> _inner;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private long _lastReportMs;

        public ThrottledProgress(IProgress<TransferProgress> inner) => _inner = inner;

        public void Report(TransferProgress value)
        {
            if (value.Completed || value.Cancelled || value.Failed)
            {
                Volatile.Write(ref _lastReportMs, _clock.ElapsedMilliseconds);
                _inner.Report(value);
                return;
            }

            long now = _clock.ElapsedMilliseconds;
            long last = Volatile.Read(ref _lastReportMs);
            if (now - last < ProgressMinIntervalMs) return;
            if (Interlocked.CompareExchange(ref _lastReportMs, now, last) != last) return;
            _inner.Report(value);
        }
    }
}
