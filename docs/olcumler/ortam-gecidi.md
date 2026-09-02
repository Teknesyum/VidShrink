# T117 — Ortam geçidi: ffmpeg var mı sorusu yetmiyor

**Tarih:** 02.09.2026 · **Sözleşme:** `.claude/relay/contracts/T117.md`

T115 CI'a ffmpeg kurdu ve atlanan ölçü 95'ten 17'ye düştü. Açılan ölçülerden
biri kırmızıya düştü. Bu belge o kırmızının sebebini ölçer, geçidi ayrıştırır
ve geriye kalan ortam varsayımlarını sayar.

Ölçülen dal: `T117-ortam-gecidi`, taban `origin/T115-ci-ffmpeg` (`0e122f2`).

## K1 — kırmızının ölçülen sebebi

Kanıt koşumu: **`33589639249`**, headSha **`0e122f2728fd429f5136ae3bc6a784736a51f85b`**.
Kapı çıktısı: `Failed: 1, Passed: 1162, Skipped: 17, Total: 1180`, süre 18 dk 59 sn.

Düşen ölçü: `PerformanceCheckTests.IslemciZamaniSayaciDogruOkuyorMu` (26 sn).

**Düşen çağrı.** Yığın izi iki satır: `PerformanceCheckTests.cs:713` →
`PerformanceCheckTests.Kos(...)` → `PerformanceCheckTests.cs:763`. 763. satır
`Assert.True(p.ExitCode == 0, ...)`. Yani düşen şey bir hesap değil, bir ffmpeg
sürecinin çıkış kodu. Etiketiyle: `(d) nvenc -threads 1 #0`, komut

```
ffmpeg -threads 1 -i ornek.mp4 -an -c:v h264_nvenc -threads 1 -f null -
```

**Hata kodları.** Süreç `-1` ile döndü. Günlükteki zincir:

| kaynak | kod | metin |
|---|---|---|
| `[h264_nvenc]` | — | `Cannot load nvcuda.dll` |
| `[vost#0:0/h264_nvenc]` | — | `Error while opening encoder` |
| `[vf#0:0]` | `-1` | `Operation not permitted` |
| `[vost#0:0/h264_nvenc]` | `-22` | `Invalid argument` |

Testin gördüğü çıkış kodu **-1**'dir; `-22` kodlayıcı iş parçacığının kendi
sonlanma kodu, süreç çıkışı değil.

**T115'in hipotezi kısmen doğru.** "CI koşucusunda GPU yok" doğru, ama kırmızının
sebebi bu değil. Sebep, GPU yokluğunun **geçitten geçebilmiş olması**. Ölçü
`EncoderCapabilities.Instance.HasEncoder("h264_nvenc")` ile korunuyordu ve
`HasEncoder`, `ffmpeg -encoders` çıktısındaki adları okur
(`EncoderCapabilities.cs:27`, `ParseEncoders`). Bu **derlemenin yeteneğini**
söyler, makinenin değil: sürücü yoksa ad listede durmaya devam eder. CI'daki
ffmpeg nvenc destekli derlendiği için geçit açıldı, açılış sürücüde düştü.

**Yerelde tekrar üretilemez** ve üretilmesi de beklenmez: bu makinede GPU var.
Ölçülen ayrım (bu makine, 02.09.2026):

| soru | çağrı | yerel | CI |
|---|---|---|---|
| ad derlemede var mı | `ffmpeg -encoders \| grep h264_nvenc` | 1 satır (ölçüldü) | evet (dolaylı: `HasEncoder` dalı açıldığı için (d) bacağı koştu) |
| bu makinede açılıyor mu | 1 karelik `-c:v h264_nvenc` denemesi | çıkış kodu 0 | `Cannot load nvcuda.dll`, -1 |

İki sorunun CI'da farklı cevap vermesi kırmızının tamamıdır.

## K2 — ölçü susturulmadı, ikiye bölündü

