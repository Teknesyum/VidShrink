using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace VidShrink.Core.Setup;

public sealed record FetchedFile(string Path, string Sha256);

public static class SetupDownloads
{
    public const string UserAgent = "VidShrink-Setup";

    public static HttpClient CreateClient(bool followRedirects = true)
    {
        var client = new HttpClient(new SocketsHttpHandler
        {
            AllowAutoRedirect = followRedirects,
            MaxConnectionsPerServer = 16,
            AutomaticDecompression = DecompressionMethods.None
        })
        { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        return client;
    }

    public static string TagFromLocation(Uri location)
    {
        var match = Regex.Match(location.AbsolutePath, @"/releases/tag/(?<tag>[^/]+)$");
        if (!match.Success) throw new SetupException(SetupText.Get("setup.release.tag-unreadable", location));
        return Uri.UnescapeDataString(match.Groups["tag"].Value);
    }

    public static async Task<string> ResolveLatestTagAsync(CancellationToken cancellationToken)
    {
        using var client = CreateClient(followRedirects: false);
        using var request = new HttpRequestMessage(HttpMethod.Head, $"https://github.com/{SetupOptions.Repository}/releases/latest");
        using var response = await client.SendAsync(request, cancellationToken);
        var location = response.Headers.Location
            ?? throw new SetupException(SetupText.Get("setup.release.not-found", (int)response.StatusCode));
        if (!location.IsAbsoluteUri) location = new Uri(new Uri("https://github.com"), location);
        return TagFromLocation(location);
    }

    public static string AssetUrl(string tag, string asset) =>
        $"https://github.com/{SetupOptions.Repository}/releases/download/{tag}/{asset}";

    public static Dictionary<string, string> ParseChecksums(string text)
    {
        var table = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in text.Split('\n'))
        {
            var match = Regex.Match(line, @"^([0-9a-fA-F]{64})\s+\*?(.+?)\s*$");
            if (match.Success) table[match.Groups[2].Value] = match.Groups[1].Value.ToLowerInvariant();
        }
        return table;
    }

    public static void AssertChecksum(IReadOnlyDictionary<string, string> table, string name, string actual)
    {
        if (!table.TryGetValue(name, out var expected))
            throw new SetupException(SetupText.Get("setup.checksum.missing", name));
        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            throw new SetupException(SetupText.Get("setup.checksum.mismatch", name, expected, actual));
    }

    public static async Task<FetchedFile> FetchAsync(HttpClient client, string url, string destination, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new SetupException(SetupText.Get("setup.download.failed", (int)response.StatusCode, url));
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await WriteHashedAsync(source, destination, cancellationToken);
    }

    public static async Task<FetchedFile> WriteHashedAsync(Stream source, string destination, CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using (var target = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20, useAsync: true))
        {
            var buffer = new byte[1 << 20];
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                hash.AppendData(buffer, 0, read);
                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
        }
        return new FetchedFile(destination, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
    }

    public static async Task<FetchedFile> FetchAssetAsync(HttpClient client, SetupOptions options, string tag, string asset, string workRoot, CancellationToken cancellationToken)
    {
        if (options.AssetSource is { } folder)
        {
            var local = Path.Combine(folder, asset);
            if (!File.Exists(local)) throw new SetupException(SetupText.Get("setup.asset.missing", tag, asset));
            return new FetchedFile(local, UpdateCheck.HashFile(local));
        }

        try
        {
            return await FetchAsync(client, AssetUrl(tag, asset), Path.Combine(workRoot, asset), cancellationToken);
        }
        catch (SetupException exception) when (exception.Message.Contains("HTTP 404", StringComparison.Ordinal))
        {
            throw new SetupException(SetupText.Get("setup.asset.missing-launcher", tag, asset));
        }
    }

    public static async Task<(string Path, bool Reused)> PrepareLibMpvAsync(
        HttpClient client, LibMpvPin pin, string? existing, string workRoot, Action<string> log, CancellationToken cancellationToken)
    {
        if (existing is not null && File.Exists(existing) &&
            string.Equals(UpdateCheck.HashFile(existing), pin.DllSha256, StringComparison.OrdinalIgnoreCase))
        {
            return (existing, true);
        }

        if (pin.ZipUrl is not null && await TryZipAsync(client, pin, workRoot, log, cancellationToken) is { } zipped)
            return (zipped, false);

        var tar =Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "tar.exe");
        if (!File.Exists(tar)) throw new SetupException(SetupText.Get("setup.libmpv.no-tar", tar));

        var archive = Path.Combine(workRoot, "mpv-dev" + Path.GetExtension(pin.Urls[0]));
        FetchedFile? fetched = null;
        var failures = new List<string>();
        foreach (var url in pin.Urls)
        {
            try
            {
                fetched = url.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? await FetchAsync(client, url, archive, cancellationToken)
                    : await CopyHashedAsync(url, archive, cancellationToken);
            }
            catch (Exception exception) when (exception is SetupException or HttpRequestException or IOException)
            {
                failures.Add(url);
                log(SetupText.Get("setup.libmpv.source-failed", url));
                continue;
            }
            if (string.Equals(fetched.Sha256, pin.ArchiveSha256, StringComparison.OrdinalIgnoreCase)) break;
            log(SetupText.Get("setup.libmpv.checksum-fallback", url));
        }

