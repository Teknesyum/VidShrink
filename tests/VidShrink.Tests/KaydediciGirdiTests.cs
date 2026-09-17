using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using VidShrink.App.Recorder;
using VidShrink.Core;
using Xunit;
using static VidShrink.Tests.KaydediciAyarTests;

namespace VidShrink.Tests;

public sealed class KaydediciGirdiTests
{
    private static string Kanit
    {
        get
        {
            var yol = Path.Combine(GirdiKanit.Root, ".calisma", "paket-2");
            Directory.CreateDirectory(yol);
            return yol;
        }
    }

    private static string? Dosyada(string anahtar)
        => JsonNode.Parse(File.ReadAllText(RecorderSettings.FilePath!))?[anahtar]?.ToJsonString();

    private sealed class SahteKanca : IInputHooks
    {
        public List<string> Cagrilar { get; } = new();
        public Action<PixelPoint>? Tik;
        public Action<uint, bool>? Tus;

        public bool Start(bool clicks, bool keys, Action<PixelPoint> clicked, Action<uint, bool> key)
        {
            Cagrilar.Add($"basla fare={clicks} klavye={keys}");
            Tik = clicked;
            Tus = key;
            return true;
        }

        public void Stop() => Cagrilar.Add("dur");
    }

    private sealed class SahteBindirme : IInputOverlay
    {
        public List<string> Cagrilar { get; } = new();

        public void Ring(PixelPoint point) => Cagrilar.Add($"halka {point.X},{point.Y}");

        public void Keys(string text, PixelRect region) => Cagrilar.Add($"tus {text}");

        public void Hide() => Cagrilar.Add("gizle");
    }

    private sealed class SahteSes : IClickSound
    {
        public int Calinan;

        public void Play() => Calinan++;
    }

    [Fact]
    public void TusYazisiDegistiricilerleBicimlenir()
    {
        Assert.Equal("Ctrl + Shift + S", KeyText.Format(0x53, true, true, false, false));
        Assert.Equal("Ctrl + Alt + Delete", KeyText.Format(0x2E, true, false, true, false));
        Assert.Equal("Win + F12", KeyText.Format(0x7B, false, false, false, true));
        Assert.Equal("Num 7", KeyText.Format(0x67, false, false, false, false));
        Assert.Equal("←", KeyText.Format(0x25, false, false, false, false));
        Assert.Null(KeyText.Format(0xA2, true, false, false, false));
        Assert.Null(KeyText.Format(0xFF, false, false, false, false));

        var izci = new KeyTracker();
        var akis = new List<string?>
        {
            izci.Feed(0xA2, true), izci.Feed(0xA0, true), izci.Feed(0x53, true), izci.Feed(0x53, false),
            izci.Feed(0xA0, false), izci.Feed(0x54, true), izci.Feed(0xA2, false), izci.Feed(0x54, true)
        };

        Assert.Equal(new string?[] { null, null, "Ctrl + Shift + S", null, null, "Ctrl + T", null, "T" }, akis);
    }

    [Fact]
    public void TiklamaSesiGecerliDalgaDosyasi()
    {
        var dalga = ClickTone.Wave();
        var ornek = ClickTone.SampleRate * ClickTone.Milliseconds / 1000;

        Assert.Equal("RIFF", Encoding.ASCII.GetString(dalga, 0, 4));
        Assert.Equal("WAVE", Encoding.ASCII.GetString(dalga, 8, 4));
        Assert.Equal(dalga.Length - 8, BitConverter.ToInt32(dalga, 4));
        Assert.Equal(ClickTone.SampleRate, BitConverter.ToInt32(dalga, 24));
        Assert.Equal("data", Encoding.ASCII.GetString(dalga, 36, 4));
        Assert.Equal(ornek * 2, BitConverter.ToInt32(dalga, 40));
        Assert.Equal(44 + ornek * 2, dalga.Length);
        var tepe = Enumerable.Range(0, ornek).Max(i => Math.Abs((int)BitConverter.ToInt16(dalga, 44 + 2 * i)));
        Assert.InRange(tepe, short.MaxValue / 4, short.MaxValue / 2);
    }

    [Fact]
    public void BindirmeYeriHesaplanir()
    {
        Assert.Equal(new PixelRect(276, 176, 48, 48), OverlayPlace.Ring(new PixelPoint(300, 200), 48));
        Assert.Equal(new PixelPoint(260, 516), OverlayPlace.Caption(new PixelRect(100, 100, 640, 480), new PixelSize(320, 40), 24));
        Assert.Equal(new PixelPoint(100, 100), OverlayPlace.Caption(new PixelRect(100, 100, 320, 30), new PixelSize(320, 40), 24));
    }

