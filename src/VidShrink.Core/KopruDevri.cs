using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace VidShrink.Core;

public static class KopruSabitleri
{
    public const string Etiket = "v0.0.1";

    public const string Surum = "0.0.1";

    public const string YayinKoku = "https://github.com/Teknesyum/VidShrink-Player/releases/download/" + Etiket + "/";

    public const string WindowsKurucuAdi = "VidShrinkPlayer-Setup.exe";

    public const string WindowsKurucuUrl = YayinKoku + WindowsKurucuAdi;

    public const string WindowsKurucuSha256 = "";

    public const string MacArmArsivAdi = "vidshrink-osx-arm64.zip";

    public const string MacArmArsivUrl = YayinKoku + MacArmArsivAdi;

    public const string MacArmArsivSha256 = "";

    public const string MacX64ArsivAdi = "vidshrink-osx-x64.zip";

    public const string MacX64ArsivUrl = YayinKoku + MacX64ArsivAdi;

    public const string MacX64ArsivSha256 = "";

    public const string YeniKurulumKlasoru = "VidShrink Player";

    public const string YeniBaslatici = "VidShrinkPlayer.exe";

    public const string YeniKaldirmaAnahtari = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\VidShrinkPlayer";

    public const string YeniMacPaketi = "VidShrink Player.app";

    public const string YeniMacAnaIkili = "VidShrinkPlayer";

    public const string YeniMacYedekIkili = "VidShrinkPlayer.App";

    public const string MacHazirlikKlasoru = ".vidshrink-kopru";

    public static readonly IReadOnlyList<string> WindowsKurucuArgumanlari = new[] { "--silent", "--tag", Etiket };

    public static readonly TimeSpan KurulumSuresi = TimeSpan.FromMinutes(15);

    public static readonly TimeSpan IndirmeSuresi = TimeSpan.FromMinutes(10);
}

public enum KopruSonucu
{
    OzetYok,
    IndirmeDustu,
    OzetTutmadi,
    KurulumDustu,
    ImzaGecersiz,
    YeniUygulamaYok,
    BaslatmaDustu,
    Tamam
}

public sealed class KopruOrtami
{
    public required Func<string, string, CancellationToken, Task> Indir { get; init; }

    public required Func<string, string> Ozet { get; init; }

    public required Func<string, IReadOnlyList<string>, TimeSpan, int> CalistirBekle { get; init; }

    public required Func<string, IReadOnlyList<string>, bool> Baslat { get; init; }

    public required Func<string, bool> DosyaVar { get; init; }

    public required Func<string, bool> KlasorVar { get; init; }

    public required Action<string, string> ArsiviAc { get; init; }

    public required Action<string, string> KlasoruTasi { get; init; }

    public required Action<string> KlasoruSil { get; init; }

    public Func<string?> KayitliKurulumKoku { get; init; } = () => null;

    public static KopruOrtami Gercek { get; } = new()
    {
        Indir = GercekIndir,
        Ozet = UpdateCheck.HashFile,
        CalistirBekle = GercekCalistirBekle,
        Baslat = GercekBaslat,
        DosyaVar = File.Exists,
        KlasorVar = Directory.Exists,
        ArsiviAc = (arsiv, hedef) => ZipFile.ExtractToDirectory(arsiv, hedef, overwriteFiles: true),
        KlasoruTasi = Directory.Move,
        KlasoruSil = yol => { if (Directory.Exists(yol)) Directory.Delete(yol, recursive: true); },
        KayitliKurulumKoku = GercekKayitliKok
    };

    private static async Task GercekIndir(string url, string hedef, CancellationToken iptal)
    {
        var klasor = Path.GetDirectoryName(hedef);
        if (!string.IsNullOrEmpty(klasor)) Directory.CreateDirectory(klasor);
        using var istemci = new HttpClient { Timeout = KopruSabitleri.IndirmeSuresi };
        istemci.DefaultRequestHeaders.UserAgent.ParseAdd("VidShrink-Kopru");
        using var yanit = await istemci.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, iptal);
        yanit.EnsureSuccessStatusCode();
        await using var dosya = File.Create(hedef);
        await yanit.Content.CopyToAsync(dosya, iptal);
    }

    private static int GercekCalistirBekle(string dosya, IReadOnlyList<string> argumanlar, TimeSpan sure)
    {
        var baslangic = new ProcessStartInfo(dosya)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(dosya) ?? ""
        };
        foreach (var arguman in argumanlar) baslangic.ArgumentList.Add(arguman);
        using var surec = Process.Start(baslangic);
        if (surec is null) return -1;
        surec.OutputDataReceived += (_, _) => { };
        surec.ErrorDataReceived += (_, _) => { };
        surec.BeginOutputReadLine();
        surec.BeginErrorReadLine();
        if (!surec.WaitForExit(sure)) return -2;
        surec.WaitForExit();
        return surec.ExitCode;
    }

    private static bool GercekBaslat(string dosya, IReadOnlyList<string> argumanlar)
    {
        var baslangic = new ProcessStartInfo(dosya)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(dosya) ?? ""
        };
        foreach (var arguman in argumanlar) baslangic.ArgumentList.Add(arguman);
        using var surec = Process.Start(baslangic);
        return surec is not null;
    }

    private static string? GercekKayitliKok()
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            using var anahtar = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(KopruSabitleri.YeniKaldirmaAnahtari);
            return anahtar?.GetValue("InstallLocation") as string;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return null;
        }
    }
}

