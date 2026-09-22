using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

public sealed class TasmaKarariTests
{
    private static List<MediaPacket> Paketler(int saniye = 100, int kareHizi = 10, int anahtarAraligi = 20, long videoBoyu = 1000, long sesBoyu = 200)
    {
        var paketler = new List<MediaPacket>();
        var kareSayisi = saniye * kareHizi;
        for (var i = 0; i < kareSayisi; i++)
            paketler.Add(new MediaPacket(true, (double)i / kareHizi, videoBoyu, i % anahtarAraligi == 0));
        for (var i = 0; i < saniye * 10; i++)
            paketler.Add(new MediaPacket(false, i / 10.0, sesBoyu, true));
        return paketler;
    }

    [Theory]
    [InlineData(TrimSide.End)]
    [InlineData(TrimSide.Start)]
    [InlineData(TrimSide.Both)]
    public void KesimPlaniKabukPayiylaBirlikteHedefeSigar(TrimSide yon)
    {
        var paketler = Paketler();
        var yuk = paketler.Sum(p => p.Size);
        const long kabuk = 50_000;
        var dosya = yuk + kabuk;
        var hedef = (long)(dosya / 1.025);

        var plan = OvershootTrim.Plan(paketler, dosya, hedef, 100, yon);

        Assert.NotNull(plan);
        Assert.True(plan!.KeptBytes <= hedef, $"kalan {plan.KeptBytes} > hedef {hedef}");
        var kalanYuk = paketler.Where(p => p.Pts >= plan.StartSeconds - 1e-9 && p.Pts < plan.EndSeconds - 1e-9).Sum(p => p.Size);
        Assert.True(kalanYuk + kabuk <= hedef, $"paketlerden sayılan {kalanYuk + kabuk} > hedef {hedef}");
        Assert.InRange(plan.RemovedSeconds, 0.1, 100 * 0.03 + (yon == TrimSide.End ? 0 : 2));

        switch (yon)
        {
            case TrimSide.End:
                Assert.Equal(0, plan.StartSeconds);
                Assert.True(plan.EndSeconds < 100);
                break;
            case TrimSide.Start:
                Assert.Equal(100, plan.EndSeconds);
                Assert.Equal(0, Math.Round(plan.StartSeconds * 10) % 20);
                break;
            default:
                Assert.True(plan.RemovedFromStart > 0 && plan.RemovedFromEnd > 0, $"baş {plan.RemovedFromStart}, son {plan.RemovedFromEnd}");
                Assert.Equal(0, Math.Round(plan.StartSeconds * 10) % 20);
                break;
        }
    }

    [Theory]
    [InlineData(16.48, 16, true)]
    [InlineData(16.2, 16, true)]
    [InlineData(16.49, 16, false)]
    [InlineData(15.9, 16, false)]
    public void KesimYalnizYuzdeUcIcindekiTasmadaOnerilir(double cikti, double hedef, bool bekleniyor)
        => Assert.Equal(bekleniyor, OvershootTrim.Offered(cikti, hedef));

    [Theory]
    [InlineData(TrimSide.End)]
    [InlineData(TrimSide.Start)]
    [InlineData(TrimSide.Both)]
    public async Task KesilenDosyaGercektenHedefinAltindaKalir(TrimSide yon)
    {
        if (!ToolLocator.IsAvailable(out _)) return;
        var klasor = YeniKlasor();
        var kaynak = await KaynakYapAsync(klasor, 20);
        var hedef = (long)(new FileInfo(kaynak).Length / 1.028);
        var cikti = Path.Combine(klasor, $"kesik-{yon}.mp4");

        var sonuc = await OvershootTrimmer.TrimAsync(kaynak, cikti, hedef, yon, CancellationToken.None);

        Assert.True(sonuc.Landed, sonuc.Error);
        Assert.True(File.Exists(cikti));
        Assert.True(new FileInfo(cikti).Length <= hedef, $"{new FileInfo(cikti).Length} > {hedef}");
        var harita = await OvershootTrimmer.ReadAsync(cikti, CancellationToken.None);
        Assert.NotNull(harita);
        Assert.InRange(harita!.DurationSeconds, 20 - 20 * 0.06, 20 - 0.05);
    }

