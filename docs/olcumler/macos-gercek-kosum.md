# macOS'ta gerçek koşum — 18 değil 20 atlanan, 10 kırmızı

İş 1'in raporu. Süit ilk kez gerçek bir Apple Silicon Mac'te koştu.

## Düzenek

| | |
|---|---|
| Makine | Apple M1, 8 çekirdek, 8 GB RAM, arm64 |
| İşletim sistemi | macOS 26.6.2 (25G83) |
| .NET SDK | 8.0.424 |
| ffmpeg | 9.0.1 (Homebrew `ffmpeg 9.0.1_1`), `/opt/homebrew/bin/ffmpeg` |
| libvmaf | 3.2.0 (Homebrew) |
| Dal / taban | `serkan/macos-olcum`, `53bbc16` (`origin/main`) |

Bu ffmpeg derlemesinin etkin bayrakları — raporun yarısı buradan çıkıyor:

```
ffmpeg -version | tr ' ' '\n' | grep -E "^--enable"
```

```
--enable-audiotoolbox --enable-ffplay --enable-gpl --enable-libdav1d
--enable-libmp3lame --enable-libopus --enable-libsvtav1 --enable-libvmaf
--enable-libvpx --enable-libx264 --enable-libx265 --enable-neon
--enable-openssl --enable-pthreads --enable-shared --enable-version3
--enable-videotoolbox
```

`--enable-libzimg` **yok**. Homebrew'un bugünkü `ffmpeg` formülü zimg'e bağımlı değil:

```
brew deps ffmpeg
# ca-certificates dav1d lame libvmaf libvpx mpg123 openssl@3 opus
# sdl2-compat sdl3 svt-av1 x264 x265 xz
```

Makinede zscale taşıyan başka bir ffmpeg de yok:

```
for f in $(which -a ffmpeg); do printf "%s: " "$f"; "$f" -hide_banner -filters | grep -c " zscale "; done
# /opt/homebrew/bin/ffmpeg: 0
```

## K1 — Tam süit çıktısı

```
dotnet test -c Release --no-build \
  --logger "trx;LogFileName=taban.trx" --results-directory .calisma/gunluk/taban
```

Ham sonuç satırı:

```
Failed!  - Failed:    10, Passed:  1306, Skipped:    20, Total:  1336, Duration: 19 m 10 s - VidShrink.Tests.dll (net8.0)
```

**Paketin verdiği "18 atlanan" bu ağaçta geçerli değil.** `macos-guncelleme.md:346`
964 ölçüde 18 atlanan yazıyor; `origin/main`de süit 1336 ölçü ve **20** atlıyor.
Aşağıdaki iki bölümün sayıları tek tek sayılmıştır, paketten alınmamıştır.

## K1 — Atlananlar: 20, kapısına göre ayrılmış

Sayım `taban.trx` içindeki `outcome="NotExecuted"` kayıtlarından, atlama gerekçesi
metniyle birlikte. Beş grup, toplam 12 + 2 + 3 + 2 + 1 = **20**.

### `VIDSHRINK_LIVE_SOURCE` verilmedi — 12 ölçü

1. `CalibrationProbeTests.LiveEncodeTimeMatchesTheMeasuredEstimate`
2. `CalibrationProbeTests.LiveFastModeLandsInsideTheBandOnTheFirstAttempt`
3. `CalibrationProbeTests.LiveTimeSurvivesTheTargetsThatKeepThePlanShape`
4. `ExtremeCompressionTests.LiveExtremeTargetsProduceAPlayablePicture`
5. `ExtremeCompressionTests.LiveProbeMeasuresMotionWithinItsTimeBudget`
6. `ExtremeCompressionTests.LiveQualityCurveShowsWhereTheCodecStopsCarryingThePicture`
7. `FillBandTests.LiveFillTargetRunStaysInsideTheBand`
8. `HardwareFlagTests.LiveFastRunDoesNotSpendEveryAttempt`
9. `HardwareRateControlTests.LiveFastTargetsLandInsideTheBandOnTheFirstAttempt`
10. `HardwareRateControlTests.LiveProcessorTargetsStillLandInsideTheBandOnTheFirstAttempt`
11. `PlaybackFrameSourceTests.Canli_kaynak_iki_paneli_besliyor`
12. `PlaybackFrameSourceTests.Duraklatma_sureci_oldurmez`

**`macos-paket.md:75` bu grubu 9 sayıyor ve `HardwareRateControlTests` ile
`HardwareFlagTests`i `VIDSHRINK_LIVE_PROBE` grubuna koyuyor. Kodda öyle değil:**
`HardwareRateControlTests.cs:440`, `HardwareRateControlTests.cs:449` ve
`HardwareFlagTests.cs:179` `[LiveSourceTheory]` taşıyor, yani kapıları
`VIDSHRINK_LIVE_SOURCE`. Doğrusu 12 + 2, 9 + 5 değil.

### `VIDSHRINK_LIVE_PROBE` verilmedi — 2 ölçü

1. `HardwareVerdictTests.LiveProbeDecidesOnThisMachine`
2. `HardwareVerdictTests.TheFirstLayoutDoesNotWaitForTheProbe`

### `VIDSHRINK_LAUNCHER_EXE` bir dosyayı göstermiyor — 3 ölçü

