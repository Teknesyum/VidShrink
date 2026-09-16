# Paket 3 — WhatsApp Karanlık Video Kalitesi (Kalem 8)

Durum: **ölçüldü, kod değişmedi.**

Soru: WhatsApp çipi (`Compatible` → libx264) karanlık sahnede gölgeyi eziyor mu, ve libx264'e
AQ eklemek bunu düzeltir mi? Ürün bugün libx264'e hiçbir psy/AQ argümanı vermiyor
(`FfmpegArguments.Psychovisual`), yani x264'ün varsayılanı (aq-mode=1) koşuyor.

## Düzenek

- İş akışı `.github/workflows/kalite-olcumu.yml`, koşum **35111531254** (etiket `olcum-kalite-3`), iş `olcum (whatsapp)`.
- Betik `tools/kalite-paketi-3/kos.ps1 -Is whatsapp`, tablo `tools/kalite-paketi-3/ozet.py whatsapp`.
- Kaynak: Sintel 1080p (`https://download.blender.org/durian/movies/Sintel.2010.1080p.mkv`),
  sha256 `97F1DBC66231DF42AD49BD8C29AA174B8F48933058E47E7157D4BA63D93A8EFA`, 1920×818, 24 kare/sn, 888 sn.
  Tears of Steel 1080p adresi 404 döndü.
