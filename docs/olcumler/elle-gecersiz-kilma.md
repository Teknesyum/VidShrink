# T165 — elle gecersiz kilma

Motorun sekiz karari devraliniyor (`docs/danisma/004-arayuz-yonu-gorusu.md`nun "acilacak"
listesi). Kilit yapisi T162'nin `LockedCodec`iyle ayni desen: bos deger "secim yok",
dolu deger motorun ilgili hesabini devre disi birakiyor, geri kalan her sey otomatik.
Yeni alanlar `PlanOptions` (`src/VidShrink.Core/PlanCalculator.cs`): `LockedMode`,
`LockedCrf`, `LockedPreset`, `LockedAudioKbps`, `AudioChannels` (`AudioChannelOverride`),
`MinResolutionHeight`, `MinFps`, `EncoderPath` (`EncoderPathOverride`). Test dosyasi:
`tests/VidShrink.Tests/ManualOverrideTests.cs` (19 kol, K2_05 ve K5 birer theory ile 23'e
cikiyor).

## K1 — varsayilan hicbir seyi degistirmiyor

Bes farkli kaynak/hedef bilesimi, sekiz alanin hicbiri sabitlenmemisken (`null`/`Auto`)
uretilen plan `once`/`sonra` birebir ayni. Ham cikti (`K1_VarsayilanHicbirSeyiDegistirmiyor`):

```
1920x1080@30 -> 25MB:  once codec=libsvtav1 mode=2pass videoK=1567 1920x1080@30 ses=128k/kaynak
                        sonra ayni
1280x720@24 -> 8MB:    once codec=libsvtav1 mode=2pass videoK=188  1202x676@24  ses=26k/1
                        sonra ayni
3840x2160@60 -> 50MB:  once codec=libsvtav1 mode=2pass videoK=9016 3840x2160@60 ses=128k/kaynak
                        sonra ayni
1920x1080@30 -> 6MB:   once codec=libsvtav1 mode=2pass videoK=80   690x388@30   ses=0k/kaynak
                        sonra ayni
1280x720@30 -> 100MB:  once codec=libx264   mode=2pass videoK=27305 1280x720@30 ses=128k/kaynak
                        sonra ayni
```

Bes bilesimin bes inde de codec, mode, videoK, crf, cozunurluk, fps, ses hedefi/kanali,
preset ve `Reason` metni tek karakter farksiz. Kol: `K1_VarsayilanHicbirSeyiDegistirmiyor`
(`MemberData` ile 5 kol).

## K2 — sekiz kalemin her biri gercekten geciyor

