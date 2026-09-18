# Kaydedici: Çoklu Ekran ve Ölçek ≠ 1 Piksel Ölçümü

18 Eylül 2026. Makine: tek ekran DISPLAY1 1024x768, ölçek 1,0 (`GetSystemMetrics` DPI-farkındasız
ve farkındalı okumada aynı: 1024x768). İkinci fiziksel ekran ve ölçek ≠ 1 bu makinede **yok**;
ekran yerleşimi ve ölçek çarpanı enjekte edilerek ölçüldü.

Düzenek `tools/kaydedici-yerlesim`. Ham çıktı bu belgenin sonunda, kelimesi kelimesine.

## 1. gdigrab Hangi Koordinat Uzayında Konuşuyor

Kaydedicinin bütün piksel hesabı buna dayanıyor: Avalonia'nın `Screen.Bounds`'u Windows'ta
fiziksel piksel veriyor, ama `gdigrab`'ın `-offset_x/-offset_y/-video_size` sayılarının fiziksel
mi yoksa DPI ile sanallaştırılmış mantıksal piksel mi olduğu ffmpeg sürecinin DPI farkındalığına
bağlı. DPI-farkındasız bir süreç masaüstü DC'sini ölçeğe bölünmüş görür ve aynı sayılar başka
yeri yakalar.

Ölçü: `ffmpeg -f gdigrab -framerate 5 -i desktop -t 4 -f null -` koşarken sürecin
`GetProcessDpiAwareness` değeri okundu (`shcore.dll`), iki bağımsız koşumda.

| Süreç | hr | awareness | Ad |
|---|---|---|---|
| ffmpeg, koşum 1 (pid 25172) | 0 | 2 | `PROCESS_PER_MONITOR_DPI_AWARE` |
| ffmpeg, koşum 2 (pid 29252) | 0 | 2 | `PROCESS_PER_MONITOR_DPI_AWARE` |
| Negatif kontrol: ölçen PowerShell, `SetProcessDPIAware` çağrılmadan | 0 | 0 | `PROCESS_DPI_UNAWARE` |

ffmpeg sürümü: winget `yt-dlp.FFmpeg` altındaki `ffmpeg-N-125875-g5d4d3bdc61-win64-gpl`.
Aynı koşumun bildirdiği girdi satırı: `Video: bmp, bgra, 1024x768` — makinenin fiziksel çözünürlüğü.

**Sonuç:** `gdigrab` argümanları fiziksel piksel. Avalonia'nın verdiği sınırlar da fiziksel
piksel olduğu için Windows kolunda ölçekten kaynaklanan bir çevirim **gerekmiyor**; yakalama
dikdörtgeni ölçülen **beş** yerleşimin — ölçek ≠ 1 olan **üçü** dahil: (b1), (b2), (d) — hiçbirinde
kaymıyor.
Ölçek, kaydedicinin **pencere** tarafını — bölge çizim örtüsünü — ilgilendiriyor.

Bu satırın sınırı: ölçü bu makinedeki bu ffmpeg ikilisine ait. Başka bir yapının manifesti
farkındalığı değiştirebilir; kaydedici ffmpeg'i `tools\ffmpeg`'ten getirdiği için sürüm
değişiminde bu ölçü tekrarlanmalı.

## 2. Bulunan Dört Kusur

### K1 — İki monitörlü masaüstünde "Ekran 1" iki monitörü birden kaydediyor

`RecorderArguments.WindowsInput` ekran hedefini yalnız `ScreenIndex != 0` iken ofsetli bölgeye
çeviriyordu; indeks 0'da `-i desktop` ofsetsiz kalıyor ve `gdigrab` **bütün sanal masaüstünü**
veriyor.

| Yerleşim | indeks | ESKİ yakalanan (fiziksel px) | YENİ yakalanan | Monitörün kendisi |
|---|---|---|---|---|
| (c) 1920x1080 + soldaki 1280x1024 | 0 | 3200x1080+-1280,0 | 1920x1080+0,0 | 1920x1080+0,0 |
| (c) | 1 | 1280x1024+-1280,0 | 1280x1024+-1280,0 | 1280x1024+-1280,0 |
| (d) 1920x1080 + soldaki 2880x1620 @1,5 | 0 | 4800x1620+-2880,0 | 1920x1080+0,0 | 1920x1080+0,0 |
| (d) | 1 | 2880x1620+-2880,0 | 2880x1620+-2880,0 | 2880x1620+-2880,0 |
| (a) tek ekran 1920x1080 @1,0 | 0 | bütün masaüstü, ofsetsiz | bütün masaüstü, ofsetsiz | 1920x1080+0,0 |
| (b1) tek ekran 2400x1350 @1,25 | 0 | bütün masaüstü, ofsetsiz | bütün masaüstü, ofsetsiz | 2400x1350+0,0 |
| (b2) tek ekran 2880x1620 @1,5 | 0 | bütün masaüstü, ofsetsiz | bütün masaüstü, ofsetsiz | 2880x1620+0,0 |

