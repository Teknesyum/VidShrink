using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using VidShrink.App.Localization;
using VidShrink.App.Recorder;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;
using static VidShrink.Tests.KaydediciAyarTests;

namespace VidShrink.Tests;

public sealed class KaydediciHedefTests
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

    /// <summary>Son asertten sonra çağrılır; kuralı <see cref="KanitKapanisi"/> anlatıyor.</summary>
    private static void Kapat(params string[] adlar) => KanitKapanisi.Kapat(Kanit, adlar);

    private static RecorderRequest Istek() => new()
    {
        Platform = RecorderPlatform.Windows,
        Target = RecorderTargetKind.Region,
        Region = new RecorderRegion(0, 0, 640, 480),
        Fps = 15,
        Preset = "ultrafast"
    };

    [Fact]
    public void BoyutSiniriFsArgumaninaGecerVeSifirReddedilir()
    {
        var sinirli = RecorderArguments.Build(Istek() with { MaxMegabytes = 5 }, "a.mp4");
        var sinirsiz = RecorderArguments.Build(Istek(), "a.mp4");

        Assert.Equal("5000000", Deger(sinirli, "-fs"));
        Assert.Null(Deger(sinirsiz, "-fs"));
        Assert.Contains(RecorderArguments.Validate(Istek() with { MaxMegabytes = 0 }, "a.mp4"), e => e.Contains("size limit"));
        Assert.Contains(RecorderArguments.Validate(Istek() with { MaxMegabytes = 2, Container = RecorderContainer.Gif }, "a.gif"), e => e.Contains("GIF"));
    }

    /// <summary>
    /// Kaydedici ozetindeki cozunurluk ailenin yazimini kullanir. Bu satir sifir
    /// pimliydi: adim 5'in mutasyonunda govdeyi atlamak 0 kirmizi veriyordu. Ozet
    /// yalnizca kucultulmus adayda cozunurluk yazar, o yuzden merdivenden <c>Scale</c>
    /// tasiyan ilk aday seciliyor.
    /// </summary>
    [Fact]
    public void OtomatikOzetCozunurlugunuCarpiIsaretiyleYazar()
    {
        var ozet = AyarDosyasiyla(ayarYolu => AppHost.Run(() =>
        {
            var view = new RecorderView(ayarYolu) { SkipAutoMeasure = true };
            var makine = new RecorderMachine(1920, 1080, 60, 8, []);
            var kucultulmus = RecorderAutoPlan.Candidates(makine).First(c => c.Scale is not null);

            view.AutoChoice = kucultulmus;
            typeof(RecorderView)
                .GetMethod("ShowAutoSummary", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(view, new object?[] { null });

            return Bul<TextBlock>(view, "TxtAutoSummary").Text ?? string.Empty;
        }));

        Assert.Contains("960×540", ozet);
        Assert.DoesNotContain("960x540", ozet);
    }

    /// <summary>
    /// Butce notundaki bit hizi ailenin yazimini kullanir: basamak ayraci yok.
    /// Not <c>N0</c> ile yaziliyordu, yani Turkce arayuzde <c>2.666</c>; ayni
    /// uygulamanin kaynak bilgisi ve plan paneli ayni birimi <c>2666</c> diye
    /// yaziyordu. Bu kol notun rakamlarini okur — eski yazimda ayrac gorunurdu.
    /// </summary>
    [Fact]
    public void ButceNotuBasamakAyraciYazmaz()
    {
        var not = AyarDosyasiyla(ayarYolu => AppHost.Run(() =>
        {
            var onceki = Strings.Language;
            Strings.Use("tr");
            try
            {
                var view = new RecorderView(ayarYolu) { SkipAutoMeasure = true, AutoChoice = RecorderAutoPlan.Candidates(new RecorderMachine(1920, 1080, 60, 8, []))[0] };
                Yaz(view, "TxtTargetSeconds", "30");
                Yaz(view, "TxtTargetMegabytes", "10");
                return Bul<TextBlock>(view, "TxtBudgetNote").Text ?? string.Empty;
            }
            finally
            {
                Strings.Use(onceki);
            }
        }));

        File.WriteAllText(Path.Combine(Kanit, "butce-notu.txt"), not);

        Assert.Contains("2666", not);
        Assert.DoesNotContain("2.666", not);
        Assert.DoesNotContain("2,796", not);

        Kapat("butce-notu.txt");
    }

    /// <summary>
    /// Yalnız süre verilen bütçe satırı saniyeyi basamak ayracı olmadan yazıyor. Satır
    /// <c>N0</c> kullanıyordu: Türkçe arayüzde 2796 saniye <c>2.796</c> diye yazılıp
    /// ondalık gibi okunuyordu.
    /// </summary>
    [Fact]
    public void YalnizSureButcesiBasamakAyraciYazmaz()
    {
        var not = AyarDosyasiyla(ayarYolu => AppHost.Run(() =>
        {
            var onceki = Strings.Language;
            Strings.Use("tr");
            try
            {
                var view = new RecorderView(ayarYolu) { SkipAutoMeasure = true };
                Yaz(view, "TxtTargetSeconds", "2796");
                return Bul<TextBlock>(view, "TxtBudgetNote").Text ?? string.Empty;
            }
            finally { Strings.Use(onceki); }
        }));

        Assert.Contains("2796", not);
        Assert.DoesNotContain("2.796", not);
        Assert.DoesNotContain("2,796", not);
        Assert.DoesNotContain("2796,0", not);
    }

    [Fact]
    public void BoyutSiniriParcalaraKalanlaBolunur()
    {
        var istek = Istek() with { MaxMegabytes = 5 };

        Assert.Same(istek, RecorderArguments.ForSegment(istek, TimeSpan.Zero));
        Assert.Equal(2, RecorderArguments.ForSegment(istek, TimeSpan.FromSeconds(3), 3)!.MaxMegabytes);
        Assert.Null(RecorderArguments.ForSegment(istek, TimeSpan.FromSeconds(3), 5));
        Assert.Null(RecorderArguments.ForSegment(Istek(), TimeSpan.FromSeconds(3), 3)!.MaxMegabytes);
    }

    [Fact]
    public void Mp4VeMovHerZamanMatroskayaYakalanir()
    {
        Assert.True(RecorderArguments.CapturesInMatroska(Istek() with { MaxMegabytes = 5 }));
        Assert.True(RecorderArguments.CapturesInMatroska(Istek() with { Container = RecorderContainer.Mov, Split = new RecorderSplit(null, 5) }));
        Assert.True(RecorderArguments.CapturesInMatroska(Istek()));
        Assert.True(RecorderArguments.CapturesInMatroska(Istek() with { Split = new RecorderSplit(TimeSpan.FromSeconds(5), null) }));
        Assert.False(RecorderArguments.CapturesInMatroska(Istek() with { Container = RecorderContainer.Mkv, MaxMegabytes = 5 }));
        Assert.False(RecorderArguments.CapturesInMatroska(Istek() with { Container = RecorderContainer.Mkv }));
        Assert.False(RecorderArguments.CapturesInMatroska(Istek() with { Container = RecorderContainer.Gif }));

        var yakalama = RecorderArguments.MatroskaCapturePath(Path.Combine("k", "kayit_1.mp4"));
        Assert.Equal(Path.Combine("k", "kayit_1.yakalama.mkv"), yakalama);
        Assert.Equal(Path.Combine("k", "kayit_1.mp4"), RecorderArguments.MatroskaDeliveryPath(yakalama, ".mp4"));
        Assert.Equal(Path.Combine("k", "kayit_1.bolum2.mov"), RecorderArguments.MatroskaDeliveryPath(Path.Combine("k", "kayit_1.yakalama.bolum2.mkv"), ".mov"));
    }

    [Fact]
    public void TekHedefKutusuKendiSinirinaGecer()
    {
        var olcu = AyarDosyasiyla(ayarYolu => AppHost.Run(() =>
        {
            (RecorderRequest Istek, string Not) Kur(string saniye, string mb)
            {
                var view = new RecorderView(ayarYolu) { SkipAutoMeasure = true, AutoChoice = RecorderAutoPlan.Candidates(new RecorderMachine(1920, 1080, 60, 8, []))[0] };
                Yaz(view, "TxtTargetSeconds", saniye);
                Yaz(view, "TxtTargetMegabytes", mb);
                return (view.BuildRequest()!, Bul<TextBlock>(view, "TxtBudgetNote").Text ?? string.Empty);
            }

            return (sure: Kur("12", ""), boyut: Kur("", "7"), bos: Kur("", ""), ikisi: Kur("30", "10"));
        }));

        File.WriteAllLines(Path.Combine(Kanit, "tek-hedef.txt"), new[]
        {
            $"sure: t={olcu.sure.Istek.MaxDuration} fs={olcu.sure.Istek.MaxMegabytes} not={olcu.sure.Not}",
            $"boyut: t={olcu.boyut.Istek.MaxDuration} fs={olcu.boyut.Istek.MaxMegabytes} not={olcu.boyut.Not}",
            $"bos: t={olcu.bos.Istek.MaxDuration} fs={olcu.bos.Istek.MaxMegabytes} not={olcu.bos.Not}",
            $"ikisi: t={olcu.ikisi.Istek.MaxDuration} fs={olcu.ikisi.Istek.MaxMegabytes} b={olcu.ikisi.Istek.BitrateKbps}"
        });

        Assert.Equal((TimeSpan.FromSeconds(12), (double?)null), (olcu.sure.Istek.MaxDuration, olcu.sure.Istek.MaxMegabytes));
        Assert.Equal(((TimeSpan?)null, (double?)7), (olcu.boyut.Istek.MaxDuration, olcu.boyut.Istek.MaxMegabytes));
        Assert.Equal(((TimeSpan?)null, (double?)null, string.Empty), (olcu.bos.Istek.MaxDuration, olcu.bos.Istek.MaxMegabytes, olcu.bos.Not));
        Assert.Contains("12", olcu.sure.Not);
        Assert.Contains("7", olcu.boyut.Not);
        Assert.Equal((TimeSpan.FromSeconds(30), (double?)10), (olcu.ikisi.Istek.MaxDuration, olcu.ikisi.Istek.MaxMegabytes));
        Assert.NotNull(olcu.ikisi.Istek.BitrateKbps);
        Assert.Equal("5000000", Deger(RecorderArguments.Build(olcu.boyut.Istek with { MaxMegabytes = 5, Container = RecorderContainer.Mkv }, "a.mkv"), "-fs"));

        Kapat("tek-hedef.txt");
    }

    [Fact]
    public void KayitBitinceKlasorYalnizKutuAcikkenAcilir()
    {
        var dosya = Path.Combine(Kanit, "klasor-ac.mkv");
        File.WriteAllBytes(dosya, new byte[] { 1 });
        var olcu = AyarDosyasiyla(ayarYolu => AppHost.Run(() =>
        {
            var acilan = new List<string>();
            var kapali = new RecorderView(ayarYolu) { RevealFolder = acilan.Add };
            kapali.Deliver(new RecordResult(true, dosya, 1, false, 0, string.Empty, 1));
            var kapaliSayi = acilan.Count;

            var once = new RecorderView(ayarYolu);
            Bul<CheckBox>(once, "ChkOpenFolder").IsChecked = true;
            var dosyada = JsonNode.Parse(File.ReadAllText(ayarYolu))?["openFolderWhenDone"]?.ToJsonString();

            var acik = new RecorderView(ayarYolu) { RevealFolder = acilan.Add };
            acik.Deliver(new RecordResult(true, dosya, 1, false, 0, string.Empty, 1));
            return (kapaliSayi, dosyada, acilan: acilan.ToArray(), kutu: Bul<CheckBox>(acik, "ChkOpenFolder").IsChecked);
        }));

        Assert.Equal(0, olcu.kapaliSayi);
        Assert.Equal("true", olcu.dosyada);
        Assert.True(olcu.kutu);
        Assert.Equal(new[] { dosya }, olcu.acilan);

        Kapat("klasor-ac.mkv");
    }

    [Fact]
    public void IptalKisayoluOturumYokkenIslemezVeTanimdaVar()
    {
        var olcu = AyarDosyasiyla(ayarYolu => AppHost.Run(() => new RecorderView(ayarYolu).RunHotkeyAsync(HotkeyAction.Discard).GetAwaiter().GetResult()));

        Assert.False(olcu);
        Assert.Equal(HotkeyAction.Discard, RecorderHotkeys.ActionOf(Avalonia.Input.Key.F10, Avalonia.Input.KeyModifiers.None));
        Assert.Equal(5, RecorderHotkeys.All.Select(b => b.VirtualKey).Distinct().Count());
    }

    private static void Pompala(Task gorev, int sinirMs)
    {
        var saat = System.Diagnostics.Stopwatch.StartNew();
        while (!gorev.IsCompleted && saat.ElapsedMilliseconds < sinirMs)
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Thread.Sleep(20);
        }

        Assert.True(gorev.IsCompleted, "gorev zamaninda bitmedi");
        gorev.GetAwaiter().GetResult();
    }

    private static void KayitKapat(RecorderView view, Task baslat)
    {
        var saat = System.Diagnostics.Stopwatch.StartNew();
        while (!baslat.IsCompleted && saat.ElapsedMilliseconds < 60000)
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Thread.Sleep(20);
        }

        if (!view.HasSession) return;
        var iptal = view.RunHotkeyAsync(HotkeyAction.Discard);
        saat.Restart();
        while (!iptal.IsCompleted && saat.ElapsedMilliseconds < 15000)
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Thread.Sleep(20);
        }
    }

    [KayitFact]
    public void IptalKaydiDurdururVeDosyayiSiler()
    {
        var klasor = Path.Combine(Kanit, "iptal");
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);

        var olcu = AyarDosyasiyla(ayarYolu => AppHost.Run(() =>
        {
            var view = new RecorderView(ayarYolu) { SkipAutoMeasure = true };
            Elle(view);
            Bul<ComboBox>(view, "CmbTarget").SelectedIndex = (int)RecorderTargetKind.Region;
            Yaz(view, "TxtRegionX", "0");
            Yaz(view, "TxtRegionY", "0");
            Yaz(view, "TxtRegionWidth", "320");
            Yaz(view, "TxtRegionHeight", "240");
            Sec(view, "CmbCodec", "libx264");
            Sec(view, "CmbPreset", "ultrafast");
            Yaz(view, "TxtOutputFolder", klasor);

            var baslat = view.StartAsync();
            try
            {
                Pompala(baslat, 45000);
                var basladi = view.HasSession;
                Pompala(Task.Delay(1500), 3000);
                var iptal = view.RunHotkeyAsync(HotkeyAction.Discard);
                Pompala(iptal, 15000);
                return (basladi, hata: view.ErrorText, iptal: iptal.Result, oturum: view.HasSession, not: view.NoticeText,
                    kalan: Directory.Exists(klasor) ? Directory.GetFiles(klasor, "*", SearchOption.AllDirectories) : Array.Empty<string>());
            }
            finally
            {
                KayitKapat(view, baslat);
            }
        }));

        File.WriteAllLines(Path.Combine(Kanit, "iptal.txt"), new[] { $"basladi={olcu.basladi} iptal={olcu.iptal} oturum={olcu.oturum} not={olcu.not} hata={olcu.hata} kalan={olcu.kalan.Length}" });
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);

        Assert.True(olcu.basladi, olcu.hata);
        Assert.True(olcu.iptal);
        Assert.False(olcu.oturum);
        Assert.Empty(olcu.kalan);
        Assert.NotEmpty(olcu.not);

        Kapat("iptal.txt");
    }

    /// <summary>
    /// Sinir ve bekleme ortama bagimli degil. Eski degerler (0,05 MB / 8 sn) hareketsiz
    /// masaustunde tutmuyordu: 19 Eylul 2026'da gdigrab 8 saniyede 26694 bayt yazdi, sinir
    /// 52428'di ve olcu kirmizi dondu. Kusur kodda degil, ekran iceriginde — durgun goruntu
    /// az bayt uretir. Sinir olculen hizin (~3300 bayt/sn) yarisinin altina indirildi ve
    /// bekleme iki katindan fazlasina acildi: 0,02 MB'a hareketsiz masaustunde ~6,4 saniyede
    /// ulasilir, hareketli ekranda cok daha erken. Iki yonde de genis pay var.
    /// </summary>
    [KayitFact]
    public async Task BoyutSiniriDolunacaKayitKendiBiter()
    {
        var cikti = Path.Combine(Kanit, "boyut-siniri.mp4");
        var istek = Istek() with { Quality = 0, KeyframeSeconds = 1, MaxMegabytes = 0.02 };

        var oturum = await RecorderSession.StartAsync(istek, cikti);
        var saat = System.Diagnostics.Stopwatch.StartNew();
        var bitti = await Task.WhenAny(oturum.Ended, Task.Delay(20000)) == oturum.Ended;
        var sure = saat.Elapsed.TotalSeconds;
        var sonuc = await oturum.StopAsync();
        var bayt = File.Exists(cikti) ? new FileInfo(cikti).Length : 0;
        var yakalama = RecorderArguments.MatroskaCapturePath(cikti);
        var (kod, metin) = KayitKanit.Ffprobe(cikti, "boyut-siniri.ffprobe.txt");
        var baslik = File.Exists(cikti) ? System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(cikti), 4, 4) : string.Empty;
        var (paketBayt, sonKume) = Paketler(cikti);
        var sinir = Megabayt.Tavan(0.02);

        File.WriteAllLines(Path.Combine(Kanit, "boyut-siniri.txt"), new[] { $"ended={bitti} sn={sure:0.00} ok={sonuc.Ok} teslim={sonuc.OutputPath} bayt={bayt} paket={paketBayt} sonIkiAnahtarKumesi={sonKume} sinir={sinir} yakalamaKaldi={File.Exists(yakalama)} probe={kod}" });
        if (File.Exists(cikti)) File.Delete(cikti);

        Assert.True(bitti, "0,02 MB siniri dolunca oturum kendiliginden bitmeli");
        Assert.True(sonuc.Ok, sonuc.StandardError);
        Assert.Equal(cikti, sonuc.OutputPath);
        Assert.False(File.Exists(yakalama));
        Assert.Equal(0, kod);
        Assert.Equal("ftyp", baslik);
        Assert.Contains("codec_type=video", metin);
        Assert.InRange(sonKume, 1, paketBayt);
        Assert.InRange(paketBayt - sonKume, 0, sinir);

        Kapat("boyut-siniri.txt");
        KayitKanit.Kapat("boyut-siniri.ffprobe.txt");
    }

    /// <summary>
    /// CI kirmizisinin pimi (35316250545): sinir ilk karede dolunca ffmpeg 0 ile biter ve
    /// baslangic el sikismasi ilerleme blogunu hic gormeyebilir. Biten kayit "baslatamadi" degildir.
    /// </summary>
    [KayitFact]
    public async Task IlerlemeBloguGelmese_deSifirlaBitenKayitBaslatamadiSayilmaz()
    {
        var cikti = Path.Combine(Kanit, "yaris-ilk-kare.mp4");
        var istek = Istek() with { Quality = 0, KeyframeSeconds = 1, MaxMegabytes = 0.05 };

        RecorderSession.IlerlemeyiYut = true;
        RecordResult sonuc;
        bool bitti;
        try
        {
            var oturum = await RecorderSession.StartAsync(istek, cikti);
            bitti = await Task.WhenAny(oturum.Ended, Task.Delay(15000)) == oturum.Ended;
            sonuc = await oturum.StopAsync();
        }
        finally { RecorderSession.IlerlemeyiYut = false; }

        var bayt = File.Exists(cikti) ? new FileInfo(cikti).Length : 0;
        File.WriteAllLines(Path.Combine(Kanit, "yaris-ilk-kare.txt"), new[] { $"ilerlemeYutuldu=True ended={bitti} ok={sonuc.Ok} bayt={bayt} kod={sonuc.ExitCode}" });
        if (File.Exists(cikti)) File.Delete(cikti);

        Assert.True(bitti, "sinir dolunca oturum kendiliginden bitmeli");
        Assert.True(sonuc.Ok, sonuc.StandardError);
        Assert.True(bayt > 0);
        Kapat("yaris-ilk-kare.txt");
    }

    private static (long Toplam, long SonKume) Paketler(string dosya)
    {
        if (!File.Exists(dosya)) return (0, 0);
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = ToolLocator.Ffprobe,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in new[] { "-hide_banner", "-v", "error", "-select_streams", "v:0", "-show_entries", "packet=size,flags", "-of", "csv=p=0", dosya })
            psi.ArgumentList.Add(arg);
        using var surec = System.Diagnostics.Process.Start(psi)!;
        var hata = surec.StandardError.ReadToEndAsync();
        var satirlar = surec.StandardOutput.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        surec.WaitForExit(20_000);
        _ = hata.Result;

        var kumeler = new List<long>();
        foreach (var satir in satirlar)
        {
            var parca = satir.Split(',');
            if (!long.TryParse(parca[0], out var boy)) continue;
            if (kumeler.Count == 0 || (parca.Length > 1 && parca[1].StartsWith('K'))) kumeler.Add(0);
            kumeler[^1] += boy;
        }
        return (kumeler.Sum(), kumeler.TakeLast(2).Sum());
    }
}
