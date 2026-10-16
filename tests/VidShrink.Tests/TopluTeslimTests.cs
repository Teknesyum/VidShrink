using System.Diagnostics;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Threading;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// later.md #40, DZA-09: düzenleyicinin toplu teslimi. Proje dosyaları sırayla, komut satırı teslimiyle aynı yoldan
/// (<see cref="EditBatch.ExportOneAsync"/>) teslim edilir; okunamayan, eskimiş ya da geri sarma sınırını aşan proje
/// sırayı durdurmaz. Hazırlık ve koşu sahte; ffmpeg koşmaz. Geçici dosyalar <c>.calisma/test-ciktilari/toplu-teslim/</c>.
/// </summary>
public sealed class TopluTeslimTests : IDisposable
{
    private readonly string _klasor = Path.Combine(TestPaths.OutputRoot, "toplu-teslim", Guid.NewGuid().ToString("N")[..8]);

    public TopluTeslimTests() => Directory.CreateDirectory(_klasor);

    public void Dispose()
    {
        try { Directory.Delete(_klasor, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static long S(double saniye) => EditTime.FromSeconds(saniye);

    private SourceStamp Kaynak(string ad) => new(Path.Combine(_klasor, ad), 1000, 11);

    private string Proje(string ad, SourceStamp kaynak)
    {
        var yol = Path.Combine(_klasor, ad + EditProject.Extension);
        var model = new EditTimeline(new[] { new EditClip(S(1), S(3)), new EditClip(S(5), S(6)) }, S(10), null, null);
        Assert.True(EditProject.From(kaynak, model, "Fast").Write(yol));
        return yol;
    }

    private static ExportPlan Plan(string cikti, params int[] tersAsan)
        => new(ExportMode.Fast, ExportMode.Fast, Array.Empty<ExportStep>(), tersAsan, 90, cikti, cikti + ".is");

    private sealed class Sahte
    {
        public readonly List<string> Hazirlanan = new();
        public readonly List<string> Kosan = new();
        public int[] TersAsan = Array.Empty<int>();

        public Task<ExportPlan> Hazirla(EditProjectJob is_, string cikti, CancellationToken _)
        {
            Hazirlanan.Add(is_.Sources[0]);
            return Task.FromResult(Plan(cikti, TersAsan));
        }

        public Task Kos(ExportPlan plan, CancellationToken _)
        {
            Kosan.Add(plan.OutputPath);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Dört proje: sağlam, bozuk JSON, kaynağı değişmiş, sağlam. Yalnız sağlamlar hazırlanır ve koşar; ikinci ve üçüncü
    /// kendi nedeniyle düşer, dördüncü yine teslim edilir. Çıktı adı kaynağın yanında, komut satırınınkiyle aynı kural.
    /// </summary>
    [Fact]
    public async Task DusenProjeSirayiDurdurmaz()
    {
        var a = Kaynak("a.mp4");
        var b = Kaynak("b.mp4");
        var c = Kaynak("c.mp4");
        var bozuk = Path.Combine(_klasor, "bozuk" + EditProject.Extension);
        File.WriteAllText(bozuk, "{bozuk");
        var yollar = new[] { Proje("bir", a), bozuk, Proje("uc", b), Proje("dort", c) };
        SourceStamp? Damga(string yol) => yol == b.Path ? b with { Size = 999 } : new[] { a, b, c }.FirstOrDefault(k => k.Path == yol);
        var sahte = new Sahte();
        var sira = new EditBatch(yollar);

        var ozet = await sira.RunAsync((oge, ct) => EditBatch.ExportOneAsync(oge.Path, Damga, sahte.Hazirla, sahte.Kos, ct), null, CancellationToken.None);

        Assert.Equal(new ConversionQueueSummary(2, 2, 0), ozet);
        Assert.Equal(new[] { ConversionItemState.Done, ConversionItemState.Failed, ConversionItemState.Failed, ConversionItemState.Done },
            sira.Items.Select(o => o.State));
        Assert.Equal(EditBatchFailure.Unreadable, Assert.IsType<EditBatchException>(sira.Items[1].Error).Reason);
        var bayat = Assert.IsType<EditBatchException>(sira.Items[2].Error);
        Assert.Equal(EditBatchFailure.Stale, bayat.Reason);
        Assert.Equal(new[] { a.Path, c.Path }, sahte.Hazirlanan);
        Assert.Equal(new[] { EditOutputName.For(a.Path), EditOutputName.For(c.Path) }, sahte.Kosan);
        Assert.Equal(sahte.Kosan, new[] { sira.Items[0].Output, sira.Items[3].Output });
    }

    /// <summary>Hazırlık ters klibin bellek sınırını aştığını söylerse proje koşmadan düşer; ileti klip numaralarını 1'den sayar.</summary>
    [Fact]
    public async Task TersSiniriAsanProjeKosmaz()
    {
        var a = Kaynak("a.mp4");
        var sahte = new Sahte { TersAsan = new[] { 0, 2 } };

        var hata = await Assert.ThrowsAsync<EditBatchException>(() =>
            EditBatch.ExportOneAsync(Proje("ters", a), yol => yol == a.Path ? a : null, sahte.Hazirla, sahte.Kos, CancellationToken.None));

        Assert.Equal(EditBatchFailure.ReverseLimit, hata.Reason);
        Assert.Equal("1, 3", hata.Detail);
        Assert.Equal(90, hata.LimitSeconds);
        Assert.Empty(sahte.Kosan);
    }

    /// <summary>İlk proje koşarken iptal: o proje ve kalanlar iptal sayılır, hiçbiri düşmüş görünmez.</summary>
    [Fact]
    public async Task IptalKalanlariIptalSayar()
    {
        using var cts = new CancellationTokenSource();
        var sira = new EditBatch(new[] { "x", "y", "z" });

        var ozet = await sira.RunAsync((_, ct) => { cts.Cancel(); ct.ThrowIfCancellationRequested(); return Task.FromResult(""); }, null, cts.Token);

        Assert.Equal(new ConversionQueueSummary(0, 0, 3), ozet);
    }

    /// <summary>Aynı proje ikinci kez eklenmez; yalnız yeni yollar döner.</summary>
    [Fact]
    public void AyniProjeIkiKezEklenmez()
    {
        var sira = new EditBatch(new[] { Path.Combine(_klasor, "a" + EditProject.Extension) });

        var eklenen = sira.Add(new[] { Path.Combine(_klasor, "A" + EditProject.Extension), Path.Combine(_klasor, "b" + EditProject.Extension) });

        Assert.Equal(string.Equals("a", "A", WatchFolder.PathComparison) ? 2 : 3, sira.Items.Count);
        Assert.Contains(eklenen, o => o.Path.EndsWith("b" + EditProject.Extension, StringComparison.Ordinal));
    }

    /// <summary>
    /// Pencere: üç projenin ortancası eskimiş kaynakla düşer. Satır durumları, düşen satırın ipucundaki yerel neden,
    /// özet, klasörde göster ve başlat düğmesinin hâli.
    /// </summary>
    [Fact]
    public void PencereOrtadakiProjeDuser()
    {
        var ayar = Path.Combine(_klasor, "ayar", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(ayar)!);
        var sonuc = AppHost.Run(() =>
        {
            var kultur = CultureInfo.CurrentUICulture;
            var eskiAyar = Environment.GetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH");
            CultureInfo.CurrentUICulture = new CultureInfo("en");
            Environment.SetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH", ayar);
            EditBatchWindow pencere;
            try
            {
                pencere = new EditBatchWindow(new[] { "a" + EditProject.Extension, "b" + EditProject.Extension });
                pencere.Add(new[] { "c" + EditProject.Extension, "a" + EditProject.Extension });
            }
            finally
            {
                Environment.SetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH", eskiAyar);
                CultureInfo.CurrentUICulture = kultur;
            }

            try
            {
                var basta = (Baslik: pencere.TxtHeadline.Text, Basla: pencere.BtnStart.IsEnabled);
                pencere.Kosturucu = (yol, _, _) => yol.StartsWith('b')
                    ? Task.FromException<string>(new EditBatchException(EditBatchFailure.Stale, "b.mp4"))
                    : Task.FromResult(yol + ".mp4");
                var is_ = pencere.StartAsync();
                Dongu(() => is_.IsCompleted, 30);
                is_.GetAwaiter().GetResult();
                return (Basta: basta, Durum: Enumerable.Range(0, 3).Select(pencere.StateTextFor).ToArray(),
                    Hata: Enumerable.Range(0, 3).Select(pencere.ErrorTextFor).ToArray(), Ozet: pencere.TxtSummary.Text,
                    Goster: pencere.BtnReveal.IsVisible, Basla: pencere.BtnStart.IsEnabled, Ekle: pencere.BtnAdd.IsEnabled);
            }
            finally
            {
                pencere.Close();
                Strings.Use("en");
            }
        });

        string Say(string anahtar, params object?[] args) => LanguageCatalog.Title(Strings.GetIn("en", anahtar, args), "en");
        Assert.Equal((Say("editor.batch.headline", 3), true), sonuc.Basta);
        Assert.Equal(new[] { Say("main.convert-queue.done"), Say("main.convert-queue.failed"), Say("main.convert-queue.done") }, sonuc.Durum);
        Assert.Equal(new[] { null, Say("editor.project.stale"), null }, sonuc.Hata);
        Assert.Equal(Say("main.convert-queue.summary", 2, 1, 0), sonuc.Ozet);
        Assert.True(sonuc.Goster);
        Assert.False(sonuc.Basla);
        Assert.False(sonuc.Ekle);
    }

    private static void Dongu(Func<bool> bitti, int saniye)
    {
        var saat = Stopwatch.StartNew();
        while (!bitti() && saat.Elapsed < TimeSpan.FromSeconds(saniye)) Dispatcher.UIThread.RunJobs();
        Assert.True(bitti(), "pencere işi süresinde bitmedi");
    }
}
