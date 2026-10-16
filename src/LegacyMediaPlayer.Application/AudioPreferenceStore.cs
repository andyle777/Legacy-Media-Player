using System.Text.Json;

namespace LegacyMediaPlayer.Application;

public sealed record AudioPreferences(int Volume = 75, bool IsMuted = false);

/// <summary>Small per-user settings file. Settings failures never prevent playback.</summary>
public sealed class AudioPreferenceStore(string path)
{
    public AudioPreferences Load()
    {
        try
        {
            var preferences = JsonSerializer.Deserialize<AudioPreferences>(File.ReadAllText(path));
            return preferences is { Volume: >= 0 and <= 100 } ? preferences : new();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        { return new(); }
    }

    public void Save(AudioPreferences preferences)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(temporary, JsonSerializer.Serialize(preferences));
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { /* Read-only profiles still get a fully usable player. */ }
        finally
        {
            try { File.Delete(temporary); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }
}
