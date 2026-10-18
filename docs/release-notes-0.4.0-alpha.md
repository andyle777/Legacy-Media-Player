# Legacy Media Player 0.4.0-alpha — development notes

Adds local media drag/drop to the released 0.3.0-alpha audio/video player.

- Drop MP3, WAV, FLAC, MP4, MKV, WebM or AVI onto the picture/placeholder to replace the queue and play the first valid file.
- Drop onto the queue, or Ctrl+drop onto the picture, to append without interrupting a current or paused track. With no current item, playback starts as with Add.
- Multiple files retain supplied order and deliberate duplicates. Unsupported, missing, nonlocal and invalid items are skipped; no valid files means playback is unchanged.
- Targets highlight during a compatible drag. Only Copy is accepted; source files are never moved or modified.

Existing queue/navigation, seek, preferences, fullscreen, lazy LibVLC startup and MIT license remain in place. The portable Windows 10/11 x64 folder bundles .NET and LibVLC. This is a development review build, not a published release.

See drag-drop-verification.md for actual Windows findings and limitations. No directory recursion, subtitles, repeat/shuffle, installer or unrelated features are added. Codec/GPU coverage remains representative.
