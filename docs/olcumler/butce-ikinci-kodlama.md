# Bütçe Doldurmanın İkinci Tam Kodlaması ve B5'in İlk Deneme Ölçümü

Ölçüm tarihi 2026-09-17/18. Dal `t0/butce-ikinci-kodlama`, main `9c4b9907`.

İki soru ölçülüyor:

1. Bütçe arama döngüsünün koşturduğu ikinci (ve üçüncü) tam kodlama, toplam sürenin
   ne kadarı; x265 ile x264 arasında bu pay nasıl değişiyor.
2. B5 hız kapısı (`hiz_orani_tavan = 1.0`) yalnız **ilk denemenin** süresiyle
   ölçülse açılır mı.

Hüküm sorusu ayrı: ilk denemenin ölçülen bit hızından hedefi tek denemede
tutturmak mümkün mü, yoksa ikinci tam kodlama bütçe doğruluğu için mi gerekiyor.

## Ölçüm neden yeni bir alan gerektirdi

Soru sorulduğunda cevabı verecek sayı hiçbir yerde yoktu: `EncodeAttempt` kaydı
deneme başına süre taşımıyordu, yalnız toplam `elapsedSeconds` vardı. Bu yüzden

- `EncodeAttempt` kaydına `Seconds` alanı eklendi; ana döngüdeki `attemptClock` ve
  `FillUpAsync`'in kendi saati her iz satırına yazılıyor (`src/VidShrink.Ffmpeg/EncodeRunner.cs`,
  20 iz satırının hepsi tek `Iz(...)` kapısından geçiyor),
- CLI `--json` çıktısı her deneme için `seconds` basıyor (`src/VidShrink.Cli/CliApp.cs`),
- düzeneğe `butceilk` kolu eklendi (`tools/kalite-paketi-3/hb.ps1`): aynı kesitte
  ürünü `--kodek x265` ve `--kodek h264` ile koşturuyor, ürünün x265 çıktısının
  kbps'sinde HandBrake'i bayt eşleyerek koşturuyor, kapı satırına iki kodek için
  `_oran_toplam` ve `_oran_ilk_deneme` yazıyor.

Eksik ölçüm sıfır sayılmıyor: iz satırlarından biri süre taşımazsa `IzSureleri`
`Eksik = $true` döndürüyor ve alanlar boş kalıyor. "İlk deneme 0 saniye sürdü"
diye bir satır rapora giremez. Pim: `IzSureleriEksikAlaniSifirDiyeOkumuyor`.

## Bant ve bütçe sabitleri

Tabloların "bantta mı" kararı motorun kendi kademelerinden geliyor
(`FillBand.For`, `src/VidShrink.Core/PlanCalculator.cs`):

| Hedef | Alt kenar | Sert taban | Üst kenar |
|---|---|---|---|
| ≥ 50 MB | 0,972·T | 0,944·T | T |
| ≥ 10 MB | 0,95·T | 0,90·T | T |
| < 10 MB | 0,92·T | 0,85·T | T |

Bütçe doldurma (`BudgetFill`, `src/VidShrink.Core/BudgetFill.cs`):
`Floor = 0,97`, `Aim = 0,985`, `ExtraAttempts = 1`.

**Yapının kendisi şunu söylüyor:** bütçe doldurma 0,97·T'nin *altında* tetikleniyor,
teslim bandının alt kenarı ise 0,92·T. 0,92·T ile 0,97·T arasına düşen bir sonuç
**zaten teslim edilebilir**; doldurma onu 0,985·T nişanına çekiyor, yani hedefin en
çok %6,5'i kadar doluluk kazandırıyor. Yani `budget fill` dalıyla koşan ikinci tam
kodlama doğruluk için değil doluluk için koşuyor. Gerçek tahmin hatası `under band`
dalıdır. İki sebep hücre başına dal adından ayrılıyor, tabloda ayrı sütun.

Bu kademeler `tools/kalite-paketi-3/butce-ozet.py` içinde tekrarlanıyor ve motorun
çıktısına karşı pimli: `OzetleyicininBantKademesiMotorunFillBandiylaAyniKenariVeriyor`.
Mutasyon denendi — `(0.0, 0.92, 0.85)` → `(0.0, 0.90, 0.85)`:

```
Başarısız! - Başarısız:     1, Başarılı:     6, Atlanan:     0, Toplam:     7
```

derleme hatası sayısı 0. Sabit geri alındıktan sonra:

```
Başarılı!  - Başarısız:     0, Başarılı:     7, Atlanan:     0, Toplam:     7, Süre: 377 ms
```

## Yerel sonda (HandBrake'siz)

Kullanıcının makinesinde ağır ızgara yasak; yerelde yalnız iki kısa CLI koşumu
yapıldı. Girdi 6 sn 640×360 lavfi klibi, hedef 0,5 MB, bant alt kenarı 0,46 MB.
Ham izler:

```
x265: 3 deneme, kodlama 7,83 sn
  1:under band:495k:0.48->0.348  (1,34 sn)
  2:in band:668k:0.48->0.477     (3,47 sn)
  3:budget fill, the fuller result delivered:690k:0.492->0.492  (2,98 sn)
h264: 2 deneme, kodlama 1,21 sn
  1:in band:668k:0.48->0.468     (0,58 sn)
  2:budget fill, the fuller result delivered:702k:0.492->0.495  (0,60 sn)
```

Buradan çıkan, HandBrake'e bağlı olmayan iki sayı: x265'te ilk deneme toplam
kodlama süresinin **%17'si** (1,34 / 7,83), h264'te **%48'i** (0,58 / 1,21).
İlk denemenin sapması x265'te **−%30,40** (bandın dışında), h264'te **−%6,40**
(bandın içinde, yalnız 0,97 eşiğinin altında).

**Bu sondada HandBrake ölçülmedi.** Yerelde HandBrakeCLI koşturulmadığı için
oran ve B5 sütunları yerel veriden çıkarılamaz; `hb.ps1` zaten GitHub Actions
dışında koşmayı reddediyor (`exit 3`). Yerel sondanın işi düzeneği ve
özetleyiciyi doğrulamaktı; B5 hükmü yalnız ızgaradan gelen sayıyla veriliyor.

## Izgara koşumu

```
gh workflow run handbrake-kiyas.yml --ref t0/butce-ikinci-kodlama -f isler=butceilk
gh run list --branch t0/butce-ikinci-kodlama
35282699847 handbrake-kiyas completed success
```

Dört hücre paralel: `karanlik`, `parlak`, `hareketli`, `ekran`; her kesitte 600 ve
2000 kbit, her kbit'te ürün `--kodek x265` ve `--kodek h264` ile, sonra HandBrake
ürünün x265 çıktısının kbps'sinde bayt eşlenerek. 16 ürün koşumu, 8 HandBrake
koşumu. Zorlanan kodek 16/16 tuttu (`kodek_tuttu: true`).

**Negatif kontrol tuttu:** HandBrake bayt eşlemesi 8/8 hücrede sağlandı, sapma
−%1,38 ile +%0,32 arasında (`es_bayt: true`). Yani süre oranları aynı bayta
karşılık geliyor.

**Bu kolda kalite ölçülmedi.** VMAF/SSIM hesaplanmadı; soru süre ve deneme sayısı
sorusu. Birinci denemeyle teslim edilse kalitenin ne olacağı bu koşumdan
**ölçülemedi**, çünkü bu kol kalite hesaplamıyor.

**Bu oranlar `handbrake-kiyas-cli.md`'nin B5 oranlarının yerine geçmez.** Orada
hedef boy ve HandBrake ayarı başka; burada HandBrake ürünün kendi çıkış kbps'sine
eşleniyor ve hedef `kbit × süre`'den kuruluyor. İki belgenin oranları ayrı
düzeneklerin sayıları.

## Tablo 1 — Deneme sayisi, sure dagilimi, HandBrake orani

