# NVENC 2 Danışması: Fable

Tarih: 2026-09-17. Model: fable (Agent aracı). Soru ve yanıt kelimesi kelimesine.

## Soru

Bir video kodlama ayarı kararı için danışma. Araç kullanma, dosya okuma; yalnız aşağıdaki veriye dayanarak Türkçe yanıt ver.

VidShrink (hedef boyuta sıkıştıran araç) NVENC yolunu HandBrake NVENC'in kalitesinin üstüne çıkarmak istiyoruz, boyut garantisi bozulmadan (ürün çıktıyı hedefe oturtana kadar yeniden dener; son çıktı hedefi asla aşmaz). Makine RTX 5070 Ti, ffmpeg 9.0. Kaynak: Sintel'den üç 10 sn FFV1 kesit (karanlik, parlak, hareketli), 1920x818@24. Ölçü VMAF-NEG ort / p10. Tek bir kodlama ~2 sn.

Bugünkü ürün argümanı (bit hızı yolu): -preset p4 (av1 p6) -b:v B -maxrate B*tepe -bufsize B*(1+2*(tepe-1)) (tepe 1.02..1.10, burada ~1.094) -rc vbr -multipass fullres -g 120 (donanımda anahtar kare tavanı 5 sn = "seek bütçesi" sabiti) -spatial-aq 1 -temporal-aq 1. Plan katmanı hevc/av1/h264'te çözünürlüğü küçültüyor (2000 kbit'te hevc 1420x604, av1 1728x736, h264 1152x490; 1000 kbit'te hevc ~806-998 yükseklik 344-424; 3500'de hevc 1574-1920).
HandBrake (kaynak kodundan): rc vbr, preset p4, rc-lookahead 10, maxrate/bufsize yok, multipass yok, AQ yok, keyint 10 sn, tam çözünürlük.

Ölçümler (eş bayt, her kol -b:v düzeltilerek ±%1,5'e oturtuldu; bazı tam çözünürlük hücreleri 3 denemede %5-10 sapıyor):
1) Ürün geometrisinde argüman kolları, 9 hücre (3 kesit x 3 kodek, 2000 kbit):
 - "yeni" = AQ yok, tepe yok, -g 240, multipass fullres. 9/9 hücrede ürünü geçiyor: ort farkı +0,33..+3,80, p10 +0,24..+15,53 (parlak hevc 85,95/70,14 -> 89,09/85,67; parlak h264 82,18/62,56 -> 85,98/77,42). HandBrake'in aynı geometrideki koluyla başabaş ya da biraz önde.
 - "yeni_aq" (AQ açık) 9/9 hücrede "yeni"den kötü: ort -0,3..-2,3.
 - "yeni_tepe15" (maxrate 1,5x, bufsize 2x): 8 hücrede yeni ile eşit (±0,05), parlak hevc -0,63 ort / -4,96 p10, parlak h264 -0,38 / -2,04.
 - parlak hevc tek hücre ayrıştırma: tepe 1,094 + AQ yok 86,99/71,81; tepesiz + AQ yok 88,82/83,86; tepesiz + AQ 87,50/79,40; +g240 89,09/85,67; rc-lookahead 10 88,80/83,57; tune uhq 88,50/82,72; p7 89,03/83,75; b_ref_mode middle ve multipass qres fark yok.
