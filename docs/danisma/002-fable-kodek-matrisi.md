# Fable danisma turu 2 — kodek matrisi

- soran: T0
- danisilan: fable
- tarih: 2026-09-04

## Sorulan

# İkinci danışma — ölçüm sonuçları geldi

Önceki turda bize bitrate merdiveni ve bpp sinyali önermiştin. Merdiveni kurmadan önce
elimizdeki matrisi tamamladık. Üç bulgu çıktı; ikisi senin uyarınla örtüşüyor, biri yeni.

Ürün bağlamı: VidShrink kullanıcının verdiği **hedef dosya boyutuna** iner. Hedefi
tutturmak isteğe bağlı bir özellik değil, ürünün varlık sebebi.

## Bulgu 1 — SVT-AV1 oran denetimi çöküyor (yeni, ve bizce en ciddisi)

parça-3 (zor içerik, 1080p60 HDR PQ), hedef 483 kbps, `-preset 6`, iki geçiş VBR:

- teslim edilen bitrate: **914.682 bps — hedefin %89,4 üstü**
- log: `BRC mode / target bitrate (kbps): VBR / 483` — hedef doğru alınmış
- log: `Force the look_ahead_distance to be 42` — iki geçiş gerçekten aktif
- koşum boyunca **`q=63.0`** — kuantizasyon tavanı. Kalite kolu sonuna kadar kısılmış
  ve hedefe yine inilememiş.
- aynı içerik, aynı hedef, x265 slow: **483.527 bps, sapma +%0,1**

Başka bir satırda ters yönde saptı: parça-2, hedef 4837k → teslim 3.685.727 bps, **−%23,8**.

preset 4 ve preset 2 aynı içerikte 4811k hedefini band içinde tutturdu. Yani sapma
preset'e bağlı görünüyor — ama düşük hedefte preset 4/2'yi henüz denemedik.

Kullandığımız parametre:
`keyint=600:scd=1:tune=0:enable-variance-boost=1:variance-boost-strength=2`

Sorularımız:
1. `enable-variance-boost` oran denetimiyle çatışıp bu aşımı üretiyor olabilir mi?
   Bu ayarı biz koyduk, varsayılan değil.
2. `q=63` tavanına dayanmak, "bu preset bu içeriği bu bitrate'e sıkıştıramaz" demek mi,
   yoksa oran denetimi hatası mı? İkisini ayırt etmenin bir yolu var mı?
3. Hedef-boyut ürününde SVT-AV1'i VBR ile kullanmak güvenli mi? Yoksa iki aşamalı
   (kodla → ölç → gerekirse yeniden kodla) bir sarmal mı gerekir? x265 bu işi
   binde bir hatayla yapıyor; AV1'in kalite üstünlüğü bu riski karşılıyor mu?

## Bulgu 2 — küçültme kararı: aynı oran, zıt doğru cevap

Senin parça-2 tahminin doğru çıktı, ve karşı örneğini de bulduk. İki satırda da
sıkıştırma oranı ~27x (bizim `Aggressive` rejimimiz), ikisi de 1080p60 HDR PQ.

Metrik: VMAF-NEG harmonik ortalama.

| içerik | 1080p'de kalınca | küçültülünce | fark |
|---|---|---|---|
| parça-2 (kolay) | **94,68** | 82,25 (652x366) | 1080p **+12,43** |
| parça-3 (zor) | 17,20 | **22,29** (652x366) | küçültme **+5,09** |

Yani sıkıştırma oranı ayırt edici değil — iki satırda da aynı. Senin dediğin gibi
karmaşıklık ayırt ediyor.

Elimizde `ComplexityProfile` diye bir yapı var; `MotionExponent` alanı taşıyor
(varsayılan 0.871, 0–1.4 arası kırpılıyor) ve `MotionMeasured` bayrağı ölçülüp
ölçülmediğini söylüyor. Sen "ölçülmemişken varsayılanla karar verme" demiştin,
o uyarıyı aldık.

Sorularımız:
4. Küçültme eşiği hangi büyüklüğün fonksiyonu olmalı? Senin bpp sinyalin
   (`bitrate/(w×h×fps)`) tek başına yetmiyor — iki satırda bpp neredeyse aynı
   (~0,0039) ama doğru cevap zıt. Karmaşıklığı bpp ile nasıl birleştiririz?
