# NVENC 4: Hedef Altı Teslimin Kaynağı Ve Bütçe Doldurma

Makine RTX 5070 Ti, ffmpeg 9.0. 22 Eylül 2026. Düzenek `nvenc-3-lookahead-handbrake.md` ile
aynı üç 10 sn FFV1 kesit (`karanlik`, `parlak`, `hareketli`), iki kodek (hevc_nvenc,
av1_nvenc), üç hedef (1000 / 2000 / 3500 kbit → 1,22 / 2,44 / 4,27 MB). Betik
`tools/nvenc-4/teslim.ps1`, ham veri `docs/olcumler/nvenc-4-ham.json`. VMAF-NEG
`VidShrink.Bench measure-pair` ile, baytı tabanla aynı çıkan hücre yeniden ölçülmedi.

Taban kolunun 18 dosyası `nvenc-3-ham.json`'daki baytlarla **birebir aynı** çıktı; NVENC
bu düzenekte belirlenimci, kollar arasındaki fark gürültü değil.

## Hedef altı teslim nereden geliyor

`nvenc-3`'ün günlüklerinden (`.calisma/nvenc-3/urun-*.log`) deneme izi okundu:

| Yol | Hücre | Teslim / hedef | Neden |
|---|---|---|---|
| İlk deneme bantta kabul | 5 (karanlık av1 ×3, karanlık hevc 1000, hareketli av1 2000) | 0,922 .. 0,996 | Bant alt sınırı 10 MB altında hedefin 0,92'si; NVENC'in karanlık av1'de verimi <1, bant içinde kalınca yukarı deneme yok |
| Tavan aşımı → düzeltme | 12 | 0,957 .. 0,999 | `PlanCalculator.RetryAimMb` ölçülen verimle **bant merkezine** nişan alıyor: 10 MB altında hedefin 0,96'sı |
| İki aşım → tavan koruması | 1 (parlak hevc 1000) | 0,866 | `CeilingGuard.Aim = 0,90` ve en kötü verime bölme |

İlk denemenin aşımı hedefe göre büyük: parlak hevc +%36,9 / +%28,4 / +%25,9, parlak av1 +%20,0 / +%17,7 / +%18,2
(ilk istek, tepe ve tampon 2,0x, `-rc vbr -multipass fullres`, lookahead açık). Aşımın yol açtığı
düzeltme denemesi nişanın **%0,4 altı ile %6,5 üstü** arasına iniyor (13 düzeltmede); bu
yayılım `nvenc-butce-doldurma.md`'nin ölçtüğü ±%3,5 ile aynı mertebe. Yani ortalama −%3,6'nın
büyük kısmı NVENC hız denetiminin değil, **küçük hedefte bant merkezinin (0,96)** payı; NVENC'in
katkısı bu merkezi daha yukarı almayı güvensiz kılan yayılım. 50 MB üstü hedefte merkez 0,986,
10–50 MB'ta 0,975 — bu kesitler o aralığı ölçmüyor.

## Adaylar

Hepsi aynı değişiklik: `BudgetFill.Plan`'ın NVENC kapısını açmak, yalnız nişan farklı.
Teslim hedefin 0,97'sinin altındaysa bir yukarı deneme; hedefi aşar ya da küçük çıkarsa atılır,
önceki dosya teslim edilir (`BudgetFill.Keeps`). Hedef sözü yapı gereği korunuyor. Tavan üstü
örnek varsa istek iki ölçüm arasında aradeğerle sınırlanıyor (yerel eğim).

| Kol | Ort. teslim | En kötü | Hedefi aşan | Değişen hücre | Yukarı deneme / boşa giden | VMAF-NEG ort farkı (18 hücre) | p10 farkı (değişen hücreler ort / en kötü) |
|---|---|---|---|---|---|---|---|
| taban | 0,9637 | 0,8656 | 0 | – | – | – | – |
| nişan 0,97 | **0,9712** | **0,8847** | 0 | 8 | 10 / 2 | +0,054 | +0,237 / −0,01 |
| nişan 0,985 | 0,9736 | 0,8656 | 0 | 7 | 10 / 4 | +0,056 | +0,164 / −0,01 |
| nişan 1,00 | 0,9662 | 0,8656 | 0 | 3 | 10 / 7 | +0,025 | +0,190 / +0,05 |

## Hücreler

Taban: teslim / deneme / VMAF-NEG ort / p10. Adaylar: teslim / deneme / VMAF-NEG ort.

