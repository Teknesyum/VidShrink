using System.Globalization;
using VidShrink.Cli;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit.Abstractions;
using static VidShrink.Tests.B1KalanGirdi;

namespace VidShrink.Tests;

/// <summary>
/// Ses ornekleme hizi (<c>--ses-hizi</c>, HandBrake <c>--arate</c>) ve AC-3/E-AC-3 cozucusunun
/// dinamik aralik olcegi (<c>--ses-drc</c>, HandBrake <c>--drc</c>). Ikisi de var olan ses yolundan
/// gecer: <see cref="StreamRequest"/> → <see cref="StreamMapping.Decide"/> → <see cref="AudioTrack"/>.
/// Istek izi gercekten degistiriyorsa kopya yeniden kodlamaya doner, degistirmiyorsa kopya kalir.
/// Kodlayicinin yazamadigi hiz komutu durdurur. Olcum: <c>docs/olcumler/ses-ornekleme-drc.md</c>.
/// </summary>
public sealed class SesOrneklemeDrcTests(ITestOutputHelper cikti)
{
    private static readonly CliText Tr = CliText.ForLanguage("tr");
    private static readonly CliText En = CliText.ForLanguage("en");

    private static SourceStream Ses(string kodek = "aac", int ornekleme = 48000, int kanal = 2, int sira = 1)
        => new(sira, StreamKind.Audio, kodek, "eng", Channels: kanal, BitrateBps: 128_000, SampleRate: ornekleme);

    private static string Klasor
    {
        get
        {
            var yol = Path.Combine(GirdiKanit.Root, ".calisma", "ses-ornekleme-drc");
            Directory.CreateDirectory(yol);
            return yol;
        }
    }

    private static StreamPlan Karar(StreamRequest istek, OutputContainer kap = OutputContainer.Mp4, string kodek = "aac",
        params SourceStream[] sesler)
        => StreamMapping.Decide(Kaynak(600, new[] { Video }.Concat(sesler.Length == 0 ? new[] { Ses() } : sesler).ToArray()),
            istek, kap, 160, null, kodek, true, 1000);

    private static CliParseResult Ayristir(params string[] ek)
        => CliParser.Parse(new[] { "kucult", "a.mp4", "--hedef", "25MB" }.Concat(ek).ToArray());

    [Theory]
    [InlineData("--ses-hizi", "44.1", 44100)]
    [InlineData("--arate", "48", 48000)]
    [InlineData("--arate", "22.05", 22050)]
    [InlineData("--ses-hizi", "11.025", 11025)]
    [InlineData("--arate", "8", 8000)]
    [InlineData("--ses-hizi", "44100", 44100)]
    [InlineData("--arate", "12000", 12000)]
    public void HizBayragiHzOlarakPlanaIniyor(string bayrak, string deger, int beklenen)
    {
        var sonuc = Ayristir(bayrak, deger);

        Assert.True(sonuc.Ok, sonuc.ErrorKey);
        Assert.Equal(beklenen, sonuc.Request!.AudioSampleRate);
        Assert.Equal(beklenen, sonuc.Request.ToPlanOptions(25).AudioSampleRate);
        Assert.Null(Ayristir().Request!.ToPlanOptions(25).AudioSampleRate);
    }

    [Theory]
    [InlineData("auto")]
    [InlineData("AUTO")]
    public void AutoHiziKaynagaBirakiyor(string deger)
    {
        var sonuc = Ayristir("--ses-hizi", "8", "--arate", deger);

        Assert.True(sonuc.Ok, sonuc.ErrorKey);
        Assert.Null(sonuc.Request!.AudioSampleRate);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("44")]
    [InlineData("96")]
    [InlineData("44101")]
    [InlineData("abc")]
    public void ListedeOlmayanHizReddediliyor(string deger)
    {
        var sonuc = Ayristir("--arate", deger);

        Assert.Equal("error.bad-arate", sonuc.ErrorKey);
        Assert.Equal(deger, sonuc.ErrorArgument);
    }

