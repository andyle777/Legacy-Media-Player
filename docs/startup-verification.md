# Windows startup verification — 2026-10-16

Baseline: PR #2 head `808419724624ce2d6162ae62d96514e32cf7c9d7`
(`feat/playlist-windows-player`). This change is stacked on that unmerged work;
PR #2 and main are unchanged. Windows x64 build 26300, .NET SDK 10.0.402,
self-contained runtime 10.0.13. No system .NET 10 or VLC installation was required.

## Findings and changes

The original application constructed LibVLC synchronously before creating the
main window. In the slow traced launch, new LibVLC took **52,433.042 ms**.
Loading its native DLLs took 824.203 ms; creating MediaPlayer took 11.465 ms;
creating the video HWND took 2.124 ms. The framework had reached initialization
completion at process +8,373.936 ms, but the window opened at +62,781.134 ms.
The process-to-managed-entry interval was 3,751.028 ms; that includes Windows
loader/runtime work and does not isolate the .NET host itself.

The published VideoLAN plugin tree lacked plugins.dat. A fresh cache-generation
probe spent 72,455.701 ms discovering plugins. Subsequent fresh-process probes
against the same tree measured 155.523 ms with the cache and 788.071 ms with the
cache temporarily absent, both warm. This supports expensive first-use plugin
discovery as a major contributor; it does not prove an antivirus-specific cause.
[VideoLAN recommends generating the cache once and regenerating it when LibVLC changes](https://docs.videolan.me/libvlcsharp/docs/best_practices.html).
The VLC 3 plugin bank validates cached DLL size and modification time.

A managed DeferredPlaybackEngine now implements the existing IPlaybackEngine.
State, preferences and video-surface attachment do not load native code. The
first valid file open awaits one worker initialization while the window remains
responsive. Closing during initialization disposes a late-created backend on that
worker. Errors explain missing bundled dependencies in the player. The queue
service and Core contract are unchanged. Normal commands retain the existing
serialized-caller contract; initialization and disposal publication use a small
lock. A failed native initialization requires repairing the bundle and relaunching
rather than repeatedly rebuilding it.

Publishing generates plugins.dat once against the exact bundled tree. Normal
launches never reset it; VLC can still fall back to discovery if it is missing or
stale. All 321 native plugin DLLs remain bundled. No trimming, single-file
extraction, security exclusions or video redesign was introduced.

## Measurements

External measurements run Process.Start to the first nonzero MainWindowHandle,
polled every 20 ms. This is a window-creation milestone, not a guarantee of first
visible pixels, interactivity or audible sound. Values below are milliseconds.

| Run | Before | After |
| --- | ---: | ---: |
| First launch of a fresh original publish / initial lazy-only publish | 83,357.3695 | 11,861.0548 |
| First measured launch of the final publish | — | 4,734.2727 |
| Quiet warm trial 1 | 4,366.7928 | 2,168.1176 |
| Quiet warm trial 2 | 3,357.3765 | 2,355.4756 |
| Quiet warm trial 3 | 2,829.2084 | 3,021.5724 |
| Quiet warm median | 3,357.3765 | 2,355.4756 |

The first lazy-only measurement had tracing enabled and other build activity.
Its two similarly noisy warm measurements were 5,013.2535 and 4,661.8372 ms;
those runs are not evidence of warm-start improvement. The final quiet series
used no tracing and had no builds/tests running. DOTNET_ROOT was unset and PATH
contained only Windows directories, confirming use of the bundled runtime.
The final publish had already run its cache-generation maintenance process,
which also warms executable/native files: its 4,734.2727 ms result is not directly
comparable to an untouched cold folder. Publishing intentionally moves that
native discovery work to packaging time.

A separate traced baseline run measured 62,968.6429 ms externally (window first
frame +62,788.581 ms); its warm launches were 2,727.8823 and 3,014.233 ms. Baseline
mock mode measured 2,043.1049 ms without LibVLC. The traces help attribute the
stall; small differences between these and the quiet series reflect filesystem,
OS scheduling and tracing variability.

The final published executable opening an MP3 path containing spaces measured
2,913.279 ms externally. Its first animation-frame callback was +2,799.643 ms;
LibVLC loaded on thread 5 while the UI remained on thread 2. Native DLL loading
took 22.130 ms, LibVLC creation 88.494 ms, MediaPlayer creation 39.386 ms, and
Playing arrived at +3,221.597 ms. That event does not prove audible output.
An empty-window traced run contained no LibVLC initialization stages at all.

These are first-use and warm launches in one Windows session, not rebooted or
cache-flushed cold-start measurements. No ETW/ProcMon or antivirus exclusion
experiment was performed. Windows loader/dependency probing, filesystem cache,
antivirus scanning and runtime/Avalonia first-use work cannot be separated
confidently by these measurements. Remaining OS-cold behavior needs a rebooted
Windows 11 machine and a newly extracted folder.

## Deployment and automated verification

- Release build: succeeded with zero warnings/errors.
- Managed tests: 37 passed; Avalonia headless UI tests: 11 passed.
- Native tests: 9 passed, including deferred initialization and MP3 → WAV → FLAC
  natural queue advancement, pause/seek, previous/next, volume/mute and stop.
- Focused UI tests cover asynchronous opening, close during initialization,
  useful failure reporting, transport shortcuts, queue arrow navigation,
  and saving volume/mute on close. Settings corruption/read-only cases are tested.
- Self-contained Windows publish: succeeded; coreclr/host DLLs, both LibVLC DLLs,
  321 plugins and a nonempty plugins.dat verified by the publish script.
- ZIP/extract to a different folder: all 321 plugin DLL sizes/timestamps match.
  A fresh native probe against the relocated tree took 628.769 ms to load DLLs
  and 241.700 ms to construct LibVLC. Cache survives folder relocation.
- Published application with synthetic MP3, WAV and FLAC startup arguments reached native
  Playing without a crash. This also exercises spaces in measurement arguments.

Local normal restore reached the blocked NuGet vulnerability endpoint and failed
NU1900. Verification used cached packages and temporary command-line
`-p:NuGetAudit=false`; no audit setting was changed in project files or CI.

Commands (online environments use normal restore, without the local override):

```powershell
dotnet --info
dotnet restore LegacyMediaPlayer.sln -p:NuGetAudit=false
dotnet build LegacyMediaPlayer.sln -c Release --no-restore
dotnet test LegacyMediaPlayer.sln -c Release --no-restore
dotnet test tests/LegacyMediaPlayer.NativeTests -c Release -p:NuGetAudit=false
dotnet restore src/LegacyMediaPlayer.App -p:PublishProfile=Windows -p:NuGetAudit=false
./scripts/Publish-Windows.ps1 -NoRestore
./scripts/Measure-Startup.ps1 -Executable ./artifacts/LegacyMediaPlayer-win-x64/LegacyMediaPlayer.exe
```

Optional phase tracing: pass -TimingLog with a writable JSONL path to the
measurement script, or set LEGACY_MEDIA_PLAYER_STARTUP_LOG for a launch.
There is no logging/UI noise when it is unset. Logs contain stage names,
process/thread IDs and timings, not media paths. Tracing adds first-use
JSON/file overhead, so keep it off for comparable external timings. The script
leaves its process open for manual checks; close it before the next trial.

## Desktop checks

After manual launch, the running executable path was verified against this
checkout (an older build was initially open in a different folder). Desktop
checks using Computer Use then confirmed:

- A user-selected MP3 reached Playing and Pause with elapsed/total time shown.
- A generated MP3 opened through Ctrl+O; Space paused it once, and Right sought
  from 0:10 to 0:15 while paused.
- Down decreased the volume slider; M toggled Mute/Unmute.
- The Add button appended WAV/FLAC without changing the paused MP3 or position.
- Ctrl+Right started WAV; natural completion advanced WAV to FLAC.
- Moving the playing FLAC up preserved playback, and completion advanced to WAV
  in the new order. Removing a different row left WAV playing.
- Ctrl+Left started the previous FLAC; Stop returned it to Stopped.

Ctrl+Shift+O failed both automated and user keyboard checks on this desktop,
although a headless routed-input test confirms the app handles it when delivered.
AMD Software is installed; [AMD documents that same default overlay shortcut](https://www.amd.com/en/resources/support-articles/faqs/DH3-038.html).
A global shortcut conflict is the best-supported explanation, not a proven
interception trace. System/device inspection was restricted, and no graphics
settings were changed. Ctrl+Shift+A is now an alternate Add shortcut, covered by
the routed-input regression test and advertised in the button tooltip.

The generated 30-second FLAC had an unknown total-sample count (zero in
STREAMINFO), so LibVLC initially estimated a four-minute duration even though
the stream ended at 30 seconds. A corrected fixture was prepared outside Git;
duration display should be checked with a normal tagged FLAC. The existing
native test fixtures have known duration and passed. This is not evidence of
a new duration regression in the application.

Further desktop automation was blocked when the window became minimized and
Computer Use repeatedly reported 'user input was detected in this window'.
Remaining manual checks: close/relaunch with saved volume/mute, physical
listening/device volume, the new alternate shortcut, and reboot-cold startup
from a newly extracted folder. The final-item stop is covered by native tests;
the desktop run did not wait for that final state. Automated decoder/headless
tests and Playing events do not prove audible output. Video/fullscreen remain
outside this change.
