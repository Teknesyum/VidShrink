using VidShrink.Cli;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using static VidShrink.Tests.B1KalanGirdi;

namespace VidShrink.Tests;

/// <summary>
/// Kapak resmi. MP4'te kaynagin <c>attached_pic</c> izi (mjpeg/png/bmp) kopyalanip
/// <c>-disposition:v:1 attached_pic</c> ile yazilir; kapak varken video bayraklari iz basina
/// (<c>-c:v:0</c>...) yazilir, yoksa ffmpeg kapagi da kodlar. Matroska'da png/jpeg kapak kosucunun
/// cikardigi dosyadan <c>-attach</c> ile ek olarak yazilir. Bayt iki yolda da butceye girer.
/// Tasinamayan yerde (MOV, Matroska'da bmp, MP4'te resim olmayan kodek) <c>CoverDropped</c> notu
/// duser. Olcum <c>docs/olcumler/kapak-mkv-mov.md</c>.
/// </summary>
public sealed class KapakResmiTests
{
    private static readonly SourceStream Ses = new(1, StreamKind.Audio, "aac", "eng", Channels: 2, BitrateBps: 128_000);
    private static readonly SourceStream Kapak = new(2, StreamKind.Video, "png", IsAttachedPicture: true, Bytes: 600_000);
    private static readonly SourceStream Font = new(3, StreamKind.Attachment, "ttf", Bytes: 1_000);
    private static readonly SourceStream Pgs = new(4, StreamKind.Subtitle, "hdmv_pgs_subtitle", "tur", Bytes: 50_000);

    private static StreamPlan Karar(OutputContainer kap, SourceStream? kapak, bool platform = false, bool hepsi = false, params SourceStream[] ek)
        => StreamMapping.Decide(Kaynak(600, new[] { Video, Ses }.Concat(kapak is null ? Array.Empty<SourceStream>() : new[] { kapak }).Concat(ek).ToArray()),
            new StreamRequest(KeepAllTracks: hepsi, PlatformDelivery: platform), kap, 160, null, "aac", true, 100);

    /// <summary>MP4'te kapak eslenir, kopyalanir, bayragi yazilir ve bayti yan butceye eklenir.</summary>
    [Fact]
    public void Mp4KapagiTasiyor()
    {
        var kapakli = Karar(OutputContainer.Mp4, Kapak);
        var kapaksiz = Karar(OutputContainer.Mp4, null);
        var args = kapakli.OutputArguments();
        var liste = args.ToList();

        Assert.Equal("0:2", kapakli.CoverMap);
        Assert.Equal("0:2", liste[liste.IndexOf("0:0") + 2]);
        Assert.Equal("copy", Sonraki(args, "-c:v:1"));
        Assert.Equal("attached_pic", Sonraki(args, "-disposition:v:1"));
        Assert.Equal(600_000 * 8.0 / 1000 / 600, kapakli.SideK - kapaksiz.SideK, 3);
        Assert.Equal("0:0", kapakli.VideoMap);
        Assert.Null(kapakli.CoverAttachment);
        Assert.DoesNotContain(StreamNote.CoverDropped, kapakli.Notes);
        Assert.Empty(kapakli.CoverAttachArguments("kapak.png"));
    }

    /// <summary>Olumsuz kontrol: MKV, MOV, WebM, platform teslimi ve resim olmayan kodek kapagi video izi olarak eslemez.</summary>
    [Fact]
    public void KapakTasinmayanYerler()
    {
        Assert.Null(Karar(OutputContainer.Mkv, Kapak).CoverMap);
        Assert.Null(Karar(OutputContainer.Mov, Kapak).CoverMap);
        Assert.Null(Karar(OutputContainer.WebM, Kapak).CoverMap);
        Assert.Null(Karar(OutputContainer.Mp4, Kapak, platform: true).CoverMap);
        Assert.Null(Karar(OutputContainer.Mp4, Kapak with { Codec = "h264" }).CoverMap);
        Assert.DoesNotContain("-disposition:v:1", Karar(OutputContainer.Mkv, Kapak).OutputArguments());
        Assert.DoesNotContain("0:2", Karar(OutputContainer.Mkv, Kapak).OutputArguments());
    }

