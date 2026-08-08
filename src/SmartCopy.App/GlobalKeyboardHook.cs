using System.Runtime.InteropServices;

namespace SmartCopy.App;

public sealed class GlobalKeyboardHook : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeydown = 0x0100;
    private const int WmQuit = 0x0012;
    private const byte VkV = 0x56;
    private const byte VkControl = 0x11;
    private const byte VkMenu = 0x12;

    public event Func<bool>? InterceptPaste;

    private readonly LowLevelKeyboardProc _proc;
    private readonly Thread _thread;
    private IntPtr _hookId;
    private volatile bool _running;

    public GlobalKeyboardHook()
    {
        _proc = HookCallback;
        _thread = new Thread(Run) { IsBackground = true, Name = "SmartCopyHook" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    private void Run()
    {
        _hookId = SetWindowsHookEx(WhKeyboardLl, _proc, IntPtr.Zero, 0);
        _running = true;
        while (_running && GetMessage(out var msg, IntPtr.Zero, 0, 0))
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }
        if (_hookId != IntPtr.Zero) UnhookWindowsHookEx(_hookId);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WmKeydown)
        {
            var info = Marshal.PtrToStructure<KbdLParam>(lParam);
            bool ctrl = (GetAsyncKeyState(VkControl) & 0x8000) != 0;
            bool alt = (GetAsyncKeyState(VkMenu) & 0x8000) != 0;
            if (!alt && ctrl && info.VkCode == VkV)
            {
                var handler = InterceptPaste;
                if (handler is not null && handler())
                    return (IntPtr)1;
            }
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        _running = false;
        if (_thread.IsAlive)
            PostThreadMessage((uint)_thread.ManagedThreadId, WmQuit, IntPtr.Zero, IntPtr.Zero);
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLParam
    {
        public uint VkCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Msg
    {
        public IntPtr Hwnd;
        public uint Message;
        public IntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public int PtX;
        public int PtY;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern bool GetMessage(out Msg lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref Msg lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref Msg lpMsg);

    [DllImport("user32.dll")]
    private static extern bool PostThreadMessage(uint idThread, int msg, IntPtr wParam, IntPtr lParam);
}
