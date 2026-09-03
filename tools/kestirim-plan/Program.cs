using System.Text.Json;
using VidShrink.Core;
using VidShrink.Ffmpeg;

// T159: kestirim dogrulugu ile plan kalitesi arasindaki bag olculuyor. Bu arac gercek bir
// kaynagi problar, karmasiklik kestirimini bilinen carpanlarla bozar, her carpanla plani
// yeniden hesaplar, gercekten kodlar ve gercek VMAF-NEG olcer. src/ altina yazmaz; yalniz
// VidShrink.Core ve VidShrink.Ffmpeg'in genel (public) uclarini cagirir.

var carpanlar = new (string Etiket, double Deger)[]
{
    ("x0.50", 0.50), ("x0.70", 0.70), ("x0.85", 0.85), ("x1.00", 1.00),
    ("x1.20", 1.20), ("x1.50", 1.50), ("x2.00", 2.00)
};

var kaynakKlasoru = args.Length > 0 ? args[0] : Path.Combine("..", "..", ".calisma", "kaynak");
var ciktiKlasoru = args.Length > 1 ? args[1] : Path.Combine("..", "..", ".calisma", "T159", "olcum");
var hedefMb = args.Length > 2 ? double.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture) : 20.0;
var kaynakFiltre = args.Length > 3 && args[3].Length > 0 ? args[3].Split(',', StringSplitOptions.RemoveEmptyEntries) : null;
var carpanFiltre = args.Length > 4 && args[4].Length > 0 ? args[4].Split(',', StringSplitOptions.RemoveEmptyEntries) : null;
var planSadece = Environment.GetEnvironmentVariable("KESTIRIM_PLAN_SADECE") == "1";

Directory.CreateDirectory(ciktiKlasoru);
var geciciKlasor = Path.Combine(ciktiKlasoru, "gecici");
Directory.CreateDirectory(geciciKlasor);

// --- ffmpeg/ffprobe sureç sayacı: iki vekil (proxy) exe gerçek ikiliyi bulup çalıştırmadan
// önce bir sayaç dosyasını artırır. PATH'e kendi klasörlerini gerçek ffmpeg'den önce koyarak
// ToolLocator'ın (VidShrink.Ffmpeg) onları bulmasını sağlıyoruz. src/ dokunulmadı; sadece bu
// process'in ortam değişkenleri değişti.
var sayacFfmpeg = Path.Combine(ciktiKlasoru, "sayac-ffmpeg.txt");
var sayacFfprobe = Path.Combine(ciktiKlasoru, "sayac-ffprobe.txt");
File.WriteAllText(sayacFfmpeg, "0");
File.WriteAllText(sayacFfprobe, "0");
Environment.SetEnvironmentVariable("KESTIRIM_SAYAC_FFMPEG", sayacFfmpeg);
Environment.SetEnvironmentVariable("KESTIRIM_SAYAC_FFPROBE", sayacFfprobe);
var repoKok = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var vekilFfmpegDir = Path.GetFullPath(Path.Combine(repoKok, "tools", "kestirim-plan", "proxy", "ffmpeg", "bin", "Release", "net8.0"));
var vekilFfprobeDir = Path.GetFullPath(Path.Combine(repoKok, "tools", "kestirim-plan", "proxy", "ffprobe", "bin", "Release", "net8.0"));
if (Directory.Exists(vekilFfmpegDir) && Directory.Exists(vekilFfprobeDir))
{
    var mevcutPath = Environment.GetEnvironmentVariable("PATH") ?? "";
    Environment.SetEnvironmentVariable("PATH", vekilFfmpegDir + Path.PathSeparator + vekilFfprobeDir + Path.PathSeparator + mevcutPath);
    Console.WriteLine($"[sayac] vekiller PATH'e eklendi: {vekilFfmpegDir} ; {vekilFfprobeDir}");
}
else
{
    Console.WriteLine("[sayac] UYARI: vekil exe'ler bulunamadi, surec sayisi olculemeyecek. Once proxy/ffmpeg ve proxy/ffprobe'u Release derle.");
}

const int CalibrationRounds = 2; // MainWindow.axaml.cs:43 ile ayni deger, uretim davranisini kopyalar.

var ct = CancellationToken.None;
var sonuclar = new List<Hucre>();

var kaynaklar = Directory.EnumerateFiles(kaynakKlasoru, "*.mkv")
    .Where(f => Path.GetFileNameWithoutExtension(f).StartsWith("parca-", StringComparison.OrdinalIgnoreCase))
    .Where(f => kaynakFiltre is null || kaynakFiltre.Contains(Path.GetFileNameWithoutExtension(f), StringComparer.OrdinalIgnoreCase))
    .OrderBy(f => f)
    .ToList();

