using System.Text;
using VidShrink.App.Playback;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Oynatıcı araçlarında kare dışa aktarma ve küçük resim tablosu (later 82). Saf ölçüler kare
/// sayısını, tablo boyutunu ve argümanı; canlı ölçü kısa üretilmiş klipte yazılan dosya sayısını
/// ve tablonun ffprobe ile okunan boyutunu tutar.
/// </summary>
public sealed class OynaticiKareTabloTests
{
    [Theory]
    [InlineData(4, 1, 4)]
    [InlineData(3.5, 1, 3)]
    [InlineData(0.3, 1, 1)]
    [InlineData(10, 2.5, 4)]
    [InlineData(1, 0.1, 10)]
    public void KareSayisiAraligaVeAdimaGoreHesaplanir(double sure, double adim, int beklenen)
        => Assert.Equal(beklenen, FrameExport.FrameCount(sure, adim));

    [Theory]
    [InlineData(4, 9, 25, 4, 5)]
    [InlineData(double.NaN, double.NaN, 25, 0, 25)]
    [InlineData(9, 4, 25, 0, 25)]
    [InlineData(double.NaN, double.NaN, double.NaN, 0, 0)]
    public void AralikIsaretlerdenYoksaDosyaninTamamindanAlinir(double a, double b, double sure, double bas, double uzunluk)
        => Assert.Equal((bas, uzunluk), PlayerView.FrameRange(a, b, sure));

    [Fact]
    public void KareSayisiUstSinirdaKesilir()
        => Assert.Equal(FrameExport.MaximumFrames, FrameExport.FrameCount(100000, 0.1));

    [Theory]
    [InlineData(3, 2, 160, 480, 212)]
    [InlineData(4, 4, 320, 1280, 752)]
    [InlineData(1, 1, 80, 80, 78)]
    public void TabloBoyutuSatirSutunVeKareCarpimidir(int sutun, int satir, int genislik, int beklenenG, int beklenenY)
    {
        var boyut = FrameExport.SheetSize(sutun, satir, genislik);
        Assert.Equal((beklenenG, beklenenY), boyut);
        Assert.Equal(FrameExport.CellHeight(genislik) * satir + FrameExport.HeaderHeight, boyut.Height);
    }

    [Fact]
    public void KareArgumaniIkiIsParcacigiVeNumaraliDesenTasir()
    {
        var hedef = Path.Combine("C:\\cikti", "film%50_frames");
        var istek = new FrameRequest("C:\\kaynak\\film.mp4", hedef, 4, 4, FrameKind.Frames, IntervalSeconds: 1);
        var args = FrameExport.Arguments(istek, null, null);

        Assert.Equal("2", args[args.ToList().IndexOf("-threads") + 1]);
        Assert.Equal("fps=1/1", args[args.ToList().IndexOf("-vf") + 1]);
        Assert.Equal("4", args[args.ToList().IndexOf("-frames:v") + 1]);
        Assert.Equal("4", args[args.ToList().IndexOf("-ss") + 1]);
        Assert.Equal(Path.Combine("C:\\cikti", "film%%50_frames", "film%%50_frames_%04d.png"), args[^1]);
    }

    [Fact]
    public void TabloSuzgeciEsitAralikliKareleriDiziyorVeBaslikYaziyor()
    {
        var istek = new FrameRequest("C:\\kaynak\\film.mp4", "C:\\cikti\\film_sheet.png", 0, 4, FrameKind.Sheet, Columns: 3, Rows: 2, CellWidth: 160);
        var args = FrameExport.Arguments(istek, "C:\\cikti\\film_sheet.png.txt", "C:\\Windows\\Fonts\\arial.ttf");
        var suzgec = args[args.ToList().IndexOf("-vf") + 1];

        Assert.StartsWith("fps=6/4,scale=160:90:force_original_aspect_ratio=decrease,pad=160:90:(ow-iw)/2:(oh-ih)/2,tile=3x2,pad=iw:ih+32:0:32,", suzgec, StringComparison.Ordinal);
        Assert.Contains("fontfile='C\\:/Windows/Fonts/arial.ttf':", suzgec, StringComparison.Ordinal);
        Assert.Contains("textfile='C\\:/cikti/film_sheet.png.txt':", suzgec, StringComparison.Ordinal);
        Assert.Contains("expansion=none", suzgec, StringComparison.Ordinal);
        Assert.Equal("1", args[args.ToList().IndexOf("-frames:v") + 1]);
        Assert.Equal("2", args[args.ToList().IndexOf("-threads") + 1]);

        var yaziTipsiz = FrameExport.Arguments(istek, "C:\\t.txt", null);
        Assert.DoesNotContain("fontfile=", yaziTipsiz[yaziTipsiz.ToList().IndexOf("-vf") + 1], StringComparison.Ordinal);
    }

