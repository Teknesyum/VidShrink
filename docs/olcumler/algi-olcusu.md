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

    [0:v]<test-normalizasyonu>,settb=AVTB,setpts=N[t];
    [1:v]<tonemap-öneki><referans-normalizasyonu>,settb=AVTB,setpts=N[r];
    [t][r]<metrik>

> **T116:** bu satır T110'a kadar kilitsiz hâliyle duruyordu ve §9.11'in kirlenmiş
> satır envanterine de girmemişti. Kilidin kendisi §9.5'te, kaynağı
> `MeasureFilterGraph.Build` (`QualityMeter.cs:86-88`).

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

> **T116 düzeltmesi:** T110'un buraya koyduğu damga yanlış yere basılmıştı.
> §9.10 yalnız **p1**'i ölçtü (97,4257, kilitli ve kilitsiz aynı). Aşağıdaki
> yan bulgunun 97,4256'sı §7'nin özdeş tablosunda **p3**'ün sayısıdır (:556) ve
> **yeniden ölçülmedi.** İki ayrı klibin sayısı tek damgada birleştirilmişti.

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

> **T110 — `VMAF-NEG min` satırı kaymış ölçüyle yazıldı, geçersiz** (§9.11).
> Tablonun kalan satırları yeniden **ölçülmedi.**

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

> **T110 — kaymış ölçüyle yazıldı, geçersiz.** Aşağıdaki 0,79 bir kalite
> olayı değil, kare kilidi olmayan ölçerin yanlış eşlediği bir karedir.
> Tanı ("hizalanma artığı") doğru çıktı, nedeni o turda bilinmiyordu
> (§9.2). Bu çift kilitli ölçüyle **yeniden ölçülmedi.**

Dikkat çeken tek şey `VmafNegMin`: iyi çıktıda 0,79. 90,6 ortalamalı bir klipte
0,79 puanlı bir kare kalite olayı değil, sahne kesmesinde tek karelik bir
hizalanma/ani değişim artığı. Kullanılabilir bir taban değil (§4).

> **T110 — kaçırılmış ipucu.** `settb` tek başına zaman tabanını birleştirir,
> kare **numarasını** birleştirmez; kaymayı kaldıran `setpts=N`'dir (§9.5,
> mutasyon M6). Deneme doğruydu, eksik kurulmuştu.

**Yan bulgu, düzeltilmedi.** `xpsnr` filtresi bu çiftte uyarı basıyor:
`not matching timebases found between first input: 1/15360 and second input
1/1000`. Her iki zincirin sonuna `settb=AVTB` eklenip ölçüm tekrarlandı; sonuç
kuruşu kuruşuna aynı çıktı (38,9113 / 42,7523 / 43,7238). Uyarı bu girdide
ölçüyü kaydırmıyor, o yüzden üretim zinciri değiştirilmedi. Farklı kare hızlı
çiftte tekrar bakılmalı — bu turda ölçülmedi.


## 4. En kötü birim — 2 saniyelik sabit pencere

Sorun: filmin tamamındaki tek en kötü kare kullanıcıyı ilgilendirmiyor, hem de
ölçülemiyor. §2'nin yan bulgusu: özdeş 1080p içerikte min 97,43. Buna karşılık
gerçekten iyi bir kodlamada min 0,79 çıkabiliyor (§3 — **kaymış
ölçüyle yazıldı, geçersiz**, §9.11). Aynı sayı hem özdeş
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

> **T110:** bu tablonun p1 satırı kilitli ölçüyle yeniden ölçüldü ve
> **değişmedi** (97,4257). p2 ve p3 satırları **ölçülmedi.** (§9.10)

**Ölçüm — min içeriği değil modeli ölçüyor.** Bit düzeyinde özdeş üç klipte:

| İçerik | özdeş min | özdeş ortalama |
|---|---|---|
| p1 | 97,4257 | 98,8005 |
| p2 | 97,4253 | 97,4396 |
| p3 | 97,4256 | 99,8882 |

Üç ayrı içerik, hiçbir kalite kaybı yokken **aynı sayı**: 97,425. Bu bir içerik
ölçüsü değil, modelin tek karelik taban salınımı. §3'teki 0,79 da aynı ailedendir
(sahne kesmesinde hizalanma artığı).

> **T110 — kaymış ölçüyle yazıldı, geçersiz.** p1 satırı yeniden ölçüldü:
> kilitli ölçüde crf8 min **92,5920**, crf12 min **89,4449**; sinyal 0,0136
> değil **3,1471**. Aşağıdaki 4,3'ler kodlama kalitesini değil yanlış kare
> eşlemesini ölçüyordu. p2 ve p3 **ölçülmedi**; K5 kararı bu turda
> değiştirilmedi (§9.10).

**Ölçüm — gerçek yarışmacıları ayıramıyor.** Aynı referansa karşı crf 8 / crf 12:

| İçerik | crf8 min | crf12 min | sinyal | gürültü |
|---|---|---|---|---|
| p1 | 4,3147 | 4,3011 | 0,0136 | 1,3748 |
| p2 | 95,2609 | 94,2846 | 0,9763 | 0,0143 |
| p3 | 2,2023 | 2,2041 | **−0,0018** | 2,4626 |

p1'de sinyal gürültünün yüzde biri; p3'te **sıra ters dönüyor** — kötü kodlama
daha iyi görünüyor.

> **T116:** yukarıdaki tablo kilitsiz ölçüden gelir ve §9.10'da damgalıdır; bu
> cümle damgasız kalmıştı. Kilitli ölçüde **p1 ayırıyor**: sinyal 0,0136'dan
> **3,1471**'e, gürültü 1,3748'e karşı. "p1'de sinyal gürültünün yüzde biri"
> gözlemi kilitli ölçüde **yok**. p2 ve p3 satırları **yeniden ölçülmedi**, o
> yüzden "sıra ters dönüyor" ve "yalnız p2 ayırıyor" cümleleri için
> **ölçülmedi** geçerlidir. Yalnız p2 ayırıyor, o da hiçbir yolun ayıramadığı durağan
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

> **T116:** bu cümlenin "üç içerikte ölçüldü ve kalite ayıramadı" kısmı
> **kilitsiz ölçüye dayanıyor.** Kilitli ölçüde p1 ayırıyor (§9.10); p2 ve p3
> **yeniden ölçülmedi.** Bugünkü doğru ifade: üç içerikte kilitsiz ölçüldü,
> birinde kilitli yeniden ölçüldü ve orada ayırdı, ikisi ölçülmedi. K5 kararı
> yine de değiştirilmedi — kararı yeniden açmak ayrı bir iştir.


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


## 9. Kare kilidi — ürün ölçeri kareleri damgayla eşliyordu (T110)