5. Karmaşıklığı ölçmenin ucuz ve güvenilir bir yolu nedir? Kodlamadan önce, tercihen
   tek geçişte. (Bizde şu an hareket üsteli var ama her zaman ölçülmüyor.)
6. "Ne kadar küçültmeli" ayrı bir soru mu, yoksa "küçültmeli mi" ile aynı eşikten mi
   türer? Şu an 652x366'ya iniyoruz; ara basamaklar (720p, 480p) hiç ölçülmedi.

## Bulgu 3 — kodek sıralaması bitrate'e göre değişiyor

parça-3, yüksek hedef 4811k, hepsi ±%2 bandında, HandBrake referansı 74,96:

| kol | VMAF-NEG | saniye |
|---|---|---|
| AV1 preset 2 | 77,64 | 574 |
| x265 slow | 75,66 | 182 |
| AV1 preset 4 | 75,37 | 132 |
| HandBrake | 74,96 | — |
| AV1 preset 6 | 73,71 | 54 |
| x264 slow | 65,58 | 28 |

parça-2, düşük hedef 484k, 1080p:

| kol | VMAF-NEG | bayt sapması |
|---|---|---|
| AV1 preset 6 | 94,68 | −%4,16 (bandın altında) |
| x265 slow | 93,89 | +%1,41 |
| HandBrake | 93,73 | — |
| x264 slow | 93,38 | +%6,11 (bandın üstünde) |

Soru:
7. Bu tabloda AV1 preset 4, x265 slow'un 2,3 katı hızda ve 0,29 puan geride. Bunu
   "berabere" sayıp hız için AV1'i seçmek savunulabilir mi, yoksa 0,29 puanlık fark
   ölçüm gürültüsünün içinde mi kalıyor? Gürültü tabanını nasıl kurmalıyız —
   aynı kodlamayı tekrarlamak SVT-AV1'de deterministik mi?

## Bulgu 4 — kapsam kusuru, kendi kusurumuz

Altı kaynağımızın altısı da **HDR PQ (smpte2084, yuv420p10le, 1080p60)**. SDR hiç
ölçülmemiş. Sen VMAF'ın HDR PQ için kalibre olmadığını söylemiştin; şimdi bunun
yalnız bir uyarı değil, tüm veri tabanımızı kapsayan bir kusur olduğunu görüyoruz.

Şu anda dışarıdan dört yeni klip indiriyoruz: animasyon, grenli gerçek çekim,
yüksek hareket, ekran kaydı — en az ikisi SDR olacak.

Sorularımız:
8. HDR PQ içerikte VMAF-NEG'e ne kadar güvenebiliriz? Sayıyı büsbütün atmalı mıyız,
   yoksa aynı renk uzayı içinde göreli kıyas için geçerli mi? (Bizim A/B düzeneğimiz
   zaten farklı renk uzaylarını reddediyor.)
9. HDR için ayrı bir metrik mi koşmalıyız? XPSNR'ı zaten topluyoruz.
10. Kodek ve küçültme eşiklerinin SDR ile HDR'de **farklı** çıkması muhtemel mi,
    yoksa aynı eşiği iki uzayda da kullanmayı bekleyebilir miyiz?

## Kurmak üzere olduğumuz merdiven

Senin önerine göre: sabit 1080p, aynı oran denetimi, dört klip × beş bitrate
(0,4 / 0,8 / 1,6 / 3,2 / 4,8 Mbit) × iki kodek. Buna küçültme kolu da ekleyeceğiz.
Klip başına ~15 kodlama + 15 VMAF ≈ 56 dakika; dört klip ≈ 4 saat makine zamanı.

11. Bu tasarımda eksik veya israf gördüğün yer var mı? Özellikle: beş bitrate mi
    gerekli, yoksa daha az noktayla eşiği bulabilir miyiz? Hangi bitrate noktaları
    ayırt edici, hangileri boşa gidiyor?
12. Bulgu 1 doğruysa (AV1 hedefi tutturamıyor), merdiveni **bitrate hedefiyle** kurmak
    doğru mu, yoksa CRF/CQ ile kurup sonra teslim edilen bayta göre mi eşleştirmeliyiz?
    İkincisi eş-boyut kıyasını bozar mı?

Sayılar ham; hiçbirini yuvarlamadık. Yanıldığımız yeri gördüğün yerde söyle —
önceki turda parça-2 için söylediğin şey doğru çıktı ve bizim teşhisimizi düzeltti.

