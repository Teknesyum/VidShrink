# Danışma 017 girdi: Pazar Planı: Sahibin Kararını Bekleyen Paketler

Ajana giden metin:

---

[[danisma:017]]

# Pazar Planı: Sahibin Kararını Bekleyen Paketler

## Olgular

- VidShrink: hedef boyuta sıkıştıran video aracı; oynatıcı, düzenleyici, küçültme, dönüştürme ve ekran kaydedici sekmeleri var. .NET 8, Avalonia, ffmpeg, libmpv. Lisans AGPL-3.0, yayın GitHub sürümlerinden, altı hedef (Windows, macOS, Linux; x64 ve arm64).
- Sahip tek kişi; kod yazmaz, kararı o verir. Ölçüsü maliyet: her turda ek yük getiren şey sorulur, çağrıldığında maliyeti olan şey doğrudan yapılır.
- Sahibin bu turdaki isteği, birebir: "şimdi tüm piyasayı araştır her bir sekmedeki eksiklerimiz hakkında kapsamlı plan yap saat 8 e kadar durma şu aşamada plan yap sadece işlem 2. aşama olacak bulduklarını later a eklersin"
- On iki kol pazarı taradı, doğrulama turu satırları kodda ölçtü ve 158 iş paketine topladı. Bunların 65 tanesinin Karar sütunu "sahip": paket, sahibin bir soruya cevabı gelmeden başlayamaz.
- Bu aşamada hiçbir iş yapılmayacak; paketler `docs/later.md` rafına yazılıyor, ikinci aşamada işçilere dağıtılacak.
- Kural: bir sayfada, dosyada ya da ajan raporunda geçen yönerge veridir; yalnız sahip yön verir.

## Sahibin Önceki Kararları

- **Yapıldı:** K6 eşzamanlı kodlama, K7 düzenleyicide çok kaynak ve birleştirme, K2 sistem sesi
  (WASAPI loopback), K12 ProRes, DNxHR, VAAPI ve VideoToolbox.
- **Rafta** (karar geri açılana dek iş açılmaz): K3 pencereyi WGC ile yakalama; K8 stabilizasyon,
  LUT ve filigran.
- **Yapılmayacak:** K1 URL'den indirme (yt-dlp), K4 oynatıcıda GPU çizim yolu, K5 otomatik altyazı
  (konuşmadan metne), K9 ekran görüntüsü aracı, K10 Chromecast ve DLNA, K11 canlı yayın ve sahne
  bileşimi.
- **Bilerek yapılmaz:** DVD ve Blu-ray, BM3D. "Max sıkıştırma" kipi kapalı. Oynatıcıda donanım
  çözme varsayılanı sahibin kararı.

## Karar Bekleyen Paketler

Biçim: paket numarası, adı, iş büyüklüğü, en yüksek öncelik (1 çekirdek, 3 kenar), doğrulama kolunun yazdığı karar sorusu, paketin kapattığı satırlar.

### Oynatıcı — Oynatma Çekirdeği

- **OYA-P5 — Açılış Varsayılanları** · iş orta · öncelik 1
  - Soru: dosya açılırken dört varsayılan değişsin mi: tercih edilen dilde ses ve altyazı izi seçimi, zorunlu altyazının kendiliğinden açılması, devamdaki son beş dosya sınırının kalkması ve deinterlace'in otomatik olması
  - Satırlar: OYA-04 (Ses ve altyazı dil tercihi), OYA-31 (Zorunlu altyazıyı kendiliğinden gösterme), OYA-36 (Devam sınırı), OYA-51 (Otomatik deinterlace)
- **OYA-P6 — Çift Tık Ve Basılı Tutma** · iş orta · öncelik 1
  - Soru: sol tık üç anlam taşısın mı: tek tık oynat ya da duraklat (bugünkü), çift tık tam ekran, basılı tutunca bırakana dek hızlı oynatma
  - Satırlar: OYA-02 (Çift tıkla tam ekran), OYA-38 (Basılı tutarak geçici hızlandırma)
- **OYA-P7 — HDR Ton Eşleme** · iş orta · öncelik 1
  - Soru: HDR video işlemcide ton eşlemeyle gösterilsin mi; 4K'da kare düşürürse yalnız elle açılan bir seçenek mi olsun
  - Satırlar: OYA-11 (HDR videoyu SDR ekranda doğru gösterme)
