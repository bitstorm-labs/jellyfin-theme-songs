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
    private static readonly TimeSpan Throttle = TimeSpan.FromSeconds(1);

    private readonly ILogger<ItemAddedListener> _logger = loggerFactory.CreateLogger<ItemAddedListener>();
    private readonly CancellationTokenSource _cts = new();

    public Task StartAsync(CancellationToken cancellationToken)
    {
        libraryManager.ItemAdded += OnItemAdded;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        libraryManager.ItemAdded -= OnItemAdded;
        // Cancels every in-flight handler body below, not just future ones: unsubscribing alone
        // only stops new work, but an already-running Task.Run could still be mid-flight against
        // libraryManager/providerManager while the host is tearing them down.
        _cts.Cancel();
        return Task.CompletedTask;
    }

    private void OnItemAdded(object? sender, ItemChangeEventArgs e)
    {
        if (ThemeSongsPlugin.Instance?.Configuration.EnableItemAddedHook != true) return;
        if (e.Item is not Series series) return;

        var ct = _cts.Token;

        _ = Task.Run(async () =>
        {
            // Shared with ThemeSongsScheduledTask: both touch the same ThemeSongs.attempts.json
            // file, and this queues behind a running nightly sweep rather than racing it.
            var gateAcquired = false;
            try
            {
                await ThemeSongsGate.AttemptsFile.WaitAsync(ct).ConfigureAwait(false);
                gateAcquired = true;

                using var http = new HttpClient();
                http.DefaultRequestHeaders.UserAgent.ParseAdd("Jellyfin-ThemeSongs/1.0 (+https://github.com/bitstorm-labs)");

                var store = new AttemptStore(Path.Combine(appPaths.PluginConfigurationsPath, "ThemeSongs.attempts.json"));
                await store.LoadAsync(ct).ConfigureAwait(false);

                var service = new ThemeDownloadService(
                    libraryManager, providerManager, new PlexThemeProvider(http), store, fileSystem,
                    loggerFactory.CreateLogger<ThemeDownloadService>());

                var madeRequest = false;
                try
                {
                    madeRequest = await service.RunForSeriesAsync(series, ct).ConfigureAwait(false);
                }
                finally
                {
                    // Save even if RunForSeriesAsync was cancelled mid-flight, so a recorded
                    // failure isn't lost. CancellationToken.None deliberately: a cancelled token
                    // here would lose the very records this save exists to protect (the same
                    // reasoning behind ThemeDownloadService.RunAsync's own finally-save).
                    await store.SaveAsync(CancellationToken.None).ConfigureAwait(false);
                }

                if (madeRequest)
                {
                    // ~1/sec throttle upstream, matching the nightly sweep's throttle. Only paid
                    // when an upstream request actually happened - a scan that bulk-adds series
                    // whose themes are already on disk (or outside the retry window) must not
                    // stall behind this.
                    await Task.Delay(Throttle, ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Normal on shutdown (StopAsync cancelled the token) - not a fault, don't warn.
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
                if (gateAcquired) ThemeSongsGate.AttemptsFile.Release();
            }
        });
    }
}
