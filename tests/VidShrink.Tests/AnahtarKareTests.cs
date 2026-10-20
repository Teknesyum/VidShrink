using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using VidShrink.App.Editing;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using VidShrink.Player;
using Xunit;

namespace VidShrink.Tests;

public sealed class AnahtarKareTests
{
    private const string Dal = "yan-anahtar-kare";
    private const long Gb = 1_000_000_000;

    private static long S(double saniye) => EditTime.FromSeconds(saniye);

    private static string Kanit
    {
        get
        {
            var yol = Path.Combine(GirdiKanit.Root, ".calisma", Dal);
            Directory.CreateDirectory(yol);
            return yol;
        }
    }

    private static MediaInfo Bilgi(int w = 320, int h = 240) => new()
    {
        FilePath = "kaynak.mp4",
        FileSizeBytes = 1,
        DurationSeconds = 60,
        Width = w,
        Height = h,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 1,
        AudioCodec = "aac",
        PixelFormat = "yuv420p",
        BitDepth = 8
    };

    private static KeyframeTrack<VolumeKey> Ses(params VolumeKey[] kareler) => new(kareler);

    private static KeyframeTrack<CropKey> Kirp(params CropKey[] kareler) => new(kareler);

    private static readonly KeyframeTrack<CropKey> SoldanSaga = Kirp(new CropKey(0, 0, 0, 0.5, 0), new CropKey(S(2), 0.5, 0, 0, 0));

    [Theory]
    [InlineData(KeyframeEase.Linear, -10)]
    [InlineData(KeyframeEase.EaseIn, -5)]
    [InlineData(KeyframeEase.EaseOut, -15)]
    [InlineData(KeyframeEase.EaseInOut, -10)]
    public void EgriIkiKareArasindaDegeriBicimlendirir(KeyframeEase egri, double ortada)
    {
        var iz = Ses(new VolumeKey(0, 0, egri), new VolumeKey(S(2), -20));
        Assert.Equal(0, iz.ValueAt(0).Db, 6);
        Assert.Equal(ortada, iz.ValueAt(S(1)).Db, 6);
        Assert.Equal(-20, iz.ValueAt(S(2)).Db, 6);
        Assert.Equal(-20, iz.ValueAt(S(5)).Db, 6);
        var ceyrek = iz.ValueAt(S(0.5)).Db;
        var bekleneni = egri switch { KeyframeEase.EaseIn => -1.25, KeyframeEase.EaseOut => -8.75, KeyframeEase.EaseInOut => -3.125, _ => -5 };
        Assert.Equal(bekleneni, ceyrek, 6);
    }

    [Fact]
    public void IzSiralanirAyniOfsetteSonuncuKalirVeIcerikleKarsilastirilir()
    {
        var iz = Ses(new VolumeKey(S(2), -6), new VolumeKey(0, 0), new VolumeKey(S(2), -12), new VolumeKey(-5, 3));
        Assert.Equal(new[] { new VolumeKey(0, 3), new VolumeKey(S(2), -12) }, iz.ToArray());
        Assert.Equal(iz, Ses(new VolumeKey(0, 3), new VolumeKey(S(2), -12)));
        Assert.NotEqual(iz, Ses(new VolumeKey(0, 3), new VolumeKey(S(2), -12, KeyframeEase.EaseOut)));
        Assert.Equal(new ClipEffects { VolumeKeys = iz }, new ClipEffects { VolumeKeys = Ses(new VolumeKey(0, 3), new VolumeKey(S(2), -12)) });
    }

