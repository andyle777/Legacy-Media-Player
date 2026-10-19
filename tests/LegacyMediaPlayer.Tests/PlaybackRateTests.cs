using System.Text.Json;
using LegacyMediaPlayer.Application;
using LegacyMediaPlayer.Core;
using Xunit;

namespace LegacyMediaPlayer.Tests;

public sealed class PlaybackRateTests
{
    [Theory]
    [InlineData(0.5f)] [InlineData(0.75f)] [InlineData(1f)]
    [InlineData(1.25f)] [InlineData(1.5f)] [InlineData(2f)]
    public async Task Preferred_rate_survives_lazy_initialization_and_transport(float rate)
    {
        using var file = new TempMedia();
        var creates = 0;
        using var deferred = new DeferredPlaybackEngine(() => { creates++; return new MockPlaybackEngine(); }, new(false, false, false));
        using var session = new PlayerSession(deferred, a => a());
        Assert.Equal(1f, session.Snapshot.Rate);
        session.SetRate(rate);
        Assert.Equal(0, creates);
        Assert.Equal(rate, session.Snapshot.Rate);
        Assert.Null(session.Snapshot.AcceptedRate);
        await deferred.InitializeAsync();
        session.OpenFiles([file.Path]);
        Assert.Equal(1, creates);
        Assert.Equal(rate, session.Snapshot.AcceptedRate);
        session.Pause(); session.Seek(TimeSpan.FromSeconds(1)); session.Play();
        Assert.Equal(rate, session.Snapshot.Rate);
        session.Stop(); session.Play();
        Assert.Equal(rate, session.Snapshot.AcceptedRate);
        session.Dispose();
        Assert.Throws<ObjectDisposedException>(() => deferred.SetRate(1));
    }

    [Theory]
    [InlineData(0f)] [InlineData(-1f)] [InlineData(3f)] [InlineData(float.NaN)] [InlineData(float.PositiveInfinity)]
    public void Invalid_rate_does_not_change_preferences_or_initialize_native_code(float rate)
    {
        var creates = 0;
        using var engine = new DeferredPlaybackEngine(() => { creates++; return new MockPlaybackEngine(); }, new(false, false, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.SetRate(rate));
        Assert.Equal(1f, engine.Snapshot.Rate);
        Assert.Equal(0, creates);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"Rate\":999,\"Volume\":37}")]
    [InlineData("{\"Rate\":0}")]
    public void Missing_or_invalid_saved_rate_uses_normal_speed(string json)
    {
        using var file = new TempMedia(); File.WriteAllText(file.Path, json);
        Assert.Equal(1f, new AudioPreferenceStore(file.Path).Load().Rate);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Rate_edits_during_initialization_are_retained_and_disposal_prevents_late_application(bool close)
    {
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var backend = new MockPlaybackEngine();
        using var engine = new DeferredPlaybackEngine(() => { entered.SetResult(); release.Wait(); return backend; }, new(false, false, false));
        var initialization = engine.InitializeAsync();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            engine.SetRate(2);
            if (close) engine.Dispose();
            release.Set();
            if (close)
            {
                await Assert.ThrowsAsync<ObjectDisposedException>(() => initialization);
                Assert.Throws<ObjectDisposedException>(() => backend.SetRate(1));
                return;
            }
            await initialization;
            Assert.Equal(2f, engine.Snapshot.Rate);
            Assert.Null(engine.Snapshot.AcceptedRate);
        }
        finally { release.Set(); }
    }

    [Fact]
    public void Failed_open_keeps_the_selected_rate()
    {
        using var engine = new MockPlaybackEngine(); using var session = new PlayerSession(engine, a => a());
        session.SetRate(1.5f); session.OpenFiles([Path.Combine(Path.GetTempPath(), Guid.NewGuid()+".mp4")]);
        Assert.NotNull(session.ErrorMessage);
        Assert.Equal(1.5f, session.Snapshot.Rate);
    }

    [Fact]
    public void Saved_rate_is_restored_without_losing_existing_preferences()
    {
        using var file = new TempMedia();
        File.WriteAllText(file.Path, "{\"Volume\":37,\"IsMuted\":true,\"Repeat\":2,\"ShuffleEnabled\":true,\"Rate\":0.75}");
        var store = new AudioPreferenceStore(file.Path);
        var restored = store.Load();
        store.Save(restored);
        using var json = JsonDocument.Parse(File.ReadAllText(file.Path));
        Assert.True(json.RootElement.TryGetProperty("Rate", out var rate), "The selected speed must survive a settings round trip.");
        Assert.Equal(0.75, rate.GetDouble());
        Assert.Equal(37, restored.Volume);
        Assert.True(restored.IsMuted);
        Assert.Equal(RepeatMode.One, restored.Repeat);
        Assert.True(restored.ShuffleEnabled);
    }
}
