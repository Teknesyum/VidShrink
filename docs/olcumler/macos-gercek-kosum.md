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

**Düzeltilmedi.** Sebebi: düzeltmesi kod değişikliği değil ortam değişikliği —
`zscale` taşıyan bir ffmpeg gerekiyor (kaynaktan `--enable-libzimg`, ya da
zimg taşıyan bir dağıtım). Bunu kurmak paketin sınırları dışında ve T0'ın kararı;
`_sorun.log`a yazıldı.

İki not, ikisi de ölçülmüş:

- `EncoderCapabilitiesTests.cs:55` `Assert.True(caps.HasFilter("zscale"))` diyor
  ama o ölçü kırmızı listede değil — çünkü gerçek ffmpeg'i değil dosyadaki sabit
  metni ayrıştırıyor (`EncoderCapabilitiesTests.cs:40`).
- Bu eksik İş 2'nin ölçerini kesiyor: `tools/VidShrink.Bench measure` aynı
  `QualityMeter`i çağırıyor (`Program.cs:76`), yani bu makinede koşmuyor.
  **Ama Bench'in ikinci bir ölçeri var ve o koşuyor:** `bench shrink` kendi
  `VmafNegAsync`ini kullanıyor (`Program.cs`, `BenchMeasureFilterGraph.Build`),
  o da `scale=...:flags=lanczos` + kare kilidi kuruyor, `zscale` istemiyor.
  Aşağıdaki uçtan uca koşum VMAF üretti. İkisinin farkı tam olarak renk
  normalizasyonu: Bench'in yolu **açık renk normalizasyonu yapmıyor**, yani
  etiketi farklı iki klibi karşılaştırdığında `QualityMeter`in kapattığı kusura
  açık. Aynı renk uzayındaki iki klip için sonuç güvenilir, farklı olanlar için
  değil.

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

### `VIDSHRINK_LIVE_SOURCE` — koşturulamadı

Ortak ölçüm havuzu bu makinede yok ve indirilemedi; ayrıntısı
`.claude/relay/live/_sorun.log`da. Yukarıdaki 12 ölçü bu paket turunda da koşmadı.

## Koşan ve koşmayan — özet

| Kapı | Ölçü | Bu turda |
|---|---|---|
| `VIDSHRINK_LIVE_PROBE` | 2 | **koştu, geçti** |
| Platform kapısı (macOS) | 7 | **koştu, geçti** |
| `VIDSHRINK_LIVE_SOURCE` | 12 | koşmadı — kaynak yok |
| `zscale` yok | 2 atlanan + 10 kırmızı | koşmadı — ffmpeg derlemesi |
| `VIDSHRINK_LAUNCHER_EXE` | 3 | macOS'ta hiç koşamaz |
| `h264_nvenc` yok | 1 | Apple Silicon'da hiç koşamaz |
