# NVENC Tam Kareye Yapışma ve Lookahead 20

Makine: RTX 5070 Ti. Tarih: 2026-09-17. Dal: `t0/nvenc-tamkare`. Karar: `docs/danisma/2026-09-17-nvenc-hevc-sonraki-fable.md`
(Soru 2 ve Yedek; Soru 1'e dokunulmaz).

## Düzenek

Kaynak Sintel 1080p `https://download.blender.org/durian/movies/Sintel.2010.1080p.mkv`, sha256
`97F1DBC66231DF42AD49BD8C29AA174B8F48933058E47E7157D4BA63D93A8EFA` (1 180 090 590 bayt). Kesitler nvenc-2 ile aynı: 10 sn
FFV1 1920x818@24, `karanlik` 600, `parlak` 370, `hareketli` 325, `orta` 365 sn'den (`-ss <t> -i kaynak -t 10 -an -sn
-map 0:v:0 -c:v ffv1 -pix_fmt yuv420p`). Yeniden kesilen dosyalar nvenc-2 boyutlarıyla bayt bayt aynı (85 588 331 /
105 255 334 / 110 072 003 / 111 313 591).

Kodlama `tools/nvenc-2/tara.ps1` ile, her kol ürünün donanım argümanı: `-c:v <kodek> -preset <p4|av1 p6> -b:v Bk -maxrate
2Bk -bufsize 2Bk -rc vbr -multipass fullres -g 120 -pix_fmt yuv420p`, AQ yok. `-b:v` hedefe ±%1,5 içinde en çok 5
denemede oturtulur. Ölçü `VidShrink.Bench measure-pair` (VMAF-NEG ort / p10). Kodlamalar sıralı, her an tek ffmpeg.
Betikler: `tools/nvenc-tamkare/kos.ps1`, `tools/nvenc-tamkare/ozet.ps1`.

**Düzenek sağlaması.** hareketli hevc 2000 1882x802, hedef 1916,3: nvenc-gop10 g120 satırını (1899,5 kbps, 95,84 / 88,05)
yeniden üretmeli.

## Adım A — Karar Kuralı (Ölçümden Önce Yazıldı)

**Hücreler (6).** nvenc-gop10'daki 1882x802 ürün satırları: hevc `hareketli`, `parlak`, `orta`, `karanlik` 2000; av1
`hareketli`, `karanlik` 2000. Her hücrede tek kodlama: 1920x818 (ölçek süzgeci yok), hedef = aynı satırın g120 teslim
kbps'i (1899,5 / 1980,4 / 1905,3 / 1933,2 / 2020,0 / 1912,6). Karşılaştırılan 1882 puanları nvenc-gop10 g120 kolundan.

**Geçiş (üçü birden).**
1. 6 hücrenin en az 5'inde (tam − 1882) ort ≥ 0.
2. hevc dörtlüsünde (yukarıdaki dört hevc hücresi) ortalama (tam − HB) ort ≥ 0. HB puanları nvenc-2.md tablosundan
   (hareketli 96,15, parlak 91,03, orta 95,31, karanlik 94,11). karanlik hevc 2000'in HB bayt sapması %2,03 (sınırda);
   fable'ın dörtlüsüne girdiği için dahil.
3. Hiçbir hücrede (tam − 1882) p10 < −0,20.

Geçerse `PlanCalculator`'da yalnız NVENC donanım yolunda plan ölçeği ≥ 0,95 ise tam kareye çıkılır (ölçek süzgeci
yazılmaz); bugünkü eşik `Dimensions` içinde 0,985, yazılım yolu değişmez. Geçmezse kod değişmez, sonuç burada belgelenir.

## Adım B — Karar Kuralı (Ölçümden Önce Yazıldı)

**Çiftler (7), hevc_nvenc p4.** 4 adil hevc: `hareketli` 2000, `parlak` 2000, `orta` 2000 (A geçerse 1920x818, geçmezse
1882x802; hedef A'daki gibi g120 teslim kbps'i), `karanlik` 1000 (1650x702, hedef 983,6). 3 hevc 3500 tam kare:
`karanlik`, `parlak`, `hareketli` 1920x818, hedef = 3500 × 1,024 × (1 + nvenc-2 yeni sapma): 3458,2 / 3498,3 / 3447,5.
Kollar: `temel` (ürün argümanı) ve `la20` (temel + `-rc-lookahead 20`), ikisi de aynı hedefe.

**Geçiş (üçü birden).**
1. 7 çiftin en az 5'inde (la20 − temel) ort ≥ 0.
2. 7 çiftin (la20 − temel) ort ortalaması ≥ +0,15.
3. Bayt: 14 kodlamanın her biri hedefin ±%1,5 içinde.

Geçerse hevc_nvenc bit hızı argümanlarına `-rc-lookahead 20` eklenir; geçmezse kod değişmez. Kurallar sonuçtan sonra
gevşetilmez.

## Sonuç: İki Kapı da Kaldı, Kod Değişmedi

Düzenek sağlaması geçti: hareketli hevc 1882x802 1916,3 hedefi 1899,5 kbps, 95,84 / 88,05 verdi (nvenc-gop10 g120 ile aynı). Toplam NVENC kodlama süresi 43,7 sn (sağlama dahil 21 kodlama; B'nin 7 temel satırı 1882x802'de yeniden kodlandı).

### Adım A

| Hücre | Hedef kbps | 1882 ort / p10 | Tam kbps (sapma %) / ort / p10 | tam−1882 ort / p10 | tam−HB ort |
|---|---|---|---|---|---|
| hareketli hevc | 1899.5 | 95.84 / 88.05 | 1911 (0.61) / 95.84 / 88.00 | 0.00 / -0.05 | -0.31 |
| parlak hevc | 1980.4 | 90.53 / 85.18 | 2001.5 (1.06) / 90.75 / 83.31 | 0.22 / -1.87 | -0.28 |
| orta hevc | 1905.3 | 94.80 / 92.23 | 1918.9 (0.71) / 95.29 / 93.01 | 0.49 / 0.78 | -0.02 |
| karanlik hevc | 1933.2 | 93.69 / 88.66 | 1949.8 (0.86) / 93.67 / 88.71 | -0.02 / 0.05 | -0.44 |
| hareketli av1 | 2020 | 97.00 / 90.25 | 2028 (0.39) / 97.12 / 90.57 | 0.12 / 0.32 | — |
| karanlik av1 | 1912.6 | 95.11 / 90.55 | 1912.9 (0.02) / 95.17 / 90.64 | 0.06 / 0.09 | — |

A kural 1: tam−1882 ort ≥ 0 hücre 5/6 (≥5) -> GEÇTİ
A kural 2: hevc dörtlüsü ort(tam−HB) -0.26 (≥ 0) -> KALDI
A kural 3: p10 < −0,20 hücre var -> KALDI

A kapısı **kaldı** (kural 2 ve 3). parlak hevc tam karede ort +0,22 ama p10 −1,87; hevc dörtlüsünde HB açığı −0,26 (1882'de −0,44). `PlanCalculator` değişmedi. B, A kaldığı için 2000 hücrelerinde 1882x802 ürün geometrisiyle ölçüldü.

### Adım B

| Çift | Geo | Hedef kbps | temel kbps (sapma %) / ort / p10 | la20 kbps (sapma %) / ort / p10 | la20−temel ort / p10 |
|---|---|---|---|---|---|
| hareketli | 1882x802 | 1899.5 | 1911.4 (0.63) / 95.90 / 88.07 | 1918.8 (1.01) / 95.19 / 85.65 | -0.71 / -2.42 |
| parlak | 1882x802 | 1980.4 | 1980.2 (-0.01) / 90.54 / 85.14 | 1956.2 (-1.22) / 90.43 / 84.11 | -0.11 / -1.03 |
| orta | 1882x802 | 1905.3 | 1922.1 (0.88) / 94.84 / 92.21 | 1928.4 (1.21) / 94.61 / 92.12 | -0.23 / -0.09 |
| karanlik | 1650x702 | 983.6 | 997.3 (1.39) / 85.35 / 78.67 | 991 (0.75) / 85.38 / 77.52 | 0.03 / -1.15 |
| karanlik | 1920x818 | 3458.2 | 3436 (-0.64) / 97.61 / 94.01 | 3551.9 (2.71) / 97.63 / 93.99 | 0.02 / -0.02 |
| parlak | 1920x818 | 3498.3 | 3531.1 (0.94) / 94.00 / 91.02 | 3483.3 (-0.43) / 93.96 / 90.50 | -0.04 / -0.52 |
| hareketli | 1920x818 | 3447.5 | 3431.8 (-0.46) / 98.56 / 94.79 | 3466.2 (0.54) / 98.21 / 93.23 | -0.35 / -1.56 |

B kural 1: la20−temel ort ≥ 0 çift 2/7 (≥5) -> KALDI
B kural 2: ortalama -0.20 (≥ +0,15) -> KALDI
B kural 3: 14 kodlama ±%1,5 bantta -> KALDI

B kapısı **kaldı** (üç kural). la20 yedi çiftin beşinde ort'ta eksi, p10'da yedisinde eksi. karanlik 3500 la20 satırı beş denemede bandı tutturamadı (+%2,71); kural 1 ve 2 bundan bağımsız kalıyor. `-rc-lookahead 20` ffmpeg'de çıkış 0 ile kodlandı; argüman eklenmedi, kabul/negatif kontrol gerekmedi.

## Açıklar

- HB ve 1882 puanları nvenc-2 / nvenc-gop10 koşumlarından; aynı gün yeniden kodlanmadı (sağlama bir hücrede birebir tuttu).
- A'daki tam kare kolları hedefin %0,0-1,1 üstünde teslim etti; 1882 kolu hedefin tam kendisi. Bayt farkı tam karenin lehine.
- Tek kaynak (Sintel 24 fps). Fable'ın notundaki %4,8 boş bütçe döngüsü ayrı iş, dokunulmadı.
