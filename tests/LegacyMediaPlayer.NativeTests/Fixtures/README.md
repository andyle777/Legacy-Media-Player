# Generated audio fixtures

These two-second, mono 44.1 kHz files contain a generated 440 Hz sine wave.
They contain no recorded music or third-party audio. The generated waveform is
provided under CC0-1.0 for unrestricted testing.

MP3 (128 kb/s) and FLAC were encoded from 16-bit PCM WAV using the pinned
VideoLAN.LibVLC.Windows 3.0.24 runtime. They are intentional test assets.
The tests also generate WAV at runtime and check paths with Unicode and '#'.

Reproduction: generate 88,200 signed 16-bit samples with
round(3000 * sin(2 * pi * 440 * sampleIndex / 44100)), wrap in a mono PCM WAV,
and use LibVLC sout with:

    #transcode{acodec=mp3,ab=128,channels=1,samplerate=44100}:std{access=file,mux=raw,dst='test-tone.mp3'}
    #transcode{acodec=flac,ab=128,channels=1,samplerate=44100}:std{access=file,mux=raw,dst='test-tone.flac'}

These fixtures prove basic decoding, not format-wide compatibility.

LibVLC writes streaming FLAC with an unknown total sample count. Set the lower
36 bits of the STREAMINFO 64-bit value at byte offset 18 to 88,200 after
encoding, so this finite file advertises its known two-second duration.


## Video fixtures

These small, checked-in fixtures make CI independent of external downloads. Native tests use software decoding and dummy audio/video output; they do not visually verify the Windows HWND renderer.

| File | Source | Upstream Git blob |
| --- | --- | --- |
| h264.mp4 | web-platform-tests/wpt/webcodecs/h264.mp4 | `e0d6a6bedcf12dad5ec45a768f638bf1cb1ec1e7` |
| hevc.mp4 | web-platform-tests/wpt/webcodecs/h265.mp4 | `1c1adb07630704920efd0721ac85f474ab7cbd6a` |
| av1.mp4 | web-platform-tests/wpt/webcodecs/av1.mp4 | `8d2a7acdb860d76bbf9bf217de26d351c0db4f7e` |
| vp9.mp4 | web-platform-tests/wpt/webcodecs/vp9.mp4 | `7553e5cae9202c0f144126910e9471658038b455` |
| vp8.webm | web-platform-tests/wpt/webcodecs/vp8.webm | `14d970e30106ed9c6faa75d948788aa311ddfc7d` |
| h264.mkv | chromium/chromium/media/test/data/testsrc-no-durations-h264.mkv | `cd3163cb445aae66543aa0a415772403dbbfefe8` |

The WPT assets are one-second FFmpeg test patterns (320×240, 10 fps, YUV 4:2:0): H.264, HEVC, AV1, VP9 and VP8 respectively. Generation commands are in the [upstream README](https://github.com/web-platform-tests/wpt/blob/master/webcodecs/README.md); retain LICENSE-WPT.txt. The Chromium MKV is an H.264 test pattern deliberately lacking durations; retain LICENSE-Chromium.txt. It tests natural completion without duration/position events.

tone-video.mp4 and tone-video.avi are original six-second 160×90, 10 fps patterns with a white circle and a quiet 440 Hz tone. The MP4 uses H.264/AAC; the AVI uses MPEG-4 Part 2/PCM. They were generated with the installed VLC 3 encoder from a synthetic RGB24/PCM AVI, not acquired user media. The exact generator source is synthetic-generator.cs.txt. To regenerate, copy it to Program.cs in a throwaway net10.0 console project referencing LibVLCSharp 3.10.2 and pass an installed VLC folder containing its encoders as the first argument. Outputs go beside the generator executable. The application and CI use only the bundled VideoLAN.LibVLC.Windows runtime; they never require that encoder installation.

Codec names and these few samples are smoke-test coverage, not certification of every profile, bit depth, resolution, hardware decoder or damaged-file variation.

## Subtitle fixtures

`subtitles.mkv` repeats the existing Chromium H.264 pattern to six seconds and
adds original English/Vietnamese UTF-8 subtitle tracks. Its video remains covered
by LICENSE-Chromium.txt. `subtitles.mp4` adds two original timed-text tracks to
our CC0 synthetic tone-video.mp4, retaining its H.264/AAC bytes. Subtitle text is
original CC0 test material. Neither contains user media.

`subtitle-fixture-generator.mjs.txt` is the exact deterministic Node generator.
Copy it to `.mjs` alongside h264.mkv and tone-video.mp4 and run with Node.
Node is a fixture-generation tool only; application and CI consume the fixtures
without Node, an encoder installation, or network access. Native tests verify
track IDs, selection and transport, not visually readable text.
