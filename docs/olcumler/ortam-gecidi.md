# T117 — Ortam geçidi: ffmpeg var mı sorusu yetmiyor

**Tarih:** 02.09.2026 · **Sözleşme:** `.claude/relay/contracts/T117.md`

T115 CI'a ffmpeg kurdu ve atlanan ölçü 95'ten 17'ye düştü. Açılan ölçülerden
biri kırmızıya düştü. Bu belge o kırmızının sebebini ölçer, geçidi ayrıştırır
ve geriye kalan ortam varsayımlarını sayar.

Ölçülen dal: `T117-ortam-gecidi`, taban `origin/T115-ci-ffmpeg` (`0e122f2`).

**Üç cümlelik sonuç.** (1) CI'daki kırmızı kararsız değil, **belirlenimci**: iki
farklı commit'te iki koşum, aynı ölçü, aynı dörtlü — GitHub runner'ında NVIDIA
aygıtı yok. (2) `origin/main`in ci.yml'inde ffmpeg kurulumu yok
(`grep -c 'codexffmpeg'` → 0), yani main'in bugünkü yeşili bu ölçünün geçtiğini
değil **hiç koşmadığını** gösteriyor; T115 main'e girdiği anda main de bu ölçüden
kırmızıya dönerdi. (3) Bu belgedeki geçit o kapıyı açar: ölçü artık gerçek NVIDIA
aygıtı olmayan makinede sebebiyle atlanır, olan makinede koşar — ikisi de
ölçüldü (§K3).

**Bu ölçü gerçek NVIDIA aygıtı gerektiriyor mu?** Evet. (d)(e) bacakları
`h264_nvenc` ile gerçek kodlama yapar; sürücü yoksa `Cannot load nvcuda.dll`
ile düşer. Bu yüzden geçit "nvenc derlenmiş mi" (`HasEncoder`) sorusuna değil,
**"nvenc bu makinede açılıyor mu"** sorusuna bağlandı — `WorksAsEncoder`ın
altındaki `Probe` yolu. Ölçünün donanım gerektirmeyen bacakları (a)(b)(c)(f)(g)
ayrıldı ve `[FfmpegFact]` altında koşmaya devam ediyor.

Üretim kodundaki kök sebep — `HasEncoder`ın sürücüye değil derlemeye bakması —
bu sözleşmede **düzeltilmedi**, T123'ün. Burada yapılan, ölçünün doğru ortamda
koşmasıdır.

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

## K7 — ikinci eksen: makinenin yükü

Bu bölüm T0'ın tur ortasında eklediği maddedir. Aynı dosyada, aynı sınıftan
ikinci bir kusur: **ölçü, koştuğu makinenin durumunu sabit sanıyor.**

### İki kırmızı karıştırılmasın

| olay | nerede | ölçülen sıklık | sebep | sınıf |
|---|---|---|---|---|
| `IslemciZamaniSayaciDogruOkuyorMu` | CI | 2/2 kırmızı (`33589639249` `0e122f2`, `33593652976` `045648e`) | runner'da NVIDIA aygıtı yok, `Cannot load nvcuda.dll` | **belirlenimci** |
| `OlcumYukAltindaYalnizAgirlasiyor:458` | bu makine | 1/5 kırmızı (T113 ölçtü, yalıtılmış koşumlar) | altı ajan koşuyordu, boş okumalar `HeavyLoad` düştü | **kararsız** |

İkisinin ortak yanı sebep değil, kusur sınıfı: her ikisi de ortam varsayımını
sınamadan kullanıyor. Sebepleri ayrı, çözümleri ayrı geçit.

Belirlenimci olanın **üretim kodundaki** yüzü bu sözleşmede düzeltilmedi:
`HasEncoder` derlenmiş yeteneğe bakıyor, sürücüye değil. O T123'ün.

### Kararsız iddianın sınıflandırması

Kaldırılan satır (`PerformanceCheckTests.cs:458`, T117 öncesi):

