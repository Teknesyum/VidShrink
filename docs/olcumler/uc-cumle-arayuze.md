# Uc dusme sebebi kullaniciya ulasiyor (T157)

T152 `PlanCalculator`i uc ayri cumle uretecek sekilde duzeltmisti; arayuz uc durumun
ucune de tek yerellestirme anahtari veriyordu, yani olculmemis bir aday icin de kullanici
"bu makinede kullanilamadi" okuyordu. Bu olcum ayrimin arayuze kadar tasindigini gosterir.

Dal: `T157-uc-cumle-arayuze`. Kusur commit'i `e9c16ca`, duzeltme `70ad1d1`.

## K1 — Once kirmizi

`PlanCalculatorProbeTests.ArayuzunDusmeCumlesiCoreunkiyleAyniSeyiSoyluyor` tek girdi
(`RecordingAvailability`) geziyordu ve o girdi tam da arayuzun ayrismadigi ucuncu duruma
dusuyordu. Olcu `[Theory]`ye cevrildi, uc durumu uc ayri girdi kuruyor:

| durum | girdi |
|---|---|
| derlemede yok | `MissingCodecAvailability("av1_nvenc", ("hevc_nvenc", Working))` |
| olculmedi | `FastOrderAvailability(("av1_nvenc", Unmeasured), ("hevc_nvenc", Working))` |
| olculdu, calismiyor | `FastOrderAvailability(("av1_nvenc", NotWorking), ("hevc_nvenc", Working))` |

`MissingCodecAvailability` eksik aday icin `HasEncoder` false dondururken `EncoderState`
icin `NotWorking` donuyor (T152 borc 4). Girdi bu ayrilmaya yaslanmiyor:
`PreferredCodecInBuild` yanlisken `EncoderFallbackCauseFor` durum sorusuna hic bakmiyor.

Kusur commit'inde (`e9c16ca`) iki durum kirmizi, biri yesil — ham cikti:

```
[xUnit.net 00:00:05.32]     ...ArayuzunDusmeCumlesiCoreunkiyleAyniSeyiSoyluyor(durum: "olculmedi") [FAIL]
[xUnit.net 00:00:05.32]     ...ArayuzunDusmeCumlesiCoreunkiyleAyniSeyiSoyluyor(durum: "derlemede yok") [FAIL]
  Basarisiz ...(durum: "olculmedi") [20 ms]
  Hata Iletisi:
   durum "olculmedi" | anahtar main.reason.encoder-fallback
  arayuz : the av1_nvenc encoder could not be used on this machine, so encoding falls back to hevc_nvenc
  core   : the av1_nvenc encoder has not been measured on this machine, so encoding falls back to hevc_nvenc
  Basarisiz ...(durum: "derlemede yok") [< 1 ms]
  Hata Iletisi:
   durum "derlemede yok" | anahtar main.reason.encoder-fallback
  arayuz : the av1_nvenc encoder could not be used on this machine, so encoding falls back to hevc_nvenc
  core   : the av1_nvenc encoder is not part of this ffmpeg build, so encoding falls back to hevc_nvenc

Basarisiz! - Basarisiz: 2, Basarili: 1, Atlanan: 0, Toplam: 3, Sure: 21 ms
```

Dusen iki durum: **derlemede yok** ve **olculmedi**. Yesil kalan durum
**olculdu, calismiyor** — arayuzun tek anahtari zaten o durumun cumlesiydi.

## K2 — Ayrim arayuze tasindi

`EncoderFallbackCause` (`src/VidShrink.Core/EncodePlan.cs`) Core'da uretiliyor ve
`ReasonNote.FallbackCause` ile arayuze geciyor. Arayuz esleyicileri:
`MainWindow.EncoderFallbackReasonKey` ve `MainWindow.EncoderFallbackAdviceKey`.

Anahtar degisikliginin tam listesi (`git diff main..HEAD -- src/VidShrink.App/Locales`,
`+`/`-` satirlari sayildi): iki dilde **2 anahtar dustu, 6 anahtar eklendi**; toplam
12 eklenen, 4 silinen satir.

Dusen anahtarlar (her ikisi de `en` ve `tr`de):

```
main.reason.encoder-fallback
main.advice.encoder-fallback
```

Eklenen anahtarlar (her biri `en` ve `tr`de):

