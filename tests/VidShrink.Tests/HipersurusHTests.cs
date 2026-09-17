using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Threading;
using VidShrink.App;
using VidShrink.App.Playback;

namespace VidShrink.Tests;

public sealed class HipersurusHTests
{
    private static async Task<bool> Bekle(Func<bool> kosul, TimeSpan sure)
    {
        var saat = Stopwatch.StartNew();
        while (!kosul() && saat.Elapsed < sure) await Task.Delay(10);
        return kosul();
    }

    [Fact]
    public async Task BakimAcilisGoruntusundenOnceBaslamiyor()
    {
        var acilis = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var baslayan = 0;
        var bakim = Program.BakimiErteleAsync(acilis.Task, TimeSpan.FromSeconds(30), () => { Interlocked.Increment(ref baslayan); return true; });

        Assert.False(await Bekle(() => baslayan > 0, TimeSpan.FromMilliseconds(400)));
        Assert.False(bakim.IsCompleted);

        acilis.SetResult();
        Assert.True(await bakim.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(1, baslayan);
    }

    [Fact]
    public async Task GoruntuHicGelmezseBakimYedekBeklemedenSonraYineKosuyor()
    {
        var hicBitmeyen = new TaskCompletionSource().Task;
        var baslayan = 0;
        var saat = Stopwatch.StartNew();
        var sonuc = await Program.BakimiErteleAsync(hicBitmeyen, TimeSpan.FromMilliseconds(150), () => { baslayan++; return true; })
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(sonuc);
        Assert.Equal(1, baslayan);
        Assert.True(saat.ElapsedMilliseconds >= 140);
    }

    [Fact]
    public async Task BaslaticiHatasiAcilisiDusurmuyor()
    {
        var sonuc = await Program.BakimiErteleAsync(Task.CompletedTask, TimeSpan.FromSeconds(30), () => throw new InvalidOperationException("baslatici yok"))
            .WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(sonuc);
    }

    [Fact]
    public void UretimYoluBakimiAcilisGoruntusuneBagliyorVeDusukOncelikleAciyor()
    {
        var program = File.ReadAllText(Path.Combine(TipSources.Root, "src", "VidShrink.App", "Program.cs"));
        Assert.Contains("=> BakimiErteleAsync(AcilisBitti.Task, BakimYedekBeklemesi, BaslaticiyiBakimIcinAc);", program, StringComparison.Ordinal);
        Assert.Contains("surec.PriorityClass = System.Diagnostics.ProcessPriorityClass.BelowNormal;", program, StringComparison.Ordinal);

        var pencere = File.ReadAllText(Path.Combine(TipSources.Root, "src", "VidShrink.App", "MainWindow.AcilisIzi.cs"));
        Assert.Contains("internal Action AcilisGoruntusu { get; set; } = Program.AcilisGoruntusuGeldi;", pencere, StringComparison.Ordinal);
        var yapici = File.ReadAllText(Path.Combine(TipSources.Root, "src", "VidShrink.App", "MainWindow.axaml.cs"));
        Assert.Contains("        AcilisGoruntusunuBildir();", yapici, StringComparison.Ordinal);
    }

    [Fact]
    public void OlcumAraAsamasiPaneliErteliyorBitinceUyguluyor()
    {
        var yapici = File.ReadAllText(Path.Combine(TipSources.Root, "src", "VidShrink.App", "MainWindow.axaml.cs"));
        Assert.Contains("_preview.AraOlcum = stage == ShrinkMeasureStage.Probed;", yapici, StringComparison.Ordinal);
        Assert.Contains("finally { if (_preview is not null) _preview.AraOlcum = false; }", yapici, StringComparison.Ordinal);
        Assert.Contains("if (ReferenceEquals(_info, info)) _preview?.ErtelenenPlaniUygula();", yapici, StringComparison.Ordinal);
    }

    private static void YuklemeyiKapat(MainWindow pencere)
    {
        var olay = typeof(Window).GetEvent(nameof(Window.Opened))!;
        olay.RemoveEventHandler(pencere, Delegate.CreateDelegate(typeof(EventHandler), pencere, "OnWindowLoaded"));
    }

    [Fact]
    public void BosAcilistaSinyalPencereAcilincaBoyayaKuruluyor()
    {
        var (acilmadan, acilincaKuruldu, boyadanOnce, boyadanSonra) = AppHost.Run(() =>
        {
            var pencere = new MainWindow();
            YuklemeyiKapat(pencere);
            var sayac = 0;
            Action? bekleyen = null;
            pencere.AcilisGoruntusu = () => sayac++;
            pencere.BoyaSonrasi = sonra => bekleyen = sonra;
            Dispatcher.UIThread.RunJobs();
            var once = bekleyen is null && sayac == 0;
            pencere.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                var kuruldu = bekleyen is not null;
                var oncesi = sayac;
                bekleyen?.Invoke();
                return (once, kuruldu, oncesi, sayac);
            }
            finally { pencere.Close(); }
        });

        Assert.True(acilmadan);
        Assert.True(acilincaKuruldu);
        Assert.Equal(0, boyadanOnce);
        Assert.Equal(1, boyadanSonra);
    }

    [Fact]
    public void DosyaylaAcilistaSinyalBoyayaDegilIlkKareyeBagli()
    {
        var (kareOncesi, ilkKare, ikinciKare) = AppHost.Run(() =>
        {
            var pencere = new MainWindow(Path.Combine(TestPaths.OutputRoot, "hiper-h", "yok.mp4"));
            YuklemeyiKapat(pencere);
            var sayac = 0;
            var boyaKuruldu = false;
            pencere.AcilisGoruntusu = () => sayac++;
            pencere.BoyaSonrasi = sonra => { boyaKuruldu = true; sonra(); };
            pencere.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                var once = boyaKuruldu ? -1 : sayac;

                var player = pencere.FindControl<PlayerView>("Player")!;
                var ciz = typeof(PlayerView).GetMethod("DrawFrame", BindingFlags.Instance | BindingFlags.NonPublic)!;
                var piksel = Marshal.AllocHGlobal(4 * 4 * 4);
                try
                {
                    ciz.Invoke(player, new object[] { piksel, 4, 4, 16 });
                    var bir = sayac;
                    ciz.Invoke(player, new object[] { piksel, 4, 4, 16 });
                    return (once, bir, sayac);
                }
                finally { Marshal.FreeHGlobal(piksel); }
            }
            finally { pencere.Close(); }
        });

        Assert.Equal(0, kareOncesi);
        Assert.Equal(1, ilkKare);
        Assert.Equal(1, ikinciKare);
    }
}
