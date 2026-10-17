using Avalonia.Controls;
using Avalonia.Platform;
using LegacyMediaPlayer.Core;

namespace LegacyMediaPlayer.App;

/// <summary>Windows view boundary. No LibVLC types are needed to host video.</summary>
public sealed class VideoHost : NativeControlHost
{
    private bool _attached;
    private WindowsVideoInput? _input;
    public IPlaybackEngine? Engine { get; set; }

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        StartupTiming.Mark("video.surface.begin");
        var child = base.CreateNativeControlCore(parent);
        if (OperatingSystem.IsWindows() && parent.HandleDescriptor == "HWND")
        {
            try { _input = new WindowsVideoInput(child.Handle, parent.Handle); }
            catch { base.DestroyNativeControlCore(child); throw; }
        }
        if (Engine is { Capabilities.Video: true } engine)
        {
            engine.AttachVideoSurface(new(VideoSurfaceKind.WindowsHwnd, child.Handle));
            _attached = true;
        }
        StartupTiming.Mark("video.surface.end");
        return child;
    }

    public void Release()
    {
        if (_attached && Engine is { } engine)
        {
            engine.Stop();
            engine.AttachVideoSurface(null);
            _attached = false;
        }
        _input?.Dispose();
        _input = null;
        Engine = null;
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        Release();
        base.DestroyNativeControlCore(control);
    }
}
