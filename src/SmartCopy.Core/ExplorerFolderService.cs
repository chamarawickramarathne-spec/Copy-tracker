using System.Text;

namespace SmartCopy.Core;

public static class ExplorerFolderService
{
    private static readonly Guid ShellWindowsClsid = new("9BA05972-F6A8-11CF-A442-00A0C90A8F39");

    public static string? GetFolderPathForWindow(IntPtr windowHandle)
    {
        Type? shellType = Type.GetTypeFromCLSID(ShellWindowsClsid);
        if (shellType is null) return null;

        dynamic? shell = Activator.CreateInstance(shellType);
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

    public static string? GetWindowClassName(IntPtr windowHandle)
    {
        var builder = new StringBuilder(256);
        return NativeMethods.GetClassName(windowHandle, builder, builder.Capacity) > 0 ? builder.ToString() : null;
    }
}
