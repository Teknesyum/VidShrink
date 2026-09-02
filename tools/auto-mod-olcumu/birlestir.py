# -*- coding: utf-8 -*-
import io, os, subprocess, sys

base = os.path.dirname(os.path.abspath(__file__))
kok = os.path.abspath(os.path.join(base, "..", ".."))
belge = os.path.join(kok, "docs", "olcumler", "auto-mod.md")

subprocess.check_call([sys.executable, os.path.join(base, "tablolar.py")], stdout=open(os.devnull, "w"))
uretilen = io.open(os.path.join(base, "uretilen.md"), encoding="utf-8").read()
k3tab, k4tab = uretilen.split("---K4---")

metin = io.open(os.path.join(base, "k3-metin.md"), encoding="utf-8").read()
k4not = io.open(os.path.join(base, "k4-not.md"), encoding="utf-8").read()
k6 = io.open(os.path.join(base, "k6-metin.md"), encoding="utf-8").read()

metin = metin.replace("<!--K3TABLO-->", k3tab.strip())
metin = metin.replace("<!--K4TABLO-->", k4tab.strip())
metin = metin.replace("<!--K4NOT-->", k4not.strip())

d = io.open(belge, encoding="utf-8").read()

for isaret in ("## K3 ", "## K6 "):
    if isaret in d:
        sys.exit("zaten var: " + isaret)

a5 = "## K5 — HandBrake'in sormadığı, bizim sorduğumuz"
akus = "## Ölçüm sırasında bulunan kusurlar"
assert a5 in d and akus in d

d = d.replace(a5, metin.strip() + "\n\n---\n\n" + a5, 1)
d = d.replace(akus, k6.strip() + "\n\n---\n\n" + akus, 1)

io.open(belge, "w", encoding="utf-8", newline="\n").write(d)
print("birlestirildi")
