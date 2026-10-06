using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using VidShrink.App.Recorder;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Kaydedicinin genel kısayolları yeniden atanır: atama <c>recorder-settings.json</c>'a yazılıp
/// yeni görünümde geri okunur, alanı olmayan ya da bozuk ayar F7–F11 ile açılır, başka eylemin
/// tuşu ve sistemin kaydetmediği tuş reddedilip o eylemin satırında söylenir, atanan tuş sahte
/// kancada eylemi tetikler ve eski tuş tetiklemez. Gerçek <c>RegisterHotKey</c> çağrılmaz
/// (sahte <see cref="IGlobalHotkeys"/>), ayar her ölçünün kendi dosyasındadır.
/// </summary>
public sealed class KaydediciKisayolAtamaTests
{
    private sealed class SahteKanca : IGlobalHotkeys
    {
        private Action<HotkeyAction>? _basildi;

        public HashSet<(Key, KeyModifiers)> Dolu { get; } = new();

        public IReadOnlyList<HotkeyBinding>? Kayitli { get; private set; }

        public int Kayit { get; private set; }

        public int Birakma { get; private set; }

        public IReadOnlyList<HotkeyBinding> Register(IReadOnlyList<HotkeyBinding> bindings, Action<HotkeyAction> pressed)
        {
            Kayit++;
            _basildi = pressed;
            var reddedilen = bindings.Where(b => Dolu.Contains((b.Key, b.Modifiers))).ToList();
            Kayitli = bindings.Except(reddedilen).ToList();
            return reddedilen;
        }

        public void Unregister()
        {
            Birakma++;
            Kayitli = null;
        }

        public bool Bas(Key tus, KeyModifiers ek = KeyModifiers.None)
        {
            if (Kayitli?.FirstOrDefault(b => b.Key == tus && b.Modifiers == ek) is not { } bag) return false;
            _basildi!(bag.Action);
            return true;
        }
    }

    private sealed class SahteBolge : IRegionEditorHost
    {
        public string Tus { get; private set; } = string.Empty;

        public bool IsOpen => false;

        public string HideKey { set => Tus = value; }

        public event EventHandler<PixelRect>? Changed { add { } remove { } }

        public event EventHandler<PixelRect>? Committed { add { } remove { } }

        public event EventHandler? StartRequested { add { } remove { } }

        public event EventHandler? SettingsRequested { add { } remove { } }

        public event EventHandler? PauseRequested { add { } remove { } }

        public event EventHandler? ResumeRequested { add { } remove { } }

        public event EventHandler? StopRequested { add { } remove { } }

        public event EventHandler? SnapshotRequested { add { } remove { } }

        public event EventHandler? HideRequested { add { } remove { } }

        public event EventHandler? Dismissed { add { } remove { } }

        public void Show(PixelRect region, double? ratio, RegionEditorPhase phase)
        {
        }

        public void Hide()
        {
        }

        public void Close()
        {
        }
    }

    private static readonly string[] YeniAnahtarlar =
    {
        "recorder.hotkeys.title", "recorder.hotkeys.frame", "recorder.hotkeys.discard", "recorder.hotkeys.replay-save",
        "recorder.hotkeys.reset", "recorder.hotkeys.duplicate", "recorder.hotkeys.rejected", "recorder.hotkeys.unsupported"
    };

    private static HotkeyBinding Bag(RecorderView view, HotkeyAction eylem) => RecorderHotkeys.Of(view.Hotkeys, eylem);

