# Kaydedici Piksel Ölçümleri

Paket 2b. Makine: DISPLAY1 1024x768, konsol oturumu, ffmpeg 9.0 gyan full build.

## VP9 Profili ve Piksel Biçimi

Komut: `ffmpeg -f lavfi -i testsrc2=s=64x64:r=5:d=1 -c:v libvpx-vp9 -deadline realtime -cpu-used 8 -threads 1 -pix_fmt <biçim> -profile:v <profil>`, çıktı ffprobe ile okundu.

| Profil | Biçim | Çıkış kodu | ffprobe |
|---|---|---|---|
| 0 | yuv420p | 0 | Profile 0, yuv420p |
| 1 | yuv420p | -1094995529 | okunamaz; "Error encoding frame: Invalid parameter" |
| 1 | yuv444p | 0 | Profile 1, yuv444p |
| 2 | yuv420p | -22 | dosya yok |
| 2 | yuv420p10le | 0 | Profile 2, yuv420p10le |
| 3 | yuv444p10le | 0 | Profile 3, yuv444p10le |
| 0 | yuv444p | -1094995529 | okunamaz; "Error encoding frame: Invalid parameter" |
| 3 | yuv420p | -22 | dosya yok |
| 4 | yuv420p | -22 | dosya yok |
| main | yuv420p | -22 | "Unable to parse profile option value main" |

Sonuç: `-profile:v` 0..3 tamsayı; profil biçimden çıkar (`RecorderArguments.Vp9ProfileFor`), uyuşmayan çift `Validate`'te reddedilir. Ölçü `KayitFfmpegKoluTests.Vp9ProfilUyusmazligiFfmpegdeDeDuser` aynı komutu koşar.

## Renk Aralığı Adları

`ffmpeg -h full` ilk `-color_range` bloğu: `unknown`, `tv`, `pc`, `unspecified`, `mpeg`, `jpeg`, `limited`, `full`. Kaydedici kümesi `unknown`/`unspecified` dışındaki altı ad; eşitlik `KayitFfmpegKoluTests.RenkAraligiKumesiFfmpeginAdlariylaAyni` ile her koşumda ffmpeg'den okunur.
