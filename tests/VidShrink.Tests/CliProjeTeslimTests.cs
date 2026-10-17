using System.Diagnostics;
using System.Text.Json;
using VidShrink.Cli;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

/// <summary>
/// later.md #40, ilk adim: <c>teslim</c> pencere acmadan proje dosyasini okur, cok parcali cizelgeyi kurar ve teslim eder.
/// Cekirdek <see cref="EditProject.ToJob"/>; komutun akisi sahte hazirlama ve kosu katmaniyla, uctan uca yol kisa bir
/// ffmpeg klibiyle olculur. Gecici dosyalar <c>.calisma/test-ciktilari/cli-teslim/</c>, test siler.
/// 18 Ekim 2026: Hizli ve Akilli kip gercek ffmpeg ile kendi kipinde teslim eder; iki kaynakli muzikli proje Tam'a duser,
/// muzik karisir (ortalama ses -40 dB ustu), ikinci parca ek kaynaktan gelir. Kaynaklar tek kodlamadan bolunur: ayri
/// x264 kodlamalarinin extradata'si farkli, kopya yolu acilmaz ve muziksiz kontrol de Tam'a duserdi.
/// </summary>
public sealed class CliProjeTeslimTests : IDisposable
{
    private readonly string _klasor = Path.Combine(TipSources.Root, ".calisma", "test-ciktilari", "cli-teslim", Guid.NewGuid().ToString("N"));

    public CliProjeTeslimTests() => Directory.CreateDirectory(_klasor);

    public void Dispose()
    {
        if (Directory.Exists(_klasor)) Directory.Delete(_klasor, true);
    }

    private static long S(double saniye) => EditTime.FromSeconds(saniye);

    private static readonly SourceStamp A = new(Path.GetFullPath("kaynak-a.mp4"), 1000, 11);
    private static readonly SourceStamp B = new(Path.GetFullPath("kaynak-b.mp4"), 2000, 22);

    private static EditProject UcParcali(string? kip = "Fast")
    {
        var model = new EditTimeline(new[]
        {
            new EditClip(S(1), S(3)),
            new EditClip(S(6), S(8)),
            new EditClip(S(0), S(2)) { Source = 1 }
        }, S(10), new[] { new TextLayer("baslik", 0, S(1)) }, new[] { S(4) });
        return EditProject.From(A, model, kip, new[] { B }, audioTrack: 1);
    }

    private static SourceStamp? Ayni(string yol) => yol == A.Path ? A : yol == B.Path ? B : null;

    private static CliRequest Ayristir(params string[] args)
    {
        var parsed = CliParser.Parse(args);
        Assert.True(parsed.Request is not null, parsed.ErrorKey + " " + parsed.ErrorArgument);
        return parsed.Request!;
    }

    [Fact]
    public void ProjeIsiCokParcaliCizelgeyiKurar()
    {
        var proje = UcParcali();

        var is_ = proje.ToJob(Ayni, out var bayat);

        Assert.Null(bayat);
        Assert.NotNull(is_);
        Assert.Equal(new[] { A.Path, B.Path }, is_!.Sources);
        Assert.Equal(proje.Clips, is_.Timeline.Clips);
        Assert.Equal(proje.Texts, is_.Timeline.Texts);
        Assert.Equal(new[] { S(4) }, is_.Timeline.ExtraSources);
        Assert.Equal(S(10), is_.Timeline.SourceDuration);
        Assert.Equal(ExportMode.Fast, is_.Mode);
        Assert.Equal(1, is_.AudioTrack);
    }

