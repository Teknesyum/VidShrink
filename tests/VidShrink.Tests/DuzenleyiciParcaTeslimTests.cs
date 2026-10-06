using System.Diagnostics;
using System.Globalization;
using Avalonia.Controls;
using VidShrink.App;
using VidShrink.App.Editing;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Core.Share;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Parcalari ayri dosyalara yazan teslim: <see cref="EditOutputName.Segments"/> adlari,
/// <see cref="EditExport.BuildSegments"/> parca basina plani, <see cref="EditExportRunner.RunSegmentsAsync"/>
/// sirali kosumu ve duzenleyicideki kutu. Canli kollar 4 sn'lik 320x240 kaynagi tek surecle isler.
/// Kanit <c>.calisma/worktree-agent-ae0de684a3b5fbf2d/parca-*</c>, test siler.
/// </summary>
public sealed class DuzenleyiciParcaTeslimTests
{
    private const long Gb = 1_000_000_000;

    private static string Kok => Path.Combine(GirdiKanit.Root, ".calisma", "worktree-agent-ae0de684a3b5fbf2d");

    private static long S(double saniye) => EditTime.FromSeconds(saniye);

    private static MediaInfo Bilgi() => new()
    {
        FilePath = "kaynak.mp4",
        FileSizeBytes = 1,
        DurationSeconds = 60,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 1,
        AudioCodec = "aac",
        PixelFormat = "yuv420p",
        BitDepth = 8
    };

    private static string Deger(IReadOnlyList<string> args, string anahtar) => args[args.ToList().IndexOf(anahtar) + 1];

    private static string[] Yollar(string on, int adet) => Enumerable.Range(0, adet).Select(i => on + i.ToString(CultureInfo.InvariantCulture)).ToArray();

    [Fact]
    public void ParcaAdlariSifirDolguluSiraNumarasiAlir()
    {
        var klasor = Path.Combine(Path.GetTempPath(), "yok-klasor");
        var cikti = Path.Combine(klasor, "tatil-duzenlenmis.mp4");

        Assert.Equal(
            new[] { "tatil-duzenlenmis-01.mp4", "tatil-duzenlenmis-02.mp4", "tatil-duzenlenmis-03.mp4" },
            EditOutputName.Segments(cikti, 3, _ => false).Select(Path.GetFileName));
        Assert.All(EditOutputName.Segments(cikti, 3, _ => false), yol => Assert.Equal(klasor, Path.GetDirectoryName(yol)));

        var yuzYirmi = EditOutputName.Segments(cikti, 120, _ => false);
        Assert.Equal(120, yuzYirmi.Count);
        Assert.Equal("tatil-duzenlenmis-001.mp4", Path.GetFileName(yuzYirmi[0]));
        Assert.Equal("tatil-duzenlenmis-099.mp4", Path.GetFileName(yuzYirmi[98]));
        Assert.Equal("tatil-duzenlenmis-120.mp4", Path.GetFileName(yuzYirmi[119]));

        Assert.Equal("a-10.MOV", Path.GetFileName(EditOutputName.Segments(Path.Combine(klasor, "a.MOV"), 10, _ => false)[9]));
        Assert.Equal("a-01.MOV", Path.GetFileName(Assert.Single(EditOutputName.Segments(Path.Combine(klasor, "a.MOV"), 1, _ => false))));
        Assert.Throws<ArgumentOutOfRangeException>(() => EditOutputName.Segments(cikti, 0, _ => false));
    }

    [Fact]
    public void AdlardanBiriVarsaButunKumeYeniGovdeyeKayar()
    {
        var klasor = Path.Combine(Path.GetTempPath(), "yok-klasor");
        var cikti = Path.Combine(klasor, "tatil-duzenlenmis.mp4");
        var dolu = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Path.Combine(klasor, "tatil-duzenlenmis-02.mp4") };

        Assert.Equal(
            new[] { "tatil-duzenlenmis-2-01.mp4", "tatil-duzenlenmis-2-02.mp4", "tatil-duzenlenmis-2-03.mp4" },
            EditOutputName.Segments(cikti, 3, dolu.Contains).Select(Path.GetFileName));

        dolu.Add(Path.Combine(klasor, "tatil-duzenlenmis-2-03.mp4"));
        Assert.Equal(
            new[] { "tatil-duzenlenmis-3-01.mp4", "tatil-duzenlenmis-3-02.mp4", "tatil-duzenlenmis-3-03.mp4" },
            EditOutputName.Segments(cikti, 3, dolu.Contains).Select(Path.GetFileName));

