using LegacyMediaPlayer.Application;
using LegacyMediaPlayer.Core;
using Xunit;

namespace LegacyMediaPlayer.Tests;

public sealed class DeferredPlaybackEngineTests
{
    [Fact]
    public void Empty_window_preferences_and_disposal_do_not_create_native_resources()
    {
        var creates = 0;
        using var engine = new DeferredPlaybackEngine(() => { creates++; return new MockPlaybackEngine(); }, new(false, false, false));
        engine.SetVolume(29);
        engine.SetMuted(true);
        engine.AttachVideoSurface(null);
        engine.Stop();
        Assert.Equal(PlaybackState.Empty, engine.Snapshot.State);
        Assert.Equal(29, engine.Snapshot.Volume);
        Assert.True(engine.Snapshot.IsMuted);
        Assert.Empty(engine.GetTracks());
        engine.Dispose();
        Assert.Equal(0, creates);
        Assert.Throws<ObjectDisposedException>(() => engine.SetVolume(10));
    }

    [Fact]
    public async Task Concurrent_initialization_creates_one_engine_and_applies_latest_preferences()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var creates = 0;
        using var backend = new MockPlaybackEngine();
        using var engine = new DeferredPlaybackEngine(() =>
        {
            Interlocked.Increment(ref creates);
            entered.SetResult();
            Assert.True(release.Wait(TimeSpan.FromSeconds(5)));
            return backend;
        }, new(false, false, false));
        try
        {
            var first = engine.InitializeAsync();
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var second = engine.InitializeAsync();
            Assert.Same(first, second);
            engine.SetVolume(41); engine.SetMuted(true);
            release.Set();
            await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(1, creates);
            Assert.Equal(41, backend.Snapshot.Volume);
            Assert.True(backend.Snapshot.IsMuted);
            using var file = new TempMedia();
            engine.Open(MediaSource.FromFile(file.Path)); engine.Play();
            Assert.Equal(PlaybackState.Playing, engine.Snapshot.State);
        }
        finally { release.Set(); }
    }

    [Fact]
    public async Task Close_during_initialization_returns_without_waiting_and_disposes_late_engine()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        using var backend = new MockPlaybackEngine();
        var engine = new DeferredPlaybackEngine(() =>
        {
            entered.SetResult();
            Assert.True(release.Wait(TimeSpan.FromSeconds(5)));
            return backend;
        }, new(false, false, false));
        var changes = 0;
        engine.Changed += (_, _) => changes++;
        var init = engine.InitializeAsync();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            engine.Dispose(); // Must return while the factory is still blocked.
            Assert.False(init.IsCompleted);
            release.Set();
            await Assert.ThrowsAsync<ObjectDisposedException>(() => init);
            Assert.Throws<ObjectDisposedException>(() => backend.SetVolume(1));
            Assert.Equal(0, changes);
        }
        finally { release.Set(); engine.Dispose(); }
    }

    [Fact]
    public async Task Native_startup_failure_is_reported_to_the_session_without_crashing()
    {
        using var engine = new DeferredPlaybackEngine(() => throw new PlaybackException("Bundled LibVLC is missing."), new(false, false, false));
        await Assert.ThrowsAsync<PlaybackException>(engine.InitializeAsync);
        using var file = new TempMedia();
        using var session = new PlayerSession(engine, action => action());
        session.OpenAndPlayFile(file.Path);
        Assert.Contains("Bundled LibVLC", session.ErrorMessage);
        Assert.Equal(PlaybackState.Empty, session.Snapshot.State);
    }
}
