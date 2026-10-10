# K12 Ara Kodekler Ve Yeni Donanım Kodlayıcıları Ölçümü

Tarih 2026-10-10. Makine: Windows 11 Pro. ffmpeg `9.0-full_build-www.gyan.dev`.
Kaynak her koşumda lavfi: `testsrc=size=1280x720:rate=25:duration=2` + `sine=frequency=440` (44,1 kHz),
kodlama `-threads 2`. Aynı anda tek ffmpeg süreci koştu; yük ölçülmedi.

Plan `docs/plan-k12-kodekler.md`. VideoToolbox'ın eski ölçümleri `docs/olcumler/videotoolbox.md` ve
`docs/olcumler/videotoolbox-hizli.md`'de; bu belge onları tekrarlamaz.

## 1. Gerçek Kodlama

Her kodek bir kez kodlandı, çıktı ffprobe ile okundu.

| Kol | Komutun Özü | Çıkış | ffprobe |
|---|---|---|---|
| A1 ProRes 422 HQ → mov | `-c:v prores_ks -profile:v hq -pix_fmt yuv422p10le -c:a pcm_s16le` | 0 | `prores`, profil `HQ`, etiket `apch`, 1280x720, `yuv422p10le`; ses `pcm_s16le` 44100; süre 2.000000; 4 617 332 bayt |
| A2 DNxHR SQ → mxf, `-ar` yok | `-c:v dnxhd -profile:v dnxhr_sq -pix_fmt yuv422p -c:a pcm_s16le` | 127 | dosya okunmuyor: `[mxf] only 48khz is implemented` |
| A2b DNxHR SQ → mxf, `-ar 48000` | A2 + `-ar 48000` | 0 | `dnxhd`, profil `DNXHR SQ`, 1280x720, `yuv422p`; ses `pcm_s16le` 48000; `format_name=mxf`; süre 2.000000; 13 575 213 bayt |
| A3 DNxHR SQ → mov | A2'nin mov eşi | 0 | `dnxhd`, profil `DNXHR SQ`, `yuv422p` |

A2'nin düşüşü ürüne girdi: MXF kabında ses varsa komuta `-ar 48000` yazılır
(`ConversionArguments.MxfAudioArgs`).

Yukarıdaki dört kol elle yazılmış komuttur. Ürünün kendi komutu (`ConversionArguments.Build`) ayrıca
`AraKodekDonanimTests.AraKodekGercektenKodlanirVeFfprobeOkur` içinde koşar: 2 sn 320x240 kaynak,
ProRes LT → mov ve DNxHR LB → mxf; ffprobe kodeği, piksel biçimini ve MXF'te 48 kHz PCM'i okur, aynı
komut uydurma profille sıfırdan farklı çıkar. Yerelde 2/2 yeşil.

## 2. Profil Kabulü

Tek karelik kodlama, tablodaki on profilin hepsi:

| Kodlayıcı | Profil | Piksel Biçimi | Çıkış |
|---|---|---|---|
| `prores_ks` | `proxy`, `lt`, `standard`, `hq` | `yuv422p10le` | 0 |
| `prores_ks` | `4444` | `yuv444p10le` | 0 |
| `dnxhd` | `dnxhr_lb`, `dnxhr_sq`, `dnxhr_hq` | `yuv422p` | 0 |
| `dnxhd` | `dnxhr_hqx` | `yuv422p10le` | 0 |
| `dnxhd` | `dnxhr_444` | `yuv444p10le` | 0 |

Görevde geçen "DNxHR SB" ffmpeg'de yok; ffmpeg'in adı `dnxhr_sq` (SQ).

## 3. Olumsuz Kontroller

Bir anahtarın "kabul edildi" sayılması için uydurma eşinin reddedilmesi gerekir.

