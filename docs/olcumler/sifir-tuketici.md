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
`CalibrationProbe.cs` `ComplexityProbe`i cagiriyor, `VidShrink.Ffmpeg/QualityMeter.cs:294` `sceneMap`i
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
| `alan-okumasi` | `public static readonly` **alan** uyesinin `src/**` altindaki **her** gorunumu — kosulsuz |

Bunlarin hicbirine uymayan her gorunum **uretim**dir. **Bu cumle yalnizca enum uyeleri
icin gecerlidir.** Alan uyelerinde konum hic bakilmiyor: `MemberScan.Classify`
(`OluUyeTests.cs:189-190`) `Kind == "alan"` gorunce dogrudan `(true, "alan-okumasi")`
donuyor, dilbilgisi kurallari hic calismiyor.

Bunun olcuye etkisi olculebilir bir sinirdir: bir alan uyesi `src/**` altinda **bir kez
bile** goruluyorsa tuketilmis sayilir, dolayisiyla **hicbir alan uyesi `varsayilan-kol`
bicimine dusemez.** Bugun pimdeki bes alan uyesinin hepsi ya `hic-gorunmeyen`
(`LauncherUpdate.CommitWindow`, `MacUpdate.DownloadTimeout`) ya `yalniz-disarida`
(`FfmpegArguments.SceneMapRuleOfRecord`, `UpdateCheck.ManifestTimeout`) — yani uretimde
sifir gorunum. Bir alan uretimde yalnizca **yazilarak** kullaniliyorsa olcu onu tuketilmis
sanar ve **bulmaz**; bu bir yanlis pozitif degil, bir **yanlis negatif** kaynagidir ve
kalan borca yazildi. Alan uyeleri icin okuma/yazma ayrimi ancak IL'den cagri yeri sayan
bir duzenekle yapilabilir (T148 kalibi).

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
sifir tuketici: 26  hic kullanilmayan: 5
dizgi/yorum icinde kalan gorunum: 20
```

129 uyenin 31'i bulgulu: 26 sifir tuketicili, 5 hic kullanilmayan. Kalan 98 uye
tuketiliyor.

**Saklanan 20 gorunumun hepsi docstring satiri.** `TheStripperHidesOnlyCommentsAndStringLiterals`
bunu pimliyor: saklanan her gorunumun satiri `///` ile baslamak zorunda. Bir gun bir uye
yalniz bir dizgi sabitinin icinde geciyorsa bu test kirmizi olur ve kirpicinin o uyeyi
gizledigi gorulur — 63'un tekrarlamasini engelleyen kol budur.

**"Desen listesi yok" kaniti:** `TheMemberSetComesFromTheAssemblyNotFromThisFile` olcunun
kendi kaynagini okuyup, buldugu 129 uyeden kacinin adinin test dosyasinda hic gecmedigini
sayiyor. Bugun 93 tanesinin adi gecmiyor; kume pim listesinden turemiyor, yansimadan turuyor.

## K3 — Sayi pimlenir

Pim `OluUyeTests.Pinned`: 31 satir, her satirda uye adi, **hesaplanan bicim** ve karar.
Bicim yorum degil, olcunun kendi verisinden cikan bir siniflandirma:

| bicim | anlami | bugun |
|---|---:|---:|
| `varsayilan-kol` | ayni turun baska uyesi okunuyor, bu uye okuma tarafinda hic adlandirilmiyor | 24 |
| `hic-okunmayan-tur` | turun **hicbir** uyesi okunmuyor | 2 |
| `yalniz-disarida` | uretimde sifir gorunum, testlerde/araclarda var | 2 |
| `hic-gorunmeyen` | hicbir yerde gorunmuyor | 3 |

`TheZeroConsumerSetIsThePinnedSet` bulunan kumeyi bu listeyle **tam** karsilastiriyor —
fazla satir da eksik satir da kirmizi.

