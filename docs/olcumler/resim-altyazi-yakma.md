# Resim Altyazıyı Görüntüye Yakma — 7 Ekim 2026

`--yak N` (HandBrake `--subtitle-burned`) resim tabanlı altyazıda (PGS, VOBSUB, DVB: yazı değil
hazır resim taşıyan altyazı türleri) bugüne dek `error.bad-burn` ile reddediliyordu. `subtitles`
süzgeci bunları çizemiyor; ffmpeg'de yol `overlay` (bir görüntüyü ötekinin üstüne bindiren süzgeç).
ffmpeg 9.0 (gyan full), aynı anda tek süreç, `-threads 2`, libx264 ultrafast, CRF 30, 320x240.

Ölçen sınıf `tests/VidShrink.Tests/ResimAltyaziYakmaTests.cs`. "Sınıf" yazan satırlar onun canlı
kollarının yazdığı sayılar; "elle" yazanlar bu turda tek tek koşulan ffmpeg komutlarından, sınıfta
kolu yok.

## Kaynak Ve Ölçü

İnternetten dosya indirilmedi. ffmpeg 9.0'da PGS kodlayıcı yok, `dvdsub` kodlayıcı da SRT'den
resim üretmiyor. PGS akışı (`.sup`) testin içinde bayt bayt yazılıyor: tek olay, 64x16 düz beyaz
kutu, 320x240 tuvalde x 128–191, y 200–215. VOBSUB ve DVB kaynakları o dosyadan `-c:s dvdsub` /
`-c:s dvbsub` ile türetiliyor. Video `color=c=black:size=320x240:rate=25` (tam siyah).

`.sup` ayrı girdi olunca ilk olayın damgası sıfıra çekiliyor; kaynak kurulurken `-itsoffset` ile
yerine konuyor.

Ölçü: çıktıdan bir kare gri tonlu ham bayta çevrilir. Üst ve alt yarının en parlak pikseli
(0 siyah, 255 beyaz) ve 128'den parlak piksellerin sınır kutusu okunur. Siyah karede parlak
piksel yalnız altyazıdan gelir.

## Grafik

`-vf` yerine `-filter_complex`, video `-map [v]` ile alınır:

`[0:0]<kırpma, döndürme…>[b];[0:3]scale=<kare genişliği>:-1[s];[b][s]overlay=x=(W-w)/2:y=H-h:eof_action=pass,<ölçek, fps…>[v]`

Önünde süzgeç yoksa `[b]` adımı yazılmaz. 8 bitin üstündeki kaynakta `:format=yuv420p10` eklenir.
İki geçişte grafik aynıdır.

## Yakma (Sınıf)

| Kaynak altyazı | Üst yarı | Alt yarı | Kutu (x, y) | Paket | Süre |
|---|---|---|---|---|---|
| PGS (`hdmv_pgs_subtitle`) | 0 | 255 | 128–191, 200–215 | 50 | 2,00 sn |
| VOBSUB (`dvd_subtitle`) | 0 | 255 | 128–191, 200–215 | 50 | 2,00 sn |
| DVB (`dvb_subtitle`) | 0 | 255 | 128–191, 200–215 | 50 | 2,00 sn |
| Aynı üç kaynak, yakmasız (olumsuz kontrol) | 0 | 0 | yok | | |

Kutu 1024 piksel (64x16), kaynaktaki yerinde. Yakılan çıktıda altyazı izi yok (ffprobe).

| Kol | Sonuç |
|---|---|
| Tuval 640x480, kare 320x240 | kutu 128–191, 200–215: tuval kareye ölçeklendi |
| Kırpma `crop=320:160:0:40` | kare 320x160, kutu 128–191, 120–135: alt kenara oturdu |
| İki geçiş, hedef 0,05 MB | üst 0, alt 255, 50 paket |
| Kesit 11,5–14 sn, altyazı 12–13,5 sn | 0,2 sn'de 0; 1,2 sn'de 255; 2,3 sn'de 0; 62 paket, 2,48 sn |

Kesitte ek sarma yok: altyazı kaynağın kendi akışı olduğu için `-ss` ikisini birlikte kaydırıyor.

