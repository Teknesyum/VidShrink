# Fable danisma turu 3 — taban, kucultme ve variance-boost

- soran: T0
- danisilan: fable
- tarih: 2026-09-05

## Sorulan

# VidShrink — üçüncü tur: taban, küçültme ve VB'nin işaret dönmesi

Önceki turda üç şey istemiştin: CRF 63 taban teşhisi, tek değişkenli küçültme sınaması,
ve merdiven tasarımı. İlk ikisi koştu. Aşağıdaki her sayı gerçek koşumdan; ham günlükler
`.calisma/kodek-matris/` altında.

Kaynaklar HDR PQ (bt2020/smpte2084), ses yok. Ölçü: VMAF-NEG harmonik ortalama (`harm`),
referans kaynağın kendisi.

## 1. CRF 63 taban — senin teşhisin

parça-3, 1080p, 2 geçiş VBR hedefi 483k. CRF63 kolu tek geçiş.

| küme | CRF63 çıktısı (bps) | VBR@483k teslimi (bps) |
|---|---|---|
| preset 6, VB'siz | 407.489 | 421.051 |
| preset 6, VB'li | 1.025.695 | 914.682 |
| preset 4, VB'siz | 435.383 | 393.113 |

İki okuma çıkarıyorum, ikisini de sorguluyorum:

**(a) "CRF63 mutlak tabandır" varsayımı düştü.** preset 4 kolunda VBR teslimi (393k)
kendi CRF63 çıktısının (435k) altında. Benim açıklamam: CRF 63 ile VBR'ın q=63 tavanı
aynı iç qindex'e oturmuyor, CRF kolu zamansal katmanlara q ölçekleme uygulayıp bazı
kareleri 63'ün altında kodluyor. Bu doğru mu? SVT-AV1'de CRF ile VBR'ın azami q
semantiği gerçekten ayrı mı?

**(b) VB teşhisi ayakta.** Aynı preset, tek değişken VB: 407k → 1.026k, **2,52 kat**.
VB'li kol VBR'da 915k teslim etti — kendi azami-q çıktısının %89'u. Yani denetim
gerçekten dibe vurmuş. Katılıyor musun?

## 2. Denetimli küçültme — tek değişken çözünürlük

Tüm kollar AV1 preset 4, `keyint=600:scd=1:tune=0` (VB yok), 2 geçiş.

**parça-3 (zor içerik), hedef 483k**

| çözünürlük | teslim bps | bayt | harm |
|---|---|---|---|
| 882x496 | 483.785 | 3.653.588 | 26,01 |
| 1280x720 | 483.852 | 3.654.094 | 27,63 |

Bayt farkı %0,01 — parite kusursuz. 1280x720 +1,62.

**parça-2 (kolay içerik), hedef 484k**

| çözünürlük | teslim bps | bayt | harm |
|---|---|---|---|
| 882x496 | 333.430 | 2.517.404 | 60,13 |
| 1280x720 | 431.158 | 3.255.248 | 74,54 |
| 1920x1080 | 465.502 | 3.514.541 | 95,15 |

Burada parite yok. Küçültülen kollar bütçeyi kullanamadı: 882 hedefin %31 altında,
1280 %11 altında, 1080p %4 altında teslim etti.

**Sorularım:**

**2.1** Küçültmenin eksik teslimi büyütmesi bekliyor musun, yoksa bu bizim
kurulumumuza özgü bir kusur mu? Mekanizması ne — daha az piksel + aynı q tavanı mı,
yoksa ölçekleyicinin (lanczos) yüksek frekansı silmesi kodlayıcıya "harcayacak detay
yok" mu dedirtiyor?

**2.2** parça-2'de parite kırıldığı için kıyas teknik olarak geçersiz. Ama 1080p hem
%40 daha çok bayt harcayıp hem +35,02 puan alıyor. Bu farkın boyutla açıklanamayacağı
sonucunu çıkarmak savunulabilir mi, yoksa pariteyi zorlayan bir tekrar mı istersin
(1080p'yi 333k'ya kelepçeleyip 882 ile eşitlemek gibi)?

