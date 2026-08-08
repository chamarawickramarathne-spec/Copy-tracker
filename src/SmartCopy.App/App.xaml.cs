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
    private SettingsService _settings = new();
    private CancellationTokenSource? _currentCts;
    private string _lastFolder = string.Empty;

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
    }

    private bool OnInterceptPaste()
    {
        var files = ClipboardService.TryGetFileDropList();
        if (files is null || files.Count == 0) return false;

        IntPtr foreground = NativeMethods.GetForegroundWindow();
        string? windowClass = ExplorerFolderService.GetWindowClassName(foreground);
        if (windowClass is not "CabinetWClass" and not "ExploreWClass") return false;

        string? folder = ExplorerFolderService.GetFolderPathForWindow(foreground);
        if (string.IsNullOrEmpty(folder)) return false;

        string[] snapshot = files.ToArray();
        Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => StartTransfer(snapshot, folder)));
        return true;
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

        var engine = new TransferEngine(_settings.BufferSize, _settings.ParallelLimit);
        var renamer = new IntelligentRenamer((RenameScheme)_settings.RenameScheme);
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
            string message = ex is AggregateException agg
                ? (agg.InnerExceptions.FirstOrDefault()?.Message ?? "Transfer failed.")
                : ex.Message;
            _miniPlayer?.Fail(message);
            onDone?.Invoke(null);
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
