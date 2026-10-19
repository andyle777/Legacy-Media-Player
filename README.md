# Legacy Media Player

Performance investigation and Windows package audit:
[first-playback measurements](docs/first-playback-performance.md).

An original, lightweight media player with the classic desktop-player feel and
modern codecs underneath. This milestone adds core embedded video playback to the editable-queue Windows 10/11 x64 player
using C#/.NET 10, Avalonia and LibVLCSharp.

## Run the Windows player

Download **LegacyMediaPlayer-win-x64** from a successful branch build in GitHub
Actions. Extract the Actions archive, then extract its versioned Windows ZIP and
open **LegacyMediaPlayer.exe** inside that folder. No separate
.NET or VLC installation is needed. Keep the DLLs and `libvlc` folder together.
Read the [Windows usage and packaging guide](docs/windows-player.md).

Open local MP3/WAV/FLAC or MP4/MKV/WebM/AVI files, or use Add to queue while listening. The queue
supports previous/next, automatic advancement, selected-item play, remove and
move up/down. Repeat cycles Off/All/One; Shuffle randomizes playback without
reordering the queue, with Previous/Next history. These mode preferences persist
alongside volume/mute. Removing the current track stops it; Play continues through
remaining items. See the [repeat/shuffle behavior and checks](docs/repeat-shuffle-verification.md).
The queue lasts for the current app session only.

The beta development branch adds a keyboard-accessible **Speed** selector:
0.5×, 0.75×, 1.0× (Normal), 1.25×, 1.5× and 2.0×. The preference persists and
can be chosen before playback without loading native codecs. LibVLC rejection
shows a warning and keeps the prior preference; native acceptance is not a
guarantee of pitch preservation or smooth output for every file.
See [speed behavior and verification](docs/playback-speed-verification.md).

## Quick start

Install the **.NET SDK 10.0.402** and Git. Open `LegacyMediaPlayer.sln` or the
repository folder in VS Code with Microsoft's C# extension.

```powershell
dotnet restore LegacyMediaPlayer.sln
dotnet build LegacyMediaPlayer.sln -c Release --no-restore
dotnet test LegacyMediaPlayer.sln -c Release --no-build --no-restore
dotnet run --project src/LegacyMediaPlayer.App
```

The native Windows runtime comes from `VideoLAN.LibVLC.Windows`; a separate VLC
installation is unnecessary. Keep its `libvlc/win-x64` directory beside the app.
An existing .NET runtime alone cannot build this project. The SDK is pinned in
`global.json`; use that exact version or deliberately update the pin and CI.

To develop without loading native codecs:

```powershell
dotnet run --project src/LegacyMediaPlayer.App -- --mock
```

Mock mode shows its name in the window. It simulates transport and a three-minute
duration, accepts real local files, and does not decode or advance time. The
normal mode reports native startup failures and offers an explicit mock option.

## What works now

- Core/Application/App/LibVLC adapter projects with a clean `IPlaybackEngine`.
- A simple shell: open local files, play, pause, stop, seek, volume, mute and
  an embedded aspect-fit video surface that follows the active media, with F11 /
  Alt+Enter / Escape / double-click fullscreen.
- Filename, state, elapsed/total time, recoverable errors and a deterministic mock engine.
- Replay after completion, seeking while paused, and audio preferences across file switches.
- xUnit behavior tests and a separate native decoder smoke suite.
- Windows CI, a publish artifact, architecture notes and a v1 completion plan.

This is a development player, not a stable v1 release. Track/subtitle
menus, classic visual polish, broad codec validation and robustness work remain.
Skins, visualizers, streaming services and a large media library are outside v1.

## Verify the native engine

The native suite is deliberately outside the main solution. It requires
Windows x64 and the native runtime, uses generated audio and small licensed video fixtures and
uses dummy audio/video outputs so CI needs no speakers or display surface.

```powershell
dotnet test tests/LegacyMediaPlayer.NativeTests -c Release
```

Its checks cover MP3/WAV/FLAC decoding, pause/resume, seeking, replay after
completion, file switching/released handles, preferences, damaged
media recovery, video output state, mixed queues, renderer failures, invalid input
and disposal. See [video verification](docs/video-verification.md). It does not prove hardware decoding,
video rendering or support for every codec/container.

## Build a development distribution

```powershell
./scripts/Package-Windows.ps1
```

The script prints the versioned ZIP, SHA-256, and freshly extracted verification
executable. Run `artifacts/LegacyMediaPlayer-v0.4.0-beta-win-x64/LegacyMediaPlayer.exe`. Keep the entire folder,
including native libraries, plugin cache and third-party license files. See
[the draft beta notes](docs/release-notes-0.4.0-beta.md) and [fullscreen/package verification](docs/fullscreen-verification.md). No installer, signing
or automatic updates are configured yet.

## License

Legacy Media Player's original source is licensed under the [MIT License](LICENSE).
Windows packages include this license. Bundled dependencies and test media retain
their own terms; see [third-party notices](docs/THIRD-PARTY-NOTICES.md) and fixture notices.

## Project map

| Project | Responsibility | Dependencies |
| --- | --- | --- |
| Core | Playback contract and neutral models | .NET only |
| Application | Player session, UI dispatch boundary, mock | Core |
| Playback.LibVLC | Native media, controls, events and video handle | Core, LibVLCSharp |
| App | Desktop shell, file picker, video host, composition | Application, adapter, Avalonia, Windows runtime |
| Tests | Managed behavior tests | Application, xUnit |
| UiTests | Headless window input regressions | App, Avalonia.Headless.XUnit |
| NativeTests | Explicit real decoder checks | Adapter, Windows runtime, xUnit |

Linux and Android are future targets. They need their own native packaging,
video surface integration and tests. No macOS target, packaging or CI is planned.

Read [architecture](docs/architecture.md), [milestones](docs/milestones.md),
[the design](docs/superpowers/specs/2026-10-16-milestone-0-design.md),
[implementation plan](docs/superpowers/plans/2026-10-16-milestone-0.md) and
[verification record](docs/verification.md). See the current
[Milestone 1 checks and manual testing steps](docs/milestone-1-audio.md).

Drag local MP3/WAV/FLAC/MP4/MKV/WebM/AVI files onto the picture/placeholder to replace
the queue and play, or onto the queue panel to append. Ctrl+drop on the picture also
appends. Order and duplicate entries are preserved; invalid/missing items are skipped
with a warning. No source file is moved or modified. Appending starts playback only
when there is no current item. See [drag/drop checks](docs/drag-drop-verification.md).
