# Legacy Media Player v0.5.0-alpha — draft release notes

Release candidate only. This document does not authorize a tag or publication.

Legacy Media Player is a lightweight, portable Windows 10/11 x64 media player. The ZIP includes the .NET runtime, LibVLC and its complete 321-plugin x64 tree. Extract the whole folder before running LegacyMediaPlayer.exe. Project code is MIT-licensed; dependency licenses and notices are included.

## New since v0.4.0-beta

- Open local M3U/M3U8 playlists into an idle queue; press Play when ready.
- Save playlists in UTF-8, preserving visible order and deliberate duplicate entries. Portable relative paths resolve against the playlist's directory; absolute-path export is also available.
- Restore the previous queue after normal shutdown without autoplay, a selected/current track or eager native initialization. Volume/mute, speed and Repeat/Shuffle preferences remain separate from queue data. Position and shuffle history are not restored.
- Keep large playlist parsing and filesystem validation off the UI thread, validate before committing queue changes, and guard cancellation, closing and superseded imports. Missing/unsafe entries are skipped with a warning; failed imports preserve the existing queue.
- Select embedded subtitle tracks or explicit Off, and attach a local external UTF-8 SRT while using the existing native video renderer.
- Retain the current item's subtitle choice through Stop/Play and Repeat One; reset it when changing queue items. Subtitle choices are not persisted across launches; automatic discovery is deferred.
- Keep the native file picker responsive across a natural media ending: playback can continue while browsing, but repeat/automatic advancement waits at EOF until the dialog closes. A valid external SRT selected after EOF can be applied on same-item replay.

## Retained from v0.4.0-beta

Local MP3/WAV/FLAC audio and MP4/MKV/WebM/AVI video through LibVLC; editable queue and playing-item marker; Previous/Next and automatic advancement; seeking; saved volume/mute; Repeat Off/All/One and Shuffle with playback history; six speed settings (0.5x, 0.75x, 1x, 1.25x, 1.5x, 2x); Explorer queue append, playback-area replacement and Ctrl-drop append; fullscreen with F11/Alt+Enter/Escape and video double-click; deferred native startup and verified plugin-cache/timestamp-shift handling.

Native rate acceptance does not itself measure audible or displayed speed. Pitch preservation and smoothness depend on the media and output; rejected native changes are reported without silently pretending they succeeded.

## Known limitations

This is alpha software. Representative codecs/containers and controlled subtitle fixtures are not universal format/GPU certification. Windows 10, other GPUs, multi-monitor and DPI edge cases need broader real-machine validation.

First-file preparation remains variable. A controlled single-system investigation recorded 33 first-file trials and 33 same-process replays; 4/33 preparations exceeded five seconds, including all three extraction-first launches. MP3 preparation median was 843 ms (718–19,842), FLAC 844 ms (709–8,725), and H.264 MP4 910 ms (737–12,277). Same-process open-to-Playing medians were 59–90 ms; that boundary excludes preceding Stop and first engine initialization and is not equivalent to first-file preparation. A separate unlogged empty-window startup median was 5.012 seconds. These are event/observer timings, not sound-onset or decoded-pixel measurements. They are not reboot-cold benchmarks. No proven underlying cause or safe production fix was established; no performance workaround is included.

Reboot-cold startup, audible onset and first-pixel timing remain unverified. Broader native hardware/output validation remains necessary; experimental isolation results are recorded in the verification report rather than treated as production playback certification.

Playlist files are limited to 4 MiB, 10,000 media entries and 32,768 characters per line. Local supported media paths only: no URLs, UNC/device paths, nested playlist traversal or directory scanning. M3U8 requires UTF-8; M3U supports UTF-8 with current Windows ANSI fallback, without arbitrary code-page detection. Use M3U8 for portable Unicode names. Queue persistence covers normal shutdown, not crash recovery or coordinating concurrent instances.

External subtitles initially support SRT only. There are no downloads, automatic discovery, delay/style controls or persisted subtitle choices. A malformed SRT may contain no renderable cues despite native acceptance. The existing nonblocking limitation in visibility of a failed shutdown-save warning remains deferred.

No audio-track selection UI, streaming, media library, skins, visualizers, installer, code signing or auto-updater is included. Preserve the complete extracted folder and notices.

Final candidate physical acceptance and release documentation integration must be completed before publication. Andy will authorize publication separately.
