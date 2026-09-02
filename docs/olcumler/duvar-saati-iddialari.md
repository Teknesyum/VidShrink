# Duvar saatine bağlı iddialar

T127 `SplitDragTests`in duvar saatini kapattıktan sonra denetçinin işaretlediği beş
iddia, artı ölçüm sırasında `main`i kırmış olan altıncısı. Taban `2ff3d3d`.

Cevapların dağılımı: **(a) hiçbiri, (b) üç iddia, (c) bir iddia**; beşincisi eski
bandıyla (c) idi, daraltıldıktan sonra (b) oldu. K0 ayrı: sayaç enjekte edilmedi,
yarışın kendisi kapatıldı.

## K0 — `ComplexityProbeTests.cs:61`, bugün düşen iddia

Koşum `33627045645`, commit `f33c13e`: `1 pencere olculdu`.

**Sebep: (i) sayaç yarışı.** Üç aday da ölçüldü:

| aday | ölçüm | sonuç |
|---|---|---|
| (ii) pencere sayısı ortama bağlı | `ComplexityProbe.ProductionPlan` = `SamplingPlan.Fixed`; bu dalda `secondBits` hiç hesaplanmıyor ve `PlanWindows` `FixedWindows`e düşüyor, 8 sn klip → 2 pencere. 45 koşumda ölçüldü (25 yüklü/16 çekirdek + 20 iki çekirdeğe sabitlenmiş): `plan=2`, 45/45. CI ffmpeg'i zaten yereldekiyle aynı sürüme (GyanD 9.0) sabitli. | **elendi** |
| (iii) bir pencere ölçülmeden geçildi | Aynı 45 koşumda `result.QualityMeasurements.Count=2`, 45/45. Bir pencere yedek yola düşseydi bu sayı da 1'e inerdi. | **gözlenmedi** |
| (i) `Calls++` atomik değil | Aynı eşzamanlılık deseni (`Task.WhenAll` ile iki görev, aralarında rastgele gecikme) 200.000 kez koşuldu: **156 kayıp artış, %0,078** (16 çekirdek). İki çekirdeğe sabitlendiğinde 200.000 denemede 0 kayıp — iki artış aynı çekirdekte sıraya giriyor. | **sebep bu** |

Plan her koşumda 2 ve iki pencere de gerçekten ölçüldüğü halde sayacın 1 okunması
ancak kaybolan bir artışla mümkün. Düzenek `.calisma/t132/yaris/`, ham veri
`.calisma/test-ciktilari/t132/tani.csv`.

`FakeMeter` sayacı `Interlocked.Increment` / `Volatile.Read` oldu — aynı dosyadaki
`CancellingMeter` bunu zaten böyle yapıyordu. İddia `>= 2` yerine `== 2`: 8 sn klip
2 sn'lik iki pencereye bölünüyor.

| mutasyon | sonuç |
|---|---|
| `ComplexityProbe.Windows`: kısa klipte pencere sayısı 2 → 1 | **kırıldı** — `1 pencere olculdu` |
| `SplitSampleAsync`: ölçer yalnız `start > 2.0` penceresinde çağrılıyor, yani (iii)'ün şekli | **kırıldı** — `1 pencere olculdu` |

## K1 — Beş iddianın sınıflaması

| # | yer | ne tutmak istiyor | cevap | gerekçe |
|---|---|---|---|---|
| 1 | `PerformanceCheckTests.cs:757` | `CalibrateCpuClock(1500)` gerçekten 1500 ms yakıyor mu | (b) | Yakan iş parçacığı kendi `Stopwatch`ına bakıyor; saat üretim kodunun içinde ve bu sözleşme üretimi değiştirmiyor. Bant ölçülen dağılımdan daraltıldı. |
| 2 | `PerformanceCheckTests.cs:618` | dar bütçe (900 ms) geniş bütçeye (60.000 ms) göre gerçekten erken kesiyor mu | (b) | İki taraf da gerçek ffmpeg geçişleri koşuyor, süre gerçek süreç süresi. Docstring bu satırı kendisi "ikincil ve bilerek gevşek" ilan ediyor; asıl kanıt üstündeki iki `Assert.False`. Bant değişmedi. |
| 3 | `PerformanceCheckTests.cs:854` | ffmpeg geçişi gerçekten koştu mu | (c) | **Bu satır ölçmüyor.** Bir satır üstündeki `Assert.True(p.ExitCode == 0, ...)` sürecin koştuğunu zaten kanıtlıyor; `> 0` ondan sonra hiçbir şey eklemiyor. Kırabilecek gerçekçi bir mutasyon yok: `Stopwatch` çözünürlüğünün altında biten bir geçiş çıkış kodu da veremezdi. |
| 4 | `UpdaterTests.cs:316` | ağsız açılış `ManifestTimeout` içinde vazgeçiyor mu | (b) | `HttpClient.Timeout` gerçek bir ağ isteğini kesiyor, enjekte edilebilir saat yok. Bant ölçülen dağılımdan daraltıldı. |
| 5 | `UpdaterTests.cs:1173` | silme adımı geri çekilme turlarını gerçekten koşuyor mu | (b) | `Install-VidShrink.ps1`in gerçek `Start-Sleep` çağrıları, ayrı bir powershell süreci. Eski tabanıyla (1 sn) hiçbir şey tutmuyordu — o haliyle (c) idi; ölçülen dağılıma göre daraltılınca mutasyonu yakalamaya başladı (K3). |

