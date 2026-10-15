using System.Text.Json;
using VidShrink.App;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using static VidShrink.Tests.B1KalanGirdi;

namespace VidShrink.Tests;

/// <summary>
/// Dönüştür'de etiket yazımı (başlık, sanatçı, yıl, açıklama, kapak) ve "Onar" hazır ayarı. Etiketler
/// meta silme dizisinden sonra, çıktı yolundan önce yazılır; boş alan anahtar yazmaz. Kapak MP4'te
/// <c>attached_pic</c> video izi, MKV'de <c>-attach</c> ekidir (<c>docs/olcumler/kapak-mkv-mov.md</c>).
/// Onar girdiden önce bozuk paketi atan, zaman damgası üreten bayrakları yazar.
/// </summary>
public sealed class EtiketOnarDonusturTests
{
    private static readonly SourceStream Tur = new(1, StreamKind.Audio, "aac", "tur", Channels: 2, BitrateBps: 128_000);

    private static MediaInfo Ornek() => Kaynak(62, Video, Tur) with { FilePath = @"C:\Kayitlar\gezi.mp4" };

    private static ConversionPlan Kopya(string kap) => new() { Container = kap, VideoCodec = "copy", AudioCodec = "copy" };

    private static string? Secili(Avalonia.Controls.ComboBox box) => (box.SelectedItem as Avalonia.Controls.ComboBoxItem)?.Tag as string;

    private static List<string> Kur(ConversionPlan plan, string cikti) => ConversionArguments.Build(Ornek(), plan, cikti).ToList();

    /// <summary>
    /// Dört alan <c>-metadata anahtar=değer</c> olarak çıktı yolundan hemen önce yazılır; yıl <c>date</c>
    /// anahtarına gider. Meta silme açıkken etiketler <c>-map_metadata -1</c>'den sonra gelir, yoksa silinirdi.
    /// </summary>
    [Fact]
    public void EtiketlerMetaSilmedenSonraCiktidanOnceYaziliyor()
    {
        var plan = Kopya("mp4") with { DropMetadata = true, Title = "Gezi", Artist = "Ayşe", Year = "2024", Comment = "Deniz kıyısı" };
        var komut = Kur(plan, "c.mp4");

        var etiketler = new[] { "-metadata", "title=Gezi", "-metadata", "artist=Ayşe", "-metadata", "date=2024", "-metadata", "comment=Deniz kıyısı", "c.mp4" };
        Assert.Equal(etiketler, komut.TakeLast(etiketler.Length));
        Assert.True(komut.IndexOf("-map_metadata") < komut.IndexOf("title=Gezi"));
    }

    /// <summary>Boş ya da yalnız boşluk olan alan anahtar yazmaz; dört alan boşken komut alansız planınkiyle aynıdır.</summary>
    [Fact]
    public void BosAlanAnahtarYazmiyor()
    {
        var yalnizYil = Kur(Kopya("mkv") with { Title = "  ", Artist = "", Year = " 1999 " }, "c.mkv");
        Assert.DoesNotContain(yalnizYil, arg => arg.StartsWith("title=", StringComparison.Ordinal) || arg.StartsWith("artist=", StringComparison.Ordinal) || arg.StartsWith("comment=", StringComparison.Ordinal));
        Assert.Equal("date=1999", Sonraki(yalnizYil, "-metadata"));

        Assert.Equal(Kur(Kopya("mkv"), "c.mkv"), Kur(Kopya("mkv") with { Title = "", Artist = " ", Year = "", Comment = "" }, "c.mkv"));
    }

    /// <summary>
    /// MP4'te kapak ana girdiden ve dış girdilerden sonra gelen ayrı bir <c>-i</c>'dir; ikinci video izi
    /// olarak eşlenir, kopyalanır ve <c>attached_pic</c> işaretlenir. Dış ses varken girdi sırası bir kayar.
    /// </summary>
    [Fact]
    public void Mp4KapakAttachedPicIziOluyor()
    {
        var kapak = @"C:\Kapaklar\on.png";
        var komut = Kur(Kopya("mp4") with { CoverPath = kapak }, "c.mp4");
        Assert.Equal(kapak, komut[komut.LastIndexOf("-i") + 1]);
        Assert.Contains("1:v:0", komut);
        Assert.Equal("attached_pic", Sonraki(komut, "-disposition:v:1"));
        Assert.Equal("copy", Sonraki(komut, "-c:v:1"));
        Assert.DoesNotContain("-attach", komut);

        var disSesli = Kur(Kopya("mp4") with { CoverPath = kapak, ExternalAudio = new ExternalAudio(@"C:\Kayitlar\anlatim.m4a", "tur") }, "c.mp4");
        Assert.Contains("2:v:0", disSesli);
        Assert.Equal(kapak, disSesli[disSesli.LastIndexOf("-i") + 1]);
    }