```
main.reason.encoder-fallback-not-in-build
main.reason.encoder-fallback-not-measured
main.reason.encoder-fallback-not-working
main.advice.encoder-fallback-not-in-build
main.advice.encoder-fallback-not-measured
main.advice.encoder-fallback-not-working
```

`main.advice.encoder-fallback-gpu` dokunulmadi; hizli mod acikken uc durumun ucu de
bugunku tek cumlesini aliyor (`GpuKoluUcDurumdaDaAyniCumleyiVeriyor`).

`LocalizationTests` ham ciktisi (iki dilin anahtar kumesi birebir ayni):

```
Basarili!  - Basarisiz: 0, Basarili: 15, Atlanan: 0, Toplam: 15, Sure: 1 s
```

## K3 — Kullanicinin gordugu cumle

`PlanCalculatorProbeTests.UcCumleTablosunuYazar` koddan uretti; asagisi o kosumun ham
ciktisi (`--logger "console;verbosity=detailed"`):

```
 | durum | en | tr |
 |---|---|---|
 | derlemede yok | the av1_nvenc encoder is not part of this ffmpeg build, so encoding falls back to hevc_nvenc | av1_nvenc kodlayıcısı bu ffmpeg derlemesinde yok, bu yüzden hevc_nvenc'e düşüldü |
 | olculmedi | the av1_nvenc encoder has not been measured on this machine, so encoding falls back to hevc_nvenc | av1_nvenc kodlayıcısı bu makinede henüz ölçülmedi, bu yüzden hevc_nvenc'e düşüldü |
 | olculdu, calismiyor | the av1_nvenc encoder could not be used on this machine, so encoding falls back to hevc_nvenc | av1_nvenc kodlayıcısı bu makinede kullanılamadı, bu yüzden hevc_nvenc'e düşüldü |

 | durum | anahtar | tavsiye en | tavsiye tr |
 |---|---|---|---|
 | derlemede yok | main.reason.encoder-fallback-not-in-build | The Preferred Encoder Is Not Part Of This FFmpeg Build; Falling Back To A Software Encoder. | Tercih Edilen Kodlayıcı Bu FFmpeg Derlemesinde Yok; Yazılım Karşılığına Düşüldü. |
 | olculmedi | main.reason.encoder-fallback-not-measured | The Preferred Encoder Has Not Been Measured On This Machine; Falling Back To A Software Encoder. | Tercih Edilen Kodlayıcı Bu Makinede Henüz Ölçülmedi; Yazılım Karşılığına Düşüldü. |
 | olculdu, calismiyor | main.reason.encoder-fallback-not-working | The Preferred Encoder Could Not Be Used On This Machine; Falling Back To A Software Encoder. | Tercih Edilen Kodlayıcı Bu Makinede Kullanılamadı; Yazılım Karşılığına Düşüldü. |
```

Tavsiye satirinin bastan sona baslik bicimi olmasi bu sozlesmenin getirdigi bir sey degil:
`AdviceLine` `Speak` uzerinden geciyor, `Speak` de `LanguageCatalog.Title` cagiriyor. GPU
cumlesi de bugun ayni bicimde cikiyor.

Olcu pencere acmiyor. Okudugu sey: `src/VidShrink.App/Locales/{en,tr}/main.json`
dosyalari (`Locales.Values`, kaynaktan okur, cikti kopyasindan degil) ve
`MainWindow`in kendi statik esleyicileri. Arayuzun `ReasonLines` yolu ayni esleyiciyi
cagiriyor, yani olculen anahtar secimi kullaniciya giden anahtar secimidir.

## K4 — Mutasyon

Her mutasyondan once `dotnet build -c Release --no-incremental`; hicbir kosumda
`--no-build` ile eski ikili kosturulmadi (mutasyon uygulanip yeniden derlendikten sonra
`--no-build` yalniz taze ikiliyi kosturmak icin kullanildi). Filtre iki verify kolu:
`PlanCalculatorProbeTests|LocalizationTests`.

