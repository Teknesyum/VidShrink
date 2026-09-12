using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// 9c kolu: ekran kaydinin ffmpeg argumanina eklenen on kol. Her olcu <b>uretilen arguman
/// dizisini</b> okuyor — sabiti sabitle karsilastiran bir olcu bu kolda hicbir mutasyonu
/// kirmazdi, cunku kollarin hepsi ayni istekten cikan bir listeye yaziyor.
/// <para>
/// Her kolun bir negatif kontrolu var: uydurma bir kap, tek sayili bir olcek, tanimsiz bir
/// profil ya da hedefsiz bir bit hizi kolu sessizce yutulmuyor,
/// <see cref="RecorderArguments.Validate"/> satiriyla geri donuyor. Bu depoda SVT-AV1'in
/// tanimadigi anahtari sessizce yuttugu ve uydurma anahtarin da "kabul" dondugu olculdu;
/// kapali kumeler o tuzagi kapatiyor.
/// </para>
/// </summary>
public sealed class KayitFfmpegKoluTests
{
    private static RecorderRequest Istek(RecorderPlatform platform = RecorderPlatform.Windows) => new()
    {
        Platform = platform,
        Target = RecorderTargetKind.Screen
    };

    private static string Metin(IReadOnlyList<string> args) => string.Join(" ", args);

    /// <summary>Bir bayragin degeri; bayrak yoksa <c>null</c>.</summary>
    private static string? Deger(IReadOnlyList<string> args, string flag)
    {
        for (var i = 0; i < args.Count - 1; i++)
            if (string.Equals(args[i], flag, StringComparison.Ordinal))
                return args[i + 1];
        return null;
    }

    // 1 — kayit kabi

    [Theory]
    [InlineData(RecorderContainer.Mp4, "mp4")]
    [InlineData(RecorderContainer.Mkv, "mkv")]
    [InlineData(RecorderContainer.Mov, "mov")]
    public void KapKendiUzantisiniVerir(RecorderContainer kap, string uzanti)
    {
        Assert.Equal(uzanti, RecorderArguments.Extension(kap));
        Assert.Equal(kap, RecorderArguments.ContainerOf($"C:\\kayit\\a.{uzanti}"));
        Assert.Equal(kap, RecorderArguments.ContainerOf(uzanti));
    }

    /// <summary>
    /// <c>+faststart</c> mp4/mov muxer'inin isi; Matroska'da boyle bir atom yok ve bayrak
    /// yazilmiyor. Ayni istek yalniz kap degisince iki farkli arguman veriyor.
    /// </summary>
    [Fact]
    public void MatroskaFaststartYazmazMp4Yazar()
    {
        var mkv = RecorderArguments.Build(Istek() with { Container = RecorderContainer.Mkv }, @"C:\kayit\a.mkv");
        var mp4 = RecorderArguments.Build(Istek() with { Container = RecorderContainer.Mp4 }, @"C:\kayit\a.mp4");
        var mov = RecorderArguments.Build(Istek() with { Container = RecorderContainer.Mov }, @"C:\kayit\a.mov");

        Assert.DoesNotContain("-movflags", mkv);
        Assert.Equal("+faststart", Deger(mp4, "-movflags"));
        Assert.Equal("+faststart", Deger(mov, "-movflags"));
        Assert.Equal(@"C:\kayit\a.mkv", mkv[^1]);
    }

    /// <summary>
    /// Kabin secilme sebebi: oldurulen kayitta yalniz Matroska oynatilabilir kaliyor.
    /// <c>RecordResult.Partial</c> yolunun maliyeti burada dusuyor.
    /// </summary>
    [Fact]
    public void OldurulenKaydiYalnizMatroskaTasir()
    {
        Assert.True(RecorderArguments.SurvivesKill(RecorderContainer.Mkv));
        Assert.False(RecorderArguments.SurvivesKill(RecorderContainer.Mp4));
        Assert.False(RecorderArguments.SurvivesKill(RecorderContainer.Mov));
    }

    /// <summary>Negatif kontrol: tanimsiz uzanti ve kapla ayrisan uzanti reddedilir.</summary>
    [Fact]
    public void UydurmaUzantiVeAyrisanKapReddedilir()
    {
        Assert.Null(RecorderArguments.ContainerOf("a.zzz"));
        Assert.Null(RecorderArguments.ContainerOf(""));

        var uydurma = RecorderArguments.Validate(Istek(), @"C:\kayit\a.zzz");
        var ayrisan = RecorderArguments.Validate(Istek() with { Container = RecorderContainer.Mkv }, @"C:\kayit\a.mp4");

        Assert.Contains(uydurma, satir => satir.Contains("not one of the recorder's containers"));
        Assert.Contains(ayrisan, satir => satir.Contains("mp4") && satir.Contains("mkv"));
        Assert.Empty(RecorderArguments.Validate(Istek() with { Container = RecorderContainer.Mkv }, @"C:\kayit\a.mkv"));
    }

