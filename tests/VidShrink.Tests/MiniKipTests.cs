using System;
using System.IO;
using System.Linq;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Mini kipin kararları. Ölçülen şey çizim değil, kaynaktaki hüküm: hangi öğe şeritte
/// duruyor, hangisi büyük pencerede kalıyor, hep üstte kalma nereye bağlı ve geçiş
/// düğmesi ayar kutusuna mı gömülmüş.
///
/// <para>Kararların kaynağı <c>docs/arastirma/kaydedici-mini-arayuz-2026-09-15.md</c> ve
/// <c>docs/arastirma/kaydedici-kucuk-denetim-seridi-2026-09-15.md</c>; ölçüler
/// <c>MiniKipOlcusuTests</c>'te ölçülüyor.</para>
/// </summary>
public class MiniKipTests
{
    private static string Oku(params string[] parca)
        => File.ReadAllText(Path.Combine(TipSources.Root, Path.Combine(parca))).Replace("\r\n", "\n");

    /// <summary>
    /// Şeritte yalnız kaydın temel öğeleri var: nokta, sayaç, kayıt yokken bölge seçimi, ayarlar,
    /// tek düğmeye indirilmiş başlat/duraklat, durdur, büyüt. Kare ve düşen kare okumaları büyük
    /// pencerede kalıyor — sahadaki hiçbir kompakt yüzeyde ikiden fazla okuma yok.
    /// </summary>
    [Fact]
    public void SeritteYalnizTemelOgelerVar()
    {
        var mini = Oku("src", "VidShrink.App", "Recorder", "RecorderMini.axaml");

        Assert.Contains("x:Name=\"LiveDot\"", mini);
        Assert.Contains("x:Name=\"TxtElapsed\"", mini);
        Assert.Contains("x:Name=\"BtnToggle\"", mini);
        Assert.Contains("x:Name=\"BtnStop\"", mini);
        Assert.Contains("x:Name=\"BtnExpand\"", mini);
        Assert.Contains("x:Name=\"BtnOptions\"", mini);
        Assert.Contains("x:Name=\"BtnRegion\"", mini);

        Assert.DoesNotContain("TxtFrames", mini);
        Assert.DoesNotContain("TxtDropped", mini);
        Assert.DoesNotContain("CmbTarget", mini);
        Assert.DoesNotContain("ResultPanel", mini);

        Assert.Equal(5, mini.Split("x:Name=\"Btn").Length - 1);
    }

    /// <summary>Üç ölçü de belirteçten okunuyor; XAML'a sayı yazılmıyor.</summary>
    [Fact]
    public void OlculerBelirtecten()
    {
        var mini = Oku("src", "VidShrink.App", "Recorder", "RecorderMini.axaml");
        var tema = Oku("src", "VidShrink.App", "Themes", "Recorder.axaml");

        Assert.Contains("MinWidth=\"{StaticResource RecorderMiniMinWidth}\"", mini);
        Assert.Contains("Height=\"{StaticResource RecorderMiniHeight}\"", mini);
        Assert.Contains("MinWidth=\"{StaticResource RecorderMiniReadoutWidth}\"", mini);

        Assert.Contains("x:Key=\"RecorderMiniHeight\">56<", tema);
        Assert.Contains("x:Key=\"RecorderMiniReadoutWidth\">72<", tema);
        Assert.Contains("x:Key=\"RecorderMiniMinWidth\">276<", tema);
    }

    /// <summary>
    /// Hep üstte yalnız kayıt sürerken. LICEcap'in kuralı: kayıt yokken sürekli üstte
    /// duran pencere yoldan çekilmiyor.
    /// </summary>
    [Fact]
    public void HepUsttelikKayitlaBagli()
    {
        var kod = Oku("src", "VidShrink.App", "Recorder", "RecorderMini.axaml.cs");

        Assert.Contains("Topmost = running || paused || counting;", kod);
        Assert.DoesNotContain("Topmost=\"True\"", Oku("src", "VidShrink.App", "Recorder", "RecorderMini.axaml"));
    }

    /// <summary>Sayacın kendisi tutamak: ayrı bir tutamak sütunu yer yiyordu.</summary>
    [Fact]
    public void SayacTutamak()
    {
        var mini = Oku("src", "VidShrink.App", "Recorder", "RecorderMini.axaml");
        var kod = Oku("src", "VidShrink.App", "Recorder", "RecorderMini.axaml.cs");

        var sayac = mini[mini.IndexOf("x:Name=\"TxtElapsed\"", StringComparison.Ordinal)..];
        var kapanis = sayac.IndexOf("/>", StringComparison.Ordinal);

        Assert.Contains("Cursor=\"SizeAll\"", sayac[..kapanis]);
        Assert.Contains("PointerPressed=\"OnDrag\"", sayac[..kapanis]);
        Assert.Contains("BeginMoveDrag(e)", kod);
    }

