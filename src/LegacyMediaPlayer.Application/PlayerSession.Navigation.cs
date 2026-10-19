namespace LegacyMediaPlayer.Application;

public sealed partial class PlayerSession
{
    private readonly Random _random;
    // QueueItem uses reference identity, so identical paths remain distinct entries.
    private readonly List<QueueItem> _remaining = [];
    private readonly List<QueueItem> _history = [];
    private int _historyIndex = -1;

    public RepeatMode Repeat { get; private set; }
    public bool ShuffleEnabled { get; private set; }
    public bool CanNext => _queue.Count > 0 && (ShuffleEnabled
        ? _historyIndex < _history.Count - 1 || _remaining.Count > 0 || Repeat == RepeatMode.All
        : CurrentIndex < _queue.Count - 1 || Repeat == RepeatMode.All);
    public bool CanPrevious => ShuffleEnabled
        ? (CurrentItem is null ? _historyIndex : _historyIndex - 1) >= 0
        : CurrentIndex > 0 || CurrentIndex == 0 && Repeat == RepeatMode.All;

    public void SetRepeatMode(RepeatMode mode) => Execute(() =>
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        Repeat = mode;
    });

    public void SetShuffleEnabled(bool enabled) => Execute(() =>
    {
        if (ShuffleEnabled == enabled) return;
        ShuffleEnabled = enabled;
        ResetTraversal();
    });

    public void Next() => Execute(() => { if (CanNext) Advance(); });
    public void Previous() => Execute(() =>
    {
        if (!CanPrevious) return;
        if (ShuffleEnabled)
        {
            if (CurrentItem is not null) _historyIndex--;
            StartItem(_history[_historyIndex]);
        }
        else StartItem(_queue[CurrentIndex > 0 ? CurrentIndex - 1 : _queue.Count - 1]);
    });

    private void ResetTraversal()
    {
        _remaining.Clear(); _history.Clear(); _historyIndex = -1;
        if (!ShuffleEnabled) return;
        _remaining.AddRange(_queue.Where(item => !ReferenceEquals(item, CurrentItem)));
        ShuffleRemaining();
        if (CurrentItem is { } current) RecordChoice(current);
    }

    private void ShuffleRemaining()
    {
        for (var i = _remaining.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (_remaining[i], _remaining[j]) = (_remaining[j], _remaining[i]);
        }
    }

    private void RecordChoice(QueueItem item)
    {
        _remaining.Remove(item);
        if (_historyIndex < _history.Count - 1)
            _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
        if (_historyIndex >= 0 && ReferenceEquals(_history[_historyIndex], item)) return;
        _history.Add(item);
        // Keep recent navigation useful without growing forever during daily repeat.
        if (_history.Count > 256) _history.RemoveAt(0);
        _historyIndex = _history.Count - 1;
    }

    private void StartChosenItem(QueueItem item)
    {
        if (ShuffleEnabled) RecordChoice(item);
        StartItem(item);
    }

    private void Advance()
    {
        if (_queue.Count == 0) return;
        if (!ShuffleEnabled)
        {
            StartItem(_queue[CurrentIndex >= 0 && CurrentIndex < _queue.Count - 1 ? CurrentIndex + 1 : 0]);
            return;
        }
        if (_historyIndex < _history.Count - 1)
        {
            StartItem(_history[++_historyIndex]);
            return;
        }
        if (_remaining.Count == 0)
        {
            if (Repeat != RepeatMode.All) return;
            _remaining.AddRange(_queue);
            ShuffleRemaining();
            if (_remaining.Count > 1 && ReferenceEquals(_remaining[0], CurrentItem))
                (_remaining[0], _remaining[1]) = (_remaining[1], _remaining[0]);
        }
        StartChosenItem(_remaining[0]);
    }

    private void ForgetItem(QueueItem item)
    {
        _remaining.Remove(item);
        for (var i = _history.Count - 1; i >= 0; i--)
            if (ReferenceEquals(_history[i], item))
            {
                _history.RemoveAt(i);
                if (i <= _historyIndex) _historyIndex--;
            }
    }
}