```csharp
Assert.False(sessizler.Any(r => r.Impact == RecordingImpact.SoftwareHeavyLoad)
             && yuklu.Impact == RecordingImpact.SoftwareLightLoad,
    "yuk altinda karar hafifledi");
```

İddia yalnızca **boş okumalar zaten `SoftwareHeavyLoad` çıktığında** — yani
kendi öncülü olan "sessiz taban" yanlışken — kurulabiliyor. Gerçekten boş bir
makinede vakuf doğru, hiçbir şey ölçmüyor. Dolu bir makinede ölçtüğü şey ürünün
kararı değil, makinenin o anki gürültüsü. **Eşik gevşetilmedi, kaldırıldı ve
öncülünü ilan eden bir geçidin arkasına taşındı.**

### Ölçülen sayı: bu makine hiç sessiz değil

`OlcumYukAltindaYalnizAgirlasiyor` dört koşum, dördü de yeşil
(4 dk 23 sn / 4 dk 8 sn / 3 dk 56 sn / 3 dk 14 sn). Ölçüm günlüğünden boş
okumaların hepsi eşiğin (`HeavyLoadCores = 1.0`) üstünde:

| koşum | boş okumalar (gerçek zaman çekirdeği) | yüklü | kırmızıya uzaklık |
|---|---|---|---|
| 1 | 3,381 · 3,358 · 3,075 | 2,610 | taban×0,8 = 2,460 → **%6,1** |
| 2 | 3,011 · 2,509 · 2,270 | 3,837 | 1,816 → %111 |
| 3 | 2,305 · 2,458 · 3,108 | 2,220 | 1,844 → %20 |
| 4 | 1,806 · 2,289 · 1,909 | 2,072 | 1,445 → %43 |

Bu dört koşumda on iki boş okumanın on ikisi de `SoftwareHeavyLoad`. Yani
"yükün etkisi" bir tahmin değil: ölçüldüğü anda paralel ajanlar altında boş
taban yoktu ve kaldırılan iddianın öncülü her koşumda sağlanıyordu — kırmızıyı
belirleyen tek şey yüklü okumanın o anda nereye düştüğüydü.

**Bu cümle sonraki ölçümle sınırlandı.** Beşinci bir koşum grubunda okumalar
eşiğin *dibine* indi (1,023 / 1,052 / 0,916) ve biri eşiğin altına geçti. Yani
"bu makinede boş taban hiç yok" yanlış olurdu; doğrusu: **taban ajan sayısıyla
değişiyor ve eşiğin iki yanına da düşebiliyor.** Bir sonraki bölüm bunun
doğurduğu ikinci kusuru anlatıyor.

### İkinci kırmızı: uyum bandı eşiği kesiyor (satır 450)

Beş koşumluk kanıt turunda 4 yeşil 1 kırmızı çıktı — ama **T0'ın işaret ettiği
satırdan değil.** Düşen assert 450. satırdı:

```
ayni sessiz makinede art arda alinan okumalar farkli karar verdi:
SoftwareHeavyLoad/1.023  SoftwareHeavyLoad/1.052  SoftwareLightLoad/0.916
```

Mekanizma ayrı ve satır 458'inkiyle karıştırılmamalı: `TabanUyumBandi = 1,25`
okumaların **sayıca** anlaştığını tanımlıyordu (0,916 × 1,25 = 1,145; üçü de
bandın içinde), sonra bu okumaların aynı **karar sınıfına** düşmesini istiyordu.
Karar sınıfı eşiğin kesikli bir fonksiyonu: eşik 1,0 tam bu üç sayının arasından
geçiyor. Sayıca %13 anlaşan iki okuma farklı sınıfa düşebilir. İddia, okumaların
eşikten **uzak** olmasını gerektiriyordu — ölçünün denetleyemediği bir makine
özelliği.

