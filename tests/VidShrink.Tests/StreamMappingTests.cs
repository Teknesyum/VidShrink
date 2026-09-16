using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

internal static class AkisGirdisi
{
    private static string? _cokIzli;
    private static string? _donuk;

    internal static string Folder
    {
        get
        {
            var path = Path.Combine(GirdiKanit.Root, ".calisma", "hb-1c-test");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    internal static void Write(string name, string body)
        => File.WriteAllText(Path.Combine(Folder, name), body, new UTF8Encoding(false));

    internal static async Task<string> CokIzliAsync()
    {
        if (_cokIzli is not null && File.Exists(_cokIzli)) return _cokIzli;
        var dir = Folder;
        var srt = Path.Combine(dir, "girdi.srt");
        var sup = Path.Combine(dir, "girdi.sup");
        var meta = Path.Combine(dir, "girdi-meta.txt");
        var output = Path.Combine(dir, "cok-izli.mkv");
        File.WriteAllText(srt, "1\n00:00:00,500 --> 00:00:02,000\nMerhaba\n\n", new UTF8Encoding(false));
        File.WriteAllBytes(sup, Pgs());
        File.WriteAllText(meta,
            ";FFMETADATA1\ntitle=Deneme Basligi\ncreation_time=2024-05-01T10:00:00.000000Z\n\n"
            + "[CHAPTER]\nTIMEBASE=1/1000\nSTART=0\nEND=1500\ntitle=Bir\n\n"
            + "[CHAPTER]\nTIMEBASE=1/1000\nSTART=1500\nEND=3000\ntitle=Iki\n",
            new UTF8Encoding(false));

        await RunOrThrowAsync(ToolLocator.Ffmpeg, new[]
        {
            "-hide_banner", "-y",
            "-f", "lavfi", "-i", "testsrc2=s=480x270:r=25:d=3,noise=alls=30:allf=t",
            "-f", "lavfi", "-i", "sine=f=440:d=3:r=48000",
            "-f", "lavfi", "-i", "sine=f=660:d=3:r=48000",
            "-i", srt, "-i", sup, "-i", meta,
            "-map", "0:v", "-map", "1:a", "-map", "2:a", "-map", "3:s", "-map", "4:s",
            "-map_metadata", "5", "-map_chapters", "5",
            "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p",
            "-c:a:0", "aac", "-b:a:0", "96k", "-ac:a:0", "2",
            "-c:a:1", "ac3", "-b:a:1", "192k", "-ac:a:1", "6",
            "-c:s:0", "srt", "-c:s:1", "copy",
            "-metadata:s:a:0", "language=eng", "-metadata:s:a:1", "language=tur",
            "-metadata:s:s:0", "language=tur", "-metadata:s:s:1", "language=eng",
            "-disposition:a:0", "0", "-disposition:a:1", "default",
            output
        });
        _cokIzli = output;
        return output;
    }

    internal static async Task<string> DonukAsync()
    {
        if (_donuk is not null && File.Exists(_donuk)) return _donuk;
        var dir = Folder;
        var plain = Path.Combine(dir, "yatay.mp4");
        var output = Path.Combine(dir, "donuk.mp4");
        await RunOrThrowAsync(ToolLocator.Ffmpeg, new[]
        {
            "-hide_banner", "-y", "-f", "lavfi", "-i", "testsrc2=s=320x240:r=25:d=2",
            "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", plain
        });
        await RunOrThrowAsync(ToolLocator.Ffmpeg, new[]
        {
            "-hide_banner", "-y", "-display_rotation", "90", "-i", plain, "-c", "copy", output
        });
        _donuk = output;
        return output;
    }

    internal static byte[] Pgs()
    {
        var ms = new MemoryStream();
        void Segment(uint pts, byte type, params byte[][] parts)
        {
            var data = parts.SelectMany(part => part).ToArray();
            ms.Write(new byte[] { 0x50, 0x47 });
            ms.Write(U32(pts));
            ms.Write(U32(0));
            ms.WriteByte(type);
            ms.Write(U16(data.Length));
            ms.Write(data);
        }

        var rle = new byte[] { 1, 1, 1, 1, 0, 0, 1, 1, 1, 1, 0, 0 };
        Segment(45000, 0x16, U16(320), U16(240), new byte[] { 0x10 }, U16(0), new byte[] { 0x80, 0, 0, 1 }, U16(0), new byte[] { 0, 0 }, U16(10), U16(200));
        Segment(45000, 0x17, new byte[] { 1, 0 }, U16(10), U16(200), U16(4), U16(2));
        Segment(45000, 0x14, new byte[] { 0, 0, 1, 235, 128, 128, 255 });
        Segment(45000, 0x15, U16(0), new byte[] { 0, 0xC0 }, new byte[] { 0, 0, (byte)(rle.Length + 4) }, U16(4), U16(2), rle);
        Segment(45000, 0x80);
        Segment(135000, 0x16, U16(320), U16(240), new byte[] { 0x10 }, U16(1), new byte[] { 0, 0, 0, 0 });
        Segment(135000, 0x17, new byte[] { 1, 0 }, U16(10), U16(200), U16(4), U16(2));
        Segment(135000, 0x80);
        return ms.ToArray();
    }

    private static byte[] U16(int value) => new[] { (byte)(value >> 8), (byte)value };

    private static byte[] U32(uint value) => new[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value };

    internal static async Task<(int Code, string Out, string Err)> RunAsync(string exe, IEnumerable<string> args)
    {
        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        using var process = new Process { StartInfo = psi };
        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await stdout, await stderr);
    }

    internal static async Task RunOrThrowAsync(string exe, IEnumerable<string> args)
    {
        var result = await RunAsync(exe, args);
        if (result.Code != 0) throw new InvalidOperationException(result.Err);
    }

    internal static async Task<Cikti> ProbeAsync(string path)
    {
        var result = await RunAsync(ToolLocator.Ffprobe, new[]
        {
            "-v", "error", "-print_format", "json", "-show_format", "-show_streams", "-show_chapters", path
        });
        Assert.Equal(0, result.Code);
        return Cikti.Parse(result.Out);
    }

    internal static async Task<(EncodePlan Plan, EncodeResult Result, Cikti Probe)> KodlaAsync(string input, PlanOptions options, string name, RetryDecisionAsync? retry = null)
    {
        var info = await FfprobeClient.ProbeAsync(input);
        var plan = PlanCalculator.Build(info, options);
        var output = Path.Combine(Folder, name + "." + (plan.Streams?.Extension ?? "mp4"));
        if (File.Exists(output)) File.Delete(output);
        Write(name + "-komut.txt", FfmpegArguments.ToCommandLine(FfmpegArguments.Build(info, plan, output, plan.ModeEnum == EncodeMode.TwoPass ? 2 : 0, null)));
        var result = await new EncodeRunner().RunAsync(info, plan, output, options.TargetMb, null, CancellationToken.None, options.FillPolicy, askBeforeRetry: retry);
        Assert.True(result.Success, result.Error);
        return (plan, result, await ProbeAsync(output));
    }

    internal static PlanOptions Secenek(double targetMb) => new()
    {
        TargetMb = targetMb,
        LockedCodec = "libx264",
        AllowResolutionDrop = false,
        AllowFpsDrop = false
    };
}

internal sealed record Iz(string Type, string Codec, string? Language, int Channels, int Width, int Height, int Rotation);

internal sealed record Cikti(IReadOnlyList<Iz> Streams, int Chapters, string? Title, string? CreationTime, string FormatName)
{
    internal IEnumerable<Iz> Of(string type) => Streams.Where(stream => stream.Type == type);

