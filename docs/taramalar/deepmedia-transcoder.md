Tema: mobil sikistirma · kaynak: mobil-sikistirma.md

# Mobil paylaşım için video sıkıştırma — üç depo

`natario1/Transcoder` boş kabuk (2 yıldız, lisanssız, yalnız README + docs, sürüm yok). Gerçek kod **`deepmedia/Transcoder`**'da; tarama onun üstünden yapıldı.

**Ortak bulgu:** üçünde de hedef boyut girişi yok. Hepsi "bit hızını düşür, boyut düşsün" mantığında. VidShrink'in bütçe + doldurma bandı zaten öndedir; buradan alınacak şey formül değil, merdiven değerleri ve emniyet sınırları.

## deepmedia/Transcoder — `natario1/Transcoder` yerine
Apache-2.0 · 863 yıldız · 32 açık issue · son push ve `v0.11.2` 2024-11-05.

**Ne yapıyor.** Genel amaçlı MediaCodec transcoder (kırpma, birleştirme, hız). Hedef boyut hesabı yok; hazır ayarlar sabit: `for720x1280()` = 720×1280 · 2 Mbps · 30 fps · 3 sn anahtar kare, `for360x480()` = 360×480 · 500 kbps · 30 fps · 3 sn. Bunun yerine Resizer zinciri: `Exact` · `AspectRatio` · `Fraction(0..1)` · `AtMost(limit)` · `PassThrough`, `MultiResizer` ile sıralanır. Kare hızı daima `min(girdi, hedef)`. Bit hızı `BITRATE_UNKNOWN` bırakılırsa tahmin edilir.

**Alınacak — bu taramanın en değerlisi.** **Validator + `PASS_THROUGH`**: MIME, çözünürlük, fps ve anahtar kare aralığı zaten hedefe uygunsa iz yeniden kodlanmaz, kopyalanır; hiçbir iz işlenmeyecekse iş `SUCCESS_NOT_NEEDED` ile *başlamadan* biter. VidShrink karşılığı: dosya zaten hedefin altında ve codec uyumluysa kodlama başlatma, "gerek yoktu" de. Ek olarak `WriteVideoValidator` mantığı — yalnız ses sıkıştırmak boyutu kayda değer düşürmüyorsa video izine dokunma. Bu, en sık yaşanacak sessiz kalite kaybını kapatır.

**Alınmayacak.** İki sabit ön ayarı hedef boyut yerine koymak; otuz saniyelik ve üç dakikalık videoya aynı bit hızını verirler.

**Nereye dokunur.** `src/VidShrink.Core/CompressionStrategy.cs` + `PlanCalculator.cs` (pass-through kararı) · `src/VidShrink.Ffmpeg/EncodeRunner.cs` (`-c copy` yolu) · `tests/VidShrink.Tests/PlanCalculatorTests.cs`.

## Platform yeniden kodlaması — aranan bulunamadı
Üç deponun **hiçbirinde** WhatsApp/Discord'un yüklemeden sonra dosyayı yeniden kodlamasına karşı öneri, belge veya ayar yok. Dolaylı sinyaller: LightCompressor H.264 Baseline'a düşebiliyor ve `isStreamable` ile `moov`'u başa alıyor; Transcoder anahtar kare aralığını 3 sn'ye sabitliyor; üçü de H.264 + AAC + MP4 dışına çıkmıyor. Çıkarım: hedefin biraz altını hedefle, H.264 + AAC + faststart varsayılanını koru, paylaşım niyetinde AV1/HEVC'yi varsayılan yapma. Bu bir ölçüm değil, ortak varsayım — **doğrulanmadı**, VidShrink kendi testiyle sınamalı.

## Kaynaklar
`gh api repos/{AbedElazizShe/LightCompressor, natario1/Transcoder, deepmedia/Transcoder, numandev1/react-native-compressor}` ve `/releases/latest` (2026-08-22) · LightCompressor `config/VideoResizer.kt`, `utils/CompressorUtils.kt`, `compressor/Compressor.kt`, README · Transcoder `strategy/DefaultVideoStrategies.java`, `strategy/DefaultVideoStrategy.java`, `docs/track-strategies.mdx`, `docs/validators.mdx` · react-native-compressor `android/…/Video/VideoCompressionProfile.kt`, `android/…/Video/AutoVideoCompression.kt`, README
