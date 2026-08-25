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

Komut: `bench play <klipA,klipB> --only k2`

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

> **GEÇERSİZ — tur 1 ve tur 2 çürüttü (2×1920×1080 satırı).** Bu tablo kareyi tek bir
> `read` çağrısında alan tüketiciyle ölçüldü. Tur 1 (Ö8) o okuma biçiminin kendisinin
> darboğaz olduğunu gösterdi; tur 2 (Ö11) parçalı okumayla aynı boyutta **107,6-116,7 fps**
> ölçtü. Buradaki **37,5 fps** ve **p99 30,47 ms** borunun kapasitesi değil, okuma
> biçiminin cezasıdır. Kapı kararı için Ö11 tablosuna bakılacak. 2×960×540 ve 2×1280×720
> satırları da aynı sebeple düşük; onlar da Ö11'de yeniden ölçüldü.

Komut: `bench play <klipA,klipB> --only p1 --seconds 10 --fps 60`

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

> **GEÇERSİZ — tur 1 çürüttü.** "%78,3 boru kaybı" ve "duvar borunun kendisi" yargısı,
> yukarıdaki geçersiz Ö1 sayısından türetilmişti. Tur 1'in Ö10 ölçümü aynı boruda
> **161,8 fps** taşıma ölçtü; tur 2'nin Ö11'i parçalı okumayla **107,6-116,7 fps** kare
> teslimi ölçtü. Boru kaybı %78 değil. Bu bölümün tek geçerli kalan gözlemi, ölçülen
> MB/s'in ~1,7-1,8 GB/s bandına çıkabildiğidir (Ö11).

Komut: `bench play <klipA,klipB> --only p1b --seconds 10 --fps 60`

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

> **KISMEN GEÇERSİZ — tur 1 ve tur 2 çürüttü.** Tablodaki sayılar tek okumalı tüketiciyle
> alındı, o yüzden 2×1080p satırlarının ~37,7 fps tavanı gerçek değil. Bölümün sonundaki
> **"boru yolunda 2×1080p'nin pratik tavanı 30 fps"** cümlesi geçersizdir: Ö11 aynı
> boyutta 107,6 fps ölçtü, yani 60 fps hedefi çözünürlük düşürmeden tutuluyor.
> **Geçerli kalan kısım:** duvarın bayt/saniye cinsinden olduğu ve sürdürülen fps'in
> hedeften bağımsız çıktığı gözlemi; "fps düşer, çözünürlük düşmez" kuralı da bu ölçümle
> çelişmiyor — ama bugün 2×1080p'de düşürülecek bir şey yok.

Komut: `bench play <klipA,klipB> --only k3 --seconds 10`

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

Komut: `bench play <klipA,klipB> --only p6 --seconds 10 --fps 60`

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

Komut: `bench play <klipA,klipB> --only p3 --seconds 10 --fps 60`

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

Komut: `bench play <klipA,klipB> --only p5 --seconds 10 --fps 60 --matrix <altı klip virgülle>`

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

> **GEÇERSİZ — tur 2 yeniden ölçtü.** Ön sonuçtaki %36-84 yavaşlama süreç sağanağını da
> içeriyordu ve boru tarafı tek okumalı tüketiciyle koşuyordu. Geçerli G3 sayısı belgenin
> sonundaki tur 2 bölümündedir.

Komut: `bench play <klipA,klipB> --only p2 --seconds 10 --fps 60 --target 5`

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

> **BU TABLO GEÇERSİZ — tur 2'nin kapı tablosu geçerlidir.** Belgenin sonundaki
> "Tur 2 — kapılar iki ayağıyla" bölümüne bakılacak. G1 ve G2 orada yeniden karara
> bağlandı, G3 orada ölçüldü.

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
5. ~~**Boru duvarının sebebi ayrıştırılmadı.**~~ **Tur 1'de ayrıştırıldı:** sebep okuma
   biçimiydi (Ö8). Aşağıdaki cümleler geçersizdir. ~590 MB/s (16,6 MB kare) ile ~1285 MB/s
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

---

# Tur 1 — duvar boru değilmiş