public static class KopruDevri
{
    public static bool Uygun(bool portable, string? settingsPathOverride, string? settingsPathEnvironment, string? updateDisabled, bool autoUpdate)
        => !portable
           && string.IsNullOrWhiteSpace(settingsPathOverride)
           && string.IsNullOrWhiteSpace(settingsPathEnvironment)
           && updateDisabled != "1"
           && autoUpdate;

    public static string WindowsYeniBaslatici(KopruOrtami ortam, string localAppData)
    {
        var kayitli = ortam.KayitliKurulumKoku();
        if (!string.IsNullOrWhiteSpace(kayitli))
        {
            var aday = Path.Combine(kayitli, KopruSabitleri.YeniBaslatici);
            if (ortam.DosyaVar(aday)) return aday;
        }
        return Path.Combine(localAppData, "Programs", KopruSabitleri.YeniKurulumKlasoru, KopruSabitleri.YeniBaslatici);
    }

    public static async Task<KopruSonucu> WindowsAsync(
        KopruOrtami ortam,
        string calismaKlasoru,
        string localAppData,
        string? settingsPath,
        CancellationToken iptal,
        string beklenenOzet = KopruSabitleri.WindowsKurucuSha256)
    {
        if (string.IsNullOrWhiteSpace(beklenenOzet))
            return Dustu(settingsPath, KopruSonucu.OzetYok, "kopru-ozet-yok");

        var kurulu = WindowsYeniBaslatici(ortam, localAppData);
        if (ortam.DosyaVar(kurulu))
            return YeniyiBaslat(ortam, kurulu, Array.Empty<string>(), settingsPath, "kopru-baslatma-dustu", null);

        var kurucu = Path.Combine(calismaKlasoru, KopruSabitleri.WindowsKurucuAdi);
        try
        {
            await ortam.Indir(KopruSabitleri.WindowsKurucuUrl, kurucu, iptal);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Dustu(settingsPath, KopruSonucu.IndirmeDustu, "kopru-indirme " + exception.GetType().Name + ": " + exception.Message);
        }

        if (!OzetTutar(ortam, kurucu, beklenenOzet))
        {
            Sil(kurucu);
            return Dustu(settingsPath, KopruSonucu.OzetTutmadi, "kopru-ozet-tutmadi");
        }

        int cikis;
        try { cikis = ortam.CalistirBekle(kurucu, KopruSabitleri.WindowsKurucuArgumanlari, KopruSabitleri.KurulumSuresi); }
        catch (Exception exception) { return Dustu(settingsPath, KopruSonucu.KurulumDustu, "kopru-kurulum " + exception.GetType().Name + ": " + exception.Message); }
        if (cikis != 0) return Dustu(settingsPath, KopruSonucu.KurulumDustu, "kopru-kurulum-cikis=" + cikis);

        var yeni = WindowsYeniBaslatici(ortam, localAppData);
        if (!ortam.DosyaVar(yeni)) return Dustu(settingsPath, KopruSonucu.YeniUygulamaYok, "kopru-yeni-uygulama-yok");
        return YeniyiBaslat(ortam, yeni, Array.Empty<string>(), settingsPath, "kopru-baslatma-dustu", kurucu);
    }

    private static KopruSonucu YeniyiBaslat(KopruOrtami ortam, string dosya, IReadOnlyList<string> argumanlar, string? settingsPath, string sebep, string? silinecek)
    {
        bool basladi;
        try { basladi = ortam.Baslat(dosya, argumanlar); }
        catch (Exception) { basladi = false; }
        if (!basladi) return Dustu(settingsPath, KopruSonucu.BaslatmaDustu, sebep);
        if (silinecek is not null) Sil(silinecek);
        UpdateHealth.RecordSuccess(settingsPath, DateTimeOffset.UtcNow);
        return KopruSonucu.Tamam;
    }

    public static (string Url, string Ozet, string Ad) MacArsivi(Architecture mimari) =>
        mimari == Architecture.Arm64
            ? (KopruSabitleri.MacArmArsivUrl, KopruSabitleri.MacArmArsivSha256, KopruSabitleri.MacArmArsivAdi)
            : (KopruSabitleri.MacX64ArsivUrl, KopruSabitleri.MacX64ArsivSha256, KopruSabitleri.MacX64ArsivAdi);

