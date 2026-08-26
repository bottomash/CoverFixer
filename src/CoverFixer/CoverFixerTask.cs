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
        "为电影、剧集、季和单集补全缺失的主封面；优先英文，没有时回退简体中文。";

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
        BaseItem[] items = _libraryManager.GetItemList(
            new InternalItemsQuery
            {
                Recursive = true,
                IncludeItemTypes = SupportedItemTypes,
                IsVirtualItem = false,
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
            "开始补全封面：媒体总数={0} 缺失或单集封面异常={1}",
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

        RemoteImageInfo? selected = item is Episode
            ? CoverSelector.SelectEpisodeStill(candidates)
            : CoverSelector.Select(candidates);
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

        ItemImageInfo image = item.GetImageInfo(ImageType.Primary, 0);
        return image is not null && CoverSelector.IsEpisodePosterLike(image.Width, image.Height);
    }
}
