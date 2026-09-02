using System.Globalization;
using System.Text;
using System.Text.Json;
using VidShrink.Core;

namespace VidShrink.SahneButcesi;

public static class Rapor
{
    public static void Uret(string isKok, string ciktiYolu, JsonSerializerOptions json)
    {
        var sb = new StringBuilder();
        var kollar = Program.Kollar.Keys.ToArray();

        var k1 = new Dictionary<(string Kol, string Pencere), K1Kaydi>();
        var haritalar = new Dictionary<string, HaritaKaydi>();
        foreach (var p in Program.Pencereler)
        {
            var hy = Path.Combine(isKok, $"harita-{p.Ad}.json");
            if (File.Exists(hy)) haritalar[p.Ad] = JsonSerializer.Deserialize<HaritaKaydi>(File.ReadAllText(hy), json)!;
            foreach (var kol in kollar)
            {
                var y = Path.Combine(isKok, $"k1-{kol}-{p.Ad}.json");
                if (File.Exists(y)) k1[(kol, p.Ad)] = JsonSerializer.Deserialize<K1Kaydi>(File.ReadAllText(y), json)!;
            }
        }

        sb.AppendLine("# Sahne basina bit dagitimi — harita plana bagli degil (T114)");
        sb.AppendLine();
        sb.AppendLine("Bu sayfadaki her sayi `tools/sahne-butcesi/` altindaki duzenekten cikar ve");
        sb.AppendLine("bu sayfayi da o duzenek yazar (`SahneButcesi rapor`). Ozet cumleler elle");
        sb.AppendLine("yazilmaz, tablodan hesaplanir.");
        sb.AppendLine();
        sb.AppendLine("Karar kurallari olcumden **once** yazildi: `tools/sahne-butcesi/ESIKLER.md`,");
        sb.AppendLine("commit `eb9165c`. Sonradan secilen esik kanit degildir.");
        sb.AppendLine();

        Ortam(sb, isKok);
        Sorulan(sb);
        Kaynaklar(sb, haritalar);
        var k2 = K1K2(sb, k1, haritalar, kollar);
        K3(sb);
        K4(sb, isKok);
        var k5 = K5K6(sb, isKok, json, kollar);
        var k7 = K7(sb, isKok, json, kollar, k5);
        Sonuc(sb, k2, k5, k7);

        Directory.CreateDirectory(Path.GetDirectoryName(ciktiYolu)!);
        File.WriteAllText(ciktiYolu, sb.ToString());
        Console.WriteLine($"rapor yazildi: {ciktiYolu}");
    }

    private static void Ortam(StringBuilder sb, string isKok)
    {
        var (_, ver) = Kabuk.Yakala(VidShrink.Ffmpeg.ToolLocator.Ffmpeg, new[] { "-version" });
        var ilk = ver.Split('\n').FirstOrDefault()?.Trim() ?? "bilinmiyor";
        sb.AppendLine("## Olcum ortami");
        sb.AppendLine();
        sb.AppendLine($"- `{ilk}` — butun kosumlar tek surumle. Surum sinirini gecen kiyas yok.");
        sb.AppendLine($"- Is parcacigi sabit: `-threads {Program.Threads}`, x265 `pools={Program.Threads}`,");
        sb.AppendLine($"  x264 `threads={Program.Threads}`, SVT-AV1 `lp={Program.Threads}`.");
        sb.AppendLine("- **Makine paylasimliydi**; paralelde baska ajanlarin olcumleri kosuyordu.");
        sb.AppendLine("  Bu damga yalniz **sure** sayilarindadir. Bu sayfada sure sayisi yok:");
        sb.AppendLine("  bit, boyut ve kalite sayilari is parcacigi sabitken yukten etkilenmez.");
        var (_, sha) = Kabuk.Yakala("git", new[] { "rev-parse", "HEAD" });
        var (_, dal) = Kabuk.Yakala("git", new[] { "rev-parse", "--abbrev-ref", "HEAD" });
        sb.AppendLine($"- Olculen agac: `{dal.Trim()}` @ `{sha.Trim()}` — butun kodlamalar bu");
        sb.AppendLine("  commit'te derlenmis ikiliyle kosuldu (`--no-incremental`).");
        sb.AppendLine($"- Ham cikti: `{isKok.Replace('\\', '/')}` (gitignore'lu).");
        sb.AppendLine();
    }

