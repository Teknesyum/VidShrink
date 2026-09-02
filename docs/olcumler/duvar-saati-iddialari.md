# Duvar saatine bağlı iddialar

T127 `SplitDragTests`in duvar saatini kapattıktan sonra denetçinin işaretlediği beş
iddia, artı ölçüm sırasında `main`i kırmış olan altıncısı. Taban `2ff3d3d`.

Cevapların dağılımı: **(a) hiçbiri, (b) üç iddia, (c) bir iddia**; beşincisi eski
bandıyla (c) idi, daraltıldıktan sonra (b) oldu. K0 ayrı: sayaç enjekte edilmedi,
yarışın kendisi kapatıldı.

Bu üç dosyada duvar saatinden türeyen assert sayısı **yirmi üç**, bunların **on biri**
bant. Sınıflanan beşi aşağıda; kalan altı bant (`UpdaterTests.cs:890`, `:915`, `:916`,
`:1141`, `PerformanceCheckTests.cs:462`, `:542`) tur 1'in ve tur 2'nin tarama deseni
yüzünden gözden kaçtı. Sayı bu turda **metin deseniyle değil türden** çıkarıldı ve bir
ölçüyle koda pimlendi (K5); tur 1'in "başka yok" cümlesiyle tur 2'nin "sekiz" sayımı
geri çekildi. Altı bandın düzeltilmesi bu turda istenmedi.

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

### Daralmaların kendisi pimlendi mi — iki tavan

Yukarıdaki ızgaranın yapısal boşluğu: dört mutasyondan yalnız biri (iddia 5) eski
bandı yaşatıp yeni bandı öldürüyor. İddia 1'in mutasyonu **tabanı** kırıyor,
iddia 4'ünki hem eski hem yeni tavanı aşıyor. Yani `15.000 → 5.000` ve `500 → 250`
daralmalarının **daralma olduğunu** hiçbir ölçü tutmuyordu. İkisi için de "eski
bantta yaşayan, yeni bantta ölen" birer mutasyon arandı; tur 3'te ikisi de bulundu.

| ölçü | mutasyon | eski bant | eski bantla | yeni bant | yeni bantla |
|---|---|---|---|---|---|
| 4 `UpdaterTests.cs:316` | `HttpClient { Timeout = TimeSpan.FromMilliseconds(1150) }`, `ManifestTimeout` sabiti 800'de bırakıldı | 1300 ms | **yaşadı** — 1160 ms | 1050 ms | **kırıldı** — 1159 ms > 1050 ms |
| 1 `PerformanceCheckTests.cs:757` | (a) yakım `durationMs * 5` | 15.000 ms | **yaşadı** — 7508 ms | 5.000 ms | **kırıldı** — 7526 ms > 5.000 ms |
| 1 `PerformanceCheckTests.cs:757` | (b) `burner.Join()` sonrası `Thread.Sleep(6000)` | 15.000 ms | **yaşadı** — 7521 ms | 5.000 ms | **kırıldı** — 7511 ms > 5.000 ms |

Her mutasyon `dotnet build -c Release --no-incremental` ile derlendi; test
koşumlarında `--no-build` kullanılmadı. Kayıtlar `.calisma/t132/t2/` altında ve
`.calisma/` git'e girmediği için karar veren satırlar buraya gömülü:

```
eski-bant.txt:25:  Başarılı VidShrink.Tests.UpdaterTests.AnUnreachableSourceIsGivenUpWithinTheManifestTimeout [2 s]
eski-bant.txt:27:   ağsız manifest denemesi: 1160 ms (zaman aşımı 800 ms, tavan 1300 ms)
iddia4-yeni-bant.txt:27:  Başarısız VidShrink.Tests.UpdaterTests.AnUnreachableSourceIsGivenUpWithinTheManifestTimeout [2 s]
iddia4-yeni-bant.txt:29:   ağsız açılış zaman aşımını aştı: 1159 ms > 1050 ms
t3/a-eski-bant.txt:  Başarılı ...IslemciZamaniSayaciDogruOkuyorMu [2 m 13 s]      <- (a) eski bant, yakim-duvar=7508ms
t3/a-yeni-bant.txt:  Assert.InRange() Failure  Range: (1500 - 5000)  Actual: 7526 <- (a) yeni bant
t3/b-eski-bant.txt:  Başarılı ...IslemciZamaniSayaciDogruOkuyorMu [2 m 19 s]      <- (b) eski bant, yakim-duvar=7521ms
t3/b-yeni-bant.txt:  Assert.InRange() Failure  Range: (1500 - 5000)  Actual: 7511 <- (b) yeni bant
```

