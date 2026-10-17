using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using VidShrink.App.Editing;
using VidShrink.App.Playback;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using VidShrink.Player;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Ileri renk (later 88, DZB-36 ve DZB-38): otomatik renk <c>normalize</c>, ana egri <c>curves</c>, uc bolgeli renk tekerleri
/// <c>colorbalance</c>. Normallestirme, suzgec dizgesi, zincirdeki yeri (otomatik basta, egri ve teker sonda), disa aktarim
/// grafi ve Tam'a dusme, onizlemenin parca penceresi, proje dosyasi, canli ffmpeg karesinde once/sonra olcusu, libmpv'nin
/// tirnakli egri grafini kabul etmesi, arayuz kutulari. Kanit <c>.calisma/yan/egriler-renk/</c>, test siler.
/// </summary>
public sealed class DuzenleyiciIleriRenkTests
{
    private const long Gb = 1_000_000_000;

    private static long S(double saniye) => EditTime.FromSeconds(saniye);

    private static string Kanit
    {
        get
        {
            var yol = Path.Combine(GirdiKanit.Root, ".calisma", "yan", "egriler-renk");
            Directory.CreateDirectory(yol);
            return yol;
        }
    }

    private static MediaInfo Bilgi() => new()
    {
        FilePath = "kaynak.mp4",
        FileSizeBytes = 1,
        DurationSeconds = 60,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 1,
        AudioCodec = "aac",
        PixelFormat = "yuv420p",
        BitDepth = 8
    };

    private static readonly double[] Anahtarlar = { 0, 2, 4, 6, 8 };

    private static string Deger(IReadOnlyList<string> args, string anahtar) => args[args.ToList().IndexOf(anahtar) + 1];

    [Fact]
    public void NormallesirAciSarilirMiktarVeEgriKirpilir()
    {
        Assert.False(ClipEffects.None.HasGrade);
        Assert.True(ClipEffects.None.Normalized().IsNeutral);

        var bozuk = new ClipEffects
        {
            CurveShadows = -1, CurveMids = 0.555, CurveHighlights = 7, ShadowHue = -30, ShadowAmount = 3,
            MidHue = 725, MidAmount = 0.333, HighlightHue = 90, HighlightAmount = -0.5,
        }.Normalized();
        Assert.Equal((0.0, 0.56, 1.0), (bozuk.CurveShadows, bozuk.CurveMids, bozuk.CurveHighlights));
        Assert.Equal((330, 1.0), (bozuk.ShadowHue, bozuk.ShadowAmount));
        Assert.Equal((5, 0.33), (bozuk.MidHue, bozuk.MidAmount));
        Assert.Equal((90, 0.0), (bozuk.HighlightHue, bozuk.HighlightAmount));

        var sonsuz = new ClipEffects { CurveMids = double.NaN, MidAmount = double.PositiveInfinity, MidHue = 40 }.Normalized();
        Assert.True(sonsuz.IsNeutral, sonsuz.ToString());

        foreach (var tek in new[]
                 {
                     ClipEffects.None with { AutoColor = true }, ClipEffects.None with { CurveMids = 0.6 },
                     ClipEffects.None with { HighlightAmount = 0.2 },
                 })
        {
            Assert.True(tek.HasGrade, tek.ToString());
            Assert.True(tek.HasColor, tek.ToString());
            Assert.False(tek.IsNeutral, tek.ToString());
        }

        Assert.False((ClipEffects.None with { ShadowHue = 90 }).HasGrade);
        Assert.True((ClipEffects.None with { ShadowHue = 90 }).IsNeutral);
        Assert.Equal(string.Empty, ClipGrade.Filters(ClipEffects.None with { ShadowHue = 90 }));
    }

    [Fact]
    public void TekerKaymasiUcKanaldaToplamSifir()
    {
        Assert.Equal((0.4, -0.2, -0.2), ClipGrade.Shift(0, 0.4));
        Assert.Equal((-0.15, 0.3, -0.15), ClipGrade.Shift(120, 0.3));
        Assert.Equal((-0.15, -0.15, 0.3), ClipGrade.Shift(240, 0.3));
        for (var aci = 0; aci < 360; aci += 15)
        {
            var (r, g, b) = ClipGrade.Shift(aci, 1);
            Assert.True(Math.Abs(r + g + b) < 0.003, $"{aci}: {r} {g} {b}");
        }
    }

