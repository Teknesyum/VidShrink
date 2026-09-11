[[netlestirme:009]]

# Netleştirme: Karar ver (ikinci tur, sorular cevaplandi): Avalonia 12 gecisi, gelecek major yu

İşe başlamadan önce soruyu keskinleştir. Görüş verme, plan yazma, kod yazma.
Yalnız şunu döndür: soruda belirsiz kalan yerler, her biri için tek satırlık bir netleştirme sorusu, en fazla beş. Belirsizlik yoksa "net" yaz.

## Soru

Karar ver (ikinci tur, sorular cevaplandi): Avalonia 12 gecisi, gelecek major yuku, acgozlu hedefe gore v1 kapsami, tahmin yontemi ve kendi kutuphane tetigi

## Elde olan olgular

Netleştirme 008'in beş sorusuna T0'ın cevapları (kullanıcının cümlesine dayanıyor; bu ikinci tur, yeni netleştirme sorusu sorma, kalan belirsizlikte varsayımını tek satırla yazıp kararın tamamını döndür, Türkçe):
1. Hız ölçütü: birincil olarak ilk yayımlanabilir v1'e ulaşma süresi (kullanıcı: "özellikle ilk versiyonu yayımlamak için hazır lib ile bu işi çözmek istiyorum"). Uzun vadeli major yükü ikincil ama ayrıca soruldu ("gelecekte 13 çıksa ... sürekli bizim için yük mü"); ikisi zıt çekerse ikisini ayrı satırda göster, kararı v1 süresine göre ver.
2. Platform kümesi: mevcut 4 masaüstü RID (win-x64, osx-arm64, osx-x64, linux-x64). Mobil ve WASM kapsam dışı (masaüstü video sıkıştırma aracı, ffmpeg ayrı süreç).
3. Kullanıcı açgözlülüğünün farkında ve senden gerçekçi kıyas istiyor ("aşırı greddy olduğumuzu bilsin fable ve ona göre kıyas yapsın"). v1 için erteleme önermek serbest; ama iki senaryoyu ayrı satır ver: (a) Standart 30 + Gelişmiş 13 hepsi 4 RID'de v1'de, (b) senin önerdiğin v1 kesiti; her ertelemenin gerekçesi ve bedeli.
4. Karar sonda sonucu olmadan verilsin; sondanın kabul ölçütü ve tutmazsa dönüş dalı kararın parçası olsun. Sonda karardan hemen sonra koşulacak.
5. Her iki veri de: genel iş gerçekleşenleri (T193, Avalonia taşıması T11-T14, boru T167/T175/T182) ve sınanmamış motor tahminleri (007). Tek örnekten kalibrasyonun yeterli olup olmadığına ve pilotun zorunlu olup olmadığına sen karar ver; pilotsa hangi iş, hangi ölçüler, kalan nasıl hesaplanır.

Olgular (netleştirme 008 ile aynı, kısaltılmadan):
Kullanıcının cümlesi (aynen):
"öncelikle avolonia yı güncellemek ve programı 12 ye taşımak ne kadar zahmetli ve gelecekte 13 çıksa vb çıksa sürekli bizim için yük mü oluşturacak program baştan mı inşa edilecek yoksa basit bir güncelleme ile çözülebilecek bir durum mu
şöyle söyleyeyim en hızlı yöntemi istiyorum ancak tüm özellikleri tüm platformlarda istiyorum burda aşırı greddy olduğumuzu bilsin fable ve ona göre kıyas yapsın
kendi lib imizi yazma konusunda da bizim geçmişteki incelemelerimiz ne kadar tuttu bunun hesabı kitabı ile veya pilot bir iş yapıp kalanı tahmin etme sistemi üzerinden gidebiliriz şeklinde bir düşünelim evet çok zordur ancak sadece gerçekten gerekliyse kendi lib imizi yazalım şeklinde düşünüyorum onun haricinde özellikle ilk versiyonu yayımlamak için hazır lib ile bu işi çözmek istiyorum önce şu temelimizdeki avolonia sorununu konuşalım sonra oynatıcı kısmına dokunuş yaparız"

T0'ın yorumu: Kullanıcı açıkça "aşırı açgözlüyüz" diyor: en hızlı yol + bütün özellikler (Standart 30 + Gelişmiş 13) + bütün platformlar. Kıyas bu açgözlülüğü hesaba katsın: hangisi gerçekçi, hangisi ertelenmeli, ilk sürüm (v1) için neyin şart olduğu. Sıra: önce Avalonia temeli, sonra oynatıcı. Kendi kütüphanemiz yalnız gerçekten gerekirse; v1 hazır kütüphaneyle.

