using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VidShrink.Cli;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Tanı günlüğü (E7). Kullanıcı sorun bildirirken kopyalayacağı tek yer; bu yüzden
/// günlüğün iki borcu var: her koşumu yazmak ve hiçbir satırda tam yol bırakmamak.
///
/// <para>Gizlilik burada süs değil kabul şartı: günlüğü paylaşan kullanıcı kullanıcı
/// adını, klasör ağacını ve sürücü harfini paylaşmış olmayacak. Ölçünün çekirdeği
/// <see cref="Gunluk.YoluIndirge"/>; olumlu kontrol, indirgenmemiş metinde taramanın
/// aynı yolu gerçekten bulduğunu gösteren asert.</para>
///
/// <para>Klasör ayar dosyasını izliyor: <c>VIDSHRINK_SETTINGS_PATH</c> testte proje
/// altına bakıyor, yani ölçüm gerçek <c>%APPDATA%\VidShrink</c>'e hiç dokunmuyor.</para>
/// </summary>
public sealed class TaniGunluguTests
{
    private static string[] Yollar =>
    [
        @"C:\Users\Administrator\Desktop\Projeler\VidShrink\girdi.mp4",
        @"\\sunucu\pay\arsiv\tatil 2026\kayit.mkv",
        "/home/administrator/videolar/girdi.mp4",
    ];

    private static string Klasor()
        => Path.Combine(TestPaths.OutputRoot, "tani-gunlugu", Guid.NewGuid().ToString("N"));

