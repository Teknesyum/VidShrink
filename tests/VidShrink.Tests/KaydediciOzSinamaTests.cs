using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VidShrink.App.Recorder;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Kaydedici oz sinamasi (<c>--recorder-selftest</c>): renk kartinin yerlesimi, orneklemesi,
/// Delta E hesabi, esigi ve rapor. CI'da HDR ekran yok; yakalama sahte ffmpeg ile, kart karesi
/// testte cizilerek olculur. Gercek ekranda kosum kullanicinin makinesinde yapilir.
/// </summary>
public sealed class KaydediciOzSinamaTests
{
    /// <summary>Kartin <paramref name="en"/> x <paramref name="boy"/> <c>rgb24</c> karesi, pencerenin cizdigi gibi.</summary>
    internal static byte[] Kart(int en, int boy, Func<int, (byte R, byte G, byte B)>? renk = null)
    {
        var kare = new byte[en * boy * 3];
        for (var i = 0; i < RecorderColourCard.Patches.Count; i++)
        {
            var p = RecorderColourCard.Patches[i];
            var (r, g, b) = renk?.Invoke(i) ?? (p.R, p.G, p.B);
            var (x, y, w, h) = RecorderColourCard.Cell(i, en, boy);
            for (var yy = y; yy < y + h; yy++)
            for (var xx = x; xx < x + w; xx++)
            {
                var o = (yy * en + xx) * 3;
                kare[o] = r;
                kare[o + 1] = g;
                kare[o + 2] = b;
            }
        }
        return kare;
    }

    [Fact]
    public void HucrelerKartiBosluksuzVeCakismasizKaplar()
    {
        var kaplanan = new int[641 * 479];
        for (var i = 0; i < RecorderColourCard.Patches.Count; i++)
        {
            var (x, y, w, h) = RecorderColourCard.Cell(i, 641, 479);
            for (var yy = y; yy < y + h; yy++)
            for (var xx = x; xx < x + w; xx++)
                kaplanan[yy * 641 + xx]++;
        }

        Assert.Equal(RecorderColourCard.Columns * RecorderColourCard.Rows, RecorderColourCard.Patches.Count);
        Assert.All(kaplanan, n => Assert.Equal(1, n));
    }

    [Fact]
    public void OrneklemeCizilenKartiAynenOkur()
    {
        var sonuc = RecorderColourCard.Compare(RecorderColourCard.Sample(Kart(320, 240), 320, 240));

        Assert.All(sonuc, r => Assert.Equal(0, r.DeltaE));
        Assert.All(sonuc, r => Assert.True(RecorderColourCard.Passes(r)));
        Assert.Equal(new byte[] { 255, 255, 255 }, new[] { sonuc[0].R, sonuc[0].G, sonuc[0].B });
    }

    /// <summary>Kenar orneklenmez: yakalama bolgesi hucre boyunun dortte birinden az kayarsa sonuc degismez.</summary>
    [Fact]
    public void KenarKaymasiOrneklemeyeKarismaz()
    {
        const int en = 320, boy = 240;
        var kare = Kart(en, boy);
        var kaymis = new byte[kare.Length];
        for (var y = 0; y < boy; y++)
        for (var x = 0; x < en; x++)
        {
            var kaynak = (Math.Min(boy - 1, y + 10) * en + Math.Min(en - 1, x + 10)) * 3;
            Array.Copy(kare, kaynak, kaymis, (y * en + x) * 3, 3);
        }

        Assert.All(RecorderColourCard.Compare(RecorderColourCard.Sample(kaymis, en, boy)), r => Assert.Equal(0, r.DeltaE));
    }

    [Fact]
    public void DeltaEBilinenDegerleriVerir()
    {
        Assert.Equal(100.0, RecorderColourCard.DeltaE76((255, 255, 255), (0, 0, 0)), 2);
        Assert.Equal(0.0, RecorderColourCard.DeltaE76((194, 150, 130), (194, 150, 130)));
        Assert.Equal(RecorderColourCard.DeltaE76((255, 0, 0), (255, 10, 10)), RecorderColourCard.DeltaE76((255, 10, 10), (255, 0, 0)), 9);
        Assert.InRange(RecorderColourCard.DeltaE76((128, 128, 128), (129, 128, 128)), 0.3, 1.0);
        Assert.InRange(RecorderColourCard.DeltaE76((255, 0, 0), (255, 0, 40)), 3.0, 30.0);
    }