    [Fact]
    public void IfadeKareArasindaEgriyleZamanaBaglanir()
    {
        var iz = Ses(new VolumeKey(0, 0), new VolumeKey(S(2), -20, KeyframeEase.EaseIn), new VolumeKey(S(3), -20));
        Assert.Equal("if(lt((t),2),0+(-20)*(clip(((t)-0)/2,0,1)),if(lt((t),3),(-20),(-20)))", iz.Expression(k => k.Db, "t"));
        var giris = Ses(new VolumeKey(0, 0, KeyframeEase.EaseIn), new VolumeKey(S(1), 6));
        Assert.Equal("if(lt((t),1),0+6*(clip(((t)-0)/1,0,1)*clip(((t)-0)/1,0,1)),6)", giris.Expression(k => k.Db, "t"));
        var cikis = Ses(new VolumeKey(0, 0, KeyframeEase.EaseOut), new VolumeKey(S(1), 6));
        Assert.Contains("*(2-clip(", cikis.Expression(k => k.Db, "t"));
        var ikisi = Ses(new VolumeKey(0, 0, KeyframeEase.EaseInOut), new VolumeKey(S(1), 6));
        Assert.Contains("*(3-2*clip(", ikisi.Expression(k => k.Db, "t"));
        Assert.Equal("(-4)", Ses(new VolumeKey(S(1), -4)).Expression(k => k.Db, "t"));
    }

    [Fact]
    public void SesZinciriSabitYerineZamanaBagliKazancYazar()
    {
        var etki = new ClipEffects { VolumeDb = -3, VolumeKeys = Ses(new VolumeKey(0, 0), new VolumeKey(S(2), -20)) };
        Assert.True(etki.HasAudio);
        Assert.False(etki.IsNeutral);
        var zincir = ClipFilters.Audio(etki, S(2));
        Assert.Equal("volume=volume='pow(10,(if(lt((t),2),0+(-20)*(clip(((t)-0)/2,0,1)),(-20)))/20)':eval=frame", zincir);
        Assert.DoesNotContain("-3dB", zincir);
        Assert.Equal("volume=-3dB", ClipFilters.Audio(new ClipEffects { VolumeDb = -3 }, S(2)));
    }

    [Fact]
    public void KirpmaKareleriOlcekVeKaydirmaIleBoyutuSabitTutar()
    {
        var etki = new ClipEffects { CropKeys = SoldanSaga }.Normalized();
        Assert.True(etki.HasMovingCrop);
        Assert.Equal((160, 240), etki.OutputSize(320, 240));
        var zincir = ClipFilters.Geometry(etki, 320, 240, 1, 1, (160, 240));
        Assert.StartsWith("scale=w='ceil(160/(1-(", zincir);
        Assert.Contains(":eval=frame,crop=160:240:x='min(iw-ow,max(0,floor((if(lt((t),2),0+0.5*(clip(((t)-0)/2,0,1)),0.5))*iw)))'", zincir);
        Assert.DoesNotContain("crop=160:240:0:0", zincir);

        var tek = new ClipEffects { CropKeys = Kirp(new CropKey(S(1), 0.25, 0, 0.25, 0)) }.Normalized();
        Assert.False(tek.HasMovingCrop);
        Assert.Equal("crop=160:240:80:0,setsar=1", ClipFilters.Geometry(tek, 320, 240, 1, 1, (160, 240)));
    }

    [Fact]
    public void TeslimGrafiKlipBasindanZamanaBagliIfadeTasir()
    {
        var c = EditTimeline.FromSource(S(4));
        Assert.True(c.Split(S(2)));
        Assert.True(c.SetEffects(new[] { 1 }, e => e with { CropKeys = SoldanSaga, VolumeKeys = Ses(new VolumeKey(0, 0), new VolumeKey(S(2), -30)) }));
        var plan = EditExport.Build(c, Bilgi(), new double[] { 0, 2 }, 0, ExportMode.Fast, "cikti.mp4", "is", 8 * Gb);
        Assert.Equal(ExportMode.Full, plan.Effective);
        var args = plan.Steps[0].Args.ToList();
        var graf = args[args.IndexOf("-filter_complex") + 1];
        Assert.Contains("eval=frame,crop=160:240:x=", graf);
        Assert.Contains("volume=volume='pow(10,(if(lt((t),2),0+(-30)*", graf);
        Assert.Contains("pad=320:240", graf);
    }

