using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Üst çubuktaki güncelleme rozetinin ölçüsü. Özel rafın güncelleme paneli ölçütü iki şey
/// istiyor: rozet her an konuşsun (denetim sessizce vazgeçemez) ve saat taşıyan durumlar
/// saatini <c>· HH:MM</c> kalıbıyla yazsın. İkisi de burada pimli.
/// </summary>
public sealed class GuncellemeRozetiTests
{
    [Theory]
    [InlineData(UpdateBadgeState.UpToDate, true)]
    [InlineData(UpdateBadgeState.Offline, true)]
    [InlineData(UpdateBadgeState.Checking, false)]
    [InlineData(UpdateBadgeState.NewVersion, false)]
    [InlineData(UpdateBadgeState.Installing, false)]
    [InlineData(UpdateBadgeState.Downloading, false)]
    [InlineData(UpdateBadgeState.Ready, false)]
    public void SaatiYalnizSonucBildirenDurumlarTasir(UpdateBadgeState durum, bool bekleniyor)
        => Assert.Equal(bekleniyor, UpdateBadge.CarriesTime(durum));

    [Fact]
    public void SaatliDurumGovdeninArdinaYirmiDortSaatlikDamgaKoyar()
    {
        var an = new DateTimeOffset(2026, 9, 13, 21, 5, 0, TimeSpan.FromHours(3));
        Assert.Equal("Güncel · 21:05", UpdateBadge.Compose(UpdateBadgeState.UpToDate, "Güncel", an));
    }

    /// <summary>
    /// Damga kullanıcının saat yereline göre 12 saatlik yazıya düşmemeli: öğleden sonraki
    /// bir an <c>09:05 PM</c> değil <c>21:05</c> olarak yazılıyor.
    /// </summary>
    [Fact]
    public void DamgaOnIkiSaatlikYerelYazimaDusmez()
    {
        var onceki = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            var an = new DateTimeOffset(2026, 9, 13, 21, 5, 0, TimeSpan.Zero);
            var metin = UpdateBadge.Compose(UpdateBadgeState.Offline, "Offline", an);
            Assert.EndsWith("· 21:05", metin, StringComparison.Ordinal);
            Assert.DoesNotContain("PM", metin, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = onceki;
        }
    }

    [Fact]
    public void SaatsizDurumGovdeyiOldugGibiBirakir()
        => Assert.Equal("Denetleniyor…", UpdateBadge.Compose(
            UpdateBadgeState.Checking, "Denetleniyor…", DateTimeOffset.Now));

    /// <summary>
    /// Denetimin eski hali ağ hatasında hiçbir şey söylemeden dönüyordu. Ölçü o dönüşün
    /// geri gelmediğini kaynaktan pimliyor: yakalama kolu rozeti çevrimdışıya yazmalı.
    /// </summary>
    [Fact]
    public void AgHatasiSessizceVazgecmez()
    {
        var kod = File.ReadAllText(TipSources.WindowCodePath);
        var basla = kod.IndexOf("private async Task CheckForUpdateAsync()", StringComparison.Ordinal);
        Assert.True(basla > 0, "denetim gövdesi bulunamadı");
        var govde = kod.Substring(basla, 1600);

        Assert.Contains("SetUpdateBadge(UpdateBadgeState.Checking)", govde, StringComparison.Ordinal);
        Assert.Contains("SetUpdateBadge(UpdateBadgeState.Offline)", govde, StringComparison.Ordinal);
        Assert.Contains("UpdateBadgeState.UpToDate", govde, StringComparison.Ordinal);
    }

    [Fact]
    public void RozetUstCubuktaDurur()
    {
        var axaml = File.ReadAllText(TipSources.WindowXamlPath);
        Assert.Contains("x:Name=\"BtnUpdateBadge\"", axaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"UpdateBadgeDot\"", axaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"OnUpdateBadgeClicked\"", axaml, StringComparison.Ordinal);
    }

    /// <summary>Rozetin dört metni 42 dilin hepsinde var; biri eksikse rozet o dilde susar.</summary>
    [Fact]
    public void DortMetinButunDillerdeVar()
    {
        var kok = Path.Combine(TipSources.Root, "src", "VidShrink.App", "Locales");
        var anahtarlar = new[]
        {
            "main.update.checking", "main.update.current",
            "main.update.offline", "main.update.starting",
            "main.update.badge", "main.update.downloading", "main.update.ready", "main.update.failed",
            "main.update.log.manifest", "main.update.log.found", "main.update.log.current",
            "main.update.log.unreachable", "main.action.download", "main.action.downloadinstall"
        };

        var diller = Directory.GetDirectories(kok);
        Assert.True(diller.Length >= 42, $"dil sayısı beklenenden az: {diller.Length}");

        foreach (var dil in diller)
        {
            using var akis = File.OpenRead(Path.Combine(dil, "main.json"));
            using var belge = JsonDocument.Parse(akis);
            foreach (var anahtar in anahtarlar)
            {
                Assert.True(belge.RootElement.TryGetProperty(anahtar, out var deger),
                    $"{Path.GetFileName(dil)} dilinde {anahtar} yok");
                Assert.False(string.IsNullOrWhiteSpace(deger.GetString()),
                    $"{Path.GetFileName(dil)} dilinde {anahtar} boş");
            }
        }
    }

    /// <summary>
    /// İndirme hızı yalnız oynatıcı oynarken sınırlanır: boşta bekleme sıfır, oynarken
    /// okunan bayt saniyelik tavanı aşınca fark kadar bekler, oynatma durunca pencere sıfırlanır.
    /// </summary>
    [Fact]
    public void IndirmeYalnizOynarkenYavaslar()
    {
        var oynuyor = false;
        var saat = TimeSpan.Zero;
        var fren = new DownloadThrottle(1000, () => oynuyor, () => saat);

        Assert.Equal(TimeSpan.Zero, fren.Account(10_000));

        oynuyor = true;
        Assert.Equal(TimeSpan.FromSeconds(2), fren.Account(2000));
        saat = TimeSpan.FromSeconds(1);
        Assert.Equal(TimeSpan.FromSeconds(2), fren.Account(1000));
        saat = TimeSpan.FromSeconds(10);
        Assert.Equal(TimeSpan.Zero, fren.Account(1000));

        oynuyor = false;
        Assert.Equal(TimeSpan.Zero, fren.Account(50_000));
        oynuyor = true;
        Assert.Equal(TimeSpan.FromSeconds(1), fren.Account(1000));
    }
}