    [Theory]
    [InlineData("--ses-drc", "2.5", 2.5)]
    [InlineData("--drc", "0", 0)]
    [InlineData("--drc", "4", 4)]
    [InlineData("--ses-drc", "1", 1)]
    public void DrcBayragiPlanaIniyor(string bayrak, string deger, double beklenen)
    {
        var sonuc = Ayristir(bayrak, deger);

        Assert.True(sonuc.Ok, sonuc.ErrorKey);
        Assert.Equal(beklenen, sonuc.Request!.AudioDrcScale);
        Assert.Equal(beklenen, sonuc.Request.ToPlanOptions(25).AudioDrcScale);
        Assert.Null(Ayristir().Request!.ToPlanOptions(25).AudioDrcScale);
    }

    [Theory]
    [InlineData("4.01")]
    [InlineData("5")]
    [InlineData("x")]
    public void SinirDisiDrcReddediliyor(string deger)
    {
        var sonuc = Ayristir("--drc", deger);

        Assert.Equal("error.bad-drc", sonuc.ErrorKey);
        Assert.Equal(deger, sonuc.ErrorArgument);
    }

    [Theory]
    [InlineData("--ses-hizi", "48")]
    [InlineData("--arate", "48")]
    [InlineData("--ses-drc", "1")]
    [InlineData("--drc", "1")]
    public void DegersizBayrakVeIzleKomutu(string bayrak, string deger)
    {
        Assert.Equal("error.missing-value", Ayristir(bayrak).ErrorKey);
        Assert.Equal("error.not-in-watch",
            CliParser.Parse(new[] { "izle", "k", "--cikti", "c", "--hedef", "25MB", bayrak, deger }).ErrorKey);
        Assert.Contains(bayrak, CliParser.NotInWatch);
    }

    [Fact]
    public void FarkliHizKopyayiYenidenKodlamayaCeviriyorAyniHizKopyaliyor()
    {
        var farkli = Karar(new StreamRequest(AudioSampleRate: 44100));
        var ayni = Karar(new StreamRequest(AudioSampleRate: 48000));
        var isteksiz = Karar(StreamRequest.Default);

        Assert.False(farkli.Audio[0].Copies);
        Assert.Equal(44100, farkli.Audio[0].SampleRate);
        var args = farkli.OutputArguments().ToList();
        Assert.Equal("44100", Sonraki(args, "-ar"));
        Assert.True(args.IndexOf("-ar") > args.IndexOf("-filter:a"), string.Join(' ', args));

        Assert.True(ayni.Audio[0].Copies);
        Assert.True(isteksiz.Audio[0].Copies);
        Assert.DoesNotContain("-ar", ayni.OutputArguments());
        Assert.DoesNotContain("-ar", isteksiz.OutputArguments());
    }

    [Fact]
    public void IkiIzdeHizIzBasinaYaziliyor()
    {
        var plan = Karar(new StreamRequest(KeepAllTracks: true, AudioSampleRate: 48000), OutputContainer.Mkv, "aac",
            Ses(ornekleme: 44100, sira: 1), Ses(ornekleme: 44100, sira: 2));
        var args = plan.OutputArguments();

        Assert.Equal(2, plan.Audio.Count);
        Assert.Equal("48000", Sonraki(args, "-ar:a:0"));
        Assert.Equal("48000", Sonraki(args, "-ar:a:1"));
        Assert.DoesNotContain("-ar", args);
    }

    [Theory]
    [InlineData(2.0, "2")]
    [InlineData(2.5, "2.5")]
    [InlineData(0.0, "0")]
    public void DrcYalnizDolbyKaynaginCozucusuneYaziliyor(double olcek, string yazim)
    {
        var dolby = Karar(new StreamRequest(AudioDrcScale: olcek), OutputContainer.Mp4, "aac", Ses("ac3"));
        var aac = Karar(new StreamRequest(AudioDrcScale: olcek));
        var isteksiz = Karar(StreamRequest.Default, OutputContainer.Mp4, "aac", Ses("ac3"));

        Assert.False(dolby.Audio[0].Copies);
        Assert.Equal(olcek, dolby.Audio[0].DrcScale);
        Assert.Equal(new[] { "-drc_scale:1", yazim }, dolby.InputArguments());

        Assert.True(aac.Audio[0].Copies);
        Assert.Empty(aac.InputArguments());
        Assert.True(isteksiz.Audio[0].Copies);
        Assert.Empty(isteksiz.InputArguments());
    }

