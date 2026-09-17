# B7 — HandBrake Dalga 2: Yedi Açığın Önce/Sonrası

Durum: **kod değişti, CI'da yeniden ölçüldü.** Kaynak açıklar dalga 1b'den (koşum **35158725446**, commit `02993179`,
B1–B6). Dal `t0/hb-2-aciklar`. Yerelde ağır kodlama yok; her sayı aşağıdaki koşumların yapıtlarından.

## Koşumlar

| Koşum | Etiket | Commit | Ölçtüğü |
|---|---|---|---|
| 35158725446 | `olcum-hb-handbrake+dusuk+social+bantlasma+turbo+hdr+ekranbant+vt--1` | `02993179` | önce (B1–B6) |
| 35166699260 | `olcum-hb-svtara+turboilk+socialkodek+vtara--1` | `78cd46d3` | araştırma kolları |
| 35170987175 | `olcum-hb-handbrake+dusuk+bantlasma+turbo+socialkodek+vt--2` | `8c481e45` | ürün yolu, açık 1/3/4/7 |
| 35173328586 | `olcum-hb-socialkodek+bantlasma--3` | `9312612c` (`d3330294` + main) | Social p4, taban tespiti |
| 35173566541 | `olcum-hb-vt--3` | `c2435429` | VT iki okumalı ölçer |
| 35177004568 | `olcum-hb-bantlasma--4` | `c10e78b2` | taban kuralı bayt artışı |

Danışmalar (verbatim): `docs/danisma/2026-09-17-hb2-soru-fable.md` / `hb2-yanit`, `hb2b-soru` / `hb2b-yanit`,
`hb2c-soru` / `hb2c-yanit`.

## Ölçer Düzeltmesi: CAMBI (i) Geçersiz

İki okuma kuruldu: (i) 8 bit kaynak 10 bit kaba konarak, (ii) çıktı dither'sız 8 bite indirilerek (zscale `dither=none`;
macOS'ta zscale yok, swscale `sws_dither=none`). Kontrol: kaynağın kendisi (i) **4,052**, (ii) **0,404**
(35166699260). (i) kendine eş kaynağı bantlı okuyor; CAMBI hükmü yalnız (ii) ile verilir.

- **B6'nın CAMBI cümleleri düşer.** `karanlik` 2000'de HB VT (ii) 8,96, ürün 9,06; B6'nın "+6,22" farkı eski
  ölçerin swscale dither'ıydı. 5500'de kalan fark gerçek ama küçük (HB 6,89, ürün 8,17). B6'nın XPSNR farkı ayakta.
- vt işinin düz `xpsnr` sütunu 10 bit çıktıda (i) gibi okur; VT hükümleri `xpsnr_ii` ile.
- Hangi izleyici için hüküm verildiği (dither'sız 8 bit panel mi, libmpv'nin dither'lı çıkışı mı) ürün kararı, açık.

## Açık 1 — VideoToolbox Yazılım Gibi Planlanıyordu

**Kök neden.** `CodecModel.IsHardware` VT'yi donanım saymıyordu; ürün `-pass 2 -passlogfile` ve `-preset slow`
yazıyordu, ikisi de `hevc_videotoolbox`ta karşılıksız. **Düzeltme** (`b07ed823`, `8c481e45`): VT tek geçiş, `-preset`
yok, SDR'de `-pix_fmt p010le -profile:v main10`; `-maxrate` tavanı kalır. Testler `CodecModelTests`,
`UretimYoluTests`. Negatif kontrol: `-foo 1` çıkış kodu 8 (35170987175 ve 35173566541).

35173566541, 8 hücrenin 8'i `Main 10`. Ürüne karşı (35166699260'ın 8 bit ürün satırı), okuma (ii):

| Kesit | kbit | ΔVMAF-NEG | ΔXPSNR(ii) | Δkbps | Kapı (≥ −0,3 / ≥ −0,2) |
|---|---|---|---|---|---|
| karanlik | 2000 | +0,02 | −0,231 | −%0,19 | **kaldı** (0,03 dB) |
| karanlik | 5500 | +0,24 | +0,035 | +%2,48 | ölçülmedi (±%2 dışı) |
| parlak | 2000 | +1,35 | +0,314 | +%1,18 | geçti |
| parlak | 5500 | +0,82 | +0,443 | −%5,67 | ölçülmedi (bant altı) |
| hareketli | 2000 | +0,10 | −0,051 | −%0,07 | geçti |
| hareketli | 5500 | +0,17 | +0,310 | −%1,03 | geçti |
| ekran | 2000 | −0,19 | −0,179 | — | bilgi |
| ekran | 5500 | −0,19 | −0,291 | — | bilgi |

