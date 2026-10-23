# PR #11 P2: playlist import responsiveness

Baseline: fa2efce3f5267e48c08bc1b4f92cd1925ef187e3. Windows 11 local measurements, 2026-10-23. No native backend, codec, settings-format or package-policy changes.

## Cause and repair

PlaylistFileService already parses and checks readability on a worker. Its UI continuation previously called PlayerSession.ReplaceQueue, which repeated MediaSource.FromFile/File.Exists for every accepted entry on the Avalonia dispatcher. At 7,500 accepted entries this monopolized the dispatcher for roughly a second or longer.

PlayerSession.PrepareQueueAsync now validates the complete batch on a worker; ReplacePreparedQueue only constructs queue entries, stops current media and commits the collection/traversal on the UI thread. The existing synchronous ReplaceQueue API remains for non-UI callers. Play still validates its chosen file, since files can disappear after preparation.

Each window serializes read + preparation through one semaphore. Each import owns its cancellation source and UI cleanup. Newer imports cancel older callers; identity/token checks reject stale results. WaitAsync lets close/cancellation abandon even an uncooperative synchronous filesystem call. That call retains the gate until it actually returns, so a new import may wait behind it, but no unbounded parallel probes are started. No dispatcher waits on a worker synchronously. Missing session files are distinguished from media disappearing during preparation, preserving the old snapshot on validation failure.

## Method and measurements

External probe/evidence: C:/Users/lilan/Documents/Codex/lmp11-p2/probe, before-final.json, after.json and cancel-after.json. Three sequential trials per size, same harness and machine, fresh headless window each trial, reused local files/warm filesystem. Mixed playlists include ordered duplicate Vietnamese MP3 paths, FLAC/WAV/video, missing files and invalid URLs; 100/1,000/10,000 entries yield 76/750/7,500 accepted entries. No reboot-cold or physical-frame benchmark is claimed.

A background sampler posts to the actual Avalonia dispatcher every approximately 5 ms and records maximum post-to-execution delay per trial. It includes scheduler/layout/startup noise. Queue mutation is measured from first collection notification through import completion, including binding/control refresh. Text-read/comment-filter timing is a separate reference pass, not a claim to isolate the full production parser from path normalization. Production parsing + readability is measured together, with an additional isolated MediaSource/File.Exists pass reported separately. Component passes are not additive because they repeat I/O under different cache/scheduling conditions.

Milliseconds: median (minimum–maximum), n=3.

| Entries | Maximum dispatcher delay before | After | Queue mutation before | After |
| --- | ---: | ---: | ---: | ---: |
| 100 | 98.53 (94.53–522.31) | 106.61 (47.02–444.86) | 1.50 (1.11–9.16) | 0.80 (0.77–10.87) |
| 1,000 | 344.48 (139.57–421.88) | 43.28 (33.48–72.52) | 5.47 (5.04–48.42) | 4.10 (3.72–7.87) |
| 10,000 | 1,451.05 (1,396.86–1,941.77) | 46.35 (39.25–326.13) | 37.31 (22.32–44.21) | 22.88 (8.48–239.23) |

| Entries | Text read/filter median before → after | Production parse/readability worker median before → after | Isolated filesystem revalidation median before → after | Total import median before → after |
| --- | ---: | ---: | ---: | ---: |
| 100 | 0.46 → 0.42 | 30.25 → 15.42 | 10.43 → 14.26 | 45.30 → 24.42 |
| 1,000 | 0.62 → 0.49 | 265.54 → 139.58 | 176.90 → 78.34 | 568.68 → 253.46 |
| 10,000 | 2.55 → 3.02 | 2,590.02 → 1,659.65 | 1,926.37 → 1,088.13 | 4,034.20 → 2,708.56 |

The supported conclusion is removal of batch filesystem work from the UI thread, confirmed independently by controlled validators. Disk throughput was not optimized; total-duration differences are observational and affected by host/cache variability. Small-playlist dispatcher improvement is not established. A 326 ms post-repair dispatcher outlier and a 239 ms collection/layout commit remain; no universal frame-latency guarantee is claimed.

With an intentionally blocked reader, baseline import tasks remained pending after the 1,000 ms test deadline on cancellation and close. After repair, cancellation-to-import-return median was 6.82 ms (0.48–12.30); close-to-import-return 29.71 ms (8.93–31.97), n=3. Window closure was already allowed during baseline reads; the improvement is abandonment of the outstanding import task, not cancellation of the OS call itself. These samples use no persisted shutdown save and do not promise a bound for slow snapshot writes.

## Regression and review evidence

Controlled tests first failed on UI-thread probing, ignored newer imports and pending tasks after cancellation/close. Final managed/UI suite: 126 managed + 62 headless UI tests passed. Tests cover 100/1,000/10,000 entries, worker blocking, dispatcher callback execution, validation-before-mutation, failure/cancellation preservation, close, supersession, serialized readers, Unicode/duplicates, idle/current/selection state, repeat/shuffle/rate and drag/drop rejection while busy followed by append after completion. Existing malformed/unsafe playlist and startup restore tests remain. One read-only independent final review found no blocking defect.

Release build passed with zero warnings/errors. Full native retry passed 54/54 after the isolated test passed: 126 managed + 62 UI + 54 native = 242 passing tests, no skips. One initial native run hit the 90-second watchdog in the unchanged Video_output_failure_is_reported_even_when_audio_is_playable test. Preserve this observation and the local dump; do not infer a cause or silently discard the failed run.

Package preflight/architecture selection, all 321 x64 plugins/cache, bundled runtime/licenses and 669-file extracted ZIP integrity passed. Local repair ZIP: 95,439,163 bytes; SHA-256 a59b726a23351fd6019f05f7fea101c797d84df5e6975a748ea53ba23f8e3e53. Physical large-playlist and updated CI outcomes belong in the final external report/PR description, not inferred from headless results.

Opt-in existing startup diagnostics now include playlist.read, playlist.validation and playlist.commit begin/end stages with thread IDs. Normal UI remains quiet. Performance logging itself adds synchronous diagnostic-file writes when explicitly enabled; production runs leave it disabled.

## Separate limitations

P3 remains deferred: a failed shutdown snapshot save preserves the previous file but its warning closes too quickly to read. Accepted extraction-first native playback latency, last-successful-writer multi-instance behavior, syntactic local-path checks and representative codec/GPU coverage are unchanged. No merge, tag, release or PR-ready transition is authorized by this repair.
