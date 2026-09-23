using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Paket 2b borcu: <see cref="RecorderSession"/>'ın kendiliğinden bölmesi, süre sınırının parçalara
/// kalanla dağıtılması ve <c>_partial</c> yolu gerçek gdigrab kaydında. 5 sn sınır ve 2 sn bölme en az iki
/// numaralı parça verir, parçaların toplamı 5 sn'yi aşmaz; bölmesiz aynı kayıt tek parça (negatif kontrol).
/// <para>
/// 23 Eylül 2026'da bölme dışarıdan (250 ms'de bir yoklayan <c>WatchSplitAsync</c> + nazik <c>q</c> kapanışı)
/// uygulanıyordu ve paylaşılan CI koşucusunda 2 sn'lik bölme 4,4 sn'lik parça üretti — sabit bir tavan (önce
/// 3,5, sonra 4,0 sn) her yük artışında yeniden kırmızı veriyordu (<c>docs/olcumler/kayit-bolme-parca-siniri.md</c>).
/// Kök neden gerçekti: nazik kapanışın gecikmesi CI yükü arttıkça uzuyor ve parça süresine giriyordu. Düzeltme
/// tavanı gevşetmek değil, aşımı üründen kaldırmaktı: <see cref="RecorderArguments.ForSegment"/> artık bölme
/// ölçütünü parçanın kendi <c>-t</c>/<c>-fs</c> sınırına katıyor (<c>min(bölme ölçütü, kalan toplam)</c>), ffmpeg
/// parçayı içerik zamanında kendisi kapatıyor; dış yoklama ve nazik kapanış artık parça süresine hiç girmiyor
/// (<see cref="RecorderSession.WatchExitAsync"/> dogal cikisi "parça doldu, sonrakini aç" diye okuyor). Bu
/// yüzden alt sınır artık gerçek ve sıkı: 2 sn'lik bölmede ölçülen parçalar 2,0-2,03 sn arası (üç yerel koşum,
/// <c>docs/olcumler/kayit-bolme-parca-siniri.md</c>), tavan 2,5 sn — 4,4 sn'ye değil 2 katına (yaklaşımın
/// asıl kusuruna) karşı pimli.
/// </para>
/// Argüman düzeyindeki davranış (bölme ölçütünün <c>-t</c>'ye katılması, son kısa parça, bölmesiz kayıtta hiç
/// yazılmaması) <see cref="KayitFfmpegKoluTests"/>'te <c>ForSegment</c> üzerinden belirlemeci ve mutasyonla
/// pimli; burada yalnız gerçek ffmpeg sürecinin bunu uyguladığı ölçülüyor.
/// Kapanma süresi 1 ms verilen kayıt öldürülür ve yarım işaretlenir; öldürülen Matroska ffprobe'la okunur paket verir,
/// öldürülen mp4 vermez — <see cref="RecorderArguments.SurvivesKill"/> tablosu davranışla ölçülür. Ölçülen:
/// <c>-flush_packets 1</c> olmadan 7 sn'lik Matroska öldürülünce 0 bayt kalıyordu. Kanıt <c>.calisma/paket-2b/bolme/</c>.
/// </summary>
public sealed class KayitBolmeTests
{
    private static string Kanit
    {
        get
        {
            var yol = Path.Combine(GirdiKanit.Root, ".calisma", "paket-2b", "bolme");
            Directory.CreateDirectory(yol);
            return yol;
        }
    }

    /// <summary>Son asertten sonra çağrılır; kuralı <see cref="KanitKapanisi"/> anlatıyor.</summary>
    private static void Kapat(params string[] adlar) => KanitKapanisi.Kapat(Kanit, adlar);

    private static void Onceki(params string[] adlar) => KanitKapanisi.Onceki(Kanit, adlar);

    private static RecorderRequest Istek() => new()
    {
        Platform = RecorderPlatform.Windows,
        Target = RecorderTargetKind.Region,
        Region = new RecorderRegion(0, 0, 320, 240),
        Fps = 15,
        Container = RecorderContainer.Mkv,
        Preset = "ultrafast",
        MaxDuration = TimeSpan.FromSeconds(5)
    };

    private static double Sure(string dosya)
        => double.Parse(KaydediciOnizlemeTests.Probe(dosya, "format=duration"), CultureInfo.InvariantCulture);

    private static int Oynar(string dosya)
        => File.Exists(dosya)
            ? KaydediciOnizlemeTests.Probe(dosya, "packet=flags").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Count(s => s.StartsWith('K') || s.StartsWith('_'))
            : 0;

