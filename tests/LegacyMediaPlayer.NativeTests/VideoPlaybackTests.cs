using System.Collections.Concurrent;
using LegacyMediaPlayer.Application;
using LegacyMediaPlayer.Core;
using LegacyMediaPlayer.Playback.LibVLC;
using Xunit;

namespace LegacyMediaPlayer.NativeTests;

[Collection("Native playback")]
public sealed class VideoPlaybackTests
{
    [Theory]
    [InlineData("h264.mp4")]
    [InlineData("hevc.mp4")]
    [InlineData("vp9.mp4")]
    [InlineData("av1.mp4")]
    [InlineData("vp8.webm")]
    [InlineData("h264.mkv")]
    public async Task Video_fixtures_create_output_and_finish(string file)
    {
        using var engine = Create();
        var outputs = new ConcurrentQueue<PlaybackSnapshot>();
        engine.Changed += (_, e) => { if (e.Snapshot.HasVideoOutput) outputs.Enqueue(e.Snapshot); };
        engine.Open(MediaSource.FromFile(Fixture(file)));
        Assert.False(engine.Snapshot.HasVideo);
        Assert.False(engine.Snapshot.HasVideoOutput);
        engine.Play();
        await WaitFor(engine, s => s.State == PlaybackState.Ended);
        Assert.NotEmpty(outputs);
        Assert.All(outputs, s => Assert.True(s.HasVideo));
        Assert.True(engine.Snapshot.HasVideo);
        Assert.False(engine.Snapshot.HasVideoOutput);
    }

    [Fact]
    public async Task Switching_video_audio_video_resets_output_and_preserves_transport_and_preferences()
    {
        using var wave = new WaveFile(8);
        using var engine = new DeferredPlaybackEngine(Create, new(true, true, true));
        await engine.InitializeAsync();
        engine.SetVolume(34); engine.SetMuted(true);
        engine.Open(MediaSource.FromFile(Fixture("tone-video.mp4"))); engine.Play();
        await WaitFor(engine, s => s.HasVideoOutput && s.CanSeek);
        engine.Pause(); await WaitFor(engine, s => s.State == PlaybackState.Paused);
        Assert.True(engine.Snapshot.HasVideoOutput);
        engine.Seek(TimeSpan.FromSeconds(3));
        Assert.Equal(TimeSpan.FromSeconds(3), engine.Snapshot.Position);
        engine.Play(); await WaitFor(engine, s => s.State == PlaybackState.Playing);
        engine.Open(MediaSource.FromFile(wave.Path));
        Assert.False(engine.Snapshot.HasVideo); Assert.False(engine.Snapshot.HasVideoOutput);
        engine.Play(); await WaitFor(engine, s => s.Position.TotalMilliseconds > 250);
        Assert.False(engine.Snapshot.HasVideo); Assert.False(engine.Snapshot.HasVideoOutput);
        foreach (var file in new[] { "tone-video.mp4", "tone-video.avi" })
        {
            engine.Open(MediaSource.FromFile(Fixture(file)));
            Assert.False(engine.Snapshot.HasVideoOutput);
            engine.Play(); await WaitFor(engine, s => s.HasVideoOutput && s.State == PlaybackState.Playing);
            Assert.Equal(34, engine.Snapshot.Volume); Assert.True(engine.Snapshot.IsMuted);
        }
        engine.Stop();
        Assert.False(engine.Snapshot.HasVideoOutput);
        engine.Play(); await WaitFor(engine, s => s.HasVideoOutput);
        var changes = 0;
        engine.Changed += (_, _) => Interlocked.Increment(ref changes);
        engine.Dispose();
        var atDispose = changes;
        await Task.Delay(150);
        Assert.Equal(atDispose, changes);
        Assert.Throws<ObjectDisposedException>(engine.Play);
    }

