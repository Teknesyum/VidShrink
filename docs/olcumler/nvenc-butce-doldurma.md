# NVENC'te Bütçe Doldurma Neden Kapalı Kalıyor

19 Eylül 2026. Makine: RTX 5070 Ti, ffmpeg 9.0. Kaynak: Sintel 1080p, 600. saniyeden
10 sn FFV1 kesit. Betikler `.calisma/nvenc-butce/`.

`docs/olcumler/nvenc-2.md` ürünün ortalama **%4,79 bütçe bıraktığını** yazıyor ve açık
hücrelerde "boş bütçe yukarı denemesi NVENC'te kapıda kaldı (tek denemeyle %3 penceresine
sığmıyor)" diyor. O cümle ölçülmemiş bir gerekçeydi; burada ölçüldü.

`BudgetFill.Plan` NVENC'e hiç bakmıyor: `src/VidShrink.Core/BudgetFill.cs:23` yazılım
dışındaki her satıcıda `null` dönüyor. Sorulan soru, o satırın kalkıp kalkamayacağı.

## Birinci ölçüm: NVENC istenen bit hızını izliyor mu

hevc_nvenc p4, 1882x802, `-rc vbr -multipass fullres -g 120`, tepe ve tampon 2,0x.

| istenen | teslim | sapma |
|---|---|---|
| 900 | 996,3 | +10,70% |
| 950 | 1012,7 | +6,60% |
| 975 | 1006,0 | +3,18% |
| 1000 | 1004,6 | +0,46% |
| 1010 | 1012,1 | +0,20% |
| 1025 | 1025,2 | +0,02% |
| 1050 | 1143,7 | +8,93% |
| 1100 | 1166,5 | +6,04% |
| 1200 | 1293,0 | +7,75% |
| 1400 | 1472,2 | +5,15% |
| 1700 | 1771,6 | +4,21% |
| 2000 | 2088,1 | +4,41% |
| 3500 | 3673,9 | +4,97% |

İki şey çıkıyor. **900–1025 arası bir taban:** ne istenirse istensin teslim ~1005–1025
kbit'te duruyor, yani oradaki sapma hız denetiminin hatası değil, kodlayıcının o
geometride inebildiği alt sınır. Tabanın üstünde teslim istenenin **üstünde** kalıyor,
çarpan %4–8 ve bit hızı büyüdükçe %4,2–5,0'a oturuyor.

Ölçüm bit birebir tekrarlanabilir: 1000/1050/1100 üçlüsü üç turda aynı bayta düştü
(+0,46 / +8,93 / +6,04). Sıçrama gürültü değil.

## İkinci ölçüm: orantılı düzeltme pencereye oturuyor mu

Bütçe doldurmanın yaptığı şey bu: tabandaki teslimden oranla yeni bir istek hesapla
(`scaledK = baseK * aim / delivered`), bir kez daha kodla. İki kodek, iki geometri,
dört taban/hedef çifti — 16 düzeltme, her biri gerçek ikinci kodlama.

| kodek | geo | taban | teslim | hedef | düzeltilmiş istek | son | hedefe sapma |
|---|---|---|---|---|---|---|---|
| hevc | 1882x802 | 1200 | 1293,0 | 1500 | 1392 | 1463,0 | −2,47% |
| hevc | 1882x802 | 1600 | 1637,2 | 2000 | 1954 | 2071,0 | +3,55% |
| hevc | 1882x802 | 2200 | 2276,5 | 2700 | 2609 | 2729,4 | +1,09% |
| hevc | 1882x802 | 3000 | 3173,2 | 3600 | 3403 | 3638,7 | +1,07% |
| hevc | 1382x588 | 1200 | 1224,9 | 1500 | 1469 | 1539,6 | +2,64% |
| hevc | 1382x588 | 1600 | 1654,0 | 2000 | 1934 | 2041,6 | +2,08% |
| hevc | 1382x588 | 2200 | 2325,5 | 2700 | 2554 | 2666,5 | −1,24% |
| hevc | 1382x588 | 3000 | 3143,1 | 3600 | 3436 | 3586,5 | −0,38% |
| av1 | 1882x802 | 1200 | 1269,1 | 1500 | 1418 | 1440,0 | −4,00% |
| av1 | 1882x802 | 1600 | 1586,5 | 2000 | 2017 | 2031,1 | +1,55% |
| av1 | 1882x802 | 2200 | 2188,6 | 2700 | 2714 | 2761,4 | +2,27% |
| av1 | 1882x802 | 3000 | 3074,5 | 3600 | 3512 | 3554,9 | −1,25% |
| av1 | 1382x588 | 1200 | 1208,9 | 1500 | 1488 | 1475,8 | −1,61% |
| av1 | 1382x588 | 1600 | 1574,8 | 2000 | 2032 | 2054,7 | +2,73% |
| av1 | 1382x588 | 2200 | 2227,5 | 2700 | 2666 | 2675,5 | −0,91% |
| av1 | 1382x588 | 3000 | 3021,9 | 3600 | 3573 | 3630,7 | +0,85% |

n=16, ortalama **+0,37%**, standart sapma **2,14**, aralık **−4,00 .. +3,55**.

## Hüküm: kapı kapalı kalıyor

> 22 Eylül 2026: kapı `nvenc-4-teslim.md`'de yeniden ölçülüp NVENC için 0,97 nişanıyla açıldı.
> Aşağıdaki hesap yukarı denemenin ortalama inişine bakıyor; `Keeps` ise ikisinin büyüğünü teslim ediyor.

Yukarı deneme hedefi aşarsa `BudgetFill.Keeps` onu atıyor, yani aşım kalite kaybı değil
**boşa giden deneme**. Bugünkü nişan `Aim = 0,985`. Ölçülen yayılım o nişanla ne yapıyor:

| nişan | hedefi aşan | inişin indiği aralık |
|---|---|---|
| 0,985 (bugün) | 6/16 | 0,946 .. 1,020 |
| 0,97 | 1/16 | 0,931 .. 1,004 |
| 0,95 | 0/16 | 0,912 .. 0,984 |

0,985'te denemelerin **çoğu boşa gidiyor**. Nişanı 0,95'e indirmek aşımı bitiriyor ama
inişi 0,912–0,984 aralığına, yani **ortalama 0,95'e** oturtuyor — ürünün NVENC'te bugün
zaten teslim ettiği yere (`nvenc-2.md`: −%4,79). Kazanç yok.

Sebep tek cümlede: **NVENC'in teslim yayılımı (±%3,5) kapatılmaya çalışılan bütçe
boşluğu (%4,8) kadar büyük.** Tek düzeltme adımı sabit bir çarpan varsayıyor; NVENC'te
çarpan hücreden hücreye %4,2 ile %7,8 arasında geziniyor ve o gezinme düzeltmenin
kendisini yiyor.

`BudgetFill.cs:23`'teki yazılım kapısı duruyor. Kaldırmanın şartı, tek adımlı orantı
yerine iki ölçüm noktasından yerel eğim uyduran bir yaklaşım; o da `ExtraAttempts = 1`
sözünü bozar, yani ayrı bir karar. Bu ölçüm o kararın öncülüdür, kendisi değil.

Tabanın altı ayrıca kapalı kalmalı: 1882x802'de hevc ~1005 kbit'in altına inmiyor, o
yüzden taban altındaki hedeflerde hiçbir istek tutmaz. Bugün bunu
`PlanCalculator.RunnableVideoBitrateK` kapısı zaten yapıyor.
