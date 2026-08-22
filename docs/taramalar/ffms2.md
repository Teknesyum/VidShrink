Tema: medya probe · kaynak: medya-probe.md

# Medya çözümleme ve üstveri okuma taraması

Not: `MediaArea/MediaInfo` kabuk, mantık `MediaArea/MediaInfoLib`'de; o depo okundu. Yollar `src/` altında.

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

## Kaynaklar
gh api repos/{MediaArea/MediaInfo, MediaArea/MediaInfoLib, FFMS/ffms2, yt-dlp/yt-dlp} + /releases/latest;
MediaInfoLib `File__Analyze_Streams_Finish.cpp` ve `Multiple/File_Mpeg4.cpp`; ffms2 `doc/ffms2-api.md`
ve `COPYING`; yt-dlp `postprocessor/ffmpeg.py` ve `utils/_utils.py`. Hepsi master, 2026-08-22.
