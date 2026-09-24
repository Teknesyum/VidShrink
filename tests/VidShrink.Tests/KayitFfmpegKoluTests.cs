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

    [Theory]
    [InlineData("nv12")]
    [InlineData("p010le")]
    [InlineData("rgb24")]
    [InlineData("bgr0")]
    [InlineData("gbrp")]
    public void SessizceCevrilenBicimlerKumeyeGirmez(string bicim)
    {
        var hatalar = RecorderArguments.Validate(Istek() with { PixelFormat = bicim }, @"C:\kayit\a.mp4");

        Assert.Contains(hatalar, satir => satir.Contains(bicim));
        Assert.DoesNotContain(bicim, RecorderArguments.PixelFormats);
    }

    [Theory]
    [InlineData("nv12", "yuv420p")]
    [InlineData("P010LE", "yuv420p10le")]
    [InlineData("yuv444p", "yuv444p")]
    [InlineData("rgb24", RecorderArguments.DefaultPixelFormat)]
    [InlineData("yuv999p", RecorderArguments.DefaultPixelFormat)]
    public void EskiAyardakiBicimKumeyeDoner(string eski, string beklenen)
        => Assert.Equal(beklenen, RecorderArguments.StoredPixelFormat(eski));

    [Fact]
    public void KodlayicininAlmadigiBicimReddedilir()
    {
        var av1 = RecorderArguments.Validate(Istek() with { VideoCodec = "libsvtav1", PixelFormat = "yuv444p" }, @"C:\kayit\a.mp4");
        var qsv = RecorderArguments.Validate(Istek() with { VideoCodec = "h264_qsv", PixelFormat = "yuv420p10le" }, @"C:\kayit\a.mp4");
        var x264 = RecorderArguments.Validate(Istek() with { VideoCodec = "libx264", PixelFormat = "yuv444p" }, @"C:\kayit\a.mp4");

        Assert.Contains(av1, satir => satir.Contains("libsvtav1") && satir.Contains("yuv444p"));
        Assert.Contains(qsv, satir => satir.Contains("h264_qsv") && satir.Contains("yuv420p10le"));
        Assert.DoesNotContain(x264, satir => satir.Contains("pixel format"));
    }

    [Theory]
    [InlineData("h264_qsv", "yuv420p", "nv12")]
    [InlineData("hevc_qsv", "yuv420p10le", "p010le")]
    [InlineData("hevc_nvenc", "yuv420p10le", "p010le")]
    [InlineData("hevc_amf", "yuv420p10le", "p010le")]
    [InlineData("hevc_nvenc", "yuv420p", "yuv420p")]
    [InlineData("libx264", "yuv420p10le", "yuv420p10le")]
    [InlineData("libsvtav1", "yuv420p10le", "yuv420p10le")]
    public void PaketliAdYalnizDuzlemselAdAlinmayincaYazilir(string kodlayici, string bicim, string yazilan)
    {
        var args = RecorderArguments.Build(Istek() with { VideoCodec = kodlayici, PixelFormat = bicim }, @"C:\kayit\a.mp4");

        Assert.Equal(yazilan, Deger(args, "-pix_fmt"));
    }

    [Fact]
    public void HerKodlayicininBicimleriFfmpeginBildirdigiKumede()
    {
        var kodlayicilar = new[]
        {
            "libx264", "libx265", "libsvtav1", "libvpx-vp9", "h264_nvenc", "hevc_nvenc", "av1_nvenc",
            "h264_qsv", "hevc_qsv", "h264_amf", "hevc_amf"
        };

        foreach (var kodlayici in kodlayicilar)
        {
            var kume = RecorderArguments.PixelFormatsFor(kodlayici);
            Assert.NotEmpty(kume);
            var bildirilen = FfmpegBicimleri(kodlayici);
            Assert.All(kume, bicim =>
                Assert.Contains(RecorderArguments.PixelFormatArgument(kodlayici, bicim), bildirilen));
        }

        Assert.Empty(RecorderArguments.PixelFormatsFor("uydurma264"));
    }

    [Theory]
    [InlineData("yuv420p", "0")]
    [InlineData("yuv444p", "1")]
    [InlineData("yuv420p10le", "2")]
    [InlineData("yuv444p10le", "3")]
    public void Vp9ProfiliBicimleEslesinceKabulEdilir(string bicim, string profil)
    {
        var istek = Istek() with { VideoCodec = "libvpx-vp9", PixelFormat = bicim, Profile = profil };

        Assert.Equal(new[] { "0", "1", "2", "3" }, RecorderArguments.ProfilesFor("libvpx-vp9"));
        Assert.DoesNotContain(RecorderArguments.Validate(istek, @"C:\kayit\a.mp4"), satir => satir.Contains("profile"));
        Assert.Equal(profil, Deger(RecorderArguments.Build(istek, @"C:\kayit\a.mp4"), "-profile:v"));
    }

    [Theory]
    [InlineData("yuv420p", "0", 0)]
    [InlineData("yuv444p", "1", 0)]
    [InlineData("yuv420p", "1", 1)]
    [InlineData("yuv444p", "0", 1)]
    [InlineData("yuv420p", "2", 1)]
    public void Vp9ProfilUyusmazligiFfmpegdeDeDuser(string bicim, string profil, int beklenenHata)
    {
        var hatalar = RecorderArguments.Validate(
            Istek() with { VideoCodec = "libvpx-vp9", PixelFormat = bicim, Profile = profil }, @"C:\kayit\a.mp4");
        var klasor = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".calisma", "paket-2b", "vp9");
        Directory.CreateDirectory(klasor);
        var cikti = Path.Combine(klasor, $"{bicim}-{profil}.webm");
        File.Delete(cikti);

        var kod = KisaFfmpeg($"-hide_banner -y -f lavfi -i testsrc2=s=64x64:r=5:d=1 -c:v libvpx-vp9 -deadline realtime -cpu-used 8 -threads 1 -pix_fmt {bicim} -profile:v {profil} \"{cikti}\"");
        var okunur = kod == 0 && File.Exists(cikti) && new FileInfo(cikti).Length > 0;
        File.Delete(cikti);

        Assert.Equal(beklenenHata, hatalar.Count(satir => satir.Contains("libvpx-vp9 profile")));
        Assert.Equal(beklenenHata == 0, okunur);
    }

    [Theory]
    [InlineData("mpeg")]
    [InlineData("jpeg")]
    [InlineData("limited")]
    [InlineData("full")]
    public void RenkAraligiTakmaAdlariKabulEdilir(string aralik)
    {
        var istek = Istek() with { ColorRange = aralik };

        Assert.Empty(RecorderArguments.Validate(istek, @"C:\kayit\a.mp4"));
        Assert.Equal(aralik, Deger(RecorderArguments.Build(istek, @"C:\kayit\a.mp4"), "-color_range"));
    }

    [Fact]
    public async Task RenkAraligiKumesiFfmpeginAdlariylaAyni()
    {
        using var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo("ffmpeg", "-hide_banner -h full")
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.Start();
        var stderr = process.StandardError.ReadToEndAsync();
        var satirlar = process.StandardOutput.ReadToEnd().Split('\n');
        process.WaitForExit();
        await stderr;
        var bas = Array.FindIndex(satirlar, s => s.TrimStart().StartsWith("-color_range ", StringComparison.Ordinal));
        Assert.True(bas >= 0, "ffmpeg -h full -color_range bildirmedi.");
        var adlar = satirlar.Skip(bas + 1)
            .TakeWhile(s => !s.TrimStart().StartsWith("-", StringComparison.Ordinal))
            .Select(s => s.Trim().Split(' ', 2)[0])
            .Where(s => s.Length > 0 && s is not "unknown" and not "unspecified")
            .ToHashSet();

        Assert.Equal(adlar.OrderBy(s => s), RecorderArguments.ColorRanges.OrderBy(s => s));
    }

    private static int KisaFfmpeg(string argumanlar)
    {
        using var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo("ffmpeg", argumanlar)
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.Start();
        var stderr = process.StandardError.ReadToEndAsync();
        var stdout = process.StandardOutput.ReadToEndAsync();
        if (!process.WaitForExit(30000))
        {
            process.Kill(true);
            return -1;
        }
        process.WaitForExit();
        _ = stderr.Result + stdout.Result;
        return process.ExitCode;
    }

    private static string[] FfmpegBicimleri(string kodlayici)
    {
        using var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo("ffmpeg", $"-hide_banner -h encoder={kodlayici}")
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.Start();
        var stderr = process.StandardError.ReadToEndAsync();
        var stdout = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        var satir = (stdout + "\n" + stderr.Result).Split('\n')
            .FirstOrDefault(s => s.TrimStart().StartsWith("Supported pixel formats:", StringComparison.Ordinal));
        Assert.True(satir is not null, $"{kodlayici} icin ffmpeg piksel bicimi bildirmedi.");
        return satir!.Split(':', 2)[1].Split(new[] { ' ', '\r' }, StringSplitOptions.RemoveEmptyEntries);
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

    [Theory]
    [InlineData(0, "150")]
    [InlineData(40, "110")]
    [InlineData(149.5, "0.5")]
    public void SonrakiParcayaKalanSureYazilir(double gecen, string beklenen)
    {
        var istek = Istek() with { MaxDuration = TimeSpan.FromSeconds(150) };

        var parca = RecorderArguments.ForSegment(istek, TimeSpan.FromSeconds(gecen));

        Assert.NotNull(parca);
        Assert.Equal(beklenen, Deger(RecorderArguments.Build(parca!, @"C:\kayit\a.mp4"), "-t"));
    }

    [Theory]
    [InlineData(150)]
    [InlineData(151)]
    [InlineData(149.9995)]
    public void SuresiDolanKayitYeniParcaAcmaz(double gecen)
        => Assert.Null(RecorderArguments.ForSegment(
            Istek() with { MaxDuration = TimeSpan.FromSeconds(150) }, TimeSpan.FromSeconds(gecen)));

    /// <summary>
    /// Sinirsiz kayitta bolme olcutu artik parcanin kendi -t'sine yaziliyor (ffmpeg parcayi
    /// kendisi kapatiyor); eskiden istek degismeden geri donuyordu ve kesme
    /// <c>RecorderSession</c>'in disaridan yoklamasina birakiliyordu. Split her seferinde
    /// argumana giriyor, sonraki parca da ayni sureyle acilir (surekli bolme).
    /// </summary>
    [Fact]
    public void SinirsizKayittaBolmeSuresiIlkParcayaYazilir()
    {
        var istek = Istek() with { Split = new RecorderSplit(TimeSpan.FromMinutes(1)) };

        var ilkParca = RecorderArguments.ForSegment(istek, TimeSpan.Zero)!;
        Assert.Null(ilkParca.Split);
        Assert.Equal("60", Deger(RecorderArguments.Build(ilkParca, @"C:\kayit\a.mp4"), "-t"));

        var sonrakiParca = RecorderArguments.ForSegment(istek, TimeSpan.FromMinutes(3))!;
        Assert.Equal("60", Deger(RecorderArguments.Build(sonrakiParca, @"C:\kayit\a.mp4"), "-t"));
    }

    /// <summary>
    /// Bolme suresi kalan toplam sureden uzunsa parca kalanla kurulur (son, kisa parca):
    /// 150 sn sinirda 120 sn gectiyse kalan 30 sn, 60 sn'lik bolme onu kesmez. Ilk parcada
    /// (capturedBefore=0) kalan (150) bolme suresinden (60) uzun oldugu icin parca 60 sn'de
    /// kurulur — bolme olcutu artik hicbir zaman degismeden tasinmiyor (Split hep null'a
    /// dusuyor), aksi halde kalan sure bolme suresinden kisa kaldiginda dogrulama parcayi
    /// reddederdi.
    /// </summary>
    [Fact]
    public void BolmeSuresiKalanSuredenUzunOlsaDaParcaKurulur()
    {
        var istek = Istek() with
        {
            MaxDuration = TimeSpan.FromSeconds(150),
            Split = new RecorderSplit(TimeSpan.FromSeconds(60))
        };

        var sonParca = RecorderArguments.ForSegment(istek, TimeSpan.FromSeconds(120))!;
        Assert.Equal("30", Deger(RecorderArguments.Build(sonParca, @"C:\kayit\a.mp4"), "-t"));
        Assert.Null(sonParca.Split);

        var ilkParca = RecorderArguments.ForSegment(istek, TimeSpan.Zero)!;
        Assert.Equal("60", Deger(RecorderArguments.Build(ilkParca, @"C:\kayit\a.mp4"), "-t"));
        Assert.Null(ilkParca.Split);
    }

    /// <summary>Negatif kontrol: bolme kurulmamis bir istekte -t bolme olcutunden gelmez, hic yazilmaz.</summary>
    [Fact]
    public void BolmeYokkenOlcutArgumanaGirmez()
    {
        var istek = Istek();
        Assert.Null(istek.Split);

        var parca = RecorderArguments.ForSegment(istek, TimeSpan.Zero)!;
        Assert.Same(istek, parca);
        Assert.DoesNotContain("-t", RecorderArguments.Build(parca, @"C:\kayit\a.mp4"));
    }

    /// <summary>
    /// Kendiliginden biten parca: bolmesiz kayit son olcumu -t'nin birkac ms altinda kalsa da
    /// biter (CI'da 5 sn'lik kayit 2 parca cikiyordu); bolmeyle sinirlanan parca sonrakini acar,
    /// toplam sinira gelen son parca acmaz.
    /// </summary>
    [Theory]
    [InlineData(null, 5.0, 0.0, 4.97, false)]
    [InlineData(null, null, 0.0, 30.0, false)]
    [InlineData(2.0, 5.0, 0.0, 1.97, true)]
    [InlineData(2.0, 5.0, 3.94, 4.97, false)]
    [InlineData(2.0, 5.0, 3.94, 4.60, false)]
    [InlineData(2.0, 5.0, 2.9, 4.6, true)]
    [InlineData(2.0, 5.0, 3.0, 5.0, false)]
    [InlineData(2.0, null, 40.0, 42.0, true)]
    public void DogalCikisYalnizBolmeSinirindaParcaAcar(double? bolme, double? toplam, double baslangic, double cikis, bool acar)
    {
        var istek = Istek() with
        {
            MaxDuration = toplam is { } t ? TimeSpan.FromSeconds(t) : null,
            Split = bolme is { } b ? new RecorderSplit(TimeSpan.FromSeconds(b)) : null
        };

        Assert.Equal(acar, RecorderArguments.NaturalExitContinues(istek, TimeSpan.FromSeconds(baslangic), 0, TimeSpan.FromSeconds(cikis)));
    }

    /// <summary>
    /// Boyutla bolmede ayni kural: bolme boyutu kalan toplamdan kucukse devam, degilse son parca.
    /// Toplam sure sinirina birkac ms kala biten boyut parcasi da kaydi bitirir.
    /// </summary>
    [Theory]
    [InlineData(0.0, 1.0, true)]
    [InlineData(8.5, 1.0, false)]
    [InlineData(0.0, 4.97, false)]
    public void BoyutlaBolmedeSonParcaDevamEtmez(double yazilan, double cikis, bool acar)
    {
        var istek = Istek() with { MaxMegabytes = 10, MaxDuration = TimeSpan.FromSeconds(5), Split = new RecorderSplit(Megabytes: 2) };

        Assert.Equal(acar, RecorderArguments.NaturalExitContinues(istek, TimeSpan.Zero, yazilan, TimeSpan.FromSeconds(cikis)));
    }

    [Fact]
    public void OturumDogalCikistaKarariSaftanAlir()
    {
        var kaynak = File.ReadAllText(Path.Combine(KokDizin(), "src", "VidShrink.Ffmpeg", "RecorderSession.cs"));

        Assert.Contains("RecorderArguments.NaturalExitContinues(_request, _segmentCapturedAtStart, _segmentWrittenAtStart, _capturedBefore)", kaynak);
    }

    [Fact]
    public void OturumHerParcayiKalanSureyleKurar()
    {
        var kaynak = File.ReadAllText(Path.Combine(KokDizin(), "src", "VidShrink.Ffmpeg", "RecorderSession.cs"));

        Assert.Contains("RecorderArguments.ForSegment(_request, _capturedBefore, _segments.Count == 0 ? 0 : WrittenMb)", kaynak);
        Assert.DoesNotContain("RecorderArguments.Build(_request", kaynak);
    }

    private static string KokDizin()
    {
        var dizin = new DirectoryInfo(AppContext.BaseDirectory);
        while (dizin is not null && !File.Exists(Path.Combine(dizin.FullName, "VidShrink.sln"))) dizin = dizin.Parent;
        return dizin?.FullName ?? throw new DirectoryNotFoundException("VidShrink.sln bulunamadi.");
    }

    /// <summary>
    /// <c>Build</c>'in kendisi Split'i hic okumuyor: bir istege sadece Split eklemek (ForSegment'tan
    /// gecirmeden) argumani degistirmiyor. Split'i -t/-fs'e katan <c>ForSegment</c>; onun ustunden
    /// gecen davranis <c>BolmeSuresiKalanSuredenUzunOlsaDaParcaKurulur</c> ve
    /// <c>SinirsizKayittaBolmeSuresiIlkParcayaYazilir</c>'de pimli.
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
    /// <c>gdigrab</c> ekran indeksi almiyor: her monitor masaustu koordinatlarindaki ofsete
    /// ve boyuta cevriliyor. <b>Indeks 0 da dahil</b> — <c>-i desktop</c> ofsetsiz birakilirsa
    /// butun sanal masaustu kaydediliyor, iki monitorlu makinede "Ekran 1" iki monitoru
    /// birden aliyordu. Ofsetsiz kol yalniz monitorun masaustunun tamami oldugu tek ekranli
    /// makinede kaliyor.
    /// </summary>
    [Fact]
    public void HerMonitorOfsetliBolgeyeCevrilir()
    {
        var ilk = RecorderArguments.Build(Istek() with { Screens = IkiMonitor }, @"C:\kayit\a.mp4");
        var ikinci = RecorderArguments.Build(
            Istek() with { ScreenIndex = 1, Screens = IkiMonitor }, @"C:\kayit\a.mp4");
        var tek = RecorderArguments.Build(
            Istek() with { Screens = new[] { IkiMonitor[0] } }, @"C:\kayit\a.mp4");

        Assert.Equal("0", Deger(ilk, "-offset_x"));
        Assert.Equal("1920x1080", Deger(ilk, "-video_size"));
        Assert.Contains("-i desktop", Metin(ilk));

        Assert.Equal("1920", Deger(ikinci, "-offset_x"));
        Assert.Equal("0", Deger(ikinci, "-offset_y"));
        Assert.Equal("2560x1440", Deger(ikinci, "-video_size"));
        Assert.Contains("-i desktop", Metin(ikinci));

        Assert.DoesNotContain("-offset_x", tek);
        Assert.Contains("-i desktop", Metin(tek));
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

    [Fact]
    public void Mp4KaydiYenidenKodlamadanKurulur()
    {
        var args = RecorderArguments.BuildRemuxToMp4(@"C:\kayit\a.mkv", @"C:\kayit\a.mp4");

        Assert.Equal(@"C:\kayit\a.mkv", Deger(args, "-i"));
        Assert.Equal("copy", Deger(args, "-c"));
        Assert.Equal("0", Deger(args, "-map"));
        Assert.Equal("+faststart", Deger(args, "-movflags"));
        Assert.DoesNotContain("-c:v", args);
        Assert.Equal(@"C:\kayit\a.mp4", args[^1]);
        Assert.Throws<ArgumentException>(() => RecorderArguments.BuildRemuxToMp4(@"C:\kayit\a.mkv", @"C:\kayit\a.mov"));

        var mevcut = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { @"C:\kayit\a.mp4", @"C:\kayit\a_2.mp4" };
        Assert.Equal(@"C:\kayit\a_3.mp4", RecorderArguments.RemuxTarget(@"C:\kayit\a.mkv", mevcut.Contains));
        Assert.Equal(@"C:\kayit\b.mp4", RecorderArguments.RemuxTarget(@"C:\kayit\b.mkv", mevcut.Contains));
    }

    [Fact]
    public void GifKabiUzantisiniVerirVeMatroskayaYakalar()
    {
        Assert.Equal("gif", RecorderArguments.Extension(RecorderContainer.Gif));
        Assert.Equal(RecorderContainer.Gif, RecorderArguments.ContainerOf(@"C:\kayit\a.gif"));
        Assert.Equal(RecorderContainer.Mkv, RecorderArguments.CaptureContainer(RecorderContainer.Gif));
        Assert.Equal(RecorderContainer.Mp4, RecorderArguments.CaptureContainer(RecorderContainer.Mp4));
        Assert.Equal(@"C:\kayit\a.gif-kayit.mkv", RecorderArguments.CapturePath(@"C:\kayit\a.gif", RecorderContainer.Gif));
        Assert.Equal(@"C:\kayit\a.mp4", RecorderArguments.CapturePath(@"C:\kayit\a.mp4", RecorderContainer.Mp4));

        var args = RecorderArguments.Build(Istek() with { Container = RecorderContainer.Gif, Fps = 15 }, @"C:\kayit\a.gif");

        Assert.Equal(@"C:\kayit\a.gif-kayit.mkv", args[^1]);
        Assert.DoesNotContain("-movflags", args);
        Assert.DoesNotContain("palettegen", Metin(args));
    }

    [Fact]
    public void GifIstegiSesBolmeVeYuksekKareHiziniReddeder()
    {
        var gif = Istek() with { Container = RecorderContainer.Gif };
        const string yol = @"C:\kayit\a.gif";

        Assert.Empty(RecorderArguments.Validate(gif with { Fps = GifPalette.MaxFps }, yol));
        Assert.Contains(RecorderArguments.Validate(gif with { Fps = GifPalette.MaxFps + 1 }, yol),
            satir => satir.Contains("hundredths of a second"));
        Assert.Contains(RecorderArguments.Validate(gif with { Audio = Plan(AudioTrackLayout.MixedSingleTrack) }, yol),
            satir => satir.Contains("no audio track"));
        Assert.Contains(RecorderArguments.Validate(gif with { Split = new RecorderSplit(TimeSpan.FromMinutes(1)) }, yol),
            satir => satir.Contains("cannot be split"));
        Assert.Empty(RecorderArguments.Validate(
            Istek() with { Container = RecorderContainer.Mkv, Fps = 60, Audio = Plan(AudioTrackLayout.MixedSingleTrack) },
            @"C:\kayit\a.mkv"));
        Assert.Contains(RecorderArguments.Validate(gif, @"C:\kayit\a.mkv"), satir => satir.Contains("selected container is gif"));
    }

    [Fact]
    public void GifPaletiKlipVeKaydaAyniFiltreyiVerir()
    {
        Assert.Equal(
            "fps=12,scale=480:-1:flags=lanczos,split[a][b];[a]palettegen[p];[b][p]paletteuse",
            VidShrink.App.Playback.ClipExport.Filter(12, 480));
        Assert.Equal("fps=15,split[a][b];[a]palettegen[p];[b][p]paletteuse", GifPalette.Filter(15, null));

        var args = GifPalette.Build(@"C:\kayit\a.gif-kayit.mkv", @"C:\kayit\a.gif", 15);
        Assert.Equal(@"C:\kayit\a.gif-kayit.mkv", Deger(args, "-i"));
        Assert.Equal("0", Deger(args, "-loop"));
        Assert.Contains("-nostdin", args);
        Assert.Equal(@"C:\kayit\a.gif", args[^1]);
        Assert.Throws<ArgumentException>(() => GifPalette.Build(" ", @"C:\kayit\a.gif", 15));
    }

    [Fact]
    public void OturumGifiDurunkaYakalamadanCevirir()
    {
        var kaynak = File.ReadAllText(Path.Combine(KokDizin(), "src", "VidShrink.Ffmpeg", "RecorderSession.cs"));

        Assert.Contains("RecorderArguments.CaptureRequest(request)", kaynak);
        Assert.Contains("GifPalette.Build(capture.OutputPath, gifPath, fps)", kaynak);
        Assert.Contains("ConvertToGifAsync(capture, _gifPath, _request.Fps, ct)", kaynak);
        Assert.Contains("return _gifPath is null ? result : await ConvertToGifAsync(result, ct);", kaynak);
    }

    [Fact]
    public async Task KisaMatroskaGifeCevrilir()
    {
        var klasor = Path.Combine(KokDizin(), ".calisma", "gif-olcu-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        try
        {
            var mkv = Path.Combine(klasor, "a.gif-kayit.mkv");
            var gif = Path.Combine(klasor, "a.gif");
            var uret = await VidShrink.Ffmpeg.FfmpegRunner.RunAsync(new[]
            {
                "-hide_banner", "-y", "-nostdin", "-f", "lavfi", "-i", "testsrc=size=64x48:rate=10:duration=1",
                "-c:v", "libx264", "-pix_fmt", "yuv420p", mkv
            });
            Assert.True(uret.Ok, uret.StandardError);

            var cevir = await VidShrink.Ffmpeg.FfmpegRunner.RunAsync(GifPalette.Build(mkv, gif, 10));
            Assert.True(cevir.Ok, cevir.StandardError);

            var bas = new byte[6];
            using (var dosya = File.OpenRead(gif)) Assert.Equal(6, dosya.Read(bas, 0, 6));
            Assert.Equal("GIF89a", System.Text.Encoding.ASCII.GetString(bas));
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }
}