`IslemciZamaniSayaciDogruOkuyorMu` yedi bacak koşuyordu: (a) iş parçacığı
kalibrasyonu, (b)(c) x264, (d)(e) nvenc, (f) taban kopya, (g) tekrar. Yalnız
(d)(e) donanım varsayıyor. Ölçünün tamamını `Skip` etmek geri kalan beş bacağı
da CI'dan silerdi.

Yapılan: (d)(e) `PerformanceCheckTests.DonanimKodlayiciIslemciZamaniniOlculebilirYaziyorMu`
ölçüsüne taşındı ve `[HardwareEncoderFact]` ile korundu. Kalan bacaklar
`IslemciZamaniSayaciDogruOkuyorMu` içinde `[FfmpegFact]` altında kaldı ve
CI'da koşmaya devam ediyor.

Atlanan ölçü koşum özetinde görünür. Koşum `33591434219` günlüğünden, olduğu
gibi:

```
[xUnit.net 00:15:49.03]     VidShrink.Tests.PerformanceCheckTests.DonanimKodlayiciIslemciZamaniniOlculebilirYaziyorMu [SKIP]
  Skipped VidShrink.Tests.PerformanceCheckTests.DonanimKodlayiciIslemciZamaniniOlculebilirYaziyorMu [1 ms]
Passed!  - Failed:     0, Passed:  1163, Skipped:    18, Total:  1181, Duration: 15 m 50 s
```

Sessiz erken çıkış yok: eski `else` dalındaki `Log(...)` satırı — ölçüm
günlüğüne yazan ama koşum özetine hiç girmeyen — kaldırıldı. Ölçü artık
adıyla sayılıyor ve `Skipped` toplamına giriyor.

**Sebep metni CI konsolunda yazmıyor — ölçüldü.** Yukarıdaki üç satır günlükteki
her şeydir; `SKIP` satırından sonra sebep basılmıyor. Sebep `Skip` alanında
duruyor ve TRX/IDE tarafında okunuyor, konsolda okunmuyor. Sebebi konsola
düşürmek `dotnet test` çağrısına dokunmayı ister
(`tools/kosum-kapisi/kosum-kapisi.ps1:20`, `--logger trx` ya da yükseltilmiş
`--verbosity` yok); o dosya T115'in, bu sözleşmede dokunulmadı. Açık kalan
budur.

Geçidin yazdığı üç sebepten hangisinin çıktığı hangi durumun geçerli olduğunu
söyler:

| durum | `Skip` metni |
|---|---|
| ffmpeg yok | `<arac> bulunamadi, donanim kodlayici olculeri kosturulmadi.` |
| ad derlemede yok | `h264_nvenc bu ffmpeg derlemesinde yok, ...` |
| ad var, açılış düştü | `h264_nvenc derlemede var ama bu makinede acilmadi (<ms>ms), ...` |

## K3 — ayrışan geçit ve mutasyon kanıtı

`HardwareEncoderFactAttribute` (`tests/VidShrink.Tests/FrameGrabberTests.cs`)
yeni bir yoklama düzeneği kurmaz. `EncoderCapabilities` içinde ayrım zaten
vardı ve kullanılmıyordu:

- `HasEncoder(codec)` — `ffmpeg -encoders` listesinde ad var mı (derleme)
- `Probe(codec)` — 1 karelik gerçek kodlama, sonuç süreç ömrü boyunca
  önbellekte (`EncoderCapabilities.cs:79`)

Geçit sırayla ikisini de sorar, `Probe` başarısızsa ölçüyü sebebiyle atlar.

**İki yönlü mutasyon.** Her koşumdan önce
`dotnet build VidShrink.sln -c Release --no-incremental`.

| mutasyon | ortam | filtre | sonuç |
|---|---|---|---|
| yok (gerçek geçit) | yerel (GPU var) | iki ölçü | `Başarısız 0, Başarılı 2, Atlanan 0`, 23 sn |
| B: geçit her zaman `false` | yerel (GPU var) | iki ölçü | `Başarısız 0, Başarılı 1, Atlanan 1`, 32 sn |
| yok (gerçek geçit) | CI (GPU yok) | tam süit | `Failed 0, Passed 1163, Skipped 18, Total 1181` — koşum `33591434219` |
| A: geçit her zaman `true` | CI (GPU yok) | tam süit | `Failed 1, Passed 1163, Skipped 17, Total 1181` — koşum `33591455814` |

