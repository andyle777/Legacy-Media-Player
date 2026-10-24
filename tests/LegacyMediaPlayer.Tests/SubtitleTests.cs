using System.Collections.Concurrent;
using LegacyMediaPlayer.Application;
using LegacyMediaPlayer.Core;
using Xunit;

namespace LegacyMediaPlayer.Tests;

public sealed class SubtitleTests
{
    [Theory]
    [InlineData(RepeatMode.One, false)]
    [InlineData(RepeatMode.All, false)]
    [InlineData(RepeatMode.All, true)]
    public async Task Same_item_natural_repeat_keeps_pending_subtitle_validation(RepeatMode repeat, bool shuffle)
    {
        using var media = new TempMedia(); using var srt = new Srt(); using var engine = new SubtitleEngine();
        using var session = new PlayerSession(engine, action => action());
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        session.OpenFiles([media.Path]); session.SetRepeatMode(repeat); session.SetShuffleEnabled(shuffle);
        var item = session.CurrentItem;
        var loading = session.LoadSubtitleAsync(srt.Path, default, p => { entered.Set(); release.Wait(); return SubtitleFile.FromFile(p); });
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
        engine.End();
        Assert.Same(item, session.CurrentItem); Assert.Equal(PlaybackState.Playing, session.Snapshot.State);
        release.Set();
        Assert.True(await loading);
        Assert.Equal(srt.Path, engine.LastSelection?.File?.Path);
    }

    [Theory]
    [InlineData("https://example.com/a.srt")]
    [InlineData("relative.srt")]
    [InlineData("\\\\server\\share\\a.srt")]
    [InlineData("/\\server/share/a.srt")]
    [InlineData("C:\\file.srt:stream")]
    [InlineData("C:\\file.txt")]
    public void Subtitle_rejects_nonlocal_device_and_non_srt_paths(string path) =>
        Assert.ThrowsAny<ArgumentException>(() => SubtitleFile.FromFile(path));

    [Fact]
    public void Subtitle_unicode_and_reserved_characters_round_trip_without_source_writes()
    {
        using var file = new Srt();
        var source = SubtitleFile.FromFile(file.Path);
        Assert.Equal(file.Path, source.Uri.LocalPath);
        Assert.Contains("%23", source.Uri.AbsoluteUri);
        Assert.Contains("Xin chào", File.ReadAllText(file.Path));
    }

    [Fact]
    public void Subtitle_refresh_does_not_initialize_idle_deferred_backend()
    {
        var creates = 0;
        using var engine = new DeferredPlaybackEngine(() => { creates++; return new MockPlaybackEngine(); }, new(true, true, true));
        engine.RefreshSubtitles();
        Assert.Empty(engine.Snapshot.SubtitleTracks);
        Assert.Equal(0, creates);
        Assert.Throws<InvalidOperationException>(() => engine.SetSubtitles(SubtitleSelection.Off));
        Assert.Equal(0, creates);
    }

    [Fact]
    public void Off_and_track_choices_survive_stop_repeat_but_not_duplicate_queue_items()
    {
        using var file = new TempMedia(); using var engine = new SubtitleEngine();
        using var session = new PlayerSession(engine, a => a());
        session.OpenFiles([file.Path, file.Path]);
        session.SelectSubtitle(42);
        Assert.Equal(42, session.Snapshot.SelectedSubtitleId);
        session.Stop(); session.Play();
        Assert.Equal(42, session.Snapshot.SelectedSubtitleId);
        session.SetRepeatMode(RepeatMode.One); engine.End();
        Assert.Equal("Vietnamese", engine.LastSelection?.TrackName);
        Assert.Null(engine.LastSelection?.TrackId); // IDs must be resolved again after Open.
        session.Next();
        Assert.Null(engine.LastSelection);
        Assert.Equal(-1, session.Snapshot.SelectedSubtitleId);
        session.SelectSubtitle(-1); session.Stop(); session.Play();
        Assert.Equal(SubtitleSelection.Off, engine.LastSelection);
    }

    [Fact]
    public async Task Subtitle_validation_failure_preserves_media_and_rate()
    {
        using var file = new TempMedia(); using var engine = new SubtitleEngine();
        using var session = new PlayerSession(engine, a => a());
        session.OpenFile(file.Path); session.Play(); session.SetRate(1.5f);
        Assert.False(await session.LoadSubtitleAsync("missing.srt"));
        Assert.Equal(PlaybackState.Playing, session.Snapshot.State);
        Assert.Equal(1.5f, session.Snapshot.Rate);
        Assert.NotNull(session.ErrorMessage);
    }

