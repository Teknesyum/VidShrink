# Konsey: Karşılaştırma paneli

**Tarih:** 24.08.2026
**Üyeler:** fable ve opus, aynı brifing, birbirlerini görmeden
**Karar veren:** T0

Kullanıcının tarifi: ekranın ortasında, "Yapılacak işlem" bölümünün üstünde bir panel.
Tek kare, sol yarı orijinal, sağ yarı sıkıştırılmış, aradaki çizgi sürüklenebilir. Dosya
yüklenince kendiliğinden gelir, düğme yok. Fare aşağı inince medya kontrolleri belirir,
normalde saydam. Tekerlek yakınlaştırır. Panel diğer panellerin üstünde durur.

---

## Ortak çıkan kararlar — doğrulanmış sayılıyor

### 1. Sağ tarafta gerçekten kodlanmış kareler gösterilecek

İkisi de aynı şeyi buldu ve aynı yere vardı.

`CalibrationProbe.cs:182` **zaten** planın kendi codec, preset ve CRF değerleriyle 2
saniyelik pencereler kodluyor — ve `:208` satırında `-f null` ile pikselleri çöpe atıyor.

Yani gerçek sıkıştırılmış kare üretmek yeni bir maliyet değil, **hâlihazırda atılan bir
çıktı.**

Bu, kullanıcının istediğinden daha iyisini veriyor: iş bitmeden önce bile sağ tarafta
gerçekten kodlanmış görüntü durabilir.

Etiket sözleşmesi: "bu ayarlarla örnek kodlama" denir, "çıktınız" denmez. Koşum bitince
gerçek dosyaya döner.

### 2. Duran kare, oynatma yok

İkisi de reddetti. Opus sayıyla gerekçelendirdi: 1080p yuv420p'de kare başına ~3,1 MB,
24 fps'te saniyede ~75 MB ham veri — **iki dosya için iki katı**, üstüne renk çevrimi ve
iki akışın kare kilidi.

Bu, adı konmamış bir oynatıcı çekirdeğidir; önceki konseyin yasakladığı şeyin ta kendisi.

Karşılığında: kullanıcının sorusu ("sıkıştırma neyi bozdu") **duran karede daha iyi**
cevaplanıyor. Artefakt hareket hâlinde değil dururken incelenir.

### 3. Tekerlek iki ayrı jest olmalı

İkisi de kullanıcının tarifine katılmadı ve aynı gerekçeyi verdi.

Tekerlek yalnız görüntüyü yakınlaştırsın. Panel boyutu ayrı bir denetimle değişsin.

Sebep: her tekerlek çentiğinde panel yüksekliği değişirse sekmenin tamamı yeniden
yerleşir ve "Yapılacak işlem" bölümü, kullanıcının gözü karedeyken aşağı yukarı zıplar.

Opus bir uzlaşma önerdi: 1:1'in üstüne çıkıldığında panel **bir kez** büyük kipe terfi
etsin. İstenen his verilir, sürekli sarsıntı verilmez.

### 4. Popup kullanılmayacak

İkisi de reddetti. Avalonia'da `Popup` Windows'ta ayrı bir üst düzey penceredir; bu
pencere `ExtendClientAreaToDecorationsHint` ve saydamlıkla çalışıyor, popup oraya kötü
oturuyor. Ayrıca hafif kapanma işaret yakalamayı çalar ve **sürgü sürüklemesini öldürür.**

### 5. Panel ayrı bir denetim dosyasına yazılacak

`MainWindow.axaml.cs` zaten 1700 satırın üstünde ve dört sekmenin mantığını taşıyor.
Panel oraya girerse sürgü, yakınlaştırma ve kare önbelleği kodlama akışıyla iç içe geçer.

### 6. stdout'tan PNG borularken stderr eşzamanlı boşaltılacak

İkisi de aynı taramadan aynı bulguyu getirdi: stderr boşaltılmazsa ffmpeg asılır. Bu
Jellyfin'in bizzat yaşadığı bir hata, `RAPOR.md:27` içinde kayıtlı.

### 7. Önce ölçüm kapısı

İkisi de kod yazmadan önce ölçüm istedi: kare çekme gecikmesi, kodlama sürerken aynı
işlemin maliyeti, örnek kodlamanın süresi, bitmap bellek maliyeti.

Opus eşikleri de yazdı: 1080p'de p95 < 150 ms ise sürgü canlı; 150-400 ms ise gecikmeli;
> 400 ms ise yalnız önceden çekilmiş sabit noktalar. Kodlama yavaşlaması %5'i geçerse
kare çekimi koşum boyunca kapatılır.

