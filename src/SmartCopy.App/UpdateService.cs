using System.Diagnostics;
using System.Net;
using System.Text.Json;
using SmartCopy.Core;

namespace SmartCopy.App;

public sealed class UpdateService
{
    public const string UpdateAssetName = "SmartCopy.exe";

    public Version CurrentVersion { get; } =
        typeof(UpdateService).Assembly.GetName().Version ?? new Version(1, 0, 0);

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SmartCopy-Updater");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    public async Task<Version?> CheckForUpdatesAsync(string repository, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(repository)) return null;

        // "releases/latest" only reports fully published releases (never a bare git tag),
        // so the app never sees a version whose binary does not exist yet.
        string url = $"https://api.github.com/repos/{repository}/releases/latest";
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden) return null;
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var latest = GitHubReleaseInfo.GetPublishedVersion(json, UpdateAssetName);
        return latest is not null && latest > CurrentVersion ? latest : null;
    }

    public async Task ApplyUpdateAsync(string repository, Version version, IProgress<string> progress, CancellationToken ct = default)
    {
        await DownloadUpdateAsync(repository, version, progress, ct).ConfigureAwait(false);
        ScheduleApplyUpdate();
    }

    public async Task DownloadUpdateAsync(string repository, Version version, IProgress<string> progress, CancellationToken ct = default)
    {
        string appDir = AppContext.BaseDirectory;
        string exe = Path.Combine(appDir, "SmartCopy.exe");
        if (!File.Exists(exe))
            throw new InvalidOperationException("Could not locate SmartCopy.exe for update.");

        // Resolve the exact asset URL from the published release instead of assuming a fixed path.
        string assetUrl = await ResolveAssetUrlAsync(repository, version, ct).ConfigureAwait(false);

        string staged = Path.Combine(appDir, "SmartCopy.exe.new");
        progress.Report($"Downloading v{version}...");
        using (var fs = new FileStream(staged, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
        {
            using var response = await Http.GetAsync(assetUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await response.Content.CopyToAsync(fs, ct).ConfigureAwait(false);
        }
    }

    private static async Task<string> ResolveAssetUrlAsync(string repository, Version version, CancellationToken ct)
    {
        string url = $"https://api.github.com/repos/{repository}/releases/tags/v{version}";
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new InvalidOperationException(
                $"Release v{version} is not published yet. The update becomes available only after the GitHub release is created.");
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        string? downloadUrl = GitHubReleaseInfo.GetAssetUrl(json, UpdateAssetName);
        if (downloadUrl is null)
            throw new InvalidOperationException(
                $"Release v{version} has no {UpdateAssetName} asset yet (it may still be uploading). Try again shortly.");
        return downloadUrl;
    }

    public void ScheduleApplyUpdate()
    {
        string appDir = AppContext.BaseDirectory;
        string exe = Path.Combine(appDir, "SmartCopy.exe");
        string staged = Path.Combine(appDir, "SmartCopy.exe.new");
        if (!File.Exists(staged))
            throw new InvalidOperationException("No downloaded update to apply.");

        string script = Path.Combine(appDir, "apply_update.cmd");
        string quotedExe = $"\"{exe}\"";
        string quotedStaged = $"\"{staged}\"";
        string quotedScript = $"\"{script}\"";
        string lines =
            "@echo off\r\n" +
            "timeout /t 2 /nobreak >nul\r\n" +
            ":wait\r\n" +
            $"del /q {quotedExe} >nul 2>&1\r\n" +
            $"if exist {quotedExe} ( timeout /t 1 /nobreak >nul & goto wait )\r\n" +
            $"move /y {quotedStaged} {quotedExe} >nul\r\n" +
            $"start \"\" {quotedExe} --tray\r\n" +
            $"del /q {quotedScript} >nul 2>&1\r\n";
        File.WriteAllText(script, lines);

        var launch = new ProcessStartInfo(script) { UseShellExecute = true, CreateNoWindow = true };
        Process.Start(launch);
    }
}

public static class GitHubReleaseInfo
{
    public static Version? GetPublishedVersion(string json, string assetName)
    {
        using var doc = JsonDocument.Parse(json);
        string tag = doc.RootElement.GetProperty("tag_name").GetString()?.TrimStart('v') ?? string.Empty;
        if (!Version.TryParse(tag, out var version)) return null;
        return HasUploadedAsset(doc.RootElement, assetName) ? version : null;
    }

    public static string? GetAssetUrl(string json, string assetName)
    {
        using var doc = JsonDocument.Parse(json);
        foreach (var asset in doc.RootElement.GetProperty("assets").EnumerateArray())
        {
            if (!string.Equals(asset.GetProperty("name").GetString(), assetName, StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(asset.GetProperty("state").GetString(), "uploading", StringComparison.OrdinalIgnoreCase)) continue;
            return asset.GetProperty("browser_download_url").GetString();
        }
        return null;
    }

    private static bool HasUploadedAsset(JsonElement release, string assetName)
        => release.GetProperty("assets").EnumerateArray()
            .Any(a => string.Equals(a.GetProperty("name").GetString(), assetName, StringComparison.OrdinalIgnoreCase)
                      && !string.Equals(a.GetProperty("state").GetString(), "uploading", StringComparison.OrdinalIgnoreCase));
}
