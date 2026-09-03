import re, sys, glob, os, collections

KAPALI = {"done"}

def oku(yol):
    with open(yol, "rb") as f:
        ham = f.read().decode("utf-8", "replace").replace("\r\n", "\n")
    if not ham.startswith("---"):
        return None
    son = ham.find("\n---", 3)
    if son < 0:
        return None
    return ham[3:son]

def alan(bas, ad):
    m = re.search(r"^%s:[ 	]*(.*)$" % ad, bas, re.M)
    return m.group(1).strip() if m else ""

def owns_listesi(bas):
    m = re.search(r"^owns:[ 	]*(.*)$", bas, re.M)
    if not m:
        return []
    tek = m.group(1).strip()
    if tek.startswith("["):
        return [p.strip().strip('"\'') for p in tek[1:-1].split(",") if p.strip()]
    yollar = []
    for satir in bas[m.end():].split("\n"):
        if re.match(r"^\s*-\s+", satir):
            yollar.append(re.sub(r"^\s*-\s+", "", satir).strip().strip('"\''))
        elif satir.strip() and not satir.startswith(" "):
            break
    return yollar

def main():
    kok = os.path.join(os.path.dirname(__file__), "..", "contracts")
    sahip = collections.defaultdict(list)
    acik = []
    for yol in sorted(glob.glob(os.path.join(kok, "T*.md"))):
        bas = oku(yol)
        if bas is None:
            continue
        kid = alan(bas, "id") or os.path.basename(yol)[:-3]
        durum = alan(bas, "status")
        if durum in KAPALI:
            continue
        acik.append((kid, durum))
        for p in owns_listesi(bas):
            sahip[p].append(kid)

    print("Kapanmamis sozlesme: %d" % len(acik))
    for kid, d in acik:
        print("  %-6s %s" % (kid, d))

    cakisan = {p: v for p, v in sahip.items() if len(v) > 1}
    print("\nAyni yolu tutan sozlesmeler: %d" % len(cakisan))
    for p in sorted(cakisan):
        print("  CAKISMA  %-55s %s" % (p, ", ".join(cakisan[p])))

    kotu = [p for p in sahip if "**" in p or "*" in p]
    if kotu:
        print("\nGlob tasiyan owns (muhurden once somutlanmali): %d" % len(kotu))
        for p in sorted(kotu):
            print("  GLOB     %-55s %s" % (p, ", ".join(sahip[p])))

    return 1 if cakisan else 0

sys.exit(main())
