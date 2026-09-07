using System.Diagnostics;
using System.Globalization;
using VidShrink.Ffmpeg;
using VidShrink.Ffmpeg.Playback;

var komut = args.Length > 0 ? args[0] : "hepsi";
Directory.CreateDirectory(CalismaKok());

switch (komut)
{
    case "stderr": await StderrOlc(); break;
    case "arama": await AramaOlc(); break;
    case "oldurme": OldurmeOlc(); break;
    case "pay": await PayOlc(); break;
    case "hepsi":
        await StderrOlc();
        await AramaOlc();
        OldurmeOlc();
        await PayOlc();
        break;
    default:
        Console.WriteLine("kullanim: boru-kapanis [stderr|arama|oldurme|pay|hepsi]");
        return 1;
}
return 0;

static string KokBul()
{
    var d = new DirectoryInfo(AppContext.BaseDirectory);
    while (d is not null && !File.Exists(Path.Combine(d.FullName, "VidShrink.sln"))) d = d.Parent;
    return d?.FullName ?? AppContext.BaseDirectory;
}

static string CalismaKok() => Path.Combine(KokBul(), ".calisma", "boru-kapanis");

static string Ondalik(double d) => d.ToString("0.###", CultureInfo.InvariantCulture);

static string Bir(double d) => d.ToString("0.0", CultureInfo.InvariantCulture);

static string Y(List<double> sirali, double oran)
{
    var i = (int)Math.Ceiling(oran * sirali.Count) - 1;
    return Bir(sirali[Math.Clamp(i, 0, sirali.Count - 1)]);
}

static string[] KlipArgs(string boyut, int sure, double anahtarAralik, string cikti) => new[]
{
    "-y", "-f", "lavfi", "-i", "testsrc=size=" + boyut + ":rate=30:duration=" + sure,
    "-force_key_frames", "expr:gte(t,n_forced*" + Ondalik(anahtarAralik) + ")",
    "-pix_fmt", "yuv420p", "-c:v", "libx264", "-preset", "ultrafast", "-an", cikti
};

static ProcessStartInfo Sessiz()
{
    return new ProcessStartInfo(ToolLocator.Ffmpeg)
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };
}

static async Task<string> KlipUret(string boyut, int sure, double aralik)
{
    var ad = "klip-" + boyut.Replace("x", "-") + "-" + sure + "-" + Ondalik(aralik) + ".mkv";
    var yol = Path.Combine(CalismaKok(), ad);
    if (File.Exists(yol)) return yol;
    var psi = Sessiz();
    foreach (var a in KlipArgs(boyut, sure, aralik, yol)) psi.ArgumentList.Add(a);
    using var p = Process.Start(psi)!;
    var o = p.StandardOutput.ReadToEndAsync();
    var e = p.StandardError.ReadToEndAsync();
    await o;
    await e;
    await p.WaitForExitAsync();
    return yol;
}

static async Task StderrOlc()
{
    Console.WriteLine("## 1. stderr bayt sayisi (cikti yoluna bagli)");
    Console.WriteLine("| cikti dosyasi | tam yol uzunlugu | stderr bayt |");
    Console.WriteLine("| --- | --- | --- |");
    foreach (var ad in new[] { "s.mkv", "stderr-olcumu.mkv", "cok-daha-uzun-bir-cikti-adi-ornegi.mkv" })
    {
        var yol = Path.Combine(CalismaKok(), ad);
        var psi = Sessiz();
        foreach (var a in KlipArgs("320x180", 2, 1.0, yol)) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var hata = await p.StandardError.ReadToEndAsync();
        await p.StandardOutput.ReadToEndAsync();
        await p.WaitForExitAsync();
        var bayt = System.Text.Encoding.UTF8.GetByteCount(hata);
        Console.WriteLine("| " + ad + " | " + yol.Length + " | " + bayt + " |");
    }
    Console.WriteLine("| Windows anonim boru tamponu | - | 4096 |");
    Console.WriteLine();
}

