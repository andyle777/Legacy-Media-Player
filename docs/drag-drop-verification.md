# Drag/drop development verification

Baseline: released main 5ebd1d879fb1c99a03cb15714b69ec5eaf789813 (v0.3.0-alpha).
Development version: 0.4.0-alpha. No release or tag is created by this branch.

Avalonia 11.3.22 uses DragEventArgs.DataTransfer/TryGetFiles. Incoming OLE data are
materialized into local paths synchronously, before awaiting deferred playback startup.
Files are selected in Application through existing MediaSource validation; Core and
IPlaybackEngine are unchanged. Main-area drops use OpenFilesAsync; queue/Ctrl drops
use its append path, retaining PlayerSession queue behavior and duplicate identities.
Only Copy is advertised, including Shift drags; Move-only payloads are rejected.
Hover checks names without codec startup or filesystem probing; Drop rechecks file
existence. Folders, URLs/text payloads and non-filesystem storage items are not imported.
Existing mounted/UNC filesystem paths follow the same validation as Open; no
streaming protocol or special network-share handling is added.

Invalid items are skipped with a concise warning. If none are valid, the queue/media
remain unchanged. Drops while opening/closing are rejected. Decoding failures still
use existing playback errors; file extension acceptance is not codec certification.

Headless tests exercise real Avalonia drag routing and real temporary storage files,
including multi-file replacement, append while paused, duplicate/order retention,
invalid transfers, copy-only effects/highlight cleanup, delayed initialization and
close during a pending drop. Managed tests check all seven extensions and invalid
path selection without altering sources. Native and actual Explorer/HWND checks
are recorded below after verification; headless tests do not prove Windows OLE input.

## PR #8 integration verification

The merged `0.4.0-alpha` Release build completed with zero warnings and errors.
The managed and headless UI suites passed 45 and 33 tests respectively; all 34
native decoder, lifecycle, and LibVLC cache-policy tests passed (112 total).
Package preflight and Windows architecture-selection regressions passed.

The fresh self-contained Windows x64 package contains the .NET runtime, LibVLC
native DLLs, all 321 x64 plugins, cache and integrity metadata, MIT license,
third-party notices, release notes, and usage/verification documentation. Its ZIP
extracted and passed dependency, hash, and plugin-timestamp checks across 667
files. The extracted executable was launched for MP3, FLAC, and H.264 MP4 playback
timing checks. The ZIP is approximately 95.4 MB, about 51% smaller than the
195,073,110-byte `v0.3.0-alpha` release ZIP.

On the Windows 11 integration host, the first MP3 run immediately after a fresh
ZIP extraction reported 37.52 seconds from process start to the `Playing` event.
Two subsequent fresh-process runs in the same Windows session reported 4.72 and
4.80 seconds. These are host-specific timings; they are not reboot-cold results,
and a `Playing` event does not confirm audible output. No reboot-cold measurement
was taken.

The earlier PR #7 verification record below describes manual Explorer/HWND checks
reported for that PR's extracted package; those checks were not repeated during
PR #8 integration. In this integration pass, the extracted executable was run
by the playback timing harness and the UI tests exercised drag/drop and
fullscreen behavior headlessly. No physical audibility, visible video, Explorer
drag/drop, fullscreen restoration, queue-transition, or relaunch result is
claimed for the integrated package. These desktop checks remain outstanding.

## Historical PR #7 physical verification record

The original PR #7 verification record stated that the following checks were
performed on its extracted Windows 11 package: multiple WAV/MP3/FLAC files
replaced the queue in supplied order and started the first item; MP4 replaced
audio and visibly rendered; dropping WAV directly onto the playing native picture
replaced video with audio; queue-panel append did not restart playback; Ctrl+drop
appended while paused without resuming; and a mixed supported/unsupported drop
played valid media with a skipped-item warning. These are historical reports for
the earlier package, not verification of the PR #8 integrated package. They do
not certify all Windows/Explorer versions, GPU drivers, virtual cloud providers,
or every codec. A physical Explorer drop while fullscreen remains unverified.
