# Ekran görüntüsü düzeneği ve çekimler

T189. Ölçüm makinesi: Windows 11 Pro 10.0.22631, .NET 8, Avalonia 11.3.20.
Düzenek: `tools/VidShrink.Shot`. Çıktı: `docs/gorseller/T189-<konu>-<dil>.png`.

## Seçilen yöntem: ekran dışı (offscreen) render

Sözleşmenin tercih sırasında (a) olan yol tutuyor, gerçek pencereye gerek kalmadı.

Uygulama Avalonia'nın başsız platformunda `UseHeadlessDrawing = false` ile kuruluyor —
çizimi yine Skia yapıyor, yalnız pencere platformu yok. `MainWindow` 1600x1000 görüş
alanında ölçülüp yerleştiriliyor ve kök görsel `RenderTargetBitmap` üzerine çiziliyor.
Bu yolu depo zaten kullanıyordu: `tests/VidShrink.Tests/WindowOpacityTests.cs` aynı
şekilde piksel okuyor, `WindowLayoutTests` aynı yerleşim geçişini kuruyor.

Gerekçe — (b) gerçek pencere yerine (a):

- Ekran kapısı gerekmiyor. Düzenek kapı kapalıyken de koşar.
- Sonuç makineden bağımsız: ekran çözünürlüğü, masaüstü ölçeklemesi ve pencere
  yöneticisi kareye karışmıyor. Ölçüsü sabit 1600x1000.
- Tekrarlanabilir. İki ardışık koşumun on dört karesinin de sha256'sı aynı çıktı
  (aşağıda ham çıktı).

Bedeli: kare pencerenin **istemci alanı**; işletim sisteminin pencere gölgesi ve köşe
yuvarlaması karede yok. Uygulama kendi başlık çubuğunu çizdiği için
(`ExtendClientAreaToDecorationsHint`) başlık, düğmeler ve kenarlık yine görünüyor.

## Koşulan komutlar ve ham çıktı

### Derleme (K1 CHECK)

    $ dotnet build tools/VidShrink.Shot
        0 Uyarı
        0 Hata

    Geçen Süre 00:00:02.29

### Çekim

    $ dotnet run --project tools/VidShrink.Shot
    klip	C:\Users\Administrator\Desktop\Projeler\Vidshrink\.claude\worktrees\T189\.calisma\T189\klip.mp4
    T189-kucult-en.png	368669
    T189-donustur-en.png	361631
    T189-ayarlar-en.png	233923
    T189-gelismis-en.png	432494
    T189-hakkinda-en.png	357185
    T189-onizleme-en.png	63179
    T189-oynatici-en.png	223370
    T189-kucult-tr.png	363894
    T189-donustur-tr.png	361678
    T189-ayarlar-tr.png	232601
    T189-gelismis-tr.png	433466
    T189-hakkinda-tr.png	366178
    T189-onizleme-tr.png	63165
    T189-oynatici-tr.png	225114
    toplam	14

### Dosya sayısı (K2 CHECK)

    $ ls docs/gorseller/T189-*.png | wc -l
    14

### Ölçüler

Tam pencere kareleri aynı ölçüde. `onizleme` pencerenin tamamı değil, karşılaştırma
panelinin kendi ölçüsünde kesilmiş bir parça — README'de yan yana duracak olanlar
1600x1000 olanlar.

    T189-ayarlar-en.png 1600 1000
    T189-ayarlar-tr.png 1600 1000
    T189-donustur-en.png 1600 1000
    T189-donustur-tr.png 1600 1000
    T189-gelismis-en.png 1600 1000
    T189-gelismis-tr.png 1600 1000
    T189-hakkinda-en.png 1600 1000
    T189-hakkinda-tr.png 1600 1000
    T189-kucult-en.png 1600 1000
    T189-kucult-tr.png 1600 1000
    T189-onizleme-en.png 506 512
    T189-onizleme-tr.png 506 512
    T189-oynatici-en.png 1600 1000
    T189-oynatici-tr.png 1600 1000

