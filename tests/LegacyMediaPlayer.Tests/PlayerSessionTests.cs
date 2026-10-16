using LegacyMediaPlayer.Application;
using LegacyMediaPlayer.Core;
using Xunit;

namespace LegacyMediaPlayer.Tests;

public class PlayerSessionTests
{
    [Fact]
    public void Opening_for_playback_starts_audio_and_switches_source_with_preferences()
    {
        using var first = new TempMedia();
        using var second = new TempMedia();
        using var session = new PlayerSession(new MockPlaybackEngine(), action => action());
        session.SetVolume(28);
        session.SetMuted(true);
        session.OpenAndPlayFile(first.Path);
        session.Seek(TimeSpan.FromSeconds(20));
        session.OpenAndPlayFile(second.Path);
        Assert.Equal(second.Path, session.Snapshot.Media?.Path);
        Assert.Equal(PlaybackState.Playing, session.Snapshot.State);
        Assert.Equal(TimeSpan.Zero, session.Snapshot.Position);
        Assert.Equal(28, session.Snapshot.Volume);
        Assert.True(session.Snapshot.IsMuted);
    }

    [Fact]
    public void Failed_open_does_not_resume_a_paused_previous_file()
    {
        using var file = new TempMedia();
        using var session = new PlayerSession(new MockPlaybackEngine(), action => action());
        session.OpenAndPlayFile(file.Path);
        session.Pause();
        session.OpenAndPlayFile(file.Path + ".missing");
        Assert.Equal(PlaybackState.Paused, session.Snapshot.State);
        Assert.Equal(file.Path, session.Snapshot.Media?.Path);
        Assert.NotNull(session.ErrorMessage);
        session.OpenAndPlayFile(file.Path);
        Assert.Equal(PlaybackState.Playing, session.Snapshot.State);
        Assert.Null(session.ErrorMessage);
    }

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