HB VT'ye karşı, aynı koşum, okuma (ii):

| Kesit | kbit | ΔVMAF-NEG | ΔXPSNR(ii) | ΔCAMBI(ii) | Çıplak kodlama sn ürün / HB |
|---|---|---|---|---|---|
| karanlik | 2000 | +3,10 | −0,213 | +0,81 | 4,3 / 6,8 |
| karanlik | 5500 | +0,35 | −0,216 | +1,61 | 8,3 / 6,1 |
| parlak | 2000 | +1,28 | −0,328 | +0,003 | 8,1 / 7,2 |
| parlak | 5500 | +0,73 | −0,119 | — | 6,7 / 6,4 |
| hareketli | 2000 | +2,58 | −0,012 | +0,21 | 3,9 / 6,7 |
| hareketli | 5500 | +0,17 | −0,218 | +0,06 | 3,3 / 5,9 |
| ekran | 2000 | +7,77 | +8,04 | −0,53 | 3,3 / 3,1 |
| ekran | 5500 | +8,86 | +9,32 | −0,74 | 3,0 / 3,0 |

- Film hücrelerinde XPSNR(ii) açığı toplamı −2,04 dB'den (8 bit ürün, 35166699260) **−1,11 dB**'e indi; VMAF-NEG 6
  hücrenin 6'sında HB VT'nin önünde.
- HB eşitlik kapısı (XPSNR ≥ −0,2): film hücrelerinde **2/6** (hareketli 2000, parlak 5500), taban 2/6; azalmadı.
  parlak 5500 ürünü bant altında, bu hücre şüpheli.
- `ekran` iki hücrede HB VT'nin ≥ +0,3 önünde (kapı geçti). Çıplak kodlama en kötü 1,36× (karanlik 5500), ≤ 1,5×.
- Ürünün toplam süresi 31,7–53,3 sn; kodlamanın 3–8 sn'sine sondalar ekleniyor. Açık.

## Açık 2 — SVT-AV1 Karanlık Bantlaşma

**Açık kaldı.** 35166699260 `svtara`, `karanlik` 600 ve 2000, eş bayt, CAMBI (ii):

| Kol | 600 | 2000 |
|---|---|---|
| e0 8 bit (ürün) | 9,309 | 9,392 |
| 10bit | 9,151 | 9,255 |
| 10bit-qm | 9,227 | 9,309 |
| 10bit-vb1 | 9,172 | 9,379 |
| 10bit-tf0 | 9,011 | 9,113 |
| 10bit-grain8 | 0,005 | 0,004 |
| HB x265 | 6,479 | 6,491 |
| negatif uydurma anahtar | 9,302 | 9,389 |

10 bit ve dört ayar CAMBI(ii)'yi 9,01–9,38 aralığında bıraktı; eski ölçerin 10 bit 3,23'ü swscale dither'ıydı.
grain8 bantlaşmayı örtüyor ama kodlama 3× (77,2 / 25,5 sn) ve VMAF-NEG −0,84 (600); reddedildi. Negatif kontrol
`vidshrinkuydurma=1`: "Error parsing option vidshrinkuydurma" uyarısı. HB x265'in 6,48'i gerçek bir kodlayıcı farkı.

## Açık 3 — Otomatik Planda Kare Hızı Düşürme

**Kök neden.** `PlanCalculator` düşük bütçede kaynak fps'te çalışabilir bir ölçek varken de 16 fps'e iniyordu. **Düzeltme** (`fb764674`):
fps ancak kaynak fps'te hiçbir ölçek çalışabilir bit hızını geçmediğinde düşer. Test `OtomatikFpsTests`; negatif
kontrol: bütçe çalışabilir tabanın altındayken fps yine düşer.

| Hücre | Koşum | Geometri | VMAF-NEG ort / harm | HB ort / harm |
|---|---|---|---|---|
| hareketli 300 önce | 35158725446 | 1920x818@16 | 49,14 / 3,96 | B2: ürün −5,78 / −49,09 |
| hareketli 300 sonra | 35170987175 | 1804x768@24 | 67,38 / 65,69 | 54,44 / 52,49 |

