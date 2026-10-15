using Avalonia.Controls;
using VidShrink.App;
using VidShrink.Core;
using static VidShrink.Tests.B1KalanGirdi;

namespace VidShrink.Tests;

/// <summary>
/// Dönüştür'de ses seçenekleri (DON-P9): AC3 ve E-AC3 kodlama, Opus, OGG, AIFF ve AC3 ses kapları,
/// MP4'e AC3/E-AC3/Opus kopyası, ses kabında kaynağa uyan sesin kopyalanması ve ses yüksekliği,
/// kanal ve örnekleme hızı işlemesi. İşleme seçilince ses kopyası kapanır.
/// </summary>
public sealed class DonusturSesSecenekleriTests
{
    private static readonly SourceStream Aac = new(1, StreamKind.Audio, "aac", "tur", Channels: 2, BitrateBps: 128_000, SampleRate: 44100);
    private static readonly SourceStream Ac3 = new(1, StreamKind.Audio, "ac3", "tur", Channels: 6, BitrateBps: 448_000, SampleRate: 48000);
    private static readonly SourceStream Eac3 = new(1, StreamKind.Audio, "eac3", "tur", Channels: 6, BitrateBps: 640_000, SampleRate: 48000);
    private static readonly SourceStream Opus = new(1, StreamKind.Audio, "opus", "tur", Channels: 2, BitrateBps: 128_000, SampleRate: 48000);
    private static readonly SourceStream Vorbis = new(1, StreamKind.Audio, "vorbis", "tur", Channels: 2, BitrateBps: 128_000, SampleRate: 44100);
    private static readonly SourceStream Pcm = new(1, StreamKind.Audio, "pcm_s16le", "tur", Channels: 2, BitrateBps: 1_411_000, SampleRate: 44100);
    private static readonly SourceStream PcmBe = new(1, StreamKind.Audio, "pcm_s16be", "tur", Channels: 2, BitrateBps: 1_411_000, SampleRate: 44100);

    private static MediaInfo Ornek(params SourceStream[] streams)
        => Kaynak(62, streams) with { FilePath = @"C:\Kayitlar\gezi.mkv" };

    private static List<string> Kur(MediaInfo info, ConversionPlan plan)
        => ConversionArguments.Build(info, plan, "cikti." + plan.Container).ToList();

    /// <summary>Yeni ses kapları ses-yalnız sayılır ve her biri kendi kodlayıcısıyla yazılır.</summary>
    [Theory]
    [InlineData("opus", "libopus")]
    [InlineData("ogg", "libopus")]
    [InlineData("aiff", "pcm_s16be")]
    [InlineData("ac3", "ac3")]
    public void YeniSesKaplariKendiKodlayicisiylaYazilir(string kap, string kodlayici)
    {
        var plan = new ConversionPlan { Container = kap, AudioCodec = ConversionPresetPlan.AudioCodecForAudioContainer(kap) };

        Assert.True(plan.AudioOnly);
        Assert.Equal(kodlayici, plan.AudioCodec);
        var args = Kur(Ornek(Video, Aac), plan);
        Assert.Contains("-vn", args);
        Assert.Equal(kodlayici, Sonraki(args, "-c:a"));
        Assert.Equal("cikti." + kap, args[^1]);
    }

    /// <summary>Yeni ses kapları tek iz taşır; iki iz reddedilir.</summary>
    [Theory]
    [InlineData("opus")]
    [InlineData("ogg")]
    [InlineData("aiff")]
    [InlineData("ac3")]
    public void YeniSesKaplariTekIzTasir(string kap)
    {
        var info = Ornek(Video, Aac, Ac3 with { Index = 2 });
        var plan = new ConversionPlan { Container = kap, AudioCodec = ConversionPresetPlan.AudioCodecForAudioContainer(kap), AudioTracks = new[] { 0, 1 } };

        Assert.Contains($"The {kap.ToUpperInvariant()} container carries only one audio track.", ConversionArguments.Validate(info, plan));
    }

