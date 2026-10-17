using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LegacyMediaPlayer.Application;
using LegacyMediaPlayer.Core;

namespace LegacyMediaPlayer.App;

public partial class MainWindow : Window
{
    private readonly PlayerSession _session;
    private bool _refreshing;
    private bool _closed;
    private bool _seeking;
    private readonly Func<Task>? _preparePlayback;
    private bool _opening;
    private bool _spaceHandled;

    // The XAML runtime/designer can create a window without loading native codecs.
    public MainWindow() : this(new MockPlaybackEngine(), true) { }

    public MainWindow(IPlaybackEngine engine, bool mock, Func<Task>? preparePlayback = null, Action<PlaybackSnapshot>? savePreferences = null)
    {
        _preparePlayback = preparePlayback;
        StartupTiming.Mark("window.xaml.begin");
        InitializeComponent();
        StartupTiming.Mark("window.xaml.end");
        Opened += (_, _) =>
        {
            StartupTiming.Mark("window.opened");
            RequestAnimationFrame(_ =>
            {
                StartupTiming.Mark("window.first-frame");
                if (Environment.GetEnvironmentVariable("LEGACY_MEDIA_PLAYER_STARTUP_EXIT_AFTER_FRAME") == "1")
                    Dispatcher.UIThread.Post(Close);
            });
        };
        _session = new PlayerSession(engine, action => Dispatcher.UIThread.Post(action));
        AddHandler(KeyDownEvent, OnPlayerKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, (_, e) =>
        {
            // Focused buttons also activate on Space release; consume that release
            // when the player handled the press so transport does not fire twice.
            if (e.Key == Key.Space && _spaceHandled) { e.Handled = true; _spaceHandled = false; }
        }, RoutingStrategies.Tunnel);
        QueueList.ItemsSource = _session.Queue;
        ModeLabel.Text = mock ? "Mock mode (no decoding)" : "LibVLC · Local playback";
        VideoSurface.Engine = engine;
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
            savePreferences?.Invoke(_session.Snapshot);
            VideoSurface.Release();
            _session.Dispose();
        };
        Refresh();
    }

    private void OnOpen(object? sender, RoutedEventArgs e) => PickFiles(false);
    private void OnAdd(object? sender, RoutedEventArgs e) => PickFiles(true);
    private async void PickFiles(bool append)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new()
            {
                Title = append ? "Add local media to queue" : "Open local media",
                AllowMultiple = true,
                FileTypeFilter =
                [
                    new FilePickerFileType("Local media") { Patterns = ["*.mp3", "*.wav", "*.flac", "*.mp4", "*.mkv", "*.webm", "*.avi"] },
                    new FilePickerFileType("Video (MP4, MKV, WebM, AVI)") { Patterns = ["*.mp4", "*.mkv", "*.webm", "*.avi"] },
                    new FilePickerFileType("Audio (MP3, WAV, FLAC)") { Patterns = ["*.mp3", "*.wav", "*.flac"] },
                    FilePickerFileTypes.All
                ]
            });
            if (!_closed && files.Count > 0)
            {
                var paths = files.Select(f => f.TryGetLocalPath() ?? string.Empty).ToArray();
                if (append) AddFiles(paths); else OpenFiles(paths);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            if (!_closed) _session.ReportError($"Could not open the file picker: {ex.Message}");
        }
    }

    public void OpenFile(string path) => OpenFiles([path]);
    public void OpenFiles(IEnumerable<string> paths) => _ = OpenFilesAsync(paths);
    public void AddFiles(IEnumerable<string> paths) => _ = OpenFilesAsync(paths, append: true);

    public async Task OpenFilesAsync(IEnumerable<string> paths, bool append = false)
    {
        if (_closed || _opening) return;
        try
        {
            var files = paths.ToArray();
            if (files.Length == 0) return;
            // Reject missing/invalid files without starting native codecs.
            foreach (var path in files) _ = MediaSource.FromFile(path);
            _opening = true;
            Refresh();
            StartupTiming.Mark("playback.prepare.begin");
            if (_preparePlayback is not null) await _preparePlayback();
            StartupTiming.Mark("playback.prepare.end");
            if (_closed) return;
            if (append) _session.AddFiles(files); else _session.OpenFiles(files);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException or PlaybackException)
        {
            if (!_closed) _session.ReportError($"Playback could not start: {ex.Message}");
        }
        finally { _opening = false; Refresh(); }
    }

    private void OnPrevious(object? sender, RoutedEventArgs e) => _session.Previous();
    private void OnNext(object? sender, RoutedEventArgs e) => _session.Next();
    private void OnPlaySelected(object? sender, RoutedEventArgs e) => _session.PlayAt(QueueList.SelectedIndex);
    private void OnQueueDoubleTap(object? sender, TappedEventArgs e) => _session.PlayAt(QueueList.SelectedIndex);
    private void OnRemove(object? sender, RoutedEventArgs e)
    {
        var index = QueueList.SelectedIndex;
        _session.RemoveAt(index);
        QueueList.SelectedIndex = Math.Min(index, _session.Queue.Count - 1);
    }
    private void OnMoveUp(object? sender, RoutedEventArgs e) => MoveSelected(-1);
    private void OnMoveDown(object? sender, RoutedEventArgs e) => MoveSelected(1);
    private void MoveSelected(int delta)
    {
        var from = QueueList.SelectedIndex;
        var to = from + delta;
        if (from < 0 || to < 0 || to >= _session.Queue.Count) return;
        _session.MoveItem(from, to);
        QueueList.SelectedIndex = to;
    }
    private void OnQueueSelection(object? sender, SelectionChangedEventArgs e)
    {
        // XAML may raise selection events before the session has been assigned.
        if (_session is not null) RefreshQueueButtons();
    }
    private void RefreshQueueButtons()
    {
        var selected = QueueList.SelectedIndex;
        PlaySelectedButton.IsEnabled = RemoveButton.IsEnabled = selected >= 0;
        MoveUpButton.IsEnabled = selected > 0;
        MoveDownButton.IsEnabled = selected >= 0 && selected < _session.Queue.Count - 1;
    }

    private void OnPlayerKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || _closed || _opening) return;
        var control = e.Source as Control;
        // Text entry and native slider/list navigation keep their normal keys.
        bool Within<T>() where T : Control => control is T || control?.FindAncestorOfType<T>() is not null;
        if (Within<TextBox>()) return;
        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.O) PickFiles(false);
        else if (e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift) && e.Key is Key.O or Key.A) PickFiles(true);
        else if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.Right) _session.Next();
        else if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.Left) _session.Previous();
        else if (e.KeyModifiers != KeyModifiers.None) return;
        else if (e.Key == Key.Space)
        {
            if (!_spaceHandled)
            {
                if (_session.Snapshot.State == PlaybackState.Playing) _session.Pause(); else _session.Play();
                _spaceHandled = true;
            }
        }
        else if (e.Key == Key.M) _session.SetMuted(!_session.Snapshot.IsMuted);
        else if (Within<Slider>() || Within<ListBox>()) return;
        else if (e.Key is Key.Left or Key.Right && _session.Snapshot.CanSeek)
        {
            var seconds = _session.Snapshot.Position.TotalSeconds + (e.Key == Key.Left ? -5 : 5);
            _session.Seek(TimeSpan.FromSeconds(Math.Max(0, seconds)));
        }
        else if (e.Key is Key.Up or Key.Down)
            _session.SetVolume(Math.Clamp(_session.Snapshot.Volume + (e.Key == Key.Up ? 5 : -5), 0, 100));
        else return; // Fullscreen keys remain reserved; video is outside this change.
        e.Handled = true;
    }

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
            // Show/layout the HWND when a video stream is discovered. Waiting for
            // Vout would deadlock outputs that need a visible host before allocation.
            VideoSurface.IsVisible = snapshot.HasVideo && snapshot.State is PlaybackState.Ready or PlaybackState.Playing or PlaybackState.Paused;
            Placeholder.IsVisible = !VideoSurface.IsVisible;
            OpenButton.IsEnabled = AddButton.IsEnabled = !_opening;
            PlayButton.IsEnabled = !_opening && _session.Queue.Count > 0;
            PreviousButton.IsEnabled = _session.CanPrevious;
            NextButton.IsEnabled = _session.CanNext;
            QueueLabel.Text = _session.CurrentIndex >= 0
                ? $"Queue · current {_session.CurrentIndex + 1}/{_session.Queue.Count}"
                : $"Queue · {_session.Queue.Count} tracks";
            RefreshQueueButtons();
            PauseButton.IsEnabled = snapshot.State == PlaybackState.Playing;
            StopButton.IsEnabled = snapshot.Media is not null;
            SeekSlider.IsEnabled = snapshot.CanSeek;
            SeekSlider.Maximum = Math.Max(1, snapshot.Duration.TotalSeconds);
            if (!_seeking) SeekSlider.Value = snapshot.Position.TotalSeconds;
            VolumeSlider.Value = snapshot.Volume;
            MuteButton.Content = snapshot.IsMuted ? "Unmute" : "Mute";
            TimeLabel.Text = $"{FormatTime(snapshot.Position)} / {FormatTime(snapshot.Duration)}";
            StatusLabel.Text = _opening ? "Starting playback engine…" : snapshot.Media is { } media
                ? $"{snapshot.State} · {(_session.CurrentIndex >= 0 ? $"{_session.CurrentIndex + 1}/{_session.Queue.Count} · " : "")}{System.IO.Path.GetFileName(media.Path)}"
                : "Ready · Open a local media file";
            ErrorLabel.Text = _session.ErrorMessage;
            ErrorLabel.IsVisible = !string.IsNullOrEmpty(_session.ErrorMessage);
        }
        finally { _refreshing = false; }
    }

    private static string FormatTime(TimeSpan time) => time.TotalHours >= 1
        ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
        : $"{(int)time.TotalMinutes}:{time.Seconds:00}";
}
