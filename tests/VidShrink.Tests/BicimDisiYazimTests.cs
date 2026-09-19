using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Biçim gövdelerinin dışında kalan elle yazım taraması. <c>docs/plan.md</c>'nin Ölçü
/// bölümünde söz verilmişti: on adım boyunca ~110 çağrı yeri gövdelere indi ama geri
/// dönüşü durduran bir ölçü yoktu — yarın eklenen bir satır aynı kusuru sessizce geri
/// getirebiliyordu.
///
/// <para>İki desen aranıyor: kültür verilmeyen sayısal <c>ToString("0…")</c> ve
/// <see cref="System.Globalization.CultureInfo.CurrentCulture"/>. İkisi de aynı şeyi
/// yapıyor — makinenin kültürünü okuyor, arayüzün dilini değil.</para>
///
/// <para>Beklenen küme elle listelenmiyor: muafiyetler <c>Veri/bicim-muafiyetleri.txt</c>
/// dosyasından okunuyor ve muafiyet dosyasında karşılığı kalmayan satır ölçüyü kırıyor.
/// Böylece muafiyet listesi de ağaçla birlikte bayatlayamıyor.</para>
/// </summary>
public sealed class BicimDisiYazimTests
{
    private static readonly Regex KultursuzSayi = new(@"\.ToString\(""[0#][^""]*""\)", RegexOptions.Compiled);
    private static readonly Regex MakineKulturu = new(@"CultureInfo\.CurrentCulture", RegexOptions.Compiled);

    private static string Kok
    {
        get
        {
            var dizin = new DirectoryInfo(AppContext.BaseDirectory);
            while (dizin is not null && !File.Exists(Path.Combine(dizin.FullName, "VidShrink.sln")))
                dizin = dizin.Parent;
            return dizin?.FullName ?? throw new InvalidOperationException("VidShrink.sln bulunamadı.");
        }
    }

    private static IReadOnlyList<string> Kaynaklar() => Directory
        .EnumerateFiles(Path.Combine(Kok, "src"), "*.cs", SearchOption.AllDirectories)
        .Where(yol => !yol.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                   && !yol.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        .OrderBy(yol => yol, StringComparer.Ordinal)
        .ToList();

    private static IReadOnlyList<string> Bulgular()
    {
        var bulunan = new List<string>();
        foreach (var yol in Kaynaklar())
        {
            var goreli = Path.GetRelativePath(Kok, yol).Replace('\\', '/');
            var satirlar = File.ReadAllLines(yol);
            for (var i = 0; i < satirlar.Length; i++)
                if (KultursuzSayi.IsMatch(satirlar[i]) || MakineKulturu.IsMatch(satirlar[i]))
                    bulunan.Add(goreli + ":" + (i + 1));
        }

        return bulunan;
    }

    private static IReadOnlyList<string> Muafiyetler()
    {
        var yol = Path.Combine(Kok, "tests", "VidShrink.Tests", "Veri", "bicim-muafiyetleri.txt");
        return File.ReadAllLines(yol)
            .Select(satir => satir.Trim())
            .Where(satir => satir.Length > 0 && !satir.StartsWith('#'))
            .Select(satir => satir.Split('#')[0].Trim())
            .ToList();
    }

    [Fact]
    public void BicimDisiYazimYok()
    {
        var muaf = Muafiyetler().Select(s => s.Split(':')[0]).ToHashSet(StringComparer.Ordinal);
        var kalan = Bulgular().Where(b => !muaf.Contains(b.Split(':')[0])).ToList();

        Assert.True(kalan.Count == 0,
            "Gövde dışında elle yazım kaldı:" + Environment.NewLine + string.Join(Environment.NewLine, kalan));
    }

    /// <summary>
    /// Muafiyet listesi ağaçla birlikte bayatlamasın: listedeki her dosya hâlâ var ve
    /// hâlâ desene takılıyor olmalı. Takılmayan satır listeden düşer.
    /// </summary>
    [Fact]
    public void MuafiyetListesiBayatDegil()
    {
        var bulunan = Bulgular().Select(b => b.Split(':')[0]).ToHashSet(StringComparer.Ordinal);
        var olu = Muafiyetler().Select(s => s.Split(':')[0]).Where(d => !bulunan.Contains(d)).Distinct().ToList();

        Assert.True(olu.Count == 0,
            "Muafiyet listesinde karşılığı kalmayan satır var:" + Environment.NewLine + string.Join(Environment.NewLine, olu));
    }

    /// <summary>
    /// Taramanın kör olmadığının olumlu kontrolü: iki desen de gerçekten eşleşiyor.
    /// Desen bozulursa ölçü sessizce yeşil dönmesin.
    /// </summary>
    [Fact]
    public void DesenKorDegil()
    {
        Assert.Matches(KultursuzSayi, @"x.ToString(""0.0"")");
        Assert.Matches(MakineKulturu, "value.ToString(CultureInfo.CurrentCulture)");
        Assert.DoesNotMatch(KultursuzSayi, @"x.ToString(""0.0"", kultur)");
        Assert.True(Kaynaklar().Count > 100, "Tarama kaynak dosyalarını bulamadı.");
    }
}
