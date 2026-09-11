using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using VidShrink.App.Playback;
using VidShrink.Core.Playback;
using VidShrink.Ffmpeg;
using VidShrink.Player;
using Xunit;

namespace VidShrink.Tests;

internal static class KarsilastirmaKanit
{
    internal static string Folder
    {
        get
        {
            var path = Path.Combine(GirdiKanit.Root, ".calisma", "dalga5");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    internal static void Write(string name, string body)
        => File.WriteAllText(Path.Combine(Folder, name), body, new UTF8Encoding(false));

    internal static string F(double value) => value.ToString("0.000", CultureInfo.InvariantCulture);
}

internal static class SeritKlip
{
    internal const int Fps = 30;
    internal const int Bits = 10;
    internal const int Width = 320;
    internal const int Height = 180;
    internal const double Sure = 10;

    private const int UretimTavaniMs = 300_000;

    internal static string Kaynak => Uret("serit-kaynak-320x180-30.mp4",
        "-f", "lavfi", "-i", $"color=c=black:size={Width}x{Height}:rate={Fps}:duration={Sure.ToString(CultureInfo.InvariantCulture)}",
        "-vf", $"format=yuv420p,geq=lum='if(bitand(N,pow(2,floor(X*{Bits}/W))),235,16)':cb=128:cr=128",
        "-c:v", "libx264", "-preset", "ultrafast", "-crf", "8", "-g", "30", "-keyint_min", "30", "-sc_threshold", "0",
        "-pix_fmt", "yuv420p", "-an");

    internal static string Cikti => UretKaynaktan("serit-cikti-crf35.mp4",
        "-c:v", "libx264", "-preset", "ultrafast", "-crf", "35", "-g", "90", "-keyint_min", "90", "-sc_threshold", "0",
        "-pix_fmt", "yuv420p", "-an");

    internal static int Hedef(double seconds) => (int)Math.Round(seconds * Fps, MidpointRounding.AwayFromZero);

    internal static int? Oku(PlaybackFrame frame, int half)
    {
        var panel = frame.SplitX;
        var value = 0;
        for (var bit = 0; bit < Bits; bit++)
        {
            var x = half * panel + (int)((bit + 0.5) * panel / Bits);
            var ones = 0;
            var zeros = 0;
            foreach (var y in new[] { frame.Height / 4, frame.Height / 2, frame.Height * 3 / 4 })
            {
                var g = frame.Buffer[(y * frame.Width + x) * 4 + 1];
                if (g >= 192) ones++;
                else if (g <= 64) zeros++;
            }

            if (ones == 3) value |= 1 << bit;
            else if (zeros != 3) return null;
        }

        return value;
    }

    private static string UretKaynaktan(string name, params string[] args)
    {
        var kaynak = Kaynak;
        return Uret(name, new[] { "-i", kaynak }.Concat(args).ToArray());
    }

    private static string Uret(string name, params string[] args)
    {
        var path = Path.Combine(MotorKanit.Folder, name);
        if (File.Exists(path) && new FileInfo(path).Length > 0) return path;

        Assert.True(ToolLocator.IsAvailable(out var missing), $"klip uretimi icin {missing} gerekli; bu test ffmpeg olmadan kirmizi kalir");

        var partial = path + ".part.mp4";
        var psi = new ProcessStartInfo(ToolLocator.Ffmpeg)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-y");
        psi.ArgumentList.Add("-hide_banner");
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        psi.ArgumentList.Add(partial);

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(UretimTavaniMs))
        {
            try { process.Kill(true); } catch { }
            throw new TimeoutException($"klip uretimi {UretimTavaniMs} ms icinde bitmedi: {name}");
        }

        stdout.GetAwaiter().GetResult();
        var log = stderr.GetAwaiter().GetResult();
        Assert.True(process.ExitCode == 0, $"ffmpeg {name} uretemedi (kod {process.ExitCode}): {log[Math.Max(0, log.Length - 600)..]}");
        File.Move(partial, path, true);
        return path;
    }
}

public sealed class OynaticiKarsilastirmaTests
{
    private sealed record Okuma(int? Sol, int? Sag, double SolDamga, double SagDamga, long Sira);

