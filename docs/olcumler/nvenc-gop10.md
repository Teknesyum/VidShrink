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
