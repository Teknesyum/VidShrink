# Algı ölçüsü — normalizasyon, tonemap yolu ve en kötü birim

T97 + T104. Ölçüm makinesi: Windows 11, `ffmpeg 9.0-full_build-www.gyan.dev`
(`--enable-libvmaf --enable-libzimg`).

T97 kaynağı: `.calisma/kaynak/parca-1.mkv` — `kaynak-1080p60-hdr-17dk.mp4` içinden
00:02:00 noktasından 60 sn, `-c copy`. 1920x1080, 60 fps, 3624 kare, yuv420p10le,
bt2020/smpte2084/bt2020nc, `color_range=pc`.

T104 kaynakları: aynı ana kaynaktan alınmış üç ayrı 60 sn'lik parça
(`parca-1.mkv`, `parca-2.mkv`, `parca-3.mkv`); her birinin **ilk 30 sn'si**
`-map 0:v:0 -c copy` ile alındı. `parca-2`/`parca-3` ses taşıyor, `parca-1`
taşımıyor; karşılaştırma videoya indirgenerek bu fark ortadan kaldırıldı.
Üçü de 1920x1080 60 fps HDR, 1800/1799/1799 kare. **Üçü aynı ana kaynaktan
geliyor** — içerik türü çeşitliliği (film, konuşan kafa, ekran kaydı) hâlâ
ölçülmedi.

Bu turlarda süre/hız iddiası yok. T104 ölçümleri paylaşımlı makinede koştu
(beş ajan); bir VMAF koşumu ffmpeg'i geçici olarak düşürdü, tekrarında geçti.


## 1. Mevcut durum — `QualityMeter` bugün ne ölçüyor

Tek bir özel `MeasureAsync` üç genel giriş tarafından çağrılıyor: `MeasureAsync`
(düz), `MeasureTonemappedReferenceAsync` (`tonemapReference: true`) ve iki
`MeasureWindowAsync` aşırı yüklemesi (referans ve test için ayrı `-ss`).

Her metrik **ayrı bir ffmpeg koşumu**. Üçünün de filtre grafiği aynı:

    [0:v]<test-normalizasyonu>[t];[1:v]<tonemap-öneki><referans-normalizasyonu>[r];[t][r]<metrik>

Normalizasyon `zscale` ile açık: giriş uzayı dosyanın etiketlerinden, çıkış uzayı
referanstan alınıyor. `zscale` yoksa ölçüm hata fırlatıyor — sessizce etiketsiz
karşılaştırma yapılmıyor. Referans HDR ise çıkış `yuv420p10le` ve referansın
kendi uzayı; değilse `yuv420p` bt709 limited.

| Metrik | Çağrı | Kaynak | Toplama |
|---|---|---|---|
| VMAF-NEG ortalama | `libvmaf=model=version=vmaf_v0.6.1neg:log_fmt=json` | kare başına JSON günlüğü | aritmetik ortalama |
| VMAF-NEG harmonik | aynı koşum | aynı günlük | `n / Σ(1/max(x,1))` — 0 puanlı kare 1 sayılıyor |
| VMAF-NEG p10 | aynı koşum | aynı günlük | sıralı küme üzerinde doğrusal ara değerli 10. yüzdelik |
| VMAF-NEG min | aynı koşum | aynı günlük | tüm kümenin en küçüğü |
| VMAF-NEG en kötü birim | aynı koşum | aynı günlük | **T97'de eklendi** — 2 sn'lik ardışık pencerelerin en düşük ortalaması; `SceneMap` verilirse sahne sınırları (§4) |
| XPSNR | `xpsnr` | stderr özet satırı | `(4y + u + v) / 6`, klibin tamamı |
| SSIM | `ssim` | stderr `All:` | klibin tamamı |

Bilinen sınır: XPSNR ve SSIM'in kare başına dökümü okunmuyor, yalnız özet satırı
alınıyor. Bu yüzden p10, min ve sahne tabanı **yalnız VMAF-NEG için** var. Bu
turda değiştirilmedi.

Karşılaştırılabilirlik kapısı: referans ve test'in HDR olup olmadığı ayrışıyorsa
ya da ikisi de HDR ama aktarım/ana renkleri farklıysa ölçüm `Comparable=false`
dönüyor ve hiçbir sayı üretilmiyor. Tek istisna tonemap yolu (§3).


## 2. `NormalizeVmafCeiling` yargısı — **bozuyordu, kaldırıldı**

Kaldırılan kod, kare başına *ve* toplanmış dört değerin her birine ayrı ayrı
uygulanıyordu:

    private static double NormalizeVmafCeiling(double score)
        => score >= 99.8 ? 100.0 : score;

Üstündeki cümle "vmaf_v0.6.1neg özdeş karelerde ~99,87 veriyor" diyordu. O sayı
tek bir içeriğe ait: 320x240 `testsrc2`. Gerçek tavan içeriğe ve çözünürlüğe göre
oynuyor.

**Ölçüm — kelepçenin tavanla ilişkisi yok.** Bit düzeyinde özdeş dosyanın
kendisiyle karşılaştırılması:

| Özdeş çift | ham VMAF-NEG ortalama | kelepçeli | eşiğin altında mı |
|---|---|---|---|
| 320x240 testsrc2 crf 23 | 99,8712 | **100,0000** | hayır, 99,8 üstü → kelepçe çalıştı |
| 1920x1080 60 fps gerçek içerik (10 sn) | 99,6769 | 99,6769 | evet → kelepçe hiç çalışmadı |

Yani sabit 99,8 eşiği asıl hedef içerikte (1080p60) modelin tavanına *ulaşmıyor*:
özdeş kopya 99,68 diye raporlanıyordu. Eşik yalnız küçük sentetik klipte
tetikleniyordu.

**Ölçüm — kare başına kelepçe neredeyse hiç iş yapmıyordu.** VMAF-NEG kareleri
ya tam 100 veriyor ya da 99,8'in belirgin altına düşüyor; `[99,8; 100)` bandında
kare pek kalmıyor:

| Günlük | kare | `[99,8; 100)` | `== 100` |
|---|---|---|---|
| 1080p özdeş | 602 | 0 | 509 |
| 1080p crf 8 | 602 | 2 | 498 |
| 1080p crf 12 | 602 | 7 | 453 |
| 1080p tonemap yüksek | 3624 | 11 | 247 |
| 1080p tonemap düşük | 3624 | 0 | 0 |

Bütün etkiyi **toplanmış değere uygulanan ikinci kelepçe** yapıyordu.

**Ölçüm — A/B sonucunu nasıl bozuyordu.** İki gerçek yarışmacı, aynı referansa
karşı (320x240 crf 23 referans):

| Yarışmacı | ham ortalama | ham harmonik | kelepçeli ortalama | kelepçeli harmonik |
|---|---|---|---|---|
| A: özdeş kopya | 99,8712 | 99,8680 | 100,0000 | 100,0000 |
| B: crf 10 yeniden kodlama | 99,8392 | 99,8341 | 100,0000 | 100,0000 |
| **A − B** | **+0,0320** | **+0,0339** | **0,0000** | **0,0000** |

Hastalık **ölçek kayması değil, tavan çökmesi.** Kelepçe iki yarışmacıyı farklı
ölçeklendirmiyor; `[99,8; 100]` aralığının **tamamını tek bir noktaya indiriyor**.
O aralıkta duran her A/B karşılaştırması berabere raporlanıyordu — kayan değil,
silinen bir sonuç. Hedefin görsel kayıpsıza yakın olduğu durum tam olarak burası.

Orta kalitede etkisi ölçülebilir ama önemsizdi (1080p, aynı referansa karşı
crf 8 / crf 12): ham fark +0,3151, kelepçeli fark +0,3141 — 0,001 VMAF. Yani
sorun her yerde değil, yalnız tavanda; ama tavan da işin asıl bölgesi.

**Yapılan:** `NormalizeVmafCeiling` tamamen kaldırıldı. `VmafNegMean/Harmonic/P10/Min`
artık libvmaf'ın verdiği ham değerler. Özdeş kopya artık 100 değil, modelin o
içerikteki tavanını raporluyor — 1080p60 gerçek içerikte 99,68, 320x240
`testsrc2`'de 99,87. Kullanıcıya gösterilecek "kusursuz" rozeti istenirse sayıyı
bozarak değil ayrı bir alanla verilmeli.

Yan bulgu: özdeş 1080p içerikte kare bazlı **min 97,4256**, p10 97,9241. Yani
`VmafNegMin` özdeş dosyada bile 97,4 diyor — bu bir kalite işareti değil, modelin
kendi gürültüsü. §4'ün gerekçesi bu.

### 2.1 Geriye dönük kapsam — kaldırma hangi eski sayıyı bozdu (K4c)

T97 kelepçeyi kaldırdı ama **daha önce yayımlanmış sayıların** ne olacağını
yazmadı. Kapsam şu: kelepçe yalnız `[99,8; 100]` bandındaki değerleri
değiştiriyordu. O bandın dışındaki her sayı kelepçeli ve kelepçesiz **birebir
aynı**. Dolayısıyla geriye dönük düzeltme gerektiren sayı, yalnız o banda düşmüş
olanlardır.

`docs/olcumler/handbrake-acigi.md` tarandı: **o belgedeki hiçbir VMAF değeri
`[99,8; 100]` bandında değil.** Eş-boyut tablosundaki (`handbrake-acigi.md:40-51`)
en yüksek değer **93,51** (HDR 1/2, HandBrake); SDR satırlarının en yükseği 89,55.
Tonemap hizalı ölçüm 48,96 / 40,17. Bandın alt sınırına en yakın sayı 93,51 ve
aradaki mesafe 6,3 puan. Yani:

- **8,79 puanlık sıkıştırma farkı iddiası ayakta** — kelepçeden etkilenmedi.
- Aynı belgedeki harmonik 10,18 ve p10 14,60 farkları da etkilenmedi.
- Etkilenen tek ifade **düzyazı**: aynı dosyanın kendisiyle karşılaştırıldığı
  testin "kullanıcıya VMAF 100 verdi" cümlesi. O cümle kelepçenin *davranışını*
  anlatıyor ve artık geçersiz — bugün aynı test modelin tavanını raporlar. Sayı
  değil, tarif eskidi.

Bu cümlenin düzeltilmesi T94'ün dosyasında; T104 oraya yazmıyor.

Tarama `docs/` ağacının tamamında koştu (`99,8x`, `99,9x`, `VMAF 100`,
`100,0000`); yukarıdaki tek düzyazı cümlesi dışında bandın içine düşen değer
çıkmadı. Kod ve test ağacı taranmadı — orada yayımlanmış ölçüm sayısı yok.


## 3. Tonemap'li referans yolu — **çağrılıyor ve duyarlı**

Yol gerçekten var: `MeasureTonemappedReferenceAsync`'in tek üretim çağıranı
`tools/VidShrink.Bench` (`bench measure-tonemapped`). Uygulama içinden çağıran
yok — HandBrake karşılaştırması bu kapıdan geçiyor.

Ne yapıyor: referansın `MediaInfo`'su SDR gibi yeniden etiketleniyor
(`IsHdr=false`, bt709/bt709/bt709/tv, yuv420p) ki karşılaştırılabilirlik kapısı
HDR/SDR ayrışmasında ölçümü reddetmesin; ardından referans zincirinin başına
`HdrResolver.TonemapFilter` ekleniyor. Böylece referans, çıktının üretildiği
tonemap'in aynısından geçiyor.

**Duyarlılık ölçümü.** Aynı HDR kaynaktan (`parca-1.mkv`, 60 sn) aynı tonemap
filtresiyle iki SDR çıktı; tek fark bit oranı. Ölçüm `bench measure-tonemapped`
ile, üretim kodunun kendi yolundan:

| | yüksek (`-crf 18`, 14 855 kb/s) | düşük (`-b:v 300k`, 202 kb/s) | fark |
|---|---|---|---|
| VMAF-NEG ortalama | **90,5957** | **25,1360** | 65,46 |
| VMAF-NEG harmonik | 86,3932 | 12,5446 | 73,85 |
| VMAF-NEG p10 | 88,2500 | 5,7337 | 82,52 |
| VMAF-NEG min | 0,7867 | 0,0000 | 0,79 |
| VMAF-NEG en kötü sahne (2 sn) | 85,9740 @ 50,0 sn | 5,8757 @ 12,0 sn | 80,10 |
| XPSNR | **40,3535** | **21,3199** | 19,03 |
| SSIM | 0,98478 | 0,825074 | 0,1597 |

Bit oranı yetmiş kat değişirken üç metrik de ayrışıyor.
`docs/olcumler/handbrake-acigi.md`'deki GEÇERSİZ tablodaki hastalık — XPSNR'ın
14,86 / 14,78 / 14,67'de çakılı kalması — **bu yolda yok**: XPSNR 40,35'ten
21,32'ye iniyor.

Dikkat çeken tek şey `VmafNegMin`: iyi çıktıda 0,79. 90,6 ortalamalı bir klipte
0,79 puanlı bir kare kalite olayı değil, sahne kesmesinde tek karelik bir
hizalanma/ani değişim artığı. Kullanılabilir bir taban değil (§4).

**Yan bulgu, düzeltilmedi.** `xpsnr` filtresi bu çiftte uyarı basıyor:
`not matching timebases found between first input: 1/15360 and second input
1/1000`. Her iki zincirin sonuna `settb=AVTB` eklenip ölçüm tekrarlandı; sonuç
kuruşu kuruşuna aynı çıktı (38,9113 / 42,7523 / 43,7238). Uyarı bu girdide
ölçüyü kaydırmıyor, o yüzden üretim zinciri değiştirilmedi. Farklı kare hızlı
çiftte tekrar bakılmalı — bu turda ölçülmedi.


## 4. En kötü birim — 2 saniyelik sabit pencere

Sorun: filmin tamamındaki tek en kötü kare kullanıcıyı ilgilendirmiyor, hem de
ölçülemiyor. §2'nin yan bulgusu: özdeş 1080p içerikte min 97,43. Buna karşılık
gerçekten iyi bir kodlamada min 0,79 çıkabiliyor (§3). Aynı sayı hem özdeş
içerikte 97 hem iyi kodlamada 1 diyorsa taban olarak kullanılamaz.

Eklenen: kareler **ardışık, örtüşmeyen birimlere** bölünüyor, her birimin
ortalaması alınıyor, en düşüğü ve başlangıç saniyesi raporlanıyor
(`VmafNegWorstScene`, `WorstSceneStartSeconds`, `SceneWindowSeconds`). Başlangıç
saniyesi referans zaman çizgisine göre; pencereli ölçümde `referenceStartSeconds`
ekleniyor.

Birim sınırı iki kaynaktan gelebilir: sabit uzunluklu pencere, ya da `SceneMap`
varsa sahne kesmelerinin kendisi. İkisi §4.2'de karşılaştırıldı.


### 4.1 Yargı ölçütü

Ölçüt her iki yolda da aynı: birim, metriğin kendi gürültüsünden büyük bir sinyal
vermeli.

- **Gürültü** = bit düzeyinde özdeş klipte en kötü birimin, klip ortalamasının
  altına düşme miktarı. Özdeş çiftte gerçek bir kalite olayı yok; bu düşüş
  tamamen modelin kendi salınımı.
- **Sinyal** = aynı referansa karşı crf 8 ve crf 12'nin en kötü birimleri
  arasındaki fark. İki gerçek yarışmacı, ayırt edilmesi gereken şey bu.

Bilinen yozlaşma: birim klip uzunluğuna yaklaştıkça gürültü sıfıra gidiyor ve
oran patlıyor, ama ölçü artık taban olmaktan çıkıp klip ortalamasına dönüşüyor.
Bu yüzden **oran tek başına karar vermiyor; mutlak sinyal de bakılıyor.**


### 4.2 Sabit pencere / sahne sınırı — üç içerik (T104)

Üç 30 sn'lik HDR klip, her biri crf 8 ve crf 12'ye kodlandı, ayrıca kendisiyle
karşılaştırıldı. Sahne haritası `SceneDetector` ile, üç eşikte
(0,20 = `SceneMap.DefaultThreshold`, 0,10, 0,05). Özdeş klip ortalamaları:
p1 98,8005 — p2 97,4396 — p3 99,8882.

| Yol | p1 gürültü | p1 sinyal | p2 gürültü | p2 sinyal | p3 gürültü | p3 sinyal |
|---|---|---|---|---|---|---|
| sabit 1 sn | 1,2437 | 2,1050 | 0,0027 | 1,0195 | 0,7724 | 2,5138 |
| **sabit 2 sn** | **1,1745** | **1,7258** | **0,0027** | **0,9310** | **0,5813** | **2,5611** |
| sabit 3 sn | 1,1807 | 1,6297 | 0,0018 | 0,8498 | 0,3698 | 2,5552 |
| sabit 5 sn | 0,9750 | 1,5959 | 0,0012 | 0,8083 | 0,1716 | 1,8208 |
| sahne @0,20 | 0,7119 | 1,6275 | 0,0011 | 0,9802 | 0,5813 | 2,5611 |
| sahne @0,10 | 1,0115 | 1,7185 | 0,0011 | 0,9802 | 0,0210 | 1,1440 |
| sahne @0,05 | 1,0273 | 1,7185 | 0,0011 | 0,9802 | 0,1642 | 1,2476 |

Üretim eşiğinde (0,20) 30 saniyeye düşen sahne sayısı:

| İçerik | sahne | en kısa | en uzun |
|---|---|---|---|
| p1 | 2 | 5,49 sn | 24,52 sn |
| p2 | 2 | 1,67 sn | 28,33 sn |
| p3 | **1** | 30,00 sn | 30,00 sn |

**Karar içerikler arasında değişiyor — ve K2'nin öngördüğü şey bu.**

- **p1**: sahne yolu orana göre kazanıyor (2,29 vs 1,47), mutlak sinyale göre
  kaybediyor (1,6275 vs 1,7258). Oran kazancının kaynağı ayrım gücü değil,
  gürültünün birim uzadıkça küçülmesi: kazanan "sahne" 24,5 saniyelik. 24,5
  saniyelik bir birim taban değil, ikinci bir ortalama.
- **p2**: hiçbir yol ayırt edemiyor. Özdeş klipte en kötü birim klip
  ortalamasının 0,001–0,003 altında; gürültü sıfıra çökmüş, her oran yozlaşmış.
  Bu klip neredeyse durağan bir plan.
- **p3**: sahne yolu üretim eşiğinde **hiç kurulamıyor** — 30 saniyede tek sahne
  var, birim sayısı ikiden az kalıyor ve kod sabit pencereye düşüyor (§4.4).
  Eşiği zorlayınca sinyal yarılanıyor: 2,5611 → 1,2476 (@0,05), 1,1440 (@0,10).

**Kayıp yöntemden mi, haritanın çözünürlüğünden mi (T101 uyarısı).** T101 aynı
kaynakta haritanın **az böldüğünü** ölçtü: gözle doğrulanmış 144,2–333,3 sn
penceresinde 28 gerçek kesim var, harita 10 üretiyor; yanlış pozitif sıfır, kaçan
18 kesim 0,112–0,199 skorlarıyla `SceneMap.DefaultThreshold = 0.2` eleğinde
düşüyor (ölçen T101, `docs/olcumler/sahne-haritasi.md` — o tur kendi dalında).
Yani harita "sahne" derken çoğu zaman iki-üç gerçek çekimi birden gösteriyor, ve
en kötü çekim birleşik birimin ortalamasında kaybolabilir. O halde sahne yolunun
kaybı yöntemden değil, kaba haritadan geliyor olabilir.

Yukarıdaki tablo bu ayrımı kısmen yapıyor: 0,10 ve 0,05 eşikleri **daha ince
bölen** haritalar (p1 2 → 6 → 10 sahne, p3 1 → 2 → 8 sahne). İnceltmek sahne
yolunu kurtarmıyor:

| İçerik | sahne @0,20 | @0,10 | @0,05 | sabit 2 sn |
|---|---|---|---|---|
| p1 sinyal | 1,6275 | 1,7185 | 1,7185 | **1,7258** |
| p3 sinyal | (sabite düştü) | 1,1440 | 1,2476 | **2,5611** |

p1'de sinyal inceldikçe yükseliyor ama sabit pencerenin altında duruyor ve iki
eşikte aynı sayıya oturuyor; p3'te inceltmek sinyali yarıdan aşağı düşürüyor.
Ölçülen aralıkta "daha ince harita" hipotezi sahne yolunu öne geçirmiyor.

**Ama ayrım tam değil.** Eşiği düşürmek, T101'in kastettiği *doğru* ince harita
değil: T96 eşiği 0,20'de yanlış pozitif sıfır olduğu için seçmişti, 0,05'te
gelen kesmelerin doğruluğu bu turda doğrulanmadı. Yani "doğru ve ince bir
haritayla sahne yolu kazanır mı" sorusu **ölçülmedi**. Ölçülen: eldeki
dedektörün ürettiği hiçbir eşikte kazanmıyor.

**K6 yargısı: ölçüldü, sahne tabanlı yol kazandırmadı.** Sabit 2 sn'lik pencere
kalıyor. Gerekçe üçü birlikte:

1. Üretim eşiğinde sahne yolu üç içeriğin ikisinde erişilebilir değil (p3 tek
   sahne, p2 tek uzun sahne + kırıntı).
2. Erişilebildiği yerde mutlak sinyali yükseltmiyor: p1'de 1,7258 → 1,6275,
   p3'te 2,5611 → 1,2476.
3. Tek iyileşme p1'in oranı, o da gürültünün birim uzamasından küçülmesiyle —
   T97'nin 10 sn'lik pencerede zaten adını koyduğu yozlaşmanın aynısı.

Sahne sınırıyla arama **kodda duruyor** ve `SceneMap` verilirse çalışıyor (§4.4);
üretimde varsayılan yol sabit pencere. İlkeye uyduğu için değiştirilmedi.

Yargı haritanın bugünkü haliyle sınırlı. T101 eşiği inceltirse bu karşılaştırma
yeniden koşturulmalı — düzenek `analyze` komutuyla hazır, tek gereken yeni
haritayla üç klibin sahne satırlarını tekrar üretmek.

**Yan kazanç: T97'nin ince payı kalınlaştı.** T97 2 sn'yi tek içerik çiftinden,
%3,6'lık payla seçmişti (sinyal 0,715 / gürültü 0,690). Üç içerikte aynı seçim
p1'de %47 (1,7258 / 1,1745), p3'te %341 (2,5611 / 0,5813) payla duruyor; p2 karar
veremiyor ama bir yolu diğerine de tercih etmiyor. T97'nin mutlak sayıları burada
tekrar etmiyor — o 10 sn'lik klipti, bu 30 sn. Farklı ölçüm, çelişki değil.

1 sn p1'de daha yüksek sinyal veriyor (2,1050) ama gürültüsü de en yüksek
(1,2437) ve p3'te 2 sn'nin altında kalıyor. 3 ve 5 sn her içerikte sinyali
düşürüyor. 2 sn üç içerikte de ilk ikinin içinde kalan tek uzunluk.

**T97'nin tek içerikli taraması, kayıt için.** Farklı klip (`parca-1`, 10 sn),
farklı sayılar; T104 tablosunun yerini almıyor, yalnız 0,5 sn ve 10 sn uçlarını
gösterdiği için duruyor. Özdeş klip ortalaması 99,677.