    [Fact]
    public async Task SonDenemedeDeSorulurVeBuyukSonucKabulEdilirseTeslimEdilir()
    {
        if (!ToolLocator.IsAvailable(out _)) return;
        var klasor = YeniKlasor();
        var kaynak = await KaynakYapAsync(klasor, 4);
        var sorulan = new List<(int Deneme, bool Tekrar)>();
        var cikti = Path.Combine(klasor, "buyuk.mp4");

        var sonuc = await new EncodeRunner().RunAsync(Bilgi(kaynak, 4), Plan(), cikti, targetMb: 0.001, progress: null,
            fillPolicy: FillPolicy.QualityCeiling,
            askBeforeRetry: (s, _) =>
            {
                sorulan.Add((s.Attempt, s.CanRetry));
                return Task.FromResult(s.CanRetry ? OvershootChoice.Retry : OvershootChoice.AcceptLarger);
            });

        Assert.Equal(new[] { (1, true), (2, true), (3, true), (4, false) }, sorulan);
        Assert.Contains(sonuc.Trace!, a => a.Branch == "encoder floor, the layout steps down");
        Assert.True(sonuc.Success);
        Assert.True(sonuc.OverTarget);
        Assert.False(sonuc.CeilingExceeded);
        Assert.True(File.Exists(cikti));
        Assert.True(new FileInfo(cikti).Length > 0.001 * 1024 * 1024);
    }

    [Fact]
    public async Task TasmaSorusundaKesmeSecilinceSonucHedefeIner()
    {
        if (!ToolLocator.IsAvailable(out _)) return;
        var klasor = YeniKlasor();
        var kaynak = await KaynakYapAsync(klasor, 20);
        var bilgi = Bilgi(kaynak, 20);

        var ilk = await new EncodeRunner().RunAsync(bilgi, Plan(), Path.Combine(klasor, "ilk.mp4"), targetMb: 0.001, progress: null,
            fillPolicy: FillPolicy.QualityCeiling, askBeforeRetry: (_, _) => Task.FromResult(OvershootChoice.AcceptLarger));
        Assert.True(ilk.OverTarget);
        var hedefMb = ilk.OutputMb / 1.02;

        RetryPrompt? soru = null;
        var cikti = Path.Combine(klasor, "kesik.mp4");
        var sonuc = await new EncodeRunner().RunAsync(bilgi, Plan(), cikti, targetMb: hedefMb, progress: null,
            fillPolicy: FillPolicy.QualityCeiling,
            askBeforeRetry: (s, _) => { soru = s; return Task.FromResult(OvershootChoice.TrimEnd); });

        Assert.NotNull(soru);
        Assert.NotNull(soru!.TrimFor(TrimSide.End));
        Assert.NotNull(soru.TrimFor(TrimSide.Start));
        Assert.NotNull(soru.TrimFor(TrimSide.Both));
        Assert.True(sonuc.Success, sonuc.Error);
        Assert.NotNull(sonuc.Trim);
        Assert.False(sonuc.OverTarget);
        Assert.True(new FileInfo(cikti).Length <= (long)Math.Floor(hedefMb * 1024 * 1024), $"{new FileInfo(cikti).Length} bayt, hedef {hedefMb} MB");
    }

    [Fact]
    public async Task TasmaYuzdeUcuAsinckaKesmeOnerilmez()
    {
        if (!ToolLocator.IsAvailable(out _)) return;
        var klasor = YeniKlasor();
        var kaynak = await KaynakYapAsync(klasor, 4);
        RetryPrompt? soru = null;

        await new EncodeRunner().RunAsync(Bilgi(kaynak, 4), Plan(), Path.Combine(klasor, "x.mp4"), targetMb: 0.001, progress: null,
            fillPolicy: FillPolicy.QualityCeiling,
            askBeforeRetry: (s, _) => { soru ??= s; return Task.FromResult(OvershootChoice.Leave); });

        Assert.NotNull(soru);
        Assert.Empty(soru!.Trims ?? Array.Empty<TrimPlan>());
    }

