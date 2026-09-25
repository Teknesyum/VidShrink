using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.Core;

namespace VidShrink.Tests;

/// <summary>
/// Güncelleme bildiriminin iki davranışı. "İndir ve yükle": indirme bitince kurulum
/// kullanıcı bir daha basmadan koşar, iptal ve hatada bayrak düşer. "Yeni sürüme geçildi":
/// şerit <see cref="MainWindow.AppliedNoticeSeconds"/> saniye sonra kendiliğinden kapanır,
/// fare ya da odak sayacı durdurur. Ağa çıkmaz, gerçek kurulum yapmaz: indirme ve kurulum
/// yerine sahte iş, sayaca sahte saat verilir.
/// </summary>
public sealed class GuncellemeIndirYukleTests
{
    private static string Durum(MainWindow p) =>
        $"birincil:{Anahtar(p.BtnNoticeInstall.Content)}|ikinci:{p.BtnNoticeDownloadInstall.IsVisible}|bayrak:{p.InstallAfterDownload}";

    private static string Anahtar(object? icerik) =>
        new[] { "download", "cancel", "install" }
            .FirstOrDefault(k => Equals(icerik, LanguageCatalog.Display(Strings.Get("main.action." + k)))) ?? $"?{icerik}";

    private static void SahteIndirme(MainWindow p, List<string> kurulum)
    {
        p.UpdateDownloadStarter = () =>
        {
            p.SetUpdateBadge(UpdateBadgeState.Downloading);
            p.ShowUpdateProgress(new InstallProgress());
        };
        p.StagedUpdateInstaller = () => kurulum.Add("kur");
    }

    private static void Sahnelendi(MainWindow p)
    {
        p.UpdateReports.Enqueue(new UpdateStageReport(UpdateStagePhase.Staged, 1, 1));
        p.OnUpdateDownloadFinished(Task.FromResult<bool?>(true));
    }

    private static void Tikla(Button dugme) => dugme.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [Fact]
    public void IndirVeYukleIndirmeBitinceKurar()
    {
        var sonuc = AppHost.Run(() =>
        {
            var p = new MainWindow();
            try
            {
                var kurulum = new List<string>();
                SahteIndirme(p, kurulum);
                var goruldu = new List<string>();

                p.SetUpdateBadge(UpdateBadgeState.NewVersion);
                goruldu.Add("bosta " + Durum(p) + $"|birincil-gorunur:{p.BtnNoticeInstall.IsVisible}");

                Tikla(p.BtnNoticeDownloadInstall);
                goruldu.Add("inerken " + Durum(p));

                Sahnelendi(p);
                goruldu.Add("hazir " + Durum(p) + $"|kurulum:{kurulum.Count}");
                return goruldu;
            }
            finally { p.Close(); }
        });

        Assert.Equal(new[]
        {
            "bosta birincil:download|ikinci:True|bayrak:False|birincil-gorunur:True",
            "inerken birincil:cancel|ikinci:False|bayrak:True",
            "hazir birincil:install|ikinci:False|bayrak:False|kurulum:1"
        }, sonuc);
    }

    /// <summary>Olumsuz kontrol: rozetten ya da "İndir"den başlayan indirme bitince kurmaz, "Yükle"yi bekler.</summary>
    [Fact]
    public void DuzIndirmeBitinceKurmaz()
    {
        var sonuc = AppHost.Run(() =>
        {
            var p = new MainWindow();
            try
            {
                var kurulum = new List<string>();
                SahteIndirme(p, kurulum);
                p.SetUpdateBadge(UpdateBadgeState.NewVersion);
                p.UpdateDownloadStarter!();
                Sahnelendi(p);
                return Durum(p) + $"|kurulum:{kurulum.Count}";
            }
            finally { p.Close(); }
        });

        Assert.Equal("birincil:install|ikinci:False|bayrak:False|kurulum:0", sonuc);
    }

    public static TheoryData<string> DusenSonlar => new() { "iptal", "hata", "kilit" };

    [Theory]
    [MemberData(nameof(DusenSonlar))]
    public void IptalVeHataBayragiSifirlarIkiDugmeGeriGelir(string son)
    {
        var sonuc = AppHost.Run(() =>
        {
            var p = new MainWindow();
            try
            {
                var kurulum = new List<string>();
                SahteIndirme(p, kurulum);
                p.SetUpdateBadge(UpdateBadgeState.NewVersion);
                Tikla(p.BtnNoticeDownloadInstall);
                var once = Durum(p);

                p.OnUpdateDownloadFinished(son switch
                {
                    "iptal" => Task.FromCanceled<bool?>(new CancellationToken(true)),
                    "hata" => Task.FromException<bool?>(new IOException("ağ yok")),
                    _ => Task.FromResult<bool?>(null)
                });
                var sonra = Durum(p);

                Sahnelendi(p);
                return $"{once} => {sonra} => sonraki-indirme-kurulum:{kurulum.Count}";
            }
            finally { p.Close(); }
        });

        Assert.Equal(
            "birincil:cancel|ikinci:False|bayrak:True => birincil:download|ikinci:True|bayrak:False => sonraki-indirme-kurulum:0",
            sonuc);
    }

    [Fact]
    public void BaslamayanIndirmeBayragiBirakmaz()
    {
        var sonuc = AppHost.Run(() =>
        {
            var p = new MainWindow();
            try
            {
                p.UpdateDownloadStarter = () => { };
                p.StagedUpdateInstaller = () => { };
                p.SetUpdateBadge(UpdateBadgeState.NewVersion);
                Tikla(p.BtnNoticeDownloadInstall);
                return Durum(p);
            }
            finally { p.Close(); }
        });

        Assert.Equal("birincil:download|ikinci:True|bayrak:False", sonuc);
    }