Makine: DESKTOP-0J80KVV · 16 mantıksal çekirdek · ffmpeg 9.0-full_build (Gyan)
Kaynak: `C:/Users/Administrator/Videos/gothic2026-08-15 14-01-29.mp4`, iki taraf da aynı klip.
Envanter (`ffprobe -v error -select_streams v:0 -show_entries stream=codec_name,profile,width,height,r_frame_rate,pix_fmt`):
h264 High · 1920×1080 · 48/1 fps · yuv420p · 52,65 s · 870 496 767 bayt · ~132 Mbit/s.
Gerçek bir ekran kaydı, yeniden üretilemez; envanteri eşdeğer bir kaynak seçilebilsin diye verildi.
Her ölçüm **3 koşu**, 5 saniye, 300 kare. Ölçen: T0 (üç ajan üst üste düştü).

## P10 — taşıma tavanı: duvar boru mu, tüketici mi

Komut: `bench play <klip,klip> --only p10 --seconds 5 --fps 60 --runs 3`

### 2×1920×1080 — 15,8 MB kare

| Çıkış yolu | Koşu | Ortalama fps | Sapma | Tek tek |
|---|---|---|---|---|
| `-f null -` (kare hiç paketlenmiyor) | 3 | 267,4 | 8,75 | 267 / 258,8 / 276,3 |
| `-f rawvideo` → NUL (paketlenir, boru yok) | 3 | 213,6 | 12,97 | 198,6 / 221,5 / 220,6 |
| **boru → ham boşaltma, 1 MB blok** | 3 | **161,8** | 0,65 | 161,3 / 162,5 / 161,7 |
| **boru → kare hizalı havuz okuması** | 3 | **70,1** | 0,20 | 69,9 / 70,3 / 70,1 |

### 2×960×540 — 4 MB kare

| Çıkış yolu | Koşu | Ortalama fps | Sapma |
|---|---|---|---|
| `-f null -` | 3 | 302,8 | 8,59 |
| `-f rawvideo` → NUL | 3 | 305,5 | 4,44 |
| boru → ham boşaltma | 3 | 407,9 | 11,01 |
| boru → kare hizalı havuz | 3 | 387,9 | 5,09 |

## P8 — okuma bloğu büyütülünce ne oluyor

Komut: `bench play <klip,klip> --only p8 --seconds 5 --fps 60 --runs 3`

### 2×1920×1080 — 15,8 MB kare

| Okuma bloğu | Koşu | Ortalama fps | Sapma |
|---|---|---|---|
| 64 KB | 3 | 163,3 | 1,84 |
| 1 MB | 3 | 165,2 | 0,99 |
| **1 kare (15,8 MB) tek okumada** | 3 | **70,9** | 0 |
| 2 kare | 3 | 41,8 | 0,11 |
| 4 kare | 3 | 22,6 | 0,08 |
| **kare havuzu, 64 KB parçalarla toplanır** | 3 | **148,0** | 0,29 |
| kare havuzu, 256 KB parçalarla toplanır | 3 | 148,3 | 0,40 |
| kare havuzu, 1 MB parçalarla toplanır | 3 | 143,9 | 0,69 |

## Sonuç — round 0'ın vardığı yargı yanlış

Round 0 şunu yazmıştı: *"2×1080p'de ffmpeg 172 fps üretiyor, boru 37,5 teslim ediyor,
%78 taşımada kayboluyor. Duvar boru, kod çözme değil."*

**Boru 2×1080p'de 161,8 fps taşıyor.** Round 0'ın 37,5 rakamı borunun kapasitesi değil,
**okuma biçiminin** sonucuymuş.

P8 kök nedeni tek satırda gösteriyor: kareyi **tek okumada** almak 70,9 fps veriyor,
**aynı kareyi 64 KB parçalarla toplamak 148 fps** veriyor. İki katından fazla fark, ve
okuma bloğu büyüdükçe durum kötüleşiyor — 2 kare 41,8, 4 kare 22,6.

Sebep: Windows anonim borusunun iç tamponu küçük. 15,8 MB'lık tek bir okuma isteği
tamamı gelene kadar blokluyor ve üretici ile tüketiciyi sıraya sokuyor. Küçük parçalarla
okumak ikisini birlikte çalıştırıyor.

### Karar için anlamı

| | Ölçülen | Hedef |
|---|---|---|
| Boru taşıma tavanı, 2×1080p | 161,8 fps | — |
| Parçalı okumayla kare teslimi, 2×1080p | **148 fps** | 60 fps |
| Round 0'ın "duvar" dediği sayı | 37,5 fps | — |

