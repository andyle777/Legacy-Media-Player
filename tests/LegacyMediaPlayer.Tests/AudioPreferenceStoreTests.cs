using LegacyMediaPlayer.Application;
using Xunit;

namespace LegacyMediaPlayer.Tests;

public sealed class AudioPreferenceStoreTests
{
    [Fact]
    public void Volume_and_mute_round_trip_without_storing_media_paths()
    {
        using var file = new TempMedia();
        var store = new AudioPreferenceStore(file.Path);
        store.Save(new(37, true));
        Assert.Equal(new AudioPreferences(37, true), store.Load());
        Assert.DoesNotContain("Media", File.ReadAllText(file.Path));
    }

    [Theory]
    [InlineData("bad json")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"Volume\":101,\"IsMuted\":true}")]
    public void Damaged_or_out_of_range_preferences_use_safe_defaults(string contents)
    {
        using var file = new TempMedia();
        File.WriteAllText(file.Path, contents);
        Assert.Equal(new AudioPreferences(), new AudioPreferenceStore(file.Path).Load());
    }

    [Fact]
    public void Missing_or_unwritable_settings_do_not_prevent_playback()
    {
        using var file = new TempMedia();
        Assert.Equal(new AudioPreferences(), new AudioPreferenceStore(file.Path + ".missing").Load());
        // A regular file cannot be used as the parent directory for preferences.
        new AudioPreferenceStore(Path.Combine(file.Path, "preferences.json")).Save(new(20, false));
    }
}
