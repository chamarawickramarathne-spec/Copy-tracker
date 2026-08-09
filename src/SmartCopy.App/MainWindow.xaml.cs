using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using SmartCopy.Core;

namespace SmartCopy.App;

public sealed record HistoryEntry(string Time, string Destination, int FileCount, string Duration);

public partial class MainWindow : Window
{
    private readonly SettingsService _settings;
    private readonly UpdateService _updater = new();
    private readonly ObservableCollection<HistoryEntry> _history = new();

    public MainWindow(SettingsService settings)
    {
        InitializeComponent();
        _settings = settings;
        LoadSettings();
        lstHistory.ItemsSource = _history;
        string version = VersionText(_updater.CurrentVersion);
        Title = $"SmartCopy {version}";
        txtAppVersion.Text = version;
        txtVersion.Text = $"Version {version}";
    }

    private static string VersionText(Version v)
        => v.Revision > 0 ? v.ToString() : $"{v.Major}.{v.Minor}.{v.Build}";

    public void AddHistory(string[] files, string folder, TimeSpan elapsed)
        => _history.Insert(0, new HistoryEntry(
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), folder, files.Length, $"{elapsed.TotalSeconds:0.#}s"));

    private void LoadSettings()
    {
        sldBuffer.Value = _settings.BufferSizeKb;
        sldParallel.Value = _settings.ParallelLimit;
        chkOpenFolder.IsChecked = _settings.OpenFolderWhenDone;
        chkAutoStart.IsChecked = _settings.AutoStart;
        chkStartMinimized.IsChecked = _settings.StartMinimized;
        chkAutoUpdate.IsChecked = _settings.AutoUpdate;
        UpdateLabels();
    }

    private void UpdateLabels()
    {
        lblBuffer.Text = $"{_settings.BufferSizeKb} KB ({(double)_settings.BufferSize / (1024 * 1024):0.0} MB)";
        lblParallel.Text = _settings.ParallelLimit.ToString();
    }

    private void OnBufferChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded) return;
        _settings.BufferSizeKb = (int)e.NewValue;
        UpdateLabels();
    }

    private void OnParallelChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded) return;
        _settings.ParallelLimit = (int)e.NewValue;
        UpdateLabels();
    }

    private void OnBrowse(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Choose destination folder" };
        if (dialog.ShowDialog(this) == true)
            txtDest.Text = dialog.FolderName;
    }

    private void OnCopyNow(object sender, RoutedEventArgs e)
    {
        string[] sources = txtSources.Text
            .Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => File.Exists(p) || Directory.Exists(p))
            .ToArray();
        string dest = txtDest.Text.Trim();

        if (sources.Length == 0) { txtManualStats.Text = "Enter at least one valid source path."; return; }
        if (string.IsNullOrEmpty(dest) || !Directory.Exists(dest)) { txtManualStats.Text = "Enter a valid destination folder."; return; }

        btnCopy.IsEnabled = false;
        manualBar.Value = 0;
        txtManualStats.Text = "Copying...";
        var app = (App)Application.Current;
        app.StartTransfer(sources, dest,
            onProgress: p => { manualBar.Value = p.TotalBytes > 0 ? (double)p.TotalBytesCopied / p.TotalBytes * 100 : 0; txtManualStats.Text = $"Copied {p.FilesDone} of {p.FilesTotal} files"; },
            onDone: r =>
            {
                btnCopy.IsEnabled = true;
                if (r is null) return;
                manualBar.Value = 100;
                txtManualStats.Text = $"Completed — {r.CopiedFiles.Count} file(s) in {r.Elapsed.TotalSeconds:0.#}s";
            });
    }

    private void OnSaveSettings(object sender, RoutedEventArgs e)
    {
        _settings.OpenFolderWhenDone = chkOpenFolder.IsChecked == true;
        _settings.AutoStart = chkAutoStart.IsChecked == true;
        _settings.StartMinimized = chkStartMinimized.IsChecked == true;
        _settings.AutoUpdate = chkAutoUpdate.IsChecked == true;
        _settings.Save();
        _settings.ApplyAutoStart();
        txtSettingsStatus.Text = "Settings saved.";
    }

    private void OnClearHistory(object sender, RoutedEventArgs e) => _history.Clear();

    private async void OnCheckUpdate(object sender, RoutedEventArgs e)
    {
        string repo = _settings.UpdateRepository.Trim();
        if (string.IsNullOrWhiteSpace(repo)) return;

        btnUpdate.IsEnabled = false;
        btnUpdate.Content = "Checking...";
        try
        {
            var latest = await _updater.CheckForUpdatesAsync(repo);
            if (latest is null)
            {
                btnUpdate.Content = "Up to date";
            }
            else
            {
                btnUpdate.Content = $"Downloading {latest}...";
                await _updater.DownloadUpdateAsync(repo, latest, new Progress<string>());
                btnUpdate.Content = "Restarting...";
                ((App)Application.Current).RestartForUpdate();
                return;
            }
        }
        catch
        {
            btnUpdate.Content = "Check failed";
        }

        btnUpdate.IsEnabled = true;
        await Task.Delay(3000);
        btnUpdate.Content = "Check for updates";
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!((App)Application.Current).IsExiting)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }
}