**60 fps @ 2×1080p boru yoluyla karşılanıyor, hem de 2,5 kat payla.**

libmpv'nin +60-100 MB kurulumu, karışık GPLv2+/LGPL lisansı, Linux self-contained sorunu
ve süreç içi çökme riski **kullanıcının istediği hedef için gerekli değil.** Ö7 açılmadı.

Ses ve 4K hâlâ yalnız libmpv'de; onlar ayrı bir gerekçe ve ayrı bir karar.

### P9 ölçülmedi — gerekmedi

Paylaşımlı bellek, borunun yetersiz kaldığı varsayımı üzerine planlanmıştı. Boru 148 fps
verdiğine göre çözülecek bir sorun yok. Ölçüm koşulmadı; gerekirse sonra koşulabilir.

### Sıradaki soru

Duvar artık okuma tarafında değil. Geriye **Ö4** kalıyor: Avalonia `WriteableBitmap`
sunum yolu bu kareleri ekrana koyabiliyor mu? İkinci görüş de bağımsız olarak buraya
işaret etmişti. 148 fps üretilen kareyi ekrana basamıyorsa darboğaz oraya taşınmış olur.

---

# Tur 2 — kapılar iki ayağıyla

Denetçi üç KRİTİK verdi: G2 hiç cevaplanmamıştı (aralık istatistiği toplanmıyordu),
çürütülmüş sayılar işaretsiz duruyordu, ölçüm satırlarında komut yoktu. Bu bölüm üçünü de
kapatır. **Yukarıdaki geçersizlik notları bu turda düşüldü.**

## Ortam ve kaynaklar

| Alan | Değer |
|---|---|
| Makine | DESKTOP-0J80KVV · 16 mantıksal çekirdek |
| ffmpeg | 9.0-full_build-www.gyan.dev |
| .NET | 9.0.316, `PATH` üzerinde (`dotnet --version`) |

Sözleşmedeki "`PATH` üzerindeki dotnet 3.1.201" ortam notu **artık geçerli değil**;
bu makinede `PATH` üzerindeki dotnet 9.0.316 ve `net8.0` hedeflerini sorunsuz derliyor.

İki kaynak kullanıldı, çünkü tur 1'in tek kaynağı gerçek bir ekran kaydıydı ve
üretilemiyordu:

**A — sentetik, yeniden üretilebilir:**

```
ffmpeg -hide_banner -loglevel error -y -f lavfi -i "testsrc2=size=1920x1080:rate=60:duration=20" -c:v libx264 -preset veryfast -crf 20 -pix_fmt yuv420p t33-src.mp4
```

h264 High · 1920×1080 · 60/1 fps · yuv420p · 20,0 s · 27 691 119 bayt.

**B — gerçek ekran kaydı:** `C:/Users/Administrator/Videos/gothic2026-08-15 14-01-29.mp4`,
h264 High · 1920×1080 · 48/1 fps · yuv420p · 52,65 s · 870 496 767 bayt · ~132 Mbit/s.
Üretilemez; envanteri eşdeğerini seçebilmek için verildi. Her ölçümde iki taraf da
aynı klip.

## Ö11 — kapı ölçümü: sürdürülen fps **ve** aralık p99

Tur 1'in 148 fps'i üreten yolu (kare sabit havuza **64 KB parçalarla** toplanır) aralık
istatistiği toplayacak şekilde tamamlandı: `PipeAsync` artık parçalı okur ve her kare
sınırında aralık kaydeder. Kapının p99 ayağı için koşuların **en kötü** p99'u alınır;
ayrıca 25 ms'i aşan kare yüzdesi sayılır.

Komut (kaynak A):

```
bench play <A,A> --only p11 --seconds 5 --fps 60 --runs 5
```

