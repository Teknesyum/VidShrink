using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using VidShrink.App;
using VidShrink.App.Recorder;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Paket 2b T7: "Kaydı izle" açıkken biten kayıt küçültme sekmesine ve oynatıcıya yüklenir,
/// seçili sekme değişmez; oynatıcıda açılan yerel dosya da küçültme sekmesine geçer. Kapalıyken
/// hiçbir sekme dosyasını bırakmaz (negatif kontrol). Kanıt <c>.calisma/paket-2b/odak/</c>.
/// </summary>
public sealed class KayitOdakTakibiTests
{
    private static readonly string Kanit = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".calisma", "paket-2b", "odak"));

    private static void Dongu(Func<bool> bitti, double saniye)
    {
        var saat = Stopwatch.StartNew();
        while (!bitti() && saat.Elapsed.TotalSeconds < saniye)
        {
            using var dilim = new CancellationTokenSource(TimeSpan.FromMilliseconds(5));
            Dispatcher.UIThread.MainLoop(dilim.Token);
        }
    }

    private static string KisaVideo(string ad)
    {
        Directory.CreateDirectory(Kanit);
        var dosya = Path.Combine(Kanit, ad);
        if (File.Exists(dosya)) return dosya;
        using var surec = Process.Start(new ProcessStartInfo("ffmpeg",
            $"-hide_banner -y -f lavfi -i testsrc2=s=160x120:r=10:d=2 -c:v libx264 -preset ultrafast -threads 1 \"{dosya}\"")
        {
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        })!;
        var hata = surec.StandardError.ReadToEndAsync();
        Assert.True(surec.WaitForExit(30000), "ffmpeg kisa videoyu 30 sn icinde yazmadi.");
        _ = hata.Result;
        Assert.True(File.Exists(dosya), "ffmpeg kisa videoyu yazmadi.");
        return dosya;
    }

    [Fact]
    public void SecenekKapaliykenHicbirSekmeKaydaDonmez()
    {
        var dosya = KisaVideo("kapali.mkv");
        var olcu = AppHost.Run(() =>
        {
            var window = new MainWindow();
            try
            {
                var yuklenen = new List<string>();
                var acilan = new List<string>();
                window.FollowShrinkLoader = p => { yuklenen.Add(p); return Task.CompletedTask; };
                window.FollowPlayerOpener = p => { acilan.Add(p); return Task.CompletedTask; };
                window.ChkFollowRecording.IsChecked = false;
                var once = window.Tabs.SelectedIndex;
                var is1 = window.FollowRecordingAsync(dosya);
                Dongu(() => is1.IsCompleted, 5);
                window.PlayerOpenedForTest(dosya);
                return (yuklenen.Count, acilan.Count, once, sonra: window.Tabs.SelectedIndex);
            }
            finally { window.Close(); }
        });

        Assert.Equal(0, olcu.Item1);
        Assert.Equal(0, olcu.Item2);
        Assert.Equal(olcu.once, olcu.sonra);
    }

    [Fact]
    public void OynaticidaAcilanDosyaSecenekAcikkenKucultmeyeGecer()
    {
        var dosya = KisaVideo("oynatici.mkv");
        var olcu = AppHost.Run(() =>
        {
            var window = new MainWindow();
            try
            {
                var yuklenen = new List<string>();
                window.FollowShrinkLoader = p => { yuklenen.Add(p); return Task.CompletedTask; };
                window.ChkFollowRecording.IsChecked = true;
                window.PlayerOpenedForTest(dosya);
                window.PlayerOpenedForTest(Path.Combine(Kanit, "olmayan.mkv"));
                var acikSayi = yuklenen.Count;
                window.ChkFollowRecording.IsChecked = false;
                window.PlayerOpenedForTest(dosya);
                return (acikSayi, kapaliSonra: yuklenen.Count);
            }
            finally { window.Close(); }
        });

        Assert.Equal(1, olcu.acikSayi);
        Assert.Equal(1, olcu.kapaliSonra);
    }

    [Fact]
    public void RecorderYalnizBasariliKaydiTeslimKapisinaVerir()
    {
        var dosya = KisaVideo("teslim.mkv");
        var olcu = AppHost.Run(() =>
        {
            var gelen = new List<string>();
            var view = new RecorderView { RevealFolder = _ => { } };
            view.RecordingDelivered = p => { gelen.Add(p); return Task.CompletedTask; };
            view.Deliver(new RecordResult(true, dosya, 1, false, 0, string.Empty, 1));
            view.Deliver(new RecordResult(false, dosya, 1, false, 1, string.Empty, 1));
            view.Deliver(new RecordResult(true, dosya, 1, true, 0, string.Empty, 1));
            view.Deliver(new RecordResult(true, Path.Combine(Kanit, "olmayan.mkv"), 1, false, 0, string.Empty, 1));

            var window = new MainWindow();
            try
            {
                window.Tabs.SelectedIndex = window.RecorderTabIndex;
                var kurulu = window.PageRecorder.Content is RecorderView pane && pane.RecordingDelivered is not null;
                return (gelen: gelen.ToArray(), kurulu);
            }
            finally { window.Close(); }
        });

        Assert.Equal(new[] { dosya }, olcu.gelen);
        Assert.True(olcu.kurulu, "Ana pencere kaydedicinin teslim kapisini kurmadi.");
    }

    [Fact]
    public void SecenekAcikkenKayitKucultmeyeVeOynaticiyaSekmeDegismedenYuklenir()
    {
        var eski = Environment.GetEnvironmentVariable("VIDSHRINK_LIBMPV");
        var dosya = KisaVideo("acik.mkv");
        var olcu = AppHost.Run(() =>
        {
            var window = new MainWindow();
            try
            {
                window.ChkFollowRecording.IsChecked = true;
                window.Tabs.SelectedIndex = window.RecorderTabIndex;
                var is1 = window.FollowRecordingAsync(dosya);
                Dongu(() => is1.IsCompleted, 30);
                var hata = is1.Exception?.GetBaseException().Message;
                return (tamam: is1.IsCompletedSuccessfully, hata,
                    kucultme: window.ShrinkLoadedPath, oynatici: window.PlayerTab.LoadedPath,
                    oynuyor: window.PlayerTab.IsPlaying, sekme: window.Tabs.SelectedIndex, kayitSekmesi: window.RecorderTabIndex);
            }
            finally
            {
                window.PlayerTab.Close();
                window.Close();
            }
        });

        File.WriteAllText(Path.Combine(Kanit, "acik.txt"),
            $"dosya={dosya}\nkucultme={olcu.kucultme}\noynatici={olcu.oynatici}\noynuyor={olcu.oynuyor}\nsekme={olcu.sekme} kayitSekmesi={olcu.kayitSekmesi}\nlibmpv={eski}\n");
        Assert.True(olcu.tamam, olcu.hata);
        Assert.Equal(dosya, olcu.kucultme);
        Assert.Equal(dosya, olcu.oynatici);
        Assert.False(olcu.oynuyor);
        Assert.Equal(olcu.kayitSekmesi, olcu.sekme);
    }
}
