# VidShrink.Tests

Tek test projesi. `dotnet test` tamamı yeşil olmadan teslim yok; paralel koşum kapalı
(`LanguageTests.cs` içindeki assembly özniteliği).

- Zamanlama ölçen testleri yük altında okuma; yerelde filtreli koş, tam süit CI'da.
- Çıktı ve kanıt dosyaları `.calisma/` altına (`GirdiKanit`, `MotorKanit`).
- `OynaticiMotorTests.cs` — libmpv motoru: başsız kare, bozuk dosya, exact/keyframe inişi,
  geç işlenen SEEK olayı (`BeforeEvent` iç kancası), aramadan kalan geç `time-pos` olayı
  (`OnTimePosChanged` iç girişi), oynarken aramanın karesi (referans
  kareyle bayt eşitliği), PlayerView karesi, A/V farkı (audio-delay negatif kontrolü), 10 tık birikmesi, arama
  medyanları (1080p ≤60 ms, 2160p ≤200 ms, HEVC 1080p sayı), Dispose sırasında okuma
  yarışı. libmpv ya da ffmpeg yoksa kırmızı olur, atlanmaz. Yerelde `VIDSHRINK_LIBMPV` ister.
  1080p/2160p ve 10 tıkın 150 ms eşikleri `[HedefMakineFact]`: CI'da (`GITHUB_ACTIONS`) hep
  atlanır, yerelde yalnız sessiz makinede koşar; 10 tıkın birikme ve gösterim şartı CI'da da koşar. `[QuietMachineFact]` CI'yı ayırmaz; koşucu boş okununca koştu.
- `OynaticiKurulumTests.cs` — libmpv konum sırası, kurucuların libmpv sabitleri CI ile aynı,
  `EngineFactory` ile açılamayan motor atılır.
- `OynaticiGorunumTests.cs` — 3. dalga; ayar/son dosyalar `.calisma/dalga3/gecici` altina yazar.
- `OynaticiKarsilastirmaTests.cs` — karşılaştırma paneli iki motor örneğinde: şerit kodlu
  klipte iki yarının kare farkı ≤1; yarı güncel bileşik kare ortağı gelmeden yayınlanmaz (elle
  sürülen sahte motor); eski ffmpeg borusuna ve NAudio'ya canlı başvuru yok.
