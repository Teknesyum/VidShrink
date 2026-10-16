using System;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using VidShrink.App.Recorder;
using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// later.md #42, EKB-15: prompter. Model (<see cref="PrompterScroll"/>) saf ölçülür: hız basamakları, sona varınca
/// durma, sondan yeniden başlayınca başa sarma. Pencere gerçek kurulur: yakalamadan çıkarıldığı Windows'un geri
/// okuduğu değerle (0x11), negatif kontrol <c>ExcludeFromCapture=false</c> kolunda 0. Akış saatle değil
/// <c>Advance</c>'a verilen zamanla ölçülür. Kaydediciyle bağ: kayıt başlayınca akar, durunca durur, elle
/// durdurulan akışı sonraki şerit tazelemesi yeniden başlatmaz; metin ve hız ayara yazılır.
/// </summary>
public sealed class PrompterTests
{
    private const uint Haric = 0x11;

    private static readonly string UzunMetin = string.Join(Environment.NewLine, Enumerable.Range(1, 80).Select(i => $"Satır {i}: kayıt sırasında okunacak konuşma metni."));

    [DllImport("user32.dll")]
    private static extern bool GetWindowDisplayAffinity(IntPtr hwnd, out uint affinity);

    private static uint Oku(TopLevel pencere)
        => pencere.TryGetPlatformHandle() is { } tutamac && GetWindowDisplayAffinity(tutamac.Handle, out var deger) ? deger : uint.MaxValue;

    [Fact]
    public void HizBasamaklariArtarVeUclardaDurur()
    {
        Assert.True(PrompterScroll.Speeds.Zip(PrompterScroll.Speeds.Skip(1)).All(c => c.First < c.Second));
        var akis = new PrompterScroll();
        Assert.Equal(PrompterScroll.DefaultStep, akis.Step);
        while (akis.Faster()) { }
        Assert.Equal(PrompterScroll.Speeds.Length - 1, akis.Step);
        Assert.False(akis.Faster());
        while (akis.Slower()) { }
        Assert.Equal(0, akis.Step);
        Assert.False(akis.Slower());
        Assert.Equal(PrompterScroll.DefaultStep, PrompterScroll.ClampStep(-1));
        Assert.Equal(PrompterScroll.DefaultStep, PrompterScroll.ClampStep(PrompterScroll.Speeds.Length));
        Assert.Equal(4, PrompterScroll.ClampStep(4));
    }

    [Fact]
    public void AkisSonaVarincaDururSondanBasaSarar()
    {
        var akis = new PrompterScroll();
        akis.Play();
        Assert.False(akis.Playing);

        akis.Resize(1000, 400);
        Assert.Equal(600, akis.Extent);
        akis.Play();
        akis.Tick(TimeSpan.FromSeconds(2));
        Assert.Equal(akis.Speed * 2, akis.Offset, 6);
        Assert.True(akis.Playing);

        akis.Tick(TimeSpan.FromMinutes(5));
        Assert.Equal(600, akis.Offset);
        Assert.False(akis.Playing);

        akis.Play();
        Assert.Equal(0, akis.Offset);
        Assert.True(akis.Playing);

        akis.MoveTo(500);
        akis.Resize(700, 400);
        Assert.Equal(300, akis.Offset);
        akis.Resize(300, 400);
        Assert.Equal((0d, 0d, false), (akis.Extent, akis.Offset, akis.Playing));
    }

    [Fact]
    public void AyarMetniVeHiziTasir()
    {
        using var ayar = new KaydediciAyarTests.OzelAyar();
        var kayit = RecorderSettings.Load(ayar.Yol);
        Assert.Equal((string.Empty, PrompterScroll.DefaultStep), (kayit.PrompterText, kayit.PrompterStep));
        kayit.PrompterText = "Merhaba\nikinci satır";
        kayit.PrompterStep = 5;
        kayit.Save(ayar.Yol);
        var geri = RecorderSettings.Load(ayar.Yol);
        Assert.Equal(("Merhaba\nikinci satır", 5), (geri.PrompterText, geri.PrompterStep));

        geri.PrompterStep = 99;
        geri.Save(ayar.Yol);
        Assert.Equal(PrompterScroll.DefaultStep, RecorderSettings.Load(ayar.Yol).PrompterStep);
    }

