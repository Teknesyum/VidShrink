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

K1–K3'ün kare kare JSON'ları T111'in arşivinden geliyor: dal
`T111-auto-mod`, HEAD `0c38256`, arşiv commit `f430952`, yol
`tools/auto-mod-olcumu/vmaf-t111/`. **T111 henüz `main`e birleşmedi**,
dolayısıyla bu dosyalar T122'nin tabanında (`59eac70`) yok. Bu sayfanın
denetlenebilir olması için gereken üç tanesi
(`auto`, `uzman-hb2`, `uzman-biz3`) buraya `arsiv-` önekiyle kopyalandı:
`tools/kuyruk-anatomisi/vmaf-t122/arsiv-*-kilitli.json.gz`. İçerik
değiştirilmedi.

K4–K6'nın sayıları bu sözleşmenin kendi koşumlarından ve aynı klasörde,
öneksiz. İki küme karıştırılmadı; ayrımı "Yeniden kodlama" bölümü anlatıyor.

Hesap betiklerinin tamamı `tools/kuyruk-anatomisi/` altında ve **başka hiçbir
dosya olmadan** koşar; hiçbiri yeniden kodlama gerektirmez.

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
   normalize örtüşme 0,588, bir saniyelikte 0,650 — taramanın en yüksek
   değeri. İki kodlayıcı klibin kabaca aynı bölgelerinde zorlanıyor.
   Bu pay **kaynağa** ait.
2. **Kare ölçeğinde ortaklık büyük ölçüde kayboluyor.** Aynı yarım
   saniyenin içinde hangi karenin düşeceğini kaynak belirlemiyor:
   normalize örtüşme 0,097'ye iniyor. Şansın 1,87 katı, yani sıfır değil
   ama üst sınırın onda biri. Kuyruğun **%81'i** (295/363) yalnız bir
   tarafta.

Kaynak arenayı seçiyor, kodlayıcı kurbanı seçiyor. **Kuyruğun büyük kısmı
kodlayıcıya ait, dolayısıyla düzeltilebilir.**

Bu okumanın kontrolü aşağıda: aynı yapılandırmanın iki ayrı kodlaması
kuyruğunun **%87,3'ünü** aynı karelere koyuyor. Kare düzeyindeki %18,7,
ölçüm gürültüsünün tavanı değil; kodlayıcı farkının kendisidir.

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
tamamen klibin ikinci yarısında belirleniyor.

Ortaklık burada bitiyor. S14'te üçü de yükseliyor (1,94 / 1,32 / 2,10) ama
S15'te tamamen ayrışıyorlar: `uzman-hb2` **6,42**, `auto` 1,45, `uzman-biz3`
**0,14**. Yani "zor sahne" diye tek bir liste yok; ortak olan yalnız S13'ün
kolay olduğu. K2'nin "saniye ölçeğinde ortak zemin" katmanının payı buradan
geliyor, ve S15 aynı katmanın sınırını gösteriyor.

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

Anahtar kareden itibaren kalite düşüyor: 95,703 → 94,944, uçtan uca
**0,759 puan**. On iki dilimin onunda bir önceki dilimin altında; iki
istisna da 0,05 puanın altında (30–39 +0,048, 70–79 +0,039). Kötü kare
yoğunluğu 0,00'dan 2,17'ye çıkıyor.

Aynı testin `-g 300` ile koşan `uzman-biz3` üzerindeki hali (yine yalnız S14):

| faz (g=300) | ort VMAF | kötü kare | beklenen | oran |
|---|---|---|---|---|
| 0–24 | 96,177 | **0** | 31,6 | **0,00** |
| 25–49 | 95,757 | 14 | 31,6 | 0,44 |
| 50–74 | 95,667 | 28 | 31,6 | 0,89 |
| 75–99 | 96,139 | 18 | 31,6 | 0,57 |
| 100–124 | 96,011 | 16 | 28,6 | 0,56 |
| 125–149 | 95,813 | 8 | 26,3 | 0,30 |
| 150–174 | 95,708 | 13 | 26,3 | 0,49 |
| 175–199 | 95,783 | 28 | 26,3 | 1,06 |
| 200–224 | 95,732 | 32 | 31,6 | 1,01 |
| 225–249 | 95,661 | 60 | 31,6 | 1,90 |
| 250–274 | 95,654 | 68 | 31,6 | 2,15 |
| 275–299 | 95,616 | **75** | 31,6 | **2,38** |

