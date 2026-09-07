# VidShrink — amaç, motor incelemesi ve geliştirme önerileri

İnceleme tarihi: 6 Eylül 2026. Kod tabanı: `631a27d5`. Bu rapor mevcut kaynak kodunun statik incelemesidir; yeni bir sıkıştırma karşılaştırması veya kalite ölçümü değildir. Kodda bulunan sabitler ölçüm sonucu olarak sunulmamıştır. Önerilen deneylerin sonuçları ancak `tools/VidShrink.Bench` üzerinden üretildikten sonra iddia edilmelidir.

## Anladığım amaç

VidShrink'in vaadi, kullanıcıyı video kodlama uzmanı yapmadan bir dosyayı paylaşılabilir boyuta indirmek. Hedef boyut sert sınır; bu sınır içinde korunmak istenen şey yalnız çözünürlük değil, izlerken hissedilen bütünlük: ayrıntı, akıcılık, ses ve renk. Kullanıcı hangi kaybın neden seçildiğini anlayabilmeli. Yerel çalışma, kolay açılış, sağ tık akışı ve karşılaştırmalı önizleme bu vaadi tamamlıyor.

Bu nedenle başarıyı yalnız “dosya küçüldü” veya “VMAF yükseldi” ile tanımlamazdım. Boyut sınırının tutulması, kabul edilebilir görünüm, toplam bekleme süresi ve kararların açıklanabilirliği birlikte değerlendirilmeli. `FillTarget` ile `QualityCeiling` ayrımı da korunmalı: kalite kazanımı bitmişken bütçeyi doldurmak her kullanıcı için fayda değil.

## Mimari ve motorun bugünkü akışı

- `VidShrink.Core`: içerik modeli, sıkıştırma stratejisi, plan araması, bütçe düzeltmesi ve FFmpeg argümanları. Bunun yanında paylaşım, güncelleme ve kabuk sözleşmeleri de burada bulunuyor.
- `VidShrink.Ffmpeg`: medya bilgisi okuma, içerik ve kodlayıcı yoklamaları, kalibrasyon, kalite ölçümü, kodlama süreçleri ve oynatma borusu.
- `VidShrink.App`: Avalonia arayüzü, ayarların uygulanması, ölçüm–yeniden planlama döngüsü, hızlı küçültme ve karşılaştırma deneyimi.
- `tests` ve `tools`: regresyon koruması ile gerçek medya üzerindeki ölçüm düzenekleri. Kaynak ağacında bu konuya ciddi yatırım var.

Ana pencerenin akışı `MainWindow.MeasureComplexityAsync` içinde izlenebiliyor: sahne haritası çıkarılıyor; `ComplexityProbe` kaynak ve küçültülmüş örneklerden bit maliyeti ile ayrıntı değişimini, ayrıca hareket örneğinden kare hızı etkisini kestiriyor. Kullanılabilir ve karşılaştırılabilir kalite ölçümleri profile ekleniyor. `PlanCalculator.BuildDetailed` kodek, ses bütçesi, çözünürlük/kare hızı ve kodlama modunu seçiyor. `CalibrationProbe` seçilen taslağı farklı kalite noktalarında ölçüyor; plan değişirse sınırlı yeniden kalibrasyon yapılıyor.

`EncodeRunner` gerçek dosya boyutunu kontrol ediyor, gerekirse planı düzeltiyor ve kodlama çıktısını hedefe taşımadan önce tavanı denetliyor. Önceden elde edilmiş küçük bir çıktı varsa onu yedek olarak saklaması, başarısız yeniden denemede kullanıcı emeğini koruyan iyi bir karar.

## Korunması gereken güçlü yönler

**Kaynak bit hızından daha anlamlı bilgi toplamak.** `ComplexityProfile` içerik maliyetini, ölçek etkisini ve zamansal maliyeti ayrı modelliyor. Aynı çözünürlükteki ekran kaydı ile hareketli çekimin aynı probleme indirgenmemesi ürünün esas değeri.