    /// <summary>
    /// Matroska png ve jpeg kapagi ek olarak planlar: akis, uzanti, mimetype, bayt; bayt MP4'teki
    /// kadar yan butceye girer ve not dusmez. Kapaksiz kaynak olumsuz kontrol.
    /// </summary>
    [Fact]
    public void MkvKapagiEkOlarakPlanlar()
    {
        var png = Karar(OutputContainer.Mkv, Kapak);
        var jpg = Karar(OutputContainer.Mkv, Kapak with { Codec = "mjpeg", Bytes = 300_000 });
        var kapaksiz = Karar(OutputContainer.Mkv, null);

        Assert.Equal(new CoverAttachment("0:2", "png", "image/png", 600_000), png.CoverAttachment);
        Assert.Equal(new CoverAttachment("0:2", "jpg", "image/jpeg", 300_000), jpg.CoverAttachment);
        Assert.Equal("cover.png", png.CoverAttachment!.FileName);
        Assert.Equal("cover.jpg", jpg.CoverAttachment!.FileName);
        Assert.Equal(600_000 * 8.0 / 1000 / 600, png.SideK - kapaksiz.SideK, 3);
        Assert.Equal(300_000 * 8.0 / 1000 / 600, jpg.SideK - kapaksiz.SideK, 3);
        Assert.DoesNotContain(StreamNote.CoverDropped, png.Notes);
        Assert.DoesNotContain(StreamNote.CoverDropped, jpg.Notes);

        Assert.Null(kapaksiz.CoverAttachment);
        Assert.DoesNotContain(StreamNote.CoverDropped, kapaksiz.Notes);
        Assert.Empty(kapaksiz.CoverAttachArguments("kapak.png"));
    }

    /// <summary>
    /// Ek argumani: dosya, mimetype ve ad ayni ek akisina yazilir; belirtec kaynaktan kopyalanan
    /// eklerden sonraki siradir (eksiz 0, bir font kopyalaninca 1). Dosya yolu yokken hic yazilmaz.
    /// </summary>
    [Fact]
    public void EkArgumaniKopyalananEklerdenSonra()
    {
        var yalin = Karar(OutputContainer.Mkv, Kapak);
        Assert.Equal(new[] { "-attach", "C:\\gecici\\k.png", "-metadata:s:t:0", "mimetype=image/png", "-metadata:s:t:0", "filename=cover.png" },
            yalin.CoverAttachArguments("C:\\gecici\\k.png"));
        Assert.Empty(yalin.CoverAttachArguments(null));
        Assert.Empty(yalin.CoverAttachArguments(""));

        var fontlu = Karar(OutputContainer.Mkv, Kapak with { Codec = "mjpeg" }, hepsi: true, ek: Font);
        Assert.Single(fontlu.Attachments);
        Assert.Equal(new[] { "-attach", "k.jpg", "-metadata:s:t:1", "mimetype=image/jpeg", "-metadata:s:t:1", "filename=cover.jpg" },
            fontlu.CoverAttachArguments("k.jpg"));

        var fontsuzIstek = Karar(OutputContainer.Mkv, Kapak, ek: Font);
        Assert.Empty(fontsuzIstek.Attachments);
        Assert.Equal("-metadata:s:t:0", fontsuzIstek.CoverAttachArguments("k.png")[2]);
    }

    /// <summary>
    /// Tasinamayan kapak not duser: MOV, Matroska'da bmp, MP4'te resim olmayan kodek. Tasinan
    /// yerde, kapaksiz kaynakta, platform tesliminde ve WebM'de (kendi notu var) dusmez.
    /// </summary>
    [Fact]
    public void TasinamayanKapakNotDusuyor()
    {
        Assert.Contains(StreamNote.CoverDropped, Karar(OutputContainer.Mov, Kapak).Notes);
        Assert.Contains(StreamNote.CoverDropped, Karar(OutputContainer.Mkv, Kapak with { Codec = "bmp" }).Notes);
        Assert.Contains(StreamNote.CoverDropped, Karar(OutputContainer.Mp4, Kapak with { Codec = "h264" }).Notes);
        Assert.Null(Karar(OutputContainer.Mov, Kapak).CoverAttachment);
        Assert.Null(Karar(OutputContainer.Mkv, Kapak with { Codec = "bmp" }).CoverAttachment);
        Assert.Equal(Karar(OutputContainer.Mov, null).SideK, Karar(OutputContainer.Mov, Kapak).SideK, 6);

        Assert.DoesNotContain(StreamNote.CoverDropped, Karar(OutputContainer.Mp4, Kapak).Notes);
        Assert.DoesNotContain(StreamNote.CoverDropped, Karar(OutputContainer.Mkv, Kapak).Notes);
        Assert.DoesNotContain(StreamNote.CoverDropped, Karar(OutputContainer.Mov, null).Notes);
        Assert.DoesNotContain(StreamNote.CoverDropped, Karar(OutputContainer.Mov, Kapak, platform: true).Notes);
        Assert.Null(Karar(OutputContainer.Mkv, Kapak, platform: true).CoverAttachment);

        var webm = Karar(OutputContainer.WebM, Kapak);
        Assert.DoesNotContain(StreamNote.CoverDropped, webm.Notes);
        Assert.Contains(StreamNote.WebmStreamDropped, webm.Notes);
        Assert.Null(webm.CoverAttachment);
    }

