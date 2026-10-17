# Third-party software

This Windows folder bundles unmodified, dynamically linked dependencies. Keep this
file and the licenses folder with the executable. Native DLLs and plugins must
remain replaceable; this is a folder deployment, without trimming or single-file extraction.

| Component | Version / evidence | License and source |
| --- | --- | --- |
| .NET runtime | Exact version in LegacyMediaPlayer.runtimeconfig.json | MIT and third-party notices copied from the matching restored runtime package; https://github.com/dotnet/runtime |
| Avalonia, Win32, Skia integration, themes, Remote.Protocol | 11.3.22 | MIT; licence.md and NOTICE.md from https://github.com/AvaloniaUI/Avalonia/tree/11.3.22 |
| ANGLE native assets | 2.1.25547.20250602 | BSD notice copied from the NuGet package; source commit cb8b4e1307a9d8f5ff56b8c5973bea4158ffead8 at https://github.com/AvaloniaUI/angle |
| MicroCom.Runtime | 0.11.0 | MIT; https://github.com/kekekeks/MicroCom |
| SkiaSharp and Windows native assets | 2.88.9 | MIT plus bundled third-party notices copied from NuGet; https://github.com/mono/SkiaSharp/tree/v2.88.9 |
| HarfBuzzSharp and Windows native assets | 8.3.1.1 | Bundled license and native third-party notices copied from NuGet; https://github.com/mono/SkiaSharp |
| LibVLCSharp | 3.10.2 | LGPL-2.1-or-later; https://github.com/videolan/libvlcsharp/tree/3.10.2 |
| VideoLAN.LibVLC.Windows | 3.0.24 | NuGet declares LGPL-2.1-or-later; https://www.nuget.org/packages/VideoLAN.LibVLC.Windows/3.0.24 |

LibVLC license texts and author acknowledgements are copied from the matching VLC
3.0.24 source tag: https://github.com/videolan/vlc/tree/3.0.24 . Upstream source archive:
https://download.videolan.org/pub/videolan/vlc/3.0.24/vlc-3.0.24.tar.xz . Native package/build
sources: https://code.videolan.org/videolan/libvlc-nuget . Preserve upstream terms for
LibVLC and its codec plugins; no codec patent or redistribution certification is implied.

License/notice texts are included verbatim. The publish script requires these
files and the notices supplied by the restored Windows native/runtime packages.
Test media are excluded from the release folder; their separate provenance and
licenses are in tests/LegacyMediaPlayer.NativeTests/Fixtures.

Legacy Media Player's original source is licensed under the MIT License in the
repository root and packaged LICENSE file. This does not change the separate
licenses of bundled dependencies or test media.
