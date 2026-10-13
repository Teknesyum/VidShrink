# Ayarlar, Hakkında Ve Kabuk — Doğrulanmış Eksik Tablosu

13 Ekim 2026. `ayarlar-kabuk-eksikler.md` tablosunun doğrulanmış hali. Kod `main` dalının `eb0a5db0` commit'inde okundu. Kaynak numaraları `ayarlar-kabuk.md` içindeki Kaynaklar bölümüne bakar. 48 satır geldi, 48 satır kaldı.

| No | Eksik | Kimde Var | Bizde Durum | Fayda | İş | Öncelik | Karar | Kaynak |
|---|---|---|---|---|---|---|---|---|
| AYR-01 | Ayar Yedeği: Bütün ayarları (kısayollar, ad deseni, palet, kaydedici ayarı) tek dosyaya dışa aktarma ve o dosyadan geri yükleme; gizli anahtarlar dışarıda kalır. | PowerToys, ShareX | yarım: yalnız ön ayar dışa ve içe aktarılıyor (src/VidShrink.App/MainWindow.OnAyar.cs:301); ayarın tümü yalnız sıfırlanabiliyor, sıfırlamanın tuttuğu dosya listesi yedek için hazır envanter (src/VidShrink.App/AppDataReset.cs:18) | Bilgisayar değiştiren ya da yeniden kuran kişi kısayollarını ve ayarlarını baştan kurmaz. | orta | 1 | gerekmez | [1][18][39][36] |
| AYR-02 | Sistem Temasını İzleme: İşletim sistemi açık ya da koyu görünüme geçince uygulamanın seçili açık ve koyu palet arasında kendiliğinden geçmesi; seçenek olarak, varsayılan değişmeden. | PowerToys, mpv.net, Windows Terminal | yok; en yakın kod: açılış `RequestedThemeVariant="Dark"` ile sabit (src/VidShrink.App/App.axaml:5), 36 palet elle seçiliyor (src/VidShrink.App/MainWindow.axaml:1547) | Gündüz açık, gece koyu çalışan kişi paleti elle değiştirmez. | orta | 2 | gerekmez | [1][7][76][68] |
| AYR-03 | Pencere Konumunu Ve Boyutunu Hatırlama: Uygulamanın kapandığı yerde, aynı boyutta ve aynı ekranda açılması. | mpv.net, LosslessCut, Windows Terminal | yok; en yakın kod: yerleşim dosyasında yalnız bir panel yüksekliği saklanıyor (src/VidShrink.App/MainWindow.axaml.cs:3123); pencere her açılışta ekranı kaplamış açılıyor, geri yüklenince 1560×1060 ve ekran ortasında (src/VidShrink.App/MainWindow.axaml:12-14) | İkinci ekranda ya da küçültülmüş pencereyle çalışan kişi her açılışta pencereyi yeniden yerleştirmez. | küçük | 1 | sahip: açılışta ekranı kaplamış pencere yerine son konum, boyut ve durum gelir; varsayılan davranış değişir | [7][46][37][61] |
| AYR-04 | Çeviri Katkı Platformu: Gönüllülerin dizgileri tarayıcıdan çevirip düzeltebildiği ortak çeviri sitesi (Transifex, Crowdin, Weblate gibi). | HandBrake, OBS Studio, IINA, LosslessCut ve 2 rakip daha | yarım: 42 dil klasörü var (src/VidShrink.App/Localization/Strings.cs:62), katkı yolu yalnız "Open an issue" (README.md:348); 40 dilin çevirisi okunmadı; defterde | Anadili konuşan kişi yanlış çeviriyi kendisi düzeltir, 42 dil sözü insan gözünden geçmiş olur. | orta | 2 | sahip: dış hizmette proje hesabı açılır ve katkı süreci yürütülür | [10][15][12][14][7][58] |
| AYR-05 | Taşınabilir Kip: Ayar ve günlük dosyalarının uygulama klasörünün yanında tutulması; kurulum izi bırakmadan bellekten çalışma. | Windows Terminal, mpv.net, ShareX, OBS Studio ve 1 rakip daha | yarım: kurulumsuz zip yayımlanıyor (v1.2.16 yayınında `vidshrink-win-x64.zip`) ama ayar yine kullanıcı klasörüne yazılıyor; ayar yolu yalnız ölçüm için `VIDSHRINK_SETTINGS_PATH` ortam değişkeniyle değişiyor (src/VidShrink.Core/UpdateCheck.cs:476) | Kurulum yetkisi olmayan iş bilgisayarında ya da USB bellekte uygulama ayarlarıyla birlikte taşınır. | orta | 1 | sahip: yeni dağıtım biçimi ve güncellemenin bu kipte nasıl davranacağı | [4][7][17][19][16][55][40] |
| AYR-06 | Yenilikler Ekranı: Güncellemeden sonra ne değiştiğini gösteren kısa not ve Hakkında'da sürüm notlarına bağlantı. | PowerToys, OBS Studio; LosslessCut'ta yalnız yeni sürüm bağlantısı | yok; en yakın kod: Güncelleme kartında yalnız aç ve kapa anahtarı (src/VidShrink.App/MainWindow.axaml:1719), Hakkında'da yalnız sürüm satırı (src/VidShrink.App/Locales/en/main.json:412), yayın sayfası yalnız başlatıcısız kurulumda açılıyor (src/VidShrink.App/MainWindow.Guncelleme.cs:84) | Güncelleme çıkışta sessizce kurulduğu için kullanıcı neyin değiştiğini ancak buradan öğrenir. | küçük | 2 | gerekmez | [1][72][33][13][65] |
| AYR-07 | Hata Bildirme Yolu: Sürümü, işletim sistemini ve günlük yolunu hazır taşıyan, depodaki hata şablonunu açan "hata bildir" düğmesi. | PowerToys, LosslessCut; OBS Studio'da yalnız günlük yükleme | yarım: başlık çubuğunda yalnız depo bağlantısı (src/VidShrink.App/MainWindow.axaml:1873), günlük dosyası elle açılıyor (src/VidShrink.App/MainWindow.axaml:1601); `.github` altında yalnız `workflows` var | Sessiz kalan hatalar kullanıcıdan eksiksiz bilgiyle ve tek tıkla gelir. | küçük | 2 | gerekmez | [1][72][59][60] |
| AYR-08 | Ayarlarda Arama: Ayar adını yazınca ilgili kartın bulunup öne gelmesi. | PowerToys, Windows Terminal, IINA, mpv.net | yok; en yakın kod: Ayarlar 12 kartlık tek duvar (src/VidShrink.App/MainWindow.axaml:1512) | Aranan ayar kaydırmadan bulunur; kart sayısı arttıkça değeri büyür. | küçük | 2 | gerekmez | [1][5][11][7] |
| AYR-09 | Güncelleme Kanalı Seçimi: Kararlı sürüm ile ön sürüm arasında Ayarlar'dan seçim. | PowerToys, ShareX, OBS Studio | yarım: sürüm numarasındaki ön sürüm eki ayrıştırılıyor (src/VidShrink.Core/UpdateCheck.cs:286), kanal seçimi yok; Güncelleme kartında yalnız otomatik güncelleme anahtarı (src/VidShrink.App/MainWindow.axaml:1719) | Gönüllü kullanıcı yeni sürümü erken dener, hata herkese ulaşmadan yakalanır. | orta | 2 | sahip: ön sürüm kanalı açmak yayın sürecini değiştirir | [2][18][34][72] |
| AYR-10 | Uygulama İçi Günlük Görüntüleyici: Günlüğün uygulama penceresinde okunması ve panoya kopyalanması. | HandBrake, IINA, OBS Studio | yarım: "Günlüğü aç" dosyayı dosya yöneticisinde seçili gösteriyor, içeriğini açmıyor (src/VidShrink.App/MainWindow.axaml.cs:1511; src/VidShrink.App/Platform.cs:9); günlüğün tamamını döndüren yöntem hazır (src/VidShrink.Core/Gunluk.cs:128) | Kullanıcı hatayı uygulamadan çıkmadan görür ve metni tek tıkla kopyalar. | küçük | 2 | gerekmez | [29][11][72] |
| AYR-11 | Lisanslar Sayfası: ffmpeg, libmpv ve öteki bileşenlerin lisans ve bildirim metinlerini Hakkında'dan açma. | OBS Studio, LosslessCut; Shutter Encoder'da içerik doğrulanmadı | yarım: `LICENSE` ve `THIRD-PARTY-NOTICES.md` yalnız depoda, README'den bağlı (README.md:405); Hakkında dizgilerinde lisans satırı yok (src/VidShrink.App/Locales/en/main.json:395) | Kullanıcı ve kurum denetçisi hangi bileşenin hangi lisansla geldiğini uygulamanın içinden okur. | küçük | 3 | gerekmez | [64][21] |
| AYR-12 | Arayüz Ölçeği Ya Da Yazı Boyutu: Ayarlar'da yazı boyutu ve sıkılık seçimi ya da sistemin "metni büyüt" ayarını izleme. | OBS Studio (ayar), Shotcut (ortam değişkeni) | yok; en yakın kod: yazı boyutları sabit belirteç (src/VidShrink.App/Themes/Theme.axaml:219), pencere en az 1136×720 (src/VidShrink.App/MainWindow.axaml:12) | Az gören ya da çok yüksek çözünürlüklü ekranda çalışan kişi yazıyı okuyabileceği boya getirir. | büyük | 2 | sahip: ölçü yalnız belirteçten gelir kuralına ölçek çarpanı eklenir | [72][30][23][28][75][42] |
| AYR-13 | Yüksek Karşıtlık Desteği: Windows karşıtlık teması ya da macOS "Karşıtlığı Artır" açıkken daha yüksek karşıtlıklı görünüme geçme. | Windows Terminal, PowerToys | yok; en yakın kod: 36 palet sabit renkli, sistem karşıtlık ayarı okunmuyor (src/VidShrink.App/Themes/PaletteCatalog.cs:51) | Az gören kullanıcı sistemde seçtiği karşıtlığı uygulamada da bulur. | orta | 2 | sahip: renk yalnız paletten gelir kuralına sistem renkleri ya da yeni palet eklenir | [24][28][75][5][6] |
| AYR-14 | Canlandırmaları Kapatma Anahtarı: Sistem ayarından bağımsız, Ayarlar'da elle "canlandırmaları kapat" seçeneği. | LosslessCut, Shutter Encoder, Windows Terminal | yarım: canlandırma yalnız Windows sistem ayarına göre kapanıyor, elle anahtar yok (src/VidShrink.App/MainWindow.axaml.cs:377) | Hareketten rahatsız olan kişi sistem ayarına dokunmadan uygulamayı sakinleştirir. | küçük | 2 | gerekmez | [13][66][22][76] |
| AYR-15 | macOS Ve Linux'ta Azaltılmış Hareketi İzleme: Sistemin "hareketi azalt" ayarını Windows dışında da okuma. | IINA, LosslessCut | yarım: `AnimationsAllowed` Windows dışında hep izinli dönüyor (src/VidShrink.App/MainWindow.axaml.cs:379) | macOS ve Linux kullanıcısının sistem tercihi uygulamada da geçerli olur. | küçük | 2 | gerekmez | [11][13][75] |
| AYR-16 | Ekran Okuyucu Denemesi: Anlatıcı, NVDA ve VoiceOver ile her sekmenin baştan sona okunması ve bulguların kayda geçmesi. | HandBrake, LosslessCut, OBS Studio | yarım: 283 `AutomationProperties` ataması var, Ayarlar ve başlık çubuğu bölgesinde 16 (src/VidShrink.App/MainWindow.axaml:1530); deneme kaydı `docs/olcumler` altında yok | Görmeyen kullanıcının uygulamayı kullanıp kullanamadığı tahminle değil ölçüyle bilinir. | orta | 2 | gerekmez | [9][13][35][51][24] |
| AYR-17 | Klavyeyle Tam Kullanım Denemesi: Fare olmadan her sekmede işin baştan sona yapılabildiğinin, sekme sırasının ve klavye tuzağı olmadığının denenmesi. | Microsoft, Apple ve WCAG yönergeleri ister; rakipte doğrulanmadı | yarım: odak halkası var (src/VidShrink.App/Themes/Controls.axaml:13); `TabIndex`, `KeyboardNavigation`, `IsTabStop` 0 eşleşme, deneme kaydı yok | Fare kullanamayan kişi her işi klavyeyle bitirebilir. | orta | 3 | gerekmez | [27][23][75] |
| AYR-18 | Ana Pencere Kısayolları: Sekmeler arası geçiş, Ayarlar'ı açma ve dosya açma gibi kabuk düzeyi kısayollar ile bunların düzenlenebilir tablosu. | VLC, OBS Studio, LosslessCut | yarım: kısayol tablosu yalnız oynatıcı ve düzenleyicide (src/VidShrink.App/Playback/ShortcutBindings.cs:20); ana pencere yalnız Esc tuşunu dinliyor (src/VidShrink.App/MainWindow.PencereKipi.cs:41) | Beş sekme arasında fareye uzanmadan geçilir. | orta | 2 | gerekmez | [26][72][13][44] |
| AYR-19 | macOS Ve Linux'ta İş Bitti Bildirimi: Kodlama bitince işletim sisteminin bildirimini Windows dışında da gösterme. | LosslessCut, HandBrake | yarım: bildirim yalnız Windows'ta (src/VidShrink.App/IsBittiBildirimi.cs:64); Windows çağrıları sahada hiç koşmadı; defterde | macOS ve Linux kullanıcısı uzun işte pencerenin başında beklemez. | orta | 2 | gerekmez | [13][67][9][74] |
| AYR-20 | Bildirim Sesi: İş ya da kuyruk bitince ses çalma ve ses dosyasını seçme. | HandBrake, ShareX, Shutter Encoder | yok; en yakın kod: ses yalnız kaydedicide tıklama sesi olarak çalınıyor (src/VidShrink.App/Recorder/RecorderInput.cs:151) | Başka pencerede çalışan kişi işin bittiğini ekrana bakmadan duyar. | küçük | 2 | gerekmez | [74][18][22] |
| AYR-21 | Süreç Önceliği Ayarı: Kodlama sürecinin işlemci önceliğini (düşük, normal, yüksek) Ayarlar'dan seçme. | HandBrake, OBS Studio, Shotcut | yok; en yakın kod: `BelowNormal` yalnız yardımcı süreçlerde (küçük resim, sessizlik tarama, sahne bulma, ses ölçümü) ve güncelleme başlatıcısında veriliyor (src/VidShrink.Core/Editing/ThumbnailQueue.cs:276; src/VidShrink.App/MainWindow.KapanistaGuncelleme.cs:239), kodlama sürecinde öncelik ayarı yok; Ayarlar'da yalnız eşzamanlı iş sayısı (src/VidShrink.App/MainWindow.axaml:1645) | Uzun kodlama sürerken tarayıcı ve öteki programlar takılmaz. | küçük | 2 | gerekmez | [52][72][53] |
| AYR-22 | Tepsiye Küçültme: Ana pencereyi görev çubuğu yerine bildirim alanına küçültme seçeneği. | HandBrake, OBS Studio, PowerToys | yarım: tepsi simgesi yalnız kaydedicide (src/VidShrink.App/Recorder/RecorderTray.cs:14) | Uzun kuyruk arka planda sürerken görev çubuğu boş kalır. | küçük | 2 | sahip: pencereyi küçültme ve kapatma davranışı değişir | [73][72][1][62] |
| AYR-23 | İlk Açılış Karşılaması: İlk açılışta beş işi ve nereden başlanacağını gösteren kısa karşılama. | IINA, OBS Studio, PowerToys | yarım: yalnız kaydedici kurulum sihirbazı (src/VidShrink.App/Recorder/RecorderSettings.cs:271) ve varsayılan uygulama önerisi (src/VidShrink.App/MainWindow.axaml.cs:366) | Yeni kullanıcı beş sekmeli uygulamada aradığı işi ilk ekranda bulur. | orta | 2 | sahip: açılışa yeni bir ekran eklenir | [11][12][57][54] |
| AYR-24 | Kullanım Kılavuzu: Sekme sekme, ekran görüntülü çevrimiçi kılavuz. | HandBrake, VLC, OBS Studio, Shutter Encoder | yarım: iki dilde, beşer ekran görüntülü kullanım belgesi depoda var (docs/kullanim.md:1; docs/kullanim.tr.md:1) ve README'den bağlı (README.md:373); sekmelerden yalnız kaydedicinin kendi başlığı var (docs/kullanim.md:64), belge uygulamadan açılmıyor, öteki dillerde ve site olarak yok | Kullanıcı bir ayarın ne işe yaradığını resimli ve kendi dilinde okur, destek sorusu azalır. | büyük | 2 | sahip: belge sitesi açılır ve dillerde sürdürülür | [8][25][16][22] |
| AYR-25 | Bakımsız Çeviriyi İşaretleme: İnsan gözünden geçmemiş dilleri dil listesinde işaretleme ya da çeviri tamlığını yayımlama. | HandBrake, ShareX, LosslessCut | yarım: 42 dil listede eşit görünüyor (src/VidShrink.App/Localization/Strings.cs:62); 40 dilin çevirisi okunmadı; defterde | Kullanıcı seçtiği dilin gözden geçmiş mi, ham mı olduğunu bilir. | küçük | 2 | sahip: 42 dil sözünün nasıl sunulacağı | [9][18][14] |
| AYR-26 | Yardım Girişi: F1 ya da "Yardım" düğmesiyle açık sekmenin kılavuz sayfasına gitme. | OBS Studio; ötekilerde uygulama içi giriş doğrulanmadı | yok; en yakın kod: başlık çubuğunda depo bağlantısı (src/VidShrink.App/MainWindow.axaml:1873), ana pencere yalnız Esc tuşunu dinliyor (src/VidShrink.App/MainWindow.PencereKipi.cs:41); açılacak kullanım belgesi depoda var (docs/kullanim.md:1) | Takılan kullanıcı tarayıcıda aramadan doğru sayfaya ulaşır. | küçük | 3 | gerekmez | [72] |
| AYR-27 | Tanılama Paketi: Günlüğü, ayar dosyalarını ve sistem bilgisini tek sıkıştırılmış dosyada toplama. | PowerToys | yarım: tek günlük dosyası elle açılıp elle ekleniyor (src/VidShrink.Core/Gunluk.cs:28) | Hata bildiren kişi tek dosya ekler, geliştirici eksik bilgi için geri sormaz. | küçük | 3 | gerekmez | [1] |
| AYR-28 | Kodlama Başına Günlük: Her işin günlüğünü ayrı dosyaya ya da çıktının yanına yazma. | HandBrake | yarım: tek dönen günlük, 1 MiB'de devriliyor ve tek yedek tutuyor (src/VidShrink.Core/Gunluk.cs:19) | Dün bozuk çıkan dosyanın günlüğü bugünkü işlerin altında kaybolmaz. | küçük | 3 | gerekmez | [29][52] |
| AYR-29 | Gizlilik Beyanı Bağlantısı: "Hiçbir veri gönderilmez" beyanını ve ağ isteği listesini Hakkında'dan açma. | PowerToys; Shotcut beyanı yalnız web sitesinde | yarım: beyan ve ağ isteği listesi yalnız README'de (README.md:413) | Kullanıcı uygulamanın ağa ne gönderdiğini uygulamanın içinden öğrenir. | küçük | 3 | gerekmez | [1][30] |
| AYR-30 | Seçmeli Tanılama Verisi: Kullanıcının izniyle anonim kullanım ve hata verisi toplama, toplanan veriyi gösterme. | PowerToys | yok; en yakın kod: README "No telemetry" der (README.md:12), güncelleme düşüşleri yalnız yerelde sayılıyor | Geliştirici sessiz hataları kullanıcı bildirmeden görür. | büyük | 3 | sahip: README'deki "No telemetry" sözü değişir | [1] |
| AYR-31 | Ayarları Cihazlar Arası Eşitleme: Ayarların bir hesap ya da bulut klasörü üstünden bilgisayarlar arasında taşınması. | rakipte doğrulanmadı; PowerToys ve Windows Terminal'de açık istek | yok; en yakın kod: ayar yolu sabit, yalnız ortam değişkeniyle değişiyor (src/VidShrink.Core/UpdateCheck.cs:476) | İki bilgisayar kullanan kişinin ayarı kendiliğinden aynı kalır. | büyük | 3 | sahip: hesap ya da bulut hizmeti gerekir | [38][36] |
| AYR-32 | Erişim Tuşları Ve Kısayol Duyurusu: Düğmelere Alt ile harf atama ve kısayolu ekran okuyucuya bildirme. | Microsoft yönergesi ister; rakipte doğrulanmadı | yok; `AccessKey` ve `AcceleratorKey` 0 eşleşme, en yakın kod erişilebilir adlar (src/VidShrink.App/MainWindow.axaml:1530) | Ekran okuyucu kullanan kişi bir düğmenin kısayolunu duyar, menüye harfle ulaşır. | orta | 3 | gerekmez | [27] |
| AYR-33 | Bölgeler Arası Geçiş Tuşu: F6 ile sekme şeridi, içerik ve başlık çubuğu arasında atlama. | Microsoft yönergesi ister; rakipte doğrulanmadı | yok; en yakın kod: ana pencere yalnız Esc tuşunu dinliyor (src/VidShrink.App/MainWindow.PencereKipi.cs:41); F6 kaydedicide bölüm işareti kısayolu olarak tutuluyor, varsayılan tuş çakışır (src/VidShrink.App/Recorder/RecorderHotkeys.cs:42) | Klavye kullanıcısı onlarca Tab basmadan bölüm değiştirir. | küçük | 3 | gerekmez | [27] |
| AYR-34 | E-posta Bildirimi: İş bitince belirlenen adrese e-posta gönderme. | Shutter Encoder | yok; en yakın kod: bildirim yalnız pencere ve tepsi balonu (src/VidShrink.App/IsBittiBildirimi.cs:124) | Bilgisayarın başında olmayan kişi işin bittiğini telefonundan öğrenir. | orta | 3 | sahip: posta sunucusu bilgisi saklanır ve yeni ağ isteği doğar | [22] |
| AYR-35 | İş Bitince Özel Komut: Kodlama ya da kuyruk bitince kullanıcının seçtiği programı çıktı dosyasıyla çalıştırma. | HandBrake | yarım: kuyruk sonunda dört hazır seçenek var, özel komut yok (src/VidShrink.App/QueueEndActions.cs:9) | Çıktıyı yükleme ya da taşıma gibi sonraki adım kendiliğinden başlar. | küçük | 3 | sahip: kullanıcının yazdığı komutu çalıştırmak yeni bir güvenlik yüzeyi | [9][74] |
| AYR-36 | İş Parçacığı Sınırı: Kodlamanın kullanacağı işlemci iş parçacığı sayısını sınırlama. | Shutter Encoder | yok; en yakın kod: Ayarlar'da yalnız eşzamanlı iş sayısı (src/VidShrink.App/MainWindow.axaml:1645) | Kodlama sürerken bilgisayarda başka işe yer kalır. | küçük | 3 | gerekmez | [22] |
| AYR-37 | Grafik Bağdaştırıcısı Seçimi: Birden çok ekran kartı olan makinede hangisinin kullanılacağını seçme. | Shotcut, Shutter Encoder | yok; en yakın kod: kaydedici yalnız ilk bağdaştırıcının ekranlarını sayıyor (src/VidShrink.App/Recorder/DdaOutputs.cs:41) | Tümleşik ve ayrık kartı olan dizüstünde iş yanlış karta düşmez. | orta | 3 | gerekmez | [20][22] |
| AYR-38 | Pilde Duraklatma: Dizüstü pile geçince kuyruğu duraklatma seçeneği. | HandBrake | yok; `battery`, `PowerLineStatus`, `SystemPowerStatus` 0 eşleşme, en yakın kod kuyruk sonu eylemleri (src/VidShrink.App/QueueEndActions.cs:9) | Uzun kodlama pili bitirip işi yarıda bırakmaz. | küçük | 3 | gerekmez | [9][52] |
| AYR-39 | Disk Azalınca Duraklatma: Kodlama sürerken boş alan eşiğin altına inince kuyruğu duraklatma. | HandBrake | yarım: boş alan yalnız iş başlamadan denetleniyor (src/VidShrink.App/MainWindow.axaml.cs:4514) | Disk dolduğunda yarım dosya yerine duraklamış kuyruk kalır. | küçük | 3 | gerekmez | [52][56] |
| AYR-40 | Başlangıçta Çalıştırma: Oturum açılınca uygulamayı ya da kaydediciyi kendiliğinden başlatma seçeneği. | PowerToys, ShareX | yok; en yakın kod: kaydedici tepsi simgesi (src/VidShrink.App/Recorder/RecorderTray.cs:14) | Sık kayıt alan kişi kısayol tuşunu oturum açar açmaz kullanır. | küçük | 3 | sahip: açılışta arka planda çalışma yeni bir davranış | [1][63] |
| AYR-41 | Komut Paleti: Tek kısayolla açılan, bütün eylemleri adıyla aratıp çalıştıran kutu. | Windows Terminal | yok; en yakın kod: eylem ve kısayol eşlemesi tabloda duruyor (src/VidShrink.App/Playback/ShortcutBindings.cs:20) | Kısayolunu bilmeyen kişi eylemi yazarak bulur. | orta | 3 | sahip: yeni bir arayüz yüzeyi | [3][47] |
| AYR-42 | Ayar Dosyası Yedeği Ve Bozulmada Uyarı: Güncellemeden önce ayarın yedeğini alma, dosya okunamayınca yedekten dönme ve kullanıcıya söyleme. | PowerToys, ShareX | yarım: okunamayan ayar sessizce varsayılana düşüyor, yedek ve uyarı yok (src/VidShrink.App/AppSettings.cs:163) | Bozulan ayar dosyası kullanıcının bütün tercihlerini haber vermeden silmez. | küçük | 2 | gerekmez | [1][18] |
| AYR-43 | Kısayol Tablosunda Arama: Eylem adına ya da tuşa göre kısayol satırını süzme. | OBS Studio | yok; en yakın kod: tablo bütün satırları sırayla diziyor (src/VidShrink.App/Playback/ShortcutTable.cs:69) | Uzun tabloda aranan eylem ya da çakışan tuş hemen bulunur. | küçük | 3 | gerekmez | [72] |
| AYR-44 | Ayar Başına Bağlantı: Tek bir ayarı doğrudan açan, kopyalanabilir bağlantı. | PowerToys | yok; en yakın kod: Ayarlar tek kaydırmalı sayfa (src/VidShrink.App/MainWindow.axaml:1512) | Destek yanıtında "şu ayarı aç" demek tek bağlantıya iner. | küçük | 3 | gerekmez | [6] |
| AYR-45 | Kodlama Sürerken Uykuyu Engelleme: Kuyruk çalışırken bilgisayarın kendiliğinden uykuya geçmesini önleme. | HandBrake | yok; `SetThreadExecutionState`, `PowerCreateRequest`, `caffeinate`, `systemd-inhibit` 0 eşleşme, en yakın kod kuyruk sonu eylemleri (src/VidShrink.App/QueueEndActions.cs:9) | Uzun kodlama, bilgisayar boşta sayılıp uykuya geçtiği için yarıda kalmaz. | küçük | 3 | sahip: açık gelirse bilgisayarın uyku davranışı değişir | [52] |
| AYR-46 | Günlük Ayrıntı Düzeyi: Tanılama günlüğünün ne kadar ayrıntılı yazılacağını Ayarlar'dan seçme. | HandBrake | yok; `LogLevel`, `Verbos`, `AyrintiDuzeyi` günlük sınıfında, ayar deposunda ve Ayarlar sayfasında 0 eşleşme, en yakın kod tanılama günlüğü (src/VidShrink.Core/Gunluk.cs:28) | Zor yakalanan hatada ayrıntılı günlük açılır, sorun tek denemede görülür. | küçük | 3 | gerekmez | [52] |
| AYR-47 | Elle Güncelleme Denetimi: Ayarlar'da güncellemeyi hemen denetleyen "şimdi denetle" düğmesi. | OBS Studio, PowerToys; HandBrake'te yalnız denetim sıklığı seçimi | yarım: otomatik güncelleme kapalıyken denetim başlık çubuğundaki rozetle elle başlıyor; açıkken rozet gizleniyor ve kullanıcının elle başlatacağı yol kalmıyor (src/VidShrink.App/MainWindow.axaml.cs:2919), denetim ancak üçüncü sessiz düşüşte kendiliğinden yedek kipte koşuyor (src/VidShrink.App/MainWindow.KapanistaGuncelleme.cs:89) ve indirme hazır olunca rozet görünüyor (src/VidShrink.App/MainWindow.KapanistaGuncelleme.cs:161); Güncelleme kartında yalnız anahtar var (src/VidShrink.App/MainWindow.axaml:1719) | Kullanıcı güncellemenin çalıştığını kendi gözüyle görür; sessiz takılma fark edilir. | küçük | 2 | gerekmez | [72][73][1] |
| AYR-48 | Renk Körlüğü Seçeneği: Durum renklerini renk körlüğüne uygun bir takımla değiştirme seçeneği. | OBS Studio | yarım: uyarı durumu renkle değil simge ve ağırlıkla ayrılıyor (AGENTS.md:16); durumu yalnız renkle anlatan yer kalıp kalmadığına dair ölçüm docs/olcumler altında bulunmadı | Kırmızı ile yeşili ayıramayan kişi durum satırını yanlış okumaz. | orta | 3 | sahip: yeni renk takımı palet kararına dokunur | [72] |

