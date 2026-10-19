using System.Diagnostics;
using LegacyMediaPlayer.Core;
using LegacyMediaPlayer.Playback.LibVLC;
using Xunit;

namespace LegacyMediaPlayer.NativeTests;

[Collection("Native playback")]
public sealed class PlaybackRateTests
{
    [Fact]
    public async Task Rate_selected_during_asynchronous_opening_reaches_the_new_input()
    {
        using var file = new WaveFile(12);
        using var engine = new LibVlcPlaybackEngine(["--aout=dummy", "--vout=dummy"]);
        engine.SetRate(0.5f); engine.Open(MediaSource.FromFile(file.Path)); engine.Play();
        // Play returns before native Playing/stream discovery on a fresh input.
        engine.SetRate(2);
        await WaitFor(engine, s => s.State == PlaybackState.Playing && s.Position.TotalMilliseconds > 200);
        Assert.Equal(2f, engine.Snapshot.Rate); Assert.Equal(2f, engine.Snapshot.AcceptedRate);
        var start = engine.Snapshot.Position; var watch = Stopwatch.StartNew(); await Task.Delay(1800);
        Assert.InRange((engine.Snapshot.Position - start).TotalMilliseconds / watch.Elapsed.TotalMilliseconds, 1.4, 2.7);
    }

    [Theory]
    [InlineData(0.5f)] [InlineData(0.75f)] [InlineData(1f)]
    [InlineData(1.25f)] [InlineData(1.5f)] [InlineData(2f)]
    public async Task Native_rate_survives_pause_seek_stop_and_media_replacement(float rate)
    {
        using var file = new WaveFile(12);
        using var engine = new LibVlcPlaybackEngine(["--aout=dummy", "--vout=dummy"], enableHardwareDecoding: false);
        engine.SetRate(rate); Assert.Null(engine.Snapshot.AcceptedRate);
        engine.Open(MediaSource.FromFile(file.Path)); engine.Play();
        await WaitFor(engine, s => s.CanSeek && s.Position.TotalMilliseconds > 200);
        Assert.Equal(rate, engine.Snapshot.AcceptedRate);
        engine.Pause(); await WaitFor(engine, s => s.State == PlaybackState.Paused);
        engine.SetRate(rate == 2 ? 0.5f : 2); // Live change while paused must not resume.
        Assert.Equal(PlaybackState.Paused, engine.Snapshot.State);
        engine.SetRate(rate); engine.Seek(TimeSpan.FromSeconds(2)); engine.Play();
        Assert.Equal(rate, engine.Snapshot.AcceptedRate);
        engine.Stop(); engine.Play(); await WaitFor(engine, s => s.State == PlaybackState.Playing);
        Assert.Equal(rate, engine.Snapshot.Rate); Assert.Equal(rate, engine.Snapshot.AcceptedRate);
        engine.Open(MediaSource.FromFile(Path.Combine(AppContext.BaseDirectory, "Fixtures", "tone-video.mp4")));
        engine.Play(); await WaitFor(engine, s => s.HasVideoOutput);
        Assert.Equal(rate, engine.Snapshot.AcceptedRate);
        engine.Dispose(); Assert.Throws<ObjectDisposedException>(() => engine.SetRate(1));
    }

    [Theory]
    [InlineData(0.5f, 0.25, 0.9)]
    [InlineData(2f, 1.4, 2.7)]
    public async Task Native_audio_clock_progresses_at_non_normal_rate(float rate, double minimum, double maximum)
    {
        using var file = new WaveFile(12);
        using var engine = new LibVlcPlaybackEngine(["--aout=dummy", "--vout=dummy"]);
        engine.SetRate(rate); engine.Open(MediaSource.FromFile(file.Path)); engine.Play();
        await WaitFor(engine, s => s.Position.TotalMilliseconds > 200);
        var start = engine.Snapshot.Position; var watch = Stopwatch.StartNew();
        await Task.Delay(1800);
        var ratio = (engine.Snapshot.Position - start).TotalMilliseconds / watch.Elapsed.TotalMilliseconds;
        Assert.InRange(ratio, minimum, maximum); // Detects a native rate request that silently stays at 1×.
    }

    [Theory]
    [InlineData("mp3", 0.5f)] [InlineData("mp3", 2f)]
    [InlineData("flac", 0.5f)] [InlineData("flac", 2f)]
    public async Task Encoded_audio_accepts_rates_and_finishes(string extension, float rate)
    {
        using var engine = new LibVlcPlaybackEngine(["--aout=dummy", "--vout=dummy"]);
        engine.SetRate(rate);
        engine.Open(MediaSource.FromFile(Path.Combine(AppContext.BaseDirectory, "Fixtures", "test-tone." + extension)));
        engine.Play(); await WaitFor(engine, s => s.State == PlaybackState.Ended);
        Assert.Equal(rate, engine.Snapshot.Rate); Assert.Null(engine.Snapshot.RateError);
    }

    [Fact]
    public async Task Damaged_video_does_not_erase_speed_or_prevent_good_audio_recovery()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid()+".mp4");
        try
        {
            File.WriteAllText(path, "Not a movie");
            using var engine = new LibVlcPlaybackEngine(["--aout=dummy", "--vout=dummy"]);
            engine.SetRate(1.5f); engine.Open(MediaSource.FromFile(path));
            try { engine.Play(); } catch (PlaybackException) { }
            await WaitFor(engine, s => s.State == PlaybackState.Failed, allowFailure: true);
            Assert.Equal(1.5f, engine.Snapshot.Rate);
            using var wav = new WaveFile(8); engine.Open(MediaSource.FromFile(wav.Path)); engine.Play();
            await WaitFor(engine, s => s.CanSeek); Assert.Equal(1.5f, engine.Snapshot.AcceptedRate);
        }
        finally { File.Delete(path); }
    }

    private static async Task WaitFor(IPlaybackEngine engine, Func<PlaybackSnapshot, bool> predicate, bool allowFailure = false)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < TimeSpan.FromSeconds(25))
        {
            var snapshot = engine.Snapshot;
            if (!allowFailure) { Assert.Null(snapshot.Error); Assert.Null(snapshot.RateError); }
            if (predicate(snapshot)) return;
            await Task.Delay(20);
        }
        Assert.Fail($"Playback did not reach expected state: {engine.Snapshot}");
    }
}
