using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using VidShrink.Core.Share;

namespace VidShrink.Core.Subtitles;

/// <summary>
/// OpenSubtitles.com REST API v1 sağlayıcısı.
/// </summary>
/// <remarks>
/// Anahtar <b>kullanıcınındır</b> ve depoda durmaz: kullanıcı Ayarlar'da kendi anahtarını
/// girer, anahtar <c>settings.json</c>'a yazılır. Açık kaynak bir depoya gömülen uygulama
/// anahtarı bütün kurulumların kotasını paylaşır ve sağlayıcı tarafından iptal edilir.
/// Ağ <see cref="IHttpTransport"/> arkasındadır; testler oraya sahte cevap koyar.
/// </remarks>
public sealed class OpenSubtitlesProvider : ISubtitleProvider
{
    /// <summary>Kullanıcının anahtarını aldığı sayfa. Anahtar yokken arayüz bunu açar.</summary>
    public const string KeyPageUrl = "https://www.opensubtitles.com/consumers";

    private const string DefaultBaseUrl = "https://api.opensubtitles.com/api/v1";

    private readonly IHttpTransport _transport;
    private readonly string _apiKey;
    private readonly string _baseUrl;

    public OpenSubtitlesProvider(IHttpTransport transport, string? apiKey, string? baseUrl = null)
    {
        _transport = transport;
        _apiKey = (apiKey ?? "").Trim();
        _baseUrl = (baseUrl ?? DefaultBaseUrl).TrimEnd('/');
    }

    public bool IsConfigured => _apiKey.Length > 0;

