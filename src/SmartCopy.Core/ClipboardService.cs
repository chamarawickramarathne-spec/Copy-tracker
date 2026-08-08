using System.Text;

namespace SmartCopy.Core;

public static class ClipboardService
{
    public static IReadOnlyList<string>? TryGetFileDropList()
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            if (NativeMethods.OpenClipboard(IntPtr.Zero))
            {
                try
                {
                    IntPtr handle = NativeMethods.GetClipboardData(NativeMethods.CfHdrop);
                    if (handle == IntPtr.Zero) return null;

                    uint count = NativeMethods.DragQueryFile(handle, uint.MaxValue, null, 0);
                    if (count == 0) return null;

                    var files = new List<string>((int)count);
                    for (uint i = 0; i < count; i++)
                    {
                        uint length = NativeMethods.DragQueryFile(handle, i, null, 0);
                        var builder = new StringBuilder((int)length + 1);
                        NativeMethods.DragQueryFile(handle, i, builder, (uint)builder.Capacity);
                        files.Add(builder.ToString());
                    }
                    return files;
                }
                finally
                {
                    NativeMethods.CloseClipboard();
                }
            }
            Thread.Sleep(15);
        }
        return null;
    }
}