Sonra `dusuk` işinde 12 ürün satırının 12'si ilk denemede bantta, 24 fps; `urun-otomatik` 6 hücrenin 6'sında HB'nin
önünde (+2,54 ile +12,94 VMAF-NEG ort).

## Açık 4 — x265 Turbo İlk Geçiş

**Kök neden.** Turbo ilk geçişte ön ayarı düşürüp ikinci geçişe zayıf istatistik bırakıyordu. **Düzeltme**
(`f6b0e35f`): ilk geçiş ön ayarı korur, `slow-firstpass=0`. Test `TurboFirstPassTests`, `TurboTavanTests`.

35170987175, `turbo` işi, turbo / turbosuz:

| Kesit | kbit | VMAF-NEG | Kodlama sn |
|---|---|---|---|
| hareketli | 600 | 77,28 / 77,23 | 61,8 / 74,4 |
| hareketli | 2000 | 96,79 / 96,80 | 73,2 / 102,8 |
| karanlik | 600 | 76,65 / 76,06 (+%1,9 kbps) | 49,0 / 86,0 |
| karanlik | 2000 | 95,68 / 95,66 | 72,8 / 96,4 |

Önce (B5) turbo hareketli 600'de −1,84 VMAF-NEG kaybettiriyordu; sonra +0,05. HB turbosuna karşı ürün turbo
hareketli 600 77,28 / 77,24, hareketli 2000 96,79 / 96,47, karanlik 2000 95,68 / 95,16; karanlik 600 76,65 / 76,30
eş bayt değil (+%1,9).

## Açık 5 — Düşük Hedef ve Doygunluk

**Kök neden.** Kodlayıcı tabanında istek düşse de bayt inmiyor, ürün aynı düzende yeniden deniyor ya da bant
dışında kabul ediyordu. **Düzeltme** (`9ee64e43`, `d3330294`, `c10e78b2`) `Saturation`:

- Taban: iki örnek tavan üstü, istek ≤ 0,80×, bayt 0,95×–1,01× arasında. Referans ardışık 2-pass koşusunun en eski
  eşleşen örneği (`FloorReference`). Tabanda düzen iner, sonra ses, +1 deneme.
- Ölü verim (< 0,5): bölme ya da 2×, sonra `Saturated`.
- 1,01 üst sınırı 35173328586 `rampa` 1200 `e0-duzen`'deki yanlış pozitiften: 1174k→1,931 MB, 855k→2,005 MB (bayt
  +%3,8) taban sayıldı, düzen 1558x876'ya indi, teslim %8,3.

Testler `SaturationTests` (14), `TasmaKarariTests` (gerçek ffmpeg, iz dalı). Mutasyon kontrolü: `FloorReference`
yalnız son örneğe bakınca en eski örnek testi, 1,01 satırı silinince artış testi kırıldı.

35173328586, `bantlasma` `karanlik`: `e0-duzen` 100 1 denemede bantta, 1036x442, 0,118 MB (tavan 0,122); 300 ve
1200 hücreleri 1 deneme bantta. `e0` 100 (düzen kapalı, ses yok) yapısal olarak dosyasız.

35177004568 (`c10e78b2`), danışma hb2c ölçütlerine karşı:

| Hücre | Ölçüt | Sonuç | Hüküm |
|---|---|---|---|
| karanlik 100 `e0-duzen` | ≤ 0,117 MB, bantta, 1 deneme, yükseklik < 818 | 0,1179 MB (tavan 0,1221), bantta, 1 deneme, 1036x442 | boyut ölçütü 0,0009 MB aştı; diğerleri geçti |
| karanlik 100 `e0` | dosya yok, 3 deneme, taban izi yok | 98k/84k/74k → 0,137/0,134/0,134 MB, dosya yok, taban izi yok | geçti |
| karanlik 300 `e0` / `e0-duzen` | 1 deneme, bantta, e0 1920x818 | 1 deneme bantta, 1920x818 / 1842x784 | geçti |
| karanlik 1200 üç kol | 1 deneme | 3 kolun 3'ü 1 deneme bantta | geçti |
| rampa 1200 `e0-duzen` | taban izi yok, `Saturated` yok, ≤ 3 deneme, "did not answer" yok | 1174k/1102k/908k → 1,497/1,707/1,522 MB, 3 deneme, dört iz koşulu tuttu | geçti |
| rampa 1200 `e0-duzen` | teslim ≥ 0,703 MB | dosya yok (tavan 1,4648, üç deneme tavan üstü) | **kaldı** |

