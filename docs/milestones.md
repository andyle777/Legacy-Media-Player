# Path to Legacy Media Player v1

## Milestone 0 — framework bootstrap

Four projects, clean engine boundary, mock and native engines, simple diagnostic
shell, tests, documentation and Windows CI. A successful bootstrap proves that
the architecture builds and a real decoder can run. It is not codec certification.

## Milestone 1 — complete the basic player

Add a small playlist/queue, previous/next, automatic advancement (dispatch out of
native callbacks), fullscreen/Escape, keyboard shortcuts, track/subtitle menus,
and accessible classic-style controls. Finish seeking/replay behavior, useful
loading/buffering presentation and error recovery for disappearing files.

## Milestone 2 — compatibility and robustness

Use a legally shareable fixture collection spanning MP3, WAV, FLAC, AAC, Opus,
Vorbis; MP4, MKV, WebM, AVI; H.264, HEVC/H.265, VP9, AV1. Record container/codec,
duration, audio/video output and CPU/GPU decoding separately on Windows 10/11.
Include variable frame rates, multiple tracks, subtitles, long paths, Unicode,
empty/truncated/corrupt files, rapid open/seek/stop, device changes, suspend/resume
and long playback. Check memory/handle growth and repeated close/reopen.
Conduct adversarial code review before treating the core as stable.

## Milestone 3 — distribution

Choose the source license, complete third-party notices and redistribution
review, define supported Windows builds, produce signed Windows packages,
test on clean Windows 10/11 x64 machines, document recovery/uninstall and set
release/versioning policy. Publish only after the compatibility checklist passes.

## Later

Linux desktop and Android need dedicated hosts, surfaces, package/runtime
integration, interaction design and CI. macOS is not a target. Legacy Media
Player EX can explore skins, visualizers and richer customization after v1.