## Düzeltmeler

Görev metninin ayrıca sorduğu üç şey:

- **Üç kod iddiası.** Pencere konumu saklanmıyor: doğru, ama pencere ekran ortasında değil ekranı kaplamış açılıyor (`AYR-03`). Bozuk ayar dosyası sessizce varsayılana düşüyor: doğru, yedek de uyarı da yok (`AYR-42`). Otomatik güncelleme açıkken elle denetim: sağlıklı durumda kullanıcının elle başlatabileceği bir yol gerçekten yok; ama üçüncü sessiz düşüşte denetim kendiliğinden yedek kipte koşuyor ve indirme hazır olunca rozet görünüyor. Bu, kök AGENTS.md'nin anlattığıyla uyumlu; satır çıkarılmadı, Bizde Durum düzeltildi (`AYR-47`).
- **İki sayım.** Sistem temasını izleyen kod yok (dört adla arama 0 eşleşme). Dil klasörü 42, palet klasörü 36 sayıldı; ikisi de tablodaki sayılarla aynı.
- **On iki satırın önceliği.** "Ana iş" ürünün ana işi olarak okundu: sıkıştırma, dönüştürme, kayıt, oynatma, düzenleme. Bu okumayla on iki satırın hiçbiri ana işe dokunmuyor ve hiçbirinin sayılır oy kanıtı yok (oyu okunan tek istek AYR-14'ün kaynağı LosslessCut #2580, 3 beğeni); hepsi 2 kaldı. Aynı okuma, gelen yedi öncelik 1 satırına da uygulandı: oyla kanıtlı üçü 1 kaldı, dördü 2'ye indi.

Oy, rakibin iş takipçisinde bir isteğe bırakılan beğeni işaretidir; sayılar `gh` ile 13 Ekim 2026'da okundu. Oy dalı yalnız isteği sonradan yerine getiren ürünlerde sayıldı; hiçbir üründe yapılmamış olan `AYR-31` bu yüzden 3 kaldı.

| No | Ne Değişti | Kanıt |
|---|---|---|
| `AYR-01` | Öncelik 1 kaldı: ana işe dokunmuyor, 1'i oy dalı taşıyor. Bizde Durum'a yedek için hazır dosya listesi eklendi. | PowerToys Genel sayfası açıldı: yedekleme ve geri yükleme bölümü var. ShareX değişiklik günlüğü açıldı: 13.1.0 ayar penceresine dışa ve içe aktarma sekmesini ekliyor, 13.2.0 dışa aktarma düğmesinden söz ediyor. Oy: PowerToys #586 kapalı ve tamamlanmış, 74 beğeni. Kod: src/VidShrink.App/AppDataReset.cs:18. |
| `AYR-02` | Öncelik 1 → 2. Üç üründe belgeli ama ana işe dokunmuyor, oy kanıtı zayıf. | PowerToys Genel sayfası: uygulama teması seçeneklerinde sistem varsayılanı var. Windows Terminal Görünüm sayfası: `theme` ayarı `system` değerini kabul ediyor. mpv.net kılavuzu: `--dark-mode=system`. Oy: ShareX #6679, 3 beğeni. Kod: `PlatformSettings`, `ColorValuesChanged`, `ThemeVariant.Default`, `AppsUseLightTheme` aramaları 0 eşleşme; src/VidShrink.App/App.axaml:5. |
| `AYR-03` | Öncelik 1 kaldı (oy dalı). Bizde Durum düzeltildi: pencere ekran ortasında değil, ekranı kaplamış açılıyor; 1560×1060 yalnız geri yüklenen boyut. Karar cümlesi buna göre yeniden yazıldı. | Windows Terminal Başlangıç sayfası (learn.microsoft.com/en-us/windows/terminal/customize-settings/startup; notlarda olmayan ikinci kaynak): `firstWindowPreference` ayarının `persistedWindowLayout` değeri kapanıştaki pencere düzenini saklayıp açılışta geri getiriyor; aynı sayfada `initialPosition` ve `launchMode` ayarları da var. mpv.net kılavuzu: `--remember-window-position`. Oy: Windows Terminal #766 kapalı, 254 beğeni; LosslessCut #44 kapalı ve tamamlanmış, 8 beğeni. Kod: src/VidShrink.App/MainWindow.axaml:13 `WindowState="Maximized"`; src/VidShrink.App/MainWindow.axaml.cs:3123-3162 yalnız `planPanelHeight`. |
| `AYR-04` | Öncelik 1 → 2. Altı üründe belgeli ama ana işe dokunmuyor, oy kanıtı yok. Satır numarası düzeltildi (README.md:346 → 348). | HandBrake katkı sayfası ve mpv.net kılavuzu açıldı: ikisi de çeviriyi Transifex'te yürütüyor. Kod: README.md:348; src/VidShrink.App/Locales altında 42 klasör sayıldı. |
| `AYR-05` | Öncelik 1 kaldı (oy dalı). Bizde Durum'a kurulumsuz zip'in zaten yayımlandığı eklendi. | Windows Terminal dağıtım sayfası: exe yanına `.portable` dosyası konunca ayarlar yandaki `settings` klasörüne yazılıyor. OBS başlatma parametreleri: `--portable`. mpv.net kılavuzu: `portable_config` klasörü. Oy: PowerToys #598 kapalı ve tamamlanmış, 55 beğeni. Yayın v1.2.16 dosya listesinde `vidshrink-win-x64.zip`. Kod: src/VidShrink.Core/UpdateCheck.cs:476-481. |
| `AYR-06` | Öncelik 1 → 2. Tam karşılığı iki üründe; LosslessCut yalnız yeni sürüm bağlantısı veriyor. Kimde Var düzeltildi, Bizde Durum'a yayın sayfasının ne zaman açıldığı eklendi. | PowerToys Genel sayfası: güncellemeden sonra yenilikleri gösterme anahtarı. OBS en-US.ini: `Basic.MainMenu.Help.WhatsNew` ve `ReleaseNotes`. LosslessCut deposundaki `menu.ts`: yenilik girdisi yok, yeni sürüm bağlantısı yayın sayfasını açıyor; LosslessCut #669, 0 beğeni. Kod: src/VidShrink.App/MainWindow.Guncelleme.cs:84. |
| `AYR-07` | Öncelik 1 → 2. Tam karşılığı iki üründe; OBS Studio'da hata bildir girdisi yok, yalnız günlüğü görme ve yükleme var. Kimde Var düzeltildi. | PowerToys Genel sayfası: hata raporu paketi üreten ve önceden doldurulmuş GitHub kaydı açan düğme. LosslessCut deposundaki `menu.ts`: Yardım menüsünde hata bildirme girdisi. OBS en-US.ini: Yardım menüsü dizgilerinde hata bildir girdisi yok; `Logs.ViewCurrentLog` ve `Logs.UploadCurrentLog` var. Kod: `.github` altında yalnız `workflows`; `issues/new`, `reportbug`, `hatabildir` aramaları uygulama kaynağında 0 eşleşme. |
| `AYR-08` | Öncelik 2 kaldı. Dört üründe belgeli; ama ayar araması 12 kartlık tek sayfada bir kolaylık, ana işe dokunmuyor, oy kanıtı yok. | PowerToys Genel sayfasında ayar araması görüldü. Kod: src/VidShrink.App/MainWindow.axaml:1512. |
| `AYR-09` | Öncelik 2 kaldı. Üç üründe belgeli; yayın mekaniği, ana işe dokunmuyor; kardeş tabloda da 2. Bizde Durum `yok` iken `yarım` oldu: ön sürüm eki zaten ayrıştırılıyor. | ShareX değişiklik günlüğü 16.1.0: güncelleme kanalı seçeneği. Kod: src/VidShrink.Core/UpdateCheck.cs:286. |
| `AYR-10` | Öncelik 2 kaldı. Üç üründe belgeli; tanı kolaylığı, ana işe dokunmuyor. Bizde Durum düzeltildi: düğme dosyayı dış programda açmıyor, dosya yöneticisinde seçili gösteriyor; okuyan yöntem hazır. | OBS en-US.ini: `Basic.MainMenu.Help.Logs.ViewCurrentLog`. Kod: src/VidShrink.App/Platform.cs:9-26; src/VidShrink.Core/Gunluk.cs:128. |
| `AYR-11` | Kimde Var'a LosslessCut eklendi. Öncelik 3 kaldı: ürün sayısı ikiye çıktı ama lisans metnini uygulamadan okuyan kitle dar. | LosslessCut deposundaki `menu.ts`: Yardım menüsünde lisanslar girdisi. Kod: README.md:399-405. |
| `AYR-14` | Öncelik 2 kaldı. Üç üründe belgeli; görünüm tercihi, ana işe dokunmuyor. Satır numarası düzeltildi (374 → 377). | Windows Terminal Görünüm sayfası: `disableAnimations`. Kod: src/VidShrink.App/MainWindow.axaml.cs:377. |
| `AYR-16` | Öncelik 2 kaldı. Üç üründe belgeli; bir deneme işi, kullanıcıya yeni yetenek vermiyor, ana işe dokunmuyor. | Kod: `AutomationProperties` uygulama genelinde 283, src/VidShrink.App/MainWindow.axaml:1504-1900 arasında 16 sayıldı; docs/olcumler altında ekran okuyucu ya da klavye denemesi kaydı yok; adında karşıtlık geçen tek ölçüm var (docs/olcumler/dugme-kenari-karsitlik.md). |
| `AYR-17` | Öncelik 2 → 3. Hiçbir rakipte belgeli değil; ölçütün 2 için istediği iki koşul da tutmuyor. Deneme yine AYR-P7'ye bedavaya giriyor. | Satırın kendi Kimde Var hücresi; kaynakları [27][23][75] yönerge, rakip belgesi değil. Kod: `TabIndex`, `KeyboardNavigation`, `IsTabStop` aramaları 0 eşleşme. |
| `AYR-18` | Öncelik 2 kaldı. Üç üründe belgeli; kabuk gezinmesi, ana işe dokunmuyor. | Kod: src/VidShrink.App/Playback/ShortcutBindings.cs:14-20 yalnız `Player` ve `Editor` takımı; src/VidShrink.App/MainWindow.PencereKipi.cs:41. |
| `AYR-20` | Öncelik 2 kaldı. Üç üründe belgeli; iş sonunun kenarında, çıktıyı etkilemiyor. | ShareX değişiklik günlüğü 17.0.0: işlem bitince ses çalma seçeneği. Kod: src/VidShrink.App/Recorder/RecorderInput.cs:151. |
| `AYR-21` | Öncelik 2 kaldı. Üç üründe belgeli; on iki satır içinde ana işe en yakın olan bu, ama çıktıyı değil makinenin öteki işlerini etkiliyor, yani kenarda. Sahip yükseltecekse ilk aday. Bizde Durum düzeltildi: `BelowNormal` yalnız güncelleme sürecinde değil, yardımcı süreçlerde de var; kodlama sürecinde yok. | Kod: `PriorityClass` araması src/VidShrink.Core/Editing/ThumbnailQueue.cs:276, src/VidShrink.Core/Editing/SilenceCut.cs:215, src/VidShrink.Ffmpeg/SceneDetector.cs:123 ve src/VidShrink.Ffmpeg/AudioLevelSource.cs:88 satırlarını buldu; src/VidShrink.Ffmpeg/EncodeRunner.cs içinde eşleşme yok. |
| `AYR-22` | Öncelik 2 kaldı. Üç üründe belgeli; pencere davranışı, ana işe dokunmuyor. | OBS başlatma parametreleri: `--minimize-to-tray`. Kod: src/VidShrink.App/Recorder/RecorderTray.cs:74-106. |
| `AYR-23` | Öncelik 2 kaldı. Üç üründe belgeli; ilk açılış yardımı, ana işe dokunmuyor. | Kod: src/VidShrink.App/Recorder/RecorderSettings.cs:271; src/VidShrink.App/MainWindow.axaml.cs:362-372. |
| `AYR-24` | Öncelik 2 kaldı. Dört üründe belgeli; belge işi, ana işe dokunmuyor. Bizde Durum düzeltildi: "belge yalnız README" yanlıştı; iki dilde, beşer ekran görüntülü kullanım belgesi depoda var. | docs/kullanim.md:1 ve :64; docs/kullanim.tr.md:1; README.md:373. |
| `AYR-25` | Öncelik 2 kaldı. Üç üründe belgeli; çevirinin sunumu, ana işe dokunmuyor. | Kod: src/VidShrink.App/Localization/Strings.cs:62-93; aynı dosyadaki `UntrackedLanguages` (satır 123) harf aralığıyla ilgili, çeviri tamlığını işaretlemiyor. |
| `AYR-26` | Bizde Durum'a açılacak belgenin depoda hazır olduğu eklendi. | docs/kullanim.md:1. |
| `AYR-33` | Bizde Durum'a tuş çakışması eklendi: F6 kaydedicinin bölüm işareti kısayolu. | src/VidShrink.App/Recorder/RecorderHotkeys.cs:42. |
| `AYR-42` | Öncelik 3 → 2: ikinci rakip bulundu ve eksik dar kitleyi değil herkesi vuruyor. Kimde Var'a ShareX, Kaynak'a [18] eklendi. Kod iddiası doğru. | PowerToys Genel sayfası: ayar dosyaları her güncellemeden önce yedekleniyor, bozulma görülürse geri yükleniyor. ShareX değişiklik günlüğü 13.2.0: ayar yüklenemezse geçici dosyaya ve haftalık yedeğe düşüyor. Kod: src/VidShrink.App/AppSettings.cs:163-168. |
| `AYR-47` | Öncelik 3 → 2: ikinci rakip bulundu. Kimde Var'a PowerToys, Kaynak'a [1] eklendi. Bizde Durum düzeltildi: denetim otomatik güncelleme açıkken hiç koşmuyor değil; üçüncü sessiz düşüşte yedek kipte kendiliğinden koşuyor. Eksik olan, kullanıcının sağlıklı durumda elle başlatacağı yol. | OBS en-US.ini: `Basic.MainMenu.Help.CheckForUpdates`. PowerToys Genel sayfası: güncellemeyi elle denetleme. Kod: src/VidShrink.App/MainWindow.axaml.cs:2917-2923; src/VidShrink.App/MainWindow.KapanistaGuncelleme.cs:89, :93-98, :161, :177. |

## Çıkarılanlar

| No | Eksik | Neden | Kanıt (dosya:satır) |
|---|---|---|---|

Çıkarılan satır yok. Kodda tam karşılığı bulunan satır çıkmadı; on üç satırın Bizde Durum'u düzeltildi ya da genişletildi.

## Yinelenenler

- AYR-05 = DAG-10 — taşınabilir kip. Burada öncelik 1, dağıtım tablosunda 2; iki tablo birleştirilirken tek değer seçilmeli.
- AYR-09 = DAG-24 — güncelleme kanalı seçimi. İki tabloda da öncelik 2 ve sahip kararı.

Yakın ama aynı değil: AYR-01 bütün ayarın yedeğini ister, DAG-33 yalnız ön ayarın komut satırından dışa aktarımını; AYR-47 elle denetim düğmesini ister, DAG-40 kurumsal ilke anahtarını; AYR-35 iş bitince tek bir komut çalıştırır, DAG-36 betik ve eklenti arabirimi ister.

## Doğrulanamayanlar

- AYR-04: OBS Studio'nun Crowdin'i, LosslessCut'ın Weblate'i kullandığı bu turda görülemedi (sayfalar 404 ve 403 döndü). Satır HandBrake ve mpv.net üzerinden doğrulandı; Kimde Var olduğu gibi bırakıldı.
- AYR-01: ShareX'in ayar dışa aktarma ekranı görülmedi; kanıt yalnız değişiklik günlüğü satırları. Oy kanıtı tek üründen (PowerToys) geliyor; sahip video dışı ürünün oyunu saymazsa satır 2 olur.
- AYR-05: Oy kanıtı tek üründen (PowerToys) geliyor ve kardeş tablo aynı eksiğe 2 vermiş; öncelik 1 bırakıldı, son söz birleştirmede.
- AYR-17: Rakiplerde klavyeyle tam kullanım belgesi bu turda aranmadı. Satır ölçüte göre 3'e indi; kanıt bulunursa 2'ye döner.
- AYR-48: Durumu yalnız renkle anlatan yer kalıp kalmadığı ölçülmedi; araştırmanın notu olduğu gibi duruyor.
- Öncelik 2 ve 3 satırların rakip iddiaları tek tek açılmadı. Yalnız öncelik 1 satırlar için açılan sayfalarda rastlananlar görüldü: AYR-08, AYR-09, AYR-10, AYR-11, AYR-14, AYR-20, AYR-22, AYR-40, AYR-42, AYR-47.
- `tests/VidShrink.Tests` altındaki ölçüler bu satırlar için taranamadı; "bizde var" kanıtı yalnız `src`, `docs` ve README'den geliyor.

## Ek Notlar

- Yedek denetimin bıraktığı rozete tıklanınca, yeni sürüm durumu dışında, rozet denetim yapmadan yeniden gizleniyor: tıklama denetimi yedek kip bayrağı olmadan çağırıyor (src/VidShrink.App/MainWindow.axaml.cs:2914 ve :2919-2922). Kod okumasıyla görüldü, çalıştırılıp denenmedi.
- Kodlama ilerlemesi görev çubuğu simgesinde gösterilmiyor: `ITaskbarList`, `SetProgressValue`, `TaskbarProgress` aramaları `src` altında 0 eşleşme. Tabloda satırı yok; rakip kanıtı bu turda aranmadı.
- `AnimationsAllowed` iki ayrı kopya halinde duruyor (src/VidShrink.App/MainWindow.axaml.cs:377 ve src/VidShrink.App/Playback/HoverZone.cs:260); AYR-14 ve AYR-15 yapılırken ikisi birlikte değişmeli.
- Defterde "rozet tıkı yalnız indirsin mi, kursun mu" sorusu açık duruyor (.claude/acik.md:101); AYR-47 yapılırken birlikte ele alınmalı.
- Araştırma notundaki "otomatik güncelleme açıkken elle denetim koşmaz, rozet gizlenir" cümlesi (ayarlar-kabuk.md, 87. satır) eksik: üçüncü sessiz düşüşte denetim yedek kipte koşuyor ve indirme hazır olunca rozet görünüyor (src/VidShrink.App/MainWindow.KapanistaGuncelleme.cs:89 ve :161).
- Açılan web sayfalarında ve okunan dosyalarda bana yönelik yazılmış bir yönergeye rastlanmadı. Oturum açılış kancası defteri işaretlememi istedi; görev depoya yazmayı yasakladığı için uygulanmadı.

## Sekme Özeti

- Görünüm ve dil sayısında güçlüyüz: 36 palet, 42 dil klasörü, oynatıcı ve düzenleyicide düzenlenebilir kısayol tablosu, düşüşünü sayan sessiz güncelleme.
- Rakiplerde yaygın olup bizde olmayanlar ayarın korunması ve kullanıcıyı bilgilendirme tarafında toplanıyor: ayar yedeği yok, bozuk ayar sessizce sıfırlanıyor, pencere yerini hatırlamıyor, güncellemenin ne getirdiği gösterilmiyor.
- Erişilebilirlikte kod hazırlığı var (283 erişilebilir ad, odak halkası) ama hiçbir deneme kaydı yok; durum ölçüyle değil tahminle biliniyor.
- En ağır üç eksik: ayar yedeği ile bozulmada sessiz düşüş (AYR-01, AYR-42), pencere konumunu hatırlamama (AYR-03), taşınabilir kip (AYR-05).
- En ucuz üç kazanç: "şimdi denetle" düğmesi (AYR-47), günlüğü uygulama içinde okuma (AYR-10; okuyan yöntem hazır), Hakkında'ya sürüm notu, lisans ve gizlilik bağlantıları (AYR-06, AYR-11, AYR-29).
- Öncelik 1 ya da 2 olan 25 satırın 15'i karar beklemeden altı pakette yapılabilir; 10'u altı pakette sahibin kararını bekliyor.

## Paket Önerisi

Sıra önerilen yapım sırasıdır. Ekran okuyucu, ekrandaki metni sesli okuyan yardımcı programdır; yedek kip, otomatik güncelleme üç kez sessizce düşünce uygulamanın elle denetime dönmesidir. Paket sayısı on iki sınırında olduğu için üç sahip paketi ikişer satır taşıyor; planları hangi satırın önce teslim edileceğini söylüyor.

### AYR-P1 — Pencere Konumunu Hatırlama
- Satırlar: AYR-03
- İş: küçük
- Karar: sahip: Uygulama her açılışta ekranı kaplamış açılmak yerine son konumunda, boyutunda ve durumunda mı açılsın?
- Dokunacağı yer: src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs
- Plan: Pencerenin konumu, boyutu, ekranı ve kaplama durumu kapanışta, bugün yalnız panel yüksekliğini tutan yerleşim dosyasına yazılır ve açılışta oradan okunur. Kayıtlı konum bağlı ekranların hiçbirine düşmüyorsa bugünkü açılışa dönülür. Doğrulama: pencere açmadan koşan arayüz testiyle kaydet ve geri yükle turu, bir de sökülmüş ikinci ekran durumu.

### AYR-P2 — Ayar Yedeği Ve Bozulma Uyarısı
- Satırlar: AYR-01, AYR-42
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.App/AppSettings.cs, src/VidShrink.App/AppDataReset.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/Locales/
- Plan: Ayarlar'a "dışa aktar" ve "içe aktar" düğmeleri eklenir; paket, sıfırlamanın zaten saydığı dosya listesinden tek zip olarak üretilir, gizli anahtar taşıyan dosyalar dışarıda kalır. Ayar dosyası her başarılı okumadan sonra bir yedek kopyaya yazılır; dosya okunamazsa yedekten dönülür, bozuk dosya silinmeyip yanına ayrılır ve kullanıcıya uyarı durumuyla söylenir. Doğrulama: dışa aktar, sıfırla, içe aktar turunun testi ve bilerek bozulmuş `settings.json` ile açılış testi.

### AYR-P3 — Güncelleme, Hakkında Ve Tanılama
- Satırlar: AYR-06, AYR-07, AYR-10, AYR-47, AYR-11, AYR-27, AYR-29
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/MainWindow.Guncelleme.cs, src/VidShrink.Core/Gunluk.cs, .github/ISSUE_TEMPLATE/, src/VidShrink.App/Locales/
- Plan: Güncelleme kartına otomatik güncelleme açıkken de çalışan "şimdi denetle" düğmesi ve son denetimin sonucu eklenir; güncellemeden sonraki ilk açılışta sürüm notlarına giden kısa bir bildirim gösterilir. Hakkında'ya sürüm notları, lisanslar ve gizlilik beyanı bağlantıları konur. Tanılama kartında günlük uygulama içinde okunur ve kopyalanır; "hata bildir" düğmesi sürümü, işletim sistemini ve günlük yolunu taşıyan hazır kaydı açar, yanındaki düğme günlüğü ve sistem bilgisini tek zip'e toplar. Doğrulama: her düğmenin açtığı adresi ve ürettiği metni ölçen testler ile 42 dilde dizgi tamlığı testi.

### AYR-P4 — Sistem Teması Ve Hareket Ayarı
- Satırlar: AYR-02, AYR-14, AYR-15
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.App/Themes/PaletteCatalog.cs, src/VidShrink.App/AppSettings.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/Playback/HoverZone.cs
- Plan: Görünüm kartına kapalı gelen "sistem temasını izle" seçeneği eklenir; açıkken kullanıcının seçtiği bir açık ve bir koyu palet arasında Avalonia'nın sistem renk olayıyla geçilir, geçiş yine yalnız `PaletteCatalog.Use` üstünden yapılır. `AnimationsAllowed` iki kopyadan tek yere toplanır, macOS ve Linux'ta sistemin "hareketi azalt" ayarını okur ve Ayarlar'daki elle "canlandırmaları kapat" anahtarını da dinler. Doğrulama: palet geçişi ve üç işletim sistemi kolu için birim testleri; sistem ayarı teste sahte kaynakla verilir.

### AYR-P5 — Uzun İşte Öncelik, Bildirim Ve Ses
- Satırlar: AYR-19, AYR-20, AYR-21
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.App/IsBittiBildirimi.cs, src/VidShrink.Ffmpeg/EncodeRunner.cs, src/VidShrink.Ffmpeg/FfmpegRunner.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/Locales/
- Plan: Ayarlar'a kodlama sürecinin önceliği (düşük, normal, yüksek; varsayılan bugünkü gibi normal) eklenir ve süreç başlarken uygulanır. İş bitti bildirimi macOS ve Linux'ta sistemin kendi bildirim yoluyla gösterilir; kapalı gelen bir "bitince ses çal" seçeneği ve ses dosyası seçimi eklenir. Doğrulama: öncelik için süreç başlatma testi, bildirim için işletim sistemi başına komut üreten katmanın testi; gerçek bildirim macOS ve Linux'ta bir kez elle görülür.

### AYR-P6 — Kabuk Kısayolları Ve Ayar Araması
- Satırlar: AYR-08, AYR-18, AYR-26, AYR-33, AYR-43
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.App/Playback/ShortcutBindings.cs, src/VidShrink.App/Playback/ShortcutTable.cs, src/VidShrink.App/MainWindow.PencereKipi.cs, src/VidShrink.App/MainWindow.axaml
- Plan: Kısayol eşlemesine oynatıcı ve düzenleyicinin yanına bir kabuk takımı eklenir: sekmeler arası geçiş, Ayarlar, dosya açma, bölgeler arası geçiş ve kullanım belgesini açan yardım tuşu; hepsi aynı düzenlenebilir tabloda görünür. Ayarlar sayfasına ve kısayol tablosuna aynı süzme kutusu konur. Bölge geçişinin varsayılan tuşu F6 olamaz, çünkü kaydedici onu bölüm işareti için tutuyor. Doğrulama: tuş eşleme ve çakışma testleri ile süzme kutusu için kart görünürlüğü testi.

### AYR-P7 — Erişilebilirlik Denemesi
- Satırlar: AYR-16, AYR-17
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: docs/olcumler/, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/Themes/Controls.axaml
- Plan: Her sekmede bir ana iş baştan sona önce yalnız klavyeyle, sonra ekran okuyucuyla (Windows'ta Anlatıcı ve NVDA) yapılır; sekme sırası, klavye tuzağı ve adı okunmayan denetimler tek tek yazılır. Bulgular `docs/olcumler` altına kayıt olarak girer; adı eksik denetim ve bozuk sekme sırası gibi küçük kusurlar aynı turda düzeltilir, büyükleri deftere yazılır. macOS'ta VoiceOver denemesi o makinede ayrıca yapılır.

### AYR-P8 — Taşınabilir Kip Ve Güncelleme Kanalı
- Satırlar: AYR-05, AYR-09
- İş: büyük
- Karar: sahip: Yayına iki yeni biçim eklensin mi: ayarını yanında taşıyan kurulumsuz paket ve gönüllülere açık ön sürüm kanalı?
- Dokunacağı yer: src/VidShrink.Core/UpdateCheck.cs, src/VidShrink.App/MainWindow.axaml, .github/workflows/, docs/kurulum.md
- Plan: Önce AYR-05: exe'nin yanındaki bir işaret dosyası ayar ve günlük yolunu uygulama klasörüne çevirir; bu kipte güncellemenin nasıl davranacağı sahibin kararına göre kurulur. Sonra AYR-09: Güncelleme kartına kararlı ve ön sürüm seçimi eklenir, denetim seçilen kanalın yayınlarına bakar. İki satır dağıtım tablosundaki DAG-10 ve DAG-24 ile aynı iştir; orada DAG-24 kendi paketinde, DAG-10 ise DAG-11 ve DAG-20 ile aynı pakette duruyor, birleştirmede bu paketlerle tek işe indirilmelidir. Doğrulama: işaret dosyasıyla ayar yolunun testi ve kanal seçiminin sürüm karşılaştırma testi.

### AYR-P9 — Tepsiye Küçültme
- Satırlar: AYR-22
- İş: küçük
- Karar: sahip: Ana pencere için kapalı gelen bir "tepsiye küçült" seçeneği eklensin mi; eklenecekse kapatma düğmesi de tepsiye mi gitsin?
- Dokunacağı yer: src/VidShrink.App/Recorder/RecorderTray.cs, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/MainWindow.axaml
- Plan: Kaydedicinin tepsi simgesi kodu ana pencerenin de kullanabileceği ortak bir parçaya çıkarılır. Seçenek açıkken küçültülen pencere görev çubuğundan kalkar, tepsi simgesine tıklanınca geri gelir; kuyruk ilerlemesi simgenin ipucunda yazar. Doğrulama: pencere durumu geçişlerinin testi; tepsi simgesi Windows'ta bir kez elle görülür.

### AYR-P10 — Çeviri Katkısı Ve Tamlık İşareti
- Satırlar: AYR-04, AYR-25
- İş: orta
- Karar: sahip: 42 dil sözü nasıl sunulsun: gözden geçmemiş diller listede işaretlensin mi ve bir çeviri sitesinde proje hesabı açılsın mı?
- Dokunacağı yer: src/VidShrink.App/Localization/Strings.cs, src/VidShrink.App/Locales/, README.md, .github/workflows/
- Plan: Önce küçük adım olan AYR-25: insan gözünden geçmiş diller bir listede tutulur, öteki diller dil seçicide işaretlenir. Sonra AYR-04: sahibin seçtiği çeviri sitesi (Weblate, Crowdin ya da Transifex) depodaki `Locales` klasörüne bağlanır ve README'deki katkı yolu oraya çevrilir. Doğrulama: işaretli dil listesinin testi ve siteden gelen ilk deneme çevirisinin dizgi tamlığı testinden geçmesi.

### AYR-P11 — Arayüz Ölçeği Ve Yüksek Karşıtlık
- Satırlar: AYR-12, AYR-13
- İş: büyük
- Karar: sahip: "Renk yalnız paletten, ölçü yalnız belirteçten" kuralına sistemin karşıtlık ve yazı boyutu ayarları için istisna açılsın mı?
- Dokunacağı yer: src/VidShrink.App/Themes/Theme.axaml, src/VidShrink.App/Themes/PaletteCatalog.cs, src/VidShrink.App/Themes/Palette/, src/VidShrink.App/MainWindow.axaml
- Plan: Önce AYR-13: sistem yüksek karşıtlık istediğinde seçilecek bir yüksek karşıtlıklı palet eklenir ve geçiş `PaletteCatalog.Use` üstünden yapılır. Sonra AYR-12: yazı boyutu belirteçlerine tek bir ölçek çarpanı bağlanır, Ayarlar'a yazı boyutu seçimi konur ve en küçük pencere boyutunda taşma her sekmede ölçülür. Doğrulama: karşıtlık oranı ölçümü ve pinli yerleşim ölçülerinin her ölçekte yeniden koşulması.

### AYR-P12 — İlk Açılış Karşılaması Ve Kılavuz
- Satırlar: AYR-23, AYR-24
- İş: büyük
- Karar: sahip: Yeni kullanıcıya yardım nasıl sunulsun: ilk açılışta bir karşılama ekranı ve dillerde sürdürülen çevrimiçi kılavuz sitesi açılsın mı?
- Dokunacağı yer: docs/kullanim.md, docs/kullanim.tr.md, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, README.md
- Plan: Önce AYR-24: depodaki iki dilli kullanım belgesi sekme sekme tamamlanır ve sahibin seçtiği yerde site olarak yayımlanır. Sonra AYR-23: ilk açılışta bir kez görünen, beş işi gösteren ve kılavuza bağlanan kısa karşılama eklenir; kapatılınca bir daha çıkmaz. Doğrulama: karşılamanın yalnız ilk açılışta göründüğünün testi ve belgedeki her ekran görüntüsünün güncel sürümden alınması.

## Pakete Girmeyenler

Hepsi öncelik 3; hiçbiri bir paketin yanında bedavaya gelmiyor.

- AYR-28: Kodlama başına günlük, kodlama koşucusuna ve günlük dosyası düzenine dokunur; hiçbir paketle ortak dosyası yok.
- AYR-30: Tek rakipte var, büyük iş ve README'deki "No telemetry" sözünü değiştirir.
- AYR-31: Hiçbir üründe yapılmış hali görülmedi, hesap ya da bulut hizmeti ister; AYR-P2 bitince dışa aktarılan dosya elle taşınarak aynı ihtiyacı karşılar.
- AYR-32: Erişim tuşları 42 dilde harf seçimi ister; AYR-P7'nin denemesi bittikten sonra bulgularla birlikte ele alınmalı.
- AYR-34: Tek rakipte var; posta sunucusu bilgisi saklamak ve yeni ağ isteği ister.
- AYR-35: Tek rakipte var; kullanıcının yazdığı komutu çalıştırmak ayrı bir sahip kararı.
- AYR-36: İş parçacığı sınırı her kodlayıcıda ayrı seçenekle verilir ve motorun argüman üretimine dokunur; AYR-P5'in yanında bedavaya gelmiyor.
- AYR-37: Dar kitle (birden çok ekran kartı olan makine); kaydedicinin ve kodlayıcı seçiminin içine dokunur.
- AYR-38: Tek rakipte var; kuyruğu duraklatan bir düzenek ister, o düzenek bugün yok.
- AYR-39: AYR-38 ile aynı duraklatma düzeneğini ister; ikisi birlikte ayrı bir küçük iş eder.
- AYR-40: Ayrı sahip kararı ve üç işletim sisteminde ayrı kayıt yolu ister; AYR-P9'un yanında bedavaya gelmiyor.
- AYR-41: Tek rakipte var ve yeni bir arayüz yüzeyi; AYR-P6'nın kabuk takımı bittikten sonra onun üstüne kurulabilir.
- AYR-44: Tek rakipte var; uygulamaya bağlantı şeması kaydı ister (`vidshrink://` araması 0 eşleşme).
- AYR-45: Tek rakipte belgeli ve sahip kararı ister; öncelik 3 satırlar içinde ana işi doğrudan koruyan tek satır bu, sahip öne almak isterse AYR-P5'ten sonra ayrı küçük paket olur.
- AYR-46: Günlük sınıfına düzey kavramı ve çağrı yerlerinde ayrım ister; AYR-P3'ün yanında bedavaya gelmiyor.
- AYR-48: Tek rakipte var ve palet kararına dokunur; durumu yalnız renkle anlatan yer kalıp kalmadığı önce ölçülmeli.