1. `UpdaterTests.EveryLaunchChecksAndStaysWithinTheTimeout`
2. `UpdaterTests.SwitchedOffLauncherMakesNoNetworkRequestAtAll`
3. `UpdaterTests.TheIncomingBinaryRenamesItselfOntoTheTargetName`

Üçü de kurulu bir Windows `VidShrink.exe` başlatıcısı istiyor. macOS'ta böyle bir
dosya olamaz; bu üç ölçü bu makinede **hiçbir koşulda** koşamaz.

### Bu ffmpeg derlemesinde `zscale`/`tonemap` yok — 2 ölçü

1. `FrameGrabberTests.Hdr_kaynak_sdr_ciktiyla_eslesince_ton_eslenir_ve_bildirilir`
2. `QualityMeterTests.TonemappedReferenceSeparatesTwoSdrQualities`

`macos-paket.md:78` bu grubu **1** sayıyor. Mac'te 2: Windows koşucusundaki
GyanD full build `zscale` taşıdığı için orada ikincisi hiç atlanmıyor.

### `h264_nvenc` bu ffmpeg derlemesinde yok — 1 ölçü

1. `PerformanceCheckTests.DonanimKodlayiciIslemciZamaniniOlculebilirYaziyorMu`

Windows'ta atlanmayan, **yalnız Mac'te beliren** atlama. Apple Silicon'da NVENC
olamaz; bu ölçü bu makinede hiçbir koşulda koşamaz.

## K2 — Windows'ta erken dönüp geçen, Mac'te gerçekten koşan ölçüler: 7

Sayım `tests/VidShrink.Tests` altında platform kapısı arayarak yapıldı:

```
grep -rn "if (!OperatingSystem.IsMacOS()) return\|if (OperatingSystem.IsWindows()) return" tests/VidShrink.Tests/*.cs
```

| # | Ölçü | Kapı |
|---|---|---|
| 1 | `MacOsStartupTests.MacOsFindsFfmpegOutsideThePath` | `MacOsStartupTests.cs:37` |
| 2 | `MacOsBundleTests.TheBundleCarriesTheVersionTheTreeDeclares` | `MacOsBundleTests.cs:62` |
| 3 | `MacOsBundleTests.TheBundleRenamesAnOldReleaseLauncher` | `MacOsBundleTests.cs:97` |
| 4 | `MacOsBundleTests.UninstallLeavesNothingBehind` | `MacOsBundleTests.cs:127` |
| 5 | `MacUpdateTests.ABrokenSignatureStopsTheSwap` | `MacUpdateTests.cs:116` |
| 6 | `MacUpdateTests.AVerifiedBundleSwapsIntoPlace` | `MacUpdateTests.cs:150` |
| 7 | `MacUpdateTests.PreparingFromAReleaseLeavesASignedBundleBesideTheInstalledOne` | `MacUpdateTests.cs:183` |

Yedisi de bu Mac'te koştu ve **geçti**. Üç sınıfın tamamı:

```
dotnet test -c Release --no-build \
  --filter "FullyQualifiedName~MacOsBundleTests|FullyQualifiedName~MacUpdateTests|FullyQualifiedName~MacOsStartupTests"
# Passed!  - Failed: 0, Passed: 16, Skipped: 0, Total: 16, Duration: 774 ms
```

16 = `MacOsStartupTests` 6 + `MacOsBundleTests` 4 + `MacUpdateTests` 6; üç dosyada
`[Theory]` yok, `[Fact]` sayıları `grep -c '^\s*\[Fact\]'` ile sayıldı.

1 numaralı ölçünün ikinci bir kapısı daha var (`MacOsStartupTests.cs:42`,
ffmpeg `ToolLocator.MacToolDirectories` altında değilse yine sessizce dönüyor).
Bu makinede boş geçmiyor: ffmpeg `/opt/homebrew/bin/ffmpeg`, o dizin de
`ToolLocator.cs:7`deki listenin ilk elemanı.

### Aynı kusurun ters yönü: Mac'te erken dönüp geçen 12 ölçü

Paket Windows'un macOS hakkında hiçbir şey söylemediğini yazıyor. Simetriği de
doğru ve bugüne kadar yazılmamış — bu Mac koşumu Windows hakkında hiçbir şey
söylemiyor:

```
grep -rn "if (OperatingSystem.IsMacOS()) return\|if (!OperatingSystem.IsWindows()) return" tests/VidShrink.Tests/*.cs
```

| # | Ölçü | Kapı |
|---|---|---|
| 1 | `MacOsStartupTests.TheMacFallbackIsOffOutsideMacOs` | `MacOsStartupTests.cs:60` |
| 2 | `UpdaterTests.TheDeletionStepWaitsOutATransientLock` | `UpdaterTests.cs:1118` |
| 3 | `UpdaterTests.TheDeletionStepGivesUpWithAMessageThatSaysWhatHappened` | `UpdaterTests.cs:1156` |
| 4 | `UpdaterTests.AStuckIncomingLauncherDoesNotCancelTheApplicationUpdate` | `UpdaterTests.cs:1261` |
| 5 | `ShellMenuTests.Written_extensions_are_the_application_list` | `ShellMenuTests.cs:157` |
| 6 | `ShellMenuTests.Every_command_calls_the_installed_launcher_with_the_path` | `ShellMenuTests.cs:169` |
| 7 | `ShellMenuTests.Remove_switch_leaves_no_entry` | `ShellMenuTests.cs:186` |
| 8 | `ShellMenuTests.Skip_shortcuts_writes_nothing` | `ShellMenuTests.cs:199` |
| 9 | `ShellMenuTests.Second_run_neither_fails_nor_duplicates` | `ShellMenuTests.cs:209` |
| 10 | `ShellMenuTests.Forced_language_decides_the_label` | `ShellMenuTests.cs:225` |
| 11 | `ShellMenuTests.Automatic_language_follows_the_system_interface` | `ShellMenuTests.cs:237` |
| 12 | `Windows11ShellMenuTests.Missing_modern_package_files_fall_back_to_a_working_classic_menu` | `Windows11ShellMenuTests.cs:75` |

