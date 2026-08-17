using Jellyfin.Plugin.ThemeSongs;

namespace Jellyfin.Plugin.ThemeSongs.Tests;

public class ThemeFileTests
{
    private static byte[] FakeMp3(int size)
    {
        var b = new byte[size];
        b[0] = (byte)'I'; b[1] = (byte)'D'; b[2] = (byte)'3';
        return b;
    }

    [Fact]
    public void AcceptsRealMp3()
        => Assert.True(ThemeFile.IsValidMp3(FakeMp3(400_000), "audio/mpeg"));

    [Fact]
    public void AcceptsFrameSyncWithoutId3()
    {
        var b = new byte[400_000];
        b[0] = 0xFF; b[1] = 0xFB;
        Assert.True(ThemeFile.IsValidMp3(b, "audio/mpeg"));
    }

    [Fact]
    public void RejectsHtmlErrorPage()
    {
        var html = System.Text.Encoding.ASCII.GetBytes("<html><body>404 Not Found</body></html>");
        Assert.False(ThemeFile.IsValidMp3(html, "text/html"));
    }

    [Fact]
    public void RejectsTruncatedFile()
        => Assert.False(ThemeFile.IsValidMp3(FakeMp3(1000), "audio/mpeg"));

    [Fact]
    public void RejectsEmptyBody()
        => Assert.False(ThemeFile.IsValidMp3([], "audio/mpeg"));

    [Fact]
    public void RejectsWrongContentTypeEvenIfBodyLooksRight()
        => Assert.False(ThemeFile.IsValidMp3(FakeMp3(400_000), "text/html"));

    [Fact]
    public async Task WriteAtomicLeavesNoTempFileOnSuccess()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        var dest = Path.Combine(dir, "theme.mp3");
        await ThemeFile.WriteAtomicAsync(dest, FakeMp3(400_000), CancellationToken.None);

        Assert.True(File.Exists(dest));
        Assert.Empty(Directory.GetFiles(dir, "*.tmp"));
        Directory.Delete(dir, true);
    }
}
