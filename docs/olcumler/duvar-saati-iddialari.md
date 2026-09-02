# Duvar saatine bağlı iddialar

T127 `SplitDragTests`in duvar saatini kapattıktan sonra denetçinin işaretlediği beş
iddia, artı ölçüm sırasında `main`i kırmış olan altıncısı. Taban `2ff3d3d`.

Cevapların dağılımı: **(a) hiçbiri, (b) üç iddia, (c) bir iddia**; beşincisi eski
bandıyla (c) idi, daraltıldıktan sonra (b) oldu. K0 ayrı: sayaç enjekte edilmedi,
yarışın kendisi kapatıldı.

Bu üç dosyada assert edilen duvar saati bantlarının **toplamı beş değil sekiz.**
Sınıflanan beşi aşağıda; kalan üçü (`UpdaterTests.cs:915`, `:916`, `:1141`) tur 1'in
eksik tarama deseni yüzünden gözden kaçtı ve üstüne "başka yok" yazıldı. O cümle geri
çekildi, üç bant K5'te listelendi; bu turda düzeltilmeleri istenmedi.

## K0 — `ComplexityProbeTests.cs:63`, bugün düşen iddia

Koşum `33627045645`, commit `f33c13e`: `1 pencere olculdu`.

**En olası sebep (i), sayaç yarışı; (iii) elenmiş değil.** Üç aday da ölçüldü:

| aday | ölçüm | sonuç |
|---|---|---|
| (ii) pencere sayısı ortama bağlı | `ComplexityProbe.ProductionPlan` = `SamplingPlan.Fixed`; bu dalda `secondBits` hiç hesaplanmıyor ve `PlanWindows` `FixedWindows`e düşüyor, 8 sn klip → 2 pencere. 45 koşumda ölçüldü (25 yüklü/16 çekirdek + 20 iki çekirdeğe sabitlenmiş): `plan=2`, 45/45. CI ffmpeg'i zaten yereldekiyle aynı sürüme (GyanD 9.0) sabitli. | **elendi** |
| (iii) bir pencere ölçülmeden geçildi | Aynı 45 koşumda `result.QualityMeasurements.Count=2`, 45/45. Bir pencere yedek yola düşseydi bu sayı da 1'e inerdi. | **gözlenmedi** |
| (i) `Calls++` atomik değil | `Task.WhenAll` ile iki görev, iki `Calls++`, aralarında **bilerek hizalanmış** gecikme (`Spin(jitter)` / `Spin(40-jitter)`) 200.000 kez koşuldu: tur 1'de 156 kayıp artış (%0,078), tur 2'de 105 (%0,0525), ikisi de 16 çekirdekte. İki çekirdeğe sabitlendiğinde 200.000 denemede 0 kayıp. | **mekanizma var** |

Plan her koşumda 2 ve 45 koşumda iki pencere de ölçüldüğü halde sayacın 1 okunması
kaybolan bir artışla açıklanıyor. Düzenek `.calisma/t132/yaris/`, ham çıktı
`.calisma/t132/yaris/ham-cikti.txt`, tanı verisi
`.calisma/test-ciktilari/t132/tani.csv`.

Tur 2'de düzenek yeniden koşuldu, ham çıktı:

```
# tur 2 yeniden kosum, 2026-09-02 18:40:51, makine yuklu
deneme=200000 yuk=3 cekirdek=16 kayip=105 oran=%0,0525 sure=471ms
```

**Ölçülen oran gerçek testin oranı değil.** Düzenek iki artışı `Spin` ile bilerek aynı
ana denk getiriyor; yani yarışın **var olduğunu** kanıtlar, sıklığını değil. Gerçek
testte iki artışın arasında ayrı ayrı ffmpeg kodlamaları var, iki `Calls++`ın
çakışma olasılığı %0,078'in çok altında. Bu yüzden bu satır "sebep budur" değil,
"mekanizma vardır ve gözlenen düşüşü açıklayabilir" der; ayrıştırmanın kalanı
aşağıdaki (iii) paragrafına bağlı.

