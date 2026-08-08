using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using SmartCopy.Core;

namespace SmartCopy.App;

public partial class MiniPlayerWindow : Window
{
    private const double CollapsedHeight = 122;
    private const double ExpandedHeight = 336;

    private bool _expanded;
    private double _targetProgress;
    private double _currentProgress;
    private bool _animating;
    private string[] _files = Array.Empty<string>();
    private string _folder = string.Empty;
    private DispatcherTimer? _hideTimer;

    public event Action? CancelRequested;
    public event Action? OpenFolderRequested;

    public MiniPlayerWindow()
    {
        InitializeComponent();
        Hide();
    }

    public void BeginTransfer(string[] files, string folder)
    {
        _files = files;
        _folder = folder;

        _targetProgress = 0;
        _currentProgress = 0;
        bar.Value = 0;
        bar.Foreground = (Brush)FindResource("AccentGradient");
        btnCancel.Visibility = Visibility.Visible;
        btnOpenFolder.Visibility = Visibility.Collapsed;
        txtFile.Text = files.Length > 0 ? Path.GetFileName(files[0]) : string.Empty;
        txtState.Text = $"Copying {files.Length} item{(files.Length == 1 ? "" : "s")}";
        txtStats.Text = "Preparing...";
        txtDoneSummary.Text = string.Empty;

        lstFiles.ItemsSource = files.Select(Path.GetFileName).ToList();

        ShowWidget();
        StartAnimating();
    }

    public void Update(TransferProgress p)
    {
        Dispatcher.VerifyAccess();

        if (p.FilesTotal > 0 && p.TotalBytes > 0)
        {
            _targetProgress = Math.Clamp((double)p.TotalBytesCopied / p.TotalBytes, 0, 1);
            txtState.Text = $"Copying {p.FilesDone + 1} of {p.FilesTotal}";
        }

        string current = Path.GetFileName(p.CurrentFile);
        if (!string.IsNullOrEmpty(current)) txtFile.Text = current;

        string speed = FormatBytes(p.SpeedBytesPerSecond) + "/s";
        string done = FormatBytes(p.TotalBytesCopied);
        string total = FormatBytes(p.TotalBytes);
        string eta = p.Remaining is { } r ? $" · {FormatTime(r)} left" : "";
        txtStats.Text = $"{done} / {total} · {speed}{eta}";
    }

    public void Complete(TimeSpan elapsed)
    {
        _targetProgress = 1;
        SetBarValue(100);
        txtState.Text = "Done";
        txtStats.Text = $"{_files.Length} file(s) copied in {FormatTime(elapsed)}";
        btnCancel.Visibility = Visibility.Collapsed;
        btnOpenFolder.Visibility = Visibility.Visible;
        bar.Foreground = (Brush)FindResource("Success");
        ScheduleHide();
    }

    public void Fail(string message)
    {
        StopAnimating();
        txtState.Text = "Failed";
        txtStats.Text = message;
        btnCancel.Visibility = Visibility.Collapsed;
        btnOpenFolder.Visibility = Visibility.Visible;
        bar.Foreground = (Brush)FindResource("Error");
        ScheduleHide();
    }

    public void Cancelled()
    {
        StopAnimating();
        txtState.Text = "Cancelled";
        txtStats.Text = string.Empty;
        btnCancel.Visibility = Visibility.Collapsed;
        bar.Foreground = (Brush)FindResource("Warning");
        ScheduleHide();
    }

    private void OnCancel(object sender, RoutedEventArgs e) => CancelRequested?.Invoke();

    private void OnOpenFolder(object sender, RoutedEventArgs e)
    {
        OpenFolderRequested?.Invoke();
        ScheduleHide();
    }

    private void OnToggleExpand(object sender, RoutedEventArgs e)
    {
        _expanded = !_expanded;
        gridDetails.Visibility = _expanded ? Visibility.Visible : Visibility.Collapsed;
        AnimateSize(_expanded ? ExpandedHeight : CollapsedHeight);
    }

    private void OnClose(object sender, RoutedEventArgs e) => HideWidget();

    private void AnimateSize(double targetHeight)
    {
        double targetTop = SystemParameters.WorkArea.Bottom - targetHeight - 16;
        var duration = TimeSpan.FromMilliseconds(220);
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        BeginAnimation(HeightProperty, new DoubleAnimation(Height, targetHeight, duration) { EasingFunction = ease });
        BeginAnimation(TopProperty, new DoubleAnimation(Top, targetTop, duration) { EasingFunction = ease });
    }

    private void ShowWidget()
    {
        StopHideTimer();
        Opacity = 0;
        Left = SystemParameters.WorkArea.Right - Width - 16;
        Top = SystemParameters.WorkArea.Bottom - Height - 16;
        Show();
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
    }

    private void HideWidget()
    {
        StopHideTimer();
        Hide();
    }

    private void ScheduleHide()
    {
        StopHideTimer();
        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _hideTimer.Tick += (_, _) => { StopHideTimer(); HideWidget(); };
        _hideTimer.Start();
    }

    private void StopHideTimer()
    {
        _hideTimer?.Stop();
        _hideTimer = null;
    }

    private void StartAnimating()
    {
        if (_animating) return;
        _animating = true;
        CompositionTarget.Rendering += OnRenderFrame;
    }

    private void StopAnimating()
    {
        _animating = false;
        CompositionTarget.Rendering -= OnRenderFrame;
    }

    private void OnRenderFrame(object? sender, EventArgs e)
    {
        _currentProgress += (_targetProgress - _currentProgress) * 0.16;
        if (Math.Abs(_targetProgress - _currentProgress) < 0.0005) _currentProgress = _targetProgress;
        bar.Value = _currentProgress * 100;
    }

    private void SetBarValue(double value)
    {
        _currentProgress = value / 100;
        bar.Value = value;
    }

    private static string FormatBytes(double bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return unit == 0 ? $"{value:0} {units[unit]}" : $"{value:0.0} {units[unit]}";
    }

    private static string FormatTime(TimeSpan t)
        => t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes}m" : $"{t.Minutes}m {t.Seconds}s";

    protected override void OnClosed(EventArgs e)
    {
        StopAnimating();
        StopHideTimer();
        base.OnClosed(e);
    }
}
