# T33 — Oynatma mimarisi ölçümleri

**Tarih:** 24.08.2026 · **Sözleşme:** `.claude/relay/contracts/T33.md` · **Dayanak:** T30, T32

İki aday ölçüldü: **Aday A**, tek ffmpeg süreci + `hstack` + BGRA boru. **Aday B**, libmpv
render API. Karar kuralı sözleşmede; bu belge yalnız sayı üretir. `src/` altına tek satır
yazılmadı.

## Ortam

| Alan | Değer |
|---|---|
| Makine | DESKTOP-630ME6G, Windows 11 Pro 26100, 16 mantıksal çekirdek |
| ffmpeg | 9.0-full_build (gyan.dev), WinGet `Gyan.FFmpeg` |
| .NET | 8.0 (`%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe`) |
| Ölçüm komutu | `bench play <klipA,klipB> --only k2,p1,p2,p3,p5,p6` |

### Test klipleri

T30/T32 klipleri 30 fps'ti; oynatma kapısı 60 fps sorduğu için **60 fps kaynak** üretildi.
Aynı `testsrc2` yolu, `%TEMP%\vidshrink-play` altına:

```
ffmpeg -f lavfi -i "testsrc2=size=1920x1080:rate=60:duration=20" -c:v libx264 -preset veryfast -crf 23 -pix_fmt yuv420p 1080p_h264.mp4
ffmpeg -f lavfi -i "testsrc2=size=1920x1080:rate=60:duration=20" -c:v libx265 -preset veryfast -crf 25 -tag:v hvc1 -pix_fmt yuv420p 1080p_hevc.mp4
ffmpeg -f lavfi -i "testsrc2=size=1920x1080:rate=60:duration=20" -c:v av1_amf -b:v 6M -pix_fmt yuv420p 1080p_av1.mp4
```

4K karşılıkları `size=3840x2160`, av1 için `-b:v 20M`. Altı klip: 1080p ve 4K × h264,
hevc, av1. `testsrc2` yüksek entropili, yani kod çözme maliyeti kötümser tarafta.

## K2 — Bu makinedeki ffmpeg'de gerçekten ne var

Liste sorgusuyla değil, her yetenek için küçük bir girdiyle grafik kurup stderr'de
`Error parsing option` / `No such filter` aranarak sınandı.

| Yetenek | Deneme | Sonuç | stderr |
|---|---|---|---|
| `hstack` | iki lavfi girdi, `hstack=inputs=2` | **var** | — |
| `scale` + `format=bgra` | `scale=64:36,format=bgra` | **var** | — |
| `fps` | `fps=60` | **var** | — |
| `zscale` | `zscale=w=32:h=32` | **var** | — |
| `tonemap` | `setparams=HDR` → `zscale=t=linear,tonemap=hable,zscale=t=bt709` | **var** | — |
| rawvideo/bgra boru | `-f rawvideo -pix_fmt bgra -` | **var** | — |
| `-hwaccel d3d11va` | gerçek dosyada tek kare | **var** | — |
| `-hwaccel dxva2` | gerçek dosyada tek kare | **var** | — |
| `-hwaccel qsv` | gerçek dosyada tek kare | yok | `Failed to find d3d11va adapter by vendor id 0x8086` |
| `-hwaccel cuda` | gerçek dosyada tek kare | **var** | — |
| `-hwaccel vulkan` | gerçek dosyada tek kare | **var** | — |
| `d3d11va` + `hwdownload` | `-hwaccel_output_format d3d11 -vf hwdownload,format=nv12` | **var** | — |

Grafiğin ihtiyaç duyduğu her filtre bu makinede mevcut. `qsv` yok, çünkü makinede Intel
grafik yok — kalan dört hızlandırıcı çalışıyor.

