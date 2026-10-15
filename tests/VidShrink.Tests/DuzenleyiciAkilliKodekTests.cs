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
        string? kap = "mov,mp4,m4a,3gp,3g2,mj2", int gecikme = 0, string? ses = "aac", bool taramali = false, string? alan = null,
        long bitHizi = 1, long sesBitHizi = 0) => new()
    {
        FilePath = "kaynak.mp4",
        FileSizeBytes = 1,
        DurationSeconds = 60,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = kodek,
        TotalBitrateBps = bitHizi,
        AudioCodec = ses,
        AudioBitrateBps = sesBitHizi,
        PixelFormat = piksel,
        BitDepth = bit,
        VideoProfile = profil,
        VideoCodecTag = etiket,
        FormatName = kap,
        VideoDelayFrames = gecikme,
        IsInterlaced = taramali,
        FieldOrder = alan
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
    [InlineData("mpeg2video", "mpeg2video")]
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
    [InlineData("mpeg1video")]
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
    public void KapiTamBesKodekTasir()
        => Assert.Equal(new[] { "av1", "h264", "hevc", "mpeg2video", "vp9" }, SmartCutCodec.Codecs.OrderBy(k => k, StringComparer.Ordinal));

    [Fact]
    public void Mpeg2KenarKaynaginBitHiziVeProfiliyleKodlanir()
    {
        var ana = SmartCutCodec.Resolve(Bilgi("mpeg2video", "Main", bitHizi: 8_000_000, sesBitHizi: 192_000))!;
        Assert.Equal("mpeg2video", ana.Encoder);
        Assert.Equal("mpeg2video", Deger(ana.VideoArgs, "-c:v"));
        Assert.Equal("7808000", Deger(ana.VideoArgs, "-b:v"));
        Assert.Equal("4", Deger(ana.VideoArgs, "-profile:v"));
        Assert.Equal("yuv420p", Deger(ana.VideoArgs, "-pix_fmt"));
        Assert.DoesNotContain("-flags", ana.VideoArgs);
        Assert.Equal(SmartCutCodec.TransportStream, ana.Container);

        var basit = SmartCutCodec.Resolve(Bilgi("mpeg2video", "Simple", bitHizi: 4_000_000))!;
        Assert.Equal("5", Deger(basit.VideoArgs, "-profile:v"));
        Assert.Equal("0", Deger(basit.VideoArgs, "-bf"));
        Assert.Equal("4000000", Deger(basit.VideoArgs, "-b:v"));

        var okunamayan = SmartCutCodec.Resolve(Bilgi("mpeg2video", bitHizi: 100_000, sesBitHizi: 192_000))!;
        Assert.DoesNotContain("-b:v", okunamayan.VideoArgs);
        Assert.DoesNotContain("-profile:v", okunamayan.VideoArgs);
        Assert.Equal("2", Deger(okunamayan.VideoArgs, "-q:v"));

        Assert.Null(SmartCutCodec.Resolve(Bilgi("mpeg2video", "High", bitHizi: 8_000_000)));
        Assert.Null(SmartCutCodec.Resolve(Bilgi("mpeg2video", "4:2:2", "yuv422p", bitHizi: 8_000_000)));
        Assert.Null(SmartCutCodec.Resolve(Bilgi("mpeg2video", "Main", "yuv420p10le", 10, bitHizi: 8_000_000)));
        Assert.Null(SmartCutCodec.Resolve(Bilgi("mpeg2video", "Main", bitHizi: 8_000_000), ad => ad != "mpeg2video"));
    }

    [Theory]
    [InlineData("tt")]
    [InlineData("bb")]
    [InlineData("tb")]
    [InlineData("bt")]
    public void TaramaliMpeg2AlanlariAyriKodlanir(string alan)
    {
        var esleme = SmartCutCodec.Resolve(Bilgi("mpeg2video", "Main", taramali: true, alan: alan, bitHizi: 8_000_000))!;

        Assert.Equal("+ildct+ilme", Deger(esleme.VideoArgs, "-flags"));
        Assert.Equal(ExportMode.Smart, Plan(Bilgi("mpeg2video", "Main", taramali: true, alan: alan, bitHizi: 8_000_000)).Effective);
        Assert.DoesNotContain("-flags", SmartCutCodec.Resolve(Bilgi("mpeg2video", "Main", bitHizi: 8_000_000))!.VideoArgs);
    }

    [Theory]
    [InlineData("h264", "tt")]
    [InlineData("hevc", "bb")]
    [InlineData("av1", "tb")]
    [InlineData("vp9", "bt")]
    public void Mpeg2DisindakiTaramaliKaynakTamKipeDuser(string kodek, string alan)
    {
        Assert.Null(SmartCutCodec.Resolve(Bilgi(kodek, taramali: true, alan: alan, bitHizi: 8_000_000)));
        Assert.Equal(ExportMode.Full, Plan(Bilgi(kodek, taramali: true, alan: alan, bitHizi: 8_000_000)).Effective);
    }

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
        foreach (var ses in new[] { "vorbis", "flac", "dts", "truehd", "pcm_s16le", "mp2" })
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

    [Theory]
    [InlineData("ac3", "c.mkv")]
    [InlineData("ac3", "c.mp4")]
    [InlineData("ac3", "c.mov")]
    [InlineData("AC3", "c.m4v")]
    [InlineData("eac3", "c.mkv")]
    [InlineData("eac3", "c.mp4")]
    [InlineData("eac3", "c.MOV")]
    [InlineData("mp3", "c.mkv")]
    [InlineData("mp3", "c.mp4")]
    [InlineData("mp3", "c.mov")]
    public void Ac3Eac3VeMp3SesDuzKesimdeAynenKopyalanirAkilliKipKalir(string ses, string cikti)
    {
        var plan = Plan(Bilgi("h264", ses: ses), cikti);

        Assert.Equal(ExportMode.Smart, plan.Effective);
        Assert.False(plan.AudioForcedFull);
        Assert.False(plan.FellBackToFull);
        var adim = Assert.Single(plan.Steps, s => s.Args.Contains("0:a:0")).Args;
        Assert.Equal("copy", Deger(adim, "-c"));
        Assert.Equal("mpegts", Deger(adim, "-f"));
        Assert.EndsWith(".ts", adim[^1], StringComparison.Ordinal);
        Assert.DoesNotContain(plan.Steps, s => s.Args.Contains("-c:a"));
    }

    [Theory]
    [InlineData("eac3", "c.m4v")]
    [InlineData("mp3", "c.m4v")]
    [InlineData("ac3", "c.webm")]
    [InlineData("eac3", "c.webm")]
    [InlineData("mp3", "c.webm")]
    [InlineData("ac3", "c.avi")]
    public void Ac3Eac3VeMp3SesTasimayanKaptaTamKipeDuserNedeniSestir(string ses, string cikti)
    {
        var plan = Plan(Bilgi("h264", ses: ses), cikti);

        Assert.Equal(ExportMode.Full, plan.Effective);
        Assert.True(plan.AudioForcedFull);
    }

    [Fact]
    public void KopyalananSesHizYaDaTersKliptaTamKipeDuserAacKalir()
    {
        var hizli = new EditTimeline(new[] { new EditClip(S(1), S(3)), new EditClip(S(4), S(6), 2m) });
        var ters = new EditTimeline(new[] { new EditClip(S(1), S(3)), new EditClip(S(4), S(6), 1m, true) });

        foreach (var cizelge in new[] { hizli, ters })
        {
            foreach (var ses in new[] { "opus", "ac3", "eac3", "mp3" })
            {
                var dusen = Plan(Bilgi("vp9", ses: ses), "c.mkv", cizelge: cizelge);
                Assert.Equal(ExportMode.Full, dusen.Effective);
                Assert.True(dusen.AudioForcedFull, ses);
                Assert.Equal(ExportMode.Smart, Plan(Bilgi("vp9", ses: ses), "c.mkv").Effective);
            }

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
            new EditTimeline(new[] { new EditClip(S(1), S(3)), new EditClip(S(4), S(6)) }), Bilgi("vp9", ses: "vorbis"), Kareler, 0,
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
        Assert.Equal(0.001, SmartCutCodec.CopySeekNudge(Bilgi("h264", kap: "mpegts", gecikme: 1)), 9);
        Assert.Equal(0.001, SmartCutCodec.CopySeekNudge(Bilgi("mpeg2video", kap: "mpegts", gecikme: 2)), 9);
        Assert.Equal(3.0 / 23.0 + 0.001, SmartCutCodec.CopySeekNudge(Bilgi("h264", kap: null, gecikme: 1)), 9);

        var govde = Plan(mkv, "c.mkv").Steps[1].Args;
        Assert.InRange(double.Parse(Deger(govde, "-ss"), CultureInfo.InvariantCulture), beklenen - 0.00001, beklenen + 0.00001);
        Assert.Equal("4", Deger(govde, "-t"));
    }

    [Fact]
    public void BasKenarAnahtarKaredenOnceBiterListeSuresiSinirdaKalir()
    {
        var plan = Plan(Bilgi());

        var bas = double.Parse(Deger(plan.Steps[0].Args, "-t"), CultureInfo.InvariantCulture);
        Assert.Equal(1 - 0.5 / 30, bas, 5);
        Assert.Contains("\nduration 1\n", plan.Steps[^1].ListContent, StringComparison.Ordinal);
        Assert.Equal("1", Deger(plan.Steps[2].Args, "-t"));
    }

    [Fact]
    public void KareArasindanBaslayanBasKenarListedeTamKareSuresiAlir()
    {
        var plan = Plan(Bilgi(), cizelge: new EditTimeline(new[] { new EditClip(S(1.01), S(7)) }));

        Assert.Contains("\nduration 0.966667\n", plan.Steps[^1].ListContent, StringComparison.Ordinal);
        Assert.DoesNotContain("\nduration 0.99\n", plan.Steps[^1].ListContent, StringComparison.Ordinal);
    }

    [Fact]
    public void KareArasindaBitenKuyrukKenarListedeKodlananKareSayisiKadarSurer()
    {
        var plan = Plan(Bilgi(), cizelge: new EditTimeline(new[] { new EditClip(S(1.01), S(6.99)) }));
        var liste = plan.Steps[^1].ListContent;

        Assert.Contains("\nduration 0.966667\nfile", liste, StringComparison.Ordinal);
        Assert.Contains("\nduration 4\nfile", liste, StringComparison.Ordinal);
        Assert.EndsWith("\nduration 1\n", liste, StringComparison.Ordinal);
        Assert.DoesNotContain("\nduration 0.99\n", liste, StringComparison.Ordinal);
    }

    [Fact]
    public void TsKaynaktaKenarOncekiAnahtarKaredenAranirCiktidaKesilir()
    {
        static (string? Once, string? Sonra) Arama(IReadOnlyList<string> args)
        {
            var girdi = args.ToList().IndexOf("-i");
            string? Bul(int bas, int son)
            {
                for (var i = bas; i < son - 1; i++) if (args[i] == "-ss") return args[i + 1];
                return null;
            }
            return (Bul(0, girdi), Bul(girdi, args.Count));
        }

        var ts = Plan(Bilgi("mpeg2video", kap: "mpegts", gecikme: 2), "c.mkv");
        Assert.Equal((null, null), Arama(ts.Steps[0].Args));
        Assert.StartsWith("setpts=PTS-STARTPTS,trim=start=1:duration=", Deger(ts.Steps[0].Args, "-vf"));
        Assert.Equal(("5.999", null), Arama(ts.Steps[2].Args));
        Assert.StartsWith("setpts=PTS-STARTPTS,trim=start=0:duration=", Deger(ts.Steps[2].Args, "-vf"));

        var mp4 = Plan(Bilgi("mpeg2video", gecikme: 2), "c.mkv");
        Assert.Equal(("1", null), Arama(mp4.Steps[0].Args));
        Assert.Equal(("6", null), Arama(mp4.Steps[2].Args));
        Assert.DoesNotContain("trim=", Deger(mp4.Steps[0].Args, "-vf"));
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
            "-show_entries", "stream=codec_name,profile,pix_fmt,codec_tag_string,field_order,r_frame_rate,nb_read_packets,nb_read_frames:format=duration",
            "-of", "default=noprint_wrappers=1", dosya
        });
        var (kod, cikti, err) = Kos("ffprobe", klasor, args.ToArray());
        Assert.True(kod == 0, err);
        hata = err;
        return cikti.Split('\n').Select(s => s.Trim().Split('=', 2)).Where(p => p.Length == 2).GroupBy(p => p[0]).ToDictionary(g => g.Key, g => g.First()[1]);
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

    private static IReadOnlyList<string> Alanlar(string klasor, string dosya)
    {
        var (kod, cikti, hata) = Kos("ffprobe", klasor, "-v", "error", "-select_streams", "v:0", "-show_entries",
            "frame=interlaced_frame,top_field_first", "-of", "csv=p=0", dosya);
        Assert.True(kod == 0, hata);
        return cikti.Split('\n').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
    }

    private sealed record Kol(
        string Ad, string Uzanti, string[] Kodlayici, string CiktiUzanti, bool Govde = true, string? Etiket = null, string Ses = "aac",
        string SesKodek = "opus", int PaketAlt = 199, int PaketUst = 201, string? Alan = null, int GovdeKare = 72);

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

            var plan = IkiIzlekli(await EditExportRunner.PrepareAsync(new[] { kaynak }, cizelge, ExportMode.Smart, cikti, 8 * Gb));
            Assert.Equal(ExportMode.Smart, plan.Effective);
            var kopya = plan.Steps.Where(s => s.Args.Contains("-frames:v")).ToArray();
            if (kol.Govde) Assert.Equal(kol.GovdeKare.ToString(CultureInfo.InvariantCulture), Deger(Assert.Single(kopya).Args, "-frames:v"));
            else Assert.Empty(kopya);

            await EditExportRunner.RunAsync(plan, null);
            Assert.False(Directory.Exists(plan.WorkDirectory));

            var kaynakBilgi = Oku(klasor, kaynak, false, out _);
            var olcu = Oku(klasor, cikti, true, out var probeHata);
            var sure = double.Parse(olcu["duration"], CultureInfo.InvariantCulture);
            var (cozKod, _, cozHata) = Kos("ffmpeg", klasor, "-hide_banner", "-nostdin", "-v", "error", "-threads", "2", "-i", cikti, "-f", "null", "-");
            var (birebir, enDusuk, satir) = Psnr(klasor, cikti, kaynak, 12, 96);

            _cikti.WriteLine($"akilli kesme kolu kostu: {kol.Ad} kodek={olcu["codec_name"]} profil={olcu["profile"]} piksel={olcu["pix_fmt"]} alan={olcu.GetValueOrDefault("field_order")} " +
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
            Assert.Equal(kaynakBilgi["r_frame_rate"], olcu["r_frame_rate"]);
            if (kol.Alan is not null)
            {
                Assert.Equal(kol.Alan, kaynakBilgi["field_order"]);
                Assert.Contains(olcu["field_order"], new[] { "tt", "bb", "tb", "bt" });
                var kaynakAlan = Alanlar(klasor, kaynak);
                var ciktiAlan = Alanlar(klasor, cikti);
                _cikti.WriteLine($"akilli kesme kolu alanlari: {kol.Ad} kaynak={string.Join("", kaynakAlan.Distinct())} cikti={string.Join("", ciktiAlan.Distinct())} kare={ciktiAlan.Count}");
                Assert.Equal(96, ciktiAlan.Count);
                Assert.Equal(kaynakAlan.Skip(12).Take(96), ciktiAlan);
                Assert.Single(ciktiAlan.Distinct());
            }
            if (kol.Etiket is not null) Assert.Equal(kol.Etiket, olcu["codec_tag_string"]);
            Assert.Equal(kol.Govde ? kol.GovdeKare : 0, birebir);
            Assert.True(enDusuk > 30, $"en dusuk PSNR {enDusuk}");

            if (kol.Ses == "aac") return;
            var kaynakSes = SesOku(klasor, kaynak);
            var ses = SesOku(klasor, cikti);
            _cikti.WriteLine($"akilli kesme kolu sesi: {kol.Ad} kaynak={kaynakSes.Kodek} cikti={ses.Kodek} paket={ses.Paket} sure={ses.Sure.ToString("0.000", CultureInfo.InvariantCulture)}");
            Assert.False(plan.AudioForcedFull);
            Assert.DoesNotContain(plan.Steps, s => s.Args.Contains("-c:a"));
            Assert.Equal(kol.SesKodek, kaynakSes.Kodek);
            Assert.Equal(kaynakSes.Kodek, ses.Kodek);
            Assert.InRange(ses.Paket, kol.PaketAlt, kol.PaketUst);
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

    private static readonly string[] X264 = { "-c:v", "libx264", "-pix_fmt", "yuv420p" };

    [AkilliKodekFact("libx264", "ac3")]
    public Task CanliH264Ac3MkvKaynaktaSesKopyalanirDortOlcutuGecer()
        => Olc(new Kol("h264-ac3", "mkv", X264, "mkv", Ses: "ac3", SesKodek: "ac3", PaketAlt: 114, PaketUst: 116));

    [AkilliKodekFact("libx264", "eac3")]
    public Task CanliH264Eac3Mp4CiktidaSesKopyalanirDortOlcutuGecer()
        => Olc(new Kol("h264-eac3", "mkv", X264, "mp4", Ses: "eac3", SesKodek: "eac3", PaketAlt: 114, PaketUst: 116));

    [AkilliKodekFact("libx264", "libmp3lame")]
    public Task CanliH264Mp3MovCiktidaSesKopyalanirDortOlcutuGecer()
        => Olc(new Kol("h264-mp3", "mp4", X264, "mov", Ses: "libmp3lame", SesKodek: "mp3", PaketAlt: 152, PaketUst: 155));

    /// <summary>Kapali GOP'lu mpeg2video 24 yerine 22 karede anahtar kare koyuyor: 0,917 ile 3,667 sn arasi 66 kare.</summary>
    private const int Mpeg2Govde = 66;

    private static readonly string[] Mpeg2Kapali = { "-c:v", "mpeg2video", "-b:v", "2M", "-bf", "2", "-flags", "+cgop", "-sc_threshold", "1000000000" };

    [AkilliKodekFact("mpeg2video")]
    public Task CanliMpeg2KapaliGopBKareliKaynaktaDortOlcutuGecer()
        => Olc(new Kol("mpeg2-mp4", "mp4", Mpeg2Kapali, "mp4", GovdeKare: Mpeg2Govde));

    [AkilliKodekFact("mpeg2video", "ac3")]
    public Task CanliMpeg2Ac3TsKaynaktaDortOlcutuGecer()
        => Olc(new Kol("mpeg2-ts-ac3", "ts", Mpeg2Kapali, "mkv", Ses: "ac3", SesKodek: "ac3", PaketAlt: 114, PaketUst: 116, GovdeKare: Mpeg2Govde));

    [AkilliKodekFact("mpeg2video")]
    public Task CanliMpeg2TaramaliKaynakAlanSirasiniKoruyarakDortOlcutuGecer()
        => Olc(new Kol("mpeg2-taramali", "mkv", Mpeg2Kapali.Concat(new[] { "-flags", "+cgop+ildct+ilme", "-vf", "setfield=tff" }).ToArray(), "mkv", Alan: "tb", GovdeKare: Mpeg2Govde));

    [AkilliKodekFact("mpeg2video")]
    public Task CanliMpeg2AltAlanOncelikliTsKaynakAlanSirasiniKorur()
        => Olc(new Kol("mpeg2-taramali-bb", "ts", Mpeg2Kapali.Concat(new[] { "-flags", "+cgop+ildct+ilme", "-vf", "setfield=bff" }).ToArray(), "mp4", Alan: "bb", GovdeKare: Mpeg2Govde));

    [AkilliKodekFact("mpeg2video")]
    public Task CanliMpeg2AcikGopKaynaktaGovdeKopyalanmazCiktiSaglamKalir()
        => Olc(new Kol("mpeg2-acik", "mp4", new[] { "-c:v", "mpeg2video", "-b:v", "2M", "-bf", "2" }, "mp4", Govde: false));

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

            var plan = IkiIzlekli(await EditExportRunner.PrepareAsync(new[] { kaynak }, cizelge, ExportMode.Smart, cikti, 8 * Gb));
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
            var plan = await EditExportRunner.PrepareAsync(new[] { kaynak }, cizelge, ExportMode.Smart, Path.Combine(klasor, "cikti.mp4"), 8 * Gb);
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
