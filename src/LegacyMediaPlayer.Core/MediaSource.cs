namespace LegacyMediaPlayer.Core;

public sealed record MediaSource
{
    private MediaSource(string path) => Path = path;
    public string Path { get; }
    public Uri Uri => new(Path);

    public static MediaSource FromFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !System.IO.Path.IsPathFullyQualified(path))
            throw new ArgumentException("Choose a local file using its full path.", nameof(path));
        var fullPath = System.IO.Path.GetFullPath(path);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("The media file was not found.", fullPath);
        return new(fullPath);
    }
}