    // 2 — cikti cozunurlugu

    [Fact]
    public void OlceklemeVfZincirineGirer()
    {
        var args = RecorderArguments.Build(Istek() with { Scale = new RecorderScale(1280, 720) }, @"C:\kayit\a.mp4");

        Assert.Equal("scale=1280:720", Deger(args, "-vf"));
    }

    /// <summary>
    /// macOS'ta kirpma ve olcekleme <b>ayni</b> zincire giriyor ve sira sabit: once
    /// <c>crop</c>, sonra <c>scale</c>. Ters sirada dikdortgenin koordinatlari olceklenmis
    /// kareye duser ve kullanicinin sectigi yer kayar.
    /// </summary>
    [Fact]
    public void KirpmaOlceklemedenOnceGelir()
    {
        var args = RecorderArguments.Build(
            Istek(RecorderPlatform.MacOs) with
            {
                Target = RecorderTargetKind.Region,
                Region = new RecorderRegion(10, 30, 640, 480),
                Scale = new RecorderScale(320, 240)
            },
            "/tmp/a.mp4");

        Assert.Equal("crop=640:480:10:30,scale=320:240", Deger(args, "-vf"));
    }

    /// <summary>Olcekleme secilmemisse zincir hic kurulmuyor; bugunku arguman degismiyor.</summary>
    [Fact]
    public void OlceklemeYokkenVfYazilmaz()
    {
        var args = RecorderArguments.Build(Istek(), @"C:\kayit\a.mp4");

        Assert.DoesNotContain("-vf", args);
    }

    /// <summary>Negatif kontrol: tek sayili ve sifir olcek reddedilir.</summary>
    [Fact]
    public void KabulEdilemezOlcekReddedilir()
    {
        var tek = RecorderArguments.Validate(Istek() with { Scale = new RecorderScale(1281, 720) }, @"C:\kayit\a.mp4");
        var sifir = RecorderArguments.Validate(Istek() with { Scale = new RecorderScale(0, 720) }, @"C:\kayit\a.mp4");

        Assert.Contains(tek, satir => satir.Contains("even"));
        Assert.Contains(sifir, satir => satir.Contains("positive"));
    }

    // 3 — anahtar kare, profil, tune

    /// <summary>
    /// <c>-g</c> kare cinsinden: saniye cinsinden aralik kare hiziyla carpiliyor. Ayni
    /// aralik iki kare hizinda iki farkli sayi veriyor — sabit bir <c>-g</c> bu olcuyu
    /// kirar.
    /// </summary>
    [Theory]
    [InlineData(30, 2, "60")]
    [InlineData(15, 2, "30")]
    [InlineData(60, 4, "240")]
    public void AnahtarKareAraligiKareHizindanCikar(int fps, int saniye, string beklenen)
    {
        var args = RecorderArguments.Build(Istek() with { Fps = fps, KeyframeSeconds = saniye }, @"C:\kayit\a.mp4");

        Assert.Equal(beklenen, Deger(args, "-g"));
    }

    [Fact]
    public void SifirAralikAnahtarKareyiKodlayiciyaBirakir()
    {
        var args = RecorderArguments.Build(Istek() with { KeyframeSeconds = 0 }, @"C:\kayit\a.mp4");

        Assert.DoesNotContain("-g", args);
    }

    [Fact]
    public void ProfilVeTuneKendiBayraklariniVerir()
    {
        var args = RecorderArguments.Build(
            Istek() with { VideoCodec = "libx264", Profile = "high", Tune = "zerolatency" },
            @"C:\kayit\a.mp4");

        Assert.Equal("high", Deger(args, "-profile:v"));
        Assert.Equal("zerolatency", Deger(args, "-tune"));
    }

    [Fact]
    public void SecilmeyenProfilVeTuneYazilmaz()
    {
        var args = RecorderArguments.Build(Istek(), @"C:\kayit\a.mp4");

        Assert.DoesNotContain("-profile:v", args);
        Assert.DoesNotContain("-tune", args);
    }

    /// <summary>
    /// Kabul edilen kume kodege gore degisiyor: <c>high10</c> h264'un, <c>main10</c>
    /// hevc'nin. Birini otekinin kodegine vermek reddediliyor.
    /// </summary>
    [Fact]
    public void ProfilKumesiKodegeGoreAyrisir()
    {
        Assert.Contains("high10", RecorderArguments.ProfilesFor("libx264"));
        Assert.DoesNotContain("high10", RecorderArguments.ProfilesFor("libx265"));
        Assert.Contains("main10", RecorderArguments.ProfilesFor("hevc_nvenc"));
        Assert.Empty(RecorderArguments.ProfilesFor("libsvtav1"));

        var yanlisAile = RecorderArguments.Validate(
            Istek() with { VideoCodec = "libx265", Profile = "high10" }, @"C:\kayit\a.mp4");

        Assert.Contains(yanlisAile, satir => satir.Contains("high10") && satir.Contains("libx265"));
    }

