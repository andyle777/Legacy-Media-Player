using LegacyMediaPlayer.Application;
using LegacyMediaPlayer.Core;
using Xunit;

namespace LegacyMediaPlayer.Tests;

public sealed class ModalPlaybackTests
{
    [Theory]
    [InlineData(RepeatMode.One, false, 1, 0)]
    [InlineData(RepeatMode.All, false, 1, 0)]
    [InlineData(RepeatMode.All, true, 1, 0)]
    [InlineData(RepeatMode.Off, false, 2, 1)]
    public void Natural_end_waits_for_picker_then_repeats_or_advances(RepeatMode repeat, bool shuffle, int count, int next)
    {
        using var file = new TempMedia(); using var engine = new EndableEngine();
        using var session = new PlayerSession(engine, action => action());
        session.OpenFiles(Enumerable.Repeat(file.Path, count));
        session.SetRepeatMode(repeat); session.SetShuffleEnabled(shuffle);
        var expected = session.Queue[next];
        var stops = engine.Stops;
        var modal = session.DeferAutomaticAdvance();
        engine.End(); engine.End(); // Duplicate callbacks still produce one transition.
        Assert.Equal(stops, engine.Stops);
        Assert.Equal(PlaybackState.Ended, session.Snapshot.State);
        modal.Dispose(); modal.Dispose();
        Assert.Equal(stops + 1, engine.Stops);
        Assert.Same(expected, session.CurrentItem);
        Assert.Equal(PlaybackState.Playing, session.Snapshot.State);
    }

    [Fact]
    public void Nested_pickers_wait_until_last_scope_exits_and_repeat_off_stays_ended()
    {
        using var file = new TempMedia(); using var engine = new EndableEngine();
        using var session = new PlayerSession(engine, action => action());
        session.OpenFiles([file.Path]);
        var stops = engine.Stops;
        using var outer = session.DeferAutomaticAdvance();
        var inner = session.DeferAutomaticAdvance();
        engine.End(); inner.Dispose();
        Assert.Equal(stops, engine.Stops);
        outer.Dispose();
        Assert.Equal(stops, engine.Stops);
        Assert.Equal(PlaybackState.Ended, session.Snapshot.State);
    }

    [Theory]
    [InlineData("stop")]
    [InlineData("replace")]
    [InlineData("remove")]
    [InlineData("dispose")]
    public void Invalidated_ending_cannot_restart_after_picker_exits(string change)
    {
        using var file = new TempMedia(); using var engine = new EndableEngine();
        using var session = new PlayerSession(engine, action => action());
        session.OpenFiles([file.Path]); session.SetRepeatMode(RepeatMode.One);
        var modal = session.DeferAutomaticAdvance();
        engine.End();
        if (change == "stop") session.Stop();
        if (change == "replace") session.ReplaceQueue([file.Path]);
        if (change == "remove") session.RemoveAt(0);
        if (change == "dispose") session.Dispose();
        var stops = engine.Stops;
        modal.Dispose();
        Assert.Equal(stops, engine.Stops);
    }

    private sealed class EndableEngine : IPlaybackEngine
    {
        private readonly MockPlaybackEngine _inner = new();
        private bool _ended;
        public int Stops { get; private set; }
        public EndableEngine() => _inner.Changed += (_, _) => Changed?.Invoke(this, new(Snapshot));
        public PlaybackCapabilities Capabilities => _inner.Capabilities;
        public PlaybackSnapshot Snapshot => _inner.Snapshot with { State = _ended ? PlaybackState.Ended : _inner.Snapshot.State };
        public event EventHandler<PlaybackChangedEventArgs>? Changed;
        public void End() { _ended = true; Changed?.Invoke(this, new(Snapshot)); }
        public void Open(MediaSource source) { _ended = false; _inner.Open(source); }
        public void Play() => _inner.Play(); public void Pause() => _inner.Pause();
        public void Stop() { Stops++; _ended = false; _inner.Stop(); }
        public void Seek(TimeSpan position) => _inner.Seek(position);
        public void SetVolume(int volume) => _inner.SetVolume(volume);
        public void SetMuted(bool muted) => _inner.SetMuted(muted);
        public void SetRate(float rate) => _inner.SetRate(rate);
        public IReadOnlyList<PlaybackTrack> GetTracks() => [];
        public void SelectTrack(PlaybackTrackKind kind, int id) => throw new NotSupportedException();
        public void AttachVideoSurface(VideoSurface? surface) { }
        public void Dispose() => _inner.Dispose();
    }
}
