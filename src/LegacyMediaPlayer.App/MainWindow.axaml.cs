using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Collections.Specialized;
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
    private bool _fullscreen;
    private bool _fullscreenToggleHeld;
    private WindowState _restoreState;
    private Size _restoreSize;
    private PixelPoint _restorePosition;
    private readonly DispatcherTimer _cursorIdleTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private Cursor? _hiddenCursor;
    private Cursor? _restoreCursor;

    // The XAML runtime/designer can create a window without loading native codecs.
    public MainWindow() : this(new MockPlaybackEngine(), true) { }

    public MainWindow(IPlaybackEngine engine, bool mock, Func<Task>? preparePlayback = null, Action<PlaybackSnapshot>? savePreferences = null,
        AudioPreferences? preferences = null, Action<AudioPreferences>? savePlaybackPreferences = null,
        IPlaylistFileService? playlistFiles = null, string? sessionPlaylistPath = null,
        Func<string, MediaSource>? validatePlaylistFile = null, Func<Task<string?>>? pickSubtitlePath = null)
    {
        _preparePlayback = preparePlayback;
        _playlistFiles = playlistFiles ?? new PlaylistFileService();
        _sessionPlaylistPath = sessionPlaylistPath;
        _validatePlaylistFile = validatePlaylistFile;
        _pickSubtitlePath = pickSubtitlePath;
        StartupTiming.Mark("window.xaml.begin");
        InitializeComponent();
        StartupTiming.Mark("window.xaml.end");
        _cursorIdleTimer.Tick += (_, _) =>
        {
            _cursorIdleTimer.Stop();
            if (_fullscreen && IsActive && !_closed)
                Cursor = _hiddenCursor ??= new Cursor(StandardCursorType.None);
        };
        AddHandler(PointerMovedEvent, (_, _) => RestartFullscreenCursor(), RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, (_, _) => RestartFullscreenCursor(), RoutingStrategies.Tunnel);
        Activated += (_, _) => RestartFullscreenCursor();
        Deactivated += (_, _) =>
        {
            _cursorIdleTimer.Stop();
            if (_fullscreen) Cursor = _restoreCursor;
            _fullscreenToggleHeld = _spaceHandled = false;
        };
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
        InitializeSubtitles();
        ((INotifyCollectionChanged)_session.Queue).CollectionChanged += (_, _) => { if (!_restoringSession) _queueEdited = true; };
        RateSelector.ItemsSource = new[] { "0.5×", "0.75×", "1.0× (Normal)", "1.25×", "1.5×", "2.0×" };
        RateSelector.SelectionChanged += (_, _) =>
        {
            if (!_refreshing && !_closed && RateSelector.SelectedIndex >= 0)
                _session.SetRate(PlaybackRates.Supported[RateSelector.SelectedIndex]);
        };
        if (preferences is { } saved)
        {
            _session.SetVolume(saved.Volume); _session.SetMuted(saved.IsMuted);
            _session.SetRepeatMode(saved.Repeat); _session.SetShuffleEnabled(saved.ShuffleEnabled);
            _session.SetRate(saved.Rate);
        }
        AddHandler(KeyDownEvent, OnPlayerKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, (_, e) =>
        {
            if (e.Key is Key.F11 or Key.Enter && _fullscreenToggleHeld)
            {
                _fullscreenToggleHeld = false;
                e.Handled = true;
            }
            // Focused buttons also activate on Space release; consume that release
            // when the player handled the press so transport does not fire twice.
            if (e.Key == Key.Space && _spaceHandled) { e.Handled = true; _spaceHandled = false; }
        }, RoutingStrategies.Tunnel);
        QueueList.ItemsSource = _session.Queue;
        ModeLabel.Text = mock ? "Mock mode (no decoding)" : "LibVLC · Local playback";
        VideoSurface.Engine = engine;
        InitializeMediaDrop();
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
        Closing += OnPersistQueueClosing;
        Closing += (_, e) =>
        {
            if (e.Cancel || _closed) return;
            _closed = true;
            _playlistLifetime.Cancel();
            _playlistLifetime.Dispose();
            _cursorIdleTimer.Stop();
            if (_fullscreen) Cursor = _restoreCursor;
            _hiddenCursor?.Dispose();
            savePreferences?.Invoke(_session.Snapshot);
            savePlaybackPreferences?.Invoke(new(_session.Snapshot.Volume, _session.Snapshot.IsMuted,
                _session.Repeat, _session.ShuffleEnabled, _session.Snapshot.Rate));
            VideoSurface.Release();
            _session.Dispose();
        };
        Refresh();
    }

    private void OnOpen(object? sender, RoutedEventArgs e) => PickFiles(false);
    private void OnAdd(object? sender, RoutedEventArgs e) => PickFiles(true);
    private async void PickFiles(bool append)
    {
        if (PlaylistBusy) return;
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
        if (PlaylistBusy) return;
        try
        {
            StartupTiming.Mark("playback.request");
            var files = paths.ToArray();
            if (files.Length == 0) return;
            // Reject missing/invalid files without starting native codecs.
            StartupTiming.Mark("playback.validation.begin");
            foreach (var path in files) _ = MediaSource.FromFile(path);
            StartupTiming.Mark("playback.validation.end");
            _opening = true;
            Refresh();
            StartupTiming.Mark("playback.prepare.begin");
            if (_preparePlayback is not null) await _preparePlayback();
            StartupTiming.Mark("playback.prepare.end");
            if (_closed || _savingSession) return;
            if (append) _session.AddFiles(files); else _session.OpenFiles(files);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException or PlaybackException)
        {
            if (!_closed && !_savingSession) _session.ReportError($"Playback could not start: {ex.Message}");
        }
        finally { _opening = false; Refresh(); }
    }

    private void OnPrevious(object? sender, RoutedEventArgs e) { if (_session.CanPrevious) _ = RunTransportAsync(_session.Previous); }
    private void OnNext(object? sender, RoutedEventArgs e) { if (_session.CanNext) _ = RunTransportAsync(_session.Next); }
    private void OnPlaySelected(object? sender, RoutedEventArgs e)
    { var index = QueueList.SelectedIndex; if (index >= 0) _ = RunTransportAsync(() => _session.PlayAt(index)); }
    private void OnQueueDoubleTap(object? sender, TappedEventArgs e) => OnPlaySelected(sender, e);
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
        PlaySelectedButton.IsEnabled = RemoveButton.IsEnabled = !PlaylistBusy && selected >= 0;
        MoveUpButton.IsEnabled = !PlaylistBusy && selected > 0;
        MoveDownButton.IsEnabled = !PlaylistBusy && selected >= 0 && selected < _session.Queue.Count - 1;
    }

    private void OnPlayerKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || _closed) return;
        if (e.Key == Key.Escape && _fullscreen)
        {
            ExitFullscreen(); e.Handled = true; return;
        }
        if ((e.Key == Key.F11 && e.KeyModifiers == KeyModifiers.None) ||
            (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.Alt))
        {
            if (!_fullscreenToggleHeld)
            {
                _fullscreenToggleHeld = true;
                ToggleFullscreen();
            }
            e.Handled = true; return;
        }
        if (PlaylistBusy) return;
        var control = e.Source as Control;
        // Text entry and native slider/list navigation keep their normal keys.
        bool Within<T>() where T : Control => control is T || control?.FindAncestorOfType<T>() is not null;
        if (Within<TextBox>()) return;
        // Let the speed selector keep Space/arrows/Enter/Escape and popup focus.
        if ((Within<ComboBox>() || Within<ComboBoxItem>()) && e.KeyModifiers == KeyModifiers.None &&
            e.Key is Key.Space or Key.Enter or Key.Escape or Key.Left or Key.Right or Key.Up or Key.Down or
                Key.Home or Key.End or Key.PageUp or Key.PageDown) return;
        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.O) PickFiles(false);
        else if (e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift) && e.Key is Key.O or Key.A) PickFiles(true);
        else if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.Right) OnNext(sender, e);
        else if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.Left) OnPrevious(sender, e);
        else if (e.KeyModifiers != KeyModifiers.None) return;
        else if (e.Key == Key.Space)
        {
            if (!_spaceHandled)
            {
                if (_session.Snapshot.State == PlaybackState.Playing) _session.Pause(); else _ = RunTransportAsync(_session.Play);
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
        else return;
        e.Handled = true;
    }

    private void OnVideoDoubleTap(object? sender, TappedEventArgs e)
    {
        ToggleFullscreen();
        e.Handled = true;
    }

    private void ToggleFullscreen()
    {
        if (_fullscreen) { ExitFullscreen(); return; }
        if (_opening || !VideoSurface.IsVisible) return;
        _restoreState = WindowState;
        _restoreSize = ClientSize;
        _restorePosition = Position;
        _restoreCursor = Cursor;
        _fullscreen = true;
        SetFullscreenLayout(true);
        WindowState = WindowState.FullScreen;
        VideoSurface.Focus();
        RestartFullscreenCursor();
    }

    private void ExitFullscreen()
    {
        if (!_fullscreen) return;
        _fullscreen = false;
        _cursorIdleTimer.Stop();
        Cursor = _restoreCursor;
        SetFullscreenLayout(false);
        WindowState = WindowState.Normal;
        if (_restoreState == WindowState.Normal)
        {
            Width = _restoreSize.Width;
            Height = _restoreSize.Height;
            Position = _restorePosition;
        }
        else WindowState = _restoreState;
        PlayButton.Focus();
    }

    private void RestartFullscreenCursor()
    {
        _cursorIdleTimer.Stop();
        if (!_fullscreen) return;
        Cursor = _restoreCursor;
        if (IsActive && !_closed) _cursorIdleTimer.Start();
    }

    private void SetFullscreenLayout(bool fullscreen)
    {
        OpenPanel.IsVisible = QueuePanel.IsVisible = SeekPanel.IsVisible =
            TransportPanel.IsVisible = StatusPanel.IsVisible = !fullscreen;
        PlayerLayout.Margin = fullscreen ? new Thickness(0) : new Thickness(10);
        VideoBorder.BorderThickness = fullscreen ? new Thickness(0) : new Thickness(2);
        MediaLayout.ColumnDefinitions[1].Width = fullscreen ? new GridLength(0) : new GridLength(310);
        // Keep the same NativeControlHost in the visual tree: no reparent, Stop or reopen.
    }

    private void OnPlay(object? sender, RoutedEventArgs e) => _ = RunTransportAsync(_session.Play);
    private void OnPause(object? sender, RoutedEventArgs e) => _session.Pause();
    private void OnStop(object? sender, RoutedEventArgs e) => _session.Stop();
    private void OnMute(object? sender, RoutedEventArgs e) => _session.SetMuted(!_session.Snapshot.IsMuted);
    private void OnRepeat(object? sender, RoutedEventArgs e) => _session.SetRepeatMode(_session.Repeat switch
    {
        RepeatMode.Off => RepeatMode.All,
        RepeatMode.All => RepeatMode.One,
        _ => RepeatMode.Off
    });
    private void OnShuffle(object? sender, RoutedEventArgs e) => _session.SetShuffleEnabled(!_session.ShuffleEnabled);
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
            // During Open/Next, stream discovery is briefly unknown. Keep fullscreen
            // through that gap; an audio timestamp/pause identifies a non-video input.
            if (_fullscreen && (snapshot.State is PlaybackState.Empty or PlaybackState.Stopped or PlaybackState.Ended or PlaybackState.Failed ||
                (!snapshot.HasVideo && (snapshot.Position > TimeSpan.Zero || snapshot.State == PlaybackState.Paused ||
                    (!_opening && _session.ErrorMessage is not null))))) ExitFullscreen();
            OpenButton.IsEnabled = AddButton.IsEnabled = OpenPlaylistButton.IsEnabled = SavePlaylistButton.IsEnabled = !PlaylistBusy;
            QueueList.IsEnabled = !PlaylistBusy;
            PlayButton.IsEnabled = !PlaylistBusy && _session.Queue.Count > 0;
            PreviousButton.IsEnabled = !PlaylistBusy && _session.CanPrevious;
            NextButton.IsEnabled = !PlaylistBusy && _session.CanNext;
            RateSelector.SelectedIndex = Array.IndexOf(PlaybackRates.Supported.ToArray(), snapshot.Rate);
            RefreshSubtitles();
            RateStatusLabel.Text = snapshot.RateError is not null ? "Unavailable" : snapshot.AcceptedRate is null ? "Preferred" : "";
            ToolTip.SetTip(RateSelector, snapshot.RateError?.Message ??
                (snapshot.AcceptedRate is null ? "Preferred speed; applied when playback starts." :
                    "Rate request accepted by LibVLC. Actual output depends on the media; pitch preservation is not guaranteed."));
            var repeatLabel = $"Repeat: {_session.Repeat}";
            RepeatButton.Content = repeatLabel;
            Avalonia.Automation.AutomationProperties.SetName(RepeatButton, repeatLabel);
            ToolTip.SetTip(RepeatButton, repeatLabel + " — natural endings: Off stops, All wraps, One replays current. Click to cycle.");
            var shuffleLabel = $"Shuffle: {(_session.ShuffleEnabled ? "On" : "Off")}";
            ShuffleButton.Content = shuffleLabel;
            Avalonia.Automation.AutomationProperties.SetName(ShuffleButton, shuffleLabel);
            ToolTip.SetTip(ShuffleButton, shuffleLabel + " — random playback order; visible queue stays in manual order.");
            QueueLabel.Text = _session.CurrentIndex >= 0
                ? $"Queue · current {_session.CurrentIndex + 1}/{_session.Queue.Count}"
                : $"Queue · {_session.Queue.Count} tracks";
            RefreshQueueButtons();
            PauseButton.IsEnabled = snapshot.State == PlaybackState.Playing;
            var activeInput = _session.CurrentItem is not null || snapshot.State is PlaybackState.Ready or PlaybackState.Playing or PlaybackState.Paused;
            StopButton.IsEnabled = activeInput && snapshot.Media is not null;
            SeekSlider.IsEnabled = activeInput && snapshot.CanSeek;
            SeekSlider.Maximum = Math.Max(1, snapshot.Duration.TotalSeconds);
            if (!_seeking) SeekSlider.Value = snapshot.Position.TotalSeconds;
            VolumeSlider.Value = snapshot.Volume;
            MuteButton.Content = snapshot.IsMuted ? "Unmute" : "Mute";
            TimeLabel.Text = $"{FormatTime(snapshot.Position)} / {FormatTime(snapshot.Duration)}";
            StatusLabel.Text = _opening || _savingSession ? _playlistOperation ?? "Starting playback engine…" : _session.CurrentItem is null
                ? (_session.Queue.Count > 0 ? $"Queue loaded · {_session.Queue.Count} tracks · Press Play" : "Ready · Open a local media file")
                : snapshot.Media is { } media
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
