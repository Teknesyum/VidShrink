# VidShrink Motor Hata Analizi ve Üst Düzey Geliştirme Raporu

Tarih: 18 Ağustos 2026  
Hedef okuyucu: Projeyi devralacak Claude veya başka bir kıdemli .NET/FFmpeg ajanı

## Yönetici özeti

VidShrink'in mevcut motoru sıradan bir FFmpeg komut oluşturucusundan ileridedir: hedef boyutu toplam bit bütçesine çevirir, ses bütçesini ayırır, içerikten kısa örnekler ölçer, çözünürlük/FPS adaylarını skorlar, kalite tavanında gereksiz bit harcamaz ve taşan sonucu ölçerek tekrar kodlar. Ancak bugün için "piyasanın en iyi motoru" olduğu kanıtlanamaz. Bunun nedeni yalnızca eksik özellikler değil, karşılaştırmalı ve tekrarlanabilir bir kalite benchmark sisteminin henüz bulunmamasıdır.

Bu denetimde altı somut hata/güvenlik açığı düzeltildi ve ilk gerçek test projesi eklendi. Motorun bundan sonraki ana hedefi heuristik bppf tahmininden, örnek kodlama + tam referanslı algısal metrik + aday arama sistemine geçmektir.

## Bu denetimde düzeltilen hatalar

### 1. Filtresiz GIF ikinci geçişi geçersiz filtergraph üretiyordu

Önceki komut, ölçek/FPS filtresi olmadığında `[x];[x][1:v]paletteuse=...` üretiyordu. `[x]` akışı hiçbir yerde oluşturulmadığı için FFmpeg başarısız oluyordu.

Yeni davranış:

- Filtre yoksa `[0:v][1:v]paletteuse=dither=sierra2_4a` kullanılır.
- Filtre varsa filtre çıkışı `[x]` etiketiyle paletteuse'a bağlanır.
- Gerçek FFmpeg ile sentetik 320×240/24 FPS kaynak üzerinde iki geçişli GIF üretimi doğrulandı.
- Çıktı: geçerli GIF, 1.000 saniye, 289.661 bayt.

### 2. Hedef boyut düzeltmesi sabit ses maliyetini yanlış ölçekliyordu

Önceki tekrar hesabı bütün dosya boyutunun oranını yalnızca video bit hızına uyguluyordu. Ses boyutu sabit kaldığı için özellikle ses payının yüksek olduğu sıkı hedeflerde ikinci deneme yine taşabiliyordu.

Yeni davranış:

- Önce süreden tahmini ses megabaytı hesaplanır.
- Gerçek çıktıdan ses payı çıkarılarak ölçülen video boyutu bulunur.
- Hedefte ses için yer ayrıldıktan sonra kalan video bütçesine göre düzeltme yapılır.
- Video bit hızı ayrıca hedefin yüzde 94 güvenlik payıyla hesaplanan mutlak kalan bütçeyi aşamaz.

### 3. Dönüştürme ekranı geçersiz container/codec eşleşmelerini FFmpeg'e kadar geçiriyordu

Örnekler: WebM + H.264 + AAC veya MP4 + Opus. Bunlar artık kodlama başlamadan anlaşılır doğrulama hatası üretir. Video ve ses encoder/container uyumluluğu ayrı matrislerle kontrol edilir.

Ek doğrulamalar:

- Negatif başlangıç/bitiş zamanı reddedilir.
- Kaynak süresinin sonundan başlayan kırpma reddedilir.
- Sıfır/negatif FPS reddedilir.
- Pozitif olmayan veya yuv420p için tek sayılı özel çözünürlük reddedilir.
- GIF ses encoder seçimini yok sayar; gereksiz uyumluluk hatası çıkarmaz.

### 4. AI `extraArgs` alanı güvenli değildi

Önceki filtre yalnızca boş değer, `..`, `&` ve `|` içeren tokenları kaldırıyordu. `-f image2 evil.png`, ek `-i`, protokol veya ek çıktı argümanları hâlâ kabul edilebilirdi. Ayrıca tokenların tek tek silinmesi flag/değer çiftlerini bozabilirdi.

Yeni davranış:

- Yalnızca eşli ve doğrulanmış allowlist argümanları kabul edilir.
- Şimdilik güvenli liste: `-tune`, `-profile:v`, `-level:v`, `-aq-mode`, `-aq-strength`.
- Bilinmeyen flag ve değerler komuta eklenmez; kullanıcıya uyarı döner.
- Çıktı yaratabilen veya yeni girdi açabilen argümanlar reddedilir.

