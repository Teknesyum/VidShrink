using VidShrink.Cli;
using VidShrink.Core;

namespace VidShrink.Tests;

/// <summary>
/// MOV kabı küçültme kolunda (<c>docs/netlestirme/022-mov-kabi-kucultmede.md</c>).
/// Karar üç parçalı: kap uzantıdan türer, MOV MP4'un muxer soyundan sayılır, ayrıldıkları
/// tek yer ses kopyalama listesidir — MOV'da opus/flac taşınmaz, iz yeniden kodlanır ve
/// kullanıcıya not düşülür.
/// </summary>
public sealed class MovKabiTests
{
    private static readonly SourceStream Video = new(0, StreamKind.Video, "h264");

    private static MediaInfo Kaynak(params SourceStream[] streams) => new()
    {
        FilePath = "kaynak.mkv",
        FileSizeBytes = 400_000_000L,
        DurationSeconds = 600,
        Width = 1920,
        Height = 1080,
        Fps = 24,
        VideoCodec = "h264",
        TotalBitrateBps = 5_000_000,
        AudioCodec = streams.FirstOrDefault(stream => stream.Kind == StreamKind.Audio)?.Codec,
        AudioBitrateBps = streams.FirstOrDefault(stream => stream.Kind == StreamKind.Audio)?.BitrateBps ?? 0,
        AudioChannels = streams.FirstOrDefault(stream => stream.Kind == StreamKind.Audio)?.Channels ?? 0,
        Streams = streams
    };

    private static StreamPlan Plan(OutputContainer kap, string kodek, int kbit = 128_000)
        => StreamMapping.Decide(
            Kaynak(Video, new SourceStream(1, StreamKind.Audio, kodek, "eng", Channels: 2, BitrateBps: kbit)),
            StreamRequest.Default, kap, 160, null, "aac", true, 100);

    /// <summary>Uzantı kabı belirler: <c>.mov</c> artık sessizce MP4'e düşmüyor.</summary>
    [Theory]
    [InlineData("cikti.mov", OutputContainer.Mov)]
    [InlineData("cikti.MOV", OutputContainer.Mov)]
    [InlineData("cikti.mp4", OutputContainer.Mp4)]
    [InlineData("cikti.mkv", OutputContainer.Mkv)]
    [InlineData("cikti.webm", OutputContainer.WebM)]
    public void KapUzantidanTuruyor(string yol, OutputContainer beklenen)
        => Assert.Equal(beklenen, StreamMapping.ContainerOf(yol));

    /// <summary>
    /// O3: teslim kabı plana girer. Plan MP4'e kurulup çıktı <c>.mov</c> yazılınca
    /// <see cref="StreamMapping.ForOutput"/> akışları yeniden hesaplıyor, opus'un yeniden
    /// kodlanması ve yan bütçe gerekçede görünmüyordu. Artık kodlamaya giden akış planı,
    /// bütçenin kurulduğu ve gerekçenin okuduğu planın ta kendisi.
    /// </summary>
    [Fact]
    public void TeslimKabiPlanaGiriyor()
    {
        var info = Kaynak(Video, new SourceStream(1, StreamKind.Audio, "opus", "eng", Channels: 2, BitrateBps: 128_000));
        var kapsiz = PlanCalculator.Build(info, new PlanOptions { TargetMb = 25 });
        var movlu = PlanCalculator.Build(info, new PlanOptions { TargetMb = 25, DeliveredContainer = OutputContainer.Mov });

        Assert.Equal(OutputContainer.Mp4, kapsiz.Streams!.Container);
        Assert.Equal(OutputContainer.Mov, movlu.Streams!.Container);
        Assert.Same(movlu.Streams, StreamMapping.ForOutput(info, movlu, "cikti.mov"));
        Assert.Contains(StreamNote.AudioCodecNotInContainer, movlu.Streams.Notes);
        Assert.DoesNotContain(StreamNote.AudioCodecNotInContainer, kapsiz.Streams.Notes);
        Assert.Equal(OutputContainer.Mov,
            PlanCalculator.WithTarget(new PlanOptions { DeliveredContainer = OutputContainer.Mov }, 10).DeliveredContainer);
    }

    /// <summary>Ön ayar kabı kuralı: WebM kodekle gelir, plana kap olarak inmez.</summary>
    [Theory]
    [InlineData(OutputContainer.Mov, OutputContainer.Mov)]
    [InlineData(OutputContainer.Mkv, OutputContainer.Mkv)]
    [InlineData(OutputContainer.WebM, null)]
    [InlineData(null, null)]
    public void OnAyarKabiPlanaIniyor(OutputContainer? onAyar, OutputContainer? beklenen)
        => Assert.Equal(beklenen, PresetLibrary.DeliveredContainer(onAyar));

    /// <summary>CLI'da çıktı uzantısı kabı söyler; plan o kaba kurulur.</summary>
    [Theory]
    [InlineData("cikti.mov", OutputContainer.Mov)]
    [InlineData("cikti.mkv", OutputContainer.Mkv)]
    public void CliCiktiUzantisiPlanKabi(string cikti, OutputContainer beklenen)
    {
        var parsed = CliParser.Parse(["plan", "a.mp4", "--hedef", "25", "--cikti", cikti]);

        Assert.True(parsed.Ok, parsed.ErrorKey);
        Assert.Equal(beklenen, parsed.Request!.ToPlanOptions(10).DeliveredContainer);
    }

    /// <summary>Uzantı geri yönde de aynı: kap → uzantı.</summary>
    [Fact]
    public void MovUzantisiGeriDonuyor()
        => Assert.Equal("mov", StreamMapping.ExtensionOf(OutputContainer.Mov));

