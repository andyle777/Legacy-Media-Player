# Changelog

## 0.5.0-alpha.1 — hotfix candidate

- Fix playback startup when extraction rewrites intact LibVLC plugin timestamps:
  validate plugin SHA-256 hashes when timestamp metadata is outside the trusted
  timezone-shift policy.
- Continue rejecting missing or modified native files and plugin content.
- Retain the portable Windows x64 package, complete 321-plugin tree, playlists,
  embedded and external subtitles, playback speed, Repeat/Shuffle and fullscreen.

- Select embedded subtitles or Off, and attach local UTF-8 SRT through the existing playback abstraction/native renderer.
- Retain the current item's subtitle choice across Stop/Play and Repeat One; reset on queue-item changes. No discovery or subtitle preference persistence.
- Defer natural repeat/advancement while an owned native file picker is open, keeping subtitle selection responsive across EOF.
- Validate large playlist imports off the UI thread with cancellation, close and supersession guards before atomic queue replacement.
- Retain variable extraction-first playback latency as an alpha limitation; no speculative performance workaround is included.

- Import/export local M3U and UTF-8 M3U8 playlists with ordered duplicates and portable relative paths.
- Restore the previous queue idle after normal restart; preserve audio/rate/repeat/shuffle preferences.
- Separate playlist controls from media Open/Add; keep deferred codec startup when first playing a restored queue.
- Bound untrusted playlist parsing, skip invalid/missing/nonlocal entries, and atomically replace saved files.

## 0.4.0-beta — development

- Compact accessible playback-speed selector: 0.5×, 0.75×, Normal 1.0×,
  1.25×, 1.5× and 2.0× through the existing playback-engine abstraction.
- Persist speed alongside existing preferences without eager native startup.
- Keep speed across transport, queue changes, repeat/shuffle and local drops;
  report native rate limitations without stopping otherwise playable media.
- Retain the full 321-plugin Windows x64 package and prior cache validation.

Development only; no beta release or tag is published by this work.

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