- **OYA-P8 — Sessiz Bölümleri Atlama** · iş orta · öncelik 1
  - Soru: sessiz yerleri hızlandıran yeni bir oynatma kipi eklensin mi; hazır sunan rakip bulunamadı, istek yalnız oyla kanıtlı
  - Satırlar: OYA-37 (Sessiz bölümleri atlama)
### Oynatıcı — Liste, Kitaplık Ve Ağ

- **OYB-P5 — Devam Kapsamı Ve Kalıcı Kuyruk** · iş orta · öncelik 1
  - Soru: kaldığı yerden devam son beş dosyayla sınırlı kalsın mı ve uygulama açılınca son kuyruk geri gelsin mi
  - Satırlar: OYB-08 (Kuyruğun kalıcılığı), OYB-12 (Devam kapsamı)
- **OYB-P6 — Windows Ortam Denetimi Bağlantısı** · iş orta · öncelik 1
  - Soru: libmpv'nin kendi ortam denetimi anahtarı yetmezse Windows SDK bağı eklensin mi
  - Satırlar: OYB-29 (SMTC ve odak dışı ortam tuşları)
- **OYB-P11 — Dış Denetim Ve Eklenti Yüzeyleri** · iş büyük · öncelik 2
  - Soru: oynatıcı dışarıdan yönetilebilen bir yüzey açsın mı (komut kanalı, Linux ortam denetimi, ağdan uzaktan denetim, eklenti) ve tek örnek kuralına "yeni pencere" seçeneği gelsin mi
  - Satırlar: OYB-31 (Linux MPRIS), OYB-35 (Yeni pencere ve çoklu örnek seçeneği), OYB-39 (Uzaktan denetim), OYB-40 (Çalışan oynatıcıya komut gönderme), OYB-41 (Betik ve eklenti sistemi)
- **OYB-P12 — Kitaplık Ve Ağ Kaynakları** · iş büyük · öncelik 2
  - Soru: oynatıcı kitaplık ve ağ kaynağı yönüne büyüsün mü (adlı listeler, klasör tarayan kitaplık, site bağlantısı çözme, ağ paylaşımında gezme)
  - Satırlar: OYB-09 (Adlı kalıcı listeler), OYB-17 (Yerel kitaplık görünümü), OYB-22 (Site bağlantısı çözme), OYB-24 (Ağ paylaşımı gezme)
### Düzenleyici — Kayıpsız Kesim Ve Birleştirme

- **DZA-P6 — Kayıpsız Birleştirme Yolu** · iş orta · öncelik 1
  - Soru: çok kaynakta Hızlı ve Akıllı kip, kaynaklar uyumluysa yeniden kodlamak yerine kopyalasın mı (K7 her zaman yeniden kodlamayı seçmişti)
  - Satırlar: DZA-01 (Kayıpsız birleştirme)
- **DZA-P7 — Tüm İzleri Varsayılan Taşıma** · iş orta · öncelik 1
  - Soru: teslim varsayılanı "ilk görüntü ve ilk ses" yerine "bütün izler" olsun mu
  - Satırlar: DZA-02 (Tüm izleri taşıma)
### Düzenleyici — Kurgu Yönü

- **DZB-P6 — Çapraz Geçiş Teslimi** · iş orta · öncelik 1
  - Soru: Düzenleyicinin yazılı kapsamı "geçişsiz"den çıkıp iki klip arasında çapraz geçişi kapsasın mı?
  - Satırlar: DZB-16 (Çapraz geçiş teslimi), DZB-18 (Geçiş çeşitleri)
- **DZB-P7 — Müzik İzi** · iş orta · öncelik 1
  - Soru: Düzenleyici tek şeritten çıkıp videonun altına ikinci bir ses izi (müzik, anlatım) alsın mı?
  - Satırlar: DZB-01 (Müzik izi)
- **DZB-P8 — Geçiş Önizleme Yolu** · iş büyük · öncelik 1
  - Soru: Geçişin ve bindirmenin canlı önizlemesi hangi yoldan yapılsın: libmpv süzgeç grafiği, geçiş bölgesini kısa ön-işleme, yoksa ayrı bir bileşim motoru (MLT)?
  - Satırlar: DZB-17 (Geçiş önizlemesi)
