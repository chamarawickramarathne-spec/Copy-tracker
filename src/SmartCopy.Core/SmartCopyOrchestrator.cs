using System.Diagnostics;

namespace SmartCopy.Core;

public sealed record SmartCopyResult(IReadOnlyList<string> CopiedFiles, string DestinationFolder, TimeSpan Elapsed);

public sealed class SmartCopyOrchestrator
{
    private readonly TransferEngine _engine;
    private readonly IntelligentRenamer _renamer;

    public SmartCopyOrchestrator(TransferEngine? engine = null, IntelligentRenamer? renamer = null)
    {
        _engine = engine ?? new TransferEngine();
        _renamer = renamer ?? new IntelligentRenamer();
    }

    public async Task<SmartCopyResult> ExecuteAsync(
        IReadOnlyList<string> sourceFiles,
        string destinationFolder,
        IProgress<TransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var items = _renamer.BuildItems(sourceFiles, destinationFolder);
        var clock = Stopwatch.StartNew();
        var copied = await _engine.CopyAsync(items, progress, cancellationToken);
        clock.Stop();
        return new SmartCopyResult(copied, destinationFolder, clock.Elapsed);
    }
}
