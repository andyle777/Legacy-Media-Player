# Subtitle Support Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Embedded subtitle selection/Off and live local SRT attachment without interrupting video.

**Architecture:** Keep IPlaybackEngine, PlayerSession and the HWND. Native callbacks mark refresh needed; the serialized session owner reads current native state. Current-item choices survive Stop/Play and Repeat One, with new native IDs resolved after reopening.

**Tech Stack:** .NET 10, Avalonia 11.3.22, LibVLCSharp 3.10.2 / LibVLC 3.0.24, xUnit.

**Spec:** `docs/superpowers/specs/2026-10-23-subtitle-support-design.md` (approved by Andy).

## Global Constraints

- Development version remains `0.5.0-alpha`; no new dependency or renderer.
- Core cannot reference native/UI APIs; no native calls from native callbacks.
- No subtitle discovery implementation or subtitle preference persistence.
- No merge, tag or release; final PR remains draft.
- Preserve 321 x64 plugins, cache optimisation, licensing, queue and existing playback behavior.

## Review Focus

- Identical paths in distinct queue entries must not carry subtitle choices across items (task 1).
- Slow validation/pickers completing after Stop, replacement or close must not touch native objects (tasks 1 and 3).
- Native acceptance without track readiness must not display false success or block playback (task 2).
- Track IDs can change across reopen; ambiguous name matching must fail safely (tasks 1 and 2).
- A subtitle popup must preserve keyboard navigation and never initialize idle native playback (task 3).

## File map and interface ledger

Core: add `Subtitles.cs` for validated local SRT source and replayable selection; extend snapshot and engine contracts. Application: `PlayerSession.Subtitles.cs` owns current-item preference and asynchronous validation; existing CurrentItem/Stop/StartItem/Changed paths integrate it. Adapter: `LibVlcPlaybackEngine.Subtitles.cs` owns native attachment, event-driven refresh and bounded pending confirmation. App: `MainWindow.Subtitles.cs` owns the picker and compact control. Tests mirror those boundaries.

Pre-flight: task 1 produces `SubtitleFile.FromFile(string)`, `SubtitleSelection.Off/Track(int,string)/External(SubtitleFile)`, `IPlaybackEngine.SetSubtitles(SubtitleSelection)` and `RefreshSubtitles()`. Tasks 2 and 3 consume the same interfaces. Snapshot adds read-only `SubtitleTracks`, `SelectedSubtitleId`, `SubtitlePending`, `SubtitleRefreshRequired`, `SubtitleError`. Native refresh only reads current input after owner dispatch; no callback track ID is used. No conflicting interfaces found.

### Task 1: Core and serialized session workflow

**Files:** Core `Subtitles.cs`, `PlaybackModels.cs`, `IPlaybackEngine.cs`; Application `PlayerSession.Subtitles.cs`, `PlayerSession.cs`, `DeferredPlaybackEngine.cs`, `MockPlaybackEngine.cs`; tests `SubtitleTests.cs`.

**Interfaces:** produce the contract above plus session `SelectSubtitle(int)`, `LoadSubtitleAsync(string,CancellationToken,Func<string,SubtitleFile>?)`, `SubtitleInputRevision` and `CanUseSubtitles`. `LoadSubtitleAsync` returns `Task<bool>` and resumes on the owning context; callers must await on that context. Validation uses a single bounded worker gate and lifetime/supersession checks.

- [ ] Write deterministic tests: URI escaping/Unicode, invalid network/device/ADS/extension paths, no lazy startup, current-item versus duplicate-path lifetime, off/track selection, cancellation/close/supersession with blocked validator, failure preserves video, Repeat One replay and reordered item identity.
- [ ] Run `dotnet test tests/LegacyMediaPlayer.Tests -c Release --filter Subtitle`; expect missing-contract failures before implementation.
- [ ] Implement minimal records and session workflow. Preserve choices via semantic name on reopen; reset when CurrentItem identity changes. Defaults on IPlaybackEngine preserve unsupported test/backend compatibility; mock explicitly rejects selection. No file validation on UI.
- [ ] Run full managed tests; expect all pass. Commit `feat: add subtitle state and session lifecycle`.

### Task 2: Native implementation and fixtures

**Files:** adapter `LibVlcPlaybackEngine.cs`, new `.Subtitles.cs`; native `SubtitlePlaybackTests.cs`, controlled subtitle fixtures with provenance.

**Interfaces:** consumes task 1; implements `SetSubtitles` and `RefreshSubtitles`, with ES events only flagging refresh. Before-play external source uses Media.AddSlave; live uses MediaPlayer.AddSlave. Actual selection readback drives snapshot.

- [ ] Write tests for real embedded IDs/Off/live SRT, malformed/missing source, switch/reset, Stop/replay, pending errors, and native dispose.
- [ ] Run native subtitle tests; expect unavailable state/selection before implementation.
- [ ] Implement read-only native track snapshots, selection validation, event coalescing, replay name matching, finite confirmation (five seconds playing time) and nonfatal errors. Recheck local file at use, never preserve native IDs across Open. Do not permanently poll or create native worker threads.
- [ ] Run native subtitle and full managed/native suites. Commit `feat: render native embedded and external subtitles`.

### Task 3: Accessible UI and regression tests

**Files:** App `MainWindow.Subtitles.cs`, `MainWindow.axaml`, `MainWindow.axaml.cs`; UI `SubtitleWindowTests.cs`.

**Interfaces:** consumes task 1 session/snapshot; public `LoadSubtitleAsync(string)` lets actual picker and headless tests use one route. Capture input revision before picker and discard superseded results. Existing lifetime cancellation covers close.

- [ ] Write tests for idle/audio disabling, labels/Off/nonsequential IDs, pending/error, keyboard selection, no playback interruption, close/replace during picker validation, fullscreen continuing native video and lazy startup.
- [ ] Run UI subtitle tests; expect missing controls/behaviour.
- [ ] Implement labelled selector and separate Open Subtitle button, guarded refresh and concise status. Existing fullscreen hides controls; Escape allows selection. Keep all shortcuts unchanged.
- [ ] Run full managed/UI/native tests. Commit `feat: add accessible subtitle controls`.

### Task 4: Windows package, physical verification and draft PR

**Files:** architecture/user docs plus external verification receipt; no release metadata changes.

**Interfaces:** final exact implementation commit and existing packaging scripts.

- [ ] Run Release restore/build, full tests, architecture/preflight checks and `scripts/Package-Windows.ps1`; extract and verify all 321 plugins/cache/runtime/licenses and SHA-256.
- [ ] Run published player on Windows with embedded English/Vietnamese and external Unicode SRT. Separately record readable visual text, Off, timing/seek/pause, rates, fullscreen, repeat/shuffle, transitions and close. Ask for physical help only where automation cannot prove it. Leave unavailable checks pending.
- [ ] Perform one independent whole-branch review; fix actionable defects with reproducing tests and rerun required checks after changes.
- [ ] Commit verified docs, push normal feature history, open draft PR, verify Windows CI and report exact SHA, tests, package and observed limitations. Do not merge/tag/release.

## Execution notes

Andy explicitly approved the design and requested implementation. Proceed inline without another permission round; the plan makes the approved design concrete. Production work has not begun at plan creation.
