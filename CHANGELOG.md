# Changelog

## 0.4.0-alpha — unreleased candidate

- Windows Explorer drag/drop: replace/play on the media area; append on the queue
  or with Ctrl. Preserve order and duplicate entries; skip invalid items safely.
- Validate the packaged LibVLC cache across uniform ZIP timezone timestamp shifts
  to avoid unnecessary plugin discovery on first playback.
- Exclude unused x86/ARM64 LibVLC trees from the Windows x64 package while retaining
  all 321 x64 plugins, the bundled runtime and license notices.
- Repeat Off, All and One; seeded/testable shuffle traversal without moving the
  visible queue, with recent Previous/Next history and stable identity during edits.
- Persist repeat/shuffle preferences alongside volume/mute, without saving playlists.
- Mark the current queue item independently of the row selected for editing.
- Retain local audio/video, queue editing, fullscreen, keyboard shortcuts and lazy
  startup from 0.3.0-alpha. First-play latency remains variable.

This candidate adds no speed control, subtitles, track-selection UI, streaming,
skins, visualizers or installer. No tag or GitHub Release is created by preparation.

## 0.3.0-alpha — 2026-10-18

- First published portable Windows x64 alpha: MP3/WAV/FLAC and embedded local video
  through LibVLC; editable queue, navigation, natural advancement, seek and mute.
- Fullscreen video (F11, Alt+Enter, Escape and double-click), cursor idle hiding,
  deferred native startup, persisted audio preferences and MIT project license.
- Self-contained .NET/LibVLC folder ZIP with licenses and SHA-256 sidecar.

Codec/GPU coverage is representative rather than universal certification.
