# Ilk olculmemis aday sirayi kilitliyordu (T151)

`PlanCalculator.PickFastCodec` hizli kipte `FastHardwareOrder` adaylarini sirayla
geziyordu ve ilk **olculmemis** adayda durup onu donduruyordu. Sirada calisan bir kodek
olsa bile ona bakmiyordu.

Makine: Windows 11 Pro 22631, .NET 8, Release. Dal `T151-ilk-olculmemis-aday`.

## K1 — secim ve gerekcesi

**Secim: 1 numarali cevap — devam et, sonuna kadar gez.** Olculmemis aday hatirlanir,
tarama durmaz; calisan aday bulunursa o doner, hicbiri calismiyorsa hatirlanan **ilk**
olculmemis aday `CodecNotMeasured` isaretiyle doner.

Ucunun de olculmesi gerekiyordu. Uc olcum secimi tutuyor.

### Olcum 1 — "ucuz cevap adayi ac birakir" riski gerceklesmiyor

Sozlesmenin itirazi suydu: olculmemisi atlarsan aday hic olculmez, sira hep calisan
eski kodege duser. **Olculdu, oyle olmuyor.** `EncoderAvailabilityState.KnownState`
sormakla olcume yollamayi ayni adima koyuyor:

```
KnownState(codec) -> EncoderState(codec)            // gecit: cevap yok, Unmeasured
                  -> IsMeasured(codec) -> Ready(..) // gecit: arka plan yoklamasini baslatir
```

Yani tarama bir adayin **uzerinden gecerken de** onu yoklamaya yolluyor. Olcu:
`GecilenOlculmemisAdayYoklamayaYollaniyor` — gercek `MainWindow.DeferredEncoderAvailability`
gecidi altinda tek bir `BuildDetailed`, sonra yoklamalarin bitmesi beklenir.

| kol | yoklamaya giden donanim adayi | gecidin baslattigi yoklama |
|---|---|---|
| T151 oncesi | 1 / 7 (`av1_nvenc`) | ilk adaydan sonra durur |
| T151 sonrasi | 7 / 7 | >= 7 |

Kirmizi koldaki ham cikti tam olarak eksigi sayiyor:

```
yoklamaya gitmeyen aday: hevc_nvenc, av1_qsv, hevc_qsv, av1_amf, hevc_amf, h264_nvenc
```

### Olcum 2 — bugunku davranis yakinsamiyor (3 numarali cevap eleniyor)

Yoklamasi **hic yerlesmeyen** bir aday kalici olarak `Unmeasured` kalir; bu T136'dan beri
pinli (`AnUnsettledProbeIsNeverPromotedToAMeasurement`). Boyle bir aday sirada birinci
olunca eski tarama onu **hicbir turda** gecemiyordu: `av1_nvenc` yoklamasi istisnayla
duser, `hevc_nvenc` olculur ve calisir, plan yine de `av1_nvenc` + `CodecNotMeasured`
doner. T139 tur 2'nin dogrulamasi o isareti "donanim yok"a ceviriyor — makinede calisan
donanim kodlayicisi varken yanlis-negatif.

Olcu: `YerlesmeyenAdaySirayiKaliciKilitlemiyor`. Gecit gercek, kaynak yoklama istisna
firlatiyor.

| kol | `gate.KnownState("av1_nvenc")` | `gate.KnownState("hevc_nvenc")` | plan kodegi | `CodecNotMeasured` |
|---|---|---|---|---|
| T151 oncesi | Unmeasured (kalici) | Working | `av1_nvenc` | evet |
| T151 sonrasi | Unmeasured (kalici) | Working | `hevc_nvenc` | hayir |

Bu kol yakinsama sorusunu kapatiyor: eski davranis "olcum gelince duzelir" degildi,
olcum **gelmiyordu**. 3 numarali cevap (bugunku davranis dogru) elendi.

### Olcum 3 — "olc, sonra karar ver" maliyeti (2 numarali cevap eleniyor)

