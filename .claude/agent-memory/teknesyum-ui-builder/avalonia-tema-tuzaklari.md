---
name: avalonia-tema-tuzaklari
description: VidShrink Avalonia 11 temasında ekranda görülüp düzeltilen native sızıntılar ve WPF'ten farklı davranan noktalar
metadata:
  type: project
---

VidShrink arayüzü T11'de WPF'ten Avalonia 11.3.20'ye taşındı. Derlemenin geçmesi
yetmedi; şunlar ancak ekran görüntüsünde ortaya çıktı.

**Native sızıntı: `FocusAdorner`.** Kendi `ControlTheme`'ini yazınca Avalonia'nın
varsayılan beyaz odak dikdörtgeni kontrolün çevresinde kalıyor. Her etkileşimli temaya
`<Setter Property="FocusAdorner" Value="{x:Null}"/>` konmalı, yoksa neon halkanın
dışında beyaz bir kutu duruyor.

**`MaxWidth` verilen `TextBlock` ortalanıyor.** Avalonia'da varsayılan
`HorizontalAlignment="Stretch"`; `MaxWidth` ile daraltılan metin panelin ortasına
kayıyor. `TipText` gibi temalara `HorizontalAlignment="Left"` açıkça yazılmalı.

**20x20 onay kutusunda 8 yarıçap daire okunuyor.** Radyo düğmesiyle karışıyor;
çip yarıçapı (6) doğru değer.

**Gradyanlarda nokta relative değil.** WPF `StartPoint="0,0.5"` göreli, Avalonia'da
mutlak piksel. `0%,50%` yazılmalı, yoksa gradyan görünmez. `ColorInterpolationMode`
Avalonia'da hiç yok.

**Doğrulama yolu — deneme projesi.** `MainWindow` başka bir ajandayken tema tek başına
doğrulanabiliyor: geçici klasörde `AssemblyName`i `VidShrink.App` olan bir csproj kur,
gerçek `Theme.axaml`/`Controls.axaml`/`App.axaml`/`Program.cs` dosyalarını `Link` ile
bağla, kendi deneme penceresini yaz. `avares://VidShrink.App/...` yolları böyle çalışıyor.
Odak durumunu `Focus(NavigationMethod.Tab)` ile, hover'ı `SetCursorPos` ile zorlayıp
PrintWindow ile yakala. Açılan liste ayrı bir HWND olduğu için PrintWindow'a girmiyor;
`ComboBoxItem`leri `IsSelected`/`IsEnabled` ile statik olarak diz.

**Why:** T11'de üç kusur da derlemede görünmedi, yalnız ekran görüntüsünde çıktı.

**How to apply:** Bu depoda Avalonia teması değiştirirken önce bu dört maddeyi kontrol et,
sonra deneme projesiyle ekran görüntüsü al. Yakalama yöntemi için
[[vidshrink-arayuz-dogrulama]].

**`Window` özniteliği `OnPropertyChanged`'i erken tetikliyor (T16).** XAML'de
`WindowState="Maximized"` yazınca `OnPropertyChanged` daha `InitializeComponent` bitmeden
koşuyor; `x:Name`li denetimler henüz null olduğu için orada onlara dokunan kod
`NullReferenceException` ile uygulamayı açılışta düşürüyor. Derleme temiz geçiyor.
`MainWindow`'da bir `_controlsReady` bayrağı `InitializeComponent`ten hemen sonra
kurulmalı ve `OnPropertyChanged` bayrak yokken erken dönmeli.

**Bu projede `Expander` için `ControlTheme` yok.** Açılır bölüm gerekince temasız
`Expander` koyma — kendi başlığını ve okunu sızdırır. `GhostButton` + `IsVisible`
ile aç/kapa yap.
