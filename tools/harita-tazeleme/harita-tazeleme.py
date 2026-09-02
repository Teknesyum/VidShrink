#!/usr/bin/env python3
"""Yol haritasindaki toplu hukum cumlelerini veriden uretir ve belgede dogrular.

Alt komutlar:

  tara      Yol haritasini tarar; olcumden gelen her sayiyi ve her "beklenir"
            cumlesini satir + bolum capasiyla listeler. K1'in tarama komutu.
  bayat     Bayat sayilar tablosunu (satir x eski x yeni x kaynak capasi) markdown
            olarak basar. K1 + K2.
  sira      Yedi maddenin eski/yeni siralamasini, kaymasini ve gerekcesini basar. K3.
  hukum     Belgeye giren toplu hukum cumlelerini basar. K6. Bu cumleler elle
            yazilmaz; belgeye buradan kopyalanir.
  dogrula   hukum ciktisindaki her cumleyi yol haritasinda birebir arar; her
            duzeltilmis sayinin hem kaynak belgede hem haritada oldugunu ve kaynak
            capasinin gercek bir baslik oldugunu denetler. Bulgu varsa cikis kodu 1.

Bosluk ve satir sonu aramada normallestirilir: belge 75 sutuna sariliyor, cumle
sarilmis halde de birebir sayilir. Bagimlilik yok, Python 3.
"""

import argparse
import re
import sys
from pathlib import Path

SURUM = "1.0"

KOK = Path(__file__).resolve().parents[2]
HARITA = KOK / "docs" / "inceleme" / "handbrake-motoru.md"
KAYNAK = KOK / "docs" / "olcumler" / "auto-mod.md"


def sayi(deger, basamak=3, isaretli=False):
    metin = f"{abs(deger):.{basamak}f}".replace(".", ",")
    if isaretli:
        return ("+" if deger >= 0 else "-") + metin
    return ("-" if deger < 0 else "") + metin


def kat(bolen, bolunen, basamak=1):
    return f"{bolunen / bolen:.{basamak}f}".replace(".", ",")


ACIK_ESKI = {"ort": 1.269, "p10": 0.827, "harmonik": 39.414}
ACIK_YENI = {"ort": 0.097, "p10": 0.477, "harmonik": 0.099}
UZMAN_ACIK = {"ort": 0.437, "p10": 0.673, "harmonik": 0.439}
URETIM_SINIRI = {"ort": 0.013, "p10": 0.023}

MADDELER = [
    {
        "eski": 1,
        "ad": "Anahtar kare aralığı içerikten türesin",
        "olculdu": {"ort": 0.181, "p10": 0.235},
        "sozlesme": "T133",
        "sozlesme_durum": "open",
        "gerekce": "Yedi maddenin ölçülmüş etkisi olan tek maddesi: kilitli ölçümde "
                   "p10 +0,235, ortalama +0,181.",
    },
    {
        "eski": 3,
        "ad": "`SceneMap` plana bağlansın",
        "olculdu": None,
        "sozlesme": "T114",
        "sozlesme_durum": "active",
        "gerekce": "Ölçülmüş değeri yok; ama müdahalenin şekli açığın kaldığı eksene "
                   "(p10) oturuyor ve ölçümü yürüyen bir sözleşmede.",
    },
    {
        "eski": 5,
        "ad": "Otomatik kırpma",
        "olculdu": None,
        "sozlesme": "T134",
        "sozlesme_durum": "active",
        "gerekce": "Bu kaynakta yapısal olarak sıfır; ama ölçülen kaynakta kalan "
                   "açık zaten küçük ve hiç ölçmediğimiz kaynak sınıfını yürüyen bir "
                   "sözleşme ölçüyor.",
    },
    {
        "eski": 2,
        "ad": "Örnekleme penceresi içeriğe göre büyüsün",
        "olculdu": None,
        "sozlesme": None,
        "sozlesme_durum": None,
        "gerekce": "Ölçülmedi, yürüyen sözleşmesi yok; kendi metni puan açığına "
                   "katkısını dolaylı ve küçük diyor.",
    },
    {
        "eski": 4,
        "ad": "Kare hızı modu `pfr` olsun",
        "olculdu": None,
        "sozlesme": None,
        "sozlesme_durum": None,
        "gerekce": "Ölçülmedi, yürüyen sözleşmesi yok; yalnız fps düşürülen "
                   "koşumlarda görünür.",
    },
    {
        "eski": 7,
        "ad": "Turbo ilk geçiş",
        "olculdu": None,
        "sozlesme": None,
        "sozlesme_durum": None,
        "gerekce": "Kalite maddesi değil, süre maddesi; puan açığına katkısı yok ya "
                   "da hafif negatif.",
    },
    {
        "eski": 6,
        "ad": "Taramalı içerik",
        "olculdu": None,
        "sozlesme": None,
        "sozlesme_durum": None,
        "gerekce": "Ürün kapsamı sorusu, açık sorusu değil — ölçütün dışında kaldığı "
                   "için sonda.",
    },
]

