# UI Finish-Gate — arayüz eleştirisi

- tarih: 2026-09-06
- danışılan: `agency/design-ui-finish-gate-reviewer` (msitarzewski/agency-agents, teknesyum-core 0.16.4 aynası)
- model: opus
- maliyet: 63.495 token, 205 sn, 1 araç çağrısı
- soran: T0

## Girdi (ajana gönderilen tam metin)

Rolünü şu komutla yükle ve **aynen benimse**:

```
node C:/Users/Administrator/.claude/plugins/cache/teknesyum/teknesyum-core/0.16.4/scripts/agency.js show design-ui-finish-gate-reviewer --lean
```

Sen o koltuksun. Cevabını **Türkçe** yaz.

Proje dosyalarını okuma, kod arama yapma. Aşağıdaki olgular sana hazır veriliyor; görüşünü bunların üstüne kur.

---

## Ürün

**VidShrink** — videoyu **hedef bir dosya boyutuna** sıkıştıran masaüstü aracı. .NET 8 + Avalonia, Windows/macOS/Linux. Motor ffmpeg.

Kullanıcı: videosu bir yere sığmayan kişi. WhatsApp 16 MB, Discord 25 MB, e-posta eki 10 MB gibi. En sık akış: dosyayı sürükle → hedef boyutu seç → Başlat → çıkan dosyayı paylaş.

Ürünün açık hedefi: piyasadaki en iyi sıkıştırma aracı olmak. Yani asıl değer motorda, arayüzün işi o değeri **görünür ve hızlı** kılmak.

## Ölçülmüş olgular (arayüzden sayılarak çıkarıldı)

Ana pencere sekmeli: Küçült · Dönüştür · Karşılaştırma/Önizleme · Ayarlar · Oynatıcı.

- Ana `MainWindow.axaml` içinde **23 adet ComboBox** var.
- Bunların **beşi üç ya da daha az seçenekli**:
  - `CmbIntent` — 3 seçenek: Arşiv / Paylaşım / Sosyal medya
  - `CmbCodec` — 3 seçenek: Otomatik / Uyumlu / En küçük
  - `CmbFillPolicy` — **2 seçenek**: hedefi doldur / tavan olarak kullan
  - `CmbHdrPolicy` — **2 seçenek**: HDR'ı koru / SDR'a indir
  - `CmbQualityMode` — **2 seçenek**
- Toplam `ToggleSwitch` + `CheckBox` sayısı: **5**. Yani ikili seçimlerin çoğu açılır listede.
- Bu ayarların her biri **üç dikey satır** yiyor: (1) etiket + yanında `?` bilgi düğmesi, (2) pencere genişliğine yayılmış `HorizontalAlignment="Stretch"` ComboBox, (3) altında ipucu/durum metni.
- "Amaç" (`CmbIntent`) üç seçenekli bir tercih ve tam genişlikte kendi paneline sahip.
- Hedef boyut için ayrı olarak **7 adet yonga düğmesi** var (WrapPanel: WhatsApp, 8, 25, 100, yarıya indir vb.).
- Gelişmiş ayarlar bölümü (`AdvancedBody`) var ve çalışıyor, ama **tek uzun kaydırmanın en altında** duruyor; içinde beş ayar daha, hepsi aynı üç satırlık kalıpta.
- Karşılaştırma/önizleme alanı, kullanıcının kendi ifadesiyle "hâlâ çok büyük".

## Sahibinin şikâyeti (kendi cümleleri)

"Ayar kısmında çok daha küçük alana çok daha verimli ayarlar koyabilecekken kocaman bir amaç paneli koymuşuz, kocaman bir boşluk ve içinde paylaşım yazıyor. Veya bazı 2-3 seçenekli basit şeyler için toggle kullanmak çok daha hoş duracakken scroll list kullanıyoruz."

Ayrıca: Oynatıcı sekmesi en sola alınacak.

## Tasarlanacak yeni akış

