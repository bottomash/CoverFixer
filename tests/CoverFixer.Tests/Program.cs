using System;
using System.Collections.Generic;
using System.Text.Json;
using CoverFixer;
using MediaBrowser.Model.Providers;

var tests = new List<(string Name, Action Run)>
{
    ("Original language wins over English", OriginalLanguageWins),
    ("English wins over larger Chinese image", EnglishWins),
    ("Simplified Chinese variants are accepted", ChineseVariants),
    ("Language-neutral image is the fourth choice", NeutralLanguageFallback),
    ("Any language is the final fallback", AnyLanguageFallback),
    ("TMDB original language response is parsed", TmdbOriginalLanguageIsParsed),
    ("Shortcut injection registers CoverFixer command source", ShortcutInjectionIsBuilt),
    ("Emby numeric Series Item ID is accepted", NumericSeriesItemIdIsAccepted),
    ("Emby GUID Series Item ID is rejected", GuidSeriesItemIdIsRejected),
    ("Highest resolution wins within a language", ResolutionWins),
    ("Empty URL is rejected", EmptyUrlIsRejected),
    ("Episode still accepts language-neutral TMDB image", EpisodeStillAcceptsNeutralImage),
    ("Episode still prefers TMDB over a larger secondary provider", EpisodeStillPrefersTmdb),
    ("Episode still rejects portrait artwork", EpisodeStillRejectsPortrait),
    ("Episode still requires larger area to replace screenshot", EpisodeStillRequiresLargerArea),
    ("Episode still keeps equal-resolution screenshot untouched", EpisodeStillKeepsEqualScreenshot),
    ("Episode poster-like detection uses aspect ratio", EpisodePosterDetection),
    ("Import cutoff uses one rolling calendar month", ImportCutoffUsesCalendarMonth),
};

int failed = 0;
foreach ((string name, Action run) in tests)
{
    try
    {
        run();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception error)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {name}: {error.Message}");
    }
}

Console.WriteLine($"RESULT total={tests.Count} failed={failed}");
return failed == 0 ? 0 : 1;

static void OriginalLanguageWins()
{
    var japanese = Image("ja", "https://example.invalid/ja.jpg", 1000, 1500);
    var english = Image("en", "https://example.invalid/en.jpg", 4000, 6000);
    AssertSame(japanese, CoverSelector.Select(new[] { english, japanese }, "ja-JP"));
}

static void EnglishWins()
{
    var chinese = Image("zh-CN", "https://example.invalid/zh.jpg", 4000, 6000);
    var english = Image("en", "https://example.invalid/en.jpg", 1000, 1500);
    AssertSame(english, CoverSelector.Select(new[] { chinese, english }));
}

static void ChineseVariants()
{
    foreach (string language in new[] { "zh", "zh-CN", "zh_Hans", "cmn-Hans" })
    {
        var image = Image(language, $"https://example.invalid/{language}.jpg", 1000, 1500);
        AssertSame(image, CoverSelector.Select(new[] { image }));
    }
}

static void NeutralLanguageFallback()
{
    var neutral = Image(null, "https://example.invalid/neutral.jpg", 1000, 1500);
    var japanese = Image("ja", "https://example.invalid/ja.jpg", 4000, 6000);
    AssertSame(neutral, CoverSelector.Select(new[] { japanese, neutral }));
}

static void AnyLanguageFallback()
{
    var small = Image("ko", "https://example.invalid/ko.jpg", 1000, 1500);
    var large = Image("ja", "https://example.invalid/ja.jpg", 2000, 3000);
    AssertSame(large, CoverSelector.Select(new[] { small, large }));
}

static void TmdbOriginalLanguageIsParsed()
{
    using JsonDocument document = JsonDocument.Parse("{\"original_language\":\"ko\"}");
    if (TmdbLanguageClient.ReadOriginalLanguage(document.RootElement) != "ko")
    {
        throw new InvalidOperationException("未正确读取 TMDB original_language");
    }
}