    [Fact]
    public async Task Mixed_queue_advances_once_and_stale_notifications_do_not_restore_video()
    {
        using var wave = new WaveFile();
        var pending = new ConcurrentQueue<Action>();
        using var engine = Create();
        using var session = new PlayerSession(engine, pending.Enqueue);
        session.OpenFiles([Fixture("h264.mp4"), wave.Path, Fixture("vp8.webm")]);
        var seen = new HashSet<int>();
        var deadline = DateTime.UtcNow.AddSeconds(25);
        while (DateTime.UtcNow < deadline)
        {
            while (pending.TryDequeue(out var action)) action();
            Assert.Null(session.ErrorMessage);
            if (session.Snapshot.State == PlaybackState.Playing && session.Snapshot.Position > TimeSpan.Zero)
            {
                seen.Add(session.CurrentIndex);
                if (session.CurrentIndex == 1) Assert.False(session.Snapshot.HasVideoOutput);
            }
            if (session.CurrentIndex == 2 && session.Snapshot.State == PlaybackState.Ended) break;
            await Task.Delay(20);
        }
        Assert.Equal(PlaybackState.Ended, session.Snapshot.State);
        Assert.Equal(new[] { 0, 1, 2 }, seen.Order().ToArray());
        session.Previous();
        while (pending.TryDequeue(out var action)) action();
        Assert.Equal(1, session.CurrentIndex);
        Assert.False(session.Snapshot.HasVideoOutput);
        session.Next();
        await WaitFor(engine, s => s.HasVideoOutput);
        session.Stop();
        await Task.Delay(100);
        while (pending.TryDequeue(out var action)) action();
        Assert.Equal(2, session.CurrentIndex);
        Assert.Equal(PlaybackState.Stopped, session.Snapshot.State);
        Assert.False(session.Snapshot.HasVideoOutput);
    }

    [Theory]
    [InlineData("mp4")]
    [InlineData("mkv")]
    [InlineData("webm")]
    [InlineData("avi")]
    public async Task Damaged_video_reports_error_and_recovers_with_video(string extension)
    {
        var path = Path.Combine(Path.GetTempPath(), $"legacy-damaged-{Guid.NewGuid():N}.{extension}");
        File.WriteAllText(path, "Not a valid video container.");
        try
        {
            using var engine = Create();
            engine.Open(MediaSource.FromFile(path));
            try { engine.Play(); } catch (PlaybackException) { }
            await WaitFor(engine, s => s.State == PlaybackState.Failed, allowError: true);
            Assert.NotNull(engine.Snapshot.Error);
            Assert.False(engine.Snapshot.HasVideoOutput);
            engine.Open(MediaSource.FromFile(Fixture("tone-video.mp4"))); engine.Play();
            await WaitFor(engine, s => s.HasVideoOutput);
            Assert.Null(engine.Snapshot.Error);
        }
        finally { File.Delete(path); }
    }


    [Fact]
    public async Task Video_output_failure_is_reported_even_when_audio_is_playable()
    {
        using var engine = new LibVlcPlaybackEngine(["--aout=dummy", "--vout=unavailable-output"], enableHardwareDecoding: false);
        engine.Open(MediaSource.FromFile(Fixture("tone-video.mp4"))); engine.Play();
        await WaitFor(engine, s => s.Error is not null, allowError: true);
        Assert.Equal("video-output-failed", engine.Snapshot.Error!.Code);
        Assert.False(engine.Snapshot.HasVideoOutput);
        engine.Stop();
        Assert.Equal(PlaybackState.Stopped, engine.Snapshot.State);
        engine.Play();
        await WaitFor(engine, s => s.Error?.Code == "video-output-failed", allowError: true);
        Assert.NotEqual(PlaybackState.Stopped, engine.Snapshot.State);
        Assert.Equal("video-output-failed", engine.Snapshot.Error!.Code);
        using var wave = new WaveFile();
        engine.Open(MediaSource.FromFile(wave.Path)); engine.Play();
        await WaitFor(engine, s => s.Position.TotalMilliseconds > 200);
        Assert.Null(engine.Snapshot.Error);
    }