2) Tam çözünürlükte (yeni argümanlar) tepe 2x/buf 2x tepesizle eşit: parlak hevc 90,51/84,15 (1850 kbps) vs 90,94/84,21 (2008 kbps); hareketli hevc 96,00/88,67 vs 96,07/88,72; karanlik av1 95,41/90,85 vs 95,37/90,77; parlak h264 1488x634 87,09/75,02 vs 87,16/75,13; hareketli h264 1286x548 93,00/83,42 vs 92,89/83,04.
3) -g 120 vs -g 240 (10 sn kesitte 2'ye 1 I-kare): ort -0,2..-0,4, p10 -0,3..-2,6 (parlak h264 -2,59, parlak hevc -0,86).
4) Geometri taraması (yeni argümanlar), VMAF-NEG ort:
 1000 kbit: h264 karanlik 0,34 78,36 / 0,67 80,46 / 1,0 76,04; parlak 0,42 75,67 / 0,71 79,33 / 1,0 79,13; hareketli 0,34 78,44 / 0,67 81,15 / 1,0 71,86.
            hevc karanlik 0,42 82,45 / 0,71 84,78 / 1,0 85,40; parlak 0,52 80,57 / 0,76 83,52 / 1,0 84,46; hareketli 0,42 84,21 / 0,71 87,40 / 1,0 86,16.
            av1 karanlik 0,48 85,55 / 0,74 86,75 / 1,0 84,46 (849 kbps, %11 eksik dolu); parlak 0,6 83,56 / 0,8 85,41 / 1,0 88,00 (1044 kbps); hareketli 0,48 87,82 / 0,74 89,44 / 1,0 90,62 (1035 kbps).
 2000 kbit: h264 karanlik 0,5 90,54 / 0,67 91,55 / 1,0 90,15; parlak 0,6 85,98 / 0,775 87,16 / 1,0 87,37 (1808 kbps); hareketli 0,48 91,23 / 0,67 92,89 / 1,0 91,25.
            hevc karanlik 0,6 92,87 / 0,775 93,90 / 1,0 94,17; parlak 0,74 89,09 / 1,0 90,94; hareketli 0,6 94,40 / 0,775 95,70 / 1,0 96,07.
            av1 karanlik 0,72 94,94 / 1,0 95,37; parlak 0,9 91,45 / 1,0 92,65; hareketli 0,72 96,26 / 1,0 97,24.
 3500 kbit: h264 karanlik 0,68 96,06 / 0,84 96,60 / 1,0 96,21; parlak 0,82 91,37 / 0,91 91,60 / 1,0 92,33 (p10 86,41/85,28/84,29); hareketli 0,66 96,61 / 0,83 97,27 / 1,0 97,01.
            hevc karanlik 0,82 97,28 / 0,91 97,47 / 1,0 97,62; hareketli aynı sıra 98,16/98,41/98,51. av1 0,98 -> 1,0 +0,1..0,2.
 HandBrake tam çözünürlük 2000 kbit: h264 karanlik 88,01 parlak 87,64 (2083 kbps) hareketli 89,78; hevc 94,22 / 91,03 / 96,16; av1 95,38 / 92,21 / 97,14. Yani "yeni" + doğru geometri hevc/av1'de HandBrake ile başabaş, h264'te 0,67 ölçekle +1,5..+3,5 önde.
5) Plan katmanının donanım skoru: skor = min(tahmini_kalite, tavan[hevc/h264 96, av1 98]) - 10*(1/s-1)^1.1; tahmini_kalite = A - 6*log2(gereken_bppf/verilen_bppf), gereken ∝ s^0.55. Tavan bağlamazsa optimum s≈0,5 her bit hızında; tavan bağlarsa tavana ulaşan en büyük s.

Sorular:
a) Tepe: NVENC'te maxrate/bufsize'ı tamamen kaldırmak mı, yoksa 2x/2x gibi geniş bir tavan mı? Ürün aşımı yeniden deneyerek düzeltiyor; ama önceki bir ölçüm (400 sn 1080p60 av1) tepe açıldıkça aşımın büyüdüğünü gösteriyordu (1,05 -> 1,034, 1,50 -> 1,098). Hangisi, neden?
b) Anahtar kare tavanı: donanımda 5 sn "seek bütçesi" olarak sabit. 10 sn kesitlerde 10 sn'ye çıkarmanın getirisi küçük ve kesit boyuyla şişmiş olabilir. Değiştirmeli miyim, yoksa kapsam dışı bırakıp raporda bulgu olarak mı yazmalıyım?
c) Geometri: en az riskli uygulama nedir? Seçenekler: (i) NVENC kodek başına ölçek cezası çarpanı (h264 ~1,5, hevc/av1 ~3), (ii) NVENC için kodek başına asgari ölçek (h264 0,67, hevc 0,75, av1 0,8 gibi), (iii) donanım tavanlarını düşürmek. Üç kesit x üç bit hızı x üç ölçek veriyle aşırı uyum riski nasıl tartılmalı; dördüncü bir tutulmuş kesit ("orta", parlak/düşük hareket) doğrulama için var.
d) p7/uhq/lookahead gibi ince kollar gürültü içinde; eklemeli miyim?
Kısa, karar odaklı yanıt ver: her soru için seçim + bir iki cümle gerekçe + nelere dikkat.

## Yanıt

## a) Tepe: 2x/2x açık yazılsın, tamamen kaldırma

Seçim: `-maxrate 2B -bufsize 2B`. Ölçüm ikisini eşit gösteriyor (tam çözünürlükte beş hücre, ürün geometrisinde 1,5x sadece parlak hevc'yi ısırıyor, 2x'te o da kapanıyor). Eşitken açık değeri seçmenin nedeni belirlenimlilik: `maxrate` verilmeyince nvenc `maxBitRate=0` bırakır ve sürücü kendi varsayılanını uygular; bugün 2x'e denk gelen bu varsayılan sürücü sürümüyle değişebilir. Açık 2x, aşım üst sınırını da senin elinde tutar; yeniden deneme döngüsünün maliyeti bu sınıra bağlı.

