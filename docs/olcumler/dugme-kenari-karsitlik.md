# Düğme Kenarı Karşıtlığı

Tarih: 7 Ekim 2026; 10 Ekim 2026'da güncellendi (13 paletin açığı kapandı, girdi kenarı eklendi).
Ölçü: WCAG 2.1 1.4.11 (metin dışı karşıtlık, eşik 3:1).
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

7 Ekim:

- `Themes/Controls.axaml`: `GhostButton` dinlenme kenarı `HeaderRestBorder` → `NeonBlueBorderStrong`.
- `ShortcutKeyButton` `.listening`, `PlaybackToggleButton` `:checked`, `EditorToolButton` `:checked`:
  kenar `NeonBlueBorderStrong` → `NeonBlue`. Dinlenme kenarı güçlenince bu üç hâl dinlenmeyle
  aynı fırçaya düşüyordu; mevcut opak vurgu belirteciyle ayrıldı.

10 Ekim (sahibin kararı):

- 13 palette `NeonBlueBorderStrongColor` alfası, üç zeminin en kötüsünde 3:1'i geçen en küçük
  değere çıktı. RGB aynı, yeni renk anahtarı yok, öbür 23 palet `80` alfada kaldı. Değer elle
  yazılmadı: `tools/VidShrink.PaletteGen/PaletteBuilder.cs` içindeki `BorderStrongAlpha` taban
  `80`'den başlayıp eşiği tutan ilk alfayı seçer, palet dosyaları oradan üretilir.
- `Themes/Controls.axaml`: `TextBox` kenarı ve kaydırıcının boş izi (`RemainingTrack`)
  `NeonBlueBorder` → `NeonBlueBorderStrong`. Kaydırıcının üstüne gelme ve odak kenarı
  `NeonBlueBorderStrong` → `NeonBlue`; yoksa dinlenmeyle aynı fırçaya düşüyordu.
- Kart kenarı (`Panel`), ayraçlar ve dolgular olduğu gibi.

Kenarı `NeonBlueBorderStrong` olan temalar: AccentButton, ChipButton, ChipRemoveButton,
EditorToolButton, GhostButton, PlaybackBarButton, PlaybackPlayButton, PlaybackToggleButton,
PlaybackZoomButton, ShortcutKeyButton, SupportButton, TextBox, kaydırıcının boş izi. Kenarı zaten
opak `NeonBlue` olanlar: InfoButton, InfoButtonBesideLabel, PrimaryButton, ComboBox. Kenarsızlar
(HeaderButton, PanelHeaderToggle, LinkButton) ölçüye girmez.

## Özet

108 satır (36 palet × 3 zemin).

| Fırça | En Düşük | En Yüksek | 3:1 Altındaki Satır |
|---|---|---|---|
| 7 Ekim öncesi: `HeaderRestBorder` | 1,23 (SolarizedLight / Surface) | 1,86 (Catppuccin / AppBg) | 108 / 108 |
| `NeonBlueBorder` | 1,60 (Teknesyum / AppBg) | 2,46 (Cobalt / AppBg) | 108 / 108 |
| `NeonBlueBorderStrong`, 7 Ekim (alfa `80`) | 2,50 (RosePineDawn / AppBg) | 4,24 (Monokai / AppBg) | 37 / 108 |
| `NeonBlueBorderStrong`, 10 Ekim | 3,00 (Buz / PanelSurface) | 4,24 (Monokai / AppBg) | 0 / 108 |
| `NeonBlue` (opak) | 8,21 (Teknesyum / AppBg) | 14,80 (Neon / AppBg) | 0 / 108 |

36 paletin hepsi üç zeminde de 3:1'i geçiyor. Testte muafiyet listesi kalmadı.

## Kapanan Açık

Oran paletin en kötü zemininde, üç ondalıkla. Alfa on altılık; yüzde alfa / 255.