BAYAT = [
    {
        "satir": ":68",
        "capa": "1. Hedef boyut yolu > Bulgu",
        "eski": "boyut eşlemesi HandBrake için **iki koşum**",
        "yeni": "T102 için doğru; T111 boyut eşlemesini yeniden kurdu — AV1'de beş "
                "deneme, HandBrake'te bir deneme",
        "kaynak": 'Boyut eşlemesi — band yoklandı, yöntem ve deneme sayısı | "AV1\'de beş deneme, HandBrake\'te bir deneme."',
        "durum": "kilitten etkilenmez",
        "dogrula": [],
    },
    {
        "satir": ":164",
        "capa": "3. Dinamiklik iddiası > anahtar kare zamanları",
        "eski": "`0,02 / 10,02 / 20,02 / 28,35 / 38,35 / 48,35 / 56,87`",
        "yeni": "aynı — `ffprobe` sayımı, VMAF ölçerinden bağımsız",
        "kaynak": 'K4 > En büyük kalem: anahtar kare aralığı | "HandBrake\'in anahtar kare zamanları"',
        "durum": "kilitten etkilenmez",
        "dogrula": [],
    },
    {
        "satir": ":167",
        "capa": "3. Dinamiklik iddiası > sahne kesmeleri",
        "eski": "sahne kesme skorları 0,314 ve 0,261",
        "yeni": "aynı — sahne tespiti, VMAF ölçerinden bağımsız",
        "kaynak": 'K4 > En büyük kalem: anahtar kare aralığı | "Çıktı: pts_time 28.353"',
        "durum": "kilitten etkilenmez",
        "dogrula": [],
    },
    {
        "satir": ":398",
        "capa": "6. Açık uçlar > öncelik uyarısı",
        "eski": "auto ortalaması **94,462**",
        "yeni": "**95,647** — kilitli, T111'in yeniden ürettiği auto",
        "kaynak": 'Boyut eşlemesi — band yoklandı, yöntem ve deneme sayısı | tablo başlığı "açık (auto = 95,647)"',
        "durum": "kilitle düzeltildi",
        "dogrula": ["95,647"],
    },
    {
        "satir": ":398",
        "capa": "6. Açık uçlar > öncelik uyarısı",
        "eski": "uzman-hb2 ortalaması **95,731**",
        "yeni": "**95,743** — kilitli",
        "kaynak": 'Boyut eşlemesi — band yoklandı, yöntem ve deneme sayısı | tablo satırı "`uzman-hb2` | 1900 kbps"',
        "durum": "kilitle düzeltildi",
        "dogrula": ["95,743"],
    },
    {
        "satir": ":398",
        "capa": "6. Açık uçlar > öncelik uyarısı",
        "eski": "alıntıdaki **15,04 MiB**",
        "yeni": "alıntı kaynakta aynen duruyor; T111'in yeni auto'su **15 496 155** "
                "bayt teslim etti, %1,72 küçük",
        "kaynak": 'Boyut eşlemesi — band yoklandı, yöntem ve deneme sayısı | "yeni auto **15 496 155** bayt teslim etti"',
        "durum": "alıntı geçerli, sayı başka koşumun",
        "dogrula": ["15 496 155"],
    },
    {
        "satir": ":412",
        "capa": "6. Açık uçlar > eski madde 1",
        "eski": "`-g 300` ortalama **+0,155**",
        "yeni": "**+0,181** — kilitli",
        "kaynak": 'K4 — Açığın ayar başına ayrıştırması | tablo satırı "anahtar kare aralığı (-g)"',
        "durum": "kilitle düzeltildi",
        "dogrula": ["+0,181"],
    },
    {
        "satir": ":412",
        "capa": "6. Açık uçlar > eski madde 1",
        "eski": "`-g 300` p10 **+0,333**",
        "yeni": "**+0,235** — kilitli",
        "kaynak": 'K4 — Açığın ayar başına ayrıştırması | tablo satırı "anahtar kare aralığı (-g)"',
        "durum": "kilitle düzeltildi",
        "dogrula": ["+0,235"],
    },
    {
        "satir": ":412",
        "capa": "6. Açık uçlar > eski madde 1",
        "eski": "boyut **%24,5** küçülüyor",
        "yeni": "aynı — boyut, VMAF ölçerinden bağımsız",
        "kaynak": 'K4 — Açığın ayar başına ayrıştırması | tablo satırı "anahtar kare aralığı (-g)"',
        "durum": "kilitten etkilenmez",
        "dogrula": [],
    },
    {
        "satir": ":414",
        "capa": "6. Açık uçlar > eski madde 1",
        "eski": "HandBrake **1,269 puan** önde",
        "yeni": "ortalama **+0,097**, p10 **+0,477**, harmonik **+0,099** — kilitli",
        "kaynak": 'K3 — AV1 ↔ HandBrake yeniden ölçüldü | tablo satırı "HandBrake açığı | T111 yeni (**kilitli**)"',
        "durum": "kilitle düzeltildi",
        "dogrula": ["+0,097", "+0,477", "+0,099"],
    },
    {
        "satir": ":419",
        "capa": "6. Açık uçlar > eski madde 1 notu",
        "eski": "hizalamanın p10 payı **+0,135**",
        "yeni": "aynı — bu sayı zaten T111'in kilitli ölçümü",
        "kaynak": 'K4 > Yerleşimin payı ölçüldü: sıfır değil, negatif | "ama p10\'da **+0,135**"',
        "durum": "güncel",
        "dogrula": ["+0,135"],
    },
    {
        "satir": ":448",
        "capa": "6. Açık uçlar > eski madde 3",
        "eski": "auto harmonik **56,313**, HandBrake **95,727** — aradaki ~39,4",
        "yeni": "kilitli harmonik açığı **+0,099**; harmonik artık ayrı bir eksen değil",
        "kaynak": 'K3 — AV1 ↔ HandBrake yeniden ölçüldü | tablo satırı "HandBrake açığı | T111 yeni (**kilitli**)"',
        "durum": "kilitle düzeltildi",
        "dogrula": ["+0,099"],
    },
    {
        "satir": ":450",
        "capa": "6. Açık uçlar > eski madde 3",
        "eski": "26 kare 1 puan altında, en düşük kare **0,00**",
        "yeni": "kilitli ölçümde 26 → **0**, en düşük kare 0,000 → **92,376**",
        "kaynak": 'Kilidin tek başına etkisi — aynı dosya, iki ölçüm | tablo satırı "`auto`"',
        "durum": "kilitle düzeltildi",
        "dogrula": ["92,376"],
    },
    {
        "satir": ":451",
        "capa": "6. Açık uçlar > eski madde 3",
        "eski": "kapı: **T111 kapanmadan bu maddeye yatırım yapılmamalı**",
        "yeni": "T111 `status: done` — kapı kaldırıldı, yerine sonucu yazıldı",
        "kaynak": ".claude/relay/contracts/done/T111.md | `status: done`",
        "durum": "kapı kaldırıldı",
        "dogrula": [],
    },
    {
        "satir": ":402",
        "capa": "6. Açık uçlar > öncelik uyarısı",
        "eski": "kapı: **T111 kapandığında sıra yeniden çizilmelidir**",
        "yeni": "T111 `status: done` — sıra bu sözleşmede yeniden çizildi",
        "kaynak": ".claude/relay/contracts/done/T111.md | `status: done`",
        "durum": "kapı kaldırıldı",
        "dogrula": [],
    },
    {
        "satir": ":411",
        "capa": "6. Açık uçlar > eski madde 1",
        "eski": '"Açığın hangi kısmını kapatması beklenir: **en büyüğü**"',
        "yeni": "hüküm betikten yeniden türetildi — H6",
        "kaynak": "tools/harita-tazeleme/harita-tazeleme.py | `hukum`",
        "durum": "yeniden türetildi",
        "dogrula": [],
    },
    {
        "satir": ":435",
        "capa": "6. Açık uçlar > eski madde 2",
        "eski": '"Puan açığına katkısı dolaylı ve muhtemelen küçük"',
        "yeni": "**düzeltilmemiş** — bu madde ne kilitten önce ne sonra ölçüldü",
        "kaynak": "yok — ölçüm yok",
        "durum": "düzeltilmemiş",
        "dogrula": [],
    },
    {
        "satir": ":447",
        "capa": "6. Açık uçlar > eski madde 3",
        "eski": '"Beklenen: p10 ve harmonik ortalamada kazanç, ortalamada az"',
        "yeni": "harmonik ayağı düştü — harmonik artık ayrı eksen değil; p10 ayağı "
                "duruyor ve **düzeltilmemiş**, ölçülmedi",
        "kaynak": 'K3 — AV1 ↔ HandBrake yeniden ölçüldü | tablo satırı "HandBrake açığı | T111 yeni (**kilitli**)"',
        "durum": "yarısı düştü, yarısı düzeltilmemiş",
        "dogrula": [],
    },
    {
        "satir": ":461",
        "capa": "6. Açık uçlar > eski madde 4",
        "eski": '"Beklenen: küçük"',
        "yeni": "**düzeltilmemiş** — bu madde hiç ölçülmedi",
        "kaynak": "yok — ölçüm yok",
        "durum": "düzeltilmemiş",
        "dogrula": [],
    },
    {
        "satir": ":469",
        "capa": "6. Açık uçlar > eski madde 5",
        "eski": '"T102\'nin kaynağında **sıfır**"',
        "yeni": "aynı — yapısal, `--crop-mode none` iki tarafta da; hiç ölçmediğimiz "
                "kaynak sınıfını T134 ölçüyor",
        "kaynak": ".claude/relay/contracts/T134.md | `status: active`",
        "durum": "kilitten etkilenmez",
        "dogrula": [],
    },
    {
        "satir": ":481",
        "capa": "6. Açık uçlar > eski madde 6",
        "eski": '"T102 açığına katkısı **sıfır**"',
        "yeni": "aynı — yapısal, kaynak taramalı değil",
        "kaynak": "yok — yapısal, ölçüm gerektirmiyor",
        "durum": "kilitten etkilenmez",
        "dogrula": [],
    },
    {
        "satir": ":491",
        "capa": "6. Açık uçlar > eski madde 7",
        "eski": '"puan açığına katkısı yok ya da hafif negatif"',
        "yeni": "**düzeltilmemiş** — ne puan ne süre ölçüldü",
        "kaynak": "yok — ölçüm yok",
        "durum": "düzeltilmemiş",
        "dogrula": [],
    },
    {
        "satir": ":84",
        "capa": "1. Hedef boyut yolu > İki geçiş nasıl kuruluyor",
        "eski": '"`hb_video_multipass_is_supported` **sıfır** dönüyorsa"',
        "yeni": "aynı — HandBrake kaynak mantığı, ölçüm sayısı değil",
        "kaynak": "yok — ölçüm değil, kaynak okuması",
        "durum": "tarama yanlış pozitifi",
        "dogrula": [],
    },
    {
        "satir": ":312",
        "capa": '4. Preset\'ler veri olarak | tablo satırı "`VideoPreset`"',
        "eski": "preset alanı değerleri `:332` / `:1012`",
        "yeni": "aynı — HandBrake preset verisi, ölçüm sayısı değil",
        "kaynak": "yok — ölçüm değil, kaynak okuması",
        "durum": "tarama yanlış pozitifi",
        "dogrula": [],
    },
    {
        "satir": ":344",
        "capa": "4. Preset'ler veri olarak > T102'nin `uzman-hb2` koşumu hangi preset'ti",
        "eski": "`uzman-hb2` komut satırı alıntısı",
        "yeni": "aynı — komut metni, kaynakta aynen duruyor; kilit komutu değiştirmez",
        "kaynak": 'K3 — Uzman açığı | tablo satırı "`uzman-handbrake`"',
        "durum": "alıntı geçerli",
        "dogrula": [],
    },
    {
        "satir": ":387",
        "capa": '5. Bizim tarafımız | tablo satırı "Anahtar kare aralığı"',
        "eski": "`FfmpegArguments.cs:162` — `-g = max(2, round(fps × 2))`, **sabit formül**",
        "yeni": "T98 (`8ea80c4`) dinamik aralığa çevirdi: `FfmpegArguments.KeyframeArgs`, "
                "taban 1 s, tavan sahne haritasından 5-10 s'ye kelepçeli",
        "kaynak": 'K6\'nın önerileri bugünkü `main`e karşı denetlendi | "Madde 1 — anahtar kare aralığını uzat: T98\'de uygulandı."',
        "durum": "kod durumu bayat — düzeltildi",
        "dogrula": [],
    },
    {
        "satir": ":406",
        "capa": "6. Açık uçlar > eski madde 1",
        "eski": '"`-g` bugün `fps × 2` sabiti"',
        "yeni": "aynı bayatlık; madde artık öneri değil, T98'de yapılmış iş",
        "kaynak": 'K6\'nın önerileri bugünkü `main`e karşı denetlendi | "Bu madde artık bir öneri değil, yapılmış bir iş."',
        "durum": "kod durumu bayat — düzeltildi",
        "dogrula": [],
    },
]


