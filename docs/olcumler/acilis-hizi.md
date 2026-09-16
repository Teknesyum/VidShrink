# Açılış hızı — çift tıktan ilk kareye

Tarih: 2026-09-12. Düzenek: `tools/acilis-hizi/olcum.ps1`. Dalga: 7c.
Makine: DESKTOP-0J80KVV / Windows NT 10.0.22631.0.

Ölçülen şey tek bir şey: kullanıcı bir mp4'e çift tıkladığında ilk karenin ekrana
düşmesi ne kadar sürüyor. Sayılar `.calisma/dalga7c/` altındaki ham CSV'lerden çıkar.

## Sıfır noktası ve ilk kare

Sıfır noktası `Process.GetCurrentProcess().StartTime`. Kabuğun `CreateProcess`
çağrısından **önce** harcadığı süre bu ölçünün dışında; buradaki sayılar
"süreç doğduktan sonra" geçen zamandır, çift tıkın kendisinden değil.

İlk kare işareti `Player.Frame.Source`'un dolmasıdır. `MainWindow` ile `PlayerView`
aynı derlemede olduğu için bu alan dışarıdan okunabiliyor; böylece 7a ve 7b'nin
yazdığı `Playback/` dosyalarının hiçbirine dokunmadan ölçüm alınabildi. Kanca
`MainWindow.AcilisIzi.cs` içinde ve `VIDSHRINK_ACILIS_IZI` değişkeni boşken
hiçbir iş yapmaz.

p95 en yakın sıra yöntemiyle, `ceil(0.95*n)`.

## Neden eşleşik ölçüm

İlk denemede "önce" ve "sonra" ayrı oturumlarda ölçüldü ve sonuç **yanıltıcı**
çıktı: iyileştirilmiş yapı taban yapıdan daha yavaş göründü. Ama aynı tabloda
değişikliğin eline hiç değmediği adımlar da şişmişti — çerçeve kurulumu, XAML
açılımı. Bu, kodun değil makinenin kaydığını söylüyordu.

Kayma küçük değil. Aynı taban ikilisi, aynı klip, aynı makine:

| Oturum | `ilk-kare` ortanca |
| --- | --- |
| 12:58, ayrı oturum | 3355,2 ms |
| 13:0x, eşleşik oturum | 1971,2 ms |

Aynı ikili iki oturumda 1,7 kat fark veriyor. **Bu makinede eşleşmemiş
önce/sonra karşılaştırması geçersizdir.**

Bunun üzerine düzenek eşleşik hale getirildi: her tekrarda iki yapı da koşuyor ve
sıra tekrardan tekrara dönüyor. Arka plan işi, disk, termal durum — hepsi iki
yapıya aynı anda vuruyor. Karar, tekrar içi farkların ortancasına ve farkın kaç
tekrarda aynı yöne baktığına bakılarak veriliyor.

Taban yapı ayrı bir worktree'de `0197f734`'ten derlendi; iki ikili de Release.

## Sonuç

### Sıcak (n = 12 çift)

| Adım | Taban ortanca | İyileştirme ortanca | Taban p95 | İyileştirme p95 |
| --- | --- | --- | --- | --- |
| `ilk-kare` | 1971,2 | 1857,8 | 3475,0 | 3045,8 |

Eşleşik fark `ilk-kare`: **ortanca −240,7 ms**, aralık −429,2 … −21,9,
**12 çiftin 12'si** iyileştirme lehine.

### Soğuk (n = 8 çift)

| Adım | Taban ortanca | İyileştirme ortanca | Taban p95 | İyileştirme p95 |
| --- | --- | --- | --- | --- |
| `ilk-kare` | 1765,0 | 1586,0 | 1900,0 | 1734,2 |

Eşleşik fark `ilk-kare`: **ortanca −177,0 ms**, aralık −266,2 … −22,3,
**8 çiftin 8'i** iyileştirme lehine.

İki kip birbirini doğruluyor ve ikisinde de tek bir tekrar bile ters yöne
bakmıyor.

Ortanca sütunları eşleşmemiş sayılardır ve oturum kaymasını taşır; hükmü veren
sütun ortancalar değil, **eşleşik fark**tır.

## Hangi değişiklik kaç ms getirdi

İzdeki işaretler birikimli: bir adımın farkı kendinden öncekilerin farkını da
içerir. Aşağıdaki pay, ardışık iki işaretin farkı çıkarılarak bulundu.