    /// <summary>AC3 ve E-AC3 kodlayıcıları MP4, MOV ve MKV'de seçilebilir; WebM ve AVI almaz.</summary>
    [Fact]
    public void Ac3VeEac3KodlamaTablosu()
    {
        var info = Ornek(Video, Aac);
        foreach (var kodlayici in new[] { "ac3", "eac3" })
        {
            foreach (var kap in new[] { "mp4", "mov", "mkv" })
            {
                var plan = new ConversionPlan { Container = kap, AudioCodec = kodlayici };
                Assert.Empty(ConversionArguments.Validate(info, plan));
                Assert.Equal(kodlayici, Sonraki(Kur(info, plan), "-c:a"));
            }
            foreach (var kap in new[] { "webm", "avi" })
                Assert.Contains($"The {kap.ToUpperInvariant()} container does not support the selected {kodlayici} audio encoder.",
                    ConversionArguments.Validate(info, new ConversionPlan { Container = kap, VideoCodec = kap == "webm" ? "libvpx-vp9" : "libx264", AudioCodec = kodlayici }));
        }
    }

    /// <summary>MP4'e AC3, E-AC3 ve Opus kopyalanır (Küçült'ün kopya tablosu); Vorbis kopyalanmaz.</summary>
    [Fact]
    public void Mp4KopyaTablosuKucultIleEsit()
    {
        foreach (var kaynak in new[] { Ac3, Eac3, Opus })
        {
            var plan = new ConversionPlan { Container = "mp4", AudioCodec = "copy" };
            var info = Ornek(Video, kaynak);
            Assert.Empty(ConversionArguments.Validate(info, plan));
            Assert.Equal("copy", Sonraki(Kur(info, plan), "-c:a"));
        }

        Assert.Contains("The MP4 container does not support copying the source vorbis audio stream.",
            ConversionArguments.Validate(Ornek(Video, Vorbis), new ConversionPlan { Container = "mp4", AudioCodec = "copy" }));
    }

    /// <summary>Ses kabı seçilince kaynak ses o kaba sığıyorsa yeniden kodlanmadan çıkarılır; sığmıyorsa kodlanır.</summary>
    [Theory]
    [InlineData("m4a", "aac")]
    [InlineData("ac3", "ac3")]
    [InlineData("opus", "opus")]
    [InlineData("ogg", "opus")]
    [InlineData("ogg", "vorbis")]
    [InlineData("aiff", "pcm_s16be")]
    [InlineData("wav", "pcm_s16le")]
    public void SesKabiKaynagaUyanSesiKopyalar(string kap, string kaynakKodek)
    {
        var info = Ornek(Video, Aac with { Codec = kaynakKodek });
        var plan = new ConversionPlan { Container = kap, AudioCodec = ConversionPresetPlan.AudioCodecForAudioContainer(kap) };

        var args = Kur(info, plan);
        Assert.Equal("copy", Sonraki(args, "-c:a"));
        Assert.DoesNotContain("-b:a", args);
    }

    /// <summary>Kaynak kaba sığmıyorsa, dış ses varsa ya da işleme seçildiyse ses kabı yeniden kodlar (olumsuz kontrol).</summary>
    [Fact]
    public void SesKabiUymayanKaynagiKodlar()
    {
        var aac = Ornek(Video, Aac);
        Assert.Equal("libopus", Sonraki(Kur(aac, new ConversionPlan { Container = "opus", AudioCodec = "libopus" }), "-c:a"));
        Assert.Equal("ac3", Sonraki(Kur(Ornek(Video, Eac3), new ConversionPlan { Container = "ac3", AudioCodec = "ac3" }), "-c:a"));
        Assert.Equal("pcm_s16be", Sonraki(Kur(Ornek(Video, Pcm), new ConversionPlan { Container = "aiff", AudioCodec = "pcm_s16be" }), "-c:a"));
        Assert.Equal("aac", Sonraki(Kur(Ornek(Video, Aac with { Codec = "mp3" }), new ConversionPlan { Container = "m4a", AudioCodec = "aac" }), "-c:a"));

        var islenen = Kur(aac, new ConversionPlan { Container = "m4a", AudioCodec = "aac", AudioLoudnorm = true });
        Assert.Equal("aac", Sonraki(islenen, "-c:a"));

        var dis = Kur(aac, new ConversionPlan { Container = "m4a", AudioCodec = "aac", ExternalAudio = new ExternalAudio(@"C:\Muzik\fon.mp3", null) });
        Assert.DoesNotContain("copy", dis);
    }

