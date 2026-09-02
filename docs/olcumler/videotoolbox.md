# VideoToolbox — Apple Silicon'da üç kol ölçümü

Tarih: 2026-09-02 · Ölçen: Serkan · Dal: `serkan/macos-olcum`

## Ortam

| Ne | Değer | Nereden |
|---|---|---|
| Makine | Apple M1, 8 çekirdek, 8 GB | `sysctl -n machdep.cpu.brand_string`, `hw.ncpu`, `hw.memsize` |
| İşletim sistemi | macOS 26.6.2 (25G83) | `sw_vers` |
| ffmpeg | 9.0.1, `homebrew-ffmpeg/ffmpeg` derlemesi | `ffmpeg -version` |
| libvmaf | 3.2.0 | `brew list --versions libvmaf`; `otool -L $(which ffmpeg)` → `libvmaf.3.dylib` |
| zimg | 3.0.6 | `brew list --versions zimg` |
| VMAF modeli | `vmaf_v0.6.1neg` | `QualityMeter.cs:283` |

`ffmpeg -version` çıktısının ilk iki satırı:

```
ffmpeg version 9.0.1 Copyright (c) 2000-2026 the FFmpeg developers
built with Apple clang version 21.0.0 (clang-2100.1.1.101)
```

İlgili derleme bayrakları (aynı çıktının `configuration:` satırından):
`--enable-libx265 --enable-videotoolbox --enable-libvmaf --enable-libzimg`.

**Bu ffmpeg bu tur için kuruldu.** Stok Homebrew ffmpeg'inde `zimg` yok, `zscale`
yok, ve `QualityMeter` `zscale`i koşulsuz şart koşuyor (`QualityMeter.cs:399`);
yani ölçer bu makinede hiç koşmuyordu. Kurulumun komutları ve öncesi/sonrası
karşılaştırması `macos-gercek-kosum.md`in "Düzeltme" bölümünde.

## Kaynak

Ortak ölçüm havuzu, `.calisma/kaynak/`. `shasum -a 256` çıktısı:

```
89CBDE4012ED6220243C973F1BA1D657C984695FD1A935742DFED9511BBD9492  parca-1.mkv
18F9B8E578285705F67BD4324687D2DA8A5E6DCC59A3A541EE060354ACD8A7BA  parca-2.mkv
B69C00C589D60CBF0B2A4199408B5B22E6C417913762CCBD03A711F7E60B104D  parca-3.mkv
```

Üçü de beklenen sha256 ile birebir. `ffprobe` okuması: 1920×1080, hevc,
`yuv420p10le`, `bt2020nc`/`smpte2084`/`bt2020`, 60 fps.

**Üç parçanın üçü de video-only koştu.** Havuzun bilinen kusuru — `parca-1`de ses
yok, `parca-2` ve `parca-3`te AAC var — hedef boyutun bir kısmını sese yedirip
parçalar arası kıyası haksız yapıyor. Bu yüzden her parçanın önce `-c:v copy` ile
video-only kopyası çıkarıldı; hem kodlama girdisi hem VMAF referansı o kopya.
Kodlama kollarının hepsinde ayrıca `-an` var. Süreler birebir eşit değil
(60,399 / 60,442 / 60,432 sn); kıyas parça **içinde** kollar arasında yapılıyor,
parçalar arasında değil.

Referans çıkarma komutu (`tools/videotoolbox/olc.sh:38`):

```
ffmpeg -hide_banner -loglevel error -nostdin -y -i .calisma/kaynak/<parca>.mkv \
  -an -sn -dn -c:v copy .calisma/vt/cikti/<parca>-video.mkv
```

## Düzenek

`tools/videotoolbox/olc.sh`. Üç kolun tamamı **tek geçiş ABR**, aynı `-b:v`:
VideoToolbox iki geçiş desteklemiyor, dolayısıyla üç kola eşit hız denetimi ancak
böyle kurulabiliyor. Renk etiketleri üç kolda da açıkça yazılıyor ki ölçüm tarafı
hangi uzaydan normalize edeceğini tahmin etmesin.

Kodlama komutu (kol argümanları `olc.sh:36-38`):

```
/usr/bin/time -p ffmpeg -hide_banner -loglevel error -nostdin -y \
  -i .calisma/vt/cikti/<parca>-video.mkv -an -sn -dn <kol> -b:v 5500k \
  .calisma/vt/cikti/<parca>-<kol>.mp4
```

