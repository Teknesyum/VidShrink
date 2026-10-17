using System.Diagnostics;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Threading;
using VidShrink.App;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

/// <summary>
/// ARC-20, sesten video (later 90). Yalnız ses kaynağında "Görüntü" kapak ya da dalga biçimi seçilince MP4, MKV, MOV ve
/// WebM açılır; çıktı sesin (kırpılmış) süresi kadar sürer. Planın doğrulaması: çıktının süresi ses süresine eşit.
/// Canlı kol ffmpeg'in ürettiği 2 sn sinüs mp3 ve 64x48 PNG ile, <c>-threads 2</c>; klasör
/// <c>.calisma/testler/sesten-video-*</c>, test siler.
/// </summary>
public sealed class SestenVideoTests
{
    private static readonly MediaInfo Ses = new()
    {
        FilePath = @"C:\muzik\parca.mp3",
        FileSizeBytes = 2_000_000L,
        DurationSeconds = 60,
        Width = 0,
        Height = 0,
        Fps = 0,
        VideoCodec = "none",
        TotalBitrateBps = 256_000,
        AudioCodec = "mp3",
        AudioBitrateBps = 256_000,
        AudioChannels = 2,
        IsAudioOnly = true,
        Streams = new[] { new SourceStream(0, StreamKind.Audio, "mp3", Channels: 2) }
    };

    private static readonly MediaInfo KapakliSes = Ses with
    {
        Streams = new[] { new SourceStream(0, StreamKind.Audio, "mp3", Channels: 2), new SourceStream(1, StreamKind.Video, "png", IsAttachedPicture: true) }
    };

    private const string KapakYok = "The audio file has no embedded cover; pick a cover image.";
    private const string KodekGerek = "Making video from audio needs an H.264, H.265, VP9 or AV1 encoder.";
    private const string GoruntuYok = "The source has no video stream; choose an audio container.";

    [Fact]
    public void GorunturSecilinceVideoKaplariAcilir()
    {
        foreach (var kap in new[] { "mp4", "mkv", "mov", "webm" })
        {
            Assert.False(ConversionArguments.ContainerFitsSource(kap, Ses, AudioVisual.None), kap);
            Assert.True(ConversionArguments.ContainerFitsSource(kap, Ses, AudioVisual.Cover), kap);
            Assert.True(ConversionArguments.ContainerFitsSource(kap, Ses, AudioVisual.Waveform), kap);
        }
        foreach (var kap in new[] { "avi", "gif", "webp", "png", "mxf" })
            Assert.False(ConversionArguments.ContainerFitsSource(kap, Ses, AudioVisual.Waveform), kap);
        Assert.True(ConversionArguments.ContainerFitsSource("mp3", Ses, AudioVisual.Waveform));

        var dalga = new ConversionPlan { Container = "mp4", AudioVisual = AudioVisual.Waveform };
        Assert.Empty(ConversionArguments.Validate(Ses, dalga));
        Assert.Contains(GoruntuYok, ConversionArguments.Validate(Ses, dalga with { AudioVisual = AudioVisual.None }));
        Assert.Contains(GoruntuYok, ConversionArguments.Validate(Ses, dalga with { Container = "avi" }));
        Assert.Contains(KodekGerek, ConversionArguments.Validate(Ses, dalga with { VideoCodec = "copy" }));
        Assert.Contains(KodekGerek, ConversionArguments.Validate(Ses, dalga with { VideoCodec = IntermediateCodecs.ProRes, VideoProfile = "hq", Container = "mov" }));
        Assert.Contains(KapakYok, ConversionArguments.Validate(Ses, dalga with { AudioVisual = AudioVisual.Cover }));
        Assert.Empty(ConversionArguments.Validate(KapakliSes, dalga with { AudioVisual = AudioVisual.Cover }));
        Assert.Empty(ConversionArguments.Validate(Ses, dalga with { AudioVisual = AudioVisual.Cover, CoverPath = @"C:\k\kapak.png" }));
        Assert.Contains("Cover art must be a PNG or JPEG image.", ConversionArguments.Validate(Ses, dalga with { AudioVisual = AudioVisual.Cover, CoverPath = @"C:\k\kapak.bmp" }));
    }