T106 aynı kusuru `tools/VidShrink.Bench` içinde bulup kapattı. Ürün ölçeri
(`src/VidShrink.Ffmpeg/QualityMeter.cs`) kapatılmamıştı; `--measured-quality`
yolundan geçen kalibrasyon çıpaları oradan geçiyordu. Bu bölüm o açığı ürün
kodunda kendi ölçümüyle gösteriyor, kapatıyor ve kapanışın bedelini yazıyor.

Ölçüm makinesi Windows 11, `ffmpeg` (libvmaf + libzimg), altı ajanla paylaşımlı.
**Bu bölümde hiçbir süre sayısı yok**; kalite ve kare sayıları paylaşımdan
etkilenmiyor, tekrarlanabilirlik §9.7'de ölçüldü. Üreten komutlar "Yeniden
üretim" bölümünde.

### 9.1 Kaymayı üreten şey kabın `start_time`'ı değil — ölçüldü

İlk hipotez ("video akışının `start_time`'ı sıfır değilse kayma olur") **yanlış
çıktı.** `-output_ts_offset 0.02` ile üretilen tek akışlı bir dosyanın
`start_time`'ı `0,020000` okunuyor ama filtre grafiğine `0` girdi; ölçüm
kilitli/kilitsiz kuruşu kuruşuna aynı çıktı (VMAF-NEG ortalama 90,1189 ve
91,6715; x264 ve x265).

`showinfo` ile ölçülen gerçek davranış:

| dosya | akışlar (`start_time`) | grafiğe giren ilk üç `pts_time` |
|---|---|---|
| `parca-1.mkv` | video 0,000000 | 0 / 0,016 / 0,033 |
| `parca-2.mkv` | video 0,020000, ses 0,000000 | **0,02** / 0,036 / 0,053 |
| `ref2.mkv` (4 sn kesit, ses korunmuş) | video 0,020000, ses 0,000000 | **0,02** / 0,036 / 0,053 |
| aynı kesit, `-an` ile sesi atılmış | video 0,020000 | 0 / 0,016 / 0,033 |
| sentetik `-output_ts_offset 0.02` | video 0,020000 | 0 / 0,033 / 0,067 |

Model: **grafiğe giren kayma = video akışının `start_time`'ı eksi kaptaki en
erken akışın `start_time`'ı.** ffmpeg kabın en erken damgasını sıfırlıyor; video
tek başınaysa kendi kaymasını da götürüyor, yanında sıfırdan başlayan bir ses
akışı varsa götürmüyor. Beş dosyanın beşinde de model `showinfo` ölçümüyle
uyuştu. Ürün koduna giren `TimestampOffsetSecondsAsync` tam bu farkı hesaplıyor.

Kodlanmış çıktılar bu yüzden hep sıfır kaymalı: `p2-x265.mkv` kabının
`start_time`'ı `0,017000` ama tek akış olduğu için grafiğe `0`'dan giriyor.
Kayma **çift içindeki farktan** doğuyor, tek dosyanın damgasından değil.

### 9.2 Kaynak × kilit ızgarası

Kaynaklar `.calisma/kaynak/parca-{1,2}.mkv`'nin ilk 4 saniyesi (`-c copy`,
1920x1080 60 fps HDR10, 240 kare). `ref1` temiz (kayma 0), `ref2` kaymış
(0,020 s = 1,20 kare). Testler aynı kaynaklardan `libx265 -crf 32` ve
`libsvtav1 -crf 45`, iş parçacığı sabitlenmiş.

| kaynak | kayma | kodlayıcı | kilit | eşlenen kare | ortalama | p10 | min | `<1` kare |
|---|---|---|---|---|---|---|---|---|
| ref1 | 0 | x265 | yok | 240 | 40,6656 | 33,1747 | **2,5864** | 0 |
| ref1 | 0 | x265 | **var** | 240 | 41,2331 | 33,4906 | **30,2347** | 0 |
| ref1 | 0 | AV1 | yok | 240 | 62,0461 | 53,1884 | **3,8497** | 0 |
| ref1 | 0 | AV1 | **var** | 240 | 62,8468 | 53,7968 | **49,7269** | 0 |
| ref2 | 1,20 kare | x265 | yok | **239** | 81,4185 | 81,2890 | 80,9937 | 0 |
| ref2 | 1,20 kare | x265 | **var** | **240** | 81,4366 | 81,3168 | 81,0174 | 0 |
| ref2 | 1,20 kare | AV1 | yok | **239** | 93,8184 | 93,7167 | 93,6651 | 0 |
| ref2 | 1,20 kare | AV1 | **var** | **240** | 93,8408 | 93,7538 | 93,7025 | 0 |

**Sözleşmenin beklentisi tutmadı ve bu bir kusur değil.** Kriter "temiz kaynakta
fark çıkmamalı; çıkıyorsa kilidin kendisi bozuktur" diyordu. Fark **temiz
kaynakta** çıktı. Kilit bozuk değil: kare başına günlükler kilitsiz koşumda 240
karenin **6'sının** yanlış eşlendiğini gösteriyor — 29/30, 133/134, 205/206:

| kare | kilitsiz | kilitli |
|---|---|---|
| 29 | 31,1700 | 36,8686 |
| 30 | **7,3791** | 45,7804 |
| 133 | 33,6985 | 41,4162 |
| 134 | **2,5864** | 39,6974 |
| 205 | 44,0798 | 54,0565 |
| 206 | **5,9679** | 44,0571 |

**T116 yeniden ölçtü — 234 değil 232.** Yukarıdaki altı satır doğru ama sayım
değil: ham günlükte **8 kare** iki koşumda farklı, **232 kare** aynı. Farklı olan
sekiz kare 29, 30, **31**, 133, 134, **135**, 205, 206. Altısı bir puandan fazla
oynuyor (tablodakiler); kalan ikisi kilitle **aşağı** iniyor — 31. kare
36,7814 → 36,0200 (**−0,7613**), 135. kare 49,9762 → 49,9433 (−0,0328). Kilit
kare **eklemiyor**, yanlış eşlenmiş altı kareyi **kaldırıyor**: min 2,59'dan
30,23'e, ortalama +0,57.

Izgaranın ref1 satırları bu turda rakamı rakamına yeniden üretildi (kilitsiz
40,6656 / 33,1747 / 2,5864; kilitli 41,2331 / 33,4906 / 30,2347). ref2'nin
kilitsiz satırı da birebir aynı çıktı (81,4185 / 81,2890 / 80,9937).

**ref2'nin kilitli satırında eşlenen kare sayısı yeniden üretilemedi.** Tablo
**240** diyor; bu turda kesilen `ref2.mkv` **239 kare** taşıyor ve kilitli koşum
da 239 eşliyor (81,4372 / 81,3171 / 81,0174). Fark ölçümde değil kesme adımında:
kaptaki 0,020 s'lik kayma yüzünden `-t 4` bir kareyi dışarıda bırakıyor. T110'un
ref2'si 240 kare taşımış olmalı. Belgenin sayısı geri çekilmedi; yeniden üretimin
sayısı yanına yazıldı, hangisinin doğru olduğu **ölçülmedi** — iki dosya artık
karşılaştırılamıyor.

