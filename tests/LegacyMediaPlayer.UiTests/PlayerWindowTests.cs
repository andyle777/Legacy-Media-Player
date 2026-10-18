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

public partial class PlayerWindowTests
{
    [AvaloniaTheory]
    [InlineData(PhysicalKey.F11, RawInputModifiers.None)]
    [InlineData(PhysicalKey.Enter, RawInputModifiers.Alt)]
    public void Fullscreen_shortcuts_preserve_paused_media_preferences_and_queue(PhysicalKey key, RawInputModifiers modifiers)
    {
        var path = Path.GetTempFileName();
        using var engine = new VideoEventsEngine();
        var window = new MainWindow(engine, false);
        try
        {
            window.Show(); window.OpenFiles([path, path]);
            engine.VideoOutput(true, true);
            engine.Seek(TimeSpan.FromSeconds(27)); engine.SetVolume(39); engine.SetMuted(true);
            Click(window, "PauseButton"); Dispatcher.UIThread.RunJobs();
            var before = engine.Snapshot;
            var size = window.ClientSize;
            var position = window.Position;
            window.FindControl<Button>("PlayButton")!.Focus();
            Press(window, key, modifiers);
            Assert.Equal(WindowState.FullScreen, window.WindowState);
            Assert.False(window.FindControl<ListBox>("QueueList")!.IsEffectivelyVisible);
            Assert.False(window.FindControl<Button>("OpenButton")!.IsEffectivelyVisible);
            Assert.Equal(before, engine.Snapshot);
            Assert.Equal(2, window.FindControl<ListBox>("QueueList")!.ItemCount);
            Press(window, PhysicalKey.ArrowRight);
            Assert.Equal(TimeSpan.FromSeconds(32), engine.Snapshot.Position);
            Press(window, PhysicalKey.Space); Assert.Equal(PlaybackState.Playing, engine.Snapshot.State);
            Press(window, PhysicalKey.Space); Assert.Equal(PlaybackState.Paused, engine.Snapshot.State);
            var afterSeek = engine.Snapshot;
            Press(window, PhysicalKey.Escape);
            Assert.Equal(WindowState.Normal, window.WindowState);
            Assert.True(window.FindControl<ListBox>("QueueList")!.IsEffectivelyVisible);
            Assert.Equal(size, window.ClientSize);
            Assert.Equal(position, window.Position);
            Assert.Equal(afterSeek, engine.Snapshot);
        }
        finally { window.Close(); File.Delete(path); }
    }

    [AvaloniaFact]
    public void Fullscreen_toggle_restores_maximized_window_and_closes_without_changing_media()
    {
        var path = Path.GetTempFileName();
        using var engine = new VideoEventsEngine();
        var window = new MainWindow(engine, false);
        try
        {
            window.Show(); window.OpenFile(path); engine.VideoOutput(true, true);
            Dispatcher.UIThread.RunJobs(); window.WindowState = WindowState.Maximized;
            Press(window, PhysicalKey.F11); Assert.Equal(WindowState.FullScreen, window.WindowState);
            Press(window, PhysicalKey.F11); Assert.Equal(WindowState.Maximized, window.WindowState);
            Press(window, PhysicalKey.Enter, RawInputModifiers.Alt);
            Assert.Equal(WindowState.FullScreen, window.WindowState);
            window.Close();
            Assert.Throws<ObjectDisposedException>(engine.Play);
            engine.VideoOutput(true, true); Dispatcher.UIThread.RunJobs();
        }
        finally { window.Close(); File.Delete(path); }
    }

    [AvaloniaFact]
    public void Fullscreen_exits_when_video_is_replaced_by_audio_and_audio_cannot_enter()
    {
        var path = Path.GetTempFileName();
        using var engine = new VideoEventsEngine();
        var window = new MainWindow(engine, false);
        try
        {
            window.Show(); window.OpenFile(path);
            Press(window, PhysicalKey.F11); Assert.Equal(WindowState.Normal, window.WindowState);
            engine.VideoOutput(true, true); Dispatcher.UIThread.RunJobs();
            Press(window, PhysicalKey.F11); Assert.Equal(WindowState.FullScreen, window.WindowState);
            window.OpenFile(path); engine.Seek(TimeSpan.FromSeconds(1)); Dispatcher.UIThread.RunJobs();
            Assert.Equal(WindowState.Normal, window.WindowState);
            Assert.True(window.FindControl<TextBlock>("Placeholder")!.IsEffectivelyVisible);
            Press(window, PhysicalKey.Enter, RawInputModifiers.Alt);
            Assert.Equal(WindowState.Normal, window.WindowState);
            Assert.Equal(PlaybackState.Playing, engine.Snapshot.State);
        }
        finally { window.Close(); File.Delete(path); }
    }