| Değişiklik | Nerede görünüyor | Sıcak | Soğuk |
| --- | --- | --- | --- |
| B — `TempCleanup` arayüz iş parçacığından alındı | `ayar-okundu` | **−94 ms** | **−73 ms** |
| C — varsayılan uygulama önerisi ve güncelleme yoklaması ilk karenin arkasına alındı | `giris-canlandirmasi` | **−46 ms** | **−32 ms** |
| A — libmpv arka planda önden yüklendi | `ilk-kare` | **−44 ms** | **−7 ms** |
| Atfedilemeyen kalan | pencere kurulumu boyunca | −56 ms | −65 ms |
| **Toplam** | `ilk-kare` | **−240 ms** | **−177 ms** |

Paylar ölçülen toplama birebir oturuyor: −94−46−44−56 = −240 ve −73−32−7−65 = −177.

`ayar-okundu` farkı sıcakta 12/12, soğukta 8/8 aynı yöne bakıyor: B ölçünün en
sağlam parçası.

Son satır dürüstlük payıdır. Pencere kurulumu boyunca biriken bu fark tek bir
değişikliğe bağlanamıyor; en makul açıklama, artık eşzamanlı koşmayan geçici
dosya taramasının pencere kurulurken diski meşgul etmemesi, ama bu **ölçülmedi**,
çıkarım.

## Beklenip çıkmayan: libmpv 115 MB değil, 500 ms motor

Açılışın uzun kuyruğunun 115 MB'lik yerel kitaplığın okunmasından geldiği
tahmin edilmişti. Ölçü bunu **çürüttü**: önden yükleme açıkken `libmpv-hazir`
işareti sıcakta 74,0 ms, soğukta 67,1 ms ortancayla düşüyor. Kitaplık, ihtiyaç
duyulmadan çok önce hazır.

Dolayısıyla `sekme` ile `ilk-kare` arasındaki ~330 ms kitaplık yüklemesi değil,
`mpv_create` ve ilk çözme işidir. A'nın 7–44 ms'lik dar katkısı da bunu söylüyor:
önden yüklemenin kazanabileceği tek şey zaten küçüktü.

Bu kuyruğun asıl gövdesi `Playback/` altındaki motor açılışında ve 7c'nin
dokunmaması gereken dosyalarda duruyor.

## Okuma tuzakları

**`varsayilan-oneri` +1597 ms görünüyor — bu yavaşlama değil.** O işaret C ile
bilerek `LoadStartupFileAsync`'in arkasına taşındı, yani artık tasarım gereği
daha geç yazılıyor. Aynı sebeple n'i kısa: ilk kareden sonra bırakılan 1500 ms'lik
kuyruk bazı koşumlarda o işaretten önce bitiyor. `kucultme-yuklendi` satırının
n'i de aynı kuyruk yüzünden eksik.

**Soğuk, disk soğukluğu değildir.** Soğuk kipte yayın klasörü her tekrar için yeni
bir yola kopyalanır; sürecin imaj eşlemesi sıfırdan kurulur ama işletim sisteminin
sayfa önbelleği kopyalama sırasında zaten ısınır. Buradaki soğukluk **süreç ve yol
soğukluğudur**. Gerçek disk soğuğu için makinenin yeniden başlatılması gerekirdi;
yapılmadı.

