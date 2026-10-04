using System.Diagnostics;
using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using VidShrink.App.Editing;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// 028 kabul şartlarının zaman ölçen üçü: 1 (çizim süresi kaynak uzunluğuna bağlı değil),
/// 4 (bırakıştan sonra oynatma başı ile motor aynı karede, gerçek libmpv) ve 5 (düzenlemeden
/// sonra ilk kare ≤ 1 sn, 10 dk 1080p kaynak, beş tekrar medyanı). Sayılar
/// <c>docs/olcumler/d3-cizelge-olcumleri.md</c>'ye, döküm <c>.calisma/duzenleyici-olcum/</c>'a.
/// </summary>
public sealed class DuzenleyiciOlcumTests
{
    private const long Sn = EditTime.TicksPerSecond;
    private const double Genislik = 1136;

    private static string Klasor(string ad)
    {
        var yol = Path.Combine(GirdiKanit.Root, ".calisma", "duzenleyici-olcum", ad);
        Directory.CreateDirectory(yol);
        return yol;
    }

    private static void Yaz(string ad, string govde) =>
        File.WriteAllText(Path.Combine(Klasor(""), ad), govde, new UTF8Encoding(false));

    private static void Ffmpeg(params string[] args)
    {
        Assert.True(ToolLocator.IsAvailable(out var eksik), $"olcum icin {eksik} gerekli");
        var (kod, _, hata) = GorunumKanit.Kos(ToolLocator.Ffmpeg, args);
        Assert.True(kod == 0, $"ffmpeg dustu: {hata[Math.Max(0, hata.Length - 400)..]}");
    }

    private static string? Gercek(string degisken)
    {
        var yol = Environment.GetEnvironmentVariable(degisken);
        if (string.IsNullOrWhiteSpace(yol)) return null;
        Assert.True(File.Exists(yol), $"{degisken} dosyasi yok: {yol}");
        return yol;
    }

    private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    private static double Medyan(IReadOnlyList<double> dizi)
    {
        var s = dizi.OrderBy(x => x).ToArray();
        return s.Length % 2 == 1 ? s[s.Length / 2] : (s[s.Length / 2 - 1] + s[s.Length / 2]) / 2;
    }

    private static double Yuzdelik(IReadOnlyList<double> dizi, double p)
    {
        var s = dizi.OrderBy(x => x).ToArray();
        return s[Math.Min(s.Length - 1, (int)Math.Ceiling(p * s.Length) - 1)];
    }