Nedeni ölçüldü: referansın kendi damgaları düzgün ızgarada değil. `ref1.mkv`'nin
30. karesi `0,501000`'de, testin karşılık gelen karesi `0,500000`'da. Referans
damgası testinkini geçtiği anda framesync bir önceki referans karesini
tekrarlıyor. **`start_time` sıfır olsa bile ölçü kayıyor;** kilit bunu da
kapatıyor.

### 9.3 Kontrol — ref2 hücreleri hizalama hakkında bir şey ölçmüyor

ref2 satırlarında kilidin etkisi 0,02 puan. Bunu "60 fps'te kayma zararsız" diye
okumak §9.2'nin bulduğu hatanın aynısını tekrarlamak olurdu. Kontrol: `ref2`,
kendi içeriğinin **kasten 2 kare kaydırılmış** ve neredeyse kayıpsız
(`libx265 -crf 16`) kopyasına karşı ölçüldü.

| ölçüm | ortalama | min |
|---|---|---|
| ref2 vs 2 kare kaydırılmış crf16 kopya | 93,0121 | 92,8769 |
| ref2 vs kaydırılmamış crf16 kopya | 92,9542 | 92,8613 |

İki kare kaydırmak bu klipte skoru **0,06 puan** oynatıyor — yani klip kaymayı
neredeyse hiç görmüyor. **ref2 hücreleri kilidin işe yarayıp yaramadığını
ölçmüyor;** ızgarada duruyorlar çünkü kayma gerçekten oradaydı, ama duyarlılık
başka bir düzenekten gelmek zorunda (§9.4). Kaymış kaynağın zararsız görünmesi
kaynağın durgunluğundandır, kaymanın masumiyetinden değil.

### 9.4 Kayma kare cinsinden ne yapıyor — duyarlı düzenek

`testsrc2 320x240 30 fps 3 sn` (hareketli), `libx264 -crf 12` kaynak; test aynı
kaynağın `-crf 16` yeniden kodlaması. Kaynağın video akışı `-itsoffset` ile
geciktirilip yanına sıfırdan başlayan sessiz ses akışı konarak §9.1'deki kayma
üretildi.

| kayma | kare cinsinden | kilitsiz ortalama | kilitli ortalama |
|---|---|---|---|
| 0 ms | 0,00 | 98,3724 | 98,3724 |
| 10 ms | 0,30 | **52,3008** | 98,3724 |
| 20 ms | 0,60 | **52,3008** | 98,3724 |
| 40 ms | 1,20 | **37,8810** | 98,3724 |
| 50 ms | 1,50 | **37,8810** | 98,3724 |

Yukarıdaki beş satır bu turda rakamı rakamına yeniden üretildi.

**T110'un bir kare çıpası çevrimseldi; geri çekildi.** Belge "aynı kaynağın
bilerek bir kare kaydırılmış kodlaması (ilk kare atılıp `setpts=N/FR/TB`)
52,3008 veriyor" diyordu. Ölçüldü: o tarifle üretilen dosya **65,7226** veriyor,
52,3008 vermiyor. 52,3008'i veren dosya `-itsoffset 0.020` kopyasının **kendisi**,
yani ızgaranın 20 ms satırındaki `k0.020.mkv`. Yani çıpa kendi ölçtüğü sayıyla
karşılaştırılmış; bağımsız bir kanıt değildi.

Çevrimsel olmayan çıpalar ölçüldü. Kaydırma **içeriğe** uygulanır, damgaya değil
(`select=gte(n\,K),setpts=N/FR/TB`, sonra `libx264 -crf 16`), ve sonuç
**kilitli** ölçülür — böylece ölçülen şey kaymanın kendisi olur, ölçerin kusuru
değil:

| gerçekten kaydırılmış içerik | kilitli ortalama | karşılığı olan kilitsiz damga kayması | kilitsiz ortalama | fark |
|---|---:|---|---:|---:|
| kaydırma yok | 98,3724 | 0 ms | 98,3724 | 0 |
| 1 kare | **52,1174** | 10 ms ve 20 ms | 52,3008 | 0,18 |
| 2 kare | **37,4526** | 40 ms ve 50 ms | 37,8810 | 0,43 |

`(1, 2]` bacağının eksik çıpası budur. Yarım kareden küçük bir damga kayması tam
bir kare kaydırmaya dönüşüyor; T106'nın ana iddiası ürün kodunda doğrulandı.
Zarar kaymanın milisaniyesine değil **kare cinsinden tavanına** bağlı: (0, 1]
kare bir kare, (1, 2] kare iki kare kaydırıyor — iki bacağın da çıpası artık
ölçülmüş. Kilitli sütun her kaymada 98,3724 — onarım tam.

**Yan ölçüm, 0 ms satırının tarifi.** 0 ms hücresi ancak `src.mp4` doğrudan
kullanıldığında 98,3724 veriyor. Aynı dosyanın `-itsoffset 0` ile sessiz ses
akışının yanına konmuş mkv kopyası kilitsiz **81,8084** veriyor ve 90 yerine
**91 kare** üretiyor: mkv'nin 1/1000 zaman tabanı 60→30 fps ızgarasını
yuvarlıyor ve kayma sıfırken bile kare eşlemesini bozuyor. Kilitli ölçüde aynı
dosya 98,3724 ve 90 kare. Izgaranın 0 ms satırı `src.mp4` ile üretilmiştir.

### 9.5 Kilidin kendisi

`settb=AVTB,setpts=N`, her iki zincirin sonunda — **T106'nın kullandığı kilidin
birebir aynısı** (`MeasureFilterGraph.FrameLock`). Farklı bir kilit seçilmedi;
iki yerde iki ayrı kilit bir sonraki ayrışmanın tohumu olurdu. Grafik artık
`MeasureFilterGraph.Build` üzerinden kuruluyor:

    [0:v]<test-normalizasyonu>,settb=AVTB,setpts=N[t];
    [1:v]<tonemap-öneki><referans-normalizasyonu>,settb=AVTB,setpts=N[r];
    [t][r]<metrik>

Telafi katsayısı yok; kayma dengelenmiyor, kaldırılıyor.

### 9.6 Kaymış kaynak raporlanıyor — karar

Sessizce doğru sonuç üretmek yetmez; ölçüyü okuyan, ölçerin neyi onardığını
görebilmeli. Karar: kayma günlüğe değil **`QualityScore.Alignment` alanına**
yazılıyor.

    QualityScore.Alignment: { ReferenceOffsetSeconds, TestOffsetSeconds,
                              FrameDurationSeconds, ShiftSeconds, ShiftFrames,
                              Shifted, Note }

