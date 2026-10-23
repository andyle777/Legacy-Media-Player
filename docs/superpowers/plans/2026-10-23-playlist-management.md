# Playlist Management Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Import/export local M3U playlists and restore the previous queue idle.
**Architecture:** One Application file service for public playlists and the last-queue.m3u8 snapshot. Existing PlayerSession owns queue identity/traversal; Avalonia owns pickers, cancellation and asynchronous preparation. IPlaybackEngine stays unchanged.
**Tech Stack:** Existing C#/.NET 10, Avalonia 11.3.22, LibVLCSharp, xUnit; no new dependency.
**Spec:** ../specs/2026-10-23-playlist-management-design.md (approved by Andy).

## Global Constraints

- Both Open Playlist and restart restoration replace the queue and wait for Play.
- Ordered duplicate entries survive; current/selected item and playback position/history are not restored.
- Do not scan directories, fetch URLs or follow UNC/device/nested playlist entries.
- Preserve deferred LibVLC startup, all 321 plugins, cache policy, runtime, licenses and existing controls.
- Branch feat/playlist-management, development version 0.5.0-alpha; draft PR only, no merge/tag/release.

## Review Focus

- Closing while startup restoration is pending must preserve the prior session file.
- First Play/Next from a restored queue must prepare asynchronously, never synchronously stall the UI.
- Relative filenames starting with # or spaces must round-trip without becoming comments/losing identity.
- A canceled/failed export must preserve the previous destination and current playback.
- Failed/all-invalid import must leave the old queue untouched; a valid empty playlist must clear it idle.

### Task 1: Bounded M3U service and atomic file writes

Files: create Application/PlaylistFileService.cs and managed PlaylistFileTests.cs.
Interfaces: IPlaylistFileService.LoadAsync(string,CancellationToken) -> Task<PlaylistReadResult>; SaveAsync(string,IReadOnlyList<string>,bool relativePaths,CancellationToken) -> Task. Result exposes Paths, EntryCount, SkippedCount.

- [ ] Write failing round-trip/Unicode/relative/duplicates test; run filtered managed tests and record RED.
- [ ] Implement UTF-8 M3U8, M3U ANSI fallback, comments, local-only validation, 4MiB/10,000-entry/32KiB-line bounds, ordered skip warnings. Save through a flushed sibling temporary file and atomic replace; validate cancellation before replacement and clean temporary files.
- [ ] Add and run malformed/missing/network/device/empty/bounds/export-failure/cancellation tests, including # and leading-space filenames.
- [ ] Commit the tested file service and approved design/plan.

### Task 2: Idle queue replacement and traversal safety

Files: modify Application/PlayerSession.cs; create managed PlaylistSessionTests.cs.
Interface: PlayerSession.ReplaceQueue(IEnumerable<string>) replaces valid paths without Open/Play; retains modes, clears CurrentItem and resets traversal after Stop/cancel-completion.

- [ ] Write failing idle/lazy/duplicate identity, queue edit, repeat/shuffle/rate, stale completion and disposal tests; record RED.
- [ ] Implement queue replacement with full validation before mutation. Reuse existing QueueItem and traversal; no native changes.
- [ ] Run focused tests and commit.

### Task 3: Playlist UI, restart persistence and lazy transport

Files: create App/MainWindow.Playlists.cs; modify MainWindow.axaml(.cs), App.axaml.cs; create UiTests/PlaylistWindowTests.cs.
Interfaces: MainWindow receives optional IPlaylistFileService and sessionPlaylistPath; exposes OpenPlaylistAsync(path), SavePlaylistAsync(path), RestoreQueueAsync(). Public playlist loads and startup restoration have separate error policies. Existing media methods remain available.

- [ ] Write failing headless tests for distinct controls, portable save, idle import/restore/current marker, async first transport, close cancellation and prior snapshot preservation; record RED.
- [ ] Add Open Playlist/Save Playlist and relative-path controls. Perform file I/O off-thread with lifetime cancellation and busy guards. Clear list selection on replacement; show idle queue state rather than the old media filename.
- [ ] Share asynchronous playback preparation for Play, selection/double-click, Next/Previous and their keyboard paths. Existing initialized/mock paths complete synchronously.
- [ ] Restore last-queue.m3u8 after opening the window unless command-line media overrides it. On normal close atomically save once before disposal, with async closing guard. Failed/pending restoration must not overwrite the prior snapshot without a user queue edit. Headless/mock tests never implicitly read real settings.
- [ ] Add drag/drop/edit/shortcut/persistence/error regressions; run managed/UI suites and commit.

### Task 4: Review, packaging and draft PR

Files: update Directory.Build.props, CHANGELOG.md, README.md, architecture docs and release-notes-0.5.0-alpha.md; add playlist verification notes.

- [ ] Set development version/notes; run Release restore/build, managed/UI/native tests, preflight and x64 selection. Restore Windows publish profile before using --no-restore.
- [ ] Build a new versioned ZIP, extract and verify all files, plugin timestamps/cache, runtime and notices; preserve old output folders.
- [ ] Obtain fresh whole-branch adversarial review; repair actionable defects with failing regressions and retest.
- [ ] Launch the extracted Windows app; save/reopen a playlist with duplicates/Unicode/relative paths, restart idle, then play audio/video and check preserved controls. Report human listening/Explorer observations separately.
- [ ] Review staged files, commit, push branch, open draft PR, verify exact-head CI; report SHA, tests, package and limitations. Never merge/tag/release.
