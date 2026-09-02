#!/usr/bin/env python3
"""Belgedeki birebir alintiyi kaynagindan dogrular.

Denetlenen iddia bicimleri:

  A (kunye -> alinti): `yol/dosya.uzanti:N[-M]` kunyesini bir ayirac (— – - :)
      ve ters tirnakli bir dizge izliyorsa, dizge o dosyada aranir. Kunye satir
      numarasi tasimak zorundadir.
  B (kunye -> yigin): kunyeyi bir cit blogu (```) izliyorsa, blogun icerigi o
      dosyada aranir. Kunye satir numarasi tasimak zorundadir.
  C (blok kapsami, --supheli ile): bir madde ya da paragraf icindeki ters
      tirnakli dizge, o blokta anilan butun kaynak dosyalarin birlesiminde
      aranir; hicbirinde yoksa bildirilir.

Bulgu siniflari:
  KAYMA        dizge dosyada hic yok.
  SATIR KAYDI  dizge var ama kunyedeki satir araliginda degil.
  SUPHELI      C bicimi bulgusu (yanlis pozitif orani daha yuksek).

Atlananlar sayilir ve --atlananlar ile listelenir.
"""

import argparse
import os
import re
import subprocess
import sys
import tempfile
from pathlib import Path

SURUM = "1.0"

KOD_UZANTILARI = {
    ".cs", ".xaml", ".axaml", ".ps1", ".sh", ".py", ".js", ".json", ".yml",
    ".yaml", ".csproj", ".sln", ".md", ".c", ".h", ".cpp", ".vbs", ".txt",
    ".props", ".targets", ".resx", ".xml", ".toml", ".cfg", ".ini", ".bat",
}

KUNYE = re.compile(
    r"`(?P<yol>[A-Za-z0-9_./\\-]+\.[A-Za-z0-9]{1,8})"
    r"(?::(?P<bas>\d+)(?:-(?P<son>\d+))?)?`"
)

TIRNAK_SPAN = re.compile(r"(?<!`)(`{1,2})(?!`)(.+?)(?<!`)\1(?!`)", re.DOTALL)

ELIPS = re.compile(r"\.\.\.|…")

NOKTALAMA = set("=(){}[]$|;<>\"'/\\*&+%#@!?,:")

DUZYAZI_ISARETI = set("×·−≤≥≠⇒→←≈±²³√∙–—")

SOZDIZIM_ISARETI = set(";{}()[]\"'$<>\\|")

BAYRAK = re.compile(r"(?:^|\s)--?[A-Za-z]")

ATAMA = re.compile(
    r"[A-Za-z_][A-Za-z0-9_.]*\s*(?:==|!=|<=|>=|\+=|-=|=|<|>)\s*"
    r"[A-Za-z_][A-Za-z0-9_.]*"
)

EK = re.compile(r"['’][A-Za-zçğıöşüÇĞİÖŞÜ]*")

BULUNMA_EKI = re.compile(r"['’]n?[dt][aeıiuü](ki)?$")

DUZ = {
    "’": "'", "‘": "'", "“": '"', "”": '"',
    " ": " ", "​": "",
}


def duzle(s):
    for a, b in DUZ.items():
        s = s.replace(a, b)
    return s


def normalle(s):
    s = duzle(s)
    parcalar = []
    harita = []
    onceki_bosluk = True
    for i, ch in enumerate(s):
        if ch.isspace():
            if not onceki_bosluk:
                parcalar.append(" ")
                harita.append(i)
                onceki_bosluk = True
        else:
            parcalar.append(ch)
            harita.append(i)
            onceki_bosluk = False
    while parcalar and parcalar[-1] == " ":
        parcalar.pop()
        harita.pop()
    return "".join(parcalar), harita


class Kaynak:
    def __init__(self, yol, metin):
        self.yol = yol
        self.metin = metin
        self.norm, self.harita = normalle(metin)
        self.satir_basi = [0]
        for i, ch in enumerate(metin):
            if ch == "\n":
                self.satir_basi.append(i + 1)

    def satir_no(self, ham_indeks):
        dus, yuk = 0, len(self.satir_basi) - 1
        while dus < yuk:
            orta = (dus + yuk + 1) // 2
            if self.satir_basi[orta] <= ham_indeks:
                dus = orta
            else:
                yuk = orta - 1
        return dus + 1

    def ara(self, ihtiyac):
        n, _ = normalle(ihtiyac)
        if not n:
            return None
        i = self.norm.find(n)
        if i < 0:
            return None
        return self.satir_no(self.harita[i])

    def ara_parcali(self, parcalar):
        imlec = 0
        ilk = None
        for p in parcalar:
            n, _ = normalle(p)
            if not n:
                continue
            i = self.norm.find(n, imlec)
            if i < 0:
                return None
            if ilk is None:
                ilk = self.satir_no(self.harita[i])
            imlec = i + len(n)
        return ilk