| Kol | `<kol>` |
|---|---|
| `libx265` | `-c:v libx265 -preset slow -pix_fmt yuv420p10le -tag:v hvc1 -color_primaries bt2020 -color_trc smpte2084 -colorspace bt2020nc` |
| `hevc_videotoolbox` | `-c:v hevc_videotoolbox -profile:v main10 -pix_fmt p010le -tag:v hvc1 -color_primaries bt2020 -color_trc smpte2084 -colorspace bt2020nc` |
| `h264_videotoolbox` | `-c:v h264_videotoolbox -pix_fmt yuv420p -color_primaries bt2020 -color_trc smpte2084 -colorspace bt2020nc` |

Ölçüm komutu (`olc.sh:80`):

```
tools/VidShrink.Bench/bin/Release/net8.0/VidShrink.Bench measure \
  .calisma/vt/cikti/<parca>-video.mkv .calisma/vt/cikti/<parca>-<kol>.mp4
```

Bu, deponun kendi ölçeri: `QualityMeter.MeasureAsync`, `vmaf_v0.6.1neg` modeli,
kare kilidi `settb=AVTB,setpts=N` (`QualityMeter.cs:80`), açık renk normalizasyonu
`zscale` ile (`QualityMeter.cs:478`). p10 tanımı `Percentile(sorted, 10)`
(`QualityMeter.cs:292`, `:385`): sıralı listede `0,10 × (n−1)` sırasında doğrusal
ara değer.

Duvar saati yalnız **kodlamayı** kapsıyor; VMAF ölçümü ve yoklama dışarıda.
Ölçüm boyunca bu oturumdan başka iş koşturmadım, ama makine yalnız bana ait
değildi: sistem servisi `mediaanalysisd` oturum boyunca aralıklı olarak bir
çekirdekten fazlasını harcadı (`ps -o time= -p $(pgrep -x mediaanalysisd)`
5 sa 42 dk'lık açılışta 19 dk 13 sn CPU biriktirmiş). `.calisma/` altında
hiçbir dosyası açık değildi (`lsof -p <pid> | grep -c calisma` → `0`), yani
benim dosyalarımı değil Photos kitaplığını işliyordu. Etkisi ölçüldü ve
"Duvar saati ne kadar güvenilir" bölümünde sayıyla veriliyor.

Duvar saatinin yayılımını ölçmek için aynı kol beş kez tekrarlandı; kullanılan
komut yukarıdaki kalıbın `hevc_videotoolbox` kolunun birebir aynısı, tek farkı
çıktı adının tekrar numarası taşıması:

```
for i in 1 2 3 4 5; do
  /usr/bin/time -p ffmpeg -hide_banner -loglevel error -nostdin -y \
    -i .calisma/vt-ac/cikti/parca-1-video.mkv -an -sn -dn \
    -c:v hevc_videotoolbox -profile:v main10 -pix_fmt p010le -tag:v hvc1 \
    -color_primaries bt2020 -color_trc smpte2084 -colorspace bt2020nc \
    -b:v 5500k .calisma/vt-tekrar/p1-$i.mp4
done
```

## K1 — Üç kol, üç parça

Bir satırdaki her sayı **tek bir kodlamadan**: kalite, boyut ve duvar saati
aynı koşumun (`.calisma/vt/olcumler.tsv`) çıktısı. Üreten komut yukarıdaki
iki kalıp; `<parca>` ve `<kol>` sütunlardaki değerlerle doldurulur. Duvar
saatinin ne kadar güvenilir olduğu bir sonraki bölümde ayrıca ölçüldü.

| Parça | Kodek | `-b:v` | Boyut (bayt) | Boyut (MB) | Kodlama sn | VMAF ort | **VMAF p10** | Harmonik | Min | Çıktı `pix_fmt` |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---|
| `parca-1` | `libx265` | 5500k | 38 216 599 | 38,22 | 391,11 | 85,323 | **72,893** | 84,113 | 46,092 | `yuv420p10le` |
| `parca-1` | `hevc_videotoolbox` | 5500k | 42 264 115 | 42,26 | 19,38 | 77,595 | **55,528** | 71,942 | 5,603 | `yuv420p10le` |
| `parca-1` | `h264_videotoolbox` | 5500k | 42 061 920 | 42,06 | 17,90 | 71,853 | **43,740** | 63,194 | 10,133 | `yuvj420p` |
| `parca-2` | `libx265` | 5500k | 42 733 541 | 42,73 | 327,25 | 96,177 | **95,804** | 96,171 | 87,910 | `yuv420p10le` |
| `parca-2` | `hevc_videotoolbox` | 5500k | 42 321 840 | 42,32 | 19,67 | 90,074 | **89,421** | 90,039 | 62,713 | `yuv420p10le` |
| `parca-2` | `h264_videotoolbox` | 5500k | 42 089 412 | 42,09 | 18,47 | 83,553 | **77,562** | 82,600 | 22,190 | `yuvj420p` |
| `parca-3` | `libx265` | 5500k | 40 552 848 | 40,55 | 1056,40 | 77,857 | **71,990** | 77,537 | 60,043 | `yuv420p10le` |
| `parca-3` | `hevc_videotoolbox` | 5500k | 42 173 248 | 42,17 | 45,54 | 57,905 | **38,633** | 53,399 | 7,479 | `yuv420p10le` |
| `parca-3` | `h264_videotoolbox` | 5500k | 41 927 652 | 41,93 | 45,05 | 52,888 | **40,617** | 51,065 | 9,054 | `yuvj420p` |

3 parça × 3 kol = **9 koşum**; tabloda 9 satır var.

### Duvar saati ne kadar güvenilir

Koşumun ortasında şarj takıldığı için süre sütunu şüpheliydi. Dokuz kolun
kodlaması yalnız süre için AC'de baştan koşturuldu (`OLCUM=0`, aynı rig, aynı
argümanlar). Sonuç beklenenin tersi çıktı: **ikinci pas her yerde daha yavaş,**
üretilen dosyalar ise bayt bayt aynı.