    public async Task<SubtitleSearchResult> SearchAsync(SubtitleQuery query, CancellationToken cancellationToken)
    {
        if (!IsConfigured) return SubtitleSearchResult.Failed(SubtitleOutcome.NoKey);

        var languages = Languages(query.Languages);

        if (!string.IsNullOrEmpty(query.MovieHash))
        {
            var byHash = await Ask(languages + "moviehash=" + query.MovieHash.ToLowerInvariant(), query, cancellationToken).ConfigureAwait(false);
            if (byHash.Outcome != SubtitleOutcome.NoResult) return byHash;
        }

        if (string.IsNullOrWhiteSpace(query.Name)) return SubtitleSearchResult.Failed(SubtitleOutcome.NoResult);
        return await Ask(languages + "query=" + Escape(query.Name), query, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// <c>languages</c> virgülle ayrılmış ve <b>sıralı</b> gider; sağlayıcı sırasız listeyi
    /// reddediyor. Yeğleme sırası burada değil <see cref="Rank"/>'te uygulanır.
    /// </summary>
    private static string Languages(IReadOnlyList<string> preferred)
    {
        // Parametreler alfabetik sirada gidiyor (languages < moviehash < query); saglayici
        // sirasiz ya da buyuk harfli istegi yonlendirmeyle karsiliyor ve yonlendirme kota
        // sayacina ikinci bir istek olarak dusebiliyor.
        var codes = preferred
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToList();
        return codes.Count == 0 ? "" : "languages=" + string.Join(",", codes) + "&";
    }

    /// <summary>
    /// Saglayici sorgu degerinde boslugu <c>+</c> olarak istiyor; <c>%20</c> yonlendirmeye
    /// yol aciyor. Kucuk harf de ayni sebeple.
    /// </summary>
    private static string Escape(string value)
        => Uri.EscapeDataString(value.Trim().ToLowerInvariant()).Replace("%20", "+", StringComparison.Ordinal);

    private async Task<SubtitleSearchResult> Ask(string queryString, SubtitleQuery query, CancellationToken cancellationToken)
    {
        using var request = Stamp(new HttpRequestMessage(HttpMethod.Get, _baseUrl + "/subtitles?" + queryString));

        HttpResponseMessage response;
        try
        {
            response = await _transport.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or IOException)
        {
            return SubtitleSearchResult.Failed(SubtitleOutcome.NetworkError);
        }

        using (response)
        {
            if (Trouble(response.StatusCode) is { } trouble) return SubtitleSearchResult.Failed(trouble);

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var found = Parse(body);
            if (found is null) return SubtitleSearchResult.Failed(SubtitleOutcome.NetworkError);
            if (found.Count == 0) return SubtitleSearchResult.Failed(SubtitleOutcome.NoResult);
            return new SubtitleSearchResult(SubtitleOutcome.Ok, Rank(found, query.Languages));
        }
    }

    /// <summary>
    /// Sıralama: tam eşleşen hash önce, sonra yeğlenen dil sırası, sonra indirilme sayısı.
    /// Arayüz ilk sırayı öne koyar, kullanıcı listeden başkasını seçebilir.
    /// </summary>
    private static List<SubtitleCandidate> Rank(List<SubtitleCandidate> found, IReadOnlyList<string> preferred)
    {
        var order = preferred
            .Select((code, index) => (Code: (code ?? "").Trim().ToLowerInvariant(), Index: index))
            .Where(pair => pair.Code.Length > 0)
            .GroupBy(pair => pair.Code, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Min(pair => pair.Index), StringComparer.Ordinal);

        return found
            .OrderByDescending(candidate => candidate.HashMatch)
            .ThenBy(candidate => order.TryGetValue(candidate.Language.ToLowerInvariant(), out var index) ? index : int.MaxValue)
            .ThenByDescending(candidate => candidate.DownloadCount)
            .ThenBy(candidate => candidate.FileId)
            .ToList();
    }

    private static List<SubtitleCandidate>? Parse(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return null;
            if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array) return null;

            var found = new List<SubtitleCandidate>();
            foreach (var item in data.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                if (!item.TryGetProperty("attributes", out var attributes) || attributes.ValueKind != JsonValueKind.Object) continue;
                if (!attributes.TryGetProperty("files", out var files) || files.ValueKind != JsonValueKind.Array) continue;

                foreach (var file in files.EnumerateArray())
                {
                    if (file.ValueKind != JsonValueKind.Object) continue;
                    if (!file.TryGetProperty("file_id", out var id) || !id.TryGetInt64(out var fileId)) continue;

                    found.Add(new SubtitleCandidate(
                        fileId,
                        Text(attributes, "language"),
                        Text(attributes, "release"),
                        Text(file, "file_name"),
                        Number(attributes, "download_count"),
                        attributes.TryGetProperty("moviehash_match", out var match) && match.ValueKind == JsonValueKind.True));
                }
            }

            return found;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Text(JsonElement parent, string name)
        => parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

    private static int Number(JsonElement parent, string name)
        => parent.TryGetProperty(name, out var value) && value.TryGetInt32(out var found) ? found : 0;

    public async Task<SubtitleDownloadResult> DownloadAsync(SubtitleCandidate candidate, string mediaPath, CancellationToken cancellationToken)
    {
        if (!IsConfigured) return SubtitleDownloadResult.Failed(SubtitleOutcome.NoKey);

        string link;
        var remaining = -1;
        using (var request = Stamp(new HttpRequestMessage(HttpMethod.Post, _baseUrl + "/download")))
        {
            request.Content = new StringContent(
                "{\"file_id\":" + candidate.FileId.ToString(CultureInfo.InvariantCulture) + ",\"sub_format\":\"srt\"}",
                Encoding.UTF8,
                "application/json");

            HttpResponseMessage response;
            try
            {
                response = await _transport.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or IOException)
            {
                return SubtitleDownloadResult.Failed(SubtitleOutcome.NetworkError);
            }

            using (response)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (Trouble(response.StatusCode) is { } trouble)
                    return SubtitleDownloadResult.Failed(Sharpen(trouble, body), Quota(body));

                try
                {
                    using var document = JsonDocument.Parse(body);
                    remaining = Quota(body);
                    if (!document.RootElement.TryGetProperty("link", out var value) || value.ValueKind != JsonValueKind.String)
                        return SubtitleDownloadResult.Failed(SubtitleOutcome.NetworkError, remaining);
                    link = value.GetString() ?? "";
                }
                catch (JsonException)
                {
                    return SubtitleDownloadResult.Failed(SubtitleOutcome.NetworkError);
                }

                if (link.Length == 0) return SubtitleDownloadResult.Failed(SubtitleOutcome.NetworkError, remaining);
            }
        }

        byte[] bytes;
        using (var fetch = new HttpRequestMessage(HttpMethod.Get, link))
        {
            // Imzali, uc saat gecerli baglanti; kendi kendini yetkilendiriyor. Anahtar
            // gondermiyoruz, yalniz kimlik basligi gidiyor.
            fetch.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
            HttpResponseMessage response;
            try
            {
                response = await _transport.SendAsync(fetch, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or IOException)
            {
                return SubtitleDownloadResult.Failed(SubtitleOutcome.NetworkError, remaining);
            }

            using (response)
            {
                if (Trouble(response.StatusCode) is { } trouble) return SubtitleDownloadResult.Failed(trouble, remaining);
                if (!response.IsSuccessStatusCode) return SubtitleDownloadResult.Failed(SubtitleOutcome.NetworkError, remaining);
                bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        if (bytes.Length == 0) return SubtitleDownloadResult.Failed(SubtitleOutcome.NetworkError, remaining);

        try
        {
            var target = SidecarPath(mediaPath, candidate.Language);
            File.WriteAllBytes(target, bytes);
            return new SubtitleDownloadResult(SubtitleOutcome.Ok, target, remaining);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return SubtitleDownloadResult.Failed(SubtitleOutcome.WriteError, remaining);
        }
    }

    /// <summary>
    /// Videonun yanına <c>&lt;ad&gt;.&lt;dil&gt;.srt</c>. Aynı adlı dosya varsa üstüne
    /// yazılmaz, sonuna sayı eklenir; kullanıcının kendi altyazısı ezilmez.
    /// </summary>
    public static string SidecarPath(string mediaPath, string language)
    {
        var full = Path.GetFullPath(mediaPath);
        var folder = Path.GetDirectoryName(full) ?? ".";
        var stem = Path.GetFileNameWithoutExtension(full);
        var tag = string.IsNullOrWhiteSpace(language) ? "" : "." + language.Trim().ToLowerInvariant();

        var candidate = Path.Combine(folder, stem + tag + ".srt");
        var counter = 2;
        while (File.Exists(candidate))
        {
            candidate = Path.Combine(folder, stem + tag + "." + counter.ToString(CultureInfo.InvariantCulture) + ".srt");
            counter++;
        }

        return candidate;
    }

    /// <summary>
    /// Sağlayıcının durum kodu → kullanıcıya dönen kol. Başarılıysa <c>null</c>.
    /// 401/403 anahtarın kendisini, 406 günlük kotayı, 429 istek sınırını gösteriyor.
    /// </summary>
    private static SubtitleOutcome? Trouble(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => SubtitleOutcome.BadKey,
        HttpStatusCode.NotAcceptable => SubtitleOutcome.QuotaExceeded,
        HttpStatusCode.TooManyRequests => SubtitleOutcome.RateLimited,
        _ when (int)status >= 400 => SubtitleOutcome.NetworkError,
        _ => null
    };

    /// <summary>
    /// <c>/download</c> kota dolunca 401 de donebiliyor; govdede kalan hak ve yenilenme
    /// saati varsa bu, anahtarin reddi degil kotanin dolmasidir. Ayrimi yapmayan istemci
    /// sonsuz yeniden giris dongusune giriyor.
    /// </summary>
    private static SubtitleOutcome Sharpen(SubtitleOutcome trouble, string body)
    {
        if (trouble != SubtitleOutcome.BadKey) return trouble;
        return body.Contains("reset_time", StringComparison.OrdinalIgnoreCase)
               || body.Contains("\"remaining\"", StringComparison.OrdinalIgnoreCase)
            ? SubtitleOutcome.QuotaExceeded
            : trouble;
    }

    private static int Quota(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.ValueKind == JsonValueKind.Object
                   && document.RootElement.TryGetProperty("remaining", out var left)
                   && left.TryGetInt32(out var howMany)
                ? howMany
                : -1;
        }
        catch (JsonException)
        {
            return -1;
        }
    }

    /// <summary>
    /// Saglayicinin istedigi bicim: <c>Uygulama vX.Y.Z</c>. Baska bicimde gonderilen kimlik
    /// 403 ile geri cevriliyor, bu yuzden <see cref="ShareIdentity"/>'nin
    /// <c>ad/surum (+adres)</c> bicimi burada kullanilamaz.
    /// </summary>
    internal static string UserAgent { get; } = ShareIdentity.ProductName + " v" + Version();

    private static string Version()
    {
        var version = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version
                      ?? System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        return version is null
            ? "0.0.0"
            : version.Major.ToString(CultureInfo.InvariantCulture) + "." +
              version.Minor.ToString(CultureInfo.InvariantCulture) + "." +
              version.Build.ToString(CultureInfo.InvariantCulture);
    }

    private HttpRequestMessage Stamp(HttpRequestMessage request)
    {
        request.Headers.TryAddWithoutValidation("Api-Key", _apiKey);
        request.Headers.TryAddWithoutValidation("Accept", "*/*");
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        return request;
    }
}
