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