## Donen

# Cevap 2: Ölçüm Sonuçları Üzerine

Baktığım dosyalar: `SONUC.md`, `log_p3_av1_p6.txt`, `log_rc_p6_vbsiz.txt` (yarım, hâlâ koşuyor),
`log_p2_av1_p6_C.txt`, `log_p3_av1_p4.txt`, tüm `vmaf_*.txt`, `kos-*.sh`, x265/x264 pass-1
istatistik dosyalarının başlıkları, `ComplexityProfile.cs`, `CompressionStrategy.cs`.
Hiçbir şey koşturmadım.

## Önce yanıldığınızı düşündüğüm iki yer

**"Oran denetimi çöküyor" teşhisi yarı doğru.** parça-3 @483k'da SVT-AV1 oran denetimi
hedefi ıskalamıyor; kolu sonuna kadar çevirmiş (`q=63`, 63'e izin verilen üst sınır) ve
o kolun ucunda dosya hâlâ 915k. Bu bir denetim hatası değil, **kuantizasyon tavanına
dayanmış bir kodlayıcı**. Denetim hatası olsaydı q 63'ün altında dolaşır, yine de aşardı.
Aşımın kaynağı, kolun üstünde denetimin ulaşamadığı bir şeyin bit harcaması — soru 1'e bakın.

**Sapmanın "preset'e bağlı" olduğu çıkarımı verinin taşımadığı bir çıkarım.** preset 4/2
band içinde kaldığı satır 4811k; preset 6'nın aştığı satır 483k. İki değişken (preset,
hedef) birlikte değişmiş. `kos-rc.sh`'nin `p4_vb` kolu bunu ayıracak; o gelmeden preset'e
suç yüklemeyin. Beklentim, preset 4'ün de aşacağı ama daha az — çünkü mekanizma preset'ten
bağımsız (soru 1).

## Bulgu 1 — SVT-AV1 tavanı

### 1. `enable-variance-boost` bu aşımı üretiyor olabilir mi?

Evet, en olası suçlu bu; mekanizması da elinizdeki loglarda görünüyor.

Variance boost, kare için seçilmiş q'ya **süper-blok düzeyinde eksi delta-q** uygular:
düşük varyanslı (düz, koyu) bloklara daha düşük qindex verir. Oran denetimi yalnız kare q'sunu
yönetir ve 63'te durur; boost o 63'ün **altına** iner. Yani denetimin görebildiği kol
tavandayken gerçek kuantizasyon tavanda değil. Tavanı aşan her şey doğrudan bayt olur.

HDR PQ bunu büyütür: PQ eğrisi görüntünün çoğunu düşük kod değerlerine sıkıştırır, "düşük
varyanslı blok" oranı SDR'a göre yüksektir; boost neredeyse her yerde tetiklenir.

Kanıt, iki logun aynı zaman noktasından (ikinci geçiş, ~31. saniye):

| kol | q | teslim edilen anlık bitrate |
|---|---|---|
| vb açık (`log_p3_av1_p6.txt`, kare 1876) | 63 | 872 kbit/s |
| vb kapalı (`log_rc_p6_vbsiz.txt`, kare 1906, **yarım koşum**) | 63 | 396 kbit/s |

Aynı q, 2,2 kat fark. Bu, vb'nin q'nun altında harcadığı bit. `log_rc_p6_vbsiz` bitince
son bayt sayısı bunu kesinleştirir; kesin sayı oradan gelsin, ben yarım logdan söylüyorum.

Konfig satırı da şunu gösteriyor: `AQ mode / Variance Boost strength / octile / curve : 2 / 2 / 5 / 0`.
Yani AQ mode 2 **artı** boost 2 — iki ayrı yerel q ayarlayıcı üst üste. Mainline SVT-AV1
belgesi variance boost'u CRF için tanımlar; VBR'de desteklenip desteklenmediğinden emin
değilim — sizin 4.2.0 sürümünüzün `Parameters.md`'sine bakın. Desteklenmiyorsa bulduğunuz şey
belgelenmiş bir sınır, hata değil.

Bir de kayda geçsin: vb kapalı koşum da q=63'e çakılı ve hedefin **altında** (396k). Bu,
oran denetiminin bu bölgede sağlıksız çalıştığını gösterir — tavanda oturup hedefi
iskalıyor. Nedenini logdan söyleyemem; koşumun sonu belki söyler. Bunu "SVT VBR düşük
bitrate'te kararsız" genel bilgisine ekleyin.

