using System.Diagnostics;
using System.Globalization;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

public sealed class KayipsizBirlestirmeTests
{
    private const long Gb = 1_000_000_000;

    private static string Kok => Path.Combine(GirdiKanit.Root, ".calisma", "yan-kayipsiz-birlestirme");

    private static long S(double saniye) => EditTime.FromSeconds(saniye);

    private static MediaInfo Bilgi(string yol, bool ses = true) => new()
    {
        FilePath = yol,
        FileSizeBytes = 1_000_000,
        DurationSeconds = 10,
        Width = 320,
        Height = 240,
        Fps = 30,
        VideoCodec = "h264",
        VideoProfile = "High",
        PixelFormat = "yuv420p",
        VideoTimeBase = "1/15360",
        VideoExtradataHash = "CRC32:9f62cb5f",
        AudioCodec = ses ? "aac" : null,
        AudioChannels = ses ? 2 : 0,
        TotalBitrateBps = 1_000_000,
        Streams = ses
            ? new[] { new SourceStream(0, StreamKind.Video, "h264"), new SourceStream(1, StreamKind.Audio, "aac", Channels: 2, SampleRate: 48000) }
            : new[] { new SourceStream(0, StreamKind.Video, "h264") }
    };

    private static MediaInfo SesHizi(MediaInfo bilgi, int hiz)
        => bilgi with { Streams = bilgi.Streams.Select(s => s.Kind == StreamKind.Audio ? s with { SampleRate = hiz } : s).ToArray() };

    private static EditTimeline IkiKaynak()
    {
        var model = EditTimeline.FromSource(S(10));
        Assert.Equal(1, model.AddSource(S(4)));
        return model;
    }

    private static Dictionary<int, ExportSource> Kaynaklar(MediaInfo? ikinci = null, double[]? kareler = null) => new()
    {
        [0] = new ExportSource(Bilgi("birinci.mp4"), new[] { 0.0, 2.0, 4.0 }, 0),
        [1] = new ExportSource(ikinci ?? Bilgi("ikinci.mp4"), kareler ?? new[] { 0.0, 2.0 }, 0.1)
    };

    [Fact]
    public void AyniKaynaklarUyumludur()
    {
        Assert.Equal(MergeMismatch.None, MergeCompatibility.Check(new[] { Bilgi("a.mp4"), Bilgi("b.mp4") }));
        Assert.Equal(MergeMismatch.None, MergeCompatibility.Check(new[] { Bilgi("a.mp4", false), Bilgi("b.mp4", false) }));
        Assert.Equal(MergeMismatch.None, MergeCompatibility.Check(new[] { Bilgi("a.mp4"), Bilgi("b.mp4") with { Fps = 30.0004, VideoCodec = "H264" } }));
    }

    public static TheoryData<string, MergeMismatch> Farklar()
    {
        return new TheoryData<string, MergeMismatch>
        {
            { "kodek", MergeMismatch.VideoCodec },
            { "profil", MergeMismatch.VideoProfile },
            { "baslik-verisi", MergeMismatch.Extradata },
            { "genislik", MergeMismatch.Resolution },
            { "yukseklik", MergeMismatch.Resolution },
            { "piksel-orani", MergeMismatch.PixelAspect },
            { "piksel-bicimi", MergeMismatch.PixelFormat },
            { "piksel-bicimi-yok", MergeMismatch.PixelFormat },
            { "kare-hizi", MergeMismatch.FrameRate },
            { "zaman-tabani", MergeMismatch.TimeBase },
            { "zaman-tabani-yok", MergeMismatch.TimeBase },
            { "ses-yok", MergeMismatch.AudioPresence },
            { "ses-kodegi", MergeMismatch.AudioCodec },
            { "ornekleme", MergeMismatch.SampleRate },
            { "ornekleme-yok", MergeMismatch.SampleRate },
            { "kanal", MergeMismatch.Channels },
            { "akis-dizilisi", MergeMismatch.StreamLayout },
            { "yalniz-ses", MergeMismatch.Unsupported },
            { "gif", MergeMismatch.Unsupported }
        };
    }

