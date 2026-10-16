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
last for the current app session. MP3, WAV and FLAC are the validated formats.

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
code signing, automatic updates, persistent playlists, or broad video validation.

## Listening checks

Automated native tests use dummy audio output. Confirm audible MP3/WAV/FLAC on
your speakers/headphones, physical volume changes and mute, and paused seeking.
Check add/remove/reorder, Next/Previous, natural advance and the final stop with
your own music. Do not treat a green decoder test as proof of audible output.