Rampa 1200'de 908k örneği 1174k'ya göre 0,77× istekte 1,017× bayt verdi; 1,01 sınırı bunu taban saymadı, düzen
inmedi. Ama SVT oran denetimi bu yapay kaynakta aynı 1174k istekte 1,931 MB (35173328586) ve 1,497 MB (35177004568)
üretiyor, deneme bütçesi tavanın altına inmeye yetmedi. Aynı koşumda düzen kapalı `e0` 3 denemede 1,109 MB (%76)
"under band accepted" teslim etti.

## Açık 6 — Bench Özeti Planlanan Kipi Yazıyordu

**Düzeltme** (`fb764674`): özet satırı teslim edilen denemenin kipini `EncodeResult.PlanUsed`'dan yazar
(`tools/VidShrink.Bench/TeslimOzeti.cs`, test `TeslimOzetiTests`, negatif kontrol: ilk denemede teslimde özet
planla aynı). CI ayrıca `--intent`'in Bench'te plana geçmediğini yakaladı (`d3330294`, `Intent = intent`).

## Açık 7 — Social 1080p60 Eksik Dolum

**Kök neden** iki parça: SVT preset 6 1080p60'ta HB'nin gerisinde (B2 parlak); ürünün etkin hedefi istenen MB'ın ~%96'sı.
**Düzeltme** (`8c481e45`): Social niyetinde SVT preset 4. Test `SosyalOnAyarTests` (diğer profiller p6).

35173328586 `socialkodek`, HB'ye karşı ΔVMAF-NEG / ΔXPSNR:

| Kol | parlak | karanlik | hareketli |
|---|---|---|---|
| urun-sosyal (p4) | −0,35 / −0,10 (−%4,82 kbps) | +0,28 / +0,02 (−%3,80) | +0,19 / +0,22 (−%5,22) |
| urun-otomatik (p6) | −0,70 / −0,48 (−%4,25) | +0,17 / −0,25 | +0,15 / −0,14 |
| svt-p4 eş bayt | −0,24 / −0,01 (+%2,93) | +0,30 / +0,01 (+%0,18) | +0,28 / +0,35 (+%1,45) |
| negatif yarım bit | −2,66 / −1,51 | −1,12 / −1,69 | −0,69 / −1,33 |

- Önce (B2) parlak −0,64 / −0,45; sonra ürün yolu −0,35 / −0,10, %3,8–5,2 daha az baytla.
- 12 SVT satırının 12'sinde günlükte preset okundu. SVT'nin ilk geçişi preset satırına "Pass 1" basıyor; kapı ikinci
  geçişi okur. Negatif: uydurma anahtar "Error parsing option vidshrinkuydurma".
- Süre: urun-sosyal parlak 2 deneme, toplam 135,6 sn (HB 25,9); karanlik 82,6, hareketli 80,9 sn, 1 deneme.
- Kalan açık %96 dolum payı; merkez hedef kuralı (danışma hb2b Soru C.3) değişmedi.

## İlk Denemede Bantta

35170987175'in 51 ürün satırından **35**'i ilk denemede bantta: bantlasma 4/9, dusuk 12/12, handbrake 6/8,
socialkodek 2/6, turbo 8/8, vt 3/8.

## Ölçülmedi ve Açık Kalanlar

- Açık 2: CAMBI(ii)'yi oynatan SVT ayarı bulunmadı.
- Açık 5: rampa 1200 `e0-duzen` dosyasız; tavan üstü üç denemeden sonra bütçe büyütülmedi (danışma hb2c Soru 2). Ürün yolunda taban adımını tetikleyen gerçek kaynak ölçülmedi.
- VT: karanlik 2000 XPSNR(ii) −0,231 (kapı −0,2); HB kapısı 2/6; ekran eski ürüne −0,19; sonda maliyeti 31–53 sn.
- Social: ~%96 dolum payı ve yeniden denemede 135,6 sn; p4'ün Social dışına genellenmesi (dalga 2 kolu) ölçülmedi.
- SVT 2-pass ve VT deterministik değil; md5 kanıtı yerine günlük satırı ve uydurma anahtar negatifi kullanıldı.
