using VidShrink.Core;

namespace VidShrink.Tests;

public sealed class KopruDevirTests
{
    private const string DogruOzet = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private static readonly string Klasor = Path.Combine(TipSources.Root, ".calisma", "test-ciktilari", "kopru-devir");

    private sealed class Sahte
    {
        public readonly List<string> Indirilen = new();
        public readonly List<(string Dosya, string[] Argumanlar)> Calistirilan = new();
        public readonly List<(string Dosya, string[] Argumanlar)> Baslatilan = new();
        public readonly List<(string Kaynak, string Hedef)> Tasinan = new();
        public readonly HashSet<string> Dosyalar = new(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> Klasorler = new(StringComparer.OrdinalIgnoreCase);
        public string Ozet = DogruOzet;
        public Func<string, string[], int> Cikis = (_, _) => 0;
        public Action<string, string[]>? KurulumYapar;

        public KopruOrtami Ortam() => new()
        {
            Indir = (url, hedef, _) => { Indirilen.Add(url); Dosyalar.Add(hedef); return Task.CompletedTask; },
            Ozet = _ => Ozet,
            CalistirBekle = (dosya, argumanlar, _) =>
            {
                var dizi = argumanlar.ToArray();
                Calistirilan.Add((dosya, dizi));
                var kod = Cikis(dosya, dizi);
                if (kod == 0) KurulumYapar?.Invoke(dosya, dizi);
                return kod;
            },
            Baslat = (dosya, argumanlar) => { Baslatilan.Add((dosya, argumanlar.ToArray())); return true; },
            DosyaVar = Dosyalar.Contains,
            KlasorVar = Klasorler.Contains,
            ArsiviAc = (_, hedef) => Klasorler.Add(hedef),
            KlasoruTasi = (kaynak, hedef) => { Tasinan.Add((kaynak, hedef)); Klasorler.Remove(kaynak); Klasorler.Add(hedef); },
            KlasoruSil = yol => Klasorler.Remove(yol),
            KayitliKurulumKoku = () => null
        };
    }

    private static string AyarYolu()
    {
        Directory.CreateDirectory(Klasor);
        return Path.Combine(Klasor, Guid.NewGuid().ToString("N") + ".json");
    }

    private static string Calisma => Path.Combine(Klasor, "calisma");

    private static string Yerel => Path.Combine(Klasor, "yerel");

    private static string YeniExe => Path.Combine(Yerel, "Programs", "VidShrink Player", "VidShrinkPlayer.exe");

    [Fact]
    public async Task BosOzetteDevirBaslamazVeSayilir()
    {
        var sahte = new Sahte();
        var ayar = AyarYolu();

        var sonuc = await KopruDevri.WindowsAsync(sahte.Ortam(), Calisma, Yerel, ayar, CancellationToken.None, beklenenOzet: "");

        Assert.Equal(KopruSonucu.OzetYok, sonuc);
        Assert.Empty(sahte.Indirilen);
        Assert.Empty(sahte.Calistirilan);
        Assert.Empty(sahte.Baslatilan);
        Assert.Equal(1, UpdateHealth.Load(ayar).Failures);
    }

    [Fact]
    public async Task UymayanOzetteKurucuCalismazVeSayilir()
    {
        var sahte = new Sahte { Ozet = new string('b', 64) };
        var ayar = AyarYolu();

        var sonuc = await KopruDevri.WindowsAsync(sahte.Ortam(), Calisma, Yerel, ayar, CancellationToken.None, beklenenOzet: DogruOzet);

        Assert.Equal(KopruSonucu.OzetTutmadi, sonuc);
        Assert.Equal(new[] { KopruSabitleri.WindowsKurucuUrl }, sahte.Indirilen);
        Assert.Empty(sahte.Calistirilan);
        Assert.Empty(sahte.Baslatilan);
        var saglik = UpdateHealth.Load(ayar);
        Assert.Equal(1, saglik.Failures);
        Assert.Equal("kopru-ozet-tutmadi", saglik.LastError);
    }

