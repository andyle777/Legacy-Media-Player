using LegacyMediaPlayer.Core;
using LegacyMediaPlayer.Playback.LibVLC;
using Xunit;

namespace LegacyMediaPlayer.NativeTests;

public sealed class NativePlaybackTests
{
    [Theory]
    [InlineData("mp3")]
    [InlineData("flac")]
    public async Task Supported_audio_fixture_decodes_and_finishes(string extension)
    {
        using var engine = new LibVlcPlaybackEngine(["--aout=dummy", "--vout=dummy"]);
        engine.Open(MediaSource.FromFile(Path.Combine(AppContext.BaseDirectory, "Fixtures", "test-tone." + extension)));
        engine.Play();
        await WaitFor(engine, s => s.Position.TotalMilliseconds > 200);
        Assert.InRange(engine.Snapshot.Duration.TotalSeconds, 1.5, 3);
        await WaitFor(engine, s => s.State == PlaybackState.Ended);
        Assert.Equal(engine.Snapshot.Duration, engine.Snapshot.Position);
    }

    [Fact]
    public async Task Audio_transport_pauses_seeks_restarts_and_releases_switched_files()
    {
        using var first = new WaveFile(8);
        using var second = new WaveFile(8);
        using var engine = new LibVlcPlaybackEngine(["--aout=dummy", "--vout=dummy"]);
        engine.Open(MediaSource.FromFile(first.Path));
        engine.SetVolume(31);
        engine.SetMuted(true);
        engine.Play();
        await WaitFor(engine, s => s.State == PlaybackState.Playing && s.CanSeek && s.Position.TotalMilliseconds > 200);
        engine.Pause();
        await WaitFor(engine, s => s.State == PlaybackState.Paused);
        var paused = engine.Snapshot.Position;
        await Task.Delay(350);
        Assert.InRange(Math.Abs((engine.Snapshot.Position - paused).TotalMilliseconds), 0, 100);
        engine.Seek(TimeSpan.FromSeconds(5));
        Assert.Equal(TimeSpan.FromSeconds(5), engine.Snapshot.Position);
        engine.Play();
        await WaitFor(engine, s => s.State == PlaybackState.Playing && s.Position.TotalSeconds >= 5);
        engine.Stop();
        Assert.Equal(TimeSpan.Zero, engine.Snapshot.Position);
        engine.Play();
        await WaitFor(engine, s => s.State == PlaybackState.Playing && s.Position.TotalMilliseconds > 200 && s.Position.TotalSeconds < 3);
        engine.Open(MediaSource.FromFile(second.Path));
        using (File.Open(first.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        Assert.Equal(TimeSpan.Zero, engine.Snapshot.Position);
        Assert.Equal(31, engine.Snapshot.Volume);
        Assert.True(engine.Snapshot.IsMuted);
        engine.SetMuted(false);
        engine.Play();
        await WaitFor(engine, s => s.CanSeek && s.State == PlaybackState.Playing);
        engine.Seek(TimeSpan.FromSeconds(7));
        await WaitFor(engine, s => s.State == PlaybackState.Ended);
        engine.Play();
        await WaitFor(engine, s => s.State == PlaybackState.Playing && s.Position.TotalMilliseconds > 200 && s.Position.TotalSeconds < 3);
        engine.Dispose();
        using (File.Open(second.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
    }

    private static async Task WaitFor(IPlaybackEngine engine, Func<PlaybackSnapshot, bool> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            var snapshot = engine.Snapshot;
            Assert.Null(snapshot.Error);
            if (predicate(snapshot)) return;
            await Task.Delay(25);
        }
        Assert.Fail($"Timed out: {engine.Snapshot}");
    }

    [Fact]
    public async Task Native_decoder_advances_finishes_and_reopens_generated_wave()
    {
        using var wave = new WaveFile();
        using var engine = new LibVlcPlaybackEngine(["--aout=dummy", "--vout=dummy"]);
        var progress = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.Changed += (_, e) =>
        {
            if (e.Snapshot.Error is { } error)
            {
                progress.TrySetException(new Exception(error.Message));
                ended.TrySetException(new Exception(error.Message));
            }
            if (e.Snapshot.Position > TimeSpan.FromMilliseconds(200)) progress.TrySetResult();
            if (e.Snapshot.State == PlaybackState.Ended) ended.TrySetResult();
        };
        engine.Open(MediaSource.FromFile(wave.Path));
        engine.SetVolume(35);
        engine.SetMuted(true);
        engine.Play();
        await progress.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.True(engine.Snapshot.Duration >= TimeSpan.FromSeconds(1));
        Assert.Equal(35, engine.Snapshot.Volume);
        Assert.True(engine.Snapshot.IsMuted);
        await ended.Task.WaitAsync(TimeSpan.FromSeconds(15));
        engine.Open(MediaSource.FromFile(wave.Path));
        Assert.Equal(PlaybackState.Ready, engine.Snapshot.State);
        Assert.Equal(TimeSpan.Zero, engine.Snapshot.Position);
        engine.Play();
        engine.Stop();
        Assert.Equal(PlaybackState.Stopped, engine.Snapshot.State);
    }

    [Theory]
    [InlineData("mp3")]
    [InlineData("wav")]
    [InlineData("flac")]
    public async Task Malformed_media_reports_failure_and_can_be_replaced(string extension)
    {
        using var wave = new WaveFile();
        var badPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"legacy-bad-{Guid.NewGuid():N}.{extension}");
        File.WriteAllText(badPath, "This is not a media file.");
        try
        {
            using var engine = new LibVlcPlaybackEngine(["--aout=dummy", "--vout=dummy"]);
            var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var observed = new System.Collections.Concurrent.ConcurrentQueue<string>();
            engine.Changed += (_, e) =>
            {
                observed.Enqueue($"{e.Snapshot.State}: position={e.Snapshot.Position}, duration={e.Snapshot.Duration}, error={e.Snapshot.Error?.Code}");
                if (e.Snapshot.Error is not null) failed.TrySetResult();
            };
            engine.Open(MediaSource.FromFile(badPath));
            try { engine.Play(); }
            catch (PlaybackException) { /* A synchronous rejection is also a reported failure. */ }
            try { await failed.Task.WaitAsync(TimeSpan.FromSeconds(15)); }
            catch (TimeoutException) { Assert.Fail(string.Join(Environment.NewLine, observed)); }
            Assert.Equal(PlaybackState.Failed, engine.Snapshot.State);
            engine.Open(MediaSource.FromFile(wave.Path));
            Assert.Null(engine.Snapshot.Error);
            Assert.Equal(PlaybackState.Ready, engine.Snapshot.State);
        }
        finally { File.Delete(badPath); }
    }

    [Fact]
    public void Native_adapter_rejects_invalid_commands_without_losing_source()
    {
        using var wave = new WaveFile();
        var engine = new LibVlcPlaybackEngine(["--aout=dummy", "--vout=dummy"]);
        try
        {
            Assert.Throws<InvalidOperationException>(engine.Play);
            engine.Open(MediaSource.FromFile(wave.Path));
            Assert.Throws<ArgumentOutOfRangeException>(() => engine.SetVolume(101));
            Assert.Throws<ArgumentOutOfRangeException>(() => engine.Seek(TimeSpan.FromSeconds(-1)));
            Assert.Throws<ArgumentException>(() => engine.AttachVideoSurface(new(VideoSurfaceKind.WindowsHwnd, 0)));
            Assert.Equal(wave.Path, engine.Snapshot.Media?.Path);
        }
        finally { engine.Dispose(); }
        engine.Dispose();
        Assert.Throws<ObjectDisposedException>(engine.Play);
    }
}

internal sealed class WaveFile : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"legacy 音 #{Guid.NewGuid():N}.wav");

    public WaveFile(int seconds = 2)
    {
        const int rate = 8000;
        var samples = rate * seconds;
        using var writer = new BinaryWriter(File.Create(Path));
        writer.Write("RIFF"u8);
        writer.Write(36 + samples * 2);
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(rate);
        writer.Write(rate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(samples * 2);
        for (var i = 0; i < samples; i++)
            writer.Write((short)(Math.Sin(i * 2 * Math.PI * 440 / rate) * 1000));
    }

    public void Dispose() => File.Delete(Path);
}