## Neden Bu Seçenekler (Elle)

| Soru | Ölçülen |
|---|---|
| `eof_action=pass` olmadan | Dosya sonunda bozuk damgalı bir kare daha: mp4 101 paket (100 beklenir); süre dvd kaynakta 745,68 sn, dvb'de 30,04 sn, mkv çıktıda 4294970 sn. Seçenekle 100 paket, 4,000 sn. |
| Altyazı ölçeklenmeden | Tuval 640x480, kare 320x240: 0 parlak piksel. Kutu karenin dışında kalıyor. |
| `format` verilmeden, 10 bit kaynak | Çıktı 8 bite iniyor. `format=yuv420p10` ile `yuv420p10le`. |
| `format=auto`, 8 bit kaynak | Çıktı `yuv444p` oluyor; bu yüzden kullanılmadı. |
| Kırpma + ölçek aynı grafikte | 160x80 çıktı, kutu 64–95, 60–67. |
| Döndürme (`transpose`) aynı grafikte | 240x320 çıktı, kutu 96–143, 290–301: altyazı döndürülmüş karenin altında, dik duruyor. |
| Kare hızı kipi | Sabit kip 42 paket / 4,2 sn, tavanlı kip 41 paket / 4,04 sn; ikisi de yakıyor. |
| Kapak resmi | `-map [v] -map 1:v … attached_pic` birlikte çalışıyor. |
| `-hwaccel auto` | Yakma çalışıyor. |

## MP4'te Resim Altyazı

MP4'e `-c copy` ile taşıma (sınıf, PGS): ffmpeg -22 ile düşüyor. Elle: PGS ve DVB "Could not find
tag for codec", VOBSUB "Error muxing a packet". Aynı kopya MKV'ye geçiyor.

VidShrink bugün de sessiz düşürmüyordu: plan `ImageSubtitleDropped` notunu yazıyordu. Eksik olan
çıkış yoluydu. Not artık `--yak N` seçeneğini adıyla söylüyor (`plan.stream.image-subtitle-dropped`,
`…-by-container`). Kendiliğinden yakmaya düşülmüyor: birden çok iz varken hangisinin yakılacağı
kullanıcının kararı, yakma da geri alınamıyor.

## Sınırlar

- Girdi araması (`-ss`) bir olayın başlangıcından çok sonraya düşerse o olay hiç gelmiyor:
  altyazı 0,2–13 sn, arama 9,5 sn öncesinden başlayınca kutu görünmedi; 1,5 sn öncesinden
  başlayınca görünüyor. Kesit kolu bu yakın durumu ölçüyor.
- Geç başlayan PGS'te ffmpeg "Could not find codec parameters … unspecified size" uyarısı
  veriyor; yakma yine de çalışıyor.
- 12 bit ve 4:4:4 kaynak ölçülmedi; 8 bitin üstü 10 bit 4:2:0'a iner.
- Gerçek bir Blu-ray ya da DVD altyazısı ölçülmedi: çok renkli palet, yarı saydamlık, birden çok
  pencere, olay içinde konum değişimi. Kaynak tek renkli tek kutu.
- `overlay` süzgecinin ffmpeg derlemesinde bulunduğu yoklanmıyor (libass gibi isteğe bağlı değil).
- Arayüzde yakma listesi hâlâ yalnız metin altyazı gösteriyor.

## Mutasyonlar

Üç yakma sınıfı birlikte 65 kol (`ResimAltyaziYakmaTests`, `AltyaziYakmaTests`,
`DisAltyaziYakmaTests`). Her mutasyon elle geri alındı.

| Mutasyon | Kırmızı |
|---|---|
| `eof_action=pass` kaldırıldı | 8 |
| Altyazı ölçeği kırpılmış kare yerine kaynak genişliğinden | 1 |
| `-map [v]` koşulu ters | 14 |
| libass yoklaması resim izde de soruyor | 1 |
| Doğrulama resim izi yine reddediyor | 7 |
| 10 bit kaynakta `format` yazılmıyor | 1 |
