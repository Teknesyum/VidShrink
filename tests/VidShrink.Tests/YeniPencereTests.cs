using System.Diagnostics;
using VidShrink.App.Playback;
using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Later 137 "yeni pencerede aç": ikinci süreç ayar klasörünün kopyasına yazar, kuyruğu almaz,
/// kopya kapanışta ve ölmüş sürecinki sonraki açılışta silinir. Klasörler <c>.calisma/test-ciktilari</c> altında.
/// </summary>
public sealed class YeniPencereTests : IDisposable
{
    private readonly string _kok = Path.Combine(TestPaths.OutputRoot, "yeni-pencere", Guid.NewGuid().ToString("N"));
    private readonly string? _eskiAyar = Environment.GetEnvironmentVariable(NewWindow.SettingsVariable);

    public YeniPencereTests()
    {
        Directory.CreateDirectory(_kok);
        NewWindow.ResetForTests();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(NewWindow.SettingsVariable, _eskiAyar);
        NewWindow.ResetForTests();
        try { Directory.Delete(_kok, recursive: true); }
        catch (IOException) { }
    }

    private string Ayar => Path.Combine(_kok, "settings.json");

    [Theory]
    [InlineData(new[] { "--yeni-pencere" }, true)]
    [InlineData(new[] { "a.mp4", "--new-window" }, true)]
    [InlineData(new[] { "--Yeni-Pencere" }, false)]
    [InlineData(new[] { "a.mp4" }, false)]
    [InlineData(new string[0], false)]
    public void Bayrak_iki_dilde_ve_buyuk_kucuk_harfe_duyarli(string[] args, bool beklenen)
        => Assert.Equal(beklenen, NewWindow.Requested(args));

    [Fact]
    public void Bayrak_argumanlardan_cikarilir_hedef_arkasina_eklenir()
    {
        Assert.False(NewWindow.Requested(null));
        Assert.Equal(new[] { "a.mp4", "b.mp4" }, NewWindow.Without(new[] { "--yeni-pencere", "a.mp4", "--new-window", "b.mp4" }));
        Assert.Equal(new[] { NewWindow.Flag }, NewWindow.Arguments(null));
        Assert.Equal(new[] { NewWindow.Flag }, NewWindow.Arguments("  "));
        Assert.Equal(new[] { NewWindow.Flag, "https://ornek.test/v.mp4" }, NewWindow.Arguments("https://ornek.test/v.mp4"));
    }

    [Fact]
    public void Kopya_ayarlari_alir_kuyrugu_ve_alt_klasoru_almaz()
    {
        File.WriteAllText(Ayar, "{\"a\":1}");
        File.WriteAllText(Path.Combine(_kok, "history.json"), "[]");
        File.WriteAllText(Path.Combine(_kok, PlayerQueue.FileName), "[\"kuyruk\"]");
        File.WriteAllText(Path.Combine(_kok, "notlar.txt"), "x");
        Directory.CreateDirectory(Path.Combine(_kok, "alt"));
        File.WriteAllText(Path.Combine(_kok, "alt", "derin.json"), "{}");

        var kopya = NewWindow.Prepare(Ayar, 4242, new[] { PlayerQueue.FileName.ToUpperInvariant() });

        var klasor = NewWindow.SnapshotFolder(Ayar, 4242);
        Assert.Equal(Path.Combine(_kok, NewWindow.FolderName, "4242"), klasor);
        Assert.Equal(Path.Combine(klasor, "settings.json"), kopya);
        Assert.Equal("{\"a\":1}", File.ReadAllText(kopya));
        Assert.True(File.Exists(Path.Combine(klasor, "history.json")));
        Assert.False(File.Exists(Path.Combine(klasor, PlayerQueue.FileName)));
        Assert.False(File.Exists(Path.Combine(klasor, "notlar.txt")));
        Assert.False(Directory.Exists(Path.Combine(klasor, "alt")));

        File.WriteAllText(kopya, "{\"a\":2}");
        Assert.Equal("{\"a\":1}", File.ReadAllText(Ayar));
    }