Yoklama senkron ve aday basina; tarama boyunca cagiranin uzerine binen sure aday
sayisiyla carpiliyor. Olcu: `AdayBasinaYoklamaMaliyetiTaramaBoyuncaCarpiliyor`,
aday basina 20 ms'lik taklit gecikme, bes kosum:

| kosum | yoklama | taklit gecikme | gecen sure |
|---|---|---|---|
| 1 | 8 | 20 ms | 164 ms |
| 2 | 8 | 20 ms | 165 ms |
| 3 | 8 | 20 ms | 249 ms |
| 4 | 8 | 20 ms | 254 ms |
| 5 | 8 | 20 ms | 250 ms |

Sekiz yoklamanin yedisi donanim adayi, biri tavsiye kodlayicisi. Gercek yoklamanin
olculmus suresi `docs/olcumler/ui-yoklama-donmasi.md:40-44`'te **173-469 ms**; ayni
carpim gercek yoklamayla 1,2-3,3 s eder. Yerlesmeyen yoklamanin ust siniri ise 15 000 ms
(`ui-yoklama-donmasi.md:109`), yani secenek 2 tam da olcum 2'nin makinesinde arayuzu
aday basina 15 saniyeye kadar kilitlerdi. Secenek 2 elendi.

**Sonuc:** secenek 1 tek basina hicbir maliyet eklemiyor — yoklamalar zaten arka planda,
tarama yalnizca daha fazla adayi kuyruga koyuyor — ve iki yanlis-negatifi birden
kapatiyor.

## K2 — yanlis-negatif senaryosu teste dondu

Olcu `OlculmemisIlkAdayCalisanSonrakiAdayinOnunuKesmiyor`: `av1_nvenc` **Unmeasured**,
`hevc_nvenc` **Working**, geri kalan `NotWorking`. Beklenen plan `hevc_nvenc`,
`CodecNotMeasured` yok.

### Kirmizi kol (duzeltme geri alinmis, olculer yerinde)

```
  Başarısız VidShrink.Tests.PlanCalculatorProbeTests.OlculmemisIlkAdayCalisanSonrakiAdayinOnunuKesmiyor [2 ms]
  Hata İletisi:
Expected: "hevc_nvenc"
Actual:   "av1_nvenc"
  Başarısız VidShrink.Tests.PlanCalculatorProbeTests.GecilenOlculmemisAdayYoklamayaYollaniyor [5 ms]
  Hata İletisi:
   yoklamaya gitmeyen aday: hevc_nvenc, av1_qsv, hevc_qsv, av1_amf, hevc_amf, h264_nvenc
  Başarısız VidShrink.Tests.PlanCalculatorProbeTests.YerlesmeyenAdaySirayiKaliciKilitlemiyor [17 ms]
  Hata İletisi:
Expected: "hevc_nvenc"
Actual:   "av1_nvenc"
Başarısız! - Başarısız:     3, Başarılı:    35, Atlanan:     0, Toplam:    38, Süre: 38 s - VidShrink.Tests.dll (net8.0)
```

### Yesil kol (duzeltme yerinde)

```
Başarılı!  - Başarısız:     0, Başarılı:    38, Atlanan:     0, Toplam:    38, Süre: 38 s - VidShrink.Tests.dll (net8.0)
```

Iki kol da `dotnet build -c Release --no-incremental` ile derlendi; `--no-build` yalniz
derlemeden **sonra** test kosarken kullanildi.

## K3 — mutasyon izgarasi

Her hucre once `dotnet build -c Release --no-incremental`, sonra
`dotnet test -c Release --no-build --filter "PlanCalculatorProbeTests|EncoderStateConsumptionTests"`.

