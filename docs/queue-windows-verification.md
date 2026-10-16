# Queue and Windows build verification — 16 October 2026

Base: merged PR #1, `35caf2998c73f94cf518d75e4434be254c7c482a`.
Feature branch: `feat/playlist-windows-player`. No merge to main.

## Automated checks

- Restore: passed using pinned SDK 10.0.402.
- Release solution build: passed, 0 warnings, 0 errors.
- Managed tests: **27 passed**.
- Avalonia headless window tests: **6 passed**.
- Native Windows LibVLC tests: **9 passed**, including real MP3/WAV/FLAC
  queue advancement and final end, previous/next, seek while paused, stop,
  track switching and volume/mute preferences. Native tests use dummy outputs.
- Self-contained `win-x64` publish: passed. Dependency checks found the local
  .NET runtime DLLs, LibVLC DLLs and **321 native plugin DLLs**.
- Published runtime configuration includes Microsoft.NETCore.App 10.0.13;
  no separately installed runtime is requested.
- `git diff --check`: passed.

Queue and window tests were written before implementation and initially failed
on the missing API. Independent review found a missing-file retry involving
duplicate paths. A new behavioral regression reproduced the error before the
fix; successful-open bookkeeping now prevents replaying the prior loaded copy.

The first native run with both classes in parallel hit its 60-second inactivity
limit and was aborted. The isolated queue test passed. Both classes now share
one xUnit collection; the complete serialized run passed all nine tests with no
skips. This avoids concurrent native startup/plugin loading during verification;
the original timeout is retained here rather than counted as a passing run.

## Published executable on this Windows machine

Launched `outputs/artifacts/LegacyMediaPlayer-win-x64/LegacyMediaPlayer.exe`
directly, outside the source/build directory. The app showed its native LibVLC
mode and loaded native DLLs from that output folder.

Exercised through the real Windows UI:

- Add multiple files without interrupting the current file.
- MP3 playback, automatic transition from the short MP3 fixture to WAV.
- Pause WAV; seek from about 6 to 20 seconds, with immediate paused-time update.
- Volume slider and Mute/Unmute state.
- Move a selected FLAC entry up and down while preserving the paused WAV and time.
- Next to FLAC, natural final completion at 0:02/0:02, Next disabled.
- Previous back to WAV, with the volume/mute preferences retained.
- Remove a different item without interrupting playback; remove the current WAV
  and observe Stopped at 0:00.

Startup was variable: the fresh copied output took several minutes to become
visible on this machine, while an earlier output opened sooner. A diagnostic
stack after startup showed the normal Avalonia dispatcher loop, without a
managed crash. The startup delay is unresolved; do not claim consistently fast
launch or broad daily-use stability from this run.

## Checks for Andy

Confirm audible MP3/WAV/FLAC, physical speaker/headphone volume changes and mute,
and paused seek with your own music. The agent observed UI and decoder state but
cannot certify what came out of the speakers. Also check a fresh launch after
closing, a long listening session, and startup on another clean Windows 11 x64
machine with no .NET installation. Session-only queues, no installer/signing,
and no automatic default-app registration are intentional for this milestone.

The GitHub Actions job publishes **LegacyMediaPlayer-win-x64** and test result
artifacts; consult the actual branch run for its current status.
