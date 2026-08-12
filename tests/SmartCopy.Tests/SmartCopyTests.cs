using SmartCopy.App;
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

        var renamer = new IntelligentRenamer(RenameFormat.UnderscoreWithName);
        string result = renamer.GenerateSmartFilePath(source, folder);

        Assert.Equal("photo_vacation_3.jpg", Path.GetFileName(result));
    }

    [Fact]
    public void FolderBasedNaming_SkipsCollisions()
    {
        using var tmp = new TempDir(MakeDir());
        string folder = Path.Combine(tmp.Path, "vacation");
        Directory.CreateDirectory(folder);
        string source = Path.Combine(tmp.Path, "photo.jpg");
        File.WriteAllBytes(Path.Combine(folder, "photo_vacation_1.jpg"), [1]);
        File.WriteAllBytes(Path.Combine(folder, "photo_vacation_2.jpg"), [1]);

        var renamer = new IntelligentRenamer();
        string result = renamer.GenerateSmartFilePath(source, folder);

        Assert.Equal("photo_vacation_3.jpg", Path.GetFileName(result));
    }

    [Fact]
    public void FolderWithGaps_NumberContinuesFromFileCount()
    {
        using var tmp = new TempDir(MakeDir());
        string folder = Path.Combine(tmp.Path, "chethana");
        Directory.CreateDirectory(folder);
        string[] existing =
        [
            "a_chethana_1.jpg", "b_chethana_1.jpg", "c_chethana_1.jpg",
            "d_chethana_4.jpg", "e_chethana_4.jpg", "f_chethana_4.jpg",
        ];
        foreach (string name in existing) File.WriteAllBytes(Path.Combine(folder, name), [1]);

        var renamer = new IntelligentRenamer();
        string result = renamer.GenerateSmartFilePath(Path.Combine(tmp.Path, "photo.jpg"), folder);

        Assert.Equal("photo_chethana_6.jpg", Path.GetFileName(result));
    }

    [Fact]
    public void BuildItems_DifferentNames_SameBatch_ConsecutiveNumbers()
    {
        using var tmp = new TempDir(MakeDir());
        string folder = Path.Combine(tmp.Path, "chethana");
        Directory.CreateDirectory(folder);
        string[] existing =
        [
            "a_chethana_1.jpg", "b_chethana_1.jpg", "c_chethana_1.jpg",
            "d_chethana_4.jpg", "e_chethana_4.jpg", "f_chethana_4.jpg",
        ];
        foreach (string name in existing) File.WriteAllBytes(Path.Combine(folder, name), [1]);

        var renamer = new IntelligentRenamer();
        var items = renamer.BuildItems(
            [Path.Combine(tmp.Path, "photo.jpg"), Path.Combine(tmp.Path, "vacation.jpg")], folder);

        Assert.Equal("photo_chethana_6.jpg", Path.GetFileName(items[0].DestinationPath));
        Assert.Equal("vacation_chethana_7.jpg", Path.GetFileName(items[1].DestinationPath));
    }

    [Fact]
    public void SequentialNaming_UsesSourceStemAndFolder()
    {
        using var tmp = new TempDir(MakeDir());
        string folder = Path.Combine(tmp.Path, "trip");
        Directory.CreateDirectory(folder);
        string source = Path.Combine(tmp.Path, "my_photo.jpg");

        var renamer = new IntelligentRenamer(RenameFormat.UnderscoreWithName);
        string result = renamer.GenerateSmartFilePath(source, folder);

        Assert.Equal("my_photo_trip_1.jpg", Path.GetFileName(result));
    }

    [Fact]
    public void SpaceWithName_UsesSpaces()
    {
        using var tmp = new TempDir(MakeDir());
        string folder = Path.Combine(tmp.Path, "vacation");
        Directory.CreateDirectory(folder);
        string source = Path.Combine(tmp.Path, "photo.jpg");

        var renamer = new IntelligentRenamer(RenameFormat.SpaceWithName);
        string result = renamer.GenerateSmartFilePath(source, folder);

        Assert.Equal("photo vacation 1.jpg", Path.GetFileName(result));
    }

    [Fact]
    public void SpaceFolderNumber_UsesFolderAndNumberOnly()
    {
        using var tmp = new TempDir(MakeDir());
        string folder = Path.Combine(tmp.Path, "vacation");
        Directory.CreateDirectory(folder);
        string source = Path.Combine(tmp.Path, "photo.jpg");

        var renamer = new IntelligentRenamer(RenameFormat.SpaceFolderNumber);
        string result = renamer.GenerateSmartFilePath(source, folder);

        Assert.Equal("vacation 1.jpg", Path.GetFileName(result));
    }

    [Fact]
    public void SpaceFolderNumber_Batch_ConsecutiveNumbers()
    {
        using var tmp = new TempDir(MakeDir());
        string folder = Path.Combine(tmp.Path, "vacation");
        Directory.CreateDirectory(folder);
        for (int i = 1; i <= 6; i++)
            File.WriteAllBytes(Path.Combine(folder, $"a_vacation_{i}.jpg"), [1]);

        var renamer = new IntelligentRenamer(RenameFormat.SpaceFolderNumber);
        var items = renamer.BuildItems(
            [Path.Combine(tmp.Path, "photo.jpg"), Path.Combine(tmp.Path, "trip.jpg")], folder);

        Assert.Equal("vacation 7.jpg", Path.GetFileName(items[0].DestinationPath));
        Assert.Equal("vacation 8.jpg", Path.GetFileName(items[1].DestinationPath));
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

    [Fact]
    public void BuildItems_MultipleSameExtension_GeneratesUniqueDestinations()
    {
        using var tmp = new TempDir(MakeDir());
        string folder = Path.Combine(tmp.Path, "vacation");
        Directory.CreateDirectory(folder);
        string[] sources =
        [
            Path.Combine(tmp.Path, "a.jpg"),
            Path.Combine(tmp.Path, "b.jpg"),
            Path.Combine(tmp.Path, "c.jpg"),
        ];

        var renamer = new IntelligentRenamer(RenameFormat.UnderscoreWithName);
        var items = renamer.BuildItems(sources, folder);

        Assert.Equal(3, items.Count);
        Assert.Equal(3, items.Select(i => i.DestinationPath).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void BuildItems_SequentialScheme_SameStemFromDifferentFolders_GeneratesUnique()
    {
        using var tmp = new TempDir(MakeDir());
        string folderA = Path.Combine(tmp.Path, "a");
        string folderB = Path.Combine(tmp.Path, "b");
        Directory.CreateDirectory(folderA);
        Directory.CreateDirectory(folderB);
        string[] sources = [Path.Combine(folderA, "photo.jpg"), Path.Combine(folderB, "photo.jpg")];

        var renamer = new IntelligentRenamer(RenameFormat.UnderscoreWithName);
        var items = renamer.BuildItems(sources, tmp.Path);

        Assert.Equal(2, items.Count);
        Assert.Equal(2, items.Select(i => i.DestinationPath).Distinct(StringComparer.OrdinalIgnoreCase).Count());
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

    [Fact]
    public async Task CopyAsync_MultipleSameExtension_AllSucceed()
    {
        using var src = new TempDir(Path.Combine(Path.GetTempPath(), "smartcopy_src_" + Guid.NewGuid().ToString("N")));
        using var dst = new TempDir(Path.Combine(Path.GetTempPath(), "smartcopy_dst_" + Guid.NewGuid().ToString("N")));

        for (int i = 0; i < 3; i++)
        {
            var bytes = new byte[64 * 1024];
            bytes[0] = (byte)(i + 1);
            bytes[^1] = (byte)(i + 1);
            File.WriteAllBytes(Path.Combine(src.Path, $"img{i}.jpg"), bytes);
        }

        var items = new IntelligentRenamer(RenameFormat.UnderscoreWithName).BuildItems(
            Directory.GetFiles(src.Path, "*.jpg"), dst.Path);
        var engine = new TransferEngine(parallelLimit: 4);

        var copied = await engine.CopyAsync(items);

        Assert.Equal(3, copied.Count);
        Assert.Equal(3, copied.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(3, Directory.GetFiles(dst.Path, "*.jpg").Length);
    }

    [Fact]
    public async Task CopyAsync_DuplicateDestinations_ThrowsClearError()
    {
        using var src = new TempDir(Path.Combine(Path.GetTempPath(), "smartcopy_src_" + Guid.NewGuid().ToString("N")));
        using var dst = new TempDir(Path.Combine(Path.GetTempPath(), "smartcopy_dst_" + Guid.NewGuid().ToString("N")));

        string sourceFile = Path.Combine(src.Path, "data.bin");
        File.WriteAllBytes(sourceFile, new byte[16]);

        var engine = new TransferEngine(parallelLimit: 2);
        var items = new[]
        {
            new TransferItem(sourceFile, Path.Combine(dst.Path, "out.bin")),
            new TransferItem(sourceFile, Path.Combine(dst.Path, "out.bin")),
        };

        await Assert.ThrowsAsync<ArgumentException>(() => engine.CopyAsync(items));
    }
}

public sealed class GitHubReleaseInfoTests
{
    private const string LatestJson = """
        {
          "tag_name": "v1.0.11",
          "assets": [
            { "name": "SmartCopy.exe", "state": "uploaded", "browser_download_url": "https://github.com/owner/repo/releases/download/v1.0.11/SmartCopy.exe" },
            { "name": "SmartCopySetup_1.0.11.exe", "state": "uploaded", "browser_download_url": "https://github.com/owner/repo/releases/download/v1.0.11/SmartCopySetup_1.0.11.exe" }
          ]
        }
        """;

    [Fact]
    public void GetPublishedVersion_ReturnsVersionWhenAssetUploaded()
    {
        Assert.Equal(new Version(1, 0, 11), GitHubReleaseInfo.GetPublishedVersion(LatestJson, "SmartCopy.exe"));
    }

    [Fact]
    public void GetPublishedVersion_ReturnsNullWhenAssetStillUploading()
    {
        string json = LatestJson.Replace("\"uploaded\"", "\"uploading\"", StringComparison.Ordinal);

        Assert.Null(GitHubReleaseInfo.GetPublishedVersion(json, "SmartCopy.exe"));
    }

    [Fact]
    public void GetPublishedVersion_ReturnsNullWhenAssetMissing()
    {
        string json = """{"tag_name":"v1.0.11","assets":[]}""";

        Assert.Null(GitHubReleaseInfo.GetPublishedVersion(json, "SmartCopy.exe"));
    }

    [Fact]
    public void GetPublishedVersion_ReturnsNullForInvalidTag()
    {
        string json = """{"tag_name":"not-a-version","assets":[]}""";

        Assert.Null(GitHubReleaseInfo.GetPublishedVersion(json, "SmartCopy.exe"));
    }

    [Fact]
    public void GetAssetUrl_ReturnsRealDownloadUrlForMatchingAsset()
    {
        string? url = GitHubReleaseInfo.GetAssetUrl(LatestJson, "SmartCopy.exe");

        Assert.Equal("https://github.com/owner/repo/releases/download/v1.0.11/SmartCopy.exe", url);
    }

    [Fact]
    public void GetAssetUrl_ReturnsNullForMissingOrUploadingAsset()
    {
        Assert.Null(GitHubReleaseInfo.GetAssetUrl(LatestJson, "Missing.exe"));
        string uploading = LatestJson.Replace("\"uploaded\"", "\"uploading\"", StringComparison.Ordinal);
        Assert.Null(GitHubReleaseInfo.GetAssetUrl(uploading, "SmartCopy.exe"));
    }
}

public sealed class SettingsServiceTests
{
    [Fact]
    public void ResolveRepository_Empty_FallsBackToDefault()
    {
        Assert.Equal(SettingsService.DefaultUpdateRepository, SettingsService.ResolveRepository(""));
        Assert.Equal(SettingsService.DefaultUpdateRepository, SettingsService.ResolveRepository(null));
        Assert.Equal(SettingsService.DefaultUpdateRepository, SettingsService.ResolveRepository("   "));
    }

    [Fact]
    public void ResolveRepository_Configured_ReturnsTrimmedValue()
    {
        Assert.Equal("owner/repo", SettingsService.ResolveRepository("  owner/repo  "));
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
