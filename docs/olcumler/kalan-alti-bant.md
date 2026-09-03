# T132'den devreden altı duvar saati bandı (T145)

T132 saat türevi 23 iddiayı sayıp 11 bant olarak sınıflandırdı, beş bandı daralttı
ve kalan altısını açık borç olarak devretti. Bu belge o altı bandı kapatır.

Her bandın üç meşru cevabı vardı: **daralt**, **saatten kurtar**, ya da **ölçülmüş
gerekçeyle bırak**. Altısı da bir cevap aldı; gerekçesiz bırakılan bant yok.

## Ölçüm düzeneği

Bantların üçü (`UpdaterTests.cs:890`, `:915`, `:916`) `[LiveLauncherFact]` kapısının
arkasındaydı ve `VIDSHRINK_LAUNCHER_EXE` kurulu olmadığı için **bu depoda hiç
ölçülmemişti** — ne bu makinede ne CI'da. T132 de bunu "atlanıyor" diye kayda
geçirmişti. Ölçebilmek için gerçek bir kurulum kökü kuruldu:

```
.calisma/T145/kurulum/VidShrink.exe        (dotnet publish src/VidShrink.Launcher)
.calisma/T145/kurulum/app/VidShrink.App.exe (dotnet publish src/VidShrink.App)
```

Kök eksikken başlatıcı ölçülemiyor, **asılıyor**: `app/VidShrink.App.exe` yoksa
`Program.cs:40`, `tools/ffmpeg` yoksa `Program.cs:78` modal bir uyarı kutusu açıp
insan bekliyor, `MeasureLaunch` ise zaman aşımsız `WaitForExit()` çağırıyor
(`UpdaterTests.cs:949`). İlk denemede koşum tam olarak böyle takıldı. Bu, `:915`
ve `:916` bantlarının bugüne kadar hiç değerlendirilmemiş olmasının sebebidir.

Kullanılan yük: `yes > /dev/null` × 15 (mantıksal çekirdek sayısı 16, eksi bir).
Her örnek ayrı bir `dotnet test` çağrısı; `--no-build` hiç kullanılmadı.

**Kirlenen bir örneklem atıldı.** İlk turda iki ölçüm grubu farkında olmadan aynı
anda koştu ve aynı dosyaya yazdı (20 tur istendi, dosyada 23 tur çıktı). O örneklem
`.calisma/T145/COPLUK-ad-bos-kirli.txt` adına taşındı, kullanılmadı, aşağıdaki
hiçbir sayı ondan gelmiyor. Yükün hiç binmediği ikinci bir grup da
(`COPLUK-ad-yuklu-yuksuz.txt`) aynı şekilde atıldı.

## K1 — Altı bandın karar tablosu

| # | Yer | İddia | Cevap | Eski | Yeni | Dayanak |
|---|---|---|---|---|---|---|
| A | `UpdaterTests.cs:890` | `process.WaitForExit(60_000)` | **daralt** | 60.000 ms | 5.000 ms | 40 koşum: boş 99–123 ms, yüklü 159–280 ms. Yeni tavan gözlenen en kötünün 18 katı |
| B | `UpdaterTests.cs:915` | `offlineFirst < 3 sn` | **bırak** | 3.000 ms | 3.000 ms | Soğuk ikili + yük altında 2291–2438 ms ölçüldü; 2.000 ms yanlış kırmızı üretirdi |
| C | `UpdaterTests.cs:916` | `offlineSecond < 3 sn` | **daralt** | 3.000 ms | 2.000 ms | 45 koşumda en kötü 1110 ms. Yeni tavan onun 1,8 katı |
| D | `UpdaterTests.cs:1141` | `release.Join(10 sn)` | **daralt** | 10.000 ms | 2.000 ms | 40 koşumun 40'ında 0 ms |
| E | `PerformanceCheckTests.cs:462` | `yuklu >= taban * YonPayi` | **daralt** | `YonPayi = 0,8` | `YonPayi = 0,9` | 10 sakin tur, oran 1,682–2,166; yüklü okumanın kendi saçılması ±%6 |
| F | `PerformanceCheckTests.cs:542` | `yuklu > bos` | **bırak** | katsayı yok | katsayı yok | Katsayı koymak makine gürültüsünü ürün iddiası yapardı; ×1,5 kayıtlı on turun dördünü kırardı |