    private static void Sorulan(StringBuilder sb)
    {
        sb.AppendLine("## Sorulan tek soru");
        sb.AppendLine();
        sb.AppendLine("Kodlayicinin kendi hiz denetimi sahne basina biti zaten dogru dagitiyor mu?");
        sb.AppendLine("Uc dagitim yan yana olculur:");
        sb.AppendLine();
        sb.AppendLine("| Ad | Nasil olculdu | Kimin karari |");
        sb.AppendLine("|----|---------------|--------------|");
        sb.AppendLine($"| hak edilen | her sahne **ayri ayri**, sabit `CRF {Program.ReferansCrf}` | referans |");
        sb.AppendLine("| verilen | pencere butun halinde bugunku planla (iki gecis, hedef boyut); paket boyutlari sahne araligina toplanir | kodlayici |");
        sb.AppendLine("| harita | `SceneMap.Scenes[i].Bits` — sonda ciktisi (x264 ultrafast crf23, 640 genislik) | bizim onerimiz |");
        sb.AppendLine();
        sb.AppendLine("Paylar penceredeki toplama normalize edilir; birim yuzde puani (pp).");
        sb.AppendLine();
    }

    private static void Kaynaklar(StringBuilder sb, Dictionary<string, HaritaKaydi> haritalar)
    {
        sb.AppendLine("## Kaynaklar");
        sb.AppendLine();
        sb.AppendLine("Uc pencere de `kaynak-1080p60-hdr-17dk-yalniz-video.mkv` icinden kesildi");
        sb.AppendLine("(1920x1080 hevc 10-bit HDR, 60 fps). Ses akisi yok: T63'te A/B'yi haksiz");
        sb.AppendLine("yapan ses farki bu olcume giremez. Pencere sinirlari T105'in yer gercegi");
        sb.AppendLine("pencereleridir; gercek kesim sayilari oradan gelir.");
        sb.AppendLine();
        sb.AppendLine("| Pencere | Icerik | Gercek kesim (T105) | Harita sahnesi | Sure (sn) |");
        sb.AppendLine("|---------|--------|---------------------|----------------|-----------|");
        foreach (var p in Program.Pencereler)
        {
            var h = haritalar.GetValueOrDefault(p.Ad);
            var gercek = p.Ad switch { "p1-karisik" => "28", "p2-durgun" => "7", _ => "0" };
            sb.AppendLine($"| `{p.Ad}` | {p.Not.Split('—').Last().Trim()} | {gercek} | " +
                          $"{(h is null ? "olculmedi" : h.Scenes.Count.ToString(CultureInfo.InvariantCulture))} | " +
                          $"{(h is null ? "olculmedi" : Kabuk.Inv(h.Duration, "0.0"))} |");
        }
        sb.AppendLine();
        sb.AppendLine("**Uc pencere de tek kaynaktan gelir.** Uc ayri film degil; icerik rejimi");
        sb.AppendLine("uc ayri olsa da kodlayici davranisi ayni kamera ve ayni kodlama gecmisi");
        sb.AppendLine("uzerinde olculmustur. Bu sayfanin en zayif yani budur.");
        sb.AppendLine();
    }

    public sealed record K2Sonuc(bool Kapandi, IReadOnlyList<string> Satirlar);