    [Theory]
    [InlineData(2, 1, true)]
    [InlineData(2, -1, true)]
    [InlineData(2, 2, false)]
    [InlineData(0, 1, true)]
    [InlineData(0, -2, false)]
    public void GriYamaKanalBasinaBirKodTolerir(int yama, int kayma, bool gecer)
    {
        var p = RecorderColourCard.Patches[yama];
        var r = new PatchResult(p, (byte)Math.Clamp(p.R + kayma, 0, 255), p.G, p.B, 0);

        Assert.True(RecorderColourCard.IsGrey(p));
        Assert.Equal(gecer, RecorderColourCard.Passes(r));
    }

    [Fact]
    public void RenkliYamaDeltaEEsigiyleOlculur()
    {
        var kirmizi = RecorderColourCard.Patches.Single(p => p.Name == "red");

        Assert.False(RecorderColourCard.IsGrey(kirmizi));
        Assert.True(RecorderColourCard.Passes(new PatchResult(kirmizi, 255, 3, 0, RecorderColourCard.ColourTolerance)));
        Assert.False(RecorderColourCard.Passes(new PatchResult(kirmizi, 255, 3, 0, RecorderColourCard.ColourTolerance + 0.01)));
    }

    [Fact]
    public void BayrakTaninir()
    {
        Assert.True(RecorderSelfTest.Requested(new[] { "--recorder-selftest" }));
        Assert.True(RecorderSelfTest.Requested(new[] { "x", " --RECORDER-SELFTEST " }));
        Assert.False(RecorderSelfTest.Requested(new[] { "--recorder" }));
        Assert.False(RecorderSelfTest.Requested(Array.Empty<string>()));
    }

    [Fact]
    public void KareYakalamaKaydinGirdisiniHamRgbyeYazar()
    {
        var istek = new RecorderRequest
        {
            Platform = RecorderPlatform.Windows,
            Target = RecorderTargetKind.Region,
            Region = new RecorderRegion(100, 50, 640, 480),
            DdaOutputs = new[] { new DdaOutput(0, 0, 0, 2560, 1440, Hdr: true, SdrWhiteNits: 240) },
            Capture = RecorderCapture.Ddagrab,
            Fps = 10,
            ShowCursor = false,
            HdrToSdr = true
        };

        var args = RecorderArguments.BuildFrameGrab(istek, "k.raw");

        var kaynak = args[args.ToList().IndexOf("lavfi") + 2];
        Assert.StartsWith("ddagrab=output_idx=0:framerate=10:draw_mouse=0:offset_x=100:offset_y=50:video_size=640x480:output_fmt=10bit,", kaynak);
        Assert.EndsWith(RecorderArguments.HdrSdrChain(240, RecorderArguments.DefaultPeakNits), kaynak);
        Assert.Equal(new[] { "-frames:v", "1", "-f", "rawvideo", "-pix_fmt", "rgb24", "k.raw" }, args.TakeLast(7));
        Assert.DoesNotContain("-c:v", args);
        Assert.DoesNotContain("-vf", args);
    }

    private static readonly RecorderRegion KartBolgesi = new(960, 480, 640, 480);

    private static Func<IReadOnlyList<string>, CancellationToken, Task<FfmpegRun>> Sahte(
        List<string> kaynaklar, Func<string, byte[]?> kare)
        => (args, _) =>
        {
            var kaynak = args[args.ToList().IndexOf("lavfi") + 2];
            kaynaklar.Add(kaynak);
            var icerik = kare(kaynak);
            if (icerik is null) return Task.FromResult(new FfmpegRun(false, 1, "ddagrab: access lost", TimeSpan.Zero));
            File.WriteAllBytes(args[^1], icerik);
            return Task.FromResult(new FfmpegRun(true, 0, string.Empty, TimeSpan.Zero));
        };

