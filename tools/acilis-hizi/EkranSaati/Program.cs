using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Win32;

namespace EkranSaati;

internal sealed record Taraf(string Etiket, string Kok, Dictionary<string, string> Ortam);

internal static class Program
{
    private const string MasaustuAdi = "vidshrink-olcum";

    private static int Main(string[] args)
    {
        Yerel.SetErrorMode(0x0001 | 0x0002 | 0x8000);
        var s = Secenek(args);
        var cikti = Path.GetFullPath(Al(s, "cikti"));
        Directory.CreateDirectory(cikti);
        var klip = Path.GetFullPath(Al(s, "klip"));
        var kalkan = Path.GetFullPath(Al(s, "kalkan"));
        if (!File.Exists(kalkan)) throw new FileNotFoundException("kalkan", kalkan);
        var yukGunlugu = s.TryGetValue("yuk", out var yg) ? Path.GetFullPath(yg) : null;
        var kip = s.GetValueOrDefault("kip", "sicak");
        var tekrar = int.Parse(s.GetValueOrDefault("tekrar", "14"), CultureInfo.InvariantCulture);
        var sinir = TimeSpan.FromSeconds(int.Parse(s.GetValueOrDefault("zaman-asimi", "60"), CultureInfo.InvariantCulture));
        var ayarSablonu = JsonNode.Parse(File.ReadAllText(Al(s, "ayar")))!.AsObject();

        var taraflar = new List<Taraf> { new(s.GetValueOrDefault("etiket-a", "a"), Path.GetFullPath(Al(s, "a")), Ortam(s.GetValueOrDefault("ortam-a", ""))) };
        if (s.TryGetValue("b", out var b))
            taraflar.Add(new(s.GetValueOrDefault("etiket-b", "b"), Path.GetFullPath(b), Ortam(s.GetValueOrDefault("ortam-b", ""))));

        var koruma = new KayitKorumasi();
        Console.WriteLine($"kayit korumasi: sag tik menusu {koruma.MenuSayisi} deger, toplam {koruma.Sayi}");

        using var masaustu = new Masaustu(MasaustuAdi);
        var kosumlar = new List<Dictionary<string, object?>>();
        var ad = string.Join("-vs-", taraflar.Select(t => t.Etiket));
        var hamYol = Path.Combine(cikti, $"ham-{ad}-{kip}.jsonl");
        File.WriteAllText(hamYol, "");

        for (var i = 1; i <= tekrar; i++)
        {
            var sira = taraflar.ToList();
            if (sira.Count == 2 && i % 2 == 0) sira.Reverse();
            foreach (var taraf in sira)
            {
                var bekleme = Stopwatch.StartNew();
                while (Yuk(yukGunlugu) is { } yuk && (yuk.Cpu > 60 || yuk.Gpu > 60))
                {
                    if (bekleme.Elapsed > TimeSpan.FromMinutes(10))
                    {
                        Console.Error.WriteLine($"SISTEM YUKU 10 DAKIKADIR YUKSEK ({yuk.Satir}), olcum durduruldu");
                        return 5;
                    }
                    Console.WriteLine($"yuk yuksek, bekleniyor: {yuk.Satir}");
                    Thread.Sleep(10000);
                }
                var yukBaslangic = DateTime.Now;
                if (Denetle(koruma, "once") is int hata) return hata;

                var kok = taraf.Kok;
                string? soguk = null;
                if (kip == "soguk")
                {
                    soguk = Path.Combine(cikti, "soguk", $"k{i}-{taraf.Etiket}");
                    SogukKopya(taraf.Kok, soguk);
                    kok = soguk;
                    Thread.Sleep(2000);
                }

                var kosumDizini = Path.Combine(cikti, "kosum", $"{kip}-{taraf.Etiket}-{i}");
                if (Directory.Exists(kosumDizini)) Sil(kosumDizini);
                Directory.CreateDirectory(kosumDizini);
                var sonuc = Kosum(taraf, kok, kosumDizini, klip, sinir, ayarSablonu, kalkan, masaustu);
                Thread.Sleep(TimeSpan.FromSeconds(yukGunlugu is null ? 0 : 13));
                if (EnYuksekCpu(yukGunlugu, yukBaslangic) is { } tepe && tepe.Cpu > 80)
                {
                    Console.Error.WriteLine($"OLCUM SIRASINDA CPU 80'I GECTI ({tepe.Satir}), tekrar {i} {taraf.Etiket} atildi, olcum durduruldu");
                    if (Denetle(koruma, "sonra") is int h) return h;
                    return 5;
                }
                sonuc["tekrar"] = i;
                sonuc["etiket"] = taraf.Etiket;
                kosumlar.Add(sonuc);
                File.AppendAllText(hamYol, JsonSerializer.Serialize(sonuc) + Environment.NewLine);
                Console.WriteLine($"{i,3} {taraf.Etiket,-12} app={Yuvarla(sonuc.GetValueOrDefault("iz:app-dogdu"))} ilk-kare={Yuvarla(sonuc.GetValueOrDefault("iz:ilk-kare"))} kalkan={sonuc.GetValueOrDefault("kalkan")}");

                if (Denetle(koruma, "sonra") is int hata2) return hata2;
                if (sonuc.GetValueOrDefault("kalkan") is not true)
                {
                    Console.Error.WriteLine("KALKAN KURULMADI, olcum durduruldu");
                    return 6;
                }

                if (soguk is not null) Sil(soguk);
                Thread.Sleep(1000);
            }
        }

        var ozet = Ozet(kosumlar, taraflar, kip, klip, tekrar);
        var ozetYol = Path.Combine(cikti, $"ozet-{ad}-{kip}.txt");
        File.WriteAllText(ozetYol, ozet);
        Console.WriteLine(ozet);
        Console.WriteLine("ham: " + hamYol);
        Console.WriteLine("ozet: " + ozetYol);
        return 0;
    }

