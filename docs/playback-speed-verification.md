# Playback speed — beta development verification

Built on released v0.4.0-alpha, main `1d228d2fd78ea1e3d381dbba3a6e1375288064df`.
No published release, tag, main protection rule or playback renderer is changed.

## Contract

The compact Speed selector offers 0.5×, 0.75×, 1.0× (Normal), 1.25×, 1.5× and
2.0×. The selected preference is saved in the existing settings file. Missing or
invalid saved rates use 1× without discarding valid repeat/shuffle/audio fields.
Changing or restoring a rate while the queue is empty does not initialize LibVLC.

IPlaybackEngine owns the command and neutral snapshot state. Rate is the preference;
AcceptedRate is nullable native request/readback evidence, not measured output
speed. RateError is independent of decode failure. A rejected live change restores
the previous selector value and warns; a per-file failure to apply the existing
preference leaves that preference intact and does not stop otherwise playable media.
Open and Stop clear per-input acceptance; Play reapplies the preference, including
the asynchronous opening interval before the native Playing event arrives.

Transport commands remain serialized. Native callbacks only update managed data;
they never apply a rate or issue native transport. Queue traversal stays in the
existing PlayerSession. No second controller, custom stretcher, rendering backend
or decoder package was introduced.

## Automated and adversarial checks

Local Release verification on 19 October 2026 passed **182 tests**: 84 managed,
44 headless UI and 54 native/cache-policy. Restore/build completed with zero
warnings/errors; package preflight and x64 architecture selection passed. Local
restore disabled the restricted environment's NuGet advisory lookup only; no
repository or CI audit settings were changed. Exact-head CI and extracted desktop
findings are reported on the draft PR and in the external candidate receipt.

- Saved-rate round-trip and missing-selector tests failed before implementation.
- Managed tests cover all six rates, defaults/invalid input, lazy/concurrent
  initialization, disposal during initialization, transport, repeat modes,
  shuffle/history, queue replacement/append and failed-open recovery.
- Headless UI tests cover selector defaults, accessibility, persistence, rejected
  changes, focused keyboard behavior, drops and fullscreen integration.
- Native tests cover all rates through pause/seek/stop and audio/video switching,
  encoded MP3/FLAC completion, damaged-media recovery and controlled rejection/
  mismatched-readback policy. At 0.5× and 2× they check media-clock progression
  against monotonic elapsed time with broad bounds, rather than just getter values.
- Independent adversarial review found two defects: selection during asynchronous
  opening was not applied, and selector focus swallowed global shortcuts. Both
  received failing regressions and focused passing reruns after minimal repairs.
  The independent six-trial native reproduction changed from 6/6 failures to 6/6
  passing. Re-review found no further actionable defects.

Headless/native tests do not prove visible video, audio audibility, smooth frames
or pitch preservation. Native acceptance can differ from actual output behavior;
see [LibVLC's SetRate documentation](https://docs.videolan.me/libvlcsharp/api/LibVLCSharp.Shared.MediaPlayer.html#LibVLCSharp_Shared_MediaPlayer_SetRate_System_Single_).

## Desktop checklist

Use the newly extracted beta executable, keeping its full dependency folder intact.
Select speeds both with an empty queue and during MP3/FLAC or H.264 playback.
Check 0.5× and 2× audibility/continuity, visible video and fullscreen, pause/resume,
seek, natural completion, repeat/shuffle, Previous/Next, manual selection and
Explorer replacement/append. Close/relaunch should restore the chosen preference
with an empty queue. Verify selector keyboard operation without intercepting the
existing Open/Add/navigation/fullscreen shortcuts. Record actual desktop results
separately from automated tests and note file/output-specific limitations.

## Performance and packaging limits

Keep the complete 321-plugin x64 tree, cache validation, ZIP timestamp-shift policy,
runtime and all licenses/notices. Packaging uses the existing scripts and Optimal
ZIP compression. No dependencies or foreign architecture trees are added.

The accepted intermittent extraction-first latency remains unchanged in scope:
the earlier investigation had 3/27 first-file trials over five seconds, all the
first playback in newly extracted folders, primarily during packetizer selection.
No specific root cause or safe fix was established. Warm/restart timings are not
reboot-cold results. Broader Windows 10/GPU/DPI and codec coverage remain open.

This is a focused development PR, not beta publication or a merge authorization.
