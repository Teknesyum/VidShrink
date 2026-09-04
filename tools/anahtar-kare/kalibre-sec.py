"""Zorluk-esitlenmis calisma noktasini kalibrasyon merdiveninden secer.

Hedef ortalama = o kaynagin kendi kayipsiz tavani eksi BOSLUK. Kayipsiz tavan
hizalama kapisindan gelir (`hizalama.txt`), merdiven `kalibre*.txt`ten. Ara
deger log-bitrate'te dogrusal interpolasyondur; secilen bitrate elle
yazilmaz.
"""
import os, sys, math

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CAL = os.path.join(ROOT, ".calisma", "t133")
BOSLUK = 10.0


def tavanlar():
    d = {}
    for line in open(os.path.join(CAL, "hizalama.txt"), encoding="utf-8"):
        p = line.split()
        if len(p) < 6:
            continue
        d[p[0]] = float(p[-3])
    return d


def merdiven():
    d = {}
    for ad in ("kalibre.txt", "kalibre-s4.txt", "kalibre-ek.txt"):
        yol = os.path.join(CAL, ad)
        if not os.path.exists(yol):
            continue
        for line in open(yol, encoding="utf-8"):
            p = line.split()
            if len(p) != 7 or p[0] == "kaynak":
                continue
            try:
                d.setdefault(p[0], []).append((int(p[1]), float(p[3])))
            except ValueError:
                continue
    for k in d:
        d[k] = sorted(set(d[k]))
    return d


def sec(nokta, hedef):
    alt = ust = None
    for br, m in nokta:
        if m <= hedef:
            alt = (br, m)
        if m >= hedef and ust is None:
            ust = (br, m)
    if alt is None or ust is None or alt[0] == ust[0]:
        return None, "merdiven hedefi kapsamiyor"
    f = (hedef - alt[1]) / (ust[1] - alt[1])
    br = alt[0] * math.exp(f * math.log(ust[0] / alt[0]))
    return int(round(br / 10.0) * 10), "%d(%.3f) -> %d(%.3f), f=%.3f" % (alt[0], alt[1], ust[0], ust[1], f)


if __name__ == "__main__":
    tv, md = tavanlar(), merdiven()
    satir = []
    for s in ("s1-kesikli", "s2-durgun", "s3-hareketli", "s4-yuksek"):
        hedef = tv[s] - BOSLUK
        br, nasil = sec(md.get(s, []), hedef)
        print("# %s kayipsiz_tavan=%.3f hedef_ort=%.3f secilen=%s (%s)" % (s, tv[s], hedef, br, nasil))
        if br:
            satir.append("%s %d" % (s, br))
    with open(os.path.join(CAL, "bitrate-zorluk.txt"), "w", encoding="utf-8") as f:
        f.write("\n".join(satir) + "\n")
    print("\n".join(satir))