| Palet | Eski Alfa | Eski Oran | Yeni Alfa | Yeni Oran | Yeni Alfa % |
|---|---|---|---|---|---|
| RosePineDawn | 80 | 2,497 | 96 | 3,008 | 58,8 |
| AyuLight | 80 | 2,530 | 94 | 3,027 | 58,0 |
| SolarizedLight | 80 | 2,557 | 93 | 3,013 | 57,6 |
| GithubLight | 80 | 2,578 | 92 | 3,032 | 57,3 |
| GruvboxLight | 80 | 2,596 | 91 | 3,001 | 56,9 |
| Teknesyum | 80 | 2,634 | 8D | 3,017 | 55,3 |
| CatppuccinLatte | 80 | 2,637 | 8F | 3,028 | 56,1 |
| Kar | 80 | 2,651 | 8E | 3,027 | 55,7 |
| Keskin | 80 | 2,716 | 8C | 3,034 | 54,9 |
| Kirik | 80 | 2,786 | 88 | 3,002 | 53,3 |
| Kagit | 80 | 2,788 | 88 | 3,014 | 53,3 |
| Buz | 80 | 2,853 | 85 | 3,004 | 52,2 |
| Gece | 80 | 2,996 | 81 | 3,036 | 50,6 |

Üç ondalıkta en dar pay GruvboxLight'ta (3,001). Öbür 23 paletin en kötü oranı `80` alfada
3,187 ile 4,086 arasında; onlara dokunulmadı.

"En küçük" iki yönden pimli (`GucluKenarAlfasiEsigiTutanEnKucukDeger`): her palette alfa bir
düşürülünce eşik tutmuyor ya da alfa zaten taban `80`. Bir fazlası da kırmızı.

## Girdiler

`TextBox` kenarı ve kaydırıcının boş izi ince kenarla (`NeonBlueBorder`) 1,60 – 2,46 arasındaydı,
36 paletin tamamında 3:1 altında. Var olan `NeonBlueBorderStrong` belirteciyle çözüldü: yeni en
düşük 3,00, 216 satırın hiçbiri eşiğin altında değil (`GirdiKenariZemindenAyriliyor`).
ComboBox kenarı opak `NeonBlue`: 8,21 – 14,80, açık yoktu.

| Hâl | Fırça | Zemine En Düşük | Dinlenme Kenarına Oran |
|---|---|---|---|
| `TextBox` odak, kaydırıcı üstünde ve odak | `NeonBlue` | 8,21 | 2,53 – 3,84 |
| `TextBox` üstüne gelme | `NeonPurple` | 4,06 | 1,00 – 3,00 |

## Hâller

Üstüne gelme, basılı, seçili ve dinleme kenarı her palette zemine karşı 3:1'i geçiyor ve
dinlenme kenarıyla aynı renk değil (3024 satır, `EtkilesimKenariDinlenmedenVeZemindenAyriliyor`).

| Hâl | Fırça | Zemine En Düşük | Dinlenme Kenarına Oran |
|---|---|---|---|
| Basılı, seçili, dinleme | `NeonBlue` | 8,21 | 2,53 – 3,84 |
| Üstüne gelme (Ghost ailesi) | `NeonPurple` | 4,06 | 1,00 – 3,00 |
| Üstüne gelme (InfoButton) | `NeonPink` | 3,69 | 1,01 – 2,64 |
| Üstüne gelme (PrimaryButton) | `TextBody` | 9,41 | 1,04 – 2,56 |

### Açık Bırakılan: Üstüne Gelme İle Dinlenme Farkı

Üstüne gelme kenarının dinlenme kenarına parlaklık oranı bazı paletlerde 1,00'e iniyor.
Oranı 1,5'in altında kalan palet sayısı:

| Fırça | Kullanan | En Düşük | 1,5 Altı Palet |
|---|---|---|---|
| `NeonPurple` | 11 düğme teması, `TextBox` | 1,00 (Neon / PanelSurface) | 11 / 36 |
| `NeonPink` | InfoButton | 1,01 (RosePine) | 21 / 36 |
| `TextBody` | PrimaryButton | 1,04 (Cobalt) | 24 / 36 |

O paletlerde ayrım parlaklıktan değil ton farkından, 2 px kalınlıktan, parıltıdan ve ölçek
değişiminden geliyor. Var olan belirteçlerle tek çıkış üstüne gelme kenarını `NeonBlue`'ya
almak (dinlenmeye en düşük 2,53); ama o zaman 36 palette üstüne gelme tonu mordan maviye döner
ve basılı hâlle aynı fırçaya düşer. Bu bir ton kararı, yapılmadı; sahibin kararını bekliyor.
WCAG 1.4.11 hâller arası fark için eşik koymaz, yalnız zemine karşı 3:1 ister; o tutuyor.

