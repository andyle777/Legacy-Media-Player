namespace LegacyMediaPlayer.Core;

public static class PlaybackRates
{
    public static IReadOnlyList<float> Supported { get; } = Array.AsReadOnly<float>([0.5f, 0.75f, 1f, 1.25f, 1.5f, 2f]);
    public static bool IsSupported(float rate) => Supported.Contains(rate);
    public static void Validate(float rate)
    {
        if (!IsSupported(rate)) throw new ArgumentOutOfRangeException(nameof(rate), "Choose a supported playback speed from 0.5× to 2.0×.");
    }
}