static async Task AramaOlc()
{
    Console.WriteLine("## 2. Arama gecikmesi (120 arama, tohum 7, 0-19 sn)");
    Console.WriteLine("| duzenek | n | p50 | p90 | p99 | max | baslatilan ffmpeg | null | sizinti |");
    Console.WriteLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- |");
    var duzenekler = new (string Etiket, string Boyut, long Tavan)[]
    {
        ("320x180, onbellek 2 kare", "320x180", 320L * 180 * 4 * 2),
        ("1920x1080, onbellek 2 kare", "1920x1080", 1920L * 1080 * 4 * 2),
        ("1920x1080, varsayilan onbellek", "1920x1080", DecoderPipe.DefaultCacheByteCeiling)
    };
    foreach (var (etiket, boyut, tavan) in duzenekler)
    {
        var klip = await KlipUret(boyut, 20, 1.0);
        var oncekiler = Process.GetProcessesByName("ffmpeg").Select(p => p.Id).ToHashSet();
        var olcumler = new List<double>();
        var bosDonus = 0;
        int baslatilan;
        using (var boru = new DecoderPipe(tavan))
        {
            await boru.OpenAsync(klip);
            var rnd = new Random(7);
            for (var i = 0; i < 120; i++)
            {
                var t = Math.Round(rnd.NextDouble() * 19.0, 3);
                var sw = Stopwatch.StartNew();
                var kare = await boru.SeekAsync(t);
                sw.Stop();
                olcumler.Add(sw.Elapsed.TotalMilliseconds);
                if (kare is null) bosDonus++;
            }
            baslatilan = boru.ProcessesStarted;
        }
        await Task.Delay(500);
        var sizinti = Process.GetProcessesByName("ffmpeg").Count(p => !oncekiler.Contains(p.Id));
        olcumler.Sort();
        Console.WriteLine("| " + etiket + " | 120 | " + Y(olcumler, 0.50) + " | " + Y(olcumler, 0.90)
            + " | " + Y(olcumler, 0.99) + " | " + Bir(olcumler[^1]) + " | " + baslatilan
            + " | " + bosDonus + " | " + sizinti + " |");
    }
    Console.WriteLine();
}

static void OldurmeOlc()
{
    Console.WriteLine("## 3. Oldurme suresi (20 surec, 60 ms sonra Kill(true) + WaitForExit)");
    var olcumler = new List<double>();
    for (var i = 0; i < 20; i++)
    {
        var psi = Sessiz();
        foreach (var a in new[]
        {
            "-hide_banner", "-nostdin", "-loglevel", "error",
            "-f", "lavfi", "-i", "testsrc=size=1920x1080:rate=30",
            "-fps_mode", "passthrough", "-an", "-sn", "-dn",
            "-f", "rawvideo", "-pix_fmt", "bgra", "-"
        }) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        Thread.Sleep(60);
        var sw = Stopwatch.StartNew();
        p.Kill(true);
        p.WaitForExit();
        sw.Stop();
        olcumler.Add(sw.Elapsed.TotalMilliseconds);
    }
    olcumler.Sort();
    Console.WriteLine("| n | min | p50 | p90 | p99 | max |");
    Console.WriteLine("| --- | --- | --- | --- | --- | --- |");
    Console.WriteLine("| 20 | " + Bir(olcumler[0]) + " | " + Y(olcumler, 0.50) + " | " + Y(olcumler, 0.90)
        + " | " + Y(olcumler, 0.99) + " | " + Bir(olcumler[^1]) + " |");
    Console.WriteLine();
}

static async Task PayOlc()
{
    Console.WriteLine("## 4. Cozucu canlilik payi (SeekAsync(1.0) dondukten sonra ffmpeg ne kadar yasiyor)");
    Console.WriteLine("| klip | anahtar araligi | anahtar kare | pay ms (3 kosum) |");
    Console.WriteLine("| --- | --- | --- | --- |");
    var adaylar = new (string Boyut, int Sure, double Aralik)[]
    {
        ("640x480", 6, 0.1),
        ("640x480", 20, 0.05),
        ("1280x720", 20, 0.05)
    };
    foreach (var (boyut, sure, aralik) in adaylar)
    {
        var klip = await KlipUret(boyut, sure, aralik);
        var paylar = new List<long>();
        for (var r = 0; r < 3; r++)
        {
            var oncekiler = Process.GetProcessesByName("ffmpeg").Select(p => p.Id).ToHashSet();
            using var boru = new DecoderPipe();
            await boru.OpenAsync(klip);
            await boru.SeekAsync(1.0);
            var sw = Stopwatch.StartNew();
            var cocuk = Process.GetProcessesByName("ffmpeg").FirstOrDefault(p => !oncekiler.Contains(p.Id));
            if (cocuk is null)
            {
                paylar.Add(0);
                continue;
            }
            while (!cocuk.HasExited && sw.ElapsedMilliseconds < 10000) Thread.Sleep(5);
            sw.Stop();
            paylar.Add(sw.ElapsedMilliseconds);
            cocuk.Dispose();
        }
        var kareSayisi = (int)Math.Round(sure / aralik);
        Console.WriteLine("| " + boyut + ", " + sure + " sn | " + Ondalik(aralik) + " | " + kareSayisi
            + " | " + string.Join(" / ", paylar) + " |");
    }
    Console.WriteLine();
}