| Parça | Kodek | 1. pas sn | 2. pas sn | 2/1 | 1. pas bayt | 2. pas bayt | Bayt aynı mı |
|---|---|---:|---:|---:|---:|---:|---|
| `parca-1` | `libx265` | 391,11 | 810,66 | 2,073 | 38 216 599 | 38 216 599 | **evet** |
| `parca-1` | `hevc_videotoolbox` | 19,38 | 27,13 | 1,400 | 42 264 115 | 42 264 115 | **evet** |
| `parca-1` | `h264_videotoolbox` | 17,90 | 28,47 | 1,591 | 42 061 920 | 42 061 920 | **evet** |
| `parca-2` | `libx265` | 327,25 | 494,29 | 1,510 | 42 733 541 | 42 733 541 | **evet** |
| `parca-2` | `hevc_videotoolbox` | 19,67 | 30,65 | 1,558 | 42 321 840 | 42 321 840 | **evet** |
| `parca-2` | `h264_videotoolbox` | 18,47 | 31,96 | 1,730 | 42 089 412 | 42 089 412 | **evet** |
| `parca-3` | `libx265` | 1056,40 | 1243,67 | 1,177 | 40 552 848 | 40 552 848 | **evet** |
| `parca-3` | `hevc_videotoolbox` | 45,54 | 32,95 | 0,724 | 42 173 248 | 42 173 248 | **evet** |
| `parca-3` | `h264_videotoolbox` | 45,05 | 33,04 | 0,733 | 41 927 652 | 41 927 652 | **evet** |

Dokuz kolun dokuzunda da iki pasın ürettiği dosya bayt bayt aynı boyda —
kodlayıcı belirlenimci, dolayısıyla kalite ve boyut sütunları iki pastan
hangisinin okunduğuna bakmaksızın geçerli. Sapan tek şey süre.

İki pas arasındaki tek yapısal fark şu: 1. pasta her kodlamanın ardından
~6 dakikalık VMAF ölçümü koşuyordu, 2. pasta (`OLCUM=0`) dokuz kodlama
aralıksız arka arkaya koştu. Hangisinin doğru olduğunu anlamak için makine
~7 dakika boş bırakılıp tek kol beş kez tekrarlandı:

| Tekrar | Süre sn | Bayt | Yük (1 dk) | O sırada `mediaanalysisd` CPU sn |
|---:|---:|---:|---:|---:|
| 1 | 19,50 | 42 264 115 | 5,55 | 4,8 |
| 2 | 19,40 | 42 264 115 | 4,91 | 0,4 |
| 3 | 19,38 | 42 264 115 | 4,81 | 0,3 |
| 4 | 19,39 | 42 264 115 | 4,66 | 0,4 |
| 5 | 19,37 | 42 264 115 | 4,43 | 0,3 |

