using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using VidShrink.App.Editing;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using VidShrink.Player;
using Xunit;

namespace VidShrink.Tests;

public sealed class DuzenleyiciLutTests
{
    private const string Dal = "yan-lut";
    private const long Gb = 1_000_000_000;
    private const string ZorAd = "lut canlı, 'şğü' [1];x";

    private static long S(double saniye) => EditTime.FromSeconds(saniye);

    private static string Kanit
    {
        get
        {
            var yol = Path.Combine(GirdiKanit.Root, ".calisma", Dal);
            Directory.CreateDirectory(yol);
            return yol;
        }
    }

    private static string Klasor(string ad)
    {
        var klasor = Path.Combine(Kanit, ad);
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        Directory.CreateDirectory(klasor);
        return klasor;
    }

    private static void Kapat(string klasor)
    {
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        KanitKapanisi.Kapat(Kanit);
    }

    private static string KirmiziMavi()
    {
        var metin = new StringBuilder("TITLE \"kirmizi mavi takasi\"\n# deneme\nLUT_3D_SIZE 2\n");
        for (var b = 0; b < 2; b++)
            for (var g = 0; g < 2; g++)
                for (var r = 0; r < 2; r++)
                    metin.Append(b).Append(' ').Append(g).Append(' ').Append(r).Append('\n');
        return metin.ToString();
    }

    private static string LutYaz(string klasor, string ad = "kırmızı-mavi.cube")
    {
        var yol = Path.Combine(klasor, ad);
        File.WriteAllText(yol, KirmiziMavi(), new UTF8Encoding(false));
        return yol;
    }

    [Fact]
    public void YolKacisiSurucuHarfiTirnakVirgulKoseliParantezVeTurkce()
    {
        Assert.Equal(@"C\\:\\\\Kullanıcılar\\\\Şule\\\'s\, \[x\]\;y\\\\renk.cube",
            LutFile.FilterPath(@"C:\Kullanıcılar\Şule's, [x];y\renk.cube"));
        Assert.Equal("/tmp/a b/ç.cube", LutFile.FilterPath("/tmp/a b/ç.cube"));
        Assert.Equal(@"\\\\\\\\sunucu\\\\pay\\\\l.cube", LutFile.FilterPath(@"\\sunucu\pay\l.cube"));
    }

    [Fact]
    public void RenkZinciriLutuEnSonaKoyarOnizlemeVirgulluYoluBolmez()
    {
        var yol = @"C:\a, b\ş.cube";
        var etki = ClipEffects.None with { Saturation = 0, Temperature = 3000, Lut = yol };
        Assert.True(etki.HasLut);
        Assert.True(etki.HasColor);
        var lut = @"lut3d=file=C\\:\\\\a\, b\\\\ş.cube";
        Assert.Equal("eq=saturation=0,colortemperature=temperature=3000," + lut, ClipFilters.Color(etki));
        Assert.Equal(lut, ClipFilters.Lut(etki));

        var c = EditTimeline.FromSource(S(10));
        Assert.True(c.Split(S(4)));
        c.SetEffects(new[] { 1 }, _ => ClipEffects.None with { Lut = yol });
        var (video, ses) = ClipFilters.Timed(new EdlPreview("k.mp4", c));
        Assert.Equal(lut + ":enable='gte(t,4)*lt(t,10)'", video);
        Assert.Null(ses);

        var sifir = ClipEffects.None with { Lut = yol, LutMix = 0 };
        Assert.False(sifir.HasLut);
        Assert.False(sifir.HasColor);
        Assert.False(sifir.IsNeutral);
        Assert.Equal(string.Empty, ClipFilters.Color(sifir));

        var tasan = (ClipEffects.None with { Lut = "  " + yol + " ", LutMix = 250 }).Normalized();
        Assert.Equal(yol, tasan.Lut);
        Assert.Equal(100, tasan.LutMix);
        Assert.Equal(0, (ClipEffects.None with { Lut = yol, LutMix = -5 }).Normalized().LutMix);
        Assert.Null((ClipEffects.None with { Lut = " " }).Normalized().Lut);
    }

    [Fact]
    public void MpvSargisiKoseliParantezdeUzunlukluTirnagaGecer()
    {
        Assert.Equal("lavfi=[eq=saturation=0]", MpvEngine.Lavfi("eq=saturation=0"));
        var zincir = @"lut3d=file=C\\\:\\\\ş \[1\].cube";
        Assert.Equal("lavfi=%" + Encoding.UTF8.GetByteCount(zincir).ToString(CultureInfo.InvariantCulture) + "%" + zincir, MpvEngine.Lavfi(zincir));
        Assert.NotEqual(zincir.Length, Encoding.UTF8.GetByteCount(zincir));
    }

