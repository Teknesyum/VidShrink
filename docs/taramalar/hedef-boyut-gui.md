# Hedef dosya boyutuna kodlayan masaüstü araçları

## HandBrake/HandBrake

**Künye** — GPLv2 ("çoğu dosya", LICENSE), son push 2026-08-22, 24.110 yıldız, 295 açık issue, son sürüm 1.11.2 (2026-06-07). Marka politikası depoda yok (doğrulanamadı).

**Ne yapıyor** — Genel amaçlı transcoder. Hedef boyut özelliğini *kaldırdı*: NEWS.markdown 0.9.6 bölümü "Target Size is gone, and isn't coming back / Don't bother complaining on the forums" diyor; daha önceki satırlar özelliğin tekrar tekrar bozulduğunu gösteriyor ("It keeps breaking itself"). Bugün yalnız sabit kalite (RF) veya ortalama bitrate var; boyut hesabını kullanıcı yapıyor. İki geçişte `VideoTwoPass` + `VideoTurboTwoPass` (ilk geçiş hızlandırılmış); turbo yalnız x264/x265'te destekli (`gtk/src/videohandler.c` içindeki `turbo_supported` kontrolü).

**Alınacak fikir** — Turbo ilk geçiş: analiz geçişi son geçişle aynı preset'te koşmak zorunda değil. VidShrink iki geçişli VBR'de ilk geçişi ucuzlatabilir. Ayrıca "hangi kodlayıcıda turbo geçerli" sorusunun kodlayıcı yetenek tablosunda yaşaması gerektiği fikri.

**Alınmayacak** — Hedef boyutu tamamen terk etmek. HandBrake'in gerekçesi kullanıcı beklentisiyle tek geçişlik tahminin uyuşmaması; VidShrink'in cevabı düzeltme turları, özelliği silmek değil. Preset/queue mimarisi de bizim tek işlik akışımıza ağır gelir.

**VidShrink'te nereye dokunur** — `src/VidShrink.Ffmpeg/EncoderCapabilities.cs` (turbo destek bayrağı), `src/VidShrink.Core/ConversionArguments.cs` ve `FfmpegArguments.cs` (ilk geçiş preset'i), `src/VidShrink.Core/CompressionStrategy.cs`.

## Kagami/webm.py

**Künye** — CC0-1.0, son push 2020-08-02 (terk edilmiş sayılır), 145 yıldız, 4 açık issue, GitHub'da etiketli sürüm yok (releases API 404); PyPI'da `webm` paketi var, sürüm tarihi doğrulanamadı.

**Ne yapıyor** — Tek dosyalık CLI, imageboard yükleme limitine sığdırmak için WebM üretir. `-l 10` = 10 MiB limit. Formül tek satır: `video_kbit = limit_MiB*8*1024/süre_sn - ses_kbit`, sonuç 0,1 kbit'e yuvarlanıyor, negatifse anlaşılır bir hata metniyle duruyor. **Hiç deneme yapmıyor** — tek 2 geçişli kodlama, bitti. Sonunda çıktıyı ölçüp `OVERWEIGHT: N B` veya `underweight: N B` basıyor, düzeltmeyi kullanıcıya bırakıyor. İlk geçişte `speed = max(4, speed)` (turbo eşdeğeri). Vorbis VBR'de sesin payını çıkarabilmek için sabit bir `q → kbps` eşleme tablosu tutuyor.

**Alınacak fikir** — (1) Ses bitrate'i sabit değilse bile plan hesabı için **ölçülmüş bir tahmin tablosu** gerekir; VidShrink Opus/AAC VBR seçeneği eklerse aynı sorun çıkar. (2) Hedefe sığmayan durumu kodlamadan *önce* yakalayıp "limit çok düşük / süre çok uzun / ses bitrate'i çok yüksek" diye üç somut nedenle reddetmek — bizim hata metnimizden daha kullanışlı bir kalıp.

**Alınmayacak** — Kuru bölme + tek atış. Konteyner overhead'i, karmaşıklık ve kodlayıcı sapması hesaba katılmadığı için sonuç sistematik olarak limitin bir yanına düşüyor; "underweight" durumunda kalite bedava bırakılıyor. VidShrink'in doluluk bandı tam olarak bu boşluğu kapatıyor, geri adım atmayalım.

**VidShrink'te nereye dokunur** — `src/VidShrink.Core/PlanCalculator.cs` (fizibilite ön kontrolü), `src/VidShrink.Core/MediaInfo.cs` (ses bitrate tahmini), `src/VidShrink.App/MainWindow.xaml.cs` (ret mesajı).

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