Burada eğri `auto`'daki kadar düzgün değil: 75–99 dilimi araya bir sıçrama
koyuyor (96,139). Duran iki uç şu: **fazın ilk 25 karesinde sıfır kötü kare**
ve yığılmanın tamamı son dilimlerde (225'ten sonraki 75 karede 203 kötü kare,
S14'ün kuyruğunun %56,4'ü). Aynı testere dişi, farklı periyotta. Bu, GOP uzunluğu değişince faza göre
yeniden hizalandığı için **kaynağın bir özelliği olamaz** — kodlayıcının
bit dağıtımının özelliğidir.

### Üç seçenekten cevap

Sözleşme üç şık soruyor: sahne geçişinin hemen sonrası (anahtar kare
bütçesi), sahnenin içi (bit hızı), ya da ilişkisiz.

**Cevap: sahnenin içi.** Kuyruk sahne geçişlerine değil, sahne içindeki
GOP fazına bağlı. Destekleyen üç sayı: S13'te sıfır kötü kare, GOP fazı
0–9'da sıfır kötü kare, faz 110–119'da 2,17× yığılma. Sahne geçişi etkisi
`auto`'da var ama küçük (kuyruğun %24'ü) ve HandBrake'te yok.

Sahne içinde ikinci bir aday — hareket — K4'te aynı ölçüyle sınandı ve
elendi: hareket onda birlikleri arasında yığılma oranı 0,75 ile 1,24
arasında düz kalıyor. Sahnenin içinde kuyruğu belirleyen değişken **anahtar
kare aralığı**.

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

`tools/kuyruk-anatomisi/k4b-yapi.py` (yapı ve paket sayıları),
`k4c-hareket.py` (hareket), `k5-bit.py` (sahne/faz başına bayt).
`bitler.py` paket boyutlarını `ffprobe`'dan alır
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
| inter paket başına | 602 B | 3 327 B |
| **kodlanan inter kare başına** | **1 190 B** | **3 327 B** |

Son satır aşağıda gerekçelendiriliyor: SVT-AV1 görüntü karelerinin yarısını
`show_existing_frame` olarak yazıyor, dolayısıyla `auto`'da paket sayısı
kodlanan kare sayısı değil. Karşılaştırmaya giren sayı **kodlanan** satırdır.

Anahtar kare başına maliyet iki tarafta neredeyse aynı (362 KB ↔ 376 KB).
Fark **sayıdadır**: `-g 120` 60 fps'te iki saniyede bir anahtar kare demek,
60 saniyelik klipte 31 tane. HandBrake 7 tane koyuyor. Sonuç: `auto`'nun
kodlanan inter kareleri HandBrake'inkinin **%36'sı** kadar bit alıyor
(1190 ↔ 3327).

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

### AV1'de paket boyutu kare maliyeti değil — ölçülen düzeltme

`auto`'nun 3593 inter paketinin **1781'i (%49,6) tam 3 bayt**; dağılımda
başka küçük boyut yok, hepsi **çift görüntü indeksinde**, ardışık farkları
2 (1750 çiftte 2, 30 çiftte 4).

İlk okuyuşta bu "önceki kareyi tekrarla" gibi görünüyor ve kaynağın 60 fps
kapta 30 fps içerik taşıdığı sonucuna götürüyor. **Bu sonuç sınandı ve
çürütüldü.** Üç ölçüm:

| sınama | ölçülen | sonuç |
|---|---|---|
| Kaynakta ardışık iki karenin MD5'i aynı mı (`ffmpeg -f framehash`) | 3623 çiftin **0**'ı | kaynakta tekrar kare yok |
| Kaynakta ardışık kare farkı (`tblend=difference,signalstats` YAVG, 10 bit) | 3 baytlık konumlarda ort **0,566** / medyan 0,115; diğerlerinde ort **0,521** / medyan 0,119 | iki grup **ayırt edilemiyor** |
| `auto` çıktısında ardışık iki karenin MD5'i aynı mı | 3623 çiftin **83**'ü; 1781 üç baytlık karenin yalnız **43**'ü | 3 baytlık paket kopya kare üretmiyor |