**(iii) elenmiş değil.** 45 yerel koşum, CI'daki **tek** düşüşe karşı (iii)'ü eleyecek
güçte değil: yerelde hiç görülmemesi CI'da görülmediği anlamına gelmiyor. Denetim
şunu gösterdi: `ComplexityProbe.SampleWindowAsync` yedek yolu yavaş koşucuda
`SampleTimeout` ile tetiklendiğinde tam olarak `Calls=1` üretir — yani (iii)'ün
gerçekten bir mekanizması var ve gözlenen düşüşün şekli ikisinde de aynı. O yol
**T141** olarak ayrı sözleşmeye açıldı. Sebep ataması bu yüzden **kesin değil**:
(i) ölçülmüş bir mekanizma, (iii) ölçülmemiş ama gösterilmiş bir mekanizma; ikisi de
`Calls=1` üretir ve bugünkü veriyle ayrılamıyorlar. Yapılan düzeltme (i)'i kapatır,
(iii)'ü T141 kapatacak.

`FakeMeter` sayacı `Interlocked.Increment` / `Volatile.Read` oldu — aynı dosyadaki
`CancellingMeter` bunu zaten böyle yapıyordu. İddia `>= 2` yerine `== 2`: 8 sn klip
2 sn'lik iki pencereye bölünüyor.

| mutasyon | sonuç |
|---|---|
| `ComplexityProbe.Windows`: kısa klipte pencere sayısı 2 → 1 | **kırıldı** — `1 pencere olculdu` |
| `SplitSampleAsync`: ölçer yalnız `start > 2.0` penceresinde çağrılıyor, yani (iii)'ün şekli | **kırıldı** — `1 pencere olculdu` |

Düzeltmeden sonra tam süit beş kez koşuldu, beşi de yeşil:

| koşum | süre | sonuç |
|---|---|---|
| 1 | 11 m 26 s | Başarısız 0, Başarılı 1328, Atlanan 17, Toplam 1345 |
| 2 | 11 m 28 s | aynı |
| 3 | 14 m 13 s | aynı |
| 4 | 12 m 5 s | aynı |
| 5 | 19 m 51 s | aynı |

Ham kayıt `.calisma/t132/k03-tam-suit.txt`. Dal koşumu `33637321742`, sonuç `success`.

**Nihai kanıt bu beş tam süit koşumu ile CI koşumudur.** Tur 1'in raporunda anılan
yerel `verify` sonucu (`Toplam 104, Başarısız 0`) için saklanmış bir kayıt yoktu;
`.calisma/t132/verify-final.log` adını taşıyan dosya taban sıfırlamasından **önceki**
tura ait (`Toplam 73, Başarısız 1`, düşen test `DonanimYoluKapatilincaKararDegisiyor`,
duvar saati değil). O dosya adı yalan söylüyordu; adı düzeltilip
`.calisma/t132/onceki-tur/sifirlama-oncesi-verify.log` yoluna taşındı. Tur 2'nin
`verify` koşumu `.calisma/t132/verify-tur2.log` olarak saklandı ve şöyle bitiyor:

```
Başarılı!  - Başarısız: 0, Başarılı: 101, Atlanan: 3, Toplam: 104, Süre: 2 m 59 s
```

**Ölü ham veri ağaçtan çıkarıldı.** `.calisma/t132/claim1.csv` ve `claim2.csv` taban
sıfırlamasından önceki tura aitti ve K2 tablosuyla uyuşmuyordu (claim1 boş dilim
1500-1516, tabloda 1500-1500; claim2 geniş bütçe 10848-27666, tabloda 6714-7236).
Silinmediler, `.calisma/t132/onceki-tur/` altına taşındılar:

```
$ ls .calisma/t132/onceki-tur/
claim1.csv                    400 B   11:02
claim2.csv                    830 B   11:22
sifirlama-oncesi-verify.log  10821 B  12:03
```