    /// <summary>Not planin gerekcesine ve iki CLI diline kendi cumlesiyle duser; kapagi tasiyan planda yoktur.</summary>
    [Fact]
    public void NotGerekcedeVeCliMetninde()
    {
        var info = Kaynak(600, Video, Ses, Kapak);
        var mov = PlanCalculator.Build(info, new PlanOptions { TargetMb = 25, DeliveredContainer = OutputContainer.Mov });
        var mkv = PlanCalculator.Build(info, new PlanOptions { TargetMb = 25, DeliveredContainer = OutputContainer.Mkv });

        Assert.Contains("cover art cannot be carried", mov.Reason);
        Assert.DoesNotContain("cover art cannot be carried", mkv.Reason);
        Assert.NotNull(mkv.Streams!.CoverAttachment);

        Assert.Equal("cover-dropped", StreamNotes.Slug(StreamNote.CoverDropped));
        Assert.Equal("Bu çıktı kapak resmini taşıyamaz, kapak düşürüldü", CliText.ForLanguage("tr")["plan.stream.cover-dropped"]);
        Assert.Equal("This output cannot carry the cover art, so it was dropped", CliText.ForLanguage("en")["plan.stream.cover-dropped"]);
    }

    /// <summary>Kapak varken video bayraklari iz basina; yokken genel yazim (olumsuz kontrol).</summary>
    [Fact]
    public void KapakVarkenBayraklarIzBasina()
    {
        var info = Kaynak(600, Video, Ses, Kapak);
        var plan = PlanCalculator.Build(info, new PlanOptions { TargetMb = 25 });

        var kapakli = FfmpegArguments.Build(info, plan, "cikti.mp4", 2, "log");
        Assert.Contains("-c:v:0", kapakli);
        Assert.Contains("-pix_fmt:v:0", kapakli);
        Assert.DoesNotContain("-c:v", kapakli);
        Assert.DoesNotContain("-pix_fmt", kapakli);

        var mkv = FfmpegArguments.Build(info, plan, "cikti.mkv", 2, "log");
        Assert.Contains("-c:v", mkv);
        Assert.DoesNotContain("-c:v:0", mkv);

        var ilk = FfmpegArguments.Build(info, plan, "cikti.mp4", 1, "log");
        Assert.Contains("-c:v", ilk);
    }

    /// <summary>
    /// Komut eki yalniz kosucu dosyayi cikarinca ve yalniz dosyayi yazan geciste tasir: yolsuz planda
    /// (gosterilen komut), ilk geciste ve MP4/MOV ciktida <c>-attach</c> yoktur. Cikti yolu sonda kalir.
    /// </summary>
    [Fact]
    public void KomuttaEkYalnizSonGecisteVeYolVarken()
    {
        var info = Kaynak(600, Video, Ses, Kapak);
        var plan = PlanCalculator.Build(info, new PlanOptions { TargetMb = 25, DeliveredContainer = OutputContainer.Mkv });

        Assert.DoesNotContain("-attach", FfmpegArguments.Build(info, plan, "cikti.mkv", 2, "log"));

        var yollu = plan.Clone();
        yollu.CoverAttachmentPath = "C:\\gecici\\k.png";
        var son = FfmpegArguments.Build(info, yollu, "cikti.mkv", 2, "log");
        Assert.Equal("C:\\gecici\\k.png", Sonraki(son, "-attach"));
        Assert.Equal("mimetype=image/png", Sonraki(son, "-metadata:s:t:0"));
        Assert.Equal("cikti.mkv", son[^1]);
        Assert.Contains("-attach", FfmpegArguments.Build(info, yollu, "cikti.mkv", 0, null));

        Assert.DoesNotContain("-attach", FfmpegArguments.Build(info, yollu, "cikti.mkv", 1, "log"));
        Assert.DoesNotContain("-attach", FfmpegArguments.Build(info, yollu, "cikti.mp4", 2, "log"));
        Assert.DoesNotContain("-attach", FfmpegArguments.Build(info, yollu, "cikti.mov", 2, "log"));
        Assert.Equal("C:\\gecici\\k.png", yollu.Clone().CoverAttachmentPath);
    }

