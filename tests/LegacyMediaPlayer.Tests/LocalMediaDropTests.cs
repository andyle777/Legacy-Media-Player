using LegacyMediaPlayer.Application;
using Xunit;

namespace LegacyMediaPlayer.Tests;

public class LocalMediaDropTests
{
    [Theory]
    [InlineData(".MP3")]
    [InlineData(".wav")]
    [InlineData(".flac")]
    [InlineData(".mp4")]
    [InlineData(".mkv")]
    [InlineData(".webm")]
    [InlineData(".avi")]
    public void Select_accepts_existing_media_and_keeps_duplicate_entries(string extension)
    {
        var file = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + extension);
        File.WriteAllBytes(file, [1]);
        try
        {
            var selection = LocalMediaDrop.Select([file, file]);
            Assert.Equal(new[] { file, file }, selection.Paths);
            Assert.Equal(0, selection.SkippedCount);
            Assert.True(File.Exists(file));
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void Select_skips_invalid_missing_directory_and_unsupported_items_without_losing_valid_order()
    {
        var first = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".mp3");
        var second = first + ".avi";
        File.WriteAllBytes(first, [1]); File.WriteAllBytes(second, [2]);
        try
        {
            var selection = LocalMediaDrop.Select([null, first, first + ".txt", "relative.wav", "https://example.invalid/music.wav", second, first + ".wav", Path.GetTempPath(), "bad\0.mp4"]);
            Assert.Equal(new[] { first, second }, selection.Paths);
            Assert.Equal(7, selection.SkippedCount);
            Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(first));
            Assert.Equal(new byte[] { 2 }, File.ReadAllBytes(second));
        }
        finally { File.Delete(first); File.Delete(second); }
    }
}