### Mutasyon izgarasi

Izgara **tur 2'de bastan kosuldu**: pim 27'den 26'ya temellendigi icin tur 1'in mutasyon
kanitlari gecersizdi. Her hucrede once `dotnet build -c Release --no-incremental`, sonra
duz `dotnet test -c Release --filter "OluUyeTests"` kosuldu. **`--no-build` hicbir kolda
kullanilmadi** (tur 1 onu derlemeden hemen sonraki kosumda kullanmisti; tur 2 bunu da
kaldirdi). Derleme hatasi sayisi her kosumda 0, agac `073a43a` (rebase sonrasi dal tepesi).

| # | mutasyon | beklenen | eski kol | yeni kol |
|---|---|---|---|---|
| 1 | `EncoderVendor`e `Rkmpp` uyesi + `CodecModel.Vendor`da `c.Contains("rkmpp")` uretici satiri | kirmizi (sayi 26 → 27) | `Geçti: 11`, `sifir tuketici: 26`, cikis kodu 0 | `Geçti: 10  Başarısız: 1`, `sifir tuketici: 27`, cikis kodu 1 |
| 2 | `QualityArgs`a acik `EncoderVendor.Software => new[] { "-crf", exact }` kolu | kirmizi (sayi 26 → 25) | `Geçti: 11`, `sifir tuketici: 26`, cikis kodu 0 | `Geçti: 10  Başarısız: 1`, `sifir tuketici: 25`, cikis kodu 1 |

**Eski kol (iki mutasyon icin de ayni taban).** Rebase sonrasi, mutasyonsuz agac:

```
$ dotnet build -c Release --no-incremental        → 0 Hata
$ dotnet test -c Release --filter "OluUyeTests" --logger "console;verbosity=detailed"

dosya: 70  uye: 129
sifir tuketici: 26  hic kullanilmayan: 5
uye: 129  bu dosyada adi gecmeyen: 93  pimlenen: 31
mesru: 9  borc: 22

Test Çalıştırması Başarılı.
Toplam test sayısı: 11
     Geçti: 11
cikis kodu: 0
```

**Mutasyon 1 — yeni sifir tuketicili uye.** `Rkmpp` uretiliyor, hicbir yerde okunmuyor.
Kodek adlarindan hicbiri `rkmpp` icermedigi icin uretim davranisi degismiyor; kirmizi
yalniz olcuden geliyor. Yeni kol ham cikti:

```
dosya: 70  uye: 130
sifir tuketici: 27  hic kullanilmayan: 5
uye: 130  bu dosyada adi gecmeyen: 94  pimlenen: 31

EncoderVendor.Rkmpp [enum] SIFIR-TUKETICI uretim=1 tuketim=0 disarida=0 maskeli=0
    U uretim               src/VidShrink.Core/CodecModel.cs:134  if (c.Contains(       )) return EncoderVendor.Rkmpp;

  Başarısız VidShrink.Tests.OluUyeTests.TheZeroConsumerSetIsThePinnedSet [244 ms]
   Assert.Equal() Failure: Collections differ
Expected: [···, "ConversionQualityMode.Bitrate  varsayilan-kol", "EncoderVendor.Software  varsayilan-kol", "FfmpegArguments.SceneMapRuleOfRecord  yalniz-disar"···, "FillPolicy.QualityCeiling  varsayilan-kol", ···]
Actual:   [···, "ConversionQualityMode.Bitrate  varsayilan-kol", "EncoderVendor.Rkmpp  varsayilan-kol", "EncoderVendor.Software  varsayilan-kol", "FfmpegArguments.SceneMapRuleOfRecord  yalniz-disar"···, ···]

Test Çalıştırması Başarısız.
Toplam test sayısı: 11
     Geçti: 10
     Başarısız: 1
cikis kodu: 1
```

Uye sayisinin 129'dan 130'a cikmasi kumeyle pim listesinin ayri sayildiginin de kaniti:
kume yansimadan buyudu, pim listesi 31'de kaldi.

