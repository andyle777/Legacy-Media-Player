# Fullscreen and package verification

Version: 0.3.0-alpha. Fullscreen PR #5 was originally stacked on video PR #4 at
e6892b7191d65bd3fab2b648d1ac2a941c05c46d. PR #4 has since been merged into main
and #5 retargeted without duplicating video commits.

Fullscreen changes the existing Avalonia window state and layout only. The same
NativeControlHost and HWND remain attached; no playback command, source replacement
or backend initialization is needed to enter/exit. F11 and Alt+Enter toggle;
Escape exits. Audio/empty windows ignore entry, and switching to audio, stopping,
completion or failure restores normal mode. A new input remains fullscreen while
stream discovery is unknown; a non-video timestamp/pause returns to normal mode,
avoiding exits between video queue items. This is a heuristic: a video with delayed
stream discovery can briefly look like audio. Rapid Next then Pause before the
video callback may exit fullscreen; press F11 again once video is identified.
That event-order edge remains a manual follow-up; no metadata parsing or new Core
contract was added for this bounded UI change. Normal window size/position and the
previous maximized state are restored. Queue and ordinary controls are hidden;
existing transport/seek/volume/queue shortcuts remain available. Cursor auto-hide
uses a three-second UI timer only while fullscreen and active; mouse activity,
deactivation, exit and close restore/release the cursor and stop the timer.

Native input is disabled in the LibVLC adapter. Avalonia's two Win32 host windows
otherwise swallow mouse messages; a small UI-thread SetWindowSubclass hit-test
bridge passes input to Avalonia. It does not hook LibVLC's renderer-thread children.
References: https://github.com/AvaloniaUI/Avalonia/blob/11.3.22/src/Windows/Avalonia.Win32/Win32NativeControlHost.cs
and https://github.com/videolan/vlc/blob/3.0.24/modules/video_output/win32/events.c .
The hooks are removed before the host is destroyed. Headless UI tests cannot prove
this Win32 input/rendering behavior; published Windows checks are separate.

Build/package command: scripts/Package-Windows.ps1. It calls the self-contained
Windows publish, generates plugins.dat, requires runtime/native/notices files,
creates a versioned ZIP, extracts to another directory and compares every file's
size and SHA-256. Plugin modification times are also compared, because native
cache validation depends on them. A SHA-256 sidecar accompanies the ZIP. Existing
ZIP/verification output is never overwritten; select a clean OutputDirectory.

One probe publish timed out at 180 seconds during cache generation while other
build work was active. The exact standalone generation command then completed in
about 38 seconds. This does not establish the original cause; cold probing,
filesystem/antivirus effects and contention remain possible. No timeout was hidden
or raised, and empty launch still defers codec initialization. Final packaging
must complete its own generation and relocated playback checks.

This is a draft review package, without GitHub Release/tag, signing, installer or
auto-update. The original project source is now MIT licensed; bundled dependencies
retain their separate terms. The desktop results below describe the earlier
review package, before the MIT license was added.
Windows 10 and other GPUs still need desktop coverage; representative formats are
not universal codec certification. CI dummy outputs do not prove visible pixels
or physical audibility.

## Windows 11 review results (17 October 2026)

Release restore/build passed with zero build warnings/errors. Tests: 37 managed,
23 headless UI, 25 real-native dummy-output tests (85 total). The package preflight
regression also passed. A previously timing-sensitive UI fake backend watchdog
was extended from 5 to 30 seconds; its independent responsiveness assertions remain.

The 0.3.0-alpha ZIP built successfully, including 321 native plugins and notices.
All 1,495 files matched after extraction (length and SHA-256); plugin mtimes matched.
A missing-notice negative control was rejected, then the notice was restored and
dependency checks passed again. ZIP size: 189,675,640 bytes. SHA-256:
`a10adcce94e8caf624ac96f890a2501af1abb75bf051271f572eb86012448781`.

The executable was launched from the extracted ZIP directory, not the build output.
MP4/H.264 and MKV/H.264 visibly rendered on Windows 11 / Intel UHD 620. F11, Escape,
Alt+Enter and native-video double-click toggled fullscreen. Playing/paused media,
seek position and the six-item queue survived transitions. Video stayed aspect-fit
in normal, maximized and fullscreen layouts; exiting restored normal/maximized
state. Idle cursor hiding and recovery on mouse activity were observed. Previous/Next,
video-to-video continuity, natural video-to-audio advancement, audio-to-video,
fullscreen seek/pause/resume and volume/mute responded. MP3/WAV/FLAC clocks advanced;
Andy confirmed audible tone output on this extracted build. Closing while fullscreen
released the process; relaunch restored volume 50/mute true and opened video again.

Three fresh empty processes from the extracted package reached the first-frame
callback at 2575.181, 2424.481 and 2254.976 ms, with no LibVLC initialization stages.
These are warm-session samples, not reboot-cold timings or proof of speed on other
machines. HEVC/VP9/AV1, WebM and AVI have baseline video-milestone coverage but were
not visually rerun for this fullscreen package. Rapid Next-then-Pause during delayed
video discovery, multi-monitor/DPI transitions, Windows 10 and other GPUs remain
manual follow-up checks. Headless tests do not establish physical rendering.

Stop after relaunch returned to Stopped at 0:00 with the video hidden. The player
was left unmuted after checking saved mute; no test fixtures enter the ZIP or Git.
The ZIP was created before this results appendix; its included verification note
describes the method. This repository file holds the completed desktop evidence.