    [Fact]
    public void GirdiAyarlariArayuzdenDosyayaVeKancayaGecer()
    {
        var olcu = AyarDosyasiyla(() => AppHost.Run(() =>
        {
            var kapaliKanca = new SahteKanca();
            var kapali = new RecorderView { SkipAutoMeasure = true, InputHooks = kapaliKanca, InputOverlay = new SahteBindirme(), ClickSound = new SahteSes() };
            kapali.SyncInput(true);
            var kapaliDurum = (kapali.InputActive, kapaliKanca.Cagrilar.Count);

            var once = new RecorderView { SkipAutoMeasure = true };
            Bul<CheckBox>(once, "ChkShowClicks").IsChecked = true;
            Bul<CheckBox>(once, "ChkShowKeys").IsChecked = true;
            var dosya = (Dosyada("showClicks"), Dosyada("clickSound"), Dosyada("showKeys"));

            var kanca = new SahteKanca();
            var bindirme = new SahteBindirme();
            var ses = new SahteSes();
            var view = new RecorderView { SkipAutoMeasure = true, InputHooks = kanca, InputOverlay = bindirme, ClickSound = ses };
            var kutular = (Bul<CheckBox>(view, "ChkShowClicks").IsChecked, Bul<CheckBox>(view, "ChkClickSound").IsChecked, Bul<CheckBox>(view, "ChkShowKeys").IsChecked);
            view.OnInputClick(new PixelPoint(5, 5));
            view.SyncInput(false);
            var oturumsuz = kanca.Cagrilar.Count;
            view.SyncInput(true);
            kanca.Tik!(new PixelPoint(300, 200));
            kanca.Tus!(0xA2, true);
            kanca.Tus!(0x43, true);
            kanca.Tus!(0xA2, false);
            var sesKapaliyken = ses.Calinan;
            view.SyncInput(false);
            kanca.Tik!(new PixelPoint(1, 1));

            var sesli = new RecorderView { SkipAutoMeasure = true };
            Bul<CheckBox>(sesli, "ChkShowClicks").IsChecked = false;
            Bul<CheckBox>(sesli, "ChkShowKeys").IsChecked = false;
            Bul<CheckBox>(sesli, "ChkClickSound").IsChecked = true;
            var sesKanca = new SahteKanca();
            var sesBindirme = new SahteBindirme();
            var sesSayac = new SahteSes();
            var yalnizSes = new RecorderView { SkipAutoMeasure = true, InputHooks = sesKanca, InputOverlay = sesBindirme, ClickSound = sesSayac };
            yalnizSes.SyncInput(true);
            sesKanca.Tik!(new PixelPoint(10, 10));
            sesKanca.Tus!(0x41, true);
            yalnizSes.SyncInput(false);

            return (kapaliDurum, dosya, kutular, oturumsuz, kanca: kanca.Cagrilar, bindirme: bindirme.Cagrilar, sesKapaliyken, sesKanca: sesKanca.Cagrilar, sesBindirme: sesBindirme.Cagrilar, sesSayac: sesSayac.Calinan);
        }));

        File.WriteAllLines(Path.Combine(Kanit, "girdi-gosterimi.txt"), new[]
        {
            $"kapali: aktif={olcu.kapaliDurum.InputActive} kanca cagrisi={olcu.kapaliDurum.Count}",
            $"json showClicks={olcu.dosya.Item1} clickSound={olcu.dosya.Item2} showKeys={olcu.dosya.Item3}",
            $"yeni gorunum kutulari={olcu.kutular}",
            $"halka+tus: kanca={string.Join(" | ", olcu.kanca)} bindirme={string.Join(" | ", olcu.bindirme)} ses={olcu.sesKapaliyken}",
            $"yalniz ses: kanca={string.Join(" | ", olcu.sesKanca)} bindirme={olcu.sesBindirme.Count} ses={olcu.sesSayac}"
        });

        Assert.Equal((false, 0), olcu.kapaliDurum);
        Assert.Equal(("true", "false", "true"), olcu.dosya);
        Assert.Equal((true, false, true), (olcu.kutular.Item1 == true, olcu.kutular.Item2 == true, olcu.kutular.Item3 == true));
        Assert.Equal(0, olcu.oturumsuz);
        Assert.Equal(new[] { "basla fare=True klavye=True", "dur" }, olcu.kanca);
        Assert.Equal(new[] { "halka 300,200", "tus Ctrl + C", "gizle" }, olcu.bindirme);
        Assert.Equal(0, olcu.sesKapaliyken);
        Assert.Equal(new[] { "basla fare=True klavye=False", "dur" }, olcu.sesKanca);
        Assert.Equal(new[] { "gizle" }, olcu.sesBindirme);
        Assert.Equal(1, olcu.sesSayac);
    }

