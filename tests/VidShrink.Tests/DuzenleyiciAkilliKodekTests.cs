using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using Xunit;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// ffmpeg ve adi verilen kodlayicilar bu derlemede yoksa atlanir; atlanan kol kosum dokumunde
/// sebebiyle gorunur, yesil sayilmaz.
/// </summary>
public sealed class AkilliKodekFactAttribute : FactAttribute
{
    public AkilliKodekFactAttribute(params string[] kodlayicilar)
    {
        if (!ToolLocator.IsAvailable(out var missing))
        {
            Skip = $"{missing} bulunamadi, akilli kesme kolu kosturulmadi.";
            return;
        }

        foreach (var kodlayici in kodlayicilar)
        {
            if (EncoderCapabilities.Instance.HasEncoder(kodlayici)) continue;
            Skip = $"{kodlayici} bu ffmpeg derlemesinde yok, akilli kesme kolu kosturulmadi.";
            return;
        }
    }
}

/// <summary>
/// Akilli kesmenin kodek kapisi (<see cref="SmartCutCodec"/>, <c>docs/olcumler/akilli-kesme-kodekler.md</c>).
/// Saf kollar kapinin kararini ve plan argumanlarini her yerde pimler. Canli kollar 6 sn 320x240
/// lavfi kaynagindan 4 sn'lik kesimi gercek ffmpeg ile teslim edip dort olcutu okur: sure, cozme
/// hatasi, paket/kare sayisi, bas ile govdenin ayni akista cozulmesi (govde kaynakla bayt bayt ayni).
/// Kanit <c>.calisma/worktree-agent-a037bbec50fedd114/akilli-kodek/</c>, test siler.
/// </summary>
public sealed class DuzenleyiciAkilliKodekTests
{
    private const long Gb = 1_000_000_000;
    private const double Kare = 1.0 / 24;

    private readonly ITestOutputHelper _cikti;

    public DuzenleyiciAkilliKodekTests(ITestOutputHelper cikti) => _cikti = cikti;

    private static long S(double saniye) => EditTime.FromSeconds(saniye);

    internal static IReadOnlyList<SmartCutPoint> Sinirlar(IReadOnlyList<double> kareler, double fps = 30)
        => kareler.Select(k => new SmartCutPoint(k, (int)Math.Round(k * fps))).ToArray();

    private static MediaInfo Bilgi(
        string kodek = "h264", string? profil = null, string piksel = "yuv420p", int bit = 8, string? etiket = null,
        string? kap = "mov,mp4,m4a,3gp,3g2,mj2", int gecikme = 0, string? ses = "aac", bool taramali = false) => new()
    {
        FilePath = "kaynak.mp4",
        FileSizeBytes = 1,
        DurationSeconds = 60,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = kodek,
        TotalBitrateBps = 1,
        AudioCodec = ses,
        PixelFormat = piksel,
        BitDepth = bit,
        VideoProfile = profil,
        VideoCodecTag = etiket,
        FormatName = kap,
        VideoDelayFrames = gecikme,
        IsInterlaced = taramali
    };

    private static string Deger(IReadOnlyList<string> args, string anahtar) => args[args.ToList().IndexOf(anahtar) + 1];

    private static readonly double[] Kareler = { 0.0, 2, 4, 6, 8 };

    private static ExportPlan Plan(MediaInfo bilgi, string cikti = "c.mp4", Func<string, bool>? kodlayici = null, EditTimeline? cizelge = null)
        => EditExport.Build(cizelge ?? new EditTimeline(new[] { new EditClip(S(1), S(7)) }), bilgi, Kareler, 0, ExportMode.Smart,
            cikti, "is", 8 * Gb, Sinirlar(Kareler), kodlayici);

    [Theory]
    [InlineData("h264", "libx264")]
    [InlineData("hevc", "libx265")]
    [InlineData("av1", "libsvtav1")]
    [InlineData("vp9", "libvpx-vp9")]
    public void OlcumuGecenKodekKapidanGecerKenariKendiKodegiyleKodlar(string kodek, string kodlayici)
    {
        var plan = Plan(Bilgi(kodek));

        Assert.Equal(ExportMode.Smart, plan.Effective);
        Assert.False(plan.FellBackToFull);
        Assert.Equal(kodlayici, Deger(plan.Steps[0].Args, "-c:v"));
        Assert.Equal("copy", Deger(plan.Steps[1].Args, "-c"));
        Assert.Equal(kodlayici, Deger(plan.Steps[2].Args, "-c:v"));
        Assert.Contains(kodek, SmartCutCodec.Codecs);
    }

    [Theory]
    [InlineData("mpeg4")]
    [InlineData("mpeg2video")]
    [InlineData("prores")]
    [InlineData("uydurma-kodek")]
    [InlineData("")]
    public void OlcumuGecmeyenVeUydurmaKodekTamKipeDuser(string kodek)
    {
        var plan = Plan(Bilgi(kodek));

        Assert.Equal(ExportMode.Smart, plan.Requested);
        Assert.Equal(ExportMode.Full, plan.Effective);
        Assert.True(plan.FellBackToFull);
        Assert.Null(SmartCutCodec.Resolve(Bilgi(kodek)));
        Assert.DoesNotContain(kodek, SmartCutCodec.Codecs);
    }

