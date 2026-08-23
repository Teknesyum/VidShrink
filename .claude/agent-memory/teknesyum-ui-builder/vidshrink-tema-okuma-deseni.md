---
name: vidshrink-tema-okuma-deseni
description: VidShrink'te Theme.axaml'ı düzenli ifadeyle okuyan iki yer var ve etiket listesi eksik kalırsa derleme MSB3073 ile düşer
metadata:
  type: project
---

`Theme.axaml`'ı Avalonia'sız, düzenli ifadeyle okuyan iki yer var ve ikisi de kabul
ettiği XAML etiketlerini elle sayıyor: `tools/VidShrink.SplashGen/Program.cs`
(`Theme.Read`) ve `tests/VidShrink.Tests/SplashTests.cs` (`ReadTheme`). Listede olmayan
bir etiketle yazılmış belirteci istersen `KeyNotFoundException` alırsın; başlatıcı
derlemesi bunu `MSB3073` olarak gösterir çünkü üreteç derleme adımında koşuyor.

Temadaki etiketler türe göre değişiyor: renkler `<Color>`, ölçüler `<x:Double>`, süreler
`<sys:TimeSpan>`, sayımlar `<x:Int32>`, gölgeler `<BoxShadows>` (ayrı desen).

**Why:** Panel görüntüsü derleme sırasında temadan üretiliyor ve testler PNG'nin `tEXt`
bölümündeki belirteçleri tema değerleriyle karşılaştırıyor — kasıtlı bir kapı.

**How to apply:** Panele yeni bir belirteç bağlarken önce temadaki etiketine bak, sonra
iki desene de ekle. Testi gevşetme; liste güncellenir. Bkz. [[avalonia-tema-tuzaklari]].