**Düzeltme eşiği gevşetmedi, iddiayı değiştirdi.** `TabanUyumBandi` silindi;
yerine gelen iddia eşiğin kendisini sınıyor ve yükten bağımsız:

```csharp
foreach (var okuma in olculen)
{
    var beklenen = okuma.SoftwareRealtimeCores >= PerformanceCheck.HeavyLoadCores
        ? RecordingImpact.SoftwareHeavyLoad
        : RecordingImpact.SoftwareLightLoad;
    Assert.True(beklenen == okuma.Impact, ...);
}
```

Söylediği şey: **sınıflandırıcı canlı veri üzerinde de saf.** Donanım yokken
(`FakeAvailability`) `PerformanceCheck` kararı yalnız `sw.RealtimeCores >=
HeavyLoadCores` karşılaştırmasından üretir (`PerformanceCheck.cs:309-310`);
ölçü artık tam olarak bunu doğruluyor. Makinenin o an ne kadar meşgul olduğunu
sormuyor, dolayısıyla ajan sayısıyla değişmiyor.

### Yeni geçit: `QuietMachineFactAttribute`

`FrameGrabberTests.cs` içinde, donanım geçidinin yanında. Sorduğu soru ikili
değil, ölçülen: **bir kere** boş okuma alır (`PerformanceProbe.RunAsync` ile,
kodlayıcıları kapalı sahte `NoEncoders` üzerinden, süreç başına önbellekli) ve
üç halden birini yazar:

| durum | `Skip` metni |
|---|---|
| ffmpeg yok | `<arac> bulunamadi, bos makine olculeri kosturulmadi.` |
| okuma alınamadı | `makinenin bos okumasi alinamadi (yazilim bacagi olculemedi), bos makine iddiasi kurulmadi.` |
| makine dolu | `makine olcum oncesi bos degil: yazilim bacagi <n> gercek zaman cekirdegi istedi, esik 1. Bos taban yok, yuk iddiasi kurulmadi.` |

Donanım geçidinden farkı, T0'ın işaret ettiği yer: "GPU var mı" ikili bir
sorudur, "makine yüklü mü" değildir. Bu yüzden geçit eşiği kendisi uydurmaz,
ürünün kendi eşiğini (`PerformanceCheck.HeavyLoadCores`) ve ürünün kendi
sınıflandırmasını (`RecordingImpact`) kullanır.

### Geçidin taşıdığı yeni iddia

`YukAltindaKararHafiflemiyorMu` — geçit boş tabanı garanti ettiği için iddia
artık kurulabilir: kendi yarattığımız yük (mantıksal çekirdek eksi bir iş
parçacığı) ölçümde **görünmek zorunda**. Tek assert, aynı koşumdaki iki okumayı
birbirine göre okur, mutlak süreye bakmaz:

```
yuklu.SoftwareRealtimeCores > bos.SoftwareRealtimeCores
```

Geçit olmasaydı bu fark ters dönebilirdi ve **ölçüldü**: dolu bir makinede
taban 1,177 iken yüklü okuma 1,041 çıktı — dışarıdan gelen yük kendi yükümüzün
işaretini yuttu. Geçidin işi tam olarak bunu engellemek.

**İkinci bir assert yazıldı, ölçüldü ve geri çekildi.** İlk hali ayrıca
`yuklu.Impact != SoftwareLightLoad` istiyordu — yani on beş iş parçacığının
kararı eşiğin (1,0) üstüne taşıması. Bu ürünün değil **makinenin** özelliği:
boş okuması 0,573 olan hızlı bir anda aynı on beş iş parçacığı okumayı yalnız
0,868'e taşıdı, eşiği hiç geçmedi. Assert kalsaydı hiçbir ürün kusuru olmadan
kırmızıya dönerdi. Karar sınıfı ile sayının tutarlılığı zaten
`OlcumYukAltindaYalnizAgirlasiyor` içinde her canlı okuma için ayrı ayrı
sınanıyor (yukarıdaki 450 düzeltmesi), yani kapsam kaybı yok — iddia yer
değiştirdi.

