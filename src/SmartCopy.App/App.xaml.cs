using System.Windows;
using System.Windows.Threading;
using SmartCopy.Core;
using Application = System.Windows.Application;

namespace SmartCopy.App;

public partial class App : Application
{
    private Mutex? _instanceMutex;
    private TrayIcon? _tray;
    private GlobalKeyboardHook? _hook;
    private MainWindow? _mainWindow;
    private MiniPlayerWindow? _miniPlayer;
    private readonly UpdateService _updater = new();
    private SettingsService _settings = new();
    private CancellationTokenSource? _currentCts;
    private string _lastFolder = string.Empty;
    private volatile bool _replayingPaste;
    private int _activeTransfers;
    private volatile bool _pendingApply;
    private volatile bool _checkingUpdate;

    public bool IsExiting { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _instanceMutex = new Mutex(true, @"Local\SmartCopy.SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        bool trayOnly = e.Args.Contains("--tray");

        _settings = SettingsService.Load();
        _settings.ApplyAutoStart();

        _miniPlayer = new MiniPlayerWindow();
        _miniPlayer.CancelRequested += () => _currentCts?.Cancel();
        _miniPlayer.OpenFolderRequested += OpenDestinationFolder;

        _mainWindow = new MainWindow(_settings);

        _tray = new TrayIcon();
        _tray.OpenRequested += ShowMain;
        _tray.ExitRequested += ExitApp;

        _hook = new GlobalKeyboardHook();
        _hook.InterceptPaste += OnInterceptPaste;

        if (!trayOnly && !_settings.StartMinimized)
        {
            ShowMain();
        }
        else
        {
            _tray.ShowBalloon("SmartCopy",
                "Running in the system tray. Copy files and press Ctrl+V in a folder to smart-copy them.");
        }

        StartUpdateCheck();
    }

    private void StartUpdateCheck()
    {
        if (!_settings.AutoUpdate || string.IsNullOrWhiteSpace(_settings.UpdateRepository)) return;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            _ = CheckAndApplyUpdateAsync();
        };
        timer.Start();
    }

    private async Task CheckAndApplyUpdateAsync()
    {
        if (_checkingUpdate) return;
        _checkingUpdate = true;
        try
        {
            string repo = _settings.UpdateRepository.Trim();
            if (string.IsNullOrWhiteSpace(repo)) return;

            var latest = await _updater.CheckForUpdatesAsync(repo);
            if (latest is null) return;

            var progress = new Progress<string>(s => _tray?.ShowBalloon("SmartCopy Update", s));
            await _updater.DownloadUpdateAsync(repo, latest, progress);

            _tray?.ShowBalloon("SmartCopy Update", $"SmartCopy {latest} downloaded. Restarting to apply...");
            _pendingApply = true;
            if (Interlocked.CompareExchange(ref _activeTransfers, 0, 0) == 0) ApplyPendingUpdate();
        }
        catch
        {
            // silent — the update check runs again on the next app start
        }
        finally
        {
            _checkingUpdate = false;
        }
    }

    private void ApplyPendingUpdate()
    {
        if (!_pendingApply) return;
        _pendingApply = false;
        if (Interlocked.CompareExchange(ref _activeTransfers, 0, 0) != 0) return;
        RestartForUpdate();
    }

    public void RestartForUpdate()
    {
        try { _updater.ScheduleApplyUpdate(); }
        catch { return; }
        IsExiting = true;
        _currentCts?.Cancel();
        _hook?.Dispose();
        _tray?.Dispose();
        _miniPlayer?.Close();
        Shutdown();
    }

