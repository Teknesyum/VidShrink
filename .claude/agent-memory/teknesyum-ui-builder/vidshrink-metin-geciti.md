---
name: vidshrink-metin-geciti
description: VidShrink'te görünen metne toplu kural uygularken elle dizge düzenleme yerine tek geçit kullan; sözlük anahtarı tuzağı ve owns dışı Theme.axaml sınırı
metadata:
  type: project
---

VidShrink'te "her metinde şu kural geçerli olsun" tipi bir iş geldiğinde yüzlerce dizgeyi
elle düzenleme. `LanguageCatalog` içine tek bir dönüştürücü koy ve şu beş yerden geçir:
açılışta pencere ağacının tamamı, dil değişiminde aynı ağaç, `T()`, `Localize()`, plan
satırları. Sözlük anahtarları da statik kuruluşta aynı geçitten geçmeli.

**Why:** T25'te büyük harf kuralı 142 sözlük çiftini, 108 XAML `Text` ve 74 `Content`
değerini birden ilgilendiriyordu. Tek geçit hepsini kapattı, kaynak okunabilir kaldı ve
kural tek yerde sınanabilir oldu.

**How to apply:**

- **Sözlük anahtarı tuzağı:** `TranslateTree` sözlükte ekrandaki metnin birebir eşleşmesini
  arıyor. Ekranda gösterilen metni dönüştürüp sözlük anahtarını dönüştürmezsen çeviri
  sessizce kırılır — metin İngilizce kalır, hata çıkmaz. Anahtarı ve değeri aynı
  dönüştürücüden geçir, ayrıca aramada ham metni de dene.
- **Owns dışı metin kaynağı:** `Themes/Theme.axaml` içinde `<x:String x:Key="AiHintText">`
  gibi paylaşılan metin sabitleri var. Bunlar çoğu arayüz sözleşmesinin `owns` listesinde
  değil. Sözlükteki karşılığını değiştirirsen XAML tarafı eşleşmez. Ya sözlüğü olduğu gibi
  bırak ya da `Window.Resources` içinde gölgele.
- Türkçe büyütme `CultureInfo.GetCultureInfo("tr-TR")` ister; `ToUpperInvariant()` `işlem`
  kelimesini `Işlem` yapar.
- Renkli `Run` taşıyan `TextBlock` ağaç geçidinde atlanmalı, yoksa `Text` yazımı inline
  renkleri siler.

İlgili: [[vidshrink-arayuz-dogrulama]], [[avalonia-tema-tuzaklari]]
