# Argüman Kurulumu İncelemesi

Ortam: ffmpeg 9.0-full_build (gyan.dev), nvenc/amf/libvpl/libsvtav1/libvpx açık. "Ölçüldü" = ona soruldu.

## 1. Ne yapıyor

- `CodecModel` — kodlayıcı sınıflandırması (`IsHardware`, `Family`) ve model sabitleri: göreli
  bit ihtiyacı, kalite tavanı, CRF aralığı, referans CRF.
- `FfmpegArguments` — küçültme yolunun komutu: preset tabloları, hız/oran kontrolü, iki geçiş,
  `-vf` zinciri, ses ve konteyner bayrakları.
- `HdrResolver` — HDR koru/SDR'ye çevir kararı; pix_fmt, tone-map filtresi, renk üstverisi
  argümanları ve "politika değişti" bayrağı üretir.
- `ConversionArguments` — dönüştürme yolunun komutu; ayrıca konteyner-kodlayıcı ve
  konteyner-kopyalama uyumluluğunu doğrular. `HdrResolver` + `CodecModel`'i çağırır, preset'i
  `FfmpegArguments.DefaultPreset`'ten alır.

İki ayrı komut kurucu; ortak nokta yalnız `CodecModel` ve `HdrResolver`. Aşağıdakilerin çoğu bunun ürünü.

## 2. Argüman doğruluğu

**K1 — `-ss` girişte, `-to` çıkışta; kesme aralığı ikiye katlanıyor.** `ConversionArguments.cs:40` `-ss`'i
`-i`'den önce, `:42` `-to`'yu sonra yazıyor. Giriş araması çıkış zaman eksenini sıfırladığı için `-to` süre
gibi davranır. Ölçüldü: 30 sn kaynak, `-ss 10 -to 20` → çıktı **20.000 sn** (beklenen 10). Sessiz; kullanıcı
yanlış klibi alır. Çözüm: `-t (End-Start)` yaz ya da `-ss`'i `-i`'den sonraya al.

**K2 — `-cq` QSV ve AMF kodlayıcılarında yok.** `CodecModel.cs:54` `UsesCq = IsHardware` diyor, yani
nvenc/qsv/amf hepsi. Kullananlar: `FfmpegArguments.cs:64`, `ConversionArguments.cs:81`. Ölçüldü: `-h
encoder=h264_qsv` ve `-h encoder=h264_amf` çıktısında `-cq` **yok** (nvenc'te var). ffmpeg bilinmeyen
kodlayıcı özel seçeneğini sert hatayla değil uyarıyla yutuyor ("has not been used for any stream"), yani
**kalite hedefi hiç uygulanmıyor**. QSV'de `-global_quality`, AMF'te `-rc qvbr -qvbr_quality_level` (ya da
`-rc cqp -qp_i/-qp_p`) gerekir.

**K3 — libvpx-vp9'da `-preset` diye bir seçenek yok.** `FfmpegArguments.cs:12` VP9 için "0".."8" tablosu
tutuyor, `:28` varsayılanı "4", `:60` her kodlayıcıya koşulsuz `-preset` yazıyor;
`ConversionArguments.cs:79` aynısını yapıyor. Ölçüldü: `-c:v libvpx-vp9 -preset 4` → uyarı, seçenek yok
sayılıyor. VP9 hız kontrolü `-deadline` + `-cpu-used`; yani VP9'da hız ayarı hiçbir zaman etkili olmuyor.

**K4 — NVENC CRF dalında `-rc vbr` ve `-b:v 0` yok.** `FfmpegArguments.cs:62-68` CRF modunda `-cq N -maxrate
2Xk -bufsize 4Xk` yazıyor; ne `-rc` ne `-b:v`. `-cq` yalnız VBR oran denetimi altında anlamlıdır, `-b:v 0`
yoksa kodlayıcı varsayılan bit hızına düşer. `ConversionArguments.cs:80-82`'de `-maxrate` de yok, tek başına
`-cq`. Test makinesinde NVIDIA donanımı olmadığı için ölçülemedi; seçenek listesinden çıkarıldı.

**K5 — `IsValidPreset` komut kurulurken hiç çağrılmıyor.** `FfmpegArguments.cs:41` tanımlı ama `:60`
preset'i doğrulamadan yazıyor; doğrulama yalnız plan üretiminde (`PlanCalculator.cs:517,528`,
`PlanParser.cs:117`). Geçersiz preset ffmpeg'i sert hatayla düşürüyor — ölçüldü: `-preset zzz` → "x264
[error]: invalid preset 'zzz'".

