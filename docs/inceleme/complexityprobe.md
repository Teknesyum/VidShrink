# ComplexityProbe İncelemesi — salt-okuma, `src/VidShrink.Ffmpeg/ComplexityProbe.cs` (576 satır)

## 1. Ne yapıyor

`RunAsync` 2–3 adet 2 saniyelik pencere seçer (`:44-47`), her pencereyi tek ffmpeg çağrısında
`split=2` ile tam ve yarı ölçekte libx264 CRF 23 kodlar (`:381-431`), byte/kare toplar. Buradan
`ComplexityProfile.FromProbe` `ReferenceBppf` ve `DetailExponent = log(half/full)/log(0.5)`
üretir (`:64-71`, `ComplexityProfile.cs:94-113`). Pencerelerin dosyayı temsil edip etmediği ayrıca
ölçülür: önce `ScanBiasAsync` — 40 nokta × 1 sn, 480×270, ultrafast, vstats ile ısınma atlanır
(`:256-290`); güvenilmezse `PacketBiasAsync` — ffprobe paket boyutlarından saniye profili
(`:292-308`). Bias `ReferenceBppf`'i böler; her hata sessizce `FromSourceBitrate`'e düşer (`:77-80`).

## 2. Ölçüm doğruluğu

### K1 — Bias, ölçümden bambaşka bir domainde hesaplanıyor (yüksek)
Ana örnek tam çözünürlük + `medium`/`veryfast` preset (`:42`, `:470`); bias ise 480×270 +
`ultrafast` (`:21-23`, `:264`, `:508`). Downscale yüksek frekanslı detayı yok eder, ultrafast
hareket araması yapmaz. Grain, metin, ekran içeriği ağırlıklı **çok statik ama detaylı** kaynakta
scan'in gördüğü pencere/dosya oranı tam çözünürlüktekiyle aynı değildir; bu oran doğrudan
`ReferenceBppf`'e bölündüğü için (`ComplexityProfile.cs:101`) hata hedef bit hızına geçer.

### K2 — Scan biası minimum örnek sayısı istemiyor, ama en dar bandı alıyor (yüksek)
`ComputeScanBias` başarısız noktaları sessizce atıyor (`:127`) ve tek pencere + tek yayılım
noktası kalsa bile bias döndürüyor (`:140`). Sonuç `WindowBiasSource.Scan` etiketlenip
`EstimateBand`'i 0.05'e çekiyor (`ComplexityProfile.cs:69`, `:76`) — yani en zayıf kanıt en yüksek
güveni veriyor. `ComputeWindowBias`'ta en azından `pool.Count < 4` koruması var (`:195`).

### K3 — Scan noktalarının yalnızca %25'i sayılıyor (yüksek)
`ScanPointSeconds = 1.0`, `ScanWarmupSeconds = 0.75` (`:16-17`, `:521`): her 1 saniyelik kodlamanın
son 0.25 saniyesi ölçülüyor. 30 fps'te nokta başına ~7 kare, 40 noktada ~280 kare. **Sahne değişimi
yoğun** kaynakta bir kesmeyi yakalama olasılığı düşük, dolayısıyla scan biası I-frame patlamalarını
göremez; pencereye kesme düştüyse `ReferenceBppf` yukarı biaslı kalır ve düzeltilmez.

### K4 — Pencere örneğinde I-frame payı gerçek kodlamanın ~5 katı (yüksek)
Pencere 2 saniye ve ısınma atılmıyor (`:369-378`, `:393`), yani 1 IDR / 2 sn. Gerçek kodlamada
GOP tipik 250 kare (~10 sn). **Çok statik** kaynakta bitlerin çoğu I-frame'dedir; oran katlanır ve
`ReferenceBppf` sistematik olarak yüksek çıkar. Scan tarafı ısınma attığı için bu şişme biasa
yansımıyor — iki ölçüm arasındaki asimetri düzeltmeyi de bozuyor.