Yerel filtre: `--filter "FullyQualifiedName~IslemciZamaniSayaciDogruOkuyorMu|FullyQualifiedName~DonanimKodlayiciIslemciZamaniniOlculebilirYaziyorMu"`.

Mutasyon A dalı `T117-mutasyon-a` denetim bitene kadar duruyor (koşum kaydı
id ile kalıcı, dal denetçinin doğrulaması için tutuldu); `main`e
gitmez.

Süreler paylaşımlı makinede alındı (aynı anda sekiz ajan koşuyor); karar
sayıları değil, geçen/atlanan sayıları kanıttır.

## K4 — diğer on bir dosyanın ortam varsayımları

`[FfmpegFact]` taşıyan on üç dosya var; ikisi bu sözleşmenin
(`FrameGrabberTests.cs` 21 ölçü, `PerformanceCheckTests.cs` 7 ölçü). Kalan on
biri aşağıda. **Hiçbirine yazılmadı**, hepsi okundu.

"CI'da tutuyor mu" sütununun kanıtı koşum `33589639249`: o koşumda tek düşen
`IslemciZamaniSayaciDogruOkuyorMu` idi ve on yedi atlananın hiçbiri bu on bir
dosyada değil. Yani bu dosyaların bütün `[FfmpegFact]` ölçüleri CI'da koştu ve
geçti — "tutuyor" bunu söyler, ölçünün **anlamlı** koştuğunu söylemez.

| dosya | ölçü | ffmpeg'in ötesindeki varsayım (belirteç, satır) | CI'da |
|---|---|---|---|
| `ComplexityProbeTests.cs` | 6 | kodlayıcı `libx264` (179); geçici dizin `TestPaths.OutputRoot` altında GUID (174-182); duvar saati `WaitAsync(10 sn)` (166) | tutuyor |
| `EncodeRunnerTests.cs` | 4 | kodlayıcı `libx264` + `-preset ultrafast` (119, 128, 143); geçici dizin + ara çıktı silme (135, 164) | tutuyor |
| `FfmpegArgumentsTests.cs` | 2 | yalnız x264'te olan `-x264-params scenecut=0` (990); filtre `filter_complex ... concat=n=8` (987-988); `ffprobe -skip_frame nokey` ile I-kare sayımı (913) | tutuyor |
| `FpsDropTests.cs` | 5 | **donanım kodlayıcı** `h264_nvenc, hevc_nvenc, av1_nvenc, h264_qsv, h264_amf` (323); ses akışı `sine` + `-c:a aac` (47-49); `.calisma/t60/olcum.txt`'ye kilitli yazım (73-78) | tutuyor, **ama boş** (aşağıda) |
| `PanelHostTests.cs` | 11 | Avalonia çalışma zamanı, Windows'ta `UseWin32()+UseSkia()` (`AppHost.cs:48-51`); duvar saati eşikleri (390-444, 520, 588) ve ilk kare için 10 sn son tarih (128, 644); `SegmentEncoder.TempPrefix` dosya sayımı (655-660) | tutuyor |
| `PreviewSyncTests.cs` | 4 | Avalonia `PanelHost`/`ComparisonPanel` (233-242); filtre `geq` rampası (45), `filter_complex` fps/scale/hstack + `rawvideo gray` (390-394), `-stream_loop -1` (384) | tutuyor |
| `QualityMeterTests.cs` | 12 (+1 `TonemapFact`) | filtre `libvmaf`, `XPSNR` (21-23, 70), `zscale`+`tonemap` (524, 532), `select`/`setpts` (192, 218), `boxblur` (325); ses akışı `anullsrc` + `aac` (512) | tutuyor; `TonemapFact` **atlanmadı**, yani CI'daki ffmpeg'de `zscale`/`tonemap` var |
| `QualityTargetTests.cs` | 1 | kodlayıcı `libx264 -crf 23`; 120 sn'lik 1080p30 ve 720p60 klipler `.calisma/t57` altında **kalıcı** tutuluyor, varsa yeniden üretilmiyor (489-508) | tutuyor; önbelleğin CI'da dolu mu boş mu geldiği **ölçülmedi** |
| `SceneMapTests.cs` | 4 | kodlayıcı `libx264`; sonda kolu çıktısının bayt boyutu **%2 içinde** eşleşmeli (441-447); filtre `eq=brightness`, `concat=n=6`, `hstack` (492-493, 512-513) | tutuyor |
| `SegmentEncoderTests.cs` | 6 | eşzamanlılık `Assert.Equal(1, PeakConcurrentEncodes)` (168) ve `TempPrefix` disk sayımı (185-209); filtre `fps/scale/hstack` + `rawvideo bgra` (319-326); kayıpsız `-qp 0` (123-124) | tutuyor |
| `VmafPoolingTests.cs` | 3 | filtre `psnr=stats_file=...` (162-186) ve eşik `psnr_y >= 80.0 dB` (256); kodlayıcı `ffv1` kayıpsız (269); `psnr.log`'un sürecin `WorkingDirectory`'sine yazıldığı varsayımı (276, 296) | tutuyor |