    [Fact]
    public void PaneldekiHerDugmeKendiSeceneginiDondurur()
    {
        var kesimler = new[]
        {
            new TrimPlan(TrimSide.End, 0, 137.2, 141.4, 16_000_000),
            new TrimPlan(TrimSide.Start, 4.2, 141.4, 141.4, 16_000_000),
            new TrimPlan(TrimSide.Both, 2, 139.2, 141.4, 16_000_000)
        };
        var soru = new RetryPrompt(3, 3, 16, 16.4, TimeSpan.FromSeconds(30), false, 0, kesimler);

        var sonuclar = AppHost.Run(() =>
        {
            var pencere = new MainWindow();
            try
            {
                var goruldu = new List<string>();
                var bulunan = new List<OvershootChoice>();

                var gorev = pencere.ShowRetryAskForTest(soru);
                goruldu.Add($"tekrar:{pencere.BtnRetryAgain.IsVisible}");
                goruldu.Add($"kes:{pencere.BtnRetryTrim.IsVisible}");
                Assert.False(pencere.RetryTrimPanel.IsVisible);
                pencere.BtnRetryAccept.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                bulunan.Add(gorev.Result);

                gorev = pencere.ShowRetryAskForTest(soru);
                pencere.BtnRetryStop.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                bulunan.Add(gorev.Result);

                gorev = pencere.ShowRetryAskForTest(soru);
                pencere.BtnRetryTrim.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                goruldu.Add($"serit:{pencere.RetryTrimPanel.IsVisible}");
                goruldu.Add($"aralik:{pencere.TxtTrimRange.Text}");
                pencere.RbTrimStart.IsChecked = true;
                goruldu.Add($"aralik-bas:{pencere.TxtTrimRange.Text}");
                pencere.BtnTrimConfirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                bulunan.Add(gorev.Result);

                gorev = pencere.ShowRetryAskForTest(soru with { Attempt = 1 });
                goruldu.Add($"tekrar1:{pencere.BtnRetryAgain.IsVisible}");
                pencere.BtnRetryAgain.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                bulunan.Add(gorev.Result);

                gorev = pencere.ShowRetryAskForTest(soru with { Trims = Array.Empty<TrimPlan>() });
                goruldu.Add($"kesyok:{pencere.BtnRetryTrim.IsVisible}");
                return (goruldu, bulunan);
            }
            finally { pencere.Close(); }
        });

        Assert.Equal(new[] { OvershootChoice.AcceptLarger, OvershootChoice.Leave, OvershootChoice.TrimStart, OvershootChoice.Retry }, sonuclar.bulunan);
        Assert.Contains("tekrar:False", sonuclar.goruldu);
        Assert.Contains("kes:True", sonuclar.goruldu);
        Assert.Contains("serit:True", sonuclar.goruldu);
        Assert.Contains("tekrar1:True", sonuclar.goruldu);
        Assert.Contains("kesyok:False", sonuclar.goruldu);
        var sonAralik = sonuclar.goruldu.Single(s => s.StartsWith("aralik:", StringComparison.Ordinal));
        var basAralik = sonuclar.goruldu.Single(s => s.StartsWith("aralik-bas:", StringComparison.Ordinal));
        Assert.Contains("2:17.2", sonAralik);
        Assert.Contains("0:00.0", basAralik);
        Assert.NotEqual(sonAralik["aralik:".Length..], basAralik["aralik-bas:".Length..]);
    }

    /// <summary>
    /// Soru paneli ve kırpma seçenekleri belirerek girer: ilk karede görünür ama saydam, iş
    /// kuyruğu boşalınca hedef opaklık 1 ve bir geçiş taşıyor (çizilen ara kare saat zamanlamasına
    /// bağlı, ölçülmez). Kırpma seçenekleri
    /// ikinci basışta anlık kapanır.
    /// </summary>
    [Fact]
    public void SoruPaneliBelirerekGirer()
    {
        var kesimler = new[] { new TrimPlan(TrimSide.End, 0, 137.2, 141.4, 16_000_000) };
        var soru = new RetryPrompt(3, 3, 16, 16.4, TimeSpan.FromSeconds(30), false, 0, kesimler);

        var olcum = AppHost.Run(() =>
        {
            var pencere = new MainWindow();
            try
            {
                var gorev = pencere.ShowRetryAskForTest(soru);
                var ilk = (pencere.RetryAskPanel.IsVisible, pencere.RetryAskPanel.Opacity);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs(Avalonia.Threading.DispatcherPriority.SystemIdle);
                var son = (pencere.RetryAskPanel.GetBaseValue(Visual.OpacityProperty).GetValueOrDefault<double>(), pencere.RetryAskPanel.Transitions?.Count ?? 0);
                pencere.BtnRetryTrim.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var serit = (pencere.RetryTrimPanel.IsVisible, pencere.RetryTrimPanel.Opacity);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs(Avalonia.Threading.DispatcherPriority.SystemIdle);
                var seritSon = (pencere.RetryTrimPanel.GetBaseValue(Visual.OpacityProperty).GetValueOrDefault<double>(), pencere.RetryTrimPanel.Transitions?.Count ?? 0);
                pencere.BtnRetryTrim.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var kapali = pencere.RetryTrimPanel.IsVisible;
                pencere.BtnRetryStop.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                _ = gorev.Result;
                return (ilk, son, serit, seritSon, kapali);
            }
            finally { pencere.Close(); }
        });

        Assert.Equal((true, 0.0), olcum.ilk);
        Assert.Equal((1.0, 1), olcum.son);
        Assert.Equal((true, 0.0), olcum.serit);
        Assert.Equal((1.0, 1), olcum.seritSon);
        Assert.False(olcum.kapali);
    }