### K5 — `-ss` girişten önce: nominal pencere ile ölçülen bölge örtüşmüyor (orta)
`-ss` `-i`'den önce veriliyor (`:392-394`, `:454-456`, `:500-502`) → ffmpeg en yakın keyframe'e
atlar. Uzun GOP'lu kaynakta (10 sn keyframe aralığı) gerçekte kodlanan bölge saniyelerce kayabilir.
Buna karşın bias hesabı **nominal** saniyeleri seçili sayıyor (`:183-189`, `:101-103`,
`CoveredSeconds :214-220`), yani "pencere" ile "pencerenin biası" farklı bölgeleri ölçüyor.

### K6 — Yarısı başarısız pencere `DetailExponent`'i bozuyor (orta)
`:56` — `HalfFrames <= 0` olan pencere `full` toplamına girer ama `half` toplamına girmez. Böylece
`fullBppf` ve `halfBppf` farklı pencere kümelerinden hesaplanır (`:64-67`) ve `DetailExponent` iki
farklı içeriğin oranı olur. Split yolu ikisini birlikte verdiği için normalde eşleşir; fallback
yolunda (`:376-377`) yarı ölçek ayrı bir süreçte ve tek başına başarısız olabilir.

### K7 — Split yolu ile fallback yolu farklı büyüklük ölçüyor (orta)
Split gerçek dosya boyutunu okuyor (`:427-428`); fallback `-f null` özetindeki yuvarlanmış
`video: NNkB` satırını parse ediyor (`:486`, `:554-568`). 2 saniyelik küçük örneklerde kB
kuantalaması %1–2 fark yaratır. Ayrıca split iki çıkışa **aynı** `frame=` değerini atıyor (`:424`,
`:431`); `ParseFrames` stderr'deki son eşleşmeyi alıyor (`:570-574`) — ffmpeg sürümü bu alanı
çoklu çıkışta toplam raporlarsa `ReferenceBppf` sessizce yarıya iner. Test yok.

### K8 — Pencere konumları sabit ve son %17 hiç görülmüyor (orta)
`Windows` `usable*(i+0.5)/count` veriyor (`:91-94`): 3 pencerede %16.7 / %50 / %83. Videonun son
altıda biri asla örneklenmiyor; jenerik, outro, kredi gibi bölümler profile girmiyor. Ayrıca
düzenli aralık, periyodik içerikle (tekrarlayan sahne, reklam döngüsü) senkronize olabilir.

### K9 — Değişken kare hızı (düşük)
`bppf` kare başına normalize edildiği için VFR'ye kısmen dayanıklı (`:64`); ama `sampled` nominal
`WindowSeconds` ile artıyor (`:54`) ve pencereler düz toplamla birleşiyor (`:52-58`) — `-t 2.0`
içindeki kare sayısı değişince pencereler eşit ağırlık taşımaz. Aynısı `ComputeScanBias`'ta (`:130-137`).

## 3. Paralellik ve kaynak yönetimi

- **Zaman aşımı yok.** Dört ffmpeg/ffprobe çağrısının hiçbirinde süre sınırı yok (`:328-336`,
  `:411-419`, `:474-482`, `:511-518`). Bozuk veya ağ üzerindeki dosyada süreç asılırsa ölçüm
  süresiz bekler; iptal yalnızca yeni dosya seçilince geliyor (`MainWindow.xaml.cs:200-210`).
- **Dispose edilmiş CTS yarışı.** `MainWindow.xaml.cs:201-203` önceki `_probeCts`'i iptal edip
  hemen `Dispose` ediyor; eski çağrı hâlâ `ct.Register` kullanıyor (`:330`, `:413`, `:476`).
  `ObjectDisposedException` `:77`'deki genel catch'e düşer ve profil sessizce `FromSourceBitrate`'e
  geriler — kullanıcı ölçülmüş sanır. Orta-yüksek.
- **Çoklu örnek temp yarışı.** Uygulama açılışta `vidshrink_*` kalıbındaki her şeyi siliyor
  (`App.xaml.cs:17`), probe dosyaları da bu kalıpta (`:383`, `:493`). İkinci bir VidShrink açılırsa
  birincinin **aktif** probe dosyalarını siler. PID veya oturum klasörü ile ayrılmalı.