    private static int? Denetle(KayitKorumasi koruma, string an)
    {
        var (geri, fazla) = koruma.GeriYaz();
        if (geri == 0 && fazla == 0) return null;
        Console.Error.WriteLine($"KAYIT DEFTERI DEGISTI ({an}): {geri} deger geri yazildi, {fazla} fazla deger silindi; olcum durduruldu");
        return 3;
    }

    private static Dictionary<string, object?> Kosum(Taraf taraf, string kok, string kosumDizini, string klip,
        TimeSpan sinir, JsonObject ayarSablonu, string kalkan, Masaustu masaustu)
    {
        var sonuc = new Dictionary<string, object?>();
        var baslatici = Path.Combine(kok, "VidShrink.exe");

        var ayar = (JsonObject)ayarSablonu.DeepClone();
        ayar["fileAssociationRegisteredFor"] = baslatici;
        var ayarYolu = Path.Combine(kosumDizini, "ayar", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(ayarYolu)!);
        File.WriteAllText(ayarYolu, ayar.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var izYolu = Path.Combine(kosumDizini, "iz.txt");
        var kovanDizini = Path.Combine(kosumDizini, "kovan");
        var ortam = new Dictionary<string, string?>
        {
            ["VIDSHRINK_SETTINGS_PATH"] = ayarYolu,
            ["VIDSHRINK_INSTANCE_CHANNEL"] = "olcum-" + Guid.NewGuid().ToString("N"),
            ["VIDSHRINK_ACILIS_IZI"] = izYolu,
            ["VIDSHRINK_OLCUM_KOVAN"] = kovanDizini,
            ["DOTNET_STARTUP_HOOKS"] = kalkan,
            ["DOTNET_DbgEnableMiniDump"] = "0",
        };
        foreach (var (anahtar, deger) in taraf.Ortam) ortam[anahtar] = deger;
        var eski = ortam.Keys.ToDictionary(k => k, Environment.GetEnvironmentVariable);
        foreach (var (anahtar, deger) in ortam) Environment.SetEnvironmentVariable(anahtar, deger);

        try
        {
            var saat = Stopwatch.StartNew();
            var surec = masaustu.Baslat($"\"{baslatici}\" \"{klip}\"", kok);
            sonuc["createprocess"] = Math.Round(saat.Elapsed.TotalMilliseconds, 1);
            Yerel.CloseHandle(surec);

            while (saat.Elapsed < sinir)
            {
                if (File.Exists(izYolu) && Satirlar(izYolu).Any(l => l.StartsWith("ilk-kare\t", StringComparison.Ordinal))) break;
                Thread.Sleep(20);
            }
            sonuc["zaman-asimi"] = saat.Elapsed >= sinir;
            Thread.Sleep(500);
        }
        finally
        {
            Oldur(kok);
            foreach (var (anahtar, deger) in eski) Environment.SetEnvironmentVariable(anahtar, deger);
        }

        var kalkanlar = Directory.Exists(kovanDizini) ? Directory.GetFiles(kovanDizini, "*.kalkan") : Array.Empty<string>();
        sonuc["kalkan-surec"] = (double)kalkanlar.Length;
        foreach (var hata in Directory.Exists(kovanDizini) ? Directory.GetFiles(kovanDizini, "*.hata") : Array.Empty<string>())
            Console.Error.WriteLine("kalkan hatasi: " + File.ReadAllText(hata));
        sonuc["kalkan"] = kalkanlar.Any(f => File.ReadAllText(f).EndsWith(@"\app\VidShrink.App.exe", StringComparison.OrdinalIgnoreCase));

        if (File.Exists(izYolu))
        {
            foreach (var satir in Satirlar(izYolu))
            {
                var p = satir.Split('\t');
                if (p.Length != 2 || sonuc.ContainsKey("iz:" + p[0])) continue;
                if (double.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var ms))
                    sonuc["iz:" + p[0]] = Math.Round(ms, 1);
            }
        }
        return sonuc;
    }

