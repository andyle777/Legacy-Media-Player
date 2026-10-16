# Legacy Media Player

An original, lightweight media player with the classic desktop-player feel and
modern codecs underneath. Milestone 1 adds functional local audio playback to the Windows 10/11 x64 foundation
using C#/.NET 10, Avalonia and LibVLCSharp.

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
- A simple shell: open one local file, play, pause, stop, seek, volume, mute and
  a native video surface. Open MP3, WAV or FLAC to start playback immediately.
- Filename, state, elapsed/total time, recoverable errors and a deterministic mock engine.
- Replay after completion, seeking while paused, and audio preferences across file switches.
- xUnit behavior tests and a separate native decoder smoke suite.
- Windows CI, a publish artifact, architecture notes and a v1 completion plan.

This is a development player, not a stable v1 release. Queue, fullscreen, track/subtitle
menus, classic visual polish, broad codec validation and robustness work remain.
Skins, visualizers, streaming services and a large media library are outside v1.

## Verify the native engine

The native suite is deliberately outside the main solution. It requires
Windows x64 and the native runtime, uses generated WAV/MP3/FLAC fixtures and
uses dummy audio/video outputs so CI needs no speakers or display surface.

```powershell
dotnet test tests/LegacyMediaPlayer.NativeTests -c Release
```

Its checks cover MP3/WAV/FLAC decoding, pause/resume, seeking, replay after
completion, file switching/released handles, preferences, damaged
media recovery, invalid input and disposal. It does not prove hardware decoding,
video rendering or support for every codec/container.

## Build a development distribution

```powershell
dotnet publish src/LegacyMediaPlayer.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -o artifacts/win-x64
```

Run `artifacts/win-x64/LegacyMediaPlayer.App.exe`. Distribute the entire folder,
including native libraries and third-party license files. No installer, signing
or automatic updates are configured yet. Choose the project's own open-source
license before public redistribution; no license has been chosen on Andy's behalf.

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