**K6 — `-movflags +faststart` konteynerden bağımsız.** `FfmpegArguments.cs:106` her zaman yazıyor,
`ConversionArguments.cs:87` mp4/mov/m4a ile sınırlıyor. Ölçüldü: mkv/webm'de ffmpeg uyarı bile vermeden yok
sayıyor; küçültme yolu da hep mp4 üretiyor (`MainWindow.xaml.cs:283,430`) — bugün etkisiz, konteyner
seçilebilir olursa değil.

**K7 — kare hızı düşürme iki yolda iki yöntem.** `FfmpegArguments.cs:56-57` `-r` (çerçeve atar/kopyalar),
`ConversionArguments.cs:97` `fps=` filtresi (zaman damgasını yeniden kurar). Aynı plandan iki farklı sonuç.

**K8 — küçük noktalar.** `ConversionArguments.cs:62`'de `palettePath = outputPath` ataması dizi başlatıcısı
içinde değer olarak kullanılıyor (ölü atama); `:87` listesindeki `"m4a"` erişilemez çünkü `AudioOnly` dalı
`:49`'da erken dönüyor; `Validate` `plan.End`'in kaynak süresini aşmasını denetlemiyor (`:10-13`).

## 3. HDR

**Üstveri aktarımı yalnız libx265'te tam.** `HdrResolver.cs:7` `libx265`, `libsvtav1` ve `hevc_nvenc`'i
HDR10 koruyabilir sayıyor, ama `MasteringDisplayMetadata` ve `ContentLightLevel` yalnız `:35-44` libx265
dalında `-x265-params`'a yazılıyor. Diğer ikisi `:46`'dan sadece `-color_primaries/-color_trc/-colorspace`
ile çıkıyor; **mastering display ve max-CLL sessizce düşüyor**. libsvtav1 için `-svtav1-params
mastering-display=:content-light=`, hevc_nvenc için `-master_display` / `-max_cll` gerekir.
`MediaInfo.ColorRange` (`MediaInfo.cs:22`) hiç okunmuyor, `-color_range` yazılmıyor.

**10-bit + donanım kodlayıcı uyuşmuyor.** `HdrResolver.cs:46` koru dalında herkese `yuv420p10le` veriyor.
Ölçüldü: `hevc_nvenc` desteklenen formatlar `yuv420p nv12 p010le yuv444p ...` — `yuv420p10le` **listede
yok**; `av1_nvenc` de aynı. NVENC için `p010le` olmalı. `libsvtav1` (`yuv420p yuv420p10le`) ve `libx265`
sorunsuz.

**Tone-map zinciri çalışıyor, sıra ideal değil.** `HdrResolver.cs:9`. Gerçek HDR kaynakta (bt2020nc /
smpte2084 / yuv420p10le) hatasız koştuğu ölçüldü. Üç sapma: (a) `format=gbrpf32le` yok, ffmpeg otomatik
takıyor — hata çıkmıyor ama zincir belgelenmiş sıradan ayrılıyor; (b) gamut dönüşümü `p=bt709` tone-map'ten
**sonra**, ffmpeg'in kendi örneğinde önce; (c) `FfmpegArguments.cs:50-52` ve `ConversionArguments.cs:76`
ölçekleme filtresini tone-map'ten **önce** koyuyor, lanczos PQ eğrisi üzerinde çalışıyor. Üçü de hata değil,
kalite sapması.

