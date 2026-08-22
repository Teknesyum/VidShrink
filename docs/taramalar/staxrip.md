Tema: hedef boyut GUI · kaynak: hedef-boyut-gui.md

# Hedef dosya boyutuna kodlayan masaüstü araçları

## staxrip/staxrip

**Künye** — MIT, son push 2026-08-08, 2.968 yıldız, 158 açık issue, son sürüm v2.52.5 (2026-08-08). Konumuza en yakın depo bu.

**Ne yapıyor** — Windows/.NET masaüstü encoder GUI'si; hedef boyuttan bitrate hesaplıyor ve **Compressibility Check** ile kalibre ediyor. İki mekanizma:

*Boyut muhasebesi* (`Source/General/Misc.vb`, `Calc`): video bayt payı = hedef − HDR10+/Dolby Vision yan dosyaları − ses − altyazı (kaynak boyutun 1/3'ü tahmini) − konteyner overhead. Overhead **kare başına** modelleniyor: mp4 ≈ 0,013 KiB/kare, mkv ≈ 0,014 KiB/kare, avi ≈ 0,024 KiB/kare + ses izi başına 0,04 KiB/kare.

*CompCheck* (`Source/Encoding/VideoEncoder.vb`): kaynaktan `SelectRangeEvery(every, range)` ile örnek çıkarıyor — `range = fps × 2 sn`, `every = (100 / 5) × range`, yani varsayılan olarak videonun **%5'i, 2 saniyelik bloklar hâlinde**. Örneği sabit kalitede kodlayıp `Compressibility = bit / kare / piksel` ölçüyor. Plandaki `BPF = bitrate×1000/(w×h×fps)`; `Percent = BPF / Compressibility × 100`. Sonra iki eylemden biri: **AdjustFileSize** (hedef boyutu 1 MB adımlarla artırıp Percent ≥ 50 olan ilk değeri seçiyor) veya **AdjustImageSize** (genişliği 16 px azaltarak Percent hedefe çıkana kadar küçültüyor, 720 px tabanında duruyor).

**Alınacak fikir** — (1) **Kare başına konteyner overhead'i.** VidShrink'te `ContainerOverhead = 0.995` sabit çarpanı var; kısa/yüksek FPS klipte bu yanlış tarafa sapar, kare sayısına bağlı model daha doğru ve bandın alt ucunu güvenle yukarı çeker. (2) **Karmaşıklık ölçüsünü "bit/kare/piksel" olarak normalize etmek** — çözünürlük ve FPS değiştiğinde aynı sayı karşılaştırılabilir kalır, bu da yeniden boyutlandırma kararını ölçüme bağlar. (3) **İki ayrı düzeltme ekseni**: bitrate yetmiyorsa ya hedefi ya çözünürlüğü oynat; hangisinin oynayacağı açık bir politika olarak duruyor, kodun içine gömülü değil.

**Alınmayacak** — `GetAutoSize`'ın 1'den 100.000'e lineer taraması (kapalı formülle çözülür), 720 px alt sınırı (16 MB WhatsApp hedefinde anlamsız), AviSynth/VapourSynth script motoru bağımlılığı, ve `%50` gibi kaynağı belgelenmemiş eşik sabitleri — bunları kendi ölçümümüzle yerine koymalıyız, kopyalamamalıyız.

**VidShrink'te nereye dokunur** — `src/VidShrink.Core/PlanCalculator.cs` (`ContainerOverhead`, `FillBand`, `RetryAimMb`), `src/VidShrink.Core/ComplexityProfile.cs` ve `src/VidShrink.Ffmpeg/ComplexityProbe.cs` (bit/kare/piksel normalizasyonu, pencere örnekleme oranı), `src/VidShrink.Ffmpeg/CalibrationProbe.cs` (örnek kodlamadan ölçüm), `src/VidShrink.Core/CompressionStrategy.cs` (boyut mu çözünürlük mü ekseni).

## Kaynaklar

- `gh api repos/HandBrake/HandBrake` + `/releases/latest`; `NEWS.markdown` (0.9.6 bölümü); `libhb/preset.c`, `gtk/src/videohandler.c`, `macosx/HBVideo.m`
- `gh api repos/Kagami/webm.py` + `/releases/latest` (404); `webm.py` — `_calc_video_bitrate`, `_vorbisq2bitrate`, `_encode`, `print_stats`
- `gh api repos/staxrip/staxrip` + `/releases/latest`; `Source/General/Misc.vb` (`Calc`), `Source/General/Project.vb` (varsayılanlar), `Source/General/GlobalClass.vb` (`GetAutoSize`), `Source/Encoding/VideoEncoder.vb` (`RunCompCheck`, `AutoSetImageSize`)
- VidShrink karşılaştırması: `src/VidShrink.Core/PlanCalculator.cs` (satır 24-43)