---

## Ayrışma — "üstte durma" nasıl kurulacak

**Fable:** yerleşimde en üst bölüm. Büyük kipte kök `Grid`'e `ZIndex`'li merkez katman.

**Opus:** Shrink sekmesinin tamamı bir `ScrollViewer` içinde (`MainWindow.axaml:147`).
Sütun içine konan panel büyüdüğünde **kırpılır** ve kaydırmayla ekrandan çıkar. Üstte
durabilmesi için büyümüş hâlinin `ScrollViewer`'ın dışında, sekme kökünde barınması
gerekir.

Önerisi: **tek içerik, iki barınak.** Küçük kipte orta sütunun 0. satırı, büyük kipte
kök katman.

**T0 kararı: opus.** Gerekçe somut ve dosyadan geliyor — `ZIndex` ebeveynin kırpmasından
kaçamaz. Fable'ın önerisi küçük kipte doğru, büyük kipte çalışmaz.

---

## Yalnız opus'un gördükleri

Konseyin asıl kazancı burada. Dördü de gerçek ve hiçbiri fable'ın cevabında yok.

**Döndürme üstverisi.** Telefonla dikey çekilmiş videoda kaynaktan çekilen ham kare yan
yatar; çıktıda döndürme pişmiş olabilir. İki yarı ters yönde durur.

**HDR.** Kaynak PQ/HLG ise ham kare soluk gri görünür. Plan SDR'a çeviriyorsa çıktı
normal görünür ve kullanıcı bunu **"sıkıştırma rengi bozdu" diye okur.**

**Kare hizası.** Fps düşürülmüşse çıktıda kaynağın zaman damgasında kare yoktur; en yakın
kare **başka bir andır** ve hareketli sahnede dikiş kopuk görünür.

Çözümü de yazdı: eksen çıktının kare ızgarasına kilitlenir, kaynak ondan türetilir.

**Pencere alt sınırı.** 1040×720'de önizleme ve plan paneli aynı anda sığmaz. Panelin
kendini katlayacağı bir eşik olmak zorunda.

**Süreç sağanağı.** Hızlı sürgüde onlarca kısa ömürlü ffmpeg süreci doğar; Windows'ta uç
nokta güvenliği her açılışa 50-200 ms bindirir.

---

## Kullanıcının illüzyon önerisinin akıbeti

Kullanıcı sonradan netleştirdi: amaç sahte çıktı sunmak değil, "orijinalin bu kısmı
işleme sokuluyor" demekti. Yani yanıltma kaygısı zaten yoktu.

Konsey bundan bağımsız olarak **daha iyisini** buldu: sağ tarafta gerçekten kodlanmış
örnek kareler. Kullanıcının istediği ilerleme göstergesi de kalıyor — zaman çizgisinde
`out_time_ms`'ten gelen konum imleci, "analiz 1/2 · deneme 2" etiketiyle.

Opus'un dürüstlük borcu olarak yazdığı tek şey: örnek kodlama, tam koşumdaki hız
denetimiyle birebir aynı değil. Bu yüzden etiket "örnek" der ve koşum bitince gerçek
dosyaya döner. Örneğin nihai çıktıdan ne kadar saptığı **ölçülmeden** bu dalga
ilerlemeyecek.

---

# İkinci tur: tekerlek jesti

Kullanıcı ilk turun "tekerlek iki ayrı jest olsun" önerisine katılmadı ve daha net bir
tarif verdi:

> Tekerlek zoom yapacak panele ve videoya, ancak panelin genişleyeceği daralacağı alan
> hep belli, max da tüm paneli kapsayacak. Zoom yaparken panel hep belli bir rotada
> büyüyüp küçülecek, ve bu akıcı dinamik bir şekilde olacak. İki kademe statik değil,
> mümkünse animatif bir yöntemle.

İkisine de bu düğüm tek başına soruldu.

## Ortak çıkan kararlar

### Tek jest kabul — ama büyüme komşusunu itmeyecek

İkisi de itirazlarının **büyümeye değil, büyümenin komşusunu itmesine** olduğunu söyledi.
Kullanıcının koyduğu "alan hep belli" kısıtı bunu zaten çözüyor.

Kurulum: orta sütunda panele **sabit bir bant** ayrılır, `PlanPanel` bandın altındaki
satırda kalır. Panel bandın içinde büyür. Böylece `PlanPanel`'in konumu tekerlekten hiç
etkilenmez.

Opus bir şart ekledi: bant **tekerlekle değil, dosya yüklenirken bir kez** ayrılır. O tek
hareket animasyonlu olabilir; sonrasında tekerlek yerleşimi bir daha kıpırdatmaz.