| Kesit | kbit | Zorlanan | Kodlayici | Deneme | Deneme sureleri | Ilk deneme sn | Deneme toplami sn | Kodlama disi sn | Toplam sn | HB sn | Oran toplam | Oran ilk deneme | Tek deneme toplami sn | Oran tek deneme | B5 toplam | B5 ilk deneme | B5 tek deneme |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| karanlik | 600 | h264 | libx264 | 1 | 1:11.3sn | 11,3 | 11,3 | 49,8 | 61,1 | 55,8 | 1,09 | 0,20 | 61,1 | 1,09 | kaldi | gecti | kaldi |
| karanlik | 600 | x265 | libx265 | 2 | 1:53.9sn / 2:55.4sn | 53,9 | 109,4 | 66,3 | 175,7 | 55,8 | 3,15 | 0,97 | 120,2 | 2,15 | kaldi | gecti | kaldi |
| karanlik | 2000 | h264 | libx264 | 2 | 1:17.1sn / 2:17.2sn | 17,1 | 34,3 | 50,3 | 84,7 | 74,8 | 1,13 | 0,23 | 67,4 | 0,90 | kaldi | gecti | gecti |
| karanlik | 2000 | x265 | libx265 | 2 | 1:97.4sn / 2:116.1sn | 97,4 | 213,5 | 74,9 | 288,4 | 74,8 | 3,86 | 1,30 | 172,3 | 2,30 | kaldi | kaldi | kaldi |
| parlak | 600 | h264 | libx264 | 2 | 1:12.4sn / 2:12.4sn | 12,4 | 24,9 | 46,7 | 71,6 | 61,0 | 1,17 | 0,20 | 59,1 | 0,97 | kaldi | gecti | gecti |
| parlak | 600 | x265 | libx265 | 3 | 1:64.5sn / 2:65.4sn / 3:65.8sn | 64,5 | 195,6 | 67,7 | 263,3 | 61,0 | 4,32 | 1,06 | 132,2 | 2,17 | kaldi | kaldi | kaldi |
| parlak | 2000 | h264 | libx264 | 2 | 1:18.7sn / 2:18.8sn | 18,7 | 37,5 | 49,5 | 87,0 | 78,8 | 1,10 | 0,24 | 68,2 | 0,87 | kaldi | gecti | gecti |
| parlak | 2000 | x265 | libx265 | 3 | 1:106.2sn / 2:105.5sn / 3:105.2sn | 106,2 | 316,9 | 65,9 | 382,9 | 78,8 | 4,86 | 1,35 | 172,1 | 2,18 | kaldi | kaldi | kaldi |
| hareketli | 600 | h264 | libx264 | 2 | 1:11.5sn / 2:11.6sn | 11,5 | 23,1 | 50,6 | 73,7 | 61,4 | 1,20 | 0,19 | 62,1 | 1,01 | kaldi | gecti | kaldi |
| hareketli | 600 | x265 | libx265 | 2 | 1:56.3sn / 2:57.1sn | 56,3 | 113,4 | 79,9 | 193,3 | 61,4 | 3,15 | 0,92 | 136,2 | 2,22 | kaldi | gecti | kaldi |
| hareketli | 2000 | h264 | libx264 | 2 | 1:18.3sn / 2:19.6sn | 18,3 | 37,9 | 56,8 | 94,7 | 82,3 | 1,15 | 0,22 | 75,1 | 0,91 | kaldi | gecti | gecti |
| hareketli | 2000 | x265 | libx265 | 2 | 1:122.4sn / 2:106.4sn | 122,4 | 228,8 | 103,4 | 332,2 | 82,3 | 4,04 | 1,49 | 225,8 | 2,74 | kaldi | kaldi | kaldi |
| ekran | 600 | h264 | libx264 | 3 | 1:3.6sn / 2:3.6sn / 3:3.6sn | 3,6 | 10,8 | 31,0 | 41,8 | 18,4 | 2,27 | 0,20 | 34,6 | 1,88 | kaldi | gecti | kaldi |
| ekran | 600 | x265 | libx265 | 3 | 1:13.4sn / 2:13.2sn / 3:13.2sn | 13,4 | 39,9 | 44,9 | 84,8 | 18,4 | 4,61 | 0,73 | 58,3 | 3,17 | kaldi | gecti | kaldi |
| ekran | 2000 | h264 | libx264 | 4 | 1:3.3sn / 2:4.8sn / 3:4.9sn / 4:4.9sn | 3,3 | 17,8 | 32,3 | 50,1 | 18,1 | 2,77 | 0,18 | 35,6 | 1,97 | kaldi | gecti | kaldi |
| ekran | 2000 | x265 | libx265 | 4 | 1:10.4sn / 2:20.2sn / 3:20.3sn / 4:20.4sn | 10,4 | 71,3 | 45,4 | 116,7 | 18,1 | 6,45 | 0,57 | 55,8 | 3,08 | kaldi | gecti | kaldi |

## Tablo 2 — Ikinci tam kodlamanin sebebi ve tahmin hatasi

Bant kenarlari FillBand.For kademelerinden; ust kenar hedefin kendisi. Butce doldurma esigi 0,97*hedef, nisani 0,985*hedef.

