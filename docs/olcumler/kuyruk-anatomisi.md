# Kuyruk anatomisi — en kötü %10 kare nerede duruyor (T122)

T111 kare kilidiyle **ortalamayı** neredeyse kapattı (HandBrake açığı +1,269 →
+0,097) ama **p10 kapanmadı**. Bugüne kadar p10 tek bir sayı olarak ölçüldü:
kusurun büyüklüğünü söylüyor, **yerini** söylemiyor. Bu sayfa kuyruğun hangi
kareler olduğunu, videoda nerede durduğunu ve iki kodlayıcının aynı yerde
mi bozulduğunu ölçer.

| | |
|---|---|
| Kaynak | `.calisma/kaynak/parca-2.mkv` — 1920x1080@60, HDR10, 60,442 s, 3624 kare |
| Kaynağın yeri kaynakta | 00:07:29,6 → `[449,600 – 510,042]` s (`docs/olcumler/ab-duzenegi.md:101`) |
| Ölçer | **kilitli** VMAF-NEG: `[0:v]scale=1920:1080:lanczos,settb=AVTB,setpts=N[t];[1:v]settb=AVTB,setpts=N[r]` |
| p10 tanımı | `r = 0,10·(n−1)`, `srt[floor r]` ile `srt[ceil r]` arasında doğrusal ara değer |
| "Kötü kare" tanımı | Koşumun **kendi** p10 eşiğine eşit ya da altındaki kare. Her koşumda 363 kare (3624'ün %10,02'si) |

**Kilitsiz hiçbir sayı bu sayfaya girmedi.**

## Verinin nereden geldiği

Kare kare JSON'lar T111'in arşivinden geliyor: dal `T111-auto-mod`, HEAD
`0c38256`, arşiv commit `f430952`, yol `tools/auto-mod-olcumu/vmaf-t111/`.
**T111 henüz `main`e birleşmedi**, dolayısıyla bu dosyalar T122'nin tabanında
(`59eac70`) yok; ölçüm için `.calisma/t122/vmaf/` altına kopyalandılar ve iş
bitince silindiler. Denetçi arşivi T111 dalında bulur.

Hesap betikleri `tools/kuyruk-anatomisi/` altında ve arşiv verildiğinde
yeniden koşar; hiçbiri yeniden kodlama gerektirmez.

## K1 — Kuyruk kaç parça ve nerede

`tools/kuyruk-anatomisi/k1-konum.py`. Bitişik kabul edilme toleransı **6 kare**
(0,1 sn); iki kötü kare arasında altıdan fazla iyi kare varsa ayrı küme sayılır.

| | `auto` | `uzman-hb2` |
|---|---|---|
| p10 eşiği | 94,903 | 95,380 |
| kötü kare | 363 | 363 |
| **küme sayısı** | **25** | **39** |
| ≥5 karelik küme | 13 | 21 |
| ilk kötü kare | 1703 (28,383 s) | 2341 (39,017 s) |
| son kötü kare | 3479 (57,983 s) | 3623 (60,383 s) |
| en büyük küme | 2906–2999, 48,433–49,983 s, **94 kare** | 3501–3623, 58,350–60,383 s, **123 kare** |

Kuyruk **tek bir felaket sahnesi değil**. `auto`'da 25, HandBrake'te 39 ayrı
kümeye dağılmış. En büyük küme her iki tarafta da kuyruğun dörtte birini bile
tutmuyor (94/363 ve 123/363).

### `auto` — ≥5 karelik kümeler

| kare | saniye | kare sayısı |
|---|---|---|
| 1703–1707 | 28,383–28,450 | 5 |
| **1737–1799** | **28,950–29,983** | **63** |
| 2313–2317 | 38,550–38,617 | 5 |
| 2335–2353 | 38,917–39,217 | 19 |
| 2366–2399 | 39,433–39,983 | 34 |
| 2720–2729 | 45,333–45,483 | 10 |
| 2749–2759 | 45,817–45,983 | 11 |
| **2814–2875** | **46,900–47,917** | **62** |
| **2906–2999** | **48,433–49,983** | **94** |
| 3205–3215 | 53,417–53,583 | 11 |
| 3223–3239 | 53,717–53,983 | 17 |
| 3411–3419 | 56,850–56,983 | 9 |
| 3450–3479 | 57,500–57,983 | 30 |

