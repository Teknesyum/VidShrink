# T131 — İptal ulaşmıyor mu, kurulum mu boğuluyor

**Tarih:** 02.09.2026 · **Sözleşme:** `.claude/relay/contracts/T131.md`

Sonuç: **(b) doğru — kurulum boğuluyor. (a) ölçümle çürütüldü.**
`ComplexityProbe.RunDetailedAsync` iptali yutmuyor; `cts.Cancel()` ile
`OperationCanceledException` arasında geçen süre yük altında bile 0,1–8,8 s.
31 saniyenin tamamı `cts.Cancel()` çağrılmadan **önce** geçiyor.

## Ortam

| Alan | Değer |
|---|---|
| Makine | Windows 11 Pro 22631, 16 mantıksal çekirdek |
| ffmpeg | WinGet `yt-dlp.FFmpeg`, N-125875-g5d4d3bdc61-win64-gpl |
| .NET | 8.0, `dotnet test -c Release` (`--no-build` kullanılmadı) |
| Süit koşumu | `[assembly: CollectionBehavior(DisableTestParallelization = true)]` — süit **seri** koşuyor |
| Yapay yük | 32 adet dönen `Math.Sqrt` ipliği (16 çekirdek, 2× fazla abonelik) |
| Klip | `testsrc2=size=320x240:rate=12:duration=8`, libx264, yuv420p |

Ölçüm düzeneği geçiciydi: `ComplexityProbeTests` içine eklenen enstrümanlı iki test
(`TempOlcum`, `TempAyirt`) sayıları üretip kaldırıldı. Yapay yük şöyle üretildi:

