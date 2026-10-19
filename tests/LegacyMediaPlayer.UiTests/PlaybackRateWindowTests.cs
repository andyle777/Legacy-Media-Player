using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using LegacyMediaPlayer.App;
using LegacyMediaPlayer.Application;
using Xunit;

namespace LegacyMediaPlayer.UiTests;

public partial class PlayerWindowTests
{
    [AvaloniaFact]
    public void Focused_selector_keeps_global_previous_next_and_mute_shortcuts()
    {
        using var files = new DropTestFiles("one.wav", "two.wav");
        using var engine = new MockPlaybackEngine(); var window = new MainWindow(engine, true);
        try
        {
            window.Show(); window.OpenFiles([files[0], files[1]]);
            var selector = window.FindControl<ComboBox>("RateSelector")!; selector.Focus();
            Press(window, PhysicalKey.ArrowRight, RawInputModifiers.Control);
            Assert.Equal(files[1], engine.Snapshot.Media!.Path);
            Press(window, PhysicalKey.ArrowLeft, RawInputModifiers.Control);
            Assert.Equal(files[0], engine.Snapshot.Media!.Path);
            Press(window, PhysicalKey.M); Assert.True(engine.Snapshot.IsMuted);
            bool handled = false;
            window.AddHandler(InputElement.KeyDownEvent, (_, e) => handled = e.Handled,
                Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
            foreach (var (key, modifiers) in new[]
            {
                (PhysicalKey.O, RawInputModifiers.Control),
                (PhysicalKey.A, RawInputModifiers.Control | RawInputModifiers.Shift),
                (PhysicalKey.O, RawInputModifiers.Control | RawInputModifiers.Shift)
            })
            {
                handled = false; selector.Focus(); Press(window, key, modifiers); Assert.True(handled);
            }
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Rate_restore_selection_and_close_persist_without_loading_native_playback()
    {
        var path = Path.GetTempFileName();
        try
        {
            var store = new AudioPreferenceStore(path); store.Save(new(37, true, RepeatMode.All, true, 0.75f));
            var creates = 0;
            using var deferred = new DeferredPlaybackEngine(() => { creates++; return new MockPlaybackEngine(); }, new(false, false, false));
            var window = new MainWindow(deferred, false, deferred.InitializeAsync, preferences: store.Load(), savePlaybackPreferences: store.Save);
            try
            {
                window.Show(); Dispatcher.UIThread.RunJobs();
                var selector = window.FindControl<ComboBox>("RateSelector")!;
                Assert.Equal(1, selector.SelectedIndex);
                selector.SelectedIndex = 5; Dispatcher.UIThread.RunJobs();
                Assert.Equal(2f, deferred.Snapshot.Rate); Assert.Equal(0, creates);
                window.Close(); Assert.Equal(new AudioPreferences(37, true, RepeatMode.All, true, 2), store.Load());
            }
            finally { window.Close(); }
        }
        finally { File.Delete(path); }
    }

    [AvaloniaFact]
    public void Rejected_rate_resets_selector_and_shows_warning_without_stopping_playback()
    {
        using var file = new DropTestFiles("audio.wav"); using var engine = new VideoEventsEngine();
        var window = new MainWindow(engine, false);
        try
        {
            window.Show(); window.OpenFile(file[0]);
            engine.RejectRateRequests();
            window.FindControl<ComboBox>("RateSelector")!.SelectedIndex = 5;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(2, window.FindControl<ComboBox>("RateSelector")!.SelectedIndex);
            Assert.Equal(1f, engine.Snapshot.Rate);
            Assert.Equal(LegacyMediaPlayer.Core.PlaybackState.Playing, engine.Snapshot.State);
            Assert.True(window.FindControl<TextBlock>("ErrorLabel")!.IsVisible);
            Assert.Equal("Unavailable", window.FindControl<TextBlock>("RateStatusLabel")!.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Selector_keyboard_does_not_trigger_transport_volume_or_seek_shortcuts()
    {
        using var engine = new MockPlaybackEngine(); var window = new MainWindow(engine, true);
        try
        {
            window.Show(); var selector = window.FindControl<ComboBox>("RateSelector")!;
            selector.Focus(); var volume = engine.Snapshot.Volume;
            Press(window, PhysicalKey.ArrowDown); Press(window, PhysicalKey.Space);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(volume, engine.Snapshot.Volume);
            Assert.Equal(LegacyMediaPlayer.Core.PlaybackState.Empty, engine.Snapshot.State);
            Assert.False(window.FindControl<TextBlock>("ErrorLabel")!.IsVisible);
            Press(window, PhysicalKey.Escape);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Non_normal_rate_survives_drop_append_replace_and_fullscreen()
    {
        using var files = new DropTestFiles("first.mp3", "video.mp4", "second.flac");
        using var engine = new VideoEventsEngine(); var window = new MainWindow(engine, false);
        try
        {
            window.Show(); window.OpenFile(files[0]); window.FindControl<ComboBox>("RateSelector")!.SelectedIndex = 4;
            Click(window, "PauseButton");
            using var append = await DropData(window, files[1]); DropAt(window, "QueuePanel", append);
            Assert.Equal(LegacyMediaPlayer.Core.PlaybackState.Paused, engine.Snapshot.State);
            Assert.Equal(1.5f, engine.Snapshot.Rate);
            using var replace = await DropData(window, files[1], files[2]); DropAt(window, "VideoBorder", replace);
            engine.VideoOutput(true, true); Dispatcher.UIThread.RunJobs();
            Press(window, PhysicalKey.F11); Assert.Equal(WindowState.FullScreen, window.WindowState);
            Press(window, PhysicalKey.Escape); Assert.NotEqual(WindowState.FullScreen, window.WindowState);
            Assert.Equal(1.5f, engine.Snapshot.Rate); Assert.Equal(4, window.FindControl<ComboBox>("RateSelector")!.SelectedIndex);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Speed_selector_is_accessible_and_does_not_initialize_an_empty_player()
    {
        var creates = 0;
        using var engine = new DeferredPlaybackEngine(() => { creates++; return new MockPlaybackEngine(); }, new(false, false, false));
        var window = new MainWindow(engine, false, engine.InitializeAsync) { Width = 900 };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var selector = window.FindControl<ComboBox>("RateSelector");
            Assert.NotNull(selector);
            Assert.Equal(6, selector.Items.Count);
            Assert.Equal(2, selector.SelectedIndex);
            Assert.Equal("Playback speed", Avalonia.Automation.AutomationProperties.GetName(selector));
            selector.SelectedIndex = 5; Dispatcher.UIThread.RunJobs();
            Assert.Equal(0, creates);
            Assert.Empty(window.FindControl<ListBox>("QueueList")!.Items);
        }
        finally { window.Close(); }
    }
}
