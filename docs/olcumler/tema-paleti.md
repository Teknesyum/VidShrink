# Tema Paleti — Ölçüm

Tarih: 2026-09-09. Dal: `claude/tema-paleti`.

## Ne Ölçüldü

Yirmi palet `src/VidShrink.App/Themes/Palette/` altında, her biri **32 anahtar**
(28 `Color` + 4 `BoxShadows`):

```
Amber 32  Azure 32  Citron 32  Cobalt 32  Crimson 32  Emerald 32  Fern 32
Flare 32  Gold 32   Indigo 32  Jade 32    Lagoon 32   Lime 32     Magenta 32
Neon 32   Orchid 32 Rose 32    Scarlet 32 Teal 32     Violet 32
```

Renk uydurulmadı, **türetildi**: `tools/VidShrink.PaletteGen` Neon'un kendi renklerini
HSL'de 18°'lik adımlarla döndürür, doygunluk ve açıklık yerinde kalır. Neon değişirse
yirmisi de yeniden üretilir.

```
Neon  NeonBlueColor #FF00F3FF   NeonPinkColor #FFFF00EA
Teal  NeonBlueColor #FF00FFBE   NeonPinkColor #FFC800FF
```

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

`dotnet test --filter PaletteTests` → **4/4 yeşil**.

| Ölçü | Ne söylüyor |
| --- | --- |
| `HerPaletAyniAnahtarKumesiniTasir` | 20 dosya, 32 anahtar, liste klasörle bire bir |
| `PaletlerBirbirinindenFarkli` | Yirmi seçenek yirmi ayrı görünüş, kopya yok |
| `PaletDegisince_AyniAnahtarBaskaRengiVerir` | `NeonBlueColor` palet değişince değişiyor, geri dönünce eskiye oturuyor |
| `SecilenTemaAyarDosyasindaSaklanir` | Seçim `settings.json` içindeki `theme` anahtarında, açılışta okunuyor |

İlgili aile ölçümü (`Localization|Language|SettingsTab|AyarKaliciligi|Palette`):
**159/159 yeşil**, 13 sn.