| Pencere | özdeş klipte en kötü | gürültü | sinyal | sinyal > gürültü |
|---|---|---|---|---|
| 0,5 sn | 97,914 | 1,763 | 1,384 | hayır |
| 1 sn | 98,338 | 1,339 | 1,089 | hayır |
| **2 sn** | **98,987** | **0,690** | **0,715** | **evet** |
| 3 sn | 99,130 | 0,547 | 0,606 | evet |
| 5 sn | 99,360 | 0,317 | 0,516 | evet |
| 10 sn | 99,676 | 0,001 | 0,316 | evet ama yozlaşmış |

Alt uç: 0,5 sn'de özdeş klip 97,91'e düşüyor, ki bu kullanılamaz min'in (97,43)
neredeyse aynısı. Üst uç: 10 sn'lik klipte 10 sn'lik pencere klip ortalamasının
kendisi oluyor (99,676 vs 99,677) ve ayrımı ortalamalar farkına indiriyor.

Seçilen uzunlukta taban, ortalamanın gizlediğini gösteriyor: tonemap yüksek
çıktıda ortalama 90,60 iken en kötü 2 sn birimi 85,97 (50. saniye); düşük çıktıda
ortalama 25,14 iken en kötü birim 5,88 (12. saniye). Ayrım 80,10 — ortalamalar
farkının (65,46) belirgin üstünde.

### 4.3 Son kısmi birim — atmak mı, tutmak mı (K4a)

T97'de son kısmi pencere düşüyordu: klip 1800 kareyse ve pencere 120 kareyse
kalan 0–119 kare hiç bakılmadan atılıyordu. Bu klibin son saniyesindeki bir
çöküşü gizler.

Ölçüm: her klip, crf 12'nin en kötü 0,5 sn'lik bloğunun hemen ardından kesildi;
sonra baştan kare atılarak kuyruk uzunluğu 1/3/6/15/30/45/60 kareye zorlandı.
Yani çöküş **bilerek** kuyruğa yerleştirildi. İki kural yan yana koşturuldu.

| Kuyruk | p1 atan | p1 tutan | p3 atan | p3 tutan |
|---|---|---|---|---|
| 1 kare (0,017 sn) | 1,7826 | 2,8385 | 2,4572 | **0,0282 — kaldı** |
| 3 kare (0,05 sn) | 1,7862 | 3,9017 | 2,4834 | 1,5174 |
| 6 kare (0,1 sn) | 1,7929 | 3,9826 | 2,4069 | 1,7274 |
| 15 kare (0,25 sn) | 1,7062 | 3,4769 | 2,5564 | 2,4043 |
| **30 kare (0,5 sn)** | **1,5530** | **2,7353** | **1,5301** | **2,3842** |
| 45 kare (0,75 sn) | 1,3779 | 2,5980 | 0,9327 | 2,4114 |
| 60 kare (1 sn) | 1,8300 | 1,8300 | 2,4936 | 2,4936 |

(Sayılar sinyal; gürültü iki kolda birebir aynı çıkıyor — kural özdeş klipte
hiçbir şey değiştirmiyor, yani tutmanın gürültü maliyeti yok.)

0,5 sn'lik kuyrukta tutmak p1'de sinyali %76 (1,5530 → 2,7353), p3'te %56
(1,5301 → 2,3842) yükseltiyor. Gizlediği şey somut: p3'ün son 0,5 saniyesinde
crf 12 gerçekte **84,1481**, atan kural aynı klipte en kötü birimi **92,3877**
diye raporluyor — 8,24 puanlık kör nokta.

Ama sınırsız tutmak da olmuyor. 1 karelik kuyrukta p3'te sinyal 0,0282'ye
çöküyor ve kural **kalıyor**: tek kare her iki yarışmacıda da aynı sahne kesme
artığını yakalıyor (14,5851 / 14,5569), yani `VmafNegMin`'in hastalığının aynısı
(§7).

**Karar: son kısmi birim, tam pencerenin dörtte birinden — 0,5 saniyeden —
uzunsa tutuluyor, kısaysa atılıyor.** `QualityMeter.MinimumUnitSeconds`.

Gerekçe doğrudan tablodan: **tutmanın atmayı geçtiği en kısa kuyruk 0,5 sn.**
Daha kısa her uzunlukta tutmak kaybediyor ya da p1'de kazanırken p3'te
kaybediyor — 0,25 sn'de p3 tutan 2,4043, atan 2,5564; 0,1 sn'de 1,7274 / 2,4069;
0,05 sn'de 1,5174 / 2,4834; 1 karede 0,0282 / 2,4572. 0,5 sn'de ikisi birden
kazanıyor (p1 2,7353 / 1,5530, p3 2,3842 / 1,5301). Seçilen değer güvenli
sınırın katı değil, güvenli sınırın kendisi — bu yüzden altına inilmiyor.

Aynı alt sınır sahne yolunda da geçerli: yarım saniyeden kısa bir sahne en kötü
sahne seçilemiyor.

### 4.4 Harita yokken davranış (K3)

`WorstScene` haritasız da çağrılabiliyor; harita `null` ise, boşsa, ya da klip
içine düşen kesme sayısı ikiden az birim üretiyorsa **sabit 2 sn'lik pencereye
düşülüyor**. Bu kuramsal bir kol değil: p3, üretim eşiğinde tam olarak bu yoldan
ölçüldü (30 saniyede tek sahne). İki yol da ölçüyle sabitlendi (§6).

Boş puan listesi artık savunmalı: `WorstScene` `PositiveInfinity` döndürmek
yerine `ArgumentException` fırlatıyor (K4b).

`WindowQualityMeasurement`'a dört alan **sona, varsayılan değerle** eklendi
(`VmafNegMin`, `VmafNegWorstScene`, `WorstSceneStartSeconds`,
`SceneWindowSeconds`). Var olan üye kaldırılmadı, adı değişmedi, sırası bozulmadı.