**Teslim sınırını tahminden ayırmak.** Normal kodlama kolunda tavan kontrolü gerçek dosyaya uygulanıyor. Tahmin iyi değilse bile büyük dosya başarı diye teslim edilmiyor. `EncodeRunnerAtomicOutputTests`, `FillBandTests` ve ilgili testler bu yaklaşımın etrafında koruma oluşturuyor.

**Bilginin sınırını ifade etmek.** Kodlayıcının derlemede bulunması, çalışması ve henüz ölçülmemiş olması ayrılmış. HDR yeteneğinin ayrıca ele alınması, karşılaştırılamayan kaliteyi sayı gibi sunmama yaklaşımı ve düşürülen FFmpeg seçeneklerinin raporlanması değerli.

**Kararı görünür kılmak.** `ReasonNote` ve tavsiye kodları, manuel isteğin uygulanamadığı durumları da açıklıyor. Daha fazla otomatik karar eklenecekse bu açıklanabilirlik kaybolmamalı.

## Öncelikli bulgular

### 1. Kalibrasyonun geçerlilik sözleşmesi eksik

**Kod kanıtı:** `ComplexityProfile.cs` içindeki `CalibrationSignature.Matches`, yalnız kodek, ölçek ve kare hızını karşılaştırıyor. İmzada genişlik/yükseklik var ama eşleşmede kullanılmıyor; preset, renk/filtre yolu ve hız denetimi kimliği yok. `CalibrationProbe.RunAsync` ise belirli bir taslak üzerinden ölçüyor.

**Etkisi:** Aynı geometri ve kodekte preset veya HDR/filtre ayarı değişen bir planın eski kalibrasyonu geçerli sayılmasını model düzeyi engellemiyor. Her arayüz yolunun bunu ayrıca doğru temizlemesine bağımlı olmak kırılgan. Bu, her kullanımda yanlış çıktı oluştuğu iddiası değil; sözleşmenin ölçülen koşulu tam tanımlamadığı bulgusudur.

**Öneri:** Kalibrasyon anahtarını fiilen üretilen kodlama koşullarından oluşturun. Preset, geometri, kare hızı, piksel biçimi, renk/filtre zinciri ve ilgili hız denetimi parametrelerini kapsasın. Kalıcı önbellek yapılırsa kaynak kimliği ile FFmpeg/kodlayıcı sürümü de anahtara girsin. Anahtar değiştiğinde ya yeniden ölçün ya da tahmini açıkça kalibrasyonsuz sınıfa indirin.

**Kabul:** Preset ve renk yolu değişiklikleri eski ölçümü geçersiz kılmalı; aynı koşuldaki yeniden hesaplama gereksiz ölçüm başlatmamalı.

### 2. Kalite ölçümü bağlı; fakat plan hâlâ ölçülen kalite eğrisinin tamamını bilmiyor

**Kod kanıtı:** `MainWindow.ProbeWithMeasuredQualityAsync`, pencere sonuçlarından yalnız `VmafNegMean` değerlerini alıp `WithProbeQuality` çağırıyor. Bu metot her kalite değerini aynı `ProbeBppf` ile eşliyor. `WithMeasuredQuality` eğim öğrenebiliyor, ancak bu üretim yolunda bit maliyeti ekseninde yayılım oluşmadığı için eğim önsel değerde kalıyor. `PlanCalculator.Decompose` buna ölçek ve kare hızı cezaları ekliyor.

**Etkisi:** “Kalite ölçümü motora bağlı değil” demek artık yanlış. Buna karşılık sonuç dosyasının algısal kalitesinin doğrudan ölçüldüğünü veya her adayın kalite eğrisinin öğrenildiğini söylemek de fazla güçlü. Ortalama kalite çıpası, kötü sahneleri ve içerik türüne göre farklı kayıp tercihlerini tek başına temsil etmiyor.

