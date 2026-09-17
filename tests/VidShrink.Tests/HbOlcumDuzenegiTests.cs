using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using VidShrink.Cli;
using VidShrink.Core;

namespace VidShrink.Tests;

/// <summary>
/// B5 SVT kolunda HandBrake'e ürünün x265 preset adı ("slow") <c>--encoder-preset</c>
/// olarak geçirilmişti. HandBrake'in svt_av1 kodlayıcısı orada sayı bekliyor; ad
/// verildiğinde kendi varsayılanına düşüyor ve iki karanlık hücrede 400 sn'nin üzerinde
/// kodlama süresi ölçüldü. Yani o hücrelerin hız oranı düzenek hatasıydı, ürünün hükmü
/// değil. Eşleme burada ölçülüyor: betiğin kendi fonksiyonu pwsh'te koşturuluyor,
/// metinle karşılaştırılmıyor.
/// </summary>
public sealed class HbOlcumDuzenegiTests
{
    private static string Script => File.ReadAllText(Path.Combine(TipSources.Root, "tools", "kalite-paketi-3", "hb.ps1"));

    private static string? PowerShell()
    {
        foreach (var name in new[] { "pwsh", "powershell" })
        {
            var command = OperatingSystem.IsWindows() ? "where" : "which";
            try
            {
                using var probe = Process.Start(new ProcessStartInfo(command, name)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });
                if (probe is null) continue;
                var first = probe.StandardOutput.ReadLine();
                probe.WaitForExit(20000);
                if (probe.ExitCode == 0 && !string.IsNullOrWhiteSpace(first)) return first.Trim();
            }
            catch (Exception) { }
        }
        return null;
    }

    private static string Kes(string pattern)
    {
        var match = Regex.Match(Script, pattern, RegexOptions.Singleline);
        Assert.True(match.Success, $"hb.ps1 içinde bulunamadı: {pattern}");
        return match.Value;
    }

    [Fact]
    public void SvtKolununPresetEslemesiUrununAdiniSayiyaCeviriyor()
    {
        var shell = PowerShell();
        if (shell is null) return;

        var girdiler = new[] { "veryslow", "slower", "slow", "medium", "fast", "faster", "veryfast", "ultrafast", "6", "", "bilinmeyen" };
        var beklenen = new[] { "4", "5", "6", "8", "9", "10", "11", "12", "6", "8", "8" };

        var directory = Path.Combine(Path.GetTempPath(), "vidshrink-hbsvt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var script = Path.Combine(directory, "eslem.ps1");
            File.WriteAllText(script, string.Join(
                Environment.NewLine,
                Kes(@"\$script:HbSvtPresetEslemesi = @\{.*?\r?\n\}"),
                Kes(@"function HbSvtPresetNo\(\[string\]\$Preset\) \{.*?\r?\n\}"),
                "foreach ($ad in $args) { HbSvtPresetNo $ad }"));

            var baslatma = new ProcessStartInfo(shell)
            {
                ArgumentList = { "-NoProfile", "-File", script },
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var girdi in girdiler) baslatma.ArgumentList.Add(girdi.Length == 0 ? "''" : girdi);

            using var run = Process.Start(baslatma)!;
            var cikti = run.StandardOutput.ReadToEnd();
            var hata = run.StandardError.ReadToEnd();
            run.WaitForExit(120000);
            Assert.True(run.ExitCode == 0, $"pwsh çıkışı {run.ExitCode}: {hata}");

            var satirlar = cikti.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(beklenen, satirlar);
        }
        finally
        {
            try { Directory.Delete(directory, true); }
            catch (IOException) { }
        }
    }

    /// <summary>
    /// SVT kolu ürünün preset adını artık doğrudan HandBrake'e vermiyor; eşleme
    /// fonksiyonundan geçiyor. Doğrudan geçirme geri gelirse ölçüm yine düzeneği ölçer.
    /// </summary>
    [Fact]
    public void SvtKoluUrununPresetAdiniDogrudanGecirmiyor()
    {
        var svtArg = Kes(@"function HbSvtArg\(.*?\r?\n\}");
        Assert.Contains("HbSvtPresetNo $Preset", svtArg, StringComparison.Ordinal);
        Assert.DoesNotContain("if ($Preset) { $Preset }", svtArg, StringComparison.Ordinal);
        Assert.Contains("'--encoder-preset', $p", svtArg, StringComparison.Ordinal);
    }

    /// <summary>
    /// Yalnız SVT hücrelerini koşan kol var ve kapı değerleri ortak: düzeltme yeniden
    /// koşulurken B1 hücrelerinin kapısı değişmiyor.
    /// </summary>
    [Fact]
    public void YalnizSvtHucrelerinikosanKolVarVeKapilariOrtak()
    {
        var script = Script;
        Assert.Contains("'handbrakecli-svt'", script, StringComparison.Ordinal);
        Assert.Contains("function HandbrakeCliSvt", script, StringComparison.Ordinal);

        var svtIs = Kes(@"function HandbrakeCliSvt \{.*?\r?\n\}\r?\n\r?\nswitch");
        Assert.Contains("$k = $script:CliKapi", svtIs, StringComparison.Ordinal);
        Assert.DoesNotContain("'handbrake'", svtIs, StringComparison.Ordinal);
        Assert.DoesNotContain("negatif-handbrake", svtIs, StringComparison.Ordinal);

        var kapi = Kes(@"\$script:CliKapi = \[ordered\]@\{.*?\r?\n");
        Assert.Contains("bayt_sapma_yuzde = 2.0", kapi, StringComparison.Ordinal);
        Assert.Contains("hiz_orani_tavan = 1.0", kapi, StringComparison.Ordinal);
    }

    /// <summary>
    /// K4 kapısı: karanlık kesitte Dengeli kolu ölçülmeli. Rejim kaynak/hedef oranından
    /// türediği için düzenek rejimi <c>--source-mb</c> ile zorlar; betikteki
    /// <c>$DengeliOran</c> varsayılanı motorun Dengeli bandının içinde olmazsa kol
    /// başka bir rejimi ölçer ve hüküm kayar. Oran buradan okunup motora sorulur.
    /// </summary>
    [Fact]
    public void DengeliKolununZorladigiOranMotorunDengeliBandinaDusuyor()
    {
        var kol = Kes(@"Dene \$Kesit 'urun-dengeli' \$kbit \{.*?\r?\n        \}");
        Assert.Contains("'--source-mb'", kol, StringComparison.Ordinal);
        Assert.Contains("$mb * $DengeliOran", kol, StringComparison.Ordinal);
        Assert.Contains("$ek.beklenen_kodek = 'libx264'", kol, StringComparison.Ordinal);
        Assert.Contains("$ek.genisleme_kapisi", kol, StringComparison.Ordinal);
        Assert.Contains("$ek.cambi_ii -gt 7.5", kol, StringComparison.Ordinal);
        Assert.Contains("$ek.cambi_tavan = 7.5", kol, StringComparison.Ordinal);
        Assert.Contains("$ek.x265_bolu_dengeli_sure -le 2.0", kol, StringComparison.Ordinal);
        Assert.Contains("$u.Kodlayici -eq 'libx264' -and $u.Komut -like '*libx264*'", kol, StringComparison.Ordinal);

        var oranMetni = Regex.Match(Script, @"\[double\]\$DengeliOran = ([\d.]+)");
        Assert.True(oranMetni.Success, "hb.ps1 içinde $DengeliOran varsayılanı yok");
        var oran = double.Parse(oranMetni.Groups[1].Value, CultureInfo.InvariantCulture);

        foreach (var hedefMb in new[] { 0.7324, 2.4414 })
        {
            Assert.Equal(CompressionRegime.Balanced, CompressionStrategy.RegimeFor(hedefMb * oran, hedefMb));
            Assert.Equal(oran, CompressionStrategy.Ratio(hedefMb * oran, hedefMb), 3);
            Assert.Equal(CodecPreference.Compatible, CompressionStrategy.AutoPreference(CompressionStrategy.RegimeFor(hedefMb * oran, hedefMb)));
        }
    }

    /// <summary>
    /// <c>butceilk</c> kolu bütçe arama döngüsünün süresini ölçüyor: ilk denemenin
    /// süresi ile toplamı ayrı sütunlarda duruyor. Kol, kodeği CLI'nın kendi
    /// <c>--kodek</c> anahtarıyla zorluyor; zorlanan ad ile motorun kilitlediği
    /// kodlayıcı adı burada eşleştiriliyor, yoksa "x265 kolu" diye yazılan satır
    /// başka bir kodlayıcıyı ölçer. Hız tavanı ayrı bir sabit değil, B5'in
    /// kendi <c>$script:CliKapi</c> tavanı.
    /// </summary>
    [Fact]
    public void ButceIlkKoluZorladigiKodekleriMotorunKilitledigiAdlaraEsliyor()
    {
        var script = Script;
        Assert.Contains("'butceilk'", script, StringComparison.Ordinal);
        Assert.Contains("function ButceIlk", script, StringComparison.Ordinal);

        var harita = Kes(@"\$script:ButceKodekleri = \[ordered\]@\{.*?\r?\n");
        Assert.Contains("x265 = 'libx265'", harita, StringComparison.Ordinal);
        Assert.Contains("h264 = 'libx264'", harita, StringComparison.Ordinal);

        foreach (var (ad, beklenen) in new[] { ("x265", "libx265"), ("h264", "libx264") })
        {
            Assert.True(CliParser.TryParseCodec(ad, out var kodek), $"CLI --kodek {ad} tanımıyor");
            var kilit = new CliRequest { Codec = kodek }.ToPlanOptions(1.0).LockedCodec;
            if (kodek == CliCodec.Hevc) Assert.Equal(beklenen, kilit);
            else Assert.Null(kilit);
        }
        Assert.Equal(CodecPreference.Compatible, new CliRequest { Codec = CliCodec.H264 }.ToPlanOptions(1.0).Codec);

        var kol = Kes(@"function ButceIlk \{.*?\r?\n\}");
        Assert.Contains("@('--kodek', $ad)", kol, StringComparison.Ordinal);
        Assert.Contains("kodek_tuttu = ($u.Kodlayici -eq $beklenen)", kol, StringComparison.Ordinal);
        Assert.Contains("$k = $script:CliKapi", kol, StringComparison.Ordinal);
        Assert.Contains("_oran_ilk_deneme\"] = [math]::Round($u.IlkDenemeSn / $script:bhx.Sn, 3)", kol, StringComparison.Ordinal);
        Assert.Contains("_oran_toplam\"] = [math]::Round($u.ToplamSn / $script:bhx.Sn, 3)", kol, StringComparison.Ordinal);
        Assert.Contains("-le $k.hiz_orani_tavan", kol, StringComparison.Ordinal);
        Assert.DoesNotContain("hiz_orani_tavan = ", kol, StringComparison.Ordinal);
        Assert.Contains("HbEsBayt $girdi $c $hk (HbTemel $b)", kol, StringComparison.Ordinal);

        var kapi = Kes(@"\$script:CliKapi = \[ordered\]@\{.*?\r?\n");
        Assert.Contains("hiz_orani_tavan = 1.0", kapi, StringComparison.Ordinal);
    }

    /// <summary>
    /// Deneme başına süre CLI izinden okunuyor. Alan boş gelirse düzenek sıfır
    /// yazmaz, <c>Eksik</c> der: eksik ölçüm "ilk deneme 0 saniye sürdü" diye
    /// rapora giremez.
    /// </summary>
    [Fact]
    public void IzSureleriEksikAlaniSifirDiyeOkumuyor()
    {
        var f = Kes(@"function IzSureleri\(.*?\r?\n\}");
        Assert.Contains("Eksik = $true", f, StringComparison.Ordinal);
        Assert.Contains("$ler.Count -eq 0", f, StringComparison.Ordinal);
        Assert.Contains("Ilk = [math]::Round($sn[0], 1)", f, StringComparison.Ordinal);
        Assert.Contains("Disi = [math]::Round($KodlamaSn - $toplam, 1)", f, StringComparison.Ordinal);

        var urun = Kes(@"function UrunCli\(.*?\r?\n\}");
        Assert.Contains("$iz = IzSureleri $j.result.trace", urun, StringComparison.Ordinal);
        Assert.Contains("IlkDenemeSn = $iz.Ilk", urun, StringComparison.Ordinal);
    }
}