    [Fact]
    public void SuzgecDizgesiVeZincirdekiYeri()
    {
        Assert.Equal(string.Empty, ClipGrade.Filters(ClipEffects.None));
        Assert.Equal(string.Empty, ClipGrade.Auto(ClipEffects.None));
        Assert.Equal("curves=master='0/0 0.25/0.1 0.5/0.6 0.75/0.75 1/1'",
            ClipGrade.Filters(ClipEffects.None with { CurveShadows = 0.1, CurveMids = 0.6 }));
        Assert.Equal("colorbalance=rs=0.4:gs=-0.2:bs=-0.2:rh=-0.15:gh=-0.15:bh=0.3:pl=1",
            ClipGrade.Filters(ClipEffects.None with { ShadowAmount = 0.4, HighlightHue = 240, HighlightAmount = 0.3 }));
        Assert.Equal("colorbalance=rm=-0.15:gm=0.3:bm=-0.15:pl=1", ClipGrade.Filters(ClipEffects.None with { MidHue = 120, MidAmount = 0.3 }));

        var hepsi = new ClipEffects { AutoColor = true, Saturation = 0.5, Temperature = 4000, CurveHighlights = 0.9, MidAmount = 0.2 }.Normalized();
        Assert.Equal("normalize=smoothing=24:independence=1:strength=1,eq=saturation=0.5,colortemperature=temperature=4000,"
                     + "curves=master='0/0 0.25/0.25 0.5/0.5 0.75/0.9 1/1',colorbalance=rm=0.2:gm=-0.1:bm=-0.1:pl=1",
            ClipFilters.Color(hepsi));
        Assert.Equal(ClipGrade.AutoColorFilter, ClipFilters.Color(ClipEffects.None with { AutoColor = true }));
    }

    [Theory]
    [InlineData(ExportMode.Fast)]
    [InlineData(ExportMode.Smart)]
    public void DisaAktarimGrafiIleriRengiTasirVeTamaDuser(ExportMode kip)
    {
        var sinirlar = DuzenleyiciAkilliKodekTests.Sinirlar(Anahtarlar);
        var c = EditTimeline.FromSource(S(10));
        Assert.True(c.SetEffects(new[] { 0 }, e => e with { AutoColor = true, CurveMids = 0.7 }));
        Assert.True(c.HasEffects);
        var plan = EditExport.Build(c, Bilgi(), Anahtarlar, 0, kip, "cikti.mp4", "is", 8 * Gb, sinirlar);
        Assert.Equal(ExportMode.Full, plan.Effective);
        Assert.True(plan.EffectsForcedFull);
        Assert.Contains("setpts=PTS-STARTPTS,normalize=smoothing=24:independence=1:strength=1,curves=master='0/0 0.25/0.25 0.5/0.7 0.75/0.75 1/1'[v0]",
            Deger(plan.Steps[0].Args, "-filter_complex"));
    }

    [Fact]
    public void OnizlemeIleriRenkParcaPenceresindeKalir()
    {
        var c = EditTimeline.FromSource(S(10));
        Assert.True(c.Split(S(4)));
        c.SetEffects(new[] { 1 }, e => e with { AutoColor = true, CurveShadows = 0.2, ShadowAmount = 0.5 });
        var (video, ses) = ClipFilters.Timed(new EdlPreview("k.mp4", c));
        const string pencere = ":enable='gte(t,4)*lt(t,10)'";
        Assert.Equal(ClipGrade.AutoColorFilter + pencere + ",curves=master='0/0 0.25/0.2 0.5/0.5 0.75/0.75 1/1'" + pencere
                     + ",colorbalance=rs=0.5:gs=-0.25:bs=-0.25:pl=1" + pencere, video);
        Assert.Null(ses);
    }

