using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using LegacyMediaPlayer.Application;
using LegacyMediaPlayer.Core;
using LegacyMediaPlayer.Playback.LibVLC;

namespace LegacyMediaPlayer.App;

public partial class App : Avalonia.Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mock = desktop.Args?.Contains("--mock", StringComparer.OrdinalIgnoreCase) == true;
            try
            {
                IPlaybackEngine engine = mock ? new MockPlaybackEngine() : new LibVlcPlaybackEngine();
                try
                {
                    var main = new MainWindow(engine, mock);
                    desktop.MainWindow = main;
                    var paths = desktop.Args?.Where(a => !a.Equals("--mock", StringComparison.OrdinalIgnoreCase)).ToArray() ?? [];
                    if (paths.Length > 0) main.OpenFiles(paths);
                }
                catch { engine.Dispose(); throw; }
            }
            catch (PlaybackException ex)
            {
                desktop.MainWindow = CreateStartupError(desktop, ex.Message);
            }
        }
        base.OnFrameworkInitializationCompleted();
    }

    private static Window CreateStartupError(IClassicDesktopStyleApplicationLifetime desktop, string message)
    {
        var window = new Window { Title = "Legacy Media Player — startup", Width = 560, Height = 250 };
        var mockButton = new Button { Content = "Start mock mode" };
        mockButton.Click += (_, _) =>
        {
            var main = new MainWindow(new MockPlaybackEngine(), true);
            desktop.MainWindow = main;
            main.Show();
            window.Close();
        };
        window.Content = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 18,
            Children =
            {
                new TextBlock { Text = "Playback engine unavailable", FontSize = 20 },
                new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                mockButton
            }
        };
        return window;
    }
}
