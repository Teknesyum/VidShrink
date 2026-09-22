using System.Text.RegularExpressions;
using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// B1a: cok kanalli sesin ac3/eac3 kolu. Olcum <c>docs/olcumler/b1a-ac3-merdiveni.md</c>,
/// plan <c>docs/plan.md</c>'deki "B1a" bolumu.
///
/// <para>Merdiven ve kanal tabani burada <b>elle yazilmiyor</b>: ikisi de olcum belgesinden
/// okunup kodun tablosuyla karsilastiriliyor. Sayilar bir gun yeniden olculurse belge ile kod
/// birlikte degismek zorunda; biri degisip digeri kalirsa olcu kirmizi olur.</para>
///
/// <para>ffmpeg bit hizi isteginde <b>hata vermiyor</b>, sessizce merdivene oturuyor. O yuzden
/// urunun kendi kirpmasi gerekiyor: butceye giren sayi ile teslim edilen sayi ayrisirsa hedef
/// boyut hesabi yalan soyler. Kanal tabani ise gercekten hata veriyor, kol aac/libopus'a doner.</para>
/// </summary>
public sealed class DolbySesKoluTests
{
    private static MediaInfo Kaynak(params SourceStream[] streams) => new()
    {
        FilePath = "girdi.mkv",
        FileSizeBytes = 400L * 1024 * 1024,
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

    private static readonly SourceStream Video = new(0, StreamKind.Video, "h264");

    private static MediaInfo CokKanalli(int channels = 6, string codec = "ac3") =>
        Kaynak(Video, new SourceStream(1, StreamKind.Audio, codec, "eng", IsDefault: true, Channels: channels, BitrateBps: 448_000));

    private static string Belge => File.ReadAllText(Path.Combine(TipSources.Root, "docs", "olcumler", "b1a-ac3-merdiveni.md"));

    private static StreamPlan Karar(MediaInfo info, OutputContainer container, int audioK, string codec, bool kanallariKoru = true)
        => StreamMapping.Decide(info, StreamRequest.Default, container, audioK, null, codec, false, 100, kanallariKoru);

    /// <summary>Merdiven kodda (<c>StreamMapping</c>) elle yazili, olcu belgeden okuyup ikisinin birebir ayni oldugunu sinar.</summary>
    [Fact]
    public void Ac3MerdiveniOlcumBelgesindenOkunuyor()
    {
        var satir = Regex.Match(Belge, @"Ölçülen basamaklar \(hepsi birebir teslim edildi\): ([0-9,\s]+)\.");

        Assert.True(satir.Success, "Merdiven satiri belgede bulunamadi; belge tasinmis ya da cumlesi degismis olabilir.");

        var belgedeki = satir.Groups[1].Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(int.Parse)
            .ToArray();

        Assert.Equal(19, belgedeki.Length);
        Assert.Equal(belgedeki, StreamMapping.Ac3LadderK);
    }

    /// <summary>
    /// Merdivene oturtma kurali: istekten kucuk ya da esit en buyuk basamak, hicbiri yoksa
    /// en kucuk basamak, 640'in ustu 640. Her satir belgedeki olculmus bir istegin karsiligi.
    /// </summary>
    [Theory]
    [InlineData(24, 32)]
    [InlineData(40, 40)]
    [InlineData(56, 56)]
    [InlineData(130, 128)]
    [InlineData(200, 192)]
    [InlineData(320, 320)]
    [InlineData(700, 640)]
    [InlineData(768, 640)]
    public void Ac3IstegiMerdivenineOturuyor(int istenen, int beklenen)
        => Assert.Equal(beklenen, StreamMapping.FitDolbyBitrateK("ac3", istenen));

    /// <summary>
    /// Olumsuz kontrol: eac3 ac3'un merdivenini tasimiyor. Ayni istekler eac3'te
    /// oturmadan geciyor — yani yukaridaki esitlikler "her sey ayni sayiyi donduruyor"
    /// diye yesil degil.
    /// </summary>
    [Theory]
    [InlineData(130)]
    [InlineData(200)]
    [InlineData(700)]
    [InlineData(768)]
    [InlineData(3024)]
    public void Eac3MerdivenTasimiyor(int istenen)
        => Assert.Equal(istenen, StreamMapping.FitDolbyBitrateK("eac3", istenen));

    /// <summary>Kanal tabani da belgeden: tablo satir satir kodun hukmuyle karsilastiriliyor.</summary>
    [Fact]
    public void KanalTabaniOlcumBelgesindenOkunuyor()
    {
        var satirlar = Regex.Matches(Belge, @"^\| (\d) \| (\d+)k \| (\d+)k \|", RegexOptions.Multiline);
        Assert.Equal(6, satirlar.Count);

        foreach (Match satir in satirlar)
        {
            var kanal = int.Parse(satir.Groups[1].Value);
            Assert.Equal(int.Parse(satir.Groups[2].Value), StreamMapping.ChannelFloorK("ac3", kanal));
            Assert.Equal(int.Parse(satir.Groups[3].Value), StreamMapping.ChannelFloorK("eac3", kanal));
        }

        Assert.NotEqual(StreamMapping.ChannelFloorK("ac3", 4), StreamMapping.ChannelFloorK("eac3", 4));
    }

    /// <summary>
    /// Kap kapisi: WebM ac3 tasimiyor, kol libopus'a doner ve not duser. Olumsuz kontrol
    /// ayni istegin MKV'de ac3 olarak durmasi.
    /// </summary>
    [Fact]
    public void KapTasimiyorsaDolbyDuserNotDuser()
    {
        var webm = Karar(CokKanalli(), OutputContainer.WebM, 384, "ac3");
        var mkv = Karar(CokKanalli(), OutputContainer.Mkv, 384, "ac3");

        Assert.Equal("libopus", Assert.Single(webm.Audio).Codec);
        Assert.Contains(StreamNote.DolbyCodecNotInContainer, webm.Notes);

        Assert.Equal("ac3", Assert.Single(mkv.Audio).Codec);
        Assert.DoesNotContain(StreamNote.DolbyCodecNotInContainer, mkv.Notes);
    }

    /// <summary>
    /// Kanal tabaninin altina dusen butce Dolby kolunu kuramaz: MP4'te aac'ye doner ve
    /// ayri bir not duser. Tabanin kendisi (6 kanal ac3 = 48k) gecer — olumsuz kontrol.
    /// </summary>
    [Fact]
    public void TabaninAltindakiButceDolbyKolunuKurmuyor()
    {
        var altinda = Karar(CokKanalli(), OutputContainer.Mp4, 40, "ac3");
        var tabanda = Karar(CokKanalli(), OutputContainer.Mp4, 48, "ac3");

        Assert.Equal("aac", Assert.Single(altinda.Audio).Codec);
        Assert.Contains(StreamNote.DolbyCodecBelowChannelFloor, altinda.Notes);

        Assert.Equal("ac3", Assert.Single(tabanda.Audio).Codec);
        Assert.Equal(48, Assert.Single(tabanda.Audio).BitrateK);
        Assert.DoesNotContain(StreamNote.DolbyCodecBelowChannelFloor, tabanda.Notes);
    }

    /// <summary>
    /// "Kaynaktaki gibi" kanal secimi 5.1'i koruyor ve stereo notu dusmuyor; secim
    /// kapaliyken ayni kaynak iki kanala iniyor. Dolby kolunun anlami bu ikiliye bagli.
    /// </summary>
    [Fact]
    public void KaynaktakiGibiKanalSayisiniKoruyor()
    {
        var koruyan = Karar(CokKanalli(), OutputContainer.Mkv, 384, "ac3");
        var indiren = Karar(CokKanalli(), OutputContainer.Mkv, 384, "ac3", kanallariKoru: false);

        Assert.Equal(6, Assert.Single(koruyan.Audio).Channels);
        Assert.DoesNotContain(StreamNote.AudioDownmixedToStereo, koruyan.Notes);

        Assert.Equal(2, Assert.Single(indiren.Audio).Channels);
        Assert.Contains(StreamNote.AudioDownmixedToStereo, indiren.Notes);
    }

    /// <summary>
    /// Butceye giren sayi teslim edilenle ayni: 384k isteyen 5.1 ac3 izi planda da 384k,
    /// 200k isteyen 192k. Olumsuz kontrol aac kolunda kirpmanin olmamasi.
    /// </summary>
    [Fact]
    public void PlandakiBitHiziMerdivendeDuruyor()
    {
        Assert.Equal(384, Assert.Single(Karar(CokKanalli(), OutputContainer.Mkv, 384, "ac3").Audio).BitrateK);
        Assert.Equal(192, Assert.Single(Karar(CokKanalli(), OutputContainer.Mkv, 200, "ac3").Audio).BitrateK);
        Assert.Equal(200, Assert.Single(Karar(CokKanalli(), OutputContainer.Mkv, 200, "aac").Audio).BitrateK);
        Assert.Equal(200, Assert.Single(Karar(CokKanalli(), OutputContainer.Mkv, 200, "eac3").Audio).BitrateK);
    }

    /// <summary>Gelismis paneldeki secim plan secenegine, oradan izin kodegine geciyor.</summary>
    [Theory]
    [InlineData(AudioCodecChoice.Ac3, "ac3")]
    [InlineData(AudioCodecChoice.Eac3, "eac3")]
    [InlineData(AudioCodecChoice.Aac, "aac")]
    [InlineData(AudioCodecChoice.Auto, "aac")]
    public void PanelSecimiIzinKodegineGeciyor(AudioCodecChoice secim, string beklenen)
    {
        var plan = PlanCalculator.Build(CokKanalli(), new PlanOptions
        {
            TargetMb = 100,
            AudioCodec = secim,
            AudioChannels = AudioChannelOverride.Source,
            LockedAudioKbps = 384
        });

        Assert.Equal(beklenen, Assert.Single(plan.Streams!.Audio).Codec);
    }

    /// <summary>
    /// Kalite hedefi yolu secenekleri <c>WithTarget</c> ile kopyalayip her denemede yeniden
    /// planliyor. Kopya ses kodegini tasimazsa kullanicinin Dolby secimi merdivene ulasmadan
    /// AAC'ye duser (main 8f09dc60'ten beri kirmiziydi). Auto olumsuz kontrol.
    /// </summary>
    [Theory]
    [InlineData(AudioCodecChoice.Ac3, "ac3")]
    [InlineData(AudioCodecChoice.Eac3, "eac3")]
    [InlineData(AudioCodecChoice.Auto, "aac")]
    public void KaliteHedefiYoluSecimiKoruyor(AudioCodecChoice secim, string beklenen)
    {
        var sonuc = PlanCalculator.TargetMbForQuality(CokKanalli(), new PlanOptions
        {
            TargetMb = 100,
            AudioCodec = secim,
            AudioChannels = AudioChannelOverride.Source,
            LockedAudioKbps = 384
        }, 60);

        Assert.Equal(beklenen, Assert.Single(sonuc.Plan.Plan.Streams!.Audio).Codec);
    }

    /// <summary>
    /// Akis listesi olmayan kaynakta (eski yoklama, tek ses izi) plan kodegi eslemeden degil
    /// dogrudan secimden geliyor; o kol da secimi okumali.
    /// </summary>
    [Theory]
    [InlineData(AudioCodecChoice.Ac3, "ac3")]
    [InlineData(AudioCodecChoice.Eac3, "eac3")]
    [InlineData(AudioCodecChoice.Auto, "aac")]
    public void AkisListesizKaynaktaSecimOkunuyor(AudioCodecChoice secim, string beklenen)
    {
        var info = Kaynak() with { AudioCodec = "ac3", AudioBitrateBps = 448_000, AudioChannels = 6 };

        var plan = PlanCalculator.Build(info, new PlanOptions { TargetMb = 100, AudioCodec = secim });

        Assert.Equal(beklenen, plan.AudioCodec);
    }

    /// <summary>
    /// <c>IsDolby</c> kapali bir kume: yalniz ac3 ve eac3. Uydurma ve komsu adlarin
    /// kolu acmadigi olumsuz kontrol.
    /// </summary>
    [Fact]
    public void DolbyKumesiKapali()
    {
        Assert.True(StreamMapping.IsDolby("ac3"));
        Assert.True(StreamMapping.IsDolby("eac3"));

        foreach (var yabanci in new string?[] { null, "", "aac", "libopus", "truehd", "dts", "ac4", "ac-3", "dolby" })
            Assert.False(StreamMapping.IsDolby(yabanci));
    }
}