class Dizin:
    def __init__(self, kok):
        self.kok = Path(kok).resolve()
        self.yollar = {}
        self.taban = {}
        self.onbellek = {}
        for r in self._dosyalar():
            p = r.replace("\\", "/")
            self.yollar[p.lower()] = r
            self.taban.setdefault(os.path.basename(p).lower(), []).append(r)

    def _dosyalar(self):
        try:
            c = subprocess.run(
                ["git", "-C", str(self.kok), "ls-files"],
                capture_output=True, text=True, encoding="utf-8", timeout=60,
            )
            if c.returncode == 0 and c.stdout.strip():
                return [l for l in c.stdout.splitlines() if l.strip()]
        except Exception:
            pass
        cikti = []
        for d, alt, dosyalar in os.walk(self.kok):
            alt[:] = [a for a in alt if a not in (".git", "node_modules", "bin", "obj")]
            for f in dosyalar:
                cikti.append(
                    str(Path(d, f).relative_to(self.kok)).replace("\\", "/")
                )
        return cikti

    def coz(self, atif):
        a = atif.replace("\\", "/").lstrip("./").lower()
        if a in self.yollar:
            return self.yollar[a], None
        adaylar = [v for k, v in self.yollar.items() if k.endswith("/" + a)]
        if len(adaylar) == 1:
            return adaylar[0], None
        if len(adaylar) > 1:
            return None, "belirsiz-yol"
        t = os.path.basename(a)
        if t in self.taban:
            if len(self.taban[t]) == 1:
                return self.taban[t][0], None
            return None, "belirsiz-taban"
        if os.path.splitext(t)[1] not in KOD_UZANTILARI:
            return None, "kod-disi-uzanti"
        return None, "depoda-yok"

    def oku(self, goreli):
        if goreli not in self.onbellek:
            try:
                m = (self.kok / goreli).read_text(encoding="utf-8", errors="replace")
            except OSError:
                m = None
            self.onbellek[goreli] = Kaynak(goreli, m) if m is not None else None
        return self.onbellek[goreli]


def icerik_mi(dizge):
    d = dizge.strip()
    if len(d) < 12:
        return False, "kisa"
    if KUNYE.fullmatch("`" + d + "`"):
        return False, "kunye"
    if re.fullmatch(r"[A-Za-z0-9_./\\-]+\.[A-Za-z0-9]{1,8}", d):
        return False, "yol"
    if re.fullmatch(r"[-+]?[\d.,%\s]+(?:[a-zA-Z%]{0,4})?", d):
        return False, "sayi"
    if not any(ch in NOKTALAMA or ch == " " for ch in d):
        return False, "tek-simge"
    if not any(ch in NOKTALAMA for ch in d):
        return False, "noktalama-yok"
    if any(ch in DUZYAZI_ISARETI for ch in d):
        return False, "duzyazi-formulu"
    if (not any(ch in SOZDIZIM_ISARETI for ch in d) and not BAYRAK.search(d)
            and not ATAMA.fullmatch(d)):
        return False, "sozdizim-yok"
    return True, None


def citsiz_bloklar(metin):
    satirlar = metin.splitlines()
    icerde = False
    cit_araligi = []
    bas = None
    for i, s in enumerate(satirlar):
        if s.lstrip().startswith("```"):
            if not icerde:
                bas = i
                icerde = True
            else:
                cit_araligi.append((bas, i))
                icerde = False
    if icerde and bas is not None:
        cit_araligi.append((bas, len(satirlar) - 1))
    return satirlar, cit_araligi


def cit_bloklari(metin):
    satirlar, araliklar = citsiz_bloklar(metin)
    for bas, son in araliklar:
        onceki = bas - 1
        while onceki >= 0 and not satirlar[onceki].strip():
            onceki -= 1
        if onceki < 0:
            continue
        yield satirlar[onceki], onceki + 1, "\n".join(satirlar[bas + 1:son]), bas + 2