### İki büyüklük, iki ayrı araç

İkisi de aynı ayrımı yaptı ve gerekçeleri örtüşüyor:

| Ne büyüyor | Araç | Neden |
|---|---|---|
| Görüntünün yakınlaştırması | `RenderTransform` (`ScaleTransform`) | Yerleşim geçersizleşmez, GPU'da bileşimlenir |
| Panelin boyutu | Gerçek `Height` | `RenderTransform` kenarlığı, köşe yarıçapını ve metni de ölçekler, bulanıklaştırır |

Opus geçersizleşmenin nereye kadar yayıldığının bizim elimizde olduğunu ekledi: bant
sabit yükseklikte bir `Grid` satırıysa, çocuğun boyutu değişse bile satırın kendi ölçümü
değişmez — geçersizleşme satırda durur, `PlanPanel`'e ve `ScrollViewer`'a ulaşmaz.

Buradan çıkan kural: **büyüyen bölgenin içinde sarmalı metin, `WrapPanel` ve iç içe
`ScrollViewer` olmayacak.** Kare düşüren şey `Height` değişimi değil, bunların her karede
yeniden ölçülmesi.

Opus ayrıca büyüyen kenarlıkta `BoxShadow` olmamasını istedi — gölge her karede yeniden
bileşimleniyor ve ölçek animasyonunda en pahalı kalem o.

### Tek jest parametresi, iki sayaç değil

İkisi de bağımsız olarak aynı şeyi söyledi: yakınlaştırma tek bir kayan değerden
türetilmeli. Panel boyutu da yakınlaştırma da o değerin fonksiyonu olmalı.

Fable'ın gerekçesi: iki ayrı sayaç tutulursa ileri-geri çevirmede yuvarlama hatası
birikir ve başlangıç konumuna tam dönülmez.

Opus'un gerekçesi: ikisi farklı noktalarda tavana varırsa jestin geri kalanı ölü hisseder.

### Tekerlek çatışması baştan çözülecek

İkisi de aynı tuzağı gösterdi: panel `ScrollViewer` içinde. İmleç panelin üstündeyken
tekerlek zoom mu yapacak, sayfayı mı kaydıracak?

Olay `Handled` işaretlenmezse ikisi birden olur. Kural baştan konacak.

Opus bir incelik ekledi: tekerlek yalnız **görüntü alanında** tüketilsin, panelin kenar
boşluğunda sayfaya geçsin. Yoksa geniş bir panel sayfa kaydırmayı büyük ölçüde yutar.

### Azaltılmış hareket

`Window.reduced-motion` sınıfı zaten var (`MainWindow.axaml:18`). O sınıfta yumuşatma
kapanır, değer doğrudan hedefe oturur. Jest yine sürekli çalışır.

## Ayrışma — yumuşatmanın aracı

**Fable:** `Transition` kullanılsın (`ScaleX`/`ScaleY` üzerinde `DoubleTransition`).
Hedef değiştiğinde Avalonia mevcut değerden yeni hedefe yeniden yönelir, kuyruk oluşmaz.
Süre `MotionFast` (160 ms).

**Opus:** `Transition` ayrık durum değişimleri için tasarlanmış; tekerlek **sürekli** bir
jest. Kuyruk oluşmaz ama **gecikme** oluşur — panel tekerleğin çeyrek saniye gerisinden
gelir, lastik gibi hissedilir. Doğrusu: tekerlek hedefi anında günceller, gerçek değer
hedefe kare başına kritik sönümlü bir takiple yaklaşır. Zaman sabiti yine `MotionFast`.

**T0 kararı: opus.** Gerekçe: kullanıcı "akıcı ve dinamik" istedi. Sürekli bir jestte
geçiş temelli yumuşatmanın lastik hissi verdiği bilinen bir şey. Fable'ın `Transition`
önerisi bandın **tek seferlik açılışı** için doğru ve orada kullanılacak.

## Yalnız opus'un gördüğü — en kritik bulgu

**Yakınlaştırmak yeni detay getirmez.**

Kare, panel genişliğinde çözülüp bitmap'e alınıyor. Kullanıcı 4× yakınlaştırdığında
küçültülmüş bir çözümün büyütülmüş pikselini görür.

Bunun iki sonucu var ve ikincisi ağır:

1. Artefakt incelemek için değersizdir
2. **Kullanıcı "sıkıştırma bunu bozdu" sandığı şeyin bizim ölçeklememiz olduğunu fark
   etmez**

