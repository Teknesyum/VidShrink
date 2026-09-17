# HandBrake Dalga 2 — İkinci Danışma Sorusu

Tarih: 17 Eylül 2026. Önceki yanıtın (`.calisma/danisma/hb2-yanit.md`) kurallarıyla koşum 35166699260 ölçüldü.
Tablolar (hepsi CI, macos-15 VT; ubuntu diğerleri):

- `.calisma/hb2/vt-tablo.txt` — Soru 6 kolları, 4 kesit × 2000/5500, HB VT ile.
- `.calisma/hb2/svt-karanlik-tablo.txt` — Soru 1 kolları, `karanlik` 600/2000, ölçer kontrol satırları başta.
- `.calisma/hb2/turbo-tablo.txt` — Soru 2 (uygulandı, soru yok; bilgi).
- Social (Soru 5) sayıları aşağıda.

Ham JSON: `.calisma/hb2/r2/hb-sonuc-*/`. `rampa` svtara işi hâlâ koşuyor.

## Uygulananlar (kurala göre, soru değil)

- Turbo: yalnız `slow` + `slow-firstpass=0` dört hücrenin hepsini geçti (ΔVMAF-NEG −0,02…+0,22, ΔXPSNR ≥ −0,04,
  süre %29-33 kısa). `ilk-medium` `karanlik` 600'de %14 (ikinci geçiş 47,5 sn'ye çıktı, koşucu gürültüsü olabilir)
  ile düştü. Ürüne girdi: x265 turbo ön ayar düşürmez, ilk geçişe `slow-firstpass=0`. Negatif: `foo=1` Unknown option,
  `slow-firstpass=1` tabanla md5-eş.
- VT tek geçiş ve `-preset` yazılmaması ürüne girdi (yanıt 6b kararıydı). Kanıt: `negatif-preset-olu` "not used"
  uyarısı bulundu; `vt-8bit-tek` bugünkü ürünle eş bayt hücrelerde gürültü içinde, kodlama 5,1 → 2,6 sn.

## Soru A — Ölçer kontrolü (i) okumada tutmadı

Kaynak kendine: (i) `format=yuv420p10le` yolu CAMBI **4,05**, (ii) `zscale dither=none` **0,404** (B3 ile aynı).
Yanıtın koşulu "(i) → 0,404 çıkmalı, tutmuyorsa kollar koşulmaz" idi. (i) 8 bitlik kaynağın 4 kodluk basamaklarını
10 bitte bant sayıyor gibi: 8 bit e0 (i) 12,8 / (ii) 9,3; 10 bit kol (i) 3,1 / (ii) 9,2. Eski ölçer satırı 0,404.

Sorum: (i) okuma geçersiz sayılıp hüküm yalnız (ii) ile mi verilir? (ii) ile SVT'de hiçbir kol eşiği (≤ 7,48/7,49)
geçmiyor: 10bit 9,15/9,26, qm 9,23/9,31, vb1 9,17/9,38, tf0 9,01/9,11; grain8 0,005 ama süre 77 sn (e0 25,5, 3×)
ve yanıtın dediği gibi örtü. Yani Soru 1 "değişiklik yok, açık kalır" mı? Aynı ölçer tuzağı B6'daki HB VT CAMBI
2,84'ü de açıklıyor mu (bu koşumda HB VT `karanlik` (ii) 8,96 / 6,89, ürün 9,06 / 8,17)?

## Soru B — VT: hiçbir küme 6 film hücresini geçmedi

HB VT'ye karşı (ii) okuması, VMAF-NEG / XPSNR farkı (bant ±0,3 / ±0,2):

