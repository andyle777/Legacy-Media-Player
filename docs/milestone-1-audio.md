# Milestone 1: functional audio playback

The existing Core → Application → App and Core → LibVLC adapter boundary is
preserved. No projects or dependencies were added. Core remains independent of
Avalonia, LibVLC and Windows APIs. Video/track hooks remain available for later
work; no queue, skins, visualizers, streaming or library was added.

## Changes

- The local picker prioritizes MP3/WAV/FLAC. A successful open starts playback.
  Open + play share one error boundary, so a failed open cannot resume an old
  paused track. Cancel leaves the current track alone.
- Existing transport, volume, mute, filename, state and elapsed/total time UI
  use PlayerSession and IPlaybackEngine. Hover no longer freezes seek updates;
  dragging still previews without being overwritten by playback notifications.
- LibVLC input is stopped before replaying a completed/failed input. Paused
  seeks publish the requested position immediately; later native time events
  remain authoritative. Natural completion displays the known full duration.
- Existing stop/switch/close ownership is retained and tested, including opening
  the previous file exclusively after switching and the last file after disposal.
- Focused tests cover autoplay/error recovery, UI transport/status, native
  decoding of generated WAV/MP3/FLAC, pause/resume, seek, stop/restart, end/replay,
  volume/mute preferences, malformed audio, and released file handles.

## Windows verification (2026-10-16)

Windows x64, .NET SDK 10.0.402 / runtime 10.0.13. Local verification used a
task-local SDK because the system installation only has older runtimes.

Normal restore was attempted and failed NU1900: the local process runner cannot
reach https://api.nuget.org/v3/index.json for vulnerability data. Cached packages
were restored with a command-only NuGetAudit=false override. This override is
not committed to project configuration or CI. Online dependency audit remains
to be verified by CI or on an unrestricted workstation.

Commands (from the repository root, with .NET 10 SDK on PATH):

    dotnet restore -p:NuGetAudit=false
    dotnet build -c Release --no-restore
    dotnet test -c Release --no-build --no-restore
    dotnet test tests/LegacyMediaPlayer.NativeTests -c Release -p:NuGetAudit=false --blame-hang-timeout 90s
    dotnet publish src/LegacyMediaPlayer.App -c Release -r win-x64 --self-contained true -p:NuGetAudit=false -p:PublishSingleFile=false -p:PublishTrimmed=false -o artifacts/win-x64

On a normal connected machine, omit the NuGetAudit override. The native suite
uses dummy output, so decoder tests do not establish speaker audibility.

A real Windows app window was launched; a local MP3 showed Playing, advancing
elapsed time and a 2:44 duration. Pause displayed Paused. Andy confirmed audible
output through the desktop session. No private audio or filenames are committed.
The paused-seek display issue found during that check has an automated regression.

The Release build completed with zero warnings/errors. Managed tests passed 21/21
and headless UI tests passed 5/5. The final complete native codec/transport suite
passed 8/8, including the paused-seek feedback regression (34 total tests).
One subsequent native run in a fresh isolated output directory exceeded the
90-second inactivity watchdog before its first result. The targeted malformed
MP3/WAV/FLAC rerun passed 3/3; cold native initialization timing remains a
robustness issue to investigate on clean machines. Temporary diagnostic traces
were removed. The final verification also used isolated build outputs to avoid
replacing assemblies in the running desktop app.

## Manual checks before merging

1. Run dotnet run --project src/LegacyMediaPlayer.App, open a local MP3, WAV and
   FLAC, and confirm audible output, filename, progress and duration for each.
2. Pause/resume; seek while playing and paused; stop then play; let a short track
   finish and press Play again. Confirm the final rebuilt paused-seek feedback.
3. Change volume, mute/unmute, switch files while playing and paused, and confirm
   preferences persist. Confirm sound level changes through your actual device.
4. Cancel the picker; try missing, empty, corrupt and unsupported files; open a
   valid track afterwards. Confirm useful errors and recovery.
5. Close during playback and reopen. Try Windows 10 and Windows 11 separately,
   headphones/Bluetooth/device changes, long files and rapid repeated controls.

Generated two-second fixtures establish basic decoding of each requested format.
They do not establish format-wide compatibility. WAV/FLAC audible output, final
UI seek retest and physical volume/mute behavior need manual confirmation.
Device switching, suspend/resume, long sessions and clean-machine packaging
remain outside this check. No release/tag or merge to main is created.