**Öneri:** Önce ölçülen referans kalite, modelden tahmin edilen kalite ve son çıktı ölçümünü sözleşmede açıkça ayırın. Sonra aynı pencereyi farklı kodlama noktalarında ölçerek yerel bir kalite eğrisi kurmayı deneyin. Farklı sahnelerin farklı kalite değerlerini tek başına bit hızı eğrisi sanmayın. Kuyruk kalitesi için alt yüzdelik veya kötü sahne ölçüsü karar verisine taşınabilir; tek kötü karenin bütün planı yönetmesi de önlenmeli.

**Kabul:** Yeni seçimler aynı boyut koşulunda mevcut planlarla karşılaştırılmalı; ortalama yanında kötü sahne görünümü ve toplam hazırlık maliyeti raporlanmalı. Ekran yazılarının okunabilirliği ve hareket akıcılığı görsel kontrolden geçmeli.

### 3. İçeriği ölçen motorun üst stratejisi hâlâ kaynak dosya boyutuna bağlı

**Kod kanıtı:** `CompressionStrategy.RegimeFor`, kaynak/hedef boyut oranından rejim seçiyor. Bu rejim `AutoPreference`, ceza ağırlıkları, tabanlar ve ses bütçesi payını etkiliyor. `BuildDetailedCore` bu kararı ölçülen içerik maliyetinden önce alıyor.

**Etkisi:** Aynı içeriğin farklı kaynak kodlamaları, benzer ölçülen karmaşıklığa rağmen farklı strateji bölgelerine düşebilir. Kaynağın önceki kodlayıcısının verimsizliği yeni planın ne kadar agresif olacağını etkileyebilir. Bu, “kaynak bit hızına güvenmek yerine içeriği ölçmek” amacında kalan önemli bir bağımlılık.

**Öneri:** Rejimi ölçülen referans ihtiyacı ile kullanılabilir video bütçesinin oranına dayandıran bir aday politika deneyin. Kaynak boyut oranı kullanıcı açıklaması ve ölçüm başarısızlığındaki yedek yol olarak kalabilir. Mevcut eşikleri ölçmeden topluca değiştirmeyin.

**Kabul:** Aynı görsel içeriğin farklı kaynak kodlamalarında karar tutarlılığını değerlendirin; gerçekten farklı kaynak kalitesini de aynı içerik sanmayın.

### 4. Sahne bilgisi her ölçüm aşamasında aynı şekilde kullanılmıyor

**Kod kanıtı:** `ComplexityProbe.ProductionPlan` sabit pencere planını seçiyor; hareket ölçümü ortadaki örnekten türetiliyor. Buna karşılık `CalibrationProbe.Windows`, gelen sahne haritasını kullanabiliyor. Ana pencere sahne haritasını ilk karmaşıklık örneklerinin yerleşimine aktarmıyor.

**Etkisi:** Sahne uyarlaması mevcut; eksik olan bunun ilk içerik modeline tutarlı biçimde taşınması. Kısa ama zor bir hareketli bölüm, sabit örneklerin dışında kalabilir. Tek hareket örneğinin bütün videoya genellenmesi özellikle karışık içerikte sınanmalı.

**Öneri:** Üretilmiş sahne haritasını ilk örnekleme için de kullanmayı deneyin. Temsilî pencerelere ek olarak zor sahneyi kapsayan örnek ve örnekler arası uyuşmazlığa göre ek ölçüm düşünülebilir. Ölçüm bütçesine üst sınır koyun; kısa videoda hazırlığın kodlamadan pahalı olmasını önleyin.

**Kabul:** Sabit ve uyarlanabilir örneklemeyi aynı medya kümesinde karşılaştırın. Boyut tahmin hatası, seçilen plan, kötü sahne kalitesi ve hazırlık süresi birlikte incelensin.