İki yan etki daha: `GhostButton` ile `AccentButton` aynı kenarı taşıyor (fark parıltı ve
`NeonBlue` yazı). Edilgen düğmenin kenarı (`TextDisabled`, opak) 5 satırda 3:1 altında
(en düşük 2,18, SolarizedLight / Surface). WCAG edilgen denetimi eşikten muaf tutar.

## Mutasyonlar

10 Ekim turu. Hepsi kırmızı, hepsi elle geri alındı.

| Mutasyon | Koşulan | Kırmızı |
|---|---|---|
| Buz alfasını `85` → `80` (eski değer) | `DugmeKenariKarsitligiTests`, `PaletteTests` | 5 / 121 |
| Buz alfasını `85` → `86` (gereğinden büyük) | aynı | 2 / 121 |
| `TextBox` kenarını `NeonBlueBorder`'a geri almak | aynı | 36 / 121 |
| Kaydırıcı üstünde kenarını `NeonBlueBorderStrong`'a geri almak | aynı + `KaydiriciTests` | 37 / 138 |

7 Ekim turundan geçerliliğini koruyanlar (taban o gün 75): `GhostButton` kenarını
`HeaderRestBorder`'a geri almak 24 / 75; `EditorToolButton` `:checked` kenarını geri almak
36 / 75; `ShortcutKeyButton` `.listening` kenarını geri almak 36 / 75. Pim listesiyle ilgili iki
mutasyon listeyle birlikte kalktı.

## Ölçülmeyenler

- `PanelSurface`'in arkasından geçen atmosfer ışıması; zemin düz `AppBg` sayıldı.
- Video üstündeki `PlaybackScrim` zemini (`PlaybackZoomButton`).
- 1 px kenarın kenar yumuşatmasıyla ekranda aldığı gerçek renk.
- Odak halkası (ayrı ölçüde: `PaletKarsitligiTests.OdakHalkasiZemindenAyriliyor`).
- Kaynaktan çözülen kenarın canlı stille eşitliği yalnız yürürlükteki palette sınanıyor.

## Tablo