Gerekçe: alan `bench measure` çıktısında ve `QualityScore`'u okuyan her
tüketicide görünüyor, ayrı bir günlük kanalı gerekmiyor, ve karşılaştırılamaz
sonuçlarda bile doluyor (`Comparable=false` dönen erken çıkış da `Alignment`
taşıyor). `Note` yalnız kayma varken dolu:

    "Kaynak ve test zaman damgaları 20 ms (1.2 kare) ayrık; kareler zaman
     damgasına değil kare indeksine eşlendi."

> **T116:** yukarıdaki metin çalışan koddan okundu (60 fps ref2 çifti,
> `ShiftFrames = 1.2`). Belge önce "1,2 kare" yazıyordu; kod
> `CultureInfo.InvariantCulture` ile biçimliyor, ondalık ayırıcı **nokta**.
> Arayüzün geri kalanı Türkçe olduğu için bu bir tutarsızlık, ama düzeltmesi
> `QualityMeter.cs`'te ve o dosya T116'nın `owns`'unda değil — **değiştirilmedi**,
> yalnız belgedeki alıntı koda uyduruldu.

Ölçüsü `ShiftedSourceIsReportedNotSilentlyRepaired` ve
`VideoStartAheadOfTheContainerIsTheOffsetThatReachesTheFilterGraph`. İkincisi
§9.1'deki modeli pimliyor: video akışı geciktirilmiş dosyada 0,02, **her şeyi**
geciken dosyada 0.

### 9.7 Kalibrasyon çıpaları — eski/yeni

`--measured-quality` yolu: `ComplexityProbe.SplitSampleAsync` her pencere için
bir örnek kodluyor ve `IQualityMeasurement.MeasureWindowAsync` ile kaynağa karşı
ölçüyor; sonuçlar `ComplexityProfile.WithProbeQuality` ile tek bir
`QualityAnchor`'a toplanıyor. Bu yolun tamamı `QualityMeter`'dan geçiyor, yani
**bugüne kadarki bütün çıpalar kaymış ölçüyle konmuştu.**

İki ikili aynı ağaçtan, biri kilitli biri kilitsiz derlendi; aynı komut koşuldu.

| kaynak | çıpa | eski (kilitsiz) | yeni (kilitli) | fark |
|---|---|---|---|---|
| parca-1 (temiz) | 1 | 86,16 | 87,43 | +1,27 |
| parca-1 | 2 | 86,95 | 86,95 | **ölçüldü, değişmedi** |
| parca-1 | 3 | **75,07** | **87,20** | **+12,13** |
| parca-1 | `QualityAnchor.VmafNeg` | 82,73 | 87,19 | **+4,47** |
| parca-2 (kaymış) | 1 | 92,44 | 92,48 | +0,04 |
| parca-2 | 2 | 91,11 | 91,13 | +0,02 |
| parca-2 | 3 | 91,10 | 91,12 | +0,02 |
| parca-2 | `QualityAnchor.VmafNeg` | 91,55 | 91,58 | +0,03 |

`QualityAnchor` satırları çıpaların ortalaması. **Bu iki satır ölçülmedi,
türetildi:** `bench` çıpaları iki basamağa yuvarlayarak bastığı için ortalama
yuvarlanmış değerlerden hesaplandı; alanın kendisi okunmadı.

Üç bağımsız koşumda parca-1'in iki sütunu da rakamı rakamına aynı çıktı
(86,16 / 86,95 / 75,07 ve 87,43 / 86,95 / 87,20) — bu ölçü kararsız değil.

En büyük hasar yine **temiz** kaynakta: tek bir çıpa 12,13 puan yanlıştı ve
plana giren çıpayı 4,47 puan aşağı çekiyordu.

### 9.8 Mutasyon kanıtı

Her mutasyon tek başına uygulanıp `dotnet test -c Release --filter
QualityMeterTests` koşuldu (24 ölçü).

| # | mutasyon | sonuç |
|---|---|---|
| M0 | mutasyonsuz | 24 yeşil |
| M1 | kilit yalnız `[t]`'den kaldırıldı | **9 kırmızı** |
| M2 | kilit yalnız `[r]`'den kaldırıldı | **9 kırmızı** |
| M3 | kilit iki zincirden de kaldırıldı | **1 kırmızı** |
| M4 | `[r]`'de `setpts=N` yerine `setpts=N+1` | **7 kırmızı** |
| M5 | iki zincirde birden `setpts=N` yerine `setpts=N+1` | 24 yeşil — **eşdeğer mutasyon** |
| M6 | `settb=AVTB` kaldırıldı | **3 kırmızı** |
| M7 | `TimestampOffsetSecondsAsync` hep 0 dönüyor | **2 kırmızı** |
| M8 | etiketsiz taraf kapısı kaldırıldı (§9.9) | **1 kırmızı** |

**M4 sözleşmenin istediği kontrol**: bir kare kaydıran kilit ölçüye takılıyor.
T106 denetçisinin bulduğu tuzak — `Assert.Contains("settb=AVTB,setpts=N", ...)`
gibi dizgi karşılaştıran bir ölçü — burada yok; M4'ü öldüren ölçüler skor
**dizisini** karşılaştırıyor.

**M5 eşdeğer; bu bir iddia değil, ölçüm.** Aynı çifte `setpts=N` ve `setpts=N+1`
ile iki kez ham libvmaf koşuldu; kare başına puan dizileri **bit düzeyinde aynı**
(90 kare, ortalama 98,372383, dizinin SHA-256 öneki iki koşumda da
`01557f15134c6e11`, dizi eşitliği `True`). İki akış eşit kaydığı için eşleme
değişmiyor. Bu mutasyonu öldüren bir davranış ölçüsü **yok ve olamaz**; kırmızıya
döndürecek tek şey dizgi karşılaştırmak olurdu, ki reddedilen tuzak tam odur.
Yakalanması gereken mutasyon tek taraflıdır ve M4 onu yakalıyor.

M3'ün yalnız 1 kırmızı vermesi de ölçülmüş bir olgudur: kilit iki taraftan da
kalkınca aynı zaman tabanlı mp4 çiftleri hâlâ doğru eşleşiyor; ölen tek ölçü
kaymış kaynağı kullanan `SubFrameTimestampSlipDoesNotCostTheScoreAWholeFrame`.

### 9.9 Etiketsiz taraf artık sayı bastırmıyor

T95 bağımsız olarak şunu buldu: `ColorFilter` etiketsiz girdiye varsayım
uyduruyor (`?? (hdr ? "bt2020" : "bt709")`) ve `ColorIncompatibility` yalnız
HDR/SDR ayrışmasına baktığı için etiketsiz taraf ölçere kadar gidiyordu. Ölçer
susup bir uzay seçiyor ve sayı basıyordu.

