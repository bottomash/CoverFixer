using System;
using System.Collections.Generic;
using CoverFixer;
using MediaBrowser.Model.Providers;

var tests = new List<(string Name, Action Run)>
{
    ("English wins over larger Chinese image", EnglishWins),
    ("Simplified Chinese variants are accepted", ChineseVariants),
    ("Traditional and unrelated languages are rejected", UnsupportedLanguages),
    ("Highest resolution wins within a language", ResolutionWins),
    ("Empty URL is rejected", EmptyUrlIsRejected),
    ("Episode still accepts language-neutral TMDB image", EpisodeStillAcceptsNeutralImage),
    ("Episode still prefers TMDB over a larger secondary provider", EpisodeStillPrefersTmdb),
    ("Episode still rejects portrait artwork", EpisodeStillRejectsPortrait),
    ("Episode still requires larger area to replace screenshot", EpisodeStillRequiresLargerArea),
    ("Episode still keeps equal-resolution screenshot untouched", EpisodeStillKeepsEqualScreenshot),
    ("Episode poster-like detection uses aspect ratio", EpisodePosterDetection),
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

static void UnsupportedLanguages()
{
    var traditional = Image("zh-TW", "https://example.invalid/tw.jpg", 1000, 1500);
    var japanese = Image("ja", "https://example.invalid/ja.jpg", 1000, 1500);
    if (CoverSelector.Select(new[] { traditional, japanese }) is not null)
    {
        throw new InvalidOperationException("不应选择繁体中文或无关语言");
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