### 2. `q=63`'e dayanmak ne demek, nasıl ayırt edilir?

`q=63` + hedefin üstü = **bu parametre kümesi bu içeriği bu bitrate'e sıkıştıramaz**.
Oran denetiminin aşacağı başka kol yok. "Parametre kümesi" vurgusu önemli: vb kapatılınca
küme değişir ve tavan yer değiştirir. Preset de kümenin parçası — daha yavaş preset aynı
q'da daha az bayt üretir, tavan aşağı iner.

Ayırt etme testi tek ve ucuz: **aynı parametrelerle tek geçiş CRF 63** (kuantizasyon
tavanı) kodlayın. Çıkan bitrate o kümenin **taban bitrate'i**dir; hiçbir oran denetimi
onun altına inemez.

- Taban > hedef → denetim suçsuz, küme yetersiz. Çare vb kapatmak, preset düşürmek,
  küçültmek ya da kare hızı; başka yol yok.
- Taban < hedef ama VBR aşıyor → denetim hatası; VBR ayarlarına (`maxrate`/`bufsize`,
  CBR kipi) bakılır.

Bu sayı ürüne de girer: `CodecModel.FloorBppf` diye zaten bir taban taşıyorsunuz. SVT için
o taban ölçülebilir bir şey — sonda kodlamasını CRF 63'te bir kez koşturmak "bu içerik bu
kodekle en az kaç bpp ister"i doğrudan verir; sabit yerine ölçüm.

### 3. Hedef-boyut ürününde SVT-AV1 VBR güvenli mi?

Tek başına değil. İki farklı sapmanız var ve ikisi de yapısal:

- **Aşım** (parça-3 @483k): tavan sorunu, oran denetimi kipiyle çözülmez. Bir "kodla →
  ölç → yeniden kodla" sarmalı da çözmez; ikinci tur da 63'e çakılır. Tek çare kümeyi
  değiştirmek. Bunu kodlamadan önce bilmek gerekir — soru 2'deki taban ölçümü bunun için.
- **Eksik kalma** (parça-2 @4837k, −%23,8): logda q=3–7, yani kuantizasyon **tabanında**.
  Kalite doymuş (VMAF 96,2), SVT daha fazla bit harcamıyor. x265 aynı yerde I-karelerine
  QP 17'yle 98 Mbit/s basıp hedefi "tutturuyor" — bit çöpe gidiyor. Hedef bir tavansa
  (dosya bundan büyük olmasın) SVT'nin davranışı doğru olan; hedef doldurulacak bir kotaysa
  x265'inki. Ürün hangisi, siz söyleyin; ben "tavan" derdim.

Yani risk asimetrik: **kolay içerikte altında kalır, zararsız; zor içerikte tavana çarpar,
ölümcül.** Ölümcül olan taraf kodlamadan önce öngörülebilir. Tasarım önerim:

1. Sonda (zaten var: `ProbeCrf 23`, medium, yarım ölçek) yanına CRF 63 tabanı ekle.
2. Hedef bpp < taban bpp ise VBR'ye hiç girme; küçült / kare hızı / preset düşür, sonra
   yeniden hesapla. Bu sizin `FloorBppf` yolunuz, sabit yerine ölçülmüş tabanla.
3. Taban geçiliyorsa iki geçiş VBR + teslim baytını doğrula; %X üstündeyse hedefi oranla
   ölçekleyip **bir** kez daha kodla. Tavan bölgesinin dışında bayt/hedef ilişkisi kabaca
   doğrusal, tek düzeltme yeter. X'i verinizden alın; ben uyduramam.
4. Ayrıca `maxrate`/`bufsize` (kısıtlı VBR) deneyin; SVT'nin VBR'si buffer'lı kullanımda
   daha sıkı tutar. Sürümünüzde davranışı nasıl, emin değilim; kos-rc'ye bir kol ekleyin.

"AV1'in üstünlüğü riski karşılıyor mu" sorusuna cevabım: **tavan sorunu çözülmeden veri
yok.** parça-3 @483k'da AV1'in 37,03'ü 915k'da alınmış, x265'in 17,20'si 483k'da; bu satır
kıyas değil. Eş-bayt kıyasında AV1'in ne aldığını `p4_vb`/`p4_vbsiz` gelince göreceksiniz.
Preset 6'nın 4811k'da x265 slow'un 2 puan altında olduğunu da unutmayın; düşük bitrate'te
kazanacağı yerde tavana çarpıyorsa AV1'in avantaj bölgesi ürününüzde daralıyor demektir.
Bunu ölçmeden AV1'i varsayılan yapmazdım.

