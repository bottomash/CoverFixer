using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CoverFixer;

internal sealed class TmdbLanguageClient
{
    private static readonly HttpClient HttpClient = new()
    {
        BaseAddress = new Uri("https://api.themoviedb.org/3/"),
        Timeout = TimeSpan.FromSeconds(15),
    };

    private readonly ConcurrentDictionary<string, string> _languageCache =
        new(StringComparer.OrdinalIgnoreCase);

    public async Task<string?> GetOriginalLanguage(
        string mediaType,
        string tmdbId,
        string readAccessToken,
        CancellationToken cancellationToken)
    {
        string cacheKey = $"{mediaType}:{tmdbId}";
        if (_languageCache.TryGetValue(cacheKey, out string? cached))
        {
            return cached;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{mediaType}/{tmdbId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", readAccessToken.Trim());
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using HttpResponseMessage response = await HttpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using JsonDocument document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        string? language = ReadOriginalLanguage(document.RootElement);
        if (!string.IsNullOrWhiteSpace(language))
        {
            language = language.Trim();
            _languageCache.TryAdd(cacheKey, language);
        }

        return language;
    }

    internal static string? ReadOriginalLanguage(JsonElement root) =>
        root.TryGetProperty("original_language", out JsonElement value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
