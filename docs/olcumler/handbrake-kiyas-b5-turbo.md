# B5 — Turbo İlk Geçiş: Süre ve Kalite

Durum: **ölçüldü, kod değişmedi.** Aynı kodlayıcıda (x265 slow, 2 geçiş) ürünün turbo ilk geçişi ile HandBrake'in
`--turbo`'su süre ve kalite açısından karşılaştırıldı.

## Düzenek

- Koşum **35158725446**, etiket `olcum-hb-handbrake+dusuk+social+bantlasma+turbo+hdr+ekranbant+vt--1`, commit
  `02993179`, işler `olcum (turbo, karanlik)` ve `olcum (turbo, hareketli)`, 600 ve 2000 kbit.
- Ürün turbo'yu yalnız Fast modunda açıyor; Bench `shrink` 2pass kullandığı için turbo Bench'i iş akışında ayrı git
  worktree'sinde `plan.Mode = "2pass"; plan.TurboFirstPass = true;` yamasıyla derlendi. Ürün kodu değişmedi. Turbo
  tavanı `CodecModel.TurboFirstPassCeilings`: libx265 ilk geçiş `veryfast`.
- Kollar: `urun-x265`, `urun-x265-turbo` (`--force-codec libx265`, düşürme kapalı), `handbrake-x265` (turbo yok),
  `handbrake-x265-turbo` (`--turbo`); HandBrake kolları ürünün baytına ±%2. Bu işte SSIM ve CAMBI koşulmadı.
- "Kodlama sn": üründe Bench `results.json` `EncodeSeconds`, HandBrake'te son denemenin duvar saati.

- Üç kıyas tablosu sırasıyla: `urun-x265`−`handbrake-x265`, `urun-x265-turbo`−`handbrake-x265-turbo`,
  `urun-x265-turbo`−`urun-x265`.

## Sonuç

- **Turbo'suz, ürün − HandBrake**: 4 satırın 3'ü önde (XPSNR +0,19 ile +0,43), `karanlik` 600 **geride**
  (VMAF-NEG −0,44).
- **Turbo'lu, ürün − HandBrake**: `hareketli` 600 **geride** (VMAF-NEG −1,94), `karanlik` 600 **geride** (−0,57,
  ürün %2,01 fazla bayt harcamasına rağmen), `karanlik` 2000 bantta, `hareketli` 2000 eş bayt değil (HandBrake %2,91
  az bayt; ürün +0,12 / +0,35) → ölçülemedi.
- **Turbo'nun maliyeti, ürün**: kodlama süresi 0,61–0,65 kat; VMAF-NEG −1,84 (`hareketli` 600), −0,42, −0,31,
  −0,22; XPSNR −0,07 ile −0,12.
- **Turbo'nun maliyeti, HandBrake** (tablodan, eş bayt içinde): süre 59,3/83,9 = 0,71, 79,8/109,9 = 0,73,
  69,9/73,2 = 0,95, 71,8/98,2 = 0,73 kat; VMAF-NEG +0,06, 0,00, −0,29, +0,05.
- Ürün turbo'yla HandBrake turbo'dan hızlı kodluyor (0,64–0,85 kat) ama hareketli düşük bit hızında HandBrake'in
  turbo'da korumadığı kaliteyi kaybediyor.

Karar: HandBrake'in turbo'su kaliteye neredeyse dokunmuyor (en çok −0,29); ürünün libx265 `veryfast` ilk geçişi
`hareketli` 600'de −1,84 puan götürüyor. `TurboFirstPassCeilings`'te libx265 için "güvenli" işareti bu kesitte
ölçüyle desteklenmiyor. Yalnız raporlandı.
**Ölçülmedi:** libx264 ve SVT-AV1 turbo, 10 sn'den uzun kesit.

## Tablo

