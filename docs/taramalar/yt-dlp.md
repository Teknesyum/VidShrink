Tema: medya probe · kaynak: medya-probe.md

# Medya çözümleme ve üstveri okuma taraması

Not: `MediaArea/MediaInfo` kabuk, mantık `MediaArea/MediaInfoLib`'de; o depo okundu. Yollar `src/` altında.

## yt-dlp/yt-dlp
**Ne yapıyor** — indirici; format seçerken aynı dört soruyu eksik üstveriyle cevaplıyor.
Unlicense, sürüm 2026.08.19, push 2026-08-20, 2605 açık issue (kalabalık, hepsi hata değil).
**Süre/bit hızı** (`postprocessor/ffmpeg.py`) — `_get_real_video_duration` yalnız
`('format','duration')` okuyor, boşsa "ffprobe returned empty duration"; `fatal=False` çağrısında
`None` dönüp iş atlanıyor, `_duration_mismatch` beyan ile ölçümü 2 sn toleransla karşılaştırıyor.
Boyut yoksa `filesize_from_tbr(tbr, duration)`=`duration*tbr*125`; parçalı formatta `tbr` tepe
değer olduğu için sonuç ayrı `filesize_approx` alanına gidiyor.
**HDR** — tek alan `dynamic_range`: `SDR`, `HDR10`, `HDR10+`, `HDR12`, `HLG`, `DV`. `parse_codecs`
codec dizesinden türetiyor (`dvh1`/`dvhe`→DV, `vp9.2`→HDR10), bilinmiyorsa `SDR`'ye sabitliyor.
**Alınacak fikir** — (1) HDR'yi bool yerine sıralı türe çevir: `IsHdr` şu an
`pix_fmt.Contains("10le")` içeriyor, 10-bit SDR kaynağı HDR sayıp `HdrResolver`'a gereksiz bt2020
yolu seçtiriyor. (2) Ölçülen ile tahmini boyutu/bit hızını ayrı alanda tut.
**Alınmayacak** — codec dizesinden HDR çıkarımı; yerelde `color_transfer` ve `side_data_list` var.
**Nereye dokunur** — `VidShrink.Core/MediaInfo.cs` (`IsHdr` → `DynamicRange`),
`VidShrink.Core/HdrResolver.cs` (DV/HLG dalları), `FfprobeClient.cs` (DOVI side data, tahmin bayrağı).

## Kaynaklar
gh api repos/{MediaArea/MediaInfo, MediaArea/MediaInfoLib, FFMS/ffms2, yt-dlp/yt-dlp} + /releases/latest;
MediaInfoLib `File__Analyze_Streams_Finish.cpp` ve `Multiple/File_Mpeg4.cpp`; ffms2 `doc/ffms2-api.md`
ve `COPYING`; yt-dlp `postprocessor/ffmpeg.py` ve `utils/_utils.py`. Hepsi master, 2026-08-22.