if (kaynaklar.Count == 0)
{
    Console.Error.WriteLine($"[hata] {kaynakKlasoru} altinda parca-*.mkv bulunamadi.");
    return 1;
}

foreach (var kaynakYolu in kaynaklar)
{
    var kaynakAdi = Path.GetFileNameWithoutExtension(kaynakYolu);
    Console.WriteLine($"=== {kaynakAdi} ({kaynakYolu}) ===");

    var info = await FfprobeClient.ProbeAsync(kaynakYolu, ct);
    Console.WriteLine($"  info: {info.Width}x{info.Height}@{info.Fps:0.##} {info.VideoCodec} {info.DurationSeconds:0.#}s hdr={info.IsHdr}");

    var sahneDenemesi = await EncodeRunner.TryBuildSceneMapAsync(info, ct: ct);
    Console.WriteLine($"  sahne haritasi: {(sahneDenemesi.Ok ? "uretildi" : $"dusus ({sahneDenemesi.Fallback})")}");

    var olcumAraci = sahneDenemesi.Map is null ? QualityMeasurement.Instance : new QualityMeasurement(sahneDenemesi.Map);
    var problamaSonucu = await ComplexityProbe.RunDetailedAsync(info, SpeedMode.Quality, measureQuality: true, olcumAraci, ct);
    var anchors = problamaSonucu.QualityMeasurements
        .Where(q => q is { Comparable: true, VmafNegMean: not null })
        .Select(q => q.VmafNegMean!.Value)
        .ToArray();
    var tabanProfil = anchors.Length > 0 ? problamaSonucu.Profile.WithProbeQuality(anchors) : problamaSonucu.Profile;
    Console.WriteLine($"  gercek kestirim: ReferenceBppf={tabanProfil.ReferenceBppf:0.######} Measured={tabanProfil.Measured} QualityMeasured={tabanProfil.QualityMeasured}");

    var kaynakSatirlari = new List<Hucre>();

    foreach (var (etiket, m) in carpanlar)
    {
        if (carpanFiltre is not null && !carpanFiltre.Contains(etiket, StringComparer.OrdinalIgnoreCase)) continue;

        Console.WriteLine($"  --- {kaynakAdi} {etiket} (carpan={m}) ---");

        // K1 ENJEKSIYON NOKTASI (tools/kestirim-plan/Program.cs, bu satir): plana giren
        // ReferenceBppf degeri burada carpanla bozuluyor; olcum yolu (bu dosya) ile karar
        // yolu (PlanCalculator/ComplexityProfile, src/ altinda, dokunulmuyor) burada ayrisiyor.
        var enjekteProfil = m == 1.0 ? tabanProfil : tabanProfil with { ReferenceBppf = tabanProfil.ReferenceBppf * m };

        var secenekler = new PlanOptions
        {
            TargetMb = hedefMb,
            Intent = Intent.Sharing,
            Codec = CodecPreference.Compatible,
            AllowResolutionDrop = true,
            AllowFpsDrop = true,
            HdrPolicy = HdrPolicy.Preserve,
            FillPolicy = FillPolicy.FillTarget,
            SpeedMode = SpeedMode.Quality
        };

        var ilkTaslak = PlanCalculator.BuildDetailed(info, secenekler, enjekteProfil, null).Plan;

        var draft = ilkTaslak;
        var profil = enjekteProfil;
        var sonProfil = enjekteProfil;
        var sonPlan = ilkTaslak;

        if (!planSadece)
        {
            for (var round = 0; round < CalibrationRounds; round++)
            {
                var kalibre = await CalibrationProbe.RunAsync(info, draft, profil, SpeedMode.Quality, ct, sahneDenemesi.Map);
                sonProfil = kalibre;
                sonPlan = draft;
                if (!kalibre.Calibrated) break;

                var yerlesmis = PlanCalculator.BuildDetailed(info, secenekler, kalibre, null).Plan;
                sonPlan = yerlesmis;
                var oran = info.Height <= 0 ? 1.0 : (double)yerlesmis.Height / info.Height;
                if (kalibre.AppliesTo(yerlesmis.Codec, oran, yerlesmis.Fps)) break;

                draft = yerlesmis;
                profil = kalibre.WithoutCalibration();
            }
        }

        double? cikisMb = null, vmafOrt = null, vmafHarm = null, vmafP10 = null, vmafMin = null;
        bool kodlamaBasarili = false; string? kodlamaHata = null; int kodlamaDeneme = 0;

        if (!planSadece)
        {
            var ciktiYolu = Path.Combine(geciciKlasor, $"{kaynakAdi}-{etiket}.mkv");
            try
            {
                var sonuc = await new EncodeRunner().RunAsync(info, sonPlan, ciktiYolu, hedefMb, null, ct, secenekler.FillPolicy, sonProfil, null, sahneDenemesi.Map);
                kodlamaBasarili = sonuc.Success;
                kodlamaHata = sonuc.Error;
                kodlamaDeneme = sonuc.Attempts;
                cikisMb = sonuc.OutputMb;

                if (sonuc.Success && File.Exists(sonuc.OutputPath))
                {
                    var kalite = info.IsHdr
                        ? await QualityMeter.MeasureTonemappedReferenceAsync(kaynakYolu, sonuc.OutputPath, ct)
                        : await QualityMeter.MeasureAsync(kaynakYolu, sonuc.OutputPath, sahneDenemesi.Map, ct);
                    vmafOrt = kalite.VmafNegMean;
                    vmafHarm = kalite.VmafNegHarmonic;
                    vmafP10 = kalite.VmafNegP10;
                    vmafMin = kalite.VmafNegMin;
                    Console.WriteLine($"    kodlandi: {sonuc.OutputMb:0.00} MB, VMAF-NEG ort={vmafOrt:0.###} p10={vmafP10:0.###} ({kalite.Message ?? "ok"})");
                }
                else
                {
                    Console.WriteLine($"    kodlama basarisiz: {sonuc.Error}");
                }
            }
            finally
            {
                try { if (File.Exists(ciktiYolu)) File.Delete(ciktiYolu); } catch { }
            }
        }
        else
        {
            Console.WriteLine($"    plan-sadece: {sonPlan.Width}x{sonPlan.Height}@{sonPlan.Fps:0.##} {sonPlan.Codec} {sonPlan.Mode} crf={sonPlan.Crf} vbr={sonPlan.VideoBitrateK}k");
        }

        var satir = new Hucre(
            kaynakAdi, etiket, m,
            tabanProfil.ReferenceBppf, enjekteProfil.ReferenceBppf,
            ilkTaslak.Width, ilkTaslak.Height, ilkTaslak.Fps, ilkTaslak.Codec, ilkTaslak.Mode, ilkTaslak.Crf, ilkTaslak.VideoBitrateK,
            sonPlan.Width, sonPlan.Height, sonPlan.Fps, sonPlan.Codec, sonPlan.Mode, sonPlan.Crf, sonPlan.VideoBitrateK,
            sonProfil.Calibrated,
            cikisMb, vmafOrt, vmafHarm, vmafP10, vmafMin,
            kodlamaBasarili, kodlamaHata, kodlamaDeneme);

        kaynakSatirlari.Add(satir);
        sonuclar.Add(satir);
    }

    var jsonYolu = Path.Combine(ciktiKlasoru, $"{kaynakAdi}.json");
    File.WriteAllText(jsonYolu, JsonSerializer.Serialize(kaynakSatirlari, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"  yazildi: {jsonYolu}");
}

var tumJsonYolu = Path.Combine(ciktiKlasoru, "tumu.json");
File.WriteAllText(tumJsonYolu, JsonSerializer.Serialize(sonuclar, new JsonSerializerOptions { WriteIndented = true }));

var sayimFfmpeg = long.TryParse(File.ReadAllText(sayacFfmpeg).Trim(), out var nf) ? nf : -1;
var sayimFfprobe = long.TryParse(File.ReadAllText(sayacFfprobe).Trim(), out var np) ? np : -1;
Console.WriteLine();
Console.WriteLine($"toplam hucre: {sonuclar.Count}");
Console.WriteLine($"dogurulan ffmpeg sureci: {sayimFfmpeg}");
Console.WriteLine($"dogurulan ffprobe sureci: {sayimFfprobe}");
Console.WriteLine($"tumu.json: {tumJsonYolu}");

return 0;

record Hucre(
    string Kaynak, string Carpan, double CarpanDeger,
    double TabanReferenceBppf, double EnjekteReferenceBppf,
    int TaslakWidth, int TaslakHeight, double TaslakFps, string TaslakCodec, string TaslakMode, int? TaslakCrf, int TaslakVideoBitrateK,
    int SonWidth, int SonHeight, double SonFps, string SonCodec, string SonMode, int? SonCrf, int SonVideoBitrateK,
    bool Calibrated,
    double? CikisMb, double? VmafNegOrt, double? VmafNegHarmonik, double? VmafNegP10, double? VmafNegMin,
    bool KodlamaBasarili, string? KodlamaHata, int KodlamaDeneme);