Beş tekrar: en düşük **19,37**, ortanca **19,39**, en yüksek **19,50** sn — yayılım %0,67. Beşinin de baytı 42 264 115, yani 1. ve 2. pasla aynı.

Dinlenmiş makinenin sayısı (19,39 sn) **1. pasın sayısını**
(19,38 sn) birebir üretiyor, 2. pasınkini (27,13 sn) değil.
Yani 2. pas "temiz güç durumu" değil, sürekli yük altındaki makine. Güç
kaynağı da yük ortalaması da açıklayıcı değil: beş tekrarın en hızlıları
yük ortalaması 4,43–5,55
aralığında koştu ve `mediaanalysisd` koşum başına yalnız
0,4 CPU saniyesi harcadı.
Öncesindeki sürekli yükün süresi tek ayırt edici değişken; ısınmayı doğrudan
ölçmedim, bu yüzden mekanizmayı adlandırmıyorum.

**Rapora etkisi:** K1'in süre sütunu 1. pastan, yani dinlenmiş makinenin
yeniden üretilebilir sayısından. Duvar saatinin bu makinedeki belirsizliği
dinlenmiş durumda ±%1'in altında, ama sürekli yük altında 2 katına kadar
çıkabiliyor. Aşağıdaki hız sonucu bu belirsizliğe dayanıklı çünkü aradaki
fark kat kat büyük.

### Hız

`libx265` süresi bölü VideoToolbox kolunun süresi, her iki pas için:

| Parça | Pas | `libx265` sn | `hevc_videotoolbox` sn | Oran |
|---|---|---:|---:|---:|
| `parca-1` | 1. pas | 391,11 | 19,38 | **20,2×** |
| `parca-1` | 2. pas | 810,66 | 27,13 | **29,9×** |
| `parca-2` | 1. pas | 327,25 | 19,67 | **16,6×** |
| `parca-2` | 2. pas | 494,29 | 30,65 | **16,1×** |
| `parca-3` | 1. pas | 1056,40 | 45,54 | **23,2×** |
| `parca-3` | 2. pas | 1243,67 | 32,95 | **37,7×** |

6 oranın hepsi 16,1× ile 37,7× arasında.
Kaynakların üçü de ~60 saniye; VideoToolbox kolları bu 60 saniyeyi
17,9–45,5 sn'de, `libx265 -preset slow` ise
327,2–1243,7 sn'de kodladı.

### Teslim edilen boyut kol başına eşit değil

Üç kolda da aynı `-b:v` verildi; teslim edilen bayt kodlayıcının hız
denetimine kaldı. Parça içi yayılım:

| Parça | En küçük | En büyük | Yayılım |
|---|---|---|---:|
| `parca-1` | `libx265` 38,22 MB | `hevc_videotoolbox` 42,26 MB | %10,59 |
| `parca-2` | `h264_videotoolbox` 42,09 MB | `libx265` 42,73 MB | %1,53 |
| `parca-3` | `libx265` 40,55 MB | `hevc_videotoolbox` 42,17 MB | %4,00 |


## K2 — Donanım yolunun kalite bedeli

Aşağıdaki fark tablosunun her sayısı K1 tablosundaki iki hücrenin çıkarmasıdır;
çıkarma **gösterilen** (üç haneye yuvarlanmış) değerler üzerinden yapıldı, yani
tablodaki her rakam K1'de birebir bulunur.

| Parça | p10 `libx265` − `hevc_videotoolbox` | p10 `libx265` − `h264_videotoolbox` | ort `libx265` − `hevc_videotoolbox` | ort `libx265` − `h264_videotoolbox` |
|---|---:|---:|---:|---:|
| `parca-1` | 72,893 − 55,528 = **17,365** | 72,893 − 43,740 = **29,153** | 85,323 − 77,595 = **7,728** | 85,323 − 71,853 = **13,470** |
| `parca-2` | 95,804 − 89,421 = **6,383** | 95,804 − 77,562 = **18,242** | 96,177 − 90,074 = **6,103** | 96,177 − 83,553 = **12,624** |
| `parca-3` | 71,990 − 38,633 = **33,357** | 71,990 − 40,617 = **31,373** | 77,857 − 57,905 = **19,952** | 77,857 − 52,888 = **24,969** |