| Kesit | kbit | Zorlanan | Hedef MB | Bant alt MB | Ilk cikan MB | Ilk sapma % | Ilk bantta | Ilk esigin ustunde | 1. kbit | Lineer duzeltme kbit | 2. deneme kbit | 2. deneme dali | 2. cikan MB | 2. sapma % | Ek deneme sn | Ek denemenin toplamdaki payi % |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| karanlik | 600 | h264 | 0,732 | 0,674 | 0,713 | -2,65 | evet | evet | 587 | 603 | — | — | — | — | 0,0 | 0,0 |
| karanlik | 600 | x265 | 0,732 | 0,674 | 0,696 | -4,97 | evet | hayir | 587 | 618 | 608 | budget fill, the fuller result delivered | 0,717 | -2,10 | 55,4 | 31,5 |
| karanlik | 2000 | h264 | 2,441 | 2,246 | 2,344 | -3,99 | evet | hayir | 1956 | 2037 | 2007 | budget fill, the fuller result delivered | 2,403 | -1,57 | 17,2 | 20,3 |
| karanlik | 2000 | x265 | 2,441 | 2,246 | 2,309 | -5,42 | evet | hayir | 1956 | 2068 | 2037 | budget fill, the fuller result delivered | 2,386 | -2,27 | 116,1 | 40,3 |
| parlak | 600 | h264 | 0,732 | 0,674 | 0,698 | -4,70 | evet | hayir | 587 | 616 | 606 | budget fill, the fuller result delivered | 0,722 | -1,42 | 12,4 | 17,3 |
| parlak | 600 | x265 | 0,732 | 0,674 | 0,666 | -9,07 | hayir | hayir | 587 | 646 | 619 | in band | 0,705 | -3,74 | 131,2 | 49,8 |
| parlak | 2000 | h264 | 2,441 | 2,246 | 2,334 | -4,40 | evet | hayir | 1956 | 2046 | 2015 | budget fill, the fuller result delivered | 2,407 | -1,41 | 18,8 | 21,6 |
| parlak | 2000 | x265 | 2,441 | 2,246 | 2,219 | -9,11 | hayir | hayir | 1956 | 2152 | 2066 | in band | 2,345 | -3,95 | 210,7 | 55,0 |
| hareketli | 600 | h264 | 0,732 | 0,674 | 0,705 | -3,74 | evet | hayir | 587 | 610 | 601 | budget fill, the fuller result delivered | 0,720 | -1,69 | 11,6 | 15,7 |
| hareketli | 600 | x265 | 0,732 | 0,674 | 0,690 | -5,79 | evet | hayir | 587 | 623 | 614 | budget fill, the fuller result delivered | 0,725 | -1,01 | 57,1 | 29,5 |
| hareketli | 2000 | h264 | 2,441 | 2,246 | 2,307 | -5,51 | evet | hayir | 1956 | 2070 | 2038 | budget fill, the fuller result delivered | 2,405 | -1,49 | 19,6 | 20,7 |
| hareketli | 2000 | x265 | 2,441 | 2,246 | 2,301 | -5,75 | evet | hayir | 1956 | 2075 | 2044 | budget fill, the fuller result delivered | 2,425 | -0,67 | 106,4 | 32,0 |
| ekran | 600 | h264 | 0,317 | 0,292 | 0,276 | -13,04 | hayir | hayir | 587 | 675 | 648 | in band | 0,302 | -4,85 | 7,2 | 17,2 |
| ekran | 600 | x265 | 0,317 | 0,292 | 0,288 | -9,26 | hayir | hayir | 587 | 647 | 621 | in band | 0,293 | -7,69 | 26,4 | 31,1 |
| ekran | 2000 | h264 | 1,058 | 0,973 | 0,593 | -43,95 | hayir | hayir | 1522 | 2715 | 1956 | under band | 0,867 | -18,05 | 14,6 | 29,1 |
| ekran | 2000 | x265 | 1,058 | 0,973 | 0,438 | -58,60 | hayir | hayir | 1035 | 2500 | 1956 | under band | 0,834 | -21,16 | 60,9 | 52,2 |

## Tablo 3 — Lineer duzeltmenin olculmus karsiligi

Ilk denemenin kbit'inden hedefe lineer gidilse istenecek kbit; yanina ayni kosumda o kbit'e en yakin OLCULMUS deneme ve onun cikan boyutu. Kbit farki kucukse satir olculmus sayilir, buyukse lineer degerde olcum yok.

| Kesit | kbit | Zorlanan | Hedef MB | Lineer kbit | En yakin olculmus kbit | Kbit farki % | O denemenin cikani MB | Sapma % | Bantta | Esigin ustunde |
|---|---|---|---|---|---|---|---|---|---|---|
| karanlik | 600 | h264 | 0,732 | 603 | 587 | -2,6 | 0,713 | -2,65 | evet | evet |
| karanlik | 600 | x265 | 0,732 | 618 | 608 | -1,6 | 0,717 | -2,10 | evet | evet |
| karanlik | 2000 | h264 | 2,441 | 2037 | 2007 | -1,5 | 2,403 | -1,57 | evet | evet |
| karanlik | 2000 | x265 | 2,441 | 2068 | 2037 | -1,5 | 2,386 | -2,27 | evet | evet |
| parlak | 600 | h264 | 0,732 | 616 | 606 | -1,6 | 0,722 | -1,42 | evet | evet |
| parlak | 600 | x265 | 0,732 | 646 | 633 | -1,9 | 0,719 | -1,83 | evet | evet |
| parlak | 2000 | h264 | 2,441 | 2046 | 2015 | -1,5 | 2,407 | -1,41 | evet | evet |
| parlak | 2000 | x265 | 2,441 | 2152 | 2118 | -1,6 | 2,399 | -1,74 | evet | evet |
| hareketli | 600 | h264 | 0,732 | 610 | 601 | -1,4 | 0,720 | -1,69 | evet | evet |
| hareketli | 600 | x265 | 0,732 | 623 | 614 | -1,5 | 0,725 | -1,01 | evet | evet |
| hareketli | 2000 | h264 | 2,441 | 2070 | 2038 | -1,5 | 2,405 | -1,49 | evet | evet |
| hareketli | 2000 | x265 | 2,441 | 2075 | 2044 | -1,5 | 2,425 | -0,67 | evet | evet |
| ekran | 600 | h264 | 0,317 | 675 | 669 | -0,9 | 0,312 | -1,70 | evet | evet |
| ekran | 600 | x265 | 0,317 | 647 | 662 | 2,3 | 0,322 | 1,45 | hayir | evet |
| ekran | 2000 | h264 | 1,058 | 2715 | 2367 | -12,8 | 1,045 | -1,22 | evet | evet |
| ekran | 2000 | x265 | 1,058 | 2500 | 2503 | 0,1 | 1,047 | -1,03 | evet | evet |