    [Fact]
    public void KarisikIzlerdeDrcYalnizDolbyIzininBelirteciyle()
    {
        var plan = Karar(new StreamRequest(KeepAllTracks: true, AudioDrcScale: 3), OutputContainer.Mkv, "aac",
            Ses("aac", sira: 1), Ses("eac3", sira: 2));

        Assert.Equal(new[] { "-drc_scale:2", "3" }, plan.InputArguments());
        Assert.True(plan.Audio[0].Copies);
        Assert.False(plan.Audio[1].Copies);
        var hizli = Karar(new StreamRequest(AudioSampleRate: 44100, AudioDrcScale: 3));
        Assert.False(hizli.Audio[0].Copies);
        Assert.Empty(hizli.InputArguments());
        Assert.Equal("a:0", new AudioTrack("0:a:0?", TrackAction.Encode, "aac", 96, null, null).InputSpecifier);
    }

    [Fact]
    public void DrcGirdidenOnceVeIkiGecisteAyni()
    {
        var info = Kaynak(600, Video, Ses("ac3"));
        var plan = PlanCalculator.Build(info, new PlanOptions { TargetMb = 25, AudioDrcScale = 2 });
        var isteksiz = PlanCalculator.Build(info, new PlanOptions { TargetMb = 25 });

        foreach (var gecis in new[] { 0, 2 })
        {
            var args = FfmpegArguments.Build(info, plan, "cikti.mp4", gecis, gecis == 0 ? null : "gecis").ToList();
            var drc = args.IndexOf("-drc_scale:1");
            Assert.True(drc >= 0 && drc < args.IndexOf("-i"), string.Join(' ', args));
            Assert.Equal("2", args[drc + 1]);
        }
        Assert.DoesNotContain(FfmpegArguments.Build(info, isteksiz, "cikti.mp4", 0, null), arg => arg.StartsWith("-drc_scale", StringComparison.Ordinal));
    }

    [Fact]
    public void KodlayicininYazamadigiHizAdiylaDonuyor()
    {
        Assert.Equal(new[] { 32000, 44100, 48000 }, StreamMapping.SampleRatesFor("ac3"));
        Assert.Equal(new[] { 32000, 44100, 48000 }, StreamMapping.SampleRatesFor("eac3"));
        Assert.Equal(new[] { 48000 }, StreamMapping.SampleRatesFor("libopus"));
        Assert.Null(StreamMapping.SampleRatesFor("aac"));
        Assert.Null(StreamMapping.SampleRatesFor("flac"));

        Assert.Equal("ac3", Karar(new StreamRequest(AudioSampleRate: 22050, ExplicitAudioCodec: true), OutputContainer.Mp4, "ac3").RejectedSampleRate()!.Codec);
        Assert.Null(Karar(new StreamRequest(AudioSampleRate: 44100, ExplicitAudioCodec: true), OutputContainer.Mp4, "ac3").RejectedSampleRate());
        Assert.Equal("libopus", Karar(new StreamRequest(AudioSampleRate: 24000), OutputContainer.Mkv).RejectedSampleRate()!.Codec);
        Assert.Null(Karar(new StreamRequest(AudioSampleRate: 48000), OutputContainer.Mkv, "aac", Ses(ornekleme: 44100)).RejectedSampleRate());
        Assert.Null(Karar(new StreamRequest(AudioSampleRate: 8000)).RejectedSampleRate());
        Assert.Null(Karar(StreamRequest.Default, OutputContainer.Mkv).RejectedSampleRate());
    }

    [Theory]
    [InlineData(8000, 2, 42)]
    [InlineData(8000, 1, 37)]
    [InlineData(11025, 2, 58)]
    [InlineData(24000, 2, 125)]
    [InlineData(24000, 1, 111)]
    [InlineData(32000, 2, null)]
    [InlineData(48000, 2, null)]
    [InlineData(8000, 6, null)]
    public void AacTavaniHizVeKanaldan(int hiz, int kanal, int? beklenen)
        => Assert.Equal(beklenen, StreamMapping.AacCeilingK(hiz, kanal));