Dikkat: Bugünkü formül (`bufsize = B*(1+2*(tepe-1))`) tepe 2'de 3x buf üretir; ölçülen çift 2x/2x, formülü değil ölçüleni yaz. Ürün geometrisinde parlak hevc'de 2x hücresi ölçülmedi (yalnız 1,5x ve tam çözünürlük var), tek hücre 2 sn, mühürden önce koş. Aşım tarafında 1,5x'te %9,8 görüldüyse 2x'te ilk geçişin çoğu zaman hedefi aşacağını varsay: ilk `-b:v`'yi beklenen aşımla önden küçültmek (0,92B gibi) uzun dosyada bir tam kodlamayı kurtarır; bu katsayıyı 400 sn'lik dosyada bir kez ölç.

## b) Anahtar kare: 5 sn kalsın, rapora bulgu

Seçim: değiştirme, kapsam dışı. Bu bir kalite düğmesi değil ürün özelliği (arama gecikmesi); ölçülen kazanç ort 0,2-0,4, p10 en kötü 2,6 ve tam çözünürlük hücrelerinin kendi salınımı %5-10. Geometri düzeltmesinin +1..+3'ü yanında ikinci sıra.

Dikkat: 10 sn kesitte etki şişmiş değil, saniye başına I-kare yoğunluğu uzun içerikte de aynı; ama gerçek içerikte sahne kesimleri zaten I-kare ekler ve farkı seyreltir. Raporda "5 sn'nin ölçülen bedeli" diye yaz, kullanıcı isterse ayar olur.

## c) Geometri: (i) kodek başına ceza çarpanı, NVENC yolunda

Seçim: (i). Yapıyı bozmadan tek parametre/kodek; 27 nokta/kodek karşısında tek skalerde aşırı uyum riski düşük, asıl risk biçim hatasıdır ve o zaten bugünkü modelde var (yazılım kodlayıcı için kalibre edilmiş `s^0.55`, NVENC'te küçültmenin karşılığını veremiyor). (ii) taban 0,75 parlak hevc'yi 89,09'da bırakır, HandBrake'in 91,03'üne yetişmez; ölçüm aralığı dışında (300-600 kbit) küçültmeyi de sertçe yasaklar. (iii) tavan indirme süreksiz davranır: 1000 kbit'te tavana ulaşılamayınca model yine 0,5'e düşer.

Uydurma ölçütü ondalıklar değil argmax olsun: çarpan, her (kesit, bit hızı) hücresinde modelin seçtiği ölçeği ölçülen en iyinin 0,3 VMAF platosuna düşürmeli. Kalıp sağlam ve kodek düzeyinde: hevc/av1 ≥2000'de 8/9 hücre 1,0'a tek düze, 1000'de 0,7-1,0 plato; h264 her bit hızında iç optimum 0,67-0,84. Çarpanı bu kalıba uydur. av1 karanlik 1000/1,0 hücresi %11 eksik dolu, uydurmaya sokma; av1 tam çözünürlükte karanlık içerikte QP tabanına çarpıyor olabilir, ayrı not.

Doğrulama: "orta" kesitte önce modelin ölçeğini yazdır, sonra üç ölçek kodla; geçme ölçütü "en iyinin 0,3 içinde", "tam tahmin" değil. Kodlama 2 sn olduğuna göre bir de aralık dışı satır ekle (hevc/av1 600 kbit): çarpan 3'ün orada hâlâ küçültüp küçültmediğini gör, model o bölgede kör.

## d) İnce kollar: ekleme

Seçim: p7, uhq, lookahead 10 girmesin. Üçü de gürültü içinde (p7 +0,2/-1,9 p10, uhq -0,6, lookahead -0,3) ve p7 kodlama süresini yeniden deneme sayısıyla çarpar; uhq sürücü/kodek bağımlı. Raporda "denendi, ayrışmadı" satırı yeter.

Bir uyarı: AQ'yu kapatmak VMAF-NEG'de kazandırdı, ama VMAF AQ'nun yeniden dağıttığı biti ödüllendirmez; temporal AQ NVENC'te zaten şüpheli, spatial AQ algısal olarak genelde kazandırır. Hedef HandBrake'i bu ölçüde geçmek ve HandBrake'te de AQ yok, karar savunulur; raporda "metrik güdümlü seçim" diye açık yaz.

## Mühürden önce ölç

1. Ürün geometrisinde parlak hevc, 2x/2x tek hücre.
2. 400 sn dosyada 2x/2x ilk geçiş aşımı (önden küçültme katsayısı için).
3. Çarpanla modelin her hücrede seçtiği ölçek, plato tablosu; "orta" ve 600 kbit doğrulaması.


# İkinci Danışma: Ağırlık Kapısı

Tarih: 2026-09-17. Model: fable (Agent aracı). Soru ve yanıt kelimesi kelimesine.

## Soru

Kısa bir karar danışması (Türkçe yanıtla, en çok 15 satır, kod okumana gerek yok; veriler aşağıda).

VidShrink yerleşim puanı: score = rate − ScalePenalty·W − FpsPenalty·W. NVENC için W'yi h264 2.5, hevc/av1 4.5 yaptık (eski W=1). Gerekçe: 1920x818@24 kaynakta 1000/2000/3500 kbit'te eski W küçük kareyi seçiyordu, tam çözünürlük VMAF-NEG'de 3.9 puana kadar daha iyiydi. Bu noktada bütçenin tam kaynak karedeki "kullanılabilir taban" oranı (bppf / taban_bppf(fps), taban_bppf = (4.29e-3 + 0.0756/fps)·1.15) 1000 kbit'te 3.1, en düşük ölçtüğümüz 600 kbit hücresinde 1.86 (orada hevc/av1 yeni geometri HandBrake tam çözünürlükle başa baş).

Çatışma: eski, canlı ölçülmüş bir ızgara var: av1_nvenc 800k, 1080p60 kaynak, VMAF-NEG 1920x1080 31.8, 1600x900 37.1, 1280x720 38.7, 960x540 40.0 (kodlayıcı 1080p60'ı ~624k altına indiremiyor). Tam karede taban oranı ≈1.0. Yeni W=4.5 bu ızgarada puan sırasını ters çeviriyor (1080p60'ı seçer) ve pinli test kırılıyor.

Seçenekler:
A) W'yi tam kaynak karedeki taban oranına bağla: oran ≤1.0 → W=1, oran ≥1.86 → tam W, arada doğrusal. İki uç da ölçülmüş noktalar; ara ölçülmedi.
B) W'yi sabit bırak, ızgara testini av1_qsv'ye taşı (ölçüm av1_nvenc'teydi; dürüst değil).
C) W'yi yalnız fps düşmeyen/kaynak fps ≤30 durumlarına uygula.
D) Başka.