Altı satır saydım: **dört daraltma** (A, C, D, E), **iki ölçülmüş gerekçeyle
bırakma** (B, F), **sıfır saatten kurtarma**, **sıfır gerekçesiz bırakma**.
Hiçbir bant genişletilmedi.

Saatten kurtarma neden hiç seçilmedi: altı bandın dördü (A, B, C, D) gerçek bir
süreç ya da iş parçacığı sınırıdır — enjekte edilecek bir saat yok, ölçülen şey
işletim sisteminin kendi zamanlaması. Kalan ikisi (E, F) zaten saat okumasının
kendisini karşılaştırıyor; ölçünün para birimi duvar saati olmaktan çıkarsa iddia
da kalmaz.

## K1 dayanakları — ölçülen dağılımlar

### A — `UpdaterTests.cs:890`, geçiş sürecinin çıkışı

Ham örnekler (ms, sıralı):

```
boş   (n=20):  99  99  99  99 100 100 101 102 102 103 104 107 107 107 111 111 112 118 120 123
yüklü (n=20): 159 166 170 174 174 178 182 185 185 188 199 201 203 207 213 216 225 241 269 280
```

Yirmişer örnek saydım. Eski tavan 60.000 ms, gözlenen en kötünün **214 katı**.
Yeni tavan 5.000 ms, gözlenen en kötünün **18 katı**. CI koşucusu bu makineden
yavaş olduğu için pay bilerek geniş bırakıldı.

Aynı koşumlarda ölçülen komşu büyüklük, silme probunun süresi (D'nin bağlamı):
boş 459–544 ms, yüklü 857–1527 ms.

### B — `UpdaterTests.cs:915`, ağsız **ilk** açılış

```
ısınmış ikili, boş   (n=20): 875 878 879 880 881 886 889 889 891 893 894 894 895 895 895 897 897 898 898 899
ısınmış ikili, yüklü (n=20): 968 970 970 974 979 991 992 995 997 1007 1014 1020 1033 1058 1099 1106 1140 1266 1279 1368
soğuk ikili, yüklü   (n=5): 2291 2300 2375 2422 2438
```

İlk iki satır 3.000 ms tavanının çok altında duruyor ve bandı 2.000 ms'e çekmeyi
haklı gösteriyordu. **Üçüncü satır bunu durdurdu.** Taze yayımlanmış 68 MB'lık tek
dosyalık başlatıcının ilk açılışı soğuk dosya önbelleğiyle koşuyor; yük altında
2291–2438 ms sürüyor. 2.000 ms'lik bir tavan bu koşumların **beşini de** kırardı.

Bu satır ölçülmeden önce band 2.000 ms'e çekilmişti ve yeşil görünüyordu (ısınmış
ikiliyle 991 ms). Soğuk ölçüm alınmasaydı bu sözleşme CI'a yanlış kırmızı üreten
bir daralma teslim edecekti.

**Bandın bugünkü payı sanılandan dar.** Gözlenen en kötü 2438 ms, tavan 3.000 ms:
kalan pay **%23**. Bu bir genişletme gerekçesi değil — sözleşme genişletmeyi
yasaklıyor ve ben de genişletmedim — ama T0'a bildirilmesi gereken bir risktir,
aşağıda "T0'a" başlığında duruyor.

### C — `UpdaterTests.cs:916`, ağsız **ikinci** açılış

