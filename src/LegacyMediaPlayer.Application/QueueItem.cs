using System.ComponentModel;
using LegacyMediaPlayer.Core;

namespace LegacyMediaPlayer.Application;

// Reference identity deliberately distinguishes duplicate paths in the queue.
public sealed class QueueItem(MediaSource source) : INotifyPropertyChanged
{
    private bool _isCurrent;
    public MediaSource Source { get; } = source;
    public string DisplayName => Path.GetFileName(Source.Path);
    public bool IsCurrent
    {
        get => _isCurrent;
        internal set
        {
            if (_isCurrent == value) return;
            _isCurrent = value;
            PropertyChanged?.Invoke(this, new(nameof(IsCurrent)));
        }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    public override string ToString() => DisplayName;
}
