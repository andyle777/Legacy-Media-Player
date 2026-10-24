# Subtitle support (0.5.0-alpha development)

This feature uses LibVLC's existing video renderer through IPlaybackEngine.
The Windows HWND host and playback queue remain unchanged.

## Use

Start a video, then use **Subtitles** to choose an embedded track or **Off**.
**Open subtitle...** accepts a local `.srt` without reopening or stopping video.
Unicode paths and text are passed to LibVLC using an escaped file URI. Network,
device and alternate-stream paths are rejected. Audio-only and idle media disable
the controls. A loading indicator and nonfatal error distinguish a requested
attachment from a confirmed native selection.

Fullscreen continues displaying native subtitles. Press Escape to change a
selection in the normal window, then F11 to return. Existing shortcuts are
unchanged. Duplicate track names show native IDs for distinction; IDs are never
treated as menu indexes.

Choices belong to the current queue entry, including Stop/Play and Repeat One.
Switching items resets the choice, even for duplicate paths. Embedded choices
are resolved again after reopening; ambiguous/unavailable matches show a warning.
Subtitle choices are not saved between launches. This PR adds no automatic
subtitle discovery; any native default/discovered track is reflected as native
state. Explicit local SRT loading is the supported external-file workflow.

## Evidence boundaries

The controlled `subtitles.mkv` and `subtitles.mp4` fixtures contain original
English/Vietnamese text; fixture provenance and generator are recorded in the
native Fixtures directory. Automated native tests use dummy outputs and verify
discovery, IDs, selection/Off, attachment, replay, transitions and transport.
They do **not** prove visibly rendered text or subjective timing quality.

After the modal/replay repair pass, the local run passed 157 managed,
73 headless UI and 66 native tests (296 total). Final head CI, packaging and physical results must be
recorded separately in the PR receipt; these counts are not a future-head claim.

The review identified six related lifecycle/confirmation issues, repaired with
deterministic native-boundary tests and real LibVLC regressions. File validation
now cancels its waiting operation promptly on Stop/replacement/supersession while
an abandoned filesystem call retains the single worker gate until it returns.
Requested choices are retained only after specific native selection is confirmed.

Attachments use non-selecting AddSlave, one submitted load at a time, then confirm
the specific completed slave URI and its uniquely added native track. They do not
infer an external attachment from a default embedded selection. Late older loads
cannot force selection; the latest explicit choice is reconciled on owner refresh.
Native metadata replay keys are separate from fallback display labels. Ambiguous
or unidentified replay explicitly selects Off. External source associations remain
available after Off and re-selection for the same current queue item.

Before replay, the adapter retains the subtitle intent; actual SRT attachment is
deferred until the input is active. Stop/Play with a retained choice recreates the
stopped native Media so prior slave lists and IDs cannot be mistaken for current
completion. Live attachment never reloads media. A dispatcher timer runs only
while subtitle work is pending, including paused inputs, with no background thread
or idle polling. This is necessary because a native ES-added event can precede
the completed media-slave list update.

## Remaining acceptance checks

The Windows tester confirmed readable embedded English/Vietnamese and Off in both
controlled MKV and MP4 files. A subsequent user-context hang dump showed the UI
thread inside OnEngineChanged -> StartItem -> LibVLC Stop during Repeat One,
while the native picker thread was still inside IModalWindow.Show. The repair
avoids automatic native Stop/Open/Play transitions during the modal interval;
Andy confirmed the extracted modal-fix build keeps the picker responsive across
the clip ending, restarts playback and displays readable external Unicode SRT.
He also confirmed pause/resume, seeking, F11/Escape, Stop/Play, Repeat One
retention and clearing the external choice when switching queue items.

### Modal picker behavior

Media continues while browsing. If it finishes, repeat/automatic advancement
waits until the dialog closes, then processes that ending once. This protection
covers Open/Add media, Open/Save playlist, and Open subtitle. Cancelling a dialog
or a picker error also releases the hold. Nested holds are idempotent; Stop,
queue replacement/removal, and disposal invalidate obsolete completion work.

The subtitle hold includes worker validation after the dialog closes. A valid
SRT selected after EOF is staged for replay without querying the inactive native
input. Its intent is separate from native confirmation, so an earlier native
subtitle error cannot erase it. Repeat One applies it to the same item on replay;
Repeat Off stays ended until Play. Switching items resets it. No new native
thread, dependency, playback backend, or persisted subtitle preference is added.

The earlier same-item revision repair prevents picker results/validation from
being discarded during replay; it alone did not fix the native modal hang.
Opt-in startup logs record subtitle picker and attachment boundaries without
paths. Automated managed/UI tests substitute the picker boundary and assert no
native Stop during a held natural ending. Native dummy-output tests cover the
EOF-to-SRT replay path, not physical dialog responsiveness or visible text.

Prior local fresh-cache generation timed out inside LibVLCNew, before subtitle
initialization. Local review packages may reuse the proven byte-identical native
tree/cache, with all ordinary cache-only startup, plugin/hash/architecture,
license and ZIP extraction checks retained. Source and cache provenance, final
counts, physical checks and CI results are recorded separately in the PR receipt.

- Read actual embedded English/Vietnamese and external Unicode SRT in the
  extracted Windows executable. Switch embedded tracks and Off.
- Check seeking, pause/resume, fullscreen/Escape, 0.5x/1x/1.5x/2x, Repeat One/All,
  Shuffle, duplicate queue entries, audio/video transitions and replacement.
- Check missing/corrupt files and changing/closing media during attachment.
- Verify the final self-contained ZIP, 321 x64 plugins/cache, runtime and notices.

## Limitations

Manual external loading initially supports SRT only. There are no subtitle
downloads, delay/style controls, subtitle preference persistence or custom
renderer. Native acceptance/selection does not certify visible text: a malformed
subtitle silently accepted by LibVLC may have no renderable cues. A file that
produces no confirmable track is reported nonfatally after a finite playing-time
window; paused playback does not consume that window.

Representative fixtures are not universal container, subtitle or GPU coverage.
The previously accepted intermittent extraction-first playback latency remains
an alpha limitation. Windows 10, other GPUs and multi-monitor/DPI cases still
need broader real-machine coverage. The known P3 shutdown-save warning visibility
issue is unchanged and outside this feature.
