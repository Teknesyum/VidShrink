using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// Kesitin <b>kosucudaki</b> iki cagri yeri. Cekirdek kurallari (
/// <see cref="OvershootTrim.Offered(EncodePlan, double, double)"/> ve
/// <see cref="EncodePlan.EffectiveDurationSeconds"/>) <c>KucultmeAraligiTests</c>'te
/// pimli, ama <c>EncodeRunner</c>'in onlari <b>cagirdigi</b> pimsizdi: iki cagriyi eski
/// haline dondurmek butun kolu yesil birakiyordu. Burada olculen sey davranis, kod degil:
/// gercek bir kodlama kosuluyor, kosucunun uretttigi tasma istemi ve duzeltilmis plan
/// okunuyor.
/// </summary>
public sealed class KucultmeAraligiKosucuTests
{
    private readonly ITestOutputHelper _cikti;

    public KucultmeAraligiKosucuTests(ITestOutputHelper cikti) => _cikti = cikti;

    private const double KaynakSaniye = 8.0;
    private static readonly TrimWindow Kesit = new(2.0, 6.0);

    /// <summary>
    /// S5'in kosucudaki karsiligi: kullanicinin kendi sectigi kesit varken tasma kirpmasi
    /// <b>istemde bile</b> gorunmez. Negatif kol ayni kosumu kesitsiz tekrarlar; orada
    /// kirpma onerisi doluyor, boylece olcunun dikisi canli oldugu kanitlanir.
    /// </summary>
    [FfmpegFact]
    public async Task KullaniciKesitiVarkenIstemeKirpmaOnerisiGirmez()
    {
        await KlipleAsync(async (info, klasor) =>
        {
            var kesitli = await IstemAsync(info, klasor, Kesit);
            var kesitsiz = await IstemAsync(info, klasor, null);

            _cikti.WriteLine($"kesitli\ttasma {kesitli.OverPercent:0.###}%\tkirpma {kesitli.Trims?.Count ?? -1}");
            _cikti.WriteLine($"kesitsiz\ttasma {kesitsiz.OverPercent:0.###}%\tkirpma {kesitsiz.Trims?.Count ?? -1}");

            Assert.InRange(kesitsiz.OverPercent, 0.0, OvershootTrim.ThresholdPercent);
            Assert.NotEmpty(kesitsiz.Trims!);

            Assert.InRange(kesitli.OverPercent, 0.0, OvershootTrim.ThresholdPercent);
            Assert.Empty(kesitli.Trims ?? Array.Empty<TrimPlan>());
        });
    }

    /// <summary>
    /// S2'nin kosucudaki karsiligi: tasmadan sonraki duzeltme kaynak suresini degil
    /// <b>kesit</b> suresini kullanir. Duzeltmenin ses payi sureyle doğru orantili
    /// (<c>PlanCalculator.Correct</c>: <c>SizeMb(0, NonVideoK, durationSeconds)</c>), bu
    /// yuzden gerekce notundaki <c>AudioMb</c> iki sureyi ayirt eder: kesitle 4 sn, kaynak
    /// suresiyle 8 sn. Ikisi arasinda iki kat fark var.
    /// </summary>
    [FfmpegFact]
    public async Task TasmaDuzeltmesiKesitSuresindenHesaplanir()
    {
        await KlipleAsync(async (info, klasor) =>
        {
            var plan = Plan(Kesit);
            var dogal = await DogalMbAsync(info, plan, klasor, "duzeltme-yoklama.mp4");
            var hedefMb = dogal / 1.4;

            var sonuc = await new EncodeRunner().RunAsync(
                info, Plan(Kesit), Path.Combine(klasor, "duzeltme.mp4"), hedefMb,
                progress: null, ct: CancellationToken.None, fillPolicy: FillPolicy.QualityCeiling);

            foreach (var adim in sonuc.Trace ?? Array.Empty<EncodeAttempt>())
                _cikti.WriteLine($"iz\t{adim.Number}\t{adim.Branch}\t{adim.ActualMb:0.###} MB\t{adim.VideoBitrateK}k\t{adim.Mode}");

            var not = (sonuc.PlanUsed.ReasonCodes ?? new List<ReasonNote>())
                .FirstOrDefault(n => n.Code == ReasonCode.RetryScaled);
            Assert.NotNull(not);

            var kesitSuresi = Kesit.DurationSeconds;
            var kesitPayi = SesPayiMb(plan, kesitSuresi);
            var kaynakPayi = SesPayiMb(plan, KaynakSaniye);

            _cikti.WriteLine($"ses payi\tnot {not!.AudioMb:0.#####}\tkesit {kesitPayi:0.#####}\tkaynak {kaynakPayi:0.#####}");

            Assert.Equal(kesitPayi, not.AudioMb, 5);
            Assert.NotEqual(kaynakPayi, not.AudioMb, 5);
            Assert.Equal(kesitSuresi, plan.EffectiveDurationSeconds(KaynakSaniye), 5);
        });
    }