    private static string[] Satirlar(string yol)
    {
        try
        {
            using var akis = new FileStream(yol, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var okuyucu = new StreamReader(akis);
            return okuyucu.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
        catch (IOException) { return Array.Empty<string>(); }
    }

    private sealed record YukSatiri(int Cpu, int Gpu, string Satir);

    private static YukSatiri? EnYuksekCpu(string? gunluk, DateTime baslangic)
    {
        if (gunluk is null || !File.Exists(gunluk)) return null;
        YukSatiri? tepe = null;
        foreach (var satir in Satirlar(gunluk).Reverse().Take(60))
        {
            if (!TimeSpan.TryParse(satir.Split(' ')[0], CultureInfo.InvariantCulture, out var saat)) continue;
            var an = DateTime.Today + saat;
            if (an > DateTime.Now.AddMinutes(1)) an = an.AddDays(-1);
            if (an < baslangic.AddSeconds(-1)) break;
            if (YukCoz(satir) is { } y && (tepe is null || y.Cpu > tepe.Cpu)) tepe = y;
        }
        return tepe;
    }

    private static YukSatiri? Yuk(string? gunluk)
    {
        if (gunluk is null || !File.Exists(gunluk)) return null;
        var son = Satirlar(gunluk).LastOrDefault();
        return son is null ? null : YukCoz(son);
    }

    private static YukSatiri? YukCoz(string son)
    {
        int Deger(string ad)
        {
            var i = son.IndexOf(ad + "=", StringComparison.Ordinal);
            if (i < 0) return 0;
            var bas = i + ad.Length + 1;
            var bit = bas;
            while (bit < son.Length && char.IsDigit(son[bit])) bit++;
            return int.TryParse(son[bas..bit], out var v) ? v : 0;
        }
        return new YukSatiri(Deger("cpu"), Deger("gpu"), son);
    }

    private static void Oldur(string kok)
    {
        var onek = kok.TrimEnd('\\') + "\\";
        for (var tur = 0; tur < 3; tur++)
        {
            var bulundu = false;
            foreach (var p in Process.GetProcessesByName("VidShrink").Concat(Process.GetProcessesByName("VidShrink.App")))
            {
                var yol = Yerel.SurecYolu(p.Id);
                if (yol is not null && yol.StartsWith(onek, StringComparison.OrdinalIgnoreCase))
                {
                    bulundu = true;
                    try { p.Kill(); p.WaitForExit(10000); } catch (Exception) { }
                }
                p.Dispose();
            }
            if (!bulundu) break;
            Thread.Sleep(200);
        }
    }

    private static void SogukKopya(string kaynak, string hedef)
    {
        Sil(hedef);
        Directory.CreateDirectory(hedef);
        var r = Process.Start(new ProcessStartInfo("robocopy", $"\"{kaynak}\" \"{hedef}\" /E /J /XD ffmpeg /NFL /NDL /NJH /NJS /NP") { UseShellExecute = false, RedirectStandardOutput = true })!;
        r.StandardOutput.ReadToEnd();
        r.WaitForExit();
        if (r.ExitCode >= 8) throw new InvalidOperationException("robocopy " + r.ExitCode);
        var ff = Path.Combine(hedef, "tools", "ffmpeg");
        Directory.CreateDirectory(ff);
        foreach (var ad in new[] { "ffmpeg.exe", "ffprobe.exe" })
        {
            var kaynakDosya = Path.Combine(kaynak, "tools", "ffmpeg", ad);
            if (File.Exists(kaynakDosya)) Yerel.CreateHardLink(Path.Combine(ff, ad), kaynakDosya, IntPtr.Zero);
        }
    }

    private static void Sil(string yol)
    {
        for (var tur = 0; tur < 20 && Directory.Exists(yol); tur++)
        {
            try { Directory.Delete(yol, true); }
            catch (Exception) { Thread.Sleep(250); }
        }
    }

    private static string Ozet(List<Dictionary<string, object?>> kosumlar, List<Taraf> taraflar, string kip, string klip, int tekrar)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"kip      : {kip}");
        sb.AppendLine($"klip     : {klip} ({new FileInfo(klip).Length / 1048576.0:0.0} MB)");
        sb.AppendLine($"tekrar   : {tekrar}");
        sb.AppendLine($"makine   : {Environment.MachineName} / {Environment.OSVersion.VersionString}");
        sb.AppendLine($"an       : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"masaustu : WinSta0\\{MasaustuAdi}; kullanicinin ekranina pencere acilmaz, ekran okunmaz.");
        sb.AppendLine("saat     : ms, uygulamanin kendi izi. Sifir baslaticinin Process.StartTime'i; app satirlari");
        sb.AppendLine("           ayni tabana (VIDSHRINK_ACILIS_T0) yazar. Kabugun CreateProcess oncesi payi girmez.");
        sb.AppendLine("kayit    : her surece KayitKalkani baslangic kancasi; HKCU ozel kovana yonlenir. Her kosumdan");
        sb.AppendLine("           once ve sonra sag tik menusu, etiketler ve iliskilendirme degerleri karsilastirilir.");
        foreach (var t in taraflar) sb.AppendLine($"taraf    : {t.Etiket} = {t.Kok} ortam={string.Join(";", t.Ortam.Select(o => o.Key + "=" + o.Value))}");
        sb.AppendLine();

        var anahtarlar = kosumlar.SelectMany(k => k.Keys).Where(k => k is not "tekrar" and not "etiket").Distinct().ToList();
        anahtarlar = anahtarlar.OrderBy(k => Ortanca(kosumlar.Select(x => x.GetValueOrDefault(k)).OfType<double>().ToArray()) ?? 1e9).ToList();

        foreach (var t in taraflar)
        {
            sb.AppendLine($"== {t.Etiket} ==");
            sb.AppendLine($"{"adim",-34}{"n",4}{"en_az",10}{"ortanca",10}{"p95",10}{"en_cok",10}");
            foreach (var a in anahtarlar)
            {
                var d = kosumlar.Where(k => (string)k["etiket"]! == t.Etiket).Select(k => k.GetValueOrDefault(a)).OfType<double>().OrderBy(x => x).ToArray();
                if (d.Length == 0) continue;
                sb.AppendLine($"{a,-34}{d.Length,4}{d[0],10:0.0}{Ortanca(d),10:0.0}{d[Math.Max(0, (int)Math.Ceiling(0.95 * d.Length) - 1)],10:0.0}{d[^1],10:0.0}");
            }
            sb.AppendLine();
        }

        if (taraflar.Count == 2)
        {
            var (a, b) = (taraflar[0].Etiket, taraflar[1].Etiket);
            sb.AppendLine($"== eslesik fark ({b} eksi {a}, eksi deger {b} lehine) ==");
            sb.AppendLine($"{"adim",-34}{"cift",5}{"ortanca",10}{"en_az",10}{"en_cok",10}{"lehine",8}");
            foreach (var k in anahtarlar)
            {
                var farklar = new List<double>();
                for (var i = 1; i <= tekrar; i++)
                {
                    var x = kosumlar.FirstOrDefault(r => (int)r["tekrar"]! == i && (string)r["etiket"]! == a)?.GetValueOrDefault(k);
                    var y = kosumlar.FirstOrDefault(r => (int)r["tekrar"]! == i && (string)r["etiket"]! == b)?.GetValueOrDefault(k);
                    if (x is double xd && y is double yd) farklar.Add(yd - xd);
                }
                if (farklar.Count == 0) continue;
                var s = farklar.OrderBy(v => v).ToArray();
                sb.AppendLine($"{k,-34}{s.Length,5}{Ortanca(s),10:0.0}{s[0],10:0.0}{s[^1],10:0.0}{s.Count(v => v < 0),5}/{s.Length}");
            }
        }
        return sb.ToString();
    }

    private static double? Ortanca(double[] d)
    {
        if (d.Length == 0) return null;
        var s = d.OrderBy(x => x).ToArray();
        return s.Length % 2 == 1 ? s[s.Length / 2] : (s[s.Length / 2 - 1] + s[s.Length / 2]) / 2;
    }

    private static string Yuvarla(object? v) => v is double d ? d.ToString("0", CultureInfo.InvariantCulture) : "-";

    private static Dictionary<string, string> Ortam(string metin)
        => metin.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .ToDictionary(p => p[0], p => p.Length > 1 ? p[1] : "");

    private static Dictionary<string, string> Secenek(string[] args)
    {
        var s = new Dictionary<string, string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--")) continue;
            var ad = args[i][2..];
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--")) s[ad] = args[++i];
            else s[ad] = "";
        }
        return s;
    }

