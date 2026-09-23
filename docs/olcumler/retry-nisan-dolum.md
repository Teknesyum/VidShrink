# Yeniden Deneme Nişanı Bütçe Doldurma Nişanına Çekildi

`butce-ikinci-kodlama.md` "Hüküm" bölümünün ölçülmemiş önerisinin ölçümü.

**Değişiklik** (`869b689f`): `PlanCalculator.RetryAimMb` ölçülmüş verim varken bant
merkezini (10 MB altında 0,96·T) değil `BudgetFill.Aim · T`'yi (0,985·T) döndürüyor.
Ölçülmemiş verim dalı değişmedi. `BudgetFill.Floor` 0,97, yani yeni nişan tabanın
üstünde ve T'nin altında.

**Ürün eşiği:** T'yi aşan teslim tek hücrede bile olursa değişiklik geçmez.

## Değişikliğin Etki Alanı

`RetryAimMb`'nin ölçülmüş dalı yalnız `PlanCalculator.Correct` üzerinden istenen kbit'i
değiştiriyor, yani **yalnız bir önceki deneme bandı kaçırdığında** koşan düzeltme
denemesini. İlk deneme ve bütçe doldurma denemesi (`BudgetFill.Plan`, kendi 0,985
nişanı) bu değişiklikten etkilenmiyor.

