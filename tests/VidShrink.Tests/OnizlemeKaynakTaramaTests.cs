using System.Text;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// Onizleme parcasinin kaynak uzayi taramasi: her kaynak gercekten yoklanir, plan gercek
/// <see cref="PlanCalculator"/> ile kurulur ve uretilen arguman gercek ffmpeg'e verilir.
/// Dizgi pimleri bu kusuru goremezdi — 19 Eylul 2026'da kullanicinin makinesinde
/// "Onizleme Ornegi Kodlanamadi" ciktı ve yedi cagri yerinin hicbiri argumani
/// kosturmadigi icin suit yesil kalmisti.
///
/// Kaynaklar kucuk secildi ki hepsi hedefin <b>ustunde</b> kalmasin: hedefin altinda kalan
/// kaynak plani passthrough'a dusurur ve kusur tam orada yasiyordu.
/// </summary>
public sealed class OnizlemeKaynakTaramaTests : IDisposable
{
    private readonly ITestOutputHelper _cikti;
    private readonly string _dizin;

    public OnizlemeKaynakTaramaTests(ITestOutputHelper cikti)
    {
        _cikti = cikti;
        _dizin = Path.Combine(TipSources.Root, ".calisma", "onizleme-kaynak", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_dizin);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dizin, recursive: true); } catch (IOException) { }
    }

    private static readonly (string Ad, string[] Ek)[] Kaynaklar =
    {
        ("h264", new[] { "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "30" }),
        ("hevc", new[] { "-c:v", "libx265", "-pix_fmt", "yuv420p", "-crf", "30", "-tag:v", "hvc1" }),
        ("hevc10", new[] { "-c:v", "libx265", "-pix_fmt", "yuv420p10le", "-crf", "30", "-tag:v", "hvc1" }),
        ("yuv444", new[] { "-c:v", "libx264", "-pix_fmt", "yuv444p", "-crf", "30" }),
        ("dondurulmus", new[] { "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "30", "-metadata:s:v", "rotate=90" }),
    };

    [FfmpegTheory]
    [InlineData(2)]
    [InlineData(500)]
    public void HerKaynakTuruOnizlemeParcasiUretiyor(double hedefMb)
    {
        var dusuk = new StringBuilder();

        foreach (var (ad, ek) in Kaynaklar)
        {
            var kaynak = Path.Combine(_dizin, ad + ".mp4");
            if (!Uret(kaynak, ek)) { dusuk.AppendLine($"{ad}: olcum klibi uretilemedi"); continue; }

            var info = FfprobeClient.ProbeAsync(kaynak).GetAwaiter().GetResult();
            var plan = PlanCalculator.Build(info, new PlanOptions { TargetMb = hedefMb });
            var cikti = Path.Combine(_dizin, $"{ad}-{hedefMb}-parca.mp4");

            var parca = PreviewSegment.For(info, plan, 2.0, cikti, durationSeconds: 1.0);
            var kod = SegmentClips.Ffmpeg(parca.Arguments);
            var boyut = File.Exists(cikti) ? new FileInfo(cikti).Length : 0;

            _cikti.WriteLine($"{ad} (hedef {hedefMb} MB): kip={plan.Mode} kod={kod} boyut={boyut}");
            _cikti.WriteLine("  " + FfmpegArguments.ToCommandLine(parca.Arguments));

            if (kod != 0 || boyut <= 0)
                dusuk.AppendLine($"{ad}: kip={plan.Mode} kod={kod} boyut={boyut} :: {FfmpegArguments.ToCommandLine(parca.Arguments)}");
        }

        Assert.True(dusuk.Length == 0, dusuk.ToString());
    }

    private static bool Uret(string yol, string[] ek)
    {
        var args = new List<string>
        {
            "-y", "-hide_banner", "-loglevel", "error",
            "-f", "lavfi", "-i", "testsrc2=size=640x360:rate=30:duration=6",
        };
        args.AddRange(ek);
        args.Add(yol);
        return SegmentClips.Ffmpeg(args) == 0 && File.Exists(yol);
    }
}