    private static string Al(Dictionary<string, string> s, string ad)
        => s.TryGetValue(ad, out var v) ? v : throw new ArgumentException("--" + ad + " gerekli");
}

internal sealed class KayitKorumasi
{
    private const string Menuler = @"Software\Classes\SystemFileAssociations";
    private static readonly string[] Agaclar =
    {
        @"Software\Teknesyum\VidShrink\ShellLabels",
        @"Software\Classes\Teknesyum.VidShrink.Video",
    };

    private readonly Dictionary<(string Anahtar, string Ad), (object Deger, RegistryValueKind Tur)> _ilk = Oku();

    public int Sayi => _ilk.Count;
    public int MenuSayisi => _ilk.Keys.Count(k => k.Anahtar.StartsWith(Menuler, StringComparison.OrdinalIgnoreCase));

    private static Dictionary<(string, string), (object, RegistryValueKind)> Oku()
    {
        var sonuc = new Dictionary<(string, string), (object, RegistryValueKind)>();
        using (var kok = Registry.CurrentUser.OpenSubKey(Menuler))
        {
            foreach (var uzanti in kok?.GetSubKeyNames() ?? Array.Empty<string>())
                foreach (var menu in new[] { "VidShrink", "VidShrinkKucult" })
                    Topla($@"{Menuler}\{uzanti}\shell\{menu}", sonuc);
        }
        foreach (var agac in Agaclar) Topla(agac, sonuc);
        return sonuc;
    }