## Tablo 4 — x265 ve x264 ayni kesitte

| Kesit | kbit | x265 deneme | x264 deneme | x265 toplam sn | x264 toplam sn | x265 ilk sn | x264 ilk sn | x265/x264 toplam | x265 oran (HB) | x264 oran (HB) |
|---|---|---|---|---|---|---|---|---|---|---|
| karanlik | 600 | 2 | 1 | 175,7 | 61,1 | 53,9 | 11,3 | 2,88 | 3,15 | 1,09 |
| karanlik | 2000 | 2 | 2 | 288,4 | 84,7 | 97,4 | 17,1 | 3,40 | 3,86 | 1,13 |
| parlak | 600 | 3 | 2 | 263,3 | 71,6 | 64,5 | 12,4 | 3,68 | 4,32 | 1,17 |
| parlak | 2000 | 3 | 2 | 382,9 | 87,0 | 106,2 | 18,7 | 4,40 | 4,86 | 1,10 |
| hareketli | 600 | 2 | 2 | 193,3 | 73,7 | 56,3 | 11,5 | 2,62 | 3,15 | 1,20 |
| hareketli | 2000 | 2 | 2 | 332,2 | 94,7 | 122,4 | 18,3 | 3,51 | 4,04 | 1,15 |
| ekran | 600 | 3 | 3 | 84,8 | 41,8 | 13,4 | 3,6 | 2,03 | 4,61 | 2,27 |
| ekran | 2000 | 4 | 4 | 116,7 | 50,1 | 10,4 | 3,3 | 2,33 | 6,45 | 2,77 |

## Soru 1 — İlk denemenin süresi HandBrake'e karşı

**Kodlama olarak ilk deneme HandBrake'in altında, ama kapı toplam süreyi ölçüyor.**

İlk denemenin *kendi* süresi / HandBrake (Tablo 1, "Oran ilk deneme"):

- x265: 0,57 – 1,49. Kapıdan geçen 4 hücre (karanlık/600 0,97; hareketli/600 0,92;
  ekran/600 0,73; ekran/2000 0,57), kalan 4 hücre geçmiyor (karanlık/2000 1,30;
  parlak/600 1,06; parlak/2000 1,35; hareketli/2000 1,49).
- h264: 0,18 – 0,24, 8/8 geçiyor.

Ama B5 kapısı `x265_oran_toplam`'a bakıyor, yani sürecin toplam duvar saatine.
Koşumun gerçek toplam oranı (Tablo 1, "Oran toplam" = toplam sn / HandBrake sn):

- x265: 3,15 – 6,45 → **0/8 geçiyor**.
- h264: 1,09 – 2,77 → **0/8 geçiyor** (en küçüğü karanlık/600 1,09).

Deneme sayısı bire düşse bile toplam, **kodlama dışı** süreyi taşımaya devam eder.
Bunu ayrıca ölçtük (Tablo 1, "Oran tek deneme" = (kodlama dışı + ilk deneme) /
HandBrake); bu sütun ölçülmüş değil **kurgusal** bir toplamdır, koşumun kendi
toplamı değildir:

- x265: 2,15 – 3,17 → **8/8 kalıyor**.
- h264: 0,87 – 1,97 → 4 hücre geçiyor (karanlık/2000 0,90; parlak/600 0,97;
  parlak/2000 0,87; hareketli/2000 0,91), 4 hücre kalıyor.

Sebep ölçüldü: kodlama dışı süre 31,0 – 103,4 sn ve bu tek başına HandBrake'in
**tüm kodlamasının 0,63 ile 2,51 katı**. Ürünün toplam süresinin içinde denemelere
düşmeyen bu pay, kendi başına B5'i x265'te kapatmaya yetiyor.

**Cevap:** hayır. Deneme sayısı bire indirilse bile B5 x265'te açılmaz; ölçülen
tek-deneme oranı 2,15'in altına inmiyor. İddianın "farkın çoğu deneme sayısından"
kısmı x265'te **yanlış**: x265'te ek denemeler toplamın %29,5 – %55,0'i (31,5 /
40,3 / 49,8 / 55,0 / 29,5 / 32,0 / 31,1 / 52,2), yani yarısından azı; geri kalanı
kodlayıcının kendi hızı ile kodlama dışı iş. h264'te aynı pay %15,7 – %29,1
(tek denemede biten karanlık/600/h264 hariç); %15,7 hareketli/600/h264'tür.