    [Fact]
    public void OnizlemeIfadesiParcaninEdlZamaninaKayar()
    {
        var c = EditTimeline.FromSource(S(4));
        Assert.True(c.Split(S(1.5)));
        Assert.True(c.SetEffects(new[] { 1 }, e => e with { CropKeys = SoldanSaga, VolumeKeys = Ses(new VolumeKey(0, 0), new VolumeKey(S(2), -30)) }));
        var onizleme = new EdlPreview("kaynak.mp4", c);
        var gorunum = new EditLook(onizleme, 320, 240);
        var geometri = gorunum.Geometry(onizleme.Parts[1]);
        Assert.Contains("if(lt(((t-1.5)/1),2)", geometri);
        Assert.Contains("eval=frame", geometri);
        Assert.Contains("volume=volume='pow(10,(if(lt(((t-1.5)/1),2),0+(-30)*", gorunum.TimedAudio);
        Assert.Contains(":eval=frame:enable='gte(t,1.5)*lt(t,4)'", gorunum.TimedAudio);

        var hizli = EditTimeline.FromSource(S(4));
        Assert.True(hizli.SetSpeed(0, -2m));
        Assert.True(hizli.SetEffects(new[] { 0 }, e => e with { CropKeys = SoldanSaga }));
        var ters = new EdlPreview("kaynak.mp4", hizli);
        Assert.Contains("((4-t)/2)", new EditLook(ters, 320, 240).Geometry(ters.Parts[0]));
    }

    [Fact]
    public void BolmeKareleriIkiYanaKesipUcDegeriKorur()
    {
        var c = EditTimeline.FromSource(S(4));
        Assert.True(c.SetEffects(new[] { 0 }, e => e with { VolumeKeys = Ses(new VolumeKey(0, 0), new VolumeKey(S(4), -20)) }));
        Assert.True(c.Split(S(1)));
        Assert.Equal(new[] { new VolumeKey(0, 0), new VolumeKey(S(1), -5) }, c.Clips[0].Effects.VolumeKeys.ToArray());
        Assert.Equal(new[] { new VolumeKey(0, -5), new VolumeKey(S(3), -20) }, c.Clips[1].Effects.VolumeKeys.ToArray());

        Assert.True(c.SetSpeed(1, 2m));
        Assert.Equal(S(1.5), c.Clips[1].Effects.VolumeKeys[^1].Offset);
    }

    [Fact]
    public void NormallestirmeKareDegerleriniAraligaCeker()
    {
        var etki = new ClipEffects
        {
            VolumeKeys = Ses(new VolumeKey(0, -500), new VolumeKey(S(1), double.NaN), new VolumeKey(S(2), 99.04)),
            CropKeys = Kirp(new CropKey(0, 0.9, -1, 0.9, 2)),
        }.Normalized();
        Assert.Equal(new[] { ClipEffects.MinVolumeDb, 0, ClipEffects.MaxVolumeDb }, etki.VolumeKeys.Select(k => k.Db).ToArray());
        var kare = etki.CropKeys[0];
        Assert.Equal((0.9, 0.0, 0.05, 0.95), (kare.Left, kare.Top, Math.Round(kare.Right, 6), kare.Bottom));
        Assert.Empty(etki.WithFit(16, 9, "000000").CropKeys);
    }

    [Fact]
    public void ProjeDosyasiKareleriTasirEskiDosyaKaresizAcilir()
    {
        var c = EditTimeline.FromSource(S(10));
        Assert.True(c.SetEffects(new[] { 0 }, e => e with
        {
            VolumeKeys = Ses(new VolumeKey(0, 0, KeyframeEase.EaseInOut), new VolumeKey(S(3), -12)),
            CropKeys = Kirp(new CropKey(0, 0, 0, 0.5, 0, KeyframeEase.EaseOut), new CropKey(S(5), 0.5, 0.1, 0, 0.2)),
        }));
        var damga = new SourceStamp(@"C:\a.mp4", 16, 1);
        var json = EditProject.From(damga, c).ToJson();
        var okunan = EditProject.Parse(json);
        Assert.NotNull(okunan);
        Assert.Equal(c.Clips, okunan!.Clips);
        Assert.Contains("\"ease\":\"in-out\"", json.Replace(" ", string.Empty));
        Assert.Equal(1, JsonDocument.Parse(json).RootElement.GetProperty("version").GetInt32());

        var eski = EditProject.From(damga, EditTimelineWith(e => e with { VolumeDb = -6 })).ToJson();
        Assert.DoesNotContain("volumeKeys", eski);
        var eskiOkunan = EditProject.Parse(eski)!;
        Assert.Empty(eskiOkunan.Clips[0].Effects.VolumeKeys);
        Assert.Empty(eskiOkunan.Clips[0].Effects.CropKeys);
        Assert.Equal(-6, eskiOkunan.Clips[0].Effects.VolumeDb);
    }