Kapatılan **dar** hâli: bir taraf tamamen etiketsizken öteki taraf bt709 **dışı**
bir ana renk / aktarım / matris taşıyorsa iki taraf farklı uzaylardan normalize
edilir; o durumda ölçü `Comparable=false` dönüyor ve **hiçbir sayı basmıyor.**
Etiketsiz ile bt709 çifti karşılaştırılabilir kalıyor; varsayımla kanıt orada
çelişmiyor ve `Bt709MetadataOnlyRemuxMatchesTheIdenticalCopyScore` bu eşdeğerliği
zaten tutuyor.

Ölçüsü `UntaggedSourceAgainstANonBt709TagIsRefusedInsteadOfAssumed`, mutasyonu
M8. **Ölçülmedi:** etiketsiz bir kaynağın gerçek uzayı bt709 değilse ölçünün ne
kadar yanıldığı. Kapı yalnız çelişkiyi yakalıyor, varsayımın kendisini
doğrulamıyor.

### 9.10 §7'nin min tablosu yeniden ölçüldü — K5'in ikinci kanıtı çöktü

§7 `VmafNegMin`'i "kalite yargısı olarak kullanılmıyor" diye sınırlandırırken iki
ölçüme dayanıyordu. İkisi de p1 için, belgenin kendi "Yeniden üretim" tarifiyle
yeniden kuruldu ve kilitli/kilitsiz ölçüldü (1800 kare; `<1` puanlı kare her
hücrede 0).

| çift | kilitsiz min | kilitli min | kilitsiz ort. | kilitli ort. |
|---|---|---|---|---|
| p1-ref vs p1-ref (özdeş) | 97,4257 | **97,4257** | 98,8005 | 98,8005 |
| p1-ref vs crf8 | **4,3022** | **92,5920** | 96,4930 | 96,9712 |
| p1-ref vs crf12 | **4,2889** | **89,4449** | 95,1904 | 95,6643 |

İki ayrı sonuç:

**Özdeş klip satırı ayakta.** 97,4257 kilitli ve kilitsiz **aynı**; kilit onu
kımıldatmıyor. "97,425 modelin tek karelik taban salınımı" bulgusu **ölçüldü,
değişmedi.**

**crf8 / crf12 satırları geçersiz.** Belgedeki 4,3147 ve 4,3011 yeniden üretildi
(4,3022 ve 4,2889) — yani o sayılar gerçekti, ama ölçtükleri şey **kodlama
kalitesi değil yanlış kare eşlemesiydi.** Kilit takılınca min 4,3'ten 92,6'ya ve
4,3'ten 89,4'e çıktı; crf8 ile crf12 arasındaki sinyal 0,0136'dan **3,1471**'e
yükseldi. "p1'de sinyal gürültünün yüzde biri" gözlemi kilitli ölçüde yok.

**K5 kararı değiştirilmedi.** İki nedenle: p2 ve p3 satırları **yeniden
ölçülmedi** (K5 üç içeriğe dayanıyor, elimde biri var) ve karar T110'un
kapsamında değil. Yapılan şey kanıtın durumunu yazmak: K5'in birinci kanıtı
geçerli, ikinci kanıtı p1 için geçersiz. Kararın yeniden açılması ayrı bir iştir.

### 9.11 Kirlenmiş satırlar

Belgedeki bütün VMAF sayıları kilitsiz ölçüyle üretildi. Kaymanın **göründüğü**
yerler anormal düşük `min` değerleridir; hepsi aşağıda, her biri ya yeniden
ölçüldü ya damgalandı.

| yer | sayı | durum |
|---|---|---|
| §2 yan bulgusu | özdeş min 97,4256 | **ölçüldü, değişmedi** (§9.10) |
| §3 tablosu | `VMAF-NEG min` 0,7867 / 0,0000 | **kaymış ölçüyle yazıldı, geçersiz** — yeniden ölçülmedi |
| §3 metni | "0,79 puanlı bir kare … hizalanma artığı" | tanı doğruymuş, nedeni bilinmiyordu (§9.2); damgalandı |
| §3 yan bulgusu | `settb=AVTB` denendi, sonuç aynı çıktı | **kaçırılmış ipucu** — aşağıda |
| §4 gerekçesi | "iyi bir kodlamada min 0,79 çıkabiliyor" | aynı ölçümden türüyor, **geçersiz** |
| §7 özdeş tablosu | 97,4257 / 97,4253 / 97,4256 | p1 **ölçüldü, değişmedi**; p2 ve p3 **ölçülmedi** |
| §7 crf8/crf12 tablosu | p1 4,3147 / 4,3011 | **geçersiz**, yeniden ölçüldü (§9.10) |
| aynı tablo | p2 95,2609 / 94,2846, p3 2,2023 / 2,2041 | **ölçülmedi**; p3'ün 2,2'si p1'inkiyle aynı aileden görünüyor ama bu bir tahmin |
| §1 grafiği | kilitsiz filtre satırı | **envanterde eksikti** — T116 ekledi; belgenin kendi tarif satırı kilitsiz haliyle duruyordu, kilitli haliyle değiştirildi (§1) |

**Kaçırılmış ipucu.** §3'ün yan bulgusu `xpsnr` filtresinin bastığı
`not matching timebases found between first input: 1/15360 and second input
1/1000` uyarısını kaydediyor, `settb=AVTB`'yi **deniyor**, sonuç değişmediği için
üretim zincirini değiştirmiyor. Deneme doğruydu ama eksikti: `settb` tek başına
zaman tabanını birleştirir, kare **numarasını** birleştirmez; kaymayı kaldıran
`setpts=N`'dir (mutasyon M6, 3 kırmızı). O turda uyarı görülüp geçildi.

Kilitten **etkilenmesi beklenmeyen ama ölçülmemiş** satırlar: §2 ve §5'in
ortalama / harmonik / p10 sayıları (ızgarada ortalama en fazla 0,80 oynadı) ve
§4.2 ile §4.3'ün pencere bölme sayıları (kare eşlemesinden bağımsız aritmetik).
**Bunların hiçbiri yeniden ölçülmedi.**

### 9.12 Bu turda ölçülmeyenler

- **libvmaf ve framesync'in hangi eşleme kuralını uyguladığı ölçülmedi.** §9.2 ve
  §9.4'ün sonuçları davranış olarak ölçüldü; bunları üreten iç kural okunmadı.
- **HDR/tonemap yolunda kilidin etkisi ölçülmedi.** Kilit her iki zincire de
  giriyor ama §3'ün tonemap'li çifti yeniden ölçülmedi.