| Boyut | Boru kipi | Koşu | Ortalama fps | Sapma | Tek tek fps | p50 ms | p95 ms | Ortalama p99 ms | En kötü p99 ms | En kötü max ms | 25 ms üstü kare % | MB/s | CPU % |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 2×960×540 | azami hız | 5 | 348,0 | 22,57 | 358 / 338,5 / 313 / 361,1 / 369,3 | 2,49 | 5,65 | 9,20 | 11,60 | 14,2 | **0** | 1376,3 | 370,5 |
| 2×960×540 | -re 60 fps | 5 | 63,7 | 0,18 | 63,9 / 63,6 / 63,5 / 63,6 / 63,9 | 15,52 | 30,99 | 36,47 | 52,25 | 61,2 | 8,70 | 251,9 | 13,0 |
| 2×1280×720 | azami hız | 5 | 253,5 | 13,77 | 255,9 / 258,1 / 229,6 / 259,7 / 264,4 | 3,84 | 5,17 | 6,85 | 7,41 | 10,5 | **0** | 1782,8 | 44,5 |
| 2×1280×720 | -re 60 fps | 5 | 63,5 | 0,23 | 63,2 / 63,4 / 63,8 / 63,5 / 63,6 | 15,58 | 30,87 | 38,84 | 61,52 | 72,5 | 8,43 | 446,8 | 16,7 |
| **2×1920×1080** | **azami hız** | 5 | **108,4** | 5,38 | 101,3 / 104,5 / 109,3 / 113 / 113,7 | 9,04 | 11,85 | 14,45 | **16,41** | 19,4 | **0** | 1714,4 | 30,5 |
| 2×1920×1080 | -re 60 fps | 5 | 63,4 | 0,22 | 63,2 / 63,2 / 63,4 / 63,7 / 63,3 | 15,53 | 29,69 | 32,64 | 33,56 | 37,3 | 6,29 | 1002,4 | 7,0 |

Komut (kaynak B):

```
bench play <B,B> --only p11 --seconds 5 --fps 60 --runs 5
```

| Boyut | Boru kipi | Koşu | Ortalama fps | Sapma | Tek tek fps | p50 ms | p95 ms | Ortalama p99 ms | En kötü p99 ms | En kötü max ms | 25 ms üstü kare % | MB/s | CPU % |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 2×960×540 | azami hız | 5 | 278,6 | 28,26 | 291,4 / 252,5 / 254,4 / 274,5 / 320,4 | 2,38 | 9,98 | 17,19 | 22,47 | 37,1 | 0,20 | 1102,0 | 1022,5 |
| 2×960×540 | -re 60 fps | 5 | 63,2 | 0,24 | 63,2 / 63,5 / 63 / 63,2 / 62,9 | 15,23 | 32,20 | 38,39 | 44,64 | 80,9 | 24,62 | 249,8 | 28,2 |
| 2×1280×720 | azami hız | 5 | 244,3 | 11,79 | 242,8 / 235 / 237,1 / 264,6 / 241,8 | 3,38 | 8,31 | 13,65 | 15,15 | 23,6 | **0** | 1717,6 | 939,3 |
| 2×1280×720 | -re 60 fps | 5 | 63,2 | 0,19 | 63,1 / 63,2 / 63,1 / 63,5 / 62,9 | 15,02 | 32,32 | 37,40 | 45,84 | 49,2 | 24,48 | 444,1 | 32,1 |
| **2×1920×1080** | **azami hız** | 5 | **107,6** | 6,85 | 97,4 / 106,7 / 110,2 / 107,3 / 116,3 | 8,44 | 15,18 | **28,22** | **59,63** | 64,9 | **1,00** | 1701,7 | 383,9 |
| 2×1920×1080 | -re 60 fps | 5 | 63,6 | 0,84 | 63,4 / 63 / 63,4 / 62,9 / 65 | 13,91 | 33,09 | 39,55 | 47,26 | 72,6 | 16,05 | 1005,5 | 55,5 |

### `-re` satırları kapıyı ölçmüyor

`-re` kipinde p95 her üç boyutta da ~30-33 ms, yani **iki kare periyodu**. Bu, boru
kapasitesinden bağımsızdır: 2×960×540'ta boru hedefin beş katı hızda çalışabiliyorken de
aynı 31 ms çıkıyor. Sebep ffmpeg'in gerçek zamanlı hız sınırlayıcısının kareleri ikişerli
salvolar hâlinde vermesi. Yani `-re`'nin p99'u **ffmpeg'in temposu**, borunun gecikmesi
değil. Kapı bu yüzden "azami hız" satırlarından okunur; üretimde tempoyu oynatıcının kendi
saati kurar.

## Ö12 — kuyruklu tüketici: **ölçülemedi**

