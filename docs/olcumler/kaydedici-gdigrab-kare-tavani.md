# Kaydedici Gdigrab Kare Tavanı

Tarih 2026-09-26. Makine: 16 iş parçacığı, RTX 5070 Ti, birincil ekran 180 Hz. ffmpeg 9.0 (Gyan full_build).

Düzenek: `testsrc2` oynayan ffplay penceresi 300,300'de 1280x720; aynı dikdörtgen gdigrab ile 12 sn,
`libx264 -preset veryfast -crf 23`. ffmpeg CPU'su `-benchmark` utime+stime, DWM CPU'su süreç zamanı farkı,
kare sayısı ffprobe `nb_read_packets`. Her koşum tek, kısa; stres yükü yok.

| Koşum | İstenen fps | Gelen kare | fps | ffmpeg CPU | DWM CPU |
|---|---|---|---|---|---|
| önce (otomatik kip, 144+ Hz) | 120 | 976 | 81,3 | 0,50 s | 0,41 s |
| sonra (tavan 30) | 30 | 356 | 29,7 | 0,17 s | 0,06 s |
| önce, tekrar | 120 | 982 | 81,8 | 0,56 s | 0,30 s |
| sonra, tekrar | 30 | 357 | 29,8 | 0,22 s | 0,44 s |

İlk tur (aynı düzenek): 60 istenince 494 kare (41,2 fps), ffmpeg 0,45 s, DWM 0,16 s; 30'da 359 kare,
ffmpeg 0,36 s, DWM 0,09 s. ddagrab 60'ta 712 kare (59,3 fps), ffmpeg 0,28 s.

Okuma: 60 ve üstünde gdigrab istenen kareye yetişmiyor, yakalama döngüsü boşluksuz koşuyor; 30'da
istenen kare tam geliyor ve ffmpeg CPU'su yarıdan fazla düşüyor. DWM sayısı gürültülü: üç önce/sonra çiftinin
ikisinde düşüyor, sonuncusunda artıyor. Hızlı makinede mutlak sayılar küçük; yavaş makinede aynı döngü daha çok yer.
