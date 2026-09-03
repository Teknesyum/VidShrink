# Kalan alti duvar saati bandi — T145

T132 sayimi bitirdi (23 saat turevi iddia, 11 bant) ve bes bandi daraltti. Kalan alti bant
sayilmis ama daraltilmamis borctu. Bu belge o alti bandin her biri icin karari, karari
tutan olcumu ve mutasyon izgarasini tasiyor.

Butun kosumlar Windows 11, 16 mantiksal cekirdek, .NET 9.0.316 SDK / net8.0 hedefi,
`-c Release`. **Makine bos degildi:** ayni makinede baska yapici ajanlar kosuyordu.
Bu, olculen sayilari asagi degil yukari iter (gurultu duvar saatini uzatir), yani
daraltma kararlari gercekte olduklarindan daha temkinli.

Her mutasyon kolu icin ayri `dotnet build -c Release --no-incremental` kosuldu;
`--no-build` mutasyon kollarinda hic kullanilmadi.

## K1 — Alti bandin karar tablosu

| # | Yer | Eski bant | Karar | Yeni bant | Dayanak |
|---|---|---|---|---|---|
| 1 | `UpdaterTests.cs:890` | `WaitForExit(60_000)` | **daraltildi** (kol 1) | `GecisTavaniMs = 5_000` | 5 kosum: 101/81/84/117/82 ms |
| 2 | `UpdaterTests.cs:915` | `< TimeSpan.FromSeconds(3)` | **birakildi** (kol 3) | `AgsizAcilisTavani = 3 sn` | urun butceleri toplami 2887–2949 ms |
| 3 | `UpdaterTests.cs:916` | `< TimeSpan.FromSeconds(3)` | **birakildi** (kol 3) | `AgsizAcilisTavani = 3 sn` | ayni olcum |
| 4 | `UpdaterTests.cs:1141` | `Join(TimeSpan.FromSeconds(10))` | **daraltildi** (kol 1) | `KilidiBirakanIsParcacigiTavani = 1 sn` | 5 kosum: 0/0/0/0/0 ms |
| 5 | `PerformanceCheckTests.cs:462` | `YonPayi = 0.8` | **daraltildi** (kol 1) | `YonPayi = 1.0` | 13 kosum: oran 1,781–2,156 |
| 6 | `PerformanceCheckTests.cs:542` | `yuklu > bos` | **birakildi** (kol 3) | degismedi | 8 kosum: oran 1,441–1,888 |

Uc daraltildi, uc olculmus gerekceyle birakildi. Hicbiri genisletilmedi, hicbiri
`Skip`e alinmadi, hicbiri filtreden cikarilmadi.

## Bant 1 — `UpdaterTests.cs:890`, gecis sureci tavani

**60 sn nereden geliyordu:** yaziliadi. Iddia "gecis sureci cikar"; 60 sn yalnizca
asilmama tavani.

Bandi olcmek icin baslatici yayimlandi (`dotnet publish`, tek parcali kendi kendine
acilan 64,8 MB `VidShrink.exe`) ve `VIDSHRINK_LAUNCHER_EXE` ona gosterildi. **Bu olcum
pencere acmaz:** `src/VidShrink.Launcher/Program.cs:36` gecis kipini (`--commit-launcher`)
`Alert` cagrisindan **once** ele aliyor ve dogrudan donuyor; uygulama hic acilmiyor.
Onceki kosumun masaustune dusurdugu kutu bu testten degil, `:915`/`:916`/`:928`in
kullandigi `MeasureLaunch`ten (normal kip) geliyordu.

Bes kosum, mesgul makine:

```
geçiş süresi: 101 ms, tavan 60000 ms
geçiş süresi:  81 ms, tavan 60000 ms
geçiş süresi:  84 ms, tavan 60000 ms
geçiş süresi: 117 ms, tavan 60000 ms
geçiş süresi:  82 ms, tavan 60000 ms
```

En yuksek okuma 117 ms; eski tavan onun 513 kati. Yeni tavan **5 000 ms**, en yuksek
okumanin 43 kati. Tek parcali ikilinin ilk acilista odedigi acma payi da bu araligin
icinde kalir. Sure artik gunluge yaziliyor, bir sonraki tur tahmin etmez.

## Bant 4 — `UpdaterTests.cs:1141`, kilidi birakan is parcacigi