## K2 — Ölçülen dağılım

Her iddia 40 kez koşuldu: 20 boş makinede, 20 tanesi `ProcessorCount-1` = 15 işçilik
yapay CPU yükü altında. Boş koşumdan önce makine doğrulandı: canlı ffmpeg süreci yok,
CPU %0-2. Ham veri `.calisma/test-ciktilari/t132/iddia{1,2,4,5}.csv`.

| iddia | durum | n | min | max | ortanca | p95 |
|---|---|---|---|---|---|---|
| 1 — `saat.ElapsedMilliseconds` (ms) | boş | 20 | 1500 | 1500 | 1500 | 1500 |
| 1 | yüklü | 20 | 1503 | 1573 | 1517 | 1551 |
| 2 — dar bütçe (ms) | boş | 20 | 934 | 963 | 946,5 | 959 |
| 2 — dar bütçe | yüklü | 20 | 967 | 2262 | 1065,5 | 1260 |
| 2 — geniş bütçe (ms) | boş | 20 | 6714 | 7236 | 6899 | 7179 |
| 2 — geniş bütçe | yüklü | 20 | 9651 | 12787 | 9926,5 | 12591 |
| 2 — oran (dar/geniş) | boş | 20 | 0,131 | 0,140 | 0,137 | 0,139 |
| 2 — oran | yüklü | 20 | 0,077 | 0,229 | 0,102 | 0,128 |
| 4 — `stopwatch.Elapsed` (ms) | boş | 20 | 800 | 815 | 803,5 | 814 |
| 4 | yüklü | 20 | 804 | 837 | 813 | 828 |
| 5 — `stopwatch.Elapsed` (ms) | boş | 20 | 7189 | 7399 | 7272,5 | 7343 |
| 5 | yüklü | 20 | 7715 | 10848 | 7880 | 9604 |

Eski bantların bu dağılımın neresine düştüğü:

- **İddia 1**, eski `[1500, 15_000]`: gözlenen en yüksek 1573 ms. Bandın 13.500 ms'lik
  üst payının kullanılan kısmı **73 ms**, yani %0,5. Tavana hiç yaklaşılmıyor.
- **İddia 2**, eşik `oran < 0,5`: gözlenen en kötü oran 0,229 — eşiğin yarısından az.
- **İddia 4**, eski tavan `800 + 500 = 1300 ms`: gözlenen en yüksek 837 ms. Zaman
  aşımının üstünde kullanılan pay en fazla **37 ms**, verilen pay 500 ms.
- **İddia 5**, eski taban `1000 ms`: gözlenen en düşük 7189 ms, tabanın **7 katı**.
  Taban hiçbir şey tutmuyordu; K3 bunu mutasyonla gösteriyor.

## K3 — Mutasyon ızgarası

Mutasyonlar üretim dosyalarına geçici uygulandı, her biri
`dotnet build -c Release --no-incremental` ile derlendi, ölçü `--filter` ile koşuldu
(her filtre kolunun `Toplam: 1` ile tek test eşlediği doğrulandı) ve dosya
`git checkout --` ile geri alındı. Teslim edilen dalda üretim kodu değişmedi.

| ölçü | mutasyon | eski bantla | yeni bantla |
|---|---|---|---|
| K0 `ComplexityProbeTests.cs:61` | pencere sayısı 2 → 1 | — | **kırıldı** |
| K0 `ComplexityProbeTests.cs:61` | ölçer bir pencerede hiç çağrılmıyor | — | **kırıldı** |
| 1 `PerformanceCheckTests.cs:757` | `CalibrateCpuClock` yakımı `durationMs / 100` | **kırıldı** (15 ms) | **kırıldı** (15 ms) |
| 4 `UpdaterTests.cs:316` | `HttpClient.Timeout` 800 → 3000 ms, `ManifestTimeout` sabiti yerinde | **kırıldı** (3006 > 1300) | **kırıldı** (3006 > 1050) |
| 5 `UpdaterTests.cs:1173` | `Install-VidShrink.ps1`: `RemoveAttempts` 6 → 5, yani 3200 ms'lik son geri çekilme turu düşüyor | **yaşadı** (4147 ms > 1000 ms) | **kırıldı** (4147 ms < 5000 ms) |