    private static void Topla(string yol, Dictionary<(string, string), (object, RegistryValueKind)> sonuc)
    {
        using var k = Registry.CurrentUser.OpenSubKey(yol);
        if (k is null) return;
        foreach (var ad in k.GetValueNames())
            sonuc[(yol, ad)] = (k.GetValue(ad, null, RegistryValueOptions.DoNotExpandEnvironmentNames)!, k.GetValueKind(ad));
        foreach (var alt in k.GetSubKeyNames()) Topla(yol + "\\" + alt, sonuc);
    }

    public (int GeriYazilan, int Fazla) GeriYaz()
    {
        var simdi = Oku();
        var geri = 0;
        foreach (var ((anahtar, ad), (deger, tur)) in _ilk)
        {
            if (simdi.TryGetValue((anahtar, ad), out var s) && s.Item2 == tur && Esit(s.Item1, deger)) continue;
            using var k = Registry.CurrentUser.CreateSubKey(anahtar);
            k.SetValue(ad, deger, tur);
            geri++;
        }
        var fazla = 0;
        foreach (var (anahtar, ad) in simdi.Keys.Where(a => !_ilk.ContainsKey(a)))
        {
            using var k = Registry.CurrentUser.OpenSubKey(anahtar, writable: true);
            k?.DeleteValue(ad, throwOnMissingValue: false);
            fazla++;
        }
        return (geri, fazla);
    }

