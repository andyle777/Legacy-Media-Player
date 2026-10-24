namespace LegacyMediaPlayer.Core;

/// <summary>A validated local SRT; validation performs I/O and belongs on a worker.</summary>
public sealed record SubtitleFile
{
    private SubtitleFile(string path) => Path = path;
    public string Path { get; }
    public Uri Uri => new(Path);

    public static SubtitleFile FromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var normalized = path.Replace('/', '\\');
        if (!System.IO.Path.IsPathFullyQualified(path) || normalized.StartsWith("\\\\", StringComparison.Ordinal) ||
            path.Any(c => char.IsControl(c) || c is '<' or '>' or '"' or '|' or '?' or '*') ||
            path.IndexOf(':', 2) >= 0 || !System.IO.Path.GetExtension(path).Equals(".srt", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Choose a local SRT subtitle using its full path.", nameof(path));
        var full = System.IO.Path.GetFullPath(path);
        if (full.Replace('/', '\\').StartsWith("\\\\", StringComparison.Ordinal))
            throw new ArgumentException("Network and device subtitle paths are not supported.", nameof(path));
        using var input = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (input.Length == 0) throw new InvalidDataException("The subtitle file is empty.");
        return new(full);
    }
}

/// <summary>A current-item choice, not a persisted preference. Native IDs are input-scoped.</summary>
public sealed record SubtitleSelection
{
    private SubtitleSelection(int? id, string? name, SubtitleFile? file) { TrackId = id; TrackName = name; File = file; }
    public int? TrackId { get; }
    public string? TrackName { get; }
    public SubtitleFile? File { get; }
    public IReadOnlyList<SubtitleFile> ExternalFiles { get; private init; } = Array.Empty<SubtitleFile>();
    public static SubtitleSelection Off { get; } = new(-1, null, null);
    public static SubtitleSelection Track(int id, string? name) => id < 0 ? throw new ArgumentOutOfRangeException(nameof(id)) : new(id, name, null);
    public static SubtitleSelection External(SubtitleFile file) => new(null, null, file ?? throw new ArgumentNullException(nameof(file)));
    public SubtitleSelection ForReplay() => TrackId is >= 0 ? new(null, TrackName, null) : this;
    public SubtitleSelection WithFiles(IEnumerable<SubtitleFile> files) => this with { ExternalFiles = Array.AsReadOnly(files.Distinct().ToArray()) };
}