Yani 3 baytlık paketler AV1'in `show_existing_frame`'leridir: **bitleri daha
önceki bir pakette taşınan, o noktada yalnız gösterilen kareler.** Yapı
kodlayıcıya ait, kaynağa değil — ve ayarlardan bağımsız:

| koşum | anahtar kare | inter paket | 3 baytlık | **kodlanan inter** |
|---|---|---|---|---|
| `auto` (`-g 120`) | 31 | 3593 | 1781 | **1812** |
| `auto-g300` | 13 | 3611 | 1799 | **1812** |
| `auto-g600` | 7 | 3617 | 1805 | **1812** |
| `auto-g600-boyutesit` | 7 | 3617 | 1805 | **1812** |
| `uzman-hb2` (x265) | 7 | 3617 | **0** | 3617 |

Dört SVT-AV1 koşumunun dördü de **tam 1812** inter kare kodluyor — `-g`
değişse, bit hızı değişse bile. 1812 = 3624 / 2. Bu düzenin AV1 piramidinde
hangi mekanizmadan çıktığı **ölçülmedi**; ölçülen şey değişmezliği.

**Sonucu:** AV1'de görüntü indeksi başına paket boyutu, o karenin maliyeti
değildir. Kare başına bit veren her sayı bu yüzden iki türlü verilir:
tüm inter paketler üzerinden (`auto` 602 B) ve **yalnız kodlanan** inter
kareler üzerinden (`auto` **1190 B**). HandBrake'te ikisi aynı (3327 B),
çünkü x265 her görüntü karesini kodluyor.

`auto`'nun 363 kötü karesinin 174'ü (%47,9) 3 baytlık konumda — payı
klipteki genel paya (%49,6) eşit. Kuyruk bu yapıyla **ilişkili değil**.

### Hareket — ölçüldü, sahne içinde kuyruğu açıklamıyor

`tools/kuyruk-anatomisi/k4b-kaynak.sh` kaynağın ardışık iki karesi
arasındaki farkı ölçer (`tblend=all_mode=difference` + `signalstats`;
YAVG = fark karesinin ortalama parlaklığı, 10 bit ölçekte 0–1023). Kare
başına bir hareket vekili. `k4c-hareket.py` onu kuyrukla karşılaştırır;
ham ölçüm `hareket-t122/kaynak-fark.csv.gz`.

| sahne | ort YAVG | **medyan** | en büyük |
|---|---|---|---|
| S13 | 0,124 | **0,051** | 1,5 |
| S14 | 0,931 | **0,159** | 125,8 |
| S15 | 0,769 | **0,096** | 123,0 |

Ortalamalar birkaç sahne kesme karesinin egemenliğinde (en büyük 125,8'e
karşı medyan 0,11), o yüzden karşılaştırma medyandan. **Üç koşumun da sıfır
kötü kare verdiği S13, medyanda S14'ün üçte biri kadar hareketli.** Sahneler
arasında ilişki gerçek.

Sahnenin **içinde** değil. S14'ün 1711 karesi hareket onda birliklerine
bölünüp K3'teki yığılma oranının aynısı hesaplandı:

| dilim | YAVG aralığı | `auto` | `uzman-hb2` | `uzman-biz3` |
|---|---|---|---|---|
| 1 (en durgun) | 0,001–0,102 | 0,75 | 1,55 | 1,20 |
| 2 | 0,102–0,118 | 1,24 | 1,11 | 1,45 |
| 3 | 0,118–0,131 | 1,12 | 0,62 | 1,03 |
| 4 | 0,131–0,144 | 1,12 | 0,84 | 1,00 |
| 5 | 0,144–0,159 | 1,12 | 0,84 | 0,89 |
| 6 | 0,159–0,179 | 1,05 | 0,71 | 0,81 |
| 7 | 0,179–0,222 | 0,87 | 0,66 | 0,72 |
| 8 | 0,222–0,364 | 0,99 | 1,28 | 1,11 |
| 9 | 0,364–0,796 | 0,90 | 0,80 | 0,81 |
| 10 (en hareketli) | 0,796–125,8 | 0,84 | 1,58 | 0,99 |