    /// <summary>
    /// Negatif kontrol: uydurma profil, uydurma tune ve <c>-tune</c> almayan kodege
    /// verilen tune reddedilir; kabul edilen deger gecer.
    /// </summary>
    [Fact]
    public void UydurmaProfilVeTuneReddedilir()
    {
        var profil = RecorderArguments.Validate(
            Istek() with { VideoCodec = "libx264", Profile = "ultrahigh9000" }, @"C:\kayit\a.mp4");
        var tune = RecorderArguments.Validate(
            Istek() with { VideoCodec = "libx264", Tune = "hiznerede" }, @"C:\kayit\a.mp4");
        var tunesuzKodek = RecorderArguments.Validate(
            Istek() with { VideoCodec = "libsvtav1", Tune = "film" }, @"C:\kayit\a.mp4");
        var profilsizKodek = RecorderArguments.Validate(
            Istek() with { VideoCodec = "libsvtav1", Profile = "main" }, @"C:\kayit\a.mp4");
        var uzunAralik = RecorderArguments.Validate(
            Istek() with { KeyframeSeconds = RecorderArguments.MaxKeyframeSeconds + 1 }, @"C:\kayit\a.mp4");
        var negatifAralik = RecorderArguments.Validate(Istek() with { KeyframeSeconds = -1 }, @"C:\kayit\a.mp4");

        Assert.Contains(profil, satir => satir.Contains("ultrahigh9000"));
        Assert.Contains(tune, satir => satir.Contains("hiznerede"));
        Assert.Contains(tunesuzKodek, satir => satir.Contains("no -tune"));
        Assert.Contains(profilsizKodek, satir => satir.Contains("no -profile:v"));
        Assert.Contains(uzunAralik, satir => satir.Contains("Keyframe interval"));
        Assert.Contains(negatifAralik, satir => satir.Contains("negative"));
        Assert.Empty(RecorderArguments.Validate(
            Istek() with { VideoCodec = "libx264", Profile = "main", Tune = "film" }, @"C:\kayit\a.mp4"));
    }

    // 4 — hedef bit hizi kolu

    /// <summary>
    /// Iki kol ayrisiyor: kalite kolu <c>-crf</c> yaziyor ve <c>-b:v</c> yazmiyor, bit hizi
    /// kolu tam tersi. Ayni istek yalniz kol degisince iki farkli arguman veriyor.
    /// </summary>
    [Fact]
    public void BitHiziKoluCrfYerineBvYazar()
    {
        var kalite = RecorderArguments.Build(Istek(), @"C:\kayit\a.mp4");
        var hiz = RecorderArguments.Build(
            Istek() with { RateControl = RecorderRateControl.Bitrate, BitrateKbps = 6000 },
            @"C:\kayit\a.mp4");

        Assert.Equal("23", Deger(kalite, "-crf"));
        Assert.DoesNotContain("-b:v", kalite);

        Assert.Equal("6000k", Deger(hiz, "-b:v"));
        Assert.DoesNotContain("-crf", hiz);
    }

    [Fact]
    public void TavanVeTamponVerildigindeYazilir()
    {
        var args = RecorderArguments.Build(
            Istek() with
            {
                RateControl = RecorderRateControl.Bitrate,
                BitrateKbps = 6000,
                MaxBitrateKbps = 9000,
                BufferKbits = 12000
            },
            @"C:\kayit\a.mp4");

        Assert.Equal("9000k", Deger(args, "-maxrate"));
        Assert.Equal("12000k", Deger(args, "-bufsize"));
    }

    /// <summary>
    /// Bit hizi kolu <see cref="CodecModel.BitrateRateControlArgs"/>i kullaniyor: kayit
    /// kolu kendi hiz kontrolunu uydurmuyor, saticinin olcegini motordan okuyor. NVENC
    /// ayni istekte yazilim kodegiyle ayni argumani vermiyor.
    /// </summary>
    [Fact]
    public void SaticininHizKontroluMotordanOkunur()
    {
        var yazilim = RecorderArguments.Build(
            Istek() with { VideoCodec = "libx264", RateControl = RecorderRateControl.Bitrate, BitrateKbps = 6000 },
            @"C:\kayit\a.mp4");
        var nvenc = RecorderArguments.Build(
            Istek() with { VideoCodec = "h264_nvenc", RateControl = RecorderRateControl.Bitrate, BitrateKbps = 6000 },
            @"C:\kayit\a.mp4");

        foreach (var arg in CodecModel.BitrateRateControlArgs("h264_nvenc")) Assert.Contains(arg, nvenc);
        Assert.Equal("vbr", Deger(nvenc, "-rc"));
        Assert.DoesNotContain("-rc", yazilim);
        Assert.NotEqual(Metin(yazilim), Metin(nvenc));
    }

