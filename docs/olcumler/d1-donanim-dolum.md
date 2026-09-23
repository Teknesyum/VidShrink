# D1: Donanım Kolunda Bütçe Doldurma Nişanı 0,985'e Çekildi

HandBrake durum tablosu satır 6, açık iş D1 (`docs/handbrake/acik-durumu-2026-09-17.md`).
Makine RTX 5070 Ti, ffmpeg 9.0, 23 Eylül 2026. Taban `c635394c`
(`t0/tur-birlesim-0923`, `RetryAimMb` 0,985·T nişanını içeriyor).

## Düzenek

`nvenc-4-teslim.md` ile aynı ızgara: üç 10 sn FFV1 kesit (`.calisma/nvenc-2/kesit-{karanlik,parlak,hareketli}.mkv`,
makinede duruyordu, yeni dosya indirilmedi), iki kodek (hevc_nvenc, av1_nvenc), üç hedef
(1000 / 2000 / 3500 kbit → 1,2207 / 2,4414 / 4,2725 MB). Ürün yolu `VidShrink.Bench shrink`,
`--speed quality --no-measure --lock-codec <kodek>`. VMAF ölçülmedi; bu işin ölçüsü teslim/hedef.

Yük sınırı: ürün ffmpeg'e `-threads` vermiyor ve Bench'te bayrağı yok. Eşdeğeri süreç yakınlığı:
her koşum `cmd /c start "" /affinity 3 /wait /b dotnet ... shrink ...` ile iki mantıksal çekirdeğe
bağlandı, ffmpeg alt süreçleri yakınlığı miras alıyor. Koşumlar sıralı, paralel yok.
Taban kolu 417 sn, aday kolu 415 sn, tek hücreli deneme 62 sn; toplam ≈ 15 dk.

```
dotnet build tools/VidShrink.Bench -c Release -m:2 -o .calisma/t0-d1-donanim-dolum/bin-taban
pwsh -NoProfile -File tools/d1-donanim-dolum/teslim.ps1 -Kollar taban -KesitDizini <kök>\.calisma\nvenc-2
# BudgetFill.NvencAim 0.97 -> 0.985
dotnet build tools/VidShrink.Bench -c Release -m:2 -o .calisma/t0-d1-donanim-dolum/bin-n0985
pwsh -NoProfile -File tools/d1-donanim-dolum/teslim.ps1 -Kollar n0985 -KesitDizini <kök>\.calisma\nvenc-2
```

Ham veri (her hücrenin deneme izi dahil) `docs/olcumler/d1-donanim-dolum-ham.json`.

## Bugünkü Boşluk

D1 satırındaki %4,8, `nvenc-4`'ten önceki fotoğraftı (kapı kapalı, düzeltme nişanı 0,96·T).
Bugünkü uçta taban kolu **ortalama 0,9750**, yani boşluk **%2,50**; eşik (%2) üstünde.
NVENC belirlenimci kaldı: karanlık/hevc/1000 bu koşumda da `nvenc-4` ile aynı 0,9868.

Tabanın izinden okunan kusur: NVENC yukarı denemesinin nişanı `BudgetFill.NvencAim` = 0,97, yani
`BudgetFill.Floor`'un kendisi. Yukarı deneme teslimi en iyi ihtimalle tabana taşıyordu; karanlık/hevc/2000'de
istek 1860k → 1865k'ye çıktı, dosya 2,358 MB'ta aynı kaldı ve atıldı ("came out smaller").
Aday: `nvenc-4-teslim.md`'nin ölçtüğü 0,985 kolu (orada da hedefi aşan hücre 0, ortalama en yüksek kol).

## Tablo

Teslim/hedef ve deneme sayısı. Fark yüzde puan.

| Kesit | Kodek | kbit | Taban (0,97) | Aday (0,985) | Fark |
|---|---|---|---|---|---|
| karanlik | hevc_nvenc | 1000 | 0,9868 / 1 | 0,9868 / 1 | 0 |
| karanlik | hevc_nvenc | 2000 | 0,9657 / 3 | 0,9695 / 3 | +0,38 |
| karanlik | hevc_nvenc | 3500 | 0,9766 / 2 | 0,9766 / 2 | 0 |
| karanlik | av1_nvenc | 1000 | 0,9492 / 2 | 0,9492 / 2 | 0 |
| karanlik | av1_nvenc | 2000 | 0,9886 / 2 | 0,9940 / 2 | +0,54 |
| karanlik | av1_nvenc | 3500 | 0,9686 / 2 | 1,0000 / 2 | +3,14 |
| parlak | hevc_nvenc | 1000 | 0,8740 / 4 | 0,8740 / 4 | 0 |
| parlak | hevc_nvenc | 2000 | 0,9535 / 4 | 0,9587 / 4 | +0,52 |
| parlak | hevc_nvenc | 3500 | 0,9840 / 2 | 0,9840 / 2 | 0 |
| parlak | av1_nvenc | 1000 | 0,9948 / 2 | 0,9948 / 2 | 0 |
| parlak | av1_nvenc | 2000 | 0,9892 / 2 | 0,9892 / 2 | 0 |
| parlak | av1_nvenc | 3500 | 0,9839 / 2 | 0,9839 / 2 | 0 |
| hareketli | hevc_nvenc | 1000 | 0,9979 / 4 | 0,9993 / 4 | +0,14 |
| hareketli | hevc_nvenc | 2000 | 0,9842 / 2 | 0,9842 / 2 | 0 |
| hareketli | hevc_nvenc | 3500 | 0,9965 / 2 | 0,9965 / 2 | 0 |
| hareketli | av1_nvenc | 1000 | 0,9955 / 4 | 0,9987 / 4 | +0,32 |
| hareketli | av1_nvenc | 2000 | 0,9959 / 1 | 0,9959 / 1 | 0 |
| hareketli | av1_nvenc | 3500 | 0,9653 / 4 | 0,9683 / 4 | +0,30 |

