# Fluent Simge Eşlemesi

Kaynak: [microsoft/fluentui-system-icons](https://github.com/microsoft/fluentui-system-icons) (MIT).

Bütün yollar **Filled** temanın **24 px** çiziminden geliyor; her dosya tek bir `<path>` gövdesi
taşıyor ve `assets/<Klasör>/SVG/<dosya>` yolundan ham olarak alındı. Gövde `Themes/Icons.axaml`'a
`F1` (NonZero) dolgu kuralı ve `M 0,0 M 24,24` sabitleyicisiyle giriyor; arayüz artık kalemle
değil `Fill` ile çiziyor, bu yüzden `IconStroke` belirteci düştü.

`docs/danisma/2026-09-17-fable-kararlar.md` §9 "26 yol" diyor. Depodaki gerçek sayı **27**:
`IconRestore`, ölçümün alındığı 13 Eylül'den sonra eklenmiş. Aşağıdaki tablo depodaki sayıdır ve
`IconsTests` bu tabloyla `Icons.axaml`'ı karşılıklı okur; tabloya girmeyen ya da tablodan düşen
her anahtar ölçüyü kırar.

| # | Anahtar | Fluent klasörü | Fluent dosyası | Sürüm / boy | Kullanıldığı yer |
|---|---|---|---|---|---|
| 1 | IconPlayer | `Play Circle` | `ic_fluent_play_circle_24_filled.svg` | Filled / 24 px | Oynatıcı sekmesi |
| 2 | IconShrink | `Arrow Minimize` | `ic_fluent_arrow_minimize_24_filled.svg` | Filled / 24 px | Sıkıştır sekmesi |
| 3 | IconConvert | `Arrow Swap` | `ic_fluent_arrow_swap_24_filled.svg` | Filled / 24 px | Dönüştür sekmesi |
| 4 | IconRecorder | `Video` | `ic_fluent_video_24_filled.svg` | Filled / 24 px | Kaydedici sekmesi |
| 5 | IconAdvanced | `Options` | `ic_fluent_options_24_filled.svg` | Filled / 24 px | Gelişmiş bölüm başlığı |
| 6 | IconAbout | `Info` | `ic_fluent_info_24_filled.svg` | Filled / 24 px | Hakkında sekmesi |
| 7 | IconSettings | `Settings` | `ic_fluent_settings_24_filled.svg` | Filled / 24 px | Ayarlar sekmesi (K4) |
| 8 | IconPlay | `Play` | `ic_fluent_play_24_filled.svg` | Filled / 24 px | Oynat |
| 9 | IconPause | `Pause` | `ic_fluent_pause_24_filled.svg` | Filled / 24 px | Duraklat |
| 10 | IconRewind | `Rewind` | `ic_fluent_rewind_24_filled.svg` | Filled / 24 px | Geri sar |
| 11 | IconFastForward | `Fast Forward` | `ic_fluent_fast_forward_24_filled.svg` | Filled / 24 px | İleri sar |
| 12 | IconVolume | `Speaker 2` | `ic_fluent_speaker_2_24_filled.svg` | Filled / 24 px | Ses açık |
| 13 | IconVolumeMute | `Speaker Mute` | `ic_fluent_speaker_mute_24_filled.svg` | Filled / 24 px | Ses kapalı |
| 14 | IconSpeed | `Top Speed` | `ic_fluent_top_speed_24_filled.svg` | Filled / 24 px | Hız |
| 15 | IconFullScreen | `Full Screen Maximize` | `ic_fluent_full_screen_maximize_24_filled.svg` | Filled / 24 px | Tam ekran |
| 16 | IconMenu | `More Vertical` | `ic_fluent_more_vertical_24_filled.svg` | Filled / 24 px | Şerit menüsü |
| 17 | IconCamera | `Camera` | `ic_fluent_camera_24_filled.svg` | Filled / 24 px | Kare yakala |
| 18 | IconChevronDown | `Chevron Down` | `ic_fluent_chevron_down_24_filled.svg` | Filled / 24 px | Açılır başlık (kapalı) |
| 19 | IconChevronUp | `Chevron Up` | `ic_fluent_chevron_up_24_filled.svg` | Filled / 24 px | Açılır başlık (açık) |
| 20 | IconStop | `Stop` | `ic_fluent_stop_24_filled.svg` | Filled / 24 px | Kaydı durdur |
| 21 | IconRestart | `Previous` | `ic_fluent_previous_24_filled.svg` | Filled / 24 px | Başa dön |
| 22 | IconClose | `Dismiss` | `ic_fluent_dismiss_24_filled.svg` | Filled / 24 px | Pencere kapat |
| 23 | IconMaximize | `Maximize` | `ic_fluent_maximize_24_filled.svg` | Filled / 24 px | Pencere büyüt |
| 24 | IconMinimize | `Subtract` | `ic_fluent_subtract_24_filled.svg` | Filled / 24 px | Pencere küçült |
| 25 | IconRestore | `Square Multiple` | `ic_fluent_square_multiple_24_filled.svg` | Filled / 24 px | Pencere geri al |
| 26 | IconCoffee | `Drink Coffee` | `ic_fluent_drink_coffee_24_filled.svg` | Filled / 24 px | Bağış bağlantısı |
| 27 | IconCode | `Code` | `ic_fluent_code_24_filled.svg` | Filled / 24 px | Teknesyum bağlantısı (K4) |

## Ölçülen Sınır Kutuları

Sabitleyici çıkarıldıktan sonra `Geometry.Parse(...).Bounds` ile okunan değerler
`IkonKutusuTests` içinde pimli. Fluent'in kendi çiziminden gelen üç istisna var:

- `IconPlay` — üçgen optik olarak sağa kaydırılmış (cx 13,43).
- `IconSpeed` — gösterge kütlesi merkezin üstünde (cy 11,00).
- `IconCoffee` — kulp sağda 2 birimlik kenar payını taşıyor (sağ kenar 23,00).

Bu üçü ayrı ayrı, ölçülen kutularıyla pimlendi; kalan 24 simge genel kurala (mürekkep
2–22 aralığında, merkez 12±0,55) uyuyor.
