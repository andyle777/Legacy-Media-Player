# Repeat and shuffle development verification

Baseline: merged PR #8, main 2c9a664a18cf678a0d8fbd24913334490ceb4cc0.
Version remains 0.4.0-alpha. This change does not publish a release or tag.

PlayerSession owns repeat policy and shuffle traversal; Core, IPlaybackEngine,
LibVLC, native video hosting and deferred initialization are unchanged. A seedable
Random shuffles a bag of stable QueueItem references. Visible queue order is never
randomized. Duplicate paths retain their separate identities. Recent history is
bounded to 256 entries. The normal per-user settings file stores only audio/mode
preferences, never queue contents, history or media paths.

Repeat One handles natural completion only. Explicit Next/Previous and choosing
a queue row still navigate. Repeat All refills the shuffled bag and avoids the
last entry first if another entry exists. Appends join the remaining traversal;
removals prune bag/history; removing the current entry stops playback. Play then
continues with forward history/unplayed entries, starting a fresh traversal if
the removed current item had exhausted the previous one. Reordering leaves the active
shuffle cycle intact; switching shuffle off uses the visible order. Empty queues
stay idle, and a single item loops only in Repeat All/One. Failed files remain
selected with a recoverable error, rather than creating an automatic retry loop.

## Automated coverage

Managed tests exercise natural endings (including duplicate queued notifications),
all repeat combinations, seeded traversal, cycle boundaries, Previous/forward
history, manual choices, duplicates, add/remove/reorder/replacement, empty/single
queues, Stop/dispose cancellation, and legacy/invalid settings.

Headless UI tests exercise real controls/accessibility, minimum-width layout,
empty-window mode restoration without native startup, save-on-close, routed
drag/drop replacement and append while paused, fullscreen and existing shortcuts.

Native tests use the real bundled LibVLC with dummy output to check natural
completion/repeat and shuffled MP3/FLAC/MP4 traversal, plus existing decoder and
lifecycle coverage. Distinct completions require an intervening Playing event;
multiple Ended snapshots from native teardown do not count as extra plays.

Headless tests and native output events do not prove visible pixels, audible
sound, physical Explorer drops or all GPU/DPI combinations. Reboot-cold first-play
latency remains uncharacterized. See first-playback-performance.md for the cache
policy and earlier measurements; no native plugin, codec or runtime is removed.
