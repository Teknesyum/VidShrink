using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// B1 kalaninin (flac, ses suzgeci, dis altyazi, kapak, forced, yakma) ortak canli duzenegi.
/// Girdiler ≤3 sn lavfi, 320x240; kanit <c>.calisma/b1-kalan/</c> altinda, her olcu kendi
/// dosyalarini son asertten sonra siler.
/// </summary>
internal static class B1KalanGirdi
{
    internal static string Folder
    {
        get
        {
            var path = Path.Combine(GirdiKanit.Root, ".calisma", "b1-kalan");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    internal static string Yol(string ad) => Path.Combine(Folder, ad);

    internal static void Kapat(params string[] adlar)
    {
        foreach (var ad in adlar)
            foreach (var yol in Directory.GetFiles(Folder, ad))
                File.Delete(yol);
        if (!Directory.EnumerateFileSystemEntries(Folder).Any()) Directory.Delete(Folder);
    }

    internal static MediaInfo Kaynak(double sure, params SourceStream[] streams) => new()
    {
        FilePath = Path.Combine("C:\\kaynak", "girdi.mkv"),
        FileSizeBytes = 400L * 1024 * 1024,
        DurationSeconds = sure,
        Width = 1920,
        Height = 1080,
        Fps = 24,
        VideoCodec = "h264",
        TotalBitrateBps = 5_000_000,
        AudioCodec = streams.FirstOrDefault(stream => stream.Kind == StreamKind.Audio)?.Codec,
        AudioBitrateBps = streams.FirstOrDefault(stream => stream.Kind == StreamKind.Audio)?.BitrateBps ?? 0,
        AudioChannels = streams.FirstOrDefault(stream => stream.Kind == StreamKind.Audio)?.Channels ?? 0,
        Streams = streams
    };

    internal static readonly SourceStream Video = new(0, StreamKind.Video, "h264");

    internal static string Sonraki(IReadOnlyList<string> args, string bayrak)
    {
        var index = args.ToList().IndexOf(bayrak);
        Assert.True(index >= 0 && index + 1 < args.Count, bayrak + " bayragi yok: " + string.Join(' ', args));
        return args[index + 1];
    }

    /// <summary>Gurultulu 3 sn video (kaynak hedefin altina dusmesin diye ~1,8 MB) ve istenen ses.</summary>
    internal static async Task<string> SesliAsync(string ad, int ornekleme, int kanal)
    {
        var yol = Yol(ad);
        await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, new[]
        {
            "-hide_banner", "-y",
            "-f", "lavfi", "-i", "testsrc=size=320x240:rate=25:duration=3,noise=alls=60:allf=t",
            "-f", "lavfi", "-i", $"sine=frequency=440:sample_rate={ornekleme}:duration=3",
            "-map", "0", "-map", "1", "-c:v", "libx264", "-preset", "ultrafast", "-b:v", "3M",
            "-c:a", "aac", "-ac", kanal.ToString(CultureInfo.InvariantCulture), yol
        });
        return yol;
    }

    /// <summary>Siyah 3 sn video, ses ve tek metin altyazi; <paramref name="zorunlu"/> altyaziya forced bayragi koyar.</summary>
    internal static async Task<string> AltyaziliAsync(string ad, bool zorunlu)
    {
        var srt = Yol(Path.GetFileNameWithoutExtension(ad) + "-kaynak.srt");
        File.WriteAllText(srt, "1\n00:00:00,000 --> 00:00:03,000\nMERHABA DUNYA MERHABA\n\n", new UTF8Encoding(false));
        var yol = Yol(ad);
        await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, new[]
        {
            "-hide_banner", "-y",
            "-f", "lavfi", "-i", "color=c=black:size=320x240:rate=25:duration=3",
            "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000:duration=3",
            "-i", srt,
            "-map", "0", "-map", "1", "-map", "2", "-c:v", "libx264", "-preset", "ultrafast", "-b:v", "3M",
            "-c:a", "aac", "-c:s", "srt", "-metadata:s:s:0", "language=eng",
            "-disposition:s:0", zorunlu ? "forced" : "0", yol
        });
        return yol;
    }

