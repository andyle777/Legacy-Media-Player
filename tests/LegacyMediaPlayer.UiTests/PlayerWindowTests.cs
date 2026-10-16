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
    public void Queue_arrow_navigation_is_preserved_and_audio_preferences_save_on_close()
    {
        var settings = Path.GetTempFileName();
        var store = new AudioPreferenceStore(settings);
        var engine = new MockPlaybackEngine();
        var window = new MainWindow(engine, true, savePreferences: s => store.Save(new(s.Volume, s.IsMuted)));
        try
        {
            window.Show();
            window.OpenFiles([settings, settings]);
            var queue = window.FindControl<ListBox>("QueueList")!;
            queue.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();
            queue.GetVisualDescendants().OfType<ListBoxItem>().First().Focus();
            Press(window, PhysicalKey.ArrowDown);
            Assert.Equal(1, queue.SelectedIndex);
            Assert.Equal(75, engine.Snapshot.Volume);
            window.FindControl<Slider>("VolumeSlider")!.Value = 43;
            Click(window, "MuteButton");
            window.Close();
            Assert.Equal(new AudioPreferences(43, true), store.Load());
        }
        finally { window.Close(); File.Delete(settings); }
    }

    [AvaloniaFact]
    public void Player_shortcuts_control_transport_queue_seek_volume_and_mute()
    {
        WithPlayer((window, engine) =>
        {
            var path = engine.Snapshot.Media!.Path;
            window.OpenFiles([path, path]);
            window.FindControl<Button>("PlayButton")!.Focus();
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Assert.Equal(PlaybackState.Paused, engine.Snapshot.State);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Assert.Equal(PlaybackState.Paused, engine.Snapshot.State);
            Press(window, PhysicalKey.Space);
            Assert.Equal(PlaybackState.Playing, engine.Snapshot.State);
            engine.Seek(TimeSpan.FromSeconds(20));
            Dispatcher.UIThread.RunJobs();
            Press(window, PhysicalKey.ArrowLeft);
            Assert.Equal(TimeSpan.FromSeconds(15), engine.Snapshot.Position);
            Press(window, PhysicalKey.ArrowRight);
            Assert.Equal(TimeSpan.FromSeconds(20), engine.Snapshot.Position);
            Press(window, PhysicalKey.ArrowUp);
            Assert.Equal(80, engine.Snapshot.Volume);
            Press(window, PhysicalKey.ArrowDown);
            Assert.Equal(75, engine.Snapshot.Volume);
            Press(window, PhysicalKey.M);
            Assert.True(engine.Snapshot.IsMuted);
            Press(window, PhysicalKey.ArrowRight, RawInputModifiers.Control);
            Assert.StartsWith("Playing · 2/2", window.FindControl<TextBlock>("StatusLabel")!.Text);
            Press(window, PhysicalKey.ArrowLeft, RawInputModifiers.Control);
            Assert.StartsWith("Playing · 1/2", window.FindControl<TextBlock>("StatusLabel")!.Text);
        });
    }

    private static void Press(MainWindow window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
    }

    [AvaloniaFact]
    public async Task First_open_keeps_window_responsive_and_closing_does_not_wait_for_native_startup()
    {
        var path = Path.GetTempFileName();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        using var backend = new MockPlaybackEngine();
        using var deferred = new DeferredPlaybackEngine(() =>
        {
            entered.SetResult();
            Assert.True(release.Wait(TimeSpan.FromSeconds(5)));
            return backend;
        }, new(false, false, false));
        var window = new MainWindow(deferred, false, deferred.InitializeAsync);
        try
        {
            window.Show();
            var open = window.OpenFilesAsync([path]);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(open.IsCompleted);
            Assert.Equal("Starting playback engine…", window.FindControl<TextBlock>("StatusLabel")!.Text);
            Assert.False(window.FindControl<Button>("OpenButton")!.IsEnabled);
            window.FindControl<Slider>("VolumeSlider")!.Value = 36;
            window.Close();
            release.Set();
            await open.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Throws<ObjectDisposedException>(() => backend.SetVolume(1));
        }
        finally { release.Set(); window.Close(); File.Delete(path); }
    }

    [AvaloniaFact]
    public async Task Missing_file_does_not_load_native_codecs_and_failed_native_startup_is_visible()
    {
        var creates = 0;
        var path = Path.GetTempFileName();
        using var deferred = new DeferredPlaybackEngine(() =>
        {
            creates++;
            throw new PlaybackException("Bundled decoder could not load.");
        }, new(false, false, false));
        var window = new MainWindow(deferred, false, deferred.InitializeAsync);
        try
        {
            window.Show();
            await window.OpenFilesAsync([path + ".missing"]);
            Assert.Equal(0, creates);
            await window.OpenFilesAsync([path]);
            Assert.Equal(1, creates);
            Assert.Contains("Bundled decoder could not load", window.FindControl<TextBlock>("ErrorLabel")!.Text);
            Assert.True(window.FindControl<Button>("OpenButton")!.IsEnabled);
        }
        finally { window.Close(); File.Delete(path); }
    }

    [AvaloniaFact]
    public void Queue_selection_editing_and_transport_control_the_intended_items()
    {
        WithPlayer((window, engine) =>
        {
            var path = engine.Snapshot.Media!.Path;
            window.OpenFile(path);
            window.AddFiles([path, path]);
            var queue = window.FindControl<ListBox>("QueueList")!;
            Assert.Equal(3, queue.ItemCount);
            queue.SelectedIndex = 2;
            Click(window, "MoveUpButton");
            Assert.Equal(1, queue.SelectedIndex);
            Click(window, "PlaySelectedButton");
            Assert.StartsWith("Playing · 2/3", window.FindControl<TextBlock>("StatusLabel")!.Text);
            Click(window, "PreviousButton");
            Assert.StartsWith("Playing · 1/3", window.FindControl<TextBlock>("StatusLabel")!.Text);
            Click(window, "NextButton");
            queue.SelectedIndex = 0; Click(window, "RemoveButton");
            Assert.Equal(2, queue.ItemCount);
            Assert.Equal(PlaybackState.Playing, engine.Snapshot.State);
            queue.SelectedIndex = 0; Click(window, "RemoveButton");
            Assert.Equal(PlaybackState.Stopped, engine.Snapshot.State);
            Assert.True(window.FindControl<Button>("PlayButton")!.IsEnabled);
            Click(window, "PlayButton");
            Assert.StartsWith("Playing · 1/1", window.FindControl<TextBlock>("StatusLabel")!.Text);
        });
    }

    private static void Click(MainWindow window, string name) => window.FindControl<Button>(name)!
        .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

    [AvaloniaFact]
    public void Audio_open_and_transport_update_visible_status_and_dispose_on_close()
    {
        WithPlayer((window, engine) =>
        {
            window.OpenFile(engine.Snapshot.Media!.Path);
            Assert.Equal(PlaybackState.Playing, engine.Snapshot.State);
            Assert.Contains(System.IO.Path.GetFileName(engine.Snapshot.Media.Path), window.FindControl<TextBlock>("StatusLabel")!.Text);
            Assert.Equal("0:00 / 3:00", window.FindControl<TextBlock>("TimeLabel")!.Text);
            window.FindControl<Button>("PauseButton")!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(PlaybackState.Paused, engine.Snapshot.State);
            window.FindControl<Slider>("VolumeSlider")!.Value = 42;
            window.FindControl<Button>("MuteButton")!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(42, engine.Snapshot.Volume);
            Assert.True(engine.Snapshot.IsMuted);
            Assert.Equal("Unmute", window.FindControl<Button>("MuteButton")!.Content);
            window.FindControl<Button>("PlayButton")!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(PlaybackState.Playing, engine.Snapshot.State);
            window.FindControl<Button>("StopButton")!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(PlaybackState.Stopped, engine.Snapshot.State);
            window.Close();
            Assert.Throws<ObjectDisposedException>(engine.Play);
        });
    }

    [AvaloniaFact]
    public void Right_click_released_outside_seek_bar_does_not_freeze_position()
    {
        WithPlayer((window, engine) =>
        {
            var slider = window.FindControl<Slider>("SeekSlider")!;
            var point = slider.TranslatePoint(new Point(slider.Bounds.Width / 2, slider.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Right);
            window.MouseMove(new Point(5, 5));
            window.MouseUp(new Point(5, 5), MouseButton.Right);
            engine.Seek(TimeSpan.FromSeconds(42));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(42, slider.Value);
        });
    }

    [AvaloniaFact]
    public void Hovering_seek_bar_does_not_freeze_playback_position()
    {
        WithPlayer((window, engine) =>
        {
            var slider = window.FindControl<Slider>("SeekSlider")!;
            var point = slider.TranslatePoint(new Point(slider.Bounds.Width / 2, slider.Bounds.Height / 2), window)!.Value;
            window.MouseMove(point);
            Assert.True(slider.IsPointerOver);
            engine.Seek(TimeSpan.FromSeconds(42));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(42, slider.Value);
        });
    }

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
