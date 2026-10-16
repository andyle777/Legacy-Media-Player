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
