---
name: vidshrink-tema-olcumu
description: Avalonia temasını ve boyayıcıyı pencere açmadan ölçme — StyleInclude.Loaded, blokun kendi Resources'ı, XDocument taraması
metadata:
  type: project
---

Tema davranışını sınamak için pencereye gerek yok, kaynak ağacı tek başına yüklenebilir:

- `new StyleInclude(new Uri("avares://VidShrink.App/")) { Source = ...Themes/Controls.axaml }`
  → `(Styles)include.Loaded`, sonra `TryGetResource(key, ThemeVariant.Dark, out …)`.
  `Theme.axaml` bu sözlüğe zaten birleşik, belirteçler de oradan çıkıyor.
  Öncesinde Avalonia kurulmalı — `AppHost.Ensure()` (tests/VidShrink.Tests/AppHost.cs).
- `Styles.Resources`'ı bir blokun `MergedDictionaries`'ine ekleme: "already has a parent"
  atar. Çözülmüş nesneyi tek tek `block.Resources.Add(key, value)` ile koy.
- Boyayıcı fırçayı `block.TryFindResource(...)` ile arıyorsa ölçüm bloğu ağaca bağlamadan
  koşabilir; `MainWindow.PaintBullets` bu yüzden static ve pencereden bağımsız.
- `Run.Foreground` varsayılanı `null` değil `Black`; "boyanmadı" testi `Assert.Null` ile
  değil vurgu fırçasına `NotSame` ile yazılır.
- Ekrandaki blokların hangi temayı taşıdığını regexle değil `XDocument` ile tara
  (`XNamespace "https://github.com/avaloniaui"`), öznitelik sırası oynadığında kırılmaz.

**Neden:** T29 denetimi kaynak metni regexle arayan envanteri kırılgan bulup geri çevirdi.
**Nasıl uygulanır:** arayüz kuralı ölçülecekse önce bu yolu dene; `Window` açmak ancak
düzen/tıklama kanıtı gerektiğinde (bkz. [[vidshrink-ekran-disi-kosu]]).
