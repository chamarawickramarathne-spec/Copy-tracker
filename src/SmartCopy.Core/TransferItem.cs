namespace SmartCopy.Core;

public sealed record TransferItem(string SourcePath, string DestinationPath);

public sealed class TransferProgress
{
    public string CurrentFile { get; init; } = string.Empty;
    public long FileBytesCopied { get; init; }
    public long FileBytesTotal { get; init; }
    public long TotalBytesCopied { get; init; }
    public long TotalBytes { get; init; }
    public double SpeedBytesPerSecond { get; init; }
    public TimeSpan? Remaining { get; init; }
    public int FilesDone { get; init; }
    public int FilesTotal { get; init; }
    public bool Completed { get; init; }
    public bool Cancelled { get; init; }
    public bool Failed { get; init; }
}
