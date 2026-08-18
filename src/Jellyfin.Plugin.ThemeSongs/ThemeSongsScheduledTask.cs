using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ThemeSongs;

public class ThemeSongsScheduledTask(
    ILibraryManager libraryManager,
    IProviderManager providerManager,
    IApplicationPaths appPaths,
    IFileSystem fileSystem,
    ILoggerFactory loggerFactory) : IScheduledTask
{
    public string Name => "Download theme songs";
    public string Key => "ThemeSongsDownload";
    public string Description => "Downloads missing TV theme songs and saves them as theme.mp3.";
    public string Category => "Theme Songs";

    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Jellyfin-ThemeSongs/1.0 (+https://github.com/bitstorm-labs)");

        var store = new AttemptStore(Path.Combine(appPaths.PluginConfigurationsPath, "ThemeSongs.attempts.json"));
        var service = new ThemeDownloadService(
            libraryManager, providerManager, new PlexThemeProvider(http), store, fileSystem,
            loggerFactory.CreateLogger<ThemeDownloadService>());

        await service.RunAsync(progress, cancellationToken).ConfigureAwait(false);
    }

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() =>
    [
        new TaskTriggerInfo
        {
            Type = TaskTriggerInfoType.DailyTrigger,
            TimeOfDayTicks = TimeSpan.FromHours(3).Ticks
        }
    ];
}
