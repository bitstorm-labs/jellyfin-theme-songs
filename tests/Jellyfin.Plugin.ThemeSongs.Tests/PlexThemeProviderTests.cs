using System.Net;
using Jellyfin.Plugin.ThemeSongs;

namespace Jellyfin.Plugin.ThemeSongs.Tests;

public class PlexThemeProviderTests
{
    private sealed class StubHandler(HttpStatusCode code, byte[] body, string contentType)
        : HttpMessageHandler
    {
        public Uri? LastUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUri = request.RequestUri;
            var content = new ByteArrayContent(body);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            return Task.FromResult(new HttpResponseMessage(code) { Content = content });
        }
    }

    private static byte[] FakeMp3(int size = 400_000)
    {
        var b = new byte[size];
        b[0] = (byte)'I'; b[1] = (byte)'D'; b[2] = (byte)'3';
        return b;
    }

    [Fact]
    public async Task ReturnsBytesForValidMp3()
    {
        var handler = new StubHandler(HttpStatusCode.OK, FakeMp3(), "audio/mpeg");
        var provider = new PlexThemeProvider(new HttpClient(handler));

        var result = await provider.FetchAsync("371572", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("https://tvthemes.plexapp.com/371572.mp3", handler.LastUri!.ToString());
    }

    [Fact]
    public async Task ReturnsNullOn404()
    {
        var html = System.Text.Encoding.ASCII.GetBytes("<html>404</html>");
        var provider = new PlexThemeProvider(
            new HttpClient(new StubHandler(HttpStatusCode.NotFound, html, "text/html")));

        Assert.Null(await provider.FetchAsync("444904", CancellationToken.None));
    }

    [Fact]
    public async Task ReturnsNullWhenBodyIsNotAudioDespite200()
    {
        var provider = new PlexThemeProvider(
            new HttpClient(new StubHandler(HttpStatusCode.OK, FakeMp3(400_000), "text/html")));

        Assert.Null(await provider.FetchAsync("371572", CancellationToken.None));
    }

    [Fact]
    public async Task ReturnsNullWhenBodyIsTruncated()
    {
        var provider = new PlexThemeProvider(
            new HttpClient(new StubHandler(HttpStatusCode.OK, FakeMp3(1000), "audio/mpeg")));

        Assert.Null(await provider.FetchAsync("371572", CancellationToken.None));
    }

    [Fact]
    public async Task EscapesUnexpectedCharactersInTvdbId()
    {
        var handler = new StubHandler(HttpStatusCode.OK, FakeMp3(), "audio/mpeg");
        var provider = new PlexThemeProvider(new HttpClient(handler));

        var result = await provider.FetchAsync("12?x=1", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("https://tvthemes.plexapp.com/12%3Fx%3D1.mp3", handler.LastUri!.ToString());
    }
}
