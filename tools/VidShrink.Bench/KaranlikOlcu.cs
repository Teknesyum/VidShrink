using System.Diagnostics;
using System.Globalization;
using VidShrink.Ffmpeg;

public sealed record KaranlikSonuc(
    int Kare,
    long KaranlikPiksel,
    double KaranlikOran,
    double? KaranlikPsnr,
    double? TonOrani,
    double? OrtalamaKayma);

public sealed class KaranlikBirikim
{
    private int _kare;
    private long _toplamPiksel;
    private long _karanlikPiksel;
    private double _kareHata;
    private double _kayma;
    private double _tonOraniToplam;
    private int _tonKaresi;
    private readonly bool[] _kaynakTon = new bool[256];
    private readonly bool[] _testTon = new bool[256];

    public void Ekle(ReadOnlySpan<byte> kaynak, ReadOnlySpan<byte> test)
    {
        if (kaynak.Length != test.Length)
            throw new ArgumentException("Kaynak ve test karesi aynı boyda olmalı.");

        Array.Clear(_kaynakTon);
        Array.Clear(_testTon);
        _kare++;
        _toplamPiksel += kaynak.Length;
        for (var i = 0; i < kaynak.Length; i++)
        {
            var k = kaynak[i];
            if (k >= KaranlikOlcu.Esik) continue;
            var t = test[i];
            var fark = (double)t - k;
            _karanlikPiksel++;
            _kareHata += fark * fark;
            _kayma += fark;
            _kaynakTon[k] = true;
            _testTon[t] = true;
        }

        var kaynakSayi = 0;
        var testSayi = 0;
        for (var v = 0; v < 256; v++)
        {
            if (_kaynakTon[v]) kaynakSayi++;
            if (_testTon[v]) testSayi++;
        }
        if (kaynakSayi == 0) return;
        _tonOraniToplam += (double)testSayi / kaynakSayi;
        _tonKaresi++;
    }

    public KaranlikSonuc Sonuc()
    {
        double? psnr = null;
        double? kayma = null;
        if (_karanlikPiksel > 0)
        {
            var mse = _kareHata / _karanlikPiksel;
            psnr = mse <= 0 ? KaranlikOlcu.PsnrTavani : Math.Min(KaranlikOlcu.PsnrTavani, 10 * Math.Log10(255.0 * 255.0 / mse));
            kayma = _kayma / _karanlikPiksel;
        }
        return new KaranlikSonuc(
            _kare,
            _karanlikPiksel,
            _toplamPiksel == 0 ? 0 : (double)_karanlikPiksel / _toplamPiksel,
            psnr,
            _tonKaresi == 0 ? null : _tonOraniToplam / _tonKaresi,
            kayma);
    }
}

public static class KaranlikOlcu
{
    public const int Esik = 64;
    public const double PsnrTavani = 100.0;

    public static int IsParcacigi()
    {
        var deger = Environment.GetEnvironmentVariable("VIDSHRINK_BENCH_THREADS");
        return int.TryParse(deger, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > 0 ? n : 2;
    }

    public static async Task<KaranlikSonuc> OlcAsync(string kaynakYolu, string testYolu, int genislik, int yukseklik, CancellationToken iptal, string? testFps = null)
    {
        var boy = genislik * yukseklik;
        using var kaynak = Baslat(kaynakYolu, $"scale=w={genislik}:h={yukseklik}:flags=lanczos,format=gray");
        using var test = Baslat(testYolu, (string.IsNullOrWhiteSpace(testFps) ? "" : $"fps={testFps},") + $"scale=w={genislik}:h={yukseklik}:flags=lanczos,format=gray");
        var kaynakHata = kaynak.StandardError.ReadToEndAsync(iptal);
        var testHata = test.StandardError.ReadToEndAsync(iptal);

        var birikim = new KaranlikBirikim();
        var kaynakKare = new byte[boy];
        var testKare = new byte[boy];
        var kaynakAkis = kaynak.StandardOutput.BaseStream;
        var testAkis = test.StandardOutput.BaseStream;
        while (true)
        {
            var a = await OkuAsync(kaynakAkis, kaynakKare, iptal);
            var b = await OkuAsync(testAkis, testKare, iptal);
            if (!a || !b) break;
            birikim.Ekle(kaynakKare, testKare);
        }

        Kapat(kaynak);
        Kapat(test);
        await Task.WhenAll(kaynakHata, testHata);
        return birikim.Sonuc();
    }

    private static Process Baslat(string yol, string filtre)
    {
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
                     "-hide_banner", "-nostdin", "-threads", IsParcacigi().ToString(CultureInfo.InvariantCulture),
                     "-i", yol, "-an", "-sn", "-vf", filtre, "-threads", IsParcacigi().ToString(CultureInfo.InvariantCulture),
                     "-f", "rawvideo", "-pix_fmt", "gray", "-"
                 })
            psi.ArgumentList.Add(arg);
        var surec = Process.Start(psi) ?? throw new InvalidOperationException("ffmpeg başlatılamadı.");
        try { surec.PriorityClass = ProcessPriorityClass.BelowNormal; } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { }
        return surec;
    }

    private static async Task<bool> OkuAsync(Stream akis, byte[] tampon, CancellationToken iptal)
    {
        var dolu = 0;
        while (dolu < tampon.Length)
        {
            var n = await akis.ReadAsync(tampon.AsMemory(dolu), iptal);
            if (n == 0) return false;
            dolu += n;
        }
        return true;
    }

    private static void Kapat(Process surec)
    {
        try
        {
            if (!surec.HasExited)
            {
                surec.StandardOutput.BaseStream.CopyTo(Stream.Null);
                surec.WaitForExit(30000);
                if (!surec.HasExited) surec.Kill(true);
            }
        }
        catch (InvalidOperationException) { }
    }
}