### 5. FFprobe süreç yaşam döngüsü ortak çalıştırıcı kadar güvenli değil

**Kod kanıtı:** `FfprobeClient.ProbeAsync`, stdout ve stderr'i sırayla okuyor. İptalde süreç ağacını öldüren kayıt veya kendi zaman sınırı yok. `FfmpegRunner.RunAsync` ise iki akışı eşzamanlı tüketiyor ve iptal kaydıyla süreci sonlandırıyor.

**Etkisi:** Bekleyen veya hata akışını dolduran bir süreçte takılma riski var; beklemeyi iptal etmek çocuk sürecin bittiğini garanti etmiyor. Burada canlı bir takılma yeniden üretilmedi; fark doğrudan koddan görülüyor.

**Öneri:** Araç yolunu parametre alan ortak süreç çalıştırma katmanı kullanın. Akış tüketimi, iptal, zaman aşımı ve süreç ağacı temizliğini tek yerde yönetin. Sahte ffprobe ile çok stderr üreten ve iptale kadar bekleyen senaryoları sınayın.

### 6. Sert tavan güvencesi pass-through kolunda yeniden denetlenmiyor

**Kod kanıtı:** `EncodeRunner.RunAsync`, `PassThrough` modunda hedef hesabından önce `PassThrough` metoduna dönüyor. Bu metot dosyayı doğrudan kopyalıyor ve başarı bildiriyor; hedef boyutu almıyor. Planlayıcının `CanPassThrough` kontrolü normal kullanım için ilk koruma sağlıyor.

**Etkisi:** Plan sonrası değişen kaynak veya dışarıdan verilmiş tutarsız plan için son teslim katmanı kendi değişmezini korumuyor. Doğrudan kopyalama da normal kodlamanın geçici dosya üzerinden teslim korumasına sahip değil.

**Öneri:** Kopyalama yoluna da etkin hedefi taşıyın, geçici dosyaya kopyalayıp gerçek boyutu kontrol ederek teslim edin. Kaynak boyutu plan sonrası değişen ve önceden hedef dosyası bulunan senaryoları sınayın. Ürünün en güçlü vaadi çağıranın doğru plan vermesine bağlı kalmasın.

## Yapısal geliştirmeler

**Ölçüm akışını pencereden çıkarın.** `MainWindow` bugün ölçüm, kalite çıpası, kalibrasyon döngüsü ve plan yenilemeyi yönetiyor. Bunları bir uygulama servisine taşımak ana pencere, hızlı küçültme ve ölçüm aracının aynı politikayı kullanmasını kolaylaştırır. Önce davranışı koruyan bir çıkarma yapın; aynı değişiklikte algoritmayı da yenilemeyin.

**Sessiz yedek yolları tanılanabilir yapın.** `ComplexityProbe` ve `CalibrationProbe` genel hatalarda güvenli varsayımlara dönüyor, fakat hata nedenini sonuçta taşımıyor. Kullanıcının ilerleyebilmesi iyi; “zaman aşımı”, “ölçüm aracı yok”, “örnek başarısız” gibi nedenlerin yapılandırılmış sonuçta bulunması destek ve ölçüm yorumunu iyileştirir. İptali hata sınıfına karıştırmayın.

**Tahmin bantlarını ampirik olarak değerlendirin.** `ComplexityProfile.EstimateBandFor` bilgi durumuna göre sabit bant seçiyor. Bunu istatistiksel güven aralığı olarak sunmadan önce, bantların gerçek sonuçları ne sıklıkla kapsadığını bağımsız içeriklerde ölçün. Kalibrasyon sırasında paralel işlerden hesaplanan hızın tek uzun kodlamayı ne kadar temsil ettiği de ayrı doğrulansın.