Kalan 12 küme 5 kareden küçük, toplam 25 kare.

### `uzman-hb2` — en büyük altı küme

| kare | saniye | kare sayısı |
|---|---|---|
| **3501–3623** | **58,350–60,383** | **123** |
| 3116–3167 | 51,933–52,783 | 52 |
| 3176–3223 | 52,933–53,717 | 48 |
| 3295–3321 | 54,917–55,350 | 27 |
| 2720–2741 | 45,333–45,683 | 22 |
| 3270–3282 | 54,500–54,700 | 13 |

Kalan 33 küme; 18'i 5 kareden küçük, toplam 34 kare.

**HandBrake'in en büyük kümesi klibin son 2 saniyesi.** 123 kare, 3501'den
son kareye kadar kesintisiz. `auto`'nun kuyruğu 3479'da bitiyor; klibin son
144 karesinde `auto`'nun tek bir kötü karesi yok.

## K2 — İki çıktının kötü kareleri aynı yerde mi

Sözleşmenin çekirdek sorusu. Aynı yerdeyse kusur **kaynağa** aittir: o sahne
zordur, kodlayıcı değiştirmek kurtarmaz. Farklı yerdeyse kusur **kodlayıcıya**
aittir ve düzeltilebilir.

`tools/kuyruk-anatomisi/k2-kesisim.py` ve `k2b-cozunurluk.py`.

### Kare düzeyinde

| | |
|---|---|
| `auto` kötü kare | 363 |
| `uzman-hb2` kötü kare | 363 |
| **kesişim** | **68 kare** |
| Jaccard | 0,1033 |
| her iki tarafın örtüşme oranı | 0,1873 |
| bağımsızlık varsayımıyla beklenen | 363 · 363 / 3624 = **36,4 kare** |
| gözlenen / şans | **1,87×** |
| üst sınır (tamamen aynı yer) | 363 kare |

Ölçek: kesişim **36,4** olsaydı iki kuyruk birbirinden tamamen bağımsız,
**363** olsaydı tamamen aynı yerde olurdu. Gözlenen 68, bu aralığın
**%9,7**'sinde. Yani kare düzeyinde iki kuyruk **birbirinden bağımsıza çok
daha yakın.**

### Çözünürlük taraması

Kare düzeyi tek bakış açısı değil. Aynı sahnede bozulup farklı karelerde
bozulmak ile bambaşka sahnelerde bozulmak farklı şeyler. Kötü kareler
`w` karelik kovalara toplanıp aynı hesap tekrarlandı:

| pencere | sn | `auto` kova | `hb2` kova | kesişim | şans | üst sınır | oran | normalize |
|---|---|---|---|---|---|---|---|---|
| 1 | 0,02 | 363 | 363 | 68 | 36,4 | 363 | 1,87× | **0,097** |
| 3 | 0,05 | 141 | 168 | 43 | 19,6 | 141 | 2,19× | 0,193 |
| 6 | 0,10 | 82 | 108 | 32 | 14,7 | 82 | 2,18× | 0,257 |
| 15 | 0,25 | 42 | 56 | 24 | 9,7 | 42 | **2,47×** | 0,442 |
| 30 | 0,50 | 27 | 34 | 19 | 7,6 | 27 | **2,50×** | 0,588 |
| 60 | 1,00 | 17 | 20 | 13 | 5,6 | 17 | 2,33× | 0,650 |
| 120 | 2,00 | 11 | 12 | 8 | 4,3 | 11 | 1,88× | 0,555 |
| 300 | 5,00 | 7 | 6 | 5 | 3,2 | 6 | 1,55× | 0,639 |