    /// <summary>
    /// Kaynagi urunun kendi plan ve arguman zincirinden gecirir: yoklama <see cref="FfprobeClient"/>,
    /// plan <see cref="PlanCalculator.Build"/>, arguman <see cref="FfmpegArguments.Build"/>. Kodlama
    /// hizli olsun diye CRF + ultrafast kilitli; hedef kaynagin altinda kalir.
    /// </summary>
    internal static async Task<(MediaInfo Info, EncodePlan Plan, IReadOnlyList<string> Args)> KodlaAsync(
        string kaynak, string cikti, Action<PlanOptions> ayar)
    {
        var info = await FfprobeClient.ProbeAsync(kaynak);
        var options = new PlanOptions
        {
            TargetMb = 1.5,
            LockedCodec = "libx264",
            LockedMode = EncodeMode.Crf,
            LockedCrf = 35,
            LockedPreset = "ultrafast"
        };
        ayar(options);
        var plan = PlanCalculator.Build(info, options);
        Assert.NotEqual(EncodeMode.PassThrough, plan.ModeEnum);
        var args = FfmpegArguments.Build(info, plan, cikti, 0, null);
        await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, args);
        return (info, plan, args);
    }

    internal static async Task<List<JsonElement>> AkislarAsync(string yol)
    {
        var sonuc = await AkisGirdisi.RunAsync(ToolLocator.Ffprobe, new[] { "-v", "error", "-show_streams", "-of", "json", yol });
        Assert.Equal(0, sonuc.Code);
        using var doc = JsonDocument.Parse(sonuc.Out);
        return doc.RootElement.GetProperty("streams").EnumerateArray().Select(stream => stream.Clone()).ToList();
    }

    internal static string Alan(JsonElement stream, string ad)
        => stream.TryGetProperty(ad, out var deger) ? deger.ToString() : "";

    internal static string Etiket(JsonElement stream, string ad)
        => stream.TryGetProperty("tags", out var tags) && tags.TryGetProperty(ad, out var deger) ? deger.GetString() ?? "" : "";

    internal static int Bayrak(JsonElement stream, string ad)
        => stream.GetProperty("disposition").GetProperty(ad).GetInt32();

    /// <summary>Tumlesik yukseklik (LUFS), ffmpeg'in <c>ebur128</c> ozetinden.</summary>
    internal static async Task<double> YukseklikAsync(string yol)
    {
        var sonuc = await AkisGirdisi.RunAsync(ToolLocator.Ffmpeg, new[] { "-hide_banner", "-nostats", "-i", yol, "-map", "0:a:0", "-af", "ebur128", "-f", "null", "-" });
        Assert.Equal(0, sonuc.Code);
        var eslesme = Regex.Matches(sonuc.Err, @"I:\s+(-?\d+(?:\.\d+)?) LUFS").Last();
        return double.Parse(eslesme.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    /// <summary>Ortalama ses duzeyi (dB), <c>volumedetect</c>'ten.</summary>
    internal static async Task<double> OrtalamaDuzeyAsync(string yol)
    {
        var sonuc = await AkisGirdisi.RunAsync(ToolLocator.Ffmpeg, new[] { "-hide_banner", "-nostats", "-i", yol, "-map", "0:a:0", "-af", "volumedetect", "-f", "null", "-" });
        Assert.Equal(0, sonuc.Code);
        var eslesme = Regex.Match(sonuc.Err, @"mean_volume: (-?\d+(?:\.\d+)?) dB");
        Assert.True(eslesme.Success, sonuc.Err);
        return double.Parse(eslesme.Groups[1].Value, CultureInfo.InvariantCulture);
    }
}
