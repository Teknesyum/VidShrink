# Bütçe Doldurma: Altta Kalınca Bir Yukarı Deneme

Dal: `t0/butce-doldur` (`origin/main` 34231f4d üstü). Kaynak: `nvenc-2.md`, ürün hedefin ortalama %4,8'ini boş bırakıyor
(−%0,54 .. −%14,81).

## Kök Neden (Kaynaktan)

1. `src/VidShrink.Core/PlanCalculator.cs:104-106` `FillBand.For`: hedef < 10 MB iken alt kenar hedefin %92'si, 10-50 MB
   arası %95'i. nvenc-2 hücreleri 10 sn kesit, 1,2-4,3 MB; −%3..−%7,7 teslim "bantta" sayılıp ilk denemede kabul ediliyor
   (`src/VidShrink.Ffmpeg/EncodeRunner.cs:210-214`). Bant donanımda kapalı değil; geniş.
2. `EncodeRunner.cs:99` `MaxAttempts = 3` ve `EncodeRunner.cs:278-283`: tavan üstü (+%27..42) → bant altı → tavan üstü
   izinde son deneme aşınca ilk bant altı yedek (−%8,7..−%14,8) teslim ediliyor; o yedeği dolduracak deneme kalmıyor.
3. `EncodeRunner.cs:207-208`: bant altı yeniden deneme koşu başına bir (ölçülmüş verimle iki) kez; sonrası
   `EncodeRunner.cs:368` "under band accepted".

## Değişiklik (Ölçümden Önce Yazıldı)

`src/VidShrink.Core/BudgetFill.cs`: `FillPolicy.FillTarget` koşusunda teslim edilecek sonuç hedefin %97'sinin altındaysa ve
bilerek durma, doygunluk, ölü verim ya da kullanıcı kararı değilse **bir** yukarı deneme koşulur. İstek, teslimin video
payını hedefin %98,5'ine ölçekler; koşuda bu isteğin üstünde tavanı aşmış bir örnek varsa iki nokta arası doğrusal
aradeğerle sınırlanır. Yukarı deneme tavanı aşarsa ya da daha küçük çıkarsa teslim edilmez, önceki sonuç teslim edilir.
Deneme bütçesi koşunun deneme sınırı + 1. Tavan bekçisinin (HB-2b) "en küçük tavan üstü sonuç" teslimine dokunulmaz.

## Karar Kuralı

Her hücre iki kol: **önce** (`origin/main` 34231f4d Bench) ve **sonra** (bu dal Bench), aynı oturumda, aynı ölçerle.

| Kapı | Koşul |
|---|---|
| K1 dolum | sonra kolunda teslim ≥ hedefin %97'si olan hücre oranı ≥ %80 |
| K2 tavan | sonra kolunda hiçbir teslim hedefi aşmaz |
| K3 deneme | ort(sonra deneme − önce deneme) ≤ +1 |
| K4 kalite | ort(sonra VMAF-NEG ort − önce VMAF-NEG ort) ≥ 0 |

Dördü de geçerse değişiklik kalır; biri kalırsa kod geri alınır ve sonuç burada yazılır. Kural sonuçtan sonra gevşetilmez.

## Hücreler

- NVENC: `nvenc-2.md` tablosunda \|yeni sapma\| > %3 olan 26 hücre (kesit × kodek × kbit).
- Yazılım (bant yazılımda da geçerli, `FillBand.For` kodek ayırmıyor): libx264 ve libx265 × `karanlik` 1000, `parlak` 1000,
  `hareketli` 2000 = 6 hücre.
- Negatif kol: sonra Bench'i `--fill qualityceiling` ile bir hücrede; yukarı deneme izde görünmemeli.

Düzenek: `tools/butce-doldur/kos.ps1`; `VidShrink.Bench shrink <kesit> <mb> --speed quality --no-measure --lock-codec <kodek>`,
ölçü `VidShrink.Bench measure-pair` (VMAF-NEG ort/p10). Kodlamalar sırayla, makinede tek ffmpeg.

## Aday 1 Sonucu: Kapı Kaldı

Tüm kodek yolları, 32 hücre (26 NVENC + 6 yazılım), toplam kodlama 578,9 sn. Önce kolu nvenc-2 sayılarını yeniden üretti
(ör. hareketli av1 1000 −11,07 / 3 deneme / 87,81).

| Küme | n | K1 ≥ %97 (önce → sonra) | K2 en büyük sapma | K3 Δdeneme | K4 ΔVMAF-NEG ort / p10 | Ort sapma önce → sonra |
|---|---|---|---|---|---|---|
| Tümü | 32 | 0 → 16 (%50) **kaldı** | −0,29 geçti | +0,97 geçti | +0,292 / +0,462 geçti | −6,22 → −3,36 |
| NVENC | 26 | 0 → 10 (%38) | −0,29 | +1,00 | +0,229 / +0,392 | −6,47 → −3,76 |
| Yazılım | 6 | 0 → 6 (%100) | −0,60 | +0,83 | +0,562 / +0,765 | −5,16 → −1,60 |