```powershell
$jobs = @()
for ($i = 0; $i -lt 32; $i++) {
  $jobs += Start-Process powershell.exe -PassThru -WindowStyle Hidden `
    -ArgumentList "-NoProfile","-Command","`$x=0.0; while(`$true){ `$x=[Math]::Sqrt(`$x+1.0) }"
}
Start-Sleep -Seconds 3
dotnet test -c Release tests\VidShrink.Tests\VidShrink.Tests.csproj --filter <ad>
foreach ($j in $jobs) { Stop-Process -Id $j.Id -Force }
```

## K1 — İki sayı ayrı ayrı

`klip_kurulum`: `WithClipAsync`'in ürettiği 8 saniyelik klibin kodlanması
(`ComplexityProbeTests.cs:172-183`).
`olcere_giris`: `RunDetailedAsync` çağrısından sahte ölçerin ilk kez çalıştırılmasına
kadar geçen süre — sondanın **kendi** ffmpeg pencere kodlamaları. Testte bu aralığı
10 saniyelik `WaitAsync` koruyordu.
`iptal_ulasma`: `cts.Cancel()` ile istisnanın gözlenmesi arası.

### Boş makine (5 koşum)

| Koşum | klip_kurulum | olcere_giris | iptal_ulasma | Sonuç |
|---|---|---|---|---|
| 1 | 348 ms | 599 ms | 3 ms | OperationCanceledException |
| 2 | 103 ms | 320 ms | 104 ms | OperationCanceledException |
| 3 | 139 ms | 640 ms | 40 ms | OperationCanceledException |
| 4 | 121 ms | 338 ms | 1 ms | OperationCanceledException |
| 5 | 162 ms | 414 ms | 37 ms | OperationCanceledException |

### 32 iplik yapay yük altında (5 koşum)

| Koşum | klip_kurulum | olcere_giris | iptal_ulasma | Sonuç |
|---|---|---|---|---|
| 1 | 8.685 ms | 21.856 ms | 93 ms | OperationCanceledException |
| 2 | 8.196 ms | 21.801 ms | 205 ms | OperationCanceledException |
| 3 | 8.997 ms | 23.363 ms | 427 ms | OperationCanceledException |
| 4 | 8.303 ms | 20.099 ms | 212 ms | OperationCanceledException |
| 5 | 7.584 ms | 19.056 ms | 229 ms | OperationCanceledException |

Toplam ≈ 29–32 s. T115'in bildirdiği 31 s bu toplamla örtüşüyor.
Dağılım: yaklaşık **8 s klip kurulumu + 21 s sondanın kendi pencere kodlaması + 0,2 s
iptal**. İptalin payı toplamın binde 7'si.

**T115 koşumunun kendi dağılımı ölçülmedi** — T115 yalnız toplam süreyi bildirmiş,
hangi satırın attığı kayıtlı değil. Yukarıdaki dağılım bu makinede yeniden üretilen
koşumun dağılımıdır.

### Daha ucuz klip yardım etmiyor

`testsrc2=size=128x128:rate=6:duration=8` (piksel hacmi 1/7,5) ile aynı yük altında:

| Koşum | klip_kurulum | olcere_giris | iptal_ulasma |
|---|---|---|---|
| 1 | 8.834 ms | 26.102 ms | 355 ms |
| 2 | 9.110 ms | 28.489 ms | 385 ms |
| 3 | 9.444 ms | 21.416 ms | 435 ms |
| 4 | 10.996 ms | 20.938 ms | 226 ms |
| 5 | 8.378 ms | 20.916 ms | 247 ms |

Süre düşmedi. Darboğaz kodlanan piksel değil, süreç başlatma ve iş parçacığı
zamanlaması. **Kurulumu küçülterek duvar saati bağımlılığı kurtarılamaz** —
bağımlılığın kendisini kaldırmak gerekti (K3/K4).

## (a)'yı çürüten denetim koşumu

T115 denetçisinin bağımsız gözlemi şunu öne sürdü: 10 saniyelik sınır yalnız
`Entered`'i koruyor, `Cancel()` sonrası sınır yok, dolayısıyla ~20 s `Cancel()`
**sonrasına** düşüyor ve istisnayı iptal değil ffmpeg'in doğal bitişi atıyor.

Bu okuma iki ölçümle çürütüldü.

**1. Sıra ölçümü.** `olcere_giris` `Cancel()`'dan öncedir; enstrümantasyon iki aralığı
ayrı saydı. 20 saniye `Cancel()`'dan **önce** geçiyor, sonra değil.

**2. Denetim koşumu (iptalsiz).** Ölçer bloke olduktan sonra token **hiç iptal
edilmezse** `RunDetailedAsync` doğal olarak bitebiliyor mu? Yük altında üç koşum:

| Koşum | Ölçere girdikten sonra 30 s içinde bitti mi? |
|---|---|
| 1 | Hayır (30.071 ms bekledi, hâlâ çalışıyordu) |
| 2 | Hayır (30.070 ms) |
| 3 | Hayır (30.102 ms) |

Bitmiyor ve bitemez: `BlockingMeter` `Task.Delay(Timeout.InfiniteTimeSpan, ct)` ile
sonsuza kadar bekler, `RunDetailedAsync` da `Task.WhenAll(pending)` ile onu bekler.
İptal olmadan tek çıkış `SampleTimeout` (90 s). Dolayısıyla `Cancel()`'dan 92–8.785 ms
sonra gelen istisna doğal bitişten gelemez; iptal yolundan gelir.

**Kullanılamayan sütun:** aynı koşumda `Process.GetProcessesByName("ffmpeg")` ile
süreç sayımı da alındı, ama sayaç makinedeki **tüm** ffmpeg süreçlerini görüyor —
o sırada başka worktree'lerin koşumları da vardı (bir koşumda 18 süreç, hepsinin
bitmesi 120 s'lik sınırda kesildi). Bu sütun kirli; kanıt olarak kullanılmadı,
karar denetim koşumuna dayanıyor.

## K2 — Uygulanmadı

(a) doğru çıkmadığı için `RunDetailedAsync`'te yutan `catch` yok; düzeltilecek ürün
kusuru bulunamadı. İptal yolu iki yerde korunuyor:

- `src/VidShrink.Ffmpeg/ComplexityProbe.cs:477` — ölçer çağrısını saran
  `catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }`
- `src/VidShrink.Ffmpeg/ComplexityProbe.cs:101` — `RunDetailedAsync`'in dış
  `catch`inden önceki aynı yeniden fırlatma

## K3/K4 — Ölçü duvar saatinden koparıldı

Eski ölçü iptali arka planda tetikliyordu ve ölçerin çalışmaya başlamasını
`WaitAsync(TimeSpan.FromSeconds(10))` ile bekliyordu. Kabul, o 10 saniyeye —
yani kurulumun hızına — bağlıydı.

Yeni ölçüde iptali **ölçerin kendisi** tetikliyor: `CancellingMeter` çağrıldığı anda
`source.CancelAsync()` çağırıyor, sonra token üstünde bloke oluyor. Kurulum 0,5 s de
sürse 60 s de sürse kabul değişmiyor; test yalnız "ölçere ulaşıldı mı" ve
"iptal `RunDetailedAsync`'ten `OperationCanceledException` olarak çıktı mı" sorularını
soruyor. **Testte hiçbir süre sınırı, `Task.Delay` ya da `Stopwatch` kalmadı.**
`meter.Calls > 0` iddiası, ölçere hiç uğramadan sessizce geçen bir koşumun yeşil
görünmesini engelliyor.

Kurulum böylece ölçümün dışına çıktı: klip üretimi testin **süresini** hâlâ belirliyor
ama **sonucunu** belirlemiyor.

## K5 — Mutasyon

Her koşum tam derlemeyle (`--no-build` yok).

| # | Mutasyon | Beklenen | Ölçü | Süre |
|---|---|---|---|---|
| M1 | `ComplexityProbe.cs:477` içteki yeniden fırlatma silindi (ölçerin iptali `catch { quality = null; }` tarafından yutulur) | kırmızı | **yeşil — yakalanmadı** | 455 ms |
| M2 | `ComplexityProbe.cs:101` dıştaki yeniden fırlatma silindi (iptal yutulur, taban profil döner) | kırmızı | **kırmızı** | 481 ms |
| M3 | M1 + M2 birlikte | kırmızı | **kırmızı** | 479 ms |
| M4 | Ölçünün kendisi eski haline döndürüldü, 32 iplik yük altında | kırmızı | **kırmızı**, `ComplexityProbeTests.cs:182` — yani `WaitAsync(10 s)` satırı | 17 s |
| — | Düzeltilmiş ölçü, 32 iplik yük altında | yeşil | **yeşil** | 24 s |
| — | Düzeltilmiş ölçü, boş makine | yeşil | **yeşil** | 1 s |

**M4 (b)'nin doğrudan kanıtı:** eski ölçü yük altında iptal iddiasında değil,
kurulumu bekleyen satırda düşüyor.

**M1 yakalanmıyor — bu ölçünün bilinen boşluğu.** İçteki yeniden fırlatma silindiğinde
ölçerin `OperationCanceledException`'ı yutuluyor ve sonda çalışmaya devam ediyor, ama
kısa süre sonra iptal edilmiş token üzerinde başka bir aşama (`MeasureWindowBiasAsync`)
yine `OperationCanceledException` atıyor; dıştaki yeniden fırlatma onu geçiriyor ve
test yeşil kalıyor. İki koruma yedekli. M1'i M2'den ayıran tek gözlenebilir fark
**iptalden sonra ne kadar fazladan iş yapıldığı**, yani süre — K4 gereği ölçüye süre
iddiası koyulmadı. Bu boşluk kapatılmadı, gizlenmedi de: içteki koruma tek başına
**ölçülmüyor**.

## K6 — Sınıfın geri kalanı

`ComplexityProbeTests` taranarak bulunanlar. Hiçbiri bu sözleşmede düzeltilmedi.

| Yer | Kusur |
|---|---|
| `WithClipAsync:172-183` | Her `[FfmpegFact]`/`[FfmpegTheory]` için 8 saniyelik libx264 klibi baştan kodlar. İptal jetonu almaz, zaman sınırı yoktur. Sınıfta 11 koşum var; yük altında her biri ≈8–11 s kurulum ödüyor. Paylaşılan bir fixture bu maliyeti bire indirir. |
| `RunFfmpegAsync:185-194` | `ToolLocator` ffmpeg'ini zaman sınırsız ve iptalsiz bekler. ffmpeg asılırsa test asılır; süiti kesen tek şey koşucunun kendi sınırı olur. |
| `WindowAndMotionSamplesCountTheSameByteUnit:104-105` | `drift < 0.08` eşiği duvar saatine bağlı değil (bayt sayar), ama ampirik ve gerekçesi dosyada yazılı değil. Kodlayıcı sürümü değişirse sessizce kayabilir. |

Duvar saatine bağlı başka kabul kalmadı: sınıfta `TimeSpan`, `Stopwatch`, `Task.Delay`
ve `WaitAsync` kullanımı yalnız `Task.Delay(Timeout.InfiniteTimeSpan, ct)` satırında
sürüyor, o da süre değil iptal beklemek için.

## Sözleşmedeki iki yanlış öncül

1. Sözleşme (b)'yi "tam süit **paralel** koşarken" diye tarifliyor. Süit paralel
   koşmuyor: `LanguageTests.cs:17`'deki
   `[assembly: CollectionBehavior(DisableTestParallelization = true)]` bunu kapatıyor.
   Yavaşlatan şey süit içi paralellik değil, makinenin genel yükü.
2. Sözleşme yükü klip kurulumuna yıkıyor. Kurulum 31 saniyenin ≈8'i; kalan ≈21 saniye
   sondanın **kendi** pencere kodlamaları, yani `RunDetailedAsync`'in içi.
   `WithClipAsync`'i hızlandırmak tek başına yetmezdi.

## `owns` listesindeki yol yanlış

Sözleşme `src/VidShrink.Core/ComplexityProbe.cs` diyor; dosya
`src/VidShrink.Ffmpeg/ComplexityProbe.cs`. `Core` altında böyle bir dosya yok.
Ölçüm ve mutasyonlar `Ffmpeg` altındaki dosya üzerinde yapıldı; sonuçta o dosya
değiştirilmedi.
