using System.Net;
using System.Net.Http.Headers;

namespace VidShrink.Core.Share;

/// <summary>
/// Bir dosyayı gövde olarak taşıyan, gönderdikçe ilerleme bildiren içerik.
/// </summary>
/// <remarks>
/// Uzunluk <see cref="TryComputeLength"/> ile bildirilir: ön imzalı R2 adresleri parçalı
/// (<c>chunked</c>) gövde kabul etmez, <c>Content-Length</c> şart.
/// </remarks>
public sealed class ProgressFileContent : HttpContent
{
    private const int BufferSize = 128 * 1024;

    private readonly string _path;
    private readonly long _length;
    private readonly IProgress<UploadProgress>? _progress;
    private readonly CancellationToken _cancellationToken;

    public ProgressFileContent(
        string path,
        string contentType,
        IProgress<UploadProgress>? progress,
        CancellationToken cancellationToken)
    {
        _path = path;
        _length = new FileInfo(path).Length;
        _progress = progress;
        _cancellationToken = cancellationToken;
        Headers.ContentType = new MediaTypeHeaderValue(contentType);
        Headers.ContentLength = _length;
    }

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        await using var file = new FileStream(
            _path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);

        var buffer = new byte[BufferSize];
        long sent = 0;
        _progress?.Report(new UploadProgress(0, _length));

        int read;
        while ((read = await file.ReadAsync(buffer, _cancellationToken).ConfigureAwait(false)) > 0)
        {
            await stream.WriteAsync(buffer.AsMemory(0, read), _cancellationToken).ConfigureAwait(false);
            sent += read;
            _progress?.Report(new UploadProgress(sent, _length));
        }
    }

    protected override bool TryComputeLength(out long length)
    {
        length = _length;
        return true;
    }
}

/// <summary>Dosya uzantısından MIME türü. Sağlayıcılar bunu init ve PUT adımında aynı kullanır.</summary>
public static class MediaTypes
{
    public static string ForFile(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".mp4" or ".m4v" => "video/mp4",
        ".mkv" => "video/x-matroska",
        ".mov" => "video/quicktime",
        ".webm" => "video/webm",
        ".avi" => "video/x-msvideo",
        ".gif" => "image/gif",
        _ => "application/octet-stream"
    };
}