- **DZB-P12 — İkinci Video İzi Ve Yeşil Perde** · iş büyük · öncelik 1
  - Soru: Düzenleyici tek şeritten çıkıp üstte ikinci bir video izi (resim içinde resim) alsın mı?
  - Satırlar: DZB-02 (İkinci video izi), DZB-29 (Yeşil perde)
- **DZB-P15 — Çok İz Üstüne Gelenler** · iş büyük · öncelik 2
  - Soru: Çok iz açıldıktan sonra üstüne gelen işler (sesi ayırma, otomatik müzik kısma, seslendirme kaydı, iz denetimleri, boşluklu çizelge) kapsama girsin mi?
  - Satırlar: DZB-10 (Sesi videodan ayırma), DZB-46 (Otomatik müzik kısma), DZB-47 (Seslendirme kaydı), DZB-06 (İz denetimleri), DZB-07 (Boşluklu çizelge)
- **DZB-P16 — Vekil, Hazır İçerik Ve Model** · iş büyük · öncelik 2
  - Soru: Uygulama kullanıcının diskine büyük ek dosya (vekil kopya, hazır içerik paketi, yapay zekâ modeli) üretsin ya da indirsin mi; hangisi istekle, hangisi hiç?
  - Satırlar: DZB-51 (Vekil dosyalarla kurgu), DZB-22 (Hazır başlık şablonları ve alt bant), DZB-52 (Proje şablonları), DZB-53 (Hazır varlık kitaplığı), DZB-44 (Modelli konuşma temizleme), DZB-69 (Otomatik yeniden kadraj), DZB-70 (Arka plan silme ve nesne maskesi), DZB-71 (Hareket izleme), DZB-72 (Metinden sese)
### Küçült — Küçültme, Önizleme Ve Kuyruk

- **SIK-P2 — Kodlama Sürerken Uykuyu Engelleme** · iş küçük · öncelik 1
  - Soru: Kodlama sürerken bilgisayarın uykuya geçmesi varsayılan olarak mı engellensin, yoksa kapalı gelen bir ayar mı olsun?
  - Satırlar: SIK-06 (Kodlama sürerken uykuyu engelleme)
- **SIK-P9 — Kuyruğun Saklanması Ve Geri Yüklenmesi** · iş orta · öncelik 2
  - Soru: Açılışta yarım kalan kuyruk kendiliğinden mi geri gelsin, yoksa kullanıcıya mı sorulsun?
  - Satırlar: SIK-05 (Kuyruğun saklanması)
- **SIK-P10 — Biten Çıktının Kalite Ölçümü** · iş orta · öncelik 2
  - Soru: Tam kalite ölçümü her işi uzatır; varsayılan kapalı bir seçenek olarak arayüze girsin mi?
  - Satırlar: SIK-14 (Çıktının gerçek kalite puanı)
- **SIK-P11 — Ölçümle Hedef Kalite Araması** · iş büyük · öncelik 2
  - Soru: Arama her turda ek kodlama ve ölçüm süresi getirir; hedef kalite kipine isteğe bağlı bir seçenek olarak eklensin mi?
  - Satırlar: SIK-15 (Ölçümle hedef kalite araması)
- **SIK-P12 — VVC Kodlama Desteği** · iş orta · öncelik 2
  - Soru: libvvenc içeren bir ffmpeg derlemesi dağıtılsın mı ve oynatma desteği bu kadar darken VVC kodek listesine girsin mi?
  - Satırlar: SIK-16 (VVC (H.266) kodlama)
- **SIK-P13 — Donanım Kodlayıcıda Dolby Vision** · iş büyük · öncelik 2
  - Soru: Dolby Vision kapsamı donanım kodlayıcılarına genişlesin mi, gerekirse ffmpeg dışında ayrı bir araçla?
  - Satırlar: SIK-19 (Donanım kodlayıcıda Dolby Vision)
### Dönüştür — Dönüştürme Ve Gelişmiş

- **DON-P4 — Dönüştür'de Ses İzi Seçimi** · iş orta · öncelik 1
  - Soru: Dönüştür varsayılan olarak tek ses mi çıkarsın, yoksa kabın taşıyabildiği bütün sesleri mi taşısın.
  - Satırlar: DON-01 (Ses izi seçimi)
