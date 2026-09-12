# Açılış Hızı: Çift Tıktan İlk Kareye

Ölçülen şey: kabuktan bir `.mp4`'e çift tıklandığında süreç yaratılmasından
(`Process.StartTime`) oynatıcının ilk karesinin ekrana geldiği ana kadar geçen süre.
Birim milisaniye. Tüm adımlar `AcilisIzi` kancasıyla aynı sürecin içinden damgalanır;
ölçüm dışarıdan bir kronometreyle değil, uygulamanın kendi zaman çizgisinden gelir.

Düzenek `tools/acilis-hizi/olcum.ps1`. Eşleşik tasarım: her tekrarda taban ve iyileştirme
yapıları arka arkaya koşturulur, sıra tekrardan tekrara döner. Böylece makinenin o
dakikadaki durumu iki tarafa da aynı biçimde yansır; karşılaştırma ortancaların farkı
değil, **çift başına farkların ortancası** olur.

Makine: DESKTOP-0J80KVV / Windows NT 10.0.22631.0. Klip: 2,6 MB `kucuk.mp4`.
Taban yapısı `worktree-agent-dalga7c`'nin ayrıldığı commit, iyileştirme aynı dalın ucu.

## Üç iyileştirme

**A — geçici temizlik arka plana alındı** (`App.axaml.cs`). `TempCleanup` açılışta
`%TEMP%`'i tarıyordu ve bu tarama arayüz iş parçacığında duruyordu. Sildiği şey ölü
koşumların artığı; yeni koşum kendi kiralamasını zaten yazıyor, yani ilk kareyi bekleten
bir bağı yok.

**B — libmpv pencereyle birlikte ısıtılıyor** (`Program.cs`). 115 MB'lik yerel kitaplık
bugüne kadar pencere kurulduktan **sonra**, ilk karenin hemen önünde yükleniyordu;
bekleme tam da açılışın sonuna düşüyordu. Artık kabuktan bir dosya geldiğinde pencere
kurulurken arka planda başlatılıyor. Yalnız dosyayla açılışta çağrılır: boş açılan
pencerede oynatıcı hiç kullanılmayabilir.

**C — varsayılan uygulama önerisi ile sürüm sorusu ilk karenin arkasına alındı**
(`MainWindow.axaml.cs`). İkisi de kullanıcının açmak istediği dosyayla ilgisiz, ikisi de
arayüz iş parçacığında duruyor (öneri uzantı başına bir `AssocQueryString`, sürüm sorusu
bir `HttpClient` kurulumu). Sıra değişikliği görünürlüklerini değiştirmez.

## Sıcak koşum (12 tekrar)

Kaynak: `.calisma/dalga7c/eslesik/ozet-taban-vs-iyilestirme-sicak.txt`

| adım | taban ortanca | iyileştirme ortanca | çift farkı ortancası | iyileştirme lehine çift |
| --- | --- | --- | --- | --- |
| ayar-okundu | 448,0 | 373,1 | **−95,7** | 12/12 |
| palet | 671,2 | 617,3 | **−112,6** | 11/12 |
| pencere-kuruldu | 1261,9 | 1187,7 | **−123,0** | 9/12 |
| pencere-yuklendi | 1497,4 | 1475,4 | **−147,5** | 10/12 |
| sekme | 1566,4 | 1498,9 | **−196,3** | 11/12 |
| kare-kaynagi | 1946,2 | 1856,6 | **−243,2** | 12/12 |
| **ilk-kare** | **1971,2** | **1857,8** | **−240,7** | **12/12** |

`ilk-kare` farkının aralığı −429,2 … −21,9 ms. On iki çiftin on ikisi iyileştirme lehine;
tek bir tekrar bile ters yönde değil.

`libmpv-hazir` yalnız iyileştirme tarafında var, ortancası 74,0 ms — kitaplık, pencere
daha yapıcısındayken hazır oluyor.

## Soğuk koşum (8 tekrar)

Kaynak: `.calisma/dalga7c/eslesik/ozet-taban-vs-iyilestirme-soguk.txt`

| adım | taban ortanca | iyileştirme ortanca | çift farkı ortancası | iyileştirme lehine çift |
| --- | --- | --- | --- | --- |
| ayar-okundu | 411,2 | 319,7 | **−84,0** | 8/8 |
| palet | 634,4 | 518,3 | **−107,8** | 8/8 |
| pencere-kuruldu | 1150,0 | 1038,5 | **−120,7** | 8/8 |
| sekme | 1404,2 | 1256,1 | **−170,2** | 8/8 |
| kare-kaynagi | 1763,8 | 1585,0 | **−176,9** | 8/8 |
| **ilk-kare** | **1765,0** | **1586,0** | **−177,0** | **8/8** |

Aralık −266,2 … −22,3 ms, sekiz çiftin sekizi iyileştirme lehine.

## Okuma tuzağı: `varsayilan-oneri`

Her iki tabloda da `varsayilan-oneri` satırı **+1597,2** (sıcak) ve **+1474,6** (soğuk)
ms gösterir ve hiçbir çift iyileştirme lehine değildir. Bu bir gerileme değil.

C iyileştirmesi bu damgayı `LoadStartupFileAsync`'in **arkasına** taşıdı. Yani taban
tarafında damga dosya açılmadan önce, iyileştirme tarafında dosya açıldıktan sonra
vuruluyor — iki tarafta aynı olayı ölçmüyorlar. Önerinin kullanıcıya görünme anı
değişmedi; değişen, izin hangi noktada yazıldığı. Ölçülmek istenen şey `ilk-kare`
ve o satır 20/20 çiftte iyileştirme lehine.

`n` değerinin bazı satırlarda 12 yerine 7 olması da aynı yerden gelir: damga ilk kareden
sonraya düştüğü için birkaç tekrarda pencere kapanmadan önce yazılmadı.

## Sonuç

Çift tıktan ilk kareye kadar geçen süre sıcak koşumda **−241 ms**, soğuk koşumda
**−177 ms**. Üç değişikliğin hiçbiri bir işi atmıyor; üçü de aynı işi ilk karenin
önünden arkasına ya da arka plana alıyor.
