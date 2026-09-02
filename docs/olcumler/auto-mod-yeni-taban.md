# Auto mod — bugünkü motorla yeniden ölçüm

T120. Ölçüm tarihi 2026-09-02. Dal `T120-yeni-taban`, **taban commit `2d5f710`**
(`origin/main`). Bu belgedeki her sayı o ağaçtaki motorla, bu makinede
koşturulmuş bir ölçümden gelir. Ölçülmemiş olan yerde açıkça **ölçülmedi** yazar.

`docs/olcumler/auto-mod.md` (T102 + T111) aynı kaynakta aynı soruları soruyor ama
**taban `3688336`** ile. O tabandan beri `main` motoru değiştirdi. Bu belge o
ölçümü bugünkü motorla tekrarlar ve iki tabanı yan yana koyar.

## Neyin değiştiği ölçüldü, varsayılmadı

Bugünkü auto planı, ölçümden önce plan kipinde alındı:

    cd tools/auto-mod-olcumu/harness
    T102_PLAN_ONLY=1 dotnet run -c Release -- \
      ../../../.calisma/t120/gui/parca-2.mkv 16 \
      ../../../.calisma/t120/ciktilar/auto.mp4 auto

**Derlenmiş ikili çağrılmadı, kaynak her koşumda yeniden derlendi — ve bu
ayrım bu turda gerçekten fark yarattı.** `bin/Release/net8.0/` altındaki hazır
ikili T111 tabanından kalmaydı; çağrıldığında `-g 120` basıyor, yani T98 öncesi
motoru anlatıyordu. Aşağıdaki tablonun sağ sütunu o ikiliden değil, yeniden
derlenmiş koşumdan geliyor.

| | T111 tabanı (`3688336`) | bugünkü `main` (`2d5f710`) |
|---|---|---|
| kodek / preset | `libsvtav1` / 6 | `libsvtav1` / 6 |
| çözünürlük, kare hızı | 1920x1080@60 | 1920x1080@60 |
| istenen video bit hızı | `-b:v 2026k` | `-b:v 2026k` |
| piksel biçimi | `p010le` | `p010le` |
| ses | `aac 128k` | `aac 128k` |
| psy | `tune=0:enable-variance-boost=1:variance-boost-strength=2` | aynı |
| **anahtar kare** | **`-g 120`** | **`-g 600 -svtav1-params keyint=600:scd=1`** |

**Plan tarafında değişen tek şey anahtar kare argümanı.** Bu, ayrıştırmayı
kolaylaştıran bir tesadüf değil, ölçülen bir sonuç: T98'in dokunduğu yer bu
kaynakta plana başka hiçbir yerden girmiyor.

**Üretim yolu sahne haritasını kullanmıyor — ölçüldü.** T98 tavanı sahne
haritasından çıkarıp 5-10 s'ye kelepçeliyor (`FfmpegArguments.KeyframeCeilingSeconds`).
Ama kodlayan yol (`EncodeRunner.EncodeArguments` → `FfmpegArguments.Build`)
`scenes` parametresini **hiç vermiyor**; `FfmpegArguments.Build`'i harita ile
çağıran tek yer `src/VidShrink.App/MainWindow.axaml.cs:1807`. Harita yokken tavan
varsayılan 10 s'de kalıyor, 60 fps'te **600 kare**. Yani bu kaynakta ölçülen
`-g 600` haritadan gelmiş bir sayı değil, haritasız varsayılan. Bu bir kusur
tespitidir; düzeltmek bu sözleşmenin işi değil.

## Ölçüm düzeneği

| | |
|---|---|
| Kaynak | `.calisma/kaynak/parca-2.mkv` — 1920x1080@60, HDR10 (PQ / bt2020nc), 60,442 s, 3624 video paketi, `aac` 48000 Hz stereo, 115 933 238 bayt |
| Kaynağın yeri | ölçüm için `.calisma/t120/gui/parca-2.mkv`'ye birebir kopyalandı |
| Hedef boyut | 16 MB — uygulamanın kendi varsayılanı |
| Kalite ölçüsü | VMAF-NEG (`vmaf_v0.6.1neg`), **kare kilidi takılı** |
| Kilit | `settb=AVTB,setpts=N` — T110/T111'inkiyle **birebir aynı** |
| Kodlayıcı | SVT-AV1 `v4.2.0-68-gc1e79b04f`, ffmpeg 9.0-full (gyan.dev), HandBrakeCLI x265_10bit |

Ölçüm grafiği (`.calisma/t120/olc.sh`):

    ffmpeg -threads 4 -i ciktilar/<ad>.mp4 -i gui/parca-2.mkv -lavfi \
      "[0:v]scale=w=1920:h=1080:flags=lanczos,settb=AVTB,setpts=N[t];\
       [1:v]settb=AVTB,setpts=N[r];\
       [t][r]libvmaf=model=version=vmaf_v0.6.1neg:n_threads=4:log_fmt=json:log_path=vmaf/<ad>-kilitli.json" \
      -f null -

**Bu belgede kilitsiz sayı yok.** Sözleşme kilitsiz ölçümü geçersiz sayıyor;
tablolarda yalnız kilitli sayılar var.

