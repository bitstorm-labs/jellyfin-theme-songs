using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ThemeSongs;

public class ThemeDownloadService(
    ILibraryManager libraryManager,
    IProviderManager providerManager,
    IThemeProvider themeProvider,
    AttemptStore attempts,
    IFileSystem fileSystem,
    ILogger<ThemeDownloadService> logger)
{
    private static readonly TimeSpan Throttle = TimeSpan.FromSeconds(1);

    public async Task<int> RunAsync(IProgress<double>? progress, CancellationToken ct)
    {
        await attempts.LoadAsync(ct).ConfigureAwait(false);

        var series = libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = [BaseItemKind.Series],
            Recursive = true
        }).OfType<Series>().ToList();

        var written = 0;
        var libraryUnwritableLogged = false;

        try
        {
            for (var i = 0; i < series.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report(i * 100.0 / series.Count);

                Outcome result;
                try
                {
                    result = await TryOneAsync(series[i], ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    // Real cancellation of THIS run must still stop the sweep. Without the
                    // filter, a TaskCanceledException from an HttpClient timeout (which derives
                    // from OperationCanceledException) would be misclassified as cancellation
                    // and abort the run instead of falling through to the catch below.
                    throw;
                }
                catch (Exception ex)
                {
                    // One sick series (e.g. a refresh failure on locked/corrupt metadata,
                    // or an HttpClient timeout) must not take down the rest of the sweep.
                    logger.LogWarning(ex, "Unexpected error processing {Series}", series[i].Name);
                    continue;
                }

                if (result == Outcome.Written) written++;
                if (result == Outcome.Unwritable && !libraryUnwritableLogged)
                {
                    // Once per run, not per item: 305 identical errors buries the one that matters.
                    logger.LogError("Library is not writable; no themes can be saved.");
                    libraryUnwritableLogged = true;
                }
                if (i < series.Count - 1 && result != Outcome.Skipped) await Task.Delay(Throttle, ct).ConfigureAwait(false);
            }
        }
        finally
        {
            // Save even if the run was cancelled or a series threw past the catch above,
            // so recorded failures aren't lost and the backoff is honoured next run.
            // Use CancellationToken.None: if ct is already cancelled, the save must still succeed.
            await attempts.SaveAsync(CancellationToken.None).ConfigureAwait(false);
        }

        progress?.Report(100);
        logger.LogInformation("Theme sweep complete: {Written} themes written.", written);
        return written;
    }

    public Task RunForSeriesAsync(Series series, CancellationToken ct) => TryOneAsync(series, ct);

    private enum Outcome { Skipped, Written, Fetched, Unwritable }

    private async Task<Outcome> TryOneAsync(Series series, CancellationToken ct)
    {
        var tvdbId = series.GetProviderId(MetadataProvider.Tvdb);
        // No TVDB id is normal (e.g. YouTube channel "series") — not an error.
        if (string.IsNullOrEmpty(tvdbId) || string.IsNullOrEmpty(series.Path)) return Outcome.Skipped;

        var dest = Path.Combine(series.Path, "theme.mp3");
        if (File.Exists(dest)) return Outcome.Skipped;

        var config = ThemeSongsPlugin.Instance?.Configuration ?? new PluginConfiguration();
        if (!attempts.ShouldTry(tvdbId, config.RetryAfterDays, DateTimeOffset.UtcNow)) return Outcome.Skipped;

        byte[]? body;
        try
        {
            body = await themeProvider.FetchAsync(tvdbId, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            // Transient: do NOT record a failure, so it retries on the next run.
            logger.LogWarning(ex, "Theme fetch failed for {Series}", series.Name);
            return Outcome.Fetched;
        }

        if (body is null)
        {
            attempts.RecordFailure(tvdbId, DateTimeOffset.UtcNow);
            logger.LogDebug("No theme available for {Series} (tvdb {Tvdb})", series.Name, tvdbId);
            return Outcome.Fetched;
        }

        try
        {
            await ThemeFile.WriteAtomicAsync(dest, body, ct).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException) { return Outcome.Unwritable; }
        catch (IOException) { return Outcome.Unwritable; }

        // Jellyfin does not expose ThemeMedia until the item is refreshed — verified.
        await providerManager.RefreshSingleItem(
            series,
            new MetadataRefreshOptions(new DirectoryService(fileSystem)),
            ct).ConfigureAwait(false);

        logger.LogInformation("Saved theme for {Series}", series.Name);
        return Outcome.Written;
    }
}
