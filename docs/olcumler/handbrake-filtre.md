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
35248878850'de düşürüldü, ffprobe `field_order` progressive olmayan bir değer, satırda kayıtlı; taramalı 4:2:0 x264 yüksekliğin 4'e bölünmesini istediği için 35252046279'da 1920x818
kodlanamadı, ara dosya ve referans `crop=iw:trunc(ih/4)*4:0:0` ile 1920x816'ya kırpılır), referans aynı kesitin çift
kareleri (`select=not(mod(n\,2))`). Kollar `kapali` ve `otomatik`, birer tekrar. Hüküm:

1. `otomatik` komutunda `bwdif` var, `kapali` komutunda yok
2. VMAF-NEG ortalama: otomatik − kapali ≥ **1,0**

Kural sayıları bu commit'te sabitlenmiştir; sonuç tablosu aşağıya ölçümden sonra eklenir.

## Sonuç

Koşum 35254572887 (`handbrake-kiyas`, `isler=filtre`, `vt=false`), windows koşucusu, 2000 kbit.

Progressive kesitler (kapali'ya karşı, iki tekrar):

| kesit | kol | ΔVMAF-NEG | süre farkı | hüküm |
| --- | --- | --- | --- | --- |
| karanlik | otomatik | +0,0026 | +0,34 % | geçti |
| karanlik | acik | +0,0008 | +0,40 % | geçti |
| parlak | otomatik | −0,0124 | +0,98 % | geçti |
| parlak | acik | +0,0571 | +22,62 % | **kaldı** |
| hareketli | otomatik | −0,0098 | −1,74 % | geçti |
| hareketli | acik | +0,0112 | −1,52 % | geçti |

Otomatik kip üç kesitte de geçti: bayraksız koşumda zincir boş kaldı (`deinterlace=Auto zincir=`),
kalite ve süre kapali koluyla aynı. `acik` kolu kaliteyi bozmuyor ama `parlak` kesitinde elle
açılan `idet,bwdif` zinciri kodlamayı %22,6 yavaşlattı (kapali en küçük 81,8 sn, acik 100,3 sn);
varsayılan Auto olduğu için bu maliyet yalnız elle açana düşer.

Taramalı sentetik negatif kontrol (`hareketli`, ffprobe `field_order=tb`):

| kol | zincir | VMAF-NEG ort | hüküm |
| --- | --- | --- | --- |
| taramali-kapali | yok | 23,87 | — |
| taramali-otomatik | `idet,bwdif=mode=send_frame:parity=auto:deint=interlaced` | 98,78 | geçti |

ΔVMAF-NEG = +74,92 (eşik 1,0); bwdif yalnız otomatik kolun komutunda. Taramalı kaynakta
zincirin kurulması ayrıca süreyi düşürdü (60,4 → 57,8 sn).