**İddia 4 pimlendi.** 1150 ms'lik zaman aşımı eski tavanın altında, yeni tavanın
üstünde kalıyor; daralma artık bir ölçüyle tutuluyor.

**İddia 1 pimlendi.** İki mutasyon da `saat`i ~7500 ms'ye çıkarıyor: eski tavan 15.000
bunu geçiriyor, yeni tavan 5.000 öldürüyor. Daralmanın daralma olduğu artık iki ayrı
mutasyonla tutuluyor. Dört kol da `dotnet build -c Release --no-incremental` ile
derlendi, `--no-build` kullanılmadı; kayıtlar
`.calisma/t132/t3/{a,b}-{eski,yeni}-bant.txt`.

### Tur 2'nin `Atlandi` gerekçesi yanlıştı — geri çekiliyor

Tur 2 bu daralmayı "pimlenemedi" diye kapatmış ve sebebini şuna bağlamıştı: `:757`'den
önceki `if (!guvenilir) Atlandi(...)` bir kapıdır, yüklü makinede kapanır, `:757` hiç
koşmaz. **Bu yanlıştı.** `Atlandi` bir `private void` günlükçüdür
(`PerformanceCheckTests.cs:48-52`): `return` yok, `throw` yok, `Skip` yok; çağrı
yerinde de `return` yok, `:753-757` düz akış. `:757` Windows'ta **her** koşumda
değerlendiriliyor.

Bu turun dört koşumu bunu doğrudan gösteriyor: dördünde de `[atlandi]` satırı basıldı
**ve** `:757` yine de değerlendirildi; iki kolunda kırmızı verdi.

| koşum | okunan katsayı | `[atlandi]` basıldı mı | `:757` değerlendirildi mi |
|---|---|---|---|
| (a) yeni bant | 6,155 | evet | **evet** — kırıldı, 7526 ms |
| (a) eski bant | 16,552 | evet | **evet** — geçti, 7508 ms |
| (b) eski bant | 5,662 | evet | **evet** — geçti, 7521 ms |
| (b) yeni bant | 12,0 | evet | **evet** — kırıldı, 7511 ms |

Tur 2'nin bu yanlış olgudan türettiği iki sonuç geri çekildi ve belgeden **çıkarıldı**;
düzenlenmiş halleri de kalmadı. Ne söylüyorlardı ve yerlerine ne geçti:

- "`:757`'nin bandı yalnız sayacın güvenilir okuduğu koşumlarda değerlendiriliyor"
  → **yanlış.** Windows'ta her koşumda değerlendiriliyor; kapı diye anlatılan şey yok.
- "K2'deki iddia 1 dağılımı kapının önüne konmuş geçici bir ölçümden geliyordu, bandın
  değerlendirildiğinin kanıtı değildir" → **dayanağı düştü.** Kapı olmadığı için o
  dağılım doğrudan bandın değerlendirildiği büyüklüğün dağılımıdır; K2'nin ve K4'ün
  daraltma gerekçesi olduğu gibi duruyor.

Geriye tek gözlem kalıyor: katsayı bu makinede toleransın dışında okunuyor (bu turda
5,662 - 16,552). Sebebi hâlâ **izole edilmedi**; yükle birlikte gözlendi, yükün sebep
olduğu ölçülmedi. Kapı olmadığı ölçüldüğü için bu artık bandı gizleyen bir şey değil,
ayrı bir gözlem.

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

