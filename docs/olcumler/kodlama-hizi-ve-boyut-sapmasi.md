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