    [HedefMakineFact]
    public void CizimSuresiKaynakUzunlugunaBagliDegil()
    {
        const int Isinma = 20;
        const int Tekrar = 150;
        var rapor = AppHost.Run(() =>
        {
            (EditorTimeline Cizelge, RenderTargetBitmap Hedef, long Adim, long Sure) Kur(long kaynakSn, long klipSn, bool sigdir)
            {
                var model = new EditTimeline(Enumerable.Range(0, (int)(kaynakSn / klipSn)).Select(i => new EditClip(i * klipSn * Sn, (i + 1) * klipSn * Sn)));
                var cizelge = new EditorTimeline();
                cizelge.Measure(new Size(Genislik, double.PositiveInfinity));
                cizelge.Show(model);
                cizelge.Fps = 30;
                cizelge.Measure(new Size(Genislik, double.PositiveInfinity));
                cizelge.Arrange(new Rect(0, 0, Genislik, cizelge.DesiredSize.Height));
                cizelge.PixelsPerTick = sigdir ? cizelge.MinPixelsPerTick : cizelge.TrackWidth / (10 * klipSn * Sn);
                var hedef = new RenderTargetBitmap(new PixelSize((int)Genislik, (int)Math.Ceiling(cizelge.DesiredSize.Height)));
                return (cizelge, hedef, klipSn * Sn, model.Duration);
            }

            var kollar = new (string Ad, long Kaynak, long Klip, bool Sigdir)[]
            {
                ("1 dk, 10 klip x 6 sn, 60 sn gorunum", 60, 6, false),
                ("60 dk, 600 klip x 6 sn, 60 sn gorunum", 3600, 6, false),
                ("60 dk, 10 klip x 6 dk, tamami gorunur", 3600, 360, true),
            };
            var kurulu = kollar.Select(k => Kur(k.Kaynak, k.Klip, k.Sigdir)).ToArray();
            var sureler = kollar.Select(_ => new List<double>()).ToArray();
            var gorunur = new int[kollar.Length];
            var rastgele = new Random(28);
            var saat = new Stopwatch();

            for (var i = 0; i < Isinma + Tekrar; i++)
            {
                for (var k = 0; k < kurulu.Length; k++)
                {
                    var (cizelge, hedef, adim, sure) = kurulu[k];
                    var bosluk = Math.Max(0, sure / adim - 10);
                    var bas = bosluk > 0 ? rastgele.NextInt64(0, bosluk + 1) * adim : 0;
                    saat.Restart();
                    cizelge.ViewStart = bas;
                    cizelge.Measure(new Size(Genislik, double.PositiveInfinity));
                    cizelge.Arrange(new Rect(0, 0, Genislik, cizelge.DesiredSize.Height));
                    hedef.Render(cizelge);
                    saat.Stop();
                    if (i >= Isinma) sureler[k].Add(saat.Elapsed.TotalMilliseconds);
                    gorunur[k] = Math.Max(gorunur[k], cizelge.Realized.Count);
                }
            }

            foreach (var (_, hedef, _, _) in kurulu) hedef.Dispose();
            return (kollar, sureler, gorunur);
        });

        var (adlar, olculer, gorunenler) = rapor;
        var taban = Medyan(olculer[0]);
        var metin = new StringBuilder($"kabul 1: kaydirma + yerlesim + RenderTargetBitmap, {Genislik} px, isinma {Isinma}, tekrar {Tekrar}, kollar sirayla serpistirilir{Environment.NewLine}");
        for (var k = 0; k < adlar.Length; k++)
            metin.AppendLine($"{adlar[k].Ad}: medyan {F(Medyan(olculer[k]))} ms, p95 {F(Yuzdelik(olculer[k], 0.95))} ms, en cok kurulan klip {gorunenler[k]}, oran {F(Medyan(olculer[k]) / taban)}");
        Yaz("kabul-1-cizim.txt", metin.ToString());

        for (var k = 1; k < adlar.Length; k++)
        {
            var oran = Medyan(olculer[k]) / taban;
            Assert.True(oran is >= 0.8 and <= 1.2, metin.ToString());
        }
        Assert.All(gorunenler, g => Assert.InRange(g, 10, 12));
    }