    private static MediaInfo Bozuk(string alan)
    {
        var b = Bilgi("b.mp4");
        return alan switch
        {
            "kodek" => b with { VideoCodec = "hevc" },
            "profil" => b with { VideoProfile = "Main" },
            "baslik-verisi" => b with { VideoExtradataHash = "CRC32:58ff8c70" },
            "genislik" => b with { Width = 322 },
            "yukseklik" => b with { Height = 238 },
            "piksel-orani" => b with { ParNum = 4, ParDen = 3 },
            "piksel-bicimi" => b with { PixelFormat = "yuv420p10le" },
            "piksel-bicimi-yok" => b with { PixelFormat = null },
            "kare-hizi" => b with { Fps = 29.97 },
            "zaman-tabani" => b with { VideoTimeBase = "1/12800" },
            "zaman-tabani-yok" => b with { VideoTimeBase = null },
            "ses-yok" => Bilgi("b.mp4", false),
            "ses-kodegi" => b with { AudioCodec = "opus" },
            "ornekleme" => SesHizi(b, 44100),
            "ornekleme-yok" => SesHizi(b, 0),
            "kanal" => b with { AudioChannels = 1 },
            "akis-dizilisi" => b with { Streams = b.Streams.Reverse().ToArray() },
            "yalniz-ses" => b with { IsAudioOnly = true },
            "gif" => b with { VideoCodec = "gif" },
            _ => throw new ArgumentOutOfRangeException(nameof(alan), alan, null)
        };
    }

    [Theory]
    [MemberData(nameof(Farklar))]
    public void TekAlanFarkiKopyayiKapatir(string alan, MergeMismatch beklenen)
    {
        var ikinci = Bozuk(alan);
        Assert.Equal(beklenen, MergeCompatibility.Check(new[] { Bilgi("a.mp4"), ikinci }));
        Assert.Equal(MergeDecision.Incompatible, MergeCompatibility.Decide(IkiKaynak(), Kaynaklar(ikinci), ExportMode.Fast, EditExportSettings.Default, EditAudioTrack.Default));

        var plan = EditExport.Build(IkiKaynak(), Kaynaklar(ikinci), ExportMode.Fast, "cikti.mp4", "is", 8 * Gb);
        Assert.Equal(ExportMode.Full, plan.Effective);
        Assert.True(plan.MergeForcedFull);
        Assert.Equal(MergeDecision.Incompatible, plan.MergeDecision);
    }

    [Fact]
    public void IlkKaynaktaOkunamayanAlanUyumsuzdur()
    {
        Assert.Equal(MergeMismatch.PixelFormat, MergeCompatibility.Check(new[] { Bilgi("a.mp4") with { PixelFormat = null }, Bilgi("b.mp4") }));
        Assert.Equal(MergeMismatch.TimeBase, MergeCompatibility.Check(new[] { Bilgi("a.mp4") with { VideoTimeBase = null }, Bilgi("b.mp4") }));
        Assert.Equal(MergeMismatch.SampleRate, MergeCompatibility.Check(new[] { SesHizi(Bilgi("a.mp4"), 0), Bilgi("b.mp4") }));
        Assert.Equal(MergeMismatch.Unsupported, MergeCompatibility.Check(Array.Empty<MediaInfo>()));
    }

    [Fact]
    public void KararYenidenKodlamaIsteyenCizelgedeKopyaDemez()
    {
        var ayar = EditExportSettings.Default;
        Assert.Equal(MergeDecision.Copy, MergeCompatibility.Decide(IkiKaynak(), Kaynaklar(), ExportMode.Fast, ayar, EditAudioTrack.Default));
        Assert.Equal(MergeDecision.Copy, MergeCompatibility.Decide(IkiKaynak(), Kaynaklar(), ExportMode.Smart, ayar, EditAudioTrack.First));
        Assert.Equal(MergeDecision.FullRequested, MergeCompatibility.Decide(IkiKaynak(), Kaynaklar(), ExportMode.Full, ayar, EditAudioTrack.Default));
        Assert.Equal(MergeDecision.Settings, MergeCompatibility.Decide(IkiKaynak(), Kaynaklar(), ExportMode.Fast, ayar with { Fps = 24 }, EditAudioTrack.Default));
        Assert.Equal(MergeDecision.AudioTrack, MergeCompatibility.Decide(IkiKaynak(), Kaynaklar(), ExportMode.Fast, ayar, 1));
        Assert.Equal(MergeDecision.NoKeyframes, MergeCompatibility.Decide(IkiKaynak(), Kaynaklar(kareler: Array.Empty<double>()), ExportMode.Fast, ayar, EditAudioTrack.Default));

        var hizli = new EditTimeline(new[] { new EditClip(0, S(10)), new EditClip(0, S(4), 2m) { Source = 1 } }, extraSources: new[] { S(4) });
        Assert.Equal(MergeDecision.Edits, MergeCompatibility.Decide(hizli, Kaynaklar(), ExportMode.Fast, ayar, EditAudioTrack.Default));
        var ters = new EditTimeline(new[] { new EditClip(0, S(10)), new EditClip(0, S(4), reversed: true) { Source = 1 } }, extraSources: new[] { S(4) });
        Assert.Equal(MergeDecision.Edits, MergeCompatibility.Decide(ters, Kaynaklar(), ExportMode.Fast, ayar, EditAudioTrack.Default));
    }