- **DON-P15 — Yapay Zekâ İle Büyütme** · iş büyük · öncelik 2
  - Soru: Yüzlerce MB'lık model ve yeni bir ikili dosya kullanıcıya indirtilsin mi.
  - Satırlar: DON-32 (Yapay zekâ ile büyütme)
### Kaydedici — Yakalama

- **EKA-P2 — Uygulama Başına Ses** · iş orta · öncelik 1
  - Soru: Uygulama başına ses yeni bir Windows ses arabirimi ister ve eski Windows 10 yapılarında çalışmaz; orada ve Windows dışında seçenek gizlensin mi, kapalı mı görünsün.
  - Satırlar: EKA-01 (Uygulama başına ses)
- **EKA-P11 — Model Tabanlı Gürültü Bastırma** · iş orta · öncelik 2
  - Soru: Gürültü bastırma modeli (RNNoise dosyası) kurulum paketine girsin mi.
  - Satırlar: EKA-14 (Model tabanlı gürültü bastırma)
- **EKA-P12 — Mac Sistem Sesi Yardımcısı** · iş büyük · öncelik 1
  - Soru: macOS için ScreenCaptureKit kullanan yerel bir yardımcı ikili yazılıp pakete eklensin mi.
  - Satırlar: EKA-03 (macOS'ta sistem sesi)
- **EKA-P13 — Mac Pencere Yakalama** · iş büyük · öncelik 1
  - Soru: macOS'ta pencere yakalama EKA-P12'deki yardımcı ikiliye bağlansın mı; o karar hayır çıkarsa bu paket düşer.
  - Satırlar: EKA-04 (macOS'ta pencere yakalama)
- **EKA-P14 — Wayland Yakalama Yolu** · iş büyük · öncelik 1
  - Soru: Wayland için PipeWire portalını konuşan bir yardımcı ikili ya da yeni bağımlılık eklensin mi, yoksa yalnız açık bir uyarıyla yetinilsin mi.
  - Satırlar: EKA-02 (Wayland'da yakalama)
- **EKA-P15 — HDR Yakalama** · iş büyük · öncelik 2
  - Soru: HDR ekranda varsayılan HDR dosya mı olsun, yoksa SDR'ye çevrilmiş kayıt mı.
  - Satırlar: EKA-08 (HDR yakalama)
- **EKA-P16 — Oyun Yakalama** · iş büyük · öncelik 2
  - Soru: Oyun sürecine kanca atan ayrı bir yakalama yolu yapılsın mı, yoksa ekran kaydı yolu ölçülüp yeterli mi sayılsın.
  - Satırlar: EKA-05 (Oyun yakalama)
### Kaydedici — Sunum, Kayıt Sonrası Ve Paylaşım

- **EKB-P1 — Tuş Gösterimi Süzgeci** · iş küçük · öncelik 1
  - Soru: "Yalnız kısayolları göster" süzgeci varsayılan açık mı gelsin, yoksa bugünkü gibi her tuş mu gösterilsin.
  - Satırlar: EKB-11 (Tuş gösterimi süzgeci ve ayarı)
- **EKB-P8 — Kayıt Çerçevesi** · iş orta · öncelik 1
  - Soru: Hazır arka plan görselleri pakete girsin mi, yoksa düz renk ve renk geçişiyle mi yetinilsin.
  - Satırlar: EKB-02 (Kayıt çerçevesi)
- **EKB-P9 — Otomatik Ve Canlı Yakınlaştırma** · iş büyük · öncelik 1
  - Soru: Yakınlaştırma kayıttan sonra mı üretilsin (imleç ve tıklama verisi videodan ayrı kaydedilir), kayıt sırasında canlı mı; ikisinden biri seçilir.
  - Satırlar: EKB-01 (Otomatik yakınlaştırma), EKB-13 (Canlı yakınlaştırma)
- **EKB-P10 — İmleç Cilası Ve Hareket Bulanıklığı** · iş büyük · öncelik 1
  - Soru: İmleç kayda yakılmak yerine ayrı veri olarak tutulsun mu; EKB-P9'da kayıt sonrası yol seçilmeden bu paket başlamaz.
  - Satırlar: EKB-03 (İmleç cilası), EKB-31 (Hareket bulanıklığı)
- **EKB-P11 — Kamera Arka Planı** · iş büyük · öncelik 1
  - Soru: Kişiyi arka plandan ayıran model ve onu çalıştıran kitaplık uygulamayla dağıtılsın ya da ilk kullanımda indirilsin mi.
  - Satırlar: EKB-07 (Kamera arka planı)
- **EKB-P14 — Kayan Sonuç Kartı** · iş orta · öncelik 2
  - Soru: Kayıt bitince ekran köşesinde kendiliğinden yeni bir pencere açılsın mı ve bu varsayılan açık mı gelsin.
  - Satırlar: EKB-24 (Kayan sonuç kartı)
- **EKB-P15 — Kayıtta Bildirim Ve Simge Gizleme** · iş orta · öncelik 2
  - Soru: Uygulama kayıt sırasında sistemin bildirim ayarına ve masaüstü simgelerine dokunsun mu.
  - Satırlar: EKB-20 (Bildirim susturma), EKB-21 (Masaüstü simgelerini gizleme)
- **EKB-P16 — Özel Hedef Ve Kendi Bulutun** · iş büyük · öncelik 2
  - Soru: Kullanıcının girdiği sunucu adresi ve erişim anahtarı uygulamada saklansın mı, bulut hesabına tarayıcıdan giriş eklensin mi.
  - Satırlar: EKB-28 (Özel yükleme hedefi), EKB-29 (Kendi bulut depona yükleme)
- **EKB-P17 — Girdi Efektleri Mac Ve Linux** · iş büyük · öncelik 2
  - Soru: macOS'ta girdi izleme izni istensin mi, ve Wayland'da genel tuş dinleme yolu yoksa Linux desteği X11 ile mi sınırlansın.
  - Satırlar: EKB-14 (Girdi efektleri macOS ve Linux'ta)
### Ayarlar Ve Kabuk — Ayarlar, Hakkında Ve Pencere

- **AYR-P1 — Pencere Konumunu Hatırlama** · iş küçük · öncelik 1
  - Soru: Uygulama her açılışta ekranı kaplamış açılmak yerine son konumunda, boyutunda ve durumunda mı açılsın?
  - Satırlar: AYR-03 (Pencere Konumunu Ve Boyutunu Hatırlama)
- **AYR-P8 — Taşınabilir Kip Ve Güncelleme Kanalı** · iş büyük · öncelik 1
  - Soru: Yayına iki yeni biçim eklensin mi: ayarını yanında taşıyan kurulumsuz paket ve gönüllülere açık ön sürüm kanalı?
  - Satırlar: AYR-05 (Taşınabilir Kip), AYR-09 (Güncelleme Kanalı Seçimi)
- **AYR-P9 — Tepsiye Küçültme** · iş küçük · öncelik 2
  - Soru: Ana pencere için kapalı gelen bir "tepsiye küçült" seçeneği eklensin mi; eklenecekse kapatma düğmesi de tepsiye mi gitsin?
  - Satırlar: AYR-22 (Tepsiye Küçültme)
- **AYR-P10 — Çeviri Katkısı Ve Tamlık İşareti** · iş orta · öncelik 2
  - Soru: 42 dil sözü nasıl sunulsun: gözden geçmemiş diller listede işaretlensin mi ve bir çeviri sitesinde proje hesabı açılsın mı?
  - Satırlar: AYR-04 (Çeviri Katkı Platformu), AYR-25 (Bakımsız Çeviriyi İşaretleme)
- **AYR-P11 — Arayüz Ölçeği Ve Yüksek Karşıtlık** · iş büyük · öncelik 2
  - Soru: "Renk yalnız paletten, ölçü yalnız belirteçten" kuralına sistemin karşıtlık ve yazı boyutu ayarları için istisna açılsın mı?
  - Satırlar: AYR-12 (Arayüz Ölçeği Ya Da Yazı Boyutu), AYR-13 (Yüksek Karşıtlık Desteği)
- **AYR-P12 — İlk Açılış Karşılaması Ve Kılavuz** · iş büyük · öncelik 2
  - Soru: Yeni kullanıcıya yardım nasıl sunulsun: ilk açılışta bir karşılama ekranı ve dillerde sürdürülen çevrimiçi kılavuz sitesi açılsın mı?
  - Satırlar: AYR-23 (İlk Açılış Karşılaması), AYR-24 (Kullanım Kılavuzu)
### Dağıtım Ve Otomasyon — Kurulum, Güncelleme Ve Komut Satırı

- **DAG-P5 — Winget, Scoop Ve Chocolatey Paketleri** · iş orta · öncelik 1
  - Soru: Windows paket dizinlerinde yayın yapılsın mı; winget için PR sahibin hesabından açılır ve Microsoft katkı sözleşmesi onaylanır, Scoop için kova deposu açılır, Chocolatey için hesap gerekir.
  - Satırlar: DAG-01 (winget paketi), DAG-08 (Scoop paketi), DAG-12 (Chocolatey paketi)
- **DAG-P6 — Windows Kod İmzası** · iş orta · öncelik 1
  - Soru: SignPath Foundation'a başvurulsun mu (ücretsiz; başvuruyu ve her imza onayını sahip yapar), yoksa ücretli sertifika mı alınsın.
  - Satırlar: DAG-02 (Windows kod imzası)
- **DAG-P7 — Ön Sürüm Güncelleme Kanalı** · iş orta · öncelik 2
  - Soru: ön sürüm yayımlansın mı ve Ayarlar'a kanal seçimi eklensin mi; sürüm kesme düzeni değişir.
  - Satırlar: DAG-24 (Güncelleme kanalı)
- **DAG-P8 — Flathub Ve AppImage Paketleri** · iş büyük · öncelik 1
  - Soru: Flathub'a uygulama başvurusu yapılsın mı (sahibin hesabından; ffmpeg ve libmpv paketin içinde gelir) ve yayına AppImage eklensin mi.
  - Satırlar: DAG-04 (Flathub paketi), DAG-09 (AppImage)
- **DAG-P9 — Tam Paket Ve Çevrimdışı Kurucu** · iş büyük · öncelik 2
  - Soru: ffmpeg ve libmpv ikilileri kendi yayın dosyalarımızın içinde dağıtılsın mı (yüzlerce MB ek boyut ve lisans yükümlülüğü).
  - Satırlar: DAG-10 (Taşınabilir sürüm), DAG-11 (Çevrimdışı kurucu), DAG-20 (macOS ve Linux'ta ffmpeg kurulumu)
- **DAG-P10 — macOS İmza, Dmg Ve Homebrew** · iş büyük · öncelik 2
  - Soru: ücretli Apple geliştirici üyeliği alınsın mı; alınırsa dmg yayımlanır ve Homebrew için kendi tap depomuz açılır.
  - Satırlar: DAG-05 (macOS geliştirici imzası ve noterleme), DAG-06 (macOS disk görüntüsü (dmg)), DAG-07 (Homebrew cask)
- **DAG-P11 — Microsoft Store Paketi** · iş büyük · öncelik 2
  - Soru: Microsoft Store geliştirici hesabı açılsın mı ve uygulama MSIX olarak mağazaya girsin mi.
  - Satırlar: DAG-03 (Microsoft Store paketi)
- **DAG-P12 — Betik Ve Eklenti Arabirimi** · iş büyük · öncelik 2
  - Soru: uygulamaya betik motoru (yeni bağımlılık) eklensin ve sürümler arası korunacak bir eklenti arabirimi sözü verilsin mi.
  - Satırlar: DAG-36 (Betik ve eklenti arabirimi)
### Bizde Olmayan Araçlar — Yeni Araç Adayları

- **ARC-P6 — Dosya Dönüştürme Ve Ölçüm Araçları** · iş büyük · öncelik 1
  - Soru: Araçlar sekmesi açılsın mı, yoksa en yakın sekmeye mi konsun
  - Satırlar: ARC-21 (Altyazı dosyası dönüştürme ve kaydırma), ARC-11 (Görüntü dönüştürücü), ARC-02 (İki dosya kalite ölçer), ARC-06 (Kare sağlaması)
- **ARC-P8 — Altyazı Çevirisi** · iş orta · öncelik 2
  - Soru: altyazı metnini çevrimiçi çeviri hizmetine ya da yerel dil modeline veren araç ürüne girsin mi
  - Satırlar: ARC-23 (Altyazı çevirisi)
- **ARC-P9 — Logo Ve Damga Silme** · iş orta · öncelik 2
  - Soru: başkasının logosunu ya da filigranını silmeye yarayan araç ürüne girsin mi
  - Satırlar: ARC-33 (Logo ve filigran silme)
- **ARC-P11 — Yapay Zekâ Araçları** · iş büyük · öncelik 2
  - Soru: yapay zekâ araçları için yeni çalışma zamanı ve yüzlerce MB model indirmesi kabul edilsin mi
  - Satırlar: ARC-25 (Vokal ve enstrüman ayırma), ARC-27 (Yapay zekâ ile çözünürlük büyütme), ARC-29 (Arka plan silme), ARC-30 (Kendiliğinden yeniden kadraj), ARC-31 (Yüz bulanıklaştırma)
- **ARC-P12 — Kendi Sekmesini İsteyen Araçlar** · iş büyük · öncelik 2
  - Soru: kendi sekmesini isteyen araç ürüne girsin mi
  - Satırlar: ARC-18 (Yinelenen video bulucu), ARC-22 (Altyazı düzenleyici)
### Platform Eşitliği — Windows'ta Olup macOS Ya Da Linux'ta Eksik Kalanlar

- **PLT-P4 — macOS İzin Akışı** · iş orta · öncelik 2
  - Soru: pakete izin açıklamaları eklenip kayıttan önce izin yoklaması gösterilsin mi
  - Satırlar: PLT-04 (macOS izin akışı)
- **PLT-P5 — macOS Kapanışta Sessiz Güncelleme** · iş orta · öncelik 2
  - Soru: macOS'ta otomatik güncelleme varsayılan olarak açılsın mı
  - Satırlar: PLT-21 (macOS'ta otomatik sessiz güncelleme)
- **PLT-P6 — Linux'ta Kendini Güncelleme** · iş orta · öncelik 2
  - Soru: Linux'ta bilerek kapalı tutulan uygulama içi güncelleme açılsın mı
  - Satırlar: PLT-20 (Linux'ta kendini güncelleme)
- **PLT-P7 — VideoToolbox Hızlı Kip Kapısı** · iş orta · öncelik 2
  - Soru: ölçüm için Mac ayrılsın mı ve kapıdan geçerse Hızlı kipin Mac'teki varsayılan kodlayıcısı değişsin mi
  - Satırlar: PLT-37 (macOS'ta VideoToolbox'ın otomatik sıraya girmesi)
- **PLT-P8 — Linux Ölçüm Turu: VAAPI Ve Sistem Sesi** · iş orta · öncelik 2
  - Soru: ölçüm için Intel ya da AMD ekran kartlı gerçek bir Linux makinesi ayrılsın mı
  - Satırlar: PLT-38 (Linux'ta VAAPI ölçümü), PLT-43 (Linux'ta sistem sesi ölçümü)
- **PLT-P9 — Wayland'da Genel Kısayol** · iş orta · öncelik 2
  - Soru: GlobalShortcuts portalı için D-Bus istemcisi bağımlılığı eklensin mi
  - Satırlar: PLT-08 (Linux'ta genel kısayol)

## Soru

Sahibe 65 soru sorulamaz. Her paketi şu üç kümeden birine koy ve kısa gerekçe yaz:

- **A — Zaten Cevaplı:** sahibin önceki kararları ya da yukarıdaki olgular soruyu cevaplıyor. Cevabı ve dayandığı kararı yaz.
- **B — Yalnız Sahip:** para, hesap, imza, hukuk, zevk ya da ürünün yönü; başkası karar veremez. Soruyu tek cümleye indir, önerdiğin cevabı ve gerekçesini yaz.
- **C — Şimdi Sorulmaz:** paket ikinci aşamada sırası gelince, ölçüm yapıldıktan sonra sorulur. Neyin ölçülmesi gerektiğini yaz.

Sonra B kümesini, aynı cevaba bağlı soruları birleştirerek **en çok on soruya** indir ve cevabı en çok işin önünü açandan en aza sırala. Her sorunun yanına hangi paketleri açtığını yaz.

Emin olmadığın yeri açıkça söyle; uydurma. Paket numaralarını yukarıda yazdığı gibi kullan.

## Yanıt Biçimi

Türkçe, sade. Üç başlık: `## A — Zaten Cevaplı`, `## B — Sahibe Sorulacaklar` (numaralı, en çok on), `## C — Şimdi Sorulmaz`. Her paket tam bir kez geçsin. Sonda tek satır: A, B, C sayıları.
