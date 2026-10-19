# Architecture and playback contract

App → Application → Core. Playback.LibVLC → Core. Only the App composition root
constructs the concrete adapter. Neither Core nor Application references Avalonia
or LibVLCSharp. The App's video host uses only IPlaybackEngine and a neutral HWND
descriptor; no codec-library type escapes the adapter.

## State and ownership

Open validates an existing, fully qualified local file and loads it in Ready
state; it does not start playback. Play starts/resumes. Pause is idempotent when
not playing. Stop preserves the source and resets position. Native duration and
seekability arrive asynchronously. Seek rejects negative time, requires seekable
media and clamps at a known duration. Volume accepts 0..100, and mute does not
overwrite the chosen volume. Opening a new file resets media state while
preserving audio preferences. A file that disappears before Open is rejected
without replacing loaded media; a later filesystem/decoder failure is reported
as a playback error. File extensions are not used as a format allowlist.

The session owns the engine. The native adapter owns one LibVLC runtime, one
MediaPlayer and the current Media. On source replacement it stops the decoder,
assigns the new media and releases the old media. At shutdown, the video host
stops/detaches before its window is destroyed, and the session unsubscribes and
disposes the engine. Dispose is idempotent; subsequent commands throw.

## Threading and errors

Call commands and Dispose serially on the UI thread. Snapshot reads are safe;
native events can arrive on LibVLC threads. Event handlers must never issue
commands directly. The adapter's callbacks only update immutable managed
snapshots; PlayerSession dispatches notifications to the UI and reads the latest
snapshot after dispatch, avoiding stale queued state. Late shutdown notifications
are ignored. Subscribers must not throw from native event handlers.

Invalid arguments, unavailable operations and command failures throw exceptions
at the engine boundary. PlayerSession converts expected user-facing failures
into ErrorMessage. Asynchronous decode errors set Failed plus a PlaybackError.
A new Open clears decode errors and allows recovery. Native bootstrap failures
show a startup error; entering mock mode requires an explicit choice.

Track queries return neutral IDs, names and audio/subtitle kinds. Selection
validates against current tracks; IDs can change when media changes. Track menus
and external subtitle loading are deferred. The mock exposes no video/tracks
and throws NotSupportedException for them; its seekable duration is simulated.

## Playback speed

Playback-speed commands use IPlaybackEngine.SetRate, not direct UI access to
LibVLC. Snapshot.Rate is the persisted preference; AcceptedRate is nullable native
request/readback evidence, not measured output speed. RateError is a recoverable
rate-only limitation, separate from decode failure. Empty/deferred engines save
the preference without loading codecs; native application runs on serialized
transport commands. Native callbacks remain data-only. Open and Stop clear the
per-input acceptance state without erasing the preference. Settings from older
versions default to 1×; unsupported saved rates are sanitized independently.

## Per-media video state

HasVideo records discovery of a video elementary stream for the current input;
HasVideoOutput records an active native output, independently of backend capability.
The window shows/layouts the HWND on video-stream discovery in Ready/Playing/Paused,
before native output creation: waiting for Vout can deadlock a renderer that needs
a visible host. Empty/audio media keep the placeholder. Open resets both flags;
Stop, completion and errors hide output. Pause retains the picture. LibVLC owns
aspect-fit/letterboxing and Avalonia resizes the native child; no forced aspect
ratio, crop, scale or Avalonia overlay is applied.

ESAdded/Vout/log callbacks update managed data only. Stop/Open suppress teardown
notifications across LibVLC 3's synchronous Stop boundary. A remembered output
allows a short, durationless video to end naturally rather than be misreported
as empty. Pinned LibVLC output-creation errors are surfaced even when audio can
continue; a later successful output clears that warning so format fallback can
recover. Unsupported-track logs remain warnings because they can refer to optional
audio/subtitle streams. A video input ending without any output fails rather than
auto-advancing as a successful clip. Output evidence resets for each Stop/replay.
The session clears recovered native warnings without erasing unrelated picker errors. Native input handling is
disabled so LibVLC does not add its own fullscreen/hotkey behavior.

The adapter defaults to hardware decoding on desktop. Headless tests explicitly
disable it through the adapter constructor: LibVLCSharp's per-player setting
overrides the global avcodec-hw option. This avoids the observed Intel D3D11VA
plus dummy-output teardown hang; it does not prove desktop GPU compatibility.

## Platform boundary

The bootstrap's only video surface kind is WindowsHwnd. NativeControlHost owns
the Windows child window. Controls are outside the video rectangle because
native video can cover Avalonia overlays. Linux X11/Wayland and Android surfaces
must be added with explicit capabilities and disposal rules, not by reusing a
Windows handle. Desktop UI reuse does not imply Android readiness.

## Dependencies and updates

Versions live in Directory.Packages.props. Avalonia is pinned to 11.3.22 for
this baseline; upgrading to the newer 12.x line is a separate compatibility
change. LibVLCSharp and native LibVLC are different packages and version lines.
Only Avalonia.Win32 and Avalonia.Skia are included, rather than the all-platform
Desktop bundle. This avoids unused Linux/macOS integrations and the vulnerable
D-Bus dependency found in the initial restore. Keep dynamically linked native
files and licenses intact when publishing.

Primary references consulted for this bootstrap:

- [SDK selection](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json)
- [Avalonia 11.3.22](https://www.nuget.org/packages/Avalonia/11.3.22)
- [LibVLCSharp](https://www.nuget.org/packages/LibVLCSharp/3.10.2)
- [Windows native runtime](https://www.nuget.org/packages/VideoLAN.LibVLC.Windows/3.0.24)
- [VideoLAN lifetime and callback guidance](https://github.com/videolan/libvlcsharp/blob/3.x/docs/best_practices.md)