    /// <summary>
    /// Ayni hesap kullanicinin "yeniden dene" dedigi kolda da kesit suresinden gitmeli.
    /// Yukaridaki olcu <c>askBeforeRetry</c> verilmeyen kolu yuruyor; bu kol istemi acip
    /// <see cref="OvershootChoice.Retry"/> donuyor, boylece ayri bir <c>Correct</c> cagrisi
    /// kosuyor. Eskiden o cagri kaynak suresini okuyordu ve hicbir olcu gormuyordu.
    /// </summary>
    [FfmpegFact]
    public async Task YenidenDeneKolununDuzeltmesiDeKesitSuresinden()
    {
        await KlipleAsync(async (info, klasor) =>
        {
            var plan = Plan(Kesit);
            var dogal = await DogalMbAsync(info, plan, klasor, "yeniden-yoklama.mp4");

            var sonuc = await new EncodeRunner().RunAsync(
                info, Plan(Kesit), Path.Combine(klasor, "yeniden.mp4"), dogal / 1.4,
                progress: null, ct: CancellationToken.None, fillPolicy: FillPolicy.QualityCeiling,
                profile: null,
                askBeforeRetry: (_, _) => Task.FromResult(OvershootChoice.Retry));

            var not = (sonuc.PlanUsed.ReasonCodes ?? new List<ReasonNote>())
                .FirstOrDefault(n => n.Code == ReasonCode.RetryScaled);
            Assert.NotNull(not);

            var kesitPayi = SesPayiMb(plan, Kesit.DurationSeconds);
            var kaynakPayi = SesPayiMb(plan, KaynakSaniye);
            _cikti.WriteLine($"yeniden dene\tnot {not!.AudioMb:0.#####}\tkesit {kesitPayi:0.#####}\tkaynak {kaynakPayi:0.#####}");

            Assert.Equal(kesitPayi, not.AudioMb, 5);
            Assert.NotEqual(kaynakPayi, not.AudioMb, 5);
        });
    }

    /// <summary>
    /// Ilerleme paydasi da kesit suresi. Kesitli kodlamada ffmpeg'in saati sifirdan kesit
    /// suresine kadar sayar; payda kaynak suresi olursa cubuk kesitin oranina takilip kalir
    /// (burada yarisinda). Olcu bitmis kosumun en yuksek oranini okuyor, kesitsiz kol negatif
    /// kontrol degil kiyas: ikisi de sona ulasmali.
    /// </summary>
    [FfmpegFact]
    public async Task KesitliKosumdaIlerlemeSonaUlasir()
    {
        await KlipleAsync(async (info, klasor) =>
        {
            var enYuksek = 0.0;
            var izci = new Progress<EncodeProgress>(p => enYuksek = Math.Max(enYuksek, p.Fraction));

            var sonuc = await new EncodeRunner().RunAsync(
                info, Plan(Kesit), Path.Combine(klasor, "ilerleme.mp4"), targetMb: 1000,
                progress: izci, ct: CancellationToken.None, fillPolicy: FillPolicy.QualityCeiling);

            await Task.Delay(200);
            _cikti.WriteLine($"kesit {Kesit.DurationSeconds} sn / kaynak {KaynakSaniye} sn\ten yuksek oran {enYuksek:0.###}");

            Assert.True(sonuc.Success);
            Assert.True(enYuksek > 0.9, $"ilerleme {enYuksek:0.###} oraninda kaldi");
        });
    }