def oynayanlar():
    cikti = []
    for yeni_yer, m in enumerate(MADDELER, start=1):
        fark = m["eski"] - yeni_yer
        if abs(fark) >= 2:
            yon = "yukarı" if fark > 0 else "aşağı"
            cikti.append(f"eski {m['eski']}. madde {yeni_yer}. sıraya, {abs(fark)} basamak {yon}")
    return cikti


def hukumler():
    a, e, u, s = ACIK_YENI, ACIK_ESKI, UZMAN_ACIK, URETIM_SINIRI
    olculen = [m for m in MADDELER if m["olculdu"]]
    en_buyuk = olculen[0]
    yuruyen = [m for m in MADDELER if m["sozlesme"]]
    tasinan = oynayanlar()
    return [
        ("H1",
         f"Ölçülmüş HandBrake açığı bugün ortalamada {sayi(a['ort'], 3, True)}, p10'da "
         f"{sayi(a['p10'], 3, True)}, harmonikte {sayi(a['harmonik'], 3, True)}; bu "
         f"belgenin sırayı çizdiği eski ortalama açığı {sayi(e['ort'], 3, True)} idi, "
         f"yani {kat(a['ort'], e['ort'])} kat büyük okunmuş."),
        ("H2",
         f"Açığın ayakta kaldığı eksen p10: {sayi(a['p10'], 3, True)}, yeniden üretim "
         f"sınırının ({sayi(s['p10'])}) {kat(s['p10'], a['p10'])} katı. Ortalamadaki "
         f"{sayi(a['ort'], 3, True)} kendi sınırının ({sayi(s['ort'])}) "
         f"{kat(s['ort'], a['ort'])} katı — sıfır değil, ama p10'un "
         f"{kat(a['ort'], a['p10'])} katı küçüğü."),
        ("H3",
         f"Harmonik artık ayrı bir eksen değil: kilitli ölçümde harmonik açığı "
         f"({sayi(a['harmonik'], 3, True)}) ortalama açığından ({sayi(a['ort'], 3, True)}) "
         f"{sayi(abs(a['harmonik'] - a['ort']))} uzakta."),
        ("H4",
         f"Auto'nun bugünkü en büyük ölçülmüş açığı HandBrake'e karşı değil, kendi "
         f"uzman ayarlarımıza karşı: uzman açığı ortalamada {sayi(u['ort'], 3, True)}, "
         f"HandBrake açığının {kat(a['ort'], u['ort'])} katı; p10'da "
         f"{sayi(u['p10'], 3, True)}, {kat(a['p10'], u['p10'])} katı."),
        ("H5",
         f"Yedi maddenin {len(olculen)}'inde ölçülmüş bir etki var, "
         f"{len(MADDELER) - len(olculen)}'sında yok."),
        ("H6",
         f"En büyük ölçülmüş kalem eski {en_buyuk['eski']}. madde: p10'da "
         f"{sayi(en_buyuk['olculdu']['p10'], 3, True)}, HandBrake p10 açığının "
         f"({sayi(a['p10'], 3, True)}) %{round(en_buyuk['olculdu']['p10'] / a['p10'] * 100)}"
         f"'u; ortalamada {sayi(en_buyuk['olculdu']['ort'], 3, True)}, ortalama açığının "
         f"({sayi(a['ort'], 3, True)}) {kat(a['ort'], en_buyuk['olculdu']['ort'])} katı."),
        ("H7",
         "Yeni sıra, eski numaralarıyla: " + ", ".join(str(m["eski"]) for m in MADDELER) +
         f". {len(yuruyen)} madde yürüyen bir sözleşmeye değiyor — " +
         ", ".join(f"eski {m['eski']}. madde {m['sozlesme']}" for m in yuruyen) + "."),
        ("H8",
         f"{len(tasinan)} madde iki basamak oynadı: " + "; ".join(tasinan) + "."),
        ("H9",
         f"Ölçülmemiş altı maddenin beklentilerinden {len(olculmemis_maddeler())}'i "
         "düzeltilmemiş — karşılığı ne kilitten önce ne sonra ölçüldü: eski " +
         ", ".join(str(n) for n in olculmemis_maddeler()) + ". maddeler."),
    ]