    private static K2Sonuc K1K2(
        StringBuilder sb,
        Dictionary<(string, string), K1Kaydi> k1,
        Dictionary<string, HaritaKaydi> haritalar,
        string[] kollar)
    {
        sb.AppendLine("## K1 — bugunku dagitimin hatasi");
        sb.AppendLine();

        foreach (var kol in kollar)
        {
            foreach (var p in Program.Pencereler)
            {
                if (!k1.TryGetValue((kol, p.Ad), out var k)) continue;
                sb.AppendLine($"### {kol} / {p.Ad}");
                if (k.ReferansToplamBit == 0)
                {
                    sb.AppendLine();
                    foreach (var b in k.Bilinmiyor) sb.AppendLine($"- **bilinmiyor**: {b}");
                    sb.AppendLine();
                    continue;
                }
                sb.AppendLine();
                sb.AppendLine($"Plan: `{k.Plan.Codec}` {k.Plan.Mode} {k.Plan.VideoBitrateK}k " +
                              $"{k.Plan.Width}x{k.Plan.Height}@{Kabuk.Inv(k.Plan.Fps, "0.##")} preset `{k.Plan.Preset}` " +
                              $"hedef {Kabuk.Inv(k.Plan.TargetMb, "0.#")} MB. " +
                              $"Referans toplami {Kabuk.Inv(k.ReferansToplamBit / 1e6, "0.0")} Mbit, " +
                              $"plan ciktisi {Kabuk.Inv(k.PlanToplamBit / 1e6, "0.0")} Mbit.");
                sb.AppendLine();
                var h = haritalar[p.Ad];
                sb.AppendLine("| Sahne | Bas (sn) | Sure (sn) | Karmasiklik | hak edilen (pp) | verilen (pp) | harita (pp) | verilen−hak | harita−hak |");
                sb.AppendLine("|-------|----------|-----------|-------------|-----------------|--------------|-------------|-------------|------------|");
                for (var i = 0; i < k.HakEdilen.Count; i++)
                {
                    var s = h.Scenes[i];
                    sb.AppendLine($"| {i} | {Kabuk.Inv(s.Start, "0.0")} | {Kabuk.Inv(s.End - s.Start, "0.0")} | " +
                                  $"{Kabuk.Inv(s.Complexity, "0.00")} | {Kabuk.Inv(k.HakEdilen[i] * 100, "0.00")} | " +
                                  $"{Kabuk.Inv(k.Verilen[i] * 100, "0.00")} | {Kabuk.Inv(k.Harita[i] * 100, "0.00")} | " +
                                  $"{Kabuk.Inv((k.Verilen[i] - k.HakEdilen[i]) * 100, "+0.00;-0.00;0.00")} | " +
                                  $"{Kabuk.Inv((k.Harita[i] - k.HakEdilen[i]) * 100, "+0.00;-0.00;0.00")} |");
                }
                sb.AppendLine();
                foreach (var b in k.Bilinmiyor) sb.AppendLine($"- **bilinmiyor**: {b}");
                if (k.Bilinmiyor.Count > 0) sb.AppendLine();
            }
        }

        sb.AppendLine("## K2 — kodlayicinin dagitimi bizim onerimizle yan yana");
        sb.AppendLine();
        sb.AppendLine("Kapi (`ESIKLER.md`, olcumden once): (1) `rho(verilen,hak) >= 0,80`,");
        sb.AppendLine("(2) `MAE(verilen) <= MAE(harita)`, (3) ters dusen sahne orani `< %20`.");
        sb.AppendLine("Ucu birden saglaniyorsa is biter ve kod degismez.");
        sb.AppendLine();
        sb.AppendLine("| Kol | Pencere | Sahne | rho(verilen,hak) | rho(harita,hak) | MAE verilen (pp) | MAE harita (pp) | Ters dusen | K1 kapi | K2 kapi | K3 kapi |");
        sb.AppendLine("|-----|---------|-------|------------------|-----------------|------------------|-----------------|------------|---------|---------|---------|");

        var satirlar = new List<string>();
        var bilinmeyen = new List<string>();
        var hepsiKapandi = true;
        var olculdu = false;
        var hucre = 0;
        var g1Gecen = 0;
        var g2Gecen = 0;
        var g3Gecen = 0;
        var ucuBirden = 0;
        foreach (var kol in kollar)
        {
            foreach (var p in Program.Pencereler)
            {
                if (!k1.TryGetValue((kol, p.Ad), out var k)) continue;
                if (k.ReferansToplamBit == 0)
                {
                    bilinmeyen.Add($"{kol}/{p.Ad}: {string.Join("; ", k.Bilinmiyor)}");
                    sb.AppendLine($"| {kol} | `{p.Ad}` | {k.Harita.Count} | bilinmiyor | bilinmiyor | bilinmiyor | bilinmiyor | bilinmiyor | — | — | — |");
                    continue;
                }
                olculdu = true;
                hucre++;
                var rhoV = SceneMap.Spearman(k.Verilen, k.HakEdilen);
                var rhoH = SceneMap.Spearman(k.Harita, k.HakEdilen);
                var maeV = Butce.MeanAbsoluteError(k.Verilen, k.HakEdilen) * 100;
                var maeH = Butce.MeanAbsoluteError(k.Harita, k.HakEdilen) * 100;
                var ters = Butce.TersDusenler(k.HakEdilen, k.Verilen, k.Harita);
                var oran = (double)ters / k.HakEdilen.Count;
                var g1 = rhoV >= 0.80;
                var g2 = maeV <= maeH;
                var g3 = oran < 0.20;
                if (!(g1 && g2 && g3)) hepsiKapandi = false;
                if (g1) g1Gecen++;
                if (g2) g2Gecen++;
                if (g3) g3Gecen++;
                if (g1 && g2 && g3) ucuBirden++;
                var rhoNot = k.HakEdilen.Count < 4 ? $" (n={k.HakEdilen.Count}, anlamsiz)" : string.Empty;
                satirlar.Add($"{kol}/{p.Ad}: rho(verilen)={Kabuk.Inv(rhoV, "0.000")} rho(harita)={Kabuk.Inv(rhoH, "0.000")} " +
                             $"MAE {Kabuk.Inv(maeV, "0.00")} vs {Kabuk.Inv(maeH, "0.00")} pp, ters {ters}/{k.HakEdilen.Count}");
                sb.AppendLine($"| {kol} | `{p.Ad}` | {k.HakEdilen.Count} | {Kabuk.Inv(rhoV, "0.000")}{rhoNot} | {Kabuk.Inv(rhoH, "0.000")} | " +
                              $"{Kabuk.Inv(maeV, "0.00")} | {Kabuk.Inv(maeH, "0.00")} | {ters}/{k.HakEdilen.Count} ({Kabuk.Inv(oran * 100, "0")}%){rhoNot} | " +
                              $"{Evet(g1)} | {Evet(g2)} | {Evet(g3)} |");
            }
        }
        sb.AppendLine();
        if (!olculdu) { sb.AppendLine("**bilinmiyor** — K1 ciktisi yok."); sb.AppendLine(); return new K2Sonuc(false, satirlar); }

        sb.AppendLine(hepsiKapandi
            ? $"**K2 kapisi kapandi.** Olculen {hucre} hucrenin hepsinde ucu birden saglandi."
            : $"**K2 kapisi kapanmadi.** Olculen {hucre} hucreden {ucuBirden} tanesinde ucu birden saglandi.");
        sb.AppendLine();
        sb.AppendLine($"- K1 kapisi (`rho(verilen,hak) >= 0,80`): {g1Gecen}/{hucre} hucre");
        sb.AppendLine($"- K2 kapisi (`MAE(verilen) <= MAE(harita)`): {g2Gecen}/{hucre} hucre");
        sb.AppendLine($"- K3 kapisi (ters dusen orani `< %20`): {g3Gecen}/{hucre} hucre");
        sb.AppendLine();
        if (bilinmeyen.Count > 0)
        {
            sb.AppendLine($"Olculemeyen {bilinmeyen.Count} hucre (varsayilana dusurulmedi, ayri satir):");
            foreach (var b in bilinmeyen) sb.AppendLine($"- **bilinmiyor** — {b}");
            sb.AppendLine();
        }
        sb.AppendLine("Sahne sayisi 4'un altindaki pencerede sira korelasyonu anlamsizdir ve o");
        sb.AppendLine("sutunda isaretlidir; o pencerede karari MAE tasir. Sahne sayisi icerigin");
        sb.AppendLine("kendisidir: kesimi olmayan pencerede dagitilacak sahne de yoktur.");
        sb.AppendLine();
        return new K2Sonuc(hepsiKapandi, satirlar);
    }

