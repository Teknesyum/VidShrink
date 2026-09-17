using System.Diagnostics;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VidShrink.App;
using VidShrink.Core;

namespace VidShrink.Tests;

/// <summary>
/// Ayarlar sekmesinde iki seçenekli açılır liste kalmadı: çıktı klasörü, ffmpeg yolu ve paylaşım hedefi radyo
/// şeridi. Seçim kaydedilir, geri yüklenir, seçici satırını açıp kapatır.
/// </summary>
public sealed class AyarRadyoSeridiTests
{
    private static string SettingsFile()
    {
        var folder = Path.Combine(TestPaths.OutputRoot, "ayar-radyo");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "settings-" + Guid.NewGuid().ToString("N") + ".json");
    }

    private const BindingFlags Her = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private static readonly object Fare = Yeni(typeof(MouseDevice), new Avalonia.Input.Pointer(Avalonia.Input.Pointer.GetNextFreeId(), PointerType.Mouse, true));

    private static object Yeni(Type tur, params object?[] args)
        => Activator.CreateInstance(tur, Her, null, args, null)!;

    private static void Fareyle(TopLevel top, RawPointerEventType tur, Point nokta, RawInputModifiers tuslar)
    {
        var impl = typeof(TopLevel).GetProperty("PlatformImpl", Her)!.GetValue(top)!;
        var kok = (IInputRoot)typeof(TopLevel).GetProperty("InputRoot", Her)!.GetValue(top)!;
        var giris = (Action<RawInputEventArgs>)impl.GetType().GetInterfaces()
            .Select(i => i.GetProperty("Input", Her)).First(p => p is not null)!.GetValue(impl)!;
        giris((RawInputEventArgs)Yeni(typeof(RawPointerEventArgs), Fare, (ulong)Environment.TickCount64, kok, tur, nokta, tuslar));
    }

    private static void Dongu(Func<bool> bitti, double saniye)
    {
        var saat = Stopwatch.StartNew();
        while (!bitti() && saat.Elapsed.TotalSeconds < saniye)
        {
            using var dilim = new CancellationTokenSource(TimeSpan.FromMilliseconds(2));
            Dispatcher.UIThread.MainLoop(dilim.Token);
        }
    }

    internal static List<string> IkiSecenekliAcilirListeler(Control kapsam) =>
        kapsam.GetLogicalDescendants().OfType<ComboBox>()
            .Where(box => box.ItemCount <= 2)
            .Select(box => $"{box.Name ?? box.GetType().Name} {box.ItemCount} secenek")
            .ToList();

    /// <summary>
    /// Ayarlar sekmesinin tamamı taranır, yalnız genel panel değil: paylaşım hedefi de artık
    /// radyo şeridi. Negatif kontrol: paylaşım paneline eklenen iki seçenekli bir liste
    /// aynı taramada yakalanır.
    /// </summary>
    [Fact]
    public void AyarlarSekmesindeIkiSecenekliAcilirListeKalmaz()
    {
        var sonuc = AppHost.Run(() =>
        {
            var window = new MainWindow { SettingsPathOverride = SettingsFile() };
            try
            {
                window.RestoreSettingsForTest(new UpdateSettings());
                var kutular = window.PageSettings.GetLogicalDescendants().OfType<ComboBox>().Count();
                var temiz = IkiSecenekliAcilirListeler(window.PageSettings);
                var panel = (Panel)window.SharePanel.Child!;
                panel.Children.Add(new ComboBox { Name = "SahteIkili", ItemsSource = new[] { "a", "b" } });
                var kirli = IkiSecenekliAcilirListeler(window.PageSettings);
                return (kutular, temiz, kirli, Radyolar: window.ShareTargetRadios.Count);
            }
            finally { window.Close(); }
        });

        Assert.True(sonuc.kutular >= 3, $"taramada yalniz {sonuc.kutular} acilir liste goruldu");
        Assert.Empty(sonuc.temiz);
        Assert.Contains(sonuc.kirli, satir => satir.StartsWith("SahteIkili 2", StringComparison.Ordinal));
        Assert.Equal(2, sonuc.Radyolar);
    }

    /// <summary>
    /// Paylaşım hedefi ham fare tıkıyla seçilir: süre satırı sabit metne döner, seçim ayar
    /// dosyasına yazılır ve yeni pencerede aynı radyo işaretli gelir.
    /// </summary>
    [Fact]
    public void PaylasimHedefiHamTiklaSecilirKaydedilirVeGeriYuklenir()
    {
        var file = SettingsFile();
        try
        {
            var sonuc = AppHost.Run(() =>
            {
                var window = new MainWindow { SettingsPathOverride = file, Width = 1280, Height = 900 };
                int once, sonra;
                bool sabitGorunur, listeGizli;
                string ad, teshis;
                try
                {
                    window.Show();
                    Dongu(() => window.IsLoaded && window.ShareTargetRadios.Count == 2, 10);
                    Dongu(() => false, 1);
                    window.Tabs.SelectedItem = window.TabSettings;
                    Dongu(() => window.PageSettings.IsEffectivelyVisible, 5);
                    var radyo = window.ShareTargetRadios[1];
                    ad = radyo.Content?.ToString() ?? "";
                    var icerik = (Visual)window.PageSettings.Content!;
                    Dongu(() => radyo.TranslatePoint(default, icerik) is { Y: > 0 }, 5);
                    var yer = radyo.TranslatePoint(default, icerik)!.Value;
                    window.PageSettings.Offset = new Vector(0, Math.Max(0, yer.Y - window.PageSettings.Viewport.Height / 2));
                    Dongu(() => false, 0.5);
                    once = window.ShareTargetIndex;
                    var nokta = radyo.TranslatePoint(new Point(radyo.Bounds.Width / 2, radyo.Bounds.Height / 2), window)!.Value;
                    teshis = $"nokta {nokta} sinir {radyo.Bounds} gorunur {radyo.IsEffectivelyVisible} isabet {window.InputHitTest(nokta)?.GetType().Name} pencere {window.Bounds} ofset {window.PageSettings.Offset} kapsam {window.PageSettings.Extent} gorus {window.PageSettings.Viewport} sb {window.PageSettings.Bounds} sayfa {window.Tabs.SelectedIndex}";
                    Fareyle(window, RawPointerEventType.Move, nokta, RawInputModifiers.None);
                    Dongu(() => false, 0.05);
                    Fareyle(window, RawPointerEventType.LeftButtonDown, nokta, RawInputModifiers.LeftMouseButton);
                    Dongu(() => false, 0.05);
                    Fareyle(window, RawPointerEventType.LeftButtonUp, nokta, RawInputModifiers.None);
                    Dongu(() => window.ShareTargetIndex == 1, 2);
                    sonra = window.ShareTargetIndex;
                    sabitGorunur = window.TxtShareRetentionFixed.IsVisible;
                    listeGizli = !window.CmbShareRetention.IsVisible;
                }
                finally { window.Close(); }

                var kayit = UpdateSettings.Load(file).ShareTarget;
                var ikinci = new MainWindow { SettingsPathOverride = file };
                bool geriGeldi;
                try
                {
                    ikinci.RestoreSettingsForTest(UpdateSettings.Load(file));
                    geriGeldi = ikinci.ShareTargetRadios[1].IsChecked == true && ikinci.ShareTargetRadios[0].IsChecked != true;
                }
                finally { ikinci.Close(); }
                return (ad, once, sonra, sabitGorunur, listeGizli, kayit, geriGeldi, teshis);
            });

            Assert.Equal("uguu.se", sonuc.ad);
            Assert.Equal(0, sonuc.once);
            Assert.True(sonuc.sonra == 1, sonuc.teshis);
            Assert.True(sonuc.sabitGorunur, "sabit sureli hedefte sure metni gorunmedi");
            Assert.True(sonuc.listeGizli, "sabit sureli hedefte sure listesi gizlenmedi");
            Assert.Equal(1, sonuc.kayit);
            Assert.True(sonuc.geriGeldi, "kaydedilen hedef yeni pencerede isaretli gelmedi");
        }
        finally { if (File.Exists(file)) File.Delete(file); }
    }
    [Fact]
    public void DilVeTemaYanYanaDurur()
    {
        var columns = AppHost.Run(() =>
        {
            var window = new MainWindow { SettingsPathOverride = SettingsFile() };
            try
            {
                var row = window.LanguageThemeRow;
                int ColumnOf(Control control) => Grid.GetColumn((Control)control.GetLogicalParent()!);
                return (Parent: window.CmbLanguage.GetLogicalParent()!.GetLogicalParent() == row
                               && window.CmbTheme.GetLogicalParent()!.GetLogicalParent() == row,
                        Language: ColumnOf(window.CmbLanguage), Theme: ColumnOf(window.CmbTheme));
            }
            finally { window.Close(); }
        });

        Assert.True(columns.Parent, "dil ve tema ayni satirda degil");
        Assert.NotEqual(columns.Language, columns.Theme);
    }

    [Fact]
    public void RadyoSecimiSeciciyiAcarKaydedilirVeGeriYuklenir()
    {
        var file = SettingsFile();
        try
        {
            var result = AppHost.Run(() =>
            {
                var first = new MainWindow { SettingsPathOverride = file };
                bool outputRowShown, ffmpegRowShown, ffmpegErrorShown, outputRowHidden;
                try
                {
                    first.RbOutputFixed.IsChecked = true;
                    outputRowShown = first.OutputFolderPickerRow.IsVisible;
                    first.RbFfmpegManual.IsChecked = true;
                    ffmpegRowShown = first.FfmpegPathPickerRow.IsVisible;
                    ffmpegErrorShown = first.TxtFfmpegPathError.IsVisible;
                }
                finally { first.Close(); }

                var saved = AppSettings.Load(file);

                var second = new MainWindow { SettingsPathOverride = file };
                bool fixedRestored, manualRestored;
                try
                {
                    second.RestoreAppSettingsForTest(saved);
                    fixedRestored = second.RbOutputFixed.IsChecked == true && second.RbOutputBesideSource.IsChecked != true;
                    manualRestored = second.RbFfmpegManual.IsChecked == true && second.RbFfmpegAuto.IsChecked != true;
                    second.RbOutputBesideSource.IsChecked = true;
                    outputRowHidden = !second.OutputFolderPickerRow.IsVisible;
                }
                finally { second.Close(); }

                return (outputRowShown, ffmpegRowShown, ffmpegErrorShown, saved.OutputFolderMode, saved.FfmpegPathMode,
                    fixedRestored, manualRestored, outputRowHidden, AfterBeside: AppSettings.Load(file).OutputFolderMode);
            });

            Assert.True(result.outputRowShown, "sabit klasor secilince klasor secici acilmadi");
            Assert.True(result.ffmpegRowShown, "elle secilince ffmpeg yolu secici acilmadi");
            Assert.True(result.ffmpegErrorShown, "bos elle yol hata gostermedi");
            Assert.Equal(1, result.OutputFolderMode);
            Assert.Equal(1, result.FfmpegPathMode);
            Assert.True(result.fixedRestored, "sabit klasor radyosu geri yuklenmedi");
            Assert.True(result.manualRestored, "elle ffmpeg radyosu geri yuklenmedi");
            Assert.True(result.outputRowHidden, "kaynagin yani secilince klasor secici kapanmadi");
            Assert.Equal(0, result.AfterBeside);
        }
        finally { if (File.Exists(file)) File.Delete(file); }
    }
}