    /// <summary>
    /// <c>Correct</c>'in ayirdigi ses payi. Bicim sabitleri testte tekrarlanmaz: video
    /// hizi sifirlanmis bir planin <see cref="PlanCalculator.EstimatedMb"/> degeri tam
    /// olarak ayni hesaptir.
    /// </summary>
    private static double SesPayiMb(EncodePlan plan, double saniye)
    {
        var yalnizSes = plan.Clone();
        yalnizSes.Mode = "2pass";
        yalnizSes.Crf = null;
        yalnizSes.VideoBitrateK = 0;
        return PlanCalculator.EstimatedMb(yalnizSes, saniye)!.Value;
    }

    private static async Task<RetryPrompt> IstemAsync(MediaInfo info, string klasor, TrimWindow? kesit)
    {
        var ad = kesit is null ? "kesitsiz" : "kesitli";
        var dogal = await DogalMbAsync(info, Plan(kesit), klasor, $"{ad}-yoklama.mp4");

        RetryPrompt? gorulen = null;
        await new EncodeRunner().RunAsync(
            info, Plan(kesit), Path.Combine(klasor, $"{ad}.mp4"), dogal / 1.02,
            progress: null, ct: CancellationToken.None, fillPolicy: FillPolicy.QualityCeiling,
            profile: null,
            askBeforeRetry: (istem, _) =>
            {
                gorulen ??= istem;
                return Task.FromResult(OvershootChoice.AcceptLarger);
            });

        Assert.NotNull(gorulen);
        return gorulen!;
    }

    private static async Task<double> DogalMbAsync(MediaInfo info, EncodePlan plan, string klasor, string ad)
    {
        var yol = Path.Combine(klasor, ad);
        var sonuc = await new EncodeRunner().RunAsync(
            info, plan, yol, targetMb: 1000, progress: null,
            ct: CancellationToken.None, fillPolicy: FillPolicy.QualityCeiling);
        Assert.True(sonuc.Success);
        Assert.Equal(1, sonuc.Attempts);
        File.Delete(yol);
        return sonuc.OutputMb;
    }

    private static EncodePlan Plan(TrimWindow? kesit) => new()
    {
        Codec = "libx264",
        Mode = "crf",
        Crf = 26,
        VideoBitrateK = 600,
        AudioCodec = "aac",
        AudioBitrateK = 128,
        Width = 320,
        Height = 240,
        Fps = 10,
        Preset = "ultrafast",
        Trim = kesit,
        ReasonCodes = new List<ReasonNote>()
    };

    private static async Task KlipleAsync(Func<MediaInfo, string, Task> govde)
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "kesit-kosucu", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        try
        {
            var kaynak = Path.Combine(klasor, "kaynak.mp4");
            var cikti = await EncodeRunner.RunCommandAsync(
                new[]
                {
                    "-y", "-f", "lavfi", "-i", $"testsrc2=size=320x240:rate=10:duration={KaynakSaniye.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}",
                    "-f", "lavfi", "-i", $"sine=frequency=440:duration={KaynakSaniye.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}",
                    "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p",
                    "-c:a", "aac", "-b:a", "128k", "-shortest", kaynak
                },
                durationSeconds: KaynakSaniye, progress: null, stage: "hazirlik", spanFrom: 0.0, spanTo: 1.0,
                ct: CancellationToken.None);
            Assert.Equal(0, cikti.ExitCode);

            var info = new MediaInfo
            {
                FilePath = kaynak,
                FileSizeBytes = new FileInfo(kaynak).Length,
                DurationSeconds = KaynakSaniye,
                Width = 320,
                Height = 240,
                Fps = 10,
                VideoCodec = "h264",
                AudioCodec = "aac",
                TotalBitrateBps = 800_000
            };

            await govde(info, klasor);
        }
        finally { try { Directory.Delete(klasor, true); } catch { } }
    }
}
