using Avalonia.Controls;
using Avalonia.Platform;
using LegacyMediaPlayer.Core;

namespace LegacyMediaPlayer.App;

/// <summary>Windows view boundary. No LibVLC types are needed to host video.</summary>
public sealed class VideoHost : NativeControlHost
{
    private bool _attached;
    public IPlaybackEngine? Engine { get; set; }

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        var child = base.CreateNativeControlCore(parent);
        if (Engine is { Capabilities.Video: true } engine)
        {
            engine.AttachVideoSurface(new(VideoSurfaceKind.WindowsHwnd, child.Handle));
            _attached = true;
        }
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
        Engine = null;
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        Release();
        base.DestroyNativeControlCore(control);
    }
}
