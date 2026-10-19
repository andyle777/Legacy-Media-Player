using Avalonia;

namespace LegacyMediaPlayer.App;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        StartupTiming.Mark("managed.entry");
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("Milestone 0 supports Windows 10/11 x64. Linux and Android are planned; macOS is not a target.");
            return 2;
        }
        if (args is ["--generate-plugin-cache"]) return GeneratePluginCache();
        if (args is ["--verify-plugin-cache"]) return VerifyPluginCache();
        StartupTiming.Mark("avalonia.start");
        return AppBuilder.Configure<App>().UseWin32().UseSkia().LogToTrace().StartWithClassicDesktopLifetime(args);
    }
    // Package smoke check: same cache policy/backend as playback, no window/preferences.
    private static int VerifyPluginCache()
    {
        try
        {
            var usedCache = false;
            using var engine = new LegacyMediaPlayer.Playback.LibVLC.LibVlcPlaybackEngine(
                ["--aout=dummy", "--vout=dummy"], stage => usedCache |= stage == "libvlc.plugins.cache-only");
            return usedCache ? 0 : 1;
        }
        catch (LegacyMediaPlayer.Core.PlaybackException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
    // Packaging maintenance only. No Avalonia window or user preferences are loaded.
    private static int GeneratePluginCache()
    {
        try
        {
            using var cache = new LegacyMediaPlayer.Playback.LibVLC.LibVlcPlaybackEngine(
                ["--reset-plugins-cache", "--aout=dummy", "--vout=dummy"]);
            var path = Path.Combine(AppContext.BaseDirectory, "libvlc", "win-x64", "plugins", "plugins.dat");
            return File.Exists(path) && new FileInfo(path).Length > 0 ? 0 : 1;
        }
        catch (LegacyMediaPlayer.Core.PlaybackException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}
