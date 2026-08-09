using System.Diagnostics;
using SmartCopy.Core;

namespace SmartCopy.App;

public sealed class UpdateService
{
    public Version CurrentVersion { get; } =
        typeof(UpdateService).Assembly.GetName().Version ?? new Version(1, 0, 0);

    public async Task<Version?> CheckForUpdatesAsync(string repository, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(repository)) return null;

        var psi = new ProcessStartInfo("git", $"ls-remote --tags https://github.com/{repository}.git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var proc = Process.Start(psi);
        if (proc is null) return null;

        string output = await proc.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
        await proc.WaitForExitAsync(ct).ConfigureAwait(false);
        if (proc.ExitCode != 0) return null;

        var latest = GitTagParser.ParseVersions(output).DefaultIfEmpty(null).Max();
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

        string url = $"https://github.com/{repository}/releases/download/v{version}/SmartCopy.exe";
        string staged = Path.Combine(appDir, "SmartCopy.exe.new");

        progress.Report($"Downloading v{version}...");
        using (var http = new HttpClient())
        using (var fs = new FileStream(staged, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
        {
            var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await response.Content.CopyToAsync(fs, ct).ConfigureAwait(false);
        }
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