### 5. AI planı kullanıcı izinlerini aşabiliyordu

AI JSON'u, kullanıcı çözünürlük veya FPS düşürmeyi kapatsa bile daha düşük değerler uygulayabiliyordu.

Yeni davranış:

- Çözünürlük düşürme kapalıysa kaynak çözünürlüğü geri yüklenir.
- FPS düşürme kapalıysa kaynak FPS'i geri yüklenir.
- Kaynaktan büyük çözünürlük ve yüzde 1,5'ten fazla en-boy oranı sapması reddedilir.

### 6. FFmpeg hatasında kısmi çıktı kalabiliyordu

İptal durumunda çıktı siliniyordu, fakat normal FFmpeg hatalarında yarım dosya kalabiliyordu. Hem sıkıştırma hem dönüştürme akışı artık tüm hata türlerinde kısmi çıktıyı temizler.

## Test altyapısı

Önceden çözümde test projesi yoktu; `dotnet test VidShrink.sln` başarılı görünse de hiçbir davranışı sınamıyordu.

Eklenen proje: `tests/VidShrink.Tests`

Mevcut 11 test şunları kapsar:

- Filtresiz ve ölçekli GIF filtergraph'ları.
- GIF ses seçimi köşe durumu.
- Tek sayılı çözünürlük reddi.
- Container/video/audio codec uyumsuzlukları.
- Geçersiz kırpma başlangıcı.
- Ses payını ayıran hedef boyut düzeltmesi.
- Zararlı/bozuk AI ek argümanlarının çiftler hâlinde temizlenmesi.
- Kullanıcının çözünürlük/FPS izinlerinin korunması.
- En-boy oranı bozan AI planının reddi.

## Mevcut motorun güçlü tarafları

- Hedef MB değerini süreye bağlı toplam bitrate bütçesine çeviriyor.
- Ses bütçesini video bütçesinden önce ayırıyor; sıkı hedefte mono veya sessiz seçenek üretebiliyor.
- Kaynak bitrate'ine körü körüne güvenmek yerine kısa örneklerle içerik karmaşıklığı ölçüyor.
- Tam ve yarım çözünürlük örneklerinden başlığa özel detail-falloff tahmini çıkarıyor.
- Çözünürlük/FPS adaylarını kalite ve algısal ceza üzerinden arıyor.
- Kalite tavanında dosyayı hedefe kadar anlamsız biçimde şişirmiyor.
- Hedefi zorlayan durumda iki geçişli VBR ve ölçülen taşmaya göre tekrar deneme kullanıyor.
- İptalde process tree sonlandırma, pass-log ve kısmi çıktı temizliği var.
- AI planı doğrulama başarısızsa otomatik plana güvenli biçimde dönüyor.

## "Piyasanın en iyisi" iddiasını engelleyen kritik boşluklar

### P0 — HDR ve 10-bit doğruluğu

Motor bütün sıkıştırma çıktılarında koşulsuz `yuv420p` kullanıyor. HDR/10-bit kaynakta bu, bit derinliği ve HDR sinyalinin doğru korunmaması veya doğru tone-map yapılmaması riskini doğurur. `MediaInfo.IsHdr` ölçülüyor fakat planlama/argüman üretiminde kullanılmıyor.

Gerekli çözüm:

1. Kullanıcı politikası: HDR koru veya SDR'ye tone-map et.
2. HDR korumada Main10 uyumlu encoder, `yuv420p10le`, color primaries/transfer/matrix ve mastering metadata aktarımı.
3. SDR dönüşümünde zscale/tonemap/zscale zinciri ve BT.709 metadata.
4. HDR10, HLG, Dolby Vision fallback ve 8/10-bit regresyon klipleri.

Bu tamamlanmadan genel amaçlı en iyi motor iddiası kullanılamaz.

### P0 — Algısal kalite kapalı döngüsü yok

Mevcut `ComplexityProbe`, x264 CRF örneklerinin byte/frame değerinden bppf çıkarıyor. Bu iyi bir heuristik fakat çıktıyı kaynakla karşılaştırıp gerçek algısal kaybı ölçmüyor.

Önerilen mimari:

1. Sahne değişimlerini temsil eden 5–12 kısa pencere seç.
2. Her codec/çözünürlük adayı için hızlı örnek encode yap.
3. Çıktıyı referans çözünürlüğüne getirerek VMAF NEG + XPSNR ölç.
4. Ortalama yerine p10/harmonic mean ve en kötü sahne tabanı kullan.
5. CRF veya q değerini interpolasyonlu binary search ile ara.
6. Hedef boyut ve minimum kaliteyi birlikte sağlayan Pareto adayını seç.