**1080p seti kullanılmadı.** 31,6 MB'lik klip için alınan sıcak taban gürültüye
boğuldu (ortanca 5237, p95 9404, en az 3941; değişiklikle ilgisiz `cerceve` adımı
bile 747 → 1456'ya çıktı). O setten hüküm çıkarılmadı; rapordaki bütün sayılar
2,6 MB'lik `kucuk.mp4` ile alındı.

## Kanıt

| Ne | Yol |
| --- | --- |
| Eşleşik sıcak, ham | `.calisma/dalga7c/eslesik/ham-taban-vs-iyilestirme-sicak.csv` |
| Eşleşik sıcak, özet | `.calisma/dalga7c/eslesik/ozet-taban-vs-iyilestirme-sicak.txt` |
| Eşleşik soğuk, ham | `.calisma/dalga7c/eslesik/ham-taban-vs-iyilestirme-soguk.csv` |
| Eşleşik soğuk, özet | `.calisma/dalga7c/eslesik/ozet-taban-vs-iyilestirme-soguk.txt` |
| Eşleşmemiş ilk ölçümler (kayma kanıtı) | `.calisma/dalga7c/once/`, `.calisma/dalga7c/adimA/` |
| Klipler | `.calisma/dalga7c/klip/kucuk.mp4`, `buyuk-1080p.mp4` |

Düzeneğin kendisi ve kullanımı: `tools/acilis-hizi/`.


# Hipersürüş A dalgası — 16 Eylül 2026

Düzenek aynı, iki şey değişti. Birincisi **ölçülen süreç**: artık `VidShrink.exe`
(başlatıcı) koşuyor, uygulama değil — kullanıcının çift tıkladığı şey bu. İkincisi
**dış saat**: `kabuk-ilk-kare` sütunu ölçerin kendi kronometresinden geliyor,
`Start-Process` çağrısından ilk kare satırı görülene kadar, 20 ms yoklamayla.

Bu sütun zorunluydu. Tabanın izi sıfır noktası olarak uygulamanın doğumunu alıyor,
yeni yapınınki başlatıcının doğumunu; `ilk-kare` sütunları bu yüzden aynı şeyi
ölçmüyor ve yeni yapı haksız yere ~70 ms geride görünüyor. Sıfır noktası yapıdan
bağımsız olan tek sütun `kabuk-ilk-kare`'dir, hüküm oradan verilir.

Eşleşik sıcak, 14 tekrar, 6,2 MB 720p klip, taban `1a1385c1`, yeni `d2ea8bd5`:

| Sütun | Taban ortanca | Hipersürüş ortanca | Eşleşik fark |
| --- | --- | --- | --- |
| `kabuk-ilk-kare` | 1796,5 ms | 1747,7 ms | **−71,0 ms**, 14 çiftin 9'u yeni yapı lehine |
| `ilk-kare` (izden) | 1692,4 ms | 1734,4 ms | +12,2 ms — sıfır noktası farklı, **hüküm vermez** |
| `app-dogdu` | yok | 67,4 ms | başlatıcının uygulamayı doğurmaya kadar harcadığı süre |

**Hüküm: A dalgası ~71 ms kazandırdı, hedef 100 ms'e 1,7 saniye var.** Fark
gürültülü (en az −267,5, en çok +101,5); ortancası tutarlı ama kazanç, açılışın
%4'ü. Bu şaşırtıcı değil: A dalgasının dokunduğu yer başlatıcının bakım işleriydi
ve o işler zaten ~70 ms'ti.

Kalan 1,7 saniyenin dağılımı yeni yapının kendi tablosundan okunur ve hepsi
uygulamanın içinde:

| Aralık | Süre | Ne yapılıyor |
| --- | --- | --- |
| `app-dogdu` → `pencere-yapici` | ~660 ms | .NET başlangıcı, palet, geçici temizlik |
| `pencere-yapici` → `pencere-yuklendi` | ~700 ms | XAML açılımı, yedi sekmenin kurulması |
| `sekme` → `ilk-kare` | ~275 ms | `mpv_create` ve ilk çözme |

Ortadaki 700 ms **A2**'nin (oynatıcıyı `MainWindow`'dan önce açmak), sondaki 275 ms
**B4**'ün konusu. 100 ms eşiği bu ikisi yapılmadan görünmüyor.

## Ölçülürken düzeltilen iki şey

**Betik başlatıcının ölümünü koşumun sonu sanıyordu.** Başlatıcı uygulamayı doğurup
hemen çıkıyor; ilk koşum 24 tekrarın hepsinde boş döndü. Döngü artık izi bekliyor
([olcum.ps1:57](../../tools/acilis-hizi/olcum.ps1:57)).

**Öldürme adla değil yolla yapılıyor.** Ölçüm uygulamayı kapatırken `VidShrink.App`
adını arıyordu; kullanıcının masaüstündeki kurulumu da aynı adı taşıyor. Artık
yalnız ölçüm klasörünün altından koşan süreç öldürülüyor.

## Kanıt

| Ne | Yol |
| --- | --- |
| Eşleşik sıcak, özet (depoya alındı) | `docs/olcumler/T-hipersurus-A-ozet.txt` |
| Eşleşik sıcak, ham | `.calisma/hiper/eslesik/ham-taban-vs-hipersurus-sicak.csv` |
| İki yapı | `.calisma/hiper/taban/`, `.calisma/hiper/yeni/` |
| Klip | `.calisma/hiper/klip/kucuk.mp4` (720p30, 20 sn, 6,2 MB) |

