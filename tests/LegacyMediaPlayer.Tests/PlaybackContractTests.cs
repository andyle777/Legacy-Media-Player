using LegacyMediaPlayer.Application;
using LegacyMediaPlayer.Core;
using Xunit;

namespace LegacyMediaPlayer.Tests;

public class PlaybackContractTests
{
    [Fact]
    public void Local_file_with_unicode_spaces_and_hash_round_trips_as_file_uri()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"音 楽 #{Guid.NewGuid():N}.wav");
        File.WriteAllBytes(path, [0]);
        try { Assert.Equal(path, MediaSource.FromFile(path).Uri.LocalPath); }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("https://example.com/media.mp4")]
    [InlineData("relative.mp3")]
    [InlineData("")]
    public void Network_or_relative_sources_are_rejected(string path) =>
        Assert.Throws<ArgumentException>(() => MediaSource.FromFile(path));

    [Fact]
    public void Missing_file_is_rejected() =>
        Assert.Throws<FileNotFoundException>(() => MediaSource.FromFile(System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString())));

    [Fact]
    public void Transport_requires_media_and_stop_preserves_source()
    {
        using var engine = new MockPlaybackEngine();
        Assert.Throws<InvalidOperationException>(engine.Play);
        using var file = new TempMedia();
        engine.Open(MediaSource.FromFile(file.Path));
        Assert.Equal(PlaybackState.Ready, engine.Snapshot.State);
        engine.Play();
        Assert.Equal(PlaybackState.Playing, engine.Snapshot.State);
        engine.Pause();
        Assert.Equal(PlaybackState.Paused, engine.Snapshot.State);
        engine.Seek(TimeSpan.FromSeconds(8));
        engine.Stop();
        Assert.Equal(PlaybackState.Stopped, engine.Snapshot.State);
        Assert.Equal(TimeSpan.Zero, engine.Snapshot.Position);
        Assert.Equal(file.Path, engine.Snapshot.Media?.Path);
    }

    [Fact]
    public void Seek_clamps_at_duration_and_rejects_negative_time()
    {
        using var file = new TempMedia();
        using var engine = new MockPlaybackEngine();
        engine.Open(MediaSource.FromFile(file.Path));
        engine.Seek(TimeSpan.FromHours(2));
        Assert.Equal(TimeSpan.FromMinutes(3), engine.Snapshot.Position);
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.Seek(TimeSpan.FromSeconds(-1)));
        Assert.Equal(TimeSpan.FromMinutes(3), engine.Snapshot.Position);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Invalid_volume_does_not_change_current_setting(int volume)
    {
        using var engine = new MockPlaybackEngine();
        engine.SetVolume(43);
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.SetVolume(volume));
        Assert.Equal(43, engine.Snapshot.Volume);
    }

    [Fact]
    public void Mute_preserves_volume_when_reversed()
    {
        using var engine = new MockPlaybackEngine();
        engine.SetVolume(0);
        engine.SetMuted(true);
        Assert.True(engine.Snapshot.IsMuted);
        engine.SetMuted(false);
        Assert.False(engine.Snapshot.IsMuted);
        Assert.Equal(0, engine.Snapshot.Volume);
    }

    [Fact]
    public void Opening_another_file_resets_position_but_preserves_audio_preferences()
    {
        using var first = new TempMedia();
        using var second = new TempMedia();
        using var engine = new MockPlaybackEngine();
        engine.Open(MediaSource.FromFile(first.Path));
        engine.Play();
        engine.Seek(TimeSpan.FromSeconds(10));
        engine.SetVolume(25);
        engine.SetMuted(true);
        engine.Open(MediaSource.FromFile(second.Path));
        Assert.Equal(PlaybackState.Ready, engine.Snapshot.State);
        Assert.Equal(second.Path, engine.Snapshot.Media?.Path);
        Assert.Equal(TimeSpan.Zero, engine.Snapshot.Position);
        Assert.Equal(25, engine.Snapshot.Volume);
        Assert.True(engine.Snapshot.IsMuted);
    }

    [Fact]
    public void Deleted_source_does_not_replace_loaded_media()
    {
        using var first = new TempMedia();
        using var second = new TempMedia();
        var missing = MediaSource.FromFile(second.Path);
        using var engine = new MockPlaybackEngine();
        engine.Open(MediaSource.FromFile(first.Path));
        File.Delete(second.Path);
        Assert.Throws<FileNotFoundException>(() => engine.Open(missing));
        Assert.Equal(first.Path, engine.Snapshot.Media?.Path);
    }

    [Fact]
    public void Mock_reports_unsupported_video_and_tracks_explicitly()
    {
        using var engine = new MockPlaybackEngine();
        Assert.False(engine.Capabilities.Video);
        Assert.Empty(engine.GetTracks());
        Assert.Throws<NotSupportedException>(() => engine.SelectTrack(PlaybackTrackKind.Audio, 0));
        Assert.Throws<NotSupportedException>(() => engine.AttachVideoSurface(new(VideoSurfaceKind.WindowsHwnd, 1)));
        engine.AttachVideoSurface(null);
    }

    [Fact]
    public void Disposed_engine_rejects_commands_and_dispose_is_idempotent()
    {
        var engine = new MockPlaybackEngine();
        engine.Dispose();
        engine.Dispose();
        Assert.Throws<ObjectDisposedException>(engine.Play);
        Assert.Throws<ObjectDisposedException>(() => engine.SetVolume(10));
    }
}