- Kesitler 10 sn, ffv1: `karanlik` 600. sn (YAVG ortalaması 29,9; kaynak piksellerin 0,94'ü Y<64),
  `orta` 365. sn (YAVG 178,78; Y<64 payı 0,09).
- Kodlama: libx264 slow, 2 geçiş, 1280×546, GOP 48, 500 ve 1000 kbit/sn. Kollar: `urun` (bench `psy-args libx264`
  = boş), `urun-tekrar` (belirlenim denetimi), `aq3`, `aq3-s08` (aq-strength 0,8), `negatif-aq0` (AQ kapalı).
- Ölçü: `bench measure-pair` — VMAF-NEG, XPSNR ve karanlık ölçüsü (`tools/VidShrink.Bench/KaranlikOlcu.cs`):
  kaynakta Y<64 olan piksellerde PSNR, ortalama kayma (test − kaynak) ve ayrık ton sayısı oranı.

## Sonuç

- `urun-tekrar` her satırda `urun` ile birebir aynı: ölçü belirlenimli.
- `aq3` ve `aq3-s08` karanlık PSNR'ı sekiz satırda −0,01 ile +0,07 dB arasında oynattı; hiçbiri 0,1 dB'e varmıyor.
- Negatif kontrol `negatif-aq0` karanlık PSNR'ı dört satırın dördünde düşürdü (−0,05 ile −0,24 dB) ve kaymayı
  dördünde artırdı (+0,04 ile +0,19). Ölçü AQ'nun yokluğunu görüyor.
- Aynı `negatif-aq0` VMAF-NEG ortalamasını 2,44 ile 6,73 puan **yükseltti**. VMAF-NEG karanlık gölge kalitesinin
  vekili değil; bu kalemde karar karanlık PSNR ve kaymadan verildi.

Karar: ürünün x264 varsayılanı karanlık bölgede ölçülen en iyi kolla 0,07 dB içinde; argüman eklemek için
ölçüm dayanağı yok. **Ölçülmedi:** WhatsApp'ın sunucu tarafında yeniden kodlaması (ağ ve hesap ister);
donanım kodlayıcı yolu (CI'da yok).

Açık kusur: `orta` kesiti (365. sn) `parlak` kesitine (370. sn) bitişik seçildi; seçim kuralı
çakışmayı engellemiyordu. Sonraki koşumlarda `hareketli` seçimi çakışanları dışarıda bırakıyor.

## Tablo

| Kesit | kbit | Kol | kbps | VMAF-NEG ort | VMAF-NEG harm | XPSNR | Karanlık PSNR | Ton oranı | Kayma |
|---|---|---|---|---|---|---|---|---|---|
| karanlik | 500 | urun | 513,4 | 59,67 | 58,92 | 33,98 | 36,93 | 1,97 | 0,22 |
| karanlik | 500 | urun-tekrar | 513,4 | 59,67 | 58,92 | 33,98 | 36,93 | 1,97 | 0,22 |
| karanlik | 500 | aq3 | 513,8 | 59,89 | 59,08 | 33,91 | 36,92 | 1,98 | 0,24 |
| karanlik | 500 | aq3-s08 | 512,5 | 61,92 | 61,21 | 33,74 | 36,99 | 1,93 | 0,27 |
| karanlik | 500 | negatif-aq0 | 509,5 | 66,39 | 65,84 | 32,36 | 36,76 | 1,80 | 0,41 |
| karanlik | 1000 | urun | 1009,0 | 79,14 | 78,85 | 36,98 | 39,42 | 1,73 | 0,16 |
| karanlik | 1000 | urun-tekrar | 1009,0 | 79,14 | 78,85 | 36,98 | 39,42 | 1,73 | 0,16 |
| karanlik | 1000 | aq3 | 1009,0 | 79,26 | 78,93 | 36,95 | 39,43 | 1,73 | 0,16 |
| karanlik | 1000 | aq3-s08 | 1007,3 | 80,62 | 80,33 | 36,85 | 39,49 | 1,69 | 0,16 |
| karanlik | 1000 | negatif-aq0 | 1003,0 | 83,33 | 83,09 | 35,67 | 39,35 | 1,62 | 0,21 |
| orta | 500 | urun | 500,8 | 83,05 | 82,74 | 37,69 | 44,66 | 1,71 | 0,11 |
| orta | 500 | urun-tekrar | 500,8 | 83,05 | 82,74 | 37,69 | 44,66 | 1,71 | 0,11 |
| orta | 500 | aq3 | 501,3 | 83,80 | 83,47 | 37,75 | 44,73 | 1,69 | 0,12 |
| orta | 500 | aq3-s08 | 501,6 | 84,90 | 84,63 | 37,82 | 44,74 | 1,67 | 0,13 |
| orta | 500 | negatif-aq0 | 501,7 | 87,52 | 87,36 | 37,59 | 44,42 | 1,64 | 0,19 |
| orta | 1000 | urun | 979,8 | 88,41 | 88,28 | 39,37 | 45,89 | 1,63 | 0,06 |
| orta | 1000 | urun-tekrar | 979,8 | 88,41 | 88,28 | 39,37 | 45,89 | 1,63 | 0,06 |
| orta | 1000 | aq3 | 982,7 | 88,90 | 88,76 | 39,42 | 45,93 | 1,63 | 0,06 |
| orta | 1000 | aq3-s08 | 981,8 | 89,47 | 89,35 | 39,47 | 45,96 | 1,61 | 0,06 |
| orta | 1000 | negatif-aq0 | 983,9 | 90,85 | 90,77 | 39,49 | 45,83 | 1,58 | 0,10 |

| Kesit | kbit | Kol | Δ VMAF-NEG ort | Δ XPSNR | Δ karanlık PSNR | Δ kayma |
|---|---|---|---|---|---|---|
| karanlik | 500 | urun-tekrar | 0,00 | 0,00 | 0,00 | 0,00 |
| karanlik | 500 | aq3 | 0,22 | -0,07 | -0,01 | 0,02 |
| karanlik | 500 | aq3-s08 | 2,26 | -0,24 | 0,06 | 0,05 |
| karanlik | 500 | negatif-aq0 | 6,73 | -1,62 | -0,17 | 0,19 |
| karanlik | 1000 | urun-tekrar | 0,00 | 0,00 | 0,00 | 0,00 |
| karanlik | 1000 | aq3 | 0,12 | -0,03 | 0,01 | -0,00 |
| karanlik | 1000 | aq3-s08 | 1,48 | -0,13 | 0,07 | -0,01 |
| karanlik | 1000 | negatif-aq0 | 4,19 | -1,31 | -0,07 | 0,05 |
| orta | 500 | urun-tekrar | 0,00 | 0,00 | 0,00 | 0,00 |
| orta | 500 | aq3 | 0,75 | 0,06 | 0,07 | 0,01 |
| orta | 500 | aq3-s08 | 1,85 | 0,13 | 0,07 | 0,02 |
| orta | 500 | negatif-aq0 | 4,47 | -0,11 | -0,24 | 0,08 |
| orta | 1000 | urun-tekrar | 0,00 | 0,00 | 0,00 | 0,00 |
| orta | 1000 | aq3 | 0,49 | 0,05 | 0,04 | 0,00 |
| orta | 1000 | aq3-s08 | 1,06 | 0,10 | 0,07 | 0,00 |
| orta | 1000 | negatif-aq0 | 2,44 | 0,12 | -0,05 | 0,04 |