```
ısınmış ikili, boş   (n=20): 878 878 879 882 882 883 884 884 887 888 890 891 892 894 894 897 899 901 905 908
ısınmış ikili, yüklü (n=20): 953 955 963 964 964 968 972 975 976 986 989 991 994 996 1001 1006 1014 1042 1106 1110
soğuk ikili, yüklü   (n=5): 947 964 965 973 982
```

Kırk beş örnek saydım. İkinci açılış **hiçbir koşulda soğuk değil**: ilk açılış
ikiliyi zaten önbelleğe almış oluyor, soğuk ikilili koşumda bile 947–982 ms.
Gözlenen en kötü 1110 ms; yeni tavan 2.000 ms, onun **1,8 katı**.

Bandın iki yarısının farklı tavan taşıması bilinçli: `:915` soğuk başlangıcı da
ölçüyor, `:916` ölçmüyor. Tek sabit altında toplansalardı soğuk başlangıcın payı
ikinci açılışa da ödenirdi.

### D — `UpdaterTests.cs:1141`, kilidi bırakan iş parçacığı

```
boş   (n=20): 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0
yüklü (n=20): 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0
```

Kırk koşumun kırkında 0 ms. Sebebi yapısal: silme probu zaten kilidin
bırakılmasını bekliyor, yani `Join` çağrıldığında iş parçacığı çoktan bitmiş
oluyor. Bu, K2'deki ilk mutasyon denemesinin neden işe yaramadığını da açıklıyor.

### E — `PerformanceCheckTests.cs:462`, `YonPayi`

On tur, sakin makine, hepsi yeşil, atlanan yok. `taban` = üç boş okumanın en
düşüğü, `yuklu` = yük altındaki okuma:

| tur | taban | yüklü | oran |
|---|---|---|---|
| 1 | 0,713 | 1,221 | 1,712 |
| 2 | 0,707 | 1,220 | 1,726 |
| 3 | 0,724 | 1,218 | 1,682 |
| 4 | 0,608 | 1,317 | 2,166 |
| 5 | 0,720 | 1,344 | 1,867 |
| 6 | 0,708 | 1,201 | 1,696 |
| 7 | 0,668 | 1,264 | 1,892 |
| 8 | 0,767 | 1,307 | 1,704 |
| 9 | 0,755 | 1,305 | 1,728 |
| 10 | 0,750 | 1,339 | 1,785 |

On satır saydım. En düşük oran **1,682**, en yüksek 2,166.

`YonPayi`nin ne kadar olması gerektiği buradan türüyor. İddianın gerçek eşiği
1,0'dir: "yük maliyeti yalnız artırabilir". Pay yalnızca **yüklü okumanın kendi
saçılmasını** soğurmak zorunda, çünkü `taban` üç okumanın minimumudur ve
kirlenme minimumu yalnız yukarı iter — yani taban tarafındaki gürültü iddiayı
kolaylaştırır, zorlaştırmaz. Yüklü okumanın saçılması on turda 1,201–1,344,
ortalaması 1,272, yani ortalamasının çevresinde **±%6**. Eski pay %20
soğuruyordu; gözlemlerin hiçbiri bunun üçte birini kullanmadı. Yeni pay %10,
ölçülen saçılmanın 1,7 katı.

Payı 1,0'e kadar sıkmak da ölçüme göre mümkündü (en düşük oran 1,682) ama
yapılmadı: bu makinede 16 çekirdek var ve yük `ProcessorCount-1` = 15 işçi.
Dört çekirdekli bir CI koşucusunda yük 3 işçidir ve oranın ne çıkacağı
**ölçülmedi**. %10'luk pay o ölçülmemiş farkın altında duruyor.

### F — `PerformanceCheckTests.cs:542`, yükün ölçümde görünmesi

On tur, `[QuietMachineFact]` kapısı açıkken:

| tur | boş | yüklü | oran |
|---|---|---|---|
| 1 | 0,825 | 1,242 | 1,505 |
| 2 | 0,827 | 1,151 | 1,392 |
| 3 | 0,825 | 1,171 | 1,419 |
| 4 | 0,807 | 1,302 | 1,613 |
| 5 | 0,816 | 1,119 | 1,371 |
| 6 | 0,753 | 1,178 | 1,564 |
| 7 | 0,860 | 1,342 | 1,560 |
| 8 | 0,842 | 1,154 | 1,371 |
| 9 | 0,862 | 1,366 | 1,585 |
| 10 | 0,880 | 1,325 | 1,506 |

On satır saydım. Oran 1,371–1,613; iddianın eşiği 1,0, yani gözlenen en dar
kenar payı **%37**.

**Neden bırakıldı.** Bu bantta daraltılacak bir sabit yok: iddia `yuklu > bos`,
katı eşitsizlik, bir yön iddiasının en dar hali. Daraltmak ancak bir katsayı
**eklemekle** olurdu (`yuklu > bos * k`) ve o katsayı bu makinenin oranını ürün
iddiası haline getirirdi. Ölçüm bunu doğruluyor: `k = 1,5` konsaydı yukarıdaki on
turun **dördü** (2, 3, 5, 8 — oranları 1,392, 1,419, 1,371, 1,371) kırmızıya
düşerdi. Testin kendi belgesi de aynı hatanın T117'de yapılıp geri alındığını
yazıyor: karar sınıfının değişmesi "ürünün değil makinenin özelliği".

**Canlı kırmızı üretilemedi ve sebebi kaydediliyor.** `k = 1,5` geçici olarak
konup dokuz kez koşuldu; dokuzunda da ölçü kırmızı değil **atlandı**, çünkü
`[QuietMachineFact]` kapısı kapandı: makinenin boş okuması saatler süren ölçüm
koşumlarından sonra 1,122'den 1,415'e sürüklendi ve eşik 1,0'in üstünde kaldı.
Yukarıdaki dört kırmızı bu yüzden **kayıtlı okumalar üzerinde yapılmış aritmetik**,
canlı koşum değil. Kapının üst üste dokuz kez kapanması ise iddianın ne kadar
makineye bağlı olduğunun doğrudan kanıtı ve bırakma kararını güçlendiriyor.

## K2 — Mutasyon ızgarası

Daraltılan dört bandın her biri için, **eski bantta yaşayan ve yeni bantta ölen**
birer mutasyon. Her mutasyon `dotnet build -c Release --no-incremental` ile
derlendi; `--no-build` hiç kullanılmadı. Başlatıcıyı ilgilendiren mutasyonlarda
`dotnet publish` ile kurulum kökündeki ikili de yenilendi, yoksa mutasyon ölçüye
hiç ulaşmazdı. Üretim kodu teslim edilen dalda **değişmedi**
(`git diff main..HEAD -- src/` boş).

| Bant | Mutasyon | Eski bant | Eski bantla | Yeni bant | Yeni bantla |
|---|---|---|---|---|---|
| A `:890` | `LauncherUpdate.Commit` içine `Thread.Sleep(8000)` (üretim, geçici) + başlatıcı yeniden yayımlandı | 60.000 ms | **yaşadı** — 8119 ms | 5.000 ms | **öldü** — 5014 ms |
| C `:916` | `new HttpClient { Timeout = ... }` kullanım yerinde 800 → 2400 ms, `ManifestTimeout` sabiti 800'de bırakıldı (üretim, geçici) + yeniden yayım | 3.000 ms | **yaşadı** — 2566 ms | 2.000 ms | **öldü** — 2576 ms |
| D `:1141` | Kilidi bırakan iş parçacığında `stream.Dispose()` sonrasına `Thread.Sleep(5000)` | 10.000 ms | **yaşadı** — 4743 ms | 2.000 ms | **öldü** — 2002 ms |
| E `:462` | İddianın sol tarafı `taban * 0.85`e sabitlendi, yani "yük maliyeti %15 düşürdü" dünyası | `YonPayi = 0,8` | **yaşadı** | `YonPayi = 0,9` | **öldü** |