    [Fact]
    public void DalgaBicimiKomutuSesSuresineBaglanir()
    {
        var plan = new ConversionPlan { Container = "mp4", AudioVisual = AudioVisual.Waveform, Start = TimeSpan.FromSeconds(10), End = TimeSpan.FromSeconds(25) };
        var args = ConversionArguments.Build(Ses, plan, @"C:\o\parca.mp4");
        var metin = string.Join(' ', args);

        Assert.Equal(15, ConversionArguments.AudioVideoSeconds(Ses, plan), 3);
        Assert.Equal(60, ConversionArguments.AudioVideoSeconds(Ses, new ConversionPlan()), 3);
        Assert.Equal(30, ConversionArguments.AudioVideoSeconds(Ses, new ConversionPlan { Start = TimeSpan.FromSeconds(30), End = TimeSpan.FromSeconds(90) }), 3);
        Assert.Equal("00:00:15.000", Deger(args, "-t"));
        Assert.Contains("[0:0]showwaves=s=1280x720:mode=cline:rate=25:colors=white,format=yuv420p[v]", metin);
        Assert.Equal(1, args.Count(a => a == "-i"));
        Assert.Equal(new[] { "[v]", "0:0" }, args.Select((a, i) => (a, i)).Where(x => x.a == "-map").Select(x => args[x.i + 1]).ToArray());
        Assert.Equal("libx264", Deger(args, "-c:v"));
        Assert.Equal("yuv420p", Deger(args, "-pix_fmt"));
        Assert.Equal(@"C:\o\parca.mp4", args[^1]);
        Assert.DoesNotContain("-vn", args);

        Assert.Equal((640, 360), ConversionArguments.AudioVideoSize(new ConversionPlan { Height = 360 }));
        Assert.Equal((500, 500), ConversionArguments.AudioVideoSize(new ConversionPlan { Width = 500, Height = 500 }));
        Assert.Contains("showwaves=s=640x360:mode=cline:rate=30", string.Join(' ', ConversionArguments.Build(Ses, plan with { Height = 360, Fps = 30 }, @"C:\o\p.mp4")));
    }

    [Fact]
    public void KapakIkinciGirdidenOkunurAramaKapagiAtmaz()
    {
        var plan = new ConversionPlan { Container = "mkv", AudioVisual = AudioVisual.Cover, Start = TimeSpan.FromSeconds(5) };
        var gomulu = ConversionArguments.Build(KapakliSes, plan, @"C:\o\parca.mkv");
        var girdiler = gomulu.Select((a, i) => (a, i)).Where(x => x.a == "-i").Select(x => x.i).ToArray();
        Assert.Equal(2, girdiler.Length);
        Assert.Equal("-ss", gomulu[girdiler[0] - 2]);
        Assert.NotEqual("-ss", gomulu[girdiler[1] - 2]);
        Assert.Equal(KapakliSes.FilePath, gomulu[girdiler[1] + 1]);
        Assert.Contains("[1:1]loop=loop=-1:size=1:start=0,fps=25,scale=1280:720:force_original_aspect_ratio=decrease,pad=1280:720", string.Join(' ', gomulu));
        Assert.Equal("00:00:55.000", Deger(gomulu, "-t"));

        var dosya = ConversionArguments.Build(KapakliSes, plan with { CoverPath = @"C:\k\kapak.jpg" }, @"C:\o\parca.mkv");
        var ikinci = dosya.Select((a, i) => (a, i)).Where(x => x.a == "-i").Select(x => x.i).Last();
        Assert.Equal(@"C:\k\kapak.jpg", dosya[ikinci + 1]);
        Assert.Equal(new[] { "-loop", "1" }, new[] { dosya[ikinci - 2], dosya[ikinci - 1] });
        Assert.Contains("[1:v:0]fps=25,", string.Join(' ', dosya));
        Assert.DoesNotContain("-attach", dosya);
    }