**Teste “neyin çalıştığını” da yazdırın.** Birim testleri, gerçek FFmpeg testleri ve donanım gerektiren kontrollerin kapsamı raporda ayrı görünmeli. Yeşil test sonucu bütün GPU'ların veya gerçek videoların sınandığı anlamına gelmez. Algoritma değişikliklerinde kaynak metindeki sabitleri doğrulamakla yetinmeyip plan davranışını ve gerçek çıktıyı ölçün.

**Dokümantasyonu bugünkü koda hizalayın.** README'nin algısal ölçümü yalnız ölçüm düzeneğine ait göstermesi güncel üretim akışıyla çelişiyor. `docs/tasks/yol-haritasi.md` de uygulanmış bazı işleri eski durumlarıyla anlatıyor. Geçmiş raporlar tarihsel kayıt olarak kalmalı; güncel motor özeti ve açık işler için tek bir giriş belgesi olmalı. Yorumlardaki görev tarihçesi gerektiğinde raporlara taşınabilir; kodda güncel gerekçe ve değişmez daha kolay bulunmalı.

## Önerdiğim ilerleme sırası

Önce kalibrasyon anahtarı, FFprobe yaşam döngüsü ve pass-through teslim kontrolü ele alınmalı. Bunlar kalite politikasını yeniden tasarlamadan güvenilirliği güçlendirir. Ardından ölçüm orkestrasyonu ortak servise çıkarılıp yedek yol nedenleri görünür yapılmalı.

Algoritma tarafında sonraki adım, kaynak boyutuna bağlı rejim ile sabit örneklemenin etkisini ayrı deneylerde ölçmek olmalı. Kalite eğrisi ve kötü sahne odaklı seçim daha sonra, hazırlık süresi bütçesiyle birlikte değerlendirilmeli. Aynı deneyde hem rejim hem örnekleme hem kalite cezası değiştirilirse kazancın nedeni anlaşılamaz.

Benim ana eleştirim özellik eksikliği değil: motor çok sayıda ölçüm ve koruma biriktirmiş, fakat ölçümün hangi koşullarda geçerli olduğu ve üretim akışının hangi kısmında kullanıldığı her yerde aynı açıklıkta değil. En yüksek getiriyi yeni ayarlardan önce bu sözleşmeleri netleştirmekte görüyorum.

## Doğrulama

Bu çalışmada yalnız bu rapor eklendi; motor davranışı değiştirilmedi. İlk `rtk dotnet test VidShrink.sln` çağrısında iç test çıktısı başarılı görünürken RTK başarısız çıkış verdi. Bu tutarsız sonuç tam doğrulama kabul edilmedi. Ardından `rtk proxy dotnet test VidShrink.sln --no-restore --logger "console;verbosity=minimal" --results-directory .calisma/astra-test` çalıştırıldı.

Filtrelenmemiş koşuda şu mevcut hatalar görüldü:

- `QualityHintTests.EverySevenChipsCarryATooltipPanelAndAreListedInTheCode`: test XAML aramasını `ChipWhatsApp` üzerinden başlatıyor, oysa `Chip8` ondan önce geliyor; böylece mevcut düğmeyi inceleme aralığının dışında bırakıyor. Bu bulgu düğmenin üründe eksik olduğu anlamına gelmiyor.
- `TipOverflowTests.NoTipLineOverflowsTheBalloonByASingleWord`: bazı Türkçe ve İngilizce ipucu satırlarının taşma denetimi başarısız.

Koşuda atlanan canlı/ortam bağımlı testler de vardı. Hata bildirimlerinden sonra uzun süre yeni sonuç üretmeyen koşu durduruldu; bütün testlerin tamamlandığı veya başka hata olmadığı iddia edilmiyor. Projenin “tüm testler yeşil” teslim koşulu bu incelemede sağlanamadı. Bu rapor, doğrulanmış bir motor değişikliği veya sürüm teslimi değildir. Yeni Bench koşusu yapılmadığından performans veya kalite kazancı iddiası yoktur.
