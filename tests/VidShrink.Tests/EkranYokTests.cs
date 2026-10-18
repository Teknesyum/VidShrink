using System.Diagnostics;
using Avalonia;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.Core;
using Xunit;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// Linux'ta X ekranı açılamayınca uygulama yığın dökümüyle düşmez (<c>Program.PencereyiAc</c>,
/// <c>Program.EkranKapisi</c>). Önceden Avalonia'nın <c>XOpenDisplay failed</c> istisnası
/// yakalanmıyor, süreç 134 koduyla (SIGABRT) düşüyordu. Birim kolları sahte pencere alt sistemiyle,
/// süreç kolları derlenmiş uygulamayla ve var olmayan bir ekranla (<c>DISPLAY=:57</c>) koşar.
/// </summary>
public sealed class EkranYokTests
{
    private const string XHatasi = "XOpenDisplay failed";

    private readonly ITestOutputHelper _cikti;

    public EkranYokTests(ITestOutputHelper cikti) => _cikti = cikti;

    private static AppBuilder SahteAltSistem(Action kur)
        => AppBuilder.Configure<Avalonia.Application>().UseWindowingSubsystem(kur, "X11");

    [Fact]
    public void LinuxtaPencereAltSistemininHatasiEkranHatasinaDoner()
    {
        var kapili = Program.EkranKapisi(SahteAltSistem(() => throw new Exception(XHatasi)), linux: true);

        var hata = Assert.Throws<Program.EkranAcilamadiException>(() => kapili.WindowingSubsystemInitializer!());
        Assert.Equal(XHatasi, hata.Message);
        Assert.Equal(XHatasi, hata.InnerException!.Message);
        Assert.Equal("X11", kapili.WindowingSubsystemName);
    }

    /// <summary>Olumsuz kontrol: Windows/macOS kolunda kurucu sarılmaz, istisna olduğu gibi geçer.</summary>
    [Fact]
    public void LinuxDisindaKurucuyaDokunulmaz()
    {
        Action kur = () => throw new Exception(XHatasi);
        var builder = SahteAltSistem(kur);

        var kapisiz = Program.EkranKapisi(builder, linux: false);

        Assert.Same(kur, kapisiz.WindowingSubsystemInitializer);
        var hata = Assert.Throws<Exception>(() => kapisiz.WindowingSubsystemInitializer!());
        Assert.IsNotType<Program.EkranAcilamadiException>(hata);
    }

    /// <summary>Olumlu kontrol: kurulabilen alt sistem kapıdan geçince de bir kez kurulur.</summary>
    [Fact]
    public void KurulabilenAltSistemKapidanGecer()
    {
        var kuruldu = 0;
        var kapili = Program.EkranKapisi(SahteAltSistem(() => kuruldu++), linux: true);

        kapili.WindowingSubsystemInitializer!();

        Assert.Equal(1, kuruldu);
    }

    [Fact]
    public void EkranHatasiKisaIletiVeHataKoduVerir()
    {
        var yazi = new StringWriter();

        var kod = Program.PencereyiAc(
            () => throw new Program.EkranAcilamadiException(new Exception(XHatasi)),
            yazi,
            () => "ekran yok iletisi");

        Assert.Equal(Program.EkranYokCikisKodu, kod);
        Assert.Equal(VidShrink.Cli.ExitCodes.Error, kod);
        var satirlar = yazi.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Equal(2, satirlar.Length);
        Assert.Equal("ekran yok iletisi", satirlar[0]);
        Assert.Contains(XHatasi, satirlar[1], StringComparison.Ordinal);
        Assert.Contains("DISPLAY=", satirlar[1], StringComparison.Ordinal);
    }

    /// <summary>Olumsuz kontrol: uygulamanın kendi istisnası ekran hatası sayılmaz, yutulmaz.</summary>
    [Fact]
    public void BaskaIstisnaYutulmaz()
    {
        var yazi = new StringWriter();

        Assert.Throws<InvalidOperationException>(() =>
            Program.PencereyiAc(() => throw new InvalidOperationException(XHatasi), yazi, () => "ileti"));
        Assert.Throws<Exception>(() =>
            Program.PencereyiAc(() => throw new Exception(XHatasi), yazi, () => "ileti"));
        Assert.Equal("", yazi.ToString());
    }

    [Fact]
    public void PencereninKoduOlduguGibiDoner()
    {
        var yazi = new StringWriter();

        Assert.Equal(7, Program.PencereyiAc(() => 7, yazi, () => "ileti"));
        Assert.Equal("", yazi.ToString());
    }

    [Fact]
    public void IletiKirkIkiDildeVar()
    {
        var diller = Directory.GetDirectories(Path.Combine(TipSources.Root, "src", "VidShrink.App", "Locales"))
            .Select(Path.GetFileName)
            .ToList();
        Assert.Equal(42, diller.Count);

        foreach (var dil in diller)
        {
            var metin = Strings.PeekIn(dil!, Program.EkranYokAnahtari);
            Assert.NotEqual(Program.EkranYokAnahtari, metin);
            Assert.Contains("VidShrink", metin, StringComparison.Ordinal);
            Assert.Contains("Xwayland", metin, StringComparison.Ordinal);
            if (dil != "en") Assert.NotEqual(Strings.PeekIn("en", Program.EkranYokAnahtari), metin);
        }
    }