Normalize sütunu `(gözlenen − şans) / (üst sınır − şans)`: 0 = şans,
1 = tamamen aynı yer.

**Cevap iki katmanlı ve ikisi de sayıyla duruyor:**

1. **Saniye ölçeğinde ortak bir zemin var.** Yarım saniyelik pencerede
   normalize örtüşme 0,588; iki kodlayıcı klibin kabaca aynı bölgelerinde
   zorlanıyor. Bu pay **kaynağa** ait.
2. **Kare ölçeğinde ortaklık yok.** Aynı yarım saniyenin içinde hangi
   karenin düşeceğini kaynak belirlemiyor: normalize örtüşme 0,097'ye
   iniyor, şansın 1,87 katı. Kuyruğun **%81'i** (295/363) yalnız bir
   tarafta.

Kaynak arenayı seçiyor, kodlayıcı kurbanı seçiyor. **Kuyruğun büyük kısmı
kodlayıcıya ait, dolayısıyla düzeltilebilir.**

### Ayrışan kümeler

İki tarafın en büyük kümeleri birbirini hiç görmüyor:

| küme | `auto`'da | `hb2`'de |
|---|---|---|
| 1737–1799 (28,95–29,98 s), 63 kare | **tamamı kötü** | tek kare yok |
| 3501–3623 (58,35–60,38 s), 123 kare | tek kare yok | **tamamı kötü** |
| 2814–2875 (46,90–47,92 s), 62 kare | **tamamı kötü** | tek kare yok |
| 3116–3167 (51,93–52,78 s), 52 kare | tek kare yok | **tamamı kötü** |

Ortak kümelerin en büyükleri çok daha küçük: 2341–2353 (13 kare),
3450–3462 (13), 2909–2919 (11), 3206–3215 (10).

48,4–50,0 s aralığı iki tarafta da kötü ama **iç içe geçmiş**: `auto`
2906–2939 / 2949–2969 / 2978–2999'da, `hb2` 2909–2919 / 2940–2948 /
2970–2977'de. Aynı saniyeler, farklı kareler — yukarıdaki iki katmanın
tek bir örnekte görünmüş hali.

## K3 — Sahne haritasıyla üst üste bindirme

T105'in haritası (`tools/sahne-yer-gercegi/sahneler.csv`) 1036 s'lik kaynağın
tamamını kapsıyor. `parca-2` kaynağın `[449,600 – 510,042]` aralığı olduğuna
göre üç sahneye düşüyor:

| sahne | kaynakta | parçada (sn) | parçada (kare) | T105 karmaşıklığı |
|---|---|---|---|---|
| S13 | 333,300–477,933 | 0,000–28,333 | 0–1700 | **0,07119** |
| S14 | 477,933–506,450 | 28,333–56,850 | 1700–3411 | **0,12892** |
| S15 | 506,450–519,666 | 56,850–60,442 | 3411–3624 | **0,08546** |

Bağımsız doğrulama: T111 hizalama koşumlarında `-force_key_frames
28.353,56.870` kullanılmıştı. Aradaki +0,020 s tam olarak `parca-2`'nin kap
içi video başlangıç kaymasıdır (`docs/olcumler/algi-olcusu.md:649`). Aynı iki
kesim, iki ayrı yoldan.

`tools/kuyruk-anatomisi/k4-sahne.py`:

| koşum | sahne | kare | kötü kare | % | şanstan | oran |
|---|---|---|---|---|---|---|
| `auto` | S13 | 1700 | **0** | %0,0 | 170,3 | **0,00** |
| `auto` | S14 | 1711 | 332 | %19,4 | 171,4 | 1,94 |
| `auto` | S15 | 213 | 31 | %14,6 | 21,3 | 1,45 |
| `uzman-hb2` | S13 | 1700 | **0** | %0,0 | 170,3 | **0,00** |
| `uzman-hb2` | S14 | 1711 | 226 | %13,2 | 171,4 | 1,32 |
| `uzman-hb2` | S15 | 213 | 137 | %64,3 | 21,3 | **6,42** |
| `uzman-biz3` | S13 | 1700 | **0** | %0,0 | 170,3 | **0,00** |
| `uzman-biz3` | S14 | 1711 | 360 | %21,0 | 171,4 | 2,10 |
| `uzman-biz3` | S15 | 213 | 3 | %1,4 | 21,3 | 0,14 |

**S13 klibin %46,9'u ve içinde üç koşumun da tek bir kötü karesi yok.** p10
tamamen klibin ikinci yarısında belirleniyor. Bu K2'nin "saniye ölçeğinde
ortak zemin" katmanının kaynağı: üç kodlayıcı da aynı sahnede rahat, aynı
sahnede zorlanıyor.

### Sahne geçişinin hemen sonrası mı?

Kesimler 1700 ve 3411. İki kesimin ardındaki pencerelerde kötü kare
yoğunluğu:

| pencere | `auto` kötü / şans / oran | `hb2` kötü / şans / oran |
|---|---|---|
| kesim +15 kare (0,25 s) | 11 / 3,0 / **3,66** | 0 / 3,0 / **0,00** |
| kesim +30 kare (0,50 s) | 11 / 6,0 / 1,83 | 0 / 6,0 / **0,00** |
| kesim +60 kare (1,00 s) | 41 / 12,0 / 3,41 | 11 / 12,0 / 0,92 |
| kesim +120 kare (2,00 s) | 88 / 24,0 / 3,66 | 44 / 24,0 / 1,83 |
| kesim −60 kare | 1 / 12,0 / 0,08 | 7 / 12,0 / 0,58 |

`auto`'da kesim sonrası gerçekten bir yığılma var, ama **kuyruğun
tamamının %24'ü** (88/363) ve HandBrake'te aynı etki yok (+15 ve +30
karede sıfır). Yalnız iki kesim var; bu, üç maddelik testin en zayıf
ayağı ve tek başına bir cevap taşımıyor.

### Sahnenin içi — GOP fazı

Asıl yapı burada. `auto`'nun 13 büyük kümesinden **yedisi** 120'nin katından
en çok beş kare önce bitiyor: 1799, 2399, 2759, 2875, 2999, 3239, 3479.
Hiçbir küme GOP'un ilk 23 karesinde **başlamıyor**.

`tools/kuyruk-anatomisi/k3-gop-fazi.py` her kareyi `kare % g` fazına göre
kovalıyor. Aşağıdaki tablo **yalnız S14** üzerinde (`[1700, 3411)`) — S13'ün
sıfır kötü karesi hesabı kaçırmasın diye:

| `auto` faz (g=120) | ort VMAF | kötü kare | beklenen | oran |
|---|---|---|---|---|
| 0–9 | 95,703 | **0** | 27,2 | **0,00** |
| 10–19 | 95,496 | 1 | 27,2 | 0,04 |
| 20–29 | 95,394 | 9 | 29,1 | 0,31 |
| 30–39 | 95,442 | 18 | 29,1 | 0,62 |
| 40–49 | 95,369 | 11 | 29,1 | 0,38 |
| 50–59 | 95,069 | 22 | 27,4 | 0,80 |
| 60–69 | 95,000 | 36 | 27,2 | 1,33 |
| 70–79 | 95,039 | 32 | 27,2 | 1,18 |
| 80–89 | 95,012 | 45 | 27,2 | 1,66 |
| 90–99 | 94,979 | 46 | 27,2 | 1,69 |
| 100–109 | 94,971 | 53 | 27,2 | 1,95 |
| 110–119 | 94,944 | **59** | 27,2 | **2,17** |

Anahtar kareden itibaren kalite **tek yönlü** düşüyor: 95,703 → 94,944,
**0,759 puan**. Kötü kare yoğunluğu 0,00'dan 2,17'ye çıkıyor.

