# Hiç Bağlanmamış Kare Kesme Yolu Kaldırıldı (19 Eylül 2026)

## Bulgu

Kod borcu 8 "önizleme türlerinin beş üyesi üretiliyor, okuma tarafında adı geçmiyor"
diyordu. Kök daha derinde: üyeleri üreten **sınıfın kendisi** üretimde hiç koşmuyor.

`git log -S "new FrameGrabber" -- src/` boş dönüyor — sınıf `5a44468b` ile eklendi,
bağlayan commit hiç gelmedi. `GrabPairAsync`'in üretimde tek çağıranı yok. Onu besleyen
`PreviewStatus.Derive` de öyle: kendi belgesi "arayüz kendi koşullarından durum uydurmaz,
bunu çağırır" diyor, çağıran arayüz hiç olmadı.

Karşılaştırma paneli T176'dan beri libmpv yolundan besleniyor
(`EngineComparisonFrameSource`); ffmpeg'den kare kesen bu yol onun altında kaldı.

## Karar

`trash/`'a taşındı: `FrameGrabber.cs`, `PreviewTimeline.cs`, `KeyframeIndex.cs` ve iki
test dosyası. `PreviewSegment` canlı kalıyor (`SegmentEncoder` kullanıyor), o yüzden
`PreviewQuality`'nin iki pimi bu işin dışında ve borç olarak duruyor.

Paylaşılan xUnit geçitleri (`FfmpegFact`, `TonemapFact`, `HardwareEncoderFact`,
`QuietMachineFact`, `NoEncoders`) `FrameGrabberTests.cs`'in başında yaşıyordu ve depo
genelinde kullanılıyor; `tests/VidShrink.Tests/TestGecitleri.cs`'e ayrıldı.

## Ölçüm

Ölçü taşımanın kendisi değil, **kümenin tam olarak ne kadar küçüldüğü**:
`OluUyeTests.OluOzellikYuzeyiPimlenenKume` ölçülen ölü üye kümesiyle pim listesinin birebir
eşit olmasını istiyor. Fazladan bir satır kalsa da, beklenmedik bir satır gelse de kırmızı.

Üç tur kırmızı verdi ve her biri gerçek bir yan etkiydi:

| Tur | Kümede beliren | Ne demekti |
| --- | --- | --- |
| 1 | `GrabbedFrame.*` altı pim artık kümede yok | taşınan türün üyeleri |
| 2 | `KeyframeIndex.IsEmpty` **yeni** ölü | tek okuyucusu ölü sınıftı; tür de taşındı |
| 3 | `TimelinePoint.*` beş pim artık kümede yok | taşınan dosyanın ikinci türü |

Sonuç: 21 düz pim ve 3 borç pimi düştü, başka hiçbir satır kaymadı.
`OluUye` + `PreviewSegment` + `OlcekModulu` süzgeci **80/80**, CI bayraklarıyla
(`-c Release -warnaserror`) derleme temiz.

`KeyframeIndex`'in ikinci turda ortaya çıkması bu işin asıl bulgusu: ölü kod yalnız kendi
satırlarını değil, yalnızca kendisinin okuduğu başka bir türü de ayakta tutuyordu.