    [Fact]
    public void AacDusukHizdaIzKelepceleniyorFarkVideoyaKaliyor()
    {
        var dusuk = Karar(new StreamRequest(AudioSampleRate: 8000));
        var yuksek = Karar(new StreamRequest(AudioSampleRate: 32000));

        Assert.Equal(42, dusuk.Audio[0].BitrateK);
        Assert.Equal(160, yuksek.Audio[0].BitrateK);
        Assert.Equal(118, yuksek.SideK - dusuk.SideK, 3);

        var info = Kaynak(600, Video, Ses());
        var dar = PlanCalculator.Build(info, new PlanOptions { TargetMb = 25, AudioSampleRate = 8000, LockedAudioKbps = 128 });
        var genis = PlanCalculator.Build(info, new PlanOptions { TargetMb = 25, AudioSampleRate = 32000, LockedAudioKbps = 128 });
        Assert.Equal(42, dar.Streams!.Audio[0].BitrateK);
        Assert.Equal(128, genis.Streams!.Audio[0].BitrateK);
        Assert.True(dar.VideoBitrateK > genis.VideoBitrateK, $"{dar.VideoBitrateK} / {genis.VideoBitrateK}");
    }

    [Fact]
    public void FlacTavaniVeLoudnormSonuIstenenHizdan()
    {
        var flac = StreamMapping.Decide(Kaynak(600, Video, Ses()), new StreamRequest(AudioSampleRate: 16000),
            OutputContainer.Mkv, 160, null, "flac", false, 1000);
        var kaynakHizinda = StreamMapping.Decide(Kaynak(600, Video, Ses()), StreamRequest.Default,
            OutputContainer.Mkv, 160, null, "flac", false, 1000);

        Assert.Equal(512, flac.Audio[0].BitrateK);
        Assert.Equal(1536, kaynakHizinda.Audio[0].BitrateK);
        Assert.EndsWith("aresample=22050", StreamMapping.AudioFilter(new StreamRequest(AudioLoudnorm: true, AudioSampleRate: 22050), 48000));
        Assert.EndsWith("aresample=48000", StreamMapping.AudioFilter(new StreamRequest(AudioLoudnorm: true), 48000));
    }

    [Fact]
    public void IstekTamKopyayiYalnizIziDegistiriyorsaKesiyor()
    {
        var aac = Kaynak(600, Video, Ses()) with { FileSizeBytes = 5_000_000 };
        var dolby = Kaynak(600, Video, Ses("ac3")) with { FileSizeBytes = 5_000_000 };
        EncodeMode Kip(MediaInfo info, Action<PlanOptions> ayar)
        {
            var options = new PlanOptions { TargetMb = 25 };
            ayar(options);
            return PlanCalculator.Build(info, options).ModeEnum;
        }

        Assert.Equal(EncodeMode.PassThrough, Kip(aac, _ => { }));
        Assert.NotEqual(EncodeMode.PassThrough, Kip(aac, options => options.AudioSampleRate = 44100));
        Assert.Equal(EncodeMode.PassThrough, Kip(aac, options => options.AudioSampleRate = 48000));
        Assert.Equal(EncodeMode.PassThrough, Kip(aac, options => options.AudioDrcScale = 2));
        Assert.NotEqual(EncodeMode.PassThrough, Kip(dolby, options => options.AudioDrcScale = 2));
        Assert.Equal(EncodeMode.PassThrough, Kip(dolby, _ => { }));

        Assert.True(StreamMapping.AudioRequestChanges(aac, 44100, null));
        Assert.False(StreamMapping.AudioRequestChanges(aac, 48000, 2));
        Assert.False(StreamMapping.AudioRequestChanges(Kaynak(600, Video), 44100, 2));
    }

    [Fact]
    public void HedefDegisinceIstekTasiniyor()
    {
        var tasinan = PlanCalculator.WithTarget(new PlanOptions { TargetMb = 25, AudioSampleRate = 22050, AudioDrcScale = 1.5 }, 10);

        Assert.Equal(22050, tasinan.AudioSampleRate);
        Assert.Equal(1.5, tasinan.AudioDrcScale);
        Assert.Null(PlanCalculator.WithTarget(new PlanOptions { TargetMb = 25 }, 10).AudioSampleRate);
    }