**Tuzak:** `tonemap` ilk denemede "yok" göründü. Sebep filtre eksikliği değildi;
`testsrc2` SDR olduğu için `zscale` `no path between colorspaces` dedi. T32'nin bulgusuna
uyup girdi `setparams=color_primaries=bt2020:color_trc=smpte2084:colorspace=bt2020nc` ile
HDR etiketlendiğinde filtre çalıştı. Çıkış seçeneği olarak verilen
`-color_trc`/`-color_primaries` libx264/libx265 üzerinden sessizce düşüyor; HDR sınaması
`setparams` olmadan sahte negatif verir.

## Ö1 — Boru akışının hızı (Aday A)

Tek ffmpeg süreci, iki girdi, `fps` + `scale` + `hstack`, `-f rawvideo -pix_fmt bgra` ile
boruya çift genişlikte kare. Klipler: `1080p_h264` + `1080p_hevc`. n = 600 kare/satır.

Süre ölçümü **ilk kare geldikten sonra** başlıyor; süreç açılışı ayrı sütunda.

| Boyut | Kare | Açılış ms | Sürdürülen fps | Aralık p50 ms | p95 ms | p99 ms | max ms | MB/s | CPU % |
|---|---|---|---|---|---|---|---|---|---|
| **2×960×540** | 600 | 149,4 | **309,0** | 3,11 | 4,28 | **5,02** | 16,7 | 1222 | 637,5 |
| 2×1280×720 | 600 | 161,8 | 154,6 | 6,36 | 7,72 | 8,60 | 12,5 | 1087 | 371,0 |
| **2×1920×1080** | 600 | 177,3 | **37,5** | 26,53 | 29,28 | **30,47** | 36,6 | 594 | 137,3 |

CPU % = tüm çekirdeklere toplam (16 mantıksal çekirdek = %1600 tavan).

**Açılış 150-180 ms.** T32'nin 692-800 ms'lik p95 kuyruğu burada bir kez ödeniyor, kare
başına değil. Kalıcı sürecin amortisman iddiası doğrulandı: 600 karelik bir akışta açılış
kare başına 0,3 ms'e düşüyor. T32'nin "üçüncü kapıyı ikinciye taşıyabilecek tek aday"
dediği yol gerçekten çalışıyor — ama aşağıdaki Ö1b yeni bir duvar buluyor.

## Ö1b — Duvar nerede: boru mu, kod çözme mi

Aynı grafik, kareler boruya yazılmadan (`-f null -`). İki sayı arasındaki fark borunun
taşıma kapasitesi.

| Boyut | Boruyla fps | Borusuz fps | Boru kaybı % | Boru MB/s | Borusuz CPU % |
|---|---|---|---|---|---|
| 2×960×540 | 324,8 | 341,5 | **4,9** | 1285 | 600,4 |
| 2×1280×720 | 154,9 | 314,6 | **50,8** | 1089 | 677,6 |
| 2×1920×1080 | 37,3 | 171,8 | **78,3** | 590 | 468,3 |

**Duvar borunun kendisi, kod çözme değil.** 2×1080p'de ffmpeg 172 fps üretebiliyor ama
boru 37 fps teslim ediyor: kapasitenin %78'i taşımada kayboluyor. Kanıt CPU sütununda da
var — çözünürlük yükseldikçe ffmpeg'in CPU'su **düşüyor** (%638 → %137), çünkü süreç
kareyi hesaplamakla değil boruya yazmakla meşgul.

Ölçülen boru tavanı ~590 MB/s (16,6 MB'lık karelerde) ile ~1285 MB/s (4,1 MB'lık
karelerde) arasında. Kare büyüdükçe taşıma verimi düşüyor.

Bu, kullanıcının "1080p sınırı" itirazının sayısal karşılığı: sınır kod çözmede değil,
**ham kareyi işlemciden geçirmekte.** Konseyin öngördüğü ~1 GB/s rakamı doğru çıktı ve
makinenin boru tavanı tam oraya düşüyor.

