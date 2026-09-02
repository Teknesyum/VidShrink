import json, os, subprocess, sys

KOK = "C:/Users/Administrator/Desktop/Projeler/Vidshrink"
IS = KOK + "/.claude/worktrees/T134/.calisma/t134"
KAYNAK = IS + "/kaynak"
CIKTI = IS + "/cikti"
OLCU = IS + "/olcu"

AKTIF = {"KA": (804, 138), "KB": (872, 104), "KC": (1036, 22), "KD": (804, 120),
         "VD": (804, 138)}
HIZLAR = {"KA": (1000, 2000, 4000), "KB": (1000, 2000, 4000),
          "KC": (1000, 2000, 4000), "KD": (1000, 2000, 4000), "VD": (2000,)}
MODEL = "model=version=vmaf_v0.6.1neg:n_threads=8"


def vmaf(dist, ref, dvf, rvf, log):
    if os.path.exists(log):
        return
    lav = "[0:v]%s[d];[1:v]%s[r];[d][r]libvmaf=%s:log_fmt=json:log_path=%s" % (
        dvf, rvf, MODEL, os.path.basename(log))
    a = ["ffmpeg", "-hide_banner", "-loglevel", "error", "-nostdin",
         "-i", dist, "-i", ref, "-lavfi", lav, "-f", "null", "-"]
    p = subprocess.run(a, capture_output=True, text=True, errors="replace",
                       cwd=os.path.dirname(log))
    if not os.path.exists(log):
        print("HATA", log, p.stderr[-400:], flush=True)


def oku(log):
    with open(log, encoding="utf-8") as f:
        d = json.load(f)
    anahtar = "vmaf_neg" if "vmaf_neg" in d["frames"][0]["metrics"] else "vmaf"
    p = sorted(k["metrics"][anahtar] for k in d["frames"])
    n = len(p)
    i = int(0.10 * (n - 1))
    return {"kare": n, "ort": round(sum(p) / n, 3), "p10": round(p[i], 3),
            "en_kotu": round(p[0], 3)}


def main():
    os.makedirs(OLCU, exist_ok=True)
    sonuc = {}
    for ad, (h, y) in AKTIF.items():
        kirp = "crop=1920:%d:0:%d" % (h, y)
        dolgu = "pad=1920:1080:0:%d:black" % y
        for hiz in HIZLAR[ad]:
            for kol in ("duz", "kirp"):
                dist = "%s/%s-%s-%d.mp4" % (CIKTI, ad, kol, hiz)
                ref = "%s/%s.mkv" % (KAYNAK, ad)
                if not os.path.exists(dist):
                    print("YOK", dist, flush=True)
                    continue
                anahtar = "%s|%s|%d" % (ad, kol, hiz)
                sonuc[anahtar] = {"boyut": os.path.getsize(dist)}

                la = "%s/A-%s-%s-%d.json" % (OLCU, ad, kol, hiz)
                vmaf(dist, ref, kirp if kol == "duz" else "null", kirp, la)
                if os.path.exists(la):
                    sonuc[anahtar]["A"] = oku(la)

                if hiz == 2000:
                    lb = "%s/B-%s-%s-%d.json" % (OLCU, ad, kol, hiz)
                    vmaf(dist, ref, "null" if kol == "duz" else dolgu, "null", lb)
                    if os.path.exists(lb):
                        sonuc[anahtar]["B"] = oku(lb)
                print(anahtar, "olculdu", flush=True)

    for ad, (h, y) in {"NA": (1042, 4), "NB": (1072, 6)}.items():
        ref = "%s/%s.mkv" % (KAYNAK, ad)
        for kol, dvf in (("duz", "null"), ("yanlis", "pad=1920:1080:0:%d:black" % y)):
            dist = "%s/%s-%s-2000.mp4" % (CIKTI, ad, kol)
            if not os.path.exists(dist):
                print("YOK", dist, flush=True)
                continue
            anahtar = "%s|%s|2000" % (ad, kol)
            sonuc[anahtar] = {"boyut": os.path.getsize(dist)}
            lb = "%s/B-%s-%s-2000.json" % (OLCU, ad, kol)
            vmaf(dist, ref, dvf, "null", lb)
            if os.path.exists(lb):
                sonuc[anahtar]["B"] = oku(lb)
            print(anahtar, "olculdu", flush=True)

    with open(OLCU + "/vmaf.json", "w", encoding="utf-8") as f:
        json.dump(sonuc, f, indent=1)
    print("OLC BITTI")


if __name__ == "__main__":
    main()
