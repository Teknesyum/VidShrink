# GPU kodlama — ölçüm ve değerlendirme

Tarih: 2026-08-20 · Ölçen: T0 · Makine: RTX 5070 Ti (sürücü 610.74), ffmpeg 9.0-full

## Soru

Motor CPU yerine GPU kullanabilir mi? Bugün `Fast - NVENC` seçeneği `h264_nvenc`
üretiyor ve arayüz "megabayt başına belirgin kalite kaybettirir" diye uyarıyor.
Bu uyarı hâlâ doğru mu?

## Ölçüm

Kaynak: `gothic2026-08-15 14-01-29.mp4`, 10. saniyeden 8 saniyelik kesit,
1920x1080@48, CRF 12 ile yeniden kodlanmış referans (58,2 MB).
Hepsi tek geçiş VBR, boyut eşitlenerek. Ölçüm VMAF NEG (`vmaf_v0.6.1neg`).

| Kodlayıcı | Boyut | VMAF NEG | Kodlama süresi |
|---|---|---|---|
| `libx265` slow | 1 719 KB | **66,44** | 11,4 sn |
| `hevc_nvenc` p7 | 1 730 KB | 62,01 | 2,6 sn |
| `av1_nvenc` p7 | 1 645 KB | 65,62 | 1,6 sn |

Aynı boyutta `hevc_nvenc` x265'in **4,4 VMAF altında** — bugünkü uyarı bu kodlayıcı
için doğru. `av1_nvenc` ise %4,5 **daha küçük** dosyada yalnızca 0,8 VMAF geride,
yani pratikte x265 slow ile başa baş ve **7 kat hızlı**.

## Bulgular

1. **`av1_nvenc` mevcut ve kullanılmıyor.** `PlanParser.AllowedCodecs`, `CodecModel`
   ve `PlanCalculator.CodecPreference` yalnızca `h264_nvenc` / `hevc_nvenc` /
   `*_qsv` tanıyor. Blackwell nesli NVENC'in AV1 kodlayıcısı bu boşlukta kalıyor.
2. **NVENC hedef bitrate'i tutturmakta CPU kodlayıcılardan daha gevşek.** 1 876 kbit/sn
   istendiğinde 1 645 KB üretti — hedefin %12 altı. Hedef boyut aracında bu doğrudan
   doluluk bandını (T3) etkiler: NVENC yolunda bandın alt sınırı gevşetilmeli veya
   düzeltme turu zorunlu kılınmalı.
3. **`-hwaccel cuda` ile çözme %19 hızlanıyor** (5,8 → 4,7 sn, tam dosya). Kaliteyi
   hiç etkilemez, çünkü yalnızca girdinin çözülmesini GPU'ya alır. Bugün hiç
   kullanılmıyor. Karmaşıklık ve kalibrasyon problarında da kazandırır.
4. **VMAF ölçümü GPU'ya alınamıyor.** Bu derlemede `libvmaf_cuda` yok, yalnızca
   `libvmaf`. Ölçüm 1,07× hızda koşuyor — T5'in doğrulama turu bu yüzden uzun sürecek.
5. **AMD ve Intel kodlayıcıları da mevcut** (`av1_amf`, `hevc_amf`, `av1_qsv`).
   Motor `*_qsv`'yi tanıyor ama `*_amf`'yi hiç tanımıyor.

## Öneri

- `av1_nvenc` desteklenen kodlayıcılara eklenmeli; ölçüm onu "hızlı ama kalitesiz"
  kategorisinden çıkarıyor. Kalite uyarısı kodlayıcı bazında ayrışmalı.
- `-hwaccel cuda` bedava hızlanma; problarda ve kodlamada açılmalı, düşülebilir olmalı.
- NVENC yolunda bitrate sapması ölçülüp doluluk bandına yansıtılmalı.

Bu bulgular kod değişikliği içermez; ayrı bir sözleşmeye konu olacak.