    /// <summary>MKV'de kapak <c>-attach</c> ekidir; mimetype ve dosya adı yazılır, ek girdi açılmaz.</summary>
    [Theory]
    [InlineData(@"C:\Kapaklar\on.jpg", "image/jpeg", "cover.jpg")]
    [InlineData(@"C:\Kapaklar\on.JPEG", "image/jpeg", "cover.jpg")]
    [InlineData(@"C:\Kapaklar\on.png", "image/png", "cover.png")]
    public void MkvKapakEkOluyor(string kapak, string mime, string ad)
    {
        var komut = Kur(Kopya("mkv") with { CoverPath = kapak }, "c.mkv");
        Assert.Equal(kapak, Sonraki(komut, "-attach"));
        var i = komut.IndexOf("-attach");
        Assert.Equal(new[] { "-metadata:s:t:0", "mimetype=" + mime, "-metadata:s:t:0", "filename=" + ad }, komut.Skip(i + 2).Take(4));
        Assert.Single(komut, arg => arg == "-i");
        Assert.DoesNotContain("attached_pic", komut);
    }

    /// <summary>
    /// Kapak yalnız png ya da jpeg olur, yalnız MP4 ve MKV'ye yazılır (MOV sessizce düşürüyor). MP4'te kapak
    /// izi kopyalanır; video yeniden kodlanırken süzgeç kapağa da uygulanacağından kapak kopya ister.
    /// </summary>
    [Fact]
    public void KapakKisitlariDogrulaniyor()
    {
        var info = Ornek();
        Assert.Empty(ConversionArguments.Validate(info, Kopya("mp4") with { CoverPath = @"C:\k.png" }));
        Assert.Empty(ConversionArguments.Validate(info, Kopya("mkv") with { CoverPath = @"C:\k.jpg" }));
        Assert.Empty(ConversionArguments.Validate(info, new ConversionPlan { Container = "mkv", VideoCodec = "libx264", AudioCodec = "aac", CoverPath = @"C:\k.jpg" }));
        Assert.NotEmpty(ConversionArguments.Validate(info, Kopya("mov") with { CoverPath = @"C:\k.png" }));
        Assert.NotEmpty(ConversionArguments.Validate(info, Kopya("mkv") with { CoverPath = @"C:\k.bmp" }));
        Assert.NotEmpty(ConversionArguments.Validate(info, new ConversionPlan { Container = "mp4", VideoCodec = "libx264", AudioCodec = "aac", CoverPath = @"C:\k.png" }));
        Assert.Empty(ConversionArguments.Validate(info, Kopya("mov") with { CoverPath = "" }));
    }

    /// <summary>
    /// Onar girdiden önce <c>-fflags +genpts+discardcorrupt -err_detect ignore_err</c>, çıktıya
    /// <c>-avoid_negative_ts make_zero</c> yazar; kapalı plan bu bayrakların hiçbirini taşımaz.
    /// </summary>
    [Fact]
    public void OnarBayraklariGirdidenOnceYaziliyor()
    {
        var acik = Kur(Kopya("mkv") with { Repair = true }, "c.mkv");
        var girdi = acik.IndexOf(Ornek().FilePath) - 1;
        Assert.Equal(new[] { "-fflags", "+genpts+discardcorrupt", "-err_detect", "ignore_err" }, acik.Skip(girdi - 4).Take(4));
        Assert.Equal("make_zero", Sonraki(acik, "-avoid_negative_ts"));
        Assert.True(acik.IndexOf("-avoid_negative_ts") > girdi);

        var kapali = Kur(Kopya("mkv"), "c.mkv");
        Assert.DoesNotContain("-fflags", kapali);
        Assert.DoesNotContain("-err_detect", kapali);
        Assert.DoesNotContain("-avoid_negative_ts", kapali);
    }

    private sealed record Okuma(Dictionary<string, string> Etiketler, int Video, int Kapak);

    private static async Task<Okuma> OkuAsync(string yol)
    {
        var psi = new System.Diagnostics.ProcessStartInfo(ToolLocator.Ffprobe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in new[] { "-v", "error", "-show_entries", "format_tags:stream=codec_type:stream_disposition=attached_pic", "-of", "json", yol })
            psi.ArgumentList.Add(arg);
        using var surec = System.Diagnostics.Process.Start(psi)!;
        var cikti = surec.StandardOutput.ReadToEndAsync();
        var hata = surec.StandardError.ReadToEndAsync();
        await surec.WaitForExitAsync();
        await hata;
        Assert.Equal(0, surec.ExitCode);
        using var doc = JsonDocument.Parse(await cikti);
        var etiketler = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (doc.RootElement.GetProperty("format").TryGetProperty("tags", out var tags))
            foreach (var tag in tags.EnumerateObject()) etiketler[tag.Name] = tag.Value.GetString() ?? "";
        var akislar = doc.RootElement.GetProperty("streams").EnumerateArray().ToList();
        var kapak = akislar.Count(a => a.GetProperty("disposition").GetProperty("attached_pic").GetInt32() == 1);
        var video = akislar.Count(a => a.GetProperty("codec_type").GetString() == "video") - kapak;
        return new Okuma(etiketler, video, kapak);
    }

