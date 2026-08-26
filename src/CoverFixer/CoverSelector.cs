using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Model.Providers;

namespace CoverFixer;

public static class CoverSelector
{
    private const double MinimumEpisodeStillAspectRatio = 1.2;

    private static readonly HashSet<string> SimplifiedChineseLanguages =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "zh",
            "zh-cn",
            "zh-hans",
            "cmn-hans",
        };

    public static RemoteImageInfo? Select(IEnumerable<RemoteImageInfo> images)
    {
        ArgumentNullException.ThrowIfNull(images);

        return images
            .Where(image => image is not null && !string.IsNullOrWhiteSpace(image.Url))
            .Select(image => new { Image = image, LanguageRank = GetLanguageRank(image.Language) })
            .Where(candidate => candidate.LanguageRank < int.MaxValue)
            .OrderBy(candidate => candidate.LanguageRank)
            .ThenByDescending(candidate => PixelArea(candidate.Image))
            .ThenByDescending(candidate => candidate.Image.CommunityRating ?? double.MinValue)
            .ThenByDescending(candidate => candidate.Image.VoteCount ?? int.MinValue)
            .ThenBy(candidate => candidate.Image.ProviderName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(candidate => candidate.Image)
            .FirstOrDefault();
    }

    public static RemoteImageInfo? SelectEpisodeStill(
        IEnumerable<RemoteImageInfo> images,
        long minimumPixelArea = 0)
    {
        ArgumentNullException.ThrowIfNull(images);

        return images
            .Where(image =>
                image is not null
                && !string.IsNullOrWhiteSpace(image.Url)
                && IsLandscape(image.Width ?? 0, image.Height ?? 0)
                && PixelArea(image) > minimumPixelArea)
            .OrderBy(image => IsMovieDb(image.ProviderName) ? 0 : 1)
            .ThenByDescending(PixelArea)
            .ThenByDescending(image => image.CommunityRating ?? double.MinValue)
            .ThenByDescending(image => image.VoteCount ?? int.MinValue)
            .ThenBy(image => image.ProviderName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    public static bool IsEpisodePosterLike(int width, int height) =>
        width > 0 && height > 0 && !IsLandscape(width, height);

    internal static int GetLanguageRank(string? language)
    {
        string normalized = NormalizeLanguage(language);
        if (normalized.Equals("en", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("en-", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return SimplifiedChineseLanguages.Contains(normalized) ? 1 : int.MaxValue;
    }

    private static string NormalizeLanguage(string? language) =>
        (language ?? string.Empty).Trim().Replace('_', '-').ToLowerInvariant();

    internal static long PixelArea(int width, int height) =>
        Math.Max(0, (long)width) * Math.Max(0, (long)height);

    private static long PixelArea(RemoteImageInfo image) =>
        PixelArea(image.Width ?? 0, image.Height ?? 0);

    private static bool IsLandscape(int width, int height) =>
        width > 0 && height > 0 && width / (double)height >= MinimumEpisodeStillAspectRatio;

    private static bool IsMovieDb(string? providerName) =>
        string.Equals(providerName?.Trim(), "TheMovieDb", StringComparison.OrdinalIgnoreCase);
}