    [Fact]
    public void Ayar_klasoru_yoksa_bos_kopya_kurulur()
    {
        var yok = Path.Combine(_kok, "yok", "settings.json");
        var kopya = NewWindow.Prepare(yok, 7, null);
        Assert.True(Directory.Exists(Path.GetDirectoryName(kopya)));
        Assert.False(File.Exists(kopya));
    }

    [Fact]
    public void Supurge_yalniz_olmus_surecin_sayisal_klasorunu_siler()
    {
        var kok = Path.Combine(_kok, NewWindow.FolderName);
        foreach (var ad in new[] { "11", "22", "33", "baska" }) Directory.CreateDirectory(Path.Combine(kok, ad));

        var silinen = NewWindow.Sweep(Ayar, self: 33, alive: pid => pid == 22);

        Assert.Equal(1, silinen);
        Assert.False(Directory.Exists(Path.Combine(kok, "11")));
        Assert.True(Directory.Exists(Path.Combine(kok, "22")));
        Assert.True(Directory.Exists(Path.Combine(kok, "33")));
        Assert.True(Directory.Exists(Path.Combine(kok, "baska")));
        Assert.Equal(0, NewWindow.Sweep(Path.Combine(_kok, "bos", "settings.json"), 1, _ => false));
    }

    [Fact]
    public void Kaldirma_yalniz_pencereler_altindaki_kopyayi_siler()
    {
        File.WriteAllText(Ayar, "{}");
        NewWindow.Remove(Ayar);
        Assert.True(File.Exists(Ayar));

        var kopya = NewWindow.Prepare(Ayar, 99, null);
        NewWindow.Remove(kopya);
        Assert.False(Directory.Exists(Path.GetDirectoryName(kopya)));
        Assert.True(File.Exists(Ayar));
    }

    [Fact]
    public void Giris_ayar_yolunu_kopyaya_cevirir_cocuk_surece_gercegini_verir()
    {
        File.WriteAllText(Ayar, "{}");
        Directory.CreateDirectory(Path.Combine(_kok, NewWindow.FolderName, "5"));
        Environment.SetEnvironmentVariable(NewWindow.SettingsVariable, Ayar);

        var kopya = NewWindow.Enter(Ayar, 6, null, _ => false);

        Assert.True(NewWindow.Active);
        Assert.Equal(Ayar, NewWindow.OriginalSettings);
        Assert.Equal(kopya, Environment.GetEnvironmentVariable(NewWindow.SettingsVariable));
        Assert.Equal(Path.Combine(NewWindow.SnapshotFolder(Ayar, 6), "settings.json"), kopya);
        Assert.False(Directory.Exists(Path.Combine(_kok, NewWindow.FolderName, "5")));

        var baslat = new ProcessStartInfo("x");
        baslat.Environment[NewWindow.SettingsVariable] = kopya;
        NewWindow.RestoreFor(baslat);
        Assert.Equal(Ayar, baslat.Environment[NewWindow.SettingsVariable]);
    }

    [Fact]
    public void Degisken_yokken_cocuk_surec_degiskensiz_baslar()
    {
        Environment.SetEnvironmentVariable(NewWindow.SettingsVariable, null);
        NewWindow.Enter(Ayar, 8, null, _ => false);
        Assert.Null(NewWindow.OriginalSettings);

        var baslat = new ProcessStartInfo("x");
        baslat.Environment[NewWindow.SettingsVariable] = "kopya";
        NewWindow.RestoreFor(baslat);
        Assert.False(baslat.Environment.ContainsKey(NewWindow.SettingsVariable));
    }

    [Fact]
    public void Ikinci_pencere_degilken_cocuk_surece_dokunulmaz()
    {
        var baslat = new ProcessStartInfo("x");
        baslat.Environment[NewWindow.SettingsVariable] = "olduğu gibi";
        NewWindow.RestoreFor(baslat);
        Assert.Equal("olduğu gibi", baslat.Environment[NewWindow.SettingsVariable]);
        Assert.False(NewWindow.Active);
    }
}
