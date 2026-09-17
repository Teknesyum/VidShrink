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
/// Paket 2b borcu: <see cref="RecorderSession"/>'ın kendiliğinden bölme yoklaması, süre sınırının parçalara
/// kalanla dağıtılması ve <c>_partial</c> yolu gerçek gdigrab kaydında. 5 sn sınır ve 2 sn bölme en az iki
/// numaralı parça verir, parçaların toplamı 5 sn'yi aşmaz; bölmesiz aynı kayıt tek parça (negatif kontrol).
/// Ölçülen: 2 sn bölmede parçalar 3,2 + 1,8 sn — yoklama, ffmpeg'in ilerleme aralığı ve nazik kapanış birinci
/// parçayı ~1,2 sn uzatıyor, kalan süre ikinciden düşüyor; üst sınır bu yüzden 3,5 sn.
/// Kapanma süresi 1 ms verilen kayıt öldürülür ve yarım işaretlenir. Kanıt <c>.calisma/paket-2b/bolme/</c>.
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
        foreach (var eski in Directory.GetFiles(Kanit)) File.Delete(eski);

        var bolunmus = await Kaydet(Istek() with { Split = new RecorderSplit(TimeSpan.FromSeconds(2)) }, Path.Combine(Kanit, "bolunmus.mkv"));
        var parcaSureleri = (bolunmus.Files ?? Array.Empty<string>()).Select(Sure).ToList();
        var tek = await Kaydet(Istek(), Path.Combine(Kanit, "tek.mkv"));

        var yarimOturum = await RecorderSession.StartAsync(Istek() with { MaxDuration = null }, Path.Combine(Kanit, "yarim.mkv"));
        await Task.Delay(1500);
        var yarim = await yarimOturum.StopAsync(1);

        File.WriteAllLines(Path.Combine(Kanit, "olcu.txt"), new[]
        {
            $"bolunmus ok={bolunmus.Ok} partial={bolunmus.Partial} parca={bolunmus.Segments} sureler={string.Join(' ', parcaSureleri.Select(s => s.ToString("0.###", CultureInfo.InvariantCulture)))} toplam={parcaSureleri.Sum().ToString("0.###", CultureInfo.InvariantCulture)}",
            $"tek ok={tek.Ok} parca={tek.Segments} sure={(File.Exists(tek.OutputPath) ? Sure(tek.OutputPath) : double.NaN).ToString("0.###", CultureInfo.InvariantCulture)}",
            $"yarim ok={yarim.Ok} partial={yarim.Partial} cikis={yarim.ExitCode} dosya={File.Exists(yarim.OutputPath)}"
        });

        Assert.True(bolunmus.Ok, bolunmus.StandardError);
        Assert.False(bolunmus.Partial);
        Assert.InRange(bolunmus.Segments, 2, 4);
        Assert.Equal(bolunmus.Segments, parcaSureleri.Count);
        Assert.All(parcaSureleri, s => Assert.InRange(s, 0.2, 3.5));
        Assert.InRange(parcaSureleri.Sum(), 4.0, 5.6);

        Assert.True(tek.Ok, tek.StandardError);
        Assert.Equal(1, tek.Segments);
        Assert.InRange(Sure(tek.OutputPath), 4.0, 5.6);

        Assert.True(yarim.Partial);
        Assert.False(yarim.Ok);
    }
}
