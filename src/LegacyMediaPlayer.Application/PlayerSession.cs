using System.ComponentModel;
using System.Collections.ObjectModel;
using LegacyMediaPlayer.Core;

namespace LegacyMediaPlayer.Application;

public sealed partial class PlayerSession : INotifyPropertyChanged, IDisposable
{
    private readonly IPlaybackEngine _engine;
    private readonly Action<Action> _dispatch;
    private volatile bool _disposed;
    private readonly ObservableCollection<QueueItem> _queue = [];
    private long _generation;
    private bool _completionHandled;
    private bool _currentOpened;
    private bool _engineErrorVisible;
    private QueueItem? _currentItem;

    public PlayerSession(IPlaybackEngine engine, Action<Action> dispatch, Random? random = null)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(dispatch);
        _engine = engine;
        _dispatch = dispatch;
        _random = random ?? Random.Shared;
        Queue = new(_queue);
        Snapshot = engine.Snapshot;
        engine.Changed += OnEngineChanged;
    }

    public PlaybackSnapshot Snapshot { get; private set; }
    public ReadOnlyObservableCollection<QueueItem> Queue { get; }
    public QueueItem? CurrentItem
    {
        get => _currentItem;
        private set
        {
            if (ReferenceEquals(_currentItem, value)) return;
            if (_currentItem is not null) _currentItem.IsCurrent = false;
            _currentItem = value;
            if (_currentItem is not null) _currentItem.IsCurrent = true;
        }
    }
    public int CurrentIndex => CurrentItem is null ? -1 : _queue.IndexOf(CurrentItem);
    public string? ErrorMessage { get; private set; }
    public event PropertyChangedEventHandler? PropertyChanged;
    public void OpenFile(string path) => Execute(() =>
    {
        var item = new QueueItem(MediaSource.FromFile(path));
        CancelCompletion();
        _engine.Open(item.Source);
        _queue.Clear(); _queue.Add(item); CurrentItem = item; _currentOpened = true;
        ResetTraversal();
    });
    // Keep open + play in one error boundary: a failed open must not resume the old file.
    public void OpenAndPlayFile(string path) => OpenFiles([path]);
    public void OpenFiles(IEnumerable<string> paths) => Execute(() =>
    {
        var items = paths.Select(p => new QueueItem(MediaSource.FromFile(p))).ToArray();
        if (items.Length == 0) return;
        CancelCompletion();
        _engine.Stop();
        CurrentItem = null;
        _queue.Clear();
        foreach (var item in items) _queue.Add(item);
        ResetTraversal();
        StartChosenItem(items[0]);
    });
    public void AddFiles(IEnumerable<string> paths) => Execute(() =>
    {
        var items = paths.Select(p => new QueueItem(MediaSource.FromFile(p))).ToArray();
        foreach (var item in items) _queue.Add(item);
        if (ShuffleEnabled) { _remaining.AddRange(items); ShuffleRemaining(); }
        if (CurrentItem is null && _queue.Count > 0) Advance();
    });
    public void PlayAt(int index) => Execute(() =>
    {
        if (index >= 0 && index < _queue.Count) StartChosenItem(_queue[index]);
    });
    public void RemoveAt(int index) => Execute(() =>
    {
        if (index < 0 || index >= _queue.Count) return;
        if (ReferenceEquals(_queue[index], CurrentItem))
        {
            CancelCompletion();
            _engine.Stop();
            CurrentItem = null;
        }
        ForgetItem(_queue[index]);
        _queue.RemoveAt(index);
    });
    public void MoveItem(int from, int to) => Execute(() =>
    {
        if (from >= 0 && from < _queue.Count && to >= 0 && to < _queue.Count)
            _queue.Move(from, to);
    });
    private void CancelCompletion() { _generation++; _completionHandled = true; }
    private void StartItem(QueueItem item)
    {
        CancelCompletion();
        _engine.Stop();
        CurrentItem = item;
        _currentOpened = false;
        // Validate again: queued files may have been deleted since they were added.
        _engine.Open(MediaSource.FromFile(item.Source.Path));
        _currentOpened = true;
        _completionHandled = false;
        _engine.Play();
    }
    public void Play() => Execute(() =>
    {
        if (CurrentItem is null && _queue.Count > 0)
        {
            // Explicit Play restarts a completed traversal if its current item was removed.
            if (ShuffleEnabled && !CanNext) ResetTraversal();
            Advance();
        }
        else if (CurrentItem is not null && (!_currentOpened || Snapshot.State is PlaybackState.Ended or PlaybackState.Failed)) StartItem(CurrentItem);
        else if (CurrentItem is not null)
        {
            _generation++;
            _completionHandled = false;
            _engine.Play();
        }
        else if (Snapshot.Media is null) throw new InvalidOperationException("Add a local media file first.");
    });
    public void Pause() => Execute(_engine.Pause);
    public void Stop() => Execute(() => { CancelCompletion(); _engine.Stop(); });
    public void Seek(TimeSpan position) => Execute(() => _engine.Seek(position));
    public void SetVolume(int volume) => Execute(() => _engine.SetVolume(volume));
    public void SetMuted(bool muted) => Execute(() => _engine.SetMuted(muted));
    public void SetRate(float rate) => Execute(() => _engine.SetRate(rate));
    public void ReportError(string message)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        _engineErrorVisible = false;
        ErrorMessage = message;
        Notify();
    }

    private void Execute(Action action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        try
        {
            action();
            ErrorMessage = (_engine.Snapshot.Error ?? _engine.Snapshot.RateError)?.Message;
            _engineErrorVisible = ErrorMessage is not null;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException or PlaybackException)
        {
            _engineErrorVisible = false;
            ErrorMessage = ex.Message;
        }
        Snapshot = _engine.Snapshot;
        Notify();
    }

    private void OnEngineChanged(object? sender, PlaybackChangedEventArgs e)
    {
        if (_disposed) return;
        var generation = _generation;
        _dispatch(() =>
        {
            if (_disposed) return;
            // Read the latest value after dispatch: queued native events can arrive
            // out of order, and must never move the displayed state backwards.
            Snapshot = _engine.Snapshot;
            // Only the end belonging to the current play command can advance.
            // Never call native transport on a LibVLC event thread; dispatch must enqueue.
            if (e.Snapshot.State == PlaybackState.Ended && Snapshot.State == PlaybackState.Ended &&
                generation == _generation && !_completionHandled && CurrentItem is not null)
            {
                _completionHandled = true;
                if (Repeat == RepeatMode.One) { Execute(() => StartItem(CurrentItem)); return; }
                if (CanNext) { Next(); return; }
            }
            if ((Snapshot.Error ?? Snapshot.RateError) is { } error)
            {
                ErrorMessage = error.Message;
                _engineErrorVisible = true;
            }
            else if (_engineErrorVisible)
            {
                ErrorMessage = null;
                _engineErrorVisible = false;
            }
            Notify();
        });
    }

    private void Notify() => PropertyChanged?.Invoke(this, new(null));

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _engine.Changed -= OnEngineChanged;
        PropertyChanged = null;
        _engine.Dispose();
    }
}
