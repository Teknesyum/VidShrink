<!-- lang -->

[<img src="docs/gorseller/badge-lang.tr.svg" alt="Türkçe seçili, switch to English" width="124" height="44">](README.md)

# VidShrink

**libmpv üzerine kurulu ücretsiz, açık kaynak video oynatıcı; aynı pencerede hedef boyuta
sıkıştırıcı, biçim dönüştürücü, ekran kaydedici ve zaman çizelgeli düzenleyici var —
çevrimdışı; Windows, macOS ve Linux'ta.**

**42 dil · 36 tema · Ölçülen 36 durumun 0'ında hedef boyut aşıldı · Reklam yok · Filigran
yok · Hesap yok · Abonelik yok · Telemetri yok**

[![Son sürüm](https://img.shields.io/github/v/release/Teknesyum/VidShrink?label=s%C3%BCr%C3%BCm)](https://github.com/Teknesyum/VidShrink/releases/latest)
[![Lisans AGPL-3.0-or-later](https://img.shields.io/badge/lisans-AGPL--3.0--or--later-blue)](LICENSE)
[![Windows, macOS, Linux](https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux-lightgrey)](#kurulum)

<a href="docs/gorseller/T201-oynatici-tr.png"><img src="docs/gorseller/T201-oynatici-tr.png" alt="VidShrink Oynatıcı sekmesi Türkçe: pencereyi dolduran bir video, üstte Oynatıcı, Düzenleyici, Küçült, Dönüştür, Kaydedici ve Ayarlar sekmeleri, altta süre, ses, on saniyelik atlamalar, oynat-duraklat, hız, klip ve tam ekran düğmelerini taşıyan kontrol şeridi" width="800"></a>

Bir videoyu hedef dosya boyutuna sıkıştıran araç olarak başladı. Bugün önce bir medya
oynatıcı: altyazı, çalma listesi ve ağ akışlarıyla mpv motoru, fareyle kullanılan bir
pencerenin arkasında.

Öteki sekmeler videoyla iş yapılacağında devreye giriyor. Yükleme sınırının altına
küçültün, dönüştürün, ekranı kaydedin ya da zaman çizelgesinde kesin. Siz istemedikçe
makinenizden hiçbir şey çıkmıyor — bkz. [Gizlilik](#kod-imzalama-politikasi).

## Kurulum

Yönetici hakkı yok, .NET SDK yok. Windows 10 ya da 11, macOS 14 ve üstü, ya da X11/Wayland
koşan bir Linux masaüstü.

### Windows'a Kurulum

Üç yol, bu sırayla.

1. **Teknesyum Base — önerilen yol.**
   [`Teknesyum-Base.exe`](https://github.com/Teknesyum/Teknesyum-Base/releases/latest/download/Teknesyum-Base.exe)
   dosyasını ([`.sha256`](https://github.com/Teknesyum/Teknesyum-Base/releases/latest/download/Teknesyum-Base.exe.sha256))
   indirip çalıştırın, listeden **VidShrink**'i bulup kurun. Base sonradan güncellemeyi ve
   kaldırmayı da yapar. Base henüz imzalı değil; Windows SmartScreen ilk açılışta uyarabilir:
   *Ek bilgi*'yi, sonra *Yine de çalıştır*'ı seçin. Ayrıntı: [Teknesyum Base](https://github.com/Teknesyum/Teknesyum-Base).
2. **VidShrink-Setup.exe.**
   [`VidShrink-Setup.exe`](https://github.com/Teknesyum/VidShrink/releases/latest/download/VidShrink-Setup.exe)
   dosyasını indirip çalıştırın. Kendi başına çalışan küçük bir program: PowerShell de WinGet
   de gerekmez.
3. **Tek satır kod.** PowerShell'e ya da Komut İstemi'ne yapıştırın. Son
   `VidShrink-Setup.exe`'yi geçici klasöre indirip çalıştırır — 2. yoldaki kurucunun aynısı.

   ```powershell
   powershell -NoProfile -Command "[Net.ServicePointManager]::SecurityProtocol=3072; Set-Variable ProgressPreference SilentlyContinue; Set-Location ([IO.Path]::GetTempPath()); irm https://github.com/Teknesyum/VidShrink/releases/latest/download/VidShrink-Setup.exe -OutFile VidShrink-Setup.exe; .\VidShrink-Setup.exe"
   ```

Sürümler imzalanana kadar Akıllı Uygulama Denetimi açık olan bir bilgisayar VidShrink'i
hangi yolla kurulursa kurulsun engeller.

**Kaldırma.** `VidShrink-Setup.exe --uninstall` kısayolları, sağ tık girdilerini, Birlikte aç
kaydını ve kurulum klasörünü siliyor.

### macOS Ve Linux'a Kurulum

```bash
curl -fsSL https://raw.githubusercontent.com/Teknesyum/VidShrink/main/install-vidshrink.sh | sh
```

Kaldırmak için aynı kurucuyu `--uninstall` ile çalıştırın:

```bash
curl -fsSL https://raw.githubusercontent.com/Teknesyum/VidShrink/main/install-vidshrink.sh | sh -s -- --uninstall
```

<details>
<summary>Gerekenler, kurucunun indirdikleri ve PowerShell betiği</summary>

Her sürüm tek bir sürüm numarasından dört hedef üretiyor: `win-x64`, `osx-arm64`, `osx-x64`,
`linux-x64`. Yukarıdaki sistemin yanında `ffmpeg` ve `ffprobe` gerekiyor. FFmpeg ve libmpv
sürümle birlikte gelmiyor; kurucu bunları Windows'ta pinlenmiş SHA-256 özetlerine karşı
indiriyor, diğerlerinde paket yöneticinizin komutunu yazıyor. Windows sürümleri, sertifika
verildiğinde [Kod İmzalama Politikası](#kod-imzalama-politikasi) altında imzalanıyor.

Windows'ta `Install-VidShrink.ps1` betiği aynı kurulumu yapar; WinGet yoksa
FFmpeg'i pinlenmiş arşivden indirir:

```powershell
powershell -NoProfile -Command "[Net.ServicePointManager]::SecurityProtocol=3072; iex (irm https://raw.githubusercontent.com/Teknesyum/VidShrink/main/Install-VidShrink.ps1)"
```

Sağlama doğrulaması, sağ tık girdisi, kendi kendini güncelleme akışı ve bütün anahtarlar
[`docs/kurulum.tr.md`](docs/kurulum.tr.md) içinde.

</details>

## Altyazılı, Çalma Listeli Ve Ağ Akışlı Video Oynatıcı

Oynatıcı sekmesi, mpv'nin içindeki motor olan **libmpv** üzerine kurulu. MKV, MP4, WebM,
AVI, MOV, TS ve gerisini açıyor; müzik dosyalarını kapak kartıyla çalıyor.

Dosyayı sürükleyerek, Windows'ta Explorer sağ tık menüsünden ya da komut satırından açın.
İzlediğiniz son beş videoda oynatma kaldığı yerden sürüyor.

- **Altyazı** — yan dosyalar kendiliğinden yükleniyor, sürüklenen altyazı dosyası doğrudan
  çalışıyor. Zamanlamayı adım adım kaydırın, ilkinin yanında ikinci bir altyazı gösterin;
  yazı tipini, rengi, anahattı, gölgeyi ve zemini ayarlayın. **OpenSubtitles araması ve
  indirmesi** pencerenin içinde, kendi hesabınızla.
- **Ses** — ses izi ve ses gecikmesi, hazır ayarlı 10 bantlı ekolayzer, ses düzeyi
  dengeleme ve izin verdiğinizde %200'e kadar ses.
- **Görüntü** — parlaklık, karşıtlık, doygunluk, gama, renk tonu, keskinlik, taramayı
  giderme, kırpma, döndürme, aynalama, en-boy oranı ve yakınlaştırma. Donanım çözme bir
  anahtar.
- **Gezinme** — 1, 10, 60 ve 300 saniyelik atlamalar, kare kare ilerleme, bölümler, yer
  imleri, A-B tekrarı ve süre çubuğunda işaretçiyi izleyen küçük resim.
- **Hız** — 0,05'lik adımlarla 0,25× ile 4× arası, ve belirlediğiniz iki hız arasında tek
  tuşla geçiş.
- **Çalma listeleri** — M3U, PLS, WPL ve ASX açıyor, kuyruğu M3U8 olarak kaydediyor,
  karıştırıyor ve tekrarlıyor. Klasördeki sonraki ve önceki dosya, son dosyalar ve
  klavyenin medya tuşları.
- **Ağ akışları** — adresi yapıştırın, sürükleyin ya da yazın: http, https, rtsp, rtmp, srt
  ya da udp. Doğrudan medya adresini oynatıyor; YouTube adresi gibi bir sayfa bağlantısını
  çözmüyor.
- **Yakalama** — PNG ya da JPG ekran görüntüsü, o anki kareyi panoya kopyalama ve oynayan
  videodan doğrudan kesilen klip ya da GIF.
- **Bilgi** — kodeği, bit derinliğini ve SDR, HDR10, HLG ya da Dolby Vision'ı gösteren bir
  panel; yanında düşen kare gibi canlı sayılar.
- **Pencere** — tam ekran, mini kip, her zaman üstte ve kendiliğinden gizlenen bir kontrol
  şeridi. Klavye kısayolları yeniden atanabiliyor.

Tek tuş, oynayan dosyayı aynı konumda Düzenleyici'ye gönderiyor.

## Videoyu Hedef Dosya Boyutuna Sıkıştırma

<table>
<tr>
<td><a href="docs/gorseller/T201-kucult-onizleme-tr.png"><img src="docs/gorseller/T201-kucult-onizleme-tr.png" alt="Küçült sekmesi sunum-prototip.mp4 yüklü ve hedef 0,15 MB iken: solda kaynak bilgileri, hedef ve kalite kaydırıcıları, ortada mavi bölme çizgisinin Orijinal tarafında kaynak, İşlenmiş tarafında CRF 43'lük planlanan çıktıyla karşılaştırma paneli, altında libsvtav1, 72 kbit/sn iki geçiş, 666x374 ve 30 FPS yazan Yapılacak İşlem paneli, sağda kaliteyi 65,1/100 öngören Çıktı paneli" width="400"></a></td>
<td><a href="docs/gorseller/T201-kucult-yakin-tr.png"><img src="docs/gorseller/T201-kucult-yakin-tr.png" alt="Aynı Küçült sekmesi, karşılaştırma paneli uygulamanın içinde %196'ya yakınlaştırılmış: bölme çizgisinin solunda kaynak kitap sırtlarının kenarlarını koruyor, sağında 0,15 MB'lık çıktı onları yumuşatıyor" width="400"></a></td>
</tr>
</table>

Videoyu sürükleyin, bir boyuta dokunun, başlat deyin. Otomatik kip kodeki, kalite düzeyini,
çözünürlüğü ve kare hızını **sizin** dosyanız için seçiyor ve beklenen boyutu daha hiçbir
şey koşmadan söylüyor. Ölçülen 36 durumda hedef **sıfır** kez aşıldı.

- **Boyutlar** — 8 MB (katı e-posta geçitleri), 16 (WhatsApp), 25 (Gmail) ve 180 (WhatsApp
  Web) için yongalar, gerisi için kaydırıcı. Windows'ta Explorer sağ tık menüsünde
  "VidShrink ile Küçült" altı boyutla duruyor: 8, 16, 20, 25, 50 ve 100 MB.
- **Öncesi ve sonrası** — bölmeli panel, kodlamaya başlamadan önce kaynağı planlanan
  çıktının yanında, yakınlaştırmayla gösteriyor.
- **Kodlayıcılar** — yazılım ile NVENC, Quick Sync ve AMF; her biri önce kendi makinenizde
  yoklanıyor. macOS'ta VideoToolbox ve Linux'ta VAAPI kodek kilidiyle seçilebiliyor; otomatik
  sırada değiller.
- **Kalite puanı** — sonuç **VMAF-NEG** ile puanlanıyor: ortalama, harmonik ortalama, 10.
  yüzdelik ve en kötü kare.
- **Toplu iş** — bir klasörün tamamı kuyruktan geçiyor; kuyruk bitince klasörü açıyor,
  bilgisayarı uyutuyor ya da kapatıyor.

## Video Ve Ses Biçimi Dönüştürme

<table>
<tr>
<td><a href="docs/gorseller/T201-donustur-tr.png"><img src="docs/gorseller/T201-donustur-tr.png" alt="Dönüştür sekmesi sunum-prototip.mp4 yüklüyken: kapsayıcı, kodek, kalite kipi, çözünürlük, kare hızı ve kırpma alanları, yanlarında FFmpeg Komutu ve İlerleme panelleri" width="400"></a></td>
<td><a href="docs/gorseller/T201-gelismis-tr.png"><img src="docs/gorseller/T201-gelismis-tr.png" alt="Gizli Gelişmiş sekmesi: koşacak komutun kendisini gösteren FFmpeg Komut Satırı kutusu, AI Ayarları kutusu ve Başarım Ölçümü kutusu" width="400"></a></td>
</tr>
</table>

MP4, MKV, WebM, MOV, MXF, AVI, GIF, hareketli WebP ve hareketli AVIF; yalnız ses için MP3,
M4A, WAV ve FLAC. H.264, H.265, VP9, AV1 ya da doğrudan akış kopyası, kırpma ve ses çıkarma.
Kurgu için ara kodekler de var: ProRes (Proxy, LT, 422, HQ, 4444) MOV'a, DNxHR (LB, SQ, HQ,
HQX, 444) MOV ya da MXF'e ([`k12-ara-kodekler.md`](docs/olcumler/k12-ara-kodekler.md)). **On
sekiz hazır hedef** — WhatsApp, Discord, Telegram, Gmail, Outlook, Chromecast, Nest Hub,
Apple TV ve fazlası — her alanı sizin yerinize dolduruyor; koşacak FFmpeg komutunun kendisi
de ekranda.

## Ekran Kaydı

<a href="docs/gorseller/T201-kaydedici-tr.png"><img src="docs/gorseller/T201-kaydedici-tr.png" alt="Kaydedici sekmesi Gelişmiş düzende, ayarları program seçerken: kaynak, ekran ve geri sayım seçicileri, Yeniden Ölç düğmesiyle tahmini süre ve boyut alanları, mikrofon ve sistem sesi seçicileri, Gelişmiş Kodlama, Kamera ve Kayıt Tamponu panelleri" width="800"></a>

- **Ne** — tüm ekran, tek pencere ya da çizdiğiniz bir bölge. Windows ddagrab ile 60 kareye
  kadar yakalıyor, olmazsa bunu söyleyip gdigrab'a düşüyor; macOS avfoundation, Linux
  x11grab kullanıyor. 60 kare istendiğinde ddagrab 59,8 kare verdi; gdigrab 24,2 ve 44,8
  ([`kaydedici-ddagrab.md`](docs/olcumler/kaydedici-ddagrab.md)).
- **Ses** — mikrofon ve sistem sesi adıyla seçiliyor, böylece sırası değişen bir aygıt
  listesi mikrofonunuzu sessizce değiştiremiyor; kazanç, gürültü kapısı ve gürültü bastırma.
- **Kamera** — kendi boyutu ve köşesi olan bir kamera katmanı, yeşil perde seçeneğiyle.
- **Anlatım videoları için** — imleç, tıklama halkaları ve tıklama sesi, bastığınız tuşlar
  ekranda, büyüteç ve canlı önizleme.
- **Denetim** — Windows'ta F7-F11 genel kısayolları, F6 ile bölüm işareti; geri sayım, süre
  sınırı, süreye ya da boyuta göre bölme, son anları tutan tekrar arabelleği, tepsi ve mini
  kaydedici kipleri.
- **Kodlayıcılar** — x264, x265, SVT-AV1 ve VP9, artı NVIDIA NVENC, Intel Quick Sync ve AMD
  AMF. **Otomatik kip** makinenizde her aday için üç gerçek saniye kaydediyor, ffmpeg'in
  düşen kare sayacını okuyor ve kare düşürmeyeni tutuyor.
- **Çıktı** — MP4, MKV, MOV ya da GIF; isterseniz hedef boyut ya da süre bütçesi; durdurunca
  düzgün kapanan bir dosya. Tek tıkla Düzenleyici'ye, Oynatıcı'ya ya da Paylaş'a gidiyor.

## Video Kesme Ve Düzenleme

<a href="docs/gorseller/T201-duzenleyici-tr.png"><img src="docs/gorseller/T201-duzenleyici-tr.png" alt="Düzenleyici sekmesi: üstte video, altında Böl, Sil, Hız, Geri Al, Yinele, yakınlaştırma, Kaydet, Farklı Kaydet ve Dosyayı Paylaş düğmeleri, onların altında üç klibe bölünmüş zaman çizelgesi" width="800"></a>

Herhangi bir sekmeden bir videoyu açın ve zaman çizelgesinde kesin: bölün, bir klibi ya da
bir aralığı silin, klipleri taşıyın, her klibin hızını 0,01× ile 100× arasında ayarlayın,
tersine oynatın, geri alın ve yineleyin. Sonra üç dışa aktarma kipinden biriyle **Kaydet**,
**Farklı Kaydet** ya da **Paylaş**:

- **Hızlı** — anahtar karelerden keser, akışları kopyalar; yeniden kodlama yok, kalite kaybı
  yok.
- **Akıllı** — kopyalayabildiğini kopyalar, yalnız kesimlerin gerektirdiğini yeniden kodlar.
- **Tam** — sonucun tamamını yeniden kodlar.

## Komut satırı

Aynı paket, pencerenin yanında başsız bir komut satırı aracı da taşıyor: Windows ve
Linux'ta `vidshrink`, macOS'ta `vidshrink-cli`. Pencerenin çağırdığı karar motorunu
çağırıyor, yani aynı girdi aynı ffmpeg argümanlarını veriyor; bir test ikisini
karşılaştırıyor.

```bash
vidshrink kucult clip.mp4 --hedef 25MB              # boyuta küçült
vidshrink kucult clip.mp4 --kalite 80 --kodek av1   # kalite puanına küçült
vidshrink plan clip.mp4 --hedef 8MB --json          # yalnız plan ve argümanlar, kodlama yok
```

Her uzun anahtarın bir de İngilizce takma adı var (`--hedef` ile `--target`, `--kodek` ile
`--codec` aynı anahtar). Bütün anahtarlar, takma ad tablosunun tamamı ve çıkış kodları:
[`docs/cli.tr.md`](docs/cli.tr.md).

### İzlenen klasör

```bash
vidshrink izle ~/Gelen --cikti ~/Giden --hedef 25MB             # Ctrl+C'ye kadar koşar
vidshrink izle ~/Gelen --cikti ~/Giden --hedef 25MB --bir-kez   # klasörü boşaltır, çıkar
```

`izle`, klasöre düşen her videoyu küçültüyor. `--cikti` çıktı klasörüdür ve zorunludur;
izlenen klasörün kendisi olamaz. Dosyanın ne zaman alındığı, ilerlemenin nerede tutulduğu,
harf büyüklüğü ve çıkış kodları: [`docs/cli.tr.md`](docs/cli.tr.md).

## Büyük Dosyayı Bağlantı Olarak Paylaşma

Küçült'te, Kaydedici'de ya da Düzenleyici'de **Paylaş**'a basın, dosya bağlantı olarak
yüklensin: 25 GB'a kadar dosyalar için bir ile yedi gün tutan **storage.to**, ya da 128 MB'a
kadar dosyalar için üç saat tutan **uguu.se**. Bağlantı telefonunuz için bir QR koduyla
geliyor, yarıda kopan yükleme yeniden denenebiliyor. Paylaşım hedefleri ve ölçülmüş boyut
tavanları [`paylasim-hedefleri.json`](paylasim-hedefleri.json) içinde.

## mpv, HandBrake, OBS Ve LosslessCut İle Karşılaştırma

Bunların her biri kendi tek işini bu uygulamadan daha derin yapıyor. Satırlar, adı geçen
sürümlerin yazılı bir taramasından ([`tarama-2026-10-05.md`](docs/piyasa/tarama-2026-10-05.md)).

| İş | Burada | Başka yerde |
|---|---|---|
| Oynatma | Yukarıdaki denetimlerle libmpv | mpv 0.41.0'da HDR ton eşleme, gölgelendiriciler, ara kare üretimi, betikler ve yt-dlp de var |
| MB olarak hedef boyut | Var; ölçülen 36 durumda hiç aşılmadı | HandBrake 1.11.2'de hedef boyut alanı yok. Shutter Encoder 20.4 ve FFmpeg Batch'te var; Shutter boyutu garanti etmediğini söylüyor |
| Kodlamadan önce önizleme | Bölmeli öncesi/sonrası paneli | HandBrake önizleme için bir örnek kodluyor |
| Eş boyutta kalite | 8 SDR satırının 8'inde ve 4 HDR10 satırının 3'ünde HandBrake x265 `slow`'un önünde | HandBrake karanlık sahnede daha az bantlaşıyor |
| Kodlama hızı | 8 satırın 8'inde daha yavaş; toplam süre HandBrake'in 1,64 ile 3,73 katı | HandBrake |
| Donanım kodlayıcıları | NVENC, Quick Sync, AMF; VideoToolbox (macOS) ve VAAPI (Linux) yalnız kodek kilidiyle, VAAPI burada gerçek donanımda henüz kodlamadı | HandBrake VideoToolbox ve VAAPI'yi olağan kodlayıcı listesinde sunuyor |
| Aynı anda birkaç kodlama | Yok, tek iş | HandBrake, FFmpeg Batch |
| Ekran kaydı | Ekran, pencere, bölge, kamera | OBS 32.2.2'de oyun yakalama, gerçek masaüstü sesi yakalama, sahneler ve yayın da var |
| Yeniden kodlamadan kesme | Hızlı ve Akıllı dışa aktarma | LosslessCut 3.69.0'da birleştirme, iz yönetimi ve EDL/CSV de var; kendi akıllı kesimine deneysel diyor |

<sub>Ürün adları sahiplerine aittir; VidShrink'in hiçbiriyle bağı yoktur.</sub>

## Yapmadıkları

- Oynatıcıda HDR ton eşleme denetimi, gölgelendirici, ara kare üretimi ya da betik yok.
- URL'den indirme yok. Oynatıcı doğrudan medya adresini açıyor, sayfa bağlantılarını
  çözmüyor; yt-dlp yok.
- Oynatıcıdan Chromecast'e gönderme yok, otomatik altyazı üretimi yok.
- Sabitleme, LUT ya da filigran yok; FFV1 çıktısı yok. ProRes ve DNxHR yalnız Dönüştür
  sekmesinde; hedef boyuta sürülemiyorlar.
- Dosya birleştirme ya da resim dizisi yok.
- Canlı yayın, sahne ya da oyun yakalama yok. Pencere yakalama pencerenin dikdörtgenini
  ekrandan kırpıyor; sistem sesi gerçek bir geri döngüyle değil aygıt adıyla bulunuyor.
- Ekran görüntüsü aracı, kaydırmalı yakalama, OCR ya da açıklama ekleme yok.
- macOS ve Linux'ta kaydedici yakalıyor, ama kamera katmanı, genel kısayollar ve tıklama ile
  tuş gösterimi yok.

## Ölçülmüş Sonuçlar

Ölçülen 36 vakada hedef boyut bir kez bile aşılmadı — **0/36 aşım** — ve geride kaldığımız iki
yer de yanında yayımlı.

<details>
<summary>Tablo, kapılar, geride kaldığımız yerler ve düzeneğin dürüst tutulması</summary>

Arama tablosu yok. Karar verilmeden önce ffmpeg gerçek dosyanıza karşı koşuyor: iki
çözünürlük ve iki CRF'te kısa örnek kodlamalar, bir sahne haritası, sonra aynı pencereleri
dört CRF adımı arayla yeniden kodlayıp iki sonuçtan bit maliyeti eğrisini okuyan bir
kalibrasyon geçişi. Yazılım kodlayıcıları sonra iki geçiş, donanım tek VBR geçişi koşuyor;
aşım olursa durup ikinci denemeden önce soruyor.

36 vaka: üç 1080p30 klip (yüksek detay, yüksek hareket, ağır gürültü) × üç hedef × yazılım
ve donanım kolu × **ikişer tekrar**; AMD Ryzen 7 9700X, RTX 5070 Ti ve ffmpeg 9.0-full
([`docs/olcumler/bench-2026-09-13.md`](docs/olcumler/bench-2026-09-13.md)):

| Hedef | Klip | Kodlayıcı | Çıkan (1. / 2. koşum) | Bütçe doluluğu | Deneme |
|---|---|---|---|---|---|
| 100 MB | yüksek detay | libx264 | 97,30 / 97,37 MB | %97,3 | 1 |
| 100 MB | ağır gürültü | h264_nvenc | 98,63 / 98,63 MB | %98,6 | 3 |
| 50 MB | yüksek hareket | h264_nvenc | 49,88 / 49,88 MB | %99,8 | 1 |
| 25 MB | yüksek hareket | libx264 | 24,24 / 24,26 MB | %97,0 | 1 |
| 25 MB | ağır gürültü | libsvtav1 | 24,28 / 24,28 MB | %97,1 | 1 |
| 8 MB | ağır gürültü | libsvtav1 | 7,67 / 7,67 MB | %95,9 | 1 |
| 8 MB | yüksek hareket | h264_nvenc | 7,71 / 7,71 MB | %96,3 | 1 |

Kalibrasyon 36/36 tuttu, 36/36 iki geçişli seçildi. Donanım kolu tekrarlar arasında bit
düzeyinde aynı — 18/18 vakada 0,000 MB sapma — `libx264` en fazla 0,074 MB kaydı.

**AV1 dalı hedefin altına düşüyor.** 36 vakanın 31'i bant içinde kaldı; **beş kaçağın
beşi de, tek sert taban ihlali de (6,80 MB tabana karşı 6,68 MB) `libsvtav1`** — `libx264`
6/6, `h264_nvenc` 18/18 bant içi. Tekrarlar arası sapma da orada: 2,484 MB'a ve 73,5
saniyeye kadar.

**HandBrake: eş boyutta öndeyiz, karanlık bantlaşmada gerideyiz.** HandBrakeCLI 1.11.2'nin
x265 `slow` ön ayarına karşı, teslim edilen bayt eşitken 8 SDR satırının 8'inde
([`handbrake-kiyas-b1-sdr.md`](docs/olcumler/handbrake-kiyas-b1-sdr.md)) ve 4 HDR10 satırının
3'ünde ([`handbrake-kiyas-b4-hdr.md`](docs/olcumler/handbrake-kiyas-b4-hdr.md)) öndeyiz.
Karanlık sahneler hâlâ daha çok bantlaşıyor: AV1 çıktımız CAMBI 9,31 ve 9,39, HandBrake 6,48
ve 6,49 alıyor; düşük olan iyi
([`handbrake-kiyas-b7-aciklar.md`](docs/olcumler/handbrake-kiyas-b7-aciklar.md)). Ayrıca daha
yavaşız: 8 satırın 8'inde toplam süre HandBrake'in 1,64 ile 3,73 katı
([`handbrake-kiyas-cli.md`](docs/olcumler/handbrake-kiyas-cli.md)).

**Düzenek** ([`docs/olcumler/ab-duzenegi.md`](docs/olcumler/ab-duzenegi.md)), bir çıktının
renk uzayı, transferi, primaries'i ya da piksel biçimi referansla eşleşmediğinde hiç sayı
basmıyor. **VMAF-NEG**, **XPSNR** ve **SSIM**'i kare kilitli akışlar üzerinden, 17 dakikalık
bir kaynağın gerçek anahtar karelerde kesilmiş üç adet 60 saniyelik parçasında puanlıyor;
her parça istenen saniyeden 0,4 sn önceye oturuyor. 60 MB'lık bir kodlamayı 600 MB'lıktan en
az 1,00 VMAF-NEG puanıyla ayırmak zorunda; ölçülen, HandBrake için **+39,26**, VidShrink
için **+39,85**. Bilinen bir kusur olduğu gibi yayımlı: plan kare hızını düşürdüğünde düzenek
farklı anları karşılaştırıyor — yalnız `fps=30` yeniden örneklemesi XPSNR'ı **1,70 dB'den
21,14 dB'ye** taşıdı — ve o yola dayanan hükümler kendi belgelerinde *temelsiz* diye
işaretli.

Kaydedicinin otomatik kipi de aynı yolla ölçülüyor:
[aday başına üç saniyelik gerçek kayıt](src/VidShrink.Ffmpeg/RecorderAutoProbe.cs), yeniden
ölçümü [`auto-mod-yeni-taban.md`](docs/olcumler/auto-mod-yeni-taban.md) içinde.

</details>

Uzun hâli — kalibrasyon, durma ölçütü, HDR, algısal puanlama, bugünkü sınırlar:
[`docs/motor.tr.md`](docs/motor.tr.md). Buradaki her sayı [`docs/olcumler/`](docs/olcumler/)
altındaki bir belgeden geliyor.

## 42 Dil Ve 36 Tema

<a href="docs/gorseller/T201-ayarlar-tr.png"><img src="docs/gorseller/T201-ayarlar-tr.png" alt="Ayarlar sekmesi: dil ve tema seçicileri, sağ tık menüsü girdileri, kendiliğinden güncelleme anahtarı, çıktı, altyazı ve paylaşım ayarları" width="800"></a>

Pencerenin tamamı 42 dil konuşuyor, Arapçadan Vietnamcaya, ve yanında 36 renk teması
geliyor — Catppuccin, Dracula, Gruvbox, Nord, Rose Pine, Solarized, Tokyo Night ve yirmi
dokuz tane daha, açık ve koyu. İkisini de Ayarlar'dan seçiyorsunuz, hiçbir şey yeniden
başlamıyor. Diliniz yok mu, ya da çeviri kötü mü duruyor?
[Issue açın](https://github.com/Teknesyum/VidShrink/issues/new).

**Sisteminizde neyi değiştirdiği, ve fazlası değil.** Windows'ta kurucu Başlat menüsüne ve
masaüstüne kısayol, Explorer sağ tık menüsüne "Bu videoyu VidShrink ile aç" girdisi (Windows
11'de birincil menüde) ve video dosyalarının **Birlikte aç** listesine VidShrink ekliyor —
[kullanıcı başına yazılıyor](docs/olcumler/kabuk-menusu.md), yönetici hakkı istemiyor ve
varsayılan oynatıcınız varsayılan kalıyor. Uygulama yeni sürüm için GitHub'a bakıp
güncellemeyi iki düğmeyle öneriyor: Güncelle ve Sonra. Bu denetim Windows'ta varsayılan
açık, Ayarlar'dan kapatılıyor. Hepsi tek komutla geri alınıyor — bkz. [Kurulum](#kurulum).

## Kaynaktan Derleme

Depoyu derlemek için .NET 8 SDK gerekiyor; derlediğinizi çalıştırmak için makinede `ffmpeg`,
`ffprobe` ve libmpv de olmalı.

```bash
dotnet build VidShrink.sln -c Release
dotnet test VidShrink.sln
```

Proje yerleşimi ve tasarım kuralları [`CONTRIBUTING.md`](CONTRIBUTING.md) içinde.

## Belgeler

[Kullanım](docs/kullanim.tr.md) · [Komut satırı](docs/cli.tr.md) · [Motor](docs/motor.tr.md) ·
[Kurulum ve güncelleme](docs/kurulum.tr.md) · [Ölçümler](docs/olcumler/) ·
[Yol haritası](docs/YOL-HARITASI.md) · [Sürüm notları](CHANGELOG.md) · [Katkı](CONTRIBUTING.md)

## Yol Haritası

Ölçülmüş, açık, bu sırayla — ayrıntı [`docs/YOL-HARITASI.md`](docs/YOL-HARITASI.md) içinde.

- **HandBrake'le kalan son açığı kapatmak: karanlık sahnede bantlaşma** (CAMBI 9,31 ve
  9,39'a karşı 6,48 ve 6,49). Ölçüt aynı düzenek, aynı kaynak ve o açığın sıfır olması.
- **AV1 dalının hedef altına düşmesi** — beş bant kaçağının beşi `libsvtav1`.
- **Ölçüm düzeneğinin zamanda hizalanması**, ki kare hızı düşüren planlar ölçülebilsin.
- **Tepe hızı tavanını açmak**, ki bu küçük hedeflerdeki donanım aşımını da düzeltiyor.
- **Ölçek ve kare hızı cezalarını ölçülmüş kaliteye göre kalibre etmek**.
- **Sahne başına kodlama** — ölçüldü, ürüne girmedi: kalite kapısını geçmedi
  ([`sahne-butcesi.md`](docs/olcumler/sahne-butcesi.md)).

## Katkı

Önce bir konu açın, değişikliği tek derde tutun, `dotnet test VidShrink.sln` koşun —
kırmızı hiçbir şey birleşmez — ve her commit'i [Developer Certificate of Origin](DCO)
uyarınca `git commit -s` ile imzalayın. Derleme talimatı, proje yerleşimi ve tasarım
kuralları: [`CONTRIBUTING.md`](CONTRIBUTING.md).

## Lisans

[AGPL-3.0-or-later](LICENSE). Telif hakkı (C) 2026 Teknesyum.

FFmpeg ve libmpv kendi lisansları altında ayrı programlardır; VidShrink ikisini de yeniden
dağıtmaz ve içine GPL kodu bağlamaz ([`docs/kurulum.tr.md`](docs/kurulum.tr.md)).

Kaynak ağacının içinde dağıtılan üçüncü taraf malzeme — simge takımının kesildiği Fluent UI
System Icons (MIT) — [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md) dosyasında.

<a id="kod-imzalama-politikasi"></a>

## Kod İmzalama Politikası

Ücretsiz kod imzalama [SignPath.io](https://about.signpath.io) tarafından, sertifika
[SignPath Foundation](https://signpath.org) tarafından sağlanır. Başvuru değerlendirmede;
onaylanana kadar Windows sürümleri imzasız çıkar.

**Gizlilik.** Bu program, kullanıcı ya da onu kuran veya çalıştıran kişi özellikle
istemedikçe başka ağ sistemlerine bilgi aktarmaz.

<details>
<summary>İmzayı kim onaylar, uygulamanın yapabildiği her istek</summary>

- Kod yazan, gözden geçiren ve onaylayan: [Teknesyum](https://github.com/Teknesyum)

Yalnız bu depodan GitHub Actions'ın derlediği dosyalar imzalanır; her imza isteği elle
onaylanır. Ekipteki herkes çok aşamalı doğrulamayla oturum açar.

Yapabildiği istekler, her birini neyin başlattığı ve o anda kimin gizlilik politikasının
geçerli olduğu:

- **Güncelleme denetimi** — GitHub'a son sürümü sorar. Windows'ta varsayılan açık;
  Ayarlar'dan kapatılır. [GitHub Gizlilik Bildirimi](https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement)
- **Kurucu** — FFmpeg ve libmpv'yi GitHub sürümlerinden, SHA-256'ya sabitlenmiş olarak indirir.
  [GitHub Gizlilik Bildirimi](https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement)
- **Paylaş** — dosyayı yalnız Paylaş'a bastığınızda, seçtiğiniz sunucuya yükler.
  [storage.to gizlilik](https://storage.to/privacy) · [uguu.se SSS](https://uguu.se/faq)
- **OpenSubtitles** — yalnız siz kullandığınızda oturum açar ve arar.
  [OpenSubtitles gizlilik politikası](https://www.opensubtitles.com/en/privacy/)
- **Ağ akışları** — oynatıcı bir adrese yalnız siz açtığınızda bağlanır.

</details>

<!-- signature -->
<div align="center">

<a href="https://github.com/sponsors/Teknesyum"><img src="docs/gorseller/badge-sponsor.svg" alt="Support Teknesyum" height="38"></a>
&nbsp;
<a href="LICENSE"><img src="docs/gorseller/badge-license.svg" alt="License AGPL-3.0" height="38"></a>

</div>