    [Fact]
    public async Task HdrEkrandaIkiYolOlculurVeGecer()
    {
        var kaynaklar = new List<string>();
        var cikislar = new[] { new DdaOutput(0, 0, 0, 2560, 1440, Hdr: true, SdrWhiteNits: 240) };

        var (metin, kod) = await RecorderSelfTest.MeasureAsync(KartBolgesi, cikislar, Sahte(kaynaklar, _ => Kart(640, 480)));

        Assert.Equal(RecorderSelfTest.PassCode, kod);
        Assert.Equal(2, kaynaklar.Count);
        Assert.Contains("npl=240", kaynaklar[0]);
        Assert.EndsWith(",hwdownload,format=bgra", kaynaklar[1]);
        Assert.Contains("selftest-path: hdr-to-sdr max-de=0.00 mean-de=0.00 pass", metin);
        Assert.Contains("selftest-path: bgra-8bit max-de=0.00 mean-de=0.00 pass", metin);
        Assert.Contains("sdr-white: 240 nits (Windows)", metin);
        Assert.Contains("selftest-result: pass", metin);
    }

    [Fact]
    public async Task SdrEkrandaYalnizSekizBitYolKosar()
    {
        var kaynaklar = new List<string>();

        var (metin, kod) = await RecorderSelfTest.MeasureAsync(KartBolgesi, new[] { new DdaOutput(0, 0, 0, 2560, 1440) },
            Sahte(kaynaklar, _ => Kart(640, 480)));

        Assert.Equal(RecorderSelfTest.PassCode, kod);
        Assert.Single(kaynaklar);
        Assert.DoesNotContain("hdr-to-sdr max-de", metin);
    }

    [Fact]
    public async Task KaymisRenkEsigiAsarVeYamasiSoylenir()
    {
        var kaynaklar = new List<string>();
        var cikislar = new[] { new DdaOutput(0, 0, 0, 2560, 1440, Hdr: true) };
        var kirmizi = RecorderColourCard.Patches.ToList().FindIndex(p => p.Name == "red");

        var (metin, kod) = await RecorderSelfTest.MeasureAsync(KartBolgesi, cikislar, Sahte(kaynaklar, k => k.Contains("npl=", StringComparison.Ordinal)
                ? Kart(640, 480, i => i == kirmizi ? ((byte)255, (byte)40, (byte)30) : (RecorderColourCard.Patches[i].R, RecorderColourCard.Patches[i].G, RecorderColourCard.Patches[i].B))
                : Kart(640, 480)));

        Assert.Equal(RecorderSelfTest.FailCode, kod);
        Assert.Contains("selftest-path: hdr-to-sdr", metin);
        Assert.Contains("red      drawn=255,0,0 captured=255,40,30", metin);
        Assert.Contains(" off", metin);
        Assert.Contains("selftest-path: bgra-8bit max-de=0.00 mean-de=0.00 pass", metin);
        Assert.Contains("sdr-white: 203 nits (assumed, Windows value unreadable)", metin);
        Assert.Contains("selftest-result: fail", metin);
    }

    [Fact]
    public async Task FfmpegDuserseVeKartCikistaDegilseHataKodu()
    {
        var kaynaklar = new List<string>();
        var dusen = await RecorderSelfTest.MeasureAsync(KartBolgesi, new[] { new DdaOutput(0, 0, 0, 2560, 1440) },
            Sahte(kaynaklar, _ => null));
        var disarida = await RecorderSelfTest.MeasureAsync(new RecorderRegion(2400, 0, 640, 480), new[] { new DdaOutput(0, 0, 0, 2560, 1440) },
            Sahte(kaynaklar, _ => Kart(640, 480)));

        Assert.Equal(RecorderSelfTest.ErrorCode, dusen.Code);
        Assert.Contains("selftest-error: bgra-8bit ffmpeg failed: ddagrab: access lost", dusen.Text);
        Assert.Contains("selftest-result: error", dusen.Text);
        Assert.Equal(RecorderSelfTest.ErrorCode, disarida.Code);
        Assert.Contains("selftest-error: card is not inside one DXGI output", disarida.Text);
        Assert.Single(kaynaklar);
    }
}