    private static string YeniKlasor()
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "tasma-karari", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        return klasor;
    }

    private static async Task<string> KaynakYapAsync(string klasor, int saniye)
    {
        var kaynak = Path.Combine(klasor, "kaynak.mp4");
        var psi = new ProcessStartInfo
        {
            FileName = ToolLocator.Ffmpeg,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in new[]
        {
            "-y", "-f", "lavfi", "-i", $"testsrc=size=320x240:rate=10:duration={saniye}",
            "-f", "lavfi", "-i", $"sine=frequency=440:duration={saniye}",
            "-c:v", "libx264", "-crf", "18", "-g", "10", "-pix_fmt", "yuv420p",
            "-c:a", "aac", "-b:a", "64k", "-shortest", kaynak
        }) psi.ArgumentList.Add(arg);

        using var surec = new Process { StartInfo = psi };
        surec.Start();
        var bosalt = Task.WhenAll(surec.StandardOutput.ReadToEndAsync(), surec.StandardError.ReadToEndAsync());
        await surec.WaitForExitAsync();
        await bosalt;
        Assert.True(File.Exists(kaynak));
        return kaynak;
    }

    private static MediaInfo Bilgi(string kaynak, int saniye) => new()
    {
        FilePath = kaynak,
        FileSizeBytes = new FileInfo(kaynak).Length,
        DurationSeconds = saniye,
        Width = 320,
        Height = 240,
        Fps = 10,
        VideoCodec = "h264",
        TotalBitrateBps = 400_000
    };

    private static EncodePlan Plan() => new()
    {
        Codec = "libx264",
        Mode = "crf",
        Crf = 18,
        VideoBitrateK = 2000,
        AudioCodec = "aac",
        AudioBitrateK = 64,
        Width = 320,
        Height = 240,
        Fps = 10,
        Preset = "ultrafast",
        ExtraArgs = new List<string> { "-threads", "1" }
    };

    /// <summary>
    /// "Buyuk haliyle kabul et" ile "Kesip sigdir" yazilarini koddan aliyor: XAML'da Content
    /// yok, arayuz taramasi da statik XAML okudugu icin ikisini "adsiz dugme" sayiyordu. XAML'a
    /// yer tutucusuz sabit ad kondu, kod da tazelerken adi canli yaziya esitliyor.
    ///
    /// <para>Kesme kolu gizliyken yazi kurulmuyor; o yolda adin sabit karsiliga dustugu ayrica
    /// pimli. Olumsuz kontrol adin bos ya da yer tutuculu kalmamasi.</para>
    /// </summary>
    [Fact]
    public void TasmaDugmeleriEkranOkuyucuyaAdiylaGorunuyor()
    {
        var kesimler = new[] { new TrimPlan(TrimSide.End, 0, 137.2, 141.4, 16_000_000) };
        var soru = new RetryPrompt(3, 3, 16, 16.4, TimeSpan.FromSeconds(30), false, 0, kesimler);

        var okunan = AppHost.Run(() =>
        {
            var pencere = new MainWindow();
            try
            {
                var kabulIlk = AutomationProperties.GetName(pencere.BtnRetryAccept);
                var kesmeIlk = AutomationProperties.GetName(pencere.BtnRetryTrim);

                pencere.ShowRetryAskForTest(soru);
                var kabul = AutomationProperties.GetName(pencere.BtnRetryAccept);
                var kabulYazi = pencere.BtnRetryAccept.Content as string;
                var kesme = AutomationProperties.GetName(pencere.BtnRetryTrim);
                var kesmeYazi = pencere.BtnRetryTrim.Content as string;

                pencere.ShowRetryAskForTest(soru with { Trims = Array.Empty<TrimPlan>() });
                var kesmeGizli = AutomationProperties.GetName(pencere.BtnRetryTrim);

                return (kabulIlk, kesmeIlk, kabul, kabulYazi, kesme, kesmeYazi, kesmeGizli, kesmeGorunur: pencere.BtnRetryTrim.IsVisible);
            }
            finally { pencere.Close(); }
        });

        Assert.Equal(Strings.Get("main.retry.accept.name"), okunan.kabulIlk);
        Assert.Equal(Strings.Get("main.retry.trim.name"), okunan.kesmeIlk);

        Assert.Equal(okunan.kabulYazi, okunan.kabul);
        Assert.Equal(okunan.kesmeYazi, okunan.kesme);
        Assert.Contains(Strings.Get("main.retry.accept.name"), okunan.kabul);
        Assert.NotEqual(Strings.Get("main.retry.accept.name"), okunan.kabul);
        Assert.Contains(Strings.Get("main.retry.trim.name"), okunan.kesme);
        Assert.Matches(@"\d", okunan.kabul);
        Assert.Matches(@"\d", okunan.kesme);

        Assert.False(okunan.kesmeGorunur);
        Assert.Equal(Strings.Get("main.retry.trim.name"), okunan.kesmeGizli);
        Assert.NotEqual(okunan.kesme, okunan.kesmeGizli);

        foreach (var ad in new[] { okunan.kabul, okunan.kesme, okunan.kesmeGizli })
        {
            Assert.False(string.IsNullOrWhiteSpace(ad));
            Assert.DoesNotContain("{0}", ad);
        }
    }
}