`ab-av1` bugün örnek encode + CRF search + VMAF/XPSNR yaklaşımını uygulayan önemli bir karşılaştırma noktasıdır. VidShrink bunu çoklu codec, hedef boyut, çözünürlük/FPS ve kullanıcı niyetiyle daha ileri taşımalıdır.

### P0 — Rakip benchmark laboratuvarı yok

En iyi iddiası için sabit bir corpus ve otomatik sonuç tablosu gereklidir.

Corpus sınıfları:

- Düşük ışık ve film grain.
- Anime/2D çizgi içerik.
- Ekran kaydı, yazı ve UI.
- Spor ve hızlı kamera hareketi.
- Konuşan kafa ve sığ alan derinliği.
- Telefon VFR görüntüsü.
- 4K HDR10/HLG.
- Eski interlaced içerik.
- Çok kanallı ses ve sessiz video.
- Çok kısa ve çok uzun video.

Her klip/hedef/codec için metrikler:

- Hedef boyut sapması yüzde ve mutlak MB.
- VMAF NEG mean, harmonic mean, p10 ve minimum.
- XPSNR ve SSIM/MS-SSIM.
- CAMBI veya banding metriği.
- Encode süresi, fps, CPU/GPU kullanımı ve enerji tahmini.
- Çıktı uyumluluğu ve decode testi.
- İnsan A/B tercihi için kör örnek kimliği.

Rakipler: VidShrink mevcut motor, ab-av1, HandBrake varsayılanları, doğrudan FFmpeg codec reçeteleri ve hedef-boyut iddiası olan seçilmiş masaüstü araçları. Aynı encoder sürümü ve aynı makine kullanılmalıdır.

Başarı kapıları:

- Hedef boyut p95 sapması: `<= %1`, hiçbir taşma yok.
- Aynı boyutta median VMAF NEG: en iyi rakipten kötü değil.
- En kötü yüzde 10 sahnede rakibe karşı anlamlı gerileme yok.
- Decode/uyumluluk başarısı: `%100`.
- Başarısız veya yarım çıktı: `0`.

### P1 — Codec aday turnuvası

Auto bugün rejime göre çoğunlukla H.264 veya H.265 seçiyor. AV1 ve VP9 hedef boyut motorunun otomatik aday havuzunda etkin yarışmıyor.

- Uyumluluk hedefi ve donanım decoder bilgisini girdi yap.
- H.264, x265 Main/Main10, SVT-AV1 ve uygun olduğunda VP9 için kısa örnek turnuvası çalıştır.
- Kalite, süre ve enerji ağırlıklarını kullanıcı niyetine göre değiştir.
- Hızlı modda NVENC/QSV/AMF capability probe ve yazılım fallback ekle.

### P1 — Codec'e özgü rate-control

Tek bir `-pass 1/2`, `-maxrate` ve `-bufsize` reçetesi bütün encoderlarda aynı anlama gelmez. NVENC/QSV/SVT-AV1/x265 için ayrı strateji sınıfları gerekir.

Önerilen `IEncoderStrategy` sorumlulukları:

- Capability probe.
- Quality range ve quality-direction.
- CRF/CQ/VBR/2-pass argüman üretimi.
- Preset maliyet modeli.
- 8/10-bit ve HDR desteği.
- Pass-log davranışı.
- Hedef boyut tahmini ve düzeltme politikası.

### P1 — Film grain, animasyon ve ekran içeriği sınıflandırması

SVT-AV1'in resmi belgeleri film-grain synthesis'in verimi ciddi biçimde artırabildiğini, fakat aşırı kullanımın ince ayrıntıyı silebildiğini belirtiyor. VidShrink örnek pencerelerden noise/grain, çizgi yoğunluğu ve screen-content özellikleri çıkarmalıdır.

İlk sürüm:

- Grain, animation/screen-content ve dark-scene skorları.
- SVT-AV1 film-grain adayları 0/4/8/12.
- x264/x265 tune adayları film/animation/grain.
- Yalnızca örnek metrik ve kabul eşiği kazandırıyorsa uygulama.

### P1 — Kaynak normalizasyonu

Eksik başlıklar:

- Interlace tespiti ve bwdif/yadif kararı.
- VFR zaman tabanı ve frame pacing doğrulaması.
- Sample/display aspect ratio.
- Crop/letterbox tespiti.
- Renk aralığı ve chroma location.
- Döndürme metadata'sının çıktı doğrulaması.
- Birden fazla ses/altyazı akışı politikası.

### P1 — Ses motoru

