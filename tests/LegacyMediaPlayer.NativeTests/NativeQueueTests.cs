using System.Collections.Concurrent;
using LegacyMediaPlayer.Application;
using LegacyMediaPlayer.Core;
using LegacyMediaPlayer.Playback.LibVLC;
using Xunit;

namespace LegacyMediaPlayer.NativeTests;

[Collection("Native playback")]
public sealed class NativeQueueTests
{
    [Fact]
    public async Task Native_queue_advances_mp3_wave_flac_and_stops_at_the_end()
    {
        using var wav = new WaveFile();
        var pending = new ConcurrentQueue<Action>();
        using var engine = new DeferredPlaybackEngine(() => new LibVlcPlaybackEngine(["--aout=dummy", "--vout=dummy"]), new(true, true, true));
        await engine.InitializeAsync();
        using var session = new PlayerSession(engine, pending.Enqueue);
        session.AddFiles([Fixture("mp3"), wav.Path, Fixture("flac")]);
        var visited = new HashSet<int>();
        await WaitFor(session, pending, () =>
        {
            if (session.Snapshot.State == PlaybackState.Playing && session.Snapshot.Position > TimeSpan.FromMilliseconds(200))
                visited.Add(session.CurrentIndex);
            return session.CurrentIndex == 2 && session.Snapshot.State == PlaybackState.Ended;
        });
        Assert.Equal(new[] { 0, 1, 2 }, visited.Order().ToArray());
        Assert.False(session.CanNext);
        session.SetVolume(32); session.SetMuted(true);
        session.Previous();
        await WaitFor(session, pending, () => session.Snapshot.State == PlaybackState.Playing && session.Snapshot.CanSeek);
        Assert.Equal(wav.Path, session.Snapshot.Media!.Path);
        Assert.Equal(32, session.Snapshot.Volume); Assert.True(session.Snapshot.IsMuted);
        session.Pause();
        await WaitFor(session, pending, () => session.Snapshot.State == PlaybackState.Paused);
        session.Seek(TimeSpan.FromSeconds(1));
        Assert.Equal(TimeSpan.FromSeconds(1), session.Snapshot.Position);
        session.Next();
        await WaitFor(session, pending, () => session.Snapshot.State == PlaybackState.Playing && session.Snapshot.CanSeek);
        Assert.Equal(Fixture("flac"), session.Snapshot.Media!.Path);
        session.Stop();
        await Task.Delay(150);
        Drain(pending);
        Assert.Equal(PlaybackState.Stopped, session.Snapshot.State);
        Assert.Equal(2, session.CurrentIndex);
    }

    private static string Fixture(string extension) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "test-tone." + extension);
    private static void Drain(ConcurrentQueue<Action> pending) { while (pending.TryDequeue(out var a)) a(); }
    private static async Task WaitFor(PlayerSession s, ConcurrentQueue<Action> pending, Func<bool> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(25);
        while (DateTime.UtcNow < deadline)
        {
            Drain(pending);
            Assert.Null(s.ErrorMessage);
            if (predicate()) return;
            await Task.Delay(20);
        }
        Assert.Fail($"Queue {s.CurrentIndex}: {s.Snapshot}");
    }
}