    internal static Cikti Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var streams = new List<Iz>();
        foreach (var stream in root.GetProperty("streams").EnumerateArray())
        {
            string? language = null;
            if (stream.TryGetProperty("tags", out var tags))
                foreach (var tag in tags.EnumerateObject())
                    if (tag.Name.Equals("language", StringComparison.OrdinalIgnoreCase)) language = tag.Value.GetString();
            var rotation = 0;
            if (stream.TryGetProperty("side_data_list", out var side))
                foreach (var item in side.EnumerateArray())
                    if (item.TryGetProperty("rotation", out var r) && r.ValueKind == JsonValueKind.Number) rotation = r.GetInt32();
            streams.Add(new Iz(
                stream.GetProperty("codec_type").GetString() ?? "",
                stream.TryGetProperty("codec_name", out var codec) ? codec.GetString() ?? "" : "",
                language,
                stream.TryGetProperty("channels", out var channels) ? channels.GetInt32() : 0,
                stream.TryGetProperty("width", out var width) ? width.GetInt32() : 0,
                stream.TryGetProperty("height", out var height) ? height.GetInt32() : 0,
                rotation));
        }

        string? title = null;
        string? created = null;
        var format = root.GetProperty("format");
        if (format.TryGetProperty("tags", out var formatTags))
            foreach (var tag in formatTags.EnumerateObject())
            {
                if (tag.Name.Equals("title", StringComparison.OrdinalIgnoreCase)) title = tag.Value.GetString();
                if (tag.Name.Equals("creation_time", StringComparison.OrdinalIgnoreCase)) created = tag.Value.GetString();
            }