### İki tarama, iki yöntem

Tur 1 "yok" dedi, tur 2 "sekiz" dedi; ikisi de anahtar kelime deseniyle sayıyordu ve
ikisi de eksikti. Anahtar kelime listesi **kapalı bir küme değil**, o yüzden deseni
genişletmek bu hatayı bitirmiyor. Bu turda sayım iki bağımsız yöntemle üretildi;
ikisinin de ham çıktısı aşağıda.

#### Tarama A — genişletilmiş anahtar kelime deseni

Tur 2'nin desenine `WaitForExit(`, `WaitForExitAsync(`, `WaitAsync(` ve `.Wait(`
eklendi. Desen `.calisma/t132/t3/desen-a.txt`, satır başına bir desen:

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
\.Wait\(
WaitForExit\(
WaitForExitAsync\(
WaitAsync\(
Timeout
```

Komut ve ham çıktı — 73 satır, `.calisma/t132/t3/tarama-a.txt`:

```
$ grep -n -E -f desen-a.txt PerformanceCheckTests.cs UpdaterTests.cs ComplexityProbeTests.cs
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
PerformanceCheckTests.cs:843:        p.WaitForExit();
PerformanceCheckTests.cs:850:        Log($"[sayac] {etiket} cikis={p.ExitCode} cpu={N(cpu)}ms duvar={clock.ElapsedMilliseconds}ms " +
PerformanceCheckTests.cs:851:            $"cpu/duvar={N(clock.ElapsedMilliseconds > 0 ? cpu / clock.ElapsedMilliseconds : 0)}");
PerformanceCheckTests.cs:854:        Assert.True(clock.ElapsedMilliseconds > 0, $"{etiket} olculebilir bir sure kosmadi");
PerformanceCheckTests.cs:890:        "Stopwatch", "Elapsed", "WaitForExit", "WaitOne", "WaitAsync", ".Join(", ".Wait("
PerformanceCheckTests.cs:905:        new(@"^ {4}(?:public|private|internal|protected)[^\r\n=;]*?\b(?:TimeSpan|Stopwatch|long|double|int)\s+(\w+)\s*\(",
PerformanceCheckTests.cs:926:            + string.Join(" ", bulunan));
PerformanceCheckTests.cs:1033:        var sade = metin.Replace("string.Join(", " ", StringComparison.Ordinal);
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
UpdaterTests.cs:890:        Assert.True(process.WaitForExit(60_000), "geçiş süreci çıkmadı");
UpdaterTests.cs:901:    public void EveryLaunchChecksAndStaysWithinTheTimeout()
UpdaterTests.cs:915:        Assert.True(offlineFirst < TimeSpan.FromSeconds(3), $"ağsız açılış çok uzun: {offlineFirst}");
UpdaterTests.cs:916:        Assert.True(offlineSecond < TimeSpan.FromSeconds(3), $"ağsız ikinci açılış çok uzun: {offlineSecond}");
UpdaterTests.cs:947:        var stopwatch = Stopwatch.StartNew();
UpdaterTests.cs:949:        process.WaitForExit();
UpdaterTests.cs:956:        return stopwatch.Elapsed;
UpdaterTests.cs:1057:        var routine = string.Join("\n", knobs) + "\n\n"
UpdaterTests.cs:1098:        process.WaitForExit();
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
ComplexityProbeTests.cs:470:        await process.WaitForExitAsync();
ComplexityProbeTests.cs:516:        await process.WaitForExitAsync();
```

`PerformanceCheckTests.cs:890,905,926,1033` bu turda eklenen sayım ölçüsünün kendi
gövdesi — desen kendi tohum listesini eşliyor, iddia değil.

**Tarama A dokuzuncu bandı buldu** (`UpdaterTests.cs:890`, tur 2'de listede yoktu)
**ama iki iddiayı hâlâ bulmuyor:** `PerformanceCheckTests.cs:462` ve `:542`. O iki
satırda `Elapsed`, `Stopwatch`, `TimeSpan`, `Timeout`, `Wait` ya da `Join` geçmiyor;
iddia duvar saatinden **türetilmiş** bir üretim üyesine konmuş: `SoftwareRealtimeCores`,
yani `RealtimeCores => VideoMs <= 0 ? 0 : WallMs / VideoMs`
(`src/VidShrink.Core/PerformanceCheck.cs:37`). Desen ne kadar genişletilirse
genişletilsin bulunamazlar. Tarama A bu yüzden tek başına yeterli değil.

#### Tarama B — türden çıkarılan kapalı küme

Sayım metinden değil **türden** çıkarılıyor. Küme üç kuralla tanımlı:

1. Üretimde saatten türeyen üyeler: `WallMs`, `ElapsedMs`, `RealtimeCores`,
   `RealtimeFactor`, `ReportedCpuParallelism`, artı BCL'nin `Stopwatch` ve `Elapsed`i.
2. Zaman aşımı argümanı alan bekleme çağrıları: `WaitForExit`, `WaitOne`, `WaitAsync`,
   `.Join(`, `.Wait(`.
3. Bunlardan türeyenler: gövdesinde (1) ya da (2) geçen ve `TimeSpan` / `Stopwatch` /
   `long` / `double` / `int` döndüren yardımcı metotların adları, ve sağ tarafında bir
   tohum geçen yerel değişkenler — metot bloğu içinde, sabit noktaya kadar.

Sonra bu kümeye **dokunan** her `Assert` ifadesi listeleniyor. `string.Join(`
eşleşmeleri metin biçimlemedir, sayımdan düşülüyor.

Küme kaynaktan türetilebiliyor, dolayısıyla **sonlanıyor**: anahtar kelime listesi gibi
"bir tane daha eklemeyi akıl et" demiyor. Kural belgede değil,
`PerformanceCheckTests.SaatTureviIddialarinSayisiBelgedekiyleAyni` ölçüsünün içinde kod
olarak duruyor. Ham çıktı ölçünün kendi hata iletisi:

```
saat turevi iddia sayisi 23, belge 24 diyor: PerformanceCheckTests.cs:165
PerformanceCheckTests.cs:221 PerformanceCheckTests.cs:253 PerformanceCheckTests.cs:290
PerformanceCheckTests.cs:302 PerformanceCheckTests.cs:303 PerformanceCheckTests.cs:341
PerformanceCheckTests.cs:450 PerformanceCheckTests.cs:462 PerformanceCheckTests.cs:542
PerformanceCheckTests.cs:566 PerformanceCheckTests.cs:618 PerformanceCheckTests.cs:629
PerformanceCheckTests.cs:646 PerformanceCheckTests.cs:751 PerformanceCheckTests.cs:757
PerformanceCheckTests.cs:854 UpdaterTests.cs:316 UpdaterTests.cs:890 UpdaterTests.cs:915
UpdaterTests.cs:916 UpdaterTests.cs:1141 UpdaterTests.cs:1173
```

Kayıt `.calisma/t132/t3/sayim-mutasyon.txt`; ölçü 23'e eşitlendiğindeki yeşil koşum
`.calisma/t132/t3/sayim-yesil.txt`.

Tarama B, tarama A'nın bulduğu her bandı buluyor **ve** `:462`, `:542`, `:629`u da
getiriyor. Tersi doğru değil: tarama A'nın `Thread.Sleep` / `Task.Delay` satırları
iddia olmadıkları için B'de yok, aşağıdaki "assert edilmeyen" listesinde duruyorlar.

### Saat türevi 23 iddianın tamamı — 11 bant, 12 elenmiş

Tarama B'nin verdiği 23 satırın hepsi burada. Sessiz bırakılan satır yok.

| # | yer | iddia | sınıf | gerekçe |
|---|---|---|---|---|
| 1 | `PerformanceCheckTests.cs:462` | `yuklu.SoftwareRealtimeCores >= taban * YonPayi` | **bant** | `RealtimeCores = WallMs / VideoMs`; canlı okumaya sabit katsayılı (`YonPayi = 0,8`, `:358`) alt sınır. Hiçbir metin deseninin bulamadığı iddia. Bu turda daraltılmadı. |
| 2 | `PerformanceCheckTests.cs:542` | `yuklu.SoftwareRealtimeCores > bos.SoftwareRealtimeCores` | **bant** | İki duvar saati okumasının karşılaştırması; sabit yok ama iki ölçüm de makinenin o anki yüküne bağlı. `[QuietMachineFact]` geçidi altında. Bu turda daraltılmadı. |
| 3 | `PerformanceCheckTests.cs:618` | `basladi.ElapsedMilliseconds < genisSaat.ElapsedMilliseconds / 2` | **bant** | İddia 2; K1'de (b) olarak daraltıldı ve pimlendi. |
| 4 | `PerformanceCheckTests.cs:629` | `butce.WallMs > dar` | elenmiş | Üretimin kendi koruyucusundan totolojik çıkıyor: `:627` bulgunun varlığını şart koşuyor, bulgu ancak `elapsedMs > budgetMs` iken üretiliyor (`PerformanceCheck.cs:237,243-244`), `:628` `budgetMs == dar`ı çiviliyor. Kararsızlaşma yönü yok — makine yavaşladıkça iddia sağlamlaşır. "Hiçbir şey ölçmüyor" da değil: taşıma iddiası, aşağıda mutasyonla gösterildi. |
| 5 | `PerformanceCheckTests.cs:757` | `InRange(saat.ElapsedMilliseconds, 1500, 5_000)` | **bant** | İddia 1; tavan 15.000 → 5.000 daraltıldı, iki mutasyonla pimlendi. |
| 6 | `PerformanceCheckTests.cs:854` | `clock.ElapsedMilliseconds > 0` | **bant** | İddia 3; K1'de (c) — bant biçiminde ama pratikte hiçbir şey ölçmüyor, kayda geçirildi. |
| 7 | `UpdaterTests.cs:316` | `stopwatch.Elapsed < ManifestTimeout + 250 ms` | **bant** | İddia 4; tavan 1300 → 1050 ms daraltıldı, mutasyonla pimlendi. |
| 8 | `UpdaterTests.cs:890` | `process.WaitForExit(60_000)` | **bant** | Gerçek süreç açılışına (`Process.Start`, `:889`) konmuş 60 sn'lik sabit tavan; `TheIncomingBinaryRenamesItselfOntoTheTargetName`, `[LiveLauncherFact]`. Tur 1'de de tur 2'de de listelenmedi — dokuzuncu bant. Bu turda daraltılmadı. |
| 9 | `UpdaterTests.cs:915` | `offlineFirst < TimeSpan.FromSeconds(3)` | **bant** | Gerçek süreç açılışının süresine (`MeasureLaunch`, `:946-957`) konmuş 3 sn'lik sabit tavan; `[LiveLauncherFact]`. Tur 1'de listelenmedi. |
| 10 | `UpdaterTests.cs:916` | `offlineSecond < TimeSpan.FromSeconds(3)` | **bant** | Aynısı, ikinci açılış. |
| 11 | `UpdaterTests.cs:1141` | `release.Join(TimeSpan.FromSeconds(10))` | **bant** | Gerçek bir iş parçacığının bitmesine konmuş 10 sn'lik tavan; sade `[Fact]`, Windows'ta koşuyor. Tur 1'de listelenmedi. |
| 12 | `UpdaterTests.cs:1173` | `stopwatch.Elapsed > TimeSpan.FromSeconds(5)` | **bant** | İddia 5; taban 1 → 5 sn daraltıldı, mutasyonla pimlendi. |
| 13 | `PerformanceCheckTests.cs:165` | `Equal(1.4, result.SoftwareRealtimeCores, 3)` | elenmiş | `Cost()` kurgusundan gelen sabit sayı; sade `[Fact]`, saat hiç okunmuyor. |
| 14 | `PerformanceCheckTests.cs:221` | `f.RealtimeCores > 0` | elenmiş | Aynı kurgu, sade `[Fact]`. |
| 15 | `PerformanceCheckTests.cs:253` | `Equal(cabuk.SoftwareRealtimeCores, yavas.SoftwareRealtimeCores, 6)` | elenmiş | İki kurgu değerlendirmesinin karşılaştırması; saat yok. |
| 16 | `PerformanceCheckTests.cs:290` | `Equal(12_000, finding.WallMs)` | elenmiş | `gecen: 12_000` testin kendi verdiği sabit. |
| 17 | `PerformanceCheckTests.cs:302` | `Equal(1400, net.WallMs)` | elenmiş | Kurgu `EncoderCost` aritmetiği. |
| 18 | `PerformanceCheckTests.cs:303` | `Equal(0.7, net.RealtimeCores, 6)` | elenmiş | Aynı aritmetik. |
| 19 | `PerformanceCheckTests.cs:341` | `result.SoftwareRealtimeCores > 0` | elenmiş | Canlı okuma ama totolojik: `:333`teki `if (!result.SoftwareMeasured) return;`den sonra geliyor, `Usable` da yalnız `VideoMs > 0, WallMs > 0` olan maliyeti geçiriyor (`PerformanceCheck.cs:345`) — ölçülmüş bacakta `RealtimeCores > 0` zorunlu. `:629` ile aynı sınıf. |
| 20 | `PerformanceCheckTests.cs:450` | `beklenen == okuma.Impact` | elenmiş | `beklenen` de karar da **aynı** okumadan türüyor; eşik tutarlılığı iddiası, duvar saati bandı değil. |
| 21 | `PerformanceCheckTests.cs:566` | `sebep.Length > 0` | elenmiş | Veri akışı yanlış pozitifi: `sebep` `ElapsedMs`i yalnız ileti metninde geçiriyor. İddia "sebep bulundu mu", süre değil. |
| 22 | `PerformanceCheckTests.cs:646` | `Equal(12_000, bulgu.WallMs)` | elenmiş | Sade `[Fact]`, `gecen: 12_000` sabiti. |
| 23 | `PerformanceCheckTests.cs:751` | `Equal(saglam.SoftwareRealtimeCores, buMakine.SoftwareRealtimeCores, 6)` | elenmiş | Aynı kurgu maliyetin iki sayaç katsayısıyla değerlendirmesi; kararın sayaca kaymadığını tutuyor, süre tutmuyor. |

**Bantların sayısı on bir, saat türevi assert'lerin sayısı yirmi üç.** Tur 1 "beş, başka
yok" dedi, tur 2 "sekiz" dedi; ikisi de eksikti. Sayı artık belgede değil **kodda**
tutuluyor — yirmi dördüncü saat türevi assert eklendiği anda CI kırmızıya döner,
bir sonraki turun raporuna kalmaz.

### `:629`un elenmesi ölçüldü

Danışmanın gerekçesi kendi koşumumla teyit edildi. `PerformanceCheck.cs:244`te
`WallMs: elapsedMs` → `WallMs: 0` mutasyonu uygulandı,
`dotnet build -c Release --no-incremental` ile derlendi, ölçü `--filter` ile koşuldu:

```
Başarısız VidShrink.Tests.PerformanceCheckTests.ButceGercektenBagliyorVeSebebiniSoyluyor [11 s]
  Hata İletisi:  butce bulgusu gecen sureyi tasimiyor
  ...PerformanceCheckTests.cs:line 629
```

Yani `:629` bir şey ölçüyor: geçen sürenin bulguya taşındığını. Bant değil — sabit eşik
yok, ve karşılaştırdığı `dar` üretimin bulguyu üretmek için zaten aştığını şart koştuğu
eşik. Kayıt `.calisma/t132/t3/iddia629-mutasyon.txt`. Üretim dosyası `git checkout --`
ile geri alındı; teslim edilen dalda üretim kodu değişmedi.

### Sayımı tutan ölçü

`PerformanceCheckTests.SaatTureviIddialarinSayisiBelgedekiyleAyni` üç test dosyasını
kaynak metin olarak okuyor, yukarıdaki kapalı kümeyi kuruyor ve bulduğu assert sayısını
`SaatTureviIddiaSayisi = 23` sabitine eşitliyor. Kalıp bu depoda zaten var:
`UpdaterTests.cs:1185-1196` `Program.cs`i kaynak metin olarak okuyup `IndexOf` ile iddia
kuruyor.

Sayı tutmazsa hata iletisi bulunan bütün satırları basıyor; bir sonraki okuyan farkı
listeyle karşılaştırıp doğrudan görüyor.

| mutasyon | sonuç |
|---|---|
| `SaatTureviIddiaSayisi` 23 → 24 | **kırıldı** — `saat turevi iddia sayisi 23, belge 24 diyor: ...` |
| mutasyonsuz | **yeşil** — `Başarısız: 0, Başarılı: 1, Toplam: 1` |

Ölçünün bilinen sınırı: `Assert` ifadesinin sonu "sonu `;` olan ilk satır" kuralıyla
bulunuyor. Bu depodaki biçimlendirmede tutuyor; bir `Assert`in içine çok ifadeli bir
lambda konursa ifade erken kesilebilir. O durumda sayı değişir ve ölçü kırmızıya döner —
sessizce yanlış saymaz.

### Assert edilmeyen zaman kullanımları

Tarama A'nın getirdiği, tarama B'nin haklı olarak getirmediği satırlar:

- `PerformanceCheckTests.cs:594,598,602,735,838` — iddiaların kendi saatlerinin kurulumu.
- `PerformanceCheckTests.cs:607-609,739,850-851` — yalnız `Log`.
- `PerformanceCheckTests.cs:843` — `p.WaitForExit()`, zaman aşımı argümanı yok.
- `PerformanceCheckTests.cs:881` — `Task.WaitAll(_workers, 5000)`, yapay yükün
  kapatılması; üzerinde assert yok.
- `UpdaterTests.cs:311,1143,1165` — yalnız `_output.WriteLine`.
- `UpdaterTests.cs:947,956` — `MeasureLaunch`in kendi saati; onu assert eden `:915/:916`
  yukarıda bant olarak listelendi.
- `UpdaterTests.cs:781,821,1131` — `Thread.Sleep(25)` / `Thread.Sleep(5)`, sınırlı
  yoklama gecikmeleri; üzerlerinde bant iddiası yok.
- `UpdaterTests.cs:796` (`DateTime.UtcNow + 20 sn`), `:1126` (`+30 sn`) — döngü son
  tarihleri; süre dolduğunda döngü sessizce çıkıyor, assert yok. `:1126`nın sessiz
  çıkışını `:1141`in 10 sn'lik `Join`i yakalıyor.
- `UpdaterTests.cs:853,949,1098` — süresiz `Join()` / `WaitForExit()`; bant değil.
- `UpdaterTests.cs:127,147,325,653,709,1242,1277` — `DateTimeOffset.UtcNow` manifest
  kurgusunda; zaman iddiası yok.
- `ComplexityProbeTests.cs:48` — `Task.Delay(Timeout.InfiniteTimeSpan, ct)`, iptal
  testinde bilerek sonsuz bekleme.
- `ComplexityProbeTests.cs:470,516` — `await process.WaitForExitAsync()`, zaman aşımı
  argümanı yok.

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

**Bu sözleşmenin üç dosyasında saat türevi assert sayısı yirmi üç; bunların on biri
duvar saati bandı.** Beşi tur 1 kapsamındaydı, üçü tur 1'de gözden kaçtı, biri
(`UpdaterTests.cs:890`) tur 2'de de gözden kaçtı, ikisi (`:462`, `:542`) hiçbir metin
deseninin bulamayacağı yerdeydi. Sayım artık bir ölçüyle tutuluyor; tur 1'in "başka
yok" cümlesi ve tur 2'nin "sekiz" sayımı geri çekildi.

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