    /// <summary>Resim altyazi yakilirken (<c>-filter_complex</c>, <c>-map [v]</c>) Matroska kapagi yine ek olarak yazilir.</summary>
    [Fact]
    public void ResimAltyaziYakarkenEkDuruyor()
    {
        var info = Kaynak(600, Video, Ses, Kapak, Pgs);
        var plan = PlanCalculator.Build(info, new PlanOptions { TargetMb = 25, DeliveredContainer = OutputContainer.Mkv, Filters = new VideoFilterOptions { BurnSubtitle = 0 } }).Clone();
        plan.CoverAttachmentPath = "k.png";

        var args = FfmpegArguments.Build(info, plan, "cikti.mkv", 0, null);

        Assert.Contains("-filter_complex", args);
        Assert.Equal("[v]", Sonraki(args, "-map"));
        Assert.Equal("k.png", Sonraki(args, "-attach"));
        Assert.DoesNotContain("0:2", args);
    }

    /// <summary>Cikarma komutu kapagi kodlamadan tek dosyaya yazar.</summary>
    [Fact]
    public void CikarmaKomutu()
    {
        var args = EncodeRunner.CoverExtractArguments("kaynak.mp4", new CoverAttachment("0:2", "png", "image/png", 1), "k.png");

        Assert.Equal(new[] { "-hide_banner", "-y", "-i", "kaynak.mp4", "-map", "0:2", "-c", "copy", "-frames:v", "1", "-update", "1", "-f", "image2", "k.png" }, args);
    }

    /// <summary>
    /// Canli kol: yoklama kapagin baytini png dosyasinin boyuna esit okuyor; MP4 cikti png
    /// <c>attached_pic=1</c> tasiyor, yolsuz planla kurulan MKV komutu tek video izi (olumsuz kontrol).
    /// </summary>
    [Fact]
    public async Task CanliKapak()
    {
        var (kaynak, bayt) = await KapakliAsync("kapak-kaynak.mp4");

        var info = await FfprobeClient.ProbeAsync(kaynak);
        Assert.Equal(bayt, info.Streams.Single(stream => stream.IsAttachedPicture).Bytes);

        await KodlaAsync(kaynak, Yol("kapak-cikti.mp4"), _ => { });
        await KodlaAsync(kaynak, Yol("kapak-cikti.mkv"), _ => { });

        var mp4 = (await AkislarAsync(Yol("kapak-cikti.mp4"))).Where(stream => Alan(stream, "codec_type") == "video").ToList();
        Assert.Equal(2, mp4.Count);
        var resim = mp4.Single(stream => Bayrak(stream, "attached_pic") == 1);
        Assert.Equal("png", Alan(resim, "codec_name"));
        Assert.Equal("h264", Alan(mp4.Single(stream => Bayrak(stream, "attached_pic") == 0), "codec_name"));

        var mkv = (await AkislarAsync(Yol("kapak-cikti.mkv"))).Where(stream => Alan(stream, "codec_type") == "video").ToList();
        Assert.Single(mkv);

        Kapat("kapak-*");
    }

    private static async Task<EncodeResult> KosAsync(MediaInfo info, string cikti)
    {
        var plan = PlanCalculator.Build(info, new PlanOptions
        {
            TargetMb = 1.5,
            LockedCodec = "libx264",
            LockedMode = EncodeMode.Crf,
            LockedCrf = 35,
            LockedPreset = "ultrafast",
            DeliveredContainer = StreamMapping.ContainerOf(cikti)
        });
        Assert.NotEqual(EncodeMode.PassThrough, plan.ModeEnum);
        plan.ExtraArgs.AddRange(new[] { "-threads", "2" });
        return await new EncodeRunner().RunAsync(info, plan, cikti, 1.5, null);
    }

