using System.Globalization;
using System.Text.Json;
using VidShrink.App;
using VidShrink.Cli;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using static VidShrink.Tests.B1KalanGirdi;

namespace VidShrink.Tests;

/// <summary>
/// Gelişmiş paneldeki "Meta veriyi sil" kutusu: CLI'ın <c>--meta-yok</c> bayrağıyla aynı Core
/// yoluna (<see cref="PlanOptions.DropMetadata"/>) iner. Kutu açıkken pencerenin komutu CLI'ın
/// komutuyla aynıdır, kapalıyken etiket eşlemi durur; seçim <c>advDropMetadata</c> olarak saklanır.
/// </summary>
public sealed class MetaSilArayuzTests
{
    private static readonly string Klasor = Path.Combine(TestPaths.OutputRoot, "meta-sil-arayuz");

    /// <summary>
    /// Her çağrı klasördeki eski ayar dosyalarını süpürür: gösterilmeden kapatılan pencere
    /// sonraki pencere kurulurken kendi dosyasını yeniden yazıyor, <c>finally</c> onu yakalamıyor.
    /// </summary>
    private static string AyarDosyasi()
    {
        Directory.CreateDirectory(Klasor);
        foreach (var eski in Directory.GetFiles(Klasor, "settings-*.json")) File.Delete(eski);
        return Path.Combine(Klasor, "settings-" + Guid.NewGuid().ToString("N") + ".json");
    }

    private static void Sil(string dosya)
    {
        if (File.Exists(dosya)) File.Delete(dosya);
    }

    private static MediaInfo Ornek(long bayt = 90_000_000L) => new()
    {
        FilePath = @"C:\Kayitlar\gezi.mp4",
        FileSizeBytes = bayt,
        DurationSeconds = 62.0,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 11_600_000,
        AudioCodec = "aac",
        AudioBitrateBps = 128_000,
        AudioChannels = 2,
        PixelFormat = "yuv420p",
        Streams = new[]
        {
            new SourceStream(0, StreamKind.Video, "h264"),
            new SourceStream(1, StreamKind.Audio, "aac", "tur", Channels: 2, BitrateBps: 128_000)
        }
    };

    private sealed record Okuma(bool Kutu, bool Secenek, string Komut, EncodeMode Kip, string Json);

    private static Okuma Pencere(MediaInfo info, bool? kutu, double hedefMb = 25)
    {
        var dosya = AyarDosyasi();
        try
        {
            return AppHost.Run(() =>
            {
                var window = new MainWindow { SettingsPathOverride = dosya };
                try
                {
                    window.LoadWithoutProbing(info.FilePath, info);
                    window.TxtTarget.Text = hedefMb.ToString("0.##", CultureInfo.InvariantCulture);
                    if (kutu is { } deger) window.ChkAdvDropMetadata.IsChecked = deger;
                    window.RecalculateForTest();
                    var options = window.PlanOptionsForTest();
                    return new Okuma(window.ChkAdvDropMetadata.IsChecked == true, options.DropMetadata,
                        window.TxtCommand.Text ?? "", window.ActivePlanForTest!.ModeEnum, JsonSerializer.Serialize(options));
                }
                finally { window.Close(); }
            });
        }
        finally { Sil(dosya); }
    }

    private static CliDecision Cli(MediaInfo info, bool metaYok)
    {
        var satir = new List<string> { "plan", info.FilePath, "--hedef", "25MB" };
        if (metaYok) satir.Add("--meta-yok");
        var istek = CliParser.Parse(satir.ToArray()).Request!;
        return CliApp.Decide(istek with { SkipMeasurement = true, PreferredLanguage = "en" }, info, null, null, null);
    }

    /// <summary>Kutu dokunulmamış pencerede kapalıdır ve seçenek plana kapalı gider.</summary>
    [Fact]
    public void KutuVarsayilanKapali()
    {
        var okuma = Pencere(Ornek(), null);

        Assert.False(okuma.Kutu);
        Assert.False(okuma.Secenek);
        Assert.Contains("-map_metadata 0", okuma.Komut, StringComparison.Ordinal);
        Assert.DoesNotContain("language=tur", okuma.Komut, StringComparison.Ordinal);
    }

