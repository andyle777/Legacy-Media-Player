# Lightweight playlist management

Baseline: published v0.4.0-beta, protected main cadc15ce464f5d70b964acb7c8a7e7e4b02d13d0. Feature branch: feat/playlist-management. Development version: 0.5.0-alpha. No merge, tag, release, database, new playback controller, subtitles or performance work.

## Intent and decisions

Andy wants portable playlists and the previous queue available after a normal restart. Open Playlist replaces the queue and waits for Play, as Andy chose. It never resumes playback automatically. Existing Open files/Add media and Explorer drop behavior remain unchanged.

## Approaches

We considered M3U/M3U8 import/export with a separate atomic JSON restart snapshot, and using M3U for both purposes. Andy selected M3U for restart restoration too. Use a UTF-8 last-queue.m3u8 file beside the existing per-user audio preferences. No separate JSON queue schema or stable cross-process IDs are needed: each imported occurrence becomes its own QueueItem, preserving duplicate reference identity within a session.

## File service

Add a small Application-layer playlist file service, independent of Avalonia and LibVLC. Support basic and extended M3U comments/EXTINF, UTF-8 M3U/M3U8, Unicode/Vietnamese filenames, relative and absolute local paths. M3U8 uses strict UTF-8; M3U may fall back to the Windows ANSI encoding for legacy files. Export UTF-8 for both extensions. Save portable relative entries by default where possible, retaining absolute paths across drives; the restart snapshot uses absolute paths. Resolve imported relative paths against the playlist directory. Preserve source order and every valid duplicate occurrence.

Reject URLs, UNC/device paths, nested playlists, directories, invalid paths and unsupported media types before attempting playback; do not enumerate directories or follow playlist instructions/metadata. Missing/unreadable entries are skipped with a concise count warning. Bound file size, entry count and line length to avoid unbounded work. Failed reads leave the current queue/playback unchanged. A genuinely empty/comments-only playlist clears the queue and remains idle; a nonempty playlist with no valid media leaves the current queue untouched and warns.

Export and restart persistence write a temporary sibling file, flush it, then atomically replace the destination. Cancellation before replacement preserves the prior file; clean up the temporary file. Save/export errors are reported without altering queue/playback or crashing shutdown. Store only queue paths, never native state, active playback position or history.

## Queue and UI

Add a PlayerSession operation that replaces queue contents without opening media or playing. It cancels old completion, stops old playback, clears CurrentItem, resets traversal and retains repeat/shuffle/rate/audio preferences. Restored queue has no selected/current entry or triangle; pressing Play starts according to the existing shuffle preference. Revalidate media when actually playing it. The idle queue view must not display the previous stopped media as its current file; the existing backend may retain its stopped media reference until the next open/disposal.

Add distinct Open Playlist and Save Playlist buttons plus an accessible portable-relative-path option. Keep existing keyboard shortcuts unchanged. File pickers remain distinct from media pickers. Materialize the queue paths on the UI thread; perform bounded file I/O off-thread. Ignore/cancel work after close, prevent conflicting playlist/drop/open operations while busy, and keep callbacks data-only.

Idle restored queues introduce new native-start entry points: Play, Play selected/double-click and Next/Previous must share the existing asynchronous preparation path, including keyboard equivalents. Do not synchronously initialize codecs on the UI thread, and do not initialize on import/restoration alone.

On normal close save the current queue once before disposal. On startup restore it idle without codecs; command-line media overrides restoration. Mock/headless tests use explicit injected stores, never real user files. No playlist/session position/history persistence or named-playlist collection UI.

## Verification and delivery

Managed tests: encodings/Unicode, relative/absolute paths, duplicates, comments, malformed/empty files, rejected network/device entries, missing/inaccessible files, bounds, atomic writes/failure/cancellation, idle restoration, stable identity and repeat/shuffle/rate consistency. UI tests: separate controls, import/export, no-autoplay/no native initialization, selected/current state, restored queue edits/drops, async preparation, picker cancellation and close during I/O/startup.

Run full Release restore/build, managed/UI/native suites, preflight, architecture selection, self-contained publish and extracted integrity/cache/license verification with all 321 plugins. Test saving/reopening and restart restoration on the actual extracted Windows app; distinguish visual/human evidence from automated tests. Push the feature branch and open a draft PR, verify CI, and report exact SHA/artifacts/limitations. All historical tags/releases and protected-main settings remain unchanged.
