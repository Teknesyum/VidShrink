# Karanlık İçerikte x265 Geçişi

Karar: `docs/danisma/2026-09-17-karanlik-x265-fable.md`. Dal `t0/karanlik-x265`, taban main `460ecc89`.

## 1. Dört Kesitte YAVG

Düzenek `tools/kalite-paketi-3/hb.ps1 -Is yavg`, handbrake-kiyas koşum #12 (commit `08b26026`).
Filtre ürün sondasıyla aynı: `fps=4,scale=160:-2,format=yuv420p,signalstats,metadata=print:key=lavfi.signalstats.YAVG`,
pencereler `ComplexityProbe.Windows` ile aynı (2 sn, <12 sn kesitte 2 pencere).

| Kesit | Süre | Pencere başları | Pencere YAVG | Pencere ort. | Tam kesit | Pencere ms (CI, ffv1) |
|---|---|---|---|---|---|---|
| karanlik | 10 | 2, 6 | 27,85 / 29,62 | **28,74** | 31,08 | 949 / 1174 |
| ekran | 4,333 | 0,583, 1,75 | 66,69 / 67,10 | **66,90** | 66,75 | 598 / 636 |
| hareketli | 10 | 2, 6 | 136,96 / 138,35 | **137,66** | 134,57 | 893 / 850 |
| parlak | 10 | 2, 6 | 205,32 / 188,75 | **197,04** | 185,87 | 1113 / 1110 |

Ölçer kontrolü (sentetik 1080p ffv1): siyah 16,02, beyaz 235,02 (8 bit sınırlı aralık 16/235).

Ayrım: en yakın komşu ekran, 66,90 / 28,74 = **2,33×** ≥ 2× → iş sürer.
Eşik: iki değerin geometrik ortası √(28,74 × 66,90) = 43,85 → **44** (`DarkContentSwitch.MeanLumaThreshold`).
Eşiğe pay: karanlik 44/28,74 = 1,53×, ekran 66,90/44 = 1,52×.

## 2. Negatif Kontroller (Mutasyon)

`KaranlikGecisTests` (19 ölçü), her mutasyonda derleme + filtreli koşum, sonra geri alma.
Düzenek `.calisma/mutasyon/kos.ps1`, sonuç trx'ten okundu.

| Mutasyon | Kırılan test |
|---|---|
| `requested == Auto` → `true` | AcikMaxCompressionKaranlikKaynaktaSvtKalir, SafKararHerKoluAyriTutar |
| kilit kolu silindi | KodekKilidiKaranlikKaynaktaDegismez (1 durum), SafKararHerKoluAyriTutar |
| rejim kolu silindi | SafKararHerKoluAyriTutar |
| motor kodeği (libsvtav1) kolu silindi | HizliKipteDonanimSecimineDokunulmaz, SafKararHerKoluAyriTutar |
| `IsDark` kolu silindi | KaranlikOlmayanKaynakSvtKalir (4), SafKararHerKoluAyriTutar |
| `<` → `<=` eşikte | EsikOlculenIkiKesitinGeometrikOrtasinda |
| `DarkCodecUsable` silindi | CalismayanX265SvtdeBirakir |
| `codec = libx265` ataması silindi | AutoSikiRejimdeKaranlikKaynakX265TurboyaGecer (2) |
| tercih kodeği `darkSwitch ? codec :` silindi | AutoSikiRejimdeKaranlikKaynakX265TurboyaGecer (2) |
| `TurboFirstPass \|= darkSwitch` (2 yer) silindi | AutoSikiRejimdeKaranlikKaynakX265TurboyaGecer (2) |
| sondada `with { MeanLuma = meanLuma }` silindi | SondaKaranlikKlibiEsigeGoreAyirir (2) |
| ayrıştırıcı anahtarı YAVG → YMIN | AyristiriciSabitSignalstatsSatirlariniOrtalar, SondaKaranlikKlibiEsigeGoreAyirir (2) |
| geri alınmış | 19/19 geçti |
| pencerede `DarkContentHevc` kolu `when false` (ayrı koşum) | GerekceNotuPencereninSatirinaVeKirkIkiDileCevrilir, OluUyeTests.TheZeroConsumerSetIsThePinnedSet |