**`FpsDropTests.Donanim_kodlayicilari_da_ayni_kare_sayisini_uretir` yeşil ama
hiçbir şey ölçmüyor.** Beş kodlayıcının hepsini `WorksAsEncoder` ile eliyor
(323) — geçit doğru, çünkü `HasEncoder` değil `Probe` yolunu kullanıyor. CI'da
hepsi elenir, `tried = 0` kalır, döngü gövdesindeki tek `Assert` hiç
çalışmaz ve ölçü geçer. Bu, T117'nin kapattığı kusurun aynası: orada geçit
gevşekti ve kırmızı verdi, burada geçit doğru ama **atlandığı görünmüyor**.
`tried == 0` durumu koşum özetine girmiyor, yalnız ölçüm günlüğüne yazılıyor.
Dosya bu sözleşmenin değil; not olarak bırakılıyor.

Öncelik sırası (T0 buradan sözleşme açacaksa): `FpsDropTests` (sessiz boş
geçiş), `QualityMeterTests` (en çok filtre varsayımı, hepsi tek geçidin
arkasında), `PanelHostTests`/`PreviewSyncTests` (ffmpeg değil Avalonia
varsayıyor, `FfmpegFact` bunu söylemiyor).

## K5 — CI doğrulaması

İki koşum, ikisi de ffmpeg'li `ci.yml` ile, ikisi de `windows-latest`.

| koşum | dal | headSha | sonuç | Failed / Passed / Skipped / Total | süre |
|---|---|---|---|---|---|
| **`33591434219`** | `T117-ortam-gecidi` | **`58e2d45feb4e0958d07cd19e88497eed23c17b30`** | **success** | 0 / 1163 / 18 / 1181 | 15 dk 50 sn |
| `33591455814` | `T117-mutasyon-a` (atılacak) | `8336c9088390b0a072422c6496801615c5196e50` | failure | 1 / 1163 / 17 / 1181 | 16 dk 48 sn |

Yeşil koşumun kapı satırı:

```
KOŞUM KAPISI GEÇTİ: başarısız=0 toplam=1181 alt-sınır=1134 atlanan=18 ust-sinir=30
```

**K0 kapandı.** `T117-ortam-gecidi` dalında ffmpeg'li CI koşumu yeşil:
`33591434219`, headSha `58e2d45feb4e0958d07cd19e88497eed23c17b30`.

Taban koşumla (`33589639249`) karşılaştırma:

| | taban `33589639249` | yeşil `33591434219` | mutasyon A `33591455814` |
|---|---|---|---|
| Failed | 1 | **0** | 1 |
| Passed | 1162 | 1163 | 1163 |
| Skipped | 17 | **18** | 17 |
| Total | 1180 | 1181 | 1181 |

Total 1180 → 1181: ayrılan donanım bacakları yeni bir ölçü oluşturdu.
Skipped 17 → 18: yeni ölçü CI'da atlanan o birdir.
Failed 1 → 0: `IslemciZamaniSayaciDogruOkuyorMu` artık koşuyor **ve geçiyor** —
yeşil koşumun atlananlar listesinde yok, düşenler listesi boş.

Mutasyon A aynı sayıları ters çeviriyor: geçit hep `true` olunca yeni ölçü
atlanmıyor (Skipped 17), koşuyor ve `Cannot load nvcuda.dll` / çıkış kodu `-1`
ile düşüyor (Failed 1). Yani geçidin CI'daki kararı ölçünün sonucunu
belirliyor; geçit eşdeğer değil.

## K6 — CI'da atlanan on yedi ölçü

Koşum `33589639249`, `Skipped: 17`. Adlar günlükten çıkarıldı, sayılmadı.

**Hiçbiri `[FfmpegFact]` değil.** On yedisi de ortam değişkeni bekleyen beş
"canlı" geçit sınıfından geçiyor: gerçek bir kaynak dosya ya da gerçek bir başlatıcı
ikilisi olmadan koşamazlar. Bu ölçüler T115'ten önce de atlanıyordu; ffmpeg'in
gelmesi onları etkilemedi.

| # | ölçü | geçit | beklediği |
|---|---|---|---|
| 1 | `CalibrationProbeTests.LiveFastModeLandsInsideTheBandOnTheFirstAttempt` | `LiveSourceTheory` (410) | `VIDSHRINK_LIVE_SOURCE` |
| 2 | `CalibrationProbeTests.LiveEncodeTimeMatchesTheMeasuredEstimate` | `LiveSourceTheory` (355) | `VIDSHRINK_LIVE_SOURCE` |
| 3 | `CalibrationProbeTests.LiveTimeSurvivesTheTargetsThatKeepThePlanShape` | `LiveSourceFact` (332) | `VIDSHRINK_LIVE_SOURCE` |
| 4 | `ExtremeCompressionTests.LiveProbeMeasuresMotionWithinItsTimeBudget` | `LiveSourceFact` (268) | `VIDSHRINK_LIVE_SOURCE` |
| 5 | `ExtremeCompressionTests.LiveExtremeTargetsProduceAPlayablePicture` | `LiveSourceTheory` (287) | `VIDSHRINK_LIVE_SOURCE` |
| 6 | `ExtremeCompressionTests.LiveQualityCurveShowsWhereTheCodecStopsCarryingThePicture` | `LiveSourceFact` (318) | `VIDSHRINK_LIVE_SOURCE` |
| 7 | `FillBandTests.LiveFillTargetRunStaysInsideTheBand` | `LiveSourceTheory` (470) | `VIDSHRINK_LIVE_SOURCE` |
| 8 | `HardwareFlagTests.LiveFastRunDoesNotSpendEveryAttempt` | `LiveSourceTheory` (179) | `VIDSHRINK_LIVE_SOURCE` |
| 9 | `HardwareRateControlTests.LiveFastTargetsLandInsideTheBandOnTheFirstAttempt` | `LiveSourceTheory` (440) | `VIDSHRINK_LIVE_SOURCE` |
| 10 | `HardwareRateControlTests.LiveProcessorTargetsStillLandInsideTheBandOnTheFirstAttempt` | `LiveSourceTheory` (449) | `VIDSHRINK_LIVE_SOURCE` |
| 11 | `PlaybackFrameSourceTests.Canli_kaynak_iki_paneli_besliyor` | `LivePlaybackFact` (351) | `VIDSHRINK_LIVE_SOURCE` |
| 12 | `PlaybackFrameSourceTests.Duraklatma_sureci_oldurmez` | `LivePlaybackFact` (396) | `VIDSHRINK_LIVE_SOURCE` |
| 13 | `HardwareVerdictTests.LiveProbeDecidesOnThisMachine` | `LiveProbeFact` (481) | `VIDSHRINK_LIVE_PROBE` |
| 14 | `HardwareVerdictTests.TheFirstLayoutDoesNotWaitForTheProbe` | `LiveProbeFact` (531) | `VIDSHRINK_LIVE_PROBE` |
| 15 | `UpdaterTests.EveryLaunchChecksAndStaysWithinTheTimeout` | `LiveLauncherFact` (873) | `VIDSHRINK_LAUNCHER_EXE` |
| 16 | `UpdaterTests.SwitchedOffLauncherMakesNoNetworkRequestAtAll` | `LiveLauncherFact` (900) | `VIDSHRINK_LAUNCHER_EXE` |
| 17 | `UpdaterTests.TheIncomingBinaryRenamesItselfOntoTheTargetName` | `LiveLauncherFact` (919) | `VIDSHRINK_LAUNCHER_EXE` |