    /// <summary>
    /// Negatif kontrol: hedefsiz bit hizi kolu, sifir hedef, hedefin altinda tavan ve
    /// kalite kolunda birakilmis bit hizi degeri reddedilir. Kayit icin olculmus bir hedef
    /// bit hizi yok, o yuzden motor varsayilan uydurmuyor.
    /// </summary>
    [Fact]
    public void EksikVeTutarsizBitHiziReddedilir()
    {
        var hedefsiz = RecorderArguments.Validate(
            Istek() with { RateControl = RecorderRateControl.Bitrate }, @"C:\kayit\a.mp4");
        var sifir = RecorderArguments.Validate(
            Istek() with { RateControl = RecorderRateControl.Bitrate, BitrateKbps = 0 }, @"C:\kayit\a.mp4");
        var alcakTavan = RecorderArguments.Validate(
            Istek() with { RateControl = RecorderRateControl.Bitrate, BitrateKbps = 6000, MaxBitrateKbps = 3000 },
            @"C:\kayit\a.mp4");
        var yanlisKol = RecorderArguments.Validate(
            Istek() with { RateControl = RecorderRateControl.Quality, BitrateKbps = 6000 }, @"C:\kayit\a.mp4");

        Assert.Contains(hedefsiz, satir => satir.Contains("no measured default"));
        Assert.Contains(sifir, satir => satir.Contains("greater than zero"));
        Assert.Contains(alcakTavan, satir => satir.Contains("below the target"));
        Assert.Contains(yanlisKol, satir => satir.Contains("only belong to the bitrate"));
    }

    // 5 — piksel bicimi ve renk uzayi

    [Fact]
    public void PikselBicimiVeRenkKendiBayraklariniVerir()
    {
        var args = RecorderArguments.Build(
            Istek() with { PixelFormat = "yuv444p", ColorSpace = "bt709", ColorRange = "pc" },
            @"C:\kayit\a.mp4");

        Assert.Equal("yuv444p", Deger(args, "-pix_fmt"));
        Assert.Equal("bt709", Deger(args, "-colorspace"));
        Assert.Equal("pc", Deger(args, "-color_range"));
    }

    /// <summary>Renk uzayi ve araligi secilmezse yazilmiyor; piksel bicimi her zaman var.</summary>
    [Fact]
    public void SecilmeyenRenkAyarlariYazilmaz()
    {
        var args = RecorderArguments.Build(Istek(), @"C:\kayit\a.mp4");

        Assert.Equal(RecorderArguments.DefaultPixelFormat, Deger(args, "-pix_fmt"));
        Assert.DoesNotContain("-colorspace", args);
        Assert.DoesNotContain("-color_range", args);
    }

    /// <summary>Negatif kontrol: uydurma bicim, uzay ve aralik reddedilir.</summary>
    [Fact]
    public void UydurmaRenkAyarlariReddedilir()
    {
        var bicim = RecorderArguments.Validate(Istek() with { PixelFormat = "yuv999p" }, @"C:\kayit\a.mp4");
        var uzay = RecorderArguments.Validate(Istek() with { ColorSpace = "bt9999" }, @"C:\kayit\a.mp4");
        var aralik = RecorderArguments.Validate(Istek() with { ColorRange = "orta" }, @"C:\kayit\a.mp4");
        var bos = RecorderArguments.Validate(Istek() with { PixelFormat = "  " }, @"C:\kayit\a.mp4");

        Assert.Contains(bicim, satir => satir.Contains("yuv999p"));
        Assert.Contains(uzay, satir => satir.Contains("bt9999"));
        Assert.Contains(aralik, satir => satir.Contains("orta"));
        Assert.Contains(bos, satir => satir.Contains("Pixel format is required"));
        Assert.All(RecorderArguments.PixelFormats, deger =>
            Assert.Empty(RecorderArguments.Validate(Istek() with { PixelFormat = deger }, @"C:\kayit\a.mp4")));
    }

    // 6 — sure siniri ve bolme

    [Fact]
    public void SureSiniriTBayragiVerir()
    {
        var args = RecorderArguments.Build(
            Istek() with { MaxDuration = TimeSpan.FromMinutes(2.5) }, @"C:\kayit\a.mp4");

        Assert.Equal("150", Deger(args, "-t"));
        Assert.DoesNotContain("-t", RecorderArguments.Build(Istek(), @"C:\kayit\a.mp4"));
    }

