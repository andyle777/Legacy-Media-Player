# Legacy Media Player 0.4.0-beta — development draft

Playback-speed development on the released 0.4.0-alpha baseline. This branch
does not publish a beta Release/tag or change the historical alpha distribution.

- Choose 0.5×, 0.75×, 1.0× (Normal), 1.25×, 1.5× or 2.0× near the transport controls.
- Mouse or keyboard selection; the preferred speed is visible and persists
  alongside volume/mute and repeat/shuffle. The queue itself is not persisted.
- Select before playback without initializing LibVLC. Pause/resume, seek,
  stop/restart, repeat, shuffle, queue navigation and drops retain the preference.
- A rejected live change keeps the previous preference and displays a warning.
  Per-file rejection does not stop other playable content or erase the saved rate.

The existing Windows 10/11 x64 self-contained portable player, local audio/video,
editable queue, Explorer drops, fullscreen and deferred native startup remain.
Keep the entire extracted folder together. All 321 x64 LibVLC plugins, the .NET
runtime, MIT project license and dependency notices remain bundled.

## Limitations

LibVLC acceptance/readback reports a requested native rate, not measured audible
or displayed-frame speed. Rate support, pitch handling and video smoothness depend
on the file, decoder and output. No universal pitch-preservation or codec/GPU
certification is claimed. Native tests use dummy outputs; they do not visually
prove rendering or prove speaker audibility.

Intermittent first playback after extracting to a new folder remains an accepted
alpha limitation. The prior investigation observed 3 of 27 first-file trials above
five seconds, all extraction-first, with delays primarily during native packetizer
selection. It did not establish a specific root cause or safe production fix.
Warm playback and restarting an already-used extraction are different conditions;
reboot-cold startup, broader Windows 10/GPU and multi-monitor/DPI coverage remain
uncharacterized. No security exclusions or speculative preload workaround is added.

No subtitles/track-selection UI, streaming, skins, visualizers, installer,
auto-updater, code signing or unrelated features are included.