    [Fact]
    public void ArayuzOynatmaBasindaSecilenEgriyleKareEklerVeTemizler()
    {
        var klasor = Path.Combine(Kanit, "arayuz");
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        Directory.CreateDirectory(klasor);
        var dosya = Path.Combine(klasor, "kaynak.mp4");
        File.WriteAllBytes(dosya, new byte[16]);
        try
        {
            AppHost.Run(() =>
            {
                var motor = new KlipMotoru();
                var view = new EditorView
                {
                    KnownInfo = yol => new MediaInfo { FilePath = yol, FileSizeBytes = 6_000_000, DurationSeconds = 10, Width = 320, Height = 240, Fps = 30, VideoCodec = "h264", TotalBitrateBps = 4_000_000 }
                };
                view.Player.EngineFactory = () => motor;
                view.OverlayRoot = Path.Combine(klasor, "katman");
                var pencere = new Window { Width = 900, Height = 600, Content = view };
                pencere.Show();
                try
                {
                    var acilis = view.OpenSourceAsync(dosya);
                    DenetimSurucu.Pump(view.Player, () => acilis.IsCompleted, 10);
                    acilis.GetAwaiter().GetResult();
                    var model = view.Model!;
                    view.Timeline.SelectedIndex = 0;
                    Assert.Equal(4, view.CmbClipEase.ItemCount);
                    Assert.False(view.BtnClipKeysClear.IsEnabled);

                    view.Timeline.Playhead = S(2);
                    view.CmbClipEase.SelectedIndex = 1;
                    Assert.True(view.AddVolumeKey());
                    Assert.True(view.SetClipEffects(e => e with { VolumeDb = -30 }));
                    view.Timeline.Playhead = S(6);
                    Assert.True(view.AddVolumeKey());
                    Assert.Equal(new[] { new VolumeKey(S(2), 0, KeyframeEase.EaseIn), new VolumeKey(S(6), -30, KeyframeEase.EaseIn) }, model.Clips[0].Effects.VolumeKeys.ToArray());
                    Assert.Equal(string.Format(Strings.Culture, Strings.Get("editor.clip.keys.count"), 2, 0, 0), view.TxtClipKeys.Text);
                    Assert.True(view.BtnClipKeysClear.IsEnabled);

                    Assert.True(view.CropClip(1));
                    Assert.True(view.AddCropKey());
                    Assert.Single(model.Clips[0].Effects.CropKeys);
                    Assert.Equal(S(6), model.Clips[0].Effects.CropKeys[0].Offset);

                    view.Timeline.Playhead = model.Duration;
                    Assert.Equal(model.Clips[0].TimelineLength - 1, view.KeyOffset());

                    view.BtnClipKeysClear.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                    Assert.False(model.Clips[0].Effects.HasKeyframes);
                    Assert.True(view.Undo());
                    Assert.Equal(2, model.Clips[0].Effects.VolumeKeys.Count);
                }
                finally
                {
                    DuzenleyiciKapanis.Kapat(pencere, view);
                }
            });
        }
        finally
        {
            if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        }
    }

    private static EditTimeline EditTimelineWith(Func<ClipEffects, ClipEffects> ayar)
    {
        var c = EditTimeline.FromSource(S(10));
        Assert.True(c.SetEffects(new[] { 0 }, ayar));
        return c;
    }