**Mutasyon 2 — var olan uyeye gercek tuketici.** `EncoderVendor.Software` acik bir
`switch` koluna cikinca `switch-kolu` kurali onu tuketici sayiyor ve uye kumeden dusuyor:

```
dosya: 70  uye: 129
sifir tuketici: 25  hic kullanilmayan: 5
uye: 129  bu dosyada adi gecmeyen: 93  pimlenen: 31

  Başarısız VidShrink.Tests.OluUyeTests.TheZeroConsumerSetIsThePinnedSet [215 ms]
   Assert.Equal() Failure: Collections differ
Expected: [···, "ConversionQualityMode.Bitrate  varsayilan-kol", "EncoderVendor.Software  varsayilan-kol", "FfmpegArguments.SceneMapRuleOfRecord  yalniz-disar"···, "FillPolicy.QualityCeiling  varsayilan-kol", ···]
Actual:   [···, "ConversionQualityMode.Bitrate  varsayilan-kol", "FfmpegArguments.SceneMapRuleOfRecord  yalniz-disar"···, "FillPolicy.QualityCeiling  varsayilan-kol", "HardwareVerdictReason.BitrateFloorTooHigh  varsayi"···, ···]

Test Çalıştırması Başarısız.
Toplam test sayısı: 11
     Geçti: 10
     Başarısız: 1
cikis kodu: 1
```

**Geri alma kaniti.** Iki mutasyon da `CodecModel.cs`'in mutasyon oncesi kopyasindan geri
yuklendi; sonra yeniden derlenip kosuldu:

```
$ git diff src/VidShrink.Core/CodecModel.cs
(cikti bos)
$ dotnet build -c Release --no-incremental        → 0 Hata
$ dotnet test -c Release --filter "OluUyeTests" --logger "console;verbosity=detailed"

dosya: 70  uye: 129
sifir tuketici: 26  hic kullanilmayan: 5
uye: 129  bu dosyada adi gecmeyen: 93  pimlenen: 31

Test Çalıştırması Başarılı.
Toplam test sayısı: 11
     Geçti: 11
cikis kodu: 0
```

**Ikinci mutasyonun kalici ornegi zaten var.** K5'in patlayan kolu
`EncoderVendor.VideoToolbox`u gercekten tuketici konumuna tasidi ve o agacta sayi bir
dustu (28 → 27; ikisi de K5-oncesi/K5-sonrasi olculmus, rebase oncesi degerlerdir —
bugunku taban 26). Mutasyon 2 ile ayni bicim, ama gecici degil. Bkz. K5.

## K4 — Yedi olayin karari ve beyaz liste

Beyaz liste pimin kendisidir: her satir `mesru` ya da `borc` tasir ve **gerekcesiz satir
yoktur** — `EveryPinnedFindingCarriesAReason` her satirin en az 60 karakterlik bir gerekce
cumlesi tasidigini ve kararin taninan iki degerden biri oldugunu dogruluyor.

- **`mesru`** — uyenin okuma tarafinda adlandirilmamasi kasitli, gerekcesi kodda ya da
  bir olcumde yazili. Bugun 9 satir.
- **`borc`** — T150 bu uyeyi **siniflamadi**. Mesru oldugu gosterilmedi, kaza oldugu da
  gosterilmedi. Bugun 22 satir. Bu satirlar "gerekcesiz" degil; gerekce yerine **neyin
  olculmedigi** yaziyor. Dusurmek uretim davranisini degistirdigi ve cogu bu sozlesmenin
  `owns` listesi disinda oldugu icin karar ayri bir sozlesmenin isi.

Yedi olayin karari:

| # | Olay | Karar | Gerekce |
|---|---|---|---|
| 1 | T137 `ProbeOutcome.Unmeasured` | **kaza**, kapandi | Sozlesme KRITIK sayip duzeltti: `WorksAsEncoder` artik ucuncu cevabi yutmuyor (`EncoderCapabilities.cs:96`). |
| 2 | T140 turbo ilk gecis | **mesru** | Ozellik kuruldu, varsayilan olcume birakildi; bugun `FfmpegArguments.cs` uretim tuketicisi. |
| 3 | T142 K3 `PlanWindows` | **kaza**, kapandi | `CalibrationProbe` sabit kopyasini birakip duzenegi cagiriyor. |
| 4 | T143 (1) `WorstScene(map)` | **kaza**, kapandi | Uretim cagrisi artik `sceneMap`i tasiyor (`VidShrink.Ffmpeg/QualityMeter.cs:294`). |
| 5 | T143 (2) `SceneWindowSeconds` | **kaza**, kapandi | Deger kosullu hale getirildi. |
| 6 | T144 `FfmpegRun.StandardError` | **kaza**, kapandi | Basari karari artik metni okuyor (`FfmpegRunner.cs:138`). |
| 7 | T149 `EncoderVendor.VideoToolbox` | **mesru** | `CodecModel.cs:142-144` docstring'i: donanim kapisinin arkasindaki hicbir sabit VideoToolbox'ta olculmedi, kapiyi acmak bir olcumdur. Kapi acilmadi. |

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

CI kosumu **`33688802430`** — `completed success`, commit `fd46868`
(dalin kod tasiyan son commit'i; `.claude/**`, `docs/**` ve `*.md` degisiklikleri
is akisinin `paths-ignore` listesinde, kosum acmiyorlar). Kosum kapisi adimi
`-MinimumTotal 1134 -MaximumSkipped 30` esigiyle gecti.

**Tur 1'in tam suit beyani yanlisti ve geri cekildi.** Tur 1 "guncel `main`
birlestirilmis agacta … Toplam: 1503" yazmisti; o kosum birlesmemis agacta yapilmisti.
1503 dalin **tek basina** toplamidir. Gercek birlesik toplam K9'da, asagida.

## Kalan borc

1. **Alan uyelerinde okuma/yazma ayrilmiyor.** `alan-okumasi` kurali kosulsuz tuketici
   sayiyor; uretimde yalnizca yazilan bir `public static readonly` alan olcunun gozunden
   kacar. Ayrimi ancak IL'den cagri yeri sayan bir duzenek verir.