## Hipersürüş C dalgası — 16 Eylül 2026

Eşleşik sıcak, 14 tekrar, 720p30 20 sn 6,2 MB klip. Taban `20208611` (sürüm 0.5.5), yeni
yapı C dalgası. Ham özet: [T-hipersurus-C-ozet.txt](T-hipersurus-C-ozet.txt).

**Bu oturum yüklü bir makinede koştu.** Taban aynı yapı olduğu halde A dalgası ölçümünde
1796,5 ms veren sütun burada 6506,3 ms veriyor — makine ~3,6 kat kaymış. Bu yüzden mutlak
sayılar iki oturum arasında karşılaştırılmaz; hüküm yalnız `eşleşik fark` tablosundan
okunur, orada iki yapı aynı tekrarda sırayla koşuyor.

### Hüküm

| Sütun | Taban ortanca | C ortanca | Eşleşik fark ortancası | C lehine çift |
| --- | --- | --- | --- | --- |
| `kabuk-ilk-kare` (dış saat) | 6506,3 ms | 4064,0 ms | **−2603,3 ms** | **14/14** |

En az −4422,7, en çok −1402,8. On dört çiftin **hepsi** aynı yöne bakıyor; A dalgasında
bu sayı 9/14 ve fark −71,0 ms'ti.

### Perde

Yeni `perde` adımı: başlatıcının doğumundan ekranda ilk görüntü olana kadar geçen süre.

| | Ortanca | En az | p95 |
| --- | --- | --- | --- |
| `perde` | 211,4 ms | 149,9 ms | 669,1 ms |

Aynı koşumda tabanın ilk karesi 6506,3 ms'te geliyor, yani kullanıcı **6,5 saniye boş
ekrana** bakıyordu; perdeyle ekran 211 ms'te doluyor. Makinenin kayması bu sütunu da
büyütüyor: aynı kaymayla A dalgası oturumuna indirgenirse karşılığı ~58 ms. **100 ms
hedefi bu saatte tutuluyor**, ama sayının kendisi yüklü makinede 211 ms.

Perde bir örtü değil: uygulamanın "ilk karem ekranda" işareti gelene kadar duruyor,
gelince kalkıyor. İşaret hiç gelmezse 8 saniyelik tavanla kalkıyor.

### Nereden geldi

Ortanca sütunları kaymayı taşıyor; aşağıdaki paylar iki yapının **kendi içinde**
ardışık iki işaretin ortancası çıkarılarak okundu, yani yaklaşık.

| Adım | Taban payı | C payı | Not |
| --- | --- | --- | --- |
| `ayar-okundu` → `palet` | 1183,6 ms | **1,2 ms** | C3: yürürlükteki palet yeniden uygulanmıyor |
| `pencere-yapici` → `xaml` | 1535,0 ms | 1225,2 ms | C1: XAML açılımının JIT payı düştü |
| `app-dogdu` → `cerceve` | 1011,3 ms | 723,6 ms | C1: .NET ve Avalonia başlatması |
| `sekme` → `motor-acildi` | 504,5 ms | 380,4 ms | dokunulmadı; kayma |

Paletin payı **1183,6 ms'ten 1,2 ms'e** indi. A dalgası ölçümünde bu adım 194,1 ms'ti;
aradaki fark makinenin kayması. Tek satırlık bir kapı: istenen palet zaten yürürlükteyse
iki palet dosyası ayrıştırılmıyor, sözlüğün ilk sırası yenilenmiyor, kurulmuş fırçalar
gezilmiyor.

### Bedeli

| | Taban | C |
| --- | --- | --- |
| `app/` klasörü | 207 MB | 224 MB |
| `VidShrink.exe` | 64,9 MB | 65,6 MB |

ReadyToRun önceden derlenmiş kodu pakete koyuyor; güncelleme indirmesi ~%8 büyüyor.

### Kanıt