Sonda sureci cikinca is parcacigi `Thread.Sleep(5)` dongusunden cikip kilidi birakiyor;
`Join` cagrildiginda genellikle **coktan bitmis** oluyor. Bes kosum:

```
kilidi bırakan iş parçacığı: 0 ms, tavan 10000 ms   (geçici kilit: çıkış 0, 1262 ms)
kilidi bırakan iş parçacığı: 0 ms, tavan 10000 ms   (geçici kilit: çıkış 0, 1132 ms)
kilidi bırakan iş parçacığı: 0 ms, tavan 10000 ms   (geçici kilit: çıkış 0, 1093 ms)
kilidi bırakan iş parçacığı: 0 ms, tavan 10000 ms   (geçici kilit: çıkış 0, 1126 ms)
kilidi bırakan iş parçacığı: 0 ms, tavan 10000 ms   (geçici kilit: çıkış 0, 1076 ms)
```

Yeni tavan **1 sn**, is parcaciginin 5 ms'lik yoklama turunun iki yuz kati.

## Bant 2 ve 3 — `UpdaterTests.cs:915` ve `:916`, agsiz acilis

Bu iki bant daraltilmadi. Gerekce iki ayakli, ikisi de olculmus.

### Neden uctan uca olculemez

`EveryLaunchChecksAndStaysWithinTheTimeout` baslaticiyi **normal kipte** acar. Normal kip
iki pencere kaynagi tasir:

1. `app/VidShrink.App.exe` yoksa `Alert` ile modal hata kutusu (onceki kosumda olan buydu);
2. `SplashGate.Threshold = 400 ms` (`src/VidShrink.Launcher/Splash.cs:24`) — is blogu
   400 ms'yi asarsa bekleme paneli **cizilir**.

Birincisi uygulamayi baslaticinin yanina koyarak kapatilabilir. Ikincisi kapatilamaz:
agsiz bacak manifest zaman asimi kadar, yani ~810 ms surer, ve 810 > 400. **Her agsiz
acilis paneli cizer.** Bu yuzden bandi uctan uca olcmek kullanicinin oturumunda pencere
acmadan mumkun degil; olculmedi.

### Bandin ayrik olcumu

Bant, olculebilir parcalarina ayrildi:

| Terim | Kaynak | Olcum |
|---|---|---|
| surec acilisi | bant 1'in gecis kipi kosumu | 81–117 ms (5 kosum) |
| manifest bekleyisi | `UpdateCheck.FetchManifestAsync` → `http://10.255.255.1/...` | 806–832 ms (8 cagri) |
| panel kapanis payi | `SplashGate.Dispose` → `_thread.Join(TimeSpan.FromSeconds(2))` | tavan 2 000 ms |

`Updater.Run` acilis basina **tek** manifest cagirir (`src/VidShrink.Launcher/Updater.cs:45`).
Manifest olcumunun ham ciktisi:

```
ManifestTimeout = 800 ms
agsiz manifest 1: 832 ms, sonuc=null
agsiz manifest 2: 806 ms, sonuc=null
agsiz manifest 3: 812 ms, sonuc=null
agsiz manifest 4: 813 ms, sonuc=null
agsiz manifest 5: 811 ms, sonuc=null
agsiz manifest 6: 807 ms, sonuc=null
agsiz manifest 7: 810 ms, sonuc=null
agsiz manifest 8: 810 ms, sonuc=null
```

Toplam en kotu hal: 81 + 806 + 2000 = **2 887 ms**, 117 + 832 + 2000 = **2 949 ms**.
3 000 ms'lik bandin alti **51–113 ms**.

**Sonuc: bant daraltilamaz.** Tavani kucultmek olcuyu urunun kendi izin verdigi en kotu
halde kirmiziya dusurur — yani kirilan sey olcu degil, olcunun urun hakkindaki iddiasi
olur. `UpdateCheck.ManifestTimeout` (800 ms) uretim kodunda, `SplashGate` da
`VidShrink.Launcher` icinde `internal`; test projesi baslaticiya basvurmuyor ve
`owns` uretim kodunu ve `csproj`u icermiyor, bu yuzden tavan bu sabitlerden **turetilerek**
yazilamadi. Yapilan: 3 sn `AgsizAcilisTavani` adiyla tek yere alindi ve dayanagi ustune
yazildi. Deger degismedi.