def olculmemis_maddeler():
    numaralar = set()
    for b in BAYAT:
        if "düzeltilmemiş" not in b["durum"]:
            continue
        eslesme = re.search(r"eski madde (\d)", b["capa"])
        if eslesme:
            numaralar.add(int(eslesme.group(1)))
    return sorted(numaralar)


def normalize(metin):
    return re.sub(r"\s+", " ", metin)


GECISLER = [
    ("sayı/beklenti",
     re.compile(r"[0-9]+,[0-9]+|%[0-9]+|beklenir|Beklenen|sıfır|yatırım yapılmamalı|yeniden çizilmelidir")),
    ("kaynak alıntısı", re.compile(r"auto-mod\.md")),
    ("kod durumu iddiası", re.compile(r"fps × 2")),
]


def komut_tara():
    kunye = re.compile(r"`[^`]*\.(cs|c|h|json|axaml|sh|py)[^`]*`")
    bolum, alt = "", ""
    bulgu = 0
    for no, satir in enumerate(HARITA.read_text(encoding="utf-8").splitlines(), start=1):
        if satir.startswith("## "):
            bolum, alt = satir[3:].strip(), ""
        elif satir.startswith("### "):
            alt = satir[4:].strip()
        temiz = kunye.sub("<künye>", satir)
        etiketler = [ad for ad, desen in GECISLER if desen.search(temiz)]
        if not etiketler:
            continue
        capa = bolum + (" > " + alt if alt else "")
        bulgu += 1
        print(f"{no:>4} | {'/'.join(etiketler)} | {capa} | {satir.strip()[:80]}")
    print(f"\n{bulgu} satır, {len(GECISLER)} geçiş.")
    return 0


