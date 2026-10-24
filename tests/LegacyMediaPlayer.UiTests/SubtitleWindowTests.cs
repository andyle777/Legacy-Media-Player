using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using LegacyMediaPlayer.App;
using LegacyMediaPlayer.Application;
using LegacyMediaPlayer.Core;
using Xunit;

namespace LegacyMediaPlayer.UiTests;

public partial class PlayerWindowTests
{
    [AvaloniaTheory]
    [InlineData("repeat")]
    [InlineData("replace")]
    [InlineData("stop")]
    [InlineData("close")]
    public async Task Subtitle_picker_survives_same_item_repeat_but_discards_replaced_or_closed_context(string transition)
    {
        using var media = new DropTestFiles("video.mp4", "subtitle.srt");
        File.WriteAllText(media[1], "1\n00:00:00,000 --> 00:00:06,000\nXin chào\n");
        var picked = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var engine = new TracksEngine();
        var window = new MainWindow(engine, false, pickSubtitlePath: () => picked.Task);
        try
        {
            window.Show(); window.OpenFile(media[0]); Dispatcher.UIThread.RunJobs();
            Click(window, "RepeatButton"); Click(window, "RepeatButton");
            var opening = window.PickSubtitleAsync();
            Assert.False(opening.IsCompleted);
            if (transition == "repeat") { engine.End(); Dispatcher.UIThread.RunJobs(); }
            if (transition == "replace") window.OpenFile(media[0]);
            if (transition == "stop") Click(window, "StopButton");
            if (transition == "close") window.Close();
            picked.SetResult(media[1]); await opening;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(transition == "repeat" ? 1 : 0, engine.Attachments);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Subtitle_controls_are_accessible_and_idle_does_not_load_codecs()
    {
        var creates = 0;
        using var engine = new DeferredPlaybackEngine(() => { creates++; return new MockPlaybackEngine(); }, new(true, true, true));
        var window = new MainWindow(engine, false);
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var selector = window.FindControl<ComboBox>("SubtitleSelector")!;
            Assert.NotNull(selector); Assert.False(selector.IsEnabled);
            Assert.Equal("Subtitles", AutomationProperties.GetName(selector));
            Assert.False(window.FindControl<Button>("OpenSubtitleButton")!.IsEnabled);
            Assert.Equal(0, creates);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Subtitle_selection_uses_native_id_and_error_preserves_actual_selection_and_playback()
    {
        using var file = new DropTestFiles("video.mp4"); using var engine = new TracksEngine();
        var window = new MainWindow(engine, false);
        try
        {
            window.Show(); window.OpenFile(file[0]); Dispatcher.UIThread.RunJobs();
            var selector = window.FindControl<ComboBox>("SubtitleSelector")!;
            Assert.True(selector.IsEnabled); Assert.Equal(3, selector.Items.Count);
            Assert.Equal(3, selector.Items.Select(i => i!.ToString()).Distinct().Count()); // duplicate labels distinguish IDs
            selector.SelectedIndex = 2; Dispatcher.UIThread.RunJobs();
            Assert.Equal(42, engine.Snapshot.SelectedSubtitleId);
            Assert.Equal(PlaybackState.Playing, engine.Snapshot.State);
            engine.Reject = true; selector.SelectedIndex = 1; Dispatcher.UIThread.RunJobs();
            Assert.Equal(2, selector.SelectedIndex);
            Assert.True(window.FindControl<TextBlock>("ErrorLabel")!.IsVisible);
            engine.Reject = false; selector.SelectedIndex = 0; Dispatcher.UIThread.RunJobs();
            Assert.Equal(-1, engine.Snapshot.SelectedSubtitleId);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delayed_subtitle_result_cannot_attach_after_window_replacement_or_close(bool close)
    {
        using var media = new DropTestFiles("video.mp4", "subtitle.srt");
        File.WriteAllText(media[1], "1\n00:00:00,000 --> 00:00:05,000\nXin chào\n");
        using var engine = new TracksEngine(); var window = new MainWindow(engine, false);
        try
        {
            window.Show(); window.OpenFile(media[0]); Dispatcher.UIThread.RunJobs();
            var load = window.LoadSubtitleAsync(media[1]);
            if (close) window.Close(); else window.OpenFile(media[0]);
            Assert.False(await load);
            Assert.Equal(0, engine.Attachments);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Audio_switch_disables_subtitles_and_removes_previous_track_list()
    {
        using var media = new DropTestFiles("video.mp4", "audio.mp3"); using var engine = new TracksEngine();
        var window = new MainWindow(engine, false);
        try
        {
            window.Show(); window.OpenFile(media[0]); Dispatcher.UIThread.RunJobs();
            window.FindControl<ComboBox>("SubtitleSelector")!.SelectedIndex = 2;
            window.OpenFile(media[1]); Dispatcher.UIThread.RunJobs();
            Assert.False(window.FindControl<ComboBox>("SubtitleSelector")!.IsEnabled);
            Assert.False(window.FindControl<Button>("OpenSubtitleButton")!.IsEnabled);
            Assert.Equal(-1, engine.Snapshot.SelectedSubtitleId);
        }
        finally { window.Close(); }
    }

    private sealed class TracksEngine : IPlaybackEngine
    {
        private readonly MockPlaybackEngine _inner = new();
        private bool _video, _ended; private int _selected = -1; private PlaybackError? _error;
        public bool Reject { get; set; }
        public int Attachments { get; private set; }
        public TracksEngine() => _inner.Changed += (_, _) => Changed?.Invoke(this, new(Snapshot));
        public PlaybackCapabilities Capabilities { get; } = new(true, true, true);
        public PlaybackSnapshot Snapshot => _inner.Snapshot with { HasVideo = _video, State = _ended ? PlaybackState.Ended : _inner.Snapshot.State, SubtitleError = _error, SelectedSubtitleId = _selected,
            SubtitleTracks = _video ? Array.AsReadOnly(new[] { new PlaybackTrack(7, PlaybackTrackKind.Subtitle, "Vietnamese"), new PlaybackTrack(42, PlaybackTrackKind.Subtitle, "Vietnamese") }) : Array.Empty<PlaybackTrack>() };
        public event EventHandler<PlaybackChangedEventArgs>? Changed;
        public void Open(MediaSource source) { _ended = false; _video = Path.GetExtension(source.Path) == ".mp4"; _selected = -1; _error = null; _inner.Open(source); }
        public void End() { _ended = true; Changed?.Invoke(this, new(Snapshot)); }
        public void Play() => _inner.Play(); public void Pause() => _inner.Pause(); public void Stop() => _inner.Stop();
        public void Seek(TimeSpan position) => _inner.Seek(position); public void SetVolume(int volume) => _inner.SetVolume(volume);
        public void SetMuted(bool muted) => _inner.SetMuted(muted); public void SetRate(float rate) => _inner.SetRate(rate);
        public IReadOnlyList<PlaybackTrack> GetTracks() => Snapshot.SubtitleTracks;
        public void SelectTrack(PlaybackTrackKind kind, int id) => throw new NotSupportedException();
        public void SetSubtitles(SubtitleSelection selection)
        {
            if (Reject) _error = new("subtitle-unavailable", "Track unavailable");
            else { _error = null; _selected = selection.TrackId ?? -1; if (selection.File is not null) Attachments++; }
            Changed?.Invoke(this, new(Snapshot));
        }
        public void RefreshSubtitles() { }
        public void AttachVideoSurface(VideoSurface? surface) { }
        public void Dispose() => _inner.Dispose();
    }
}
