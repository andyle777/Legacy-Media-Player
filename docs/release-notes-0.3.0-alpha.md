# Legacy Media Player 0.3.0-alpha — draft release notes

A portable Windows 10/11 x64 development player inspired by classic desktop media players.

- Local MP3, WAV and FLAC audio playback, seeking, volume and mute.
- Editable session queue: add/remove/reorder, Previous/Next and natural advancement.
- Deferred LibVLC initialization and a generated plugin cache keep codec startup out of empty launch.
- Embedded, aspect-fit local video: representative MP4, MKV, WebM and AVI samples.
- Fullscreen video with F11, Alt+Enter, Escape and video double-click; playback and queue state are retained.
- Space toggles Play/Pause; Ctrl+O opens, Ctrl+Shift+A adds, Ctrl+Right/Left changes track,
  Left/Right seeks, Up/Down adjusts volume, and M mutes. Sliders/queue keep their normal arrow navigation.
- Volume/mute persist between launches.
- Self-contained Windows x64 folder and versioned ZIP include LegacyMediaPlayer.exe,
  the .NET runtime, LibVLC DLLs/plugins/cache, usage notes and third-party notices.

Representative video checks cover MP4/H.264+AAC, MKV/H.264, WebM/VP8,
AVI/MPEG-4 Part 2+PCM, and MP4/HEVC, VP9 and AV1. Previous Windows verification used
Intel UHD 620; alpha testing is representative rather than universal codec/GPU certification.
See fullscreen-verification.md for the checks performed on this exact milestone.

Known limits: no installer, code signing or automatic updater. No subtitles/track
menus, streaming, skins, visualizers, library or fullscreen overlay controls.
Windows 10 and other GPUs still need manual coverage. Reboot-cold startup and
antivirus first-launch effects are not fully characterized. Headless CI does not
prove visible rendering or physical audibility. Queue contents are not saved.

Extract the entire ZIP and run LegacyMediaPlayer.exe; keep all companion files.
No separately installed .NET runtime or VLC application is needed. Exit fullscreen
before using visible queue/seek controls, or use the existing keyboard shortcuts.

This is a review candidate: no GitHub Release/tag has been published. The project's
own source license remains Andy's decision before public redistribution.