    [Fact]
    public void BaslikDosyaAdiniAraligiVeIzgarayiSoyler()
    {
        var baslik = FrameExport.Header("C:\\kaynak\\tatil.mp4", 65, 30, 4, 3);
        Assert.Contains("tatil.mp4", baslik, StringComparison.Ordinal);
        Assert.Contains("1:05", baslik, StringComparison.Ordinal);
        Assert.Contains("1:35", baslik, StringComparison.Ordinal);
        Assert.Contains("4x3", baslik, StringComparison.Ordinal);
        Assert.DoesNotContain('\n', baslik);
    }

    [Fact]
    public void KareVeTabloAyarlariYazilirOkunurSinirlanir()
    {
        var klasor = AracKanit.Gecici("kare-ayar");
        try
        {
            var dosya = Path.Combine(klasor, ToolsOptions.FileName);
            var ayar = new ToolsOptions();
            ayar.UseFrameSeconds(2.5);
            ayar.UseSheetGrid(5, 3);
            ayar.UseSheetWidth(241);
            ayar.Save(dosya);

            var okunan = ToolsOptions.Load(dosya);
            Assert.Equal(2.5, okunan.FrameSeconds);
            Assert.Equal(5, okunan.SheetColumns);
            Assert.Equal(3, okunan.SheetRows);
            Assert.Equal(242, okunan.SheetWidth);
            Assert.Contains("frames=2.5 sheet=5x3/242", okunan.Describe(), StringComparison.Ordinal);

            okunan.Reset();
            Assert.Equal(ToolsOptions.DefaultFrameSeconds, okunan.FrameSeconds);
            Assert.Equal(ToolsOptions.DefaultSheetColumns, okunan.SheetColumns);
            Assert.Equal(ToolsOptions.DefaultSheetRows, okunan.SheetRows);
            Assert.Equal(ToolsOptions.DefaultSheetWidth, okunan.SheetWidth);

            var sinir = new ToolsOptions();
            sinir.UseFrameSeconds(0.01);
            sinir.UseSheetGrid(0, 99);
            sinir.UseSheetWidth(5000);
            Assert.Equal(ToolsOptions.MinimumFrameSeconds, sinir.FrameSeconds);
            Assert.Equal(ToolsOptions.MinimumSheetCells, sinir.SheetColumns);
            Assert.Equal(ToolsOptions.MaximumSheetCells, sinir.SheetRows);
            Assert.Equal(ToolsOptions.MaximumSheetWidth, sinir.SheetWidth);
            sinir.UseFrameSeconds(double.NaN);
            Assert.Equal(ToolsOptions.DefaultFrameSeconds, sinir.FrameSeconds);
        }
        finally
        {
            Directory.Delete(klasor, recursive: true);
        }
        Assert.False(Directory.Exists(klasor));
    }

    [Fact]
    public void KaynakYokkenKomutlarDosyaYazmazVeSoyler()
    {
        var izler = AppHost.Run(() =>
        {
            var view = GirdiSurucu.Kur(out var window);
            var sonuc = new List<string>();
            foreach (var eylem in new[] { ToolsOptions.Frames, ToolsOptions.Sheet })
            {
                view.Apply(eylem.ToCommand());
                sonuc.Add(view.Trace[^1]);
                sonuc.Add(view.ViewStateText.Contains(VidShrink.App.Localization.Strings.Get("player.tools.novideo"), StringComparison.Ordinal).ToString());
            }

            window.Close();
            return sonuc;
        });

        Assert.Equal(new[] { "frames -> no", "True", "sheet -> no", "True" }, izler);
    }

