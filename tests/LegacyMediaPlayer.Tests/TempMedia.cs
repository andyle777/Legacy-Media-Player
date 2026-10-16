namespace LegacyMediaPlayer.Tests;

internal sealed class TempMedia : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"legacy-{Guid.NewGuid():N}.wav");
    public TempMedia() => File.WriteAllBytes(Path, [0]);
    public void Dispose() => File.Delete(Path);
}
