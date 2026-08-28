using System;
using System.Globalization;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;

namespace CoverFixer;

internal static class TmdbLanguageResolver
{
    public static bool TryResolve(
        BaseItem item,
        ILibraryManager libraryManager,
        out string mediaType,
        out string tmdbId)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(libraryManager);

        BaseItem? lookupItem;
        if (item is Movie)
        {
            mediaType = "movie";
            lookupItem = item;
        }
        else
        {
            mediaType = "tv";
            lookupItem = item switch
            {
                Series => item,
                Season season => season.Series,
                Episode episode => episode.Series,
                _ => null,
            };

            if (lookupItem is null && item.SeriesId > 0)
            {
                lookupItem = libraryManager.GetItemById(item.SeriesId);
            }
        }

        if (lookupItem is not null
            && lookupItem.ProviderIds.TryGetValue("Tmdb", out string? value)
            && long.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out long numericId)
            && numericId > 0)
        {
            tmdbId = numericId.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        tmdbId = string.Empty;
        return false;
    }
}
