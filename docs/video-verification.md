# Core video verification

This milestone keeps IPlaybackEngine and the existing Windows HWND host. Core adds
per-media HasVideo and HasVideoOutput snapshot flags; no LibVLC type crosses the
boundary. The host becomes visible at video-stream discovery, before native output
allocation. Empty/audio launches retain the placeholder and deferred native startup.

## Samples and evidence

Small controlled fixtures and their provenance/licenses live in
[test fixtures](https://github.com/andyle777/Legacy-Media-Player/blob/feat/video-playback/tests/LegacyMediaPlayer.NativeTests/Fixtures/README.md).
These checks are representative samples, not codec/container certification.

| Container / codec | Native dummy-output test | Windows embedded picture |
| --- | --- | --- |
| MP4 / H.264 + AAC | Output, completion, transport and switching | Confirmed circle/grid and letterboxing |
| MKV / H.264 | Output and natural completion without duration metadata | Confirmed test pattern |
| WebM / VP8 | Output, completion and mixed queue | Confirmed test pattern |
| AVI / MPEG-4 Part 2 + PCM | Switching and disposal | Confirmed circle/grid |
| MP4 / HEVC | Output and completion | Confirmed test pattern |
| MP4 / VP9 | Output and completion | Confirmed pattern, pause, paused seek and resize |
| MP4 / AV1 | Output and completion | Confirmed pattern and natural advancement after display fallback |

Desktop checks use the self-contained published executable on Windows 11 with
Intel UHD 620 graphics. Longer desktop copies of the one-second test patterns
change container timebases only; their encoded streams are unchanged. The original
MP4/AVI tone clips are six seconds. MP3/WAV/FLAC regression fixtures also exist.
The user confirmed audible tone output on speakers/headphones. Automated native
tests use dummy audio/video outputs and do not verify pixels or physical audibility.

Observed desktop behavior includes MP3/WAV/FLAC progress, video-to-audio and
audio-to-video switching, video Stop restoring the placeholder, mute/volume,
video-to-video automatic advancement, Previous,
queue add/remove/reorder without disturbing a paused picture, Ctrl+Shift+A picker,
paused seeking, aspect-fit resizing, close/relaunch, and restored volume/mute.
The final non-diagnostic publish also visibly rendered AV1 and H.264 and advanced
naturally between them. Controls remain outside the HWND rectangle.

## Final local results

Release restore/build succeeded with zero build warnings/errors. All 76 tests
passed: 37 managed, 14 headless UI and 25 native. Windows self-contained publish
verified the bundled runtime, native DLLs, 321 plugin DLLs and generated plugins.dat.
Local restore used NuGetAudit=false because this environment cannot reach the audit
endpoint; the repository and CI audit settings remain enabled.

Three empty launches of the final executable reached the first-frame callback at
3183.112 ms, 2688.954 ms and 2577.917 ms from process start, with no LibVLC stages
in the opt-in log. These are warm-session callback timings, not pixel-ready or
reboot-cold measurements and not a controlled performance comparison.

## Automated coverage

Focused tests cover per-input video discovery/output, visibility before output
allocation, audio/video replacement, mixed queue progression, late dispatched
notifications, pause/resume, seek, Stop/replay, close/dispose, malformed containers,
unsupported video with playable audio, optional unsupported audio with playable
video, renderer failure, and warning recovery without losing independent picker errors.
Headless UI tests exercise real session/dispatch/control behavior with a substituted
native event source; they do not inspect decoded pixels.

## Limits and follow-up checks

AV1 initially logged a display-conversion failure; allowing native output fallback
recovered and visibly rendered the sample. This does not establish broad GPU/display
compatibility. Test
larger files, long playback, unusual frame rates, other graphics drivers, and files
with multiple streams separately. No fullscreen, track/subtitle menus, streaming,
filters, speed controls, library or platform expansion is included.

Native tests explicitly disable hardware decoding: this machine's Intel D3D11VA
plus dummy-output teardown hung. Desktop retains the existing hardware default.
LibVLC log messages are pinned-runtime heuristics: unsupported optional tracks warn
without stopping playable streams; no video output by end is a failure. A successful
native output can recover from an earlier output-creation warning.

Build through scripts/Publish-Windows.ps1. Its generated plugins.dat must accompany
the bundled runtime and plugin tree; a raw dotnet publish alone omits cache generation
and caused a long first-playback discovery stall during verification. Startup timing
remains opt-in. Reboot-cold startup and antivirus first-launch effects are uncharacterized.