    [Fact]
    public void UygulandiSeridiBesSaniyeSonraKapanirFareDuraklatir()
    {
        var sonuc = AppHost.Run(() =>
        {
            var p = new MainWindow();
            try
            {
                var saat = TimeSpan.Zero;
                p.AppliedNoticeClock = () => saat;
                var goruldu = new List<string>();
                string Oku() => $"gorunur:{p.AppliedNotice.IsVisible}|sayac:{p.AppliedNoticeTimer?.IsEnabled}|kalan:{p.AppliedNoticeCountdown?.Remaining.TotalSeconds}";

                p.ShowAppliedNotice();
                goruldu.Add($"acildi {Oku()}|aralik:{p.AppliedNoticeTimer!.Interval.TotalSeconds}");

                saat = TimeSpan.FromSeconds(3);
                p.AppliedNoticePointer(true);
                goruldu.Add($"fare {Oku()}");

                saat = TimeSpan.FromSeconds(20);
                p.AppliedNoticeTick();
                goruldu.Add($"fare-bekledi {Oku()}");

                p.AppliedNoticePointer(false);
                goruldu.Add($"fare-gitti {Oku()}|aralik:{p.AppliedNoticeTimer!.Interval.TotalSeconds}");

                saat = TimeSpan.FromSeconds(21.9);
                p.AppliedNoticeTick();
                goruldu.Add($"erken {p.AppliedNotice.IsVisible}");

                saat = TimeSpan.FromSeconds(22);
                p.AppliedNoticeTick();
                goruldu.Add($"doldu {Oku()}");
                return goruldu;
            }
            finally { p.Close(); }
        });

        Assert.Equal(new[]
        {
            $"acildi gorunur:True|sayac:True|kalan:{MainWindow.AppliedNoticeSeconds}|aralik:5",
            "fare gorunur:True|sayac:False|kalan:2",
            "fare-bekledi gorunur:True|sayac:False|kalan:2",
            "fare-gitti gorunur:True|sayac:True|kalan:2|aralik:2",
            "erken True",
            "doldu gorunur:False|sayac:False|kalan:"
        }, sonuc);
    }

    [Fact]
    public void UygulandiSeridiOdaktayikenKapanmaz()
    {
        var sonuc = AppHost.Run(() =>
        {
            var p = new MainWindow();
            try
            {
                var saat = TimeSpan.Zero;
                p.AppliedNoticeClock = () => saat;
                p.Show();
                p.ShowAppliedNotice();
                var odak = p.BtnAppliedDismiss.Focus(NavigationMethod.Tab);
                var goruldu = new List<string> { $"odak:{odak && p.AppliedNotice.IsKeyboardFocusWithin}|sayac:{p.AppliedNoticeTimer?.IsEnabled}" };

                saat = TimeSpan.FromSeconds(60);
                p.AppliedNoticeTick();
                goruldu.Add($"odakta:{p.AppliedNotice.IsVisible}|kalan:{p.AppliedNoticeCountdown?.Remaining.TotalSeconds}");

                p.UpdateNotice.IsVisible = true;
                var disari = p.BtnNoticeDismiss.Focus(NavigationMethod.Tab);
                goruldu.Add($"odak-gitti:{disari && !p.AppliedNotice.IsKeyboardFocusWithin}|sayac:{p.AppliedNoticeTimer?.IsEnabled}|aralik:{p.AppliedNoticeTimer?.Interval.TotalSeconds}");

                saat = TimeSpan.FromSeconds(65);
                p.AppliedNoticeTick();
                goruldu.Add($"doldu:{p.AppliedNotice.IsVisible}");
                return goruldu;
            }
            finally { p.Close(); }
        });
        Assert.Equal(new[] { "odak:True|sayac:False", "odakta:True|kalan:5", "odak-gitti:True|sayac:True|aralik:5", "doldu:False" }, sonuc);
    }

    /// <summary>Saat dolunca şeridi kapatan gerçekten zamanlayıcının kendisi; testte 5 sn beklenmez, aralık kısaltılır.</summary>
    [Fact]
    public void ZamanlayiciDoluncaSeritGizlenir()
    {
        var sonuc = AppHost.Run(() =>
        {
            var p = new MainWindow();
            try
            {
                var saat = TimeSpan.Zero;
                p.AppliedNoticeClock = () => saat;
                p.ShowAppliedNotice();
                saat = TimeSpan.FromSeconds(MainWindow.AppliedNoticeSeconds);
                p.AppliedNoticeTimer!.Interval = TimeSpan.FromMilliseconds(20);

                var olcu = Stopwatch.StartNew();
                while (p.AppliedNotice.IsVisible && olcu.Elapsed < TimeSpan.FromSeconds(3))
                {
                    using var dilim = new CancellationTokenSource(TimeSpan.FromMilliseconds(5));
                    Dispatcher.UIThread.MainLoop(dilim.Token);
                }
                return p.AppliedNotice.IsVisible;
            }
            finally { p.Close(); }
        });

        Assert.False(sonuc);
    }

    [Fact]
    public void KullaniciKapatincaSayacDurur()
    {
        var sonuc = AppHost.Run(() =>
        {
            var p = new MainWindow();
            try
            {
                p.ShowAppliedNotice();
                var once = p.AppliedNoticeTimer?.IsEnabled;
                Tikla(p.BtnAppliedDismiss);
                return $"{once}|{p.AppliedNoticeTimer?.IsEnabled}|{p.AppliedNotice.IsVisible}|{p.AppliedNoticeCountdown is null}";
            }
            finally { p.Close(); }
        });

        Assert.Equal("True|False|False|True", sonuc);
    }
}