def bloklar(metin):
    satirlar, araliklar = citsiz_bloklar(metin)
    citli = set()
    for bas, son in araliklar:
        for i in range(bas, son + 1):
            citli.add(i)
    tampon = []
    bas_no = None
    for i, s in enumerate(satirlar):
        if i in citli:
            if tampon:
                yield "\n".join(tampon), bas_no
                tampon, bas_no = [], None
            continue
        yeni_madde = re.match(r"^\s*(?:[-*+]|\d+[.)])\s", s)
        if not s.strip() or yeni_madde:
            if tampon:
                yield "\n".join(tampon), bas_no
                tampon, bas_no = [], None
        if s.strip():
            if not tampon:
                bas_no = i + 1
            tampon.append(s)
    if tampon:
        yield "\n".join(tampon), bas_no


def tirnak_spanlari(metin):
    for m in TIRNAK_SPAN.finditer(metin):
        yield m.start(), m.group(2)


def dogrula(kaynak, dizge):
    if ELIPS.search(dizge):
        parcalar = [p for p in ELIPS.split(dizge) if p.strip()]
        if len(parcalar) < 2:
            return "atla", None, "kirpik-tek-parca"
        satir = kaynak.ara_parcali(parcalar)
        return ("bulundu" if satir else "yok"), satir, "kirpik"
    satir = kaynak.ara(dizge)
    return ("bulundu" if satir else "yok"), satir, None


class Bulgu:
    def __init__(self, sinif, belge, satir, atif, dizge, not_=""):
        self.sinif = sinif
        self.belge = belge
        self.satir = satir
        self.atif = atif
        self.dizge = dizge
        self.not_ = not_

    def yaz(self):
        k = self.dizge.replace("\n", " ")
        if len(k) > 96:
            k = k[:93] + "..."
        s = f"{self.sinif}  {self.belge}:{self.satir}  -> {self.atif}\n    `{k}`"
        if self.not_:
            s += f"\n    {self.not_}"
        return s


def satir_no_of(metin, indeks):
    return metin.count("\n", 0, indeks) + 1


def belge_denetle(dizin, belge_yolu, metin, supheli=False):
    bulgular = []
    atlanan = []
    denetlenen = 0

    for m in KUNYE.finditer(metin):
        if not m.group("bas"):
            continue
        kuyruk = metin[m.end():]
        ek = EK.match(kuyruk)
        bulunma = bool(ek and BULUNMA_EKI.match(ek.group(0)))
        if ek:
            kuyruk = kuyruk[ek.end():]
        ayr = re.match(r"[ \t]*[—–:-][ \t]*", kuyruk)
        paren = re.match(r"[ \t]*\n?[ \t]*\(", kuyruk)
        if ayr:
            kalan = kuyruk[ayr.end():]
        elif paren:
            kalan = kuyruk[paren.end():]
        elif bulunma:
            bos = re.match(r"[ \t]*\n?[ \t]*", kuyruk)
            if not bos.group(0):
                continue
            kalan = kuyruk[bos.end():]
        else:
            continue
        sp = TIRNAK_SPAN.match(kalan)
        if not sp:
            continue
        dizge = sp.group(2)
        yer = satir_no_of(metin, m.start())
        atif = m.group(0).strip("`")
        goreli, sebep = dizin.coz(m.group("yol"))
        if not goreli:
            atlanan.append((belge_yolu, yer, atif, sebep))
            continue
        kaynak = dizin.oku(goreli)
        if kaynak is None:
            atlanan.append((belge_yolu, yer, atif, "okunamadi"))
            continue
        uygun, neden = icerik_mi(dizge)
        if not uygun:
            atlanan.append((belge_yolu, yer, atif, "icerik-degil:" + neden))
            continue
        denetlenen += 1
        durum, bsatir, notu = dogrula(kaynak, dizge)
        if durum == "atla":
            atlanan.append((belge_yolu, yer, atif, notu))
            denetlenen -= 1
        elif durum == "yok":
            bulgular.append(Bulgu("KAYMA", belge_yolu, yer, atif, dizge,
                                  f"{goreli} icinde yok"))
        else:
            bas = int(m.group("bas"))
            son = int(m.group("son") or bas)
            if not (bas <= bsatir <= son):
                bulgular.append(Bulgu(
                    "SATIR KAYDI", belge_yolu, yer, atif, dizge,
                    f"{goreli} icinde var ama :{bsatir}, kunye :{bas}"
                    + (f"-{son}" if son != bas else "")))

    for onceki, onceki_no, govde, govde_no in cit_bloklari(metin):
        kunyeler = [k for k in KUNYE.finditer(onceki) if k.group("bas")]
        if len(kunyeler) != 1:
            if KUNYE.search(onceki):
                atlanan.append((belge_yolu, onceki_no, "cit", "kunye-satirsiz-ya-da-coklu"))
            continue
        k = kunyeler[0]
        goreli, sebep = dizin.coz(k.group("yol"))
        if not goreli:
            atlanan.append((belge_yolu, onceki_no, k.group(0).strip("`"), sebep))
            continue
        kaynak = dizin.oku(goreli)
        if kaynak is None:
            continue
        uygun, neden = icerik_mi(govde)
        if not uygun:
            atlanan.append((belge_yolu, govde_no, k.group(0).strip("`"),
                            "icerik-degil:" + neden))
            continue
        denetlenen += 1
        durum, bsatir, notu = dogrula(kaynak, govde)
        if durum == "atla":
            atlanan.append((belge_yolu, govde_no, k.group(0).strip("`"), notu))
            denetlenen -= 1
        elif durum == "yok":
            bulgular.append(Bulgu("KAYMA", belge_yolu, govde_no,
                                  k.group(0).strip("`"), govde,
                                  f"{goreli} icinde yok (cit blogu)"))
        else:
            bas = int(k.group("bas"))
            son = int(k.group("son") or bas)
            if not (bas <= bsatir <= son):
                bulgular.append(Bulgu(
                    "SATIR KAYDI", belge_yolu, govde_no, k.group(0).strip("`"),
                    govde, f"{goreli} icinde var ama :{bsatir}, kunye :{bas}"))

    if supheli:
        for blok, blok_no in bloklar(metin):
            kaynaklar = []
            kunye_yerleri = []
            for k in KUNYE.finditer(blok):
                goreli, _ = dizin.coz(k.group("yol"))
                if goreli:
                    s = dizin.oku(goreli)
                    if s is not None:
                        kaynaklar.append(s)
                kunye_yerleri.append((k.start(), k.end()))
            if not kaynaklar:
                continue
            for yer, dizge in tirnak_spanlari(blok):
                if any(a <= yer < b for a, b in kunye_yerleri):
                    continue
                uygun, neden = icerik_mi(dizge)
                if not uygun:
                    continue
                if any(dogrula(s, dizge)[0] == "bulundu" for s in kaynaklar):
                    continue
                if any(dogrula(s, dizge)[0] == "atla" for s in kaynaklar):
                    continue
                denetlenen += 1
                bulgular.append(Bulgu(
                    "SUPHELI", belge_yolu,
                    blok_no + blok.count("\n", 0, yer),
                    ", ".join(s.yol for s in kaynaklar), dizge,
                    "blokta anilan dosyalarin hicbirinde yok"))

    return bulgular, atlanan, denetlenen