    [Theory]
    [InlineData("switch")]
    [InlineData("stop")]
    [InlineData("close")]
    [InlineData("cancel")]
    public async Task Delayed_validation_cannot_commit_after_lifetime_change(string change)
    {
        using var file = new TempMedia(); using var srt = new Srt(); using var engine = new SubtitleEngine();
        using var session = new PlayerSession(engine, a => a());
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        using var cancel = new CancellationTokenSource();
        session.OpenFiles([file.Path, file.Path]);
        var load = session.LoadSubtitleAsync(srt.Path, cancel.Token, p =>
        { entered.Set(); release.Wait(); return SubtitleFile.FromFile(p); });
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
        if (change == "switch") session.Next();
        if (change == "stop") session.Stop();
        if (change == "close") session.Dispose();
        if (change == "cancel") cancel.Cancel();
        try { Assert.False(await load.WaitAsync(TimeSpan.FromSeconds(2))); }
        finally { release.Set(); }
        Assert.Null(engine.LastSelection);
    }

    [Fact]
    public async Task Newer_subtitle_and_reorder_use_current_identity_and_supersede_older_work()
    {
        using var file = new TempMedia(); using var one = new Srt(); using var two = new Srt(); using var engine = new SubtitleEngine();
        using var session = new PlayerSession(engine, a => a());
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        session.OpenFiles([file.Path, file.Path]);
        var old = session.LoadSubtitleAsync(one.Path, default, p => { entered.Set(); release.Wait(); return SubtitleFile.FromFile(p); });
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
        var latest = session.LoadSubtitleAsync(two.Path);
        session.MoveItem(0, 1);
        release.Set();
        Assert.False(await old);
        Assert.True(await latest);
        Assert.Equal(two.Path, engine.LastSelection?.File?.Path);
        Assert.Equal(1, session.CurrentIndex);
    }

    private sealed class Srt : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"âm nhạc #{Guid.NewGuid():N}.srt");
        public Srt() => File.WriteAllText(Path, "1\n00:00:00,000 --> 00:00:04,000\nXin chào Việt Nam\n");
        public void Dispose() => File.Delete(Path);
    }

    // Only native boundary substituted: exercise the real session and worker validation.
    private sealed class SubtitleEngine : IPlaybackEngine
    {
        private readonly MockPlaybackEngine _inner = new();
        public SubtitleSelection? LastSelection { get; private set; }
        private int _selected = -1;
        private bool _ended;
        private SubtitleFile? _file;
        public SubtitleEngine() => _inner.Changed += (_, _) => Changed?.Invoke(this, new(Snapshot));
        public PlaybackCapabilities Capabilities { get; } = new(true, true, true);
        public PlaybackSnapshot Snapshot => _inner.Snapshot with { HasVideo = true, State = _ended ? PlaybackState.Ended : _inner.Snapshot.State,
            SubtitleTracks = Array.AsReadOnly((_file is null ? new[] { new PlaybackTrack(42, PlaybackTrackKind.Subtitle, "Vietnamese") } :
                new[] { new PlaybackTrack(42, PlaybackTrackKind.Subtitle, "Vietnamese"), new PlaybackTrack(100, PlaybackTrackKind.Subtitle, "External") { ExternalSubtitle = _file } })), SelectedSubtitleId = _selected };
        public event EventHandler<PlaybackChangedEventArgs>? Changed;
        public void Open(MediaSource source) { _file = null; _ended = false; LastSelection = null; _selected = -1; _inner.Open(source); }
        public void Play() => _inner.Play(); public void Pause() => _inner.Pause(); public void Stop() => _inner.Stop();
        public void End() { _ended = true; Changed?.Invoke(this, new(Snapshot)); }
        public void Seek(TimeSpan position) => _inner.Seek(position);
        public void SetVolume(int volume) => _inner.SetVolume(volume); public void SetMuted(bool muted) => _inner.SetMuted(muted);
        public void SetRate(float rate) => _inner.SetRate(rate);
        public IReadOnlyList<PlaybackTrack> GetTracks() => Snapshot.SubtitleTracks;
        public void SelectTrack(PlaybackTrackKind kind, int id) => SetSubtitles(id == -1 ? SubtitleSelection.Off : SubtitleSelection.Track(id, "Vietnamese"));
        public void SetSubtitles(SubtitleSelection selection)
        {
            LastSelection = selection;
            if (selection.File is { } file) { _file = file; _selected = 100; }
            else _selected = selection.TrackId ?? (selection.TrackName is not null ? 42 : -1);
            Changed?.Invoke(this, new(Snapshot));
        }
        public void RefreshSubtitles() { }
        public void AttachVideoSurface(VideoSurface? surface) { }
        public void Dispose() => _inner.Dispose();
    }
}