        if (fetched is null) throw new SetupException(SetupText.Get("setup.libmpv.all-sources-failed", string.Join(", ", failures)));
        if (!string.Equals(fetched.Sha256, pin.ArchiveSha256, StringComparison.OrdinalIgnoreCase))
            throw new SetupException(SetupText.Get("setup.libmpv.archive-mismatch", pin.ArchiveSha256, fetched.Sha256));

        var extract = Path.Combine(workRoot, "libmpv");
        Directory.CreateDirectory(extract);
        var start = new ProcessStartInfo(tar) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in new[] { "-xf", archive, "-C", extract, pin.FileName }) start.ArgumentList.Add(argument);
        using (var process = Process.Start(start) ?? throw new SetupException(SetupText.Get("setup.tar.start-failed")))
        {
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0) throw new SetupException(SetupText.Get("setup.libmpv.extract-failed", process.ExitCode));
        }

        var dll = Path.Combine(extract, pin.FileName);
        if (!File.Exists(dll)) throw new SetupException(SetupText.Get("setup.libmpv.not-in-archive", pin.FileName));
        var actual = UpdateCheck.HashFile(dll);
        if (!string.Equals(actual, pin.DllSha256, StringComparison.OrdinalIgnoreCase))
            throw new SetupException(SetupText.Get("setup.checksum.mismatch", pin.FileName, pin.DllSha256, actual));
        return (dll, false);
    }

    /// <summary>
    /// Zip kaynağı: .NET'in kendi açıcısıyla açılır, tar gerekmez. Windows 10'un tar'ı 7z'yi
    /// açamıyor. Herhangi bir adım tutmazsa <c>null</c> döner ve 7z kaynaklarına düşülür.
    /// </summary>
    private static async Task<string?> TryZipAsync(
        HttpClient client, LibMpvPin pin, string workRoot, Action<string> log, CancellationToken cancellationToken)
    {
        var url = pin.ZipUrl!;
        var archive = Path.Combine(workRoot, "libmpv.zip");
        try
        {
            var fetched = url.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? await FetchAsync(client, url, archive, cancellationToken)
                : await CopyHashedAsync(url, archive, cancellationToken);
            if (!string.Equals(fetched.Sha256, pin.ZipSha256, StringComparison.OrdinalIgnoreCase))
            {
                log(SetupText.Get("setup.libmpv.checksum-fallback", url));
                return null;
            }

            var extract = Path.Combine(workRoot, "libmpv-zip");
            Directory.CreateDirectory(extract);
            var dll = Path.Combine(extract, pin.FileName);
            using (var zip = System.IO.Compression.ZipFile.OpenRead(archive))
            {
                var entry = zip.Entries.FirstOrDefault(e => string.Equals(e.Name, pin.FileName, StringComparison.OrdinalIgnoreCase));
                if (entry is null)
                {
                    log(SetupText.Get("setup.libmpv.source-failed", url));
                    return null;
                }
                System.IO.Compression.ZipFileExtensions.ExtractToFile(entry, dll, true);
            }

            if (string.Equals(UpdateCheck.HashFile(dll), pin.DllSha256, StringComparison.OrdinalIgnoreCase)) return dll;
            log(SetupText.Get("setup.libmpv.checksum-fallback", url));
            return null;
        }
        catch (Exception exception) when (exception is SetupException or HttpRequestException or IOException or InvalidDataException)
        {
            log(SetupText.Get("setup.libmpv.source-failed", url));
            return null;
        }
    }

    public static async Task<IReadOnlyDictionary<string, string>> FetchFfmpegAsync(
        FfmpegPin pin, string workRoot, CancellationToken cancellationToken)
    {
        IRangeSource source = pin.Url.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? new HttpRangeSource(pin.Url, UserAgent)
            : new FileRangeSource(pin.Url);
        var zip = await RemoteZip.OpenAsync(source, cancellationToken);
        var directory = Path.Combine(workRoot, "ffmpeg");
        Directory.CreateDirectory(directory);

        var results = await Task.WhenAll(pin.Entries.Select(async entry =>
        {
            var target = Path.Combine(directory, entry.FileName);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var file = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20, useAsync: true))
            await using (var hashing = new HashingStream(file, hash))
            {
                await zip.ExtractToAsync(entry.EntryPath, hashing, cancellationToken);
            }
            var actual = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            if (!string.Equals(actual, entry.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new SetupException(SetupText.Get("setup.checksum.mismatch", entry.FileName, entry.Sha256, actual));
            return (entry.FileName, target);
        }));

        return results.ToDictionary(r => r.FileName, r => r.target, StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<FetchedFile> CopyHashedAsync(string path, string destination, CancellationToken cancellationToken)
    {
        await using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, useAsync: true);
        return await WriteHashedAsync(source, destination, cancellationToken);
    }

    private sealed class HashingStream : Stream
    {
        private readonly Stream _inner;
        private readonly IncrementalHash _hash;

        public HashingStream(Stream inner, IncrementalHash hash)
        {
            _inner = inner;
            _hash = hash;
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => throw new NotSupportedException(); }
        public override void Flush() => _inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            _hash.AppendData(buffer, offset, count);
            _inner.Write(buffer, offset, count);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            _hash.AppendData(buffer.Span);
            await _inner.WriteAsync(buffer, cancellationToken);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }
}
