---
name: avalonia-thickness-belirteci
description: Thickness belirtecini x:Double belirtecinden türetmenin yolu yok; derleme geçer, çalışma zamanı InvalidCastException ile düşer
metadata:
  type: feedback
---

Bir `Thickness` kaynağını (`DropZonePadding`) bir `x:Double` kaynağından (`SpaceMd`)
türetmek Avalonia biçimlemesinde mümkün değil. `Margin="{StaticResource SpaceMd}"`
**derlemeyi geçer**, çalışma zamanında `XamlDynamicSetter` içinde
`InvalidCastException` ile düşer. `<Thickness x:Key="...">{StaticResource ...}</Thickness>`
de çalışmaz; `Thickness` özellikleri salt okunur, nesne öğesi sözdizimiyle kurulamaz.

**Why:** Denetçi "aynı sayı iki yerde, biri değişirse diğeri sessizce ayrışır" diye borç
açtı; doğrudan bağlama denendi ve kırıldı.

**How to apply:** Aynı sayıyı iki belirteçte tutmak zorundaysan bağı ölçümle kur — testte
iki kaynağı okuyup eşitliğini doğrula. Ayrışırsa test kırmızı olur, sessizce geçmez.
Yeni bir dönüştürücü/işaretleme uzantısı dosyası açmak `owns` dışına taşar.

İlgili: [[avalonia-tema-tuzaklari]]