**Üretimde bu makinenin ölçtüğü fark henüz görünmüyor.** Tek üretim çağıranı
`ComplexityProbe`, `MeasureWindowAsync`'i 2 sn'lik pencerelerle çağırıyor ve
yalnız `VmafNegMean` okuyor. Ölçülen aralık tam olarak bir birim olduğu için en
kötü birim orada pencere ortalamasının kendisi; sahne sınırı da o çağrı yerinde
sabit pencereden farklı çıkamaz. Alanları okuyan üretim tüketicisi eklemek T104
kapsamında değil.


## 5. Kurulu metrik envanteri

`ffmpeg -filters`, karşılaştırma metrikleriyle sınırlı:

    TS identity          VV->V      Calculate the Identity between two video streams.
    .. libvmaf           VV->V      Calculate the VMAF between two video streams.
    TS msad              VV->V      Calculate the MSAD between two video streams.
    TS psnr              VV->V      Calculate the PSNR between two video streams.
    TS ssim              VV->V      Calculate the SSIM between two video streams.
    .. ssim360           VV->V      Calculate the SSIM between two 360 video streams.
    .. vmafmotion        V->V       Calculate the VMAF Motion score.
    T. xpsnr             VV->V      Calculate the extended perceptually weighted peak
                                    signal-to-noise ratio (XPSNR) between two video streams.

**SSIMULACRA2 ve butteraugli bu derlemede yok.** `libjxl` derlenmiş olsa da
karşılık gelen filtre listede geçmiyor. Kurulu olmadıkları için eklenmediler.

`ffmpeg -h filter=libvmaf`:

    libvmaf AVOptions:
       log_path          <string>     ..FV....... Set the file path to be used to write log.
       log_fmt           <string>     ..FV....... Set the format of the log (csv, json, xml, or sub). (default "xml")
       pool              <string>     ..FV....... Set the pool method to be used for computing vmaf.
       n_threads         <int>        ..FV....... Set number of threads to be used when computing vmaf. (default 0)
       n_subsample       <int>        ..FV....... Set interval for frame subsampling used when computing vmaf. (default 1)
       model             <string>     ..FV....... Set the model to be used for computing vmaf. (default "version=vmaf_v0.6.1")
       feature           <string>     ..FV....... Set the feature to be used for computing vmaf.

    framesync AVOptions:
       eof_action        <int>        (default repeat)   repeat / endall / pass
       shortest          <boolean>    (default false)
       repeatlast        <boolean>    (default true)
       ts_sync_mode      <int>        (default default)  default / nearest

Model yoklaması — aynı 320x240 klip kendisiyle karşılaştırılarak:

| `model=version=` | sonuç |
|---|---|
| `vmaf_v0.6.1` | 99,742838 |
| `vmaf_v0.6.1neg` | 99,742505 — **kullanılan model** |
| `vmaf_4k` | `Error initializing filters` — yok |
| `vmaf_4k_v0.6.1` | 100,000000 |
| `vmaf_float_v0.6.1` | 99,742788 |
| `vmaf_b_v0.6.3` | `Error initializing filters` — yok |

`vmaf_4k` adı kurulu değil; 4K modeli `vmaf_4k_v0.6.1` adıyla var. Bu turda
kullanılmadı: kaynaklar 1080p ve altında, 4K modeli o çözünürlükte fazla iyimser.
`n_subsample` var ve maliyeti düşürebilir; sayıları değiştirdiği için bu turda
açılmadı.

Değer katan ve kurulu olduğu halde eklenmeyen: `psnr` (XPSNR zaten onun
algısal ağırlıklı hali), `identity` ve `msad` (algısal değil), `vmafmotion`
(kalite değil hareket ölçüsü), `ssim360` (360 içerik yok).


## 6. Mutasyon kanıtı

