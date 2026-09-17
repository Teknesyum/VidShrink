"""butceilk kolunun ham satirlarindan rapor tablosu uretir.

Girdi: hb.ps1'in yazdigi butceilk-<kesit>.json dosyalarini tasiyan klasor.
Her sayi ham satirdan gelir; burada hicbir deger elle yazilmaz.

Bant kademeleri motorun kendisinden: FillBand.For (PlanCalculator.cs) hedef
50 MB ve ustunde alt kenari 0,972*hedef ve sert tabani 0,944*hedef, 10 MB ve
ustunde 0,95 ile 0,90, altinda 0,92 ile 0,85 yapiyor; ust kenar her zaman
hedefin kendisi. BudgetFill.Floor 0,97 ve BudgetFill.Aim 0,985 (BudgetFill.cs).
Bu kademeler HbOlcumDuzenegiTests icinde motorun cikisina karsi pimli.
"""

import json
import re
import sys
from pathlib import Path

BANT_KADEME = ((50.0, 0.972, 0.944), (10.0, 0.95, 0.90), (0.0, 0.92, 0.85))
DOLDUR_ESIK = 0.97
DOLDUR_HEDEF = 0.985


def bant(hedef):
    for esik, alt, taban in BANT_KADEME:
        if hedef >= esik:
            return hedef * alt, hedef * taban
    raise ValueError(hedef)


SIRA = {"karanlik": 0, "parlak": 1, "hareketli": 2, "ekran": 3}
IZ = re.compile(r"^(\d+):(.+?):(\d+)k:([\d.]+)->([\d.]+)$")


def v(x, n=2):
    if x is None or x == "":
        return "—"
    if isinstance(x, bool):
        return "evet" if x else "hayir"
    if isinstance(x, (int, float)):
        return f"{x:.{n}f}".replace(".", ",")
    return str(x).replace("|", "/")


def denemeler(dallar):
    out = []
    for parca in str(dallar or "").split(" | "):
        m = IZ.match(parca.strip())
        if m:
            out.append({
                "no": int(m.group(1)),
                "dal": m.group(2),
                "kbit": int(m.group(3)),
                "hedeflenen_mb": float(m.group(4)),
                "cikan_mb": float(m.group(5)),
            })
    return out


def sureler(metin):
    out = {}
    for parca in str(metin or "").split(" | "):
        m = re.match(r"^(\d+):([\d.]+)sn$", parca.strip())
        if m:
            out[int(m.group(1))] = float(m.group(2))
    return out


def yukle(kok):
    satir = []
    for yol in sorted(Path(kok).rglob("*butceilk-*.json")):
        try:
            satir.extend(json.loads(yol.read_text(encoding="utf-8-sig")))
        except Exception as e:  # noqa: BLE001
            print(f"okunamadi {yol}: {e}", file=sys.stderr)
    return satir