Bu turun geçerli ham verisi `.calisma/test-ciktilari/t132/` altında
(`iddia1.csv`, `iddia2.csv`, `iddia4.csv`, `iddia5.csv`, `tani.csv`).

## K1 — Beş iddianın sınıflaması

| # | yer | iddia | ne tutmak istiyor | cevap | gerekçe |
|---|---|---|---|---|---|
| 1 | `PerformanceCheckTests.cs:757` | `Assert.InRange(saat.ElapsedMilliseconds, 1500, 5_000)` | `CalibrateCpuClock(1500)` gerçekten 1500 ms yakıyor mu | (b) | Yakan iş parçacığı kendi `Stopwatch`ına bakıyor; saat üretim kodunun içinde ve bu sözleşme üretimi değiştirmiyor. Bant ölçülen dağılımdan daraltıldı. |
| 2 | `PerformanceCheckTests.cs:618` | `Assert.True(basladi.ElapsedMilliseconds < genisSaat.ElapsedMilliseconds / 2, ...)` | dar bütçe (900 ms) geniş bütçeye (60.000 ms) göre gerçekten erken kesiyor mu | (b) | İki taraf da gerçek ffmpeg geçişleri koşuyor, süre gerçek süreç süresi. Docstring bu satırı kendisi "ikincil ve bilerek gevşek" ilan ediyor; asıl kanıt üstündeki iki `Assert.False`. Bant değişmedi. |
| 3 | `PerformanceCheckTests.cs:854` | `Assert.True(clock.ElapsedMilliseconds > 0, ...)` | ffmpeg geçişi gerçekten koştu mu | (c) | **Bu satır ölçmüyor.** Bir satır üstündeki `Assert.True(p.ExitCode == 0, ...)` sürecin koştuğunu zaten kanıtlıyor; `> 0` ondan sonra hiçbir şey eklemiyor. Kırabilecek gerçekçi bir mutasyon yok: `Stopwatch` çözünürlüğünün altında biten bir geçiş çıkış kodu da veremezdi. |
| 4 | `UpdaterTests.cs:316` | `Assert.True(stopwatch.Elapsed < ceiling, ...)`, `ceiling = ManifestTimeout + 250 ms` | ağsız açılış `ManifestTimeout` içinde vazgeçiyor mu | (b) | `HttpClient.Timeout` gerçek bir ağ isteğini kesiyor, enjekte edilebilir saat yok. Bant ölçülen dağılımdan daraltıldı. |
| 5 | `UpdaterTests.cs:1173` | `Assert.True(stopwatch.Elapsed > TimeSpan.FromSeconds(5), ...)` | silme adımı geri çekilme turlarını gerçekten koşuyor mu | (b) | `Install-VidShrink.ps1`in gerçek `Start-Sleep` çağrıları, ayrı bir powershell süreci. Eski tabanıyla (1 sn) hiçbir şey tutmuyordu — o haliyle (c) idi; ölçülen dağılıma göre daraltılınca mutasyonu yakalamaya başladı (K3). |

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
| K0 `ComplexityProbeTests.cs:63` | pencere sayısı 2 → 1 | — | **kırıldı** |
| K0 `ComplexityProbeTests.cs:63` | ölçer bir pencerede hiç çağrılmıyor | — | **kırıldı** |
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

### Tur 1'in taraması eksikti, üstüne yokluk beyanı yazılmıştı

Tur 1'de koşulan komut şuydu:

```
grep -n "Thread\.Sleep\|Task\.Delay\|Stopwatch\|DateTime\.Now\|DateTimeOffset\.Now\|ElapsedMilliseconds\|\.Elapsed\b"
```

Desende `TimeSpan.From`, `DateTime.UtcNow` ve `.Join(` yoktu. Bu yüzden üç assert
edilmiş bant hiç görünmedi ve bölümün altına şu cümle yazıldı:

> ~~"Beşin dışında **assert edilen** yeni bir duvar saati bandı bulunmadı."~~

**Bu cümle geri çekildi; yanlıştı.** Yerine geçen cümle bölümün sonunda.