`EncodeRunner.cs:272` aynı yöntemi her denemenin iz satırındaki "nişan" alanı için de
çağırıyor; o değer yalnız `Iz(...)` kaydına gidiyor. Bu yüzden yeni koşumun izinde ilk
denemenin nişanı 0,703 yerine 0,721 (0,7324 MB hedefte) görünüyor, ama istenen kbit iki
koşumda da aynı: 587k, 1956k, 1522k, 1035k (Tablo 1'in altındaki izler).

## Pim ve Mutasyon

`tests/VidShrink.Tests/FillBandTests.cs`:

- `RetryAimTargetsTheBudgetFillAimWhenTheYieldIsMeasured` — literal beklenen:
  180 → 177,3; 25 → 24,625; 8 → 7,88.
- `RetryAimSitsBetweenTheBudgetFillFloorAndTheCeilingSoAnOnAimAttemptIsDelivered` —
  180 / 8 / 1 / 4,88 MB'da nişan literal, `nişan > 0,97·T`, `nişan < T` ve
  `BudgetFill.Wants(nişan, T, 2, 3, false)` **yanlış**: nişanını tutturan deneme bir
  tam kodlama daha istemiyor. Eski pim (`RetryAimStaysUnderTheBudgetFillFloor…`) bunun
  tersini pimliyordu, kaldırıldı.

Mutasyon: dönüş elle `return band.CenterMb;`'ye geri alındı, `-warnaserror` ile
0 uyarı 0 hata derlendi:

```
--filter FullyQualifiedName~FillBandTests.RetryAim
Failed!  - Failed:     7, Passed:     0, Skipped:     0, Total:     7
```

Elle geri konduktan sonra, aynı derleme bayraklarıyla:

```
--filter "FullyQualifiedName~FillBand|FullyQualifiedName~BudgetFill|FullyQualifiedName~PlanCalculator|FullyQualifiedName~Correct"
Passed!  - Failed:     0, Passed:   151, Skipped:     1, Total:   152
```

## Izgara Koşumu

```
gh workflow run handbrake-kiyas.yml --ref t0/retry-nisan-dolum --field isler=butceilk
35839839480 handbrake-kiyas t0/retry-nisan-dolum (869b689f)
```

Düzenek eski koşumla (35282699847, `9642686d`) aynı: dört kesit × 600/2000 kbit ×
x265/h264, HandBrake ürünün x265 çıktısına bayt eşlenerek. Eski koşumun artefaktları
`gh run download 35282699847` ile yeniden indirildi; özetleyici eski veride
`butce-ikinci-kodlama.md` Tablo 1'in satırlarını aynen verdi.

## Tablo 1 — Hücre Hücre Kıyas

Eski = 35282699847, yeni = 35839839480. "Teslim/T" teslim edilen denemenin çıkan
boyutu / hedef; "previous result delivered" dalında bir önceki denemenin boyutu.
"Doldurma" o hücrede koşan bütçe doldurma denemesi sayısı.

| Kesit | kbit | Kodek | Deneme eski | Deneme yeni | Toplam sn eski | Toplam sn yeni | Deneme sn eski | Deneme sn yeni | HB oranı eski | HB oranı yeni | Teslim/T eski | Teslim/T yeni | Doldurma eski | Doldurma yeni | T üstü deneme yeni | T üstü teslim yeni |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| karanlik | 600 | h264 | 1 | 1 | 61,1 | 36,6 | 11,3 | 7,1 | 1,09 | 1,15 | 0,974 | 0,974 | 0 | 0 | yok | hayır |
| karanlik | 600 | x265 | 2 | 2 | 175,7 | 111,6 | 109,4 | 65,5 | 3,15 | 3,52 | 0,979 | 0,979 | 1 | 1 | yok | hayır |
| karanlik | 2000 | h264 | 2 | 2 | 84,7 | 53,6 | 34,3 | 21,2 | 1,13 | 1,31 | 0,984 | 0,984 | 1 | 1 | yok | hayır |
| karanlik | 2000 | x265 | 2 | 2 | 288,4 | 151,2 | 213,5 | 106,3 | 3,86 | 3,69 | 0,977 | 0,978 | 1 | 1 | yok | hayır |
| parlak | 600 | h264 | 2 | 2 | 71,6 | 63,4 | 24,9 | 23,4 | 1,17 | 1,43 | 0,986 | 0,990 | 1 | 1 | yok | hayır |
| **parlak** | **600** | **x265** | **3** | **2** | 263,3 | 145,2 | 195,6 | 97,8 | 4,32 | 3,27 | 0,982 | 0,989 | 1 | 0 | yok | hayır |
| parlak | 2000 | h264 | 2 | 2 | 87,0 | 68,1 | 37,5 | 30,4 | 1,10 | 1,17 | 0,986 | 0,984 | 1 | 1 | yok | hayır |
| **parlak** | **2000** | **x265** | **3** | **2** | 382,9 | 207,8 | 316,9 | 158,9 | 4,86 | 3,58 | 0,983 | 0,985 | 1 | 0 | yok | hayır |
| hareketli | 600 | h264 | 2 | 2 | 73,7 | 74,0 | 23,1 | 23,4 | 1,20 | 1,20 | 0,983 | 0,984 | 1 | 1 | yok | hayır |
| hareketli | 600 | x265 | 2 | 2 | 193,3 | 198,9 | 113,4 | 114,0 | 3,15 | 3,23 | 0,990 | 0,990 | 1 | 1 | yok | hayır |
| hareketli | 2000 | h264 | 2 | 2 | 94,7 | 94,1 | 37,9 | 35,7 | 1,15 | 1,14 | 0,985 | 0,984 | 1 | 1 | yok | hayır |
| hareketli | 2000 | x265 | 2 | 2 | 332,2 | 326,2 | 228,8 | 226,3 | 4,04 | 3,96 | 0,993 | 0,993 | 1 | 1 | yok | hayır |
| **ekran** | **600** | **h264** | **3** | **2** | 41,8 | 38,8 | 10,8 | 7,4 | 2,27 | 2,30 | 0,983 | 0,974 | 1 | 0 | yok | hayır |
| ekran | 600 | x265 | 3 | 3 | 84,8 | 85,3 | 39,9 | 40,1 | 4,61 | 5,05 | 0,923 | 0,945 | 1 | 1 | 3: 0,319 MB (1,005·T) | hayır |
| ekran | 2000 | h264 | 4 | 4 | 50,1 | 67,1 | 17,8 | 24,5 | 2,77 | 3,67 | 0,988 | 0,997 | 1 | 1 | yok | hayır |
| ekran | 2000 | x265 | 4 | 4 | 116,7 | 117,5 | 71,3 | 71,4 | 6,45 | 6,42 | 0,990 | 0,992 | 1 | 1 | yok | hayır |

Toplamlar: deneme 39 → 36, bütçe doldurma denemesi 15 → 12. Ürünün kendi
`tavan_asildi` alanı yeni koşumda 16/16 `false`.

**Süre sütunları koşucu farkını taşıyor.** karanlık/600/h264 iki koşumda aynı işi
yaptı (1 deneme, 587k, 0,713 MB) ama deneme süresi 11,3 → 7,1 sn, HandBrake oranı
1,09 → 1,15. Süre farkları bu yüzden yalnız değişiklikten gelmiyor; deneme sayısı ve
Teslim/T koşucudan bağımsız.

### Yeni Koşumun Deneme İzleri

`no:dal:kbit:nişan->çıkan (çıkan/T)`, özetleyicinin ham satırlarından:

- karanlik/600/h264 (T=0,7324): 1:in band:587k:0,721->0,713 (0,974)
- karanlik/600/x265 (T=0,7324): 1:in band:587k:0,721->0,696 (0,950) / 2:budget fill, the fuller result delivered:608k:0,721->0,717 (0,979)
- karanlik/2000/h264 (T=2,4414): 1:in band:1956k:2,405->2,344 (0,960) / 2:budget fill, the fuller result delivered:2007k:2,405->2,403 (0,984)
- karanlik/2000/x265 (T=2,4414): 1:in band:1956k:2,405->2,308 (0,945) / 2:budget fill, the fuller result delivered:2037k:2,405->2,387 (0,978)
- parlak/600/h264 (T=0,7324): 1:in band:587k:0,721->0,699 (0,954) / 2:budget fill, the fuller result delivered:605k:0,721->0,725 (0,990)
- parlak/600/x265 (T=0,7324): 1:under band:587k:0,721->0,667 (0,911) / 2:in band:635k:0,721->0,724 (0,989)
- parlak/2000/h264 (T=2,4414): 1:in band:1956k:2,405->2,33 (0,954) / 2:budget fill, the fuller result delivered:2019k:2,405->2,403 (0,984)
- parlak/2000/x265 (T=2,4414): 1:under band:1956k:2,405->2,217 (0,908) / 2:in band:2122k:2,405->2,405 (0,985)
- hareketli/600/h264 (T=0,7324): 1:in band:587k:0,721->0,705 (0,963) / 2:budget fill, the fuller result delivered:600k:0,721->0,721 (0,984)
- hareketli/600/x265 (T=0,7324): 1:in band:587k:0,721->0,689 (0,941) / 2:budget fill, the fuller result delivered:614k:0,721->0,725 (0,990)
- hareketli/2000/h264 (T=2,4414): 1:in band:1956k:2,405->2,309 (0,946) / 2:budget fill, the fuller result delivered:2037k:2,405->2,402 (0,984)
- hareketli/2000/x265 (T=2,4414): 1:in band:1956k:2,405->2,302 (0,943) / 2:budget fill, the fuller result delivered:2043k:2,405->2,424 (0,993)
- ekran/600/h264 (T=0,3174): 1:under band:587k:0,313->0,276 (0,870) / 2:in band:666k:0,313->0,309 (0,974)
- ekran/600/x265 (T=0,3174): 1:under band:587k:0,313->0,29 (0,914) / 2:in band:633k:0,313->0,3 (0,945) / 3:budget fill over the target, the previous result delivered:659k:0,313->0,319 (1,005)
- ekran/2000/h264 (T=1,0579): 1:under band:1522k:1,016->0,593 (0,561) / 2:under band:1956k:1,042->0,869 (0,821) / 3:in band:2346k:1,042->1,022 (0,966) / 4:budget fill, the fuller result delivered:2392k:1,042->1,055 (0,997)
- ekran/2000/x265 (T=1,0579): 1:under band:1035k:1,016->0,438 (0,414) / 2:under band:1956k:1,042->0,828 (0,783) / 3:in band:2460k:1,042->1,015 (0,959) / 4:budget fill, the fuller result delivered:2524k:1,042->1,049 (0,992)

## Tablo 2 — Düzeltme Denemesinin Düştüğü Yer

Değişikliğin dokunduğu denemeler: bandı kaçıran bir denemeden sonra `Correct`'in
ölçülmüş verim dalıyla kurulan ve bandın içine düşen deneme. 6 hücrede var.

| Hücre | Deneme | kbit eski → yeni | Çıkan/T eski | Çıkan/T yeni | ≥ 0,97·T | Ardından doldurma |
|---|---|---|---|---|---|---|
| parlak/600/x265 | 2 | 619 → 635 | 0,963 | 0,989 | evet | yok |
| parlak/2000/x265 | 2 | 2066 → 2122 | 0,961 | 0,985 | evet | yok |
| ekran/600/h264 | 2 | 648 → 666 | 0,951 | 0,974 | evet | yok |
| ekran/600/x265 | 2 | 621 → 633 | 0,923 | 0,945 | hayır | var, T'yi aştı, teslim edilmedi |
| ekran/2000/h264 | 3 | 2292 → 2346 | 0,954 | 0,966 | hayır | var |
| ekran/2000/x265 | 3 | 2381 → 2460 | 0,937 | 0,959 | hayır | var |

Eski çıkan/T değerleri `butce-ikinci-kodlama-ham.md`'deki izlerden (0,705 / 0,7324;
2,345 / 2,4414; 0,302 / 0,3174; 0,293 / 0,3174; 1,009 / 1,0579; 0,991 / 1,0579).
Bu altı denemenin hiçbiri T'yi aşmadı; en yükseği 0,989·T.

