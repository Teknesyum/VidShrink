# Siyah kenar kırpma — ölçüm

T134. `docs/inceleme/handbrake-motoru.md` § 6.5, madde 5: siyah kenar kırpma
VidShrink'te hiç yok, HandBrake varsayılan olarak yapıyor. Bugüne kadarki tüm
ölçümlerimiz kenarsız kaynaklarla yapıldı; letterbox'lı kaynak sınıfı hakkında
elimizde sıfır veri vardı. Bu belge o veriyi üretiyor.

## Hüküm

<!-- BETIK-HUKUM-BASLANGIC -->
**Kirpma degmez.** Ortalama p10 kazanci 0,083 puan (n=4 letterbox'li kaynak, kaynak basina 1200 kare, VMAF-NEG p10 aktif goruntu alaninda, 2000k 2 gecis, teslim boyutu esitlenmis), K4'un +0,30 tabaninin altinda; 2/4 kaynakta kazanc pozitif.

Kaynak basina kazanc (2000k, p10): KA 0,102 KB -0,048 KC -0,130 KD 0,408.

Kazanclar **gercek bant sinirina** gore olculdu, cropdetect'in buldugu sinira gore degil. Varsayilan `limit=24` ile on kareye yayilmis yoklama dort letterbox'li kaynagin **3 tanesinde** gercek siniri buluyor (KA, KB, KC); kalan 1 kaynakta kirpma varsayilan ayarla hic tetiklenmez, yani oradaki kazanc erisilebilir degil, tavandir.

Yalniz erisilebilir kaynaklara bakildiginda (KA, KB, KC) ortalama kazanc -0,025 puan; hukmu pozitife tasiyan tek kaynak varsayilan ayarla bulunamayan KD.

K4'un iki vetosu da tetiklenmedi. Yanlis kirpma vetosu varsayilan `limit=24`'te degerlendirildi; limit taramasi (K2) daha yuksek degerlerde kenarsiz kaynaklarin kirpildigini gosteriyor, yani veto ayardan bagimsiz degil.
<!-- BETIK-HUKUM-BITIS -->

Bu paragraf elle yazılmaz. `tools/siyah-kenar/oz.py` tabloları okuyup cümleyi
üretir ve iki işaretin arasına yazar.

## K4 — Karar eşiği (ölçümden önce yazıldı)

Bu bölüm K3 koşulmadan önce commit edildi. Sonradan seçilen eşik kanıt değildir.

**Karar ölçüsü:** VMAF-NEG p10 — kare puanlarının 10. yüzdeliği — **aktif görüntü
alanında** ölçülür, teslim edilen dosya boyutu eşitlenmiş iki kol arasında.
Kırpmalı kolun p10'u eksi kırpmasız kolun p10'u = **kazanç**.

| Karar | Koşul |
|---|---|
| **Değer** | Dört letterbox'lı kaynağın kazanç ortalaması **≥ +1,00** ve en az üçünde kazanç pozitif |
| **Şu sınıfta değer** | Ortalama kazanç **+0,30 ile +1,00** arasında, ya da kazanç bant yüzdesiyle ayrışıyor. Sınıf sınırını veri çizer: kesim noktası kazancın +1,00'ı geçtiği en düşük bant yüzdesidir |
| **Değmez** | Ortalama kazanç **< +0,30** |

**Eşikten bağımsız iki veto.** Hüküm "değer" çıksa bile bunlar sağlanmazsa
kırpma **varsayılan açık gelemez**:

1. **Yanlış kırpma vetosu.** K5(a)'daki iki kenarsız kaynağın herhangi birinde
   `cropdetect` sıfırdan farklı bir kırpma öneriyorsa. Bir piksel fazla kırpmak
   görüntüyü keser; bu, kazanılan puanla telafi edilmez.
2. **Yoklama maliyeti vetosu.** `cropdetect` yoklaması klip başına **2,0
   saniyeyi** aşıyorsa. Bu maliyet doğrudan kullanıcının bekleme süresine biner.

Vetolar tetiklenirse üst sınır "kullanıcı onaylı seçenek"tir, varsayılan değil.

**Hangi hedef boyutta.** Üç bitrate noktası koşulur: **1000k, 2000k, 4000k**
(1920x1080@60, libx264, 2 geçiş, preset slow, sessiz). **Karar noktası
2000k'dir**; 1000k ve 4000k duyarlılık satırıdır ve hükmü değiştirmez, yalnız
hükmün bitrate'e ne kadar bağlı olduğunu gösterir. Bu satır da K3
koşulmadan önce yazıldı.

**+1,00 ve +0,30 nereden geliyor.** +0,30 alt sınırı, uygulama maliyetinin
karşılığı: kırpma yeni bir yoklama geçişi, yeni bir plan alanı ve yeni bir
hata sınıfı (yanlış kırpma) getiriyor; bundan küçük bir kazanç bu bedeli
ödemez. +1,00 üst sınırı, bu depoda bir satırda tek başına anlamlı sayılan
fark mertebesi (T125'te p10'da +0,86 "puanı değişti" diye raporlandı).

## K3 — Karşılaştırmayı adil kılan yöntem (ölçmeden önce yazıldı)

Kırpılmış çıktı 1920x804, kırpmasız çıktı 1920x1080. VMAF iki girdiyi kare kare
hizalar ve aynı çerçeveyi bekler; farklı çerçeveli iki dosyanın puanı doğrudan
karşılaştırılamaz. İki yöntem koşuldu — aynı ölçüme iki ayrı soru soruyorlar.

**A yöntemi — yalnız aktif görüntü alanında puanlama (birincil, karar buna bağlı).**
Referans, kaynak dosyanın aktif alanı: `crop=1920:H:0:Y`. Kırpmasız kolun çıktısı
aynı `crop` ile aktif alana indirilir; kırpmalı kolun çıktısı zaten o çerçevededir.
Her iki kol da 1920xH karede ve aynı referansa karşı puanlanır.

- **Tuttuğu:** izleyicinin gerçekten baktığı piksellerde kalite farkı. "Bit banda
  mı görüntüye mi gidiyor" sorusunun doğrudan cevabı budur.
- **Kaçırdığı:** bandın içinde olan hiçbir şey. Kırpmalı kol bandı hiç taşımıyor;
  A yöntemi bunu ne ödüllendirir ne cezalandırır. KD'de bant gürültülüdür,
  kırpma o gürültüyü tamamen atar — A yöntemi bu farkı görmez. Kırpmanın
  oynatma tarafındaki sonucunu (dosyanın en-boy oranı artık 2,39:1, oynatıcı
  kendi siyahını koyuyor) da ölçmez.

**B yöntemi — geri doldurup tam karede puanlama (ikincil).**
Kırpmalı kolun çıktısı `pad=1920:1080:0:Y:black` ile özgün çerçeveye döndürülür;
her iki kol da tam 1920x1080 karede, referans olarak kaynağın kendisiyle
puanlanır.

- **Tuttuğu:** kırpmanın bant içeriğini kaybetmesinin bedeli. Kaynağın bandı tam
  siyah değilse geri doldurma o farkı ceza olarak yazar. Bant sahne içinde
  kalkıyorsa gerçek görüntüyü kesmenin bedelini de yazar — K5(b)'nin ölçüsü
  budur.
- **Kaçırdığı:** tam siyah bantta iki kol da bant bölgesinde neredeyse kusursuz
  puan alır ve puanın büyük kısmı karenin kolay dörtte birinden gelir; bu,
  aktif alandaki gerçek farkı **seyreltir**. Karar bu yüzden A'ya bağlandı.

**Ölçünün kendi künyesi.** Model `vmaf_v0.6.1neg`, `n_threads=8`, ölçek
değişikliği yok — iki kol da kendi doğal çözünürlüğünde puanlanır. A yönteminde
bu çözünürlük 1920xH'dir (örn. 1920x804), yani modelin varsaydığı 1080p izleme
düzeninden biraz farklıdır; ama **iki kol da aynı çerçevede** olduğu için
aradaki fark bundan etkilenmez. B yönteminde iki kol da 1920x1080'dedir.

**İkisinin de kaçırdığı.** VMAF-NEG algısal bir vekildir. Kırpmanın asıl
kullanıcı faydası — aynı ekranda daha büyük görüntü, daha az ölü piksel —
oynatma tarafındadır ve bu belgede ölçülmüyor.

### İki kolun aynı kaynaktan aynı aralıkta olduğunun kanıtı

Bu depoda ölçüm parçalarının sessizce farklı çıktığı görüldü. Burada o hata
yapısal olarak imkânsız kılındı: **iki kol tek bir kaynak dosyayı okur.**
Kırpmasız kol dosyayı olduğu gibi, kırpmalı kol aynı dosyaya `crop` filtresi
ekleyerek alır. Ayrı kesim, ayrı `-ss`, ayrı ara kodlama yok.

<!-- BETIK-KANIT-BASLANGIC -->
| Kaynak | Kaynak dosya sha256 (ilk 16) | Kare | PTS ilk / son | Aktif alan framemd5 ozeti (ilk 16) | Tekrar esit mi |
|---|---|---|---|---|---|
| KA 2,39:1 duz bant | `f4d63b722db3927e` | 1200 | 0 / 1199 | `2e8aee8dcd5dd7ed` | evet |
| KB 2,20:1 duz bant | `8acbbf63e9173ee3` | 1200 | 0 / 1199 | `d9d7a2b81bf9239c` | evet |
| KC 1,85:1 duz bant | `72c40a33e9679992` | 1200 | 0 / 1199 | `3a067325d44c7db8` | evet |
| KD 2,39:1 gurultulu asimetrik bant | `756c8bfe8aa8e311` | 1200 | 0 / 1199 | `9e0217dc9cd428da` | evet |
| NA kenarsiz (parca-1) | `f9d1009f9bd21447` | 1200 | 0 / 1199 | `b9ddd380b2cbd791` | evet |
| NB kenarsiz (parca-2) | `d71ee789c0cf4ada` | 1200 | 0 / 1199 | `72f96a2f58b1bf44` | evet |
| VD bant genisligi sahne icinde degisiyor | `bd292496252557ed` | 1200 | 0 / 1199 | `dee486bb34619999` | evet |

Her satirda tek bir kaynak dosya var ve iki kol da onu okuyor; kirpmali kolun gordugu aktif alanin kare kare md5 dizisi iki bagimsiz cozumde ayni cikti. Kaynak dosyalarin uretimi `tools/siyah-kenar/kaynak.sh`, kanit `tools/siyah-kenar/kanit.py`.
<!-- BETIK-KANIT-BITIS -->

## K1 — Kaynak sınıfı

Paylaşılan havuzda (`.calisma/kaynak/`) letterbox'lı kaynak yok: havuzdaki üç
parça da 1920x1080, kenarsız, 60 fps, HEVC 10 bit HDR PQ. Kaynak sınıfı bu
yüzden havuzdan **üretildi**; havuza yazılmadı, dokunulmadı.

**Üretim yöntemi** (`tools/siyah-kenar/kaynak.sh`): her kaynak, havuzdaki bir
parçanın **20.–40. saniyesinden** alınır. Görüntü önce hedef en-boy oranının
yüksekliğine **kırpılır** (`crop=1920:H:0:(1080-H)/2`) — yani aktif alan gerçek
içeriktir, dikey olarak sıkıştırılmış değil — sonra `pad` ile 1920x1080 kutuya
siyah bantla geri konur. Ara kodlama libx264 CRF 12, preset veryfast,
yuv420p10le, keyint 120, sahne kesme kapalı, ses yok; renk künyesi kaynağınkiyle
aynı (bt2020nc / smpte2084 / pc).

Kaynağa özel işler:

- **KA** — 2,39:1. Başına 1,5 saniyelik siyahtan açılma (`fade=t=in`) konuldu.
  Gerçek filmlerde olan bu açılma, K2'nin örnekleme yeri sorusunun sınama
  düzeneğidir.
- **KD** — 2,39:1, **gerçek dünya kusuru taşıyan kaynak.** İki kusuru birden
  taşır: (1) bant tam siyah değil — #0c0c0c zemine 8 bit alanında üretilmiş,
  zamanla değişen gürültü bindirildi (`noise=alls=10:allf=t+u`), sonra 10 bite
  çevrildi; (2) bant genişliği simetrik değil, üstte 120 altta 156 piksel.
  Aktif alanı KA ile aynı içerikten geldiği için KA–KD çifti yalnız bandın
  kusurunu yalıtır.

  **Bu kaynak bir kez yeniden üretildi.** İlk üretimde gürültü doğrudan 10 bit
  düzlemde uygulanmıştı; `noise` filtresinin genliği 10 bit ölçekte
  değerlendirildiği için bant neredeyse siyah kaldı (YAVG 6,8/1023). Gürültü
  8 bit alanında üretilip 10 bite çevrilerek düzeltildi. KD'nin bütün
  kodlamaları ve yoklamaları yeni kaynakla baştan koşuldu; bu belgedeki KD
  sayıları yalnız yeni kaynaktandır. Eşik (K4) bu düzeltmeden önce
  commit'lenmişti ve değişmedi.
- **VD** — ilk 10 saniyesi 2,39:1 letterbox, son 10 saniyesi tam kare. Bant
  genişliğinin sahne içinde değiştiği kaynak; K5(b) bunu kullanır.
- **NA, NB** — kenarsız denetim kaynakları. Aynı zaman aralığı, aynı ara
  kodlama, hiçbir geometri işlemi yok. K5(a) bunları kullanır.
- **KE** — yalnız `limit` taraması için, ağır gürültülü bantla üretildi
  (zemin #181818, 8 bit alanında `noise=alls=22`). K3 ızgarasına girmez.

<!-- BETIK-K1-BASLANGIC -->
| Kaynak | Cozunurluk | Gercek goruntu alani | Bant yuzdesi | Sure / kare | Kodek |
|---|---|---|---|---|---|
| KA 2,39:1 duz bant | 1920x1080 | 1920x804 | 25,56% | 20,0 sn / 1200 kare | h264 yuv420p10le |
| KB 2,20:1 duz bant | 1920x1080 | 1920x872 | 19,26% | 20,0 sn / 1200 kare | h264 yuv420p10le |
| KC 1,85:1 duz bant | 1920x1080 | 1920x1036 | 4,07% | 20,0 sn / 1200 kare | h264 yuv420p10le |
| KD 2,39:1 gurultulu asimetrik bant | 1920x1080 | 1920x804 | 25,56% | 20,0 sn / 1200 kare | h264 yuv420p10le |
| NA kenarsiz (parca-1) | 1920x1080 | 1920x1080 | 0,00% | 20,0 sn / 1200 kare | h264 yuv420p10le |
| NB kenarsiz (parca-2) | 1920x1080 | 1920x1080 | 0,00% | 20,0 sn / 1200 kare | h264 yuv420p10le |
| VD bant genisligi sahne icinde degisiyor | 1920x1080 | 1920x804 | 25,56% | 20,0 sn / 1200 kare | h264 yuv420p10le |

| Kaynak | Ust bant px | Alt bant px | Bant YAVG (min/ort/maks) | Aktif alan YAVG (min/ort/maks) |
|---|---|---|---|---|
| KA 2,39:1 duz bant | 138 | 138 | 0,01 / 2,46 / 64,00 | 64,00 / 346,37 / 383,38 |
| KB 2,20:1 duz bant | 104 | 104 | 0,00 / 0,00 / 0,00 | 256,92 / 324,01 / 372,55 |
| KC 1,85:1 duz bant | 22 | 22 | 0,14 / 0,32 / 0,96 | 229,34 / 314,43 / 357,08 |
| KD 2,39:1 gurultulu asimetrik bant | 120 | 156 | 44,59 / 44,69 / 44,77 | 345,95 / 359,31 / 384,65 |
| VD bant genisligi sahne icinde degisiyor | 138 | 138 | 0,02 / 171,60 / 509,04 | 226,60 / 309,71 / 350,74 |
<!-- BETIK-K1-BITIS -->

## K2 — Tespit güvenilirliği

<!-- BETIK-K2-BASLANGIC -->
Sureler bu tabloda yok: ilk kosumda makine mesguldu ve sayilar kararsizdi. Sure olcumu asagida, bos makinede tekrarlanmis haliyle verilir.

| Kaynak | Gercek sinir | cropdetect t=10sn | Fark (px) |
|---|---|---|---|
| KA 2,39:1 duz bant | 1920:804:0:138 | 1920:804:0:138 | +0 yatay / +0 dikey |
| KB 2,20:1 duz bant | 1920:872:0:104 | 1920:872:0:104 | +0 yatay / +0 dikey |
| KC 1,85:1 duz bant | 1920:1036:0:22 | 1920:1036:0:22 | +0 yatay / +0 dikey |
| KD 2,39:1 gurultulu asimetrik bant | 1920:804:0:120 | 1920:1080:0:0 | +0 yatay / +276 dikey |
| NA kenarsiz (parca-1) | 1920:1080:0:0 | 1920:1080:0:0 | +0 yatay / +0 dikey |
| NB kenarsiz (parca-2) | 1920:1080:0:0 | 1920:1080:0:0 | +0 yatay / +0 dikey |
| VD bant genisligi sahne icinde degisiyor | 1920:1080:0:0 | 1920:1080:0:0 | +0 yatay / +0 dikey |

| Kaynak | t=0 | t=5 | t=10 | t=15 | 10 kare yayilmis (birlesim) | tam klip |
|---|---|---|---|---|---|---|
| KA 2,39:1 duz bant | 1920:1080:0:0 | 1920:804:0:138 | 1920:804:0:138 | 1920:804:0:138 | 1920:1080:0:0 | 1920:1080:0:0 |
| KB 2,20:1 duz bant | 1920:872:0:104 | 1920:872:0:104 | 1920:872:0:104 | 1920:872:0:104 | 1920:872:0:104 | 1920:872:0:104 |
| KC 1,85:1 duz bant | 1920:1036:0:22 | 1920:1036:0:22 | 1920:1036:0:22 | 1920:1036:0:22 | 1920:1036:0:22 | 1920:1036:0:22 |
| KD 2,39:1 gurultulu asimetrik bant | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 |
| NA kenarsiz (parca-1) | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 |
| NB kenarsiz (parca-2) | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 |
| VD bant genisligi sahne icinde degisiyor | 1920:804:0:138 | 1920:804:0:138 | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 |
<!-- BETIK-K2-BITIS -->

### Tekil karelerin birleştirilmesi

10 tek kareyi ayrı ayrı yoklayıp sonuçları birleştirmek (HandBrake'in önizleme
taraması bu şekle benziyor) tek bir tamamen siyah kareye karşı kırılgandır:
siyah karede `cropdetect` tam kareyi döndürür, birleşim de kırpmayı iptal eder.
Aynı on kareden **en sık geçen kutuyu** almak (eşitlikte geniş olanı seçerek)
o kareyi yutar. İkisi de aşağıda.

<!-- BETIK-K2B-BASLANGIC -->
| Kaynak | Gercek sinir | 10 tekil karenin birlesimi | 10 tekil karenin modu (esitlikte genis olan) | Tam kare donen kare sayisi |
|---|---|---|---|---|
| KA 2,39:1 duz bant | 1920:804:0:138 | 1920:1080:0:0 | 1920:804:0:138 | 1/10 |
| KB 2,20:1 duz bant | 1920:872:0:104 | 1920:872:0:104 | 1920:872:0:104 | 0/10 |
| KC 1,85:1 duz bant | 1920:1036:0:22 | 1920:1036:0:22 | 1920:1036:0:22 | 0/10 |
| KD 2,39:1 gurultulu asimetrik bant | 1920:804:0:120 | 1920:1080:0:0 | 1920:1080:0:0 | 10/10 |
| NA kenarsiz (parca-1) | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | 10/10 |
| NB kenarsiz (parca-2) | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | 10/10 |
| VD bant genisligi sahne icinde degisiyor | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | 5/10 |
<!-- BETIK-K2B-BITIS -->

### `limit` eşiği taraması

`cropdetect`'in tek ayarı `limit`. Düşük tutmak gürültülü bandı bant saymaz
(kırpma kaçar), yüksek tutmak koyu görüntüyü bant sanar (görüntü kesilir). KE,
yalnız bu taramayı sınamak için üretilmiş ağır gürültülü bant taşıyan kaynaktır;
K3 ızgarasına girmez.

<!-- BETIK-LIMIT-BASLANGIC -->
| Kaynak | Gercek sinir | limit=8 | limit=16 | limit=24 | limit=32 | limit=40 | limit=48 | limit=56 | limit=64 | limit=80 | limit=96 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| KD 2,39:1 gurultulu asimetrik bant | 1920:804:0:120 | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | 1920:804:0:120 | 1920:804:0:120 | 1920:804:0:120 | 1920:804:0:120 | 1920:804:0:120 |
| KE 2,39:1 agir gurultulu bant (yalniz tespit sinamasi) | 1920:804:0:120 | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | 1920:812:0:112 |
| NA kenarsiz (parca-1) | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1042:0:4 | 1920:904:0:136 | 1920:902:0:136 |
| NB kenarsiz (parca-2) | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1072:0:6 | 1920:1070:0:6 | 1920:1064:0:10 |

| Kaynak | Ust bant YMIN / YAVG / YMAX (8 bit olcek, 60 kare ortalamasi) |
|---|---|
| KD 2,39:1 gurultulu asimetrik bant | 4,67 / 44,65 / 85,28 |
| KE 2,39:1 agir gurultulu bant (yalniz tespit sinamasi) | 14,15 / 95,72 / 178,21 |

Taranan 10 limit degerinden **8..56** kenarsiz kaynaklarin ikisini de bozmadan biraktigi araliktir. KD'nin (bant YAVG 44,65) gercek sinirini veren aralik **48..96**; ikisinin kesisimi **48..56**.
KE (bant YAVG 95,72) icin boyle bir ortak deger **yok**: KE'yi dogru bulan aralik yok, kenarsiz kaynaklari bozmayan aralik 8..56, kesisim bos. Bant parlakligi belli bir noktayi gectikten sonra tek bir esik degeriyle hem tespit hem guvenlik saglanamiyor.
<!-- BETIK-LIMIT-BITIS -->

### Yoklamanın süresi

İlk koşumda makinede başka sözleşmelerin `ffmpeg` süreçleri vardı ve süreler
kararsız çıktı; süre ölçümü ayrıca, makine boşken tekrarlanarak alındı.

<!-- BETIK-SURE-BASLANGIC -->
Makine bosken tekrarlanan olcum. Tekrar sayisi 3, medyan verildi. Olcum sirasinda es zamanli ffmpeg sayisi: basta 0, sonda 0.

| Kaynak | 2 sn pencere (120 kare) | 10 tekil kare yayilmis | Tam klip (1200 kare) |
|---|---|---|---|
| KA 2,39:1 duz bant | 0,27 sn | 1,02 sn | 0,63 sn |
| KB 2,20:1 duz bant | 0,20 sn | 1,09 sn | 0,58 sn |
| KC 1,85:1 duz bant | 0,35 sn | 1,88 sn | 1,26 sn |
| KD 2,39:1 gurultulu asimetrik bant | 0,35 sn | 1,79 sn | 1,24 sn |
| NA kenarsiz (parca-1) | 0,22 sn | 1,22 sn | 0,72 sn |
| NB kenarsiz (parca-2) | 0,21 sn | 1,14 sn | 0,60 sn |
| VD bant genisligi sahne icinde degisiyor | 0,33 sn | 1,78 sn | 1,19 sn |
<!-- BETIK-SURE-BITIS -->

## K3 — Kazanç ızgarası

<!-- BETIK-K3-BASLANGIC -->
| Kaynak | Bitrate | Kol | Boyut (bayt) | Boyut farki | VMAF-NEG ort | p10 | En kotu kare |
|---|---|---|---|---|---|---|---|
| KA 2,39:1 duz bant | 1000k | kirpmasiz | 2537145 | 0,34% | 65,991 | 56,952 | 0,000 |
| KA 2,39:1 duz bant | 1000k | kirpmali | 2545864 | 0,34% | 66,664 | 57,993 | 0,000 |
| KA 2,39:1 duz bant | 2000k | kirpmasiz | 5054864 | 0,05% | 74,618 | 63,431 | 0,000 |
| KA 2,39:1 duz bant | 2000k | kirpmali | 5052161 | 0,05% | 74,808 | 63,533 | 0,000 |
| KA 2,39:1 duz bant | 4000k | kirpmasiz | 10104187 | 0,06% | 80,405 | 67,327 | 0,000 |
| KA 2,39:1 duz bant | 4000k | kirpmali | 10110617 | 0,06% | 80,346 | 67,229 | 0,000 |
| KB 2,20:1 duz bant | 1000k | kirpmasiz | 2731144 | 0,02% | 94,364 | 93,015 | 66,016 |
| KB 2,20:1 duz bant | 1000k | kirpmali | 2731810 | 0,02% | 94,361 | 92,989 | 65,955 |
| KB 2,20:1 duz bant | 2000k | kirpmasiz | 5655586 | 0,23% | 95,823 | 94,821 | 67,012 |
| KB 2,20:1 duz bant | 2000k | kirpmali | 5642530 | 0,23% | 95,781 | 94,773 | 66,993 |
| KB 2,20:1 duz bant | 4000k | kirpmasiz | 11410728 | 0,72% | 96,640 | 96,424 | 68,208 |
| KB 2,20:1 duz bant | 4000k | kirpmali | 11492868 | 0,72% | 96,599 | 96,370 | 68,147 |
| KC 1,85:1 duz bant | 1000k | kirpmasiz | 2585385 | 0,09% | 9,585 | 2,977 | 0,000 |
| KC 1,85:1 duz bant | 1000k | kirpmali | 2587595 | 0,09% | 9,614 | 2,945 | 0,000 |
| KC 1,85:1 duz bant | 2000k | kirpmasiz | 5120818 | 0,41% | 23,756 | 6,270 | 0,000 |
| KC 1,85:1 duz bant | 2000k | kirpmali | 5141636 | 0,41% | 24,239 | 6,140 | 0,000 |
| KC 1,85:1 duz bant | 4000k | kirpmasiz | 10122706 | 0,18% | 43,160 | 8,156 | 0,000 |
| KC 1,85:1 duz bant | 4000k | kirpmali | 10140520 | 0,18% | 43,638 | 8,076 | 0,000 |
| KD 2,39:1 gurultulu asimetrik bant | 1000k | kirpmasiz | 2547261 | 0,20% | 67,043 | 58,229 | 0,000 |
| KD 2,39:1 gurultulu asimetrik bant | 1000k | kirpmali | 2552468 | 0,20% | 67,149 | 58,323 | 0,000 |
| KD 2,39:1 gurultulu asimetrik bant | 2000k | kirpmasiz | 5053744 | 0,09% | 74,875 | 63,837 | 0,000 |
| KD 2,39:1 gurultulu asimetrik bant | 2000k | kirpmali | 5058074 | 0,09% | 75,060 | 64,245 | 0,000 |
| KD 2,39:1 gurultulu asimetrik bant | 4000k | kirpmasiz | 10070886 | 0,17% | 79,927 | 67,132 | 0,000 |
| KD 2,39:1 gurultulu asimetrik bant | 4000k | kirpmali | 10087811 | 0,17% | 80,481 | 67,646 | 0,000 |

**Kazanc (kirpmali p10 - kirpmasiz p10), A yontemi, aktif alanda:**

| Kaynak | Bant yuzdesi | 1000k | 2000k (karar) | 4000k |
|---|---|---|---|---|
| KA 2,39:1 duz bant | 25,56% | 1,041 | 0,102 | -0,098 |
| KB 2,20:1 duz bant | 19,26% | -0,026 | -0,048 | -0,054 |
| KC 1,85:1 duz bant | 4,07% | -0,032 | -0,130 | -0,080 |
| KD 2,39:1 gurultulu asimetrik bant | 25,56% | 0,094 | 0,408 | 0,514 |

Bitrate yukseldikce kazanc 1 kaynakta artiyor (KD), 3 kaynakta azaliyor (KA, KB, KC). Karar noktasinda (2000k) kazanci pozitif olan kaynak sayisi 2/4: KA, KD.

**Kaynagin kendi zorlugu** (kirpmasiz kol, 2000k, A yontemi). p10 duzeyi kaynaga gore cok degisiyor; asagidaki iki sutun kazanci hangi zeminde olctugumuzu gosterir.

| Kaynak | Kare | VMAF-NEG medyan | 1,0'in altinda kalan kare |
|---|---|---|---|
| KA 2,39:1 duz bant | 1200 | 77,549 | 2 |
| KB 2,20:1 duz bant | 1200 | 96,090 | 0 |
| KC 1,85:1 duz bant | 1200 | 26,008 | 10 |
| KD 2,39:1 gurultulu asimetrik bant | 1200 | 77,551 | 2 |

**B yontemi (kirpmali cikti geri doldurulup tam karede puanlandi), 2000k:**

| Kaynak | Kirpmasiz p10 | Kirpmali+dolgu p10 | Kazanc |
|---|---|---|---|
| KA 2,39:1 duz bant | 73,769 | 74,783 | 1,014 |
| KB 2,20:1 duz bant | 95,258 | 95,276 | 0,018 |
| KC 1,85:1 duz bant | 3,255 | 2,759 | -0,496 |
| KD 2,39:1 gurultulu asimetrik bant | 73,905 | 73,507 | -0,398 |
<!-- BETIK-K3-BITIS -->

## K5 — Zarar tarafı

Kazanç tarafı tek başına kararı vermez: kırpma yanlış tetiklenirse görüntü
kalıcı olarak kesilir ve bu, birkaç puanlık VMAF kazancıyla telafi edilebilir
bir şey değil. Üç risk ayrı ölçüldü — (a) kenarsız kaynakta `cropdetect`'in
hiç kırpma önermemesi gerekiyor, (b) bant genişliği sahne içinde değişen
kaynakta tek bir kırpma kutusu yanlış olmak zorunda, (c) yanlış kırpma
gerçekten uygulanırsa bedeli ne.

(c)'deki hatalı kırpmalar uydurulmuş değil: `limit=64` taramasında
`cropdetect`'in NA ve NB üzerinde **kendi önerdiği** kutulardır.

<!-- BETIK-K5-BASLANGIC -->
**(a) Kenarsiz kaynakta yanlis kirpma:**

| Kaynak | Gercek sinir | t=0 | t=5 | t=10 | t=15 | 10 kare yayilmis | tam klip | Yanlis kirpma |
|---|---|---|---|---|---|---|---|---|
| NA kenarsiz (parca-1) | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | hayir |
| NB kenarsiz (parca-2) | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | 1920:1080:0:0 | hayir |

**(b) Bant genisligi sahne icinde degisen kaynak (VD):**

| Olcu | Deger |
|---|---|
| Ilk 10 sn | 2,39:1 letterbox (aktif 1920x804) |
| Son 10 sn | tam kare (aktif 1920x1080) |
| cropdetect t=0 | 1920:804:0:138 |
| cropdetect t=5 | 1920:804:0:138 |
| cropdetect t=15 | 1920:1080:0:0 |
| cropdetect 10 kare yayilmis | 1920:1080:0:0 |
| cropdetect tam klip | 1920:1080:0:0 |
| Boyut, kirpmasiz / kirpilmis | 5171048 / 5118851 bayt (fark 1,01%, **es boyut degil**) |
| B yontemi p10, kirpmasiz | 10,837 |
| B yontemi p10, 804'e kirpilip geri doldurulmus | 12,211 |
| B yontemi en kotu kare, kirpmasiz | 0,000 |
| B yontemi en kotu kare, kirpilmis | 1,645 |

**VD'nin iki yarisi ayri ayri.** Bant yalniz ilk yaridadir; ikinci yarida 804'e kirpmak **gercek goruntuyu** kesiyor. B yontemi kare farki (kirpilmis - kirpmasiz), ilk 600 karede ortalama 6,806, son 600 karede ortalama -2,761. Son yaridaki 600 karenin 413 tanesinde kirpilmis kol daha kotu. Yine de kaynagin **butun klip p10'u kirpmayla yukseliyor**: p10 alt %10'luk dilime bakiyor ve o dilim buyuk olcude ilk yaridan geliyor. Tek bir toplu VMAF sayisi, gercek goruntunun kesilmesini bu kaynakta gizleyebiliyor.

**(c) Yanlis kirpmanin bedeli.** `limit=64`'te cropdetect'in kenarsiz kaynaklarda onerdigi hatali kirpma uygulandi, cikti geri doldurulup tam karede puanlandi (B yontemi, 2000k):

| Kaynak | Hatali kirpma | Kesilen piksel | Boyut farki | Kirpmasiz p10 | Hatali kirpilmis p10 | Fark |
|---|---|---|---|---|---|---|
| NA kenarsiz (parca-1) | 1920:1042:0:4 | 38 satir | 0,06% | 72,705 | 71,675 | -1,030 |
| NB kenarsiz (parca-2) | 1920:1072:0:6 | 8 satir | 2,33% **es boyut degil** | 94,585 | 93,651 | -0,934 |
<!-- BETIK-K5-BITIS -->

## Öneri — bir uygulama sözleşmesi açılacaksa öncülleri

Bu ölçüm otomatik kırpmayı **önermiyor.** Karar noktasında ortalama kazanç
K4'ün tabanının altında kaldı ve dört kaynağın ikisinde negatif. Gerekçe tek
bir sayı değil, üç bulgunun üst üste binmesi:

1. **Kazancın olduğu yerde tespit yok, tespitin olduğu yerde kazanç yok.**
   Varsayılan `limit=24` ile gerçek sınırı bulunan üç kaynağın ortalama
   kazancı negatif; hükmü pozitife taşıyan tek kaynak, `cropdetect`'in
   varsayılan ayarla hiç bulamadığı KD. Sayılar hükümde.
2. **Tek bir `limit` değeri hem bulup hem güvende kalamıyor.** Bant parlaklığı
   arttıkça tespit için gereken eşik, kenarsız kaynağı kesmeye başlayan eşiğin
   üstüne çıkıyor; KE'de kesişim boş.
3. **Metrik yanlış kırpmayı tek başına yakalamıyor.** VD'nin ikinci yarısında
   gerçek görüntüden 276 satır kesildiği hâlde klibin p10 değeri yükseliyor. Kırpmanın güvenliği VMAF ile
   denetlenemez; yapısal bir korumaya (birden çok noktadan örnekleme + tutarlılık
   şartı) ihtiyaç var.

Buna rağmen bir uygulama sözleşmesi açılırsa, bu ölçümden çıkan öncüller:

- **Örnekleme yeri tek nokta olamaz.** `t=0` KA'da açılış karartmasına, VD'de
  sahne değişimine düşüyor. En az on kareye yayılmış örnekleme gerekir.
- **Birleştirme birleşim değil, mod olmalı.** Birleşim tek bir siyah kareye
  karşı kırılgan; KA'da kırpmayı iptal ediyor, mod kurtarıyor.
- **`limit` varsayılanı yükseltilemez.** 56'nın üstünde kenarsız kaynaklar
  kesilmeye başlıyor.
- **Kırpma varsayılan açık gelemez.** Kazanç, hatalı kırpmanın bedelinden
  küçük; K5(c) bunun ölçüsüdür.
- **Bant genişliği sahne içinde değişiyorsa kırpma yapılmamalı.** Örneklenen
  kareler farklı kutular veriyorsa (VD'de 5/10) karar "kırpma" değil
  "dokunma" olmalı.

## Bu ölçümün kapsamadıkları

- **Kaynak sınıfı üretilmiş, bulunmuş değil.** Bantlar `pad` ile konuldu; gerçek
  bir DVD/Blu-ray aktarımının bandında olan sıkıştırma çınlaması, hafif eğik
  bant sınırı ve kenar bulanıklığı burada yok. KD ve KE bunun yerine gürültü ve
  asimetri taşıyor. Gerçek bir letterbox'lı aktarımda `cropdetect`'in davranışı
  ölçülmedi.
- **Tek çözünürlük, tek kodek, tek süre.** 1920x1080, 60 fps, libx264 2 geçiş,
  20 saniye. 4K, 24 fps, libx265 ve uzun klip ölçülmedi.
- **Ses yok.** Bütün koşumlar sessiz; teslim boyutu eşitlemesi yalnız video
  akışı üzerinden yapıldı.
- **Oynatma tarafı ölçülmedi.** Kırpılmış dosyanın en-boy oranı değişiyor;
  oynatıcının kendi siyah bandını koyması, ekranı doldurma davranışı ve
  kullanıcının gördüğü görüntü büyüklüğü bu belgenin dışında.
- **Yoklamanın kendi kodlama maliyeti yok.** Ölçülen süre yalnız `cropdetect`
  geçişidir; VidShrink'in mevcut yoklamasına eklendiğinde paylaşılan çözme
  maliyeti bu sayıdan düşük olabilir.
- **Kazançlar gerçek bant sınırıyla ölçüldü.** Kırpma kolunun `crop` değeri
  kaynağın bilinen sınırından geldi, `cropdetect`'in önerisinden değil. Bu
  bilerek seçildi: K3 kırpmanın *tavanını* ölçüyor, tespitin başarısını değil.
  Tespit ayrı ölçüldü (K2) ve ikisi hükümde birleştirildi. Uçtan uca bir boru
  hattının (yokla → kırp → kodla) gerçek kazancı bu belgede yok.
- **Havuzda letterbox'lı kaynak yoktu.** `.calisma/kaynak` altındaki üç ortak
  parçanın üçü de tam kare. Sınıf bu yüzden üretildi. Ölçüm ortamının boşluğu
  olarak kayda değer: siyah kenar üzerine ileride yapılacak her ölçüm aynı
  üretim adımını tekrarlamak zorunda kalacak.

## K6 — Özeti üreten düzenek

Bu belgedeki **her tablo ve hüküm cümlesi** elle yazılmadı; işaretçiler arasına
betikle basıldı. Elle yazılan kısımlar yalnız yöntem, gerekçe ve yorum
paragraflarıdır.

Üretici: `tools/siyah-kenar/oz.py`. Çalıştırma (worktree kökünden):

```
python tools/siyah-kenar/oz.py
```

Betik `stdout`'a yalnız hüküm cümlesini basar; aynı metni raporun
`BETIK-HUKUM-*` işaretçileri arasına da yazar. İkisinin birebir aynı olduğunun
denetimi:

```
python tools/siyah-kenar/oz.py > .calisma/t134/hukum-stdout.txt
python - <<'PY'
m = '<!-- BETIK-HUKUM-BASLANGIC -->'
s = open('docs/olcumler/siyah-kenar.md', encoding='utf-8').read()
a = s.index(m) + len(m); b = s.index('<!-- BETIK-HUKUM-BITIS -->')
r = s[a:b].strip()
c = open('.calisma/t134/hukum-stdout.txt', encoding='utf-8').read().strip()
print('AYNI' if r == c else 'FARKLI', len(r.encode()), len(c.encode()))
PY
```

Son koşumun sonucu (bu satırı da betik yazar):

<!-- BETIK-K6CHECK-BASLANGIC -->
`python tools/siyah-kenar/oz.py` — rapordaki hukum blogu ile betigin stdout ciktisi **birebir ayni** (1023 bayt).
<!-- BETIK-K6CHECK-BITIS -->

**Hangi sayı hangi komuttan geliyor:**

| Rapordaki blok | Veri dosyası | Üreten komut |
|---|---|---|
| K1 künye + kanıt tabloları | `.calisma/t134/olcu/kaynak-kanit.json` | `python tools/siyah-kenar/kanit.py` |
| K2 cropdetect ızgarası, birleşim/mod | `.calisma/t134/yokla/cropdetect.json` | `python tools/siyah-kenar/yokla.py` |
| K2 `limit` taraması + bant lumaları | `.calisma/t134/yokla/limit.json` | `python tools/siyah-kenar/esik.py` |
| K2 yoklama süreleri | `.calisma/t134/yokla/sure.json` | `python tools/siyah-kenar/sure.py` |
| K3 ızgarası, K5 sayıları | `.calisma/t134/olcu/vmaf.json` + `A-*.json` / `B-*.json` | `python tools/siyah-kenar/olc.py` |
| Kaynaklar | `.calisma/t134/kaynak/*.mkv` | `bash tools/siyah-kenar/kaynak.sh` |
| Kodlamalar | `.calisma/t134/cikti/*.mp4` | `bash tools/siyah-kenar/kos.sh`, `bash tools/siyah-kenar/zarar.sh` |

Ölçüm çıktıları `.calisma/` altındadır ve git'e girmez; yeniden üretmek için
yukarıdaki komutlar bu sırayla koşulur.