| Kol | Ort. teslim | Boşluk | En kötü | 0,97 altı hücre | Hedefi aşan teslim | Toplam deneme |
|---|---|---|---|---|---|---|
| taban (0,97) | 0,9750 | %2,50 | 0,8740 | 6 | **0 / 18** | 45 |
| aday (0,985) | **0,9780** | **%2,20** | 0,8740 | 5 | **0 / 18** | 45 |

Yedi hücre yukarı, hiçbir hücre aşağı, deneme sayısı aynı (yukarı deneme iki kolda da aynı
hücrelerde koşuyor; değişen yalnız isteği). En yakın teslim karanlık/av1/3500: 4,272399 MB,
hedef 4,2725 MB (hedefin 0,99998'i, altında). Hedefi aşan **deneme** iki kolda da bir tane:
parlak/hevc/1000'in yukarı denemesi (taban 1,235 MB, aday 1,240 MB); ürün ikisini de teslim
etmedi (`budget fill over the target, the previous result delivered`). Aşım şartı tuttu, aday kaldı.

## Kalan Boşluk

%2,20 hâlâ %2'nin üstünde; bu değişikliğin kapatamadığı üç hücre boşluğun çoğunu taşıyor:

- parlak/hevc/1000 (0,8740): iki tavan aşımından sonra tavan koruması (`CeilingGuard`) 610k'ye iniyor,
  0,874'te bitiyor; yukarı deneme 664k / 673k ile hedefi aşıyor ve atılıyor.
- karanlık/av1/1000 (0,9492): NVENC isteğe yanıt vermiyor; 967k → 994k / 1010k aynı 1,159 MB.
- parlak/hevc/2000 (0,9587): aynı tavan koruması yolu.

Üçü de nişanla değil, NVENC'in küçük hedefte isteğe tepkisiyle ilgili.

### İstek-Boyut Eğrisi Ölçüldü: Bant Hiçbir İstekle Tutmuyor

`t0/d1-tavan`, 23 Eylül 2026, aynı makine. Ürünün günlüğündeki komut (`komut:` satırı, aynı ölçek,
`-maxrate`/`-bufsize` = 2·istek, 2pass) istek değiştirilerek doğrudan koşuldu; iki çekirdek, sıralı:

```
pwsh -NoProfile -File tools/d1-donanim-dolum/egri.ps1 -Girdi <kök>\.calisma\nvenc-2\kesit-parlak.mkv -Olcek 1804:768 -Kodek hevc_nvenc -Kbitler 610,625,640,650,655,656,657,658,660,664,673,685,696 -HedefMb 1.2207
pwsh -NoProfile -File tools/d1-donanim-dolum/egri.ps1 -Girdi <kök>\.calisma\nvenc-2\kesit-parlak.mkv -Olcek 1882:802 -Kodek hevc_nvenc -Kbitler 1325,1400,1441,1459,1465,1470,1475,1480,1485,1491 -HedefMb 2.4414
pwsh -NoProfile -File tools/d1-donanim-dolum/egri.ps1 -Girdi <kök>\.calisma\nvenc-2\kesit-karanlik.mkv -Olcek 1882:802 -Kodek av1_nvenc -Onayar p6 -Kbitler 967,1010,1030,1035,1040,1045,1050,1070,1100,1150,1200,1300 -HedefMb 1.2207
```

Düzenek ürünün denemelerini bayt bayt yeniden üretiyor: 610k → 1,0668, 673k → 1,2398, 696k → 1,2514,
1325k → 2,1320, 1459k → 2,3407, 967k → 1,1509, 1010k → 1,1587 MB (hepsi ham json'daki izle aynı).

Teslim/hedef; bant 0,97–1,00:

| parlak/hevc/1000 | | parlak/hevc/2000 | | karanlık/av1/1000 | |
|---|---|---|---|---|---|
| 610k | 0,8740 | 1325k | 0,8733 | 967k | 0,9428 |
| 640k | 0,8839 | 1400k | 0,9408 | 1010k | 0,9492 |
| 650k | 0,8847 | 1441k | 0,9535 | 1030k | 0,9609 |
| **656k** | **0,8847** | 1459k | 0,9587 | **1040k** | **0,9618** |
| 657k | 1,0090 | **1470k** | **0,9633** | 1045k | 0,9618 |
| 664k | 1,0118 | 1475k | 1,0099 | 1050k | 1,0701 |
| 673k | 1,0157 | 1485k | 1,0116 | 1100k | 1,0847 |
| 696k | 1,0252 | 1491k | 1,0116 | 1200k | 1,2125 |

Üç hücrede de eğri **basamaklı**: 1k'lik istek artışı dosyayı tek adımda bandın altından hedefin
üstüne atıyor (656k → 657k: +0,152 MB, hedefin %12,4'ü; 1470k → 1475k: +0,114 MB, %4,7;
1045k → 1050k: +0,132 MB, %10,8). Basamağın boyu bandın genişliğinden (%3) büyük; bandın içine düşen
**hiçbir istek yok**. Basamaktan önce eğri düz: 640k–656k 1,079–1,080 MB, 1465k–1470k aynı 2,3518 MB.

Aradeğerleme ve tavan koruması kötü istek üretmiyor; ürünün istekleri ölçülen eğride doğru yerde:
parlak/hevc/1000'in dolum isteği 673k basamağın üstünde, ama basamağın altındaki en iyi istek (≤656k)
0,8847 veriyor, bugünkü 610k'den yalnız +1,07 puan. Ne isteğin kaynağı (doğrusal ya da üç örnekli
ikinci derece aradeğer ikisi de ≈672k verir) ne tavan korumasının nişanı bandı açar.

Kâhin sınırı (her hücrede basamağın altındaki en iyi isteği bilen bir kural):

| Hücre | Bugün | Ulaşılabilir en iyi | Fark |
|---|---|---|---|
| parlak/hevc/1000 | 0,8740 | 0,8847 | +1,07 |
| parlak/hevc/2000 | 0,9587 | 0,9633 | +0,46 |
| karanlık/av1/1000 | 0,9492 | 0,9618 | +1,26 |

18 hücre ortalamasına etkisi +0,155 puan: 0,9780 → **0,9795**, boşluk %2,05; en kötü hücre 0,8847'de
kalır. Kâhinsiz bir kural basamağın yerini tek ek denemeyle bilemez (düz bölge ile basamak ilk
örneklerin hiçbirinde görünmüyor), bu yüzden `ExtraAttempts = 1` altında kod değişmedi.
Karanlık/av1/1000'in "yanıt yok" görünümü doygunluk değil, aynı basamak: 1030k'de dosya büyüyor,
1050k'de hedefi %7 aşıyor.

Bandı açacak kol istek değil, NVENC oran denetiminin kendisi (`-maxrate`/`-bufsize` oranı, AQ, QP
aralığı); bu ölçümün kapsamı dışında.

## Değişiklik, Test Ve Mutasyon

`src/VidShrink.Core/BudgetFill.cs:9` `NvencAim` 0,97 → 0,985.

`tests/VidShrink.Tests/BudgetFillTests.cs`:

- `NvencteYukariDenemeYuzdeDoksanSekizBucugaNisanlanir` (3 kodek): literal 1039k ve gerekçede "aims at 0.985".
- `NvencOlculenHucredeYukariDenemeTavanOrnegiyleAradegerlenenYaziliminNisaniniIster`: ölçülen karanlık/hevc/2000
  hücresinin iki örneğiyle (1945k → 2,514 MB tavan üstü, 1860k → 2,3577 MB) istek literal 1885k (ölçümde
  ürünün istediği), libx265 ile aynı istek, 2,3674 MB'lık sonuç `Keeps`.
- `NvencteTavanUstuOrnekIstegiNvencNisaniylaAradegerler`: aradeğer 0,985 ile.
- Eski `NegatifKontrolYazilimNisaniNvencinkindenYuksek` kaldırıldı (artık eşitler; eşitliği ölçülen hücre testi pimliyor).

Derleme `dotnet build tests/VidShrink.Tests -c Release -warnaserror -m:2`: 0 uyarı, 0 hata.

Mutasyon: `NvencAim` elle 0,97'ye yazıldı, aynı bayraklarla derlendi:

```
dotnet test tests/VidShrink.Tests -c Release --no-build --filter "FullyQualifiedName~BudgetFill"
Failed!  - Failed:     5, Passed:    36, Skipped:     0, Total:    41
```

Kırmızı beşi: `NvencteYukariDeneme...` (3), ölçülen hücre testi (1865k ≠ 1885k), aradeğer testi.
Elle 0,985'e geri yazıldıktan sonra yeniden derlenip:

```
--filter "FullyQualifiedName~BudgetFill|FullyQualifiedName~FillBand|FullyQualifiedName~PlanCalculator|FullyQualifiedName~Correct"
Passed!  - Failed:     0, Passed:   151, Skipped:     1, Total:   152
```
