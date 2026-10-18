using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VidShrink.App;
using VidShrink.App.Editing;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using VidShrink.App.Recorder;
using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

public sealed class KabukKisayolTests
{
    private static readonly Pointer Fare = new(Pointer.GetNextFreeId(), PointerType.Mouse, true);

    private static string Kok => Path.Combine(TipSources.Root, ".calisma", "kabuk-kisayol");

    private static T Dosyayla<T>(Func<string, T> olcu)
    {
        Directory.CreateDirectory(Kok);
        var dosya = Path.Combine(Kok, Guid.NewGuid().ToString("N") + ".json");
        ShortcutBindings.UseFile(dosya);
        try
        {
            return olcu(dosya);
        }
        finally
        {
            ShortcutBindings.UseFile(null);
            if (File.Exists(dosya)) File.Delete(dosya);
        }
    }

    private static T Pencereyle<T>(Func<MainWindow, T> olcu)
        => AppHost.Run(() => Dosyayla(_ =>
        {
            Strings.Use("en");
            var ayar = Path.Combine(Kok, Guid.NewGuid().ToString("N"), "settings.json");
            Directory.CreateDirectory(Path.GetDirectoryName(ayar)!);
            var pencere = new MainWindow { SettingsPathOverride = ayar, Width = 1400, Height = 900 };
            pencere.DisAcici = _ => { };
            try
            {
                pencere.Show();
                Strings.Use("en");
                Dispatcher.UIThread.RunJobs();
                return olcu(pencere);
            }
            finally
            {
                pencere.Close();
                try { Directory.Delete(Path.GetDirectoryName(ayar)!, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }));

    private static void Dongu(Func<bool> bitti, double saniye)
    {
        var saat = Stopwatch.StartNew();
        while (!bitti() && saat.Elapsed.TotalSeconds < saniye)
        {
            using var dilim = new CancellationTokenSource(TimeSpan.FromMilliseconds(5));
            Dispatcher.UIThread.MainLoop(dilim.Token);
        }
    }

    private static bool Bas(Control hedef, Key tus, KeyModifiers ek = KeyModifiers.None, bool islenmis = false)
    {
        var olay = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = tus, KeyModifiers = ek, Handled = islenmis };
        hedef.RaiseEvent(olay);
        Dispatcher.UIThread.RunJobs();
        return olay.Handled;
    }

    private static bool FareBas(Control hedef, PointerUpdateKind tur, RawInputModifiers dugme)
    {
        var olay = new PointerPressedEventArgs(hedef, Fare, hedef, new Point(2, 2), 0, new PointerPointProperties(dugme, tur), KeyModifiers.None, 1)
        {
            RoutedEvent = InputElement.PointerPressedEvent
        };
        hedef.RaiseEvent(olay);
        Dispatcher.UIThread.RunJobs();
        return olay.Handled;
    }

    [Fact]
    public void VarsayilanTuslarKomutlaraIner()
    {
        var sonuc = AppHost.Run(() => Dosyayla(_ => (
            Ctrl1: ShellKeymap.For(Key.D1, KeyModifiers.Control),
            Ctrl2: ShellKeymap.For(Key.D2, KeyModifiers.Control),
            Ctrl6: ShellKeymap.For(Key.D6, KeyModifiers.Control),
            CtrlO: ShellKeymap.For(Key.O, KeyModifiers.Control),
            CtrlF6: ShellKeymap.For(Key.F6, KeyModifiers.Control),
            CtrlShiftF6: ShellKeymap.For(Key.F6, KeyModifiers.Control | KeyModifiers.Shift),
            F1: ShellKeymap.For(Key.F1, KeyModifiers.None),
            F6: ShellKeymap.For(Key.F6, KeyModifiers.None),
            Duz2: ShellKeymap.For(Key.D2, KeyModifiers.None),
            Satir: ShellKeymap.Rows.Count)));

        Assert.Equal(ShellCommand.TabPlayer, sonuc.Ctrl1);
        Assert.Equal(ShellCommand.TabEditor, sonuc.Ctrl2);
        Assert.Equal(ShellCommand.TabSettings, sonuc.Ctrl6);
        Assert.Equal(ShellCommand.OpenFile, sonuc.CtrlO);
        Assert.Equal(ShellCommand.NextRegion, sonuc.CtrlF6);
        Assert.Equal(ShellCommand.PreviousRegion, sonuc.CtrlShiftF6);
        Assert.Equal(ShellCommand.Help, sonuc.F1);
        Assert.Equal(ShellCommand.None, sonuc.F6);
        Assert.Equal(ShellCommand.None, sonuc.Duz2);
        Assert.Equal(10, sonuc.Satir);
    }

    [Fact]
    public void VarsayilanlarOynaticiDuzenleyiciVeKaydediciyleCakismaz()
    {
        var kabuk = ShellKeymap.DefaultRows.Select(r => r.Input).ToList();
        var oynatici = Keymap.DefaultRows.Where(r => kabuk.Any(k => ShortcutBindings.Same(k, r.Input))).Select(r => ShortcutBindings.Canon(r.Input)).ToList();
        var duzenleyici = EditorKeymap.DefaultRows.Where(r => kabuk.Any(k => ShortcutBindings.Same(k, r.Input))).Select(r => ShortcutBindings.Canon(r.Input)).ToList();
        var kaydedici = RecorderHotkeys.All.Where(h => kabuk.Any(k => k.Kind == PlayerInputKind.Key && k.Key == h.Key && k.Modifiers == h.Modifiers)).Select(h => h.Action.ToString()).ToList();
        var kendi = kabuk.Select(ShortcutBindings.Canon).GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        var bolum = RecorderHotkeys.Of(RecorderHotkeys.All, HotkeyAction.Chapter);
        var bolge = ShellKeymap.DefaultRows.Where(r => r.Command is ShellCommand.NextRegion or ShellCommand.PreviousRegion).Select(r => r.Input).ToList();

        Assert.Empty(oynatici);
        Assert.Empty(duzenleyici);
        Assert.Empty(kaydedici);
        Assert.Empty(kendi);
        Assert.Equal(Key.F6, bolum.Key);
        Assert.All(bolge, g => Assert.False(g.Key == bolum.Key && g.Modifiers == bolum.Modifiers));
    }

    [Fact]
    public void KabukAtamasiTakimIcindeTusuAlirVeDosyayaYazilir()
    {
        var sonuc = AppHost.Run(() => Dosyayla(dosya =>
        {
            var yardim = ShellKeymap.DefaultRows.First(r => r.Command == ShellCommand.Help);
            var ac = ShellKeymap.DefaultRows.First(r => r.Command == ShellCommand.OpenFile);
            var calinan = ShortcutBindings.Assign(ShortcutMap.Shell, ShellKeymap.SlotId(yardim), ac.Input);
            var yazilan = File.ReadAllText(dosya);
            var ctrlO = ShellKeymap.For(Key.O, KeyModifiers.Control);
            var f1 = ShellKeymap.For(Key.F1, KeyModifiers.None);
            var oynaticiDegismedi = !ShortcutBindings.HasOverrides(ShortcutMap.Player) && !ShortcutBindings.HasOverrides(ShortcutMap.Editor);
            ShortcutBindings.UseFile(dosya);
            var geri = ShellKeymap.For(Key.O, KeyModifiers.Control);
            ShortcutBindings.Reset(ShortcutMap.Shell);
            var sifir = (ShellKeymap.For(Key.O, KeyModifiers.Control), ShellKeymap.For(Key.F1, KeyModifiers.None));
            return (calinan: calinan.Select(s => s.Id).ToList(), yazilan, ctrlO, f1, oynaticiDegismedi, geri, sifir, acId: ShellKeymap.SlotId(ac));
        }));

        Assert.Equal(new[] { sonuc.acId }, sonuc.calinan);
        Assert.Contains("\"shell\"", sonuc.yazilan);
        Assert.Equal(ShellCommand.Help, sonuc.ctrlO);
        Assert.Equal(ShellCommand.None, sonuc.f1);
        Assert.True(sonuc.oynaticiDegismedi);
        Assert.Equal(ShellCommand.Help, sonuc.geri);
        Assert.Equal((ShellCommand.OpenFile, ShellCommand.Help), sonuc.sifir);
    }

    [Fact]
    public void SekmeTuslariPencereyeIsleniyorIslenmisTusaDokunulmaz()
    {
        var sonuc = Pencereyle(pencere =>
        {
            pencere.Tabs.SelectedItem = pencere.TabShrink;
            var islendi = Bas(pencere, Key.D2, KeyModifiers.Control);
            var duzenleyici = pencere.TabEditor.IsSelected;
            Bas(pencere, Key.D6, KeyModifiers.Control);
            var ayarlar = pencere.TabSettings.IsSelected;
            var oncedenIslenmis = Bas(pencere, Key.D1, KeyModifiers.Control, islenmis: true);
            var yineAyarlar = pencere.TabSettings.IsSelected;
            var duzIki = Bas(pencere, Key.D2);
            return (islendi, duzenleyici, ayarlar, oncedenIslenmis, yineAyarlar, duzIki, hala: pencere.TabSettings.IsSelected);
        });

        Assert.True(sonuc.islendi);
        Assert.True(sonuc.duzenleyici);
        Assert.True(sonuc.ayarlar);
        Assert.True(sonuc.yineAyarlar);
        Assert.False(sonuc.duzIki);
        Assert.True(sonuc.hala);
    }

    [Fact]
    public void FareDugmesineAtananKabukKomutuCalisir()
    {
        var sonuc = Pencereyle(pencere =>
        {
            var ayarlar = ShellKeymap.DefaultRows.First(r => r.Command == ShellCommand.TabSettings);
            ShortcutBindings.Assign(ShortcutMap.Shell, ShellKeymap.SlotId(ayarlar), PlayerInput.OnPress(PlayerButton.Forward));
            pencere.Tabs.SelectedItem = pencere.TabShrink;
            var islendi = FareBas(pencere, PointerUpdateKind.XButton2Pressed, RawInputModifiers.XButton2MouseButton);
            return (islendi, secili: pencere.TabSettings.IsSelected);
        });

        Assert.True(sonuc.islendi);
        Assert.True(sonuc.secili);
    }

    [Fact]
    public void DosyaAcTusuSekmeyeGoreSeciciyiAcar()
    {
        var sonuc = Pencereyle(pencere =>
        {
            var kipler = new List<bool>();
            pencere.SourcePicker = coklu => { kipler.Add(coklu); return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>()); };
            pencere.Tabs.SelectedItem = pencere.TabSettings;
            var islendi = Bas(pencere, Key.O, KeyModifiers.Control);
            Dongu(() => kipler.Count >= 1, 10);
            var ayarlardanKucult = pencere.TabShrink.IsSelected;
            pencere.Tabs.SelectedItem = pencere.TabPlayer;
            Bas(pencere, Key.O, KeyModifiers.Control);
            Dongu(() => kipler.Count >= 2, 10);
            var oynaticidaKaldi = pencere.TabPlayer.IsSelected;
            return (islendi, ayarlardanKucult, oynaticidaKaldi, kipler: kipler.ToArray());
        });

        Assert.True(sonuc.islendi);
        Assert.True(sonuc.ayarlardanKucult);
        Assert.True(sonuc.oynaticidaKaldi);
        Assert.Equal(new[] { true, false }, sonuc.kipler);
    }

    [Fact]
    public void YardimAdresiDileVeSekmeyeGore()
    {
        Assert.Equal(Destek.DepoAdresi + "/blob/main/docs/kullanim.md", MainWindow.HelpAddress("en", false));
        Assert.Equal(Destek.DepoAdresi + "/blob/main/docs/kullanim.tr.md", MainWindow.HelpAddress("tr", false));
        Assert.Equal(Destek.DepoAdresi + "/blob/main/docs/kullanim.md#the-recorder-tab", MainWindow.HelpAddress("de", true));
        Assert.Equal(Destek.DepoAdresi + "/blob/main/docs/kullanim.tr.md#kaydedici-sekmesi", MainWindow.HelpAddress("tr", true));

        var kok = TipSources.Root;
        Assert.Contains("### The Recorder tab", File.ReadAllText(Path.Combine(kok, "docs", "kullanim.md")));
        Assert.Contains("### Kaydedici sekmesi", File.ReadAllText(Path.Combine(kok, "docs", "kullanim.tr.md")));
    }

    [Fact]
    public void F1KullanimBelgesiniDikistenAcar()
    {
        var sonuc = Pencereyle(pencere =>
        {
            var acilan = new List<string?>();
            pencere.DisAcici = adres => acilan.Add(adres);
            pencere.Tabs.SelectedItem = pencere.TabShrink;
            var islendi = Bas(pencere, Key.F1);
            pencere.Tabs.SelectedItem = pencere.TabRecorder;
            Bas(pencere, Key.F1);
            return (islendi, acilan: acilan.ToArray());
        });

        Assert.True(sonuc.islendi);
        Assert.Equal(new[] { MainWindow.HelpAddress("en", false), MainWindow.HelpAddress("en", true) }, sonuc.acilan);
    }

    [Fact]
    public void AyarAramasiBaslikCubugundaYalnizAyarlardaGorunurCtrlFOdaklar()
    {
        var sonuc = Pencereyle(pencere =>
        {
            pencere.Tabs.SelectedItem = pencere.TabSettings;
            Dispatcher.UIThread.RunJobs();
            var ayarlarda = pencere.TxtSettingsSearch.IsVisible;
            var baslikta = pencere.TitleBarRight.IsVisualAncestorOf(pencere.TxtSettingsSearch);
            var sayfadaDegil = !pencere.SettingsPage.IsVisualAncestorOf(pencere.TxtSettingsSearch);
            var tutuldu = Bas(pencere, Key.F, KeyModifiers.Control);
            var odakta = ReferenceEquals(pencere.FocusManager?.GetFocusedElement(), pencere.TxtSettingsSearch);
            pencere.Tabs.SelectedItem = pencere.TabShrink;
            Dispatcher.UIThread.RunJobs();
            var baskaSekmede = pencere.TxtSettingsSearch.IsVisible;
            var baskaCtrlF = Bas(pencere, Key.F, KeyModifiers.Control);
            return (ayarlarda, baslikta, sayfadaDegil, tutuldu, odakta, baskaSekmede, baskaCtrlF);
        });

        Assert.True(sonuc.ayarlarda);
        Assert.True(sonuc.baslikta);
        Assert.True(sonuc.sayfadaDegil);
        Assert.True(sonuc.tutuldu);
        Assert.True(sonuc.odakta);
        Assert.False(sonuc.baskaSekmede);
        Assert.False(sonuc.baskaCtrlF);
    }

    [Fact]
    public void BolgeGecisiOdagiSekmeSayfaVeBaslikArasindaDolastirir()
    {
        var sonuc = Pencereyle(pencere =>
        {
            pencere.Tabs.SelectedItem = pencere.TabSettings;
            Dispatcher.UIThread.RunJobs();
            string Bolge()
            {
                var odak = pencere.FocusManager?.GetFocusedElement() as Visual;
                if (odak is null) return "yok";
                if (ReferenceEquals(odak, pencere.TabSettings) || pencere.TabSettings.IsVisualAncestorOf(odak)) return "sekme";
                if (pencere.TitleBarRight.IsVisualAncestorOf(odak)) return "baslik";
                if (pencere.SettingsPage.IsVisualAncestorOf(odak)) return "sayfa";
                return "baska";
            }

            var adimlar = new List<string>();
            Bas(pencere, Key.F6, KeyModifiers.Control);
            adimlar.Add(Bolge());
            Bas(pencere, Key.F6, KeyModifiers.Control);
            adimlar.Add(Bolge());
            Bas(pencere, Key.F6, KeyModifiers.Control);
            adimlar.Add(Bolge());
            Bas(pencere, Key.F6, KeyModifiers.Control);
            adimlar.Add(Bolge());
            Bas(pencere, Key.F6, KeyModifiers.Control | KeyModifiers.Shift);
            adimlar.Add(Bolge());
            var duzF6 = Bas(pencere, Key.F6);
            return (adimlar: adimlar.ToArray(), duzF6);
        });

        Assert.Equal(new[] { "sekme", "sayfa", "baslik", "sekme", "baslik" }, sonuc.adimlar);
        Assert.False(sonuc.duzF6);
    }

    [Fact]
    public void AyarAramasiKartlariSuzerKisayolSatiriniAcarVeTemizleyinceGeriVerir()
    {
        var sonuc = Pencereyle(pencere =>
        {
            pencere.Tabs.SelectedItem = pencere.TabSettings;
            Dispatcher.UIThread.RunJobs();
            var oncekiGenel = pencere.GeneralSettingsPanel.IsVisible;
            var oncekiAcik = pencere.ShellShortcutsPanel.IsOpen;
            var oncekiSatir = pencere.ShellShortcutsPanel.Shown.Count;

            pencere.TxtSettingsSearch.Text = "next region";
            Dispatcher.UIThread.RunJobs();
            var aramada = (
                Genel: pencere.GeneralSettingsPanel.IsVisible,
                Sifirla: pencere.ResetSettingsPanel.IsVisible,
                Kabuk: pencere.ShellShortcutsPanel.IsVisible,
                Acik: pencere.ShellShortcutsPanel.IsOpen,
                Satir: pencere.ShellShortcutsPanel.Shown.Select(s => s.Label).ToArray(),
                Oynatici: pencere.PlayerShortcutsPanel.IsVisible,
                Bos: pencere.TxtSettingsSearchEmpty.IsVisible);

            pencere.TxtSettingsSearch.Text = "Window shortcuts";
            Dispatcher.UIThread.RunJobs();
            var baslikla = pencere.ShellShortcutsPanel.Shown.Count;

            pencere.TxtSettingsSearch.Text = "zzqxqz";
            Dispatcher.UIThread.RunJobs();
            var hicbiri = (Bos: pencere.TxtSettingsSearchEmpty.IsVisible, Gorunen: pencere.SettingsCards.Children.Count(c => c.IsVisible));

            pencere.TxtSettingsSearch.Text = "";
            Dispatcher.UIThread.RunJobs();
            var sonra = (
                Genel: pencere.GeneralSettingsPanel.IsVisible,
                Sifirla: pencere.ResetSettingsPanel.IsVisible,
                Acik: pencere.ShellShortcutsPanel.IsOpen,
                Satir: pencere.ShellShortcutsPanel.Shown.Count,
                Bos: pencere.TxtSettingsSearchEmpty.IsVisible);
            return (oncekiGenel, oncekiAcik, oncekiSatir, aramada, baslikla, hicbiri, sonra);
        });

        Assert.True(sonuc.oncekiGenel);
        Assert.False(sonuc.oncekiAcik);
        Assert.Equal(10, sonuc.oncekiSatir);
        Assert.False(sonuc.aramada.Genel);
        Assert.False(sonuc.aramada.Sifirla);
        Assert.False(sonuc.aramada.Oynatici);
        Assert.True(sonuc.aramada.Kabuk);
        Assert.True(sonuc.aramada.Acik);
        Assert.Equal(new[] { "Next region (tabs, page, title bar)" }, sonuc.aramada.Satir);
        Assert.False(sonuc.aramada.Bos);
        Assert.Equal(10, sonuc.baslikla);
        Assert.True(sonuc.hicbiri.Bos);
        Assert.Equal(0, sonuc.hicbiri.Gorunen);
        Assert.True(sonuc.sonra.Genel);
        Assert.True(sonuc.sonra.Sifirla);
        Assert.False(sonuc.sonra.Acik);
        Assert.Equal(10, sonuc.sonra.Satir);
        Assert.False(sonuc.sonra.Bos);
    }

    [Fact]
    public void KisayolTablosuTusMetnineGoreDeSuzulur()
    {
        var sonuc = AppHost.Run(() => Dosyayla(_ =>
        {
            Strings.Use("en");
            var panel = new ShellShortcutsPanel { IsOpen = true };
            var pencere = new Window { Width = 900, Height = 1400, Content = panel };
            pencere.Show();
            Dispatcher.UIThread.RunJobs();
            try
            {
                panel.Table.Filter = "F6";
                var f6 = panel.Shown.Select(s => s.Label).ToArray();
                var sayi = panel.Table.Matches("F6");
                panel.Table.Filter = "guide";
                var rehber = panel.Shown.Select(s => s.Label).ToArray();
                panel.Table.Filter = "";
                return (f6, sayi, rehber, hepsi: panel.Shown.Count);
            }
            finally
            {
                pencere.Close();
            }
        }));

        Assert.Equal(new[] { "Next region (tabs, page, title bar)", "Previous region" }, sonuc.f6);
        Assert.Equal(2, sonuc.sayi);
        Assert.Equal(new[] { "Open the user guide" }, sonuc.rehber);
        Assert.Equal(10, sonuc.hepsi);
    }
}
