# Akış Kalitesi Ve Kesintisiz Çalma Ölçümü

18 Ekim 2026 · later 83 (OYB-21, OYB-43) · dal `yan/akis-kesintisiz` · libmpv `tools/libmpv/libmpv-2.dll`

Ağ yok: yayınlar ffmpeg ile üretildi, 127.0.0.1'deki sınama sunucusundan (`KlasorSunucu`) açıldı. Ses
hoparlöre gitmedi (`ao=null`, örnek sayımında `ao=pcm` dosyaya). Ölçümler bir kez koşan geçici
sonda testleriyle alındı; kalıcı ölçü `tests/VidShrink.Tests/OynaticiAkisKalitesiTests.cs`.

## Ön Koşul: OYB-P1

Bitmiş. `PlayerView.Kuyruk.cs` `ResolveQueue` uzak adresi liste saymıyor (`MediaAddress.IsAddress`),
`PlaylistFile.cs:48` listedeki uzak girdiyi tutuyor; `OynaticiAdresTests` ikisini de ölçüyor.

## 1. Uyarlamalı Yayının Katmanları İz Listesinde

Yayın: 640x360 (BANDWIDTH 1289680) ve 320x180 (BANDWIDTH 346672) iki katman, her biri bir video ve
bir ses izi, 6 sn, 2 sn'lik parçalar.

| `hls-bitrate` | `track-list` | seçilen katman |
|---|---|---|
| varsayılan (`max`) | 2 iz: video id=1 640x360, ses id=1, ikisinde `hls-bitrate=1289680`, `program-id=0` | yüksek |
| `min` | 2 iz: video id=2 320x180, ses id=2, `hls-bitrate=346672`, `program-id=1` | düşük |
| `max` | yüksekle aynı | yüksek |
| `500000` | düşükle aynı | düşük |

- İz listesinde **yalnız seçili katmanın izleri** var; öbür katman hiç görünmüyor. Varsayılan açılışta
  `vid=2` yazmak katmanı değiştirmiyor, video izini kapatıyor (`current-tracks/video/id` boş).
- Bu yüzden kalite seçimi video izi değiştirerek yapılamaz. Katmanlar ana listeden okunur
  (`Core/HlsVariants`), seçim `hls-bitrate`'e yazılır ve yayın aynı konumdan yeniden açılır
  (`MpvEngine.SetStreamBitrateAsync`).
- İz kimlikleri katmana bağlı (ses id 1 ↔ 2). Takılma kurtarmasının `ReopenAsync`'i eski ses kimliğini
  geri yazıyor; katman değişiminde bu, sesi kapatırdı. Katman değişimi o adımı atlıyor; ölçü katman
  değişince `AudioTrack > 0` olduğunu pinliyor.

## 2. Kesintisiz Çalma: `PlayerView.Kuyruk.cs:13` Kararı

Karar: "Liste mpv'ye verilmez: mpv'nin kendi listesi arayüzün yolunu ve kartını güncellemiyor."

### Gerekçe yeniden ölçüldü

`a.wav` arayüzden açıldı, `b.wav` motorun listesine `loadfile append` ile eklendi, oynatıldı. Geçişten sonra:
motor `path=b.wav`, `time-pos=0,54`; arayüz `LoadedPath=a.wav`, başlık `a.wav`. Gerekçe bugün de doğru:
dosya başına kurulan durum (yol, başlık, kart, geçmiş, iz hafızası, yan altyazı, A-B döngüsü) yalnız
`OpenCoreAsync`'te kuruluyor ve motorun kendi geçişi onu tetiklemiyor.

### Geçiş boşluğu

Dosyalar: 2,000 sn'lik iki sinüs (440 Hz, 660 Hz), 48 kHz, wav ve mp3. Üç tur.

Örnek düzeyi (`ao=pcm`, mono s16, dosyaya): dört yolun hepsi 192000 örnek yazdı (beklenen 192000),
en uzun sessiz kesit 2 örnek (0,04 ms, sinüsün sıfır geçişi). Yani hiçbir yolda örnek kaybı ya da
araya giren sessizlik yok:

| yol | wav | mp3 |
|---|---|---|
| mpv listesi, `gapless-audio=yes` | 192000, boşluk 0 | 192000, boşluk 0 |
| mpv listesi, `gapless-audio=no` | 192000, boşluk 0 | 192000, boşluk 0 |
| mpv listesi, `gapless-audio=weak` | 192000, boşluk 0 | 192000, boşluk 0 |
| aynı motorda `loadfile` (sırayla) | 192000, boşluk 0 | 192000, boşluk 0 |

Zaman düzeyi (`ao=null`, duvar saati): EOF'tan B'nin 0,3 sn'ye varmasına kadar geçen süreden 0,3 çıkarıldı.

| yol | wav (3 tur, ms) | mp3 (3 tur, ms) |
|---|---|---|
| yeni motor (bugünkü arayüz yolu: `Close` + yeni `MpvEngine` + `OpenAsync`) | 71, 31, 88 | 26, 26, 26 |
| aynı motorda `loadfile` | 26, 26, 24 | 27, 27, 27 |

`ao=null` gerçek bir aygıtın boşaltma süresini taklit etmiyor: 2,000 sn'lik dosyanın EOF'u oynatmanın
1,62 sn'sinde geldi (null çıkış tamponu açılışta ~0,37 sn'yi bir anda yutuyor). Bu yüzden mpv listesinin
duvar saati sayısı (62-85 ms "fazla") ve sıralı yolun negatif toplamı (-277 ile -357 ms) aygıt boşluğu değil,
null çıkışın tampon yapıtı; karşılaştırmaya alınmadı. Gerçek aygıtta duyulan boşluk bu ortamda ölçülemez
(testler ses çalmaz).

### Karar

Değişmedi. Ölçülebilen tek fark, sıradaki dosyayı açmanın motor tarafında 24-88 ms sürmesi; örnek
kaybı yok. Kuyruğu motora vermek, geçişte dosya başına durumu `playlist-pos` gözleyerek yeniden kurmayı
gerektirir (arayüzün açılış yolunu ikiye bölmek) ve kazancı bu ortamda ölçülemiyor. Ayrıca otomatik
geçiş yalnız "tümünü tekrarla" ya da karışık kipte oluyor (`PlayerView.Window.cs` `AfterEnd`).
Yeniden açılması için önce gerçek ses aygıtında, iki kayıtla (çıkıştan geri döngü) boşluğun duyulur
olduğu gösterilmeli.
