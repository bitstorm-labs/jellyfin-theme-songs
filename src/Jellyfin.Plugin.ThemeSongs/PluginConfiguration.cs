using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.ThemeSongs;

public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>Days to wait before retrying a show whose theme was not found.</summary>
    public int RetryAfterDays { get; set; } = 7;

    /// <summary>Fetch a theme as soon as a new series is added.</summary>
    public bool EnableItemAddedHook { get; set; } = true;
}