Beş geçit sınıfı (`LiveSourceFact`, `LiveSourceTheory`, `LiveProbeFact`,
`LivePlaybackFact`, `LiveLauncherFact`), üç ortam değişkeni:
`VIDSHRINK_LIVE_SOURCE` 12 ölçüyü, `VIDSHRINK_LIVE_PROBE` 2 ölçüyü,
`VIDSHRINK_LAUNCHER_EXE` 3 ölçüyü tutuyor. 12+2+3 = 17.

**Meşru mu?** Üç değişkenin üçü de bir dosya yolu bekliyor ve dosya CI'da yok. Ortam
yokluğu, kod kusuru değil — geçitler sebebi yazıyor ve sayıya giriyor. Ama
üç değişkenin hiçbiri `.github/workflows/ci.yml` içinde kurulmuyor: bu on yedi
ölçü **hiçbir CI koşumunda koşmadı** ve doğru çalıştıkları CI'da ölçülmedi.
Bu belge bunu tespit eder, çözmez.

Kaçının içinde saklı bir kusur olduğu **ölçülmedi**: ölçmek gerçek bir kaynak
dosya ve gerçek bir başlatıcı ikilisi ister, ikisi de CI'da yok.

## Ölçülmeyenler

- Mutasyon A'nın **yerel** karşılığı ölçülmedi: bu makinede GPU var, geçit
  zaten `true` dönüyor, mutasyon eşdeğer olurdu.
- Mutasyon B'nin **CI** karşılığı ölçülmedi: CI'da gerçek geçit de `false`
  dönüyor, mutasyon eşdeğer olurdu.
- On bir dosyadaki ölçülerin **hangi bacağının** hangi varsayıma dayandığı
  ayrıştırılmadı; envanter dosya düzeyindedir, `IslemciZamani...` için yapılan
  bacak ayrımı onlarda yapılmadı.
- `tools/ci-gibi-kos.sh` bu sözleşmede **koşturulmadı**. Betik PATH'ten ffmpeg
  ve ffprobe'u siliyor (`ci-gibi-kos.sh:6`) ve başlığında "CI'ın gördüğü hal:
  ffmpeg ve ffprobe PATH'te yok" yazıyor. T115'ten sonra bu cümle yanlış: CI'da
  ffmpeg var. Betiğin sahibi yok, bu sözleşmede dokunulmadı.
- Atlama **sebep metninin** CI konsolunda görünmesi sağlanamadı; sebep `Skip`
  alanında duruyor ama `dotnet test` çağrısı T115'in dosyasında. Bkz. K2.
- Yeşil koşumda `IslemciZamaniSayaciDogruOkuyorMu` geçti, ama (f)(g)
  bacaklarının **ürettiği sayılar** okunmadı: ölçüm günlüğü `.calisma/` altında
  kalıyor ve CI eserlerine yüklenmiyor. Geçtiği ölçüldü, ne yazdığı ölçülmedi.
