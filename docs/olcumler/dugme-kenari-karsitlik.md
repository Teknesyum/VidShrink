# Düğme Kenarı Karşıtlığı

Tarih: 7 Ekim 2026. Ölçü: WCAG 2.1 1.4.11 (metin dışı karşıtlık, eşik 3:1).
Sayıların tamamı `tests/VidShrink.Tests/DugmeKenariKarsitligiTests.cs` çıktısından alındı;
test tema dosyalarını ve 36 palet dosyasını kaynaktan okur.

## Ne Ölçüldü

Kenarı çizilen düğme temalarının dinlenme hâlindeki kenar fırçası, üç zemine harmanlanıp
(fırçanın alfası × `Opacity`, 8 bit yuvarlama) zeminle oranlandı. Zeminler: `AppBg`,
`Surface` ve `PanelSurface` (%90, altında `AppBg`).

İşin öncülü kenarın `NeonBlueBorder` olduğunu söylüyordu. Kaynakta öyle değil:
`GhostButton` tabanı `HeaderRestBorder` kullanıyordu, o da `NeonBlueBorder`'dan zayıf.
On bir düğme teması bu tabandan türüyor.

## Ne Değişti

- `Themes/Controls.axaml`: `GhostButton` dinlenme kenarı `HeaderRestBorder` → `NeonBlueBorderStrong`.
- `ShortcutKeyButton` `.listening`, `PlaybackToggleButton` `:checked`, `EditorToolButton` `:checked`:
  kenar `NeonBlueBorderStrong` → `NeonBlue`. Dinlenme kenarı güçlenince bu üç hâl dinlenmeyle
  aynı fırçaya düşüyordu; mevcut opak vurgu belirteciyle ayrıldı.
- Yeni renk anahtarı yok, palet dosyalarına dokunulmadı. Kart kenarı (`Panel`), ayraçlar ve
  dolgular olduğu gibi.

Kenarı `NeonBlueBorderStrong` olan temalar: AccentButton, ChipButton, ChipRemoveButton,
EditorToolButton, GhostButton, PlaybackBarButton, PlaybackPlayButton, PlaybackToggleButton,
PlaybackZoomButton, ShortcutKeyButton, SupportButton. Kenarı zaten opak `NeonBlue` olanlar:
InfoButton, InfoButtonBesideLabel, PrimaryButton. Kenarsızlar (HeaderButton, PanelHeaderToggle,
LinkButton) ölçüye girmez.

## Özet

108 satır (36 palet × 3 zemin).

| Fırça | En Düşük | En Yüksek | 3:1 Altındaki Satır |
|---|---|---|---|
| Eski: `HeaderRestBorder` | 1,23 (SolarizedLight / Surface) | 1,86 (Catppuccin / AppBg) | 108 / 108 |
| `NeonBlueBorder` | 1,60 (Teknesyum / AppBg) | 2,46 (Cobalt / AppBg) | 108 / 108 |
| Yeni: `NeonBlueBorderStrong` | 2,50 (RosePineDawn / AppBg) | 4,24 (Monokai / AppBg) | 37 / 108 |
| `NeonBlue` (opak) | 8,21 (Teknesyum / AppBg) | 14,80 (Neon / AppBg) | 0 / 108 |

Yeni kenarla 23 palet üç zeminde de 3:1'i geçiyor. 13 palet en kötü zemininde altında kalıyor.

## Kalan Açık

Bu 13 palet testte adıyla ve üç ondalıklı en düşük oranıyla pimli. Palet düzelirse ya da
oran kayarsa test kırmızıya döner; liste elle küçültülür.

| Palet | En Düşük Oran |
|---|---|
| RosePineDawn | 2,497 |
| AyuLight | 2,530 |
| SolarizedLight | 2,557 |
| GithubLight | 2,578 |
| GruvboxLight | 2,596 |
| Teknesyum | 2,634 |
| CatppuccinLatte | 2,637 |
| Kar | 2,651 |
| Keskin | 2,716 |
| Kirik | 2,786 |
| Kagit | 2,788 |
| Buz | 2,853 |
| Gece | 2,996 |