### Düzeltilmiş tarama

```
grep -n -E -f desen.txt PerformanceCheckTests.cs UpdaterTests.cs ComplexityProbeTests.cs
```

`desen.txt`, satır başına bir desen:

```
Thread\.Sleep
Task\.Delay
Stopwatch
DateTime\.Now
DateTimeOffset\.Now
DateTime\.UtcNow
DateTimeOffset\.UtcNow
ElapsedMilliseconds
\.Elapsed\b
TimeSpan\.From
\.Join\(
\.WaitOne\(
Timeout
```

Ham çıktı (63 satır) aşağıda; yerel kopyaları `.calisma/t132/k5-tarama.txt` ve
`.calisma/t132/k5-desen.txt` (`.calisma/` git'e girmiyor, o yüzden çıktı belgeye
gömülü). `string.Join(` eşleşmeleri metin biçimleme, zamanla ilgisi yok; aşağıdaki
sayımın dışında.

```
PerformanceCheckTests.cs:152:        Assert.True(eksik.Length == 0, "hic uretilmeyen bulgu kodu: " + string.Join(", ", eksik));
PerformanceCheckTests.cs:335:                string.Join(",", result.Findings.Select(f => f.Code)));
PerformanceCheckTests.cs:426:            $"bos okumalar: {string.Join(" ", okumalar.Select(r => $"{r.Impact}/{N(r.SoftwareRealtimeCores)}/olculdu={r.SoftwareMeasured}/butce={r.BudgetExhausted}"))} | " +
PerformanceCheckTests.cs:433:                "yazilim bacagi butce dolmadan olculemedi: " + string.Join(",", eksik.Findings.Select(f => f.Code)));
PerformanceCheckTests.cs:438:                    string.Join(" ", okumalar.Select(r => r.ElapsedMs + "ms")));
PerformanceCheckTests.cs:536:                string.Join(",", yuklu.Findings.Select(f => f.Code)));
PerformanceCheckTests.cs:556:        var kodlar = string.Join(",", sonuc.Findings.Select(f => f.Code));
PerformanceCheckTests.cs:594:        var tabanSaat = System.Diagnostics.Stopwatch.StartNew();
PerformanceCheckTests.cs:598:        var genisSaat = System.Diagnostics.Stopwatch.StartNew();
PerformanceCheckTests.cs:602:        var basladi = System.Diagnostics.Stopwatch.StartNew();
PerformanceCheckTests.cs:606:        var bulgular = string.Join(",", result.Findings.Select(f => f.Code));
PerformanceCheckTests.cs:607:        Log($"[butce] sinir={dar}ms gecen={basladi.ElapsedMilliseconds}ms " +
PerformanceCheckTests.cs:608:            $"gecissiz taban gecen={tabanSaat.ElapsedMilliseconds}ms " +
PerformanceCheckTests.cs:609:            $"genis={YukOlcumButcesiMs}ms gecen={genisSaat.ElapsedMilliseconds}ms " +
PerformanceCheckTests.cs:618:            Assert.True(basladi.ElapsedMilliseconds < genisSaat.ElapsedMilliseconds / 2,
PerformanceCheckTests.cs:619:                $"butce baglamadi: {dar}ms butceyle {basladi.ElapsedMilliseconds}ms, " +
PerformanceCheckTests.cs:620:                $"genis butceyle {genisSaat.ElapsedMilliseconds}ms");
PerformanceCheckTests.cs:622:            Atlandi($"genis butceyle de hicbir bacak olculemedi ({string.Join(",", genis.Findings.Select(f => f.Code))}), " +
PerformanceCheckTests.cs:735:        var saat = System.Diagnostics.Stopwatch.StartNew();
PerformanceCheckTests.cs:739:            $"yakim-duvar={saat.ElapsedMilliseconds}ms (1 = saglam sayac, 0 = olculemedi)");
PerformanceCheckTests.cs:757:        if (OperatingSystem.IsWindows()) Assert.InRange(saat.ElapsedMilliseconds, 1500, 5_000);
PerformanceCheckTests.cs:838:        var clock = System.Diagnostics.Stopwatch.StartNew();
PerformanceCheckTests.cs:850:        Log($"[sayac] {etiket} cikis={p.ExitCode} cpu={N(cpu)}ms duvar={clock.ElapsedMilliseconds}ms " +
PerformanceCheckTests.cs:851:            $"cpu/duvar={N(clock.ElapsedMilliseconds > 0 ? cpu / clock.ElapsedMilliseconds : 0)}");
PerformanceCheckTests.cs:854:        Assert.True(clock.ElapsedMilliseconds > 0, $"{etiket} olculebilir bir sure kosmadi");
UpdaterTests.cs:127:        var manifest = new ReleaseManifest("1.1.0", "abc", DateTimeOffset.UtcNow, "win-x64", new[]
UpdaterTests.cs:147:        var manifest = new ReleaseManifest("1.1.0", "abc", DateTimeOffset.UtcNow, "win-x64",
UpdaterTests.cs:299:    public async Task AnUnreachableSourceIsGivenUpWithinTheManifestTimeout()
UpdaterTests.cs:302:        var ceiling = UpdateCheck.ManifestTimeout + TimeSpan.FromMilliseconds(250);
UpdaterTests.cs:306:        var stopwatch = Stopwatch.StartNew();
UpdaterTests.cs:311:            $"ağsız manifest denemesi: {stopwatch.Elapsed.TotalMilliseconds:F0} ms " +
UpdaterTests.cs:312:            $"(zaman aşımı {UpdateCheck.ManifestTimeout.TotalMilliseconds:F0} ms, " +
UpdaterTests.cs:316:        Assert.True(stopwatch.Elapsed < ceiling,
UpdaterTests.cs:317:            $"ağsız açılış zaman aşımını aştı: {stopwatch.Elapsed.TotalMilliseconds:F0} ms > {ceiling.TotalMilliseconds:F0} ms");
UpdaterTests.cs:325:        var manifest = new ReleaseManifest("1.4.0", "abc", DateTimeOffset.UtcNow, "win-x64", new[]
UpdaterTests.cs:653:        var withoutLauncher = new ReleaseManifest("0.2.2", "abc", DateTimeOffset.UtcNow, "win-x64",
UpdaterTests.cs:709:        var manifest = new ReleaseManifest("0.2.2", "abc", DateTimeOffset.UtcNow, "win-x64", new[] { appFile })
UpdaterTests.cs:751:            $"{step}. adımdan sonra kurulum kökünde {LauncherUpdate.ExecutableName} yok; kökte duranlar: {string.Join(", ", names)}");
UpdaterTests.cs:760:        string.Join(", ", Directory.GetFiles(root).Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal));
UpdaterTests.cs:781:                Thread.Sleep(25);
UpdaterTests.cs:796:        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
UpdaterTests.cs:798:        while (rounds < minimumRounds || (untilCaught && misses == 0 && DateTime.UtcNow < deadline))
UpdaterTests.cs:821:                    Thread.Sleep(25);
UpdaterTests.cs:853:            _thread.Join();
UpdaterTests.cs:901:    public void EveryLaunchChecksAndStaysWithinTheTimeout()
UpdaterTests.cs:915:        Assert.True(offlineFirst < TimeSpan.FromSeconds(3), $"ağsız açılış çok uzun: {offlineFirst}");
UpdaterTests.cs:916:        Assert.True(offlineSecond < TimeSpan.FromSeconds(3), $"ağsız ikinci açılış çok uzun: {offlineSecond}");
UpdaterTests.cs:947:        var stopwatch = Stopwatch.StartNew();
UpdaterTests.cs:956:        return stopwatch.Elapsed;
UpdaterTests.cs:1057:        var routine = string.Join("\n", knobs) + "\n\n"
UpdaterTests.cs:1126:            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
UpdaterTests.cs:1127:            while (DateTime.UtcNow < deadline)
UpdaterTests.cs:1131:                Thread.Sleep(5);
UpdaterTests.cs:1138:        var stopwatch = Stopwatch.StartNew();
UpdaterTests.cs:1141:        Assert.True(release.Join(TimeSpan.FromSeconds(10)), "kilidi bırakan iş parçacığı bitmedi");
UpdaterTests.cs:1143:        _output.WriteLine($"geçici kilit: çıkış {code}, {stopwatch.Elapsed.TotalMilliseconds:F0} ms");
UpdaterTests.cs:1161:        var stopwatch = Stopwatch.StartNew();
UpdaterTests.cs:1165:        _output.WriteLine($"kalıcı kilit: çıkış {code}, {stopwatch.Elapsed.TotalMilliseconds:F0} ms");
UpdaterTests.cs:1173:        Assert.True(stopwatch.Elapsed > TimeSpan.FromSeconds(5), $"geri çekilme adımları koşmadı: {stopwatch.Elapsed}");
UpdaterTests.cs:1242:        var manifest = new ReleaseManifest("0.2.2", "abc", DateTimeOffset.UtcNow, "win-x64",
UpdaterTests.cs:1277:        var manifest = new ReleaseManifest("0.2.2", "abc", DateTimeOffset.UtcNow, "win-x64", new[] { appFile })
UpdaterTests.cs:1293:        var entries = string.Join(",", files.Select(file =>
ComplexityProbeTests.cs:48:            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
```