    private static string UygulamaDll => Path.Combine(AppContext.BaseDirectory, AppIdentity.AppProcessName + ".dll");

    private sealed record Kosum(int Kod, string Cikti, string Hata, long Ms);

    private Kosum Kos(string? display, string dil, string kanal, params string[] argumanlar)
    {
        var calisma = Path.Combine(TestPaths.OutputRoot, "ekran-yok", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(calisma);
        try
        {
            var bilgi = new ProcessStartInfo("dotnet")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = calisma
            };
            bilgi.ArgumentList.Add(UygulamaDll);
            foreach (var arguman in argumanlar) bilgi.ArgumentList.Add(arguman);
            if (display is null) bilgi.Environment.Remove("DISPLAY");
            else bilgi.Environment["DISPLAY"] = display;
            bilgi.Environment.Remove("WAYLAND_DISPLAY");
            bilgi.Environment["LANG"] = dil;
            bilgi.Environment["LC_ALL"] = dil;
            bilgi.Environment[SingleInstanceChannel.ChannelVariable] = kanal;
            bilgi.Environment["VIDSHRINK_SETTINGS_PATH"] = Path.Combine(calisma, "settings.json");

            var saat = Stopwatch.StartNew();
            using var surec = Process.Start(bilgi)!;
            var cikti = surec.StandardOutput.ReadToEndAsync();
            var hata = surec.StandardError.ReadToEndAsync();
            if (!surec.WaitForExit(30_000))
            {
                surec.Kill(entireProcessTree: true);
                Assert.Fail("uygulama 30 sn icinde cikmadi");
            }
            surec.WaitForExit();
            var kosum = new Kosum(surec.ExitCode, cikti.Result, hata.Result, saat.ElapsedMilliseconds);
            _cikti.WriteLine($"DISPLAY={display ?? "(yok)"} LANG={dil} {string.Join(' ', argumanlar)} -> {kosum.Kod} ({kosum.Ms} ms)");
            _cikti.WriteLine(kosum.Hata);
            return kosum;
        }
        finally
        {
            try { Directory.Delete(calisma, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private static void TemizCikis(Kosum kosum, string ileti)
    {
        Assert.Equal(Program.EkranYokCikisKodu, kosum.Kod);
        Assert.Contains(ileti, kosum.Hata, StringComparison.Ordinal);
        Assert.Contains(XHatasi, kosum.Hata, StringComparison.Ordinal);
        Assert.DoesNotContain("Unhandled exception", kosum.Hata, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", kosum.Hata, StringComparison.Ordinal);
    }

    private static string Kanal() => "test-ekran-" + Guid.NewGuid().ToString("n");

    [LinuxFact]
    public void OlmayanEkrandaUygulamaTemizCikar()
    {
        TemizCikis(Kos(":57", "C.UTF-8", Kanal()), Strings.PeekIn("en", Program.EkranYokAnahtari));
    }

    [LinuxFact]
    public void DisplayYokkenIletiSistemDilinde()
    {
        TemizCikis(Kos(null, "tr_TR.UTF-8", Kanal()), Strings.PeekIn("tr", Program.EkranYokAnahtari));
    }

    [LinuxFact]
    public void KabukKucultmeYoluDaTemizCikar()
    {
        var yok = Path.Combine(TestPaths.OutputRoot, "ekran-yok", "olmayan.mp4");
        TemizCikis(Kos(":57", "C.UTF-8", Kanal(), ShellIntegration.ShrinkFlag, "10", yok),
            Strings.PeekIn("en", Program.EkranYokAnahtari));
    }

    /// <summary>
    /// İkinci süreç ekran istemeden sahibe teslim eder: tek örnek devri pencereden önce.
    /// Olumlu kontrol: aynı kanalda sahip yokken aynı süreç ekran hatasıyla çıkar.
    /// </summary>
    [LinuxFact]
    public void IkinciSurecEkranOlmadanSahibeTeslimEder()
    {
        var kanal = Kanal();
        var gelen = 0;
        using (var sahip = new SingleInstanceChannel(kanal))
        {
            Assert.True(sahip.IsOwner);
            sahip.StartListening(_ => { Interlocked.Increment(ref gelen); return true; });

            var kosum = Kos(":57", "C.UTF-8", kanal);

            Assert.Equal(0, kosum.Kod);
            Assert.Equal("", kosum.Hata);
            Assert.Equal(1, Volatile.Read(ref gelen));
        }

        TemizCikis(Kos(":57", "C.UTF-8", kanal), Strings.PeekIn("en", Program.EkranYokAnahtari));
    }
}

public sealed class LinuxFactAttribute : FactAttribute
{
    public LinuxFactAttribute()
    {
        if (!OperatingSystem.IsLinux()) Skip = "X ekranı yalnız Linux'ta açılır.";
    }
}
