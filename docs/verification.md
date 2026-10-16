# Milestone 0 verification — 16 October 2026

## Environment and preservation

GitHub repository: andyle777/Legacy-Media-Player. Both the remote and original
local checkout were empty, with no commits or source files. No AGENTS.md or
project conventions were present. Implementation used an independent clone on
`milestone-0-bootstrap` inside this task's outputs folder. Existing work was not
overwritten.

The machine initially had only .NET 6.0.32 runtimes and no SDK. The official
installer installed SDK **10.0.402** into this task's `work/dotnet` directory.
No machine-wide SDK, PATH or editor settings were changed. For development in a
normal terminal, install that SDK globally or use the provided development
environment script. Runtime-only installations cannot build the source.

Git had no author identity configured. The local bootstrap commit uses the
agent identity `Codex <codex@localhost>` without changing any Git identity
settings. Configure your own Git name/email before making later commits.

## Checks and results

| Check | Result |
| --- | --- |
| Release solution build | Passed, 0 warnings and 0 errors |
| Managed behavior tests | 19 passed |
| Headless UI tests | 2 passed: real thumb drag and Arrow Up seek |
| Native LibVLC smoke tests | 3 passed using a generated PCM WAV and dummy outputs |
| Self-contained win-x64 publish | Passed; full folder retained locally |
| Source formatting and whitespace checks | Passed |
| Mock executable startup/shutdown | Main window created, exit 0 |
| Native executable startup/shutdown | Main window created, exit 0; observed startup 27 seconds |

Native checks verify decoding progress, duration, natural end, reopen/stop,
damaged-file error/recovery, invalid commands and disposal. They do not verify
audible sound or rendered video. UI tests exercise the real window with the
mock engine, not native video rendering.

The first behavior run failed all 18 original tests against unimplemented
methods. They passed after implementation. Independent read-only review found
the seek event and picker-error issues; their regressions failed before the
fixes and passed after them. No critical review findings were raised.

The first all-platform Avalonia restore failed NuGet audit on a vulnerable
Tmds.DBus.Protocol dependency. The Windows-only Win32/Skia package selection
removed it; warnings and audit were not suppressed. LibVLC's damaged-file
end/stop sequence was traced and handled explicitly rather than weakening the
test.

## Remaining validation and limits

- Native desktop startup initially exceeded a 45-second timeout; a diagnostic
  retry succeeded in 27 seconds. Investigate plugin-cache generation/loading and
  measure on clean Windows machines before claiming lightweight startup.
- No Windows 10/11 machine matrix or mixed modern/legacy codec corpus has been
  exercised. Audio devices, hardware decoding, rendered video, subtitles, queue,
  fullscreen, long-running playback and resource stress remain v1 work.
- GitHub Actions has been configured but cannot have run until a branch is
  pushed. No remote commit, push, PR, merge or release was performed.
- The bootstrap has no chosen project license, installer, signing or completed
  redistribution notices. The self-contained output is a local development
  build, not a release package.
- Linux and Android remain future integration work; macOS is not targeted.

See [milestones](milestones.md) for the remaining acceptance work.