**2.3** Zor içerikte fark yalnız +1,62. Küçültmenin *hiçbir* rejimde kazandırmadığı
sonucuna varmak için bu yeterli mi, yoksa daha agresif bir noktada (örneğin 150k,
oran ~200:1) işaret dönebilir mi? Dönerse kural "asla küçültme" değil "şu eşiğin
altında küçült" olur.

## 3. Variance-boost'un işaret dönmesi

Üretimin parça-3 Auto çıktısı: 882x496, VB açık, preset 6 → 22,29
Bu turdaki 882x496: VB kapalı, preset 4 → 26,01
Aynı yerleşim, ~aynı bayt. **+3,72 VB'nin aleyhine.**

Önceki turda VB +12,11 getiriyor görünüyordu — ama orada boyut serbestti (VB 6,3 MB
harcadı, VB'siz 3,0 MB). Boyut sabitlenince işaret dönüyor.

**3.1** Bu okuma doğru mu? "VB boyut serbestken kazandırır, boyut sabitken kaybettirir"
genel bir ifade mi, yoksa bizim iki değişkenimizin (VB + preset) karıştığı bir yanılsama mı?
Preset 6/4 farkını 0,5-0,8 puanla sınırlayan verim var ama VB'yi tek başına izole eden
sabit-boyut koşumu yok. Onu koşmamı ister misin?

**3.2** Eğer doğruysa, VB'yi tümden kaldırmak mı doğru, yoksa CRF kipinde (hedef boyut
olmayan "en iyi kalite" kipi) tutup VBR/hedef-boyut yolunda kapatmak mı?

## 4. Üretim tabanına karşı ilk sonuç

parça-2 @3,50 MB taban satırı (`tools/VidShrink.Ab/veri/t125/parca-2-auto.md`),
aynı referans, aynı ölçüm aracı:

| | bayt | harm |
|---|---|---|
| HandBrake x265 1080p | 3.735.428 | 93,73 |
| VidShrink Auto (1650x928, AV1, VB açık) | 3.806.095 | 82,25 |
| **bu tur: 1080p, AV1, VB'siz, preset 4** | **3.514.541** | **95,15** |

Yani iki değişikliği (küçültme yok + VB yok) uygulayınca HandBrake'i **daha az baytla**
+1,42 geçiyoruz. Bu sonucu geçerli sayıyor musun, yoksa gördüğün bir tuzak var mı?

## 5. Merdiven tasarımı — önceki turdan devreden

Merdiveni henüz kurmadım çünkü senin 11 ve 12 numaralı cevapların tasarımı değiştiriyordu.
Şimdi bu üç bulguyla birlikte tekrar soruyorum:

**5.1** Merdiven CRF üstüne mi bitrate üstüne mi kurulsun? Ürünün kendisi hedef-boyut
odaklı, yani gerçek kullanım VBR. CRF merdiveni BD-rate için daha temiz ama ürünün
koştuğu kipi ölçmüyor. İkisini birden koşmak klip başına ~40 kodlamaya çıkarıyor.

**5.2** Küçültme kolu hâlâ gerekli mi? Bölüm 2 küçültmenin kaybettirdiğini gösterdi.
Merdivende {720, 540, 360} × alt 3 bitrate koşmak yerine, yalnız en agresif noktada
tek doğrulama koşumu yeterli mi?

**5.3** Elimde dört yeni SDR klip var (`.calisma/kaynak-genis/`): animasyon (BBB 1080p60),
gren (ToS 1920x800 24fps), hareket (ToS 1920x800 24fps), gren2 (Xiph old_town_cross
1080p50). HDR tarafında parça-1/2/3 var. Bu küme her rejimi kapsıyor mu, yoksa eksik
bir içerik sınıfı görüyor musun?