    private static async Task<(int Kod, string Out, string Err)> PlanAsync(MediaInfo info, CliText dil, params string[] ek)
    {
        var ad = $"plan-{Guid.NewGuid():N}.mkv";
        var kaynak = Path.Combine(Klasor, ad);
        File.WriteAllText(kaynak, "x");
        try
        {
            var args = new List<string> { "plan", kaynak, "--hedef", "25MB", "--olcumsuz" };
            args.AddRange(ek);
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var kod = await CliApp.RunAsync(args, stdout, stderr, dil,
                new CliServices
                {
                    MissingTool = () => null,
                    Probe = (_, _) => Task.FromResult(info with { FilePath = kaynak }),
                    Availability = () => null
                },
                CancellationToken.None);
            return (kod, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            KanitKapanisi.Kapat(Klasor, ad);
        }
    }

    [Theory]
    [InlineData("ac3", "8", ExitCodes.Usage, true)]
    [InlineData("eac3", "24", ExitCodes.Usage, true)]
    [InlineData("ac3", "44.1", ExitCodes.InBand, false)]
    [InlineData("aac", "8", ExitCodes.InBand, false)]
    public async Task YazilamayanHizdaKomutDuruyor(string kodek, string hiz, int beklenen, bool ileti)
    {
        var (kod, _, err) = await PlanAsync(Kaynak(600, Video, Ses()), Tr, "--ses-kodek", kodek, "--ses-hizi", hiz);

        Assert.Equal(beklenen, kod);
        Assert.Equal(ileti, err.Contains("32000, 44100, 48000", StringComparison.Ordinal));
        Assert.Equal(ileti, err.Contains(kodek + " kodlayıcısı", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MatroskadaOpusYalnizKirkSekizKabulEdiyor()
    {
        var info = Kaynak(600, Video, Ses(ornekleme: 44100));
        var (red, _, redErr) = await PlanAsync(info, En, "--arate", "24", "--cikti", Path.Combine(Klasor, "opus-red.mkv"));
        var (kabul, _, kabulErr) = await PlanAsync(info, En, "--arate", "48", "--cikti", Path.Combine(Klasor, "opus-kabul.mkv"));

        Assert.Equal(ExitCodes.Usage, red);
        Assert.Contains("libopus encoder cannot write 24000 Hz", redErr);
        Assert.Equal(ExitCodes.InBand, kabul);
        Assert.DoesNotContain("cannot write", kabulErr);
    }

    [Fact]
    public async Task KelepceVeEtkisizDrcPlanMetnindeSoyleniyor()
    {
        var aac = Kaynak(600, Video, Ses());
        var (_, dusuk, _) = await PlanAsync(aac, Tr, "--ses-hizi", "8");
        var (_, yuksek, _) = await PlanAsync(aac, Tr, "--ses-hizi", "32");
        var (_, drcAac, _) = await PlanAsync(aac, Tr, "--ses-drc", "2");
        var (_, drcDolby, _) = await PlanAsync(Kaynak(600, Video, Ses("ac3")), Tr, "--ses-drc", "2");
        var (_, sessiz, _) = await PlanAsync(Kaynak(600, Video), Tr, "--ses-hizi", "8", "--ses-drc", "2");
        var (_, isteksiz, _) = await PlanAsync(aac, Tr);

        Assert.Contains(Tr.Format("note.arate-bitrate-capped", 8000, 42), dusuk);
        Assert.DoesNotContain("yazabiliyor", yuksek);
        Assert.Contains(Tr.Format("note.drc-not-dolby", "aac"), drcAac);
        Assert.DoesNotContain("--ses-drc", drcDolby);
        Assert.Contains(Tr["note.arate-no-audio"], sessiz);
        Assert.Contains(Tr["note.drc-no-audio"], sessiz);
        Assert.DoesNotContain("--ses-hizi", isteksiz);
        Assert.DoesNotContain("--ses-drc", isteksiz);
    }

    [Fact]
    public void YardimVeIletilerIkiDilde()
    {
        foreach (var dil in new[] { Tr, En })
        {
            Assert.Contains("--ses-hizi", dil["help"]);
            Assert.Contains("--ses-drc", dil["help"]);
            Assert.Contains("7", dil.Format("error.bad-arate", "7"));
            Assert.Contains("9", dil.Format("error.bad-drc", "9"));
            Assert.Contains("--ses-hizi", dil["note.arate-no-audio"]);
            Assert.Contains("--ses-drc", dil["note.drc-no-audio"]);
            Assert.Contains("opus", dil.Format("note.drc-not-dolby", "opus"));
        }
        Assert.Contains("--arate", En["help"]);
        Assert.Contains("--drc", En["help"]);
        foreach (var anahtar in new[]
                 {
                     "error.bad-arate", "error.bad-drc", "error.arate-codec", "note.arate-no-audio",
                     "note.arate-bitrate-capped", "note.drc-no-audio", "note.drc-not-dolby"
                 })
            Assert.NotEqual(Tr[anahtar], En[anahtar]);
    }

    private static async Task<string> KaynakAsync(string ad, string sesKodegi, string sesGirdisi)
    {
        var yol = Path.Combine(Klasor, ad);
        await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, new[]
        {
            "-hide_banner", "-y",
            "-f", "lavfi", "-i", "testsrc=size=320x240:rate=25:duration=3,noise=alls=60:allf=t",
            "-f", "lavfi", "-i", sesGirdisi,
            "-map", "0", "-map", "1", "-threads", "2", "-c:v", "libx264", "-preset", "ultrafast", "-b:v", "3M",
            "-c:a", sesKodegi, "-b:a", "192k", "-ac", "2", yol
        });
        return yol;
    }

    private static async Task<(List<string> Args, int Kod, string Err)> KodlaAsync(string kaynak, string cikti, Action<PlanOptions> ayar,
        Func<List<string>, List<string>>? degistir = null)
    {
        var info = await FfprobeClient.ProbeAsync(kaynak);
        var options = new PlanOptions
        {
            TargetMb = 1.5,
            LockedCodec = "libx264",
            LockedMode = EncodeMode.Crf,
            LockedCrf = 35,
            LockedPreset = "ultrafast"
        };
        ayar(options);
        var plan = PlanCalculator.Build(info, options);
        Assert.NotEqual(EncodeMode.PassThrough, plan.ModeEnum);
        var args = FfmpegArguments.Build(info, plan, cikti, 0, null).ToList();
        args.InsertRange(args.Count - 1, new[] { "-threads", "2" });
        if (degistir is not null) args = degistir(args);
        var sonuc = await AkisGirdisi.RunAsync(ToolLocator.Ffmpeg, args);
        return (args, sonuc.Code, sonuc.Err);
    }

    private static async Task<string> SesAlaniAsync(string yol, string alan)
        => Alan((await AkislarAsync(yol)).Single(stream => Alan(stream, "codec_type") == "audio"), alan);

    private static async Task<double> SesKbitAsync(string yol)
    {
        var paketler = await AkisGirdisi.RunAsync(ToolLocator.Ffprobe, new[]
        {
            "-v", "error", "-select_streams", "a:0", "-show_entries", "packet=size", "-of", "csv=p=0", yol
        });
        var sure = await AkisGirdisi.RunAsync(ToolLocator.Ffprobe, new[]
        {
            "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", yol
        });
        var bayt = paketler.Out.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Sum(satir => long.Parse(satir.TrimEnd(','), CultureInfo.InvariantCulture));
        return bayt * 8 / double.Parse(sure.Out.Trim(), CultureInfo.InvariantCulture) / 1000;
    }

    [FfmpegFact]
    public async Task CanliAacIstenenHizdaYaziliyor()
    {
        var kaynak = await KaynakAsync("canli-hiz-kaynak.mkv", "aac", "sine=frequency=440:sample_rate=48000:duration=3");
        var dusuk = Path.Combine(Klasor, "canli-hiz-8k.mp4");
        var isteksiz = Path.Combine(Klasor, "canli-hiz-yok.mp4");

        var (args, kod, err) = await KodlaAsync(kaynak, dusuk, options => options.AudioSampleRate = 8000);
        var (_, bosKod, _) = await KodlaAsync(kaynak, isteksiz, _ => { });

        Assert.True(kod == 0, err);
        Assert.Equal(0, bosKod);
        Assert.Equal("8000", Sonraki(args, "-ar"));
        Assert.Equal("8000", await SesAlaniAsync(dusuk, "sample_rate"));
        Assert.Equal("48000", await SesAlaniAsync(isteksiz, "sample_rate"));

        KanitKapanisi.Kapat(Klasor, "canli-hiz-*");
    }

    [FfmpegFact]
    public async Task CanliAacTavaniOlculenDegerinUstunde()
    {
        var cikti8 = Path.Combine(Klasor, "canli-tavan-8k.mka");
        var sonuc = await AkisGirdisi.RunAsync(ToolLocator.Ffmpeg, new[]
        {
            "-hide_banner", "-y", "-threads", "2",
            "-f", "lavfi", "-i", "anoisesrc=color=white:sample_rate=48000:duration=4:amplitude=0.9:seed=7",
            "-ac", "2", "-c:a", "aac", "-b:a", "256k", "-ar", "8000", cikti8
        });
        Assert.True(sonuc.Code == 0, sonuc.Err);

        var kbit = await SesKbitAsync(cikti8);
        var tavan = StreamMapping.AacCeilingK(8000, 2)!.Value;
        cikti.WriteLine($"aac 8 kHz stereo, istenen 256k: {kbit.ToString("0.0", CultureInfo.InvariantCulture)} kbit/sn, tavan {tavan}");
        Assert.True(kbit <= tavan * 1.05, $"olculen {kbit} tavan {tavan}");
        Assert.True(kbit > tavan * 0.8, $"olculen {kbit} tavanin cok altinda, tablo bayat");

        KanitKapanisi.Kapat(Klasor, "canli-tavan-*");
    }

    [FfmpegFact]
    public async Task CanliKodlayiciTablodakiHizlariReddediyor()
    {
        async Task<(int Kod, string Hiz)> DeneAsync(string kodek, int hiz)
        {
            var yol = Path.Combine(Klasor, $"canli-red-{kodek}-{hiz}.mka");
            var sonuc = await AkisGirdisi.RunAsync(ToolLocator.Ffmpeg, new[]
            {
                "-hide_banner", "-y", "-threads", "2",
                "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000:duration=1",
                "-ac", "2", "-c:a", kodek, "-b:a", "128k", "-ar", hiz.ToString(CultureInfo.InvariantCulture), yol
            });
            var okunan = sonuc.Code == 0 && new FileInfo(yol).Length > 0 ? await SesAlaniAsync(yol, "sample_rate") : "";
            return (sonuc.Code, okunan);
        }

        Assert.NotEqual(0, (await DeneAsync("ac3", 24000)).Kod);
        Assert.Equal((0, "32000"), await DeneAsync("ac3", 32000));
        Assert.NotEqual(0, (await DeneAsync("eac3", 22050)).Kod);
        Assert.NotEqual(0, (await DeneAsync("libopus", 44100)).Kod);
        Assert.Equal((0, "48000"), await DeneAsync("libopus", 24000));
        Assert.Equal((0, "11025"), await DeneAsync("aac", 11025));

        KanitKapanisi.Kapat(Klasor, "canli-red-*");
    }

    [FfmpegFact]
    public async Task CanliDrcDolbyKaynaktaUyarisizKabulEdiliyor()
    {
        var kaynak = await KaynakAsync("canli-drc-kaynak.mkv", "ac3", "sine=frequency=440:sample_rate=48000:duration=3");
        var hedef = Path.Combine(Klasor, "canli-drc-cikti.mp4");

        var (args, kod, err) = await KodlaAsync(kaynak, hedef, options => options.AudioDrcScale = 2);
        var (_, yanlisKod, yanlisErr) = await KodlaAsync(kaynak, Path.Combine(Klasor, "canli-drc-yanlis.mp4"), options => options.AudioDrcScale = 2,
            liste => liste.Select(arg => arg == "-drc_scale:1" ? "-drc_scale:0" : arg).ToList());
        var (_, uydurmaKod, _) = await KodlaAsync(kaynak, Path.Combine(Klasor, "canli-drc-uydurma.mp4"), options => options.AudioDrcScale = 2,
            liste => liste.Select(arg => arg == "-drc_scale:1" ? "-drc_uydurma:1" : arg).ToList());

        Assert.Equal("2", Sonraki(args, "-drc_scale:1"));
        Assert.True(kod == 0, err);
        Assert.DoesNotContain("has not been used", err);
        Assert.Equal("aac", await SesAlaniAsync(hedef, "codec_name"));

        Assert.Equal(0, yanlisKod);
        Assert.Contains("has not been used", yanlisErr);
        Assert.NotEqual(0, uydurmaKod);

        KanitKapanisi.Kapat(Klasor, "canli-drc-*");
    }
}