**Cümle:** Bu Mac'te, aynı `-b:v 5500k` ve video-only kodlamada,
`hevc_videotoolbox` `libx265`in p10'unun **17,365 / 6,383 / 33,357** puan
altında kalıyor (sırasıyla `parca-1` / `parca-2` / `parca-3`); ortalama VMAF'ta
aynı açık **7,728 / 6,103 / 19,952**. `h264_videotoolbox`ta p10 açığı
**29,153 / 18,242 / 31,373**.

Üç sayıyı tek sayıya indirmiyorum: aralık 6,383 ile 33,357 arası, yani beş katından
geniş. Üç parçanın ortalamasını almak bu yayılımı saklardı.

### Açık boyutla satın alınmış değil

Fark tablosu ancak `libx265`in daha çok bayt harcamadığı ölçüde anlamlı. K1'in
boyut sütunundan, `libx265` eksi `hevc_videotoolbox`:

| Parça | `libx265` bayt | `hevc_videotoolbox` bayt | `libx265`in farkı |
|---|---:|---:|---:|
| `parca-1` | 38 216 599 | 42 264 115 | **%9,58 daha küçük** |
| `parca-2` | 42 733 541 | 42 321 840 | %0,97 daha büyük |
| `parca-3` | 40 552 848 | 42 173 248 | **%3,84 daha küçük** |

Üç parçanın ikisinde `libx265` hem daha küçük dosya veriyor hem daha yüksek p10;
oralarda açık olduğundan **büyük**, gösterilenden değil. Yalnız `parca-2`de
`libx265`in %0,97 boyut üstünlüğü var ve oradaki 6,383'ün küçük bir kısmı buna
düşebilir — o parça zaten en dar açığın olduğu parça.

### `parca-3`te `h264_videotoolbox`, `hevc_videotoolbox`u p10'da geçiyor

40,617 > 38,633. Öteki iki parçada sıra tersine (55,528 > 43,740 ve
89,421 > 77,562). Üç parçada iki farklı sıra çıkması bu kolun sırasının
içeriğe bağlı olduğunu gösteriyor; tek bir "hevc h264'ten iyidir" cümlesi bu
ölçümle kurulamaz.

### Windows'la kıyas: istenen çift depoda yok

Paket "Windows'ta `libx265 p10 − hevc_nvenc p10` zaten ellerinde" diyor. Aradım,
**yok**. Aramanın kapsamı: `docs/` ve `docs/olcumler/` altındaki bütün `.md`
dosyalarında hem `^| libx265` hem `^| hevc_nvenc` satırı taşıyan dosyalar
(`bppf-tabani.md`, `handbrake-acigi.md`, `gpu-kodlama-bulgusu.md`), artı
`hevc_nvenc` geçen her satırın ±4 satır komşuluğunda `p10` araması. Bulunanlar:

| Kayıt | Ne veriyor | Neden istenen çift değil |
|---|---|---|
| `gpu-kodlama-bulgusu.md:19-21` | `libx265` slow 66,44 ↔ `hevc_nvenc` p7 62,01, eş boyut (1 719 KB / 1 730 KB) | **p10 yok**, yalnız ortalama VMAF |
| `handbrake-acigi.md:198-199` | `libx265` slow 2-pass p10 36,57 ↔ `av1_nvenc` p10 30,03, aynı kaynak ve aynı 1,50× tepe, 116,34 / 114,02 MiB | p10 var ama donanım kolu **`av1_nvenc`**, `hevc_nvenc` değil |
| `bppf-tabani.md:310-320` | `hevc_nvenc` p10 eğrisi, sekiz bit hızı, 1920×1080@60 | Karşısında `libx265` yok; o belgedeki yazılım kolu `libsvtav1` ve 1280×720@60'ta |

Yan yana konabilecek iki fark, ikisi de tam eşleşme değil:

| Ölçüm | Fark | Ne kıyaslıyor |
|---|---:|---|
| Mac, bu tur, p10 | **17,365 / 6,383 / 33,357** | `libx265` − `hevc_videotoolbox`, üç parça |
| Windows, `handbrake-acigi.md:198-199`, p10 | 36,57 − 30,03 = **6,54** | `libx265` − `av1_nvenc`, tek koşum |
| Mac, bu tur, ortalama | **7,728 / 6,103 / 19,952** | `libx265` − `hevc_videotoolbox`, üç parça |
| Windows, `gpu-kodlama-bulgusu.md:19-21`, ortalama | 66,44 − 62,01 = **4,43** | `libx265` − `hevc_nvenc`, tek koşum |

