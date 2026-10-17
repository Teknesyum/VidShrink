# Erişilebilirlik Denemesi

Later 78 (AYR-16 Ekran Okuyucu Denemesi, AYR-17 Klavyeyle Tam Kullanım Denemesi), 2026-10-18,
Windows 11 22631. Ölçü `tests/VidShrink.Tests/ErisilebilirlikDenemesiTests.cs`.

## Yöntem

- **Klavye:** her sekmede seçili sekme düğmesinden başlanıp gerçek Tab tuşu (Win32 penceresine
  ham klavye girdisi) en çok 300 kez basılır. Her durakta odaklanan denetimin türü, adı ve
  görünürlüğü yazılır. Odak sekme düğmesine dönerse sıra tamamdır. Aynı denetimde kalırsa
  tuzak sayılır.
- **Ekran okuyucu:** Anlatıcı ve NVDA, okuyacakları adı UI Automation ağacından alır. Ağaç iki
  yoldan ölçüldü. Test içinde her durakta `ControlAutomationPeer.GetName()` okundu. Canlı
  uygulamada da Windows PowerShell 5.1'in `UIAutomationClient`'ıyla her sekme seçilip ekrandaki
  bütün klavyeyle odaklanabilir öğeler döküldü. Ölçüm kurulu 1.2.15 sürümünde ve bu dalın
  derlemesinde yapıldı.
- Sesli dinleme yapılmadı (aşağıda açık kalanlar).

## Sekme Sırası

| Sekme | Durak (Tab) | Son durak, düzeltmeden önce |
|---|---|---|
| Oynatıcı | 16 | `BtnSeritFullScreen` üstünde takılıyordu |
| Düzenleyici | 17 | `Timeline` üstünde takılıyordu |
| Küçült (boş) | 28 | `BtnAdvancedToggle` üstünde takılıyordu |
| Küçült (kaynak yüklü) | 33 | aynı |
| Dönüştür | 40 | `TxtConvertCommand` üstünde takılıyordu |
| Kaydedici | 25 | `CmbWebcam` üstünde takılıyordu |
| Ayarlar | 63 | `BtnResetSettings` üstünde takılıyordu |

Sıra sekme şeridi, ardından başlık çubuğu (dil, Destek, GitHub, pencere düğmeleri), ardından
sekme içeriği yukarıdan aşağı. Düzeltmeden sonra yedi kolun hepsinde döngü sekme düğmesine
dönüyor.

## Klavye Tuzağı Ve Nedeni

Altı sekmenin hepsinde Tab son denetimde takılıyordu: klavye kullanıcısı pencerenin sonuna
gelince başa dönemiyordu. Shift+Tab ya da fare gerekiyordu.

Neden Avalonia 12.1.2'de. Sekme şeridinin panelinin `TabNavigation` değeri `Once`.
`TabControl`, içerikte odak alan denetimi şeridin `TabOnceActiveElement`'ine yazıyor. Pencere
sonunda `Cycle` başa sarınca şeridin girişi o denetimi geri veriyor, odak yerinde kalıyor.
Pencereye `TabNavigation="Cycle"` vermek işe yaramadı, kök zaten `Cycle`.

Düzeltme `src/VidShrink.App/MainWindow.SekmeSeridi.cs`. Sekme denetiminde her `GotFocus`'ta
şeridin etkin öğesi seçili sekme düğmesine geri yazılıyor.

## Adı Okunmayan Denetimler

Canlı UIA dökümü. Sayılar adsız / ekrandaki odaklanabilir öğe:

| Sekme | Kurulu 1.2.15 | Bu dal |
|---|---|---|
| Oynatıcı | 1 / 23 | 0 / 23 |
| Düzenleyici | 1 / 42 | 0 / 43 |
| Küçült | 2 / 38 | 0 / 37 |
| Dönüştür | 13 / 44 | 0 / 51 |
| Kaydedici | 0 / 30 | 0 / 35 |
| Ayarlar | 7 / 54 | 0 / 70 |

Adsız olanlar:

- Oynatıcı: oynatıcı yüzeyi (`Player`).
- Düzenleyici: program monitörü (`Preview`).
- Küçült: hedef boyut kutusu, kalite hedefi kutusu.
- Dönüştür: kapsayıcı, video kodeği, kalite kipi, CRF, çözünürlük, kare hızı, ses, ses bit hızı,
  kırpma başı ve sonu, komut kutusu ve öteki seçim kutuları.
- Ayarlar: dil, tema, varsayılan hedef, çıktı adı, paralel iş, kodlama önceliği, kodlayıcı
  iş parçacığı, OpenSubtitles anahtarı, paylaşım süresi.

Ayrıca ses kaydırıcısı "Ses {0}" diye, biçimlenmemiş yer tutucuyla okunuyordu. Artık "Ses"
okunuyor; ad `PlayerView.Serit.cs`'te dil değiştikçe yazılıyor.

Düzeltmede `MainWindow.axaml`'da 43 denetime, `EditorView.axaml`'da program monitörüne,
`ComparisonPanel.axaml`'da panel kabuğuna `AutomationProperties.Name` verildi. Hepsi mevcut
etiket anahtarlarından, yeni metin anahtarı yok. Kapalı bölümlerdeki girdiler (Gelişmiş, ön
ayar adı, özel çözünürlük) Tab yürüyüşüne girmediği için ayrıca biçimlemeden pimleniyor
(`AnaPenceredeAdsizGirdiAlaniKalmaz`).

## Açık Kalanlar

- **Etiket düğmeleri fazla durak:** alan etiketleri ("Kapsayıcı" vb.) yardım ipucu açan
  odaklanabilir düğmeler. Her alan iki Tab durağı oluyor. Büyük iş, ayrı başlık ister.
- **Çiplerde birim okunmuyor:** Küçült hedef çipleri yalnız sayıyı okutuyor ("8", "16").
  Yeni metin anahtarı ister.
- **Paylaşım bağlantısı kutuları adsız:** `TxtShareLink` (ana pencere, `ShrinkJobWindow`),
  düzenleyici ve kaydedicideki eşleri. Ekran okuyucu adresi değer olarak okuyor, uygun anahtar
  yok.
- **Shift+Tab yönü ölçülmedi.**
- **Sesli deneme yapılmadı:** Anlatıcı'nın konuşması bu oturumda dinlenemedi, NVDA kurulu
  değil. Okunacak ad UIA ağacından ölçüldü. Sesli deneme bir kişiyle yapılmalı.
- **macOS VoiceOver:** o makinede ayrıca yapılacak.

## Mutasyon

Altı python mutasyonunun altısı kırmızı (8 kol): şerit öğesini geri yazmamak 7, işleyiciyi bağlamamak 7, `CmbContainer` adını kaldırmak 2, ses adını ham anahtarla yazmak 1, `Player` adını kaldırmak 1, `TxtQualityTarget` adını kaldırmak 2.