    [Fact]
    public void ProjeDosyasiIleriRengiTasirEskiDosyaNotrAcilir()
    {
        var model = EditTimeline.FromSource(S(60));
        Assert.True(model.Split(S(30)));
        model.SetEffects(new[] { 0 }, e => e with { AutoColor = true, CurveHighlights = 0.85, HighlightHue = 200, HighlightAmount = 0.25 });
        model.SetEffects(new[] { 1 }, e => e with { Rotation = 90, ShadowHue = 30, ShadowAmount = 0.1, MidHue = 300, MidAmount = 0.4, CurveShadows = 0.3, CurveMids = 0.45 });
        var json = EditProject.From(new SourceStamp(@"C:\ileri.mp4", 16, 1), model).ToJson();
        var okunan = EditProject.Parse(json);
        Assert.NotNull(okunan);
        Assert.Equal(model.Clips, okunan!.Clips);

        var eski = Regex.Replace(json, ",\\s*\"(autoColor|curve[A-Za-z]+|shadow[A-Za-z]+|mid[HA][a-z]+|highlight[A-Za-z]+)\"\\s*:[^,}]+", string.Empty);
        Assert.DoesNotContain("autoColor", eski);
        Assert.DoesNotContain("highlightAmount", eski);
        var eskiOkunan = EditProject.Parse(eski);
        Assert.True(eskiOkunan is not null, eski);
        Assert.Equal(90, eskiOkunan!.Clips[1].Effects.Rotation);
        Assert.All(eskiOkunan.Clips, k => Assert.False(k.Effects.HasGrade, k.Effects.ToString()));

        var notr = EditProject.From(new SourceStamp(@"C:\ileri.mp4", 16, 1), EditTimeline.FromSource(S(60))).ToJson();
        Assert.DoesNotContain("autoColor", notr);
    }

    [FfmpegFact]
    public async Task CanliKaredeOnceSonraOlcusu()
    {
        var klasor = Path.Combine(Kanit, "canli");
        Directory.CreateDirectory(klasor);
        var adlar = new[] { "gri.mp4", "koyu.mp4", "soluk.mp4", "egri.mp4", "teker.mp4", "otomatik.mp4", "yalin.mp4", "kare.rgb" };
        KanitKapanisi.Onceki(klasor, adlar);
        var gri = Path.Combine(klasor, "gri.mp4");
        Ffmpeg("-hide_banner", "-nostdin", "-y", "-f", "lavfi", "-i", "color=0x808080:s=320x240:r=30:d=2",
            "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", "-threads", "2", gri);
        var koyu = Path.Combine(klasor, "koyu.mp4");
        Ffmpeg("-hide_banner", "-nostdin", "-y", "-f", "lavfi", "-i", "color=0x606060:s=320x240:r=30:d=2",
            "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", "-threads", "2", koyu);
        var soluk = Path.Combine(klasor, "soluk.mp4");
        Ffmpeg("-hide_banner", "-nostdin", "-y", "-f", "lavfi", "-i", "testsrc2=s=320x240:r=30:d=2",
            "-vf", "eq=contrast=0.5:brightness=0.08,colorchannelmixer=rr=0.8:bb=0.6", "-c:v", "libx264", "-preset", "ultrafast",
            "-pix_fmt", "yuv420p", "-threads", "2", soluk);
        var kare = Path.Combine(klasor, "kare.rgb");

        async Task<Olcu> Aktar(string kaynak, string ad, Func<ClipEffects, ClipEffects> ayar)
        {
            var c = EditTimeline.FromSource(S(2));
            c.SetEffects(new[] { 0 }, ayar);
            var cikti = Path.Combine(klasor, ad);
            var plan = await EditExportRunner.PrepareAsync(new[] { kaynak }, c, ExportMode.Fast, cikti, 8 * Gb);
            Assert.Equal(ExportMode.Full, plan.Effective);
            await EditExportRunner.RunAsync(IkiIsParcacigi(plan), null);
            return Olc(cikti, kare);
        }

        var griOnce = Olc(gri, kare);
        var egri = await Aktar(gri, "egri.mp4", e => e with { CurveMids = 0.8 });
        Assert.True(egri.Luma - griOnce.Luma > 50, $"egri orta tonu kaldirmadi {griOnce} -> {egri}");

        var koyuOnce = Olc(koyu, kare);
        var teker = await Aktar(koyu, "teker.mp4", e => e with { MidHue = 240, MidAmount = 0.6 });
        Assert.True(teker.B - teker.R > 15 && Math.Abs(koyuOnce.B - koyuOnce.R) < 4, $"mavi teker maviye kaydirmadi {koyuOnce} -> {teker}");

        var solukOnce = Olc(soluk, kare);
        var otomatik = await Aktar(soluk, "otomatik.mp4", e => e with { AutoColor = true });
        Assert.True(solukOnce.Sapma > 40, $"kaynakta renk kaymasi yok {solukOnce}");
        Assert.True(otomatik.Sapma < solukOnce.Sapma / 2, $"otomatik renk kaymayi gidermedi {solukOnce} -> {otomatik}");
        Assert.True(otomatik.EnDarAralik > solukOnce.EnDarAralik + 30, $"otomatik renk araligi acmadi {solukOnce} -> {otomatik}");

        var yalin = await Aktar(soluk, "yalin.mp4", e => e with { Saturation = 0.99 });
        Assert.True(Math.Abs(yalin.EnDarAralik - solukOnce.EnDarAralik) < 15, $"yalin aktarim araligi degistirdi {solukOnce} -> {yalin}");

        KanitKapanisi.Kapat(klasor, adlar);
    }