    private static void K3(StringBuilder sb)
    {
        sb.AppendLine("## K3 — kural `SceneMap`'in kendi sayilarindan cikiyor mu");
        sb.AppendLine();
        sb.AppendLine("Evet. Aday kural `Butce.ZoneCarpanlari` yalniz su ucunu okur:");
        sb.AppendLine();
        sb.AppendLine("| Girdi | Kaynak | Yeni sonda kosumu |");
        sb.AppendLine("|-------|--------|-------------------|");
        sb.AppendLine("| `Scene.Complexity` | `SceneMap.cs:13` — sonda ciktisi | yok |");
        sb.AppendLine("| `Scene.Bits` | `SceneMap.cs:12` — sonda ciktisi | yok |");
        sb.AppendLine("| sahne suresi | `Scene.Start` / `Scene.End` | yok |");
        sb.AppendLine();
        sb.AppendLine($"Kural: `b_i = clamp(Complexity_i^gamma, {Kabuk.Inv(Butce.ZoneFloor, "0.00")}, " +
                      $"{Kabuk.Inv(Butce.ZoneCeiling, "0.0")})`, sure agirlikli ortalamasi 1,0'a normalize.");
        sb.AppendLine($"`gamma = 1 - qcomp = {Kabuk.Inv(Butce.Gamma(Butce.DefaultQcomp), "0.00")}` " +
                      $"(x264/x265 varsayilan `qcomp = {Kabuk.Inv(Butce.DefaultQcomp, "0.00")}`).");
        sb.AppendLine();
        sb.AppendLine("`gamma` telafi sabiti degil: iki gecis hiz denetimi biti karmasikliga");
        sb.AppendLine("`qcomp` ussuyle dagitir, harita tam oranli dagitim onerir (us 1,0);");
        sb.AppendLine("us farki tam olarak `1 - qcomp`'tur. Normalizasyon K6'nin sartidir —");
        sb.AppendLine("carpanlar biti yeniden bolusturur, toplami degistirmez.");
        sb.AppendLine();
        sb.AppendLine("**T96'nin %10,4'luk sonda maliyeti artmaz**: kural mevcut haritanin");
        sb.AppendLine("ustunde calisir, yeni tarama acmaz.");
        sb.AppendLine();
    }

