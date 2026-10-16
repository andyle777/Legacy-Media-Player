using System.ComponentModel;
using LegacyMediaPlayer.Core;

namespace LegacyMediaPlayer.Application;

public sealed class PlayerSession : INotifyPropertyChanged, IDisposable
{
    private readonly IPlaybackEngine _engine;
    private readonly Action<Action> _dispatch;
    private volatile bool _disposed;

    public PlayerSession(IPlaybackEngine engine, Action<Action> dispatch)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(dispatch);
        _engine = engine;
        _dispatch = dispatch;
        Snapshot = engine.Snapshot;
        engine.Changed += OnEngineChanged;
    }

    public PlaybackSnapshot Snapshot { get; private set; }
    public string? ErrorMessage { get; private set; }
    public event PropertyChangedEventHandler? PropertyChanged;
    public void OpenFile(string path) => Execute(() => _engine.Open(MediaSource.FromFile(path)));
    public void Play() => Execute(_engine.Play);
    public void Pause() => Execute(_engine.Pause);
    public void Stop() => Execute(_engine.Stop);
    public void Seek(TimeSpan position) => Execute(() => _engine.Seek(position));
    public void SetVolume(int volume) => Execute(() => _engine.SetVolume(volume));
    public void SetMuted(bool muted) => Execute(() => _engine.SetMuted(muted));
    public void ReportError(string message)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ErrorMessage = message;
        Notify();
    }

    private void Execute(Action action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        try
        {
            action();
            ErrorMessage = _engine.Snapshot.Error?.Message;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException or PlaybackException)
        {
            ErrorMessage = ex.Message;
        }
        Snapshot = _engine.Snapshot;
        Notify();
    }

    private void OnEngineChanged(object? sender, PlaybackChangedEventArgs e)
    {
        if (_disposed) return;
        _dispatch(() =>
        {
            if (_disposed) return;
            // Read the latest value after dispatch: queued native events can arrive
            // out of order, and must never move the displayed state backwards.
            Snapshot = _engine.Snapshot;
            if (Snapshot.Error is { } error) ErrorMessage = error.Message;
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