**5.4** Kullanıcının şartı: kural her boyutta ve her rejimde geçerli olmalı — SDR ve HDR,
küçük hedef ve büyük hedef, agresif küçültme dahil. Merdivenin bitrate noktaları bunu
kapsayacak şekilde nasıl seçilmeli? Önceki turda `ReferenceBppf × {1/32…1/2}` demiştin;
bu aralık agresif ucu yeterince kapsıyor mu?

## 6. Serbest

Yukarıdakilerin dışında, bu verilerde benim atladığım bir şey görüyorsan söyle.
Özellikle "bu sonucu şu koşum olmadan iddia etme" tipinde bir uyarın varsa.

## Donen

# Cevap 3: Taban, Küçültme ve VB'nin İşaret Dönmesi

Baktığım dosyalar: `SONUC.md`, `taban-log.txt`, `kucultme-log.txt`, `rc-log.txt`, tüm
`log_kc_*`, `log_taban_*`, `log_rc_*`, tüm `vmaf_*.txt`, `kos-*.sh`,
`tools/VidShrink.Ab/veri/t125/parca-2-auto.md` ve `parca-3-auto.md`,
`FfmpegArguments.cs:348-533`, `QualityMeter.cs:528`, `.calisma/kaynak-genis/` künyeleri.

Kodlama koşturmadım. İki şey koşturdum, ikisi de salt okuma: mevcut `.mp4`'lerde
`ffprobe -show_entries packet=size,flags` (kare başına bayt), ve ffmpeg ilerleme
satırlarından `q=` örneklerini geçiş bazında ayırma. Sayılar aşağıda, yeniden üretilebilir.

## Önce yanıldığınızı düşündüğüm üç yer

**Bölüm 4'ün atfı yanlış.** HandBrake'i geçen şey iki değişiklik değil, tek değişiklik:
küçültmemek. `SONUC.md` B tablosunda 1080p + preset 6 + **VB açık** kol zaten 94,68 almış,
3.580.091 baytla — HandBrake'in 93,73'ünün üstünde, daha az baytla. VB'yi kapatıp preset
4'e inmek buna +0,47 ekliyor (95,15, 3.514.541 bayt). O +0,47 tek başına ölçülmedi ve
JND'nin altında. "Küçültme yok + VB yok birlikte HandBrake'i geçti" cümlesi veriyi taşımıyor;
"küçültmemek tek başına geçti" cümlesi taşıyor.

**Bölüm 3'teki "preset farkı 0,5-0,8 ile sınırlı" bir sınır değil.** G tablosundaki iki
çiftte de preset 4 **daha az baytla daha yüksek** puan almış (25,46 @393k / 24,71 @421k;
37,57 @839k / 37,03 @915k). Eş baytta preset farkı bu sayıların **en az** bu kadarı, üst
sınırı bilinmiyor. Üstelik o ölçüm 1080p'de q≈62-63 rejiminde; 882x496'da q≈46-55. Bir
rejimin farkını ötekine taşımayın.

**"Küçültme hiçbir rejimde kazandırmaz" (2.3) veriden çıkmıyor; tersine, veri bir eşiğin
varlığını gösteriyor.** Gerekçe 2.3'te.

## 1. CRF 63 taban

### 1(a) — CRF 63 ile VBR'ın azami q'su aynı şey değil; okumanız yön olarak doğru

