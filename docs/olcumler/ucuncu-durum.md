# Ucuncu durum: "olculemedi" cagirana gorunur oldu

T129. Yoklama sonucunun disari acilan yuzu iki durumluydu; ic yapi T94'ten beri uc
durumlu. Bu belge dikisin kapatilmasini ve her kabul kriterinin kanitini tasiyor.

Olculen agac: `T129-ucuncu-durum`, `origin/main` (13a8507) uzerine.

## K1 — Dort kacagin dogrulamasi

Dordu de `origin/main` 13a8507'de kodda goruldu. Satir numaralari o commit'e ait.

| kacak | satir | kodda gorulen |
|---|---|---|
| `RunOptionProbe` zaman asiminda duz `false` | `EncoderCapabilities.cs:199-203` | `if (!process.WaitForExit(ProbeKillMs)) { Kill; return false; }` |
| `WarmEncoderOption` onu kosulsuz onbellege yaziyor | `EncoderCapabilities.cs:53-54` | `var supported = HasEncoder(codec) && RunOptionProbe(...); _encoderOptions[key] = supported;` |
| `Hdr10PixelFormat` zaman asiminda `null`, cagiran "HDR10 yok" okuyor | `EncoderCapabilities.cs:66-68` + `HdrResolver.cs:58` | `if (!timedOut) _hdr10PixelFormats[codec] = result; return result;` → `return Hdr10PixelFormat(codec, availability) is not null;` |
| `ProbeHdr10PixelFormat`'ta `case TimedOut: ... break;` dongu suruyor | `EncoderCapabilities.cs:140-146` | `case ProbeOutcome.Accepted: return (pixelFormat, false);` — p010le zaman asimina ugrayip yuv420p10le kabul edilirse `timedOut` atiliyor ve **yanlis sonuc onbellege yaziliyor** |

Sozlesmenin kendi K5 listesindeki ucu de tuttu:

| kacak | satir | kodda gorulen |
|---|---|---|
| `catch` surecin baslayamamasini "reddedildi" sayiyor | `EncoderCapabilities.cs:130-133` | `catch { return ProbeOutcome.Rejected; }` |
| `RunCapture`'da `ReadToEnd()` sonrasi `WaitForExit(5000)` fiilen etkisiz | `EncoderCapabilities.cs:273-277` | `var output = process.StandardOutput.ReadToEnd(); process.WaitForExit(5000);` |
| `Probe` kilidi ffmpeg suresince tutuyor, `SupportsEncoderOption` tutmuyor | `EncoderCapabilities.cs:79-92` / `:39-40` | `lock (_probed) { ... ProbeEncoder(codec) ... }` |

Ek olarak `HardwareVerdict.cs:48` docstring'i "Yoklamanin kendi zaman asimi 4000 ms"
diyordu; `ProbeKillMs` T94'te 15000 olmustu. Bayat cumle duzeltildi.

**Tutmayan kacak yok.** Hepsi yazildigi gibiydi.

## K2 — Ucuncu durumun bicimi ve reddedilen secenekler

Secilen bicim: **var olan pozisyonel kaydin uzerine `init` alani + turetilmis enum.**

```
public enum EncoderProbeState { Working, NotWorking, Unmeasured }

public sealed record EncoderProbeResult(string Codec, bool Succeeded, long ElapsedMs)
{
    public bool Measured { get; init; } = true;
    public EncoderProbeState State => ...;
    public static EncoderProbeResult Unmeasured(string codec, long elapsedMs) => ...;
}
```

`IEncoderAvailability` ucuncu durumu **varsayilan gerceklestirmeli** bir uye ile aciyor:

```
EncoderProbeState EncoderState(string codec) =>
    WorksAsEncoder(codec) ? EncoderProbeState.Working : EncoderProbeState.NotWorking;
```

Gercekten yoklayan taraf bunu ezer ve **surec dogurmadan** cevap verir:

```
public EncoderProbeState EncoderState(string codec)
{
    lock (_probed)
        if (_probed.TryGetValue(codec, out var cached)) return cached.State;

    return HasEncoder(codec) ? EncoderProbeState.Unmeasured : EncoderProbeState.NotWorking;
}
```

`Hdr10State` ayni kurali izliyor. Ayrim su: `WorksAsEncoder` / `Probe` /
`Hdr10PixelFormat` bilmedigini **ogrenmek icin** ffmpeg cagirir; `EncoderState` /
`Hdr10State` yalnizca **zaten bilineni okur**. Bu ikincisi arayuz is parcacigindan
cagrilabilir ve T130'un sorunu tam burasi: `Recalculate` her hesapta canli ffmpeg
doguruyor. `Unmeasured` "bu makinede calismiyor" degil "henuz bakmadik" demek; olcumu
yaptirmak isteyen taraf `Probe`'u arka planda kosturur.