- **Temp sızıntısı düşük ama var.** `finally` blokları silmeyi deniyor (`:443-444`, `:525`); süreç
  `Kill` edildikten hemen sonra handle serbest kalmamışsa `File.Delete` başarısız olur ve
  `catch {}` yutar. Açılıştaki `TempCleanup` bunu toplar, gün içinde birikir.
- **Yarış / süreç sızıntısı yok.** `Task.WhenAll` (`:47`, `:279`) tüm görevleri bekliyor, gate
  ondan sonra dispose ediliyor, stdout ve stderr'in **ikisi de** okunuyor (`:415-417`, `:478-480`)
  — ffmpeg pipe deadlock'u yok; `TryKill` süreç ağacını kapatıyor (`:551`). Bu kısım temiz.
- **CPU aşırı aboneliği.** `ScanConcurrency = 8` sabit (`:20`, `:265`), makine çekirdek sayısından
  bağımsız; ffmpeg'e `-threads` verilmiyor, yani 8 süreç × N iş parçacığı. Pencereler de 3 paralel
  tam çözünürlük kodlaması (`:44-47`).

## 4. Sabitler

`WindowSeconds=2`, `MaxWindows=3`, `MinProfileSeconds=4` (`:12-14`); `ScanPointSeconds=1.0`,
`ScanWarmupSeconds=0.75`, `ScanPointCount=40`, `ScanPointsPerWindow=4`, `ScanConcurrency=8`,
`ScanWidth/Height=480×270` (`:16-22`); `PacketFullReadSeconds=180`, `PacketIntervalCount=40`
(`:26-29`). **Hiçbirinin arkasında ölçüm kaydı yok.** `docs/` altında yalnızca hedef ifadeler var:
`docs/claude-engine-audit-report.md:124` "5–12 kısa pencere" öneriyor (kodda 3);
`docs/tasks/yol-haritasi.md:145` sabit üç pencerelik `Windows()`'un değişmesini planlıyor;
`docs/cpu-algoritma-checkup.md:48-52` artık geçersiz (pencereler paralelleşti, split kuruldu).
Testler yalnızca sayıların **kendisini** sabitliyor (`ComplexityScanTests.cs:44-46` 40/12 bekliyor),
doğruluğunu değil. 0.75'lik ısınma hiçbir yerde gerekçelendirilmemiş.

## 5. Hız

1. **Scan'i tek sürece indir (en büyük kazanç).** 40 nokta = 40 ffmpeg başlatma + 40 seek + 40
   encoder init (`:266-277`). `concat` demuxer `inpoint`/`outpoint` listesiyle 40 nokta tek çağrıda,
   tek `-vstats_file` ile kodlanabilir; ısınma ayrımı zaten `time=` üzerinden (`:529-547`).
2. **Ucuz olanı önce dene.** `MeasureWindowBiasAsync` önce pahalı scan'i çalıştırıyor (`:247-251`);
   `PacketBiasAsync` hiç kodlama yapmayan tek ffprobe çağrısı (`:296-298`). >180 sn'lik kaynaklarda
   sırayı ters çevirmek 40 kodlamayı tamamen atlar. Band farkı (0.08 vs 0.05) bunun bedeli — ya
   bandı eşitle ya da scan'i yalnızca paket biası güvenilmezse çalıştır.
3. **Boşa kodlamayı ölçüme çevir.** Nokta başına 0.75 sn ısınma için kodlanıyor, 0.25 sn sayılıyor
   (K3). `ScanPointSeconds` 1.0 → 1.5 ve `ScanPointCount` 40 → 28: kodlanan toplam süre aynı kalır,
   ölçülen kare sayısı birkaç kat artar, süreç sayısı %30 azalır.
4. **`-threads 1` + `ScanConcurrency = Environment.ProcessorCount`.** 480×270 ultrafast tek iş
   parçacığında zaten hızlı; iş parçacığı çakışmasını kaldırmak duvar saatini düşürür (`:20`).
5. **Yarı ölçek örneğini scan'den türet.** `WindowScanPoints` zaten pencerelerin içinde (`:97-105`).
   Scan tam çözünürlükte + `split=2` ile yapılırsa ayrı pencere kodlaması (`:44-47`) tamamen kalkar
   ve K1'deki domain uyumsuzluğu da kapanır — tek geçişte hem profil hem bias.
