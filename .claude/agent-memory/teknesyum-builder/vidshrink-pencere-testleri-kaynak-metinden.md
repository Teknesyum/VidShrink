---
name: vidshrink-pencere-testleri-kaynak-metinden
description: VidShrink test projesi MainWindow'u acamaz (Avalonia.Headless yok) - arayuz olcumu kaynak metni okur, is mantigi Avalonia'ya dokunmayan ayri bir sinifa cikarilir
metadata:
  type: project
---

`tests/VidShrink.Tests` `VidShrink.App`'e basvurur ve `InternalsVisibleTo` ile
internal turleri gorur, ama `MainWindow` ornegi **kuramaz**: `Avalonia.Headless`
bagli degil. Yerlesim olcen testler (`WindowLayoutTests`, `ChipTests`) kendi
duzeneklerini `AppHost` uzerinden kurar; geri kalan arayuz olcumleri
`TipSources.WindowXamlPath` / `WindowCodePath` ile **kaynak dosyayi metin olarak** okur.

**Why:** Pencere gercek bir platform istiyor; xUnit is parcacigi havuzunda kurulum
kararsizlasiyor.

**How to apply:** Arayuze yeni bir is baglarken mantigi `MainWindow.axaml.cs` icinde
Avalonia turu gecmeyen ayri bir `internal` sinifa cikar (ornek: `ShareFlow`,
`QualityHint`, `ShareTargetTable`) - o sinif dogrudan olculebilir. Dugmenin gercekten
bagli oldugu ise XAML/kod metninde `Click="OnX"` arayarak sabitlenir.
Bkz. [[vidshrink-app-ikiz-share-turleri]].

**Yan etki - siniftaki yer onemli:** bu testlerin bazisi dosyayi iki isaret arasindan
dilimler (`QualityHintTests.TheScorePathStartsNoProcess`, `QualityHint` ile `ShareTarget`
arasi). Iki isaretin arasina yeni bir sinif koyarsan o testi kirarsin, testin iddiasi
dogru kalsa bile. Yeni tur eklerken **dosyanin sonuna** koy.
