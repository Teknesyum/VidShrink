using System.Diagnostics;
using Avalonia.Controls;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using VidShrink.Player;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// OYB-21 akış kalite seçimi: HLS ana listesinin katmanları, kalite menüsü ve <c>hls-bitrate</c>
/// ile katman değişimi. Gerçek motorlu ölçü iki katmanlı yerel bir yayını <see cref="KlasorSunucu"/>'dan
/// (127.0.0.1) açar; ses <c>ao=null</c>. Ölçüm notu: <c>docs/olcumler/akis-kalitesi-kesintisiz.md</c>.
/// </summary>
public sealed class OynaticiAkisKalitesiTests
{
    private const string Ana = "#EXTM3U\n#EXT-X-VERSION:3\n"
        + "#EXT-X-STREAM-INF:BANDWIDTH=346672,AVERAGE-BANDWIDTH=331130,RESOLUTION=320x180,CODECS=\"avc1.42c00d,mp4a.40.2\"\nk1.m3u8\n"
        + "#EXT-X-STREAM-INF:CODECS=\"avc1.42c01e,mp4a.40.2\",RESOLUTION=640x360,BANDWIDTH=1289680\r\nk0.m3u8\n"
        + "#EXT-X-STREAM-INF:BANDWIDTH=64000,CODECS=\"mp4a.40.2\"\nses.m3u8\n"
        + "#EXT-X-STREAM-INF:BANDWIDTH=346672,RESOLUTION=320x180\nkopya.m3u8\n"
        + "#EXT-X-STREAM-INF:BANDWIDTH=abc,RESOLUTION=1x1\nbozuk.m3u8\n"
        + "#EXT-X-STREAM-INF:BANDWIDTH=0\nsifir.m3u8\n"
        + "#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID=\"a\",NAME=\"BANDWIDTH=9\"\n";

    [Fact]
    public void AnaListeKatmanlariYuksektenDusugeOkunur()
    {
        Assert.Equal(new[]
        {
            new HlsVariant(1289680, 640, 360),
            new HlsVariant(346672, 320, 180),
            new HlsVariant(64000, 0, 0)
        }, HlsVariants.Parse(Ana));
        Assert.Empty(HlsVariants.Parse("#EXTM3U\n#EXTINF:2,\nparca0.ts\n"));
        Assert.Equal(new[] { new HlsVariant(5, 0, 0) }, HlsVariants.Parse("#EXT-X-STREAM-INF:NAME=\"X,BANDWIDTH=9\",BANDWIDTH=5,RESOLUTION=0x0\n"));
    }

    [Theory]
    [InlineData("http://127.0.0.1:9/canli/yayin.m3u8", true)]
    [InlineData("https://ornek.test/yayin.M3U8?anahtar=1", true)]
    [InlineData("https://ornek.test/klip.mp4", false)]
    [InlineData("https://ornek.test/radyo.pls", false)]
    [InlineData("C:\\yerel\\yayin.m3u8", false)]
    [InlineData(null, false)]
    public void KaliteYalnizUzakM3u8AdresindeAranir(string? yol, bool beklenen)
        => Assert.Equal(beklenen, HlsVariants.IsCandidate(yol));

    [Fact]
    public void KatmanAdiCozunurlukVeHizdir()
    {
        var hiz = Strings.Get("player.tracks.quality.rate", Math.Round(1289680 / 1000.0).ToString("N0", Strings.Culture));
        Assert.Equal("640×360 · " + hiz, PlayerView.VariantName(new HlsVariant(1289680, 640, 360)));
        Assert.Equal(Strings.Get("player.tracks.quality.rate", "64"), PlayerView.VariantName(new HlsVariant(64000, 0, 0)));
        Assert.Equal("max", MpvEngine.StreamBitrateValue(0));
        Assert.Equal("346672", MpvEngine.StreamBitrateValue(346672));
    }

