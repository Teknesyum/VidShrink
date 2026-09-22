using Avalonia;
using VidShrink.App;
using VidShrink.Core;

namespace VidShrink.Tests;

/// <summary>
/// C1-7: ön ayar içe/dışa aktarma pencereye bağlı. VidShrink dosyası doğrudan, HandBrake
/// dosyası çeviriyle geliyor; var olan ön ayar ezilmiyor. Her ölçüm kendi geçici klasöründe,
/// gerçek <c>%APPDATA%\VidShrink\presets.json</c> okunmaz da yazılmaz da.
/// </summary>
public sealed class OnAyarAktarimTests : IDisposable
{
    private static readonly Size WindowSize = new(1560, 1060);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"vidshrink-aktarim-{Guid.NewGuid():N}");

    public OnAyarAktarimTests() => Directory.CreateDirectory(_folder);

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

    private static PresetProfile Profil(string id, double targetMb) => new()
    {
        Id = id,
        Name = id,
        Kind = PresetKind.User,
        SizeCapped = true,
        TargetMb = targetMb
    };

    private string HandBrakeKopyasi(string fileFormat)
    {
        var path = Path.Combine(_folder, $"hb-{fileFormat}.json");
        File.WriteAllText(path, File.ReadAllText(HandBrakeOnAyarCeviriTests.FixturePath)
            .Replace("\"av_mp4\"", $"\"{fileFormat}\"", StringComparison.Ordinal));
        return path;
    }

    /// <summary>VidShrink dosyası okunuyor, kaydediliyor ve yonga şeridine giriyor.</summary>
    [Fact]
    public void VidShrinkDosyasiIceAktariliyor()
    {
        var kaynak = Path.Combine(_folder, "gelen.json");
        PresetLibrary.Export(new[] { Profil("Gelen", 25) }, kaynak);

        var (sonuc, bellek, bildirim) = Read(w => (w.ImportPresets(kaynak), w.UserPresets.ToList(), w.TxtPresetNotice.Text));

        Assert.True(sonuc);
        Assert.Equal("Gelen", Assert.Single(bellek).Id);
        var dosyada = Assert.Single(PresetLibrary.LoadUser(PresetPath));
        Assert.Equal(25, dosyada.TargetMb);
        Assert.Contains("1", bildirim);
    }

    /// <summary>
    /// HandBrake dosyası çevriliyor: bildirim ön ayar adını ve çevirinin sayımlarını taşıyor.
    /// WebM kabı yaklaşık düştüğü için ayrı satırda adıyla yazılıyor; mp4'te o satır yok (olumsuz kontrol).
    /// </summary>
    [Theory]
    [InlineData("av_webm", true)]
    [InlineData("av_mp4", false)]
    public void HandBrakeDosyasiCevrilipOzetleniyor(string fileFormat, bool yaklasikKap)
    {
        var kaynak = HandBrakeKopyasi(fileFormat);
        var ceviri = Assert.Single(HandBrakePresetImport.TranslateFile(kaynak));
        var alanlar = ceviri.Notes.Where(n => n.Reason != PresetNoteReason.Structural).ToList();
        var yaklasik = alanlar.Count(n => n.Outcome == PresetNoteOutcome.Approximated);

        var (sonuc, bildirim) = Read(w => (w.ImportPresets(kaynak), w.TxtPresetNotice.Text ?? ""));

        Assert.True(sonuc);
        var satirlar = bildirim.Split('\n');
        Assert.Equal(2 + yaklasik, satirlar.Length);
        Assert.Contains(ceviri.PresetName, satirlar[1]);
        Assert.Contains(alanlar.Count(n => n.Outcome == PresetNoteOutcome.Carried).ToString(), satirlar[1]);
        Assert.Contains(alanlar.Count(n => n.Outcome == PresetNoteOutcome.Dropped).ToString(), satirlar[1]);
        Assert.Equal(yaklasikKap, satirlar.Skip(2).Any(s => s.StartsWith("FileFormat: ", StringComparison.Ordinal)));

        var dosyada = Assert.Single(PresetLibrary.LoadUser(PresetPath));
        Assert.Equal("hb-sentetik-sosyal-10-mb-720p", dosyada.Id);
        Assert.Equal(OutputContainer.Mp4, dosyada.Container);
    }

    /// <summary>
    /// Aynı kimlik gelince var olan ezilmiyor: gelen <c>-2</c> ekiyle yanına yazılıyor,
    /// eskisinin hedefi değişmiyor. Üçüncü kez gelen <c>-3</c> alıyor.
    /// </summary>
    [Fact]
    public void AyniKimlikEzilmiyorEkAliyor()
    {
        PresetLibrary.SaveUser(Profil("Ortak", 10), PresetPath);
        var kaynak = Path.Combine(_folder, "gelen.json");
        PresetLibrary.Export(new[] { Profil("Ortak", 50) }, kaynak);

        Read(w => w.ImportPresets(kaynak) && w.ImportPresets(kaynak));

        var dosyada = PresetLibrary.LoadUser(PresetPath).ToDictionary(p => p.Id);
        Assert.Equal(3, dosyada.Count);
        Assert.Equal(10, dosyada["Ortak"].TargetMb);
        Assert.Equal("Ortak", dosyada["Ortak"].Name);
        Assert.Equal(50, dosyada["Ortak-2"].TargetMb);
        Assert.Equal("Ortak (2)", dosyada["Ortak-2"].Name);
        Assert.Equal("Ortak (3)", dosyada["Ortak-3"].Name);
    }

    /// <summary>Hazır ön ayarın kimliğiyle gelen dosya reddedilmiyor, ek alıp kullanıcıya yazılıyor.</summary>
    [Fact]
    public void HazirKimlikleGelenEkAliyor()
    {
        var hazir = PresetLibrary.BuiltIn.Profiles[0].Id;
        var kaynak = Path.Combine(_folder, "gelen.json");
        PresetLibrary.Export(new[] { Profil(hazir, 40) }, kaynak);

        var sonuc = Read(w => w.ImportPresets(kaynak));

        Assert.True(sonuc);
        Assert.Equal($"{hazir}-2", Assert.Single(PresetLibrary.LoadUser(PresetPath)).Id);
    }

    /// <summary>Dışa aktarılan dosya başka bir kurulumda aynı profilleri geri veriyor.</summary>
    [Fact]
    public void DisaAktarilanGeriOkunuyor()
    {
        PresetLibrary.SaveUser(Profil("Bir", 8), PresetPath);
        PresetLibrary.SaveUser(Profil("Iki", 16), PresetPath);
        var hedef = Path.Combine(_folder, "disari.json");

        Assert.True(Read(w => w.ExportPresets(hedef)));

        var geri = PresetLibrary.Import(hedef).ToDictionary(p => p.Id);
        Assert.Equal(8, geri["Bir"].TargetMb);
        Assert.Equal(16, geri["Iki"].TargetMb);
    }

    /// <summary>Kendi ön ayarı yokken dosya yazılmıyor ve bildirim çıkıyor.</summary>
    [Fact]
    public void BosKitaplikDisaAktarilmiyor()
    {
        var hedef = Path.Combine(_folder, "disari.json");

        var (sonuc, gorunur) = Read(w => (w.ExportPresets(hedef), w.PresetNoticeVisible));

        Assert.False(sonuc);
        Assert.True(gorunur);
        Assert.False(File.Exists(hedef));
    }

    /// <summary>Bozuk dosya kitaplığa dokunmuyor, hata bildirimde görünüyor.</summary>
    [Fact]
    public void BozukDosyaKitapligaDokunmuyor()
    {
        PresetLibrary.SaveUser(Profil("Duran", 12), PresetPath);
        var kaynak = Path.Combine(_folder, "bozuk.json");
        File.WriteAllText(kaynak, "{ bu json değil");

        var (sonuc, gorunur) = Read(w => (w.ImportPresets(kaynak), w.PresetNoticeVisible));

        Assert.False(sonuc);
        Assert.True(gorunur);
        Assert.Equal("Duran", Assert.Single(PresetLibrary.LoadUser(PresetPath)).Id);
    }

    /// <summary>Yedi yeni anahtar 42 dilde var; sayı taşıyanlar yer tutucularını koruyor.</summary>
    [Theory]
    [InlineData("main.preset.import", 0)]
    [InlineData("main.preset.export", 0)]
    [InlineData("main.preset.file-type", 0)]
    [InlineData("main.preset.imported", 1)]
    [InlineData("main.preset.exported", 1)]
    [InlineData("main.preset.export-empty", 0)]
    [InlineData("main.preset.import.summary", 4)]
    public void AktarimAnahtarlariButunDillerde(string key, int yerTutucu)
    {
        Assert.Equal(42, Locales.Languages.Count);

        foreach (var language in Locales.Languages)
        {
            var values = Locales.Values(language);
            Assert.True(values.ContainsKey(key), $"{language} dilinde {key} yok.");
            Assert.False(string.IsNullOrWhiteSpace(values[key]), $"{language} dilinde {key} boş.");
            for (var i = 0; i < yerTutucu; i++)
                Assert.Contains($"{{{i}}}", values[key], StringComparison.Ordinal);
        }
    }
}