    /// <summary>
    /// Bolme olcutu arguman uretmiyor: parcalari <c>RecorderSession</c> aciyor. Olcut
    /// verilmis bir istek, verilmemis istegin argumaniyla birebir ayni kaliyor.
    /// </summary>
    [Fact]
    public void BolmeOlcutuArgumanaGirmez()
    {
        var bolmesiz = RecorderArguments.Build(Istek(), @"C:\kayit\a.mp4");
        var bolmeli = RecorderArguments.Build(
            Istek() with { Split = new RecorderSplit(TimeSpan.FromMinutes(5), 512) }, @"C:\kayit\a.mp4");

        Assert.Equal(bolmesiz, bolmeli);
    }

    /// <summary>Negatif kontrol: olcutsuz bolme, sifir olcut ve sinirdan uzun bolum reddedilir.</summary>
    [Fact]
    public void KabulEdilemezSinirVeBolmeReddedilir()
    {
        var olcutsuz = RecorderArguments.Validate(Istek() with { Split = new RecorderSplit() }, @"C:\kayit\a.mp4");
        var sifirSure = RecorderArguments.Validate(
            Istek() with { Split = new RecorderSplit(TimeSpan.Zero) }, @"C:\kayit\a.mp4");
        var sifirBoyut = RecorderArguments.Validate(
            Istek() with { Split = new RecorderSplit(Megabytes: 0) }, @"C:\kayit\a.mp4");
        var sifirSinir = RecorderArguments.Validate(Istek() with { MaxDuration = TimeSpan.Zero }, @"C:\kayit\a.mp4");
        var sinirdanUzun = RecorderArguments.Validate(
            Istek() with
            {
                MaxDuration = TimeSpan.FromMinutes(1),
                Split = new RecorderSplit(TimeSpan.FromMinutes(5))
            },
            @"C:\kayit\a.mp4");

        Assert.Contains(olcutsuz, satir => satir.Contains("needs a duration or a size"));
        Assert.Contains(sifirSure, satir => satir.Contains("Split duration"));
        Assert.Contains(sifirBoyut, satir => satir.Contains("Split size"));
        Assert.Contains(sifirSinir, satir => satir.Contains("time limit"));
        Assert.Contains(sinirdanUzun, satir => satir.Contains("cannot be longer than"));
        Assert.False(new RecorderSplit().IsSet);
        Assert.True(new RecorderSplit(TimeSpan.FromSeconds(1)).IsSet);
    }

    // 7 — ayri ses izleri

    private static readonly AudioCaptureDevice Mikrofon =
        new("Mikrofon", CaptureBackend.DirectShow, AudioSourceRole.Microphone);

    private static readonly AudioCaptureDevice SistemSesi =
        new("Hoparlor", CaptureBackend.DirectShow, AudioSourceRole.SystemAudio);

    private static readonly AudioCaptureDevice[] Liste = { Mikrofon, SistemSesi };

    private static AudioCapturePlan Plan(AudioTrackLayout duzen, AudioFilterOptions? filtreler = null)
        => AudioCaptureArguments.Build(
            new AudioCaptureSelection(Mikrofon, SistemSesi), Liste,
            RecorderArguments.AudioFirstInputIndex, duzen, filtreler);

    /// <summary>
    /// Ayri iz kolunda <c>amix</c> hic kurulmuyor ve iki girdi kendi <c>-map</c>'ini
    /// aliyor. Karistirilan kol ayni secimde tek esleme ve bir <c>amix</c> grafigi
    /// veriyordu.
    /// </summary>
    [Fact]
    public void AyriIzlerAmixYerineIkiMapVerir()
    {
        var karisik = Plan(AudioTrackLayout.MixedSingleTrack);
        var ayri = Plan(AudioTrackLayout.SeparateTracks);

        Assert.Contains(AudioCaptureArguments.MixFilterName, karisik.FilterComplex!);
        Assert.Equal(new[] { $"[{AudioCaptureArguments.MixOutputLabel}]" }, karisik.Maps);

        Assert.Null(ayri.FilterComplex);
        Assert.Equal(new[] { "1:a", "2:a" }, ayri.Maps);
        Assert.Equal(karisik.Inputs, ayri.Inputs);
    }

