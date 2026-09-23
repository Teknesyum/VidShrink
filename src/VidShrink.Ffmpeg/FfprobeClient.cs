using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using VidShrink.Core;

namespace VidShrink.Ffmpeg;

public static class FfprobeClient
{
    public static async Task<MediaInfo> ProbeAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Input file not found.", filePath);

        var args = new[]
        {
            "-hide_banner", "-v", "error",
            "-print_format", "json",
            "-show_format", "-show_streams", "-show_chapters", "-show_programs",
            filePath
        };

        using var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffprobe, args) };
        process.Start();
        var stdout = await process.StandardOutput.ReadToEndAsync(ct);
        var stderr = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"ffprobe failed ({process.ExitCode}): {stderr.Trim()}");

        using var doc = JsonDocument.Parse(stdout);
        var root = doc.RootElement;
        var format = root.GetProperty("format");
        var streams = root.GetProperty("streams");

        JsonElement? video = null, audio = null;
        foreach (var s in streams.EnumerateArray())
        {
            var type = s.TryGetProperty("codec_type", out var t) ? t.GetString() : null;
            if (type == "video" && video is null && !IsAttachedPicture(s)) video = s;
            else if (type == "audio" && (audio is null || (!Disposition(audio.Value, "default") && Disposition(s, "default")))) audio = s;
        }

        if (video is null)
            throw new InvalidOperationException("The file contains no video stream.");

        var v = video.Value;
        var fileSize = new FileInfo(filePath).Length;
        var duration = ParseDouble(format, "duration") ?? ParseDouble(v, "duration") ?? 0;
        if (duration <= 0)
            throw new InvalidOperationException("Duration could not be determined; the file may be corrupt.");

        var inventory = Inventory(streams, duration);
        if (inventory.Any(stream => stream.Kind == StreamKind.Subtitle && stream.Bytes <= 0))
            inventory = await MeasureSubtitleBytesAsync(filePath, inventory, ct);
        if (inventory.FirstOrDefault(stream => stream.IsAttachedPicture) is { } cover)
            inventory = await MeasureCoverBytesAsync(filePath, inventory, cover.Index, ct);
        var chapters = root.TryGetProperty("chapters", out var chapterList) && chapterList.ValueKind == JsonValueKind.Array
            ? ChapterMarks(chapterList)
            : (IReadOnlyList<ChapterMark>)Array.Empty<ChapterMark>();

        var pixFmt = GetString(v, "pix_fmt");
        var colorTransfer = GetString(v, "color_transfer");
        var isHdr = colorTransfer is "smpte2084" or "arib-std-b67"
            || (GetString(v, "color_primaries") is "bt2020" && (pixFmt?.Contains("10le") ?? false));
        var dolbyVision = ParseDolbyVision(v);
        var hdr10Plus = isHdr && await HasHdr10PlusAsync(filePath, GetInt(v, "index") ?? 0, ct);
        var fieldOrder = GetString(v, "field_order");

        var titles = Basliklar(root, duration, DisplayDimensions(v), inventory.Count, chapters.Count);

        return new MediaInfo
        {
            FilePath = filePath,
            FileSizeBytes = fileSize,
            DurationSeconds = duration,
            Width = DisplayDimensions(v).width,
            Height = DisplayDimensions(v).height,
            ParNum = PikselOrani(v).num,
            ParDen = PikselOrani(v).den,
            Fps = ParseFraction(GetString(v, "avg_frame_rate")) ?? ParseFraction(GetString(v, "r_frame_rate")) ?? 30,
            VideoCodec = GetString(v, "codec_name") ?? "unknown",
            TotalBitrateBps = ParseLong(format, "bit_rate") ?? (long)(fileSize * 8 / duration),
            PixelFormat = pixFmt,
            IsHdr = isHdr,
            DolbyVisionProfile = dolbyVision.Profile,
            DolbyVisionCompatibilityId = dolbyVision.CompatibilityId,
            HasHdr10Plus = hdr10Plus,
            ColorPrimaries = GetString(v, "color_primaries"),
            ColorTransfer = colorTransfer,
            ColorSpace = GetString(v, "color_space"),
            ColorRange = GetString(v, "color_range"),
            BitDepth = GetInt(v, "bits_per_raw_sample") ?? BitDepthFromPixFmt(pixFmt),
            MasteringDisplayMetadata = ParseMasteringDisplay(v),
            ContentLightLevel = ParseContentLightLevel(v),
            IsInterlaced = fieldOrder is not null and not "progressive" and not "unknown",
            FieldOrder = fieldOrder,
            AudioCodec = audio is null ? null : GetString(audio.Value, "codec_name"),
            AudioBitrateBps = audio is null ? 0 : ParseLong(audio.Value, "bit_rate") ?? 128_000,
            AudioChannels = audio is null ? 0 : GetInt(audio.Value, "channels") ?? 2,
            Streams = inventory,
            Chapters = chapters,
            Titles = titles
        };
    }

    /// <summary>
    /// ffprobe'un bolum dizisi. Baslik <c>tags.title</c>'dan gelir, yoksa <c>null</c>;
    /// numara dosyadaki sirayla 1'den baslar, ffprobe'un kendi <c>id</c>'sinden degil —
    /// id her kapta 0'dan baslamiyor.
    /// </summary>
    private static List<ChapterMark> ChapterMarks(JsonElement chapters)
    {
        var list = new List<ChapterMark>();
        foreach (var c in chapters.EnumerateArray())
        {
            var start = ParseDouble(c, "start_time");
            var end = ParseDouble(c, "end_time");
            if (start is null || end is null) continue;
            string? title = null;
            if (c.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Object)
                title = GetString(tags, "title");
            list.Add(new ChapterMark(list.Count + 1, start.Value, end.Value, title));
        }
        return list;
    }

    internal static List<SourceStream> Inventory(JsonElement streams, double duration)
    {
        var list = new List<SourceStream>();
        foreach (var s in streams.EnumerateArray())
        {
            var kind = GetString(s, "codec_type") switch
            {
                "video" => StreamKind.Video,
                "audio" => StreamKind.Audio,
                "subtitle" => StreamKind.Subtitle,
                "attachment" => StreamKind.Attachment,
                _ => StreamKind.Data
            };
            var index = GetInt(s, "index") ?? list.Count;
            var tags = s.TryGetProperty("tags", out var t) && t.ValueKind == JsonValueKind.Object ? t : (JsonElement?)null;
            var bitrate = ParseLong(s, "bit_rate") ?? TagLong(tags, "BPS") ?? 0;
            var bytes = TagLong(tags, "NUMBER_OF_BYTES") ?? 0;
            if (kind == StreamKind.Attachment) bytes = GetInt(s, "extradata_size") ?? bytes;
            if (bytes <= 0 && bitrate > 0 && duration > 0) bytes = (long)(bitrate * duration / 8);
            if (bitrate <= 0 && bytes > 0 && duration > 0) bitrate = (long)(bytes * 8 / duration);

            list.Add(new SourceStream(
                index,
                kind,
                GetString(s, "codec_name") ?? "unknown",
                tags is { } tagValues ? GetString(tagValues, "language") : null,
                Disposition(s, "default"),
                Disposition(s, "forced"),
                GetInt(s, "channels") ?? 0,
                bitrate,
                bytes,
                kind == StreamKind.Video && IsAttachedPicture(s),
                tags is { } titleTags ? GetString(titleTags, "title") : null,
                (int)(ParseLong(s, "sample_rate") ?? 0)));
        }
        return list;
    }

    /// <summary>
    /// Kapak resmi tek paketlik bir video izi; ffprobe'un bit hizindan sure ile carpilan bayt
    /// ona uymaz. Yalniz o izin ilk paketi okunur ve boyutu izin baytina yazilir.
    /// </summary>
    private static async Task<List<SourceStream>> MeasureCoverBytesAsync(string filePath, List<SourceStream> inventory, int coverIndex, CancellationToken ct)
    {
        var totals = await PacketTotalsAsync(filePath, coverIndex.ToString(CultureInfo.InvariantCulture), "%+#1", ct);
        return totals is not null && totals.TryGetValue(coverIndex, out var size)
            ? inventory.Select(stream => stream.Index == coverIndex ? stream with { Bytes = size } : stream).ToList()
            : inventory;
    }

    private static async Task<List<SourceStream>> MeasureSubtitleBytesAsync(string filePath, List<SourceStream> inventory, CancellationToken ct)
    {
        var totals = await PacketTotalsAsync(filePath, "s", null, ct);
        if (totals is null) return inventory;
        return inventory
            .Select(stream => stream.Kind == StreamKind.Subtitle && stream.Bytes <= 0 && totals.TryGetValue(stream.Index, out var total)
                ? stream with { Bytes = total }
                : stream)
            .ToList();
    }

    private static async Task<Dictionary<int, long>?> PacketTotalsAsync(string filePath, string select, string? readIntervals, CancellationToken ct)
    {
        var args = new List<string> { "-hide_banner", "-v", "error", "-select_streams", select };
        if (readIntervals is not null) args.AddRange(new[] { "-read_intervals", readIntervals });
        args.AddRange(new[] { "-show_entries", "packet=stream_index,size", "-of", "csv=p=0", filePath });

        try
        {
            using var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffprobe, args) };
            process.Start();
            var stderrTask = process.StandardError.ReadToEndAsync(ct);
            var stdout = await process.StandardOutput.ReadToEndAsync(ct);
            await stderrTask;
            await process.WaitForExitAsync(ct);
            if (process.ExitCode != 0) return null;

            var totals = new Dictionary<int, long>();
            foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parts = line.Split(',');
                if (parts.Length < 2) continue;
                if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)) continue;
                if (!long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var size)) continue;
                totals[index] = totals.GetValueOrDefault(index) + size;
            }
            return totals;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    private static bool Disposition(JsonElement stream, string name)
        => stream.TryGetProperty("disposition", out var d)
           && d.TryGetProperty(name, out var value)
           && value.ValueKind == JsonValueKind.Number
           && value.GetInt32() == 1;

    private static long? TagLong(JsonElement? tags, string name)
    {
        if (tags is not { } values) return null;
        foreach (var property in values.EnumerateObject())
        {
            if (!property.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && !property.Name.StartsWith(name + "-", StringComparison.OrdinalIgnoreCase)) continue;
            if (property.Value.ValueKind == JsonValueKind.String
                && long.TryParse(property.Value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                return parsed;
        }
        return null;
    }

    private static int BitDepthFromPixFmt(string? pixFmt)
    {
        if (pixFmt is null) return 8;
        if (pixFmt.Contains("12le") || pixFmt.Contains("12be")) return 12;
        if (pixFmt.Contains("10le") || pixFmt.Contains("10be")) return 10;
        return 8;
    }

    internal static string? ParseMasteringDisplay(JsonElement stream)
    {
        if (!stream.TryGetProperty("side_data_list", out var list)) return null;
        foreach (var item in list.EnumerateArray())
        {
            if (GetString(item, "side_data_type") != "Mastering display metadata") continue;
            var rx = ParseRatio(GetString(item, "red_x"));
            var ry = ParseRatio(GetString(item, "red_y"));
            var gx = ParseRatio(GetString(item, "green_x"));
            var gy = ParseRatio(GetString(item, "green_y"));
            var bx = ParseRatio(GetString(item, "blue_x"));
            var by = ParseRatio(GetString(item, "blue_y"));
            var wx = ParseRatio(GetString(item, "white_point_x"));
            var wy = ParseRatio(GetString(item, "white_point_y"));
            var maxLum = ParseRatio(GetString(item, "max_luminance"));
            var minLum = ParseRatio(GetString(item, "min_luminance"));
            if (rx is null || ry is null || gx is null || gy is null || bx is null || by is null || wx is null || wy is null || maxLum is null || minLum is null)
                return null;

            int Chroma(double value) => (int)Math.Round(value * 50000);
            int Luma(double value) => (int)Math.Round(value * 10000);
            return $"G({Chroma(gx.Value)},{Chroma(gy.Value)})B({Chroma(bx.Value)},{Chroma(by.Value)})R({Chroma(rx.Value)},{Chroma(ry.Value)})WP({Chroma(wx.Value)},{Chroma(wy.Value)})L({Luma(maxLum.Value)},{Luma(minLum.Value)})";
        }
        return null;
    }

    internal static (int? Profile, int? CompatibilityId) ParseDolbyVision(JsonElement stream)
    {
        if (!stream.TryGetProperty("side_data_list", out var list) || list.ValueKind != JsonValueKind.Array) return (null, null);
        foreach (var item in list.EnumerateArray())
        {
            if (GetString(item, "side_data_type") != "DOVI configuration record") continue;
            return (GetInt(item, "dv_profile"), GetInt(item, "dv_bl_signal_compatibility_id"));
        }
        return (null, null);
    }

    internal static bool FrameCarriesHdr10Plus(string frameJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(frameJson);
            if (!doc.RootElement.TryGetProperty("frames", out var frames) || frames.ValueKind != JsonValueKind.Array) return false;
            foreach (var frame in frames.EnumerateArray())
            {
                if (!frame.TryGetProperty("side_data_list", out var list) || list.ValueKind != JsonValueKind.Array) continue;
                foreach (var item in list.EnumerateArray())
                    if (GetString(item, "side_data_type") is { } type && type.Contains("SMPTE2094-40", StringComparison.Ordinal))
                        return true;
            }
        }
        catch (JsonException)
        {
            return false;
        }
        return false;
    }

    /// <summary>
    /// Kaynagin butun karelerini cozup HDR10+ yan verisini <see cref="Hdr10PlusJson.Collector"/>'a
    /// satir satir akitir; dokum bellekte tutulmaz. <paramref name="framesRead"/> okunan kare
    /// sayisini bildirir. ffprobe acilmaz ya da sifirdan farkli donerse <c>null</c>. Iptalde
    /// surec oldurulur: uzun bir kaynagin cozumu iptalden sonra arkada surmemeli.
    /// </summary>
    public static async Task<Hdr10PlusExtraction?> ReadHdr10PlusAsync(string filePath, Action<int>? framesRead, CancellationToken ct)
    {
        try
        {
            using var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffprobe, Hdr10PlusJson.FfprobeArguments(filePath)) };
            process.Start();
            using var kill = ct.Register(() => { try { process.Kill(entireProcessTree: true); } catch { } });
            var stderrTask = process.StandardError.ReadToEndAsync(ct);
            var collector = new Hdr10PlusJson.Collector();
            var reported = 0;
            while (await process.StandardOutput.ReadLineAsync(ct) is { } line)
            {
                collector.AddLine(line);
                if (framesRead is not null && collector.Frames != reported)
                {
                    reported = collector.Frames;
                    framesRead(reported);
                }
            }
            await stderrTask;
            await process.WaitForExitAsync(ct);
            return process.ExitCode == 0 ? collector.Result() : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    /// Dosyada HDR10+ tasiyan kare sayisi; cikisin kaynakla kare kare karsilastirmasi icin.
    /// Okunamazsa 0: sayim uyarinin tetigi, okunamayan cikis "tasindi" sayilmamali.
    /// </summary>
    public static async Task<int> CountHdr10PlusAsync(string filePath, CancellationToken ct)
        => (await ReadHdr10PlusAsync(filePath, null, ct))?.Hdr10PlusFrames ?? 0;

    private static async Task<bool> HasHdr10PlusAsync(string filePath, int streamIndex, CancellationToken ct)
    {
        var args = new[]
        {
            "-hide_banner", "-v", "error",
            "-select_streams", streamIndex.ToString(CultureInfo.InvariantCulture),
            "-read_intervals", "%+#1",
            "-show_frames", "-show_entries", "frame_side_data=side_data_type",
            "-of", "json", filePath
        };
        try
        {
            using var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffprobe, args) };
            process.Start();
            var stderrTask = process.StandardError.ReadToEndAsync(ct);
            var stdout = await process.StandardOutput.ReadToEndAsync(ct);
            await stderrTask;
            await process.WaitForExitAsync(ct);
            return process.ExitCode == 0 && FrameCarriesHdr10Plus(stdout);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return false;
        }
    }

    internal static string? ParseContentLightLevel(JsonElement stream)
    {
        if (!stream.TryGetProperty("side_data_list", out var list)) return null;
        foreach (var item in list.EnumerateArray())
        {
            if (GetString(item, "side_data_type") != "Content light level metadata") continue;
            var max = GetInt(item, "max_content");
            var avg = GetInt(item, "max_average");
            if (max is null || avg is null) return null;
            return $"{max},{avg}";
        }
        return null;
    }

    private static (int width, int height) DisplayDimensions(JsonElement stream)
    {
        var width = GetInt(stream, "width") ?? 0;
        var height = GetInt(stream, "height") ?? 0;
        return CeyrekDonus(stream) ? (height, width) : (width, height);
    }

    private static bool CeyrekDonus(JsonElement stream)
    {
        var rotation = 0;
        if (stream.TryGetProperty("tags", out var tags))
            rotation = GetInt(tags, "rotate") ?? 0;
        if (stream.TryGetProperty("side_data_list", out var sideData))
            foreach (var item in sideData.EnumerateArray())
                rotation = GetInt(item, "rotation") ?? rotation;
        return Math.Abs(rotation) % 180 == 90;
    }

    /// <summary>
    /// Piksel en-boy orani. <c>sample_aspect_ratio</c> yoksa, <c>"0:1"</c>, <c>"N/A"</c>
    /// ya da paydasi sifirsa kare piksel sayilir ve 1:1 doner. Ceyrek donuste oran da ters
    /// cevrilir, cunku dondurulmus karede genis piksel uzun piksele donusur.
    /// </summary>
    private static (int num, int den) PikselOrani(JsonElement stream)
    {
        var metin = GetString(stream, "sample_aspect_ratio");
        if (metin is null) return (1, 1);
        var parca = metin.Split(':');
        if (parca.Length != 2
            || !int.TryParse(parca[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var num)
            || !int.TryParse(parca[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var den)
            || num <= 0 || den <= 0)
            return (1, 1);
        return CeyrekDonus(stream) ? (den, num) : (num, den);
    }

    private static bool IsAttachedPicture(JsonElement stream)
        => stream.TryGetProperty("disposition", out var d)
           && d.TryGetProperty("attached_pic", out var a)
           && a.GetInt32() == 1;

    /// <summary>
    /// Kaynaktaki basliklar. Cok programli bir yayinda her program bir basliktir; program
    /// yoksa ya da tek program varsa kaynak duz dosya sayilir ve tek baslik doner. Numara
    /// ffprobe'un <c>program_id</c>'sidir, sira degil; kullanici bu sayiyi yaziyor.
    /// </summary>
    private static IReadOnlyList<SourceTitle> Basliklar(
        JsonElement root, double duration, (int width, int height) olcu, int akisSayisi, int bolumSayisi)
    {
        var liste = new List<SourceTitle>();
        if (root.TryGetProperty("programs", out var programs) && programs.ValueKind == JsonValueKind.Array)
        {
            foreach (var p in programs.EnumerateArray())
            {
                var numara = GetInt(p, "program_id");
                if (numara is null) continue;
                int genislik = 0, yukseklik = 0;
                double sure = 0;
                var indeksler = new List<int>();
                if (p.TryGetProperty("streams", out var ps) && ps.ValueKind == JsonValueKind.Array)
                    foreach (var st in ps.EnumerateArray())
                    {
                        if (GetInt(st, "index") is { } ix) indeksler.Add(ix);
                        sure = Math.Max(sure, ParseDouble(st, "duration") ?? 0);
                        if (GetString(st, "codec_type") != "video" || IsAttachedPicture(st) || genislik > 0) continue;
                        var boy = DisplayDimensions(st);
                        genislik = boy.width;
                        yukseklik = boy.height;
                    }

                string? etiket = null;
                if (p.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Object)
                    etiket = GetString(tags, "service_name");

                liste.Add(new SourceTitle(
                    numara.Value,
                    sure > 0 ? sure : duration,
                    genislik,
                    yukseklik,
                    indeksler.Count,
                    0,
                    etiket)
                { StreamIndexes = indeksler });
            }
        }

        if (liste.Count > 1) return liste;

        return new[]
        {
            new SourceTitle(1, duration, olcu.width, olcu.height, akisSayisi, bolumSayisi, null),
        };
    }

    private static string? GetString(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? GetInt(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number) return v.GetInt32();
        return int.TryParse(v.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null;
    }

    private static long? ParseLong(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number) return v.GetInt64();
        return long.TryParse(v.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) ? l : null;
    }

    private static double? ParseDouble(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number) return v.GetDouble();
        return double.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    /// <summary>
    /// Kare hizi icin: 0,1-1000 disindaki oran (ornegin <c>0/0</c> yerine yazilan 90000/1)
    /// yok sayilir. Renk koordinati ve parlaklik icin <see cref="ParseRatio"/>; bu suzgec
    /// <c>blue_y</c> (0,06) ve <c>min_luminance</c> (0,005) degerlerini dusurup mastering
    /// display satirini her kaynakta bos birakiyordu.
    /// </summary>
    private static double? ParseFraction(string? value)
        => ParseRatio(value) is { } fps && fps > 0.1 && fps < 1000 ? fps : null;

    private static double? ParseRatio(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var parts = value.Split('/');
        if (parts.Length != 2) return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var single) ? single : null;
        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var num)) return null;
        if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var den) || den == 0) return null;
        return num / den;
    }
}