| Ne | Nerede |
| --- | --- |
| Ham özet | [T-hipersurus-C-ozet.txt](T-hipersurus-C-ozet.txt) |
| Ölçüm düzeneği | [tools/acilis-hizi/olcum.ps1](../../tools/acilis-hizi/olcum.ps1) |
| Plan ve kararlar | [docs/plan.md](../plan.md) C dalgası |
| Fable'ın netleştirmesi | [016](../netlestirme/016-a-dalgasi-olculdu-cift-tik-ilk-kare-1796.md) |
| Pimler | [HipersurusTests.cs](../../tests/VidShrink.Tests/HipersurusTests.cs) |

## D dalgası — Tembel kaydedici ve önden ısıtılan motor (16 Eylül 2026)

Eşleşik sıcak ölçüm, 14 tekrar, aynı klip, aynı makine. Taban **0.6.0** (C dalgası),
karşı taraf **D**. Makine bu oturumda boştaydı; C dalgası ölçümündeki mutlak sayılarla
karşılaştırılamaz, çift içindeki fark karşılaştırılır.

| Sütun | 0.6.0 | D | Eşleşik fark ortancası | D lehine çift |
| --- | --- | --- | --- | --- |
| kabuk-ilk-kare | 1239,8 ms | 1019,3 ms | **−213,7 ms** | 13/14 |
| motor-acildi | 1212,6 ms | 888,1 ms | −338,0 ms | 14/14 |
| kare-kaynagi | 1217,6 ms | 920,1 ms | −311,1 ms | 13/14 |
| xaml | 587,8 ms | 506,4 ms | −89,6 ms | 12/14 |

İki kalem:

**Kaydedici sekmesi tembel.** `RecorderView` XAML'den çıktı, sekme ilk seçildiğinde
kuruluyor. Sonda ölçümü tek başına bu görünümü 89,4 ms göstermişti; `xaml` adımındaki
−89,6 ms bire bir o.

**Motor pencere kurulurken açılıyor.** Kabuktan dosya geldiğinde `AcilisMotoru`
libmpv hazır olur olmaz `mpv_create` + `loadfile`'ı arka planda koşturuyor; oynatıcı
sekmesi hazır motoru devralıyor. `sekme → kare-kaynagi` payı 268,8 ms'den 52,7 ms'ye
indi.

### Ölçülüp geri alınan

`hwdec=auto-copy` motor adımını 272,8 ms'den 297,0 ms'ye çıkardı, yaklaşık **+24 ms**.
Geri alındı; pim `OynaticiYazilimsalCozuyor_DonanimOlculdu_GeriAlindi`.

### Sonda

`AcilisIsareti.Ad` iliştirilmiş özelliği XAML öğelerine konunca öğe kurulurken ize
satır yazıyor. `InitializeComponent`'in 215 ms'i yedi sekmeye eşit dağılmıyordu: altı
sekme toplam ~50 ms, tek başına kaydedici 89,4 ms. Bu ölçü, planlanan "her sekmeyi
`UserControl`'e böl" ameliyatını gereksiz kıldı.

### Algı saati

`perde` 79,3 ms. 100 ms hedefi bu saatte tutuyor; çift tık → ilk kare saatinde
hedef 1 sn'nin biraz altında.

### Kanıt

| Ne | Nerede |
| --- | --- |
| Ham özet | [T-hipersurus-D-ozet.txt](T-hipersurus-D-ozet.txt) |
| Ölçüm düzeneği | [tools/acilis-hizi/olcum.ps1](../../tools/acilis-hizi/olcum.ps1) |
| Plan ve kararlar | [docs/plan.md](../plan.md) D dalgası |
| Fable'ın netleştirmesi | [017](../netlestirme/017-c-dalgasindan-sonra-cift-tik-ile-ilk-kar.md) |
| Pimler | [HipersurusTests.cs](../../tests/VidShrink.Tests/HipersurusTests.cs) |

## E dalgası — Pencere kurulumunda kalan kendi kalemlerimiz (16 Eylül 2026)

D dalgası "kendi kodumuzda büyük kalem kalmadı" demişti. Bu iddia sondayla ölçüldü ve
tutmadı: yapıcı ile ilk kare arasında dört kendi kalemimiz vardı, en büyüğü 186 ms.

Eşleşik sıcak ölçüm, 14 tekrar, aynı 6,2 MB klip. Taban **c87c2afb** (0.8.0), karşı taraf
**E**. İki yapı da iz noktasız; aynı başlatıcı ve `tools\libmpv` ile.

