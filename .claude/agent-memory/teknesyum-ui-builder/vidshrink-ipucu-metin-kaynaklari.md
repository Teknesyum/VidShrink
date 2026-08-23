---
name: vidshrink-ipucu-metin-kaynaklari
description: VidShrink'te ipucu metni üç ayrı yerde yaşıyor; yalnız XAML'e bakan çeviri ölçümü sessizce yeşil veriyor
metadata:
  type: project
---

VidShrink'te bir ipucunun İngilizce metni üç yerden gelebilir:

1. `MainWindow.axaml` içinde `Theme="{StaticResource TipText}" Text="..."` (çoğu)
2. `Themes/Theme.axaml` içinde `<x:String x:Key="AiHintText">` gibi paylaşılan sabitler
3. `MainWindow.axaml.cs` içinde `HardwareTipEnglish`, `NoHardwareTipEnglish`,
   `AutoUpdateEffectEnglish`, `NoSelfUpdateEffectEnglish` sabitleri — bunlar çalışma
   anında `Localize()` ile yazılıyor ve XAML'deki varsayılan metni eziyor

**Why:** T26'da sözleşme "9 ipucunun karşılığı yok" diyordu; XAML tarafında hepsi vardı,
arıza yalnızca 3. gruptaydı. T25 XAML ile sözlüğü madde biçimine çevirmiş ama arka koddaki
sabitleri paragraf bırakmıştı, `Localize()` araması tutmuyordu ve kimse fark etmemişti.
Sadece XAML'i tarayan bir ölçüm bu arızayı göremez, yeşil verir.

**How to apply:** Çeviri kapsamını ölçerken üç kaynağı da tara. Bunu yapan test
`tests/VidShrink.Tests/TipTranslationTests.cs`; yeni bir metin kaynağı eklersen desenine
de ekle, yoksa kapı yine sessizleşir. Bkz. [[vidshrink-metin-geciti]].

**Balon ölçüleri (T26, 1920x1080):** İpucu balonunun tavanı `TooltipMaxWidth` (460).
`TipMaxWidth` (420) balona ait değil, açılış paneline ait — `tools/VidShrink.SplashGen`
ve `SplashTests` onu okuyor, değiştirme. Ölçülen balon genişlikleri 447-460 arasında
değişiyor, yani kutu içerikten besleniyor. Ekranın sağ kenarındaki `?` düğmesinde balon
sol değil **sağ** kenara hizalanıyor (Avalonia taşmayı böyle önlüyor); bu genişletmeden
önce de böyleydi.

**Yakalama tuzağı (T26):** `EnumWindows` ile ana pencereyi ararken başlığı "VidShrink"
olan **ilk** pencereyi alma — ipucu balonu da aynı başlığı taşıyor ve listede önce
gelebiliyor. En büyük alanlıyı seç. Ayrıca `AutomationElement.BoundingRectangle` pencere
ön planda değilken genişlik 0 dönüyor; ölçülü düğme sayısı beşi geçene kadar öne alıp
yeniden dene. Bkz. [[vidshrink-arayuz-dogrulama]].