    public static string MacHedefPaket(string eskiPaket) =>
        Path.Combine(Path.GetDirectoryName(eskiPaket) ?? "", KopruSabitleri.YeniMacPaketi);

    public static async Task<KopruSonucu> MacAsync(
        KopruOrtami ortam,
        string eskiPaket,
        string url,
        string beklenenOzet,
        string arsivAdi,
        string? settingsPath,
        CancellationToken iptal)
    {
        if (string.IsNullOrWhiteSpace(beklenenOzet))
            return Dustu(settingsPath, KopruSonucu.OzetYok, "mac-kopru-ozet-yok");

        var ust = Path.GetDirectoryName(eskiPaket) ?? "";
        var hazirlik = Path.Combine(ust, KopruSabitleri.MacHazirlikKlasoru);
        var arsiv = Path.Combine(hazirlik, arsivAdi);
        var yuk = Path.Combine(hazirlik, "payload");
        var hazirPaket = Path.Combine(hazirlik, KopruSabitleri.YeniMacPaketi);
        var hedef = MacHedefPaket(eskiPaket);
        if (ortam.KlasorVar(hedef))
            return YeniyiBaslat(ortam, "/usr/bin/open", new[] { "-n", hedef }, settingsPath, "mac-kopru-baslatma-dustu", null);

        try
        {
            ortam.KlasoruSil(hazirlik);
            await ortam.Indir(url, arsiv, iptal);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Temizle(ortam, hazirlik);
            return Dustu(settingsPath, KopruSonucu.IndirmeDustu, "mac-kopru-indirme " + exception.GetType().Name + ": " + exception.Message);
        }

        if (!OzetTutar(ortam, arsiv, beklenenOzet))
        {
            Temizle(ortam, hazirlik);
            return Dustu(settingsPath, KopruSonucu.OzetTutmadi, "mac-kopru-ozet-tutmadi");
        }

        try
        {
            ortam.ArsiviAc(arsiv, yuk);
            var betik = Path.Combine(yuk, MacUpdate.BundleScriptName);
            var ikili = ortam.DosyaVar(Path.Combine(yuk, KopruSabitleri.YeniMacAnaIkili))
                ? KopruSabitleri.YeniMacAnaIkili
                : KopruSabitleri.YeniMacYedekIkili;
            var cikis = ortam.CalistirBekle("/bin/sh", new[] { betik, yuk, ikili, KopruSabitleri.Surum, hazirPaket }, KopruSabitleri.KurulumSuresi);
            if (cikis != 0)
            {
                Temizle(ortam, hazirlik);
                return Dustu(settingsPath, KopruSonucu.KurulumDustu, "mac-kopru-paketleme-cikis=" + cikis);
            }
        }
        catch (Exception exception)
        {
            Temizle(ortam, hazirlik);
            return Dustu(settingsPath, KopruSonucu.KurulumDustu, "mac-kopru-paketleme " + exception.GetType().Name + ": " + exception.Message);
        }

        if (ortam.CalistirBekle("/usr/bin/codesign", new[] { "--verify", "--strict", hazirPaket }, KopruSabitleri.KurulumSuresi) != 0)
        {
            Temizle(ortam, hazirlik);
            return Dustu(settingsPath, KopruSonucu.ImzaGecersiz, "mac-kopru-imza-gecersiz");
        }

        try
        {
            ortam.KlasoruTasi(hazirPaket, hedef);
        }
        catch (Exception exception)
        {
            Temizle(ortam, hazirlik);
            return Dustu(settingsPath, KopruSonucu.KurulumDustu, "mac-kopru-yerlestirme " + exception.GetType().Name + ": " + exception.Message);
        }
        Temizle(ortam, hazirlik);

        if (!ortam.KlasorVar(hedef)) return Dustu(settingsPath, KopruSonucu.YeniUygulamaYok, "mac-kopru-yeni-uygulama-yok");
        return YeniyiBaslat(ortam, "/usr/bin/open", new[] { "-n", hedef }, settingsPath, "mac-kopru-baslatma-dustu", null);
    }

    private static bool OzetTutar(KopruOrtami ortam, string dosya, string beklenen)
    {
        try
        {
            return string.Equals(ortam.Ozet(dosya), beklenen, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static KopruSonucu Dustu(string? settingsPath, KopruSonucu sonuc, string sebep)
    {
        UpdateHealth.RecordFailure(settingsPath, DateTimeOffset.UtcNow, sebep);
        return sonuc;
    }

    private static void Sil(string dosya)
    {
        try { if (File.Exists(dosya)) File.Delete(dosya); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
    }

    private static void Temizle(KopruOrtami ortam, string klasor)
    {
        try { ortam.KlasoruSil(klasor); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
    }
}
