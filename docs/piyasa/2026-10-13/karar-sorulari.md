# Karar Soruları

### Soru Sırası

Sahibe bağlı 65 paket ayrı bir danışmada ayıklandı: 8'i önceki kararlarla zaten cevaplı, 41'i dokuz soruya iniyor, 16'sı bir ölçüm yapılmadan sorulamaz. Sıra, cevabın açtığı paket sayısına göre. Öneriler danışmanın görüşüdür; karar sahibin. Danışma eş paketler birleştirilmeden önce yapıldı: DON-P15 sonradan ARC-P11'in eşi sayıldı, ikisi de üçüncü sorunun içinde; dalga tablosu bu yüzden bir eksik gösterir.

1. **Varsayılanlar paketi: on varsayılan önerildiği gibi alınsın mı?** Açar: 10 paket. Tek "tamam" ya da satır satır veto.

   | Paket | Önerilen Varsayılan |
   |---|---|
   | OYA-P5 | Ses ve altyazı izi dil tercihine göre seçilir, zorunlu altyazı kendiliğinden açılır, devam sınırı kalkar, taramalı görüntü kendiliğinden düzeltilir |
   | OYB-P5 | Kaldığı yerden devam sayı sınırsız, eskiler yaşla silinir; kuyruk açılışta geri gelmez |
   | DZA-P7 | Kayıpsız kesimde bütün izler taşınır |
   | DON-P4 | Kabın taşıyabildiği bütün ses izleri taşınır, iz başına kutu olur |
   | SIK-P2 | Kodlama sürerken bilgisayarın uyuması engellenir, ekran kapanabilir |
   | SIK-P9 | Yarım kalan kuyruk kendiliğinden, duraklatılmış gelir; açılışta soru sorulmaz |
   | EKA-P15 | HDR ekranda kayıt varsayılan olarak SDR'ye çevrilir, HDR dosya seçenek olur |
   | EKB-P1 | Tuş gösteriminde "yalnız kısayollar" açık gelir; yazılan parola kayda girmez |
   | EKB-P14 | Kayıt bitince kart açık gelir, üstünde "bir daha gösterme" durur |
   | AYR-P1 | Pencerenin son konumu, boyutu ve durumu hatırlanır |

2. **Ölçüm için gerçek bir Mac ve Intel ya da AMD kartlı bir Linux makinesi ayrılacak mı?** Açar: EKA-P12, EKA-P13, PLT-P7, PLT-P8; sonra ölçüm bekleyenlerden PLT-P4, EKA-P14, PLT-P9, EKB-P17 (8 paket). Öneri: ikisine de evet; makine yoksa bu sekiz paket açılmaz.
3. **Uygulama yapay zekâ modeli ya da hazır içerik paketi indirsin mi?** Açar: DZB-P16, DON-P15, EKB-P11, EKA-P11, ARC-P11, ARC-P8, EKB-P8 (7 paket). Öneri: model indiren özellik yok (K5 ile aynı çizgi), hazır içerik paketi yok. Evet olanlar: istekle üretilen ve silinebilen vekil dosya, pakette gelen küçük proje şablonları, küçükse pakette gelen gürültü bastırma modeli.
4. **Hangi hesapları ve imzayı açacaksın?** Açar: DAG-P6, DAG-P5, DAG-P8, AYR-P10, DAG-P10, DAG-P11 (6 paket). Öneri: ücretsizlerin hepsi evet (SignPath ilk sırada, winget, Scoop, Weblate; Flathub Linux makinesi gelince), Chocolatey sonra, AppImage hayır; ücretli ikisi şimdi değil (Apple geliştirici üyeliği macOS indirme payı ölçülünce, Microsoft Store hayır).
5. **Düzenleyici tek şeritli kesiciden çok izli kurguya büyüsün mü?** Açar: DZB-P6, DZB-P7, DZB-P12; sonra DZB-P8, DZB-P15 (5 paket). Öneri: evet, kademeli: önce geçiş ve müzik izi, onlar teslim edilince ikinci video izi. Ayrı bir bileşim motoru yok. Listedeki en uzun taahhüt bu.
6. **Uygulama medya merkezi yönüne büyüsün mü: kitaplık, ağ kaynakları, uzaktan denetim, eklenti arabirimi, sessizliği atlayan çalma?** Açar: OYB-P11, OYB-P12, DAG-P12, OYA-P8 (4 paket). Öneri: hayır (K1, K10, K11 ile aynı çizgi). Paketlerin içindeki üç küçük satır yön değiştirmez, ölçümle sonra açılır: OYB-09, OYB-31, OYB-40.
7. **Yeni yüzey açılsın mı: Araçlar sekmesi, karşılama ekranı, kılavuz sitesi?** Açar: ARC-P6, ARC-P12, AYR-P12 (3 paket). Öneri: hayır; dört küçük araç en yakın sekmeye girer.
8. **Yayına taşınabilir zip ve ön sürüm kanalı eklensin mi?** Açar: AYR-P8, DAG-P7 (2 paket). Öneri: taşınabilir evet, ön sürüm kanalı hayır.
9. **Logo silme aracı ve bulut hesabına giriş yapılsın mı?** Açar: ARC-P9, EKB-P16 (2 paket). Öneri: ikisi de hayır; kullanıcının kendi sunucu adresi ve anahtarı evet, işletim sisteminin anahtarlığında saklanır.