    private static void Bekle(int ms)
    {
        var saat = Stopwatch.StartNew();
        while (saat.ElapsedMilliseconds < ms)
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Thread.Sleep(20);
        }
    }

    /// <summary>
    /// Alt sınır tutma süresi (360 ms), üst sınır yalnız "kendiliğinden kapanır" demek: dağıtıcı zamanlayıcısı
    /// yüklü koşucuda gecikiyor. Yerelde 379 ms; t0/paket-2b CI koşumu 35191568301'de canlı ffmpeg testleriyle
    /// aynı anda 6304 ms ölçüldü ve eski 5000 ms sınırı düştü. Üst sınır bekleme döngüsünün 10 sn tavanının altında.
    /// </summary>
    [Fact]
    public void GercekHalkaVeTusYazisiYerindeAcilipKendiligindenKapanir()
    {
        var olcu = AppHost.Run(() =>
        {
            var bindirme = new RecorderInputOverlay();
            var bolge = new PixelRect(100, 100, 640, 480);
            var yaziSaat = Stopwatch.StartNew();
            bindirme.Keys("Ctrl + S", bolge);
            var halkaSaat = Stopwatch.StartNew();
            bindirme.Ring(new PixelPoint(300, 200));
            var halka = bindirme.RingWindow!;
            var yazi = bindirme.CaptionWindow!;
            var olcek = halka.RenderScaling;
            var ilk = (halkaAcik: halka.IsVisible, halkaKonum: halka.Position, halkaGen: halka.Width, yaziAcik: yazi.IsVisible, yaziKonum: yazi.Position, yazi: yazi.Text, yaziBoy: yazi.Bounds.Size);
            long halkaKapandi = -1, yaziKapandi = -1;
            var halkaKapaninca = true;
            while (yaziSaat.ElapsedMilliseconds < 10000 && (halkaKapandi < 0 || yaziKapandi < 0))
            {
                Bekle(10);
                if (halkaKapandi < 0 && !halka.IsVisible) { halkaKapandi = halkaSaat.ElapsedMilliseconds; halkaKapaninca = yazi.IsVisible; }
                if (yaziKapandi < 0 && !yazi.IsVisible) yaziKapandi = yaziSaat.ElapsedMilliseconds;
            }
            bindirme.Hide();
            return (ilk, halkaKapandi, yaziKapandi, halkaKapaninca, olcek);
        });

        File.WriteAllLines(Path.Combine(Kanit, "girdi-bindirme.txt"), new[]
        {
            $"0ms: halka acik={olcu.ilk.halkaAcik} konum={olcu.ilk.halkaKonum} genislik={olcu.ilk.halkaGen} | yazi acik={olcu.ilk.yaziAcik} konum={olcu.ilk.yaziKonum} metin={olcu.ilk.yazi} boy={olcu.ilk.yaziBoy}",
            $"halka kapandi={olcu.halkaKapandi}ms (tutma 360) yazi o an acik={olcu.halkaKapaninca}",
            $"yazi kapandi={olcu.yaziKapandi}ms (tutma 1500)"
        });

        var cap = (int)Math.Ceiling(48 * olcu.olcek);
        Assert.True(olcu.ilk.halkaAcik);
        Assert.Equal(new PixelPoint(300 - cap / 2, 200 - cap / 2), olcu.ilk.halkaKonum);
        Assert.Equal(48, olcu.ilk.halkaGen);
        Assert.True(olcu.ilk.yaziAcik);
        Assert.Equal("Ctrl + S", olcu.ilk.yazi);
        Assert.True(olcu.ilk.yaziKonum.Y > 100 + 480 / 2 && olcu.ilk.yaziKonum.Y < 100 + 480);
        Assert.InRange(olcu.halkaKapandi, 360, 9500);
        Assert.InRange(olcu.yaziKapandi, 1500, 10000);
    }

    [KayitFact]
    public void KayitBaslayincaKancaKurulurBolgeDisiTiklamaHalkaVermez()
    {
        var klasor = Path.Combine(Kanit, "girdi-canli");
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);

        var olcu = AyarDosyasiyla(() => AppHost.Run(() =>
        {
            var kanca = new SahteKanca();
            var bindirme = new SahteBindirme();
            var view = new RecorderView { SkipAutoMeasure = true, InputHooks = kanca, InputOverlay = bindirme, ClickSound = new SahteSes() };
            Elle(view);
            Bul<ComboBox>(view, "CmbTarget").SelectedIndex = (int)RecorderTargetKind.Region;
            Yaz(view, "TxtRegionX", "0");
            Yaz(view, "TxtRegionY", "0");
            Yaz(view, "TxtRegionWidth", "320");
            Yaz(view, "TxtRegionHeight", "240");
            Sec(view, "CmbCodec", "libx264");
            Sec(view, "CmbPreset", "ultrafast");
            Yaz(view, "TxtOutputFolder", klasor);
            Bul<CheckBox>(view, "ChkShowClicks").IsChecked = true;

            var once = kanca.Cagrilar.Count;
            var basla = view.StartAsync();
            var saat = Stopwatch.StartNew();
            while (!basla.IsCompleted && saat.ElapsedMilliseconds < 15000) Bekle(20);
            var oturum = view.HasSession;
            var kayitta = string.Join(" | ", kanca.Cagrilar);
            kanca.Tik?.Invoke(new PixelPoint(100, 100));
            kanca.Tik?.Invoke(new PixelPoint(900, 700));
            Bekle(800);
            var iptal = view.RunHotkeyAsync(HotkeyAction.Discard);
            saat.Restart();
            while (!iptal.IsCompleted && saat.ElapsedMilliseconds < 15000) Bekle(20);
            return (once, oturum, kayitta, hata: view.ErrorText, sonra: string.Join(" | ", kanca.Cagrilar), bindirme: string.Join(" | ", bindirme.Cagrilar), aktif: view.InputActive);
        }));

        File.WriteAllLines(Path.Combine(Kanit, "girdi-canli.txt"), new[] { $"oncesi={olcu.once} oturum={olcu.oturum} kayitta=[{olcu.kayitta}] sonra=[{olcu.sonra}] bindirme=[{olcu.bindirme}] aktif={olcu.aktif} hata={olcu.hata}" });
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);

        Assert.True(olcu.oturum, olcu.hata);
        Assert.Equal(0, olcu.once);
        Assert.Equal("basla fare=True klavye=False", olcu.kayitta);
        Assert.Equal("basla fare=True klavye=False | dur", olcu.sonra);
        Assert.Equal("halka 100,100 | gizle", olcu.bindirme);
        Assert.False(olcu.aktif);
    }

    [Fact]
    public void MiniAyarlarBuyukPencereyeVeDosyayaGecer()
    {
        var olcu = AyarDosyasiyla(() => AppHost.Run(() =>
        {
            var view = new RecorderView { SkipAutoMeasure = true, InputHooks = new SahteKanca(), InputOverlay = new SahteBindirme(), ClickSound = new SahteSes() };
            Bul<CheckBox>(view, "ChkShowKeys").IsChecked = true;
            view.ShrinkToMini();
            var mini = view.Mini!;
            var yansiyan = (mini.ChkShowKeys.IsChecked, mini.ChkShowClicks.IsChecked, mini.ChkCursor.IsEnabled);

            var dosyaOnce = File.ReadAllBytes(RecorderSettings.FilePath!);
            view.Mini!.ShowOptions(false, false, false, false, false, false);
            var yankisiz = (dosya: dosyaOnce.AsSpan().SequenceEqual(File.ReadAllBytes(RecorderSettings.FilePath!)), anaKutu: Bul<CheckBox>(view, "ChkShowKeys").IsChecked);
            view.ExpandFromMini();

            view.ShrinkToMini();
            view.Mini!.ChkShowClicks.IsChecked = true;
            view.Mini!.ChkOpenFolder.IsChecked = true;
            view.Mini!.ChkCursor.IsChecked = false;
            var ana = (Bul<CheckBox>(view, "ChkShowClicks").IsChecked, Bul<CheckBox>(view, "ChkOpenFolder").IsChecked, Bul<CheckBox>(view, "ChkCursor").IsChecked);
            var dosya = (Dosyada("showClicks"), Dosyada("openFolderWhenDone"), Dosyada("showCursor"));
            view.ExpandFromMini();
            return (yansiyan, yankisiz, ana, dosya);
        }));

        File.WriteAllLines(Path.Combine(Kanit, "mini-ayar.txt"), new[]
        {
            $"mini acilinca: tuslar={olcu.yansiyan.Item1} halka={olcu.yansiyan.Item2} imlec etkin={olcu.yansiyan.Item3}",
            $"ShowOptions yankisi: dosya degismedi={olcu.yankisiz.dosya} ana tus kutusu={olcu.yankisiz.anaKutu}",
            $"miniden: ana kutular halka/klasor/imlec={olcu.ana}",
            $"json showClicks={olcu.dosya.Item1} openFolderWhenDone={olcu.dosya.Item2} showCursor={olcu.dosya.Item3}"
        });

        Assert.Equal(((bool?)true, (bool?)false, true), olcu.yansiyan);
        Assert.Equal((true, (bool?)true), olcu.yankisiz);
        Assert.Equal(((bool?)true, (bool?)true, (bool?)false), olcu.ana);
        Assert.Equal(("true", "true", "false"), olcu.dosya);
    }

    [KayitFact]
    public void MiniAyarKayitSurerkenKancayiYenilerImlecKilitli()
    {
        var klasor = Path.Combine(Kanit, "mini-canli");
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);

        var olcu = AyarDosyasiyla(() => AppHost.Run(() =>
        {
            var kanca = new SahteKanca();
            var bindirme = new SahteBindirme();
            var view = new RecorderView { SkipAutoMeasure = true, InputHooks = kanca, InputOverlay = bindirme, ClickSound = new SahteSes() };
            Elle(view);
            Bul<ComboBox>(view, "CmbTarget").SelectedIndex = (int)RecorderTargetKind.Region;
            Yaz(view, "TxtRegionX", "0");
            Yaz(view, "TxtRegionY", "0");
            Yaz(view, "TxtRegionWidth", "320");
            Yaz(view, "TxtRegionHeight", "240");
            Sec(view, "CmbCodec", "libx264");
            Sec(view, "CmbPreset", "ultrafast");
            Yaz(view, "TxtOutputFolder", klasor);
            Bul<CheckBox>(view, "ChkShowClicks").IsChecked = true;
            Bul<CheckBox>(view, "ChkCursor").IsChecked = true;

            view.ShrinkToMini();
            var bosta = view.Mini!.ChkCursor.IsEnabled;
            var basla = view.StartAsync();
            var saat = Stopwatch.StartNew();
            while (!basla.IsCompleted && saat.ElapsedMilliseconds < 15000) Bekle(20);
            var oturum = view.HasSession;
            var kayitta = view.Mini!.ChkCursor.IsEnabled;

            view.Mini!.ChkShowKeys.IsChecked = true;
            kanca.Tus!(0xA2, true);
            kanca.Tus!(0x43, true);
            view.ApplyMiniOption(new MiniOption(MiniOptionKind.Cursor, false));
            var imlec = (ana: Bul<CheckBox>(view, "ChkCursor").IsChecked, mini: view.Mini!.ChkCursor.IsChecked);
            var dosya = Dosyada("showKeys");
            Bekle(500);

            var iptal = view.RunHotkeyAsync(HotkeyAction.Discard);
            saat.Restart();
            while (!iptal.IsCompleted && saat.ElapsedMilliseconds < 15000) Bekle(20);
            return (bosta, oturum, kayitta, hata: view.ErrorText, kanca: string.Join(" | ", kanca.Cagrilar), bindirme: string.Join(" | ", bindirme.Cagrilar), imlec, dosya);
        }));

        File.WriteAllLines(Path.Combine(Kanit, "mini-canli.txt"), new[]
        {
            $"imlec kutusu bosta etkin={olcu.bosta} kayitta etkin={olcu.kayitta} oturum={olcu.oturum} hata={olcu.hata}",
            $"kanca=[{olcu.kanca}] bindirme=[{olcu.bindirme}]",
            $"kayitta imlec degistirme denemesi: ana={olcu.imlec.ana} mini={olcu.imlec.mini} | json showKeys={olcu.dosya}"
        });
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);

        Assert.True(olcu.oturum, olcu.hata);
        Assert.True(olcu.bosta);
        Assert.False(olcu.kayitta);
        Assert.Equal("basla fare=True klavye=False | dur | basla fare=True klavye=True | dur", olcu.kanca);
        Assert.Equal("gizle | tus Ctrl + C | gizle", olcu.bindirme);
        Assert.Equal(((bool?)true, (bool?)true), olcu.imlec);
        Assert.Equal("true", olcu.dosya);
    }
}