| Kesit | kbit | Kol | Kodlayıcı | Geometri | kbps | Bayt | VMAF-NEG ort | VMAF-NEG harm | XPSNR | SSIM | CAMBI | Karanlık PSNR | Kodlama sn | Hata | Toplam sn | HB deneme | Ek hata |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| hareketli | 600 | urun-x265 | libx265 | 1920x818 | 581,3 | 726684 | 77,18 | 76,34 | 34,65 | — | — | 33,05 | 73,3 | — | 122,8 | — | — |
| hareketli | 600 | urun-x265-turbo | libx265 | 1920x818 | 579,0 | 723704 | 75,35 | 74,61 | 34,54 | — | — | 32,77 | 47,2 | — | 91,3 | — | — |
| hareketli | 600 | handbrake-x265 | HandBrakeCLI 1.11.2 x265 slow 2 gecis | 1920x818 | 572,9 | 716125 | 77,22 | 76,39 | 34,31 | — | — | 32,82 | 83,9 | — | 83,9 | 1 | — |
| hareketli | 600 | handbrake-x265-turbo | HandBrakeCLI 1.11.2 x265 slow 2 gecis | 1920x818 | 573,4 | 716735 | 77,28 | 76,45 | 34,33 | — | — | 32,83 | 59,3 | — | 59,3 | 1 | — |
| hareketli | 2000 | urun-x265 | libx265 | 1920x818 | 1931,5 | 2414321 | 96,82 | 96,66 | 39,55 | — | — | 38,66 | 107,0 | — | 161,2 | — | — |
| hareketli | 2000 | urun-x265-turbo | libx265 | 1920x818 | 1954,6 | 2443189 | 96,59 | 96,44 | 39,48 | — | — | 38,43 | 64,9 | — | 107,1 | — | — |
| hareketli | 2000 | handbrake-x265 | HandBrakeCLI 1.11.2 x265 slow 2 gecis | 1920x818 | 1898,8 | 2373489 | 96,47 | 96,31 | 39,13 | — | — | 37,41 | 109,9 | — | 109,9 | 1 | — |
| hareketli | 2000 | handbrake-x265-turbo | HandBrakeCLI 1.11.2 x265 slow 2 gecis | 1920x818 | 1897,8 | 2372203 | 96,47 | 96,31 | 39,13 | — | — | 37,42 | 79,8 | — | 79,8 | 1 | — |
| karanlik | 600 | urun-x265 | libx265 | 1920x818 | 577,3 | 721609 | 76,10 | 75,72 | 35,54 | — | — | 38,65 | 68,4 | — | 116,6 | — | — |
| karanlik | 600 | urun-x265-turbo | libx265 | 1920x818 | 588,0 | 735057 | 75,68 | 75,33 | 35,44 | — | — | 38,59 | 44,7 | — | 90,8 | — | — |
| karanlik | 600 | handbrake-x265 | HandBrakeCLI 1.11.2 x265 slow 2 gecis | 1920x818 | 583,9 | 729864 | 76,54 | 76,18 | 35,41 | — | — | 38,70 | 73,2 | — | 73,2 | 2 | — |
| karanlik | 600 | handbrake-x265-turbo | HandBrakeCLI 1.11.2 x265 slow 2 gecis | 1920x818 | 576,2 | 720196 | 76,25 | 75,88 | 35,36 | — | — | 38,66 | 69,9 | — | 69,9 | 1 | — |
| karanlik | 2000 | urun-x265 | libx265 | 1920x818 | 1936,3 | 2420395 | 95,67 | 95,56 | 39,66 | — | — | 42,86 | 93,1 | — | 135,7 | — | — |
| karanlik | 2000 | urun-x265-turbo | libx265 | 1920x818 | 1947,6 | 2434480 | 95,35 | 95,25 | 39,56 | — | — | 42,73 | 60,7 | — | 103,6 | — | — |
| karanlik | 2000 | handbrake-x265 | HandBrakeCLI 1.11.2 x265 slow 2 gecis | 1920x818 | 1911,2 | 2389034 | 95,11 | 94,98 | 39,47 | — | — | 42,67 | 98,2 | — | 98,2 | 1 | — |
| karanlik | 2000 | handbrake-x265-turbo | HandBrakeCLI 1.11.2 x265 slow 2 gecis | 1920x818 | 1920,4 | 2400483 | 95,16 | 95,02 | 39,49 | — | — | 42,70 | 71,8 | — | 71,8 | 1 | — |

| Kesit | kbit | Δ VMAF-NEG ort | Δ VMAF-NEG harm | Δ XPSNR | Δ SSIM | Δ CAMBI (ürün−HB) | Δ karanlık PSNR | Ürün sn / HB sn | kbps sapma % | Hüküm |
|---|---|---|---|---|---|---|---|---|---|---|
| hareketli | 600 | -0,04 | -0,05 | 0,35 | — | — | 0,23 | 0,87 | -1,45 | önde |
| hareketli | 2000 | 0,35 | 0,35 | 0,43 | — | — | 1,25 | 0,97 | -1,69 | önde |
| karanlik | 600 | -0,44 | -0,46 | 0,13 | — | — | -0,05 | 0,93 | 1,14 | geride |
| karanlik | 2000 | 0,55 | 0,59 | 0,19 | — | — | 0,18 | 0,95 | -1,30 | önde |

| Kesit | kbit | Δ VMAF-NEG ort | Δ VMAF-NEG harm | Δ XPSNR | Δ SSIM | Δ CAMBI (ürün−HB) | Δ karanlık PSNR | Ürün sn / HB sn | kbps sapma % | Hüküm |
|---|---|---|---|---|---|---|---|---|---|---|
| hareketli | 600 | -1,94 | -1,84 | 0,21 | — | — | -0,06 | 0,80 | -0,97 | geride |
| hareketli | 2000 | 0,12 | 0,13 | 0,35 | — | — | 1,02 | 0,81 | -2,91 | eş bayt değil |
| karanlik | 600 | -0,57 | -0,55 | 0,08 | — | — | -0,07 | 0,64 | -2,01 | geride (çok bayt) |
| karanlik | 2000 | 0,19 | 0,23 | 0,08 | — | — | 0,04 | 0,85 | -1,40 | bantta |

| Kesit | kbit | Δ VMAF-NEG ort | Δ VMAF-NEG harm | Δ XPSNR | Δ SSIM | Δ CAMBI (ürün−HB) | Δ karanlık PSNR | Ürün sn / HB sn | kbps sapma % | Hüküm |
|---|---|---|---|---|---|---|---|---|---|---|
| hareketli | 600 | -1,84 | -1,73 | -0,12 | — | — | -0,28 | 0,64 | 0,40 | geride |
| hareketli | 2000 | -0,22 | -0,23 | -0,07 | — | — | -0,23 | 0,61 | -1,18 | bantta |
| karanlik | 600 | -0,42 | -0,39 | -0,10 | — | — | -0,06 | 0,65 | -1,82 | geride |
| karanlik | 2000 | -0,31 | -0,31 | -0,09 | — | — | -0,13 | 0,65 | -0,58 | geride |