    private static string Evet(bool value) => value ? "evet" : "**hayir**";

    private static void K4(StringBuilder sb, string isKok)
    {
        sb.AppendLine("## K4 — aday x kodlayici izgarasi");
        sb.AppendLine();
        var yol = Path.Combine(isKok, "k4-izgara.csv");
        if (!File.Exists(yol)) { sb.AppendLine("**bilinmiyor** — izgara kosulmadi."); sb.AppendLine(); return; }

        sb.AppendLine("Cikis kodunun sifir olmasi destek sayilmaz: x264/x265 ve SVT-AV1 parametre");
        sb.AppendLine("ayristiricilari tanimadiklari anahtari uyariyla geciyor. Her hucre **iki");
        sb.AppendLine("farkli degerle** kodlandi; once ayni parametreyle iki kosum yapilip tekrar");
        sb.AppendLine("gurultusu olculdu. Fark gurultunun iki katini ve ciktinin %1'ini asmadikca");
        sb.AppendLine("destek yazilmaz.");
        sb.AppendLine();
        var satirlar = File.ReadAllLines(yol);
        sb.AppendLine("| Kodlayici | Aday | Destek | A (bayt) | B (bayt) | Fark | Gurultu | Not |");
        sb.AppendLine("|-----------|------|--------|----------|----------|------|---------|-----|");
        foreach (var line in satirlar.Skip(1))
        {
            var c = line.Split(';');
            if (c.Length < 8) continue;
            sb.AppendLine($"| `{c[0]}` | {c[1]} | {c[2]} | {c[3]} | {c[4]} | {c[5]} | {c[6]} | {c[7]} |");
        }
        sb.AppendLine();
    }

