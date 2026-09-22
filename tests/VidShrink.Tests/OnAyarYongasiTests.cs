using Avalonia;
using Avalonia.Controls;
using VidShrink.App;
using VidShrink.Core;

namespace VidShrink.Tests;

/// <summary>
/// K107: kullanıcının kendi ön ayarı yonga şeridinden kaydediliyor, uygulanıyor ve
/// siliniyor. Ölçüm başsız pencerede yapılır; ön ayar dosyası her ölçümde kendi geçici
/// klasöründedir, gerçek <c>%APPDATA%\VidShrink\presets.json</c> okunmaz da yazılmaz da.
/// </summary>
public sealed class OnAyarYongasiTests : IDisposable
{
    private static readonly Size WindowSize = new(1560, 1060);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"vidshrink-onayar-{Guid.NewGuid():N}");

    private string PresetPath => Path.Combine(_folder, "presets.json");

    public void Dispose()
    {
        try { Directory.Delete(_folder, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private T Read<T>(Func<MainWindow, T> read) =>
        AppHost.Run(() =>
        {
            var window = new MainWindow
            {
                SettingsPathOverride = Path.Combine(_folder, "settings.json"),
                PresetPathOverride = PresetPath
            };
            window.Width = double.NaN;
            window.Height = double.NaN;
            window.Measure(WindowSize);
            window.Arrange(new Rect(WindowSize));
            window.UpdateLayout();
            window.InitUserPresets();
            return read(window);
        });

    /// <summary>Kaydedilen profil o anki arayüzün alanlarını taşıyor ve kullanıcı türünde.</summary>
    [Fact]
    public void KaydedilenProfilAlanlariTasiyor()
    {
        var (kaydedildi, beklenen) = Read(window =>
        {
            var beklenen = window.CurrentPreset("Deneme");
            return (window.SavePreset("Deneme"), beklenen);
        });

        Assert.True(kaydedildi);

        var dosyadan = Assert.Single(PresetLibrary.LoadUser(PresetPath));
        Assert.Equal("Deneme", dosyadan.Id);
        Assert.Equal("Deneme", dosyadan.Name);
        Assert.Equal(PresetKind.User, dosyadan.Kind);
        Assert.Equal(beklenen.Intent, dosyadan.Intent);
        Assert.Equal(beklenen.Codec, dosyadan.Codec);
        Assert.Equal(beklenen.Fill, dosyadan.Fill);
        Assert.Equal(beklenen.SizeCapped, dosyadan.SizeCapped);
        Assert.Equal(beklenen.TargetMb, dosyadan.TargetMb);
        Assert.Equal(beklenen.MaxShortEdge, dosyadan.MaxShortEdge);
    }

    /// <summary>Boş ad kaydedilmiyor; dosya hiç oluşmuyor ve uyarı görünüyor.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BosAdKaydedilmiyor(string ad)
    {
        var (kaydedildi, uyari) = Read(window => (window.SavePreset(ad), window.PresetNoticeVisible));

        Assert.False(kaydedildi);
        Assert.True(uyari);
        Assert.False(File.Exists(PresetPath));
    }

    /// <summary>Aynı ad sessizce ezilmiyor: bir kez soruluyor, ikinci basış üstüne yazıyor.</summary>
    [Fact]
    public void AyniAdOnceSoruyorSonraYaziyor()
    {
        var (ilk, ikinci, ucuncu, sayi) = Read(window =>
        {
            var ilk = window.SavePreset("Aynı");
            var ikinci = window.SavePreset("Aynı");
            var ucuncu = window.SavePreset("Aynı");
            return (ilk, ikinci, ucuncu, window.UserPresets.Count);
        });

        Assert.True(ilk);
        Assert.False(ikinci);
        Assert.True(ucuncu);
        Assert.Equal(1, sayi);
        Assert.Single(PresetLibrary.LoadUser(PresetPath));
    }

    /// <summary>Uygulama yolu: yonganın uyguladığı alanlar profilin alanlarına eşit.</summary>
    [Fact]
    public void UygulamaProfilinAlanlariniGeriGetiriyor()
    {
        var (uygulanan, profil) = Read(window =>
        {
            var profil = new PresetProfile
            {
                Id = "Geri",
                Name = "Geri",
                Kind = PresetKind.User,
                TargetMb = 42,
                SizeCapped = true,
                Intent = Intent.Archive,
                Codec = CodecPreference.MaxCompression,
                Fill = FillPolicy.QualityCeiling,
                MaxShortEdge = 720,
                AudioKbps = 160
            };
            window.ApplyUserPreset(profil);
            return (window.CurrentPreset("Geri"), profil);
        });

        Assert.Equal(profil.Intent, uygulanan.Intent);
        Assert.Equal(profil.Codec, uygulanan.Codec);
        Assert.Equal(profil.Fill, uygulanan.Fill);
        Assert.Equal(profil.SizeCapped, uygulanan.SizeCapped);
        Assert.Equal(profil.TargetMb, uygulanan.TargetMb);
        Assert.Equal(profil.MaxShortEdge, uygulanan.MaxShortEdge);
        Assert.Equal(profil.AudioKbps, uygulanan.AudioKbps);
    }

    /// <summary>
    /// Ses bit hızı: CLI <c>--profil</c> onu kilitliyordu, pencere düşürüyordu. Merdivende
    /// olmayan değer en yakın basamağa, eşitlikte yukarıya iner; ses taşımayan profil
    /// kullanıcının seçtiği kutuya dokunmaz (olumsuz kontrol).
    /// </summary>
    [Theory]
    [InlineData(224, "256")]
    [InlineData(100, "96")]
    [InlineData(1000, "320")]
    public void SesBitHiziMerdivenineIner(int kbps, string beklenen)
    {
        var (secilen, dokunulmadi) = Read(window =>
        {
            window.ApplyUserPreset(new PresetProfile { Id = "Ses", Name = "Ses", Kind = PresetKind.User, AudioKbps = kbps });
            var secilen = window.CmbAdvAudioKbps.SelectedItem as string;
            window.CmbAdvAudioKbps.SelectedIndex = 2;
            window.ApplyUserPreset(new PresetProfile { Id = "Sessiz", Name = "Sessiz", Kind = PresetKind.User });
            return (secilen, window.CmbAdvAudioKbps.SelectedIndex);
        });

        Assert.Equal(beklenen, secilen);
        Assert.Equal(2, dokunulmadi);
    }

    /// <summary>
    /// Kap çıktı uzantısını seçiyor ve kayıtta geri geliyor; gömülü yonga onu bırakıyor,
    /// yoksa sonraki küçültme eski ön ayarın kabıyla çıkardı. WebM mp4'e düşüyor.
    /// </summary>
    [Fact]
    public void KapUzantiyiSeciyorGomuluYongaBirakiyor()
    {
        var plan = new EncodePlan { Codec = "libx264", Mode = "2pass" };
        var (mov, kayit, yongadan, webm) = Read(window =>
        {
            window.ApplyUserPreset(new PresetProfile { Id = "Kap", Name = "Kap", Kind = PresetKind.User, Container = OutputContainer.Mov });
            var mov = window.ShrinkExtension(plan);
            var kayit = window.CurrentPreset("Kap").Container;
            window.ApplyChipPlan(MainWindow.ChipPlans().First().Chip);
            var yongadan = window.ShrinkExtension(plan);
            window.ApplyUserPreset(new PresetProfile { Id = "Web", Name = "Web", Kind = PresetKind.User, Container = OutputContainer.WebM });
            return (mov, kayit, yongadan, window.ShrinkExtension(plan));
        });

        Assert.Equal("mov", mov);
        Assert.Equal(OutputContainer.Mov, kayit);
        Assert.Equal("mp4", yongadan);
        Assert.Equal("mp4", webm);
    }

    /// <summary>Silme onay istemiyor; geri alma silinen profili dosyaya geri yazıyor.</summary>
    [Fact]
    public void SilmeVeGeriAlma()
    {
        var (silindiktenSonra, dosyaSilindiktenSonra, geriAlindi, geriSonra) = Read(window =>
        {
            window.SavePreset("Silinecek");
            var silinen = window.UserPresets[0];
            window.DeletePreset(silinen);
            var bosMu = window.UserPresets.Count;
            var dosyada = PresetLibrary.LoadUser(PresetPath).Count;
            var geri = window.UndoPresetDelete();
            return (bosMu, dosyada, geri, window.UserPresets.Count);
        });

        Assert.Equal(0, silindiktenSonra);
        Assert.Equal(0, dosyaSilindiktenSonra);
        Assert.True(geriAlindi);
        Assert.Equal(1, geriSonra);
        Assert.Equal("Silinecek", Assert.Single(PresetLibrary.LoadUser(PresetPath)).Id);
    }

    /// <summary>Şeridin sırası: gömülü yongalar → ayırıcı → kullanıcı yongası → "+".</summary>
    [Fact]
    public void SeritSirasiGomuluAyiriciKullaniciArti()
    {
        var sira = Read(window =>
        {
            window.SavePreset("Sıra");
            var cocuklar = window.ChipStrip.Children;
            var ayirici = cocuklar.IndexOf(window.ChipUserSeparator);
            var arti = cocuklar.IndexOf(window.ChipAddPreset);
            var kullanici = cocuklar
                .OfType<Button>()
                .Select(cocuklar.IndexOf)
                .Where(i => (cocuklar[i] as Button)?.Tag is PresetProfile)
                .ToList();
            var gomulu = cocuklar.OfType<Button>().Where(b => b.Name == "Chip180").Select(cocuklar.IndexOf).Single();
            return (gomulu, ayirici, kullanici, arti, window.ChipUserSeparator.IsVisible);
        });

        var tek = Assert.Single(sira.kullanici);
        Assert.True(sira.gomulu < sira.ayirici, $"gömülü {sira.gomulu} < ayırıcı {sira.ayirici}");
        Assert.True(sira.ayirici < tek, $"ayırıcı {sira.ayirici} < kullanıcı {tek}");
        Assert.True(tek < sira.arti, $"kullanıcı {tek} < artı {sira.arti}");
        Assert.True(sira.Item5);
    }

    /// <summary>On yeni anahtar 42 dilde var ve boş değil; yer tutucusu olan dördü onu koruyor.</summary>
    [Theory]
    [InlineData("main.preset.add.name", false)]
    [InlineData("main.preset.add.tip", false)]
    [InlineData("main.preset.name.label", false)]
    [InlineData("main.preset.save", false)]
    [InlineData("main.preset.name-empty", false)]
    [InlineData("main.preset.overwrite", true)]
    [InlineData("main.preset.chip.tip", false)]
    [InlineData("main.preset.delete.name", true)]
    [InlineData("main.preset.deleted", true)]
    [InlineData("main.preset.undo", false)]
    public void OnAyarAnahtarlariButunDillerde(string key, bool yerTutucu)
    {
        Assert.Equal(42, Locales.Languages.Count);

        foreach (var language in Locales.Languages)
        {
            var values = Locales.Values(language);
            Assert.True(values.ContainsKey(key), $"{language} dilinde {key} yok.");
            Assert.False(string.IsNullOrWhiteSpace(values[key]), $"{language} dilinde {key} boş.");
            if (yerTutucu) Assert.Contains("{0}", values[key], StringComparison.Ordinal);
        }
    }

    /// <summary>Kullanıcı yongası yokken ayırıcı da görünmüyor.</summary>
    [Fact]
    public void KullaniciYongasiYokkenAyiriciGizli()
    {
        var gorunur = Read(window => window.ChipUserSeparator.IsVisible);
        Assert.False(gorunur);
    }
}
