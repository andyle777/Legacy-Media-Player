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

## Automated and package results

Windows Release restore/build passed with zero build warnings/errors. Final tests:
45 managed and 33 headless UI, plus 25 unchanged real-native decoder tests (103
total). The package preflight regression passed. Local restore disabled NuGet audit
for the environment only; CI/repository auditing remains enabled.

The 0.4.0-alpha self-contained Windows x64 package includes the MIT license, runtime,
LibVLC DLLs, 321 plugins and generated cache. ZIP extraction matched all 1,497 files,
including plugin timestamps. The extracted executable was launched from
`C:/Users/lilan/Documents/Codex/lmp-v040-dragdrop-review/verified-LegacyMediaPlayer-v0.4.0-alpha-win-x64/LegacyMediaPlayer-v0.4.0-alpha-win-x64/LegacyMediaPlayer.exe`.
ZIP: 189,681,876 bytes; SHA-256
`4ae0a37ebab82d5eb5f70543fe0a9d9c8e5388e2d53c61e8abb060a75a60588c`.

The automation tool rejected an Explorer drag endpoint outside its source window
(773 by 594 pixels). Physical Explorer drops were therefore handed to Andy using
controlled audio/video fixtures. Andy confirmed all three physical checks passed:
multiple WAV/MP3/FLAC files replaced the queue in supplied order and started the
first item; MP4 replaced audio and visibly rendered; dropping WAV directly onto
the playing native picture replaced video with audio. This is real Explorer/HWND
input confirmation, separate from the headless tests. The existing HWND input
bridge needed no change. Queue/Ctrl append and mixed-invalid desktop checks are
recorded separately when confirmed.
