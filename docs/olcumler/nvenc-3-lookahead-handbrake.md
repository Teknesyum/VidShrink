# NVENC 3: Lookahead Açıkken HandBrake Kıyası

Makine RTX 5070 Ti, ffmpeg 9.0, HandBrakeCLI 1.11.2. 19 Eylül 2026. Düzenek
`nvenc-2.md` ile aynı: aynı üç 10 sn FFV1 kesit, aynı `tools/nvenc-2/kos.ps1`,
HandBrake ürünün teslim ettiği kbps'e en çok üç denemede oturtuluyor. Tek fark
üründe artık `-rc-lookahead 20 -lookahead_level 3` var
(`docs/olcumler/nvenc-kalite-kollari.md`). Ham veri `.calisma/nvenc-3/kos.json`.

Soru: kol, `nvenc-2.md`'de kalan hevc −0,20 / av1 −0,10 hücre açığını kapatıyor mu?

## Hücreler

| Kesit | Kodek | kbit | Ürün sapma % / deneme | Ürün ort / p10 | HB bayt sapma % | HB ort / p10 | Ürün−HB ort / p10 | Lookahead öncesi Ürün−HB ort |
|---|---|---|---|---|---|---|---|---|
| hareketli | av1 | 1000 | -0.45 / 2 | 90.58 / 81.28 | 2.77 | 90.66 / 81.26 | -0.08 / +0.02 | 0.23 / -0.35 |
| hareketli | av1 | 2000 | -0.41 / 1 | 97.26 / 90.96 | 0.26 | 97.16 / 91.02 | +0.10 / -0.06 | -0.09 / -0.91 |
| hareketli | av1 | 3500 | -3.41 / 2 | 98.86 / 95.97 | 2.55 | 98.88 / 95.8 | -0.02 / +0.17 | -0.02 / 0.10 |
| hareketli | hevc | 1000 | -0.07 / 2 | 89.29 / 80.6 | 2.68 | 88.74 / 79.87 | +0.55 / +0.73 | 1.49 / 0.64 |
| hareketli | hevc | 2000 | -2.93 / 2 | 96.38 / 89.88 | 4.01 | 96.75 / 90.39 | -0.37 / -0.51 | -0.21 / -0.93 |
| hareketli | hevc | 3500 | -4.07 / 2 | 98.69 / 95.24 | 0.44 | 98.73 / 95.24 | -0.04 / +0.00 | -0.18 / -0.36 |
| karanlik | av1 | 1000 | -5.72 / 1 | 86.95 / 81.42 | 1.71 | 86.79 / 81.12 | +0.16 / +0.30 | -0.05 / -0.23 |
| karanlik | av1 | 2000 | -7.8 / 1 | 95.31 / 90.63 | 1.67 | 95.19 / 90.58 | +0.12 / +0.05 | -0.22 / -0.06 |
| karanlik | av1 | 3500 | -3.4 / 1 | 98.56 / 95.61 | 1.06 | 98.53 / 95.17 | +0.03 / +0.44 | 0.01 / 0.13 |
| karanlik | hevc | 1000 | -1.32 / 1 | 86.1 / 79.79 | 1.34 | 85.34 / 78.3 | +0.76 / +1.49 | 0.17 / 0.72 |
| karanlik | hevc | 2000 | -4.12 / 2 | 94.06 / 89.64 | 2.28 | 94.24 / 89.81 | -0.18 / -0.17 | -0.37 / -0.83 |
| karanlik | hevc | 3500 | -3.76 / 2 | 97.94 / 94.83 | 1.27 | 97.94 / 94.78 | +0.00 / +0.05 | -0.27 / -0.64 |
| parlak | av1 | 1000 | -1.65 / 2 | 86.97 / 75.44 | -8.92 | 86.39 / 71.15 | +0.58 / +4.29 | -0.24 / 1.70 |
| parlak | av1 | 2000 | -2.48 / 2 | 91.96 / 87.19 | 4.09 | 92.62 / 87.25 | -0.66 / -0.06 | -0.18 / 0.42 |
| parlak | av1 | 3500 | -2.85 / 2 | 94.69 / 92.01 | 0.16 | 94.95 / 92.72 | -0.26 / -0.71 | -0.03 / 0.17 |
| parlak | hevc | 1000 | -13.44 / 3 | 83.6 / 67.37 | 6.57 | 84.09 / 70.31 | -0.49 / -2.94 | 2.50 / 3.38 |
| parlak | hevc | 2000 | -4.34 / 2 | 90.63 / 84.18 | 0.96 | 91.02 / 85.46 | -0.39 / -1.28 | -0.55 / -1.08 |
| parlak | hevc | 3500 | -3.06 / 2 | 93.98 / 91.08 | -0.44 | 94.09 / 92.27 | -0.11 / -1.19 | -0.17 / -1.23 |

## Adil hücre nedir

HandBrake bayt eşitlemesi üç denemede her zaman ±%2'ye oturmuyor; bu koşumda sapma
−%8,92 ile +%6,57 arasında. Bayt eşit değilken puan farkı kodlayıcıyı değil bütçeyi
ölçer, o yüzden özet iki kez veriliyor.

| Küme | n | Ürün−HB ort | Ürün−HB p10 | HB'yi geçen ort / p10 |
|---|---|---|---|---|
| Bayt sapması ≤ %2 (adil) | 10 | +0,037 | −0,091 | 5 / 10 · 5 / 10 |
| Bayt sapması ≤ %3 | 14 | +0,046 | −0,011 | 6 / 14 · 8 / 14 |
| Hepsi | 18 | −0,017 | +0,034 | 7 / 18 · 9 / 18 |

Adil hücrelerde kodek başına: **hevc +0,044 ort / −0,186 p10** (n=5),
**av1 +0,030 ort / +0,004 p10** (n=5).

## Okuma

**Açık kapandı, üstünlük gelmedi.** `nvenc-2.md`'nin bıraktığı hevc −0,20 ve av1 −0,10
ortalama açığı bu koşumda sıfırın hafif üstüne çıktı. Bu bir galibiyet değil berabere:
±0,05 puan VMAF-NEG'de gürültü mertebesinde ve adil küme yalnız beş hücre. hevc'in p10'u
hâlâ −0,19 ile geride; kötü yüzdelik dilim düzelmedi.

**Toplam 18 hücrenin özeti yanıltıcıdır ve buraya o yüzden üçüncü satır olarak kondu:**
bayt eşitlemesi tutmayan hücreler her iki yöne de sapıyor (parlak/av1 1000'de HB %8,9 az
bayt aldı, parlak/hevc 1000'de %6,6 fazla). İki koşumun toplam ortalamalarını
karşılaştırmak da doğru değil: `nvenc-2.md`'nin HB kolu o koşumun ürün baytlarına
oturtulmuştu, bu koşumunki bu koşumunkine.

**Olumsuz kontrol tuttu:** HandBrake ürün baytının yarısında altı hücrede de belirgin
biçimde kötü (ör. hareketli/hevc 1000: 72,98'e karşı ürün 89,29). Ölçü kör değil.

**Ürünün hedef altı teslimi sürüyor:** sapma −%0,07 ile −%13,44 arasında, ortalama
−%3,6. Boş bütçe kapısı `nvenc-butce-doldurma.md`'de ölçülüp kapalı bırakılmıştı;
bu koşum o kararı değiştirmiyor, ama parlak/hevc 1000'in −%13,44'ü aynı boşluğun
en geniş hâli ve puan farkının bir kısmını o açıklıyor.