**Bu iki farkın kendisi de temiz değil.** `olcu-gecerliligi.md` her iki belgeyi de
"kirlenen geçmiş sayılar" listesine koyuyor: oradaki VMAF sütunları kare
kilidinden önceki ölçerden geçti ve kusur asimetrik — `handbrake-acigi.md` için
"kusur **yalnız bizim satırlarımızı** vuruyor" deniyor (`olcu-gecerliligi.md:336-337`).
Asimetrik bir kusur çıkarmayla sadeleşmez. Bu Mac ölçümü ise kare kilitli ölçerden
geçti (`QualityMeter.cs:80`). Yani yukarıdaki dört satırlık kıyas bir eğilim
göstergesidir, eşit temelli bir karşılaştırma değil.

`ffmpeg -version` ve `libvmaf` sürümleri raporun başında; Windows tarafının
sürümü `gpu-kodlama-bulgusu.md:3`te "ffmpeg 9.0-full" olarak yazılı, libvmaf
sürümü orada kayıtlı değil.


## K3 — `EncoderCapabilities` yoklama süresi

`tools/videotoolbox/yoklama.sh`, **12 tekrar**. Üretim kodu değiştirilmedi;
betikteki üç komut `EncoderCapabilities.cs`in kendi çalıştırdıklarının birebir
kopyası: `Load()` üç `RunCapture` (`-encoders`, `-filters`, `-version`),
`ProbeEncoder(codec)` deneme kodlaması (`EncoderCapabilities.cs:218-224`),
`RunProbe(codec, pixfmt)` HDR10 piksel biçimi yoklaması (`:261-267`).

Süre `/usr/bin/time -p` ile ölçüldü; çözünürlüğü 10 ms, dolayısıyla aşağıdaki
sayıların hepsi 10'un katı. Her satır n=12.

| Yoklama | Kodlayıcı | min ms | medyan ms | maks ms |
|---|---|---:|---:|---:|
| `Load()` `-encoders` | — | 30 | 30 | 30 |
| `Load()` `-filters` | — | 30 | 30 | 30 |
| `Load()` `-version` | — | 30 | 30 | 30 |
| deneme kodlaması | `libx264` | 30 | 30 | 30 |
| deneme kodlaması | `libx265` | 50 | 50 | 50 |
| deneme kodlaması | `hevc_videotoolbox` | 170 | 170 | 170 |
| deneme kodlaması | `h264_videotoolbox` | 150 | 160 | 160 |
| HDR10 `p010le` | `libx265` | 50 | 50 | 50 |
| HDR10 `p010le` | `hevc_videotoolbox` | 140 | 140 | 140 |
| HDR10 `p010le` | `h264_videotoolbox` | 120 | 120 | 130 |
| HDR10 `yuv420p10le` | `libx265` | 50 | 50 | 50 |
| HDR10 `yuv420p10le` | `hevc_videotoolbox` | 140 | 140 | 150 |
| HDR10 `yuv420p10le` | `h264_videotoolbox` | 120 | 120 | 130 |

On üç satır, hepsi `.calisma/vt/yoklama.tsv`den; toplam 13 × 12 = 156 ölçüm.

**En kötü tek yoklama 170 ms.** `HardwareVerdict.ProbeBudgetMs` 1500 ms
(`HardwareVerdict.cs:89`); bu makinede hiçbir yoklama o bütçenin yarısına bile
yaklaşmıyor.

Bir HDR dosyanın açılışta ödettiği toplam, `ProbeHdr10PixelFormat`in iki denemeli
mantığına göre (`EncoderCapabilities.cs:243-254`: `p010le` kabul edilirse ikinci
deneme hiç koşmuyor):

| Kodlayıcı | Kabul edilen biçim | Koşan yoklama | Toplam medyan |
|---|---|---|---:|
| `libx265` | `yuv420p10le` (ikinci deneme) | deneme + `p010le` + `yuv420p10le` | 50 + 50 + 50 = 150 ms |
| `hevc_videotoolbox` | `p010le` (ilk deneme) | deneme + `p010le` | 170 + 140 = 310 ms |
| `h264_videotoolbox` | yok — ikisi de reddedildi | deneme + `p010le` + `yuv420p10le` | 150 + 120 + 120 = 390 ms |