| mutasyon | ne degistirildi | sonuc | olen olcu |
|---|---|---|---|
| M0 | yok (duzeltilmis kol) | 38/38 gecti | — |
| M1 | `unmeasured ??= candidate; continue;` → `return candidate` (T151 oncesi davranis) | 3 kirmizi / 38 | `OlculmemisIlkAdayCalisanSonrakiAdayinOnunuKesmiyor`, `GecilenOlculmemisAdayYoklamayaYollaniyor`, `YerlesmeyenAdaySirayiKaliciKilitlemiyor` |
| M2 | `unmeasured ??= candidate` → `unmeasured = candidate` (ilk degil son olculmemis) | 3 kirmizi / 38 | `CalisanAdayYokkenHatirlananOlculmemisAdayDonuyor`, `AnUnmeasuredFastPathDoesNotBecomeANoHardwareVerdict`, `OlculmemisDonanimAdayiArayuzeDonanimVarDemiyor` |
| M3 | kuyruktaki `probe.NotMeasured` / `probe.CodecNotMeasured` atamalari silindi | 7 kirmizi / 38 | `CalisanAdayYokkenHatirlananOlculmemisAdayDonuyor`, `AnUnmeasuredFastPathDoesNotBecomeANoHardwareVerdict`, `YerlesmeyenYoklamaBaslatDugmesiniKilitlemiyor`, `PickFastCodecOlculmemisIlkAdayiElemedenGeciriyor`, `OlculmemisAdayiDogrulayanYoklamaGecerseDonanimVarDiyor`, `OlculmemisDonanimAdayiArayuzeDonanimVarDemiyor`, `OlculmemisDonanimAdayiHizliKipKutusunuAcmiyor` |

Ham cikti `.calisma/T151/k3-mutasyon.txt`'te uretildi; ucu de oldu.

## K4 — T139'un pinleri

`EncoderStateConsumptionTests` yesil kaldi. Iki sozlesme carpismiyor.

| kol | test sayisi (`--list-tests`) | sonuc |
|---|---|---|
| `EncoderStateConsumptionTests` | 7 | gecti |
| `PlanCalculatorProbeTests` | 31 (T151 oncesi 25, alti olcu eklendi) | gecti |
| birlesik verify kolu | 38 | `Başarılı! - Başarısız: 0, Başarılı: 38, Toplam: 38` |

7 + 31 = 38; birlesik kosumun bildirdigi toplamla ayni. Yeni alti olcu:
`OlculmemisIlkAdayCalisanSonrakiAdayinOnunuKesmiyor`,
`CalisanAdayYokkenHatirlananOlculmemisAdayDonuyor`,
`HicOlculmemisAdayYokkenYaziliYedegeDusuluyor`,
`GecilenOlculmemisAdayYoklamayaYollaniyor`,
`YerlesmeyenAdaySirayiKaliciKilitlemiyor`,
`AdayBasinaYoklamaMaliyetiTaramaBoyuncaCarpiliyor` — alti satir, alti ad.

## T150'nin pimi degismedi

T0'in sart kostugu ek kol: `dotnet test -c Release --filter "OluUyeTests"`.

```
Başarılı!  - Başarısız:     0, Başarılı:    11, Atlanan:     0, Toplam:    11, Süre: 650 ms
uye: 129  bu dosyada adi gecmeyen: 93  pimlenen: 31
sifir tuketici: 26  hic kullanilmayan: 5
```

Sayi **26**, T150 tur 2'nin temellendirdigi deger. Pim yeniden temellendirilmedi,
`OluUyeTests.cs` dosyasina dokunulmadi. Beklenen buydu: T151 `EncoderProbeState.Unmeasured`
ve `Working` tuketimini **yerinden oynatti ama kaldirmadi** — dongu ikisini de okumaya
devam ediyor, yalniz olculmemis kolun donusu geciktiriliyor.

## Yol ustunde gorulen, kapsam disi

`PlanCalculator.cs:197-202` yedek kodek notu artik su cumleyi uretebiliyor: "the
av1_nvenc encoder could not be used on this machine, so encoding falls back to
hevc_nvenc" — oysa `av1_nvenc` kullanilamadigi icin degil **olculmedigi** icin
gecildi. Not metni T151'in kapsaminda degil, degistirilmedi.
