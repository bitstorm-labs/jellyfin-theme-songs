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
        // Shared with ItemAddedListener: both touch the same ThemeSongs.attempts.json file, and
        // holding this for the whole sweep (which can run several minutes) is intended - item-added
        // work is background and non-urgent, so it queues behind the sweep rather than racing it.
        await ThemeSongsGate.AttemptsFile.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var http = PlexThemeProvider.CreateClient();

            var store = new AttemptStore(
                Path.Combine(appPaths.PluginConfigurationsPath, "ThemeSongs.attempts.json"),
                loggerFactory.CreateLogger<AttemptStore>());
            var service = new ThemeDownloadService(
                libraryManager, providerManager, new PlexThemeProvider(http), store, fileSystem,
                loggerFactory.CreateLogger<ThemeDownloadService>());

            var written = await service.RunAsync(progress, cancellationToken).ConfigureAwait(false);
            loggerFactory.CreateLogger<ThemeSongsScheduledTask>()
                .LogInformation("Theme Songs task finished: {Written} theme(s) downloaded.", written);
        }
        finally
        {
            ThemeSongsGate.AttemptsFile.Release();
        }
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
