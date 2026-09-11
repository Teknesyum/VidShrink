# VidShrink.Tests

Tek test projesi. `dotnet test` tamamı yeşil olmadan teslim yok; paralel koşum kapalı
(`LanguageTests.cs` içindeki assembly özniteliği).

- Zamanlama ölçen testleri yük altında okuma; yerelde filtreli koş, tam süit CI'da.
- Çıktı ve kanıt dosyaları `.calisma/` altına (`GirdiKanit`, `MotorKanit`).
- `OynaticiMotorTests.cs` — libmpv motoru: başsız kare, bozuk dosya, exact/keyframe inişi,
  PlayerView karesi, A/V farkı (audio-delay negatif kontrolü), 10 tık birikmesi, arama
  medyanları (1080p ≤60 ms, 2160p ≤200 ms, HEVC 1080p sayı), Dispose sırasında okuma
  yarışı. libmpv ya da ffmpeg yoksa kırmızı olur, atlanmaz. Yerelde `VIDSHRINK_LIBMPV` ister.
- `OynaticiKurulumTests.cs` — libmpv konum sırası, kurucuların libmpv sabitleri CI ile aynı,
  `EngineFactory` ile açılamayan motor atılır.
- `OynaticiBoruTests.cs` — ffmpeg borusu (`DecoderPipe`); karşılaştırma paneli hâlâ kullanır.
