using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using LegacyMediaPlayer.Core;

[assembly: InternalsVisibleTo("LegacyMediaPlayer.NativeTests")]

namespace LegacyMediaPlayer.Playback.LibVLC;

internal static class PackagedPluginCache
{
    private sealed record Fingerprint(long Length, long Modified, string Sha256);

    // Packaging SHA-verifies every native file. At runtime, hash the native runtime
    // and cache, then use VLC's plugin cache when plugin mtimes match or their
    // content hashes confirm extraction only rewrote timestamps. Avoid hashing
    // every codec binary on the normal path.
    // This is consistency checking, not a signed security boundary.
    internal static bool Validate(string directory)
    {
        var manifest = Path.Combine(directory, "plugins", "lmp-cache.json");
        if (!File.Exists(manifest)) return false; // Unpackaged developer/test builds.
        try
        {
            var hashes = JsonSerializer.Deserialize<Dictionary<string, Fingerprint>>(File.ReadAllText(manifest));
            var files = new[] { "libvlc.dll", "libvlccore.dll", "plugins/plugins.dat" }
                .Select(p => new FileInfo(Path.Combine(directory, p)))
                .Concat(new DirectoryInfo(Path.Combine(directory, "plugins")).EnumerateFiles("*.dll", SearchOption.AllDirectories)).ToArray();
            if (hashes is null || hashes.Count != files.Length || files.Length <= 3)
                throw new InvalidDataException("Native inventory differs.");
            long? timezoneShift = null;
            var pluginTimestampsMatch = true;
            foreach (var file in files)
            {
                var relative = Path.GetRelativePath(directory, file.FullName).Replace('\\', '/');
                if (!hashes.TryGetValue(relative, out var expected) || expected is null || file.Length != expected.Length || file.Length == 0)
                    throw new InvalidDataException("Native inventory differs.");
                if (relative.StartsWith("plugins/", StringComparison.Ordinal) && file.Extension.Equals(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    var shift = new DateTimeOffset(file.LastWriteTimeUtc).ToUnixTimeSeconds() - expected.Modified;
                    // Modern timezone offsets are multiples of 15 minutes. Do not
                    // trust plugin timestamps outside a uniform bounded timezone shift.
                    if (shift < -26 * 60 * 60 || shift > 26 * 60 * 60 || shift % (15 * 60) != 0 ||
                        (timezoneShift is { } previous && shift != previous))
                        pluginTimestampsMatch = false;
                    timezoneShift ??= shift;
                }
                else
                {
                    using var stream = file.OpenRead();
                    if (!Convert.ToHexString(SHA256.HashData(stream)).Equals(expected.Sha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Native content differs.");
                }
            }
            if (!pluginTimestampsMatch)
            {
                foreach (var file in files)
                {
                    var relative = Path.GetRelativePath(directory, file.FullName).Replace('\\', '/');
                    if (!relative.StartsWith("plugins/", StringComparison.Ordinal) ||
                        !file.Extension.Equals(".dll", StringComparison.OrdinalIgnoreCase))
                        continue;
                    using var stream = file.OpenRead();
                    if (!Convert.ToHexString(SHA256.HashData(stream)).Equals(hashes[relative].Sha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Native plugin content differs.");
                }
            }
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            throw new PlaybackException("The bundled LibVLC cache or native files are missing, changed or unreadable. Extract the complete player ZIP into a new folder and try again.", ex);
        }
    }
}
