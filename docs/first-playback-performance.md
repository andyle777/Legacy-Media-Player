# First playback and Windows package measurements — 2026-10-18

Baseline: main/tag v0.3.0-alpha, `5ebd1d879fb1c99a03cb15714b69ec5eaf789813`.
PR #7 (drag/drop) was unmerged when this independent performance branch began.
No release, tag, version or IPlaybackEngine change is made here.

## Reproduction and attribution

The actual published release ZIP is 195,073,110 bytes, SHA-256
`7da1135ac838ff8b54327c8accc738de141fc18338f07b8cf15d1066b3c5d13b`.
A matching local copy was freshly extracted; it was not substituted with an
earlier, differently compressed local build.

On the first MP3 attempt, the window handle appeared after 7,697.808 ms. The
existing prepare marker to Playing interval was 62,325.748 ms. Native DLL loading
took 1,086.799 ms, LibVLC construction 60,762.692 ms, and MediaPlayer construction
22.272 ms. The existing video HWND took 4.240 ms. This isolates the dominant
delay to native runtime construction, rather than window creation or media parsing.

All 321 extracted x64 plugin sizes matched the generated cache, but every cached
mtime differed by exactly 36,000 seconds. This is consistent with ZIP local-time
interpretation between the UTC build host and the Sydney extraction environment.
A verbose native probe logged 321 stale-cache errors. VLC validates cached plugin
sizes/mtimes and loads a plugin again when they differ:
[VLC 3 module bank](https://github.com/videolan/vlc/blob/3.0.x/src/modules/bank.c).
The cache format stores these values alongside relative paths:
[VLC 3 cache implementation](https://github.com/videolan/vlc/blob/3.0.x/src/modules/cache.c).

Fresh-process warm native probes against that same extracted tree measured
780.735 ms with scanning and 33.639 ms with cache-only loading (433 modules).
These probes isolate native construction; they do not measure audible output.
No antivirus exclusion, security-setting change, ETW trace or reboot was used.
Filesystem caching, native dependency loading and antivirus costs are not isolated.

## Change and safeguards

Packaging records native DLL/cache SHA-256 values and the complete plugin
inventory, sizes, mtimes and hashes. Publish and ZIP extraction verify every
native file's SHA-256. Runtime startup verifies the native DLL/cache hashes and
all plugin sizes/mtimes, permitting only one uniform timezone shift, in 15-minute
increments and bounded to 26 hours. If extraction rewrites plugin timestamps,
runtime startup hashes the plugins and keeps cache-only loading when their content
still matches the package manifest. The ordinary path does not hash every plugin.
A missing/extra plugin, changed native runtime/cache or changed plugin content
still produces re-extraction guidance. Developer folders without a manifest
retain normal discovery. Explicit plugin options, including cache generation,
keep their existing behavior.

This is a consistency check, not signature/authenticity verification. Like VLC's
normal cache metadata check, runtime validation cannot detect a same-size plugin
edit that also preserves its timestamp. The packaging/extraction SHA checks do
detect that edit. Rebuild the complete bundle when changing native dependencies.

A first candidate SHA-hashed every plugin on every application launch. Measurements
showed 0.8–1.6 seconds of validation overhead and a warm-playback regression, so
that approach was rejected. Buffering/parallel hash experiments still cost roughly
0.4–0.7 seconds for 109 MB. The retained metadata check avoids reading all codecs;
warm verification measured about 65–99 ms in the investigation.

Empty-window startup remains lazy. There is no background warm-up, dummy playback,
new parsing pass or repeated engine creation. Deferred publication/disposal guards,
queue logic, media ownership, HWND rendering and fullscreen are unchanged.

## Instrumentation and interpretation

Set `LEGACY_MEDIA_PLAYER_STARTUP_LOG` to a writable JSONL file for optional phase
markers. The normal UI remains unchanged. Added markers cover request/validation,
cache verification, native DLL/runtime/player construction, media creation/stop/
attachment, Play invocation, stream discovery, Playing, first clock progress and
first video-output event. They contain stage names/timings, not media paths.

There is no separate synchronous media-parse call. Stream/decoder/output work
after Play is asynchronous; ESAdded identifies stream discovery, and Vout indicates
output creation. Neither Vout nor Playing proves a displayed pixel or audible sound.
The animation-frame marker refers to Avalonia's window, not decoded video.

`scripts/Measure-FirstPlayback.ps1` runs fresh processes, preserves per-trial logs,
drains native stderr, and closes only its own processes. It refuses an existing
results directory. Its shared comparison boundary is prepare → Playing; request
and validation timings are additional data in the instrumented build. It waits
after Playing to collect clock/output evidence. Short or altered-timebase fixtures
can finish or delay output before that observation completes.

Windows 11 x64 build 26300; SDK 10.0.402; self-contained runtime 10.0.13; pinned
Avalonia 11.3.22, LibVLCSharp 3.10.2, LibVLC 3.0.24. Automated process measurements
ran under the agent's Windows execution account, separately from the interactive
desktop checks. No builds/tests ran concurrently with timing trials. The OS and
filesystem were already used in this session; none of these are reboot-cold tests.
Optional JSON/file diagnostics and host scheduling add overhead. Small samples
do not establish percentiles or behavior on other machines.

The final extracted package's initial MP3 prepare → Playing attempt measured
9,059.271 ms, with 94.965 ms validation. Much of the remainder was after Play and
before stream discovery. This remaining first-use cost is not attributed to a
specific scanner/driver. The 62.3 s baseline and 9.1 s observation are sequential
first-use observations, not a controlled cold-start percentage comparison.

## Package audit

Published baseline content, excluding ZIP headers (bytes; compressed payload is
the sum of ZIP entry sizes, not a separately compressed component archive):

| Component | Unpacked bytes | Compressed payload bytes |
| --- | ---: | ---: |
| .NET runtime | 80,374,232 | 36,852,378 |
| Application | 391,847 | 170,557 |
| Managed UI/playback dependencies | 6,287,480 | 2,344,698 |
| Supporting UI native binaries | 16,875,664 | 7,126,910 |
| LibVLC x64 binaries | 3,212,160 | 1,775,970 |
| All 321 x64 plugin DLLs | 105,748,928 | 48,440,247 |
| LibVLC x64 support/cache | 2,888,411 | 1,215,833 |
| Unused x86/ARM64 native trees | 202,209,771 | 96,629,729 |
| Documentation and notices | 505,153 | 138,444 |

The native NuGet target defaults AnyCPU to all architectures even with win-x64 RID.
The Windows publish profile now selects x64 explicitly and disables x86/ARM64.
All x64 codecs, Lua/HRTF/support data, .NET, UI native dependencies and licenses are
retained. No trimming, single-file packing or codec pruning is used. Compression
remains Optimal; host/runtime compression differences can change ZIP sizes.

The review ZIP is 95,408,853 bytes (51.09% smaller than the published asset), SHA-256
`884dc56fcea37e359121b484ec9190aa7f1ab313ae980bef0215977e6ccc8d33`.
All 666 files match after extraction. Integrity checks retain MIT/third-party
notices, bundled runtime and the complete 321-plugin x64 cache/tree. The new
`--verify-plugin-cache` packaging maintenance command exercises the actual adapter's
cache-only native startup without loading a window or preferences.

## Warm restart comparison

Three warmed fresh-process first-file trials per format, in milliseconds. The
initial first-use trials above are excluded from this table. MP4 uses the normal
six-second H.264/AAC fixture; the stretched long-MP4 investigation clips are not
used for its warm comparison. The same prepare marker is available in both builds.

| Media | Before median (range) | After median (range) | Improvement |
| --- | ---: | ---: | ---: |
| 02 tone.mp3 | 790.184 (758.923–842.723) | 505.378 (500.422–608.281) | 36.0% |
| 03 tone.flac | 839.950 (814.592–950.761) | 516.021 (495.345–595.478) | 38.6% |
| tone-video.mp4 | 863.965 (793.174–875.123) | 542.687 (514.807–779.059) | 37.2% |
| 05 h264.mkv | 870.460 (778.758–1036.145) | 506.574 (454.481–524.712) | 41.8% |

Raw warm-trial values are in [first-playback-timings.csv](first-playback-timings.csv).
These are Playing-event improvements, not measured sound/pixel improvements.

## Regression verification

Local restore and Release build passed with zero warnings/errors. Tests: 37
managed, 23 headless UI, 25 native decoder/lifecycle cases and nine bundle-policy
cases (94 total). Package preflight and architecture-selection regressions passed.
Local restore used the temporary NuGetAudit=false override; no audit setting is
changed in the repository or CI.

Published-style process smoke trials exercised MP3/WAV/FLAC, H.264 MP4/MKV,
WebM/VP8, AVI/MPEG-4, HEVC, VP9 and AV1 with cache-only mode selected. Short-fixture
Playing/stream/output events and the existing native decoder tests are separate
from visual or audible verification; codec/GPU coverage is representative.

A fresh extraction with all 321 plugin mtimes shifted by -10 hours passed complete
SHA verification and the actual cache-only native startup command. A same-size
plugin byte edit retaining its timestamp was rejected by package SHA verification
before launching the altered bundle.

On the interactive Windows desktop, the extracted review executable visibly
rendered H.264 MP4, resized with aspect ratio intact, and passed F11, Escape,
Alt+Enter and native-picture double-click fullscreen. Space paused video; Right
sought from 1:25 to 1:30 while paused. Ctrl+Right switched video to MP3 and restored
the audio placeholder. Physical listening and later desktop results are recorded
in the review PR.

Remaining limitations include the observed roughly nine-second initial fresh-folder
MP3 attempt, uncharacterized reboot-cold startup, broader Windows 10/GPU/DPI/device
coverage and per-codec first-pixel/sound measurements. No claim is made that CI
visually proved video. Drag/drop remains in separate, unmerged PR #7 and is not
included or modified here. Main and the v0.3.0-alpha release/tag are preserved.
