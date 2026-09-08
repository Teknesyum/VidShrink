# Ekran görüntüsü düzeneği ve çekimler

T189. Tur 1-2 ölçüm makinesi: Windows 11 Pro 10.0.22631. Tur 3'ün bütün ölçümleri
(beş koşumluk sha256 tablosu dahil) Windows 11 Pro 10.0.26100 üzerinde alındı.
.NET 8, Avalonia 11.3.20.
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
- Tekrarlanabilir. **Beş** ardışık tam koşumun on dört karesinin de sha256'sı aynı çıktı
  (aşağıda ham çıktı; `.calisma/T189/k9-bes-kosum-tur3.txt`). Üç koşum bunu ölçmeye
  yetmiyordu: tur 2'nin üç koşumu farkı kaçırdı, denetçinin beş koşumu yakaladı. Tur 1 ve
  tur 2'de önizleme karesi koşumdan koşuma değişiyordu; dört ayrı kaynağı ve kapatılması
  “Tur 3: önizleme karesi belirlenimli değildi (K9)” başlığında.

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
    klip	C:\Users\Teknesyum\Desktop\Projeler\VidShrink-T189\.calisma\T189\klip.mp4
    T189-kucult-en.png	369312
    T189-donustur-en.png	361631
    T189-ayarlar-en.png	233923
    T189-gelismis-en.png	432494
    T189-hakkinda-en.png	357185
    T189-onizleme-en.png	67454
    T189-oynatici-en.png	223370
    T189-kucult-tr.png	364578
    T189-donustur-tr.png	361678
    T189-ayarlar-tr.png	232601
    T189-gelismis-tr.png	433466
    T189-hakkinda-tr.png	366178
    T189-onizleme-tr.png	67430
    T189-oynatici-tr.png	225114
    toplam	14

    real	0m23.195s

Tur 1'de aynı koşum 2 dakika 5 saniye sürüyordu: önizleme beklemesi hiçbir zaman
gerçekleşmeyen bir koşula bakıyor ve iki dil için de 60 saniyelik üst sınırı doldurup
sessizce geçiyordu. Tur 2'de 39 saniyeye indi; tur 3'te pencereler yürüyerek beklenmeyip
`LoadClipAsync` ile doğrudan terminal pencereye geçildiği için 23 saniyeye indi. Yukarıdaki
çıktı `.calisma/T189/r6.stdout.txt` ve `.calisma/T189/r6.time.txt` dosyalarından; o koşum
beşinci koşumla bayt bayt aynı çıktı.

### Süit ve CI

Aşağıdaki iki çıktı **tur 1'e ait**; tur 2 süiti yeniden koşturmadı.
Tur 2 beş dosyaya dokundu (`6b23c47`): iki PNG, bu belge, `tools/VidShrink.Shot/AGENTS.md`
ve `tools/VidShrink.Shot/Program.cs`. Tur 3 altı dosyaya dokunuyor: dört PNG, bu belge ve
yine `Program.cs`. İki turda da kod tarafı yalnız `tools/VidShrink.Shot/Program.cs` —
düzenek `.sln`de değil, `dotnet test` onu ne derliyor ne koşuyor.

    $ dotnet test
    Basarili!  - Basarisiz:     0, Basarili:  1898, Atlanan:    23, Toplam:  1921, Sure: 15 m 56 s - VidShrink.Tests.dll (net8.0)

    $ gh run list --branch T189-ekran-goruntusu -L 3
    completed	success	T189: on dort kare cekildi, duzenek belgelendi, olcum belgesi yazildi	ci	T189-ekran-goruntusu	push	34162652621	19m6s	2026-09-07T21:18:22Z

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

### Tekrarlanabilirlik (K9 CHECK)