    private static bool Bas(Control hedef, Key tus, KeyModifiers ek = KeyModifiers.None)
    {
        var olay = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = tus, KeyModifiers = ek };
        hedef.RaiseEvent(olay);
        return olay.Handled;
    }

    private static void Tikla(Button dugme) => dugme.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static Window SatirPenceresi(RecorderView view)
    {
        var acilir = (Flyout)view.BtnHotkeys.Flyout!;
        var icerik = (Control)acilir.Content!;
        acilir.Content = null;
        var pencere = new Window { Width = 900, Height = 600, Content = icerik };
        pencere.Show();
        Dispatcher.UIThread.RunJobs();
        return pencere;
    }

    [Fact]
    public void AtamaAyaraYazilirYeniGorunumdeGeriOkunur()
    {
        using var ayar = new KaydediciAyarTests.OzelAyar();
        var (kabul, dosyada, okunan, digerleri, ad) = AppHost.Run(() =>
        {
            Strings.Use("en");
            var ilk = new RecorderView(ayar.Yol);
            var k = ilk.AssignHotkey(HotkeyAction.Frame, Key.K, KeyModifiers.Control);
            var d = KaydediciAyarTests.DosyadakiDeger(ayar.Yol, "hotkeys");
            var yeni = new RecorderView(ayar.Yol);
            var o = Bag(yeni, HotkeyAction.Frame);
            var kalan = RecorderHotkeys.All.Where(b => b.Action != HotkeyAction.Frame)
                .All(b => RecorderHotkeys.Same(b, Bag(yeni, b.Action)));
            return (k, d, o, kalan, yeni.HotkeyName(HotkeyAction.Frame));
        });

        Assert.True(kabul);
        Assert.Equal("\"0:F7,0:F8,2:K,0:F10,0:F11,0:F6\"", dosyada);
        Assert.Equal(Key.K, okunan.Key);
        Assert.Equal(KeyModifiers.Control, okunan.Modifiers);
        Assert.Equal(0x4Bu, okunan.VirtualKey);
        Assert.True(digerleri);
        Assert.Contains("K", ad);
        Assert.Contains("Ctrl", ad);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"hotkeys\":\"bozuk\"}")]
    [InlineData("{\"hotkeys\":\"0:F7,0:F8,0:F9,0:F10\"}")]
    [InlineData("{\"hotkeys\":\"0:F7,0:F7,0:F9,0:F10,0:F11\"}")]
    [InlineData("{\"hotkeys\":\"0:K,0:F8,0:F9,0:F10,0:F11\"}")]
    [InlineData("{\"hotkeys\":\"0:F7,0:F8,0:Escape,0:F10,0:F11\"}")]
    public void EskiYaDaBozukAyarVarsayilanlaAcilir(string icerik)
    {
        using var ayar = new KaydediciAyarTests.OzelAyar();
        File.WriteAllText(ayar.Yol, icerik);
        var yazim = AppHost.Run(() => RecorderHotkeys.Write(new RecorderView(ayar.Yol).Hotkeys));

        Assert.Equal("0:F7,0:F8,0:F9,0:F10,0:F11,0:F6", yazim);
    }

    [Fact]
    public void GecerliAyarVarsayilaniEzer()
    {
        using var ayar = new KaydediciAyarTests.OzelAyar();
        File.WriteAllText(ayar.Yol, "{\"hotkeys\":\"0:F1,3:D5,0:F9,6:NumPad1,1:Home\"}");
        var yazim = AppHost.Run(() => RecorderHotkeys.Write(new RecorderView(ayar.Yol).Hotkeys));

        Assert.Equal("0:F1,3:D5,0:F9,6:NumPad1,1:Home,0:F6", yazim);
    }

    [Fact]
    public void BaskaEyleminTusuReddedilirEskiAtamaKalir()
    {
        using var ayar = new KaydediciAyarTests.OzelAyar();
        var o = AppHost.Run(() =>
        {
            Strings.Use("en");
            var view = new RecorderView(ayar.Yol);
            var red = view.AssignHotkey(HotkeyAction.Stop, Key.F7, KeyModifiers.None);
            var hata = view.HotkeyError(HotkeyAction.Stop);
            var oteki = view.HotkeyError(HotkeyAction.Toggle);
            var kalan = Bag(view, HotkeyAction.Stop).Key;
            var dosya = KaydediciAyarTests.DosyadakiDeger(ayar.Yol, "hotkeys");
            var kabul = view.AssignHotkey(HotkeyAction.Stop, Key.F4, KeyModifiers.None);
            return (red, hata, oteki, kalan, dosya, kabul, Sonra: view.HotkeyError(HotkeyAction.Stop), Yeni: Bag(view, HotkeyAction.Stop).Key,
                Ad: VidShrink.App.LanguageCatalog.Display(Strings.Get("recorder.strip.start")));
        });

        Assert.False(o.red);
        Assert.Contains("F7", o.hata);
        Assert.Contains(o.Ad, o.hata);
        Assert.Equal(string.Empty, o.oteki);
        Assert.Equal(Key.F8, o.kalan);
        Assert.True(o.dosya is null or "\"0:F7,0:F8,0:F9,0:F10,0:F11,0:F6\"", o.dosya);
        Assert.True(o.kabul);
        Assert.Equal(string.Empty, o.Sonra);
        Assert.Equal(Key.F4, o.Yeni);
    }

    [Fact]
    public void SisteminReddettigiTusSatirdaSoylenirEskiKayitGeriGelir()
    {
        using var ayar = new KaydediciAyarTests.OzelAyar();
        var o = AppHost.Run(() =>
        {
            Strings.Use("en");
            var view = new RecorderView(ayar.Yol);
            var kanca = new SahteKanca();
            kanca.Dolu.Add((Key.F4, KeyModifiers.None));
            view.GlobalHotkeys = kanca;
            view.ActivateHotkeys();
            var red = view.AssignHotkey(HotkeyAction.Stop, Key.F4, KeyModifiers.None);
            var hata = view.HotkeyError(HotkeyAction.Stop);
            var kalan = Bag(view, HotkeyAction.Stop).Key;
            var kayitli = kanca.Kayitli!.First(b => b.Action == HotkeyAction.Stop).Key;
            var dosya = KaydediciAyarTests.DosyadakiDeger(ayar.Yol, "hotkeys");
            var kabul = view.AssignHotkey(HotkeyAction.Stop, Key.F5, KeyModifiers.None);
            var sonra = view.HotkeyError(HotkeyAction.Stop);
            var yeniKayit = kanca.Kayitli!.First(b => b.Action == HotkeyAction.Stop).Key;
            view.DeactivateHotkeys();
            return (red, hata, kalan, kayitli, dosya, kabul, sonra, yeniKayit);
        });

        Assert.False(o.red);
        Assert.Contains("F4", o.hata);
        Assert.Equal(Key.F8, o.kalan);
        Assert.Equal(Key.F8, o.kayitli);
        Assert.True(o.dosya is null or "\"0:F7,0:F8,0:F9,0:F10,0:F11,0:F6\"", o.dosya);
        Assert.True(o.kabul);
        Assert.Equal(string.Empty, o.sonra);
        Assert.Equal(Key.F5, o.yeniKayit);
    }

    [Fact]
    public void KayitAcilincaReddedilenAtamaKendiSatirindaSoylenir()
    {
        using var ayar = new KaydediciAyarTests.OzelAyar();
        var (once, stop, toggle) = AppHost.Run(() =>
        {
            Strings.Use("en");
            var view = new RecorderView(ayar.Yol);
            var kanca = new SahteKanca();
            kanca.Dolu.Add((Key.F4, KeyModifiers.None));
            view.GlobalHotkeys = kanca;
            view.AssignHotkey(HotkeyAction.Stop, Key.F4, KeyModifiers.None);
            var o = view.HotkeyError(HotkeyAction.Stop);
            view.ActivateHotkeys();
            var s = view.HotkeyError(HotkeyAction.Stop);
            var t = view.HotkeyError(HotkeyAction.Toggle);
            view.DeactivateHotkeys();
            return (o, s, t);
        });

        Assert.Equal(string.Empty, once);
        Assert.Contains("F4", stop);
        Assert.Equal(string.Empty, toggle);
    }

    [Fact]
    public void AtananTusEylemiTetiklerEskiTusTetiklemez()
    {
        using var ayar = new KaydediciAyarTests.OzelAyar();
        var o = AppHost.Run(() =>
        {
            var view = new RecorderView(ayar.Yol);
            var kanca = new SahteKanca();
            view.GlobalHotkeys = kanca;
            view.ActivateHotkeys();
            view.AssignHotkey(HotkeyAction.Frame, Key.K, KeyModifiers.Control);
            var eskiBasildi = kanca.Bas(Key.F9);
            var eskiSonrasi = view.FrameHiddenByUser;
            var yeniBasildi = kanca.Bas(Key.K, KeyModifiers.Control);
            var yeniSonrasi = view.FrameHiddenByUser;
            var pencereEski = Bas(view, Key.F9);
            var pencereEskiSonrasi = view.FrameHiddenByUser;
            var pencereYeni = Bas(view, Key.K, KeyModifiers.Control);
            var pencereYeniSonrasi = view.FrameHiddenByUser;
            view.DeactivateHotkeys();
            return (eskiBasildi, eskiSonrasi, yeniBasildi, yeniSonrasi, pencereEski, pencereEskiSonrasi, pencereYeni, pencereYeniSonrasi,
                Eski: RecorderHotkeys.ActionOf(view.Hotkeys, Key.F9, KeyModifiers.None),
                Yeni: RecorderHotkeys.ActionOf(view.Hotkeys, Key.K, KeyModifiers.Control));
        });

        Assert.False(o.eskiBasildi);
        Assert.False(o.eskiSonrasi);
        Assert.True(o.yeniBasildi);
        Assert.True(o.yeniSonrasi);
        Assert.False(o.pencereEski);
        Assert.True(o.pencereEskiSonrasi);
        Assert.True(o.pencereYeni);
        Assert.False(o.pencereYeniSonrasi);
        Assert.Null(o.Eski);
        Assert.Equal(HotkeyAction.Frame, o.Yeni);
    }

    [Fact]
    public void KutuTusuYakalarEscVazgecerDegistiriciTekBasinaAtanmaz()
    {
        using var ayar = new KaydediciAyarTests.OzelAyar();
        var o = AppHost.Run(() =>
        {
            Strings.Use("en");
            var view = new RecorderView(ayar.Yol);
            var pencere = SatirPenceresi(view);
            try
            {
                var kutu = view.HotkeyBox(HotkeyAction.Frame);
                var ilkMetin = kutu.Text;
                var sifirlamaKapali = !view.HotkeyReset(HotkeyAction.Frame).IsEnabled;

                Tikla(kutu);
                var dinliyor = ReferenceEquals(ShortcutKeyButton.Listening, kutu);
                Bas(kutu, Key.LeftCtrl, KeyModifiers.Control);
                var degistiricidenSonraDinliyor = ReferenceEquals(ShortcutKeyButton.Listening, kutu);
                Bas(kutu, Key.Escape);
                var escSonrasiDinliyor = ShortcutKeyButton.IsListening;
                var escSonrasi = Bag(view, HotkeyAction.Frame).Key;
                var escHata = view.HotkeyError(HotkeyAction.Frame);

                kutu = view.HotkeyBox(HotkeyAction.Frame);
                Tikla(kutu);
                Bas(kutu, Key.K, KeyModifiers.Control);
                var atanan = Bag(view, HotkeyAction.Frame);
                var yeniMetin = view.HotkeyBox(HotkeyAction.Frame).Text;
                var sifirlamaAcik = view.HotkeyReset(HotkeyAction.Frame).IsEnabled;

                kutu = view.HotkeyBox(HotkeyAction.Frame);
                Tikla(kutu);
                Bas(kutu, Key.J);
                var ciplakSonrasi = Bag(view, HotkeyAction.Frame).Key;
                var ciplakHata = view.HotkeyError(HotkeyAction.Frame);

                Tikla(view.HotkeyReset(HotkeyAction.Frame));
                var sifirlanan = Bag(view, HotkeyAction.Frame);
                return (ilkMetin, sifirlamaKapali, dinliyor, degistiricidenSonraDinliyor, escSonrasiDinliyor, escSonrasi, escHata, atanan, yeniMetin,
                    sifirlamaAcik, ciplakSonrasi, ciplakHata, sifirlanan, SonKapali: !view.HotkeyReset(HotkeyAction.Frame).IsEnabled,
                    SonHata: view.HotkeyError(HotkeyAction.Frame),
                    Beklenen: VidShrink.App.LanguageCatalog.Display(Strings.Get("recorder.hotkeys.unsupported")));
            }
            finally
            {
                pencere.Content = null;
                pencere.Close();
            }
        });

        Assert.Contains("F9", o.ilkMetin);
        Assert.True(o.sifirlamaKapali);
        Assert.True(o.dinliyor);
        Assert.True(o.degistiricidenSonraDinliyor);
        Assert.False(o.escSonrasiDinliyor);
        Assert.Equal(Key.F9, o.escSonrasi);
        Assert.Equal(string.Empty, o.escHata);
        Assert.Equal(Key.K, o.atanan.Key);
        Assert.Equal(KeyModifiers.Control, o.atanan.Modifiers);
        Assert.Contains("K", o.yeniMetin);
        Assert.True(o.sifirlamaAcik);
        Assert.Equal(Key.K, o.ciplakSonrasi);
        Assert.Equal(o.Beklenen, o.ciplakHata);
        Assert.Equal(Key.F9, o.sifirlanan.Key);
        Assert.Equal(KeyModifiers.None, o.sifirlanan.Modifiers);
        Assert.True(o.SonKapali);
        Assert.Equal(string.Empty, o.SonHata);
    }

    [Fact]
    public void YakalamaAcikkenGenelKayitBirakilirKapaninceYeniAtamaylaDoner()
    {
        using var ayar = new KaydediciAyarTests.OzelAyar();
        var o = AppHost.Run(() =>
        {
            var view = new RecorderView(ayar.Yol);
            var pencere = new Window { Width = 1536, Height = 832, Content = view };
            pencere.Show();
            Dispatcher.UIThread.RunJobs();
            try
            {
                var kanca = new SahteKanca();
                view.GlobalHotkeys = kanca;
                view.ActivateHotkeys();
                var acilir = (Flyout)view.BtnHotkeys.Flyout!;
                acilir.ShowAt(view.BtnHotkeys);
                Dispatcher.UIThread.RunJobs();
                var acikken = kanca.Kayitli is null;
                var kabul = view.AssignHotkey(HotkeyAction.Frame, Key.K, KeyModifiers.Control);
                var atamadanSonra = kanca.Kayitli is null;
                acilir.Hide();
                Dispatcher.UIThread.RunJobs();
                var donen = kanca.Kayitli?.FirstOrDefault(b => b.Action == HotkeyAction.Frame)?.Key;
                view.DeactivateHotkeys();
                return (acikken, kabul, atamadanSonra, donen, Son: kanca.Kayitli is null);
            }
            finally
            {
                pencere.Content = null;
                pencere.Close();
            }
        });

        Assert.True(o.acikken);
        Assert.True(o.kabul);
        Assert.True(o.atamadanSonra);
        Assert.Equal(Key.K, o.donen);
        Assert.True(o.Son);
    }

    [Fact]
    public void IpuclariTusuAtamadanOkur()
    {
        using var ayar = new KaydediciAyarTests.OzelAyar();
        var o = AppHost.Run(() =>
        {
            Strings.Use("en");
            var view = new RecorderView(ayar.Yol);
            var bolge = new SahteBolge();
            view.RegionEditor = bolge;
            var tamponOnce = ToolTip.GetTip(view.BtnReplay) as string ?? string.Empty;
            var bolgeOnce = bolge.Tus;
            view.AssignHotkey(HotkeyAction.ReplaySave, Key.F4, KeyModifiers.None);
            view.AssignHotkey(HotkeyAction.Frame, Key.F3, KeyModifiers.None);
            var tampon = ToolTip.GetTip(view.BtnReplay) as string ?? string.Empty;
            var kutu = ToolTip.GetTip(view.CmbReplaySeconds) as string ?? string.Empty;
            var duzenleyici = new RecorderRegionEditor();
            var varsayilan = duzenleyici.HideText;
            duzenleyici.SetHideKey(bolge.Tus);
            return (tamponOnce, bolgeOnce, tampon, kutu, Bolge: bolge.Tus, varsayilan, Gizle: duzenleyici.HideText);
        });

        Assert.Contains("F11", o.tamponOnce);
        Assert.Equal("F9", o.bolgeOnce);
        Assert.Contains("F4", o.tampon);
        Assert.DoesNotContain("F11", o.tampon);
        Assert.Equal(o.tampon, o.kutu);
        Assert.Equal("F3", o.Bolge);
        Assert.Contains("F9", o.varsayilan);
        Assert.Contains("F3", o.Gizle);
        Assert.DoesNotContain("F9", o.Gizle);
    }

    [Fact]
    public void YazimGidipAyniDoner()
    {
        var baglar = new[]
        {
            RecorderHotkeys.Bind(HotkeyAction.Toggle, Key.F1, KeyModifiers.None)!,
            RecorderHotkeys.Bind(HotkeyAction.Stop, Key.D5, KeyModifiers.Control | KeyModifiers.Alt)!,
            RecorderHotkeys.Bind(HotkeyAction.Frame, Key.F9, KeyModifiers.Shift)!,
            RecorderHotkeys.Bind(HotkeyAction.Discard, Key.NumPad1, KeyModifiers.Control | KeyModifiers.Shift)!,
            RecorderHotkeys.Bind(HotkeyAction.ReplaySave, Key.Home, KeyModifiers.Alt)!,
            RecorderHotkeys.Bind(HotkeyAction.Chapter, Key.K, KeyModifiers.Control)!
        };

        var yazim = RecorderHotkeys.Write(baglar);
        var okunan = RecorderHotkeys.Read(yazim);

        Assert.Equal("0:F1,3:D5,4:F9,6:NumPad1,1:Home,2:K", yazim);
        Assert.NotNull(okunan);
        Assert.Equal<HotkeyBinding>(baglar, okunan!);
        Assert.Equal("0:F7,0:F8,0:F9,0:F10,0:F11,0:F6", RecorderHotkeys.Write(RecorderHotkeys.All));
        Assert.Equal<HotkeyBinding>(RecorderHotkeys.All, RecorderHotkeys.Read("0:F7,0:F8,0:F9,0:F10,0:F11")!);
        Assert.Null(RecorderHotkeys.Read("0:F7,0:F6,0:F9,0:F10,0:F11"));
        Assert.Equal<HotkeyBinding>(RecorderHotkeys.All, RecorderHotkeys.Read(RecorderHotkeys.Write(RecorderHotkeys.All))!);
        Assert.Null(RecorderHotkeys.Read(null));
        Assert.Null(RecorderHotkeys.Read("0:F7,0:F8,0:F9,0:F10,0:F11,0:F6,0:F12"));
        Assert.Null(RecorderHotkeys.Read("0:F7,0:F8,0:F9,0:F10,8:F11"));
        Assert.Null(RecorderHotkeys.Read("0:F7,0:F8,0:F9,0:F10,0:999"));
    }

    [Theory]
    [InlineData(Key.F5, KeyModifiers.None, 0x74u)]
    [InlineData(Key.F24, KeyModifiers.Shift, 0x87u)]
    [InlineData(Key.K, KeyModifiers.Control, 0x4Bu)]
    [InlineData(Key.D0, KeyModifiers.Alt, 0x30u)]
    [InlineData(Key.NumPad9, KeyModifiers.Control | KeyModifiers.Shift, 0x69u)]
    [InlineData(Key.Pause, KeyModifiers.Alt, 0x13u)]
    public void AtanabilirTusSanalKodunuAlir(Key tus, KeyModifiers ek, uint kod)
    {
        var bag = RecorderHotkeys.Bind(HotkeyAction.Stop, tus, ek);

        Assert.NotNull(bag);
        Assert.Equal(kod, bag!.VirtualKey);
        Assert.Equal(ek, bag.Modifiers);
    }

    [Theory]
    [InlineData(Key.K, KeyModifiers.None)]
    [InlineData(Key.K, KeyModifiers.Shift)]
    [InlineData(Key.D1, KeyModifiers.None)]
    [InlineData(Key.Space, KeyModifiers.Control)]
    [InlineData(Key.Escape, KeyModifiers.None)]
    [InlineData(Key.LeftCtrl, KeyModifiers.Control)]
    [InlineData(Key.LeftAlt, KeyModifiers.Alt)]
    [InlineData(Key.None, KeyModifiers.Control)]
    public void AtanamayanTusBagVermez(Key tus, KeyModifiers ek)
        => Assert.Null(RecorderHotkeys.Bind(HotkeyAction.Stop, tus, ek));

    [Fact]
    public void DegistiricilerWin32BayraginaCevrilir()
    {
        Assert.Equal(0u, Win32GlobalHotkeys.Flags(KeyModifiers.None));
        Assert.Equal(1u, Win32GlobalHotkeys.Flags(KeyModifiers.Alt));
        Assert.Equal(2u, Win32GlobalHotkeys.Flags(KeyModifiers.Control));
        Assert.Equal(4u, Win32GlobalHotkeys.Flags(KeyModifiers.Shift));
        Assert.Equal(7u, Win32GlobalHotkeys.Flags(KeyModifiers.Alt | KeyModifiers.Control | KeyModifiers.Shift));
        Assert.Equal(0u, Win32GlobalHotkeys.Flags(KeyModifiers.Meta));
    }

    [Fact]
    public void KisayolMetinleriButunDillerdeVeTusAdiSabitDegil()
    {
        foreach (var dil in Locales.Languages)
        {
            var metin = Locales.Values(dil);
            foreach (var anahtar in YeniAnahtarlar)
                Assert.False(string.IsNullOrWhiteSpace(metin.GetValueOrDefault(anahtar)), dil + " " + anahtar);

            Assert.Contains("{0}", metin["recorder.hotkeys.duplicate"]);
            Assert.Contains("{1}", metin["recorder.hotkeys.duplicate"]);
            Assert.Contains("{0}", metin["recorder.hotkeys.rejected"]);
            Assert.Contains("{0}", metin["recorder.replay.hint"]);
            Assert.Contains("{1}", metin["recorder.replay.running"]);
            Assert.Contains("{0}", metin["recorder.region.hide"]);
            Assert.DoesNotContain("F11", metin["recorder.replay.hint"]);
            Assert.DoesNotContain("F11", metin["recorder.replay.running"]);
            Assert.DoesNotContain("F9", metin["recorder.region.hide"]);
        }
    }
}