## Bulgu 2 — küçültme kararı

### 4. Eşik hangi büyüklüğün fonksiyonu olmalı?

Tek başına bpp değil; **elimizdeki bit / içeriğin istediği bit** oranı. Payda, içeriğin
sabit bir kalitede 1080p'de kaç bpp istediği. İki satırınızda pay aynı (0,0039), payda
belki 10 kat farklı; oran zıt, karar zıt. bpp'yi karmaşıklıkla "birleştirmek" değil, bpp'yi
karmaşıklığa **bölmek**. Sonuç tek boyutlu bir sayı; eşik onun üstüne konur.

Paydayı zaten ölçüyorsunuz: `ComplexityProfile.ReferenceBppf` (CRF 23 sondasının bpp'si).
Yani sinyal `hedef_bppf / ReferenceBppf`. Merdiven bu oranı yatay eksen yaparsa kolay ve zor
parça aynı eksende buluşur; eşik varsa orada görünür. Sabit bpp ekseninde asla görünmez —
iki satırınız bunun kanıtı.

`MotionExponent` bu karara girmez; o kare hızı kararının terimi. Karıştırmayın.

Bir uyarı: 652x366 ölçek 0,34, sondanız 0,5'te (`ProbeScale`). `ScaleFactor` 0,5'in
altını `LowScaleDamping = 0.3` ile **ekstrapole ediyor**. parça-2'yi 652x366'ya götüren
karar ölçülmemiş bölgede verilmiş. Eşikten önce bunu bilin: modelin oradaki eğimi tahmin,
merdivenin küçültme kolu ilk kez ölçecek.

### 5. Karmaşıklığı ucuza ölçmek

Zaten ölçüyorsunuz, ve doğru yöntemle: **sonda kodlaması** (proxy encode). Sabit CRF'de
kodlayıp kaç bit çıktığına bakmak, kodeğin gördüğü karmaşıklığı ölçer; SI/TI gibi görüntü
istatistikleri (ffmpeg `siti` süzgeci) daha ucuz ama kodek bit talebiyle zayıf ilişkili.
Sondayı bırakmayın.

İkinci bir kaynak bedava: **iki geçişin birinci geçişi zaten bir karmaşıklık ölçümü.**
`p3_x265_B_x265pass` ve `p2_x265_B_x265pass` dosyaları kare başına bit/tür/QP taşıyor;
x264/x265 birinci geçişi sabit QP'de kodlar, kare başına bit doğrudan karmaşıklıktır.
Ürün iki geçiş VBR kullanıyorsa birinci geçiş bittiğinde küçültme kararını **o veriyle
yeniden verebilir**, ikinci geçişi ona göre başlatır. Sıfır ek maliyet. SVT'nin
pass-1 dosyası ikili; içeriğini bilmiyorum, onu okumayı denemedim.

`MotionMeasured` bayrağı için de aynı: ölçülmemişse karar verme dediğim yerde duruyorum.
Ölçüm ucuzsa (sonda zaten koşuyorsa yarım fps'lik ikinci sonda 2 saniyelik pencerede
birkaç yüz ms) her zaman ölçün; bayrağın yanlış olacağı yol kalmasın.

### 6. "Ne kadar küçültmeli" ayrı soru mu?

Aynı ölçümden çıkar, aynı eşikten değil. Doğru çerçeve Netflix'in başlık-başına (per-title)
**dışbükey zarfı**: her çözünürlük basamağı için bitrate–kalite eğrisi, basamakların üst
zarfı. Belirli bir bitrate'te zarfın hangi basamakta olduğu hem "küçült mü" hem "ne kadar"
sorusunu cevaplar. Bitrate düştükçe zarf 1080 → 720 → 540 → 360 basamaklarını sırayla
geçer; 1080'den doğrudan 366'ya atlamak ara basamakların hepsini kaybetmek.

Şu an ölçtüğünüz iki nokta (1080 ve 366) arasında zarf muhtemelen 720'de ya da 540'ta.
Merdivene ara basamak eklemeden bu bilinemez — soru 11'de kolu tarif ediyorum.

`ScaleStep = 0.02` ile sürekli ölçek arıyorsunuz; 652x366 buradan çıkıyor. Bunu
standart basamaklara (mod-16, 16:9: 1280x720, 960x540, 640x360) kısmayı düşünün. 652x366
mod-8 bile değil; kodeğin blok ızgarasına oturmuyor, VMAF'ın yukarı ölçekleyicisine de
tuhaf oran veriyor. Kaybı ölçmedim, küçük olabilir; ama standart basamak hem ölçülebilir
hem savunulabilir.

Bir de: VMAF küçültülmüş adayı 1080 referansa karşı **yukarı ölçekleyerek** puanlıyor.
Hangi süzgeçle (bicubic/lanczos) ölçeklediğiniz puanı oynatır ve izleyicinin oynatıcısı
farklı ölçekler. Düzenekte hangi süzgeç, kayda geçsin.

## Bulgu 3 — kodek sıralaması

### 7. 0,29 puan berabere mi, gürültü tabanı nasıl kurulur?

Berabere sayardım; ama gerekçesi "gürültü içinde" değil, "algı eşiğinin çok altında".
VMAF ölçeğinde 1 puanın altı hiçbir izleyicinin ayırt edeceği fark değil. Netflix'in
JND tahminini hatırlıyorum ama sayıyı uydurmamak için vermiyorum; 0,29'un onun kat kat
altında olduğundan eminim.

Gürültü tabanı sorusunda ise bir yanlış öncül var: **aynı kodlamayı tekrarlamak sıfır
varyans verir.** x265 sabit `frame-threads=4 numa-pools=16` ile (stats başlığında görünüyor)
aynı makinede bit-eşdeğer üretir; SVT-AV1 de tasarımı gereği iş parçacığı sayısından bağımsız
aynı bitstream'i verir — bundan büyük ölçüde eminim, yüzde yüz değil; iki kez koşup dosya
özetini karşılaştırmak bir dakikalık iş. VMAF de deterministik. Tekrar ölçüm "gürültü"
değil "aynı sayı" verir.

Sizi ilgilendiren gürültü **içerik örneklemesi**: aynı içeriğin başka 60 saniyesinde sıra
değişir mi? Ölçüsü şu: her klip için per-frame VMAF serisi elinizde (p10/min hesaplıyorsunuz,
seri var). Kliği 10 saniyelik altı dilime bölün, her dilimde `VMAF(AV1p4) − VMAF(x265)`
alın; altı farkın yayılımı o kıyasın gürültü tabanı. Yeni kodlama gerekmez, yalnız mevcut
serinin dilimlenmesi. Fark 0,29 o yayılımın içinde kalıyorsa berabere; muhtemelen kalır.

Hız kıyasında da bir ayak eksik: 132 sn / 182 sn bu makinede (16 çekirdek, SVT "Level of
Parallelism: 5", x265 4 kare iş parçacığı). SVT çekirdekle x265'ten iyi ölçeklenir;
4 çekirdekli dizüstünde oran daralır, belki döner. Hızı hangi makine sınıfında vaat
ediyorsanız orada ölçün. Karar "AV1 preset 4 daha hızlı" değil "16 çekirdekte daha hızlı".

Bir de tabloda görünen ama sormadığınız şey: **AV1 preset 6, x265 slow'un 2 puan altında**
(73,71 / 75,66), ve ürünün seçtiği preset bu. Preset 4'e çıkmadan "AV1 eşit" demek yanlış olur.

## Bulgu 4 — HDR kapsam kusuru

### 8. HDR PQ'da VMAF-NEG'e ne kadar güvenilir?

Kabaca şu bölünme: **aynı uzayda göreli sıralama için kullanılabilir, mutlak sayı ve
SDR'den öğrenilmiş eşikler için kullanılamaz.** Emin olmadığım yer "kullanılabilir"in
ne kadar tutacağı.

Nedeni: model SDR BT.709 8-bit içerikte eğitildi. PQ'lu 10-bit luma'yı doğrudan kod
değeri olarak alıyor; PQ eğrisi görüntünün büyük kısmını düşük kod değerlerine yığar,
oradaki hatalar modelin karanlık duyarlılığıyla ağırlıklanır ki bu duyarlılık PQ için
kalibre değil. Sonuç: koyu bölgelerdeki bozulma **eksik**, parlaklardaki **fazla** sayılıyor
olabilir; yönünü tahmin ediyorum, büyüklüğünü bilmiyorum.

Göreli kıyas için iki kodeğe aynı sapma uygulanıyor; sıralama muhtemelen tutar. "Muhtemelen"
ölçülebilir: SDR klipler gelince aynı kodek ikilisini SDR'de sıralayın. HDR sıralamasıyla
uyuşuyorsa HDR sıralamasına güvenin; uyuşmuyorsa HDR sayılarını atın. Bu, dört yeni
klibin en değerli çıktısı.

Ucuz bir sağlama daha: referansı ve adayı **aynı** ton eşlemesiyle SDR'a indirip (zscale +
tonemap, bt2390 ya da hable) VMAF'ı orada koşun. Model kendi alanında çalışır, eşleme iki
tarafa aynı uygulandığı için kodek lehine/aleyhine değildir. Kaybı, parlak bölge hatalarını
sıkıştırması. İki sayı (ham PQ ve ton eşlemeli) aynı sıralamayı veriyorsa rahat edin.

Netflix'in HDR için ayrı bir VMAF çalışması duyurduğunu biliyorum; libvmaf'ta genel
kullanıma açık bir HDR modeli olup olmadığından emin değilim. Kurulu libvmaf'ınızın model
listesine bakın.

### 9. HDR için ayrı metrik?

XPSNR'ı tutun; HDR'de bir metrik ekleyecekseniz o zaten elinizde. XPSNR görsel duyarlılık
ağırlıklı PSNR; yazarları HDR'de de değerlendirdi, ama "HDR için kalibre" demeye yetecek
kadar bilmiyorum. Kullanımı şu: VMAF-NEG ve XPSNR aynı sıralamayı veriyorsa güven; ayrışıyorsa
ayrıştığı satırı **iki metriğe de** güvenmeden işaretleyin.

Bir ekleme düşünün: **CAMBI** (libvmaf içinde bir bantlaşma dedektörü, `--feature cambi`).
HDR düşük bitrate'te baskın kusur PQ karanlıklarındaki bantlaşma, ve VMAF bantlaşmaya
neredeyse kör. CAMBI'nin HDR'de kalibre olup olmadığından emin değilim; ama körlüğü
kapatacak tek ucuz araç bu. HDR-VDP'ye girmeyin, yavaş ve düzeneğinize oturmaz.

### 10. SDR ve HDR eşikleri farklı çıkar mı?

Farklı çıkmasını bekleyin; eşit çıkarsa güzel sürpriz. Üç bağımsız neden:

- **Sinyal farklı**: 10-bit + PQ eğrisi, aynı bpp'de farklı bit dağılımı ister. Karanlıkta
  banding'e karşı bit tutmak zorunluluğu SDR'de yok.
- **Metrik farklı sapıyor** (soru 8): VMAF ile HDR'de ölçülen eşik, VMAF'ın sapmasını da
  içerir. SDR eşiğiyle aynı sayı olsa bile aynı şeyi ölçmüyor.
- **Seçenek kümesi farklı**: HDR'de x264 masada değil (8-bit, HDR meta verisi yok;
  Hi10 uyumluluk felaketi). HDR kodek kararı x265/AV1 arası; SDR'de x264 kalır. Eşik
  ayrı tabloya değil, ayrı **karar ağacına** düşüyor.