    [Fact]
    public void AkilliKipteAnahtarKareDisiKesimYenidenKodlanirHizlidaKayar()
    {
        var kesik = new EditTimeline(new[] { new EditClip(S(1), S(10)), new EditClip(S(2), S(4)) { Source = 1 } }, extraSources: new[] { S(4) });
        var ayar = EditExportSettings.Default;
        Assert.Equal(MergeDecision.CutOffKeyframe, MergeCompatibility.Decide(kesik, Kaynaklar(), ExportMode.Smart, ayar, EditAudioTrack.Default));
        Assert.Equal(MergeDecision.Copy, MergeCompatibility.Decide(kesik, Kaynaklar(), ExportMode.Fast, ayar, EditAudioTrack.Default));

        var akilli = EditExport.Build(kesik, Kaynaklar(), ExportMode.Smart, "cikti.mp4", "is", 8 * Gb);
        Assert.Equal(ExportMode.Full, akilli.Effective);
        Assert.True(akilli.MergeForcedFull);

        var hizli = EditExport.Build(kesik, Kaynaklar(), ExportMode.Fast, "cikti.mp4", "is", 8 * Gb);
        Assert.Equal(ExportMode.Fast, hizli.Effective);
        Assert.Equal(new[] { 1.0, 0.0 }, hizli.KeyframeLeads);

        var tam = new EditTimeline(new[] { new EditClip(S(2), S(10)), new EditClip(S(2), S(4)) { Source = 1 } }, extraSources: new[] { S(4) });
        Assert.Equal(MergeDecision.Copy, MergeCompatibility.Decide(tam, Kaynaklar(), ExportMode.Smart, ayar, EditAudioTrack.Default));
    }

    [Fact]
    public void UyumluKaynaklarConcatGirisiyleKopyalanir()
    {
        var kesik = new EditTimeline(new[] { new EditClip(S(3), S(10)), new EditClip(0, S(4)) { Source = 1 } }, extraSources: new[] { S(4) });
        var plan = EditExport.Build(kesik, Kaynaklar(), ExportMode.Fast, "cikti.mp4", "is", 8 * Gb);
        var adim = Assert.Single(plan.Steps);
        var args = adim.Args;

        Assert.Equal(ExportMode.Fast, plan.Effective);
        Assert.False(plan.MergeForcedFull);
        Assert.Equal(MergeDecision.Copy, plan.MergeDecision);
        Assert.DoesNotContain("-filter_complex", args);
        Assert.Equal(new[] { "-f", "concat", "-safe", "0", "-i", Path.Combine("is", EditExport.VideoListName) }, args.Skip(3).Take(6));
        Assert.Equal(new[] { "-map", "0:v:0", "-map", "0:a:0", "-c", "copy" }, args.Skip(9).Take(6));
        Assert.Equal("cikti.mp4", args[^1]);
        Assert.Equal(Path.Combine("is", EditExport.VideoListName), adim.ListPath);
        Assert.Equal("ffconcat version 1.0\nfile 'birinci.mp4'\ninpoint 2\noutpoint 10\nfile 'ikinci.mp4'\noutpoint 4.1\n", adim.ListContent);
        Assert.Equal(12.0, adim.DurationSeconds, 6);
        Assert.Equal(new[] { 1.0, 0.0 }, plan.KeyframeLeads);

        var sessiz = new Dictionary<int, ExportSource>
        {
            [0] = new ExportSource(Bilgi("birinci.mp4", false), new[] { 0.0 }, 0),
            [1] = new ExportSource(Bilgi("ikinci.mp4", false), new[] { 0.0 }, 0)
        };
        var sessizPlan = EditExport.Build(IkiKaynak(), sessiz, ExportMode.Smart, "cikti.mp4", "is", 8 * Gb);
        Assert.Equal(ExportMode.Smart, sessizPlan.Effective);
        Assert.DoesNotContain("0:a:0", sessizPlan.Steps[0].Args);
    }