    [Fact]
    public void CubeOkumaKarisimVeBozukDosya()
    {
        var kup = LutFile.Parse(KirmiziMavi());
        Assert.NotNull(kup);
        Assert.Equal(2, kup!.Size);
        Assert.Equal(new float[] { 0, 0, 1 }, kup.Data.Skip(3).Take(3).ToArray());

        var yarim = LutFile.Blend(kup, 50);
        Assert.Equal(new float[] { 0.5f, 0, 0.5f }, yarim.Data.Skip(3).Take(3).ToArray());
        Assert.Equal(new float[] { 0, 0, 0, 1, 0, 0 }, LutFile.Blend(kup, 0).Data.Take(6).ToArray());

        var alan = LutFile.Parse("LUT_3D_SIZE 2\nDOMAIN_MIN 0 0 0\nDOMAIN_MAX 2 2 2\n" + string.Concat(Enumerable.Repeat("1 1 1\n", 8)));
        Assert.NotNull(alan);
        Assert.Equal(new float[] { 1.5f, 0.5f, 0.5f }, LutFile.Blend(alan!, 50).Data.Skip(3).Take(3).ToArray());

        var geri = LutFile.Parse(LutFile.Write(yarim));
        Assert.Equal(yarim.Data, geri!.Data);

        Assert.Null(LutFile.Parse("LUT_1D_SIZE 2\n0 0 0\n1 1 1\n"));
        Assert.Null(LutFile.Parse("LUT_3D_SIZE 2\n0 0 0\n"));
        Assert.Null(LutFile.Parse("LUT_3D_SIZE 2\n" + string.Concat(Enumerable.Repeat("0 0 x\n", 8))));
        Assert.Null(LutFile.Parse("LUT_3D_SIZE 1\n0 0 0\n"));
        Assert.Null(LutFile.Parse("LUT_3D_SIZE 2\nDOMAIN_MIN 1 1 1\nDOMAIN_MAX 1 1 1\n" + string.Concat(Enumerable.Repeat("0 0 0\n", 8))));
        Assert.Null(LutFile.Parse(""));
        Assert.Null(LutFile.Read(Path.Combine(Kanit, "olmayan.cube")));
    }

    [Fact]
    public void ProjeDosyasiLutuTasirGoreliCozerYoksaLutsuzAcar()
    {
        var klasor = Klasor("proje");
        var lut = LutYaz(klasor);
        var damga = new SourceStamp(Path.Combine(klasor, "kaynak.mp4"), 16, 1);
        var model = EditTimeline.FromSource(S(60));
        Assert.True(model.Split(S(30)));
        model.SetEffects(new[] { 0 }, e => e with { Lut = lut, LutMix = 40 });
        model.SetEffects(new[] { 1 }, e => e with { Rotation = 90 });
        var json = EditProject.From(damga, model).ToJson();
        Assert.Contains("\"lutMix\": 40", json);

        var okunan = EditProject.Parse(json);
        Assert.Equal(model.Clips, okunan!.Clips);
        Assert.Empty(okunan.MissingLuts);
        Assert.Null(okunan.Clips[1].Effects.Lut);
        Assert.Equal(100, okunan.Clips[1].Effects.LutMix);

        var dosya = Path.Combine(klasor, "kurgu" + EditProject.Extension);
        var kacisli = JsonText(lut);
        File.WriteAllText(dosya, json.Replace(kacisli, JsonText(Path.GetFileName(lut))));
        Assert.DoesNotContain(kacisli, File.ReadAllText(dosya));
        var goreli = EditProject.Read(dosya);
        Assert.Equal(Path.GetFullPath(lut), goreli!.Clips[0].Effects.Lut);
        Assert.Equal(40, goreli.Clips[0].Effects.LutMix);
        Assert.Empty(goreli.MissingLuts);

        var tasinmis = Path.Combine(klasor, "eski-yer", "yok", Path.GetFileName(lut));
        File.WriteAllText(dosya, json.Replace(kacisli, JsonText(tasinmis)));
        Assert.Equal(Path.GetFullPath(lut), EditProject.Read(dosya)!.Clips[0].Effects.Lut);

        var kayip = Path.Combine(klasor, "yok.cube");
        File.WriteAllText(dosya, json.Replace(kacisli, JsonText(kayip)));
        var lutsuz = EditProject.Read(dosya);
        Assert.NotNull(lutsuz);
        Assert.Null(lutsuz!.Clips[0].Effects.Lut);
        Assert.Equal(100, lutsuz.Clips[0].Effects.LutMix);
        Assert.Equal(new[] { kayip }, lutsuz.MissingLuts);
        Assert.Equal(90, lutsuz.Clips[1].Effects.Rotation);

        var eski = json.Replace(",\r\n        \"lut\": " + kacisli, string.Empty).Replace(",\n        \"lut\": " + kacisli, string.Empty)
            .Replace(",\r\n        \"lutMix\": 40", string.Empty).Replace(",\n        \"lutMix\": 40", string.Empty);
        Assert.DoesNotContain("\"lut", eski);
        var eskiOkunan = EditProject.Parse(eski);
        Assert.NotNull(eskiOkunan);
        Assert.Null(eskiOkunan!.Clips[0].Effects.Lut);
        Assert.True(eskiOkunan.Clips[0].Effects.IsNeutral);
        Kapat(klasor);
    }