    public sealed record Satir(string Arm, OlcumKaydi K);

    public sealed record AbSonuc(bool Gecti, IReadOnlyList<Satir> Kayitlar, string Ozet);

    private static AbSonuc K5K6(StringBuilder sb, string isKok, JsonSerializerOptions json, string[] kollar)
    {
        sb.AppendLine("## K5 ve K6 — kalite kazanci ve hedef boyut");
        sb.AppendLine();
        sb.AppendLine("Kapi (olcumden once): p10 kazanci `>= +0,50`, en kotu sahne kazanci");
        sb.AppendLine("`>= +1,00`, ayni iki kaynakta; hicbir kaynakta p10 kaybi `> 0,30`;");
        sb.AppendLine("her kosum hedef bandin icinde.");
        sb.AppendLine();

        var hepsi = Oku(isKok, "k5", json, kollar);
        if (hepsi.Count == 0) { sb.AppendLine("**bilinmiyor** — K5 kosulmadi."); sb.AppendLine(); return new AbSonuc(false, hepsi, "olculmedi"); }

        Tablo(sb, hepsi);

        var p10Kazanan = 0;
        var enKotuKazanan = 0;
        var p10Kaybeden = 0;
        var cift = 0;
        var asan = hepsi.Count(x => x.K.Bilinmiyor is null && x.K.GerceklesenMb > x.K.BandUstMb);
        var altinda = hepsi.Count(x => x.K.Bilinmiyor is null && x.K.GerceklesenMb < x.K.BandAltMb);
        var olculenKosum = hepsi.Count(x => x.K.Bilinmiyor is null);
        var farklar = new List<string>();
        foreach (var g in hepsi.Where(x => x.K.Bilinmiyor is null).GroupBy(x => (x.Arm, x.K.Pencere)))
        {
            var taban = g.FirstOrDefault(x => x.K.Kol == "taban")?.K;
            var dagitim = g.FirstOrDefault(x => x.K.Kol == "dagitim")?.K;
            if (taban?.VmafP10 is null || dagitim?.VmafP10 is null) continue;
            cift++;
            var dp10 = dagitim.VmafP10.Value - taban.VmafP10.Value;
            var dworst = (dagitim.VmafWorstScene ?? double.NaN) - (taban.VmafWorstScene ?? double.NaN);
            var dmean = (dagitim.VmafMean ?? double.NaN) - (taban.VmafMean ?? double.NaN);
            var dmb = dagitim.GerceklesenMb - taban.GerceklesenMb;
            if (dp10 >= 0.50) p10Kazanan++;
            if (dworst >= 1.00) enKotuKazanan++;
            if (dp10 < -0.30) p10Kaybeden++;
            farklar.Add($"| {g.Key.Arm} | `{g.Key.Pencere}` | {Kabuk.Inv(dmean, "+0.000;-0.000;0.000")} | " +
                        $"{Kabuk.Inv(dp10, "+0.000;-0.000;0.000")} | {Kabuk.Inv(dworst, "+0.000;-0.000;0.000")} | " +
                        $"{Kabuk.Inv(dmb, "+0.00;-0.00;0.00")} |");
        }

        sb.AppendLine("### Dagitimli − dagitimsiz");
        sb.AppendLine();
        sb.AppendLine("Bir satir bir **yazilim kolu x pencere** ciftidir. Boyut farki sutunu");
        sb.AppendLine("A/B'nin adil olup olmadigini gosterir: iki kol ayni boyutta degilse");
        sb.AppendLine("kalite farki dagitimdan degil bit farkindan gelebilir.");
        sb.AppendLine();
        sb.AppendLine("| Yazilim kolu | Pencere | Δ ortalama | Δ p10 | Δ en kotu sahne | Δ boyut (MB) |");
        sb.AppendLine("|--------------|---------|------------|-------|-----------------|--------------|");
        foreach (var f in farklar) sb.AppendLine(f);
        sb.AppendLine();

        var gecti = p10Kazanan >= 2 && enKotuKazanan >= 2 && p10Kaybeden == 0 && asan == 0 && altinda == 0;
        var ozet = $"olculen cift {cift}; p10 esigini (>= +0,50) gecen {p10Kazanan}, " +
                   $"en kotu sahne esigini (>= +1,00) gecen {enKotuKazanan}, " +
                   $"esikten fazla p10 kaybeden {p10Kaybeden}; " +
                   $"olculen {olculenKosum} kosumdan hedefi **asan** {asan}, bandin **altinda** kalan {altinda}";
        sb.AppendLine($"**K5/K6 kapisi {(gecti ? "gecti" : "gecmedi")}** — {ozet}.");
        sb.AppendLine();
        sb.AppendLine($"Hedefi asan kosum orani: {Kabuk.Inv(olculenKosum == 0 ? 0 : 100.0 * asan / olculenKosum, "0.0")}%");
        sb.AppendLine($"({asan}/{olculenKosum}). Bandin altinda kalma bu duzenegin ozelligidir:");
        sb.AppendLine("`EncodeRunner`'in kapali dongu duzeltmesi kosmuyor, tek iki gecis var.");
        sb.AppendLine("Iki kol da ayni duzenekten geciyor, bu yuzden band disiligi kollari");
        sb.AppendLine("**ayirt etmez**; K6'nin asil sorusu olan asan kosum orani ayri yazildi.");
        sb.AppendLine();
        return new AbSonuc(gecti, hepsi, ozet);
    }

