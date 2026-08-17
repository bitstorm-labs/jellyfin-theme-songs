namespace Jellyfin.Plugin.ThemeSongs;

public class PlexThemeProvider(HttpClient client) : IThemeProvider
{
    private const string UrlTemplate = "https://tvthemes.plexapp.com/{0}.mp3";

    public async Task<byte[]?> FetchAsync(string tvdbId, CancellationToken ct)
    {
        var escapedId = Uri.EscapeDataString(tvdbId);
        var url = string.Format(System.Globalization.CultureInfo.InvariantCulture, UrlTemplate, escapedId);
        using var response = await client.GetAsync(url, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;

        var body = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        var contentType = response.Content.Headers.ContentType?.MediaType;
        return ThemeFile.IsValidMp3(body, contentType) ? body : null;
    }
}
