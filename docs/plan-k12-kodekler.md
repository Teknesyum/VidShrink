# K12 Planı: ProRes, DNxHR, VAAPI Ve VideoToolbox

Kaynak: `docs/piyasa/tarama-2026-10-05.md`, "Sahibin Kararını Bekleyenler" tablosunun K12 satırı.
Kapsam o satırdır; genişletilmez.

## Karar Özeti

| Kodek | Sınıf | Girdiği Yol | Platform |
|---|---|---|---|
| `h264_videotoolbox`, `hevc_videotoolbox` | donanım | hedef boyut (kilit ve yapıştırılan plan) | macOS |
| `h264_vaapi`, `hevc_vaapi`, `av1_vaapi` | donanım | hedef boyut (kilit ve yapıştırılan plan) | Linux |
| `prores_ks` (Proxy, LT, 422, HQ, 4444) | ara kodek | yalnız Dönüştür | hepsi |
| `dnxhd` (DNxHR LB, SQ, HQ, HQX, 444) | ara kodek | yalnız Dönüştür | hepsi |

`av1_videotoolbox` eklenmez: ffmpeg'de böyle bir kodlayıcı yok.

Görevdeki "DNxHR SB" ffmpeg'de yoktur; kastedilen `dnxhr_sq` (SQ) sayıldı.

## Donanım Kodlayıcıları

Mevcut desen aynen izlenir, ikinci bir yol kurulmaz.

- `CodecModel.Vendor`: `Vaapi` satıcısı eklenir. `IsHardware` VideoToolbox ve VAAPI için de doğru olur.
  Kapının arkasındaki sayılar NVENC'te ölçüldü; QSV, AMF ve Media Foundation gibi taşınır.
- İkisinin de ölçülmüş kalite ölçeği yok: `HasQualityScale` yanlış, plan bit hızı kipinde kalır,
  `QualityArgs` açıkça patlar.
- Hız anahtarı yok: tek basamaklı merdiven (`default`), `SpeedArgs` boş.
- Platform kapısı `bool windows` yerine `HostPlatform` alır: Media Foundation Windows, VideoToolbox macOS,
  VAAPI Linux. Liste makinenin donanımına değil platforma bağlıdır; seçenek sayısı makineye göre oynamaz.
- VAAPI çerçeveleri donanım yüzeyinde ister: girdiden önce `-vaapi_device /dev/dri/renderD128`,
  süzgeç zincirinin sonunda `format=nv12,hwupload`, `-pix_fmt` yazılmaz. Yoklama aynı biçimde koşar.
- Yoklama ve düşüş kuralı değişmez: `PickLockedCodec` → çalışmıyorsa aynı ailenin yazılım kodlayıcısı.
- Otomatik Hızlı sırasına (`FastHardwareOrder`, `CompatibleHardwareOrder`) **eklenmez**.
  `docs/olcumler/videotoolbox-hizli.md` bu bağlamayı ölçtü ve kapıdan geçmedi (K2 2/8, K4 5/8).
  Yeni kodlayıcılar kilitle ve yapıştırılan planla seçilir.
- K6: `ParallelJobs.NeedsHardwareSlot` `IsHardware`'den türediği için kendiliğinden sayar; testle gösterilir.

## Ara Kodekler

- Yeni tür `IntermediateCodecs`: profil tablosu (ad, kodlayıcı, `-profile:v` değeri, piksel biçimi).
- `ConversionPlan.VideoProfile` eklenir. `ConversionArguments.Build` ara kodekte
  `-c:v <kodlayıcı> -profile:v <profil> -pix_fmt <biçim>` yazar; hız anahtarı, CRF ve bit hızı yazılmaz.
- Uyum tablosu: `mov` ProRes ve DNxHR kabul eder; yeni `mxf` kabı yalnız DNxHR ve `pcm_s16le` kabul eder.
  `mkv`'nin "her şey olur" satırı ara kodekleri kapsamaz.
- Hedef boyut yolu: `PlanParser` ara kodeği açık bir hatayla reddeder, kilit kümesi tanımaz.
  Sessizce başka kodeğe düşülmez.
- Arayüz: Dönüştür sekmesinde kodek listesine on satır (profil ada gömülü), kap listesine `MXF`.
  ffmpeg'de kodlayıcı yoksa satır listede kalır, kapalı görünür. Ara kodek seçiliyken kalite alanları kapanır.
- Düzenleyici dışa aktarımında kodek seçicisi yok (`EditExport` sabit kodlayıcı kullanıyor); dokunulmaz.

## Ölçüm

- ProRes ve DNxHR birer kez gerçekten kodlanır (`-threads 2`, 2 sn `testsrc`), `ffprobe` ile doğrulanır.
- Her yeni parametre uydurma bir değerle olumsuz kontrolden geçer.
- VAAPI ve VideoToolbox bu makinede koşturulamaz; argüman üretimi saf testle pimlenir.
- Sonuç: `docs/olcumler/k12-ara-kodekler.md`.

## Dokunulacak Dosyalar

`CodecModel.cs`, `FfmpegArguments.cs`, `PlanParser.cs`, `PlanCalculator.cs`, `PreviewSegment.cs`,
`IntermediateCodecs.cs` (yeni), `ConversionPlan.cs`, `ConversionArguments.cs`, `EncoderCapabilities.cs`,
`MainWindow.axaml`, `MainWindow.axaml.cs`, `MainWindow.DonusturKap.cs`, `MainWindow.Yoklama.cs`,
`LanguageCatalog.cs`, testler, `CHANGELOG.md`, `docs/YOL-HARITASI.md`, README'ler.
