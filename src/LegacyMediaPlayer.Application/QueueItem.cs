using LegacyMediaPlayer.Core;

namespace LegacyMediaPlayer.Application;

// Reference identity deliberately distinguishes duplicate paths in the queue.
public sealed class QueueItem(MediaSource source)
{
    public MediaSource Source { get; } = source;
    public string DisplayName => Path.GetFileName(Source.Path);
    public override string ToString() => DisplayName;
}