### Assert edilen duvar saati bantları — sekiz

| # | yer | bant | durum |
|---|---|---|---|
| 1 | `PerformanceCheckTests.cs:618` | `basladi < genisSaat / 2` | iddia 2, kapsamda |
| 2 | `PerformanceCheckTests.cs:757` | `InRange(1500, 5_000)` | iddia 1, kapsamda |
| 3 | `PerformanceCheckTests.cs:854` | `> 0` | iddia 3, kapsamda |
| 4 | `UpdaterTests.cs:316` | `< ManifestTimeout + 250 ms` | iddia 4, kapsamda |
| 5 | `UpdaterTests.cs:1173` | `> 5 s` | iddia 5, kapsamda |
| 6 | `UpdaterTests.cs:915` | `offlineFirst < 3 s` | **tur 1'de listelenmedi** |
| 7 | `UpdaterTests.cs:916` | `offlineSecond < 3 s` | **tur 1'de listelenmedi** |
| 8 | `UpdaterTests.cs:1141` | `release.Join(10 s)` | **tur 1'de listelenmedi** |

Denetçinin sayımı sekiz; kendi taramam da sekiz buluyor, fazlası çıkmadı.

**`:915` / `:916` — `EveryLaunchChecksAndStaysWithinTheTimeout`.** Gerçek bir süreç
açılışına (`MeasureLaunch` → `Process.Start` + `WaitForExit`) konmuş **3 saniyelik
sabit tavan**. Ölçülen dağılımdan türetilmemiş; sözleşmenin aradığı "yük altında
düşecek yüzey" tarifinin tam örneği ve daraltılan iddia 4'ten (1050 ms) daha
kırılgan, çünkü tavan bir süreç açılışının tamamını kapsıyor. İki satır
`[LiveLauncherFact]` altında: `VIDSHRINK_LAUNCHER_EXE` var olan bir dosyayı
göstermiyorsa test atlanıyor, CI iş akışı bu değişkeni kurmuyor — yani bantlar
bugün CI'da hiç değerlendirilmiyor, ama değişkeni kuran her yerel koşumda
değerlendiriliyor. **Bu turda düzeltilmedi; listelenmesi istendi.**

