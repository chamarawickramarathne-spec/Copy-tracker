using System.Drawing;
using System.Windows.Forms;

namespace SmartCopy.App;

public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;

    public event Action? OpenRequested;
    public event Action? ExitRequested;

    public TrayIcon()
    {
        _icon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "SmartCopy — intelligent file transfer",
            Visible = true,
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open SmartCopy", null, (_, _) => OpenRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke());
        _icon.ContextMenuStrip = menu;
        _icon.DoubleClick += (_, _) => OpenRequested?.Invoke();
    }

    public void ShowBalloon(string title, string message)
        => _icon.ShowBalloonTip(4000, title, message, ToolTipIcon.Info);

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Dispose();
    }

    private static Icon LoadIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/smartcopy.ico");
            var stream = System.Windows.Application.GetResourceStream(uri)?.Stream;
            if (stream is not null)
            {
                using (stream) return new Icon(stream);
            }
        }
        catch
        {
            // fall through to default icon
        }
        return SystemIcons.Application;
    }
}