def main():
    satir = yukle(sys.argv[1])
    hata = [s for s in satir if s.get("hata")]
    urun = [s for s in satir if str(s.get("kol", "")).startswith("urun-")]
    hb = {(s["kesit"], s["istenen_kbit"]): s for s in satir if s.get("kol") == "handbrake"}
    kapi = {(s["kesit"], s["istenen_kbit"]): s for s in satir if s.get("kol") == "kapi"}

    urun.sort(key=lambda s: (SIRA.get(s["kesit"], 9), s["istenen_kbit"], s["kol"]))

    print("## Ham satirlar\n")
    print("### Urun kollari\n")
    for s in urun:
        print(json.dumps(s, ensure_ascii=False, sort_keys=False))
    print("\n### HandBrake kollari\n")
    for s in sorted(hb.values(), key=lambda s: (SIRA.get(s["kesit"], 9), s["istenen_kbit"])):
        print(json.dumps(s, ensure_ascii=False))
    print("\n### Kapi satirlari\n")
    for s in sorted(kapi.values(), key=lambda s: (SIRA.get(s["kesit"], 9), s["istenen_kbit"])):
        print(json.dumps(s, ensure_ascii=False))
    if hata:
        print("\n### Hatalar\n")
        for s in hata:
            print(f"{s.get('kesit')} {s.get('kol')} {s.get('istenen_kbit')}: {s['hata']}")

    print("\n## Tablo 1 — Deneme sayisi, sure dagilimi, HandBrake orani\n")
    print("| Kesit | kbit | Zorlanan | Kodlayici | Deneme | Deneme sureleri | Ilk deneme sn | "
          "Deneme toplami sn | Kodlama disi sn | Toplam sn | HB sn | Oran toplam | Oran ilk deneme | "
          "Tek deneme toplami sn | Oran tek deneme | B5 toplam | B5 ilk deneme | B5 tek deneme |")
    print("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|")
    for s in urun:
        h = hb.get((s["kesit"], s["istenen_kbit"]))
        hbsn = h.get("kodlama_sn") if h else None
        ilk = s.get("ilk_deneme_sn")
        dis = (s["toplam_sn"] - s["kodlama_sn"]) if (s.get("toplam_sn") and s.get("kodlama_sn")) else None
        tek = (dis + ilk) if (dis is not None and ilk is not None) else None
        ot = s.get("toplam_sn") / hbsn if hbsn else None
        oi = (ilk / hbsn) if (hbsn and ilk is not None) else None
        ok = (tek / hbsn) if (hbsn and tek is not None) else None
        print(f"| {s['kesit']} | {s['istenen_kbit']} | {s.get('zorlanan_kodek')} | "
              f"{s.get('kodlayici')}{'' if s.get('kodek_tuttu') else ' (TUTMADI)'} | {s.get('deneme')} | "
              f"{v(s.get('deneme_sureleri'))} | {v(ilk, 1)} | "
              f"{v(s.get('deneme_sn_toplami'), 1)} | {v(dis, 1)} | "
              f"{v(s.get('toplam_sn'), 1)} | {v(hbsn, 1)} | {v(ot, 2)} | {v(oi, 2)} | "
              f"{v(tek, 1)} | {v(ok, 2)} | "
              f"{'kaldi' if (ot is not None and ot > 1.0) else 'gecti' if ot is not None else '—'} | "
              f"{'kaldi' if (oi is not None and oi > 1.0) else 'gecti' if oi is not None else '—'} | "
              f"{'kaldi' if (ok is not None and ok > 1.0) else 'gecti' if ok is not None else '—'} |")

    print("\n## Tablo 2 — Ikinci tam kodlamanin sebebi ve tahmin hatasi\n")
    print("Bant kenarlari FillBand.For kademelerinden; ust kenar hedefin kendisi. "
          "Butce doldurma esigi 0,97*hedef, nisani 0,985*hedef.\n")
    print("| Kesit | kbit | Zorlanan | Hedef MB | Bant alt MB | Ilk cikan MB | Ilk sapma % | "
          "Ilk bantta | Ilk esigin ustunde | 1. kbit | Lineer duzeltme kbit | 2. deneme kbit | "
          "2. deneme dali | 2. cikan MB | 2. sapma % | Ek deneme sn | Ek denemenin toplamdaki payi % |")
    print("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|")
    for s in urun:
        hedef = s.get("hedef_mb")
        d = denemeler(s.get("dallar"))
        sn = sureler(s.get("deneme_sureleri"))
        if not d or not hedef:
            continue
        ilk = d[0]
        ilk_sapma = (ilk["cikan_mb"] - hedef) / hedef * 100
        bant_alt, _ = bant(hedef)
        ilk_bantta = bant_alt <= ilk["cikan_mb"] <= hedef
        ilk_esik_ustu = ilk["cikan_mb"] >= DOLDUR_ESIK * hedef
        iki = d[1] if len(d) > 1 else None
        iki_sapma = (iki["cikan_mb"] - hedef) / hedef * 100 if iki else None
        ek_sn = sum(x for k, x in sn.items() if k >= 2) if sn else None
        pay = (ek_sn / s["toplam_sn"] * 100) if (ek_sn is not None and s.get("toplam_sn")) else None
        lineer = ilk["kbit"] * hedef / ilk["cikan_mb"] if ilk["cikan_mb"] else None
        print(f"| {s['kesit']} | {s['istenen_kbit']} | {s.get('zorlanan_kodek')} | {v(hedef, 3)} | "
              f"{v(bant_alt, 3)} | {v(ilk['cikan_mb'], 3)} | {v(ilk_sapma, 2)} | "
              f"{v(ilk_bantta)} | {v(ilk_esik_ustu)} | {ilk['kbit']} | {v(lineer, 0)} | "
              f"{iki['kbit'] if iki else '—'} | {iki['dal'] if iki else '—'} | "
              f"{v(iki['cikan_mb'], 3) if iki else '—'} | {v(iki_sapma, 2)} | {v(ek_sn, 1)} | {v(pay, 1)} |")

    print("\n## Tablo 3 — Lineer duzeltmenin olculmus karsiligi\n")
    print("Ilk denemenin kbit'inden hedefe lineer gidilse istenecek kbit; yanina ayni "
          "kosumda o kbit'e en yakin OLCULMUS deneme ve onun cikan boyutu. Kbit farki "
          "kucukse satir olculmus sayilir, buyukse lineer degerde olcum yok.\n")
    print("| Kesit | kbit | Zorlanan | Hedef MB | Lineer kbit | En yakin olculmus kbit | "
          "Kbit farki % | O denemenin cikani MB | Sapma % | Bantta | Esigin ustunde |")
    print("|---|---|---|---|---|---|---|---|---|---|---|")
    for s in urun:
        hedef = s.get("hedef_mb")
        d = denemeler(s.get("dallar"))
        if not d or not hedef:
            continue
        ilk = d[0]
        if not ilk["cikan_mb"]:
            continue
        lineer = ilk["kbit"] * hedef / ilk["cikan_mb"]
        yakin = min(d, key=lambda x: abs(x["kbit"] - lineer))
        fark = (yakin["kbit"] - lineer) / lineer * 100
        sapma = (yakin["cikan_mb"] - hedef) / hedef * 100
        bant_alt, _ = bant(hedef)
        print(f"| {s['kesit']} | {s['istenen_kbit']} | {s.get('zorlanan_kodek')} | {v(hedef, 3)} | "
              f"{v(lineer, 0)} | {yakin['kbit']} | {v(fark, 1)} | {v(yakin['cikan_mb'], 3)} | "
              f"{v(sapma, 2)} | {v(bant_alt <= yakin['cikan_mb'] <= hedef)} | "
              f"{v(yakin['cikan_mb'] >= DOLDUR_ESIK * hedef)} |")

    print("\n## Tablo 4 — x265 ve x264 ayni kesitte\n")
    print("| Kesit | kbit | x265 deneme | x264 deneme | x265 toplam sn | x264 toplam sn | "
          "x265 ilk sn | x264 ilk sn | x265/x264 toplam | x265 oran (HB) | x264 oran (HB) |")
    print("|---|---|---|---|---|---|---|---|---|---|---|")
    esli = {}
    for s in urun:
        esli.setdefault((s["kesit"], s["istenen_kbit"]), {})[s.get("zorlanan_kodek")] = s
    for anahtar in sorted(esli, key=lambda a: (SIRA.get(a[0], 9), a[1])):
        a, b = esli[anahtar].get("x265"), esli[anahtar].get("h264")
        if not a or not b:
            continue
        h = hb.get(anahtar)
        hbsn = h.get("kodlama_sn") if h else None
        print(f"| {anahtar[0]} | {anahtar[1]} | {a.get('deneme')} | {b.get('deneme')} | "
              f"{v(a.get('toplam_sn'), 1)} | {v(b.get('toplam_sn'), 1)} | {v(a.get('ilk_deneme_sn'), 1)} | "
              f"{v(b.get('ilk_deneme_sn'), 1)} | {v(a['toplam_sn'] / b['toplam_sn'] if b.get('toplam_sn') else None)} | "
              f"{v(a['toplam_sn'] / hbsn if hbsn else None)} | {v(b['toplam_sn'] / hbsn if hbsn else None)} |")


if __name__ == "__main__":
    main()
