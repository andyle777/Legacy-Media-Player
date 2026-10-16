using System.Diagnostics;
using System.Text.Json;

namespace LegacyMediaPlayer.App;

// Opt-in file diagnostics; never puts timings or local paths in the player UI.
internal static class StartupTiming
{
    private static readonly string? LogPath = Environment.GetEnvironmentVariable("LEGACY_MEDIA_PLAYER_STARTUP_LOG");
    private static readonly long ManagedStart = Stopwatch.GetTimestamp();
    private static readonly object Gate = new();
    private static readonly Lazy<DateTime> ProcessStart = new(GetProcessStart);

    public static void Mark(string stage)
    {
        if (string.IsNullOrWhiteSpace(LogPath)) return;
        Write(stage, LogPath);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void Write(string stage, string path)
    {
        try
        {
            var line = JsonSerializer.Serialize(new
            {
                pid = Environment.ProcessId,
                stage,
                processMs = Math.Round((DateTime.UtcNow - ProcessStart.Value).TotalMilliseconds, 3),
                managedMs = Math.Round(Stopwatch.GetElapsedTime(ManagedStart).TotalMilliseconds, 3),
                thread = Environment.CurrentManagedThreadId
            });
            lock (Gate) File.AppendAllText(path, line + Environment.NewLine);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { /* Diagnostics must never prevent startup or playback. */ }
    }

    private static DateTime GetProcessStart()
    {
        using var process = Process.GetCurrentProcess();
        return process.StartTime.ToUniversalTime();
    }
}
