# Dil ekleme — ölçüm ve fiyat

8 Eylül 2026. Soru: "tüm dilleri ekleyelim" ne kadar tutar. Aşağıdaki sayılar
tahmin değil, depodan sayıldı.

## Sözlüğün ölçüsü

| Dosya | Anahtar | Sözcük | Karakter |
|---|---|---|---|
| main.json | 388 | 4248 | 23294 |
| performance.json | 31 | 343 | 1915 |
| playback.json | 18 | 73 | 397 |
| settings.json | 38 | 312 | 1646 |
| **Toplam (bir dil)** | **475** | **4976** | **27252** |

Bunların 36 tanesi madde işaretli ipucu, toplam 112 satır. 23 anahtar 200 karakterden uzun.

## Bir dilin geçmek zorunda olduğu ölçümler

Yeni bir klasör `Locales/<dil>/` açmak dili programa sokar — düğmesi kendiliğinden belirir
(`MainWindow.axaml.cs:576`). Ama şu ölçümler dili otomatik olarak kapsamına alır:

1. **Anahtar birebirliği** (`LocalizationTests`) — 475 anahtarın hepsi, eksiksiz ve fazlasız.
2. **Piksel sığması** (`TipOverflowTests` + `TipLineMetrics`) — 112 ipucu satırının her biri
   uygulamanın kendi yazı tipiyle ölçülüyor. Balon genişliğine sığmayan satır kırmızı.
   Almanca gibi uzun bileşik sözcüklü diller burada düşer; çeviri "doğru" olsa bile yeniden
   yazılması gerekir.
3. **Büyük harf yasağı** (`CasingTests`) — sözlükte tümü büyük harf yazılmış metin olamaz.
4. **Madde biçimi** (`TipTranslationTests`) — madde işaretli satır sayısı diller arasında eşit.

Yani bir dilin maliyeti çeviri değil, **sığdırma turu**. Çeviri bir kez yazılır; sığmayan
satırlar tek tek kısaltılır ve ölçüm yeniden koşulur.

## Programda duran iki dilli engeller

Bunlar düzeltilmeden üçüncü dil oynatıcıda İngilizceye düşer:

| Yer | Ne yapıyor |
|---|---|
| `Playback/ComparisonPanel.axaml.cs:245` | `SetLanguage(bool turkish)` — dil bir bayrak |
| `Playback/ControlStrip.axaml.cs:196` | aynı bayrak |
| `Playback/PanelHost.cs:361` | aynı bayrak |
| `ComparisonPanel.axaml.cs:538` | yüzde biçimi `%50` / `50%` koda gömülü |
| `Core/Playback/IComparisonFrameSource.cs:35` | `MessageTr` + `MessageEn` — motor iki dil taşıyor |
| `Ffmpeg/Playback/PipeComparisonFrameSource.cs:96` | üç hata iletisi iki dilde, koda gömülü |
| `LanguageCatalog.cs` | `Conjunctions` (ve, veya, ile) Türkçeye özel, her dile uygulanıyor |

## Fiyat

**Altyapı (dil sayısından bağımsız, bir kez):** yukarıdaki yedi kalem. Yaklaşık 8 dosya,
bayrağı dil koduna çevirmek ve üç motor iletisini anahtara taşımak. Ucuz ve geri alınabilir.

**Dil başına:** 475 anahtar çeviri + sığdırma turu. Çevirinin kendisi ucuz; sığdırma turu
her dilde 112 satırın ölçülüp taşanların kısaltılmasıdır.

**"Tüm diller" alınırsa:** Windows'un desteklediği dil sayısı 100'ün üstünde. 100 dil =
47.500 dizge ve 11.200 ölçülen satır. Bu tur bu bütçeyle bitmez ve makine çevirisiyle
doldurulan bir sözlük depoya girdiği anda geri alınması pahalı bir yük olur.

Ölçülebilir öneri: önce altyapı, sonra **dar bir ilk küme** ile bir dilin gerçek maliyeti
ölçülür, sayı elde olunca küme genişletilir.