    /// <summary>
    /// Iki iz ciktida her biri kendi <c>-c:a:N</c>'ini aliyor; tek iz tek <c>-c:a</c>
    /// aliyor. Arguman dizisinden hangi izin hangi kodlayiciya gittigi okunabiliyor.
    /// </summary>
    [Fact]
    public void HerIzKendiSesKodlayicisiniAlir()
    {
        var ayri = RecorderArguments.Build(
            Istek() with { Audio = Plan(AudioTrackLayout.SeparateTracks) }, @"C:\kayit\a.mp4");
        var karisik = RecorderArguments.Build(
            Istek() with { Audio = Plan(AudioTrackLayout.MixedSingleTrack) }, @"C:\kayit\a.mp4");

        Assert.Equal(RecorderArguments.AudioCodec, Deger(ayri, "-c:a:0"));
        Assert.Equal(RecorderArguments.AudioCodec, Deger(ayri, "-c:a:1"));
        Assert.Equal(RecorderArguments.AudioBitrate, Deger(ayri, "-b:a:0"));
        Assert.DoesNotContain("-c:a", ayri);

        Assert.Equal(RecorderArguments.AudioCodec, Deger(karisik, "-c:a"));
        Assert.DoesNotContain("-c:a:0", karisik);

        Assert.Equal(2, ayri.Count(a => a == "-map") - 1);
    }

    /// <summary>
    /// Negatif kontrol: tek cihaz secildiginde ayri iz kolu ikinci bir iz uydurmuyor ve
    /// tanimsiz bir duzen sessizce karistirmaya dusmuyor.
    /// </summary>
    [Fact]
    public void TekCihazdaAyriIzKoluIkinciIzUretmez()
    {
        var tek = AudioCaptureArguments.Build(
            new AudioCaptureSelection(Mikrofon), Liste,
            RecorderArguments.AudioFirstInputIndex, AudioTrackLayout.SeparateTracks, null);

        Assert.Equal(new[] { "1:a" }, tek.Maps);
        Assert.Null(tek.FilterComplex);

        var hata = Assert.Throws<ArgumentOutOfRangeException>(() => AudioCaptureArguments.Build(
            new AudioCaptureSelection(Mikrofon, SistemSesi), Liste,
            RecorderArguments.AudioFirstInputIndex, (AudioTrackLayout)77, null));
        Assert.Contains("tanimsiz ses izi duzeni", hata.Message);
    }

    // 8 — ses filtreleri

    [Fact]
    public void SesFiltreleriZincirOlarakKurulur()
    {
        Assert.Null(AudioCaptureArguments.FilterChain(AudioFilterOptions.None));
        Assert.Equal("volume=6dB", AudioCaptureArguments.FilterChain(new AudioFilterOptions(6)));
        Assert.Equal("agate", AudioCaptureArguments.FilterChain(new AudioFilterOptions(NoiseGate: true)));
        Assert.Equal("afftdn", AudioCaptureArguments.FilterChain(new AudioFilterOptions(NoiseSuppression: true)));
        Assert.Equal(
            "volume=-3.5dB,agate,afftdn",
            AudioCaptureArguments.FilterChain(new AudioFilterOptions(-3.5, true, true)));
    }

    /// <summary>
    /// Filtreler <c>amix</c>'ten <b>once</b> girdi basina uygulaniyor: her girdi kendi
    /// zincirinden gecip etiketleniyor, karisim o etiketleri okuyor. Karisimdan sonra
    /// uygulanan bir zincir mikrofonun kapisini sistem sesine de acardi.
    /// </summary>
    [Fact]
    public void FiltrelerKarisimdanOnceGirdiBasinaUygulanir()
    {
        var plan = Plan(AudioTrackLayout.MixedSingleTrack, new AudioFilterOptions(6, NoiseSuppression: true));
        var graf = plan.FilterComplex!;

        Assert.Equal(
            "[1:a]volume=6dB,afftdn[amix0];[2:a]volume=6dB,afftdn[amix1];[amix0][amix1]amix=inputs=2:duration=longest:dropout_transition=0[aout]",
            graf);
        Assert.True(
            graf.IndexOf("volume=", StringComparison.Ordinal) < graf.IndexOf("amix=", StringComparison.Ordinal),
            "zincir karisimdan once kurulmali");
    }

    [Fact]
    public void AyriIzlerdeFiltreHerIzeAyriBaglanir()
    {
        var plan = Plan(AudioTrackLayout.SeparateTracks, new AudioFilterOptions(NoiseGate: true));

        Assert.Equal("[1:a]agate[aout0];[2:a]agate[aout1]", plan.FilterComplex);
        Assert.Equal(new[] { "[aout0]", "[aout1]" }, plan.Maps);
        Assert.DoesNotContain("amix", plan.FilterComplex!);
    }

    /// <summary>
    /// Filtre secilmediginde grafik bugunku haliyle kaliyor: tek girdide hic kurulmuyor,
    /// iki girdide yalniz <c>amix</c>. 8d kolunun argumani degismedi.
    /// </summary>
    [Fact]
    public void FiltresizKolBugunkuGrafiginAynisi()
    {
        var eski = AudioCaptureArguments.Build(
            new AudioCaptureSelection(Mikrofon, SistemSesi), Liste, RecorderArguments.AudioFirstInputIndex);
        var yeni = Plan(AudioTrackLayout.MixedSingleTrack, AudioFilterOptions.None);

        Assert.Equal(eski.Inputs, yeni.Inputs);
        Assert.Equal(eski.FilterComplex, yeni.FilterComplex);
        Assert.Equal(eski.Maps, yeni.Maps);
        Assert.Equal(eski.InputCount, yeni.InputCount);
        Assert.DoesNotContain("volume", yeni.FilterComplex!);
    }