    [FfmpegFact]
    public async Task CanliSesDuzeyiBastaVeSondaFarkliEgriOrtayiDegistirir()
    {
        var klasor = Path.Combine(Kanit, "ses-canli");
        var adlar = new[] { "kaynak.mp4", "dogrusal.mp4", "giris.mp4" };
        Directory.CreateDirectory(klasor);
        KanitKapanisi.Onceki(klasor, adlar);
        var kaynak = Path.Combine(klasor, "kaynak.mp4");
        Ffmpeg("-hide_banner", "-nostdin", "-y", "-f", "lavfi", "-i", "color=gray:s=64x48:r=15:d=2",
            "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000:duration=2",
            "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", "-c:a", "aac", "-shortest", "-threads", "2", kaynak);

        async Task<string> Aktar(string ad, KeyframeEase egri)
        {
            var c = EditTimeline.FromSource(S(2));
            Assert.True(c.SetEffects(new[] { 0 }, e => e with { VolumeKeys = Ses(new VolumeKey(0, 0, egri), new VolumeKey(S(2), -30)) }));
            var cikti = Path.Combine(klasor, ad);
            var plan = await EditExportRunner.PrepareAsync(new[] { kaynak }, c, ExportMode.Full, cikti, 8 * Gb);
            await EditExportRunner.RunAsync(IkiIsParcacigi(plan), null);
            return cikti;
        }

        var dogrusal = await Aktar("dogrusal.mp4", KeyframeEase.Linear);
        var giris = await Aktar("giris.mp4", KeyframeEase.EaseIn);
        var bas = SesOrtalamasi(dogrusal, 0.1, 0.2);
        var son = SesOrtalamasi(dogrusal, 1.7, 0.2);
        var orta = SesOrtalamasi(dogrusal, 0.9, 0.2);
        var girisOrta = SesOrtalamasi(giris, 0.9, 0.2);
        var kayit = $"bas {bas} son {son} orta {orta} giris-orta {girisOrta}";
        Assert.True(bas - son > 20, kayit);
        Assert.InRange(bas - orta, 11, 19);
        Assert.True(girisOrta - orta > 4, kayit);

        KanitKapanisi.Kapat(klasor, adlar);
    }

    [FfmpegFact]
    public async Task CanliKirpmaIkiZamandaFarkliYeriGosterir()
    {
        var klasor = Path.Combine(Kanit, "kirp-canli");
        var adlar = new[] { "kaynak.mp4", "kayan.mp4", "bas.gray", "son.gray" };
        Directory.CreateDirectory(klasor);
        KanitKapanisi.Onceki(klasor, adlar);
        var kaynak = Path.Combine(klasor, "kaynak.mp4");
        Ffmpeg("-hide_banner", "-nostdin", "-y", "-f", "lavfi", "-i", "color=black:s=160x120:r=15:d=2,drawbox=x=80:y=0:w=80:h=120:color=white:t=fill",
            "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", "-threads", "2", kaynak);

        var c = EditTimeline.FromSource(S(2));
        Assert.True(c.SetEffects(new[] { 0 }, e => e with { CropKeys = SoldanSaga }));
        var cikti = Path.Combine(klasor, "kayan.mp4");
        var plan = await EditExportRunner.PrepareAsync(new[] { kaynak }, c, ExportMode.Full, cikti, 8 * Gb);
        await EditExportRunner.RunAsync(IkiIsParcacigi(plan), null);

        var (w, h) = Boyut(cikti);
        Assert.Equal((80, 120), (w, h));
        var bas = Parlaklik(cikti, Path.Combine(klasor, "bas.gray"), 0.05, w, h);
        var son = Parlaklik(cikti, Path.Combine(klasor, "son.gray"), 1.9, w, h);
        Assert.True(bas < 60, $"bas {bas}");
        Assert.True(son > 190, $"son {son}");

        KanitKapanisi.Kapat(klasor, adlar);
    }

