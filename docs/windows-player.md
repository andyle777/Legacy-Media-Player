# Use Legacy Media Player on Windows 11 x64

Extract the downloaded `LegacyMediaPlayer-win-x64` Actions artifact, then extract
the `LegacyMediaPlayer-v0.4.0-alpha-win-x64.zip` inside it to a folder you own and
open **LegacyMediaPlayer.exe**. Keep all files and subfolders
together. No separate .NET or VLC installation is needed. You may move the whole
folder and create a shortcut to the executable.

Open files replaces the queue and plays the first file. Add to queue appends
without interrupting the current track. Both pickers accept multiple files.
Select a row and use Play selected, Remove, Move up or Move down; double-clicking
a row also plays it. Selection is separate from the current track, shown by its
queue position above the list and in the status line. Previous and Next move one
track in playback order. Repeat Off stops at the final item; Repeat All wraps;
Repeat One replays the current item on natural completion. Explicit Previous/Next
and choosing a row still change tracks. Play replays an ended item. Stop cancels
automatic advancement without changing playback modes.

Removing the current item stops playback. Press Play to start the first remaining
item in normal order, or continue the remaining shuffled traversal. Removing
the final shuffled item after exhausting the traversal lets Play start a new cycle.
Removing another item and reordering leave playback alone. Failed files stay
in the queue with an error so you can remove them or choose another. Queue contents
last for the current app session. MP3, WAV and FLAC remain supported. Open/Add also list MP4, MKV, WebM and AVI.
The embedded video view follows the active media, preserves the source aspect
ratio and resizes with the window. Audio shows a placeholder. Pause keeps the
picture; Stop/end return to the placeholder. See [video verification](video-verification.md)
for the samples actually checked and the limits of automated tests.

You can pass one or more full file paths to the executable, drag files onto the
executable in Explorer, or choose it in Windows **Open with**. No file associations
or default apps are changed automatically.

## Build and download

Developers need the SDK pinned in global.json. From the repository root:

```powershell
./scripts/Package-Windows.ps1
```

The default output is `artifacts/LegacyMediaPlayer-v0.4.0-alpha-win-x64.zip`,
with a `.sha256` sidecar and a folder containing LegacyMediaPlayer.exe. The script
extracts a second copy and verifies its dependencies, plugin timestamps and every
file hash. Existing ZIP/verification output is never overwritten; choose another
OutputDirectory for a fresh build.
GitHub Actions uploads the ZIP and SHA-256 as **LegacyMediaPlayer-win-x64** on every
successful Windows build. Open the branch's latest Actions run and download that
artifact. Test results are uploaded separately.

This is a self-contained **folder** deployment. LibVLC needs its native DLLs and
codec/plugin directory at runtime. Keeping that tree on disk avoids introducing
single-file extraction and native lookup assumptions. Trimming is disabled for
the UI and native interop dependencies. See Microsoft's
[deployment documentation](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview).
Preserve all bundled third-party notices. This development build has no installer,
code signing, automatic updates, persistent playlists, or broad codec certification.

## Everyday controls

Space toggles Play/Pause. Ctrl+O opens files; Ctrl+Shift+A adds to the queue.
Ctrl+Shift+O also adds when available; graphics overlays may reserve that combination.
Ctrl+Right/Left moves Next/Previous. M toggles mute. With focus outside sliders
and the queue, Left/Right seeks five seconds and Up/Down changes volume by five.
Sliders and queue rows retain their own arrow-key navigation. F11 or Alt+Enter
toggles fullscreen video; Escape exits. Double-click the video picture to toggle.
Fullscreen hides the ordinary controls and queue; keyboard transport, seek, volume,
mute and Previous/Next remain available. Mouse movement restores the cursor; it
hides after three idle seconds while fullscreen is active. Audio-only media do not
enter fullscreen. Switching to audio, Stop/end or a terminal failure restores the
normal player. See [fullscreen/package checks](fullscreen-verification.md).

The Repeat button cycles Off → All → One → Off. Shuffle toggles random playback
without moving the visible queue. Previous revisits recent shuffled history
(up to 256 entries); Next follows forward history before taking another unplayed
item. Repeat All starts another shuffled cycle, avoiding an immediate repeat when
there are alternatives. Manual selection records that choice, discards forward
history and removes the chosen entry from the unplayed bag. Duplicate paths remain
separate queue entries. Appends enter the remaining traversal; removals prune it;
reordering leaves the active shuffled cycle alone. Turning shuffle off restores
manual order; turning it on starts a new traversal anchored at the current item.
Open/drop replacement resets history and starts the first supplied valid item;
Add/drop append preserves the current track. Empty queues stay idle; a single item
loops only with Repeat All/One. No new keyboard shortcuts are added.

Volume, mute, repeat and shuffle save on close in
%LOCALAPPDATA%/LegacyMediaPlayer/audio-preferences.json. No media paths or queue
contents or shuffle history are saved. Older audio-only files default to Repeat
Off and Shuffle Off. Missing/corrupt/unwritable settings fall back safely.

## Startup and native runtime

The window opens before loading codecs. First playback initializes LibVLC on a
worker and shows a short starting message; closing during that work stays
responsive. The backend is reused for the rest of the session. Missing native
files produce an in-window error; restore the full folder and relaunch.

The publish script generates libvlc/win-x64/plugins/plugins.dat for the exact
bundled DLLs. Keep this cache and the plugin tree with the deployment. Regenerate
after upgrading LibVLC or replacing/changing the plugin DLLs. For a repair:

    Start-Process ./LegacyMediaPlayer.exe -ArgumentList '--generate-plugin-cache' -Wait

This maintenance command opens no window and never runs during normal startup.
See [startup measurements and limits](startup-verification.md).

## Listening checks

Automated native tests use dummy audio output. Confirm audible MP3/WAV/FLAC on
your speakers/headphones, physical volume changes and mute, and paused seeking.
Check add/remove/reorder, Next/Previous, natural advance and the final stop with
your own music. Do not treat a green decoder test as proof of audible output.

Drag local MP3/WAV/FLAC/MP4/MKV/WebM/AVI files onto the picture/placeholder to replace
the queue and play, or onto the queue panel to append. Ctrl+drop on the picture also
appends. Order and duplicate entries are preserved; invalid/missing items are skipped
with a warning. No source file is moved or modified. Appending starts playback only
when there is no current item. See [drag/drop checks](drag-drop-verification.md).