Pencere kolu ilk CI koşumunda (#843) eksikti: `OluUyeTests` yeni kodu sıfır tüketicili üye olarak yakaladı.
Kol ve 42 dilde `main.reason.dark-content-hevc` eklendi; dokunulan alan (5 sınıf) 99/99 geçti.

Rejim kolu tümleşik planda motor kodeği koluyla örtüşür (libsvtav1'i yalnız Aggressive/Extreme seçer);
yalnız saf fonksiyon testi onu tek başına öldürür.

## 3. Sondanın Açılış Maliyeti (Yerel)

20 sn 1080p30 x264 klip (testsrc2), `bench shrink klip 5 --speed quality --no-measure --plan-only`,
main `460ecc89` ile dal sırayla, beş tur. Düzenek `.calisma/sonda/olc.ps1`.

| Tur | e0 prob (s) | dal prob (s) | dal prob-ms | e0 süreç ms | dal süreç ms |
|---|---|---|---|---|---|
| 1 | 3,2 | 3,1 | 3065 | 7332 | 6537 |
| 2 | 3,0 | 3,1 | 3062 | 6542 | 6487 |
| 3 | 3,6 | 3,2 | 3196 | 7155 | 6913 |
| 4 | 2,9 | 3,0 | 3050 | 6381 | 7331 |
| 5 | 3,1 | 3,2 | 3191 | 6592 | 6975 |
| medyan | 3,1 | 3,1 | 3065 | 6592 | 6913 |

Dalda luma 126,08, kodek iki tarafta libsvtav1. Sonda duvar süresinde fark ölçüm gürültüsünün içinde:
YAVG pencereleri diğer sonda işleriyle paralel koşuyor. Tek başına koşulan üç pencere 111 / 83 / 139 ms
(toplam 333 ms, sıralı); paralelde duvar süresine eklenen kısım bu klipte ayırt edilemedi.

## 4. CI: Ürün Otomatik, main e0 ve HandBrake x265

handbrake-kiyas koşum #13 (id 35190861711, commit `961795fc`), `hb.ps1 -Is karanlikgecis`.
e0 = main `460ecc89` bench'i aynı koşucuda. HB = HandBrakeCLI 1.11.2 x265 slow, 2 geçiş turbo, ürünün kbps'ine eş bayt.
Kalite yolu (ii) `zscale dither=none`. Ham çıktı: koşumun `hb-sonuc-karanlikgecis-*` eserleri.

### Karanlik

| kbit | Kol | Kodek | Geometri | kbps | CAMBI(ii) | VMAF-NEG | XPSNR | Kodlama sn | Toplam sn |
|---|---|---|---|---|---|---|---|---|---|
| 600 | ürün | libx265 | 1574x670 | 588,6 | **6,74** | 77,93 | 35,83 | 39,9 | 95,5 |
| 600 | e0 | libsvtav1 | 1920x818 | 603,9 | 9,21 | 81,22 | 36,68 | 25,0 | 66,9 |
| 600 | HB x265 | x265 | 1920x818 | 589,9 | 6,52 | 76,83 | 35,43 | 55,7 | 55,7 |
| 2000 | ürün | libx265 | 1920x818 | 1942,5 | **6,61** | 95,68 | 39,67 | 73,2 | 138,2 |
| 2000 | e0 | libsvtav1 | 1920x818 | 1981,0 | 9,27 | 96,08 | 39,90 | 25,7 | 75,6 |
| 2000 | HB x265 | x265 | 1920x818 | 1925,0 | 6,56 | 95,18 | 39,48 | 72,6 | 72,6 |

Hükümler: kodek libx265 (günlük ve komut) 2/2 geçti; CAMBI(ii) ≤7,5 2/2 geçti.
Süre, betiğin ölçtüğü kodlama süresiyle ürün/HB 0,716× ve 1,008× → geçti.
Toplam süreyle (sonda, deneme, iki geçiş dahil) 1,715× ve 1,904× → **1,5×'i aşıyor**; HB'nin toplamında sonda yok, bu yüzden betik kodlama süresini kıyaslar.
Ürün e0'a göre: VMAF-NEG −3,29 / −0,40, XPSNR −0,86 / −0,23; toplam süre 1,60× / 2,85× (kararın bilinen bedeli).
600'de ürün x265 kolu 1574x670'e ölçekledi, e0 SVT tam boyda kaldı.

### Negatif: Karanlık Olmayan Kesitler, 2000 kbit

| Kesit | luma | Ürün kodek | md5 eş | ΔVMAF-NEG (ürün−e0) | ΔXPSNR | Hüküm |
|---|---|---|---|---|---|---|
| ekran | 66,89 | libsvtav1 | hayır | +0,0003 | +0,0004 | geçti |
| hareketli | 137,66 | libsvtav1 | hayır | −0,0147 | +0,0073 | geçti |
| parlak | 197,04 | libsvtav1 | hayır | −0,0102 | −0,0137 | geçti |

Son komut satırları e0 ile aynı (yol dışında), bayt farkı SVT-AV1'in çok iş parçacıklı kodlamasından; tolerans |ΔVMAF| ≤0,05, |ΔXPSNR| ≤0,02.

### Sondanın CI Maliyeti

| Koşum | e0 prob s | ürün prob s | fark |
|---|---|---|---|
| ekran 2000 | 17,6 | 24,6 | +7,0 |
| hareketli 2000 | 28,3 | 35,3 | +7,0 |
| parlak 2000 | 29,2 | 40,9 | +11,7 |
| karanlik 600 | 28,2 | 35,0 | +6,8 |
| karanlik 2000 | 36,3 | 29,4 | −6,9 |

Medyan +7,0 sn, dağılım −6,9…+11,7. Her çiftte ürün önce koştu (sıra dengelenmedi); 2 çekirdekli koşucuda ffv1 1080p kaynakta
luma pencereleri diğer sonda işleriyle yarışıyor. Yereldeki gürültü içi sonuç CI'da tekrarlanmadı; açık boşluk.

## 5. Açıklar: Öneri Kodeği, HDR, Luma Kolunun Maliyeti

Dal `t0/karanlik-acik`.

**Luma kolunun maliyeti (koddan).** Üretim planı `SamplingPlan.Fixed`, üç pencere (`MaxWindows`), pencere 2 sn.
Önceki hâlde her pencere için **ayrı bir ffmpeg süreci** (`LumaArgs`) kaynağı tam çözünürlükte yeniden çözüp
`fps=4,scale=160:-2,format=yuv420p,signalstats` koşuyordu: bölünmüş sondanın zaten çözdüğü aynı 2 sn, üç
ek çözüm ve üç süreç açılışı; paralel koştuğu için 2 çekirdekli koşucuda diğer sonda işleriyle yarışıyordu
(bölüm 4: medyan +7,0 sn prob). Şimdi luma, bölünmüş sondanın `filter_complex`'ine üçüncü kol
(`split=3 … [lraw]<LumaFilter>[luma]`, `-map [luma] -f null -`). Ayrı süreç yalnız bölünmüş sonda
başarısız olursa yedek olarak koşar.

**Yerel süre (tek süreç, sırayla).** 8 sn 1080p30 testsrc2 (`eq=brightness=-0.3`), pencere `-ss 2 -t 2`,
veryfast, ffmpeg 9.0. Düzenek `.calisma/karanlik-acik/sure.ps1` (silindi), üç tur:

| Tur | eski bölünmüş (split=2) ms | ayrı luma süreci ms | yeni bölünmüş (split=3) ms |
|---|---|---|---|
| 1 | 269 | 76 | 240 |
| 2 | 225 | 79 | 225 |
| 3 | 221 | 78 | 228 |

Pencere başına ayrı süreç ~78 ms, luma kolunun bölünmüş sürece eklediği ≤7 ms (gürültü içinde); üç pencerede
~230 ms tek çekirdek işi ve üç süreç gitti. Sonuç değişmedi: luma 52,2645125 üç yolda aynı (8 değer),
`frame=60` aynı, full/half bayt 1399196 / 189003 eski ve yeni bölünmüşte aynı. `-hwaccel auto`'lu (Hızlı kip)
bölünmüş sonda da 52,2645125 okudu. Pim: `KaranlikGecisTests.BirlesikSondaAyriSondaylaAyniLumayiVeKareyiOkur`
(iki pencerede `LumaSampleAsync` == `WindowSample.MeanLuma`, kare sayısı tek örnekle eş).

Bu klipte kazanç HandBrake'e karşı 1,72× / 1,90× toplam farkını kapatacak büyüklükte değil. Bölüm 4'te ürünün
toplamı ile kodlaması arasındaki 55,6 / 65,0 sn'nin ~35 / ~29 sn'si prob; geri kalan sonda işlerinin (bias
taraması, üç x264 pencere, hareket örneği) payı ayrı ayrı ölçülmedi.

**Öneri kodeği.** `StrategyAdvice.SuggestedCodec` geçişten sonra `libsvtav1` kalıyordu (plan `libx265`).
Pencere kodlayıcı satırı `plan.Codec`'i, gerekçe satırı nottaki kodekleri yazdığı için ekranda yanlış kodek
görünmüyordu; yanlış olan çekirdeğin öneri kaydıydı. Artık öneri aynı kararı (`DarkContentSwitch.Applies`
+ x265 kullanılabilir) uygular.

**HDR.** `DarkContentSwitch.IsHdrSource`: `IsHdr` ya da aktarım `smpte2084` / `arib-std-b67` → geçiş yok.

**Negatif kontroller.** Düzeltmeden önce yeni testler 8 kırmızı (öneri 2, HDR 4, bölünmüş sonda 2). Mutasyonlar
(tek derlemede, birbirinden ayrık testler): öneri helper'ı hep `suggested` döner → iki öneri testi kırmızı;
`IsHdrSource` aktarım kıyası büyük/küçük harfe duyarlı → `HdrBayragiAktarimAdiOlmadanDaGecisiDurdurur` kırmızı;
bölünmüş koldaki filtre `fps=5` → iki sonda testi kırmızı. 5 kırmızı / 26 yeşil; geri alınca 98/98 yeşil
(`KaranlikGecisTests|ComplexityProbeTests|PlanCalculatorTests`).

**CI (handbrake-kiyas koşum 35246664536, commit `fced8c89`, `hb.ps1 -Is karanlikgecis`).** Ham çıktı koşumun
`hb-sonuc-karanlikgecis-*` eserleri. Bu dal main `0bc86188` üstünde; main'e bölüm 4'ten sonra bütçe doldurma
(`t0/butce-doldur`) girdi, bu yüzden ürün kolu e0 ile artık eş değil ve temiz bir luma A/B'si değil.

| Kesit | kbit | Kodek | luma | prob s (#13 → bu) | Deneme ürün/e0 | Kodlama sn ürün / HB | Toplam sn ürün |
|---|---|---|---|---|---|---|---|
| karanlik | 600 | libx265 | 28,73 | 35,0 → 34,5 | 2 / 1 | 78,4 / 55,6 (1,41×) | 134,5 |
| karanlik | 2000 | libx265 | 28,73 | 29,4 → 28,2 | 2 / 1 | 155,6 / 74,1 (**2,10×**) | 218,5 |
| hareketli | 2000 | libsvtav1 | 137,66 | 35,3 → 42,0 | 2 / 1 | 53,3 / – | 109,2 |
| parlak | 2000 | libsvtav1 | 197,04 | 40,9 → 34,7 | 4 / 3 | 96,4 / – | 144,0 |
| ekran | 2000 | libsvtav1 | 66,89 | 24,6 → 15,1 | 4 / 3 | 24,4 / – | 47,3 |

Luma değerleri bölüm 1 ile aynı, kodek hükmü 5/5 ve CAMBI(ii) hükmü 2/2 geçti. Prob farkı −9,5…+6,7 sn, medyan
−1,2 sn: koşucu gürültüsü içinde, luma kolunun CI kazancı bu koşumla ayırt edilemiyor. Süre hükmü 2000'de
**kaldı**: ürünün son dalı "budget fill, the fuller result delivered", yani bütçe doldurma x265'te ikinci bir tam
iki geçişli kodlama ekliyor (600'de 1,41×, 2000'de 2,10×). Negatif kesitlerde (hareketli, parlak) `negatif_hukmu`
aynı nedenle kaldı: ürün bir deneme fazla koştu, e0 `460ecc89`'de bütçe doldurma yok. Bu bu dalın açığı değil;
ayrı iş.

**Yedek luma yolu.** Bölünmüş sonda luma döndürmezse (yarım boy <64 olan küçük kaynak, `half: null` tam sonda ya da
başarısız bölünmüş sonda) ayrı süreç koşar (`WindowLumasAsync`). Testler `YarimBoyuOlmayanKucukKaynaktaAyriSondaLumayiOlcer`
(100x100 kaynakta `RunDetailedAsync` MeanLuma dolu) ve `BirlesikSondaLumasizDonerseAyriSondaDoldurur`. `?? await LumaSampleAsync`
silinince ikisi kırmızı (2 / 66), geri alınca 68/68 yeşil (`KaranlikGecisTests|ComplexityProbeTests`).

## 6. Dengeli Kolu (K4 Kapısı)

Karar: `docs/danisma/2026-09-17-fable-kararlar.md` bölüm 4. Kapı şöyle yazılmıştı: karanlik × {600, 2000}
Dengeli kolunun CAMBI(ii)'si ölçülür, **> 7,5** ise `DarkContentSwitch` rejim kümesine `Balanced` eklenir,
**≤ 7,5** ise kapsam bugünkü gibi kalır. Genişleme ayrıca "süre ≤ 2× libx264 kolu" ölçütüne bağlıydı.

Düzenek: `hb.ps1 -Is karanlikgecis` içine `urun-dengeli` kolu. Rejim kaynak/hedef oranından türediği için
(`CompressionStrategy.RegimeFor`) kol oranı `--source-mb` ile hedefin **3,0 katına** sabitliyor: 1,5 ≤ 3,0 < 6,0,
yani bandın ortası. Dal `t0/karanlik-dengeli` (`22bb3840`), CI koşumu **35273973330**, kesit ve kaynak
bölüm 4 ile aynı (Sintel sha256 `97F1DBC6…`). Kalite yolu (ii) `zscale dither=none`.

### Karanlik, Dört Kol

| kbit | Kol | Kodek | Geometri | kbps | CAMBI(ii) | VMAF-NEG(ii) | XPSNR(ii) | Kodlama sn | Toplam sn | Deneme |
|---|---|---|---|---|---|---|---|---|---|---|
| 600 | ürün (Auto→Extreme) | libx265 | 1574x670 | 601,4 | 6,55 | 78,36 | 35,86 | 79,5 | 139,4 | 2 |
| 600 | e0 `460ecc89` | libsvtav1 | 1920x818 | 603,4 | 9,24 | 81,20 | 36,68 | 25,2 | 67,4 | 1 |
| 600 | HB x265 | x265 | 1920x818 | 600,8 | 6,45 | 77,28 | 35,49 | 70,7 | 70,7 | – |
| 600 | **Dengeli** | **libx264** | 1344x572 | 598,5 | **7,79** | 66,42 | 34,92 | 11,3 | 45,5 | 1 |
| 2000 | ürün (Auto→Extreme) | libx265 | 1920x818 | 2003,0 | 6,65 | 95,93 | 39,77 | 141,1 | 204,4 | 2 |
| 2000 | e0 `460ecc89` | libsvtav1 | 1920x818 | 1979,5 | 9,29 | 96,05 | 39,89 | 26,0 | 67,7 | 1 |
| 2000 | HB x265 | x265 | 1920x818 | 1974,3 | 6,50 | 95,37 | 39,57 | 74,0 | 74,0 | – |
| 2000 | **Dengeli** | **libx264** | 1920x818 | 2018,0 | **7,46** | 90,27 | 39,17 | 34,1 | 72,2 | 2 |

Dengeli kolunda luma iki hücrede de 28,73 (bölüm 1 ile aynı), kodek hükmü (beklenen `libx264`) **2/2 geçti**:
eşik 44'ün altındaki bir kaynakta bile Balanced rejimi x265'e geçmiyor, yani bugünkü kapsam koda uygun.

### Kapı

| kbit | Dengeli CAMBI(ii) | Tavan | Kapı | x265 − Dengeli CAMBI | x265 ÷ Dengeli kodlama süresi | Genişleme süre ölçütü (≤2×) |
|---|---|---|---|---|---|---|
| 600 | 7,7867 | 7,5 | **genislet** | −1,2361 | **7,035×** | kaldı |
| 2000 | 7,4646 | 7,5 | **kalsin** | −0,8176 | **4,138×** | kaldı |

**Hüküm: kapsam değişmiyor, `Balanced` rejim kümesine eklenmiyor.** İki gerekçe, ikisi de tablodan:
CAMBI kapısı iki hücreden **yalnız birinde** (600) açıldı ve orada da payı 0,29 (7,79 karşı 7,5); 2000'de
7,46 ile tavanın 0,04 altında kaldı. Kapının açıldığı hücrede bile genişlemenin kendi süre ölçütü tutmuyor:
x265 kolu libx264'ün **7,04 katı** kodluyor, ölçüt ≤2×. 2000'de oran 4,14×, aynı yönde. Yani x265 geçişi
karanlıkta bandı gerçekten düzeltiyor (CAMBI 7,79 → 6,55 ve 7,46 → 6,65) ama Dengeli rejimde bu düzeltmenin
bedeli ölçütün üç-dört katı süre.

600'deki süre oranı salt kodek farkı değil: o hücrede Dengeli 1344x572'ye ölçekledi, x265 kolu 1574x670'te
kaldı ve bir deneme fazla koştu (bütçe doldurma). 2000'de iki kol da 1920x818 ve iki denemeli; oradaki
**4,138×** daha temiz sayı.

### Dengeli Kolu HandBrake'i Geçiyor Mu

Aynı kbps'te HandBrake x265 (slow, 2 geçiş turbo) ile karşılaştırma; Dengeli eksi HB:

| kbit | ΔCAMBI(ii) | ΔVMAF-NEG(ii) | ΔXPSNR(ii) | Dengeli ÷ HB kodlama süresi |
|---|---|---|---|---|
| 600 | +1,3351 | −10,8640 | −0,5670 | 0,160× |
| 2000 | +0,9653 | −5,1067 | −0,3982 | 0,461× |

**Dengeli kolu karanlık sahnede HandBrake'i geçmiyor: iki hücrenin ikisinde de üç kalite ölçüsünün üçü de
HB'nin gerisinde** (CAMBI'de yüksek = kötü, VMAF-NEG ve XPSNR'de düşük = kötü). Kaldığı hücre bir tanesi
değil, hepsi. Kazandığı tek sütun süre: HB'nin 0,160 ve 0,461 katı kodluyor.

600'deki −10,86 VMAF-NEG farkı yalnız kodek farkı değil; Dengeli o hedefte 1344x572'ye ölçekliyor, HB
kaynağın 1920x818'inde kodluyor. 2000'de iki kol aynı geometride ve fark −5,11.

### Yan Okumalar

Ürünün x265 kolu ile HB arasındaki süre hükmü bu koşumda **1/2**: 600'de 1,124× (geçti), 2000'de 1,907×
(kaldı, ölçüt ≤1,5×). Bölüm 5'te 1,41× / 2,10× ölçülmüştü; neden aynı, bütçe doldurmanın x265'te eklediği
ikinci tam kodlama. Defterde ayrı iş olarak duruyor.

CAMBI(ii) hükmü (x265 kolu ≤7,5) yine **2/2 geçti**: 6,55 ve 6,65.

### Düzeneğin Yerel Doğrulaması

`--source-mb` gerçekten rejimi çeviriyor mu: 3 sn 640x360 karanlık testsrc2 klibi (luma 11,29), aynı hedef
(0,7324 MB), yalnız `--source-mb` değişiyor.

| `--source-mb` | Oran | Rejim | Seçilen kodek |
|---|---|---|---|
| 2,1972 | 3,0 | Balanced | libx264 |
| 30 | 41,0 | Extreme | libx265 |

Pim: `HbOlcumDuzenegiTests.DengeliKolununZorladigiOranMotorunDengeliBandinaDusuyor` betikteki `$DengeliOran`
varsayılanını okuyup motora soruyor; 3,0 → 8,0 mutasyonunda test kırmızı (1/4), geri alınca 4/4 yeşil.
