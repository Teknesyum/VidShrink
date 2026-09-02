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