Yani "yeşil süit" iki platformda da yeşil, ama her biri ötekinin 7–12 ölçüsünü
hiç koşmadan geçiyor.

## K3 — 10 kırmızı: tek kök neden

Onunun da kapanış satırı aynı:

```
System.InvalidOperationException : Quality measurement requires the zscale filter
for explicit color normalization.
```

Fırlatan yer `src/VidShrink.Ffmpeg/QualityMeter.cs:399`:

```csharp
if (!EncoderCapabilities.Instance.HasFilter("zscale"))
    throw new InvalidOperationException(
        "Quality measurement requires the zscale filter for explicit color normalization.");
```

### Tek başına yeniden koşum

Yük yapıntısı olasılığı elendi: onu da **tek başına**, kendi süreçlerinde
yeniden koştum.

```
for t in <asagidaki on ad>; do
  dotnet test -c Release --no-build --filter "FullyQualifiedName~QualityMeterTests.$t"
done
```

| # | Ölçü | Tek başına sonuç |
|---|---|---|
| 1 | `QualityMeterTests.Bt709MetadataOnlyRemuxMatchesTheIdenticalCopyScore` | Failed: 1, Passed: 0 |
| 2 | `QualityMeterTests.HeavilyDegradedCopyScoresClearlyLowerThanTheOriginal` | Failed: 1, Passed: 0 |
| 3 | `QualityMeterTests.IdenticalClipReportsTheModelCeilingInsteadOfAForcedHundred` | Failed: 1, Passed: 0 |
| 4 | `QualityMeterTests.OneFrameOfSlipIsWorthTensOfVmafPointsOnThisFixture` | Failed: 1, Passed: 0 |
| 5 | `QualityMeterTests.ReferenceAndSampleMayUseDifferentWindowOffsets` | Failed: 1, Passed: 0 |
| 6 | `QualityMeterTests.ShiftedSourceIsReportedNotSilentlyRepaired` | Failed: 1, Passed: 0 |
| 7 | `QualityMeterTests.SubFrameTimestampSlipDoesNotCostTheScoreAWholeFrame` | Failed: 1, Passed: 0 |
| 8 | `QualityMeterTests.TwoNearLosslessRivalsKeepTheirOrderAboveTheCeilingBand` | Failed: 1, Passed: 0 |
| 9 | `QualityMeterTests.UntaggedSourceAgainstANonBt709TagIsRefusedInsteadOfAssumed` | Failed: 1, Passed: 0 |
| 10 | `QualityMeterTests.WorstSceneFindsTheDamagedSectionTheMeanHides` | Failed: 1, Passed: 0 |

Onunda da aynı istisna. Yük yapıntısı değil, ortam eksiği.

### Kök neden

`QualityMeter` renk uzayını **açıkça** normalize etmek için `zscale` şart koşuyor
(`QualityMeter.cs:478` filtreyi kuruyor, `:49` ölçerin kullanılabilirliğini ona
bağlıyor). Bu bilinçli bir karar: `QualityMeterTests`in kendisi etiketi farklı iki
klibi karşılaştırmanın sessizce yanlış puan verdiğini ölçüyor.

Homebrew'un `ffmpeg` formülü `zimg`e bağımlı değil, dolayısıyla **stok bir
Homebrew ffmpeg'i olan her Mac'te bu 10 ölçü kırmızı.** Windows CI'ı bunu hiç
görmüyor: `.github/workflows/ci.yml:44` GyanD full build indiriyor, o `zscale`
taşıyor. Depoda macOS koşan bir CI işi yok.

### Düzeltme

**Düzeltildi — ortam değişikliğiyle, kod değişmedi.** İlk turda "düzeltilmedi,
çünkü ortam kararı T0'ın" yazmıştım ve `_sorun.log`a koymuştum; T0 kurmamı
söyledi. Kurulan:

```
brew tap homebrew-ffmpeg/ffmpeg
brew uninstall --ignore-dependencies ffmpeg
brew install homebrew-ffmpeg/ffmpeg/ffmpeg --with-zimg --with-libvmaf
```

İkinci satır zorunlu: `homebrew/core` ile `homebrew-ffmpeg/ffmpeg` aynı formül
adını taşıdığı için ikisi bir arada duramıyor. `--with-libvmaf` de zorunlu:
tap'in varsayılan derlemesinde `libvmaf` **isteğe bağlı**, yalnız `--with-zimg`
verilseydi ölçer bu kez libvmaf'sız kalırdı.

Kurulum sonrası (`ffmpeg -version` ve `ffmpeg -hide_banner -filters`):

