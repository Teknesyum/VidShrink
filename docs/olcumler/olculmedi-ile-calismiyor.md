# "Olculmedi" ile "calismiyor" ayrimi yedege dusme notunda

T152. Dal `T152-olculmedi-ile-calismiyor`, olcum tarihi 3 Eylul 2026.

T151 `PickFastCodec`in taramasini ilk olculmemis adayda durdurmaktan cikardi. Bu, yedege
dusme notuna yeni bir yol acti: `av1_nvenc` olculmemis ve `hevc_nvenc` calisan makinede
artik `codec != preferredCodec` oluyor, not **ilk kez** cikiyor ve tek cumle oldugu icin
olculmemis bir donanim icin "bu makinede kullanilamadi" diyordu. Olcum yokken boyle bir
iddiada bulunulamaz.

## K1 - uc yol ayrildi

Uc cumle `PlanCalculator.EncoderFallbackReason` icinde uretiliyor. Metinler bir olcuden
dokuldu: `UcDurumUcFarkliCumleUretiyor` icine gecici bir `Assert.Fail` konup kaldirildi.
Asagidaki blok o kosumun ham ciktisidir, ayirici `>>>`:

```
the av1_nvenc encoder could not be used on this machine, so encoding falls back to hevc_nvenc >>> the av1_nvenc encoder has not been measured on this machine, so encoding falls back to hevc_nvenc >>> the av1_nvenc encoder is not part of this ffmpeg build, so encoding falls back to hevc_nvenc
```

| Durum | Girdi (`SdrSource`, `FastPreserve`) | Uretilen cumle |
| --- | --- | --- |
| (a) olculdu, calismiyor | `av1_nvenc = NotWorking`, `hevc_nvenc = Working` | `the av1_nvenc encoder could not be used on this machine, so encoding falls back to hevc_nvenc` |
| (b) olculmedi | `av1_nvenc = Unmeasured`, `hevc_nvenc = Working` | `the av1_nvenc encoder has not been measured on this machine, so encoding falls back to hevc_nvenc` |
| (c) derlemede yok | `HasEncoder("av1_nvenc") = false`, `hevc_nvenc = Working` | `the av1_nvenc encoder is not part of this ffmpeg build, so encoding falls back to hevc_nvenc` |

Yeni yerellestirme anahtari **acilmadi**; gerekcesi asagida "Kalan borc" altinda.

## K2 - kusur once kirmiziya dustu

Kirmizi commit `d599853`, icinde yalniz olcu var. Ham cikti:

```
[xUnit.net 00:00:05.41]     VidShrink.Tests.PlanCalculatorProbeTests.UcDurumUcFarkliCumleUretiyor [FAIL]
[xUnit.net 00:00:05.41]     VidShrink.Tests.PlanCalculatorProbeTests.OlculmemisAdayinNotuBuMakinedeKullanilamadiDemiyor [FAIL]
[xUnit.net 00:00:05.41]     VidShrink.Tests.PlanCalculatorProbeTests.DerlemedeOlmayanAdayinNotuOlcumIddiasiTasimiyor [FAIL]
  Hata Iletisi:
   Assert.Equal() Failure: Values differ
Expected: 3
Actual:   1
  Hata Iletisi:
   Assert.DoesNotContain() Failure: Sub-string found
                                v (pos 22)
String: ..."e av1_nvenc encoder could not be used on "...
Found:  "could not be used"
Basarisiz! - Basarisiz:     3, Basarili:     0, Atlanan:     0, Toplam:     3, Sure: 13 ms
```

Olcu cumlenin metnini sabitle karsilastirmiyor. Iki girdi yan yana kuruluyor ve yalniz
ayrimin durup durmadigina bakiliyor: olculmemis adayin notunda `could not be used`
**gecmemeli**, olculmus-calismayan adayinkinde **gecmeli**, ikisi birbirinden farkli olmali.

## K3 - mutasyon izgarasi

Her hucrede `dotnet build -c Release --no-incremental` kosuldu. `--no-build` yalniz o
derlemeden **sonraki** test adiminda kullanildi.

| Hucre | Mutasyon | Sonuc |
| --- | --- | --- |
| Duzeltilmis | - | `Basarisiz: 0, Basarili: 3, Toplam: 3` |
| M1 | `EncoderFallbackReason` govdesi eski tek cumleye dondurulur | `Basarisiz: 3, Basarili: 0, Toplam: 3` |
| M2 | `PickFastCodec` icindeki `probe.PreferredCodecState = state` satiri silinir | `Basarisiz: 2, Basarili: 1, Toplam: 3` |
| M3 | `PickFastCodec` icindeki `probe.PreferredCodecInBuild = ...` satiri silinir | `Basarisiz: 2, Basarili: 1, Toplam: 3` |

M1 ham cikti:

```
[xUnit.net 00:00:05.46]     PlanCalculatorProbeTests.UcDurumUcFarkliCumleUretiyor [FAIL]
[xUnit.net 00:00:05.46]     PlanCalculatorProbeTests.OlculmemisAdayinNotuBuMakinedeKullanilamadiDemiyor [FAIL]
[xUnit.net 00:00:05.46]     PlanCalculatorProbeTests.DerlemedeOlmayanAdayinNotuOlcumIddiasiTasimiyor [FAIL]
Basarisiz! - Basarisiz:     3, Basarili:     0, Atlanan:     0, Toplam:     3, Sure: 14 ms
```

M2 ham cikti, olen ikisi:

```
[xUnit.net 00:00:06.40]     PlanCalculatorProbeTests.UcDurumUcFarkliCumleUretiyor [FAIL]
[xUnit.net 00:00:06.40]     PlanCalculatorProbeTests.OlculmemisAdayinNotuBuMakinedeKullanilamadiDemiyor [FAIL]
  Hata Iletisi:
   Assert.Equal() Failure: Values differ
Expected: 3
Actual:   2
  Hata Iletisi:
   Assert.DoesNotContain() Failure: Sub-string found
String: ..."e av1_nvenc encoder could not be used on "...
Found:  "could not be used"
Basarisiz! - Basarisiz:     2, Basarili:     1, Atlanan:     0, Toplam:     3, Sure: 17 ms
```

M3 ham cikti, olen ikisi:

```
[xUnit.net 00:00:05.40]     PlanCalculatorProbeTests.UcDurumUcFarkliCumleUretiyor [FAIL]
[xUnit.net 00:00:05.40]     PlanCalculatorProbeTests.DerlemedeOlmayanAdayinNotuOlcumIddiasiTasimiyor [FAIL]
Basarisiz! - Basarisiz:     2, Basarili:     1, Atlanan:     0, Toplam:     3, Sure: 14 ms
```

M2 ve M3'te ayakta kalan olcu her seferinde **oteki** kolun olcusu: M2 derleme-yoklugu
kolunu bozmuyor, M3 olculmemislik kolunu bozmuyor. Ucu birlikte notun uc dalinin da
pimlendigini gosteriyor.

## K4 - kol basina test sayisi

Sayilar `dotnet test -c Release --no-build --list-tests --filter <kol>` ciktisinda
`VidShrink.Tests.` ile baslayan satirlarin sayimidir. Sifir bulan kol yok.

| Kol | Test sayisi | Sonuc |
| --- | --- | --- |
| `PlanCalculatorProbeTests` | 34 | yesil |
| `PlanCalculatorTests` | 32 | yesil |
| verify satirinin ikisi birlikte | 66 | `Basarisiz: 0, Basarili: 66, Toplam: 66` |
| `OluUyeTests`, T150'nin pimi | 11 | `Basarisiz: 0, Basarili: 11, Toplam: 11` |

66 = 34 + 32, yani iki kol ortusmuyor. Ek olarak komsu kollar
`HdrResolver|EncoderStateConsumptionTests|EncoderAvailabilityTests` 20 test buldu ve
hepsi yesil.

T151'in olculeri `PlanCalculatorProbeTests` kolunun icinde ve altisi da yesil:
`OlculmemisIlkAdayCalisanSonrakiAdayinOnunuKesmiyor`,
`CalisanAdayYokkenHatirlananOlculmemisAdayDonuyor`,
`HicOlculmemisAdayYokkenYaziliYedegeDusuluyor`,
`GecilenOlculmemisAdayYoklamayaYollaniyor`,
`YerlesmeyenAdaySirayiKaliciKilitlemiyor`,
`AdayBasinaYoklamaMaliyetiTaramaBoyuncaCarpiliyor`.

## Yol ustunde bulunan sey: durumu ikinci kez sormak yoklama sayacini kiriyor

Ilk uygulamada `EncoderFallbackReason` durumu dogrudan `availability.KnownState(...)` ile
soruyordu. O bir yoklama daha demek. `PlanCalculatorTests` icinde aday basina yoklama
sayisini pimleyen olculer var ve dordu birden kirildi:

```
PlanCalculatorTests.FastTercihiListedeOlupCalismayanDonanimiSecmiyor [FAIL]        Expected: 1  Actual: 2
PlanCalculatorTests.HizliKipDonanimYoklamasinaBagliKaliyor [FAIL]                  Expected: 1  Actual: 2
PlanCalculatorTests.MaxCompressionListedeOlupCalismayanKodlayiciyiSecmiyor [FAIL]  Expected: 2  Actual: 3
PlanCalculatorTests.OlculmusKodlayiciIcinGecicilikIsaretiKonmuyor [FAIL]           Expected: 1  Actual: 2
```

Bu yuzden durum ikinci kez sorulmuyor. `PickCodec` ve `PickFastCodec` tercih edilen adayin
durumunu zaten okuyor; okuduklarini `ProbeState.PreferredCodecState` ve
`ProbeState.PreferredCodecInBuild` ile disari tasiyorlar. Not o degerleri kullaniyor ve
yeni surec dogurmuyor.

## Kalan borc - kullaniciya ulasan cumle hala tek

Duzeltme `plan.Reason` uzerinde. Arayuzun gosterdigi cumle **oradan gelmiyor**:
`MainWindow.ReasonLines` `plan.ReasonCodes` uzerinde donuyor ve `ReasonCode.EncoderFallback`
icin tek anahtari cagiriyor (`MainWindow.axaml.cs`, `Say("main.reason.encoder-fallback", ...)`).
`plan.Reason` bugun yalniz `tools/surucu-yoklugu` tanilamasinda okunuyor.

Yani ayrim cekirdekte duruyor ama **kullaniciya henuz ulasmiyor.** Ulasmasi icin gereken uc
dosyanin ucu de T152'nin `owns` kumesi disinda, ikisi sozlesmede adiyla disarida birakilmis:

- `src/VidShrink.Core/EncodePlan.cs` - `ReasonCode` icin iki yeni uye: olculmemis aday ve
  derlemede olmayan aday.
- `src/VidShrink.App/Locales/*/main.json` - iki yeni anahtar. Sozlesme "yerellestirme
  anahtari gerekiyorsa once bildir, uydurma" dedigi icin anahtar acilmadi.
- `src/VidShrink.App/MainWindow.axaml.cs` - `ReasonLines` switch'ine iki kol.

Ayni sey `AdviceCode.EncoderFallback` ve `main.advice.encoder-fallback` icin de gecerli: o
metin de ("The preferred encoder could not be used on this machine") uc durumu ayirmiyor.
T0 karari.
