using LegacyMediaPlayer.Core;

namespace LegacyMediaPlayer.Playback.LibVLC;

/// <summary>LibVLC reports request acceptance, not measured sound/frame progression.</summary>
internal static class NativePlaybackRate
{
    internal static PlaybackSnapshot Apply(PlaybackSnapshot snapshot, float requested,
        Func<float, int> set, Func<float> read)
    {
        var result = set(requested);
        var reported = read();
        float? accepted = float.IsFinite(reported) && reported > 0 ? reported : null;
        return result == 0 && accepted is { } rate && Math.Abs(rate - requested) < 0.001f
            ? snapshot with { Rate = requested, AcceptedRate = rate, RateError = null }
            : snapshot with
            {
                AcceptedRate = accepted,
                RateError = new("rate-unavailable", "This media could not accept the requested playback speed. " +
                    (accepted is { } actual ? $"LibVLC reports {actual:0.##}×; the saved preference is unchanged." : "The native rate is unavailable; the saved preference is unchanged."))
            };
    }
}