- **`MeasureWindowAsync`'in `-ss`'li yolu ayrıca ölçülmedi.** Çıpa ölçümü (§9.7)
  o yoldan geçiyor ve değişimi gösteriyor; pencere yolunun kendisi için ayrı bir
  ızgara kurulmadı.
- **XPSNR ve SSIM'in kilitten ne kadar etkilendiği ölçülmedi.** Izgarada yalnız
  VMAF-NEG'in kare başına dökümü var.
- **İçerik çeşitliliği hâlâ yok.** İki gerçek klip aynı ana kaynaktan, üçüncü
  düzenek sentetik. Farklı kare hızları (24, 25, 50) ölçülmedi.
- **T95'in şu bulguları bu turda düzeltilmedi ve ölçülmedi:** kare başına
  puanların dışarı verilmemesi, eşlenen kare sayısının `QualityScore`'da
  bulunmaması, libvmaf JSON'unun `%TEMP%`'e yazılması, kare hızı ve en-boy oranı
  denetiminin olmaması, XPSNR'ın düzlem başına raporlanmaması. Kapsam dışı.
- **§9.9'un kapısı dar.** Etiketsiz ile bt709 dışı çelişkisini yakalıyor;
  etiketsiz ile bt709 çiftinde varsayım hâlâ duruyor ve doğrulanmadı.
- **Süre iddiası yok.** Makine altı ajanla paylaşımlıydı; hiçbir zaman sayısı bu
  bölüme girmedi.


### 9.13 İki koşum iki ayrı şeyi ölçtü — hangisi kilidi tuttu

Teslim iki koşumla raporlandı ve **ikisi aynı şeyi ölçmüyor.** Karıştırılmasın
diye ayrı yazılıyor.

| koşum | ffmpeg PATH'te | `QualityMeterTests` | kilit ölçüleri |
|---|---|---|---|
| `dotnet test -c Release --filter QualityMeterTests` | **var** | 24 geçti / 0 atlandı | **koştu** |
| `tools/ci-gibi-kos.sh` (tam süit) | **yok** | 11 geçti / **13 atlandı** | **atlandı** |

`ci-gibi-kos.sh` PATH'ten ffmpeg'i çıkarıyor; `[FfmpegFact]` taşıyan ölçüler o
koşumda çalışmıyor. §9'un beş yeni ölçüsünün **beşi de** orada atlananların
içinde: `OneFrameOfSlipIsWorthTensOfVmafPointsOnThisFixture`,
`SubFrameTimestampSlipDoesNotCostTheScoreAWholeFrame`,
`ShiftedSourceIsReportedNotSilentlyRepaired`,
`VideoStartAheadOfTheContainerIsTheOffsetThatReachesTheFilterGraph`,
`UntaggedSourceAgainstANonBt709TagIsRefusedInsteadOfAssumed`.

Yani **"ci-gibi-kos.sh 1018 geçti / 0 kaldı" kare kilidini ölçmedi.** O sayının
tek anlamı şudur: kilit ürün koduna girerken ffmpeg'siz koşan 1018 ölçüde
gerileme olmadı. Kilidin kendi kanıtı ffmpeg'li koşumdan ve §9.8'in mutasyon
bataryasından geliyor; batarya da ffmpeg'li koşuma dayanıyor (M1–M8'in kırmızı
sayıları `[FfmpegFact]` ölçülerinden çıkıyor).

Bunun sonucu: **kare kilidi CI'da korunmuyor.** CI ffmpeg görmediği sürece kilidi
kaldıran bir değişiklik yeşil geçer. Bu turda düzeltilmedi — CI'ın ffmpeg'siz
koşması ayrı bir işe (T115) alındı.

> **T116:** "yaklaşık 80" tahmindi; **sayıldı, 87.** Ölçüt kaynak ağacındaki
> öznitelikler: `[FfmpegFact]` **84** kez, `[FfmpegTheory]` **1** kez ve o tek
> teori **3** `InlineData` taşıyor. `tools/ci-gibi-kos.sh` ffmpeg ve ffprobe'u
> PATH'ten çıkarıyor, iki öznitelik de kurucuda `ToolLocator.IsAvailable`
> başarısız olunca `Skip` atıyor; yani ffmpeg'siz koşumda **87 ölçü atlanır.**
> Dağılım: `FrameGrabberTests` 21, `QualityMeterTests` 12, `PanelHostTests` 11,
> `PerformanceCheckTests` 7, `ComplexityProbeTests` 6+3, `SegmentEncoderTests` 6,
> `FpsDropTests` 5, `EncodeRunnerTests` 4, `PreviewSyncTests` 4, `SceneMapTests` 4,
> `VmafPoolingTests` 3, `QualityTargetTests` 1.
> Bu sayı **kaynaktan sayıldı, koşumdan okunmadı** — sözleşme tam süiti
> koşturmayı yasakladı. Koşulmuş atlama sayısı **ölçülmedi**.


## 10. Kilitsiz ölçerle konmuş çıpalar — geri alınır mı (T116)

T110 ürün ölçerine kare kilidini koydu ve aynı çift üzerinde çıpanın kilitsiz
75,07, kilitli 87,20 okuduğunu gösterdi — **12,13 puan**. Bu turun sorusu şuydu:
o hata daha önce konmuş çıpalara ve o çıpalara dayanan kararlara ne yaptı.

Ölçülen ağaç: worktree `T116-cipa-yeniden`, commit `31472cb`. Ölçer ffmpeg 9.0,
libvmaf `vmaf_v0.6.1neg`. Düzenek `tools/cipa-yeniden/`; her sayının komutu
oradaki `README.md` ve `duzenek/` altında yazılı.

### 10.1 Çıpa kendiliğinden düzeldi — kalibrasyon turu gerekmedi (K4)

**Bu turun en önemli sonucu budur ve sözleşmenin açılış öncülünü yumuşatıyor.**

Çıpa saklanan bir sabit değil; `ComplexityProfile.WithProbeQuality` her koşumda
pencere ölçümlerinden yeniden hesaplıyor. Yani kilit ürün koduna girdiği an
(`822dd3a`, 09-02 04:44) eski çıpa diye bir şey kalmadı — bir sonraki koşum
kilitli ölçüyle yeni çıpayı kuruyor. **Diskte düzeltilecek bir çıpa yok,
kalibrasyon turu gerekmiyor.** Bu ölçüldü: aynı ağaçtan biri kilitli biri
kilitsiz iki ikili yayımlandı ve aynı kaynaklarda koşuldu.

| kaynak | çıpa kilitsiz | çıpa kilitli | fark |
|---|---:|---:|---:|
| `parca-1.mkv` (1080p60 HDR) | 82,726502 | 87,192345 | **+4,465842** |
| `parca-2.mkv` (1080p60 HDR) | 91,550111 | 91,575281 | +0,025170 |
| `sdr-1.mkv` (ikame, 1080p60 SDR) | 82,277860 | 91,718219 | **+9,440358** |

