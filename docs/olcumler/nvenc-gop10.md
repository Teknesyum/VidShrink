# NVENC Anahtar Kare Tavanı: 5 sn → 10 sn

Makine: RTX 5070 Ti. Tarih: 2026-09-17. Dal: `t0/nvenc-gop10`. Karar: `docs/danisma/2026-09-17-nvenc-gop10-fable.md`.

## Karar Kuralı (Ölçümden Önce Yazıldı)

**Düzenek.** `tools/nvenc-2/tara.ps1`, nvenc-2 kesitleri (Sintel 10 sn FFV1 1920x818@24; `karanlik` 600, `parlak` 370,
`hareketli` 325, `orta` 365 sn'den). Her kol ürünün argümanı: `-c:v <kodek> -preset <p4|av1 p6> -b:v Bk -maxrate 2Bk
-bufsize 2Bk -rc vbr -multipass fullres -g <N> -pix_fmt yuv420p`, AQ yok. Geometri ve hedef kbps nvenc-2.md tablosundaki
ürün satırından: geometri "Yeni geo", hedef = istenen kbit × 1,024 × (1 + yeni sapma). `-b:v` hedefe ±%1,5 içinde en çok
üç denemede oturtulur. Kollar `g120` (bugünkü 5 sn) ve `g240` (10 sn).

**Izgara.** `karanlik`, `parlak`, `hareketli` × h264/hevc/av1 × {1000, 2000} × {g120, g240} = 36 kodlama; tutulmuş `orta`
× h264/hevc/av1 × 2000 × {g120, g240} = 6 kodlama.

**Hücre.** Kesit × kodek (3 × 3 = 9). Hücre farkı = iki bit hızındaki (g240 − g120) farklarının ortalaması, VMAF-NEG
ort ve p10 ayrı. `orta` satırları kapıya girmez, ayrıca raporlanır.

**Geçiş (üçü birden).**
1. 9 hücrenin en az 7'sinde ort farkı ≥ 0.
2. Hiçbir hücrede p10 farkı < −0,20.
3. hevc adil hücrelerde HandBrake açığı kapanır: nvenc-2'de \|HB sapma\| ≤ %2 olan hevc satırları (`hareketli` 2000,
   `karanlik` 1000, `parlak` 2000, `orta` 2000) üzerinde ortalama (g240 − HB) hem ort hem p10'da ≥ 0. HB puanları
   nvenc-2.md tablosundan; HB ürünün teslim ettiği kbps'e oturtulmuştu, g240 kolu aynı kbps'e oturtulur.

Geçerse `HardwareKeyframeCeilingSeconds` 5,0 → 10,0; geçmezse kod değişmez ve sonuç burada belgelenir.

**Kapı dışı ekler.** (a) Arama bedeli: `MpvEngine.SeekAsync(Exact).LatencyMs`, 24 hedef, `Random(1)`, hevc_nvenc
çıktısında 5 sn ve 10 sn tavan, p50. (b) 60 fps kabul: bir hevc_nvenc kodlaması `-g 600`, ffprobe ile anahtar kare
konumları 0/10/20 sn.

**Düzenek sağlaması.** `g120` kolu ürünün nvenc-2 puanını yeniden üretmeli (aynı argüman, aynı geometri, aynı kbps);
kesit dosyaları nvenc-2 boyutlarıyla (`orta` 85 588 331, `karanlik` 105 255 334, `hareketli` 110 072 003, `parlak`
111 313 591 bayt) aynı olmalı.

## Sonuç: Kapı Kaldı, Kod Değişmedi

Kural 1 **geçti** (ort ≥ 0 hücre 8/9; tek eksi karanlik h264, 1000'deki g240 satırı hedefin %4 altında kaldı: 941,8/981,5
kbps). Kural 2 **geçti** (en düşük hücre p10 +0,01). Kural 3 **kaldı**: hevc adil dört satırda g240−HB ort **−0,222**,
p10 +0,057 (g120−HB −0,335 / −0,37). Açık daraldı, ort'ta kapanmadı. HardwareKeyframeCeilingSeconds 5,0'da kaldı.

Kural önceden yazıldığı gibi uygulandı; sonuçtan sonra gevşetilmedi. 21 çiftin ortalaması g240−g120 +0,175 / +0,533.
Toplam NVENC kodlama süresi 88,6 sn (ızgara) + 8,4 sn (ekler).

Düzenek sağlaması: dört kesit nvenc-2 boyutlarıyla bayt bayt aynı; g120 kolu ürünün nvenc-2 puanlarını yeniden üretti
(örn. hareketli av1 2000: 97,00 / 90,25, nvenc-2'de de 97,00 / 90,25).
| Kesit | Kodek | kbit | Geo | Hedef kbps | g120 kbps / ort / p10 | g240 kbps / ort / p10 | g240−g120 ort / p10 | HB sapma % | g120−HB ort / p10 | g240−HB ort / p10 |
|---|---|---|---|---|---|---|---|---|---|---|
| hareketli | av1 | 1000 | 1882x802 | 910.6 | 895.2 / 87.52 / 77.49 | 888.8 / 87.72 / 77.41 | 0.2 / -0.08 | -2.85 (adil değil) | -0.06 / -0.22 | 0.14 / -0.3 |
| hareketli | av1 | 2000 | 1882x802 | 2019.9 | 2020 / 97 / 90.25 | 2037.2 / 97.16 / 90.77 | 0.16 / 0.52 | 0.33 | -0.09 / -0.91 | 0.07 / -0.39 |
| hareketli | h264 | 1000 | 1382x588 | 982.4 | 982.4 / 80.67 / 66.77 | 1007.3 / 81.8 / 68.48 | 1.13 / 1.71 | -8.73 (adil değil) | 11.36 / 16.74 | 12.49 / 18.45 |
| hareketli | h264 | 2000 | 1382x588 | 1931.9 | 1930.1 / 92.65 / 82.94 | 1901.8 / 92.6 / 82.85 | -0.05 / -0.09 | -0.88 | 2.87 / 3.94 | 2.82 / 3.85 |
| hareketli | hevc | 1000 | 1650x702 | 935.1 | 928.5 / 87.26 / 77.11 | 925.3 / 87.45 / 77.23 | 0.19 / 0.12 | -3.46 (adil değil) | 1.39 / 0.83 | 1.58 / 0.95 |
| hareketli | hevc | 2000 | 1882x802 | 1916.3 | 1899.5 / 95.84 / 88.05 | 1894.2 / 95.9 / 88.68 | 0.06 / 0.63 | -0.4 | -0.31 / -1.24 | -0.25 / -0.61 |
| karanlik | av1 | 1000 | 1882x802 | 992.6 | 1000.4 / 87.01 / 81.35 | 1001.2 / 87.1 / 81.46 | 0.09 / 0.11 | -0.22 | 0.12 / 0.12 | 0.21 / 0.23 |
| karanlik | av1 | 2000 | 1882x802 | 1924.1 | 1912.6 / 95.11 / 90.55 | 1913.7 / 95.21 / 90.46 | 0.1 / -0.09 | 1.17 | -0.27 / -0.18 | -0.17 / -0.27 |
| karanlik | h264 | 1000 | 1382x588 | 981.5 | 981.5 / 79.95 / 69.49 | 941.8 / 79.07 / 69.25 | -0.88 / -0.24 | -0.47 | 7.24 / 9.95 | 6.36 / 9.71 |
| karanlik | h264 | 2000 | 1382x588 | 2036.9 | 2037 / 92.06 / 85.01 | 2038.3 / 92.3 / 86.11 | 0.24 / 1.1 | 5.59 (adil değil) | 2.78 / 3.67 | 3.02 / 4.77 |
| karanlik | hevc | 1000 | 1650x702 | 997.3 | 983.6 / 85.16 / 78.64 | 982.6 / 85.25 / 78.89 | 0.09 / 0.25 | 1.99 | -0.02 / 0.69 | 0.07 / 0.94 |
| karanlik | hevc | 2000 | 1882x802 | 1939.3 | 1933.2 / 93.69 / 88.66 | 1952.3 / 94.01 / 89.47 | 0.32 / 0.81 | 2.03 (adil değil) | -0.42 / -0.78 | -0.1 / 0.03 |
| orta | av1 | 2000 | 1920x818 | 2013.6 | 1987.4 / 95.94 / 93.37 | 2092.3 / 96.03 / 93.5 | 0.09 / 0.13 | 0.12 | -0.02 / 0.02 | 0.07 / 0.15 |
| orta | h264 | 2000 | 1536x654 | 1963.6 | 1963.5 / 93.55 / 90.91 | 1937.4 / 93.57 / 90.95 | 0.02 / 0.04 | 3.06 (adil değil) | -1.39 / -1.24 | -1.37 / -1.2 |
| orta | hevc | 2000 | 1882x802 | 1922.3 | 1905.3 / 94.8 / 92.23 | 1912.9 / 94.9 / 92.35 | 0.1 / 0.12 | 1.34 | -0.51 / -0.84 | -0.41 / -0.72 |
| parlak | av1 | 1000 | 1882x802 | 872.3 | 867.4 / 85.7 / 71.96 | 867.5 / 85.83 / 72.1 | 0.13 / 0.14 | 1.84 | -0.33 / 1.52 | -0.2 / 1.66 |
| parlak | av1 | 2000 | 1882x802 | 2008.9 | 2018 / 92.04 / 86.96 | 2023.6 / 92.2 / 88.03 | 0.16 / 1.07 | -4.98 (adil değil) | -0.13 / 0.9 | 0.03 / 1.97 |
| parlak | h264 | 1000 | 1344x572 | 959.6 | 959.6 / 79.65 / 56.5 | 994.8 / 80.34 / 57.87 | 0.69 / 1.37 | 4.15 (adil değil) | 1.77 / 5.52 | 2.46 / 6.89 |
| parlak | h264 | 2000 | 1344x572 | 2021.6 | 2020.3 / 86.83 / 74.94 | 2058 / 87.33 / 77.96 | 0.5 / 3.02 | -7.68 (adil değil) | 0.39 / 6.72 | 0.89 / 9.74 |
| parlak | hevc | 1000 | 1804x768 | 874.6 | 863.5 / 83.43 / 69.35 | 862.1 / 83.57 / 69.2 | 0.14 / -0.15 | -12.38 (adil değil) | 2.32 / 3.41 | 2.46 / 3.26 |
| parlak | hevc | 2000 | 1882x802 | 1970.2 | 1980.4 / 90.53 / 85.18 | 1998.1 / 90.73 / 85.89 | 0.2 / 0.71 | 0.48 | -0.5 / -0.09 | -0.3 / 0.62 |

| Hücre | g240−g120 ort | p10 | ort ≥ 0 | p10 ≥ −0,20 |
|---|---|---|---|---|
| hareketli, av1 | 0.18 | 0.22 | evet | evet |
| hareketli, h264 | 0.54 | 0.81 | evet | evet |
| hareketli, hevc | 0.125 | 0.375 | evet | evet |
| karanlik, av1 | 0.095 | 0.01 | evet | evet |
| karanlik, h264 | -0.32 | 0.43 | hayır | evet |
| karanlik, hevc | 0.205 | 0.53 | evet | evet |
| parlak, av1 | 0.145 | 0.605 | evet | evet |
| parlak, h264 | 0.595 | 2.195 | evet | evet |
| parlak, hevc | 0.17 | 0.28 | evet | evet |

Kural 1: ort ≥ 0 olan hücre 8/9 (≥7 gerekli) -> GEÇTİ
Kural 2: p10 < −0,20 hücre yok -> GEÇTİ
Kural 3: hevc adil (4 satır) g240−HB -0.222 / 0.057 (g120−HB -0.335 / -0.37) -> KALDI
Tüm 21 çift ortalama g240−g120: 0.175 / 0.533

## Kapı Dışı Ekler

**Arama bedeli.** hevc_nvenc, Sintel 300-360 sn, 1920x818@24, 2000k, ürün argümanları; 	ools/nvenc-gop10/Arama
(MpvEngine.SeekAsync(Exact), 24 hedef, Random(1), oynarken, 400 ms ara), iki koşum:

| Tavan | Anahtar kareler | p50 ms | p90 ms | max ms |
|---|---|---|---|---|
| 5 sn (-g 120) | 0, 5, …, 55 | 45,6 / 43,8 | 94,3 / 90,1 | 148,0 / 130,0 |
| 10 sn (-g 240) | 0, 10, …, 50 | 91,7 / 95,4 | 135,2 / 140,3 | 161,2 / 156,4 |

Kaynak 1080p değil 1920x818. p50 yaklaşık iki katına çıkıyor, en kötü durum 150-160 ms civarında kalıyor.

**60 fps kabulü.** hevc_nvenc -vf fps=60 -g 600, 25 sn: çıkış kodu 0, 1500 kare, anahtar kareler 0 / 10 / 20 sn.

**Negatif kontrol.** Sabit 10,0'a çekilince FfmpegArgumentsTests 4 kırmızı verdi: Donanim_ust_siniri_bes_saniyede_sabit
("300" beklenen, "600" gerçek) ve Donanimda_ust_sinir_haritadan_etkilenmez üç kodekte (donanım tavanı yazılım
varsayılanından kısa değil). Geri alınınca 70/70 yeşil. Tavan ileride açılırsa ikinci test de yeniden temellendirilmeli;
fable'ın dokunulacaklar listesinde yok.

## Açıklar

- Kural 3 dört satıra dayanıyor ve HB puanları nvenc-2'nin koşumundan geliyor; aynı gün yeniden kodlanmadı.
- karanlik h264 1000 g240 satırı ±%1,5 bant dışında (−%4,0); kural 1'deki tek eksi hücre bu.
- Kolların arasındaki bayt farkı %5'e kadar çıkıyor (orta av1 2000: 1987 / 2092 kbps).
- Ölçüm sırasında başka bir süreç 8 idshrink_scan ffmpeg'i (%TEMP% vstats) çalıştırıyordu, ana süreci sonra kapandı.
  Bu işin kodlamaları sıralıydı: her an tek bir ffmpeg ya da measure-pair. Puanlar etkilenmez, süreler etkilenebilir.
