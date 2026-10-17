# Path to Legacy Media Player v1

## Milestone 0 — framework bootstrap

Four projects, clean engine boundary, mock and native engines, simple diagnostic
shell, tests, documentation and Windows CI. A successful bootstrap proves that
the architecture builds and a real decoder can run. It is not codec certification.

## Milestone 1 — functional audio playback

Open local MP3/WAV/FLAC with LibVLC through the existing engine boundary.
Play, pause, stop, seek, volume and mute; filename, state and elapsed/total time;
resource cleanup and recoverable errors. Verify focused behavior/UI/native tests
and real Windows audio before considering integration into main.
See [the verification and manual checklist](milestone-1-audio.md).

## Remaining basic player work after audio

The queue/Windows milestone adds a small editable queue, previous/next,
automatic advancement dispatched out of native callbacks and a self-contained
Windows folder build. See [usage and packaging](windows-player.md).
Fullscreen/Escape, keyboard shortcuts and a versioned portable review ZIP are now
implemented. Track/subtitle menus remain future work. See [fullscreen/package checks](fullscreen-verification.md).

## Milestone 2 — compatibility and robustness

Use a legally shareable fixture collection spanning MP3, WAV, FLAC, AAC, Opus,
Vorbis; MP4, MKV, WebM, AVI; H.264, HEVC/H.265, VP9, AV1. Record container/codec,
duration, audio/video output and CPU/GPU decoding separately on Windows 10/11.
Include variable frame rates, multiple tracks, subtitles, long paths, Unicode,
empty/truncated/corrupt files, rapid open/seek/stop, device changes, suspend/resume
and long playback. Check memory/handle growth and repeated close/reopen.
Conduct adversarial code review before treating the core as stable.

## Milestone 3 — distribution

The original source is MIT licensed and Windows packages include project and
third-party notices. Complete redistribution review, define supported Windows
builds, produce signed Windows packages,
test on clean Windows 10/11 x64 machines, document recovery/uninstall and set
release/versioning policy. Publish only after the compatibility checklist passes.

## Later

Linux desktop and Android need dedicated hosts, surfaces, package/runtime
integration, interaction design and CI. macOS is not a target. Legacy Media
Player EX can explore skins, visualizers and richer customization after v1.
