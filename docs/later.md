# Raf

Sahibin sözü, 13 Ekim 2026:

> şimdi tüm piyasayı araştır her bir sekmedeki eksiklerimiz hakkında kapsamlı plan yap saat 8 e kadar durma şu aşamada plan yap sadece işlem 2. aşama olacak bulduklarını later a eklersin

Aşağıdaki her başlık, ikinci aşamada tek seferde teslim edilebilecek bir iş paketidir; pazar araştırmasından çıktı.
Sıra yapım sırasıdır. Bir paketin işi başlayınca başlığı buradan silinir.
Gerekçe, sayılar ve kanıt: [plan-pazar-eksikleri.md](plan-pazar-eksikleri.md).

**Dalga 2 — Karar Gerekmez, Küçük İş Öncelik 2 Ya Da Orta İş Öncelik 1**

## 34. Medya Bilgisi Ve Denetim Raporu

13 Ekim 2026 · "bulduklarını later a eklersin" · ARC-P2 · Bizde Olmayan Araçlar · iş orta · öncelik 1 · karar gerekmez

- Satırlar (ARC-P2): ARC-01 Medya bilgisi görüntüleyici; ARC-05 Denetim raporu. [Tablo](piyasa/2026-10-13/eksik-araclar-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/MediaInfo.cs, src/VidShrink.Ffmpeg/FfprobeClient.cs, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/Playback/PlayerView.Window.cs, src/VidShrink.Core/Editing/SilenceCut.cs, src/VidShrink.App/Editing/EditorView.Sessizlik.cs
- Plan: Küçült'ün kaynak paneline ve oynatıcının bilgi katmanına "tüm ayrıntılar" düğmesi eklenir; açılan pencere ffprobe'un zaten okuduğu akışları ve bölümleri tek tek gösterir, "kopyala" ve "metin olarak kaydet" verir. Aynı pencerenin ikinci bölümü düzenleyicinin sessizlik, siyah kare ve sahne taramasını çağırıp sonucu zaman kodlu liste olarak verir; düzenleyicinin tarama paneline de "listeyi kaydet" eklenir. Doğrulama: StreamMappingTests'in ürettiği çok izli, bölümlü 3 saniyelik dosyada dökümün her izi ve bölümü içerdiği; sahte tarayıcıyla listenin tarama sonucuyla birebir aynı olduğu sınanır.

## 36. Resim Dizisi Girdisi Ve Ara Kare

13 Ekim 2026 · "bulduklarını later a eklersin" · ARC-P4 · Bizde Olmayan Araçlar · iş orta · öncelik 1 · karar gerekmez

- Satırlar (ARC-P4): ARC-09 Resim dizisi girdisi; ARC-13 Resimlerden GIF; ARC-28 Kare ara değerleme. [Tablo](piyasa/2026-10-13/eksik-araclar-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Core/DroppedMedia.cs, src/VidShrink.Core/VideoFilterChain.cs, src/VidShrink.App/MainWindow.axaml
- Plan: Üçü de Dönüştür'ün kare hızı yoluna dokunur. Bir klasör ya da numaralı resimler bırakılınca dosyalar ada göre sıralanıp ffmpeg'in resim dizisi okuyucusuyla tek kaynak sayılır; kare hızı kutusu dizinin hızını belirler, GIF kabı seçilince resimlerden GIF de aynı yoldan çıkar. Kare hızı kutusuna "ara kare üret" seçeneği eklenir (minterpolate süzgeci; yapıda yoksa seçenek gizlenir). Doğrulama: üretilmiş 30 karelik PNG dizisinde çıktının kare sayısı ve süresi beklenene eşit; ara karede çıktının kare sayısı hedef hıza uyar.

## 39. macOS Tıklama İşareti

13 Ekim 2026 · "bulduklarını later a eklersin" · PLT-P3 · Platform Eşitliği · iş küçük · öncelik 2 · karar gerekmez

- Satırlar (PLT-P3): PLT-10 macOS'ta tıklama gösterimi. [Tablo](piyasa/2026-10-13/platform-esitligi-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.App/Recorder/RecorderView.Girdi.cs, src/VidShrink.App/Recorder/RecorderView.axaml, tests/VidShrink.Tests
- Plan: Mac'te tıklamaları göster kutusu ffmpeg'in `-capture_mouse_clicks 1` seçeneğine bağlanır; ek izin ve yardımcı ikili gerekmez. PLT-P1 ile aynı fonksiyona dokunur ve PLT-P2'nin gizlediği kutuyu yalnız Mac'te geri açar, bu yüzden ikisinden sonra gelir. Argüman testiyle doğrulanır; işaretin görünümü gerçek Mac'te görülmedi. EKB-P17 açılırsa bu paket onun macOS tıklama kolunu karşılamış olur.

**Dalga 3 — Karar Gerekmez, Orta İş Öncelik 2 Ya Da Büyük İş Öncelik 1**

## 40. Komut Satırı Ve Toplu Teslim

13 Ekim 2026 · "bulduklarını later a eklersin" · DZA-P9 · Düzenleyici · iş büyük · öncelik 1 · karar gerekmez

- Satırlar (DZA-P9): DZA-28 Komut satırında çok parçalı kesim; DZA-09 Toplu teslim. [Tablo](piyasa/2026-10-13/duzenleyici-kesim-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Cli/CliRequest.cs, src/VidShrink.Core/Editing/EditProject.cs, src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.Core/JobPump.cs, src/VidShrink.App/Editing/
- Plan: Önce pencere açmadan "proje dosyası girer, teslim çıkar" koşan çekirdek işlev ayrılır ve komut satırına bağlanır; bugün komut satırı yalnız tek aralık kesiyor (CliRequest.cs:425-435). Sonra düzenleyiciye proje ve dosya sırası gelir; sıra Küçült'ün iş pompasıyla (JobPump.cs:8) art arda koşar, bir işin düşmesi ötekileri durdurmaz. Bölünecekse önce komut satırı (orta), sonra sıra. Doğrulama: komut satırı argüman testleri ve sahte teslimle sıra testi.

## 41. Hızlı Kırpma Ve Kayıt Geçmişi

13 Ekim 2026 · "bulduklarını later a eklersin" · EKB-P6 · Kaydedici · iş büyük · öncelik 1 · karar gerekmez

- Satırlar (EKB-P6): EKB-09 Hızlı kırpma; EKB-25 Kayıt geçmişi. [Tablo](piyasa/2026-10-13/kaydedici-sunum-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.App/Recorder/RecorderView.axaml, src/VidShrink.App/Recorder/RecorderView.axaml.cs, src/VidShrink.App/Recorder/, src/VidShrink.App/Playback/ClipExport.cs, src/VidShrink.App/Recorder/RecorderSettings.cs, src/VidShrink.App/Locales/
- Plan: Sonuç paneline küçük resim şeridi ve iki tutamak eklenir; kesim yeniden kodlamadan `-ss`, `-t` ve `-c copy` ile yapılır ve en yakın anahtar kareye (tek başına çözülebilen kare) oturur; bu yol oynatıcıda var (ClipExport.cs:65-71). Aynı küçük resim üretimi son kayıtların listesinde kullanılır: ad, süre, boyut; aç, paylaş, sil. İki adımda teslim edilebilir: önce kırpma, sonra geçmiş. Kesilen dosyanın süresi ffprobe ile, liste sahte klasörle ölçülür.

## 42. Ekrana Çizim Ve Prompter

13 Ekim 2026 · "bulduklarını later a eklersin" · EKB-P7 · Kaydedici · iş büyük · öncelik 1 · karar gerekmez

- Satırlar (EKB-P7): EKB-04 Ekrana çizim; EKB-15 Prompter. [Tablo](piyasa/2026-10-13/kaydedici-sunum-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.App/Recorder/, src/VidShrink.App/Recorder/CaptureAffinity.cs, src/VidShrink.App/Recorder/RecorderView.Kisayol.cs, src/VidShrink.App/Recorder/RecorderSettings.cs, src/VidShrink.App/Locales/
- Plan: Çizim için ekranı kaplayan saydam bir pencere açılır; kısayolla çizim kipine girilir, kalem, ok, kutu, numara ve yazı çizilir, pencere kayda girer. Prompter (konuşma metnini akıtan pencere) ayrı penceredir ve `CaptureAffinity.Exclude` ile kayıttan çıkarılır; Windows 10 2004 öncesinde pencere kayıttan çıkmadığı için orada uyarı gösterilir (CaptureAffinity.cs:16-20). İki adımda teslim edilebilir: önce çizim, sonra prompter. Çizim araçları başsız pencere testiyle, kayıttan çıkarma bayrağı pencere başına ölçülür.

## 43. Yerel Denetim Borusu Ve Kayıt Komutu

13 Ekim 2026 · "bulduklarını later a eklersin" · DAG-P4 · Dağıtım Ve Otomasyon · iş büyük · öncelik 1 · karar gerekmez

- Satırlar (DAG-P4): DAG-35 Denetim arabirimi; DAG-31 Komut satırında ekran kaydı. [Tablo](piyasa/2026-10-13/dagitim-otomasyon-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/SingleInstanceChannel.cs, src/VidShrink.Core/ShrinkRequest.cs, src/VidShrink.App/Integration/, src/VidShrink.App/Recorder/, src/VidShrink.Cli/, docs/
- Plan: Tek örnek borusu (ikinci kopyanın ilk kopyaya dosya yolu ilettiği, yalnız aynı kullanıcıya açık adlandırılmış boru) belgeli bir komut kanalına genişletilir: satır başına bir JSON komutu ve yanıtı (oynat, duraklat, konum, dosya aç, kaydı başlat ve durdur, kuyruğa küçültme ekle, durum sor). Yeni komutlar Ayarlar'daki bir anahtar ya da başlatma bayrağı açılmadıkça reddedilir; ağ soketi açılmaz, yeni paket eklenmez. Komut satırına bu kanalın istemcisi olan kayıt başlatma, kayıt durdurma ve durum komutları gelir; protokol docs/ altında belgelenir. Doğrulama: boru üstünden komut ve yanıt testleri, kapalıyken reddetme testi, elle de komutla kayıt başlatıp durdurma.

## 44. Etiket, Bölüm Ve Onarma

13 Ekim 2026 · "bulduklarını later a eklersin" · ARC-P5 = DON-P14 · Bizde Olmayan Araçlar · iş büyük · öncelik 1 · karar gerekmez

- Satırlar (ARC-P5): ARC-14 Etiket düzenleyici; ARC-15 Bölüm düzenleyici; ARC-16 Yeniden sararak onarma. [Tablo](piyasa/2026-10-13/eksik-araclar-dogrulanmis.md)
- Satırlar (DON-P14): DON-29 Meta veri düzenleme; DON-30 Bölüm düzenleme; DON-31 İz etiketleri. [Tablo](piyasa/2026-10-13/donustur-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/RecorderChapters.cs, src/VidShrink.Core/StreamMapping.cs, src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.Cli/CliRequest.cs
- Plan: Üçü de Dönüştür'de "Kopyala" seçiliyken, yani yeniden kodlamadan yapılır; sırayla ayrı ayrı teslim edilebilir. Etiket için "Meta veriyi sil" kutusunun yanına başlık, sanatçı, yıl, açıklama ve kapak alanları gelir; bölüm için kaydedicinin bölüm yazan yolu ortak hâle getirilir ve kaynağın bölümleri eklenip adlandırılıp silinebilen liste olur; onarma için akışları kopyalayan, bozuk zaman damgasını hoş gören bayraklarla "Onar" hazır ayarı eklenir. Doğrulama: MetaSilDonusturTests ve KayitBolumIsaretiTests'teki gibi ffprobe ile etiket ve bölüm okunur; boş alan anahtarı hiç yazmaz.

## 47. Doldurma Ve Dokunmatik Hareketler

13 Ekim 2026 · "bulduklarını later a eklersin" · OYA-P11 · Oynatıcı · iş orta · öncelik 2 · karar gerekmez

- Satırlar (OYA-P11): OYA-47 Kenar bantlarını kırparak doldurma; OYA-53 Dokunmatik hareketler. [Tablo](piyasa/2026-10-13/oynatici-cekirdek-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.App/Playback/PlayerView.axaml.cs, src/VidShrink.App/Playback/ZoomGesture.cs, src/VidShrink.App/Playback/PlayerView.Fare.cs, src/VidShrink.App/Playback/Keymap.cs
- Plan: Görüntüyü pencereye sığdıran ölçek (`Resize`, PlayerView.axaml.cs:854–862) için ikinci bir kip eklenir: doldur kipinde ölçek panoyu tümüyle kaplayacak biçimde seçilir, taşan kısım kırpılır; tek tuşla sığdır ile doldur arasında geçilir. Dokunmatik ekran için işaretçi türü ayrılır: iki parmakla yakınlaştırma, yatay kaydırmayla atlama, dikey kaydırmayla ses. Fare davranışı değişmez. Doğrulama: doldur kipinde kare ölçüsünün panoyu kapladığı görünüm testi; üretilmiş dokunma olaylarıyla hareket testi ve gerçek dokunmatik ekranda elle deneme.

## 48. Kabukta Çoklu Açma Ve Sıraya Ekleme

13 Ekim 2026 · "bulduklarını later a eklersin" · OYB-P7 · Oynatıcı · iş orta · öncelik 2 · karar gerekmez

- Satırlar (OYB-P7): OYB-32 Çoklu seçimle açma; OYB-33 Kabukta sıraya ekle fiili; OYB-34 Ses ve liste dosyalarında kabuk kaydı. [Tablo](piyasa/2026-10-13/oynatici-kitaplik-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.App/Program.cs, src/VidShrink.App/Integration/ForwardedFiles.cs, src/VidShrink.App/MainWindow.SingleInstance.cs, src/VidShrink.Core/Setup/ShellRegistration.cs, src/VidShrink.App/ShellMenu.cs, Install-VidShrink.ps1, tests/VidShrink.Tests/KabukMenusuTests.cs
- Plan: İkinci örnek artık bütün yolları çalışan örneğe aktarır; gelen yollar OYB-P2'nin toplayıcısıyla, yeni açılış yerine kuyruğa eklenir. Sağ tık menüsüne "Sıraya ekle" fiili ve onu taşıyan bir komut satırı bayrağı gelir; bayrak kuyruğa ekleme işlemine (`Enqueue`) bağlanır. Ses ve liste uzantıları yalnız "aç" ve "sıraya ekle" fiillerine eklenir, "küçült" fiiline eklenmez. Kanal ve kabuk kaydı testleriyle, sonra Explorer'da elle çoklu seçimle doğrulanır.

## 49. Yer İmi Listesi Ve Sık Kullanılanlar

13 Ekim 2026 · "bulduklarını later a eklersin" · OYB-P8 · Oynatıcı · iş orta · öncelik 2 · karar gerekmez

- Satırlar (OYB-P8): OYB-15 Adlı yer imi ve yer imi listesi; OYB-16 Sık kullanılanlar. [Tablo](piyasa/2026-10-13/oynatici-kitaplik-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.App/Playback/PlaybackHistory.cs, src/VidShrink.App/Playback/Keymap.cs, src/VidShrink.App/Playback/PlayerView.Window.cs, src/VidShrink.App/Locales/
- Plan: Yer imine isteğe bağlı bir ad alanı eklenir (adsız eski kayıt okunmaya devam eder); bir pencere dosyanın yer imlerini listeler, seçilene gider ve tek tek siler. Ayrıca dosya, klasör ve adres tutan kalıcı bir sık kullanılanlar listesi menüye eklenir. Adres saklanırken sorgunun ve parolanın diske yazılmama kuralı (`docs/plan-url-oynatma.md:26`) uygulanır. Başsız testle ekleme, adlandırma, silme ve yeniden açılışta kalıcılık ölçülür.

## 50. Mac Ortam Denetimi Bağlantısı

13 Ekim 2026 · "bulduklarını later a eklersin" · OYB-P9 · Oynatıcı · iş orta · öncelik 2 · karar gerekmez

- Satırlar (OYB-P9): OYB-30 macOS Now Playing. [Tablo](piyasa/2026-10-13/oynatici-kitaplik-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Player/MpvEngine.cs, src/VidShrink.App/Playback/
- Plan: mpv'nin ortam tuşları seçeneği (`input-media-keys`) gömülü kullanımda varsayılan kapalıdır; önce macOS'ta açılır ve klavye ortam tuşlarının, Denetim Merkezi'ndeki çalan ortam alanının oynatıcıyı yönetip yönetmediği ölçülür. Yetmezse macOS'un ortam denetimi arayüzüne ayrı bir bağ yazılır. Sonraki ve önceki tuşu yine uygulamanın kuyruğuna bağlanır. Bu iş Windows makinesinde doğrulanamaz; Mac'te koşan bir işçi ister.

## 52. Akıllı Kipte MPEG-2

13 Ekim 2026 · "bulduklarını later a eklersin" · DZA-P12 · Düzenleyici · iş orta · öncelik 2 · karar gerekmez

- Satırlar (DZA-P12): DZA-27 Akıllı kipte MPEG-2. [Tablo](piyasa/2026-10-13/duzenleyici-kesim-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/Editing/SmartCutCodec.cs, src/VidShrink.Core/Editing/EditExport.cs, tools/VidShrink.Bench
- Plan: Akıllı kipin kodek kapısına (SmartCutCodec.cs:43) MPEG-2 eklenir; kenar parçası kaynağın profiline, kare hızına ve bit hızına uyan `mpeg2video` kodlayıcısıyla yazılır. TV kayıtlarının çoğu taramalı olduğundan taramalı kaynak kapısı (SmartCutCodec.cs:47) birlikte ele alınmazsa kazanç dar kalır; bu kayıtların sesi de genellikle AC-3 ya da MP2 (yayıncılığın eski ses kodeği) olduğundan paket DZA-P4'ten sonra anlam kazanır. Doğrulama: birleşim noktasında kare sayısı ve bozulma ölçümü `tools/VidShrink.Bench` ile.

## 53. Ses Temizleme Ve Düzey

13 Ekim 2026 · "bulduklarını later a eklersin" · DZB-P5 · Düzenleyici · iş orta · öncelik 2 · karar gerekmez

- Satırlar (DZB-P5): DZB-43 Gürültü azaltma; DZB-45 Ses yüksekliğini eşitleme; DZB-42 Ses efektleri; DZB-41 Ses düzeyi göstergesi. [Tablo](piyasa/2026-10-13/duzenleyici-kurgu-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/Editing/ClipEffects.cs, src/VidShrink.Core/Editing/ClipFilters.cs, src/VidShrink.Core/Editing/EditLook.cs, src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.Core/StreamMapping.cs, src/VidShrink.App/Editing/EditorView.Klip.cs, src/VidShrink.App/Editing/EditorView.axaml, src/VidShrink.Player/MpvEngine.cs, src/VidShrink.App/Locales/
- Plan: Klip paneline gürültü azaltma (`afftdn`), yükseklik eşitleme (`loudnorm`; sabiti StreamMapping.cs:357'de hazır), ekolayzır, kompresör ve sınırlayıcı (`equalizer`, `acompressor`, `alimiter`) eklenir ve ClipFilters'ın ses zincirine (ClipFilters.cs:79) bağlanır. Düzey göstergesi ayrı parçadır: oynarken tepe düzeyi mpv'den okunur; okuma yolu kodda yok, önce o ölçülür. Doğrulama: argüman testleri ve teslim edilen dosyada ölçülen yükseklik değeri.

## 54. Medya Havuzu Ve Araya Ekleme

13 Ekim 2026 · "bulduklarını later a eklersin" · DZB-P11 · Düzenleyici · iş orta · öncelik 2 · karar gerekmez

- Satırlar (DZB-P11): DZB-64 Medya havuzu; DZB-08 Ekleme ve üstüne yazma kipleri. [Tablo](piyasa/2026-10-13/duzenleyici-kurgu-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.App/Editing/EditorView.Kaynak.cs, src/VidShrink.App/Editing/EditorView.axaml, src/VidShrink.App/Editing/EditorTimeline.cs, src/VidShrink.Core/Editing/EditTimeline.cs, src/VidShrink.Core/Editing/EditProject.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/DuzenleyiciCokKaynakTests.cs
- Plan: Projeye alınan kaynaklar bir listede görünür (proje dosyası ek kaynakları zaten tutuyor: EditProject.cs:68); listeden çizelgeye sürüklenen klip bırakıldığı yere girer. Araya ekleme sonraki klipleri iter, üstüne yazma aynı süreyi silip yerine koyar (aralık silme hazır: EditTimeline.cs:148). Dosya ekleyince çizelgenin sonuna gitme davranışı varsayılan kalır, o yüzden karar gerekmez. Doğrulama: DuzenleyiciCokKaynakTests'e iki kipin süre ve sıra ölçüleri.

## 57. Önizlemede Elle Kırpma

13 Ekim 2026 · "bulduklarını later a eklersin" · SIK-P8 · Küçült · iş orta · öncelik 2 · karar gerekmez

- Satırlar (SIK-P8): SIK-12 Görsel elle kırpma. [Tablo](piyasa/2026-10-13/kucult-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.App/MainWindow.axaml (önizleme alanı ve `ChkFltAutoCrop` çevresi), src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.Core/VideoFilterChain.cs, src/VidShrink.App/Locales/
- Plan: Önizleme karesinin üstüne tutamaçlı bir dikdörtgen çizilir; kullanıcı fareyle sürükler, sonuç kaynak pikseline çevrilip var olan `crop` süzgeç yoluna yazılır (`src/VidShrink.Core/VideoFilterChain.cs:330`). Elle kırpma seçiliyken otomatik siyah bant kırpma kapanır; genişlik ve yükseklik kodlayıcının istediği çift sayıya yuvarlanır. Doğrulama: ekran koordinatından kaynak pikseline çeviriyi ölçen test ve çıktı çözünürlüğünü ffprobe ile okuyan test.

## 60. Kodlayıcı, HDR Ve Renk Ayarları

13 Ekim 2026 · "bulduklarını later a eklersin" · DON-P11 · Dönüştür · iş orta · öncelik 2 · karar gerekmez

- Satırlar (DON-P11): DON-15 Kodlayıcı ince ayarı; DON-25 Dönüştür'de HDR seçimi; DON-26 Renk uzayı ve aralık dönüşümü. [Tablo](piyasa/2026-10-13/donustur-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.Core/HdrResolver.cs, src/VidShrink.Core/VideoFilterChain.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/
- Plan: Hız ön ayarı, tune, profil, seviye ve bit hızı kipinde iki geçiş Dönüştür'de seçilebilir olur; bugün sabit varsayılan yazılıyor (`ConversionArguments.cs:142`). HDR seçimi Küçült'teki düğmeden okunmak yerine (`MainWindow.axaml.cs:4777`) Dönüştür'ün kendi panelinde gösterilir. Renk için BT.601, BT.709 ve BT.2020 ile sınırlı ve tam aralık seçimi eklenir; BT.709 ve BT.601 kolu hazır (`VideoFilterChain.cs:397`), BT.2020 ve aralık yeni yazılır. Doğrulama: argüman testleri ve renk kolu için ölçüm aracıyla önce ve sonra kare karşılaştırması.

## 61. Arşiv Kodeği, APNG Ve GIF Ayarı

13 Ekim 2026 · "bulduklarını later a eklersin" · DON-P12 · Dönüştür · iş orta · öncelik 2 · karar gerekmez

- Satırlar (DON-P12): DON-21 FFV1; DON-22 APNG çıktısı; DON-24 GIF ince ayarı; DON-34 CineForm. [Tablo](piyasa/2026-10-13/donustur-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.Core/CodecModel.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.DonusturKap.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/
- Plan: Kodek listesine FFV1 ve CineForm, hareketli görsel kaplarına APNG eklenir; her biri kap uyum tablosuna ve kullanılabilirlik yoklamasına girer. GIF için döngü sayısı, renk sayısı ve dither (az renkle geçişleri noktalayarak yumuşatma) seçimi plana alan olarak eklenir; bugün palet ve dither sabit (`ConversionArguments.cs:105`). Kayıplı GIF sıkıştırması ffmpeg'in GIF kodlayıcısında yoksa bu parça dışarıda kalır; ölçülmedi. Doğrulama: argüman testleri ve yeni kodlayıcıların iki ffmpeg derlemesinde yoklanması.

## 62. Özel Argüman Ve Komut Satırı

13 Ekim 2026 · "bulduklarını later a eklersin" · DON-P13 · Dönüştür · iş orta · öncelik 2 · karar gerekmez

- Satırlar (DON-P13): DON-27 Özel ffmpeg argümanı; DON-28 Komut satırından dönüştürme. [Tablo](piyasa/2026-10-13/donustur-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Cli/CliRequest.cs, src/VidShrink.Cli/, src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/
- Plan: Dönüştür paneline "ek argüman" kutusu konur; yazılan seçenekler komutta çıktı yolundan önce yer alır ve salt okunur komut önizlemesinde görünür (`MainWindow.axaml:1371`). Komut satırına ayrı bir dönüştürme komutu eklenir: kap, kodek, kalite ve kesit bayrakları Dönüştür planına çevrilir; GIF ve ara kodek (ProRes, DNxHR) çıktısı buradan da alınır. Doğrulama: bayrak ayrıştırma testleri ve arayüzle komut satırının aynı plandan aynı argümanı ürettiğini gösteren test.

## 64. Yalnız Ses Ve Ses Seçenekleri

13 Ekim 2026 · "bulduklarını later a eklersin" · EKA-P3 · Kaydedici · iş orta · öncelik 2 · karar gerekmez

- Satırlar (EKA-P3): EKA-15 Yalnız ses kaydı; EKA-22 Ses kodeki seçimi; EKA-28 Ses sıkıştırıcı ve sınırlayıcı. [Tablo](piyasa/2026-10-13/kaydedici-yakalama-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.Core/AudioCaptureArguments.cs, src/VidShrink.App/Recorder/RecorderView.Ses.cs, src/VidShrink.App/Recorder/RecorderView.Gelismis.cs
- Plan: Hedef listesine "yalnız ses" eklenir; görüntü girdisi kurulmaz ve ses girdilerinin sıra numarası varsayımı (`RecorderArguments.cs:366`) buna göre parametreye çevrilir. Ses kodeki sabiti (`RecorderArguments.cs:349`) seçilebilir olur; varsayılan AAC kalır, Opus yalnız onu taşıyan kaplarda sunulur. Ses süzgeç zincirine (`AudioCaptureArguments.cs:75`) ffmpeg'in `acompressor` (ses düzeyini dengeleyen) ve `alimiter` (patlamayı kesen) süzgeçleri iki yeni seçenek olarak girer. Üçü de argüman ölçüleriyle doğrulanır.

## 65. Kodlayıcı Listesi Ve Kare Hızı

13 Ekim 2026 · "bulduklarını later a eklersin" · EKA-P4 · Kaydedici · iş orta · öncelik 2 · karar gerekmez

- Satırlar (EKA-P4): EKA-12 Kaydedicide donanım kodlayıcı Windows dışında; EKA-21 AV1 için QSV ve AMF kodlayıcıları; EKA-24 Sabit ya da değişken kare hızı seçimi. [Tablo](piyasa/2026-10-13/kaydedici-yakalama-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.Core/CodecModel.cs, src/VidShrink.App/Recorder/RecorderView.Gelismis.cs
- Plan: Kaydedicinin kodlayıcı listesine (`RecorderArguments.cs:368`) VideoToolbox, VAAPI, `av1_qsv` ve `av1_amf` eklenir; VideoToolbox ve VAAPI argümanları dönüştürme yolundan (`FfmpegArguments.cs`, K12) alınır. Aynı yere sabit ya da değişken kare hızı seçimi ffmpeg'in `-fps_mode` seçeneğiyle eklenir; varsayılan bugünkü davranış kalır. Argümanlar ölçüyle doğrulanır; macOS ve Linux kodlayıcıları bu makinede koşulamadığı için gerçek deneme o sistemlerde elle yapılır.

## 66. Komut Satırından Kayıt

13 Ekim 2026 · "bulduklarını later a eklersin" · EKA-P5 · Kaydedici · iş orta · öncelik 2 · karar gerekmez

- Satırlar (EKA-P5): EKA-16 Komut satırından kayıt. [Tablo](piyasa/2026-10-13/kaydedici-yakalama-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Cli/CliApp.cs, src/VidShrink.App/Program.cs, src/VidShrink.App/Recorder/RecorderSettings.cs, src/VidShrink.Ffmpeg/RecorderSession.cs
- Plan: Kayıtlı kaydedici ayarlarından arayüz açmadan kayıt isteği kuran tek bir giriş yazılır: başlat, süre, çıktı yolu. Giriş uygulamanın başlatma parametresine ya da komut satırı aracına bağlanır; hangisinin daha az kod taşıdığına işin başında bakılır. Kayıt süre sınırı dolunca (`RecorderArguments.cs:1217`) ya da durdurma işaretiyle biter. Ölçü: komut satırı argümanlarının kayıt isteğine çevrilmesi `tests/VidShrink.Tests` içinde sınanır.

## 67. Saatli Kayıt

13 Ekim 2026 · "bulduklarını later a eklersin" · EKA-P6 · Kaydedici · iş orta · öncelik 2 · karar gerekmez

- Satırlar (EKA-P6): EKA-10 Zamanlanmış kayıt. [Tablo](piyasa/2026-10-13/kaydedici-yakalama-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.App/Recorder/RecorderSettings.cs, src/VidShrink.App/Recorder/RecorderView.GeriSayim.cs, src/VidShrink.App/Recorder/RecorderView.Yakalama.cs, src/VidShrink.App/Locales/
- Plan: Kaydediciye başlangıç saati ve yineleme (bir kez, günlük, haftalık) seçimi eklenir; uygulama açıkken saat gelince kayıt bugünkü başlatma yolundan başlar, bitiş için var olan süre sınırı (`RecorderArguments.cs:1217`) kullanılır. Uygulama kapalıyken başlatma EKA-P5'in girişine ve sistemin görev zamanlayıcısına bırakılır; bu yüzden EKA-P5'ten sonra yapılır. Ölçü: sahte saatle zamanlama testleri.

## 68. Bas Konuş

13 Ekim 2026 · "bulduklarını later a eklersin" · EKA-P7 · Kaydedici · iş orta · öncelik 2 · karar gerekmez

- Satırlar (EKA-P7): EKA-09 Bas-konuş. [Tablo](piyasa/2026-10-13/kaydedici-yakalama-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.App/Recorder/RecorderHotkeys.cs, src/VidShrink.App/Recorder/RecorderView.Kisayol.cs, src/VidShrink.App/Recorder/RecorderInput.cs, src/VidShrink.Core/AudioCaptureArguments.cs, src/VidShrink.Ffmpeg/RecorderSession.cs
- Plan: Kısayol arabirimi tuşun basılışını ve bırakılışını ayrı bildirecek biçimde genişletilir; bugünkü kayıt yalnız basışı veriyor (`RecorderHotkeys.cs:184`), bırakılışı görebilen klavye kancası başka iş için var (`RecorderInput.cs:227`). Tuş bırakılınca mikrofon izine sessizlik yazılır. En sağlam yol mikrofonu sistem sesi gibi uygulama tarafında yakalamaktır; çalışan ffmpeg'e süzgeç komutu göndermek ikinci seçenektir ve önce ölçülmelidir. Ölçü `tests/VidShrink.Tests/KaydediciKisayolAtamaTests.cs` yanına yazılır; gerçek deneme Windows'ta elle yapılır.

## 69. Kamera Ana Kaynak Olarak

13 Ekim 2026 · "bulduklarını later a eklersin" · EKA-P8 · Kaydedici · iş orta · öncelik 2 · karar gerekmez

- Satırlar (EKA-P8): EKA-06 Aygıt kaynağı. [Tablo](piyasa/2026-10-13/kaydedici-yakalama-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.Ffmpeg/CaptureDevices.cs, src/VidShrink.App/Recorder/RecorderView.Hedef.cs, src/VidShrink.App/Recorder/RecorderView.Kamera.cs
- Plan: Hedef türlerine (`RecorderArguments.cs:24`) "aygıt" eklenir: web kamerası ya da yakalama kartı ekran girdisinin yerine ana görüntü olur. Windows'ta kamera girdisi zaten DirectShow ile kuruluyor (`RecorderArguments.cs:1203`); aynı kurucu ana girdi için yeniden kullanılır. Ölçü `tests/VidShrink.Tests/KaydediciHedefTests.cs` ve `KaydediciKameraTests.cs` yanına yazılır; gerçek deneme takılı bir kamerayla Windows'ta elle yapılır.

## 70. Kamera Katmanı Windows Dışında

13 Ekim 2026 · "bulduklarını later a eklersin" · EKA-P9 = EKB-P13 · Kaydedici · iş orta · öncelik 2 · karar gerekmez

- Satırlar (EKA-P9): EKA-07 Kamera katmanı Windows dışında. [Tablo](piyasa/2026-10-13/kaydedici-yakalama-dogrulanmis.md)
- Satırlar (EKB-P13): EKB-18 Kamera katmanı macOS ve Linux'ta. [Tablo](piyasa/2026-10-13/kaydedici-sunum-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.Ffmpeg/CaptureDevices.cs, src/VidShrink.App/Recorder/RecorderView.Kamera.cs
- Plan: Kamera girdisi macOS'ta `avfoundation`, Linux'ta `v4l2` ile kurulur (ffmpeg'in o sistemlerdeki aygıt girdileri) ve kamera paneli yalnız Windows koşulundan çıkar (`RecorderView.Kamera.cs:53`). Kardeş tablodaki EKB-18 ve onun paketi EKB-P13 aynı iştir; yalnız biri açılır. Argüman üretimi `tests/VidShrink.Tests/KaydediciKameraTests.cs` yanında her sistem için ölçülür; gerçek aygıt denemesi bu makinede yapılamaz, o sistemlerde elle yapılır.

## 71. Genel Kısayollar Windows Dışında

13 Ekim 2026 · "bulduklarını later a eklersin" · EKA-P10 · Kaydedici · iş orta · öncelik 2 · karar gerekmez

- Satırlar (EKA-P10): EKA-13 Genel kısayollar Windows dışında. [Tablo](piyasa/2026-10-13/kaydedici-yakalama-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.App/Recorder/RecorderHotkeys.cs, src/VidShrink.App/Recorder/RecorderView.Kisayol.cs
- Plan: Windows dışında bugün boş kısayol sürümü seçiliyor (`RecorderView.Kisayol.cs:29`, `RecorderHotkeys.cs:136`); macOS ve X11 karşılıkları yazılır: macOS'ta izin istemeyen sistem kısayol kaydı, X11'de tuş yakalama çağrısı. Wayland bu paketin dışında kalır; orada genel kısayol portal ister ve EKA-P14 ile birlikte düşünülür. Atama mantığı `tests/VidShrink.Tests/KaydediciKisayolAtamaTests.cs` içinde ölçülür; gerçek deneme o sistemlerde elle yapılır.

## 72. Sonuç Paneli Ve Paylaşım Kısayolları

13 Ekim 2026 · "bulduklarını later a eklersin" · EKB-P3 · Kaydedici · iş orta · öncelik 2 · karar gerekmez

- Satırlar (EKB-P3): EKB-22 Sonuçta Kopyala düğmesi; EKB-23 Sonuçta GIF'e çevir; EKB-26 Dosya adı şablonu; EKB-27 Kaydedicide hedef ve süre seçimi; EKB-33 Bitiş eylemi zinciri. [Tablo](piyasa/2026-10-13/kaydedici-sunum-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.App/Recorder/RecorderView.axaml, src/VidShrink.App/Recorder/RecorderView.axaml.cs, src/VidShrink.App/Recorder/RecorderView.Bitince.cs, src/VidShrink.App/Recorder/RecorderView.Paylas.cs, src/VidShrink.App/Recorder/RecorderSettings.cs, src/VidShrink.App/Share/ShareSession.cs, src/VidShrink.Core/AdlandirmaDeseni.cs, src/VidShrink.App/Locales/
- Plan: Sonuç paneline Kopyala ve GIF düğmeleri eklenir; kopyalama bitiş eylemindeki yolu, GIF oynatıcıdaki palet süzgecini kullanır (ClipExport.cs:55-63). Paylaş satırına hedef ve süre kutusu ile Sil düğmesi gelir; Küçült sekmesindeki parçalar örnek alınır (MainWindow.axaml.cs:2593-2613, :2769-2785). Bitiş eylemlerine "küçült, yükle, bağlantıyı kopyala" eklenir ve kayıt adı Küçült'teki ad deseninden kurulur; desene pencere adı ve sıra numarası yer tutucusu eklenir. Yükleme sahte sağlayıcıyla, ağa çıkmadan ölçülür (`DuzenleKaydetPaylasTests` düzeni).

## 73. En-Boy Sığdırma Ön Ayarı

13 Ekim 2026 · "bulduklarını later a eklersin" · EKB-P12 · Kaydedici · iş orta · öncelik 2 · karar gerekmez

- Satırlar (EKB-P12): EKB-19 En-boy ön ayarı. [Tablo](piyasa/2026-10-13/kaydedici-sunum-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/Editing/ClipEffects.cs, src/VidShrink.Core/Editing/ClipFilters.cs, src/VidShrink.App/Editing/EditorView.Klip.cs, src/VidShrink.App/Locales/
- Plan: Düzenleyicideki en-boy ön ayarları bugün ortadan kırpıyor (EditorView.Klip.cs:17, ClipEffects.cs:96-113). Yanına "sığdır" kipi eklenir: kare `scale` ile küçültülür, kalan alan `pad` ile seçilen düz renkle doldurulur; bu ikili dışa aktarımda zaten var (ClipFilters.cs:60-61). Çıktı boyutu ve dolgu pikseli düzenleyicinin dışa aktarım testlerinde ölçülür.

## 76. Uzun İşte Öncelik, Bildirim Ve Ses

13 Ekim 2026 · "bulduklarını later a eklersin" · AYR-P5 · Ayarlar Ve Kabuk · iş orta · öncelik 2 · karar gerekmez

- Satırlar (AYR-P5): AYR-19 macOS Ve Linux'ta İş Bitti Bildirimi; AYR-20 Bildirim Sesi; AYR-21 Süreç Önceliği Ayarı. [Tablo](piyasa/2026-10-13/ayarlar-kabuk-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.App/IsBittiBildirimi.cs, src/VidShrink.Ffmpeg/EncodeRunner.cs, src/VidShrink.Ffmpeg/FfmpegRunner.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/Locales/
- Plan: Ayarlar'a kodlama sürecinin önceliği (düşük, normal, yüksek; varsayılan bugünkü gibi normal) eklenir ve süreç başlarken uygulanır. İş bitti bildirimi macOS ve Linux'ta sistemin kendi bildirim yoluyla gösterilir; kapalı gelen bir "bitince ses çal" seçeneği ve ses dosyası seçimi eklenir. Doğrulama: öncelik için süreç başlatma testi, bildirim için işletim sistemi başına komut üreten katmanın testi; gerçek bildirim macOS ve Linux'ta bir kez elle görülür.

## 77. Kabuk Kısayolları Ve Ayar Araması

13 Ekim 2026 · "bulduklarını later a eklersin" · AYR-P6 · Ayarlar Ve Kabuk · iş orta · öncelik 2 · karar gerekmez

- Satırlar (AYR-P6): AYR-08 Ayarlarda Arama; AYR-18 Ana Pencere Kısayolları; AYR-26 Yardım Girişi; AYR-33 Bölgeler Arası Geçiş Tuşu; AYR-43 Kısayol Tablosunda Arama. [Tablo](piyasa/2026-10-13/ayarlar-kabuk-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.App/Playback/ShortcutBindings.cs, src/VidShrink.App/Playback/ShortcutTable.cs, src/VidShrink.App/MainWindow.PencereKipi.cs, src/VidShrink.App/MainWindow.axaml
- Plan: Kısayol eşlemesine oynatıcı ve düzenleyicinin yanına bir kabuk takımı eklenir: sekmeler arası geçiş, Ayarlar, dosya açma, bölgeler arası geçiş ve kullanım belgesini açan yardım tuşu; hepsi aynı düzenlenebilir tabloda görünür. Ayarlar sayfasına ve kısayol tablosuna aynı süzme kutusu konur. Bölge geçişinin varsayılan tuşu F6 olamaz, çünkü kaydedici onu bölüm işareti için tutuyor. Doğrulama: tuş eşleme ve çakışma testleri ile süzme kutusu için kart görünürlüğü testi.

## 78. Erişilebilirlik Denemesi

13 Ekim 2026 · "bulduklarını later a eklersin" · AYR-P7 · Ayarlar Ve Kabuk · iş orta · öncelik 2 · karar gerekmez

- Satırlar (AYR-P7): AYR-16 Ekran Okuyucu Denemesi; AYR-17 Klavyeyle Tam Kullanım Denemesi. [Tablo](piyasa/2026-10-13/ayarlar-kabuk-dogrulanmis.md)
- Dokunacağı yer: docs/olcumler/, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/Themes/Controls.axaml
- Plan: Her sekmede bir ana iş baştan sona önce yalnız klavyeyle, sonra ekran okuyucuyla (Windows'ta Anlatıcı ve NVDA) yapılır; sekme sırası, klavye tuzağı ve adı okunmayan denetimler tek tek yazılır. Bulgular `docs/olcumler` altına kayıt olarak girer; adı eksik denetim ve bozuk sekme sırası gibi küçük kusurlar aynı turda düzeltilir, büyükleri deftere yazılır. macOS'ta VoiceOver denemesi o makinede ayrıca yapılır.

## 79. Sessiz Kurulum Ve Uygulama Kaydı

13 Ekim 2026 · "bulduklarını later a eklersin" · DAG-P1 · Dağıtım Ve Otomasyon · iş orta · öncelik 2 · karar gerekmez

- Satırlar (DAG-P1): DAG-16 Sessiz kurulum bayrağı; DAG-17 Yüklü uygulamalar kaydı; DAG-19 Kaldırmada ayar temizliği; DAG-28 Windows'ta komut PATH'te değil. [Tablo](piyasa/2026-10-13/dagitim-otomasyon-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Setup/Program.cs, src/VidShrink.Core/Setup/SetupRunner.cs, src/VidShrink.Core/Setup/SetupText.cs, Install-VidShrink.ps1, docs/kurulum.md
- Plan: Kurucuya adı belli bir sessiz kip eklenir (`--silent`: pencere açmaz, soru sormaz, hata olunca beklemeden sıfırdan farklı kodla çıkar) ve yardım metni ile docs/kurulum.md'ye yazılır. Kurulum, Windows'un Ayarlar listesini besleyen kullanıcı düzeyi kaldırma kaydını (ad, sürüm, yayıncı, simge, kaldırma komutu) yazar, kaldırma onu siler; kaldırmaya ayar klasörünü de silen bir seçenek gelir. İsteğe bağlı ve varsayılanı kapalı bir seçenek kullanıcı PATH'ine yalnız küçük bir `bin` klasörü ekler; içinde komut satırı aracını çağıran küçük bir sarmalayıcı (örneğin tek satırlık `vidshrink.cmd`) durur, çünkü kökteki başlatıcı aynı adı taşıyor. Doğrulama: kurulum ve kaldırma testleri sahte kayıt köküyle, elle de Ayarlar listesinde görünme, sürümün doğru yazılması ve kaldırmanın iz bırakmaması.

## 80. Komut Satırı Çıktısı Ve Dönüştürme

13 Ekim 2026 · "bulduklarını later a eklersin" · DAG-P2 · Dağıtım Ve Otomasyon · iş orta · öncelik 2 · karar gerekmez

- Satırlar (DAG-P2): DAG-29 Makinece okunur ilerleme; DAG-30 Komut satırında dönüştürme; DAG-33 Ön ayarı komut satırından dışa aktarma; DAG-34 Kabuk tamamlama. [Tablo](piyasa/2026-10-13/dagitim-otomasyon-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Cli/CliRequest.cs, src/VidShrink.Cli/CliApp.cs, src/VidShrink.Core/PresetLibrary.cs, docs/cli.md, docs/cli.tr.md
- Plan: Dört ek aynı iki dosyada olduğu için tek elde yapılır. `--json` ile birlikte ilerleme satır başına bir JSON nesnesi olarak yazılır (NDJSON: her satırı ayrı okunabilen JSON akışı); `profiller` komutuna çekirdekteki hazır `Export` işlevini çağıran dışa aktarma seçeneği, bir de bash, zsh, fish ve PowerShell için Tab tamamlama betiğini basan `tamamlama` komutu gelir. `donustur` komutu Dönüştür sekmesinin argüman üretimini çağırır ve kodek kümesini arayüzdekiyle eşitler; o üretim çekirdekte değilse önce oraya taşınır (bu turda bakılmadı). Doğrulama: ayrıştırma ve çıktı testleri, `donustur` için arayüzle aynı ffmpeg argümanını üretme testi.

## 81. Windows 11 İlk Menüde Küçültme

13 Ekim 2026 · "bulduklarını later a eklersin" · DAG-P3 · Dağıtım Ve Otomasyon · iş orta · öncelik 2 · karar gerekmez

- Satırlar (DAG-P3): DAG-37 Windows 11 ilk menüsünde hızlı küçültme. [Tablo](piyasa/2026-10-13/dagitim-otomasyon-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.ShellExtension/VidShrink.ShellExtension.cpp, src/VidShrink.ShellExtension/AppxManifest.template.xml, src/VidShrink.Core/ShellIntegration.cs, src/VidShrink.Core/Setup/SetupRunner.cs, Install-VidShrink.ps1
- Plan: Kabuk eklentisi (Windows 11'in ilk sağ tık menüsüne girdi koyan C++ bileşeni) bugün tek komut döndürüyor ve alt komut kolu boş. Açma girdisinin yanına "VidShrink ile Küçült" üst girdisi eklenir: `GetFlags` `ECF_HASSUBCOMMANDS` döndürür, `EnumSubCommands` altı hedefi `ShellIntegration.QuickShrinkMenu` sırasıyla sayar ve her biri başlatıcıyı `--kucult` ile çağırır. Etiketler var olan `ShellLabels` kayıt anahtarından okunur, böylece menü dili korunur. Doğrulama: listeyi üç kurucuyla karşılaştıran mevcut teste eklenti dizisi eklenir; Windows 11'de ilk menüde alt menünün görünmesi ve bir hedefin küçültmeyi başlatması elle bakılır.

## 82. Oynatıcıdan Kare Ve Tablo Çıkarma

13 Ekim 2026 · "bulduklarını later a eklersin" · ARC-P7 = OYA-P12 · Bizde Olmayan Araçlar · iş orta · öncelik 2 · karar gerekmez

- Satırlar (ARC-P7): ARC-08 Toplu kare dışa aktarma; ARC-07 Küçük resim tablosu. [Tablo](piyasa/2026-10-13/eksik-araclar-dogrulanmis.md)
- Satırlar (OYA-P12): OYA-44 Küçük resim tablosu. [Tablo](piyasa/2026-10-13/oynatici-cekirdek-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.App/Playback/ToolsOptions.cs, src/VidShrink.App/Playback/PlayerView.Tools.cs, src/VidShrink.Core/GifPalette.cs, src/VidShrink.App/Locales/
- Plan: Oynatıcının araç komutlarına (klip ve GIF dışa aktarmanın yanına) iki komut eklenir. "Kareleri dışa aktar": her N saniyede ya da A-B aralığında kareler numaralı resim olarak yazılır. "Küçük resim tablosu": eşit aralıklı kareler ffmpeg'in tile süzgeciyle tek resme dizilir, üstüne dosya bilgisi başlığı konur. Doğrulama: kısa üretilmiş örnekte yazılan dosya sayısı beklenene eşit, tablonun boyutu satır, sütun ve kare boyutunun çarpımına eşit.

**Dalga 4 — Karar Gerekmez, Kalanlar**

## 83. Akış Kalitesi Ve Kesintisiz Çalma

13 Ekim 2026 · "bulduklarını later a eklersin" · OYB-P10 · Oynatıcı · iş büyük · öncelik 2 · karar gerekmez

- Satırlar (OYB-P10): OYB-21 Akış kalite seçimi; OYB-43 Kesintisiz çalma. [Tablo](piyasa/2026-10-13/oynatici-kitaplik-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Player/MpvEngine.cs, src/VidShrink.Player/IPlaybackEngine.cs, src/VidShrink.App/Playback/PlayerView.Tracks.cs, src/VidShrink.App/Playback/PlayerView.Kuyruk.cs
- Plan: İki adımda teslim edilir ve adımlar ayrı işçilere verilebilir. Önce akış kalitesi (orta): uyarlamalı yayının katmanlarının motorun iz listesinde nasıl göründüğü ölçülür, sonra iz menüsüne kalite seçimi eklenir (video izi değiştirme ya da `hls-bitrate` seçeneği); OYB-P1 bitmeden başlamaz. Sonra kesintisiz çalma (büyük): sıradaki dosyayı önden yükleme (`prefetch-playlist`, `gapless-audio`) kuyruğun motora verilmemesi kararına dokunur, bu yüzden önce o kararın gerekçesi (`PlayerView.Kuyruk.cs:13`) yeniden ölçülür. İki katmanlı yerel bir HLS yayını ve ardışık iki ses dosyasıyla doğrulanır.

## 84. Kesim Listesi Ve Kurgu Aktarımı

13 Ekim 2026 · "bulduklarını later a eklersin" · DZA-P10 · Düzenleyici · iş büyük · öncelik 2 · karar gerekmez

- Satırlar (DZA-P10): DZA-15 Kesim listesini dışa aktarma; DZA-16 Kesim listesini içe aktarma; DZA-17 Kurgu programına aktarma. [Tablo](piyasa/2026-10-13/duzenleyici-kesim-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/Editing/, src/VidShrink.App/Editing/EditorView.Proje.cs
- Plan: Parça listesini dış biçimlere çeviren tek bir okuyucu ve yazıcı modülü yazılır: dışa CSV, EDL (kurgu programlarının düz metin kesim listesi) ve YouTube bölüm metni; içe CSV, EDL ve CUE. Aynı modül kesilmiş çizelgeyi Premiere Pro ile DaVinci Resolve'un açtığı Final Cut XML'ine de yazar. Bölünecekse önce liste biçimleri (orta), sonra XML (orta). Doğrulama: her biçim için gidiş dönüş birim testi; XML'in hedef programda açıldığını sahip gözle dener.

## 85. Kap İçi İz İşleri

13 Ekim 2026 · "bulduklarını later a eklersin" · DZA-P11 · Düzenleyici · iş büyük · öncelik 2 · karar gerekmez

- Satırlar (DZA-P11): DZA-24 Kap üst verisini düzenleme; DZA-11 İz kaydırma; DZA-25 Başka dosyadan iz ekleme; DZA-26 İzleri ayrı dosyalara çıkarma; DZA-23 Döndürme bayrağı; DZA-44 Kapak resmi ekleme. [Tablo](piyasa/2026-10-13/duzenleyici-kesim-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/Editing/, src/VidShrink.Core/StreamMapping.cs, src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.App/Editing/EditorView.Teslim.cs
- Plan: Görüntüye dokunmadan dosyayı yeniden paketleyen (`-c copy`) tek bir argüman üreticisi yazılır; DZA-P5'in iz paneli onun arayüzü olur. Üretici iz başına dil, ad ve varsayılan bayrağını, gecikmeyi (`-itsoffset`), döndürme bayrağını (`-display_rotation`), başka dosyadan iz ve kapak resmi eklemeyi, seçili izi ayrı dosyaya çıkarmayı kurar. Bölünecekse önce üst veri, kaydırma ve döndürme; sonra iz ekleme, çıkarma ve kapak. Doğrulama: argüman birim testleri ve çıktıda ffprobe ile üst veri okuması.

## 86. Metin Konumu, Hareketi Ve Altyazı

13 Ekim 2026 · "bulduklarını later a eklersin" · DZB-P9 · Düzenleyici · iş büyük · öncelik 2 · karar gerekmez

- Satırlar (DZB-P9): DZB-20 Metin konumu; DZB-24 Metin hareketi arayüzü; DZB-23 Metin canlandırmaları; DZB-62 Altyazı içe ve dışa aktarma. [Tablo](piyasa/2026-10-13/duzenleyici-kurgu-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.App/Editing/EditorView.Metin.cs, src/VidShrink.App/Editing/EditorView.Monitor.cs, src/VidShrink.App/Editing/EditorView.axaml, src/VidShrink.Core/Editing/TextLayer.cs, src/VidShrink.Core/Editing/AssWriter.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/DuzenleyiciMetinTests.cs
- Plan: Metnin yeri panelden ve monitörde sürükleyerek verilir, hazır giriş ve çıkış hareketleri (kayarak gelme, daktilo) eklenir; model konumu ve anahtar kareyi (bir değerin zaman içinde değiştiği işaret noktası) zaten tutuyor (TextLayer.cs:92, AssWriter.cs:196-197), eksik olan arayüz. Altyazı dosyası (SRT, VTT, ASS) metin izine alınır ve geri yazılır; ffmpeg bu biçimleri .ass'e çevirebildiği için tek ayrıştırıcı yeter. Paket ikiye bölünebilir: konum ve hareket (DZB-20, DZB-24, DZB-23) ile altyazı alışverişi (DZB-62). Doğrulama: DuzenleyiciMetinTests'e konum ve hareket ölçüleri, altyazıda içe alıp geri yazma testi.

## 87. Resim Klibi, Tuval Ve Dönüşüm

13 Ekim 2026 · "bulduklarını later a eklersin" · DZB-P10 · Düzenleyici · iş büyük · öncelik 2 · karar gerekmez

- Satırlar (DZB-P10): DZB-04 Resim klibi; DZB-05 Düz renk klibi; DZB-03 Klip dönüşümü; DZB-56 Proje oranı ve tuval; DZB-49 Kare dondurma. [Tablo](piyasa/2026-10-13/duzenleyici-kurgu-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.App/Editing/EditorView.Kaynak.cs, src/VidShrink.App/Editing/EditorView.Klip.cs, src/VidShrink.Core/Editing/EditTimeline.cs, src/VidShrink.Core/Editing/ClipEffects.cs, src/VidShrink.Core/Editing/ClipFilters.cs, src/VidShrink.Core/Editing/EditLook.cs, src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.Core/Editing/EditProject.cs, tests/VidShrink.Tests/
- Plan: Resim ve düz renk, eklenirken kısa bir ara videoya çevrilir ve bildik kaynak gibi çizelgeye girer; önizleme yolu değişmez. Kare dondurma aynı yoldan gelir: oynatma başındaki kare resme çıkarılıp klip yapılır. Klibe serbest ölçek, konum ve açı, projeye de çıktı çerçevesi (9:16, 1:1, 16:9) eklenir; ikisi de ClipFilters'ın geometri zincirinden (ClipFilters.cs:38) geçer. Paket ikiye bölünebilir: resim, renk ve dondurma (DZB-04, DZB-05, DZB-49) ile dönüşüm ve tuval (DZB-03, DZB-56); doğrulama argüman testleri ve kare karşılaştırması.

## 88. İleri Renk Ve Bulanıklık

13 Ekim 2026 · "bulduklarını later a eklersin" · DZB-P13 · Düzenleyici · iş büyük · öncelik 2 · karar gerekmez

- Satırlar (DZB-P13): DZB-36 Eğriler ve renk tekerleri; DZB-38 Otomatik renk ve renk eşleme; DZB-28 Bulanıklık ve bölge gizleme. [Tablo](piyasa/2026-10-13/duzenleyici-kurgu-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/Editing/ClipEffects.cs, src/VidShrink.Core/Editing/ClipFilters.cs, src/VidShrink.Core/Editing/EditLook.cs, src/VidShrink.App/Editing/EditorView.Klip.cs, src/VidShrink.App/Editing/EditorView.Monitor.cs, src/VidShrink.App/Editing/EditorView.axaml, src/VidShrink.App/Locales/, tests/VidShrink.Tests/DuzenleyiciKlipOzellikTests.cs
- Plan: DZB-P3'ün üstüne gelir: eğriler ve üç bölgeli renk dengesi ffmpeg `curves` ve `colorbalance` süzgeçleriyle, tek tıkla düzeltme `normalize` ile yapılır. Bulanıklık `gblur` ile bütün kareye ya da monitörde çizilen dikdörtgene uygulanır (kırp, bulanıklaştır, geri bindir). İki çekimin rengini eşlemek için hazır süzgeç bilinmiyor; o yarı ayrı ölçüm ister. Paket ikiye bölünebilir: renk (DZB-36, DZB-38) ile bulanıklık (DZB-28); doğrulama argüman testleri ve kare karşılaştırması.

## 89. Anahtar Kare Ve Hız Rampası

13 Ekim 2026 · "bulduklarını later a eklersin" · DZB-P14 · Düzenleyici · iş büyük · öncelik 2 · karar gerekmez

- Satırlar (DZB-P14): DZB-25 Klip ayarlarına anahtar kare; DZB-48 Hız rampası; DZB-50 Akıcı ağır çekim; DZB-26 Yumuşatma eğrileri. [Tablo](piyasa/2026-10-13/duzenleyici-kurgu-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/Editing/ClipEffects.cs, src/VidShrink.Core/Editing/ClipFilters.cs, src/VidShrink.Core/Editing/EditLook.cs, src/VidShrink.Core/Editing/EditTimeline.cs, src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.Core/Editing/EditProject.cs, src/VidShrink.App/Editing/EditorView.Klip.cs, src/VidShrink.App/Editing/EditorTimeline.cs, tests/VidShrink.Tests/
- Plan: Klip ayarları (ses düzeyi, kırpma, konum) klip içinde zamanla değişebilsin diye değerlere anahtar kare eklenir; süzgeç zinciri sabit sayı yerine zamana bağlı ifade yazar, yumuşatma eğrisi iki kare arasındaki geçişin biçimidir. Hız rampası klibi hız dilimlerine böler (`setpts`), akıcı ağır çekim ara kareyi `minterpolate` süzgeciyle üretir. Önizleme zinciri bugün sabit değer taşıyor; zamana bağlı ifadeyi taşıyıp taşımadığı ilk iş ölçülür. Paket ikiye bölünebilir: anahtar kare (DZB-25, DZB-26) ile hız (DZB-48, DZB-50); doğrulama argüman testleri ve çıktı süresinin ölçümü.

## 90. Slayt Gösterisi Ve Sesten Video

13 Ekim 2026 · "bulduklarını later a eklersin" · ARC-P10 · Bizde Olmayan Araçlar · iş büyük · öncelik 2 · karar gerekmez

- Satırlar (ARC-P10): ARC-10 Slayt gösterisi yapıcı; ARC-20 Sesten video. [Tablo](piyasa/2026-10-13/eksik-araclar-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/Editing/, src/VidShrink.App/Editing/, src/VidShrink.Core/ConversionArguments.cs
- Plan: İkisi de video olmayan malzemeden video üretir. Sesten video ARC-P3'ün üstüne gelir: ses kaynağında Dönüştür'e "görüntü" seçeneği eklenir, kapak resmi ya da dalga biçimi (showwaves süzgeci). Slayt gösterisi düzenleyicinin üç parçasına bağlıdır: resim klibi (DZB-04), müzik izi (DZB-01) ve çapraz geçiş (DZB-16); onlar bitince kalan iş fotoğrafları seçip süre vererek çizelgeye dizen kısa bir sihirbazdır ve iş orta'ya iner. Doğrulama: çıktının süresi ses süresine ya da fotoğraf sayısı çarpı süreye eşit.

**Sahibin Kararını Bekleyenler**

## 91. Kodlama Sürerken Uykuyu Engelleme

13 Ekim 2026 · "bulduklarını later a eklersin" · SIK-P2 · Küçült · iş küçük · öncelik 1 · karar sahibin

- Satırlar (SIK-P2): SIK-06 Kodlama sürerken uykuyu engelleme. [Tablo](piyasa/2026-10-13/kucult-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.App/ (yeni uyku engelleme yardımcısı), src/VidShrink.App/ShrinkJobWindow.axaml.cs, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/QueueEndActions.cs, src/VidShrink.App/AppSettings.cs, src/VidShrink.App/Locales/
- Sahibin kararı: Kodlama sürerken bilgisayarın uykuya geçmesi varsayılan olarak mı engellensin, yoksa kapalı gelen bir ayar mı olsun?
- Plan: İlk iş başlarken sistemden uyumama isteği alınır, son iş bitince ya da iptalde geri verilir: Windows'ta `SetThreadExecutionState` çağrısı, macOS'ta `caffeinate` süreci, Linux'ta `systemd-inhibit`. Ekranın kararmasına dokunulmaz, yalnız uyku engellenir; "bitince uyut" eylemi seçiliyse istek önce bırakılır, sonra makine uyutulur. Doğrulama: isteğin alınıp bırakıldığını sayan sahte sağlayıcılı test ve Windows'ta `powercfg /requests` çıktısına elle bakış.

## 92. Tuş Gösterimi Süzgeci

13 Ekim 2026 · "bulduklarını later a eklersin" · EKB-P1 · Kaydedici · iş küçük · öncelik 1 · karar sahibin

- Satırlar (EKB-P1): EKB-11 Tuş gösterimi süzgeci ve ayarı. [Tablo](piyasa/2026-10-13/kaydedici-sunum-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.App/Recorder/RecorderInput.cs, src/VidShrink.App/Recorder/RecorderInputOverlay.cs, src/VidShrink.App/Recorder/RecorderSettings.cs, src/VidShrink.App/Recorder/RecorderView.axaml, src/VidShrink.App/Locales/
- Sahibin kararı: "Yalnız kısayolları göster" süzgeci varsayılan açık mı gelsin, yoksa bugünkü gibi her tuş mu gösterilsin.
- Plan: `Feed` içine süzgeç eklenir: açıkken yalnız Ctrl, Alt ya da Win ile basılan tuşlar ve işlev tuşları yazıya döner, düz harf ve rakam dönmez (RecorderInput.cs:87-98). Ayarlara "yalnız kısayollar" kutusu ile yazının konumu, boyutu ve süresi eklenir. `KaydediciGirdiTests` içinde süzgeç açıkken düz harfin boş, Ctrl+C'nin metin döndürdüğü ölçülür.

## 93. Pencere Konumunu Hatırlama

13 Ekim 2026 · "bulduklarını later a eklersin" · AYR-P1 · Ayarlar Ve Kabuk · iş küçük · öncelik 1 · karar sahibin

- Satırlar (AYR-P1): AYR-03 Pencere Konumunu Ve Boyutunu Hatırlama. [Tablo](piyasa/2026-10-13/ayarlar-kabuk-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs
- Sahibin kararı: Uygulama her açılışta ekranı kaplamış açılmak yerine son konumunda, boyutunda ve durumunda mı açılsın?
- Plan: Pencerenin konumu, boyutu, ekranı ve kaplama durumu kapanışta, bugün yalnız panel yüksekliğini tutan yerleşim dosyasına yazılır ve açılışta oradan okunur. Kayıtlı konum bağlı ekranların hiçbirine düşmüyorsa bugünkü açılışa dönülür. Doğrulama: pencere açmadan koşan arayüz testiyle kaydet ve geri yükle turu, bir de sökülmüş ikinci ekran durumu.

## 94. Açılış Varsayılanları

13 Ekim 2026 · "bulduklarını later a eklersin" · OYA-P5 · Oynatıcı · iş orta · öncelik 1 · karar sahibin

- Satırlar (OYA-P5): OYA-04 Ses ve altyazı dil tercihi; OYA-31 Zorunlu altyazıyı kendiliğinden gösterme; OYA-36 Devam sınırı; OYA-51 Otomatik deinterlace. [Tablo](piyasa/2026-10-13/oynatici-cekirdek-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.App/Playback/PlayerView.Tracks.cs, src/VidShrink.App/Playback/PlayerSettings.cs, src/VidShrink.App/Playback/PlaybackHistory.cs, src/VidShrink.Player/MpvEngine.cs, src/VidShrink.Player/MpvEngine.Advanced.cs
- Sahibin kararı: dosya açılırken dört varsayılan değişsin mi: tercih edilen dilde ses ve altyazı izi seçimi, zorunlu altyazının kendiliğinden açılması, devamdaki son beş dosya sınırının kalkması ve deinterlace'in otomatik olması
- Plan: Ayarlara ses ve altyazı için dil tercihi eklenir; `ApplyTrackOptions` açılışta iz listesindeki dil alanına bakıp eşleşen izi seçer, zorunlu (forced) izi `track-list/N/forced` ile tanır; tercih boşsa bugünkü davranış kalır. Devam sınırı `PlaybackHistory.ResumeRecent` (5) kaldırılır ya da kayıt tavanına (200) çekilir. Deinterlace (taramalı videodaki tarak izini giderme) anahtarı `auto` değerini de alır. Sahibin onaylamadığı satır paketten düşer; doğrulama çok dilli örnekle iz seçimi, altıncı dosyada `ResumeFor` ve taramalı örnekte `deinterlace` geri okuması testleridir.

## 95. Çift Tık Ve Basılı Tutma

13 Ekim 2026 · "bulduklarını later a eklersin" · OYA-P6 · Oynatıcı · iş orta · öncelik 1 · karar sahibin

- Satırlar (OYA-P6): OYA-02 Çift tıkla tam ekran; OYA-38 Basılı tutarak geçici hızlandırma. [Tablo](piyasa/2026-10-13/oynatici-cekirdek-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.App/Playback/PlayerView.Fare.cs, src/VidShrink.App/Playback/Keymap.cs, src/VidShrink.App/Playback/PlayerInputMap.cs, src/VidShrink.App/Playback/PlayerShortcutsPanel.axaml.cs
- Sahibin kararı: sol tık üç anlam taşısın mı: tek tık oynat ya da duraklat (bugünkü), çift tık tam ekran, basılı tutunca bırakana dek hızlı oynatma
- Plan: Bugün sol tık işini bırakışta yapıyor ve çift tık bilerek ayrı hareket sayılmıyor (PlayerView.Fare.cs:45, 88–94). `PlayerInputKind`'e çift basış ve basılı tutma türleri eklenir: ikinci tık ilk tıkın etkisini geri alıp tam ekrana geçer; basış bekleme eşiğini aşarsa hız geçici olarak yükselir, bırakınca eski hıza döner ve tık sayılmaz. Eşik süresi tema belirtecinden okunur, sürükleme eşiği korunur. Doğrulama: `FarePress` ve `FareRelease` üstünden zamanlı görünüm testleri; kararsız `SolTikOynatmayiDegistirmez` testi bu paketle birlikte ele alınır.

## 96. HDR Ton Eşleme

13 Ekim 2026 · "bulduklarını later a eklersin" · OYA-P7 · Oynatıcı · iş orta · öncelik 1 · karar sahibin

- Satırlar (OYA-P7): OYA-11 HDR videoyu SDR ekranda doğru gösterme. [Tablo](piyasa/2026-10-13/oynatici-cekirdek-dogrulanmis.md)
- Dokunacağı yer: tools/VidShrink.Bench/, docs/olcumler/, src/VidShrink.Player/MpvEngine.Advanced.cs, src/VidShrink.App/Playback/PlayerAdvancedPanel.axaml.cs
- Sahibin kararı: HDR video işlemcide ton eşlemeyle gösterilsin mi; 4K'da kare düşürürse yalnız elle açılan bir seçenek mi olsun
- Plan: Önce ölçüm: bir HDR10 ve bir HLG örnek bugünkü yolda açılıp kare kaydedilir, soluk ya da patlak görünüp görünmediği `docs/olcumler/`e yazılır. Sonra etiketli bir `vf` halkası (ffmpeg `zscale` ve `tonemap` süzgeçleri) eklenir; halka yalnız aktarım eğrisi PQ ya da HLG iken takılır (MediaDetails.cs:26–38). Süzgeçler libmpv derlemesinde yoksa paket burada durur ve sahibe döner. Doğrulama: aynı karede eşlemeli ve eşlemesiz ortalama parlaklık farkı; 1080p ve 4K'da `Stats` ile düşen kare sayısı.

## 97. Sessiz Bölümleri Atlama

13 Ekim 2026 · "bulduklarını later a eklersin" · OYA-P8 · Oynatıcı · iş orta · öncelik 1 · karar sahibin

- Satırlar (OYA-P8): OYA-37 Sessiz bölümleri atlama. [Tablo](piyasa/2026-10-13/oynatici-cekirdek-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Player/MpvEngine.Advanced.cs, src/VidShrink.Player/IPlaybackEngine.cs, src/VidShrink.App/Playback/PlayerView.axaml.cs, src/VidShrink.App/Playback/Keymap.cs
- Sahibin kararı: sessiz yerleri hızlandıran yeni bir oynatma kipi eklensin mi; hazır sunan rakip bulunamadı, istek yalnız oyla kanıtlı
- Plan: Etiketli bir `af` halkası olarak ffmpeg `silencedetect` süzgeci takılır; motor süzgecin bildirdiği sessizlik başlangıcında hızı yükseltir, bitişinde eski hıza döner. Atlamak yerine hızlandırmak ses ile görüntünün eşliğini bozmaz. Kip bir anahtarla açılır ve kapalı gelir; eşik ile en kısa sessizlik süresi ayardır. Doğrulama: ortasında bilinen uzunlukta sessizlik olan üretilmiş örnekte oynatma süresinin kısaldığı ve konuşmalı kısımda hızın değişmediği motor testi.

## 98. Devam Kapsamı Ve Kalıcı Kuyruk

13 Ekim 2026 · "bulduklarını later a eklersin" · OYB-P5 · Oynatıcı · iş orta · öncelik 1 · karar sahibin

- Satırlar (OYB-P5): OYB-08 Kuyruğun kalıcılığı; OYB-12 Devam kapsamı. [Tablo](piyasa/2026-10-13/oynatici-kitaplik-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.App/Playback/PlaybackHistory.cs, src/VidShrink.App/Playback/PlayerView.Kuyruk.cs, src/VidShrink.App/Playback/PlayerSettings.cs
- Sahibin kararı: kaldığı yerden devam son beş dosyayla sınırlı kalsın mı ve uygulama açılınca son kuyruk geri gelsin mi
- Plan: Devam sınırı tek sabittir (`PlaybackHistory.cs:18`); karar "kalksın" olursa sabit deponun sığasına (200 kayıt) çekilir ve sınırı ölçen testler birlikte güncellenir. Kuyruk için kapanışta kuyruk ve çalan öge diske yazılır, açılışta geri yüklenir; diskte artık olmayan dosyalar atılır. İki soru birbirinden bağımsızdır: biri "evet" alırsa o yarı tek başına yapılır. Kardeş tablodaki OYA-36 aynı iştir ve orada OYA-P5 paketine girmiştir; iş bir kez yapılır.

## 99. Windows Ortam Denetimi Bağlantısı

13 Ekim 2026 · "bulduklarını later a eklersin" · OYB-P6 · Oynatıcı · iş orta · öncelik 1 · karar sahibin

- Satırlar (OYB-P6): OYB-29 SMTC ve odak dışı ortam tuşları. [Tablo](piyasa/2026-10-13/oynatici-kitaplik-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Player/MpvEngine.cs, src/VidShrink.App/Playback/Keymap.cs, src/VidShrink.App/Playback/PlayerView.axaml.cs
- Sahibin kararı: libmpv'nin kendi ortam denetimi anahtarı yetmezse Windows SDK bağı eklensin mi
- Plan: İlk adım karar istemez: motor seçeneklerine `media-controls` yazılır ve gerçek pencerede Windows ses kutucuğunda görünüp görünmediği, ortam tuşlarının pencere arkadayken çalışıp çalışmadığı ölçülür. Yetmezse pencere Windows'un ortam denetimi arayüzüne doğrudan bağlanır (MPC-BE'nin yolu); bu yeni bir Windows SDK bağı demektir ve sahibin kararını ister. İki yolda da sonraki ve önceki tuşu uygulamanın kendi kuyruğuna bağlanmalıdır, çünkü kuyruk motora verilmiyor.

## 100. Kayıpsız Birleştirme Yolu

13 Ekim 2026 · "bulduklarını later a eklersin" · DZA-P6 · Düzenleyici · iş orta · öncelik 1 · karar sahibin

- Satırlar (DZA-P6): DZA-01 Kayıpsız birleştirme. [Tablo](piyasa/2026-10-13/duzenleyici-kesim-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.Core/Editing/SmartCutCodec.cs, src/VidShrink.App/Editing/EditorView.Teslim.cs, src/VidShrink.App/Locales/
- Sahibin kararı: çok kaynakta Hızlı ve Akıllı kip, kaynaklar uyumluysa yeniden kodlamak yerine kopyalasın mı (K7 her zaman yeniden kodlamayı seçmişti)
- Plan: Çok kaynak bugün kipe bakmadan yeniden kodlama yoluna gidiyor (EditExport.cs:252). Kaynaklar arası uyum denetimi eklenir: kodek, profil, çözünürlük, piksel biçimi, kare hızı, ses kodeği ve örnekleme hızı. Hepsi tutuyorsa parçalar ffmpeg'in concat girişiyle (dosyaları yeniden kodlamadan uç uca okuyan liste) kopyalanır; tutmuyorsa bugünkü yol ve notu kalır. Doğrulama: uyum denetimi birim testleri, örnek çıktıda kare sayısı ve süre.

## 101. Tüm İzleri Varsayılan Taşıma

13 Ekim 2026 · "bulduklarını later a eklersin" · DZA-P7 · Düzenleyici · iş orta · öncelik 1 · karar sahibin

- Satırlar (DZA-P7): DZA-02 Tüm izleri taşıma. [Tablo](piyasa/2026-10-13/duzenleyici-kesim-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.App/Editing/EditorView.Teslim.cs
- Sahibin kararı: teslim varsayılanı "ilk görüntü ve ilk ses" yerine "bütün izler" olsun mu
- Plan: DZA-P5'in paneli üstüne gelir: varsayılan seçim bütün ses, altyazı ve veri izleri olur. Altyazı ve ek ses izleri üç kipte de kesim aralığına göre kırpılır; Akıllı kipte her ses izi kendi parça dosyasıyla taşınır. Doğrulama: çok sesli ve altyazılı örnekte ffprobe ile iz sayısı ve süre eşitliği.

## 102. Çapraz Geçiş Teslimi

13 Ekim 2026 · "bulduklarını later a eklersin" · DZB-P6 · Düzenleyici · iş orta · öncelik 1 · karar sahibin

- Satırlar (DZB-P6): DZB-16 Çapraz geçiş teslimi; DZB-18 Geçiş çeşitleri. [Tablo](piyasa/2026-10-13/duzenleyici-kurgu-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/Editing/EditTimeline.cs, src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.Core/Editing/EditProject.cs, src/VidShrink.App/Editing/EditorTimeline.cs, src/VidShrink.App/Editing/EditorView.Klip.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/
- Sahibin kararı: Düzenleyicinin yazılı kapsamı "geçişsiz"den çıkıp iki klip arasında çapraz geçişi kapsasın mı?
- Plan: İki klibin sınırına geçiş süresi ve türü yazılır; Tam kipte görüntü ffmpeg `xfade`, ses `acrossfade` süzgeciyle birleştirilir. Tür listesi `xfade`'in hazır desenlerinden gelir (erime, silme, kaydırma, iris), ek içerik gerekmez. Geçişli kurgu Hızlı ve Akıllı kipte teslim edilemez; bugün efektlerde olduğu gibi Tam kipe düşer. Bu pakette canlı önizleme yok (DZB-P8); doğrulama argüman testi ve toplam sürenin geçiş süresi kadar kısaldığının ölçümü.

## 103. Müzik İzi

13 Ekim 2026 · "bulduklarını later a eklersin" · DZB-P7 · Düzenleyici · iş orta · öncelik 1 · karar sahibin

- Satırlar (DZB-P7): DZB-01 Müzik izi. [Tablo](piyasa/2026-10-13/duzenleyici-kurgu-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/Editing/EditTimeline.cs, src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.Core/Editing/EdlPreview.cs, src/VidShrink.Core/Editing/EditProject.cs, src/VidShrink.App/Editing/EditorTimeline.cs, src/VidShrink.App/Editing/EditorView.Kaynak.cs, src/VidShrink.Player/MpvEngine.cs, tests/VidShrink.Tests/
- Sahibin kararı: Düzenleyici tek şeritten çıkıp videonun altına ikinci bir ses izi (müzik, anlatım) alsın mı?
- Plan: Çizelgeye A2 izi eklenir; görüntüsü olmayan dosya artık reddedilmez (EditorView.Kaynak.cs:89-93), bu ize başlangıç anı ve düzeyiyle konur. Teslimde iki ses ffmpeg `amix` süzgeciyle karıştırılır. Önizlemede iki sesi birlikte duymanın yolu ölçülmedi: mpv tek ses izi çalar, karıştırma `lavfi-complex` ister ve bunun `edl://` ile çalışması ilk iş denenir. Doğrulama: teslim dosyasında iki sesin de duyulduğunu gösteren düzey ölçümü ve proje dosyasını yazıp geri okuma testi.

## 105. Uygulama Başına Ses

13 Ekim 2026 · "bulduklarını later a eklersin" · EKA-P2 · Kaydedici · iş orta · öncelik 1 · karar sahibin

- Satırlar (EKA-P2): EKA-01 Uygulama başına ses. [Tablo](piyasa/2026-10-13/kaydedici-yakalama-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Ffmpeg/WasapiLoopbackCapture.cs, src/VidShrink.Ffmpeg/SystemAudioCapture.cs, src/VidShrink.Ffmpeg/LoopbackFeed.cs, src/VidShrink.App/Recorder/RecorderView.Ses.cs
- Sahibin kararı: Uygulama başına ses yeni bir Windows ses arabirimi ister ve eski Windows 10 yapılarında çalışmaz; orada ve Windows dışında seçenek gizlensin mi, kapalı mı görünsün.
- Plan: Bugünkü sistem sesi kaynağının yanına süreç geri döngüsü kaynağı eklenir: `ActivateAudioInterfaceAsync` çağrısı `AUDIOCLIENT_PROCESS_LOOPBACK_PARAMS` ile seçilen programın sesini verir, ham ses aynı adlandırılmış borudan ffmpeg'e gider. Ses panelinde ses çalan programların listesi çıkar; Windows sürümü yetmiyorsa seçenek sahibin kararına göre gizlenir ya da kapalı görünür. Ölçü `ISystemAudioCapture` (`SystemAudioCapture.cs:24`) için sahte kaynakla yazılır; gerçek deneme iki program aynı anda ses çalarken elle yapılır.

## 106. Kayıt Çerçevesi

13 Ekim 2026 · "bulduklarını later a eklersin" · EKB-P8 · Kaydedici · iş orta · öncelik 1 · karar sahibin

- Satırlar (EKB-P8): EKB-02 Kayıt çerçevesi. [Tablo](piyasa/2026-10-13/kaydedici-sunum-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/, src/VidShrink.App/Recorder/RecorderView.axaml, src/VidShrink.App/Recorder/RecorderView.axaml.cs, src/VidShrink.App/Recorder/RecorderSettings.cs, src/VidShrink.App/Locales/
- Sahibin kararı: Hazır arka plan görselleri pakete girsin mi, yoksa düz renk ve renk geçişiyle mi yetinilsin.
- Plan: Biten kayıt ikinci bir ffmpeg geçişiyle arka planın üstüne oturtulur: `color` ya da `gradients` kaynağı, `scale`, yuvarlak köşe maskesi, gölge için bulanık kopya ve `overlay`. Sonuç panelinde "Çerçevele" düğmesi ile dolgu, köşe ve arka plan seçimi olur. Çıktının boyutu ve köşe pikselinin arka plan renginde olduğu lavfi ile ölçülür.

## 107. Winget, Scoop Ve Chocolatey Paketleri

13 Ekim 2026 · "bulduklarını later a eklersin" · DAG-P5 · Dağıtım Ve Otomasyon · iş orta · öncelik 1 · karar sahibin

- Satırlar (DAG-P5): DAG-01 winget paketi; DAG-08 Scoop paketi; DAG-12 Chocolatey paketi. [Tablo](piyasa/2026-10-13/dagitim-otomasyon-dogrulanmis.md)
- Dokunacağı yer: .github/workflows/release.yml, yeni packaging/winget/, packaging/scoop/, packaging/chocolatey/; depo dışında microsoft/winget-pkgs PR'ı ve kova deposu
- Sahibin kararı: Windows paket dizinlerinde yayın yapılsın mı; winget için PR sahibin hesabından açılır ve Microsoft katkı sözleşmesi onaylanır, Scoop için kova deposu açılır, Chocolatey için hesap gerekir.
- Plan: DAG-P1'den sonra yapılır: winget etkileşimsiz kurulum ister ve kurulu sürümü kaldırma kaydından tanır. winget bildirimi (paketin adını, sürümünü, kurucu adresini ve sha256'sını taşıyan YAML dosyaları) `VidShrink-Setup.exe` için yazılır; Scoop için tek JSON, Chocolatey için paket tanımı ve kurulum betiği hazırlanır. `release.yml` her sürümde üç bildirimin sürüm ve özet alanını tazeler. Doğrulama: bildirim doğrulama komutu ve temiz bir Windows'ta kurma, yükseltme ve kaldırma.

## 108. Windows Kod İmzası

13 Ekim 2026 · "bulduklarını later a eklersin" · DAG-P6 · Dağıtım Ve Otomasyon · iş orta · öncelik 1 · karar sahibin

- Satırlar (DAG-P6): DAG-02 Windows kod imzası. [Tablo](piyasa/2026-10-13/dagitim-otomasyon-dogrulanmis.md)
- Dokunacağı yer: .github/workflows/release.yml, docs/imzalama.md, README.md, src/VidShrink.ShellExtension/AppxManifest.template.xml
- Sahibin kararı: SignPath Foundation'a başvurulsun mu (ücretsiz; başvuruyu ve her imza onayını sahip yapar), yoksa ücretli sertifika mı alınsın.
- Plan: Başvurunun onayı dışarıda beklediği için karar erken verilmeli; öteki paketler bu sırada yürür. Onaydan sonra `release.yml`'e imza adımı girer: kurucu, başlatıcı, uygulama, komut satırı aracı ve kabuk eklentisi, x64 ve ARM64 için; sha256 listeleri imzadan sonra üretilir. README'deki "başvuru sürüyor" cümlesi gerçek duruma çekilir. Doğrulama: yayın dosyalarında imza denetimi geçerli döner; temiz makinede SmartScreen ve Akıllı Uygulama Denetimi davranışı elle bakılır.

## 109. Geçiş Önizleme Yolu

13 Ekim 2026 · "bulduklarını later a eklersin" · DZB-P8 · Düzenleyici · iş büyük · öncelik 1 · karar sahibin

- Satırlar (DZB-P8): DZB-17 Geçiş önizlemesi. [Tablo](piyasa/2026-10-13/duzenleyici-kurgu-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/Editing/EdlPreview.cs, src/VidShrink.Core/Editing/EditLook.cs, src/VidShrink.App/Editing/EditorView.Monitor.cs, src/VidShrink.Player/MpvEngine.cs, tools/VidShrink.Bench
- Sahibin kararı: Geçişin ve bindirmenin canlı önizlemesi hangi yoldan yapılsın: libmpv süzgeç grafiği, geçiş bölgesini kısa ön-işleme, yoksa ayrı bir bileşim motoru (MLT)?
- Plan: Önerilen yol kısa ön-işleme: iki klibin bindiği birkaç saniye arka planda küçük bir ara dosyaya işlenir ve önizleme listesine ayrı parça olarak girer; var olan `edl://` önizlemesi bozulmaz, yeni bağımlılık gelmez. Karardan önce iki ölçüm yapılır: 1080p'de bir saniyelik geçişin hazırlanma süresi ve `lavfi-complex` ile `edl://` birlikteliği. MLT (Shotcut ve Kdenlive'ın kullandığı açık kaynak bileşim motoru) yeni ikili dosya dağıtımı demektir, o yüzden son seçenek. Doğrulama: aynı andaki önizleme karesi ile teslim karesinin karşılaştırması.

## 110. İkinci Video İzi Ve Yeşil Perde

13 Ekim 2026 · "bulduklarını later a eklersin" · DZB-P12 · Düzenleyici · iş büyük · öncelik 1 · karar sahibin

- Satırlar (DZB-P12): DZB-02 İkinci video izi; DZB-29 Yeşil perde. [Tablo](piyasa/2026-10-13/duzenleyici-kurgu-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/Editing/EditTimeline.cs, src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.Core/Editing/EdlPreview.cs, src/VidShrink.Core/Editing/EditProject.cs, src/VidShrink.App/Editing/EditorTimeline.cs, src/VidShrink.App/Editing/EditorView.Klip.cs, tests/VidShrink.Tests/
- Sahibin kararı: Düzenleyici tek şeritten çıkıp üstte ikinci bir video izi (resim içinde resim) alsın mı?
- Plan: Çizelgeye V2 izi eklenir; üstteki klip başlangıç anı, boyutu ve konumuyla tutulur, teslimde ffmpeg `overlay` süzgeciyle bindirilir. Yeşil perde aynı klibe tek seçenektir: bindirmeden önce `chromakey` uygulanır (kaydedicideki hazır sabit: src/VidShrink.Core/RecorderArguments.cs:928). Canlı önizleme DZB-P8'de seçilen yoldan gelir; o karar verilmeden bu paket açılmaz. Doğrulama: argüman testi ve bindirilen bölgenin teslim karesinde ölçülmesi.

## 111. Mac Sistem Sesi Yardımcısı

13 Ekim 2026 · "bulduklarını later a eklersin" · EKA-P12 · Kaydedici · iş büyük · öncelik 1 · karar sahibin

- Satırlar (EKA-P12): EKA-03 macOS'ta sistem sesi. [Tablo](piyasa/2026-10-13/kaydedici-yakalama-dogrulanmis.md)
- Dokunacağı yer: tools/ altında yeni yardımcı proje, src/VidShrink.Ffmpeg/SystemAudioCapture.cs, src/VidShrink.Ffmpeg/CaptureDevices.cs, src/VidShrink.Core/LoopbackAudio.cs, macOS kurucusu
- Sahibin kararı: macOS için ScreenCaptureKit kullanan yerel bir yardımcı ikili yazılıp pakete eklensin mi.
- Plan: ScreenCaptureKit (macOS'un ekranı, tek pencereyi ve sistem sesini veren yakalama arabirimi) ffmpeg'de olmadığı için küçük bir yardımcı ikili yazılır; ham sesi verir, uygulama onu Windows'taki düzenle ffmpeg'e aktarır (`SystemAudioCapture.cs:36` bugün Windows dışında kaynak açmıyor). Sanal ses aygıtı yolu (`CaptureDevices.cs:59`) yedek olarak kalır. Bu makinede koşulamaz: derleme CI'ın macOS koşucusunda, ses denemesi gerçek Mac'te elle yapılır.

## 112. Mac Pencere Yakalama

13 Ekim 2026 · "bulduklarını later a eklersin" · EKA-P13 · Kaydedici · iş büyük · öncelik 1 · karar sahibin

- Satırlar (EKA-P13): EKA-04 macOS'ta pencere yakalama. [Tablo](piyasa/2026-10-13/kaydedici-yakalama-dogrulanmis.md)
- Dokunacağı yer: tools/ altındaki yardımcı proje, src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.App/Recorder/RecorderWindows.cs, src/VidShrink.App/Recorder/RecorderWindowsMac.cs
- Sahibin kararı: macOS'ta pencere yakalama EKA-P12'deki yardımcı ikiliye bağlansın mı; o karar hayır çıkarsa bu paket düşer.
- Plan: EKA-P12'nin yardımcısı seçilen pencerenin karelerini de verir ve ffmpeg onları ham görüntü girdisi olarak okur; ekran kırpması (`RecorderWindows.cs:93`, `RecorderArguments.cs:1134`) yerini bu kaynağa bırakır. Argüman üretimi `tests/VidShrink.Tests/KaydediciPencereTests.cs` yanında ölçülür. Pencere taşınınca ve üstü örtülünce kaydın bozulmadığı gerçek Mac'te elle denenir; EKA-P12 bitmeden başlamaz.

## 113. Wayland Yakalama Yolu

13 Ekim 2026 · "bulduklarını later a eklersin" · EKA-P14 · Kaydedici · iş büyük · öncelik 1 · karar sahibin

- Satırlar (EKA-P14): EKA-02 Wayland'da yakalama. [Tablo](piyasa/2026-10-13/kaydedici-yakalama-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.App/Recorder/RecorderWindowsX11.cs, src/VidShrink.App/Recorder/RecorderView.Secici.cs, Linux kurucusu
- Sahibin kararı: Wayland için PipeWire portalını konuşan bir yardımcı ikili ya da yeni bağımlılık eklensin mi, yoksa yalnız açık bir uyarıyla yetinilsin mi.
- Plan: Wayland oturumunda (`RecorderWindowsX11.cs:22` bunu zaten anlıyor) ekran, kullanıcının onay verdiği portal üzerinden istenir ve gelen PipeWire akışı bir yardımcıyla ffmpeg'e boru olarak verilir; ffmpeg'in kendi PipeWire girişi yok. Karar "yalnız uyarı" çıkarsa iş küçülür: ekran ve bölge kaydı için de pencere seçicideki gibi açık hata gösterilir. Deneme Wayland ile açılan gerçek bir Linux masaüstünde elle yapılır.

## 114. Otomatik Ve Canlı Yakınlaştırma

13 Ekim 2026 · "bulduklarını later a eklersin" · EKB-P9 · Kaydedici · iş büyük · öncelik 1 · karar sahibin

- Satırlar (EKB-P9): EKB-01 Otomatik yakınlaştırma; EKB-13 Canlı yakınlaştırma. [Tablo](piyasa/2026-10-13/kaydedici-sunum-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.App/Recorder/RecorderInput.cs, src/VidShrink.App/Recorder/RecorderMagnifier.cs, src/VidShrink.Core/, src/VidShrink.App/Editing/, src/VidShrink.App/Locales/
- Sahibin kararı: Yakınlaştırma kayıttan sonra mı üretilsin (imleç ve tıklama verisi videodan ayrı kaydedilir), kayıt sırasında canlı mı; ikisinden biri seçilir.
- Plan: Kayıt sonrası yol seçilirse fare kancası imleç konumunu ve tıklamaları zaman damgasıyla yan dosyaya yazar; kayıt bitince bu izden yakınlaşma bölgeleri çıkarılır ve `zoompan` ya da zamana bağlı `crop` ile `scale` zinciriyle yeni dosya üretilir, bölgeler düzenleyicide elle düzeltilir. Canlı yol seçilirse büyüteç merceği yerine kadrajın tamamı kısayolla yaklaşır. İz dosyası ve bölge planı saf fonksiyon olarak test edilir, çıktı karesi lavfi ile ölçülür.

## 115. İmleç Cilası Ve Hareket Bulanıklığı

13 Ekim 2026 · "bulduklarını later a eklersin" · EKB-P10 · Kaydedici · iş büyük · öncelik 1 · karar sahibin

- Satırlar (EKB-P10): EKB-03 İmleç cilası; EKB-31 Hareket bulanıklığı. [Tablo](piyasa/2026-10-13/kaydedici-sunum-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.App/Recorder/RecorderInput.cs, src/VidShrink.Core/, src/VidShrink.App/Editing/, src/VidShrink.App/Locales/
- Sahibin kararı: İmleç kayda yakılmak yerine ayrı veri olarak tutulsun mu; EKB-P9'da kayıt sonrası yol seçilmeden bu paket başlamaz.
- Plan: Kayıt imleçsiz alınır, imleç izi EKB-P9'un yan dosyasından okunur. İz yumuşatılır, imleç seçilen boyutta yeniden çizilip `overlay` ile bindirilir; hızlı geçişlere kare karıştırma (`tmix`) ile bulanıklık eklenir. Yumuşatma saf fonksiyon olarak, imlecin karedeki yeri lavfi ile ölçülür.

## 117. Taşınabilir Kip Ve Güncelleme Kanalı

13 Ekim 2026 · "bulduklarını later a eklersin" · AYR-P8 · Ayarlar Ve Kabuk · iş büyük · öncelik 1 · karar sahibin

- Satırlar (AYR-P8): AYR-05 Taşınabilir Kip; AYR-09 Güncelleme Kanalı Seçimi. [Tablo](piyasa/2026-10-13/ayarlar-kabuk-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/UpdateCheck.cs, src/VidShrink.App/MainWindow.axaml, .github/workflows/, docs/kurulum.md
- Sahibin kararı: Yayına iki yeni biçim eklensin mi: ayarını yanında taşıyan kurulumsuz paket ve gönüllülere açık ön sürüm kanalı?
- Plan: Önce AYR-05: exe'nin yanındaki bir işaret dosyası ayar ve günlük yolunu uygulama klasörüne çevirir; bu kipte güncellemenin nasıl davranacağı sahibin kararına göre kurulur. Sonra AYR-09: Güncelleme kartına kararlı ve ön sürüm seçimi eklenir, denetim seçilen kanalın yayınlarına bakar. İki satır dağıtım tablosundaki DAG-10 ve DAG-24 ile aynı iştir; orada DAG-24 kendi paketinde, DAG-10 ise DAG-11 ve DAG-20 ile aynı pakette duruyor, birleştirmede bu paketlerle tek işe indirilmelidir. Doğrulama: işaret dosyasıyla ayar yolunun testi ve kanal seçiminin sürüm karşılaştırma testi.

## 118. Flathub Ve AppImage Paketleri

13 Ekim 2026 · "bulduklarını later a eklersin" · DAG-P8 · Dağıtım Ve Otomasyon · iş büyük · öncelik 1 · karar sahibin

- Satırlar (DAG-P8): DAG-04 Flathub paketi; DAG-09 AppImage. [Tablo](piyasa/2026-10-13/dagitim-otomasyon-dogrulanmis.md)
- Dokunacağı yer: yeni packaging/flatpak/, packaging/appimage/, .github/workflows/release.yml, src/VidShrink.Core/UpdateCheck.cs, install-vidshrink.sh
- Sahibin kararı: Flathub'a uygulama başvurusu yapılsın mı (sahibin hesabından; ffmpeg ve libmpv paketin içinde gelir) ve yayına AppImage eklensin mi.
- Plan: Flatpak bildirimi (uygulamanın nasıl derleneceğini ve hangi izinlerle çalışacağını söyleyen dosya) yazılır; ffmpeg ve libmpv Flathub çalışma ortamından ya da bildirimde kaynaktan derlenerek gelir, uygulama kimliği, tanıtım dosyası ve simge eklenir. Paketin içinde kendini güncelleme kapatılır, güncellemeyi Flathub yapar. AppImage aynı yayın çıktısından `release.yml` içinde üretilir ve sha256 listesine girer. Doğrulama: CI'da paketi kurup küçültme ve oynatma denemesi; AppImage için temiz bir Ubuntu koşucusunda açılış.

## 120. Tepsiye Küçültme

13 Ekim 2026 · "bulduklarını later a eklersin" · AYR-P9 · Ayarlar Ve Kabuk · iş küçük · öncelik 2 · karar sahibin

- Satırlar (AYR-P9): AYR-22 Tepsiye Küçültme. [Tablo](piyasa/2026-10-13/ayarlar-kabuk-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.App/Recorder/RecorderTray.cs, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/MainWindow.axaml
- Sahibin kararı: Ana pencere için kapalı gelen bir "tepsiye küçült" seçeneği eklensin mi; eklenecekse kapatma düğmesi de tepsiye mi gitsin?
- Plan: Kaydedicinin tepsi simgesi kodu ana pencerenin de kullanabileceği ortak bir parçaya çıkarılır. Seçenek açıkken küçültülen pencere görev çubuğundan kalkar, tepsi simgesine tıklanınca geri gelir; kuyruk ilerlemesi simgenin ipucunda yazar. Doğrulama: pencere durumu geçişlerinin testi; tepsi simgesi Windows'ta bir kez elle görülür.

## 121. Kuyruğun Saklanması Ve Geri Yüklenmesi

13 Ekim 2026 · "bulduklarını later a eklersin" · SIK-P9 · Küçült · iş orta · öncelik 2 · karar sahibin

- Satırlar (SIK-P9): SIK-05 Kuyruğun saklanması. [Tablo](piyasa/2026-10-13/kucult-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/JobPump.cs, src/VidShrink.Core/ (yeni kuyruk deposu), src/VidShrink.App/ShrinkJobWindow.axaml.cs, src/VidShrink.App/Locales/
- Sahibin kararı: Açılışta yarım kalan kuyruk kendiliğinden mi geri gelsin, yoksa kullanıcıya mı sorulsun?
- Plan: Bekleyen ve koşan işler her değişiklikte kullanıcı veri klasöründeki bir JSON dosyasına yazılır; önce geçici dosyaya yazılıp sonra yer değiştirilir ki yarım yazılmış dosya kuyruğu bozmasın. Açılışta dosya okunur, kaynağı artık olmayan işler elenir, koşarken kesilen iş baştan sıraya girer. Doğrulama: yaz, süreci kes, yeniden oku testi ve bozuk dosyada uygulamanın boş kuyrukla açıldığını ölçen test.

## 122. Biten Çıktının Kalite Ölçümü

13 Ekim 2026 · "bulduklarını later a eklersin" · SIK-P10 · Küçült · iş orta · öncelik 2 · karar sahibin

- Satırlar (SIK-P10): SIK-14 Çıktının gerçek kalite puanı. [Tablo](piyasa/2026-10-13/kucult-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Ffmpeg/QualityMeter.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/ShrinkJobWindow.axaml.cs, src/VidShrink.App/Locales/
- Sahibin kararı: Tam kalite ölçümü her işi uzatır; varsayılan kapalı bir seçenek olarak arayüze girsin mi?
- Plan: Komut satırının `--vmaf` ile yaptığı ölçüm (`src/VidShrink.Cli/CliApp.cs:533`) arayüze taşınır: iş bitince çıktının tamamı kaynakla karşılaştırılır, VMAF puanı (görüntünün kaynağa benzerliğini 0 ile 100 arasında puanlayan ölçüt) sonuç satırında gösterilir ve iş kaydına yazılır. Ölçüm ilerleme çubuğunda ayrı bir aşama olarak görünür ve iptal edilebilir. Doğrulama: arayüzün gösterdiği puanın aynı dosyada komut satırının verdiği puanla aynı olduğunu ölçen test.

## 123. VVC Kodlama Desteği

13 Ekim 2026 · "bulduklarını later a eklersin" · SIK-P12 · Küçült · iş orta · öncelik 2 · karar sahibin

- Satırlar (SIK-P12): SIK-16 VVC (H.266) kodlama. [Tablo](piyasa/2026-10-13/kucult-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/PlanParser.cs, src/VidShrink.Core/FfmpegArguments.cs, src/VidShrink.Core/PlanCalculator.cs, src/VidShrink.App/MainWindow.axaml (kodek seçimi), src/VidShrink.App/Locales/
- Sahibin kararı: libvvenc içeren bir ffmpeg derlemesi dağıtılsın mı ve oynatma desteği bu kadar darken VVC kodek listesine girsin mi?
- Plan: Önce dağıtılan ffmpeg'de libvvenc (VVC kodlayan açık kaynak kitaplık) var mı diye yoklanır; yoksa iş derleme kararında bekler. Varsa kodek izin listesine eklenir (`src/VidShrink.Core/PlanParser.cs:13`), hız ön ayarları ve boyut verimi ölçülerek yazılır, kodek seçiminde çoğu aygıtta oynamadığı uyarısıyla gösterilir. Doğrulama: argüman testi ve ölçüm aracıyla hedef boyut bandının tuttuğunun ölçülmesi. Dönüştür sekmesindeki DON-33 ile aynı iştir.

## 125. Kayan Sonuç Kartı

13 Ekim 2026 · "bulduklarını later a eklersin" · EKB-P14 · Kaydedici · iş orta · öncelik 2 · karar sahibin

- Satırlar (EKB-P14): EKB-24 Kayan sonuç kartı. [Tablo](piyasa/2026-10-13/kaydedici-sunum-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.App/Recorder/, src/VidShrink.App/Recorder/RecorderView.axaml.cs, src/VidShrink.App/Recorder/RecorderSettings.cs, src/VidShrink.App/Locales/
- Sahibin kararı: Kayıt bitince ekran köşesinde kendiliğinden yeni bir pencere açılsın mı ve bu varsayılan açık mı gelsin.
- Plan: Kayıt bitince köşede küçük resimli, birkaç saniye sonra kaybolan bir kart açılır; dosya karttan başka uygulamaya sürüklenir (Avalonia'nın sürükle-bırak yolu, dosya verisiyle). Kart `CaptureAffinity` ile sonraki kayıttan çıkarılır. Kartın açılıp kapanması ve sürüklenen verinin dosya yolunu taşıdığı başsız pencere testiyle ölçülür.

## 126. Kayıtta Bildirim Ve Simge Gizleme

13 Ekim 2026 · "bulduklarını later a eklersin" · EKB-P15 · Kaydedici · iş orta · öncelik 2 · karar sahibin

- Satırlar (EKB-P15): EKB-20 Bildirim susturma; EKB-21 Masaüstü simgelerini gizleme. [Tablo](piyasa/2026-10-13/kaydedici-sunum-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.App/Recorder/, src/VidShrink.App/Recorder/RecorderSettings.cs, src/VidShrink.App/Recorder/RecorderView.axaml, src/VidShrink.App/Locales/
- Sahibin kararı: Uygulama kayıt sırasında sistemin bildirim ayarına ve masaüstü simgelerine dokunsun mu.
- Plan: Kayıt başlarken bildirimler susturulur ve masaüstü simgeleri gizlenir, bitince eski durum geri yüklenir; eski durum diske yazılır ki çökmeden sonra açılışta geri alınsın. Windows'ta bildirim susturmanın belgeli yolu doğrulanmadı; bulunamazsa EKB-20 yapılmaz, yalnız simge gizleme teslim edilir. EKB-20 kardeş tablodaki EKA-27 ile aynı iştir. Geri yükleme sahte sistem katmanıyla, çökme senaryosu dahil ölçülür.

## 127. Çeviri Katkısı Ve Tamlık İşareti

13 Ekim 2026 · "bulduklarını later a eklersin" · AYR-P10 · Ayarlar Ve Kabuk · iş orta · öncelik 2 · karar sahibin

- Satırlar (AYR-P10): AYR-04 Çeviri Katkı Platformu; AYR-25 Bakımsız Çeviriyi İşaretleme. [Tablo](piyasa/2026-10-13/ayarlar-kabuk-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.App/Localization/Strings.cs, src/VidShrink.App/Locales/, README.md, .github/workflows/
- Sahibin kararı: 42 dil sözü nasıl sunulsun: gözden geçmemiş diller listede işaretlensin mi ve bir çeviri sitesinde proje hesabı açılsın mı?
- Plan: Önce küçük adım olan AYR-25: insan gözünden geçmiş diller bir listede tutulur, öteki diller dil seçicide işaretlenir. Sonra AYR-04: sahibin seçtiği çeviri sitesi (Weblate, Crowdin ya da Transifex) depodaki `Locales` klasörüne bağlanır ve README'deki katkı yolu oraya çevrilir. Doğrulama: işaretli dil listesinin testi ve siteden gelen ilk deneme çevirisinin dizgi tamlığı testinden geçmesi.

## 131. macOS İzin Akışı

13 Ekim 2026 · "bulduklarını later a eklersin" · PLT-P4 · Platform Eşitliği · iş orta · öncelik 2 · karar sahibin

- Satırlar (PLT-P4): PLT-04 macOS izin akışı. [Tablo](piyasa/2026-10-13/platform-esitligi-dogrulanmis.md)
- Dokunacağı yer: macos-app-bundle.sh, src/VidShrink.App/Recorder/, src/VidShrink.App/Locales/
- Sahibin kararı: pakete izin açıklamaları eklenip kayıttan önce izin yoklaması gösterilsin mi
- Plan: Paketin Info.plist dosyasına (macOS paketinin kimlik ve izin bildirimi) mikrofon açıklaması eklenir; kamera paketi açılırsa kamera açıklaması da buraya gelir. Kayıt başlamadan Ekran Kaydı izni yoklanır, eksikse kayıt başlamaz ve Sistem Ayarları'nın ilgili sayfasına götüren hata satırı çıkar. EKA-P9, EKA-P12 ve EKA-P13'ün izin anahtarları da bu pakette toplanır. Paket içeriği testle, izin akışı yalnız gerçek Mac'te doğrulanır.

## 132. macOS Kapanışta Sessiz Güncelleme

13 Ekim 2026 · "bulduklarını later a eklersin" · PLT-P5 · Platform Eşitliği · iş orta · öncelik 2 · karar sahibin

- Satırlar (PLT-P5): PLT-21 macOS'ta otomatik sessiz güncelleme. [Tablo](piyasa/2026-10-13/platform-esitligi-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.App/MainWindow.KapanistaGuncelleme.cs, src/VidShrink.Core/UpdateCheck.cs, src/VidShrink.Core/UpdateHealth.cs
- Sahibin kararı: macOS'ta otomatik güncelleme varsayılan olarak açılsın mı
- Plan: Elle Yükle'nin kullandığı paket takası, `MacUpdate.CanSwap` doğruysa kapanış yoluna da bağlanır: sahne açılış bittikten sonra sessiz iner, çıkışta takas edilir. Her düşüş `UpdateHealth` sayacına yazılır; sayılmayan sessiz yol eklenmez. Düz kurulumda ve salt okunur yola taşınmış pakette kapalı kalır; Sparkle eklenmez. Birim testleri ve gerçek Mac'te bir sürüm atlamasıyla doğrulanır.

## 133. Linux'ta Kendini Güncelleme

13 Ekim 2026 · "bulduklarını later a eklersin" · PLT-P6 · Platform Eşitliği · iş orta · öncelik 2 · karar sahibin

- Satırlar (PLT-P6): PLT-20 Linux'ta kendini güncelleme. [Tablo](piyasa/2026-10-13/platform-esitligi-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/UpdateCheck.cs, src/VidShrink.Core/InPlaceUpdate.cs, src/VidShrink.App/YerindeGuncelleme.cs, .github/workflows/release.yml, docs/kurulum.md
- Sahibin kararı: Linux'ta bilerek kapalı tutulan uygulama içi güncelleme açılsın mı
- Plan: Kullanıcıya ait `~/.local/share/vidshrink` kurulumunda yerinde takas açılır; yazılamayan yolda kapalı kalır ve bugünkü gibi yalnız haber verilir. Linux için güncelleme paketi bugün üretilmiyor (release.yml:492-500); ya yayına Linux paketi eklenir ya da uygulama arşivi kullanılır. DAG-25 ile aynı iş, DAG paketlerinde yeri yok; DAG-P8 (Flatpak ya da AppImage) onaylanırsa güncellemeyi mağaza üstlenir ve bu paket yalnız betikle kurulanlara kalır. Takas, geri alma ve düşüş sayımı testle ölçülür.

## 134. VideoToolbox Hızlı Kip Kapısı

13 Ekim 2026 · "bulduklarını later a eklersin" · PLT-P7 · Platform Eşitliği · iş orta · öncelik 2 · karar sahibin

- Satırlar (PLT-P7): PLT-37 macOS'ta VideoToolbox'ın otomatik sıraya girmesi. [Tablo](piyasa/2026-10-13/platform-esitligi-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/CodecModel.cs, src/VidShrink.Core/PlanCalculator.cs, tools/VidShrink.Bench, docs/olcumler/videotoolbox-hizli.md
- Sahibin kararı: ölçüm için Mac ayrılsın mı ve kapıdan geçerse Hızlı kipin Mac'teki varsayılan kodlayıcısı değişsin mi
- Plan: Önceki ölçüm beş ölçütün ikisinden kaldı (docs/olcumler/videotoolbox-hizli.md:60, 62); kapı değişmeden VideoToolbox'a kalite ölçeği eklenir ve ölçüm gerçek Mac'te yinelenir. Geçerse `hevc_videotoolbox` yalnız macOS'ta ve yalnız Hızlı kipte otomatik sıraya girer; geçmezse satır kapanır. Rapora giren her sayı Bench'ten (ölçüm aracımız) çıkar.

## 135. Linux Ölçüm Turu: VAAPI Ve Sistem Sesi

13 Ekim 2026 · "bulduklarını later a eklersin" · PLT-P8 · Platform Eşitliği · iş orta · öncelik 2 · karar sahibin

- Satırlar (PLT-P8): PLT-38 Linux'ta VAAPI ölçümü; PLT-43 Linux'ta sistem sesi ölçümü. [Tablo](piyasa/2026-10-13/platform-esitligi-dogrulanmis.md)
- Dokunacağı yer: tools/VidShrink.Bench, docs/olcumler/, src/VidShrink.Core/CodecModel.cs, src/VidShrink.Ffmpeg/CaptureDevices.cs, README.md
- Sahibin kararı: ölçüm için Intel ya da AMD ekran kartlı gerçek bir Linux makinesi ayrılsın mı
- Plan: Aynı Linux makinesinde iki ölçüm yapılır: VAAPI kolu açık seçimle koşturulup hız ve kalite Bench'le ölçülür; pulse `.monitor` kaynağından kısa kayıt alınıp ses seviyesi ölçülür. Sonuç, VAAPI'nin otomatik sıraya alınıp alınmayacağını ve README'deki "sistem sesi yalnız Windows'ta" cümlesini belirler. Defterdeki VAAPI satırı bu turla kapanır.

## 136. Wayland'da Genel Kısayol

13 Ekim 2026 · "bulduklarını later a eklersin" · PLT-P9 · Platform Eşitliği · iş orta · öncelik 2 · karar sahibin

- Satırlar (PLT-P9): PLT-08 Linux'ta genel kısayol. [Tablo](piyasa/2026-10-13/platform-esitligi-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.App/Recorder/RecorderView.Kisayol.cs, src/VidShrink.App/Recorder/
- Sahibin kararı: GlobalShortcuts portalı için D-Bus istemcisi bağımlılığı eklensin mi
- Plan: X11 kolu EKA-P10 içinde; bu paket yalnız Wayland kolunu kapsar. Kısayol GlobalShortcuts portalı üzerinden kaydedilir; portalla D-Bus (Linux masaüstünde programların birbirine mesaj yolladığı hat) üzerinden konuşulur. EKA-P14 gelmeden açılmaz, çünkü Wayland'da kaydedicinin kendisi çalışmıyor. Portalı sunan gerçek bir Wayland masaüstünde doğrulanır.

## 137. Dış Denetim Ve Eklenti Yüzeyleri

13 Ekim 2026 · "bulduklarını later a eklersin" · OYB-P11 · Oynatıcı · iş büyük · öncelik 2 · karar sahibin

- Satırlar (OYB-P11): OYB-31 Linux MPRIS; OYB-35 Yeni pencere ve çoklu örnek seçeneği; OYB-39 Uzaktan denetim; OYB-40 Çalışan oynatıcıya komut gönderme; OYB-41 Betik ve eklenti sistemi. [Tablo](piyasa/2026-10-13/oynatici-kitaplik-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.App/Program.cs, src/VidShrink.Core/SingleInstanceChannel.cs, src/VidShrink.App/Integration/, src/VidShrink.Player/MpvEngine.cs
- Sahibin kararı: oynatıcı dışarıdan yönetilebilen bir yüzey açsın mı (komut kanalı, Linux ortam denetimi, ağdan uzaktan denetim, eklenti) ve tek örnek kuralına "yeni pencere" seçeneği gelsin mi
- Plan: Bu bir karar paketidir; karar gelince satır başına ayrı işlere bölünür. En ucuz ilk adım çalışan oynatıcıya komut göndermektir (OYB-40): tek örnek kanalı bugün yalnız yol taşıyor, aynı kanala duraklat, sar ve sıraya ekle komutları eklenir; yeni pencere seçeneği (OYB-35) aynı dosyalara dokunur. Linux ortam denetimi (OYB-31) ve ağdan uzaktan denetim (OYB-39) bu komut katmanının üstüne kurulur. Eklenti sistemi (OYB-41) bilerek kapatılmış `load-scripts=no` seçeneğini geri açtığı için en sona kalır.

## 138. Kitaplık Ve Ağ Kaynakları

13 Ekim 2026 · "bulduklarını later a eklersin" · OYB-P12 · Oynatıcı · iş büyük · öncelik 2 · karar sahibin

- Satırlar (OYB-P12): OYB-09 Adlı kalıcı listeler; OYB-17 Yerel kitaplık görünümü; OYB-22 Site bağlantısı çözme; OYB-24 Ağ paylaşımı gezme. [Tablo](piyasa/2026-10-13/oynatici-kitaplik-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.App/Playback/, src/VidShrink.Core/PlaylistFile.cs, src/VidShrink.Core/MediaAddress.cs, src/VidShrink.Player/MpvEngine.cs
- Sahibin kararı: oynatıcı kitaplık ve ağ kaynağı yönüne büyüsün mü (adlı listeler, klasör tarayan kitaplık, site bağlantısı çözme, ağ paylaşımında gezme)
- Plan: Bu da bir karar paketidir; dört satır dört ayrı iştir ve her biri ayrı "evet" ister. En küçük adım adlı listelerdir (OYB-09): bugünkü m3u8 yazma yolu, uygulamanın kendi klasöründe tutulan adlı listelere genişler. Site bağlantısı çözme (OYB-22) kapalı K1 kararıyla aynı aracı kullanır ve bilerek yazılmış `ytdl=no` seçeneğini geri açar; ağ paylaşımı (OYB-24) parola saklama, kitaplık (OYB-17) yeni bir sekme demektir.

## 139. Çok İz Üstüne Gelenler

13 Ekim 2026 · "bulduklarını later a eklersin" · DZB-P15 · Düzenleyici · iş büyük · öncelik 2 · karar sahibin

- Satırlar (DZB-P15): DZB-10 Sesi videodan ayırma; DZB-46 Otomatik müzik kısma; DZB-47 Seslendirme kaydı; DZB-06 İz denetimleri; DZB-07 Boşluklu çizelge. [Tablo](piyasa/2026-10-13/duzenleyici-kurgu-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/Editing/EditTimeline.cs, src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.Core/Editing/EditProject.cs, src/VidShrink.Core/AudioCaptureArguments.cs, src/VidShrink.App/Editing/EditorTimeline.cs, src/VidShrink.App/Editing/EditorView.axaml, tests/VidShrink.Tests/
- Sahibin kararı: Çok iz açıldıktan sonra üstüne gelen işler (sesi ayırma, otomatik müzik kısma, seslendirme kaydı, iz denetimleri, boşluklu çizelge) kapsama girsin mi?
- Plan: DZB-P7 ve DZB-P12 bitmeden açılmaz ve üç parçaya bölünebilir. Ses ekleri (DZB-10, DZB-46, DZB-47): klibin sesi ikinci ize ayrı parça olarak iner, müzik konuşma başlayınca `sidechaincompress` süzgeciyle kısılır, anlatım kaydedicinin hazır mikrofon yoluyla kaydedilip ize konur. İz denetimleri (DZB-06) izi susturma, gizleme ve kilitlemedir; boşluklu çizelge (DZB-07) sıralı listeyi konumlu modele çevirdiği için en ağır parçadır. Doğrulama: her parça için EditTimeline birim testleri ve teslim dosyasında ses düzeyi ölçümü.

## 140. Vekil, Hazır İçerik Ve Model

13 Ekim 2026 · "bulduklarını later a eklersin" · DZB-P16 · Düzenleyici · iş büyük · öncelik 2 · karar sahibin

- Satırlar (DZB-P16): DZB-51 Vekil dosyalarla kurgu; DZB-22 Hazır başlık şablonları ve alt bant; DZB-52 Proje şablonları; DZB-53 Hazır varlık kitaplığı; DZB-44 Modelli konuşma temizleme; DZB-69 Otomatik yeniden kadraj; DZB-70 Arka plan silme ve nesne maskesi; DZB-71 Hareket izleme; DZB-72 Metinden sese. [Tablo](piyasa/2026-10-13/duzenleyici-kurgu-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/Editing/, src/VidShrink.Core/IntermediateCodecs.cs, src/VidShrink.App/Editing/
- Sahibin kararı: Uygulama kullanıcının diskine büyük ek dosya (vekil kopya, hazır içerik paketi, yapay zekâ modeli) üretsin ya da indirsin mi; hangisi istekle, hangisi hiç?
- Plan: Bu paket bir karar demetidir, tek seferde teslim edilmez: sahip hangi satırı açarsa o satır kendi paketi olur. Vekil dosya (ağır kaynağın kurgu sırasında kullanılan küçük kopyası; DZB-51) indirme istemez, yalnız disk yeri ister ve en bağımsız parçadır. Hazır içerik (DZB-22, DZB-52, DZB-53) lisans ve paket boyutu, modelli işler (DZB-44, DZB-69, DZB-70, DZB-71, DZB-72) indirme ve çalışma zamanı sorusudur. Doğrulama her satır için ayrı kurulur.

## 141. Ölçümle Hedef Kalite Araması

13 Ekim 2026 · "bulduklarını later a eklersin" · SIK-P11 · Küçült · iş büyük · öncelik 2 · karar sahibin

- Satırlar (SIK-P11): SIK-15 Ölçümle hedef kalite araması. [Tablo](piyasa/2026-10-13/kucult-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/PlanCalculator.cs, src/VidShrink.Ffmpeg/QualityMeter.cs, src/VidShrink.Ffmpeg/EncodeRunner.cs, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.Cli/CliApp.cs, tools/VidShrink.Bench/
- Sahibin kararı: Arama her turda ek kodlama ve ölçüm süresi getirir; hedef kalite kipine isteğe bağlı bir seçenek olarak eklensin mi?
- Plan: Hedef kalite bugün örnek ölçümünden çıkan tahminle çözülüyor (`src/VidShrink.Core/PlanCalculator.cs:1170`); bu pakette kısa örnekler aday ayarla kodlanıp ölçülür ve hedef puan tutana dek ayar aralığı ikiye bölünerek aranır. Tur sayısı sınırlanır, her tur ilerlemede görünür, arama kapalıyken bugünkü yol değişmez. Doğrulama: ölçüm aracıyla sabit bir dosya kümesinde tahmin yolu ile arama yolunun hedef puandan sapması ve harcadığı süre yan yana ölçülür.

## 142. Donanım Kodlayıcıda Dolby Vision

13 Ekim 2026 · "bulduklarını later a eklersin" · SIK-P13 · Küçült · iş büyük · öncelik 2 · karar sahibin

- Satırlar (SIK-P13): SIK-19 Donanım kodlayıcıda Dolby Vision. [Tablo](piyasa/2026-10-13/kucult-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/HdrResolver.cs, src/VidShrink.Core/FfmpegArguments.cs, tools/VidShrink.Bench/, docs/olcumler/
- Sahibin kararı: Dolby Vision kapsamı donanım kodlayıcılarına genişlesin mi, gerekirse ffmpeg dışında ayrı bir araçla?
- Plan: Önce ölçülür: ekran kartı kodlayıcılarıyla yapılan kodlamada ffmpeg Dolby Vision üst verisini çıktıya yazıyor mu; sonuç `docs/olcumler/` altına girer. Yazıyorsa `src/VidShrink.Core/HdrResolver.cs:46` satırındaki kodlayıcı koşulu genişletilir; yazmıyorsa iş ayrı araç zinciri kararına döner ve paket orada durur. Doğrulama: çıktının Dolby Vision kaydını ffprobe ile okuyan test.

## 143. HDR Yakalama

13 Ekim 2026 · "bulduklarını later a eklersin" · EKA-P15 · Kaydedici · iş büyük · öncelik 2 · karar sahibin

- Satırlar (EKA-P15): EKA-08 HDR yakalama. [Tablo](piyasa/2026-10-13/kaydedici-yakalama-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.App/Recorder/DdaOutputs.cs, src/VidShrink.App/Recorder/RecorderView.Gelismis.cs, docs/olcumler/
- Sahibin kararı: HDR ekranda varsayılan HDR dosya mı olsun, yoksa SDR'ye çevrilmiş kayıt mı.
- Plan: İlk adım ölçümdür: HDR ekranda bugünkü kaydın nasıl göründüğü `docs/olcumler/` altında yok. Sonra hızlı yolda `ddagrab` süzgecinden 10 bit çıkış istenir (bugün kare hep 8 bit `bgra`ya iniyor, `RecorderArguments.cs:1118`), 10 bit kodlama ve HDR renk etiketleri yazılır. HDR olmayan ekranda davranış değişmez. Ölçü: argüman testi (`tests/VidShrink.Tests/KaydediciDdagrabTests.cs` yanında) ve HDR ekranda alınan dosyanın `ffprobe` çıktısı.

## 144. Oyun Yakalama

13 Ekim 2026 · "bulduklarını later a eklersin" · EKA-P16 · Kaydedici · iş büyük · öncelik 2 · karar sahibin

- Satırlar (EKA-P16): EKA-05 Oyun yakalama. [Tablo](piyasa/2026-10-13/kaydedici-yakalama-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.Core/RecorderWizard.cs, src/VidShrink.App/Recorder/RecorderView.Sihirbaz.cs, docs/olcumler/
- Sahibin kararı: Oyun sürecine kanca atan ayrı bir yakalama yolu yapılsın mı, yoksa ekran kaydı yolu ölçülüp yeterli mi sayılsın.
- Plan: İlk adım ölçümdür: tam ekran bir oyunun bugünkü hızlı yolla kaydı hiç ölçülmedi. Kayıt düzgünse iş küçülür: sihirbazdaki "Oyun" seçimi (`RecorderWizard.cs:5`) oyunun bulunduğu ekranı kendiliğinden hedef alır ve satır kapanır. Kayıt bozuksa kanca yolu (oyunun içine girip çizdiği kareyi oradan alan yöntem) sahibin kararına çıkar; hile koruması olan oyunlarda risk taşır. Ölçüm `tools/VidShrink.Bench` ile alınır ve `docs/olcumler/` altına yazılır.

## 146. Girdi Efektleri Mac Ve Linux

13 Ekim 2026 · "bulduklarını later a eklersin" · EKB-P17 · Kaydedici · iş büyük · öncelik 2 · karar sahibin

- Satırlar (EKB-P17): EKB-14 Girdi efektleri macOS ve Linux'ta. [Tablo](piyasa/2026-10-13/kaydedici-sunum-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.App/Recorder/RecorderInput.cs, src/VidShrink.App/Recorder/RecorderView.Girdi.cs, src/VidShrink.App/Recorder/RecorderMagnifier.cs, src/VidShrink.App/Locales/
- Sahibin kararı: macOS'ta girdi izleme izni istensin mi, ve Wayland'da genel tuş dinleme yolu yoksa Linux desteği X11 ile mi sınırlansın.
- Plan: Boş dinleyicinin yerine macOS ve X11 için gerçek fare ve klavye dinleyicileri yazılır (RecorderView.Girdi.cs:14-22); Wayland ve X11, Linux'un iki ayrı pencere sistemidir. Büyüteç için ekran kopyası o sistemlerin kendi çağrısıyla alınır. İzin verilmediğinde efekt kapalı kalır ve kullanıcıya söylenir. Dinleyiciler arayüzün arkasında sahte olay akışıyla ölçülür; gerçek deneme o sistemlerde elle yapılır.

## 147. Arayüz Ölçeği Ve Yüksek Karşıtlık

13 Ekim 2026 · "bulduklarını later a eklersin" · AYR-P11 · Ayarlar Ve Kabuk · iş büyük · öncelik 2 · karar sahibin

- Satırlar (AYR-P11): AYR-12 Arayüz Ölçeği Ya Da Yazı Boyutu; AYR-13 Yüksek Karşıtlık Desteği. [Tablo](piyasa/2026-10-13/ayarlar-kabuk-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.App/Themes/Theme.axaml, src/VidShrink.App/Themes/PaletteCatalog.cs, src/VidShrink.App/Themes/Palette/, src/VidShrink.App/MainWindow.axaml
- Sahibin kararı: "Renk yalnız paletten, ölçü yalnız belirteçten" kuralına sistemin karşıtlık ve yazı boyutu ayarları için istisna açılsın mı?
- Plan: Önce AYR-13: sistem yüksek karşıtlık istediğinde seçilecek bir yüksek karşıtlıklı palet eklenir ve geçiş `PaletteCatalog.Use` üstünden yapılır. Sonra AYR-12: yazı boyutu belirteçlerine tek bir ölçek çarpanı bağlanır, Ayarlar'a yazı boyutu seçimi konur ve en küçük pencere boyutunda taşma her sekmede ölçülür. Doğrulama: karşıtlık oranı ölçümü ve pinli yerleşim ölçülerinin her ölçekte yeniden koşulması.

## 149. Tam Paket Ve Çevrimdışı Kurucu

13 Ekim 2026 · "bulduklarını later a eklersin" · DAG-P9 · Dağıtım Ve Otomasyon · iş büyük · öncelik 2 · karar sahibin

- Satırlar (DAG-P9): DAG-10 Taşınabilir sürüm; DAG-11 Çevrimdışı kurucu; DAG-20 macOS ve Linux'ta ffmpeg kurulumu. [Tablo](piyasa/2026-10-13/dagitim-otomasyon-dogrulanmis.md)
- Dokunacağı yer: .github/workflows/release.yml, src/VidShrink.Core/UpdateCheck.cs, src/VidShrink.Setup/Program.cs, src/VidShrink.Core/Setup/SetupDownloads.cs, install-vidshrink.sh, docs/kurulum.md
- Sahibin kararı: ffmpeg ve libmpv ikilileri kendi yayın dosyalarımızın içinde dağıtılsın mı (yüzlerce MB ek boyut ve lisans yükümlülüğü).
- Plan: `release.yml` her hedef için uygulama, ffmpeg ve libmpv'yi bir arada taşıyan ikinci bir arşiv üretir; taşınabilir kip (komşu tabloda AYR-05), uygulama klasöründe bir işaret dosyası görünce ayarı yanına yazar ve kendini güncellemeyi kapatır. Çevrimdışı kurucu aynı arşivi kurucunun var olan `--asset-source` yoluyla kullanır. macOS ve Linux betiği ffmpeg yoksa durmak yerine sha256'sı sabitlenmiş derlemeyi indirir. Doğrulama: ağ kapalıyken kurulum ve ilk küçültme, taşınabilir kopyada kullanıcı klasörüne dosya yazılmadığının testi.

## 150. macOS İmza, Dmg Ve Homebrew

13 Ekim 2026 · "bulduklarını later a eklersin" · DAG-P10 · Dağıtım Ve Otomasyon · iş büyük · öncelik 2 · karar sahibin

- Satırlar (DAG-P10): DAG-05 macOS geliştirici imzası ve noterleme; DAG-06 macOS disk görüntüsü (dmg); DAG-07 Homebrew cask. [Tablo](piyasa/2026-10-13/dagitim-otomasyon-dogrulanmis.md)
- Dokunacağı yer: macos-app-bundle.sh, .github/workflows/release.yml, install-vidshrink.sh, src/VidShrink.Core/UpdateCheck.cs; depo dışında tap deposu
- Sahibin kararı: ücretli Apple geliştirici üyeliği alınsın mı; alınırsa dmg yayımlanır ve Homebrew için kendi tap depomuz açılır.
- Plan: `macos-app-bundle.sh` ad-hoc imza yerine Developer ID ile imzalar ve paketi noterlemeye gönderir (noterleme: Apple'ın paketi tarayıp onay damgası vermesi); libmpv ve öteki kitaplıklar da imzalanır. İmzalı paketten sürükle bırak dmg üretilir ve yayına eklenir. Homebrew ana dizini en az 75 yıldız, sahibin kendi başvurusunda 225 yıldız arıyor ve bizde 12 var; bu yüzden cask (Homebrew'un hazır uygulama tarifi) kendi tap depomuzda (projeye ait Homebrew paket deposu) yayımlanır ve `release.yml` her sürümde sürüm ile sha256'yı tazeler. Doğrulama: CI'da Gatekeeper ve damga denetimi, temiz Mac'te tarayıcıdan indirip çift tıkla açma elle.

## 152. Betik Ve Eklenti Arabirimi

13 Ekim 2026 · "bulduklarını later a eklersin" · DAG-P12 · Dağıtım Ve Otomasyon · iş büyük · öncelik 2 · karar sahibin

- Satırlar (DAG-P12): DAG-36 Betik ve eklenti arabirimi. [Tablo](piyasa/2026-10-13/dagitim-otomasyon-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/, src/VidShrink.App/, yeni docs/ sayfası
- Sahibin kararı: uygulamaya betik motoru (yeni bağımlılık) eklensin ve sürümler arası korunacak bir eklenti arabirimi sözü verilsin mi.
- Plan: DAG-P4'ten önce başlanmaz: dış süreçten gelen komut kanalı, eklenti isteklerinin kumanda ve otomasyon kısmını bağımlılık eklemeden karşılar. Evet denirse ilk adım dar tutulur: kullanıcı klasöründeki betikler oynatıcı ve kuyruk olaylarına abone olur ve komut kanalındaki komutları çağırır; arayüze parça ekleyen eklenti sonraya kalır. Betik dili ve motoru kararın parçasıdır. Doğrulama: örnek betikle olay ve komut testi, betik hatasının uygulamayı düşürmediğinin testi.

## 155. macOS'ta Bölge Kaydı Seçili Ekrandan

14 Ekim 2026 · Dalga 1 yapımında görüldü (PLT-P1) · Platform Eşitliği · iş küçük · öncelik 2 · karar gerekmez

- Kanıt: `src/VidShrink.App/Recorder/RecorderView.Hedef.cs:177` `ScreenIndex = target == RecorderTargetKind.Screen ? ChosenScreen : 0` yazıyor; bölge ve pencere hedefinde ekran sırası 0'a düşüyor, Mac'te bölge her zaman birinci ekrandan kesiliyor.
- Plan: Bölge ve pencere hedefi, seçimin bulunduğu ekranın sırasını taşır. Argüman testi iki ekranlı sahte düzende bölgenin ikinci ekranın adıyla girdiğini pimler. Gerçek çok ekranlı Mac bu makinede ölçülemez; belgeye ölçülmedi diye yazılır.

## 156. Uzak Liste Adresinde Yayın Adı

14 Ekim 2026 · Dalga 1 yapımında ölçüldü (OYB-P1) · Oynatıcı · iş küçük · öncelik 2 · karar gerekmez

- Kanıt: uzak bir `.pls` adresi açılınca başlıkta yayının adı yerine dosya adı ("radyo.pls") görünüyor.
- Plan: Adres listeyse başlık, mpv'nin `media-title` değerini ya da listenin `Title1` satırını alır; ikisi de yoksa bugünkü gibi dosya adı. Yerel 127.0.0.1 sunucusuyla başlık testi.

## 158. Akıllı Kipte Kare Sınırı Dışı Kesim

14 Ekim 2026 · Dalga 2 yapımında görüldü (DZA-P4) · Düzenleyici · iş küçük · öncelik 2 · karar gerekmez

- Kanıt: ajan raporu, akıllı kipte kare sınırına denk gelmeyen kesimde birleşim noktasında bir karenin tekrar ettiğini ve ffmpeg'in "non monotonically increasing dts" yazdığını söylüyor (`src/VidShrink.Core/Editing/EditExport.cs`, `SmartEncode`). Kodda doğrulanmadı.
- Plan: Önce ölçüm: kare sınırı dışı kesimli kısa klipte kare sayısı ve dts dizisi okunur. Tekrar doğrulanırsa yeniden kodlanan baş parçanın bitişi bir sonraki anahtar karenin pts'sine hizalanır; test birleşik çıktıda kare sayısını ve dts'nin artan olduğunu pimler.

## 159. Yer İmli Dosyalar Geçmişte Ve Açılışta Sorma

14 Ekim 2026 · Dalga 2 yapımında görüldü (OYB-P4) · Oynatıcı · iş küçük · öncelik 2 · karar gerekmez

- Kanıt: ajan raporu, yer imi taşıyan dosyaların geçmiş penceresinde ayrıca işaretlenmediğini ve devam ayarında "açılışta sor" kipinin olmadığını söylüyor. Kodda doğrulanmadı.
- Plan: Geçmiş penceresinde yer imi olan satıra simge; devam ayarına üçüncü seçenek olarak "sor". İki kol için pencere testi.

