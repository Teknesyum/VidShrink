---
name: vidshrink-owns-listesi-daraltiyor
description: VidShrink sözleşmelerinde owns listesi Theme.axaml/Controls.axaml/csproj'u kapsamıyor; yeni tema anahtarı ve paket eklenemez, tasarım mevcut belirteçlerle kurulur
metadata:
  type: project
---

VidShrink arayüz sözleşmelerinin `owns` listesi genellikle yalnız `MainWindow.axaml`,
`MainWindow.axaml.cs`, `LanguageCatalog.cs` ve iki test dosyasını veriyor.
`Themes/Controls.axaml`, `Themes/Theme.axaml` ve `.csproj` dosyaları dışarıda kalıyor.

**Why:** Tema anahtarları ve paket başvuruları başka sözleşmelerin alanı; ikisi aynı anda
koşuyor ve çakışma çıkıyor.

**How to apply:**

- Yeni bir `ControlTheme` anahtarı uydurma. Mevcut olanlardan seç: `Label`, `Hint`,
  `Body`, `TipText`, `MonoValue`, `PlanFactLabel`, `PlanFactValue`, `ChipButton`,
  `PanelRule`, `Panel`, `H2`, `H3`, `InfoButton`, `GhostButton`.
- Yerel bir düzeltme gerekiyorsa `Theme="{StaticResource Label}" Margin="0"` gibi tek
  özellik ezmesi yap; yeni ölçü değil, mevcut ölçüyü sıfırlama.
- `Button` içine konan tema**siz** `TextBlock`, düğmenin yazı tipini ve boyutunu miras
  alır. Yonga içinde mono görünüm için ayrı tema gerekmez.
- `Avalonia.Headless` bağlı **değil** ve `.csproj`'a dokunulamıyor, bu yüzden test
  projesinden `MainWindow` açılamaz. Görünürlük ve düzen kuralları saf veri katmanına
  (kayıt/enum döndüren bir fonksiyona) çekilip oradan ölçülmeli; yerleşim için
  [[vidshrink-pencere-ici-olcum]] deseni kullanılır.
- Test projesi `VidShrink.App`'e başvuruyor ve `InternalsVisibleTo("VidShrink.Tests")`
  `LanguageCatalog.cs` içinde ilan edilmiş, yani `internal` tipler doğrudan test edilebilir.
