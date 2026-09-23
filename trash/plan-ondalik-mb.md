# Plan — Hedef Boyutun Birimi Ondalık MB (O2)

Karar: `docs/netlestirme/025-mb-birimi.md`. 1 MB = 1 000 000 bayt; etiket "MB" kalır.

- **Core `Megabayt`**: tek yer — `Bayt = 1 000 000`, `Kbit = 8000`, `Oku(bayt)`, `Tavan(mb)`.
- **Core `PlanCalculator`**: `KbitPerMib 8388,608` → `Megabayt.Kbit`; `ContainerOverhead` ayrı
  model (mux payı) olarak kalır. `StreamMapping` geçiş bütçesi aynı katsayıya.
- **Core `MediaInfo.FileSizeMb`**, `RecorderArguments.LimitBytes`, `RecorderBudget.From`: ondalık.
- **Ffmpeg**: `EncodeRunner` her çıktı okuması ve `targetBytes`, `DiskSpaceGuard.RequiredBytes`,
  `RecorderSession` çıktı okuması: ondalık. `CalibrationProbe` ffmpeg sonekini ayrıştırıyor, kalır.
- **App/Cli**: disk alanı iletisi, kesit "kalan boyut" okuması ondalık. Paylaşım tavanı
  (`Bicim.Boyut.Bayt`, bayt → MiB) ayrı alan, kalır.
- **Yonga**: uguu 128 → 134 MB; `Chip128` → `Chip134`, `main.chip.128.*` → `main.chip.134.*`
  42 dilde, ipucu "134 MB".
- **Araçlar**: `tools/VidShrink.Bench`, `tools/VidShrink.Ab` aynı katsayıya.
- **Belge**: `docs/olcumler/onayar-kaynaklari.md` MiB cümlesi ve uguu satırı.
- **Test**: `OndalikMbTests` (olumsuz kontrol 1, 2, 4); 1024²/8388,608 pimleyen testler ve
  `N * 1024 * 1024` kaynak kalıpları ondalığa taşınır (kaynağın `FileSizeMb`'si aynı kalır).
  Mutasyon: katsayı 8388,608'e, `targetBytes` 1024²'ye — ikisi de kırmızı olmalı.
- **Kapsam dışı**: kabuk menüsünün 1024/2048 hızlı hedefleri ("1 GB" etiketi) — kayıtlı menü
  girdileri bu sayıları argüman olarak taşıyor; değiştirmek kullanıcı kararı.
