# K8 Borcu 4 — Pencerenin Boyu ve İçindeki Siyaset

19 Eylül 2026. Borç üç şey söylüyordu ve **şüpheli** işaretliydi: dosya en büyüğü,
"yoklama siyaseti, donanım hükmü ve `ReasonCode` kolları Core yerine pencerede".
Ölçülünce üçü ayrı sonuç verdi.

## Donanım hükmü zaten Core'da — öncül yanlıştı

`HardwareVerdict` `src/VidShrink.Core/HardwareVerdict.cs:73`'te tanımlı, kararı
`HardwareVerdict.Decide` veriyor. Pencerede duran şey hükmün kendisi değil, onu çağıran
ve sonucunu kutuya/ayara bağlayan yol. Aynısı `ReasonCode` için de geçerli: kodlar
`Core/EncodePlan.cs:34`'te, pencerede duran şey kod → dil anahtarı eşlemesi, ki o
zaten arayüz işi.

## Gerçek bulgu: bir dosyada üç ayrı konu

Ölçülebilir olan tek iddia dosya boyuydu. Pencere üç kapalı bölüğü taşıyordu:
yoklama geçidi, donanım hükmünün bağlanışı ve gerekçe/tavsiye metinleri. Üçü de
kendi dosyasına ayrıldı, iç içe sınıf adı (`MainWindow.DeferredEncoderAvailability`)
korundu, testlerin başvurusu değişmedi.

| dosya | satır |
|---|---|
| `MainWindow.axaml.cs` (önce) | 5081 |
| `MainWindow.axaml.cs` (sonra) | 4481 |
| `MainWindow.Yoklama.cs` | 415 |
| `MainWindow.Gerekce.cs` | 209 |

## Taşıma kaynaktan okuyan ölçüyü kırdı

`HardwareVerdictTests.TheProbeStaysOffTheUiThread` yoklama gövdesini
`MainWindow.axaml.cs` metninde arıyordu; dosya değişince kırmızı oldu. Ölçü
`TipSources.WindowProbeCodePath` ile yeni dosyaya bağlandı. Kırmızı doğruydu:
taşıma sessizce geçseydi ölçü bir daha hiçbir şey okumayacaktı.

## Yan bulgu: sahipsiz belge satırı

`MainWindow.axaml.cs`'te ayırıcı ayarını anlatan `/// <summary>` bloğu üyesinden
kopmuş, `PlanPanelRow`'un belgesinin üstüne yığılmıştı. `SplitterSettingsPath`'in
başına geri konuldu.

## Koşum

`dotnet build VidShrink.sln -c Release -warnaserror -m:2` yeşil. Dokunulan alanın
süzgeci 294 başarılı / 2 atlandı (ikisi `[LiveProbeFact]`, ortam kapılı).