| Palet | Zemin | 7 Ekim Öncesi (`HeaderRestBorder`) | `NeonBlueBorder` | `NeonBlueBorderStrong` (10 Ekim) |
|---|---|---|---|---|
| Ayu | AppBg | 1,57 | 1,94 | 3,31 |
| Ayu | PanelSurface | 1,61 | 1,97 | 3,29 |
| Ayu | Surface | 1,62 | 1,98 | 3,30 |
| AyuLight | AppBg | 1,34 | 1,71 | 3,09 |
| AyuLight | PanelSurface | 1,32 | 1,70 | 3,03 |
| AyuLight | Surface | 1,32 | 1,68 | 3,03 |
| Buz | AppBg | 1,53 | 1,82 | 3,08 |
| Buz | PanelSurface | 1,50 | 1,80 | 3,00 |
| Buz | Surface | 1,50 | 1,80 | 3,00 |
| Catppuccin | AppBg | 1,86 | 2,36 | 4,14 |
| Catppuccin | PanelSurface | 1,79 | 2,24 | 3,69 |
| Catppuccin | Surface | 1,77 | 2,23 | 3,63 |
| CatppuccinLatte | AppBg | 1,45 | 1,75 | 3,10 |
| CatppuccinLatte | PanelSurface | 1,43 | 1,73 | 3,03 |
| CatppuccinLatte | Surface | 1,43 | 1,73 | 3,03 |
| Cobalt | AppBg | 1,84 | 2,46 | 4,18 |
| Cobalt | PanelSurface | 1,73 | 2,29 | 3,69 |
| Cobalt | Surface | 1,71 | 2,26 | 3,62 |
| Dracula | AppBg | 1,70 | 2,32 | 3,92 |
| Dracula | PanelSurface | 1,62 | 2,21 | 3,57 |
| Dracula | Surface | 1,62 | 2,21 | 3,56 |
| Everforest | AppBg | 1,74 | 2,32 | 3,89 |
| Everforest | PanelSurface | 1,69 | 2,24 | 3,62 |
| Everforest | Surface | 1,68 | 2,23 | 3,61 |
| Gece | AppBg | 1,50 | 1,79 | 3,04 |
| Gece | PanelSurface | 1,53 | 1,84 | 3,07 |
| Gece | Surface | 1,52 | 1,85 | 3,05 |
| Github | AppBg | 1,60 | 1,93 | 3,24 |
| Github | PanelSurface | 1,64 | 1,96 | 3,22 |
| Github | Surface | 1,63 | 1,96 | 3,19 |
| GithubLight | AppBg | 1,50 | 1,73 | 3,11 |
| GithubLight | PanelSurface | 1,48 | 1,71 | 3,06 |
| GithubLight | Surface | 1,49 | 1,71 | 3,03 |
| Grafit | AppBg | 1,53 | 1,94 | 3,28 |
| Grafit | PanelSurface | 1,55 | 1,97 | 3,23 |
| Grafit | Surface | 1,54 | 1,98 | 3,20 |
| Gruvbox | AppBg | 1,68 | 2,36 | 4,04 |
| Gruvbox | PanelSurface | 1,60 | 2,24 | 3,64 |
| Gruvbox | Surface | 1,58 | 2,23 | 3,55 |
| GruvboxLight | AppBg | 1,44 | 1,75 | 3,23 |
| GruvboxLight | PanelSurface | 1,39 | 1,71 | 3,02 |
| GruvboxLight | Surface | 1,38 | 1,72 | 3,00 |
| Horizon | AppBg | 1,60 | 2,09 | 3,48 |
| Horizon | PanelSurface | 1,61 | 2,09 | 3,41 |
| Horizon | Surface | 1,59 | 2,07 | 3,38 |
| Kadife | AppBg | 1,57 | 2,01 | 3,41 |
| Kadife | PanelSurface | 1,57 | 2,02 | 3,36 |
| Kadife | Surface | 1,58 | 2,03 | 3,34 |
| Kagit | AppBg | 1,55 | 1,79 | 3,07 |
| Kagit | PanelSurface | 1,53 | 1,78 | 3,03 |
| Kagit | Surface | 1,53 | 1,78 | 3,01 |
| Kanagawa | AppBg | 1,60 | 2,17 | 3,65 |
| Kanagawa | PanelSurface | 1,57 | 2,13 | 3,45 |
| Kanagawa | Surface | 1,57 | 2,13 | 3,40 |
| Kar | AppBg | 1,46 | 1,74 | 3,03 |
| Kar | PanelSurface | 1,47 | 1,75 | 3,10 |
| Kar | Surface | 1,47 | 1,75 | 3,12 |
| Keskin | AppBg | 1,47 | 1,74 | 3,03 |
| Keskin | PanelSurface | 1,47 | 1,74 | 3,04 |
| Keskin | Surface | 1,47 | 1,74 | 3,04 |
| Kirik | AppBg | 1,53 | 1,80 | 3,08 |
| Kirik | PanelSurface | 1,51 | 1,77 | 3,03 |
| Kirik | Surface | 1,52 | 1,78 | 3,00 |
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
| RosePineDawn | AppBg | 1,42 | 1,67 | 3,01 |
| RosePineDawn | PanelSurface | 1,43 | 1,67 | 3,06 |
| RosePineDawn | Surface | 1,44 | 1,69 | 3,08 |
| Solarized | AppBg | 1,67 | 2,21 | 3,66 |
| Solarized | PanelSurface | 1,63 | 2,12 | 3,43 |
| Solarized | Surface | 1,62 | 2,12 | 3,42 |
| SolarizedLight | AppBg | 1,28 | 1,73 | 3,15 |
| SolarizedLight | PanelSurface | 1,24 | 1,70 | 3,04 |
| SolarizedLight | Surface | 1,23 | 1,70 | 3,01 |
| Synthwave | AppBg | 1,58 | 2,30 | 3,95 |
| Synthwave | PanelSurface | 1,55 | 2,19 | 3,69 |
| Synthwave | Surface | 1,53 | 2,19 | 3,64 |
| Teknesyum | AppBg | 1,48 | 1,60 | 3,02 |
| Teknesyum | PanelSurface | 1,48 | 1,60 | 3,02 |
| Teknesyum | Surface | 1,48 | 1,60 | 3,02 |
| TokyoNight | AppBg | 1,78 | 2,20 | 3,77 |
| TokyoNight | PanelSurface | 1,77 | 2,16 | 3,55 |
| TokyoNight | Surface | 1,77 | 2,16 | 3,54 |
