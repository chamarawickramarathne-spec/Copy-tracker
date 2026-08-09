using System.Diagnostics;
using System.Text.RegularExpressions;

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

        var tags = Regex.Matches(output, @"refs/tags/(?:v)?(\d+\.\d+\.\d+)\^?\{?\}")
            .Select(m => TryParse(m.Groups[1].Value))
            .Where(v => v is not null)
            .Cast<Version>();

        var latest = tags.DefaultIfEmpty(null).Max();
        return latest is not null && latest > CurrentVersion ? latest : null;
    }

    public async Task ApplyUpdateAsync(string repository, Version version, IProgress<string> progress, CancellationToken ct = default)
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

        progress.Report("Applying update...");

        string script = Path.Combine(appDir, "apply_update.cmd");
        string quotedExe = $"\"{exe}\"";
        string quotedStaged = $"\"{staged}\"";
        string quotedScript = $"\"{script}\"";
        string lines =
            "@echo off\r\n" +
            "timeout /t 2 /nobreak >nul\r\n" +
            $"del {quotedExe}\r\n" +
            $"move /y {quotedStaged} {quotedExe}\r\n" +
            $"start \"\" {quotedExe} --tray\r\n" +
            $"del {quotedScript}\r\n";
        File.WriteAllText(script, lines);

        var launch = new ProcessStartInfo(script) { UseShellExecute = true, CreateNoWindow = true };
        Process.Start(launch);
    }

    private static Version? TryParse(string text)
        => Version.TryParse(text, out var v) ? v : null;
}