    private static Okuma Oku(PlaybackFrame frame)
        => new(SeritKlip.Oku(frame, 0), SeritKlip.Oku(frame, 1), frame.Presentation.TotalSeconds, frame.RightPresentation.TotalSeconds, frame.Sequence);

    private static async Task<Okuma?> SonKareAsync(IComparisonFrameSource source, TimeSpan limit, TimeSpan durulma)
    {
        Okuma? son = null;
        var saat = Stopwatch.StartNew();
        var sonGelis = TimeSpan.Zero;
        while (saat.Elapsed < limit)
        {
            if (source.TryTake(out var frame))
            {
                son = Oku(frame);
                source.Return(frame);
                sonGelis = saat.Elapsed;
                continue;
            }

            if (son is not null && saat.Elapsed - sonGelis >= durulma) break;
            await Task.Delay(2);
        }

        return son;
    }

    private static string Satir(string etiket, Okuma o, int? hedef = null)
    {
        var fark = o.Sol is { } l && o.Sag is { } r ? (l - r).ToString(CultureInfo.InvariantCulture) : "?";
        var hedefMetni = hedef is { } h ? $" hedef {h}" : "";
        return $"{etiket}{hedefMetni} sol {o.Sol?.ToString(CultureInfo.InvariantCulture) ?? "okunamadi"} sag {o.Sag?.ToString(CultureInfo.InvariantCulture) ?? "okunamadi"} fark {fark} | damga sol {KarsilastirmaKanit.F(o.SolDamga)} sag {KarsilastirmaKanit.F(o.SagDamga)} sira {o.Sira}";
    }