    [Fact]
    public void MuzikIziIseGecer()
    {
        var model = EditTimeline.FromSource(S(10));
        var muzik = new EditMusic(Path.GetFullPath("muzik.mp3"), S(30));
        model.SetMusic(muzik);

        var is_ = EditProject.From(A, model).ToJob(Ayni, out _);

        Assert.Equal(muzik, is_!.Timeline.Music);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Bogus")]
    [InlineData("fast")]
    [InlineData("7")]
    public void TaninmayanKipAkilliyaDuser(string? kip)
        => Assert.Equal(ExportMode.Smart, UcParcali(kip).ToJob(Ayni, out _)!.Mode);

    [Fact]
    public void DegisenEkKaynakIsiDurdurur()
    {
        var is_ = UcParcali().ToJob(yol => yol == B.Path ? B with { Size = 2001 } : Ayni(yol), out var bayat);

        Assert.Null(is_);
        Assert.Equal(B.Path, bayat);
    }

    [Fact]
    public void KayipAnaKaynakIsiDurdurur()
    {
        var is_ = UcParcali().ToJob(yol => yol == A.Path ? null : Ayni(yol), out var bayat);

        Assert.Null(is_);
        Assert.Equal(A.Path, bayat);
    }

    [Fact]
    public void BosCizelgeIsKurmaz()
    {
        var bos = EditProject.From(A, new EditTimeline(Array.Empty<EditClip>(), S(10)));

        Assert.Null(bos.ToJob(Ayni, out var bayat));
        Assert.Null(bayat);
    }

    [Theory]
    [InlineData("teslim")]
    [InlineData("export")]
    public void KomutTeslimIstegineDuser(string ad)
    {
        var istek = Ayristir(ad, "p.vsproj.json", "--cikti", "o.mp4", "--kip", "tam", "--meta-yok", "--json");

        Assert.Equal(CliCommand.Edit, istek.Command);
        Assert.Equal("p.vsproj.json", istek.Input);
        Assert.Equal("o.mp4", istek.Output);
        Assert.Equal(ExportMode.Full, istek.EditMode);
        Assert.True(istek.DropMetadata);
        Assert.True(istek.Json);
    }

    [Theory]
    [InlineData("hizli", ExportMode.Fast)]
    [InlineData("fast", ExportMode.Fast)]
    [InlineData("akilli", ExportMode.Smart)]
    [InlineData("smart", ExportMode.Smart)]
    [InlineData("tam", ExportMode.Full)]
    [InlineData("full", ExportMode.Full)]
    public void KipAdlariIkiDildeTaninir(string ad, ExportMode beklenen)
        => Assert.Equal(beklenen, Ayristir("teslim", "p.vsproj.json", "--mode", ad).EditMode);

    [Fact]
    public void KipVerilmezseProjeninKipiKalir() => Assert.Null(Ayristir("teslim", "p.vsproj.json").EditMode);

    [Theory]
    [InlineData("error.no-input", "teslim")]
    [InlineData("error.extra-input", "teslim", "a.vsproj.json", "b.vsproj.json")]
    [InlineData("error.unknown-option", "teslim", "a.vsproj.json", "--hedef", "8")]
    [InlineData("error.bad-edit-mode", "teslim", "a.vsproj.json", "--kip", "yavas")]
    [InlineData("error.missing-value", "teslim", "a.vsproj.json", "--cikti")]
    public void YanlisKullanimReddedilir(string anahtar, params string[] args)
        => Assert.Equal(anahtar, CliParser.Parse(args).ErrorKey);

    private sealed class Sahte
    {
        public EditProjectJob? Is;
        public string? Cikti;
        public bool MetaYok;
        public int Kosu;
        public IReadOnlyList<int> GeriSinir = Array.Empty<int>();
        public ExportMode Etkin = ExportMode.Fast;
    }

    private CliServices Servis(Sahte sahte, Func<string, SourceStamp?>? damga = null) => new()
    {
        MissingTool = () => null,
        Probe = (_, _) => throw new InvalidOperationException("probe must not run"),
        Stamp = damga ?? Ayni,
        EditPrepare = (is_, cikti, metaYok, _) =>
        {
            sahte.Is = is_;
            sahte.Cikti = cikti;
            sahte.MetaYok = metaYok;
            return Task.FromResult(new ExportPlan(is_.Mode, sahte.Etkin, Array.Empty<ExportStep>(), sahte.GeriSinir, 12, cikti, Path.Combine(_klasor, "is")));
        },
        EditRun = (plan, _, _) =>
        {
            sahte.Kosu++;
            if (PathEquality.Same(Path.GetDirectoryName(plan.OutputPath)!, _klasor)) File.WriteAllBytes(plan.OutputPath, new byte[2_000_000]);
            return Task.CompletedTask;
        }
    };

    private string ProjeYaz(EditProject proje)
    {
        var yol = Path.Combine(_klasor, "kesim" + EditProject.Extension);
        Assert.True(proje.Write(yol));
        return yol;
    }

    private static async Task<(int Cikis, string Stdout, string Stderr)> Kos(CliServices servis, params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var cikis = await CliApp.RunAsync(args, stdout, stderr, CliText.ForLanguage("tr"), servis, CancellationToken.None);
        return (cikis, stdout.ToString(), stderr.ToString());
    }

    [Fact]
    public async Task ProjeGirerTeslimCikar()
    {
        var sahte = new Sahte();
        var cikti = Path.Combine(_klasor, "cikti.mp4");

        var (cikis, stdout, stderr) = await Kos(Servis(sahte), "teslim", ProjeYaz(UcParcali()), "--cikti", cikti, "--kip", "tam", "--meta-yok");

        Assert.True(cikis == ExitCodes.InBand, stderr);
        Assert.Equal(1, sahte.Kosu);
        Assert.Equal(cikti, sahte.Cikti);
        Assert.True(sahte.MetaYok);
        Assert.Equal(ExportMode.Full, sahte.Is!.Mode);
        Assert.Equal(3, sahte.Is.Timeline.Clips.Count);
        Assert.Equal(new[] { A.Path, B.Path }, sahte.Is.Sources);
        Assert.Contains(cikti, stdout, StringComparison.Ordinal);
        Assert.Contains("2,0", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CiktiVerilmezseKaynaginYaninaYazar()
    {
        var sahte = new Sahte();
        var beklenen = EditOutputName.For(A.Path);

        var (cikis, _, stderr) = await Kos(Servis(sahte), "teslim", ProjeYaz(UcParcali()));

        Assert.True(cikis == ExitCodes.InBand, stderr);
        Assert.Equal(beklenen, sahte.Cikti);
        Assert.Equal(ExportMode.Fast, sahte.Is!.Mode);
        Assert.False(sahte.MetaYok);
    }

    [Fact]
    public async Task JsonSonucuParcaSayisiniVeKipiTasir()
    {
        var sahte = new Sahte { Etkin = ExportMode.Full };
        var cikti = Path.Combine(_klasor, "cikti.mp4");

        var (cikis, stdout, stderr) = await Kos(Servis(sahte), "export", ProjeYaz(UcParcali()), "-o", cikti, "--json");

        Assert.Equal(ExitCodes.InBand, cikis);
        using var belge = JsonDocument.Parse(stdout);
        var kok = belge.RootElement;
        Assert.Equal("teslim", kok.GetProperty("command").GetString());
        Assert.Equal(cikti, kok.GetProperty("output").GetString());
        Assert.Equal(3, kok.GetProperty("clips").GetInt32());
        Assert.Equal("Fast", kok.GetProperty("requestedMode").GetString());
        Assert.Equal("Full", kok.GetProperty("effectiveMode").GetString());
        Assert.True(kok.GetProperty("result").GetProperty("success").GetBoolean());
        Assert.Contains("\"type\":\"stage\"", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DegisenKaynakTeslimEtmez()
    {
        var sahte = new Sahte();

        var (cikis, _, stderr) = await Kos(Servis(sahte, yol => yol == B.Path ? B with { ModifiedUtcTicks = 99 } : Ayni(yol)),
            "teslim", ProjeYaz(UcParcali()));

        Assert.Equal(ExitCodes.Error, cikis);
        Assert.Null(sahte.Is);
        Assert.Contains(B.Path, stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OkunamayanProjeHata1()
    {
        var bozuk = Path.Combine(_klasor, "bozuk" + EditProject.Extension);
        File.WriteAllText(bozuk, "{ degil");
        var sahte = new Sahte();

        var (cikis, _, stderr) = await Kos(Servis(sahte), "teslim", bozuk);
        var (yokCikis, _, yokStderr) = await Kos(Servis(sahte), "teslim", Path.Combine(_klasor, "yok" + EditProject.Extension));

        Assert.Equal(ExitCodes.Error, cikis);
        Assert.Contains(bozuk, stderr, StringComparison.Ordinal);
        Assert.Equal(ExitCodes.Error, yokCikis);
        Assert.Contains("yok" + EditProject.Extension, yokStderr, StringComparison.Ordinal);
        Assert.Null(sahte.Is);
    }

    [Fact]
    public async Task BosCizelgeTeslimEtmez()
    {
        var sahte = new Sahte();

        var (cikis, _, _) = await Kos(Servis(sahte), "teslim", ProjeYaz(EditProject.From(A, new EditTimeline(Array.Empty<EditClip>(), S(10)))));

        Assert.Equal(ExitCodes.Error, cikis);
        Assert.Null(sahte.Is);
    }

    [Fact]
    public async Task CiktiKaynagaYazilamaz()
    {
        var sahte = new Sahte();

        var (cikis, _, _) = await Kos(Servis(sahte), "teslim", ProjeYaz(UcParcali()), "--cikti", B.Path);

        Assert.Equal(ExitCodes.Usage, cikis);
        Assert.Null(sahte.Is);
    }

    [Fact]
    public async Task GeriOynatmaSiniriKosuyuDurdurur()
    {
        var sahte = new Sahte { GeriSinir = new[] { 1 } };

        var (cikis, _, stderr) = await Kos(Servis(sahte), "teslim", ProjeYaz(UcParcali()), "-o", Path.Combine(_klasor, "c.mp4"));

        Assert.Equal(ExitCodes.Error, cikis);
        Assert.Equal(0, sahte.Kosu);
        Assert.Contains("2", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void YardimTeslimiAnlatir()
    {
        foreach (var dil in new[] { "en", "tr" })
        {
            var yardim = CliText.ForLanguage(dil).Format("help", "1");
            Assert.Contains("vidshrink teslim", yardim, StringComparison.Ordinal);
            Assert.Contains("--kip", yardim, StringComparison.Ordinal);
        }
        Assert.Contains("--mode", CliText.ForLanguage("en")["help"], StringComparison.Ordinal);
    }

    [Fact]
    public void TamamlamaTeslimiTanir()
    {
        var teslim = CliCompletion.Commands.Single(c => c.Names.Contains("teslim"));
        Assert.Contains("export", teslim.Names);
        foreach (var secenek in new[] { "--kip", "--mode", "--cikti", "-o", "--meta-yok", "--json" })
            Assert.Contains(secenek, teslim.Options);
        foreach (var kip in teslim.Values["--kip"])
            Assert.Null(CliParser.Parse(new[] { "teslim", "p.vsproj.json", "--kip", kip }).ErrorKey);
    }

    [FfmpegFact]
    public async Task GercekProjeIkiParcayiTekDosyadaTeslimEder()
    {
        var kaynak = Path.Combine(_klasor, "kaynak.mp4");
        Ffmpeg("-hide_banner", "-y", "-f", "lavfi", "-i", "testsrc2=s=160x120:r=25:d=4",
            "-c:v", "libx264", "-preset", "ultrafast", "-g", "25", "-threads", "2", kaynak);
        var bilgi = await FfprobeClient.ProbeAsync(kaynak);
        var model = new EditTimeline(new[] { new EditClip(S(0), S(1)), new EditClip(S(2), S(3)) }, S(bilgi.DurationSeconds));
        var proje = EditProject.From(SourceStamp.Of(kaynak)!, model, "Full");
        var cikti = Path.Combine(_klasor, "teslim.mp4");

        var (cikis, _, stderr) = await Kos(new CliServices(), "teslim", ProjeYaz(proje), "--cikti", cikti);

        Assert.True(cikis == ExitCodes.InBand, stderr);
        var sonuc = await FfprobeClient.ProbeAsync(cikti);
        Assert.InRange(sonuc.DurationSeconds, 1.8, 2.3);
    }

    private string SesliKaynak(string ad, string desen)
    {
        var yol = Path.Combine(_klasor, ad);
        Ffmpeg("-hide_banner", "-y", "-f", "lavfi", "-i", desen + "=s=160x120:r=25:d=4", "-f", "lavfi", "-i", "anullsrc=r=48000:cl=stereo",
            "-t", "4", "-c:v", "libx264", "-preset", "ultrafast", "-g", "25", "-threads", "2", "-c:a", "aac", "-b:a", "64k", "-shortest", yol);
        return yol;
    }

    private (string A, string B) AyniKodlamadanIkiKaynak()
    {
        var butun = Path.Combine(_klasor, "butun.mp4");
        Ffmpeg("-hide_banner", "-y", "-f", "lavfi", "-i", "testsrc2=s=160x120:r=25:d=4", "-f", "lavfi", "-i", "smptebars=s=160x120:r=25:d=4",
            "-f", "lavfi", "-i", "anullsrc=r=48000:cl=stereo", "-filter_complex", "[0:v][1:v]concat=n=2:v=1:a=0[v]", "-map", "[v]", "-map", "2:a",
            "-t", "8", "-c:v", "libx264", "-preset", "ultrafast", "-g", "25", "-threads", "2", "-c:a", "aac", "-b:a", "64k", butun);
        var a = Path.Combine(_klasor, "a.mp4");
        var b = Path.Combine(_klasor, "b.mp4");
        Ffmpeg("-hide_banner", "-y", "-i", butun, "-t", "4", "-c", "copy", a);
        Ffmpeg("-hide_banner", "-y", "-ss", "4", "-i", butun, "-c", "copy", b);
        return (a, b);
    }

    private async Task<(int Cikis, JsonElement Sonuc, string Stderr)> GercekTeslim(EditProject proje, string cikti, params string[] ek)
    {
        var (cikis, stdout, stderr) = await Kos(new CliServices(), new[] { "teslim", ProjeYaz(proje), "--cikti", cikti, "--json" }.Concat(ek).ToArray());
        Assert.True(cikis == ExitCodes.InBand, stderr);
        using var belge = JsonDocument.Parse(stdout);
        return (cikis, belge.RootElement.Clone(), stderr);
    }

    [FfmpegTheory]
    [InlineData("hizli", "Fast", 0.0, 1.0, 2.0, 3.0)]
    [InlineData("akilli", "Smart", 0.4, 1.4, 2.3, 3.3)]
    public async Task GercekProjeHizliVeAkilliKiptaKipindeTeslimEdilir(string kip, string etkin, double a0, double a1, double b0, double b1)
    {
        var kaynak = SesliKaynak("kaynak.mp4", "testsrc2");
        var bilgi = await FfprobeClient.ProbeAsync(kaynak);
        var model = new EditTimeline(new[] { new EditClip(S(a0), S(a1)), new EditClip(S(b0), S(b1)) }, S(bilgi.DurationSeconds));
        var cikti = Path.Combine(_klasor, kip + ".mp4");

        var (_, sonuc, _) = await GercekTeslim(EditProject.From(SourceStamp.Of(kaynak)!, model, "Full"), cikti, "--kip", kip);

        Assert.Equal(etkin, sonuc.GetProperty("requestedMode").GetString());
        Assert.Equal(etkin, sonuc.GetProperty("effectiveMode").GetString());
        var cikan = await FfprobeClient.ProbeAsync(cikti);
        Assert.InRange(cikan.DurationSeconds, 1.8, 2.4);
        Assert.Equal("h264", cikan.VideoCodec);
        Assert.NotNull(cikan.AudioCodec);
    }

    [FfmpegFact]
    public async Task GercekCokKaynakliMuzikliProjeTekDosyadaKarisir()
    {
        var (a, b) = AyniKodlamadanIkiKaynak();
        var muzik = Path.Combine(_klasor, "muzik.m4a");
        Ffmpeg("-hide_banner", "-y", "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000:duration=6", "-c:a", "aac", "-b:a", "64k", "-threads", "2", muzik);
        var sureA = (await FfprobeClient.ProbeAsync(a)).DurationSeconds;
        var sureB = (await FfprobeClient.ProbeAsync(b)).DurationSeconds;

        EditProject Proje(bool muzikli)
        {
            var model = new EditTimeline(new[]
            {
                new EditClip(S(0), S(1)),
                new EditClip(S(1), S(2)) { Source = 1 },
                new EditClip(S(2), S(3))
            }, S(sureA), extraSources: new[] { S(sureB) });
            if (muzikli) model.SetMusic(new EditMusic(muzik, S(6)));
            return EditProject.From(SourceStamp.Of(a)!, model, "Fast", new[] { SourceStamp.Of(b)! });
        }

        var cikti = Path.Combine(_klasor, "muzikli.mp4");
        var sessiz = Path.Combine(_klasor, "sessiz.mp4");
        var (_, sonuc, _) = await GercekTeslim(Proje(true), cikti);
        var (_, sessizSonuc, _) = await GercekTeslim(Proje(false), sessiz);
        Assert.Equal("Fast", sessizSonuc.GetProperty("effectiveMode").GetString());

        Assert.Equal(3, sonuc.GetProperty("clips").GetInt32());
        Assert.Equal(new[] { a, b }, sonuc.GetProperty("sources").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal("Fast", sonuc.GetProperty("requestedMode").GetString());
        Assert.Equal("Full", sonuc.GetProperty("effectiveMode").GetString());
        var cikan = await FfprobeClient.ProbeAsync(cikti);
        Assert.InRange(cikan.DurationSeconds, 2.8, 3.4);
        Assert.NotNull(cikan.AudioCodec);
        Assert.InRange(OrtalamaSes(cikti), -40, 0);
        Assert.True(OrtalamaSes(sessiz) < -70, "muziksiz teslim sessiz kalmali");
        var orta = Kare(cikti, 1.5);
        Assert.InRange(Fark(orta, Kare(b, 1.5)), 0, 8);
        Assert.True(Fark(orta, Kare(a, 1.5)) > 20, "ikinci parca ek kaynaktan gelmeli");
        Assert.InRange(Fark(Kare(cikti, 2.5), Kare(a, 2.5)), 0, 8);
    }

    private static double OrtalamaSes(string yol)
    {
        var cikti = FfmpegCikti(false, "-hide_banner", "-nostats", "-i", yol, "-vn", "-af", "volumedetect", "-f", "null", "-");
        var satir = cikti.Split('\n').Last(s => s.Contains("mean_volume:", StringComparison.Ordinal));
        var deger = satir[(satir.IndexOf("mean_volume:", StringComparison.Ordinal) + 12)..].Replace("dB", "").Trim();
        return double.Parse(deger, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static double Fark(string x, string y)
    {
        var p = Convert.FromHexString(x);
        var q = Convert.FromHexString(y);
        Assert.Equal(p.Length, q.Length);
        return p.Zip(q, (u, v) => Math.Abs(u - v)).Average();
    }

    private static string Kare(string yol, double an)
        => FfmpegCikti(true, "-hide_banner", "-ss", an.ToString(System.Globalization.CultureInfo.InvariantCulture), "-i", yol, "-frames:v", "1",
            "-vf", "scale=16:12", "-pix_fmt", "gray", "-f", "rawvideo", "-");

    private static string FfmpegCikti(bool stdout, params string[] args)
    {
        var psi = new ProcessStartInfo("ffmpeg") { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var surec = Process.Start(psi)!;
        var hata = surec.StandardError.ReadToEndAsync();
        var bellek = new MemoryStream();
        var oku = surec.StandardOutput.BaseStream.CopyToAsync(bellek);
        if (!surec.WaitForExit(30000))
        {
            surec.Kill(true);
            Assert.Fail("ffmpeg 30 sn icinde bitmedi.");
        }
        oku.GetAwaiter().GetResult();
        Assert.True(surec.ExitCode == 0, hata.GetAwaiter().GetResult());
        return stdout ? Convert.ToHexString(bellek.ToArray()) : hata.GetAwaiter().GetResult();
    }

    private static void Ffmpeg(params string[] args)
    {
        var psi = new ProcessStartInfo("ffmpeg") { RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var surec = Process.Start(psi)!;
        var hata = surec.StandardError.ReadToEndAsync();
        if (!surec.WaitForExit(30000))
        {
            surec.Kill(true);
            Assert.Fail("ffmpeg 30 sn icinde bitmedi.");
        }

        Assert.True(surec.ExitCode == 0, hata.GetAwaiter().GetResult());
    }
}