    [FfmpegFact]
    public async Task LibmpvOnizlemesiZamanaBagliKirpmaVeSesiTasir()
    {
        var klasor = Path.Combine(Kanit, "kirp-mpv");
        var adlar = new[] { "kaynak.mp4" };
        Directory.CreateDirectory(klasor);
        KanitKapanisi.Onceki(klasor, adlar);
        var kaynak = Path.Combine(klasor, "kaynak.mp4");
        Ffmpeg("-hide_banner", "-nostdin", "-y", "-f", "lavfi", "-i", "color=black:s=160x120:r=30:d=3,drawbox=x=80:y=0:w=80:h=120:color=white:t=fill",
            "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000:duration=3",
            "-c:v", "libx264", "-preset", "ultrafast", "-g", "15", "-pix_fmt", "yuv420p", "-c:a", "aac", "-shortest", "-threads", "2", kaynak);

        var c = EditTimeline.FromSource(S(3));
        Assert.True(c.Split(S(1)));
        Assert.True(c.SetEffects(new[] { 1 }, e => e with { CropKeys = SoldanSaga, VolumeKeys = Ses(new VolumeKey(0, 0), new VolumeKey(S(0.5), -40)) }));
        var onizleme = new EdlPreview(kaynak, c);
        var gorunum = new EditLook(onizleme, 160, 120);

        using (var motor = new MpvEngine())
        {
            motor.SetProperty("ao", "null");
            var surucu = new EdlPreviewDriver(motor, onizleme, look: gorunum);
            await surucu.OpenAsync();
            motor.Pause();

            await surucu.SeekAsync(S(0.5), SeekPrecision.Exact);
            var ilk = await KareBekleAsync(motor, 0, k => k.Parlak > 190);
            Assert.True(ilk.Parlak > 190, $"birinci parca ortasi beyaz degil: {ilk.Parlak}");

            await surucu.SeekAsync(S(1.05), SeekPrecision.Exact);
            var bas = await KareBekleAsync(motor, ilk.Gorulen, k => k.Parlak < 60);
            Assert.True((bas.W, bas.H) == (160, 120) && bas.Parlak < 60, $"EDL 1.05 sn: {bas.W}x{bas.H} parlak {bas.Parlak}; vf: {motor.GetProperty("vf")}");

            await surucu.SeekAsync(S(2.9), SeekPrecision.Exact);
            var son = await KareBekleAsync(motor, bas.Gorulen, k => k.Parlak > 190);
            Assert.True(son.Parlak > 190, $"EDL 2.9 sn kirpma kaymadi: {son.Parlak}");

            Assert.True(motor.MeterAudio(true));
            await surucu.SeekAsync(S(0.3), SeekPrecision.Exact);
            surucu.Play();
            var saat = Stopwatch.StartNew();
            var tepeler = new List<(double Zaman, double Db)>();
            while (saat.Elapsed < TimeSpan.FromSeconds(4) && surucu.EdlPosition < S(2.6))
            {
                await Task.Delay(40);
                var db = motor.AudioPeakDb;
                if (double.IsFinite(db) || double.IsNegativeInfinity(db)) tepeler.Add((motor.PositionSeconds, db));
            }

            motor.Pause();
            Assert.True(motor.MeterAudio(false));
            var kayit = string.Join("; ", tepeler.Select(p => p.Zaman.ToString("0.00", CultureInfo.InvariantCulture) + "=" + p.Db.ToString("0.0", CultureInfo.InvariantCulture)));
            Assert.True(tepeler.Count > 10, kayit);
            var yalin = tepeler.Take(3).Max(p => p.Db);
            var gec = tepeler.TakeLast(5).Average(p => p.Db);
            Assert.True(yalin is > -21 and < -15, kayit);
            Assert.True(gec - yalin is > -43 and < -37, kayit);
        }

        KanitKapanisi.Kapat(klasor, adlar);
    }

    private static async Task<(int W, int H, int Parlak, long Gorulen)> KareBekleAsync(MpvEngine motor, long gorulen, Func<(int W, int H, int Parlak), bool> kosul)
    {
        var saat = Stopwatch.StartNew();
        var kare = await KareAsync(motor, gorulen);
        while (!kosul((kare.W, kare.H, kare.Parlak)) && saat.Elapsed < TimeSpan.FromSeconds(5))
        {
            await Task.Delay(20);
            kare = await KareAsync(motor, -1);
        }

        return kare;
    }

