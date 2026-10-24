namespace LegacyMediaPlayer.App;

public partial class MainWindow
{
    private async Task<T> WithPlaybackSafePickerAsync<T>(Func<Task<T>> pick)
    {
        // LibVLC 3 Stop is synchronous. Do not restart an ended input while a
        // native modal dialog can be waiting for messages from this UI thread.
        using var transition = _session.DeferAutomaticAdvance();
        return await pick();
    }
}
