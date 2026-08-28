using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Tasks;

namespace CoverFixer;

public sealed class CoverFixerTask : IScheduledTask
{
    private static readonly string[] SupportedItemTypes =
    {
        "Movie",
        "Series",
        "Season",
        "Episode",
    };

    private static readonly TimeSpan RequestDelay = TimeSpan.FromMilliseconds(250);

    private readonly ILibraryManager _libraryManager;
    private readonly IProviderManager _providerManager;
    private readonly IDirectoryService _directoryService;
    private readonly ILogger _logger;
    private readonly TmdbLanguageClient _tmdbLanguageClient = new();

    public CoverFixerTask(
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

    public string Name => "补全缺失封面";

    public string Key => "CoverFixerTask";

    public string Description =>
        "处理近一个月入库的电影、剧集、季和单集封面；按原始语言、英文、简体中文、无语言和任意语言的顺序选图。";

    public string Category => "媒体库";

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return new[]
        {
            new TaskTriggerInfo
            {
                Type = "DailyTrigger",
                TimeOfDayTicks = TimeSpan.FromHours(4).Ticks,
            },
        };
    }

    public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
    {
        DateTimeOffset importCutoff = GetImportCutoff(DateTimeOffset.UtcNow);
        BaseItem[] items = _libraryManager.GetItemList(
            new InternalItemsQuery
            {
                Recursive = true,
                IncludeItemTypes = SupportedItemTypes,
                IsVirtualItem = false,
                MinDateCreated = importCutoff,
            },
            cancellationToken);

        BaseItem[] pending = items
            .Where(NeedsBackfill)
            .ToArray();

        int completed = 0;
        int added = 0;
        int unavailable = 0;
        int failed = 0;

        _logger.Info(
            "开始补全封面：入库时间不早于={0:O} 媒体总数={1} 待检查={2}",
            importCutoff,
            items.Length,
            pending.Length);

        foreach (BaseItem item in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                bool saved = await BackfillItem(item, cancellationToken).ConfigureAwait(false);
                if (saved)
                {
                    added++;
                }
                else
                {
                    unavailable++;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception error)
            {
                failed++;
                _logger.ErrorException(
                    "补全封面失败：类型={0} 名称={1}",
                    error,
                    item.GetType().Name,
                    item.Name);
            }
            finally
            {
                completed++;
                progress.Report(pending.Length == 0 ? 100 : completed * 100.0 / pending.Length);
            }

            await Task.Delay(RequestDelay, cancellationToken).ConfigureAwait(false);
        }

        progress.Report(100);
        _logger.Info(
            "封面补全完成：补全或替换={0} 无合适图片={1} 失败={2}",
            added,
            unavailable,
            failed);
    }

    public static DateTimeOffset GetImportCutoff(DateTimeOffset now) => now.AddMonths(-1);

    private async Task<bool> BackfillItem(BaseItem item, CancellationToken cancellationToken)
    {
        if (!NeedsBackfill(item))
        {
            return false;
        }

        if (!item.HasImage(ImageType.Primary, 0))
        {
            _libraryManager.UpdateImages(item);
            if (item.HasImage(ImageType.Primary, 0))
            {
                _logger.Info(
                    "已登记磁盘中的封面：类型={0} 名称={1}",
                    item.GetType().Name,
                    item.Name);
                return true;
            }
        }

        bool replacingEpisodePoster = item is Episode && item.HasImage(ImageType.Primary, 0);

        long minimumEpisodeArea = 0;
        if (item is Episode episode
            && episode.GetImageInfo(ImageType.Primary, 0) is ItemImageInfo current
            && !CoverSelector.IsEpisodePosterLike(current.Width, current.Height))
        {
            // 已有横版剧照：只有更大分辨率的远程剧照才替换，避免重复替换和覆盖手动设置的图。
            minimumEpisodeArea = CoverSelector.PixelArea(current.Width, current.Height);
        }

        var libraryOptions = _libraryManager.GetLibraryOptions(item);
        var query = new RemoteImageQuery
        {
            ImageType = ImageType.Primary,
            IncludeAllLanguages = true,
        };

        RemoteImageInfo[] candidates = (await _providerManager
            .GetAvailableRemoteImages(
                item,
                libraryOptions,
                query,
                _directoryService,
                cancellationToken)
            .ConfigureAwait(false)).ToArray();

        string? originalLanguage = await GetOriginalLanguage(item, cancellationToken).ConfigureAwait(false);
        RemoteImageInfo? selected = item is Episode
            ? CoverSelector.SelectEpisodeStill(candidates, minimumEpisodeArea, originalLanguage)
            : CoverSelector.Select(candidates, originalLanguage);
        if (selected is null)
        {
            string languages = string.Join(
                ",",
                candidates
                    .Select(image => string.IsNullOrWhiteSpace(image.Language) ? "(未标注)" : image.Language)
                    .Distinct(StringComparer.OrdinalIgnoreCase));
            _logger.Info(
                "没有合适封面：类型={0} 名称={1} 候选图片数={2} 语言={3}",
                item.GetType().Name,
                item.Name,
                candidates.Length,
                languages);
            return false;
        }

        await _providerManager.SaveImage(
            item,
            libraryOptions,
            selected.Url,
            ImageType.Primary,
            replacingEpisodePoster ? 0 : null,
            Array.Empty<long>(),
            _directoryService,
            true,
            cancellationToken).ConfigureAwait(false);

        _libraryManager.UpdateImages(item);
        if (!item.HasImage(ImageType.Primary, 0))
        {
            throw new InvalidOperationException("封面已写入磁盘，但 Emby 未登记为主封面");
        }

        _logger.Info(
            "已补全封面：类型={0} 名称={1} 语言={2} 来源={3} 尺寸={4}x{5}",
            item.GetType().Name,
            item.Name,
            selected.Language ?? string.Empty,
            selected.ProviderName ?? string.Empty,
            selected.Width ?? 0,
            selected.Height ?? 0);
        return true;
    }

    private async Task<string?> GetOriginalLanguage(
        BaseItem item,
        CancellationToken cancellationToken)
    {
        string token = Plugin.Instance?.Configuration.TmdbReadAccessToken ?? string.Empty;
        if (string.IsNullOrWhiteSpace(token)
            || !TmdbLanguageResolver.TryResolve(item, _libraryManager, out string mediaType, out string tmdbId))
        {
            return null;
        }

        try
        {
            return await _tmdbLanguageClient
                .GetOriginalLanguage(mediaType, tmdbId, token, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            _logger.ErrorException(
                "查询 TMDB 原始语言失败：类型={0} 名称={1} TMDB={2}",
                error,
                item.GetType().Name,
                item.Name,
                tmdbId);
            return null;
        }
    }

    private static bool NeedsBackfill(BaseItem item)
    {
        if (!item.HasImage(ImageType.Primary, 0))
        {
            return true;
        }

        if (item is not Episode)
        {
            return false;
        }

        // 单集已有图片也要检查：自动截图会被更大分辨率的 TheMovieDb 剧照替换。
        return item.GetImageInfo(ImageType.Primary, 0) is not null;
    }
}
