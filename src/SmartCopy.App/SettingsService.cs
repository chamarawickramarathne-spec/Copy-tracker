using System.Text.Json;

namespace SmartCopy.App;

public sealed class SettingsService
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SmartCopy");

    private static readonly string FilePath = Path.Combine(Dir, "settings.json");

    public int BufferSizeKb { get; set; } = 1024;
    public int ParallelLimit { get; set; } = 4;
    public bool AutoStart { get; set; } = true;
    public bool StartMinimized { get; set; } = true;
    public bool OpenFolderWhenDone { get; set; } = true;
    public string UpdateRepository { get; set; } = "chamarawickramarathne-spec/Copy-tracker";
    public bool AutoUpdate { get; set; } = true;

    public int BufferSize => Math.Clamp(BufferSizeKb, 64, 4096) * 1024;

    public static SettingsService Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var loaded = JsonSerializer.Deserialize<SettingsService>(File.ReadAllText(FilePath));
                if (loaded is not null) return loaded;
            }
        }
        catch
        {
            // corrupt settings fall back to defaults
        }
        return new SettingsService();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // settings persistence is best-effort
        }
    }

    public void ApplyAutoStart()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key is null) return;
            if (AutoStart)
            {
                string exe = Environment.ProcessPath ?? string.Empty;
                if (!string.IsNullOrEmpty(exe)) key.SetValue("SmartCopy", $"\"{exe}\" --tray");
            }
            else
            {
                key.DeleteValue("SmartCopy", false);
            }
        }
        catch
        {
            // registry may be locked down in some environments
        }
    }
}