60 fps son tarihinde karenin hazır olup olmadığını sayan bir düzenek yazıldı
(`PipeQueuedAsync`, sınırlı kanal + son tarihe göre çeken tüketici):

```
bench play <A,A> --only p12 --seconds 5 --fps 60 --runs 3
bench play <B,B> --only p12 --seconds 5 --fps 60 --runs 3
```

Düzenek **güvenilir sayı üretmedi ve sonuçları rapora alınmadı.** Gerekçe: değerler kuyruk
derinliğinde tekdüze değil (kaynak A, 2×1280×720: derinlik 1 → %25,6 kaçırma; derinlik 3 →
%0; derinlik 8 → %14,8) ve panel boyutundan bağımsız çok saniyelik duraklamalar çıkıyor
(en kötü gecikme 8 481 ms). Boru tarafı Ö11'de 100+ fps sürdürdüğüne göre bu duraklamalar
ölçülen sistemin değil düzeneğin özelliği. Kod dosyada duruyor, sayısı yok.

**Bunun anlamı:** kuyruk derinliğinin ne olması gerektiği sorusu **açık kaldı.** Ö11'in
"azami hız" satırları borunun kapasitesini veriyor, bir tüketicinin o kareleri 60 fps
son tarihinde alıp alamayacağını vermiyor.

## Ö2 — kodlama koşarken (G3)

Yöntem: boş kodlamanın süresi **3 koşunun medyanı** olarak bir kez alınır, sonra her
yapılandırmada tek kalıcı boru (`-stream_loop -1`) yanında aynı kodlama yine 3 kez koşar.
Boru artık parçalı okuyor, yani tur 1'in kazanan yolu ölçülüyor.

Komut (kaynak A, hedef 5 MB):

```
bench play <A,A> --only p2 --seconds 5 --fps 60 --target 5
```

| Boyut | Boru kipi | Boş kodlama s | Boru koşarken kodlama s | Kodlama yavaşlaması % | Akış fps | fps kaybı % | p99 ms |
|---|---|---|---|---|---|---|---|
| 2×960×540 | azami hız | 6,48 | 12,35 | **90,5** | 232,2 | 43,7 | 22,92 |
| 2×960×540 | -re 60 fps | 6,48 | 12,97 | **100,2** | 60,4 | 5,6 | 47,66 |
| 2×1280×720 | azami hız | 6,48 | 18,27 | **181,9** | 148,7 | 25,3 | 39,47 |
| 2×1280×720 | -re 60 fps | 6,48 | 7,67 | **18,3** | 60,8 | 3,7 | 40,60 |
| 2×1920×1080 | azami hız | 6,48 | 10,35 | **59,6** | 107,0 | 15,0 | 18,79 |
| 2×1920×1080 | -re 60 fps | 6,48 | 16,91 | **160,8** | 60,3 | 5,1 | 37,07 |

**Sayıların dağılımı geniş ve tek tek güvenilir değil:** %18,3 ile %181,9 arası, üstelik
yavaşlama panel boyutuyla tekdüze artmıyor. Sebep düzenekte: boş kodlama süresi (6,48 s)
tabloda bir kez, en başta ölçülüyor; alttaki satırlar ~20 dakika sonra alınıyor ve makine
durumu bu sürede kayıyor. Bir satırın tam değerine dayanılamaz.

**Karara yeten kısım şu:** altı yapılandırmanın **hiçbiri** %10 eşiğinin altında değil.
En iyi ölçülen değer %18,3, yani kapının iki katı; medyanı ~%75. Kapının düştüğü, ölçüm
gürültüsüyle açıklanabilecek bir fark değil.

Akış tarafı ise sağlam: `-re 60 fps` kipinde kodlama yanı başında koşarken bile boru
60,3-60,8 fps sürdürüyor, fps kaybı %3,7-5,6. Yani **oynatma kodlamadan zarar görmüyor,
kodlama oynatmadan görüyor.**

## Kapı kararları — tur 2 (geçerli tablo)

Kapılar iki aday için ayrı ayrı uygulanır. **Aday B (libmpv) hâlâ ölçülmedi** — tur 1
kararıyla açılmadı.