    private static void Ffmpeg(params string[] args)
    {
        var psi = new ProcessStartInfo("ffmpeg") { RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var surec = Process.Start(psi)!;
        var hata = surec.StandardError.ReadToEndAsync();
        if (!surec.WaitForExit(30000))
        {
            surec.Kill(true);
            Assert.Fail("ffmpeg 30 sn icinde bitmedi.");
        }

        Assert.True(surec.ExitCode == 0, hata.GetAwaiter().GetResult());
    }

    private static int KareSayisi(string yol)
    {
        var psi = new ProcessStartInfo("ffprobe") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in new[] { "-v", "error", "-select_streams", "v:0", "-count_packets", "-show_entries", "stream=nb_read_packets", "-of", "csv=p=0", yol })
            psi.ArgumentList.Add(a);
        using var surec = Process.Start(psi)!;
        var hata = surec.StandardError.ReadToEndAsync();
        var cikti = surec.StandardOutput.ReadToEnd();
        Assert.True(surec.WaitForExit(30000), "ffprobe 30 sn icinde bitmedi.");
        hata.GetAwaiter().GetResult();
        return int.Parse(cikti.Trim(), CultureInfo.InvariantCulture);
    }

    private static void Uret(string yol, int referans = 1)
        => Ffmpeg("-hide_banner", "-y",
            "-f", "lavfi", "-i", "testsrc2=s=320x240:r=30:d=1",
            "-f", "lavfi", "-i", "sine=f=440:d=1:sample_rate=48000",
            "-c:v", "libx264", "-preset", "ultrafast", "-g", "15", "-x264-params", $"ref={referans}", "-pix_fmt", "yuv420p", "-threads", "2",
            "-c:a", "aac", "-ac", "2", "-shortest", yol);

    [FfmpegFact]
    public async Task UyumluIkiKlipYenidenKodlanmadanBirlesir()
    {
        var klasor = Path.Combine(Kok, "canli");
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        Directory.CreateDirectory(klasor);
        try
        {
            var a = Path.Combine(klasor, "a.mp4");
            var b = Path.Combine(klasor, "b.mp4");
            Uret(a);
            Uret(b);

            var ilk = await FfprobeClient.ProbeAsync(a);
            var ek = await FfprobeClient.ProbeAsync(b);
            Assert.NotNull(ilk.VideoTimeBase);
            Assert.Equal(ilk.VideoTimeBase, ek.VideoTimeBase);
            Assert.Equal(MergeMismatch.None, MergeCompatibility.Check(new[] { ilk, ek }));

            var farkli = Path.Combine(klasor, "c.mp4");
            Uret(farkli, 3);
            var baska = await FfprobeClient.ProbeAsync(farkli);
            Assert.NotNull(baska.VideoExtradataHash);
            Assert.Equal(MergeMismatch.Extradata, MergeCompatibility.Check(new[] { ilk, baska }));

            var model = EditTimeline.FromSource(EditTime.FromSeconds(ilk.DurationSeconds));
            Assert.Equal(1, model.AddSource(EditTime.FromSeconds(ek.DurationSeconds)));
            var cikti = Path.Combine(klasor, "birlesik.mp4");
            var plan = await EditExportRunner.PrepareAsync(new[] { a, b }, model, ExportMode.Fast, cikti, 8 * Gb);

            Assert.Equal(MergeDecision.Copy, plan.MergeDecision);
            Assert.Equal(ExportMode.Fast, plan.Effective);
            Assert.DoesNotContain("-filter_complex", plan.Steps[0].Args);

            await EditExportRunner.RunAsync(plan, null);

            var sonuc = await FfprobeClient.ProbeAsync(cikti);
            Assert.Equal(KareSayisi(a) + KareSayisi(b), KareSayisi(cikti));
            Assert.InRange(sonuc.DurationSeconds, 1.9, 2.15);
            Assert.Equal(320, sonuc.Width);
            Assert.Equal(30.0, sonuc.Fps, 1);
            Assert.True(sonuc.HasAudio);
            Assert.Equal("h264", sonuc.VideoCodec);
        }
        finally
        {
            if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
            KanitKapanisi.Kapat(Kok);
        }
    }
}
