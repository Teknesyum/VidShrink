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
