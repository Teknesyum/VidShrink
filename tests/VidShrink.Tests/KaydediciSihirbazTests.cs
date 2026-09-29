using VidShrink.App.Recorder;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

public sealed class KaydediciSihirbazTests
{
    private static RecorderWizardAnswers Cevap(
        RecorderWizardContent icerik = RecorderWizardContent.General,
        RecorderWizardAudio ses = RecorderWizardAudio.Silent,
        RecorderWizardDestination hedef = RecorderWizardDestination.Share)
        => new(icerik, ses, hedef);

    private static RecorderMachine Makine(int en = 2560, int boy = 1440, double hz = 144, params string[] calisan)
        => new(en, boy, hz, 8, calisan);

    private static RecorderRequest Temel() => new()
    {
        Platform = RecorderPlatform.Windows,
        Target = RecorderTargetKind.Screen
    };

    [Theory]
    [InlineData(RecorderWizardContent.Game, 60)]
    [InlineData(RecorderWizardContent.Lesson, 24)]
    [InlineData(RecorderWizardContent.Meeting, 30)]
    [InlineData(RecorderWizardContent.General, 30)]
    public void IcerikKareHiziTavaniniBelirler(RecorderWizardContent icerik, int beklenen)
        => Assert.Equal(beklenen, RecorderWizard.Plan(Cevap(icerik)).MaxFps);

    [Theory]
    [InlineData(RecorderWizardContent.Game, RecorderWizardDestination.Share, 26)]
    [InlineData(RecorderWizardContent.Lesson, RecorderWizardDestination.Share, 30)]
    [InlineData(RecorderWizardContent.Meeting, RecorderWizardDestination.Share, 28)]
    [InlineData(RecorderWizardContent.General, RecorderWizardDestination.Share, 28)]
    [InlineData(RecorderWizardContent.Game, RecorderWizardDestination.Archive, 18)]
    [InlineData(RecorderWizardContent.Lesson, RecorderWizardDestination.Archive, 18)]
    public void HedefVeIcerikKaliteyiBelirler(RecorderWizardContent icerik, RecorderWizardDestination hedef, double beklenen)
        => Assert.Equal(beklenen, RecorderWizard.Plan(Cevap(icerik, hedef: hedef)).Quality);

    [Theory]
    [InlineData(RecorderWizardAudio.System, false, true)]
    [InlineData(RecorderWizardAudio.Microphone, true, false)]
    [InlineData(RecorderWizardAudio.Both, true, true)]
    [InlineData(RecorderWizardAudio.Silent, false, false)]
    public void SesCevabiIkiKaynagiAyirir(RecorderWizardAudio ses, bool mikrofon, bool sistem)
    {
        var plan = RecorderWizard.Plan(Cevap(ses: ses));
        Assert.Equal((mikrofon, sistem), (plan.Microphone, plan.SystemAudio));
    }

    [Fact]
    public void ArsivKalitesiPaylasimdanYuksektir()
    {
        foreach (var icerik in Enum.GetValues<RecorderWizardContent>())
        {
            var arsiv = RecorderWizard.Plan(Cevap(icerik, hedef: RecorderWizardDestination.Archive));
            var paylas = RecorderWizard.Plan(Cevap(icerik, hedef: RecorderWizardDestination.Share));
            Assert.True(arsiv.Quality < paylas.Quality, $"{icerik}: {arsiv.Quality} / {paylas.Quality}");
            Assert.False(arsiv.ShrinkLargeCapture);
            Assert.True(paylas.ShrinkLargeCapture);
        }
    }

