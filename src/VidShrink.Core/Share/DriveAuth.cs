using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;

namespace VidShrink.Core.Share;

/// <summary>PKCE üçlüsü. Doğrulayıcı gizli kalır, meydan okuma yetkilendirme adresine gider.</summary>
public sealed record PkcePair(string Verifier, string Challenge)
{
    public const string Method = "S256";
}

/// <summary>Tarayıcı akışının sonucu: kod ve onu üreten doğrulayıcı.</summary>
public sealed record AuthorizationCode(string Code, string Verifier, string RedirectUri);

/// <summary>
/// OAuth 2 yüklü uygulama akışı: yerel geri döngü adresi + PKCE.
/// </summary>
/// <remarks>
/// <para>
/// <b>client_secret gömülmez.</b> Yüklü uygulama akışında bu alan Google'ın kendi belgesinde
/// "Optional" (native-app kılavuzu, 2026-08-07). VidShrink AGPL ve kaynağı açık; koda gömülen
/// bir sır zaten sır değildir. Bu sınıfın hiçbir yerinde istemci sırrı ne tutulur ne gönderilir.
/// </para>
/// <para>
/// <b>Kapsam yalnız <c>drive.file</c>.</b> Daha genişi istenmez. Bu kapsam Google tarafından
/// hassas sayılmaz; hassas olmadığı için doğrulanmamış uygulamaya uygulanan 100 kullanıcı tavanı
/// ve uyarı ekranı devreye girmez. Kapsam genişletilirse bu kazanç kaybolur.
/// </para>
/// </remarks>
public static class DriveAuth
{
    /// <summary>Uygulamanın kendi oluşturduğu dosyalar. Kullanıcının Drive'ının tamamı değil.</summary>
    public const string Scope = "https://www.googleapis.com/auth/drive.file";

    public const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";

    public const string TokenEndpoint = "https://oauth2.googleapis.com/token";

    /// <summary>Rastgele bir doğrulayıcı ve onun SHA-256 meydan okuması.</summary>
    public static PkcePair CreatePkce()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var verifier = Base64Url(bytes);
        return new PkcePair(verifier, ChallengeFor(verifier));
    }

    /// <summary>Verilen doğrulayıcının S256 meydan okuması.</summary>
    public static string ChallengeFor(string verifier) =>
        Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    /// <summary>Çapraz istek sahteciliğine karşı kullanılan tek seferlik değer.</summary>
    public static string CreateState() => Base64Url(RandomNumberGenerator.GetBytes(16));

    public static string BuildAuthorizationUrl(string clientId, string redirectUri, PkcePair pkce, string state)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["client_id"] = clientId;
        query["redirect_uri"] = redirectUri;
        query["response_type"] = "code";
        query["scope"] = Scope;
        query["code_challenge"] = pkce.Challenge;
        query["code_challenge_method"] = PkcePair.Method;
        query["state"] = state;
        // Yenileme jetonu ancak çevrimdışı erişim istenirse ve onay yeniden alınırsa gelir.
        query["access_type"] = "offline";
        query["prompt"] = "consent";
        return $"{AuthorizationEndpoint}?{query}";
    }

    /// <summary>Geri döngü dinleyicisi için kullanılabilir bir kapı bulur.</summary>
    public static int FreeLoopbackPort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        try
        {
            return ((IPEndPoint)probe.LocalEndpoint).Port;
        }
        finally
        {
            probe.Stop();
        }
    }

    public static string LoopbackRedirectUri(int port) => $"http://127.0.0.1:{port}/";

    /// <summary>
    /// Yetkilendirme adresini açtırır ve geri döngüye düşen kodu bekler. Tarayıcıyı açma işi
    /// çağırana bırakılır: bu katman süreç başlatmaz.
    /// </summary>
    public static async Task<AuthorizationCode> ListenForCodeAsync(
        string clientId,
        Action<string> openBrowser,
        CancellationToken cancellationToken)
    {
        var port = FreeLoopbackPort();
        var redirectUri = LoopbackRedirectUri(port);
        var pkce = CreatePkce();
        var state = CreateState();

        using var listener = new HttpListener();
        listener.Prefixes.Add(redirectUri);
        listener.Start();
        try
        {
            openBrowser(BuildAuthorizationUrl(clientId, redirectUri, pkce, state));

            using var registration = cancellationToken.Register(listener.Abort);
            var context = await listener.GetContextAsync();
            var parameters = HttpUtility.ParseQueryString(context.Request.Url?.Query ?? string.Empty);
            var code = parameters["code"];
            var error = parameters["error"];
            var returnedState = parameters["state"];

            var message = code is not null && returnedState == state
                ? "VidShrink Drive izni alındı. Bu sekmeyi kapatabilirsiniz."
                : "VidShrink Drive izni alınamadı. Bu sekmeyi kapatabilirsiniz.";
            await WriteAsync(context.Response, message);

            if (error is not null) throw new DriveAuthException(MapAuthError(error), error);
            if (returnedState != state)
            {
                throw new DriveAuthException(ShareFailure.NotAuthorized, "Yanıttaki state değeri eşleşmedi.");
            }
            if (code is null) throw new DriveAuthException(ShareFailure.NotAuthorized, "Yetkilendirme kodu gelmedi.");

            return new AuthorizationCode(code, pkce.Verifier, redirectUri);
        }
        finally
        {
            if (listener.IsListening) listener.Stop();
        }
    }

    /// <summary>Kodu jetona çevirir. İstemci sırrı gönderilmez.</summary>
    public static async Task<DriveTokens> ExchangeCodeAsync(
        IHttpTransport transport,
        string clientId,
        AuthorizationCode authorization,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["code"] = authorization.Code,
            ["code_verifier"] = authorization.Verifier,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = authorization.RedirectUri
        };
        var payload = await PostFormAsync(transport, form, cancellationToken);
        var refresh = payload.RefreshToken
            ?? throw new DriveAuthException(ShareFailure.NotAuthorized, "Yenileme jetonu dönmedi.");
        return new DriveTokens(refresh, payload.AccessToken, now.AddSeconds(payload.ExpiresIn));
    }

    /// <summary>Yenileme jetonuyla yeni erişim jetonu alır.</summary>
    public static async Task<DriveTokens> RefreshAsync(
        IHttpTransport transport,
        string clientId,
        string refreshToken,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token"
        };
        var payload = await PostFormAsync(transport, form, cancellationToken);
        // Google yenilemede yeni bir refresh_token göndermez; eldeki korunur.
        return new DriveTokens(payload.RefreshToken ?? refreshToken, payload.AccessToken, now.AddSeconds(payload.ExpiresIn));
    }

    private static async Task<TokenPayload> PostFormAsync(
        IHttpTransport transport,
        Dictionary<string, string> form,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(form)
        };

        HttpResponseMessage response;
        try
        {
            response = await transport.SendAsync(request, cancellationToken);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new DriveAuthException(ShareFailure.NetworkFailure, e.Message);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var error = ReadError(body);
                throw new DriveAuthException(MapAuthError(error), string.IsNullOrEmpty(body) ? error : body);
            }

            try
            {
                using var document = JsonDocument.Parse(body);
                var root = document.RootElement;
                return new TokenPayload(
                    root.TryGetProperty("access_token", out var access) ? access.GetString() : null,
                    root.TryGetProperty("refresh_token", out var refresh) ? refresh.GetString() : null,
                    root.TryGetProperty("expires_in", out var expires) ? expires.GetInt32() : 0);
            }
            catch (JsonException e)
            {
                throw new DriveAuthException(ShareFailure.ServiceError, e.Message);
            }
        }
    }

    private static string ReadError(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("error", out var error)
                ? error.GetString() ?? "unknown"
                : "unknown";
        }
        catch (JsonException)
        {
            return "unknown";
        }
    }

    internal static ShareFailure MapAuthError(string error) => error switch
    {
        "access_denied" => ShareFailure.NotAuthorized,
        "invalid_grant" => ShareFailure.TokenExpired,
        "expired_token" => ShareFailure.TokenExpired,
        "invalid_client" => ShareFailure.NotAuthorized,
        "invalid_scope" => ShareFailure.NotAuthorized,
        "slow_down" => ShareFailure.RateLimited,
        _ => ShareFailure.ServiceError
    };

    private static async Task WriteAsync(HttpListenerResponse response, string message)
    {
        var html = Encoding.UTF8.GetBytes(
            $"<!doctype html><meta charset=\"utf-8\"><title>VidShrink</title><p>{WebUtility.HtmlEncode(message)}</p>");
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = html.Length;
        await response.OutputStream.WriteAsync(html);
        response.Close();
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed record TokenPayload(string? AccessToken, string? RefreshToken, int ExpiresIn);
}