`parca-1`in +4,47'si T110'un 12,13'ünün üçe bölünmüş halidir. Pencere pencere:

| pencere | kilitsiz | kilitli | fark |
|---|---:|---:|---:|
| 1 (9,73 s) | 86,157256 | 87,425446 | +1,268190 |
| 2 (29,20 s) | 86,947848 | 86,947848 | **0,000000** |
| 3 (48,67 s) | 75,074404 | 87,203740 | **+12,129337** |

Üçüncü pencere T110'un çiftidir ve 12,13 rakamı rakamına yeniden üretildi.
İkinci pencere onbeş ondalık basamağa kadar özdeş: o pencerede kayma yoktu ve
kilit hiçbir şey değiştirmedi. Çıpa üç pencerenin aritmetik ortalaması olduğu
için 12,13 puanlık tek pencere hatası çıpaya **4,04 puan** olarak geçiyor;
kalan 0,42 birinci pencereden geliyor.

**Plan hiçbir hedefte değişmedi.** Aynı iki ikili `PlanCalculator.BuildDetailed`
ile koşuldu ve plan alanları (genişlik, yükseklik, fps, kodlayıcı, kip, crf,
video bit hızı, preset) karşılaştırıldı:

| kaynak | sayılan hedef | plan aynı |
|---|---|---|
| `parca-1.mkv` | 8, 12, 20, 30, 40, 60, 80 MB — **yedisi de** | 7/7 |
| `parca-2.mkv` | 8, 12, 20, 30, 40, 60, 80 MB — **yedisi de** | 7/7 |
| `sdr-1.mkv` (ikame) | 8, 12, 20, 30, 40 MB — **beşi** | 5/5 |

**Toplam 19 çift sayıldı, 19'unda plan özdeş.** `sdr-1` 23,1 MiB olduğu için
60 ve 80 MB hedefleri **sayılmadı** — kaynak zaten hedeften küçük, plan
kopyalamaya düşüyor ve karşılaştırma bir şey ölçmez.

Değişen tek alan `PredictedQuality`, ve o da çıpa farkının aynısı kadar:
`parca-1` her hedefte tam **+4,4658**, `sdr-1` her hedefte tam **+9,4404**.
Tavana dayanan satırlar (99,00 ve 100,00) hiç oynamadı. Örnek — `parca-1` @ 60 MB:
plan 1920×1080 libx264 crf 21 7585k iki koşumda da aynı, `PredictedQuality`
83,3757 → 87,8416.

**Sonuç: çıpalar yanlıştı ama kararlar yanlış değildi.** T110'un 12,13 puanı
kullanıcıya ulaşmadı; hedef boyut bütçesi çıpadan önce geliyor ve çözünürlük,
kodlayıcı, crf seçimini bütçe belirliyor. Çıpa yalnız **tahmin edilen kaliteyi**
kaydırıyor — kullanıcıya gösterilen sayıyı, teslim edilen dosyayı değil. `sdr-1`
ikamesinde ilk pencerenin **+28,32** puanlık hatası bile planı değiştirmedi;
bu, sonucu zayıflatmıyor, güçlendiriyor.

Ölçülmedi: çıpanın plana **hiçbir** koşulda geçmediği ölçülmedi. Ölçülen şey bu
üç kaynağın 19 hedefidir. `SpreadHalvings` üç kaynakta da 0 çıktı, yani
`PerHalving` yolu (çıpanın bit hızına göre eğim verdiği kol) bu turda hiç
tetiklenmedi ve **ölçülmedi**.

### 10.2 Belge envanteri — hangi sayı hangi ölçerden geçti (K1)

`docs/olcumler/` altında **20 belge** var; **8'i** VMAF ya da XPSNR sayısı
taşıyor. Ölçüt: iki kilit anı. Ölçüm boru hattı `tools/VidShrink.Bench`in kendi
grafiğinde `0e2b071` (09-02 03:09) ve `48ec9fa` (03:15) ile kilitlendi; ürün
ölçeri `QualityMeter` `822dd3a` (04:44) ile. Bir belgenin **son** commit'i
kilitten önceyse içindeki hiçbir sayı kilitli ölçerden geçmiş olamaz.

| belge | VMAF/XPSNR satırı | durum |
|---|---:|---|
| `algi-olcusu.md` | 86 | **karışık** — §9'un ızgarası ve çıpaları kilitli ölçüldü; §2, §3, §4, §5, §7'nin sayıları kilitsiz. Hangisinin yeniden ölçüldüğü §9.11'de satır satır yazılı; bu turda §9.2, §9.4 ve §7'nin p1 satırı yeniden ölçüldü |
| `olcu-gecerliligi.md` | 37 | **karışık** — gövdesi 03:30'da yazıldı: kendi `bench` koşumları boru hattı kilidinden **sonra**, T102'nin JSON'larından yeniden saydığı sayılar kilitsiz. **Yeniden ölçülmedi** (dosya T111'in) |
| `handbrake-acigi.md` | 18 | **kilitsiz ölçerden geçti, yeniden ölçülmedi** (09-01 08:49, iki kilitten de önce) |
| `auto-mod.md` | 12 | **kilitsiz ölçerden geçti, yeniden ölçülmedi** — sayıları T102'nin (09-02 01:40–02:08) ve `tools/auto-mod-olcumu/*.sh` kendi ffmpeg çağrılarını kuruyor: 8 ölçüm satırının **0'ında** kilit var. Zaman değil düzenek belirliyor |
| `tepe-tavani-ve-psy.md` | 4 | **kilitsiz ölçerden geçti, yeniden ölçülmedi** (09-01 22:54) |
| `ornekte-vmaf-maliyeti.md` | 4 | **kilitsiz ölçerden geçti, yeniden ölçülmedi** (09-01 22:18) |
| `olculen-kaliteyle-plan.md` | 3 | **kilitsiz ölçerden geçti, yeniden ölçüldü** — A/B ızgarası bu turda kilitli ve kilitsiz ikiliyle yeniden koşuldu; eski sütun geçersiz damgasıyla duruyor |
| `kazanc-kullaniciya-ulasiyor-mu.md` | 2 | **kilitsiz ölçerden geçti, yeniden ölçülmedi** (09-02 02:56, boru hattı kilidinden 13 dakika önce) |

Kalan 12 belge VMAF ya da XPSNR sayısı taşımıyor, kilitten etkilenmiyor:
`dinamik-esik.md`, `sahne-haritasi.md`, `ceviri-olcusu-mutasyonu.md`,
`surecler-arasi-olcu-yalitimi.md`, `suit-eszamanli-kosum.md`,
`t84-tur2-mutasyon.md`, `t27-ipucu-satir-genislikleri.md`,
`t27-ipucu-satir-genislikleri-once.md`, `T33-oynatma-olcumleri.md`,
`T37-sunum-olcumleri.md`, `T32-anahtar-kare-olcumleri.md`,
`T30-panel-olcumleri.md`.