`OlcumYukAltindaYalnizAgirlasiyor`'da kalan yön iddiası
(`yuklu >= taban × 0,8`) değiştirilmedi; o zaten orana kurulu ve dört koşumda
yeşil.

### Kanıt: aynı yük altında beş koşum

`--filter "OlcumYukAltindaYalnizAgirlasiyor|YukAltindaKararHafiflemiyorMu"`,
`--no-build` (ikili kaynaklardan yeni: `VidShrink.Tests.dll` 08:47:43,
en son kaynak dokunuşu 08:39:11).

| koşum | sonuç | süre |
|---|---|---|
| 1 | `Başarısız 0, Başarılı 2, Atlanan 0` | 1 dk 41 sn |
| 2 | `Başarısız 0, Başarılı 1, Atlanan 1` | 2 dk 10 sn |
| 3 | `Başarısız 0, Başarılı 1, Atlanan 1` | 1 dk 13 sn |
| 4 | `Başarısız 0, Başarılı 2, Atlanan 0` | 4 dk 10 sn |
| 5 | `Başarısız 0, Başarılı 1, Atlanan 1` | 2 dk 36 sn |

### İkinci kanıt turu: assert geri çekildikten sonra beş koşum

| koşum | sonuç | süre | `[bos-makine]` günlüğü |
|---|---|---|---|
| 1 | `Başarısız 0, Başarılı 2, Atlanan 0` | 1 dk 16 sn | 0,835 → 1,14 (`Heavy`) |
| 2 | `Başarısız 0, Başarılı 2, Atlanan 0` | 1 dk 24 sn | 0,581 → 0,972 (`Light`) |
| 3 | `Başarısız 0, Başarılı 1, Atlanan 1` | 2 dk 6 sn | geçit kapalı |
| 4 | `Başarısız 0, Başarılı 1, Atlanan 1` | 1 dk 14 sn | geçit kapalı |
| 5 | `Başarısız 0, Başarılı 2, Atlanan 0` | 1 dk 31 sn | 0,671 → 1,211 (`Heavy`) |

Beş koşum, sıfır kırmızı; iddia bu turda **üç kez** sınandı ve üçünde de yükü
gördü (fark +0,305, +0,391, +0,540 gerçek zaman çekirdeği).

**Geri çekilen assert'in doğru geri çekildiği burada ölçüldü.** Koşum 2 ve
koşum 4'te (0,581 → 0,972 ve 0,656 → 0,847) yüklü okuma eşiğin **altında**
kaldı, yani `Impact` `SoftwareLightLoad` çıktı. İlk yazdığım
`yuklu.Impact != SoftwareLightLoad` assert'i dursaydı bu iki koşum, ürün
tarafında hiçbir kusur olmadan **kırmızı** olurdu. Yani o assert kaldırılmadan
önce ölçü hâlâ kararsızdı — yalnızca kararsızlığın yönü değişmişti.

**Beş koşum, sıfır kırmızı.** Düzeltme öncesi aynı filtre aynı makinede
1 kırmızı / 4 yeşil vermişti (satır 450, yukarıda). Yeşil/atlanan dağılımının
koşumdan koşuma değişmesi kararsızlık değil, geçidin **ilan ettiği** şey:
makine boşsa ölçü koşar, doluysa sayılabilir biçimde atlanır. Kırmızı ile
yeşil arasında salınım yok.

**Bu beş koşumda yeni ölçünün iddiası hiç çalışmadı — ölçüldü.** Üç koşumda
`[QuietMachineFact]` geçidi kapattı; iki koşumda geçit açıldı ama koşum anında
alınan taban artık boş değildi ve ölçü `Atlandi` ile döndü. Ölçüm günlüğü:

```
[bos-makine] yukleyici=15 esik=1 | bos: SoftwareHeavyLoad/1.177 | yuklu: SoftwareHeavyLoad/1.041
[bos-makine] yukleyici=15 esik=1 | bos: SoftwareHeavyLoad/1.177 | yuklu: SoftwareHeavyLoad/2.219
```

Bu, yukarıda "ayrılmadı" diye yazılan **kayma penceresinin ölçülmüş hali**:
geçit keşif anında `SoftwareLightLoad` gördü, ölçü koştuğunda taban 1,177'ye
çıkmıştı.

**Sözleşmenin `verify` koşumunda iddia sonunda çalıştı ve geçti.**
`--filter "PerformanceCheckTests|FrameGrabberTests"`, `Başarısız 0, Başarılı 43,
Atlanan 0, Toplam 43`, 2 dk 45 sn. Günlük:

```
[bos-makine] yukleyici=15 esik=1 | bos: SoftwareLightLoad/0.835 | yuklu: SoftwareHeavyLoad/1.14
```

Öncül kuruldu (0,835 < 1,0), yük görüldü (1,14 > 0,835), assert geçti. Yani
iddia **bir kez sınandı**; kaç koşumda sınanacağı makinenin o anki haline bağlı
ve bu **ölçünün kendi tasarımı**, kusuru değil.

### Mutasyon C: yeni assert canlı mı

Yerine konan sınıflandırıcı iddiasının ölü olmadığı ölçüldü. Beklenen sınıf
eşleştirmesi ters çevrildi (`Heavy` ↔ `Light`), tam yeniden derleme, tek ölçü:

| mutasyon | sonuç | süre |
|---|---|---|
| yok | `Başarısız 0, Başarılı 1` (yukarıdaki beş koşumun her birinde) | — |
| C: sınıf eşleştirmesi ters | `Başarısız 1, Başarılı 0` — `canli okuma 2.446 icin karar SoftwareHeavyLoad, esik 1 ile SoftwareLightLoad olmaliydi` | 5 dk 24 sn |

Mutasyon geri alındı, kaynak yeniden derlendi (`0 Hata`).

`YukAltindaKararHafiflemiyorMu` için mutasyon **yapılmadı**: iddiasının
çalışıp çalışmayacağı makinenin o anki yüküne bağlı olduğu için mutasyonun
kırmızı vermemesi ile iddianın ölü olması ayırt edilemezdi. Bunun yerine
iddianın canlılığı doğrudan ölçüldü — `verify` koşumunda çalıştı ve sayıları
günlüğe yazdı (yukarıda).

### Geçidin ayırdığı ve ayırmadığı eksenler

**Ayırdığı** (ölçü koşmadan önce sınanan, atlandığında sayılabilen):

| eksen | geçit | nasıl sorulur |
|---|---|---|
| ffmpeg/ffprobe PATH'te var mı | `FfmpegFact` | `ToolLocator.IsAvailable` |
| tonemap süzgeci bu derlemede var mı | `TonemapFact` | süzgeç listesi + gerçek deneme |
| donanım kodlayıcı **derlemede** var mı | `HardwareEncoderFact` | `EncoderCapabilities.HasEncoder` |
| donanım kodlayıcı **bu makinede açılıyor mu** | `HardwareEncoderFact` | `EncoderCapabilities.Probe` (1 kare gerçek kodlama) |
| makine ölçüm öncesi boş mu | `QuietMachineFact` | `PerformanceProbe` boş okuma + `RecordingImpact` |

**Ayırmadığı** (bugün hâlâ varsayım):

- **Geçit ile koşum arasındaki kayma.** `Skip` keşif anında hesaplanır, ölçü
  dakikalar sonra koşar. Makine arada dolabilir. `YukAltindaKararHafiflemiyorMu`
  bunu koşum içinde tekrar okuyup `Atlandi` ile bildirir — ama `Atlandi` özette
  görünmez (aşağıdaki kusur). Kayma **ölçüldü ve gerçek**: iki koşumda geçit
  `SoftwareLightLoad` görüp açtı, ölçü koştuğunda taban 1,177'ye çıkmıştı.
  Pencerenin ne kadar sürede ne kadar kaydığı ölçülmedi, yalnız kaydığı ölçüldü.