    /// <summary>
    /// Geçiş şeridin kendi düğmesinde, ayar kutusunda değil: ScreenToGif'in kompakt kipi
    /// Ayarlar altında bir onay kutusu ve kullanıcı bulamazsa hiç kullanmıyor.
    /// </summary>
    [Fact]
    public void GecisSeritteAyardaDegil()
    {
        var gorunum = Oku("src", "VidShrink.App", "Recorder", "RecorderView.axaml");
        var serit = gorunum[gorunum.IndexOf("x:Name=\"Strip\"", StringComparison.Ordinal)..];
        var sonuc = serit.IndexOf("x:Name=\"ResultPanel\"", StringComparison.Ordinal);

        Assert.Contains("x:Name=\"BtnMini\"", serit[..sonuc]);
        Assert.DoesNotContain("recorder.mini", Oku("src", "VidShrink.App", "Recorder", "RecorderSettings.cs"));
    }

    /// <summary>
    /// Şerit kadrajın dışına konumlanıyor; bölge yoksa kullanıcıya şeridin kayda gireceği
    /// söyleniyor. gdigrab kadrajı yakalarken şerit içeri düşerse kayda karışır.
    /// </summary>
    [Fact]
    public void KadrajinDisinaKonumlaniyor()
    {
        var kod = Oku("src", "VidShrink.App", "Recorder", "RecorderMini.axaml.cs");
        var bag = Oku("src", "VidShrink.App", "Recorder", "RecorderView.Mini.cs");

        Assert.Contains("internal void PlaceOutside(PixelRect? region)", kod);
        Assert.Contains("frame.Bottom + Clearance", kod);
        Assert.Contains("_mini.PlaceOutside(region)", bag);
        Assert.Contains("recorder.mini.in-frame", bag);
    }

    /// <summary>
    /// F7 başlat/duraklat, F8 durdur. ScreenToGif ve TinyTask'ın varsayılanı; Oyun
    /// Çubuğu'nun Win+Alt+R'siyle çakışmıyor. Tuş iki pencerede de aynı kancaya bağlı.
    /// </summary>
    [Fact]
    public void SicakTuslarTekYerde()
    {
        var bag = Oku("src", "VidShrink.App", "Recorder", "RecorderView.Mini.cs");
        var tanim = Oku("src", "VidShrink.App", "Recorder", "RecorderHotkeys.cs");

        Assert.Contains("new(HotkeyAction.Toggle, Key.F7, 0x76)", tanim);
        Assert.Contains("new(HotkeyAction.Stop, Key.F8, 0x77)", tanim);
        Assert.Contains("new(HotkeyAction.Frame, Key.F9, 0x78)", tanim);
        Assert.Contains("RecorderHotkeys.ActionOf(e.Key, e.KeyModifiers)", bag);
        Assert.DoesNotContain("case Key.", bag);
        Assert.Contains("_mini.AddHandler(KeyDownEvent, OnHotkey", bag);
    }

    /// <summary>Kayıt bitince mini kip kapanıyor, sonuç büyük pencerede açılıyor.</summary>
    [Fact]
    public void KayitBitinceBuyugeDonuyor()
    {
        var serit = Oku("src", "VidShrink.App", "Recorder", "RecorderView.Serit.cs");

        var don = serit.IndexOf("ExpandFromMini();", StringComparison.Ordinal);
        var sonuc = serit.IndexOf("ShowResult(result);", StringComparison.Ordinal);

        Assert.True(don > 0, "kayit bitince mini kip kapanmiyor");
        Assert.True(don < sonuc, "sonuc panelinden once buyuk pencereye donulmeli");
    }

    /// <summary>Beş yeni anahtar 42 dilin hepsinde var.</summary>
    [Fact]
    public void AnahtarlarKirkIkiDilde()
    {
        var kok = Path.Combine(TipSources.Root, "src", "VidShrink.App", "Locales");
        var diller = Directory.GetDirectories(kok);
        Assert.Equal(42, diller.Length);

        string[] anahtarlar =
        [
            "recorder.mini.title",
            "recorder.mini.shrink",
            "recorder.mini.expand",
            "recorder.mini.drag-hint",
            "recorder.mini.in-frame",
        ];

        var eksik = diller
            .Select(d => (Dil: Path.GetFileName(d), Metin: File.ReadAllText(Path.Combine(d, "recorder.json"))))
            .SelectMany(p => anahtarlar.Where(a => !p.Metin.Contains('"' + a + '"')).Select(a => p.Dil + ": " + a))
            .ToArray();

        Assert.True(eksik.Length == 0, string.Join(", ", eksik));
    }
}