def hucre(metin):
    return metin.replace("|", "\\|")


def komut_bayat():
    print("| harita satırı | bölüm çapası | eski | düzeltilmiş | kaynak çapası | durum |")
    print("|---|---|---|---|---|---|")
    for b in sorted(BAYAT, key=lambda x: int(x["satir"].lstrip(":"))):
        print(f"| `{b['satir']}` | {hucre(b['capa'])} | {hucre(b['eski'])} | "
              f"{hucre(b['yeni'])} | § {hucre(b['kaynak'])} | {b['durum']} |")
    duzeltilmemis = [b for b in BAYAT if "düzeltilmemiş" in b["durum"]]
    print(f"\n{len(BAYAT)} iddia; {len(duzeltilmemis)}'i düzeltilmemiş "
          "(karşılığı ölçülmedi). Çok satıra yayılan iddia ilk satırında çapalandı; "
          "`tara` çıktısındaki 30 satırın tamamı bu tabloda bir satıra düşüyor.")
    print("Satır numaraları haritanın **T135 öncesi** hâline aittir (`890af6e`); "
          "bugünkü hâlde kaymışlardır — çapa bölüm başlığıdır, satır değil.")
    return 0


def komut_sira():
    print("| yeni | eski | madde | kayma | gerekçe | sözleşme |")
    print("|---|---|---|---|---|---|")
    for yeni_yer, m in enumerate(MADDELER, start=1):
        fark = m["eski"] - yeni_yer
        kayma = "değişmedi" if fark == 0 else f"{abs(fark)} basamak {'yukarı' if fark > 0 else 'aşağı'}"
        sz = f"{m['sozlesme']} ({m['sozlesme_durum']})" if m["sozlesme"] else "yok"
        print(f"| {yeni_yer} | {m['eski']} | {m['ad']} | {kayma} | {m['gerekce']} | {sz} |")
    return 0