Windows'ta ölçülen 3 625–14 855 ms (`handbrake-acigi.md:246`) **yüklü makinede**,
dokuz ajan koşarken alınmış. Boş Windows makinesindeki karşılığı
`ui-yoklama-donmasi.md:40-41`de: HDR + soğuk önbellek 374–599 ms. Bu Mac'in aynı
durumdaki karşılığı, planın kodeği `libx265` olduğu için, **150 ms**.

### Yoklamanın kabul/ret kararı

Yoklama yalnız çıkış koduna bakmıyor: `EncoderCapabilities.PixelFormatAccepted`
çıkış kodu 0 **ve** stderr'de `Incompatible pixel format` / `auto-selecting format`
yoksa kabul ediyor (`EncoderCapabilities.cs:287-291`). Üç kodlayıcı × iki biçim,
aynı komut, stderr'i yakalayarak:

| Kodlayıcı | Biçim | Çıkış | Karar | stderr |
|---|---|---:|---|---|
| `libx265` | `p010le` | 0 | **ret** | `Incompatible pixel format 'p010le' for codec 'libx265', auto-selecting format 'yuv420p10le'` |
| `libx265` | `yuv420p10le` | 0 | **kabul** | yalnız swscaler/x265 uyarıları |
| `hevc_videotoolbox` | `p010le` | 0 | **kabul** | yalnız swscaler uyarıları |
| `hevc_videotoolbox` | `yuv420p10le` | 0 | ret | `Incompatible pixel format 'yuv420p10le' for codec 'hevc_videotoolbox', auto-selecting format 'p010le'` |
| `h264_videotoolbox` | `p010le` | 0 | **ret** | `Incompatible pixel format 'p010le' for codec 'h264_videotoolbox', auto-selecting format 'nv12'` |
| `h264_videotoolbox` | `yuv420p10le` | 0 | **ret** | `Incompatible pixel format 'yuv420p10le' for codec 'h264_videotoolbox', auto-selecting format 'nv12'` |

Altı satır, `.calisma/vt/hdr10-kabul.txt`ten. Okunan:

- `hevc_videotoolbox` HDR10 taşıyor, kabul ettiği biçim `p010le`.
- **`h264_videotoolbox` HDR10 taşımıyor.** İki biçimi de reddediyor, yani
  `Hdr10PixelFormat("h264_videotoolbox")` `null` döner. Altı çıkış kodunun altısı
  da 0; yalnız çıkış koduna bakan bir yoklama üçüne de "evet" derdi.

## K4 — Hüküm: Windows eşikleri Mac'te **geçerli değil**

Üç seçenekten biri isteniyordu; cevap "geçerli değil". Gerekçe iki katmanlı:
birincisi kodun kendisinden okunuyor, ikincisi yukarıdaki tablodan.

### 1. Eşikler Mac'te uygulanmıyor — ölçülmemiş değil, erişilemez

Windows'ta donanım yolunu kuran sabitler ve bulundukları yer:

| Eşik | Değer | Yer |
|---|---:|---|
| `CodecModel.HardwareQualityCeiling` | 96,0 | `CodecModel.cs:16` |
| `CodecModel.HardwareBitrateYield` | 0,877 | `CodecModel.cs:18` |
| `CodecModel.HardwareFloorFactor` | 1,52 | `CodecModel.cs:19` |
| `PlanCalculator.HardwareDeliveryReserveK` | 11 | `PlanCalculator.cs:116` |
| `FfmpegArguments.HardwarePeakCeiling` | 1,10 | `FfmpegArguments.cs:150` |
| `CodecModel.RelativeBitrateNeed` | `hevc_nvenc` 0,88 · `libx265` 0,68 | `CodecModel.cs:95-108` |

Bunların **hepsi** `CodecModel.IsHardware(codec)` kapısının arkasında:
`FloorBppf` (`:65`), `DeliveryReserveK` (`PlanCalculator.cs:125`),
`PlanCalculator.HardwareBitrateYield` (`:894-896`), `QualityLimit` (`:117-124`).
`IsHardware` ise `Vendor(codec) != Software` demek (`CodecModel.cs:134`) ve
`Vendor` yalnız üç dizgiye bakıyor (`CodecModel.cs:125-132`):

