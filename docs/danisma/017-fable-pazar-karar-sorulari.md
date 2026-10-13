# Danışma 017: Pazar Planı: Sahibin Kararını Bekleyen Paketler

claude-fable-5-1, 62.093 çıktı token, 882 sn. Girdi: [017-fable-pazar-karar-sorulari-girdi.md](017-fable-pazar-karar-sorulari-girdi.md). Cevap olduğu gibi:

---

## A — Zaten Cevaplı

- **OYA-P7 HDR Ton Eşleme** — Evet, işlemcide. K4 (oynatıcıda GPU çizim yolu yapılmayacak) tek yolu bırakıyor; HDR'yi soluk göstermek seçenek değil, kusur. Varsayılan açık mı elle mi: 4K HDR10 örnekte kare düşümü ölçülür, ölçüm karar verir; sahibe soru düşmez.
- **SIK-P10 Biten Çıktının Kalite Ölçümü** — Evet, kapalı gelen seçenek. Dayanak: sahibin maliyet kuralı (çağrıldığında maliyeti olan doğrudan yapılır; kapalı gelen seçenek yalnız açana maliyet getirir). libvmaf pinli GyanD derlemesinde var, CI yokluyor (`.github/workflows/ci.yml:198`), Bench zaten ölçüyor. macOS/Linux ffmpeg'inde libvmaf yoksa seçenek orada EKA-P2'deki kuralla gösterilir.
- **EKA-P2 Uygulama Başına Ses** — Desteklenmeyen sistemde seçenek, kullanılamayan kodlayıcı (NVENC'siz makine) nasıl gösteriliyorsa öyle gösterilir; uygulamada iki kural yan yana olmaz. Dayanak: teknesyum-ui standardı ve mevcut yoklama düzeni. Hangi kural olduğunu bu turda okumadım; yapıcı okur, soru düşmez.
- **AYR-P9 Tepsiye Küçültme** — Kapatma düğmesi tepsiye gitmez: güncelleme kurulumu uygulamanın çıkışında yapılıyor (AGENTS.md, `MainWindow.KapanistaGuncelleme.cs`); kapanmayan uygulama güncellenmez. Küçültme için kapalı gelen seçenek: maliyet kuralı, yapıcı ekler.
- **AYR-P11 Arayüz Ölçeği Ve Yüksek Karşıtlık** — İstisna gerekmez. Ölçek, belirteçlerin kökte tek çarpanla ölçeklenmesidir (belirteç dışı ölçü doğmaz); yüksek karşıtlık `Themes/Palette/<Ad>/` altında yeni bir palettir (renk yine paletten). Dayanak: kuralın kendisi ve `docs/netlestirme/018` (uyarı hue'su yok) yeni palete de uygulanır.
- **DAG-P9 Tam Paket Ve Çevrimdışı Kurucu** — Hayır. Dayanak: "libmpv pakete girmez" (AGENTS.md) ve "FFmpeg never travels with a release" (`docs/kurulum.md:145`). DAG-11 çevrimdışı kurucu ve DAG-20 bu kuralla kapanır; DAG-10 taşınabilir sürüm B-8'de (AYR-P8 ile aynı iş).
- **PLT-P5 macOS Kapanışta Sessiz Güncelleme** — Evet, açık. Dayanak: Windows'ta varsayılan açık kararı. Teknik eşik (imzasız paketin yerinde takası, karantina) ölçümle çözülür, soru değil.
- **PLT-P6 Linux'ta Kendini Güncelleme** — Hayır, kapalı kalır. Dayanak: girdide yazılı "bilerek kapalı" kararı; Linux'ta güncelleme dağıtım kanalının işi (apt/dnf, Flathub gelirse Flatpak). Gerekçeyi kodda bulamadım, kararın kendisi yazılı.

## B — Sahibe Sorulacaklar

Sıra: cevabın açtığı paket sayısı, eşitlikte iş büyüklüğü. Dokuz soru, 41 paket.

**1. Varsayılanlar paketi: aşağıdaki on varsayılanı önerildiği gibi alalım mı?** (tek "tamam" ya da satır satır veto)

| Paket | Öneri | Gerekçe |
|---|---|---|
| OYA-P5 | Dört evet: dil tercihine göre ses/altyazı izi, zorunlu altyazı kendiliğinden, devam sınırı kalkar, deinterlace auto | mpv'nin kendi varsayılanları; auto yalnız interlace bayrağı varken çalışır, maliyeti sıfır |
| OYB-P5 | Devam: sayı sınırı yok, yaşla budanır. Kuyruk: açılışta geri gelmez | Oynatıcıların geneli (VLC, mpv) kuyruğu geri getirmez; son dosya zaten devamda |
| DZA-P7 | Bütün izler | Sessizce iz kaybetmek veri kaybıdır; LosslessCut'ın varsayılanı da bu (emin değilim) |
| DON-P4 | Kabın taşıyabildiği bütün sesler, iz başına kutu | Dönüştürme "aynı içerik, başka kap" vaadidir; HandBrake'in "ilk iz" varsayılanı orada şikâyet kaynağı |
| SIK-P2 | Kodlarken uyku engeli varsayılan açık, ekran kapanabilir | HandBrake varsayılanı; uyuyan makine saatlerce işi yarım bırakır |
| SIK-P9 | Kuyruk kendiliğinden, duraklatılmış gelir; açılış diyaloğu yok | Sahibin diyalog sevmezliği; HandBrake sorar, biz sormayız |
| EKA-P15 | Varsayılan SDR'ye çevrilmiş kayıt, HDR dosya seçenek | OBS varsayılanı; paylaşım hedefleri SDR |
| EKB-P1 | "Yalnız kısayollar" varsayılan açık | Her tuşu göstermek kayıtta yazılan parolayı sızdırır; KeyCastr her tuşu gösterir ama o güvenli taraf değil |
| EKB-P14 | Kart varsayılan açık, üstünde "bir daha gösterme" | Screen Studio, Loom, Windows Alma Aracı aynı deseni kullanıyor; kaydın sonraki adımı (Küçült/Kes) oradan açılır |
| AYR-P1 | Son konum, boyut ve durum hatırlanır; ekran yoksa görünür ekrana kırpılır | Masaüstü uygulamalarının tamamı; bugünkü `WindowState="Maximized"` (`MainWindow.axaml:13`) sabit |

**2. Ölçüm için gerçek bir Mac ve Intel/AMD kartlı bir Linux makinesi ayrılacak mı?** — Açar: EKA-P12, EKA-P13, PLT-P7, PLT-P8; sonra C'deki PLT-P4, EKA-P14, PLT-P9, EKB-P17 (toplam 8).
Öneri: evet ikisine de. Mac için ScreenCaptureKit yardımcısı CI'da derlenir ama ses ve pencere yakalama CI koşucusunda ölçülemez (ekran kaydı izni yok); ödünç Mac ya da aylık ücretli bulut Mac gerekir. Linux ucuz: iGPU'lu eski bir PC'ye Fedora (Wayland ve PipeWire varsayılan). Makine yoksa bu sekiz paket açılmaz; kör yazılan platform kodu AGENTS.md ölçüsüne (dokunulan alan yeşil olmadan teslim yok) takılır. Yardımcı ikili kararı makineyle birlikte evet: EKA-P12 evet ise EKA-P13 ona bağlanır.

**3. Uygulama yapay zekâ modeli ya da hazır içerik paketi indirsin mi; disk politikası ne?** — Açar: DZB-P16, DON-P15, EKB-P11, EKA-P11, ARC-P11, ARC-P8, EKB-P8 (7).
Öneri: model indiren yapay zekâ özelliği yok (DON-32, DZB-44/69/70/71/72, ARC-25/27/29/30/31, EKB-07, ARC-23 düşer). Dayanak K5: konuşmadan metne modeli reddedildi, aynı çizgi. Hazır içerik paketi ve varlık kitaplığı yok; kayıt çerçevesi düz renk, renk geçişi ve kullanıcının kendi görseli (EKB-P8). Evet olanlar: vekil dosya istekle üretilir ve silinebilir (DZB-51), proje şablonları küçük JSON olarak pakette (DZB-52), RNNoise modeli küçükse pakette (EKA-P11; lisansı ve boyutu doğrulanır, emin değilim). Kapı açılacaksa düzenek hazır: libmpv deseni, ilk kullanımda sha256'lı indirme, Ayarlar'dan silme; o zaman beş yapay zekâ paketi C'ye döner.

**4. Hangi hesapları ve imzayı sen açacaksın: ücretsizlerin hepsi evet, ücretli ikisi şimdi değil?** — Açar: DAG-P6, DAG-P5, DAG-P8, AYR-P10, DAG-P10, DAG-P11 (6).
Öneri: SignPath evet ve ilk sırada (`docs/imzalama.md` yolu adım adım hazır; SmartScreen'i kalıcı silen tek şey; dikkat, her sürümde imza onayı sana düşer). winget evet (tek seferlik Microsoft katkı sözleşmesi, sonrası CI'dan jetonla), Scoop evet (GitHub'da kova deposu), Chocolatey sonra (hesap ve moderasyon kuyruğu, en düşük getiri). Flathub evet, Linux makinesi (B-2) gelince; AppImage hayır, DAG-P9 kuralıyla ffmpeg'siz AppImage anlamsız. Weblate (ücretsiz libre planı) evet, gözden geçmemiş diller listede işaretlenir. Apple geliştirici üyeliği (yıllık ücret) şimdi değil: `gh api` ile sürüm varlıklarının indirme sayısından macOS payı ölçülür, eşiği sen koyarsın. Microsoft Store hayır: MSIX, Store'un kendi güncellemesini ister, kapanışta yerinde takas tasarımıyla iki güncelleme yolu doğar.

**5. Düzenleyici tek şeritli kesiciden çok izli kurguya büyüsün mü: çapraz geçiş, müzik izi, ikinci video izi?** — Açar: DZB-P6, DZB-P7, DZB-P12; sonra C'deki DZB-P8, DZB-P15 (5).
Öneri: evet, kademeli. Önce geçiş (DZB-P6, ffmpeg xfade/acrossfade) ve müzik izi (DZB-P7, amix), ikisi orta iş; ikinci video izi ve yeşil perde (DZB-P12) bu ikisi teslim edilince. Ayrı bileşim motoru (MLT) yok. Gerekçe: K7 ile yön zaten çok kaynağa döndü; VidShrink'in vaadi hedef boyut olduğundan yeniden kodlama zaten var, geçiş ve müzik üstüne ek kodlama getirmez. Karşı sav: listedeki en uzun taahhüt bu; hayır dersen düzenleyici "kesici" kalır ve README öyle der.

**6. Uygulama medya merkezi ve platform yönüne büyüsün mü: kitaplık, ağ kaynakları, uzaktan denetim, eklenti arabirimi, sessiz atlama kipi?** — Açar: OYB-P11, OYB-P12, DAG-P12, OYA-P8 (4).
Öneri: hayır. K1 (OYB-22 site bağlantısı çözme zaten kapalı), K10, K11 aynı çizgide; eklenti arabirimi (OYB-41, DAG-36) tek kişilik projede sürümler arası koruma sözüdür, AGPL ve çatallama o işi görür; ağdan uzaktan denetim (OYB-39) ve yeni pencere (OYB-35) düşer. Sessiz atlama (OYA-P8) rakipsiz yenilik, oy kanıtı büyürse diye rafa. Paketlerin içindeki üç küçük satır yön değiştirmez, ikinci aşamada ölçümle açılır: OYB-09 adlı listeler (m3u kaydet/aç), OYB-31 MPRIS (OYB-P6 ölçümünün Linux eşi), OYB-40 çalışan örneğe dosya gönderme (tek örnek kuralının borusu).

**7. Yeni yüzey açılsın mı: Araçlar sekmesi, kendi sekmesini isteyen araçlar, karşılama ekranı, kılavuz sitesi?** — Açar: ARC-P6, ARC-P12, AYR-P12 (3).
Öneri: hayır. Dört küçük araç en yakın sekmeye: altyazı dönüştürme ve kaydırma Düzenleyici'ye, görüntü dönüştürücü Dönüştür'e, iki dosya kalite ölçer Küçült'e (SIK-P10'un üstüne), kare sağlaması Gelişmiş'e. Yinelenen video bulucu başka ürün, altyazı düzenleyici K5 çizgisi: ikisi de hayır. Karşılama ekranı ilk kapatılan şeydir; 42 dilde kılavuz sitesi tek kişiye bakım borcu; README görselleri ve ipuçları kalır.

**8. Yayına taşınabilir zip ve ön sürüm kanalı eklensin mi?** — Açar: AYR-P8, DAG-P7 (2).
Öneri: taşınabilir evet (güncelleme zip'i `app/ + launcher/` zaten var; yanına ayarı exe yanında tutan işaret dosyası; ffmpeg ve libmpv ilk açılışta yine indirilir, DAG-P9 kuralı bozulmaz). Ön sürüm kanalı hayır: sürüm kesme düzeni ikiye bölünür, güncelleme kodunda ikinci yol açılır; gönüllüye GitHub'ın pre-release işareti ve elle kurulum yeter.

**9. Hukuki ve hesap riski taşıyan iki iş: logo silme aracı ve bulut hesabına giriş?** — Açar: ARC-P9, EKB-P16 (2).
Öneri: logo ve filigran silme hayır (başlıca kullanımı başkasının damgasını silmek; K8 filigran eklemeyi bile rafa koydu). Bulut hesabına tarayıcıdan giriş hayır (her sağlayıcı senin hesabında uygulama kaydı ve inceleme ister, bakımı sürer). Kullanıcının kendi sunucu adresi ve anahtarı evet, işletim sisteminin anahtarlığında saklanır (Windows Credential Manager, macOS Keychain; Linux'ta libsecret bağımlılığı ölçülür).

## C — Şimdi Sorulmaz

- **OYA-P6 Çift Tık Ve Basılı Tutma** — Ölçüm: tek tık duraklatmanın çift tık beklemesiyle geciktiği ms; YouTube deseni (ilk tık hemen duraklatır, çift tık geri alıp tam ekran) libmpv'de takılmasız mı; basılı tutma eşiği ve hızlıda ses (scaletempo2).
- **OYB-P6 Windows Ortam Denetimi** — Ölçüm: libmpv'nin `media-controls` seçeneği render API'de, pencere tutamacı olmadan SMTC'ye kaydoluyor mu; odak dışı tuşlar geliyor mu. Evetse paket kapanır, hayırsa SDK bağının boyutu ölçülüp sorulur.
- **DZA-P6 Kayıpsız Birleştirme** — Ölçüm: uyumlu kaynaklarda (aynı kodek, profil, çözünürlük, fps, zaman tabanı) concat kopyası; birleşim noktasında zaman damgası sıçraması, ses-görüntü kayması, libmpv/VLC/Chrome'da oynatma. Örnek kümede sıfır hata ise Hızlı kipte kopya, değilse K7 kalır.
- **DZB-P8 Geçiş Önizleme Yolu** — B-5 evet derse: libmpv lavfi-complex ile iki kaynaklı canlı geçişte sarma gecikmesi ve CPU; geçiş bölgesini arka planda kısa ön-işlemenin süresi ve önbellek boyutu. MLT B-5'te kapandı.
- **DZB-P15 Çok İz Üstüne Gelenler** — DZB-P7 ve P12 teslimden sonra: otomatik kısmanın (sidechaincompress) örnek kümede kalitesi; seslendirme kaydı için kaydedicinin mikrofon yolunun düzenleyiciye bağlanması.
- **SIK-P11 Ölçümle Hedef Kalite Araması** — Ölçüm: bir arama turunun (örnek pencere kodlama + VMAF) referans makinede 1080p dakika başına süresi, kaç turda oturduğu. Seçenek arkasına gizlenebilecek maliyetse soru düşer.
- **SIK-P12 VVC** — Ölçüm: pinli GyanD derlemesinde libvvenc var mı, libmpv VVC çözüyor mu, brew/apt derlemelerinde durum. Kodlayıp oynatamayan kodek listeye girmez.
- **SIK-P13 Donanım Kodlayıcıda Dolby Vision** — Ölçüm: NVENC/QSV/AMF HEVC çıktısına dovi_tool ile RPU enjeksiyonu doğrulanıyor mu, ek ikilinin boyutu. Talep ölçülemez; teknik sonuç yetmezse paket düşer.
- **EKA-P14 Wayland Yakalama** — Linux makinesi (B-2) gelince: dağıtım ffmpeg'inde PipeWire girişi var mı, portal ScreenCast akışı yardımcı ikilisiz alınıyor mu. Bugün uygulama Wayland'ı algılayıp X11 listesine düşüyor (`RecorderWindows.cs:83`).
- **EKA-P16 Oyun Yakalama** — Ölçüm: mevcut yol tam ekran oyunda 60 ve 120 fps'de kare tutuyor mu, CPU payı. Tutuyorsa paket kapanır; kanca yolu K11 çizgisine yakın, ölçüm düşmeden açılmaz.
- **EKB-P9 Otomatik Ve Canlı Yakınlaştırma** — Ölçüm: imleç ve tıklama olaylarını ayrı kaydetmenin maliyeti (dakika başına KB, CPU), kayıt sonrası işlemenin dakika başına süresi; canlı yolun

[1187 karakter kesildi]
