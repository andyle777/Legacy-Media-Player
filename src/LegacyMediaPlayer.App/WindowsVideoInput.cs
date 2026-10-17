using System.Runtime.InteropServices;

namespace LegacyMediaPlayer.App;

/// <summary>Let Avalonia receive mouse input over its two native host windows.</summary>
internal sealed class WindowsVideoInput : IDisposable
{
    private const uint WmNcHitTest = 0x0084;
    private const uint WmNcDestroy = 0x0082;
    private static readonly SubclassProc Callback = WindowProcedure;
    private readonly nint _child;
    private readonly nint _holder;
    private bool _disposed;

    public WindowsVideoInput(nint child, nint holder)
    {
        // Both handles belong to the Avalonia UI thread. Never subclass LibVLC's
        // renderer children: they belong to another thread and are input-disabled.
        _child = child;
        _holder = holder;
        if (!SetWindowSubclass(child, Callback, 1, 0) || !SetWindowSubclass(holder, Callback, 1, 0))
        {
            Dispose();
            throw new InvalidOperationException("Could not route embedded video mouse input to the player window.");
        }
    }

    private static nint WindowProcedure(nint hwnd, uint message, nuint wParam, nint lParam, nuint id, nuint data)
    {
        if (message == WmNcHitTest) return -1; // HTTRANSPARENT: continue to the same-thread Avalonia window.
        if (message == WmNcDestroy) RemoveWindowSubclass(hwnd, Callback, id);
        return DefSubclassProc(hwnd, message, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        RemoveWindowSubclass(_child, Callback, 1);
        RemoveWindowSubclass(_holder, Callback, 1);
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint SubclassProc(nint hwnd, uint message, nuint wParam, nint lParam, nuint id, nuint data);
    [DllImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(nint hwnd, SubclassProc callback, nuint id, nuint data);
    [DllImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(nint hwnd, SubclassProc callback, nuint id);
    [DllImport("comctl32.dll")]
    private static extern nint DefSubclassProc(nint hwnd, uint message, nuint wParam, nint lParam);
}
