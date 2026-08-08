using System.Diagnostics;

namespace SmartCopy.Core;

public sealed class ProgressAggregator
{
    private readonly object _gate = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly long _totalBytes;
    private long _totalCopied;
    private long _lastSampleBytes;
    private long _lastSampleMs;

    public ProgressAggregator(long totalBytes)
    {
        _totalBytes = Math.Max(1, totalBytes);
    }

    public TransferProgress Snapshot(
        string currentFile, long fileBytesCopied, long fileBytesTotal,
        int filesDone, int filesTotal, bool completed = false, bool cancelled = false, bool failed = false)
    {
        lock (_gate)
        {
            _totalCopied = Math.Min(_totalBytes, _totalCopied + fileBytesCopied);
            long ms = _clock.ElapsedMilliseconds;
            double speed = ms - _lastSampleMs > 0
                ? (double)(_totalCopied - _lastSampleBytes) * 1000.0 / (ms - _lastSampleMs)
                : 0.0;
            _lastSampleBytes = _totalCopied;
            _lastSampleMs = ms;

            long remaining = _totalBytes - _totalCopied;
            TimeSpan? eta = speed > 0.5 ? TimeSpan.FromSeconds(remaining / speed) : null;

            return new TransferProgress
            {
                CurrentFile = currentFile,
                FileBytesCopied = fileBytesCopied,
                FileBytesTotal = fileBytesTotal,
                TotalBytesCopied = _totalCopied,
                TotalBytes = _totalBytes,
                SpeedBytesPerSecond = speed,
                Remaining = eta,
                FilesDone = filesDone,
                FilesTotal = filesTotal,
                Completed = completed,
                Cancelled = cancelled,
                Failed = failed,
            };
        }
    }
}