    /// <summary>Ses yüksekliği <c>loudnorm</c> süzgeciyle, sonra kaynağın hızına dönen <c>aresample</c> ile yazılır.</summary>
    [Fact]
    public void SesYuksekligiLoudnormYazar()
    {
        var args = Kur(Ornek(Video, Aac), new ConversionPlan { Container = "mp4", AudioLoudnorm = true });

        Assert.Equal(StreamMapping.LoudnormFilter + ",aresample=44100", Sonraki(args, "-af"));
        Assert.DoesNotContain("-ar", args);
        Assert.DoesNotContain("-af", Kur(Ornek(Video, Aac), new ConversionPlan { Container = "mp4" }));
    }

    /// <summary>Kanal <c>-ac</c>, örnekleme hızı <c>-ar</c> olarak yazılır; ses yüksekliği istenen hıza döner.</summary>
    [Fact]
    public void KanalVeOrneklemeHiziYazilir()
    {
        var args = Kur(Ornek(Video, Ac3), new ConversionPlan { Container = "mkv", AudioChannels = 2, AudioSampleRate = 44100, AudioLoudnorm = true });

        Assert.Equal("2", Sonraki(args, "-ac"));
        Assert.Equal("44100", Sonraki(args, "-ar"));
        Assert.Equal(StreamMapping.LoudnormFilter + ",aresample=44100", Sonraki(args, "-af"));

        var mono = Kur(Ornek(Video, Aac), new ConversionPlan { Container = "mp4", AudioChannels = 1 });
        Assert.Equal("1", Sonraki(mono, "-ac"));
        Assert.DoesNotContain("-ar", mono);
        Assert.DoesNotContain("-af", mono);
    }

    /// <summary>İşlemeden biri seçilince ses kopyası kapanır ve kap kendi kodlayıcısıyla yazar.</summary>
    [Theory]
    [InlineData("mp4", "aac")]
    [InlineData("mkv", "libopus")]
    [InlineData("webm", "libopus")]
    [InlineData("mov", "aac")]
    public void IslemeKopyayiKapatir(string kap, string kodlayici)
    {
        var info = Ornek(Video, Opus);
        foreach (var plan in new[]
        {
            new ConversionPlan { Container = kap, VideoCodec = "libvpx-vp9", AudioCodec = "copy", AudioLoudnorm = true },
            new ConversionPlan { Container = kap, VideoCodec = "libvpx-vp9", AudioCodec = "copy", AudioChannels = 1 },
            new ConversionPlan { Container = kap, VideoCodec = "libvpx-vp9", AudioCodec = "copy", AudioSampleRate = 48000 }
        })
        {
            Assert.Empty(ConversionArguments.Validate(info, plan));
            var args = Kur(info, plan);
            Assert.Equal(kodlayici, Sonraki(args, "-c:a"));
            Assert.DoesNotContain("copy", args.Where((_, i) => i > 0 && args[i - 1] == "-c:a"));
        }

        Assert.Equal("copy", Sonraki(Kur(info, new ConversionPlan { Container = "mkv", AudioCodec = "copy" }), "-c:a"));
    }

