using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LegacyMediaPlayer.Application;
using LegacyMediaPlayer.Core;

namespace LegacyMediaPlayer.App;

public partial class MainWindow : Window
{
    private readonly PlayerSession _session;
    private bool _refreshing;
    private bool _closed;
    private bool _seeking;

    // The XAML runtime/designer can create a window without loading native codecs.
    public MainWindow() : this(new MockPlaybackEngine(), true) { }

    public MainWindow(IPlaybackEngine engine, bool mock)
    {
        InitializeComponent();
        _session = new PlayerSession(engine, action => Dispatcher.UIThread.Post(action));
        ModeLabel.Text = mock ? "Mock mode (no decoding)" : "LibVLC · Local playback";
        VideoSurface.Engine = engine;
        VideoSurface.IsVisible = engine.Capabilities.Video;
        Placeholder.IsVisible = !engine.Capabilities.Video;
        _session.PropertyChanged += (_, _) => Refresh();
        // Slider's thumb consumes release events; commit after handled drag events too.
        SeekSlider.AddHandler(InputElement.PointerReleasedEvent, OnSeekReleased,
            RoutingStrategies.Bubble, handledEventsToo: true);
        SeekSlider.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
        {
            if (e.GetCurrentPoint(SeekSlider).Properties.IsLeftButtonPressed) _seeking = true;
        },
            RoutingStrategies.Tunnel, handledEventsToo: true);
        SeekSlider.PointerCaptureLost += (_, _) => _seeking = false;
        VolumeSlider.PropertyChanged += (_, e) =>
        {
            if (!_refreshing && e.Property == RangeBase.ValueProperty)
                _session.SetVolume((int)VolumeSlider.Value);
        };
        Closing += (_, e) =>
        {
            if (e.Cancel) return;
            _closed = true;
            VideoSurface.Release();
            _session.Dispose();
        };
        Refresh();
    }

    private async void OnOpen(object? sender, RoutedEventArgs e)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new()
            {
                Title = "Open local audio",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Audio (MP3, WAV, FLAC)") { Patterns = ["*.mp3", "*.wav", "*.flac"] },
                    FilePickerFileTypes.All
                ]
            });
            if (!_closed && files.Count > 0) OpenFile(files[0].TryGetLocalPath() ?? string.Empty);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            if (!_closed) _session.ReportError($"Could not open the file picker: {ex.Message}");
        }
    }

    public void OpenFile(string path) => _session.OpenAndPlayFile(path);

    private void OnPlay(object? sender, RoutedEventArgs e) => _session.Play();
    private void OnPause(object? sender, RoutedEventArgs e) => _session.Pause();
    private void OnStop(object? sender, RoutedEventArgs e) => _session.Stop();
    private void OnMute(object? sender, RoutedEventArgs e) => _session.SetMuted(!_session.Snapshot.IsMuted);
    private void OnSeekReleased(object? sender, PointerReleasedEventArgs e)
    {
        _seeking = false;
        Seek();
    }
    private void OnSeekKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown) Seek();
    }
    private void Seek()
    {
        if (!_refreshing && _session.Snapshot.CanSeek)
            _session.Seek(TimeSpan.FromSeconds(SeekSlider.Value));
    }

    private void Refresh()
    {
        if (_closed) return;
        var snapshot = _session.Snapshot;
        _refreshing = true;
        try
        {
            PlayButton.IsEnabled = snapshot.Media is not null;
            PauseButton.IsEnabled = snapshot.State == PlaybackState.Playing;
            StopButton.IsEnabled = snapshot.Media is not null;
            SeekSlider.IsEnabled = snapshot.CanSeek;
            SeekSlider.Maximum = Math.Max(1, snapshot.Duration.TotalSeconds);
            if (!_seeking) SeekSlider.Value = snapshot.Position.TotalSeconds;
            VolumeSlider.Value = snapshot.Volume;
            MuteButton.Content = snapshot.IsMuted ? "Unmute" : "Mute";
            TimeLabel.Text = $"{FormatTime(snapshot.Position)} / {FormatTime(snapshot.Duration)}";
            StatusLabel.Text = snapshot.Media is { } media
                ? $"{snapshot.State} · {System.IO.Path.GetFileName(media.Path)}"
                : "Ready · Open a local audio file";
            ErrorLabel.Text = _session.ErrorMessage;
            ErrorLabel.IsVisible = !string.IsNullOrEmpty(_session.ErrorMessage);
        }
        finally { _refreshing = false; }
    }

    private static string FormatTime(TimeSpan time) => time.TotalHours >= 1
        ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
        : $"{(int)time.TotalMinutes}:{time.Seconds:00}";
}