### Önceki Kararlarla Cevaplı Sekiz Paket

Bunlar için soru sorulmuyor; danışma cevabı sahibin yazılı kararlarından çıkardı.

- OYA-P7 HDR ton eşleme: evet, işlemcide (K4 başka yol bırakmıyor).
- SIK-P10 biten çıktının kalite ölçümü: evet, kapalı gelen seçenek olarak.
- EKA-P2 uygulama başına ses: desteklenmeyen sistemde, kullanılamayan kodlayıcı nasıl gösteriliyorsa öyle gösterilir.
- AYR-P9 tepsiye küçültme: kapatma düğmesi tepsiye gitmez, çünkü güncelleme çıkışta kuruluyor; küçültme kapalı gelen seçenek olur.
- AYR-P11 arayüz ölçeği ve yüksek karşıtlık: istisna gerekmez; ölçek belirteçlerle, yüksek karşıtlık yeni bir paletle gelir.
- DAG-P9 tam paket ve çevrimdışı kurucu: hayır; ffmpeg ve libmpv sürümle birlikte gelmez.
- PLT-P5 macOS'ta kapanışta sessiz güncelleme: evet, Windows'taki gibi açık.
- PLT-P6 Linux'ta kendini güncelleme: hayır, kapalı kalır.

SIK-P10 ve AYR-P9'da danışma, sahibin maliyet kuralını benzetmeyle uyguladığını kendisi yazdı; tek satırlık itiraz ikisini soru listesine geri alır.

### Ölçüm Bekleyen On Altı Paket

Bunlar bugün sorulmaz; her biri önce bir ölçüm ister.

OYA-P6, OYB-P6, DZA-P6, SIK-P11, SIK-P12, SIK-P13, EKA-P16, EKB-P9, EKB-P10, EKB-P15 bir karar ya da yeni makine beklemiyor. PLT-P4, PLT-P9, EKA-P14, EKB-P17 ikinci sorudaki makineleri, DZB-P8 ve DZB-P15 beşinci sorunun cevabını bekler. Her paketin hangi ölçümü istediği danışma cevabında tek tek yazılı.

### Bu Ayıklamanın Sınırı

Danışma da bir iddiadır. Cevabında satır numarasıyla gösterdiği dört yer açıldı: üçü tuttu, biri tutmadı (Wayland'da pencere listesi X11'e düşmüyor, boş dönüyor); ayrıntı ana oturumun kod okumasında. Danışmanın kendi "emin değilim" dedikleri: LosslessCut ve KeyCastr varsayılanları, gürültü bastırma modelinin boyutu ve lisansı, Linux'ta kendini güncellemenin neden kapalı tutulduğu, EKA-P2'de hangi gösterim kuralının yürürlükte olduğu.