    private static async Task<(int W, int H, int Parlak, long Gorulen)> KareAsync(MpvEngine motor, long gorulen)
    {
        var saat = Stopwatch.StartNew();
        (int W, int H, int Parlak)? sonuc = null;
        while (sonuc is null && saat.Elapsed < TimeSpan.FromSeconds(10))
        {
            motor.TryCopyLatest(ref gorulen, (p, w, h, s) =>
            {
                var satir = new byte[s];
                Marshal.Copy(p + (h / 2) * s, satir, 0, s);
                var i = (w / 2) * 4;
                sonuc = (w, h, Math.Min(satir[i], Math.Min(satir[i + 1], satir[i + 2])));
            });
            if (sonuc is null) await Task.Delay(10);
        }

        Assert.True(sonuc is not null, "10 sn icinde kare gelmedi; son log: " + string.Join(" | ", motor.RecentLog));
        return (sonuc!.Value.W, sonuc.Value.H, sonuc.Value.Parlak, gorulen);
    }

    private static ExportPlan IkiIsParcacigi(ExportPlan plan)
    {
        var args = plan.Steps[0].Args.ToList();
        args.InsertRange(args.Count - 1, new[] { "-threads", "2" });
        return plan with { Steps = new[] { plan.Steps[0] with { Args = args } } };
    }

    private static (int W, int H) Boyut(string video)
    {
        var satir = Ffprobe("-v", "error", "-select_streams", "v:0", "-show_entries", "stream=width,height", "-of", "csv=p=0", video).Trim().Split(',');
        return (int.Parse(satir[0], CultureInfo.InvariantCulture), int.Parse(satir[1], CultureInfo.InvariantCulture));
    }

    private static double SesOrtalamasi(string video, double bas, double sure)
    {
        var dokum = Ffmpeg("-hide_banner", "-nostdin", "-i", video, "-map", "0:a",
            "-af", "atrim=start=" + bas.ToString(CultureInfo.InvariantCulture) + ":duration=" + sure.ToString(CultureInfo.InvariantCulture) + ",volumedetect",
            "-threads", "2", "-f", "null", "-");
        var eslesme = Regex.Match(dokum, @"mean_volume: (-?[\d.]+|-inf) dB");
        Assert.True(eslesme.Success, dokum);
        return eslesme.Groups[1].Value == "-inf" ? double.NegativeInfinity : double.Parse(eslesme.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    private static double Parlaklik(string video, string kare, double saniye, int w, int h)
    {
        Ffmpeg("-hide_banner", "-nostdin", "-v", "error", "-y", "-ss", saniye.ToString(CultureInfo.InvariantCulture), "-i", video,
            "-frames:v", "1", "-f", "rawvideo", "-pix_fmt", "gray", "-threads", "2", kare);
        var piksel = File.ReadAllBytes(kare);
        Assert.Equal(w * h, piksel.Length);
        return piksel.Average(b => (double)b);
    }

    private static string Ffmpeg(params string[] args) => Kos(ToolLocator.Ffmpeg, args, false);

    private static string Ffprobe(params string[] args) => Kos(ToolLocator.Ffprobe, args, true);

    private static string Kos(string arac, string[] args, bool cikti)
    {
        var psi = new ProcessStartInfo(arac)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var surec = Process.Start(psi)!;
        var hata = surec.StandardError.ReadToEndAsync();
        var standart = surec.StandardOutput.ReadToEndAsync();
        if (!surec.WaitForExit(60_000))
        {
            surec.Kill(true);
            Assert.Fail(Path.GetFileName(arac) + " 60 sn icinde bitmedi");
        }

        surec.WaitForExit();
        Assert.True(surec.ExitCode == 0, hata.Result);
        return cikti ? standart.Result : hata.Result;
    }
}
