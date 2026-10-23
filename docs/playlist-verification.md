# Playlist management verification

Open Playlist replaces the queue and waits for Play. Save Playlist writes the visible manual queue order, including duplicates; shuffle never changes that order. Relative paths default on for portable exports. Move a playlist together with its media directory structure to keep those paths usable. Different drives retain absolute paths.

The previous queue is stored in `%LOCALAPPDATA%/LegacyMediaPlayer/last-queue.m3u8`, with absolute local media paths. On normal restart, readable entries return idle, with no selected/current row, triangle, position or playback history. Preferences remain in audio-preferences.json. Command-line media overrides restoration; mock/headless windows do not implicitly read real user state.

Check a mixed Unicode/Vietnamese MP3/FLAC/H.264 queue with repeated filenames: save relative M3U8, reopen it, then close/relaunch. Verify order and duplicate identity, idle state, Play/Next initialization, repeat/shuffle, rate, seeking, fullscreen and media drops. Save an absolute M3U and reopen it too. Delete a fixture after saving and verify a skipped-item warning. Empty playlists clear the queue; failed/all-invalid reads leave it unchanged.

Automated tests cover parser round trips, limits, unsafe/missing/unreadable paths, atomic write failures/cancellation, queue identity/traversal, asynchronous transport, idle UI restoration and close during I/O. Headless/native tests do not establish actual Explorer drags, visible rendering or speaker audibility. Record extracted Windows desktop observations separately with the exact commit and ZIP hash.

Writes use a flushed sibling temporary file followed by atomic replacement. Closing during a failed/pending restore must preserve the previous snapshot unless the user edited the queue. A failed shutdown save preserves the previous file and still permits the app to close. Session persistence is for normal shutdown, not crash recovery or coordinating multiple instances; the last successful closing writer wins.

No URLs are fetched, directories scanned or nested playlists followed. M3U8 is strict UTF-8; M3U accepts UTF-8 with Windows ANSI fallback. Export is UTF-8. Comments/EXTINF are not executed. Limits are 4 MiB, 10,000 entries and 32 KiB lines. Existing native cache/321-plugin packaging policy and accepted first-file latency limitations are unchanged.