Düzeltme `RecorderArguments.ScreenCapture`: seçilen monitör masaüstü birleşiminin tamamı
değilse indeks 0 olsa da ofsete çevriliyor. Tek ekranlı makinede monitör = birleşim olduğu için
argüman değişmiyor — tablodaki (a)/(b1)/(b2) satırları bunu gösteriyor.

### K2 — Negatif X'teki monitörde bölge kaydı tümden reddediliyordu

`Validate` içinde koşulsuz bir "Region offsets cannot be negative." kuralı vardı. Birincil
monitörün soluna yerleştirilen ikinci monitör negatif masaüstü koordinatlarında oturuyor ve
`gdigrab` oraya bakabiliyor (K1'in ürettiği `-offset_x -1280` argümanı da negatif). Kural o
monitördeki her bölge seçimini kapatıyordu.

| Yerleşim | Bölge | ESKİ Validate (çıkarsandı, ölçülmedi) | YENİ Validate (ölçüldü) |
|---|---|---|---|
| (c) | 640x480+-1200,100 | `Region offsets cannot be negative.` | (hatasız) |
| (d) | 640x480+-2800,100 | `Region offsets cannot be negative.` | (hatasız) |
| (d) | 640x400+-640,1100 | `Region offsets cannot be negative.` | (hatasız) |

Düzeltme: kural sayının işaretine değil **kapsamaya** bakıyor. Monitör listesi dikdörtgeni
kapsıyorsa negatif ofset geçerli; liste boşsa masaüstünün sola uzandığına kanıt yok ve eski
kural duruyor (test `NegatifXtekiMonitordeCizilenBolgeKabulEdilir`, boş listeli negatif kontrol).

"ESKİ Validate" sütunu ölçülmedi: düzenek yalnız geçerli kaynağı çağırıyor, sütun `e962538e`'deki
koşulsuz "Region offsets cannot be negative." kuralından çıkarsandı. Çıkarsama üç satır için de
tek kuralı okur; buna karşılık "YENİ Validate" sütunu ham çıktıdan geliyor.
macOS'ta bölge girdi değil kırpma filtresi, kare içinde negatif koordinat yok: orada kural
olduğu gibi kalıyor (test `MacOstaNegatifBolgeOfsetiHalaReddedilir`).

### K3 — Monitör boşluğuna düşen bölge sessizce siyah kaydediliyordu

İki monitörün birleşim dikdörtgeni, monitörlerin kendisinden büyük olabiliyor: farklı
yükseklikte iki monitörde aradaki alanda monitör yok, `gdigrab` orayı siyah veriyor. Doğrulama
bunu hiç görmüyordu.

| Yerleşim | Bölge | `Covered` | YENİ Validate |
|---|---|---|---|
| (a) | 640x480+100,100 | True | (hatasız) |
| (a) | 640x480+1800,100 — masaüstünün sağından taşıyor | False | `... is not fully covered ...` |
| (c) | 640x40+-640,1040 — soldaki monitörün altındaki boşluk | False | `Region offsets cannot be negative.` **ve** `... is not fully covered ...` (iki hata birden) |
| (c) | 640x480+-1200,100 | True | (hatasız) |

İki monitöre yayılan (ikisinin de kapsadığı) bölge kabul ediliyor — test
`IkiMonitoreYayilanBolgeKabulEdilir`.

(c) satırı **iki** hata veriyor çünkü iki kural birden çalışıyor: bölge hem negatif X'te hem de
monitörlerin kapsamadığı boşlukta. K2'nin gevşettiği negatif kural kapsama koşuluna bağlı,
kapsanmayan bir dikdörtgende yine ateşleniyor.

### K4 — Bölge çizim örtüsü karışık DPI'da masaüstünü yanlış kaplıyordu

`RecorderRegionPicker.Cover()` (commit `e962538e`) şuydu:

    _desktop = RegionDraw.Desktop(Screens.All.Select(s => s.Bounds));
    var scaling = Screens.Primary?.Scaling ?? 1;
    Position = _desktop.Position;
    Width = _desktop.Width / scaling;
    Height = _desktop.Height / scaling;

Konum fiziksel piksel, boy ise nokta. Pencere `_desktop.Position`'da doğuyor — negatif X'teki
monitör varsa **o monitörde** — ve boyunu kendi monitörünün çarpanıyla piksele çeviriyor. Bölen
ise birincil monitörün çarpanı. Kaydedicinin diğer beş bindirmesi (`RecorderFrame`,
`RecorderInputOverlay` iki yerde, `RecorderMagnifier`, `RecorderMini`) çarpanı zaten
`ScreenFromPoint` ile alıyor; tek ayrık yer buydu.

| Yerleşim | Örtünün doğduğu monitörün ölçeği | ESKİ nokta boy | ESKİ fiziksel kaplama | YENİ nokta boy | YENİ fiziksel kaplama | Masaüstü birleşimi |
|---|---|---|---|---|---|---|
| (a) | 1 | 1920x1080 | 1920x1080+0,0 TAM | 1920x1080 | 1920x1080+0,0 TAM | 1920x1080+0,0 |
| (b1) | 1,25 | 1920x1080 | 2400x1350+0,0 TAM | 1920x1080 | 2400x1350+0,0 TAM | 2400x1350+0,0 |
| (b2) | 1,5 | 1920x1080 | 2880x1620+0,0 TAM | 1920x1080 | 2880x1620+0,0 TAM | 2880x1620+0,0 |
| (c) | 1 | 3200x1080 | 3200x1080+-1280,0 TAM | 3200x1080 | 3200x1080+-1280,0 TAM | 3200x1080+-1280,0 |
| (d) | 1,5 | 4800x1620 | **7200x2430+-2880,0 (2400x810 px fazla)** | 3200x1080 | 4800x1620+-2880,0 TAM | 4800x1620+-2880,0 |

Kusur yalnız (d)'de, yani iki monitörün **çarpanı farklıyken** çıkıyor. Tek ekranda ölçek ne
olursa olsun bölen ile pencerenin çarpanı aynı monitörden geldiği için kaplama tam; aynı
çarpanlı iki monitörde de tam. Düzeltme `RecorderLayout.Cover`: bölen, birleşimin sol üst
köşesini taşıyan monitörün çarpanı.

## 3. Ölçülmeyen

- Gerçek ikinci fiziksel monitör ve gerçek ölçek ≠ 1: bu makinede yok, yerleşim enjekte edildi.
  Ölçülen şey, verilen yerleşimden **üretilen yakalama dikdörtgeni** — çekilen karenin pikselleri değil.
- Örtü penceresinin karışık DPI'da gerçekten hangi monitörde doğduğu: hesap "birleşimin sol üst
  köşesini taşıyan monitör" varsayımına dayanıyor, pencere yöneticisinin yerleştirmesi ölçülmedi.
- Windows dışı platformlar: `x11grab` ve `avfoundation` kolları bu ölçümün dışında.
- ffmpeg'in DPI farkındalığı yalnız yukarıdaki ikili için ölçüldü.

## 4. Ham Çıktı

`tools/kaydedici-yerlesim/bin/Debug/net8.0/VidShrink.KaydediciYerlesim.exe` çıktısı, olduğu gibi:

```
# Kaydedici Yerlesim Olcumu
ffmpeg gdigrab koordinat uzayi: fiziksel piksel (surec PROCESS_PER_MONITOR_DPI_AWARE).

## (a) Tek ekran, olcek 1,0 — 1920x1080
monitorler: 1920x1080+0,0 @1

### Ekran hedefi: uretilen yakalama dikdortgeni (fiziksel piksel)
indeks	ESKI kol (index!=0 ? RegionForScreen : null)	ESKI yakalanan px	YENI ScreenCapture	YENI yakalanan px	monitorun kendisi
0	(yok)	1920x1080+0,0 (butun masaustu)	(yok)	1920x1080+0,0 (butun masaustu)	1920x1080+0,0

### Bolge cizim ortusu: masaustu kaplamasi (fiziksel piksel)
masaustu birlesimi	1920x1080+0,0
ortunun dogdugu monitorun olcegi	1
ESKI formul (Screens.Primary.Scaling=1)	nokta boy 1920x1080	fiziksel kaplama 1920x1080+0,0	TAM
YENI formul (kosedeki monitorun olcegi=1)	nokta boy 1920x1080	fiziksel kaplama 1920x1080+0,0	TAM

### Monitor bosluguna dusen bolge
640x480+100,100	Covered=True	Validate=(hatasiz)
640x480+1800,100	Covered=False	Validate=The capture region 640x480 at 1800,100 is not fully covered by the enumerated monitors; gdigrab records the uncovered part as black.

## (b1) Tek ekran, olcek 1,25 — 2400x1350 fiziksel
monitorler: 2400x1350+0,0 @1.25

### Ekran hedefi: uretilen yakalama dikdortgeni (fiziksel piksel)
indeks	ESKI kol (index!=0 ? RegionForScreen : null)	ESKI yakalanan px	YENI ScreenCapture	YENI yakalanan px	monitorun kendisi
0	(yok)	2400x1350+0,0 (butun masaustu)	(yok)	2400x1350+0,0 (butun masaustu)	2400x1350+0,0

### Bolge cizim ortusu: masaustu kaplamasi (fiziksel piksel)
masaustu birlesimi	2400x1350+0,0
ortunun dogdugu monitorun olcegi	1.25
ESKI formul (Screens.Primary.Scaling=1.25)	nokta boy 1920x1080	fiziksel kaplama 2400x1350+0,0	TAM
YENI formul (kosedeki monitorun olcegi=1.25)	nokta boy 1920x1080	fiziksel kaplama 2400x1350+0,0	TAM

### Monitor bosluguna dusen bolge
640x480+100,100	Covered=True	Validate=(hatasiz)

## (b2) Tek ekran, olcek 1,5 — 2880x1620 fiziksel
monitorler: 2880x1620+0,0 @1.5

### Ekran hedefi: uretilen yakalama dikdortgeni (fiziksel piksel)
indeks	ESKI kol (index!=0 ? RegionForScreen : null)	ESKI yakalanan px	YENI ScreenCapture	YENI yakalanan px	monitorun kendisi
0	(yok)	2880x1620+0,0 (butun masaustu)	(yok)	2880x1620+0,0 (butun masaustu)	2880x1620+0,0

### Bolge cizim ortusu: masaustu kaplamasi (fiziksel piksel)
masaustu birlesimi	2880x1620+0,0
ortunun dogdugu monitorun olcegi	1.5
ESKI formul (Screens.Primary.Scaling=1.5)	nokta boy 1920x1080	fiziksel kaplama 2880x1620+0,0	TAM
YENI formul (kosedeki monitorun olcegi=1.5)	nokta boy 1920x1080	fiziksel kaplama 2880x1620+0,0	TAM

### Monitor bosluguna dusen bolge
640x480+100,100	Covered=True	Validate=(hatasiz)

## (c) Iki ekran ayni olcek, ikincisi solda negatif X
monitorler: 1920x1080+0,0 @1 | 1280x1024+-1280,0 @1

### Ekran hedefi: uretilen yakalama dikdortgeni (fiziksel piksel)
indeks	ESKI kol (index!=0 ? RegionForScreen : null)	ESKI yakalanan px	YENI ScreenCapture	YENI yakalanan px	monitorun kendisi
0	(yok)	3200x1080+-1280,0 (butun masaustu)	1920x1080+0,0	1920x1080+0,0	1920x1080+0,0
1	1280x1024+-1280,0	1280x1024+-1280,0	1280x1024+-1280,0	1280x1024+-1280,0	1280x1024+-1280,0

### Bolge cizim ortusu: masaustu kaplamasi (fiziksel piksel)
masaustu birlesimi	3200x1080+-1280,0
ortunun dogdugu monitorun olcegi	1
ESKI formul (Screens.Primary.Scaling=1)	nokta boy 3200x1080	fiziksel kaplama 3200x1080+-1280,0	TAM
YENI formul (kosedeki monitorun olcegi=1)	nokta boy 3200x1080	fiziksel kaplama 3200x1080+-1280,0	TAM

### Monitor bosluguna dusen bolge
640x480+-1200,100	Covered=True	Validate=(hatasiz)
640x40+-640,1040	Covered=False	Validate=Region offsets cannot be negative. The capture region 640x40 at -640,1040 is not fully covered by the enumerated monitors; gdigrab records the uncovered part as black.

## (d) Iki ekran farkli olcek, ikincisi solda negatif X ve olcek 1,5
monitorler: 1920x1080+0,0 @1 | 2880x1620+-2880,0 @1.5

### Ekran hedefi: uretilen yakalama dikdortgeni (fiziksel piksel)
indeks	ESKI kol (index!=0 ? RegionForScreen : null)	ESKI yakalanan px	YENI ScreenCapture	YENI yakalanan px	monitorun kendisi
0	(yok)	4800x1620+-2880,0 (butun masaustu)	1920x1080+0,0	1920x1080+0,0	1920x1080+0,0
1	2880x1620+-2880,0	2880x1620+-2880,0	2880x1620+-2880,0	2880x1620+-2880,0	2880x1620+-2880,0

### Bolge cizim ortusu: masaustu kaplamasi (fiziksel piksel)
masaustu birlesimi	4800x1620+-2880,0
ortunun dogdugu monitorun olcegi	1.5
ESKI formul (Screens.Primary.Scaling=1)	nokta boy 4800x1620	fiziksel kaplama 7200x2430+-2880,0	EKSIK/FAZLA 2400x810 px
YENI formul (kosedeki monitorun olcegi=1.5)	nokta boy 3200x1080	fiziksel kaplama 4800x1620+-2880,0	TAM

### Monitor bosluguna dusen bolge
640x480+-2800,100	Covered=True	Validate=(hatasiz)
640x400+-640,1100	Covered=True	Validate=(hatasiz)
```

DPI farkındalığı yoklaması, olduğu gibi:

```
kosum 1 : pid=25172 hr=0 awareness=2 (PROCESS_PER_MONITOR_DPI_AWARE)
kosum 2 : pid=29252 hr=0 awareness=2 (PROCESS_PER_MONITOR_DPI_AWARE)
referans (bu PowerShell, SetProcessDPIAware cagrilmadi): hr=0 awareness=0
```

## 5. Mutasyon Koşumu

Her pimin bozulduğunda kırmızıya döndüğü, filtre `KaydediciYerlesimTests` (M1–M4 15 ölçü, M5–M6 17 ölçü):

| # | Bozulan | Kırmızıya dönen ölçü | Ham satır |
|---|---|---|---|
| M1 | `ScreenCapture` eski kurala döndürüldü (`index == 0` → `null`) | `IkiEkranliMasaustundeIlkEkranSecimiTekMonitoreDaralir` | `Assert.Equal() Failure: Strings differ / Expected: "1920x1080" / Actual: null` — Başarısız 1, Başarılı 14 |
| M2 | Negatif ofset kapsamaya bakmadan hata (`&& false`) | `NegatifXtekiMonitordeCizilenBolgeKabulEdilir`, `IkiMonitoreYayilanBolgeKabulEdilir` | `Assert.Empty() Failure: Collection was not empty` ×2 — Başarısız 2, Başarılı 13 |
| M3 | Kapsama uyarısı hiç verilmiyor (`Count >= 0` → erken çıkış) | `MonitorlerArasiBosluktakiBolgeSiyahDiyeReddedilir`, `MasaustuDisinaTasanBolgeReddedilir` | `Assert.Contains() Failure: Filter not matched in collection` ×2 — Başarısız 2, Başarılı 13 |
| M4 | `RecorderLayout.Cover` birincil monitörün çarpanını kullanıyor (`screens[0].Scale`) | `KarisikOlcekliOrtuKosedekiMonitorunCarpaniniKullanir` | `Assert.Equal() Failure: Values differ / Expected: 1,5 / Actual: 1` — Başarısız 1, Başarılı 14 |
| M5 | `Cover` köşeyi hiç sormuyor, doğrudan en büyük alanlı monitör (`var scale = LargestArea(screens);`) | `KosedekiMonitorEnGenisDegilkenDeKosedekininCarpaniSecilir` | `Assert.Equal() Failure: Values differ / Expected: 1,5 / Actual: 1` — Başarısız 1, Başarılı 16 |
| M6 | Yedek kol kaldırıldı (`ScaleAt(...) ?? 0d` → `Cover` null dönüyor) | `BirlesimKosesiBoslugaDusunceEnBuyukAlanliMonitorunCarpaniAlinir` | `System.NullReferenceException : Object reference not set to an instance of an object.` — Başarısız 1, Başarılı 16 |

M5, köşedeki monitörün **en geniş olmadığı** yerleşimi pimliyor (3840x2160 @1,0 birincil (0,0) +
1920x1200 @1,5 dizüstü (-1920,0)): orada `LargestArea` birincili, kural dizüstüyü seçiyor.
M6, yedek kolu koşturan L biçimli yerleşimi pimliyor (1920x1080 @1,0 (0,0) + 2880x1620 @1,5
(-2880,1080)): birleşimin sol üst köşesi (-2880,0) hiçbir monitörde değil. Bu iki yerleşim
yalnız test fikstüründe; ölçüm düzeneğinin beş yerleşimi ve yukarıdaki ham çıktı olduğu gibi duruyor.

M3'ün ilk denemesi `if (true) yield break;` ile yazılmıştı ve `error CS0162` ile derlenmedi;
o koşumda test ikilisi bayat kaldığı için sayılmadı, mutasyon derlenen bir koşula çevrilip
tekrarlandı. Dört mutasyon da geri alındı; geri alındıktan sonra
`dotnet build VidShrink.sln -c Release -warnaserror -m:2` 0 uyarı 0 hata.
