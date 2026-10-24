# Subtitle support design

Status: Proposed; awaiting Andy's approval before implementation.

Branch: `feat/subtitle-support`.
Verified starting main: `3c5ad0d237ade34c30344e104ee7272416bcc7bb`.
Development version remains `0.5.0-alpha`.

## Intent and scope

Make local video subtitles usable through LibVLC's existing renderer: enumerate embedded tracks, select a track or Off, and attach a local SRT while video plays. Keep the existing queue controller, deferred engine and Windows HWND surface. PR #11 is merged and its post-merge Windows CI passed all 242 tests.

No custom renderer, video-host replacement, subtitle downloads, network URLs, offset/style controls, audio-track menu, new dependency, automatic discovery implementation or persisted subtitle preferences. No performance optimisation or unrelated redesign. No merge, tag or release is authorised.

## Approaches and proposed choice

The existing architecture and the request rule out a custom renderer or direct LibVLC calls from the window. Within the native approach:

1. **Event-driven native refresh (recommended):** native stream events signal that the serialized playback owner should refresh current tracks and actual selection. Use a bounded refresh only while an attachment/selection is pending. This fits the existing data-only callback boundary and avoids permanent polling.
2. **Continuous track polling:** easier to discover late tracks but introduces recurring native work even when nothing changes. Not proposed.

Andy requested this written design before deciding to implement. The recommended approach and the behaviour choices below are proposals, not recorded approvals.

## Verified installed API

Installed LibVLCSharp is `3.10.2`, with native Windows LibVLC `3.0.24`. Installed documentation and matching upstream source confirm:

- `SpuDescription` returns descriptions with native IDs; `Spu` reports actual selected ID; `SetSpu(id)` returns acceptance.
- `MediaPlayer.AddSlave(MediaSlaveType.Subtitle, fileUri, select: true)` supports live attachment and updates the current media's slave list.
- `Media.AddSlave(...)` must be used before parsing/playing. It can stage a retained external subtitle before replay, but is not a fallback for a live attachment failure.
- ES-added/deleted/selected events can signal track refresh. Event payloads must not be treated as authoritative state for a later media input.