| Kol | Deneme | Çıkış | Okuma |
|---|---|---|---|
| N1 | `prores_ks -profile:v uydurma` | 127 | `Unable to parse "profile" option value "uydurma"`: profil gerçekten okunuyor |
| N2 | `dnxhd -profile:v dnxhr_uydurma` | 127 | aynı hata |
| N3 | `dnxhr_hqx` + `yuv422p` (8 bit) | 127 | `pixel format is incompatible with DNxHR HQX profile`: tablodaki biçim eşlemesi zorunlu |
| N4 | `prores_ks -preset slow` | 0 | **sessizce yutuldu**; ürün ara kodekte `-preset` yazmaz |
| N5 | `prores_ks -crf 23` | 0 | **sessizce yutuldu**; ürün ara kodekte `-crf` ve `-b:v` yazmaz |
| N6 | ProRes → mxf | 0 | ffmpeg yazıyor; ürün karar gereği reddeder (ProRes yalnız mov) |
| N7 | mxf + `aac` (44,1 kHz) | 127 | `only 48khz is implemented` |
| N7b | mxf + `aac` + `-ar 48000` | 127 | `Codec 'aac' is not supported by the bitstream filter 'pcm_rechunk'`: MXF'te ses yalnız PCM |
| N8 | mxf + `pcm_s16le` 44,1 kHz | 127 | `only 48khz is implemented` |
| N9 | `libx264` → mxf | 0 | ffmpeg yazıyor; ürün MXF'i yalnız DNxHR'a açar |
| N10 | ProRes → mkv | 0 | ffmpeg yazıyor; ürün ara kodeği mkv'ye yazmaz |
| N11 | mxf + `-movflags +faststart` | 0 | **sessizce yutuldu**; ürün MXF'te `-movflags` yazmaz |

N6, N9 ve N10 ffmpeg'in sınırı değil ürünün kararıdır: bu bileşimleri reddeden ffmpeg değil
`ConversionArguments.Validate`'tir.

## 4. VAAPI Ve VideoToolbox

| Soru | Okuma |
|---|---|
| Bu derlemede VAAPI kodlayıcısı | `h264_vaapi`, `hevc_vaapi`, `av1_vaapi` listede (ayrıca mjpeg, mpeg2, vp8, vp9) |
| `ffmpeg -h encoder=h264_vaapi` | tek piksel biçimi `vaapi`; `-rc_mode` 0..6 (içinde `VBR`); `-preset` yok |
| Ürünün yoklaması (`-vaapi_device /dev/dri/renderD128 … format=nv12,hwupload`) | çıkış 127: `Failed to initialise VAAPI connection: -1 (unknown libva error)`, `Device creation failed: -5` |
| Bu derlemede VideoToolbox kodlayıcısı | yok |
| `av1_videotoolbox` | ffmpeg'de böyle bir kodlayıcı yok; eklenmedi |

Sonuç: Windows ffmpeg'i VAAPI'yi listeliyor ama aygıt açılmıyor. Ürün bu yüzden kodlayıcıyı ada
değil platforma bağlar (`CodecModel.OnlyPlatform`): VAAPI yalnız Linux'ta, VideoToolbox yalnız macOS'ta
sunulur.

## 5. Ölçülmeyenler

- **VAAPI ile gerçek kodlama hiç koşmadı.** `-rc_mode VBR`, `format=nv12,hwupload` zinciri ve
  `/dev/dri/renderD128` yolu yalnız argüman testiyle pimli. `-rc_mode` için olumsuz kontrol de
  koşturulamadı: aygıt açılmadan seçenek okunmuyor.
- **VideoToolbox bu turda koşmadı.** Donanım sınıfına ölçümle değil kararla girdi. Eski ölçüm tek
  Apple M1'de kol başına tek bit hızı.
- VAAPI'de 10 bit yüzey (`p010`) denenmedi; ürün her kaynağı `nv12`'ye indirir.
- İkisinin de kalite ölçeği (`-q:v`, `-global_quality`) ölçülmedi; plan bit hızı kipinde kalır.
- ProRes ve DNxHR'da HDR kaynak, 4K, taramalı kaynak ve 25 dışındaki kare hızları denenmedi.
- DNxHR'ın 1280x720 dışındaki ölçüleri denenmedi.
- Dönüştür sekmesinin yeni liste öğeleri ekranda gözle denetlenmedi; yalnız başsız testler koştu.