    [Fact]
    public async Task IkiYariDurgunAramadaVeOynarkenEnFazlaBirKareAyrisir()
    {
        var kaynak = SeritKlip.Kaynak;
        var cikti = SeritKlip.Cikti;

        using var source = new EngineComparisonFrameSource();
        await source.StartAsync(new ComparisonFrameRequest
        {
            LeftPath = kaynak,
            RightPath = cikti,
            PanelWidth = SeritKlip.Width,
            PanelHeight = SeritKlip.Height,
            Fps = SeritKlip.Fps,
            Realtime = true,
            Loop = false
        });
        Assert.True(source.Status.State != ComparisonSourceState.Kullanilamiyor, $"motor acilmadi: {source.Status.MessageKey} {source.Status.MessageArg}");
        source.Pause();

        var rapor = new StringBuilder();
        rapor.AppendLine("dalga 5 karsilastirma paneli: iki motor ornegi, sol kaynak, sag crf35 yeniden kodlama");
        rapor.AppendLine($"klip: {SeritKlip.Width}x{SeritKlip.Height} {SeritKlip.Fps} fps {SeritKlip.Sure} sn, kare numarasi {SeritKlip.Bits} bitlik serit (geq N), sag GOP 90, sol GOP 30");
        rapor.AppendLine("okuma: her serit merkezinde uc satirin G kanali; >=192 bir, <=64 sifir, arasi okunamadi");
        rapor.AppendLine();
        rapor.AppendLine("durgun arama (duraklatilmis, exact):");

        var hedefler = new[] { 1.0, 2.5, 3.7, 5.0, 6.3, 7.9, 0.5 };
        var durgun = new List<(int Hedef, Okuma? Okuma)>();
        foreach (var hedef in hedefler)
        {
            await source.SeekAsync(TimeSpan.FromSeconds(hedef));
            var okuma = await SonKareAsync(source, TimeSpan.FromSeconds(4), TimeSpan.FromMilliseconds(300));
            durgun.Add((SeritKlip.Hedef(hedef), okuma));
            rapor.AppendLine(okuma is null ? $"  {KarsilastirmaKanit.F(hedef)} sn: kare gelmedi" : Satir($"  {KarsilastirmaKanit.F(hedef)} sn", okuma, SeritKlip.Hedef(hedef)));
        }

        await source.SeekAsync(TimeSpan.FromSeconds(1.0));
        while (source.TryTake(out var bayat)) source.Return(bayat);
        source.Play();

        var oynarken = new List<Okuma>();
        var saat = Stopwatch.StartNew();
        while (saat.Elapsed < TimeSpan.FromSeconds(3))
        {
            if (source.TryTake(out var frame))
            {
                oynarken.Add(Oku(frame));
                source.Return(frame);
            }
            else
            {
                await Task.Delay(1);
            }
        }
        source.Pause();

        var okunan = oynarken.Where(o => o.Sol is not null && o.Sag is not null).ToList();
        var farklar = okunan.Select(o => Math.Abs(o.Sol!.Value - o.Sag!.Value)).ToList();
        rapor.AppendLine();
        rapor.AppendLine($"oynarken 3 sn: {oynarken.Count} kare, okunan {okunan.Count}, fark dagilimi " +
            string.Join(" ", farklar.GroupBy(f => f).OrderBy(g => g.Key).Select(g => $"{g.Key}:{g.Count()}")));
        if (okunan.Count > 0)
            rapor.AppendLine($"sol ilerleme: {okunan[0].Sol} -> {okunan[^1].Sol}, en buyuk fark {farklar.Max()}");
        foreach (var o in oynarken) rapor.AppendLine(Satir("  ", o));

        await source.StopAsync();
        KarsilastirmaKanit.Write("k1-iki-yari-kare-farki.txt", rapor.ToString());

        foreach (var (hedef, okuma) in durgun)
        {
            Assert.True(okuma is not null, $"hedef kare {hedef}: aramadan sonra kare gelmedi");
            Assert.True(okuma!.Sol is not null && okuma.Sag is not null, $"hedef kare {hedef}: serit okunamadi ({Satir("", okuma)})");
            Assert.True(Math.Abs(okuma.Sol!.Value - okuma.Sag!.Value) <= 1, $"durgun aramada iki yari ayrisik: {Satir("", okuma, hedef)}");
            Assert.True(Math.Abs(okuma.Sol.Value - hedef) <= 1, $"sol yari hedefte degil: {Satir("", okuma, hedef)}");
        }

        Assert.True(okunan.Count >= 45, $"oynarken 3 sn'de {okunan.Count} okunur kare");
        Assert.True(okunan.Count == oynarken.Count, $"oynarken okunamayan kare {oynarken.Count - okunan.Count}");
        Assert.True(farklar.Max() <= 1, $"oynarken iki yari {farklar.Max()} kare ayrisik");
        Assert.True(okunan[^1].Sol!.Value - okunan[0].Sol!.Value >= 60, $"sol yari ilerlemedi: {okunan[0].Sol} -> {okunan[^1].Sol}");
    }