**Ölçüm zinciri T111'in arşivine karşı doğrulandı.** İşe başlamadan önce T111'in
on sekiz arşiv dosyası (`vmaf-t111/*.json.gz`) bu makinede yeniden özetlendi ve
T111'in bastığı **on sekiz satırın hepsi birebir** çıktı. Yani aşağıdaki
farklar özetleyicinin değil, motorun.

    git show T111-auto-mod:tools/auto-mod-olcumu/vmaf-t111/<ad>.json.gz > t111-arsiv/<ad>.json.gz
    python .calisma/t120/oz.py t111-arsiv/*.json.gz

**İş parçacığı.** Kodlamalarda `-threads 4` ve `svtav1-params lp=4`; ölçümlerde
`-threads 4`, `libvmaf n_threads=4`. Tek istisna **`auto`** satırı: o koşum
uygulamanın kendi yolundan (`EncodeRunner`) geçtiği için iş parçacığı
sabitlenemedi — üretim ne yapıyorsa o ölçüldü. Farkın büyüklüğü aşağıda
ölçüldü, varsayılmadı.

**Süre ölçülmedi.** Makine paylaşımlıydı, dokuz ajan koşuyordu; bu belgede
hiçbir süre sayısı yok. Kalite ve boyut sayıları iş parçacığı sabitken
yükten etkilenmez.

---

## K1 — bugünkü `main`de üç satır, kilitli

Üç koşum, aynı kaynak, aynı ölçüm grafiği, hepsinde kare kilidi takılı. Boyutlar
`auto`nun teslim ettiği dosyaya eşitlendi; eşitleme yöntemi ve denemeleri K3'te.

| koşum | ne | teslim (bayt) | Δ `auto` | ortalama | p10 | harmonik | en düşük kare | `<1` kare |
|---|---|---|---|---|---|---|---|---|
| `auto` | uygulamanın kendi kararı, hiçbir ayara dokunulmadı | 16 289 648 | — | **96,025** | **95,526** | **96,022** | 94,806 | **0** |
| `uzman-biz3` | aynı motor, elle: `libsvtav1` preset **4**, **`-g 300`** | 16 222 129 | −%0,414 | **96,099** | **95,605** | **96,097** | 94,843 | **0** |
| `uzman-hb2` | HandBrakeCLI `x265_10bit`, preset `slow` | 16 284 727 | −%0,030 | **95,759** | **95,396** | **95,757** | 94,140 | **0** |

Taban commit **`2d5f710`**. Üç satırın da `<1` kare sayısı sıfır.

### Üreten komutlar

`auto` uygulamanın kendi yolundan geçti (`ComplexityProbe` → `CalibrationProbe`
→ `PlanCalculator` → `EncodeRunner`), hedef 16 MB:

    cd tools/auto-mod-olcumu/harness
    dotnet run -c Release -- \
      ../../../.calisma/t120/gui/parca-2.mkv 16 \
      ../../../.calisma/t120/ciktilar/auto.mp4 auto

Bu koşumun bastığı plan ve ikinci geçiş komutu:

    PLAN mode=2pass codec=libsvtav1 preset=6 1920x1080@60 vbit=2026k abit=128k
         pix=p010le hdrfilt=True tahminMB=15,60
    ffmpeg -hide_banner -y -hwaccel auto -i gui/parca-2.mkv -c:v libsvtav1 \
      -preset 6 -b:v 2026k -pass 2 -passlogfile pl -g 600 \
      -svtav1-params keyint=600:scd=1 -pix_fmt p010le \
      -color_primaries bt2020 -color_trc smpte2084 -colorspace bt2020nc \
      -c:a aac -b:a 128k -movflags +faststart ciktilar/auto.mp4

`uzman-biz3` — iki geçiş; birincisi aynı komutun `-pass 1 -an -f null NUL` hâli:

    ffmpeg -hide_banner -loglevel error -y -nostdin -threads 4 -i gui/parca-2.mkv \
      -c:v libsvtav1 -preset 4 -b:v 2712k -pass 2 -passlogfile log/uzman-biz3 \
      -g 300 -pix_fmt p010le \
      -svtav1-params tune=0:enable-variance-boost=1:variance-boost-strength=2:lp=4 \
      -color_primaries bt2020 -color_trc smpte2084 -colorspace bt2020nc \
      -c:a aac -b:a 128k -movflags +faststart ciktilar/uzman-biz3.mp4

`uzman-hb2`:

    HandBrakeCLI -i gui/parca-2.mkv -o ciktilar/uzman-hb2.mp4 \
      -e x265_10bit --encoder-preset slow --encopts pools=4 \
      -b 1969 --multi-pass --turbo \
      -E ca_aac -B 128 --mixdown stereo \
      -w 1920 -l 1080 --crop-mode none -r 60 --cfr -f av_mp4 -O

Üçünün kalite sayısı da düzenek bölümündeki kilitli grafikle üretildi ve şununla
özetlendi:

    python .calisma/t120/oz.py vmaf/<ad>-kilitli.json

Boyutlar `stat -c %s ciktilar/<ad>.mp4`, kodlayıcı süreci sıfırla döndükten
sonra okundu: her koşum önce `.<ad>.yaziliyor.mp4` adına yazıyor ve dosya ancak
çıkış kodu denetlendikten sonra `mv` ile yerine konuyor.

### `auto` iki kez koşturuldu — kendi saçılımı ölçüldü

Aynı komut, aynı hedef, yalnız çıktı adı farklı:

    dotnet run -c Release -- ../../../.calisma/t120/gui/parca-2.mkv 16 \
      ../../../.calisma/t120/ciktilar/auto2.mp4 auto

| koşum | teslim (bayt) | `EncodeRunner` denemesi | ortalama | p10 | harmonik | en düşük kare |
|---|---|---|---|---|---|---|
| `auto` | 16 289 648 | 2 | 96,025 | 95,526 | 96,022 | 94,806 |
| `auto2` | 16 134 366 | 3 | 96,080 | 95,647 | 96,078 | 94,257 |
| **saçılım** | **%0,95** | | **0,055** | **0,121** | **0,056** | **0,549** |

**`auto` tekrarlanabilir değil.** Sonda kodlamaları (`ComplexityProbe`,
`CalibrationProbe`) gerçek kısa kodlamalar; makine yükü altında hem plan hem
`EncodeRunner`'ın düzeltme deneme sayısı değişiyor. İki koşum arasında teslim
edilen boyut %0,95, kilitli ortalama 0,055, p10 0,121 puan oynadı.

Bu saçılım K2'nin cetvelidir: **açıklar bu büyüklüğe göre okunuyor.** K1
tablosunun `auto` satırı birinci koşumdur; K2'de her açık iki koşuma göre de
ayrı ayrı verildi.

### İstenen bit hızı teslim edilen bit hızı değildir

`libsvtav1` VBR'de istenenin belirgin altına iniyor: `uzman-biz3` 2712 kbps
istedi, teslim edilen videoda `ffprobe` **2 029 038 bps** okuyor. Bu yüzden bu
belgede eşleme **teslim edilen bayt** üzerinden yapıldı, istenen bit hızı
üzerinden değil.

`auto` da tek geçişte hedefi tutturmuyor; `EncodeRunner` düzeltiyor ve koşum
`deneme=2` ile bitiyor. Elle koşumlar tek denemelik, bu yüzden onların istenen
bit hızı `auto`nunkinden yüksek. Teslim edilen boyutlar eşit olduğu için
karşılaştırma bundan etkilenmiyor.

**`-maxrate`/`-bufsize` bu yolda yok ve olamaz.** `FfmpegArguments.Build` VBV
sınırlarını `SupportsRateLimits` kapısının arkasına koyuyor, o kapı da
`libsvtav1` için `false` döndürüyor. Kapının doğru olduğu ölçüldü: aynı komut
elle `-maxrate` eklenerek koşturulunca kodlayıcı açılmıyor —
`Svt[error]: Max Bitrate only supported with CRF mode`, ffmpeg `-22`.

### İş parçacığı sabitlemesinin kaliteye ve boyuta etkisi ölçüldü

Aynı koşum bir kez `-threads 4` + `lp=4` ile, bir kez hiç sabitlemeden:

| koşum | teslim (bayt) | ortalama | p10 | harmonik |
|---|---|---|---|---|
| `g600-scd1` (sabit) | 12 275 437 | 95,945 | 95,382 | 95,942 |
| `g600-scd1-serbest` | 12 283 897 | 95,946 | 95,383 | 95,943 |

Fark boyutta %0,069, kalitede 0,001 puan. **Kalite ve boyut sayılarına "makine
paylaşımlıydı" damgası basılmadı**; süre sayısı bu belgede zaten yok.

---

## K2 — T111'in sayılarıyla yan yana

T111 tabanı **`3688336`**, bugünkü taban **`2d5f710`**. Her iki tabanda da açık,
kendi tabanının boyut eşli rakibine karşı ölçüldü. İşaret **rakip eksi `auto`**;
artı = rakip önde.

| açık | T111 (`3688336`, kilitli) | bugün (`2d5f710`, kilitli) | fark |
|---|---|---|---|
| uzman açığı, Δ ortalama | +0,437 | **+0,074** | −0,363 |
| uzman açığı, Δ p10 | +0,673 | **+0,079** | −0,594 |
| uzman açığı, Δ harmonik | +0,439 | **+0,075** | −0,364 |
| HandBrake açığı, Δ ortalama | +0,097 | **−0,266** | −0,363 |
| HandBrake açığı, Δ p10 | +0,477 | **−0,130** | −0,607 |
| HandBrake açığı, Δ harmonik | +0,099 | **−0,265** | −0,364 |

T111 sütunu `docs/olcumler/auto-mod.md`'nin K2 ve K3 tablolarındaki kilitli
satırlardan alındı (`uzman-biz3` − `auto`, `uzman-hb2` − `auto`). Bugünkü sütun
K1 tablosundan çıkar.

### Sonuç: açık **kapandı**

Altı ölçünün altısında da açık `auto` lehine kapandı. Ama iki rakip için iki ayrı
cümle gerekiyor, çünkü biri işareti çevirdi, diğeri ölçümün kendi
tekrarlanabilirliğinin içine düştü. Aşağıdaki tablo her açığı `auto`nun **iki
koşumuna** göre de veriyor; cetvel K1'de ölçülen saçılım (ortalamada 0,055,
p10'da 0,121).

| açık | `auto` (1. koşum) | `auto2` (2. koşum) |
|---|---|---|
| uzman, Δ ortalama | +0,074 | +0,019 |
| uzman, Δ p10 | +0,079 | **−0,042** |
| uzman, Δ harmonik | +0,075 | +0,019 |
| HandBrake, Δ ortalama | −0,266 | −0,321 |
| HandBrake, Δ p10 | −0,130 | −0,251 |
| HandBrake, Δ harmonik | −0,265 | −0,321 |

**HandBrake: geçtik.** İşaret gerçekten döndü. T111'de HandBrake üç ölçüde de
öndeydi (+0,097 / +0,477 / +0,099); bugün `auto`nun **her iki koşumuna göre de**
altı ölçünün altısında geride. Büyüklük saçılımın iki ile beş katı, yani
tekrarlanabilirliğin içine düşmüyor. Boyut avantajından da gelmiyor:
`uzman-hb2` birinci koşumdan **%0,030 küçük**, ikinciden %0,93 büyük; erişim
eğimiyle (K3) ikinci durumdaki katkı 0,006 puan, −0,321'i çevirmiyor. T111'in
kendi en dar eşleşmesi `uzman-hb3` (−%0,08, kilitli ortalama açığı +0,083)
alınsa bile bugünkü işaret onun da tersi.

**Uzman ayarı: açık kapandı ama işaret dönmedi — ve artık ölçülemiyor.**
+0,437'den +0,074'e indi, altıda bire. Ne var ki +0,074, `auto`nun kendi
koşumdan koşuma saçılımı olan 0,055'in hemen üstünde; ikinci `auto` koşumuna
göre açık +0,019'a düşüyor ve **p10'da işaret −0,042 ile değişiyor.** Yani
uzmanın hangi tarafta olduğu artık hangi `auto` koşumunu aldığınıza bağlı.

Bu yüzden **"yakaladık" da "geçtik" de yazılmıyor**: işaretin döndüğü
gösterilemedi. Yazılabilen şu: **uzman açığı ölçümün ayırt etme gücünün altına
indi.** Bunu bir "eşitlik" iddiasına çevirmek için `auto`nun saçılımını
daraltmak ya da her iki tarafı çok kez tekrarlayıp ortalamak gerekir; **bu
turda yapılmadı.**

<!--K2NOKTA-->

### Karşılaştırılabilirliğin sınırı

- T111'in sayıları o tabanın `auto`suna göre, bugünküler bugünkü `auto`ya göre.
  İki `auto` aynı dosya değil; **satırlar değil, açıklar karşılaştırılıyor.**
- Kaynak, kilit grafiği ve VMAF modeli iki tabanda birebir aynı; ölçüm zinciri
  T111'in on sekiz arşiv özetine karşı doğrulandı (düzenek bölümü).
- Tek kaynakta ölçüldü. Başka içerikte aynı işaretin çıkacağı **ölçülmedi**.

---

## K3 — boyut eşliği

**Yöntem:** istenen bit hızı üzerinde ikiye bölme; ölçüt teslim edilen bayt,
hedef `auto`nun 16 289 648 baytı. Her deneme tam bir iki geçişli kodlama;
`uzman-biz*` koşumlarında preset 4 ve `-g 300` sabit, yalnız `-b:v` değişiyor.

| koşum | istenen | teslim (bayt) | Δ `auto` |
|---|---|---|---|
| `uzman-biz1` | 2719 kbps | 16 368 743 | +%0,486 |
| `uzman-biz2` | 2705 kbps | 16 207 125 | −%0,507 |
| `uzman-biz3` | 2712 kbps | 16 222 129 | **−%0,414** |
| `uzman-hb1` | 1980 kbps | 16 372 823 | +%0,511 |
| `uzman-hb2` | 1969 kbps | 16 284 727 | **−%0,030** |

    ./uret.sh uzman-biz1 4 300 "" 2719      ./hb.sh uzman-hb1 1980
    ./uret.sh uzman-biz2 4 300 "" 2705      ./hb.sh uzman-hb2 1969
    ./uret.sh uzman-biz3 4 300 "" 2712

**Band T111'inkinden dar.** T111 AV1 tarafında beş denemede ±%0,48'e, HandBrake
tarafında bir denemede −%0,08'e inmişti. Bu turda AV1 tarafı üç denemede
**−%0,414**, HandBrake tarafı iki denemede **−%0,030**. İki taraf da T111'in
bandının içinde.

**AV1 tarafında teslim edilen boyut istenen bit hızına çok duyarlı.** 2719 →
2705 kbps, yani %0,51'lik bir istenen bit hızı değişimi teslim edilen boyutu
%0,99 oynattı. Üçüncü deneme bu yüzden iki denemenin arasına nişan aldı ve
banda girdi; dördüncü denemeye gerek görülmedi.

### Kalan sapmanın açığa katkısı

Skorun boyuta eğimi bu turda **yeniden ölçüldü**, T111'inki devralınmadı.
`uzman-biz1` ile `uzman-biz2` aynı ayarlarla üretilmiş, yalnız bit hızı farklı
iki koşum; aralarında **%0,997** boyut farkı var:

| | `uzman-biz1` (16 368 743) | `uzman-biz2` (16 207 125) | fark | eğim |
|---|---|---|---|---|
| ortalama | 96,102 | 96,096 | +0,006 | **0,006 puan / %1** |
| p10 | 95,605 | 95,601 | +0,004 | **0,004 puan / %1** |
| harmonik | 96,100 | 96,093 | +0,007 | **0,007 puan / %1** |

T111 aynı eğimi 0,003 puan / %1 ölçmüştü. İki ölçüm aynı mertebede; bugünkü iki
katı, ikisi de 0,00x.

Kalan sapmanın katkısı bu eğimle:

| koşum | Δ boyut | katkı (ortalama) | ölçülen açık | katkının payı |
|---|---|---|---|---|
| `uzman-biz3` | −%0,414 | 0,0025 puan | +0,074 | %3,4 |
| `uzman-hb2` | −%0,030 | 0,0002 puan | −0,266 | %0,1 |

**Kalan sapma sonucu değiştirmiyor.** İki koşumda da katkı ölçülen açığın yüzde
birkaçı; işareti çevirecek büyüklükte değil. Üstelik iki rakip de `auto`dan
**küçük** dosya teslim etti, yani sapmanın düzeltilmesi `auto` lehine değil
aleyhine bir düzeltme olurdu ve HandBrake açığı daha da açılırdı.

Eğim yalnız AV1 tarafında, yalnız %1'lik bir aralıkta ölçüldü. x265 tarafında
ayrı bir eğim **ölçülmedi**: `uzman-hb1` ile `uzman-hb2` arasındaki %0,54 boyut
farkında kilitli ortalama 95,759 → 95,759 hiç oynamadı, yani orada da eğim
0,00x mertebesinde, ama iki noktayla sayı verilmedi.

---

## K4 — T98'in GOP'u ayrıştırıldı

Beş koşum, **aynı kaynak, aynı istenen bit hızı (`-b:v 2026k`), aynı preset (6),
aynı psy, aynı ses**; değişen tek şey anahtar kare argümanı. Hepsi
`-threads 4` + `lp=4` ile sabitlendi, hepsi kare kilidiyle ölçüldü. Üreten komut:

    .calisma/t120/uret.sh <ad> 6 <g> "<keyint/scd>" 2026
    .calisma/t120/olc.sh   <ad>
    python .calisma/t120/tablo.py <ad>

| koşum | anahtar kare argümanı | bayt | ortalama | p10 | harmonik | en düşük kare | `<1` kare |
|---|---|---|---|---|---|---|---|
| `g120-taban` | `-g 120` (T111 tabanı) | 14 401 960 | 95,481 | 94,496 | 95,475 | 91,786 | 0 |
| `g120-scd1` | `-g 120 keyint=120:scd=1` | 14 247 160 | 95,469 | 94,498 | 95,463 | 91,785 | 0 |
| `g300-taban` | `-g 300` | 11 912 391 | 95,828 | 95,136 | 95,824 | 94,216 | 0 |
| `g600-scd0` | `-g 600 keyint=600:scd=0` | 12 218 377 | 95,945 | 95,385 | 95,942 | 94,583 | 0 |
| `g600-scd1` | `-g 600 keyint=600:scd=1` (**bugünkü**) | 12 275 437 | 95,945 | 95,382 | 95,942 | 94,583 | 0 |

Üretilen dosyalardaki anahtar kareler doğrudan sayıldı
(`ffprobe -skip_frame nokey -show_entries frame=pts_time`):

| koşum | anahtar kare | en kısa aralık | en uzun aralık |
|---|---|---|---|
| `g120-taban` | 31 | 2,00 s | 2,00 s |
| `g120-scd1` | 31 | 2,00 s | 2,00 s |
| `g300-taban` | 13 | 5,00 s | 5,00 s |
| `g600-scd0` | 7 | 10,00 s | 10,00 s |
| `g600-scd1` | 7 | 10,00 s | 10,00 s |
| `auto` (üretim yolu) | 7 | 10,00 s | 10,00 s |

### Ölçüm zinciri çapraz doğrulandı

`g300-taban` bugünkü tabanda üretildi ama argümanları T111'in `e2-gop300`
koşumuyla birebir aynı. İki taban, iki ayrı kodlama, aynı ölçer:

| | ortalama | p10 | harmonik | en düşük kare |
|---|---|---|---|---|
| T111 arşivi `e2-gop300-kilitli` (taban `3688336`) | 95,828 | 95,138 | 95,825 | 94,216 |
| T120 `g300-taban` (taban `2d5f710`) | 95,828 | 95,136 | 95,824 | 94,216 |
| fark | 0,000 | **−0,002** | −0,001 | 0,000 |

Aynı argümanlar iki tabanda aynı sayıyı veriyor. Yani aşağıdaki farklar
tabandan değil, **argümandan** geliyor — `FfmpegArguments`'in T98'de değişen
kısmı bu kaynakta yalnız anahtar kare argümanına dokunuyor, ölçüm de bunu
doğruluyor.

### Aralığın payı

| değişim | Δ bayt | Δ ortalama | Δ p10 | Δ en düşük kare |
|---|---|---|---|---|
| `-g 120` → `-g 300` | **−%17,3** | **+0,347** | **+0,640** | +2,430 |
| `-g 300` → `-g 600` | +%2,6 | +0,117 | +0,246 | +0,367 |
| `-g 120` → `-g 600` (T111 tabanı → bugün) | **−%14,8** | **+0,464** | **+0,886** | +2,797 |

**Fark 0,0x mertebesinde değil.** T98'in aralığı bu kaynakta hem dosyayı
küçültüyor hem puanı yükseltiyor: iki eksende birden kazanç. Büyük kalem
120 → 300 adımı; 300 → 600 adımı puanı yükseltmeye devam ediyor ama boyutu
%2,6 **büyütüyor**, yani tek yönlü kazanç değil.

T102 aynı 120 → 300 adımını kilitsiz ölçerle +0,155 ortalama / +0,333 p10 diye
ölçmüştü. Kilitli ölçümde aynı adım +0,347 / +0,640. **İşaret aynı, büyüklük
iki katından fazla.** Bu, T111'in "kilitsiz ölçü farkları küçültüyor" bulgusuyla
aynı yönde.

### Ayrışma teslim noktasında da ölçüldü

Yukarıdaki beş koşum `-b:v 2026k` ile üretildi ve ~12 MB teslim ediyor. Bu,
T111'in ayrıştırma noktasıdır — arşivle karşılaştırılabilir olsun diye seçildi —
ama `auto`nun teslim noktası değil (16,3 MB). Aralığın etkisinin orada da aynı
kalıp kalmadığı varsayılmadı, ölçüldü: aynı çift `-b:v 2746k` ile tekrarlandı.

| koşum | anahtar kare | istenen | teslim (bayt) | ortalama | p10 | harmonik | en düşük kare |
|---|---|---|---|---|---|---|---|
| `g120-teslim` | `-g 120` | 2746 kbps | 17 642 679 | 95,794 | 95,060 | 95,790 | 92,630 |
| `g600-teslim` | `-g 600 keyint=600:scd=1` | 2746 kbps | 17 030 628 | 96,053 | 95,609 | 96,051 | 94,837 |

    ./uret.sh g120-teslim 6 120 "" 2746
    ./uret.sh g600-teslim 6 600 "keyint=600:scd=1" 2746

| çalışma noktası | Δ boyut | Δ ortalama | Δ p10 | Δ en düşük kare |
|---|---|---|---|---|
| 2026 kbps (~12 MB) | −%14,8 | +0,464 | +0,886 | +2,797 |
| 2746 kbps (~17 MB) | −%3,5 | **+0,259** | **+0,549** | +2,207 |

**Aralığın kazancı teslim noktasında yarıya düşüyor ama işaret değişmiyor.**
Yüksek bit hızında zorlanan anahtar karenin maliyeti görece küçülüyor; yine de
`-g 600` hem daha küçük dosya hem daha yüksek puan veriyor. En düşük kare
kazancı iki noktada da 2 puanın üstünde.

**Bu çift kendi arasında boyut eşli değil** (aynı `-b:v`, farklı teslim), çünkü
ölçülen şey aralığın kendi etkisi: aynı düğme, iki değer. Boyut eşli hâli
ölçülmedi.

### `scd=1`'in payı

`scd=1` ile `-force_key_frames` **ayrı mekanizmalardır**; bu bölüm yalnız
`scd=1`'i ölçüyor. `-force_key_frames`'in bu kaynaktaki etkisi T111'de ölçüldü
ve bu belgede tekrarlanmadı.

| karşılaştırma | Δ bayt | Δ ortalama | Δ p10 | Δ en düşük kare |
|---|---|---|---|---|
| `g600-scd0` → `g600-scd1` | **+%0,47** | **0,000** | **−0,003** | 0,000 |
| `g120-taban` → `g120-scd1` | −%1,07 | −0,012 | +0,002 | −0,001 |

**`scd=1` bu kaynakta kaliteye dokunmuyor.** İki `-g` değerinde de puan farkı
0,012'nin altında, yani ölçüm gürültüsü mertebesinde. Boyuta dokunuyor ama
işareti bile sabit değil: `-g 600`'de %0,47 büyütüyor, `-g 120`'de %1,07
küçültüyor.

**Anahtar kare yerleşimine hiç dokunmuyor — ölçüldü.** `scd=1` açık ve kapalı
çıktılarda anahtar kare sayısı ve aralığı birebir aynı (7 kare / 10,00 s ve
31 kare / 2,00 s). Kaynaktaki iki sahne kesmesi (28,353 s ve 56,870 s) hiçbir
koşumda anahtar kare değil. Kodlayıcının kendisi de bunu söylüyor; `scd=1`
geçilen koşumların günlüğünde SVT-AV1 şu satırı basıyor:

    Svt[warn]: SVT-AV1 has an integrated mode decision mechanism to handle
    scene changes and will not insert a key frame at scene changes

Bu satır yalnız `scd=1` geçilen koşumlarda çıkıyor. Ölçüm koşumlarının
günlüğünde göremezsin — `uret.sh` ffmpeg'i `-loglevel error` ile çağırıyor ve
bu satır `warning` seviyesinde. Ayrı bir 1 saniyelik koşumla üç kipi yan yana
koydum; üreten komut:

    for sp in "keyint=600:scd=1" "keyint=600:scd=0" ""; do
      if [ -n "$sp" ]; then A=(-svtav1-params "$sp"); else A=(); fi
      n=$(ffmpeg -hide_banner -y -nostdin -threads 4 -t 1 -i gui/parca-2.mkv           -c:v libsvtav1 -preset 6 -b:v 2026k -g 600 -pix_fmt p010le "${A[@]}"           -an -f null NUL 2>&1 | grep -c "integrated mode decision")
      echo "svtav1-params='${sp:-yok}' -> $n"
    done

Sonuç: `scd=1` → 1, `scd=0` → 0, parametresiz → 0. Yani SVT-AV1 `v4.2.0-68`
bayrağı alıyor, sahne kesmesine anahtar kare koymayacağını söylüyor, ama
çıktıyı yine de değiştiriyor — dosyalar bayt bayt farklı
(`cmp ciktilar/g120-scd1.mp4 ciktilar/g120-taban.mp4` → `differ: char 647`).
**Değiştirdiği şeyin ne olduğu ölçülmedi**; ölçülen, kaliteye etkisinin
sıfıra yakın olduğu.

**Tek kaynakta ölçüldü.** Daha sık ve daha sert kesmeli bir içerikte `scd=1`'in
aynı çıkacağı **ölçülmedi**.

---

## K5 — `y2`/`y3`'ün p10'u neden diğerlerinin yedi katı oynuyor

T111 bunu gördü ve sebebini aramadı: kilit takılınca on üç AV1 koşumunun p10'u
+0,24 ile +0,38 arasında oynarken `y2`/`y3` **+2,343 / +2,448** oynadı. **Bu
soru ölçüldü.** Yeni kodlama gerekmedi; cevap T111'in kendi arşivinde duruyordu.
Üreten komut:

    python .calisma/t120/fark.py y1-g300-izgara y2-g300-hizali auto

Kare kare kilitli ve kilitsiz ölçüleri üç bölgeye ayırdım. Sınırlar sahne
kesmeleri: 28,353 s = kare 1701, 56,870 s = kare 3412.

| koşum | bölge | kare | kilitsiz ort. | kilitli ort. | kilidin kare başına kazancı |
|---|---|---|---|---|---|
| `y1` | 0-1700 | 1701 | 96,162 | 96,257 | +0,096 |
| `y1` | 1701-3411 | 1711 | 93,016 | 95,460 | **+2,444** |
| `y1` | 3412-son | 212 | 95,169 | 95,333 | +0,164 |
| `y2` | 0-1700 | 1701 | 96,169 | 96,262 | +0,093 |
| `y2` | 1701-3411 | 1711 | 93,050 | 95,499 | **+2,449** |
| `y2` | 3412-son | 212 | **73,494** | **73,514** | **+0,019** |
| `auto` | 3412-son | 212 | 95,516 | 95,675 | +0,159 |

**Kilidin kendisi `y2`'de daha çok iş yapmıyor.** Kare başına ortalama kazanç üç
koşumda da neredeyse aynı: `y1` +1,209, `y2` +1,201, `auto` +1,198. Bölge bölge
de aynı: kaymanın hasarı 1701-3411 aralığında toplanıyor ve orada üçünde de
+2,44 civarı kazandırıyor.

**Değişen şey `y2`/`y3`'ün son 212 karesi.** 56,870 s'deki zorlanmış anahtar
kareden sonra kalan 212 kare `y2`'de ortalama **73,49** alıyor; `y1`'de aynı
bölge 95,17, `auto`'da 95,52. Bu çöküş kilitten bağımsız: kilit orada yalnız
**+0,019** kazandırıyor, yani ölçüm kusuru değil, kodlamanın kendisi.

Mekanizma buradan çıkıyor. p10 3624 karenin en düşük **363**'ünü kesiyor.

| koşum | ölçüm | en düşük 363 karenin dağılımı (`<1701` / `1701-3411` / `≥3412`) | p10 |
|---|---|---|---|
| `y1` | kilitsiz | 2 / 309 / 52 | 94,870 |
| `y1` | kilitli | 0 / 218 / 145 | 95,137 |
| `y2` | kilitsiz | 1 / 150 / 212 | 92,778 |
| `y2` | kilitli | 1 / 150 / 212 | 95,121 |

`y2`'nin çöküş bloğu **212 kare**, yani kuyruğun 363 kontenjanının %58'i.
Geriye kaymanın hasar verdiği orta bölgeden yalnız **151** kare sığıyor;
`y1`'de ise 309 kare sığıyor. Kesme noktası hasarlı dağılımın daha derinine
iniyor, dolayısıyla kilitsiz p10 daha aşağıda başlıyor: 92,778 ile 94,870
arasındaki **2,09 puanlık** fark buradan geliyor. Kilit orta bölgeyi kaldırınca
iki koşum aynı yere oturuyor — kilitli p10'lar **95,121** ve **95,137**, aralarında
0,016 var.

**Sonuç:** `y2`/`y3`'ün p10'unun yedi kat oynaması kilidin onlara özel bir şey
yapmasından değil; **çöküş bloğunun kuyruk sıralamasını doldurup kesme noktasını
kaymanın hasar dağılımında daha derine itmesinden.** İki bileşenin ikisi de
ölçüldü.

**Çöküşün kendi sebebi ölçülmedi.** Son 3,5 saniyenin neden 73 puana düştüğü —
zorlanmış anahtar karenin 16 karelik mini-GOP yapısını kesmesi, ya da iki geçişli
bit dağıtımının son bloğu aç bırakması — bu belgede **ölçülmedi**. `y3`'ün
(boyutu eşitlenmiş, +108 kbit/s) aynı çöküşü yaşaması (en düşük kare 71,94) bunun
salt bit bütçesi olmadığını söylüyor; daha ötesi ölçülmedi.

**Bu bölüm T111'in tabanındaki (`3688336`) dosyalardan ölçüldü**, bugünkü
`main`'den değil. `y2`/`y3` bugünkü motorla yeniden üretilmedi; `-force_key_frames`
zaten motorun kullandığı bir mekanizma değil.

---

## Ölçülmedi

Bu belgeye giren her sayı yukarıdaki komutlardan çıktı. Aşağıdakiler **bu turda
ölçülmedi**; hiçbiri hakkında bu belgede cümle kurulmadı.

**Ölçüm kapsamı**

- **Tek kaynak.** Her sayı `parca-2.mkv` üzerinden. Başka içerikte — daha sık
  kesmeli, daha hareketli, SDR, düşük kare hızlı — aynı işaretin çıkacağı
  ölçülmedi. K2'nin "geçtik" cümlesi bu kaynak için doğrudur, genel bir iddia
  değildir.
- **Süre.** Makine paylaşımlıydı, on ajan koşuyordu. Bu belgede hiçbir süre
  sayısı yok; kodlama ya da ölçüm hızı hakkında hiçbir şey söylenmiyor.
- **Kilitsiz sayılar.** Sözleşme geçersiz saydığı için üretilmedi. K5'in
  kilitli/kilitsiz karşılaştırması yalnız T111'in arşivinden okundu, yeni
  kilitsiz kodlama yapılmadı.

**GOP ve `scd`**

- **`scd=1`'in ne değiştirdiği.** Çıktının bayt bayt değiştiği ölçüldü
  (`cmp` → `differ: char 647`), anahtar kare yerleşiminin değişmediği ölçüldü,
  kaliteye etkisinin 0,012'nin altında olduğu ölçüldü. **Hangi kararı
  değiştirdiği ölçülmedi.**
- **`-force_key_frames` bu turda hiç koşturulmadı.** T111 bu ikisinin ayrı
  mekanizmalar olduğunu işaretlemişti; bu belgedeki hiçbir `scd` cümlesi
  `-force_key_frames` hakkında bir şey söylemiyor.
- **Sahne haritalı `-g`.** Üretim yolu haritayı vermiyor, bu yüzden ölçülen
  `-g 600` haritasız varsayılan. Haritanın gerçekten bağlandığı bir koşumda
  aralığın ne olacağı **ölçülmedi**.
- **Ara `-g` değerleri.** 120, 300, 600 ölçüldü; aradaki değerler ölçülmedi,
  eğrinin şekli hakkında bir şey söylenmiyor.

**Boyut eşliği**

- **x265 tarafının boyut eğimi.** İki nokta arasında oynamadığı görüldü
  (95,759 → 95,759, %0,54 boyut farkı), ama iki noktayla eğim sayısı verilmedi.
- **AV1 eğiminin geçerlilik aralığı.** Eğim yalnız %1'lik bir aralıkta, yalnız
  preset 4 / `-g 300` koşumlarında ölçüldü. Daha geniş aralıkta ya da başka
  ayarda doğrusal kaldığı ölçülmedi.
- **Daha dar band.** AV1 tarafı −%0,414'te, HandBrake tarafı −%0,030'da
  bırakıldı. Dördüncü/üçüncü denemeye gidilmedi.

**`y2`/`y3` kuyruğu (K5)**

- **Çöküş bloğunun sebebi.** 56,870 s'den sonraki 212 karenin `y2`/`y3`'te neden
  73,49'a düştüğü **ölçülmedi**. Ölçülen: bu bloğun var olduğu, kilidin onu
  yalnız +0,019 oynattığı, ve p10 kotasının %58'ini doldurduğu.

**Düzenek**

- **`tools/VidShrink.Ab` bu turda çağrılmadı.** Okundu; verdiği ölçü kümesi bu
  sözleşmenin istediği beş sayıdan dördünü veriyor (`<1` kare sayısı yok) ve
  HandBrake rakibi farklı bir yapılandırmada (`-e x265` 8 bit, `-a none`,
  `H.265 MKV 1080p30` ön ayarı) — T111 zinciriyle karşılaştırılabilir değil.
  Bağımsız çapraz kontrol olarak koşturulması ölçülmedi.
- **`auto` planının kararsızlığı.** Aynı hedefte (16 MB) iki plan-only koşumu
  farklı plan verdi: biri `p010le hdrfilt=True`, diğeri `yuv420p hdrfilt=False`.
  Sonda kodlamaları makine yükü altında koştuğu için karar kayıyor. **Kararın
  hangi eşikte döndüğü ölçülmedi**; K1'in `auto` satırının hangi planla
  üretildiği yukarıda yazılı.
