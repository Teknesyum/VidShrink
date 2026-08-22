# Mobil paylaşım için video sıkıştırma — üç depo

`natario1/Transcoder` boş kabuk (2 yıldız, lisanssız, yalnız README + docs, sürüm yok). Gerçek kod **`deepmedia/Transcoder`**'da; tarama onun üstünden yapıldı.

**Ortak bulgu:** üçünde de hedef boyut girişi yok. Hepsi "bit hızını düşür, boyut düşsün" mantığında. VidShrink'in bütçe + doldurma bandı zaten öndedir; buradan alınacak şey formül değil, merdiven değerleri ve emniyet sınırları.

## AbedElazizShe/LightCompressor
Apache-2.0 · **arşivlenmiş** · 587 yıldız · 38 açık issue · son push ve son etiket `1.3.3` 2024-08-17.

**Ne yapıyor.** Telegram for Android kodundan türetilmiş MediaCodec sarmalayıcısı. Kalite = kaynak bit hızının çarpanı: VERY_HIGH ×0.6 · HIGH ×0.4 · MEDIUM ×0.3 · LOW ×0.2 · VERY_LOW ×0.1. Ölçek merdiveni uzun kenara göre: ≥1920 → ×0.5 · ≥1280 → ×0.75 · ≥960 → ×0.95 · altı → ×0.9. Kare hızı ve I-frame aralığı kaynaktan kopyalanır, düşürülmez. Kaynak ≤2 Mbps ise işi reddeder — arka arkaya sıkıştırmanın videoyu çamura çevirmesini engellemek için. H.264 Baseline'a düşme yolu var.

**Alınacak.** (a) Ölçek merdiveni, VidShrink'in `MinScale=0.25` / `ScaleStep=0.02` sürekli aramasına **başlangıç tahmini** olur; ilk denemeyi doğru yere koyar. (b) 2 Mbps altı girdide "zaten sıkıştırılmış, tekrar kodlamak bozar" uyarısı — VidShrink yine kodlasın, kullanıcı bilerek onaylasın.

**Alınmayacak.** Sabit çarpan. 40 Mbps girdide ×0.3 hâlâ 12 Mbps'tir; hedef boyutu olan üründe yanlış eksen. Kare hızını hiç düşürmemesi de 60 fps kaynakta bütçeyi harcar.

**Nereye dokunur.** `src/VidShrink.Core/PlanCalculator.cs` (merdiven tohumu) · `src/VidShrink.App/MainWindow.xaml.cs` + `LanguageCatalog.cs` (düşük bit hızı uyarısı).

## deepmedia/Transcoder — `natario1/Transcoder` yerine
Apache-2.0 · 863 yıldız · 32 açık issue · son push ve `v0.11.2` 2024-11-05.

**Ne yapıyor.** Genel amaçlı MediaCodec transcoder (kırpma, birleştirme, hız). Hedef boyut hesabı yok; hazır ayarlar sabit: `for720x1280()` = 720×1280 · 2 Mbps · 30 fps · 3 sn anahtar kare, `for360x480()` = 360×480 · 500 kbps · 30 fps · 3 sn. Bunun yerine Resizer zinciri: `Exact` · `AspectRatio` · `Fraction(0..1)` · `AtMost(limit)` · `PassThrough`, `MultiResizer` ile sıralanır. Kare hızı daima `min(girdi, hedef)`. Bit hızı `BITRATE_UNKNOWN` bırakılırsa tahmin edilir.

**Alınacak — bu taramanın en değerlisi.** **Validator + `PASS_THROUGH`**: MIME, çözünürlük, fps ve anahtar kare aralığı zaten hedefe uygunsa iz yeniden kodlanmaz, kopyalanır; hiçbir iz işlenmeyecekse iş `SUCCESS_NOT_NEEDED` ile *başlamadan* biter. VidShrink karşılığı: dosya zaten hedefin altında ve codec uyumluysa kodlama başlatma, "gerek yoktu" de. Ek olarak `WriteVideoValidator` mantığı — yalnız ses sıkıştırmak boyutu kayda değer düşürmüyorsa video izine dokunma. Bu, en sık yaşanacak sessiz kalite kaybını kapatır.

**Alınmayacak.** İki sabit ön ayarı hedef boyut yerine koymak; otuz saniyelik ve üç dakikalık videoya aynı bit hızını verirler.

**Nereye dokunur.** `src/VidShrink.Core/CompressionStrategy.cs` + `PlanCalculator.cs` (pass-through kararı) · `src/VidShrink.Ffmpeg/EncodeRunner.cs` (`-c copy` yolu) · `tests/VidShrink.Tests/PlanCalculatorTests.cs`.

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