Bu kolda kodlama dışı sürenin nereye gittiği (yoklama, karmaşıklık örneklemesi,
ses, paketleme) **ölçülmedi**; ayrım için deneme dışı adımların da saati yok.

## Soru 2 — Hüküm: ikinci tam kodlama gerekiyor mu

**Gerekmiyor. Ölçüm iki ayrı sebep gösteriyor ve ikisi de doğruluk değil.**

### (a) 16 hücrenin 10'unda ilk deneme zaten bantta

Tablo 2, "Ilk bantta" sütunu: karanlık 4/4, parlak h264 2/2, hareketli 4/4 —
toplam 10 hücrede ilk deneme teslim bandının içinde, sapma −%2,65 ile −%5,79.
Bunların 9'unda yine de ikinci tam kodlama koştu ve dalın adı
`budget fill, the fuller result delivered`. Yani **teslim edilebilir bir sonucun
üstüne biraz daha doluluk almak için** tam bir kodlama daha koşuldu. Ölçülen
kazanç: ilk denemenin sapması ikinci denemede **2,05 – 5,08 puan** düzeliyor
(−%2,65..−%5,79 → −%0,67..−%2,27).

Bu dokuz hücrenin ek deneme bedeli, Tablo 2 "Ek deneme sn" sütunundan:

| Hücre | Ek deneme sn | Toplamdaki payı % |
| --- | --- | --- |
| karanlik/600/x265 | 55,4 | 31,5 |
| karanlik/2000/h264 | 17,2 | 20,3 |
| karanlik/2000/x265 | 116,1 | 40,3 |
| parlak/600/h264 | 12,4 | 17,3 |
| parlak/2000/h264 | 18,8 | 21,6 |
| hareketli/600/h264 | 11,6 | 15,7 |
| hareketli/600/x265 | 57,1 | 29,5 |
| hareketli/2000/h264 | 19,6 | 20,7 |
| hareketli/2000/x265 | 106,4 | 32,0 |
| **toplam** | **414,6** | — |

### (b) Bu ikinci kodlama tesadüf değil, nişan noktası onu garanti ediyor

`PlanCalculator.RetryAimMb` ölçülmüş verim varken **bandın ortasını** döndürüyor
(`src/VidShrink.Core/PlanCalculator.cs:1060`), 10 MB altı hedefte bu 0,96·T.
`BudgetFill.Floor` ise 0,97. Yani **nişanını tam tutturan bir deneme, tanımı gereği
doldurma eşiğinin altına düşer** ve bir tam kodlama daha tetikler.

Ölçülmüş örnek: parlak/600/x265 ikinci denemesi 0,703 MB nişanına 0,705 MB ile
düştü (0,963·T) ve üçüncü kodlama koştu; parlak/2000/x265 ikinci denemesi 2,344
nişanına 2,345 ile düştü (0,9605·T), yine üçüncü kodlama koştu. İkisi de "isabet"
denemeleriydi.

### (c) Düzeltme gereken hücrelerde de tek deneme yetiyor

6 hücrede ilk deneme bandı kaçırdı: parlak/600/x265 −%9,07, parlak/2000/x265
−%9,11, ekran/600/h264 −%13,04, ekran/600/x265 −%9,26, ekran/2000/h264 −%43,95,
ekran/2000/x265 −%58,60.

Tablo 3 şunu ölçüyor: ilk denemenin kbit'inden hedefe **lineer** gidilse hangi kbit
istenirdi, ve aynı koşumda o kbit'e en yakın **ölçülmüş** deneme ne çıkardı.
16 hücrenin 15'inde lineer noktanın %2,6'sı içinde ölçülmüş bir deneme var:

- 14'ü bandın içine ve 0,97 eşiğinin üstüne düştü, sapma −%2,65 ile −%0,67.
- 1'i hedefi aştı: ekran/600/x265, lineer 647k, en yakın ölçüm 662k (+%2,3),
  çıkan 0,322 MB = **+%1,45** — tavan aşımı.
- 16.'sı (ekran/2000/h264) için lineer nokta 2715k, en yakın ölçüm 2367k, yani
  %12,8 uzakta: **o hücrede lineer düzeltme ölçülmedi.** Elimizdeki 2367k zaten
  −%1,22'de olduğuna göre 2715k hedefi aşardı, ama bu ölçüm değil çıkarım.

Buna karşılık motorun kendi düzeltmesi çok daha ürkek: `PlanCalculator.Correct`
adımı `Math.Min(previousVideoK * factor, videoBudgetK)` ile kırpıyor
(`PlanCalculator.cs:1090`). Ekran/2000/x265'te ilk deneme 1035k, lineer düzeltme
2500k; motor 1956k istedi, sonra 2381k, sonra 2503k. **2503k ölçüldü: 1,047 MB,
hedefin −%1,03'ü, bantta.** Yani lineer düzeltmenin vardığı yer tek denemede
tutuyordu; motor oraya üç denemede gitti ve bu hücrede ek denemeler toplamın
%52,2'si (60,9 sn).

