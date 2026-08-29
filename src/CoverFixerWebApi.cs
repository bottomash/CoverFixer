using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Services;

namespace CoverFixer;

[Route("/{Web}/modules/shortcuts.js", "GET", IsHidden = true)]
[Unauthenticated]
public sealed class GetCoverFixerShortcuts
{
    public string Web { get; set; } = string.Empty;
}

[Route("/CoverFixer/Series/{ItemId}/Refresh", "POST", IsHidden = true)]
[Authenticated(Roles = "Admin")]
public sealed class RefreshSeriesCover : IReturn<RefreshSeriesCoverResult>, IReturn
{
    [ApiMember(
        Name = "ItemId",
        Description = "Emby Series numeric Item ID",
        IsRequired = true,
        DataType = "string",
        ParameterType = "path")]
    public string ItemId { get; set; } = string.Empty;
}

public sealed class RefreshSeriesCoverResult
{
    public string ItemId { get; set; } = string.Empty;

    public string TmdbId { get; set; } = string.Empty;

    public string OriginalLanguage { get; set; } = string.Empty;

    public string ImageLanguage { get; set; } = string.Empty;

    public string ProviderName { get; set; } = string.Empty;
}
