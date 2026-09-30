# Yazılım Kodlayıcılarında Kodlama Hızı Ve Boyut Sapması

Durum: **ölçüldü, kod değişmedi** (yalnız Bench'e `--threads` ve çekirdek sınırı eklendi). Tek klip, tek hedef, tek koşum: bu bir
yön göstergesidir, dağılım değildir. **Hız sütunu makine yükü altında ölçüldü ve güvenilmez** (aşağıda).

## Düzenek

- Kaynak: Sintel 1080p'den `-c copy` ile kesilmiş 4,083 sn klip (1920x818, 24 fps, 98 kare, 7,881 MB), kesit 325. sn civarı (`hareketli`).
  Klip `.calisma/worktree-agent-aa96af72f31c91c13/klip4.mkv`; ölçüm bitince silindi.
- Hedef 2 MB. Kol ürünün kendi yolundan geçti: `--lock-codec` ile kodlayıcı kilitlendi, plan `PlanCalculator`, kodlama `EncodeRunner` döngüsü
  (preset slow / SVT-AV1 preset 6, 2 geçiş, FillTarget, en çok iki deneme). `--no-calibrate --no-measure`: kalibrasyon ve VMAF/XPSNR kapalı.
- Makine koruması: `VIDSHRINK_BENCH_CEKIRDEK=2` (Bench işlemi ve çocuk ffmpeg süreçleri 2 çekirdeğe kilitli, BelowNormal öncelik),
  `--threads 2` (ffmpeg `-threads 2`; x265 için `pools=2:frame-threads=2`, SVT-AV1 için `lp=2`). Tek süreç, sırayla.
- Komut (her kodlayıcı için, `<k>` = `libx264`, `libx265`, `libsvtav1`):
  `bench shrink klip4.mkv 2 --out o_<k> --lock-codec <k> --no-calibrate --no-measure --threads 2 --results r_<k>.json`
- Ham çıktı: Bench günlüğü ve `r_<k>.json`; sayılar oradan alındı.

## Sonuç

| Kodlayıcı | Hedef MB | Çıkan MB | Sapma % | Bant | Deneme | Kodlama sn | fps | Gerçek zaman katı |
|---|---|---|---|---|---|---|---|---|
| libx264 (slow) | 2 | 1,970 | -1,49 | içinde | 2 | 209,4 | 0,47 | 0,02 |
| libx265 (slow) | 2 | 1,953 | -2,35 | içinde | 2 | 103,1 | 0,95 | 0,04 |
| libsvtav1 (p6) | 2 | 1,842 | -7,92 | içinde | 2 | 32,6 | 3,01 | 0,125 |

Sapma = çıkan / hedef - 1, Bench'in `FillPercent` değerinden (98,51 / 97,64 / 92,08). Hiçbir kolda hedef aşımı yok, taban ihlali yok.
fps = kaynak kare (98) / kodlama süresi; süre **tüm denemeleri ve her iki geçişi** içerir (iki deneme x iki geçiş), yani tek geçiş fps'i değil,
ürün yolunun uçtan uca hızıdır. Kodlama süresine yoklama (prob) girmez; yoklama ayrı: x264 112,1 sn, x265 27,6 sn, SVT-AV1 22,7 sn.

## Okuma

- **SVT-AV1 kolu %5'i aşıyor:** hedefin %7,92 altı (1,842 MB). Bant içinde ama ilk deneme 1,842 MB çıktı; ikinci deneme 2,007 MB ile hedefi aştığı için
  Bench ilk sonucu teslim etti (`budget fill over the target, the previous result delivered`). x264 ve x265 ikinci denemede hedefe yaklaşıp teslim edildi.
- x264 ve x265 sapması %5'in altında (-1,49 ve -2,35).
- **Hız sayıları güvenilmez.** x264'ün x265'ten iki kat yavaş çıkması (0,47 ve 0,95 fps) beklenen sırayla çelişiyor; koşumdan sonra makinede
  benim işlemlerim yokken işlemci yüklemesi %100 okundu (başka işlemler çalışıyordu). Yoklama sürelerinin 112,1 / 27,6 / 22,7 sn dağılması da aynı yönde.
  Bu yüzden fps ve gerçek zaman katı yalnızca "2 çekirdekli, yüklü makinede bu koşum" değeridir; kodlayıcıların göreli hız sırası buradan çıkarılmaz.

## Ölçülmedi

Makine yüksüzken hız; tek geçiş fps'i; birden fazla klip, içerik (ekran, karanlık) ve hedef (2 MB dışı); 10 sn'e yakın klip; kalibrasyon açıkken
sapma (`--no-calibrate` kullanıldı); VMAF/XPSNR; 2 çekirdek dışı iş parçacığı sayısında hız; tekrar koşumla varyans.

## Düzeltme Sonrası

Durum: **kod değişti** (`BudgetFill.Bracket`, `EncodeRunner` bütçe doldurma kolu). Yukarı deneme hedefi aşar ve teslim edilen sonuç hedefin
%97'sinin (`BudgetFill.Floor`) altında kalırsa iki ölçüm hedefi köşeler; motor bir deneme daha ister, bit hızını iki noktadan doğrusal aradeğerle
`BudgetFill.AimFor` nişanına (0,985) koyar ve aşan denemenin bit hızının en az 1k altında tutar. Bu üçüncü deneme de hedefi aşarsa önceki sonuç
teslim edilir: tavan gevşemez. Köşeleme yoksa ya da teslim %97 ve üstündeyse ek deneme yok. Kural her kodlayıcıda aynı (`AimFor` planı olan
yazılım ve NVENC kolları). İkinci denemenin adım boyunu küçültmek ölçülmedi.

### Düzenek

- Klip yeniden kesildi: `-ss 325 -t 1.2 -map 0:v:0 -c copy` (demuxer 322,208 sn'deki anahtar kareye iner), 4,083 sn, 98 kare, 7,881 MB,
  yalnız görüntü. Sayılar ilk ölçümün klibiyle aynı. Ses taşıyan ilk kesit (8,203 MB) ölçümden çıkarıldı.
- "Önce" kolu: aynı dal, köşeleme kolu elle kapatılmış (`bracket = false`) Bench derlemesi; "sonra" kolu düzeltmenin kendisi.
- Komut: `bench shrink klip4.mkv <hedef> --lock-codec <k> --no-calibrate --no-measure --threads 2`, `VIDSHRINK_BENCH_CEKIRDEK=2`, tek süreç, sırayla.
  Toplam Bench süresi yaklaşık 9 dk.

### Sonuç

| Kol | Kodlayıcı | Hedef MB | Çıkan MB | Sapma % | Deneme | Kodlama sn | Denemeler (MB) |
|---|---|---|---|---|---|---|---|
| önce | libsvtav1 | 2 | 1,999 | -0,06 | 2 | 19,8 | 1,838 (bant altı) → 1,999 |
| önce | libsvtav1 | 3 | 2,939 | -2,04 | 2 | 17,4 | 2,785 → 2,939 |
| önce | libsvtav1 | 4 | 3,951 | -1,22 | 2 | 22,8 | 3,720 → 3,951 |
| sonra | libsvtav1 | 1,5 | 1,493 | -0,50 | 2 | 21,2 | 1,394 → 1,493 |
| sonra | libsvtav1 | 2 | 1,984 | -0,78 | 2 | 18,9 | 1,846 → 1,984 |
| sonra | libsvtav1 | 2,5 | 2,460 | -1,60 | 2 | 23,8 | 2,312 → 2,460 |
| sonra | libsvtav1 | 4 | 3,952 | -1,21 | 2 | 20,5 | 3,707 → 3,952 |
| sonra | libsvtav1 | 5 | 4,933 | -1,34 | 2 | 26,2 | 4,626 → 4,933 |
| sonra | libx264 | 2 | 1,969 | -1,56 | 2 | 19,2 | 1,920 → 1,969 |

Sapma = Bench `FillPercent` - 100. Hiçbir kolda hedef aşımı yok.

### Okuma

- **Köşeleme bu koşumlarda canlı tetiklenmedi.** Sekiz SVT-AV1 koşumunun hiçbirinde yukarı deneme hedefi aşmadı; ilk ölçümdeki 2,007 MB yeniden
  çıkmadı. Aynı 3743k istek bir koşumda 1,838 MB, ötekinde 1,846 MB verdi: SVT-AV1 koşumdan koşuma yaklaşık %0,4 oynuyor ve ilk ölçümün
  aşımı bu oynamanın ucunda. Düzeltmenin etkisi bu yüzden canlı ölçümle değil, sahte kodlayıcılı birim testiyle gösterildi (aşağıda).
- Canlı koşumlarda en büyük sapma -2,04 (önce, 3 MB); sonra kolundaki beş SVT-AV1 hedefinde en büyük sapma -1,60. Hepsi %3 içinde.
- x264 2 MB'da -1,56 ve 2 deneme: ilk ölçümdeki -1,49 ile aynı yerde, gerileme yok. Bu koşumda makine yüksüzdü; kodlama 19,2 sn (ilk ölçümde 209,4).
- Ek deneme maliyeti yalnız köşelemede ödenir: canlı koşumların hepsi 2 denemede kaldı.

### Birim Testi

`BudgetFillBracketTests` (ffmpeg'siz, `EncodeRunner.SahteKodlayici` bit hızından boyut yazar): 3600k → 1,842 MB, eğri üssü 1,3 (SVT-AV1 gibi
fazla tepki veren kodlayıcı). Yukarı deneme hedefi aşıyor, üçüncü deneme %97–100 arasına iniyor ve teslim ediliyor; üçüncü deneme de aşarsa
1,842 MB teslim ediliyor ve dosya tavanın altında; x264 kolunda yukarı deneme banda inince üçüncü deneme yok; teslim %97 üstündeyse ya da
yukarı deneme aşmadıysa `Bracket` plan vermiyor. Mutasyon (köşeleme kolu `false`): 4 testin 2'si kırmızı (iki pozitif kol), iki negatif kontrol yeşil.