    [Fact]
    public void GunlukKlasoruAyarDosyasiniIzler()
    {
        var beklenen = Path.Combine(
            Path.GetDirectoryName(UpdateSettings.DefaultPath)!, Gunluk.KlasorAdi);

        Assert.Equal(Path.GetFullPath(beklenen), Path.GetFullPath(Gunluk.Klasor));
        Assert.StartsWith(
            Path.GetFullPath(TestPaths.OutputRoot),
            Path.GetFullPath(Gunluk.Klasor),
            StringComparison.OrdinalIgnoreCase);

        var gercek = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VidShrink", Gunluk.KlasorAdi);
        Assert.NotEqual(Path.GetFullPath(gercek), Path.GetFullPath(Gunluk.Klasor), StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void TamYolDosyaAdinaIner()
    {
        foreach (var yol in Yollar)
        {
            var ham = $"Input #0, from '{yol}':";
            var inik = Gunluk.YoluIndirge(ham);

            Assert.Contains(yol, ham, StringComparison.Ordinal);
            Assert.DoesNotContain(yol, inik, StringComparison.Ordinal);
            Assert.Contains(Path.GetFileName(yol.Replace('\\', '/')), inik, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void YolOlmayanMetneDokunulmaz()
    {
        const string ham = "-vf scale=1280:-2 -c:v libx264 -crf 23 -preset medium";
        Assert.Equal(ham, Gunluk.YoluIndirge(ham));
    }

    [Fact]
    public void BlokButunAlanlariTasirVeYolTasimaz()
    {
        var blok = Gunluk.Blok(
            $"ffmpeg -i \"{Yollar[0]}\" -c:v libx264 \"{Yollar[2]}\"",
            "1.4.0",
            "ffmpeg version 9.0-full_build",
            1,
            TimeSpan.FromSeconds(12.5),
            ["ilk satir", $"Error opening output file {Yollar[1]}"]);

        Assert.Contains("vidshrink: 1.4.0", blok, StringComparison.Ordinal);
        Assert.Contains("ffmpeg: ffmpeg version 9.0-full_build", blok, StringComparison.Ordinal);
        Assert.Contains("cikis: 1", blok, StringComparison.Ordinal);
        Assert.Contains("12.5 sn", blok, StringComparison.Ordinal);
        Assert.Contains("stderr:", blok, StringComparison.Ordinal);
        Assert.Contains("girdi.mp4", blok, StringComparison.Ordinal);
        Assert.Contains("kayit.mkv", blok, StringComparison.Ordinal);

        foreach (var yol in Yollar) Assert.DoesNotContain(yol, blok, StringComparison.Ordinal);
        Assert.DoesNotContain(@"C:\Users", blok, StringComparison.Ordinal);
        Assert.DoesNotContain("/home/", blok, StringComparison.Ordinal);
    }

    /// <summary>
    /// Kuyruk penceresi sayiyla pimli: <c>Gunluk.KuyrukSatiri</c> ile olculseydi sabit
    /// degisince olcu de onunla kayar, yani hicbir seyi tutmazdi (olculdu: 0 kirmizi).
    /// On bes satir ffmpeg'in son hatasini tasimaya yetiyor, gunlugu de sismiyor.
    /// </summary>
    [Fact]
    public void KuyruktanYalnizSonOnBesSatirGirer()
    {
        Assert.Equal(15, Gunluk.KuyrukSatiri);

        var satirlar = Enumerable.Range(1, 20).Select(i => "satir " + i).ToArray();
        var blok = Gunluk.Blok("ffmpeg -version", "1.4.0", "9.0", 0, TimeSpan.Zero, satirlar);

        Assert.DoesNotContain("satir 5" + Environment.NewLine, blok, StringComparison.Ordinal);
        Assert.Contains("satir 6", blok, StringComparison.Ordinal);
        Assert.Contains("satir 20", blok, StringComparison.Ordinal);
        Assert.Equal(15, blok.Split("  satir ").Length - 1);
    }

    [Fact]
    public void BosKuyrukStderrBasligiAcmaz()
    {
        var blok = Gunluk.Blok("ffmpeg -version", "1.4.0", "9.0", 0, TimeSpan.Zero, []);
        Assert.DoesNotContain("stderr:", blok, StringComparison.Ordinal);
    }

    [Fact]
    public void YazmaBlogunuEklerVeEskisiniKorur()
    {
        var klasor = Klasor();
        Assert.True(Gunluk.Yaz(klasor, "=== bir ===" + Environment.NewLine));
        Assert.True(Gunluk.Yaz(klasor, "=== iki ===" + Environment.NewLine));

        var icerik = File.ReadAllText(Path.Combine(klasor, Gunluk.DosyaAdi));
        Assert.Contains("=== bir ===", icerik, StringComparison.Ordinal);
        Assert.Contains("=== iki ===", icerik, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(klasor, Gunluk.YedekAdi)));
    }

    [Fact]
    public void EsikAsilincaTekYedekKalirVeGunlukBastanBaslar()
    {
        var klasor = Klasor();
        var dosya = Path.Combine(klasor, Gunluk.DosyaAdi);
        var yedek = Path.Combine(klasor, Gunluk.YedekAdi);

        Directory.CreateDirectory(klasor);
        File.WriteAllText(dosya, new string('x', (int)Gunluk.DevirmeSiniri - 8));
        Assert.True(Gunluk.Yaz(klasor, "=== esik alti ===" + Environment.NewLine));
        Assert.False(File.Exists(yedek));

        Assert.True(new FileInfo(dosya).Length >= Gunluk.DevirmeSiniri);
        Assert.True(Gunluk.Yaz(klasor, "=== esik ustu ===" + Environment.NewLine));

        Assert.True(File.Exists(yedek));
        Assert.Equal("=== esik ustu ===" + Environment.NewLine, File.ReadAllText(dosya));
        Assert.Contains("=== esik alti ===", File.ReadAllText(yedek), StringComparison.Ordinal);
        Assert.Equal(2, Directory.GetFiles(klasor).Length);
    }

    [Fact]
    public void YazilamayanGunlukKosumuBozmaz()
    {
        var engel = Path.Combine(TestPaths.OutputRoot, "tani-gunlugu", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.GetDirectoryName(engel)!);
        File.WriteAllText(engel, "bu bir dosya, klasor degil");

        Assert.False(Gunluk.Yaz(engel, "=== blok ===" + Environment.NewLine));
    }

    [Fact]
    public async Task CliKoluGunluguBasarVeGunluksuzdeDeCakmaz()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var services = new CliServices
        {
            MissingTool = () => throw new InvalidOperationException("araç araması koşmamalı"),
            Probe = (_, _) => throw new InvalidOperationException("yoklama koşmamalı"),
            Availability = () => throw new InvalidOperationException("yetenek sorgusu koşmamalı")
        };

        var exit = await CliApp.RunAsync(
            ["--gunluk"], stdout, stderr,
            CliText.For(CultureInfo.GetCultureInfo("en-US")), services, CancellationToken.None);

        var metin = CliText.For(CultureInfo.GetCultureInfo("en-US"));
        var gunluk = Gunluk.Oku();

        Assert.Equal(0, exit);
        Assert.Equal(string.Empty, stderr.ToString());
        Assert.Equal(
            gunluk.Length == 0 ? metin["gunluk.bos"] + Environment.NewLine : gunluk,
            stdout.ToString());
    }

    /// <summary>
    /// Canlı kol: gerçek bir ffmpeg koşumu günlüğe düşüyor mu? Blok üretimi ayrı ölçülüyor,
    /// ama <c>EncodeRunner</c>'ın günlüğü hiç çağırmaması sessiz bir kör nokta olurdu —
    /// kullanıcı sorun bildirdiğinde günlük boş çıkar. ffmpeg yoksa kol atlanıyor.
    /// </summary>
    [Fact]
    public async Task GercekKosumGunlugeDuserVeYolTasimaz()
    {
        if (!ToolLocator.IsAvailable(out _)) return;

        var klasor = Path.Combine(TestPaths.OutputRoot, "tani-gunlugu-canli", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        var kaynak = Path.Combine(klasor, "kaynak.mp4");
        var cikti = Path.Combine(klasor, "cikti.mp4");

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = ToolLocator.Ffmpeg,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in new[]
        {
            "-y", "-f", "lavfi", "-i", "testsrc=size=160x120:rate=10:duration=2",
            "-c:v", "libx264", "-crf", "30", "-pix_fmt", "yuv420p", kaynak
        }) psi.ArgumentList.Add(arg);
        using (var surec = new System.Diagnostics.Process { StartInfo = psi })
        {
            surec.Start();
            var bosalt = Task.WhenAll(surec.StandardOutput.ReadToEndAsync(), surec.StandardError.ReadToEndAsync());
            await surec.WaitForExitAsync();
            await bosalt;
        }
        Assert.True(File.Exists(kaynak));

        var oncesi = Gunluk.Oku().Length;
        var bilgi = new MediaInfo
        {
            FilePath = kaynak,
            FileSizeBytes = new FileInfo(kaynak).Length,
            DurationSeconds = 2,
            Width = 160,
            Height = 120,
            Fps = 10,
            VideoCodec = "h264",
            TotalBitrateBps = 200_000
        };
        var plan = new EncodePlan
        {
            Codec = "libx264",
            Mode = "2pass",
            VideoBitrateK = 64,
            AudioCodec = null,
            AudioBitrateK = 0,
            Width = 160,
            Height = 120,
            Fps = 10,
            Preset = "ultrafast",
            ExtraArgs = new List<string> { "-threads", "1" }
        };

        await new EncodeRunner().RunAsync(bilgi, plan, cikti, targetMb: 1, progress: null,
            fillPolicy: FillPolicy.FillTarget);

        var gunluk = Gunluk.Oku();
        Assert.True(gunluk.Length > oncesi, "kosum gunluge hic yazmadi");

        var yeni = gunluk[oncesi..];
        Assert.Contains("komut: ", yeni, StringComparison.Ordinal);
        Assert.Contains("ffmpeg: ", yeni, StringComparison.Ordinal);
        Assert.Contains("kaynak.mp4", yeni, StringComparison.Ordinal);
        Assert.DoesNotContain(klasor, yeni, StringComparison.Ordinal);
        Assert.DoesNotContain(Path.GetDirectoryName(klasor)!, yeni, StringComparison.Ordinal);
    }
}
