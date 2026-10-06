using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using VidShrink.App.Recorder;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Kaydedicinin mini şeridi kayda girmez. Ölçü pencerenin kendi bildirdiği sonuç
/// (<c>CaptureExcluded</c>) ve Windows'un geri okuduğu değer (<c>GetWindowDisplayAffinity</c>,
/// 0x11); negatif kontrol aynı pencerenin <c>ExcludeFromCapture=false</c> kolu, orada 0 okunur.
/// Ekran yakalaması başlatılmaz: çıkarılan pencerenin kayıttaki pikseli burada ölçülmüyor.
/// </summary>
public sealed class KaydediciMiniYakalamaTests
{
    private const uint Haric = 0x11;

    [DllImport("user32.dll")]
    private static extern bool GetWindowDisplayAffinity(IntPtr hwnd, out uint affinity);

    private static uint Oku(TopLevel? yuzey)
    {
        if (yuzey?.TryGetPlatformHandle() is not { } tutamac) return uint.MaxValue;
        return GetWindowDisplayAffinity(tutamac.Handle, out var deger) ? deger : uint.MaxValue;
    }

    private static (bool bildirilen, uint okunan, uint gizleGoster) Serit(bool haric)
    {
        var mini = new RecorderMini { ExcludeFromCapture = haric };
        try
        {
            mini.Show();
            var bildirilen = mini.CaptureExcluded;
            var okunan = OperatingSystem.IsWindows() ? Oku(mini) : 0;
            mini.Hide();
            mini.Show();
            Dispatcher.UIThread.RunJobs();
            return (bildirilen, okunan, OperatingSystem.IsWindows() ? Oku(mini) : 0);
        }
        finally { mini.Close(); }
    }

    /// <summary>
    /// Şerit açıldığı anda yakalamadan çıkar ve gizlenip yeniden gösterilince (şeritten bölge
    /// çizimi) öyle kalır. Windows dışında çağrı yapılmaz, şerit yine açılır.
    /// </summary>
    [Fact]
    public void MiniSeritYakalamadanCikarilir()
    {
        var (haric, kontrol) = AppHost.Run(() => (Serit(true), Serit(false)));

        Assert.Equal((false, 0u, 0u), kontrol);
        if (!OperatingSystem.IsWindows())
        {
            Assert.False(haric.bildirilen);
            return;
        }

        Assert.True(CaptureAffinity.Supported);
        Assert.Equal((true, Haric, Haric), haric);
    }

    private static (bool bildirilen, uint okunan, bool ayriPencere) Acilir(bool haric)
    {
        var mini = new RecorderMini { ExcludeFromCapture = haric };
        mini.Classes.Add("reduced-motion");
        var acilir = (Flyout)mini.BtnOptions.Flyout!;
        try
        {
            mini.Show();
            acilir.ShowAt(mini.BtnOptions);
            Dispatcher.UIThread.RunJobs();
            var kok = TopLevel.GetTopLevel(mini.PanelOptions);
            return (mini.OptionsCaptureExcluded, OperatingSystem.IsWindows() ? Oku(kok) : 0, kok is not null && !ReferenceEquals(kok, mini));
        }
        finally
        {
            acilir.Hide();
            mini.Close();
        }
    }

    /// <summary>
    /// Seçenek açılırı şeridin dışında ayrı bir pencere: şerit kayıttan çıkıp açılır kalırsa
    /// kayıtta havada duran bir menü görünür. Açılır da aynı çağrıdan geçer.
    /// </summary>
    [Fact]
    public void SecenekAcilirPenceresiDeYakalamadanCikarilir()
    {
        var (haric, kontrol) = AppHost.Run(() => (Acilir(true), Acilir(false)));

        Assert.False(kontrol.bildirilen);
        Assert.Equal(0u, kontrol.okunan);
        if (!OperatingSystem.IsWindows())
        {
            Assert.False(haric.bildirilen);
            return;
        }

        Assert.True(haric.ayriPencere);
        Assert.True(haric.bildirilen);
        Assert.Equal(Haric, haric.okunan);
    }