Tek yönlü bir eğilim yok. `auto` 0,75–1,24, `uzman-hb2` 0,62–1,58,
`uzman-biz3` 0,72–1,45 arasında salınıyor; en durgun dilim ile en hareketli
dilim arasında `auto`'da fark 0,09. **Aynı sahnede, aynı ölçüyle, GOP fazı
0,00'dan 2,17'ye çıkıyordu.**

"Yüksek hareketli sahnelerde kötü" cümlesi bu klipte **kurulamaz**.
Sahneler arası fark ölçüldü ve var; sahne içi hareket–kuyruk ilişkisi
ölçüldü ve **bulunamadı**. Kuyruğu açıklayan değişken hareket değil,
anahtar kare aralığı.

## K5 — HandBrake aynı yerde ne yapıyor

`tools/kuyruk-anatomisi/k5b-ayrisan.py`. Her kare için iki dosyanın paket
boyutu yan yana konur. İki dosyanın kare başına bit seviyesi farklı olduğu
için mutlak bayt karşılaştırması yanıltıcı olurdu; **her sayı kendi
dosyasının aynı evrendeki ortalamasına bölünmüş** olarak da veriliyor
(`×` sütunu). Bu, seviye farkını çıkarıp **dağıtım biçimini** karşılaştırır —
sorunun sorduğu şey budur.

Evren: iki dosyada da inter olan ve **`auto`'da gerçekten kodlanan** kareler
(1811 kare). K4'ün düzeltmesi gereği `show_existing_frame` konumları dışarıda;
anahtar kareler de dışarıda. Kalite verisi her iki tarafta kendi dosyasının
kilitli ölçümünden; `auto` yeniden üretilen koşum.

| küme | kare | `auto` bayt | `auto` × | `auto` VMAF | `hb2` bayt | `hb2` × | `hb2` VMAF |
|---|---|---|---|---|---|---|---|
| evrenin tamamı | 1811 | 1191 | 1,00 | 95,480 | 3637 | 1,00 | 95,744 |
| **yalnız `auto` kötü** | 159 | 1193 | **1,00** | 94,157 | 7163 | **1,97** | 95,556 |
| **yalnız `hb2` kötü** | 149 | 1774 | **1,49** | 95,262 | 1804 | **0,50** | 95,330 |
| ortak kötü | 29 | 2939 | 2,47 | 94,071 | 4283 | 1,18 | 95,284 |
| ikisi de iyi | 1474 | 1097 | 0,92 | 95,673 | 3429 | 0,94 | 95,815 |

Tablo simetrik ve tek bir şey söylüyor:

- `auto`'nun **yalnız kendi** düştüğü 159 karede kendi ortalamasının
  **1,00 katını** harcıyor — yani o kareleri özel olarak hiç görmüyor.
  HandBrake aynı karelerde kendi ortalamasının **1,97 katını** harcıyor
  ve **düşmüyor** (95,556).
- Aynanın öbür yüzü: HandBrake'in **yalnız kendi** düştüğü 149 karede
  kendi ortalamasının **0,50 katını** harcıyor; `auto` aynı karelerde
  **1,49 katını** harcıyor ve **düşmüyor** (95,262).
- İkisinin de düştüğü 29 kare gerçekten zor: `auto` 2,47×, HandBrake
  1,18× harcıyor, ikisi de kurtaramıyor. Evrendeki kötü karelerin **%8,6**'sı.

**Kuyruk bir yetenek sorunu değil, bir dağıtım sorunu.** Her iki kodlayıcının
kötü kareleri, tam olarak kendi hız denetiminin öbürünün yargısına göre
az beslediği karelerdir. Bu K2'nin sonucunu ikinci bir yoldan doğruluyor:
kusur kodlayıcıya ait ve düzeltilebilir.