Not (kapsam disi): bandi gercekten daraltmanin yolu urun tarafinda, `SplashGate`in
kapanis `Join`ini kisaltmak ya da paneli olcum icin kapatabilen bir gecit acmaktir.
`src/**` bu sozlesmeye kapali; ayri sozlesme konusu.

## Bant 5 — `PerformanceCheckTests.cs:462`, `YonPayi`

**0,8 nereden geliyordu:** yaziliadi. Iddia "yuk maliyeti yalniz artirabilir" diyor, ama
0,8'lik pay yuk altinda %20'ye kadar **dusen** bir maliyeti sessizce geciriyordu.

`OlcumYukAltindaYalnizAgirlasiyor` on uc kez kosuldu (`[yuk]` satirlari
`.calisma/t63/olcum.txt` icinde). `taban = min(olculen bos okumalar)`,
`oran = yuklu / taban`:

```
 1  taban=0.497  yuklu=0.885  oran=1.781
 2  taban=0.496  yuklu=0.939  oran=1.893
 3  taban=0.494  yuklu=0.947  oran=1.917
 4  taban=0.495  yuklu=0.956  oran=1.931
 5  taban=0.493  yuklu=0.932  oran=1.890
 6  taban=0.492  yuklu=0.940  oran=1.911
 7  taban=0.507  yuklu=0.956  oran=1.886
 8  taban=0.501  yuklu=0.941  oran=1.878
 9  taban=0.493  yuklu=1.063  oran=2.156
10  taban=0.504  yuklu=0.944  oran=1.873
11  taban=0.516  yuklu=0.978  oran=1.895
12  taban=0.522  yuklu=0.937  oran=1.795
13  taban=0.505  yuklu=1.014  oran=2.008
n=13  min=1.781  medyan=1.893  max=2.156
```

Gerekli pay olculdu: **hicbir kosumda 1,781'in altina inilmedi.** 0,8'lik eski pay,
gozlenen en dusuk oranin 2,2 kati kadar genisti. Pay **1,0**'e cekildi; olcu artik
docstring'in soyledigi seyi siniyor ve gozlenen en dusuk orana %78 mesafe var.

### Kararlilik kaniti (K3)

Yeni degerle, ayni mesgul makinede bes kosum daha:

```
YonPayi=1.0 kararlilik 1: taban=0.492 yuklu=0.926 oran=1.882 gecti=True
YonPayi=1.0 kararlilik 2: taban=0.511 yuklu=0.929 oran=1.818 gecti=True
YonPayi=1.0 kararlilik 3: taban=0.508 yuklu=0.906 oran=1.783 gecti=True
YonPayi=1.0 kararlilik 4: taban=0.501 yuklu=0.934 oran=1.864 gecti=True
YonPayi=1.0 kararlilik 5: taban=0.503 yuklu=0.926 oran=1.841 gecti=True
```

Toplam 18 kosum (13 eski deger + 5 yeni deger), hepsi ayni makinede, makine **bos degil**:
oturum boyunca baska yapici ajanlar kosuyordu ve `dotnet test`in kendisi bes kez es
zamanli calisti. Oranin dagilimi 1,781–2,156; genislik 0,375 ve tamami 1,0'in cok
ustunde.

## Bant 6 — `PerformanceCheckTests.cs:542`, yon iddiasi

`Assert.True(yuklu.SoftwareRealtimeCores > bos.SoftwareRealtimeCores, ...)`.

Bu bir **yon** iddiasinin en dar halidir: kabul edilen bolge zaten yarim dogru
(`oran > 1`). Daraltmak ancak `yuklu > bos * K` (K > 1) yazmakla, yani **iddiayi
degistirmekle** olur — ve bu, olcuyu daha kararsiz yapar, cunku iddia etmedigimiz bir
pay talep etmis oluruz. Sozlesmenin yonu daraltmak ya da saatten kurtarmak; burada
ikisi de yok.

Eldeki pay yine de olculdu — `YukAltindaKararHafiflemiyorMu`, sekiz kosum:

```
 1  bos=0.642  yuklu=0.978  oran=1.523
 2  bos=0.606  yuklu=0.950  oran=1.568
 3  bos=0.498  yuklu=0.914  oran=1.835
 4  bos=0.507  yuklu=0.957  oran=1.888
 5  bos=0.554  yuklu=0.936  oran=1.690
 6  bos=0.609  yuklu=0.949  oran=1.558
 7  bos=0.667  yuklu=0.961  oran=1.441
 8  bos=0.510  yuklu=0.946  oran=1.855
n=8  min=1.441  medyan=1.690  max=1.888
```