**Beş** ardışık tam koşum, aynı ikili, farklı çıkış klasörleri. Ham çıktı:
`.calisma/T189/k9-bes-kosum-tur3.txt`, iz satırları `.calisma/T189/izler/r1..r5.stderr.txt`.
sha256'nın ilk 12 hanesi:

    DOSYA                     KOSUM1         KOSUM2         KOSUM3         KOSUM4         KOSUM5         SONUC
    T189-ayarlar-en.png       7ccf5b1207fa   7ccf5b1207fa   7ccf5b1207fa   7ccf5b1207fa   7ccf5b1207fa   ayni
    T189-ayarlar-tr.png       01a1cd8ffacb   01a1cd8ffacb   01a1cd8ffacb   01a1cd8ffacb   01a1cd8ffacb   ayni
    T189-donustur-en.png      d278e611294e   d278e611294e   d278e611294e   d278e611294e   d278e611294e   ayni
    T189-donustur-tr.png      fe10bac06c48   fe10bac06c48   fe10bac06c48   fe10bac06c48   fe10bac06c48   ayni
    T189-gelismis-en.png      9a939670f88c   9a939670f88c   9a939670f88c   9a939670f88c   9a939670f88c   ayni
    T189-gelismis-tr.png      af80c77a92ed   af80c77a92ed   af80c77a92ed   af80c77a92ed   af80c77a92ed   ayni
    T189-hakkinda-en.png      f2c3b1246fec   f2c3b1246fec   f2c3b1246fec   f2c3b1246fec   f2c3b1246fec   ayni
    T189-hakkinda-tr.png      fb2f244270ed   fb2f244270ed   fb2f244270ed   fb2f244270ed   fb2f244270ed   ayni
    T189-kucult-en.png        81925dbd2865   81925dbd2865   81925dbd2865   81925dbd2865   81925dbd2865   ayni
    T189-kucult-tr.png        844be8bf8d76   844be8bf8d76   844be8bf8d76   844be8bf8d76   844be8bf8d76   ayni
    T189-onizleme-en.png      046f005c202a   046f005c202a   046f005c202a   046f005c202a   046f005c202a   ayni
    T189-onizleme-tr.png      67e8c4c77cb6   67e8c4c77cb6   67e8c4c77cb6   67e8c4c77cb6   67e8c4c77cb6   ayni
    T189-oynatici-en.png      03b62757a8b1   03b62757a8b1   03b62757a8b1   03b62757a8b1   03b62757a8b1   ayni
    T189-oynatici-tr.png      a867cf3cfb67   a867cf3cfb67   a867cf3cfb67   a867cf3cfb67   a867cf3cfb67   ayni

On dört kareden on dördü beş koşumun beşinde de aynı, farklı çıkan kare yok. Altıncı bir
koşum daha alındı (`.calisma/T189/r6`) ve beşinci koşumla bayt bayt aynı çıktı.

## Tur 2'de düzeltilen iki kusur

### 1. Ayırıcı panonun soluna düşüyordu (K8)

`ClearEntrance` ağacın **bütün** `RenderTransform`'larını `null` yapıyordu. Giriş
canlandırması için doğru, ama karşılaştırma panelinin ayırıcısı canlandırma değil
**kalıcı konum taşıyıcısı**: `ComparisonPanel` konumu kod arkasında tuttuğu bir
`TranslateTransform` örneğinden veriyor (`ComparisonPanel.axaml.cs:43,82,282`) ve çizim
sırası `Settle → Relayout → ClearEntrance` olduğu için temizlik en son konumu siliyordu.
Teslim edilen kare kullanıcının hiçbir zaman görmediği bir durumu gösteriyordu.

Aynı tuzak `ControlStrip.Thumb` ve `ControlStrip.EncodeCursor` için de geçerli
(`ControlStrip.axaml.cs:57-58`); bugün görünür etkisi yok çünkü ikisi de başlangıçta
sıfırda duruyor. Temizlik artık **seçici**: `RenderTransform` değeri
`TranslateTransform` olan düğüm atlanıyor. Giriş biçemi değeri `TransformOperations`
olarak kuruyor (`Themes/Controls.axaml`, `^.enter`), dolayısıyla giriş temizliği
eskisi gibi çalışıyor — kanıtı aşağıdaki iki ölçüm.