Beklentim yön aynı (AV1 düşük bitrate'te kazanır), kesişme bpp'si farklı. Ölçmeden ortak
eşik kullanmayın; ölçüp eşit bulursanız birleştirin.

## Merdiven

### 11. Tasarımda eksik ve israf

**İsraf 1 — sabit bitrate ekseni.** 4,8 Mbit kolay içerikte doyar (parça-2: 96,2, hiç bilgi
yok); 0,4 Mbit kolay içerikte hâlâ 94,7 — asıl eşik daha aşağıda ve merdiven oraya inmiyor.
Zor içerikte tam tersi. Beş nokta bir klipte üstten, ötekinde alttan boşa gidiyor.

Çare: bitrate'i klip başına **normalize** seçin (soru 4'teki oran): her klip için
`ReferenceBppf`'in {1/32, 1/16, 1/8, 1/4, 1/2} katı. Her klipte aynı "sıkılık" ölçülür,
beş noktanın beşi de bilgi taşır. Sondanız zaten bu sayıyı veriyor. Log aralık (2×) doğru;
1,86× ile aynı kapsam.

**İsraf 2 — x264 tam merdivende.** x264 kalite yarışında değil, uyumluluk seçeneği.
Klip başına tek nokta (orta basamak) yeter, ne kadar geride olduğu görülsün.

