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
        Komutlar(sb);
        CiktiDenetimi(sb, isKok);
        Sorulan(sb);
        Kaynaklar(sb, haritalar);
        var k2 = K1K2(sb, k1, haritalar, kollar);
        K3(sb);
        K3Denetim(sb, isKok);
        var k4 = K4(sb, isKok);
        K4b(sb, isKok, kollar, k1);
        var k5 = K5K6(sb, isKok, json, kollar);
        var k7 = K7(sb, isKok, json, kollar, k5);
        KapiDenemesi(sb, isKok);
        Sonuc(sb, k2, k5, k7, k4);
        Sinirlar(sb, isKok);

        Directory.CreateDirectory(Path.GetDirectoryName(ciktiYolu)!);
        File.WriteAllText(ciktiYolu, sb.ToString());
        Console.WriteLine($"rapor yazildi: {ciktiYolu}");
    }

    private static void Sinirlar(StringBuilder sb, string isKok)
    {
        sb.AppendLine("## Bu sayfanin bilinen sinirlari");
        sb.AppendLine();
        sb.AppendLine("1. **Uc pencere tek kaynaktan.** Icerik rejimi uc ayri (kesik cok /");
        sb.AppendLine("   durgun / kesintisiz hareket) ama kamera, kodlama gecmisi ve gren");
        sb.AppendLine("   ayni. Kaynaklar arasi genelleme bu sayfadan cikmaz.");
        sb.AppendLine("2. **`p3-hareketli` iki sahneli, `p2-durgun` alti.** Iki sahnede sira");
        sb.AppendLine("   korelasyonu ve \"ters dusen orani\" anlamsizdir; tabloda isaretli.");
        sb.AppendLine("   Istatistik agirligi tasiyan tek pencere 28 sahneli `p1-karisik`.");
        sb.AppendLine("3. **Duzenek kapali dongu duzeltmesi kosmuyor.** `EncodeRunner`'in hedef");
        sb.AppendLine("   boyut duzeltme dongusu yok, tek iki gecis var; band uyeligi urunun");
        sb.AppendLine("   degil duzenegin ozelligi. K6'nin asil sorusu olan **asan** kosum");
        sb.AppendLine("   orani ayri yazildi.");
        sb.AppendLine("4. **Referans sahneleri `-ss` ile kesiliyor.** Kesim noktasi kare");
        sb.AppendLine("   sinirina yuvarlanabilir; hata butun sahnelerde ayni yonde ve paylar");
        sb.AppendLine("   normalize edildigi icin kucuk, ama sifir degil.");
        sb.AppendLine("5. **`libsvtav1` kolunda dagitim hic denenemedi.** K4 zone parametresini");
        sb.AppendLine("   sessizce yok saydigini gosterdi; uretimin varsayilan kodlayicisi bu.");
        sb.AppendLine("   Dagitim koda girse bile varsayilan yolda **etkisiz kalirdi**.");
        sb.AppendLine("6. **Referans calisma noktasi planinkiyle ayni degil.** Sabit");
        sb.AppendLine($"   `CRF {Program.ReferansCrf}` her kodlayicida farkli bir bit hizina");
        sb.AppendLine("   dusuyor; her K1 basliginda `referans/plan` orani yazili. Oran 1'den");
        sb.AppendLine("   uzaklastikca \"hak edilen\" dagitimi planin gercek calistigi hizdan");
        sb.AppendLine("   uzak bir noktada olculmus olur — sira genelde korunur ama paylar");
        sb.AppendLine("   hizla birlikte kayar. Kollar arasi hak-edilen kiyasi bu yuzden");
        sb.AppendLine("   yapilmaz; her kol kendi referansiyla karsilastirilir.");
        var araliklar = Program.Pencereler
            .Select(x => Path.Combine(isKok, $"dogrula-{x.Ad}.csv"))
            .Where(File.Exists)
            .SelectMany(File.ReadAllLines)
            .Select(l => l.Split(';'))
            .Where(c => c.Length >= 7 && c[1] == "dogru")
            .Select(c => (Pencere: c[0], Aralik: double.Parse(c[6], CultureInfo.InvariantCulture)))
            .ToList();
        if (araliklar.Count > 0)
        {
            var enGenis = araliklar.MaxBy(x => x.Aralik);
            var enDar = araliklar.MinBy(x => x.Aralik);
            sb.AppendLine("7. **Kuralin oynatabildigi aralik dar.** Zone carpani `1,0` etrafinda");
            sb.AppendLine($"   kaliyor: en genis pencere `{enGenis.Pencere}` {Kabuk.Inv(enGenis.Aralik, "0.000")},");
            sb.AppendLine($"   en dar `{enDar.Pencere}` {Kabuk.Inv(enDar.Aralik, "0.000")} (K3 eki tablosu).");
            sb.AppendLine("   Kazancin ust siniri bu araliktan gelir; `gamma`yi buyutmek araligi");
            sb.AppendLine("   acardi ama `gamma = 1 - qcomp` turetilmis bir sayidir, telafi sabitine");
            sb.AppendLine("   cevrilmedi.");
        }
        sb.AppendLine();
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
        var (_, dal) = Kabuk.Yakala("git", new[] { "rev-parse", "--abbrev-ref", "HEAD" });
        var (_, srcSha) = Kabuk.Yakala("git", new[] { "log", "-1", "--format=%H %s", "--", "src" });
        sb.AppendLine($"- Dal `{dal.Trim()}`. Olculen **uretim kodu**: `src/`in son commit'i");
        sb.AppendLine($"  `{srcSha.Trim()}`. Butun kodlamalar bu koddan `--no-incremental`");
        sb.AppendLine("  derlenmis ikiliyle kosuldu; `--no-build` kullanilmadi. Bu commit'ten");
        sb.AppendLine("  sonraki degisiklikler yalniz `tools/` ve `docs/` icindedir, plani");
        sb.AppendLine("  etkilemez.");
        sb.AppendLine($"- Ham cikti: `{isKok.Replace('\\', '/')}` (gitignore'lu).");
        sb.AppendLine();
    }

    private static void CiktiDenetimi(StringBuilder sb, string isKok)
    {
        var y = Path.Combine(isKok, "cikti-denetimi.csv");
        if (!File.Exists(y)) return;
        var satirlar = File.ReadAllLines(y).Skip(1).Select(x => x.Split(';')).Where(c => c.Length >= 5).ToList();
        if (satirlar.Count == 0) return;

        var referans = satirlar.Count(c => c[0] == "referans");
        var cikti = satirlar.Count(c => c[0] == "cikti");
        var sapan = satirlar.Count(c => c[4] != "tam");

        sb.AppendLine("## Olculen dosyalarin tamligi");
        sb.AppendLine();
        sb.AppendLine("Bu sayfadaki bitler dosya uzunluklarindan geliyor; yarim kalmis bir");
        sb.AppendLine("kodlama sessizce kucuk bir \"hak edilen\" ya da \"verilen\" uretir. Kodlamalar");
        sb.AppendLine("`<ad>.yarim.mkv`e yazilip basarida yerine tasinir; ayrica her dosyanin");
        sb.AppendLine("suresi `ffprobe` ile olculup beklenen sahne/pencere suresiyle");
        sb.AppendLine("karsilastirilir (esik 0,5 sn).");
        sb.AppendLine();
        sb.AppendLine($"Denetlenen dosya **{satirlar.Count}** — referans sahnesi {referans}, ");
        sb.AppendLine($"kodlama ciktisi {cikti}. Suresi sapan: **{sapan}**.");
        sb.AppendLine();
        if (sapan > 0)
        {
            sb.AppendLine("| Tur | Dosya | Beklenen (sn) | Olculen (sn) |");
            sb.AppendLine("|-----|-------|---------------|--------------|");
            foreach (var c in satirlar.Where(c => c[4] != "tam"))
                sb.AppendLine($"| {c[0]} | `{c[1]}` | {c[2]} | {(c[3].Length == 0 ? "**okunamadi**" : c[3])} |");
            sb.AppendLine();
        }
        sb.AppendLine("Uretim: `bash tools/sahne-butcesi/05-cikti-denetimi.sh`, ham dosya");
        sb.AppendLine("`cikti-denetimi.csv`. Olcum bittikten sonra kosar.");
        sb.AppendLine();
    }

    private static void Komutlar(StringBuilder sb)
    {
        sb.AppendLine("## Hangi sayi hangi komuttan cikti");
        sb.AppendLine();
        sb.AppendLine("Butun olcum tek komutla bastan kosar: `bash tools/sahne-butcesi/01-olcumu-kos.sh`.");
        sb.AppendLine("Ikili her seferinde `--no-incremental` derlenir; `--no-build` kullanilmaz.");
        sb.AppendLine();
        sb.AppendLine("| Bolum | Ureten komut | Ham dosya |");
        sb.AppendLine("|-------|--------------|-----------|");
        sb.AppendLine("| Kaynaklar | `00-pencereleri-kes.sh`, sonra `SahneButcesi harita maks <pencere>` | `harita-<pencere>.json` |");
        sb.AppendLine("| K1, K2 | `SahneButcesi k1 <kol> <pencere>` | `k1-<kol>-<pencere>.json` / `.csv` |");
        sb.AppendLine("| K3 | olcum degil; kural `tools/sahne-butcesi/Butce.cs` | — |");
        sb.AppendLine("| K3 eki (denetim) | `SahneButcesi dogrula <kol>` | `dogrula-<pencere>.csv` |");
        sb.AppendLine("| K3 eki (mutasyon) | `bash tools/sahne-butcesi/03-duzenek-mutasyonu.sh` | `duzenek-mutasyon.csv` |");
        sb.AppendLine("| K4 | `SahneButcesi k4 maks p1-karisik` | `k4-izgara.csv` |");
        sb.AppendLine("| K4 eki | `SahneButcesi k4b <kol> <pencere>` | `k4b-<kol>-<pencere>.csv` |");
        sb.AppendLine("| K5, K6 | `SahneButcesi k5 <kol> <pencere>` | `k5-<kol>-<pencere>.json`, `.zones.txt` |");
        sb.AppendLine("| K7 | `SahneButcesi k7 <kol> <pencere>` | `k7-<kol>-<pencere>.json`, `.zones.txt` |");
        sb.AppendLine("| Karar kodu denemesi | `bash tools/sahne-butcesi/04-kapi-denemesi.sh` | `kapi-denemesi.csv` |");
        sb.AppendLine("| Dosya tamligi | `bash tools/sahne-butcesi/05-cikti-denetimi.sh` | `cikti-denetimi.csv` |");
        sb.AppendLine("| bu sayfa | `SahneButcesi rapor` | — |");
        sb.AppendLine();
        sb.AppendLine($"Kollar: {string.Join(", ", Program.Kollar.Keys.Select(k => $"`{k}`"))}. " +
                      $"Pencereler: {string.Join(", ", Program.Pencereler.Select(p => $"`{p.Ad}`"))}.");
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
                              $"plan ciktisi {Kabuk.Inv(k.PlanToplamBit / 1e6, "0.0")} Mbit " +
                              $"(referans/plan = {Kabuk.Inv(k.PlanToplamBit == 0 ? 0 : (double)k.ReferansToplamBit / k.PlanToplamBit, "0.00")}x).");
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

    private static void K3Denetim(StringBuilder sb, string isKok)
    {
        var dosyalar = Program.Pencereler
            .Select(p => Path.Combine(isKok, $"dogrula-{p.Ad}.csv"))
            .Where(File.Exists).ToArray();
        var mut = Path.Combine(isKok, "duzenek-mutasyon.csv");
        if (dosyalar.Length == 0 && !File.Exists(mut)) return;

        sb.AppendLine("### K3 eki — kural duzenegin icinde denetleniyor");
        sb.AppendLine();
        sb.AppendLine("Dagitim koda girmeyebilir, ama K5'in A/B'sini ureten sey bu kuraldir:");
        sb.AppendLine("kuralda sessiz bir hata olsaydi K5 zayif bir kazanc olcer ve biz onu");
        sb.AppendLine("\"dagitim ise yaramiyor\" diye okurduk. Bu yuzden kural duzenek icinde");
        sb.AppendLine("denetleniyor: `SahneButcesi dogrula <kol> [pencere]`.");
        sb.AppendLine();

        if (dosyalar.Length > 0)
        {
            sb.AppendLine("Denetlenen sartlar: carpan sayisi sahne sayisina esit, her carpan");
            sb.AppendLine($"`[{Kabuk.Inv(Butce.ZoneFloor, "0.00")}, {Kabuk.Inv(Butce.ZoneCeiling, "0.00")}]`");
            sb.AppendLine("kiskaci icinde, sure agirlikli ortalama `1,0` (kiskac baglamadikca),");
            sb.AppendLine("karmasiklik sirasi carpan sirasiyla ayni, zone kare araliklari artan");
            sb.AppendLine("ve cakismasiz. Uc harita da denetlenir: dogru, eksik kesim, fazla kesim.");
            sb.AppendLine();
            sb.AppendLine("| Pencere | Harita | Sahne | Zone | En kucuk b | En buyuk b | Aralik |");
            sb.AppendLine("|---------|--------|-------|------|------------|------------|--------|");
            var gecen = 0;
            foreach (var d in dosyalar)
                foreach (var satir in File.ReadAllLines(d))
                {
                    if (satir.StartsWith("# sonuc", StringComparison.Ordinal))
                    { if (satir.Contains(";gecti;", StringComparison.Ordinal)) gecen++; continue; }
                    var c = satir.Split(';');
                    if (c.Length < 7 || c[0] == "pencere") continue;
                    sb.AppendLine($"| `{c[0]}` | {c[1]} | {c[2]} | {c[3]} | {c[4]} | {c[5]} | {c[6]} |");
                }
            sb.AppendLine();
            sb.AppendLine($"Denetlenen pencere {dosyalar.Length}, denetimden gecen {gecen}.");
            sb.AppendLine();
            sb.AppendLine("**Aralik sutunu kazancin ust sinirini soyluyor.** `1,0` \"kodlayicinin");
            sb.AppendLine("verecegi kadar ver\" demektir; aralik daraldikca dagitim kodlayicinin");
            sb.AppendLine("kararindan uzaklasamaz. `p3-hareketli`'de aralik sifira yakin: o");
            sb.AppendLine("pencerede dagitim taban kosumuyla neredeyse ayni dosyayi uretir ve");
            sb.AppendLine("kazanc olcusu oradan gelemez. Bu bir olcum kusuru degil, kuralin");
            sb.AppendLine("kesintisiz hareket iceren kaynakta soyleyecek sozu olmamasidir.");
            sb.AppendLine();
        }

        if (File.Exists(mut))
        {
            var satirlar = File.ReadAllLines(mut).Where(x => !x.StartsWith("mutasyon;", StringComparison.Ordinal))
                .Select(x => x.Split(';')).Where(c => c.Length >= 3).ToList();
            sb.AppendLine("Denetimin kendisi de olculdu: kural bilerek bozuldu ve denetimin");
            sb.AppendLine("kirildigi goruldu (`bash tools/sahne-butcesi/03-duzenek-mutasyonu.sh`).");
            sb.AppendLine();
            sb.AppendLine("| Mutasyon | Ne degisti | Denetim |");
            sb.AppendLine("|----------|------------|---------|");
            foreach (var c in satirlar)
                sb.AppendLine($"| {c[0]} | {c[1]} | {(c[2] == "kirildi" ? "**kirildi**" : c[2])} |");
            sb.AppendLine();
            var bozuk = satirlar.Where(c => c[0] != "M0").ToList();
            var kirilan = bozuk.Count(c => c[2] == "kirildi");
            var temiz = satirlar.FirstOrDefault(c => c[0] == "M0")?[2] ?? "yok";
            sb.AppendLine($"Bozucu mutasyon {bozuk.Count}, denetimi kiran {kirilan}; temiz agac: {temiz}.");
            sb.AppendLine();
        }
    }

    public sealed record K4Sonuc(int Denenen, int Calisan, bool VarsayilanIsliyor,
        IReadOnlyList<string> CalisanListesi);

    private static K4Sonuc K4(StringBuilder sb, string isKok)
    {
        sb.AppendLine("## K4 — aday x kodlayici izgarasi");
        sb.AppendLine();
        var yol = Path.Combine(isKok, "k4-izgara.csv");
        if (!File.Exists(yol)) { sb.AppendLine("**bilinmiyor** — izgara kosulmadi."); sb.AppendLine(); return new K4Sonuc(0, 0, false, Array.Empty<string>()); }

        sb.AppendLine("Cikis kodunun sifir olmasi destek sayilmaz: x264/x265 ve SVT-AV1 parametre");
        sb.AppendLine("ayristiricilari tanimadiklari anahtari uyariyla geciyor. Her hucre **iki");
        sb.AppendLine("farkli degerle** kodlandi; once ayni parametreyle iki kosum yapilip tekrar");
        sb.AppendLine("gurultusu olculdu. Fark gurultunun iki katini ve ciktinin %1'ini asmadikca");
        sb.AppendLine("destek yazilmaz.");
        sb.AppendLine();
        var satirlar = File.ReadAllLines(yol);
        sb.AppendLine("| Kodlayici | Aday | Destek | A (bayt) | B (bayt) | Fark | Gurultu | Not |");
        sb.AppendLine("|-----------|------|--------|----------|----------|------|---------|-----|");
        var hucreler = new List<string[]>();
        foreach (var line in satirlar.Skip(1))
        {
            var c = line.Split(';');
            if (c.Length < 8) continue;
            hucreler.Add(c);
            sb.AppendLine($"| `{c[0]}` | {c[1]} | {c[2]} | {c[3]} | {c[4]} | {c[5]} | {c[6]} | {c[7]} |");
        }
        sb.AppendLine();

        var calisanKodlayici = new List<string>();
        var varsayilanIsliyor = true;
        var denenenSayi = 0;
        var zon = hucreler.Where(c => c[1] == "zones").ToList();
        if (zon.Count > 0)
        {
            var calisan = zon.Where(c => c[2] == "evet").Select(c => c[0]).ToList();
            calisanKodlayici = calisan;
            denenenSayi = zon.Count;
            var calismayan = zon.Where(c => c[2] != "evet").Select(c => c[0]).ToList();
            var listeyi = (IEnumerable<string> x) => string.Join(", ", x.Select(k => $"`{k}`"));
            sb.AppendLine($"**Tabloda `zones` denenen {zon.Count} kodlayicinin {calisan.Count} tanesinde");
            sb.AppendLine($"parametre isliyor:** {listeyi(calisan)}. Islemeyen {calismayan.Count}:");
            sb.AppendLine($"{listeyi(calismayan)}.");
            sb.AppendLine();
            var varsayilan = hucreler.Any(c => c[0] == "libsvtav1" && c[1] == "zones" && c[2] != "evet");
            varsayilanIsliyor = !varsayilan;
            if (varsayilan)
            {
                sb.AppendLine("Uretimin varsayilan kolu (`maks` -> `libsvtav1`) islemeyen listede.");
                sb.AppendLine("Dagitim koda girse bile varsayilan yolda **etkisiz kalir**; kazanc");
                sb.AppendLine("yalniz `libx264` ve `libx265` yollarinda mumkun.");
                sb.AppendLine();
            }
        }

        return new K4Sonuc(denenenSayi, calisanKodlayici.Count, varsayilanIsliyor, calisanKodlayici);
    }

    public sealed record Satir(string Arm, OlcumKaydi K);

    public sealed record AbSonuc(bool Gecti, IReadOnlyList<Satir> Kayitlar, string Ozet);

    private static void K4b(StringBuilder sb, string isKok, string[] kollar,
        Dictionary<(string Kol, string Pencere), K1Kaydi> k1)
    {
        var satirlar = new List<(string Kol, string Pencere, string Aday, string Param, double? Mae, string Not)>();
        foreach (var kol in kollar)
            foreach (var p in Program.Pencereler)
            {
                var y = Path.Combine(isKok, $"k4b-{kol}-{p.Ad}.csv");
                if (!File.Exists(y)) continue;
                foreach (var l in File.ReadAllLines(y).Skip(1))
                {
                    var c = l.Split(';');
                    if (c.Length < 4) continue;
                    double? mae = double.TryParse(c[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
                    satirlar.Add((kol, p.Ad, c[0], c[1], mae, c[3]));
                }
            }
        if (satirlar.Count == 0) return;

        sb.AppendLine("### K4 eki — iki aday yan yana, K1 farkini hangisi kapatiyor");
        sb.AppendLine();
        sb.AppendLine("K4'un izgarasi \"parametre isliyor mu\" sorusunu yanitlar; kabul kriteri");
        sb.AppendLine("ayrica **hangi adayin K1 farkini daha cok kapattigini** sorar. Olcu K1'in");
        sb.AppendLine("kendi olcusudur: `MAE(verilen, hak edilen)`, yuzde puani. Uc kosum ayni");
        sb.AppendLine("plan ve ayni hedef boyutla yapilir, degisen tek sey parametredir.");
        sb.AppendLine();
        sb.AppendLine("- `taban` — bugunku plan, ek parametre yok (K1'in `verilen` sutunu).");
        sb.AppendLine("- `zones` — sahne araligina `b` carpani; carpanlar haritadan.");
        sb.AppendLine("- `qcomp` — iki gecis yanliligi. Kodlayici biti `karmasiklik^qcomp` ile");
        sb.AppendLine("  dagitir, harita `karmasiklik^1` onerir; ikisini esitleyen deger");
        sb.AppendLine("  `qcomp = 1,0`'dir. Telafi sabiti degil, haritanin onerisinin ayni");
        sb.AppendLine("  denklemdeki karsiligi.");
        sb.AppendLine();
        sb.AppendLine("| Yazilim kolu | Pencere | Aday | Parametre | MAE (pp) | Tabana gore |");
        sb.AppendLine("|--------------|---------|------|-----------|----------|-------------|");
        var kazanan = new Dictionary<string, int>();
        var hucre = 0;
        foreach (var g in satirlar.GroupBy(x => (x.Kol, x.Pencere)))
        {
            var taban = g.FirstOrDefault(x => x.Aday == "taban").Mae;
            var olculen = g.Where(x => x.Aday != "taban" && x.Mae is not null).ToList();
            foreach (var x in g)
            {
                var fark = x.Mae is null || taban is null || x.Aday == "taban"
                    ? "—"
                    : Kabuk.Inv(x.Mae.Value - taban.Value, "+0.000;-0.000;0.000");
                sb.AppendLine($"| {x.Kol} | `{x.Pencere}` | {x.Aday} | `{x.Param}` | " +
                              $"{(x.Mae is null ? $"**bilinmiyor**: {x.Not}" : Kabuk.Inv(x.Mae.Value, "0.000"))} | {fark} |");
            }
            if (olculen.Count > 0)
            {
                hucre++;
                var en = olculen.MinBy(x => x.Mae!.Value);
                kazanan[en.Aday] = kazanan.GetValueOrDefault(en.Aday) + 1;
            }
        }
        sb.AppendLine();
        if (hucre > 0)
        {
            var sirali = kazanan.OrderByDescending(x => x.Value).ToList();
            sb.AppendLine($"Iki adayin da olculdugu hucre {hucre}; hucre basina dusuk MAE'yi veren aday: " +
                          string.Join(", ", sirali.Select(x => $"`{x.Key}` {x.Value}")) + ".");
            sb.AppendLine();
        }

        sb.AppendLine("Hangi adayin kazandigi tek basina bir sey soylemez: kazanc, kapatilmasi");
        sb.AppendLine("istenen K1 acigi ile yan yana konmadan okunamaz. Acik, ayni hucrede");
        sb.AppendLine("`MAE(verilen) - MAE(harita)`; kazanc, `MAE(taban) - MAE(en iyi aday)`.");
        sb.AppendLine();
        sb.AppendLine("| Yazilim kolu | Pencere | K1 acigi (pp) | En iyi aday | Kazanc (pp) | Acigin kapanan orani |");
        sb.AppendLine("|--------------|---------|---------------|-------------|-------------|----------------------|");
        var kapanmaVar = 0;
        var kapanmaHucre = 0;
        double? enBuyukKazanc = null;
        foreach (var g in satirlar.GroupBy(x => (x.Kol, x.Pencere)))
        {
            var taban = g.FirstOrDefault(x => x.Aday == "taban").Mae;
            var olculen = g.Where(x => x.Aday != "taban" && x.Mae is not null).ToList();
            if (taban is null || olculen.Count == 0) continue;
            if (!k1.TryGetValue((g.Key.Kol, g.Key.Pencere), out var k) || k.ReferansToplamBit == 0) continue;
            var maeV = Butce.MeanAbsoluteError(k.Verilen, k.HakEdilen) * 100;
            var maeH = Butce.MeanAbsoluteError(k.Harita, k.HakEdilen) * 100;
            var acik = maeV - maeH;
            var en = olculen.MinBy(x => x.Mae!.Value);
            var kazanc = taban.Value - en.Mae!.Value;
            kapanmaHucre++;
            if (kazanc > 0) kapanmaVar++;
            if (enBuyukKazanc is null || kazanc > enBuyukKazanc) enBuyukKazanc = kazanc;
            var oran = acik > 0 ? Kabuk.Inv(kazanc / acik * 100, "0.0") + "%" : "acik yok";
            sb.AppendLine($"| {g.Key.Kol} | `{g.Key.Pencere}` | {Kabuk.Inv(acik, "+0.000;-0.000;0.000")} | " +
                          $"{en.Aday} | {Kabuk.Inv(kazanc, "+0.000;-0.000;0.000")} | {oran} |");
        }
        sb.AppendLine();
        if (kapanmaHucre > 0)
        {
            sb.AppendLine($"Olculen {kapanmaHucre} hucrenin {kapanmaVar} tanesinde en iyi aday tabani");
            sb.AppendLine($"gecti; gorulen en buyuk kazanc {Kabuk.Inv(enBuyukKazanc!.Value, "0.000")} pp.");
            sb.AppendLine("Bu sutunlar kazancin buyuklugunu soyler, isaretini degil: kucuk ama");
            sb.AppendLine("pozitif bir fark da olcum gurultusu icinde kalabilir. K5'in kalite");
            sb.AppendLine("kapisi bu sayfada karari veren yerdir, bu tablo degil.");
            sb.AppendLine();
        }
    }

    private static AbSonuc K5K6(StringBuilder sb, string isKok, JsonSerializerOptions json, string[] kollar)
    {
        sb.AppendLine("## K5 ve K6 — kalite kazanci ve hedef boyut");
        sb.AppendLine();
        sb.AppendLine("Kapi (olcumden once): p10 kazanci `>= +0,50`, en kotu sahne kazanci");
        sb.AppendLine("`>= +1,00`, ayni iki kaynakta; hicbir kaynakta p10 kaybi `> 0,30`;");
        sb.AppendLine("her kosum hedef bandin icinde ve asan kosum orani %0.");
        sb.AppendLine();
        sb.AppendLine("**Puanlar yalniz kol icinde karsilastirilabilir.** Plan cozunurlugu kola");
        sb.AppendLine("gore degisiyor (T107 sonrasi ayni pencerede libx264 `1458x820`,");
        sb.AppendLine("libx265 `1728x972`, libsvtav1 `1920x1080`); farkli cozunurlukten cikan");
        sb.AppendLine("VMAF puanlari yan yana konmaz. `taban` ile `dagitim` ayni kolda ayni");
        sb.AppendLine("cozunurluktedir — A/B icinde bu sorun yoktur.");
        sb.AppendLine();

        var hepsi = Oku(isKok, "k5", json, kollar);
        if (hepsi.Count == 0) { sb.AppendLine("**bilinmiyor** — K5 kosulmadi."); sb.AppendLine(); return new AbSonuc(false, hepsi, "olculmedi"); }

        Tablo(sb, hepsi);

        var p10Kazanan = 0;
        var enKotuKazanan = 0;
        var p10Kaybeden = 0;
        var cift = 0;
        var kolP10 = new Dictionary<string, int>();
        var kolEnKotu = new Dictionary<string, int>();
        var kolCift = new Dictionary<string, int>();
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
            kolCift[g.Key.Arm] = kolCift.GetValueOrDefault(g.Key.Arm) + 1;
            if (dp10 >= 0.50) { p10Kazanan++; kolP10[g.Key.Arm] = kolP10.GetValueOrDefault(g.Key.Arm) + 1; }
            if (dworst >= 1.00) { enKotuKazanan++; kolEnKotu[g.Key.Arm] = kolEnKotu.GetValueOrDefault(g.Key.Arm) + 1; }
            if (dp10 < -0.30) p10Kaybeden++;
            farklar.Add($"| {g.Key.Arm} | `{g.Key.Pencere}` | {Kabuk.Inv(dmean, "+0.000;-0.000;0.000")} | " +
                        $"{Kabuk.Inv(dp10, "+0.000;-0.000;0.000")} | {Kabuk.Inv(dworst, "+0.000;-0.000;0.000")} | " +
                        $"{Kabuk.Inv(dmb, "+0.00;-0.00;0.00")} |");
        }

        ZoneGenisligi(sb, isKok, "k5", kollar);

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

        var kaliteGecen = kolCift.Keys
            .Where(k => kolP10.GetValueOrDefault(k) >= 2 && kolEnKotu.GetValueOrDefault(k) >= 2)
            .ToList();
        var kalite = kaliteGecen.Count > 0 && p10Kaybeden == 0;
        var gecti = kalite && asan == 0 && altinda == 0;
        var ozet = $"olculen cift {cift}; p10 esigini (>= +0,50) gecen {p10Kazanan}, " +
                   $"en kotu sahne esigini (>= +1,00) gecen {enKotuKazanan}, " +
                   $"esikten fazla p10 kaybeden {p10Kaybeden}; " +
                   $"olculen {olculenKosum} kosumdan hedefi **asan** {asan}, bandin **altinda** kalan {altinda}";
        sb.AppendLine($"**K5/K6 kapisi {(gecti ? "gecti" : "gecmedi")}** — {ozet}.");
        sb.AppendLine();
        sb.AppendLine("Esik metni \"uc kaynagin en az ikisinde\" der; kaynak = pencere. Kollar");
        sb.AppendLine("esikten sonra eklendi, o yuzden sayim **kol icinde** yapilir: bir kolun");
        sb.AppendLine("kendi uc penceresinin en az ikisi esigi gecmelidir. Ayri kollardan birer");
        sb.AppendLine("pencere toplanip \"iki kaynak\" sayilmaz. Bu netlestirme commit `a965416`,");
        sb.AppendLine("ilk `k5-*.json` yazilmadan once: `git log -1 --format=%cI a965416` ile");
        sb.AppendLine("`.calisma/T114/k5-*.json` zaman damgalari karsilastirilabilir.");
        sb.AppendLine();
        sb.AppendLine("| Yazilim kolu | Olculen pencere | p10 esigini gecen | En kotu sahne esigini gecen | Kalite sarti (1-3) |");
        sb.AppendLine("|--------------|-----------------|-------------------|-----------------------------|--------------------|");
        foreach (var k in kolCift.Keys.OrderBy(x => x, StringComparer.Ordinal))
            sb.AppendLine($"| {k} | {kolCift[k]}/3 | {kolP10.GetValueOrDefault(k)} | {kolEnKotu.GetValueOrDefault(k)} | " +
                          $"{(kaliteGecen.Contains(k) ? "evet" : "**hayir**")} |");
        sb.AppendLine();
        sb.AppendLine($"Kalite sartlari (1-3) tek basina: {(kalite ? "**saglandi**" : "**saglanmadi**")} " +
                      $"({kaliteGecen.Count} kolda saglandi, esikten fazla p10 kaybeden {p10Kaybeden}). " +
                      $"K6 sarti (4) tek basina: {(asan == 0 && altinda == 0 ? "**saglandi**" : "**saglanmadi**")}.");
        sb.AppendLine();
        sb.AppendLine($"Hedefi asan kosum orani: {Kabuk.Inv(olculenKosum == 0 ? 0 : 100.0 * asan / olculenKosum, "0.0")}%");
        sb.AppendLine($"({asan}/{olculenKosum}). Bandin altinda kalma bu duzenegin ozelligidir:");
        sb.AppendLine("`EncodeRunner`'in kapali dongu duzeltmesi kosmuyor, tek iki gecis var.");
        sb.AppendLine("Iki kol da ayni duzenekten geciyor, bu yuzden band disiligi kollari");
        sb.AppendLine("**ayirt etmez**; K6'nin asil sorusu olan asan kosum orani ayri yazildi.");
        sb.AppendLine();
        return new AbSonuc(gecti, hepsi, ozet);
    }

    private static void ZoneGenisligi(StringBuilder sb, string isKok, string asama, string[] kollar)
    {
        var satirlar = new List<string>();
        foreach (var kol in kollar)
            foreach (var p in Program.Pencereler)
            {
                var y = Path.Combine(isKok, $"{asama}-{kol}-{p.Ad}-dagitim.zones.txt");
                if (!File.Exists(y)) continue;
                var metin = File.ReadAllText(y);
                var i = metin.IndexOf("zones=", StringComparison.Ordinal);
                if (i < 0) continue;
                var b = metin[(i + 6)..].Split('/')
                    .Select(x => x.Split("b=").Last().Trim())
                    .Select(x => double.TryParse(x, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : double.NaN)
                    .Where(double.IsFinite).ToArray();
                if (b.Length == 0) continue;
                satirlar.Add($"| {kol} | `{p.Ad}` | {b.Length} | {Kabuk.Inv(b.Min(), "0.000")} | " +
                             $"{Kabuk.Inv(b.Max(), "0.000")} | {Kabuk.Inv(b.Max() - b.Min(), "0.000")} |");
            }
        if (satirlar.Count == 0) return;

        sb.AppendLine("### Dagitimin gercekte ne kadar oynadigi");
        sb.AppendLine();
        sb.AppendLine("Zone carpani `1,0` demek \"bu sahneye kodlayicinin verecegi kadar ver\"");
        sb.AppendLine("demektir. Carpanlarin araligi dar kaldiginda dagitimin kaliteye");
        sb.AppendLine("yapabilecegi etki de dar kalir; asagidaki fark sutunu kazanc");
        sb.AppendLine("beklentisinin ust sinirini gosterir.");
        sb.AppendLine();
        sb.AppendLine("| Yazilim kolu | Pencere | Zone sayisi | En kucuk b | En buyuk b | Aralik |");
        sb.AppendLine("|--------------|---------|-------------|------------|------------|--------|");
        foreach (var x in satirlar) sb.AppendLine(x);
        sb.AppendLine();
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
        var kazanciAsan = 0;
        foreach (var g in hepsi.Where(x => x.K.Bilinmiyor is null).GroupBy(x => (x.Arm, x.K.Pencere)))
        {
            var dogru = k5.Kayitlar.FirstOrDefault(x =>
                x.Arm == g.Key.Arm && x.K.Pencere == g.Key.Pencere && x.K.Kol == "dagitim")?.K;
            var taban = k5.Kayitlar.FirstOrDefault(x =>
                x.Arm == g.Key.Arm && x.K.Pencere == g.Key.Pencere && x.K.Kol == "taban")?.K;
            if (dogru?.VmafP10 is null) continue;
            double? kazanc = taban?.VmafP10 is null ? null : dogru.VmafP10.Value - taban.VmafP10.Value;
            foreach (var (_, bozuk) in g)
            {
                if (bozuk.VmafP10 is null) continue;
                karsilastirilan++;
                var kayip = dogru.VmafP10.Value - bozuk.VmafP10.Value;
                enBuyukKayip = Math.Max(enBuyukKayip, kayip);
                var asiyor = kazanc is not null && kayip > kazanc.Value;
                if (asiyor) kazanciAsan++;
                satirlar.Add($"| {g.Key.Arm} | `{g.Key.Pencere}` | {bozuk.Kol} | " +
                             $"{Kabuk.Inv(kayip, "+0.000;-0.000;0.000")} | " +
                             $"{(kazanc is null ? "**bilinmiyor**" : Kabuk.Inv(kazanc.Value, "+0.000;-0.000;0.000"))} | " +
                             $"{(kazanc is null ? "**bilinmiyor**" : asiyor ? "**evet**" : "hayir")} |");
            }
        }
        sb.AppendLine("### Bozuk haritanin bedeli, K5 kazanciyla yan yana");
        sb.AppendLine();
        sb.AppendLine("Kapi (`ESIKLER.md`): bozuk haritayla olculen p10 **kaybi**, K5'te olculen");
        sb.AppendLine("p10 **kazancindan** buyukse dagitim koda girmez. Kayip = ayni kolda ayni");
        sb.AppendLine("pencerenin dogru haritali `dagitim` kosumu eksi bozuk kosum; kazanc = ayni");
        sb.AppendLine("hucrenin K5'teki `dagitim` eksi `taban` farki. Sabit bir kayip esigi yok;");
        sb.AppendLine("olcu kendi hucresinin kazancidir.");
        sb.AppendLine();
        sb.AppendLine("| Yazilim kolu | Pencere | Bozulma | p10 kaybi | ayni hucrenin K5 kazanci | kayip kazanci asiyor mu |");
        sb.AppendLine("|--------------|---------|---------|-----------|--------------------------|-------------------------|");
        foreach (var x in satirlar) sb.AppendLine(x);
        sb.AppendLine();
        var ozet = $"karsilastirilan {karsilastirilan} kosum, en buyuk p10 kaybi " +
                   $"{Kabuk.Inv(enBuyukKayip, "0.000")} puan, kaybi kendi hucresinin " +
                   $"K5 kazancini asan {kazanciAsan} kosum";
        sb.AppendLine($"**Bozuk haritanin bedeli**: {ozet}.");
        sb.AppendLine();
        return new AbSonuc(karsilastirilan > 0 && kazanciAsan == 0, hepsi, ozet);
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

    private static void KapiDenemesi(StringBuilder sb, string isKok)
    {
        var y = Path.Combine(isKok, "kapi-denemesi.csv");
        if (!File.Exists(y)) return;
        var satirlar = File.ReadAllLines(y).Skip(1).Where(x => x.Contains(';')).ToList();
        if (satirlar.Count == 0) return;

        sb.AppendLine("## Karari veren kodun kendisi olculdu");
        sb.AppendLine();
        sb.AppendLine("Asagidaki karar bir programdan cikiyor; o program hep \"gecti\" diyorsa");
        sb.AppendLine("sayfadaki butun sayilar bosa gider. Bu yuzden kapi kodu uydurma girdiyle");
        sb.AppendLine("kosuldu: once dort sartin da saglandigi bir girdi (karar **degismeli**),");
        sb.AppendLine("sonra her seferinde tek bir sarti bozan girdiler.");
        sb.AppendLine();
        sb.AppendLine("| Senaryo | Ne degisti | Beklenen karar | Cikan karar | Sonuc |");
        sb.AppendLine("|---------|------------|----------------|-------------|-------|");
        var gecen = 0;
        foreach (var l in satirlar)
        {
            var c = l.Split(';');
            if (c.Length < 5) continue;
            if (c[4] == "gecti") gecen++;
            sb.AppendLine($"| `{c[0]}` | {c[1]} | {c[2]} | {c[3]} | {(c[4] == "gecti" ? "gecti" : "**KIRIK**")} |");
        }
        sb.AppendLine();
        sb.AppendLine($"Denenen senaryo {satirlar.Count}, beklenen karari veren {gecen}. Uretim");
        sb.AppendLine("`SahneButcesi rapor` cagrisidir; girdi `tools/sahne-butcesi/kapi-fikstur.py`,");
        sb.AppendLine("kosum `bash tools/sahne-butcesi/04-kapi-denemesi.sh`. Bu tablodaki sayilar");
        sb.AppendLine("uydurma; olculen sey kapinin **ayirt edip etmedigi**.");
        sb.AppendLine();
    }

    private static void Sonuc(StringBuilder sb, K2Sonuc k2, AbSonuc k5, AbSonuc k7, K4Sonuc k4)
    {
        sb.AppendLine("## Sonuc");
        sb.AppendLine();
        var k5Olculdu = k5.Kayitlar.Count > 0;
        var k7Olculdu = k7.Kayitlar.Count > 0;
        var eksik = !k5Olculdu || !k7Olculdu;
        var girer = !k2.Kapandi && k5.Gecti && k7.Gecti;
        sb.AppendLine(eksik
            ? "**Karar verilemedi.** Asagidaki kapilardan en az biri olculemedi;"
              + " olculmemis kapi gecmemis sayilmaz, `bilinmiyor` kalir."
            : girer
                ? "**Dagitim koda girer.** K2 kapisi kapanmadi, K5/K6 kapisi gecti, bozuk"
                  + " harita bedeli kazancin altinda kaldi."
                : "**Dagitim koda girmez.** Kapilardan en az biri onu durdurdu:");
        sb.AppendLine();
        sb.AppendLine($"- K2 (kodlayici zaten dogru dagitiyor mu): {(k2.Kapandi ? "**kapandi** — kodlayici zaten en az bizim kadar iyi dagitiyor" : "kapanmadi")}");
        sb.AppendLine($"- K5/K6 (kalite kazanci ve hedef boyut): {(!k5Olculdu ? "**bilinmiyor**" : k5.Gecti ? "gecti" : "**gecmedi**")} — {k5.Ozet}");
        sb.AppendLine($"- K7 (bozuk harita bedeli): {(!k7Olculdu ? "**bilinmiyor**" : k7.Gecti ? "kabul edilebilir" : "**kabul edilemez**")} — {k7.Ozet}");
        sb.AppendLine();
        if (k4.Denenen > 0)
        {
            sb.AppendLine($"Karar hangi yollari kapsar: `zones` denenen {k4.Denenen} kodlayicinin");
            sb.AppendLine($"{k4.Calisan} tanesinde isliyor ({string.Join(", ", k4.CalisanListesi.Select(x => $"`{x}`"))}).");
            sb.AppendLine(k4.VarsayilanIsliyor
                ? "Uretimin varsayilan kodlayicisi bu listede; karar varsayilan yolu da kapsar."
                : "**Uretimin varsayilan kodlayicisi bu listede degil**; \"girer\" karari cikmis"
                  + " olsa bile dagitim varsayilan yolda etkisiz kalir, kazanc yalniz bu iki"
                  + " kodlayicinin secildigi kosumlarda gorulur.");
            sb.AppendLine();
        }
        sb.AppendLine("Kapilarin sayisal esikleri `tools/sahne-butcesi/ESIKLER.md` icinde ve");
        sb.AppendLine("bu olcumden onceki commit'te sabitlendi.");
        sb.AppendLine();
    }
}