**Ölçü: ayırıcının sütunu.** Kare 506x512, ortası 253. Panonun dikey yönde bir renkte
kalan parlak sütunları (üst %5 – alt %85 aralığının %90'ından fazlası):

Ölçen betik (`.calisma/` altında koşuldu, iş bitince silindi; burada tam metniyle
duruyor ki sayı yeniden üretilebilsin):

    from PIL import Image
    im = Image.open(path).convert('RGB'); w, h = im.size; px = im.load()
    top, bottom = int(h * 0.05), int(h * 0.85)
    for x in range(w):
        first = px[x, top]
        same = sum(1 for y in range(top, bottom)
                   if sum(abs(a - b) for a, b in zip(px[x, y], first)) < 40
                   and sum(px[x, y]) > 240)
        if same > (bottom - top) * 0.9: print(x)

    $ python ayirici.py .calisma/T189/base/T189-onizleme-*.png .calisma/T189/r3/T189-onizleme-*.png
    .calisma/T189/base/T189-onizleme-en.png	olcu=506x512	orta=253	dikey-cizgi-sutunlari=[0, 12, 13, 505]
    .calisma/T189/base/T189-onizleme-tr.png	olcu=506x512	orta=253	dikey-cizgi-sutunlari=[0, 12, 13, 505]
    .calisma/T189/r3/T189-onizleme-en.png	olcu=506x512	orta=253	dikey-cizgi-sutunlari=[0, 252, 253, 505]
    .calisma/T189/r3/T189-onizleme-tr.png	olcu=506x512	orta=253	dikey-cizgi-sutunlari=[0, 252, 253, 505]

`base` değişiklikten önceki koşum, `r3` sonraki. 0 ve 505 panonun kendi kenarları. Ayırıcı x=12'den x=252'ye taşındı; `_split = 0.5`'in
karşılığı tam olarak burada.

**Ölçü: giriş temizliği bozulmadı.** Değişiklikten önceki ve sonraki koşumun on iki
tam pencere karesi bayt bayt aynı — seçici temizlik hiçbir giriş dönüşümünü ayakta
bırakmamış. Yalnız iki önizleme karesi değişti:

    T189-kucult-en.png       eski=e198b42b0693 yeni=e198b42b0693 ayni
    T189-kucult-tr.png       eski=5047aaa648f2 yeni=5047aaa648f2 ayni
    T189-donustur-en.png     eski=d278e611294e yeni=d278e611294e ayni
    T189-donustur-tr.png     eski=fe10bac06c48 yeni=fe10bac06c48 ayni
    T189-ayarlar-en.png      eski=7ccf5b1207fa yeni=7ccf5b1207fa ayni
    T189-ayarlar-tr.png      eski=01a1cd8ffacb yeni=01a1cd8ffacb ayni
    T189-gelismis-en.png     eski=9a939670f88c yeni=9a939670f88c ayni
    T189-gelismis-tr.png     eski=af80c77a92ed yeni=af80c77a92ed ayni
    T189-hakkinda-en.png     eski=f2c3b1246fec yeni=f2c3b1246fec ayni
    T189-hakkinda-tr.png     eski=fb2f244270ed yeni=fb2f244270ed ayni
    T189-oynatici-en.png     eski=03b62757a8b1 yeni=03b62757a8b1 ayni
    T189-oynatici-tr.png     eski=a867cf3cfb67 yeni=a867cf3cfb67 ayni

Bu tablo **tur 2'ye ait**. Tur 3 dört kareyi yeniden çekti; `T189-kucult-<dil>` ve
`T189-onizleme-<dil>` bugün başka sha256 taşıyor, güncel değerler yukarıdaki beş koşumluk
tabloda. Kalan on kare tur 2'den beri değişmedi.

### 2. Önizleme karesi koşumdan koşuma değişiyordu (K9) — tur 2'nin eksik düzeltmesi

Tur 2 eski beklemeyi kaldırdı ve doğru bir şey yaptı: `Preview` ağacında kaynağı dolu bir
`Image` aranıyordu, **böyle bir `Image` hiç yok** — karşılaştırma paneli kareyi
`ComparisonSurface.Render` içinde tek bir `WriteableBitmap`'ten çiziyor. Koşul hiç doğru
olmuyor, bekleme 60 saniyelik üst sınırı dolduruyor ve o an panoda hangi kare varsa o
çiziliyordu. Koşum süresi 2 dk 5 sn'den 39 sn'ye bunun için indi.

Ama kare **belirlenimli olmadı**; tur 2'nin bunu anlatan cümleleri yanlıştı. Doğrusu
aşağıdaki başlıkta.

## Tur 3: önizleme karesi belirlenimli değildi (K9)

Seçilen yol **(a)**: çekim gerçekten belirlenimli hale getirildi, iddia geri alınmadı.
Gerekçe: (b) yalnız belgeyi düzeltirdi, kare koşumdan koşuma değişmeye devam ederdi ve
`docs/gorseller/`teki dosya her yeniden çekimde gereksiz yere değişirdi; kaynakların dördü
de ölçülebildi ve `tools/VidShrink.Shot` içinde kapatılabildi, uygulama koduna
dokunulmadı.

### Tur 2'nin yanlış cümlesi

Tur 2 şunu yazıyordu: “`Controls.IsPlaying` doğrudan `false` yapılır, böylece pencere
`[0,5)` sabit kalır… yakalanan kare her koşumda aynı: pencerenin 150. karesi.” İkisi de
doğru değil. `FreezePreview`'a bir iz satırı konup ölçüldü
(`.calisma/T189/k9-oncesi-iz.txt`):

    kosum 1  pozisyon=00:00:09.9666666  pencere StartSeconds=5  EndSeconds=10   serit opaklik=0
    kosum 2  pozisyon=00:00:11.9666666  pencere StartSeconds=7  EndSeconds=12   serit opaklik=0

İlk kare gelene kadar geçen sürede boru bir ya da iki pencere devrediyor; `IsPlaying`'i
sonradan `false` yapmak yalnız devri o noktada kesiyor. Kaç pencere geçtiği makinenin o
anki yüküne bağlı.

### Dört bağımsız kaynak

Denetçi üç kaynak ayırdı (`.calisma/T189-denetim/k9-fark-ayrimi.txt`); ölçerken dördüncüsü
çıktı. Sırayla:

1. **Yakalanan pencere.** Yukarıdaki iz. Kapatması: pencereleri yürüyerek beklemek yerine
   `PanelHost.LoadClipAsync` ile doğrudan **terminal pencereye** geçiliyor —
   `FreezesAtWindowEnd` doğruyken ilerlenecek pencere yok, ne devir ne ön hazırlık. Yürümeyi
   beklemek hem yavaş hem kırılgan: bir koşum 60 saniyede terminal pencereye varamadı
   (`.calisma/T189/k9-yuruyerek-bekleme-zaman-asimi.txt`).

2. **Denetim şeridinin görünürlüğü.** Şerit `!IsPlaying` iken açılıyor
   (`ControlStrip.UpdateHold` → `HoverZone.Hold`), ama açılması bir zamanlayıcı gecikmesi ve
   360 ms'lik opaklık geçişi üzerinden yürüyor. Kare geçişin ortasına düştüğü için şerit bir
   koşumda kareye giriyor, diğerinde girmiyordu. Kapatması: geçiş siliniyor, opaklık
   doğrudan yazılıyor. Şerit artık her karede açık — konumu da (`00:11 / 00:12`) okunuyor.

3. **Plan kartı (`T189-kucult-en.png`, %0,19).** Hedef kutusuna yazmak planı hemen değil,
   `MainWindow.ScheduleRecalculate`'in 160 ms'lik `DispatcherTimer`'ı üzerinden yeniliyor.
   Başsız koşumda o zamanlayıcı ancak kuyruk sürüldüğünde ilerliyor; çizimden önce tıklarsa
   kart yeni hedefin planını, tıklamazsa bir öncekini gösteriyordu. Kapatması: hesap
   doğrudan koşturuluyor ve zamanlayıcı durduruluyor. Ayrıca ilk kareden önce yetenek
   önbelleği ısıtılıyor: `EncoderCapabilities` yoklamayı `ProbeKillMs` ile kesiyor ve
   soğukta yavaş dönen ilk ffmpeg çağrısı öldürülüyordu.

4. **Yüzeyin halkası — raporda hiç yoktu.** İlk üç kaynak kapatıldıktan sonra alınan beş
   koşumda `T189-onizleme-en.png` hâlâ üç ayrı sha256 verdi, üstelik beş koşumun iz satırı
   birbirinin aynıydı (`pozisyon=00:00:11.9666666`). Karenin sol üst köşesindeki kaynak
   damgası farkı gösterdi: koşum 1'de `00:00:11.167 / 335`, koşum 3'te `00:00:11.967 / 359`
   (`.calisma/T189/k9-fark-olcum.txt`). İki ayrı sunum turu var — `PanelHost.Drain` kareyi
   yüzeyin halkasına **bırakıyor**, `ComparisonSurface.Round` onu bitmap'e **çiziyor** —
   ve barındırıcıyı durdurmak yalnız bırakmayı durduruyor. Halkada çizilmemiş kare kalırsa
   kareye giren görüntü son bırakılan değil son çizilen oluyor. Beklemek işe yaramadı:
   yüzeyin turunu `RequestAnimationFrame` sürüyor ve başsız koşumda o döngü kendiliğinden
   dönmüyor, sayaçlar hiç artmadı (`.calisma/T189/k9-yuzey-bekleme-zaman-asimi.txt`).
   Kapatması: tur bir kez elle çevriliyor — `Round` halkadaki bütün kareleri bir seferde
   tüketip en yenisini çiziyor — ve halkanın boşaldığı doğrulanıyor.

### `FreezePreview` şimdi ne yapıyor

1. Boru `Oynuyor` durumuna gelene kadar bekler.
2. `LoadClipAsync` ile terminal pencereyi kodlatır ve `FreezesAtWindowEnd`'i doğrular.
3. Terminal pencerenin borusu `Oynuyor` olana kadar bekler.
4. Sunum sayacı bir saniye boyunca değişmeyene kadar bekler — pencere bitmiştir.
5. `Controls.IsPlaying`'i doğrudan `false` yapar (düğmeye basılmış gibi değil;
   `ApplyPlayState` boruyu da duraklatırdı).
