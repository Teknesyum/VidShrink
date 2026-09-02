# Auto mod — bugünkü motorla yeniden ölçüm

T120. Ölçüm tarihi 2026-09-02. Dal `T120-yeni-taban`, **taban commit `2d5f710`**
(`origin/main`). Bu belgedeki her sayı o ağaçtaki motorla, bu makinede
koşturulmuş bir ölçümden gelir. Ölçülmemiş olan yerde açıkça **ölçülmedi** yazar.

`docs/olcumler/auto-mod.md` (T102 + T111) aynı kaynakta aynı soruları soruyor ama
**taban `3688336`** ile. O tabandan beri `main` motoru değiştirdi. Bu belge o
ölçümü bugünkü motorla tekrarlar ve iki tabanı yan yana koyar.

## Neyin değiştiği ölçüldü, varsayılmadı

Bugünkü auto planı, ölçümden önce plan kipinde alındı:

    T102_PLAN_ONLY=1 tools/auto-mod-olcumu/harness/bin/Release/net8.0/t102harness.exe \
      .calisma/t120/gui/parca-2.mkv 16 .calisma/t120/ciktilar/auto.mp4

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