    private static bool Esit(object a, object b)
        => a is byte[] x && b is byte[] y ? x.AsSpan().SequenceEqual(y) : a is string[] p && b is string[] q ? p.SequenceEqual(q) : Equals(a, b);
}

internal sealed class Masaustu : IDisposable
{
    private readonly IntPtr _tutamak;
    private readonly string _tamAd;

    public Masaustu(string ad)
    {
        _tutamak = Yerel.CreateDesktop(ad, IntPtr.Zero, IntPtr.Zero, 0, 0x10000000, IntPtr.Zero);
        if (_tutamak == IntPtr.Zero) throw new InvalidOperationException("CreateDesktop " + Marshal.GetLastWin32Error());
        _tamAd = @"WinSta0\" + ad;
    }

    public IntPtr Baslat(string komut, string dizin)
    {
        var si = new Yerel.STARTUPINFO { cb = Marshal.SizeOf<Yerel.STARTUPINFO>(), lpDesktop = _tamAd };
        if (!Yerel.CreateProcess(null, new StringBuilder(komut), IntPtr.Zero, IntPtr.Zero, false, 0, IntPtr.Zero, dizin, ref si, out var pi))
            throw new InvalidOperationException("CreateProcess " + Marshal.GetLastWin32Error());
        Yerel.CloseHandle(pi.hThread);
        return pi.hProcess;
    }

    public void Dispose() => Yerel.CloseDesktop(_tutamak);
}

internal static class Yerel
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct STARTUPINFO
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESS_INFORMATION
    {
        public IntPtr hProcess, hThread;
        public int dwProcessId, dwThreadId;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern IntPtr CreateDesktop(string name, IntPtr device, IntPtr mode, int flags, uint access, IntPtr sa);
    [DllImport("user32.dll")] public static extern bool CloseDesktop(IntPtr h);
    [DllImport("kernel32.dll")] public static extern uint SetErrorMode(uint mode);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool CreateProcess(string? app, StringBuilder cmd, IntPtr pa, IntPtr ta, bool inherit, int flags, IntPtr env, string? dir, ref STARTUPINFO si, out PROCESS_INFORMATION pi);
    [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(int access, bool inherit, int pid);
    [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern bool QueryFullProcessImageName(IntPtr h, int flags, StringBuilder sb, ref int size);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern bool CreateHardLink(string yeni, string mevcut, IntPtr sa);

    public static string? SurecYolu(int pid)
    {
        var h = OpenProcess(0x1000, false, pid);
        if (h == IntPtr.Zero) return null;
        try
        {
            var sb = new StringBuilder(1024);
            var n = sb.Capacity;
            return QueryFullProcessImageName(h, 0, sb, ref n) ? sb.ToString() : null;
        }
        finally { CloseHandle(h); }
    }
}