2. **Cagri yeri dilimi olculmedi.** Yedi olayin altisi metot / asiri yukleme / ornek alani;
   K2 duzenegi tur uyesi sayiyor. "Uretimde cagirani yok" bagintisi icin ayri bir olcu
   gerekiyor; kalip depoda var (T148 derlenmis IL'den cagri yeri sayiyor).
3. **22 satir siniflanmadi.** Pimin `borc` satirlari. En kalabaligi `ShareFailure`:
   on bir uyeden sekizi uretiliyor, hicbiri ayrilmiyor; arayuz yalniz `Cancelled`, `None`,
   `TokenExpired` ve `Unknown` soruyor.
4. **`ArchitectureOutcome` docstring'i ile kodu ayrisiyor.** `UpdateCheck.cs:34`
   "Kullaniciya ne soylenecegini bu ayiriyor" diyor; turun iki uyesinin de uretimde okuyani
   yok. Bu, "pin degisti docstring degismedi" sinifinin bir baska ornegi.
5. **`EncoderProbeState.NotWorking` borcu kapandi, olcuyle.** Tur 1'de `varsayilan-kol`
   bicimiyle `borc` satiriydi. T139 uretimde bir tuketici kol acti
   (`PerformanceProbe.cs:97`, `!= EncoderProbeState.NotWorking`), uye artik sifir tuketicili
   degil ve pimden dusuruldu. Borc listesinde yeri yok; T137'nin uc degerli cevap kaygisini
   olcen kol artik uretimde.
6. **`BitrateRateControlArgs`** — K5'in kardesi, yukarida.

## K7 — Pim birlesik agacta yeniden temellendi (tur 2)

Tur 1'in pimi dalin kendi agacinda yesildi, guncel `origin/main` ile birlesince kirmiziydi.
Rebase sonrasi ilk kosum, hicbir sey degistirmeden:

```
$ git rebase origin/main
$ dotnet build -c Release --no-incremental
$ dotnet test -c Release --filter "OluUyeTests"

Assert.Equal() Failure: Collections differ
Expected: [···, "ConversionQualityMode.Bitrate  varsayilan-kol", "EncoderProbeState.NotWorking  varsayilan-kol", "EncoderVendor.Software  varsayilan-kol", ···]
Actual:   [···, "ConversionQualityMode.Bitrate  varsayilan-kol", "EncoderVendor.Software  varsayilan-kol", ···]
   at VidShrink.Tests.OluUyeTests.TheZeroConsumerSetIsThePinnedSet() ... OluUyeTests.cs:line 456

dosya: 70  uye: 129
sifir tuketici: 26  hic kullanilmayan: 5

Basarisiz! - Basarisiz: 1, Basarili: 10, Atlanan: 0, Toplam: 11
cikis kodu: 1
```

**Sebep, satiriyla.** `EncoderProbeState.NotWorking` artik tuketiliyor:

```
EncoderProbeState.NotWorking [enum] tuketiliyor uretim=8 tuketim=1 disarida=18 maskeli=1
    T esitlik-sag  src/VidShrink.Ffmpeg/PerformanceProbe.cs:97  if (availability.KnownState(candidate) != EncoderProbeState.NotWorking) return candidate;
```

**Satir nereden geldi — olculdu, aktarilmadi.** Dalin ayrildigi taban `8b8d385`;
o agacta `PerformanceProbe.cs` icinde `NotWorking` **hic gecmiyor**
(`git show 8b8d385:src/VidShrink.Ffmpeg/PerformanceProbe.cs | grep NotWorking` bos doner).
Satiri T139 yazdi: `2caff96` `EncoderState(candidate) != EncoderProbeState.NotWorking`
olarak ekledi, `cf009f0` cagriyi `KnownState` olarak yeniden adlandirdi. `main`e
`5df0a98` birlesmesiyle geldi ve o birlesmenin ilk ebeveyni tam olarak dalin tabani
`8b8d385`. Yani "main'in `5df0a98` ile getirdigi satir" ifadesi birlesme dogruluk
duzeyinde dogru; satirin yazari `2caff96`.

**Karar: pim 26'ya temellendi, uye dusurulmedi.** `EncoderProbeState.NotWorking` artik
sifir uretim tuketicisi degil — sekiz yerde uretiliyor, bir yerde tuketiliyor. Pim
listesinden cikarildi (32 → 31 satir), sayi 27 → 26. Uyenin kendisine dokunulmadi;
`PerformanceProbe.cs` bu sozlesmenin `owns` listesinde degil ve duzeltilecek bir sey de
yok — kol dogru kol.

Gerekce koda da yazildi: `OluUyeTests.Pinned` docstring'i hangi commit'in hangi satiri
getirdigini tasiyor, boylece bir sonraki tur pimi "bayat mi, dogru mu" diye ayirt edebilir.

Yeniden temellendikten sonra:

```
dosya: 70  uye: 129
sifir tuketici: 26  hic kullanilmayan: 5
uye: 129  bu dosyada adi gecmeyen: 93  pimlenen: 31
mesru: 9  borc: 22

Test Calistirmasi Basarili.
Toplam test sayisi: 11
     Gecti: 11
```

## K8 — Eskiyen sayilar cikarildi

Pim 27'den 26'ya temellendi. Rapordaki her sayi tarandi; iddia olarak duran 27'ler
cikarildi, geriye yalniz **tarihi anlatan** ya da **mutasyonun urettigi** 27'ler kaldi.

```
$ grep -n "27" docs/olcumler/sifir-tuketici.md
176:Izgara **tur 2'de bastan kosuldu**: pim 27'den 26'ya temellendigi icin ...
184:| 1 | ... | kirmizi (sayi 26 → 27) | ... | `sifir tuketici: 27`, cikis kodu 1 |
210:sifir tuketici: 27  hic kullanilmayan: 5
272:dustu (28 → 27; ikisi de K5-oncesi/K5-sonrasi olculmus, rebase oncesi degerlerdir ...
429:listesinden cikarildi (32 → 31 satir), sayi 27 → 26. ...
```

Bes eslesmenin hicbiri "bugunku sayi 27" demiyor:

| satir | ne diyor | neden mesru |
|---|---|---|
| 176 | pimin **eski** degeri | Izgaranin neden bastan kosuldugunu anlatiyor; cumlenin kendisi 26'yi soyluyor. |
| 184 | mutasyon 1'in **beklentisi** ve yeni kolun ham sayisi | Mutasyon sayiyi 26'dan 27'ye cikariyor; 27 mutasyonlu agacin degeri. |
| 210 | mutasyon 1'in **ham ciktisi** | Kosumdan birebir kopya. Degistirilirse kanit olmaktan cikar. |
| 272 | K5'in kendi olcumu (28 → 27) | Rebase oncesi agacin degerleri; ayni cumle "bugunku taban 26" diyerek isaretliyor. |
| 429 | K7'nin karar cumlesi | Degisimin kendisini yaziyor: 27 → 26. |

Ayni tarama iki eskiyen sayi daha buldu ve ikisi de duzeltildi: `mesru`/`borc` dagilimi
"9 + 23" yaziyordu, olcunun bugunku ciktisi `mesru: 9  borc: 22` (pim 32'den 31 satira
indi). Mutasyon izgarasinin dort hucresi tur 1'in `Basarisiz: 1, Basarili: 10` ozetini
tasiyordu; hepsi bu turun kendi kosumlarindan yeniden yazildi (K10).

## K9 — Tam suit, birlesik agacta

Agac: rebase edilmis `T150-sifir-tuketici`, tum kod ve rapor degisiklikleri uygulanmis.

```
$ git rev-parse HEAD
073a43a21b4c5a5985202b92055c2c8068e5d605

$ git diff HEAD...origin/main -- src/ tests/
(cikti bos)

$ git diff --stat HEAD...origin/main -- src/ tests/
(cikti bos)

$ dotnet test -c Release
Basarili!  - Basarisiz:     0, Basarili:  1494, Atlanan:    17, Toplam:  1511, Sure: 12 m 40 s - VidShrink.Tests.dll (net8.0)
cikis kodu: 0
```

Toplam **1511**. Tur 1'in beyan ettigi 1503 dalin tek basina toplamiydi; aradaki 8 test
`main`in getirdigi `EncoderStateConsumptionTests` (T139, `5df0a98`). Suit `073a43a`
tepesinde kosuldu; o commit'ten sonra yalnizca bu rapor duzenlendi, `src/` ve `tests/`
altinda tek bayt degismedi. Kosumda `VIDSHRINK_LAUNCHER_EXE` **kurulmadi**, yani
`UpdaterTests`in canli baslatici bantlari uyandirilmadi — 17 atlanan testin kaynagi bu ve
donanim/ffmpeg isteyen canli bantlar.

**CI kosumu bu turda yok.** Uzaktaki `T150-sifir-tuketici` dali tur 2'nin rebase'inden
once itilmisti; yerel tarih ayristi ve `git push` non-fast-forward reddediyor. Uzak dalin
tarihini yeniden yazmak T0'in kullanicidan onay alacagi bir is oldugu icin bu tur **hic
itilmedi**; K9'un kaniti yukaridaki yerel kosumdur. Tur 1'in CI kosumu (`33688802430`,
`completed success`) rebase oncesi agacindir, bu agacin kaniti degildir.

`git diff HEAD...origin/main` ucuncu nokta bicimidir: `main`in ortak atadan sonra
tasidigi ve bu agacta **olmayan** her sey. Bos olmasi `main`de olup dalda bulunmayan
`src/`/`tests/` degisikligi kalmadigi anlamina gelir — tur 1'in yanlis beyani tam
buradan cikmisti, o zaman diff bos degildi (12 dosya, +286).


## K11 — Iki borc kapandi

### 1. `alan-okumasi` kolu tabloya girdi

Tuketici kurallari tablosunda sekizinci satir yok gorunuyordu: `MemberScan.Classify`
(`OluUyeTests.cs:189-190`) `Kind == "alan"` gorunce dilbilgisi kurallarina hic bakmadan
`(true, "alan-okumasi")` donuyor. Kural tabloya yazildi ve altindaki "bunlarin hicbirine
uymayan her gorunum uretimdir" cumlesi **enum uyeleriyle sinirlandi** — alan uyeleri icin
yanlisti. Ayni yerde olcunun bu yuzden neyi kaciracagi da yazildi: bir alan uyesi `src/**`
altinda bir kez bile gorulurse tuketilmis sayilir, dolayisiyla hicbir alan uyesi
`varsayilan-kol` bicimine dusemez; uretimde yalnizca **yazilan** bir alan olcuden kacar.
Bu bir yanlis negatif kaynagidir, kalan borcun 1. maddesi.

### 2. Uc bayat satir atfi — her biri yeniden olculdu

Denetci uc atfin bayat oldugunu bildirdi. Ucu de bu turda kendim olculdu; **ikisi
denetcinin verdigi sayiyla ayni cikti, ucuncusu cikmadi.**

```
$ grep -n "" src/VidShrink.Core/CodecModel.cs | sed -n '141,145p'
141:    /// are carried to instead of asking whether the vendor is a chip.
142:    /// VideoToolbox is a chip and is still false here: nothing behind this gate has been measured
143:    /// on it. docs/olcumler/videotoolbox.md gives one bitrate per arm on one Apple M1, which is not
144:    /// enough for any of them. Opening this gate for VideoToolbox is a measurement, not an edit.
145:    /// </summary>

$ grep -n "sceneMap" src/VidShrink.Ffmpeg/QualityMeter.cs
294:            return AggregateVmaf(scores, frameRate, referenceStartSeconds ?? 0, sceneMap);

$ grep -n "BelowFloor\|AboveSourceCeiling" src/VidShrink.App/MainWindow.axaml.cs
2494:            QualityTargetBound.BelowFloor => Say("main.quality.below-floor", target, reached),
2495:            QualityTargetBound.AboveSourceCeiling => Say("main.quality.above-ceiling", target, reached),

$ grep -n "" src/VidShrink.App/MainWindow.axaml.cs | sed -n '2496p'
2496:            _ => ""
```

| atif | eski | denetcinin verdigi | olculen | yazilan |
|---|---|---|---|---|
| VideoToolbox docstring'i (`CodecModel.cs`) | 142-146 | 142-144 | 142-144 | 142-144 |
| `sceneMap` tasimasi (`QualityMeter.cs`) | 295 | 294 | 294 | 294 |
| `QualityTargetBound` kollari (`MainWindow.axaml.cs`) | 2480-2482 | 2481-2483 | **2494-2496** | 2494-2496 |

Ucuncusunde denetcinin sayisi da bayatti: `BelowFloor` 2494, `AboveSourceCeiling` 2495,
`_ => ""` 2496. `OluUyeTests.cs`'deki `QualityTargetBound.Reached` gerekcesi olculen
sayiyla yazildi, denetcinin sayisiyla degil.
