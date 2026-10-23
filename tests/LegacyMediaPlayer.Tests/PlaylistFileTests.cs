using System.Text;
using LegacyMediaPlayer.Application;
using Xunit;

namespace LegacyMediaPlayer.Tests;

public sealed class PlaylistFileTests
{
    [Theory]
    [InlineData(@"\/server/share/song.mp3")]
    [InlineData(@"/\server/share/song.mp3")]
    [InlineData(@"\/./pipe/song.mp3")]
    public async Task Mixed_separator_network_or_device_sources_are_rejected_before_export(string source)
    {
        using var f = new PlaylistFiles(); var output = Path.Combine(f.Root, "q.m3u8");
        await Assert.ThrowsAsync<InvalidDataException>(() => new PlaylistFileService().SaveAsync(output, [source], false));
        Assert.False(File.Exists(output));
    }

    [Theory]
    [InlineData(@"\/server/share/q.m3u8")]
    [InlineData(@"/\server/share/q.m3u8")]
    public void Mixed_separator_playlist_destinations_are_rejected_before_file_IO(string path)
    {
        // Probe the pure validation boundary, never attempt a remote read/write for this test.
        var method = typeof(PlaylistFileService).GetMethod("PlaylistPath", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var error = Assert.Throws<System.Reflection.TargetInvocationException>(() => method.Invoke(null, [path]));
        Assert.IsType<ArgumentException>(error.InnerException);
    }
    [Theory]
    [InlineData(".m3u", true)] [InlineData(".m3u8", true)] [InlineData(".m3u8", false)]
    public async Task Round_trip_preserves_unicode_order_duplicates_and_portable_paths(string extension, bool relative)
    {
        using var f = new PlaylistFiles();
        var first = f.Media("âm nhạc Việt Nam.mp3"); var second = f.Media("# intro.flac"); var spaced = f.Media(" leading.wav");
        var playlist = Path.Combine(f.Root, "queue" + extension);
        var service = new PlaylistFileService();
        await service.SaveAsync(playlist, [first, second, first, spaced], relative);
        var result = await service.LoadAsync(playlist);
        Assert.Equal(new[] { first, second, first, spaced }, result.Paths);
        Assert.Equal(4, result.EntryCount); Assert.Equal(0, result.SkippedCount);
        var content = File.ReadAllText(playlist, Encoding.UTF8);
        Assert.StartsWith("#EXTM3U", content);
        if (relative) Assert.DoesNotContain(f.Root, content); else Assert.Contains(f.Root, content);
    }

    [Fact]
    public async Task Relative_paths_use_playlist_directory_not_process_directory()
    {
        using var f = new PlaylistFiles(); var file = f.Media("sub/one.mp3");
        var path = f.Write("local.m3u8", "#EXTM3U\n#EXTINF:1,Ignore metadata\nsub/one.mp3\n");
        var result = await new PlaylistFileService().LoadAsync(path);
        Assert.Equal(file, Assert.Single(result.Paths));
    }

    [Theory]
    [InlineData("https://example.invalid/song.mp3")]
    [InlineData("file://server/share/song.mp3")]
    [InlineData("\\\\server\\share\\song.mp3")]
    [InlineData("//server/share/song.mp3")]
    [InlineData("\\\\?\\C:\\song.mp3")]
    [InlineData("C:drive-relative.mp3")]
    [InlineData("another.m3u8")]
    [InlineData("bad\0name.mp3")]
    [InlineData("NUL.mp3")]
    public async Task Unsafe_or_invalid_entries_are_skipped_without_losing_valid_entries(string entry)
    {
        using var f = new PlaylistFiles(); var valid = f.Media("good.mp3");
        var result = await new PlaylistFileService().LoadAsync(f.Write("q.m3u8", entry + "\ngood.mp3\nmissing.flac\n"));
        Assert.Equal(valid, Assert.Single(result.Paths)); Assert.Equal(3, result.EntryCount); Assert.Equal(2, result.SkippedCount);
    }

    [Theory] [InlineData("")] [InlineData("#EXTM3U\n#EXTINF:-1,comment\n\n")]
    public async Task Empty_and_comments_only_playlists_are_valid_empty_queues(string content)
    {
        using var f = new PlaylistFiles(); var result = await new PlaylistFileService().LoadAsync(f.Write("q.m3u8", content));
        Assert.Empty(result.Paths); Assert.Equal(0, result.EntryCount);
    }

    [Fact]
    public async Task Invalid_utf8_m3u8_is_not_silently_decoded()
    {
        using var f = new PlaylistFiles(); var path = Path.Combine(f.Root, "q.m3u8"); File.WriteAllBytes(path, [0xff, 0xfe, 0x41]);
        await Assert.ThrowsAsync<InvalidDataException>(() => new PlaylistFileService().LoadAsync(path));
    }

    [Fact]
    public async Task Missing_playlist_is_a_read_failure_not_an_empty_playlist()
    {
        using var f = new PlaylistFiles();
        await Assert.ThrowsAsync<FileNotFoundException>(() => new PlaylistFileService().LoadAsync(Path.Combine(f.Root, "missing.m3u8")));
    }

    [Theory] [InlineData("size")] [InlineData("line")] [InlineData("count")]
    public async Task Oversized_inputs_are_rejected(string kind)
    {
        using var f = new PlaylistFiles(); var path = f.Write("q.m3u8", kind switch
        {
            "size" => new string('x', 4 * 1024 * 1024 + 1),
            "line" => new string('x', 32769),
            _ => string.Concat(Enumerable.Repeat("missing.mp3\n", 10001))
        });
        await Assert.ThrowsAsync<InvalidDataException>(() => new PlaylistFileService().LoadAsync(path));
    }

    [Fact]
    public async Task Cancelled_export_preserves_destination_and_leaves_no_temporary_file()
    {
        using var f = new PlaylistFiles(); var file = f.Media("good.mp3"); var path = f.Write("q.m3u8", "original");
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new PlaylistFileService().SaveAsync(path, [file], true, cancel.Token));
        Assert.Equal("original", File.ReadAllText(path)); Assert.Empty(Directory.GetFiles(f.Root, "*.tmp"));
    }

