# Legacy Media Player 0.5.0-alpha — development draft

Lightweight playlist management on the published 0.4.0-beta baseline.

- Open local M3U/M3U8 playlists into an idle queue; press Play when ready.
- Save the queue in UTF-8 M3U/M3U8, keeping order and deliberate duplicates.
- Save portable relative paths by default, or choose absolute paths. Imported relative paths resolve beside the playlist.
- Restore the previous queue after a normal restart without autoplay, a current-track marker, selected item or native codec initialization.
- Keep volume/mute, speed and repeat/shuffle preferences separate from the queue snapshot. Position and shuffle history are not restored.
- Skip missing, unreadable, unsupported and nonlocal entries with a warning. Failed imports preserve the old queue; valid empty playlists clear it.

Queue restoration uses a small last-queue.m3u8 file in the existing per-user settings folder. Exports and restart snapshots use flushed temporary sibling files and atomic replacement. No media-library database or extra playback controller is introduced.

## Limits

This is development alpha software, not a published release. Playlists contain local MP3/WAV/FLAC/MP4/MKV/WebM/AVI paths; URLs, UNC/device locations, nested playlists and directory scans are not supported. Extended comments/EXTINF are ignored. M3U8 requires UTF-8; legacy M3U can fall back to the current Windows ANSI encoding. UTF-16 and arbitrary legacy code-page detection are not supported. Use M3U8 for portable Unicode filenames.

Files are bounded to 4 MiB, 10,000 media entries and 32 KiB per line. Missing entries are skipped on import/restoration; there is no missing-item library. Normal close saves the queue; crash recovery, concurrent-instance coordination and playback-position/history persistence are outside this milestone.

Existing local audio/video, playback rates, repeat/shuffle, queue editing, Explorer media drops, fullscreen and lazy native startup remain. Keep the complete self-contained Windows x64 folder with its .NET runtime, LibVLC DLLs and all 321 x64 plugins, MIT project license and dependency notices.

The accepted intermittent extraction-first playback delay remains; prior investigation observed 3/27 first-file trials over five seconds, primarily native packetizer selection, without an established safe fix. Warm/restart behavior is distinct from reboot-cold startup. No performance workaround is added. Broader codec/GPU/Windows 10/DPI coverage remains representative rather than universal certification.

No subtitles, track-selection UI, streaming, skins, visualizers, installer, code signing or updater are added. No tag or GitHub Release is created by this PR.