Dört mutasyon, sekiz kol saydım; sekizinin de ham çıktısı aşağıda. B ve F için
mutasyon yok çünkü bantları değişmedi.

### A — ham çıktı

```
--- eski bant (60 sn) ---
  Başarılı VidShrink.Tests.UpdaterTests.TheIncomingBinaryRenamesItselfOntoTheTargetName [9 s]
 geçiş süreci: 8119 ms (tavan 60000 ms)

--- yeni bant (5 sn) ---
      System.AggregateException : One or more errors occurred. (geçiş süreci çıkmadı) (Access to the path 'VidShrink.new.exe' is denied.)
      ---- geçiş süreci çıkmadı
        geçiş süreci: 5014 ms (tavan 5000 ms)
  Başarısız VidShrink.Tests.UpdaterTests.TheIncomingBinaryRenamesItselfOntoTheTargetName [6 s]
```

### C — ham çıktı

İlk denemede mutasyon **yanlış bandı** kırdı: aynı testteki `:915` (3986 ms > 3000)
`:916`'dan önce değerlendiği için ölçü orada düştü ve `:916` hiç sınanmadı. T132'nin
"mutasyon kendini gizledi" tuzağının komşu-iddia hali. İki kolu ayırmak için mutasyon
koşumlarında `:915` tavanı geçici olarak 10 sn'ye alındı; teslim edilen kodda 3 sn.

```
--- gölgelenen ilk deneme ---
      ağsız açılış çok uzun: 00:00:03.9855030
        ağsız ilk açılış: 3986 ms
        ağsız ikinci açılış: 2597 ms

--- eski bant (3 sn), :915 yalıtıldı ---
  Başarılı VidShrink.Tests.UpdaterTests.EveryLaunchChecksAndStaysWithinTheTimeout [6 s]
 ağsız ilk açılış: 2617 ms
 ağsız ikinci açılış: 2566 ms

--- yeni bant (2 sn), :915 yalıtıldı ---
      ağsız ikinci açılış çok uzun: 00:00:02.5756041
        ağsız ilk açılış: 2602 ms
        ağsız ikinci açılış: 2576 ms
  Başarısız VidShrink.Tests.UpdaterTests.EveryLaunchChecksAndStaysWithinTheTimeout [5 s]
```

### D — ham çıktı

İlk mutasyon denemesi işe yaramadı ve bandın yapısını açıkladı: iş parçacığının
döngü uykusu 5 ms → 5000 ms yapıldığında `Join` yine **0 ms** ölçtü, çünkü silme
probu zaten aynı olayı bekliyor. Mutasyon iş parçacığının **kuyruğuna** taşındı.

```
--- işe yaramayan ilk deneme (döngü uykusu 5000 ms) ---
  Başarılı VidShrink.Tests.UpdaterTests.TheDeletionStepWaitsOutATransientLock [6 s]
 kilidi bırakan iş parçacığı: 0 ms

--- eski bant (10 sn), kuyrukta 5000 ms ---
  Başarılı VidShrink.Tests.UpdaterTests.TheDeletionStepWaitsOutATransientLock [5 s]
 kilidi bırakan iş parçacığı: 4743 ms

--- yeni bant (2 sn), kuyrukta 5000 ms ---
      kilidi bırakan iş parçacığı bitmedi
        kilidi bırakan iş parçacığı: 2002 ms (tavan 2000 ms)
  Başarısız VidShrink.Tests.UpdaterTests.TheDeletionStepWaitsOutATransientLock [2 s]
```

### E — ham çıktı

```
--- eski pay (0,8) ---
  Başarılı VidShrink.Tests.PerformanceCheckTests.OlcumYukAltindaYalnizAgirlasiyor [3 m 25 s]

--- yeni pay (0,9) ---
      yuk altinda maliyet dustu: en dusuk bos okuma 1.045, yuklu 2.4
  Başarısız VidShrink.Tests.PerformanceCheckTests.OlcumYukAltindaYalnizAgirlasiyor [3 m 18 s]
```