    [Fact]
    public async Task KareSayisiVeTabloBoyutuCanliKliptenOlculur()
    {
        var clip = MotorKlipleri.Kucuk;
        var klasor = AracKanit.Gecici("kare-tablo");
        try
        {
            var body = new StringBuilder();

            var kareKlasoru = Path.Combine(klasor, "kucuk_frames");
            var kareSonuc = await FrameExport.RunAsync(new FrameRequest(clip, kareKlasoru, 4, 4, FrameKind.Frames, IntervalSeconds: 1));
            var kareler = Directory.Exists(kareKlasoru) ? Directory.GetFiles(kareKlasoru, "*.png").OrderBy(f => f, StringComparer.Ordinal).ToList() : new List<string>();
            var ilkKare = kareler.Count > 0 ? GorunumKanit.Boyut(kareler[0]) : (0, 0);
            body.AppendLine($"kareler -> ok {kareSonuc.Ok}, sayi {kareSonuc.Count}, dosya {kareler.Count}: {string.Join(", ", kareler.Select(Path.GetFileName))}, ilk {ilkKare.Item1}x{ilkKare.Item2}");

            var tabloHedef = Path.Combine(klasor, "kucuk_sheet.png");
            var tabloSonuc = await FrameExport.RunAsync(new FrameRequest(clip, tabloHedef, 0, 4, FrameKind.Sheet, Columns: 3, Rows: 2, CellWidth: 160,
                Header: FrameExport.Header(clip, 0, 4, 3, 2)));
            var tablo = File.Exists(tabloHedef) ? GorunumKanit.Boyut(tabloHedef) : (0, 0);
            body.AppendLine($"tablo -> ok {tabloSonuc.Ok}, {tablo.Item1}x{tablo.Item2}, baslik dosyasi kaldi mi {File.Exists(tabloHedef + ".txt")}, hata '{tabloSonuc.Error}'");

            var kotuKlasor = Path.Combine(klasor, "olmaz_frames");
            var kotuSonuc = await FrameExport.RunAsync(new FrameRequest(clip, kotuKlasor, 4, 0, FrameKind.Frames));
            body.AppendLine($"NEGATIF sifir sureli aralik -> ok {kotuSonuc.Ok}, klasor {Directory.Exists(kotuKlasor)}");

            var yokSonuc = await FrameExport.RunAsync(new FrameRequest(Path.Combine(klasor, "yok.mp4"), Path.Combine(klasor, "yok_sheet.png"), 0, 4, FrameKind.Sheet, Header: "yok"));
            body.AppendLine($"NEGATIF olmayan kaynak -> ok {yokSonuc.Ok}, dosya {File.Exists(Path.Combine(klasor, "yok_sheet.png"))}, baslik {File.Exists(Path.Combine(klasor, "yok_sheet.png.txt"))}");

            AracKanit.Write("kare-tablo.txt", body.ToString());

            Assert.True(kareSonuc.Ok, kareSonuc.Error);
            Assert.Equal(FrameExport.FrameCount(4, 1), kareler.Count);
            Assert.Equal(kareler.Count, kareSonuc.Count);
            Assert.Equal((320, 180), ilkKare);
            Assert.EndsWith("kucuk_frames_0001.png", kareler[0], StringComparison.Ordinal);

            Assert.True(tabloSonuc.Ok, tabloSonuc.Error);
            Assert.Equal(FrameExport.SheetSize(3, 2, 160), tablo);
            Assert.False(File.Exists(tabloHedef + ".txt"));

            Assert.False(kotuSonuc.Ok);
            Assert.False(Directory.Exists(kotuKlasor));
            Assert.False(yokSonuc.Ok);
            Assert.False(File.Exists(Path.Combine(klasor, "yok_sheet.png")));
            Assert.False(File.Exists(Path.Combine(klasor, "yok_sheet.png.txt")));
            AracKanit.Kapat("kare-tablo.txt");
        }
        finally
        {
            Directory.Delete(klasor, recursive: true);
        }
        Assert.False(Directory.Exists(klasor));
    }
}
