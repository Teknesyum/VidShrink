using System.Diagnostics;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Threading;
using VidShrink.App;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

/// <summary>
/// ARC-P3, ses dosyası ve GIF girdisi. Görüntüsü olmayan dosya "yalnız ses" kaynağı olarak okunur, Dönüştür bu
/// kaynakta yalnız ses kaplarını sunar, Küçült almaz. GIF bırakmada ve klasör taramasında alınır; kaynağı GIF
/// olan GIF çıktısı kare hızı, genişlik ve renk sayısı düşürülerek hedefin altına iner. Canlı kollar ffmpeg'in
/// ürettiği 1 sn mp3/flac/wav ve 320x240 GIF ile, <c>-threads 2</c>; klasör <c>.calisma/ses-gif-*</c>, test siler.
/// </summary>
public sealed class SesVeGifGirdisiTests
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

    private static readonly MediaInfo Video = new()
    {
        FilePath = @"C:\media\klip.mp4",
        FileSizeBytes = 5_000_000L,
        DurationSeconds = 10,
        Width = 1280,
        Height = 720,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 4_000_000,
        AudioCodec = "aac",
        AudioChannels = 2
    };

    private static readonly MediaInfo Gif = Video with { FilePath = @"C:\media\dans.gif", VideoCodec = "gif", Width = 480, Height = 360, Fps = 25, AudioCodec = null, AudioChannels = 0 };

    private const string GoruntuYok = "The source has no video stream; choose an audio container.";

    private static readonly string[] Kaplar ={ "mp4", "mkv", "webm", "mov", "avi", "mxf", "gif", "webp", "avif", "png", "jpg", "mp3", "m4a", "wav", "flac" };

    [Fact]
    public void GifKlasorTaramasindaVeBirakmadaAlinirSesKucultmeyeGirmez()
    {
        Assert.True(WatchFolder.IsCandidate(@"C:\x\dans.GIF"));
        Assert.False(WatchFolder.IsCandidate(@"C:\x\.gizli.gif"));
        Assert.False(WatchFolder.IsCandidate(@"C:\x\parca.mp3"));

        var dosyalar = new[] { @"C:\k\b.gif", @"C:\k\a.mp4", @"C:\k\parca.flac", @"C:\k\notlar.txt" };
        var klasor = @"C:\k";
        IReadOnlyList<string> Topla(bool ses) => DroppedMedia.Collect(new[] { klasor, @"C:\tek.wav" }, ses,
            yol => yol == klasor, yol => yol == @"C:\tek.wav", _ => dosyalar);

        Assert.Equal(new[] { @"C:\k\a.mp4", @"C:\k\b.gif" }, Topla(false));
        Assert.Equal(new[] { @"C:\k\a.mp4", @"C:\k\b.gif", @"C:\k\parca.flac", @"C:\tek.wav" }, Topla(true));
    }

    [Fact]
    public void SesKaynagindaYalnizSesKaplariSecilir()
    {
        foreach (var kap in Kaplar)
        {
            var plan = new ConversionPlan { Container = kap, AudioCodec = ConversionPresetPlan.AudioCodecForAudioContainer(kap) };
            Assert.Equal(plan.AudioOnly, ConversionArguments.ContainerFitsSource(kap, Ses));
            Assert.True(ConversionArguments.ContainerFitsSource(kap, Video), kap);
            Assert.True(ConversionArguments.ContainerFitsSource(kap, null), kap);
            Assert.Equal(!plan.AudioOnly, ConversionArguments.Validate(Ses, plan).Contains(GoruntuYok));
            Assert.DoesNotContain(GoruntuYok, ConversionArguments.Validate(Video, plan));
        }

        var flac = ConversionArguments.Build(Ses, new ConversionPlan { Container = "flac", AudioCodec = "flac" }, @"C:\o\parca.flac");
        Assert.Contains("-vn", flac);
        Assert.DoesNotContain("-c:v", flac);
    }

    [Fact]
    public void GifMerdiveniHerBasamaktaKuculur()
    {
        var merdiven = GifPalette.Ladder(25, 480, 360);

        Assert.Equal(new GifStep(25, 480, 360, 256), merdiven[0]);
        Assert.True(merdiven.Count >= 6, merdiven.Count.ToString(CultureInfo.InvariantCulture));
        for (var i = 1; i < merdiven.Count; i++)
        {
            var (once, simdi) = (merdiven[i - 1], merdiven[i]);
            Assert.True(simdi.Fps <= once.Fps && simdi.Width <= once.Width && simdi.Height <= once.Height && simdi.Colors <= once.Colors, simdi.ToString());
            Assert.True(GifPalette.Weight(simdi) < GifPalette.Weight(once), simdi.ToString());
            Assert.True(simdi.Width % 2 == 0 && simdi.Height % 2 == 0, simdi.ToString());
        }
        Assert.True(merdiven[^1].Fps < 25 && merdiven[^1].Width < 480 && merdiven[^1].Colors < 256, merdiven[^1].ToString());

        Assert.Equal(new GifStep(10, 480, 360, 256), GifPalette.Ladder(Gif, new ConversionPlan { Container = "gif", Fps = 10 })[0]);
        Assert.Equal(new GifStep(25, 320, 240, 256), GifPalette.Ladder(Gif, new ConversionPlan { Container = "gif", Height = 240 })[0]);
    }

    [Fact]
    public void SonrakiBasamakTahminleAtlarAzAsimdaBirIner()
    {
        var merdiven = GifPalette.Ladder(25, 480, 360);
        var hedef = 1_000_000L;

        Assert.Equal(1, GifPalette.NextStep(merdiven, 0, 1_010_000, hedef));
        var atlanan = GifPalette.NextStep(merdiven, 0, 8_000_000, hedef);
        Assert.True(atlanan > 1, atlanan.ToString(CultureInfo.InvariantCulture));
        Assert.True(8_000_000 * GifPalette.Weight(merdiven[atlanan]) / GifPalette.Weight(merdiven[0]) <= hedef);
        Assert.True(8_000_000 * GifPalette.Weight(merdiven[atlanan - 1]) / GifPalette.Weight(merdiven[0]) > hedef);
        Assert.Equal(merdiven.Count - 1, GifPalette.NextStep(merdiven, 0, 900_000_000, hedef));
    }

    [Fact]
    public async Task SigdirmaIlkSigandaDururSigmazsaSonBasamagiTeslimEder()
    {
        var plan = new ConversionPlan { Container = "gif", TargetBytes = 1_000_000 };
        var denenen = new List<string>();
        long Boyut(string yol) => long.Parse(Path.GetFileNameWithoutExtension(yol), CultureInfo.InvariantCulture);

        var kucuk = await GifPalette.FitAsync(Gif, plan, @"C:\o\800000.gif", @"C:\o\p.png",
            (args, palet) => { denenen.Add(palet ? "p" : "g"); return Task.CompletedTask; }, Boyut);
        Assert.True(kucuk.Bytes <= plan.TargetBytes);
        Assert.Equal(1, kucuk.Attempts);
        Assert.Equal(new[] { "p", "g" }, denenen);

        var boyutlar = new Queue<long>(new[] { 3_000_000L, 1_200_000L, 990_000L });
        var adimlar = new List<GifStep>();
        var sonuc = await GifPalette.FitAsync(Gif, plan, @"C:\o\c.gif", @"C:\o\p.png",
            (args, palet) =>
            {
                if (palet) adimlar.Add(new GifStep(0, 0, 0, int.Parse(Deger(args, "max_colors"), CultureInfo.InvariantCulture)));
                else Assert.Contains(args, a => a.Contains("paletteuse", StringComparison.Ordinal));
                return Task.CompletedTask;
            }, _ => boyutlar.Dequeue());
        Assert.True(sonuc.Bytes <= plan.TargetBytes);
        Assert.Equal(3, sonuc.Attempts);
        Assert.Equal(990_000L, sonuc.Bytes);
        Assert.Equal(256, adimlar[0].Colors);
        Assert.True(adimlar[2].Colors < 256);

        var sigmaz = await GifPalette.FitAsync(Gif, plan, @"C:\o\c.gif", @"C:\o\p.png", (_, _) => Task.CompletedTask, _ => 5_000_000_000);
        Assert.True(sigmaz.Bytes > plan.TargetBytes);
        Assert.Equal(GifPalette.Ladder(Gif, plan)[^1], sigmaz.Step);
    }

    [Fact]
    public void GifHedefiYalnizGifKaynaktanGifCiktiyaVerilir()
    {
        Assert.Equal(Megabayt.Tavan(8), GifPalette.TargetFor(Gif, "gif", 8));
        Assert.Null(GifPalette.TargetFor(Video, "gif", 8));
        Assert.Null(GifPalette.TargetFor(Gif, "mp4", 8));
        Assert.Null(GifPalette.TargetFor(Gif, "gif", 0));
        Assert.True(Gif.IsGifSource);
        Assert.False(Video.IsGifSource);

        var hedefli = new ConversionPlan { Container = "gif", TargetBytes = 1_000_000 };
        Assert.Contains(ConversionNote.GifFitsTarget, ConversionArguments.Notes(hedefli));
        Assert.DoesNotContain(ConversionNote.GifFitsTarget, ConversionArguments.Notes(new ConversionPlan { Container = "gif" }));
        Assert.DoesNotContain(ConversionNote.GifFitsTarget, ConversionArguments.Notes(new ConversionPlan { Container = "mp4", TargetBytes = 1_000_000 }));

        var palet = ConversionArguments.Build(Gif, new ConversionPlan { Container = "gif", GifColors = 64 }, @"C:\o\p.png");
        Assert.Contains("palettegen=stats_mode=diff:max_colors=64", string.Join(' ', palet));
        Assert.DoesNotContain("max_colors", string.Join(' ', ConversionArguments.Build(Gif, new ConversionPlan { Container = "gif" }, @"C:\o\p.png")));
    }

    [Fact]
    public void SesKaynagiDonusturdeYalnizSesKabiSunarKucultAlmaz()
    {
        var ayar = Path.Combine(Calisma("ayar"), "settings.json");
        var olcu = AppHost.Run(() =>
        {
            var window = new MainWindow { SettingsPathOverride = ayar };
            try
            {
                window.Prober = (yol, _) => Task.FromResult(Ses with { FilePath = yol });
                window.Tabs.SelectedItem = window.TabConvert;
                Bekle(window.OpenSourcesAsync(Ses.FilePath, Array.Empty<string>()));
                var gorunen = window.CmbContainer.Items.OfType<ComboBoxItem>().Where(item => item.IsVisible).Select(item => (string)item.Tag!).ToArray();
                var secili = (string)((ComboBoxItem)window.CmbContainer.SelectedItem!).Tag!;
                var olcum = (gorunen, secili, window.BtnStart.IsEnabled, window.BtnConvert.IsEnabled, window.SourceStatusText, window.ConvertStatusForTest);

                window.Prober = (yol, _) => Task.FromResult(Video with { FilePath = yol });
                Bekle(window.OpenSourcesAsync(Video.FilePath, Array.Empty<string>()));
                var sonra = window.CmbContainer.Items.OfType<ComboBoxItem>().Count(item => item.IsVisible);
                var sonraSecili = (string)((ComboBoxItem)window.CmbContainer.SelectedItem!).Tag!;
                return (olcum, sonra, sonraSecili);
            }
            finally { window.Close(); }
        });

        Assert.Equal(new[] { "ac3", "aiff", "flac", "m4a", "mp3", "ogg", "opus", "wav" }, olcu.olcum.gorunen.Order().ToArray());
        Assert.All(olcu.olcum.gorunen, kap => Assert.True(new ConversionPlan { Container = kap }.AudioOnly, kap));
        Assert.True(new ConversionPlan { Container = olcu.olcum.secili }.AudioOnly, olcu.olcum.secili);
        Assert.False(olcu.olcum.Item3);
        Assert.True(olcu.olcum.Item4, olcu.olcum.Item6);
        Assert.Equal(VidShrink.App.Localization.Strings.Get("main.error.audio-only-shrink"), olcu.olcum.Item5);
        Assert.True(olcu.sonra > 8, olcu.sonra.ToString(CultureInfo.InvariantCulture));
        Assert.Equal("mp4", olcu.sonraSecili);
        try { Directory.Delete(Path.GetDirectoryName(Path.GetDirectoryName(ayar))!, true); } catch (IOException) { }
    }

    [FfmpegFact]
    public async Task SesDosyalariYalnizSesKaynagiOlarakOkunupCevrilir()
    {
        var klasor = Calisma("ses");
        try
        {
            var cevrim = new[] { ("mp3", "flac", "flac"), ("flac", "wav", "pcm_s16le"), ("wav", "mp3", "libmp3lame"), ("mp3", "m4a", "aac") };
            foreach (var (giris, cikis, kodek) in cevrim)
            {
                var kaynak = Path.Combine(klasor, "ton." + giris);
                if (!File.Exists(kaynak))
                    await Kos(new[] { "-hide_banner", "-y", "-f", "lavfi", "-i", "sine=frequency=440:duration=1", kaynak });
                var info = await FfprobeClient.ProbeAsync(kaynak, allowAudioOnly: true);
                Assert.True(info.IsAudioOnly, giris);
                Assert.True(info.HasAudio, giris);
                Assert.Equal(0, info.Width);
                Assert.Equal(1.0, info.DurationSeconds, 0);
                Assert.False(ConversionArguments.ContainerFitsSource("mp4", info));

                var plan = new ConversionPlan { Container = cikis, AudioCodec = ConversionPresetPlan.AudioCodecForAudioContainer(cikis) };
                Assert.Empty(ConversionArguments.Validate(info, plan));
                var hedef = Path.Combine(klasor, giris + "-" + cikis + "." + cikis);
                await Kos(ConversionArguments.Build(info, plan, hedef));
                var cikti = await FfprobeClient.ProbeAsync(hedef, allowAudioOnly: true);
                Assert.True(cikti.IsAudioOnly, hedef);
                Assert.Equal(kodek == "libmp3lame" ? "mp3" : kodek, cikti.AudioCodec);
                Assert.NotEmpty(ConversionArguments.Validate(info, plan with { Container = "mkv", AudioCodec = "aac" }));
            }

            var kucultYolu = await Assert.ThrowsAsync<InvalidOperationException>(() => FfprobeClient.ProbeAsync(Path.Combine(klasor, "ton.mp3")));
            Assert.Contains("no video stream", kucultYolu.Message, StringComparison.Ordinal);

            var altyazi = Path.Combine(klasor, "yalniz.srt");
            File.WriteAllText(altyazi, "1\n00:00:00,000 --> 00:00:01,000\nmerhaba\n");
            var hata = await Assert.ThrowsAsync<InvalidOperationException>(() => FfprobeClient.ProbeAsync(altyazi, allowAudioOnly: true));
            Assert.Contains("no video stream", hata.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    [FfmpegFact]
    public async Task GifKaynagiHedefinAltinaIner()
    {
        var klasor = Calisma("gif");
        try
        {
            var kaynak = Path.Combine(klasor, "dans.gif");
            await Kos(new[] { "-hide_banner", "-y", "-f", "lavfi", "-i", "testsrc2=size=320x240:rate=20:duration=3", kaynak });
            Assert.Contains(kaynak, DroppedMedia.Collect(new[] { klasor }));
            var info = await FfprobeClient.ProbeAsync(kaynak);
            Assert.True(info.IsGifSource);
            Assert.False(info.IsAudioOnly);

            var tamPlan = new ConversionPlan { Container = "gif" };
            var tam = Path.Combine(klasor, "tam.gif");
            var palet = Path.Combine(klasor, "palet.png");
            await GifPalette.FitAsync(info, tamPlan with { TargetBytes = long.MaxValue }, tam, palet, (args, _) => Kos(args), yol => new FileInfo(yol).Length);
            var tamBoyut = new FileInfo(tam).Length;

            var hedef = tamBoyut * 3 / 10;
            var cikti = Path.Combine(klasor, "hedef.gif");
            var sonuc = await GifPalette.FitAsync(info, tamPlan with { TargetBytes = hedef }, cikti, palet, (args, _) => Kos(args), yol => new FileInfo(yol).Length);

            var olcu = new FileInfo(cikti).Length;
            Assert.True(sonuc.Bytes <= hedef, $"tam={tamBoyut} hedef={hedef} cikti={olcu} adim={sonuc.Step}");
            Assert.True(olcu <= hedef, $"tam={tamBoyut} hedef={hedef} cikti={olcu}");
            Assert.Equal(olcu, sonuc.Bytes);
            Assert.True(sonuc.Attempts >= 2, sonuc.Attempts.ToString(CultureInfo.InvariantCulture));
            var gifCikti = await FfprobeClient.ProbeAsync(cikti);
            Assert.Equal("gif", gifCikti.VideoCodec);
            Assert.Equal(sonuc.Step.Width, gifCikti.Width);
            File.WriteAllText(Path.Combine(KokDizin(), ".calisma", "worktree-agent-ae9d637ae03a285dd", "ses-gif-olcum.txt"),
                FormattableString.Invariant($"tam={tamBoyut} hedef={hedef} cikti={olcu} deneme={sonuc.Attempts} adim={sonuc.Step}"));
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    private static string Deger(IReadOnlyList<string> args, string anahtar)
    {
        var metin = string.Join(' ', args);
        var yer = metin.IndexOf(anahtar + "=", StringComparison.Ordinal);
        Assert.True(yer >= 0, anahtar + " yok: " + metin);
        var bas = yer + anahtar.Length + 1;
        var son = bas;
        while (son < metin.Length && (char.IsDigit(metin[son]) || metin[son] == '.')) son++;
        return metin[bas..son];
    }

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

    private static string Calisma(string ad)
    {
        var klasor = Path.Combine(KokDizin(), ".calisma", "worktree-agent-ae9d637ae03a285dd", "ses-gif-" + ad + "-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(klasor);
        return klasor;
    }

    private static string KokDizin()
    {
        var dizin = new DirectoryInfo(AppContext.BaseDirectory);
        while (dizin is not null && !File.Exists(Path.Combine(dizin.FullName, "VidShrink.sln"))) dizin = dizin.Parent;
        return dizin?.FullName ?? throw new DirectoryNotFoundException("VidShrink.sln bulunamadi.");
    }
}