    [FfmpegFact]
    public async Task LibmpvTirnakliEgriGrafiniEdlZamanindaUygular()
    {
        var klasor = Path.Combine(Kanit, "mpv");
        Directory.CreateDirectory(klasor);
        var adlar = new[] { "kaynak.mp4" };
        KanitKapanisi.Onceki(klasor, adlar);
        var kaynak = Path.Combine(klasor, "kaynak.mp4");
        Ffmpeg("-hide_banner", "-nostdin", "-y", "-f", "lavfi", "-i", "color=0x808080:s=320x240:r=30:d=3",
            "-c:v", "libx264", "-preset", "ultrafast", "-g", "15", "-threads", "2", "-pix_fmt", "yuv420p", kaynak);

        var c = EditTimeline.FromSource(S(3));
        Assert.True(c.Split(S(1.5)));
        c.SetEffects(new[] { 1 }, e => e with { CurveMids = 0.85, MidHue = 0, MidAmount = 0.1 });
        var onizleme = new EdlPreview(kaynak, c);
        var gorunum = new EditLook(onizleme, 320, 240);
        Assert.Contains("curves=master='0/0 0.25/0.25 0.5/0.85 0.75/0.75 1/1':enable='gte(t,1.5)*lt(t,3)'", gorunum.TimedVideo);

        using (var motor = new MpvEngine())
        {
            motor.SetProperty("ao", "null");
            var surucu = new EdlPreviewDriver(motor, onizleme, look: gorunum);
            await surucu.OpenAsync();
            motor.Pause();

            await surucu.SeekAsync(S(0.5), SeekPrecision.Exact);
            var duz = await KareBekleAsync(motor, 0, k => k.Parlak is > 100 and < 150);
            Assert.True(duz.Parlak is > 100 and < 150, $"birinci parca gri degil {duz.Parlak}");

            await surucu.SeekAsync(S(2), SeekPrecision.Exact);
            var acik = await KareBekleAsync(motor, duz.Gorulen, k => k.Parlak > 170);
            Assert.True(acik.Parlak > 170, $"EDL 2 sn'de egri uygulanmadi {duz.Parlak} -> {acik.Parlak}; log: " + string.Join(" | ", motor.RecentLog));
        }

        KanitKapanisi.Kapat(klasor, adlar);
    }