6. `_generation`'ı artırıp barındırıcının turunu kapatır.
7. Yüzeyin turunu bir kez elle çevirip halkayı boşaltır.
8. Şeridin opaklık geçişini silip şeridi açar.

Yakalanan kare her koşumda aynı: terminal pencerenin (`[7, 12)`) son karesi, kaynak
ekseninde 11,967 sn. Beş koşumluk tablo yukarıda; kareyi çizen sayılar
`.calisma/T189/izler/` altındaki iz satırlarında.

### Ölçülmedi

- Kaynakların dördü de **bu makinede** kapatıldı. Başka bir makinede aynı on dört karenin
  çıkacağı ölçülmedi; ölçülen şey aynı makinede koşumdan koşuma değişmediği.
- Denetçinin ilk koşumundaki `T189-kucult-en.png` farkı (%0,19) doğrudan tekrar
  üretilemedi; yalnız o farkı üretebilen yarış (160 ms'lik yeniden hesap zamanlayıcısı)
  bulunup kapatıldı. “Kapandı” değil, “yarış artık yok” denebilir.

### Borçlar (K10)

1. **Sessiz zaman aşımı kapatıldı.** `Pump`'ın `bool` dönüşü yutuluyordu: 60 saniyede
   kare gelmezse boş panel çizilip çıkış kodu 0 kalıyordu. Artık bütün beklemeler
   `Await` üzerinden geçiyor ve süre dolarsa `TimeoutException` atıyor — `OpenInPlayer`
   zaten öyle yapıyordu, iki yol artık tutarlı.
2. **Düzenek `.sln`e eklenmedi.** Gerekçesi `tools/VidShrink.Shot/AGENTS.md:50`'de yazılı: CI'a
   `Avalonia.Headless` taşınmıyor. **Sonucu:** K1'in CHECK'i (`dotnet build
   tools/VidShrink.Shot`) hiçbir otomatik koşumda çalışmıyor; düzenek yalnız elle
   derleniyor ve kırıldığında CI sessiz kalır. Bu tur çözülmedi, borç olarak duruyor.
3. **Karelerdeki arayüz kusurları T192'nin işi.** `T189-kucult-tr.png`'de “Video
   Kodegi” ile “Ses” sütun başlıkları üst üste biniyor ve “Kare Hızı D...” kırpılıyor.
   Kare kusuru gizlemiyor; düzeltme arayüzde yapılacak.

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
