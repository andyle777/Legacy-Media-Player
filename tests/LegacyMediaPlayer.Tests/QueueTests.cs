using LegacyMediaPlayer.Application;
using LegacyMediaPlayer.Core;
using Xunit;

namespace LegacyMediaPlayer.Tests;

public class QueueTests
{
    [Fact]
    public void Add_preserves_playback_and_preferences_and_navigation_does_not_wrap()
    {
        using var a = new TempMedia();
        using var b = new TempMedia();
        using var s = new PlayerSession(new MockPlaybackEngine(), a => a());
        s.AddFiles([a.Path]);
        s.SetVolume(24); s.SetMuted(true);
        s.AddFiles([b.Path]);
        Assert.Equal(a.Path, s.Snapshot.Media!.Path);
        Assert.False(s.CanPrevious); Assert.True(s.CanNext);
        s.Previous();
        Assert.Equal(0, s.CurrentIndex);
        s.Next();
        Assert.Equal(b.Path, s.Snapshot.Media!.Path);
        Assert.Equal(24, s.Snapshot.Volume); Assert.True(s.Snapshot.IsMuted);
        s.Next(); Assert.Equal(1, s.CurrentIndex);
        s.Previous(); Assert.Equal(0, s.CurrentIndex);
    }

    [Fact]
    public void Reordering_duplicates_preserves_current_identity_and_changes_next_order()
    {
        using var a = new TempMedia(); using var b = new TempMedia();
        using var s = new PlayerSession(new MockPlaybackEngine(), a => a());
        s.AddFiles([a.Path, b.Path, a.Path]);
        var playing = s.CurrentItem;
        s.MoveItem(0, 2);
        Assert.Same(playing, s.CurrentItem); Assert.Equal(2, s.CurrentIndex);
        Assert.False(s.CanNext);
        s.Previous(); Assert.Equal(a.Path, s.Snapshot.Media!.Path);
        Assert.NotSame(playing, s.CurrentItem);
        s.RemoveAt(0); Assert.Equal(0, s.CurrentIndex);
        s.RemoveAt(0);
        Assert.Null(s.CurrentItem); Assert.Equal(PlaybackState.Stopped, s.Snapshot.State);
        s.Play(); Assert.Same(playing, s.CurrentItem);
        s.RemoveAt(0); s.Play();
        Assert.Empty(s.Queue); Assert.Equal(PlaybackState.Stopped, s.Snapshot.State);
    }

    [Fact]
    public void Natural_end_advances_once_and_final_item_stays_ended()
    {
        using var a = new TempMedia(); using var b = new TempMedia();
        var engine = new CompletionEngine(); var pending = new Queue<Action>();
        using var s = new PlayerSession(engine, pending.Enqueue);
        s.AddFiles([a.Path, b.Path]); Drain(pending);
        engine.End(); engine.End(); Drain(pending);
        Assert.Equal(1, s.CurrentIndex); Assert.Equal(PlaybackState.Playing, s.Snapshot.State);
        engine.End(); Drain(pending);
        Assert.Equal(1, s.CurrentIndex); Assert.Equal(PlaybackState.Ended, s.Snapshot.State);
        engine.SetVolume(50); Drain(pending);
        Assert.Equal(PlaybackState.Ended, s.Snapshot.State);
        s.Play(); Assert.Equal(PlaybackState.Playing, s.Snapshot.State);
    }

    [Fact]
    public void Stop_switch_remove_and_dispose_cancel_pending_completion()
    {
        using var a = new TempMedia(); using var b = new TempMedia(); using var c = new TempMedia();
        var engine = new CompletionEngine(); var pending = new Queue<Action>();
        var s = new PlayerSession(engine, pending.Enqueue);
        s.AddFiles([a.Path, b.Path, c.Path]); Drain(pending);
        engine.End(); s.Stop(); Drain(pending);
        Assert.Equal(0, s.CurrentIndex); Assert.Equal(PlaybackState.Stopped, s.Snapshot.State);
        s.Play(); engine.End(); s.PlayAt(1); Drain(pending);
        Assert.Equal(1, s.CurrentIndex);
        engine.End(); s.RemoveAt(1); Drain(pending);
        Assert.Null(s.CurrentItem); Assert.Equal(PlaybackState.Stopped, s.Snapshot.State);
        s.Play(); engine.End(); s.Dispose(); Drain(pending);
    }

    [Fact]
    public void Missing_queued_file_stops_old_audio_and_reports_error_without_skipping()
    {
        using var a = new TempMedia(); using var b = new TempMedia();
        using var s = new PlayerSession(new MockPlaybackEngine(), a => a());
        s.AddFiles([a.Path, b.Path]); File.Delete(b.Path);
        s.Next();
        Assert.Equal(1, s.CurrentIndex); Assert.NotNull(s.ErrorMessage);
        Assert.NotEqual(PlaybackState.Playing, s.Snapshot.State);
        s.Previous(); Assert.Null(s.ErrorMessage); Assert.Equal(PlaybackState.Playing, s.Snapshot.State);
        s.AddFiles([a.Path + ".missing"]); Assert.Equal(2, s.Queue.Count);
    }

    private static void Drain(Queue<Action> pending) { while (pending.TryDequeue(out var a)) a(); }

    [Fact]
    public void Retrying_a_missing_duplicate_does_not_resume_the_old_loaded_copy()
    {
        using var a = new TempMedia();
        using var s = new PlayerSession(new MockPlaybackEngine(), a => a());
        s.AddFiles([a.Path, a.Path]); File.Delete(a.Path);
        s.Next(); Assert.NotNull(s.ErrorMessage);
        s.Play();
        Assert.NotNull(s.ErrorMessage);
        Assert.Equal(PlaybackState.Stopped, s.Snapshot.State);
    }

    // External completion cannot be produced by the deterministic development engine.
    private sealed class CompletionEngine : IPlaybackEngine
    {
        private readonly MockPlaybackEngine inner = new();
        private PlaybackSnapshot? ended;
        public PlaybackCapabilities Capabilities => inner.Capabilities;
        public PlaybackSnapshot Snapshot => ended ?? inner.Snapshot;
        public event EventHandler<PlaybackChangedEventArgs>? Changed;
        public CompletionEngine() => inner.Changed += (_, e) => { ended = null; Changed?.Invoke(this, e); };
        public void End() { ended = inner.Snapshot with { State = PlaybackState.Ended }; Changed?.Invoke(this, new(Snapshot)); }
        public void Open(MediaSource source) => inner.Open(source);
        public void Play() => inner.Play();
        public void Pause() => inner.Pause();
        public void Stop() => inner.Stop();
        public void Seek(TimeSpan position) => inner.Seek(position);
        public void SetVolume(int volume) { if (ended is { } s) { ended = s with { Volume = volume }; Changed?.Invoke(this, new(Snapshot)); } else inner.SetVolume(volume); }
        public void SetMuted(bool muted) => inner.SetMuted(muted);
        public void SetRate(float rate) => inner.SetRate(rate);
        public IReadOnlyList<PlaybackTrack> GetTracks() => inner.GetTracks();
        public void SelectTrack(PlaybackTrackKind kind, int id) => inner.SelectTrack(kind, id);
        public void AttachVideoSurface(VideoSurface? surface) => inner.AttachVideoSurface(surface);
        public void Dispose() => inner.Dispose();
    }
}
