using System.Text;

namespace LegacyMediaPlayer.Application;

public sealed record PlaylistReadResult(IReadOnlyList<string> Paths, int EntryCount, int SkippedCount);

public interface IPlaylistFileService
{
    Task<PlaylistReadResult> LoadAsync(string path, CancellationToken cancellationToken = default);
    Task SaveAsync(string path, IReadOnlyList<string> paths, bool relativePaths, CancellationToken cancellationToken = default);
}

/// <summary>Bounded, local-only M3U I/O. Does not touch playback or enumerate directories.</summary>
public sealed class PlaylistFileService : IPlaylistFileService
{
    private const int MaximumBytes = 4 * 1024 * 1024;
    private const int MaximumEntries = 10000;
    private const int MaximumLineLength = 32768;
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public Task<PlaylistReadResult> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        Task.Run(() => Read(path, cancellationToken), cancellationToken);

    public Task SaveAsync(string path, IReadOnlyList<string> paths, bool relativePaths, CancellationToken cancellationToken = default)
    {
        var snapshot = paths.ToArray();
        return Task.Run(() => Write(path, snapshot, relativePaths, cancellationToken), cancellationToken);
    }

    private static PlaylistReadResult Read(string path, CancellationToken token)
    {
        path = PlaylistPath(path);
        token.ThrowIfCancellationRequested();
        byte[] bytes;
        using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            if (input.Length > MaximumBytes) throw new InvalidDataException("The playlist exceeds the 4 MiB limit.");
            bytes = new byte[(int)input.Length];
            input.ReadExactly(bytes);
        }
        string text;
        try { text = Utf8.GetString(bytes); }
        catch (DecoderFallbackException ex)
        {
            if (!Path.GetExtension(path).Equals(".m3u", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("M3U8 playlists must contain valid UTF-8 text.", ex);
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            text = Encoding.GetEncoding(0).GetString(bytes); // Windows ANSI for legacy M3U only.
        }
        var accepted = new List<string>();
        var count = 0;
        var skipped = 0;
        using var lines = new StringReader(text.TrimStart('\uFEFF'));
        while (lines.ReadLine() is { } line)
        {
            token.ThrowIfCancellationRequested();
            if (line.Length > MaximumLineLength) throw new InvalidDataException("A playlist line exceeds the 32 KiB limit.");
            var entry = line.Trim();
            if (entry.Length == 0 || entry.StartsWith('#')) continue;
            if (++count > MaximumEntries) throw new InvalidDataException("The playlist exceeds the 10,000 entry limit.");
            try
            {
                if (!TryLocalPath(entry, Path.GetDirectoryName(path)!, out var full)) { skipped++; continue; }
                // Validate readability without invoking codecs, and without writing source files.
                using var media = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                accepted.Add(full);
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
            { skipped++; }
        }
        return new(accepted, count, skipped);
    }

    private static void Write(string path, string[] paths, bool relativePaths, CancellationToken token)
    {
        path = PlaylistPath(path);
        if (paths.Length > MaximumEntries) throw new InvalidDataException("The queue exceeds the 10,000 entry playlist limit.");
        var directory = Path.GetDirectoryName(path)!;
        var text = new StringBuilder("#EXTM3U\n");
        foreach (var source in paths)
        {
            token.ThrowIfCancellationRequested();
            if (!Path.IsPathFullyQualified(source) || !TryLocalPath(source, directory, out var full))
                throw new InvalidDataException("Only supported absolute local media paths can be saved.");
            var entry = relativePaths ? Path.GetRelativePath(directory, full) : full;
            // Do not let a relative filename become a comment, or lose leading spaces.
            if (!Path.IsPathFullyQualified(entry)) entry = "." + Path.DirectorySeparatorChar + entry;
            if (entry.Length > MaximumLineLength) throw new InvalidDataException("A playlist path exceeds the 32 KiB limit.");
            text.Append(entry).Append('\n');
            if (text.Length > MaximumBytes) throw new InvalidDataException("The playlist exceeds the 4 MiB limit.");
        }
        var bytes = Utf8.GetBytes(text.ToString());
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("The playlist exceeds the 4 MiB limit.");
        token.ThrowIfCancellationRequested();
        Directory.CreateDirectory(directory);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                output.Write(bytes);
                output.Flush(flushToDisk: true);
            }
            token.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            try { File.Delete(temporary); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    private static string PlaylistPath(string path)
    {
        if (!Path.IsPathFullyQualified(path) || path.StartsWith("\\\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal))
            throw new ArgumentException("Choose a local playlist using its full path.", nameof(path));
        var full = Path.GetFullPath(path);
        if (full.StartsWith("\\\\", StringComparison.Ordinal) || full.StartsWith("//", StringComparison.Ordinal))
            throw new ArgumentException("Choose a local playlist using its full path.", nameof(path));
        if (!Path.GetExtension(full).Equals(".m3u", StringComparison.OrdinalIgnoreCase) &&
            !Path.GetExtension(full).Equals(".m3u8", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Choose an M3U or M3U8 playlist.");
        return full;
    }

    private static bool TryLocalPath(string entry, string directory, out string full)
    {
        full = string.Empty;
        if (entry.StartsWith("\\\\", StringComparison.Ordinal) || entry.StartsWith("//", StringComparison.Ordinal) ||
            entry.Any(c => char.IsControl(c) || c is '<' or '>' or '"' or '|' or '?' or '*')) return false;
        var colon = entry.IndexOf(':');
        if (colon >= 0 && (colon != 1 || !char.IsAsciiLetter(entry[0]) || !Path.IsPathFullyQualified(entry) || entry.IndexOf(':', 2) >= 0)) return false;
        if (Path.IsPathRooted(entry) && !Path.IsPathFullyQualified(entry)) return false;
        full = Path.GetFullPath(entry, directory);
        return !full.StartsWith("\\\\", StringComparison.Ordinal) && !full.StartsWith("//", StringComparison.Ordinal) &&
            LocalMediaDrop.IsSupportedPath(full);
    }
}
