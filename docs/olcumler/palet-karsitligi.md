# Palet Karşıtlığı: Neon Dolgunun Üstündeki Yazı

18 Eylül 2026. `OnNeonColor` neon dolgunun üstüne binen tek yazı rengi: birincil düğme,
vurgulu sekme ve işaretli satır onu kullanıyor. Bugüne kadar hiçbir ölçü bu ikilinin
okunabilir olup olmadığını sormamıştı.

Ölçü WCAG 2.1 karşıtlık oranı: kanallar `v ≤ 0,03928 ? v/12,92 : ((v+0,055)/1,055)^2,4`
ile doğrusallaştırılıyor, parlaklık `0,2126R + 0,7152G + 0,0722B`, oran
`(büyük+0,05)/(küçük+0,05)`. Her palet için dört neon dolgunun (`NeonBlue`, `NeonPurple`,
`NeonPink`, `NeonBlueActive`) **en kötüsü** yazılı — bir palet ancak en zayıf yüzü kadar
okunur.

## Sonuç

| | önce | sonra |
|---|---|---|
| AA eşiğinin (4,5:1) altında | **17 / 26** | **2 / 26** |
| 3,0:1 tabanının altında | **14 / 26** | **0 / 26** |
| en kötü palet | MaterialOcean **1,38:1** | GruvboxLight **3,77:1** |

Renk uydurulmadı: 16 palette `OnNeonColor` yalnız siyah ile beyaz arasında değiştirildi,
hangisi o paletin dolgularına daha uzaksa o. Dolgu renklerine dokunulmadı.

## Palet Palet

| Palet | önce | önce oran | sonra | sonra oran | en kötü dolgu |
|---|---|---|---|---|---|
| Ayu | `#FF000000` | 10.59 | `#FF000000` | 10.59 | `NeonBlueColor` |
| AyuLight | `#FFFFFFFF` | 2.48 | `#FF000000` | 6.21 | `NeonPurpleColor` |
| Catppuccin | `#FFFFFFFF` | 1.49 | `#FF000000` | 9.97 | `NeonBlueColor` |
| CatppuccinLatte | `#FFFFFFFF` | 3.74 | `#FF000000` | 3.88 | `NeonPurpleColor` |
| Cobalt | `#FF000000` | 10.78 | `#FF000000` | 10.78 | `NeonPurpleColor` |
| Dracula | `#FF000000` | 8.71 | `#FF000000` | 8.71 | `NeonPurpleColor` |
| Everforest | `#FFFFFFFF` | 1.82 | `#FF000000` | 9.09 | `NeonPurpleColor` |
| Github | `#FFFFFFFF` | 1.95 | `#FF000000` | 8.27 | `NeonPinkColor` |
| GithubLight | `#FFFFFFFF` | 5.05 | `#FFFFFFFF` | 5.05 | `NeonPurpleColor` |
| Gruvbox | `#FFFFFFFF` | 1.70 | `#FF000000` | 7.65 | `NeonPurpleColor` |
| GruvboxLight | `#FFFFFFFF` | 3.77 | `#FFFFFFFF` | 3.77 | `NeonPinkColor` |
| Horizon | `#FFFFFFFF` | 1.71 | `#FF000000` | 6.70 | `NeonPurpleColor` |
| Kanagawa | `#FFFFFFFF` | 2.65 | `#FF000000` | 6.01 | `NeonPurpleColor` |
| MaterialOcean | `#FFFFFFFF` | 1.38 | `#FF000000` | 8.73 | `NeonPurpleColor` |
| Monokai | `#FF000000` | 7.32 | `#FF000000` | 7.32 | `NeonPinkColor` |
| Moonlight | `#FFFFFFFF` | 1.80 | `#FF000000` | 9.14 | `NeonBlueColor` |
| Neon | `#FF000000` | 4.57 | `#FF000000` | 4.57 | `NeonPurpleColor` |
| NightOwl | `#FFFFFFFF` | 1.63 | `#FF000000` | 8.73 | `NeonPurpleColor` |
| Nord | `#FF000000` | 7.41 | `#FF000000` | 7.41 | `NeonPurpleColor` |
| OneDark | `#FFFFFFFF` | 2.02 | `#FF000000` | 7.13 | `NeonPurpleColor` |
| RosePine | `#FF000000` | 10.03 | `#FF000000` | 10.03 | `NeonPurpleColor` |
| RosePineDawn | `#FFFFFFFF` | 2.84 | `#FF000000` | 5.54 | `NeonPurpleColor` |
| Solarized | `#FFFFFFFF` | 3.00 | `#FF000000` | 6.54 | `NeonPinkColor` |
| SolarizedLight | `#FFFFFFFF` | 3.21 | `#FF000000` | 4.80 | `NeonPurpleColor` |
| Synthwave | `#FF000000` | 8.10 | `#FF000000` | 8.10 | `NeonPurpleColor` |
| TokyoNight | `#FFFFFFFF` | 1.67 | `#FF000000` | 8.34 | `NeonBlueColor` |

## Kalan Borç

`CatppuccinLatte` (3,88) ve `GruvboxLight` (3,77) iki seçenekle de 4,5'e ulaşamıyor:
dolguları açık, siyah da beyaz da yeterince uzak düşmüyor. Çözüm yazı rengi değil **dolgu**
değişikliği ister; renk uydurma yasağı bunu tek başına yapmaya izin vermiyor, karar
kullanıcının.

Borç susturulmuyor: `PaletKarsitligiTests.EsiginAltindaKalanlarPimli` bu iki adı tek tek
tutuyor. Listeye yeni bir ad düşmesi de, listedeki bir adın düzelmesi de kırmızı verir.

## Ölçünün Kendisi

Tekrarlanan ölçü `tests/VidShrink.Tests/PaletKarsitligiTests.cs`: sert taban 3,0 her palet
için ayrı bir Theory kolu, AA borcu pimli liste, ve hesabın kör olmadığının kontrolü
(beyaz/siyah 21:1, kendi üstüne 1:1, açık mavi neon üstüne beyaz 2,30:1). Bu belgedeki
önce/sonra sütunlarını üreten tek kullanımlık betik `.calisma/` ile birlikte silindi —
"önce" tarafı yalnız bu commit'te anlamlıydı.