        Assert.Equal("tatil-duzenlenmis-01.mp4", Path.GetFileName(Assert.Single(EditOutputName.Segments(cikti, 1, dolu.Contains))));
        Assert.DoesNotContain(cikti, EditOutputName.Segments(cikti, 3, dolu.Contains));
    }

    [Fact]
    public void HerParcaKendiAraliginiKendiDosyasinaYazar()
    {
        var model = new EditTimeline(new[] { new EditClip(S(0), S(2)), new EditClip(S(3), S(5), 2m, true), new EditClip(S(6), S(9)) });
        var ciktilar = new[] { "p1.mp4", "p2.mp4", "p3.mp4" };
        var isler = new[] { "is1", "is2", "is3" };

        var planlar = EditExport.BuildSegments(model, Bilgi(), Array.Empty<double>(), 0, ExportMode.Full, ciktilar, isler, 8 * Gb);
        var butun = EditExport.Build(model, Bilgi(), Array.Empty<double>(), 0, ExportMode.Full, "b.mp4", "is", 8 * Gb);

        Assert.Equal(3, planlar.Count);
        Assert.Equal(ciktilar, planlar.Select(p => p.Steps[^1].Args[^1]));
        Assert.Equal(isler, planlar.Select(p => p.WorkDirectory));
        Assert.Equal(new[] { 2.0, 1.0, 3.0 }, planlar.Select(p => Math.Round(p.TotalWorkSeconds, 6)));
        Assert.Equal(butun.TotalWorkSeconds, planlar.Sum(p => p.TotalWorkSeconds), 6);

        var graflar = planlar.Select(p => Deger(Assert.Single(p.Steps).Args, "-filter_complex")).ToArray();
        Assert.StartsWith("[0:v]trim=start=0:end=2,", graflar[0], StringComparison.Ordinal);
        Assert.StartsWith("[0:v]trim=start=3:end=5,setpts=PTS-STARTPTS,reverse,setpts=PTS/2,", graflar[1], StringComparison.Ordinal);
        Assert.StartsWith("[0:v]trim=start=6:end=9,", graflar[2], StringComparison.Ordinal);
        Assert.All(graflar, g => Assert.Contains("concat=n=1:v=1:a=1", g, StringComparison.Ordinal));
        Assert.Contains("concat=n=3:v=1:a=1", Deger(butun.Steps[0].Args, "-filter_complex"), StringComparison.Ordinal);
        Assert.DoesNotContain("start=3", graflar[0], StringComparison.Ordinal);
        Assert.DoesNotContain("start=0", graflar[2], StringComparison.Ordinal);

        Assert.Empty(planlar[0].MotionClips);
        Assert.Equal(new[] { 1 }, planlar[1].MotionClips);
        Assert.Empty(planlar[2].MotionClips);
        Assert.All(planlar, p => Assert.Equal(ExportMode.Full, p.Effective));
    }

    [Fact]
    public void HizliKipteHerParcaKendiConcatListesiniAlir()
    {
        var model = new EditTimeline(new[] { new EditClip(S(1), S(3)), new EditClip(S(6), S(8)) });
        var planlar = EditExport.BuildSegments(model, Bilgi(), new[] { 0.0, 2.5, 5.0, 7.5 }, 0, ExportMode.Fast, Yollar("c", 2), Yollar("is", 2), 8 * Gb);

        Assert.Equal(2, planlar.Count);
        Assert.All(planlar, p => Assert.Equal(ExportMode.Fast, p.Effective));
        Assert.All(planlar, p => Assert.Equal("copy", Deger(Assert.Single(p.Steps).Args, "-c")));
        Assert.Equal("ffconcat version 1.0\nfile 'kaynak.mp4'\noutpoint 3\n", planlar[0].Steps[0].ListContent);
        Assert.Equal("ffconcat version 1.0\nfile 'kaynak.mp4'\ninpoint 5\noutpoint 8\n", planlar[1].Steps[0].ListContent);
        Assert.Equal(Path.Combine("is0", "list.ffconcat"), planlar[0].Steps[0].ListPath);
        Assert.Equal(Path.Combine("is1", "list.ffconcat"), planlar[1].Steps[0].ListPath);
        Assert.Equal(new[] { 3.0, 3.0 }, planlar.Select(p => Math.Round(p.TotalWorkSeconds, 6)));
    }

    [Fact]
    public void KolButunCizelgedenSecilirMetinKendiParcasinaKayar()
    {
        var klipler = new[] { new EditClip(S(0), S(2)), new EditClip(S(3), S(6)) };
        var metinli = new EditTimeline(klipler, texts: new[] { new TextLayer("ikinci", S(2.5), S(4)), new TextLayer("iki parca", S(1), S(3)) });
        var metinsiz = new EditTimeline(klipler);
        var kareler = new[] { 0.0, 1.0, 2.0, 3.0, 4.0, 5.0, 6.0 };

        var planlar = EditExport.BuildSegments(metinli, Bilgi(), kareler, 0, ExportMode.Smart, Yollar("c", 2), Yollar("is", 2), 8 * Gb);
        var duz = EditExport.BuildSegments(metinsiz, Bilgi(), kareler, 0, ExportMode.Smart, Yollar("c", 2), Yollar("is", 2), 8 * Gb);

        Assert.All(duz, p => Assert.Equal(ExportMode.Smart, p.Effective));
        Assert.All(duz, p => Assert.False(p.TextForcedFull));
        Assert.All(duz, p => Assert.Null(p.SubtitleContent));

        Assert.All(planlar, p => Assert.Equal(ExportMode.Smart, p.Requested));
        Assert.All(planlar, p => Assert.Equal(ExportMode.Full, p.Effective));
        Assert.All(planlar, p => Assert.True(p.TextForcedFull));

        var ilk = planlar[0].SubtitleContent!;
        var ikinci = planlar[1].SubtitleContent!;
        Assert.Contains("0:00:01.00,0:00:02.00", ilk, StringComparison.Ordinal);
        Assert.Contains("iki parca", ilk, StringComparison.Ordinal);
        Assert.DoesNotContain("ikinci", ilk, StringComparison.Ordinal);
        Assert.Contains("0:00:00.50,0:00:02.00", ikinci, StringComparison.Ordinal);
        Assert.Contains("0:00:00.00,0:00:01.00", ikinci, StringComparison.Ordinal);
        Assert.Equal(Path.Combine("is0", Path.GetFileName(planlar[0].SubtitlePath!)), planlar[0].SubtitlePath);
        Assert.Equal(Path.Combine("is1", Path.GetFileName(planlar[1].SubtitlePath!)), planlar[1].SubtitlePath);
    }

    [Fact]
    public void TekParcaTekPlanVerirEksikYolReddedilir()
    {
        var tek = new EditTimeline(new[] { new EditClip(S(1), S(4)) });
        var plan = Assert.Single(EditExport.BuildSegments(tek, Bilgi(), Array.Empty<double>(), 0, ExportMode.Full, new[] { "c.mp4" }, new[] { "is" }, 8 * Gb));
        var butun = EditExport.Build(tek, Bilgi(), Array.Empty<double>(), 0, ExportMode.Full, "c.mp4", "is", 8 * Gb);
        Assert.Equal(butun.Steps[0].Args, plan.Steps[0].Args);
        Assert.Equal(3.0, plan.TotalWorkSeconds, 6);

        var iki = new EditTimeline(new[] { new EditClip(S(0), S(1)), new EditClip(S(2), S(3)) });
        Assert.Throws<ArgumentException>(() => EditExport.BuildSegments(iki, Bilgi(), Array.Empty<double>(), 0, ExportMode.Full, new[] { "c.mp4" }, Yollar("is", 2), 8 * Gb));
        Assert.Throws<ArgumentException>(() => EditExport.BuildSegments(iki, Bilgi(), Array.Empty<double>(), 0, ExportMode.Full, Yollar("c", 2), new[] { "is" }, 8 * Gb));
        Assert.Throws<ArgumentException>(() => EditExport.BuildSegments(new EditTimeline(Array.Empty<EditClip>()), Bilgi(), Array.Empty<double>(), 0, ExportMode.Full, Array.Empty<string>(), Array.Empty<string>(), 8 * Gb));
    }

    [Fact]
    public async Task CanliIkiParcaSiraylaAyriDosyayaYazilir()
    {
        var kok = Klasor("parca-canli");
        try
        {
            var kaynak = Kaynak(kok);
            var model = new EditTimeline(new[] { new EditClip(S(0.5), S(2)), new EditClip(S(2.5), S(3.5)) });
            var cikti = Path.Combine(kok, "cikti.mp4");

            var planlar = await EditExportRunner.PrepareSegmentsAsync(kaynak, model, ExportMode.Full, cikti, 8 * Gb);
            var kesirler = new List<double>();
            await EditExportRunner.RunSegmentsAsync(planlar, new AnindaIlerleme(p => kesirler.Add(p.Fraction)));

            var bir = Path.Combine(kok, "cikti-01.mp4");
            var iki = Path.Combine(kok, "cikti-02.mp4");
            Assert.Equal(new[] { bir, iki }, planlar.Select(p => p.OutputPath));
            Assert.InRange(Sure(bir), 1.35, 1.65);
            Assert.InRange(Sure(iki), 0.85, 1.15);
            Assert.False(File.Exists(cikti));

            Assert.NotEmpty(kesirler);
            Assert.Equal(kesirler.OrderBy(k => k), kesirler);
            Assert.Equal(1.0, kesirler[^1], 6);
            Assert.Contains(kesirler, k => k > 0 && k <= 0.6 + 1e-9);
            Assert.Empty(Directory.GetFileSystemEntries(kok, "vidshrink_partial_*"));

            var yeniden = await EditExportRunner.PrepareSegmentsAsync(kaynak, model, ExportMode.Full, cikti, 8 * Gb);
            Assert.Equal(new[] { "cikti-2-01.mp4", "cikti-2-02.mp4" }, yeniden.Select(p => Path.GetFileName(p.OutputPath)));
        }
        finally
        {
            Sil("parca-canli");
        }
    }

    [Fact]
    public async Task DusenParcaKalanlariDurdururHangiParcaOldugunuSoyler()
    {
        var kok = Klasor("parca-hata");
        try
        {
            var kaynak = Kaynak(kok);
            var model = new EditTimeline(new[] { new EditClip(S(0.5), S(1.5)), new EditClip(S(1.5), S(2.5)), new EditClip(S(2.5), S(3.5)) });
            var planlar = (await EditExportRunner.PrepareSegmentsAsync(kaynak, model, ExportMode.Full, Path.Combine(kok, "cikti.mp4"), 8 * Gb)).ToArray();
            var saglam = planlar.ToArray();

            var adim = planlar[1].Steps[0];
            var bozuk = adim.Args.Select(a => a == kaynak ? Path.Combine(kok, "olmayan-kaynak.mp4") : a).ToArray();
            Assert.NotEqual(adim.Args, bozuk);
            planlar[1] = planlar[1] with { Steps = new[] { adim with { Args = bozuk } } };

            var hata = await Assert.ThrowsAsync<EditSegmentException>(() => EditExportRunner.RunSegmentsAsync(planlar, null));

            Assert.Equal(1, hata.Index);
            Assert.Equal(3, hata.Count);
            Assert.NotNull(hata.InnerException);
            Assert.InRange(Sure(Path.Combine(kok, "cikti-01.mp4")), 0.85, 1.15);
            Assert.False(File.Exists(Path.Combine(kok, "cikti-02.mp4")));
            Assert.False(File.Exists(Path.Combine(kok, "cikti-03.mp4")));
            Assert.Empty(Directory.GetFileSystemEntries(kok, "vidshrink_partial_*"));

            File.Delete(Path.Combine(kok, "cikti-01.mp4"));
            await EditExportRunner.RunSegmentsAsync(saglam, null);
            Assert.True(File.Exists(Path.Combine(kok, "cikti-03.mp4")));
        }
        finally
        {
            Sil("parca-hata");
        }
    }

    [Fact]
    public async Task IptalKalanParcalariYazmazYarimDosyaBirakmaz()
    {
        var kok = Klasor("parca-iptal");
        try
        {
            var kaynak = Kaynak(kok);
            var model = new EditTimeline(new[] { new EditClip(S(0), S(2)), new EditClip(S(2), S(4)) });
            var planlar = await EditExportRunner.PrepareSegmentsAsync(kaynak, model, ExportMode.Full, Path.Combine(kok, "cikti.mp4"), 8 * Gb);

            using var cts = new CancellationTokenSource();
            var gorulen = 0.0;
            var ilerleme = new AnindaIlerleme(p => { if (p.Fraction > 0 && gorulen == 0) { gorulen = p.Fraction; cts.Cancel(); } });
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => EditExportRunner.RunSegmentsAsync(planlar, ilerleme, cts.Token));

            Assert.InRange(gorulen, double.Epsilon, 0.5 + 1e-9);
            Assert.False(File.Exists(Path.Combine(kok, "cikti-01.mp4")));
            Assert.False(File.Exists(Path.Combine(kok, "cikti-02.mp4")));
            Assert.Empty(Directory.GetFileSystemEntries(kok, "vidshrink_partial_*"));
        }
        finally
        {
            Sil("parca-iptal");
        }
    }

    [Fact]
    public void KutuTekParcadaPasifBolunceAyriDosyalarYazilirPaylasimTekDosyaKalir()
    {
        var kok = Klasor("parca-arayuz");
        try
        {
            var kaynak = Kaynak(kok, "kaynak.mp4", 6);
            var ledger = Path.Combine(kok, "paylasimlar.json");

            var s = AppHost.Run(() =>
            {
                var view = new EditorView { RevealFolder = _ => { } };
                view.Player.EngineFactory = () => new YolMotoru(sure: 6);
                SahteSaglayici? saglayici = null;
                view.CreateShareFlow = () => new ShareFlow(t => saglayici ??= new SahteSaglayici(t), new ShareLedger(ledger));
                var window = new Window { Width = 1100, Height = 700, Content = view };
                window.Show();
                try
                {
                    var kapali = view.ExportSeparatelyEnabled;
                    Bekle(view, view.OpenSourceAsync(kaynak), 10);
                    var tekEtkin = view.ExportSeparatelyEnabled;
                    view.ExportSeparately = true;
                    var tekAyri = view.ExportSeparately;

                    view.TimelineView.Playhead = EditTime.FromSeconds(4);
                    view.Split();
                    var bolunmus = (Etkin: view.ExportSeparatelyEnabled, Ayri: view.ExportSeparately);

                    var kaydet = view.SaveAsync();
                    Bekle(view, kaydet, 90);
                    var ilk = (Ok: kaydet.Result, Planlar: view.LastPlans.Select(p => p.OutputPath).ToArray(), Yazilan: view.ExportedPaths.ToArray(),
                        Sayi: view.Exports, Durum: view.ExportStatusText);

                    var tekrar = view.SaveAsync();
                    Bekle(view, tekrar, 10);
                    var tekrarSayi = view.Exports;

                    var paylas = view.ShareAsync();
                    Bekle(view, paylas, 90);
                    return (kapali, tekEtkin, tekAyri, bolunmus, ilk, tekrarSayi,
                        Paylas: (Ok: paylas.Result, Sayi: view.Exports, Yuklenen: saglayici?.Yuklenen.ToList() ?? new List<string>(), Ayri: view.ExportSeparately));
                }
                finally
                {
                    view.Player.Close();
                    window.Close();
                }
            });

            var bir = Path.Combine(kok, "kaynak-duzenlenmis-01.mp4");
            var iki = Path.Combine(kok, "kaynak-duzenlenmis-02.mp4");
            var tek = Path.Combine(kok, "kaynak-duzenlenmis.mp4");

            Assert.False(s.kapali);
            Assert.False(s.tekEtkin);
            Assert.False(s.tekAyri);
            Assert.Equal((true, true), s.bolunmus);

            Assert.True(s.ilk.Ok, s.ilk.Durum);
            Assert.Equal(new[] { bir, iki }, s.ilk.Planlar);
            Assert.Equal(new[] { bir, iki }, s.ilk.Yazilan);
            Assert.Equal(1, s.ilk.Sayi);
            Assert.Equal(1, s.tekrarSayi);
            Assert.Contains(kok, s.ilk.Durum, StringComparison.Ordinal);
            Assert.DoesNotContain("-01", s.ilk.Durum, StringComparison.Ordinal);
            Assert.InRange(Sure(bir), 3.8, 4.2);
            Assert.InRange(Sure(iki), 1.8, 2.2);

            Assert.True(s.Paylas.Ok);
            Assert.Equal(2, s.Paylas.Sayi);
            Assert.Equal(new[] { tek }, s.Paylas.Yuklenen);
            Assert.True(s.Paylas.Ayri);
            Assert.InRange(Sure(tek), 5.8, 6.2);
            Assert.Empty(Directory.GetFileSystemEntries(kok, "vidshrink_partial_*"));
        }
        finally
        {
            Sil("parca-arayuz");
        }
    }

    [Theory]
    [InlineData("editor.export.separate", 0)]
    [InlineData("editor.export.segments-done", 2)]
    [InlineData("editor.export.segment-failed", 2)]
    public void YeniAnahtarlarButunDillerdeYerTutucularYerinde(string key, int yerTutucu)
    {
        Assert.Equal(42, Locales.Languages.Count);
        foreach (var language in Locales.Languages)
        {
            var keys = VidShrink.App.Localization.Strings.KeysOf(language).ToHashSet(StringComparer.Ordinal);
            Assert.True(keys.Contains(key), $"{language} dilinde {key} yok.");
            var metin = VidShrink.App.Localization.Strings.GetIn(language, key);
            Assert.False(string.IsNullOrWhiteSpace(metin), $"{language} dilinde {key} boş.");
            for (var i = 0; i < 2; i++)
                Assert.True(metin.Contains("{" + i.ToString(CultureInfo.InvariantCulture) + "}", StringComparison.Ordinal) == i < yerTutucu, $"{language} {key}: {metin}");
        }
    }

    private static string Klasor(string ad)
    {
        var yol = Path.Combine(Kok, ad);
        if (Directory.Exists(yol)) Directory.Delete(yol, true);
        Directory.CreateDirectory(yol);
        return yol;
    }

    private static void Sil(string ad) => KanitKapanisi.Kapat(Kok, ad);

    private static void Bekle(EditorView view, Task gorev, double saniye)
    {
        DenetimSurucu.Pump(view.Player, () => gorev.IsCompleted, saniye);
        Assert.True(gorev.IsCompleted, $"{saniye} sn icinde bitmedi: {view.ExportStatusText}");
    }

    private static string Kaynak(string klasor, string ad = "kaynak.mp4", int saniye = 4)
    {
        var kaynak = Path.Combine(klasor, ad);
        var sure = saniye.ToString(CultureInfo.InvariantCulture);
        var psi = new ProcessStartInfo("ffmpeg") { RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in new[]
                 {
                     "-hide_banner", "-y", "-f", "lavfi", "-i", "testsrc2=s=320x240:r=30:d=" + sure, "-f", "lavfi", "-i", "sine=f=440:d=" + sure,
                     "-c:v", "libx264", "-preset", "ultrafast", "-g", "30", "-threads", "2", "-c:a", "aac", "-shortest", kaynak
                 })
            psi.ArgumentList.Add(a);
        using var surec = Process.Start(psi)!;
        var hata = surec.StandardError.ReadToEndAsync();
        if (!surec.WaitForExit(30000))
        {
            surec.Kill(true);
            Assert.Fail("ffmpeg 30 sn icinde bitmedi.");
        }

        Assert.True(surec.ExitCode == 0, hata.GetAwaiter().GetResult());
        return kaynak;
    }

    private static double Sure(string dosya)
    {
        var psi = new ProcessStartInfo("ffprobe", $"-v error -show_entries format=duration -of csv=p=0 \"{dosya}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var surec = Process.Start(psi)!;
        var hata = surec.StandardError.ReadToEndAsync();
        var metin = surec.StandardOutput.ReadToEnd();
        Assert.True(surec.WaitForExit(15000));
        Assert.True(surec.ExitCode == 0, hata.GetAwaiter().GetResult());
        return double.Parse(metin.Trim(), CultureInfo.InvariantCulture);
    }

    private sealed class AnindaIlerleme : IProgress<EncodeProgress>
    {
        private readonly Action<EncodeProgress> _al;

        public AnindaIlerleme(Action<EncodeProgress> al) => _al = al;

        public void Report(EncodeProgress value) => _al(value);
    }

    private sealed class SahteSaglayici : IShareProvider
    {
        public SahteSaglayici(ShareTarget target) => Target = target;

        public List<string> Yuklenen { get; } = new();

        public ShareTarget Target { get; }

        public bool CanDelete => false;

        public Task<ShareResult> UploadAsync(string filePath, int? retentionDays = null, IProgress<UploadProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            Yuklenen.Add(filePath);
            return Task.FromResult(ShareResult.Success(new ShareLink(Target.Id, "f1", "https://ornek.test/f1", Path.GetFileName(filePath), DateTimeOffset.UtcNow)));
        }

        public Task<ShareResult> CheckHealthAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(ShareResult.Success(new ShareLink(Target.Id, "h", "https://ornek.test/h", "h", DateTimeOffset.UtcNow)));

        public Task<ShareResult> DeleteAsync(ShareLink link, CancellationToken cancellationToken = default)
            => Task.FromResult(ShareResult.Success(link));
    }
}
