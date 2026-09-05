# T162 — kodek kilidi

`PlanOptions.LockedCodec` (`src/VidShrink.Core/PlanCalculator.cs`): kullanicinin acikca
sectigi kodlayici adi, nullable string, varsayilan `null`. Test dosyasi:
`tests/VidShrink.Tests/CodecLockTests.cs` (15 kol; T166 denetiminden sonra, bkz. K2/K5).

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

## K2 — uc kilit izgarasi (T166'da ayrildi)

**T166 denetim bulgusu:** eski tek tablo/tek kol uc kilidin de yazilim oldugunu (libx264,
libx265, libsvtav1) gizliyordu. `PlanCalculator.cs:139` `DeliveryReserveK` yalniz donanim
kodlayicida 11, yazilimda 0 oldugundan uc yazilim kilidi arasinda bitrate **hicbir zaman**
ayrisamaz — bu tesadufi degil, tasarimin kendisi. Eski kol `bitrates > 1 || resolutions > 1`
VEYA'siyla bunu ortuyordu: cozunurluk ayrismasi tek basina kolu yesil tutuyordu, bitrate
yarisi hic dogrulanmadan geciyordu.

**Cozum: iddia ikiye ayrildi, her biri kendi kolunda, VEYA yok.**

Yazilim izgarasi — ayni kaynak, ayni hedef, degisen tek sey kilit, iddia yalniz cozunurluk:

| kilit | kaynak | hedef | rejim | secilen | mode | videoK | cozunurluk | fps |
|---|---|---|---|---|---|---|---|---|
| libx264 | 1920x1080@30, 500 MB, h264 | 6 MB | Extreme | libx264 | 2pass | 353 | 768x432 | 30 |
| libx265 | 1920x1080@30, 500 MB, h264 | 6 MB | Extreme | libx265 | 2pass | 353 | 922x518 | 25 |
| libsvtav1 | 1920x1080@30, 500 MB, h264 | 6 MB | Extreme | libsvtav1 | 2pass | 353 | 1458x820 | 30 |

Kodlayici degisince cozunurluk (ve libx265'te fps) degisiyor — `ComplexityProfile`nin
`RelativeBitrateNeed(codec)` uzerinden aldigi kodek etkisi arama yoluna geciyor. Bitrate
(videoK) uc satirda da 353: yazilim kilitleri arasinda ayrismaz, iddia edilmiyor. Kol:
`UcYazilimKilidiFarkliCozunurlukUretiyor`.

Donanim satiri eklenmis izgara — bitrate ayrismasinin kaniti:

| kilit | kaynak | hedef | rejim | secilen | mode | videoK | cozunurluk | fps |
|---|---|---|---|---|---|---|---|---|
| libx264 | 1920x1080@30, 500 MB, h264 | 6 MB | Extreme | libx264 | 2pass | 353 | 768x432 | 30 |
| libx265 | 1920x1080@30, 500 MB, h264 | 6 MB | Extreme | libx265 | 2pass | 353 | 922x518 | 25 |
| libsvtav1 | 1920x1080@30, 500 MB, h264 | 6 MB | Extreme | libsvtav1 | 2pass | 353 | 1458x820 | 30 |
| av1_nvenc | 1920x1080@30, 500 MB, h264 | 6 MB | Extreme | av1_nvenc | 2pass | 342 | 576x324 | 30 |

`av1_nvenc` (donanim) videoK=342, uc yazilim kilidinin 353'unden ayrisiyor —
`HardwareDeliveryReserveK=11`'in devreye girmesi. Bu makinede gercek NVENC donanimi
yoklanmadi; olcu saf hesap uzerinden yuruyor (`CodecModel.IsHardware` kodlayici adina
bakan bir dize karsilastirmasi, `MinBitrateK`/`DeliveryReserveK` de olculmus sabit
formuller — ikisi de gercek GPU'yu calistirmadan, `FakeAvailability` ile deterministik
kosuyor), o yuzden makinede NVIDIA GPU olmamasi iddiayi zayiflatmiyor. Kol:
`DonanimKilidiYazilimdanFarkliBitrateUretiyor`.

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

**T166 denetim bulgusu (duzeltildi):** `UcSebepBirbirindenFarkli`nin son satiri
(`Assert.Equal(3, new object?[] { sebepler[0], "unmeasured-marker", sebepler[2] }.Distinct().Count())`)
kosulsuz dogruydu — orta eleman elle konmus sabit bir dizgeydi, kenardakiler zaten ustteki
uc `Assert` ile sabitlenmisti; dusemeyen, susleme bir olcuydu. Satir silindi. Gercek ayrimi
zaten ustteki `Assert.Equal(NotInBuild, sebepler[0])` / `Assert.Null(sebepler[1])` /
`Assert.Equal(NotWorking, sebepler[2])` yapiyor — bu ucu mutasyon K5(b)'de kirilarak
dogrulandi.

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

**T166'da yeniden kosuldu** (iki mutasyon, artik T166'nin yeni kollarini ve K3'un
gercek olcusunu de hedef aliyor). Her mutasyondan once `dotnet build -c Release
--no-incremental` calistirildi, `--no-build` kullanilmadi; her ikisi de sonrasinda geri
alindi.