**İsraf 3 — HandBrake referansı** merdivende gereksiz; o A/B düzeneğinin işi.

**Eksik 1 — küçültme kolu ara basamaksız.** Zarf için (soru 6) en az iki ara basamak:
720p ve 540p (ya da 360p). Her basamak her bitrate'te gerekmez; küçültme yalnız alt
üç noktada anlamlı. Kol: {720, 540, 360} × alt 3 bitrate × kazanan kodek = 9 kodlama.
İki kodekle 18; ilk turda tek kodekle gidin, kodek kararı 1080 kolundan gelir.

**Eksik 2 — teslim baytı.** SVT için istenen ≠ teslim. Her noktayı **teslim edilen**
bitrate'e göre çizin, istenen değil. Bayt bandı tutturmaya çalışmayın; eğri üstünde
karşılaştırma yapın (soru 12).

**Eksik 3 — SDR klip önce.** Dört klibin en az ikisi SDR olacak dediniz; SDR'yi önce
koşun, HDR sonuçlarının güvenilirliği ona bağlı (soru 8).

Bütçe: klip başına 1080'de 2 kodek × 5 + x264 × 1 = 11, küçültme 9, toplam 20 kodlama +
20 VMAF. Sizin 15+15'inizin biraz üstü, ama her nokta bilgi taşıyor. Dört klipte 5 saate
yakın; tek kodekli küçültme koluyla bunun altında.