    private bool OnInterceptPaste()
    {
        var files = ClipboardService.TryGetFileDropList();
        if (_replayingPaste || files is null || files.Count == 0) return false;

        IntPtr foreground = NativeMethods.GetForegroundWindow();
        string? windowClass = ExplorerFolderService.GetWindowClassName(foreground);
        if (windowClass is not "CabinetWClass" and not "ExploreWClass") return false;

        // Note: COM (IShellWindows) cannot be called from inside this hook callback
        // (RPC_E_CANTCALLOUT_ININPUTSYNCCALL), so folder resolution happens on the
        // UI thread. The paste is suppressed optimistically; if the folder cannot
        // be resolved we replay Ctrl+V so the normal paste still happens.
        string[] snapshot = files.ToArray();
        Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => ResolveAndTransfer(snapshot)));
        return true;
    }

    private void ResolveAndTransfer(string[] files)
    {
        IntPtr foreground = NativeMethods.GetForegroundWindow();
        string? windowClass = ExplorerFolderService.GetWindowClassName(foreground);
        if (windowClass is not "CabinetWClass" and not "ExploreWClass") return;

        string? folder = ExplorerFolderService.GetFolderPathForWindow(foreground);
        if (string.IsNullOrEmpty(folder))
        {
            ReplayPaste();
            return;
        }

        StartTransfer(files, folder);
    }

    private void ReplayPaste()
    {
        _replayingPaste = true;
        try
        {
            NativeMethods.keybd_event(NativeMethods.VkControl, 0, 0, UIntPtr.Zero);
            NativeMethods.keybd_event(NativeMethods.VkV, 0, 0, UIntPtr.Zero);
            NativeMethods.keybd_event(NativeMethods.VkV, 0, NativeMethods.KeyeventfKeyUp, UIntPtr.Zero);
            NativeMethods.keybd_event(NativeMethods.VkControl, 0, NativeMethods.KeyeventfKeyUp, UIntPtr.Zero);
        }
        finally
        {
            var release = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            release.Tick += (_, _) =>
            {
                _replayingPaste = false;
                release.Stop();
            };
            release.Start();
        }
    }

    public async void StartTransfer(
        string[] files,
        string folder,
        Action<TransferProgress>? onProgress = null,
        Action<SmartCopyResult?>? onDone = null)
    {
        _currentCts?.Cancel();
        _currentCts?.Dispose();
        var cts = new CancellationTokenSource();
        _currentCts = cts;
        _lastFolder = folder;
        Interlocked.Increment(ref _activeTransfers);

        var engine = new TransferEngine(_settings.BufferSize, _settings.ParallelLimit);
        var renamer = new IntelligentRenamer();
        var orchestrator = new SmartCopyOrchestrator(engine, renamer);

        var progress = new Progress<TransferProgress>(p =>
        {
            onProgress?.Invoke(p);
            _miniPlayer?.Update(p);
        });

        _miniPlayer?.BeginTransfer(files, folder);

        try
        {
            var result = await orchestrator.ExecuteAsync(files, folder, progress, cts.Token);
            _miniPlayer?.Complete(result.Elapsed);
            _mainWindow?.AddHistory(files, folder, result.Elapsed);
            if (_settings.OpenFolderWhenDone) OpenDestinationFolder();
            onDone?.Invoke(result);
        }
        catch (OperationCanceledException)
        {
            _miniPlayer?.Cancelled();
            onDone?.Invoke(null);
        }
        catch (Exception ex)
        {
            string message;
            if (ex is AggregateException agg && agg.InnerExceptions.Count > 0)
            {
                var lines = agg.InnerExceptions.Select(e => e.Message).Distinct().Take(5).ToList();
                message = string.Join(Environment.NewLine, lines);
                if (agg.InnerExceptions.Count > lines.Count)
                {
                    message += Environment.NewLine + $"… and {agg.InnerExceptions.Count - lines.Count} more";
                }
            }
            else
            {
                message = ex.Message;
            }
            _miniPlayer?.Fail(message);
            onDone?.Invoke(null);
        }
        finally
        {
            Interlocked.Decrement(ref _activeTransfers);
            ApplyPendingUpdate();
        }
    }

    private void ShowMain()
    {
        if (_mainWindow is null) return;
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void OpenDestinationFolder()
    {
        if (string.IsNullOrEmpty(_lastFolder)) return;
        try
        {
            System.Diagnostics.Process.Start("explorer.exe", $"\"{_lastFolder}\"");
        }
        catch
        {
            // best-effort
        }
    }

    private void ExitApp()
    {
        IsExiting = true;
        _currentCts?.Cancel();
        _hook?.Dispose();
        _tray?.Dispose();
        _miniPlayer?.Close();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hook?.Dispose();
        _tray?.Dispose();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