| mutasyon | olen olcu | sonuc |
|---|---|---|
| (a) uc reason anahtari tek anahtara donduruldu | `ArayuzunDusmeCumlesiCoreunkiyleAyniSeyiSoyluyor` (olculmedi, derlemede yok), `UcDurumUcAyriYerellestirmeAnahtarinaGidiyor`, `ArayuzunUcCumlesiIkiDildeDeAyriIddiaTasiyor` (en, tr), `LocalizationTests.KatalogdaBirikenOluCeviriListesiBuyumuyor` | 6 kirmizi / 58 |
| (b) `tr`den `main.reason.encoder-fallback-not-measured` silindi | `LocalizationTests.SevkiyattakiDillerinAnahtarKumesiIngilizceyleBirebirAyni`, `LocalizationTests.CagriYerineBaglanamayanAnahtarBicimliDizelerDeKatalogdaVar`, `TavsiyeSatiriDusmeCumlesiyleAyniIddiayiTasiyor` (tr), `UcDurumUcAyriYerellestirmeAnahtarinaGidiyor`, `ArayuzunUcCumlesiIkiDildeDeAyriIddiaTasiyor` (tr), `UcCumleTablosunuYazar` | 6 kirmizi / 58 |
| (c) reason anahtarlari takas edildi (olculmedi <-> calismiyor) | `ArayuzunDusmeCumlesiCoreunkiyleAyniSeyiSoyluyor` (olculmedi, olculdu-calismiyor), `TavsiyeSatiriDusmeCumlesiyleAyniIddiayiTasiyor` (en, tr) | 4 kirmizi / 58 |
| (d, ek) advice anahtarlari takas edildi (olculmedi <-> calismiyor) | `TavsiyeSatiriDusmeCumlesiyleAyniIddiayiTasiyor` (en, tr) | 2 kirmizi / 58 |

(d) sozlesmenin istedigi ucun disinda; tavsiye kolunun da pimli oldugunu gostermek icin
kosuldu.

(c)'nin ham ciktisindan tavsiye kolunun neyi yakaladigi:

```
 NotMeasured | could not be used on this machine | The Preferred Encoder Has Not Been Measured On This Machine; Falling Back To A Software Encoder.
```

Sol sutun Core'un o durum icin urettigi iddia, sag sutun tavsiye cumlesi; ikisi ayrilinca
olcu kirmizi.

Olcu metni sabitle karsilastirmiyor: uc cumlenin ortak onu ve ortak sonu atilip geriye
kalan **iddia** parcasi hesaplaniyor (`Iddialar`), sonra o parcanin tavsiye cumlesinde
gecmesi araniyor. Cumleler yeniden yazilinca olcu bozulmaz; bozulan tek sey ayrimin
kendisidir.

## K5 — T152'nin kazanimi

T152'nin uc olcusu gevsetilmeden ayakta:

```
dotnet test -c Release --no-build --filter "FullyQualifiedName~UcDurumUcFarkliCumleUretiyor|
  FullyQualifiedName~OlculmemisAdayinNotuBuMakinedeKullanilamadiDemiyor|
  FullyQualifiedName~DerlemedeOlmayanAdayinNotuOlcumIddiasiTasimiyor"

Basarili!  - Basarisiz: 0, Basarili: 3, Atlanan: 0, Toplam: 3, Sure: 15 ms
```

`PlanCalculator.cs`teki tek davranis degisikligi sebep hesabinin `EncoderFallbackCauseFor`a
cikarilmasi; uretilen uc Ingilizce cumle harfi harfine ayni kaldi. `PlanCalculatorTests`,
`LanguageTests`, `AdviceCoverageTests`, `OluUyeTests`, `HdrArgumentsTests` birlikte:

```
Basarili!  - Basarisiz: 0, Basarili: 119, Atlanan: 0, Toplam: 119, Sure: 3 s
```

`plan.Reason`in tek dis okuyucusu `tools/surucu-yoklugu/Program.cs:203`; `Reason` hala
noktali virgulle ayrilmis duz metin, bicimi degismedi. Proje ayrica derlendi, 0 hata.

## K6 — Verify kollari test buluyor

`--list-tests` ile kol basina sayim:

| kol | `--list-tests` | kosum |
|---|---|---|
| `PlanCalculatorProbeTests` | 43 | 43 gecti |
| `LocalizationTests` | 15 | 15 gecti |
| birlesik (`PlanCalculatorProbeTests\|LocalizationTests`) | 58 | 58 gecti |

Sifir bulan kol yok.

CI kosumu: rapor sonunda `gh run list` ile dogrulandi; kimlik ve sonuc asagida.

<!-- CI -->
