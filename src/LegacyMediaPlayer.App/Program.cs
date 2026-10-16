using Avalonia;

namespace LegacyMediaPlayer.App;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("Milestone 0 supports Windows 10/11 x64. Linux and Android are planned; macOS is not a target.");
            return 2;
        }
        return AppBuilder.Configure<App>().UseWin32().UseSkia().LogToTrace().StartWithClassicDesktopLifetime(args);
    }
}