    private static AbSonuc K7(StringBuilder sb, string isKok, JsonSerializerOptions json, string[] kollar, AbSonuc k5)
    {
        sb.AppendLine("## K7 — harita yanlisken dagitimin bedeli");
        sb.AppendLine();
        var hepsi = Oku(isKok, "k7", json, kollar);
        if (hepsi.Count == 0) { sb.AppendLine("**bilinmiyor** — K7 kosulmadi."); sb.AppendLine(); return new AbSonuc(false, hepsi, "olculmedi"); }

        sb.AppendLine("Iki bozulma ayri olculdu: **eksik kesim** (her ikinci kesim atildi) ve");
        sb.AppendLine("**fazla kesim** (her sahne ortasindan ikiye bolundu). Anahtar kare karari");
        sb.AppendLine("her kolda dogru haritadan gelir; degisen tek sey bit dagitimidir.");
        sb.AppendLine();
        Tablo(sb, hepsi);

        var satirlar = new List<string>();
        var enBuyukKayip = 0.0;
        var karsilastirilan = 0;
        foreach (var g in hepsi.Where(x => x.K.Bilinmiyor is null).GroupBy(x => (x.Arm, x.K.Pencere)))
        {
            var dogru = k5.Kayitlar.FirstOrDefault(x =>
                x.Arm == g.Key.Arm && x.K.Pencere == g.Key.Pencere && x.K.Kol == "dagitim")?.K;
            if (dogru?.VmafP10 is null) continue;
            foreach (var (_, bozuk) in g)
            {
                if (bozuk.VmafP10 is null) continue;
                karsilastirilan++;
                var d = bozuk.VmafP10.Value - dogru.VmafP10.Value;
                enBuyukKayip = Math.Min(enBuyukKayip, d);
                satirlar.Add($"| {g.Key.Arm} | `{g.Key.Pencere}` | {bozuk.Kol} | " +
                             $"{Kabuk.Inv(d, "+0.000;-0.000;0.000")} |");
            }
        }
        sb.AppendLine("### Bozuk harita − dogru harita (p10)");
        sb.AppendLine();
        sb.AppendLine("Karsilastirma tabani, ayni yazilim kolunda ayni pencerenin **dogru");
        sb.AppendLine("haritayla** dagitimli kosumudur (K5'in `dagitim` kolu).");
        sb.AppendLine();
        sb.AppendLine("| Yazilim kolu | Pencere | Bozulma | Δ p10 |");
        sb.AppendLine("|--------------|---------|---------|-------|");
        foreach (var x in satirlar) sb.AppendLine(x);
        sb.AppendLine();
        var ozet = $"karsilastirilan {karsilastirilan} kosum, en buyuk p10 kaybi " +
                   $"{Kabuk.Inv(enBuyukKayip, "0.000")} puan";
        sb.AppendLine($"**Bozuk haritanin bedeli**: {ozet}.");
        sb.AppendLine();
        return new AbSonuc(enBuyukKayip >= -0.30, hepsi, ozet);
    }