## Proje durumu
- .NET 8 (net8.0), Avalonia 11.3.20, Avalonia.Themes.Fluent, AGPL-3.0-or-later. 274 .cs/.axaml dosyası, ~66.800 satır (src+tests).
- Avalonia paketleri 4 projede: src/VidShrink.App, tests/VidShrink.Tests (düz Avalonia.Headless, kendi AppHost'u; Avalonia.Headless.XUnit KULLANILMIYOR; xunit 2.9.2), tools/VidShrink.PresentBench, tools/VidShrink.Shot. src/VidShrink.Launcher net8.0-windows (Avalonia değil, yalnız Windows başlatıcısı).
- Arayüz kodu tamamen code-behind: XAML'de {Binding} 0, x:DataType 0.
- Platform: Windows birincil, macOS ikincil. Sürüm iş akışı (release.yml) ubuntu üzerinde çapraz derlemeyle 4 RID yayımlıyor: win-x64, osx-arm64, osx-x64, linux-x64 (self-contained). CI testleri yalnız windows-latest'te koşuyor; macOS/Linux'ta hiç test koşmuyor.
- Kodda platforma özgü çağrı (DllImport/OperatingSystem.Is/Registry vb.) 65 yerde, 17 dosyada (Launcher Splash 28, UpdateCheck 7, FileAssociation 4, MainWindow 4 ...).
- Proje 22 Ağustos 2026'da (774b1871) Avalonia'ya 11.3.x ile taşındı; 11 seçimi için yazılı gerekçe yok. Avalonia 12.0.0 o tarihte 4,5 aydır çıkmıştı.

## Avalonia 12 kırılma analizi (sonnet ajanı, web + depoda grep, 11 Eylül 2026)
- 12.0.0: 7 Nisan 2026; en son 12.1.2: 2 Eylül 2026. 11.0 → 12.0 arası 1007 gün (~2,75 yıl), 11 serisinde 54 güncelleme. Önceki major: 0.10 → 11. 11.x için resmi destek bitiş (EOL) tarihi bulunamadı.
- 12 hedefleri: masaüstü/WASM için net8.0 minimum kabul, .NET 10 önerilen; mobil .NET 10 zorunlu. Platformlar: Windows, macOS, Linux (12.1.0'dan deneysel Wayland), iOS, Android, WASM.
- Resmi kırılma listesinin depodaki karşılığı:
  - IBinding kaldırıldı → BindingBase: 1 dosya, 1 satır (DefaultAppSuggestionBar.cs:90), mekanik.
  - Clipboard/IDataObject değişikliği: 1 dosya, 3 satır SetTextAsync (imzanın aynı kaldığı bildiriliyor, doğrulanamadı).
  - Screen abstract oldu: 1 satır, zaten uyumlu desen. TopLevel.GetTopLevel: 5 dosya, zaten hedef desen.
  - SystemDecorations → WindowDecorations: doğrudan kullanım bulunamadı (doğrulanamadı).
  - AvaloniaHeadlessPlatformOptions: 2 dosya, değişip değişmediği doğrulanamadı.
  - Dispatcher.UIThread: ~35 satır, 12 dosya; kaldırılmıyor, yalnız kütüphanelere öneri.
  - Compiled binding varsayılanı, Diagnostics, Gestures öneki, focus olay tipleri, Direct2D1, mobil/Blazor: depoda 0 eşleşme.
  - Ajanın özeti: "somut kırılma riski 1 dosya / 1 satır + xUnit geçişi"; xUnit maddesi Avalonia.Headless.XUnit içindir, biz kullanmıyoruz.
- Topluluk: bir kullanıcı geçişin "saatler içinde, günler değil" sürdüğünü yazmış (Avalonia 12 blogu). Başka ölçülü rapor doğrulanamadı.
- Ölçülmedi: gerçek derleme hata sayısı ve tam test süiti sonucu (NuGet indirme izni bekliyor; sonda planlı: dalda 12.1.2'ye çek, derle, hata say, tam süit).
- Avalonia 12 ile ilgili oynatıcı bağlantısı: HanumanInstitute.LibMpv.Avalonia 0.10.1 "Avalonia 12 implementation"; Avalonia Accelerate MediaPlayerControl 12 hedefli (ticari, elendi). OpenGlControlBase'in 12'deki durumu doğrulanmadı.

## Geçmiş tahminlerin isabeti (Explore ajanı, depo taraması)
- Depoda 2019 commit (16 Ağustos → 11 Eylül 2026, 26 gün), 182 mühürlü sözleşme.
- Sonradan gerçekleşeniyle eşleşen tek iş tahmini var: T193 (max modu açılış ölçümü). Tahmin 1 sözleşme + 1 denetim, 4-8 saat, 60-90k token. Gerçek: 1 sözleşme, 3 tur (2 yeniden açılış), ~5,3 saat duvar saati; token kayıtlı değil. Oranlar gerçek/tahmin: sözleşme 1,0, tur 3,0, süre 0,88.
- Avalonia taşıması (T11-T14): önceden tahmin yok. Gerçek: 4 sözleşme, ~7 tur, 22 → 26 Ağustos (4-5 gün); ana commit 27 dosya, +3883/−1675.
- Oynatıcı borusu (T167, T175, T182): önceden tahmin yok. Gerçek: 3 sözleşme, 7-9 tur, 5 → 7 Eylül (~2,2 gün); T175'in 4 turunun 3'ü "KALDI".
- Netleştirme 007'nin "1 tur ≈ 3 insan iş günü" varsayımı gerçekle kıyaslanınca: Avalonia 7 tur × 3 = 21 gün tahmin vs ~4-5 gün gerçek (≈0,2); boru 21-27 gün vs ~2,2 gün (≈0,1). Yani takvim birimi 5-10 kat şişkin; tur sayısı ise ilk tahminin ~3 katına çıkabiliyor (tek örnek).
- 007'nin tur tahminleri (libmpv ~14 tur, boru genişletme ~22, AutoGen ~36) henüz hiçbir gerçekle sınanmadı.

## Önceki kararlar (bağlam)
- Netleştirme 007: libmpv + kendi P/Invoke (115/140), LibVLCSharp ikinci; T0 düzeltmesi: T167 ölçümü (LibVLC bellek geri çağrısı yolu bu depoda ölçüldü: arama medyanı 38,9-57,2 ms, senkron 2,1 ms, kurulum +106-293 MB) eklenince LibVLC ~109. 0. dalga adım 1: libmpv SW render ölçümü (1080p ≥55, 4K ≥24 kare/sn) ve LibVLC ölçülmüş değerleriyle kıyas; tutmazsa LibVLC.
- docs/plan.md sırası: 0. dalgadan önce Avalonia 11.3.20 → 12.1.2 geçiş sondası.
- Platform notları: libmpv macOS'ta Homebrew ile, app bundle paketleme resmi değil; Linux'ta sistem paketi. LibVLC: Windows NuGet 128 MB (VideoLAN.LibVLC.Windows), macOS için ayrı NuGet, Linux'ta sistem libvlc (bu üçü bu turda ayrıca doğrulanmadı).

## İstenen karar (bu turda netleştirme sorusu sorma; kalan belirsizlikte varsayımı tek satırla yaz ve kararın tamamını döndür, Türkçe)
1. Avalonia 12'ye geçiş: zahmeti (tur/saat), "baştan inşa" mı "paket güncellemesi" mi, şimdi mi sonra mı; sondanın kabul ölçütü ve başarısız olursa ne yapılacağı.
2. Gelecekteki major'lar (13, 14 ...): sürekli yük mü; yükü sınırlayan kural (ör. her major'a ne zaman geçilir, hangi API'lerden kaçınılır).
3. Kullanıcının "aşırı açgözlü" hedefiyle (en hızlı + tüm özellikler + tüm platformlar) gerçekçi kıyas: v1 için şart olan platform × özellik kümesi, ertelenecekler; bu hedefin motor seçimine (libmpv / LibVLC) etkisi, özellikle macOS/Linux paketleme.
4. Tahmin yöntemi: geçmiş isabet verisine göre tahmin birimi düzeltilsin mi (tur → gerçek gün), yoksa pilot iş + kalanını tahmin sistemi mi; pilot seçilirse hangi iş pilot olur, hangi ölçüler alınır, kalan nasıl hesaplanır.
5. Kendi kütüphanemizi yazma: hangi ölçülebilir koşulda "gerçekten gerekli" sayılır (tetik ölçütü).
