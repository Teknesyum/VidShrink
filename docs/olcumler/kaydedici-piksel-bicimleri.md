# Kaydedici Piksel Biçimleri

Tarih: 16 Eylül 2026. ffmpeg: `9.0-full_build-www.gyan.dev` (CI ile aynı sürüm).
Komut: `ffmpeg -hide_banner -h encoder=<ad>`, "Supported pixel formats" satırı olduğu gibi.

| Kodlayıcı | ffmpeg'in bildirdiği |
|---|---|
| libx264 | yuv420p yuvj420p yuv422p yuvj422p yuv444p yuvj444p nv12 nv16 nv21 yuv420p10le yuv422p10le yuv444p10le nv20le gray gray10le |
| libx265 | yuv420p yuvj420p yuv422p yuvj422p yuv444p yuvj444p gbrp yuv420p10le yuv422p10le yuv444p10le gbrp10le yuv420p12le yuv422p12le yuv444p12le gbrp12le gray gray10le gray12le yuva420p yuva420p10le |
| libsvtav1 | yuv420p yuv420p10le |
| libvpx-vp9 | yuv420p yuva420p yuv422p yuva422p yuv440p yuv444p yuva444p yuv420p10le yuva420p10le yuv422p10le yuva422p10le yuv440p10le yuv444p10le yuva444p10le yuv420p12le yuv422p12le yuv440p12le yuv444p12le yuva444p12le gbrp gbrap gbrp10le gbrap10le gbrp12le gbrap12le |
| h264_nvenc, hevc_nvenc, av1_nvenc | yuv420p nv12 p010le yuv444p p012le p016le nv16 p210le p212le p216le yuv444p10msble yuv444p12msble yuv444p16le bgr0 bgra rgb0 rgba x2rgb10le x2bgr10le gbrp gbrp10msble gbrp16le cuda d3d11 |
| h264_qsv | nv12 qsv |
| hevc_qsv | nv12 p010le p012le yuyv422 y210le qsv bgra x2rgb10le vuyx xv30le |
| h264_amf, hevc_amf | nv12 yuv420p d3d11 dxva2_vld p010le amf bgr0 rgb0 bgra argb rgba x2bgr10le rgbaf16le |

## Karar

Eski kümenin beş üyesi (`nv12`, `p010le`, `rgb24`, `bgr0`, `gbrp`) libx264'te uyarısız
başka biçime çevriliyordu. Küme altı düzlemsel YUV adına indi; kodlayıcı başına alt küme
`RecorderArguments.PixelFormatsFor`:

| Kodlayıcı | Kullanıcıya açık | `-pix_fmt`'e yazılan |
|---|---|---|
| libx264, libx265, libvpx-vp9 | yuv420p yuv422p yuv444p yuv420p10le yuv422p10le yuv444p10le | aynı ad |
| libsvtav1 | yuv420p yuv420p10le | aynı ad |
| nvenc (üçü) | yuv420p yuv444p yuv420p10le | 10 bit → p010le |
| h264_qsv | yuv420p | nv12 |
| hevc_qsv | yuv420p yuv420p10le | nv12, p010le |
| h264_amf, hevc_amf | yuv420p yuv420p10le | 10 bit → p010le |

Donanım kollarında ölçülen şey ffmpeg'in bildirdiği küme; kartın o biçimi gerçekten
kodlayabildiği ölçülmedi. `KayitFfmpegKoluTests.HerKodlayicininBicimleriFfmpeginBildirdigiKumede`
her koşumda bu tabloyu kurulu ffmpeg'e karşı yeniden okur.
