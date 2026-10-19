using LegacyMediaPlayer.Application;
using LegacyMediaPlayer.Core;
using Xunit;

namespace LegacyMediaPlayer.Tests;

public sealed class RepeatShuffleTests
{
    [Theory]
    [InlineData(RepeatMode.Off, 1, PlaybackState.Ended)]
    [InlineData(RepeatMode.All, 0, PlaybackState.Playing)]
    [InlineData(RepeatMode.One, 1, PlaybackState.Playing)]
    public void Natural_end_applies_repeat_policy_once(RepeatMode mode, int index, PlaybackState state)
    {
        using var h = new Harness(2); var s = h.Session;
        s.SetRepeatMode(mode); s.PlayAt(1); h.Drain();
        h.Engine.End(); h.Engine.End(); h.Drain();
        Assert.Equal(index, s.CurrentIndex); Assert.Equal(state, s.Snapshot.State);
    }

    [Fact]
    public void Shuffle_visits_each_identity_once_without_reordering_queue_and_replays_history()
    {
        using var h = new Harness(5); var s = h.Session;
        var order = s.Queue.ToArray(); s.SetShuffleEnabled(true);
        var visited = new List<QueueItem> { s.CurrentItem! };
        while (s.CanNext) { s.Next(); visited.Add(s.CurrentItem!); }
        Assert.Equal(5, visited.Distinct().Count()); Assert.Equal(order, s.Queue);
        s.Previous(); Assert.Same(visited[3], s.CurrentItem);
        s.Previous(); Assert.Same(visited[2], s.CurrentItem);
        s.Next(); Assert.Same(visited[3], s.CurrentItem);
        s.Next(); Assert.Same(visited[4], s.CurrentItem); Assert.False(s.CanNext);
    }

    [Fact]
    public void Identical_seeds_produce_identical_traversals()
    {
        using var a = new Harness(8); using var b = new Harness(8);
        a.Session.SetShuffleEnabled(true); b.Session.SetShuffleEnabled(true);
        var first = new List<int>(); var second = new List<int>();
        while (a.Session.CanNext) { a.Session.Next(); first.Add(a.Session.CurrentIndex); }
        while (b.Session.CanNext) { b.Session.Next(); second.Add(b.Session.CurrentIndex); }
        Assert.Equal(first, second);
        Assert.NotEqual(Enumerable.Range(1, 7).ToArray(), first.ToArray());
    }

    [Fact]
    public void Shuffle_repeat_all_visits_every_entry_per_cycle_and_avoids_boundary_repeat()
    {
        using var h = new Harness(4); var s = h.Session;
        s.SetShuffleEnabled(true); s.SetRepeatMode(RepeatMode.All);
        var cycle = new HashSet<QueueItem> { s.CurrentItem! };
        for (var i = 0; i < 3; i++) { h.End(); Assert.True(cycle.Add(s.CurrentItem!)); }
        var last = s.CurrentItem; cycle.Clear();
        h.End(); Assert.NotSame(last, s.CurrentItem); cycle.Add(s.CurrentItem!);
        for (var i = 0; i < 3; i++) { h.End(); Assert.True(cycle.Add(s.CurrentItem!)); }
        Assert.Equal(4, cycle.Count);
    }

    [Fact]
    public void Repeat_one_does_not_consume_shuffle_bag_but_explicit_next_overrides_it()
    {
        using var h = new Harness(3); var s = h.Session;
        s.SetShuffleEnabled(true); s.SetRepeatMode(RepeatMode.One); var original = s.CurrentItem;
        h.End(); h.End(); Assert.Same(original, s.CurrentItem);
        s.Next(); Assert.NotSame(original, s.CurrentItem);
        s.SetRepeatMode(RepeatMode.Off); s.Next(); Assert.False(s.CanNext);
        h.End(); Assert.Equal(PlaybackState.Ended, s.Snapshot.State);
    }

    [Fact]
    public void Manual_choice_discards_forward_history_and_excludes_choice_from_remaining_bag()
    {
        using var h = new Harness(5); var s = h.Session; s.SetShuffleEnabled(true);
        s.Next(); var previous = s.CurrentItem; s.Next(); s.Previous();
        var chosen = s.Queue.First(x => !ReferenceEquals(x, previous) && !ReferenceEquals(x, s.Queue[0]));
        s.PlayAt(s.Queue.IndexOf(chosen)); Assert.Same(chosen, s.CurrentItem);
        s.Previous(); Assert.Same(previous, s.CurrentItem);
        s.Next(); Assert.Same(chosen, s.CurrentItem);
        while (s.CanNext) { s.Next(); Assert.NotSame(chosen, s.CurrentItem); }
    }

    [Fact]
    public void Appends_duplicates_and_reorders_preserve_current_identity_and_pending_traversal()
    {
        using var h = new Harness(3); var s = h.Session; s.SetShuffleEnabled(true);
        var current = s.CurrentItem; s.Pause(); var before = s.Snapshot;
        s.AddFiles([h.Files[0].Path]); s.MoveItem(0, 3);
        Assert.Same(current, s.CurrentItem); Assert.Equal(before, s.Snapshot);
        var visited = new HashSet<QueueItem> { s.CurrentItem! };
        while (s.CanNext) { s.Next(); Assert.True(visited.Add(s.CurrentItem!)); }
        Assert.Equal(4, visited.Count);
    }