    [Fact]
    public async Task UyanOzetteSessizKurarVeYeniUygulamayiBaslatir()
    {
        var sahte = new Sahte();
        sahte.KurulumYapar = (_, _) => sahte.Dosyalar.Add(YeniExe);
        var ayar = AyarYolu();

        var sonuc = await KopruDevri.WindowsAsync(sahte.Ortam(), Calisma, Yerel, ayar, CancellationToken.None, beklenenOzet: DogruOzet.ToUpperInvariant());

        Assert.Equal(KopruSonucu.Tamam, sonuc);
        Assert.Equal("https://github.com/Teknesyum/VidShrink-Player/releases/download/v0.0.1/VidShrinkPlayer-Setup.exe", Assert.Single(sahte.Indirilen));
        var kurulum = Assert.Single(sahte.Calistirilan);
        Assert.Equal(Path.Combine(Calisma, "VidShrinkPlayer-Setup.exe"), kurulum.Dosya);
        Assert.Equal(new[] { "--silent", "--tag", "v0.0.1" }, kurulum.Argumanlar);
        var baslatilan = Assert.Single(sahte.Baslatilan);
        Assert.Equal(YeniExe, baslatilan.Dosya);
        Assert.Empty(baslatilan.Argumanlar);
        var saglik = UpdateHealth.Load(ayar);
        Assert.Equal(0, saglik.Failures);
        Assert.NotNull(saglik.LastSuccess);
    }

    [Fact]
    public async Task KurucuHataylaBiterseYeniUygulamaBaslamazVeSayilir()
    {
        var sahte = new Sahte { Cikis = (_, _) => 3 };
        var ayar = AyarYolu();

        var sonuc = await KopruDevri.WindowsAsync(sahte.Ortam(), Calisma, Yerel, ayar, CancellationToken.None, beklenenOzet: DogruOzet);

        Assert.Equal(KopruSonucu.KurulumDustu, sonuc);
        Assert.Empty(sahte.Baslatilan);
        Assert.Equal("kopru-kurulum-cikis=3", UpdateHealth.Load(ayar).LastError);
    }

    [Fact]
    public async Task KayitliKokVarsaOradakiYeniUygulamaBaslar()
    {
        var kok = Path.Combine(Klasor, "ozel-kok");
        var sahte = new Sahte();
        var ortam = sahte.Ortam();
        ortam = new KopruOrtami
        {
            Indir = ortam.Indir, Ozet = ortam.Ozet, CalistirBekle = ortam.CalistirBekle, Baslat = ortam.Baslat,
            DosyaVar = ortam.DosyaVar, KlasorVar = ortam.KlasorVar, ArsiviAc = ortam.ArsiviAc,
            KlasoruTasi = ortam.KlasoruTasi, KlasoruSil = ortam.KlasoruSil, KayitliKurulumKoku = () => kok
        };
        sahte.KurulumYapar = (_, _) => sahte.Dosyalar.Add(Path.Combine(kok, "VidShrinkPlayer.exe"));

        var sonuc = await KopruDevri.WindowsAsync(ortam, Calisma, Yerel, AyarYolu(), CancellationToken.None, beklenenOzet: DogruOzet);

        Assert.Equal(KopruSonucu.Tamam, sonuc);
        Assert.Equal(Path.Combine(kok, "VidShrinkPlayer.exe"), Assert.Single(sahte.Baslatilan).Dosya);
    }

    [Fact]
    public async Task YeniUygulamaZatenKuruluysaIndirmedenBaslatir()
    {
        var sahte = new Sahte();
        sahte.Dosyalar.Add(YeniExe);

        var sonuc = await KopruDevri.WindowsAsync(sahte.Ortam(), Calisma, Yerel, AyarYolu(), CancellationToken.None, beklenenOzet: DogruOzet);

        Assert.Equal(KopruSonucu.Tamam, sonuc);
        Assert.Empty(sahte.Indirilen);
        Assert.Empty(sahte.Calistirilan);
        Assert.Equal(YeniExe, Assert.Single(sahte.Baslatilan).Dosya);
    }

    [Fact]
    public void TasinabilirKipteVeAyarYoluVerildigindeDevirYapilmaz()
    {
        Assert.False(KopruDevri.Uygun(portable: true, null, null, null, autoUpdate: true));
        Assert.False(KopruDevri.Uygun(portable: false, @"C:\x\ayar.json", null, null, autoUpdate: true));
        Assert.False(KopruDevri.Uygun(portable: false, null, @"C:\x\ayar.json", null, autoUpdate: true));
        Assert.False(KopruDevri.Uygun(portable: false, null, null, "1", autoUpdate: true));
        Assert.False(KopruDevri.Uygun(portable: false, null, null, null, autoUpdate: false));
        Assert.True(KopruDevri.Uygun(portable: false, null, null, null, autoUpdate: true));
    }