        return new Cikti(streams, root.GetProperty("chapters").GetArrayLength(), title, created, format.GetProperty("format_name").GetString() ?? "");
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"  kap {FormatName}, bolum {Chapters}, baslik '{Title}', tarih '{CreationTime}'");
        foreach (var stream in Streams)
            sb.AppendLine($"  {stream.Type,-8} {stream.Codec,-18} dil {stream.Language ?? "-",-4} kanal {stream.Channels} {stream.Width}x{stream.Height} donus {stream.Rotation}");
        return sb.ToString();
    }
}

public sealed class StreamMappingTests
{
    private static string I(FormattableString text) => FormattableString.Invariant(text);

    private static MediaInfo Kaynak(params SourceStream[] streams) => new()
    {
        FilePath = "girdi.mkv",
        FileSizeBytes = 400L * 1024 * 1024,
        DurationSeconds = 600,
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

    private static readonly SourceStream Video = new(0, StreamKind.Video, "h264");

    [Fact]
    public void VarsayilanTekSesSecerDilTercihiVarsayilanIzeUstunGelir()
    {
        var info = Kaynak(
            Video,
            new SourceStream(1, StreamKind.Audio, "ac3", "eng", IsDefault: true, Channels: 6, BitrateBps: 384_000),
            new SourceStream(2, StreamKind.Audio, "aac", "tur", Channels: 2, BitrateBps: 128_000));

        var turkce = StreamMapping.Decide(info, new StreamRequest(PreferredLanguage: "tr"), OutputContainer.Mp4, 96, null, "aac", false, 100);
        var bilinmeyen = StreamMapping.Decide(info, new StreamRequest(PreferredLanguage: "ja"), OutputContainer.Mp4, 96, null, "aac", false, 100);

        Assert.Equal("0:2", Assert.Single(turkce.Audio).Map);
        Assert.Equal("0:1", Assert.Single(bilinmeyen.Audio).Map);
        Assert.Contains(StreamNote.ExtraAudioDropped, turkce.Notes);
        Assert.Equal(2, Assert.Single(bilinmeyen.Audio).Channels);
        Assert.Contains(StreamNote.AudioDownmixedToStereo, bilinmeyen.Notes);
        Assert.Null(Assert.Single(turkce.Audio).Channels);
    }

    [Fact]
    public void Mp4MetinAltyaziyiMovTextYaparGoruntuAltyaziyiDuserMkvIkisiniTasir()
    {
        var info = Kaynak(
            Video,
            new SourceStream(1, StreamKind.Audio, "aac", "eng", Channels: 2, BitrateBps: 128_000),
            new SourceStream(2, StreamKind.Subtitle, "subrip", "tur", Bytes: 20_000),
            new SourceStream(3, StreamKind.Subtitle, "hdmv_pgs_subtitle", "eng", Bytes: 3_000_000));

        var mp4 = StreamMapping.Decide(info, StreamRequest.Default, OutputContainer.Mp4, 96, null, "aac", false, 100);
        var mkv = StreamMapping.Decide(info, new StreamRequest(KeepAllTracks: true), OutputContainer.Mkv, 96, null, "aac", false, 100);

        var metin = Assert.Single(mp4.Subtitles);
        Assert.Equal(("0:2", "mov_text"), (metin.Map, metin.Codec));
        Assert.Contains(StreamNote.ImageSubtitleDropped, mp4.Notes);
        Assert.Equal(new[] { "copy", "copy" }, mkv.Subtitles.Select(track => track.Codec));
        Assert.Equal("libopus", Assert.Single(mkv.Audio).Codec);

        var mp4Args = mp4.OutputArguments();
        Assert.Contains("mov_text", mp4Args);
        Assert.DoesNotContain("0:3", mp4Args);
        Assert.Contains("0:3", mkv.OutputArguments());

        var kbit = 3_020_000 * 8.0 / 1000.0 / 600;
        Assert.Equal(96 + 20_000 * 8.0 / 1000.0 / 600, mp4.SideK, 3);
        Assert.Equal(96 + kbit, mkv.SideK, 3);
    }

    [Fact]
    public void GecisYalnizKapTasirsaVeButceninYuzde15iniAsmazsaOlur()
    {
        var info = Kaynak(Video, new SourceStream(1, StreamKind.Audio, "aac", "eng", Channels: 2, BitrateBps: 128_000));

        var genis = StreamMapping.Decide(info, StreamRequest.Default, OutputContainer.Mp4, 160, null, "aac", true, 100);
        var dar = StreamMapping.Decide(info, StreamRequest.Default, OutputContainer.Mp4, 160, null, "aac", true, 5);
        var webm = StreamMapping.Decide(Kaynak(Video, new SourceStream(1, StreamKind.Audio, "ac3", "eng", Channels: 2, BitrateBps: 128_000)),
            StreamRequest.Default, OutputContainer.WebM, 160, null, "aac", true, 100);
        var izinsiz = StreamMapping.Decide(info, StreamRequest.Default, OutputContainer.Mp4, 160, null, "aac", false, 100);

        Assert.True(Assert.Single(genis.Audio).Copies);
        Assert.Equal(new[] { "-c:a", "copy" }, genis.OutputArguments().SkipWhile(arg => arg != "-c:a").Take(2));
        Assert.False(Assert.Single(dar.Audio).Copies);
        Assert.Equal("libopus", Assert.Single(webm.Audio).Codec);
        Assert.False(Assert.Single(izinsiz.Audio).Copies);
        Assert.True(100 * StreamMapping.PassthroughTargetShare * 8388.608 / 600 >= 128);
        Assert.True(5 * StreamMapping.PassthroughTargetShare * 8388.608 / 600 < 128);
    }

    [Fact]
    public void TrueHdVeDtsButceBolkenBileGecmez()
    {
        foreach (var codec in new[] { "truehd", "dts" })
        {
            var info = Kaynak(Video, new SourceStream(1, StreamKind.Audio, codec, "eng", Channels: 8, BitrateBps: 100_000));
            var plan = StreamMapping.Decide(info, new StreamRequest(KeepAllTracks: true), OutputContainer.Mkv, 640, null, "aac", true, 10_000);
            var track = Assert.Single(plan.Audio);
            Assert.False(track.Copies);
            Assert.Equal(2, track.Channels);
            Assert.Contains(StreamNote.LosslessAudioNotPassedThrough, plan.Notes);
        }

        var ac3 = Kaynak(Video, new SourceStream(1, StreamKind.Audio, "ac3", "eng", Channels: 6, BitrateBps: 100_000));
        Assert.True(Assert.Single(StreamMapping.Decide(ac3, new StreamRequest(KeepAllTracks: true), OutputContainer.Mkv, 640, null, "aac", true, 10_000).Audio).Copies);
    }

    [Fact]
    public void PlatformIstegiIzleriKoruyuEzerTekIzMp4AltyaziYok()
    {
        var info = Kaynak(
            Video,
            new SourceStream(1, StreamKind.Audio, "aac", "eng", Channels: 2, BitrateBps: 128_000),
            new SourceStream(2, StreamKind.Audio, "aac", "tur", Channels: 2, BitrateBps: 128_000),
            new SourceStream(3, StreamKind.Subtitle, "subrip", "tur", Bytes: 20_000));
        var request = new StreamRequest(KeepAllTracks: true, PlatformDelivery: true);

        var container = StreamMapping.ContainerFor(request);
        var plan = StreamMapping.Decide(info, request, container, 96, null, "aac", false, 16);

        Assert.Equal(OutputContainer.Mp4, container);
        Assert.Single(plan.Audio);
        Assert.Empty(plan.Subtitles);
        Assert.Contains(StreamNote.KeepAllTracksOverriddenByPlatform, plan.Notes);
        Assert.Equal(OutputContainer.Mkv, StreamMapping.ContainerFor(new StreamRequest(KeepAllTracks: true)));
    }

    [Fact]
    public void CokIzdeKanalIndirmesiSesIziSiradanYazilir()
    {
        var info = Kaynak(
            Video,
            new SourceStream(1, StreamKind.Audio, "aac", "eng", Channels: 2, BitrateBps: 128_000),
            new SourceStream(2, StreamKind.Audio, "ac3", "tur", Channels: 6, BitrateBps: 384_000));

        var args = StreamMapping.Decide(info, new StreamRequest(KeepAllTracks: true), OutputContainer.Mkv, 64, null, "aac", false, 100).OutputArguments();

        Assert.Equal(new[] { "-ac:a:1", "2" }, args.SkipWhile(arg => arg != "-ac:a:1").Take(2));
        Assert.DoesNotContain("-ac:1", args);
        Assert.DoesNotContain("-ac:a:0", args);
    }

    [Fact]
    public void EnvanterYoksaEskiBicimSesArgumanlariKorunur()
    {
        var info = Kaynak() with { AudioCodec = "aac", AudioChannels = 2, AudioBitrateBps = 128_000 };
        var plan = StreamMapping.Decide(info, StreamRequest.Default, OutputContainer.Mp4, 96, null, "aac", true, 100);

        Assert.Equal(
            new[] { "-map", "0:v:0", "-map", "0:a:0?", "-c:a", "aac", "-b:a", "96k", "-disposition:a:0", "default", "-map_metadata", "0", "-map_chapters", "0" },
            plan.OutputArguments());
    }

    [Fact]
    public void IzleriKoruButcedenFazlaIzinBaytiniDuser()
    {
        var info = Kaynak(
            Video,
            new SourceStream(1, StreamKind.Audio, "aac", "eng", Channels: 2, BitrateBps: 128_000),
            new SourceStream(2, StreamKind.Audio, "aac", "tur", Channels: 2, BitrateBps: 128_000),
            new SourceStream(3, StreamKind.Audio, "aac", "deu", Channels: 2, BitrateBps: 128_000),
            new SourceStream(4, StreamKind.Subtitle, "hdmv_pgs_subtitle", "eng", Bytes: 8_000_000));

        var tek = PlanCalculator.Build(info, new PlanOptions { TargetMb = 50, LockedCodec = "libx264" });
        var hepsi = PlanCalculator.Build(info, new PlanOptions { TargetMb = 50, LockedCodec = "libx264", KeepAllTracks = true });

        var rapor = string.Concat(
            I($"tek iz : kap {tek.Streams!.Container} ses {tek.Streams.Audio.Count} altyazi {tek.Streams.Subtitles.Count} yan {tek.NonVideoK:0.#} kbit video {tek.VideoBitrateK} kbit{Environment.NewLine}"),
            I($"hepsi  : kap {hepsi.Streams!.Container} ses {hepsi.Streams.Audio.Count} altyazi {hepsi.Streams.Subtitles.Count} yan {hepsi.NonVideoK:0.#} kbit video {hepsi.VideoBitrateK} kbit{Environment.NewLine}"),
            I($"hesap  : tek {(tek.VideoBitrateK + tek.NonVideoK) * 600 / 8388.608:0.00} MB, hepsi {(hepsi.VideoBitrateK + hepsi.NonVideoK) * 600 / 8388.608:0.00} MB (hedef 50){Environment.NewLine}"));
        AkisGirdisi.Write("birim-butce.txt", rapor);

        Assert.Equal(OutputContainer.Mkv, hepsi.Streams.Container);
        Assert.Equal(3, hepsi.Streams.Audio.Count);
        Assert.Single(hepsi.Streams.Subtitles);
        Assert.True(hepsi.NonVideoK > tek.NonVideoK + 100, rapor);
        Assert.True(hepsi.VideoBitrateK < tek.VideoBitrateK - 100, rapor);
        Assert.True((hepsi.VideoBitrateK + hepsi.NonVideoK) * 600 / 8388.608 <= 50, rapor);
        Assert.Equal(hepsi.Streams.SideK, hepsi.NonVideoK);
    }

    [FfmpegAvailableFact]
    public async Task VarsayilanMp4TekSesMetinAltyaziBolumVeUstVeriTasir()
    {
        var input = await AkisGirdisi.CokIzliAsync();
        var source = await AkisGirdisi.ProbeAsync(input);
        var options = AkisGirdisi.Secenek(0.5);
        options.PreferredLanguage = "tr";
        var (plan, _, output) = await AkisGirdisi.KodlaAsync(input, options, "varsayilan");

        AkisGirdisi.Write("varsayilan.txt", $"girdi:{Environment.NewLine}{source}cikti ({plan.Streams!.Container}):{Environment.NewLine}{output}notlar: {string.Join(", ", plan.Streams.Notes)}{Environment.NewLine}");

        Assert.Equal(2, source.Of("audio").Count());
        Assert.Equal(2, source.Of("subtitle").Count());
        Assert.Contains("mp4", output.FormatName);
        var audio = Assert.Single(output.Of("audio"));
        Assert.Equal(("aac", "tur", 2), (audio.Codec, audio.Language, audio.Channels));
        var subtitle = Assert.Single(output.Of("subtitle"));
        Assert.Equal(("mov_text", "tur"), (subtitle.Codec, subtitle.Language));
        Assert.Equal(2, output.Chapters);
        Assert.Equal("Deneme Basligi", output.Title);
        Assert.StartsWith("2024-05-01", output.CreationTime);
    }

    [FfmpegAvailableFact]
    public async Task DilTercihiIziSecerFfmpeginKendiSecimiSecmez()
    {
        var input = await AkisGirdisi.CokIzliAsync();
        var options = AkisGirdisi.Secenek(0.5);
        options.PreferredLanguage = "en";
        var (plan, _, output) = await AkisGirdisi.KodlaAsync(input, options, "ingilizce");

        var kontrol = Path.Combine(AkisGirdisi.Folder, "ffmpeg-kendi-secimi.mp4");
        await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, new[]
        {
            "-hide_banner", "-y", "-i", input, "-c:v", "libx264", "-preset", "ultrafast", "-c:a", "aac", "-b:a", "96k", kontrol
        });
        var negative = await AkisGirdisi.ProbeAsync(kontrol);

        AkisGirdisi.Write("dil-tercihi.txt", $"tercih en, eslemeli ({plan.Streams!.Audio[0].Action}):{Environment.NewLine}{output}eslemesiz ffmpeg:{Environment.NewLine}{negative}");

        Assert.Equal("eng", Assert.Single(output.Of("audio")).Language);
        Assert.Equal("tur", Assert.Single(negative.Of("audio")).Language);
        Assert.Empty(negative.Of("subtitle"));
        Assert.StartsWith("2024-05-01", output.CreationTime);
        Assert.True(string.IsNullOrEmpty(negative.CreationTime));
    }

