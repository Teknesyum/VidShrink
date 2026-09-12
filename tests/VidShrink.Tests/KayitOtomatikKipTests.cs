using System;
using System.Collections.Generic;
using System.Linq;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Kaydedicinin otomatik kipi. İki taraf ayrı ölçülüyor: aday merdivenini üreten saf
/// <see cref="RecorderAutoPlan"/> ve kazananı denemeden seçen
/// <see cref="RecorderAutoProbe"/>'un seçim kuralı.
///
/// <para>Süreç çalıştırılmıyor: gerçek deneme kaydı ekran ister ve CI'da ekran yok. Ölçülen
/// şey kararın kendisi — hangi kodlayıcı, hangi kare hızı, hangi aday kazanıyor.</para>
/// </summary>
public class KayitOtomatikKipTests
{
    private static RecorderMachine Makine(
        int width = 1920, int height = 1080, double hz = 60,
        int cores = 8, params string[] working)
        => new(width, height, hz, cores, working);

    [Fact]
    public void DonanimYokkenYazilimKoluSeciliyor()
    {
        Assert.Equal(RecorderAutoPlan.SoftwareCodec, RecorderAutoPlan.CodecFor(Array.Empty<string>()));
        Assert.Equal(RecorderAutoPlan.SoftwareCodec, RecorderAutoPlan.CodecFor(null));

        var ladder = RecorderAutoPlan.Candidates(Makine());
        Assert.All(ladder, aday => Assert.Equal("libx264", aday.VideoCodec));
    }

    [Fact]
    public void YeglenenSiraNvencQsvAmf()
    {
        Assert.Equal("h264_nvenc", RecorderAutoPlan.CodecFor(new[] { "h264_amf", "h264_qsv", "h264_nvenc" }));
        Assert.Equal("h264_qsv", RecorderAutoPlan.CodecFor(new[] { "h264_amf", "h264_qsv" }));
        Assert.Equal("h264_amf", RecorderAutoPlan.CodecFor(new[] { "h264_amf" }));
    }

    /// <summary>
    /// Kapalı küme: listede duran ama yeğlenenler arasında bulunmayan bir ad seçilmiyor.
    /// Uydurma bir kodlayıcı adı da seçilmiyor — negatif kontrol.
    /// </summary>
    [Fact]
    public void YeglenmeyenVeUydurmaKodlayiciSecilmiyor()
    {
        Assert.Equal(RecorderAutoPlan.SoftwareCodec, RecorderAutoPlan.CodecFor(new[] { "hevc_nvenc", "av1_nvenc" }));
        Assert.Equal(RecorderAutoPlan.SoftwareCodec, RecorderAutoPlan.CodecFor(new[] { "h264_teknesyum" }));
    }

    /// <summary>
    /// Kare hızı kapalı merdivenden çıkıyor: 75 Hz'lik bir ekran 75 değil 60 alıyor,
    /// okunamayan hız <see cref="RecorderAutoPlan.FallbackFps"/>'e düşüyor.
    /// </summary>
    [Theory]
    [InlineData(0, 30)]
    [InlineData(-1, 30)]
    [InlineData(23.5, 24)]
    [InlineData(24, 24)]
    [InlineData(59.94, 60)]
    [InlineData(75, 60)]
    [InlineData(144, 120)]
    [InlineData(240, 120)]
    public void KareHiziMerdivendenSeciliyor(double hz, int beklenen)
        => Assert.Equal(beklenen, RecorderAutoPlan.FpsFor(hz));

    [Fact]
    public void MerdivenAsagiBasamakDondurmeyiBiliyor()
    {
        Assert.Equal(60, RecorderAutoPlan.StepDown(120));
        Assert.Equal(30, RecorderAutoPlan.StepDown(60));
        Assert.Equal(24, RecorderAutoPlan.StepDown(30));
        Assert.Null(RecorderAutoPlan.StepDown(24));
    }

