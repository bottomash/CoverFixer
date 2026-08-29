using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Controller.Api;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Services;

namespace CoverFixer;

[Unauthenticated]
public sealed class CoverFixerShortcutService : IService, IRequiresRequest
{
    private readonly IHttpResultFactory _resultFactory;

    public CoverFixerShortcutService(IHttpResultFactory resultFactory)
    {
        _resultFactory = resultFactory;
    }

    public IRequest Request { get; set; } = null!;

    public object Get(GetCoverFixerShortcuts request)
    {
        return _resultFactory.GetResult(
            Request,
            ShortcutMenuHelper.GetShortcuts(),
            "application/x-javascript",
            new Dictionary<string, string>
            {
                ["Cache-Control"] = "no-cache, no-store, must-revalidate",
            });
    }
}

public sealed class CoverFixerRefreshService : BaseApiService
{
    private readonly ILibraryManager _libraryManager;
    private readonly IProviderManager _providerManager;
    private readonly IDirectoryService _directoryService;
    private readonly ILogger _logger;
    private readonly TmdbLanguageClient _tmdbLanguageClient = new();

    public CoverFixerRefreshService(
        ILibraryManager libraryManager,
        IProviderManager providerManager,
        IFileSystem fileSystem,
        ILogManager logManager)
    {
        _libraryManager = libraryManager;
        _providerManager = providerManager;
        _directoryService = new DirectoryService(fileSystem);
        _logger = logManager.GetLogger("CoverFixer");
    }

    public async Task<RefreshSeriesCoverResult> Post(RefreshSeriesCover request)
    {
        PluginConfiguration configuration = Plugin.Instance?.Configuration
            ?? throw new InvalidOperationException("CoverFixer 配置尚未加载");
        if (string.IsNullOrWhiteSpace(configuration.TmdbReadAccessToken))
        {
            throw new InvalidOperationException("请先在 CoverFixer 配置中填写 TMDB API Read Access Token");
        }

        if (!TryParseEmbyItemId(request.ItemId, out long itemId))
        {
            throw new ArgumentException(
                "无效的 Emby Series Item ID",
                nameof(RefreshSeriesCover.ItemId));
        }

        BaseItem item = _libraryManager.GetItemById(itemId)
            ?? throw new InvalidOperationException("找不到指定的 Emby 项目");
        if (item is not Series series)
        {
            throw new InvalidOperationException("指定的 Emby 项目不是剧集 Series");
        }

        if (!TmdbLanguageResolver.TryResolve(series, _libraryManager, out string mediaType, out string tmdbId))
        {
            throw new InvalidOperationException("指定剧集没有有效的 TMDB ID");
        }

        string? originalLanguage = await _tmdbLanguageClient
            .GetOriginalLanguage(
                mediaType,
                tmdbId,
                configuration.TmdbReadAccessToken,
                Request.CancellationToken)
            .ConfigureAwait(false);

        var libraryOptions = _libraryManager.GetLibraryOptions(series);
        var query = new RemoteImageQuery
        {
            ImageType = ImageType.Primary,
            IncludeAllLanguages = true,
        };
        RemoteImageInfo[] candidates = (await _providerManager
            .GetAvailableRemoteImages(
                series,
                libraryOptions,
                query,
                _directoryService,
                Request.CancellationToken)
            .ConfigureAwait(false))
            .Where(image => string.Equals(
                image.ProviderName?.Trim(),
                "TheMovieDb",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        RemoteImageInfo? selected = CoverSelector.Select(candidates, originalLanguage);
        if (selected is null)
        {
            throw new InvalidOperationException("TMDB 没有返回可用的剧集封面");
        }

        await _providerManager.SaveImage(
            series,
            libraryOptions,
            selected.Url,
            ImageType.Primary,
            series.HasImage(ImageType.Primary, 0) ? 0 : null,
            Array.Empty<long>(),
            _directoryService,
            true,
            Request.CancellationToken).ConfigureAwait(false);

        _libraryManager.UpdateImages(series);
        series.UpdateToRepository(ItemUpdateType.ImageUpdate);
        if (!series.HasImage(ImageType.Primary, 0))
        {
            throw new InvalidOperationException("封面已写入磁盘，但 Emby 未登记为主封面");
        }

        _logger.Info(
            "已从详情菜单刷新剧集 TMDB 封面：名称={0} EmbyId={1} TMDB={2} 原始语言={3} 图片语言={4} 尺寸={5}x{6}",
            series.Name,
            series.Id,
            tmdbId,
            originalLanguage ?? string.Empty,
            selected.Language ?? string.Empty,
            selected.Width ?? 0,
            selected.Height ?? 0);

        return new RefreshSeriesCoverResult
        {
            ItemId = series.InternalId.ToString(CultureInfo.InvariantCulture),
            TmdbId = tmdbId,
            OriginalLanguage = originalLanguage ?? string.Empty,
            ImageLanguage = selected.Language ?? string.Empty,
            ProviderName = selected.ProviderName ?? string.Empty,
        };
    }

    internal static bool TryParseEmbyItemId(string value, out long itemId) =>
        long.TryParse(
            value,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out itemId)
        && itemId > 0;
}