## K3 — Duvara çarpınca fps mi düşecek, çözünürlük mü

Çözünürlük sabit tutulup fps merdiveni çıkıldı.

| Boyut | Hedef fps | Sürdürülen fps | Hedefi tutuyor mu | p99 ms | MB/s | CPU % |
|---|---|---|---|---|---|---|
| 2×1920×1080 | 60 | 38,1 | hayır | 30,35 | 604 | 138,1 |
| 2×1920×1080 | 48 | 37,7 | hayır | 30,75 | 597 | 153,6 |
| **2×1920×1080** | **30** | 38,1 | **evet** | 30,79 | 603 | 188,0 |
| 2×1920×1080 | 24 | 37,6 | **evet** | 29,28 | 595 | 233,2 |
| 2×1280×720 | 60 | 153,5 | **evet** | 8,84 | 1080 | 369,7 |
| 2×1280×720 | 48 | 156,1 | **evet** | 8,60 | 1098 | 459,7 |
| 2×1280×720 | 30 | 158,6 | **evet** | 8,65 | 1116 | 578,6 |
| 2×1280×720 | 24 | 150,5 | **evet** | 11,58 | 1058 | 645,3 |

**Konseyin kararı destekleniyor.** Sürdürülen fps hedeften bağımsız olarak 2×1080p'de
~37,7, çünkü duvar bayt/saniye cinsinden ve kare boyutu değişmiyor. Yani:

- 2×1920×1080'de **fps'i 30'a düşürmek tam çözünürlüğü kurtarıyor** — 37,7 > 30, hedef
  rahat tutuluyor.
