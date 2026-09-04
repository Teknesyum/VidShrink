# T162 — kodek kilidi

`PlanOptions.LockedCodec` (`src/VidShrink.Core/PlanCalculator.cs`): kullanicinin acikca
sectigi kodlayici adi, nullable string, varsayilan `null`. Test dosyasi:
`tests/VidShrink.Tests/CodecLockTests.cs` (14 kol).

## K1 — tasarim gerekcesi

Aile (H.264/HEVC/AV1) degil **kodlayici adi** tutuluyor ("libx264", "av1_nvenc", ...).
Sebep: motorun yedege dusme mekanizmasi (`EncoderFallbackCause`, `PickCodec`/`PickFastCodec`)
zaten kodlayici adi uzerinden calisiyor; aile secilseydi "bu ailede hangi kodlayici"
sorusunu ikinci kez cozen ayri bir katman gerekirdi. Adlar `PlanParser.AllowedCodecs` ile
ayni kumeden (`KnownLockableCodecs`, PlanCalculator.cs) — bilinmeyen bir ad `ArgumentException`
ile aciyor patlar, sessizce libx264'e dusmez.

`LockedCodec == null` -> `options.Codec`/`AutoPreference` yolundan aynen gecilir (kod
degisikligi `lockedCodec is not null` dalina kapali). Kanit: `KilitBosKenPlanCiktisiDegismiyor`
(Compatible/MaxCompression/Auto icin), ham cikti:

```
Compatible: codec=libx264 mode=2pass videoK=1567 1612x906@30
Auto: codec=libsvtav1 mode=2pass videoK=1567 1920x1080@30
MaxCompression: codec=libsvtav1 mode=2pass videoK=1567 1920x1080@30
```

(Ayni satir hem `LockedCodec` alani hic ayarlanmadan hem `LockedCodec = null` ile acikca
kurulan `PlanOptions`ten geliyor, ikisi bit bit ayni.)

## K2 — uc kilit izgarasi

Kaynak: 1920x1080@30, 500 MB, h264. Hedef: 6 MB (Extreme rejim). Kilit: libx264/libx265/libsvtav1.

| kilit | secilen | mode | videoK | cozunurluk | fps |
|---|---|---|---|---|---|
| libx264 | libx264 | 2pass | 353 | 768x432 | 30 |
| libx265 | libx265 | 2pass | 353 | 922x518 | 25 |
| libsvtav1 | libsvtav1 | 2pass | 353 | 1458x820 | 30 |

Kodlayici degisince cozunurluk (ve libx265'te fps) degisiyor — `ComplexityProfile`nin
`RelativeBitrateNeed(codec)` uzerinden aldigi kodek etkisi arama yoluna geciyor.
Kol: `UcKilitFarkliBitrateVeCozunurlukUretiyor`.

## K3 — uc sebep

Kilit: `av1_nvenc`. Ham cikti:

```
NotInBuild: codec=libsvtav1 sebep=NotInBuild istenen=av1_nvenc dusulen=libsvtav1
NotMeasured: codec=av1_nvenc codecNotMeasured=True hardwareNotMeasured=True   (yedege dusme yok)
NotWorking: codec=libsvtav1 sebep=NotWorking istenen=av1_nvenc dusulen=libsvtav1
```

`NotMeasured` durumunda yedege **dusulmuyor** (kilit gecici olarak kendisiyle kullaniliyor,
`PlanResult.HardwareNotMeasured=true`) — bu, T151'de olculen "olculmedi ile calismiyor
karismasin" kuralinin kilitli koldaki karsiligi. Kol: `UcSebepBirbirindenFarkli`,
`KilitliKodlayiciDerlemedeYoksaNotInBuildIleDusuyor`,
`KilitliKodlayiciOlculmediyseGeciciKendisiKullanilirVeYedegeDusmez`,
`KilitliKodlayiciOlcupCalismiyorsaNotWorkingIleDusuyor`.

## K4 — Auto once/sonra

`MainWindow.axaml.cs:1513` `CodecPreference.Auto` kuruyor, `LockedCodec` hic ayarlamiyor ->
`lockedCodec == null` dalina dusuyor, kod bu sozlesmeden once ne calisiyorsa yine o calisiyor
(degisen tek satir kilit varken calisan yeni bir dal, Auto dalina dokunulmadi).
Ham cikti (Extreme rejim, 6 MB hedef):

```
Auto: codec=libsvtav1 mode=2pass videoK=353 1458x820@30
the search cleared ...; scaled to 1458x820 (75,9% of source); ...; two-pass VBR spends 5,76 MB, the band center of the 6 MB target; predicted quality 58,9/100 estimated from the source bitrate
```

Once/sonra karsilastirmasi: bu depoda mevcut `PlanCalculatorTests.cs`nin 32 kolu, `Auto`
dahil hicbir CodecPreference kolunu degistirmeden 32/32 yesil kaldi (asagida).

## K5 — mutasyon izgarasi

Her mutasyondan once `dotnet build -c Release --no-incremental` calistirildi, `--no-build`
kullanilmadi.

| mutasyon | ne yapildi | kirilan olcu |
|---|---|---|
| (a) kilidi yok say | `PlanCalculator.cs:200`: `lockedCodec is not null` -> `false` | `UcKilitFarkliBitrateVeCozunurlukUretiyor`, `UcSebepBirbirindenFarkli`, `KilitliDonanimKodlayiciCalisiyorsaOnuKullaniyor`x3, `KilitliKodlayiciDerlemedeYoksaNotInBuildIleDusuyor`, `KilitliKodlayiciOlcupCalismiyorsaNotWorkingIleDusuyor`, `KilitliKodlayiciOlculmediyseGeciciKendisiKullanilirVeYedegeDusmez` (8 kol) |
| (b) bitrate hesabi kodege bakmasin | `PlanCalculator.cs:262`: `SearchLayout(..., codec, ...)` -> `SearchLayout(..., "libx264", ...)` | `UcKilitFarkliBitrateVeCozunurlukUretiyor` |

Mutasyon (a) ham hata:
```
Assert.Equal() Failure: Collections differ
Expected: ["libx264", "libx265", "libsvtav1"]
Actual:   ["libsvtav1", "libsvtav1", "libsvtav1"]
```

Mutasyon (b) ham hata:
```
uc kilit ayni bitrate/cozunurluk uretti (videoK=353, 768x432), kilit isleve etki etmiyor
```

Her iki mutasyon da geri alindi, sonrasinda `dotnet build -c Release --no-incremental` ve
`dotnet test -c Release --filter "FullyQualifiedName~PlanCalculatorTests"` (32/32) +
`FullyQualifiedName~CodecLockTests` (14/14) yesil.

## K6 — kol sayisi

`dotnet test -c Release --filter "FullyQualifiedName~CodecLockTests" --list-tests`: **14 kol**
bulundu (sifir degil).
