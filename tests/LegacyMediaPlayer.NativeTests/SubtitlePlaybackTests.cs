using LegacyMediaPlayer.Core;
using LegacyMediaPlayer.Application;
using System.Collections.Concurrent;
using LegacyMediaPlayer.Playback.LibVLC;
using Xunit;

namespace LegacyMediaPlayer.NativeTests;

[Collection("Native playback")]
public sealed class SubtitlePlaybackTests
{
    [Fact]
    public async Task Subtitle_choice_after_held_natural_end_is_applied_to_repeated_native_input()
    {
        var path = Path.Combine(Path.GetTempPath(), $"Việt-modal-{Guid.NewGuid():N}.srt");
        File.WriteAllText(path, "1\n00:00:00,000 --> 00:00:06,000\nXin chào Việt Nam\n");
        try
        {
            var pending = new ConcurrentQueue<Action>();
            using var engine = Create(); using var session = new PlayerSession(engine, pending.Enqueue);
            session.OpenFiles([Fixture("tone-video.mp4")]); session.SetRepeatMode(RepeatMode.One);
            await Pump(s => s.HasVideoOutput && s.CanSeek);
            using var modal = session.DeferAutomaticAdvance();
            session.SetRate(2); session.Seek(TimeSpan.FromSeconds(5.4));
            await Pump(s => s.State == PlaybackState.Ended);
            Assert.True(await session.LoadSubtitleAsync(path));
            Assert.Equal(PlaybackState.Ended, session.Snapshot.State);
            modal.Dispose();
            await Pump(s => s.State == PlaybackState.Playing && !s.SubtitlePending &&
                s.SubtitleTracks.Any(t => t.Id == s.SelectedSubtitleId && t.ExternalSubtitle?.Path == path));
            Assert.Equal(2f, session.Snapshot.Rate);
            session.Stop();

            async Task Pump(Func<PlaybackSnapshot, bool> done)
            {
                var until = DateTime.UtcNow.AddSeconds(15);
                while (DateTime.UtcNow < until)
                {
                    while (pending.TryDequeue(out var action)) action();
                    session.RefreshSubtitleState();
                    if (done(session.Snapshot)) return;
                    await Task.Delay(20);
                }
                Assert.Fail($"Modal subtitle timeout: {session.Snapshot}; {session.ErrorMessage}");
            }
        }
        finally { File.Delete(path); }
    }
    [Fact]
    public async Task External_off_reselection_and_natural_repeat_keep_source_until_queue_item_changes()
    {
        var path = Path.Combine(Path.GetTempPath(), $"Việt #{Guid.NewGuid():N}.srt");
        File.WriteAllText(path, "1\n00:00:00,000 --> 00:00:06,000\nXin chào Việt Nam\n");
        try
        {
            var pending = new ConcurrentQueue<Action>();
            using var engine = Create(); using var session = new PlayerSession(engine, pending.Enqueue);
            session.OpenFiles([Fixture("tone-video.mp4"), Fixture("test-tone.mp3")]);
            await Pump(s => s.HasVideoOutput);
            Assert.True(await session.LoadSubtitleAsync(path));
            await Pump(s => !s.SubtitlePending && s.SelectedSubtitleId >= 0);
            var external = Assert.Single(session.Snapshot.SubtitleTracks);
            session.SelectSubtitle(-1); await Pump(s => !s.SubtitlePending && s.SelectedSubtitleId == -1);
            session.SelectSubtitle(external.Id); await Pump(s => !s.SubtitlePending && s.SelectedSubtitleId >= 0);
            session.SetRepeatMode(RepeatMode.One); session.SetRate(2); session.Seek(TimeSpan.FromSeconds(5.5));
            await Pump(s => s.State == PlaybackState.Playing && s.Position.TotalSeconds < 3 && s.SelectedSubtitleId >= 0 && !s.SubtitlePending);
            Assert.Equal(path, session.Snapshot.SubtitleTracks.Single(t => t.Id == session.Snapshot.SelectedSubtitleId).ExternalSubtitle!.Path);
            Assert.Equal(0, session.CurrentIndex); Assert.Equal(2f, session.Snapshot.Rate);
            session.Next(); await Pump(s => s.State == PlaybackState.Playing && s.Position.TotalMilliseconds > 100);
            Assert.Empty(session.Snapshot.SubtitleTracks); Assert.Equal(1, session.CurrentIndex);

            async Task Pump(Func<PlaybackSnapshot, bool> done)
            {
                var until = DateTime.UtcNow.AddSeconds(15);
                while (DateTime.UtcNow < until)
                {
                    while (pending.TryDequeue(out var action)) action();
                    session.RefreshSubtitleState();
                    if (done(session.Snapshot)) return;
                    await Task.Delay(20);
                }
                Assert.Fail($"Session subtitle timeout: {session.Snapshot}; {session.ErrorMessage}");
            }
        }
        finally { File.Delete(path); }
    }

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
        engine.SetSubtitles(SubtitleSelection.Track(vietnamese.Id, vietnamese.ReplayKey).ForReplay()); engine.Play();
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
            engine.Stop(); engine.Play();
            await Until(engine, s => s.SubtitleTracks.Count > 0 && !s.SubtitlePending && s.SelectedSubtitleId >= 0);
            track = Assert.Single(engine.Snapshot.SubtitleTracks);
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