def komut_hukum():
    for etiket, cumle in hukumler():
        print(f"{etiket}: {cumle}")
    return 0


def komut_dogrula():
    harita = normalize(HARITA.read_text(encoding="utf-8"))
    kaynak_ham = KAYNAK.read_text(encoding="utf-8")
    kaynak = normalize(kaynak_ham)
    basliklar = [s.lstrip("#").strip() for s in kaynak_ham.splitlines() if s.startswith("#")]
    bulgular = []

    for etiket, cumle in hukumler():
        if normalize(cumle) not in harita:
            bulgular.append(f"HÜKÜM YOK | {etiket} | haritada birebir bulunamadı")

    for b in BAYAT:
        for deger in b["dogrula"]:
            if deger not in kaynak:
                bulgular.append(f"KAYNAKTA YOK | {b['satir']} | {deger}")
            if deger not in harita:
                bulgular.append(f"HARİTADA YOK | {b['satir']} | {deger}")
        capa = b["kaynak"].split("|")[0].strip()
        if capa.startswith(("yok", ".claude", "tools")):
            continue
        ust = capa.split(">")[0].strip()
        if not any(ust in h for h in basliklar):
            bulgular.append(f"ÇAPA YOK | {b['satir']} | {ust}")

    for bulgu in bulgular:
        print(bulgu)
    print(f"\n{len(bulgular)} bulgu.")
    return 1 if bulgular else 0


def main():
    ayristirici = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ayristirici.add_argument("komut", choices=["tara", "bayat", "sira", "hukum", "dogrula"])
    ayristirici.add_argument("--surum", action="version", version=SURUM)
    args = ayristirici.parse_args()
    return {
        "tara": komut_tara,
        "bayat": komut_bayat,
        "sira": komut_sira,
        "hukum": komut_hukum,
        "dogrula": komut_dogrula,
    }[args.komut]()


if __name__ == "__main__":
    sys.exit(main())