Sıkıştırma bitti ve çıkan dosya hedefi **az** aştı (≤ %3). Bu sessizce kabul edilmeyecek; kullanıcıya dört seçenek sunulacak:

1. Yeniden koş (daha sıkı ayarla)
2. İptal et
3. Büyük hâlini kabul et
4. Videodan **~%3 kes** — sondan, baştan ya da her ikisinden

Bu an, kullanıcının beklediği iş bittikten hemen sonra geliyor ve kullanıcı sonucu almak istiyor. 4. seçenek üç alt seçenek taşıyor, yani karar ağacı iki katmanlı.

## Kısıt

Renk ve ölçü belirteçlerimizin bulunduğu `teknesyum-ui` deposu **kurulu değil.** Bu yüzden **hex renk, piksel değeri ya da font boyutu verme** — verirsen kullanamayız. Yapı, yoğunluk, hiyerarşi, bileşen seçimi ve durum tasarımı üzerinden konuş.

Avalonia'da mevcut: `ToggleSwitch`, `RadioButton`, `TabControl`, `Expander`, `SegmentedControl` yok ama `RadioButton`'lardan yatay şerit yapılabilir, `Flyout`, `ContentDialog` benzeri gömülü panel.

---

## Sorular

1. **23 ComboBox'lık bir yüzeyde** hangi ayar hangi bileşene gitmeli? İkili seçim, üçlü seçim, sayısal aralık, çok seçenekli liste — her sınıf için bir kural ver ve neden o kural olduğunu ürünün işine bağla.

2. **"Amaç" (Arşiv/Paylaşım/Sosyal)** üç seçenekli. Bu ayrı bir kontrol olarak durmalı mı, yoksa zaten var olan 7 hedef-boyut yongasıyla birleşmeli mi? İkisi de "ne için sıkıştırıyorsun" sorusunun cevabı gibi duruyor — çakışıyorlar mı?

3. **Küçült sekmesi tek uzun kaydırma.** Bölümlere ayrılacak. Hangi bölümler, hangi sırayla, hangisi varsayılan açık? Kriterin ne — kullanım sıklığı mı, karar sırası mı, başka bir şey mi?

4. **Taşma teklifi ekranı**: dört seçenek, biri üç alt seçenekli. Bunu nasıl sunarsın? Kesme seçeneğinin kullanıcıya ne kaybettireceğini (kaç saniye, hangi kısım) nasıl görünür kılarsın? Bu bir modal mı, gömülü panel mi, başka bir şey mi?

5. **Bizim ürünümüze özgü ne var** ki genel bir "ayarlar formu"ndan ayrılalım? Senin işin tam olarak jenerik arayüzü yakalamak — bizimki jenerik mi, nerede?

Her bulguyu **gözlenebilir bir değişikliğe** ve bir **doğrulama koşuluna** bağla. "Temiz olur", "modern durur" gibi cümleler kullanma. Sonunda **PASS/HOLD** yerine, sıraya konmuş bir iş listesi ver: her madde ne değişecek, nasıl doğrulanacak.

---

## Dönen (ajanın tam cevabı)

`node .../agency.js show design-ui-finish-gate-reviewer --lean` yüklendi; aşağıdaki inceleme o koltuktan.

---

# Ürün merceği

