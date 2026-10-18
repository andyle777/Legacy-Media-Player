using LegacyMediaPlayer.Core;

namespace LegacyMediaPlayer.Application;

// File selection only: no decoding, source writes, directory traversal or queue mutation.
public sealed record LocalMediaDrop(IReadOnlyList<string> Paths, int SkippedCount)
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp3", ".wav", ".flac", ".mp4", ".mkv", ".webm", ".avi" };

    public static bool IsSupportedPath(string? path) => !string.IsNullOrWhiteSpace(path) &&
        Path.IsPathFullyQualified(path) && Extensions.Contains(Path.GetExtension(path));

    public static LocalMediaDrop Select(IEnumerable<string?> paths)
    {
        var accepted = new List<string>();
        var skipped = 0;
        foreach (var path in paths)
        {
            try
            {
                if (!IsSupportedPath(path)) { skipped++; continue; }
                accepted.Add(MediaSource.FromFile(path!).Path);
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
            { skipped++; }
        }
        // QueueItem identity already supports deliberate duplicate paths; retain source order.
        return new(accepted, skipped);
    }
}