    [AvaloniaFact]
    public void Fullscreen_toggle_does_not_repeat_until_key_is_released()
    {
        var path = Path.GetTempFileName();
        using var engine = new VideoEventsEngine();
        var window = new MainWindow(engine, false);
        try
        {
            window.Show(); window.OpenFile(path); engine.VideoOutput(true, true);
            Dispatcher.UIThread.RunJobs();
            window.KeyPressQwerty(PhysicalKey.F11, RawInputModifiers.None);
            Assert.Equal(WindowState.FullScreen, window.WindowState);
            window.KeyPressQwerty(PhysicalKey.F11, RawInputModifiers.None);
            Assert.Equal(WindowState.FullScreen, window.WindowState);
            window.KeyReleaseQwerty(PhysicalKey.F11, RawInputModifiers.None);
            Press(window, PhysicalKey.F11);
            Assert.Equal(WindowState.Normal, window.WindowState);
        }
        finally { window.Close(); File.Delete(path); }
    }

    [AvaloniaFact]
    public async Task Fullscreen_cursor_hides_after_idle_and_returns_on_motion_and_exit()
    {
        var path = Path.GetTempFileName();
        using var engine = new VideoEventsEngine();
        var window = new MainWindow(engine, false);
        try
        {
            window.Show(); window.Activate(); window.OpenFile(path); engine.VideoOutput(true, true);
            Dispatcher.UIThread.RunJobs();
            var original = window.Cursor;
            Press(window, PhysicalKey.F11);
            await Task.Delay(TimeSpan.FromSeconds(4));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("None", window.Cursor?.ToString());
            window.MouseMove(new Point(200, 200));
            Assert.NotEqual("None", window.Cursor?.ToString());
            Press(window, PhysicalKey.Escape);
            Assert.Same(original, window.Cursor);
        }
        finally { window.Close(); File.Delete(path); }
    }

    [AvaloniaFact]
    public void Fullscreen_survives_new_video_discovery_and_exits_when_audio_progresses()
    {
        var path = Path.GetTempFileName();
        using var engine = new VideoEventsEngine();
        var window = new MainWindow(engine, false);
        try
        {
            window.Show(); window.OpenFiles([path, path]); engine.VideoOutput(true, true);
            Dispatcher.UIThread.RunJobs(); Press(window, PhysicalKey.F11);
            Assert.Equal(WindowState.FullScreen, window.WindowState);
            Press(window, PhysicalKey.ArrowRight, RawInputModifiers.Control);
            // New input is briefly unknown, then reports video; do not leave fullscreen.
            Assert.Equal(WindowState.FullScreen, window.WindowState);
            engine.VideoOutput(true, true); Dispatcher.UIThread.RunJobs();
            Assert.Equal(WindowState.FullScreen, window.WindowState);
            Assert.True(window.FindControl<VideoHost>("VideoSurface")!.IsEffectivelyVisible);
            window.OpenFile(path); // This replacement represents audio: no video stream arrives.
            engine.Seek(TimeSpan.FromSeconds(1)); Dispatcher.UIThread.RunJobs();
            Assert.Equal(WindowState.Normal, window.WindowState);
            Assert.Equal(PlaybackState.Playing, engine.Snapshot.State);
        }
        finally { window.Close(); File.Delete(path); }
    }

    [AvaloniaFact]
    public void Fullscreen_keys_on_empty_window_do_not_initialize_native_engine()
    {
        var creates = 0;
        using var engine = new DeferredPlaybackEngine(() => { creates++; return new MockPlaybackEngine(); }, new(true, true, true));
        var window = new MainWindow(engine, false);
        try
        {
            window.Show();
            Press(window, PhysicalKey.F11); Press(window, PhysicalKey.Enter, RawInputModifiers.Alt);
            Press(window, PhysicalKey.Escape);
            Assert.Equal(WindowState.Normal, window.WindowState);
            Assert.Equal(0, creates);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Fullscreen_video_double_click_toggles_without_interrupting_playback()
    {
        var path = Path.GetTempFileName();
        using var engine = new VideoEventsEngine();
        var window = new MainWindow(engine, false);
        try
        {
            window.Show(); window.OpenFile(path); engine.VideoOutput(true, true);
            Dispatcher.UIThread.RunJobs();
            var video = window.FindControl<VideoHost>("VideoSurface")!;
            var point = video.TranslatePoint(new Point(100, 100), window)!.Value;
            window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left);
            window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left);
            Assert.Equal(WindowState.FullScreen, window.WindowState);
            Assert.Equal(PlaybackState.Playing, engine.Snapshot.State);
        }
        finally { window.Close(); File.Delete(path); }
    }

    [AvaloniaFact]
    public void Empty_video_capable_player_shows_placeholder_without_loading_native_backend()
    {
        var creates = 0;
        using var engine = new DeferredPlaybackEngine(() =>
        {
            creates++;
            return new MockPlaybackEngine();
        }, new(true, true, true));
        var window = new MainWindow(engine, false);
        try
        {
            window.Show();
            Assert.False(window.FindControl<VideoHost>("VideoSurface")!.IsVisible);
            Assert.True(window.FindControl<TextBlock>("Placeholder")!.IsVisible);
            Assert.Equal(0, creates);
        }
        finally { window.Close(); }
    }


