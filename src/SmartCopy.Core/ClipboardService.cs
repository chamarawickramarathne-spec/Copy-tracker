using System.Runtime.InteropServices;
using System.Text;

namespace SmartCopy.Core;

public sealed record ClipboardFileResult(IReadOnlyList<string> Files, bool IsCut);

public static class ClipboardService
{
    private static uint? _preferredDropEffectFormat;

    private static uint PreferredDropEffectFormat =>
        _preferredDropEffectFormat ??= NativeMethods.RegisterClipboardFormat("Preferred DropEffect");

    public static ClipboardFileResult? TryGetClipboardFiles()
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            if (NativeMethods.OpenClipboard(IntPtr.Zero))
            {
                try
                {
                    IntPtr hDrop = NativeMethods.GetClipboardData(NativeMethods.CfHdrop);
                    if (hDrop == IntPtr.Zero) return null;

                    uint count = NativeMethods.DragQueryFile(hDrop, uint.MaxValue, null, 0);
                    if (count == 0) return null;

                    var files = new List<string>((int)count);
                    for (uint i = 0; i < count; i++)
                    {
                        uint length = NativeMethods.DragQueryFile(hDrop, i, null, 0);
                        var builder = new StringBuilder((int)length + 1);
                        NativeMethods.DragQueryFile(hDrop, i, builder, (uint)builder.Capacity);
                        files.Add(builder.ToString());
                    }

                    bool isCut = IsCutOperation();
                    return new ClipboardFileResult(files, isCut);
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

    private static bool IsCutOperation()
    {
        IntPtr hData = NativeMethods.GetClipboardData(PreferredDropEffectFormat);
        if (hData == IntPtr.Zero) return false;

        IntPtr ptr = NativeMethods.GlobalLock(hData);
        if (ptr == IntPtr.Zero) return false;

        try
        {
            uint dropEffect = Marshal.PtrToStructure<uint>(ptr);
            return dropEffect == 2; // DROPEFFECT_MOVE
        }
        finally
        {
            NativeMethods.GlobalUnlock(hData);
        }
    }
}