| Sütun | c87c2afb | E | Eşleşik fark ortancası | E lehine çift |
| --- | --- | --- | --- | --- |
| kabuk-ilk-kare | 1230,1 ms | 803,5 ms | **−455,2 ms** | 14/14 |
| ilk-kare | 1215,4 ms | 788,7 ms | −449,2 ms | 14/14 |
| pencere-kuruldu | 931,7 ms | 642,1 ms | −276,2 ms | 14/14 |
| perde | 104,8 ms | 102,4 ms | −1,0 ms | 8/14 |

Aralık bazında (iz dosyalarından, çift içi fark):

| Aralık | c87c2afb | E | Fark ortancası | E lehine |
| --- | --- | --- | --- | --- |
| pencere-yapici → xaml | 119,8 | 121,4 | +2,5 | 6/14 |
| xaml → yapici-bitti | 83,4 | 20,7 | −62,9 | 14/14 |
| yapici-bitti → pencere-kuruldu | 182,8 | 5,8 | −176,6 | 14/14 |
| pencere-kuruldu → pencere-yuklendi | 116,8 | 78,8 | −34,7 | 14/14 |
| pencere-yuklendi → sekme | 18,8 | 18,8 | −0,4 | 8/14 |
| sekme → kare-kaynagi | 59,7 | 43,1 | −16,8 | 14/14 |
| kare-kaynagi → ilk-kare | 114,8 | 0,3 | −114,4 | 12/14 |

### Sonda: kalemler nerede

Geçici iz noktaları yapıcıya, `OnWindowLoaded`'a, `App.OnFrameworkInitializationCompleted`'a,
`PlayerView.OpenAsync`'e ve pencerenin ilk ölçü/yerleşim/`OnOpened` geçişlerine kondu. İki
sondalı yapı eşleşik koştu (14 tekrar); ortanca pay, ms:

| Kalem | Taban | E | Değişiklik |
| --- | --- | --- | --- |
| `window.Icon` için PNG okuma | 47,9 | 2,2 | E1 |
| `window.Icon =` ataması (1254 px PNG → HICON) | 138,2 | 3,4 | E1 |
| Dil düğmeleri + dil listesi (42 kataloğun tamamı yükleniyordu) | 64,0 | 13,7 | E3 |
| Başlık logosu çözümü (1254 px PNG) | 20,8 | 5,0 | E2 |
| İlk ölçü (Measure) | 56,6 | 33,2 | E4 |
| Yerleşimden `OnOpened`'a | 52,4 | 35,8 | E1 + E4, ayrılmadı |
| Sekme değişiminden sonra oynatıcı yerleşimi | 25,2 | 14,4 | E4 |
| `kare-kaynagi → ilk-kare` | 79,8 | 0,3 | ayrılmadı |

Kalemin küçüklüğü yüzünden kesilmeyenler: güncelleme paneli (`InitializeUpdateUi`) ~0,2 ms,
`UseLanguage(tr)` ~17 ms, `ApplyAdvanced` ~9 ms, `TogglePlay` ~8 ms. `pencere-yapici → xaml`
Avalonia'nın ve XAML ağacının kendisi; tek bir kod kalemi yok.

`kare-kaynagi → ilk-kare` 80–115 ms'den 0,3 ms'ye indi ama hangi değişikliğin getirdiği
ayrı ölçülmedi; ilk karenin çizimi arayüz iş parçacığının boşalmasını bekliyordu.

### Değişiklikler

- **E1** Windows'ta pencere simgesi `Assets/VidShrink.ico`'dan (16–256 px hazır boylar);
  PNG yalnız diğer platformlarda.
- **E2** Başlık logosu arka planda çözülüyor, arayüz iş parçacığına hazır bitmap gelir.
- **E3** `Strings.PeekIn`: dil adını kataloğu yüklemeden, JSON'u baştan tarayarak okur;
  diskteki ve gömülü dosyalarda sonra gelen kazanır, `Build` ile aynı sıra.
- **E4** Kabuktan dosya geldiyse yapıcı oynatıcı sekmesini seçiyor; ilk yerleşim küçültme
  sekmesini kurmuyor.

Görünür davranış: pencere doğrudan oynatıcı sekmesinde açılır; başlık logosu pencereden
birkaç ms sonra belirir; Windows görev çubuğu simgesi ölçeklenmiş PNG yerine ICO'nun kendi
boyudur. Geri alınan değişiklik yok; dört kalemin hepsi kendi aralığında 14/14 lehine.

