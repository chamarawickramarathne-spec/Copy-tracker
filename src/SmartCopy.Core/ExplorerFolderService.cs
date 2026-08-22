using System.Text;

namespace SmartCopy.Core;

public static class ExplorerFolderService
{
    private static readonly Guid ShellWindowsClsid = new("9BA05972-F6A8-11CF-A442-00A0C90A8F39");
    private static readonly object ShellGate = new();
    private static dynamic? _shellWindows;

    /// <summary>
    /// Resolves the folder path of an Explorer window off the calling thread.
    /// Creating/enumerating the ShellWindows COM object takes tens of milliseconds,
    /// so callers on a UI thread should prefer this over the sync overload.
    /// </summary>
    public static Task<string?> GetFolderPathForWindowAsync(IntPtr windowHandle)
        => Task.Run(() => GetFolderPathForWindow(windowHandle));

    public static string? GetFolderPathForWindow(IntPtr windowHandle)
    {
        dynamic? shell = GetShellWindows();
        if (shell is null) return null;

        try
        {
            int count = shell.Count;
            for (int i = 0; i < count; i++)
            {
                dynamic? window = shell.Item(i);
                if (window is null) continue;

                int hwnd = (int)window.HWND;
                if (hwnd == 0 || new IntPtr(hwnd) != windowHandle) continue;

                string url = (string)window.LocationURL;
                if (string.IsNullOrEmpty(url) ||
                    !url.StartsWith("file:", StringComparison.OrdinalIgnoreCase)) return null;

                var uri = new Uri(url);
                return uri.LocalPath;
            }
        }
        catch
        {
            // late-bound COM can throw while a window is being torn down; treat as "not resolvable"
        }

        return null;
    }

    private static dynamic? GetShellWindows()
    {
        if (_shellWindows is not null) return _shellWindows;
        lock (ShellGate)
        {
            if (_shellWindows is not null) return _shellWindows;

            try
            {
                Type? shellType = Type.GetTypeFromCLSID(ShellWindowsClsid);
                if (shellType is null) return null;
                _shellWindows = Activator.CreateInstance(shellType);
            }
            catch
            {
                // COM unavailable right now; retry on the next paste instead of caching the failure
            }
            return _shellWindows;
        }
    }

    public static string? GetWindowClassName(IntPtr windowHandle)
    {
        var builder = new StringBuilder(256);
        return NativeMethods.GetClassName(windowHandle, builder, builder.Capacity) > 0 ? builder.ToString() : null;
    }
}