Aynı testin `-g 300` ile koşan `uzman-biz3` üzerindeki hali (yine yalnız S14):

| faz (g=300) | ort VMAF | kötü kare | beklenen | oran |
|---|---|---|---|---|
| 0–24 | 96,177 | **0** | 31,6 | 0,00 |
| 125–149 | 95,813 | 8 | 26,3 | 0,30 |
| 250–274 | 95,654 | 68 | 31,6 | 2,15 |
| 275–299 | 95,616 | **75** | 31,6 | **2,38** |

Aynı testere dişi, farklı periyotta. Bu, GOP uzunluğu değişince faza göre
yeniden hizalandığı için **kaynağın bir özelliği olamaz** — kodlayıcının
bit dağıtımının özelliğidir.

### Üç seçenekten cevap

Sözleşme üç şık soruyor: sahne geçişinin hemen sonrası (anahtar kare
bütçesi), sahnenin içi (bit hızı), ya da ilişkisiz.

**Cevap: sahnenin içi.** Kuyruk sahne geçişlerine değil, sahne
karmaşıklığına ve GOP fazına bağlı. Destekleyen üç sayı: S13'te sıfır kötü
kare, GOP fazı 0–9'da sıfır kötü kare, faz 110–119'da 2,17× yığılma.
Sahne geçişi etkisi `auto`'da var ama küçük (kuyruğun %24'ü) ve
HandBrake'te yok.

## Yeniden kodlama ve neyin neyle eşlendiği

K1–K3 arşivden çıktı, yeni kodlama gerekmedi. K4 ve K5 **kare başına bit**
istiyor; T111'in kodlama çıktıları o sözleşmenin temizliğinde silinmişti,
dolayısıyla iki koşum yeniden üretildi (`tools/kuyruk-anatomisi/k45-uret.sh`,
tek seferde tek ağır kodlama, `-threads 4` + `lp=4` / `pools=4`).

| koşum | T111'deki boyut | yeniden üretilen | fark |
|---|---|---|---|
| `uzman-hb2` | 15 743 067 B | **15 743 067 B** | **0** |
| `auto` | 15 496 155 B | 14 450 295 B | −%6,7 |

**`uzman-hb2` birebir yeniden üretildi.** Yeni dosya kilitli ölçerden geçirildi
ve arşivin JSON'uyla **kare kare aynı** çıktı: ortalama 95,7432, p10 95,3799,
en büyük kare farkı **0,000000**, kötü kare kümesi birebir aynı 363 kare.
HandBrake tarafında bit verisi ile K1–K3'ün kalite verisi **aynı dosyadan**
geliyor.

`auto` yeniden üretilemedi ve bu beklenen bir sonuç: `auto` düzenekten değil
uygulamanın kendi başsız yolundan çıkmıştı (`t111-olc.sh` onu
`gui/parca-2_shrunk.mp4` üzerinde ölçüyor), T111 zaten motorun o tabandan beri
değiştiğini ve yeni `auto`'nun T102'nin dosyasına eşlenmediğini kaydetmişti.
Yeniden üretilen `auto` **ayrı bir koşum** sayıldı ve kendi kilitli ölçümü
alındı:

| | arşiv `auto` | yeniden üretilen `auto` |
|---|---|---|
| ortalama | 95,647 | 95,486 |
| p10 | 94,903 | 94,497 |
| en düşük kare | 92,376 | 91,785 |
| kötü kare kesişimi | — | **317 / 363 (%87,3)** |

Bu kesişim K2'nin en güçlü kontrolü: **aynı yapılandırmanın iki ayrı kodlaması
kuyruğunun %87'sini aynı karelere koyuyor** (şans 36,4 kare, yani %10). Aynı
kaynağa karşı `auto` ↔ HandBrake kesişimi %18,7. Kuyruğun yerini belirleyen
şey kodlayıcıdır; ölçüm gürültüsü değildir.

