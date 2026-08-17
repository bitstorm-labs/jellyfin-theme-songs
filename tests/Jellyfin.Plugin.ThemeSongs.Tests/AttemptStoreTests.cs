using Jellyfin.Plugin.ThemeSongs;

namespace Jellyfin.Plugin.ThemeSongs.Tests;

public class AttemptStoreTests
{
    private static string TempPath() =>
        Path.Combine(Directory.CreateTempSubdirectory().FullName, "attempts.json");

    private static readonly DateTimeOffset Now = new(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TriesShowNeverAttempted()
        => Assert.True(new AttemptStore(TempPath()).ShouldTry("371572", 7, Now));

    [Fact]
    public void SkipsShowInsideBackoffWindow()
    {
        var s = new AttemptStore(TempPath());
        s.RecordFailure("444904", Now.AddDays(-2));
        Assert.False(s.ShouldTry("444904", 7, Now));
    }

    [Fact]
    public void RetriesShowPastBackoffWindow()
    {
        var s = new AttemptStore(TempPath());
        s.RecordFailure("444904", Now.AddDays(-8));
        Assert.True(s.ShouldTry("444904", 7, Now));
    }

    [Fact]
    public void ClearMakesEverythingEligibleAgain()
    {
        var s = new AttemptStore(TempPath());
        s.RecordFailure("444904", Now);
        s.Clear();
        Assert.True(s.ShouldTry("444904", 7, Now));
    }

    [Fact]
    public async Task PersistsAcrossReload()
    {
        var path = TempPath();
        var a = new AttemptStore(path);
        a.RecordFailure("444904", Now);
        await a.SaveAsync(CancellationToken.None);

        var b = new AttemptStore(path);
        await b.LoadAsync(CancellationToken.None);
        Assert.False(b.ShouldTry("444904", 7, Now));
    }

    [Fact]
    public async Task LoadOfMissingFileIsNotAnError()
    {
        var s = new AttemptStore(TempPath());
        await s.LoadAsync(CancellationToken.None);
        Assert.True(s.ShouldTry("anything", 7, Now));
    }
}
