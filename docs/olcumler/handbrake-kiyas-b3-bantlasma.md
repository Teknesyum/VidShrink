# B3 — Bantlaşma: Rampa, Çift Yönlü Negatif Kontrol, e0/e1

Durum: **ölçüldü, büyük kısmı ölçülemedi; kod değişmedi.** Soru: SVT-AV1'in `tune=1:enable-variance-boost=0`
(e0, üründeki) ayarı yerine `tune=0:enable-variance-boost=1:variance-boost-strength=2` (e1) CAMBI'yi +1,0 eşiğinden
fazla düşürüyor mu.

## Düzenek

- Koşum **35158725446**, etiket `olcum-hb-handbrake+dusuk+social+bantlasma+turbo+hdr+ekranbant+vt--1`, commit
  `02993179`, işler `olcum (bantlasma, karanlik)` ve `olcum (bantlasma, rampa)`.
- e1 Bench'i iş akışında ayrı git worktree'sinde `src/VidShrink.Core/FfmpegArguments.cs`'teki iki dizge değiştirilerek
  derlendi; ürün kodu değişmedi. İki kol `--force-codec libsvtav1 --no-resolution-drop --no-fps-drop`, 100/300/1200 kbit.
- `karanlik`: Sintel kesiti (B1). `rampa`: 1920x1080@24, 10 sn, `gradients` 0x000000→0x383838 yatay, üstüne
  `noise=alls=2:allf=t` titreşimi.
- CAMBI libvmaf `cambi` özelliği, `enc_width`/`enc_height` çıktı geometrisinde.
- Negatif kontroller iki yönlü: kaynak kendine (CAMBI ~0 beklenir) ve kaynağın 6 bite indirilmiş kopyası
  (`lutyuv=y=bitand(val\,252)`, CAMBI yükselmeli).

- Kıyas tablosunda "ürün" sütunları e0, "HB" sütunları e1 okunur; hüküm e0−e1 CAMBI farkına göre.

## Sonuç

- **Kontroller**: titreşimli rampa kendine 0,000, 6 bit kopyası 5,535 → ölçer iki yönde doğru. `karanlik` kendine
  0,404, 6 bit 0,976 → fark +0,57, eşiğin altında; film kesitinde 6 bitlik bozulmayı bile eşik yakalamıyor.
  Titreşimsiz temiz rampa kendine 23,678, 6 bit kopyası 0,000: basamaklar CAMBI penceresinden geniş, bilgi amaçlı.
- **`karanlik` 1200**: e0 9,241, e1 8,833, fark +0,41 → **eşik içinde**; kbps −%1,18. e1 VMAF-NEG'i −2,74 düşürdü.
- **`karanlik` 100**: iki kol da 3 denemede tavanı aştı (hedef 0,117 MB; e0 0,137/0,134/0,134 MB, e1 98k/29k/12k'da
  üçünde de 0,392 MB) → ürün çıktısı yok, ölçülemedi.
- **`karanlik` 300**: e1 3 denemede tavanı aştı (hedef 0,352 MB; 0,397/0,392/0,391 MB) → ölçülemedi. e1 bu kesitte
  istenen bit hızından bağımsız ~0,39 MB'ın altına inmiyor.
- **`rampa`**: altı çıktının hepsi CAMBI 23,28–23,68, yani temiz rampa düzeyi: kodlayıcı titreşimi silip basamakları
  geri getirdi. Baytlar eş değil: 100 kbit'te e0 23,7, e1 9,9 kbps; 300'de e0 285,6, e1 10,3 kbps; 1200'de e0 933,4,
  e1 1087,5 kbps. Ölçülemedi.
- e1 rampa 300'de ürün 0,589 ve 0,532 MB'lık iki tavan aşımından sonra 116k'da 0,012 MB (%3,3) üretti ve bunu
  "under band accepted" diye teslim etti. e0 rampa 1200'de "fallback to the last under-band result" ile %76.

Karar: e1'in CAMBI kazancı ölçülebilen tek eş bayt satırında eşik altında (+0,41) ve VMAF-NEG'e 2,74 puana mal oluyor;
e1'e geçmek için kanıt yok. Ürünün HandBrake'e karşı bantlaşma açığı (`handbrake-kiyas-b1-sdr.md`, `karanlik` +2,7)
bu ayarla kapanmıyor.
**Ölçülmedi:** e0/e1'in 100 ve 300 kbit `karanlik`'te karşılaştırması (tavan aşımı), eş baytta rampa.

Açık kusur (kod değişikliği ister, yalnız raporlandı): düşük hedefte SVT-AV1 2 geçiş tavanı üç denemede aşıyor ve
çıktı yok; titreşimli düz içerikte oran denetimi uçuruma düşüyor (116k → 10 kbps) ve ürün %3,3 dolulukta teslimi kabul
ediyor.

## Tablo

