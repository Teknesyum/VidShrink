# Raf

Sahibin sözü, 13 Ekim 2026:

> şimdi tüm piyasayı araştır her bir sekmedeki eksiklerimiz hakkında kapsamlı plan yap saat 8 e kadar durma şu aşamada plan yap sadece işlem 2. aşama olacak bulduklarını later a eklersin

Aşağıdaki her başlık, ikinci aşamada tek seferde teslim edilebilecek bir iş paketidir; pazar araştırmasından çıktı.
Sıra yapım sırasıdır. Bir paketin işi başlayınca başlığı buradan silinir.
Gerekçe, sayılar ve kanıt: [plan-pazar-eksikleri.md](plan-pazar-eksikleri.md).

**Dalga 2 — Karar Gerekmez, Küçük İş Öncelik 2 Ya Da Orta İş Öncelik 1**

## 42. Ekrana Çizim Ve Prompter

13 Ekim 2026 · "bulduklarını later a eklersin" · EKB-P7 · Kaydedici · iş büyük · öncelik 1 · karar gerekmez

- Satırlar (EKB-P7): EKB-04 Ekrana çizim; EKB-15 Prompter. [Tablo](piyasa/2026-10-13/kaydedici-sunum-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.App/Recorder/, src/VidShrink.App/Recorder/CaptureAffinity.cs, src/VidShrink.App/Recorder/RecorderView.Kisayol.cs, src/VidShrink.App/Recorder/RecorderSettings.cs, src/VidShrink.App/Locales/
- Adım 1 (çizim) 16 Ekim 2026'da main'de: `ScreenSketch`, `RecorderSketch`, kısayol Ctrl+F2 (`EkranaCizimTests`).
- Adım 2 (prompter) 16 Ekim 2026'da main'de: kaydedicide "Prompter" (`RecorderPrompter`, `PrompterScroll`); kayıttan çıkarılır, çıkarılamazsa pencerede uyarı; kayıt başlayınca akar, duraklayınca durur; yedi hız basamağı, metin ve hız ayarda (`PrompterTests`).
- Prompter görünümü 16 Ekim 2026'da main'de: ayna, ortada okuma çizgisi, üç basamaklı yazı boyu (belirteçlerden), ayarda (`PrompterLook`, `PrompterTests.AynaCizgiVeYaziBoyuUygulanirAyaraYazilir`).
- Plan: Çizim için ekranı kaplayan saydam bir pencere açılır; kısayolla çizim kipine girilir, kalem, ok, kutu, numara ve yazı çizilir, pencere kayda girer. Prompter (konuşma metnini akıtan pencere) ayrı penceredir ve `CaptureAffinity.Exclude` ile kayıttan çıkarılır; Windows 10 2004 öncesinde pencere kayıttan çıkmadığı için orada uyarı gösterilir (CaptureAffinity.cs:16-20). İki adımda teslim edilebilir: önce çizim, sonra prompter. Çizim araçları başsız pencere testiyle, kayıttan çıkarma bayrağı pencere başına ölçülür.
- Durum (18 Ekim 2026, main): çizime araç paneli ve mürekkep rengi geldi (`yan/cizim-prompter`). Açık: çoklu ekranda yalnız kayıt bölgesi kaplanır, renk için kısayol yok, panelin görsel denetimi elle yapılacak.

## 50. Mac Ortam Denetimi Bağlantısı

13 Ekim 2026 · "bulduklarını later a eklersin" · OYB-P9 · Oynatıcı · iş orta · öncelik 2 · karar gerekmez

- Satırlar (OYB-P9): OYB-30 macOS Now Playing. [Tablo](piyasa/2026-10-13/oynatici-kitaplik-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Player/MpvEngine.cs, src/VidShrink.App/Playback/
- Plan: mpv'nin ortam tuşları seçeneği (`input-media-keys`) gömülü kullanımda varsayılan kapalıdır; önce macOS'ta açılır ve klavye ortam tuşlarının, Denetim Merkezi'ndeki çalan ortam alanının oynatıcıyı yönetip yönetmediği ölçülür. Yetmezse macOS'un ortam denetimi arayüzüne ayrı bir bağ yazılır. Sonraki ve önceki tuşu yine uygulamanın kuyruğuna bağlanır. Bu iş Windows makinesinde doğrulanamaz; Mac'te koşan bir işçi ister.

## 60. Kodlayıcı, HDR Ve Renk Ayarları

13 Ekim 2026 · "bulduklarını later a eklersin" · DON-P11 · Dönüştür · iş orta · öncelik 2 · karar gerekmez

- Satırlar (DON-P11): DON-15 Kodlayıcı ince ayarı; DON-25 Dönüştür'de HDR seçimi; DON-26 Renk uzayı ve aralık dönüşümü. [Tablo](piyasa/2026-10-13/donustur-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.Core/HdrResolver.cs, src/VidShrink.Core/VideoFilterChain.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/
- Plan: Hız ön ayarı, tune, profil, seviye ve bit hızı kipinde iki geçiş Dönüştür'de seçilebilir olur; bugün sabit varsayılan yazılıyor (`ConversionArguments.cs:142`). HDR seçimi Küçült'teki düğmeden okunmak yerine (`MainWindow.axaml.cs:4777`) Dönüştür'ün kendi panelinde gösterilir. Renk için BT.601, BT.709 ve BT.2020 ile sınırlı ve tam aralık seçimi eklenir; BT.709 ve BT.601 kolu hazır (`VideoFilterChain.cs:397`), BT.2020 ve aralık yeni yazılır. Doğrulama: argüman testleri ve renk kolu için ölçüm aracıyla önce ve sonra kare karşılaştırması.
- Durum (18 Ekim 2026, main): Dönüştür'de hız ön ayarı, tune, profil, seviye, iki geçiş, kendi HDR seçimi ve BT.601/709/2020 ile aralık seçimi (`yan/donustur-kodlayici`). Açık: matris değişen kollarda PSNR 28–32 dB, nedeni ölçülmedi.

## 69. Kamera Ana Kaynak Olarak

13 Ekim 2026 · "bulduklarını later a eklersin" · EKA-P8 · Kaydedici · iş orta · öncelik 2 · karar gerekmez

- Satırlar (EKA-P8): EKA-06 Aygıt kaynağı. [Tablo](piyasa/2026-10-13/kaydedici-yakalama-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.Ffmpeg/CaptureDevices.cs, src/VidShrink.App/Recorder/RecorderView.Hedef.cs, src/VidShrink.App/Recorder/RecorderView.Kamera.cs
- Plan: Hedef türlerine (`RecorderArguments.cs:24`) "aygıt" eklenir: web kamerası ya da yakalama kartı ekran girdisinin yerine ana görüntü olur. Windows'ta kamera girdisi zaten DirectShow ile kuruluyor (`RecorderArguments.cs:1203`); aynı kurucu ana girdi için yeniden kullanılır. Ölçü `tests/VidShrink.Tests/KaydediciHedefTests.cs` ve `KaydediciKameraTests.cs` yanına yazılır; gerçek deneme takılı bir kamerayla Windows'ta elle yapılır.
- Durum (18 Ekim 2026, main): hedef türlerine "aygıt" girdi; kamera ya da yakalama kartı ana görüntü olur (`yan/kamera-ana-kaynak`). Açık: takılı gerçek kamerayla elle deneme, aygıtın çözünürlük seçimi, kayıt sırasında anlık kare.

## 70. Kamera Katmanı Windows Dışında

13 Ekim 2026 · "bulduklarını later a eklersin" · EKA-P9 = EKB-P13 · Kaydedici · iş orta · öncelik 2 · karar gerekmez

- Satırlar (EKA-P9): EKA-07 Kamera katmanı Windows dışında. [Tablo](piyasa/2026-10-13/kaydedici-yakalama-dogrulanmis.md)
- Satırlar (EKB-P13): EKB-18 Kamera katmanı macOS ve Linux'ta. [Tablo](piyasa/2026-10-13/kaydedici-sunum-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.Ffmpeg/CaptureDevices.cs, src/VidShrink.App/Recorder/RecorderView.Kamera.cs
- Plan: Kamera girdisi macOS'ta `avfoundation`, Linux'ta `v4l2` ile kurulur (ffmpeg'in o sistemlerdeki aygıt girdileri) ve kamera paneli yalnız Windows koşulundan çıkar (`RecorderView.Kamera.cs:53`). Kardeş tablodaki EKB-18 ve onun paketi EKB-P13 aynı iştir; yalnız biri açılır. Argüman üretimi `tests/VidShrink.Tests/KaydediciKameraTests.cs` yanında her sistem için ölçülür; gerçek aygıt denemesi bu makinede yapılamaz, o sistemlerde elle yapılır.
- Durum (18 Ekim 2026, main): kamera katmanı ve aygıt hedefi macOS (`avfoundation`, 30 fps) ve Linux'ta (`v4l2`, liste `/dev/v4l/by-id`'den) aynı kurucudan kuruluyor (`KaydediciKameraSistemTests`). Açık: o makinelerde elle deneme; katmanın sürükleme düzenleyicisi yalnız Windows'ta.

## 78. Erişilebilirlik Denemesi

13 Ekim 2026 · "bulduklarını later a eklersin" · AYR-P7 · Ayarlar Ve Kabuk · iş orta · öncelik 2 · karar gerekmez

- Satırlar (AYR-P7): AYR-16 Ekran Okuyucu Denemesi; AYR-17 Klavyeyle Tam Kullanım Denemesi. [Tablo](piyasa/2026-10-13/ayarlar-kabuk-dogrulanmis.md)
- Dokunacağı yer: docs/olcumler/, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/Themes/Controls.axaml
- Plan: Her sekmede bir ana iş baştan sona önce yalnız klavyeyle, sonra ekran okuyucuyla (Windows'ta Anlatıcı ve NVDA) yapılır; sekme sırası, klavye tuzağı ve adı okunmayan denetimler tek tek yazılır. Bulgular `docs/olcumler` altına kayıt olarak girer; adı eksik denetim ve bozuk sekme sırası gibi küçük kusurlar aynı turda düzeltilir, büyükleri deftere yazılır. macOS'ta VoiceOver denemesi o makinede ayrıca yapılır.
- Durum (18 Ekim 2026, main): klavye ve UI Automation ağacıyla denendi, Tab tuzağı düzeldi, 45 adsız denetime ad verildi (`docs/olcumler/erisilebilirlik-denemesi.md`). Açık: Anlatıcı ve NVDA ile dinleyerek deneme, macOS VoiceOver, Shift+Tab yönü, alan başına çift Tab durağı, hedef çiplerinde birim ve paylaşım bağlantısı kutularının adı (yeni anahtar ister).

## 81. Windows 11 İlk Menüde Küçültme

13 Ekim 2026 · "bulduklarını later a eklersin" · DAG-P3 · Dağıtım Ve Otomasyon · iş orta · öncelik 2 · karar gerekmez

- Satırlar (DAG-P3): DAG-37 Windows 11 ilk menüsünde hızlı küçültme. [Tablo](piyasa/2026-10-13/dagitim-otomasyon-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.ShellExtension/VidShrink.ShellExtension.cpp, src/VidShrink.ShellExtension/AppxManifest.template.xml, src/VidShrink.Core/ShellIntegration.cs, src/VidShrink.Core/Setup/SetupRunner.cs, Install-VidShrink.ps1
- Plan: Kabuk eklentisi (Windows 11'in ilk sağ tık menüsüne girdi koyan C++ bileşeni) bugün tek komut döndürüyor ve alt komut kolu boş. Açma girdisinin yanına "VidShrink ile Küçült" üst girdisi eklenir: `GetFlags` `ECF_HASSUBCOMMANDS` döndürür, `EnumSubCommands` altı hedefi `ShellIntegration.QuickShrinkMenu` sırasıyla sayar ve her biri başlatıcıyı `--kucult` ile çağırır. Etiketler var olan `ShellLabels` kayıt anahtarından okunur, böylece menü dili korunur. Doğrulama: listeyi üç kurucuyla karşılaştıran mevcut teste eklenti dizisi eklenir; Windows 11'de ilk menüde alt menünün görünmesi ve bir hedefin küçültmeyi başlatması elle bakılır.
- Durum (18 Ekim 2026, main): eklenti "VidShrink ile Küçült" alt menüsünü altı hedefle sayıyor (`yan/win11-ilk-menu`). Açık: Windows 11'de elle deneme (paket kaydı, iki dosya, dil değişimi); kurucular `ShellLabels` yazmıyor, uygulama bir kez açılana dek yerleşik adlar görünür.

## 83. Akış Kalitesi Ve Kesintisiz Çalma

13 Ekim 2026 · "bulduklarını later a eklersin" · OYB-P10 · Oynatıcı · iş büyük · öncelik 2 · karar gerekmez

- Satırlar (OYB-P10): OYB-21 Akış kalite seçimi; OYB-43 Kesintisiz çalma. [Tablo](piyasa/2026-10-13/oynatici-kitaplik-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Player/MpvEngine.cs, src/VidShrink.Player/IPlaybackEngine.cs, src/VidShrink.App/Playback/PlayerView.Tracks.cs, src/VidShrink.App/Playback/PlayerView.Kuyruk.cs
- Plan: İki adımda teslim edilir ve adımlar ayrı işçilere verilebilir. Önce akış kalitesi (orta): uyarlamalı yayının katmanlarının motorun iz listesinde nasıl göründüğü ölçülür, sonra iz menüsüne kalite seçimi eklenir (video izi değiştirme ya da `hls-bitrate` seçeneği); OYB-P1 bitmeden başlamaz. Sonra kesintisiz çalma (büyük): sıradaki dosyayı önden yükleme (`prefetch-playlist`, `gapless-audio`) kuyruğun motora verilmemesi kararına dokunur, bu yüzden önce o kararın gerekçesi (`PlayerView.Kuyruk.cs:13`) yeniden ölçülür. İki katmanlı yerel bir HLS yayını ve ardışık iki ses dosyasıyla doğrulanır.
- Durum (18 Ekim 2026, main): akış kalite seçimi ve kesintisiz çalma (`yan/akis-kesintisiz`). Açık: gerçek aygıtta dosyalar arası boşluk ölçülemedi.

## 87. Resim Klibi, Tuval Ve Dönüşüm

13 Ekim 2026 · "bulduklarını later a eklersin" · DZB-P10 · Düzenleyici · iş büyük · öncelik 2 · karar gerekmez

- Satırlar (DZB-P10): DZB-04 Resim klibi; DZB-05 Düz renk klibi; DZB-03 Klip dönüşümü; DZB-56 Proje oranı ve tuval; DZB-49 Kare dondurma. [Tablo](piyasa/2026-10-13/duzenleyici-kurgu-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.App/Editing/EditorView.Kaynak.cs, src/VidShrink.App/Editing/EditorView.Klip.cs, src/VidShrink.Core/Editing/EditTimeline.cs, src/VidShrink.Core/Editing/ClipEffects.cs, src/VidShrink.Core/Editing/ClipFilters.cs, src/VidShrink.Core/Editing/EditLook.cs, src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.Core/Editing/EditProject.cs, tests/VidShrink.Tests/
- Plan: Resim ve düz renk, eklenirken kısa bir ara videoya çevrilir ve bildik kaynak gibi çizelgeye girer; önizleme yolu değişmez. Kare dondurma aynı yoldan gelir: oynatma başındaki kare resme çıkarılıp klip yapılır. Klibe serbest ölçek, konum ve açı, projeye de çıktı çerçevesi (9:16, 1:1, 16:9) eklenir; ikisi de ClipFilters'ın geometri zincirinden (ClipFilters.cs:38) geçer. Paket ikiye bölünebilir: resim, renk ve dondurma (DZB-04, DZB-05, DZB-49) ile dönüşüm ve tuval (DZB-03, DZB-56); doğrulama argüman testleri ve kare karşılaştırması.
- Durum (18 Ekim 2026, main): DZB-04, DZB-05, DZB-49 ve DZB-03, DZB-56 bitti; resim, düz renk ve kare dondurma ara videoyla çizelgeye girer (`StillClip`, `DuzenleyiciResimKlibiTests`), klibe ölçek, konum ve açı, projeye çerçeve (Kaynak, 16:9, 9:16, 1:1) geldi (`DuzenleyiciDonusumTuvalTests`). Açık: renk seçici, sabit klip süreleri, boş çizelgede tek resim, `ara-klipler/` budanmıyor, monitörde sürükleyerek konumlama.

## 88. İleri Renk Ve Bulanıklık

13 Ekim 2026 · "bulduklarını later a eklersin" · DZB-P13 · Düzenleyici · iş büyük · öncelik 2 · karar gerekmez

- Satırlar (DZB-P13): DZB-36 Eğriler ve renk tekerleri; DZB-38 Otomatik renk ve renk eşleme; DZB-28 Bulanıklık ve bölge gizleme. [Tablo](piyasa/2026-10-13/duzenleyici-kurgu-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/Editing/ClipEffects.cs, src/VidShrink.Core/Editing/ClipFilters.cs, src/VidShrink.Core/Editing/EditLook.cs, src/VidShrink.App/Editing/EditorView.Klip.cs, src/VidShrink.App/Editing/EditorView.Monitor.cs, src/VidShrink.App/Editing/EditorView.axaml, src/VidShrink.App/Locales/, tests/VidShrink.Tests/DuzenleyiciKlipOzellikTests.cs
- Plan: DZB-P3'ün üstüne gelir: eğriler ve üç bölgeli renk dengesi ffmpeg `curves` ve `colorbalance` süzgeçleriyle, tek tıkla düzeltme `normalize` ile yapılır. Bulanıklık `gblur` ile bütün kareye ya da monitörde çizilen dikdörtgene uygulanır (kırp, bulanıklaştır, geri bindir). İki çekimin rengini eşlemek için hazır süzgeç bilinmiyor; o yarı ayrı ölçüm ister. Paket ikiye bölünebilir: renk (DZB-36, DZB-38) ile bulanıklık (DZB-28); doğrulama argüman testleri ve kare karşılaştırması.
- Durum (18 Ekim 2026, main): renk yarısı (eğriler, renk dengesi, tek tıkla düzeltme) ve DZB-28 bulanıklık ve bölge gizleme geldi; `gblur` bütün kareye ya da monitörde çizilen dikdörtgene (kırp, bulanıklaştır, geri bindir), önizleme ile teslim aynı zinciri kurar, proje dosyasında geriye uyumlu `blur` alanı (`DuzenleyiciBulaniklikTests`). Açık: renk yarısı (DZB-36 eğriler ve renk tekerleri, DZB-38 otomatik renk ve renk eşleme); bölge çizilirken monitörde canlı dikdörtgen yok, bırakınca uygulanır; bölge zamanla izlemiyor (sabit dikdörtgen); yeni beş anahtar yalnız en ve tr'de, öbür diller İngilizceye düşer; arayüzün elle denenmesi.

## 89. Anahtar Kare Ve Hız Rampası

13 Ekim 2026 · "bulduklarını later a eklersin" · DZB-P14 · Düzenleyici · iş büyük · öncelik 2 · karar gerekmez

- Satırlar (DZB-P14): DZB-25 Klip ayarlarına anahtar kare; DZB-48 Hız rampası; DZB-50 Akıcı ağır çekim; DZB-26 Yumuşatma eğrileri. [Tablo](piyasa/2026-10-13/duzenleyici-kurgu-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/Editing/ClipEffects.cs, src/VidShrink.Core/Editing/ClipFilters.cs, src/VidShrink.Core/Editing/EditLook.cs, src/VidShrink.Core/Editing/EditTimeline.cs, src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.Core/Editing/EditProject.cs, src/VidShrink.App/Editing/EditorView.Klip.cs, src/VidShrink.App/Editing/EditorTimeline.cs, tests/VidShrink.Tests/
- Plan: Klip ayarları (ses düzeyi, kırpma, konum) klip içinde zamanla değişebilsin diye değerlere anahtar kare eklenir; süzgeç zinciri sabit sayı yerine zamana bağlı ifade yazar, yumuşatma eğrisi iki kare arasındaki geçişin biçimidir. Hız rampası klibi hız dilimlerine böler (`setpts`), akıcı ağır çekim ara kareyi `minterpolate` süzgeciyle üretir. Önizleme zinciri bugün sabit değer taşıyor; zamana bağlı ifadeyi taşıyıp taşımadığı ilk iş ölçülür. Paket ikiye bölünebilir: anahtar kare (DZB-25, DZB-26) ile hız (DZB-48, DZB-50); doğrulama argüman testleri ve çıktı süresinin ölçümü.
- Durum (18 Ekim 2026, main): hız rampası ve akıcı ağır çekim (`yan/hiz-rampasi`), anahtar kare (DZB-25, DZB-26) bitti: ses düzeyi ve kırpma kareleri, dört yumuşatma eğrisi, zamana bağlı ifade teslimde ve önizlemede ([ölçüm](olcumler/anahtar-kare-onizleme.md)). Açık: ters klipte kareler aynalanmıyor; dönüşümün (ölçek, konum, açı) anahtar karesi yok.

## 90. Slayt Gösterisi Ve Sesten Video

13 Ekim 2026 · "bulduklarını later a eklersin" · ARC-P10 · Bizde Olmayan Araçlar · iş büyük · öncelik 2 · karar gerekmez

- Satırlar (ARC-P10): ARC-10 Slayt gösterisi yapıcı; ARC-20 Sesten video. [Tablo](piyasa/2026-10-13/eksik-araclar-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/Editing/, src/VidShrink.App/Editing/, src/VidShrink.Core/ConversionArguments.cs
- Plan: İkisi de video olmayan malzemeden video üretir. Sesten video ARC-P3'ün üstüne gelir: ses kaynağında Dönüştür'e "görüntü" seçeneği eklenir, kapak resmi ya da dalga biçimi (showwaves süzgeci). Slayt gösterisi düzenleyicinin üç parçasına bağlıdır: resim klibi (DZB-04), müzik izi (DZB-01) ve çapraz geçiş (DZB-16); onlar bitince kalan iş fotoğrafları seçip süre vererek çizelgeye dizen kısa bir sihirbazdır ve iş orta'ya iner. Doğrulama: çıktının süresi ses süresine ya da fotoğraf sayısı çarpı süreye eşit.
- Durum (18 Ekim 2026, main): sesten video bitti, Dönüştür'de ses kaynağına kapak ya da dalga biçimi (`yan/slayt-gosterisi`). Açık: slayt sihirbazı; resim klibine (later 87) bağlı.

**Sahibin Kararını Bekleyenler**

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
- Durum (18 Ekim 2026, `yan/oynatici-hdr`): PQ/HLG videoda `@vstone:gpu` halkası (hedef BT.709/BT.1886) varsayılan açık, Gelişmiş panelde anahtar; ölçüm `docs/olcumler/oynatici-hdr-ton-esleme.md`. Açık: HDR ekrana geçişte halkanın kalkması, `target-peak` değeri (sahibin kararı), GPU'suz makinede düşen kare ölçümü.

## 99. Windows Ortam Denetimi Bağlantısı

13 Ekim 2026 · "bulduklarını later a eklersin" · OYB-P6 · Oynatıcı · iş orta · öncelik 1 · karar sahibin

- Satırlar (OYB-P6): OYB-29 SMTC ve odak dışı ortam tuşları. [Tablo](piyasa/2026-10-13/oynatici-kitaplik-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Player/MpvEngine.cs, src/VidShrink.App/Playback/Keymap.cs, src/VidShrink.App/Playback/PlayerView.axaml.cs
- Sahibin kararı: libmpv'nin kendi ortam denetimi anahtarı yetmezse Windows SDK bağı eklensin mi
- Plan: İlk adım karar istemez: motor seçeneklerine `media-controls` yazılır ve gerçek pencerede Windows ses kutucuğunda görünüp görünmediği, ortam tuşlarının pencere arkadayken çalışıp çalışmadığı ölçülür. Yetmezse pencere Windows'un ortam denetimi arayüzüne doğrudan bağlanır (MPC-BE'nin yolu); bu yeni bir Windows SDK bağı demektir ve sahibin kararını ister. İki yolda da sonraki ve önceki tuşu uygulamanın kendi kuyruğuna bağlanmalıdır, çünkü kuyruk motora verilmiyor.

## 100. Kayıpsız Birleştirme Ölçümü

13 Ekim 2026 · "bulduklarını later a eklersin" · DZA-P6 · Düzenleyici · iş küçük · öncelik 1

- Satırlar (DZA-P6): DZA-01 Kayıpsız birleştirme. [Tablo](piyasa/2026-10-13/duzenleyici-kesim-dogrulanmis.md)
- Yapıldı (18 Ekim 2026): `Core/Editing/MergeCompatibility` kodek, profil, başlık verisi özeti (SPS/PPS), çözünürlük, piksel oranı ve biçimi, kare hızı, zaman tabanı ve ses tutan kaynakları Hızlı ve Akıllı kipte concat ile kopyalıyor; tutmayan bugünkü yeniden kodlamaya gidiyor.
- Kalan: danışma 017'nin şartı. Gerçek örnek kümede (telefon, ekran kaydı, iki ayrı kodlayıcı) birleşim noktasında zaman damgası sıçraması, ses-görüntü kayması ve libmpv/VLC/Chrome oynatması `tools/VidShrink.Bench` ile ölçülür, `docs/olcumler/` altına yazılır. Hata çıkarsa kopya yolu kapanır, K7 döner.

## 103. Müzik İzi

13 Ekim 2026 · "bulduklarını later a eklersin" · DZB-P7 · Düzenleyici · iş orta · öncelik 1 · karar sahibin

- Satırlar (DZB-P7): DZB-01 Müzik izi. [Tablo](piyasa/2026-10-13/duzenleyici-kurgu-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/Editing/EditTimeline.cs, src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.Core/Editing/EdlPreview.cs, src/VidShrink.Core/Editing/EditProject.cs, src/VidShrink.App/Editing/EditorTimeline.cs, src/VidShrink.App/Editing/EditorView.Kaynak.cs, src/VidShrink.Player/MpvEngine.cs, tests/VidShrink.Tests/
- Sahibin kararı: Düzenleyici tek şeritten çıkıp videonun altına ikinci bir ses izi (müzik, anlatım) alsın mı?
- Plan: Çizelgeye A2 izi eklenir; görüntüsü olmayan dosya artık reddedilmez (EditorView.Kaynak.cs:89-93), bu ize başlangıç anı ve düzeyiyle konur. Teslimde iki ses ffmpeg `amix` süzgeciyle karıştırılır. Önizlemede iki sesi birlikte duymanın yolu ölçülmedi: mpv tek ses izi çalar, karıştırma `lavfi-complex` ister ve bunun `edl://` ile çalışması ilk iş denenir. Doğrulama: teslim dosyasında iki sesin de duyulduğunu gösteren düzey ölçümü ve proje dosyasını yazıp geri okuma testi.
- Durum (18 Ekim 2026, `yan/muzik-izi`): A2 izi, görüntüsüz dosyanın A2'ye inmesi (oynatma başı, -12 dB), sürükleme ve sağ tık menüsü (düzey, başa taşı, değiştir, kaldır) yapıldı; model, `amix` teslimi ve proje kaydı önceden vardı. Açık: önizleme müziği çalmıyor; `lavfi-complex` + `edl://` denenmedi (hız değişen klipte EDL zamanı çizelge zamanından ayrılıyor, arama davranışı bilinmiyor).

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

## 122. Kalite Ölçümü: Toplu İş Ve Kayıt

13 Ekim 2026 · "bulduklarını later a eklersin" · SIK-P10 · Küçült · iş küçük · öncelik 2

- Satırlar (SIK-P10): SIK-14 Çıktının gerçek kalite puanı. [Tablo](piyasa/2026-10-13/kucult-dogrulanmis.md)
- Yapıldı (18 Ekim 2026): Gelişmiş'te varsayılan kapalı "kaliteyi ölç" kutusu; iş bitince `Ffmpeg/OutputQualityCheck` VMAF, yoksa SSIM, yoksa PSNR ölçüp puanı ve bandı sonuç satırına yazıyor, iptal edilebilir.
- Kalan: toplu iş ve kabuk kuyruğu penceresi (`ShrinkJobWindow`) ölçmüyor, puan iş kaydına yazılmıyor; ölçüm çubuğu yüzde göstermiyor; bant eşikleri (VMAF 93/85/70) `tools/VidShrink.Bench` ile ölçülmedi; arayüz puanının CLI `--vmaf` puanıyla aynı olduğunu ölçen test yok.

## 123. VVC Kodlama Desteği

13 Ekim 2026 · "bulduklarını later a eklersin" · SIK-P12 · Küçült · iş orta · öncelik 2 · karar sahibin

- Satırlar (SIK-P12): SIK-16 VVC (H.266) kodlama. [Tablo](piyasa/2026-10-13/kucult-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/PlanParser.cs, src/VidShrink.Core/FfmpegArguments.cs, src/VidShrink.Core/PlanCalculator.cs, src/VidShrink.App/MainWindow.axaml (kodek seçimi), src/VidShrink.App/Locales/
- Sahibin kararı: libvvenc içeren bir ffmpeg derlemesi dağıtılsın mı ve oynatma desteği bu kadar darken VVC kodek listesine girsin mi?
- Plan: Önce dağıtılan ffmpeg'de libvvenc (VVC kodlayan açık kaynak kitaplık) var mı diye yoklanır; yoksa iş derleme kararında bekler. Varsa kodek izin listesine eklenir (`src/VidShrink.Core/PlanParser.cs:13`), hız ön ayarları ve boyut verimi ölçülerek yazılır, kodek seçiminde çoğu aygıtta oynamadığı uyarısıyla gösterilir. Doğrulama: argüman testi ve ölçüm aracıyla hedef boyut bandının tuttuğunun ölçülmesi. Dönüştür sekmesindeki DON-33 ile aynı iştir.

## 126. Kayıtta Bildirim Ve Simge Gizleme

13 Ekim 2026 · "bulduklarını later a eklersin" · EKB-P15 · Kaydedici · iş orta · öncelik 2 · karar sahibin

- Satırlar (EKB-P15): EKB-20 Bildirim susturma; EKB-21 Masaüstü simgelerini gizleme. [Tablo](piyasa/2026-10-13/kaydedici-sunum-dogrulanmis.md)
- Durum (21 Ekim 2026, `worktree-agent-later-126`): EKB-21 bitti — kaydedicide "Masaüstü simgelerini gizle" seçeneği, varsayılan kapalı; kayıt başlarken simgeler gizlenir, bitince, iptalde ve çökmeden sonraki açılışta geri gelir (durum `masaustu-simgeleri.gizlendi` dosyasında), kullanıcı zaten gizlediyse dokunulmaz, Windows dışında kutu kapalı ve ipuçlu (`KaydediciMasaustuSimgeTests`). EKB-20 bildirim susturma — karar: hayır; Windows'ta belgeli yolu yok, yapılmayacak. Açık: gerçek Windows masaüstünde (Windows 10 ve 11) bir kayıtla elle deneme.
- Dokunacağı yer: src/VidShrink.App/Recorder/, src/VidShrink.App/Recorder/RecorderSettings.cs, src/VidShrink.App/Recorder/RecorderView.axaml, src/VidShrink.App/Locales/
- Sahibin kararı: Uygulama kayıt sırasında sistemin bildirim ayarına ve masaüstü simgelerine dokunsun mu.
- Plan: Kayıt başlarken bildirimler susturulur ve masaüstü simgeleri gizlenir, bitince eski durum geri yüklenir; eski durum diske yazılır ki çökmeden sonra açılışta geri alınsın. Windows'ta bildirim susturmanın belgeli yolu doğrulanmadı; bulunamazsa EKB-20 yapılmaz, yalnız simge gizleme teslim edilir. EKB-20 kardeş tablodaki EKA-27 ile aynı iştir. Geri yükleme sahte sistem katmanıyla, çökme senaryosu dahil ölçülür.

## 127. Çeviri Sitesi Bağlantısı

13 Ekim 2026 · "bulduklarını later a eklersin" · AYR-P10 · Ayarlar Ve Kabuk · iş küçük · öncelik 2 · karar sahibin

- Satırlar (AYR-P10): AYR-04 Çeviri Katkı Platformu. AYR-25 (gözden geçmemiş dil işareti) bitti: `Strings.ReviewedLanguages`, `CeviriTamlikIsaretiTests`. [Tablo](piyasa/2026-10-13/ayarlar-kabuk-dogrulanmis.md)
- Dokunacağı yer: README.md, .weblate, .github/workflows/
- Sahibin kararı: bir çeviri sitesinde (Weblate, Crowdin ya da Transifex) proje hesabı açılsın mı; hesabı sahip açar.
- Plan: Seçilen site depodaki `Locales` klasörüne bağlanır, README'deki katkı yolu oraya çevrilir. Doğrulama: siteden gelen ilk deneme çevirisinin dizgi tamlığı testinden geçmesi.

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

## 144. Oyun Yakalama

13 Ekim 2026 · "bulduklarını later a eklersin" · EKA-P16 · Kaydedici · iş büyük · öncelik 2 · karar sahibin

- Satırlar (EKA-P16): EKA-05 Oyun yakalama. [Tablo](piyasa/2026-10-13/kaydedici-yakalama-dogrulanmis.md)
- Dokunacağı yer: src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.Core/RecorderWizard.cs, src/VidShrink.App/Recorder/RecorderView.Sihirbaz.cs, docs/olcumler/
- Sahibin kararı: Oyun sürecine kanca atan ayrı bir yakalama yolu yapılsın mı, yoksa ekran kaydı yolu ölçülüp yeterli mi sayılsın.
- Plan: Sihirbazın "Oyun" cevabı oyunun ekranını kendiliğinden seçiyor (`RecorderWizard.GameScreen`, 18 Ekim 2026). Kalan: tam ekran bir oyunun bugünkü hızlı yolla kaydı `tools/VidShrink.Bench` ile ölçülür, `docs/olcumler/` altına yazılır; kayıt bozuksa kanca yolu (oyunun içine girip çizdiği kareyi alan yöntem) sahibin kararına çıkar, hile koruması olan oyunlarda risk taşır.

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