    /// <summary>Negatif kontrol: kabul araliginin disindaki kazanc sessizce kirpilmiyor.</summary>
    [Fact]
    public void AraligInDisindakiKazancReddedilir()
    {
        var secim = new AudioCaptureSelection(Mikrofon);

        Assert.Throws<ArgumentOutOfRangeException>(() => AudioCaptureArguments.Build(
            secim, Liste, RecorderArguments.AudioFirstInputIndex,
            AudioTrackLayout.MixedSingleTrack, new AudioFilterOptions(AudioCaptureArguments.MaxGainDb + 1)));

        var kabul = AudioCaptureArguments.TryBuild(
            secim, Liste, RecorderArguments.AudioFirstInputIndex,
            AudioTrackLayout.MixedSingleTrack, new AudioFilterOptions(AudioCaptureArguments.MinGainDb - 1),
            out var plan, out var sebep);

        Assert.False(kabul);
        Assert.Same(AudioCapturePlan.Silent, plan);
        Assert.Contains("kazanc", sebep);
    }

    // 9 — ekran goruntusu

    /// <summary>
    /// Ekran goruntusu ayni yakalama girdisinden tek kare okuyor: kodlama kolu hic
    /// kurulmuyor ve <c>-nostdin</c> <b>var</b> — kayit kolunun tersine, cunku bu surecin
    /// nazik durdurma yolu yok, tek kareyi alip kapaniyor.
    /// </summary>
    [Fact]
    public void EkranGoruntusuTekKareOkur()
    {
        var args = RecorderArguments.BuildSnapshot(
            Istek() with { Container = RecorderContainer.Mkv }, @"C:\kayit\kare.png");

        Assert.Contains("-f gdigrab", Metin(args));
        Assert.Equal("1", Deger(args, "-frames:v"));
        Assert.Contains("-nostdin", args);
        Assert.Contains("-an", args);
        Assert.Equal(@"C:\kayit\kare.png", args[^1]);
        Assert.DoesNotContain("-c:v", args);
        Assert.DoesNotContain("-crf", args);
        Assert.DoesNotContain("-movflags", args);
    }

    /// <summary>Kirpma ve olcekleme ekran goruntusune de giriyor; kare kayitla ayni cerceve.</summary>
    [Fact]
    public void EkranGoruntusuKayitlaAyniCerceveyiAlir()
    {
        var args = RecorderArguments.BuildSnapshot(
            Istek(RecorderPlatform.MacOs) with
            {
                Target = RecorderTargetKind.Region,
                Region = new RecorderRegion(10, 30, 640, 480),
                Scale = new RecorderScale(320, 240)
            },
            "/tmp/kare.png");

        Assert.Equal("crop=640:480:10:30,scale=320:240", Deger(args, "-vf"));
    }

    /// <summary>Negatif kontrol: kabul edilemez istek ve bos yol sessizce kare uretmiyor.</summary>
    [Fact]
    public void KabulEdilemezEkranGoruntusuReddedilir()
    {
        Assert.Throws<ArgumentException>(() => RecorderArguments.BuildSnapshot(Istek(), "  "));

        var hata = Assert.Throws<InvalidOperationException>(() => RecorderArguments.BuildSnapshot(
            Istek() with { VideoCodec = "libzzznotreal" }, @"C:\kayit\kare.png"));
        Assert.Contains("libzzznotreal", hata.Message);
    }

    // 10 — coklu monitor

    private static readonly ScreenBounds[] IkiMonitor =
    {
        new(0, 0, 0, 1920, 1080),
        new(1, 1920, 0, 2560, 1440)
    };

    /// <summary>
    /// <c>gdigrab</c> ekran indeksi almiyor: ikinci monitor, masaustu koordinatlarindaki
    /// ofsete ve boyuta cevriliyor. Ayni istek indeks 0'da ofsetsiz masaustunu aliyor.
    /// </summary>
    [Fact]
    public void IkinciMonitorOfsetliBolgeyeCevrilir()
    {
        var ilk = RecorderArguments.Build(Istek() with { Screens = IkiMonitor }, @"C:\kayit\a.mp4");
        var ikinci = RecorderArguments.Build(
            Istek() with { ScreenIndex = 1, Screens = IkiMonitor }, @"C:\kayit\a.mp4");

        Assert.DoesNotContain("-offset_x", ilk);
        Assert.Contains("-i desktop", Metin(ilk));

        Assert.Equal("1920", Deger(ikinci, "-offset_x"));
        Assert.Equal("0", Deger(ikinci, "-offset_y"));
        Assert.Equal("2560x1440", Deger(ikinci, "-video_size"));
        Assert.Contains("-i desktop", Metin(ikinci));
    }