    private static string JsonText(string metin) => System.Text.Json.JsonSerializer.Serialize(metin);

    private static string Ffmpeg(params string[] args)
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
        Assert.True(surec.ExitCode == 0, hata.Result);
        _ = standart.Result;
        return hata.Result;
    }

    private static ExportPlan IkiIsParcacigi(ExportPlan plan)
    {
        var args = plan.Steps[0].Args.ToList();
        args.InsertRange(args.Count - 1, new[] { "-threads", "2" });
        return plan with { Steps = new[] { plan.Steps[0] with { Args = args } } };
    }

    private static (double R, double G, double B) OrtaRenk(string video, string kare, double saniye, int w, int h)
    {
        Ffmpeg("-hide_banner", "-nostdin", "-v", "error", "-y", "-ss", saniye.ToString(CultureInfo.InvariantCulture), "-i", video,
            "-frames:v", "1", "-f", "rawvideo", "-pix_fmt", "rgb24", "-threads", "2", kare);
        var piksel = File.ReadAllBytes(kare);
        Assert.Equal(w * h * 3, piksel.Length);
        double r = 0, g = 0, b = 0;
        for (var i = 0; i < piksel.Length; i += 3)
        {
            r += piksel[i];
            g += piksel[i + 1];
            b += piksel[i + 2];
        }

        var n = w * h;
        return (r / n, g / n, b / n);
    }

    [FfmpegFact]
    public async Task CanliKaredeLutKirmiziyiMaviyeCevirirYogunlukKaristirir()
    {
        var klasor = Klasor(ZorAd);
        var onbellek = Path.Combine(klasor, "önbellek, [x]");
        var eskiOnbellek = LutFile.CacheFolderOverride;
        LutFile.CacheFolderOverride = onbellek;
        try
        {
            var lut = LutYaz(klasor);
            var kaynak = Path.Combine(klasor, "kaynak.mp4");
            Ffmpeg("-hide_banner", "-nostdin", "-y", "-f", "lavfi", "-i", "color=0xC03030:s=160x120:r=15:d=2",
                "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", "-threads", "2", kaynak);
            var kare = Path.Combine(klasor, "kare.rgb");

            async Task<(double R, double G, double B)> Aktar(string ad, Func<ClipEffects, ClipEffects> ayar)
            {
                var c = EditTimeline.FromSource(S(2));
                c.SetEffects(new[] { 0 }, ayar);
                var cikti = Path.Combine(klasor, ad);
                var plan = await EditExportRunner.PrepareAsync(new[] { kaynak }, c, ExportMode.Fast, cikti, 8 * Gb);
                Assert.Equal(c.Clips[0].Effects.IsNeutral ? ExportMode.Fast : ExportMode.Full, plan.Effective);
                await EditExportRunner.RunAsync(IkiIsParcacigi(plan), null);
                return OrtaRenk(cikti, kare, 1, 160, 120);
            }

            var yalin = await Aktar("yalin.mp4", e => e);
            Assert.True(yalin.R - yalin.B > 100, $"kaynak kirmizi degil {yalin}");

            var tam = await Aktar("tam.mp4", e => e with { Lut = lut });
            Assert.True(tam.B - tam.R > 100, $"LUT kirmiziyi maviye cevirmedi {yalin} -> {tam}");
            Assert.True(Math.Abs(tam.B - yalin.R) < 12 && Math.Abs(tam.R - yalin.B) < 12, $"takas tam degil {yalin} -> {tam}");

            var yarim = await Aktar("yarim.mp4", e => e with { Lut = lut, LutMix = 50 });
            var orta = (yalin.R + yalin.B) / 2;
            Assert.True(Math.Abs(yarim.R - orta) < 12 && Math.Abs(yarim.B - orta) < 12, $"%50 karisim ortada degil {yalin} -> {yarim}");
            var kopya = Directory.GetFiles(onbellek, "*" + LutFile.Extension);
            Assert.Single(kopya);
            Assert.Equal(kopya[0], LutFile.ForMix(lut, 50));

            var sifir = await Aktar("sifir.mp4", e => e with { Lut = lut, LutMix = 0 });
            Assert.True(sifir.R - sifir.B > 100, $"%0 yogunlukta renk degisti {yalin} -> {sifir}");

            var pencere = Path.Combine(klasor, "pencere.mp4");
            var cizelge = EditTimeline.FromSource(S(2));
            Assert.True(cizelge.Split(S(1)));
            cizelge.SetEffects(new[] { 1 }, e => e with { Lut = lut });
            var (zincir, _) = ClipFilters.Timed(new EdlPreview(kaynak, cizelge));
            Assert.Equal("lut3d=file=" + LutFile.FilterPath(lut) + ":enable='gte(t,1)*lt(t,2)'", zincir);
            Ffmpeg("-hide_banner", "-nostdin", "-y", "-i", kaynak, "-vf", zincir!, "-an", "-c:v", "libx264", "-preset", "ultrafast",
                "-pix_fmt", "yuv420p", "-threads", "2", pencere);
            var once = OrtaRenk(pencere, kare, 0.5, 160, 120);
            var sonra = OrtaRenk(pencere, kare, 1.5, 160, 120);
            Assert.True(once.R - once.B > 100, $"pencere disinda LUT uygulandi {once}");
            Assert.True(sonra.B - sonra.R > 100, $"pencere icinde LUT yok {sonra}");
        }
        finally
        {
            LutFile.CacheFolderOverride = eskiOnbellek;
        }

        Kapat(klasor);
    }

    private static async Task<(int A, int C, long Gorulen)> KanalBekleAsync(MpvEngine motor, long gorulen, Func<(int A, int C), bool> kosul)
    {
        var saat = Stopwatch.StartNew();
        (int A, int C)? sonuc = null;
        while (saat.Elapsed < TimeSpan.FromSeconds(10))
        {
            motor.TryCopyLatest(ref gorulen, (p, w, h, s) =>
            {
                var satir = new byte[s];
                Marshal.Copy(p + (h / 2) * s, satir, 0, s);
                var i = (w / 2) * 4;
                sonuc = (satir[i], satir[i + 2]);
            });
            if (sonuc is { } k && kosul(k)) break;
            await Task.Delay(20);
        }

        Assert.True(sonuc is not null, "10 sn icinde kare gelmedi; son log: " + string.Join(" | ", motor.RecentLog));
        return (sonuc!.Value.A, sonuc.Value.C, gorulen);
    }

    [FfmpegFact]
    public async Task LibmpvLutuZorYoldaEdlZamanindaUygular()
    {
        var klasor = Klasor(ZorAd + " mpv");
        var lut = LutYaz(klasor);
        var kaynak = Path.Combine(klasor, "kaynak.mp4");
        Ffmpeg("-hide_banner", "-nostdin", "-y", "-f", "lavfi", "-i", "color=0xC03030:s=160x120:r=15:d=3",
            "-c:v", "libx264", "-preset", "ultrafast", "-g", "15", "-threads", "2", "-pix_fmt", "yuv420p", kaynak);

        var c = EditTimeline.FromSource(S(3));
        Assert.True(c.Split(S(1.5)));
        c.SetEffects(new[] { 1 }, e => e with { Lut = lut });
        var onizleme = new EdlPreview(kaynak, c);
        var gorunum = new EditLook(onizleme, 160, 120);
        Assert.Contains("\\]", gorunum.TimedVideo);

        using (var motor = new MpvEngine())
        {
            motor.SetProperty("ao", "null");
            var surucu = new EdlPreviewDriver(motor, onizleme, look: gorunum);
            await surucu.OpenAsync();
            motor.Pause();

            await surucu.SeekAsync(S(0.5), SeekPrecision.Exact);
            var ilk = await KanalBekleAsync(motor, 0, k => Math.Abs(k.A - k.C) > 100);
            Assert.True(Math.Abs(ilk.A - ilk.C) > 100, $"birinci parca kirmizi degil {ilk}");

            await surucu.SeekAsync(S(2), SeekPrecision.Exact);
            var ikinci = await KanalBekleAsync(motor, ilk.Gorulen, k => Math.Sign(k.A - k.C) == -Math.Sign(ilk.A - ilk.C) && Math.Abs(k.A - k.C) > 100);
            Assert.True(Math.Sign(ikinci.A - ikinci.C) == -Math.Sign(ilk.A - ilk.C) && Math.Abs(ikinci.A - ikinci.C) > 100,
                $"EDL 2 sn'de LUT yok {ilk} -> {ikinci}; log: " + string.Join(" | ", motor.RecentLog));
        }

        Kapat(klasor);
    }

    [Fact]
    public void ArayuzLutYukleYogunlukKaldirVeKayipLutUyarisi()
    {
        var klasor = Klasor("arayuz");
        var dosya = Path.Combine(klasor, "kaynak.mp4");
        File.WriteAllBytes(dosya, new byte[16]);
        var lut = LutYaz(klasor);
        var bozuk = Path.Combine(klasor, "bozuk.cube");
        File.WriteAllText(bozuk, "LUT_3D_SIZE 2\n0 0 0\n");
        AppHost.Run(() =>
        {
            var motor = new KlipMotoru();
            var view = new EditorView
            {
                KnownInfo = yol => new MediaInfo { FilePath = yol, FileSizeBytes = 16, DurationSeconds = 600, Width = 320, Height = 240, Fps = 30, VideoCodec = "h264", TotalBitrateBps = 1 },
                Projects = null,
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

                view.TimelineView.SelectedIndex = 0;
                Assert.True(view.ClipPanel.IsVisible);
                Assert.Equal(Strings.Get("editor.clip.lut.none"), view.TxtClipLutName.Text);
                Assert.False(view.TxtClipLutMix.IsEnabled);
                Assert.False(view.BtnClipLutRemove.IsEnabled);

                Assert.False(view.LoadClipLut(bozuk));
                Assert.Null(model.Clips[0].Effects.Lut);
                Assert.Equal(string.Format(Strings.Culture, Strings.Get("editor.clip.lut.unreadable"), "bozuk.cube"), view.ExportStatusText);

                Assert.True(view.LoadClipLut(lut));
                Assert.Equal(lut, model.Clips[0].Effects.Lut);
                Assert.Equal(100, model.Clips[0].Effects.LutMix);
                Assert.Equal(Path.GetFileName(lut), view.TxtClipLutName.Text);
                Assert.True(view.TxtClipLutMix.IsEnabled);
                Assert.Equal("100", view.TxtClipLutMix.Text);
                DenetimSurucu.Pump(view.Player, () => view.Restyles >= 1, 10);
                Assert.Equal(ClipFilters.Lut(model.Clips[0].Effects) + ":enable='gte(t,0)*lt(t,600)'", motor.ZamanliVideo);

                void Yaz(TextBox kutu, string metin)
                {
                    kutu.Text = metin;
                    kutu.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
                }

                Yaz(view.TxtClipLutMix, "35");
                Assert.Equal(35, model.Clips[0].Effects.LutMix);
                Yaz(view.TxtClipLutMix, "abc");
                Assert.Equal(35, model.Clips[0].Effects.LutMix);
                Assert.Equal("35", view.TxtClipLutMix.Text);
                Yaz(view.TxtClipLutMix, "250");
                Assert.Equal(100, model.Clips[0].Effects.LutMix);

                Assert.True(view.Undo());
                Assert.Equal(35, model.Clips[0].Effects.LutMix);

                Assert.True(view.RemoveClipLut());
                Assert.Null(model.Clips[0].Effects.Lut);
                Assert.True(model.Clips[0].Effects.IsNeutral);
                Assert.Equal(Strings.Get("editor.clip.lut.none"), view.TxtClipLutName.Text);
                DenetimSurucu.Pump(view.Player, () => motor.ZamanliVideo is null, 10);
                Assert.Null(motor.ZamanliVideo);

                Assert.True(view.LoadClipLut(lut));
                var proje = Path.Combine(klasor, "kurgu" + EditProject.Extension);
                Assert.True(view.SaveProjectTo(proje));
                File.Move(lut, Path.Combine(klasor, "gitti.cube"));
                Assert.True(view.RemoveClipLut());
                var acilan = view.OpenProjectAsync(proje);
                DenetimSurucu.Pump(view.Player, () => acilan.IsCompleted, 10);
                Assert.True(acilan.Result);
                Assert.Null(view.Model!.Clips[0].Effects.Lut);
                Assert.Equal(string.Format(Strings.Culture, Strings.Get("editor.clip.lut.missing"), Path.GetFileName(lut)), view.ExportStatusText);
                Assert.True(Avalonia.Application.Current!.TryFindResource("StatusWarning", out var tema));
                Assert.Same(tema, view.TxtExportStatus.Theme);
            }
            finally
            {
                DuzenleyiciKapanis.Kapat(pencere, view);
            }
        });
        Kapat(klasor);
    }
}