**`:1141` — `TheDeletionStepWaitsOutATransientLock`.** `release.Join(TimeSpan.FromSeconds(10))`
gerçek bir iş parçacığının bitmesine konmuş 10 sn'lik tavan; `[Fact]`, Windows'ta
koşuyor. **Bu turda düzeltilmedi; listelenmesi istendi.**

### Assert edilmeyen zaman kullanımları

- `PerformanceCheckTests.cs:594,598,602,735,838` — iddiaların kendi saatleri.
- `PerformanceCheckTests.cs:607-609,739,850-851` — yalnız `Log`.
- `UpdaterTests.cs:311,1143,1165` — yalnız `_output.WriteLine`.
- `UpdaterTests.cs:947,956` — `MeasureLaunch` süreyi döndürüyor; çağıran `:938-939`
  onu yalnız günlüğe yazıyor (`:915/:916` ise assert ediyor, yukarıda).
- `UpdaterTests.cs:781,821,1131` — `Thread.Sleep(25)` / `Thread.Sleep(5)`, sınırlı
  yoklama gecikmeleri; üzerlerinde bant iddiası yok.
- `UpdaterTests.cs:796` (`DateTime.UtcNow + 20 s`), `:1126` (`+30 s`) — döngü son
  tarihleri; süre dolduğunda döngü sessizce çıkıyor, assert yok. `:1126`'nın
  çıkışını `:1141`'in 10 sn'lik `Join`i yakalıyor.