**Yeniden ölçülmeyen beş belgenin hiçbiri bu turda kapsam dışı bırakılmadı —
ölçülmedi.** 10.1'in sonucu bunları da ilgilendiriyor: içlerindeki VMAF sayıları
kaymış olabilir, ama hiçbiri plan kararı üretmiyor; hepsi rapor sayısı.
Kaymanın rapor sayısına ne yaptığı belge belge **ölçülmedi**.


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

### T110 — kare kilidi ölçümleri

Çalışma klasörü `.calisma/T110/` (worktree-yerel, `.gitignore`'da). İş parçacığı
her kodlamada sabit: `-threads 2`, x265'te ayrıca `pools=2`, SVT-AV1'de `lp=2`.

Kaynaklar ve testler (§9.2):

    ffmpeg -t 4 -i .calisma/kaynak/parca-1.mkv -c copy ref1.mkv
    ffmpeg -t 4 -i .calisma/kaynak/parca-2.mkv -c copy ref2.mkv
    ffmpeg -i refN.mkv -c:v libx265 -crf 32 -preset veryfast -x265-params pools=2 -threads 2 -an pN-x265.mkv
    ffmpeg -i refN.mkv -c:v libsvtav1 -crf 45 -preset 8 -svtav1-params lp=2 -threads 2 -an pN-av1.mkv

`ref2` **sesiyle birlikte** kesilir; `-an` ile kesilirse kap tek akışa iner ve
ffmpeg kaymayı sıfırlar (§9.1) — kusur ölçülemez hale gelir.

Ham ölçüm, ürün kodunun kurduğu grafiğin aynısı; `KILIT` boşken kilitsiz,
`,settb=AVTB,setpts=N` iken kilitli koşum:

    N=zscale=w=1920:h=1080:min=bt2020nc:tin=smpte2084:pin=bt2020:rin=full:m=bt2020nc:t=smpte2084:p=bt2020:r=full,format=yuv420p10le
    ffmpeg -i pN-<kodlayici>.mkv -i refN.mkv -lavfi \
      "[0:v]$N$KILIT[t];[1:v]$N$KILIT[r];[t][r]libvmaf=model=version=vmaf_v0.6.1neg:log_fmt=json:log_path=vmaf-pN-<kodlayici>-<kilit>.json" \
      -f null -

Bu yolla alınan sayılar `bench measure` çıktısıyla dört basamağa kadar uyuştu,
yani ham koşum ürün yolunun kendisini ölçüyor.

Zaman damgası modeli (§9.1) `showinfo` ile doğrulanır:

    ffmpeg -i <dosya> -vf showinfo -frames:v 3 -f null - 2>&1 | grep pts_time

Duyarlı düzenek (§9.4), `.calisma/T110/pin/`:

    ffmpeg -f lavfi -i testsrc2=size=320x240:rate=30:duration=3 -c:v libx264 -crf 12 -threads 2 src.mp4
    ffmpeg -i src.mp4 -c:v libx264 -crf 16 -threads 2 same.mp4
    ffmpeg -f lavfi -i anullsrc=r=48000:cl=stereo -t 3 -c:a aac sessiz.m4a
    ffmpeg -itsoffset <kayma> -i src.mp4 -i sessiz.m4a -map 0:v -map 1:a -c copy k<kayma>.mkv
    ffmpeg -i src.mp4 -vf "select=gte(n\,1),setpts=N/FR/TB" -c:v libx264 -crf 16 -threads 2 kaymis-1kare.mkv
    ffmpeg -i src.mp4 -vf "select=gte(n\,2),setpts=N/FR/TB" -c:v libx264 -crf 16 -threads 2 kaymis-2kare.mkv

Her `k<kayma>.mkv` `same.mp4` ile yukarıdaki ham grafikten geçirilir. 0 ms satırı
`src.mp4` ile üretilir, `k0.mkv` ile değil — mkv'nin 1/1000 zaman tabanı kayma
sıfırken bile 91 kare ve 81,8084 veriyor (§9.4).

> **T116 düzeltmesi.** Bu satır belgede `kaymis-ref.mkv` adıyla duruyordu ve
> "bir kare kaymanın referans değeridir" deniyordu. Bu tarifle üretilen dosya
> §9.4'te iddia edilen 52,3008'i **vermiyor**, 65,7226 veriyor; 52,3008'i veren
> dosya `k0.020.mkv`'nin kendisiydi. Tarif dosyaya uyduruldu, dosya tarife değil:
> iki dosya `kaymis-1kare` / `kaymis-2kare` diye ayrıldı ve **kilitli** ölçülen
> değerleri (52,1174 · 37,4526) §9.4'e çıpa olarak yazıldı.

Kontrol (§9.3) — ref2'nin kasten iki kare kaydırılmış ve kaydırılmamış kopyaları:

    ffmpeg -i ref2.mkv -vf "select=gte(n\,2),setpts=N/FR/TB" -c:v libx265 -crf 16 -x265-params pools=2 -threads 2 -an ref2-2kare-kaydirilmis.mkv
    ffmpeg -i ref2.mkv -c:v libx265 -crf 16 -x265-params pools=2 -threads 2 -an ref2-kaydirilmamis-crf16.mkv

Çıpalar (§9.7) — aynı ağaçtan iki ikili, biri kilitli biri kilitsiz:

    dotnet publish -c Release tools/VidShrink.Bench -o .calisma/T110/bench-kilitli
    # QualityMeter.cs'ten kilit çıkarılır, sonra:
    dotnet publish -c Release tools/VidShrink.Bench -o .calisma/T110/bench-kilitsiz
    <bench>/VidShrink.Bench shrink .calisma/kaynak/parca-N.mkv 60 --out cipa-out \
      --measured-quality --plan-only --no-calibrate

§7'nin yeniden ölçümü (§9.10) belgenin kendi tarifiyle `.calisma/T110/s7/` içinde
kurulur; ham grafik yukarıdakinin aynısıdır, çiftler `p1-ref` ile kendisi, `crf8`
ve `crf12`.

Mutasyon bataryası (§9.8) `.calisma/T110/mutasyon.sh`, günlüğü `mutasyon.log`;
her mutasyonu tek başına uygular, `dotnet test -c Release --filter QualityMeterTests`
koşar, kaynağı geri yükler. M5'in eşdeğerliği `pin/m5-N.json` ile `pin/m5-Np1.json`
kare dizileri karşılaştırılarak gösterildi.

**Bu klasör worktree-yereldir ve git'e girmez.** Yukarıdaki komutlar dışarıdan
yeniden üretmek için yeterlidir; `.calisma/kaynak/` ortak ve bu turda
değiştirilmedi.
