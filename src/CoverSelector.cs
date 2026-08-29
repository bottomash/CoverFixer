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

    public static RemoteImageInfo? Select(
        IEnumerable<RemoteImageInfo> images,
        string? originalLanguage = null)
    {
        ArgumentNullException.ThrowIfNull(images);

        return images
            .Where(image => image is not null && !string.IsNullOrWhiteSpace(image.Url))
            .Select(image => new
            {
                Image = image,
                LanguageRank = GetLanguageRank(image.Language, originalLanguage),
            })
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
        long minimumPixelArea = 0,
        string? originalLanguage = null)
    {
        ArgumentNullException.ThrowIfNull(images);

        return images
            .Where(image =>
                image is not null
                && !string.IsNullOrWhiteSpace(image.Url)
                && IsLandscape(image.Width ?? 0, image.Height ?? 0)
                && PixelArea(image) > minimumPixelArea)
            .OrderBy(image => IsMovieDb(image.ProviderName) ? 0 : 1)
            .ThenBy(image => GetLanguageRank(image.Language, originalLanguage))
            .ThenByDescending(PixelArea)
            .ThenByDescending(image => image.CommunityRating ?? double.MinValue)
            .ThenByDescending(image => image.VoteCount ?? int.MinValue)
            .ThenBy(image => image.ProviderName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    public static bool IsEpisodePosterLike(int width, int height) =>
        width > 0 && height > 0 && !IsLandscape(width, height);

    internal static int GetLanguageRank(string? language, string? originalLanguage)
    {
        string normalized = NormalizeLanguage(language);
        string normalizedOriginal = NormalizeLanguage(originalLanguage);
        if (!string.IsNullOrEmpty(normalizedOriginal)
            && LanguagesMatch(normalized, normalizedOriginal))
        {
            return 0;
        }

        if (normalized.Equals("en", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("en-", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (SimplifiedChineseLanguages.Contains(normalized))
        {
            return 2;
        }

        return string.IsNullOrEmpty(normalized) ? 3 : 4;
    }

    private static bool LanguagesMatch(string language, string originalLanguage) =>
        !string.IsNullOrEmpty(language)
        && (language.Equals(originalLanguage, StringComparison.OrdinalIgnoreCase)
            || language.StartsWith(originalLanguage + "-", StringComparison.OrdinalIgnoreCase)
            || originalLanguage.StartsWith(language + "-", StringComparison.OrdinalIgnoreCase));

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