- `UpdaterTests.cs:853` — `_thread.Join()`, süresiz; bant değil.
- `UpdaterTests.cs:127,147,325,653,709,1242,1277` — `DateTimeOffset.UtcNow` manifest
  kurgusunda; zaman iddiası yok.
- `ComplexityProbeTests.cs:48` — `Task.Delay(Timeout.InfiniteTimeSpan, ct)`, iptal
  testinde bilerek sonsuz bekleme.

### Üç dosyanın dışı — K5'in kapsamı değil, yine de bakıldı

Aynı desen `tests/VidShrink.Tests/*.cs` üzerinde koşuldu. Bu sözleşmenin `owns`
kümesi dışında olduğu için ölçülmedi, sınıflanmadı, düzeltilmedi; ileride bakan
olsun diye yazılıyor:

- `EncoderCapabilitiesTests.cs:312,341` — `Assert.True(probeStarted.Wait(TimeSpan.FromSeconds(5)), ...)`,
  gerçek bir yoklamaya konmuş 5 sn'lik tavan.
- `ShareProviderTests.cs:261` — `Assert.InRange((ExpiresAt - UtcNow).TotalMinutes, 175, 181)`,
  hesaplanan bir sona erme anına konmuş ±3 dk'lık pencere.
- `ComparisonPanelTests.cs:322,692`, `ShareProviderTests.cs:462`, `SplashTests.cs:149`
  — sabit `TimeSpan` karşılaştırmaları; duvar saati değil.

**Bu sözleşmenin üç dosyasında assert edilen duvar saati bantlarının sayısı sekiz.
Beşi kapsamdaydı, üçü tur 1'de gözden kaçtı ve bu bölümde listelendi. Tur 1'in
"başka yok" cümlesi geri çekildi.**

## Üretimde görülen, düzeltilmeyen

`ComplexityProbe.SampleWindowAsync` (`:735`) ölçeri yalnız `SplitSampleAsync`in hızlı
yolunda çağırıyor. O yol `null` dönerse — ffmpeg sıfır dışı çıkış, sıfır kare ya da
sıfır bayt — yedek yol iki ayrı `SampleAsync` koşuyor ve **o pencere sessizce ölçümsüz
kalıyor**; kalite ölçümü eksik çıkıyor, kimse bildirmiyor. **T141** olarak ayrı
sözleşmeye açıldı; bu turda üretim kodu değişmedi.

Bu kusur aynı zamanda K0'ın (iii) adayının mekanizmasıdır: yedek yol `SampleTimeout`
ile tetiklendiğinde iki pencereden biri ölçülmeden kalır ve sayaç tam olarak `1`
okunur — CI'da görülen düşüşün şekli. 45 yerel koşumda bir kez olmadı, ama 45 koşum
CI'daki tek düşüşe karşı bunu **elemeye yetmez**. K0'ın sebep ataması bu yüzden
kesin diye sunulmuyor.