    private static void Tablo(StringBuilder sb, IReadOnlyList<Satir> kayitlar)
    {
        sb.AppendLine("| Yazilim kolu | Pencere | Kol | Boyut (MB) | Band | Band icinde | VMAF-NEG ort. | p10 | en dusuk kare | en kotu sahne |");
        sb.AppendLine("|--------------|---------|-----|------------|------|-------------|---------------|-----|---------------|---------------|");
        foreach (var (arm, o) in kayitlar)
        {
            if (o.Bilinmiyor is not null && o.VmafMean is null)
            {
                sb.AppendLine($"| {arm} | `{o.Pencere}` | {o.Kol} | — | — | — | — | — | — | **bilinmiyor**: {o.Bilinmiyor} |");
                continue;
            }
            sb.AppendLine($"| {arm} | `{o.Pencere}` | {o.Kol} | {Kabuk.Inv(o.GerceklesenMb, "0.00")} | " +
                          $"{Kabuk.Inv(o.BandAltMb, "0.0")}–{Kabuk.Inv(o.BandUstMb, "0.0")} | {(o.BandIcinde ? "evet" : "**hayir**")} | " +
                          $"{Say(o.VmafMean)} | {Say(o.VmafP10)} | {Say(o.VmafMin)} | {Say(o.VmafWorstScene)} |");
        }
        sb.AppendLine();
    }

    private static string Say(double? v) => v is null ? "**bilinmiyor**" : Kabuk.Inv(v.Value, "0.000");

    private static List<Satir> Oku(string isKok, string asama, JsonSerializerOptions json, string[] kollar)
    {
        var list = new List<Satir>();
        foreach (var kol in kollar)
            foreach (var p in Program.Pencereler)
            {
                var y = Path.Combine(isKok, $"{asama}-{kol}-{p.Ad}.json");
                if (!File.Exists(y)) continue;
                foreach (var o in JsonSerializer.Deserialize<List<OlcumKaydi>>(File.ReadAllText(y), json)!)
                    list.Add(new Satir(kol, o));
            }
        return list;
    }

    private static void Sonuc(StringBuilder sb, K2Sonuc k2, AbSonuc k5, AbSonuc k7)
    {
        sb.AppendLine("## Sonuc");
        sb.AppendLine();
        var girer = !k2.Kapandi && k5.Gecti && k7.Gecti;
        sb.AppendLine(girer
            ? "**Dagitim koda girer.** K2 kapisi kapanmadi, K5/K6 kapisi gecti, bozuk"
            : "**Dagitim koda girmez.** Kapilardan en az biri onu durdurdu:");
        sb.AppendLine();
        sb.AppendLine($"- K2 (kodlayici zaten dogru dagitiyor mu): {(k2.Kapandi ? "**kapandi** — kodlayici zaten en az bizim kadar iyi dagitiyor" : "kapanmadi")}");
        sb.AppendLine($"- K5/K6 (kalite kazanci ve hedef boyut): {(k5.Gecti ? "gecti" : "**gecmedi**")} — {k5.Ozet}");
        sb.AppendLine($"- K7 (bozuk harita bedeli): {(k7.Gecti ? "kabul edilebilir" : "**kabul edilemez**")} — {k7.Ozet}");
        sb.AppendLine();
        sb.AppendLine("Kapilarin sayisal esikleri `tools/sahne-butcesi/ESIKLER.md` icinde ve");
        sb.AppendLine("bu olcumden onceki commit'te sabitlendi.");
        sb.AppendLine();
    }
}