    /// <summary>
    /// Örnekleme hızı kodlayıcının yazabildiği hızlarla sınırlıdır (<see cref="StreamMapping.SampleRatesFor"/>):
    /// Opus yalnız 48 kHz, AC3 32/44,1/48 kHz. MXF yalnız 48 kHz alır ve <c>-ar</c> iki kez yazılmaz.
    /// </summary>
    [Fact]
    public void OrneklemeHiziKodlayiciyaUyar()
    {
        var info = Ornek(Video, Aac);
        Assert.Contains("The libopus audio encoder does not support 44100 Hz.",
            ConversionArguments.Validate(info, new ConversionPlan { Container = "webm", VideoCodec = "libvpx-vp9", AudioCodec = "libopus", AudioSampleRate = 44100 }));
        Assert.Empty(ConversionArguments.Validate(info, new ConversionPlan { Container = "mkv", AudioCodec = "ac3", AudioSampleRate = 44100 }));
        Assert.Empty(ConversionArguments.Validate(info, new ConversionPlan { Container = "mp4", AudioCodec = "aac", AudioSampleRate = 44100 }));

        var mxf = new ConversionPlan { Container = "mxf", VideoCodec = IntermediateCodecs.DnxHr, VideoProfile = "dnxhr_sq", AudioCodec = "pcm_s16le", AudioSampleRate = 44100 };
        Assert.Contains("The MXF container carries only 48000 Hz audio.", ConversionArguments.Validate(info, mxf));
        var mxfArgs = Kur(info, mxf with { AudioSampleRate = 48000 });
        Assert.Single(mxfArgs, arg => arg == "-ar");

        Assert.Contains("Audio channel count must be positive.",
            ConversionArguments.Validate(info, new ConversionPlan { Container = "mp4", AudioChannels = 0 }));
    }

    /// <summary>Sesi düşüren planda işleme seçimleri okunmaz.</summary>
    [Fact]
    public void SessizPlandaIslemeOkunmaz()
    {
        var args = Kur(Ornek(Video, Aac), new ConversionPlan { Container = "mp4", AudioCodec = null, AudioLoudnorm = true, AudioChannels = 1, AudioSampleRate = 44100 });

        Assert.Contains("-an", args);
        Assert.DoesNotContain("-af", args);
        Assert.DoesNotContain("-ac", args);
        Assert.DoesNotContain("-ar", args);
    }

    /// <summary>Kaynak ses kodlarının ses kaplarına kopya uyumu; AIFF büyük sonlu PCM taşır, küçük sonluyu almaz.</summary>
    [Fact]
    public void SesKabiKopyaTablosu()
    {
        Assert.Empty(ConversionArguments.Validate(Ornek(Video, PcmBe), new ConversionPlan { Container = "aiff", AudioCodec = "copy" }));
        Assert.Contains("The AIFF container does not support copying the source pcm_s16le audio stream.",
            ConversionArguments.Validate(Ornek(Video, Pcm), new ConversionPlan { Container = "aiff", AudioCodec = "copy" }));
        Assert.Contains("The OPUS container does not support copying the source vorbis audio stream.",
            ConversionArguments.Validate(Ornek(Video, Vorbis), new ConversionPlan { Container = "opus", AudioCodec = "copy" }));
        Assert.Empty(ConversionArguments.Validate(Ornek(Video, Ac3), new ConversionPlan { Container = "ac3", AudioCodec = "copy" }));
    }

    private static readonly string[] Anahtarlar =
    {
        "main.convert.audio-loudnorm",
        "main.convert.audio-channels",
        "main.convert.audio-sample-rate",
        "main.validation.audio-channels-positive",
        "main.validation.mxf-sample-rate",
        "main.validation.audio-sample-rate"
    };