    /// <summary>
    /// Canlı kol, 2 sn'lik kısa klip ve 64x64 kapak, <c>-threads 2</c>: kopya kipinde onarılan MP4 ve MKV
    /// çıktıda başlık, sanatçı, tarih ve açıklama ffprobe'ta okunur, tek kapak <c>attached_pic</c> görünür ve
    /// asıl video izi tek kalır. Boş bırakılan açıklama anahtar olarak hiç görünmez.
    /// </summary>
    [FfmpegTheory]
    [InlineData("mp4", "png")]
    [InlineData("mkv", "jpg")]
    public async Task CanliEtiketVeKapakOkunuyor(string kap, string resim)
    {
        var adlar = new[] { $"etiket-onar-{kap}-kaynak.mp4", $"etiket-onar-{kap}-kapak.{resim}", $"etiket-onar-{kap}-cikti.{kap}" };
        Kapat(adlar);
        try
        {
            var kaynak = Yol(adlar[0]);
            var kapak = Yol(adlar[1]);
            await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, new[]
            {
                "-hide_banner", "-y", "-threads", "2",
                "-f", "lavfi", "-i", "testsrc=size=160x120:rate=10:duration=2", "-f", "lavfi", "-i", "sine=duration=2",
                "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", "-c:a", "aac", "-shortest",
                "-metadata", "comment=Eski", kaynak
            });
            await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, new[]
            {
                "-hide_banner", "-y", "-threads", "2", "-f", "lavfi", "-i", "color=red:size=64x64", "-frames:v", "1", kapak
            });

            var info = await FfprobeClient.ProbeAsync(kaynak);
            var cikti = Yol(adlar[2]);
            var plan = Kopya(kap) with { DropMetadata = true, Repair = true, Title = "Baş Lık", Artist = "Ayşe", Year = "2024", Comment = " ", CoverPath = kapak };
            var komut = ConversionArguments.Build(info, plan, cikti);
            await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, komut.Take(komut.Count - 1).Concat(new[] { "-threads", "2", cikti }).ToArray());

            var okuma = await OkuAsync(cikti);
            Assert.Equal("Baş Lık", okuma.Etiketler["title"]);
            Assert.Equal("Ayşe", okuma.Etiketler["artist"]);
            Assert.Equal("2024", okuma.Etiketler["date"]);
            Assert.False(okuma.Etiketler.ContainsKey("comment"));
            Assert.Equal(1, okuma.Video);
            Assert.Equal(1, okuma.Kapak);
        }
        finally { Kapat(adlar); }
    }

    /// <summary>
    /// Pencere kolu: etiket açılır panelindeki alanlar komuta iner, boşaltılınca komut ilk haline döner.
    /// "Onar" düğmesi MKV, video ve ses kopyasını seçer ve Onar kutusunu açar; komut onar bayraklarını taşır.
    /// </summary>
    [Fact]
    public void EtiketAlanlariVeOnarDugmesiKomutaIniyor()
    {
        var (once, dolu, bos, onar) = AppHost.Run(() =>
        {
            var window = new MainWindow { SettingsPathOverride = Path.Combine(TestPaths.OutputRoot, "etiket-onar-" + Guid.NewGuid().ToString("N") + ".json") };
            try
            {
                var info = Ornek();
                window.LoadWithoutProbing(info.FilePath, info);
                var ilk = window.TxtConvertCommand.Text ?? "";
                window.TxtConvertTagTitle.Text = "Gezi";
                window.TxtConvertTagArtist.Text = "Ayşe";
                window.TxtConvertTagYear.Text = "2024";
                window.TxtConvertTagComment.Text = "Not";
                var etiketli = window.TxtConvertCommand.Text ?? "";
                window.TxtConvertTagTitle.Text = "";
                window.TxtConvertTagArtist.Text = "";
                window.TxtConvertTagYear.Text = "";
                window.TxtConvertTagComment.Text = "";
                var bosalan = window.TxtConvertCommand.Text ?? "";
                window.ApplyConvertRepairPreset();
                var secim = (Kap: Secili(window.CmbContainer), Video: Secili(window.CmbConvertCodec),
                    Ses: Secili(window.CmbConvertAudio),Kutu: window.ChkConvertRepair.IsChecked == true, Komut: window.TxtConvertCommand.Text ?? "");
                return (ilk, etiketli, bosalan, secim);
            }
            finally { window.Close(); }
        });

        Assert.NotEqual("", once);
        Assert.Contains("-metadata title=Gezi -metadata artist=Ayşe -metadata date=2024 -metadata comment=Not", dolu, StringComparison.Ordinal);
        Assert.Equal(once, bos);
        Assert.Equal(("mkv", "copy", "copy", true), (onar.Kap, onar.Video, onar.Ses, onar.Kutu));
        Assert.Contains("-fflags +genpts+discardcorrupt -err_detect ignore_err", onar.Komut, StringComparison.Ordinal);
    }
}
