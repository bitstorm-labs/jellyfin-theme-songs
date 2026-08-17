using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.ThemeSongs;

public class ThemeSongsPlugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public ThemeSongsPlugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public static ThemeSongsPlugin? Instance { get; private set; }

    public override string Name => "Theme Songs";

    public override Guid Id => Guid.Parse("d8d1d1a1-4d9e-4d55-9a2e-0a0a1f5b7c31");

    public override string Description => "Downloads TV theme songs and saves them as theme.mp3.";

    public IEnumerable<PluginPageInfo> GetPages() =>
    [
        new PluginPageInfo
        {
            Name = Name,
            EmbeddedResourcePath = $"{GetType().Namespace}.configPage.html"
        }
    ];
}