    [Fact]
    public void IkiKatmanliYerelYayindaKaliteMenusuKatmaniDegistirir()
    {
        var klasor = Path.Combine(GirdiKanit.Root, ".calisma", "akis-kalitesi", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(klasor);
        try
        {
            IkiKatmanUret(klasor);
            using var sunucu = new KlasorSunucu(klasor);
            var adres = sunucu.Adres("ana.m3u8");

            var rapor = AppHost.Run(() =>
            {
                var view = new PlayerView { EngineFactory = GercekMotor, StreamVariantReader = PlayerView.ReadStreamVariantsAsync };
                var window = new Window { Width = 640, Height = 480, Content = view };
                try
                {
                    view.OpenAddress(adres);
                    DenetimSurucu.Pump(view, () => view.Navigation.IsCompleted, 30);
                    DenetimSurucu.Pump(view, () => view.Engine is { IsOpen: true } && view.StreamVariants.Count == 2, 10);
                    var motor = (MpvEngine)view.Engine!;
                    var katmanlar = view.StreamVariants.ToList();
                    var videoIzi = motor.Tracks.Count(iz => iz.Kind == PlaybackTrackKind.Video);
                    var menude = OynaticiGirdiTestsMenuSatirlari.Hepsi(view.BuildMenu().Items).Any(item => (string?)item.Header == Strings.Get("player.tracks.quality"));
                    var ilk = view.BuildStreamQualityItems().OfType<MenuItem>().Select(item => item.IsChecked).ToList();
                    var ilkHiz = motor.StreamBitrate;
                    var ilkGenislik = motor.GetProperty("current-tracks/video/demux-w");

                    var dusur = view.SelectStreamQualityAsync(katmanlar[1].Bandwidth);
                    DenetimSurucu.Pump(view, () => dusur.IsCompleted, 20);
                    var sonra = (bitti: dusur.Result, hiz: motor.StreamBitrate, genislik: motor.GetProperty("current-tracks/video/demux-w"),
                        ses: motor.AudioTrack, oynuyor: !motor.IsPaused, secili: view.BuildStreamQualityItems().OfType<MenuItem>().Select(item => item.IsChecked).ToList(),
                        hbr: motor.GetProperty("hls-bitrate"));
                    var ayni = view.SelectStreamQualityAsync(katmanlar[1].Bandwidth);
                    DenetimSurucu.Pump(view, () => ayni.IsCompleted, 5);

                    var yukselt = view.SelectStreamQualityAsync(katmanlar[0].Bandwidth);
                    DenetimSurucu.Pump(view, () => yukselt.IsCompleted, 20);
                    return (katmanlar, videoIzi, menude, ilk, ilkHiz, ilkGenislik, sonra, ayni: ayni.Result, geri: (yukselt.Result, motor.StreamBitrate, motor.GetProperty("current-tracks/video/demux-w"), motor.AudioTrack));
                }
                finally
                {
                    view.Close();
                    window.Close();
                }
            });

            Assert.Equal(2, rapor.katmanlar.Count);
            Assert.Equal((640, 360), (rapor.katmanlar[0].Width, rapor.katmanlar[0].Height));
            Assert.Equal((320, 180), (rapor.katmanlar[1].Width, rapor.katmanlar[1].Height));
            Assert.Equal(1, rapor.videoIzi);
            Assert.True(rapor.menude, "kalite menusu yok");
            Assert.Equal(new[] { true, false }, rapor.ilk);
            Assert.Equal(rapor.katmanlar[0].Bandwidth, rapor.ilkHiz);
            Assert.Equal("640", rapor.ilkGenislik);

            Assert.True(rapor.sonra.bitti, "dusuk katmana gecilemedi");
            Assert.Equal(rapor.katmanlar[1].Bandwidth.ToString(System.Globalization.CultureInfo.InvariantCulture), rapor.sonra.hbr);
            Assert.Equal(rapor.katmanlar[1].Bandwidth, rapor.sonra.hiz);
            Assert.Equal("320", rapor.sonra.genislik);
            Assert.True(rapor.sonra.ses > 0, "katman degisince ses izi kapandi");
            Assert.True(rapor.sonra.oynuyor, "katman degisince oynatma durdu");
            Assert.Equal(new[] { false, true }, rapor.sonra.secili);
            Assert.False(rapor.ayni, "calan katman yeniden acildi");

            Assert.True(rapor.geri.Item1, "yuksek katmana donulemedi");
            Assert.Equal(rapor.katmanlar[0].Bandwidth, rapor.geri.Item2);
            Assert.Equal("640", rapor.geri.Item3);
            Assert.True(rapor.geri.Item4 > 0, "geri donuste ses izi kapandi");
        }
        finally
        {
            try
            {
                Directory.Delete(klasor, true);
                var ust = Path.GetDirectoryName(klasor)!;
                if (!Directory.EnumerateFileSystemEntries(ust).Any()) Directory.Delete(ust);
            }
            catch (IOException)
            {
            }
        }
    }

    private static IPlaybackEngine GercekMotor()
    {
        var engine = new MpvEngine();
        engine.SetProperty("ao", "null");
        return engine;
    }

    private static void IkiKatmanUret(string klasor)
    {
        Assert.True(ToolLocator.IsAvailable(out var eksik), $"HLS uretimi icin {eksik} gerekli");
        var psi = new ProcessStartInfo(ToolLocator.Ffmpeg)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = klasor
        };
        foreach (var arg in new[]
                 {
                     "-y", "-hide_banner", "-threads", "2",
                     "-f", "lavfi", "-i", "testsrc2=size=640x360:rate=25:duration=6",
                     "-f", "lavfi", "-i", "sine=frequency=440:duration=6",
                     "-filter_complex", "[0:v]split=2[a][b];[b]scale=320:180[b2]",
                     "-map", "[a]", "-map", "[b2]", "-map", "1:a", "-map", "1:a",
                     "-c:v", "libx264", "-preset", "ultrafast", "-g", "25", "-b:v:0", "800k", "-b:v:1", "200k",
                     "-c:a", "aac", "-b:a", "64k",
                     "-f", "hls", "-hls_time", "2", "-hls_list_size", "0",
                     "-master_pl_name", "ana.m3u8", "-var_stream_map", "v:0,a:0 v:1,a:1",
                     "-hls_segment_filename", "k%v_%d.ts", "k%v.m3u8"
                 })
            psi.ArgumentList.Add(arg);

        using var surec = Process.Start(psi)!;
        var cikti = surec.StandardOutput.ReadToEndAsync();
        var hata = surec.StandardError.ReadToEndAsync();
        Assert.True(surec.WaitForExit(60_000), "HLS uretimi 60 sn icinde bitmedi");
        cikti.GetAwaiter().GetResult();
        var gunluk = hata.GetAwaiter().GetResult();
        Assert.True(surec.ExitCode == 0, $"HLS uretilemedi (kod {surec.ExitCode}): {gunluk[Math.Max(0, gunluk.Length - 600)..]}");
    }
}