    [FfmpegAvailableFact]
    public async Task IzleriKoruMkvTumSesVeAltyazilariTasir()
    {
        var input = await AkisGirdisi.CokIzliAsync();
        var options = AkisGirdisi.Secenek(0.5);
        options.KeepAllTracks = true;
        options.PreferredLanguage = "tr";
        var (plan, _, output) = await AkisGirdisi.KodlaAsync(input, options, "izleri-koru");

        AkisGirdisi.Write("izleri-koru.txt", $"cikti ({plan.Streams!.Container}):{Environment.NewLine}{output}ses kararlari: {string.Join(" | ", plan.Streams.Audio.Select(track => $"{track.Map} {track.Action} {track.Codec} {track.BitrateK}k ac {track.Channels}"))}{Environment.NewLine}notlar: {string.Join(", ", plan.Streams.Notes)}{Environment.NewLine}");

        Assert.Contains("matroska", output.FormatName);
        Assert.Equal(new[] { "eng", "tur" }, output.Of("audio").Select(track => track.Language));
        Assert.All(output.Of("audio"), track => Assert.True(track.Channels <= 2));
        Assert.Equal(new[] { ("subrip", "tur"), ("hdmv_pgs_subtitle", "eng") }, output.Of("subtitle").Select(track => (track.Codec, track.Language ?? "")));
        Assert.Equal(2, output.Chapters);
        Assert.Equal("Deneme Basligi", output.Title);
    }