    [HedefMakineFact]
    public void DalgaBicimiIkiYuzKesimdeKaynakUzunlugunaBagliDegil()
    {
        const int Isinma = 20;
        const int Tekrar = 150;
        const int Kesim = 200;
        var rapor = AppHost.Run(() =>
        {
            (EditorTimeline Cizelge, RenderTargetBitmap Hedef) Kur(long kaynakSn)
            {
                var klip = kaynakSn * Sn / Kesim;
                var model = new EditTimeline(Enumerable.Range(0, Kesim).Select(i => new EditClip(i * klip, (i + 1) * klip, 1m, i % 3 == 0)), kaynakSn * Sn);
                var kova = (int)(kaynakSn * AudioPeaks.BucketsPerSecond);
                var min = new short[kova];
                var max = new short[kova];
                for (var i = 0; i < kova; i++)
                {
                    max[i] = (short)(i * 7919 % short.MaxValue);
                    min[i] = (short)-max[i];
                }

                var cizelge = new EditorTimeline();
                cizelge.Measure(new Size(Genislik, double.PositiveInfinity));
                cizelge.Show(model);
                cizelge.Fps = 30;
                cizelge.Peaks = new AudioPeaks(min, max, kaynakSn * AudioPeaks.SampleRate);
                cizelge.Measure(new Size(Genislik, double.PositiveInfinity));
                cizelge.Arrange(new Rect(0, 0, Genislik, cizelge.DesiredSize.Height));
                return (cizelge, new RenderTargetBitmap(new PixelSize((int)Genislik, (int)Math.Ceiling(cizelge.DesiredSize.Height))));
            }

            var kollar = new (string Ad, long Kaynak)[] { ("1 dk, 200 kesim, tamami gorunur", 60), ("60 dk, 200 kesim, tamami gorunur", 3600) };
            var kurulu = kollar.Select(k => Kur(k.Kaynak)).ToArray();
            var sureler = kollar.Select(_ => new List<double>()).ToArray();
            var kovalar = new int[kollar.Length];
            var saat = new Stopwatch();
            for (var i = 0; i < Isinma + Tekrar; i++)
            {
                for (var k = 0; k < kurulu.Length; k++)
                {
                    var (cizelge, hedef) = kurulu[k];
                    saat.Restart();
                    cizelge.Measure(new Size(Genislik, double.PositiveInfinity));
                    cizelge.Arrange(new Rect(0, 0, Genislik, cizelge.DesiredSize.Height));
                    hedef.Render(cizelge);
                    saat.Stop();
                    if (i >= Isinma) sureler[k].Add(saat.Elapsed.TotalMilliseconds);
                    kovalar[k] = Math.Max(kovalar[k], cizelge.WaveformBucketsDrawn);
                }
            }

            var genislik = kurulu[0].Cizelge.TrackWidth;
            foreach (var (_, hedef) in kurulu) hedef.Dispose();
            return (kollar, sureler, kovalar, genislik);
        });

        var (adlar, olculer, kovalar, iz) = rapor;
        var taban = Medyan(olculer[0]);
        var metin = new StringBuilder($"dalga bicimi: {Kesim} kesim, tepeli, RenderTargetBitmap, {Genislik} px, isinma {Isinma}, tekrar {Tekrar}{Environment.NewLine}");
        for (var k = 0; k < adlar.Length; k++)
            metin.AppendLine($"{adlar[k].Ad}: medyan {F(Medyan(olculer[k]))} ms, p95 {F(Yuzdelik(olculer[k], 0.95))} ms, en cok kova {kovalar[k]}, oran {F(Medyan(olculer[k]) / taban)}");
        Yaz("dalga-bicimi-200-kesim.txt", metin.ToString());

        var oran = Medyan(olculer[1]) / taban;
        Assert.True(oran is >= 0.8 and <= 1.2, metin.ToString());
        Assert.All(kovalar, k => Assert.InRange(k, 1, (int)iz + Kesim));
    }