    [FfmpegFact]
    public async Task CiktininSuresiSesSuresineEsittir()
    {
        var klasor = Calisma();
        try
        {
            var ses = Path.Combine(klasor, "ton.mp3");
            var kapak = Path.Combine(klasor, "kapak.png");
            var kapakli = Path.Combine(klasor, "kapakli.mp3");
            await Kos(new[] { "-hide_banner", "-y", "-f", "lavfi", "-i", "sine=frequency=440:duration=2", "-c:a", "libmp3lame", "-b:a", "64k", ses });
            await Kos(new[] { "-hide_banner", "-y", "-f", "lavfi", "-i", "color=c=red:s=64x48:d=1", "-frames:v", "1", kapak });
            await Kos(new[] { "-hide_banner", "-y", "-i", ses, "-i", kapak, "-map", "0:a", "-map", "1:v", "-c:a", "copy", "-c:v", "png", "-disposition:v:0", "attached_pic", "-id3v2_version", "3", kapakli });

            var duz = await FfprobeClient.ProbeAsync(ses, allowAudioOnly: true);
            var gomulu = await FfprobeClient.ProbeAsync(kapakli, allowAudioOnly: true);
            Assert.True(gomulu.IsAudioOnly);
            Assert.Contains(gomulu.Streams, stream => stream.IsAttachedPicture);

            var kucuk = new ConversionPlan { Width = 320, Height = 180, Fps = 10 };
            var durumlar = new (MediaInfo Kaynak, ConversionPlan Plan, string Ad)[]
            {
                (duz, kucuk with { Container = "mp4", AudioVisual = AudioVisual.Waveform }, "dalga.mp4"),
                (duz, kucuk with { Container = "mkv", AudioVisual = AudioVisual.Cover, CoverPath = kapak }, "kapak.mkv"),
                (gomulu, kucuk with { Container = "mp4", AudioVisual = AudioVisual.Cover, Start = TimeSpan.FromSeconds(0.5) }, "gomulu.mp4"),
                (duz, kucuk with { Container = "webm", VideoCodec = "libvpx-vp9", AudioCodec = "libopus", AudioVisual = AudioVisual.Waveform, Start = TimeSpan.FromSeconds(0.5), End = TimeSpan.FromSeconds(1.5) }, "kirpik.webm")
            };
            var olcum = new List<string>();
            foreach (var (kaynak, plan, ad) in durumlar)
            {
                Assert.Empty(ConversionArguments.Validate(kaynak, plan));
                var hedef = Path.Combine(klasor, ad);
                await Kos(ConversionArguments.Build(kaynak, plan, hedef));
                var cikti = await FfprobeClient.ProbeAsync(hedef);
                var beklenen = ConversionArguments.AudioVideoSeconds(kaynak, plan);
                olcum.Add(string.Create(CultureInfo.InvariantCulture, $"{ad}: beklenen {beklenen:0.000} sn, olculen {cikti.DurationSeconds:0.000} sn, {cikti.Width}x{cikti.Height}"));
                Assert.False(cikti.IsAudioOnly, ad);
                Assert.True(cikti.HasAudio, ad);
                Assert.Equal((320, 180), (cikti.Width, cikti.Height));
                Assert.True(Math.Abs(cikti.DurationSeconds - beklenen) <= 0.1, olcum[^1]);
            }
            Assert.Equal(2, ConversionArguments.AudioVideoSeconds(duz, durumlar[0].Plan), 1);
            Assert.Equal(1, ConversionArguments.AudioVideoSeconds(duz, durumlar[3].Plan), 3);
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    [Fact]
    public void SesKaynagindaGoruntuSecimiMp4AcarVePlanaGirer()
    {
        var ayar = Path.Combine(Calisma(), "settings.json");
        var olcu = AppHost.Run(() =>
        {
            var window = new MainWindow { SettingsPathOverride = ayar };
            try
            {
                window.Prober = (yol, _) => Task.FromResult(Ses with { FilePath = yol });
                window.Tabs.SelectedItem = window.TabConvert;
                Bekle(window.OpenSourcesAsync(Ses.FilePath, Array.Empty<string>()));
                var panel = window.ConvertAudioVisualPanel.IsVisible;
                var once = window.CmbContainer.Items.OfType<ComboBoxItem>().Count(item => item.IsVisible && (string)item.Tag! == "mp4");
                window.CmbConvertAudioVisual.SelectedIndex = 2;
                var secili = (string)((ComboBoxItem)window.CmbContainer.SelectedItem!).Tag!;
                var plan = window.CurrentConversionPlan();
                var hazir = window.BtnConvert.IsEnabled;
                var durum = window.ConvertStatusForTest;
                window.CmbConvertAudioVisual.SelectedIndex = 0;
                var geri = (string)((ComboBoxItem)window.CmbContainer.SelectedItem!).Tag!;

                window.Prober = (yol, _) => Task.FromResult(Ses with { FilePath = yol, IsAudioOnly = false, Width = 1280, Height = 720, Fps = 30, VideoCodec = "h264" });
                Bekle(window.OpenSourcesAsync(@"C:\media\klip.mp4", Array.Empty<string>()));
                window.CmbConvertAudioVisual.SelectedIndex = 2;
                var videoPlan = window.CurrentConversionPlan().AudioVisual;
                var videoPanel = window.ConvertAudioVisualPanel.IsVisible;
                return (panel, once, secili, plan.AudioVisual, plan.Container, hazir, durum, geri, videoPlan, videoPanel);
            }
            finally { window.Close(); }
        });

        Assert.True(olcu.panel);
        Assert.Equal(0, olcu.once);
        Assert.Equal("mp4", olcu.secili);
        Assert.Equal(AudioVisual.Waveform, olcu.Item4);
        Assert.Equal("mp4", olcu.Container);
        Assert.True(olcu.hazir, olcu.durum);
        Assert.True(new ConversionPlan { Container = olcu.geri }.AudioOnly, olcu.geri);
        Assert.Equal(AudioVisual.None, olcu.videoPlan);
        Assert.False(olcu.videoPanel);
        try { Directory.Delete(Path.GetDirectoryName(ayar)!, true); } catch (IOException) { }
    }

    private static string Deger(IReadOnlyList<string> args, string anahtar) => args[args.ToList().IndexOf(anahtar) + 1];

    private static void Bekle(Task is_)
    {
        var saat = Stopwatch.StartNew();
        while (!is_.IsCompleted && saat.Elapsed.TotalSeconds < 30)
        {
            using var dilim = new CancellationTokenSource(TimeSpan.FromMilliseconds(5));
            Dispatcher.UIThread.MainLoop(dilim.Token);
        }
        is_.GetAwaiter().GetResult();
    }

    private static async Task Kos(IReadOnlyList<string> args)
    {
        var liste = args.ToList();
        liste.InsertRange(liste.Count - 1, new[] { "-threads", "2" });
        if (!liste.Contains("-nostdin")) liste.Insert(2, "-nostdin");
        var sonuc = await FfmpegRunner.RunAsync(liste);
        Assert.True(sonuc.Ok, sonuc.StandardError);
    }

    private static string Calisma()
    {
        var dizin = new DirectoryInfo(AppContext.BaseDirectory);
        while (dizin is not null && !File.Exists(Path.Combine(dizin.FullName, "VidShrink.sln"))) dizin = dizin.Parent;
        var kok = dizin?.FullName ?? throw new DirectoryNotFoundException("VidShrink.sln bulunamadi.");
        var klasor = Path.Combine(kok, ".calisma", "testler", "sesten-video-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(klasor);
        return klasor;
    }
}