    [Fact]
    public void KapiTamDortKodekTasir()
        => Assert.Equal(new[] { "av1", "h264", "hevc", "vp9" }, SmartCutCodec.Codecs.OrderBy(k => k, StringComparer.Ordinal));

    [Fact]
    public void KodlayiciMakinedeYoksaTamKipeDuser()
    {
        var sorulan = new List<string>();
        var yok = Plan(Bilgi("av1"), kodlayici: ad => { sorulan.Add(ad); return false; });

        Assert.Equal(ExportMode.Full, yok.Effective);
        Assert.True(yok.FellBackToFull);
        Assert.Equal(new[] { "libsvtav1" }, sorulan.Distinct());

        Assert.Equal(ExportMode.Smart, Plan(Bilgi("av1"), kodlayici: _ => true).Effective);
        Assert.Equal(ExportMode.Full, Plan(Bilgi("h264"), kodlayici: ad => ad != "libx264").Effective);
        Assert.Equal(ExportMode.Smart, Plan(Bilgi("h264"), kodlayici: ad => ad == "libx264").Effective);
    }

    [Theory]
    [InlineData("h264", "High 10", "high10")]
    [InlineData("hevc", "Main 10", null)]
    [InlineData("av1", "Main", null)]
    [InlineData("vp9", "Profile 2", null)]
    public void OnBitKaynakOnBitKodlanir(string kodek, string profil, string? beklenenProfil)
    {
        var esleme = SmartCutCodec.Resolve(Bilgi(kodek, profil, "yuv420p10le", 10));

        Assert.NotNull(esleme);
        Assert.Equal("yuv420p10le", Deger(esleme!.VideoArgs, "-pix_fmt"));
        Assert.DoesNotContain("yuv420p", esleme.VideoArgs);
        if (beklenenProfil is not null) Assert.Equal(beklenenProfil, Deger(esleme.VideoArgs, "-profile:v"));

        var plan = Plan(Bilgi(kodek, profil, "yuv420p10le", 10));
        Assert.Equal(ExportMode.Smart, plan.Effective);
        Assert.Equal("yuv420p10le", Deger(plan.Steps[0].Args, "-pix_fmt"));
    }

    [Theory]
    [InlineData("h264", "High", "yuv420p10le", 10)]
    [InlineData("h264", "High 10", "yuv420p", 8)]
    [InlineData("h264", "High 4:2:2", "yuv422p10le", 10)]
    [InlineData("h264", "High 4:4:4 Predictive", "yuv420p", 8)]
    [InlineData("hevc", "Main", "yuv420p10le", 10)]
    [InlineData("hevc", "Rext", "yuv420p", 8)]
    [InlineData("hevc", "Main 10", "yuv420p12le", 12)]
    [InlineData("av1", "High", "yuv420p", 8)]
    [InlineData("vp9", "Profile 0", "yuv420p10le", 10)]
    [InlineData("vp9", "Profile 1", "yuv444p", 8)]
    [InlineData("h264", "High", "yuvj420p", 8)]
    [InlineData("h264", "High", "yuv420p10le", 8)]
    [InlineData("h264", "High", "yuv420p", 10)]
    public void EslenemeyenProfilVePikselBicimiTamKipeDuser(string kodek, string profil, string piksel, int bit)
    {
        var bilgi = Bilgi(kodek, profil, piksel, bit);

        Assert.Null(SmartCutCodec.Resolve(bilgi));
        var plan = Plan(bilgi);
        Assert.Equal(ExportMode.Full, plan.Effective);
        Assert.True(plan.FellBackToFull);
    }

    [Fact]
    public void ProfilKaynaktanOkunurOkunamazsaKodlayiciyaBirakilir()
    {
        Assert.Equal("baseline", Deger(SmartCutCodec.Resolve(Bilgi(profil: "Constrained Baseline"))!.VideoArgs, "-profile:v"));
        Assert.Equal("main", Deger(SmartCutCodec.Resolve(Bilgi(profil: "Main"))!.VideoArgs, "-profile:v"));
        Assert.Equal("high", Deger(SmartCutCodec.Resolve(Bilgi(profil: "High"))!.VideoArgs, "-profile:v"));
        Assert.DoesNotContain("-profile:v", SmartCutCodec.Resolve(Bilgi())!.VideoArgs);
        Assert.Null(SmartCutCodec.Resolve(Bilgi(taramali: true)));
        Assert.NotNull(SmartCutCodec.Resolve(Bilgi()));
    }

    [Fact]
    public void SesKopyalanamiyorsaYaDaSinirOkunmadiysaTamKipeDuser()
    {
        foreach (var ses in new[] { "mp3", "ac3", "vorbis", "flac" })
        {
            var dusen = Plan(Bilgi("vp9", ses: ses), "c.mkv");
            Assert.Equal(ExportMode.Full, dusen.Effective);
            Assert.True(dusen.AudioForcedFull, ses);
        }

        var aac = Plan(Bilgi("vp9", ses: "aac"), "c.mkv");
        Assert.Equal(ExportMode.Smart, aac.Effective);
        Assert.False(aac.AudioForcedFull);

        var cizelge = new EditTimeline(new[] { new EditClip(S(1), S(7)) });
        Assert.Equal(ExportMode.Full, EditExport.Build(cizelge, Bilgi(), Kareler, 0, ExportMode.Smart, "c.mp4", "is", 8 * Gb).Effective);
        Assert.Equal(ExportMode.Full, EditExport.Build(cizelge, Bilgi(), Kareler, 0, ExportMode.Smart, "c.mp4", "is", 8 * Gb,
            Array.Empty<SmartCutPoint>()).Effective);
    }

