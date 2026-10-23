using Jellyfin.Plugin.ThemeSongs;

namespace Jellyfin.Plugin.ThemeSongs.Tests;

/// <summary>#12. ItemAdded fires before the identify pass, so a brand-new series has no TVDB id
/// and the hook skipped it until the nightly sweep. The theme must be fetched once the series is
/// identified.</summary>
public class PendingSeriesTests
{
    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static readonly DateTimeOffset T0 = new(2026, 10, 22, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ASeriesAddedWithoutAnIdIsFetchedWhenItIsIdentified()
    {
        var pending = new PendingSeries(new Clock(T0));
        var id = Guid.NewGuid();
        Assert.False(pending.OnAdded(id, hasTvdbId: false));
        Assert.False(pending.OnUpdated(id, hasTvdbId: false));
        Assert.True(pending.OnUpdated(id, hasTvdbId: true));
    }

    [Fact]
    public void ItIsFetchedOnlyOnce()
    {
        var pending = new PendingSeries(new Clock(T0));
        var id = Guid.NewGuid();
        pending.OnAdded(id, hasTvdbId: false);
        Assert.True(pending.OnUpdated(id, hasTvdbId: true));
        Assert.False(pending.OnUpdated(id, hasTvdbId: true));
    }

    [Fact]
    public void ASeriesAddedWithAnIdIsFetchedStraightAway()
    {
        var pending = new PendingSeries(new Clock(T0));
        var id = Guid.NewGuid();
        Assert.True(pending.OnAdded(id, hasTvdbId: true));
        Assert.False(pending.OnUpdated(id, hasTvdbId: true));
    }

    [Fact]
    public void UpdatesToSeriesThatWereNeverPendingAreIgnored()
        => Assert.False(new PendingSeries(new Clock(T0)).OnUpdated(Guid.NewGuid(), hasTvdbId: true));

    [Fact]
    public void ASeriesIdentifiedAfterTheWindowIsLeftToTheNightlySweep()
    {
        var clock = new Clock(T0);
        var pending = new PendingSeries(clock);
        var id = Guid.NewGuid();
        pending.OnAdded(id, hasTvdbId: false);
        clock.Now = T0 + PendingSeries.Window + TimeSpan.FromMinutes(1);
        Assert.False(pending.OnUpdated(id, hasTvdbId: true));
        Assert.Equal(0, pending.Count);
    }

    [Fact]
    public void TheWaitingSetIsBoundedAndExpiredEntriesMakeRoom()
    {
        var clock = new Clock(T0);
        var pending = new PendingSeries(clock);
        for (var i = 0; i < PendingSeries.MaxWaiting + 10; i++) pending.OnAdded(Guid.NewGuid(), hasTvdbId: false);
        Assert.Equal(PendingSeries.MaxWaiting, pending.Count);

        clock.Now = T0 + PendingSeries.Window + TimeSpan.FromMinutes(1);
        var late = Guid.NewGuid();
        pending.OnAdded(late, hasTvdbId: false);
        Assert.Equal(1, pending.Count);
        Assert.True(pending.OnUpdated(late, hasTvdbId: true));
    }
}