### Neden bu

- `EncoderProbeResult`'in pozisyonel imzasi (`string, bool, long`) korundu. `Succeeded`
  okuyan her cagiran calismaya devam ediyor ve **varsayilani guvenli yon**: olculemeyen
  yoklama `Succeeded == false` dondugu icin farkinda olmayan cagiran yazilima duser
  (K3'un politikasi). Ayrimi isteyen `State` ya da `Measured` okur.
- Varsayilan gerceklestirme, `IEncoderAvailability`'yi uygulayan **on test sahtesini**
  kirmadan uyeyi ekliyor: `PlanCalculatorTests.FakeAvailability`,
  `SpeedModeTests.FakeAvailability`, `HardwareFlagTests.Availability`,
  `HardwareRateControlTests.FixedAvailability`, `HdrArgumentsTests.FakeAvailability`,
  `HdrArgumentsTests.MutatedAvailability`, `PerformanceCheckTests.FakeAvailability`,
  `FfmpegArgumentsTests.OptionAvailability`, `FfmpegArgumentsTests.WarmingAvailability`,
  `ComplexityScanTests.ColdCapabilities`. Hepsi owns disinda; cozum kapsam disi dosyalara
  tek satir bile yazmadan derledi.

### Reddedilenler

| secenek | neden reddedildi |
|---|---|
| `bool Succeeded` → `EncoderProbeState State` (pozisyonel alani degistirmek) | `tests/VidShrink.Tests/HardwareVerdictTests.cs` **owns disinda** ve `new EncoderProbeResult("av1_nvenc", false, 4000)` cagriyor. Tipi degistirmek T129'un dokunamayacagi bir dosyayi kirardi. |
| `bool?` | `!probe.Succeeded` gibi mevcut kullanimlar sessizce anlam degistirir; `null` un "olculemedi" mi "bilinmiyor" mu oldugu tipten okunmaz. Uc durumu adlandirmayan bir tip ucuncu durumu **gorunur** kilmaz. |
| `IEncoderAvailability`'ye uyeyi varsayilansiz eklemek | Dokuz sahteyi kirar; hepsi owns disindaki test dosyalarinda. |
| `HardwareVerdictReason.ProbeUnmeasured` (yeni enum uyesi) | `MainWindow.axaml.cs:676` reason switch'inde `_` dusme kolu var ve yeni uye **kullaniciya "bit hizi tabani" cumlesini** yanlis yazdirirdi. App kapsam disi. Yerine `HardwareVerdict.Measured` eklendi: karar ayni (`ProbeFailed`, hizli mod kapali), ayrim cagirana aciliyor, kullaniciya gorunen cumle degismiyor. Ayri bir cumle gerekiyorsa T0'in karari. |

HDR10 tarafinda `IHdr10EncoderAvailability.Hdr10PixelFormat` `HdrResolver.cs` icinde
yasiyor ve o dosya **T130'un**. Uyeyi orada degistirmek yerine `IEncoderAvailability.cs`e
ayri bir yuz konuldu:

```
public interface IHdr10ProbeAvailability { EncoderProbeState Hdr10State(string codec); }
```

`EncoderCapabilities` ikisini de uyguluyor. Tuketen taraf (`HdrResolver`) T130/T128'in;
T129 yalniz **sunuyor**.

## K3 — Asimetri kodda gorunur

Politika degistirilmedi: **olculemediyse yazilima dus.** `HardwareVerdict.Decide` icinde
gerekcesiyle pimlendi (bedel karsilastirmasi ve `EncodeRunner.cs:307-308` referansi
yorumda). Sozlesmenin getirdigi olcum:

- yanlis **donanim**: `EncodeRunner` ffmpeg sifirdan farkli donunce firlatiyor, kodlayici
  duzeyinde kosum ici geri dusme yok → donusum tumden basarisiz, bedel sinirsiz.
- yanlis **yazilim**: 90 sn 1080p60'ta 12 732 / 10 347 ms = **1,23 kat**.

Sinirsiz bedelden kacmak icin sonlu bedel seciliyor. Bu sayilar T123'un olcumu; T129
yeniden olcmedi (bkz. Olculmeyenler).

## K4 — Ucuncu durumu iki olcu tutuyor, mutasyon iki yonlu

Olculer `tests/VidShrink.Tests/EncoderCapabilitiesTests.cs` icinde ve **davranis**
olcuyorlar (sabit karsilastirmiyorlar): her ikisi de yoklamanin **kac kez kostugunu**
sayiyor, yani onbelleklemenin kendisini olcuyor.

| olcu | tuttugu yol |
|---|---|
| `AnUnmeasuredProbeIsNeitherWorkingNorCachedAndFallsBackToSoftware` | olculemedi → `State == Unmeasured`, `Succeeded == false`, karar yazilima duser (`EnableFastMode == false`, `Measured == false`), sonuc **onbellege girmez** (ikinci cagri yeniden yokluyor) |
| `AMeasuredRejectionIsCachedAndStaysDistinctFromUnmeasured` | olculdu ve calismiyor → `State == NotWorking`, `Measured == true`, sonuc **onbellege girer** (ikinci cagri surec dogurmuyor) |

Iki yonlu mutasyon — `ProbeEncoder`'in sonuc esleme satiri degistirildi:

| mutasyon | sonuc |
|---|---|
| A: olculemedi "calismiyor" kovasina dusuruldu (T94 oncesi davranis) | `AnUnmeasuredProbe...` **KIRMIZI**, digeri yesil (1 basarisiz / 15) |
| B: reddedildi "olculemedi" kovasina dusuruldu | `AMeasuredRejection...` **KIRMIZI**, digeri yesil (1 basarisiz / 15) |

Iki durum ayni kovaya dusunce her seferinde tam olarak bir olcu kirildi. Sekiz
mutasyonun sekizi de dalin ucundaki agaca (`5ee8408`) karsi yeniden kosturuldu; sayilar
o kosumlarindir, `EncoderCapabilitiesTests` suiti 15 olcu.

Ucuncu olcu `AnEncoderMissingFromTheListIsMeasuredNotUnmeasured`: ffmpeg kodlayiciyi hic
listelemiyorsa bu **olculmus** bir yokluktur, surec bile dogurulmaz.

Dorduncu olcu `ReadingTheThirdStateNeverSpawnsAProcess`: ucuncu durumun yuzu okunurken
yoklama kosmuyor. Iki yoklama kancasi da `InvalidOperationException` firlatacak sekilde
kuruluyor; `EncoderState` ve `Hdr10State` yine de cevap veriyor.

| mutasyon | sonuc |
|---|---|
| G: `EncoderState` govdesi `Probe(codec).State`e delege edildi | `ReadingTheThirdState...` ve `AnUnmeasuredProbe...` **KIRMIZI** (2 basarisiz / 15) |
| H: `Hdr10State` govdesi `Hdr10Probe`a delege edildi | `ReadingTheThirdState...` **KIRMIZI**, digerleri yesil (1 basarisiz / 15) |

G'de ikinci olcunun de kirilmasi beklenen: delege edilen `EncoderState` yoklamayi bir kez
daha kosturuyor, `AnUnmeasuredProbe...`in sayaci 2 yerine 3 goruyor.

## K5 — Kalan kacaklarin kapanisi

**1. `catch { return ProbeOutcome.Rejected; }` → `Unmeasured`.** Surecin hic
baslayamamasi makine hakkinda bir sey soylemez. Enum uyesi `TimedOut` → `Unmeasured`
olarak yeniden adlandirildi; zaman asimi ve baslatma hatasi ayni kovada, ikisi de
onbellege girmiyor.

**2. `WarmEncoderOption` / `RunOptionProbe`.** `RunOptionProbe` artik `bool` degil
`ProbeOutcome` donduruyor. `Unmeasured` sonuc `false` olarak **donuyor ama yazilmiyor**;
kaynak serbestleyince bir sonraki cagri olcebiliyor. Olculer:
`AnUnmeasuredOptionProbeIsNotCached` (ayni anahtar once olculemiyor, sonra olculup
`true` yaziliyor) ve `AMeasuredOptionRejectionIsCached`.

**3. `Load` / `RunCapture`.** Uc degisiklik:
- `RunCapture` okumayi **asenkron** baslatiyor; `WaitForExit(CaptureKillMs)` artik
  gercekten sinirliyor, asilirsa surec olduruluyor ve firlatiliyor.
- `Load` bos kodlayici kumesini basarili saymiyor (`Encoders.Count == 0` → yuklenmedi).
- `Lazy<>` kaldirildi. `Instance` basarisiz acilisi **kalici tutmuyor**: en fazla
  `ReloadAfterFailureMs` (5000 ms) sonra yeniden deniyor. Gecici bir acilis hatasi artik
  `HasEncoder`i surec omru boyunca yanlis yapmiyor.

**4. `ProbeHdr10PixelFormat` dongu kacagi.** `case ProbeOutcome.Accepted:` artik
`(pixelFormat, timedOut)` donduruyor: daha onceki bir biciminin olculemedigi bir kabul
onbellege yazilmiyor. p010le zaman asimina ugrayip yuv420p10le kabul edilirse sonuc
kullanilir ama muhurlenmez.

HDR yolunun ucuncu durumu uc olcuyle tutuluyor:
`AnHdr10AcceptanceAfterAnUnmeasuredFormatIsNotCached` (dongu kacagi),
`AMeasuredHdr10AcceptanceIsCached` ve
`Hdr10StateSeparatesUnmeasuredFromMeasuredAbsence`. Ikisi de mutasyonla kirildi:

| mutasyon | sonuc |
|---|---|
| E: `case Accepted: return (pixelFormat, false)` (T129 oncesi hali) | `AnHdr10Acceptance...` **KIRMIZI** (1 basarisiz / 15) |
| F: olculemeyen HDR sonucu da onbellege yaziliyor | `Hdr10StateSeparates...` **ve** `AnHdr10Acceptance...` **KIRMIZI** (2 basarisiz / 15) |

Bu kabul **cagirana donuyor** (`Hdr10PixelFormat` bicimi verir) ama onbellege
girmedigi icin `Hdr10State` onu `Working` diye **okuyamaz**: `Hdr10State` yalnizca
muhurlenmis bilgiyi okur, muhurlenmemis kabul `Unmeasured` gorunur. Bilerek boyle: iki
yuzden biri "su an ne kullanabilirim", digeri "neyi kesin biliyorum" sorusuna cevap
veriyor ve ikincisi surec dogurmadigi icin ancak muhurlenmis olani bilebilir.

**Olcusuz kapanis:** 3 numaranin (`Load` / `RunCapture` / `Instance`) uc degisikligi de
**olcuyle tutulmuyor.** `Instance` gercek ffmpeg'i cagiran statik bir tekildir; yeniden
deneme yolunu ffmpeg'i gecici olarak bozmadan kosturacak bir dikis T129'da acilmadi.
Degisiklikler okunarak dogrulandi, davranisla degil. Bu bir borctur.

**Kapatilamayan:** `HdrResolver.cs:58`'in `null`'i "HDR10 yok" okumasi kodda duruyor —
dosya T130'un, T129 dokunamaz. T129 ayrimi `IHdr10ProbeAvailability.Hdr10State` ile
suruyor; tuketmek tuketen tarafin isi. Bu **acik bir borctur**, sessiz birakilmadi.

## K6 — Kilit ffmpeg suresince tutulmuyor

`Probe`, `WarmEncoderOption` ve `Hdr10PixelFormat` artik ayni kalibi kullaniyor: kilit
yalniz sozluge bakarken ve yazarken tutuluyor, surec kilidin **disinda** kosuyor. Yazma
sirasinda ikinci bir kontrol var (`TryGetValue(out var raced)`), yani ayni anda giren
iki yoklamadan ilk yazan kazaniyor ve herkes ayni ornegi goruyor.

Uc olcu:

| olcu | ne gosteriyor |
|---|---|
| `ASlowProbeDoesNotBlockAnotherRead` | 2000 ms suren bir yoklama surerken baska bir kodlayicinin yoklamasi 750 ms'nin altinda donuyor |
| `ASlowOptionProbeDoesNotBlockCachedOptionReads` | ayni sey secenek onbelleginde |
| `ConcurrentProbesAgreeOnOneResult` | ayni kodlayiciya ayni anda giren 16 cagrinin hepsi **ayni ornegi** goruyor; daralan kilit sonucu kararsiz yapmadi |

Kilit mutasyonu (ffmpeg cagrisi yeniden kilidin icine alindi):

| mutasyon | sonuc |
|---|---|
| C: `Probe` kilidi surec boyunca tutuyor (T129 oncesi kalip) | `ASlowProbeDoesNotBlockAnotherRead` **KIRMIZI** (1 basarisiz / 15); okuma 2 s bekledi |
| D: `WarmEncoderOption` kilidi surec boyunca tutuyor | `ASlowOptionProbeDoesNotBlockCachedOptionReads` **KIRMIZI** (1 basarisiz / 15) |

Her mutasyon tam olarak bir olcuyu kirdi; `ConcurrentProbesAgreeOnOneResult` her iki
mutasyonda da yesil kaldi, yani daralan kilidin urettigi tek fark bekleme suresi.

## K7 — CI

Kosum kapisi tam suiti kosuyor, yani CI yesili tam suit yesilidir.

| kosum | commit | sonuc |
|---|---|---|
| `33604326223` | `0df815b` (K4-K6) | Failed 0, Passed 1146, Skipped 106, Total 1252 |
| `33605024087` | `19941e1` (K5/4) | yesil |
| `33605884286` | `5ee8408` (dalin ucu) | Failed 0, Passed 1150, Skipped 106, Total 1256 |

`SplitDragTests` uc kosumun hicbirinde kirmizi cikmadi; T127'nin bildirdigi kirmizi bu
dalda gorulmedi. Toplamin 1252 → 1256 artmasi T129'un ekledigi dort yeni olcudur.

**Yerel tam suit kasitli olarak yarida kesildi.** Makinede yedi kardes ajan es zamanli
`dotnet test` kosturuyordu (47 dotnet sureci); K6'nin zaman butceli yaris olculeri
(`ReaderBudgetMs = 750`) boyle bir yukte kendiliginden kararsizdir ve yesil de kirmizi da
kanit olmazdi. Tam suitin kaniti yalniz CI kosumudur; hedefli suit (`verify` filtresi,
33 olcu) ve `EncoderCapabilitiesTests` (15 olcu) yerelde bosalmis makinede yesil kostu.

## Olculmeyenler

- **Gercek surucusuz makinede kullaniciya gorunen sonuc olculmedi.** Bu degisiklik
  olculemeyen yoklamayi kalici "donanim yok"tan cikardi; surucusu olmayan bir makinede
  kullanicinin gordugu cumlenin ve hizli mod kutusunun davranisinin ne oldugu bu makinede
  denenemedi. Bu makinede NVENC calisiyor.
- **Ucuncu durumun `PickCodec` disindaki cagiranlarda ne degistirdigi olculmedi.**
  `EncoderState` yeni bir uye ve su an **hicbir uretim cagirani yok**; `WorksAsEncoder`
  okuyan mevcut cagiranlar (`PlanCalculator.PickFastCodec:788`, `HdrResolver:57`,
  `FpsDropTests:325`) icin davranis bit bit aynidir — olculemeyen yoklama once de
  `false` donuyordu. Degisen tek sey **onbelleklenmemesi**: ayni kodlayici bir sonraki
  cagrida yeniden yoklaniyor. Bunun UI yeniden hesap yolundaki maliyeti **T130'un
  konusu** ve T129 olcmedi.
- **K3'un asimetri sayilari T123'un olcumu.** T129 ne 1,23 kati ne de yoklama surelerini
  yeniden olctu; politikayi degistirmedigi icin sozlesme yeni olcum istemiyor.
- **K5/3'un uc degisikligi olculmedi.** `RunCapture`'in yeni zaman asimi, `Load`'un bos
  kume reddi ve `Instance`'in yeniden deneme yolu okunarak dogrulandi; hicbiri bir olcuyle
  tutulmuyor. Ucu de gercek ffmpeg'i cagiran statik bir tekilin icinde ve T129 oraya dikis
  acmadi.
- **`ReloadAfterFailureMs = 5000` bir olcuye dayanmiyor.** Gecici acilis hatasinin ne
  kadar surdugu olculmedi; sayi "kalici olmasin" kuralini saglayan keyfi bir alt sinir.
  Yeniden deneme maliyeti ffmpeg bulunamadiginda uc hizli firlatmadir.
- **Arka plan yoklamasini kimin kosturacagi T129'da yok.** `EncoderState` artik surec
  dogurmadigi icin hic yoklanmamis bir kodlayici surekli `Unmeasured` doner; onbellegi
  isitacak cagriyi (`Probe`'u arka planda kosturmak) T129 yazmadi, sunulan yuzun boyle
  bir cagiranla gercek makinede nasil davrandigi olculmedi. Isitma yolu T130'un.
- **Yerel tam suit bu agacta tamamlanmadi.** Yukaridaki gerekceyle kesildi; tam suit
  yesili CI'dan okunuyor. Yerel ve CI ortamlari (ffmpeg surumu ayni, NVENC yalniz yerelde)
  farkli oldugu icin bu bire bir ayni kanit degildir.
- **Yaris olculeri zamana bakiyor.** `ReaderBudgetMs = 750` ile `SlowProbeMs = 2000`
  arasindaki pay yuklu bir CI kosumunda daralabilir; olcu kararsizlasirsa payi buyutmek
  degil, dikisi baska turlu olcmek gerekir.