| Ne | Önce | Sonra |
|---|---|---|
| ffmpeg | 9.0.1 (`homebrew/core`, `ffmpeg 9.0.1_1`) | 9.0.1 (`homebrew-ffmpeg/ffmpeg`) |
| `--enable-libzimg` | yok | var |
| `--enable-libvmaf` | var | var |
| `zscale` filtresi | 0 satır | `.S zscale V->V` |
| `libvmaf` filtresi | var | var |
| `libx265` / `videotoolbox` | var | var |
| `libvmaf` (brew) | 3.2.0 | 3.2.0 |
| `zimg` (brew) | — | 3.0.6 |

Model dosyası yerinde: `/opt/homebrew/opt/libvmaf/share/libvmaf/model/vmaf_v0.6.1neg.json`.

Bu on ölçünün onu da bu kurulumdan sonra yeşil (aşağıdaki ikinci tam koşumun
başarısız listesinde `QualityMeterTests` yok).

İki not, ikisi de ölçülmüş:

- `EncoderCapabilitiesTests.cs:55` `Assert.True(caps.HasFilter("zscale"))` diyor
  ama o ölçü kırmızı listede değil — çünkü gerçek ffmpeg'i değil dosyadaki sabit
  metni ayrıştırıyor (`EncoderCapabilitiesTests.cs:40`).
- Bu eksik İş 2'nin ölçerini de kesiyordu: `tools/VidShrink.Bench measure` aynı
  `QualityMeter`i çağırıyor (`Program.cs:76`). Kurulumdan sonra o da koşuyor ve
  İş 2'nin bütün VMAF sayıları bu yoldan, yani **açık renk normalizasyonuyla**
  üretildi (`docs/olcumler/videotoolbox.md`). Bench'in ikinci ölçeri
  (`bench shrink` → `VmafNegAsync` → `BenchMeasureFilterGraph.Build`) `zscale`
  istemiyor ama açık renk normalizasyonu da yapmıyor; İş 2'de kullanılmadı.

## Kapıları açınca ne oluyor

### `VIDSHRINK_LIVE_PROBE=1` — 2 ölçü koştu

```
VIDSHRINK_LIVE_PROBE=1 dotnet test -c Release --no-build \
  --filter "FullyQualifiedName~HardwareVerdictTests"
# Passed!  - Failed: 0, Passed: 34, Skipped: 0, Total: 34, Duration: 1 s
```

`LiveProbeDecidesOnThisMachine`in bu makinede yazdıkları:

```
codec=libx265 succeeded=True elapsed=72ms
layout=1574x886@30 requested=1085k usable=0k headroom=0,00x
verdict=NoHardwareEncoder enableFastMode=False
av1_amf listed=False succeeded=False elapsed=0ms verdict=ProbeFailed enableFastMode=False
settings first launch=0,16ms, later launch=0,06ms, rewrote=False
```

İki şey ölçüldü:

- **`auto` bu Mac'te donanım seçmiyor: `codec=libx265`, `verdict=NoHardwareEncoder`.**
  Sebebi ffmpeg'de kodlayıcı olmaması değil — `hevc_videotoolbox` ile
  `h264_videotoolbox` ffmpeg'in listesinde var. Üretim kodunun donanım
  tablolarında VideoToolbox hiç geçmiyor (`PlanCalculator.cs:129`,
  `PlanParser.cs:13`, `CodecModel.cs:99-103`; `grep -rn videotoolbox src/` boş
  dönüyor). Bu, İş 2'nin K4 hükmünün girdisidir.
- **Yoklama süresi 72 ms.** Windows'ta ölçülen 3625–14855 ms
  (`handbrake-acigi.md:246`) yüklü makinede alınmıştı; boş makinede orada da
  saniyenin altındaydı. Bu 72 ms boş bir Mac'in tek ölçümü, dağılım değil —
  dağılım İş 2 K3'e ait.

### `VIDSHRINK_LIVE_SOURCE` — sonradan koşturuldu

Bu bölüm yazıldığında ortak ölçüm havuzu makinede yoktu. Havuz sonradan geldi
ve 12 ölçünün hepsi koştu; sonucu aşağıda "Dördüncü tam koşum"da.

## İkinci tam koşum — zscale kurulduktan sonra

```
dotnet test --logger "trx;LogFileName=suit-zimg.trx" --results-directory .calisma/gunluk
```

```
Failed!  - Failed:    56, Passed:  1262, Skipped:    18, Total:  1336, Duration: 11 m 25 s - VidShrink.Tests.dll (net8.0)
```

Birinci koşumla yan yana:

| | Birinci | İkinci |
|---|---:|---:|
| ffmpeg | stok | zimg'li |
| Yapılandırma | **Release** | **Debug** |
| Toplam | 1336 | 1336 |
| Geçen | 1306 | 1262 |
| Kırmızı | 10 | 56 |
| Atlanan | 20 | 18 |
| Süre | 19 dk 10 sn | 11 dk 25 sn |

**İki koşum arasında iki şey birden değişti**, yalnız ffmpeg değil:
yapılandırma da Release'ten Debug'a geçti. Bu, kırmızı sayılarını doğrudan
kıyaslanamaz kılıyor. Eksik anahtar `Strings.cs:136`da `Debug.Fail` ile
bildiriliyor (`AssertOnMissingKey` kapısının arkasında, `Strings.cs:134`;
varsayılanı `true`, `Strings.cs:25`). `Debug.Fail` `[Conditional("DEBUG")]`
olduğundan aşağıda kök nedeni verilen yerelleştirme anahtarı
kusuru **Release'te hiç patlamaz**: birinci koşum onu görebilecek durumda
değildi. Dolayısıyla "10 → 56" farkının tamamı zimg'e yazılamaz; bu farkın
ne kadarının hangi değişkenden geldiğini bu iki koşum ayıramaz. Aşağıdaki
kök neden çözümlemesi tek başına ikinci koşuma dayanıyor ve o koşumun kendi
içinde tutarlı.

