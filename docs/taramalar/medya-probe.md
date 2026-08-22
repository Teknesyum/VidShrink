# Medya çözümleme ve üstveri okuma taraması

Not: `MediaArea/MediaInfo` kabuk, mantık `MediaArea/MediaInfoLib`'de; o depo okundu. Yollar `src/` altında.

## MediaArea/MediaInfoLib (+ MediaInfo)
**Ne yapıyor** — konteyner ayrıştırıcılarını kendi yazan üstveri okuyucu, ffmpeg'e bağlı değil.
Lib BSD-2-Clause, v26.05 (2026-05-12), push 2026-06-21, 361 açık issue; kabuk aynı lisans ve
sürüm, push 2026-08-11, 223 açık issue. İkisi de canlı.
**Süre/bit hızı** (`File__Analyze_Streams_Finish.cpp`) — `OverallBitRate` boşsa
`FileSize*8*1000/Duration`, ama tek video varsa süreyi önce `FrameCount/FrameRate`'ten yeniden
hesaplıyor (yorumdaki gerekçe: "29.97 fps'de tek kare"). Tersi de var: `Duration` boşsa
`FileSize*8*1000/OverallBitRate`. Süre <4 sn ise ölçülene güvenmeyip `Video_BitRate_Nominal`'a
düşüyor; video bit hızını konteyner payını %98 sayıp toplamdan ses/altyazıyı çıkararak buluyor.
**VFR** (`File_Mpeg4.cpp`) — mp4 `stts` min/max örnek süresinden türeyen fps farkı ≥0.001 ise
`FrameRate_Mode=VFR` + `FrameRate_Minimum`/`Maximum`, ortalama fps
`stts_FrameCount/stts_Duration*TimeScale`; eşitse CFR. **HDR** — `HDR_Format`, `..._Compatibility`,
`..._Profile`, `..._Commercial` ayrı alanlar; bit derinliği HDR kanıtı sayılmıyor.
**Alınacak fikir** — (1) VFR tespiti: `avg_frame_rate` ile `r_frame_rate` belirgin ayrışıyorsa
VFR say, planda `avg`'yi kullan, kullanıcıya göster. (2) Süre boşsa `nb_frames/avg_frame_rate`
ve `FileSize*8/bit_rate` ardıl yedekleri. (3) <4 sn dosyada ölçülen bit hızına güvenme.
**Alınmayacak** — kendi konteyner ayrıştırıcın; MediaInfoLib'i DLL bağlamak da gereksiz.
**Nereye dokunur** — `VidShrink.Ffmpeg/FfprobeClient.cs` (süre yedeği, fps kararı),
`VidShrink.Core/MediaInfo.cs` (`IsVfr`, `FpsMin/Max`, `BitrateIsEstimated`), `PlanCalculator.cs`.

## FFMS/ffms2
**Ne yapıyor** — libav üstünde kare-doğru erişim için dosyayı önce indeksleyen kütüphane. Kaynak
MIT (`COPYING`), ikili GPL; API `NOASSERTION`. Etiket 5.0 (2024-05-28), commit 2026-04-23, 14 issue.
**Süre/VFR** — `FFMS_VideoProperties.FirstTime`/`LastTime` (saniye); süre konteyner beyanından
değil bunlardan çıkıyor. Belge `FPSNumerator/FPSDenominator` için "bunlardan kare zamanı türetme,
VFR'yi ele alamazsın" diyor; doğru yol `FFMS_FrameInfo->PTS` + `FFMS_TrackTimeBase`.
**Bozuk dosya** — hata tek istisnaya çökmüyor: `FFMS_IEH_ABORT`/`CLEAR_TRACK`/`STOP_TRACK`/`IGNORE`, belge varsayılan `STOP_TRACK` öneriyor: hataya kadar okunanı sakla.
**HDR** — `FFMS_Frame` içinde `HasMasteringDisplayPrimaries`, `HasMasteringDisplayLuminance`,
`HasContentLightLevel`; her blok kendi "var mı" bayrağıyla, kısmi üstveri yarım okunmuyor.
**Alınacak fikir** — bozuk dosyada "hep ya da hiç" yerine kademeli düşme: okunanla devam et,
eksik alanı işaretle, istisna atma.
**Alınmayacak** — sıkıştırmadan önce tam indeks çıkarmak; indeksleme bütün dosyayı okur.
**Nereye dokunur** — `VidShrink.Ffmpeg/FfprobeClient.cs` (süre 0'da fırlatma yerine kısmi sonuç),
`VidShrink.App/MainWindow.xaml.cs` (eksik alan uyarısı).

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