| Hücre | ürün (8 bit, 2 geçiş, maxrate 1,5) | 10 bit tek, maxrate | 10 bit tek, sınırsız |
|---|---|---|---|
| karanlik 2000 | +3,06 / +0,02 | +3,07 / −0,09 | +0,65 / +0,16 (bayt +%2,7) |
| karanlik 5500 | +0,18 / −0,17 | +0,32 / −0,23 | +0,18 / +0,04 |
| parlak 2000 | +0,06 / −0,62 | +1,14 / −0,32 | −2,33 / −0,76 |
| parlak 5500 | −0,39 / −0,72 | +0,42 / −0,27 (bayt −%6) | +0,04 / −0,23 |
| hareketli 2000 | +2,60 / +0,05 | +2,50 / −0,02 | −0,48 / +0,03 |
| hareketli 5500 | −0,05 / −0,56 | +0,06 / −0,27 | +0,09 / +0,01 |
| ekran 2000/5500 | önde / önde | önde / önde | önde / önde |

`prio_speed 0` ve `spatial_aq 1` kolları 10 bit tek ile **metrikte birebir aynı** (ölü ya da etkisiz; md5 VT'de
yazılmadı). Süre: HB VT 5,6-6,8 sn; ürünün toplamı 30-54 sn (kodlama 5 sn, geri kalanı sondalar) — ≤1,5× kuralını
hiçbir kol ürün içinde geçemez, kollar çıplak kodlama 1-4 sn.

10 bit + maxrate bugünkü ürüne göre her hücrede eşit ya da iyi (XPSNR açığı −0,56…−0,72'den −0,23…−0,32'ye),
ama kural "geçen en küçük küme" diyor ve geçen yok. Sorum: (1) 10 bit + maxrate ürüne girer mi (baskın iyileşme),
yoksa kural gereği değişiklik yok mu? (2) parlak 5500'deki −%6 bayt ürünün yeniden deneme döngüsünde kapanır mı,
yani hüküm ürün yoluyla (eş bayt HB) yeniden mi ölçülmeli? (3) Süre kuralı çıplak kodlamaya mı ürün toplamına mı?

## Soru C — Social kodek kolu

Social 25 MB 30 sn 1080p60, HB ile eş bayt, (VMAF-NEG ort / XPSNR / CAMBI):

| Kol | parlak | karanlik | süre parlak/karanlik |
|---|---|---|---|
| HB | 95,82 / 41,45 / 0,020 | 99,15 / 42,47 / 7,21 | 24,2 / 26,9 |
| svt-p6 (bugün) | 95,21 / 41,03 / 0,044 | 99,36 / 42,23 / 8,37 | 18,9 / 24,8 |
| svt-p4 | 95,60 / 41,44 / 0,051 | 99,44 / 42,48 / 8,84 | 31,7 / 37,4 |
| x265 slow | 96,27 / 41,98 / 0,026 | 99,44 / 42,83 / 6,35 | 109,8 / 131,0 |
| negatif yarım bit | 93,09 / 39,91 | 98,03 / 40,78 / 9,35 | |

Uydurma anahtar: ffmpeg "Error parsing option" uyardı, çıktı md5-farklı ama kalite p6 ile gürültü içinde
(95,22 / 99,36). **SVT 2 geçiş koşudan koşuya deterministik değil** (aynı 6154k: 6413 / 6395 kbps), md5 kanıtı SVT'de
çalışmıyor. `hareketli` bu işte yoktu (2 kesit).

p4, p6'nın parlak açığını (−0,61 / −0,42) gürültüye indiriyor, süre p6'nın 1,5-1,7×, HB'nin 1,3-1,4×. Sorum:
(1) p4 ürüne girer mi, girerse kapsamı ne (yalnız Social ön ayarları mı, bir bpp/çözünürlük eşiği mi — sonda
verisinden türetilebilir bir kural)? (2) Tek karanlık/parlak ölçümü yeter mi, `hareketli` + 4 kesit `handbrake`
işi şart mı? (3) Nişan merkezde kalıyor (sayım: ilk denemelerin 17/42'si ±%1,2 içinde) — onay.

Kod yazma, ölçüm koşma. Her soruya kısa hüküm + gerekçe + ürüne girecekse CI kabul ölçütü.