### Tekrarlanabilirlik

İki ardışık tam koşum, aynı ikili, farklı çıkış klasörleri. sha256'nın ilk 12 hanesi:

    T189-ayarlar-en.png 7ccf5b1207fa 7ccf5b1207fa ayni
    T189-ayarlar-tr.png 01a1cd8ffacb 01a1cd8ffacb ayni
    T189-donustur-en.png d278e611294e d278e611294e ayni
    T189-donustur-tr.png fe10bac06c48 fe10bac06c48 ayni
    T189-gelismis-en.png 9a939670f88c 9a939670f88c ayni
    T189-gelismis-tr.png af80c77a92ed af80c77a92ed ayni
    T189-hakkinda-en.png f2c3b1246fec f2c3b1246fec ayni
    T189-hakkinda-tr.png fb2f244270ed fb2f244270ed ayni
    T189-kucult-en.png e198b42b0693 e198b42b0693 ayni
    T189-kucult-tr.png 5047aaa648f2 5047aaa648f2 ayni
    T189-onizleme-en.png 5f51b70f5cf6 5f51b70f5cf6 ayni
    T189-onizleme-tr.png 7adbb318834f 7adbb318834f ayni
    T189-oynatici-en.png 03b62757a8b1 03b62757a8b1 ayni
    T189-oynatici-tr.png a867cf3cfb67 a867cf3cfb67 ayni

On dörtte on dört aynı.

## Karelerde ne var

| Dosya | Ne gösteriyor | Kaynağı |
| --- | --- | --- |
| `T189-kucult-<dil>` | Küçült sekmesi, dosya yüklü, hedef 24 MB girilmiş | yoklamasız `MediaInfo` (4K/60, 420 MB, 3:07) |
| `T189-donustur-<dil>` | Dönüştür sekmesi, seçenekler ve üretilen ffmpeg komutu | aynı `MediaInfo` |
| `T189-ayarlar-<dil>` | Ayarlar sekmesi | yüksüz |
| `T189-gelismis-<dil>` | Gelişmiş sekmesi | yüksüz |
| `T189-hakkinda-<dil>` | Hakkında sekmesi | yüksüz |
| `T189-onizleme-<dil>` | Karşılaştırma paneli, orijinal / işlenmiş yan yana | üretilen klip |
| `T189-oynatici-<dil>` | Oynatıcı sekmesi, video açık, ilk kare çözülmüş | üretilen klip |

Küçültme kareleri diske ve ffmpeg'e bağlı değil: `LoadWithoutProbing` yoklamayı atlıyor,
`MediaInfo` düzeneğin içinde sabit. Görseldeki sayılar her makinede aynı.

Oynatıcı ve önizleme kareleri gerçek dosya istiyor — `PanelHost.SetFiles` kaynağı diskte
bulamazsa paneli kapatıyor, `PlayerView.OpenAsync` gerçek bir ffmpeg borusu açıyor.
Düzenek klibi kendisi üretiyor: `testsrc2` 1280x720@30, 12 sn, libx264 + aac.
İkinci argümanla gerçek bir video verilirse kare ondan gelir.

## Elle kalan adım

Yok. `dotnet run --project tools/VidShrink.Shot` on dört kareyi baştan sona kendi üretir;
tıklanacak bir şey kalmıyor.

## Başsız koşumun elle oturtulan üç geçişi

Bunlar düzeneğin bulduğu ve `Settle`/`SelectTab` içinde kapattığı tuzaklar; yazılmazsa
kare sessizce yanlış çıkar:

1. **Giriş sınıfı panelleri saydam bırakıyor.** `PreparePanelEntrance` beş panele `enter`
   sınıfı ekliyor, `PlayPanelEntrance` onu `DispatcherTimer` ile geri alıyor. Başsız
   koşumda o zamanlayıcı hiç ateşlenmiyor: ilk denemede Kaynak, Hedef, Yapılacak İşlem ve
   Çıktı panelleri yarı saydam çıktı, arkadaki grafik metnin içinden geçti. Çözüm:
   `Transitions` boşaltılıyor, sınıf siliniyor, `Opacity` 1'e çekiliyor.