    /// <summary>
    /// Açık kutunun komutu <c>--meta-yok</c> ile koşan CLI'ın komutuyla harfi harfine aynı:
    /// etiket eşlemi kapanır, iz dili açıkça yazılır.
    /// </summary>
    [Fact]
    public void AcikKutuCliIleAyniKomutuUretiyor()
    {
        var info = Ornek();

        var gui = Pencere(info, true);
        var cli = Cli(info, true);

        Assert.True(gui.Secenek);
        Assert.True(cli.Options.DropMetadata);
        Assert.Contains("-map_metadata -1", gui.Komut, StringComparison.Ordinal);
        Assert.Contains("-metadata:s:a:0 language=tur", gui.Komut, StringComparison.Ordinal);
        Assert.Equal(JsonSerializer.Serialize(cli.Options), gui.Json);
        Assert.Equal(FfmpegArguments.ToCommandLine(cli.Arguments), gui.Komut);
    }

    /// <summary>Olumsuz kontrol: kapalı kutunun komutu bayraksız CLI ile aynı, bayraklı CLI'dan ayrı.</summary>
    [Fact]
    public void KapaliKutuBayraksizCliIleAyni()
    {
        var info = Ornek();

        var gui = Pencere(info, false);

        Assert.Equal(FfmpegArguments.ToCommandLine(Cli(info, false).Arguments), gui.Komut);
        Assert.NotEqual(FfmpegArguments.ToCommandLine(Cli(info, true).Arguments), gui.Komut);
        Assert.DoesNotContain("-map_metadata -1", gui.Komut, StringComparison.Ordinal);
    }

    /// <summary>
    /// Hedefe zaten sığan kaynak kutu kapalıyken kopyalanır, açıkken yeniden kodlanır; CLI'ın
    /// <c>--meta-yok</c> davranışı da budur (<c>MetaYokTests.BayrakIstektenPlanaIniyorVeKopyayiKesiyor</c>).
    /// </summary>
    [Fact]
    public void AcikKutuKopyayiKesiyor()
    {
        var kucuk = Ornek(5_000_000L);

        Assert.Equal(EncodeMode.PassThrough, Pencere(kucuk, false).Kip);
        Assert.NotEqual(EncodeMode.PassThrough, Pencere(kucuk, true).Kip);
        Assert.Equal(EncodeMode.PassThrough, Cli(kucuk, false).Plan.ModeEnum);
        Assert.NotEqual(EncodeMode.PassThrough, Cli(kucuk, true).Plan.ModeEnum);
    }

    /// <summary>Kutu değişince ayar dosyasına yazılır, yeni pencerede geri gelir ve plana geçer.</summary>
    [Fact]
    public void SecimSaklanipGeriGeliyor()
    {
        var dosya = AyarDosyasi();
        try
        {
            var (yazilan, geriGelen, secenek) = AppHost.Run(() =>
            {
                var ilk = new MainWindow { SettingsPathOverride = dosya };
                try { ilk.ChkAdvDropMetadata.IsChecked = true; }
                finally { ilk.Close(); }
                var metin = File.ReadAllText(dosya);

                var ikinci = new MainWindow { SettingsPathOverride = dosya };
                try
                {
                    var once = ikinci.ChkAdvDropMetadata.IsChecked == true;
                    ikinci.RestoreAppSettingsForTest(AppSettings.Load(dosya));
                    return (metin, (once, ikinci.ChkAdvDropMetadata.IsChecked == true), ikinci.PlanOptionsForTest().DropMetadata);
                }
                finally { ikinci.Close(); }
            });

            using var doc = JsonDocument.Parse(yazilan);
            Assert.True(doc.RootElement.GetProperty("advDropMetadata").GetBoolean());
            Assert.False(geriGelen.once);
            Assert.True(geriGelen.Item2);
            Assert.True(secenek);
        }
        finally { Sil(dosya); }
    }

