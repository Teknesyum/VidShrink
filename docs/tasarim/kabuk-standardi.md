# Kabuk Standardı

Arayüzün ölçülebilir kuralları. Her satırın yanındaki test `tests/VidShrink.Tests/KabukStandardiTests.cs`
içindedir; kural kaynakta bozulursa o test kırmızıya döner. Ölçülemeyen kural buraya yazılmaz.

## Renk

- Renk yalnız `Themes/Palette/<Ad>/Theme.axaml`'dan gelir; palet dışındaki hiçbir `.axaml`
  dosyasında düz onaltılık renk yazılmaz. — `RenkYalnizPaletten`
- Palette `info` adında bir renk ya da fırça belirteci yoktur; nötr bildirim varsayılan
  kenar ve beyaz yazıdır. — `InfoRengiYoktur`

## Parıltı

- Parıltı kutuya konur, yazıya hiç: `TextBlock`, `TextBox`, `Label`, `AccessText` öğelerine
  gölge-parıltı bağlanmaz. — `PariltiYaziyaKonmaz`
- Parıltı taşıyan her `Effect` bir kapsayıcıdadır: `Border`, `Path`, `Ellipse`, `Rectangle`,
  `Panel`, `Grid`. Liste ya da tablo satırı parlamaz. — `PariltiKapsayicidadir`

## Giriş

- Yer tutucu metin yok: giriş alanlarında `Watermark` kullanılmaz, görünür etiket
  konur. — `YerTutucuMetinYok`

## Bildirim

- Hata bildirimi kendi kendine kapanmaz; kapanma yalnız iş bittiğinde kurulur ve
  süresi 6 saniyedir. — `HataBildirimiKendiKapanmaz`

## Pencere

- Sistem başlık çubuğu kaldırılır, kendi çubuğumuz çizilir; kırılan her şey geri verilir:
  `WindowDecorations="BorderOnly"` kenardan boyutlandırmayı, Aero Snap'i ve `Alt+F4`'ü
  bırakır, `BeginMoveDrag` sürüklemeyi, çift tık büyütme/geri almayı verir. — `KendiBaslikCubugu`

## Bağımlılık

- Görsel tema kitaplığı yok: MUI, WPF UI, MahApps, HandyControl, Material.Avalonia,
  Semi.Avalonia, FluentAvalonia ve benzerleri hiçbir `.csproj`'de
  `PackageReference` değildir. — `GorselTemaKitapligiYok`