| Kapı | Ölçü | Aday A (boru) | Aday B (libmpv) |
|---|---|---|---|
| **G1** | 2×960×540'ta ≥60 fps **ve** p99 < 25 ms | **GEÇTİ** | ölçülmedi |
| **G2** | 2×1920×1080'de aynısı | **fps ayağı GEÇTİ, p99 ayağı kaynağa göre değişiyor** | ölçülmedi |
| **G3** | Kodlama koşarken yavaşlama ≤ %10 | **KALDI** | ölçülmedi |

Cümleyle:

> **G1 geçti.** 2×960×540'ta boru sentetik kaynakta 348,0 fps, gerçek kayıtta 278,6 fps
> sürdürüyor; en kötü koşunun aralık p99'u 11,60 ms ve 22,47 ms, ikisi de 25 ms eşiğinin
> altında. Kapının iki ayağı da her iki kaynakta karşılanıyor.

> **G2'nin fps ayağı geçti, p99 ayağı sentetik kaynakta geçti gerçek kayıtta kaldı.**
> 2×1920×1080'de boru 108,4 fps (sentetik) ve 107,6 fps (gerçek kayıt) sürdürüyor —
> 60 fps hedefinin 1,8 katı, beş koşuda da. Aralık p99'u sentetik kaynakta 14,45 ms
> ortalama / 16,41 ms en kötü ve 25 ms'i aşan tek kare yok; **gerçek 132 Mbit/s'lik ekran
> kaydında ise 28,22 ms ortalama / 59,63 ms en kötü ve karelerin %1'i 25 ms'i aşıyor.**
> Yani kapı harfi harfine uygulandığında **G2 gerçek içerikte kalıyor**, ama kaldığı yer
> kapasite değil kuyruk: boruda 1,8 kat pay varken karelerin %99'u zamanında geliyor,
> %1'i geç kalıyor. Bu gecikmeleri yutacak kuyruk derinliği Ö12'de ölçülemedi.

> **G3 kaldı.** Kodlama koşarken ölçülen yavaşlamanın en iyi değeri %18,3, en kötüsü
> %181,9; kapı %10. Tek tek değerler gürültülü ama eşiğin altına inen tek bir
> yapılandırma yok. Sözleşmenin "düşerse" sütunu gereği **kodlama sırasında oynatma
> kapatılır.** Ters yön güvenli: boru, kodlamanın yanında da 60 fps'i tutuyor
> (fps kaybı %3,7-5,6).

### Ne değişmedi

Tur 1'in "60 fps @ 2×1080p boru yoluyla karşılanıyor" kararı ayakta: sürdürülen hız
107-116 fps. Ö7 (libmpv) açılmadı ve bu turda da gerekmedi. Değişen tek şey, kararın
artık p99 ayağıyla birlikte ve iki ayrı kaynakla yazılmış olması.

### Bu turda ölçülemeyenler

1. **Ö12 kuyruklu tüketici** — düzenek tutarsız sayı verdi, yukarıda anlatıldı.
2. **Ö4 (Avalonia sunum yolu)** — bu sözleşmenin `owns` listesi konsol bench projesiyle
   sınırlı; T37 sunum yolunu ayrıca ölçtü (`docs/olcumler/T37-sunum-olcumleri.md`).
3. **Ö7 (libmpv), Ö9 (paylaşımlı bellek)** — açılmadı, gerekçeleri tur 1'de.
4. **G2'nin p99 ayağı için üçüncü bir kaynak** — iki kaynak birbirinden farklı sonuç
   verdiği için üçüncüsü aydınlatıcı olurdu, koşulmadı.
5. **Ö1, Ö1b, K3, Ö6, Ö3, Ö5 tabloları parçalı okumayla yeniden koşulmadı.** Ö1/Ö1b/K3'ün
   çürüyen kısımlarına geçersizlik notu düşüldü; Ö6, Ö3 ve Ö5'in **karşılaştırmaları**
   (havuz vs. yeni tampon, yazılım vs. donanım, kodek sıralaması) her iki koldan da aynı
   okuma biçimini kullandığı için ayakta, ama **mutlak fps değerleri düşük.**

### Doğrulama — tur 2

| Kontrol | Sonuç |
|---|---|
| `dotnet build VidShrink.sln -c Release` | 0 uyarı, 0 hata |
| `dotnet test VidShrink.sln -c Release --no-build` | 369 başarılı, 0 başarısız, 8 atlanan |
| `git status -- src/ VidShrink.sln` | çıktı boş |
