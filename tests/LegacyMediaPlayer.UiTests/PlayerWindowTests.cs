using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LegacyMediaPlayer.App;
using LegacyMediaPlayer.Application;
using LegacyMediaPlayer.Core;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(LegacyMediaPlayer.UiTests.TestAppBuilder))]

namespace LegacyMediaPlayer.UiTests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<LegacyMediaPlayer.App.App>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

public class PlayerWindowTests
{
    [AvaloniaFact]
    public void Dragging_seek_thumb_commits_the_new_position()
    {
        WithPlayer((window, engine) =>
        {
            var slider = window.FindControl<Slider>("SeekSlider")!;
            var thumb = slider.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.Thumb>().Single();
            var point = thumb.TranslatePoint(new Point(thumb.Bounds.Width / 2, thumb.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseMove(point + new Vector(140, 0));
            window.MouseUp(point + new Vector(140, 0), MouseButton.Left);
            Assert.True(engine.Snapshot.Position > TimeSpan.FromSeconds(20));
            Assert.InRange(Math.Abs(engine.Snapshot.Position.TotalSeconds - slider.Value), 0, 0.1);
        });
    }

    [AvaloniaFact]
    public void Arrow_up_commits_the_keyboard_seek()
    {
        WithPlayer((window, engine) =>
        {
            var slider = window.FindControl<Slider>("SeekSlider")!;
            slider.Focus();
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            Assert.True(engine.Snapshot.Position > TimeSpan.Zero);
            Assert.Equal(slider.Value, engine.Snapshot.Position.TotalSeconds);
        });
    }

    private static void WithPlayer(Action<MainWindow, MockPlaybackEngine> action)
    {
        var file = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"legacy-ui-{Guid.NewGuid():N}.wav");
        File.WriteAllBytes(file, [0]);
        var engine = new MockPlaybackEngine();
        var window = new MainWindow(engine, true);
        try
        {
            window.Show();
            engine.Open(MediaSource.FromFile(file));
            Dispatcher.UIThread.RunJobs();
            action(window, engine);
        }
        finally { window.Close(); File.Delete(file); }
    }
}
