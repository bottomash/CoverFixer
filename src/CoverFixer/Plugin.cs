using System;
using MediaBrowser.Common.Plugins;

namespace CoverFixer;

public sealed class Plugin : BasePlugin
{
    public override string Name => "CoverFixer";

    public override string Description =>
        "定时补全电影、剧集、季和单集缺失的主封面，优先英文并回退简体中文。";

    public override Guid Id => Guid.Parse("57044f85-39b9-4aa8-b4c8-058992a6e49e");
}
