using SmartCopy.Core;

namespace SmartCopy.Tests;

public sealed class IntelligentRenamerTests
{
    private static string MakeDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "smartcopy_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void FolderBasedNaming_UsesFolderNameAndExtensionCount()
    {
        using var tmp = new TempDir(MakeDir());
        string folder = Path.Combine(tmp.Path, "vacation");
        Directory.CreateDirectory(folder);
        string source = Path.Combine(tmp.Path, "photo.jpg");
        File.WriteAllBytes(Path.Combine(folder, "existing_1.jpg"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(folder, "existing_2.jpg"), [1, 2, 3]);

        var renamer = new IntelligentRenamer(RenameScheme.FolderBased);
        string result = renamer.GenerateSmartFilePath(source, folder);

        Assert.Equal("vacation_3.jpg", Path.GetFileName(result));
    }

    [Fact]
    public void FolderBasedNaming_SkipsCollisions()
    {
        using var tmp = new TempDir(MakeDir());
        string folder = Path.Combine(tmp.Path, "vacation");
        Directory.CreateDirectory(folder);
        string source = Path.Combine(tmp.Path, "photo.jpg");
        File.WriteAllBytes(Path.Combine(folder, "vacation_1.jpg"), [1]);
        File.WriteAllBytes(Path.Combine(folder, "vacation_2.jpg"), [1]);

        var renamer = new IntelligentRenamer();
        string result = renamer.GenerateSmartFilePath(source, folder);

        Assert.Equal("vacation_3.jpg", Path.GetFileName(result));
    }

    [Fact]
    public void SequentialNaming_UsesSourceStem()
    {
        using var tmp = new TempDir(MakeDir());
        string source = Path.Combine(tmp.Path, "my_photo.jpg");

        var renamer = new IntelligentRenamer(RenameScheme.Sequential);
        string result = renamer.GenerateSmartFilePath(source, tmp.Path);

        Assert.Equal("my_photo_1.jpg", Path.GetFileName(result));
    }

    [Fact]
    public void BuildItems_MapsAllSources()
    {
        using var tmp = new TempDir(MakeDir());
        var renamer = new IntelligentRenamer();
        var items = renamer.BuildItems(
            [Path.Combine(tmp.Path, "a.txt"), Path.Combine(tmp.Path, "b.txt")], tmp.Path);

        Assert.Equal(2, items.Count);
        Assert.All(items, i => Assert.Equal(tmp.Path, Path.GetDirectoryName(i.DestinationPath)));
    }
}

public sealed class TransferEngineTests
{
    [Fact]
    public async Task CopyFile_ProducesIdenticalContent()
    {
        using var src = new TempDir(Path.Combine(Path.GetTempPath(), "smartcopy_src_" + Guid.NewGuid().ToString("N")));
        using var dst = new TempDir(Path.Combine(Path.GetTempPath(), "smartcopy_dst_" + Guid.NewGuid().ToString("N")));

        var payload = new byte[256 * 1024];
        Random.Shared.NextBytes(payload);
        string sourceFile = Path.Combine(src.Path, "data.bin");
        File.WriteAllBytes(sourceFile, payload);

        var engine = new TransferEngine(bufferSize: 64 * 1024, parallelLimit: 2);
        var copied = await engine.CopyAsync([new TransferItem(sourceFile, Path.Combine(dst.Path, "out.bin"))]);

        Assert.Single(copied);
        Assert.Equal(payload, File.ReadAllBytes(Path.Combine(dst.Path, "out.bin")));
    }

    [Fact]
    public async Task CopyFile_ReportsProgressAndCompletes()
    {
        using var src = new TempDir(Path.Combine(Path.GetTempPath(), "smartcopy_src_" + Guid.NewGuid().ToString("N")));
        using var dst = new TempDir(Path.Combine(Path.GetTempPath(), "smartcopy_dst_" + Guid.NewGuid().ToString("N")));

        string sourceFile = Path.Combine(src.Path, "file.txt");
        File.WriteAllText(sourceFile, new string('x', 512 * 1024));

        var engine = new TransferEngine();
        var reports = new List<TransferProgress>();
        var progress = new Progress<TransferProgress>(reports.Add);

        await engine.CopyAsync([new TransferItem(sourceFile, Path.Combine(dst.Path, "copy.txt"))], progress);

        Assert.True(reports.Count > 0);
        Assert.True(reports[^1].Completed);
        Assert.Equal(1, reports[^1].FilesTotal);
        Assert.True(File.Exists(Path.Combine(dst.Path, "copy.txt")));
    }

    [Fact]
    public async Task CopyFile_CancelsCleanly()
    {
        using var src = new TempDir(Path.Combine(Path.GetTempPath(), "smartcopy_src_" + Guid.NewGuid().ToString("N")));
        using var dst = new TempDir(Path.Combine(Path.GetTempPath(), "smartcopy_dst_" + Guid.NewGuid().ToString("N")));

        string sourceFile = Path.Combine(src.Path, "big.bin");
        File.WriteAllBytes(sourceFile, new byte[1024]);

        var engine = new TransferEngine();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            engine.CopyAsync([new TransferItem(sourceFile, Path.Combine(dst.Path, "big.bin"))], null, cts.Token));
    }
}

internal sealed class TempDir : IDisposable
{
    public string Path { get; }
    public TempDir(string path)
    {
        Path = path;
        Directory.CreateDirectory(path);
    }

    public void Dispose()
    {
        try { Directory.Delete(Path, true); }
        catch { /* best-effort cleanup */ }
    }
}