    [Fact]
    public async Task MacUyanOzetteBetikleSararImzayiDogrularVeYeniPaketiAcar()
    {
        var sahte = new Sahte();
        var eski = "/Applications/VidShrink.app";
        var ust = Path.GetDirectoryName(eski)!;
        var hazirlik = Path.Combine(ust, ".vidshrink-kopru");
        var hazirPaket = Path.Combine(hazirlik, "VidShrink Player.app");
        var hedef = Path.Combine(ust, "VidShrink Player.app");
        sahte.KurulumYapar = (dosya, _) => { if (dosya == "/bin/sh") sahte.Klasorler.Add(hazirPaket); };
        var ayar = AyarYolu();

        var sonuc = await KopruDevri.MacAsync(sahte.Ortam(), eski, KopruSabitleri.MacArmArsivUrl, DogruOzet, KopruSabitleri.MacArmArsivAdi, ayar, CancellationToken.None);

        Assert.Equal(KopruSonucu.Tamam, sonuc);
        Assert.Equal(KopruSabitleri.MacArmArsivUrl, Assert.Single(sahte.Indirilen));
        Assert.Equal(2, sahte.Calistirilan.Count);
        Assert.Equal("/bin/sh", sahte.Calistirilan[0].Dosya);
        Assert.Equal(Path.Combine(hazirlik, "payload", "macos-app-bundle.sh"), sahte.Calistirilan[0].Argumanlar[0]);
        Assert.Equal("VidShrinkPlayer.App", sahte.Calistirilan[0].Argumanlar[2]);
        Assert.Equal(hazirPaket, sahte.Calistirilan[0].Argumanlar[4]);
        Assert.Equal("/usr/bin/codesign", sahte.Calistirilan[1].Dosya);
        Assert.Equal(new[] { "--verify", "--strict", hazirPaket }, sahte.Calistirilan[1].Argumanlar);
        Assert.Equal((hazirPaket, hedef), Assert.Single(sahte.Tasinan));
        var acilan = Assert.Single(sahte.Baslatilan);
        Assert.Equal("/usr/bin/open", acilan.Dosya);
        Assert.Equal(new[] { "-n", hedef }, acilan.Argumanlar);
        Assert.Equal(0, UpdateHealth.Load(ayar).Failures);
    }

    [Fact]
    public async Task MacImzaGecersizseYerlestirmezVeSayilir()
    {
        var sahte = new Sahte { Cikis = (dosya, _) => dosya == "/usr/bin/codesign" ? 1 : 0 };
        var ayar = AyarYolu();

        var sonuc = await KopruDevri.MacAsync(sahte.Ortam(), "/Applications/VidShrink.app", KopruSabitleri.MacArmArsivUrl, DogruOzet, KopruSabitleri.MacArmArsivAdi, ayar, CancellationToken.None);

        Assert.Equal(KopruSonucu.ImzaGecersiz, sonuc);
        Assert.Empty(sahte.Tasinan);
        Assert.Empty(sahte.Baslatilan);
        Assert.Equal(1, UpdateHealth.Load(ayar).Failures);
    }

    [Fact]
    public async Task MacBosVeUymayanOzetteIndirmeSonrasiBirSeyCalismaz()
    {
        var bos = new Sahte();
        var ayarBos = AyarYolu();
        Assert.Equal(KopruSonucu.OzetYok, await KopruDevri.MacAsync(bos.Ortam(), "/Applications/VidShrink.app", KopruSabitleri.MacArmArsivUrl, "", KopruSabitleri.MacArmArsivAdi, ayarBos, CancellationToken.None));
        Assert.Empty(bos.Indirilen);
        Assert.Equal(1, UpdateHealth.Load(ayarBos).Failures);

        var uymayan = new Sahte { Ozet = new string('c', 64) };
        var ayarUymayan = AyarYolu();
        Assert.Equal(KopruSonucu.OzetTutmadi, await KopruDevri.MacAsync(uymayan.Ortam(), "/Applications/VidShrink.app", KopruSabitleri.MacArmArsivUrl, DogruOzet, KopruSabitleri.MacArmArsivAdi, ayarUymayan, CancellationToken.None));
        Assert.Empty(uymayan.Calistirilan);
        Assert.Empty(uymayan.Baslatilan);
        Assert.Equal(1, UpdateHealth.Load(ayarUymayan).Failures);
    }
}
