Tema: mobil sikistirma · kaynak: mobil-sikistirma.md

# Mobil paylaşım için video sıkıştırma — üç depo

`natario1/Transcoder` boş kabuk (2 yıldız, lisanssız, yalnız README + docs, sürüm yok). Gerçek kod **`deepmedia/Transcoder`**'da; tarama onun üstünden yapıldı.

**Ortak bulgu:** üçünde de hedef boyut girişi yok. Hepsi "bit hızını düşür, boyut düşsün" mantığında. VidShrink'in bütçe + doldurma bandı zaten öndedir; buradan alınacak şey formül değil, merdiven değerleri ve emniyet sınırları.

## numandev1/react-native-compressor
MIT · 1325 yıldız · 32 açık issue · son push ve `v2.0.3` 2026-07-25. Üçünün en canlısı. Android'de LightCompressor'ı içeri kopyalayıp üstüne profil katmanı koymuş.

**`auto` formülü** (`VideoCompressionProfile.kt`):
1. Uzun kenar `maxSize`'ı (varsayılan 640) aşıyorsa `scale = maxSize / uzun kenar`, boyutlar çifte yuvarlanır; aşmıyorsa dokunulmaz.
2. `fps = clamp(kaynak fps, 1, 60)`, okunamazsa 30.
3. `bitrate = kaynak bitrate × (hedef piksel / kaynak piksel) × max(hedef fps / kaynak fps, 0.85)`.
4. `clamp(bitrate, taban, min(tavan, kaynak × 0.95))`. Uzun kenara göre taban/tavan (bps): ≥1920 → 2.0M/3.5M · ≥1280 → 1.2M/2.0M · ≥960 → 900k/1.5M · ≥720 → 700k/1.2M · altı → 500k/900k.
5. Dosya `minimumFileSizeForCompress` (varsayılan 0 MB) altındaysa hiç dokunulmaz.

**Alınacak.** (a) Bit hızını kalite etiketine değil **piksel ve kare oranına** bağlamak — ölçek düşünce bütçenin ne kadarının gerçekten gerektiğini verir. (b) **`min(tavan, kaynak × 0.95)`** tavanı: çıktı asla kaynağın %95'ini geçmesin. `FillPolicy.FillTarget` bu tavan olmadan 16 MB hedefte 8 MB'lık girdiyi şişirebilir — kalite artmaz, dosya büyür.

**Alınmayacak.** `maxSize = 640` varsayılanı (masaüstünde kabul edilemez) ve "WhatsApp gibi sıkıştırır" iddiası: dayanağı tek bir Google Sheets bağlantısı, yöntem/örneklem/tarih yok — **doğrulanamadı**. "APK'ya 50 KB ekler, FFmpeg ~9 MB ekler" iddiası da depoda ölçümle desteklenmiyor — **doğrulanamadı**, zaten ffmpeg tabanlı VidShrink'e ilgisiz. Kod içi "önceki bantlar 2-3 kat büyüktü" notu geliştiricinin kendi beyanı.

**Nereye dokunur.** `src/VidShrink.Core/PlanCalculator.cs` (`Estimate` ve `FillBand` çevresi) · `src/VidShrink.Core/EncodePlan.cs` · `tests/VidShrink.Tests/FillBandTests.cs`.

## Platform yeniden kodlaması — aranan bulunamadı
Üç deponun **hiçbirinde** WhatsApp/Discord'un yüklemeden sonra dosyayı yeniden kodlamasına karşı öneri, belge veya ayar yok. Dolaylı sinyaller: LightCompressor H.264 Baseline'a düşebiliyor ve `isStreamable` ile `moov`'u başa alıyor; Transcoder anahtar kare aralığını 3 sn'ye sabitliyor; üçü de H.264 + AAC + MP4 dışına çıkmıyor. Çıkarım: hedefin biraz altını hedefle, H.264 + AAC + faststart varsayılanını koru, paylaşım niyetinde AV1/HEVC'yi varsayılan yapma. Bu bir ölçüm değil, ortak varsayım — **doğrulanmadı**, VidShrink kendi testiyle sınamalı.

## Kaynaklar
`gh api repos/{AbedElazizShe/LightCompressor, natario1/Transcoder, deepmedia/Transcoder, numandev1/react-native-compressor}` ve `/releases/latest` (2026-08-22) · LightCompressor `config/VideoResizer.kt`, `utils/CompressorUtils.kt`, `compressor/Compressor.kt`, README · Transcoder `strategy/DefaultVideoStrategies.java`, `strategy/DefaultVideoStrategy.java`, `docs/track-strategies.mdx`, `docs/validators.mdx` · react-native-compressor `android/…/Video/VideoCompressionProfile.kt`, `android/…/Video/AutoVideoCompression.kt`, README
