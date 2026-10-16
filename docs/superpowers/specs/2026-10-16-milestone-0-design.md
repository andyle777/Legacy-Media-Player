# Milestone 0 design

## Intent and finish line

Bootstrap Andy's original, lightweight classic-style player using C#/.NET 10,
Avalonia and LibVLCSharp. Windows 10/11 x64 is the first supported target.
Linux and Android are future work; macOS is excluded. Milestone 0 provides
buildable architecture, a usable diagnostic shell, a mock engine, tests,
documentation and CI. It does not certify the full v1 playback core as stable.

## Selected approach

Use four small projects: Core (contracts and immutable playback data),
Application (player session and deterministic mock), Playback.LibVLC (native
adapter), App (Avalonia desktop and composition root). Application references
only Core. App constructs its engine at startup. LibVLC types never cross the
Core contract. Use a native Windows child window for video and pass its handle
through the engine abstraction. This avoids coupling application code to a
specific video control. A LibVLCSharp.Avalonia VideoView would be simpler but
would expose backend types to the view; a full plugin host is premature.

## Contract

IPlaybackEngine owns Open, Play, Pause, Stop, Seek, volume/mute, track queries,
audio/subtitle selection, video surface attachment, immutable snapshots, errors,
and disposal. Media sources accept existing, fully qualified local files only.
No network URLs are accepted. Volume is 0..100; seek is nonnegative and clamps
to a known duration. Invalid input leaves current media intact. Optional track
and video support is explicit through capabilities; unsupported operations fail
clearly rather than pretending success.

Commands and disposal are serialized by the caller. Engine notifications may
arrive on native threads; subscribers must dispatch before issuing commands or
updating UI. Never invoke LibVLC from its own callbacks. The adapter uses one
LibVLC instance per engine; the app owns one engine. Dispose media/player/runtime
in that order, after detaching video. Native callbacks publish snapshots only.

## Shell and scope

Open one local file, play/pause/stop, basic seek, volume and mute, video host,
state/error display, and an explicit --mock mode requiring no native libraries
at runtime. Keep controls beside/below the native video area to avoid airspace
overlays. Startup failures show a useful error and offer mock mode explicitly.
No silent fallback. Mock playback is deterministic and does not decode or run a
timer. Queue/fullscreen, track menus, external subtitles and classic polish are
Milestone 1. Do not add skins, streaming, visualizers or a media library.

## Verification

xUnit tests exercise application behavior using the mock, input boundaries,
notification marshalling and lifecycle. A separate opt-in native test generates
a small PCM WAV, plays it without an audio device and verifies native progress
and end notification. Windows CI builds/tests and publishes a win-x64 folder
artifact. Real Windows 10/11 video/codec, fullscreen, packaging, resource stress
and adversarial review remain required before v1 stability claims.
