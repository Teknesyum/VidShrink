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
