using LegacyMediaPlayer.Core;
using LegacyMediaPlayer.Playback.LibVLC;
using Xunit;

namespace LegacyMediaPlayer.NativeTests;

public sealed class SubtitleStateTests
{
    [Fact]
    public void External_confirmation_requires_specific_slave_completion_not_an_embedded_default()
    {
        using var f = new Files(); var n = new NativeBoundary(); var state = n.Create();
        state.Request(SubtitleSelection.External(f.One));
        Assert.True(n.Tick(state).SubtitlePending);
        Assert.Equal(7, n.Selected); // Native default is not evidence of SRT acceptance.
        n.Complete(f.One, 42);
        var snapshot = n.Tick(state);
        Assert.False(snapshot.SubtitlePending); Assert.Equal(42, snapshot.SelectedSubtitleId);
        Assert.Equal(f.One, snapshot.SubtitleTracks.Single(t => t.Id == 42).ExternalSubtitle);
        var replay = state.ReplaySelection!;
        n.Clear(); state.Reset(); state.Request(replay);
        Assert.True(n.Tick(state).SubtitlePending);
        n.Complete(f.One, 99);
        Assert.False(n.Tick(state).SubtitlePending);
        Assert.Equal(99, n.Selected);
    }

    [Fact]
    public void Superseded_attachment_cannot_confirm_new_file_and_off_reconciles_late_selection()
    {
        using var f = new Files(); var n = new NativeBoundary(); var state = n.Create();
        state.Request(SubtitleSelection.External(f.One)); n.Tick(state);
        state.Request(SubtitleSelection.External(f.Two));
        n.Complete(f.One, 42); Assert.True(n.Tick(state).SubtitlePending);
        Assert.NotEqual(42, n.Selected);
        n.Complete(f.Two, 91); Assert.False(n.Tick(state).SubtitlePending); Assert.Equal(91, n.Selected);
        state.Request(SubtitleSelection.Off); n.Tick(state);
        n.Selected = 42; Assert.Equal(-1, n.Tick(state).SelectedSubtitleId);
        Assert.All(n.ForcedAttachments, forced => Assert.False(forced));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("duplicate")]
    public void Unidentifiable_or_ambiguous_replay_explicitly_turns_subtitles_off(string? key)
    {
        var n = new NativeBoundary(); var state = n.Create();
        n.Tracks = [new(7, PlaybackTrackKind.Subtitle, "Subtitle 7") { ReplayKey = key },
            new(42, PlaybackTrackKind.Subtitle, "Subtitle 42") { ReplayKey = key }];
        state.Request(SubtitleSelection.Track(7, key).ForReplay());
        var result = n.Tick(state);
        Assert.Equal(-1, result.SelectedSubtitleId); Assert.NotNull(result.SubtitleError);
    }

    [Fact]
    public void Rejected_and_timed_out_requests_keep_the_last_confirmed_replay_choice()
    {
        using var f = new Files(); var n = new NativeBoundary(); var state = n.Create();
        state.Request(SubtitleSelection.Track(7, "English")); Assert.False(n.Tick(state).SubtitlePending);
        n.Tracks.Add(new(42, PlaybackTrackKind.Subtitle, "Vietnamese")); n.RejectId = 42;
        state.Request(SubtitleSelection.Track(42, "Vietnamese"));
        Assert.NotNull(n.Tick(state).SubtitleError);
        Assert.Equal("English", state.ReplaySelection!.TrackName);
        state.Request(SubtitleSelection.External(f.One)); n.Tick(state);
        Assert.NotNull(n.Tick(state, TimeSpan.FromSeconds(6)).SubtitleError);
        Assert.Equal("English", state.ReplaySelection!.TrackName); Assert.Equal(7, n.Selected);
    }

    [Fact]
    public void Off_retains_external_sources_for_current_item_replay()
    {
        using var f = new Files(); var n = new NativeBoundary(); var state = n.Create();
        state.Request(SubtitleSelection.External(f.One)); n.Tick(state); n.Complete(f.One, 42); n.Tick(state);
        state.Request(SubtitleSelection.Off); n.Tick(state);
        Assert.Contains(f.One, state.ReplaySelection!.ExternalFiles);
        n.Clear(); var replay = state.ReplaySelection; state.Reset(); state.Request(replay!);
        n.Tick(state); n.Complete(f.One, 90); var result = n.Tick(state);
        Assert.Equal(-1, result.SelectedSubtitleId);
        Assert.Equal(f.One, result.SubtitleTracks.Single(t => t.Id == 90).ExternalSubtitle);
    }

    private sealed class NativeBoundary
    {
        public List<PlaybackTrack> Tracks = [new(7, PlaybackTrackKind.Subtitle, "English")];
        public List<string> Slaves = [];
        public List<bool> ForcedAttachments = [];
        public int Selected = 7;
        public int? RejectId;
        public NativeSubtitles Create() => new(() => new(Tracks.ToArray(), Selected, Slaves.ToArray()),
            id => { if (id == RejectId) return false; Selected = id; return true; },
            (file, force) => { ForcedAttachments.Add(force); return true; });
        public PlaybackSnapshot Tick(NativeSubtitles state, TimeSpan elapsed = default) =>
            state.Refresh(PlaybackSnapshot.Empty with { State = PlaybackState.Playing, HasVideo = true }, elapsed);
        public void Complete(SubtitleFile file, int id) { Slaves.Add(file.Uri.AbsoluteUri); Tracks.Add(new(id, PlaybackTrackKind.Subtitle, "External")); }
        public void Clear() { Tracks = [new(7, PlaybackTrackKind.Subtitle, "English")]; Slaves.Clear(); Selected = 7; }
    }
    private sealed class Files : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        public SubtitleFile One { get; }
        public SubtitleFile Two { get; }
        public Files()
        {
            Directory.CreateDirectory(_root);
            var one = Path.Combine(_root, "one.srt"); var two = Path.Combine(_root, "two.srt");
            File.WriteAllText(one, "1\n00:00:00,000 --> 00:00:06,000\nHello\n"); File.Copy(one, two);
            One = SubtitleFile.FromFile(one); Two = SubtitleFile.FromFile(two);
        }
        public void Dispose() { File.Delete(One.Path); File.Delete(Two.Path); Directory.Delete(_root); }
    }
}