static void ShortcutInjectionIsBuilt()
{
    string script = ShortcutMenuHelper.BuildModifiedScript("original-shortcuts");
    if (!script.StartsWith("original-shortcuts", StringComparison.Ordinal)
        || !script.Contains("registerCommandSource(coverFixerCommandSource)", StringComparison.Ordinal)
        || !script.Contains("coverfixer_refresh_tmdb_cover", StringComparison.Ordinal)
        || !script.Contains("coverFixerHiddenSeriesCommandIds", StringComparison.Ordinal)
        || !script.Contains("'favorite'", StringComparison.Ordinal)
        || !script.Contains("'markplayed'", StringComparison.Ordinal)
        || !script.Contains("'shuffle'", StringComparison.Ordinal)
        || !script.Contains("'addtocollection'", StringComparison.Ordinal)
        || !script.Contains("'addtoplaylist'", StringComparison.Ordinal)
        || !script.Contains("'sync'", StringComparison.Ordinal)
        || !script.Contains("'convert'", StringComparison.Ordinal)
        || !script.Contains("'delete'", StringComparison.Ordinal)
        || !script.Contains(".btnDeleteItem", StringComparison.Ordinal)
        || script.Contains("}, 3000);", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("详情菜单命令未正确注入 shortcuts.js");
    }
}

static void NumericSeriesItemIdIsAccepted()
{
    if (!CoverFixerRefreshService.TryParseEmbyItemId("36295", out long itemId)
        || itemId != 36295)
    {
        throw new InvalidOperationException("未正确解析 Emby 数字 Item ID");
    }
}

static void GuidSeriesItemIdIsRejected()
{
    if (CoverFixerRefreshService.TryParseEmbyItemId(
        "12345678-1234-1234-1234-123456789abc",
        out _))
    {
        throw new InvalidOperationException("不应接受 GUID 形式的 Emby Item ID");
    }
}

static void ResolutionWins()
{
    var small = Image("en-US", "https://example.invalid/small.jpg", 1000, 1500);
    var large = Image("en-GB", "https://example.invalid/large.jpg", 2000, 3000);
    AssertSame(large, CoverSelector.Select(new[] { small, large }));
}

static void EmptyUrlIsRejected()
{
    var invalid = Image("en", "", 2000, 3000);
    if (CoverSelector.Select(new[] { invalid }) is not null)
    {
        throw new InvalidOperationException("不应选择空地址图片");
    }
}

static void EpisodeStillAcceptsNeutralImage()
{
    var still = Image(null, "https://example.invalid/still.jpg", 1920, 1080, "TheMovieDb");
    AssertSame(still, CoverSelector.SelectEpisodeStill(new[] { still }));
}

static void EpisodeStillPrefersTmdb()
{
    var other = Image(null, "https://example.invalid/other.jpg", 3840, 2160, "OtherProvider");
    var tmdb = Image(null, "https://example.invalid/tmdb.jpg", 1920, 1080, "TheMovieDb");
    AssertSame(tmdb, CoverSelector.SelectEpisodeStill(new[] { other, tmdb }));
}

static void EpisodeStillRejectsPortrait()
{
    var poster = Image("en", "https://example.invalid/poster.jpg", 1000, 1500, "TheMovieDb");
    if (CoverSelector.SelectEpisodeStill(new[] { poster }) is not null)
    {
        throw new InvalidOperationException("不应把竖版海报作为单集剧照");
    }
}

static void EpisodeStillRequiresLargerArea()
{
    var screenshot = Image(null, "https://example.invalid/shot.jpg", 1280, 720, "TheMovieDb");
    var larger = Image(null, "https://example.invalid/larger.jpg", 1920, 1080, "TheMovieDb");
    AssertSame(
        larger,
        CoverSelector.SelectEpisodeStill(new[] { screenshot, larger }, minimumPixelArea: 1280L * 720));
}

static void EpisodeStillKeepsEqualScreenshot()
{
    var screenshot = Image(null, "https://example.invalid/shot.jpg", 1920, 1080, "TheMovieDb");
    var same = Image(null, "https://example.invalid/same.jpg", 1920, 1080, "TheMovieDb");
    if (CoverSelector.SelectEpisodeStill(new[] { screenshot, same }, minimumPixelArea: 1920L * 1080) is not null)
    {
        throw new InvalidOperationException("相同分辨率的剧照不应被替换");
    }
}

static void EpisodePosterDetection()
{
    if (!CoverSelector.IsEpisodePosterLike(1000, 1500))
    {
        throw new InvalidOperationException("竖版图片应判定为疑似剧集海报");
    }

    if (CoverSelector.IsEpisodePosterLike(1920, 1080))
    {
        throw new InvalidOperationException("横版图片不应判定为剧集海报");
    }
}

static void ImportCutoffUsesCalendarMonth()
{
    var now = new DateTimeOffset(2026, 3, 31, 12, 30, 0, TimeSpan.Zero);
    var expected = new DateTimeOffset(2026, 2, 28, 12, 30, 0, TimeSpan.Zero);
    if (CoverFixerTask.GetImportCutoff(now) != expected)
    {
        throw new InvalidOperationException("近一个月应按自然月回推并保留时刻与时区");
    }
}

static RemoteImageInfo Image(
    string? language,
    string url,
    int width,
    int height,
    string providerName = "test") =>
    new()
    {
        Language = language,
        Url = url,
        Width = width,
        Height = height,
        ProviderName = providerName,
    };

static void AssertSame(RemoteImageInfo expected, RemoteImageInfo? actual)
{
    if (!ReferenceEquals(expected, actual))
    {
        throw new InvalidOperationException("选择结果不符合预期");
    }
}