/// <summary>Kimlik doğrulama adımında oluşan, sınıflandırılmış hata.</summary>
public sealed class DriveAuthException : Exception
{
    public DriveAuthException(ShareFailure failure, string detail) : base(detail) => Failure = failure;

    public ShareFailure Failure { get; }
}

/// <summary>Geçerli bir erişim jetonu veren kaynak.</summary>
public interface IAccessTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Jetonu saklama yerinden okur, süresi dolduğunda yeniler ve geri yazar. Saklama yeri
/// kalıcı değilse (Windows dışı) yenileme jetonu yalnız bu nesne yaşadığı sürece durur.
/// </summary>
public sealed class DriveSession : IAccessTokenProvider
{
    private readonly IHttpTransport _transport;
    private readonly string _clientId;
    private readonly ITokenStore _store;
    private readonly Func<DateTimeOffset> _clock;

    public DriveSession(IHttpTransport transport, string clientId, ITokenStore store, Func<DateTimeOffset>? clock = null)
    {
        _transport = transport;
        _clientId = clientId;
        _store = store;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>Elde kullanılabilir bir yenileme jetonu var mı; yoksa tarayıcı akışı gerekir.</summary>
    public bool HasSession => _store.Load() is not null;

    public ITokenStore Store => _store;

    /// <summary>Tarayıcı akışından dönen kodu jetona çevirir ve saklar.</summary>
    public async Task<DriveTokens> CompleteAsync(AuthorizationCode authorization, CancellationToken cancellationToken)
    {
        var tokens = await DriveAuth.ExchangeCodeAsync(_transport, _clientId, authorization, _clock(), cancellationToken);
        _store.Save(tokens);
        return tokens;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var tokens = _store.Load()
            ?? throw new DriveAuthException(ShareFailure.NotAuthorized, "Drive izni henüz alınmadı.");

        var now = _clock();
        if (tokens.AccessUsableAt(now)) return tokens.AccessToken!;

        var refreshed = await DriveAuth.RefreshAsync(_transport, _clientId, tokens.RefreshToken, now, cancellationToken);
        if (string.IsNullOrEmpty(refreshed.AccessToken))
        {
            throw new DriveAuthException(ShareFailure.TokenExpired, "Yenilemeden erişim jetonu dönmedi.");
        }
        _store.Save(refreshed);
        return refreshed.AccessToken;
    }

    /// <summary>Jetonu yerelden siler. Sunucu tarafındaki izin ayrıca geri çekilebilir.</summary>
    public void SignOut() => _store.Clear();
}
