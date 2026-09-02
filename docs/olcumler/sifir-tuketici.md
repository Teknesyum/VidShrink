# Sifir uretim tuketicisi — tanim ve olcu

T150. Onculu T149; oncesinde ayni bulgu yedi kez yazildi ve yedincisi sozlesmenin kendi
onayladigi cevapti. Bu belge kusuru degil, **olcuyu** kuruyor: hangi tur uyesinin uretimde
tuketicisi yok, sayisi kac, hangisi kasitli hangisi degil.

Olcu `tests/VidShrink.Tests/OluUyeTests.cs` icinde durur ve `dotnet test -c Release
--filter "OluUyeTests"` ile kosar.

## K1 — Tanim

> **Sifir uretim tuketicisi.** Bir tur uyesi U, `src/**` icinde en az bir yerde
> **uretiliyor** (donduruluyor, atanıyor, bir tabloya konuluyor) ama hicbir yerde
> **tuketilmiyor** — yani U'ya esitlik, `switch` kolu, `case`, orunt birlestiricisi ya da
> tablo anahtari olarak bakan tek satir yok.

Sozlesmedeki baslangic onerisi degistirilmedi; yalnizca "tuketici" listesi karar
verilebilir olsun diye kapali bir dilbilgisi konumu listesine baglandi (asagida). Iki
komsu sinif ayrildi, cunku ikisi de bu tanima **girmiyor** ama olcunun ayni kosumunda
cikiyor:

- **hic kullanilmayan uye** — uretimi de yok. Tanim "en az bir yerde uretiliyor" dedigi
  icin bu ayri bir bulgudur; olcu ikisini de sayar, ayri etiketler.
- **yalniz disarida kullanilan uye** — uretimde hic gorunmuyor, testlerde/araclarda
  goruluyor. Bu sinifin yedi olayinda tekrar eden "testler onu tek tuketici olarak ayakta
  tutuyor" cumlesinin olculebilir hali budur.

Onemli bir sinir: **`_` kolu tuketici sayilmaz.** `switch`in son kolu bir uyeyi adiyla
anmadan yutuyorsa o uye hakkinda hicbir karar yazilmamis demektir. T149'un
`EncoderVendor.VideoToolbox`u tam bu yuzden sifir tuketiciliydi — `QualityArgs`in son kolu
onu `-crf`e goturuyordu ve kimse bunu secmemisti (K5).

### Yedi olayin uygunlugu

Sutunlar: **tanima giriyor mu**, ve **K2 duzenegi olcuyor mu**. Ikisi ayni sey degil:
duzenek `VidShrink.Core`un **tur uyelerini** sayiyor, olaylarin cogu ise ayni bagintinin
metot/asiri yukleme dilimi.

| # | Olay | Varlik | Tanima giriyor | K2 duzenegi olcuyor | Neden |
|---|---|---|---|---|---|
| 1 | T137 | `EncoderCapabilities.ProbeOutcome.Unmeasured` | evet | **hayir** | Enum uyesi, ama `internal` ve `VidShrink.Ffmpeg` altinda; duzenek Core'un uyelerini sayiyor. |
| 2 | T140 | `CodecModel.SupportsTurboFirstPass` + `TurboFirstPassCeilings` | evet | **hayir** | Metot ve `private` tablo; tur uyesi degil. Ozellik kuruldu, acilma karari olcume birakildi — yani uretim kolu yoktu. |
| 3 | T142 K3 | `ComplexityProbe.PlanWindows` / `PlanWindowCount` | evet | **hayir** | Metot. `CalibrationProbe` kendi sabit kopyasini kosuyordu, bu duzenegi cagirmiyordu. |
| 4 | T143 (1) | `QualityMeter.WorstScene(..., SceneMap?)` asiri yuklemesi | evet | **hayir** | Asiri yukleme. Uretimdeki tek cagri uc argumanliydi, `map` kolu yalniz testlerden kosuyordu. |
| 5 | T143 (2) | `QualityScore.SceneWindowSeconds` alani | evet | **hayir** | Ornek alani; uretimde kosulsuz `2.0` yaziliyor, okuyan kol ayrim yapmiyordu. |
| 6 | T144 | `FfmpegRun.StandardError` alani | evet | **hayir** | Ornek alani. Metin tutuluyordu, basari karari onu hic okumuyordu. |
| 7 | T149 | `EncoderVendor.VideoToolbox` | evet | **evet** | Core'da `public enum` uyesi — duzenegin kumesine giren tek olay. |