def tara(kok, desenler, supheli):
    dizin = Dizin(kok)
    kokp = Path(kok).resolve()
    belgeler = []
    for d in desenler:
        p = kokp / d
        if p.is_file():
            belgeler.append(p)
        else:
            belgeler.extend(sorted(p.rglob("*.md")))
    tum_bulgu, tum_atlanan = [], []
    denetlenen = 0
    for b in sorted(set(belgeler)):
        goreli = str(b.relative_to(kokp)).replace("\\", "/")
        metin = b.read_text(encoding="utf-8", errors="replace")
        bu, at, den = belge_denetle(dizin, goreli, metin, supheli)
        tum_bulgu.extend(bu)
        tum_atlanan.extend(at)
        denetlenen += den
    return tum_bulgu, tum_atlanan, denetlenen, len(set(belgeler))


ORNEK_KAYNAK = """using System;

namespace Ornek
{
    public static class Hesap
    {
        public const int Tavan = 1920;

        public static double Harmonik(double[] p)
        {
            return p.Length / p.Sum(x => 1.0 / Math.Max(x, 1.0));
        }

        public static string Etiket(int n)
        {
            return $"bant={n} tasma=yok";
        }
    }
}
"""

ORNEK_BELGE_YESIL = """# Ornek

- `Hesap.cs:7` — `public const int Tavan = 1920;`
- `Hesap.cs:11` — `return p.Length / p.Sum(x => 1.0 / Math.Max(x, 1.0));`
- Kirpilmis: `Hesap.cs:11` — `return p.Length ... Math.Max(x, 1.0));`
- Satir sonuna sarilmis: `Hesap.cs:16` — `return $"bant={n}
  tasma=yok";`

Yigin (`Hesap.cs:11`):

```csharp
return p.Length / p.Sum(x => 1.0 / Math.Max(x, 1.0));
```
"""

