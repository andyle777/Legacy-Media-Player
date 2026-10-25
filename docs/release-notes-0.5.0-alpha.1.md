# Legacy Media Player v0.5.0-alpha.1

**Alpha hotfix prerelease — portable Windows x64.** Extract the complete ZIP
before launching. The package includes the .NET runtime, LibVLC and all 321 x64
plugins, along with project and dependency license notices.

## Fixed

- Playback could fail when extraction rewrote LibVLC plugin timestamps even
  though the native plugin contents were intact.
- When plugin timestamp metadata differs from the trusted timezone-shift policy,
  startup now checks plugin SHA-256 hashes against the packaged manifest.
- Missing or modified native files, changed plugin contents, or inventory
  mismatches continue to be rejected with re-extraction guidance.

## Unchanged

- Portable Windows x64 packaging with the bundled .NET runtime, LibVLC, plugin
  cache, native SHA-256 manifest and complete 321-plugin tree.
- Local playlist management, embedded and external subtitles, playback speed,
  Repeat/Shuffle, fullscreen, audio and video playback.

## Known limitations

- First-playback preparation latency remains variable; no speculative
  performance workaround is included.
- Representative codecs, containers, subtitles, GPUs and output devices are not
  universal compatibility certification. Broader real-machine validation is
  still needed.
- Reboot-cold startup, audible-onset and first-rendered-pixel timing remain
  unverified. Native hardware/output behavior can vary by system.
- Playlists remain limited to local supported media paths, with the documented
  size, entry-count and line-length limits. External subtitles support SRT only.
- No streaming, installer, code signing, auto-updater, audio-track selection,
  subtitle downloads, automatic subtitle discovery, or persisted subtitle
  choices are included.
