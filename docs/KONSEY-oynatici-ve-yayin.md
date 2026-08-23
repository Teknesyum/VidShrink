# Konsey: Oynatıcı ve yayın özellikleri

**Tarih:** 24.08.2026
**Üyeler:** fable ve opus, aynı brifing, birbirlerini görmeden
**Karar veren:** T0

Kullanıcı iki özellik sordu ve ikisi için de konsey istedi:

- **A)** Kaynak ve çıktı videonun program içinde oynatılması; sıkıştırma sürerken
  videonun hangi kısmında işlem yapıldığının bu oynatıcılarda görünmesi
- **B)** Tek tıkla yükleme bağlantısı; karşı tarafın o adresle izlemesi; iki tarafın da
  yayını kapatabilmesi

---

## Ortak çıkan kararlar — doğrulanmış sayılıyor

İki üye birbirinden bağımsız aynı sonuca vardı. Konsey kuralı gereği bunlar tartışılmaz.

### 1. Gerçek bir oynatıcı çekirdeği gömülmeyecek

İkisi de LibVLCSharp, libmpv ve FFmpeg.AutoGen yollarını reddetti.

Gerekçeler örtüşüyor: süreç içi kod çözme, bozuk bir dosyada ffmpeg çökmesini **uygulama
çökmesine** çevirir. Bu zaten `docs/taramalar/RAPOR.md:81` içinde bir kez elenmiş bir
karar.

Opus ayrıca somut bir engel buldu: resmî `LibVLCSharp.Avalonia` içinde `VideoView`
yalnız `Window` içine konabiliyor, `UserControl` içinde çalışmıyor. Bizim dört sekmeli
düzenimize doğrudan uymuyor. Bunu aşan sürüm resmî değil, tek bakımcılı bir çatal — tek
bakımcılı bir projeye ikinci tek bakımcılı bağımlılık eklemek tedarik zinciri riskini
ikiye katlar.

Boyut tarafı: LibVLC Windows'ta 80-100 MB, libmpv 60-70 MB ekler; Linux'ta ikisi de
sistem paketine muhtaç, yani **self-contained sözü kırılır.**

### 2. Bunun yerine: kare şeridi + sistem oynatıcısı

İkisi de aynı ucuz yolu önerdi.

- Kaynak videodan tek ffmpeg geçişiyle küçük kare şeridi çıkarılır
- Kaynak ve çıktı panellerine "sistem oynatıcısında aç" düğmesi konur
- Kuruluma **0 MB** ekler, üç platformda çalışır, yeni bağımlılık yok

`Platform.cs` üç yollu açma işini zaten yapıyor (T13 K3).

### 3. "İşlem videonun neresinde" göstergesi gerçek ve verisi zaten elde

İkisi de kodu okudu ve aynı şeyi buldu: `EncodeRunner.cs` zaten `-progress pipe:1` ile
`out_time_ms` okuyor. Kaynak zaman ekseniyle eşleşme birebir.

**Ama etiketsiz gösterilirse hata gibi görünür.** İki geçişli kodlamada zaman çizgisi
iki kez taranır — birinci geçiş analizdir, çubuk sıfırlanıp yeniden ilerler. Üstüne
düzeltme turları var. Yani çubuk N×2 kez baştan sona gider.

Gösterge "analiz 1/2", "kodlama 2/2", "deneme 2" diye etiketlenmek zorunda.

Opus bir ayrıntı daha yakaladı: Convert yolunda `ConversionArguments.cs` `-ss` yazıyor,
orada zaman ofseti uygulanması gerekiyor.

### 4. Kodlama sürerken çıktı oynatılamaz

İkisi de aynı teknik sınırı gösterdi: çıktı `.partial` ve MP4'te `moov` atomu sona
yazılıyor. Canlı önizleme ancak parçalı MP4'e (`-movflags frag_keyframe+empty_moov`)
geçmekle olur.

**İkisi de bunu reddetti:** parçalı konteyner dosya boyutu hesabını değiştirir ve
ürünün tek sert sözüyle — hedeften büyük dosya asla verilmez — doğrudan çakışır.

### 5. Barındırılan yayın hizmeti yapılmayacak

İkisi de B'yi bugünkü hâliyle reddetti ve gerekçeleri aynı:

- Süregelen barındırma ve bant genişliği faturası
- Telif ve yasadışı içerik bildirimlerine cevap verecek bir muhatap gerekliliği
- Kötüye kullanım kuyruğu ve moderasyon
- Yayınlanmamış, kullanıcısız, tek bakımcılı bir projede bu **ürün değil yükümlülük**

**AGPL tarafı:** bugün masaüstü uygulama için AGPL'nin ağ maddesi fiilen GPL gibi
davranıyor. Bir sunucu bileşeni yazıldığı anda **§13 devreye girer** ve yayını açan
kullanıcı "operatör" olur — uzak kullanıcıya kaynak sunma yükümlülüğü doğar.

İkisi de "eşler arası = sunucusuz" iddiasını çürüttü: WebRTC işaretleşme sunucusu
gerektirir, simetrik NAT'ta TURN aktarımı gerektirir, TURN bant genişliği paradır.
Bu üründe sunucusuz yayın yoktur.

### 6. Sıra: önce A, sonra B

İkisi de aynı gerekçeyle: A'nın ilk dalgaları sıfır bağımlılık, sıfır MB ve sıfır hukuki
yüzey.

---

## Ayrışma — kullanıcının kararı gereken yer

Tek gerçek ayrışma **B'nin akıbetinde.**

**Fable:** B hiç planlanmasın. Tek bir karar notu yazılsın; üç model (kendi sunucun /
üçüncü taraf API / eşler arası) maliyet, sorumluluk ve AGPL yüzüyle karşılaştırılsın ve
karar "yapılmıyor" ya da "şu koşulda yapılır" diye bağlansın.

**Opus:** B'nin savunulabilir bir alt kümesi var — **yalnız yerel ağ.** `HttpListener`,
tek seferlik rastgele yol belirteci, Range istekleriyle ilerleyerek izleme, açıkta kalma
süresi tavanı, iki tarafın da kapatabilmesi, sunulan sayfaya AGPLv3 §13 kaynak
bağlantısının gömülmesi.

Opus bunun sınırlarını da yazdı: ilk açılışta Windows güvenlik duvarı sorar; kafe
wifi'sinde "yerel ağ" yabancılarla paylaşılan ağdır. Bu yüzden belirteç tahmin edilemez
uzunlukta, süre tavanı kısa, ve arayüzde kaç istemcinin bağlı olduğu görünür olmalı.

İkinci tercihi: kullanıcının **kendi deposu** (S3/WebDAV/Nextcloud kimlik bilgisiyle).
Sorumluluğu tamamen kullanıcıya taşır ve teknik olarak dürüsttür.

---

## Opus'un tek başına getirdiği fikir

Konseyin asıl kazancı çoğu zaman burada olur, bu seferki de öyle.

**Kullanıcının gerçek sorusu "video izlemek" değil, "sıkıştırma neyi bozdu".**

Buradan çıkan öneri: **A/B kare karşılaştırıcı.** Kodlama bittikten sonra aynı zaman
damgasında kaynak ve çıktı karesi yan yana veya sürgülü gösterilir.

Bu bir oynatıcı değil, karşılaştırma görünümü. Ve motorun kalite kararlarını gözle
sınamayı sağladığı için T5'in (gerçek dosyayla A/B ve bant doğrulaması) işine doğrudan
yarar.

## Opus'un ölçüm kapısı

Program içi oynatmanın hiç yapılıp yapılmayacağını **ölçüye** bağladı: atılabilir bir
çiviyle ffmpeg borusundan kare çözüp Avalonia'ya çizme hızı ölçülsün, 1080p ve 4K'da
saniyede kaç kare. 1080p'de 24 fps'in altındaysa o yol kapanır.

Karar tartışmayla değil sayıyla verilir. Bu doğru yaklaşım.

---

## T0 notu — konsey dışı, doğrulanmış

Opus üç çelişki bildirdi. Ölçtüm:

1. **`docs/taramalar/RAPOR.md:102` "VidShrink bugün MIT lisanslı" diyor.** Bayat; README
   AGPL-3.0-or-later. RAPOR §5'in MIT temelli uyarıları geçersiz. **Doğru.**
2. **İki ayrı PLAN dosyası var:** `docs/plan.md` ve `.claude/relay/PLAN.md`. **Doğru.**
3. **"T2c ve T3 hâlâ active"** — kısmen yanlış. T2c mühürlü (`done`). Ama T3 denetlenmemiş
   (`submitted`), T4 ve T5 hiç başlamamış. Yani "motor tam mühürlü değil" savı ayakta.
