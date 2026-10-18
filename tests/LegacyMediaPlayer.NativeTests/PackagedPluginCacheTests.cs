using System.Security.Cryptography;
using System.Text.Json;
using LegacyMediaPlayer.Core;
using LegacyMediaPlayer.Playback.LibVLC;
using Xunit;

namespace LegacyMediaPlayer.NativeTests;

public sealed class PackagedPluginCacheTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "lmp-cache-" + Guid.NewGuid().ToString("N"));

    public PackagedPluginCacheTests()
    {
        Directory.CreateDirectory(Path.Combine(_directory, "plugins", "codec"));
        foreach (var file in new[] { "libvlc.dll", "libvlccore.dll", "plugins/plugins.dat", "plugins/codec/libtest_plugin.dll", "plugins/codec/libother_plugin.dll" })
            File.WriteAllText(Path.Combine(_directory, file), "controlled content: " + file);
        var hashes = new Dictionary<string, object>();
        foreach (var file in Directory.GetFiles(_directory, "*", SearchOption.AllDirectories))
            hashes[Path.GetRelativePath(_directory, file).Replace('\\', '/')] = new {
                Length = new FileInfo(file).Length,
                Modified = new DateTimeOffset(File.GetLastWriteTimeUtc(file)).ToUnixTimeSeconds(),
                Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)))
            };
        File.WriteAllText(Path.Combine(_directory, "plugins", "lmp-cache.json"), JsonSerializer.Serialize(hashes));
    }

    [Fact]
    public void Verified_bundle_survives_timestamp_changes_without_loading_plugins()
    {
        foreach (var file in Directory.GetFiles(_directory, "*", SearchOption.AllDirectories))
            File.SetLastWriteTimeUtc(file, File.GetLastWriteTimeUtc(file).AddHours(-10));
        Assert.True(PackagedPluginCache.Validate(_directory));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(900)]
    public void A_single_changed_plugin_timestamp_is_not_a_timezone_shift(int seconds)
    {
        var file = Path.Combine(_directory, "plugins", "codec", "libtest_plugin.dll");
        File.SetLastWriteTimeUtc(file, File.GetLastWriteTimeUtc(file).AddSeconds(seconds));
        Assert.Throws<PlaybackException>(() => PackagedPluginCache.Validate(_directory));
    }

    [Fact]
    public void Development_folder_without_manifest_keeps_normal_discovery()
    {
        File.Delete(Path.Combine(_directory, "plugins", "lmp-cache.json"));
        Assert.False(PackagedPluginCache.Validate(_directory));
    }

    [Theory]
    [InlineData("plugins/codec/libtest_plugin.dll")]
    [InlineData("plugins/plugins.dat")]
    [InlineData("libvlccore.dll")]
    public void Changed_bundle_fails_with_reextract_guidance(string path)
    {
        var file = Path.Combine(_directory, path);
        var bytes = File.ReadAllBytes(file); bytes[0] ^= 1; File.WriteAllBytes(file, bytes);
        File.SetLastWriteTimeUtc(file, File.GetLastWriteTimeUtc(file).AddSeconds(5));
        Assert.Contains("extract", Assert.Throws<PlaybackException>(() => PackagedPluginCache.Validate(_directory)).Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Missing_or_extra_plugin_is_rejected()
    {
        File.Delete(Path.Combine(_directory, "plugins", "codec", "libtest_plugin.dll"));
        Assert.Throws<PlaybackException>(() => PackagedPluginCache.Validate(_directory));
        File.WriteAllText(Path.Combine(_directory, "plugins", "codec", "libextra_plugin.dll"), "unexpected");
        Assert.Throws<PlaybackException>(() => PackagedPluginCache.Validate(_directory));
    }

    [Fact]
    public void Malformed_manifest_is_reported_as_a_playback_error()
    {
        File.WriteAllText(Path.Combine(_directory, "plugins", "lmp-cache.json"), "{broken");
        Assert.Throws<PlaybackException>(() => PackagedPluginCache.Validate(_directory));
    }

    public void Dispose() => Directory.Delete(_directory, true);
}