Bant oldugu gibi birakildi; gerekce olcunun docstring'ine yazildi.

## K2 — Mutasyon izgarasi

Uc bant daraltildi, uc mutasyon uretildi. Her mutasyon icin **iki kol**, her kolun
kendi gunlugu, ad kolu soyluyor. Gunlukler `.calisma/T145/mutasyon/` altinda.

| Bant | Mutasyon | Eski bant kolu | Yeni bant kolu |
|---|---|---|---|
| `:890` | `LauncherUpdate.Commit` basina `Thread.Sleep(8000)` | **yasadi** | **oldu** |
| `:1141` | birakma is parcaciginin sonuna `Thread.Sleep(3000)` | **yasadi** | **oldu** |
| `:462` | `PerformanceCheck.RealtimeCores`: 0,6 ustu 0,45'e kirpiliyor | **yasadi** | **oldu** |

### `:890` — `890-mutasyon-eski-bant-60000ms.log` / `890-mutasyon-yeni-bant-5000ms.log`

Eski bant kolu (`GecisTavaniMs = 60_000`):

```
 geçiş süresi: 8113 ms, tavan 60000 ms
Toplam test sayısı: 1
     Geçti: 1
```

Yeni bant kolu (`GecisTavaniMs = 5_000`):

```
System.AggregateException : One or more errors occurred. (geçiş süreci 5000 ms içinde çıkmadı) (Access to the path 'VidShrink.new.exe' is denied.)
---- geçiş süreci 5000 ms içinde çıkmadı
Toplam test sayısı: 1
     Başarısız: 1
```

Mutasyon uygulanip baslatici yeniden yayimlandi (`.calisma/T145/launcher-mut/`); iki kol da
o ayni mutasyonlu ikiliyi kosturdu, aralarindaki tek fark tavan.

### `:1141` — `1141-mutasyon-eski-bant-10sn.log` / `1141-mutasyon-yeni-bant-1sn.log`

Eski bant kolu (10 sn):

```
 kilidi bırakan iş parçacığı: 2700 ms, tavan 10000 ms
Toplam test sayısı: 1
     Geçti: 1
```

Yeni bant kolu (1 sn):

```
kilidi bırakan iş parçacığı 1000 ms içinde bitmedi
Toplam test sayısı: 1
     Başarısız: 1
```

Not: mutasyon **once** `stream.Dispose()`in onune konmustu ve iki kolda da yasadi —
sonda sureci kilidin birakilmasini bekledigi icin uyku `Join`dan once tukeniyordu
(`geçici kilit` 1229 ms yerine 4007 ms olarak gorundu, `Join` yine 0 ms). Uyku
`Dispose`dan **sonraya** alinincaildi. Bandi sinayan mutasyon, is parcaciginin
sonda surecinden **sonra** yasamaya devam etmesidir.

### `:462` — `462-mutasyon-eski-bant-YonPayi0.8.log` / `462-mutasyon-yeni-bant-YonPayi1.0.log`

Mutasyon `src/VidShrink.Core/PerformanceCheck.cs:37`:

```
-    public double RealtimeCores => VideoMs <= 0 ? 0 : WallMs / VideoMs;
+    public double RealtimeCores => VideoMs <= 0 ? 0 : (WallMs / VideoMs > 0.6 ? 0.45 : WallMs / VideoMs);
```

Yani "agir yuk ucuz gorunuyor" kusuru: yon tersine cevriliyor ama az, oran (0,8, 1,0)
araligina dusuyor. Esikler bu makinenin olculen bos (~0,50) ve yuklu (~0,94)
okumalarindan secildi.

Eski bant kolu (`YonPayi = 0.8`) — olcum gunlugu satiri:

```
[yuk] yukleyici=15 esik=1 | bos okumalar: SoftwareLightLoad/0.493/... SoftwareLightLoad/0.494/... SoftwareLightLoad/0.507/... | yuklu: SoftwareLightLoad/0.45/olculdu=True/butce=False | ...
Toplam test sayısı: 1
     Geçti: 1
```