    /// <summary>
    /// Yarı boyut <c>yuv420p</c> için çift olmak zorunda; ölçeklenemeyecek kadar küçük
    /// yakalamaya ikinci aday üretilmiyor.
    /// </summary>
    [Fact]
    public void YariBoyutCiftVeMotorunDogrulamasindanGeciyor()
    {
        var half = RecorderAutoPlan.Halved(1366, 769);
        Assert.NotNull(half);
        Assert.Equal(0, half!.Width % 2);
        Assert.Equal(0, half.Height % 2);
        Assert.Equal(682, half.Width);
        Assert.Equal(384, half.Height);

        Assert.Null(RecorderAutoPlan.Halved(2, 2));
        Assert.Null(RecorderAutoPlan.Halved(0, 0));
    }

    [Fact]
    public void MerdivenTavaniAsmiyorVeOnceKareHiziniIndiriyor()
    {
        var ladder = RecorderAutoPlan.Candidates(Makine(hz: 120));

        Assert.InRange(ladder.Count, 1, RecorderAutoPlan.MaxCandidates);
        Assert.Equal(120, ladder[0].Fps);
        Assert.Null(ladder[0].Scale);
        Assert.Equal(60, ladder[1].Fps);
        Assert.Null(ladder[1].Scale);
        Assert.Equal(120, ladder[2].Fps);
        Assert.NotNull(ladder[2].Scale);
    }

    /// <summary>
    /// Otomatik kipin yazdığı her aday motorun kendi doğrulamasından geçiyor: uydurma bir
    /// ön ayar, kabul edilmeyen bir piksel biçimi ya da tek sayılı bir ölçek buraya
    /// sızamıyor.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("h264_nvenc")]
    [InlineData("h264_qsv")]
    [InlineData("h264_amf")]
    public void HerAdayMotorunDogrulamasindanGeciyor(string donanim)
    {
        var working = donanim.Length == 0 ? Array.Empty<string>() : new[] { donanim };
        var ladder = RecorderAutoPlan.Candidates(Makine(1366, 769, 144, 8, working));

        foreach (var aday in ladder)
        {
            var request = RecorderAutoPlan.Apply(Temel(), aday);
            var errors = RecorderArguments.Validate(request, "kayit." + RecorderArguments.Extension(aday.Container));
            Assert.True(errors.Count == 0, string.Join(" ", errors));
        }
    }

    /// <summary>Kap her adayda Matroska: öldürülen kayıt oynatılabilir kalıyor.</summary>
    [Fact]
    public void KapOldurulenKaydiTasiyor()
    {
        var ladder = RecorderAutoPlan.Candidates(Makine());
        Assert.All(ladder, aday => Assert.True(RecorderArguments.SurvivesKill(aday.Container)));
    }

    /// <summary>
    /// Ön ayar kodlayıcının kendi sözlüğünden: nvenc'in <c>p</c> ölçeği, AMF'nin adı ve
    /// x264 ailesinin adı ayrı kümeler, tek bir değer hepsine yazılmıyor.
    /// </summary>
    [Fact]
    public void OnAyarKodlayicininSozlugunden()
    {
        Assert.Equal("p4", RecorderAutoPlan.PresetFor("h264_nvenc"));
        Assert.Equal("speed", RecorderAutoPlan.PresetFor("h264_amf"));
        Assert.Equal("veryfast", RecorderAutoPlan.PresetFor("h264_qsv"));
        Assert.Equal(RecorderArguments.DefaultPreset, RecorderAutoPlan.PresetFor("libx264"));
    }