    [Fact]
    public void ArayuzIleriRenkKutulariModeleVeOnizlemeyeGider()
    {
        var klasor = Path.Combine(Kanit, "arayuz");
        Directory.CreateDirectory(klasor);
        var dosya = Path.Combine(klasor, "kaynak.mp4");
        File.WriteAllBytes(dosya, new byte[16]);
        try
        {
            AppHost.Run(() =>
            {
                var motor = new KlipMotoru();
                var view = new EditorView
                {
                    KnownInfo = yol => new MediaInfo { FilePath = yol, FileSizeBytes = 16, DurationSeconds = 600, Width = 320, Height = 240, Fps = 30, VideoCodec = "h264", TotalBitrateBps = 1 }
                };
                view.Player.EngineFactory = () => motor;
                view.OverlayRoot = Path.Combine(klasor, "katman");
                var pencere = new Window { Width = 900, Height = 600, Content = view };
                pencere.Show();
                try
                {
                    var acilis = view.OpenSourceAsync(dosya);
                    DenetimSurucu.Pump(view.Player, () => acilis.IsCompleted, 10);
                    acilis.GetAwaiter().GetResult();
                    DenetimSurucu.Pump(view.Player, () => motor.Acilan >= 1, 10);
                    var model = view.Model!;
                    var acilan = motor.Acilan;

                    view.TimelineView.SelectedIndex = 0;
                    Assert.True(view.ClipPanel.IsVisible);
                    Assert.Equal("0.5", view.TxtClipCurveMids.Text?.Replace(',', '.'));
                    Assert.Equal("0", view.TxtClipMidHue.Text);

                    void Yaz(TextBox kutu, string metin)
                    {
                        kutu.Text = metin;
                        kutu.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
                    }

                    view.BtnClipAutoColor.IsChecked = true;
                    view.BtnClipAutoColor.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.True(model.Clips[0].Effects.AutoColor);
                    DenetimSurucu.Pump(view.Player, () => view.Restyles >= 1, 10);
                    Assert.Equal(ClipGrade.AutoColorFilter + ":enable='gte(t,0)*lt(t,600)'", motor.ZamanliVideo);
                    Assert.Equal(0, view.CmbClipLook.SelectedIndex);

                    Yaz(view.TxtClipCurveMids, "0.7");
                    Assert.Equal(0.7, model.Clips[0].Effects.CurveMids, 9);
                    Yaz(view.TxtClipCurveShadows, "9");
                    Assert.Equal(1, model.Clips[0].Effects.CurveShadows);
                    Assert.Equal("1", view.TxtClipCurveShadows.Text);
                    Yaz(view.TxtClipShadowHue, "-90");
                    Assert.Equal(270, model.Clips[0].Effects.ShadowHue);
                    Assert.False(model.Clips[0].Effects.HasWheels);
                    Yaz(view.TxtClipShadowAmount, "0.3");
                    Assert.Equal((270, 0.3), (model.Clips[0].Effects.ShadowHue, model.Clips[0].Effects.ShadowAmount));
                    Assert.Equal("270", view.TxtClipShadowHue.Text);
                    Yaz(view.TxtClipHighlightAmount, "abc");
                    Assert.Equal(0, model.Clips[0].Effects.HighlightAmount);
                    Assert.Equal("0", view.TxtClipHighlightAmount.Text);
                    Yaz(view.TxtClipMidHue, "120");
                    Yaz(view.TxtClipMidAmount, "0.2");
                    Yaz(view.TxtClipHighlightHue, "60");
                    Yaz(view.TxtClipHighlightAmount, "0.1");
                    Yaz(view.TxtClipCurveHighlights, "0.8");
                    var e = model.Clips[0].Effects;
                    Assert.Equal((120, 0.2, 60, 0.1, 0.8), (e.MidHue, e.MidAmount, e.HighlightHue, e.HighlightAmount, e.CurveHighlights));
                    DenetimSurucu.Pump(view.Player, () => motor.ZamanliVideo?.Contains("colorbalance=", StringComparison.Ordinal) == true
                                                          && motor.ZamanliVideo.Contains("bh=", StringComparison.Ordinal), 10);
                    Assert.Contains("curves=master='0/0 0.25/1 0.5/0.7 0.75/0.8 1/1'", motor.ZamanliVideo);
                    Assert.Contains("colorbalance=rs=0:gs=-0.26:bs=0.26:rm=-0.1:gm=0.2:bm=-0.1:rh=0.05:gh=0.05:bh=-0.1:pl=1", motor.ZamanliVideo);

                    Assert.True(view.Undo());
                    Assert.Equal(e with { CurveHighlights = ClipEffects.HighlightPoint }, model.Clips[0].Effects);
                    Assert.Equal("0.75", view.TxtClipCurveHighlights.Text?.Replace(',', '.'));

                    view.BtnClipReset.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.True(model.Clips[0].Effects.IsNeutral);
                    Assert.False(view.BtnClipAutoColor.IsChecked);
                    DenetimSurucu.Pump(view.Player, () => motor.ZamanliVideo is null, 10);
                    Assert.Null(motor.ZamanliVideo);
                    Assert.Equal(acilan, motor.Acilan);
                }
                finally
                {
                    DuzenleyiciKapanis.Kapat(pencere, view);
                }
            });
        }
        finally
        {
            if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
            KanitKapanisi.Kapat(Kanit);
        }
    }