- **Süreç dışı paralellik.** `DisableTestParallelization` tek süreç içinde
  sıralar; aynı makinede koşan on dört ajanı sıralamaz. Geçit bunu ölçer ama
  engelleyemez.
- **Çekirdek sayısı.** Bu makine 16 mantıksal çekirdek, GitHub runner'ı değil.
  Hiçbir geçit "kaç çekirdek" sormuyor; eşik mutlak (`1.0`), çekirdek sayısına
  göre ölçeklenmiyor. Ölçüler bugün geçiyor, **ölçeklenme sınanmadı**.
- **Disk ve bellek baskısı.** Ölçülerin hiçbiri G/Ç doygunluğunu ayırmıyor.
- **Sürücü sürümü.** `Probe` "açıldı mı" sorusunu yanıtlar, "hangi sürücüyle"
  sorusunu değil. Farklı NVENC nesillerinin sayıları karşılaştırılabilir mi,
  **ölçülmedi**.
- **Termal/güç durumu.** Uzun süitte işlemci frekansı düşerse ölçü bunu yük
  sanar. Ayrılmadı.

### `-MaximumSkipped 30` marjı

`tools/kosum-kapisi/kosum-kapisi.ps1:20`. **Bu sözleşmede değiştirilmedi**,
satır T115'in.

| aşama | CI'da atlanan | CI'da toplam | ölçüm |
|---|---|---|---|
| T115 sonrası, T117 öncesi | 17 | 1180 | koşum `33593652976` (`045648e`) |
| `HardwareEncoderFact` sonrası | 18 | 1181 | koşum `33591434219` (`58e2d45`) |
| `QuietMachineFact` sonrası | **19** | **1182** | koşum `33595878496` (`297ba7b`) |
| satır 450 düzeltmesinden sonra | **19** | **1182** | koşum `33599862478` (`45ca92c`) |
| assert geri çekildikten sonra | **19** | **1182** | koşum `33602685158` (`9f25f08`) |

Kapı çıktısı, dalın son koşumu (`9f25f08`, 15 dk 54 sn):
`KOŞUM KAPISI GEÇTİ: başarısız=0 toplam=1182 alt-sınır=1134 atlanan=19 ust-sinir=30`
(`Failed: 0, Passed: 1163, Skipped: 19, Total: 1182`)

**Tavan hâlâ anlamlı, değişiklik önerilmiyor.** İki geçit toplam +2 atlanan
getirdi (17 → 19), tavana 11 pay kaldı. Tavanın işi kütlesel susturmayı
yakalamak: T115 öncesi bu sayı **95**'ti, yani tavan aşılınca ne olduğunu
depo zaten yaşadı. 19/1182 = %1,6; bir sözleşme daha bu ölçekte geçit eklerse
(+1, +2) tavan yine tutar, on bir ölçü birden susturulursa tutmaz — istenen
davranış bu. Alt sınır (`-MinimumTotal 1134`) da tarafımdan artırılmadı;
toplam 1180'den 1182'ye çıktı, sınır 48 pay altında kalmaya devam ediyor.

`QuietMachineFact`'in CI'da neden atladığı **ölçülmedi**: sebep `Skip` alanında
duruyor ama konsola basılmıyor (bkz. K2 — aynı kusurun ikinci örneği). GitHub
runner'ının çekirdek sayısı düşük olduğu için boş okumanın eşiği aşması
beklenir, ama bu bir **tahmindir**, ölçüm değil.

## K8 — üçüncü eksen: T62'nin kökü burada değil

