using LegacyMediaPlayer.Core;
using LegacyMediaPlayer.Playback.LibVLC;
using Xunit;

namespace LegacyMediaPlayer.NativeTests;

public sealed class PlaybackRatePolicyTests
{
    [Theory]
    [InlineData(-1, 1f)] [InlineData(0, 1f)] [InlineData(0, -1f)]
    public void Native_rejection_or_mismatched_readback_keeps_previous_preference(int result, float readback)
    {
        var before = PlaybackSnapshot.Empty with { Rate = 1.25f, AcceptedRate = 1.25f, State = PlaybackState.Paused };
        var after = NativePlaybackRate.Apply(before, 2, _ => result, () => readback);
        Assert.Equal(1.25f, after.Rate);
        Assert.NotNull(after.RateError);
        Assert.Equal(PlaybackState.Paused, after.State);
        Assert.Null(after.Error); // A rate limitation must not fail decoding or queue advancement.
        Assert.Equal(readback > 0 ? readback : (float?)null, after.AcceptedRate);
    }

    [Fact]
    public void Accepted_rate_clears_previous_warning()
    {
        var before = PlaybackSnapshot.Empty with { RateError = new("rate-unavailable", "Previous failure") };
        var after = NativePlaybackRate.Apply(before, 0.5f, _ => 0, () => 0.5f);
        Assert.Equal(0.5f, after.Rate); Assert.Equal(0.5f, after.AcceptedRate); Assert.Null(after.RateError);
    }
}