| Kesit | kbit | Kol | Kodlayıcı | Geometri | kbps | Bayt | VMAF-NEG ort | VMAF-NEG harm | XPSNR | SSIM | CAMBI | Karanlık PSNR | Kodlama sn | Hata | Toplam sn | HB deneme | Ek hata |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| karanlik | — | negatif-kaynak-kendisi | — | — | — | — | — | — | — | 1,0000 | 0,404 | — | — | — | — | — | — |
| karanlik | — | negatif-kaynak-6bit | — | — | — | — | — | — | — | 0,9943 | 0,976 | — | — | — | — | — | — |
| karanlik | 100 | e0 | — | — | — | — | — | — | — | — | — | — | — | urun ciktisi yok: bant-karanlik-100-e0 | — | — | — |
| karanlik | 100 | e1 | — | — | — | — | — | — | — | — | — | — | — | urun ciktisi yok: bant-karanlik-100-e1 | — | — | — |
| karanlik | 300 | e0 | libsvtav1 | 1920x818 | 299,7 | 374633 | 64,94 | 63,79 | 34,27 | 0,9673 | 8,971 | 37,72 | 21,0 | — | 62,7 | — | — |
| karanlik | 300 | e1 | — | — | — | — | — | — | — | — | — | — | — | urun ciktisi yok: bant-karanlik-300-e1 | — | — | — |
| karanlik | 1200 | e0 | libsvtav1 | 1920x818 | 1189,5 | 1486852 | 91,49 | 91,34 | 38,66 | 0,9897 | 9,241 | 41,92 | 27,4 | — | 68,4 | — | — |
| karanlik | 1200 | e1 | libsvtav1 | 1920x818 | 1175,5 | 1469337 | 88,75 | 88,53 | 38,98 | 0,9896 | 8,833 | 41,29 | 30,9 | — | 75,1 | — | — |
| rampa | — | negatif-rampa-temiz-kendisi | — | — | — | — | — | — | — | 1,0000 | 23,678 | — | — | — | — | — | — |
| rampa | — | negatif-rampa-temiz-6bit | — | — | — | — | — | — | — | 0,9949 | 0,000 | — | — | — | — | — | — |
| rampa | — | negatif-kaynak-kendisi | — | — | — | — | — | — | — | 1,0000 | 0,000 | — | — | — | — | — | — |
| rampa | — | negatif-kaynak-6bit | — | — | — | — | — | — | — | 0,9969 | 5,535 | — | — | — | — | — | — |
| rampa | 100 | e0 | libsvtav1 | 1920x1080 | 23,7 | 29603 | 85,19 | 85,19 | 37,75 | 0,9993 | 23,672 | 47,93 | 54,9 | — | 111,3 | — | — |
| rampa | 100 | e1 | libsvtav1 | 1920x1080 | 9,9 | 12376 | 85,14 | 85,14 | 37,75 | 0,9993 | 23,678 | 47,93 | 52,6 | — | 110,3 | — | — |
| rampa | 300 | e0 | libsvtav1 | 1920x1080 | 285,6 | 357059 | 85,24 | 85,24 | 37,75 | 0,9993 | 23,604 | 47,94 | 80,9 | — | 153,3 | — | — |
| rampa | 300 | e1 | libsvtav1 | 1920x1080 | 10,3 | 12828 | 85,15 | 85,15 | 37,75 | 0,9993 | 23,678 | 47,93 | 88,2 | — | 146,5 | — | — |
| rampa | 1200 | e0 | libsvtav1 | 1920x1080 | 933,4 | 1166709 | 85,59 | 85,58 | 37,76 | 0,9993 | 23,448 | 47,94 | 86,4 | — | 142,6 | — | — |
| rampa | 1200 | e1 | libsvtav1 | 1920x1080 | 1087,5 | 1359359 | 85,47 | 85,46 | 37,76 | 0,9993 | 23,276 | 47,94 | 88,4 | — | 146,8 | — | — |

| Kesit | kbit | Δ VMAF-NEG ort | Δ VMAF-NEG harm | Δ XPSNR | Δ SSIM | Δ CAMBI (ürün−HB) | Δ karanlık PSNR | Ürün sn / HB sn | kbps sapma % | Hüküm |
|---|---|---|---|---|---|---|---|---|---|---|
| karanlik | 100 | — | — | — | — | — | — | — | — | ölçülemedi |
| karanlik | 300 | — | — | — | — | — | — | — | — | ölçülemedi |
| karanlik | 1200 | 2,74 | 2,80 | -0,31 | 0,0002 | 0,407 | 0,62 | 0,89 | -1,18 | eşik içinde |
| rampa | 100 | 0,05 | 0,05 | 0,00 | 0,0000 | -0,006 | 0,00 | 1,04 | -58,23 | eşik içinde |
| rampa | 300 | 0,09 | 0,09 | 0,00 | 0,0000 | -0,074 | 0,00 | 0,92 | -96,39 | eşik içinde |
| rampa | 1200 | 0,12 | 0,12 | -0,00 | -0,0000 | 0,172 | 0,01 | 0,98 | 16,51 | eşik içinde |