    /// <summary>
    /// "Şerit kayda girecek" uyarısı yalnız dışarısı olmayan kadrajda ve şerit çıkarılamadığında
    /// verilir. Varsayılan hedef bütün ekran: şerit çıkarıldıysa görünüm uyarmaz.
    /// </summary>
    [Fact]
    public void CikarilanSeritIcinKadrajUyarisiVerilmez()
    {
        var bolge = new PixelRect(100, 50, 640, 360);
        Assert.True(RecorderView.InFrame(null, captureExcluded: false));
        Assert.False(RecorderView.InFrame(null, captureExcluded: true));
        Assert.False(RecorderView.InFrame(bolge, captureExcluded: false));
        Assert.False(RecorderView.InFrame(bolge, captureExcluded: true));

        var olcu = KaydediciAyarTests.AyarDosyasiyla(ayarYolu => AppHost.Run(() =>
        {
            var view = new RecorderView(ayarYolu) { SkipAutoMeasure = true };
            view.ShrinkToMini();
            try { return (haric: view.Mini!.CaptureExcluded, uyari: view.MiniInFrame); }
            finally { view.ExpandFromMini(); }
        }));

        Assert.Equal(OperatingSystem.IsWindows(), olcu.haric);
        Assert.Equal(!olcu.haric, olcu.uyari);
    }

    /// <summary>
    /// Çerçeve ve bölge düzenleyicisi ortak çağrıya taşındıktan sonra da 0x11 okunur; çerçevenin
    /// <c>ExcludeFromCapture=false</c> kolunda 0.
    /// </summary>
    [Fact]
    public void CerceveVeDuzenleyiciOrtakCagridanGecer()
    {
        if (!OperatingSystem.IsWindows()) return;

        var olcu = AppHost.Run(() =>
        {
            var bolge = new PixelRect(300, 300, 640, 360);
            (bool, uint) Cerceve(bool haric)
            {
                var cerceve = new RecorderFrame { ExcludeFromCapture = haric };
                try
                {
                    cerceve.Place(bolge);
                    cerceve.Show();
                    return (cerceve.CaptureExcluded, Oku(cerceve));
                }
                finally { cerceve.Close(); }
            }

            var duzenleyici = new RecorderRegionEditor(bolge, null);
            try
            {
                duzenleyici.Show();
                return (haric: Cerceve(true), kontrol: Cerceve(false), duzenleyici: (duzenleyici.CaptureExcluded, Oku(duzenleyici)));
            }
            finally { duzenleyici.Close(); }
        });

        Assert.Equal((true, Haric), olcu.haric);
        Assert.Equal((false, 0u), olcu.kontrol);
        Assert.Equal((true, Haric), olcu.duzenleyici);
    }

    /// <summary>
    /// Çağrı tek yerde: üç pencere <c>CaptureAffinity.Exclude</c>'dan geçer, P/Invoke bildirimi
    /// yalnız <c>CaptureAffinity.cs</c>'te durur. Kayda girmesi istenen bindirmeler (tıklama
    /// halkası, tuş yazısı, büyüteç) çağrıyı yapmaz.
    /// </summary>
    [Fact]
    public void CagriTekYerdeVeBindirmelerDisinda()
    {
        var klasor = Path.Combine(TipSources.Root, "src", "VidShrink.App", "Recorder");
        var kaynaklar = Directory.GetFiles(klasor, "*.cs").ToDictionary(yol => Path.GetFileName(yol)!, File.ReadAllText);

        Assert.Equal(new[] { "CaptureAffinity.cs" },
            kaynaklar.Where(k => k.Value.Contains("extern bool SetWindowDisplayAffinity")).Select(k => k.Key).ToArray());
        Assert.Equal(new[] { "RecorderFrame.axaml.cs", "RecorderMini.axaml.cs", "RecorderRegionEditor.axaml.cs" },
            kaynaklar.Where(k => k.Value.Contains("CaptureAffinity.Exclude(")).Select(k => k.Key).OrderBy(a => a, StringComparer.Ordinal).ToArray());
    }
}
