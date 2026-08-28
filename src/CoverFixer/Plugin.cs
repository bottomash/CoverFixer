using System;
using System.Collections.Generic;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;

namespace CoverFixer;

public sealed class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public Plugin(
        IApplicationPaths applicationPaths,
        IXmlSerializer xmlSerializer,
        ILogManager logManager)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        ShortcutMenuHelper.Initialize(
            applicationPaths.ProgramSystemPath,
            logManager.GetLogger("CoverFixer"));
    }

    public static Plugin? Instance { get; private set; }

    public override string Name => "CoverFixer";

    public override string Description =>
        "按作品原始语言优先级补全封面，并在剧集详情菜单中提供 TMDB 封面刷新命令。";

    public override Guid Id => Guid.Parse("57044f85-39b9-4aa8-b4c8-058992a6e49e");

    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = "CoverFixer",
                DisplayName = "CoverFixer",
                EmbeddedResourcePath = "CoverFixer.Configuration.configPage.html",
                IsMainConfigPage = true,
            },
            new PluginPageInfo
            {
                Name = "coverfixerjs",
                EmbeddedResourcePath = "CoverFixer.Configuration.configPage.js",
            },
        };
    }
}