Kırmızı satırdaki sayılar (1.045 / 2.4) gerçek okumalardır; mutasyon iddianın
**koşulunu** sabitledi, hata iletisinin biçimlendirdiği alanları değil. Yani ileti
"maliyet düştü" derken gerçek okumalar düşmemişti — kolun davranışı doğru, iletisi
yanıltıcı. Bu, teslim edilen kodda değil yalnız mutasyon kolunda görülür.

## K3 — `:462` ve `:542` için özel dikkat

İkisi de bant değil **yön** iddiası, ikisine de ayrı davranıldı.

- **`:462` için daraltmak sabit küçültmektir ve ölçülerek yapıldı.** `YonPayi`nin
  0,8 olmasının kayıtlı bir dayanağı yoktu; yukarıdaki on turluk ölçüm payın neyi
  soğurmak zorunda olduğunu (yüklü okumanın ±%6 saçılması) ve neyi soğurmak
  zorunda olmadığını (taban tarafındaki gürültü, çünkü taban minimumdur) ayırdı.
- **`:542` için daraltmak katsayı eklemek olurdu ve yapılmadı.** Gerekçe ölçülü:
  `k = 1,5` on turun dördünü kırardı.

**Tekrar sayısı ve kararlılık.** `:462`'yi taşıyan `OlcumYukAltindaYalnizAgirlasiyor`
ile `:542`'yi taşıyan `YukAltindaKararHafiflemiyorMu` **10 kez** peş peşe koşuldu;
onunda da ikisi yeşil, atlanan yok, kırmızı yok. Koşumlar makine sakinken yapıldı:
başlamadan önce `testhost`, `ffmpeg`, `VidShrink.exe`, `VidShrink.App.exe`
süreçlerinin sayısı sıfırlandı ve doğrulandı. Yeni payla (0,9) `:462` ayrıca tek
başına yeşil koştu.

**Kararlılığın sınırı, açıkça.** On tur ölçümün ardından — saatler süren koşumların
sonunda — makinenin boş okuması 1,12–1,42 aralığına sürüklendi ve
`[QuietMachineFact]` kapısı kapandı; `:542` o noktadan sonra atlanıyor. Bu bir
gerileme değil, kapının işini yapması. Ama `:542` için canlı bir kırmızı
üretemememin sebebi de budur ve F bandının gerekçesi bu yüzden aritmetiktir.

## K4 — Sayım ölçüsü

`SaatTureviIddiaSayisi = 23` sabiti **değişmedi** ve
`SaatTureviIddialarinSayisiBelgedekiyleAyni` yeşil kaldı:

```
  Başarılı VidShrink.Tests.PerformanceCheckTests.SaatTureviIddialarinSayisiBelgedekiyleAyni [754 ms]
```

Sayının değişmemesi beklenen sonuçtu: dört bandın hiçbirinde iddia **eklenmedi ya
da kaldırılmadı**, yalnız eşik değerleri değişti ve iki iddia (`:890`, `:1141`)
yerel değişkene alındı. Yerel atamanın sağ tarafı hâlâ saat çekirdeği taşıdığı
için sayaç ikisini de görmeye devam ediyor. Ölçü devre dışı bırakılmadı,
filtreden çıkarılmadı, sabit büyütülmedi.

**Yan etki, T0'a:** iddialar dosyada kaydı, yani `docs/olcumler/duvar-saati-iddialari.md`
içindeki satır numaraları artık eskidir. O dosya bu sözleşmenin `owns` listesinde
değil, dokunulmadı.

## K5 — Verify kollarının test sayısı

`dotnet test -c Release --list-tests --filter "<kol>"`:

| Kol | Bulunan test |
|---|---|
| `PerformanceCheckTests` | 22 |
| `UpdaterTests` | 54 |

İki kol saydım, sıfır bulan kol yok. İkisi birlikte **76** test buluyor;
22 + 54 = 76, kollar örtüşmüyor.

Verify koşumu (`VIDSHRINK_LAUNCHER_EXE` kurulu):

```
Başarılı!  - Başarısız: 0, Başarılı: 74, Atlanan: 2, Toplam: 76, Süre: 6 m 15 s
```

Atlanan iki ölçü `YukAltindaKararHafiflemiyorMu` (`[QuietMachineFact]`, makine
sakin değil) ve `DonanimKodlayiciIslemciZamaniniOlculebilirYaziyorMu`. İkisi de
ortam kapısı; kırmızı değil.

`VIDSHRINK_LAUNCHER_EXE` **kurulu olmadan** — yani CI'ın gördüğü halde —
`UpdaterTests` kolunun üç ölçüsü atlanır; A, B ve C bantları o koşumda hiç
değerlendirilmez. Bu, bu sözleşmenin değiştirdiği bir şey değil, devraldığı
durumdur ve aşağıda T0'a bildiriliyor.

## T0'a

1. **`:915`in payı dar ve bunu ölçüm gösterdi.** Gözlenen en kötü 2438 ms
   (soğuk ikili + yük), tavan 3.000 ms, kalan pay %23. Genişletmedim — sözleşme
   yasaklıyor ve gerekçem de yok — ama CI koşucusu bu makineden yavaşsa bu bandın
   kırmızı vermesi olasıdır. Karar T0'ın.
2. **Üç bant CI'da hâlâ hiç değerlendirilmiyor.** `VIDSHRINK_LAUNCHER_EXE` CI'da
   kurulmuyor, bu yüzden `:890`, `:915`, `:916` orada atlanıyor. Kurmak
   `.github/workflows/ci.yml` değişikliği ister; o dosya `owns` listesinde değil,
   dokunmadım. Kurulum kökünün nasıl hazırlandığı yukarıda yazılı.
3. **Assert edilmeyen bir duvar saati okuması var ve büyük.** Aynı testteki
   üçüncü açılış (`ağlı açılış`, `UpdaterTests.cs:910`) hiçbir iddianın altında
   değil. Ölçtüm: boş koşumların çoğunda 506–542 ms, ama bir turda **65.305 ms**,
   başka bir turda 41.596 ms. Bu, T132'nin "assert edilmeyen zaman kullanımları"
   bölümüne giren bir satır; ona bir bant koymak yeni bir iddia kurmaktır ve bu
   sözleşmenin kapsamında değil. Bildiriyorum.
4. **`MeasureLaunch` zaman aşımsız bekliyor** (`UpdaterTests.cs:949`,
   `process.WaitForExit()`). Ortam eksikse ölçü kırmızıya düşmüyor, **asılıyor**.
   Bu benim `owns`umda olan bir dosya ama bir **bant eklemek** demek olurdu ve
   sözleşme "bandı genişletme / yeni iddia kurma" sınırının hangi tarafında
   durduğunu söylemiyor; kendi başıma karar vermedim.
5. **`duvar-saati-iddialari.md` satır numaraları eskidi** (K4'e bakınız). Dosya
   `owns` dışında.
6. **"Altı bant, altı commit" kuralı dört commit + bu belge olarak uygulandı, ve
   bu bir sapmadır.** Daraltılan dört bant (A, C, D, E) kendi commit'ini aldı.
   B ve F ölçüm gereği kod değişikliği üretmedi; onlara ayrı birer commit açmak
   için belgeyi yapay olarak ikiye bölmem gerekirdi ve bunu yapmadım — ikisinin
   de kararı ve dayanağı bu belgede, kendi başlıkları altında duruyor. Kayıt
   granülasyonunu bu şekilde eksilttiğimi bilerek bildiriyorum.