`dotnet test -c Release --filter "QualityMeterTests"` — **19 ölçü, tamamı
geçiyor, atlanan 0.** ffmpeg gerektirenler `[FfmpegFact]`, tonemap zinciri
gerektiren `[TonemapFact]` (sınırı §8'de). Ölçünün içinde yetenek yoklayıp
sessizce dönen kol yok; iki sabiti karşılaştıran ölçü yok.

T97 mutasyonları, kaynak her seferinde geri alınarak:

| Mutasyon | Düşen ölçü |
|---|---|
| `NormalizeVmafCeiling` geri kondu (toplanmış dörtlüye) | `IdenticalClipReportsTheModelCeilingInsteadOfAForcedHundred`, `TwoNearLosslessRivalsKeepTheirOrderAboveTheCeilingBand` |
| Pencere adımı `scores.Count` yapıldı (tek pencere = tüm klip) | `WorstSceneAveragesOverTwoSecondBuckets`, `WorstSceneReportsTheWindowStartOnTheReferenceTimeline`, `WorstSceneFindsTheDamagedSectionTheMeanHides` |
| Tonemap öneki düşürüldü (`referencePrefix = ""`) | `TonemappedReferenceSeparatesTwoSdrQualities` |

T104 mutasyonları — pencere kuralı, sahne bağı, kısmi birim, boş liste savunması
ve haritalı geri düşüş tek tek bozuldu. Beşi de koşturuldu; koşum kaydı
`.calisma/T104/mutasyon.ps1` ve `mutasyon.log`:

| Mutasyon | Düşen ölçü | Sonuç |
|---|---|---|
| Sabit pencere `2 sn` → `1 sn` (`fps * SceneWindowSeconds` → `fps * 1.0`) | `WorstSceneAveragesOverTwoSecondBuckets`, `FixedWindowsDiluteTheSceneTheMapWouldIsolate` | 2 başarısız / 17 başarılı |
| Sahne bağı koparıldı (`SceneBounds(map, …)` → `SceneBounds(null, …)`) | `WorstSceneUsesSceneBoundariesWhenTheMapIsPresent`, `SceneBoundariesAreReadOnTheReferenceTimelineNotFromZero`, `SceneShorterThanHalfASecondIsNotTheWorstScene` | 3 başarısız / 16 başarılı |
| Kısmi birim eşiği `pencere/4` → `pencere` (T97'nin "kuyruğu at" kuralı) | `CollapseInTheTrailingHalfSecondIsNotDropped` | 1 başarısız / 18 başarılı |
| Boş liste savunması etkisizleştirildi (`Count == 0` → `Count == -1`) | `WorstSceneRejectsAnEmptyScoreList` | 1 başarısız / 18 başarılı |
| Geri düşüş eşiği `bounds.Count > 2` → `>= 2` (tek sahnelik harita sabit pencereye düşmez olur) | `MapWithASingleSceneFallsBackToTheFixedWindow` | 1 başarısız / 18 başarılı |

Beş koşumun hepsinde **atlanan 0**; mutasyonsuz temel koşum 19/19.

Hangi ölçü neyi tek başına tutuyor:

- `WorstSceneAveragesOverTwoSecondBuckets` — pencere uzunluğu. 600 kare @ 60 fps,
  yalnız 2,0–3,0 sn arası sıfır. 2 sn'lik pencere 50, 1 sn'lik 0, 5 sn'lik 80
  verir; ölçü 50 bekliyor. Tek bir sabitle değil, üç uzunluğun üç farklı
  cevabıyla ayrışıyor.
- `WorstSceneUsesSceneBoundariesWhenTheMapIsPresent` / 
  `FixedWindowsDiluteTheSceneTheMapWouldIsolate` — **aynı puan dizisi, iki yol.**
  Sahne sınırıyla en kötü birim 40,0 @ 2,5 sn; sabit pencereyle aynı hasar
  seyrelip 55,0 @ 2,0 sn oluyor. İkisi birden §4'ün karşılaştırmasını kodda
  tutuyor: biri düşerse yollardan biri sessizce diğerine dönüşmüş demektir.
- `SceneBoundariesAreReadOnTheReferenceTimelineNotFromZero` — sahne kesmeleri
  referans zaman çizgisinden okunuyor (offset 12,5 sn, kesme 15,0/18,0).
- `SceneShorterThanHalfASecondIsNotTheWorstScene` — yarım saniyeden kısa sahne en
  kötü seçilemiyor; 12 karelik sıfır dilim atlanıyor.
- `CollapseInTheTrailingHalfSecondIsNotDropped` / 
  `TrailingUnitShorterThanHalfASecondIsDropped` — kısmi kuyruk kuralının iki
  yüzü. 990 kare: son 0,5 sn sıfır → ölçü 0,0 @ 16,0 sn görüyor. 975 kare: son
  0,25 sn sıfır → ölçü onu atıyor ve 100,0 raporluyor. Eşiği hangi yöne
  kaydırırsan biri düşer.
- `MapWithASingleSceneFallsBackToTheFixedWindow` — harita **var** ama ikiden az
  birim üretiyorsa sabit pencereye düşülüyor. 600 kare @ 60 fps, kesmesiz 10 sn'lik
  harita: sabit yol 0,0 @ 2,0 sn buluyor, geri düşüş kapatılırsa tek birim klip
  ortalamasını (80,0 @ 0,0) veriyor. §4.4'ün p3'te ölçümle gösterdiği kol artık
  ölçüyle de tutuluyor.
- `WorstSceneRejectsAnEmptyScoreList` — boş puan listesi `ArgumentException`
  fırlatıyor, `PositiveInfinity` dönmüyor.

Bir not: `WorstSceneFindsTheDamagedSectionTheMeanHides` (ffmpeg'li bütünleşme ölçüsü)
pencere uzunluğunu **tutmuyor**; raporlanan başlangıcın raporlanan pencere
ızgarasına oturduğunu doğruluyor, o kadar. Uzunluğu tek başına tutan ölçü
`WorstSceneAveragesOverTwoSecondBuckets`.


## 7. `VmafNegMin` kararı (K5)

T97 `VmafNegMin`'i arayüz kaydına ekledi ama belgede "modelin kendi gürültüsü"
dedi. Bir alan hem taşınıp hem kullanılamaz sayılamaz; T104 karar veriyor.

**Ölçüm — min içeriği değil modeli ölçüyor.** Bit düzeyinde özdeş üç klipte:

| İçerik | özdeş min | özdeş ortalama |
|---|---|---|
| p1 | 97,4257 | 98,8005 |
| p2 | 97,4253 | 97,4396 |
| p3 | 97,4256 | 99,8882 |

Üç ayrı içerik, hiçbir kalite kaybı yokken **aynı sayı**: 97,425. Bu bir içerik
ölçüsü değil, modelin tek karelik taban salınımı. §3'teki 0,79 da aynı ailedendir
(sahne kesmesinde hizalanma artığı).

**Ölçüm — gerçek yarışmacıları ayıramıyor.** Aynı referansa karşı crf 8 / crf 12:

| İçerik | crf8 min | crf12 min | sinyal | gürültü |
|---|---|---|---|---|
| p1 | 4,3147 | 4,3011 | 0,0136 | 1,3748 |
| p2 | 95,2609 | 94,2846 | 0,9763 | 0,0143 |
| p3 | 2,2023 | 2,2041 | **−0,0018** | 2,4626 |

p1'de sinyal gürültünün yüzde biri; p3'te **sıra ters dönüyor** — kötü kodlama
daha iyi görünüyor. Yalnız p2 ayırıyor, o da hiçbir yolun ayıramadığı durağan
klip; orada min klip ortalamasına yapıştığı için "ayırıyor" görünüyor.

Aynı tabloda p10 ve en kötü birim aynı içeriklerde çalışıyor (p1 p10 sinyali
1,9059; p3 2,6384). Yani boşluğu dolduran alan zaten var.

**Karar: kalite yargısı olarak kullanılmıyor, tanı alanı olarak kalıyor.**

- `QualityScore.VmafNegMin` **kalıyor**. Gerekçesi tek: `bench measure` ve
  `bench measure-tonemapped` çıktısında görünür ve "en kötü kare hangi değere
  indi" sorusunu ölçüm turlarında yanıtlıyor — §3'teki 0,79'u fark ettiren buydu.
  Tanıya yarıyor, karara yaramıyor.
- **Hiçbir karar bu alana bakmamalı.** Plan hesabı, A/B karşılaştırması,
  kullanıcıya gösterilen kalite — hiçbiri `VmafNegMin` okumamalı. Taban gerekiyorsa
  `VmafNegWorstScene`, kuyruk gerekiyorsa `VmafNegP10`.
- `WindowQualityMeasurement.VmafNegMin` de **kalıyor**, aynı gerekçeyle ve aynı
  yasakla. Kaldırmak `IQualityMeasurement`'ı değiştirmeyi gerektirir; o dosya
  T104'ün sahibinde değil ve alan zararsız (okuyan yok).

Kısaca: silinmedi çünkü tanıda işe yarıyor; terfi de etmedi çünkü üç içerikte
ölçüldü ve kalite ayıramadı.


## 8. Bu turun sınırları

Ölçülmemiş olanı ölçülmüş göstermemek için:

- **`[TonemapFact]`'in atlama yüzeyi kriterden geniş (K4d).** Kriter yalnız
  tonemap zincirinin yokluğunda atlamayı öngörüyordu; öznitelik `zscale`
  **veya** `tonemap` filtresi eksikse de atlıyor. Daraltmak
  `tests/VidShrink.Tests/FrameGrabberTests.cs` içinde ve o dosya T104'ün
  sahibinde değil — bu yüzden **sınır olarak adlandırılıyor, düzeltilmiyor.**
  Ölçüm makinesinde iki filtre de kurulu, atlanan ölçü sıfır; yani bu yüzey
  burada boş. `zscale`'i olmayan bir makinede tonemap ölçüsü sessizce atlanır ve
  bunu kimse fark etmez. Daraltma T104 dışında bir tur ister.
- **İçerik çeşitliliği yok.** Üç klip aynı ana kaynaktan. Farklı tür (konuşan
  kafa, ekran kaydı, animasyon) ve farklı kare hızı **ölçülmedi**.
- **Doğru ve ince harita ölçülmedi.** §4.2'deki eşik indirme, T101'in kastettiği
  doğrulanmış ince harita değil.
- **Üretim tüketicisi yok.** `VmafNegWorstScene` ve kardeşlerini okuyan üretim
  kodu hâlâ yok (§4.4). Ölçü doğru, ama kimse ona bakmıyor.
- **Süre/hız iddiası yok.** Ölçümler beş ajanın koştuğu paylaşımlı makinede
  yapıldı; hiçbir zaman sayısı rapora girmedi. Bir VMAF koşumu ffmpeg'i geçici
  olarak düşürdü, aynı komut tekrarında geçti — sayılar o tekrardan.
- XPSNR/SSIM kare başına dökümü hâlâ okunmuyor; taban yalnız VMAF-NEG'de var.


## Yeniden üretim

    ffmpeg -ss 00:02:00 -t 60 -i kaynak-1080p60-hdr-17dk.mp4 -map 0:v:0 -c copy parca-1.mkv

    TM=zscale=t=linear:npl=100,tonemap=hable:desat=0,zscale=p=bt709:t=bt709:m=bt709:r=limited,format=yuv420p
    ffmpeg -i parca-1.mkv -vf "$TM" -c:v libx264 -preset veryfast -crf 18 -an sdr-yuksek.mp4
    ffmpeg -i parca-1.mkv -vf "$TM" -c:v libx264 -preset veryfast -b:v 300k -maxrate 400k -bufsize 800k -an sdr-dusuk.mp4

    dotnet run -c Release --project tools/VidShrink.Bench -- measure-tonemapped parca-1.mkv sdr-yuksek.mp4
    dotnet run -c Release --project tools/VidShrink.Bench -- measure-tonemapped parca-1.mkv sdr-dusuk.mp4

T104 tabloları (§4.2, §4.3, §7) için üç klip ve altı kodlama:

    ffmpeg -t 30 -i parca-N.mkv -map 0:v:0 -c copy pN-ref.mkv
    ffmpeg -i pN-ref.mkv -c:v libx264 -preset veryfast -crf 8  -pix_fmt yuv420p10le \
      -color_primaries bt2020 -color_trc smpte2084 -colorspace bt2020nc pN-crf8.mkv
    ffmpeg -i pN-ref.mkv -c:v libx264 -preset veryfast -crf 12 -pix_fmt yuv420p10le \
      -color_primaries bt2020 -color_trc smpte2084 -colorspace bt2020nc pN-crf12.mkv

Her çift için kare başına VMAF-NEG günlüğü alınır (`pN-ref` kendisiyle, crf 8 ile,
crf 12 ile); gürültü özdeş günlükten, sinyal iki kodlama günlüğünün farkından
hesaplanır. Sahne satırları `SceneDetector.BuildMapAsync` haritasıyla, eşik
parametresi değiştirilerek üretildi.

Ölçüm düzeneği ve ham günlükler **hâlâ yerinde**: `.calisma/T104/` (sonda programı,
`mutasyon.ps1`, `mutasyon.log`, `son.log`) ve `.calisma/is/` (klipler ve kare başına
JSON günlükleri), yaklaşık 1,4 GB. T104 ajanının silme izni yoktu; temizlik T0'da,
worktree kaldırılırken. `.gitignore`'da, git'e sızmadı.

§2 ve §4 tabloları kare başına JSON günlüğünden çıkarıldı; günlüğü doğrudan almak
için filtreye `libvmaf=model=version=vmaf_v0.6.1neg:log_fmt=json:log_path=...`
verilir. `bench measure-tonemapped` çıktısı bu çözümlemeyle birebir uyuştu
(ortalama 90,59568; XPSNR 40,35355) — çözümleme üretim yolunun kendisini ölçüyor.