    [FfmpegAvailableFact]
    public async Task PlatformCipiIzleriKoruAcikkenDeTekIzMp4Verir()
    {
        var input = await AkisGirdisi.CokIzliAsync();
        var options = AkisGirdisi.Secenek(0.5);
        options.KeepAllTracks = true;
        options.PlatformDelivery = true;
        var (plan, _, output) = await AkisGirdisi.KodlaAsync(input, options, "platform");

        AkisGirdisi.Write("platform.txt", $"cikti ({plan.Streams!.Container}):{Environment.NewLine}{output}");

        Assert.Contains("mp4", output.FormatName);
        Assert.Single(output.Of("audio"));
        Assert.Empty(output.Of("subtitle"));
    }

    [FfmpegAvailableFact]
    public async Task DonukKaynakGosterimdeDikeyKalir()
    {
        var input = await AkisGirdisi.DonukAsync();
        var source = await AkisGirdisi.ProbeAsync(input);
        var (_, _, output) = await AkisGirdisi.KodlaAsync(input, AkisGirdisi.Secenek(0.2), "donuk-cikti");

        AkisGirdisi.Write("donuk.txt", $"girdi:{Environment.NewLine}{source}cikti:{Environment.NewLine}{output}");

        static bool Dikey(Iz video) => Math.Abs(video.Rotation) % 180 == 90 ? video.Width > video.Height : video.Height > video.Width;
        var girdi = Assert.Single(source.Of("video"));
        Assert.True(girdi.Width > girdi.Height);
        Assert.True(Dikey(girdi));
        Assert.True(Dikey(Assert.Single(output.Of("video"))));
    }