**K4 ve K5'in her sayısı, kalite ve bit aynı dosyadan olmak üzere yeniden
üretilen çiftten alınmıştır.** K1–K3'ün sayıları arşivden; ikisi karıştırılmadı.

## K4 — Kötü karelerin içeriği

`tools/kuyruk-anatomisi/bitler.py` paket boyutlarını `ffprobe`'dan alır
(`packet=pts_time,size,flags`, pts'e göre sıralanır; hb2 B kare taşıdığı için
sıralama zorunlu). Kalite verisi aynı dosyanın `vmaf-t122/` altındaki kilitli
ölçümünden.

### Bütçe nereye gidiyor

| | `auto` | `uzman-hb2` |
|---|---|---|
| video toplamı | 13 397 086 B | 14 665 457 B |
| **anahtar kare sayısı** | **31** (%0,86) | **7** (%0,19) |
| anahtar karelere giden bayt | **11 235 518 (%83,9)** | **2 633 052 (%18,0)** |
| anahtar kare başına | 362 436 B | 376 150 B |
| inter karelere giden bayt | 2 161 568 (%16,1) | 12 032 405 (%82,0) |
| **inter kare başına** | **602 B** | **3 327 B** |

Anahtar kare başına maliyet iki tarafta neredeyse aynı (362 KB ↔ 376 KB).
Fark **sayıdadır**: `-g 120` 60 fps'te iki saniyede bir anahtar kare demek,
60 saniyelik klipte 31 tane. HandBrake 7 tane koyuyor. Sonuç: `auto`'nun
inter kareleri HandBrake'inkinin **beşte biri** kadar bit alıyor.

### Anahtar karelerin yeri — ölçülen