    /// <summary>Anahtarı taşımayan eski ayar dosyası kutuyu kapalı açar; açık kayıt olumlu kontrol.</summary>
    [Fact]
    public void EskiAyarDosyasiKapaliOkunuyor()
    {
        var dosya = AyarDosyasi();
        try
        {
            File.WriteAllText(dosya, "{\"advKeepTracks\":true,\"advCrf\":2}");
            var eski = AppSettings.Load(dosya);
            Assert.True(eski.AdvKeepTracks);
            Assert.False(eski.AdvDropMetadata);

            new AppSettings { AdvDropMetadata = true }.Save(dosya);
            Assert.True(AppSettings.Load(dosya).AdvDropMetadata);

            var kutu = AppHost.Run(() =>
            {
                var window = new MainWindow { SettingsPathOverride = dosya };
                try
                {
                    window.ChkAdvDropMetadata.IsChecked = true;
                    window.RestoreAppSettingsForTest(eski);
                    return window.ChkAdvDropMetadata.IsChecked == true;
                }
                finally { window.Close(); }
            });
            Assert.False(kutu);
        }
        finally { Sil(dosya); }
    }

    /// <summary>"Ayarları sıfırla" kutuyu kapatır.</summary>
    [Fact]
    public void SifirlamaKutuyuKapatiyor()
    {
        var dosya = AyarDosyasi();
        try
        {
            var (once, sonra) = AppHost.Run(() =>
            {
                var window = new MainWindow { SettingsPathOverride = dosya };
                try
                {
                    window.ChkAdvDropMetadata.IsChecked = true;
                    var ilk = window.CaptureAppSettingsForTest().AdvDropMetadata;
                    window.ConfirmResetSettingsForTest();
                    return (ilk, window.CaptureAppSettingsForTest().AdvDropMetadata);
                }
                finally { window.Close(); }
            });

            Assert.True(once);
            Assert.False(sonra);
        }
        finally { Sil(dosya); }
    }

    /// <summary>Kuyruk penceresi seçimi taşır: çoklu dosyada da her çıktı etiketsiz çıkar.</summary>
    [Fact]
    public void KuyrukSecimiTasiyor()
    {
        var dosya = AyarDosyasi();
        try
        {
            var (kapali, acik) = AppHost.Run(() =>
            {
                var window = new MainWindow { SettingsPathOverride = dosya };
                ShrinkJobWindow? ilk = null;
                ShrinkJobWindow? ikinci = null;
                try
                {
                    ilk = window.OpenBatch(new[] { "a.mp4", "b.mp4" });
                    var once = ilk.OptionsFor(new ShrinkRequest(0, "a.mp4"), Ornek()).Options.DropMetadata;
                    window.ChkAdvDropMetadata.IsChecked = true;
                    ikinci = window.OpenBatch(new[] { "a.mp4", "b.mp4" });
                    return (once, ikinci.OptionsFor(new ShrinkRequest(0, "a.mp4"), Ornek()).Options.DropMetadata);
                }
                finally
                {
                    ilk?.Close();
                    ikinci?.Close();
                    window.Close();
                }
            });

            Assert.False(kapali);
            Assert.True(acik);
        }
        finally { Sil(dosya); }
    }

    /// <summary>İki anahtar 42 dilde dolu; ipucu her dilde üç madde (silinen, kalan, yeniden kodlama).</summary>
    [Fact]
    public void AnahtarlarButunDillerde()
    {
        Assert.Equal(42, Locales.Languages.Count);
        foreach (var language in Locales.Languages)
        {
            var values = Locales.Values(language);
            Assert.True(values.TryGetValue("main.advanced.drop-metadata.label", out var etiket) && etiket.Length > 0, language);
            Assert.True(values.TryGetValue("main.advanced.drop-metadata.tip", out var ipucu) && ipucu.Length > 0, language);
            Assert.Equal(3, ipucu!.Split('\n').Count(satir => satir.StartsWith("• ", StringComparison.Ordinal)));
        }
    }