Hangisi? Kısa gerekçe ve varsa A'daki eşik/biçim için düzeltme. Ek kodlama bütçemiz ~10 dk; yeni ölçüm gerekirse en fazla 1-2 hücre önerebilirsin.

## Yanıt

**A**, küçük düzeltmelerle.

Gerekçe: iki ızgara çelişmiyor, aynı eğrinin iki ucunu ölçmüş. Tam karedeki taban oranı ≈1.0'da kodlayıcı tabanda, kare küçültmek 8 puan kazandırıyor; ≥1.86'da bütçe tam kareyi taşıyor. Cezanın ağırlığı bütçeye bağlı olmalı — W sabitse bir ucu her zaman yanlış seçer. B dürüst değil (ölçüm av1_nvenc'te). C bir vekili (fps) nedenin (bppf) yerine koyar; 1080p60 ızgarasında fps düşmüyor, yine de küçük kare kazanıyor — C'yi çürüten veri elinde zaten var.

A için biçim: `t = clamp((oran − 1.0) / 0.86, 0, 1)`, `W = 1 + (W_kodek − 1)·t`. Oran iş başına tek skaler (tam kaynak kare, kaynak fps), adaylara göre değişmez; sıralama yalnız W üzerinden kayar. Pinli test oran≈1.0'da W=1'e döner, kırılmaz.

İki uyarı:
- 1.86 "başa baş" noktası: tam kare ile küçük kare orada eşitse rampanın **ortası** orası olmalı, sonu değil; tam W'yi 1.86'da vermek geçiş noktasında tam kareyi zorlar. Elde yalnız iki uç varken rampayı 1.0→1.86 bırak ama üst ucu bir hücreyle doğrula (aşağıda).
- Oran içerik karmaşıklığını görmez; 1080p60 ızgarası yüksek hareketliyse eşik biraz iyimser kalır. Bunu şimdilik kabul et, ölçüm satırına yaz.

Ölçüm önerisi (1-2 hücre, ~10 dk içinde): oran ≈1.4 noktası — av1_nvenc 1080p60 kaynakta ~1150 kbit (bppf 0.0092 / taban 0.0064). Doğrusal rampa orada W≈2.6 verir; 1920x1080 ile 1280x720'yi VMAF-NEG'de kıyasla, puan sırası rampanın seçimiyle uyuşuyorsa A mühürlenir; uyuşmuyorsa üst eşiği 1.86'dan 2.3-2.5'e kaydır. İkinci hücre kalırsa 1920x818@24 kaynakta ~450 kbit (oran ≈1.4), aynı kontrol.

Yok