| mutasyon | ne yapildi | kirilan olcu |
|---|---|---|
| (a) kilidi yok say | `PlanCalculator.cs:200`: `lockedCodec is not null` -> `false` | `UcYazilimKilidiFarkliCozunurlukUretiyor`, `DonanimKilidiYazilimdanFarkliBitrateUretiyor`, `UcSebepBirbirindenFarkli`, `KilitliDonanimKodlayiciCalisiyorsaOnuKullaniyor`x3, `KilitliKodlayiciDerlemedeYoksaNotInBuildIleDusuyor`, `KilitliKodlayiciOlcupCalismiyorsaNotWorkingIleDusuyor`, `KilitliKodlayiciOlculmediyseGeciciKendisiKullanilirVeYedegeDusmez` (9 kol) |
| (b) K3'un olcusunu bosalt | `PlanCalculator.cs:934-937`: `EncoderFallbackCauseFor` ucyollu ternary'sini tek `EncoderFallbackCause.NotWorking;` donusune indirgedi (NotInBuild/NotMeasured ayrimi kayboldu) | `KilitliKodlayiciDerlemedeYoksaNotInBuildIleDusuyor`, `UcSebepBirbirindenFarkli` (2 kol) |

Mutasyon (a) ham hata (ozet, tam calistirma 9/15 basarisiz):
```
Başarısız! - Başarısız: 9, Başarılı: 6, Toplam: 15
KilitliDonanimKodlayiciCalisiyorsaOnuKullaniyor(locked: "hevc_nvenc")
Assert.Equal() Failure: Strings differ
Expected: "hevc_nvenc"
Actual:   "libsvtav1"

UcSebepBirbirindenFarkli
Assert.Equal() Failure: Values differ
Expected: NotInBuild
Actual:   NotWorking
```

Mutasyon (b) ham hata (2/15 basarisiz):
```
Başarısız! - Başarısız: 2, Başarılı: 13, Toplam: 15
KilitliKodlayiciDerlemedeYoksaNotInBuildIleDusuyor [FAIL]
UcSebepBirbirindenFarkli [FAIL]
  Assert.Equal() Failure: Values differ
  Expected: NotInBuild
  Actual:   NotWorking
```

Her iki mutasyon da geri alindi, sonrasinda `dotnet build -c Release --no-incremental` ve
`dotnet test -c Release --filter "FullyQualifiedName~PlanCalculatorTests|FullyQualifiedName~CodecLockTests"`
tek komutta 47/47 yesil (32 `PlanCalculatorTests` + 15 `CodecLockTests`).

## K6 — kol sayisi

`dotnet test -c Release --filter "FullyQualifiedName~CodecLockTests" --list-tests`: **15 kol**
bulundu (T162'de 14'tu). Fark +1: T166, K2'nin tek kolunu (VEYA'li,
`UcKilitFarkliBitrateVeCozunurlukUretiyor`) ikiye ayirdi —
`UcYazilimKilidiFarkliCozunurlukUretiyor` (cozunurluk, 3 yazilim kilidi) ve
`DonanimKilidiYazilimdanFarkliBitrateUretiyor` (bitrate, donanim satiri eklenmis 4 kilit)
olarak. K3'un dusemeyen satiri (eski `:238`) bir kol silmedi, `UcSebepBirbirindenFarkli`
metodunun icindeki tek bir `Assert` satiriydi; metod kendisi zaten gercek asserlar
tasidigi icin kaldi.