    [Fact]
    public async Task Failed_atomic_replace_preserves_existing_destination()
    {
        using var f = new PlaylistFiles(); var path = f.Write("q.m3u8", "original"); var file = f.Media("good.mp3");
        using (var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var error = await Record.ExceptionAsync(() => new PlaylistFileService().SaveAsync(path, [file], false));
            Assert.True(error is IOException or UnauthorizedAccessException);
        }
        Assert.Equal("original", File.ReadAllText(path)); Assert.Empty(Directory.GetFiles(f.Root, "*.tmp"));
    }

    [Fact]
    public async Task Export_never_modifies_source_and_can_save_an_empty_queue()
    {
        using var f = new PlaylistFiles(); var source = f.Media("good.mp3"); var before = File.ReadAllBytes(source);
        var service = new PlaylistFileService(); var path = Path.Combine(f.Root, "q.m3u8");
        await service.SaveAsync(path, [source], true); Assert.Equal(before, File.ReadAllBytes(source));
        await service.SaveAsync(path, [], false); Assert.Empty((await service.LoadAsync(path)).Paths);
    }

    [Fact]
    public async Task Utf8_bom_and_legacy_ansi_m3u_are_read_without_losing_filename_characters()
    {
        using var f = new PlaylistFiles(); var media = f.Media("café.mp3");
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var legacy = Path.Combine(f.Root, "legacy.m3u"); File.WriteAllText(legacy, "café.mp3\n", Encoding.GetEncoding(0));
        Assert.Equal(media, Assert.Single((await new PlaylistFileService().LoadAsync(legacy)).Paths));
        var unicode = Path.Combine(f.Root, "unicode.m3u8"); File.WriteAllText(unicode, "#EXTM3U\ncafé.mp3\n", new UTF8Encoding(true));
        Assert.Equal(media, Assert.Single((await new PlaylistFileService().LoadAsync(unicode)).Paths));
    }

    [Fact]
    public async Task Locked_media_is_skipped_and_valid_entries_remain_available()
    {
        using var f = new PlaylistFiles(); var locked = f.Media("locked.mp3"); var good = f.Media("good.flac");
        using var held = new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var result = await new PlaylistFileService().LoadAsync(f.Write("q.m3u8", locked+"\n"+good));
        Assert.Equal(good, Assert.Single(result.Paths)); Assert.Equal(1, result.SkippedCount);
    }

    [Fact]
    public async Task Portable_export_keeps_working_after_playlist_and_media_are_relocated()
    {
        using var original = new PlaylistFiles(); using var moved = new PlaylistFiles();
        var source = original.Media("media/Việt.mp3"); var playlist = Path.Combine(original.Root, "q.m3u8");
        await new PlaylistFileService().SaveAsync(playlist, [source, source], true);
        var movedMedia = moved.Media("media/Việt.mp3"); var copy = Path.Combine(moved.Root, "q.m3u8"); File.Copy(playlist, copy);
        var result = await new PlaylistFileService().LoadAsync(copy);
        Assert.Equal(new[] { movedMedia, movedMedia }, result.Paths);
    }
}

internal sealed class PlaylistFiles : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "lmp-playlist-" + Guid.NewGuid());
    public PlaylistFiles() => Directory.CreateDirectory(Root);
    public string Write(string name, string content)
    { var path = Path.GetFullPath(Path.Combine(Root, name)); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, content, new UTF8Encoding(false)); return path; }
    public string Media(string name) => Write(name, "fixture");
    public void Dispose() => Directory.Delete(Root, true);
}