```csharp
if (c.Contains("nvenc")) return EncoderVendor.Nvenc;
if (c.Contains("qsv"))   return EncoderVendor.Qsv;
if (c.Contains("amf"))   return EncoderVendor.Amf;
return EncoderVendor.Software;
```

`hevc_videotoolbox` üçünü de içermiyor, dolayısıyla **yazılım** sayılıyor. Yani
bu eşiklerin Mac'te "geçerli olup olmadığı" sorusunun bugünkü cevabı: hiçbiri
bir VideoToolbox koluna uygulanmıyor.

Zaten kodlayıcı seçilemiyor da: `PlanCalculator.FastHardwareOrder`
(`PlanCalculator.cs:127-130`) yedi kodlayıcı sayıyor, hiçbiri VideoToolbox değil;
`PlanParser.AllowedCodecs` (`PlanParser.cs:13`) on iki kodlayıcı sayıyor, hiçbiri
VideoToolbox değil. `grep -rn "videotoolbox" src/` **0 satır** dönüyor. Bu
makinede ölçülen davranış da bunu doğruluyor: `codec=libx265`,
`verdict=NoHardwareEncoder` (`macos-gercek-kosum.md`, `VIDSHRINK_LIVE_PROBE`
bölümü) — oysa `ffmpeg -encoders` `hevc_videotoolbox` ve `h264_videotoolbox`ı
listeliyor.

### 2. Biri kodlayıcıyı bağlasa, yazılım varsayımları yanlış yönde yanılırdı

Yukarıdaki tablo, eşiklerin arkasındaki varsayımları iki noktada yalanlıyor.

**Kalite tavanı.** `SoftwareQualityCeiling` 99,0, `HardwareQualityCeiling` 96,0
(`CodecModel.cs:15-16`). VideoToolbox bugün yazılım sayıldığı için plan ona 99,0
tavanı verirdi. Ölçülen: `hevc_videotoolbox`ın üç parçadaki **en yüksek**
VMAF ortalaması 90,074 (`parca-2`), en yükseği bu. Üç parçanın hiçbirinde 96,0'a
bile ulaşmıyor. Yani yazılım tavanı bu kodlayıcı için gerçeğin üstünde.

**Bit ihtiyacı.** `RelativeBitrateNeed` `libx265`e 0,68, `hevc_nvenc`e 0,88
veriyor; VideoToolbox tabloda yok, dolayısıyla `_ => 1.0` dalına düşüyor
(`CodecModel.cs:108`). Yönü doğru — VideoToolbox aynı bit hızında `libx265`ten
geride — ama 1,0 ölçülmüş bir sayı değil, tablonun dışında kalmanın sonucu.

**Ölçülemeyen eşikler.** `HardwareFloorFactor` 1,52, `HardwareBitrateYield`
0,877, `HardwareDeliveryReserveK` 11 ve `HardwarePeakCeiling` 1,10 — dördü de
NVENC üzerinde ölçüldü ve `bppf-tabani.md` bunu kendi yazıyor: §5.2 ölçümün
"doğrudan ffmpeg ile" NVENC kolunda kurulduğunu, §5.3 ise `IsHardware`ın aynı
çarpanı ölçülmemiş QSV ve AMF'ye de uyguladığını söylüyor. Bu turda
VideoToolbox için de ölçülmedi: aşağıya bakınız.

### Bu hükmün kapsamadığı

- **Yerine ne konmalı, söylenemez.** Parça başına tek bit hızı (5500k) ölçüldü;
  taban/diz eğrisi çıkarmak için `bppf-tabani.md`nin yaptığı gibi kol başına
  sekiz nokta gerekir. Bu ölçümden `HardwareFloorFactor` benzeri bir sayı
  türetilemez.
- **Tek makine, tek kuşak.** Apple M1. M2/M3/M4'ün medya motoru farklı; buradaki
  hiçbir sayı onlara taşınmaz.
- **Üç adet 60 saniyelik parça.** Aralarındaki yayılım zaten geniş (p10 açığı
  6,384 ile 33,357 arası); üç nokta bir dağılım değil.
- **`h264_videotoolbox` HDR10 taşımıyor** (K3'teki kabul tablosu). Bu kolun
  sayıları 8 bitlik bir kodlayıcının 10 bitlik HDR kaynağa verdiği cevaptır;
  kodlayıcının hatası değil, kaynakla eşleşmemesidir.

