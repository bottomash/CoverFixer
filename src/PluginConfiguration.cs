using MediaBrowser.Model.Plugins;

namespace CoverFixer;

public sealed class PluginConfiguration : BasePluginConfiguration
{
    public string TmdbReadAccessToken { get; set; } = string.Empty;
}