| Kesit | Kodek | kbit | Taban | 0,97 | 0,985 | 1,00 |
|---|---|---|---|---|---|---|
| karanlik | hevc | 1000 | 0,9868 / 1 / 86,10 / 79,79 | 0,9868 / 1 / 86,10 | 0,9868 / 1 / 86,10 | 0,9868 / 1 / 86,10 |
| karanlik | hevc | 2000 | 0,9588 / 2 / 94,06 / 89,64 | 0,9596 / 3 / 94,07 | 0,9657 / 3 / 94,11 | 0,9706 / 3 / 94,18 |
| karanlik | hevc | 3500 | 0,9624 / 2 / 97,94 / 94,83 | 0,9684 / 3 / 97,96 | 0,9751 / 3 / 97,98 | 0,9624 / 3 / 97,94 |
| karanlik | av1 | 1000 | 0,9428 / 1 / 86,95 / 81,42 | 0,9492 / 2 / 87,00 | 0,9492 / 2 / 87,00 | 0,9590 / 2 / 87,18 |
| karanlik | av1 | 2000 | 0,9220 / 1 / 95,31 / 90,63 | 0,9886 / 2 / 95,88 | 0,9940 / 2 / 95,92 | 0,9220 / 2 / 95,31 |
| karanlik | av1 | 3500 | 0,9660 / 1 / 98,56 / 95,61 | 0,9686 / 2 / 98,57 | 1,0000 / 2 / 98,64 | 0,9660 / 2 / 98,56 |
| parlak | hevc | 1000 | 0,8656 / 3 / 83,60 / 67,37 | 0,8847 / 4 / 83,83 | 0,8656 / 4 / 83,60 | 0,8656 / 4 / 83,60 |
| parlak | hevc | 2000 | 0,9566 / 2 / 90,63 / 84,18 | 0,9566 / 3 / 90,63 | 0,9566 / 3 / 90,63 | 0,9566 / 3 / 90,63 |
| parlak | hevc | 3500 | 0,9694 / 2 / 93,98 / 91,08 | 0,9694 / 3 / 93,98 | 0,9783 / 3 / 94,04 | 0,9859 / 3 / 94,08 |
| parlak | av1 | 1000 | 0,9835 / 2 / 86,97 / 75,44 | 0,9835 / 2 / 86,97 | 0,9835 / 2 / 86,97 | 0,9835 / 2 / 86,97 |
| parlak | av1 | 2000 | 0,9752 / 2 / 91,96 / 87,19 | 0,9752 / 2 / 91,96 | 0,9752 / 2 / 91,96 | 0,9752 / 2 / 91,96 |
| parlak | av1 | 3500 | 0,9715 / 2 / 94,69 / 92,01 | 0,9715 / 2 / 94,69 | 0,9715 / 2 / 94,69 | 0,9715 / 2 / 94,69 |
| hareketli | hevc | 1000 | 0,9993 / 2 / 89,29 / 80,60 | 0,9993 / 2 / 89,29 | 0,9993 / 2 / 89,29 | 0,9993 / 2 / 89,29 |
| hareketli | hevc | 2000 | 0,9707 / 2 / 96,38 / 89,88 | 0,9707 / 2 / 96,38 | 0,9707 / 2 / 96,38 | 0,9707 / 2 / 96,38 |
| hareketli | hevc | 3500 | 0,9593 / 2 / 98,69 / 95,24 | 0,9903 / 3 / 98,77 | 0,9965 / 3 / 98,80 | 0,9593 / 3 / 98,69 |
| hareketli | av1 | 1000 | 0,9955 / 2 / 90,58 / 81,28 | 0,9955 / 2 / 90,58 | 0,9955 / 2 / 90,58 | 0,9955 / 2 / 90,58 |
| hareketli | av1 | 2000 | 0,9959 / 1 / 97,26 / 90,96 | 0,9959 / 1 / 97,26 | 0,9959 / 1 / 97,26 | 0,9959 / 1 / 97,26 |
| hareketli | av1 | 3500 | 0,9659 / 2 / 98,86 / 95,97 | 0,9670 / 3 / 98,87 | 0,9659 / 3 / 98,86 | 0,9659 / 3 / 98,86 |

## Hüküm: nişan 0,97 ile kapı açıldı

Üç kabul şartı üç kolda da tuttu (hedefi aşan hücre yok, ortalama yukarı, VMAF-NEG ortalaması
düşmedi). 0,97 seçildi: ortalamada 0,985'ten 0,24 puan geride ama **en kötü hücreyi
kaldıran tek kol** (parlak hevc 1000, 0,8656 → 0,8847) ve on yukarı denemenin yalnız ikisi boşa
gidiyor (0,985'te dört, 1,00'da yedi). p10'da −0,01'lik tek hücre VMAF-NEG'in iki basamaklı
yuvarlamasının içinde; aynı hücrede ortalama +0,02.

Bedel: 18 hücrenin 10'unda bir kodlama daha. Süre bu koşumda ölçülmedi; `nvenc-3` günlüğünde hedef başına
toplam kodlama 0,9–3 sn.

`nvenc-butce-doldurma.md` kapıyı "iniş ortalaması tabanla aynı" diye kapalı tutmuştu. O hesap
yukarı denemenin **ortalama inişine** bakıyordu; `Keeps` ise yalnız hedefin altında ve öncekinden
dolu olanı teslim ettiği için kazanç inişlerin ortalaması değil, taban ile inişin büyüğü. Bu koşum
onu ölçtü.

Kalan açık: 0,97 kolunda 8 hücre hâlâ 0,97'nin altında. Düzeltme denemesi 0,96'ya
nişanlanıyor; yukarı deneme ya 0,97 eşiğinde tetiklenmiyor ya da aşıp atılıyor (parlak hevc 2000). Küçük hedefte bant merkezini NVENC için yukarı almak
ayrı bir karar; bu ölçüm onu sınamadı.

## Sonraki Değişiklik (23.09.2026)

Bu belgedeki `RetryAimMb` davranışı (ölçülen verimle bant merkezine, 10 MB altında 0,96·T) artık geçerli değil: nişan `BudgetFill.Aim`·T (0,985·T). Yazılım kodlayıcı ızgarasındaki ölçüm `retry-nisan-dolum.md`'de. NVENC satırları yeniden ölçülmedi.

NVENC yukarı denemesinin nişanı da 0,97'den 0,985'e çekildi (23.09.2026): aynı ızgarada ortalama 0,9750 → 0,9780, hedefi aşan teslim 0/18. Ölçüm d1-donanim-dolum.md'de.
