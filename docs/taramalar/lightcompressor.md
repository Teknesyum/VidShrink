Tema: mobil sikistirma · kaynak: mobil-sikistirma.md

# Mobil paylaşım için video sıkıştırma — üç depo

`natario1/Transcoder` boş kabuk (2 yıldız, lisanssız, yalnız README + docs, sürüm yok). Gerçek kod **`deepmedia/Transcoder`**'da; tarama onun üstünden yapıldı.

**Ortak bulgu:** üçünde de hedef boyut girişi yok. Hepsi "bit hızını düşür, boyut düşsün" mantığında. VidShrink'in bütçe + doldurma bandı zaten öndedir; buradan alınacak şey formül değil, merdiven değerleri ve emniyet sınırları.

## AbedElazizShe/LightCompressor
Apache-2.0 · **arşivlenmiş** · 587 yıldız · 38 açık issue · son push ve son etiket `1.3.3` 2024-08-17.

**Ne yapıyor.** Telegram for Android kodundan türetilmiş MediaCodec sarmalayıcısı. Kalite = kaynak bit hızının çarpanı: VERY_HIGH ×0.6 · HIGH ×0.4 · MEDIUM ×0.3 · LOW ×0.2 · VERY_LOW ×0.1. Ölçek merdiveni uzun kenara göre: ≥1920 → ×0.5 · ≥1280 → ×0.75 · ≥960 → ×0.95 · altı → ×0.9. Kare hızı ve I-frame aralığı kaynaktan kopyalanır, düşürülmez. Kaynak ≤2 Mbps ise işi reddeder — arka arkaya sıkıştırmanın videoyu çamura çevirmesini engellemek için. H.264 Baseline'a düşme yolu var.

**Alınacak.** (a) Ölçek merdiveni, VidShrink'in `MinScale=0.25` / `ScaleStep=0.02` sürekli aramasına **başlangıç tahmini** olur; ilk denemeyi doğru yere koyar. (b) 2 Mbps altı girdide "zaten sıkıştırılmış, tekrar kodlamak bozar" uyarısı — VidShrink yine kodlasın, kullanıcı bilerek onaylasın.

**Alınmayacak.** Sabit çarpan. 40 Mbps girdide ×0.3 hâlâ 12 Mbps'tir; hedef boyutu olan üründe yanlış eksen. Kare hızını hiç düşürmemesi de 60 fps kaynakta bütçeyi harcar.

**Nereye dokunur.** `src/VidShrink.Core/PlanCalculator.cs` (merdiven tohumu) · `src/VidShrink.App/MainWindow.xaml.cs` + `LanguageCatalog.cs` (düşük bit hızı uyarısı).

## Platform yeniden kodlaması — aranan bulunamadı
Üç deponun **hiçbirinde** WhatsApp/Discord'un yüklemeden sonra dosyayı yeniden kodlamasına karşı öneri, belge veya ayar yok. Dolaylı sinyaller: LightCompressor H.264 Baseline'a düşebiliyor ve `isStreamable` ile `moov`'u başa alıyor; Transcoder anahtar kare aralığını 3 sn'ye sabitliyor; üçü de H.264 + AAC + MP4 dışına çıkmıyor. Çıkarım: hedefin biraz altını hedefle, H.264 + AAC + faststart varsayılanını koru, paylaşım niyetinde AV1/HEVC'yi varsayılan yapma. Bu bir ölçüm değil, ortak varsayım — **doğrulanmadı**, VidShrink kendi testiyle sınamalı.

## Kaynaklar
`gh api repos/{AbedElazizShe/LightCompressor, natario1/Transcoder, deepmedia/Transcoder, numandev1/react-native-compressor}` ve `/releases/latest` (2026-08-22) · LightCompressor `config/VideoResizer.kt`, `utils/CompressorUtils.kt`, `compressor/Compressor.kt`, README · Transcoder `strategy/DefaultVideoStrategies.java`, `strategy/DefaultVideoStrategy.java`, `docs/track-strategies.mdx`, `docs/validators.mdx` · react-native-compressor `android/…/Video/VideoCompressionProfile.kt`, `android/…/Video/AutoVideoCompression.kt`, README