Primary API reference: [matching MediaPlayer source](https://github.com/videolan/libvlcsharp/blob/3082c09ca955131c705fc4767fde03e9c4a14999/src/LibVLCSharp/Shared/MediaPlayer.cs).

## Architecture and ownership

Extend the current playback abstraction with the smallest backend-neutral subtitle state and commands needed for refresh and local-file attachment. Core continues to have no Avalonia, LibVLC or Windows dependency. Do not introduce another playback controller.

Expose immutable current track descriptions, actual selected subtitle ID, pending status and a separate nonfatal subtitle error through the existing snapshot/notification flow. Track identities are native IDs scoped to the current input, never array positions. A refresh notification is coalesced and consumed by the existing serialized owner; callbacks do not query native properties, select tracks, dispatch blocking UI work or hold the snapshot lock during native calls.

Commands, refreshes, media changes and disposal share the current engine ownership boundary. Track notifications are wake-up signals: read current native state after dispatch instead of publishing a callback's old track ID. Tag asynchronous work with a monotonic input revision, and check it and disposal before committing results. Increment/invalidate revisions on replacement and shutdown, including reopening the same path. Pending work is bounded and cancelled/abandoned on supersession; there is no new permanent polling thread.

The deferred engine must return empty/unavailable subtitle state without initializing LibVLC. The mock engine and deterministic fakes implement the same contract. Empty startup, queue restoration and merely opening a control must remain lazy.

## Track and choice behaviour

- Video shows explicit **Off**, known native tracks and **Open Subtitle...**. Audio-only and empty media disable subtitle actions. An initially empty track list can populate as discovery completes.
- Labels use native names/languages when available, with a readable numbered fallback. Duplicate labels receive a distinguishing suffix; sanitize control characters and bound display text. Never infer IDs from display order.
- Display actual accepted native selection; a requested choice stays visibly pending until confirmed. Rejected changes keep the previous accepted state and show a concise warning. If the native engine changes selection independently, refresh the UI to match it.
- Selecting Off explicitly disables subtitles, including an externally attached track. It does not unload or restart the video.
- **Proposed lifetime:** remember the choice only for the current `QueueItem` identity through Pause, seek, Stop/Play and Repeat One. Repeat One currently reopens media, so restage a retained external file before Play and reapply an embedded choice only after fresh discovery confirms an unambiguous matching track. An ID from an earlier input alone is insufficient evidence. If it cannot be matched safely, show Off/unavailable and a warning rather than select a different track.
- Switching to another queue item clears the remembered choice and external association, even when the path equals the previous item. Returning later is a fresh selection. Repeat All, Shuffle, Next/Previous and replacement follow this rule; reordering the current queue item does not clear it. There is no cross-launch subtitle persistence.
- On a fresh item, reflect LibVLC's actual default selection rather than claiming the user selected it. This PR adds no automatic discovery policy. Existing native discovery, if present, is reflected as native tracks and documented during verification; explicit Open Subtitle remains the supported external-file workflow.

## External SRT workflow and errors

1. Open Subtitle uses a local `.srt` picker without pausing, stopping or reopening the current video. Capture current item/input revision before awaiting the picker or validation.
2. Validate the selected local path off the UI thread, using the project's local-path rejection rules. Reject network/UNC/device/URI paths, alternate data streams and malformed paths, including separator variants. Require a regular readable SRT file; do not recursively scan or probe arbitrary URLs. Construct an escaped absolute file URI with the platform URI API, preserving Unicode and reserved characters.
3. Do not load the complete file into managed memory or implement an SRT decoder. LibVLC owns text decoding, timing and rendering. Readability/extension checks do not certify valid SRT content.
4. Back on the serialized owner, recheck item, revision and lifetime before attaching. An older picker/validation/attachment result cannot affect a newer item, even if both have identical filenames. A newer attachment supersedes pending earlier work.
5. A successful AddSlave return means accepted, not rendered. Keep a pending indicator while the new track/selection becomes known. Use a finite refresh window (proposed maximum five seconds of live playback, suspended while stopped/paused); clear pending with a useful nonfatal message if no selectable track is confirmed. Eventual late discovery can update the track list but must not override a subsequent explicit user choice.
6. Missing, inaccessible, invalid-format, rejected and asynchronous subtitle errors must not stop audio/video or reset its position/rate. Preserve the prior selection when possible, reconcile actual native state, and allow another file or Off. Never convert an optional subtitle decoder failure into a fatal media failure. A corrupt file that native code accepts without a specific diagnostic cannot be certified as valid or visibly rendered; document that detection limit honestly.

Selecting a new external file does not attach it to subsequent queue items. Stop cancels in-flight validation/confirmation; an already accepted current-item association remains available for the next Play. Closing cancels outstanding work and suppresses late UI updates/errors; never wait synchronously for validation on the dispatcher. Native handles are released using the existing engine disposal path.

## Minimal UI

Add a compact labelled **Subtitles** control alongside existing playback controls. Keep Off and track selection separate from the Open Subtitle command so opening a picker is never mistaken for a track ID. Show loading/unavailable status and concise nonmodal errors through the existing status presentation. Use keyboard-operable controls, accessible names and tooltips; keep existing shortcuts unchanged.

Existing fullscreen hides transport controls. Preserve that contract: subtitles continue rendering fullscreen, and users press Escape to change their selection, then F11 to return. No fullscreen overlay or new global shortcut in this PR. Verify popup/focus behaviour around the HWND and that selecting tracks does not steal normal transport keys or reload media.

## Verification and acceptance

Before production changes, write an implementation plan after design approval. Test driven implementation will cover:

- Managed: empty/multiple/unnamed tracks, nonsequential IDs, Off, rejected changes, immutable snapshots, current-item lifetime, repeated same paths with distinct identities, media revisions, ordered/superseded attachments, cancellation, stop/dispose, Repeat/Shuffle and all rate paths. Control native refresh/readiness in fakes rather than timing-only assertions.
- Headless UI: availability, labels, keyboard/focus, pending/error states, actual versus requested selection, and no lazy initialization merely from startup/control use.
- Native Windows fixtures: MP4/MKV with known embedded tracks and external SRT, Unicode filenames/text, missing/corrupt SRT, accepted/off/selected IDs, transitions, seek/pause, rates, Repeat One replay and disposal. Event success is not a pixel/render assertion.
- Full Release restore/build and managed/UI/native suites, zero new warnings/errors; preserve playlist restoration/P2 responsiveness, drag/drop, queue marker, settings and fullscreen. Avoid repeating large stress investigations without evidence of regression.
- Self-contained x64 publish, ZIP extraction/checksum, runtime/licenses/notices, all 321 plugins, plugin cache and architecture/integrity checks. No dependency/plugin removals.
- **Actual Windows desktop:** read known embedded English/Vietnamese text; switch tracks and Off; attach external Unicode SRT live; verify timing after seek/pause, fullscreen/Escape, 0.5x/1x/1.5x/2x, audio/video transitions, Repeat One/All, Shuffle and queue replacement. Exercise missing/corrupt files, file switch during attachment and close. Report each observed result separately from automation, with any physical checks still pending.

Do not claim universal subtitle/codec coverage. Retain the accepted extraction-first playback latency caveat; no speculative latency fix belongs here. Keep test workloads paced on Andy's laptop.

## Delivery and builder discretion

Use normal commits on `feat/subtitle-support`, preserve reviewed history, push and open a **draft** PR (verify actual number, expected #12). Run Windows CI and report exact head, test counts, package path/hash, visual results, caveats and merge-readiness verdict. Do not mark ready, merge, tag or publish without separate authorisation.

The builder may choose precise record/method names, partial-file boundaries, event coalescing and finite retry intervals within these contracts. If installed native behaviour cannot satisfy a stated contract safely, stop and report the concrete discrepancy before expanding scope. The lifetime policy, pending/error semantics and normal-window-only selection UI remain explicit proposals for Andy's approval.