    [Theory]
    [InlineData("vp9", "c.mkv")]
    [InlineData("vp9", "c.mp4")]
    [InlineData("av1", "c.MKV")]
    [InlineData("h264", "c.mp4")]
    public void OpusSesDuzKesimdeAynenKopyalanirAkilliKipKalir(string kodek, string cikti)
    {
        var plan = Plan(Bilgi(kodek, ses: "Opus"), cikti);

        Assert.Equal(ExportMode.Smart, plan.Effective);
        Assert.False(plan.AudioForcedFull);
        Assert.False(plan.FellBackToFull);
        var ses = Assert.Single(plan.Steps, s => s.Args.Contains("0:a:0")).Args;
        Assert.Equal("copy", Deger(ses, "-c"));
        Assert.Equal("mpegts", Deger(ses, "-f"));
        Assert.DoesNotContain("-c:a", ses);
    }

    [Theory]
    [InlineData("c.mov")]
    [InlineData("c.m4v")]
    [InlineData("c.webm")]
    public void OpusSesTasimayanKaptaTamKipeDuserNedeniSestir(string cikti)
    {
        var plan = Plan(Bilgi("vp9", ses: "opus"), cikti);

        Assert.Equal(ExportMode.Full, plan.Effective);
        Assert.True(plan.AudioForcedFull);
    }

    [Fact]
    public void OpusSesHizYaDaTersKliptaTamKipeDuserAacKalir()
    {
        var hizli = new EditTimeline(new[] { new EditClip(S(1), S(3)), new EditClip(S(4), S(6), 2m) });
        var ters = new EditTimeline(new[] { new EditClip(S(1), S(3)), new EditClip(S(4), S(6), 1m, true) });

        foreach (var cizelge in new[] { hizli, ters })
        {
            var opus = Plan(Bilgi("vp9", ses: "opus"), "c.mkv", cizelge: cizelge);
            Assert.Equal(ExportMode.Full, opus.Effective);
            Assert.True(opus.AudioForcedFull);

            var aac = Plan(Bilgi("vp9", ses: "aac"), "c.mkv", cizelge: cizelge);
            Assert.Equal(ExportMode.Smart, aac.Effective);
            Assert.False(aac.AudioForcedFull);
        }
    }

    [Fact]
    public void SesNedeniYalnizGoruntuKapidanGecinceSoylenir()
    {
        var goruntu = Plan(Bilgi("mpeg4", ses: "mp3"));
        Assert.Equal(ExportMode.Full, goruntu.Effective);
        Assert.True(goruntu.FellBackToFull);
        Assert.False(goruntu.AudioForcedFull);

        var tam = EditExport.Build(new EditTimeline(new[] { new EditClip(S(1), S(7)) }), Bilgi("vp9", ses: "mp3"), Kareler, 0,
            ExportMode.Full, "c.mkv", "is", 8 * Gb, Sinirlar(Kareler));
        Assert.False(tam.AudioForcedFull);

        var hizli = EditExport.Build(new EditTimeline(new[] { new EditClip(S(1), S(7)) }), Bilgi("h264", ses: "opus"), Kareler, 0,
            ExportMode.Fast, "c.mkv", "is", 8 * Gb, Sinirlar(Kareler));
        Assert.False(hizli.AudioForcedFull);

        var parcalar = EditExport.BuildSegments(
            new EditTimeline(new[] { new EditClip(S(1), S(3)), new EditClip(S(4), S(6)) }), Bilgi("vp9", ses: "mp3"), Kareler, 0,
            ExportMode.Smart, new[] { "a.mkv", "b.mkv" }, new[] { "is1", "is2" }, 8 * Gb, Sinirlar(Kareler));
        Assert.All(parcalar, p => Assert.True(p.AudioForcedFull));
        Assert.All(parcalar, p => Assert.Equal(ExportMode.Full, p.Effective));
    }