Kanıtı loglardan değil, dosyalardan aldım. Aynı kümenin CRF63 ve VBR çıktısında paket
dağılımı (parça-3, preset 4, VB'siz, 3625 kare, 7 anahtar kare):

| dosya | toplam | anahtar kare baytı | medyan | p90 | p99 | en büyük |
|---|---|---|---|---|---|---|
| `taban_p3_p4_vbsiz.mp4` (CRF 63) | 3.268.954 | 178.086 | 22 B | 1.229 | 21.435 | 53.703 |
| `rc_p4_vbsiz.mp4` (VBR 483k) | 2.953.378 | 156.041 | 19 B | 1.289 | 13.038 | 48.805 |

Medyan kare ~20 bayt: karelerin yarısı iki kolda da fiilen boş (atlanmış). Fark tamamen
üst yüzde 1-2'de — anahtar kareler ve taban katmanı kareleri. CRF 63'te bu kareler VBR'dan
%40-60 daha büyük. Yani CRF 63, "her kare q=63" değil; TPL/hiyerarşi taban katmanına daha
düşük q veriyor, 63 yalnız tavan. VBR'da ise 63 bir kelepçe ve taban katmanı da ona yakın
tutuluyor. Sizin "zamansal katmanlara q ölçekleme" açıklamanız bu tabloyla uyumlu; SVT
kaynağından doğrulamadım, dosyadan çıkardım.

İki düzeltme:

- ffmpeg'in `q=` alanı **son paketin** qp'si, ortalama değil. "Koşum boyunca q=63" cümlesi
  örneklemden geliyor; ikinci geçişte `rc_p4_vbsiz` 48-63 arası dağılıyor, tepe 62. Kare
  başına q isterseniz ilerleme satırından değil paket boyutundan okuyun.
- Gerçek mutlak taban CRF değil, sabit-QP ve modülasyonsuz: `rc=0:qp=63:enable-tpl-la=0:aq-mode=0`.
  Ama ürünün ilgilendiği taban o da değil; ürünün tabanı "VBR'ın azami q'da teslim ettiği"
  ve onu zaten ölçtünüz: **393k**. Taban için ayrı koşum gerekmiyor.

Bir şey daha: VBR azami q'da oturup hedefin **%19 altında** kalıyor. Tabana dayanmış bir
denetim aşar, altında kalmaz. Altında kalıyorsa denetimin modeli "q=63 483k verir" diye
yanlış tahmin etmiş ve q'yu tavana çekmiş; geri besleme yavaş. Ürün için sonucu: hedef
tabanın ~1,3 katının altındayken SVT VBR iki yöne de ±%20 sapar. Bu bölgede hedefi
tutturmayı beklemeyin.

### 1(b) — VB teşhisi ayakta, katılıyorum

Aynı yöntem, tek fark VB: 407k → 1.026k. Paket tablosu da aynı şeyi söylüyor
(`taban_p3_p6_vb`: 1000 bayttan büyük paket 942, VB'siz 421; p90 4.363'e karşı 1.186).
VB tabanı 2,5 katına çıkarıyor ve VBR'ın o tabanın %89'una kadar inebilmesi denetimin
kolunun bittiğini gösteriyor.

Tek uyarı: 2,5 kat HDR PQ'da ölçüldü. Önceki turda dediğim gibi PQ "düşük varyanslı blok"
oranını şişiriyor; SDR'de çarpan daha küçük çıkar. Bu sayıyı SDR'e taşımayın, orada
yeniden ölçün.

## 2. Denetimli küçültme

### 2.1 — Eksik teslim beklenen davranış, kurulumunuza özgü değil

Mekanizma loglarda. İkinci geçiş q örnekleri, parça-2 @484k:

| çözünürlük | q aralığı (2. geçiş) | teslim |
|---|---|---|
| 882x496 | **4-17** | 333k (−%31) |
| 1280x720 | 19-27 | 431k (−%11) |
| 1920x1080 | 31-36 | 466k (−%4) |

Kodlayıcı pes etmemiş; q'yu yirmi basamak indirmiş. q≈10'da 10-bit AV1 neredeyse saydam,
her ek q basamağı çok az bayt alıyor ve SVT VBR hedefi son bayta kadar kovalamıyor
(`minsection-pct` varsayılan 0, "harcamak zorundasın" kuralı yok). Piksel 4,7 kat azalınca
aynı bütçe için q'nun bu bölgeye inmesi kaçınılmaz.

Lanczos'un yüksek frekansı silmesi buna katkı yapar ama baskın terim piksel sayısı.
Ayırmak isterseniz tek koşum: 882 kolunu `flags=bicubic` ile tekrarlayın; bayt %5'ten az
oynarsa ölçekleyici suçlu değil. Şart değil, merak giderir.

Yan not: SVT config satırı `width / height: 888 / 496`. 882 mod-8 değil, SVT içeride 888'e
dolguluyor; 6 sütun boşa kodlanıyor. Basamakları mod-16 seçin (önceki tur, soru 6).

Ürün açısından eksik teslim kusur değil — hedef tavansa. Kıyas açısından kusur; onu 2.2
çözüyor.

### 2.2 — Kıyas geçerli, ama parite zorlamakla değil, tavanla savunulur

q≈10'daki 882 kodlaması küçültülmüş görüntünün neredeyse saydam hali. Dolayısıyla 60,13
o basamağın **çözünürlük tavanına** yakın: 882'ye lanczos indirip 1080'e geri çıkarmanın
(sizde `QualityMeter.cs:528`, zscale, varsayılan bicubic) VMAF'ı. Hiçbir bayt bütçesi
bunun üstüne çıkaramaz.

Bunu sıfır kodlamayla ölçün: kaynak → `scale=882:496:flags=lanczos` → geri 1080 → libvmaf.
Tek VMAF koşumu. Tavan ~61-63 çıkarsa parça-2 sonucu parite olmadan da kesin. 1280 için de
aynı (74,54 ve q 19-27; o da tavanına yakın olabilir).

1080p'yi 333k'ya kelepçelemeyi **istemiyorum**: kimsenin teslim etmeyeceği bir noktayı
ölçer, tek klip için geçerli, tekrar kullanılamaz. Tavan ölçümü ise merdivene girer: her
klip × her basamak için bir üst sınır; 1080p'nin o bitrate'teki puanı basamağın tavanının
üstündeyse o basamak o klipte kodlanmadan elenir.

### 2.3 — Katılmıyorum: "hiçbir rejimde" veriden çıkmıyor, tersi çıkıyor

Üç neden:

1. **1080p'nin 483k'daki noktası yok.** `rc_p4_vbsiz` 1080p'de q tavanında oturup 393k
   teslim etti (25,46). 882 ve 1280 484k'da. Eş baytta 1080p–720p kıyası bu turda hiç
   yapılmadı; 25,46/26,01/27,63'ü yan yana okumak %23 bayt farkını yok saymak olur.
2. **Tabanın altında küçültme zorunlu.** parça-3 1080p'nin tabanı ~390-435k. 483k bu
   tabanın 1,1-1,2 katı; daha düşük her hedefte 1080p **kodlanamaz**, 882 kodlanır. Eşik
   yapısal olarak var: `taban(çözünürlük)`. Sorunuz "eşik var mı" değil, "tabanın üstünde
   bir bant var mı — [taban, k×taban] aralığında 720p 1080p'yi geçiyor mu". parça-3
   verisi bu bandın varsa tabanın ~1,2 katı içinde olduğunu söylüyor; genişliğini söylemiyor.
3. **26-27 ölçüm bölgesi değil.** p10 19-21, min 11. Harmonik ortalama sıfıra yakın
   karelerin esiri; +1,62 burada anlam taşımıyor. Önceki turdaki uyarı geçerli: ürün bu
   hedefte "izlenebilir sonuç vermez" demeli, en iyi basamağı aramamalı.

150k sorusu: 1080p60 HDR zor içerikte 150k tabanın çok altı, 1080p diye bir seçenek yok;
882'nin tabanı belki 120-180k (piksel oranıyla orantılı değil, daha az düşer), o da sınırda.
Yani işaret 150k'da "döner" ama kalite yarışıyla değil, fizibiliteyle. Kural biçimi doğru:
"şu eşiğin altında küçült". Eşiğin sayısı merdivenden çıkar; biçimi şimdiden belli.

## 3. Variance-boost

### 3.1 — Okuma makul, veri taşımıyor; koşumu isterim

Taşımamasının nedenleri:

- İki değişken (VB, preset) ve preset payı sınırlanmış değil (yukarıda).
- Üretim boru hattı farklı: `-hwaccel auto`, `-pix_fmt p010le`, istenen 489k, teslim
  3.715.009 bayt (`parca-3-auto.md`) — bu turun 882'sinden %1,7 fazla. Yön VB'nin aleyhine
  tutarlı (daha çok baytla daha az puan), büyüklük değil.
- G'deki "+12,11 boyut serbestken" ifadesi de yanıltıcı: boyut serbest değildi, **2,1 kat**
  bayttı. 2,1 kat baytı VB'siz harcasanız ne alırdınız, ölçülmedi. "VB boyut serbestken
  kazandırır" demeyin; "VB 2,1-2,5 kat bit harcayıp +12 alıyor, bit başına verimi bilinmiyor"
  deyin.

İstediğim koşum tek: `kos-kucultme.sh`'nin p3_882 kolu, yalnız
`enable-variance-boost=1:variance-boost-strength=2` eklenmiş. 882'de VB hedefi tutturuyor
(üretim tutturdu), parite gelir. ~1 dk kodlama + VMAF. Tahminim VB 1-3 puan kaybeder;
tahminimi değil sayıyı yazın.

**Metrik uyarısı, önemli:** VB bitleri dokulu/parlak bloklardan düz/karanlık bloklara
taşır. VMAF-NEG doku sadakatini ödüllendirir, bantlaşmaya neredeyse kör. Eş baytta VB'nin
VMAF kaybetmesi kısmen **metriğin** önyargısıdır, algısal kayıp olmayabilir. Aynı çifte
CAMBI ekleyin (`--feature cambi`, libvmaf içinde). CAMBI belirgin düzeliyorsa VB bir
takas; düzelmiyorsa VB gerçekten kaybettiriyor. VMAF tek başına bu kararı vermez.

### 3.2 — VBR yolunda kapat, koşumdan bağımsız; CRF yolunda ölçmeden karar verme

VBR/hedef-boyut yolunda VB'nin kapanma gerekçesi kalite değil, oran denetimi: denetimin
göremediği 2,5 kat bit talebi. 3.1'in sonucu ne çıkarsa çıksın bu gerekçe duruyor.
`FfmpegArguments.cs:532-533` koşulsuz ekliyor; oran denetimi kipine bağlayın. Pimleyen
testler `:135` ve `:463`.

CRF kipinde: özellik CRF için tasarlandı, orada tutulabilir. Ama "tut" kararı da ölçüm
ister — VMAF + CAMBI, HDR ve SDR'de. Ölçmeden varsayılan açık bırakmazdım; ölçüp CAMBI
kazancı görürseniz açın.

## 4. Üretim tabanına karşı ilk sonuç

Sayı geçerli, atıf değil (girişteki ilk madde). Gördüğüm tuzaklar:

- **A/B aracı bu satırı reddeder.** 3.514.541 bayt HandBrake'in −%5,9'u; ±%2 bandının
  dışında. Kazanan kol "eş boyut: hayır" düşer. Bandı tek yönlü yapmadan (altta kalmak
  serbest, üstte aşmak yasak) ürünün Auto sütunu bu kazancı gösteremez. Hedef tavansa band
  da tavan olmalı.
- **Bench betiği ≠ ürün yolu.** 95,15 `kos-kucultme.sh`'den geldi; üretim `-hwaccel auto`
  ve `p010le` ile gidiyor. Kazancı ürünün kendi komut satırından yeniden üretmeden
  "Auto HandBrake'i geçti" demeyin (hafızanızdaki "yeşil okuma gerçekti, ölçtüğü commit
  yanlıştı" kaydı tam bu).
- Tek klip, HDR, PQ üstünde VMAF-NEG. Göreli sıralama için kabul; SDR klipler gelmeden
  genelleme yok. +1,42 JND'nin üstünde ama bir örnek.
- parça-2 aşırı kolay: 1080p60 HDR'de 0,0037 bpp ile 95. "Kolay sınıf"ı tek başına bu
  klip temsil etmesin; ölçek tavana yakın, farklar sıkışır.

## 5. Merdiven

### 5.1 — CRF üstüne kur; VBR'ı doğrulama noktası olarak koş, merdiven olarak değil

Kararların ikisi de (küçült mü, hangi kodek) R-D eğrisinin özelliği; oran denetiminden
birinci dereceden bağımsız. SVT VBR ise bu turda −%31 ile +%89 arasında saptı — VBR
merdiveni kodeği değil denetim hatasını ölçer. CRF eğrisi tekdüze, deterministik; x ekseni
zaten teslim baytı.

VBR'ın ürün maliyeti ayrı ölçülür: klip başına 2 nokta, hedef = bir CRF noktasının teslim
baytı, teslim edilen bayt kaydedilir. Bu "VBR boşluğu" dağılımı ürünün düzeltme adımının
girdisi. Boşluk büyük çıkarsa (bu turun sayıları büyük) ürün için AV1'de VBR yerine CRF
araması (sonda + 1 düzeltme) masaya gelir; o kararı boşluk sayıları versin, şimdi değil.

Klip başına bütçe: AV1 1080p × 6 CRF (63/59/55/51/45/39), x265 1080p × 6 (kodek kararı
açık olan yerde), SDR'de x264 × 1, küçültme kolu ≤9 (5.2), VBR 2 → ~24. Kırk değil.

### 5.2 — Küçültme kolu gerekli; budanır, tek koşuma inmez

Tek "en agresif nokta" yetmez: en agresif nokta 1080p'nin **tabanının altında**, orada
1080p yok, kıyas yok. Aranan şey tabanın hemen üstündeki bant (2.3).

Budama sırası:

1. Çözünürlük tavanı, klip × basamak, kodlamasız (2.2). 1080p'nin bir CRF noktasındaki
   puanı basamağın tavanının üstündeyse o basamak o noktada elenir.
2. Kalan basamaklar yalnız en düşük 3 CRF'de (63/59/55). Bu üçü 1080p tabanının çevresi.
3. Her basamağın CRF 63 noktası aynı zamanda o basamağın **tabanı**. Merdivenin alt
   basamağı fizibilite tablosunu bedavaya veriyor; ürünün `FloorBppf`'i oradan gelir.

### 5.3 — Küme rejimleri kapsamıyor; iki klip zayıf, üç sınıf yok

Künyeler (`ffprobe`):

| klip | kodek / bitrate | boyut / fps | renk etiketi |
|---|---|---|---|
| genis-1 animasyon | h264 **5,3 Mbit/s** | 1920x1080 / 60 | yok |
| genis-2 gren | h264 **6,3 Mbit/s** | 1920x800 / 24 | yok |
| genis-3 hareket | h264 6,3 Mbit/s | 1920x800 / 24 | yok |
| genis-4 gren2 | h264 162 Mbit/s | 1920x1080 / 50 | yok |

- **genis-2 gren sınıfını temsil etmiyor.** 6,3 Mbit'lik ToS kopyasında gren zaten
  ezilmiş; ölçtüğünüz şey "önceden sıkıştırılmış temiz görüntü" olur. genis-4 (162 Mbit,
  gerçek gren + yavaş pan) sınıfı tek başına taşıyor. genis-2'yi ya atın ya "ön-sıkıştırılmış
  sinema" diye yeniden etiketleyin — o da geçerli bir sınıf, ama gren değil.
- genis-1 5,3 Mbit'te YouTube kalitesinde referans; 484k'ya 11:1. Animasyon için kabul,
  ama düşük hedeflerde referansın kendi bloklaması kodeğe yazılır.
- **Dört klipte de renk etiketi yok.** Kodlarken `bt709` açıkça etiketleyin; A/B aracının
  renk kapısı "unknown"ı nasıl okuyor, kontrol edin. Aksi halde SDR sonuçları kapıda düşer.
- 1920x800 basamakları 16:9 olmayacak; küçültme basamaklarını "yükseklik ölçeği" olarak
  tanımlayın, sabit 1280x720 değil.

Eksik sınıflar, önem sırasıyla:

1. **Telefon çekimi, SDR, 30 fps, el titremesi + düşük ışık gürültüsü.** Ürünün en olası
   girdisi bu ve kümede yok. 30 fps de yok (24/50/60 var).
2. **Ekran kaydı / metin / arayüz.** Önceki turda listedeydi, düştü. Keskin kenar, uzun
   sabit bölgeler, ani sahne değişimi — kodeğin ve küçültmenin en kötü davrandığı sınıf.
3. **SDR oyun yakalama 60 fps** (yüksek hareket + HUD metni). parça-1/2/3 oyun yakalamaysa
   HDR tarafı bunu karşılıyor; ne olduklarını bilmiyorum, kayda geçsin.

Bir soru: parça-1/2/3 aynı başlıktan mı? Öyleyse HDR bir örnek, üç değil.

### 5.4 — Noktaları bpp katsayısıyla değil CRF'le seçin; agresif uç kendiliğinden kapanır

`ReferenceBppf × {1/32…1/2}` tabanın altına düşebilir (parça-3'te 1/32 muhtemelen düşer) ve
o noktalar kodlanamaz — bütçe yanar. CRF ekseni bunu çözer: CRF 63 tanımı gereği kodeğin
en agresif noktası, altı yalnız küçültme/kare hızı. Her klipte 6 CRF, en agresif uç her
klipte kapsanır, hiçbir nokta boşa gitmez.

Kullanıcının şartını ("her boyut, her rejim") ekseni normalize ederek karşılayın: x =
`teslim_bppf / ReferenceBppf`. Kural bu birimde okunur, SDR/HDR/küçük/büyük aynı eksende.
Üst uçta doygunluk noktası (CRF 39 civarı) şart — "küçültme, fazla harcama" tarafını da
doğrular. SDR 8-bit'te aynı CRF listesi; HDR'de ayrı liste gerekmez, eşik farklı çıkabilir
(önceki tur, soru 10), aynı eksende görünür.

## 6. Serbest

**"Şu koşum olmadan iddia etme" listesi:**

| iddia | gereken |
|---|---|
| Küçültme hiçbir rejimde kazandırmaz | 1080p'nin taban çevresinde CRF noktaları (63/59/55) + çözünürlük tavanları |
| VB sabit boyutta kaybettirir | 882/p4/VB açık/483k koşumu + CAMBI |
| Auto HandBrake'i daha az baytla geçer | Ürünün kendi komut satırıyla yeniden üretim + A/B bandının tek yönlü olması |
| AV1 varsayılan kodek | En az iki SDR klipte aynı sıralama (önceki tur) |

**Gördüğüm başka şeyler:**

- `SONUC.md` E ve B bölümleri parça-2 Auto'yu hâlâ **652x366** yazıyor; `parca-2-auto.md`
  **1650x928** diyor, aynı 82,25 sayısıyla. Bölüm H parça-3 için düzeltti, parça-2 için
  düzeltmedi. Hafızanızdaki "geri çektim denen iddia duruyor" kalıbı.
- 1650x928 → 82,25 ile 1920x1080 → 94,68/95,15 arasındaki fark (~13 puan, %14 piksel
  farkıyla) tek başına dikkat çekici: hafif küçültme bile bu klipte pahalı. 2.2'deki
  tavan ölçümü 1650 için de yapılsın; tavan ~83 çıkarsa mekanizma kesinleşir.
- "27x", "200:1" oranları kaynağa göre; kaynak zaten HEVC 13-15 Mbit. Kliplar arası
  kıyasta oran anlamsız, bpp kullanın (yeni SDR kaynaklardan biri 162 Mbit, biri 5 Mbit).
- `rc_p4_vbsiz` (1080p, 393k, 25,46) ile `kc_p3_882` (484k, 26,01) yan yana "882 ≈ 1080p"
  diye okunmasın; %23 bayt farkı var.
- ffmpeg ilerleme `q=`'sini kanıt olarak alıntılamayı bırakın; paket boyutu tablosu
  (`ffprobe -show_entries packet=size,flags`) hem ucuz hem kare başına. Bu cevaptaki
  1(a) tablosu 6 dosya için ~1 dakikada çıktı.
