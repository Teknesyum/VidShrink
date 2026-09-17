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
