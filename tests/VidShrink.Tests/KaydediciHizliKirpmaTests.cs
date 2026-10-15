using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using VidShrink.App.Recorder;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Paket 41, sonuç panelinde yeniden kodlamadan hızlı kırpma (<see cref="RecorderQuickTrim"/>,
/// <c>RecorderView.HizliKirpma.cs</c>): başlangıç önceki anahtar kareye oturur, kesim
/// <see cref="ClipExport"/>'un <c>-ss</c>/<c>-t</c>/<c>-c copy</c> yolundan geçer, hedef kaynağın yanında
/// çakışmasız ad alır. Arayüz kolu sahte yoklama, küçük resim, kesim ve süre ölçüsüyle; düğme yalnız tam,
/// GIF olmayan kayıtta görünür. Canlı kol 6 sn 160x90 lavfi (<c>-g 30</c>, <c>-threads 2</c>) klibini
/// gerçek ffprobe ve ffmpeg ile keser, süreyi ffprobe'la okur; oturtulmamış başlangıç olumsuz kontrol.
/// Klasör <c>.calisma/worktree-agent-ac850db3641511d52/</c>, test siler.
/// </summary>
public sealed class KaydediciHizliKirpmaTests
{
    private static readonly double[] Kareler = { 0, 2, 4, 6, 8 };

    private static string Klasor()
    {
        var yol = Path.Combine(GirdiKanit.Root, ".calisma", "worktree-agent-ac850db3641511d52", "hizli-kirpma-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(yol);
        return yol;
    }

    [Fact]
    public void BaslangicOncekiAnahtarKareyeOturur()
    {
        Assert.Equal(2, RecorderQuickTrim.SnapStart(Kareler, 3.9));
        Assert.Equal(4, RecorderQuickTrim.SnapStart(Kareler, 4));
        Assert.Equal(0, RecorderQuickTrim.SnapStart(new[] { 2.0, 4.0 }, 1.5));
        Assert.Equal(8, RecorderQuickTrim.SnapStart(Kareler, 9.5));
    }

    [Fact]
    public void IstekOturmusBaslangicVeSonaKadarSureTasir()
    {
        var istek = RecorderQuickTrim.Request("k.mp4", "h.mp4", Kareler, 3.1, 7.5);

        Assert.NotNull(istek);
        Assert.Equal(2, istek!.StartSeconds);
        Assert.Equal(5.5, istek.DurationSeconds, 6);
        Assert.Equal(ClipKind.Video, istek.Kind);
        Assert.Null(RecorderQuickTrim.Request("k.mp4", "h.mp4", Kareler, 4.5, 4));
        Assert.Null(RecorderQuickTrim.Request("k.mp4", "h.mp4", Kareler, 1, double.NaN));

        var argumanlar = ClipExport.Arguments(istek).ToList();
        Assert.True(argumanlar.IndexOf("-ss") < argumanlar.IndexOf("-i"));
        Assert.True(argumanlar.IndexOf("-t") > argumanlar.IndexOf("-i"));
        Assert.Equal("copy", argumanlar[argumanlar.IndexOf("-c") + 1]);
    }

    [Fact]
    public void HedefKaynaginYanindaCakismasizAdAlir()
    {
        var kaynak = Path.Combine("d", "kayit.mp4");
        var dolu = new HashSet<string> { Path.Combine("d", "kayit-kirpilmis.mp4"), Path.Combine("d", "kayit-kirpilmis-2.mp4") };

        Assert.Equal(Path.Combine("d", "kayit-kirpilmis.mp4"), RecorderQuickTrim.TargetFor(kaynak, _ => false));
        Assert.Equal(Path.Combine("d", "kayit-kirpilmis-3.mp4"), RecorderQuickTrim.TargetFor(kaynak, dolu.Contains));
    }

    [Fact]
    public void SonucPanelindeSeritKesimVeSureSoylenir()
    {
        var klasor = Klasor();
        try
        {
            var dosya = Path.Combine(klasor, "kayit.mp4");
            File.WriteAllBytes(dosya, new byte[] { 1 });
            var gif = Path.Combine(klasor, "kayit.gif");
            File.WriteAllBytes(gif, new byte[] { 1 });

            var olcu = AppHost.Run(() =>
            {
                var resimler = new SahteResim();
                ClipRequest? verilen = null;
                var view = new RecorderView
                {
                    RevealFolder = _ => { },
                    ResultThumbnails = resimler,
                    QuickTrimProbe = (_, _) => Task.FromResult(new QuickTrimSource(10, Kareler)),
                    QuickTrimCut = (istek, _) => { verilen = istek; return Task.FromResult(new ClipResult(true, istek.Target, string.Empty, TimeSpan.Zero)); },
                    QuickTrimMeasure = (_, _) => Task.FromResult(7.0),
                };

                view.ShowResult(new RecordResult(true, gif, 1, false, 0, string.Empty, 1));
                var gifte = view.QuickTrimVisible;
                view.ShowResult(new RecordResult(true, dosya, 1, true, 0, string.Empty, 1));
                var yarimda = view.QuickTrimVisible;
                view.ShowResult(new RecordResult(true, dosya, 1, false, 0, string.Empty, 1));
                var tamda = view.QuickTrimVisible;

                var acildi = view.OpenQuickTrimAsync();
                var kareSayisi = view.QuickTrimFrameCount;
                var okunan = resimler.Anlar.ToList();
                view.SetQuickTrimRange(3.1, 7.5);
                var aralik = view.QuickTrimRangeText;
                var kesildi = view.QuickTrimAsync();
                var bildirim = view.NoticeText;
                var olculen = Saat.Sure(7.0, Strings.Culture);
                var istenen = Saat.Sure(5.5, Strings.Culture);

                view.QuickTrimCut = (istek, _) => Task.FromResult(new ClipResult(false, istek.Target, "bozuk", TimeSpan.Zero));
                _ = view.QuickTrimAsync();
                var hataSonrasi = view.NoticeText;
                return (gifte, yarimda, tamda, acildi.IsCompleted, kareSayisi, okunan, aralik, kesildi.IsCompleted, verilen, bildirim, hataSonrasi, olculen, istenen);
            });

            Assert.False(olcu.gifte);
            Assert.False(olcu.yarimda);
            Assert.True(olcu.tamda);
            Assert.True(olcu.Item4);
            Assert.Equal(RecorderView.QuickTrimFrames, olcu.kareSayisi);
            Assert.Equal(RecorderView.QuickTrimFrames, olcu.okunan.Count);
            Assert.Equal(olcu.okunan.OrderBy(t => t), olcu.okunan);
            Assert.All(olcu.okunan, t => Assert.InRange(t, 1, EditTime.FromSeconds(10) - 1));
            Assert.Contains("0:02", olcu.aralik);
            Assert.True(olcu.Item8);
            Assert.NotNull(olcu.verilen);
            Assert.Equal(dosya, olcu.verilen!.Source);
            Assert.Equal(2, olcu.verilen.StartSeconds);
            Assert.Equal(5.5, olcu.verilen.DurationSeconds, 6);
            Assert.Equal(Path.Combine(klasor, "kayit-kirpilmis.mp4"), olcu.verilen.Target);
            Assert.Contains("kayit-kirpilmis.mp4", olcu.bildirim);
            Assert.NotEqual(olcu.olculen, olcu.istenen);
            Assert.Contains(olcu.olculen, olcu.bildirim);
            Assert.DoesNotContain(olcu.istenen, olcu.bildirim);
            Assert.Empty(olcu.hataSonrasi);
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    [FfmpegFact]
    public async Task GercekKlipAnahtarKaredenKopyalanirSuresiOlculur()
    {
        var klasor = Klasor();
        try
        {
            var kaynak = Path.Combine(klasor, "kayit.mp4");
            var uret = await FfmpegRunner.RunAsync(new[]
            {
                "-hide_banner", "-y", "-nostdin", "-f", "lavfi", "-i", "testsrc=size=160x90:rate=30:duration=6",
                "-c:v", "libx264", "-preset", "ultrafast", "-g", "30", "-keyint_min", "30", "-sc_threshold", "0",
                "-threads", "2", kaynak
            });
            Assert.True(uret.Ok, uret.StandardError);

            var kaynakBilgi = await RecorderView.ProbeQuickTrimSourceAsync(kaynak, CancellationToken.None);
            Assert.Equal(new[] { 0d, 1, 2, 3, 4, 5 }, kaynakBilgi.Keyframes.Select(k => Math.Round(k, 2)));
            Assert.InRange(kaynakBilgi.Duration, 5.9, 6.1);

            var hedef = RecorderQuickTrim.TargetFor(kaynak, File.Exists);
            var istek = RecorderQuickTrim.Request(kaynak, hedef, kaynakBilgi.Keyframes, 2.4, 4.5)!;
            var sonuc = await ClipExport.RunAsync(istek);
            Assert.True(sonuc.Ok, sonuc.Error);
            var sure = await RecorderView.MeasureQuickTrimAsync(hedef, CancellationToken.None);

            var oturmamis = Path.Combine(klasor, "oturmamis.mp4");
            var ham = await ClipExport.RunAsync(new ClipRequest(kaynak, oturmamis, 2.4, 2.1, ClipKind.Video));
            Assert.True(ham.Ok, ham.Error);
            var hamSure = await RecorderView.MeasureQuickTrimAsync(oturmamis, CancellationToken.None);

            Assert.Equal(2, istek.StartSeconds);
            Assert.Equal(2.5, istek.DurationSeconds, 6);
            Assert.InRange(sure, 2.5 - 1.0 / 30 - 0.001, 2.5 + 1.0 / 30 + 0.001);
            Assert.False(Math.Abs(hamSure - 2.1) <= 1.0 / 30, $"oturmamis sure {hamSure}");
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    private sealed class SahteResim : IThumbnailSource
    {
        internal List<long> Anlar { get; } = new();

        public Task<byte[]?> ReadAsync(string source, long sourceTick, int height, CancellationToken ct)
        {
            Anlar.Add(sourceTick);
            return Task.FromResult<byte[]?>(null);
        }
    }
}