Panelin varlık sebebi "sıkıştırma neyi bozdu" sorusuna cevap vermek olduğu için bu, tek
başına özelliği anlamsızlaştırabilecek bir hata.

Çözümü de yazdı: yakınlaştırma seviyesi kare servisine geri beslenecek. İstenen piksel
genişliği = panel genişliği × yakınlaştırma, kaynağın kendi çözünürlüğüyle tavanlanır, ve
servis **ne teslim ettiğini** bildirir. Panelde "1:1" ve "kaynak sınırı" durumları
görünür olur.

Buradan çıkan davranış: her tekerlek çentiğinde yeniden kare çekilemez, yoksa çentik
başına bir ffmpeg süreci doğar. Yakınlaştırma anında eldeki bitmap'le akıcı ilerler,
tekerlek **durduktan** ~250 ms sonra daha yüksek çözünürlüklü kare gelip yerine geçer.

Görüntünün bir an sonra netleşmesi kasıtlı ve söylenmiş bir davranış olacak, sürpriz
değil.

## Opus'un diğer üç bulgusu

**İmleç etrafında yakınlaştırma kayar.** Çapa noktası görüntü koordinatında tutulup
öteleme her karede güncel panel dikdörtgeninden yeniden türetilmezse, görüntü imlecin
altından kayar. Ölçüsü basit: sabit bir imleçte yakınlaştır, imlecin altındaki içerik
noktası aynı kalmalı.

**Dokunmatik yüzeyde jest fırlar.** Hassas dokunmatik yüzeylerde tekerlek deltası kesirli
ve çok sık gelir. "Bir çentik = bir kademe" varsayılırsa jest fırlar. Delta üstel bir
katsayıya çarpılmalı.

**Küçük pencerede yerinde büyüme anlamsız.** 720 px pencerede başlık ve kenar payları
düşünce ~620 px kalıyor, `PlanPanelMinHeight` 320 px, geriye ~260 px. Ölçülebilir kural:
kullanılabilir yükseklik < 700 px ise yerinde büyüme hiç yapılmaz.

## Fable'ın diğer iki bulgusu

**Max'ta kontrol erişimi.** Panel tüm alanı kaplayınca iptal düğmesi ve ilerleme paneli
altında kalır. Kodlama sürerken kaçış yolu şart — Esc yakınlaştırmayı sıfırlasın.

**Sürgü tutamacı ölçekle küçülür.** Min boyutta ayırıcının tıklama alanı daralır.
Tutamacın tıklama payı yakınlaştırmadan bağımsız sabit piksel olmalı.

## Karara bağlanmamış — kullanıcıya sorulacak

Opus "max da tüm paneli kapsayacak" cümlesinin iki türlü okunduğunu söyledi:

- **A:** Tavan panelin **kendi bandının** tepesi. Terfi yok, tek barınak, tasarım basit.
- **B:** Tavan **tüm sekmeyi** kaplamak. Bant tepesine varınca kök katmana terfi edilir.

Opus A'yı öneriyor: B'nin tek kazancı büyük görüntü, bedeli devir teslim anındaki sıçrama
riski ve iki yerde yaşayan bir denetim.

B seçilirse teknik şartı da yazdı: terfi anında panelin dikdörtgeni ölçülür, kök
katmandaki kopya tam o dikdörtgende doğar, banda aynı boyutta yer tutucu bırakılır, ve
geri dönüşte histerezis olur (terfi 1.00'de, iniş 0.92'de) — yoksa titrek tekerlekte
panel iki barınak arasında çırpınır.

Ayrıca `t=1`'in ötesinde tekerleğin ne yapacağı kararlaştırılmalı: hiçbir şey mi, yoksa
yalnız görüntüyü yakınlaştırmaya devam mı.

---

## Kullanıcının kararı — 24.08.2026

**Tavan: tüm program.** Opus'un B okuması seçildi. Panel en büyük hâlinde tam ekrana yakın
bir görüntü verecek, boyutu programın tamamı kadar olacak.

Opus'un B için yazdığı teknik şartlar bağlayıcı:

- Terfi anında panelin pencere içindeki dikdörtgeni ölçülür
- Kök katmandaki kopya **tam o dikdörtgende** doğar
- Banda aynı boyutta bir yer tutucu bırakılır
- Geri dönüşte histerezis olur: terfi t=1.00'de, iniş t=0.92'de

Histerezisin sebebi: yoksa titrek tekerlekte panel iki barınak arasında çırpınır.

**Tavana varınca tekerlek durur.** t=1'in ötesinde yakınlaştırmaya devam edilmeyecek.