**Politika değişimi dönüştürme yolunda bildirilmiyor.** `ConversionArguments.cs:75` `hdr.PolicyChanged`
sonucunu tümüyle atıyor; küçültme yolunda `PlanCalculator.cs:70-75` aynı bayrağı `AdviceCode.HdrTonemapped`
olarak yüzeye çıkarıyor.

## 4. Sabitler

**ffmpeg'e sorularak doğrulandı:**
- QSV (`FfmpegArguments.cs:16,17,19`) ve AMF (`:20-22`) preset listeleri birebir doğru. NVENC (`:14,15,18`)
  p1..p7 geçerli ama ffmpeg ayrıca `slow/medium/fast` kabul ediyor; SVT-AV1 (`:13`) gerçek aralık **-2..13**
  — ikisi de dar, yanlış değil.
- `SupportsRateLimits` libsvtav1 istisnası (`:36-37`) **doğru**: `-b:v 300k -maxrate 450k` → "Max Bitrate
  only supported with CRF mode", sert hata. Ancak CRF modunda maxrate destekleniyor; `:66` orada da
  kapatıyor, gereksiz muhafazakâr.
- `-cq`'nun yalnız nvenc'te olduğu, VP9'da `-preset` olmadığı, nvenc pix_fmt listesi.

**Doğrulanmamış — kaynağı yok (`docs/*.md` içinde bu sayıların geçtiği tek satır yok):**
- `CodecModel.cs:20-34` `RelativeBitrateNeed` (11 değer), `:42-46` `QualityLimit` (99/98/96), `:64-75`
  `SourceBitrateNeed` (8 değer), `:13-18` `ReferenceCrf`, `:36-40` `CrfHalvingStep`, `:56-60` `CrfRange`.
- `CodecModel.cs:5-11` QualityAtReference 93, QualityPerHalving 6, DetailConcentrationExponent 0.25,
  FpsBitrateExponent 0.75, ScalePenaltyScale 10, ScalePenaltyExponent 1.1, FpsPenaltyPerHalving 5.
- `FfmpegArguments.cs:25-34` varsayılan preset seçimleri — geçerlilikleri doğrulandı, isabetleri
  doğrulanmadı. `:67,73` çarpanlar: maxrate 2x/1.5x, bufsize 4x/2x. `PlanCalculator.cs:43`
  `HardwareUncalibratedBias` 1.06.

## 5. Kaçak kopya

**Kodlayıcı listesi beş yerde.** `FfmpegArguments.cs:8-23` (13) ↔ `ConversionArguments.cs:131` (13) ↔
`CodecModel.cs:20-34` (11; libx264 ve libvpx-vp9 yok) ↔ `PlanParser.cs:13` `AllowedCodecs` (12; **libvpx-vp9
yok**) ↔ `MainWindow.xaml.cs:472` (4 + copy). Somut sonuç: libvpx-vp9 dönüştürme sekmesinde seçilebiliyor
(`MainWindow.xaml.cs:472`, `ConversionArguments.cs:133`) ama `PlanParser.cs:13` onu reddediyor.

**"Hangi donanım kodlayıcı kalite kaybetmez" iki yerde, çelişkili.** `CodecModel.cs:42-46` yalnız
`av1_nvenc`'i ayrıcalıklı sayıyor (98 vs 96) ↔ `PlanCalculator.cs:492-495` `CostsQualityInHardware` hem
`av1_nvenc` hem `av1_qsv`'yi muaf tutuyor.

**Donanım verim sıralaması iki yerde, çelişkili.** `PlanCalculator.cs:45-48` `FastHardwareOrder` =
av1_nvenc, hevc_nvenc, av1_qsv, ... ↔ `CodecModel.cs:20-34` `RelativeBitrateNeed`'e göre sıra av1_nvenc
(0.60), av1_qsv (0.62), av1_amf (0.66), hevc_nvenc (0.88) olmalı.

**Codec ailesi eşlemesi iki yerde.** `CodecModel.cs:77-86` `Family` ↔ `ConversionArguments.cs:110-113` ham
aile adlarını ("h264", "hevc", "av1", "mpeg4") ayrıca listeliyor. 