### Kanıt

| Ne | Nerede |
| --- | --- |
| Ham özet ve aralık farkları | [T-hipersurus-E-ozet.txt](T-hipersurus-E-ozet.txt) |
| Sondalı eşleşik ölçüm | [T-hipersurus-E-sonda.txt](T-hipersurus-E-sonda.txt) |
| Ölçüm düzeneği | [tools/acilis-hizi/olcum.ps1](../../tools/acilis-hizi/olcum.ps1) |
| Plan | [docs/plan.md](../plan.md) "Hipersürüş E dalgası" |
| Pimler | `LocalizationTests.DilAdiniKatalogYuklemedenOkumakTamYuklemeyleAyni`, `OynaticiGirdiTests.KabukYolununActigiSekmeOynaticidir` |

## F dalgası — Olağan açılışta perde yok, etiket yenilemesi menüyü yeniden kurmuyor (16 Eylül 2026)

Eşleşik sıcak ölçüm, 14 tekrar, aynı 6,2 MB klip, sıra her tekrarda döner. Taban **3ba42150**
(origin/main, 0.8.2), karşı taraf **6619209f** (`t0/acilis-anlik`). Saat uygulamanın kendi
izi; sıfır başlatıcının `Process.StartTime`'ı.

| Sütun | 3ba42150 | F | Eşleşik fark ortancası | F lehine çift |
| --- | --- | --- | --- | --- |
| ilk-kare | 1623,0 ms | 611,9 ms | **−992,4 ms** | 14/14 |
| yapici-bitti | 1517,8 ms | 497,1 ms | −999,9 ms | 14/14 |
| xaml | 502,6 ms | 473,3 ms | −14,1 ms | 8/14 |
| app-dogdu | 74,9 ms | 69,6 ms | +1,4 ms | 7/14 |

Farkın tamamına yakını `xaml → yapici-bitti` aralığında: taban 1015 ms, F 24 ms. Bu, etiket
yenilemesinin her açılışta sağ tık menüsünü (648 değer) baştan yazmasıydı; F yalnız farklı
etiketi yazar. Perdenin payı (`app-dogdu → perde`, tabanda ~17 ms) aynı farkın içinde küçük.

### Düzenek değişti

Ölçüm bu dalgadan itibaren kullanıcının ekranına dokunmuyor: `EkranSaati` uygulamayı ayrı
bir Win32 masaüstünde (`WinSta0\vidshrink-olcum`) doğurur, ekran okunmaz. Her sürece
`KayitKalkani` başlangıç kancası yüklenir ve HKCU özel bir kovana yönlenir. Bu yüzden tabanın
menü yazımı gerçek kayıt defteri yerine uygulama kovanına gider; ölçülen ~1 s o kovana
yazımın bedelidir, gerçek HKCU'da önceki sondada ~867 ms görülmüştü. Sayılar önceki
dalgaların ekran saatiyle doğrudan kıyaslanmaz.

Her koşumdan önce ve sonra sağ tık menüsü, etiketler ve ilişkilendirme (654 değer)
karşılaştırıldı: 28 koşumun hiçbirinde fark yok.

### Makine notu

Sistem yükü günlüğü koşum boyunca (17:07–17:19) cpu 15–80 arası; tepe 17:14:19'da 80,
eşiği (>80) aşmadı, atılan tekrar yok. Quasimorph oyunu (pid 15216) açık ama boştaydı;
kapatılmadı. İlk deneme 15:58'de cpu=93 örneğinde 6. tekrarda durdurulmuştu, o sayılar
kullanılmadı.

### Kanıt

| Ne | Nerede |
| --- | --- |
| Özet ve eşleşik fark | [T-hipersurus-F-ozet.txt](T-hipersurus-F-ozet.txt) |
| Her koşumun bütün adımları | [T-hipersurus-F-ham.jsonl](T-hipersurus-F-ham.jsonl) |
| Ölçüm düzeneği | [tools/acilis-hizi/EkranSaati](../../tools/acilis-hizi/EkranSaati), [tools/acilis-hizi/KayitKalkani](../../tools/acilis-hizi/KayitKalkani) |
| Plan | [docs/plan.md](../plan.md) "Hipersürüş F dalgası" |