On ikisi üç zeminde de eşiğin altında. `Gece` sınırda: yalnız `AppBg` üstünde 2,996, öteki iki
zeminde 3,01 ve 3,02 (aşağıdaki tabloda iki ondalıkla 3,00 görünür). Açığı kapatmanın yolu
paletin `NeonBlueBorderStrong` alfasını ya da vurgu rengini değiştirmek; bu palet sahibinin
kararı, burada yapılmadı.

## Hâller

Üstüne gelme, basılı, seçili ve dinleme kenarı her palette zemine karşı 3:1'i geçiyor ve
dinlenme kenarıyla aynı renk değil (3024 satır, `EtkilesimKenariDinlenmedenVeZemindenAyriliyor`).

| Hâl | Fırça | Zemine En Düşük | Dinlenme Kenarına Oran |
|---|---|---|---|
| Basılı, seçili, dinleme | `NeonBlue` | 8,21 | 2,53 – 4,04 |
| Üstüne gelme (Ghost ailesi) | `NeonPurple` | 4,06 | 1,00 – 3,23 |
| Üstüne gelme (InfoButton) | `NeonPink` | 3,69 | 1,01 – 2,64 |
| Üstüne gelme (PrimaryButton) | `TextBody` | 9,41 | 1,04 – 2,56 |

Bilinmesi gereken: üstüne gelme kenarının dinlenme kenarına parlaklık oranı bazı paletlerde
1,00'e iniyor (Neon / PanelSurface). O paletlerde ayrım parlaklıktan değil ton farkından, 2 px
kalınlıktan, parıltıdan ve ölçek değişiminden geliyor. InfoButton ve PrimaryButton satırları bu
işten etkilenmedi; onların dinlenme kenarı zaten `NeonBlue` idi.

İki yan etki daha: `GhostButton` ile `AccentButton` artık aynı kenarı taşıyor (fark parıltı ve
`NeonBlue` yazı). Edilgen düğmenin kenarı (`TextDisabled`, opak) bazı paletlerde dinlenme
kenarından güçlü; kendisi 5 satırda 3:1 altında (en düşük 2,18, SolarizedLight / Surface).
WCAG edilgen denetimi eşikten muaf tutar.

## Girdiler

Kapsam dışı, yalnız ölçüm. ComboBox kenarı opak `NeonBlue`: 8,21 – 14,80, açık yok.
TextBox ve kaydırıcının boş izi `NeonBlueBorder`: 1,60 – 2,46, 36 paletin tamamında 3:1 altında.
TextBox üstüne gelince `NeonPurple`, odakta `NeonBlue` alıyor; dinlenme hâli açık kalıyor.

## Mutasyonlar

Beşi de kırmızı. Taban 75 / 75 yeşil.

| Mutasyon | Kırmızı |
|---|---|
| `GhostButton` kenarını `HeaderRestBorder`'a geri almak | 24 / 75 |
| `EditorToolButton` `:checked` kenarını `NeonBlueBorderStrong`'a geri almak | 36 / 75 |
| `ShortcutKeyButton` `.listening` kenarını geri almak | 36 / 75, ayrıca `KisayolAtamaTests` 1 / 44 |
| Pim listesinden `Gece`'yi çıkarmak | 2 / 75 |
| Eşiği geçen `Monokai`'yi pim listesine eklemek | 1 / 75 |

## Ölçülmeyenler

- `PanelSurface`'in arkasından geçen atmosfer ışıması; zemin düz `AppBg` sayıldı.
- Video üstündeki `PlaybackScrim` zemini (`PlaybackZoomButton`).
- 1 px kenarın kenar yumuşatmasıyla ekranda aldığı gerçek renk.
- Odak halkası (ayrı ölçüde: `PaletKarsitligiTests.OdakHalkasiZemindenAyriliyor`).
- Kaynaktan çözülen kenarın canlı stille eşitliği yalnız yürürlükteki palette sınanıyor.

## Tablo