| kalem | sabitlenen | uretilen ffmpeg argumaninda gorunen | kol |
|---|---|---|---|
| EncodeMode | TwoPass | dogal `crf` (90MB, QualityCeiling) -> zorlaninca `-b:v 353k ... -pass 2`, `-crf` yok | `K2_01_EncodeModeTwoPassSabitleniyor` |
| EncodeMode | Crf | dogal `2pass` -> zorlaninca `-crf 41 -maxrate 470k -bufsize 940k` | `K2_01b_EncodeModeCrfSabitleniyor` |
| CRF | 19 | `-crf 19 -maxrate 58052k -bufsize 116104k`, videoK tahmini 29026'ya sicradi | `K2_02_CrfDegeriElleSabitleniyor` |
| preset / hiz | veryslow | `-preset veryslow` | `K2_03_PresetElleSabitleniyor` |
| ses hedefi | 96kbps | `-b:a 96k`, videoK 1567->1599 (ses payi geri video'ya) | `K2_04_SesHedefiKbpsElleSabitleniyor` |
| ses kanali | Stereo | `-ac 2` | `K2_05_SesKanaliElleSabitleniyor(Stereo)` |
| ses kanali | Mono | `-ac 1` | `K2_05_SesKanaliElleSabitleniyor(Mono)` |
| ses kanali | yok | `-an`, `AudioCodec=null` | `K2_05b_SesYokSabitleniyor` |
| cozunurluk tabani | en az 720p | dogal `1036x582` -> tabanli `1306x734`, `scale=1306:734` | `K2_06_CozunurlukTabaniElleSabitleniyor` |
| kare hizi tabani | en az 24 | dogal `15fps` -> tabanli `60fps` (24 istenirken tam kaynak fps'e sicradi, cunku 24'un altina inmeyen tek deliverable katman kaynak fps'iydi) | `K2_07_KareHiziTabaniElleSabitleniyor` |
| kodlayici yolu | Software | donanim tercihi (Fast/nvenc) varken `libsvtav1` (yazilim) | `K2_08_KodlayiciYoluYazilimaZorlaniyor` |
| kodlayici yolu | Hardware | yazilim tercihi (Compatible) varken `av1_nvenc` (donanim) | `K2_08b_KodlayiciYoluDonanimaZorlaniyor` |

Sekiz kalemin tamami sayildi: EncodeMode, CRF, preset, ses hedefi, ses kanali, cozunurluk
tabani, kare hizi tabani, kodlayici yolu — 8/8. Ham komut satirlari yukaridaki tabloda;
tam ffmpeg argumanlari test cikisinda (`.calisma/T165/ham-cikti.txt`, bu depoya girmez).

Ornek tam satir (CRF elle):
```
crf=19 videoK=29026 args=ffmpeg -hide_banner -y -hwaccel auto -i sample.mp4 -vf scale=1612:906:flags=lanczos
  -c:v libx264 -preset slow -crf 19 -maxrate 58052k -bufsize 116104k -g 300 -keyint_min 30
  -pix_fmt yuv420p -c:a aac -b:a 128k -movflags +faststart out.mp4
```

## K3 — CRF sabitlenince hedef boyut tahmine doner

`K3_CrfSabitlenenPlaninHedefBoyutuZorlanmiyor`: CRF 23 sabitlenince plan `mode=crf`,
`SizeEstimate.Enforced=false` (bir butce degil bir tahmin) ve tahmin bandi genis
(`122,3-237,5MB`, tek nokta degil bir yayilim), hedefin (`25MB`) cok uzaginda:

```
crf=23 mode=crf estimate=179,9MB (band 122,3-237,5) hedef=25MB
```

Motor CRF olmasaydi 2pass ile tam 25MB'a kilitlerdi; CRF sabitlenince kazanan CRF,
boyut bir sonuc oluyor.

## K4 — gecersiz kilma plan panelinde gerekceleniyor

`ReasonNote` iki yeni alan tasiyor: `ManualOverrideValue` (kullanicinin sabitledigi deger)
ve `EngineWouldHaveChosen` (motorun kendi sececegi deger). `K4_ReasonNoteMotorunKendiSeciminiDeTasiyor`
ham ciktisi (CRF 30 sabitlenmis, motor 2pass@1567k secmis olurdu):

```
ManualOverrideValue=30 EngineWouldHaveChosen=2pass@1567k
reason=scaled to 1612x906 (83,9% of source); ...; the budget lands near CRF 44,3, short of
  the CRF 20 ceiling, so two-pass VBR spends 24,38 MB, the band center of the 25 MB target;
  predicted quality 66,7/100 estimated from the source bitrate; kullanici CRF'i 30 olarak
  sabitledi; hedef boyut artik zorlanmiyor, 8145k yalniz bir tahmin — motor 2pass kipinde
  2pass@1567k secmisti
```

Motorun kendi secimi (`2pass@1567k`) kaybolmuyor, cumlenin icinde acikca duruyor. Ayni
desen sekiz overrid'in hepsinde: `ManualModeOverride`, `ManualCrfOverride`,
`ManualPresetOverride`, `ManualAudioBitrateOverride`, `ManualAudioChannelsOverride`,
`ManualMinResolutionOverride`, `ManualMinFpsOverride`, `ManualEncoderPathOverride`
`ReasonCode`larinin her biri bu iki alani doldurur (bkz K2 testlerindeki
`Assert.Contains(..., n => n.Code == ReasonCode.Manual...)` satirlari).

## K5 — kapali kalanlar kapali kaldi

`K5_PlanOptionsKapaliSabitleriDisaAcmiyor`: `PlanOptions`un tum public alanlari pinlendi —
17 alan, hicbiri `FillBand`, `RegimeFloors`, ses butce payi, `EncoderFallback` mantigi,
retry dongusu ya da `CodecModel` sabitlerine dogrudan bir kapi acmiyor:

```
PlanOptions alanlari: TargetMb, Intent, Codec, AllowResolutionDrop, AllowFpsDrop, HdrPolicy,
FillPolicy, SpeedMode, LockedCodec, LockedMode, LockedCrf, LockedPreset, LockedAudioKbps,
AudioChannels, MinResolutionHeight, MinFps, EncoderPath
```

`K5_KapaliTiplerAlanKumesiDegismedi`: kapali iki tipin alan kumesi pinlendi, disaridan yeni
ayar eklenmedi:

```
RegimeFloors: MinScale, MinHeight, MinFps
FillBand: LowerMb, HardFloorMb, UpperMb, CenterMb, RelativeWidth
```

Tek tek liste:
- `FillBand` (hedefin bandı) — alan sayisi degismedi, disaridan set edilemiyor.
- `RegimeFloors` (rejim tabanlari) — alan sayisi degismedi; `MinResolutionHeight`/`MinFps`
  bu tipi degil, `PlanCalculator.EffectiveFloors`in **cikisini** etkiliyor (bkz asagi),
  tipin kendisi acilmadi.
- ses butce payi (`CompressionStrategy.AudioBudgetShare`) — dokunulmadi, `CompressionStrategy.cs`
  bu sozlesmenin `owns` disi, okunuyor yaziliyor degil.
- `EncoderFallback` mantigi (`EncoderFallbackCauseFor`, `PickCodec`, `PickFastCodec`) —
  degismedi; `EncoderPath` override'i bu fonksiyonlarin **sonucunu** post-filtreliyor,
  fonksiyonlarin kendisi acilmadi.
- retry dongusu (`PlanCalculator.Correct`, `RetryAimMb`) — dokunulmadi.
- `CodecModel` sabitleri (donanim tabani, kalite tavani) — dokunulmadi, `CodecModel.cs`
  bu sozlesmenin `owns` disi.

## K6 — mutasyon izgarasi

Her mutasyondan once `dotnet build -c Release --no-incremental`, `--no-build` yasak,
her mutasyon sonra geri alindi ve build+test yesile dondu.

| mutasyon | ne yapildi | kirilan olcu |
|---|---|---|
| (a) sabitlenen degeri yok say | `PlanCalculator.cs`: `if (options.LockedCrf is double manualCrf)` -> `if (false && ...)` | `K2_02_CrfDegeriElleSabitleniyor`, `K3_CrfSabitlenenPlaninHedefBoyutuZorlanmiyor`, `K4_ReasonNoteMotorunKendiSeciminiDeTasiyor` (3 kol) |
| (b) K3'u geri al | Ayni blokta `plan.Mode = "crf";` satiri kaldirildi (Mode "2pass" kaliyor) | `K2_02_CrfDegeriElleSabitleniyor`, `K3_CrfSabitlenenPlaninHedefBoyutuZorlanmiyor` (2 kol) |
| (c) K4'un gerekcesini uretme | Ayni blokta `reasonCodes.Add(new ReasonNote(ReasonCode.ManualCrfOverride, ...))` kaldirildi | `K3_CrfSabitlenenPlaninHedefBoyutuZorlanmiyor` (K3 testi de ManualCrfOverride notunu ariyor), `K4_ReasonNoteMotorunKendiSeciminiDeTasiyor` (2 kol) |

Mutasyon (a) ham hata (kisaltilmis):
```
K3: Expected "crf" Actual "2pass"
K4: Assert.Single() Failure: The collection did not contain any matching items
K2_02: Expected "crf" Actual "2pass"
```

Mutasyon (b) ham hata:
```
K2_02: Expected "crf" Actual "2pass"   (crf=23 mode=2pass estimate=179,9MB ... hedef=25MB)
```

Mutasyon (c) ham hata:
```
K3: Assert.Contains() Failure: Filter not matched in collection (ManualCrfOverride yok)
K4: Assert.Single() Failure: The collection did not contain any matching items
```

Uc mutasyon da geri alindi; sonrasinda `dotnet build -c Release --no-incremental` ve
`dotnet test -c Release --filter "FullyQualifiedName~ManualOverrideTests"` 23/23,
`FullyQualifiedName~CodecLockTests` 14/14 yesil.

## K7 — kol sayisi

```
dotnet test -c Release --filter "FullyQualifiedName~ManualOverrideTests" --list-tests
```
19 adlandirilmis kol bulundu (K1 ve K5'in `MemberData`/`Theory` genisletmeleriyle
calisma aninda 23'e cikiyor, `--list-tests` genisletmeden once sayiyor). Sifir degil.

```
dotnet test -c Release --filter "FullyQualifiedName~CodecLockTests" --list-tests
```
14 kol bulundu (T162'den degismedi). Sifir degil.

Calisma zamaninda toplam: `ManualOverrideTests` 23/23, `CodecLockTests` 14/14,
`PlanCalculatorTests` 32/32 (K1 baseline), genis regresyon taramasi (`PlanCalculatorTests`,
`CodecLockTests`, `ManualOverrideTests`, `FfmpegArgumentsTests`, `FillBandTests`,
`FpsDropTests`, `SpeedModeTests`, `QualityTargetTests`, `QualityTargetUiTests`,
`PlanCalculatorProbeTests`) 250/251 (1 atlanan `LiveFillTargetRunStaysInsideTheBand`,
gercek ffmpeg gerektiren canli test, filtresiz suitte de atlaniyor).