**Yedisi de tanima giriyor; K2 duzenegi yedisinden birini olcuyor.** Tanim yanlis degil,
duzenek dar: sozlesme kumeyi acikca "`src/VidShrink.Core` altindaki tur uyeleri" diye
sinirladi. Kalan alti olayin dilimi — metot, asiri yukleme ve ornek alani icin
"uretimde cagirani yok" — ayri bir duzenek ister; cagri yeri sayan bir olcunun kalibi
depoda var (T148, derlenmis IL'den cagri yeri sayimi). **Bu bir borc, asagida yazili.**

Yedi olayin bugunku hali de bakildi: 1, 3, 4, 6 kendi sozlesmelerinde kapanmis
(`EncoderCapabilities.cs:96` artik `result.Measured ? result.Succeeded : HasEncoder(codec)`,
`CalibrationProbe.cs` `ComplexityProbe`i cagiriyor, `QualityMeter.cs:295` `sceneMap`i
tasiyor, `FfmpegRunner.cs:138` `DroppedOptionLines`i okuyor). 2 icin `FfmpegArguments.cs`
tek uretim tuketicisi. 7 icin K5'e bak.

## K2 — Kume turden turetilir

**Kume:** `VidShrink.Core` derlenmis derlemesi uzerinde **yansima**. `VidShrink.Core`
ad alanindaki her tur icin: enum ise butun uyeleri, degilse `public static readonly`
alanlari. Derleyicinin urettigi turler dusuruluyor. Anahtar kelime listesi yok — yeni bir
uye eklenince kume kendiliginden buyuyor ve pim kirmiziya donuyor.

**Konum karari:** `src/**` altindaki her `.cs` dosyasi okunuyor; once **yorumlar ve dizgi
sabitleri bosluga ceviriliyor** (satir sonlari korunuyor, indisler kaymiyor), sonra
`Tur.Uye` gorunumleri araniyor. T150 oncesi bir tarama tam bu adimi atlayip enum govdesini
docstring'leriyle ayristirmis ve 63 "olu uye" bildirmisti; adlarin yarisi docstring
metniydi.

**Tuketici konumlarin kapali listesi** (`MemberScan.ConsumerRules`) — hepsi C# dilbilgisi
konumu, tahmin degil:

| kural | konum |
|---|---|
| `case` | `case Tur.Uye:` |
| `esitlik-sol` / `esitlik-sag` | `x == Tur.Uye`, `Tur.Uye != x` |
| `orunt-onek` | `is Tur.Uye`, `not Tur.Uye`, `or Tur.Uye` |
| `orunt-birlestirici` | `Tur.Uye or ...`, `Tur.Uye and ...` |
| `switch-kolu` | `Tur.Uye => ...` (kolun **solu**) |
| `arama-cagrisi` | `Contains(Tur.Uye)`, `ContainsKey(...)`, `TryGetValue(...)`, `HasFlag(...)` |
| `arama-dizini` | `tablo[Tur.Uye]` — okuma; `[Tur.Uye] = ...` tablo girdisidir, uretim sayilir |

Bunlarin hicbirine uymayan her gorunum **uretim**dir.

Ad nitelemesi kuralin onune gecmesin diye uyeden onceki `Ad.` zinciri dusuruluyor.
Bu olmadan `result.Failure == CoreShare.ShareFailure.Cancelled` satirinda kural `==`
yerine `CoreShare.` goruyor ve iki gercek tuketici uretim sayiliyordu: ilk kosum 30,
duzeltilmis kosum 28 verdi.

### Duzenegin dayandigi iki kosul

Tarama `Tur.Uye` bicimindeki **nitelenmis** gorunumleri ariyor. Bu iki kosul saglanmazsa
tuketici gozden kacar; ikisi de olculdu:

1. **Uye adi niteliksiz yazilamaz.** `src/` altinda tek bir `using static` yok
   (`grep -rn "using static" src/` bos doner), yani bir enum uyesine `Nvenc` diye
   basvurulamiyor — C# zaten `case`/`switch` kolunda niteleme istiyor.
2. **Basit tur adi tek anlamli.** Yansimanin verdigi 129 anahtarin (`Tur.Uye`) hicbiri
   tekrar etmiyor; `VidShrink.Core` altinda ayni basit ada sahip iki tur yok, bu yuzden
   `type.Name` kullanmak iki turu birbirine karistirmiyor.

Niteleyici zinciri ayrica soyuluyor: `CoreShare.ShareFailure.Cancelled` gibi bir yazimda
"onceki pencere" `CoreShare.` ile bitiyordu ve `==` gorunmuyordu. Bu kusur ilk kosumda
iki uyeyi yanlis siniflandirdi (bkz. yukarida, 30 → 28).

### Ham cikti

**Ham cikti dosyalari `.calisma/T150/` altinda ve `.gitignore`'da — dalla birlikte
gelmiyorlar.** Denetci onlari aramasin: kararı tasiyan her parca bu rapora **satir icine**
alindi, geri kalani `dotnet test -c Release --filter "OluUyeTests" --logger
"console;verbosity=detailed"` ile yeniden uretiliyor. `TheScanDumpsEveryMemberItFound`
dokumu her kosumda ayni cikiyor, kaydedilmis kopyaya ihtiyac yok.

`OluUyeTests.TheScanDumpsEveryMemberItFound` her uyeyi, her gorunumu, gorunumu siniflayan
kurali ve dosya:satiri basiyor. Kosum:

```
dotnet test -c Release --filter "OluUyeTests" --logger "console;verbosity=detailed"
```

Ozet satiri (K5 uygulandiktan sonraki agac):

```
dosya: 70  uye: 129
sifir tuketici: 27  hic kullanilmayan: 5
dizgi/yorum icinde kalan gorunum: 20
```

129 uyenin 32'si bulgulu: 27 sifir tuketicili, 5 hic kullanilmayan. Kalan 97 uye
tuketiliyor.

**Saklanan 20 gorunumun hepsi docstring satiri.** `TheStripperHidesOnlyCommentsAndStringLiterals`
bunu pimliyor: saklanan her gorunumun satiri `///` ile baslamak zorunda. Bir gun bir uye
yalniz bir dizgi sabitinin icinde geciyorsa bu test kirmizi olur ve kirpicinin o uyeyi
gizledigi gorulur — 63'un tekrarlamasini engelleyen kol budur.

**"Desen listesi yok" kaniti:** `TheMemberSetComesFromTheAssemblyNotFromThisFile` olcunun
kendi kaynagini okuyup, buldugu 129 uyeden kacinin adinin test dosyasinda hic gecmedigini
sayiyor. Bugun 93 tanesinin adi gecmiyor; kume pim listesinden turemiyor, yansimadan turuyor.

## K3 — Sayi pimlenir

Pim `OluUyeTests.Pinned`: 32 satir, her satirda uye adi, **hesaplanan bicim** ve karar.
Bicim yorum degil, olcunun kendi verisinden cikan bir siniflandirma:

| bicim | anlami | bugun |
|---|---:|---:|
| `varsayilan-kol` | ayni turun baska uyesi okunuyor, bu uye okuma tarafinda hic adlandirilmiyor | 25 |
| `hic-okunmayan-tur` | turun **hicbir** uyesi okunmuyor | 2 |
| `yalniz-disarida` | uretimde sifir gorunum, testlerde/araclarda var | 2 |
| `hic-gorunmeyen` | hicbir yerde gorunmuyor | 3 |

`TheZeroConsumerSetIsThePinnedSet` bulunan kumeyi bu listeyle **tam** karsilastiriyor —
fazla satir da eksik satir da kirmizi.

### Mutasyon izgarasi

Her kosumda **`dotnet build -c Release --no-incremental`** kosuldu; `--no-build` yalniz
o yeniden derlemeden hemen sonraki `dotnet test`te kullanildi, yani olculen ikili her
zaman mutasyonu tasiyan ikili. Derleme hatasi sayisi her kosumda 0.

Ciktilar `.calisma/T150/` altinda (gitignore'da, dalla gelmiyor); mutasyonlar geri
alindigi icin yeniden uretilemezler, bu yuzden **karari tasiyan satirlar asagida
tam metinleriyle duruyor.** Tablodaki dosya adlari yalniz hangi kosumun hangi hucreye
denk geldigini soyluyor.

| # | mutasyon | beklenen | eski kol | yeni kol |
|---|---|---|---|---|
| 1 | `EncoderVendor`e `Rkmpp` uyesi + `CodecModel.Vendor`da `c.Contains("rkmpp")` uretici satiri | kirmizi (sayi 27 → 28) | `m1-eski.txt` 11/11 yesil | `m1-yeni.txt` 10/11, `TheZeroConsumerSetIsThePinnedSet` kirmizi |
| 2 | `QualityArgs`a acik `EncoderVendor.Software => new[] { "-crf", exact }` kolu | kirmizi (sayi 27 → 26) | `m2-eski.txt` 11/11 yesil | `m2-yeni.txt` 10/11, `TheZeroConsumerSetIsThePinnedSet` kirmizi |

**Mutasyon 1 — yeni sifir tuketicili uye.** `Rkmpp` uretiliyor ama hicbir yerde okunmuyor,
yani tam olcunun aradigi bicim. Kodek adlarindan hicbiri `rkmpp` icermedigi icin uretim
davranisi degismiyor; kirmizi yalniz olcuden geliyor. Hata satiri:

```
Actual: [..., "EncoderVendor.Rkmpp  varsayilan-kol", "EncoderVendor.Software  varsayilan-kol", ...]
Basarisiz: 1, Basarili: 10, Toplam: 11
```

**Mutasyon 2 — var olan uyeye gercek tuketici.** `EncoderVendor.Software` acik bir switch
koluna cikinca `switch-kolu` kurali onu tuketici sayiyor ve uye kumeden dusuyor:

```
Expected: [..., "EncoderVendor.Software  varsayilan-kol", "FfmpegArguments.SceneMapRuleOfRecord  yalniz-disar"...]
Actual:   [..., "FfmpegArguments.SceneMapRuleOfRecord  yalniz-disar"..., "FillPolicy.QualityCeiling  varsayilan-kol", ...]
Basarisiz: 1, Basarili: 10, Toplam: 11
```

Iki mutasyon da geri alindi; `git diff src/VidShrink.Core/CodecModel.cs` yalnizca K5'in 15
satirini gosteriyor. Geri alma sonrasi taban `m-eski.txt`: 11/11 yesil.

**Ikinci mutasyonun kalici ornegi zaten var.** K5'in patlayan kolu `EncoderVendor.VideoToolbox`u
gercekten tuketici konumuna tasidi ve sayi 28 → 27 dustu — mutasyon 2 ile ayni bicim,
ama gecici degil. Bkz. K5.

## K4 — Yedi olayin karari ve beyaz liste

Beyaz liste pimin kendisidir: her satir `mesru` ya da `borc` tasir ve **gerekcesiz satir
yoktur** — `EveryPinnedFindingCarriesAReason` her satirin en az 60 karakterlik bir gerekce
cumlesi tasidigini ve kararin taninan iki degerden biri oldugunu dogruluyor.

- **`mesru`** — uyenin okuma tarafinda adlandirilmamasi kasitli, gerekcesi kodda ya da
  bir olcumde yazili. Bugun 9 satir.
- **`borc`** — T150 bu uyeyi **siniflamadi**. Mesru oldugu gosterilmedi, kaza oldugu da
  gosterilmedi. Bugun 23 satir. Bu satirlar "gerekcesiz" degil; gerekce yerine **neyin
  olculmedigi** yaziyor. Dusurmek uretim davranisini degistirdigi ve cogu bu sozlesmenin
  `owns` listesi disinda oldugu icin karar ayri bir sozlesmenin isi.

Yedi olayin karari:

| # | Olay | Karar | Gerekce |
|---|---|---|---|
| 1 | T137 `ProbeOutcome.Unmeasured` | **kaza**, kapandi | Sozlesme KRITIK sayip duzeltti: `WorksAsEncoder` artik ucuncu cevabi yutmuyor (`EncoderCapabilities.cs:96`). |
| 2 | T140 turbo ilk gecis | **mesru** | Ozellik kuruldu, varsayilan olcume birakildi; bugun `FfmpegArguments.cs` uretim tuketicisi. |
| 3 | T142 K3 `PlanWindows` | **kaza**, kapandi | `CalibrationProbe` sabit kopyasini birakip duzenegi cagiriyor. |
| 4 | T143 (1) `WorstScene(map)` | **kaza**, kapandi | Uretim cagrisi artik `sceneMap`i tasiyor (`QualityMeter.cs:295`). |
| 5 | T143 (2) `SceneWindowSeconds` | **kaza**, kapandi | Deger kosullu hale getirildi. |
| 6 | T144 `FfmpegRun.StandardError` | **kaza**, kapandi | Basari karari artik metni okuyor (`FfmpegRunner.cs:138`). |
| 7 | T149 `EncoderVendor.VideoToolbox` | **mesru** | `CodecModel.cs:142-146` docstring'i: donanim kapisinin arkasindaki hicbir sabit VideoToolbox'ta olculmedi, kapiyi acmak bir olcumdur. Kapi acilmadi. |

**Yedincinin kodda karsiligi degisti ve bunu saklamiyorum:** K5 `QualityArgs`e VideoToolbox
icin acik bir koruma kolu koydu. O kol bir `switch` kolu oldugu icin olcu artik
`EncoderVendor.VideoToolbox`u **tuketiliyor** sayiyor; uye pim listesinde yok. Bu bir
tuketici uydurmasi degil, K5'in kendi gerekcesiyle yazilmis bir kol — ama sayiyi
degistirdigi icin izgarada ayrica gosteriliyor (mutasyon 2).

## K5 — `QualityArgs` mayini

`src/VidShrink.Core/CodecModel.cs`, eski hali:

```csharp
_ => new[] { "-crf", exact }
```

VideoToolbox bu kola dusuyordu. `-crf` kabul etmiyor; kapi acildigi gun sessizce gecersiz
bir bayrak uretilirdi — ve bu depoda olculmus bir gercek var: taninmayan anahtar
dusuruluyor, ffmpeg **0 ile cikiyor** (`docs/olcumler/handbrake-acigi.md:139`).

**Secim: acikca patla.** Kendi hiz kontrolunu dondurmek `-q:v` olceginin bir karsiligini
yazmak demekti; o olcegin bu depoda dayanagi yok. `docs/olcumler/videotoolbox.md` bir Apple
M1'de kol basina **tek bir bit hizi** veriyor, bir olcek cikarmaya yetmiyor. Sozlesme de
bunu yasakladi: olculmemis bir olcek yazma. Geriye tek dogru cevap kaliyor.

```csharp
EncoderVendor.VideoToolbox => throw new NotSupportedException(
    $"VideoToolbox hiz kontrolu olculmedi ({codec}): -crf kabul edilmiyor ve -q:v olceginin "
    + "bu depoda dayanagi yok. Kapiyi acan sozlesme olcegi olcup bu kolu yazar."),
```

Uretim davranisi degismedi: `PlanParser.AllowedCodecs` ve `PreviewSegment.ModelledCodecs`
videotoolbox kodeklerini gecirmiyor, yani bugun bu kola ulasan yok. `TheGateStaysClosed`
iki dosyada da `videotoolbox` gecmedigini pimliyor; kapinin kendi olcusu
`PlanParserTests.ParserStillRejectsVideoToolboxEncoders`.

Testler: `VideoToolboxDoesNotFallIntoTheCrfArm` (iki kodek) istisnayi ve mesajin kodek
adini + `-q:v`yi tasidigini dogruluyor; `TheSoftwareArmStillProducesCrf` (uc kodek) son
kolun yazilim kodlayicilarina eskisi gibi `-crf` verdigini dogruluyor.

**Kapsam disinda birakilan, bilerek:** `BitrateRateControlArgs` (`CodecModel.cs`) da
VideoToolbox'i `_ => Array.Empty<string>()` koluna dusuruyor. Bos arguman listesi gecersiz
bayrak uretmedigi icin sessiz kusur degil, ama kapi acildiginda oraya da bir karar
gerekecek. Sozlesme yalniz `:183`u adlandirdi; not olarak birakildi.

## K6 — Verify kolu kac test buluyor

Tek kol var: `--filter "OluUyeTests"`.

```
dotnet test -c Release --filter "OluUyeTests" --list-tests
```

Sonuc: **11 test**, hepsi `VidShrink.Tests.OluUyeTests`
altinda. Ikisi `VideoToolboxDoesNotFallIntoTheCrfArm` kodek durumu, ucu
`TheSoftwareArmStillProducesCrf` kodek durumu, geri kalan alti tekil.

`dotnet test` filtresinin varsayilan isleci `FullyQualifiedName~`; sifir teste denk gelen
bir kol cikis kodu 0 ile sessizce gecer. Sayi bu yuzden yazildi: kolun bos olmadigi
`--list-tests` ile dogrulandi, `Toplam: 11` ile koseleri tutuyor.

CI kosumu: RUNID-PLACEHOLDER

## Kalan borc

1. **Cagri yeri dilimi olculmedi.** Yedi olayin altisi metot / asiri yukleme / ornek alani;
   K2 duzenegi tur uyesi sayiyor. "Uretimde cagirani yok" bagintisi icin ayri bir olcu
   gerekiyor; kalip depoda var (T148 derlenmis IL'den cagri yeri sayiyor).
2. **23 satir siniflanmadi.** Pimin `borc` satirlari. En kalabaligi `ShareFailure`:
   on bir uyeden sekizi uretiliyor, hicbiri ayrilmiyor; arayuz yalniz `Cancelled`, `None`,
   `TokenExpired` ve `Unknown` soruyor.
3. **`ArchitectureOutcome` docstring'i ile kodu ayrisiyor.** `UpdateCheck.cs:34`
   "Kullaniciya ne soylenecegini bu ayiriyor" diyor; turun iki uyesinin de uretimde okuyani
   yok. Bu, "pin degisti docstring degismedi" sinifinin bir baska ornegi.
4. **`EncoderProbeState.NotWorking` bilerek `borc` birakildi.** Bicimi `varsayilan-kol` ve
   digerleri gibi "olumsuz kol" diye gecistirilebilirdi; T137 tam bu turde uc degerli
   cevabin ikiye dusmesini KRITIK saydigi icin gecistirilmedi.
5. **`BitrateRateControlArgs`** — K5'in kardesi, yukarida.