    [Fact]
    public void BirakistanSonraOynatmaBasiMotorlaAyniKarede()
    {
        var klasor = Klasor("kabul-4-" + Guid.NewGuid().ToString("N")[..6]);
        var gercek = Gercek("VIDSHRINK_D3_KISA_KAYNAK");
        var kaynak = gercek ?? Path.Combine(klasor, "kaynak.mp4");
        if (gercek is null)
            Ffmpeg("-y", "-hide_banner", "-f", "lavfi", "-i", "testsrc2=size=640x360:rate=30", "-t", "20", "-pix_fmt", "yuv420p", "-c:v", "libx264", "-preset", "ultrafast", "-g", "30", "-threads", "2", kaynak);

        try
        {
            var (satirlar, fps) = AppHost.Run(() =>
            {
                var view = new EditorView();
                var pencere = new Window { Width = Genislik, Height = 720, Content = view };
                pencere.Show();
                try
                {
                    var acilis = view.OpenSourceAsync(kaynak);
                    DenetimSurucu.Pump(view.Player, () => acilis.IsCompleted, 30);
                    Assert.True(acilis.IsCompleted, "kaynak 30 sn icinde acilmadi");
                    acilis.GetAwaiter().GetResult();
                    Assert.NotNull(view.Model);
                    view.Run(EditorCommand.ShuttleStop);

                    view.TimelineView.Playhead = 5 * Sn;
                    Assert.True(view.Split());
                    view.TimelineView.Playhead = 12 * Sn;
                    Assert.True(view.Split());
                    view.TimelineView.SelectedIndex = 1;
                    Assert.True(view.DeleteSelected());
                    DenetimSurucu.Pump(view.Player, () => false, 1.5);

                    var cizelge = view.TimelineView;
                    var kareHizi = cizelge.Fps;
                    var sonuc = new List<(double Hedef, double Motor, double Kare)>();
                    foreach (var oran in new[] { 0.13, 0.37, 0.52, 0.71, 0.94 })
                    {
                        var x = cizelge.TimeToX((long)(cizelge.Model!.Duration * oran));
                        cizelge.ScrubTo(x - 6, false);
                        cizelge.ScrubTo(x, false);
                        cizelge.ScrubTo(x, true);
                        var hedef = cizelge.Playhead;

                        var son = long.MinValue;
                        var sabit = Stopwatch.StartNew();
                        var toplam = Stopwatch.StartNew();
                        while (toplam.Elapsed.TotalSeconds < 5 && sabit.Elapsed.TotalMilliseconds < 300)
                        {
                            DenetimSurucu.Pump(view.Player, () => false, 0.03);
                            var simdi = view.Driver!.TimelinePosition;
                            if (simdi != son)
                            {
                                son = simdi;
                                sabit.Restart();
                            }
                        }

                        var fark = Math.Abs(hedef - son) / (Sn / kareHizi);
                        sonuc.Add((EditTime.ToSeconds(hedef), EditTime.ToSeconds(son), fark));
                    }

                    return (sonuc, kareHizi);
                }
                finally
                {
                    view.Player.Close();
                    pencere.Close();
                }
            });

            var tanim = gercek is null ? "20 sn 640x360 testsrc2" : $"gercek kaynak {Path.GetFileName(gercek)}";
            var metin = new StringBuilder($"kabul 4: {tanim}, {F(fps)} fps, iki kesim + orta parca silindi, gercek libmpv{Environment.NewLine}");
            foreach (var (hedef, motor, kare) in satirlar)
                metin.AppendLine($"birakis {F(hedef)} sn, motor {F(motor)} sn, fark {F(kare)} kare");
            Yaz(gercek is null ? "kabul-4-birakis.txt" : $"kabul-4-birakis-{Path.GetFileNameWithoutExtension(gercek)}.txt", metin.ToString());

            Assert.Equal(5, satirlar.Count);
            Assert.All(satirlar, s => Assert.True(s.Kare <= 1, metin.ToString()));
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    [HedefMakineFact]
    public void DuzenlemedenSonraIlkKareBirSaniyeninAltinda()
    {
        var klasor = Klasor("kabul-5-" + Guid.NewGuid().ToString("N")[..6]);
        var parca = Path.Combine(klasor, "parca.mp4");
        var liste = Path.Combine(klasor, "liste.txt");
        var gercek = Gercek("VIDSHRINK_D3_UZUN_KAYNAK");
        var kaynak = gercek ?? Path.Combine(klasor, "kaynak-10dk-1080p.mp4");
        if (gercek is null)
        {
            Ffmpeg("-y", "-hide_banner", "-f", "lavfi", "-i", "testsrc2=size=1920x1080:rate=30", "-t", "10", "-pix_fmt", "yuv420p", "-c:v", "libx264", "-preset", "ultrafast", "-crf", "30", "-g", "60", "-threads", "2", parca);
            File.WriteAllLines(liste, Enumerable.Repeat("file 'parca.mp4'", 60));
            Ffmpeg("-y", "-hide_banner", "-f", "concat", "-safe", "0", "-i", liste, "-c", "copy", kaynak);
        }

        try
        {
            var (olcum, sure, ilkSure, kareHizi) = AppHost.Run(() =>
            {
                var view = new EditorView();
                var pencere = new Window { Width = Genislik, Height = 720, Content = view };
                pencere.Show();
                try
                {
                    var acilis = view.OpenSourceAsync(kaynak);
                    DenetimSurucu.Pump(view.Player, () => acilis.IsCompleted, 30);
                    Assert.True(acilis.IsCompleted, "kaynak 30 sn icinde acilmadi");
                    acilis.GetAwaiter().GetResult();
                    Assert.NotNull(view.Model);
                    view.Run(EditorCommand.ShuttleStop);
                    DenetimSurucu.Pump(view.Player, () => false, 1);
                    var baslangic = EditTime.ToSeconds(view.Model!.Duration);
                    var fps = view.TimelineView.Fps;

                    var islemler = new Dictionary<string, List<double>> { ["bol"] = new(), ["tasi"] = new(), ["hiz"] = new(), ["sil"] = new() };

                    void Olc(string ad, Func<bool> islem)
                    {
                        DenetimSurucu.Pump(view.Player, () => false, 0.5);
                        var once = view.Player.DrawnFrames;
                        var yukleme = view.Reloads;
                        var saat = Stopwatch.StartNew();
                        Assert.True(islem(), ad);
                        DenetimSurucu.Pump(view.Player, () => view.Player.DrawnFrames > once, 10);
                        saat.Stop();
                        Assert.True(view.Player.DrawnFrames > once, $"{ad}: 10 sn icinde kare cizilmedi");
                        Assert.Equal(yukleme + 1, view.Reloads);
                        islemler[ad].Add(saat.Elapsed.TotalMilliseconds);
                    }

                    for (var r = 0; r < 5; r++)
                    {
                        Olc("bol", () =>
                        {
                            view.TimelineView.Playhead = view.Model!.Duration / 2;
                            return view.Split();
                        });
                        Olc("tasi", () => view.Move(0, 1));
                        Olc("hiz", () =>
                        {
                            view.TimelineView.SelectedIndex = 0;
                            return view.SetSpeed(r % 2 == 0 ? 2m : 1.5m);
                        });
                        Olc("sil", () =>
                        {
                            view.TimelineView.SelectedIndex = 0;
                            return view.DeleteSelected();
                        });
                    }

                    return (islemler, EditTime.ToSeconds(view.Model!.Duration), baslangic, fps);
                }
                finally
                {
                    view.Player.Close();
                    pencere.Close();
                }
            });

            var tanim = gercek is null ? "10 dk 1920x1080 h264 testsrc2 (10 sn parca x 60, -c copy)" : $"gercek kaynak {Path.GetFileName(gercek)}";
            var metin = new StringBuilder($"kabul 5: {tanim}, {F(kareHizi)} fps, ilk cizelge suresi {F(ilkSure)} sn, gercek libmpv, islem cagrisindan PlayerView.DrawnFrames artisina{Environment.NewLine}");
            foreach (var (ad, dizi) in olcum)
                metin.AppendLine($"{ad}: medyan {F(Medyan(dizi))} ms, en cok {F(dizi.Max())} ms, tekrarlar {string.Join(" ", dizi.Select(F))}");
            metin.AppendLine($"son cizelge suresi {F(sure)} sn");
            Yaz(gercek is null ? "kabul-5-ilk-kare.txt" : $"kabul-5-ilk-kare-{Path.GetFileNameWithoutExtension(gercek)}.txt", metin.ToString());

            Assert.All(olcum, o => Assert.True(Medyan(o.Value) <= 1000, metin.ToString()));
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }
}