## T Aşımı

- **T üstü teslim: 0/16.** Ürünün `tavan_asildi` alanı 16/16 `false`; Tablo 1'in
  Teslim/T sütununun en büyüğü 0,997.
- **T üstü deneme: 1.** ekran/600/x265'in 3. denemesi 0,319 MB (1,005·T). Bu bir
  bütçe doldurma denemesi (`BudgetFill.Plan`, bu değişiklikten bağımsız nişan) ve ürün
  onu teslim etmedi (`budget fill over the target, the previous result delivered`).
  Eski koşumda aynı hücrenin aynı dalı 0,322 MB ile T'yi aşmıştı.

## 0,97 Altında Kalıp Ek Kodlama Tetikleyen Düzeltme

Kaldı: 3 hücrede, üçü de ekran kesitinde (Tablo 2'nin "hayır" satırları). Düzeltme
denemesi 0,945·T, 0,966·T, 0,959·T'ye düştü, yani 0,985·T nişanının 0,960 – 0,981'i;
üçünde de bir bütçe doldurma denemesi koştu. Doğal kesitlerde (parlak) iki düzeltme
denemesi nişanın 1,004'ü ve 1,000'ı ile düştü ve ek kodlama koşmadı.

Değişiklik ilk denemesi bantta olan 9 hücrenin doldurma denemesine dokunmuyor; o
dokuz hücrede doldurma denemesi yeni koşumda da koştu.

## Hüküm

Ürün eşiği geçti: 16 hücrenin hiçbirinde T üstü teslim yok. Bu koşumda deneme sayısı
39'dan 36'ya indi; üç düzeltme denemesi 0,97·T'nin üstüne düşüp ek kodlamayı
kaldırdı, ekran kesitinde üç düzeltme denemesi 0,97·T'nin altında kaldı ve ek
kodlama yine koştu. Tek koşumluk ölçüm; süre kazancı koşucu farkından ayrılmadı.

## Ham Çıktı

Özetleyicinin yeni koşumdaki çıktısı olduğu gibi `docs/olcumler/retry-nisan-dolum-ham.md`
dosyasında; eski koşumunki `docs/olcumler/butce-ikinci-kodlama-ham.md`.

## CI

```
gh run list --branch t0/retry-nisan-dolum
```

| Koşum | İş Akışı | Sonuç |
|---|---|---|
| 35839839480 | handbrake-kiyas (`isler=butceilk`) | success |
| 35839833712 | ci | cancelled: 75 dk zaman aşımı |

35839833712 iptale kadar 363 test kırmızı gördü: 362'si `YerlesimDenetimiTests`, 1'i
`AdvancedPanelTests.TheCollapsedAdvancedSectionCostsNoMoreThanASectionHeader` (piksel ölçüsü:
sütun 954 / 846 px). Log'da `FillBand`, `BudgetFill` ya da `PlanCalculator` sınıfından kırmızı
yok. Aynı gün main'in ci koşumları da iptal oldu: 35830087284, 35836406925, 35839153666.
Dalın ci yeşili bu belge yazılırken alınamadı.