### Atlanan 20 → 18: hangi ikisi düştü

`suit-zimg.trx` içindeki `outcome="NotExecuted"` kayıtlarını tek tek sayıp
birinci koşumun listesiyle karşılaştırdım. Düşen tam olarak "bu ffmpeg
derlemesinde `zscale`/`tonemap` yok" grubunun ikisi:

1. `FrameGrabberTests.Hdr_kaynak_sdr_ciktiyla_eslesince_ton_eslenir_ve_bildirilir`
2. `QualityMeterTests.TonemappedReferenceSeparatesTwoSdrQualities`

İkisi de artık koşuyor ve geçiyor. Kalan 18'in hepsi öteki dört gruptan:
12 (`VIDSHRINK_LIVE_SOURCE`) + 2 (`VIDSHRINK_LIVE_PROBE`) + 3
(`VIDSHRINK_LAUNCHER_EXE`) + 1 (`h264_nvenc` yok) = **18**.

Paketin verdiği "18 atlanan" sayısı bu koşumda tesadüfen tutuyor ama **aynı 18
değil**: paketin kaynağı `macos-guncelleme.md:346`, 964 ölçülük bir süiti
anlatıyor.

### 56 kırmızı: tek kök neden, ve altında gerçek bir kusur

56'sının da istisnası aynı:

```
Microsoft.VisualStudio.TestPlatform.TestHost.DebugAssertException :
Method Debug.Fail failed with 'Localization key 'main.plan.fact.estimate'
is missing in 'en' and in 'en'.'
```

Sınıfa göre dağılım (`suit-zimg.trx`ten sayıldı, 7 sınıf, toplam
36 + 7 + 6 + 3 + 2 + 1 + 1 = **56**):

| Sınıf | Kırmızı |
|---|---:|
| `WindowLayoutTests` | 36 |
| `QualityTargetUiTests` | 7 |
| `LanguageTests` | 6 |
| `ChipTests` | 3 |
| `PlanCalculatorProbeTests` | 2 |
| `SettingsTests` | 1 |
| `HardwareVerdictTests` | 1 |

Dile göre dağılımı da aynı anahtarı gösteriyor: 31 kayıt `'en'`, 24 kayıt `'tr'`,
1 kayıt `'zz'` — 31 + 24 + 1 = 56.

**Tek başına yeşil.** İki tanesini kendi süreçlerinde koşturdum:

```
dotnet test --no-build --filter "FullyQualifiedName~WindowLayoutTests.TheTooltipBubbleFitsTheNarrowestWindow|FullyQualifiedName~LanguageTests.SinirCumlesi_EkrandaTurkce"
# Passed!  - Failed: 0, Passed: 2, Skipped: 0, Total: 2, Duration: 91 ms
```

Yani bu 56 kırmızı **yük/sıra yapıntısı.** Nedeninin sınırı: `RefreshPlanView`in
tek erken dönüşü `MainWindow.axaml.cs:1959`daki `ActivePlan is not { } plan ||
_info is null` kapısı; eksik anahtar 21 satır aşağıda (`:1980`) isteniyor. Tek
başına koşumda istisna atılmadığına göre orada kapı kapalı dönmüş, tam süitte
açılmış. Hangi ölçünün bıraktığı durumun kapıyı açtığını **izole etmedim**;
ölçülen şey iki koşumun kendisi.

**Altındaki kusur yapıntı değil.** `MainWindow.axaml.cs:1980` `Say("main.plan.fact.estimate")`
diyor; yerelleştirme dosyalarında o anahtar **yok**, olan
`main.plan.fact.estimated-size` (`Locales/en/main.json:126`,
`Locales/tr/main.json:126`). `Strings.GetIn` bulamadığı anahtarda önce `en`e
düşüyor, orada da bulamayınca `Debug.Fail` atıyor ve **anahtarın kendisini
döndürüyor** (`Strings.cs:134-139`; `Debug.Fail` `[Conditional("DEBUG")]` olduğu için Release derlemesinde sessizce yalnız anahtar döner). Yani Release'te kullanıcı plan panelinde
"Tahmini boyut" yerine ham `main.plan.fact.estimate` dizgisini görüyor.

Bu `origin/main`de de var: `git show origin/main:src/VidShrink.App/MainWindow.axaml.cs`
1980. satırda aynı anahtarı, `git show origin/main:src/VidShrink.App/Locales/en/main.json`
126. satırda `estimated-size`ı veriyor. Platforma bağlı değil; Windows'ta da aynı
dizge ekrana gelir. Süitin orada yeşil kalması kusurun yokluğunu değil, o kod
yolunun oradaki koşumda erişilemediğini gösteriyor.

**Düzeltildi.** Tek kelimelik değişiklik, çağrı yeri var olan anahtara çevrildi:

```
src/VidShrink.App/MainWindow.axaml.cs:1980
-  AddPlanFact(Say("main.plan.fact.estimate"), ...)
+  AddPlanFact(Say("main.plan.fact.estimated-size"), ...)
```

