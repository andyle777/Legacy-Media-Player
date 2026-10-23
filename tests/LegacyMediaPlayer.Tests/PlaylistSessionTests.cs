using LegacyMediaPlayer.Application;
using LegacyMediaPlayer.Core;
using Xunit;

namespace LegacyMediaPlayer.Tests;

public sealed class PlaylistSessionTests
{
    [Fact]
    public async Task Prepared_commit_does_not_probe_files_and_play_revalidates_a_disappeared_file()
    {
        using var f = new PlaylistFiles(); var path = f.Media("Việt.mp3");
        var sources = await PlayerSession.PrepareQueueAsync([path, path]); File.Delete(path);
        using var engine = new MockPlaybackEngine(); using var session = new PlayerSession(engine, a => a());
        Assert.True(session.ReplacePreparedQueue(sources)); Assert.Equal(2, session.Queue.Count);
        Assert.NotSame(session.Queue[0], session.Queue[1]); Assert.Null(session.CurrentItem);
        session.Play(); Assert.Contains("not found", session.ErrorMessage);
        session.Dispose(); Assert.Throws<ObjectDisposedException>(() => session.ReplacePreparedQueue(sources));
    }

    [Fact]
    public async Task Failed_or_cancelled_preparation_does_not_mutate_an_existing_session()
    {
        using var f = new PlaylistFiles(); var path = f.Media("one.mp3");
        using var engine = new MockPlaybackEngine(); using var session = new PlayerSession(engine, a => a()); session.OpenFiles([path]);
        var current = session.CurrentItem;
        await Assert.ThrowsAsync<FileNotFoundException>(() => PlayerSession.PrepareQueueAsync([path, Path.Combine(f.Root, "missing.flac")]));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PlayerSession.PrepareQueueAsync([path], cancelled.Token));
        Assert.Same(current, session.CurrentItem); Assert.Single(session.Queue); Assert.Equal(PlaybackState.Playing, session.Snapshot.State);
    }

    [Fact]
    public void Restored_queue_is_idle_with_distinct_duplicate_identity_and_no_native_start()
    {
        using var f = new PlaylistFiles(); var one = f.Media("one.mp3"); var two = f.Media("two.flac");
        var creates = 0; using var engine = new DeferredPlaybackEngine(() => { creates++; return new MockPlaybackEngine(); }, new(false, false, false));
        using var session = new PlayerSession(engine, a => a());
        session.SetRate(1.5f); session.SetRepeatMode(RepeatMode.One); session.SetShuffleEnabled(true);
        session.ReplaceQueue([one, two, one]);
        Assert.Equal(0, creates); Assert.Null(session.CurrentItem); Assert.Equal(-1, session.CurrentIndex);
        Assert.All(session.Queue, item => Assert.False(item.IsCurrent)); Assert.NotSame(session.Queue[0], session.Queue[2]);
        Assert.Equal(PlaybackState.Empty, engine.Snapshot.State); Assert.Null(engine.Snapshot.Media);
        Assert.Equal(1.5f, session.Snapshot.Rate); Assert.Equal(RepeatMode.One, session.Repeat); Assert.True(session.ShuffleEnabled);
        session.Play(); Assert.Equal(1, creates); Assert.NotNull(session.CurrentItem); Assert.Equal(1.5f, session.Snapshot.AcceptedRate);
    }

    [Fact]
    public void Replacing_a_playing_queue_stops_and_clears_current_then_plays_in_manual_order()
    {
        using var f = new PlaylistFiles(); var one = f.Media("one.mp3"); var two = f.Media("two.flac");
        using var engine = new MockPlaybackEngine(); using var session = new PlayerSession(engine, a => a());
        session.OpenFiles([one]); var old = session.CurrentItem;
        session.ReplaceQueue([two, one]);
        Assert.Equal(PlaybackState.Stopped, session.Snapshot.State); Assert.Null(session.CurrentItem); Assert.False(old!.IsCurrent);
        session.MoveItem(0, 1); session.Play(); Assert.Equal(one, session.CurrentItem!.Source.Path);
        session.Next(); Assert.Equal(two, session.CurrentItem!.Source.Path);
        session.ReplaceQueue([]); Assert.Empty(session.Queue); Assert.Null(session.CurrentItem);
    }

    [Fact]
    public void Failed_validation_leaves_current_queue_playing_and_disposed_session_rejects_restore()
    {
        using var f = new PlaylistFiles(); var one = f.Media("one.mp3");
        using var engine = new MockPlaybackEngine(); using var session = new PlayerSession(engine, a => a()); session.OpenFiles([one]);
        var item = session.CurrentItem; session.ReplaceQueue([Path.Combine(f.Root, "missing.mp3")]);
        Assert.Same(item, session.CurrentItem); Assert.Equal(PlaybackState.Playing, session.Snapshot.State); Assert.Single(session.Queue);
        session.Dispose(); Assert.Throws<ObjectDisposedException>(() => session.ReplaceQueue([one]));
    }
}
