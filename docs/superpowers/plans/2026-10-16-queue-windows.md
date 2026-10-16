# Queue and Windows player implementation plan

Goal: deliver a simple editable audio queue and a self-contained Windows 11 x64 folder build, on a pushed feature branch, without merging.

Architecture: PlayerSession owns ordered QueueItem objects, current identity and completion handling. IPlaybackEngine remains unchanged. Avalonia displays the queue and sends commands to the session. End events are dispatched onto the owner thread before any native command.

Constraints: no skins, visualizers, streaming, library, repeat or shuffle. Previous/Next do not wrap. Natural completion advances once and leaves the last item ended. Removing the current item stops playback; Play starts the first remaining item. Adding preserves current playback and starts only when no current item exists. Reordering preserves current identity, including duplicate paths. Failed playback stays visible without skipping. Open replaces the queue after path validation; Add appends. Queues are session-only.

## Task 1: session queue
- [x] Write behavioral tests for ordered add/open, duplicate identity, next/previous boundaries, reorder/remove, stop versus completion, stale/duplicate dispatched events, errors and preferences.
- [x] Run tests and observe missing queue API; implement QueueItem and PlayerSession queue commands; run managed suite.

## Task 2: window controls
- [x] Add headless interaction coverage for selected-item play/remove/reorder and transport.
- [x] Add multi-file Open/Add, visible current marker, selected-item controls and previous/next; run solution tests.
- [x] Accept file paths at startup for Windows Open with and drag onto the executable.

## Task 3: Windows distribution and verification
- [x] Name the executable LegacyMediaPlayer.exe; add a publish profile/script that preserves native plugin tree and checks runtime/native dependencies.
- [x] Add CI artifact, usage and verification notes. Run restore/build/tests/native queue integration tests/publish.
- [x] Launch the published executable on Windows and exercise available UI controls with MP3/WAV/FLAC. Record hearing-dependent checks separately.
- [x] Review changes. Commit/push, remote SHA and CI artifact results are recorded in the final handoff; do not merge.

Review focus: native callback deadlocks, stale completion after Stop/switch, duplicate filenames, missing files after enqueue, and selected versus playing queue item.