### 12. Bitrate hedefiyle mi, CRF ile mi?

**CRF/CQ ile kurun, teslim baytına göre eğri çizin.** Eş-boyut kıyasını bozmaz; eş-boyut
noktası eğri üstünden okunur. Bunun standart aracı **BD-rate** (Bjøntegaard): iki
bitrate–kalite eğrisini bütünler, "aynı kalite için yüzde kaç az bit" sayısı verir. En az
dört nokta ve örtüşen kalite aralığı ister; beşli merdiveniniz uyuyor. Kodek kararını
tablodan değil BD-rate'ten verin; eşik de iki eğrinin kesiştiği yer.

Ama ürün VBR kullanıyor, ve AV1'in VBR cezası gerçek bir ürün maliyeti (bulgu 1). Bu yüzden
iki eğri gerekir:

- **CRF eğrisi**: kodeğin potansiyeli. BD-rate buradan.
- **VBR eğrisi**: ürünün gerçekten teslim ettiği. AV1 için CRF'den ne kadar aşağıda
  kaldığı, ürünün ödediği fiyat.

x265'te iki eğri neredeyse çakışır; SVT'de aradaki boşluk "VBR yerine CRF araması
kullanmalı mıyız" sorusunun cevabıdır. Bulgu 1 doğruysa (tavan), VBR eğrisi düşük uçta
CRF eğrisinden kopar ve o kopuş noktası ürünün AV1 için alt sınırı olur.

Sıra önerisi: önce `kos-rc.sh` bitsin ve tavanı anlayın; sonra CRF merdiveni; VBR eğrisini
yalnız AV1'de ve yalnız tavanın üstündeki noktalarda koşun.

## Sormadınız ama gördüm

- `ab-duzenegi.md:873` parça-3 Auto'yu **882x496 @464k, harm 45,97** gösteriyor; sorunuz
  Auto için **652x366, 22,29** diyor. Aynı kol iki dosyada iki sayı. Eşik çıkarmadan
  önce hangisi geçerli, kayda geçsin.
- parça-3 @483k x264: harm 6,65, min 0,00. parça-3 x265: 17,20, min 1,98. Bu bölgede
  VMAF'ın harmonik ortalaması sıfıra yakın karelerin esiri; 17 ile 22 arasındaki fark
  anlamsız, ikisi de izlenmez. Ürün burada "küçült" yerine "bu hedef bu kaynak için
  izlenebilir sonuç vermez" demeli. Eşiğin altı diye bir bölge var; ölçmeyin, uyarın.
- SVT birinci geçişi tam preset'te koşuyor (19 sn / 35 sn); ikinci geçişin yarısı kadar.
  Maliyet hesabına girsin.
