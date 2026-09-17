# NVENC Aşağı Deneme: Yüksek İlk Nişan, Aşınca Bir Aşağı

Dal: `t0/nvenc-asagi` (`origin/main` 0bc86188 üstü). Karar: `docs/danisma/2026-09-17-fable-kararlar.md` soru 2, aday 3.
Önceki iş: `butce-doldur.md` (aday 1 kaldı, aday 2 yalnız yazılımda kaldı), hücreler `nvenc-2.md`.

## Kural (Ölçümden Önce Yazıldı)

Kodlayıcı NVENC ise (`CodecModel.Vendor(codec) == Nvenc`) planın ilk nişanı bant merkezi (hedef < 10 MB'de
hedefin %96'sı) değil `BudgetFill.Aim` (hedefin %98,5'i). İlk deneme tavanı aşarsa var olan düzeltme kolu
(`PlanCalculator.Correct`, ölçülmüş verimle bant merkezine) bir aşağı deneme koşar; tavan bekçisi ve en küçük tavan
üstü teslim değişmez. Yazılım, QSV, AMF ve VideoToolbox ilk nişanı bugünkü gibi kalır (QSV/AMF bu makinede
ölçülemiyor, iddia NVENC'le sınırlı). Yazılımın yukarı denemesi (`BudgetFill`) değişmez; testle pimli.

## Kapı

Her hücre iki kol: **önce** (`origin/main` 0bc86188 Bench) ve **sonra** (bu dal Bench), aynı oturumda, aynı ölçerle,
sırayla, makinede her an tek ffmpeg. Düzenek `tools/butce-doldur/kos.ps1`.

| Kapı | Küme | Koşul |
|---|---|---|
| K1 dolum | 26 hücre | sonra kolunda ortalama teslim/hedef sapması ≥ −%2,5 (bu 26 hücrede önce kolu −%6,47; nvenc-2'nin 38 hücre ortalaması −%4,8) |
| K2 taşma | 26 + tutma | sonra kolunda hiçbir teslim hedefi aşmaz |
| K3 deneme | 26 hücre | sonra kolunda ortalama deneme ≤ 2,0. Ölçülen 2,04, yani iki hücrelik (51 → 53 deneme) fark; tek başına gürültü genişliğinde, hüküm K1/K4/K5'e dayanıyor |
| K4 kalite | 26 + tutma | hiçbir hücrede sonra VMAF-NEG ort − önce VMAF-NEG ort < −0,3 değil |
| K5 tutma dolum | tutma | sonra ortalama sapma ≥ önce ortalama sapma |
| K6 negatif | yazılım | libx264 `parlak` 1000 ve libx265 `orta` 2000 çıktıları önce/sonra md5 aynı |

Hücreler: `butce-doldur.md`'nin 26 NVENC hücresi. Tutma: nvenc-2'nin kalan 12 NVENC hücresi (\|sapma\| ≤ %3, bütçe
işlerinde kullanılmadı): hareketli av1 2000/3500, karanlik h264 2000, karanlik hevc 1000, orta av1 2000/3500,
orta h264 1000, orta hevc 600, parlak av1 2000/3500, parlak h264 2000, parlak hevc 3500.

Hepsi geçerse kod kalır. Biri kalırsa kod geri alınır, `butce-doldur.md` "donanımın basamaklı yanıtı, kapalı" yazar ve
bütçe konusu bir daha açılmaz. Kural sonuçtan sonra gevşetilmez.

## Sonuç: Kapı Kaldı, Kod Geri Alındı

Önce `bench-once` (0bc86188), sonra bu dalın d81d0019 Bench'i; aynı oturum, aynı ölçer, sırayla tek ffmpeg. Toplam
yerel kodlama 452,1 sn. Önce kolu `butce-doldur.md` aday 1'in önce ortalamasını yeniden üretti (−6,47).

| Kapı | Küme | Önce → sonra | Sonuç |
|---|---|---|---|
| K1 dolum | 26 | ort sapma −6,47 → −5,41 | **kaldı** (≥ −2,5) |
| K2 taşma | 26 + 12 | 0 → 0 | geçti |
| K3 deneme | 26 | ort 1,96 → 2,04 | **kaldı** (≤ 2,0) |
| K4 kalite | 26 + 12 | en kötü Δ −1,32 (parlak h264 1000), −0,63 (orta hevc 600), −0,36 (hareketli av1 2000) | **kaldı** |
| K5 tutma dolum | 12 | ort sapma −1,73 → −4,24 | **kaldı** |
| K6 negatif | yazılım | koşulmadı: hüküm K1/K3/K4/K5'te verildi, kod geri alındı | — |

26 hücrede ΔVMAF-NEG ort +0,052; tutmada −0,126.

| Hücre (26) | Sapma önce → sonra | Deneme | VMAF-NEG ort | p10 |
|---|---|---|---|---|
| hareketli av1 1000 | −11,07 → −12,58 | 3 → 3 | 87,81 → 87,52 | 77,36 → 77,49 |
| hareketli h264 1000 | −4,06 → −4,06 | 2 → 2 | 80,67 → 80,67 | 66,77 → 66,77 |
| hareketli h264 2000 | −5,67 → −1,19 | 2 → 2 | 92,67 → 93,15 | 82,86 → 83,53 |
| hareketli h264 3500 | −5,07 → −0,83 | 2 → 2 | 97,26 → 97,45 | 90,12 → 90,97 |
| hareketli hevc 1000 | −10,70 → −9,40 | 3 → 3 | 87,04 → 87,23 | 76,68 → 77,11 |
| hareketli hevc 2000 | −6,43 → −1,55 | 2 → 2 | 95,94 → 96,25 | 88,36 → 89,29 |
| hareketli hevc 3500 | −3,81 → −4,10 | 2 → 2 | 98,55 → 98,58 | 94,90 → 94,88 |
| karanlik av1 1000 | −3,07 → −2,31 | 1 → 1 | 86,84 → 87,01 | 81,00 → 81,35 |
| karanlik av1 2000 | −6,05 → −1,08 | 1 → 1 | 95,16 → 95,56 | 90,67 → 90,95 |
| karanlik av1 3500 | −3,37 → −4,21 | 1 → 2 | 98,54 → 98,49 | 95,30 → 95,06 |
| karanlik h264 1000 | −4,15 → −4,15 | 1 → 1 | 79,95 → 79,95 | 69,49 → 69,49 |
| karanlik h264 3500 | −6,94 → −3,13 | 2 → 2 | 96,43 → 96,69 | 92,08 → 92,28 |
| karanlik hevc 2000 | −5,31 → −4,58 | 2 → 2 | 93,74 → 93,84 | 88,61 → 88,87 |
| karanlik hevc 3500 | −3,51 → −4,13 | 2 → 2 | 97,68 → 97,61 | 94,17 → 94,01 |
| orta av1 600 | −9,05 → −8,74 | 2 → 2 | 92,05 → 92,04 | 89,20 → 89,42 |
| orta av1 1000 | −7,74 → −7,02 | 1 → 1 | 93,95 → 93,99 | 91,32 → 91,29 |
| orta h264 2000 | −4,12 → −3,71 | 2 → 2 | 93,55 → 93,58 | 90,91 → 90,93 |
| orta h264 3500 | −4,02 → −4,07 | 2 → 2 | 96,27 → 96,28 | 94,15 → 94,11 |
| orta hevc 1000 | −6,03 → −6,53 | 2 → 2 | 92,73 → 92,71 | 89,78 → 89,78 |
| orta hevc 2000 | −6,14 → −1,51 | 2 → 2 | 94,85 → 94,94 | 92,21 → 92,30 |
| orta hevc 3500 | −3,20 → −3,75 | 2 → 2 | 96,42 → 96,41 | 94,61 → 94,65 |
| parlak av1 1000 | −15,95 → −15,95 | 3 → 3 | 85,63 → 85,63 | 71,53 → 71,53 |
| parlak h264 1000 | −6,29 → −15,15 | 2 → 3 | 79,65 → 78,33 | 56,50 → 53,50 |
| parlak h264 3500 | −4,19 → −4,14 | 2 → 2 | 91,75 → 91,75 | 84,69 → 84,81 |
| parlak hevc 1000 | −18,44 → −9,60 | 3 → 3 | 82,91 → 83,67 | 67,11 → 70,29 |
| parlak hevc 2000 | −3,80 → −3,31 | 2 → 2 | 90,48 → 90,54 | 84,19 → 85,14 |

| Tutma (12) | Sapma önce → sonra | Deneme | VMAF-NEG ort | p10 |
|---|---|---|---|---|
| hareketli av1 2000 | −1,37 → −7,22 | 2 → 2 | 97,00 → 96,64 | 90,25 → 89,33 |
| hareketli av1 3500 | −2,72 → −3,17 | 2 → 2 | 98,85 → 98,83 | 95,83 → 95,77 |
| karanlik h264 2000 | −0,54 → −0,46 | 1 → 1 | 92,06 → 92,05 | 85,01 → 85,13 |
| karanlik hevc 1000 | −2,61 → −4,46 | 1 → 2 | 85,35 → 85,07 | 78,67 → 78,29 |
| orta av1 2000 | −1,68 → −4,61 | 1 → 2 | 95,96 → 95,92 | 93,44 → 93,27 |
| orta av1 3500 | −0,76 → −4,89 | 1 → 2 | 96,69 → 96,64 | 94,86 → 94,75 |
| orta h264 1000 | −2,43 → −2,43 | 2 → 2 | 90,75 → 90,75 | 87,13 → 87,13 |
| orta hevc 600 | −0,62 → −13,74 | 2 → 3 | 90,32 → 89,69 | 86,10 → 84,67 |
| parlak av1 2000 | −1,91 → −2,59 | 2 → 2 | 91,99 → 91,94 | 86,48 → 86,52 |
| parlak av1 3500 | −2,44 → −2,87 | 2 → 2 | 94,92 → 94,90 | 92,75 → 92,65 |
| parlak h264 2000 | −1,29 → −1,29 | 2 → 2 | 86,83 → 86,83 | 74,91 → 74,91 |
| parlak hevc 3500 | −2,39 → −3,10 | 2 → 2 | 93,97 → 93,92 | 91,14 → 90,89 |

Mekanizma, parlak h264 1000 izi: önce "967k → 1,584 MB aşım; 715k → 1,144 MB bantta" (2 deneme). Sonra "993k → 1,586 MB aşım;
734k → 1,25 MB aşım; tavan bekçisi 644k → 1,036 MB" (3 deneme, −%15,15). Aşağı deneme de tavanı aşınca bekçinin son
denemesi derin altta kalıyor. Tutmada önce ilk denemede bantta biten üç hücre (karanlik hevc 1000, orta av1 2000/3500) sonra
ilk denemede aştı, düzeltme bant merkezine indi (−%4,46..−%4,89, +1 deneme); orta hevc 600 iki kez aştı, bekçi −%13,74.

Hüküm: aday 3 kaldı. Kod (`PlanCalculator.FirstAimMb`, `NvencAsagiTests`, iki testin kodek değişikliği) 844b954f ile
geri alındı. Kod kaldığı sürece 17 testin her kolu mutasyonla kırmızıya döndü (M1-M6) ve CI 35248305258 yeşildi; bu
sayılar artık yalnız tarihçe.