| koşum | anahtar kare indeksleri |
|---|---|
| `auto` | 0, 120, 240, … 3600 (tam 120'lik ızgara) + sahne kaynaklı 1697, 2313, 2345, 2721, 2777, 2881, 3377, 3393, 3409 |
| `uzman-hb2` | 0, 600, 1200, **1700**, 2300, 2900, **3411** |

HandBrake'in yedi anahtar karesinden **ikisi tam olarak S13→S14 (1700) ve
S14→S15 (3411) kesimlerine oturuyor**; kalan beşi 600 karelik (10 s) ızgara.
SVT-AV1 aynı kesimleri de buluyor (1697 ve 3409'da fazladan intra) ama
üstüne katı 120 ızgarasını da koyuyor.

Bu, K3'ün testere dişini açıklıyor: her 120 karede bir 362 KB'lik bir intra
bütçeyi silip süpürüyor, arkasındaki 119 kare artakalanı paylaşıyor ve
anahtar kareden uzaklaştıkça kalite tek yönlü düşüyor.

### Kaynak 60 fps kapta 30 fps içerik

`auto`'nun inter karelerinin **1781'i (%49,6) 10 bayttan küçük** — hepsi
**çift indeksli** ve ardışık farkları 2 (1750 çiftte 2, 30 çiftte 4).
3 bayt AV1'de "önceki kareyi tekrarla" demektir. Çıkarım kapanıyor: kodlayıcı
kopya yazdığı halde puan düşmüyorsa (aşağıdaki tablo) **kaynak kareler de
birbirinin aynısıdır**. Yani kaynak 60 kare/s kapta 30 kare/s içerik taşıyor
ve SVT-AV1 bunu doğru görüyor.

Bunun kuyruğa etkisi ölçüldü:

| | |
|---|---|
| atlanan kare ile öncekinin VMAF farkı | ortalama **+0,0025**, medyan −0,0010 |
| farkı 0,5 puandan büyük olan | 1781 karede **20** |
| kötü kare çiftleri: ikisi de kötü | **165** |
| yalnız biri kötü | 26 |

Atlanan kare, kopyaladığı karenin puanını **aynen** alıyor. Yani `auto`'nun
363 kötü karesi **191 ayrı içerik anına** karşılık geliyor; kuyruk sayının
gösterdiğinin yarısı kadar geniş. Bu, p10'un ölçüde nasıl davrandığına dair
bir düzeltme değil (eşik aynı), kuyruğun **yorumuna** dair bir düzeltme.

**`uzman-hb2`'de 10 bayttan küçük tek kare yok.** x265 kopyaları da
kodluyor; kopya karelerin VMAF farkı ortalama −0,0042, yani sonuç aynı,
maliyeti değil. HandBrake'in çift kare çiftlerinde ikisi de kötü olan
sayısı 130.

## K5 — HandBrake aynı yerde ne yapıyor

`tools/kuyruk-anatomisi/k5b-ayrisan.py`. Her kare için iki dosyanın paket
boyutu yan yana konur. İki dosyanın inter kare ortalaması farklı olduğu için
(602 B ↔ 3327 B, **5,53 kat**) mutlak bayt karşılaştırması yanıltıcı olurdu;
**her sayı kendi dosyasının inter kare ortalamasına bölünmüş** olarak da
veriliyor (`×` sütunu). Bu, seviye farkını çıkarıp **dağıtım biçimini**
karşılaştırır — sorunun sorduğu şey budur.

Anahtar kareler dışarıda. Kalite verisi her iki tarafta kendi dosyasının
kilitli ölçümünden; `auto` yeniden üretilen koşum.

| küme | kare | `auto` bayt | `auto` × | `auto` VMAF | `hb2` bayt | `hb2` × | `hb2` VMAF |
|---|---|---|---|---|---|---|---|
| tüm inter kareler | 3589 | 602 | 1,00 | 95,483 | 3332 | 1,00 | 95,743 |
| **yalnız `auto` kötü** | 302 | 630 | **1,05** | 94,144 | 6292 | **1,89** | 95,553 |
| **yalnız `hb2` kötü** | 297 | 892 | **1,48** | 95,239 | 1681 | **0,51** | 95,327 |
| ortak kötü | 60 | 1422 | 2,36 | 94,101 | 3828 | 1,15 | 95,271 |
| ikisi de iyi | 2930 | 553 | 0,92 | 95,674 | 3184 | 0,96 | 95,814 |

Tablo simetrik ve tek bir şey söylüyor:

- `auto`'nun **yalnız kendi** düştüğü 302 karede kendi ortalamasının
  **1,05 katını** harcıyor — yani o kareleri özel olarak görmüyor.
  HandBrake aynı karelerde kendi ortalamasının **1,89 katını** harcıyor
  ve **düşmüyor** (95,553).
- Aynanın öbür yüzü: HandBrake'in **yalnız kendi** düştüğü 297 karede
  kendi ortalamasının **0,51 katını** harcıyor; `auto` aynı karelerde
  **1,48 katını** harcıyor ve **düşmüyor** (95,239).
- İkisinin de düştüğü 60 kare gerçekten zor: `auto` 2,36×, HandBrake
  1,15× harcıyor, ikisi de kurtaramıyor. Kuyruğun **%16,5**'i.

**Kuyruk bir yetenek sorunu değil, bir dağıtım sorunu.** Her iki kodlayıcının
kötü kareleri, tam olarak kendi hız denetiminin öbürünün yargısına göre
az beslediği karelerdir. Bu K2'nin sonucunu ikinci bir yoldan doğruluyor:
kusur kodlayıcıya ait ve düzeltilebilir.

### Kuyruğu kapatmanın ölçülen maliyeti

`auto`'nun yalnız kendi düştüğü 302 karenin her birine HandBrake'in oranını
(1,89×) vermek, kare başına 630 B yerine 1138 B demek. Fark 302 × 508 B =
**153 KB**, video bütçesinin **%1,14**'ü. Bu bit başka bir yerden gelmek
zorunda; nereden geleceği K6'nın konusu.

