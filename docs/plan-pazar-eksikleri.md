# Pazar Eksikleri Planı

13 Ekim 2026 · kod okuması `eb0a5db0 (v1.2.16)` · birinci aşama: yalnız araştırma ve plan

Sahibin isteği, birebir:

> şimdi tüm piyasayı araştır her bir sekmedeki eksiklerimiz hakkında kapsamlı plan yap saat 8 e kadar durma şu aşamada plan yap sadece işlem 2. aşama olacak bulduklarını later a eklersin

Bu belge o isteğin birinci aşamasıdır. Kod yazılmadı, hiçbir şey derlenmedi, uygulama çalıştırılmadı.
İkinci aşamada yapılacak işler paket paket [rafta](later.md) duruyor; sıra oradaki sıradır.

## Kısa Cevap

12 kol 567 eksik yazdı; doğrulama turunda bizde tam olarak var çıkan satır olmadı, 567 satırın hepsi kaldı. Kalanların 78 tanesi öncelik 1, 246 tanesi öncelik 2; 29 satırın önceliği doğrulamada değişti.
Satırlar 154 iş paketine toplandı. 90 paket bir karar beklemeden başlayabilir (dalga sırasıyla 9, 30, 43, 8), 64 paket senin kararını bekliyor.
37 satır daha önce kapattığın bir karara denk geliyor; onlar hiçbir pakete girmedi.
18 satır kendi kolunun paketine girmedi ama başka bir kolun paketi aynı işi kapsıyor; hangisi olduğu o kolun "Pakete girmeyenler" listesinde.

En önemli üç şey:

**Çekirdek sağlam, eksik rahatlıkta.** Eksiklerin çoğu yeni bir motor değil; var olan işi kolaylaştıran
parçalar: bildirim, hatırlama, arama, tek tek seçme. Bir kısmı da bir sekmede yazılmış kodun öbür
sekmeye bağlanmasıyla kapanıyor. Örnek: Küçült'ün altyazı ve süzgeç zinciri hazır, Dönüştür onu kullanmıyor.

**Yol üstünde iki kusur, bir olası kusur ve bayat belge satırları çıktı.** Liste uzantısıyla biten yayın
adresi oynatıcıda motora gitmeden yerel liste okuma yoluna giriyor (OYB-P1). Kaydedicinin tuş gösterimi,
parola yazılırken de, her tuşu kayda yazıyor (EKB-P1). Kameralı bir Mac'te ekran yerine kamera kaydediliyor
olabilir; koddan okundu, gerçek bir Mac'te ölçülmedi (PLT-P1). README dört hedef diyor, yayın altı hedef
üretiyor; kurulum belgesi tek hedefin yayımlandığını söylüyor; README imza başvurusunu "sürüyor" diye yazıyor,
imza belgesi "hiçbiri gönderilmedi" diyor. Bunlar pazar eksiği değil; ilk düzeltilecekler.

**En büyük işler senin kararına bağlı.** Kaydedicide sunum cilası (yakınlaştırma, imleç), macOS ve Linux'ta
yakalama, dağıtımda imza ve paket dizinleri: hepsi yeni bir bağımlılık, bir hesap ya da para istiyor.
Karar gerektirmeyen paketler rafta önde duruyor; ikinci aşama onlarla başlayabilir.

## Sayılar

Sayıların hepsi betikle tablolardan sayıldı; elle sayı yazılmadı.

| Kol | Önek | Ham | Çıkarılan | Kalan | Ö1 | Ö2 | Ö3 | Küçük | Orta | Büyük | Gerekmez | Sahip | Kapalı | Öncelik Değişen | Paket | Pakette Satır |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Oynatıcı — Oynatma Çekirdeği | OYA | 55 | 0 | 55 | 12 | 17 | 26 | 33 | 12 | 10 | 36 | 10 | 9 | 1 | 12 | 34 |
| Oynatıcı — Liste, Kitaplık Ve Ağ | OYB | 47 | 0 | 47 | 6 | 23 | 18 | 12 | 22 | 13 | 25 | 20 | 2 | 0 | 12 | 30 |
| Düzenleyici — Kayıpsız Kesim Ve Birleştirme | DZA | 44 | 0 | 44 | 8 | 19 | 17 | 22 | 18 | 4 | 38 | 4 | 2 | 3 | 12 | 32 |
| Düzenleyici — Kurgu Yönü | DZB | 77 | 0 | 77 | 7 | 40 | 30 | 19 | 38 | 20 | 43 | 29 | 5 | 6 | 16 | 51 |
| Küçült — Küçültme, Önizleme Ve Kuyruk | SIK | 46 | 0 | 46 | 5 | 13 | 28 | 19 | 17 | 10 | 25 | 18 | 3 | 3 | 13 | 21 |
| Dönüştür — Dönüştürme Ve Gelişmiş | DON | 52 | 0 | 52 | 9 | 23 | 20 | 27 | 21 | 4 | 41 | 5 | 6 | 1 | 15 | 38 |
| Kaydedici — Yakalama | EKA | 29 | 0 | 29 | 4 | 12 | 13 | 10 | 14 | 5 | 19 | 9 | 1 | 1 | 16 | 21 |
| Kaydedici — Sunum, Kayıt Sonrası Ve Paylaşım | EKB | 39 | 0 | 39 | 10 | 20 | 9 | 10 | 17 | 12 | 19 | 17 | 3 | 2 | 17 | 31 |
| Ayarlar Ve Kabuk — Ayarlar, Hakkında Ve Pencere | AYR | 48 | 0 | 48 | 3 | 22 | 23 | 28 | 16 | 4 | 30 | 18 | 0 | 7 | 12 | 32 |
| Dağıtım Ve Otomasyon — Kurulum, Güncelleme Ve Komut Satırı | DAG | 42 | 0 | 42 | 4 | 19 | 19 | 12 | 24 | 6 | 17 | 24 | 1 | 4 | 12 | 26 |
| Bizde Olmayan Araçlar — Yeni Araç Adayları | ARC | 44 | 0 | 44 | 7 | 19 | 18 | 7 | 20 | 17 | 16 | 23 | 5 | 1 | 12 | 29 |
| Platform Eşitliği — Windows'ta Olup macOS Ya Da Linux'ta Eksik Kalanlar | PLT | 44 | 0 | 44 | 3 | 19 | 22 | 14 | 23 | 7 | 24 | 20 | 0 | 0 | 9 | 10 |
| **Toplam** | | **567** | **0** | **567** | **78** | **246** | **243** | **213** | **242** | **112** | **333** | **197** | **37** | **29** | **158** | **355** |

**Ham** araştırma kolunun yazdığı satır sayısıdır. **Çıkarılan**, doğrulamada bizde zaten var çıktığı için düşen satırdır.
**Ö1, Ö2, Ö3** öncelik, **Küçük, Orta, Büyük** iş tahmini (bir günün altı, birkaç gün, bir hafta ve üstü).
**Sahip** senin kararını bekleyen, **Kapalı** daha önce verdiğin bir karara denk gelen satırdır.

| Dalga | Paket | Küçük | Orta | Büyük | Satır |
|---|---|---|---|---|---|
| Dalga 1 — Karar Gerekmez, Küçük İş, Öncelik 1 | 9 | 9 | 0 | 0 | 17 |
| Dalga 2 — Karar Gerekmez, Küçük İş Öncelik 2 Ya Da Orta İş Öncelik 1 | 30 | 3 | 27 | 0 | 74 |
| Dalga 3 — Karar Gerekmez, Orta İş Öncelik 2 Ya Da Büyük İş Öncelik 1 | 43 | 0 | 38 | 5 | 115 |
| Dalga 4 — Karar Gerekmez, Kalanlar | 8 | 0 | 0 | 8 | 29 |
| Sahibin Kararını Bekleyenler | 64 | 4 | 31 | 29 | 114 |
| **Toplam** | **154** | **16** | **96** | **42** | **349** |

4 paket başka bir kolun paketiyle aynı iş çıktı; dalga tablosunda ve rafta bir kez sayıldı.

## Genel Değerlendirme

On iki tabloda 567 eksik satırı kaldı: 78'i öncelik 1, 246'sı öncelik 2, 243'ü öncelik 3. Satırların 333'ü karar istemiyor, 197'si sahibin kararını bekliyor, 37'si kapalı bir karara denk geliyor. Çekirdek işler sağlam: oynatma, kayıpsız kesim, küçültme ve Windows'ta kayıt yerinde, güncellemede pazarın önündeyiz. Geride kaldığımız yer çekirdeğin çevresi: izler ve altyazı, uzun işin konforu, kayıt sonrası cila, Windows dışı kaydedici ve kullanıcıya ulaşma kanalları.

158 paketin 9'u hemen başlanabilecek küçük iş (Dalga 1), 30'u Dalga 2, 45'i Dalga 3, 9'u Dalga 4. 65 paket sahibin kararını bekliyor ve bu kararların çoğu aşağıdaki birkaç ortak soruya iniyor.

**Oynatıcı.** Eksik dosya açılırken ve liste yönetilirken çıkıyor. En ağırları HDR videonun ton eşlemesi (parlaklık aralığını sıradan ekrana sığdırma; OYA-11, OYA-P7), tercih edilen dilde ses ve altyazı seçimi (OYA-04, OYA-P5) ve izlerken ekranın kararması (OYA-01, OYA-P3). Geçmiş tutuluyor ama gösterilmiyor (OYB-11, OYB-P4). En ucuz kazanç OYB-P1, OYA-P1 ve OYB-P2.

**Düzenleyici.** Kesim çekirdeği pazarın ortasında, ama teslim yalnız ilk görüntü ve ilk ses izini taşıyor (DZA-02, DZA-03; DZA-P7 ve DZA-P5). Kurgu yönünde ortak asgari kümedeki 11 kalemin 2'si tam, 3'ü yarım, 6'sı yok; en ağır üçü (DZB-02, DZB-17, DZB-01) tek şerit kapsamından çıkma kararı istiyor. En ucuz kazanç DZA-P1 ve DZB-P1.

**Küçült.** Eksik çekirdekte değil, uzun işin konforunda: koşan kodlamayı duraklatma beş rakipte var (SIK-01, SIK-P3), dış ses izi ekleme HandBrake'te 62 oyla en çok istenen (SIK-03, SIK-P4), gece bırakılan kuyruk bilgisayar uyuyunca yarıda kalıyor (SIK-06, SIK-P2). En ucuz kazanç SIK-P1.

**Dönüştür.** Tek dosyalık ince bir yol; açığın büyük kısmı bağlanmamış kod: 14 satırın motoru Küçült yolunda hazır. En ağır kusur sessiz kayıp: akış eşlemesi yazılmadığı için ikinci ses ve altyazı çıktıya geçmiyor (DON-01, DON-02, DON-03; DON-P4, DON-P2, DON-P3). Ardından toplu dönüştürme (DON-06, DON-P6) ve adlı ön ayar (DON-05, DON-P5) geliyor. En ucuz kazanç DON-P1.

**Kaydedici.** Windows'ta çekirdek yakalama yerinde; açık çevrede ve Windows dışında. Yakalamada en ağır dört satır (EKA-02, EKA-03, EKA-04, EKA-01) sahibin kararını bekliyor. Pazarın son iki yılda en çok eklediği şey kayıt sonrası cila: otomatik yakınlaştırma 13 üründe görüldü (EKB-01, EKB-P9), bizde hiç yok. En acil küçük iş EKB-P1: tuş gösterimi açıkken yazılan şifre harf harf kayda giriyor (EKB-11). Karar istemeyenlerde EKB-P2, EKB-P4 ve EKA-P1 öne çıkıyor.

**Ayarlar Ve Kabuk.** Görünüm ve dil sayısında güçlüyüz (36 palet, 42 dil klasörü). Eksik güven tarafında: ayar yedeği yok ve bozuk ayar sessizce sıfırlanıyor (AYR-01, AYR-42; AYR-P2), pencere yerini hatırlamıyor (AYR-03, AYR-P1), güncellemenin ne getirdiği gösterilmiyor (AYR-P3). Erişilebilirlikte 283 erişilebilir ad var ama hiçbir deneme kaydı yok (AYR-P7). En ucuz kazanç AYR-P3'ün küçük satırları (AYR-47, AYR-10).

**Dağıtım Ve Otomasyon.** Güncellemede öndeyiz, ulaşmada gerideyiz: iki platformda da imzasızız ve beş paket dizininin (kullanıcının tek komutla program kurduğu kataloglar: winget, Scoop, Chocolatey, Flathub, Homebrew) hiçbirinde yokuz. 42 satırın 24'ü sahibin kararını bekliyor; en ağırları Windows kod imzası (DAG-02, DAG-P6) ve Flathub (DAG-04, DAG-P8). Öncelik 1 satırların en ucuzu winget (DAG-01, DAG-P5). Komut satırı bugün yalnız küçültüyor (DAG-P2, DAG-P4).

**Yeni Araçlar.** Araçlar diye bir sekme yok; 44 aracın 19'u yarım, 25'i hiç yok. Geride kalınan yer motor değil, ekran ve kaynak kabulü: Dönüştür ses dosyasını, GIF'i ve resim dizisini kaynak olarak almıyor (ARC-19, ARC-12, ARC-09; ARC-P3 ve ARC-P4). 16 satır karar beklemeden var olan sekmelere girer, 23 satır sahibi bekler. En ucuz kazanç ARC-P1.

**Platform Eşitliği.** Bu tablo ayrı bir soruya bakıyor: Windows'ta olup macOS ya da Linux'ta eksik kalan. Dönüştürme, küçültme ve oynatma üç platformda çalışıyor; kaydedici Windows dışında yarım. Rakiplerin yerleşik verdiği üç şey bizde yok: Wayland'da (yeni Linux masaüstlerinin görüntü sistemi) yakalama, Mac'te sürücüsüz sistem sesi ve kamera katmanı (PLT-02, PLT-03, PLT-06; işleri EKA-P14, EKA-P12 ve EKA-P9 taşıyor). 44 satırın 24'ü komşu tablolarla ortak. En acil küçük iş PLT-P1: kameralı Mac'te ekran yerine kamera kaydediliyor olabilir (PLT-01); gerçek Mac'te ölçülmedi.

Sekmeleri aşan ortak temalar:

- Motor hazır, ekrana bağlı değil. Dönüştür'de 14 satırın motoru Küçült yolunda duruyor (DON-04); ekran üstü bildirim rozeti hazır ama çağrılmıyor (OYA-07); metin biçimi modeli hazır, paneli yok (DZB-19); Hızlı kipteki kayma hesaplanıyor ama gösterilmiyor (DZA-05). İlk dalganın çoğu bu türden.
- İzler sessizce düşüyor. Dönüştür ikinci sesi ve altyazıyı (DON-01, DON-02), Düzenleyici ilk görüntü ve ilk ses dışındaki izleri (DZA-02) taşımıyor; dış ses ekleme iki sekmede birden isteniyor (SIK-03, DON-07).
- Uzun işin konforu. Uyku engeli hem oynatıcıda hem kodlamada eksik (OYA-01, SIK-06); duraklatma, süreç önceliği, bitiş bildirimi ve sesi aynı aileden (SIK-01, SIK-02, AYR-19, AYR-20).
- Model ve büyük indirme sorusu beş tabloda aynı: büyütme (DON-P15, ARC-P11), kamera arka planı (EKB-P11), gürültü modeli (EKA-P11), hazır içerik (DZB-P16). Tek ilke kararı beş paketin yolunu belirler.
- Komut satırı ve dış denetim. Komut satırı yalnız küçültüyor; dönüştürme, kayıt, toplu teslim ve çalışan uygulamaya komut ayrı tablolarda ayrı ayrı istenmiş (DAG-P2, DON-P13, EKA-P5, DZA-P9, DAG-P4).

## İlk On Paket

1. OYB-P1 — HLS Ve Liste Adresi Düzeltmesi: liste uzantısıyla biten yayın adresi bugün hiç açılmıyor (OYB-20); bu bir eksik değil hata ve tek denetimle kapanıyor.
2. PLT-P1 — macOS Ekran Seçimi Adla: kameralı Mac'te ekran yerine kamera kaydediliyor olabilir (PLT-01); tek fonksiyonluk düzeltme kaydedicinin Mac'teki ilk işini güvene alır.
3. DON-P1 — Dönüştür'de Donanım Kodlayıcıları: motor bu kodlayıcıları zaten sürüyor, Dönüştür'de yalnız seçilemiyor (DON-04); ekran kartı olan kullanıcı dönüştürmeyi onunla yapabilir.
4. SIK-P1 — Kodlama Süreç Önceliği Ayarı: kodlama süreci bugün öncelik atanmadan açılıyor (SIK-02, SIK-20); öncelik ve iş parçacığı ayarıyla kodlama sürerken bilgisayar başka işlerde takılmaz.
5. OYA-P1 — Bildirim, Ses Düzeyi Ve Bölüm Adları: ses düzeyi hatırlanmıyor, hazır duran ekran üstü rozet çağrılmıyor, bölüm adları görünmüyor (OYA-06, OYA-07, OYA-09); üçü de her oturumda hissedilen küçük eksikler.
6. DZA-P1 — Hızlı Kip Kayma Bildirimi: Hızlı kipte kesim noktasının ne kadar kaydığı hesaplanıyor ama kullanıcıya söylenmiyor (DZA-05); yanına anahtar kareye atlama komutu geliyor (DZA-12) ve kullanıcı çıktıyı açmadan farkı görüyor.
7. OYB-P2 — Çoklu Bırakma Ve Klasör Açma: oynatıcıya bırakmada tek dosya şartı var ve klasör alt klasörleriyle kuyruğa alınamıyor (OYB-01, OYB-07); birden çok dosya bırakılınca sıra kendiliğinden kurulur.
8. DZB-P1 — Metin Biçim Ve Süsleme: metin biçimi için model hazır, yalnız panel eksik (DZB-19); kenarlık, gölge ve arka plan kutusu yazıyı her zeminde okunur kılar (DZB-21).
9. ARC-P1 — Dosyada Gürültü Temizleme Ve Yükseklik Raporu: gürültü süzgeci kaydedicide hazır ama var olan dosyaya uygulanamıyor (ARC-24); yanına dosyanın ses yüksekliğini ölçen rapor geliyor (ARC-04).
10. DON-P2 — Dönüştür'de Altyazı İzleri: Dalga 2'den; kaynaktaki gömülü altyazı Dönüştür'de çıktıya geçmiyor (DON-02) ve kullanıcı kaybı dosyayı açınca fark ediyor; ses izi kararının akış planı da bu işe dayanıyor.

| Sıra | Paket | Sekme | Ad | İş | Öncelik | Karar | Dalga |
|---|---|---|---|---|---|---|---|
| 1 | OYB-P1 | Oynatıcı | HLS Ve Liste Adresi Düzeltmesi | küçük | 1 | gerekmez | 1 |
| 2 | PLT-P1 | Platform Eşitliği | macOS Ekran Seçimi Adla | küçük | 1 | gerekmez | 1 |
| 3 | DON-P1 | Dönüştür | Dönüştür'de Donanım Kodlayıcıları | küçük | 1 | gerekmez | 1 |
| 4 | SIK-P1 | Küçült | Kodlama Süreç Önceliği Ayarı | küçük | 1 | gerekmez | 1 |
| 5 | OYA-P1 | Oynatıcı | Bildirim, Ses Düzeyi Ve Bölüm Adları | küçük | 1 | gerekmez | 1 |
| 6 | DZA-P1 | Düzenleyici | Hızlı Kip Kayma Bildirimi | küçük | 1 | gerekmez | 1 |
| 7 | OYB-P2 | Oynatıcı | Çoklu Bırakma Ve Klasör Açma | küçük | 1 | gerekmez | 1 |
| 8 | DZB-P1 | Düzenleyici | Metin Biçim Ve Süsleme | küçük | 1 | gerekmez | 1 |
| 9 | ARC-P1 | Bizde Olmayan Araçlar | Dosyada Gürültü Temizleme Ve Yükseklik Raporu | küçük | 1 | gerekmez | 1 |
| 10 | DON-P2 | Dönüştür | Dönüştür'de Altyazı İzleri | orta | 1 | gerekmez | 2 |

## Sekme Sekme

Her kolun altında doğrulama kolunun kendi özeti, paket tablosu ve paketlerin planı var.
Satır numaraları (örnek `OYA-07`) o kolun doğrulanmış tablosundaki satırı gösterir.

### Oynatıcı

#### Oynatma Çekirdeği (OYA)

55 eksik yazıldı, doğrulamada 0 satır çıktı, 55 satır kaldı. Öncelik 1, 2, 3: 12, 17, 26.
10 satır sahibin kararını bekliyor, 9 satır kapalı bir karara denk geliyor. 12 paket önerildi.

- Çekirdek oynatma sağlam: iz seçimi, gecikme, A-B tekrarı, kare kare ilerleme, yer imi, bölüm atlama, önizleme küçük resmi, görüntü ve ses süzgeçleri var. Eksiklerin çoğu rakiplerde hazır gelen rahatlık davranışları.
- 55 satırın 9'u önceki kararla kapalı (8 satır K4, 1 satır K5). Kalan 46 satırın 12'si öncelik 1, 17'si öncelik 2.
- En ağır üç eksik: HDR videonun sıradan ekranda doğru görünmesi (OYA-11), ses ve altyazı dil tercihi (OYA-04), uyku ve ekran kararması engeli (OYA-01).
- En ucuz üç kazanç: ses düzeyini hatırlama (OYA-06), ekran üstü bildirimin kapsamı (OYA-07; rozet hazır, yalnız çağrılmıyor), alt klasörde altyazı arama (OYA-29).
- Araştırmanın `küçük` dediği 40 işin 7'si `orta` çıktı; 33'ü küçük kaldı.
- Fareye dokunan iki satır (OYA-02, OYA-38) koddaki bilinçli bir karara ve kararsız bir teste denk geliyor; sahibin kararı olmadan başlanmamalı.
- On iki paketin dördü sahibin kararını bekliyor (P5, P6, P7, P8); sekizi karar beklemeden yapılabilir.

| Paket | Ad | Satırlar | İş | Öncelik | Karar | Dalga |
|---|---|---|---|---|---|---|
| OYA-P1 | Bildirim, Ses Düzeyi Ve Bölüm Adları | OYA-06, OYA-07, OYA-09 | küçük | 1 | gerekmez | 1 |
| OYA-P2 | Altyazı Bulma Ve Tuşları | OYA-25, OYA-26, OYA-29, OYA-30, OYA-32 | orta | 1 | gerekmez | 2 |
| OYA-P3 | Uyku Engeli Ve Pencere Davranışı | OYA-01, OYA-08, OYA-49, OYA-50 | orta | 1 | gerekmez | 2 |
| OYA-P4 | Ses Aygıtı Ve Çıkış Seçenekleri | OYA-03, OYA-21, OYA-22, OYA-23 | orta | 1 | gerekmez | 2 |
| OYA-P5 | Açılış Varsayılanları | OYA-04, OYA-31, OYA-36, OYA-51 | orta | 1 | sahip | K |
| OYA-P6 | Çift Tık Ve Basılı Tutma | OYA-02, OYA-38 | orta | 1 | sahip | K |
| OYA-P7 | HDR Ton Eşleme | OYA-11 | orta | 1 | sahip | K |
| OYA-P8 | Sessiz Bölümleri Atlama | OYA-37 | orta | 1 | sahip | K |
| OYA-P9 | Dosya Başına Hatırlama Ve Gezinme | OYA-05, OYA-10, OYA-55 | orta | 2 | gerekmez | 3 |
| OYA-P10 | Altyazı Biçemi Ve Kare Kaydı | OYA-27, OYA-33, OYA-34, OYA-43 | orta | 2 | gerekmez | 3 |
| OYA-P11 | Doldurma Ve Dokunmatik Hareketler | OYA-47, OYA-53 | orta | 2 | gerekmez | 3 |
| OYA-P12 | Küçük Resim Tablosu | OYA-44 | orta | 2 | gerekmez | 3 |

- **OYA-P1 — Bildirim, Ses Düzeyi Ve Bölüm Adları.** Ses düzeyi ve sessiz durumu `PlayerSettings`'e iki alan olarak yazılır, açılışta okunur. Hazır duran `ShowOsd` rozeti (PlayerView.Gosterge.cs:72) iz değişimi, gecikme, A-B noktası, bölüm atlama ve kare kaydı sonucunda da çağrılır; bugün bu olaylar yalnız izleme günlüğüne düşüyor. Bölüm adı mpv'nin `chapter-list/N/title` özelliğinden okunur, zaman çubuğu işaretinde ve bölüm atlama bildiriminde gösterilir, sağ tık menüsüne bölüm listesi eklenir. Doğrulama: `OynaticiGorunumTests` ve `OynaticiParcaTests` içinde rozet metni, ayar gidiş-dönüşü ve bölüm adı okuma.
  - Dokunacağı yer: src/VidShrink.App/Playback/PlayerView.axaml.cs, src/VidShrink.App/Playback/PlayerView.Tracks.cs, src/VidShrink.App/Playback/PlayerView.Gosterge.cs, src/VidShrink.App/Playback/PlayerSettings.cs, src/VidShrink.App/Playback/PlayerView.Window.cs, src/VidShrink.Player/MpvEngine.cs, src/VidShrink.Player/IPlaybackEngine.cs
- **OYA-P2 — Altyazı Bulma Ve Tuşları.** `SubtitleOptions.Sidecars` videonun klasörüne ek olarak `Subs`, `Subtitles` gibi alt klasörlere de bakar; uzantı listesine `.sup` ve `.idx` eklenir. Motora üç küçük üye eklenir: `sub-visibility` (izi değiştirmeden gizle), `sub-seek` ile `sub-step` (önceki ya da sonraki satıra git, altyazıyı bir satır kaydır) ve `sub-text` (ekrandaki satırı okuyup panoya yaz). Her biri `Keymap`'e yeni eylem ve tuş olarak girer. Doğrulama: geçici klasörde alt klasör taraması testi; örnek SRT ile satır atlamanın konumu değiştirdiği ve gizlemenin iz numarasını değiştirmediği motor testi.
  - Dokunacağı yer: src/VidShrink.App/Playback/SubtitleOptions.cs, src/VidShrink.App/Playback/Keymap.cs, src/VidShrink.App/Playback/PlayerView.Tracks.cs, src/VidShrink.Player/MpvEngine.cs, src/VidShrink.Player/IPlaybackEngine.cs
- **OYA-P3 — Uyku Engeli Ve Pencere Davranışı.** Oynatma sürerken ekranın kapanmasını engelleyen küçük bir sınıf yazılır: Windows'ta `SetThreadExecutionState`, macOS'ta `IOPMAssertion`, Linux'ta masaüstünün ekran koruyucu engelleme isteği; duraklayınca ve kapanınca engel kalkar. Linux kolu yeni paket isterse yalnız Windows ve macOS teslim edilir, Linux sahibe sorulur. Aynı oynat ya da duraklat olayına üç küçük davranış bağlanır: fare durunca imleci gizleme, pencere simge durumuna inince duraklatma (ayar, kapalı gelir) ve yalnız oynatırken üstte tutma seçeneği. Doğrulama: engelin oynatmada alınıp duraklamada bırakıldığını gösteren sahte sağlayıcılı test, Windows'ta `powercfg /requests` çıktısıyla elle ölçüm.
  - Dokunacağı yer: src/VidShrink.App/Playback/PlayerView.axaml.cs, src/VidShrink.App/Playback/PlayerView.Window.cs, src/VidShrink.App/Playback/PlayerView.Fare.cs, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/Playback/
- **OYA-P4 — Ses Aygıtı Ve Çıkış Seçenekleri.** Motor aygıtları mpv'nin `audio-device-list` özelliğinden okur, seçim `audio-device` ile yazılır; sağ tık menüsüne ses çıkışı alt menüsü eklenir, seçim `PlayerSettings`'te saklanır, aygıt yoksa otomatiğe dönülür. Gelişmiş panele üç anahtar eklenir: `audio-spdif` (Dolby ve DTS'i çözmeden alıcıya gönderme), `audio-exclusive` (özel kip) ve `audio-channels` (stereoya indirme); üçü de kapalı gelir, bugünkü ses yolu değişmez. Doğrulama: motor testinde listenin okunması ve seçeneğin geri okunması. Passthrough ve özel kip bu makinede denenemez; alıcısı olan bir kurulumda elle ölçüm gerekir.
  - Dokunacağı yer: src/VidShrink.Player/MpvEngine.cs, src/VidShrink.Player/IPlaybackEngine.cs, src/VidShrink.App/Playback/PlayerView.Tracks.cs, src/VidShrink.App/Playback/PlayerSettings.cs, src/VidShrink.App/Playback/PlayerAdvancedPanel.axaml.cs
- **OYA-P5 — Açılış Varsayılanları.** Ayarlara ses ve altyazı için dil tercihi eklenir; `ApplyTrackOptions` açılışta iz listesindeki dil alanına bakıp eşleşen izi seçer, zorunlu (forced) izi `track-list/N/forced` ile tanır; tercih boşsa bugünkü davranış kalır. Devam sınırı `PlaybackHistory.ResumeRecent` (5) kaldırılır ya da kayıt tavanına (200) çekilir. Deinterlace (taramalı videodaki tarak izini giderme) anahtarı `auto` değerini de alır. Sahibin onaylamadığı satır paketten düşer; doğrulama çok dilli örnekle iz seçimi, altıncı dosyada `ResumeFor` ve taramalı örnekte `deinterlace` geri okuması testleridir.
  - Dokunacağı yer: src/VidShrink.App/Playback/PlayerView.Tracks.cs, src/VidShrink.App/Playback/PlayerSettings.cs, src/VidShrink.App/Playback/PlaybackHistory.cs, src/VidShrink.Player/MpvEngine.cs, src/VidShrink.Player/MpvEngine.Advanced.cs
  - Sahibin kararı: dosya açılırken dört varsayılan değişsin mi: tercih edilen dilde ses ve altyazı izi seçimi, zorunlu altyazının kendiliğinden açılması, devamdaki son beş dosya sınırının kalkması ve deinterlace'in otomatik olması
- **OYA-P6 — Çift Tık Ve Basılı Tutma.** Bugün sol tık işini bırakışta yapıyor ve çift tık bilerek ayrı hareket sayılmıyor (PlayerView.Fare.cs:45, 88–94). `PlayerInputKind`'e çift basış ve basılı tutma türleri eklenir: ikinci tık ilk tıkın etkisini geri alıp tam ekrana geçer; basış bekleme eşiğini aşarsa hız geçici olarak yükselir, bırakınca eski hıza döner ve tık sayılmaz. Eşik süresi tema belirtecinden okunur, sürükleme eşiği korunur. Doğrulama: `FarePress` ve `FareRelease` üstünden zamanlı görünüm testleri; kararsız `SolTikOynatmayiDegistirmez` testi bu paketle birlikte ele alınır.
  - Dokunacağı yer: src/VidShrink.App/Playback/PlayerView.Fare.cs, src/VidShrink.App/Playback/Keymap.cs, src/VidShrink.App/Playback/PlayerInputMap.cs, src/VidShrink.App/Playback/PlayerShortcutsPanel.axaml.cs
  - Sahibin kararı: sol tık üç anlam taşısın mı: tek tık oynat ya da duraklat (bugünkü), çift tık tam ekran, basılı tutunca bırakana dek hızlı oynatma
- **OYA-P7 — HDR Ton Eşleme.** Önce ölçüm: bir HDR10 ve bir HLG örnek bugünkü yolda açılıp kare kaydedilir, soluk ya da patlak görünüp görünmediği `docs/olcumler/`e yazılır. Sonra etiketli bir `vf` halkası (ffmpeg `zscale` ve `tonemap` süzgeçleri) eklenir; halka yalnız aktarım eğrisi PQ ya da HLG iken takılır (MediaDetails.cs:26–38). Süzgeçler libmpv derlemesinde yoksa paket burada durur ve sahibe döner. Doğrulama: aynı karede eşlemeli ve eşlemesiz ortalama parlaklık farkı; 1080p ve 4K'da `Stats` ile düşen kare sayısı.
  - Dokunacağı yer: tools/VidShrink.Bench/, docs/olcumler/, src/VidShrink.Player/MpvEngine.Advanced.cs, src/VidShrink.App/Playback/PlayerAdvancedPanel.axaml.cs
  - Sahibin kararı: HDR video işlemcide ton eşlemeyle gösterilsin mi; 4K'da kare düşürürse yalnız elle açılan bir seçenek mi olsun
- **OYA-P8 — Sessiz Bölümleri Atlama.** Etiketli bir `af` halkası olarak ffmpeg `silencedetect` süzgeci takılır; motor süzgecin bildirdiği sessizlik başlangıcında hızı yükseltir, bitişinde eski hıza döner. Atlamak yerine hızlandırmak ses ile görüntünün eşliğini bozmaz. Kip bir anahtarla açılır ve kapalı gelir; eşik ile en kısa sessizlik süresi ayardır. Doğrulama: ortasında bilinen uzunlukta sessizlik olan üretilmiş örnekte oynatma süresinin kısaldığı ve konuşmalı kısımda hızın değişmediği motor testi.
  - Dokunacağı yer: src/VidShrink.Player/MpvEngine.Advanced.cs, src/VidShrink.Player/IPlaybackEngine.cs, src/VidShrink.App/Playback/PlayerView.axaml.cs, src/VidShrink.App/Playback/Keymap.cs
  - Sahibin kararı: sessiz yerleri hızlandıran yeni bir oynatma kipi eklensin mi; hazır sunan rakip bulunamadı, istek yalnız oyla kanıtlı
- **OYA-P9 — Dosya Başına Hatırlama Ve Gezinme.** `PlaybackHistory.Entry`'ye ses izi, altyazı izi, ikincil altyazı, gecikmeler ve hız eklenir; `ApplyTrackOptions` bugün her açılışta sıfırladığı bu değerleri kayıttan geri yükler. Zamana git için küçük bir giriş kutusu ve tuşu eklenir; yazılan saat `_seek.GoTo` ile uygulanır. Atlama adımları (1, 10, 60 ve 300 sn) sabit olmaktan çıkıp `PlayerSettings`'ten okunur; `Keymap` satırları ve şerit düğmeleri aynı değeri kullanır, varsayılanlar değişmez. Doğrulama: kayıt gidiş-dönüş testi, eski kayıt dosyasının okunabildiği test, ayarlı adımla atlama testi.
  - Dokunacağı yer: src/VidShrink.App/Playback/PlaybackHistory.cs, src/VidShrink.App/Playback/PlayerView.Tracks.cs, src/VidShrink.App/Playback/Keymap.cs, src/VidShrink.App/Playback/PlayerSettings.cs, src/VidShrink.App/Playback/PlayerView.Serit.cs
- **OYA-P10 — Altyazı Biçemi Ve Kare Kaydı.** Önce ölçüm: biçemli (ASS) bir altyazıda bugünkü yazı tipi ve renk ayarının uygulanıp uygulanmadığı görülür; uygulanmıyorsa `sub-ass-override` anahtarı eklenir. Yazı tipi listesi üç genel aileden sistem yazı tiplerine genişler, `sub-filter-sdh` ile işitme engelli notlarını gizleme anahtarı gelir. Kare kaydına altyazıyla kaydetme seçeneği eklenir: `screenshot-to-file` komutunun son değeri `video` yerine `subtitles` olur (MpvEngine.cs:538). Doğrulama: ASS örneğinde biçem geri okuması, altyazılı ve altyazısız karenin piksel farkı.
  - Dokunacağı yer: src/VidShrink.Player/MpvEngine.Advanced.cs, src/VidShrink.Player/MpvEngine.cs, src/VidShrink.App/Playback/PlayerView.Advanced.cs, src/VidShrink.App/Playback/PlayerAdvancedPanel.axaml.cs, src/VidShrink.App/Playback/PlayerSettings.cs
- **OYA-P11 — Doldurma Ve Dokunmatik Hareketler.** Görüntüyü pencereye sığdıran ölçek (`Resize`, PlayerView.axaml.cs:854–862) için ikinci bir kip eklenir: doldur kipinde ölçek panoyu tümüyle kaplayacak biçimde seçilir, taşan kısım kırpılır; tek tuşla sığdır ile doldur arasında geçilir. Dokunmatik ekran için işaretçi türü ayrılır: iki parmakla yakınlaştırma, yatay kaydırmayla atlama, dikey kaydırmayla ses. Fare davranışı değişmez. Doğrulama: doldur kipinde kare ölçüsünün panoyu kapladığı görünüm testi; üretilmiş dokunma olaylarıyla hareket testi ve gerçek dokunmatik ekranda elle deneme.
  - Dokunacağı yer: src/VidShrink.App/Playback/PlayerView.axaml.cs, src/VidShrink.App/Playback/ZoomGesture.cs, src/VidShrink.App/Playback/PlayerView.Fare.cs, src/VidShrink.App/Playback/Keymap.cs
- **OYA-P12 — Küçük Resim Tablosu.** Videodan eşit aralıklı kareler alıp tek resimde dizen bir araç eklenir: ffmpeg `select` ve `tile` süzgeçleriyle tek komut, argümanı `Core` üretir. Oynatıcının araçlar menüsüne küçük resim tablosu girer; satır ve sütun sayısı seçilir, çıktı kare kaydı klasörüne yazılır. Doğrulama: argüman üretimi için birim testi, kısa örnekte çıkan resmin beklenen ölçüde olduğu test.
  - Dokunacağı yer: src/VidShrink.Core/, src/VidShrink.Ffmpeg/, src/VidShrink.App/Playback/PlayerView.Tools.cs
  - Aynı iş: ARC-P7. Rafta o başlığın altında duruyor.

Pakete girmeyenler:

- OYA-12, OYA-13, OYA-14, OYA-16, OYA-17, OYA-18, OYA-19, OYA-20: K4 ile kapalı (oynatıcıda GPU çizim yolu yapılmayacak).
- OYA-35: K5 ile kapalı (otomatik altyazı yapılmayacak).
- OYA-15, OYA-24, OYA-28, OYA-39, OYA-40, OYA-41, OYA-42, OYA-45, OYA-46, OYA-48, OYA-52, OYA-54: öncelik 3 ve hiçbir paketin yanında bedavaya gelmiyor; her biri ayrı arayüz parçası, ayrı ölçüm ya da sahibin kararını ister.

Kanıt: [doğrulanmış tablo](piyasa/2026-10-13/oynatici-cekirdek-dogrulanmis.md) · [ham tablo](piyasa/2026-10-13/oynatici-cekirdek-eksikler.md) · [notlar ve kaynaklar](piyasa/2026-10-13/oynatici-cekirdek.md)

#### Liste, Kitaplık Ve Ağ (OYB)

47 eksik yazıldı, doğrulamada 0 satır çıktı, 47 satır kaldı. Öncelik 1, 2, 3: 6, 23, 18.
20 satır sahibin kararını bekliyor, 2 satır kapalı bir karara denk geliyor. 12 paket önerildi.

- Neredeyiz: temel taşlar yerinde. Bellekte kuyruk, klasör gezintisi, dokuz liste biçimini okuma, m3u8 kaydı, son dosyalar, yer imi ve adres açma var.
- Pazarın gerisinde kaldığımız üç yer: listeyi yönetmek, geçmişi göstermek ve işletim sisteminin ortam denetimine bağlanmak.
- En ağır eksik OYB-20: liste uzantısıyla biten yayın adresi açılmıyor. Bu bir eksik değil, hata.
- İkinci ve üçüncü ağır eksik: OYB-29 (pencere arkadayken ortam tuşları ve Windows ses kutucuğu çalışmıyor) ve OYB-11 ile OYB-12 (200 kayıtlık geçmiş tutuluyor ama gösterilmiyor, devam son beş dosyayla sınırlı).
- En ucuz üç kazanç: OYB-20 (tek denetimle kapanır), OYB-12 (tek sabit, yalnız sahibin kararını bekliyor) ve OYB-01 (bırakmadaki tek dosya şartını kaldırmak).
- On iki paket önerildi: sekizi karar beklemeden başlayabilir, dördü sahibin kararını bekliyor.
- Kitaplık, ağ paylaşımı, eklenti ve uzaktan denetim satırları ürünün yönünü değiştirir; iki karar paketinde toplandı (OYB-P11, OYB-P12). Kapalı karara denk gelen iki satır (OYB-25, OYB-26) pakete girmedi.

| Paket | Ad | Satırlar | İş | Öncelik | Karar | Dalga |
|---|---|---|---|---|---|---|
| OYB-P1 | HLS Ve Liste Adresi Düzeltmesi | OYB-20 | küçük | 1 | gerekmez | 1 |
| OYB-P2 | Çoklu Bırakma Ve Klasör Açma | OYB-01, OYB-07 | küçük | 1 | gerekmez | 1 |
| OYB-P3 | Liste Penceresinde Arama Ve Sıralama | OYB-02, OYB-03, OYB-04, OYB-05 | orta | 1 | gerekmez | 2 |
| OYB-P4 | Geçmiş Penceresi Ve Devam Ayarı | OYB-11, OYB-13, OYB-14 | orta | 1 | gerekmez | 2 |
| OYB-P5 | Devam Kapsamı Ve Kalıcı Kuyruk | OYB-08, OYB-12 | orta | 1 | sahip | K |
| OYB-P6 | Windows Ortam Denetimi Bağlantısı | OYB-29 | orta | 1 | sahip | K |
| OYB-P7 | Kabukta Çoklu Açma Ve Sıraya Ekleme | OYB-32, OYB-33, OYB-34 | orta | 2 | gerekmez | 3 |
| OYB-P8 | Yer İmi Listesi Ve Sık Kullanılanlar | OYB-15, OYB-16 | orta | 2 | gerekmez | 3 |
| OYB-P9 | Mac Ortam Denetimi Bağlantısı | OYB-30 | orta | 2 | gerekmez | 3 |
| OYB-P10 | Akış Kalitesi Ve Kesintisiz Çalma | OYB-21, OYB-43 | büyük | 2 | gerekmez | 4 |
| OYB-P11 | Dış Denetim Ve Eklenti Yüzeyleri | OYB-31, OYB-35, OYB-39, OYB-40, OYB-41 | büyük | 2 | sahip | K |
| OYB-P12 | Kitaplık Ve Ağ Kaynakları | OYB-09, OYB-17, OYB-22, OYB-24 | büyük | 2 | sahip | K |

- **OYB-P1 — HLS Ve Liste Adresi Düzeltmesi.** Kuyruk çözümü (`ResolveQueue`) yolu liste saymadan önce uzak adres mi diye bakar; adres ise liste yoluna girmeden motora olduğu gibi verilir. Liste dosyasının içindeki liste uzantılı adres girdisi de artık atılmaz (`PlaylistFile.cs:48`). Önce düşen bir test yazılır (`.m3u8` ve `.pls` ile biten adreste kuyruk kurulmadığı, motora adresin gittiği), sonra yerel bir http sunucusundan gerçek HLS adresi açılarak görülür. HLS olmayan uzak liste (örneğin radyo `.pls` adresi) motorun kendi listesine düşer; arayüzün bunu nasıl gösterdiği aynı pakette ölçülür.
  - Dokunacağı yer: src/VidShrink.App/Playback/PlayerView.Kuyruk.cs, src/VidShrink.Core/PlaylistFile.cs, tests/VidShrink.Tests/OynaticiAdresTests.cs
- **OYB-P2 — Çoklu Bırakma Ve Klasör Açma.** Bırakmadaki tek dosya şartı kalkar (`PlayerView.Window.cs:473`); bırakılan dosyalar ve klasörlerin içindeki oynatılabilir dosyalar, alt klasörlerle birlikte, doğal ad sırasıyla kuyruğa yazılır ve ilki çalar. Aynı toplayıcıya menüden bir "Klasör aç" komutu bağlanır. Küçült kolundaki toplayıcı (`src/VidShrink.Core/DroppedMedia.cs:9`) örnek alınır ama olduğu gibi kullanılmaz: yalnız küçültme adaylarını alır ve alt klasöre inmez. Başsız testle kuyruğun sayısı ve sırası ölçülür.
  - Dokunacağı yer: src/VidShrink.App/Playback/PlayerView.Window.cs, src/VidShrink.App/Playback/PlayerView.Kuyruk.cs, src/VidShrink.App/Playback/FolderNavigator.cs, src/VidShrink.App/Locales/
- **OYB-P3 — Liste Penceresinde Arama Ve Sıralama.** Liste penceresine yazdıkça süzen bir arama kutusu ve ada ya da tarihe göre dizme (ters çevirmeyle) eklenir; ikisi de küçüktür ve önce teslim edilir. Ardından kuyruk düzenleme işlemlerine (`QueueEdit`) yukarı ve aşağı taşıma, satıra da geçmiş deposunda kayıtlı süre ile kalınan yer eklenir. Elle dizilmiş kuyruğun karıştırma kipinden etkilenmeme kuralı (`_queueArranged`) korunur. Başsız testle süzme, sıra ve taşıma ölçülür.
  - Dokunacağı yer: src/VidShrink.App/Playback/PlayerView.Playlist.cs, src/VidShrink.App/Playback/PlayerView.ListeMenusu.cs, src/VidShrink.App/Playback/PlayerView.Kuyruk.cs, src/VidShrink.App/Playback/FolderNavigator.cs, src/VidShrink.App/Locales/
- **OYB-P4 — Geçmiş Penceresi Ve Devam Ayarı.** Zaten tutulan 200 kayıtlık geçmiş deposunun üstüne tarih sırasıyla gösteren, yazarak aranan ve seçilen dosyayı açan bir pencere konur. Son dosyalar menüsüne "Geçmiş…" ve "Geçmişi temizle" satırları, ayarlara "geçmişi tutma" ve "kaldığı yerden devam et" anahtarları eklenir; varsayılanlar bugünkü davranışla aynı kalır. Adresin geçmişe girmeme kararı (`docs/plan-url-oynatma.md:59`) korunur. Başsız testle liste, arama, temizleme ve ayar kapalıyken diske yazılmadığı ölçülür.
  - Dokunacağı yer: src/VidShrink.App/Playback/PlaybackHistory.cs, src/VidShrink.App/Playback/RecentFiles.cs, src/VidShrink.App/Playback/PlayerView.Window.cs, src/VidShrink.App/Playback/PlayerSettings.cs, src/VidShrink.App/Locales/
- **OYB-P5 — Devam Kapsamı Ve Kalıcı Kuyruk.** Devam sınırı tek sabittir (`PlaybackHistory.cs:18`); karar "kalksın" olursa sabit deponun sığasına (200 kayıt) çekilir ve sınırı ölçen testler birlikte güncellenir. Kuyruk için kapanışta kuyruk ve çalan öge diske yazılır, açılışta geri yüklenir; diskte artık olmayan dosyalar atılır. İki soru birbirinden bağımsızdır: biri "evet" alırsa o yarı tek başına yapılır. Kardeş tablodaki OYA-36 aynı iştir ve orada OYA-P5 paketine girmiştir; iş bir kez yapılır.
  - Dokunacağı yer: src/VidShrink.App/Playback/PlaybackHistory.cs, src/VidShrink.App/Playback/PlayerView.Kuyruk.cs, src/VidShrink.App/Playback/PlayerSettings.cs
  - Sahibin kararı: kaldığı yerden devam son beş dosyayla sınırlı kalsın mı ve uygulama açılınca son kuyruk geri gelsin mi
- **OYB-P6 — Windows Ortam Denetimi Bağlantısı.** İlk adım karar istemez: motor seçeneklerine `media-controls` yazılır ve gerçek pencerede Windows ses kutucuğunda görünüp görünmediği, ortam tuşlarının pencere arkadayken çalışıp çalışmadığı ölçülür. Yetmezse pencere Windows'un ortam denetimi arayüzüne doğrudan bağlanır (MPC-BE'nin yolu); bu yeni bir Windows SDK bağı demektir ve sahibin kararını ister. İki yolda da sonraki ve önceki tuşu uygulamanın kendi kuyruğuna bağlanmalıdır, çünkü kuyruk motora verilmiyor.
  - Dokunacağı yer: src/VidShrink.Player/MpvEngine.cs, src/VidShrink.App/Playback/Keymap.cs, src/VidShrink.App/Playback/PlayerView.axaml.cs
  - Sahibin kararı: libmpv'nin kendi ortam denetimi anahtarı yetmezse Windows SDK bağı eklensin mi
- **OYB-P7 — Kabukta Çoklu Açma Ve Sıraya Ekleme.** İkinci örnek artık bütün yolları çalışan örneğe aktarır; gelen yollar OYB-P2'nin toplayıcısıyla, yeni açılış yerine kuyruğa eklenir. Sağ tık menüsüne "Sıraya ekle" fiili ve onu taşıyan bir komut satırı bayrağı gelir; bayrak kuyruğa ekleme işlemine (`Enqueue`) bağlanır. Ses ve liste uzantıları yalnız "aç" ve "sıraya ekle" fiillerine eklenir, "küçült" fiiline eklenmez. Kanal ve kabuk kaydı testleriyle, sonra Explorer'da elle çoklu seçimle doğrulanır.
  - Dokunacağı yer: src/VidShrink.App/Program.cs, src/VidShrink.App/Integration/ForwardedFiles.cs, src/VidShrink.App/MainWindow.SingleInstance.cs, src/VidShrink.Core/Setup/ShellRegistration.cs, src/VidShrink.App/ShellMenu.cs, Install-VidShrink.ps1, tests/VidShrink.Tests/KabukMenusuTests.cs
- **OYB-P8 — Yer İmi Listesi Ve Sık Kullanılanlar.** Yer imine isteğe bağlı bir ad alanı eklenir (adsız eski kayıt okunmaya devam eder); bir pencere dosyanın yer imlerini listeler, seçilene gider ve tek tek siler. Ayrıca dosya, klasör ve adres tutan kalıcı bir sık kullanılanlar listesi menüye eklenir. Adres saklanırken sorgunun ve parolanın diske yazılmama kuralı (`docs/plan-url-oynatma.md:26`) uygulanır. Başsız testle ekleme, adlandırma, silme ve yeniden açılışta kalıcılık ölçülür.
  - Dokunacağı yer: src/VidShrink.App/Playback/PlaybackHistory.cs, src/VidShrink.App/Playback/Keymap.cs, src/VidShrink.App/Playback/PlayerView.Window.cs, src/VidShrink.App/Locales/
- **OYB-P9 — Mac Ortam Denetimi Bağlantısı.** mpv'nin ortam tuşları seçeneği (`input-media-keys`) gömülü kullanımda varsayılan kapalıdır; önce macOS'ta açılır ve klavye ortam tuşlarının, Denetim Merkezi'ndeki çalan ortam alanının oynatıcıyı yönetip yönetmediği ölçülür. Yetmezse macOS'un ortam denetimi arayüzüne ayrı bir bağ yazılır. Sonraki ve önceki tuşu yine uygulamanın kuyruğuna bağlanır. Bu iş Windows makinesinde doğrulanamaz; Mac'te koşan bir işçi ister.
  - Dokunacağı yer: src/VidShrink.Player/MpvEngine.cs, src/VidShrink.App/Playback/
- **OYB-P10 — Akış Kalitesi Ve Kesintisiz Çalma.** İki adımda teslim edilir ve adımlar ayrı işçilere verilebilir. Önce akış kalitesi (orta): uyarlamalı yayının katmanlarının motorun iz listesinde nasıl göründüğü ölçülür, sonra iz menüsüne kalite seçimi eklenir (video izi değiştirme ya da `hls-bitrate` seçeneği); OYB-P1 bitmeden başlamaz. Sonra kesintisiz çalma (büyük): sıradaki dosyayı önden yükleme (`prefetch-playlist`, `gapless-audio`) kuyruğun motora verilmemesi kararına dokunur, bu yüzden önce o kararın gerekçesi (`PlayerView.Kuyruk.cs:13`) yeniden ölçülür. İki katmanlı yerel bir HLS yayını ve ardışık iki ses dosyasıyla doğrulanır.
  - Dokunacağı yer: src/VidShrink.Player/MpvEngine.cs, src/VidShrink.Player/IPlaybackEngine.cs, src/VidShrink.App/Playback/PlayerView.Tracks.cs, src/VidShrink.App/Playback/PlayerView.Kuyruk.cs
- **OYB-P11 — Dış Denetim Ve Eklenti Yüzeyleri.** Bu bir karar paketidir; karar gelince satır başına ayrı işlere bölünür. En ucuz ilk adım çalışan oynatıcıya komut göndermektir (OYB-40): tek örnek kanalı bugün yalnız yol taşıyor, aynı kanala duraklat, sar ve sıraya ekle komutları eklenir; yeni pencere seçeneği (OYB-35) aynı dosyalara dokunur. Linux ortam denetimi (OYB-31) ve ağdan uzaktan denetim (OYB-39) bu komut katmanının üstüne kurulur. Eklenti sistemi (OYB-41) bilerek kapatılmış `load-scripts=no` seçeneğini geri açtığı için en sona kalır.
  - Dokunacağı yer: src/VidShrink.App/Program.cs, src/VidShrink.Core/SingleInstanceChannel.cs, src/VidShrink.App/Integration/, src/VidShrink.Player/MpvEngine.cs
  - Sahibin kararı: oynatıcı dışarıdan yönetilebilen bir yüzey açsın mı (komut kanalı, Linux ortam denetimi, ağdan uzaktan denetim, eklenti) ve tek örnek kuralına "yeni pencere" seçeneği gelsin mi
- **OYB-P12 — Kitaplık Ve Ağ Kaynakları.** Bu da bir karar paketidir; dört satır dört ayrı iştir ve her biri ayrı "evet" ister. En küçük adım adlı listelerdir (OYB-09): bugünkü m3u8 yazma yolu, uygulamanın kendi klasöründe tutulan adlı listelere genişler. Site bağlantısı çözme (OYB-22) kapalı K1 kararıyla aynı aracı kullanır ve bilerek yazılmış `ytdl=no` seçeneğini geri açar; ağ paylaşımı (OYB-24) parola saklama, kitaplık (OYB-17) yeni bir sekme demektir.
  - Dokunacağı yer: src/VidShrink.App/Playback/, src/VidShrink.Core/PlaylistFile.cs, src/VidShrink.Core/MediaAddress.cs, src/VidShrink.Player/MpvEngine.cs
  - Sahibin kararı: oynatıcı kitaplık ve ağ kaynağı yönüne büyüsün mü (adlı listeler, klasör tarayan kitaplık, site bağlantısı çözme, ağ paylaşımında gezme)

Pakete girmeyenler:

On yedi satır pakete girmedi.

- OYB-25, OYB-26: kapalı karar K10'a denk geliyor; kapalı satır pakete girmez.
- OYB-18, OYB-19, OYB-28: öncelik 3; dış servis hesabı, bulut ya da sunucu ister ve ayrı bir ürün sınıfıdır.
- OYB-23, OYB-27, OYB-38, OYB-42, OYB-46: öncelik 3 ve sahibin kararını bekliyor; hiçbir paketin yanında bedavaya gelmiyor (OYB-46 kardeş tabloda OYA-52).
- OYB-06, OYB-10, OYB-44, OYB-45, OYB-47: öncelik 3 ve karar istemiyor, ama her biri kendi başına yeni bir arayüz parçası; komşu paketlerin dosyalarına dokunmuyor.
- OYB-36, OYB-37: öncelik 3; doğal komşusu OYB-P6 sahibin kararını beklediği için, karar istemeyen bu iki satır oraya karıştırılmadı.

Kanıt: [doğrulanmış tablo](piyasa/2026-10-13/oynatici-kitaplik-dogrulanmis.md) · [ham tablo](piyasa/2026-10-13/oynatici-kitaplik-eksikler.md) · [notlar ve kaynaklar](piyasa/2026-10-13/oynatici-kitaplik.md)

### Düzenleyici

#### Kayıpsız Kesim Ve Birleştirme (DZA)

44 eksik yazıldı, doğrulamada 0 satır çıktı, 44 satır kaldı. Öncelik 1, 2, 3: 8, 19, 17.
4 satır sahibin kararını bekliyor, 2 satır kapalı bir karara denk geliyor. 12 paket önerildi.

- Kesim çekirdeği pazarın ortasında: üç teslim kipi, dört kodekte akıllı kesim, sessizlik, siyah kare ve sahne taraması, çok kaynak var.
- Geride kaldığımız yer izler ve kap (dosyanın görüntü, ses ve altyazıyı bir arada tutan dış biçimi): teslim yalnız ilk görüntü ve ilk sesi taşıyor.
- En ağır üç eksik: izlerin düşmesi ve seçilememesi (DZA-02, DZA-03), çok kaynağın her kipte yeniden kodlanması (DZA-01), toplu teslimin olmaması (DZA-09).
- En ucuz üç kazanç: kayma bildirimi (DZA-05, sayı zaten hesaplanıyor), otomatik bölme (DZA-06, bölen kod var), önizlemede iz düğmesi (DZA-10, seçim menüde zaten var).
- Kodda ölçülünce hiçbir satır tam çıkmadı; üç satırda durumumuz tablonun dediğinden iyi (DZA-10, DZA-33, DZA-39).
- Üç satırın önceliği düştü (DZA-10, DZA-11, DZA-23); kalan sekiz öncelik 1 satırının hepsi iki bağımsız kaynakta görüldü.
- On iki paketten ikisi sahibin kararını bekliyor (DZA-P6, DZA-P7); on paket karar beklemeden başlayabilir.

| Paket | Ad | Satırlar | İş | Öncelik | Karar | Dalga |
|---|---|---|---|---|---|---|
| DZA-P1 | Hızlı Kip Kayma Bildirimi | DZA-05, DZA-12, DZA-30 | küçük | 1 | gerekmez | 1 |
| DZA-P2 | Otomatik Bölme Ve Parça Adları | DZA-06, DZA-18, DZA-19, DZA-29, DZA-31 | orta | 1 | gerekmez | 2 |
| DZA-P3 | Tarama Aralıklarını Tek Tek Yönetme | DZA-07, DZA-20, DZA-21, DZA-22 | orta | 1 | gerekmez | 2 |
| DZA-P4 | Akıllı Kipte Ses Kodekleri | DZA-04 | orta | 1 | gerekmez | 2 |
| DZA-P5 | İz Seçim Paneli | DZA-03, DZA-10 | orta | 1 | gerekmez | 2 |
| DZA-P6 | Kayıpsız Birleştirme Yolu | DZA-01 | orta | 1 | sahip | K |
| DZA-P7 | Tüm İzleri Varsayılan Taşıma | DZA-02 | orta | 1 | sahip | K |
| DZA-P8 | Bölüm Yazma Ve Koruma | DZA-08, DZA-13, DZA-14 | orta | 2 | gerekmez | 3 |
| DZA-P9 | Komut Satırı Ve Toplu Teslim | DZA-28, DZA-09 | büyük | 1 | gerekmez | 3 |
| DZA-P10 | Kesim Listesi Ve Kurgu Aktarımı | DZA-15, DZA-16, DZA-17 | büyük | 2 | gerekmez | 4 |
| DZA-P11 | Kap İçi İz İşleri | DZA-24, DZA-11, DZA-25, DZA-26, DZA-23, DZA-44 | büyük | 2 | gerekmez | 4 |
| DZA-P12 | Akıllı Kipte MPEG-2 | DZA-27 | orta | 2 | gerekmez | 3 |

- **DZA-P1 — Hızlı Kip Kayma Bildirimi.** Hızlı kip kesim başını zaten önceki anahtar kareye (kendi başına çözülebilen tam kare) çekiyor (EditExport.cs:533, :572); aradaki fark plana yazılır ve teslim notlarına "çıktı şu kadar saniye erken başlar" satırı olarak eklenir. Çizelgenin elindeki anahtar kare listesiyle (EditorTimeline.Serit.cs:36-46) önceki ve sonraki anahtar kareye giden iki komut ve monitörde "anahtar karede" göstergesi gelir. Doğrulama: bilinen bir anahtar kare listesiyle plandaki sayıyı ve komutların vardığı kareyi ölçen birim testleri.
  - Dokunacağı yer: src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.App/Editing/EditorView.Teslim.cs, src/VidShrink.App/Editing/EditorKeymap.cs, src/VidShrink.App/Editing/EditorView.Kisayol.cs, src/VidShrink.App/Editing/EditorView.Monitor.cs, src/VidShrink.App/Locales/
- **DZA-P2 — Otomatik Bölme Ve Parça Adları.** Eşit sayı, sabit süre ya da hedef boyut için sınır listesi üretilir ve var olan bölme işlevine verilir (EditTimeline.cs:235); boyut, kaynağın ortalama bit hızından hesaplanır. Klibe etiket alanı eklenir; etiket ve işaretçiler proje dosyasına yazılır (EditProject.cs:94-135), parça adları Küçült'ün ad deseniyle kurulur (AdlandirmaDeseni.cs:18). Sahne eşiği aynı bölme alanına sayı kutusu olarak girer. Doğrulama: sınır hesabı, proje gidiş dönüşü ve ad üretimi birim testleri.
  - Dokunacağı yer: src/VidShrink.Core/Editing/EditTimeline.cs, src/VidShrink.Core/Editing/EditClip.cs, src/VidShrink.Core/Editing/EditOutputName.cs, src/VidShrink.Core/Editing/EditProject.cs, src/VidShrink.Core/AdlandirmaDeseni.cs, src/VidShrink.App/Editing/EditorView.Sahne.cs, src/VidShrink.App/Editing/EditorView.axaml
- **DZA-P3 — Tarama Aralıklarını Tek Tek Yönetme.** Tarama sonucu bugün topluca kesiliyor (EditorView.Sessizlik.cs:125-133); her aralık çizelgede tıklanır olur ve üç durumdan birini alır: kes, tut, hızlandır. Hızlandır, aralığı bölüp o parçanın hızını yükseltir; kenar payı ön ve arka diye ikiye ayrılır (SilenceCut.cs:29, :171-172), taranacak ses izi seçilir (SilenceCut.cs:90'daki `-map 0:a:0` yerine seçilen iz). Doğrulama: aralık durumlarından çıkan kesim listesini ölçen birim testleri ve başsız pencere testi.
  - Dokunacağı yer: src/VidShrink.Core/Editing/SilenceCut.cs, src/VidShrink.App/Editing/EditorView.Sessizlik.cs, src/VidShrink.App/Editing/EditorTimeline.cs, src/VidShrink.App/Editing/EditorView.axaml
- **DZA-P4 — Akıllı Kipte Ses Kodekleri.** Ses kapısı AAC ve koşullu Opus dışını Tam kipe düşürüyor (EditExport.cs:116-124). AC-3, E-AC-3 ve MP3 için bugünkü parça yolu (MPEG-TS ara dosyası, EditExport.cs:719-729) açılır; MPEG-TS'in (yayın akışı kabı) taşımadığı kodeklerde ara kap Matroska olur. Her kodek için üç ve daha çok kesimli örnekte ses ile görüntü arasındaki kayma `tools/VidShrink.Bench` ile ölçülür; eşiği aşan kodek kapının dışında kalır.
  - Dokunacağı yer: src/VidShrink.Core/Editing/EditExport.cs, tools/VidShrink.Bench
- **DZA-P5 — İz Seçim Paneli.** Teslim kutusuna kaynağın izlerini listeleyen panel gelir; varsayılan bugünkü gibi ilk görüntü ve ilk ses kalır, bu yüzden karar gerekmez. Seçim teslimin elle yazılmış `-map` satırlarına taşınır (EditExport.cs:541-542, :580-581, :656-657; ses parçası :725) ve projeye yazılır. Monitöre dinlenen iz için görünür düğme eklenir; oynatıcının var olan seçimi kullanılır (PlayerView.Tracks.cs:111-113) ve önizleme yeniden yüklenince korunur. Doğrulama: seçimden çıkan argümanları ölçen birim testleri ve çıktıda ffprobe ile iz sayımı.
  - Dokunacağı yer: src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.Core/Editing/EditProject.cs, src/VidShrink.App/Editing/EditorView.Teslim.cs, src/VidShrink.App/Editing/EditorView.Monitor.cs, src/VidShrink.App/Playback/PlayerView.Tracks.cs
- **DZA-P6 — Kayıpsız Birleştirme Yolu.** Çok kaynak bugün kipe bakmadan yeniden kodlama yoluna gidiyor (EditExport.cs:252). Kaynaklar arası uyum denetimi eklenir: kodek, profil, çözünürlük, piksel biçimi, kare hızı, ses kodeği ve örnekleme hızı. Hepsi tutuyorsa parçalar ffmpeg'in concat girişiyle (dosyaları yeniden kodlamadan uç uca okuyan liste) kopyalanır; tutmuyorsa bugünkü yol ve notu kalır. Doğrulama: uyum denetimi birim testleri, örnek çıktıda kare sayısı ve süre.
  - Dokunacağı yer: src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.Core/Editing/SmartCutCodec.cs, src/VidShrink.App/Editing/EditorView.Teslim.cs, src/VidShrink.App/Locales/
  - Sahibin kararı: çok kaynakta Hızlı ve Akıllı kip, kaynaklar uyumluysa yeniden kodlamak yerine kopyalasın mı (K7 her zaman yeniden kodlamayı seçmişti)
- **DZA-P7 — Tüm İzleri Varsayılan Taşıma.** DZA-P5'in paneli üstüne gelir: varsayılan seçim bütün ses, altyazı ve veri izleri olur. Altyazı ve ek ses izleri üç kipte de kesim aralığına göre kırpılır; Akıllı kipte her ses izi kendi parça dosyasıyla taşınır. Doğrulama: çok sesli ve altyazılı örnekte ffprobe ile iz sayısı ve süre eşitliği.
  - Dokunacağı yer: src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.App/Editing/EditorView.Teslim.cs
  - Sahibin kararı: teslim varsayılanı "ilk görüntü ve ilk ses" yerine "bütün izler" olsun mu
- **DZA-P8 — Bölüm Yazma Ve Koruma.** Çizelgeden bölüm listesi üreten saf bir işlev yazılır: parça sınırları (ad, DZA-P2 yapıldıysa parça etiketinden gelir) ve kaynağın kesimle kaymış bölümleri. Liste, Kaydedici'nin kullandığı yoldan (ffmpeg'in üst veri dosyası, RecorderChapters.cs:91 ve :109) teslimin son adımına girer. Açılışta kaynağın bölümleri (MediaInfo.cs:61) işaretçi ya da kesim noktası olarak önerilir; önce defterdeki Türkçe bölüm adı kusuru (.claude/acik.md:14) kapanır. Doğrulama: kaymış zaman hesabı birim testleri ve çıktıda ffprobe ile bölüm sayımı.
  - Dokunacağı yer: src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.Core/RecorderChapters.cs, src/VidShrink.Core/MediaInfo.cs, src/VidShrink.Ffmpeg/FfprobeClient.cs, src/VidShrink.App/Editing/EditorView.Kaynak.cs
- **DZA-P9 — Komut Satırı Ve Toplu Teslim.** Önce pencere açmadan "proje dosyası girer, teslim çıkar" koşan çekirdek işlev ayrılır ve komut satırına bağlanır; bugün komut satırı yalnız tek aralık kesiyor (CliRequest.cs:425-435). Sonra düzenleyiciye proje ve dosya sırası gelir; sıra Küçült'ün iş pompasıyla (JobPump.cs:8) art arda koşar, bir işin düşmesi ötekileri durdurmaz. Bölünecekse önce komut satırı (orta), sonra sıra. Doğrulama: komut satırı argüman testleri ve sahte teslimle sıra testi.
  - Dokunacağı yer: src/VidShrink.Cli/CliRequest.cs, src/VidShrink.Core/Editing/EditProject.cs, src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.Core/JobPump.cs, src/VidShrink.App/Editing/
- **DZA-P10 — Kesim Listesi Ve Kurgu Aktarımı.** Parça listesini dış biçimlere çeviren tek bir okuyucu ve yazıcı modülü yazılır: dışa CSV, EDL (kurgu programlarının düz metin kesim listesi) ve YouTube bölüm metni; içe CSV, EDL ve CUE. Aynı modül kesilmiş çizelgeyi Premiere Pro ile DaVinci Resolve'un açtığı Final Cut XML'ine de yazar. Bölünecekse önce liste biçimleri (orta), sonra XML (orta). Doğrulama: her biçim için gidiş dönüş birim testi; XML'in hedef programda açıldığını sahip gözle dener.
  - Dokunacağı yer: src/VidShrink.Core/Editing/, src/VidShrink.App/Editing/EditorView.Proje.cs
- **DZA-P11 — Kap İçi İz İşleri.** Görüntüye dokunmadan dosyayı yeniden paketleyen (`-c copy`) tek bir argüman üreticisi yazılır; DZA-P5'in iz paneli onun arayüzü olur. Üretici iz başına dil, ad ve varsayılan bayrağını, gecikmeyi (`-itsoffset`), döndürme bayrağını (`-display_rotation`), başka dosyadan iz ve kapak resmi eklemeyi, seçili izi ayrı dosyaya çıkarmayı kurar. Bölünecekse önce üst veri, kaydırma ve döndürme; sonra iz ekleme, çıkarma ve kapak. Doğrulama: argüman birim testleri ve çıktıda ffprobe ile üst veri okuması.
  - Dokunacağı yer: src/VidShrink.Core/Editing/, src/VidShrink.Core/StreamMapping.cs, src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.App/Editing/EditorView.Teslim.cs
- **DZA-P12 — Akıllı Kipte MPEG-2.** Akıllı kipin kodek kapısına (SmartCutCodec.cs:43) MPEG-2 eklenir; kenar parçası kaynağın profiline, kare hızına ve bit hızına uyan `mpeg2video` kodlayıcısıyla yazılır. TV kayıtlarının çoğu taramalı olduğundan taramalı kaynak kapısı (SmartCutCodec.cs:47) birlikte ele alınmazsa kazanç dar kalır; bu kayıtların sesi de genellikle AC-3 ya da MP2 (yayıncılığın eski ses kodeği) olduğundan paket DZA-P4'ten sonra anlam kazanır. Doğrulama: birleşim noktasında kare sayısı ve bozulma ölçümü `tools/VidShrink.Bench` ile.
  - Dokunacağı yer: src/VidShrink.Core/Editing/SmartCutCodec.cs, src/VidShrink.Core/Editing/EditExport.cs, tools/VidShrink.Bench

Pakete girmeyenler:

- DZA-32: tek rakip ve dar kullanım; DZA-P11'in yeniden paketleme üreticisi çıktıktan sonra küçük bir ek olur.
- DZA-33: çekirdeği hazır ama DZA-P3 zaten dolu; o paket bittikten sonra yarım günlük ek.
- DZA-34: büyük iş, tek rakip, yalnız TV kaydına yarıyor.
- DZA-35: Akıllı kipin renk kapısını açmak ölçüm ister, kitle dar.
- DZA-36: defterde zaten açık; önce ölçülmesi gerekiyor, kod işi sonra belli olur.
- DZA-37: büyük iş ve sahip kararı; DZA-P6'nın kararı çıkmadan anlamı yok.
- DZA-38: tek rakip; bit akışı süzgeci kodek başına ayrı iş ister.
- DZA-39: büyük kısmı monitör menüsünde zaten var, kalan dar.
- DZA-40: sahip kararı (yeni uzantı ve kurucu kaydı) ister, tek rakip.
- DZA-41: tek rakip, dar kitle.
- DZA-42: kapalı, K5.
- DZA-43: kapalı, K1.

Kanıt: [doğrulanmış tablo](piyasa/2026-10-13/duzenleyici-kesim-dogrulanmis.md) · [ham tablo](piyasa/2026-10-13/duzenleyici-kesim-eksikler.md) · [notlar ve kaynaklar](piyasa/2026-10-13/duzenleyici-kesim.md)

#### Kurgu Yönü (DZB)

77 eksik yazıldı, doğrulamada 0 satır çıktı, 77 satır kaldı. Öncelik 1, 2, 3: 7, 40, 30.
29 satır sahibin kararını bekliyor, 5 satır kapalı bir karara denk geliyor. 16 paket önerildi.

- Kesme ve birleştirme tarafı sağlam: çok kaynak, üç teslim kipi, klip başına kırpma, hız, solma ve bir metin izi var. Kurgu programlarının ortak asgari kümesindeki 11 kalemin 2'si tam, 3'ü yarım, 6'sı yok.
- Tablo 77 satırla kaldı: 7'si öncelik 1, 40'ı öncelik 2, 30'u öncelik 3. Beş satır önceki kararla kapalı (K8 ve K5).
- En ağır üç eksik: ikinci video izi (DZB-02), geçişin canlı önizlemesi (DZB-17), müzik izi (DZB-01). Üçü de "tek şerit" kapsamından çıkma kararı ister.
- En ucuz üç kazanç: metin biçim denetimleri (DZB-19; model hazır, yalnız panel eksik), dışa aktarım ayarları (DZB-54; sabitler seçeneğe döner), temel renk ayarı (DZB-35; tek süzgeç, önizleme yolu hazır).
- Paketlenen 51 satırın 20'si sahibin kararını bekliyor: 11'i çok iz ve geçiş kapsamına, 9'u disk yükü, hazır içerik ve modele bağlı.
- Öneri: önce karar beklemeyen ilk beş paket (DZB-P1 ile DZB-P5) yapılsın, aynı gün üç kapsam kararı sorulsun.

| Paket | Ad | Satırlar | İş | Öncelik | Karar | Dalga |
|---|---|---|---|---|---|---|
| DZB-P1 | Metin Biçim Ve Süsleme | DZB-19, DZB-21 | küçük | 1 | gerekmez | 1 |
| DZB-P2 | Dışa Aktarım Ayarları Ve Ön Ayarlar | DZB-54, DZB-55, DZB-58 | orta | 1 | gerekmez | 2 |
| DZB-P3 | Temel Renk Ve Hazır Görünüm | DZB-35, DZB-30 | orta | 1 | gerekmez | 2 |
| DZB-P4 | Klip Gruplama Ve Pano | DZB-12, DZB-11 | küçük | 2 | gerekmez | 2 |
| DZB-P5 | Ses Temizleme Ve Düzey | DZB-43, DZB-45, DZB-42, DZB-41 | orta | 2 | gerekmez | 3 |
| DZB-P6 | Çapraz Geçiş Teslimi | DZB-16, DZB-18 | orta | 1 | sahip | K |
| DZB-P7 | Müzik İzi | DZB-01 | orta | 1 | sahip | K |
| DZB-P8 | Geçiş Önizleme Yolu | DZB-17 | büyük | 1 | sahip | K |
| DZB-P9 | Metin Konumu, Hareketi Ve Altyazı | DZB-20, DZB-24, DZB-23, DZB-62 | büyük | 2 | gerekmez | 4 |
| DZB-P10 | Resim Klibi, Tuval Ve Dönüşüm | DZB-04, DZB-05, DZB-03, DZB-56, DZB-49 | büyük | 2 | gerekmez | 4 |
| DZB-P11 | Medya Havuzu Ve Araya Ekleme | DZB-64, DZB-08 | orta | 2 | gerekmez | 3 |
| DZB-P12 | İkinci Video İzi Ve Yeşil Perde | DZB-02, DZB-29 | büyük | 1 | sahip | K |
| DZB-P13 | İleri Renk Ve Bulanıklık | DZB-36, DZB-38, DZB-28 | büyük | 2 | gerekmez | 4 |
| DZB-P14 | Anahtar Kare Ve Hız Rampası | DZB-25, DZB-48, DZB-50, DZB-26 | büyük | 2 | gerekmez | 4 |
| DZB-P15 | Çok İz Üstüne Gelenler | DZB-10, DZB-46, DZB-47, DZB-06, DZB-07 | büyük | 2 | sahip | K |
| DZB-P16 | Vekil, Hazır İçerik Ve Model | DZB-51, DZB-22, DZB-52, DZB-53, DZB-44, DZB-69, DZB-70, DZB-71, DZB-72 | büyük | 2 | sahip | K |

- **DZB-P1 — Metin Biçim Ve Süsleme.** Metin paneline yazı tipi, kalın, italik ve solma süresi denetimleri eklenir; model bu alanları zaten tutuyor (TextLayer.cs:43-57), yalnız arayüz bağlanır. Kenarlık kalınlığı, gölge ve arka plan kutusu için modele üç alan eklenir ve AssWriter'ın stil satırına yazılır (ASS, metnin biçimini de taşıyan altyazı dosyasıdır). Önizleme ve teslim aynı .ass dosyasını okuduğu için ikinci bir yol gerekmez. Doğrulama: DuzenleyiciMetinTests'e stil satırı ölçüsü ve aynı andaki önizleme karesi ile teslim karesinin karşılaştırması.
  - Dokunacağı yer: src/VidShrink.App/Editing/EditorView.axaml, src/VidShrink.App/Editing/EditorView.Metin.cs, src/VidShrink.Core/Editing/TextLayer.cs, src/VidShrink.Core/Editing/AssWriter.cs, src/VidShrink.Core/Editing/EditProject.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/DuzenleyiciMetinTests.cs
- **DZB-P2 — Dışa Aktarım Ayarları Ve Ön Ayarlar.** Tam kipin sabitleri (EditExport.cs:68-70, :366-367) seçeneğe döner: çözünürlük, kare hızı, kodek, kalite ve ses bit hızı; bugünkü değerler varsayılan kalır, böylece davranış değişmez. Üstüne birkaç hazır hedef (1080p, 720p, dikey) ve kullanıcının kendi ön ayarı gelir; Küçült sekmesinin ön ayar deposu yeniden kullanılır. Yalnız ses çıktısı aynı pencerede tek seçenektir (görüntüsüz çıktı ve ses kabı). Doğrulama: argüman testleri ve çıktı dosyasında ölçülen çözünürlük, kare hızı ve kodek.
  - Dokunacağı yer: src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.App/Editing/EditorView.Teslim.cs, src/VidShrink.App/Editing/EditorView.axaml, src/VidShrink.App/MainWindow.OnAyar.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/
- **DZB-P3 — Temel Renk Ve Hazır Görünüm.** Klip ayarlarına parlaklık, kontrast, doygunluk ve renk sıcaklığı eklenir; teslimde ffmpeg `eq` ve `colortemperature` süzgeçleriyle, önizlemede klip ayarlarının bugün geçtiği EditLook zinciriyle uygulanır. Hazır görünümler (siyah beyaz, eski film) aynı alanların kayıtlı değerleridir; LUT dosyası kullanılmaz, o yol K8 ile rafta. İlk iş bu süzgeçlerin zaman aralığına bağlanabildiğini ölçmektir, çünkü önizleme bütün çizelgeye tek zincir kuruyor. Doğrulama: DuzenleyiciKlipOzellikTests'e argüman ölçüsü ve bir kare karşılaştırması.
  - Dokunacağı yer: src/VidShrink.Core/Editing/ClipEffects.cs, src/VidShrink.Core/Editing/ClipFilters.cs, src/VidShrink.Core/Editing/EditLook.cs, src/VidShrink.Core/Editing/EditProject.cs, src/VidShrink.App/Editing/EditorView.Klip.cs, src/VidShrink.App/Editing/EditorView.axaml, src/VidShrink.App/Locales/, tests/VidShrink.Tests/DuzenleyiciKlipOzellikTests.cs
- **DZB-P4 — Klip Gruplama Ve Pano.** Kliplere grup kimliği eklenir; taşıma ve silme grubun bütününe uygulanır (çoklu silme hazır: EditTimeline.cs:362). Kopyala, kes, yapıştır ve çoğalt seçili klipleri bir tampona alıp oynatma başına koyar; komutlar EditorKeymap'e ve çizelgenin sağ tık menüsüne girer. Doğrulama: EditTimeline birim testleri ve her işlemin tek adımda geri alınması.
  - Dokunacağı yer: src/VidShrink.Core/Editing/EditTimeline.cs, src/VidShrink.App/Editing/EditorTimeline.cs, src/VidShrink.App/Editing/EditorKeymap.cs, src/VidShrink.App/Editing/EditorView.axaml, src/VidShrink.App/Locales/, tests/VidShrink.Tests/
- **DZB-P5 — Ses Temizleme Ve Düzey.** Klip paneline gürültü azaltma (`afftdn`), yükseklik eşitleme (`loudnorm`; sabiti StreamMapping.cs:357'de hazır), ekolayzır, kompresör ve sınırlayıcı (`equalizer`, `acompressor`, `alimiter`) eklenir ve ClipFilters'ın ses zincirine (ClipFilters.cs:79) bağlanır. Düzey göstergesi ayrı parçadır: oynarken tepe düzeyi mpv'den okunur; okuma yolu kodda yok, önce o ölçülür. Doğrulama: argüman testleri ve teslim edilen dosyada ölçülen yükseklik değeri.
  - Dokunacağı yer: src/VidShrink.Core/Editing/ClipEffects.cs, src/VidShrink.Core/Editing/ClipFilters.cs, src/VidShrink.Core/Editing/EditLook.cs, src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.Core/StreamMapping.cs, src/VidShrink.App/Editing/EditorView.Klip.cs, src/VidShrink.App/Editing/EditorView.axaml, src/VidShrink.Player/MpvEngine.cs, src/VidShrink.App/Locales/
- **DZB-P6 — Çapraz Geçiş Teslimi.** İki klibin sınırına geçiş süresi ve türü yazılır; Tam kipte görüntü ffmpeg `xfade`, ses `acrossfade` süzgeciyle birleştirilir. Tür listesi `xfade`'in hazır desenlerinden gelir (erime, silme, kaydırma, iris), ek içerik gerekmez. Geçişli kurgu Hızlı ve Akıllı kipte teslim edilemez; bugün efektlerde olduğu gibi Tam kipe düşer. Bu pakette canlı önizleme yok (DZB-P8); doğrulama argüman testi ve toplam sürenin geçiş süresi kadar kısaldığının ölçümü.
  - Dokunacağı yer: src/VidShrink.Core/Editing/EditTimeline.cs, src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.Core/Editing/EditProject.cs, src/VidShrink.App/Editing/EditorTimeline.cs, src/VidShrink.App/Editing/EditorView.Klip.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/
  - Sahibin kararı: Düzenleyicinin yazılı kapsamı "geçişsiz"den çıkıp iki klip arasında çapraz geçişi kapsasın mı?
- **DZB-P7 — Müzik İzi.** Çizelgeye A2 izi eklenir; görüntüsü olmayan dosya artık reddedilmez (EditorView.Kaynak.cs:89-93), bu ize başlangıç anı ve düzeyiyle konur. Teslimde iki ses ffmpeg `amix` süzgeciyle karıştırılır. Önizlemede iki sesi birlikte duymanın yolu ölçülmedi: mpv tek ses izi çalar, karıştırma `lavfi-complex` ister ve bunun `edl://` ile çalışması ilk iş denenir. Doğrulama: teslim dosyasında iki sesin de duyulduğunu gösteren düzey ölçümü ve proje dosyasını yazıp geri okuma testi.
  - Dokunacağı yer: src/VidShrink.Core/Editing/EditTimeline.cs, src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.Core/Editing/EdlPreview.cs, src/VidShrink.Core/Editing/EditProject.cs, src/VidShrink.App/Editing/EditorTimeline.cs, src/VidShrink.App/Editing/EditorView.Kaynak.cs, src/VidShrink.Player/MpvEngine.cs, tests/VidShrink.Tests/
  - Sahibin kararı: Düzenleyici tek şeritten çıkıp videonun altına ikinci bir ses izi (müzik, anlatım) alsın mı?
- **DZB-P8 — Geçiş Önizleme Yolu.** Önerilen yol kısa ön-işleme: iki klibin bindiği birkaç saniye arka planda küçük bir ara dosyaya işlenir ve önizleme listesine ayrı parça olarak girer; var olan `edl://` önizlemesi bozulmaz, yeni bağımlılık gelmez. Karardan önce iki ölçüm yapılır: 1080p'de bir saniyelik geçişin hazırlanma süresi ve `lavfi-complex` ile `edl://` birlikteliği. MLT (Shotcut ve Kdenlive'ın kullandığı açık kaynak bileşim motoru) yeni ikili dosya dağıtımı demektir, o yüzden son seçenek. Doğrulama: aynı andaki önizleme karesi ile teslim karesinin karşılaştırması.
  - Dokunacağı yer: src/VidShrink.Core/Editing/EdlPreview.cs, src/VidShrink.Core/Editing/EditLook.cs, src/VidShrink.App/Editing/EditorView.Monitor.cs, src/VidShrink.Player/MpvEngine.cs, tools/VidShrink.Bench
  - Sahibin kararı: Geçişin ve bindirmenin canlı önizlemesi hangi yoldan yapılsın: libmpv süzgeç grafiği, geçiş bölgesini kısa ön-işleme, yoksa ayrı bir bileşim motoru (MLT)?
- **DZB-P9 — Metin Konumu, Hareketi Ve Altyazı.** Metnin yeri panelden ve monitörde sürükleyerek verilir, hazır giriş ve çıkış hareketleri (kayarak gelme, daktilo) eklenir; model konumu ve anahtar kareyi (bir değerin zaman içinde değiştiği işaret noktası) zaten tutuyor (TextLayer.cs:92, AssWriter.cs:196-197), eksik olan arayüz. Altyazı dosyası (SRT, VTT, ASS) metin izine alınır ve geri yazılır; ffmpeg bu biçimleri .ass'e çevirebildiği için tek ayrıştırıcı yeter. Paket ikiye bölünebilir: konum ve hareket (DZB-20, DZB-24, DZB-23) ile altyazı alışverişi (DZB-62). Doğrulama: DuzenleyiciMetinTests'e konum ve hareket ölçüleri, altyazıda içe alıp geri yazma testi.
  - Dokunacağı yer: src/VidShrink.App/Editing/EditorView.Metin.cs, src/VidShrink.App/Editing/EditorView.Monitor.cs, src/VidShrink.App/Editing/EditorView.axaml, src/VidShrink.Core/Editing/TextLayer.cs, src/VidShrink.Core/Editing/AssWriter.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/DuzenleyiciMetinTests.cs
- **DZB-P10 — Resim Klibi, Tuval Ve Dönüşüm.** Resim ve düz renk, eklenirken kısa bir ara videoya çevrilir ve bildik kaynak gibi çizelgeye girer; önizleme yolu değişmez. Kare dondurma aynı yoldan gelir: oynatma başındaki kare resme çıkarılıp klip yapılır. Klibe serbest ölçek, konum ve açı, projeye de çıktı çerçevesi (9:16, 1:1, 16:9) eklenir; ikisi de ClipFilters'ın geometri zincirinden (ClipFilters.cs:38) geçer. Paket ikiye bölünebilir: resim, renk ve dondurma (DZB-04, DZB-05, DZB-49) ile dönüşüm ve tuval (DZB-03, DZB-56); doğrulama argüman testleri ve kare karşılaştırması.
  - Dokunacağı yer: src/VidShrink.App/Editing/EditorView.Kaynak.cs, src/VidShrink.App/Editing/EditorView.Klip.cs, src/VidShrink.Core/Editing/EditTimeline.cs, src/VidShrink.Core/Editing/ClipEffects.cs, src/VidShrink.Core/Editing/ClipFilters.cs, src/VidShrink.Core/Editing/EditLook.cs, src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.Core/Editing/EditProject.cs, tests/VidShrink.Tests/
- **DZB-P11 — Medya Havuzu Ve Araya Ekleme.** Projeye alınan kaynaklar bir listede görünür (proje dosyası ek kaynakları zaten tutuyor: EditProject.cs:68); listeden çizelgeye sürüklenen klip bırakıldığı yere girer. Araya ekleme sonraki klipleri iter, üstüne yazma aynı süreyi silip yerine koyar (aralık silme hazır: EditTimeline.cs:148). Dosya ekleyince çizelgenin sonuna gitme davranışı varsayılan kalır, o yüzden karar gerekmez. Doğrulama: DuzenleyiciCokKaynakTests'e iki kipin süre ve sıra ölçüleri.
  - Dokunacağı yer: src/VidShrink.App/Editing/EditorView.Kaynak.cs, src/VidShrink.App/Editing/EditorView.axaml, src/VidShrink.App/Editing/EditorTimeline.cs, src/VidShrink.Core/Editing/EditTimeline.cs, src/VidShrink.Core/Editing/EditProject.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/DuzenleyiciCokKaynakTests.cs
- **DZB-P12 — İkinci Video İzi Ve Yeşil Perde.** Çizelgeye V2 izi eklenir; üstteki klip başlangıç anı, boyutu ve konumuyla tutulur, teslimde ffmpeg `overlay` süzgeciyle bindirilir. Yeşil perde aynı klibe tek seçenektir: bindirmeden önce `chromakey` uygulanır (kaydedicideki hazır sabit: src/VidShrink.Core/RecorderArguments.cs:928). Canlı önizleme DZB-P8'de seçilen yoldan gelir; o karar verilmeden bu paket açılmaz. Doğrulama: argüman testi ve bindirilen bölgenin teslim karesinde ölçülmesi.
  - Dokunacağı yer: src/VidShrink.Core/Editing/EditTimeline.cs, src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.Core/Editing/EdlPreview.cs, src/VidShrink.Core/Editing/EditProject.cs, src/VidShrink.App/Editing/EditorTimeline.cs, src/VidShrink.App/Editing/EditorView.Klip.cs, tests/VidShrink.Tests/
  - Sahibin kararı: Düzenleyici tek şeritten çıkıp üstte ikinci bir video izi (resim içinde resim) alsın mı?
- **DZB-P13 — İleri Renk Ve Bulanıklık.** DZB-P3'ün üstüne gelir: eğriler ve üç bölgeli renk dengesi ffmpeg `curves` ve `colorbalance` süzgeçleriyle, tek tıkla düzeltme `normalize` ile yapılır. Bulanıklık `gblur` ile bütün kareye ya da monitörde çizilen dikdörtgene uygulanır (kırp, bulanıklaştır, geri bindir). İki çekimin rengini eşlemek için hazır süzgeç bilinmiyor; o yarı ayrı ölçüm ister. Paket ikiye bölünebilir: renk (DZB-36, DZB-38) ile bulanıklık (DZB-28); doğrulama argüman testleri ve kare karşılaştırması.
  - Dokunacağı yer: src/VidShrink.Core/Editing/ClipEffects.cs, src/VidShrink.Core/Editing/ClipFilters.cs, src/VidShrink.Core/Editing/EditLook.cs, src/VidShrink.App/Editing/EditorView.Klip.cs, src/VidShrink.App/Editing/EditorView.Monitor.cs, src/VidShrink.App/Editing/EditorView.axaml, src/VidShrink.App/Locales/, tests/VidShrink.Tests/DuzenleyiciKlipOzellikTests.cs
- **DZB-P14 — Anahtar Kare Ve Hız Rampası.** Klip ayarları (ses düzeyi, kırpma, konum) klip içinde zamanla değişebilsin diye değerlere anahtar kare eklenir; süzgeç zinciri sabit sayı yerine zamana bağlı ifade yazar, yumuşatma eğrisi iki kare arasındaki geçişin biçimidir. Hız rampası klibi hız dilimlerine böler (`setpts`), akıcı ağır çekim ara kareyi `minterpolate` süzgeciyle üretir. Önizleme zinciri bugün sabit değer taşıyor; zamana bağlı ifadeyi taşıyıp taşımadığı ilk iş ölçülür. Paket ikiye bölünebilir: anahtar kare (DZB-25, DZB-26) ile hız (DZB-48, DZB-50); doğrulama argüman testleri ve çıktı süresinin ölçümü.
  - Dokunacağı yer: src/VidShrink.Core/Editing/ClipEffects.cs, src/VidShrink.Core/Editing/ClipFilters.cs, src/VidShrink.Core/Editing/EditLook.cs, src/VidShrink.Core/Editing/EditTimeline.cs, src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.Core/Editing/EditProject.cs, src/VidShrink.App/Editing/EditorView.Klip.cs, src/VidShrink.App/Editing/EditorTimeline.cs, tests/VidShrink.Tests/
- **DZB-P15 — Çok İz Üstüne Gelenler.** DZB-P7 ve DZB-P12 bitmeden açılmaz ve üç parçaya bölünebilir. Ses ekleri (DZB-10, DZB-46, DZB-47): klibin sesi ikinci ize ayrı parça olarak iner, müzik konuşma başlayınca `sidechaincompress` süzgeciyle kısılır, anlatım kaydedicinin hazır mikrofon yoluyla kaydedilip ize konur. İz denetimleri (DZB-06) izi susturma, gizleme ve kilitlemedir; boşluklu çizelge (DZB-07) sıralı listeyi konumlu modele çevirdiği için en ağır parçadır. Doğrulama: her parça için EditTimeline birim testleri ve teslim dosyasında ses düzeyi ölçümü.
  - Dokunacağı yer: src/VidShrink.Core/Editing/EditTimeline.cs, src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.Core/Editing/EditProject.cs, src/VidShrink.Core/AudioCaptureArguments.cs, src/VidShrink.App/Editing/EditorTimeline.cs, src/VidShrink.App/Editing/EditorView.axaml, tests/VidShrink.Tests/
  - Sahibin kararı: Çok iz açıldıktan sonra üstüne gelen işler (sesi ayırma, otomatik müzik kısma, seslendirme kaydı, iz denetimleri, boşluklu çizelge) kapsama girsin mi?
- **DZB-P16 — Vekil, Hazır İçerik Ve Model.** Bu paket bir karar demetidir, tek seferde teslim edilmez: sahip hangi satırı açarsa o satır kendi paketi olur. Vekil dosya (ağır kaynağın kurgu sırasında kullanılan küçük kopyası; DZB-51) indirme istemez, yalnız disk yeri ister ve en bağımsız parçadır. Hazır içerik (DZB-22, DZB-52, DZB-53) lisans ve paket boyutu, modelli işler (DZB-44, DZB-69, DZB-70, DZB-71, DZB-72) indirme ve çalışma zamanı sorusudur. Doğrulama her satır için ayrı kurulur.
  - Dokunacağı yer: src/VidShrink.Core/Editing/, src/VidShrink.Core/IntermediateCodecs.cs, src/VidShrink.App/Editing/
  - Sahibin kararı: Uygulama kullanıcının diskine büyük ek dosya (vekil kopya, hazır içerik paketi, yapay zekâ modeli) üretsin ya da indirsin mi; hangisi istekle, hangisi hiç?

Pakete girmeyenler:

- DZB-31, DZB-32, DZB-33, DZB-63, DZB-68 — önceki kararla kapalı (K8 ve K5); karar geri açılmadan iş açılmaz.
- DZB-13, DZB-14, DZB-15 — dar kitleye bakan üç kırpma aracı; her biri ayrı sürükleme davranışı istediği için bir paketin yanında bedavaya gelmiyor, istenirse üçü tek küçük paket olur.
- DZB-09, DZB-60, DZB-66 — profesyonel kurgu akışına bakan büyük işler; pazarda tek ya da dar kitlede.
- DZB-40, DZB-67, DZB-76 — çok ize bağlı; DZB-P7 ve DZB-P12 bitmeden anlamı yok.
- DZB-27, DZB-37, DZB-39, DZB-59, DZB-61, DZB-65 — tek rakipte ya da dar kitlede ve hiçbir paketin dosyalarına bedavaya oturmuyor.
- DZB-57 — kardeş tablodaki DZA-39 ile aynı iş; iki kez paketlenmesin diye kesim tablosuna bırakıldı.
- DZB-34, DZB-73, DZB-74, DZB-75, DZB-77 — üçüncü taraf ikili dosya, bulut hizmeti ya da hesap ister; öncelik 3 ve sahip kararı olmadan başlamaz.

Kanıt: [doğrulanmış tablo](piyasa/2026-10-13/duzenleyici-kurgu-dogrulanmis.md) · [ham tablo](piyasa/2026-10-13/duzenleyici-kurgu-eksikler.md) · [notlar ve kaynaklar](piyasa/2026-10-13/duzenleyici-kurgu.md)

### Küçült

#### Küçültme, Önizleme Ve Kuyruk (SIK)

46 eksik yazıldı, doğrulamada 0 satır çıktı, 46 satır kaldı. Öncelik 1, 2, 3: 5, 13, 28.
18 satır sahibin kararını bekliyor, 3 satır kapalı bir karara denk geliyor. 13 paket önerildi.

- Eksikler çekirdek işte (hedef boyuta küçültme, kodek seçimi, HDR10 taşıma, bölmeli karşılaştırma) değil; uzun işin konforunda ve kuyruk yönetiminde toplanıyor.
- 46 satırın 46'sı kaldı. Öncelik 1 beş satır (SIK-01, SIK-02, SIK-03, SIK-04, SIK-06); beşi de en az iki bağımsız kaynakta görüldü.
- En ağır üç eksik: koşan kodlamanın duraklatılamaması (SIK-01, beş rakipte var), dış ses dosyasının eklenememesi (SIK-03, HandBrake'te 62 oyla en çok istenen), kodlama sürerken uykunun engellenmemesi (SIK-06, gece bırakılan kuyruk yarıda kalır).
- En ucuz üç kazanç: kodlama süreç önceliği (SIK-02, tek atama ve bir ayar), bitince uygulamadan çık (SIK-09, var olan eylem listesine bir değer), kaynak dosyanın tarihini koruma (SIK-11, taşımadan sonra iki dosya çağrısı).
- On üç paketin yedisi karar beklemiyor; altısı sahibin kararına bağlı. Bunların içinde yalnız SIK-P2 hem küçük hem öncelik 1, ilk sorulacak karar odur.
- Dönüştür sekmesiyle altı satır ortak; dış ses ve altyazı biçimi motoru bir kez yazılıp iki sekmeye hizmet eder.
- Dolby Vision ve VVC tarafındaki satırlar pazarda var ama hepsi derleme ya da kapsam kararına bağlı; bu tur onları sıranın sonuna koydu.

| Paket | Ad | Satırlar | İş | Öncelik | Karar | Dalga |
|---|---|---|---|---|---|---|
| SIK-P1 | Kodlama Süreç Önceliği Ayarı | SIK-02, SIK-20 | küçük | 1 | gerekmez | 1 |
| SIK-P2 | Kodlama Sürerken Uykuyu Engelleme | SIK-06 | küçük | 1 | sahip | K |
| SIK-P3 | Koşan Kodlamayı Duraklatma | SIK-01, SIK-41 | orta | 1 | gerekmez | 2 |
| SIK-P4 | Dış Ses İzi Ekleme | SIK-03 | orta | 1 | gerekmez | 2 |
| SIK-P5 | Yakılan Altyazıda Biçim Seçimi | SIK-04 | orta | 1 | gerekmez | 2 |
| SIK-P6 | Çıktı Tarihi, Adı Ve Doğrulaması | SIK-11, SIK-13, SIK-18, SIK-24 | orta | 2 | gerekmez | 3 |
| SIK-P7 | Kuyrukta Zamanlama, Düzenleme Ve Bitiş | SIK-07, SIK-08, SIK-09, SIK-10 | orta | 2 | gerekmez | 3 |
| SIK-P8 | Önizlemede Elle Kırpma | SIK-12 | orta | 2 | gerekmez | 3 |
| SIK-P9 | Kuyruğun Saklanması Ve Geri Yüklenmesi | SIK-05 | orta | 2 | sahip | K |
| SIK-P10 | Biten Çıktının Kalite Ölçümü | SIK-14 | orta | 2 | sahip | K |
| SIK-P11 | Ölçümle Hedef Kalite Araması | SIK-15 | büyük | 2 | sahip | K |
| SIK-P12 | VVC Kodlama Desteği | SIK-16 | orta | 2 | sahip | K |
| SIK-P13 | Donanım Kodlayıcıda Dolby Vision | SIK-19 | büyük | 2 | sahip | K |

- **SIK-P1 — Kodlama Süreç Önceliği Ayarı.** Ayarlar'a kodlama önceliği seçimi eklenir (Normal, Normalin Altı, Düşük; varsayılan bugünkü gibi Normal) ve kodlama süreci başlar başlamaz `Process.PriorityClass` ile atanır; aynı atama yardımcı süreçlerde zaten yapılıyor (`src/VidShrink.Core/Editing/ThumbnailQueue.cs:276`). Yanına çekirdek sınırı seçimi konur ve kodlama komutuna `-threads` olarak yazılır. Bu seçenek her kodlayıcıda aynı etkiyi vermeyebilir; işçi önce her kodlayıcıda ölçer, sınırlamayan kodlayıcıda seçimi gizler. Doğrulama: ayar varsayılandayken komutun değişmediğini pimleyen argüman testi ve başlatılan sürecin önceliğini okuyan test.
  - Dokunacağı yer: src/VidShrink.Ffmpeg/EncodeRunner.cs, src/VidShrink.Ffmpeg/FfmpegRunner.cs, src/VidShrink.Core/FfmpegArguments.cs, src/VidShrink.App/MainWindow.axaml (Ayarlar, `CmbParallelJobs` yanı), src/VidShrink.App/AppSettings.cs, src/VidShrink.Cli/CliRequest.cs, src/VidShrink.App/Locales/
- **SIK-P2 — Kodlama Sürerken Uykuyu Engelleme.** İlk iş başlarken sistemden uyumama isteği alınır, son iş bitince ya da iptalde geri verilir: Windows'ta `SetThreadExecutionState` çağrısı, macOS'ta `caffeinate` süreci, Linux'ta `systemd-inhibit`. Ekranın kararmasına dokunulmaz, yalnız uyku engellenir; "bitince uyut" eylemi seçiliyse istek önce bırakılır, sonra makine uyutulur. Doğrulama: isteğin alınıp bırakıldığını sayan sahte sağlayıcılı test ve Windows'ta `powercfg /requests` çıktısına elle bakış.
  - Dokunacağı yer: src/VidShrink.App/ (yeni uyku engelleme yardımcısı), src/VidShrink.App/ShrinkJobWindow.axaml.cs, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/QueueEndActions.cs, src/VidShrink.App/AppSettings.cs, src/VidShrink.App/Locales/
  - Sahibin kararı: Kodlama sürerken bilgisayarın uykuya geçmesi varsayılan olarak mı engellensin, yoksa kapalı gelen bir ayar mı olsun?
- **SIK-P3 — Koşan Kodlamayı Duraklatma.** Koşan ffmpeg süreci askıya alınıp sürdürülür: Windows'ta `NtSuspendProcess` ve `NtResumeProcess`, macOS ve Linux'ta `SIGSTOP` ve `SIGCONT` sinyalleri. Kuyruk penceresindeki duraklat düğmesi koşan işi de durdurur, ana sekmeye de aynı düğme gelir; askıdayken kalan süre ve deneme süresi sayaçları (`src/VidShrink.Ffmpeg/EncodeRunner.cs:436`) durur, iptal askıdaki süreci de sonlandırır. Aynı duraklatma kuyruk koşarken boş disk alanı eşiğin altına inince kendiliğinden tetiklenir; kuyruk penceresi işe başlamadan önce de alanı denetler. Doğrulama: askıdayken kısmi dosyanın büyümediğini ve sürdürünce işin bittiğini ölçen test, askıda iptal testi, sahte disk okuyucusuyla otomatik duraklatma testi.
  - Dokunacağı yer: src/VidShrink.Ffmpeg/EncodeRunner.cs, src/VidShrink.Ffmpeg/ChildJob.cs, src/VidShrink.Core/JobPump.cs, src/VidShrink.App/ShrinkJobWindow.axaml, src/VidShrink.App/ShrinkJobWindow.axaml.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.Ffmpeg/DiskSpaceGuard.cs, src/VidShrink.App/Locales/
- **SIK-P4 — Dış Ses İzi Ekleme.** Ses bölümüne, dış altyazı eklemenin eşi olan bir ses dosyası ekle düğmesi gelir; dosya ffmpeg'e ikinci giriş olarak verilir ve `-map` ile ya var olan sesin yanına eklenir ya da onun yerine konur. Hedef boyut hesabı eklenen sesin bit hızını bütçeye katar; ses videodan uzunsa video süresinde kesilir. Doğrulama: çıktıdaki iz sayısını ve sırasını ffprobe ile okuyan test ve hedef boyutun bantta kaldığını ölçen test. Motor Dönüştür sekmesindeki DON-07 ile ortaktır; iki iş aynı işçiye verilirse motor bir kez yazılır.
  - Dokunacağı yer: src/VidShrink.App/MainWindow.Izler.cs, src/VidShrink.App/MainWindow.axaml (ses bölümü), src/VidShrink.Core/StreamMapping.cs, src/VidShrink.Core/FfmpegArguments.cs, src/VidShrink.Core/PlanCalculator.cs, src/VidShrink.Cli/CliRequest.cs, src/VidShrink.App/Locales/
- **SIK-P5 — Yakılan Altyazıda Biçim Seçimi.** Altyazı yakılırken yazı tipi, boyut ve arka plan kutusu seçilir; seçim ffmpeg'in `subtitles` süzgecine `force_style` ile verilir (altyazıyı çizen libass kitaplığının, dosyadaki biçimi geçersiz kılan seçeneği). Seçim boşken süzgeç metni bugünküyle aynı kalır; resim tabanlı altyazıda (PGS, VobSub) biçim uygulanamadığı için seçim gizlenir. Doğrulama: süzgeç metnini pimleyen test ve iki ayrı boyutla yakılan karelerin farklı çıktığını ölçen görüntü testi. Dönüştür sekmesindeki DON-09 aynı motoru kullanır.
  - Dokunacağı yer: src/VidShrink.Core/VideoFilterChain.cs, src/VidShrink.App/MainWindow.Izler.cs, src/VidShrink.App/MainWindow.axaml (altyazı bölümü, `CmbBurnSubtitle` yanı), src/VidShrink.Cli/CliRequest.cs, src/VidShrink.App/Locales/
- **SIK-P6 — Çıktı Tarihi, Adı Ve Doğrulaması.** Kısmi dosya asıl ada taşınmadan önce (`src/VidShrink.Ffmpeg/EncodeRunner.cs:463`) çıktı ffprobe ile okunur; süre kaynaktan belirgin kısaysa ya da dosya çözülemiyorsa iş hatalı sayılır ve hedef dosya ezilmez. Taşımadan sonra, varsayılan kapalı gelen bir ayar açıksa kaynağın oluşturma ve değiştirme tarihi çıktıya yazılır. Adlandırma desenine saat, bit hızı, kare hızı, kaynak klasörü ve sayaç yer tutucuları eklenir (`src/VidShrink.Core/AdlandirmaDeseni.cs:21`); hata veren iş satırına tek tıkla yeniden sıraya alan düğme konur. Doğrulama: kasıtlı kısaltılmış çıktının hata verdiğini, tarihin taşındığını ve her yeni yer tutucunun açıldığını ölçen testler.
  - Dokunacağı yer: src/VidShrink.Ffmpeg/EncodeRunner.cs, src/VidShrink.Core/AdlandirmaDeseni.cs, src/VidShrink.App/ShrinkJobWindow.axaml.cs, src/VidShrink.App/MainWindow.axaml (Ayarlar), src/VidShrink.App/AppSettings.cs, src/VidShrink.App/Locales/
- **SIK-P7 — Kuyrukta Zamanlama, Düzenleme Ve Bitiş.** Kuyruk penceresine "şu saatte başlat" ve "şu kadar sonra başlat" seçimi eklenir; pompa o ana dek duraklatılmış bekler ve var olan geri sayım satırı kalan süreyi gösterir. Bekleyen iş satırına düzenle düğmesi gelir: iş kuyruktan çıkmadan hedefi ve ayarı değiştirilir. Bitince eylemlerine uygulamadan çık eklenir ve Ayarlar'a varsayılan kapalı bir bitiş sesi kutusu konur; işçi önce bugünkü Windows balonunun ses çalıp çalmadığını ölçer. Doğrulama: sahte saatle zamanlama testi, düzenlenen işin yeni ayarla koştuğunu ölçen pompa testi, yeni eylemin seçilip saklandığını ölçen test.
  - Dokunacağı yer: src/VidShrink.App/ShrinkJobWindow.axaml, src/VidShrink.App/ShrinkJobWindow.axaml.cs, src/VidShrink.Core/JobPump.cs, src/VidShrink.App/QueueEndActions.cs, src/VidShrink.App/IsBittiBildirimi.cs, src/VidShrink.App/AppSettings.cs, src/VidShrink.App/Locales/
- **SIK-P8 — Önizlemede Elle Kırpma.** Önizleme karesinin üstüne tutamaçlı bir dikdörtgen çizilir; kullanıcı fareyle sürükler, sonuç kaynak pikseline çevrilip var olan `crop` süzgeç yoluna yazılır (`src/VidShrink.Core/VideoFilterChain.cs:330`). Elle kırpma seçiliyken otomatik siyah bant kırpma kapanır; genişlik ve yükseklik kodlayıcının istediği çift sayıya yuvarlanır. Doğrulama: ekran koordinatından kaynak pikseline çeviriyi ölçen test ve çıktı çözünürlüğünü ffprobe ile okuyan test.
  - Dokunacağı yer: src/VidShrink.App/MainWindow.axaml (önizleme alanı ve `ChkFltAutoCrop` çevresi), src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.Core/VideoFilterChain.cs, src/VidShrink.App/Locales/
- **SIK-P9 — Kuyruğun Saklanması Ve Geri Yüklenmesi.** Bekleyen ve koşan işler her değişiklikte kullanıcı veri klasöründeki bir JSON dosyasına yazılır; önce geçici dosyaya yazılıp sonra yer değiştirilir ki yarım yazılmış dosya kuyruğu bozmasın. Açılışta dosya okunur, kaynağı artık olmayan işler elenir, koşarken kesilen iş baştan sıraya girer. Doğrulama: yaz, süreci kes, yeniden oku testi ve bozuk dosyada uygulamanın boş kuyrukla açıldığını ölçen test.
  - Dokunacağı yer: src/VidShrink.Core/JobPump.cs, src/VidShrink.Core/ (yeni kuyruk deposu), src/VidShrink.App/ShrinkJobWindow.axaml.cs, src/VidShrink.App/Locales/
  - Sahibin kararı: Açılışta yarım kalan kuyruk kendiliğinden mi geri gelsin, yoksa kullanıcıya mı sorulsun?
- **SIK-P10 — Biten Çıktının Kalite Ölçümü.** Komut satırının `--vmaf` ile yaptığı ölçüm (`src/VidShrink.Cli/CliApp.cs:533`) arayüze taşınır: iş bitince çıktının tamamı kaynakla karşılaştırılır, VMAF puanı (görüntünün kaynağa benzerliğini 0 ile 100 arasında puanlayan ölçüt) sonuç satırında gösterilir ve iş kaydına yazılır. Ölçüm ilerleme çubuğunda ayrı bir aşama olarak görünür ve iptal edilebilir. Doğrulama: arayüzün gösterdiği puanın aynı dosyada komut satırının verdiği puanla aynı olduğunu ölçen test.
  - Dokunacağı yer: src/VidShrink.Ffmpeg/QualityMeter.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/ShrinkJobWindow.axaml.cs, src/VidShrink.App/Locales/
  - Sahibin kararı: Tam kalite ölçümü her işi uzatır; varsayılan kapalı bir seçenek olarak arayüze girsin mi?
- **SIK-P11 — Ölçümle Hedef Kalite Araması.** Hedef kalite bugün örnek ölçümünden çıkan tahminle çözülüyor (`src/VidShrink.Core/PlanCalculator.cs:1170`); bu pakette kısa örnekler aday ayarla kodlanıp ölçülür ve hedef puan tutana dek ayar aralığı ikiye bölünerek aranır. Tur sayısı sınırlanır, her tur ilerlemede görünür, arama kapalıyken bugünkü yol değişmez. Doğrulama: ölçüm aracıyla sabit bir dosya kümesinde tahmin yolu ile arama yolunun hedef puandan sapması ve harcadığı süre yan yana ölçülür.
  - Dokunacağı yer: src/VidShrink.Core/PlanCalculator.cs, src/VidShrink.Ffmpeg/QualityMeter.cs, src/VidShrink.Ffmpeg/EncodeRunner.cs, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.Cli/CliApp.cs, tools/VidShrink.Bench/
  - Sahibin kararı: Arama her turda ek kodlama ve ölçüm süresi getirir; hedef kalite kipine isteğe bağlı bir seçenek olarak eklensin mi?
- **SIK-P12 — VVC Kodlama Desteği.** Önce dağıtılan ffmpeg'de libvvenc (VVC kodlayan açık kaynak kitaplık) var mı diye yoklanır; yoksa iş derleme kararında bekler. Varsa kodek izin listesine eklenir (`src/VidShrink.Core/PlanParser.cs:13`), hız ön ayarları ve boyut verimi ölçülerek yazılır, kodek seçiminde çoğu aygıtta oynamadığı uyarısıyla gösterilir. Doğrulama: argüman testi ve ölçüm aracıyla hedef boyut bandının tuttuğunun ölçülmesi. Dönüştür sekmesindeki DON-33 ile aynı iştir.
  - Dokunacağı yer: src/VidShrink.Core/PlanParser.cs, src/VidShrink.Core/FfmpegArguments.cs, src/VidShrink.Core/PlanCalculator.cs, src/VidShrink.App/MainWindow.axaml (kodek seçimi), src/VidShrink.App/Locales/
  - Sahibin kararı: libvvenc içeren bir ffmpeg derlemesi dağıtılsın mı ve oynatma desteği bu kadar darken VVC kodek listesine girsin mi?
- **SIK-P13 — Donanım Kodlayıcıda Dolby Vision.** Önce ölçülür: ekran kartı kodlayıcılarıyla yapılan kodlamada ffmpeg Dolby Vision üst verisini çıktıya yazıyor mu; sonuç `docs/olcumler/` altına girer. Yazıyorsa `src/VidShrink.Core/HdrResolver.cs:46` satırındaki kodlayıcı koşulu genişletilir; yazmıyorsa iş ayrı araç zinciri kararına döner ve paket orada durur. Doğrulama: çıktının Dolby Vision kaydını ffprobe ile okuyan test.
  - Dokunacağı yer: src/VidShrink.Core/HdrResolver.cs, src/VidShrink.Core/FfmpegArguments.cs, tools/VidShrink.Bench/, docs/olcumler/
  - Sahibin kararı: Dolby Vision kapsamı donanım kodlayıcılarına genişlesin mi, gerekirse ffmpeg dışında ayrı bir araçla?

Pakete girmeyenler:

Hepsi öncelik 3 ya da kapalı. Hiçbiri bir paketin yanında neredeyse bedavaya gelmiyor.

- SIK-17: önceliği 3'e indi ve büyük iş; Dolby Vision kapsam kararı açılırsa SIK-P13 ile birlikte ele alınır (iPhone çekimleri yaygın olduğu için o gün ilk ölçülecek olan budur).
- SIK-21: sahibin güvenlik kararını bekliyor ve kitlesi dar; SIK-P7'nin karar beklemeyen satırlarını durdurmasın diye dışarıda.
- SIK-22: posta sunucusu ayarı ve parola saklama ister; yanına ekleneceği paket yok.
- SIK-23: rakip tarafı doğrulanmadı ve üç işletim sisteminde ayrı bildirim yolu orta iş.
- SIK-25: tek rakipte; SIK-P7 ile aynı pencereye dokunur ama o paket dört satırla dolu.
- SIK-26: geri alınamaz işlem, sahibin kararını bekliyor.
- SIK-27: tek rakipte; izleme klasörü koduna dokunan paket yok.
- SIK-28: tek rakipte ve orta iş; klasör yapısını çıktıda korumak ayrı tasarım ister.
- SIK-29: parçalı kodlama mimarisi gerekir, büyük iş.
- SIK-30: büyük iş ve SIK-29 ile aynı mimari karara bağlı.
- SIK-31: tek rakipte, orta iş; karşılaştırma paneline dokunan paket yok.
- SIK-32: tek rakipte, orta iş; SIK-31 ile birlikte ayrı bir karşılaştırma turunda ele alınır.
- SIK-33: büyük iş, Dolby Vision kapsam kararına bağlı.
- SIK-34: kapalı (Dolby Vision profil 5 kapsam dışı hükmü).
- SIK-35: ffmpeg yolu taşımıyor, ayrı araç zinciri kararı gerekir.
- SIK-36: tek rakipte; gren sentezi boyut hesabını değiştirir ve ölçüm ister.
- SIK-37: ffmpeg derlemesine ek kitaplık kararı bekliyor.
- SIK-38: ffmpeg derlemesine yeni ölçüt kitaplığı kararı bekliyor; SIK-P10 bitmeden yeri yok.
- SIK-39: yeni ikili dosya dağıtımı kararı bekliyor.
- SIK-40: ürün yönü kararı; sunucu kipi yeni bir yüzey.
- SIK-42: üç işletim sisteminde ayrı güç durumu okuması ister; SIK-P3 bittikten sonra küçük bir ek olur.
- SIK-43: tek rakipte; ara kare üretimi kodlamayı yavaşlatır, yanına ekleneceği paket yok.
- SIK-44: tek rakipte; yonga listesi başka hiçbir paketin dosyasına dokunmuyor, küçük bir iş olarak ayrıca alınabilir.
- SIK-45: kapalı (K5).
- SIK-46: kapalı (K8).

Kanıt: [doğrulanmış tablo](piyasa/2026-10-13/kucult-dogrulanmis.md) · [ham tablo](piyasa/2026-10-13/kucult-eksikler.md) · [notlar ve kaynaklar](piyasa/2026-10-13/kucult.md)

### Dönüştür

#### Dönüştürme Ve Gelişmiş (DON)

52 eksik yazıldı, doğrulamada 0 satır çıktı, 52 satır kaldı. Öncelik 1, 2, 3: 9, 23, 20.
5 satır sahibin kararını bekliyor, 6 satır kapalı bir karara denk geliyor. 15 paket önerildi.

- Dönüştür bugün tek dosyalık ince bir yol: 13 kap, yazılım kodlayıcıları, kesit, HDR ve meta veri silme. HandBrake ve Shutter Encoder'ın yanında iz, altyazı, toplu iş ve ön ayar tarafı boş.
- İyi haber: açığın büyük kısmı yazılmamış kod değil, bağlanmamış kod. 14 satırın motoru Küçült yolunda hazır; donanım kodlayıcıları ise Dönüştür'ün kendi motorunda bile hazır.
- En ağır eksik 1: iz ve altyazı. Dönüştür `-map` yazmadığı için ikinci ses ve altyazı sessizce kayboluyor (`DON-01`, `DON-02`, `DON-03`).
- En ağır eksik 2: toplu dönüştürme (`DON-06`). Rakiplerin hepsinde var, bizde çoklu dosya Küçült'e gidiyor.
- En ağır eksik 3: adlı ön ayar (`DON-05`). Dönüştür her seferinde baştan kuruluyor.
- En ucuz üç kazanç: donanım kodlayıcılarını listeye eklemek (`DON-04`), MP4 ses kopya tablosunu Küçült'le eşitlemek (`DON-20`), ses yüksekliği ile kanal ve örnekleme hızını bağlamak (`DON-16`, `DON-39`, `DON-40`).
- Öncelik 1'e gelen sekiz satırın sekizi de iki ayrı kaynakta görüldü; `DON-09` dokuzuncu olarak eklendi. Öncelik 3'teki on dört açık satırın altısı bir paketin yanında bedavaya geliyor.

| Paket | Ad | Satırlar | İş | Öncelik | Karar | Dalga |
|---|---|---|---|---|---|---|
| DON-P1 | Dönüştür'de Donanım Kodlayıcıları | DON-04 | küçük | 1 | gerekmez | 1 |
| DON-P2 | Dönüştür'de Altyazı İzleri | DON-02, DON-10, DON-11 | orta | 1 | gerekmez | 2 |
| DON-P3 | Dönüştür'de Altyazı Gömme | DON-03, DON-09 | orta | 1 | gerekmez | 2 |
| DON-P4 | Dönüştür'de Ses İzi Seçimi | DON-01 | orta | 1 | sahip | K |
| DON-P5 | Adlı Dönüştür Ön Ayarı | DON-05, DON-12 | orta | 1 | gerekmez | 2 |
| DON-P6 | Toplu Dönüştürme Ve Dosya Başına Ayar | DON-06, DON-13 | orta | 1 | gerekmez | 2 |
| DON-P7 | Harici Ses Dosyası Ekleme | DON-07 | orta | 1 | gerekmez | 2 |
| DON-P8 | Görüntü Dizisi Girdisi Ve Çıktısı | DON-08, DON-23, DON-44 | orta | 1 | gerekmez | 2 |
| DON-P9 | Dönüştür'de Ses Seçenekleri | DON-16, DON-17, DON-18, DON-19, DON-20, DON-39, DON-40 | orta | 2 | gerekmez | 3 |
| DON-P10 | Dönüştür'de Görüntü Süzgeçleri | DON-14, DON-41, DON-43 | orta | 2 | gerekmez | 3 |
| DON-P11 | Kodlayıcı, HDR Ve Renk Ayarları | DON-15, DON-25, DON-26 | orta | 2 | gerekmez | 3 |
| DON-P12 | Arşiv Kodeği, APNG Ve GIF Ayarı | DON-21, DON-22, DON-24, DON-34 | orta | 2 | gerekmez | 3 |
| DON-P13 | Özel Argüman Ve Komut Satırı | DON-27, DON-28 | orta | 2 | gerekmez | 3 |
| DON-P14 | Meta Veri, Bölüm Ve İz Etiketleri | DON-29, DON-30, DON-31 | büyük | 2 | gerekmez | 4 |
| DON-P15 | Yapay Zekâ İle Büyütme | DON-32 | büyük | 2 | sahip | K |

- **DON-P1 — Dönüştür'de Donanım Kodlayıcıları.** Kodek listesine NVENC, QSV ve AMF kodlayıcıları (Nvidia, Intel ve AMD ekran kartlarının kodlayıcıları; H.264, HEVC, AV1) eklenir. Motor bu adları zaten sürüyor (`ConversionArguments.cs:78`, `:134`, `:144`), yeni argüman yazılmaz; yoklama makinede olmayan kodlayıcıyı gizler. Kayıtlı seçim sıra numarası yerine adla saklanır, yoksa yeni öğe eski seçimi kaydırır. Doğrulama: liste ile kap uyum tablosunu eşleyen test ve ekran kartlı bir makinede elle tek dönüşüm.
  - Dokunacağı yer: src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.DonusturKap.cs, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/
- **DON-P2 — Dönüştür'de Altyazı İzleri.** Dönüştür planına altyazı alanı eklenir ve komut, Küçült'ün akış planından (`StreamMapping.cs:255`) açık `-map` ve `-c:s` alır; kabın anlamadığı altyazı oradaki kuralla çevrilir ya da düşer. Dış .srt ve .ass dosyası aynı planın ek girdisi olarak bağlanır (`StreamMapping.cs:798`). Çıkarma, seçilen izi tek başına altyazı dosyasına yazan ayrı ve kısa bir komuttur. Doğrulama: kap başına argüman testleri (en az MP4, MKV ve WebM) ve açık `-map` yazılınca ses seçiminin değişmediğini gösteren test.
  - Dokunacağı yer: src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.Core/StreamMapping.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/MainWindow.Izler.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/
- **DON-P3 — Dönüştür'de Altyazı Gömme.** Dönüştür'ün kendi süzgeç listesi (`ConversionArguments.cs:229`) yerine Küçült'ün zinciri (`VideoFilterChain.cs:321`) çağrılır; yakma süzgeci (`VideoFilterChain.cs:233`) ve resim altyazı bindirmesi böylece hazır gelir. Arayüzde Küçült'teki yakma seçimi (`MainWindow.Izler.cs:48`) Dönüştür'e de konur. Biçem için süzgece `force_style` (yazı tipi, boyut, renk, kutu) yazılır; bu yalnız metin altyazıda çalışır. Doğrulama: süzgeç metni testleri ve libass (altyazıyı çizen kitaplık) yoksa seçeneğin gizlendiğini gösteren test.
  - Dokunacağı yer: src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.Core/VideoFilterChain.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.Izler.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/
- **DON-P4 — Dönüştür'de Ses İzi Seçimi.** DON-P2'nin bağladığı akış planına dayanır. Akış isteğine seçilen ses izlerinin listesi eklenir; bugün yalnız "tüm izleri koru" ve dil tercihi var (`StreamMapping.cs:140`). Arayüzde kaynağın ses izleri işaret kutulu liste olarak gösterilir, kopya kipinde kabın kabul etmediği ses için uyarı verilir. Doğrulama: iki sesli kaynakla argüman testleri; varsayılan, sahibin kararına göre testle sabitlenir.
  - Dokunacağı yer: src/VidShrink.Core/StreamMapping.cs, src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/
  - Sahibin kararı: Dönüştür varsayılan olarak tek ses mi çıkarsın, yoksa kabın taşıyabildiği bütün sesleri mi taşısın.
- **DON-P5 — Adlı Dönüştür Ön Ayarı.** Dönüştür panelindeki seçimler adla kaydedilir ve listeden geri yüklenir. Depolama Küçült ön ayarının kitaplığını örnek alır (`MainWindow.OnAyar.cs:228`) ama ayrı bir tür olarak durur, iki sekmenin ayarı karışmaz. Dışa ve içe aktarma, Küçült'teki düğmelerin eşi olarak eklenir (`MainWindow.OnAyar.cs:385`, `:301`). Doğrulama: kaydet, yükle, dışa aktar, içe al gidiş dönüş testi.
  - Dokunacağı yer: src/VidShrink.Core/PresetLibrary.cs, src/VidShrink.App/MainWindow.OnAyar.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/
- **DON-P6 — Toplu Dönüştürme Ve Dosya Başına Ayar.** Dönüştür'e bırakılan ya da seçilen birden çok dosya bir sıraya girer. Sıradaki her öğe kendi dönüşüm planını taşır; böylece "hepsine aynı ayar" ile "bu dosyaya ayrı ayar" aynı yapıdan çıkar. Küçült kuyruğunun penceresi örnek alınır (`MainWindow.axaml.cs:3324`); bir dosyanın hatası sırayı durdurmaz, sonda özet verilir. Doğrulama: sıra mantığı için sahte koşucuyla testler ve üç dosyalık elle deneme.
  - Dokunacağı yer: src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/ (yeni sıra penceresi), src/VidShrink.Ffmpeg/EncodeRunner.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/
- **DON-P7 — Harici Ses Dosyası Ekleme.** Dönüştür'de ikinci girdi olarak bir ses dosyası seçilir; iki kip sunulur: sesi onunla değiştir ya da ek iz olarak ekle. Komut ikinci `-i` ve açık `-map` yazar; görüntü kopyalanabiliyorsa yeniden kodlanmaz. Süreler tutmuyorsa kısa olanda bitirme seçeneği konur. Doğrulama: iki kip için argüman testleri ve kabın o sesi kabul etmediği durumda yeniden kodlamaya düşüş testi.
  - Dokunacağı yer: src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/
- **DON-P8 — Görüntü Dizisi Girdisi Ve Çıktısı.** Girdi yönünde numaralı resim klasörü seçilir, kare hızı sorulur ve ffmpeg'in resim dizisi okuyucusuyla (`image2`) tek videoya çevrilir. Çıktı yönünde kap listesine PNG dizisi ve JPEG dizisi eklenir, kareler bir klasöre numaralı yazılır; tek kare kaydetme aynı yolun tek karelik halidir. Doğrulama: desen ve kare hızı argüman testleri, numarasında boşluk olan dizide davranış testi.
  - Dokunacağı yer: src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/MainWindow.DonusturKap.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/
- **DON-P9 — Dönüştür'de Ses Seçenekleri.** Önce tablolar: ses listesine AC3 ve E-AC3, ses kaplarına Opus, OGG, AIFF ve AC3 eklenir; MP4 kopya tablosu Küçült'ünkiyle eşitlenir (`StreamMapping.cs:430`); ses kabı seçilince kaynak ses o kaba sığıyorsa kopyalanır. Sonra işleme: ses yüksekliği (`loudnorm`), kanal (`-ac`) ve örnekleme hızı (`-ar`) plana alan olarak girer; karşılıkları Küçült yolunda yazılı (`StreamMapping.cs:316`, `:317`). Bu üçünden biri seçilince ses kopyası kapanır. Doğrulama: kap ve kodek uyum tablosu testleri, her yeni kodlayıcının iki ffmpeg derlemesinde yoklanması.
  - Dokunacağı yer: src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/MainWindow.DonusturKap.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/
- **DON-P10 — Dönüştür'de Görüntü Süzgeçleri.** DON-P3'ün bağladığı zincire dayanır. Dönüştür paneline kırpma, döndürme, tarama çözme ve gürültü azaltma seçimleri konur; değerler Küçült'teki süzgeç seçeneklerine yazılır (`VideoFilterChain.cs:63`). Sabit ya da değişken kare hızı seçimi (`VideoFilterChain.cs:379`) ve siyah bantla dolgu (`VideoFilterChain.cs:347`) aynı zincirden gelir; ölçekleme algoritması zincire yeni bir alan olarak eklenir. Doğrulama: zincir sırası testleri ve kopya kipinde süzgeç seçilince yeniden kodlamaya geçiş testi.
  - Dokunacağı yer: src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.Core/VideoFilterChain.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/
- **DON-P11 — Kodlayıcı, HDR Ve Renk Ayarları.** Hız ön ayarı, tune, profil, seviye ve bit hızı kipinde iki geçiş Dönüştür'de seçilebilir olur; bugün sabit varsayılan yazılıyor (`ConversionArguments.cs:142`). HDR seçimi Küçült'teki düğmeden okunmak yerine (`MainWindow.axaml.cs:4777`) Dönüştür'ün kendi panelinde gösterilir. Renk için BT.601, BT.709 ve BT.2020 ile sınırlı ve tam aralık seçimi eklenir; BT.709 ve BT.601 kolu hazır (`VideoFilterChain.cs:397`), BT.2020 ve aralık yeni yazılır. Doğrulama: argüman testleri ve renk kolu için ölçüm aracıyla önce ve sonra kare karşılaştırması.
  - Dokunacağı yer: src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.Core/HdrResolver.cs, src/VidShrink.Core/VideoFilterChain.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/
- **DON-P12 — Arşiv Kodeği, APNG Ve GIF Ayarı.** Kodek listesine FFV1 ve CineForm, hareketli görsel kaplarına APNG eklenir; her biri kap uyum tablosuna ve kullanılabilirlik yoklamasına girer. GIF için döngü sayısı, renk sayısı ve dither (az renkle geçişleri noktalayarak yumuşatma) seçimi plana alan olarak eklenir; bugün palet ve dither sabit (`ConversionArguments.cs:105`). Kayıplı GIF sıkıştırması ffmpeg'in GIF kodlayıcısında yoksa bu parça dışarıda kalır; ölçülmedi. Doğrulama: argüman testleri ve yeni kodlayıcıların iki ffmpeg derlemesinde yoklanması.
  - Dokunacağı yer: src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.Core/CodecModel.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.DonusturKap.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/
- **DON-P13 — Özel Argüman Ve Komut Satırı.** Dönüştür paneline "ek argüman" kutusu konur; yazılan seçenekler komutta çıktı yolundan önce yer alır ve salt okunur komut önizlemesinde görünür (`MainWindow.axaml:1371`). Komut satırına ayrı bir dönüştürme komutu eklenir: kap, kodek, kalite ve kesit bayrakları Dönüştür planına çevrilir; GIF ve ara kodek (ProRes, DNxHR) çıktısı buradan da alınır. Doğrulama: bayrak ayrıştırma testleri ve arayüzle komut satırının aynı plandan aynı argümanı ürettiğini gösteren test.
  - Dokunacağı yer: src/VidShrink.Cli/CliRequest.cs, src/VidShrink.Cli/, src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/
- **DON-P14 — Meta Veri, Bölüm Ve İz Etiketleri.** Dönüştür'e tek bir "Etiketler" bölümü eklenir; üç parça aynı bölüme ve aynı plan alanlarına yazıldığı için tek pakette durur. Sırayla: başlık, sanatçı ve tarih alanları (`-metadata`); bölümleri taşı ya da at seçimi ve bölüm adlarını düzenleme (`StreamMapping.cs:302`); izlerin dili, adı, varsayılan ve zorunlu bayrağı (`StreamMapping.cs:282`). DON-P2'nin bağladığı akış planına dayanır. Doğrulama: argüman testleri ve çıktının ffprobe ile okunan etiketlerini karşılaştıran test.
  - Dokunacağı yer: src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.Core/StreamMapping.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/
  - Aynı iş: ARC-P5. Rafta o başlığın altında duruyor.
- **DON-P15 — Yapay Zekâ İle Büyütme.** Karar çıkmadan iş açılmaz. Çıkarsa önce ölçüm: aday modelin bu makinedeki hızı ve çıktı kalitesi ölçüm aracıyla ölçülür, lisansı ve indirme boyu yazılır. Sonra model, kurucunun pinli ve sha256'lı indirme yoluna isteğe bağlı bileşen olarak eklenir (`SetupModel.cs:81`) ve Dönüştür'de 2 kat ile 4 kat seçenekleri açılır.
  - Dokunacağı yer: src/VidShrink.Core/Setup/SetupModel.cs, src/VidShrink.Core/, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/Locales/, tools/VidShrink.Bench/
  - Sahibin kararı: Yüzlerce MB'lık model ve yeni bir ikili dosya kullanıcıya indirtilsin mi.
  - Aynı iş: ARC-P11. Rafta o başlığın altında duruyor.

Pakete girmeyenler:

- DON-33, DON-35: karar `sahip` ve paketteki ffmpeg'de kodlayıcının olup olmadığı bilinmiyor; öncelik 3.
- DON-36, DON-37: dar kitle; her biri yeni kalite eşlemesi ve kap kuralı ister, bir paketin yanında bedavaya gelmiyor.
- DON-38: altyazı dosyasını girdi olarak almak gerekir; Dönüştür bugün yalnız video ve ses dosyası açıyor.
- DON-42: tek rakipte var ve `orta` iş; hiçbir paketin yanında ucuza gelmiyor.
- DON-45: sağ tık menüsü Ayarlar ve kabuk kolunun alanı; tek rakipte var.
- DON-46: karar `sahip`, iş `büyük`, model indirmesi ister; öncelik 3.
- DON-47: kararı belirsiz, Doğrulanamayanlar'da; sahip açarsa `DON-P5`'in yanına girer.
- DON-48, DON-49, DON-50, DON-51, DON-52: kararı `kapalı`.

Kanıt: [doğrulanmış tablo](piyasa/2026-10-13/donustur-dogrulanmis.md) · [ham tablo](piyasa/2026-10-13/donustur-eksikler.md) · [notlar ve kaynaklar](piyasa/2026-10-13/donustur.md)

### Kaydedici

#### Yakalama (EKA)

29 eksik yazıldı, doğrulamada 0 satır çıktı, 29 satır kaldı. Öncelik 1, 2, 3: 4, 12, 13.
9 satır sahibin kararını bekliyor, 1 satır kapalı bir karara denk geliyor. 16 paket önerildi.

- Windows'ta çekirdek iş yerinde: ekran ve bölge kaydı, sistem sesi, ayrı ses izleri, anlık tekrar, çökmeye dayanıklı kap ve genel kısayollar var. Açık, çekirdeğin çevresinde ve Windows dışında toplanıyor.
- 29 satırın 29'u kaldı. Öncelik 1 dört satır, öncelik 2 on iki satır, öncelik 3 on üç satır.
- En ağır üç eksik: Wayland'da yakalama (EKA-02), macOS'ta sistem sesi ve pencere (EKA-03, EKA-04), uygulama başına ses (EKA-01). Dördü de sahibin kararını bekliyor; ilk ikisi yeni yardımcı ikili ister.
- En ucuz üç kazanç: düşük disk uyarısı (EKA-11), yarım kalan kaydı kurtarma (EKA-20), yalnız ses kaydı (EKA-15). Üçü de karar beklemiyor.
- On altı paketin dokuzu hemen açılabilir (P1, P3, P4, P5, P6, P7, P8, P9, P10); yedisi sahibin tek cümlelik kararına bağlı (P2, P11, P12, P13, P14, P15, P16).
- macOS ve Linux işleri bu Windows makinesinde koşulamıyor; o paketlerin ölçüsü CI koşucusuna ve gerçek aygıtta elle denemeye kalıyor.
- K3 için pazar değişti: WGC artık ffmpeg'in içinde ve dağıttığımız sürüm onu taşıyacak kadar yeni. Kararı yeniden açmak sahibin işi.

| Paket | Ad | Satırlar | İş | Öncelik | Karar | Dalga |
|---|---|---|---|---|---|---|
| EKA-P1 | Disk Uyarısı Ve Kayıt Kurtarma | EKA-11, EKA-20 | orta | 2 | gerekmez | 3 |
| EKA-P2 | Uygulama Başına Ses | EKA-01 | orta | 1 | sahip | K |
| EKA-P3 | Yalnız Ses Ve Ses Seçenekleri | EKA-15, EKA-22, EKA-28 | orta | 2 | gerekmez | 3 |
| EKA-P4 | Kodlayıcı Listesi Ve Kare Hızı | EKA-12, EKA-21, EKA-24 | orta | 2 | gerekmez | 3 |
| EKA-P5 | Komut Satırından Kayıt | EKA-16 | orta | 2 | gerekmez | 3 |
| EKA-P6 | Saatli Kayıt | EKA-10 | orta | 2 | gerekmez | 3 |
| EKA-P7 | Bas Konuş | EKA-09 | orta | 2 | gerekmez | 3 |
| EKA-P8 | Kamera Ana Kaynak Olarak | EKA-06 | orta | 2 | gerekmez | 3 |
| EKA-P9 | Kamera Katmanı Windows Dışında | EKA-07 | orta | 2 | gerekmez | 3 |
| EKA-P10 | Genel Kısayollar Windows Dışında | EKA-13 | orta | 2 | gerekmez | 3 |
| EKA-P11 | Model Tabanlı Gürültü Bastırma | EKA-14 | orta | 2 | sahip | K |
| EKA-P12 | Mac Sistem Sesi Yardımcısı | EKA-03 | büyük | 1 | sahip | K |
| EKA-P13 | Mac Pencere Yakalama | EKA-04 | büyük | 1 | sahip | K |
| EKA-P14 | Wayland Yakalama Yolu | EKA-02 | büyük | 1 | sahip | K |
| EKA-P15 | HDR Yakalama | EKA-08 | büyük | 2 | sahip | K |
| EKA-P16 | Oyun Yakalama | EKA-05 | büyük | 2 | sahip | K |

- **EKA-P1 — Disk Uyarısı Ve Kayıt Kurtarma.** Kayıt başlamadan önce ve kayıt sürerken belirli aralıkla `DiskSpaceGuard` çağrılır; boş alan eşiğin altına inince durum satırında uyarı çıkar. Kaydedici açılırken çıktı klasöründe sahipsiz `.yakalama.mkv` dosyası aranır ve var olan Matroska'dan aktarma yoluyla (`RecorderSession.cs:213`) istenen biçime çevirmek önerilir. İki davranış da sahte disk ve sahte klasörle ölçülür; `tests/VidShrink.Tests/DiskSpaceGuardTests.cs` örnek alınır.
  - Dokunacağı yer: src/VidShrink.Ffmpeg/DiskSpaceGuard.cs, src/VidShrink.Ffmpeg/RecorderSession.cs, src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.App/Recorder/RecorderView.Yakalama.cs, src/VidShrink.App/Locales/
- **EKA-P2 — Uygulama Başına Ses.** Bugünkü sistem sesi kaynağının yanına süreç geri döngüsü kaynağı eklenir: `ActivateAudioInterfaceAsync` çağrısı `AUDIOCLIENT_PROCESS_LOOPBACK_PARAMS` ile seçilen programın sesini verir, ham ses aynı adlandırılmış borudan ffmpeg'e gider. Ses panelinde ses çalan programların listesi çıkar; Windows sürümü yetmiyorsa seçenek sahibin kararına göre gizlenir ya da kapalı görünür. Ölçü `ISystemAudioCapture` (`SystemAudioCapture.cs:24`) için sahte kaynakla yazılır; gerçek deneme iki program aynı anda ses çalarken elle yapılır.
  - Dokunacağı yer: src/VidShrink.Ffmpeg/WasapiLoopbackCapture.cs, src/VidShrink.Ffmpeg/SystemAudioCapture.cs, src/VidShrink.Ffmpeg/LoopbackFeed.cs, src/VidShrink.App/Recorder/RecorderView.Ses.cs
  - Sahibin kararı: Uygulama başına ses yeni bir Windows ses arabirimi ister ve eski Windows 10 yapılarında çalışmaz; orada ve Windows dışında seçenek gizlensin mi, kapalı mı görünsün.
- **EKA-P3 — Yalnız Ses Ve Ses Seçenekleri.** Hedef listesine "yalnız ses" eklenir; görüntü girdisi kurulmaz ve ses girdilerinin sıra numarası varsayımı (`RecorderArguments.cs:366`) buna göre parametreye çevrilir. Ses kodeki sabiti (`RecorderArguments.cs:349`) seçilebilir olur; varsayılan AAC kalır, Opus yalnız onu taşıyan kaplarda sunulur. Ses süzgeç zincirine (`AudioCaptureArguments.cs:75`) ffmpeg'in `acompressor` (ses düzeyini dengeleyen) ve `alimiter` (patlamayı kesen) süzgeçleri iki yeni seçenek olarak girer. Üçü de argüman ölçüleriyle doğrulanır.
  - Dokunacağı yer: src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.Core/AudioCaptureArguments.cs, src/VidShrink.App/Recorder/RecorderView.Ses.cs, src/VidShrink.App/Recorder/RecorderView.Gelismis.cs
- **EKA-P4 — Kodlayıcı Listesi Ve Kare Hızı.** Kaydedicinin kodlayıcı listesine (`RecorderArguments.cs:368`) VideoToolbox, VAAPI, `av1_qsv` ve `av1_amf` eklenir; VideoToolbox ve VAAPI argümanları dönüştürme yolundan (`FfmpegArguments.cs`, K12) alınır. Aynı yere sabit ya da değişken kare hızı seçimi ffmpeg'in `-fps_mode` seçeneğiyle eklenir; varsayılan bugünkü davranış kalır. Argümanlar ölçüyle doğrulanır; macOS ve Linux kodlayıcıları bu makinede koşulamadığı için gerçek deneme o sistemlerde elle yapılır.
  - Dokunacağı yer: src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.Core/CodecModel.cs, src/VidShrink.App/Recorder/RecorderView.Gelismis.cs
- **EKA-P5 — Komut Satırından Kayıt.** Kayıtlı kaydedici ayarlarından arayüz açmadan kayıt isteği kuran tek bir giriş yazılır: başlat, süre, çıktı yolu. Giriş uygulamanın başlatma parametresine ya da komut satırı aracına bağlanır; hangisinin daha az kod taşıdığına işin başında bakılır. Kayıt süre sınırı dolunca (`RecorderArguments.cs:1217`) ya da durdurma işaretiyle biter. Ölçü: komut satırı argümanlarının kayıt isteğine çevrilmesi `tests/VidShrink.Tests` içinde sınanır.
  - Dokunacağı yer: src/VidShrink.Cli/CliApp.cs, src/VidShrink.App/Program.cs, src/VidShrink.App/Recorder/RecorderSettings.cs, src/VidShrink.Ffmpeg/RecorderSession.cs
- **EKA-P6 — Saatli Kayıt.** Kaydediciye başlangıç saati ve yineleme (bir kez, günlük, haftalık) seçimi eklenir; uygulama açıkken saat gelince kayıt bugünkü başlatma yolundan başlar, bitiş için var olan süre sınırı (`RecorderArguments.cs:1217`) kullanılır. Uygulama kapalıyken başlatma EKA-P5'in girişine ve sistemin görev zamanlayıcısına bırakılır; bu yüzden EKA-P5'ten sonra yapılır. Ölçü: sahte saatle zamanlama testleri.
  - Dokunacağı yer: src/VidShrink.App/Recorder/RecorderSettings.cs, src/VidShrink.App/Recorder/RecorderView.GeriSayim.cs, src/VidShrink.App/Recorder/RecorderView.Yakalama.cs, src/VidShrink.App/Locales/
- **EKA-P7 — Bas Konuş.** Kısayol arabirimi tuşun basılışını ve bırakılışını ayrı bildirecek biçimde genişletilir; bugünkü kayıt yalnız basışı veriyor (`RecorderHotkeys.cs:184`), bırakılışı görebilen klavye kancası başka iş için var (`RecorderInput.cs:227`). Tuş bırakılınca mikrofon izine sessizlik yazılır. En sağlam yol mikrofonu sistem sesi gibi uygulama tarafında yakalamaktır; çalışan ffmpeg'e süzgeç komutu göndermek ikinci seçenektir ve önce ölçülmelidir. Ölçü `tests/VidShrink.Tests/KaydediciKisayolAtamaTests.cs` yanına yazılır; gerçek deneme Windows'ta elle yapılır.
  - Dokunacağı yer: src/VidShrink.App/Recorder/RecorderHotkeys.cs, src/VidShrink.App/Recorder/RecorderView.Kisayol.cs, src/VidShrink.App/Recorder/RecorderInput.cs, src/VidShrink.Core/AudioCaptureArguments.cs, src/VidShrink.Ffmpeg/RecorderSession.cs
- **EKA-P8 — Kamera Ana Kaynak Olarak.** Hedef türlerine (`RecorderArguments.cs:24`) "aygıt" eklenir: web kamerası ya da yakalama kartı ekran girdisinin yerine ana görüntü olur. Windows'ta kamera girdisi zaten DirectShow ile kuruluyor (`RecorderArguments.cs:1203`); aynı kurucu ana girdi için yeniden kullanılır. Ölçü `tests/VidShrink.Tests/KaydediciHedefTests.cs` ve `KaydediciKameraTests.cs` yanına yazılır; gerçek deneme takılı bir kamerayla Windows'ta elle yapılır.
  - Dokunacağı yer: src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.Ffmpeg/CaptureDevices.cs, src/VidShrink.App/Recorder/RecorderView.Hedef.cs, src/VidShrink.App/Recorder/RecorderView.Kamera.cs
- **EKA-P9 — Kamera Katmanı Windows Dışında.** Kamera girdisi macOS'ta `avfoundation`, Linux'ta `v4l2` ile kurulur (ffmpeg'in o sistemlerdeki aygıt girdileri) ve kamera paneli yalnız Windows koşulundan çıkar (`RecorderView.Kamera.cs:53`). Kardeş tablodaki EKB-18 ve onun paketi EKB-P13 aynı iştir; yalnız biri açılır. Argüman üretimi `tests/VidShrink.Tests/KaydediciKameraTests.cs` yanında her sistem için ölçülür; gerçek aygıt denemesi bu makinede yapılamaz, o sistemlerde elle yapılır.
  - Dokunacağı yer: src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.Ffmpeg/CaptureDevices.cs, src/VidShrink.App/Recorder/RecorderView.Kamera.cs
- **EKA-P10 — Genel Kısayollar Windows Dışında.** Windows dışında bugün boş kısayol sürümü seçiliyor (`RecorderView.Kisayol.cs:29`, `RecorderHotkeys.cs:136`); macOS ve X11 karşılıkları yazılır: macOS'ta izin istemeyen sistem kısayol kaydı, X11'de tuş yakalama çağrısı. Wayland bu paketin dışında kalır; orada genel kısayol portal ister ve EKA-P14 ile birlikte düşünülür. Atama mantığı `tests/VidShrink.Tests/KaydediciKisayolAtamaTests.cs` içinde ölçülür; gerçek deneme o sistemlerde elle yapılır.
  - Dokunacağı yer: src/VidShrink.App/Recorder/RecorderHotkeys.cs, src/VidShrink.App/Recorder/RecorderView.Kisayol.cs
- **EKA-P11 — Model Tabanlı Gürültü Bastırma.** ffmpeg'in `arnndn` süzgeci (konuşmayı gürültüden ayırmak için eğitilmiş küçük bir modelle çalışan süzgeç) bugünkü `afftdn` seçeneğinin yanına üçüncü kademe olarak eklenir; model dosyası kurulumla gelir. Önce pinli ffmpeg derlemesinde süzgecin açık olduğu ve model dosyasının lisansı doğrulanır. Ölçü: argüman testi ve gürültülü örnek kayıtta önce ile sonra dinleme.
  - Dokunacağı yer: src/VidShrink.Core/AudioCaptureArguments.cs, src/VidShrink.App/Recorder/RecorderView.Ses.cs, Install-VidShrink.ps1 ve öbür kurucular
  - Sahibin kararı: Gürültü bastırma modeli (RNNoise dosyası) kurulum paketine girsin mi.
- **EKA-P12 — Mac Sistem Sesi Yardımcısı.** ScreenCaptureKit (macOS'un ekranı, tek pencereyi ve sistem sesini veren yakalama arabirimi) ffmpeg'de olmadığı için küçük bir yardımcı ikili yazılır; ham sesi verir, uygulama onu Windows'taki düzenle ffmpeg'e aktarır (`SystemAudioCapture.cs:36` bugün Windows dışında kaynak açmıyor). Sanal ses aygıtı yolu (`CaptureDevices.cs:59`) yedek olarak kalır. Bu makinede koşulamaz: derleme CI'ın macOS koşucusunda, ses denemesi gerçek Mac'te elle yapılır.
  - Dokunacağı yer: tools/ altında yeni yardımcı proje, src/VidShrink.Ffmpeg/SystemAudioCapture.cs, src/VidShrink.Ffmpeg/CaptureDevices.cs, src/VidShrink.Core/LoopbackAudio.cs, macOS kurucusu
  - Sahibin kararı: macOS için ScreenCaptureKit kullanan yerel bir yardımcı ikili yazılıp pakete eklensin mi.
- **EKA-P13 — Mac Pencere Yakalama.** EKA-P12'nin yardımcısı seçilen pencerenin karelerini de verir ve ffmpeg onları ham görüntü girdisi olarak okur; ekran kırpması (`RecorderWindows.cs:93`, `RecorderArguments.cs:1134`) yerini bu kaynağa bırakır. Argüman üretimi `tests/VidShrink.Tests/KaydediciPencereTests.cs` yanında ölçülür. Pencere taşınınca ve üstü örtülünce kaydın bozulmadığı gerçek Mac'te elle denenir; EKA-P12 bitmeden başlamaz.
  - Dokunacağı yer: tools/ altındaki yardımcı proje, src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.App/Recorder/RecorderWindows.cs, src/VidShrink.App/Recorder/RecorderWindowsMac.cs
  - Sahibin kararı: macOS'ta pencere yakalama EKA-P12'deki yardımcı ikiliye bağlansın mı; o karar hayır çıkarsa bu paket düşer.
- **EKA-P14 — Wayland Yakalama Yolu.** Wayland oturumunda (`RecorderWindowsX11.cs:22` bunu zaten anlıyor) ekran, kullanıcının onay verdiği portal üzerinden istenir ve gelen PipeWire akışı bir yardımcıyla ffmpeg'e boru olarak verilir; ffmpeg'in kendi PipeWire girişi yok. Karar "yalnız uyarı" çıkarsa iş küçülür: ekran ve bölge kaydı için de pencere seçicideki gibi açık hata gösterilir. Deneme Wayland ile açılan gerçek bir Linux masaüstünde elle yapılır.
  - Dokunacağı yer: src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.App/Recorder/RecorderWindowsX11.cs, src/VidShrink.App/Recorder/RecorderView.Secici.cs, Linux kurucusu
  - Sahibin kararı: Wayland için PipeWire portalını konuşan bir yardımcı ikili ya da yeni bağımlılık eklensin mi, yoksa yalnız açık bir uyarıyla yetinilsin mi.
- **EKA-P15 — HDR Yakalama.** İlk adım ölçümdür: HDR ekranda bugünkü kaydın nasıl göründüğü `docs/olcumler/` altında yok. Sonra hızlı yolda `ddagrab` süzgecinden 10 bit çıkış istenir (bugün kare hep 8 bit `bgra`ya iniyor, `RecorderArguments.cs:1118`), 10 bit kodlama ve HDR renk etiketleri yazılır. HDR olmayan ekranda davranış değişmez. Ölçü: argüman testi (`tests/VidShrink.Tests/KaydediciDdagrabTests.cs` yanında) ve HDR ekranda alınan dosyanın `ffprobe` çıktısı.
  - Dokunacağı yer: src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.App/Recorder/DdaOutputs.cs, src/VidShrink.App/Recorder/RecorderView.Gelismis.cs, docs/olcumler/
  - Sahibin kararı: HDR ekranda varsayılan HDR dosya mı olsun, yoksa SDR'ye çevrilmiş kayıt mı.
- **EKA-P16 — Oyun Yakalama.** İlk adım ölçümdür: tam ekran bir oyunun bugünkü hızlı yolla kaydı hiç ölçülmedi. Kayıt düzgünse iş küçülür: sihirbazdaki "Oyun" seçimi (`RecorderWizard.cs:5`) oyunun bulunduğu ekranı kendiliğinden hedef alır ve satır kapanır. Kayıt bozuksa kanca yolu (oyunun içine girip çizdiği kareyi oradan alan yöntem) sahibin kararına çıkar; hile koruması olan oyunlarda risk taşır. Ölçüm `tools/VidShrink.Bench` ile alınır ve `docs/olcumler/` altına yazılır.
  - Dokunacağı yer: src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.Core/RecorderWizard.cs, src/VidShrink.App/Recorder/RecorderView.Sihirbaz.cs, docs/olcumler/
  - Sahibin kararı: Oyun sürecine kanca atan ayrı bir yakalama yolu yapılsın mı, yoksa ekran kaydı yolu ölçülüp yeterli mi sayılsın.

Pakete girmeyenler:

- EKA-17: Karar kapalı (K3); kapalı satır pakete girmez.
- EKA-18: Tek rakipte; tampon koduna dokunan paket yok, yanına bedavaya gelmiyor.
- EKA-19: Sahibin kararını bekliyor ve tek rakipte; yanına geleceği bir paket yok.
- EKA-23: Orta iş ve dar kitle; ses grafiğini yeniden kurmayı ister, hiçbir paketin yanında bedava değil.
- EKA-25: Orta iş ve dar kitle; hızlı yol tek ekran çıkışıyla sınırlı, ayrı bir yakalama düzeni ister.
- EKA-26: Orta iş ve tek rakipte; EKA-P7 mikrofonu uygulama tarafına taşırsa yeniden bakılır.
- EKA-27: Kardeş tablodaki EKB-20 ile aynı iş; orada (`EKB-P15`) paketlenmiş.
- EKA-29: Tek rakipte; hiçbir paketin dosyalarına değmiyor.

Kanıt: [doğrulanmış tablo](piyasa/2026-10-13/kaydedici-yakalama-dogrulanmis.md) · [ham tablo](piyasa/2026-10-13/kaydedici-yakalama-eksikler.md) · [notlar ve kaynaklar](piyasa/2026-10-13/kaydedici-yakalama.md)

#### Sunum, Kayıt Sonrası Ve Paylaşım (EKB)

39 eksik yazıldı, doğrulamada 0 satır çıktı, 39 satır kaldı. Öncelik 1, 2, 3: 10, 20, 9.
17 satır sahibin kararını bekliyor, 3 satır kapalı bir karara denk geliyor. 17 paket önerildi.

- Kayıt anındaki efektlerde temel var: tıklama halkası, tıklama sesi, tuş gösterimi, büyüteç ve kamera katmanı çalışıyor. Hepsi kayda yakılıyor ve yalnız Windows'ta.
- Pazarın son iki yılda en çok eklediği şey kayıt sonrası cila: otomatik yakınlaştırma, imleç yumuşatma, arka planlı çerçeve. Bizde bu yol hiç yok. CleanShot X 5.0 ve Bandicam 27.0.6 aynı ay, Eylül 2026'da ekledi.
- Kayıt sonrası ve paylaşımda iskelet sağlam (küçült, düzenle, boşluk kırp, yükle, bağlantı, QR kod); eksik olan paneldeki kısa yollar.
- En ağır üç eksik: EKB-01 otomatik yakınlaştırma (13 üründe), EKB-03 imleç cilası, EKB-07 kamera arka planı. Üçü de büyük iş ve sahip kararı istiyor.
- En ucuz üç kazanç: EKB-11 tuş süzgeci (parolanın kayda düşmesini kapatır), EKB-05 sürekli imleç vurgusu, EKB-22 ile EKB-23 sonuç panelinde Kopyala ve GIF düğmesi.
- Sayılar: 39 satırın 10'u öncelik 1, 20'si öncelik 2, 9'u öncelik 3. 17 satır sahip kararı bekliyor, 19 satır karar istemiyor, 3 satır kapalı karara denk geliyor (K5, K5, K9).

| Paket | Ad | Satırlar | İş | Öncelik | Karar | Dalga |
|---|---|---|---|---|---|---|
| EKB-P1 | Tuş Gösterimi Süzgeci | EKB-11 | küçük | 1 | sahip | K |
| EKB-P2 | İmleç Vurgusu Ve Spot Işığı | EKB-05, EKB-10, EKB-12 | orta | 1 | gerekmez | 2 |
| EKB-P3 | Sonuç Paneli Ve Paylaşım Kısayolları | EKB-22, EKB-23, EKB-26, EKB-27, EKB-33 | orta | 2 | gerekmez | 3 |
| EKB-P4 | Gizlilik Maskesi | EKB-08 | orta | 1 | gerekmez | 2 |
| EKB-P5 | Kamera Biçimi Ve Konumu | EKB-06, EKB-16, EKB-17 | orta | 1 | gerekmez | 2 |
| EKB-P6 | Hızlı Kırpma Ve Kayıt Geçmişi | EKB-09, EKB-25 | büyük | 1 | gerekmez | 3 |
| EKB-P7 | Ekrana Çizim Ve Prompter | EKB-04, EKB-15 | büyük | 1 | gerekmez | 3 |
| EKB-P8 | Kayıt Çerçevesi | EKB-02 | orta | 1 | sahip | K |
| EKB-P9 | Otomatik Ve Canlı Yakınlaştırma | EKB-01, EKB-13 | büyük | 1 | sahip | K |
| EKB-P10 | İmleç Cilası Ve Hareket Bulanıklığı | EKB-03, EKB-31 | büyük | 1 | sahip | K |
| EKB-P11 | Kamera Arka Planı | EKB-07 | büyük | 1 | sahip | K |
| EKB-P12 | En-Boy Sığdırma Ön Ayarı | EKB-19 | orta | 2 | gerekmez | 3 |
| EKB-P13 | Kamera Katmanı Mac Ve Linux | EKB-18 | orta | 2 | gerekmez | 3 |
| EKB-P14 | Kayan Sonuç Kartı | EKB-24 | orta | 2 | sahip | K |
| EKB-P15 | Kayıtta Bildirim Ve Simge Gizleme | EKB-20, EKB-21 | orta | 2 | sahip | K |
| EKB-P16 | Özel Hedef Ve Kendi Bulutun | EKB-28, EKB-29 | büyük | 2 | sahip | K |
| EKB-P17 | Girdi Efektleri Mac Ve Linux | EKB-14 | büyük | 2 | sahip | K |

- **EKB-P1 — Tuş Gösterimi Süzgeci.** `Feed` içine süzgeç eklenir: açıkken yalnız Ctrl, Alt ya da Win ile basılan tuşlar ve işlev tuşları yazıya döner, düz harf ve rakam dönmez (RecorderInput.cs:87-98). Ayarlara "yalnız kısayollar" kutusu ile yazının konumu, boyutu ve süresi eklenir. `KaydediciGirdiTests` içinde süzgeç açıkken düz harfin boş, Ctrl+C'nin metin döndürdüğü ölçülür.
  - Dokunacağı yer: src/VidShrink.App/Recorder/RecorderInput.cs, src/VidShrink.App/Recorder/RecorderInputOverlay.cs, src/VidShrink.App/Recorder/RecorderSettings.cs, src/VidShrink.App/Recorder/RecorderView.axaml, src/VidShrink.App/Locales/
  - Sahibin kararı: "Yalnız kısayolları göster" süzgeci varsayılan açık mı gelsin, yoksa bugünkü gibi her tuş mu gösterilsin.
- **EKB-P2 — İmleç Vurgusu Ve Spot Işığı.** Fare kancası hareketi ve basılan düğmeyi de iletir; bugün yalnız tıklama noktası geliyor (RecorderInput.cs:202-212). Tıklama halkasının penceresi imleci izleyen sürekli halkaya, tersine boyanınca spot ışığına dönüşür; renk, boyut ve sol ile sağ tık rengi ayara bağlanır. Halka kayda ekrandan girdiği için ffmpeg tarafı değişmez. `KaydediciGirdiTests` içinde sol ve sağ tıkın ayrı renk verdiği, hareketin halkayı taşıdığı ölçülür.
  - Dokunacağı yer: src/VidShrink.App/Recorder/RecorderInput.cs, src/VidShrink.App/Recorder/RecorderInputOverlay.cs, src/VidShrink.App/Recorder/RecorderView.Girdi.cs, src/VidShrink.App/Recorder/RecorderSettings.cs, src/VidShrink.App/Recorder/RecorderView.axaml, src/VidShrink.App/Locales/
- **EKB-P3 — Sonuç Paneli Ve Paylaşım Kısayolları.** Sonuç paneline Kopyala ve GIF düğmeleri eklenir; kopyalama bitiş eylemindeki yolu, GIF oynatıcıdaki palet süzgecini kullanır (ClipExport.cs:55-63). Paylaş satırına hedef ve süre kutusu ile Sil düğmesi gelir; Küçült sekmesindeki parçalar örnek alınır (MainWindow.axaml.cs:2593-2613, :2769-2785). Bitiş eylemlerine "küçült, yükle, bağlantıyı kopyala" eklenir ve kayıt adı Küçült'teki ad deseninden kurulur; desene pencere adı ve sıra numarası yer tutucusu eklenir. Yükleme sahte sağlayıcıyla, ağa çıkmadan ölçülür (`DuzenleKaydetPaylasTests` düzeni).
  - Dokunacağı yer: src/VidShrink.App/Recorder/RecorderView.axaml, src/VidShrink.App/Recorder/RecorderView.axaml.cs, src/VidShrink.App/Recorder/RecorderView.Bitince.cs, src/VidShrink.App/Recorder/RecorderView.Paylas.cs, src/VidShrink.App/Recorder/RecorderSettings.cs, src/VidShrink.App/Share/ShareSession.cs, src/VidShrink.Core/AdlandirmaDeseni.cs, src/VidShrink.App/Locales/
- **EKB-P4 — Gizlilik Maskesi.** Kullanıcı bölge düzenleyicisinde bir ya da birkaç dikdörtgen işaretler. Dikdörtgenler yakalama süzgeç zincirine `crop`, `boxblur` (ffmpeg'in bulanıklaştırma süzgeci) ve `overlay` üçlüsüyle ya da dolu `drawbox` ile eklenir. Maske kayda yakıldığı için ham dosyada da hassas bölge görünmez. Bileşik karenin pikseli `KaydediciArkaPlanTests` düzeninde lavfi ile ölçülür: maske içi bulanık, dışı değişmemiş.
  - Dokunacağı yer: src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.App/Recorder/RecorderView.Duzenleyici.cs, src/VidShrink.App/Recorder/RecorderSettings.cs, src/VidShrink.App/Recorder/RecorderView.axaml, src/VidShrink.App/Locales/
- **EKB-P5 — Kamera Biçimi Ve Konumu.** Kamera zincirine ayna için `hflip` (yatay çevirme süzgeci), daire ve yuvarlak köşe için saydamlık maskesi eklenir (RecorderArguments.cs:937-945). Dört köşe ve dört hazır genişlik yerine oran olarak saklanan serbest konum ve boyut gelir; kamera karesi bölge düzenleyicisinde sürüklenir. `KaydediciKameraTests` argümanı, lavfi bileşik karesi köşe pikselinin saydam kaldığını ve kameranın istenen yere oturduğunu ölçer.
  - Dokunacağı yer: src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.App/Recorder/RecorderView.Kamera.cs, src/VidShrink.App/Recorder/RecorderSettings.cs, src/VidShrink.App/Recorder/RecorderView.axaml, src/VidShrink.App/Locales/
- **EKB-P6 — Hızlı Kırpma Ve Kayıt Geçmişi.** Sonuç paneline küçük resim şeridi ve iki tutamak eklenir; kesim yeniden kodlamadan `-ss`, `-t` ve `-c copy` ile yapılır ve en yakın anahtar kareye (tek başına çözülebilen kare) oturur; bu yol oynatıcıda var (ClipExport.cs:65-71). Aynı küçük resim üretimi son kayıtların listesinde kullanılır: ad, süre, boyut; aç, paylaş, sil. İki adımda teslim edilebilir: önce kırpma, sonra geçmiş. Kesilen dosyanın süresi ffprobe ile, liste sahte klasörle ölçülür.
  - Dokunacağı yer: src/VidShrink.App/Recorder/RecorderView.axaml, src/VidShrink.App/Recorder/RecorderView.axaml.cs, src/VidShrink.App/Recorder/, src/VidShrink.App/Playback/ClipExport.cs, src/VidShrink.App/Recorder/RecorderSettings.cs, src/VidShrink.App/Locales/
- **EKB-P7 — Ekrana Çizim Ve Prompter.** Çizim için ekranı kaplayan saydam bir pencere açılır; kısayolla çizim kipine girilir, kalem, ok, kutu, numara ve yazı çizilir, pencere kayda girer. Prompter (konuşma metnini akıtan pencere) ayrı penceredir ve `CaptureAffinity.Exclude` ile kayıttan çıkarılır; Windows 10 2004 öncesinde pencere kayıttan çıkmadığı için orada uyarı gösterilir (CaptureAffinity.cs:16-20). İki adımda teslim edilebilir: önce çizim, sonra prompter. Çizim araçları başsız pencere testiyle, kayıttan çıkarma bayrağı pencere başına ölçülür.
  - Dokunacağı yer: src/VidShrink.App/Recorder/, src/VidShrink.App/Recorder/CaptureAffinity.cs, src/VidShrink.App/Recorder/RecorderView.Kisayol.cs, src/VidShrink.App/Recorder/RecorderSettings.cs, src/VidShrink.App/Locales/
- **EKB-P8 — Kayıt Çerçevesi.** Biten kayıt ikinci bir ffmpeg geçişiyle arka planın üstüne oturtulur: `color` ya da `gradients` kaynağı, `scale`, yuvarlak köşe maskesi, gölge için bulanık kopya ve `overlay`. Sonuç panelinde "Çerçevele" düğmesi ile dolgu, köşe ve arka plan seçimi olur. Çıktının boyutu ve köşe pikselinin arka plan renginde olduğu lavfi ile ölçülür.
  - Dokunacağı yer: src/VidShrink.Core/, src/VidShrink.App/Recorder/RecorderView.axaml, src/VidShrink.App/Recorder/RecorderView.axaml.cs, src/VidShrink.App/Recorder/RecorderSettings.cs, src/VidShrink.App/Locales/
  - Sahibin kararı: Hazır arka plan görselleri pakete girsin mi, yoksa düz renk ve renk geçişiyle mi yetinilsin.
- **EKB-P9 — Otomatik Ve Canlı Yakınlaştırma.** Kayıt sonrası yol seçilirse fare kancası imleç konumunu ve tıklamaları zaman damgasıyla yan dosyaya yazar; kayıt bitince bu izden yakınlaşma bölgeleri çıkarılır ve `zoompan` ya da zamana bağlı `crop` ile `scale` zinciriyle yeni dosya üretilir, bölgeler düzenleyicide elle düzeltilir. Canlı yol seçilirse büyüteç merceği yerine kadrajın tamamı kısayolla yaklaşır. İz dosyası ve bölge planı saf fonksiyon olarak test edilir, çıktı karesi lavfi ile ölçülür.
  - Dokunacağı yer: src/VidShrink.App/Recorder/RecorderInput.cs, src/VidShrink.App/Recorder/RecorderMagnifier.cs, src/VidShrink.Core/, src/VidShrink.App/Editing/, src/VidShrink.App/Locales/
  - Sahibin kararı: Yakınlaştırma kayıttan sonra mı üretilsin (imleç ve tıklama verisi videodan ayrı kaydedilir), kayıt sırasında canlı mı; ikisinden biri seçilir.
- **EKB-P10 — İmleç Cilası Ve Hareket Bulanıklığı.** Kayıt imleçsiz alınır, imleç izi EKB-P9'un yan dosyasından okunur. İz yumuşatılır, imleç seçilen boyutta yeniden çizilip `overlay` ile bindirilir; hızlı geçişlere kare karıştırma (`tmix`) ile bulanıklık eklenir. Yumuşatma saf fonksiyon olarak, imlecin karedeki yeri lavfi ile ölçülür.
  - Dokunacağı yer: src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.App/Recorder/RecorderInput.cs, src/VidShrink.Core/, src/VidShrink.App/Editing/, src/VidShrink.App/Locales/
  - Sahibin kararı: İmleç kayda yakılmak yerine ayrı veri olarak tutulsun mu; EKB-P9'da kayıt sonrası yol seçilmeden bu paket başlamaz.
- **EKB-P11 — Kamera Arka Planı.** Kamera karesi kişi ayırma modelinden geçirilir (rakipler ONNX Runtime adlı model çalıştırma kitaplığını kullanıyor), çıkan maske ile arka plan bulanıklaştırılır, silinir ya da değiştirilir. Bugünkü sabit arka plan farkı ve yeşil perde seçenekleri yanında kalır (RecorderArguments.cs:926-935). Model ve kitaplık libmpv gibi sha256 özetiyle indirilir. Önce modelin boyutu ve kare hızı `tools/VidShrink.Bench` ile ölçülür, karar o sayıya göre verilir.
  - Dokunacağı yer: src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.App/Recorder/RecorderView.Kamera.cs, src/VidShrink.App/Recorder/RecorderSettings.cs, tools/, src/VidShrink.App/Locales/
  - Sahibin kararı: Kişiyi arka plandan ayıran model ve onu çalıştıran kitaplık uygulamayla dağıtılsın ya da ilk kullanımda indirilsin mi.
- **EKB-P12 — En-Boy Sığdırma Ön Ayarı.** Düzenleyicideki en-boy ön ayarları bugün ortadan kırpıyor (EditorView.Klip.cs:17, ClipEffects.cs:96-113). Yanına "sığdır" kipi eklenir: kare `scale` ile küçültülür, kalan alan `pad` ile seçilen düz renkle doldurulur; bu ikili dışa aktarımda zaten var (ClipFilters.cs:60-61). Çıktı boyutu ve dolgu pikseli düzenleyicinin dışa aktarım testlerinde ölçülür.
  - Dokunacağı yer: src/VidShrink.Core/Editing/ClipEffects.cs, src/VidShrink.Core/Editing/ClipFilters.cs, src/VidShrink.App/Editing/EditorView.Klip.cs, src/VidShrink.App/Locales/
- **EKB-P13 — Kamera Katmanı Mac Ve Linux.** Kamera girdisi macOS'ta `avfoundation`, Linux'ta `v4l2` ile açılır (ffmpeg'in o sistemlerdeki aygıt girdileri); aygıt listesi aynı yoldan okunur ve panel yalnız Windows koşulundan çıkar (RecorderView.Kamera.cs:53, RecorderArguments.cs:894-895). Kardeş tablodaki EKA-07 ile aynı iştir, yalnız biri yapılır. Argüman üretimi her sistem için `KaydediciKameraTests` içinde ölçülür; gerçek aygıt denemesi o sistemlerde elle yapılır.
  - Dokunacağı yer: src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.Ffmpeg/, src/VidShrink.App/Recorder/RecorderView.Kamera.cs, src/VidShrink.App/Locales/
  - Aynı iş: EKA-P9. Rafta o başlığın altında duruyor.
- **EKB-P14 — Kayan Sonuç Kartı.** Kayıt bitince köşede küçük resimli, birkaç saniye sonra kaybolan bir kart açılır; dosya karttan başka uygulamaya sürüklenir (Avalonia'nın sürükle-bırak yolu, dosya verisiyle). Kart `CaptureAffinity` ile sonraki kayıttan çıkarılır. Kartın açılıp kapanması ve sürüklenen verinin dosya yolunu taşıdığı başsız pencere testiyle ölçülür.
  - Dokunacağı yer: src/VidShrink.App/Recorder/, src/VidShrink.App/Recorder/RecorderView.axaml.cs, src/VidShrink.App/Recorder/RecorderSettings.cs, src/VidShrink.App/Locales/
  - Sahibin kararı: Kayıt bitince ekran köşesinde kendiliğinden yeni bir pencere açılsın mı ve bu varsayılan açık mı gelsin.
- **EKB-P15 — Kayıtta Bildirim Ve Simge Gizleme.** Kayıt başlarken bildirimler susturulur ve masaüstü simgeleri gizlenir, bitince eski durum geri yüklenir; eski durum diske yazılır ki çökmeden sonra açılışta geri alınsın. Windows'ta bildirim susturmanın belgeli yolu doğrulanmadı; bulunamazsa EKB-20 yapılmaz, yalnız simge gizleme teslim edilir. EKB-20 kardeş tablodaki EKA-27 ile aynı iştir. Geri yükleme sahte sistem katmanıyla, çökme senaryosu dahil ölçülür.
  - Dokunacağı yer: src/VidShrink.App/Recorder/, src/VidShrink.App/Recorder/RecorderSettings.cs, src/VidShrink.App/Recorder/RecorderView.axaml, src/VidShrink.App/Locales/
  - Sahibin kararı: Uygulama kayıt sırasında sistemin bildirim ayarına ve masaüstü simgelerine dokunsun mu.
- **EKB-P16 — Özel Hedef Ve Kendi Bulutun.** Önce hedef tablosuna arayüzden satır ekleme gelir: adres, alan adları, isteğe bağlı anahtar; tablo bugün yalnız elle JSON düzenleyerek genişliyor (ShareTargets.cs:188-229, IShareProvider.cs:60-80). Ardından S3 uyumlu depo (Amazon'un dosya deposu protokolü, başka servisler de konuşur) için yeni sağlayıcı yazılır; Google Drive tarayıcıdan giriş istediği için en sona kalır. Anahtar işletim sisteminin korumalı deposunda tutulur. Sağlayıcılar sahte taşıyıcıyla, ağa çıkmadan ölçülür.
  - Dokunacağı yer: src/VidShrink.Core/Share/, src/VidShrink.App/Share/, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/Locales/
  - Sahibin kararı: Kullanıcının girdiği sunucu adresi ve erişim anahtarı uygulamada saklansın mı, bulut hesabına tarayıcıdan giriş eklensin mi.
- **EKB-P17 — Girdi Efektleri Mac Ve Linux.** Boş dinleyicinin yerine macOS ve X11 için gerçek fare ve klavye dinleyicileri yazılır (RecorderView.Girdi.cs:14-22); Wayland ve X11, Linux'un iki ayrı pencere sistemidir. Büyüteç için ekran kopyası o sistemlerin kendi çağrısıyla alınır. İzin verilmediğinde efekt kapalı kalır ve kullanıcıya söylenir. Dinleyiciler arayüzün arkasında sahte olay akışıyla ölçülür; gerçek deneme o sistemlerde elle yapılır.
  - Dokunacağı yer: src/VidShrink.App/Recorder/RecorderInput.cs, src/VidShrink.App/Recorder/RecorderView.Girdi.cs, src/VidShrink.App/Recorder/RecorderMagnifier.cs, src/VidShrink.App/Locales/
  - Sahibin kararı: macOS'ta girdi izleme izni istensin mi, ve Wayland'da genel tuş dinleme yolu yoksa Linux desteği X11 ile mi sınırlansın.

Pakete girmeyenler:

- EKB-30 — Öncelik 3, tek rakipte (Screen Studio); orta iş ve hiçbir paketin yanında bedavaya gelmiyor.
- EKB-32 — Öncelik 3; K11'in bu işi kapsayıp kapsamadığı sahibin kararı, karar gelmeden paket açılmaz.
- EKB-34 — Öncelik 3, tek rakipte (CleanShot X); kendi sunucumuzu ya da ücretli bulut hizmetini gerektirir.
- EKB-35 — Öncelik 3; sunucu işletmeyi gerektirir, bugünkü anahtarsız yükleme hedefleriyle yapılamaz.
- EKB-36 — Öncelik 3, tek rakipte (ShareX); üçüncü taraf kısaltma servisine bağımlılık getirir.
- EKB-37 — Kapalı karar K5 (otomatik altyazı).
- EKB-38 — Kapalı karar K5 (konuşmadan metne dayanıyor).
- EKB-39 — Kapalı karar K9 (ekran görüntüsü aracı).

Kanıt: [doğrulanmış tablo](piyasa/2026-10-13/kaydedici-sunum-dogrulanmis.md) · [ham tablo](piyasa/2026-10-13/kaydedici-sunum-eksikler.md) · [notlar ve kaynaklar](piyasa/2026-10-13/kaydedici-sunum.md)

### Ayarlar Ve Kabuk

#### Ayarlar, Hakkında Ve Pencere (AYR)

48 eksik yazıldı, doğrulamada 0 satır çıktı, 48 satır kaldı. Öncelik 1, 2, 3: 3, 22, 23.
18 satır sahibin kararını bekliyor, 0 satır kapalı bir karara denk geliyor. 12 paket önerildi.

- Görünüm ve dil sayısında güçlüyüz: 36 palet, 42 dil klasörü, oynatıcı ve düzenleyicide düzenlenebilir kısayol tablosu, düşüşünü sayan sessiz güncelleme.
- Rakiplerde yaygın olup bizde olmayanlar ayarın korunması ve kullanıcıyı bilgilendirme tarafında toplanıyor: ayar yedeği yok, bozuk ayar sessizce sıfırlanıyor, pencere yerini hatırlamıyor, güncellemenin ne getirdiği gösterilmiyor.
- Erişilebilirlikte kod hazırlığı var (283 erişilebilir ad, odak halkası) ama hiçbir deneme kaydı yok; durum ölçüyle değil tahminle biliniyor.
- En ağır üç eksik: ayar yedeği ile bozulmada sessiz düşüş (AYR-01, AYR-42), pencere konumunu hatırlamama (AYR-03), taşınabilir kip (AYR-05).
- En ucuz üç kazanç: "şimdi denetle" düğmesi (AYR-47), günlüğü uygulama içinde okuma (AYR-10; okuyan yöntem hazır), Hakkında'ya sürüm notu, lisans ve gizlilik bağlantıları (AYR-06, AYR-11, AYR-29).
- Öncelik 1 ya da 2 olan 25 satırın 15'i karar beklemeden altı pakette yapılabilir; 10'u altı pakette sahibin kararını bekliyor.

| Paket | Ad | Satırlar | İş | Öncelik | Karar | Dalga |
|---|---|---|---|---|---|---|
| AYR-P1 | Pencere Konumunu Hatırlama | AYR-03 | küçük | 1 | sahip | K |
| AYR-P2 | Ayar Yedeği Ve Bozulma Uyarısı | AYR-01, AYR-42 | orta | 1 | gerekmez | 2 |
| AYR-P3 | Güncelleme, Hakkında Ve Tanılama | AYR-06, AYR-07, AYR-10, AYR-47, AYR-11, AYR-27, AYR-29 | orta | 2 | gerekmez | 3 |
| AYR-P4 | Sistem Teması Ve Hareket Ayarı | AYR-02, AYR-14, AYR-15 | orta | 2 | gerekmez | 3 |
| AYR-P5 | Uzun İşte Öncelik, Bildirim Ve Ses | AYR-19, AYR-20, AYR-21 | orta | 2 | gerekmez | 3 |
| AYR-P6 | Kabuk Kısayolları Ve Ayar Araması | AYR-08, AYR-18, AYR-26, AYR-33, AYR-43 | orta | 2 | gerekmez | 3 |
| AYR-P7 | Erişilebilirlik Denemesi | AYR-16, AYR-17 | orta | 2 | gerekmez | 3 |
| AYR-P8 | Taşınabilir Kip Ve Güncelleme Kanalı | AYR-05, AYR-09 | büyük | 1 | sahip | K |
| AYR-P9 | Tepsiye Küçültme | AYR-22 | küçük | 2 | sahip | K |
| AYR-P10 | Çeviri Katkısı Ve Tamlık İşareti | AYR-04, AYR-25 | orta | 2 | sahip | K |
| AYR-P11 | Arayüz Ölçeği Ve Yüksek Karşıtlık | AYR-12, AYR-13 | büyük | 2 | sahip | K |
| AYR-P12 | İlk Açılış Karşılaması Ve Kılavuz | AYR-23, AYR-24 | büyük | 2 | sahip | K |

- **AYR-P1 — Pencere Konumunu Hatırlama.** Pencerenin konumu, boyutu, ekranı ve kaplama durumu kapanışta, bugün yalnız panel yüksekliğini tutan yerleşim dosyasına yazılır ve açılışta oradan okunur. Kayıtlı konum bağlı ekranların hiçbirine düşmüyorsa bugünkü açılışa dönülür. Doğrulama: pencere açmadan koşan arayüz testiyle kaydet ve geri yükle turu, bir de sökülmüş ikinci ekran durumu.
  - Dokunacağı yer: src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs
  - Sahibin kararı: Uygulama her açılışta ekranı kaplamış açılmak yerine son konumunda, boyutunda ve durumunda mı açılsın?
- **AYR-P2 — Ayar Yedeği Ve Bozulma Uyarısı.** Ayarlar'a "dışa aktar" ve "içe aktar" düğmeleri eklenir; paket, sıfırlamanın zaten saydığı dosya listesinden tek zip olarak üretilir, gizli anahtar taşıyan dosyalar dışarıda kalır. Ayar dosyası her başarılı okumadan sonra bir yedek kopyaya yazılır; dosya okunamazsa yedekten dönülür, bozuk dosya silinmeyip yanına ayrılır ve kullanıcıya uyarı durumuyla söylenir. Doğrulama: dışa aktar, sıfırla, içe aktar turunun testi ve bilerek bozulmuş `settings.json` ile açılış testi.
  - Dokunacağı yer: src/VidShrink.App/AppSettings.cs, src/VidShrink.App/AppDataReset.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/Locales/
- **AYR-P3 — Güncelleme, Hakkında Ve Tanılama.** Güncelleme kartına otomatik güncelleme açıkken de çalışan "şimdi denetle" düğmesi ve son denetimin sonucu eklenir; güncellemeden sonraki ilk açılışta sürüm notlarına giden kısa bir bildirim gösterilir. Hakkında'ya sürüm notları, lisanslar ve gizlilik beyanı bağlantıları konur. Tanılama kartında günlük uygulama içinde okunur ve kopyalanır; "hata bildir" düğmesi sürümü, işletim sistemini ve günlük yolunu taşıyan hazır kaydı açar, yanındaki düğme günlüğü ve sistem bilgisini tek zip'e toplar. Doğrulama: her düğmenin açtığı adresi ve ürettiği metni ölçen testler ile 42 dilde dizgi tamlığı testi.
  - Dokunacağı yer: src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/MainWindow.Guncelleme.cs, src/VidShrink.Core/Gunluk.cs, .github/ISSUE_TEMPLATE/, src/VidShrink.App/Locales/
- **AYR-P4 — Sistem Teması Ve Hareket Ayarı.** Görünüm kartına kapalı gelen "sistem temasını izle" seçeneği eklenir; açıkken kullanıcının seçtiği bir açık ve bir koyu palet arasında Avalonia'nın sistem renk olayıyla geçilir, geçiş yine yalnız `PaletteCatalog.Use` üstünden yapılır. `AnimationsAllowed` iki kopyadan tek yere toplanır, macOS ve Linux'ta sistemin "hareketi azalt" ayarını okur ve Ayarlar'daki elle "canlandırmaları kapat" anahtarını da dinler. Doğrulama: palet geçişi ve üç işletim sistemi kolu için birim testleri; sistem ayarı teste sahte kaynakla verilir.
  - Dokunacağı yer: src/VidShrink.App/Themes/PaletteCatalog.cs, src/VidShrink.App/AppSettings.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/Playback/HoverZone.cs
- **AYR-P5 — Uzun İşte Öncelik, Bildirim Ve Ses.** Ayarlar'a kodlama sürecinin önceliği (düşük, normal, yüksek; varsayılan bugünkü gibi normal) eklenir ve süreç başlarken uygulanır. İş bitti bildirimi macOS ve Linux'ta sistemin kendi bildirim yoluyla gösterilir; kapalı gelen bir "bitince ses çal" seçeneği ve ses dosyası seçimi eklenir. Doğrulama: öncelik için süreç başlatma testi, bildirim için işletim sistemi başına komut üreten katmanın testi; gerçek bildirim macOS ve Linux'ta bir kez elle görülür.
  - Dokunacağı yer: src/VidShrink.App/IsBittiBildirimi.cs, src/VidShrink.Ffmpeg/EncodeRunner.cs, src/VidShrink.Ffmpeg/FfmpegRunner.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/Locales/
- **AYR-P6 — Kabuk Kısayolları Ve Ayar Araması.** Kısayol eşlemesine oynatıcı ve düzenleyicinin yanına bir kabuk takımı eklenir: sekmeler arası geçiş, Ayarlar, dosya açma, bölgeler arası geçiş ve kullanım belgesini açan yardım tuşu; hepsi aynı düzenlenebilir tabloda görünür. Ayarlar sayfasına ve kısayol tablosuna aynı süzme kutusu konur. Bölge geçişinin varsayılan tuşu F6 olamaz, çünkü kaydedici onu bölüm işareti için tutuyor. Doğrulama: tuş eşleme ve çakışma testleri ile süzme kutusu için kart görünürlüğü testi.
  - Dokunacağı yer: src/VidShrink.App/Playback/ShortcutBindings.cs, src/VidShrink.App/Playback/ShortcutTable.cs, src/VidShrink.App/MainWindow.PencereKipi.cs, src/VidShrink.App/MainWindow.axaml
- **AYR-P7 — Erişilebilirlik Denemesi.** Her sekmede bir ana iş baştan sona önce yalnız klavyeyle, sonra ekran okuyucuyla (Windows'ta Anlatıcı ve NVDA) yapılır; sekme sırası, klavye tuzağı ve adı okunmayan denetimler tek tek yazılır. Bulgular `docs/olcumler` altına kayıt olarak girer; adı eksik denetim ve bozuk sekme sırası gibi küçük kusurlar aynı turda düzeltilir, büyükleri deftere yazılır. macOS'ta VoiceOver denemesi o makinede ayrıca yapılır.
  - Dokunacağı yer: docs/olcumler/, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/Themes/Controls.axaml
- **AYR-P8 — Taşınabilir Kip Ve Güncelleme Kanalı.** Önce AYR-05: exe'nin yanındaki bir işaret dosyası ayar ve günlük yolunu uygulama klasörüne çevirir; bu kipte güncellemenin nasıl davranacağı sahibin kararına göre kurulur. Sonra AYR-09: Güncelleme kartına kararlı ve ön sürüm seçimi eklenir, denetim seçilen kanalın yayınlarına bakar. İki satır dağıtım tablosundaki DAG-10 ve DAG-24 ile aynı iştir; orada DAG-24 kendi paketinde, DAG-10 ise DAG-11 ve DAG-20 ile aynı pakette duruyor, birleştirmede bu paketlerle tek işe indirilmelidir. Doğrulama: işaret dosyasıyla ayar yolunun testi ve kanal seçiminin sürüm karşılaştırma testi.
  - Dokunacağı yer: src/VidShrink.Core/UpdateCheck.cs, src/VidShrink.App/MainWindow.axaml, .github/workflows/, docs/kurulum.md
  - Sahibin kararı: Yayına iki yeni biçim eklensin mi: ayarını yanında taşıyan kurulumsuz paket ve gönüllülere açık ön sürüm kanalı?
- **AYR-P9 — Tepsiye Küçültme.** Kaydedicinin tepsi simgesi kodu ana pencerenin de kullanabileceği ortak bir parçaya çıkarılır. Seçenek açıkken küçültülen pencere görev çubuğundan kalkar, tepsi simgesine tıklanınca geri gelir; kuyruk ilerlemesi simgenin ipucunda yazar. Doğrulama: pencere durumu geçişlerinin testi; tepsi simgesi Windows'ta bir kez elle görülür.
  - Dokunacağı yer: src/VidShrink.App/Recorder/RecorderTray.cs, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/MainWindow.axaml
  - Sahibin kararı: Ana pencere için kapalı gelen bir "tepsiye küçült" seçeneği eklensin mi; eklenecekse kapatma düğmesi de tepsiye mi gitsin?
- **AYR-P10 — Çeviri Katkısı Ve Tamlık İşareti.** Önce küçük adım olan AYR-25: insan gözünden geçmiş diller bir listede tutulur, öteki diller dil seçicide işaretlenir. Sonra AYR-04: sahibin seçtiği çeviri sitesi (Weblate, Crowdin ya da Transifex) depodaki `Locales` klasörüne bağlanır ve README'deki katkı yolu oraya çevrilir. Doğrulama: işaretli dil listesinin testi ve siteden gelen ilk deneme çevirisinin dizgi tamlığı testinden geçmesi.
  - Dokunacağı yer: src/VidShrink.App/Localization/Strings.cs, src/VidShrink.App/Locales/, README.md, .github/workflows/
  - Sahibin kararı: 42 dil sözü nasıl sunulsun: gözden geçmemiş diller listede işaretlensin mi ve bir çeviri sitesinde proje hesabı açılsın mı?
- **AYR-P11 — Arayüz Ölçeği Ve Yüksek Karşıtlık.** Önce AYR-13: sistem yüksek karşıtlık istediğinde seçilecek bir yüksek karşıtlıklı palet eklenir ve geçiş `PaletteCatalog.Use` üstünden yapılır. Sonra AYR-12: yazı boyutu belirteçlerine tek bir ölçek çarpanı bağlanır, Ayarlar'a yazı boyutu seçimi konur ve en küçük pencere boyutunda taşma her sekmede ölçülür. Doğrulama: karşıtlık oranı ölçümü ve pinli yerleşim ölçülerinin her ölçekte yeniden koşulması.
  - Dokunacağı yer: src/VidShrink.App/Themes/Theme.axaml, src/VidShrink.App/Themes/PaletteCatalog.cs, src/VidShrink.App/Themes/Palette/, src/VidShrink.App/MainWindow.axaml
  - Sahibin kararı: "Renk yalnız paletten, ölçü yalnız belirteçten" kuralına sistemin karşıtlık ve yazı boyutu ayarları için istisna açılsın mı?
- **AYR-P12 — İlk Açılış Karşılaması Ve Kılavuz.** Önce AYR-24: depodaki iki dilli kullanım belgesi sekme sekme tamamlanır ve sahibin seçtiği yerde site olarak yayımlanır. Sonra AYR-23: ilk açılışta bir kez görünen, beş işi gösteren ve kılavuza bağlanan kısa karşılama eklenir; kapatılınca bir daha çıkmaz. Doğrulama: karşılamanın yalnız ilk açılışta göründüğünün testi ve belgedeki her ekran görüntüsünün güncel sürümden alınması.
  - Dokunacağı yer: docs/kullanim.md, docs/kullanim.tr.md, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, README.md
  - Sahibin kararı: Yeni kullanıcıya yardım nasıl sunulsun: ilk açılışta bir karşılama ekranı ve dillerde sürdürülen çevrimiçi kılavuz sitesi açılsın mı?

Pakete girmeyenler:

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

Kanıt: [doğrulanmış tablo](piyasa/2026-10-13/ayarlar-kabuk-dogrulanmis.md) · [ham tablo](piyasa/2026-10-13/ayarlar-kabuk-eksikler.md) · [notlar ve kaynaklar](piyasa/2026-10-13/ayarlar-kabuk.md)

### Dağıtım Ve Otomasyon

#### Kurulum, Güncelleme Ve Komut Satırı (DAG)

42 eksik yazıldı, doğrulamada 0 satır çıktı, 42 satır kaldı. Öncelik 1, 2, 3: 4, 19, 19.
24 satır sahibin kararını bekliyor, 1 satır kapalı bir karara denk geliyor. 12 paket önerildi.

- Güncelleme tarafında pazarın önündeyiz: yerinde takas, tek paket, sha256 doğrulaması ve düşüş sayacı var. Ulaşma tarafında gerideyiz: iki platformda da imzasızız ve beş paket dizininin (winget, Scoop, Chocolatey, Flathub, Homebrew) hiçbirinde yokuz.
- Yayındaki 23 dosyanın tamamı zip, liste ve tek indirici kurucu; dmg, msi, msix, AppImage, deb yok.
- Komut satırı küçültmede güçlü (`kucult`, `plan`, `izle`, `profiller`, `--json`), ama yalnız küçültüyor: dönüştürme ve kayıt komutu yok, ilerleme makinece okunmuyor.
- En ağır üç eksik: Windows kod imzası (DAG-02), Linux'ta paket (DAG-04), çalışan uygulamayı dışarıdan süren kanal (DAG-35).
- En ucuz üç kazanç: "Yüklü uygulamalar" kaydı (DAG-17), adı konmuş sessiz kurulum (DAG-16), makinece okunur ilerleme (DAG-29). İlk ikisi winget'in ön koşulu.
- winget (DAG-01) öncelik 1 satırların en ucuzu: tek engel sahibin CLA onayı ve DAG-P1.
- macOS zincirinin tamamı (imza, dmg, Homebrew) tek karara bağlı: ücretli Apple geliştirici üyeliği.
- 42 satırın 24'ü sahip kararı bekliyor; karar beklemeyen 17 satırın 11'i dört pakette toplandı.

| Paket | Ad | Satırlar | İş | Öncelik | Karar | Dalga |
|---|---|---|---|---|---|---|
| DAG-P1 | Sessiz Kurulum Ve Uygulama Kaydı | DAG-16, DAG-17, DAG-19, DAG-28 | orta | 2 | gerekmez | 3 |
| DAG-P2 | Komut Satırı Çıktısı Ve Dönüştürme | DAG-29, DAG-30, DAG-33, DAG-34 | orta | 2 | gerekmez | 3 |
| DAG-P3 | Windows 11 İlk Menüde Küçültme | DAG-37 | orta | 2 | gerekmez | 3 |
| DAG-P4 | Yerel Denetim Borusu Ve Kayıt Komutu | DAG-35, DAG-31 | büyük | 1 | gerekmez | 3 |
| DAG-P5 | Winget, Scoop Ve Chocolatey Paketleri | DAG-01, DAG-08, DAG-12 | orta | 1 | sahip | K |
| DAG-P6 | Windows Kod İmzası | DAG-02 | orta | 1 | sahip | K |
| DAG-P7 | Ön Sürüm Güncelleme Kanalı | DAG-24 | orta | 2 | sahip | K |
| DAG-P8 | Flathub Ve AppImage Paketleri | DAG-04, DAG-09 | büyük | 1 | sahip | K |
| DAG-P9 | Tam Paket Ve Çevrimdışı Kurucu | DAG-10, DAG-11, DAG-20 | büyük | 2 | sahip | K |
| DAG-P10 | macOS İmza, Dmg Ve Homebrew | DAG-05, DAG-06, DAG-07 | büyük | 2 | sahip | K |
| DAG-P11 | Microsoft Store Paketi | DAG-03 | büyük | 2 | sahip | K |
| DAG-P12 | Betik Ve Eklenti Arabirimi | DAG-36 | büyük | 2 | sahip | K |

- **DAG-P1 — Sessiz Kurulum Ve Uygulama Kaydı.** Kurucuya adı belli bir sessiz kip eklenir (`--silent`: pencere açmaz, soru sormaz, hata olunca beklemeden sıfırdan farklı kodla çıkar) ve yardım metni ile docs/kurulum.md'ye yazılır. Kurulum, Windows'un Ayarlar listesini besleyen kullanıcı düzeyi kaldırma kaydını (ad, sürüm, yayıncı, simge, kaldırma komutu) yazar, kaldırma onu siler; kaldırmaya ayar klasörünü de silen bir seçenek gelir. İsteğe bağlı ve varsayılanı kapalı bir seçenek kullanıcı PATH'ine yalnız küçük bir `bin` klasörü ekler; içinde komut satırı aracını çağıran küçük bir sarmalayıcı (örneğin tek satırlık `vidshrink.cmd`) durur, çünkü kökteki başlatıcı aynı adı taşıyor. Doğrulama: kurulum ve kaldırma testleri sahte kayıt köküyle, elle de Ayarlar listesinde görünme, sürümün doğru yazılması ve kaldırmanın iz bırakmaması.
  - Dokunacağı yer: src/VidShrink.Setup/Program.cs, src/VidShrink.Core/Setup/SetupRunner.cs, src/VidShrink.Core/Setup/SetupText.cs, Install-VidShrink.ps1, docs/kurulum.md
- **DAG-P2 — Komut Satırı Çıktısı Ve Dönüştürme.** Dört ek aynı iki dosyada olduğu için tek elde yapılır. `--json` ile birlikte ilerleme satır başına bir JSON nesnesi olarak yazılır (NDJSON: her satırı ayrı okunabilen JSON akışı); `profiller` komutuna çekirdekteki hazır `Export` işlevini çağıran dışa aktarma seçeneği, bir de bash, zsh, fish ve PowerShell için Tab tamamlama betiğini basan `tamamlama` komutu gelir. `donustur` komutu Dönüştür sekmesinin argüman üretimini çağırır ve kodek kümesini arayüzdekiyle eşitler; o üretim çekirdekte değilse önce oraya taşınır (bu turda bakılmadı). Doğrulama: ayrıştırma ve çıktı testleri, `donustur` için arayüzle aynı ffmpeg argümanını üretme testi.
  - Dokunacağı yer: src/VidShrink.Cli/CliRequest.cs, src/VidShrink.Cli/CliApp.cs, src/VidShrink.Core/PresetLibrary.cs, docs/cli.md, docs/cli.tr.md
- **DAG-P3 — Windows 11 İlk Menüde Küçültme.** Kabuk eklentisi (Windows 11'in ilk sağ tık menüsüne girdi koyan C++ bileşeni) bugün tek komut döndürüyor ve alt komut kolu boş. Açma girdisinin yanına "VidShrink ile Küçült" üst girdisi eklenir: `GetFlags` `ECF_HASSUBCOMMANDS` döndürür, `EnumSubCommands` altı hedefi `ShellIntegration.QuickShrinkMenu` sırasıyla sayar ve her biri başlatıcıyı `--kucult` ile çağırır. Etiketler var olan `ShellLabels` kayıt anahtarından okunur, böylece menü dili korunur. Doğrulama: listeyi üç kurucuyla karşılaştıran mevcut teste eklenti dizisi eklenir; Windows 11'de ilk menüde alt menünün görünmesi ve bir hedefin küçültmeyi başlatması elle bakılır.
  - Dokunacağı yer: src/VidShrink.ShellExtension/VidShrink.ShellExtension.cpp, src/VidShrink.ShellExtension/AppxManifest.template.xml, src/VidShrink.Core/ShellIntegration.cs, src/VidShrink.Core/Setup/SetupRunner.cs, Install-VidShrink.ps1
- **DAG-P4 — Yerel Denetim Borusu Ve Kayıt Komutu.** Tek örnek borusu (ikinci kopyanın ilk kopyaya dosya yolu ilettiği, yalnız aynı kullanıcıya açık adlandırılmış boru) belgeli bir komut kanalına genişletilir: satır başına bir JSON komutu ve yanıtı (oynat, duraklat, konum, dosya aç, kaydı başlat ve durdur, kuyruğa küçültme ekle, durum sor). Yeni komutlar Ayarlar'daki bir anahtar ya da başlatma bayrağı açılmadıkça reddedilir; ağ soketi açılmaz, yeni paket eklenmez. Komut satırına bu kanalın istemcisi olan kayıt başlatma, kayıt durdurma ve durum komutları gelir; protokol docs/ altında belgelenir. Doğrulama: boru üstünden komut ve yanıt testleri, kapalıyken reddetme testi, elle de komutla kayıt başlatıp durdurma.
  - Dokunacağı yer: src/VidShrink.Core/SingleInstanceChannel.cs, src/VidShrink.Core/ShrinkRequest.cs, src/VidShrink.App/Integration/, src/VidShrink.App/Recorder/, src/VidShrink.Cli/, docs/
- **DAG-P5 — Winget, Scoop Ve Chocolatey Paketleri.** DAG-P1'den sonra yapılır: winget etkileşimsiz kurulum ister ve kurulu sürümü kaldırma kaydından tanır. winget bildirimi (paketin adını, sürümünü, kurucu adresini ve sha256'sını taşıyan YAML dosyaları) `VidShrink-Setup.exe` için yazılır; Scoop için tek JSON, Chocolatey için paket tanımı ve kurulum betiği hazırlanır. `release.yml` her sürümde üç bildirimin sürüm ve özet alanını tazeler. Doğrulama: bildirim doğrulama komutu ve temiz bir Windows'ta kurma, yükseltme ve kaldırma.
  - Dokunacağı yer: .github/workflows/release.yml, yeni packaging/winget/, packaging/scoop/, packaging/chocolatey/; depo dışında microsoft/winget-pkgs PR'ı ve kova deposu
  - Sahibin kararı: Windows paket dizinlerinde yayın yapılsın mı; winget için PR sahibin hesabından açılır ve Microsoft katkı sözleşmesi onaylanır, Scoop için kova deposu açılır, Chocolatey için hesap gerekir.
- **DAG-P6 — Windows Kod İmzası.** Başvurunun onayı dışarıda beklediği için karar erken verilmeli; öteki paketler bu sırada yürür. Onaydan sonra `release.yml`'e imza adımı girer: kurucu, başlatıcı, uygulama, komut satırı aracı ve kabuk eklentisi, x64 ve ARM64 için; sha256 listeleri imzadan sonra üretilir. README'deki "başvuru sürüyor" cümlesi gerçek duruma çekilir. Doğrulama: yayın dosyalarında imza denetimi geçerli döner; temiz makinede SmartScreen ve Akıllı Uygulama Denetimi davranışı elle bakılır.
  - Dokunacağı yer: .github/workflows/release.yml, docs/imzalama.md, README.md, src/VidShrink.ShellExtension/AppxManifest.template.xml
  - Sahibin kararı: SignPath Foundation'a başvurulsun mu (ücretsiz; başvuruyu ve her imza onayını sahip yapar), yoksa ücretli sertifika mı alınsın.
- **DAG-P7 — Ön Sürüm Güncelleme Kanalı.** `release.yml` ekli etiketleri GitHub'da ön sürüm olarak yayımlar; kararlı kanal bunları görmez. Ayarlar'daki Güncelleme kartına kararlı ve ön sürüm seçimi gelir, varsayılan kararlı kalır; `UpdateCheck` seçili kanala göre en yeni sürümü seçer (ek ayrıştırması hazır). Ön sürümden kararlıya dönüş ve sessiz güncellemenin düşüş sayacı iki kanalda da test edilir. Aynı eksik komşu tabloda AYR-09; tek iş olarak yapılır.
  - Dokunacağı yer: src/VidShrink.Core/UpdateCheck.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/Locales/, .github/workflows/release.yml
  - Sahibin kararı: ön sürüm yayımlansın mı ve Ayarlar'a kanal seçimi eklensin mi; sürüm kesme düzeni değişir.
- **DAG-P8 — Flathub Ve AppImage Paketleri.** Flatpak bildirimi (uygulamanın nasıl derleneceğini ve hangi izinlerle çalışacağını söyleyen dosya) yazılır; ffmpeg ve libmpv Flathub çalışma ortamından ya da bildirimde kaynaktan derlenerek gelir, uygulama kimliği, tanıtım dosyası ve simge eklenir. Paketin içinde kendini güncelleme kapatılır, güncellemeyi Flathub yapar. AppImage aynı yayın çıktısından `release.yml` içinde üretilir ve sha256 listesine girer. Doğrulama: CI'da paketi kurup küçültme ve oynatma denemesi; AppImage için temiz bir Ubuntu koşucusunda açılış.
  - Dokunacağı yer: yeni packaging/flatpak/, packaging/appimage/, .github/workflows/release.yml, src/VidShrink.Core/UpdateCheck.cs, install-vidshrink.sh
  - Sahibin kararı: Flathub'a uygulama başvurusu yapılsın mı (sahibin hesabından; ffmpeg ve libmpv paketin içinde gelir) ve yayına AppImage eklensin mi.
- **DAG-P9 — Tam Paket Ve Çevrimdışı Kurucu.** `release.yml` her hedef için uygulama, ffmpeg ve libmpv'yi bir arada taşıyan ikinci bir arşiv üretir; taşınabilir kip (komşu tabloda AYR-05), uygulama klasöründe bir işaret dosyası görünce ayarı yanına yazar ve kendini güncellemeyi kapatır. Çevrimdışı kurucu aynı arşivi kurucunun var olan `--asset-source` yoluyla kullanır. macOS ve Linux betiği ffmpeg yoksa durmak yerine sha256'sı sabitlenmiş derlemeyi indirir. Doğrulama: ağ kapalıyken kurulum ve ilk küçültme, taşınabilir kopyada kullanıcı klasörüne dosya yazılmadığının testi.
  - Dokunacağı yer: .github/workflows/release.yml, src/VidShrink.Core/UpdateCheck.cs, src/VidShrink.Setup/Program.cs, src/VidShrink.Core/Setup/SetupDownloads.cs, install-vidshrink.sh, docs/kurulum.md
  - Sahibin kararı: ffmpeg ve libmpv ikilileri kendi yayın dosyalarımızın içinde dağıtılsın mı (yüzlerce MB ek boyut ve lisans yükümlülüğü).
- **DAG-P10 — macOS İmza, Dmg Ve Homebrew.** `macos-app-bundle.sh` ad-hoc imza yerine Developer ID ile imzalar ve paketi noterlemeye gönderir (noterleme: Apple'ın paketi tarayıp onay damgası vermesi); libmpv ve öteki kitaplıklar da imzalanır. İmzalı paketten sürükle bırak dmg üretilir ve yayına eklenir. Homebrew ana dizini en az 75 yıldız, sahibin kendi başvurusunda 225 yıldız arıyor ve bizde 12 var; bu yüzden cask (Homebrew'un hazır uygulama tarifi) kendi tap depomuzda (projeye ait Homebrew paket deposu) yayımlanır ve `release.yml` her sürümde sürüm ile sha256'yı tazeler. Doğrulama: CI'da Gatekeeper ve damga denetimi, temiz Mac'te tarayıcıdan indirip çift tıkla açma elle.
  - Dokunacağı yer: macos-app-bundle.sh, .github/workflows/release.yml, install-vidshrink.sh, src/VidShrink.Core/UpdateCheck.cs; depo dışında tap deposu
  - Sahibin kararı: ücretli Apple geliştirici üyeliği alınsın mı; alınırsa dmg yayımlanır ve Homebrew için kendi tap depomuz açılır.
- **DAG-P11 — Microsoft Store Paketi.** Mağazanın exe yolu imzalı, sessiz ve çevrimdışı kurucu ister; bizim kurucu indirici olduğu için o yol DAG-P6, DAG-P1 ve DAG-P9 bitmeden kapalı. MSIX yolunda paketi Microsoft imzalar: uygulama, ffmpeg ve libmpv tek pakete girer, dosya ilişkisi ve sağ tık menüsü paket bildirimine taşınır, kurulum kökü yazılamaz olduğu için kendini güncelleme kapatılır. Önce DAG-P9 kararı gerekir. Doğrulama: paket sertifika aracı, mağaza ön incelemesi ve yerelde kurup beş sekmenin açılışı.
  - Dokunacağı yer: yeni packaging/msix/, .github/workflows/release.yml, src/VidShrink.Core/UpdateCheck.cs, src/VidShrink.Core/Setup/
  - Sahibin kararı: Microsoft Store geliştirici hesabı açılsın mı ve uygulama MSIX olarak mağazaya girsin mi.
- **DAG-P12 — Betik Ve Eklenti Arabirimi.** DAG-P4'ten önce başlanmaz: dış süreçten gelen komut kanalı, eklenti isteklerinin kumanda ve otomasyon kısmını bağımlılık eklemeden karşılar. Evet denirse ilk adım dar tutulur: kullanıcı klasöründeki betikler oynatıcı ve kuyruk olaylarına abone olur ve komut kanalındaki komutları çağırır; arayüze parça ekleyen eklenti sonraya kalır. Betik dili ve motoru kararın parçasıdır. Doğrulama: örnek betikle olay ve komut testi, betik hatasının uygulamayı düşürmediğinin testi.
  - Dokunacağı yer: src/VidShrink.Core/, src/VidShrink.App/, yeni docs/ sayfası
  - Sahibin kararı: uygulamaya betik motoru (yeni bağımlılık) eklensin ve sürümler arası korunacak bir eklenti arabirimi sözü verilsin mi.

Pakete girmeyenler:

- DAG-13 — Snap mağaza hesabı ister; aynı kitleyi DAG-P8 karşılar.
- DAG-14 — tek rakipte belgeli, yeni ikili dağıtımı; DAG-P8 çıkınca gereği azalır.
- DAG-15 — ücretli Apple hesabı ve mağaza kuralları ister; DAG-P10 bitmeden başlanamaz.
- DAG-18 — dar kitle; makine geneli kopyada güncellemenin nasıl davranacağı önce karar ister.
- DAG-21 — DAG-P3 ile aynı bileşen ama ayrı derleme hedefi ve ayrı kurulum kolu; bedavaya gelmiyor, DAG-P3'ten sonra ucuzlar.
- DAG-22 — dar kitle, yeni ikili dağıtımı.
- DAG-23 — kapalı karar (macOS alt sürümü 14).
- DAG-25 — karar istemeyen tek Linux işi, ama hiçbir paketin dosyasına denk gelmiyor; DAG-P8 onaylanmazsa öne alınmalı.
- DAG-26 — yeni bağımlılık ister; bugünkü tek paket yolu çalışıyor.
- DAG-27 — imza anahtarını saklamak sahibin işi; köken kaydı adımı (Ek Notlar) anahtarsız küçük parçası.
- DAG-32 — önce arayüzde kuyruğu dosyaya kaydeden yol gerekir; iş iki parça.
- DAG-38 — resmi kaynakta tek rakip; ffmpeg'li görüntü DAG-P9 kararına bağlı.
- DAG-39 — DAG-P4 bitmeden başlanamaz; hazır bağlantı ağdan dinleyen kanal ve ayrı yayın ister.
- DAG-40 — dar kitle; hiçbir paketin dosyasına denk gelmiyor.
- DAG-41, DAG-42 — rakip belgesi bulunamadı, öncelik 3.

Kanıt: [doğrulanmış tablo](piyasa/2026-10-13/dagitim-otomasyon-dogrulanmis.md) · [ham tablo](piyasa/2026-10-13/dagitim-otomasyon-eksikler.md) · [notlar ve kaynaklar](piyasa/2026-10-13/dagitim-otomasyon.md)

### Bizde Olmayan Araçlar

#### Yeni Araç Adayları (ARC)

44 eksik yazıldı, doğrulamada 0 satır çıktı, 44 satır kaldı. Öncelik 1, 2, 3: 7, 19, 18.
23 satır sahibin kararını bekliyor, 5 satır kapalı bir karara denk geliyor. 12 paket önerildi.

- Bizde "Araçlar" diye bir sekme yok. 44 aracın 19'u yarım, 25'i hiç yok; tam olan yok.
- Rakip paketlere göre geride olduğumuz yer motor değil, ekran ve kaynak kabulü. Dosya okuma, kalite ölçme, sessizlik ve sahne taraması, bölüm yazma, akış kopyası ve gürültü süzgeci kodda duruyor; kullanıcıya açılmamış.
- 16 satır karar beklemeden var olan sekmelere girer. 23 satır sahibin kararını bekler: 18'i paketlenen beş soruya, 5'i kendi tekil sorusuna bağlı. 5 satır önceki kararlarla kapalı.
- En ağır eksik: Dönüştür'ün video olmayan kaynağı almaması (ses dosyası ARC-19, GIF ARC-12, resim dizisi ARC-09). Taranan dönüştürücü paketlerin temel işi.
- İkinci ağır eksik: yapay zekâ araçları (ARC-25, ARC-27, ARC-29, ARC-30, ARC-31). Rakiplerin son iki yılda en çok eklediği şey; bizde yeni çalışma zamanı ve büyük indirme ister.
- Üçüncü ağır eksik: etiket ve bölüm düzenleme (ARC-14, ARC-15). HandBrake takipçisinde 67 oy almış bir istek.
- En ucuz üç kazanç: dosyada gürültü temizleme (ARC-24, süzgeç kaydedicide hazır), tam medya bilgisi dökümü (ARC-01, veri zaten okunuyor), denetim raporu (ARC-05, taramalar düzenleyicide koşuyor).
- Sahibin beş sorusu: Araçlar sekmesi, yapay zekâ çalışma zamanı, kendi sekmesini isteyen araçlar, altyazı çevirisi, logo silme.

| Paket | Ad | Satırlar | İş | Öncelik | Karar | Dalga |
|---|---|---|---|---|---|---|
| ARC-P1 | Dosyada Gürültü Temizleme Ve Yükseklik Raporu | ARC-24, ARC-04 | küçük | 1 | gerekmez | 1 |
| ARC-P2 | Medya Bilgisi Ve Denetim Raporu | ARC-01, ARC-05 | orta | 1 | gerekmez | 2 |
| ARC-P3 | Ses Dosyası Ve GIF Girdisi | ARC-19, ARC-12 | orta | 1 | gerekmez | 2 |
| ARC-P4 | Resim Dizisi Girdisi Ve Ara Kare | ARC-09, ARC-13, ARC-28 | orta | 1 | gerekmez | 2 |
| ARC-P5 | Etiket, Bölüm Ve Onarma | ARC-14, ARC-15, ARC-16 | büyük | 1 | gerekmez | 3 |
| ARC-P6 | Dosya Dönüştürme Ve Ölçüm Araçları | ARC-21, ARC-11, ARC-02, ARC-06 | büyük | 1 | sahip | K |
| ARC-P7 | Oynatıcıdan Kare Ve Tablo Çıkarma | ARC-08, ARC-07 | orta | 2 | gerekmez | 3 |
| ARC-P8 | Altyazı Çevirisi | ARC-23 | orta | 2 | sahip | K |
| ARC-P9 | Logo Ve Damga Silme | ARC-33 | orta | 2 | sahip | K |
| ARC-P10 | Slayt Gösterisi Ve Sesten Video | ARC-10, ARC-20 | büyük | 2 | gerekmez | 4 |
| ARC-P11 | Yapay Zekâ Araçları | ARC-25, ARC-27, ARC-29, ARC-30, ARC-31 | büyük | 2 | sahip | K |
| ARC-P12 | Kendi Sekmesini İsteyen Araçlar | ARC-18, ARC-22 | büyük | 2 | sahip | K |

- **ARC-P1 — Dosyada Gürültü Temizleme Ve Yükseklik Raporu.** Kaydedicinin kullandığı gürültü süzgeci (afftdn, AudioCaptureArguments.cs:73) Küçült'ün ses bölümüne, yükseklik kutusunun yanına ikinci kutu olarak bağlanır; komut satırına eş bayrak eklenir. Aynı bölümde kaynağın yüksekliği ebur128 süzgeciyle bir kez ölçülüp LUFS ve gerçek tepe olarak gösterilir. Doğrulama: IzPaneliTests'teki gibi kutu açıkken süzgecin argümanda geçtiği, kapalıyken hiç yazılmadığı sınanır; canlı kolda gürültülü kısa bir örnekte gürültü tabanının düştüğü ölçülür.
  - Dokunacağı yer: src/VidShrink.Core/StreamMapping.cs, src/VidShrink.Core/AudioCaptureArguments.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.Izler.cs, src/VidShrink.Cli/CliRequest.cs, src/VidShrink.App/Locales/
- **ARC-P2 — Medya Bilgisi Ve Denetim Raporu.** Küçült'ün kaynak paneline ve oynatıcının bilgi katmanına "tüm ayrıntılar" düğmesi eklenir; açılan pencere ffprobe'un zaten okuduğu akışları ve bölümleri tek tek gösterir, "kopyala" ve "metin olarak kaydet" verir. Aynı pencerenin ikinci bölümü düzenleyicinin sessizlik, siyah kare ve sahne taramasını çağırıp sonucu zaman kodlu liste olarak verir; düzenleyicinin tarama paneline de "listeyi kaydet" eklenir. Doğrulama: StreamMappingTests'in ürettiği çok izli, bölümlü 3 saniyelik dosyada dökümün her izi ve bölümü içerdiği; sahte tarayıcıyla listenin tarama sonucuyla birebir aynı olduğu sınanır.
  - Dokunacağı yer: src/VidShrink.Core/MediaInfo.cs, src/VidShrink.Ffmpeg/FfprobeClient.cs, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/Playback/PlayerView.Window.cs, src/VidShrink.Core/Editing/SilenceCut.cs, src/VidShrink.App/Editing/EditorView.Sessizlik.cs
- **ARC-P3 — Ses Dosyası Ve GIF Girdisi.** Dosya okuyucusu görüntüsü olmayan dosyayı reddetmek yerine "yalnız ses" kaynağı olarak tanır (FfprobeClient.cs:45); Dönüştür bu kaynakta kap listesini ses kaplarına daraltır, Küçült almamaya devam eder. GIF bırakmada ve klasör taramasında da kabul edilir; kaynağı GIF olan işte GIF çıktısı kare hızı, genişlik ve renk sayısı düşürülerek hedef boyutun altına indirilir. Doğrulama: ffmpeg'in ürettiği mp3, flac, wav ve GIF örnekleriyle canlı kol; ses kaynağında video kabının seçilemediği, GIF çıktısının hedefin altında kaldığı ölçülür.
  - Dokunacağı yer: src/VidShrink.Ffmpeg/FfprobeClient.cs, src/VidShrink.Core/WatchFolder.cs, src/VidShrink.Core/DroppedMedia.cs, src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Core/GifPalette.cs, src/VidShrink.App/MainWindow.axaml.cs
- **ARC-P4 — Resim Dizisi Girdisi Ve Ara Kare.** Üçü de Dönüştür'ün kare hızı yoluna dokunur. Bir klasör ya da numaralı resimler bırakılınca dosyalar ada göre sıralanıp ffmpeg'in resim dizisi okuyucusuyla tek kaynak sayılır; kare hızı kutusu dizinin hızını belirler, GIF kabı seçilince resimlerden GIF de aynı yoldan çıkar. Kare hızı kutusuna "ara kare üret" seçeneği eklenir (minterpolate süzgeci; yapıda yoksa seçenek gizlenir). Doğrulama: üretilmiş 30 karelik PNG dizisinde çıktının kare sayısı ve süresi beklenene eşit; ara karede çıktının kare sayısı hedef hıza uyar.
  - Dokunacağı yer: src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Core/DroppedMedia.cs, src/VidShrink.Core/VideoFilterChain.cs, src/VidShrink.App/MainWindow.axaml
- **ARC-P5 — Etiket, Bölüm Ve Onarma.** Üçü de Dönüştür'de "Kopyala" seçiliyken, yani yeniden kodlamadan yapılır; sırayla ayrı ayrı teslim edilebilir. Etiket için "Meta veriyi sil" kutusunun yanına başlık, sanatçı, yıl, açıklama ve kapak alanları gelir; bölüm için kaydedicinin bölüm yazan yolu ortak hâle getirilir ve kaynağın bölümleri eklenip adlandırılıp silinebilen liste olur; onarma için akışları kopyalayan, bozuk zaman damgasını hoş gören bayraklarla "Onar" hazır ayarı eklenir. Doğrulama: MetaSilDonusturTests ve KayitBolumIsaretiTests'teki gibi ffprobe ile etiket ve bölüm okunur; boş alan anahtarı hiç yazmaz.
  - Dokunacağı yer: src/VidShrink.Core/RecorderChapters.cs, src/VidShrink.Core/StreamMapping.cs, src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.Cli/CliRequest.cs
- **ARC-P6 — Dosya Dönüştürme Ve Ölçüm Araçları.** Dört araç da aynı kalıpta (dosya seç, ayarla, çalıştır) ve iki ayrı teslime bölünür: dosya dönüştürme (ARC-21, ARC-11) ve ölçüm (ARC-02, ARC-06). Dönüştürmede ffmpeg altyazıyı SRT, ASS ve VTT arasında çevirir, bütün satırlar verilen süre kadar kaydırılır, kodlama var olan okuyucuyla (SubtitleCharset) UTF-8'e alınır, oynatıcıdaki gecikme "dosyaya yaz" ile aynı yola bağlanır; fotoğrafta ffmpeg'in resim kodlayıcılarıyla toplu biçim ve boyut değişimi yapılır. Ölçümde motor hazır (`QualityMeter.MeasureAsync` iki serbest yol alıyor): iki dosyanın seçildiği ekran puanları ve kare kare VMAF grafiğini gösterir, yanına kare özetlerini karşılaştıran "birebir aynı mı" denetimi (framemd5) gelir, komut satırına Bench'teki `measure` komutunun eşi eklenir. Sahip "en yakın sekme" derse dönüştürme Dönüştür'e yeni kaynak türü, ölçüm Küçült'ün karşılaştırma paneline "başka dosya seç" olarak girer; doğrulama: altyazıda gidiş dönüşte satır sayısı ve zamanlar korunur, aynı dosya kendisiyle ölçülünce en yüksek puan ve eşit kare özeti çıkar.
  - Dokunacağı yer: src/VidShrink.Core/SubtitleCharset.cs, src/VidShrink.Core/StreamMapping.cs, src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Ffmpeg/QualityMeter.cs, src/VidShrink.App/Playback/ComparisonPanel.axaml.cs, src/VidShrink.Cli/CliRequest.cs, src/VidShrink.App/MainWindow.axaml
  - Sahibin kararı: Araçlar sekmesi açılsın mı, yoksa en yakın sekmeye mi konsun
- **ARC-P7 — Oynatıcıdan Kare Ve Tablo Çıkarma.** Oynatıcının araç komutlarına (klip ve GIF dışa aktarmanın yanına) iki komut eklenir. "Kareleri dışa aktar": her N saniyede ya da A-B aralığında kareler numaralı resim olarak yazılır. "Küçük resim tablosu": eşit aralıklı kareler ffmpeg'in tile süzgeciyle tek resme dizilir, üstüne dosya bilgisi başlığı konur. Doğrulama: kısa üretilmiş örnekte yazılan dosya sayısı beklenene eşit, tablonun boyutu satır, sütun ve kare boyutunun çarpımına eşit.
  - Dokunacağı yer: src/VidShrink.App/Playback/ToolsOptions.cs, src/VidShrink.App/Playback/PlayerView.Tools.cs, src/VidShrink.Core/GifPalette.cs, src/VidShrink.App/Locales/
- **ARC-P8 — Altyazı Çevirisi.** Altyazı dosyası satır satır okunur, metin seçilen çeviri hizmetine gönderilir, zamanlar korunarak yeni dosya yazılır. Hizmet seçimi ve anahtar kullanıcıdan alınır; gönderilen şey yalnız altyazı metnidir ve bu ekranda açıkça yazılır. Sahip yerel dil modelini seçerse iş ARC-P11'in ortak zeminine (çalışma zamanı ve model indirme) bağlanır. Doğrulama: sahte hizmetle satır sayısının ve zamanların değişmediği, biçem etiketlerinin bozulmadığı sınanır.
  - Dokunacağı yer: src/VidShrink.Core/Subtitles/, src/VidShrink.App/MainWindow.axaml.cs
  - Sahibin kararı: altyazı metnini çevrimiçi çeviri hizmetine ya da yerel dil modeline veren araç ürüne girsin mi
- **ARC-P9 — Logo Ve Damga Silme.** Küçült'ün gelişmiş görüntü süzgeçlerine "bölge kapat" eklenir: kullanıcı önizlemede bir dikdörtgen çizer, ffmpeg'in delogo süzgeci o bölgeyi çevresinden doldurur. Kendiliğinden logo bulma yapılmaz. Doğrulama: argüman sınaması ve üretilmiş örnekte bölge dışındaki piksellerin değişmediği.
  - Dokunacağı yer: src/VidShrink.Core/VideoFilterChain.cs, src/VidShrink.App/MainWindow.axaml
  - Sahibin kararı: başkasının logosunu ya da filigranını silmeye yarayan araç ürüne girsin mi
- **ARC-P10 — Slayt Gösterisi Ve Sesten Video.** İkisi de video olmayan malzemeden video üretir. Sesten video ARC-P3'ün üstüne gelir: ses kaynağında Dönüştür'e "görüntü" seçeneği eklenir, kapak resmi ya da dalga biçimi (showwaves süzgeci). Slayt gösterisi düzenleyicinin üç parçasına bağlıdır: resim klibi (DZB-04), müzik izi (DZB-01) ve çapraz geçiş (DZB-16); onlar bitince kalan iş fotoğrafları seçip süre vererek çizelgeye dizen kısa bir sihirbazdır ve iş orta'ya iner. Doğrulama: çıktının süresi ses süresine ya da fotoğraf sayısı çarpı süreye eşit.
  - Dokunacağı yer: src/VidShrink.Core/Editing/, src/VidShrink.App/Editing/, src/VidShrink.Core/ConversionArguments.cs
- **ARC-P11 — Yapay Zekâ Araçları.** Önce ortak zemin: modeli koşturan çalışma zamanı ve model dosyalarını ilk kullanımda sha256'lı indiren yol (libmpv'nin indiği yolun eşi). Sonra araç araç, her biri ayrı teslim: büyütme (ARC-27) pilot olur, ardından vokal ayırma (ARC-25), arka plan silme (ARC-29), yeniden kadraj (ARC-30) ve yüz bulanıklaştırma (ARC-31). Sahip "evet" derse bu paket beş pakete bölünür. Doğrulama: model yokken aracın görünmediği, indirme özetinin tuttuğu, kısa örnekte çıktı çözünürlüğünün beklenene eşit olduğu.
  - Dokunacağı yer: src/VidShrink.Core/Setup/, src/VidShrink.Ffmpeg/, src/VidShrink.Core/ConversionArguments.cs, Install-VidShrink.ps1
  - Sahibin kararı: yapay zekâ araçları için yeni çalışma zamanı ve yüzlerce MB model indirmesi kabul edilsin mi
- **ARC-P12 — Kendi Sekmesini İsteyen Araçlar.** Yinelenen video bulucu: klasördeki her videodan birkaç karenin özeti ffmpeg ile çıkarılır, yakın özetler gruplanır, kullanıcı hangisini sileceğini seçer. Altyazı düzenleyici: satır listesi, metin ve zaman düzenleme, düzenleyicinin dalga biçimi verisi üstünde hizalama. Sahip "evet" derse iki pakete bölünür; altyazı düzenleyicinin hafif hali DZB-62'de zaten yazılı. Doğrulama: aynı videonun yeniden kodlanmış kopyası aynı gruba düşer; düzenlenen altyazı geri okununca aynı satırları verir.
  - Dokunacağı yer: src/VidShrink.App/MainWindow.axaml, src/VidShrink.Core/Subtitles/, src/VidShrink.Core/Editing/AudioPeaks.cs
  - Sahibin kararı: kendi sekmesini isteyen araç ürüne girsin mi

Pakete girmeyenler:

- ARC-03: öncelik 3; kendi çizim ekranını ister, hiçbir paketin yanında bedavaya gelmiyor.
- ARC-35: öncelik 3; küçük ama kendi ekranını ister ve Araçlar sorusuna bağlı.
- ARC-17: öncelik 3; ayrı bir ikili dosyanın dağıtımını ister.
- ARC-32, ARC-34: öncelik 3; yapay zekâ kararına bağlı ve dar kitleye hitap ediyor.
- ARC-26, ARC-39: öncelik 3; bulut hizmetine ya da ses modeline dayanıyor.
- ARC-36, ARC-37, ARC-38: öncelik 3; ürünün beş işinin dışında kalıyor.
- ARC-40, ARC-41, ARC-42, ARC-43, ARC-44: önceki kararlarla kapalı (K1, K5, K8, K8, DVD ve Blu-ray).

Kanıt: [doğrulanmış tablo](piyasa/2026-10-13/eksik-araclar-dogrulanmis.md) · [ham tablo](piyasa/2026-10-13/eksik-araclar-eksikler.md) · [notlar ve kaynaklar](piyasa/2026-10-13/eksik-araclar.md)

### Platform Eşitliği

#### Windows'ta Olup macOS Ya Da Linux'ta Eksik Kalanlar (PLT)

44 eksik yazıldı, doğrulamada 0 satır çıktı, 44 satır kaldı. Öncelik 1, 2, 3: 3, 19, 22.
20 satır sahibin kararını bekliyor, 0 satır kapalı bir karara denk geliyor. 9 paket önerildi.

- Windows'ta kaydedici ve kabuk bütünleşmesi tam. macOS ve Linux'ta dönüştürme, küçültme ve oynatma çalışıyor; kaydedici ise yarım.
- Rakipler (OBS Studio, CleanShot X, Screen Studio, Kooha, GPU Screen Recorder) bizde eksik olan üç şeyi yerleşik veriyor: Wayland yakalama, Mac'te sürücüsüz sistem sesi, kamera katmanı.
- En ağır üç eksik: PLT-02 Wayland'da kaydedici hiç çalışmıyor; PLT-03 Mac'te sistem sesi yok; PLT-01 kameralı Mac'te ekran yerine kamera kaydediliyor olabilir.
- PLT-01 küçük iş ama ana işi kırıyor: bugün her dizüstü Mac'te kamera var. Gerçek Mac'te ölçülmedi.
- En ucuz üç kazanç: PLT-01 (tek fonksiyon), PLT-14 (dört kutuyu gizlemek), PLT-10 (ffmpeg'in hazır seçeneği). Üçü de karar istemiyor.
- Sahibin kararını bekleyen işler üç kümede toplanıyor: ölçüm için Mac ya da Linux makinesi (PLT-37, 38, 41, 43), macOS izin istemleri (PLT-04, 06, 11, 35), yeni dağıtım kanalı (PLT-22, 23, 24).
- 44 satırın 24'ü komşu kollarla ortak; bu kolun kendi paketi dokuz, kalanını komşu paketler kapsıyor.

| Paket | Ad | Satırlar | İş | Öncelik | Karar | Dalga |
|---|---|---|---|---|---|---|
| PLT-P1 | macOS Ekran Seçimi Adla | PLT-01 | küçük | 1 | gerekmez | 1 |
| PLT-P2 | İşlemeyen Kutuları Gizleme | PLT-14 | küçük | 2 | gerekmez | 2 |
| PLT-P3 | macOS Tıklama İşareti | PLT-10 | küçük | 2 | gerekmez | 2 |
| PLT-P4 | macOS İzin Akışı | PLT-04 | orta | 2 | sahip | K |
| PLT-P5 | macOS Kapanışta Sessiz Güncelleme | PLT-21 | orta | 2 | sahip | K |
| PLT-P6 | Linux'ta Kendini Güncelleme | PLT-20 | orta | 2 | sahip | K |
| PLT-P7 | VideoToolbox Hızlı Kip Kapısı | PLT-37 | orta | 2 | sahip | K |
| PLT-P8 | Linux Ölçüm Turu: VAAPI Ve Sistem Sesi | PLT-38, PLT-43 | orta | 2 | sahip | K |
| PLT-P9 | Wayland'da Genel Kısayol | PLT-08 | orta | 2 | sahip | K |

- **PLT-P1 — macOS Ekran Seçimi Adla.** `MacInput` ekranı sıra numarasıyla değil `Capture screen N:none` adıyla verir; ffmpeg kaynağı bu adı tanır ve N kamera sayısından bağımsızdır. Bölge ve pencere hedefi de aynı girdiden geçtiği için üç hedef birden düzelir. Argüman testleri ekran, bölge ve pencere hedefinde girdi metnini pimler. Kameralı ve çok ekranlı Mac'te gerçek kayıt bu makinede ölçülemez; belgeye ölçülmedi diye yazılır.
  - Dokunacağı yer: src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.App/Recorder/RecorderView.Secici.cs, src/VidShrink.App/Recorder/RecorderView.Hedef.cs, tests/VidShrink.Tests
- **PLT-P2 — İşlemeyen Kutuları Gizleme.** Tıklama, tıklama sesi, tuş ve büyüteç kutuları kamera panelindeki gibi platform kapısına bağlanır ve Windows dışında gizlenir; hem ana görünümde hem mini şeritte. Kayıtlı ayarda açık gelen seçenek Windows dışında isteğe taşınmaz. Başsız arayüz testi, kapı kapalıyken dört kutunun da görünmediğini ölçer.
  - Dokunacağı yer: src/VidShrink.App/Recorder/RecorderView.axaml, src/VidShrink.App/Recorder/RecorderMini.axaml, src/VidShrink.App/Recorder/RecorderView.Girdi.cs, tests/VidShrink.Tests
- **PLT-P3 — macOS Tıklama İşareti.** Mac'te tıklamaları göster kutusu ffmpeg'in `-capture_mouse_clicks 1` seçeneğine bağlanır; ek izin ve yardımcı ikili gerekmez. PLT-P1 ile aynı fonksiyona dokunur ve PLT-P2'nin gizlediği kutuyu yalnız Mac'te geri açar, bu yüzden ikisinden sonra gelir. Argüman testiyle doğrulanır; işaretin görünümü gerçek Mac'te görülmedi. EKB-P17 açılırsa bu paket onun macOS tıklama kolunu karşılamış olur.
  - Dokunacağı yer: src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.App/Recorder/RecorderView.Girdi.cs, src/VidShrink.App/Recorder/RecorderView.axaml, tests/VidShrink.Tests
- **PLT-P4 — macOS İzin Akışı.** Paketin Info.plist dosyasına (macOS paketinin kimlik ve izin bildirimi) mikrofon açıklaması eklenir; kamera paketi açılırsa kamera açıklaması da buraya gelir. Kayıt başlamadan Ekran Kaydı izni yoklanır, eksikse kayıt başlamaz ve Sistem Ayarları'nın ilgili sayfasına götüren hata satırı çıkar. EKA-P9, EKA-P12 ve EKA-P13'ün izin anahtarları da bu pakette toplanır. Paket içeriği testle, izin akışı yalnız gerçek Mac'te doğrulanır.
  - Dokunacağı yer: macos-app-bundle.sh, src/VidShrink.App/Recorder/, src/VidShrink.App/Locales/
  - Sahibin kararı: pakete izin açıklamaları eklenip kayıttan önce izin yoklaması gösterilsin mi
- **PLT-P5 — macOS Kapanışta Sessiz Güncelleme.** Elle Yükle'nin kullandığı paket takası, `MacUpdate.CanSwap` doğruysa kapanış yoluna da bağlanır: sahne açılış bittikten sonra sessiz iner, çıkışta takas edilir. Her düşüş `UpdateHealth` sayacına yazılır; sayılmayan sessiz yol eklenmez. Düz kurulumda ve salt okunur yola taşınmış pakette kapalı kalır; Sparkle eklenmez. Birim testleri ve gerçek Mac'te bir sürüm atlamasıyla doğrulanır.
  - Dokunacağı yer: src/VidShrink.App/MainWindow.KapanistaGuncelleme.cs, src/VidShrink.Core/UpdateCheck.cs, src/VidShrink.Core/UpdateHealth.cs
  - Sahibin kararı: macOS'ta otomatik güncelleme varsayılan olarak açılsın mı
- **PLT-P6 — Linux'ta Kendini Güncelleme.** Kullanıcıya ait `~/.local/share/vidshrink` kurulumunda yerinde takas açılır; yazılamayan yolda kapalı kalır ve bugünkü gibi yalnız haber verilir. Linux için güncelleme paketi bugün üretilmiyor (release.yml:492-500); ya yayına Linux paketi eklenir ya da uygulama arşivi kullanılır. DAG-25 ile aynı iş, DAG paketlerinde yeri yok; DAG-P8 (Flatpak ya da AppImage) onaylanırsa güncellemeyi mağaza üstlenir ve bu paket yalnız betikle kurulanlara kalır. Takas, geri alma ve düşüş sayımı testle ölçülür.
  - Dokunacağı yer: src/VidShrink.Core/UpdateCheck.cs, src/VidShrink.Core/InPlaceUpdate.cs, src/VidShrink.App/YerindeGuncelleme.cs, .github/workflows/release.yml, docs/kurulum.md
  - Sahibin kararı: Linux'ta bilerek kapalı tutulan uygulama içi güncelleme açılsın mı
- **PLT-P7 — VideoToolbox Hızlı Kip Kapısı.** Önceki ölçüm beş ölçütün ikisinden kaldı (docs/olcumler/videotoolbox-hizli.md:60, 62); kapı değişmeden VideoToolbox'a kalite ölçeği eklenir ve ölçüm gerçek Mac'te yinelenir. Geçerse `hevc_videotoolbox` yalnız macOS'ta ve yalnız Hızlı kipte otomatik sıraya girer; geçmezse satır kapanır. Rapora giren her sayı Bench'ten (ölçüm aracımız) çıkar.
  - Dokunacağı yer: src/VidShrink.Core/CodecModel.cs, src/VidShrink.Core/PlanCalculator.cs, tools/VidShrink.Bench, docs/olcumler/videotoolbox-hizli.md
  - Sahibin kararı: ölçüm için Mac ayrılsın mı ve kapıdan geçerse Hızlı kipin Mac'teki varsayılan kodlayıcısı değişsin mi
- **PLT-P8 — Linux Ölçüm Turu: VAAPI Ve Sistem Sesi.** Aynı Linux makinesinde iki ölçüm yapılır: VAAPI kolu açık seçimle koşturulup hız ve kalite Bench'le ölçülür; pulse `.monitor` kaynağından kısa kayıt alınıp ses seviyesi ölçülür. Sonuç, VAAPI'nin otomatik sıraya alınıp alınmayacağını ve README'deki "sistem sesi yalnız Windows'ta" cümlesini belirler. Defterdeki VAAPI satırı bu turla kapanır.
  - Dokunacağı yer: tools/VidShrink.Bench, docs/olcumler/, src/VidShrink.Core/CodecModel.cs, src/VidShrink.Ffmpeg/CaptureDevices.cs, README.md
  - Sahibin kararı: ölçüm için Intel ya da AMD ekran kartlı gerçek bir Linux makinesi ayrılsın mı
- **PLT-P9 — Wayland'da Genel Kısayol.** X11 kolu EKA-P10 içinde; bu paket yalnız Wayland kolunu kapsar. Kısayol GlobalShortcuts portalı üzerinden kaydedilir; portalla D-Bus (Linux masaüstünde programların birbirine mesaj yolladığı hat) üzerinden konuşulur. EKA-P14 gelmeden açılmaz, çünkü Wayland'da kaydedicinin kendisi çalışmıyor. Portalı sunan gerçek bir Wayland masaüstünde doğrulanır.
  - Dokunacağı yer: src/VidShrink.App/Recorder/RecorderView.Kisayol.cs, src/VidShrink.App/Recorder/
  - Sahibin kararı: GlobalShortcuts portalı için D-Bus istemcisi bağımlılığı eklensin mi

Pakete girmeyenler:

Komşu kolun paketi kapsıyor:

- PLT-02: EKA-P14 kapsıyor.
- PLT-03: EKA-P12 kapsıyor.
- PLT-05: EKA-P13 kapsıyor.
- PLT-06: EKA-P9 kapsıyor (EKB-P13 aynı iş, yalnız biri açılır).
- PLT-07: EKA-P9 kapsıyor (EKB-P13 aynı iş, yalnız biri açılır).
- PLT-09: EKA-P10 kapsıyor.
- PLT-11: EKB-P17 kapsıyor.
- PLT-12: EKB-P17 kapsıyor.
- PLT-13: EKB-P17 kapsıyor.
- PLT-15: EKA-P4 kapsıyor.
- PLT-16: EKA-P4 kapsıyor.
- PLT-22: DAG-P10 kapsıyor.
- PLT-23: DAG-P8 kapsıyor.
- PLT-24: DAG-P9 kapsıyor.
- PLT-25: DAG-P9 kapsıyor.
- PLT-30: AYR-P4 kapsıyor.
- PLT-35: AYR-P5 kapsıyor.
- PLT-36: AYR-P5 kapsıyor.

Öncelik 3, hiçbir paketin yanında bedavaya gelmiyor:

- PLT-17: Tek rakipte var; pencere biçimi için sisteme özgü kod ister.
- PLT-18: EKA-P12'nin yardımcı ikilisi gelmeden yapılamaz.
- PLT-19: Ayrı dosya; EKA-P4 kare hızına dokunurken ucuzlar.
- PLT-26: DAG-41 ile aynı; DAG paketlerinde de yeri yok, rakip kanıtı yok.
- PLT-27: DAG-42 ile aynı; DAG paketlerinde de yeri yok, rakip kanıtı yok.
- PLT-28: Rakip kanıtı yok, dar kitle.
- PLT-29: Rakip kanıtı yok, dar kitle.
- PLT-31: Yeni izin istemi kararı ister, dar kitle.
- PLT-32: Yeni bağımlılık kararı ister, dar kitle.
- PLT-33: Küçük iş ama ayrı dosyalar (Platform.cs, QueueEndActions.cs); PLT-34 ile birlikte tek seferde yapılır.
- PLT-34: Küçük iş ama ayrı dosyalar (Platform.cs, QueueEndActions.cs); PLT-33 ile birlikte tek seferde yapılır.
- PLT-39: Ayrı dosya (Ffmpeg/ChildJob.cs), rakip ölçütü yok.
- PLT-40: EKA-P4 aynı kodlayıcı listesine dokunurken ucuzlar.
- PLT-41: Ölçülecek ARM donanımı gerekir.
- PLT-42: Tek satırlık iş; DAG-P9 kurucuya dokunurken eklenmeli.
- PLT-44: DAG-21 ile aynı; DAG-P3'ten sonra ucuzlar.

Kanıt: [doğrulanmış tablo](piyasa/2026-10-13/platform-esitligi-dogrulanmis.md) · [ham tablo](piyasa/2026-10-13/platform-esitligi-eksikler.md) · [notlar ve kaynaklar](piyasa/2026-10-13/platform-esitligi.md)

## İkinci Aşama Dalga Planı

Dalga, paketin kararına, işine ve içindeki en yüksek önceliğe göre betikle atandı; yargı değil kuraldır.
Karar beklemeyen iş önce gelir, çünkü senin cevabını beklemeden başlayabilir.

**Tur**, aynı anda ayrı işçilere verilebilecek paket kümesidir: dokunacakları yerler kesişmiyor.
Kesişme hesabında çeviri dosyaları ve testler sayılmadı; bir turda en çok 6 paket var.

### Dalga 1 — Karar Gerekmez, Küçük İş, Öncelik 1

| Tur | Paket | Sekme | Ad | İş | Öncelik | Satır | Dokunacağı Yer |
|---|---|---|---|---|---|---|---|
| 1.1 | OYA-P1 | Oynatıcı | Bildirim, Ses Düzeyi Ve Bölüm Adları | küçük | 1 | 3 | src/VidShrink.App/Playback/PlayerView.axaml.cs, src/VidShrink.App/Playback/PlayerView.Tracks.cs, src/VidShrink.App/Playback/PlayerView.Gosterge.cs, src/VidShrink.App/Playback/PlayerSettings.cs, src/VidShrink.App/Playback/PlayerView.Window.cs, src/VidShrink.Player/MpvEngine.cs, src/VidShrink.Player/IPlaybackEngine.cs |
| 1.1 | OYB-P1 | Oynatıcı | HLS Ve Liste Adresi Düzeltmesi | küçük | 1 | 1 | src/VidShrink.App/Playback/PlayerView.Kuyruk.cs, src/VidShrink.Core/PlaylistFile.cs, tests/VidShrink.Tests/OynaticiAdresTests.cs |
| 1.1 | DZA-P1 | Düzenleyici | Hızlı Kip Kayma Bildirimi | küçük | 1 | 3 | src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.App/Editing/EditorView.Teslim.cs, src/VidShrink.App/Editing/EditorKeymap.cs, src/VidShrink.App/Editing/EditorView.Kisayol.cs, src/VidShrink.App/Editing/EditorView.Monitor.cs, src/VidShrink.App/Locales/ |
| 1.1 | DZB-P1 | Düzenleyici | Metin Biçim Ve Süsleme | küçük | 1 | 2 | src/VidShrink.App/Editing/EditorView.axaml, src/VidShrink.App/Editing/EditorView.Metin.cs, src/VidShrink.Core/Editing/TextLayer.cs, src/VidShrink.Core/Editing/AssWriter.cs, src/VidShrink.Core/Editing/EditProject.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/DuzenleyiciMetinTests.cs |
| 1.1 | SIK-P1 | Küçült | Kodlama Süreç Önceliği Ayarı | küçük | 1 | 2 | src/VidShrink.Ffmpeg/EncodeRunner.cs, src/VidShrink.Ffmpeg/FfmpegRunner.cs, src/VidShrink.Core/FfmpegArguments.cs, src/VidShrink.App/MainWindow.axaml (Ayarlar, `CmbParallelJobs` yanı), src/VidShrink.App/AppSettings.cs, src/VidShrink.Cli/CliRequest.cs, src/VidShrink.App/Locales/ |
| 1.1 | PLT-P1 | Platform Eşitliği | macOS Ekran Seçimi Adla | küçük | 1 | 1 | src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.App/Recorder/RecorderView.Secici.cs, src/VidShrink.App/Recorder/RecorderView.Hedef.cs, tests/VidShrink.Tests |
| 1.2 | OYB-P2 | Oynatıcı | Çoklu Bırakma Ve Klasör Açma | küçük | 1 | 2 | src/VidShrink.App/Playback/PlayerView.Window.cs, src/VidShrink.App/Playback/PlayerView.Kuyruk.cs, src/VidShrink.App/Playback/FolderNavigator.cs, src/VidShrink.App/Locales/ |
| 1.2 | DON-P1 | Dönüştür | Dönüştür'de Donanım Kodlayıcıları | küçük | 1 | 1 | src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.DonusturKap.cs, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/ |
| 1.3 | ARC-P1 | Bizde Olmayan Araçlar | Dosyada Gürültü Temizleme Ve Yükseklik Raporu | küçük | 1 | 2 | src/VidShrink.Core/StreamMapping.cs, src/VidShrink.Core/AudioCaptureArguments.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.Izler.cs, src/VidShrink.Cli/CliRequest.cs, src/VidShrink.App/Locales/ |

### Dalga 2 — Karar Gerekmez, Küçük İş Öncelik 2 Ya Da Orta İş Öncelik 1

| Tur | Paket | Sekme | Ad | İş | Öncelik | Satır | Dokunacağı Yer |
|---|---|---|---|---|---|---|---|
| 2.1 | OYA-P2 | Oynatıcı | Altyazı Bulma Ve Tuşları | orta | 1 | 5 | src/VidShrink.App/Playback/SubtitleOptions.cs, src/VidShrink.App/Playback/Keymap.cs, src/VidShrink.App/Playback/PlayerView.Tracks.cs, src/VidShrink.Player/MpvEngine.cs, src/VidShrink.Player/IPlaybackEngine.cs |
| 2.1 | OYB-P3 | Oynatıcı | Liste Penceresinde Arama Ve Sıralama | orta | 1 | 4 | src/VidShrink.App/Playback/PlayerView.Playlist.cs, src/VidShrink.App/Playback/PlayerView.ListeMenusu.cs, src/VidShrink.App/Playback/PlayerView.Kuyruk.cs, src/VidShrink.App/Playback/FolderNavigator.cs, src/VidShrink.App/Locales/ |
| 2.1 | OYB-P4 | Oynatıcı | Geçmiş Penceresi Ve Devam Ayarı | orta | 1 | 3 | src/VidShrink.App/Playback/PlaybackHistory.cs, src/VidShrink.App/Playback/RecentFiles.cs, src/VidShrink.App/Playback/PlayerView.Window.cs, src/VidShrink.App/Playback/PlayerSettings.cs, src/VidShrink.App/Locales/ |
| 2.1 | DZA-P2 | Düzenleyici | Otomatik Bölme Ve Parça Adları | orta | 1 | 5 | src/VidShrink.Core/Editing/EditTimeline.cs, src/VidShrink.Core/Editing/EditClip.cs, src/VidShrink.Core/Editing/EditOutputName.cs, src/VidShrink.Core/Editing/EditProject.cs, src/VidShrink.Core/AdlandirmaDeseni.cs, src/VidShrink.App/Editing/EditorView.Sahne.cs, src/VidShrink.App/Editing/EditorView.axaml |
| 2.1 | DZA-P4 | Düzenleyici | Akıllı Kipte Ses Kodekleri | orta | 1 | 1 | src/VidShrink.Core/Editing/EditExport.cs, tools/VidShrink.Bench |
| 2.1 | SIK-P3 | Küçült | Koşan Kodlamayı Duraklatma | orta | 1 | 2 | src/VidShrink.Ffmpeg/EncodeRunner.cs, src/VidShrink.Ffmpeg/ChildJob.cs, src/VidShrink.Core/JobPump.cs, src/VidShrink.App/ShrinkJobWindow.axaml, src/VidShrink.App/ShrinkJobWindow.axaml.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.Ffmpeg/DiskSpaceGuard.cs, src/VidShrink.App/Locales/ |
| 2.2 | OYA-P3 | Oynatıcı | Uyku Engeli Ve Pencere Davranışı | orta | 1 | 4 | src/VidShrink.App/Playback/PlayerView.axaml.cs, src/VidShrink.App/Playback/PlayerView.Window.cs, src/VidShrink.App/Playback/PlayerView.Fare.cs, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/Playback/ |
| 2.2 | DZA-P3 | Düzenleyici | Tarama Aralıklarını Tek Tek Yönetme | orta | 1 | 4 | src/VidShrink.Core/Editing/SilenceCut.cs, src/VidShrink.App/Editing/EditorView.Sessizlik.cs, src/VidShrink.App/Editing/EditorTimeline.cs, src/VidShrink.App/Editing/EditorView.axaml |
| 2.2 | SIK-P4 | Küçült | Dış Ses İzi Ekleme | orta | 1 | 1 | src/VidShrink.App/MainWindow.Izler.cs, src/VidShrink.App/MainWindow.axaml (ses bölümü), src/VidShrink.Core/StreamMapping.cs, src/VidShrink.Core/FfmpegArguments.cs, src/VidShrink.Core/PlanCalculator.cs, src/VidShrink.Cli/CliRequest.cs, src/VidShrink.App/Locales/ |
| 2.2 | EKB-P2 | Kaydedici | İmleç Vurgusu Ve Spot Işığı | orta | 1 | 3 | src/VidShrink.App/Recorder/RecorderInput.cs, src/VidShrink.App/Recorder/RecorderInputOverlay.cs, src/VidShrink.App/Recorder/RecorderView.Girdi.cs, src/VidShrink.App/Recorder/RecorderSettings.cs, src/VidShrink.App/Recorder/RecorderView.axaml, src/VidShrink.App/Locales/ |
| 2.3 | OYA-P4 | Oynatıcı | Ses Aygıtı Ve Çıkış Seçenekleri | orta | 1 | 4 | src/VidShrink.Player/MpvEngine.cs, src/VidShrink.Player/IPlaybackEngine.cs, src/VidShrink.App/Playback/PlayerView.Tracks.cs, src/VidShrink.App/Playback/PlayerSettings.cs, src/VidShrink.App/Playback/PlayerAdvancedPanel.axaml.cs |
| 2.3 | DZB-P2 | Düzenleyici | Dışa Aktarım Ayarları Ve Ön Ayarlar | orta | 1 | 3 | src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.App/Editing/EditorView.Teslim.cs, src/VidShrink.App/Editing/EditorView.axaml, src/VidShrink.App/MainWindow.OnAyar.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/ |
| 2.3 | SIK-P5 | Küçült | Yakılan Altyazıda Biçim Seçimi | orta | 1 | 1 | src/VidShrink.Core/VideoFilterChain.cs, src/VidShrink.App/MainWindow.Izler.cs, src/VidShrink.App/MainWindow.axaml (altyazı bölümü, `CmbBurnSubtitle` yanı), src/VidShrink.Cli/CliRequest.cs, src/VidShrink.App/Locales/ |
| 2.3 | EKB-P4 | Kaydedici | Gizlilik Maskesi | orta | 1 | 1 | src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.App/Recorder/RecorderView.Duzenleyici.cs, src/VidShrink.App/Recorder/RecorderSettings.cs, src/VidShrink.App/Recorder/RecorderView.axaml, src/VidShrink.App/Locales/ |
| 2.3 | ARC-P2 | Bizde Olmayan Araçlar | Medya Bilgisi Ve Denetim Raporu | orta | 1 | 2 | src/VidShrink.Core/MediaInfo.cs, src/VidShrink.Ffmpeg/FfprobeClient.cs, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/Playback/PlayerView.Window.cs, src/VidShrink.Core/Editing/SilenceCut.cs, src/VidShrink.App/Editing/EditorView.Sessizlik.cs |
| 2.4 | DZA-P5 | Düzenleyici | İz Seçim Paneli | orta | 1 | 2 | src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.Core/Editing/EditProject.cs, src/VidShrink.App/Editing/EditorView.Teslim.cs, src/VidShrink.App/Editing/EditorView.Monitor.cs, src/VidShrink.App/Playback/PlayerView.Tracks.cs |
| 2.4 | DON-P2 | Dönüştür | Dönüştür'de Altyazı İzleri | orta | 1 | 3 | src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.Core/StreamMapping.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/MainWindow.Izler.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/ |
| 2.4 | EKB-P5 | Kaydedici | Kamera Biçimi Ve Konumu | orta | 1 | 3 | src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.App/Recorder/RecorderView.Kamera.cs, src/VidShrink.App/Recorder/RecorderSettings.cs, src/VidShrink.App/Recorder/RecorderView.axaml, src/VidShrink.App/Locales/ |
| 2.4 | DZB-P4 | Düzenleyici | Klip Gruplama Ve Pano | küçük | 2 | 2 | src/VidShrink.Core/Editing/EditTimeline.cs, src/VidShrink.App/Editing/EditorTimeline.cs, src/VidShrink.App/Editing/EditorKeymap.cs, src/VidShrink.App/Editing/EditorView.axaml, src/VidShrink.App/Locales/, tests/VidShrink.Tests/ |
| 2.5 | DZB-P3 | Düzenleyici | Temel Renk Ve Hazır Görünüm | orta | 1 | 2 | src/VidShrink.Core/Editing/ClipEffects.cs, src/VidShrink.Core/Editing/ClipFilters.cs, src/VidShrink.Core/Editing/EditLook.cs, src/VidShrink.Core/Editing/EditProject.cs, src/VidShrink.App/Editing/EditorView.Klip.cs, src/VidShrink.App/Editing/EditorView.axaml, src/VidShrink.App/Locales/, tests/VidShrink.Tests/DuzenleyiciKlipOzellikTests.cs |
| 2.5 | DON-P3 | Dönüştür | Dönüştür'de Altyazı Gömme | orta | 1 | 2 | src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.Core/VideoFilterChain.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.Izler.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/ |
| 2.5 | PLT-P2 | Platform Eşitliği | İşlemeyen Kutuları Gizleme | küçük | 2 | 1 | src/VidShrink.App/Recorder/RecorderView.axaml, src/VidShrink.App/Recorder/RecorderMini.axaml, src/VidShrink.App/Recorder/RecorderView.Girdi.cs, tests/VidShrink.Tests |
| 2.6 | DON-P5 | Dönüştür | Adlı Dönüştür Ön Ayarı | orta | 1 | 2 | src/VidShrink.Core/PresetLibrary.cs, src/VidShrink.App/MainWindow.OnAyar.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/ |
| 2.6 | PLT-P3 | Platform Eşitliği | macOS Tıklama İşareti | küçük | 2 | 1 | src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.App/Recorder/RecorderView.Girdi.cs, src/VidShrink.App/Recorder/RecorderView.axaml, tests/VidShrink.Tests |
| 2.7 | DON-P6 | Dönüştür | Toplu Dönüştürme Ve Dosya Başına Ayar | orta | 1 | 2 | src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/ (yeni sıra penceresi), src/VidShrink.Ffmpeg/EncodeRunner.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/ |
| 2.8 | DON-P7 | Dönüştür | Harici Ses Dosyası Ekleme | orta | 1 | 1 | src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/ |
| 2.9 | DON-P8 | Dönüştür | Görüntü Dizisi Girdisi Ve Çıktısı | orta | 1 | 3 | src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/MainWindow.DonusturKap.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/ |
| 2.10 | AYR-P2 | Ayarlar Ve Kabuk | Ayar Yedeği Ve Bozulma Uyarısı | orta | 1 | 2 | src/VidShrink.App/AppSettings.cs, src/VidShrink.App/AppDataReset.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/Locales/ |
| 2.10 | ARC-P3 | Bizde Olmayan Araçlar | Ses Dosyası Ve GIF Girdisi | orta | 1 | 2 | src/VidShrink.Ffmpeg/FfprobeClient.cs, src/VidShrink.Core/WatchFolder.cs, src/VidShrink.Core/DroppedMedia.cs, src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Core/GifPalette.cs, src/VidShrink.App/MainWindow.axaml.cs |
| 2.11 | ARC-P4 | Bizde Olmayan Araçlar | Resim Dizisi Girdisi Ve Ara Kare | orta | 1 | 3 | src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Core/DroppedMedia.cs, src/VidShrink.Core/VideoFilterChain.cs, src/VidShrink.App/MainWindow.axaml |

### Dalga 3 — Karar Gerekmez, Orta İş Öncelik 2 Ya Da Büyük İş Öncelik 1

| Tur | Paket | Sekme | Ad | İş | Öncelik | Satır | Dokunacağı Yer |
|---|---|---|---|---|---|---|---|
| 3.1 | DZA-P9 | Düzenleyici | Komut Satırı Ve Toplu Teslim | büyük | 1 | 2 | src/VidShrink.Cli/CliRequest.cs, src/VidShrink.Core/Editing/EditProject.cs, src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.Core/JobPump.cs, src/VidShrink.App/Editing/ |
| 3.1 | EKB-P6 | Kaydedici | Hızlı Kırpma Ve Kayıt Geçmişi | büyük | 1 | 2 | src/VidShrink.App/Recorder/RecorderView.axaml, src/VidShrink.App/Recorder/RecorderView.axaml.cs, src/VidShrink.App/Recorder/, src/VidShrink.App/Playback/ClipExport.cs, src/VidShrink.App/Recorder/RecorderSettings.cs, src/VidShrink.App/Locales/ |
| 3.1 | OYA-P9 | Oynatıcı | Dosya Başına Hatırlama Ve Gezinme | orta | 2 | 3 | src/VidShrink.App/Playback/PlaybackHistory.cs, src/VidShrink.App/Playback/PlayerView.Tracks.cs, src/VidShrink.App/Playback/Keymap.cs, src/VidShrink.App/Playback/PlayerSettings.cs, src/VidShrink.App/Playback/PlayerView.Serit.cs |
| 3.1 | OYB-P7 | Oynatıcı | Kabukta Çoklu Açma Ve Sıraya Ekleme | orta | 2 | 3 | src/VidShrink.App/Program.cs, src/VidShrink.App/Integration/ForwardedFiles.cs, src/VidShrink.App/MainWindow.SingleInstance.cs, src/VidShrink.Core/Setup/ShellRegistration.cs, src/VidShrink.App/ShellMenu.cs, Install-VidShrink.ps1, tests/VidShrink.Tests/KabukMenusuTests.cs |
| 3.1 | SIK-P6 | Küçült | Çıktı Tarihi, Adı Ve Doğrulaması | orta | 2 | 4 | src/VidShrink.Ffmpeg/EncodeRunner.cs, src/VidShrink.Core/AdlandirmaDeseni.cs, src/VidShrink.App/ShrinkJobWindow.axaml.cs, src/VidShrink.App/MainWindow.axaml (Ayarlar), src/VidShrink.App/AppSettings.cs, src/VidShrink.App/Locales/ |
| 3.1 | ARC-P7 | Bizde Olmayan Araçlar | Oynatıcıdan Kare Ve Tablo Çıkarma | orta | 2 | 3 | src/VidShrink.App/Playback/ToolsOptions.cs, src/VidShrink.App/Playback/PlayerView.Tools.cs, src/VidShrink.Core/GifPalette.cs, src/VidShrink.App/Locales/ |
| 3.2 | EKB-P7 | Kaydedici | Ekrana Çizim Ve Prompter | büyük | 1 | 2 | src/VidShrink.App/Recorder/, src/VidShrink.App/Recorder/CaptureAffinity.cs, src/VidShrink.App/Recorder/RecorderView.Kisayol.cs, src/VidShrink.App/Recorder/RecorderSettings.cs, src/VidShrink.App/Locales/ |
| 3.2 | ARC-P5 | Bizde Olmayan Araçlar | Etiket, Bölüm Ve Onarma | büyük | 1 | 6 | src/VidShrink.Core/RecorderChapters.cs, src/VidShrink.Core/StreamMapping.cs, src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.Cli/CliRequest.cs |
| 3.2 | OYA-P10 | Oynatıcı | Altyazı Biçemi Ve Kare Kaydı | orta | 2 | 4 | src/VidShrink.Player/MpvEngine.Advanced.cs, src/VidShrink.Player/MpvEngine.cs, src/VidShrink.App/Playback/PlayerView.Advanced.cs, src/VidShrink.App/Playback/PlayerAdvancedPanel.axaml.cs, src/VidShrink.App/Playback/PlayerSettings.cs |
| 3.2 | OYA-P11 | Oynatıcı | Doldurma Ve Dokunmatik Hareketler | orta | 2 | 2 | src/VidShrink.App/Playback/PlayerView.axaml.cs, src/VidShrink.App/Playback/ZoomGesture.cs, src/VidShrink.App/Playback/PlayerView.Fare.cs, src/VidShrink.App/Playback/Keymap.cs |
| 3.2 | DZA-P12 | Düzenleyici | Akıllı Kipte MPEG-2 | orta | 2 | 1 | src/VidShrink.Core/Editing/SmartCutCodec.cs, src/VidShrink.Core/Editing/EditExport.cs, tools/VidShrink.Bench |
| 3.2 | DZB-P11 | Düzenleyici | Medya Havuzu Ve Araya Ekleme | orta | 2 | 2 | src/VidShrink.App/Editing/EditorView.Kaynak.cs, src/VidShrink.App/Editing/EditorView.axaml, src/VidShrink.App/Editing/EditorTimeline.cs, src/VidShrink.Core/Editing/EditTimeline.cs, src/VidShrink.Core/Editing/EditProject.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/DuzenleyiciCokKaynakTests.cs |
| 3.3 | DAG-P4 | Dağıtım Ve Otomasyon | Yerel Denetim Borusu Ve Kayıt Komutu | büyük | 1 | 2 | src/VidShrink.Core/SingleInstanceChannel.cs, src/VidShrink.Core/ShrinkRequest.cs, src/VidShrink.App/Integration/, src/VidShrink.App/Recorder/, src/VidShrink.Cli/, docs/ |
| 3.3 | OYB-P8 | Oynatıcı | Yer İmi Listesi Ve Sık Kullanılanlar | orta | 2 | 2 | src/VidShrink.App/Playback/PlaybackHistory.cs, src/VidShrink.App/Playback/Keymap.cs, src/VidShrink.App/Playback/PlayerView.Window.cs, src/VidShrink.App/Locales/ |
| 3.3 | DZA-P8 | Düzenleyici | Bölüm Yazma Ve Koruma | orta | 2 | 3 | src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.Core/RecorderChapters.cs, src/VidShrink.Core/MediaInfo.cs, src/VidShrink.Ffmpeg/FfprobeClient.cs, src/VidShrink.App/Editing/EditorView.Kaynak.cs |
| 3.3 | SIK-P7 | Küçült | Kuyrukta Zamanlama, Düzenleme Ve Bitiş | orta | 2 | 4 | src/VidShrink.App/ShrinkJobWindow.axaml, src/VidShrink.App/ShrinkJobWindow.axaml.cs, src/VidShrink.Core/JobPump.cs, src/VidShrink.App/QueueEndActions.cs, src/VidShrink.App/IsBittiBildirimi.cs, src/VidShrink.App/AppSettings.cs, src/VidShrink.App/Locales/ |
| 3.3 | SIK-P8 | Küçült | Önizlemede Elle Kırpma | orta | 2 | 1 | src/VidShrink.App/MainWindow.axaml (önizleme alanı ve `ChkFltAutoCrop` çevresi), src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.Core/VideoFilterChain.cs, src/VidShrink.App/Locales/ |
| 3.3 | EKB-P12 | Kaydedici | En-Boy Sığdırma Ön Ayarı | orta | 2 | 1 | src/VidShrink.Core/Editing/ClipEffects.cs, src/VidShrink.Core/Editing/ClipFilters.cs, src/VidShrink.App/Editing/EditorView.Klip.cs, src/VidShrink.App/Locales/ |
| 3.4 | OYB-P9 | Oynatıcı | Mac Ortam Denetimi Bağlantısı | orta | 2 | 1 | src/VidShrink.Player/MpvEngine.cs, src/VidShrink.App/Playback/ |
| 3.4 | DON-P9 | Dönüştür | Dönüştür'de Ses Seçenekleri | orta | 2 | 7 | src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/MainWindow.DonusturKap.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/ |
| 3.4 | EKA-P1 | Kaydedici | Disk Uyarısı Ve Kayıt Kurtarma | orta | 2 | 2 | src/VidShrink.Ffmpeg/DiskSpaceGuard.cs, src/VidShrink.Ffmpeg/RecorderSession.cs, src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.App/Recorder/RecorderView.Yakalama.cs, src/VidShrink.App/Locales/ |
| 3.4 | EKA-P10 | Kaydedici | Genel Kısayollar Windows Dışında | orta | 2 | 1 | src/VidShrink.App/Recorder/RecorderHotkeys.cs, src/VidShrink.App/Recorder/RecorderView.Kisayol.cs |
| 3.4 | EKB-P3 | Kaydedici | Sonuç Paneli Ve Paylaşım Kısayolları | orta | 2 | 5 | src/VidShrink.App/Recorder/RecorderView.axaml, src/VidShrink.App/Recorder/RecorderView.axaml.cs, src/VidShrink.App/Recorder/RecorderView.Bitince.cs, src/VidShrink.App/Recorder/RecorderView.Paylas.cs, src/VidShrink.App/Recorder/RecorderSettings.cs, src/VidShrink.App/Share/ShareSession.cs, src/VidShrink.Core/AdlandirmaDeseni.cs, src/VidShrink.App/Locales/ |
| 3.4 | DAG-P1 | Dağıtım Ve Otomasyon | Sessiz Kurulum Ve Uygulama Kaydı | orta | 2 | 4 | src/VidShrink.Setup/Program.cs, src/VidShrink.Core/Setup/SetupRunner.cs, src/VidShrink.Core/Setup/SetupText.cs, Install-VidShrink.ps1, docs/kurulum.md |
| 3.5 | DZB-P5 | Düzenleyici | Ses Temizleme Ve Düzey | orta | 2 | 4 | src/VidShrink.Core/Editing/ClipEffects.cs, src/VidShrink.Core/Editing/ClipFilters.cs, src/VidShrink.Core/Editing/EditLook.cs, src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.Core/StreamMapping.cs, src/VidShrink.App/Editing/EditorView.Klip.cs, src/VidShrink.App/Editing/EditorView.axaml, src/VidShrink.Player/MpvEngine.cs, src/VidShrink.App/Locales/ |
| 3.5 | DON-P10 | Dönüştür | Dönüştür'de Görüntü Süzgeçleri | orta | 2 | 3 | src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.Core/VideoFilterChain.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/ |
| 3.5 | EKA-P3 | Kaydedici | Yalnız Ses Ve Ses Seçenekleri | orta | 2 | 3 | src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.Core/AudioCaptureArguments.cs, src/VidShrink.App/Recorder/RecorderView.Ses.cs, src/VidShrink.App/Recorder/RecorderView.Gelismis.cs |
| 3.5 | EKA-P5 | Kaydedici | Komut Satırından Kayıt | orta | 2 | 1 | src/VidShrink.Cli/CliApp.cs, src/VidShrink.App/Program.cs, src/VidShrink.App/Recorder/RecorderSettings.cs, src/VidShrink.Ffmpeg/RecorderSession.cs |
| 3.5 | DAG-P3 | Dağıtım Ve Otomasyon | Windows 11 İlk Menüde Küçültme | orta | 2 | 1 | src/VidShrink.ShellExtension/VidShrink.ShellExtension.cpp, src/VidShrink.ShellExtension/AppxManifest.template.xml, src/VidShrink.Core/ShellIntegration.cs, src/VidShrink.Core/Setup/SetupRunner.cs, Install-VidShrink.ps1 |
| 3.6 | DON-P11 | Dönüştür | Kodlayıcı, HDR Ve Renk Ayarları | orta | 2 | 3 | src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.Core/HdrResolver.cs, src/VidShrink.Core/VideoFilterChain.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/ |
| 3.6 | EKA-P4 | Kaydedici | Kodlayıcı Listesi Ve Kare Hızı | orta | 2 | 3 | src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.Core/CodecModel.cs, src/VidShrink.App/Recorder/RecorderView.Gelismis.cs |
| 3.6 | EKA-P6 | Kaydedici | Saatli Kayıt | orta | 2 | 1 | src/VidShrink.App/Recorder/RecorderSettings.cs, src/VidShrink.App/Recorder/RecorderView.GeriSayim.cs, src/VidShrink.App/Recorder/RecorderView.Yakalama.cs, src/VidShrink.App/Locales/ |
| 3.6 | EKA-P7 | Kaydedici | Bas Konuş | orta | 2 | 1 | src/VidShrink.App/Recorder/RecorderHotkeys.cs, src/VidShrink.App/Recorder/RecorderView.Kisayol.cs, src/VidShrink.App/Recorder/RecorderInput.cs, src/VidShrink.Core/AudioCaptureArguments.cs, src/VidShrink.Ffmpeg/RecorderSession.cs |
| 3.6 | DAG-P2 | Dağıtım Ve Otomasyon | Komut Satırı Çıktısı Ve Dönüştürme | orta | 2 | 4 | src/VidShrink.Cli/CliRequest.cs, src/VidShrink.Cli/CliApp.cs, src/VidShrink.Core/PresetLibrary.cs, docs/cli.md, docs/cli.tr.md |
| 3.7 | DON-P12 | Dönüştür | Arşiv Kodeği, APNG Ve GIF Ayarı | orta | 2 | 4 | src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.Core/CodecModel.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.DonusturKap.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/ |
| 3.7 | EKA-P8 | Kaydedici | Kamera Ana Kaynak Olarak | orta | 2 | 1 | src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.Ffmpeg/CaptureDevices.cs, src/VidShrink.App/Recorder/RecorderView.Hedef.cs, src/VidShrink.App/Recorder/RecorderView.Kamera.cs |
| 3.8 | DON-P13 | Dönüştür | Özel Argüman Ve Komut Satırı | orta | 2 | 2 | src/VidShrink.Cli/CliRequest.cs, src/VidShrink.Cli/, src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/ |
| 3.8 | EKA-P9 | Kaydedici | Kamera Katmanı Windows Dışında | orta | 2 | 2 | src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.Ffmpeg/CaptureDevices.cs, src/VidShrink.App/Recorder/RecorderView.Kamera.cs |
| 3.9 | AYR-P3 | Ayarlar Ve Kabuk | Güncelleme, Hakkında Ve Tanılama | orta | 2 | 7 | src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/MainWindow.Guncelleme.cs, src/VidShrink.Core/Gunluk.cs, .github/ISSUE_TEMPLATE/, src/VidShrink.App/Locales/ |
| 3.10 | AYR-P4 | Ayarlar Ve Kabuk | Sistem Teması Ve Hareket Ayarı | orta | 2 | 3 | src/VidShrink.App/Themes/PaletteCatalog.cs, src/VidShrink.App/AppSettings.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/Playback/HoverZone.cs |
| 3.11 | AYR-P5 | Ayarlar Ve Kabuk | Uzun İşte Öncelik, Bildirim Ve Ses | orta | 2 | 3 | src/VidShrink.App/IsBittiBildirimi.cs, src/VidShrink.Ffmpeg/EncodeRunner.cs, src/VidShrink.Ffmpeg/FfmpegRunner.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/Locales/ |
| 3.12 | AYR-P6 | Ayarlar Ve Kabuk | Kabuk Kısayolları Ve Ayar Araması | orta | 2 | 5 | src/VidShrink.App/Playback/ShortcutBindings.cs, src/VidShrink.App/Playback/ShortcutTable.cs, src/VidShrink.App/MainWindow.PencereKipi.cs, src/VidShrink.App/MainWindow.axaml |
| 3.13 | AYR-P7 | Ayarlar Ve Kabuk | Erişilebilirlik Denemesi | orta | 2 | 2 | docs/olcumler/, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/Themes/Controls.axaml |

### Dalga 4 — Karar Gerekmez, Kalanlar

| Tur | Paket | Sekme | Ad | İş | Öncelik | Satır | Dokunacağı Yer |
|---|---|---|---|---|---|---|---|
| 4.1 | OYB-P10 | Oynatıcı | Akış Kalitesi Ve Kesintisiz Çalma | büyük | 2 | 2 | src/VidShrink.Player/MpvEngine.cs, src/VidShrink.Player/IPlaybackEngine.cs, src/VidShrink.App/Playback/PlayerView.Tracks.cs, src/VidShrink.App/Playback/PlayerView.Kuyruk.cs |
| 4.1 | DZA-P10 | Düzenleyici | Kesim Listesi Ve Kurgu Aktarımı | büyük | 2 | 3 | src/VidShrink.Core/Editing/, src/VidShrink.App/Editing/EditorView.Proje.cs |
| 4.2 | DZA-P11 | Düzenleyici | Kap İçi İz İşleri | büyük | 2 | 6 | src/VidShrink.Core/Editing/, src/VidShrink.Core/StreamMapping.cs, src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.App/Editing/EditorView.Teslim.cs |
| 4.3 | DZB-P9 | Düzenleyici | Metin Konumu, Hareketi Ve Altyazı | büyük | 2 | 4 | src/VidShrink.App/Editing/EditorView.Metin.cs, src/VidShrink.App/Editing/EditorView.Monitor.cs, src/VidShrink.App/Editing/EditorView.axaml, src/VidShrink.Core/Editing/TextLayer.cs, src/VidShrink.Core/Editing/AssWriter.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/DuzenleyiciMetinTests.cs |
| 4.3 | DZB-P10 | Düzenleyici | Resim Klibi, Tuval Ve Dönüşüm | büyük | 2 | 5 | src/VidShrink.App/Editing/EditorView.Kaynak.cs, src/VidShrink.App/Editing/EditorView.Klip.cs, src/VidShrink.Core/Editing/EditTimeline.cs, src/VidShrink.Core/Editing/ClipEffects.cs, src/VidShrink.Core/Editing/ClipFilters.cs, src/VidShrink.Core/Editing/EditLook.cs, src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.Core/Editing/EditProject.cs, tests/VidShrink.Tests/ |
| 4.4 | DZB-P13 | Düzenleyici | İleri Renk Ve Bulanıklık | büyük | 2 | 3 | src/VidShrink.Core/Editing/ClipEffects.cs, src/VidShrink.Core/Editing/ClipFilters.cs, src/VidShrink.Core/Editing/EditLook.cs, src/VidShrink.App/Editing/EditorView.Klip.cs, src/VidShrink.App/Editing/EditorView.Monitor.cs, src/VidShrink.App/Editing/EditorView.axaml, src/VidShrink.App/Locales/, tests/VidShrink.Tests/DuzenleyiciKlipOzellikTests.cs |
| 4.5 | DZB-P14 | Düzenleyici | Anahtar Kare Ve Hız Rampası | büyük | 2 | 4 | src/VidShrink.Core/Editing/ClipEffects.cs, src/VidShrink.Core/Editing/ClipFilters.cs, src/VidShrink.Core/Editing/EditLook.cs, src/VidShrink.Core/Editing/EditTimeline.cs, src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.Core/Editing/EditProject.cs, src/VidShrink.App/Editing/EditorView.Klip.cs, src/VidShrink.App/Editing/EditorTimeline.cs, tests/VidShrink.Tests/ |
| 4.6 | ARC-P10 | Bizde Olmayan Araçlar | Slayt Gösterisi Ve Sesten Video | büyük | 2 | 2 | src/VidShrink.Core/Editing/, src/VidShrink.App/Editing/, src/VidShrink.Core/ConversionArguments.cs |

### Sahibin Kararını Bekleyenler

| Paket | Sekme | Ad | İş | Öncelik | Sahibin Vereceği Karar |
|---|---|---|---|---|---|
| SIK-P2 | Küçült | Kodlama Sürerken Uykuyu Engelleme | küçük | 1 | Kodlama sürerken bilgisayarın uykuya geçmesi varsayılan olarak mı engellensin, yoksa kapalı gelen bir ayar mı olsun? |
| EKB-P1 | Kaydedici | Tuş Gösterimi Süzgeci | küçük | 1 | "Yalnız kısayolları göster" süzgeci varsayılan açık mı gelsin, yoksa bugünkü gibi her tuş mu gösterilsin. |
| AYR-P1 | Ayarlar Ve Kabuk | Pencere Konumunu Hatırlama | küçük | 1 | Uygulama her açılışta ekranı kaplamış açılmak yerine son konumunda, boyutunda ve durumunda mı açılsın? |
| OYA-P5 | Oynatıcı | Açılış Varsayılanları | orta | 1 | dosya açılırken dört varsayılan değişsin mi: tercih edilen dilde ses ve altyazı izi seçimi, zorunlu altyazının kendiliğinden açılması, devamdaki son beş dosya sınırının kalkması ve deinterlace'in otomatik olması |
| OYA-P6 | Oynatıcı | Çift Tık Ve Basılı Tutma | orta | 1 | sol tık üç anlam taşısın mı: tek tık oynat ya da duraklat (bugünkü), çift tık tam ekran, basılı tutunca bırakana dek hızlı oynatma |
| OYA-P7 | Oynatıcı | HDR Ton Eşleme | orta | 1 | HDR video işlemcide ton eşlemeyle gösterilsin mi; 4K'da kare düşürürse yalnız elle açılan bir seçenek mi olsun |
| OYA-P8 | Oynatıcı | Sessiz Bölümleri Atlama | orta | 1 | sessiz yerleri hızlandıran yeni bir oynatma kipi eklensin mi; hazır sunan rakip bulunamadı, istek yalnız oyla kanıtlı |
| OYB-P5 | Oynatıcı | Devam Kapsamı Ve Kalıcı Kuyruk | orta | 1 | kaldığı yerden devam son beş dosyayla sınırlı kalsın mı ve uygulama açılınca son kuyruk geri gelsin mi |
| OYB-P6 | Oynatıcı | Windows Ortam Denetimi Bağlantısı | orta | 1 | libmpv'nin kendi ortam denetimi anahtarı yetmezse Windows SDK bağı eklensin mi |
| DZA-P6 | Düzenleyici | Kayıpsız Birleştirme Yolu | orta | 1 | çok kaynakta Hızlı ve Akıllı kip, kaynaklar uyumluysa yeniden kodlamak yerine kopyalasın mı (K7 her zaman yeniden kodlamayı seçmişti) |
| DZA-P7 | Düzenleyici | Tüm İzleri Varsayılan Taşıma | orta | 1 | teslim varsayılanı "ilk görüntü ve ilk ses" yerine "bütün izler" olsun mu |
| DZB-P6 | Düzenleyici | Çapraz Geçiş Teslimi | orta | 1 | Düzenleyicinin yazılı kapsamı "geçişsiz"den çıkıp iki klip arasında çapraz geçişi kapsasın mı? |
| DZB-P7 | Düzenleyici | Müzik İzi | orta | 1 | Düzenleyici tek şeritten çıkıp videonun altına ikinci bir ses izi (müzik, anlatım) alsın mı? |
| DON-P4 | Dönüştür | Dönüştür'de Ses İzi Seçimi | orta | 1 | Dönüştür varsayılan olarak tek ses mi çıkarsın, yoksa kabın taşıyabildiği bütün sesleri mi taşısın. |
| EKA-P2 | Kaydedici | Uygulama Başına Ses | orta | 1 | Uygulama başına ses yeni bir Windows ses arabirimi ister ve eski Windows 10 yapılarında çalışmaz; orada ve Windows dışında seçenek gizlensin mi, kapalı mı görünsün. |
| EKB-P8 | Kaydedici | Kayıt Çerçevesi | orta | 1 | Hazır arka plan görselleri pakete girsin mi, yoksa düz renk ve renk geçişiyle mi yetinilsin. |
| DAG-P5 | Dağıtım Ve Otomasyon | Winget, Scoop Ve Chocolatey Paketleri | orta | 1 | Windows paket dizinlerinde yayın yapılsın mı; winget için PR sahibin hesabından açılır ve Microsoft katkı sözleşmesi onaylanır, Scoop için kova deposu açılır, Chocolatey için hesap gerekir. |
| DAG-P6 | Dağıtım Ve Otomasyon | Windows Kod İmzası | orta | 1 | SignPath Foundation'a başvurulsun mu (ücretsiz; başvuruyu ve her imza onayını sahip yapar), yoksa ücretli sertifika mı alınsın. |
| DZB-P8 | Düzenleyici | Geçiş Önizleme Yolu | büyük | 1 | Geçişin ve bindirmenin canlı önizlemesi hangi yoldan yapılsın: libmpv süzgeç grafiği, geçiş bölgesini kısa ön-işleme, yoksa ayrı bir bileşim motoru (MLT)? |
| DZB-P12 | Düzenleyici | İkinci Video İzi Ve Yeşil Perde | büyük | 1 | Düzenleyici tek şeritten çıkıp üstte ikinci bir video izi (resim içinde resim) alsın mı? |
| EKA-P12 | Kaydedici | Mac Sistem Sesi Yardımcısı | büyük | 1 | macOS için ScreenCaptureKit kullanan yerel bir yardımcı ikili yazılıp pakete eklensin mi. |
| EKA-P13 | Kaydedici | Mac Pencere Yakalama | büyük | 1 | macOS'ta pencere yakalama EKA-P12'deki yardımcı ikiliye bağlansın mı; o karar hayır çıkarsa bu paket düşer. |
| EKA-P14 | Kaydedici | Wayland Yakalama Yolu | büyük | 1 | Wayland için PipeWire portalını konuşan bir yardımcı ikili ya da yeni bağımlılık eklensin mi, yoksa yalnız açık bir uyarıyla yetinilsin mi. |
| EKB-P9 | Kaydedici | Otomatik Ve Canlı Yakınlaştırma | büyük | 1 | Yakınlaştırma kayıttan sonra mı üretilsin (imleç ve tıklama verisi videodan ayrı kaydedilir), kayıt sırasında canlı mı; ikisinden biri seçilir. |
| EKB-P10 | Kaydedici | İmleç Cilası Ve Hareket Bulanıklığı | büyük | 1 | İmleç kayda yakılmak yerine ayrı veri olarak tutulsun mu; EKB-P9'da kayıt sonrası yol seçilmeden bu paket başlamaz. |
| EKB-P11 | Kaydedici | Kamera Arka Planı | büyük | 1 | Kişiyi arka plandan ayıran model ve onu çalıştıran kitaplık uygulamayla dağıtılsın ya da ilk kullanımda indirilsin mi. |
| AYR-P8 | Ayarlar Ve Kabuk | Taşınabilir Kip Ve Güncelleme Kanalı | büyük | 1 | Yayına iki yeni biçim eklensin mi: ayarını yanında taşıyan kurulumsuz paket ve gönüllülere açık ön sürüm kanalı? |
| DAG-P8 | Dağıtım Ve Otomasyon | Flathub Ve AppImage Paketleri | büyük | 1 | Flathub'a uygulama başvurusu yapılsın mı (sahibin hesabından; ffmpeg ve libmpv paketin içinde gelir) ve yayına AppImage eklensin mi. |
| ARC-P6 | Bizde Olmayan Araçlar | Dosya Dönüştürme Ve Ölçüm Araçları | büyük | 1 | Araçlar sekmesi açılsın mı, yoksa en yakın sekmeye mi konsun |
| AYR-P9 | Ayarlar Ve Kabuk | Tepsiye Küçültme | küçük | 2 | Ana pencere için kapalı gelen bir "tepsiye küçült" seçeneği eklensin mi; eklenecekse kapatma düğmesi de tepsiye mi gitsin? |
| SIK-P9 | Küçült | Kuyruğun Saklanması Ve Geri Yüklenmesi | orta | 2 | Açılışta yarım kalan kuyruk kendiliğinden mi geri gelsin, yoksa kullanıcıya mı sorulsun? |
| SIK-P10 | Küçült | Biten Çıktının Kalite Ölçümü | orta | 2 | Tam kalite ölçümü her işi uzatır; varsayılan kapalı bir seçenek olarak arayüze girsin mi? |
| SIK-P12 | Küçült | VVC Kodlama Desteği | orta | 2 | libvvenc içeren bir ffmpeg derlemesi dağıtılsın mı ve oynatma desteği bu kadar darken VVC kodek listesine girsin mi? |
| EKA-P11 | Kaydedici | Model Tabanlı Gürültü Bastırma | orta | 2 | Gürültü bastırma modeli (RNNoise dosyası) kurulum paketine girsin mi. |
| EKB-P14 | Kaydedici | Kayan Sonuç Kartı | orta | 2 | Kayıt bitince ekran köşesinde kendiliğinden yeni bir pencere açılsın mı ve bu varsayılan açık mı gelsin. |
| EKB-P15 | Kaydedici | Kayıtta Bildirim Ve Simge Gizleme | orta | 2 | Uygulama kayıt sırasında sistemin bildirim ayarına ve masaüstü simgelerine dokunsun mu. |
| AYR-P10 | Ayarlar Ve Kabuk | Çeviri Katkısı Ve Tamlık İşareti | orta | 2 | 42 dil sözü nasıl sunulsun: gözden geçmemiş diller listede işaretlensin mi ve bir çeviri sitesinde proje hesabı açılsın mı? |
| DAG-P7 | Dağıtım Ve Otomasyon | Ön Sürüm Güncelleme Kanalı | orta | 2 | ön sürüm yayımlansın mı ve Ayarlar'a kanal seçimi eklensin mi; sürüm kesme düzeni değişir. |
| ARC-P8 | Bizde Olmayan Araçlar | Altyazı Çevirisi | orta | 2 | altyazı metnini çevrimiçi çeviri hizmetine ya da yerel dil modeline veren araç ürüne girsin mi |
| ARC-P9 | Bizde Olmayan Araçlar | Logo Ve Damga Silme | orta | 2 | başkasının logosunu ya da filigranını silmeye yarayan araç ürüne girsin mi |
| PLT-P4 | Platform Eşitliği | macOS İzin Akışı | orta | 2 | pakete izin açıklamaları eklenip kayıttan önce izin yoklaması gösterilsin mi |
| PLT-P5 | Platform Eşitliği | macOS Kapanışta Sessiz Güncelleme | orta | 2 | macOS'ta otomatik güncelleme varsayılan olarak açılsın mı |
| PLT-P6 | Platform Eşitliği | Linux'ta Kendini Güncelleme | orta | 2 | Linux'ta bilerek kapalı tutulan uygulama içi güncelleme açılsın mı |
| PLT-P7 | Platform Eşitliği | VideoToolbox Hızlı Kip Kapısı | orta | 2 | ölçüm için Mac ayrılsın mı ve kapıdan geçerse Hızlı kipin Mac'teki varsayılan kodlayıcısı değişsin mi |
| PLT-P8 | Platform Eşitliği | Linux Ölçüm Turu: VAAPI Ve Sistem Sesi | orta | 2 | ölçüm için Intel ya da AMD ekran kartlı gerçek bir Linux makinesi ayrılsın mı |
| PLT-P9 | Platform Eşitliği | Wayland'da Genel Kısayol | orta | 2 | GlobalShortcuts portalı için D-Bus istemcisi bağımlılığı eklensin mi |
| OYB-P11 | Oynatıcı | Dış Denetim Ve Eklenti Yüzeyleri | büyük | 2 | oynatıcı dışarıdan yönetilebilen bir yüzey açsın mı (komut kanalı, Linux ortam denetimi, ağdan uzaktan denetim, eklenti) ve tek örnek kuralına "yeni pencere" seçeneği gelsin mi |
| OYB-P12 | Oynatıcı | Kitaplık Ve Ağ Kaynakları | büyük | 2 | oynatıcı kitaplık ve ağ kaynağı yönüne büyüsün mü (adlı listeler, klasör tarayan kitaplık, site bağlantısı çözme, ağ paylaşımında gezme) |
| DZB-P15 | Düzenleyici | Çok İz Üstüne Gelenler | büyük | 2 | Çok iz açıldıktan sonra üstüne gelen işler (sesi ayırma, otomatik müzik kısma, seslendirme kaydı, iz denetimleri, boşluklu çizelge) kapsama girsin mi? |
| DZB-P16 | Düzenleyici | Vekil, Hazır İçerik Ve Model | büyük | 2 | Uygulama kullanıcının diskine büyük ek dosya (vekil kopya, hazır içerik paketi, yapay zekâ modeli) üretsin ya da indirsin mi; hangisi istekle, hangisi hiç? |
| SIK-P11 | Küçült | Ölçümle Hedef Kalite Araması | büyük | 2 | Arama her turda ek kodlama ve ölçüm süresi getirir; hedef kalite kipine isteğe bağlı bir seçenek olarak eklensin mi? |
| SIK-P13 | Küçült | Donanım Kodlayıcıda Dolby Vision | büyük | 2 | Dolby Vision kapsamı donanım kodlayıcılarına genişlesin mi, gerekirse ffmpeg dışında ayrı bir araçla? |
| EKA-P15 | Kaydedici | HDR Yakalama | büyük | 2 | HDR ekranda varsayılan HDR dosya mı olsun, yoksa SDR'ye çevrilmiş kayıt mı. |
| EKA-P16 | Kaydedici | Oyun Yakalama | büyük | 2 | Oyun sürecine kanca atan ayrı bir yakalama yolu yapılsın mı, yoksa ekran kaydı yolu ölçülüp yeterli mi sayılsın. |
| EKB-P16 | Kaydedici | Özel Hedef Ve Kendi Bulutun | büyük | 2 | Kullanıcının girdiği sunucu adresi ve erişim anahtarı uygulamada saklansın mı, bulut hesabına tarayıcıdan giriş eklensin mi. |
| EKB-P17 | Kaydedici | Girdi Efektleri Mac Ve Linux | büyük | 2 | macOS'ta girdi izleme izni istensin mi, ve Wayland'da genel tuş dinleme yolu yoksa Linux desteği X11 ile mi sınırlansın. |
| AYR-P11 | Ayarlar Ve Kabuk | Arayüz Ölçeği Ve Yüksek Karşıtlık | büyük | 2 | "Renk yalnız paletten, ölçü yalnız belirteçten" kuralına sistemin karşıtlık ve yazı boyutu ayarları için istisna açılsın mı? |
| AYR-P12 | Ayarlar Ve Kabuk | İlk Açılış Karşılaması Ve Kılavuz | büyük | 2 | Yeni kullanıcıya yardım nasıl sunulsun: ilk açılışta bir karşılama ekranı ve dillerde sürdürülen çevrimiçi kılavuz sitesi açılsın mı? |
| DAG-P9 | Dağıtım Ve Otomasyon | Tam Paket Ve Çevrimdışı Kurucu | büyük | 2 | ffmpeg ve libmpv ikilileri kendi yayın dosyalarımızın içinde dağıtılsın mı (yüzlerce MB ek boyut ve lisans yükümlülüğü). |
| DAG-P10 | Dağıtım Ve Otomasyon | macOS İmza, Dmg Ve Homebrew | büyük | 2 | ücretli Apple geliştirici üyeliği alınsın mı; alınırsa dmg yayımlanır ve Homebrew için kendi tap depomuz açılır. |
| DAG-P11 | Dağıtım Ve Otomasyon | Microsoft Store Paketi | büyük | 2 | Microsoft Store geliştirici hesabı açılsın mı ve uygulama MSIX olarak mağazaya girsin mi. |
| DAG-P12 | Dağıtım Ve Otomasyon | Betik Ve Eklenti Arabirimi | büyük | 2 | uygulamaya betik motoru (yeni bağımlılık) eklensin ve sürümler arası korunacak bir eklenti arabirimi sözü verilsin mi. |
| ARC-P11 | Bizde Olmayan Araçlar | Yapay Zekâ Araçları | büyük | 2 | yapay zekâ araçları için yeni çalışma zamanı ve yüzlerce MB model indirmesi kabul edilsin mi |
| ARC-P12 | Bizde Olmayan Araçlar | Kendi Sekmesini İsteyen Araçlar | büyük | 2 | kendi sekmesini isteyen araç ürüne girsin mi |

## Sahibin Kararları

65 paket sahibin kararını bekliyor (4 küçük, 31 orta, 30 büyük; 115 satır). Aşağıda on tema var; her paket bir kez geçiyor, temalar cevabı en çok işin önünü açandan en aza sıralı. Önerilen cevaplar girdideki plan notlarına dayanan değerlendirmedir, ölçüm değildir.

### Varsayılan Davranış Değişiklikleri

- SIK-P2: kodlama sürerken uyku varsayılan olarak mı engellensin, yoksa kapalı gelen bir ayar mı olsun; öneri varsayılan açık ve Ayarlar'dan kapatılabilir, çünkü gece bırakılan kuyruk bilgisayar uyuyunca yarıda kalıyor (SIK-06).
- EKB-P1: tuş gösteriminde "yalnız kısayolları göster" süzgeci varsayılan açık mı gelsin; öneri evet, çünkü bugün tuş gösterimi açıkken yazılan şifre harf harf kayda giriyor (EKB-11).
- AYR-P1: uygulama ekranı kaplamış açılmak yerine son konumunda, boyutunda ve durumunda mı açılsın; öneri evet, çünkü küçük bir iş ve her açılışta hissediliyor (AYR-03).
- OYA-P5, OYB-P5: dosya açılırken varsayılanlar değişsin mi: devam sınırı, dil tercihi, zorunlu altyazı, otomatik deinterlace (taramalı videodaki çizgileri giderme) ve kalıcı kuyruk; öneri devam sınırı kalksın (tek sabit, depo zaten 200 kayıt tutuyor; OYA-36, OYB-12), dil tercihi ve zorunlu altyazı açılsın (OYA-04, OYA-31), deinterlace otomatik olsun (OYA-51), kalıcı kuyruk kapalı gelen bir ayar olsun (OYB-08), çünkü ilk dördü kullanıcının her dosyada elle yaptığı işi kaldırıyor.
- OYA-P6: sol tık üç anlam taşısın mı (tek tık oynat ya da duraklat, çift tık tam ekran, basılı tutunca hızlı oynatma); öneri çift tık için evet, basılı tutma sonra, çünkü iki satır da öncelik 1 (OYA-02, OYA-38) ama kodda çift tıkın ayrı hareket sayılmaması bilinçli bir seçim ve sol tık testi bugün kararsız, önce test sağlamlaşmalı.
- DZA-P6: çok kaynakta Hızlı ve Akıllı kip, kaynaklar uyumluysa yeniden kodlamak yerine kopyalasın mı; öneri evet, uyumsuz kaynakta bugünkü yeniden kodlamaya düşerek, çünkü kayıpsız birleştirme bu sekmenin ana işi (DZA-01) ve K7'de yapılan yol yedek olarak kalıyor.
- DZA-P7: teslim varsayılanı "ilk görüntü ve ilk ses" yerine "bütün izler" olsun mu; öneri evet, DZA-P5'in iz seçim paneli geldikten sonra, çünkü bugün öbür izler teslimde sessizce düşüyor (DZA-02) ve panel istenmeyeni kapatma yolu veriyor.
- DON-P4: Dönüştür varsayılan olarak tek ses mi çıkarsın, kabın taşıyabildiği bütün sesleri mi; öneri bütün sesler, DON-P2'nin akış planından sonra, çünkü ikinci ses bugün sessizce kayboluyor (DON-01) ve DZA-P7 ile aynı yönde tek kural olur.
- SIK-P9: açılışta yarım kalan kuyruk kendiliğinden mi geri gelsin, kullanıcıya mı sorulsun; öneri sorulsun, çünkü kendiliğinden başlayan kodlama kullanıcının haberi olmadan işlemciyi doldurur (SIK-05).

### Düzenleyicinin Kapsamı

Girdinin önerdiği sıra: önce karar istemeyen DZB-P1 ile DZB-P5 yapılır, aynı gün ilk üç kapsam kararı sorulur.

- DZB-P6: Düzenleyicinin yazılı kapsamı "geçişsiz"den çıkıp iki klip arasında çapraz geçişi kapsasın mı; öneri evet, çünkü ffmpeg'in hazır geçiş desenleri yetiyor ve ek içerik gerekmiyor (DZB-16, DZB-18).
- DZB-P7: videonun altına ikinci bir ses izi (müzik, anlatım) gelsin mi; öneri evet, çünkü kurgu programlarının ortak asgari kümesinde olup bizde hiç olmayan en ağır üç kalemden biri (DZB-01).
- DZB-P8: geçişin ve bindirmenin canlı önizlemesi hangi yoldan yapılsın (libmpv süzgeç grafiği, geçiş bölgesini kısa ön-işleme, ayrı bir bileşim motoru); öneri kısa ön-işleme, çünkü bugünkü önizleme bozulmuyor ve yeni bağımlılık gelmiyor; karardan önce iki ölçüm gerekiyor (DZB-17).
- DZB-P12: üstte ikinci bir video izi (resim içinde resim) ve yeşil perde gelsin mi; öneri DZB-P8'in ölçümünden sonra karar, çünkü canlı önizlemesi orada seçilen yoldan geliyor (DZB-02, DZB-29).
- DZB-P15: çok iz açıldıktan sonra üstüne gelen işler (sesi ayırma, otomatik müzik kısma, seslendirme kaydı, iz denetimleri, boşluklu çizelge) kapsama girsin mi; öneri şimdi karar verilmesin, çünkü DZB-P7 ve DZB-P12 bitmeden açılamıyor.

### Dağıtım, İmza Ve Hesaplar

- DAG-P6: Windows kod imzası için SignPath Foundation'a (açık kaynak projelere ücretsiz imza veren kuruluş) başvurulsun mu, ücretli sertifika mı alınsın; öneri hemen ücretsiz başvuru, çünkü onay dışarıda bekliyor (DAG-02); önce README ile docs/imzalama.md arasındaki çelişki giderilmeli, biri başvuru bekliyor diyor, öbürü hiçbir şey gönderilmedi diyor.
- DAG-P5: winget, Scoop ve Chocolatey'de yayın yapılsın mı; öneri winget için evet, öbür ikisi sonra, çünkü winget öncelik 1 satırların en ucuzu (DAG-01) ve tek engel sahibin katkı sözleşmesi onayı ile önce bitmesi gereken DAG-P1.
- DAG-P8: Flathub'a (Linux'un yaygın uygulama mağazası) başvurulsun ve yayına AppImage (tek dosyalık kurulumsuz Linux paketi) eklensin mi; öneri evet ama aşağıdaki ikili dağıtımı sorusuyla birlikte, çünkü Flathub paketi ffmpeg ve libmpv'yi içinde taşıyor (DAG-04, DAG-09).
- DAG-P9, AYR-P8, DAG-P7: iki soru: ffmpeg ve libmpv ikilileri kendi yayın dosyalarımızda dağıtılsın ve taşınabilir paket çıksın mı (AYR-05, DAG-10); ön sürüm kanalı açılsın mı (AYR-09, DAG-24); öneri ilki için lisans yükümlülüğü çalışılmadan karar verilmesin, ikincisi için şimdilik hayır, çünkü ffmpeg'in GPL derlemesini kendi paketimizde dağıtmanın yükü bu turda çalışılmadı ve ön sürüm kanalı sürüm kesme düzenini değiştiriyor.
- DAG-P10: ücretli Apple geliştirici üyeliği alınsın mı; öneri macOS kaydedici kararı (EKA-P12) evet çıkarsa evet, yoksa beklesin, çünkü dmg, imza ve Homebrew zincirinin tamamı bu tek karara bağlı (DAG-05, DAG-06, DAG-07).
- DAG-P11: Microsoft Store geliştirici hesabı açılsın ve uygulama mağazaya girsin mi; öneri sonra, çünkü mağazanın exe yolu DAG-P6, DAG-P1 ve DAG-P9 bitmeden kapalı (DAG-03).
- PLT-P5: macOS'ta otomatik güncelleme varsayılan olarak açılsın mı; öneri gerçek Mac'te bir ölçüm turundan sonra evet, çünkü macOS davranışı bu turda yalnız koddan okundu (PLT-21).
- PLT-P6: Linux'ta bilerek kapalı tutulan uygulama içi güncelleme açılsın mı; öneri hayır, çünkü taranan rakiplerde Linux güncellemesini mağaza yapıyor ve DAG-P8 evet çıkarsa bizde de öyle olur (PLT-20, DAG-25).

### Model Ve Büyük İndirme

- ARC-P11, DON-P15, EKB-P11: yapay zekâ işleri için yeni bir çalışma zamanı (modeli çalıştıran kitaplık) ve yüzlerce MB model indirmesi kabul edilsin mi; öneri yalnız tek pilot için evet (büyütme; ARC-27, DON-32), indirme kullanıcının isteğiyle ilk kullanımda, kalanlar pilotun sonucuna göre, çünkü rakiplerin son iki yılda en çok eklediği şey bu ve ortak zemin üç paketi birden taşıyor.
- EKA-P11: gürültü bastırma modeli (RNNoise dosyası) kurulum paketine girsin mi; öneri önce süzgecin dağıtılan ffmpeg'de olup olmadığı ölçülsün, varsa evet, çünkü soru yalnız bir model dosyası ve yeni çalışma zamanı istemiyor (EKA-14).
- DZB-P16: uygulama kullanıcının diskine büyük ek dosya (vekil kopya, hazır içerik paketi, yapay zekâ modeli) üretsin ya da indirsin mi; öneri üçe bölünsün: vekil dosya (ağır videonun kurguda akıcı oynaması için üretilen küçük kopya; DZB-51) evet, hazır içerik şimdilik hayır, model parçaları pilotu beklesin, çünkü vekil indirme istemiyor ve en bağımsız parça.

### macOS Ve Linux Kaydedici

- PLT-P7, PLT-P8: ölçüm için bir Mac ve Intel ya da AMD ekran kartlı gerçek bir Linux makinesi ayrılsın mı; öneri evet ve bu temanın ilk kararı olsun, çünkü platform tablosunun tamamı yalnız koddan okundu (PLT-37, PLT-38, PLT-43); Mac'te varsayılan kodlayıcı değişikliği ölçüm sonucuna bırakılsın.
- PLT-P4: pakete izin açıklamaları eklenip kayıttan önce izin yoklaması gösterilsin mi; öneri evet ve öne alınsın, çünkü kamera ve bildirim izinleri birkaç pakette ayrı ayrı çıkıyor (EKA-P9, AYR-P5) ve tek yerde toplanmalı (PLT-04).
- EKA-P12, EKA-P13: macOS için ScreenCaptureKit (macOS'un ekranı, tek pencereyi ve sistem sesini veren yakalama arabirimi) kullanan yerel bir yardımcı ikili yazılıp pakete eklensin ve pencere yakalama da ona bağlansın mı; öneri ölçüm için Mac ayrıldıktan sonra evet, çünkü iki satır da öncelik 1 (EKA-03, EKA-04) ve ikinci paket birincisi hayır çıkarsa düşüyor.
- EKA-P14: Wayland için PipeWire portalını (Wayland'da ekran paylaşımına izin veren sistem kapısı) konuşan bir yardımcı ikili ya da yeni bağımlılık eklensin mi, yoksa açık bir uyarıyla mı yetinilsin; öneri önce uyarı, ikili kararı Linux ölçüm makinesinden sonra, çünkü bugün Wayland'da ekran ve bölge kaydı için hiç uyarı yok ve uyarı küçük bir iş (EKA-02).
- EKB-P17: macOS'ta girdi izleme izni istensin mi ve Wayland'da genel tuş dinleme yolu yoksa Linux desteği X11 ile mi sınırlansın; öneri macOS için PLT-P4'ün izin akışıyla birlikte evet, Linux'ta X11 ile sınırla, çünkü izin istemeyen küçük parça PLT-P3 ile zaten önden gidiyor (EKB-14).
- PLT-P9: Wayland'da genel kısayol için D-Bus istemcisi bağımlılığı eklensin mi; öneri EKA-P14 kararını beklesin, çünkü Wayland yakalama gelmeden açılmıyor (PLT-08).

### Araçlar Sekmesi Ve Yeni Araçlar

- ARC-P6, ARC-P12: yeni sekme açılsın mı: ortak bir Araçlar sekmesi ve kendi sekmesini isteyen araçlar (yinelenen video bulucu, altyazı düzenleyici); öneri tek Araçlar sekmesi evet (ARC-21, ARC-11, ARC-02, ARC-06), araç başına ayrı sekme şimdilik hayır (ARC-18, ARC-22), çünkü 16 satır zaten var olan sekmelere giriyor ve tek sekme kalan küçük araçlara yer açıyor.
- ARC-P8: altyazı metnini çevrimiçi çeviri hizmetine ya da yerel dil modeline veren araç ürüne girsin mi; öneri şimdilik hayır, çünkü çevrimiçi yol kullanıcının metnini dışarı gönderir, yerel yol model kararına bağlı (ARC-23).
- ARC-P9: başkasının logosunu ya da filigranını silmeye yarayan araç ürüne girsin mi; öneri şimdilik hayır, çünkü teknik engel yok ama soru ürünün adına yük getiren bir değer yargısı ve öncelik 2 (ARC-33).

### Oynatıcının Yönü

- OYB-P11, DAG-P12: uygulama dışarıdan yönetilebilen bir yüzey açsın mı: komut kanalı, Linux ortam denetimi, ağdan uzaktan denetim, betik ve eklenti, bir de tek örnek kuralına "yeni pencere" seçeneği; öneri yalnız yerel komut kanalı evet (OYB-40; karar istemeyen DAG-P4 aynı kanalı kuruyor), ağdan denetim ve eklenti hayır (OYB-39, OYB-41, DAG-36), çünkü ağda dinleyen kapı ve kalıcı arabirim sözü güvenlik ve bakım yükü getiriyor; Linux ortam denetimi (OYB-31) ve yeni pencere (OYB-35) ayrı küçük kararlar olarak sonra.
- OYB-P12: oynatıcı kitaplık ve ağ kaynağı yönüne büyüsün mü (adlı listeler, klasör tarayan kitaplık, site bağlantısı çözme, ağ paylaşımında gezme); öneri yalnız en küçük adım olan adlı listeler evet (OYB-09), kalanlar hayır, çünkü dört satır dört ayrı evet istiyor; site bağlantısı çözme (OYB-22) kapalı K1 ile aynı aracı kullandığı için burada önerilmiyor.
- OYB-P6: libmpv'nin kendi ortam denetimi anahtarı yetmezse Windows SDK bağı eklensin mi; öneri karar ölçümden sonra, çünkü ilk adım karar istemiyor: anahtar yazılıp ölçülür, yetmezse evet (OYB-29).
- OYA-P7: HDR video işlemcide ton eşlemeyle gösterilsin mi, 4K'da kare düşürürse yalnız elle açılan seçenek mi olsun; öneri önce bir HDR10 ve bir HLG örnekle ölçüm, kare düşmüyorsa varsayılan açık, düşüyorsa elle açılan seçenek, çünkü bu kapalı K4'ü açmadan yapılabilen orta yol (OYA-11).
- OYA-P8: sessiz yerleri hızlandıran yeni oynatma kipi eklensin mi; öneri şimdilik hayır, çünkü hazır sunan rakip bulunamadı ve istek yalnız oyla kanıtlı (OYA-37).

### Kaydedicide Kayıt Sonrası Ve Yakalama

- EKB-P9, EKB-P10: yakınlaştırma kayıttan sonra mı üretilsin (imleç ve tıklama verisi videodan ayrı kaydedilir), kayıt sırasında canlı mı; imleç kayda yakılmak yerine ayrı veri olarak tutulsun mu; öneri kayıt sonrası yol, çünkü imleç cilası yalnız bu yolda başlayabiliyor (EKB-03) ve otomatik yakınlaştırma 13 üründe görüldü (EKB-01).
- EKB-P8: kayıt çerçevesi için hazır arka plan görselleri pakete girsin mi, düz renk ve renk geçişiyle mi yetinilsin; öneri düz renk ve renk geçişi, çünkü pakete yeni içerik eklemeden işi görüyor (EKB-02).
- EKB-P14: kayıt bitince ekran köşesinde kendiliğinden yeni bir pencere açılsın mı ve varsayılan açık mı gelsin; öneri kapalı gelen seçenek, EKB-P3'ün sonuç paneli bittikten sonra (EKB-24).
- EKB-P15: uygulama kayıt sırasında sistemin bildirim ayarına ve masaüstü simgelerine dokunsun mu; öneri şimdilik hayır, çünkü Windows'ta bildirim susturmanın belgeli yolu doğrulanmadı ve satırın önceliği belirsiz (EKB-20, EKB-21).
- EKB-P16: kullanıcının girdiği sunucu adresi ve erişim anahtarı uygulamada saklansın mı, bulut hesabına tarayıcıdan giriş eklensin mi; öneri şimdilik hayır, çünkü öncelik 2 ve sır saklamak yeni bir güvenlik yükü (EKB-28, EKB-29).
- EKA-P2: uygulama başına ses eski Windows 10 yapılarında ve Windows dışında gizlensin mi, kapalı mı görünsün; öneri özellik evet, desteklenmeyen yerde gizlensin, çünkü PLT-P2 de işlemeyen kutuları gizliyor ve tek kural olur (EKA-01).
- EKA-P15: HDR ekranda varsayılan HDR dosya mı olsun, SDR'ye çevrilmiş kayıt mı; öneri önce ölçüm, varsayılan SDR, çünkü her yerde oynayan dosya güvenli taraf (EKA-08).
- EKA-P16: oyun sürecine kanca atan ayrı bir yakalama yolu yapılsın mı, ekran kaydı yolu ölçülüp yeterli mi sayılsın; öneri önce ölçüm, kanca yolu şimdilik hayır, çünkü tam ekran oyunun bugünkü yolla kaydı ölçülmedi (EKA-05).

### Küçült'te Kalite Ve Kodek

- SIK-P10, SIK-P11: işi uzatan tam kalite ölçümü (VMAF: görüntünün kaynağa benzerliğini 0 ile 100 arasında puanlayan ölçüt) ve ölçümle hedef kalite araması isteğe bağlı seçenek olarak girsin mi; öneri ölçüm için evet ve kapalı gelsin (SIK-14), arama onun sonucuna bakılarak sonra (SIK-15), çünkü arama aynı ölçümü her turda yeniden koşturuyor.
- SIK-P12: libvvenc içeren bir ffmpeg derlemesi dağıtılsın ve VVC (HEVC'nin ardılı yeni kodek) kodek listesine girsin mi; öneri şimdilik hayır, çünkü oynatma desteği dar ve komşu tabloda aynı satır öncelik 3 (SIK-16, DON-33).
- SIK-P13: Dolby Vision kapsamı donanım kodlayıcılarına genişlesin mi, gerekirse ffmpeg dışında ayrı bir araçla; öneri önce ölçüm, ayrı araç şimdilik hayır (SIK-19).

### Ayarlar Ve Kabuk

- AYR-P9: ana pencere için kapalı gelen bir "tepsiye küçült" seçeneği eklensin mi, kapatma düğmesi de tepsiye mi gitsin; öneri seçenek evet ve kapalı gelsin, kapatma düğmesi uygulamayı kapatmayı sürdürsün, çünkü güncelleme bugün uygulamanın çıkışında kuruluyor (AYR-22).
- AYR-P10: 42 dil nasıl sunulsun: gözden geçmemiş diller listede işaretlensin mi ve bir çeviri sitesinde proje hesabı açılsın mı; öneri işaret evet, hesap sonra, çünkü işaret kullanıcıya dürüst bilgi verir ve hesap gerektirmez (AYR-04, AYR-25).
- AYR-P11: "renk yalnız paletten, ölçü yalnız belirteçten" kuralına sistemin karşıtlık ve yazı boyutu ayarları için istisna açılsın mı; öneri önce AYR-P7'nin erişilebilirlik denemesi, sonra karar, çünkü bugün hiçbir deneme kaydı yok (AYR-12, AYR-13).
- AYR-P12: ilk açılışta bir karşılama ekranı ve dillerde sürdürülen çevrimiçi kılavuz sitesi açılsın mı; öneri karşılama ekranı evet, kılavuz sitesi hayır, çünkü 42 dilde site sürdürmek kalıcı bakım yükü (AYR-23, AYR-24).

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

## Aynı İş, İki Kol

Girdideki yinelenen satırlar paket düzeyine çevrildi. Aşağıdaki dört çift büyük ölçüde aynı iş; sondaki çift yargıdır, çünkü üç satırın ikisi ortak.

- EKA-P9 = EKB-P13: ikisi de kamera katmanını macOS ve Linux'a taşıyor ve aynı tek satıra dayanıyor (EKA-07, EKB-18); yalnız biri açılır.
- ARC-P7 = OYA-P12: küçük resim tablosu iki tabloda da aynı satır (ARC-07, OYA-44); soldaki buna kare çıkarmayı da eklediği için kapsamlı olan o.
- ARC-P11 = DON-P15: sağdaki paketin tek satırı olan yapay zekâ ile büyütme (DON-32) soldakinin pilotuyla aynı (ARC-27) ve sahibe sorulan soru aynı.
- ARC-P5 = DON-P14: etiket ve bölüm düzenleme iki tabloda da aynı satırlar (ARC-14 ve DON-29, ARC-15 ve DON-30); DON-31 sol pakete taşınır.

**Kısmen Örtüşenler**

- OYA-P5 ile OYB-P5: devam sınırı aynı satır (OYA-36, OYB-12) ve tek sabit; karar bir kez verilir, iş bir pakette yapılır. OYA-P9 da aynı geçmiş dosyasına yazıyor, üçü aynı anda açılmamalı.
- SIK-P4 ile DON-P7: dış ses izi ekleme aynı satır (SIK-03, DON-07); motor ortak, arayüz iki sekmede ayrı.
- SIK-P5 ile DON-P3: yakılan altyazıda biçim seçimi aynı süzgeç (SIK-04, DON-09); iş tahmini bir tabloda orta, öbüründe küçük.
- AYR-P8 ile DAG-P7: ön sürüm güncelleme kanalı aynı satır (AYR-09, DAG-24).
- AYR-P8 ile DAG-P9: taşınabilir paket aynı satır (AYR-05, DAG-10); öncelik bir tabloda 1, öbüründe 2.
- ARC-P4 ile DON-P8: resim dizisinden video aynı satır (ARC-09, DON-08).
- ARC-P7 ile DON-P8: numaralı kare çıktısı aynı satır (ARC-08, DON-23).
- ARC-P5 ile DZA-P11: kap üst verisini düzenleme ortak (ARC-14, DZA-24).
- ARC-P1 ile DZB-P5: aynı gürültü süzgeci (ARC-24, DZB-43); biri dosyada, öbürü düzenleyicide.
- ARC-P4 ile DZB-P14: ara kare üretimi ortak (ARC-28, DZB-50).
- ARC-P11 ile DZB-P16: arka plan silme ve yeniden kadraj aynı satırlar (ARC-29 ve DZB-70, ARC-30 ve DZB-69); ikisi de model kararına bağlı.
- ARC-P10 ile DZB-P10: aynı iş değil, sıra bağı; slayt gösterisi resim klibine (DZB-04), müzik izine (DZB-01) ve çapraz geçişe (DZB-16) dayanıyor.
- PLT-P3 ile EKB-P17: macOS tıklama işareti (PLT-10), EKB-14'ün izin istemeyen küçük parçası; iki tabloda iş ve karar hücresi ayrı.
- PLT-P9 ile EKA-P10: aynı satırın iki kolu (PLT-08 ve PLT-09, EKA-13); X11 ve macOS bir pakette, Wayland öbüründe.
- PLT-P4 ile EKA-P9: EKA-P9 karar istemez diye paketlendi ama macOS'ta Kamera izni istemi ister (PLT-06); izin anahtarları PLT-P4'te tek yerde toplanmalı.

Girdinin yinelenen listesinde olmayan, satır metninden görülenler:

- SIK-P1 ile AYR-P5: kodlama süreç önceliği ayarı iki tabloda da var (SIK-02, AYR-21).
- SIK-P7 ile AYR-P5: iş bitince bildirim sesi iki tabloda da var (SIK-10, AYR-20).
- DAG-P2 ile DON-P13: komut satırından dönüştürme (DAG-30, DON-28).
- DAG-P4 ile EKA-P5: komut satırından kayıt (DAG-31, EKA-16).
- DAG-P4 ile OYB-P11: çalışan uygulamaya dışarıdan komut (DAG-35, OYB-40); biri karar istemiyor, öbürü sahibi bekliyor.
- DAG-P12 ile OYB-P11: betik ve eklenti arabirimi (DAG-36, OYB-41).
- OYA-P3 ile SIK-P2: uyku engeli iki ayrı sekmede aynı sistem çağrısına dayanıyor (OYA-01, SIK-06).

**Platform Tablosunun Komşularla Bağı**

Platform tablosunun pakete girmeyen satırlarını komşu paketler taşıyor: PLT-02 işi EKA-P14'te, PLT-03 EKA-P12'de, PLT-05 EKA-P13'te, PLT-06 ve PLT-07 EKA-P9'da, PLT-09 EKA-P10'da, PLT-11, PLT-12 ve PLT-13 EKB-P17'de, PLT-15 ve PLT-16 EKA-P4'te, PLT-22 DAG-P10'da, PLT-23 DAG-P8'de, PLT-24 ve PLT-25 DAG-P9'da, PLT-30 AYR-P4'te, PLT-35 ve PLT-36 AYR-P5'te.

Aynı satırın iki tabloda çelişen hücreleri; birleştirirken tek değer seçilmeli:

- PLT-05 ile EKA-04: öncelik 2 ve 1.
- PLT-23 ile DAG-04: öncelik 2 ve 1.
- PLT-30 ile AYR-15: öncelik 3 ve 2.
- PLT-20 ile DAG-25: bir tabloda öncelik 2 ve sahip, öbüründe öncelik 3 ve gerekmez.
- PLT-06 ile EKA-07 ve EKB-18: Kamera izni yüzünden bir tabloda sahip, öbürlerinde gerekmez.
- PLT-35 ile AYR-19: bildirim izni yüzünden bir tabloda sahip, öbüründe gerekmez.
- PLT-08 ile EKA-13: D-Bus bağımlılığı yüzünden bir tabloda sahip, öbüründe gerekmez.
- PLT-10 ile EKB-14: bir tabloda küçük ve gerekmez, öbüründe büyük ve sahip.

## Kapalı Kararlar Ve Pazar

Kapalı bir karara 37 satır denk geldi. Aşağıdaki sıra satır sayısına göre değil, karar numarasına göredir.

### K1 — URL'den İndirme

Üç satır: DZA-43, DON-48, ARC-40. Girdide pazarın son iki yılda bu konuda neyi değiştirdiği yazılı değil; yalnız özelliği bugün sunan rakipler sayılmış (LosslessCut, Shutter Encoder, FFmpeg Batch AV Converter, Format Factory, VideoProc Converter AI, Any Video Converter).

Kapalı kalan: adresten dosya indirme. Sınırda bir satır var: oynatma için site bağlantısı çözme (OYB-22) aynı aracı kullanıyor ama karar bekleyen olarak bırakıldı; sahip K1'i oynatmayı da kapsar sayarsa o da kapanır. Orta yol görülmedi. Kararı yeniden açmak sahibin işidir.

### K3 — Pencereyi WGC İle Yakalama

Bir satır: EKA-17. WGC, Windows'un tek pencereyi yakalamaya yarayan arabirimi. Pazarda değişen: ffmpeg 8.1 ile gelen gfxcapture süzgeci bu arabirimi veriyor ve sarı kenarlık seçeneği varsayılan olarak kapalı; kurucunun pinlediği sürüm ffmpeg 9.0 olduğu için karar açılırsa yeni bağımlılık gerekmiyor (EKA-17).

İki şey ölçülmedi: süzgecin pinli derlemede açık olduğu ve kenarlığın paketsiz uygulamada kapandığı. Rafta kalan: pencere yakalama. Kararı yeniden açmak sahibin işidir.

### K4 — Oynatıcıda GPU Çizim Yolu

Sekiz satır: OYA-12, OYA-13, OYA-14, OYA-16, OYA-17, OYA-18, OYA-19, OYA-20. Bu satırlardaki özellikler dokuz oynatıcıda görüldü (mpv, IINA, PotPlayer, mpv.net, VLC, MPC-HC, MPC-BE, Infuse, Screenbox); girdide son iki yıla özgü bir değişiklik ayrıca yazılı değil.

Kapalı kalan: görüntünün ekran kartında çizilmesine bağlı işler. Orta yol: HDR'yi işlemcide ton eşlemeyle göstermek (OYA-P7, OYA-11); karar bekliyor ve önce ölçüm istiyor. Kararı yeniden açmak sahibin işidir.

### K5 — Otomatik Altyazı

Dokuz satır: OYA-35, DZA-42, DZB-63, DZB-68, SIK-45, DON-52, EKB-37, EKB-38, ARC-41. Aynı satır altı tabloda yineleniyor (ARC-41, DON-52, SIK-45, DZB-63, EKB-37, OYA-35). Konuşmadan metne altyazı kurgu programlarında (DZB-63: Shotcut, Kdenlive, Clipchamp, CapCut ve 5 rakip daha) ve ekran kaydedicilerde (EKB-37: Screen Studio, Cap, Bandicam, Camtasia ve 6 rakip daha) yaygın.

Kapalı kalan: konuşmayı metne çeviren model. Girdi orta yol olarak üç paketi gösteriyor: DZB-P9 (DZB-62), ARC-P6 (ARC-21) ve DZA-P3; ilki ve sonuncusu karar istemiyor, ortadaki sekme kararını bekliyor. Kararı yeniden açmak sahibin işidir.

### K8 — Stabilizasyon, LUT Ve Filigran

Sekiz satır: stabilizasyon DZB-31, ARC-42; LUT (hazır renk tablosu dosyası) DZB-32, SIK-46, DON-50; filigran DZB-33, DON-51, ARC-43. Girdide son iki yıla özgü bir değişiklik yazılı değil; SIK-46'da rakip doğrulanmadı, istek HandBrake'te 10 oyla duruyor.

Rafta kalan: üç iş de. Orta yol: DZB-P3 hazır görünümleri LUT dosyası kullanmadan, süzgeç ön ayarıyla veriyor (DZB-30). Kararı yeniden açmak sahibin işidir.

### K9 — Ekran Görüntüsü Aracı

Bir satır: EKB-39 (Cap, ShareX, CleanShot X, Snagit). Girdide son iki yıla özgü bir değişiklik yazılı değil. Kapalı kalan: ayrı bir ekran görüntüsü aracı. Orta yol görülmedi. Kararı yeniden açmak sahibin işidir.

### K10 — Chromecast Ve DLNA

İki satır: OYB-25, OYB-26. Chromecast ve DLNA, videoyu ağdaki televizyona ya da başka cihaza gönderme yollarıdır. Pazar bu yönde hareket ediyor: MPC-HC 2026'da Chromecast ve DLNA yayınını deneme sürümüyle ekliyor, IINA'nın en çok oy alan iki açık isteği bu konuda (527 ve 307 oy; OYB-26); VLC 4 geliştirme dalı UPnP sunucu desteği ekliyor, IINA'da 110 oyluk açık istek var (OYB-25).

Kapalı kalan: başka cihaza yayın. Belirsiz olan: karar DLNA'yı adıyla sayıyor ama ağdaki sunucudan okuma yönü ayrıca sorulmamış (OYB-25). Orta yol görülmedi. Kararı yeniden açmak sahibin işidir.

### DVD Ve Blu-ray

İki satır: DON-49, ARC-44. Girdide pazar değişikliği yazılı değil. Not: kodda DVD-Video başlığı okuyan bir yol duruyor (SourceTitles.cs:28); karar metninin bu yolu kapsayıp kapsamadığı netleşmeli. Kararı yeniden açmak sahibin işidir.

### macOS Alt Sürümü 14

Bir satır: DAG-23. Pazar notu: HandBrake'in indirme sayfası alt sınırı macOS 10.13 gösteriyor. Kapalı kalan: 14'ten eski macOS desteği. Kararı yeniden açmak sahibin işidir.

### Dolby Vision Profil 5

Bir satır: SIK-34 (HandBrake). Kapalı kalan: profil 5'in kapsam dışı sayılması. Komşu işler açık duruyor: SIK-P13 karar bekliyor, SIK-17 ve SIK-33 ayrı satırlar. Kararı yeniden açmak sahibin işidir.

### 85 Ön Ayar

Bir satır: DON-47, ve satır kararsız. Karar metni HandBrake'in 85 ön ayarını birebir eşlemeyi reddediyor, cihaz hedeflerinin tümünü değil; ortak karar listesinde bu kalem ayrıca yok. Sahip "bir avuç cihaz hedefi olur" derse satır öncelik 2 olur ve DON-P5'in yanına girer. Kararı yeniden açmak sahibin işidir.

Hiç satır düşmeyen kararlar: K2, K6, K7, K11, K12, BM3D, Max sıkıştırma kipi, oynatıcıda donanım çözme varsayılanı (EKB-32'nin K11'e girip girmediği belirsiz; karar bekleyen olarak bırakıldı).

Kapalı bir karara denk gelen 37 satırın tamamı:

| No | Eksik | Karar | Kimde Var |
|---|---|---|---|
| OYA-12 | Ton eşleme ayarları: ton eşleme yöntemini ve hedef tepe parlaklığı seçme. | kapalı: K4 | mpv, IINA, PotPlayer, mpv.net |
| OYA-13 | HDR çıkışı: HDR ekranda videoyu HDR olarak gösterme. | kapalı: K4 | mpv, VLC, MPC-HC, IINA |
| OYA-14 | Dolby Vision renk dönüşümü: Dolby Vision dosyanın rengini kendi üst verisiyle çözme. | kapalı: K4 | mpv, MPC-HC, MPC-BE, Infuse ve 1 rakip daha |
| OYA-16 | Kare ara değerleme ve ekranla eşzamanlama: video kare hızını ekranın yenileme hızına uydurup takılmayı azaltma. | kapalı: K4 | mpv, mpv.net |
| OYA-17 | Shader yükleme: görüntüyü ekran kartında işleyen küçük program dosyalarını oynatıcıya takma. | kapalı: K4 | mpv, MPC-HC |
| OYA-18 | Ekran kartıyla büyütme ve SDR'den HDR üretme: RTX Video Super Resolution, AMD ve benzeri sürücü özellikleri. | kapalı: K4 | VLC, mpv, Infuse, Screenbox |
| OYA-19 | Ölçekleyici ve bant giderme seçimi: büyütme süzgecini seçme ve renk basamaklarını yumuşatma. | kapalı: K4 | mpv, VLC |
| OYA-20 | Renk yönetimi: görüntüyü ekranın ICC renk profiline göre düzeltme. | kapalı: K4 | mpv, IINA, VLC |
| OYA-35 | Yapay zekâ ile altyazı üretimi: konuşmadan kendiliğinden altyazı çıkarma. | kapalı: K5 | PotPlayer, GOM Player |
| OYB-25 | UPnP ve DLNA sunucusundan okuma: ağdaki ortam sunucularını bulup içeriğini listeleme ve oynatma. | kapalı: K10 — karar DLNA'yı adıyla sayıyor, okuma yönü ayrıca sorulmadı; pazar: VLC 4 geliştirme dalı UPnP sunucu desteği ekliyor, IINA'da 110 oyluk açık istek var | VLC, Infuse |
| OYB-26 | Chromecast ve AirPlay yayını: çalan videoyu televizyona ya da hoparlöre gönderme. | kapalı: K10 — pazar: MPC-HC 2026'da Chromecast ve DLNA yayınını deneme sürümüyle ekliyor, IINA'nın en çok oy alan iki açık isteği bu (527 ve 307) | Screenbox, VLC, SMPlayer, Stremio ve 1 rakip daha |
| DZA-42 | Konuşma metninden kesim: dolgu sözcükleri ve tekrar çekimleri konuşmayı yazıya döküp bulma. | kapalı: K5 | TimeBolt, Gling, Clipchamp |
| DZA-43 | Akış adresinden kayıpsız indirme: HLS adresindeki videoyu yeniden kodlamadan dosyaya alma. | kapalı: K1 | LosslessCut |
| DZB-31 | Stabilizasyon: titrek çekimi yazılımla sabitlemek. | kapalı: K8 | Shotcut, DaVinci Resolve |
| DZB-32 | LUT: hazır renk tablosu dosyasıyla görünüm uygulamak. | kapalı: K8 | OpenShot, Shotcut |
| DZB-33 | Filigran ve logo: görüntünün köşesine sabit resim ya da yazı bindirmek. | kapalı: K8 | OpenShot, Clipchamp |
| DZB-63 | Otomatik altyazı: konuşmayı yazıya çevirip altyazı üretmek. | kapalı: K5 | Shotcut, Kdenlive, Clipchamp, CapCut ve 5 rakip daha |
| DZB-68 | Metin üzerinden kurgu: konuşmanın dökümünde sözcük silince videonun da kesilmesi. | kapalı: K5 | Descript, Clipchamp, VEGAS, CapCut ve 3 rakip daha |
| SIK-34 | Dolby Vision profil 5: HDR10 tabanı olmayan akış profili taşınır. | kapalı: Dolby Vision profil 5 kapsam dışı hükmü | HandBrake |
| SIK-45 | Konuşmadan altyazı üretme: videodaki konuşma yazıya dökülüp altyazı yapılır. | kapalı: K5 | Shutter Encoder |
| SIK-46 | LUT uygulama: renk dönüşüm tablosu (LUT) kodlama sırasında görüntüye uygulanır. | kapalı: K8 | doğrulanmadı (istek HandBrake'te 10 oy) |
| DON-47 | Cihaz ve platform ön ayarları: telefon, konsol, televizyon ya da site adına göre hazır Dönüştür ayarı seçmek. | kapalı: 85 ön ayar bilerek yapılmaz | HandBrake, XMedia Recode, Wondershare UniConverter, VideoProc Converter ve 3 rakip daha |
| DON-48 | URL'den indirme: bağlantıdaki videoyu indirip aynı adımda dönüştürmek. | kapalı: K1 | Shutter Encoder, FFmpeg Batch AV Converter, Format Factory |
| DON-49 | DVD ve Blu-ray: diskten ya da disk klasöründen okuyup dönüştürmek. | kapalı: DVD ve Blu-ray bilerek yapılmaz | Shutter Encoder, Format Factory, Any Video Converter |
| DON-50 | LUT uygulama: hazır renk tablosunu (LUT) dönüşümde görüntüye uygulamak. | kapalı: K8 | Shutter Encoder |
| DON-51 | Filigran: dönüşümde videoya logo ya da yazı basmak. | kapalı: K8 | Shutter Encoder |
| DON-52 | Otomatik altyazı: konuşmayı yazıya döküp altyazı üretmek. | kapalı: K5 | Shutter Encoder, Permute |
| EKA-17 | Windows'ta gerçek pencere yakalama: pencereyi WGC ile, üstü örtülse de kaydetme ve taşınınca izleme. | kapalı: K3; pazar notu: ffmpeg 8.1 ile gelen gfxcapture süzgeci WGC'yi veriyor ve sarı kenarlık seçeneği varsayılan kapalı | OBS, Bandicam |
| EKB-37 | Otomatik altyazı: konuşmayı cihazda yazıya çevirip altyazı olarak ekler. | kapalı: K5 | Screen Studio, Cap, Bandicam, Camtasia ve 6 rakip daha |
| EKB-38 | Metinden kurgu: konuşmanın yazısından sözcük silerek videoyu keser, dolgu sözcükleri kendiliğinden atar. | kapalı: K5 | Descript, Tella, Camtasia, Bandicam ve 1 rakip daha |
| EKB-39 | Ekran görüntüsü kipi: kaydediciden durağan görüntü alır, arka plan ve gölgeyle güzelleştirir. | kapalı: K9 | Cap, ShareX, CleanShot X, Snagit |
| DAG-23 | Eski macOS desteği: alt sınır macOS 14. | kapalı: macOS alt sürümü 14 (docs/YOL-HARITASI.md:72) | HandBrake, LosslessCut, OBS Studio |
| ARC-40 | URL'den indirme: yapıştırılan bağlantıdaki videoyu dosya olarak indiren araç. | kapalı: K1 | Shutter Encoder, VideoProc Converter AI, Any Video Converter, Format Factory |
| ARC-41 | Konuşmadan metne: videodaki konuşmayı yazıya döküp altyazı dosyası üreten araç. | kapalı: K5 | Shutter Encoder, UniConverter, Movavi Video Suite, HitPaw Univd ve 5 rakip daha |
| ARC-42 | Stabilizasyon: titrek çekimi yazılımla sabitleyen araç. | kapalı: K8 | VideoProc Converter AI, Any Video Converter, HitPaw Univd, Kapwing ve 1 rakip daha |
| ARC-43 | Filigran ekleme: videonun üstüne logo ya da yazı basan araç. | kapalı: K8 | UniConverter, Any Video Converter, Aiseesoft, HitPaw Univd |
| ARC-44 | Disk yazma ve kopyalama: DVD, Blu-ray ve müzik CD'sine yazan ya da diskten dosyaya kopyalayan araç. | kapalı: DVD ve Blu-ray | Shutter Encoder, UniConverter, Movavi Video Suite, Format Factory ve 2 rakip daha |

## Yöntem Ve Sınırlar

- 12 araştırma kolu aynı ortak kuralla çalıştı: önce koddan envanter (her "bizde var" satırı `dosya:satır` taşır), sonra rakip taraması, sonra eksik tablosu. Kurallar: [yontem-arastirma.md](piyasa/2026-10-13/yontem-arastirma.md).
- Son kol (Platform Eşitliği) öteki kollardan farklı bir soruya baktı: pazarda olup bizde hiç olmayana değil, bizde Windows'ta olup macOS ya da Linux'ta eksik kalana. Öteki kolların aynı farkı yazdığı satırlar Aynı İş bölümünde eşlendi.
- Her tabloyu ayrı bir doğrulama kolu satır satır ölçtü: bizde gerçekten yok mu (kodda en az üç adla arama), önceki bir karara denk geliyor mu, öncelik 1 satırın kaynağı açılıyor mu. Kurallar: [yontem-dogrulama.md](piyasa/2026-10-13/yontem-dogrulama.md).
- Doğrulamada 567 satırın hiçbiri bizde zaten var çıkmadı; 29 satırın önceliği değişti.
- Değerlendirme bölümlerini (Genel Değerlendirme, İlk On Paket, Sahibin Kararları, Aynı İş, Kapalı Kararlar) tek bir sentez kolu yazdı; sayılar, tablolar ve dalgalar betikten çıktı. Kurallar: [yontem-sentez.md](piyasa/2026-10-13/yontem-sentez.md).
- İş büyüklüğü (küçük, orta, büyük) tahmindir, ölçüm değildir.
- Uygulama çalıştırılmadı: bizdeki davranış koddan okundu, ekranda denenmedi. Rakip özelliği de kurulup denenmedi; resmi sayfadan, belgeden ya da sürüm notundan okundu.
- Görülemeyen şeye "rakipte yok" denmedi, "doğrulanamadı" dendi. Kol kol liste doğrulanmış tabloların Doğrulanamayanlar bölümünde.
- Kod okuması 9de48d8b üzerinde yapıldı; v1.2.16 (eb0a5db0) ile arasındaki fark yalnız sürüm numarası ve değişiklik günlüğü.
- Doğrulama turu hiçbir satırı "bizde zaten var" diye düşürmediği için ana oturum "yok" denen satırlardan rastgele 20 tanesini kaynakta kendisi aradı; 20 tanesi gerçekten yok çıktı. Bu bir yoklamadır, kanıt değil.
- Koşulan tek komut ffmpeg süzgeç listesidir; çıktısı kod okuması dosyasının sonunda.
- Ajan raporu iddiadır. Ana oturum 349 iddiayı kendisi koddan açıp okudu, hepsi tuttu; öncelik 1 kalan 78 satırın hepsi, ilk iki dalganın paketlerindeki 91 satırın hepsi, üçüncü dalganın paketlerindeki 115 satırın hepsi, dördüncü dalganın paketlerindeki 29 satırın hepsi bu okumanın ya da yukarıdaki yoklamanın içinde. Bütün satırlarda sayı 567: 381 satırı ana oturum da okudu, 186 satır yalnız doğrulama kolunun okumasına dayanıyor. Liste: [t0-kod-okumasi.md](piyasa/2026-10-13/t0-kod-okumasi.md).
- Bazı araştırma kolları işi kendi içinde böldü: 13 alt kol açıldı, 10 tanesi rapor döndü. Alt kolların bulguları açan kolun notlarına girdi; görev metinleri ve raporları yazışma dosyasında.

- Platform tablosunun tamamı yalnız koddan okundu; macOS ve Linux'ta hiçbir satır gerçek makinede görülmedi. PLT-01 kayıtla doğrulanmadı; doğru çıkmazsa PLT-P1 ilk on listesinden düşer. PLT-04'te macOS mikrofonu çocuk sürece vermiyorsa satır öncelik 1 olur ve PLT-P4 öne geçer.
- SIK-04'ün öncelik 1'i yalnız 40 oya dayanıyor ve ölçütte oy eşiği yok; rakip sayısı esas alınırsa öncelik 2 olur ve SIK-P5 Dalga 2'den düşer.
- AYR-01 ve AYR-05'in oy kanıtı tek ve video dışı bir üründen (PowerToys); sayılmazsa AYR-01 öncelik 2 olur ve AYR-P2 Dalga 3'e iner. AYR-05 komşu tabloda zaten 2 (DAG-10).
- Süzgeç varlığı ölçülmedi: tile, minterpolate, ebur128, showwaves, delogo, framemd5, arnndn ve libvvenc'in dağıtılan ffmpeg'de olup olmadığı bilinmiyor; Windows x64 ve arm64 derlemeleri ayrı kaynaktan geliyor. Eksik çıkan süzgeç ARC-P1, ARC-P4, ARC-P7, ARC-P10, EKA-P11 ve SIK-P12'yi derleme kararına bağlar; ARC-P1 ilk on listesinde.
- OYA-11: HDR dosyanın bugün oynatıcıda nasıl göründüğü ölçülmedi; ton eşleme süzgeçlerinin libmpv derlemesinde olup olmadığı bilinmiyor. OYA-P7'nin büyüklüğü buna bağlı.
- DZB-01 ve DZB-17: mpv'nin süzgeç grafiği ile bugünkü önizleme yolunun birlikte çalışıp çalışmadığı ölçülmedi; DZB-P7'nin "orta" tahmini ve DZB-P8'deki yol seçimi buna bağlı.
- DZA-10 ve DZA-39: monitörün sağ tık menüsü çalışırken görülmedi; açılmıyorsa iki satır "yok"a döner ve DZA-P5 büyür.
- EKA-05: tam ekran oyunun bugünkü yolla kaydı ölçülmedi; kusur çıkarsa satır öncelik 1 olur ve EKA-P16 öne gelir.
- OYB-22'nin kapalı K1'e girip girmediği kararsız; kapalı sayılırsa OYB-P12'den düşer. OYB-29'da libmpv'nin ortam denetimi anahtarının gömülü kullanımda çalışıp çalışmadığı bilinmiyor (OYB-P6).
- Dağıtım tablosunda on bir satır üç ve daha çok rakipte belgeli olduğu halde öncelik 2 taşıyor; bu bir yargıdır. En güçlü aday DAG-10: öncelik 1 olursa DAG-P9 öne gelir. EKB-20'de bir rakip sayfası açılamadı; öncelik 2 mi 3 mü belirsiz (EKB-P15).

## Kanıt Dosyaları

| Kol | Notlar Ve Kaynaklar | Ham Tablo | Doğrulanmış Tablo |
|---|---|---|---|
| Oynatıcı — Oynatma Çekirdeği | [oynatici-cekirdek.md](piyasa/2026-10-13/oynatici-cekirdek.md) | [oynatici-cekirdek-eksikler.md](piyasa/2026-10-13/oynatici-cekirdek-eksikler.md) | [oynatici-cekirdek-dogrulanmis.md](piyasa/2026-10-13/oynatici-cekirdek-dogrulanmis.md) |
| Oynatıcı — Liste, Kitaplık Ve Ağ | [oynatici-kitaplik.md](piyasa/2026-10-13/oynatici-kitaplik.md) | [oynatici-kitaplik-eksikler.md](piyasa/2026-10-13/oynatici-kitaplik-eksikler.md) | [oynatici-kitaplik-dogrulanmis.md](piyasa/2026-10-13/oynatici-kitaplik-dogrulanmis.md) |
| Düzenleyici — Kayıpsız Kesim Ve Birleştirme | [duzenleyici-kesim.md](piyasa/2026-10-13/duzenleyici-kesim.md) | [duzenleyici-kesim-eksikler.md](piyasa/2026-10-13/duzenleyici-kesim-eksikler.md) | [duzenleyici-kesim-dogrulanmis.md](piyasa/2026-10-13/duzenleyici-kesim-dogrulanmis.md) |
| Düzenleyici — Kurgu Yönü | [duzenleyici-kurgu.md](piyasa/2026-10-13/duzenleyici-kurgu.md) | [duzenleyici-kurgu-eksikler.md](piyasa/2026-10-13/duzenleyici-kurgu-eksikler.md) | [duzenleyici-kurgu-dogrulanmis.md](piyasa/2026-10-13/duzenleyici-kurgu-dogrulanmis.md) |
| Küçült — Küçültme, Önizleme Ve Kuyruk | [kucult.md](piyasa/2026-10-13/kucult.md) | [kucult-eksikler.md](piyasa/2026-10-13/kucult-eksikler.md) | [kucult-dogrulanmis.md](piyasa/2026-10-13/kucult-dogrulanmis.md) |
| Dönüştür — Dönüştürme Ve Gelişmiş | [donustur.md](piyasa/2026-10-13/donustur.md) | [donustur-eksikler.md](piyasa/2026-10-13/donustur-eksikler.md) | [donustur-dogrulanmis.md](piyasa/2026-10-13/donustur-dogrulanmis.md) |
| Kaydedici — Yakalama | [kaydedici-yakalama.md](piyasa/2026-10-13/kaydedici-yakalama.md) | [kaydedici-yakalama-eksikler.md](piyasa/2026-10-13/kaydedici-yakalama-eksikler.md) | [kaydedici-yakalama-dogrulanmis.md](piyasa/2026-10-13/kaydedici-yakalama-dogrulanmis.md) |
| Kaydedici — Sunum, Kayıt Sonrası Ve Paylaşım | [kaydedici-sunum.md](piyasa/2026-10-13/kaydedici-sunum.md) | [kaydedici-sunum-eksikler.md](piyasa/2026-10-13/kaydedici-sunum-eksikler.md) | [kaydedici-sunum-dogrulanmis.md](piyasa/2026-10-13/kaydedici-sunum-dogrulanmis.md) |
| Ayarlar Ve Kabuk — Ayarlar, Hakkında Ve Pencere | [ayarlar-kabuk.md](piyasa/2026-10-13/ayarlar-kabuk.md) | [ayarlar-kabuk-eksikler.md](piyasa/2026-10-13/ayarlar-kabuk-eksikler.md) | [ayarlar-kabuk-dogrulanmis.md](piyasa/2026-10-13/ayarlar-kabuk-dogrulanmis.md) |
| Dağıtım Ve Otomasyon — Kurulum, Güncelleme Ve Komut Satırı | [dagitim-otomasyon.md](piyasa/2026-10-13/dagitim-otomasyon.md) | [dagitim-otomasyon-eksikler.md](piyasa/2026-10-13/dagitim-otomasyon-eksikler.md) | [dagitim-otomasyon-dogrulanmis.md](piyasa/2026-10-13/dagitim-otomasyon-dogrulanmis.md) |
| Bizde Olmayan Araçlar — Yeni Araç Adayları | [eksik-araclar.md](piyasa/2026-10-13/eksik-araclar.md) | [eksik-araclar-eksikler.md](piyasa/2026-10-13/eksik-araclar-eksikler.md) | [eksik-araclar-dogrulanmis.md](piyasa/2026-10-13/eksik-araclar-dogrulanmis.md) |
| Platform Eşitliği — Windows'ta Olup macOS Ya Da Linux'ta Eksik Kalanlar | [platform-esitligi.md](piyasa/2026-10-13/platform-esitligi.md) | [platform-esitligi-eksikler.md](piyasa/2026-10-13/platform-esitligi-eksikler.md) | [platform-esitligi-dogrulanmis.md](piyasa/2026-10-13/platform-esitligi-dogrulanmis.md) |

Ajanlara giden görev metinleri ve dönen son raporlar: [ajan-yazismalari.md](piyasa/2026-10-13/ajan-yazismalari.md).
Sentez kolunun yazdığı metin, birebir: [sentez.md](piyasa/2026-10-13/sentez.md). Bu belgedeki değerlendirme bölümleri oradan alındı.
Ana oturumun kendi okuduğu kod satırları: [t0-kod-okumasi.md](piyasa/2026-10-13/t0-kod-okumasi.md).
Sahibe bağlı 65 paketin hangisinin önceki kararlarla zaten cevaplandığı, hangisinin sorulacağı, hangisinin ölçüm beklediği ayrı bir danışmada ayıklandı (claude-fable-5-1): [danisma/017-fable-pazar-karar-sorulari.md](danisma/017-fable-pazar-karar-sorulari.md). O kayıt 12000 karakterde kesilmiş; cevabın tamamı ajan yazışmalarında duruyor. Danışmaya giden metin, birebir: [danisma/017-fable-pazar-karar-sorulari-girdi.md](danisma/017-fable-pazar-karar-sorulari-girdi.md).
Sayıları ve bu belgeyi üreten betik: `tools/pazar-plani/topla.py` (nasıl koşulur: `tools/pazar-plani/AGENTS.md`).