    [Fact]
    public async Task OnizlemeSesiGoruntusuzMotordaAtlarVeCalar()
    {
        var klip = MotorKlipleri.Kucuk;
        using var audio = new PreviewAudio();
        await audio.AttachAsync(klip);

        Assert.True(audio.Attached, "ses motoru takilmadi");
        Assert.True(audio.HasAudio, "sesli klipte ses akisi gorulmedi");
        var engine = Assert.IsType<MpvEngine>(audio.Engine);
        var vid = engine.GetProperty("vid");

        audio.SeekTo(12.0);
        var saat = Stopwatch.StartNew();
        while (Math.Abs(engine.PositionSeconds - 12.0) > 0.1 && saat.Elapsed < TimeSpan.FromSeconds(3)) await Task.Delay(10);
        var aramaSonrasi = engine.PositionSeconds;

        audio.Play();
        await Task.Delay(800);
        var oynarken = engine.PositionSeconds;
        var oynuyor = !engine.IsPaused;

        audio.Pause();
        saat.Restart();
        while (!engine.IsPaused && saat.Elapsed < TimeSpan.FromSeconds(2)) await Task.Delay(10);
        var duraklatildi = engine.IsPaused;
        await Task.Delay(200);
        var durdugu = engine.PositionSeconds;
        await Task.Delay(400);
        var sonra = engine.PositionSeconds;

        KarsilastirmaKanit.Write("k2-onizleme-sesi.txt",
            $"klip: {Path.GetFileName(klip)} sure {KarsilastirmaKanit.F(engine.DurationSeconds)} sn{Environment.NewLine}"
            + $"vid: {vid}, render edilen kare {engine.FramesRendered}, ses {engine.HasAudio}{Environment.NewLine}"
            + $"SeekTo(12) sonrasi konum {KarsilastirmaKanit.F(aramaSonrasi)}, arama sayaci {audio.Seeks}{Environment.NewLine}"
            + $"Play + 800 ms: konum {KarsilastirmaKanit.F(oynarken)}, oynuyor {oynuyor}{Environment.NewLine}"
            + $"Pause: duraklatildi {duraklatildi}, konum {KarsilastirmaKanit.F(durdugu)} -> 400 ms sonra {KarsilastirmaKanit.F(sonra)}{Environment.NewLine}");

        Assert.Equal("no", vid);
        Assert.Equal(0, engine.FramesRendered);
        Assert.Equal(1, audio.Seeks);
        Assert.True(Math.Abs(aramaSonrasi - 12.0) <= 0.1, $"ses motoru 12 sn'ye inmedi: {KarsilastirmaKanit.F(aramaSonrasi)}");
        Assert.True(oynuyor, "Play sonrasi ses motoru duraklatilmis");
        Assert.True(oynarken - aramaSonrasi >= 0.4, $"Play sonrasi konum ilerlemedi: {KarsilastirmaKanit.F(aramaSonrasi)} -> {KarsilastirmaKanit.F(oynarken)}");
        Assert.True(duraklatildi, "Pause sonrasi ses motoru oynamaya devam ediyor");
        Assert.True(Math.Abs(sonra - durdugu) < 0.05, $"Pause sonrasi konum ilerledi: {KarsilastirmaKanit.F(durdugu)} -> {KarsilastirmaKanit.F(sonra)}");
    }
}

public sealed class OynaticiEskiYolTests
{
    private static readonly Regex Yasak = new(
        @"\b(DecoderPipe|AudioSink|PlaybackClock|PipeComparisonFrameSource|ComparisonGraph|NAudio)\b|VidShrink\.Ffmpeg\.Playback",
        RegexOptions.Compiled);

    private static readonly string[] Uzantilar = { ".cs", ".axaml", ".csproj", ".props", ".targets" };

