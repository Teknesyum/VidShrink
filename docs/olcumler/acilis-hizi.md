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
