# Legacy Media Player 0.4.0-alpha — development notes

Draft release notes for Andy and Leia's review. This is an unreleased Windows
10/11 x64 portable alpha candidate; no tag or GitHub Release has been published.

Retains the released 0.3.0-alpha local MP3/WAV/FLAC audio and MP4/MKV/WebM/AVI
video player through LibVLC, including representative H.264, HEVC, VP8, VP9, AV1
and MPEG-4 coverage. Codec/GPU coverage is representative, not universal certification.
Queue editing, Previous/Next/natural advancement, seeking, volume/mute,
fullscreen (F11/Alt+Enter/Escape/double-click), cursor auto-hide, existing shortcuts
and deferred native initialization remain available.

Also adds compact Repeat Off/All/One and Shuffle controls. Shuffle leaves the
editable queue in manual order, supports recent Previous/Next history, and
handles queue edits by item identity. Repeat One applies to natural completion;
explicit navigation still switches tracks. Mode preferences save alongside
volume/mute; playlist contents and history are not persisted.

- Drop MP3, WAV, FLAC, MP4, MKV, WebM or AVI onto the picture/placeholder to replace the queue and play the first valid file.
- Drop onto the queue, or Ctrl+drop onto the picture, to append without interrupting a current or paused track. With no current item, playback starts as with Add.
- Multiple files retain supplied order and deliberate duplicates. Unsupported, missing, nonlocal and invalid items are skipped; no valid files means playback is unchanged.
- Targets highlight during a compatible drag. Only Copy is accepted; source files are never moved or modified.

Existing queue/navigation, seek, preferences, fullscreen, lazy LibVLC startup and MIT license remain in place. The portable Windows 10/11 x64 folder bundles .NET and LibVLC. This is a development review build, not a published release.

The first-playback cache validation and Windows x64 package-size improvements
remain in place, retaining all 321 x64 LibVLC plugins.

The portable folder includes .NET, LibVLC, the MIT project license and third-party
notices. Keep the entire extracted folder together; no separate VLC/.NET install
is required. Verify the ZIP with its SHA-256 sidecar.

Known limits: alpha stability, variable first-file latency, no reboot-cold startup
characterization, broader Windows 10 and GPU coverage needed, and multi-monitor/DPI
edge cases needing more testing. There is no code signing or auto-updater. This
candidate excludes playback-speed control, subtitles, audio-track selection,
streaming, skins, visualizers and an installer.

See drag-drop-verification.md and repeat-shuffle-verification.md for Windows
findings and limitations. No directory recursion, subtitles, installer or
unrelated features are added. Codec/GPU coverage remains representative.