Yerelleştirme dosyaları değil çağrı yeri düzeltildi, çünkü `estimated-size`
anahtarı iki dilde de doğru metinle zaten duruyor ve başka bir çağıranı yok.

## Üçüncü tam koşum — birinci yerelleştirme düzeltmesinden sonra

İkinci koşumla aynı yapılandırma (Debug), aynı ffmpeg; tek değişen yukarıdaki
`main.plan.fact.estimate` düzeltmesi:

```
dotnet test --logger "trx;LogFileName=suit-duzeltme.trx" --results-directory .calisma/gunluk
```

```
Failed!  - Failed:    37, Passed:  1281, Skipped:    18, Total:  1336, Duration: 20 m 7 s - VidShrink.Tests.dll (net8.0)
```

| | İkinci | Üçüncü |
|---|---:|---:|
| Toplam | 1336 | 1336 |
| Geçen | 1262 | 1281 |
| Kırmızı | 56 | 37 |
| Atlanan | 18 | 18 |
| Süre | 11 dk 25 sn | 20 dk 7 sn |

Süre neredeyse iki katına çıktı. Nedenini ayırmadım: bu makinede duvar saatinin
öncesindeki sürekli yüke göre 2 katına kadar saptığını İş 2'de ölçtüm
(`videotoolbox.md`, "Duvar saati ne kadar güvenilir"), dolayısıyla iki süit
süresini yan yana koyup fark çıkarmak bu makinede güvenli değil.

### Düzeltme işe yaradı, ama altından ikinci bir kusur çıktı

Her iki koşumun `trx`indeki kırmızıların iletisinden eksik anahtar adını çekip
saydım:

```
import xml.etree.ElementTree as ET, re, collections
ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
for etiket, p in (("2. koşum", ".calisma/gunluk/suit-zimg.trx"),
                  ("3. koşum", ".calisma/gunluk/suit-duzeltme.trx")):
    fails = [r for r in ET.parse(p).getroot().findall(".//t:UnitTestResult", ns)
             if r.get("outcome") == "Failed"]
    c = collections.Counter()
    for r in fails:
        m = re.search(r"Localization key '([^']+)' is missing", "".join(r.itertext()))
        c[m.group(1) if m else "BASKA NEDEN"] += 1
    print(f"{etiket}: {len(fails)} kırmızı ->", dict(c))
```

```
2. koşum: 56 kırmızı -> {'main.plan.fact.estimate': 56}
3. koşum: 37 kırmızı -> {'main.quality.target': 37}
```

İki koşumun da kırmızısında **tek bir kök neden** var ve ikisi farklı anahtar.
`BASKA NEDEN` sayacı iki koşumda da sıfır: 56'nın ve 37'nin hepsi eksik
yerelleştirme anahtarı. Düzeltme birinci anahtarı tamamen kaldırdı — 3. koşumda
onu anan tek bir kırmızı yok.

Küme farkı da bunu doğruluyor: 2. koşumun kırmızılarından 20 tanesi yeşile
döndü, 36'sı kırmızı kaldı, 1 tanesi "yeni" göründü
(`PlaybackBaseHeightTests.Bos_panel_de_sahne_de_ayni_tabana_oturur`). Bu
sonuncusu gerçekte yeni değil: 2. koşumda ikinci anahtara **varamadan** birinci
anahtarda patlıyordu, birinci düzelince ikinciye ulaştı. Aynı sebeple 2.
koşumda kırmızı olan
`HardwareVerdictTests.TheSettingsPathOverrideKeepsTheTestOutOfAppData` de
yeşile döndü — onun iletisi de `main.plan.fact.estimate` diyordu.

37 kırmızının sınıf dağılımı:

| Sınıf | Kırmızı |
|---|---:|
| `WindowLayoutTests` | 17 |
| `QualityTargetUiTests` | 7 |
| `LanguageTests` | 6 |
| `ChipTests` | 3 |
| `PlanCalculatorProbeTests` | 2 |
| `PlaybackBaseHeightTests` | 1 |
| `SettingsTests` | 1 |

7 sınıf, 17+7+6+3+2+1+1 = **37**.

### İkinci kusurun kök nedeni: `QualityBody` iki anahtarı da yanlış çağırıyor

Çağrı yeri `MainWindow.axaml.cs:1894-1896`. Yığın izi
`QualityBody` → `RefreshQualityPanels` (`:1858`) → `Recalculate` (`:1792`).

```
AddQualityRow(grid, Say("main.quality.target"), ...);
AddQualityRow(grid, Say("main.quality.predicted"), ...);
AddQualityRow(grid, Say("main.quality.loss"), Say("main.quality.points", ...));
```

`main.quality.target` hiçbir dilde yok. `main.quality.points` de yok — locale'de
duran ad `main.quality.loss-points` (`Locales/en/main.json:70`,
`Locales/tr/main.json:70`, metni `"{0} points"` / `"{0} puan"`). İkincisi 37
kırmızının hiçbirinde görünmüyor çünkü aynı satır dizisinde birinci anahtar
önce patlıyor ve onu gölgeliyor.

### Anahtarları koşumla değil taramayla avladım

Her düzeltmeden sonra 20 dakikalık süit koşup bir sonraki anahtarı beklemek
yerine, kodun çağırdığı bütün anahtar sabitlerini çıkarıp locale kümesinden
çıkardım:

```
import json, re, pathlib
kok = pathlib.Path("src/VidShrink.App")
en = set()
for f in (kok / "Locales" / "en").glob("*.json"):
    en |= set(json.load(open(f)).keys())
pat = re.compile(r'\b(?:Say|Strings\.Get(?:In)?)\(\s*"([^"]+)"')
eksik = {m.group(1) for f in kok.rglob("*.cs") for m in pat.finditer(f.read_text())} - en
print("eksik anahtar:", sorted(eksik) or "yok")
```

Düzeltmeden önce iki anahtar çıktı, ikisi de aynı yerden:

```
kod 158 ayri anahtar cagiriyor; 2 tanesi hicbir locale dosyasinda yok:
  main.quality.points     src/VidShrink.App/MainWindow.axaml.cs:1896
  main.quality.target     src/VidShrink.App/MainWindow.axaml.cs:1894
```

Aynı tarama `en` ve `tr` kataloglarının birebir aynı 387 anahtarı taşıdığını da
gösterdi (iki yönde de fark yok), yani kusur çeviri eksiği değil, çağrı yeriyle
katalog arasındaki uyuşmazlık.

**Taramanın kör noktası:** anahtarı çalışma anında kuran çağrılar sabit
taramayla görünmez. Depoda böyle iki çağrı var, `MainWindow.axaml.cs:1557` ve
`:1558`; ikisi de anahtarı başka yerdeki sabitlerden alıyor, yani bu iki satır
yeni bir eksik anahtar saklamıyor. Onun dışında her `Say(`/`Strings.Get(`
çağrısı sabit dizeyle yazılmış.

### İkinci düzeltme

İki kusur, iki farklı onarım — çünkü birinde doğru anahtar zaten duruyor,
öbüründe hiç yazılmamış:

```
- AddQualityRow(grid, Say("main.quality.loss"), Say("main.quality.points", ...));
+ AddQualityRow(grid, Say("main.quality.loss"), Say("main.quality.loss-points", ...));
```

```
  Locales/en/main.json:68  + "main.quality.target": "Target",
  Locales/tr/main.json:68  + "main.quality.target": "Hedef",
```

`main.quality.points` için çağrı yeri düzeltildi: `loss-points` anahtarı iki
dilde de doğru metinle duruyor ve başka çağıranı yok — yani eksik olan anahtar
değil, adı. `main.quality.target` içinse ortada bir anahtar yok, o yüzden
anahtarın kendisi eklendi. Metni `main.target.title`ın metninden birebir alındı
(`"Target"` / `"Hedef"`); satırın gösterdiği değer zaten hedef boyut. Bunu
doğrudan `main.target.title`a bağlamak da mümkündü, ama o anahtar hedef
panelinin başlığı; kalite ipucu satırını panel başlığına bağlamak ikisini
birbirine kilitler. **Yeni bir kullanıcıya görünen dize eklemek T0'ın kararı;
tersini isterseniz tek satır.**

Düzeltmeden sonra tarama temiz:

```
eksik anahtar: yok
```

## Dördüncü tam koşum — iki düzeltme + canlı kaynak

Aynı yapılandırma (Debug), aynı ffmpeg; iki değişen: ikinci yerelleştirme
düzeltmesi ve `VIDSHRINK_LIVE_SOURCE` kapısının açılması.

```
VIDSHRINK_LIVE_SOURCE=$PWD/.calisma/kaynak/parca-2.mkv \
dotnet test --logger "trx;LogFileName=suit-canli.trx" --results-directory .calisma/gunluk
```

Kaynak `parca-2.mkv`: ortak havuzun ses taşıyan tek temsili parçası
(video+AAC, 60,442 sn, 115 933 238 bayt). `parca-1`de ses yok.

```
Failed!  - Failed:     9, Passed:  1339, Skipped:     6, Total:  1354, Duration: 2 h 44 m
```

| | Üçüncü | Dördüncü |
|---|---:|---:|
| Toplam | 1336 | **1354** |
| Geçen | 1281 | 1339 |
| Kırmızı | 37 | **9** |
| Atlanan | 18 | **6** |
| Süre | 20 dk 7 sn | 2 sa 44 dk |

Toplam 1336'dan 1354'e çıktı: canlı kaynakla birlikte `LiveSourceTheory`
üyelerinin `InlineData` durumları da sayıya girdi, +18.

### İki düzeltme de doğrulandı

Dokuz kırmızının **hiçbiri** yerelleştirme kaynaklı değil — üçüncü koşumda
37/37 olan `main.quality.target` iletisi bu koşumda sıfır kez geçiyor, ikinci
koşumun `main.plan.fact.estimate`i de öyle. Statik taramanın "eksik anahtar:
yok" sonucu koşumla tutuyor.

### `VIDSHRINK_LIVE_SOURCE` kapısındaki 12 ölçünün hepsi koştu

Atlanan 18'den 6'ya düştü. Kalan 6'yı tek tek saydım:

| Ölçü | Kapı |
|---|---|
| `UpdaterTests.EveryLaunchChecksAndStaysWithinTheTimeout` | `VIDSHRINK_LAUNCHER_EXE` |
| `UpdaterTests.TheIncomingBinaryRenamesItselfOntoTheTargetName` | `VIDSHRINK_LAUNCHER_EXE` |
| `UpdaterTests.SwitchedOffLauncherMakesNoNetworkRequestAtAll` | `VIDSHRINK_LAUNCHER_EXE` |
| `HardwareVerdictTests.LiveProbeDecidesOnThisMachine` | `VIDSHRINK_LIVE_PROBE` |
| `HardwareVerdictTests.TheFirstLayoutDoesNotWaitForTheProbe` | `VIDSHRINK_LIVE_PROBE` |
| `PerformanceCheckTests.DonanimKodlayiciIslemciZamaniniOlculebilirYaziyorMu` | `h264_nvenc` yok |

3 + 2 + 1 = **6**. `VIDSHRINK_LIVE_SOURCE` ve `zscale` grupları listeden
tamamen düştü.

### Kalan 9 kırmızı

| Ölçü | İleti (ilk satır) |
|---|---|
| `CalibrationProbeTests.LiveFastModeLandsInsideTheBandOnTheFirstAttempt(8)` | `target 8 MB \| band 7,36-8,00 \| plan libx264 slow 1920x1080@60 2pass bias 1` |
| `CalibrationProbeTests.LiveFastModeLandsInsideTheBandOnTheFirstAttempt(25)` | `target 25 MB \| band 23,75-25,00 \| plan libx264 slow 1920x1080@60 2pass bias 1` |
| `CalibrationProbeTests.LiveFastModeLandsInsideTheBandOnTheFirstAttempt(50)` | `target 50 MB \| band 48,60-50,00 \| plan libx264 slow 1920x1080@60 2pass bias 1` |
| `CalibrationProbeTests.LiveFastModeLandsInsideTheBandOnTheFirstAttempt(100)` | `target 100 MB \| band 97,20-100,00 \| plan libx264 slow 1920x1080@60 2pass bias 1` |
| `FillBandTests.LiveFillTargetRunStaysInsideTheBand(180)` | `target 180 MB \| band 174,96-180,00 \| hard floor 169,92 \| calibrated True` |
| `HardwareFlagTests.LiveFastRunDoesNotSpendEveryAttempt(180)` | `target 180 MB \| band 174,96-180,00 \| plan libx264 slow 1920x1080@60 2pass 14174k` |
| `HardwareRateControlTests.LiveFastTargetsLandInsideTheBandOnTheFirstAttempt(50)` | `Fast 50 MB \| libx264 slow 1920x1080@60 2pass 6680k` |
| `PlaybackFrameSourceTests.Duraklatma_sureci_oldurmez` | `Assert.Equal() Failure: Values differ` |
| `WindowLayoutTests.ThePageContentStaysAtItsPinnedHeight(loaded: True, narrow: True, least: 1002, most: 1102)` | `Assert.InRange() Failure: Value not in range` |

7 + 2 = **9**. İlk yedisi canlı kaynak kapısındandı ve hepsi aynı şeyi
söylüyor: bu kaynakta plan hedef bandının dışına düşüyor. Son ikisi canlı
kaynakla ilgisiz; üçüncü koşumda da vardılar (`WindowLayoutTests`in 17
kırmızısından biri) ya da orada yerelleştirme kırmızısının altında kalmıştı.
Bu dokuzunun kök nedenini **çözümlemedim** — İş 1'in kapsamı Windows'ta erken
dönüp geçen ölçüleri gerçekten koşturmak ve kırmızıya düşenleri raporlamaktı;
bu yedi ölçü bu turda ilk kez gerçek kaynakla koştu, dolayısıyla bulgunun
kendisi çıktı.

**Yedi canlı kırmızının hepsinde plan `libx264`.** "Fast" yolu bu makinede
yazılım kodlayıcıya düşüyor; donanım kolu yok. Bu, İş 2'nin K4 hükmüyle aynı
kapı: depoda VideoToolbox'a giden tek satır bile yok, dolayısıyla Apple
Silicon'da `FastHardwareOrder` boşa çıkıyor.

### Koşumun 2 saat 44 dakika sürmesinin sebebi de aynı hüküm

Canlı ölçüler 60 saniyelik 1080p60 HDR klibi altı ayrı hedef boyutta
`libx264`/`libx265` **yazılım** kodlayıcıyla defalarca kodluyor. İş 2'de bu
makinede ölçülen hız: `libx265 -preset slow` 60 saniyelik kaynağı 327-1244
saniyede kodluyor, `hevc_videotoolbox` aynı işi 17,9-45,5 saniyede
(`videotoolbox.md`, "Hız" — 6 oranın hepsi 16,1×-37,7× arasında). Yani süre
bir ölçüm kazası değil, donanım kolunun bağlı olmamasının doğrudan bedeli.

## Koşan ve koşmayan — özet

| Kapı | Ölçü | Bu turda |
|---|---|---|
| `VIDSHRINK_LIVE_PROBE` | 2 | **koştu, geçti** |
| Platform kapısı (macOS) | 7 | **koştu, geçti** |
| `VIDSHRINK_LIVE_SOURCE` | 12 | **koştu** (dördüncü koşum, `parca-2.mkv`); 7'si kırmızı |
| `zscale` yok | 2 atlanan + 10 kırmızı | koşmadı — ffmpeg derlemesi |
| `VIDSHRINK_LAUNCHER_EXE` | 3 | macOS'ta hiç koşamaz |
| `h264_nvenc` yok | 1 | Apple Silicon'da hiç koşamaz |