    /// <summary>
    /// <see cref="RecorderAutoPlan.Apply"/> kullanıcının hedefini, bölgesini ve ses kolunu
    /// korurken bit hızı kolundan kalan sayıları temizliyor: kalite koluna geçen bir istek
    /// <c>-b:v</c> taşımıyor ve doğrulama bunu reddederdi.
    /// </summary>
    [Fact]
    public void UygulamaHedefiKoruyorBitHiziKolunuTemizliyor()
    {
        var temel = Temel() with
        {
            Target = RecorderTargetKind.Region,
            Region = new RecorderRegion(10, 20, 640, 480),
            RateControl = RecorderRateControl.Bitrate,
            BitrateKbps = 8000,
            MaxBitrateKbps = 12000,
            BufferKbits = 16000,
            ShowCursor = false
        };

        var aday = RecorderAutoPlan.Candidates(Makine())[0];
        var sonuc = RecorderAutoPlan.Apply(temel, aday);

        Assert.Equal(RecorderTargetKind.Region, sonuc.Target);
        Assert.Equal(temel.Region, sonuc.Region);
        Assert.False(sonuc.ShowCursor);
        Assert.Equal(RecorderRateControl.Quality, sonuc.RateControl);
        Assert.Null(sonuc.BitrateKbps);
        Assert.Null(sonuc.MaxBitrateKbps);
        Assert.Null(sonuc.BufferKbits);
        Assert.Empty(RecorderArguments.Validate(sonuc, "kayit.mkv"));
    }

    /// <summary>
    /// Yoklamanın üç değerli cevabı: yalnız <see cref="EncoderProbeState.Working"/> çalışıyor
    /// sayılıyor. Sürücüsü olmayan makinede <c>h264_nvenc</c> listede duruyor ama
    /// kodlamıyor — <see cref="EncoderProbeState.Unmeasured"/> de seçilmiyor.
    /// </summary>
    [Fact]
    public void YalnizCalistigiOlculenDonanimSeciliyor()
    {
        var probe = new SahteYoklama(new Dictionary<string, EncoderProbeState>
        {
            ["h264_nvenc"] = EncoderProbeState.Unmeasured,
            ["h264_qsv"] = EncoderProbeState.NotWorking,
            ["h264_amf"] = EncoderProbeState.Working
        });

        var working = RecorderAutoProbe.WorkingHardwareEncoders(probe);

        Assert.Equal(new[] { "h264_amf" }, working);
        Assert.Equal("h264_amf", RecorderAutoPlan.CodecFor(working));
    }

    /// <summary>
    /// Kazanma kuralı: kare düşürmeyen aday kazanır, hiçbiri sıfır değilse oranı en küçük
    /// olan kazanır, tamamlanmayan deneme kazanamaz.
    /// </summary>
    [Fact]
    public void OlculemeyenDenemeKazanmiyor()
    {
        var ladder = RecorderAutoPlan.Candidates(Makine(hz: 120));
        var basarisiz = new RecorderTrial(ladder[0], 0, 0, false, "hata");
        var basarili = new RecorderTrial(ladder[1], 300, 12, true, null);

        Assert.Equal(1.0, basarisiz.DropRatio);
        Assert.True(basarili.DropRatio < basarisiz.DropRatio);
        Assert.Equal(12.0 / 312.0, basarili.DropRatio, 6);
    }

    [Fact]
    public void KareDusurmeyenAdaySifirOraniniVeriyor()
    {
        var aday = RecorderAutoPlan.Candidates(Makine())[0];
        Assert.Equal(0.0, new RecorderTrial(aday, 180, 0, true, null).DropRatio);
        Assert.Equal(1.0, new RecorderTrial(aday, 0, 0, true, null).DropRatio);
    }

    [Fact]
    public async System.Threading.Tasks.Task BosMerdivenReddediliyor()
        => await Assert.ThrowsAsync<ArgumentException>(() => RecorderAutoProbe.ChooseAsync(
            Temel(), ".calisma", Array.Empty<RecorderAutoChoice>()));

    private static RecorderRequest Temel() => new()
    {
        Platform = RecorderPlatform.Windows,
        Target = RecorderTargetKind.Screen
    };

    private sealed class SahteYoklama : IEncoderProbeState
    {
        private readonly IReadOnlyDictionary<string, EncoderProbeState> _states;

        internal SahteYoklama(IReadOnlyDictionary<string, EncoderProbeState> states) => _states = states;

        public EncoderProbeState WorksAsEncoderState(string codec)
            => _states.TryGetValue(codec, out var state) ? state : EncoderProbeState.NotWorking;
    }
}