    private readonly record struct Olcu(double R, double G, double B, int EnDarAralik)
    {
        public double Luma => 0.299 * R + 0.587 * G + 0.114 * B;

        public double Sapma => Math.Max(R, Math.Max(G, B)) - Math.Min(R, Math.Min(G, B));

        public override string ToString()
            => string.Create(CultureInfo.InvariantCulture, $"R={R:0.0} G={G:0.0} B={B:0.0} sapma={Sapma:0.0} aralik={EnDarAralik}");
    }

    private static Olcu Olc(string video, string kare)
    {
        Ffmpeg("-hide_banner", "-nostdin", "-v", "error", "-y", "-ss", "1", "-i", video,
            "-frames:v", "1", "-f", "rawvideo", "-pix_fmt", "rgb24", "-threads", "2", kare);
        var piksel = File.ReadAllBytes(kare);
        Assert.Equal(320 * 240 * 3, piksel.Length);
        var toplam = new double[3];
        var enAz = new[] { 255, 255, 255 };
        var enCok = new[] { 0, 0, 0 };
        for (var i = 0; i < piksel.Length; i++)
        {
            var k = i % 3;
            toplam[k] += piksel[i];
            enAz[k] = Math.Min(enAz[k], piksel[i]);
            enCok[k] = Math.Max(enCok[k], piksel[i]);
        }

        var n = piksel.Length / 3.0;
        var aralik = Enumerable.Range(0, 3).Min(k => enCok[k] - enAz[k]);
        return new Olcu(toplam[0] / n, toplam[1] / n, toplam[2] / n, aralik);
    }

    private static void Ffmpeg(params string[] args)
    {
        var psi = new ProcessStartInfo(ToolLocator.Ffmpeg)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var surec = Process.Start(psi)!;
        var hata = surec.StandardError.ReadToEndAsync();
        var standart = surec.StandardOutput.ReadToEndAsync();
        if (!surec.WaitForExit(60_000))
        {
            surec.Kill(true);
            Assert.Fail("ffmpeg 60 sn icinde bitmedi");
        }

        surec.WaitForExit();
        _ = standart.Result;
        Assert.True(surec.ExitCode == 0, hata.Result);
    }

    private static ExportPlan IkiIsParcacigi(ExportPlan plan)
    {
        var args = plan.Steps[0].Args.ToList();
        args.InsertRange(args.Count - 1, new[] { "-threads", "2" });
        return plan with { Steps = new[] { plan.Steps[0] with { Args = args } } };
    }

    private static async Task<(int Parlak, long Gorulen)> KareBekleAsync(MpvEngine motor, long gorulen, Func<(int W, int H, int Parlak), bool> kosul)
    {
        var saat = Stopwatch.StartNew();
        var kare = await KareAsync(motor, gorulen);
        while (!kosul(kare.Item1) && saat.Elapsed < TimeSpan.FromSeconds(5))
        {
            await Task.Delay(20);
            kare = await KareAsync(motor, -1);
        }

        return (kare.Item1.Parlak, kare.Item2);
    }

    private static async Task<((int W, int H, int Parlak), long)> KareAsync(MpvEngine motor, long gorulen)
    {
        var saat = Stopwatch.StartNew();
        (int W, int H, int Parlak)? sonuc = null;
        while (sonuc is null && saat.Elapsed < TimeSpan.FromSeconds(10))
        {
            motor.TryCopyLatest(ref gorulen, (p, w, h, s) =>
            {
                var satir = new byte[s];
                Marshal.Copy(p + (h / 2) * s, satir, 0, s);
                var i = (w / 2) * 4;
                sonuc = (w, h, Math.Min(satir[i], Math.Min(satir[i + 1], satir[i + 2])));
            });
            if (sonuc is null) await Task.Delay(10);
        }

        Assert.True(sonuc is not null, "10 sn icinde kare gelmedi; son log: " + string.Join(" | ", motor.RecentLog));
        return (sonuc!.Value, gorulen);
    }
}
