# HandBrake A1 — Filtre Zinciri Ölçümü (B9)

Düzenek: `tools/kalite-paketi-3/hb.ps1 -Is filtre`, iş akışı `handbrake-kiyas.yml` (`isler=filtre`).
Yalnız GitHub Actions koşucusunda çalışır; yerelde kodlama yok.

## Kural (Ölçümden Önce Yazıldı, Sonra Gevşetilmez)

Girdi: `karanlik`, `parlak`, `hareketli` kesitleri (Sintel, 10 sn, ffv1). Her kesit önce
`libx264 -crf 4 -preset veryfast` ile h264 ara dosyaya çevrilir; kaynak ve VMAF referansı bu dosyadır.
Ürün yolu `bench shrink <ara> <2000 kbit karşılığı MB> --speed quality --no-resolution-drop --no-fps-drop --no-measure`.

**Progressive kesitler.** Üç kol, iki tekrar, sıra dönüşümlü (1. tekrar kapali → otomatik → acik,
2. tekrar acik → otomatik → kapali):

- `kapali`: `--filters deinterlace=off`
- `otomatik`: filtre bayrağı yok (varsayılan zincir; yoklama gerekiyorsa idet çalışır)
- `acik`: `--filters deinterlace=on` (idet+bwdif zinciri progressive içerikte çalışır)

Hüküm, kesit başına, `otomatik` ve `acik` kollarının her biri için `kapali`ya karşı:

1. |ΔVMAF-NEG ortalama| < **0,1** (iki tekrarın ortalaması)
2. Süre: kol başına iki tekrarın en küçük toplam süresi (`toplam_sn`); (kol − kapali) / kapali < **%5**

**Taramalı sentetik negatif kontrol.** `hareketli` kesitinden `tinterlace=mode=interleave_top,setfield=tff`
ile taramalı ara dosya (`-flags +ilme+ildct`; ffmpeg 8+ `-top` seçeneğini reddettiği için ilk koşum
35248878850'de düşürüldü, ffprobe `field_order` progressive olmayan bir değer, satırda kayıtlı), referans aynı kesitin çift
kareleri (`select=not(mod(n\,2))`). Kollar `kapali` ve `otomatik`, birer tekrar. Hüküm:

1. `otomatik` komutunda `bwdif` var, `kapali` komutunda yok
2. VMAF-NEG ortalama: otomatik − kapali ≥ **1,0**

Kural sayıları bu commit'te sabitlenmiştir; sonuç tablosu aşağıya ölçümden sonra eklenir.

## Sonuç

Henüz ölçülmedi.