| Palet | Zemin | Eski (`HeaderRestBorder`) | `NeonBlueBorder` | Yeni (`NeonBlueBorderStrong`) |
|---|---|---|---|---|
| Ayu | AppBg | 1,57 | 1,94 | 3,31 |
| Ayu | PanelSurface | 1,61 | 1,97 | 3,29 |
| Ayu | Surface | 1,62 | 1,98 | 3,30 |
| AyuLight | AppBg | 1,34 | 1,71 | 2,58 |
| AyuLight | PanelSurface | 1,32 | 1,70 | 2,55 |
| AyuLight | Surface | 1,32 | 1,68 | 2,53 |
| Buz | AppBg | 1,53 | 1,82 | 2,93 |
| Buz | PanelSurface | 1,50 | 1,80 | 2,85 |
| Buz | Surface | 1,50 | 1,80 | 2,86 |
| Catppuccin | AppBg | 1,86 | 2,36 | 4,14 |
| Catppuccin | PanelSurface | 1,79 | 2,24 | 3,69 |
| Catppuccin | Surface | 1,77 | 2,23 | 3,63 |
| CatppuccinLatte | AppBg | 1,45 | 1,75 | 2,69 |
| CatppuccinLatte | PanelSurface | 1,43 | 1,73 | 2,66 |
| CatppuccinLatte | Surface | 1,43 | 1,73 | 2,64 |
| Cobalt | AppBg | 1,84 | 2,46 | 4,18 |
| Cobalt | PanelSurface | 1,73 | 2,29 | 3,69 |
| Cobalt | Surface | 1,71 | 2,26 | 3,62 |
| Dracula | AppBg | 1,70 | 2,32 | 3,92 |
| Dracula | PanelSurface | 1,62 | 2,21 | 3,57 |
| Dracula | Surface | 1,62 | 2,21 | 3,56 |
| Everforest | AppBg | 1,74 | 2,32 | 3,89 |
| Everforest | PanelSurface | 1,69 | 2,24 | 3,62 |
| Everforest | Surface | 1,68 | 2,23 | 3,61 |
| Gece | AppBg | 1,50 | 1,79 | 3,00 |
| Gece | PanelSurface | 1,53 | 1,84 | 3,02 |
| Gece | Surface | 1,52 | 1,85 | 3,01 |
| Github | AppBg | 1,60 | 1,93 | 3,24 |
| Github | PanelSurface | 1,64 | 1,96 | 3,22 |
| Github | Surface | 1,63 | 1,96 | 3,19 |
| GithubLight | AppBg | 1,50 | 1,73 | 2,64 |
| GithubLight | PanelSurface | 1,48 | 1,71 | 2,60 |
| GithubLight | Surface | 1,49 | 1,71 | 2,58 |
| Grafit | AppBg | 1,53 | 1,94 | 3,28 |
| Grafit | PanelSurface | 1,55 | 1,97 | 3,23 |
| Grafit | Surface | 1,54 | 1,98 | 3,20 |
| Gruvbox | AppBg | 1,68 | 2,36 | 4,04 |
| Gruvbox | PanelSurface | 1,60 | 2,24 | 3,64 |
| Gruvbox | Surface | 1,58 | 2,23 | 3,55 |
| GruvboxLight | AppBg | 1,44 | 1,75 | 2,74 |
| GruvboxLight | PanelSurface | 1,39 | 1,71 | 2,61 |
| GruvboxLight | Surface | 1,38 | 1,72 | 2,60 |
| Horizon | AppBg | 1,60 | 2,09 | 3,48 |
| Horizon | PanelSurface | 1,61 | 2,09 | 3,41 |
| Horizon | Surface | 1,59 | 2,07 | 3,38 |
| Kadife | AppBg | 1,57 | 2,01 | 3,41 |
| Kadife | PanelSurface | 1,57 | 2,02 | 3,36 |
| Kadife | Surface | 1,58 | 2,03 | 3,34 |
| Kagit | AppBg | 1,55 | 1,79 | 2,84 |
| Kagit | PanelSurface | 1,53 | 1,78 | 2,80 |
| Kagit | Surface | 1,53 | 1,78 | 2,79 |
| Kanagawa | AppBg | 1,60 | 2,17 | 3,65 |
| Kanagawa | PanelSurface | 1,57 | 2,13 | 3,45 |
| Kanagawa | Surface | 1,57 | 2,13 | 3,40 |
| Kar | AppBg | 1,46 | 1,74 | 2,65 |
| Kar | PanelSurface | 1,47 | 1,75 | 2,71 |
| Kar | Surface | 1,47 | 1,75 | 2,73 |
| Keskin | AppBg | 1,47 | 1,74 | 2,72 |
| Keskin | PanelSurface | 1,47 | 1,74 | 2,72 |
| Keskin | Surface | 1,47 | 1,74 | 2,72 |
| Kirik | AppBg | 1,53 | 1,80 | 2,83 |
| Kirik | PanelSurface | 1,51 | 1,77 | 2,79 |
| Kirik | Surface | 1,52 | 1,78 | 2,79 |
| Kor | AppBg | 1,54 | 1,94 | 3,27 |
| Kor | PanelSurface | 1,55 | 1,97 | 3,24 |
| Kor | Surface | 1,54 | 1,98 | 3,21 |
| MaterialOcean | AppBg | 1,61 | 2,04 | 3,53 |
| MaterialOcean | PanelSurface | 1,63 | 2,07 | 3,47 |
| MaterialOcean | Surface | 1,64 | 2,08 | 3,44 |
| Monokai | AppBg | 1,73 | 2,46 | 4,24 |
| Monokai | PanelSurface | 1,62 | 2,28 | 3,65 |
| Monokai | Surface | 1,60 | 2,25 | 3,59 |
| Moonlight | AppBg | 1,67 | 2,34 | 4,00 |
| Moonlight | PanelSurface | 1,60 | 2,21 | 3,61 |
| Moonlight | Surface | 1,60 | 2,21 | 3,59 |
| Neon | AppBg | 1,34 | 2,12 | 4,09 |
| Neon | PanelSurface | 1,40 | 2,21 | 4,10 |
| Neon | Surface | 1,39 | 2,23 | 4,12 |
| NightOwl | AppBg | 1,61 | 2,15 | 3,74 |
| NightOwl | PanelSurface | 1,61 | 2,12 | 3,53 |
| NightOwl | Surface | 1,60 | 2,12 | 3,48 |
| Nord | AppBg | 1,74 | 2,34 | 3,94 |
| Nord | PanelSurface | 1,69 | 2,26 | 3,66 |
| Nord | Surface | 1,67 | 2,23 | 3,65 |
| OneDark | AppBg | 1,84 | 2,22 | 3,67 |
| OneDark | PanelSurface | 1,82 | 2,21 | 3,57 |
| OneDark | Surface | 1,82 | 2,19 | 3,53 |
| RosePine | AppBg | 1,60 | 2,10 | 3,53 |
| RosePine | PanelSurface | 1,61 | 2,08 | 3,48 |
| RosePine | Surface | 1,60 | 2,09 | 3,45 |
| RosePineDawn | AppBg | 1,42 | 1,67 | 2,50 |
| RosePineDawn | PanelSurface | 1,43 | 1,67 | 2,52 |
| RosePineDawn | Surface | 1,44 | 1,69 | 2,54 |
| Solarized | AppBg | 1,67 | 2,21 | 3,66 |
| Solarized | PanelSurface | 1,63 | 2,12 | 3,43 |
| Solarized | Surface | 1,62 | 2,12 | 3,42 |
| SolarizedLight | AppBg | 1,28 | 1,73 | 2,66 |
| SolarizedLight | PanelSurface | 1,24 | 1,70 | 2,56 |
| SolarizedLight | Surface | 1,23 | 1,70 | 2,56 |
| Synthwave | AppBg | 1,58 | 2,30 | 3,95 |
| Synthwave | PanelSurface | 1,55 | 2,19 | 3,69 |
| Synthwave | Surface | 1,53 | 2,19 | 3,64 |
| Teknesyum | AppBg | 1,48 | 1,60 | 2,63 |
| Teknesyum | PanelSurface | 1,48 | 1,60 | 2,64 |
| Teknesyum | Surface | 1,48 | 1,60 | 2,64 |
| TokyoNight | AppBg | 1,78 | 2,20 | 3,77 |
| TokyoNight | PanelSurface | 1,77 | 2,16 | 3,55 |
| TokyoNight | Surface | 1,77 | 2,16 | 3,54 |