    /// <summary>
    /// Pencere kolu: ses panelindeki kanal, örnekleme hızı ve ses yüksekliği kutuları plana ve komuta girer,
    /// kopya seçiliyken kopyayı kapatır; ses taşımayan kapta kapanır. Yeni kaplar ve kodlayıcılar listelerin sonunda.
    /// </summary>
    [Fact]
    public void PanelSecimleriPlanaVeKomutaGirer()
    {
        var info = Kaynak(62, Video, Opus) with { FilePath = @"C:\Kayitlar\gezi.mkv" };
        var sonuc = Pencerede(window =>
        {
            window.LoadWithoutProbing(info.FilePath, info);
            window.CmbContainer.SelectedItem = Oge(window.CmbContainer, "mkv");
            window.CmbConvertAudio.SelectedItem = Oge(window.CmbConvertAudio, "copy");
            var once = window.TxtConvertCommand.Text ?? "";
            window.CmbConvertChannels.SelectedItem = Oge(window.CmbConvertChannels, "1");
            window.CmbConvertSampleRate.SelectedItem = Oge(window.CmbConvertSampleRate, "48000");
            window.ChkConvertLoudnorm.IsChecked = true;
            var plan = window.CurrentConversionPlan();
            var sonra = window.TxtConvertCommand.Text ?? "";
            window.CmbContainer.SelectedItem = Oge(window.CmbContainer, "gif");
            var gifAcik = window.ChkConvertLoudnorm.IsEnabled || window.CmbConvertChannels.IsEnabled || window.CmbConvertSampleRate.IsEnabled;
            window.CmbContainer.SelectedItem = Oge(window.CmbContainer, "ogg");
            window.CmbConvertChannels.SelectedIndex = 0;
            window.CmbConvertSampleRate.SelectedIndex = 0;
            window.ChkConvertLoudnorm.IsChecked = false;
            var ogg = window.TxtConvertCommand.Text ?? "";
            var kaplar = window.CmbContainer.Items.OfType<ComboBoxItem>().Select(item => (string?)item.Tag).SkipWhile(tag => tag != "opus").Take(4).ToArray();
            var kodlayicilar = window.CmbConvertAudio.Items.OfType<ComboBoxItem>().Select(item => (string?)item.Tag).TakeLast(2).ToArray();
            return (once, plan, sonra, gifAcik, ogg, kaplar, kodlayicilar);
        });

        Assert.Contains("-c:a copy", sonuc.once);
        Assert.True(sonuc.plan.AudioLoudnorm);
        Assert.Equal(1, sonuc.plan.AudioChannels);
        Assert.Equal(48000, sonuc.plan.AudioSampleRate);
        Assert.Contains("-c:a libopus", sonuc.sonra);
        Assert.Contains("-ac 1", sonuc.sonra);
        Assert.Contains("-ar 48000", sonuc.sonra);
        Assert.Contains(StreamMapping.LoudnormFilter, sonuc.sonra);
        Assert.False(sonuc.gifAcik);
        Assert.Contains("-c:a copy", sonuc.ogg);
        Assert.Equal(new[] { "opus", "ogg", "aiff", "ac3" }, sonuc.kaplar);
        Assert.Equal(new[] { "ac3", "eac3" }, sonuc.kodlayicilar);
    }

    [Fact]
    public void AltiAnahtarKirkIkiDildeCevrili()
    {
        var english = Locales.Domain("en", "main");
        Assert.Equal(42, Locales.Languages.Count);
        var eksik = new List<string>();
        foreach (var language in Locales.Languages)
        {
            var values = Locales.Domain(language, "main");
            foreach (var key in Anahtarlar)
            {
                if (!values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value)) eksik.Add($"{language} {key}: yok");
                else if (language != "en" && value == english[key]) eksik.Add($"{language} {key}: cevrilmemis");
                else if (key == "main.validation.audio-sample-rate" && !(value.Contains("{0}", StringComparison.Ordinal) && value.Contains("{1}", StringComparison.Ordinal)))
                    eksik.Add($"{language} {key}: yer tutucu yok");
            }
        }
        Assert.Empty(eksik);
    }

    private static ComboBoxItem Oge(ComboBox kutu, string etiket)
        => kutu.Items.OfType<ComboBoxItem>().First(item => (string?)item.Tag == etiket);

    private static T Pencerede<T>(Func<MainWindow, T> govde)
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "donustur-ses-secenekleri", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        try
        {
            return AppHost.Run(() =>
            {
                var window = new MainWindow { SettingsPathOverride = Path.Combine(klasor, "settings.json") };
                try { return govde(window); }
                finally { window.Close(); }
            });
        }
        finally
        {
            try { Directory.Delete(klasor, true); }
            catch (IOException) { }
        }
    }
}
