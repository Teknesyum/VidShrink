using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using VidShrink.Cli;
using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// E9, ölçek modülü (HandBrake'in <c>--modulus</c>'u). İki şeyi birden ölçer: kuralın
/// kendisi ve kuralın tek yerde durduğu.
///
/// <para>Kural üç dosyada ayrı ayrı <c>private static int EvenDown</c> olarak yazılıydı
/// (<c>PlanCalculator</c>, <c>ComplexityProbe</c>, kaldırılan <c>FrameGrabber</c>) ve çarpan üçünde de
/// 2'ye gömülüydü. Kopyalar kaldırıldı; geri gelirlerse bu ölçü değil, bir sonraki ayrışma
/// yakalar — o yüzden kaynak taraması da burada.</para>
/// </summary>
public sealed class OlcekModuluTests
{
    [Theory]
    [InlineData(1920, 2, 1920)]
    [InlineData(1921, 2, 1920)]
    [InlineData(1921, 4, 1920)]
    [InlineData(1922, 4, 1920)]
    [InlineData(1080, 16, 1072)]
    [InlineData(1080, 8, 1080)]
    [InlineData(719, 16, 704)]
    public void AsagiYuvarliyor(int deger, int modul, int beklenen)
    {
        Assert.Equal(beklenen, Olcek.Modul(deger, modul));
    }

    /// <summary>Yukarı yuvarlamıyor: küçültme aracında kenarı büyütmek yanlış yön.</summary>
    [Theory]
    [InlineData(1921, 2)]
    [InlineData(1921, 4)]
    [InlineData(1921, 16)]
    public void YukariYuvarlamiyor(int deger, int modul)
    {
        Assert.True(Olcek.Modul(deger, modul) <= deger);
    }

    /// <summary>Sonuç sıfır olmaz: sıfır genişlikli kare ffmpeg'e verilemez.</summary>
    [Theory]
    [InlineData(1, 16)]
    [InlineData(8, 16)]
    [InlineData(16, 16)]
    [InlineData(1, 2)]
    public void SifirDonmuyor(int deger, int modul)
    {
        Assert.Equal(modul, Olcek.Modul(deger, modul));
    }

    /// <summary>Çarpan verilmezse bugünkü davranış: çift sayıya iner.</summary>
    [Theory]
    [InlineData(1921, 1920)]
    [InlineData(1920, 1920)]
    [InlineData(1079, 1078)]
    public void VarsayilanCiftSayi(int deger, int beklenen)
    {
        Assert.Equal(2, Olcek.VarsayilanModul);
        Assert.Equal(beklenen, Olcek.Modul(deger));
    }

    /// <summary>Küme kapalı; küme dışı sayı varsayılana düşer, sessizce kabul edilmez.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(32)]
    public void KumeDisiSayiGecerliDegil(int modul)
    {
        Assert.False(Olcek.GecerliModul(modul));
    }

    [Fact]
    public void KumeTamOlarakDortSayi()
    {
        Assert.Equal(new[] { 2, 4, 8, 16 }, Olcek.Moduller.ToArray());
        foreach (var modul in Olcek.Moduller) Assert.True(Olcek.GecerliModul(modul));
    }

    /// <summary>Küme dışı sayı <see cref="Olcek.Modul"/>'e verilirse varsayılan uygulanır.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-4)]
    public void KumeDisiCarpanVarsayilanaDusuyor(int modul)
    {
        Assert.Equal(Olcek.Modul(1921, Olcek.VarsayilanModul), Olcek.Modul(1921, modul));
    }

    /// <summary>
    /// Planın boyut kararı çarpanı gerçekten okuyor: aynı kaynak ve aynı hedef, yalnız
    /// çarpan değişince kenarlar çarpanın katına iniyor.
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    public void PlanKenarlariCarpanaOturuyor(int modul)
    {
        var info = new MediaInfo
        {
            FilePath = "olcum.mp4",
            Width = 1918,
            Height = 1078,
            Fps = 30,
            DurationSeconds = 60,
            VideoCodec = "h264",
            FileSizeBytes = 200L * 1024 * 1024,
            TotalBitrateBps = 28_000_000
        };

        var plan = PlanCalculator.Build(info, new PlanOptions { TargetMb = 8, ScaleModulus = modul });

        Assert.Equal(0, plan.Width % modul);
        Assert.Equal(0, plan.Height % modul);
    }