    private static async Task<byte[]> KapagiCikarAsync(string dosya, string hedef)
    {
        var kapak = (await AkislarAsync(dosya)).Single(stream => Bayrak(stream, "attached_pic") == 1);
        await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, EncodeRunner.CoverExtractArguments(dosya,
            new CoverAttachment("0:" + Alan(kapak, "index"), "png", "image/png", 0), hedef));
        return File.ReadAllBytes(hedef);
    }

    /// <summary>
    /// Canli kol, kosucunun kendisi: MP4 kaynagin png kapagi MKV ciktida <c>attached_pic=1</c>,
    /// <c>cover.png</c> / <c>image/png</c> etiketli ve kaynaktaki dosyayla bayt bayt ayni; uyari yok.
    /// O MKV yeniden kucultulunce kapak yine ayni baytla tasinir. MOV cikti kapak tasimaz ve plan
    /// bunu soyler (olumsuz kontrol).
    /// </summary>
    [FfmpegFact]
    public async Task CanliMkvKapagiKosucudanGeciyor()
    {
        var (kaynak, bayt) = await KapakliAsync("kapakmkv-kaynak.mp4");
        var png = File.ReadAllBytes(Yol("kapakmkv-kaynak-kapak.png"));
        var info = await FfprobeClient.ProbeAsync(kaynak);

        var ilk = await KosAsync(info, Yol("kapakmkv-ilk.mkv"));
        Assert.True(ilk.Success, ilk.Error);
        Assert.DoesNotContain(EncodeRunner.CoverLostWarning, ilk.DroppedOptions ?? Array.Empty<string>());
        Assert.NotNull(ilk.PlanUsed.Streams!.CoverAttachment);

        var akislar = await AkislarAsync(Yol("kapakmkv-ilk.mkv"));
        var resim = akislar.Single(stream => Bayrak(stream, "attached_pic") == 1);
        Assert.Equal("png", Alan(resim, "codec_name"));
        Assert.Equal("cover.png", Etiket(resim, "filename"));
        Assert.Equal("image/png", Etiket(resim, "mimetype"));
        Assert.Single(akislar, stream => Alan(stream, "codec_type") == "video" && Bayrak(stream, "attached_pic") == 0);
        Assert.Equal(bayt, png.Length);
        Assert.Equal(png, await KapagiCikarAsync(Yol("kapakmkv-ilk.mkv"), Yol("kapakmkv-ilk.png")));

        var ikinciInfo = await FfprobeClient.ProbeAsync(Yol("kapakmkv-ilk.mkv"));
        var ikinci = await KosAsync(ikinciInfo, Yol("kapakmkv-ikinci.mkv"));
        Assert.True(ikinci.Success, ikinci.Error);
        Assert.Equal(png, await KapagiCikarAsync(Yol("kapakmkv-ikinci.mkv"), Yol("kapakmkv-ikinci.png")));

        var mov = await KosAsync(info, Yol("kapakmkv-cikti.mov"));
        Assert.True(mov.Success, mov.Error);
        Assert.Contains(StreamNote.CoverDropped, mov.PlanUsed.Streams!.Notes);
        Assert.DoesNotContain(await AkislarAsync(Yol("kapakmkv-cikti.mov")), stream => Bayrak(stream, "attached_pic") == 1);

        Kapat("kapakmkv-*");
    }

    /// <summary>
    /// Canli kol: kapak cikarilamazsa (yoklama olmayan bir akisi gosteriyor) kodlama dusmez, cikti
    /// kapaksiz teslim edilir ve sonuc bunu <see cref="EncodeRunner.CoverLostWarning"/> ile soyler.
    /// </summary>
    [FfmpegFact]
    public async Task CanliCikarilamayanKapakUyariVeriyor()
    {
        var (kaynak, _) = await KapakliAsync("kapakyok-kaynak.mp4");
        var gercek = await FfprobeClient.ProbeAsync(kaynak);
        var bozuk = gercek with
        {
            Streams = gercek.Streams.Select(stream => stream.IsAttachedPicture ? stream with { Index = 9 } : stream).ToList()
        };

        var sonuc = await KosAsync(bozuk, Yol("kapakyok-cikti.mkv"));

        Assert.True(sonuc.Success, sonuc.Error);
        Assert.Contains(EncodeRunner.CoverLostWarning, sonuc.DroppedOptions!);
        Assert.DoesNotContain(await AkislarAsync(Yol("kapakyok-cikti.mkv")), stream => Bayrak(stream, "attached_pic") == 1);

        Kapat("kapakyok-*");
    }
}