    /// <summary>Tek sayili monitor olcusu <c>yuv420p</c> icin asagi ciftleniyor.</summary>
    [Fact]
    public void TekSayiliMonitorOlcusuCiftlenir()
    {
        var bolge = RecorderArguments.RegionForScreen(new[] { new ScreenBounds(0, 5, 7, 1365, 767) }, 0);

        Assert.Equal(new RecorderRegion(5, 7, 1364, 766), bolge);
    }

    /// <summary>
    /// Negatif kontrol: listede olmayan monitor, bos liste ve ekran disi bir hedefle
    /// verilen indeks reddedilir. Sessizce ilk ekrana dusme yok.
    /// </summary>
    [Fact]
    public void ListedeOlmayanMonitorReddedilir()
    {
        Assert.Null(RecorderArguments.RegionForScreen(IkiMonitor, 5));
        Assert.Null(RecorderArguments.RegionForScreen(Array.Empty<ScreenBounds>(), 1));

        var listesiz = RecorderArguments.Validate(Istek() with { ScreenIndex = 1 }, @"C:\kayit\a.mp4");
        var yok = RecorderArguments.Validate(
            Istek() with { ScreenIndex = 9, Screens = IkiMonitor }, @"C:\kayit\a.mp4");
        var bolgeyle = RecorderArguments.Validate(
            Istek() with
            {
                Target = RecorderTargetKind.Region,
                Region = new RecorderRegion(0, 0, 640, 480),
                ScreenIndex = 1,
                Screens = IkiMonitor
            },
            @"C:\kayit\a.mp4");
        var negatif = RecorderArguments.Validate(Istek() with { ScreenIndex = -1 }, @"C:\kayit\a.mp4");

        Assert.Contains(listesiz, satir => satir.Contains("gdigrab"));
        Assert.Contains(yok, satir => satir.Contains("not in the enumerated monitor bounds"));
        Assert.Contains(bolgeyle, satir => satir.Contains("only selects a monitor"));
        Assert.Contains(negatif, satir => satir.Contains("negative"));
        Assert.Empty(RecorderArguments.Validate(
            Istek() with { ScreenIndex = 1, Screens = IkiMonitor }, @"C:\kayit\a.mp4"));
    }

    /// <summary>
    /// macOS'un ekran indeksi girdide duruyor ve monitor listesine bakmiyor; Windows'un
    /// kolu ayni indekste bolgeye cevriliyor. Iki platformun argumani ayrisik.
    /// </summary>
    [Fact]
    public void MacEkranIndeksiGirdideKalir()
    {
        var mac = RecorderArguments.Build(
            Istek(RecorderPlatform.MacOs) with { ScreenIndex = 1 }, "/tmp/a.mp4");

        Assert.Contains("-i 1:none", Metin(mac));
        Assert.DoesNotContain("-offset_x", mac);
    }

    /// <summary>
    /// Kolun tasiyici karari degismedi: on kol eklendikten sonra da kayit argumaninda
    /// <c>-nostdin</c> ve ilerleme bayraklari yok. Ekran goruntusu kolu ayri — orada
    /// <c>-nostdin</c> var.
    /// </summary>
    [Fact]
    public void YeniKollarTasiyiciKararlariniBozmaz()
    {
        var args = RecorderArguments.Build(
            Istek() with
            {
                Container = RecorderContainer.Mkv,
                Scale = new RecorderScale(1280, 720),
                KeyframeSeconds = 2,
                Profile = "high",
                Tune = "zerolatency",
                RateControl = RecorderRateControl.Bitrate,
                BitrateKbps = 8000,
                PixelFormat = "yuv420p",
                ColorSpace = "bt709",
                MaxDuration = TimeSpan.FromMinutes(10),
                Audio = Plan(AudioTrackLayout.SeparateTracks, new AudioFilterOptions(3, true, true))
            },
            @"C:\kayit\a.mkv");

        Assert.DoesNotContain("-nostdin", args);
        Assert.DoesNotContain("-progress", args);
        Assert.DoesNotContain("-nostats", args);
        Assert.Equal(@"C:\kayit\a.mkv", args[^1]);
        Assert.True(
            Metin(args).IndexOf("-i desktop", StringComparison.Ordinal)
            < Metin(args).IndexOf("-c:v", StringComparison.Ordinal),
            "kodlama kolu girdiden sonra gelmeli");
    }
}
