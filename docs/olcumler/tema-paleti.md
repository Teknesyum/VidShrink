# Tema Paleti — Ölçüm

Tarih: 2026-09-09. Dal: `claude/tema-paleti`.

## Ne Ölçüldü

Yirmi palet `src/VidShrink.App/Themes/Palette/` altında, her biri **32 anahtar**
(28 `Color` + 4 `BoxShadows`).

Renk uydurulmadı: on dokuzu **tanınmış açık şemaların gerçek renkleri** (Dracula, Nord,
Gruvbox, Tokyo Night, Catppuccin Mocha, One Dark, Monokai Pro, Solarized Dark,
Everforest, Rosé Pine, Ayu, Night Owl, SynthWave '84, Cobalt2, Material Ocean,
GitHub Dark, Kanagawa, Horizon, Moonlight), yirmincisi programın kendi Neon'u.
Seçimi fable ajanı yaptı; girdi ve dönen JSON `docs/danisma/` yerine doğrudan
`src/VidShrink.App/Themes/Palette/seeds.json` içine yazıldı — kayıt orada, ham hâliyle.

Palet dosyaları elle yazılmıyor: kaynak **`seeds.json`**, tema başına **on bir çekirdek
renk**. Kalan 21 anahtar alfa ve karışım kuralıyla türetiliyor
(`tools/VidShrink.PaletteGen`). Elle boyama yolu `docs/tema.md`.

## Liste Neden Elle Yazılı

`AssetLoader.GetAssets` denendi ve palet klasörü için **boş döndü** — derlenmiş XAML
ham kaynak listesinde görünmüyor:

```
avares://VidShrink.App/            -> 6: Assets/VidShrink.png, Fonts/*.ttf
avares://VidShrink.App/Themes/Palette/ -> 0:
avares://VidShrink.App/Fonts/      -> 3: ...
exists(Themes/Palette/Teal.axaml)  = False
```

Bu yüzden `PaletteCatalog.Names` elle yazılı bir liste; klasörle aynı kaldığını
`PaletteTests.HerPaletAyniAnahtarKumesiniTasir` denetliyor. Palet eklenip listeye
yazılmazsa ölçü kırmızı yanar.

## Ölçüler

`dotnet test --filter PaletteTests` → **5/5 yeşil**.

| Ölçü | Ne söylüyor |
| --- | --- |
| `HerPaletAyniAnahtarKumesiniTasir` | 20 dosya, 32 anahtar, liste klasörle bire bir |
| `HerPaletKendiCekirdeginiTasir` | Her dosyanın zemini, yüzeyi, üç vurgusu ve yazısı `seeds.json`'daki çekirdekle bire bir |
| `PaletlerBirbirinindenFarkli` | Yirmi seçenek yirmi ayrı görünüş, kopya yok |
| `PaletDegisince_AyniAnahtarBaskaRengiVerir` | `NeonBlueColor` palet değişince değişiyor, geri dönünce eskiye oturuyor |
| `SecilenTemaAyarDosyasindaSaklanir` | Seçim `settings.json` içindeki `theme` anahtarında, açılışta okunuyor |

İlgili aile ölçümü (`Localization|Language|SettingsTab|AyarKaliciligi|Palette`):
**160/160 yeşil**, 12 sn.