T0 tur ortasında koşullu sahiplik verdi: T62'nin bulduğu kararsızlık kökü
`TempCleanup` / `AppHost` içinde duruyor, "yalnızca kovaladığın kararsızlık
oraya çıkarsa yaz". Çıkmadı. Ölçüm:

**Yol düzeltmesi.** Verilen yollar (`src/VidShrink.App/TempCleanup.cs`,
`src/VidShrink.App/AppHost.cs`) hiçbir dalda ve `9b37fc5`'te **yok**. Gerçek
yerleri `src/VidShrink.Ffmpeg/TempCleanup.cs` (147 satır) ve
`tests/VidShrink.Tests/AppHost.cs` (100 satır).

**Bağlantı ölçüldü, yok:**

| soru | ölçüm | sonuç |
|---|---|---|
| `PerformanceCheckTests` bu dosyalara dokunuyor mu | `grep -nE "AppHost\|TempCleanup\|Avalonia"` | **0 satır** |
| `PerformanceProbe` / `PerformanceCheck` `TempCleanup` çağırıyor mu | aynı grep | 0 çağrı (yalnız bir açıklama satırında adı geçiyor, `PerformanceProbe.cs:303`) |
| `CleanupStaleArtifacts`'i kim çağırıyor | depo geneli grep | yalnız `src/VidShrink.App/App.axaml.cs:27` (uygulama açılışı) ve `TempCleanupTests` (kendi geçici dizinini vererek, `%TEMP%`'i değil) |

Üstelik `TempCleanup.DeleteMatching` `Directory.EnumerateFiles` kullanıyor —
**dosya** siler, dizin silmez. Ölçülerin ürettiği `vidshrink_*` artıkları dizin;
silme yolu onlara zaten uğramıyor.

`AppHost`'un çözdüğü kararsızlık ayrı bir eksen ve kendi belgesinde açık:
Avalonia arayüz iş parçacığının hangi ölçüm sınıfına düştüğü ("Call from
invalid thread"). `PerformanceCheckTests` Avalonia'ya hiç dokunmuyor.

**Sonuç: bu iki dosyaya yazılmadı.** Kovaladığım kararsızlığın kökü ölçüldü ve
başka yerde: paralel ajanların işlemci tüketimi (§K7). T62'nin gözlemi
yanlışlanmadı — yalnızca **benim eksenimin sebebi o değil.** Kim ölçerse
ölçsün, üç gözlem üç ayrı sebeple açıklanmalı:

| # | gözlem | ölçülen sebep | sahibi |
|---|---|---|---|
| 1 | CI'da belirlenimci kırmızı | `nvcuda.dll` yok, runner'da NVIDIA aygıtı yok | geçit T117'de, kök T123'te |
| 2 | bu makinede 1/5 kırmızı | paralel ajanların işlemci yükü, boş taban yok (12/12 okuma eşik üstü) | T117 (§K7) |
| 3 | süit iki koşumda farklı | **T117'de ölçülmedi** — T62'nin gözlemi, benim eksenimle bağlantısı ölçüldü ve yok | açık |

## Ölçülmeyenler

- `YukAltindaKararHafiflemiyorMu` **bir kez** gerçekten sınandı (`verify`
  koşumu, 0,835 → 1,14). Kaç koşumda sınandığı ölçülmedi; beş koşumluk turda
  sıfır, `verify` turunda bir. Mutasyon kanıtı bu yüzden üretilemedi.
- `Atlandi` yolunun sayılabilir hale getirilmesi **yapılamadı**: xUnit 2 koşum
  sırasında test atlayamıyor, `Skip` yalnız keşif anında yazılabiliyor. Bugün
  bu yol "geçti" olarak sayılıyor; sebebi ölçüm günlüğüne ve test çıktısına
  yazılıyor ama özet sayısında görünmüyor.
- T62'nin gözlemi ("süit iki koşumda farklı sonuç") T117'de **yeniden
  üretilmedi**; yalnız benim eksenimle bağlantısının olmadığı ölçüldü (§K8).

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
