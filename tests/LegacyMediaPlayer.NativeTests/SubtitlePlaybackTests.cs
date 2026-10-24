using LegacyMediaPlayer.Core;
using LegacyMediaPlayer.Playback.LibVLC;
using Xunit;

namespace LegacyMediaPlayer.NativeTests;

[Collection("Native playback")]
public sealed class SubtitlePlaybackTests
{
    [Theory]
    [InlineData("subtitles.mkv")]
    [InlineData("subtitles.mp4")]
    public async Task Embedded_tracks_use_native_ids_and_restore_semantic_choice_after_reopen(string fixture)
    {
        using var engine = Create();
        var source = MediaSource.FromFile(Fixture(fixture));
        engine.Open(source); engine.Play();
        await Until(engine, s => s.SubtitleTracks.Count == 2);
        Assert.Contains(engine.Snapshot.SubtitleTracks, t => t.Name.Contains("English"));
        var vietnamese = Assert.Single(engine.Snapshot.SubtitleTracks, t => t.Name.Contains("Vietnamese"));
        engine.SelectTrack(PlaybackTrackKind.Subtitle, vietnamese.Id);
        await Until(engine, s => s.SelectedSubtitleId == vietnamese.Id && !s.SubtitlePending);
        engine.SetSubtitles(SubtitleSelection.Off);
        await Until(engine, s => s.SelectedSubtitleId == -1 && !s.SubtitlePending);
        engine.Open(source);
        engine.SetSubtitles(SubtitleSelection.Track(vietnamese.Id, vietnamese.Name).ForReplay()); engine.Play();
        await Until(engine, s => s.SelectedSubtitleId >= 0 && !s.SubtitlePending);
        Assert.Equal(vietnamese.Name, engine.Snapshot.SubtitleTracks.Single(t => t.Id == engine.Snapshot.SelectedSubtitleId).Name);
        engine.Pause(); engine.RefreshSubtitles(); engine.SetRate(1.5f); engine.Seek(TimeSpan.FromSeconds(2)); engine.Play();
        await Until(engine, s => s.Position.TotalSeconds > 2 && s.SelectedSubtitleId >= 0);
    }

    [Fact]
    public async Task Live_unicode_srt_selects_native_track_off_and_replay_without_reopening_video()
    {
        var path = Path.Combine(Path.GetTempPath(), $"Việt #{Guid.NewGuid():N}.srt");
        File.WriteAllText(path, "1\n00:00:00,000 --> 00:00:06,000\nXin chào Việt Nam\n");
        try
        {
            using var engine = Create();
            engine.Open(MediaSource.FromFile(Fixture("tone-video.mp4"))); engine.Play();
            await Until(engine, s => s.HasVideoOutput && s.Position.TotalMilliseconds > 300);
            var media = engine.Snapshot.Media; var rate = engine.Snapshot.Rate;
            engine.SetSubtitles(SubtitleSelection.External(SubtitleFile.FromFile(path)));
            await Until(engine, s => s.SubtitleTracks.Count > 0 && !s.SubtitlePending && s.SelectedSubtitleId >= 0);
            Assert.Same(media, engine.Snapshot.Media); Assert.Equal(rate, engine.Snapshot.Rate);
            var track = Assert.Single(engine.Snapshot.SubtitleTracks);
            engine.SetSubtitles(SubtitleSelection.Off);
            await Until(engine, s => s.SelectedSubtitleId == -1 && !s.SubtitlePending);
            engine.SelectTrack(PlaybackTrackKind.Subtitle, track.Id);
            await Until(engine, s => s.SelectedSubtitleId == track.Id && !s.SubtitlePending);
            engine.Stop(); engine.Play();
            await Until(engine, s => s.SelectedSubtitleId >= 0 && !s.SubtitlePending);
            engine.Open(MediaSource.FromFile(Fixture("test-tone.mp3"))); engine.Play();
            await Until(engine, s => s.Position.TotalMilliseconds > 200);
            Assert.Empty(engine.Snapshot.SubtitleTracks); Assert.Equal(-1, engine.Snapshot.SelectedSubtitleId);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Subtitle_failure_is_nonfatal_and_disposal_rejects_late_refresh()
    {
        using var engine = Create();
        engine.Open(MediaSource.FromFile(Fixture("tone-video.mp4"))); engine.Play();
        await Until(engine, s => s.HasVideoOutput);
        engine.SelectTrack(PlaybackTrackKind.Subtitle, 99999);
        Assert.NotNull(engine.Snapshot.SubtitleError);
        Assert.Equal(PlaybackState.Playing, engine.Snapshot.State);
        Assert.Null(engine.Snapshot.Error);
        engine.SetSubtitles(SubtitleSelection.Off);
        await Until(engine, s => !s.SubtitlePending);
        Assert.Null(engine.Snapshot.SubtitleError);
        engine.Dispose(); Assert.Throws<ObjectDisposedException>(engine.RefreshSubtitles);
    }

    private static LibVlcPlaybackEngine Create() => new(["--aout=dummy", "--vout=dummy", "--no-sub-autodetect-file"], enableHardwareDecoding: false);
    private static string Fixture(string file) => Path.Combine(AppContext.BaseDirectory, "Fixtures", file);
    private static async Task Until(IPlaybackEngine engine, Func<PlaybackSnapshot, bool> done)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            engine.RefreshSubtitles();
            Assert.Null(engine.Snapshot.Error);
            if (done(engine.Snapshot)) return;
            await Task.Delay(25);
        }
        Assert.Fail($"Subtitle timeout: {engine.Snapshot}; tracks: {string.Join(',', engine.Snapshot.SubtitleTracks)}");
    }
}