NVENC'te 26 yukarı denemenin 9'u tavanı aştı (önceki teslim kaldı), 4'ü aynı ya da daha küçük çıktı: istek +%2,7 iken
boyut +%4,8 (orta h264 2000, 1768k → 2,341 MB, 1815k → 2,453 MB), istek +%2,6 iken boyut değişmedi (hareketli h264 1000,
891k/914k → 1,171 MB). Tek denemelik %3 genişliğindeki pencere donanımın deneme başı sapmasından dar. Aday 1 kural gereği
geri alındı: donanım yolu aday 2 bekçisiyle 34231f4d davranışına döndü.

## Aday 2: Yalnız Yazılım Kodlayıcı (Aday 1'den Sonra, Ölçümden Önce Yazıldı)

Aynı karar, `CodecModel.Vendor(codec)` `Software` değilse (NVENC, QSV, AMF, ölçülmemiş VideoToolbox) yukarı deneme kurulmaz; donanım yolu 34231f4d ile aynı kalır (birim testle
pimli). Aday 1'in 6 yazılım hücresi aday 2'yle aynı kod yolundan geçti, geçerli sayılır. Uyum şüphesine karşı **yeni 6
tutma hücresi**: libx264 `orta` 2000, `karanlik` 3500, `hareketli` 1000, `parlak` 2000; libx265 `orta` 2000,
`karanlik` 3500.

Kapı: K1-K4 aynı eşiklerle hem 6 tutma hücresinde ayrı ayrı hem 12 yazılım hücresinin toplamında geçmeli. Biri kalırsa
aday 2 de geri alınır. NVENC hücreleri kapıya girmez; donanım davranışı değişmediği için "donanımda %4,8 boş bütçe"
açık kalır.

## Aday 2 Sonucu: Kapı Geçti

Önce `bench-once` (34231f4d), sonra aday 2 Bench'i; aynı oturum, aynı ölçer, sırayla tek ffmpeg. Ham satırlar ve deneme
izleri `.calisma/butce/aday2.json`'dan buraya taşındı. Bu turun toplam yerel kodlaması 849,8 sn (aday 1 dahil).

| Tutma hücresi | Bayt sapma önce → sonra | Deneme | VMAF-NEG ort | p10 |
|---|---|---|---|---|
| orta libx264 2000 | −4,55 → −0,24 | 2 → 3 | 93,17 → 93,39 | 90,83 → 90,94 |
| karanlik libx264 3500 | −4,15 → −1,57 | 1 → 2 | 95,97 → 96,16 | 91,06 → 91,44 |
| hareketli libx264 1000 | −4,14 → −1,54 | 1 → 2 | 75,05 → 75,91 | 62,98 → 64,80 |
| parlak libx264 2000 | −7,00 → −1,78 | 1 → 2 | 87,00 → 87,56 | 84,50 → 85,29 |
| orta libx265 2000 | −7,08 → −1,60 | 1 → 2 | 95,79 → 95,87 | 94,03 → 94,29 |
| karanlik libx265 3500 | −6,22 → −1,58 | 1 → 2 | 98,49 → 98,62 | 95,34 → 95,73 |

| Küme | n | K1 | K2 en büyük sapma | K3 Δdeneme | K4 ΔVMAF-NEG ort / p10 | Ort sapma önce → sonra |
|---|---|---|---|---|---|---|
| Tutma | 6 | 6/6 geçti | −0,24 geçti | +1,00 geçti (sınırda) | +0,340 / +0,625 geçti | −5,52 → −1,39 |
| Yazılım toplam | 12 | 12/12 geçti | −0,24 geçti | +0,92 geçti | +0,451 / +0,695 geçti | −5,34 → −1,49 |

Negatif kol: aday 2 Bench'i `--fill qualityceiling`, `parlak` libx264 2000: 1 deneme, iz yalnız "deneme 1: in band,
1956k, 2,276 MB" (−6,75); yukarı deneme kurulmadı.

K3 tutma kümesinde tam +1,00: her tetiklenen hücre bir deneme ekliyor, kural bunu izin veriyor ama pay yok. Donanımdaki
%4,8 boş bütçe açık: NVENC'in basamaklı hız yanıtı tek denemelik pencereye sığmıyor.
## Aday 3 Sonucu: Donanımın Basamaklı Yanıtı, Kapalı

Donanımda ilk nişan %98,5 + aşağı deneme (`nvenc-asagi.md`) kapıdan kaldı: 26 hücrede ort sapma −6,47 → −5,41 (kapı
≥ −2,5), deneme 1,96 → 2,04, parlak h264 1000 VMAF-NEG −1,32; tutma hücrelerinde dolum −1,73 → −4,24. Kod geri alındı.
Donanımdaki boş bütçe NVENC'in basamaklı hız yanıtından geliyor; bütçe konusu **kapandı**, bir daha açılmaz.
