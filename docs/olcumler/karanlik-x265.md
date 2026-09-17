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

Bu klipte kazanç HandBrake'e karşı 1,72× / 1,90× toplam farkını kapatacak büyüklükte değil; farkın gövdesi
luma dışındaki sonda işleri (bias taraması, üç x264 pencere, hareket örneği) ve deneme kodlaması.

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
