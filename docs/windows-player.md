# Use Legacy Media Player on Windows 11 x64

Extract the entire downloaded `LegacyMediaPlayer-win-x64` artifact to a folder
you own, then open **LegacyMediaPlayer.exe**. Keep all files and subfolders
together. No separate .NET or VLC installation is needed. You may move the whole
folder and create a shortcut to the executable.

Open files replaces the queue and plays the first file. Add to queue appends
without interrupting the current track. Both pickers accept multiple files.
Select a row and use Play selected, Remove, Move up or Move down; double-clicking
a row also plays it. Selection is separate from the current track, shown by its
queue position above the list and in the status line. Previous and Next move one
track and never wrap. At natural completion playback advances once; the final
track stays ended. Play replays it. Stop cancels automatic advancement.

Removing the current item stops playback. Press Play to start the first remaining
item. Removing another item and reordering leave playback alone. Failed files stay
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
./scripts/Publish-Windows.ps1
```

The default output is `artifacts/LegacyMediaPlayer-win-x64/LegacyMediaPlayer.exe`.
GitHub Actions uploads the folder as **LegacyMediaPlayer-win-x64** on every
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
Sliders and queue rows retain their own arrow-key navigation. F11/Alt+Enter are
reserved for future video fullscreen and currently do nothing.

Volume and mute save on close in
%LOCALAPPDATA%/LegacyMediaPlayer/audio-preferences.json. No media paths or queue
contents are saved. Missing/corrupt/unwritable settings fall back safely.

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