    [AvaloniaFact]
    public void Video_host_is_visible_before_output_creation_and_survives_pause_switch_and_late_dispatch()
    {
        var path = Path.GetTempFileName();
        using var engine = new VideoEventsEngine();
        var window = new MainWindow(engine, false);
        try
        {
            window.Show();
            window.OpenFile(path);
            engine.VideoOutput(true, false);
            Dispatcher.UIThread.RunJobs();
            // LibVLC may need a visible, laid-out HWND to create its first output.
            Assert.True(window.FindControl<VideoHost>("VideoSurface")!.IsVisible);
            engine.VideoOutput(true, true);
            Dispatcher.UIThread.RunJobs();
            Assert.True(window.FindControl<VideoHost>("VideoSurface")!.IsVisible);
            Click(window, "PauseButton");
            Assert.True(window.FindControl<VideoHost>("VideoSurface")!.IsVisible);
            // Leave an old video notification queued, then replace with audio.
            engine.VideoOutput(true, true);
            window.OpenFile(path);
            Dispatcher.UIThread.RunJobs();
            Assert.False(window.FindControl<VideoHost>("VideoSurface")!.IsVisible);
            Assert.True(window.FindControl<TextBlock>("Placeholder")!.IsVisible);
            engine.VideoOutput(true, true);
            Dispatcher.UIThread.RunJobs();
            Click(window, "StopButton");
            Assert.False(window.FindControl<VideoHost>("VideoSurface")!.IsVisible);
            window.Close();
            engine.VideoOutput(true, true); // An already queued external notification is harmless after close.
            Dispatcher.UIThread.RunJobs();
        }
        finally { window.Close(); File.Delete(path); }
    }

    [AvaloniaFact]
    public void Recovered_native_warning_clears_but_independent_picker_error_survives()
    {
        using var engine = new VideoEventsEngine();
        using var session = new PlayerSession(engine, a => a());
        engine.ReportNativeError(new("video-output-failed", "Video could not be rendered."));
        Assert.Equal("Video could not be rendered.", session.ErrorMessage);
        engine.ReportNativeError(null);
        Assert.Null(session.ErrorMessage);
        engine.ReportNativeError(new("video-output-failed", "Video could not be rendered."));
        session.ReportError("Picker failed.");
        engine.ReportNativeError(null);
        Assert.Equal("Picker failed.", session.ErrorMessage);
    }

    // Substitute only the external native events; the real session, dispatcher and UI remain in use.
    private sealed class VideoEventsEngine : IPlaybackEngine
    {
        private readonly MockPlaybackEngine _inner = new();
        private bool _video, _output;
        private PlaybackError? _error;
        public VideoEventsEngine() => _inner.Changed += (_, _) => Changed?.Invoke(this, new(Snapshot));
        public PlaybackCapabilities Capabilities { get; } = new(true, false, false);
        public PlaybackSnapshot Snapshot => _inner.Snapshot with { HasVideo = _video, HasVideoOutput = _output, Error = _error };
        public event EventHandler<PlaybackChangedEventArgs>? Changed;
        public void ReportNativeError(PlaybackError? error) { _error = error; Changed?.Invoke(this, new(Snapshot)); }
        public void VideoOutput(bool video, bool output) { _video = video; _output = output; Changed?.Invoke(this, new(Snapshot)); }
        public void Open(MediaSource source) { _video = _output = false; _inner.Open(source); }
        public void Play() => _inner.Play();
        public void Pause() => _inner.Pause();
        public void Stop() { _output = false; _inner.Stop(); }
        public void Seek(TimeSpan position) => _inner.Seek(position);
        public void SetVolume(int volume) => _inner.SetVolume(volume);
        public void SetMuted(bool muted) => _inner.SetMuted(muted);
        public IReadOnlyList<PlaybackTrack> GetTracks() => [];
        public void SelectTrack(PlaybackTrackKind kind, int id) => throw new NotSupportedException();
        public void AttachVideoSurface(VideoSurface? surface) { }
        public void Dispose() => _inner.Dispose();
    }

    [AvaloniaFact]
    public void Open_and_add_shortcuts_are_handled_by_the_window()
    {
        var window = new MainWindow(new MockPlaybackEngine(), true);
        try
        {
            window.Show();
            window.FindControl<Button>("OpenButton")!.Focus();
            bool handled = false;
            window.AddHandler(InputElement.KeyDownEvent, (_, e) => handled = e.Handled,
                Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
            Press(window, PhysicalKey.O, RawInputModifiers.Control);
            Assert.True(handled);
            handled = false;
            Press(window, PhysicalKey.O, RawInputModifiers.Control | RawInputModifiers.Shift);
            Assert.True(handled);
            handled = false;
            Press(window, PhysicalKey.A, RawInputModifiers.Control | RawInputModifiers.Shift);
            Assert.True(handled);
        }
        finally { window.Close(); }
    }

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
            // Keep the fake backend blocked until the test releases it, even on a
            // loaded desktop. The separate entry/completion waits still bound responsiveness.
            Assert.True(release.Wait(TimeSpan.FromSeconds(30)));
            return backend;
        }, new(false, false, false));
        var window = new MainWindow(deferred, false, deferred.InitializeAsync);
        try
        {
            window.Show();
            var open = window.OpenFilesAsync([path]);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(open.IsCompleted, window.FindControl<TextBlock>("ErrorLabel")!.Text);
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
