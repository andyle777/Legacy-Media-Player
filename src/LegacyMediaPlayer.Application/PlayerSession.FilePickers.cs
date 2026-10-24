using LegacyMediaPlayer.Core;

namespace LegacyMediaPlayer.Application;

public sealed partial class PlayerSession
{
    private int _automaticAdvanceDeferrals;
    private long? _deferredCompletionGeneration;

    /// <summary>
    /// Create/dispose on the owning context. Native modal dialogs must finish
    /// before an automatic transition synchronously stops their owner's video.
    /// </summary>
    public IDisposable DeferAutomaticAdvance()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _automaticAdvanceDeferrals++;
        return new AdvanceDeferral(() =>
        {
            _automaticAdvanceDeferrals--;
            if (_disposed || _automaticAdvanceDeferrals != 0 || _deferredCompletionGeneration is not { } generation) return;
            _deferredCompletionGeneration = null;
            Snapshot = _engine.Snapshot;
            if (!TryCompleteNaturalEnd(generation)) Notify();
        });
    }

    private bool TryCompleteNaturalEnd(long generation)
    {
        if (_disposed || generation != _generation || _completionHandled || CurrentItem is null || Snapshot.State != PlaybackState.Ended)
            return false;
        if (_automaticAdvanceDeferrals > 0)
        {
            _deferredCompletionGeneration = generation;
            return false;
        }
        _deferredCompletionGeneration = null;
        _completionHandled = true;
        if (Repeat == RepeatMode.One) { Execute(() => StartItem(CurrentItem)); return true; }
        if (CanNext) { Next(); return true; }
        return false;
    }

    private sealed class AdvanceDeferral(Action release) : IDisposable
    {
        private Action? _release = release;
        public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
    }
}