- LUFS ve true-peak analizi.
- Konuşma/müzik sınıflandırması.
- AAC/Opus codec seçimi ve VBR modu.
- 5.1 downmix matrisi ve dialog normalization.
- Sessizlik oranına göre ses bütçesi.
- Container ve cihaz uyumluluğuna göre ses profili.

### P2 — Boyut kalibrasyonu ve öğrenen model

- Gerçek encode sonrası codec, preset, içerik özellikleri, kalite değeri, boyut ve metrik kaydı tut.
- Kullanıcı izniyle yerel kalibrasyon modeli oluştur.
- Container overhead, muxer ve encoder sürümüne göre kalibre et.
- İlk denemede hedefe yaklaşma oranını ölç ve model versiyonla.
- Öğrenen model hiçbir zaman hard ceiling ve güvenlik kurallarını aşmamalıdır.

### P2 — Dayanıklılık ve operasyon

- FFmpeg/ffprobe sürüm ve encoder capability cache.
- Uygulama kapanırken aktif işin garantili iptali.
- Windows sleep engelleme seçeneği.
- Atomic output: önce `.partial`, başarıda final ada taşı.
- Disk alanı ön kontrolü.
- Ağ yolu/uzun yol/Unicode/locked file testleri.
- Structured log ve tek tuşla tanılama paketi.
- Crash sonrası eski pass log ve `.partial` temizliği.

## Önerilen uygulama sırası

### Milestone 1 — Kanıtlanabilir doğruluk

1. Test corpus manifesti ve benchmark CLI.
2. Hedef boyut sapma testleri.
3. HDR/10-bit güvenli politika.
4. Atomic output ve disk alanı kontrolü.
5. Encoder capability matrisi.

Çıkış kriteri: 100+ otomatik test, sentetik ve gerçek medya entegrasyon testleri, hiçbir hedef taşması/yarım çıktı yok.

### Milestone 2 — Algısal arama

1. Sahne-temsilli örnek seçimi.
2. VMAF NEG + XPSNR ölçümü.
3. CRF binary/interpolation search.
4. p10/harmonic-mean kalite kapısı.
5. Çözünürlük ve codec Pareto turnuvası.

Çıkış kriteri: Sabit corpus üzerinde mevcut motordan istatistiksel olarak daha iyi kalite; süre bütçesi raporlu.

### Milestone 3 — İçerik uzmanlığı

1. Grain/animation/screen/dark sınıflandırması.
2. Codec'e özgü tune ve 10-bit seçenekleri.
3. Ses analiz motoru.
4. VFR/interlace/crop/color pipeline.

### Milestone 4 — Piyasa iddiası

1. Rakiplerin aynı makine/encoder sürümünde otomatik benchmarkı.
2. Sonuçların CSV/JSON ve grafik olarak yayımlanması.
3. Kör insan A/B testi.
4. Başarı kapılarının CI'da korunması.

Yalnızca bu milestone tamamlandıktan sonra "piyasanın en iyisi" ifadesi teknik olarak savunulabilir.

## Referanslar

- Netflix VMAF: https://github.com/Netflix/vmaf
- VMAF model ve NEG açıklaması: https://github.com/Netflix/vmaf/blob/master/resource/doc/models.md
- AOM Common Test Conditions metrikleri: https://github.com/Netflix/vmaf/blob/master/resource/doc/aom_ctc.md
- ab-av1 örnek encode, CRF arama ve VMAF/XPSNR yaklaşımı: https://github.com/alexheretic/ab-av1
- SVT-AV1 resmi proje ve belgeler: https://gitlab.com/AOMediaCodec/SVT-AV1/
- SVT-AV1 film grain tavsiyeleri: https://gitlab.com/AOMediaCodec/SVT-AV1/-/blob/master/Docs/CommonQuestions.md
- SVT-AV1 rate-control ve parametreler: https://gitlab.com/AOMediaCodec/SVT-AV1/-/blob/master/Docs/Parameters.md

## Claude için doğrudan görev özeti

Önce bu dosyayı, `docs/claude-handoff-report.md`, `docs/implementation-report.md` ve motor kaynaklarını oku. P0 sırasını bozma. Yeni kalite optimizasyonu eklemeden önce benchmark corpus ve ölçüm CLI'sını kur. Her değişiklikte hedef boyut, VMAF NEG/XPSNR, encode süresi ve uyumluluk sonuçlarını önceki sürümle karşılaştır. Ölçülmeyen iyileştirmeyi başarı sayma; HDR ve hard-ceiling doğruluğunu regresyona açık bırakma.
