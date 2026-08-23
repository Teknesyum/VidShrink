using System.Net;
using System.Net.Http.Headers;
using VidShrink.Core;

namespace VidShrink.Launcher;

/// <summary>
/// Sessiz fark güncellemesi. Manifest tek başına çekilir, yalnız özeti tutmayan dosyalar
/// arşivden aralık isteğiyle indirilir, hepsi doğrulandıktan sonra uygulama klasörüne
/// geçer. Hiçbir hata açılışı engellemez.
/// </summary>
internal static class Updater
{
    /// <summary>Manifest çekmenin zaman aşımı; açılışın gecikebileceği en uzun süre budur.</summary>
    private static readonly TimeSpan ManifestTimeout = TimeSpan.FromSeconds(2);

    /// <summary>İndirme dahil tüm güncellemenin üst sınırı.</summary>
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(90);

    private const string StageDirectoryName = "update-stage";
    private const string HashCacheName = ".update-hashes.json";

    public static void Run(string baseDirectory, string appDirectory)
    {
        // Ayar kapalıyken manifest bile çekilmez: kapatan kullanıcı ağ turunu da istemiyor.
        if (!UpdateCheck.AutoUpdateEnabled()) return;
        if (Environment.GetEnvironmentVariable("VIDSHRINK_UPDATE_DISABLED") == "1") return;

        using var cancellation = new CancellationTokenSource(Budget);
        try { RunAsync(baseDirectory, appDirectory, cancellation.Token).GetAwaiter().GetResult(); }
        catch (Exception) { }
    }

    private static async Task RunAsync(string baseDirectory, string appDirectory, CancellationToken cancellationToken)
    {
        var rid = UpdateCheck.Rid;
        var source = Environment.GetEnvironmentVariable("VIDSHRINK_UPDATE_SOURCE");

        var json = await FetchManifestAsync(rid, source, cancellationToken);
        if (json is null) return;

        var manifest = UpdateCheck.ParseManifest(json);
        if (manifest.Files.Count == 0) return;

        // Aynı sürüm zaten uygulanmışsa hiçbir dosya özetlenmez.
        if (UpdateCheck.ReadVersionMarker(appDirectory) == manifest.Version) return;

        var cache = new HashCache(Path.Combine(appDirectory, HashCacheName));
        var changed = UpdateCheck.Diff(appDirectory, manifest, cache);
        cache.Save();
        if (changed.Count == 0)
        {
            UpdateCheck.WriteVersionMarker(appDirectory, manifest.Version);
            return;
        }

        var stage = Path.Combine(baseDirectory, StageDirectoryName);
        UpdateStage.Discard(stage);
        Directory.CreateDirectory(stage);

        try
        {
            var archive = await RemoteZip.OpenAsync(ArchiveSource(rid, source), cancellationToken);
            foreach (var file in changed)
            {
                var entry = archive.Resolve(file.Path)
                    ?? throw new FileNotFoundException($"Arşivde yok: {file.Path}");
                var bytes = await archive.ExtractAsync(entry, cancellationToken);

                // İnen her dosyanın özeti manifesttekiyle karşılaştırılır; tutmayan atılır.
                if (!string.Equals(UpdateCheck.HashBytes(bytes), file.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"İnen dosyanın özeti tutmadı: {file.Path}");

                var target = UpdateCheck.LocalPath(stage, file.Path);
                var folder = Path.GetDirectoryName(target);
                if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
                await File.WriteAllBytesAsync(target, bytes, cancellationToken);
            }
        }
        catch (Exception)
        {
            UpdateStage.Discard(stage);
            throw;
        }

        UpdateStage.Apply(stage, appDirectory, changed);
        UpdateCheck.WriteVersionMarker(appDirectory, manifest.Version);
    }

    private static async Task<string?> FetchManifestAsync(string rid, string? source, CancellationToken cancellationToken)
    {
        var asset = UpdateCheck.ManifestAssetName(rid);
        if (source is not null)
        {
            var local = Path.Combine(source, asset);
            return File.Exists(local) ? await File.ReadAllTextAsync(local, cancellationToken) : null;
        }

        using var client = new HttpClient { Timeout = ManifestTimeout };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("VidShrink-Launcher");

        using var response = await client.GetAsync(AssetUrl(asset), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static IRangeSource ArchiveSource(string rid, string? source)
    {
        var asset = UpdateCheck.ArchiveAssetName(rid);
        if (source is not null) return new FileRangeSource(Path.Combine(source, asset));
        return new HttpRangeSource(AssetUrl(asset));
    }

    /// <summary>Yayın adresi; ölçüm ve deneme için VIDSHRINK_UPDATE_BASE_URL ile değiştirilebilir.</summary>
    private static string AssetUrl(string asset)
    {
        var baseUrl = Environment.GetEnvironmentVariable("VIDSHRINK_UPDATE_BASE_URL");
        return string.IsNullOrWhiteSpace(baseUrl)
            ? UpdateCheck.LatestAssetUrl(asset)
            : baseUrl.TrimEnd('/') + "/" + asset;
    }
}

/// <summary>Yayın arşivinin yalnız istenen bayt aralığını çeken kaynak.</summary>
internal sealed class HttpRangeSource : IRangeSource
{
    private readonly string _url;
    private readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(30) };
    private long _length = -1;

    public HttpRangeSource(string url)
    {
        _url = url;
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("VidShrink-Launcher");
    }

    public async Task<long> LengthAsync(CancellationToken cancellationToken)
    {
        if (_length >= 0) return _length;
        using var request = new HttpRequestMessage(HttpMethod.Head, _url);
        using var response = await _client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        _length = response.Content.Headers.ContentLength
            ?? throw new InvalidDataException("Arşivin boyutu bildirilmedi.");
        return _length;
    }

    public async Task<byte[]> ReadAsync(long offset, int length, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _url);
        request.Headers.Range = new RangeHeaderValue(offset, offset + length - 1);
        using var response = await _client.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.PartialContent)
            throw new InvalidDataException("Sunucu aralık isteğini karşılamadı.");
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }
}