**Dayanıklılık:** aynı tablo tüm 3589 inter paket üzerinde de kuruldu
(`show_existing_frame`'ler dahil, yani K4 düzeltmesi öncesi hali). Oranlar
1,05 ↔ 1,89 ve 1,48 ↔ 0,51 çıkıyor. İki evren de aynı simetriyi veriyor;
sonuç bu seçime bağlı değil.

### Kuyruğu kapatmanın ölçülen maliyeti

`auto`'nun yalnız kendi düştüğü 159 kodlanan karenin her birine HandBrake'in
oranını (1,969×) vermek, kare başına 1193 B yerine 2345 B demek. Fark
159 × 1151 B = **179 KB**, video bütçesinin **%1,37**'si. Bu bit başka bir
yerden gelmek zorunda; nereden geleceği K6'nın konusu.

## K6 — Öneri ve maliyeti

Sözleşme uygulama istemiyor; bulguyu, yerini ve bedelini istiyor.

### Önce: ölçülen kuyruk artık `main`de olmayan bir motora ait

K4 anahtar kare aralığını suçlu gösterince kodun bugünkü hali okundu.
`src/VidShrink.Core/FfmpegArguments.cs:216-253`: sabit `-g` **kaldırılmış**.
Yerinde bir aralık var — taban 1 s, tavan sahne haritasının **ortanca** sahne
uzunluğu, `[KeyframeCeilingMinSeconds = 5,0 ; KeyframeCeilingMaxSeconds = 10,0]`
arasına kırpılıyor, harita yoksa 10 s.

Bunu getiren commit **`8ea80c4`** ("T98 K1+K2: sabit -g yerine anahtar kare
aralığı"). T111'in ölçüm tabanı **`3688336`**. `merge-base --is-ancestor` ile
doğrulandı: `8ea80c4` **`3688336`'nın atası değil**, ama T122 tabanının
(`59eac70`) atası. Yani T111'in arşivindeki her `-g 120` koşumu **T98'den
önceki motoru** ölçüyor. T111 bunu arşivinin `OKU.md`'sinde zaten damgalamıştı;
T122 o damganın **kuyruğun tamamını** kapsadığını gösteriyor.

60 fps'te tavan `[5 s, 10 s]` demek `-g` ∈ `[300, 600]` demek. İkisi de ölçüldü.

### Ölçülen süpürme

`tools/kuyruk-anatomisi/k6-ozet.py` tabloyu tek komutta üretir. Beşi de
aynı kaynağa (`parca-2.mkv`), aynı ölçere (kilitli VMAF-NEG), aynı
kodlayıcı ayarlarına — yalnız `-g` ve `-b:v` değişiyor. `AK` = anahtar kare.

| koşum | `-g` | `-b:v` | boyut | AK | AK bütçe payı | kodlanan inter B/kare | ort | **p10** | en düşük kare |
|---|---|---|---|---|---|---|---|---|---|
| `auto` (T111 ayarı) | 120 | 2026k | 14 450 295 | 31 | **%83,9** | 1 190 | 95,486 | **94,497** | 91,785 |
| `auto-g300` | 300 | 2026k | 11 788 146 | 13 | %64,2 | 2 120 | 95,821 | 95,137 | 94,230 |
| `auto-g600` | 600 | 2026k | 12 172 458 | 7 | %38,0 | 3 800 | 95,944 | 95,382 | 94,583 |
| **`auto-g600-boyutesit`** | 600 | **2405k** | **14 646 149** | 7 | %33,8 | **4 965** | **96,015** | **95,553** | **94,790** |
| `uzman-hb2` (HandBrake) | — | 1900 | 15 743 067 | 7 | %18,0 | 3 327 | 95,743 | 95,380 | 94,156 |

Son sütun K4'ün düzeltmesine göre: SVT-AV1 koşumlarının dördü de tam 1812
inter kare kodluyor, geri kalanı `show_existing_frame`. HandBrake'te
kodlanan kare sayısı görüntü kare sayısına eşit.

`-b:v` 2026k'da `-g` büyüdükçe dosya **küçülüyor** (14,45 MB → 12,17 MB):
anahtar kareler bütçeyi dolduruyordu, kalkınca SVT-AV1 boşalan payı inter
karelere aktarmıyor, daha küçük dosya teslim ediyor. Bu yüzden son satır
`-b:v`'yi `2405k`'ya çekip boyutu eşitliyor (14 646 149 ↔ 14 450 295,
**%+1,36**), ve karşılaştırma orada yapılıyor.

**Eşit boyutta, yalnız `-g` 120 → 600:**

| | ortalama | p10 | en düşük kare |
|---|---|---|---|
| `auto` (g=120) | 95,486 | 94,497 | 91,785 |
| `auto-g600-boyutesit` | 96,015 | 95,553 | 94,790 |
| **fark** | **+0,529** | **+1,056** | **+3,005** |

**Aynı koşum HandBrake'e karşı** (%7,0 **daha küçük** dosyayla):

| | ortalama | p10 | en düşük kare |
|---|---|---|---|
| `uzman-hb2` | 95,743 | 95,380 | 94,156 |
| `auto-g600-boyutesit` | 96,015 | 95,553 | 94,790 |
| **fark** | **+0,272** | **+0,173** | **+0,634** |

T111'in kapatamadığı p10 açığı burada kapanıyor ve işaret değiştiriyor.
Nedeni K4'te ölçülen tek şey: anahtar kare başına maliyet iki kodlayıcıda
neredeyse aynı (362 KB ↔ 376 KB), fark sayıdadır, ve `-g 120` 60 fps'te
bütçenin %83,9'unu 31 kareye veriyordu.

### Öneri

**Öneri 1 — ölçümü yenile, tek satır bile değiştirme.**
`docs/olcumler/auto-mod.md`'nin K3 tablosu ve "HandBrake açığı" satırları
emekli bir motoru anlatıyor. Yapılacak iş: T111 düzeneğini
(`tools/auto-mod-olcumu/t111-uret.sh` + `t111-olc.sh`) bugünkü `main`de
yeniden koşmak.
*Yer:* yeni sözleşme, `docs/olcumler/auto-mod.md`.
*Beklenen kazanç:* yukarıdaki tabloya göre p10'da +1,0 mertebesi; sayı
zaten var, eksik olan onun `auto` yolundan da geldiğinin gösterilmesi.
*Maliyet:* 16 kodlama + 32 ölçüm, T111'de yaklaşık iki saat makine.
*Risk:* düşük — kod değişmiyor.

**Öneri 2 — tavanı bit bütçesine de baktır.**
`FfmpegArguments.KeyframeCeilingSeconds` (`:233-253`) tavanı **yalnız içerikten**
okuyor: ortanca sahne uzunluğu. Bu ölçüm gösteriyor ki kuyruğu belirleyen şey
sahne uzunluğu değil, **anahtar karelerin bütçe payı** — ve o pay hedef boyuta
bağlı, içeriğe değil. Aynı klipte pay `-g 120`'de %83,9, `-g 600`'de %33,8.
*Değişecek yer:* `KeyframeCeilingSeconds`'a hedef bit hızı ve kare başına
tahmini intra maliyeti girdi olarak eklenir; tavan, öngörülen pay bir eşiğin
altına inene kadar yükseltilir. İki ölçülü çapa var: %18,0 (HandBrake) ve
%33,8 (`auto-g600-boyutesit`) sağlıklı, %83,9 çöküyor. **Eşiğin kendisi
ölçülmedi** — bu ölçüm üç nokta veriyor, eğri vermiyor.
*Beklenen kazanç:* süpürmede kırpmanın tabanı (`-g 300`) p10 95,137,
tavanı (`-g 600`) 95,382 verdi — **aynı `-b:v`**, ama `-g 600` dosyası %3,3
daha büyük. Kırpmanın iki ucunu **eşit boyutta** karşılaştıran bir koşum
yapılmadı; bu 0,245 puanın ne kadarının `-g`'den ne kadarının boyuttan
geldiği **ölçülmedi**. Bu klipte hangi ucun seçildiği de ölçülmedi (K7).
Kazanç yönü belli, büyüklüğü değil.
*Maliyet:* `KeyframeCeilingSeconds` imzası değişir; `FfmpegArgumentsTests`
içindeki tavan testleri yeniden temellendirilir. Bir sözleşmelik iş.
*Risk:* **arama süresi.** T98 kendi ölçümünde 5,62 s tavanla 154,9 ms,
10 s tavanla 202,6 ms arama maliyeti ölçmüştü (`FfmpegArguments.cs:139-141`).
Tavanı yükseltmek aramayı pahalılaştırır ve bu kullanıcıya dokunan bir
takas; kalite kazancıyla birlikte tartılması gerekir.

**Öneri 3 — ölçüm araçları AV1'de paket sayısını kare sayısı saymasın.**
K4 `auto`'nun inter paketlerinin %49,6'sının 3 baytlık `show_existing_frame`
olduğunu ölçtü: SVT-AV1 dört koşumun dördünde de tam 1812 kare kodluyor,
geri kalanını yalnız gösteriyor. Kare başına bit veren her araç bu klipte
`auto` için 602 B okuyor; gerçek kodlanan kare maliyeti **1190 B**, yani
**%98 sapma**. Bu bir kodlayıcı kusuru değil, ölçme kusuru.
*Değişecek yer:* kare başına bit hesaplayan ölçüm/rapor yolları — bu
sözleşmede `tools/kuyruk-anatomisi/bitler.py` düzeltildi, ama aynı hata
kare başına bit okuyan başka bir yerde varsa aranmalı. Üretim kodunda böyle
bir yol olup olmadığı **aranmadı**.
*Beklenen kazanç:* doğrudan kalite kazancı yok; yanlış sayı üretmeyi
bırakmak. `PlanCalculator`'a etkisi **ölçülmedi**.
*Maliyet:* arama + düzeltme, yarım sözleşme.
*Risk:* düşük — ölçüm tarafı.

**Üçünün de uygulaması bu sözleşmede yok.**

## Sahne sınırlarının üç yoldan doğrulanması

K3'ün tamamı iki kesimin (kare 1700 ve 3411) doğru yerde olmasına dayanıyor.
Üç bağımsız kaynak aynı iki kareyi veriyor:

1. **T105 haritası + aritmetik.** `sahneler.csv` sınırları 477,933 ve 506,450;
   parçanın kaynaktaki başlangıcı 449,600 çıkarılınca 28,333 ve 56,850 s.
2. **T111'in hizalama koşumları.** `-force_key_frames 28.353,56.870` — aradaki
   +0,020 s tam olarak `parca-2`'nin kap içi video başlangıç kayması.
3. **HandBrake'in kendi sahne kesimi.** Yedi anahtar karesinden ikisi
   ölçülerek **1700** ve **3411** karelerinde bulundu; kalan beşi 600'lük
   ızgarada. SVT-AV1 de aynı yerlere fazladan intra koydu (1697, 3409).

Üçü de aynı yeri gösteriyor, biri diğerinden türetilmedi.

## K7 — Ölçülmedi

- **Hareketin kendisi değil, bir vekili ölçüldü.** Ölçülen şey ardışık iki
  kaynak karesi arasındaki ortalama mutlak parlaklık farkıdır. Hareket
  vektörü, blok başına hareket, panning/zoom ayrımı **ölçülmedi**; kamera
  hareketi ile nesne hareketi bu ölçüde ayrışmıyor.
- **Kare başına karmaşıklık.** Elde olan karmaşıklık T105'in **sahne
  başına** `ComplexityProbe` sayısıdır (S13 0,07119 / S14 0,12892 /
  S15 0,08546). `ComplexityProbe` kare başına koşturulmadı.
- **`show_existing_frame` düzeninin mekanizması.** Dört SVT-AV1 koşumunun
  dördünde de tam 1812 inter kare kodlandığı ölçüldü; bunun AV1
  piramidinde hangi yapıdan çıktığı bit akışı çözülerek **doğrulanmadı**.
- **Bugünkü motorun bu klipte hangi tavanı seçtiği.** Kural okundu
  (`FfmpegArguments.cs:233-253`) ve tavanın `[5 s, 10 s]` = `-g [300, 600]`
  aralığına düştüğü biliniyor, ama uygulamanın kendi `SceneMap`'i
  `parca-2` üzerinde **koşturulmadı**. K6'nın süpürmesi aralığın iki ucunu
  da ölçtüğü için sonuç bu belirsizliğe dayanıklı; yine de hangi ucun
  seçildiği ölçülmedi.
- **Üretim komut satırının kendisi.** Süpürme yalın `-g N` ile koştu;
  üretim `-g N -svtav1-params keyint=N:scd=1` yazıyor. Açık `keyint`/`scd`
  yazmanın farkı ölçülmedi.
- **Anahtar kare bütçe payının eşiği.** Üç nokta ölçüldü (%18,0 / %33,8 /
  %83,9); aradaki eğri ölçülmedi, "şu payın altında kal" diyecek bir sayı
  bu sayfadan çıkmaz.
- **Arşivdeki `auto`'nun bütçe payı.** O dosya T111 temizliğinde silindi;
  %83,9 **yeniden üretilen** koşumdan. Anahtar kare sayısı (31) `-g 120` ve
  3624 kareden zaten belli, ama payın kendisi eski dosyada ölçülmedi.
- **SVT-AV1'in yeniden üretilebilirliği.** `auto-g300` 11 788 146 B teslim
  etti; T111'in aynı ayarlarla koşan `y1-g300-izgara`'sı 11 809 579 B idi
  — **%0,18** fark. HandBrake tarafı **birebir** yeniden üretildi
  (15 743 067 B, kare kare aynı VMAF). SVT-AV1'deki bu küçük kararsızlığın
  nedeni ölçülmedi.
- **Süreler.** Makine paylaşımlıydı; bu sözleşme koşarken aynı makinede
  başka ajanların ölçümleri vardı. Hiçbir süre sayısı üretilmedi.
- **Tek klip.** Her sayı `parca-2` üzerinde. `parca-1` ve `parca-3` ölçülmedi;
  bulguların genellenmesi ölçülmedi.
- **Üretim kodunda kare başına bit okuyan bir yol olup olmadığı.** K4'ün
  düzeltmesi bu sözleşmenin kendi betiğine uygulandı; aynı hatanın
  `src/` altında bir karşılığı olup olmadığı **aranmadı** (Öneri 3).
- **Hiçbir üretim kodu değişmedi.** K6 üç öneri veriyor, üçü de uygulanmadı.

## T122 — ne bulundu

1. Kuyruk tek bir zor sahne değil: `auto`'da 25, HandBrake'te 39 ayrı küme.
2. **İki çıktının kötü kareleri aynı yerde değil.** Kare düzeyinde kesişim
   68/363, şansın 1,87 katı, olabileceğin %9,7'si. Kontrol: aynı
   yapılandırmanın iki ayrı kodlaması **%87,3** kesişiyor. Kuyruğun yerini
   kodlayıcı belirliyor.
3. Saniye ölçeğinde ortak bir zemin var (normalize örtüşme 0,588): kaynak
   arenayı seçiyor, kodlayıcı kurbanı seçiyor.
4. Kuyruk sahne geçişinde değil **sahne içinde**: S13'te (klibin %46,9'u)
   üç koşumun da sıfır kötü karesi var, GOP fazı 0–9'da sıfır, 110–119'da
   2,17 kat.
5. **Hareket kuyruğu açıklamıyor.** Sahne içinde hareket onda birlikleri
   düz: `auto` 0,75–1,24. Aynı sahnede GOP fazı 0,00–2,17. "Yüksek
   hareketli sahnede kötü" cümlesi ölçüyle **kurulamadı**.
6. Nedeni ölçüldü: `-g 120` bütçenin **%83,9'unu 31 anahtar kareye** veriyor,
   kodlanan inter kareye 1190 bayt kalıyor. HandBrake 7 anahtar kareyle
   %18,0 harcayıp inter kareye 3327 bayt bırakıyor.
7. Ayrışan karelerde her iki kodlayıcı da öbürünün az beslediği yerde
   düşüyor (1,97× ↔ 1,00× ve 1,49× ↔ 0,50×). Kuyruk yetenek değil
   **dağıtım** sorunu.
8. **Ölçme kusuru bulundu ve düzeltildi:** AV1'de paket boyutu kare maliyeti
   değil. `auto`'nun inter paketlerinin %49,6'sı 3 baytlık
   `show_existing_frame`; kare başına bit 602 yerine **1190 B**. İlk okuyuşta
   çıkan "kaynak 30 fps" sonucu üç ölçümle **çürütüldü**.
9. Bu kuyruk **`main`de olmayan bir motora ait**: sabit `-g` T98'de
   (`8ea80c4`) kaldırıldı, T111'in tabanı ondan önce. Aralığın iki ucu da
   ölçüldü; boyut eşitlenince `-g 600` `auto`'yu p10'da **+1,056**
   iyileştiriyor ve HandBrake'i %7,0 küçük dosyayla **+0,173** geçiyor.
