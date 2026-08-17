namespace Jellyfin.Plugin.ThemeSongs;

public interface IThemeProvider
{
    /// <summary>Returns validated MP3 bytes, or null when no theme is available.
    /// Implementations must not throw for an ordinary "not found".</summary>
    Task<byte[]?> FetchAsync(string tvdbId, CancellationToken ct);
}