Kullanıcı bir dosyayı **bir yere sığdırmaya** çalışıyor. Ekrandaki asıl nesne video değil, **bayt bütçesi**: hedef MB, tahmini çıktı MB ve aradaki fark. Birincil eylem tek: "Başlat". Diğer 23 kontrolün tamamının anlamı, o bütçeyi kaç MB oynattığıdır. Şu an ekranda bütçe biçiminde hiçbir şey yok — etiket/kontrol/ipucu üçlüsünden oluşan jenerik bir kodlayıcı formu var. Handbrake, Shutter Encoder, XMedia Recode ile birebir değiştirilebilir bir yüzey. Ayırt edici olan tek şey (MB'ı sen seç, gerisini motor türetsin) ekranda görünmüyor.

---

## 1. Bileşen kuralları (sınıf → bileşen → gerekçe)

Kural, seçenek sayısına değil **sonucun adı var mı** sorusuna bağlı:

**İkili, biri varsayılan, diğeri sapma** → `ToggleSwitch`, etiket açık hâli olumlu cümleyle söyler. Örnek: `CmbFillPolicy` → "Hedef boyutu doldur" anahtarı. Gerekçe: sapma nadirdir, kapalı hâlin adının okunmasına gerek yoktur.

**İkili, iki kol da alan sonucu** → iki bölmeli yatay `RadioButton` şeridi. Örnek: `CmbHdrPolicy`. "HDR'ı koru" / "SDR'a indir" ikisi de kullanıcının bilmesi gereken sonuç; toggle biri "kapalı" diye adını gizler ve renk bozulması sessiz kalır.

**Üçlü** → tek satır yatay şerit, üç seçenek de aynı anda okunur. Örnek: `CmbCodec` (Otomatik / Uyumlu / En küçük). Gerekçe: üç, tıklamadan okunabilecek üst sınır. Açılır liste üçte ikisini gizler ve kullanıcı "En küçük" diye bir seçeneğin varlığını hiç öğrenmez.

**Dört–altı, kapalı küme** → tek satır ComboBox, etiket solda, kontrol **içeriğine göre** genişlikte. `HorizontalAlignment="Stretch"` kalkar: pencere genişledikçe büyüyen bir açılır liste, önemsiz bir ayarı ekranın en geniş nesnesi yapıyor.

**Yedi ve üstü / açık uçlu (kodlayıcı, kap, çözünürlük, preset)** → ComboBox kalır, ama ilk madde "Otomatik — şu an: H.264" biçiminde çözülen değeri gösterir. Gerekçe: kullanıcıların çoğu bu listeyi hiç açmayacak; açmayanın da ne olduğunu bilmesi gerekir.

**Sayısal aralık (CRF, bitrate, çözünürlük ölçeği)** → kaydırıcı + sayı kutusu, ve **yanında MB cinsinden etkisi**. Asla sihirli sayılardan oluşan bir açılır liste değil. Gerekçe: ürünün birimi MB; CRF 23'ün ne demek olduğunu bilen kullanıcı zaten ffmpeg kullanıyor.

**Üçüncü satır kuralı:** kontrolün altındaki sabit ipucu metni kaldırılır. Yerine panel başına **tek canlı sonuç satırı** gelir. 23 statik ipucu satırı sahte yoğunluktur — hiç değişmezler, dolayısıyla hiç okunmazlar.

**`?` düğmesi kuralı:** her etiketin yanında ayrı bir bilgi düğmesi kalmaz; açıklama etiketin kendisine bağlı `Flyout` olur ve yalnız Gelişmiş bölümündeki ayarlarda kalır.

Doğrulama: `MainWindow.axaml` kaynağını okuyan bir test, seçenek sayısı ≤ 3 olan `ComboBox` sayısının **0** olduğunu ve `HorizontalAlignment="Stretch"` taşıyan `ComboBox` sayısının **0** olduğunu iddia eder. Sayıyı gözle değil testle pinle — bu taramanın elle yapıldığında eksik saydığı zaten kayıtlı.

Ölçü etkisi: bugün yalnız açılır listeler için ~23 × 3 = **69 dikey satır**. Kural uygulandığında ~23 satır + birkaç şerit; kabaca üçte bire iner.

---

## 2. "Amaç" ile 7 hedef yongası çakışıyor

Evet, çakışıyorlar. "WhatsApp" yongasını seçen kullanıcı hem boyutu (16 MB) hem amacı (paylaşım) hem de uyumluluk beklentisini (telefonda açılsın → uyumlu kodek) söylemiştir. Ardından ona ayrıca "Amaç: Paylaşım" seçtirmek, aynı soruyu ikinci kez sormaktır; dahası ikisi çelişebilir (WhatsApp + Arşiv) ve o çelişkinin ne yaptığı ekranda hiçbir yerde yazmıyor.

Değişiklik: `CmbIntent` **kontrol olarak kaldırılır**. Yonga şeridi "Nereye gidiyor?" olur ve amacı da taşır: WhatsApp · Discord · E-posta · 100 MB · Yarıya indir · **Arşiv** (boyut hedefi yok, kalite hedefi var) · Özel… Arşiv aynı şeritte durur çünkü o da bir hedeftir, sadece kısıtı boyut değil kalite. Seçim, altındaki tek satırlık türetme cümlesiyle görünür kalır: "16 MB · uyumlu kodek · hedefi doldur". O cümledeki her parça tıklanınca ilgili kontrole götürür.

Doğrulama: `CmbIntent` axaml'den kalkar; her yonga için "yonga → plan" eşlemesini iddia eden Core testi; ekran görüntüsünde yonga seçimi sonrası türetme cümlesinin üç alanı da içerdiği görülür.

---

## 3. Küçült sekmesinin bölümleri

Kriter **karar sırası**, kullanım sıklığı değil. Kullanıcı bu ekrandan dosya başına bir kez, yukarıdan aşağı geçiyor; sıklık istatistiği burada yanlış eksen. İkinci kriter: nadir ama riskli olan gizlenir, fakat **özeti başlıkta durur**.

1. **Kaynak ve hedef** — başlıksız, ekranın kendisi. Bırakma alanı + yonga şeridi. Her zaman açık.
2. **Ne çıkacak** — salt okunur bütçe satırı: hedef MB, tahmini MB, fark, süre, çözünürlük, kodek. İlk okunan nesne budur. Her zaman görünür, katlanmaz.
3. **Kalite ve uyumluluk** — kodek politikası, kalite modu, doldurma politikası, (kaynak HDR ise) HDR. Varsayılan **kapalı**, başlıkta güncel değerlerin özeti; bir uyarı varsa kendiliğinden açılır.
4. **Ses** — kapalı, başlıkta özet. Kaynakta ses yoksa bölüm hiç render edilmez.
5. **Kırpma ve çözünürlük** — kapalı, başlıkta özet.
6. **Gelişmiş** — kapalı, ama artık uzun kaydırmanın dibinde değil, 3-5 ile **aynı seviyede** bir `Expander`. Bugünkü hâli keşfedilemez: çalışan bir özellik, ulaşılamadığı için yok sayılıyor.

Katlanmış her başlık kendi değerlerini yazar; katlamak durumu gizlemez.

Doğrulama (sert koşul): varsayılan ayarlarla, bir dosya bırakıldıktan sonra Küçült sekmesi belirtilen asgari pencere yüksekliğinde **kaydırma çubuğu olmadan** sığar — `ScrollViewer.Extent ≤ Viewport` iddiası. İkinci doğrulama: ses bit hızını değiştir, bölümü katla, başlık metninin yeni değeri içerdiğini iddia et.

---

## 4. Taşma teklifi

**Modal değil.** Dosya zaten üretildi; modal, kullanıcının elindeki sonucu bir kaza tıklamasıyla kaybedebileceği bir kapı ve odağı çalar. Doğru yer: aynı sekmedeki **koşum panelinin kendisi**. İlerleme çubuğunun bulunduğu alan yerinde karar paneline dönüşür. Başlık, iddiayı değil olguyu söyler: "Dosya hazır — 25,6 MB, hedefin %2,4 üstünde."

**Dört eşit kart yapma.** Dört eşit ağırlıklı seçenek, üründen bağımsız jenerik bir karar ızgarasıdır ve hepsinin eşit derecede makul olduğunu ima eder. Bir tanesi önerilen birincil, üçü ikincil olur; hangisinin birincil olacağı **veriden** gelir: az önceki koşumun ölçülen süresi kısaysa "Yeniden koş", uzunsa "Kes".

**Her seçenek fiyatını taşır** — ürüne özgü olan kısım bu:

- Yeniden koş — "≈2 dk (önceki koşum ölçüldü), kalite bir kademe düşer"
- Kabul et — "25,6 MB · WhatsApp 16 MB sınırını aşar" (hedefin adıyla, soyut "büyük" değil)
- ~%3 kes — "4,2 sn kısalır: 2:21 → 2:17"
- İptal — "üretilen dosya silinir"

**Alt seçenek için ikinci pencere açma.** "Kes" seçilince satır yerinde açılır: üç bölmeli şerit (Sondan · Baştan · İkisinden), varsayılan **Sondan**, ve altında hangi saniyelerin gittiğini yazan canlı satır: "2:16,8 – 2:21,0 atılıyor". Ürünün zaten önizleme borusu var; kesim noktasındaki kareyi burada göstermek, "ne kaybediyorum" sorusunu tahminden çıkarıp gösterime çeviren tek hamledir. Onay ayrı düğme: "Kes ve kaydet". Kesim sonrası oturum boyunca "kesmeden önceki dosyayı geri al" durur.

**Panel her taşmada çıkmaz.** Sert tavanı olan hedeflerde (WhatsApp, Discord, e-posta) çıkar; "Arşiv" ya da "yarıya indir" gibi tavanı olmayan hedeflerde %3 taşma anlamsızdır, sessizce kabul edilir.

Doğrulama: taşmayı zorlayan bir fikstürle panelin dört seçeneğinin **her birinin bir sayı içerdiği** ekran görüntüsüyle kanıtlanır; "Kes"e tıklamak yeni pencere açmaz (pencere sayısı sabit kalır); kesilecek saniye = süre × taşma oranı iddiası birim testinde; Arşiv hedefinde aynı taşmada panelin hiç görünmediği iddiası.

---

## 5. Nerede jeneriğiz

Şu an ayar yüzeyinin tamamı jenerik. Somut kanıt: etiket + `?` + tam genişlik açılır liste + statik ipucu kalıbı 23 kez tekrarlanıyor; bu kalıp herhangi bir kodlayıcı arayüzüne kopyalanabilir ve hiçbir şey değişmez. Ürünün asıl nesnesi — **bayt bütçesi** — ekranda hiçbir biçim almamış.

Jenerik olmaktan çıkaran üç şey, üçü de uygulanabilir:

**Bütçe defteri.** Hedef, tahmin ve fark ilk görüntüde sabit durur ve her kontrol değişiminde güncellenir. Doğrulama: herhangi bir kontrolü değiştir, satırın metninin değiştiğini iddia et.

**Her kontrolün ikincil metni tanım değil delta.** "Ses bit hızı" altında "AAC kodlayıcı bit hızı" değil, "−1,4 MB". Doğrulama: ses bit hızını 128'den 96'ya al, ikincil metnin MB farkını gösterdiğini iddia et.

**Tahminin güveni.** Bu bir tahmin; aralık olarak sunulur ve koşumdan sonra "tahmin 24,1 MB · gerçek 25,6 MB" biçiminde kapanır. Ürünün iddiası "en iyi sıkıştırma" ise, tahmin doğruluğunu göstermek o iddianın ekrandaki tek kanıtıdır ve hiçbir jenerik form bunu yapmaz.

Karşılaştırma/önizleme alanı da bu yüzden büyük: şu an genel amaçlı bir oynatıcı gibi duruyor. Tek bir soruya hizmet etmeli — "kalite hayatta kaldı mı?" — yani seçilen bir zaman damgasında eşleştirilmiş A/B kare, kompakt. Büyük oynatma zaten Oynatıcı sekmesinin işi.

**Yasaklı varsayılanlar (bu ürün için):** eşit ağırlıklı kart ızgarası; hiç değişmeyen açıklama satırı; pencereye yayılan açılır liste; kontrol başına ayrı bilgi düğmesi; her ayarın kendi başlıklı panelini alması; sonucu bildiren dekoratif modal.

**Risk — Oynatıcı sekmesinin en sola alınması:** en soldaki sekme ürünün ne olduğunu ilan eder. Oynatıcı sola alınırsa uygulama kendini oynatıcı olarak tanıtır. Taşınacaksa, açılışta **seçili** sekme Küçült kalmalı. Doğrulama koşulu: uygulama boş açıldığında ilk görüntüde bırakma alanı ve hedef boyut görünür, transport çubuğu değil.

---

# Sıraya konmuş iş listesi

1. **Amaç kontrolünü kaldır, yonga şeridiyle birleştir.** `CmbIntent` axaml'den çıkar; yongalar boyut + uyumluluk + doldurma politikasını birlikte kurar, altında tek satır türetme cümlesi. Doğrulama: yonga→plan eşleme testi; `CmbIntent` referansı 0.
2. **Üç satırlık kalıbı tek satıra indir.** Etiket solda, kontrol içeriğine göre genişlikte, statik ipucu satırları silinir. Doğrulama: `Stretch` taşıyan ComboBox sayısı 0; kaynak okuyan test.
3. **≤3 seçenekli beş açılır listeyi dönüştür.** Doldurma politikası ve kalite modu → toggle/şerit (varsayılan var mı kuralına göre), HDR ve kodek → şerit. Doğrulama: seçenek sayısı ≤3 olan ComboBox sayısı 0 iddiası testte.
4. **Bütçe defterini ekle.** Hedef / tahmin / fark satırı, her değişimde güncellenir; kontrol altı metinler MB deltasına döner. Doğrulama: kontrol değişimi → metin değişimi testi.
5. **Küçült sekmesini altı bölüme ayır, Gelişmiş'i eş seviyeye çıkar.** Katlı başlıklar güncel değerleri yazar; kaynakta olmayan alanlar (ses yok, HDR değil) hiç render edilmez. Doğrulama: varsayılan durumda asgari yükseklikte kaydırma yok (`Extent ≤ Viewport`); katlı başlık özeti testi.
6. **Taşma panelini gömülü olarak kur.** Koşum paneli yerinde karar paneline döner; dört seçenek, her biri sayı taşır; birincil seçim ölçülen koşum süresinden türetilir. Doğrulama: fikstürle ekran görüntüsü; pencere sayısı sabit.
7. **Kesme alt seçimini satır içinde aç.** Üç bölmeli şerit, varsayılan sondan, atılan aralık metni + kesim karesi, ayrı onay, oturum boyu geri al. Doğrulama: kesilecek saniye = süre × oran birim testi.
8. **Taşma panelini yalnız sert tavanlı hedeflerde göster.** Doğrulama: Arşiv hedefinde aynı taşmada panel görünmez iddiası.
9. **Karşılaştırma alanını A/B kareye indir.** Büyük oynatma Oynatıcı sekmesinde kalır. Doğrulama: iki pencere genişliğinde ekran görüntüsü; alanın yüksekliği bütçe defterini ilk görüntüden düşürmüyor.
10. **Sekme sırası değişirse açılış sekmesini pinle.** Doğrulama: boş açılışta ilk görüntüde bırakma alanı ve hedef boyut var.

---

# Senden istediklerim

1. **Oynatıcı sekmesi kararı.** "En sola alınsın, ama açılışta seçili sekme `Küçült` kalsın" mı, yoksa "en sola alınsın ve açılışta o seçili olsun" mu? İkincisini seçersen 10. madde düşer, ürünün ilk okunan kimliği oynatıcı olur.
2. **`CmbQualityMode`'un iki seçeneğinin adları.** Adları bana verilmedi; kural (varsayılanı olan ikili → toggle, iki kolu da adlandırılmış ikili → şerit) hangisine düşeceğini bu adlar belirliyor. Tam metni yaz: örneğin `Boyut öncelikli` / `Kalite öncelikli`.
3. **Asgari pencere yüksekliği.** 5. maddedeki "kaydırmasız sığar" testinin pinleneceği sayıyı sen ver — ölçü belirteci olmadan uydurmam. Tek satır yeter: `MinHeight = <değer>`.