    [Fact]
    public void SesNedeniGenelNottanOnceSoylenirMetniButunDillerdeAyridir()
    {
        var kaynak = File.ReadAllText(Path.Combine(GirdiKanit.Root, "src", "VidShrink.App", "Editing", "EditorView.Teslim.cs"));
        var ses = kaynak.IndexOf("plan.AudioForcedFull) lines.Add(Strings.Get(\"editor.export.audio-full\"))", StringComparison.Ordinal);
        var genel = kaynak.IndexOf("plan.FellBackToFull) lines.Add(Strings.Get(\"editor.export.fallback\"))", StringComparison.Ordinal);
        Assert.True(ses > 0, "ses notu Notes icinde yok");
        Assert.True(genel > ses, "genel dusus notu ses notundan once geliyor");

        Assert.Equal(42, Locales.Languages.Count);
        foreach (var dil in Locales.Languages)
        {
            var anahtarlar = VidShrink.App.Localization.Strings.KeysOf(dil).ToHashSet(StringComparer.Ordinal);
            Assert.True(anahtarlar.Contains("editor.export.audio-full"), $"{dil} dilinde editor.export.audio-full yok.");
            var metin = VidShrink.App.Localization.Strings.GetIn(dil, "editor.export.audio-full");
            Assert.False(string.IsNullOrWhiteSpace(metin), $"{dil} dilinde ses notu bos.");
            Assert.NotEqual(VidShrink.App.Localization.Strings.GetIn(dil, "editor.export.fallback"), metin);
            Assert.DoesNotContain("{0}", metin, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void GovdeKareSayisiylaKesilirSesAyriListedenGelir()
    {
        var plan = Plan(Bilgi());

        Assert.Equal(5, plan.Steps.Count);
        var govde = plan.Steps[1].Args;
        Assert.Equal("2.001", Deger(govde, "-ss"));
        Assert.Equal("4", Deger(govde, "-t"));
        Assert.Equal("120", Deger(govde, "-frames:v"));
        Assert.Equal("make_zero", Deger(govde, "-avoid_negative_ts"));
        Assert.Contains("-an", govde);

        var ses = plan.Steps[3].Args;
        Assert.Equal(new[] { "-ss", "1", "-i", "kaynak.mp4", "-ss", "0", "-t", "6" }, ses.Skip(3).Take(8));
        Assert.Equal("mpegts", Deger(ses, "-f"));

        var son = plan.Steps[4];
        Assert.Equal(Path.Combine("is", EditExport.VideoListName), son.ListPath);
        Assert.Equal(Path.Combine("is", EditExport.AudioListName), son.AudioListPath);
        Assert.Equal("ffconcat version 1.0\nfile 'is/v0000.ts'\nduration 1\nfile 'is/v0001.ts'\nduration 4\nfile 'is/v0002.ts'\nduration 1\n", son.ListContent);
        Assert.Equal("ffconcat version 1.0\nfile 'is/a0000.ts'\nduration 6\n", son.AudioListContent);
        Assert.Equal(2, son.Args.Count(a => a == "concat"));
        Assert.Contains("1:a:0", son.Args);
        Assert.Equal(6, son.DurationSeconds, 6);

        var sessiz = Plan(Bilgi(ses: null));
        Assert.Equal(4, sessiz.Steps.Count);
        Assert.Null(sessiz.Steps[^1].AudioListPath);
        Assert.Null(sessiz.Steps[^1].AudioListContent);
        Assert.Single(sessiz.Steps[^1].Args, a => a == "concat");
        Assert.DoesNotContain("1:a:0", sessiz.Steps[^1].Args);
    }

    [Fact]
    public void AraKapKodegeGoreSecilirMp4ParcalarOrtakZamanOlcegiAlir()
    {
        foreach (var kodek in new[] { "av1", "vp9" })
        {
            var plan = Plan(Bilgi(kodek));
            foreach (var adim in plan.Steps.Take(3))
            {
                Assert.Equal("mp4", Deger(adim.Args, "-f"));
                Assert.Equal("90000", Deger(adim.Args, "-video_track_timescale"));
                Assert.EndsWith(".mp4", adim.Args[^1], StringComparison.Ordinal);
            }
        }

        foreach (var kodek in new[] { "h264", "hevc" })
        {
            var plan = Plan(Bilgi(kodek));
            foreach (var adim in plan.Steps.Take(3))
            {
                Assert.Equal("mpegts", Deger(adim.Args, "-f"));
                Assert.DoesNotContain("-video_track_timescale", adim.Args);
                Assert.EndsWith(".ts", adim.Args[^1], StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void Hvc1EtiketiYalnizMp4AilesindeVeKaynakTasiyorsaKorunur()
    {
        Assert.Equal("hvc1", Deger(Plan(Bilgi("hevc", etiket: "hvc1")).Steps[^1].Args, "-tag:v"));
        Assert.Equal("hvc1", Deger(Plan(Bilgi("hevc", etiket: "hvc1"), "c.mov").Steps[^1].Args, "-tag:v"));
        Assert.DoesNotContain("-tag:v", Plan(Bilgi("hevc", etiket: "hev1")).Steps[^1].Args);
        Assert.DoesNotContain("-tag:v", Plan(Bilgi("hevc", etiket: "hvc1"), "c.mkv").Steps[^1].Args);
        Assert.DoesNotContain("-tag:v", Plan(Bilgi("h264", etiket: "avc1")).Steps[^1].Args);
    }

    [Fact]
    public void PtsIleAramayanKaptaBKareliAkisBuyukAramaPayiAlir()
    {
        var mkv = Bilgi("hevc", kap: "matroska,webm", gecikme: 2);
        var beklenen = 2 + 3.0 / 23.0 + 0.001;

        Assert.Equal(3.0 / 23.0 + 0.001, SmartCutCodec.CopySeekNudge(mkv), 9);
        Assert.Equal(0.001, SmartCutCodec.CopySeekNudge(Bilgi("hevc", gecikme: 2)), 9);
        Assert.Equal(0.001, SmartCutCodec.CopySeekNudge(Bilgi("vp9", kap: "matroska,webm", gecikme: 0)), 9);
        Assert.Equal(3.0 / 23.0 + 0.001, SmartCutCodec.CopySeekNudge(Bilgi("h264", kap: "mpegts", gecikme: 1)), 9);
        Assert.Equal(3.0 / 23.0 + 0.001, SmartCutCodec.CopySeekNudge(Bilgi("h264", kap: null, gecikme: 1)), 9);

        var govde = Plan(mkv, "c.mkv").Steps[1].Args;
        Assert.InRange(double.Parse(Deger(govde, "-ss"), CultureInfo.InvariantCulture), beklenen - 0.00001, beklenen + 0.00001);
        Assert.Equal("4", Deger(govde, "-t"));
    }

    [Fact]
    public void AramaPayiSonrakiAnahtarKareyiAsiyorsaKlipTumuyleKodlanir()
    {
        var kareler = new[] { 0.0, 0.1, 2 };
        var sinirlar = new[] { new SmartCutPoint(0, 0), new SmartCutPoint(0.1, 3), new SmartCutPoint(2, 60) };
        var cizelge = new EditTimeline(new[] { new EditClip(S(0), S(3)) });

        var mkv = EditExport.Build(cizelge, Bilgi("hevc", kap: "matroska,webm", gecikme: 2), kareler, 0, ExportMode.Smart, "c.mkv", "is", 8 * Gb, sinirlar);
        Assert.Equal(ExportMode.Smart, mkv.Effective);
        Assert.Equal(3, mkv.Steps.Count);
        Assert.Equal("libx265", Deger(mkv.Steps[0].Args, "-c:v"));
        Assert.Equal("3", Deger(mkv.Steps[0].Args, "-t"));
        Assert.DoesNotContain(mkv.Steps.Take(1), s => s.Args.Contains("-frames:v"));

        var mp4 = EditExport.Build(cizelge, Bilgi("hevc", gecikme: 2), kareler, 0, ExportMode.Smart, "c.mp4", "is", 8 * Gb, sinirlar);
        Assert.Equal("60", Deger(mp4.Steps[0].Args, "-frames:v"));
        Assert.Equal("0.001", Deger(mp4.Steps[0].Args, "-ss"));
    }

    [Fact]
    public void TemizSiniriOlmayanKlipTumuyleKaynaginKodegiyleKodlanir()
    {
        var cizelge = new EditTimeline(new[] { new EditClip(S(2.5), S(3.5)), new EditClip(S(1), S(7)) });
        var plan = EditExport.Build(cizelge, Bilgi("hevc"), Kareler, 0, ExportMode.Smart, "c.mp4", "is", 8 * Gb,
            new[] { new SmartCutPoint(0, 0), new SmartCutPoint(4, 120) });

        Assert.Equal(ExportMode.Smart, plan.Effective);
        Assert.Equal(5, plan.Steps.Count);
        Assert.Equal(new[] { "libx265", "copy", "libx265", "copy" },
            plan.Steps.Take(4).Select(s => s.Args.Contains("-c:v") ? Deger(s.Args, "-c:v") : Deger(s.Args, "-c")));
        Assert.DoesNotContain(plan.Steps, s => s.Args.Contains("-frames:v"));
        Assert.Equal("6", Deger(plan.Steps[2].Args, "-t"));
        Assert.Equal(7, plan.Steps[^1].DurationSeconds, 6);
    }

    [Fact]
    public void AcikGopAnahtarKaresiSinirOlamaz()
    {
        var kapali = SmartCutCodec.ParsePackets("0.000000,K__\n0.083333,___\n0.041667,___\n1.000000,K__\n1.083333,___\n1.041667,___\n0.000000\n");
        Assert.Equal(6, kapali.Count);
        Assert.Equal(new[] { new SmartCutPoint(0, 0), new SmartCutPoint(1, 3) }, SmartCutCodec.CleanCuts(kapali, 0));

        var acik = SmartCutCodec.ParsePackets("0.000000,K__\n0.083333,___\n0.041667,___\n1.000000,K__\n0.958333,___\n0.916667,___\n2.000000,K__\n2.041667,___\n");
        Assert.Equal(new[] { new SmartCutPoint(0, 0), new SmartCutPoint(2, 6) }, SmartCutCodec.CleanCuts(acik, 0));

        var kaymis = SmartCutCodec.CleanCuts(SmartCutCodec.ParsePackets("1.5,K__\r\n1.541667,___\r\n2.5,K_\r\n"), 1.5);
        Assert.Equal(new[] { new SmartCutPoint(0, 0), new SmartCutPoint(1, 2) }, kaymis);

        Assert.Empty(SmartCutCodec.CleanCuts(SmartCutCodec.ParsePackets("0.0,K__\nN/A,___\n1.0,K__\n"), 0));
        Assert.Empty(SmartCutCodec.CleanCuts(Array.Empty<SmartCutPacket>(), 0));
    }

    private static string Klasor(string ad)
        => Path.Combine(GirdiKanit.Root, ".calisma", "worktree-agent-a037bbec50fedd114", "akilli-kodek", ad);

    private static void Sil(string klasor)
    {
        for (var deneme = 0; deneme < 20; deneme++)
        {
            try
            {
                if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
                var ust = Path.GetDirectoryName(klasor)!;
                if (Directory.Exists(ust) && !Directory.EnumerateFileSystemEntries(ust).Any()) Directory.Delete(ust);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                System.Threading.Thread.Sleep(100);
            }
        }
    }

    private static (int Kod, string Cikti, string Hata) Kos(string arac, string klasor, params string[] args)
    {
        var psi = new ProcessStartInfo(arac)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = klasor
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var surec = Process.Start(psi)!;
        var cikti = surec.StandardOutput.ReadToEndAsync();
        var hata = surec.StandardError.ReadToEndAsync();
        if (!surec.WaitForExit(120000))
        {
            surec.Kill(true);
            Assert.Fail($"{arac} 120 sn icinde bitmedi.");
        }

        return (surec.ExitCode, cikti.GetAwaiter().GetResult(), hata.GetAwaiter().GetResult());
    }

    private static string Kaynak(string klasor, string uzanti, params string[] kodlayici)
        => SesliKaynak(klasor, uzanti, "aac", kodlayici);

    private static string SesliKaynak(string klasor, string uzanti, string ses, string[] kodlayici)
    {
        var kaynak = Path.Combine(klasor, "kaynak." + uzanti);
        var args = new List<string>
        {
            "-hide_banner", "-nostdin", "-y", "-v", "error",
            "-f", "lavfi", "-i", "testsrc2=s=320x240:r=24:d=6", "-f", "lavfi", "-i", "sine=f=440:d=6", "-threads", "2"
        };
        args.AddRange(kodlayici);
        args.AddRange(new[] { "-g", "24", "-c:a", ses, "-shortest", kaynak });
        var (kod, _, hata) = Kos("ffmpeg", klasor, args.ToArray());
        Assert.True(kod == 0 && File.Exists(kaynak), hata);
        return kaynak;
    }

    private static Dictionary<string, string> Oku(string klasor, string dosya, bool say, out string hata)
    {
        var args = new List<string> { "-v", "error", "-select_streams", "v:0" };
        if (say) args.AddRange(new[] { "-count_packets", "-count_frames" });
        args.AddRange(new[]
        {
            "-show_entries", "stream=codec_name,profile,pix_fmt,codec_tag_string,nb_read_packets,nb_read_frames:format=duration",
            "-of", "default=noprint_wrappers=1", dosya
        });
        var (kod, cikti, err) = Kos("ffprobe", klasor, args.ToArray());
        Assert.True(kod == 0, err);
        hata = err;
        return cikti.Split('\n').Select(s => s.Trim().Split('=', 2)).Where(p => p.Length == 2).ToDictionary(p => p[0], p => p[1]);
    }

    private static ExportPlan IkiIzlekli(ExportPlan plan)
        => plan with
        {
            Steps = plan.Steps.Select(s => s with { Args = s.Args.Take(s.Args.Count - 1).Concat(new[] { "-threads", "2", s.Args[^1] }).ToArray() }).ToArray()
        };

    private static (int Birebir, double EnDusuk, int Satir) Psnr(string klasor, string cikti, string kaynak, int ilk, int adet)
    {
        var son = (ilk + adet - 1).ToString(CultureInfo.InvariantCulture);
        var cizge = $"[0:v]select=lt(n\\,{adet}),settb=AVTB,setpts=N/24/TB[a];" +
                    $"[1:v]select=between(n\\,{ilk}\\,{son}),settb=AVTB,setpts=N/24/TB[b];[a][b]psnr=stats_file=psnr.log";
        var (kod, _, hata) = Kos("ffmpeg", klasor, "-hide_banner", "-nostdin", "-v", "error", "-threads", "2",
            "-i", cikti, "-i", kaynak, "-lavfi", cizge, "-an", "-f", "null", "-");
        Assert.True(kod == 0, hata);
        var satirlar = File.ReadAllLines(Path.Combine(klasor, "psnr.log")).Where(s => s.Contains("psnr_avg:", StringComparison.Ordinal)).ToArray();
        var degerler = satirlar.Select(s => s.Split("psnr_avg:")[1].Split(' ')[0]).ToArray();
        var sonlu = degerler.Where(d => d != "inf").Select(d => double.Parse(d, CultureInfo.InvariantCulture)).DefaultIfEmpty(double.PositiveInfinity);
        return (degerler.Count(d => d == "inf"), sonlu.Min(), satirlar.Length);
    }

    private sealed record Kol(
        string Ad, string Uzanti, string[] Kodlayici, string CiktiUzanti, bool Govde = true, string? Etiket = null, string Ses = "aac");

    private static (string Kodek, int Paket, double Sure) SesOku(string klasor, string dosya)
    {
        var (kod, cikti, hata) = Kos("ffprobe", klasor, "-v", "error", "-select_streams", "a:0", "-count_packets",
            "-show_entries", "stream=codec_name,nb_read_packets,duration:format=duration", "-of", "default=noprint_wrappers=1", dosya);
        Assert.True(kod == 0, hata);
        var alan = cikti.Split('\n').Select(s => s.Trim().Split('=', 2)).Where(p => p.Length == 2 && p[1] != "N/A")
            .GroupBy(p => p[0]).ToDictionary(g => g.Key, g => g.First()[1]);
        return (alan["codec_name"], int.Parse(alan["nb_read_packets"], CultureInfo.InvariantCulture),
            double.Parse(alan["duration"], CultureInfo.InvariantCulture));
    }

    private async Task Olc(Kol kol)
    {
        var klasor = Klasor(kol.Ad);
        Sil(klasor);
        Directory.CreateDirectory(klasor);
        try
        {
            var kaynak = SesliKaynak(klasor, kol.Uzanti, kol.Ses, kol.Kodlayici);
            var cikti = Path.Combine(klasor, "cikti." + kol.CiktiUzanti);
            var cizelge = new EditTimeline(new[] { new EditClip(S(0.5), S(4.5)) });

            var plan = IkiIzlekli(await EditExportRunner.PrepareAsync(kaynak, cizelge, ExportMode.Smart, cikti, 8 * Gb));
            Assert.Equal(ExportMode.Smart, plan.Effective);
            var kopya = plan.Steps.Where(s => s.Args.Contains("-frames:v")).ToArray();
            if (kol.Govde) Assert.Equal("72", Deger(Assert.Single(kopya).Args, "-frames:v"));
            else Assert.Empty(kopya);

            await EditExportRunner.RunAsync(plan, null);
            Assert.False(Directory.Exists(plan.WorkDirectory));

            var kaynakBilgi = Oku(klasor, kaynak, false, out _);
            var olcu = Oku(klasor, cikti, true, out var probeHata);
            var sure = double.Parse(olcu["duration"], CultureInfo.InvariantCulture);
            var (cozKod, _, cozHata) = Kos("ffmpeg", klasor, "-hide_banner", "-nostdin", "-v", "error", "-threads", "2", "-i", cikti, "-f", "null", "-");
            var (birebir, enDusuk, satir) = Psnr(klasor, cikti, kaynak, 12, 96);

            _cikti.WriteLine($"akilli kesme kolu kostu: {kol.Ad} kodek={olcu["codec_name"]} profil={olcu["profile"]} piksel={olcu["pix_fmt"]} " +
                             $"etiket={olcu["codec_tag_string"]} sure={olcu["duration"]} paket={olcu["nb_read_packets"]} kare={olcu["nb_read_frames"]} " +
                             $"birebir={birebir} enDusukPsnr={enDusuk.ToString("0.00", CultureInfo.InvariantCulture)} adim={plan.Steps.Count}");

            Assert.Equal(string.Empty, probeHata.Trim());
            Assert.InRange(sure, 4.0 - Kare, 4.0 + Kare);

            Assert.Equal(0, cozKod);
            Assert.Equal(string.Empty, cozHata.Trim());

            Assert.Equal("96", olcu["nb_read_packets"]);
            Assert.Equal("96", olcu["nb_read_frames"]);
            Assert.Equal(96, satir);

            Assert.Equal(kaynakBilgi["codec_name"], olcu["codec_name"]);
            Assert.Equal(kaynakBilgi["profile"], olcu["profile"]);
            Assert.Equal(kaynakBilgi["pix_fmt"], olcu["pix_fmt"]);
            if (kol.Etiket is not null) Assert.Equal(kol.Etiket, olcu["codec_tag_string"]);
            Assert.Equal(kol.Govde ? 72 : 0, birebir);
            Assert.True(enDusuk > 30, $"en dusuk PSNR {enDusuk}");

            if (kol.Ses == "aac") return;
            var kaynakSes = SesOku(klasor, kaynak);
            var ses = SesOku(klasor, cikti);
            _cikti.WriteLine($"akilli kesme kolu sesi: {kol.Ad} kaynak={kaynakSes.Kodek} cikti={ses.Kodek} paket={ses.Paket} sure={ses.Sure.ToString("0.000", CultureInfo.InvariantCulture)}");
            Assert.False(plan.AudioForcedFull);
            Assert.DoesNotContain(plan.Steps, s => s.Args.Contains("-c:a"));
            Assert.Equal("opus", kaynakSes.Kodek);
            Assert.Equal(kaynakSes.Kodek, ses.Kodek);
            Assert.InRange(ses.Paket, 199, 201);
            Assert.InRange(ses.Sure, 4.0 - Kare, 4.0 + Kare);
        }
        finally
        {
            Sil(klasor);
        }
    }

    private const string X265 = "log-level=error:pools=2:open-gop=0";

    [AkilliKodekFact("libx264")]
    public Task CanliH264BKareliKaynaktaDortOlcutuGecer()
        => Olc(new Kol("h264", "mp4", new[] { "-c:v", "libx264", "-pix_fmt", "yuv420p" }, "mp4"));

    [AkilliKodekFact("libx264")]
    public Task CanliH264OnBitDortOlcutuGecer()
        => Olc(new Kol("h264-10bit", "mp4", new[] { "-c:v", "libx264", "-pix_fmt", "yuv420p10le" }, "mp4"));

    [AkilliKodekFact("libx265")]
    public Task CanliHevcHvc1EtiketiniKoruyarakDortOlcutuGecer()
        => Olc(new Kol("hevc-hvc1", "mp4", new[] { "-c:v", "libx265", "-x265-params", X265, "-pix_fmt", "yuv420p", "-tag:v", "hvc1" }, "mp4", Etiket: "hvc1"));

    [AkilliKodekFact("libx265")]
    public Task CanliHevcMatroskadaBuyukAramaPayiylaDortOlcutuGecer()
        => Olc(new Kol("hevc-mkv", "mkv", new[] { "-c:v", "libx265", "-x265-params", X265, "-pix_fmt", "yuv420p" }, "mkv"));

    [AkilliKodekFact("libx265")]
    public Task CanliHevcOnBitDortOlcutuGecer()
        => Olc(new Kol("hevc-10bit", "mp4", new[] { "-c:v", "libx265", "-x265-params", X265, "-pix_fmt", "yuv420p10le" }, "mp4", Etiket: "hev1"));

    [AkilliKodekFact("libx265")]
    public Task CanliHevcAcikGopKaynaktaGovdeKopyalanmazCiktiSaglamKalir()
        => Olc(new Kol("hevc-acik", "mp4", new[] { "-c:v", "libx265", "-x265-params", "log-level=error:pools=2:open-gop=1", "-pix_fmt", "yuv420p" }, "mp4", Govde: false));

    [AkilliKodekFact("libsvtav1")]
    public Task CanliAv1DortOlcutuGecer()
        => Olc(new Kol("av1", "mp4", new[] { "-c:v", "libsvtav1", "-preset", "12", "-svtav1-params", "lp=2", "-pix_fmt", "yuv420p" }, "mp4"));

    [AkilliKodekFact("libvpx-vp9")]
    public Task CanliVp9DortOlcutuGecer()
        => Olc(new Kol("vp9", "mkv", new[] { "-c:v", "libvpx-vp9", "-deadline", "good", "-cpu-used", "8", "-row-mt", "1", "-pix_fmt", "yuv420p" }, "mkv"));

    private static readonly string[] Vp9 = { "-c:v", "libvpx-vp9", "-deadline", "good", "-cpu-used", "8", "-row-mt", "1", "-pix_fmt", "yuv420p" };

    [AkilliKodekFact("libvpx-vp9", "libopus")]
    public Task CanliVp9OpusWebmKaynaktaSesKopyalanirDortOlcutuGecer()
        => Olc(new Kol("vp9-opus", "webm", Vp9, "mkv", Ses: "libopus"));

    [AkilliKodekFact("libvpx-vp9", "libopus")]
    public Task CanliVp9OpusMp4CiktidaSesKopyalanirDortOlcutuGecer()
        => Olc(new Kol("vp9-opus-mp4", "webm", Vp9, "mp4", Ses: "libopus"));

    [AkilliKodekFact("libx264")]
    public async Task CanliHizDegisenKlipAkilliKipteKalirSureVeKareSayisiTutar()
    {
        var klasor = Klasor("h264-hiz");
        Sil(klasor);
        Directory.CreateDirectory(klasor);
        try
        {
            var kaynak = Kaynak(klasor, "mp4", "-c:v", "libx264", "-pix_fmt", "yuv420p");
            var cikti = Path.Combine(klasor, "cikti.mp4");
            var cizelge = new EditTimeline(new[] { new EditClip(S(0.5), S(2.5)), new EditClip(S(3), S(4), 2m) });

            var plan = IkiIzlekli(await EditExportRunner.PrepareAsync(kaynak, cizelge, ExportMode.Smart, cikti, 8 * Gb));
            Assert.Equal(ExportMode.Smart, plan.Effective);
            Assert.Equal("24", Deger(Assert.Single(plan.Steps, s => s.Args.Contains("-frames:v")).Args, "-frames:v"));
            await EditExportRunner.RunAsync(plan, null);

            var olcu = Oku(klasor, cikti, true, out var probeHata);
            var (cozKod, _, cozHata) = Kos("ffmpeg", klasor, "-hide_banner", "-nostdin", "-v", "error", "-threads", "2", "-i", cikti, "-f", "null", "-");
            var (birebir, enDusuk, _) = Psnr(klasor, cikti, kaynak, 12, 48);
            _cikti.WriteLine($"akilli kesme kolu kostu: h264-hiz sure={olcu["duration"]} paket={olcu["nb_read_packets"]} kare={olcu["nb_read_frames"]} birebir={birebir}");

            Assert.Equal(string.Empty, probeHata.Trim());
            Assert.InRange(double.Parse(olcu["duration"], CultureInfo.InvariantCulture), 2.5 - Kare, 2.5 + Kare);
            Assert.Equal(0, cozKod);
            Assert.Equal(string.Empty, cozHata.Trim());
            Assert.Equal("60", olcu["nb_read_packets"]);
            Assert.Equal("60", olcu["nb_read_frames"]);
            Assert.Equal(24, birebir);
            Assert.True(enDusuk > 30, $"en dusuk PSNR {enDusuk}");
        }
        finally
        {
            Sil(klasor);
        }
    }

    [AkilliKodekFact("mpeg4")]
    public async Task CanliMpeg4KaynakTamKipeDuser()
    {
        var klasor = Klasor("mpeg4");
        Sil(klasor);
        Directory.CreateDirectory(klasor);
        try
        {
            var kaynak = Kaynak(klasor, "mp4", "-c:v", "mpeg4", "-q:v", "4", "-pix_fmt", "yuv420p");
            var cizelge = new EditTimeline(new[] { new EditClip(S(0.5), S(4.5)) });
            var plan = await EditExportRunner.PrepareAsync(kaynak, cizelge, ExportMode.Smart, Path.Combine(klasor, "cikti.mp4"), 8 * Gb);
            _cikti.WriteLine($"akilli kesme kolu kostu: mpeg4 etkin={plan.Effective}");

            Assert.Equal(ExportMode.Full, plan.Effective);
            Assert.True(plan.FellBackToFull);
        }
        finally
        {
            Sil(klasor);
        }
    }
}
