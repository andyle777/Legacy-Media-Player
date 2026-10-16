using LegacyMediaPlayer.Application;
using LegacyMediaPlayer.Core;
using Xunit;

namespace LegacyMediaPlayer.Tests;

public class PlayerSessionTests
{
    [Fact]
    public void Picker_failure_survives_playback_notifications_until_next_successful_action()
    {
        using var engine = new MockPlaybackEngine();
        using var session = new PlayerSession(engine, action => action());
        session.ReportError("Could not open the file picker.");
        engine.SetVolume(50);
        Assert.Equal("Could not open the file picker.", session.ErrorMessage);
        session.SetVolume(60);
        Assert.Null(session.ErrorMessage);
    }

    [Fact]
    public void Missing_file_reports_error_without_losing_playing_media()
    {
        using var file = new TempMedia();
        using var session = new PlayerSession(new MockPlaybackEngine(), action => action());
        session.OpenFile(file.Path);
        session.Play();
        session.OpenFile(System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString()));
        Assert.NotNull(session.ErrorMessage);
        Assert.Equal(file.Path, session.Snapshot.Media?.Path);
        Assert.Equal(PlaybackState.Playing, session.Snapshot.State);
        session.OpenFile(file.Path);
        Assert.Null(session.ErrorMessage);
    }

    [Fact]
    public void Invalid_transport_operation_is_a_recoverable_error()
    {
        using var session = new PlayerSession(new MockPlaybackEngine(), action => action());
        session.Play();
        Assert.NotNull(session.ErrorMessage);
        Assert.Equal(PlaybackState.Empty, session.Snapshot.State);
    }

    [Fact]
    public void Session_publishes_snapshot_on_supplied_dispatcher()
    {
        using var engine = new MockPlaybackEngine();
        var pending = new Queue<Action>();
        using var session = new PlayerSession(engine, pending.Enqueue);
        var changes = 0;
        session.PropertyChanged += (_, _) => changes++;
        engine.SetVolume(21);
        Assert.Equal(0, changes);
        Assert.Equal(75, session.Snapshot.Volume);
        Assert.NotEmpty(pending);
        while (pending.TryDequeue(out var update)) update();
        Assert.True(changes > 0);
        Assert.Equal(21, session.Snapshot.Volume);
    }

    [Fact]
    public void Shutdown_ignores_queued_notifications_and_disposes_engine()
    {
        var engine = new MockPlaybackEngine();
        var pending = new Queue<Action>();
        var session = new PlayerSession(engine, pending.Enqueue);
        var changes = 0;
        session.PropertyChanged += (_, _) => changes++;
        engine.SetVolume(21);
        session.Dispose();
        session.Dispose();
        while (pending.TryDequeue(out var update)) update();
        Assert.Equal(0, changes);
        Assert.Throws<ObjectDisposedException>(engine.Play);
        Assert.Throws<ObjectDisposedException>(session.Play);
    }
}
