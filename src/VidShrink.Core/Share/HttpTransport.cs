using System.Reflection;

namespace VidShrink.Core.Share;

/// <summary>
/// HTTP katmanı. Testler bunun yerine kendi uygulamasını koyar; ağa çıkılmaz.
/// </summary>
public interface IHttpTransport
{
    Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken);
}

/// <summary>Gerçek ağa çıkan taşıyıcı.</summary>
public sealed class HttpClientTransport : IHttpTransport, IDisposable
{
    private readonly HttpClient _client;
    private readonly bool _owned;

    public HttpClientTransport(HttpClient? client = null)
    {
        _owned = client is null;
        _client = client ?? new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
    }

    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

    public void Dispose()
    {
        if (_owned) _client.Dispose();
    }
}

/// <summary>
/// İsteklerde uygulamayı adıyla ve sürümüyle tanıtan kimlik.
/// </summary>
/// <remarks>
/// Sebep hukuki değil pratik: kötüye kullanım şikâyeti kullanıcının IP'sine değil uygulamaya
/// gitsin ve bize haber gelsin. Sağlayıcılar anonim yüklemede kimlik doğrulamıyor, geriye
/// tek iz olarak bu başlık kalıyor.
/// </remarks>
public static class ShareIdentity
{
    /// <summary>Uygulamanın adı. Sağlayıcı günlüklerinde bu görünür.</summary>
    public const string ProductName = "VidShrink";

    /// <summary>İletişim adresi. Şikâyet buraya gelsin.</summary>
    public const string ContactUrl = "https://github.com/Teknesyum/VidShrink";

    private static string? _userAgent;

    /// <summary><c>VidShrink/1.2.3 (+https://...)</c> biçiminde <c>User-Agent</c> değeri.</summary>
    public static string UserAgent => _userAgent ??= Build();

    private static string Build()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version
                      ?? Assembly.GetExecutingAssembly().GetName().Version;
        var shown = version is null ? "0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
        return $"{ProductName}/{shown} (+{ContactUrl})";
    }

    /// <summary>İsteğe kimlik başlığını koyar. Her sağlayıcı her istekte bunu çağırır.</summary>
    public static HttpRequestMessage Stamp(HttpRequestMessage request)
    {
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        return request;
    }
}
