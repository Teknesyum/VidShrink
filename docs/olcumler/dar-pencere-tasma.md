# Dar Pencerede Kaynak Bilgi Izgarası (T194)

28 Eylül 2026, `main` = `6d5260c4`, .NET SDK 10.0.401, Windows 11. Ölçüyü
`KareYerlesimTests.KaynakBilgiEtiketleriKendiHucresindeKalir` üretti; `Kapat()` geçici
olarak devre dışı bırakılıp kanıt dosyaları tutuldu, test dosyası sonra geri alındı.
Ham çıktı: [`T194-ham/`](T194-ham/).

## Karar (Kodda Olan)

Sözleşme iki yol sormuştu: sarma mı, sütun düşürme mi. Kod ikisini de değil, üçüncüsünü
seçmiş:

1. **Tek satır, üç nokta, balon** — `76538e5e` (16 Eylül): `InfoGrid`'in 8 etiketi ve 8
   değeri `NoWrap` + `CharacterEllipsis`, tam metin `ToolTip`'te.
2. **Sütun sayısı içerikten** — `9359cec0` (23 Eylül): `SutunIzgara`
   (`src/VidShrink.App/SutunIzgara.cs`) `MaxColumns="4"`'ten aşağı iner; yalnız hücre
   sayısını tam bölen sayıları dener, her sütun kendi en geniş hücresi kadar yer ister.

Eşik bu yüzden `Theme.axaml` belirteci **değil**: sabit bir piksel eşiği yok, eşik her
seferinde hücrelerin doğal genişliğinden ölçülüyor. Sözleşmenin "sayıyı uydurma" şartı
bu yolla karşılanıyor; "eşik belirteç olsun" şartı konusuz kaldı.

## Ölçüm

Değer hücrelerine test sabit bir metin koyuyor: `hevc (Main 10) · 3840x2160`
(doğal 229 px).

| Dil kolu | Pencere | Ölçüm içindeki dil | Sütun | Hücre | En geniş etiket | Taşan | Kısalan |
| --- | --- | --- | --- | --- | --- | --- | --- |
| tr | 1600x1000 | tr | 1 | 452 px | Video Kodeği 116 px | 0 | 0 |
| en | 1600x1000 | en | 1 | 452 px | Video Codec 109 px | 0 | 0 |
| tr | 1040x720 | tr | 1 | 266 px | Video Kodeği 116 px | 0 | 0 |
| en | 1040x720 | **tr** | 1 | 266 px | Video Kodeği 116 px | 0 | 0 |

16 metnin hepsi dört kolda tek satır, hücreye sığıyor, balonu tam.

## Bu Ölçünün Söylemediği

- **İngilizce dar kol İngilizceyi ölçmüyor.** `dil=en` verilen 1040x720 kolunda, ölçüm
  anında `Strings.Language` `tr`. Etiketi okunan altı koşumun altısında aynı sonuç çıktı; tek başına koşulduğunda
  da öyle. `en` 1600x1000 ile `en` 1600x720 İngilizce, `en` 1300x720 Türkçe çıktı. Kök neden
  kanıtlanmadı. Test `SettingsPathOverride` vermiyor, `MainWindow.OnWindowLoaded` dili ayar
  dosyasından ve `CultureInfo.CurrentUICulture`'dan yeniden seçiyor; şüphe orada.
  Sözleşmenin uyarısındaki sınıf bu: yerelde bir şey, CI'da başka bir şey ölçülüyor.
- **Dört sütunlu yerleşim hiç ölçülmüyor.** 229 px'lik sabit değer iki sütunu bile 452 px'e
  sığdırmıyor; iki pencerede de ızgara tek sütuna iniyor. Dar koldaki `sutun < 4` aserti bu
  yüzden her durumda doğru, yani boş.
- **Yalnız `InfoGrid` ölçüldü.** Sözleşmenin 1. adımı 1040x720'de taşan *bütün* öğeleri
  istiyordu; o sayım yapılmadı.
- **1040x720 artık ulaşılamayan bir boyut.** `MainWindow.axaml:12` bugün `MinWidth="1136"`
  (`892639cb`, 15 Eylül). Testin belge yorumu hâlâ `MinWidth="1040"` diyor.

Bu dört madde T195'e devredildi.
