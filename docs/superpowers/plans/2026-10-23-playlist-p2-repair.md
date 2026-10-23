# Playlist P2 responsiveness repair

**Specification:** Andy's PR #11 P2 repair request, baseline fa2efce3f5267e48c08bc1b4f92cd1925ef187e3. Preserve idle imports/restoration, all-or-nothing validation, order/duplicates, local-path restrictions and existing playback behavior. Keep the P3 shutdown-warning issue separate. No merge, history rewrite, tag or release.

**Approach:** Measure the unchanged baseline with 100/1,000/10,000-entry mixed playlists. The parser/readability check already runs off-thread; prepare validated MediaSource instances on a worker too, then commit them through PlayerSession on the UI thread without filesystem access. Serialize preparation per window. Each import owns cancellation and identity; newer imports cancel older ones, and close abandons pending results without waiting for synchronous filesystem calls.

1. Record baseline parse/readability, isolated filesystem revalidation, dispatcher gaps, mutation and close measurements in an external probe/evidence folder.
2. Write deterministic regression tests for blocked validation, cancellation/close, supersession, serialized probes, no partial replacement, state/order/duplicates and idle restoration; observe failures before implementation.
3. Implement worker preparation and a filesystem-free queue commit. Preserve the synchronous ReplaceQueue API for existing non-UI callers. Keep playback-time validation for files disappearing after import.
4. Run managed/UI/native suites, Release build, preflight/architecture/cache/ZIP checks, repeat the same measurements and test the extracted Windows player. Review the complete repair once, address important findings with regressions, and report physical checks honestly.
5. Append normal commits to the existing PR branch, verify exact remote source tree and Windows CI, retain draft status and document measurements/limitations.

**Evidence:** C:/Users/lilan/Documents/Codex/lmp11-p2. Existing linked worktree and historical release artifacts are preserved. No additional implementation agents are needed; one final independent reviewer checks cancellation/generation ownership and filesystem-free commit.
