Tema: medya probe · kaynak: medya-probe.md

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

## Kaynaklar
gh api repos/{MediaArea/MediaInfo, MediaArea/MediaInfoLib, FFMS/ffms2, yt-dlp/yt-dlp} + /releases/latest;
MediaInfoLib `File__Analyze_Streams_Finish.cpp` ve `Multiple/File_Mpeg4.cpp`; ffms2 `doc/ffms2-api.md`
ve `COPYING`; yt-dlp `postprocessor/ffmpeg.py` ve `utils/_utils.py`. Hepsi master, 2026-08-22.