    private static async Task<RecordResult> Oldur(RecorderContainer kap, string cikti)
    {
        var oturum = await RecorderSession.StartAsync(Istek() with { MaxDuration = null, Container = kap, Region = new RecorderRegion(0, 0, 640, 480) }, cikti);
        await Task.Delay(7000);
        return await oturum.StopAsync(1);
    }

    private static async Task<RecordResult> Kaydet(RecorderRequest istek, string cikti)
    {
        var oturum = await RecorderSession.StartAsync(istek, cikti);
        var bitti = await Task.WhenAny(oturum.Ended, Task.Delay(20000));
        Assert.True(bitti == oturum.Ended, "kayit 20 sn icinde kendiliginden bitmedi");
        return await oturum.StopAsync();
    }

    [KayitFact]
    public async Task BolmeSureSiniriniParcalaraDagitirYarimKayitIsaretlenir()
    {
        Onceki("olcu.txt", "tek.mkv", "yarim.mkv", "yarim.mp4");
        foreach (var eski in Directory.GetFiles(Kanit, "bolunmus*")) File.Delete(eski);

        var bolunmus = await Kaydet(Istek() with { Split = new RecorderSplit(TimeSpan.FromSeconds(2)) }, Path.Combine(Kanit, "bolunmus.mkv"));
        var parcaSureleri = (bolunmus.Files ?? Array.Empty<string>()).Select(Sure).ToList();
        var tek = await Kaydet(Istek(), Path.Combine(Kanit, "tek.mkv"));

        var yarim = await Oldur(RecorderContainer.Mkv, Path.Combine(Kanit, "yarim.mkv"));
        var yarimMp4 = await Oldur(RecorderContainer.Mp4, Path.Combine(Kanit, "yarim.mp4"));

        File.WriteAllLines(Path.Combine(Kanit, "olcu.txt"), new[]
        {
            $"bolunmus ok={bolunmus.Ok} partial={bolunmus.Partial} parca={bolunmus.Segments} sureler={string.Join(' ', parcaSureleri.Select(s => s.ToString("0.###", CultureInfo.InvariantCulture)))} toplam={parcaSureleri.Sum().ToString("0.###", CultureInfo.InvariantCulture)}",
            $"tek ok={tek.Ok} parca={tek.Segments} sure={(File.Exists(tek.OutputPath) ? Sure(tek.OutputPath) : double.NaN).ToString("0.###", CultureInfo.InvariantCulture)}",
            $"yarim ok={yarim.Ok} partial={yarim.Partial} cikis={yarim.ExitCode} dosya={File.Exists(yarim.OutputPath)} paket={Oynar(yarim.OutputPath)}",
            $"yarim mp4 ok={yarimMp4.Ok} partial={yarimMp4.Partial} cikis={yarimMp4.ExitCode} dosya={File.Exists(yarimMp4.OutputPath)} paket={Oynar(yarimMp4.OutputPath)}"
        });

        Assert.True(bolunmus.Ok, bolunmus.StandardError);
        Assert.False(bolunmus.Partial);
        Assert.InRange(bolunmus.Segments, 2, 4);
        Assert.Equal(bolunmus.Segments, parcaSureleri.Count);
        var toplam = parcaSureleri.Sum();
        Assert.All(parcaSureleri, s => Assert.InRange(s, 0.2, 2.5));
        Assert.InRange(toplam, 4.0, 5.6);

        Assert.True(tek.Ok, tek.StandardError);
        Assert.Equal(1, tek.Segments);
        Assert.InRange(Sure(tek.OutputPath), 4.0, 5.6);

        Assert.True(yarim.Partial);
        Assert.False(yarim.Ok);
        Assert.True(Oynar(yarim.OutputPath) > 15, "oldurulen mkv oynamiyor");
        Assert.True(yarimMp4.Partial);
        Assert.Equal(0, Oynar(yarimMp4.OutputPath));
        Assert.Equal(Oynar(yarim.OutputPath) > 0, RecorderArguments.SurvivesKill(RecorderContainer.Mkv));
        Assert.Equal(Oynar(yarimMp4.OutputPath) > 0, RecorderArguments.SurvivesKill(RecorderContainer.Mp4));

        Kapat((bolunmus.Files ?? Array.Empty<string>()).Select(Path.GetFileName)
            .Concat(new[] { "olcu.txt", "bolunmus.mkv", "tek.mkv", "yarim.mkv", "yarim.mp4" }).ToArray()!);
    }

}