    [Fact]
    public void BicimKareHiziniTavanaIndirirVeTekrariAyiklar()
    {
        var adaylar = RecorderAutoPlan.Candidates(Makine(1920, 1080, 144));
        Assert.Contains(adaylar, a => a.Fps > RecorderWizard.LessonMaxFps);

        var bicimli = RecorderWizard.Shape(adaylar, Cevap(RecorderWizardContent.Lesson), Makine(1920, 1080, 144));

        Assert.All(bicimli, a => Assert.True(a.Fps <= RecorderWizard.LessonMaxFps, $"{a.Fps}"));
        Assert.All(bicimli, a => Assert.Equal(RecorderWizard.ShareLessonQuality, a.Quality));
        Assert.Equal(bicimli.Count, bicimli.Select(a => (a.Fps, a.Scale)).Distinct().Count());
        Assert.True(bicimli.Count < adaylar.Count, $"{bicimli.Count} / {adaylar.Count}");
        Assert.Contains(bicimli, a => a.Notes.Contains(RecorderAutoNote.FpsSteppedDown));
        Assert.DoesNotContain(bicimli, a => a.Notes.Contains(RecorderAutoNote.FpsFollowsRefreshRate));
    }

    [Fact]
    public void OyundaKareHiziTavanAltindaysaDokunulmaz()
    {
        var adaylar = RecorderAutoPlan.Candidates(Makine(1920, 1080, 60));
        var bicimli = RecorderWizard.Shape(adaylar, Cevap(RecorderWizardContent.Game, hedef: RecorderWizardDestination.Archive), Makine(1920, 1080, 60));

        Assert.Equal(adaylar.Select(a => (a.Fps, a.Scale)), bicimli.Select(a => (a.Fps, a.Scale)));
        Assert.Equal(adaylar.Select(a => a.Notes), bicimli.Select(a => a.Notes), new NotKarsilastirici());
    }

    [Fact]
    public void PaylasimdaBuyukYakalamaYariyaIner()
    {
        var makine = Makine(2560, 1440, 60);
        var adaylar = RecorderAutoPlan.Candidates(makine);
        var paylas = RecorderWizard.Shape(adaylar, Cevap(), makine);
        var arsiv = RecorderWizard.Shape(adaylar, Cevap(hedef: RecorderWizardDestination.Archive), makine);

        Assert.All(paylas, a => Assert.Equal(new RecorderScale(1280, 720), a.Scale));
        Assert.DoesNotContain(paylas, a => a.Notes.Contains(RecorderAutoNote.ResolutionKept));
        Assert.Contains(arsiv, a => a.Scale is null);
    }

    [Fact]
    public void PaylasimdaKucukYakalamaKorunur()
    {
        var makine = Makine(1920, 1080, 60);
        var paylas = RecorderWizard.Shape(RecorderAutoPlan.Candidates(makine), Cevap(), makine);

        Assert.Contains(paylas, a => a.Scale is null);
    }

    [Theory]
    [InlineData(RecorderWizardContent.Game, RecorderWizardDestination.Share, "")]
    [InlineData(RecorderWizardContent.Lesson, RecorderWizardDestination.Share, "h264_nvenc")]
    [InlineData(RecorderWizardContent.Meeting, RecorderWizardDestination.Archive, "h264_qsv")]
    [InlineData(RecorderWizardContent.General, RecorderWizardDestination.Archive, "h264_amf")]
    public void HerBicimliAdayMotorunDogrulamasindanGecer(RecorderWizardContent icerik, RecorderWizardDestination hedef, string donanim)
    {
        var calisan = donanim.Length == 0 ? Array.Empty<string>() : new[] { donanim };
        var makine = Makine(2560, 1440, 144, calisan);
        var bicimli = RecorderWizard.Shape(RecorderAutoPlan.Candidates(makine), Cevap(icerik, hedef: hedef), makine);

        Assert.NotEmpty(bicimli);
        foreach (var aday in bicimli)
        {
            var istek = RecorderAutoPlan.Apply(Temel(), aday);
            var hatalar = RecorderArguments.Validate(istek, "kayit." + RecorderArguments.Extension(aday.Container));
            Assert.True(hatalar.Count == 0, string.Join(" ", hatalar));
        }
    }