ORNEK_BELGE_KIRMIZI = """# Ornek

- `Hesap.cs:7` — `public const int Tavan = 1080;`
- `Hesap.cs:3` — `return p.Length / p.Sum(x => 1.0 / Math.Max(x, 1.0));`

Yigin (`Hesap.cs:11`):

```csharp
return p.Length / p.Sum(x => 1.0 / Max(x, 1.0));
```
"""


def kendini_sina():
    gecti = True
    with tempfile.TemporaryDirectory() as td:
        kok = Path(td)
        (kok / "src").mkdir()
        (kok / "docs").mkdir()
        (kok / "src" / "Hesap.cs").write_text(ORNEK_KAYNAK, encoding="utf-8")

        (kok / "docs" / "o.md").write_text(ORNEK_BELGE_YESIL, encoding="utf-8")
        b, a, d = _sina_kos(kok)
        gecti &= _bekle("yesil ornek bulgu vermez", len(b), 0, b)
        gecti &= _bekle("yesil ornek 5 iddia denetler", d, 5, b)

        (kok / "docs" / "o.md").write_text(ORNEK_BELGE_KIRMIZI, encoding="utf-8")
        b, a, d = _sina_kos(kok)
        kayma = [x for x in b if x.sinif == "KAYMA"]
        satir = [x for x in b if x.sinif == "SATIR KAYDI"]
        gecti &= _bekle("bozuk ornek 2 KAYMA verir", len(kayma), 2, b)
        gecti &= _bekle("bozuk ornek 1 SATIR KAYDI verir", len(satir), 1, b)

        (kok / "docs" / "o.md").write_text(ORNEK_BELGE_YESIL, encoding="utf-8")
        b, a, d = _sina_kos(kok)
        gecti &= _bekle("duzeltilince yine yesil", len(b), 0, b)

        (kok / "src" / "Hesap.cs").write_text(
            ORNEK_KAYNAK.replace("Tavan = 1920", "Tavan = 1280"), encoding="utf-8")
        b, a, d = _sina_kos(kok)
        gecti &= _bekle("kaynak degisince kirmizi", len(b), 1, b)

    print("KENDI SINAMASI: " + ("GECTI" if gecti else "KALDI"))
    return 0 if gecti else 1


def _sina_kos(kok):
    return tara(str(kok), ["docs"], False)[:3]


def _bekle(ad, oldu, olmali, bulgular):
    if oldu == olmali:
        print(f"  ok   {ad}")
        return True
    print(f"  KALDI {ad}: {oldu} != {olmali}")
    for b in bulgular:
        print("        " + b.yaz().replace("\n", "\n        "))
    return False


def main(argv):
    a = argparse.ArgumentParser(
        prog="alinti-denetimi",
        description="Belgede birebir diye verilen dizgeyi kaynagindan dogrular.",
        epilog=__doc__,
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )
    a.add_argument("hedef", nargs="*", default=["docs"],
                   help="taranacak klasor ya da dosya (varsayilan: docs)")
    a.add_argument("--kok", default=".", help="depo koku (varsayilan: .)")
    a.add_argument("--supheli", action="store_true",
                   help="C bicimini (blok kapsami) da denetle")
    a.add_argument("--atlananlar", action="store_true",
                   help="atlanan iddialari sebebiyle listele")
    a.add_argument("--self-test", dest="sinama", action="store_true",
                   help="gomulu ornekle kendini sina")
    a.add_argument("--surum", action="version", version=f"alinti-denetimi {SURUM}")
    n = a.parse_args(argv)

    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")

    if n.sinama:
        return kendini_sina()

    bulgular, atlanan, denetlenen, belge_sayisi = tara(n.kok, n.hedef, n.supheli)

    for b in bulgular:
        print(b.yaz())
        print()

    if n.atlananlar:
        print("ATLANAN IDDIALAR")
        sayac = {}
        for belge, satir, atif, sebep in atlanan:
            sayac[sebep] = sayac.get(sebep, 0) + 1
            print(f"  {belge}:{satir}  {atif}  [{sebep}]")
        print()
        for s, c in sorted(sayac.items(), key=lambda x: -x[1]):
            print(f"  {c:4d}  {s}")
        print()

    kayma = sum(1 for b in bulgular if b.sinif == "KAYMA")
    satir = sum(1 for b in bulgular if b.sinif == "SATIR KAYDI")
    sup = sum(1 for b in bulgular if b.sinif == "SUPHELI")
    print(f"belge: {belge_sayisi}  denetlenen iddia: {denetlenen}  "
          f"atlanan: {len(atlanan)}")
    print(f"KAYMA: {kayma}  SATIR KAYDI: {satir}  SUPHELI: {sup}")
    return 1 if bulgular else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
