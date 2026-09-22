# Danışma 010 girdi: Soru: Ön ayar profilindeki kap (Container) alanı uygulansın mı, nasıl?

Ajana giden metin:

---

[[danisma:010]]

# Soru: Ön ayar profilindeki kap (Container) alanı uygulansın mı, nasıl?

Kullanıcı önceliği: kullanıcı kolaylığı ön planda.

## Olgular (kodda ölçüldü, 2026-09-22)
- `PresetProfile.Container` (`src/VidShrink.Core/PresetLibrary.cs:34`) hiçbir yolda okunmuyor: ne CLI `--profil` (`src/VidShrink.Cli/CliRequest.cs:153-158` Intent/Fill/MaxShortEdge/AudioKbps/Codec uyguluyor), ne pencerenin ön ayar yongası (`MainWindow.OnAyar.cs ApplyUserPreset`).
- Gömülü 42 profilin yalnız 4'ü (Chromecast/Nest/Apple TV cihaz profilleri) `container: Mp4` taşıyor; Mp4 zaten varsayılan. Diğerleri kap taşımıyor.
- Kap taşıyabilen tek kaynak HandBrake ön ayar içe aktarımı: `av_mp4`/`av_mkv`/`av_webm`/`av_mov` → Mp4/Mkv/WebM/Mov (`PresetLibrary.cs:431-434`).
- Plan kabı `StreamMapping.ContainerFor` ile yalnız "izleri koru" (KeepAllTracks) seçeneğinden türüyor: koru → Mkv, değilse Mp4. Kodlama aşamasında kap çıktı dosyasının uzantısından da okunuyor (`ContainerOf`: .mkv/.webm/.mov/.mp4); MOV ailesi MP4 gibi davranıyor.
- WebM için kodlama kolu yok: WebM ses listesi opus/vorbis, video tarafında vp9 kodlayıcı ürün merdiveninde yok (AV1 var). Yani WebM profili bugün kurulabilir bir plan üretmez.

## Seçenekler
A) Kapı yok say, alanı içe aktarımda da düşür; içe aktarma notu "kap taşınmadı" desin (bugünkü davranışın dürüst hali).
B) Mp4/Mov/Mkv'yi uygula: CLI'da `--cikti` verilmediyse varsayılan uzantı profilin kabı; pencerede küçültme çıktısının uzantısı. Mkv, izleri koru'yu açmadan yalnız kabı değiştirir. WebM → Mp4'e düşer ve içe aktarma notu bunu söyler.
C) B + WebM için AV1+opus kolu açmak (yeni kodlama yolu, ölçüm ister).

## Soru
Hangisi? Kullanıcı kolaylığı açısından ve "kullanıcıya yalan yok" ilkesi (vaat edilen = yapılan) açısından gerekçeyle, tek öneri.