    [Fact]
    public async Task Unsupported_video_codec_with_valid_audio_reports_error_and_recovers()
    {
        var bytes = File.ReadAllBytes(Fixture("tone-video.mp4"));
        var codec = bytes.AsSpan().IndexOf("avc1\0\0\0\0"u8);
        Assert.True(codec > 0, "The controlled fixture must contain an AVC sample entry.");
        "zzzz"u8.CopyTo(bytes.AsSpan(codec));
        var path = Path.Combine(Path.GetTempPath(), $"legacy-unsupported-{Guid.NewGuid():N}.mp4");
        File.WriteAllBytes(path, bytes);
        try
        {
            using var engine = Create();
            engine.Open(MediaSource.FromFile(path)); engine.Play();
            await WaitFor(engine, s => s.Error is not null, allowError: true);
            Assert.Equal("unsupported-track", engine.Snapshot.Error!.Code);
            Assert.False(engine.Snapshot.HasVideoOutput);
            await WaitFor(engine, s => s.State == PlaybackState.Failed, allowError: true);
            Assert.Equal("video-unavailable", engine.Snapshot.Error!.Code);
            engine.Open(MediaSource.FromFile(Fixture("tone-video.mp4"))); engine.Play();
            await WaitFor(engine, s => s.HasVideoOutput);
            Assert.Null(engine.Snapshot.Error);
        }
        finally { File.Delete(path); }
    }


    [Fact]
    public async Task Unsupported_audio_track_does_not_disable_valid_video()
    {
        var bytes = File.ReadAllBytes(Fixture("tone-video.mp4"));
        var codec = bytes.AsSpan().IndexOf("mp4a\0\0\0\0"u8);
        Assert.True(codec > 0, "The controlled fixture must contain an AAC sample entry.");
        "zzzz"u8.CopyTo(bytes.AsSpan(codec));
        var path = Path.Combine(Path.GetTempPath(), $"legacy-optional-track-{Guid.NewGuid():N}.mp4");
        File.WriteAllBytes(path, bytes);
        try
        {
            using var engine = Create();
            engine.Open(MediaSource.FromFile(path)); engine.Play();
            await WaitFor(engine, s => s.HasVideoOutput && s.Error?.Code == "unsupported-track", allowError: true);
            Assert.Equal(PlaybackState.Playing, engine.Snapshot.State);
            engine.Play(); // Repeated Play must not reset an already-playing warning to Ready.
            Assert.Equal(PlaybackState.Playing, engine.Snapshot.State);
            await WaitFor(engine, s => s.State == PlaybackState.Ended, allowError: true);
            Assert.Equal("unsupported-track", engine.Snapshot.Error!.Code);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Stop_replay_does_not_reuse_output_evidence_from_previous_attempt()
    {
        var path = Path.Combine(Path.GetTempPath(), $"legacy-replay-{Guid.NewGuid():N}.mp4");
        var bytes = File.ReadAllBytes(Fixture("tone-video.mp4"));
        File.WriteAllBytes(path, bytes);
        try
        {
            using var engine = Create();
            engine.Open(MediaSource.FromFile(path)); engine.Play();
            await WaitFor(engine, s => s.HasVideoOutput);
            engine.Stop();
            var codec = bytes.AsSpan().IndexOf("avc1\0\0\0\0"u8);
            Assert.True(codec > 0);
            "zzzz"u8.CopyTo(bytes.AsSpan(codec));
            File.WriteAllBytes(path, bytes);
            engine.Play();
            await WaitFor(engine, s => s.State is PlaybackState.Ended or PlaybackState.Failed, allowError: true);
            Assert.Equal(PlaybackState.Failed, engine.Snapshot.State);
            Assert.Equal("video-unavailable", engine.Snapshot.Error?.Code);
        }
        finally { File.Delete(path); }
    }

    private static LibVlcPlaybackEngine Create() => new(["--aout=dummy", "--vout=dummy"], enableHardwareDecoding: false);
    private static string Fixture(string file) => Path.Combine(AppContext.BaseDirectory, "Fixtures", file);
    private static async Task WaitFor(IPlaybackEngine engine, Func<PlaybackSnapshot, bool> predicate, bool allowError = false)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            var snapshot = engine.Snapshot;
            if (!allowError) Assert.Null(snapshot.Error);
            if (predicate(snapshot)) return;
            await Task.Delay(15);
        }
        Assert.Fail($"Timed out: {engine.Snapshot}");
    }
}