    /// <summary>
    /// Olumsuz kontrol: 16 ile 2 aynı kaynakta aynı kenarı vermiyor. Vermeseydi yukarıdaki
    /// ölçü çarpan hiç okunmasa da yeşil kalırdı — 1918/1078 ikisi de 2'nin katı.
    /// </summary>
    [Fact]
    public void BuyukCarpanKenariGercektenKirpiyor()
    {
        var info = new MediaInfo
        {
            FilePath = "olcum.mp4",
            Width = 1918,
            Height = 1078,
            Fps = 30,
            DurationSeconds = 60,
            VideoCodec = "h264",
            FileSizeBytes = 200L * 1024 * 1024,
            TotalBitrateBps = 28_000_000
        };

        var iki = PlanCalculator.Build(info, new PlanOptions { TargetMb = 8, ScaleModulus = 2 });
        var onalti = PlanCalculator.Build(info, new PlanOptions { TargetMb = 8, ScaleModulus = 16 });

        Assert.NotEqual((iki.Width, iki.Height), (onalti.Width, onalti.Height));
    }
    /// <summary>
    /// Kopyanın unuttuğu alan sessizce varsayılana döner. <c>ScaleModulus</c> eklendiğinde
    /// tam bu oldu: seçenek plana giriyordu ama merdiven <c>WithTarget</c> kopyasıyla
    /// kuruluyor ve çarpan orada düşüyordu. Ölçü tek alanı değil, kuralı tutar: yazılabilir
    /// her <c>PlanOptions</c> özelliği kopya başlatıcısında adıyla geçer.
    /// </summary>
    [Fact]
    public void KopyaHicbirSecenegiDusurmuyor()
    {
        var kaynak = File.ReadAllText(Path.Combine(
            TipSources.Root, "src", "VidShrink.Core", "PlanCalculator.cs"));
        var govde = Regex.Match(
            kaynak,
            @"WithTarget\(PlanOptions options, double targetMb\) => new\(\)\s*\{(.*?)\};",
            RegexOptions.Singleline);
        Assert.True(govde.Success, "WithTarget başlatıcısı bulunamadı.");

        var ozellikler = typeof(PlanOptions)
            .GetProperties()
            .Where(o => o.CanWrite)
            .Select(o => o.Name)
            .ToArray();
        Assert.True(ozellikler.Length >= 15, $"Yüzey daraldı: {ozellikler.Length} özellik.");

        foreach (var ad in ozellikler)
        {
            Assert.True(
                Regex.IsMatch(govde.Groups[1].Value, $@"\b{ad}\s*="),
                $"{ad} kopyada geçmiyor; seçenek merdivene ulaşmadan varsayılana düşer.");
        }
    }
    /// <summary>CLI bayrağı istekten plan seçeneğine iniyor; iki yazım aynı yere çıkıyor.</summary>
    [Theory]
    [InlineData("--modul")]
    [InlineData("--modulus")]
    public void CliCarpaniPlanSecenegineIniyor(string yazim)
    {
        var parsed = CliParser.Parse(["plan", "a.mp4", "--hedef", "25", yazim, "16"]);

        Assert.True(parsed.Ok, parsed.ErrorKey);
        Assert.Equal(16, parsed.Request!.ScaleModulus);
        Assert.Equal(16, parsed.Request.ToPlanOptions(10).ScaleModulus);
    }

    /// <summary>Olumsuz kontrol: bayraksız koşumda motorun varsayılanı kalıyor.</summary>
    [Fact]
    public void BayraksizIstekteVarsayilanKaliyor()
    {
        var parsed = CliParser.Parse(["plan", "a.mp4", "--hedef", "25"]);

        Assert.Null(parsed.Request!.ScaleModulus);
        Assert.Equal(Olcek.VarsayilanModul, parsed.Request.ToPlanOptions(10).ScaleModulus);
    }

    /// <summary>Küme dışı ve bozuk değer adıyla reddediliyor, sessizce yuvarlanmıyor.</summary>
    [Theory]
    [InlineData("3")]
    [InlineData("0")]
    [InlineData("32")]
    [InlineData("iki")]
    public void CliKumeDisiDegeriReddediyor(string deger)
    {
        var parsed = CliParser.Parse(["plan", "a.mp4", "--hedef", "25", "--modul", deger]);

        Assert.False(parsed.Ok);
        Assert.Equal("error.bad-modulus", parsed.ErrorKey);
    }

    /// <summary>Gelişmiş panelin kutusu seçeneğe yazılıyor; bağlantının kaynak pimi.</summary>
    [Fact]
    public void GelismisPanelCarpaniMotoraYaziyor()
    {
        var kaynak = File.ReadAllText(TipSources.WindowCodePath);

        Assert.Contains("AdvancedText(CmbAdvModulus)", kaynak, System.StringComparison.Ordinal);
        Assert.Contains("options.ScaleModulus = modulus", kaynak, System.StringComparison.Ordinal);
    }
}