    private static async Task<(string Baslik, string Dil)> EtiketlerAsync(string yol)
    {
        var sonuc = await AkisGirdisi.RunAsync(ToolLocator.Ffprobe,
            new[] { "-v", "error", "-show_entries", "format_tags=title:stream=codec_type:stream_tags=language", "-of", "json", yol });
        Assert.Equal(0, sonuc.Code);
        using var doc = JsonDocument.Parse(sonuc.Out);
        var baslik = doc.RootElement.GetProperty("format").TryGetProperty("tags", out var tags)
            && tags.TryGetProperty("title", out var deger) ? deger.GetString() ?? "" : "";
        var ses = doc.RootElement.GetProperty("streams").EnumerateArray()
            .First(stream => stream.GetProperty("codec_type").GetString() == "audio");
        return (baslik, Etiket(ses, "language"));
    }

    private static PlanOptions PenceredenSecenek(string kaynak, MediaInfo info, bool kutu)
    {
        var dosya = AyarDosyasi();
        try
        {
            return AppHost.Run(() =>
            {
                var window = new MainWindow { SettingsPathOverride = dosya };
                try
                {
                    window.LoadWithoutProbing(kaynak, info);
                    window.ChkAdvDropMetadata.IsChecked = kutu;
                    return window.PlanOptionsForTest();
                }
                finally { window.Close(); }
            });
        }
        finally { Sil(dosya); }
    }

    private static async Task<IReadOnlyList<string>> KodlaAsync(MediaInfo info, PlanOptions options, string cikti)
    {
        options.TargetMb = 0.4;
        options.LockedCodec = "libx264";
        options.LockedMode = EncodeMode.Crf;
        options.LockedCrf = 35;
        options.LockedPreset = "ultrafast";
        var plan = PlanCalculator.Build(info, options);
        Assert.NotEqual(EncodeMode.PassThrough, plan.ModeEnum);
        var args = FfmpegArguments.Build(info, plan, cikti, 0, null);
        await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, args);
        return args;
    }

    /// <summary>
    /// Canlı kol: başlıklı ve Türkçe sesli 2 sn'lik kaynak pencereye yüklenir, pencerenin verdiği
    /// seçenekle kodlanır. Kutu açıkken başlık düşer ve dil kalır; kapalı eşi başlığı taşır.
    /// </summary>
    [Fact]
    public async Task CanliKutuAcikkenBaslikDusuyorDilKaliyor()
    {
        var adlar = new[] { "meta-arayuz-kaynak.mkv", "meta-arayuz-temiz.mp4", "meta-arayuz-tasinan.mp4" };
        Kapat(adlar);
        try
        {
            var kaynak = Yol(adlar[0]);
            await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, new[]
            {
                "-hide_banner", "-y", "-threads", "2",
                "-f", "lavfi", "-i", "testsrc=size=320x240:rate=25:duration=2,noise=alls=60:allf=t",
                "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000:duration=2",
                "-map", "0", "-map", "1", "-c:v", "libx264", "-preset", "ultrafast", "-b:v", "3M", "-c:a", "aac",
                "-metadata", "title=Gizli Baslik", "-metadata:s:a:0", "language=tur", kaynak
            });
            Assert.Equal(("Gizli Baslik", "tur"), await EtiketlerAsync(kaynak));

            var info = await FfprobeClient.ProbeAsync(kaynak);
            var acik = PenceredenSecenek(kaynak, info, true);
            var kapali = PenceredenSecenek(kaynak, info, false);
            Assert.True(acik.DropMetadata);
            Assert.False(kapali.DropMetadata);

            var temiz = Yol(adlar[1]);
            var tasinan = Yol(adlar[2]);
            var args = await KodlaAsync(info, acik, temiz);
            await KodlaAsync(info, kapali, tasinan);

            Assert.Equal("-1", Sonraki(args, "-map_metadata"));
            Assert.Equal(("", "tur"), await EtiketlerAsync(temiz));
            Assert.Equal(("Gizli Baslik", "tur"), await EtiketlerAsync(tasinan));
        }
        finally { Kapat(adlar); }
    }
}