### Ölçülmüş tahmin hatası payı

İlk denemenin ölçülen bit hızından hedefe lineer gidilse sapma, lineer noktanın
%2,6'sı içinde ölçüm bulunan 15 hücrede **−%2,65 ile +%1,45** arasında. Bu pay
teslim bandının genişliğinden (alt kenar hedefin %8 altında) belirgin şekilde dar;
yani hedefi tek düzeltmeyle tutturmak ölçülmüş olarak mümkün. Tek risk üst taraf:
+%1,45 tavan aşımıdır ve kabul edilmez. Onun karşılığı nişanı hedefin biraz altına,
`BudgetFill.Aim` değeri olan 0,985·T'ye koymaktır.

**Bu nişanın ne vereceği ölçülmedi.** Elimizdeki veriden yalnız şu okunuyor: 12
doğal hücrede (karanlık, parlak, hareketli) ilk denemenin teslim/nişan oranı
0,947 – 1,014. Nişan 0,985·T olsaydı aynı oranlar 0,933·T – 0,999·T verirdi; bu bir
izdüşüm, ölçüm değil. Ekran kesitinde aynı oran 0,431'e kadar düşüyor, orada
düzeltme denemesi her hâlükârda gerekir.

## Hüküm

1. **İkinci tam kodlama azaltılabilir.** 16 hücrenin 9'unda o kodlama zaten bantta
   olan bir sonucun üstüne 2,05 – 5,08 puanlık sapma kazancı için koşuyor,
   doğruluk için değil; bu dokuz hücrenin faturası 414,6 sn.
   Kökü yapısal: yeniden deneme nişanı 0,96·T, doldurma tabanı 0,97 — isabetli
   deneme kendi kendine bir kodlama daha doğuruyor.
2. **Düzeltme gereken 6 hücrede de tek deneme ölçülmüş olarak yetiyor**; lineer
   düzeltmenin vardığı kbit'in yanında ölçülmüş sapma −%2,65 ile +%1,45.
3. **Ama bu B5'i açmıyor.** Koşumun ölçülen toplam oranı x265'te 3,15 – 6,45,
   h264'te 1,09 – 2,77; ikisi de 0/8. Denemeler bire indirilse bile kurgusal
   tek-deneme oranı x265'te 2,15 – 3,17; kapı 1,0.
   Kodlama dışı süre tek başına HandBrake'in 0,63 – 2,51 katı. B5'in x265'teki
   8/8 kalışı deneme sayısıyla kapanmaz; kodlayıcının kendi hızı ve kodlama dışı
   iş ayrı bir madde.
4. x265 ile x264 farkı (Tablo 4): x265'in toplamı aynı hücrede x264'ün 2,03 – 4,40
   katı. Deneme sayısı 3 hücrede x265'te daha fazla (karanlık/600, parlak/600,
   parlak/2000), 5 hücrede eşit. Yani fark ağırlıkla denemenin *sayısından* değil
   *süresinden* geliyor.

Ölçümün desteklediği tek değişiklik önerisi: `RetryAimMb`'nin ölçülmüş verim
dalındaki nişanı bandın ortasından `BudgetFill.Aim`'e (0,985·T) çekmek. Bu
değişikliğin sonucu ölçülmedi; ölçülen, mevcut nişanın doldurma eşiğinin altında
kaldığı ve bunun 9 hücrede fazladan tam kodlama ürettiğidir.

## Denetim sonrası kapatılan pim borçları

Denetim dört pim borcu buldu; dördü de kapatıldı, üçünün mutasyon kanıtı aşağıda.
Mutasyonların hepsi **0 derleme hatasıyla** derlendi, sonra geri alındı.

**1. `RetryAimTargetsTheBandCenterWhenTheYieldIsMeasured` totolojikti.** Beklenen
değeri de ölçüleni de `FillBand.For` üretiyordu; `For`daki 0,92 kaysa iki taraf
birlikte kayardı. Beklenen artık literal: 180 → 177,48; 25 → 24,375; 8 → 7,68.
Mutasyon `lowerFactor = 0.92` → `0.90`:

```
Assert.Equal() Failure: Values are not within 6 decimal places
Başarısız! - Başarısız:     1, Başarılı:     2, Atlanan:     0, Toplam:     3
```

**2. `BudgetFillTests` eşiğin aşağısına kördü.** `InlineData(0.97)` Floor 0,96
olsa da `false` kalıyordu. Yeni kol
`EsigiAsagiYaDaYukariKaydiranMutasyonTeslimKararindaGoruluyor` eşiğin iki yakasını
davranıştan okuyor: 0,9650 ve 0,9699 → ister, 0,9750 ve 0,9800 → istemez; hem
`Wants` hem `Plan` için. Mutasyon `Floor = 0.97` → `0.96`:

```
BudgetFillTests.EsigiAsagiYaDaYukariKaydiranMutasyonTeslimKararindaGoruluyor(oran: 0,965, bekleniyor: True) [FAIL]
BudgetFillTests.EsigiAsagiYaDaYukariKaydiranMutasyonTeslimKararindaGoruluyor(oran: 0,9699, bekleniyor: True) [FAIL]
Başarısız! - Başarısız:     2, Başarılı:    30, Atlanan:     0, Toplam:    32
```

**3. Hükmün merkezî ilişkisi pimsizdi.** `RetryAimMb(T, ölçülmüş verim) <
BudgetFill.Floor · T` (T < 10 MB) hiçbir testte sınanmıyordu. Yeni kol
`RetryAimStaysUnderTheBudgetFillFloorSoAnOnAimAttemptTriggersOneMoreEncode`:
nişan ve eşik literal (8 → 7,68 / 7,76; 1 → 0,96 / 0,97; 4,88 → 4,6848 / 4,7336),
ardından `BudgetFill.Wants(nişan, T, 2, 3, false)` **doğru** olmalı — yani nişanını
tam tutturan deneme bir tam kodlama daha istemeli. Mutasyon olarak **belgenin
önerdiği düzeltme** uygulandı (`return band.CenterMb` → `return BudgetFill.Aim *
targetMb`):

```
Başarısız! - Başarısız:     3, Başarılı:     0, Atlanan:     0, Toplam:     3
```

Yani bu pim ilişkiyi iki yönde de tutuyor: bozulursa da düzeltilirse de kırmızı
olur. Öneri uygulandığında **bu testin beklentisi de değişmeli**; pim, düzeltmenin
sessizce girmesini engelliyor.

**4. `HbOlcumDuzenegiTests`in sessiz geçen kolu.** `var shell = PowerShell(); if
(shell is null) return;` kabuk yokken hiçbir şey sınamadan yeşil dönüyordu. Kol
`[PowerShellFact]`e çevrildi (`LiveSourceFactAttribute` deseni). Kabuksuz durum
yerelde üretilemediği için koşul geçici olarak `|| true` yapılıp raporlama ölçüldü:

```
Atlandı VidShrink.Tests.HbOlcumDuzenegiTests.SvtKolununPresetEslemesiUrununAdiniSayiyaCeviriyor [1 ms]
Atlandı!   - Başarısız: 0, Başarılı: 0, Atlanan: 1, Toplam: 1
```

Koşul geri alındı. **Kabuksuz makinede gerçek koşum ölçülmedi**; ölçülen, `Skip`
gerekçesinin atlanmış olarak raporlandığıdır.

Kapılar (mutasyonlar geri alınmış hâlde):

```
dotnet build VidShrink.sln -c Release -warnaserror -m:2   → 0 Uyarı, 0 Hata
--filter FullyQualifiedName~FillBandTests          → 31 başarılı, 1 atlanan, 32 toplam
--filter FullyQualifiedName~BudgetFillTests        → 32 başarılı, 0 atlanan, 32 toplam
--filter FullyQualifiedName~HbOlcumDuzenegiTests   → 7 başarılı, 0 atlanan, 7 toplam
```

`FillBandTests`teki 1 atlanan `LiveFillTargetRunStaysInsideTheBand`; gerekçesini
`LiveSourceTheoryAttribute` yazıyor.

## Ham çıktı

Ham satırların tamamı `docs/olcumler/butce-ikinci-kodlama-ham.md` dosyasında,
özetleyicinin bastığı hâliyle.

## CI

```
gh run list --branch t0/butce-ikinci-kodlama
35291636284 ci af10255d completed success
35287674916 ci 8918b542 completed success
35285341684 ci 00dd1ba8 completed failure
35282699847 handbrake-kiyas 9642686d completed success
```

`00dd1ba8` kirmizisi olcumun degil pimin kusuruydu: `OzetSabiti` capasi `$` idi, CI'da dosya CRLF geldiginde satir sonundaki tasiyici donuse takilip "DOLDUR_ESIK bulunamadi" dedi. Capa `\s*$` oldu, betik depoda CRLF'e cevrildi; `8918b542` yesil: kosumun kendi kutugu `Failed: 0, Passed: 3226, Skipped: 27, Total: 3253`.

Denetim duzeltmelerinin kosumu `35291636284` yesil; kosumun kendi kutugu
`Failed: 0, Passed: 3233, Skipped: 27, Total: 3260` ve `KOSUM KAPISI GECTI:
basarisiz=0 toplam=3260 atlanan=27`. Toplam 3253'ten 3260'a cikti: FillBandTests
+3, BudgetFillTests +4.