    [Fact]
    public void UcSoruCevaplaninceAyarKaydedilirVeOzetGorunur()
    {
        var olcu = KaydediciAyarTests.AyarDosyasiyla(ayarYolu => AppHost.Run(() =>
        {
            var olcumSayisi = 0;
            var gorunum = new RecorderView(ayarYolu) { WizardMeasure = () => { olcumSayisi++; return Task.CompletedTask; } };
            KaydediciAyarTests.Elle(gorunum);

            gorunum.OpenWizard();
            var ilkSoru = (gorunum.WizardOpen, gorunum.WizardStep, gorunum.WizardOptions.Count, gorunum.WizardSameOffered);
            gorunum.PickWizardAsync((int)RecorderWizardContent.Lesson).GetAwaiter().GetResult();
            var ikinciSecenek = gorunum.WizardOptions.Count;
            gorunum.PickWizardAsync((int)RecorderWizardAudio.Silent).GetAwaiter().GetResult();
            var ucuncuSecenek = gorunum.WizardOptions.Count;
            gorunum.PickWizardAsync((int)RecorderWizardDestination.Archive).GetAwaiter().GetResult();

            var dosyada = RecorderSettings.Load(ayarYolu);
            var ozet = gorunum.WizardSummary;
            var sonra = (gorunum.WizardOpen, gorunum.AutoMode, olcumSayisi);

            var yeni = new RecorderView(ayarYolu) { WizardMeasure = () => { olcumSayisi++; return Task.CompletedTask; } };
            yeni.OpenWizard();
            var ayniSunuldu = yeni.WizardSameOffered;
            yeni.WizardSameAsync().GetAwaiter().GetResult();

            return (ilkSoru, ikinciSecenek, ucuncuSecenek,
                Kayit: (dosyada.WizardContent, dosyada.WizardAudio, dosyada.WizardDestination),
                ozet, sonra, ayniSunuldu, YeniAcik: yeni.WizardOpen, YeniOzet: yeni.WizardSummary, olcumSayisi);
        }));

        Assert.Equal((true, 0, 4, false), olcu.ilkSoru);
        Assert.Equal(4, olcu.ikinciSecenek);
        Assert.Equal(2, olcu.ucuncuSecenek);
        Assert.Equal((RecorderWizardContent.Lesson, RecorderWizardAudio.Silent, RecorderWizardDestination.Archive), olcu.Kayit);
        Assert.False(string.IsNullOrWhiteSpace(olcu.ozet));
        Assert.Contains("18", olcu.ozet, StringComparison.Ordinal);
        Assert.Equal((false, true, 1), olcu.sonra);
        Assert.True(olcu.ayniSunuldu);
        Assert.False(olcu.YeniAcik);
        Assert.False(string.IsNullOrWhiteSpace(olcu.YeniOzet));
        Assert.Equal(2, olcu.olcumSayisi);
    }

    [Fact]
    public void KayitliCevapYokkenAyniAyarlarSunulmaz()
    {
        var olcu = KaydediciAyarTests.AyarDosyasiyla(ayarYolu => AppHost.Run(() =>
        {
            var gorunum = new RecorderView(ayarYolu) { WizardMeasure = () => Task.CompletedTask };
            gorunum.OpenWizard();
            var sunuldu = gorunum.WizardSameOffered;
            gorunum.WizardSameAsync().GetAwaiter().GetResult();
            return (sunuldu, gorunum.WizardOpen, gorunum.WizardSummary, gorunum.WizardAnswers);
        }));

        Assert.False(olcu.sunuldu);
        Assert.True(olcu.WizardOpen);
        Assert.Equal(string.Empty, olcu.WizardSummary);
        Assert.Null(olcu.WizardAnswers);
    }

    private sealed class NotKarsilastirici : IEqualityComparer<IReadOnlyList<RecorderAutoNote>>
    {
        public bool Equals(IReadOnlyList<RecorderAutoNote>? x, IReadOnlyList<RecorderAutoNote>? y)
            => x is not null && y is not null && x.SequenceEqual(y);

        public int GetHashCode(IReadOnlyList<RecorderAutoNote> obj) => obj.Count;
    }
}