2. **`Fade` yavaşlaması posta kuyruğunda bekliyor.** `Fade(control, true)`
   `Dispatcher.UIThread.Post` ile opaklığı 1 yapıyor; kuyruğu kimse boşaltmıyor.
   `Dispatcher.UIThread.RunJobs()` ile sürülüyor.
3. **Sekme geçişi eskiyi yeninin altında bırakıyor.** `NeonTabControl` içeriği bir
   `TransitioningContentControl` içinde değiştiriyor; geçiş bitmediği için Gelişmiş
   sekmesinin karesinde Küçült sekmesi hâlâ görünüyordu. `PageTransition` `null`lanıyor.

Ayrıca dil: karşılaştırma paneli metnini yalnız `Strings.Changed` olayında tazeliyor ve o
olay değer değişince ateşleniyor. Pencere doğrudan hedef dilde kurulursa panel İngilizce
kalıyor. Düzenek pencereyi karşı dilde kurup sonra hedef dile geçiriyor.

## Eski görselden yenisine

README'nin (her iki dilde) kullandığı dokuz görsel ve karşılıkları. **Hiçbiri silinmedi;
README'yi değiştirmek T190'ın işi.**

| Eski | Yeni | Not |
| --- | --- | --- |
| `T25-ana-en.png` | `T189-kucult-en.png` | Eski kare boş pencereydi; yenisi dosya yüklü. Oynatıcı sekmesi ve öneri şeridi eskisinde yok. |
| `T25-ana-tr.png` | `T189-kucult-tr.png` | Aynı. |
| `T8-hizli-en.png` | `T189-kucult-en.png` | Eskisi "dosya yüklü Küçült sekmesi"; yeni kare ikisinin de yerini tutuyor. |
| `T8-hizli-tr.png` | `T189-kucult-tr.png` | Aynı. |
| `T25-gelismis-sekmesi.png` | `T189-gelismis-tr.png` | Eskisi yalnız Türkçeydi; artık İngilizce eşi de var (`T189-gelismis-en.png`). |
| `t26-pencere-tr.png` | `T189-donustur-tr.png` | Eskisi yalnız Türkçeydi; İngilizce eşi `T189-donustur-en.png`. |
| `t27-kodek-en.png` | **karşılığı yok** | İpucu balonu. Düzenek balon açmıyor; aşağıya bakın. |
| `t27-kodek-tr.png` | **karşılığı yok** | Aynı. |
| `macos-paket-uygulama.png` | **karşılığı yok** | macOS karesi; bu makine Windows. |

README'de karşılığı olmayan, T189'un yeni getirdiği kareler: `T189-ayarlar-<dil>`,
`T189-hakkinda-<dil>`, `T189-onizleme-<dil>`, `T189-oynatici-<dil>`.

## Ölçemediklerim

- **İpucu balonu kareleri (`t27-kodek-*`).** Çekemedim. Balon `ToolTip` ile açılıyor ve
  açılması işaretçi olayı istiyor; başsız koşumda işaretçi yok, balon ayrı bir açılır
  pencerede yaşıyor ve pencerenin kök görselinin altında değil, yani aynı
  `RenderTargetBitmap`'e düşmüyor. Gerçek pencereyle ya da balonu doğrudan kurup ayrı
  çizerek alınabilir; ikisi de bu sözleşmenin dışında.
- **macOS karesi.** Çekemedim, makine Windows.
- **Karenin gerçek pencereye ne kadar benzediği.** Ölçemedim. İki yolu yan yana koyup
  piksel farkı almadım; gerçek pencere açmadım.
- **Başka çözünürlükler.** Ölçemedim, yalnız 1600x1000 çekildi.
- **Karanlık/aydınlık tema ayrımı.** Ölçemedim; uygulamanın tek teması var.