    [FfmpegAvailableFact]
    public async Task CokIzliGirdideHedefBoyutTutarYanIzleriSaymayanButceTasar()
    {
        var input = await AkisGirdisi.CokIzliAsync();
        var info = await FfprobeClient.ProbeAsync(input);
        const double target = 0.3;
        var options = AkisGirdisi.Secenek(target);
        options.KeepAllTracks = true;
        options.PreferredLanguage = "tr";

        var (plan, result, output) = await AkisGirdisi.KodlaAsync(input, options, "hedef");

        var eski = plan.Clone();
        eski.VideoBitrateK += (int)Math.Round(plan.NonVideoK - plan.Streams!.Audio[0].BitrateK);
        var eskiYol = Path.Combine(AkisGirdisi.Folder, "hedef-eski-butce.mkv");
        if (File.Exists(eskiYol)) File.Delete(eskiYol);
        var eskiSonuc = await new EncodeRunner().RunAsync(info, eski, eskiYol, target, null, CancellationToken.None, options.FillPolicy,
            askBeforeRetry: (_, _) => Task.FromResult(false));
        var eskiIlk = eskiSonuc.Trace![0].ActualMb;

        var band = FillBand.For(target);
        var rapor = string.Concat(
            I($"hedef {target} MB, bant {band.HardFloorMb:0.000}-{band.UpperMb:0.000}{Environment.NewLine}"),
            I($"plan: video {plan.VideoBitrateK} kbit, yan izler {plan.NonVideoK:0.#} kbit (ses {string.Join("+", plan.Streams.Audio.Select(track => track.BitrateK))}), mod {plan.Mode}{Environment.NewLine}"),
            I($"yeni butce: {result.OutputMb:0.0000} MB, deneme {result.Attempts}, iz {string.Join(" | ", result.Trace!.Select(a => I($"{a.Number}:{a.ActualMb:0.0000}")))}{Environment.NewLine}"),
            I($"eski butce (yalniz ilk ses sayilir, video {eski.VideoBitrateK} kbit): ilk deneme {eskiIlk:0.0000} MB{Environment.NewLine}"),
            $"cikti:{Environment.NewLine}{output}");
        AkisGirdisi.Write("hedef-dogrulugu.txt", rapor);

        Assert.Equal(2, output.Of("audio").Count());
        Assert.Equal(2, output.Of("subtitle").Count());
        Assert.True(result.OutputMb <= target * 1.01, rapor);
        Assert.True(result.OutputMb >= band.HardFloorMb, rapor);
        Assert.True(eskiIlk > target, rapor);
    }
}