    [Fact]
    public void Removing_current_history_and_upcoming_entries_never_uses_stale_references()
    {
        using var h = new Harness(6); var s = h.Session; s.SetShuffleEnabled(true);
        s.Next(); s.Next(); var removed = s.CurrentItem;
        s.RemoveAt(s.CurrentIndex); Assert.Null(s.CurrentItem); Assert.Equal(PlaybackState.Stopped, s.Snapshot.State);
        s.RemoveAt(0); s.RemoveAt(s.Queue.Count - 1); s.MoveItem(0, s.Queue.Count - 1);
        s.Play(); Assert.NotNull(s.CurrentItem); Assert.NotSame(removed, s.CurrentItem);
        while (s.CanNext) { s.Next(); Assert.Contains(s.CurrentItem!, s.Queue); }
        while (s.CanPrevious) { s.Previous(); Assert.Contains(s.CurrentItem!, s.Queue); }
    }

    [Fact]
    public void Play_after_removing_final_shuffled_item_can_restart_the_remaining_queue()
    {
        using var h = new Harness(3); var s = h.Session; s.SetShuffleEnabled(true);
        while (s.CanNext) s.Next();
        s.RemoveAt(s.CurrentIndex); s.Play();
        Assert.NotNull(s.CurrentItem); Assert.Contains(s.CurrentItem, s.Queue);
        Assert.Equal(PlaybackState.Playing, s.Snapshot.State);
    }

    [Fact]
    public void Replacement_resets_history_and_disabling_shuffle_restores_manual_order()
    {
        using var h = new Harness(4); var s = h.Session; s.SetShuffleEnabled(true); s.Next();
        s.OpenFiles([h.Files[3].Path, h.Files[1].Path]);
        Assert.Equal(h.Files[3].Path, s.Snapshot.Media!.Path); Assert.False(s.CanPrevious);
        s.Next(); Assert.Equal(h.Files[1].Path, s.Snapshot.Media!.Path);
        s.SetShuffleEnabled(false); s.Previous(); Assert.Equal(0, s.CurrentIndex);
    }

    [Theory]
    [InlineData(RepeatMode.Off)]
    [InlineData(RepeatMode.All)]
    [InlineData(RepeatMode.One)]
    public void Empty_and_single_item_queues_are_safe(RepeatMode mode)
    {
        using var h = new Harness(0); var s = h.Session;
        s.SetRepeatMode(mode); s.SetShuffleEnabled(true); s.Next(); s.Previous(); s.Stop();
        Assert.Empty(s.Queue); Assert.False(s.CanNext); Assert.False(s.CanPrevious);
        using var file = new TempMedia(); s.AddFiles([file.Path]); var item = s.CurrentItem;
        h.End(); Assert.Same(item, s.CurrentItem);
        Assert.Equal(mode == RepeatMode.Off ? PlaybackState.Ended : PlaybackState.Playing, s.Snapshot.State);
        s.RemoveAt(0); s.Play(); Assert.Null(s.CurrentItem); Assert.Empty(s.Queue);
    }

    [Fact]
    public void Stop_and_dispose_cancel_pending_natural_repeat_and_preserve_paused_traversal()
    {
        using var h = new Harness(3); var s = h.Session;
        s.SetRepeatMode(RepeatMode.All); s.SetShuffleEnabled(true); h.Drain();
        h.Engine.End(); s.Stop(); h.Drain();
        Assert.Equal(PlaybackState.Stopped, s.Snapshot.State); Assert.Equal(0, s.CurrentIndex);
        s.Play(); s.Pause(); var item = s.CurrentItem;
        s.Play(); Assert.Same(item, s.CurrentItem);
        h.Engine.End(); s.Dispose(); h.Drain();
        Assert.Throws<ObjectDisposedException>(s.Next);
    }

    private sealed class Harness : IDisposable
    {
        public TempMedia[] Files { get; }
        public EndEngine Engine { get; } = new();
        public PlayerSession Session { get; }
        private readonly Queue<Action> pending = new();
        public Harness(int count)
        {
            Files = Enumerable.Range(0, count).Select(_ => new TempMedia()).ToArray();
            Session = new(Engine, pending.Enqueue, new Random(73));
            Session.OpenFiles(Files.Select(x => x.Path)); Drain();
        }
        public void Drain() { while (pending.TryDequeue(out var action)) action(); }
        public void End() { Engine.End(); Drain(); }
        public void Dispose() { Session.Dispose(); foreach (var file in Files) file.Dispose(); }
    }

    private sealed class EndEngine : IPlaybackEngine
    {
        private readonly MockPlaybackEngine inner = new();
        private PlaybackSnapshot? ended;
        public PlaybackCapabilities Capabilities => inner.Capabilities;
        public PlaybackSnapshot Snapshot => ended ?? inner.Snapshot;
        public event EventHandler<PlaybackChangedEventArgs>? Changed;
        public EndEngine() => inner.Changed += (_, e) => { ended = null; Changed?.Invoke(this, e); };
        public void End() { ended = inner.Snapshot with { State = PlaybackState.Ended }; Changed?.Invoke(this, new(Snapshot)); }
        public void Open(MediaSource s) => inner.Open(s);
        public void Play() => inner.Play(); public void Pause() => inner.Pause(); public void Stop() => inner.Stop();
        public void Seek(TimeSpan p) => inner.Seek(p); public void SetVolume(int v) => inner.SetVolume(v);
        public void SetMuted(bool m) => inner.SetMuted(m);
        public IReadOnlyList<PlaybackTrack> GetTracks() => inner.GetTracks();
        public void SelectTrack(PlaybackTrackKind k, int id) => inner.SelectTrack(k, id);
        public void AttachVideoSurface(VideoSurface? s) => inner.AttachVideoSurface(s);
        public void Dispose() => inner.Dispose();
    }
}