    [Fact]
    public void PencereYakalamadanCikarilir()
    {
        var (haric, kontrol) = AppHost.Run(() =>
        {
            (bool, uint, bool) Ac(bool cikar)
            {
                var pencere = new RecorderPrompter { ExcludeFromCapture = cikar };
                try
                {
                    pencere.Show();
                    Dispatcher.UIThread.RunJobs();
                    return (pencere.CaptureExcluded, OperatingSystem.IsWindows() ? Oku(pencere) : 0, pencere.WarningRow.IsVisible);
                }
                finally { pencere.Close(); }
            }

            return (Ac(true), Ac(false));
        });

        Assert.Equal((false, 0u, true), kontrol);
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal((false, 0u, true), haric);
            return;
        }

        Assert.Equal((true, Haric, false), haric);
    }

    [Fact]
    public void OkumaKipindeAkarTuslaDururKapanistaKaydeder()
    {
        var olcu = AppHost.Run(() =>
        {
            var pencere = new RecorderPrompter(UzunMetin, 3) { ExcludeFromCapture = false };
            (string Metin, int Basamak)? kaydedilen = null;
            pencere.Saved += (metin, basamak) => kaydedilen = (metin, basamak);
            try
            {
                pencere.Show();
                Dispatcher.UIThread.RunJobs();
                var bosKip = (pencere.Reading, pencere.Press(Key.Space));

                pencere.Start();
                Dispatcher.UIThread.RunJobs();
                pencere.Measure();
                var akiyor = (pencere.Reading, pencere.Scroll.Playing, pencere.Ticking, pencere.Scroll.Extent > 0);

                pencere.Scroll.Pause();
                pencere.Scroll.MoveTo(0);
                pencere.Scroll.Play();
                pencere.Advance(TimeSpan.Zero);
                pencere.Advance(TimeSpan.FromSeconds(1));
                Dispatcher.UIThread.RunJobs();
                var ilerleme = (pencere.Scroll.Offset, pencere.Stage.Offset.Y);

                var tuslar = (pencere.Press(Key.Up), pencere.Scroll.Step, pencere.Press(Key.Space), pencere.Scroll.Playing, pencere.Ticking);
                pencere.Press(Key.Home);
                var basta = pencere.Scroll.Offset;

                pencere.Edit();
                var duzenleme = (pencere.Reading, pencere.Scroll.Playing, pencere.Press(Key.Space));
                pencere.Close();
                return (bosKip, akiyor, ilerleme, tuslar, basta, duzenleme, kaydedilen);
            }
            finally { pencere.Close(); }
        });

        Assert.Equal((false, false), olcu.bosKip);
        Assert.Equal((true, true, true, true), olcu.akiyor);
        Assert.Equal(PrompterScroll.Speeds[3], olcu.ilerleme.Offset, 6);
        Assert.Equal(olcu.ilerleme.Offset, olcu.ilerleme.Y, 0);
        Assert.Equal((true, 4, true, false, false), olcu.tuslar);
        Assert.Equal(0, olcu.basta);
        Assert.Equal((false, false, false), olcu.duzenleme);
        Assert.Equal((UzunMetin, 4), olcu.kaydedilen);
    }

    [Fact]
    public void GorunumBasamaklariUclardaDurur()
    {
        var gorunum = new PrompterLook();
        Assert.Equal((false, true, PrompterLook.DefaultSize, false), (gorunum.Mirror, gorunum.Line, gorunum.Size, gorunum.CanGrow));
        Assert.Same(gorunum, gorunum.Larger());
        var kucuk = gorunum.Smaller().Smaller();
        Assert.Equal((0, false), (kucuk.Size, kucuk.CanShrink));
        Assert.Same(kucuk, kucuk.Smaller());
        Assert.Equal(PrompterLook.DefaultSize, PrompterLook.ClampSize(PrompterLook.SizeCount));
        Assert.Equal(PrompterLook.DefaultSize, PrompterLook.ClampSize(-1));
    }

    [Fact]
    public void AynaCizgiVeYaziBoyuUygulanirAyaraYazilir()
    {
        using var ayar = new KaydediciAyarTests.OzelAyar();
        var kayit = RecorderSettings.Load(ayar.Yol);
        Assert.Equal(new PrompterLook(), kayit.PrompterLook);
        kayit.PrompterText = UzunMetin;
        kayit.Save(ayar.Yol);

        var olcu = AppHost.Run(() =>
        {
            var view = new RecorderView(ayar.Yol);
            var pencere = view.ShowPrompter();
            try
            {
                pencere.Read();
                Dispatcher.UIThread.RunJobs();
                var buyuk = (pencere.Lines.FontSize, pencere.ReadingLine.IsVisible, pencere.Lines.RenderTransform is null, pencere.BtnLargerText.IsEnabled);
                pencere.Measure();
                var buyukUzunluk = pencere.Scroll.Extent;

                pencere.SetLook(pencere.Look.Smaller().Smaller());
                pencere.ChkMirror.IsChecked = true;
                pencere.ChkLine.IsChecked = false;
                Dispatcher.UIThread.RunJobs();
                pencere.UpdateLayout();
                pencere.Measure();
                var ayna = pencere.Lines.RenderTransform is Avalonia.Media.ScaleTransform { ScaleX: -1, ScaleY: 1 };
                var kucuk = (pencere.Lines.FontSize, pencere.ReadingLine.IsVisible, ayna, pencere.BtnSmallerText.IsEnabled, pencere.Scroll.Extent < buyukUzunluk);
                return (buyuk, kucuk, Kucuk: (double)pencere.FindResource("FontSizeLg")!, Buyuk: (double)pencere.FindResource("FontSizeHero")!);
            }
            finally { pencere.Close(); }
        });

        Assert.Equal((olcu.Buyuk, true, true, false), olcu.buyuk);
        Assert.Equal((olcu.Kucuk, false, true, false, true), olcu.kucuk);
        Assert.Equal(new PrompterLook(Mirror: true, Line: false, Size: 0), RecorderSettings.Load(ayar.Yol).PrompterLook);

        var acilis = AppHost.Run(() =>
        {
            var pencere = new RecorderView(ayar.Yol).ShowPrompter();
            try { return (pencere.ChkMirror.IsChecked, pencere.ChkLine.IsChecked, pencere.Lines.FontSize); }
            finally { pencere.Close(); }
        });
        Assert.Equal((true, false, olcu.Kucuk), acilis);
    }

    [Fact]
    public void BosMetinAkmaz()
    {
        var olcu = AppHost.Run(() =>
        {
            var pencere = new RecorderPrompter("  ", PrompterScroll.DefaultStep) { ExcludeFromCapture = false };
            try
            {
                pencere.Show();
                pencere.Start();
                pencere.TogglePlay();
                return (pencere.Reading, pencere.Scroll.Playing, pencere.BtnPlay.IsEnabled);
            }
            finally { pencere.Close(); }
        });

        Assert.Equal((false, false, false), olcu);
    }

    [Fact]
    public void KaydediciKayitlaAkitirElleDurdurmayiEzmez()
    {
        using var ayar = new KaydediciAyarTests.OzelAyar();
        var kayit = RecorderSettings.Load(ayar.Yol);
        kayit.PrompterText = UzunMetin;
        kayit.PrompterStep = 1;
        kayit.Save(ayar.Yol);

        var olcu = AppHost.Run(() =>
        {
            var view = new RecorderView(ayar.Yol);
            var pencere = view.ShowPrompter();
            try
            {
                Dispatcher.UIThread.RunJobs();
                var ayni = ReferenceEquals(pencere, view.ShowPrompter());
                var acilis = (pencere.Text, pencere.Scroll.Step, pencere.Scroll.Playing);

                view.SyncPrompter(true);
                Dispatcher.UIThread.RunJobs();
                pencere.Measure();
                var basladi = (pencere.Reading, pencere.Scroll.Playing);

                pencere.Stop();
                view.SyncPrompter(true);
                var elle = pencere.Scroll.Playing;

                view.SyncPrompter(false);
                view.SyncPrompter(true);
                var yeniden = pencere.Scroll.Playing;
                view.SyncPrompter(false);
                var durdu = pencere.Scroll.Playing;

                pencere.Scroll.Faster();
                pencere.Close();
                Dispatcher.UIThread.RunJobs();
                return (ayni, acilis, basladi, elle, yeniden, durdu, Kapandi: view.PrompterWindow is null);
            }
            finally { pencere.Close(); }
        });

        Assert.True(olcu.ayni);
        Assert.Equal((UzunMetin, 1, false), olcu.acilis);
        Assert.Equal((true, true), olcu.basladi);
        Assert.Equal((false, true, false, true), (olcu.elle, olcu.yeniden, olcu.durdu, olcu.Kapandi));
        var geri = RecorderSettings.Load(ayar.Yol);
        Assert.Equal((UzunMetin, 2), (geri.PrompterText, geri.PrompterStep));
    }
}
