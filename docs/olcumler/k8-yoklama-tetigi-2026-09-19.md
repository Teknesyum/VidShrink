# K8 Borcu 3 — Geçici Ölçüm Arayüzünün Sökülmesi

19 Eylül 2026. Ölçüler `tests/VidShrink.Tests/PlanCalculatorProbeTests.cs` ve
`PlanCalculatorTests.cs`, mutasyon düzeneği `.calisma/borc3/mutasyon.py`,
ham çıktı `.calisma/borc3/sonuc.txt`.

## Borcun öncülü yanlıştı

Borç "T129 birleşince `IEncoderMeasurementState` kalkar" diyordu; T129 birleşmişti ama arayüz
duruyordu. Sebep gecikme değil, borcun yanlış yazılmasıydı: arayüz **yalnız durum taşımıyordu**,
arka plan yoklamasını kuyruğa alan tetik de oydu.

`DeferredEncoderAvailability.IsMeasured` çağrısı `Ready(...)` üzerinden `Measure(...)` doğuruyor.
Core'daki iki çağrı yerini "geçici arayüz" diye silmek ertelenmiş yoklamayı tümden durdururdu:
hiçbir kodlayıcı hiç ölçülmez, üç durumlu yüz kalıcı olarak `Unmeasured` döndürürdü.

## Yapılan

Tetik cevabın yanına taşındı. `EncoderState` sorulan kodlayıcı yerleşmemişse ölçümü kendisi
kuyruğa alıp `Unmeasured` döner; `Hdr10State` aynısını hdr10 anahtarı için yapar ve adaptör
artık `IHdr10ProbeAvailability`'yi uyguluyor.

Böylece Core tarafı üç durumlu cevabı alıp **ikinci bir çağrıyla** ölçüm başlatmıyor.
`EncoderAvailabilityState.KnownState` tek satıra indi, `HdrResolver` "ölçülmedi" kolunu
`Hdr10State(codec) == Unmeasured` ile okuyor, `IEncoderMeasurementState` silindi.

İki test sahtesi yeni yüze geçti. `PlanCalculatorTests.OlculmemisMakine`'nin `EncoderState`'i
ölçülmüş kodlayıcıda `WorksAsEncoder`'a iniyor — eski sahtede ölçüm sayacını artıran yol buydu
ve `OlculmusKodlayiciIcinGecicilikIsaretiKonmuyor` o sayıyı pimliyor. Sahteyi kümelerden
okutmak ölçüyü sessizce zayıflatırdı; ilk turda tam bunu yapıp kırmızı aldım.

## Mutasyon dökümü

Taban 0/86, geri 0/86. Beş kesim, beşi kırmızı.

| Kesim | Kırmızı |
|---|---|
| M1 tetik kalktı (ölçülmemiş kodlayıcı kuyruğa girmiyor) | 3 |
| M2 üçüncü durum iki duruma çöktü | 5 |
| M3 hdr10 kolu üçüncü durumu kaybetti | 1 |
| M4 `HdrResolver` "ölçülmedi" kolunu okumuyor | 1 |
| M5 Core dikişi iki durumlu yüze düştü | 20 |

## Eklenen ölçüler

`PlanCalculatorProbeTests` üç ölçü kazandı: `EncoderState`'e sormak ölçümü kuyruğa alıyor
(olumsuz kontrol: sorulmayan kodlayıcı yoklamaya gitmiyor), aynısı `Hdr10State` kolunda,
ve yerleşen cevap tekrar sorulunca ikinci bir yoklama doğurmuyor.

Mevcut ölçüler korundu: `GecilenOlculmemisAdayYoklamayaYollaniyor` zinciri uçtan uca
ölçüyor ve tetik taşındıktan sonra da yeşil — kuyruklama davranışı değişmedi.