- Aynı rahatlığı çözünürlük düşürerek almak da mümkün (2×1280×720'de 60 fps geçiyor), ama
  o zaman incelenecek artefakt yok oluyor.

İki yol da aynı bayt bütçesini serbest bırakıyor; artefakt incelemek panelin varlık sebebi
olduğuna göre **fps düşer, çözünürlük düşmez** kararı ölçümle uyumlu. Boru yolunda
2×1080p'nin pratik tavanı **30 fps.**

## Ö6 — Kare başına bellek ayırma

Sabit havuzdan okuma ile kare başına yeni tampon ayırma karşılaştırıldı.

| Boyut | Okuma biçimi | Kare | Kare başı ayrılan bayt | Kare boyutu bayt | Sürdürülen fps |
|---|---|---|---|---|---|
| 2×960×540 | **sabit havuz** | 600 | **20 330** | 4 147 200 | 316,2 |
| 2×960×540 | kare başı yeni tampon | 600 | 4 168 965 | 4 147 200 | 308,3 |
| 2×1280×720 | **sabit havuz** | 600 | **36 008** | 7 372 800 | 156,1 |
| 2×1280×720 | kare başı yeni tampon | 600 | 7 409 516 | 7 372 800 | 143,9 |
| 2×1920×1080 | **sabit havuz** | 600 | **81 118** | 16 588 800 | 38,2 |
| 2×1920×1080 | kare başı yeni tampon | 600 | 16 670 572 | 16 588 800 | 38,0 |

Sabit havuzla kare başına ayrılan bayt **kare boyutunun %0,5'i**; kalan 20-81 KB `async`
okuma makinesinin kendi ayırması, kare tamponu değil. Hedef (sıfır kopya, sabit havuz)
tutuldu.

Naif yol 1080p'de fps'i düşürmüyor — çünkü orada zaten boru duvarına yaslanmış durumda.
Asıl zararı 720p'de görünüyor: %8 fps kaybı. Konseyin uyarısı yerinde ama duvarın
gerisinde kalıyor.

## Ö3 — Donanım kod çözme

2×1920×1080 panel, aynı klipler.

| Yol | Açılış ms | Sürdürülen fps | p99 ms | MB/s | CPU % |
|---|---|---|---|---|---|
| yazılım (taban) | 201,0 | 38,9 | 32,45 | 615 | **140,3** |
| `d3d11va` örtük indirme | 270,5 | 38,4 | 29,57 | 607 | **107,9** |
| `d3d11va` + `hwdownload` | **1882,2** | 39,1 | 29,61 | 619 | **98,2** |
| `dxva2` örtük indirme | 768,4 | 38,5 | 31,08 | 610 | 129,9 |

**Donanım kod çözme fps kazandırmıyor da kaybettirmiyor da** — dört yol da 38-39 fps'te,
çünkü sınırlayan boru. Kazancı CPU'da: %140 → %98, yani üçte bir daha az işlemci.

Konseyin "`hwdownload` pahalı olabilir" uyarısı **kısmen doğru**: kare başına maliyeti
yok, ama açılışa 1,7 saniye ekliyor (201 → 1882 ms). Kalıcı süreçte bu bir kez ödenir, ama
kullanıcı dosyayı her değiştirdiğinde yeniden ödenir.

Öneri: `d3d11va` örtük indirme. CPU'yu %23 düşürüyor, açılışa 70 ms ekliyor.

## Ö5 — Codec matrisi

Aynı klip iki kez, altı kaynak. n = 600 kare/satır.

**2×960×540 panelde:**

| Klip | Açılış ms | Sürdürülen fps | p50 ms | p95 ms | p99 ms | MB/s | CPU % |
|---|---|---|---|---|---|---|---|
| 1080p h264 | 140,3 | 328,4 | 2,92 | 4,06 | 5,18 | 1299 | 534,5 |
| 1080p hevc | 157,0 | 326,6 | 2,97 | 4,02 | 4,98 | 1292 | 794,0 |
| 1080p av1 | 89,9 | 334,3 | 2,93 | 3,80 | 4,28 | 1322 | 654,9 |
| 4K h264 | 306,0 | 200,1 | 4,12 | 9,58 | 11,93 | 791 | 1150,2 |
| 4K hevc | 374,8 | 154,0 | 4,63 | 15,72 | 23,20 | 609 | 1210,6 |
| 4K av1 | 118,4 | 149,5 | 6,64 | 9,48 | 11,09 | 591 | 734,9 |

**Altı kombinasyonun altısı da 60 fps'i tutturuyor.** En yavaşı 4K av1 ile 149,5 fps, yani
hedefin 2,5 katı. 4K kaynaklarda p99 yükseliyor (4K hevc'de 23,2 ms) ama 25 ms eşiğinin
altında kalıyor. 4K hevc CPU'yu %1210'a çıkarıyor — 16 çekirdeğin dörtte üçü.

**2×1920×1080 panelde:**

| Klip | Açılış ms | Sürdürülen fps | p99 ms | MB/s | CPU % |
|---|---|---|---|---|---|
| 1080p h264 | 739,8 | 37,4 | 31,51 | 593 | 129,7 |
| 1080p hevc | 1777,9 | 38,0 | 30,55 | 601 | 149,4 |
| 1080p av1 | 124,9 | 37,9 | 30,05 | 600 | 144,8 |
| 4K h264 | 1957,1 | 38,5 | 29,99 | 609 | 240,6 |
| 4K hevc | 1990,2 | 38,4 | 29,80 | 607 | 313,8 |
| 4K av1 | 1768,5 | 38,1 | 29,58 | 602 | 264,4 |

**Altısı da aynı yere düşüyor: ~38 fps.** Codec'in hiçbir etkisi yok, çünkü sınırlayan
kod çözme değil boru. Ö1b'nin bulgusunu bağımsız olarak doğruluyor: kaynak ne olursa olsun
duvar ~600 MB/s.

Not: `av1_amf` ile üretilen 1080p klip 1920×1082 çıktı (kodlayıcının hizalama dolgusu).
Ölçümü etkilemiyor, panel zaten yeniden ölçekliyor.

## Ö2 — Kodlama koşarken (ÖN SONUÇ, yöntem düzeltiliyor)

> **Bu bölüm kesin değil.** İki geçiş yapıldı, ikisi de aynı yöntem hatasını taşıyor:
> ölçüm boruyu kodlama boyunca **defalarca yeniden başlatıyor**, oysa mimarinin iddiası
> tek kalıcı süreç. Süreç sağanağı hem kodlamayı hem boruyu haksız yere cezalandırıyor.
> Düzeltilmiş ölçüm (tek `-stream_loop` borusu + 3 kodlamanın medyanı) koda yazıldı,
> derleniyor, **çalıştırılmadı.**

Kaynak `1080p_h264` (18,8 MB), hedef 5 MB. İlk denemede hedef 20 MB verilmişti; kaynak
zaten 18,8 MB olduğu için plan geçişli çıktı ve kodlama 0,01 sn sürdü — o geçiş atıldı.

| Boyut | Boru kipi | Boş kodlama s | Boru koşarken s | Kodlama yavaşlaması % | Akış fps | fps kaybı % |
|---|---|---|---|---|---|---|
| 2×960×540 | azami hız | 6,15 | 10,01 | 62,7 | 220,7 | 36,3 |
| 2×960×540 | `-re` 60 fps | 6,15 | 8,37 | **36,1** | 61,8 | −0,1 |
| 2×1280×720 | azami hız | 6,15 | 9,30 | 51,2 | 114,2 | 24,9 |
| 2×1280×720 | `-re` 60 fps | 6,15 | 9,21 | **49,6** | 61,7 | 0,1 |
| 2×1920×1080 | azami hız | 6,15 | 10,74 | 74,5 | 34,8 | 8,1 |
| 2×1920×1080 | `-re` 30 fps | 6,15 | 11,30 | **83,6** | 30,5 | −0,5 |

Şimdilik okunabilen tek şey: **throttle edilmiş boru kendi hedefini şaşmadan tutuyor**
(fps kaybı ≈ %0, üç boyutta da). Kodlamaya verdiği zarar ise ön sonuçta %36-84 ve
kapının %10 eşiğinin çok üstünde — ama bu sayı süreç sağanağını da içeriyor, bu yüzden
karar için kullanılamaz.

## Kapı kararları

Kapılar iki aday için ayrı ayrı uygulanır. **Aday B ölçülmedi**, o yüzden onun satırları boş.

| Kapı | Ölçü | Aday A (boru) | Aday B (libmpv) |
|---|---|---|---|
| **G1** | 2×960×540'ta ≥60 fps, p99 < 25 ms | **GEÇTİ** — 309 fps, p99 5,02 ms | ölçülmedi |
| **G2** | 2×1920×1080'de aynısı | **KALDI** — 37,5 fps, p99 30,5 ms | ölçülmedi |
| **G3** | Kodlama koşarken yavaşlama ≤ %10 | karara bağlanmadı (ön sonuç %36-84) | ölçülmedi |

Cümleyle:

> **Boru yaklaşımı G1'den rahat geçiyor:** 2×960×540 panelde 309 fps sürdürüyor, kare
> aralığı p99'u 5,02 ms ile 25 ms eşiğinin beşte biri. Codec matrisi bunu altı kaynak
> için de doğruluyor; en yavaş kombinasyon (4K av1) bile 149,5 fps veriyor.

> **Boru yaklaşımı G2'de kalıyor:** 2×1920×1080 panelde 37,5 fps ve p99 30,5 ms; hem fps
> hem gecikme eşiği tutmuyor. Sebep kod çözme değil, borunun kendisi — aynı grafik boruya
> yazmadan 171,8 fps üretiyor, yani kapasitenin %78'i ham kareyi işlemciden geçirirken
> kayboluyor. Donanım kod çözme bunu kurtarmıyor (38-39 fps'te kalıyor), çünkü sorun kod
> çözmede değil.

> **G3 için boru hakkında henüz geçti/kaldı denemez.** Ön sonuçlar eşiğin çok üstünde
> (%36-84) ama ölçüm yöntemi kalıcı tek süreci değil süreç sağanağını ölçüyordu; düzeltilmiş
> koşum yapılmadı.

> **Aday B (libmpv) hiçbir kapıdan geçmedi ya da kalmadı — sırası gelmeden durduruldu.**

Karar kuralı bu tabloyla **henüz işletilemiyor**: G3'ün boru için sonucu ve Aday B'nin
tamamı eksik.

## Ölçülmeyenler

Sırayla, devam eden oturumun başlayacağı yer en üstte.

1. **Ö7 — Aday B (libmpv) hiç ölçülmedi. Sırası gelmedi.** Ne fps, ne kare aralığı, ne CPU,
   ne kurulum MB'ı, ne Linux durumu, ne lisans, ne ses, ne 4K, ne çökme. Hiçbiri.
   Makinede mpv veya `libmpv-2.dll` kurulu değil (`PATH`'te yok, `C:\Program Files\mpv` yok).
   **Engel:** ölçüm için `libmpv` ikilisinin indirilmesi gerekiyor ve dosya indirmek
   kullanıcının açık iznine bağlı; bu oturumda o izin istenmedi. Devam eden oturum önce bu
   izni almalı. Sözleşme `RAPOR.md:106` gereği shinchiro yapılarının kullanılamayacağını
   söylüyor, yani kaynak seçimi de ayrıca karara bağlanacak.
2. **Ö2 düzeltilmiş koşumu.** Tek `-stream_loop` borusu + 3 kodlamanın medyanı koda yazıldı
   ve derleniyor, çalıştırılmadı. `bench play <a,b> --only p2 --seconds 10 --target 5`.
   G3'ün boru için kararı buna bağlı.
3. **Ö4 — Arayüze yükleme ölçülmedi.** Avalonia `WriteableBitmap` ile çift tamponlu sunum
   ve atılabilir pencerede sunulan fps. `VidShrink.Bench` konsol projesi ve Avalonia
   referansı yok; csproj `owns` dışında olduğu için ölçüm scratchpad'de ayrı bir harness
   isteyecek. Boru 2×960×540'ta 309 fps üretiyor, ama sunum yolunun bunu taşıyıp taşımadığı
   **bilinmiyor.**
4. **Ses ölçülmedi** (boru zaten veremiyor; mpv tarafı ölçülmediği için karşılaştırma yok).
5. **Boru duvarının sebebi ayrıştırılmadı.** ~590 MB/s (16,6 MB kare) ile ~1285 MB/s
   (4,1 MB kare) arasında değişiyor; kare büyüdükçe verim düşüyor. Adlandırılmış boru,
   paylaşımlı bellek ya da daha büyük boru tamponu denenmedi — duvarı kaldırabilirler.
6. **Gerçek kamera kaydıyla tekrarlanmadı.** `testsrc2` yüksek entropili; kod çözme
   maliyeti kötümser tarafta, boru maliyeti ise içerikten bağımsız.
7. **`cuda` ve `vulkan` hızlandırıcıları** K2'de var çıktı ama Ö3'te ölçülmedi.

## Doğrulama

| Kontrol | Sonuç |
|---|---|
| `dotnet build VidShrink.sln -c Release` | 0 uyarı, 0 hata |
| `dotnet test VidShrink.sln -c Release` | 306 başarılı, 0 başarısız, 6 atlanan |
| `git status -- src/ VidShrink.sln` | çıktı boş — **K1 sağlandı** |

Atlanan 6 test T33'ten önce de atlanıyordu (`VIDSHRINK_LIVE_SOURCE` isteyen canlı ölçümler).
Değişen tek kaynak dosyası `tools/VidShrink.Bench/Program.cs`.