    /// <summary>MOV, MP4 ailesinden; MKV ve WebM değil.</summary>
    [Theory]
    [InlineData(OutputContainer.Mp4, true)]
    [InlineData(OutputContainer.Mov, true)]
    [InlineData(OutputContainer.Mkv, false)]
    [InlineData(OutputContainer.WebM, false)]
    public void Mp4AilesiMovuIceriyor(OutputContainer kap, bool beklenen)
        => Assert.Equal(beklenen, StreamMapping.IsMp4Family(kap));

    /// <summary>
    /// Aile kararının ilk sonucu: MOV da <c>+faststart</c> alıyor. MKV almıyor — kolun
    /// her kaba açık olmadığı olumsuz kontrolle pimli.
    /// </summary>
    [Theory]
    [InlineData("cikti.mov", true)]
    [InlineData("cikti.mp4", true)]
    [InlineData("cikti.mkv", false)]
    public void FaststartMovdaDaVeriliyor(string yol, bool beklenen)
    {
        var info = Kaynak(Video, new SourceStream(1, StreamKind.Audio, "aac", "eng", Channels: 2, BitrateBps: 128_000));
        var plan = PlanCalculator.Build(info, new PlanOptions { TargetMb = 25 });
        var args = FfmpegArguments.ToCommandLine(FfmpegArguments.Build(info, plan, yol, 0, null));

        Assert.Equal(beklenen, args.Contains("+faststart"));
    }

    /// <summary>
    /// Ailenin ikinci sonucu: metin altyazı MOV'da da <c>mov_text</c>'e iniyor, MKV'de
    /// kopyalanıyor.
    /// </summary>
    [Fact]
    public void MetinAltyaziMovdaMovTexteIniyor()
    {
        var info = Kaynak(Video,
            new SourceStream(1, StreamKind.Audio, "aac", "eng", Channels: 2, BitrateBps: 128_000),
            new SourceStream(2, StreamKind.Subtitle, "subrip", "eng", Bytes: 20_000));

        var mov = StreamMapping.Decide(info, StreamRequest.Default, OutputContainer.Mov, 160, null, "aac", true, 100);
        var mkv = StreamMapping.Decide(info, StreamRequest.Default, OutputContainer.Mkv, 160, null, "aac", true, 100);

        Assert.Equal("mov_text", Assert.Single(mov.Subtitles).Codec);
        Assert.Equal("copy", Assert.Single(mkv.Subtitles).Codec);
    }

    /// <summary>
    /// Ayrıldıkları yer: aac MOV'da da kopyalanıyor, opus kopyalanmıyor. MP4'te ikisi de
    /// kopyalanabildiği için karşılaştırma kabın kendisini ölçüyor, kodeği değil.
    /// </summary>
    [Theory]
    [InlineData("aac", true)]
    [InlineData("ac3", true)]
    [InlineData("mp3", true)]
    [InlineData("opus", false)]
    public void MovdaOpusKopyalanmiyorAacKopyalaniyor(string kodek, bool kopyalanir)
    {
        Assert.Equal(kopyalanir, Assert.Single(Plan(OutputContainer.Mov, kodek).Audio).Copies);
        Assert.True(Assert.Single(Plan(OutputContainer.Mp4, kodek).Audio).Copies);
    }

    /// <summary>
    /// Yeniden kodlama sessiz geçmiyor: kaynağın kodeği başka kapta taşınabilirken bu
    /// kapta taşınamıyorsa not düşülüyor. Kopyalanan iz not almıyor — kapının her izde
    /// çalmadığı olumsuz kontrolle pimli.
    /// </summary>
    [Fact]
    public void TasinamayanKodekNotDusuyor()
    {
        var opus = Plan(OutputContainer.Mov, "opus");
        var aac = Plan(OutputContainer.Mov, "aac");

        Assert.Contains(StreamNote.AudioCodecNotInContainer, opus.Notes);
        Assert.DoesNotContain(StreamNote.AudioCodecNotInContainer, aac.Notes);
        Assert.Equal("aac", Assert.Single(opus.Audio).Codec);
    }

    /// <summary>
    /// MOV, MP4 gibi tek ses izli kalıyor: "tüm izleri koru" yalnız MKV'de tüm izleri
    /// taşıyor. Aile kararı buraya da uyguland; MOV'un MKV kolu yoktur.
    /// </summary>
    [Fact]
    public void IzleriKoruMovdaMp4GibiDavraniyor()
    {
        var info = Kaynak(Video,
            new SourceStream(1, StreamKind.Audio, "aac", "eng", Channels: 2, BitrateBps: 128_000),
            new SourceStream(2, StreamKind.Audio, "aac", "tur", Channels: 2, BitrateBps: 128_000));
        var istek = StreamRequest.Default with { KeepAllTracks = true };

        Assert.Single(StreamMapping.Decide(info, istek, OutputContainer.Mov, 160, null, "aac", true, 100).Audio);
        Assert.Single(StreamMapping.Decide(info, istek, OutputContainer.Mp4, 160, null, "aac", true, 100).Audio);
        Assert.Equal(2, StreamMapping.Decide(info, istek, OutputContainer.Mkv, 160, null, "aac", true, 100).Audio.Count);
    }

    /// <summary>HandBrake ön ayarı <c>av_mov</c> yazıyor; ikisi de aynı kaba iniyor.</summary>
    [Theory]
    [InlineData("mov")]
    [InlineData("av_mov")]
    public void HandBrakeOnAyariMovuOkuyor(string yazim)
    {
        var json = "{\"PresetList\":[{\"PresetName\":\"Mov\",\"Type\":1,\"FileFormat\":\"" + yazim + "\"}]}";
        var ceviri = Assert.Single(HandBrakePresetImport.Translate(json));

        Assert.Equal(OutputContainer.Mov, ceviri.Profile.Container);
    }
}