İlk denemede iddia 4 için `ManifestTimeout` sabitinin kendisi mutasyona uğratılmıştı;
ölçü tavanını aynı sabitten türettiği için mutasyon **kendini gizledi** — tavan da
3500 ms'ye çıktı ve ölçü yeşil kaldı. Mutasyon zaman aşımının kullanıldığı yere
taşındı.

İddia 2 ve 3 için mutasyon koşulmadı; ikisinin de bandı değişmedi. İddia 2'yi
bağımsız kıracak bir mutasyon da aranmadı: bütçeyi bozan her mutasyon üstündeki iki
`Assert.False`u önce kırıyor, satır 618 onların gölgesinde kalıyor.

## K4 — Bant değişiklikleri ve yönü

| # | eski | yeni | yön | gerekçe |
|---|---|---|---|---|
| 1 | `[1500, 15_000]` ms | `[1500, 5_000]` ms | **daraldı** | Gözlenen en yüksek 1573 ms (yüklü). Yeni tavan, gözlenen aşımın (73 ms) 48 katı pay bırakıyor; CI koşucusu bu makineden yavaş olduğu için pay bilerek geniş tutuldu. |
| 2 | oran `< 0,5` | değişmedi | **değişmedi** | Gözlenen en kötü oran 0,229. Daraltmak yanlış-kırmızı riskini artırır, genişletmenin gerekçesi yok. |
| 3 | `> 0` | değişmedi | **değişmedi** | (c) — satır bir şey ölçmüyor, bu belge onu kayda geçiriyor. Silinmedi: `ExitCode` iddiasının yanında günlüğe giren süreyi okunur tutuyor. |
| 4 | `ManifestTimeout + 500` (1300 ms) | `ManifestTimeout + 250` (1050 ms) | **daraldı** | Zaman aşımının üstünde gözlenen en büyük pay 37 ms; yeni pay onun 6,8 katı. |
| 5 | `> 1 s` | `> 5 s` | **daraldı** | Gözlenen en düşük 7189 ms (boş), 7715 ms (yüklü). Bir tur eksik geri çekilme 4147 ms sürüyor; 5 sn tam ikisinin arasına düşüyor, yani taban artık gerçekten geri çekilme turlarını tutuyor. Gözlenen minimuma %30 pay kalıyor. |

Hiçbir bant genişletilmedi, hiçbir zaman aşımı artırılmadı, `[Skip]` eklenmedi.

## K5 — Sınıf taraması

Komut, üç dosyada koşuldu (`PerformanceCheckTests.cs`, `UpdaterTests.cs`,
`ComplexityProbeTests.cs`):

```
grep -n "Thread\.Sleep\|Task\.Delay\|Stopwatch\|DateTime\.Now\|DateTimeOffset\.Now\|ElapsedMilliseconds\|\.Elapsed\b"
```

Beş iddianın dışında bulunan satırlar:

- `PerformanceCheckTests.cs:594,598,602` — iddia 2'nin kendi saatleri
  (`tabanSaat`, `genisSaat`, `basladi`); zaten kapsamda.
- `PerformanceCheckTests.cs:735,838` — iddia 1 ve 3'ün saatleri; kapsamda.
- `PerformanceCheckTests.cs:607-609,739,850-851` — yalnız `Log`, üzerlerinde iddia yok.
- `UpdaterTests.cs:311,1143` — yalnız `_output.WriteLine`.
- `UpdaterTests.cs:947,956` — `MeasureLaunch` ölçtüğü süreyi döndürüyor, ama çağıran
  yer (`:912-914`, `:938-939`) onu yalnız günlüğe yazıyor; iddialar istek sayısında.
- `UpdaterTests.cs:781,821,1131` — `Thread.Sleep(25)` / `Thread.Sleep(5)`; sınırlı
  deneme sayacına bağlı yoklama gecikmeleri, üzerlerinde bant iddiası yok.
- `ComplexityProbeTests.cs:46` — `Task.Delay(Timeout.InfiniteTimeSpan, ct)`, iptal
  testinde bilerek sonsuz bekleme; bant değil.

Beşin dışında **assert edilen** yeni bir duvar saati bandı bulunmadı.

## Üretimde görülen, düzeltilmeyen

`ComplexityProbe.SampleWindowAsync` (`:735`) ölçeri yalnız `SplitSampleAsync`in hızlı
yolunda çağırıyor. O yol `null` dönerse — ffmpeg sıfır dışı çıkış, sıfır kare ya da
sıfır bayt — yedek yol iki ayrı `SampleAsync` koşuyor ve **o pencere sessizce ölçümsüz
kalıyor**; kalite ölçümü eksik çıkıyor, kimse bildirmiyor. 45 koşumda bir kez olmadı,
bu yüzden K0'ın sebebi değil. Kapsam dışı: ayrı sözleşme konusu.