taban 0,493, yuklu 0,45, oran 0,913 ≥ 0,8 → **yasadi**.

Yeni bant kolu (`YonPayi = 1.0`):

```
yuk altinda maliyet dustu: en dusuk bos okuma 0.492, yuklu 0.45
Toplam test sayısı: 1
     Başarısız: 1
```

0,45 < 0,492 → **oldu**.

Her iki kolda da mutasyon kaynakta duruyordu ve her kol icin
`dotnet build -c Release --no-incremental` kosuldu. Mutasyon kollardan sonra
kaynaktan geri alindi; `git diff src/` bos.

## K4 — Sayim olcusu

`SaatTureviIddiaSayisi = 23` **degismedi**. Degistirilmedi de: sabit oldugu gibi duruyor.

Neden degismedi: bes iddianin ifadesi degisti ama hicbiri yeni bir iddia dogurmadi ve
hicbiri kaybolmadi. Sayim kumeyi turden cikariyor — saatten tureyen uyeler, zaman asimi
argumani alan bekleme cagrilari, ve bunlardan tureyen yereller. Yeni yazimda:

- `:890` — `var exited = process.WaitForExit(GecisTavaniMs);` yereli `WaitForExit`
  tohumundan tohumlaniyor, `Assert.True(exited, ...)` onu tasiyor. Bir iddia.
- `:1141` — `var bitti = release.Join(...);` ayni sekilde `.Join(` tohumundan geliyor.
  Bir iddia.
- `:915`/`:916` — `offlineFirst`/`offlineSecond` zaten `MetotBasligi` uzerinden tohum olan
  `MeasureLaunch`ten geliyor; sabitin adlandirilmasi bunu degistirmiyor. Iki iddia.
- `:462`/`:542` — `Assert` govdesi degismedi. Iki iddia.

Olcunun kosumu (her duzenlemeden sonra tekrarlandi, sonuncusu):

```
Başarılı!  - Başarısız: 0, Başarılı: 1, Atlanan: 0, Toplam: 1 — SaatTureviIddialarinSayisiBelgedekiyleAyni
```

`docs/olcumler/duvar-saati-iddialari.md`deki tablo hala gecerli: ayni 23 iddia, ayni
satirlar; degisen sey uc bandin **degeri** ve uc bandin **gerekcesi**.

## K5 — Verify kollari

Sozlesmenin verify komutu `dotnet test -c Release --filter "PerformanceCheckTests|UpdaterTests"`.
Iki kolun her biri `--list-tests` ile ayri ayri sayildi; **ikisi de bos degil**:

| Kol | `--list-tests` sayisi |
|---|---|
| `PerformanceCheckTests` | **22** |
| `UpdaterTests` | **54** |

Ham listeler `.calisma/T145/list-PerformanceCheckTests.txt` ve
`.calisma/T145/list-UpdaterTests.txt` icinde.

Birlesik verify kosumu:

```
Başarılı!  - Başarısız: 0, Başarılı: 73, Atlanan: 3, Toplam: 76, Süre: 2 m 4 s
```

CI kosumu: **33733262807** (dal `T145-kalan-alti-bant-tur2`), `completed / success`.
https://github.com/Teknesyum/VidShrink/actions/runs/33733262807

76 = 22 + 54. Atlanan uc test `[LiveLauncherFact]` gecidinin arkasindaki uclu
(`VIDSHRINK_LAUNCHER_EXE` kurulu degil); bant 1'in kosumu bu gecit acilarak ayrica
yapildi.

## Kalan borc

1. `:915`/`:916` uretim tarafi olmadan daraltilamaz. Bandi gercekten kucultmek
   `SplashGate`in kapanis `Join` tavanini (2 sn) kisaltmayi ya da olcum icin paneli
   kapatan bir gecit acmayi gerektirir. `src/**` bu sozlesmeye kapaliydi.
2. `[LiveLauncherFact]` uclusu CI'da hic kosmuyor (`VIDSHRINK_LAUNCHER_EXE` kurulu degil).
   Bant 1'in yeni tavani bu yuzden CI'da sinanmiyor; yerelde yayimlanmis baslaticiyla
   sinandi.
3. `:462`in yeni payi (1,0) CI makinesinde (windows-latest, 4 cekirdek) olculmedi;
   yerel olcum 16 cekirdekli makineden. CI kosumu bunu ilk kez sinayacak.