    private static IReadOnlyList<string> CanliProjeKlasorleri(string root)
    {
        var sln = File.ReadAllText(Path.Combine(root, "VidShrink.sln"));
        return Regex.Matches(sln, "\"([^\"]+\\.csproj)\"")
            .Select(m => Path.GetDirectoryName(Path.GetFullPath(Path.Combine(root, m.Groups[1].Value.Replace('\\', Path.DirectorySeparatorChar))))!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool Derleme(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(p => p is "bin" or "obj");
    }

    internal static (int Taranan, List<string> Bulgular, IReadOnlyList<string> Projeler) Tara(string root, string kendisi)
    {
        var projeler = CanliProjeKlasorleri(root);
        var dosyalar = projeler
            .SelectMany(dir => Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            .Concat(Directory.EnumerateFiles(root, "Directory.*", SearchOption.TopDirectoryOnly))
            .Where(f => Uzantilar.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .Where(f => !Derleme(Path.GetRelativePath(root, f)))
            .Where(f => !string.Equals(Path.GetFileName(f), kendisi, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var bulgular = new List<string>();
        foreach (var dosya in dosyalar)
        {
            var satirlar = File.ReadAllLines(dosya);
            for (var i = 0; i < satirlar.Length; i++)
                if (Yasak.IsMatch(satirlar[i]))
                    bulgular.Add($"{Path.GetRelativePath(root, dosya)}:{i + 1}: {satirlar[i].Trim()}");
        }

        return (dosyalar.Count, bulgular, projeler);
    }

    [Fact]
    public void EskiBoruyaVeNAudioyaCanliBasvuruYok()
    {
        var root = GirdiKanit.Root;
        var (taranan, bulgular, projeler) = Tara(root, "OynaticiKarsilastirmaTests.cs");

        var derlemeler = new[]
        {
            typeof(ToolLocator).Assembly,
            typeof(EngineComparisonFrameSource).Assembly,
            typeof(MpvEngine).Assembly,
            typeof(PlaybackFrame).Assembly,
            typeof(OynaticiEskiYolTests).Assembly
        };
        var eskiTurler = derlemeler
            .SelectMany(a => a.GetTypes())
            .Where(t => t.Namespace == "VidShrink.Ffmpeg.Playback" || Yasak.IsMatch(t.Name))
            .Select(t => t.FullName!)
            .ToList();
        var naudio = derlemeler
            .SelectMany(a => a.GetReferencedAssemblies().Select(r => $"{a.GetName().Name} -> {r.Name}"))
            .Where(r => r.Contains("NAudio", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var yuklu = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetName().Name ?? "")
            .Where(n => n.StartsWith("NAudio", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var mainWindow = File.ReadAllText(Path.Combine(root, "src", "VidShrink.App", "MainWindow.axaml.cs"));
        var motorFabrikasi = mainWindow.Contains("new PanelHost(Preview, () => new EngineComparisonFrameSource())", StringComparison.Ordinal);
        var sesAlani = typeof(PreviewAudio)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Any(f => f.FieldType == typeof(IPlaybackEngine));

        var rapor = new StringBuilder();
        rapor.AppendLine($"canli projeler (VidShrink.sln): {projeler.Count}");
        foreach (var p in projeler) rapor.AppendLine($"  {Path.GetRelativePath(root, p)}");
        rapor.AppendLine($"taranan dosya (.cs .axaml .csproj .props .targets, bin/obj haric): {taranan}");
        rapor.AppendLine($"eski yol / NAudio satiri: {bulgular.Count}");
        foreach (var b in bulgular) rapor.AppendLine($"  {b}");
        rapor.AppendLine($"derlemelerde eski tur: {eskiTurler.Count} {string.Join(", ", eskiTurler)}");
        rapor.AppendLine($"NAudio derleme basvurusu: {naudio.Count} {string.Join(", ", naudio)}");
        rapor.AppendLine($"surecte yuklu NAudio: {yuklu.Count}");
        rapor.AppendLine($"MainWindow karsilastirma fabrikasi motorda: {motorFabrikasi}");
        rapor.AppendLine($"PreviewAudio motor alani: {sesAlani}");
        KarsilastirmaKanit.Write("k3-eski-yol-taramasi.txt", rapor.ToString());

        Assert.True(projeler.Count >= 5, $"sln'den {projeler.Count} proje okundu");
        Assert.True(taranan >= 200, $"tarama olu: yalniz {taranan} dosya");
        Assert.True(bulgular.Count == 0, "eski yola canli basvuru:\n" + string.Join("\n", bulgular));
        Assert.Empty(eskiTurler);
        Assert.Empty(naudio);
        Assert.Empty(yuklu);
        Assert.True(motorFabrikasi, "MainWindow karsilastirma panelini motor kaynagiyla kurmuyor");
        Assert.True(sesAlani, "PreviewAudio motor tutmuyor");
    }
}
