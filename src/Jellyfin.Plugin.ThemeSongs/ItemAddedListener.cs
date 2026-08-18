using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ThemeSongs;

public class ItemAddedListener(
    ILibraryManager libraryManager,
    IProviderManager providerManager,
    IApplicationPaths appPaths,
    IFileSystem fileSystem,
    ILoggerFactory loggerFactory) : IHostedService
{
    // Serializes the handler body across concurrent ItemAdded events (e.g. a library scan
    // adding many series at once), which would otherwise race load -> modify -> save on the
    // same AttemptStore JSON file and lose updates - including against the nightly sweep.
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private readonly ILogger<ItemAddedListener> _logger = loggerFactory.CreateLogger<ItemAddedListener>();

    public Task StartAsync(CancellationToken cancellationToken)
    {
        libraryManager.ItemAdded += OnItemAdded;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        libraryManager.ItemAdded -= OnItemAdded;
        return Task.CompletedTask;
    }

    private void OnItemAdded(object? sender, ItemChangeEventArgs e)
    {
        if (ThemeSongsPlugin.Instance?.Configuration.EnableItemAddedHook != true) return;
        if (e.Item is not Series series) return;

        _ = Task.Run(async () =>
        {
            await Gate.WaitAsync().ConfigureAwait(false);
            try
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.UserAgent.ParseAdd("Jellyfin-ThemeSongs/1.0 (+https://github.com/bitstorm-labs)");

                var store = new AttemptStore(Path.Combine(appPaths.PluginConfigurationsPath, "ThemeSongs.attempts.json"));
                await store.LoadAsync(CancellationToken.None).ConfigureAwait(false);

                var service = new ThemeDownloadService(
                    libraryManager, providerManager, new PlexThemeProvider(http), store, fileSystem,
                    loggerFactory.CreateLogger<ThemeDownloadService>());

                await service.RunForSeriesAsync(series, CancellationToken.None).ConfigureAwait(false);
                await store.SaveAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // RunForSeriesAsync has no per-series exception guard (unlike the nightly RunAsync's
                // sweep loop), so this catch is the only thing standing between a fault here - a
                // network error, a write failure, a refresh failure - and total silence.
                _logger.LogWarning(ex, "Item-added theme fetch failed for {Series}", series.Name);
            }
            finally
            {
                Gate.Release();
            }
        });
    }
}
