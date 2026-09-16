# Hipersürüş — çift tıktan ilk kareye

> "ister güncelleme olsun ister olmasın bir videoya tıklandığı oynatmaya geçme
> basamaklarını minimuma indirecek bir hipersürüş tasarısı tıklandığı anda ms ler
> içinde video oynayacak — gerekirse güncelleme ertelenecek bu konu mühim"

## Bugün nerede duruyoruz

Ölçü deponun kendi aracından: `tools/acilis-hizi/olcum.ps1`, enstrüman
`src/VidShrink.App/MainWindow.AcilisIzi.cs`, sonuçlar
[docs/olcumler/acilis-hizi.md](olcumler/acilis-hizi.md).

| Ölçü | Değer |
| --- | --- |
| `ilk-kare` ortancası, sıcak | 1857,8 ms |
| `ilk-kare` ortancası, soğuk | 1586,0 ms |
| `sekme` → `ilk-kare` arası | ~330 ms |
| libmpv ön yüklemesi (arka planda) | 74,0 / 67,1 ms |

**Bütçenin dağılımı burada saklı.** Motorun kendisi (mpv_create + ilk çözme) ~330 ms.
Geriye kalan ~1250-1500 ms, sekmeye geçilmeden önce, yani **oynatmayla hiç ilgisi
olmayan işlerde** harcanıyor. Hipersürüşün hedefi bu 1250 ms'dir; 330 ms'lik motor
payı ikinci sırada gelir.

Ayrıca ölçünün sıfır noktası app sürecinin `Process.StartTime`'ı
([acilis-hizi.md:10-14](olcumler/acilis-hizi.md)); **başlatıcının harcadığı süre bu
sayılara hiç girmiyor.** Kullanıcının beklediği süre bugün ölçtüğümüzden büyük ve
ne kadar büyük olduğunu bilmiyoruz. İlk iş bunu ölçmek.

## Fable'ın netleştirmesi

[docs/netlestirme/014](netlestirme/014-bir-videoya-cift-tiklandigi-andan-ilk-ka.md).
Beş soru sordu; üçünü burada cevaplıyorum, ikisi kullanıcının kararı:

- **Sıfır noktası** çift tıkın kendisi olacak: başlatıcı dahil. Ölçü bunu göremiyor,
  H0 bunu düzeltiyor.
- **İlk kare** tanımı bugünkü işaret kalıyor: `Frame.Source = _bitmap`
  ([PlayerView.axaml.cs:647](../src/VidShrink.App/Playback/PlayerView.axaml.cs:647)).
  Poster ya da boş pencere sayılmaz — kullanıcı "video oynayacak" dedi, çerçeve değil.
- **Doğrulama zemini** mevcut düzenek: eşleşik (paired) A/B, ortanca fark, ve her
  tekrarın hangi yöne baktığı. Ölçüm bu makinede eşleşmemiş karşılaştırmanın
  geçersiz olduğunu gösterdi ([acilis-hizi.md:22-40](olcumler/acilis-hizi.md)).
- **Hedef eşik** ve **hangi görünür davranışlara dokunulabileceği** kullanıcının kararıydı;
  16 Eylül 2026'da ikisi de cevaplandı, aşağıda.

## H0 — Ölçüyü çift tıka kadar geriye çek

Ölçemediğimiz şeyi iyileştiremeyiz. Başlatıcı, app'i doğurmadan önce sekiz iş
yapıyor ve hiçbiri ölçüde görünmüyor:

| Yer | İş | Neden açılış yolunda değil |
| --- | --- | --- |
| [Launcher/Program.cs:92](../src/VidShrink.Launcher/Program.cs:92) | `SeedVersionMarker` | Her açılışta disk yazımı |
| [:97](../src/VidShrink.Launcher/Program.cs:97) | `UpdateStage.ResumePending` | Bekleyen güncelleme varsa **yüzlerce MB kopyalama** |
| [:115](../src/VidShrink.Launcher/Program.cs:115) | `progress.WriteLog` | `update-log.txt`, her açılışta |
| [:116](../src/VidShrink.Launcher/Program.cs:116) → [Splash.cs:122](../src/VidShrink.Launcher/Splash.cs:122) | Splash join | Panel çizildiyse **2+2 sn tavan** |
| [:121](../src/VidShrink.Launcher/Program.cs:121) | `ToolsPresent` + PATH taraması | **Oynatma ffmpeg kullanmıyor** |

H0: başlatıcıya kendi iz işaretini koy, sıfır noktasını `CreateProcess` anına taşı,
ölçüyü tekrar al. Kazanç hedefi yok; bu **tartıyı kurmak**.

## A dalgası — Oynatma yolunu açılış yolundan ayır

Tek fikir: **argv'de bir video varsa uygulama açılmaz, oynatıcı açılır.** Geri kalan
her şey ilk karenin arkasına düşer.

### A1 — Başlatıcıyı yoldan çıkar (~ölçülecek, tahmin 300-800 ms)

Video yolu argv'deyse başlatıcı **önce** `Process.Start` eder, güncelleme işlerini
**sonra** yapar. Bugün `ResumePending` ve `ToolsPresent` app'in önünde
([Program.cs:97,121](../src/VidShrink.Launcher/Program.cs:97)); ikisi de app doğduktan
sonra koşabilir. `Updater.Run` zaten doğru tarafta ([:145](../src/VidShrink.Launcher/Program.cs:145)) —
onu örnek al.

Kullanıcının cümlesinin karşılığı bu madde: *"ister güncelleme olsun ister olmasın"*.
Bekleyen bir güncelleme varsa **bir sonraki açılışa** ertelenir, video önce oynar.

### A2 — Oynatıcıyı `MainWindow`'dan önce aç (tahmin 400-900 ms)

Bugün [MainWindow.axaml.cs:149](../src/VidShrink.App/MainWindow.axaml.cs:149)
`InitializeComponent()` **dört sekmenin tamamını** kuruyor: küçültme, dönüştürme,
kaydedici, ayarlar, gelişmiş paneller, karşılaştırma paneli. Arkasından ~60 `Watch`
bağlaması, liste kurulumları, `LoadTitleBarLogo`, `SetupShellMenu`
([:169-265](../src/VidShrink.App/MainWindow.axaml.cs:169)) ve `OnWindowLoaded`'daki
ayar yığını ([:500-508](../src/VidShrink.App/MainWindow.axaml.cs:500)) geliyor.
Kullanıcı bunların hiçbirini görmeyecek.

A2: argv'de video varken `PlayerView`'i taşıyan ince bir pencere önce açılır; dört
sekmeli kabuk ilk kareden **sonra**, arka planda kurulur ve oynatıcı oraya taşınır —
ya da kabuk hiç kurulmaz, kullanıcı bir sekmeye basana kadar bekler.

İki seçenek arasındaki fark kullanıcıya görünür (üst şerit ilk anda var mı yok mu),
bu yüzden karar kullanıcının.

### A3 — Motoru arayüzle paralel kur (tahmin 150-300 ms)

`new MpvEngine()` bugün arayüz iş parçacığında, senkron
([PlayerView.axaml.cs:529](../src/VidShrink.App/Playback/PlayerView.axaml.cs:529));
içinde `mpv_create` + `mpv_initialize` + render bağlamı var
([MpvEngine.cs:82,90,96](../src/VidShrink.Player/MpvEngine.cs:82)) ve
`mpv_initialize` soğuk açılışın en pahalı tek çağrısı.

A3: argv'de video varken motor, `libmpv` ön yüklemesinin hemen ardından
([App/Program.cs:182-188](../src/VidShrink.App/Program.cs:182)) arka planda kurulur
ve `loadfile` XAML açılımıyla **aynı anda** koşar. Pencere hazır olduğunda kare zaten
bekliyor olur.

### A4 — `Play()`'in önündeki dört dosya işini arkaya al (tahmin 20-80 ms)

`Play()` çağrısından **önce** koşan senkron disk işleri:

| Yer | İş |
| --- | --- |
| [PlayerView.axaml.cs:549](../src/VidShrink.App/Playback/PlayerView.axaml.cs:549) | `PlaybackHistory.Load` |
| [PlayerView.Window.cs:252-253](../src/VidShrink.App/Playback/PlayerView.Window.cs:252) | `PlayerSettings.Load` + `RecentFiles.Load` |
| [PlayerView.Window.cs:163](../src/VidShrink.App/Playback/PlayerView.Window.cs:163) | **`RecentFiles.Save` — diske yazım** |

Son kullanılanlar listesinin **yazımı** ilk karenin önünde duruyor. Dördü de
`Play()`'den sonra koşabilir; tek istisna `PlaybackHistory.Load`, çünkü kaldığı yere
arama ondan geliyor ([:559-561](../src/VidShrink.App/Playback/PlayerView.axaml.cs:559)).

### A5 — İlk kareyi saatten kopar

`StartRender()` 16 ms'lik bir `DispatcherTimer`
([PlayerView.axaml.cs:557](../src/VidShrink.App/Playback/PlayerView.axaml.cs:557));
ilk kare en kötü halde bir tam tık bekliyor. A5: ilk kare motorun kendi güncelleme
geri çağrısıyla ([MpvEngine.cs:96-104](../src/VidShrink.Player/MpvEngine.cs:96))
saati beklemeden çizilir, saat ikinci kareden itibaren devralır. Kazanç küçük
(0-16 ms) ama bedeli de küçük.

## B dalgası — Motorun kendi payı (~330 ms)

Bu dalga kullanıcıya görünür davranışa dokunuyor; **çatal 2** buraya bakıyor.

| # | Değişiklik | Bugün | Tahmin |
| --- | --- | --- | --- |
| B1 | `hwdec=auto-copy` varsayılan olsun | `no` ([MpvEngine.cs:143](../src/VidShrink.Player/MpvEngine.cs:143)) | çözme ucuzlar, kare kopyası durur |
| B2 | `demuxer-lavf-probe-info` / `demuxer-max-bytes` kısılsın | dokunulmuyor | ilk `loadfile` erken döner |
| B3 | `cache=no` ya da küçük önbellek | libmpv varsayılanı | ilk kare öne çekilir |
| B4 | `vo` SW render yerine donanım yüzeyi | `libmpv` BGRA, CPU kopyası ([:142](../src/VidShrink.Player/MpvEngine.cs:142)) | en büyük kazanç, **en pahalı iş** |

B1-B3 seçenek yazısı; B4 render yolunun yeniden yazımı ve
`ComparisonSurface`/`EngineComparisonFrameSource` de ondan besleniyor. B4 kendi
dalgası olmayı hak ediyor, A ve B1-B3 ölçülmeden başlanmaz.

## Sıra ve ölçü

1. **H0** — tartıyı kur. Bunsuz hiçbir sayı doğrulanamaz.
2. **A1, A4, A5** — görünür davranışa dokunmayan, ucuz, kesin kazançlar.
3. **A3** — paralelleştirme; A2'den önce çünkü A2'nin kazancını o açığa çıkarıyor.
4. **A2** — en büyük tek kalem, en görünür karar.
5. **B1-B3** — mpv seçenekleri, her biri ayrı ölçülür.
6. **B4** — ayrı dalga.

Her adım eşleşik A/B ile ölçülür ve kazancı `docs/olcumler/acilis-hizi.md`'ye yazılır.
Tahminler burada **tahmindir**; hükmü ölçüm verir. `tools/acilis-hizi` `.sln`'e
eklenip CI'da koşan bir eşik pimi kazanır — bugün bu yolun süresini ölçen **hiçbir
test yok** ve kazanılan her ms sessizce geri kaybedilebilir.

## İki çatal — cevaplandı (16 Eylül 2026)

**Çatal 1 — hedef eşik: 100 ms altı, mümkünse.** Mümkün değilse erişilebilenin en iyisi.
Bu eşik B4'ü (render yolunun yeniden yazımı) planın zorunlu bir dalgası yapıyor: A dalgası
tek başına ~300 ms'e iniyor, 100 ms'in altı motorun kendi payına dokunmadan görünmüyor.

**Çatal 2 — hepsi masada.** Görünür davranış değişebilir: A2'de dört sekmeli kabuk ilk
anda görünmeyebilir, B1'de donanım çözme varsayılan olabilir, B4'te render yolu değişebilir.

## Yapıldı

**H0 — tartı kuruldu.** Başlatıcının kendi izi var (`src/VidShrink.Launcher/AcilisIzi.cs`);
sıfır noktası artık başlatıcının doğumu ve bu an `VIDSHRINK_ACILIS_T0` ile app'e geçiyor,
yani iki sürecin satırları aynı eksende okunuyor. `olcum.ps1` iki yeni adım sayıyor:
`baslatici`, `app-dogdu`.

**A1 — başlatıcı yoldan çıktı.** Argümanda açılacak bir dosya varsa uygulama bakım
işlerinin önünde doğuyor; onarım, sürüm işareti ve güncelleme arkasına düşüyor. Bekleyen
dosyaların taşınması (`ResumePending`) o turda hiç koşmuyor, bir sonraki normal açılışa
kalıyor. ffmpeg varlık sınaması da atlanıyor: oynatma libmpv ile.

**A3 — motor arayüz ipliğinden çıktı.** `new MpvEngine()` artık `Task.Run` içinde;
`mpv_initialize` koşarken pencere kendi düzenini kuruyor.

**A4 — ayar yazımları oynatmanın arkasına düştü.** `AfterOpen` (ayar okuması, son
kullanılanlar listesinin diske yazımı) `TogglePlay`'den sonra çağrılıyor.

**A5 — ilk kare saatten koptu.** Çizim saati ilk kare düşene kadar 1 ms adımla koşuyor,
sonra 16 ms'e dönüyor; ayrıca kurulur kurulmaz bir kare deneniyor.

**Ölçüldü (16 Eylül 2026).** Eşleşik sıcak, 14 tekrar, taban `1a1385c1` — yeni `d2ea8bd5`:
dış saatte (`kabuk-ilk-kare`) ortanca fark **−71,0 ms**, 14 çiftin 9'u yeni yapı lehine.
Taban 1796,5 ms, yeni 1747,7 ms. Tam tablo ve okuma tuzakları:
[acilis-hizi.md](olcumler/acilis-hizi.md).

**Hedefe 1,7 saniye var.** Kalanın hepsi uygulamanın içinde: ~700 ms XAML açılımı ve yedi
sekmenin kurulması (**A2**), ~275 ms `mpv_create` ve ilk çözme (**B4**). 100 ms eşiği bu
ikisi yapılmadan görünmüyor; A dalgasının dokunabildiği yer zaten ~70 ms'ti.

Sırada **A2** (oynatıcıyı `MainWindow`'dan önce açmak) ve **B** dalgası var. Her adımın
kazancı eşleşik A/B ile ölçülüp `docs/olcumler/acilis-hizi.md`'ye yazılacak.

## C dalgası — Algı + JIT + palet (16 Eylül 2026)

Fable'ın netleştirmesi: [016](netlestirme/016-a-dalgasi-olculdu-cift-tik-ilk-kare-1796.md).
Beş soruyu burada cevaplıyorum.

**1. Hedefin bittiği işaret — iki saat.** Kullanıcının izin verdiği algı yolu hedefi
ikiye ayırıyor:

| Saat | Ne ölçer | Hedef |
| --- | --- | --- |
| `perde` | Çift tıktan **ekranda bir şey görünene** kadar | ~100 ms |
| `kabuk-ilk-kare` | Çift tıktan **gerçek videonun ilk karesine** kadar | elden geldiğince, üst sınır yok |

Perde bir örtü değil oyalama: arkasında gerçek iş koşuyor ve ilk kare gelir gelmez
kapanıyor. "Bitti" demeden kapanmıyor, donmuş bir panel gecikmeden kötüdür.

**2. İskeletin sahibi — başlatıcı.** Yeni bir çatı kurulmuyor: başlatıcıda **zaten**
çıplak Win32 bir panel var (`src/VidShrink.Launcher/Splash.cs`, 847 satır) ve görüntüsü
her derlemede `App.axaml`'ın paletinden üretiliyor. Bugün yalnız kurulum 400 ms'i geçince
çiziliyor. C2 eşiği sıfırlıyor ve panelin kapanış şartını değiştiriyor. Üçüncü süreç yok.

**3. Başlatıcının sırası — değişmiyor.** A1 bunu zaten yaptı: argümanda dosya varsa
uygulama bakım işlerinin önünde doğuyor.

**4. Teknik sınırlar.**

| Teknik | Karar |
| --- | --- |
| `PublishReadyToRun` | **Bu dalgada.** Yayın boyutu büyür, açılışta JIT'in payı düşer |
| `TieredPGO` | **Bu dalgada.** Bedava anahtar |
| `PublishAot` | **Yasak.** Avalonia XAML ve palet yansımayla kuruluyor |
| Yedi sekmeyi `UserControl`e bölmek | **Bu dalgada değil.** 4601 satırlık kod-arkası sekmelerin içindeki `x:Name`'lere bağlı; ayrı dalga, ayrı ölçüm |
| `hwdec` | **Bu dalgada değil.** B1 olarak duruyor, uyumluluk kolu ayrı ölçülür |
| Yerleşik bekleyen süreç | **Yasak.** Kullanıcının makinesinde boşta duran süreç bırakmıyoruz |

**5. Başarı ölçüsü — aynı protokol.** Eşleşik sıcak, 14 tekrar, ortanca, `olcum.ps1`.
Explorer'ın `CreateProcess` öncesi payı dış saate girmiyor; ölçer `Start-Process`'ten
sayıyor ve bu iki yapı için de aynı.

### C1 — ReadyToRun ve TieredPGO

`VidShrink.App.csproj` ve `VidShrink.Launcher.csproj` bugün hiçbir açılış anahtarı
taşımıyor; her açılışta bütün IL JIT'leniyor. `cerceve` öncesi 303 ms ile `xaml`
adımının 305 ms'i büyük ölçüde bu.

### C2 — Açılış perdesi

Başlatıcı uygulamayı doğurduğu anda paneli açıyor ve uygulamanın "ilk karem ekranda"
işaretini bekliyor. İşaret adlandırılmış bir olay (`VIDSHRINK_ACILIS_PERDESI` ortam
değişkeniyle geçen ad); uygulama ilk kareyi ya da ilk yüklenen pencereyi gördüğü anda
kuruyor. Olay gelmezse tavan süre panelin kendi durma kuralına düşüyor.

Yeni iz adımı: `perde`. `olcum.ps1` onu sayıyor, ölçüm tablosu iki saatli oluyor.

### C3 — Palet kısa devresi

`PaletteCatalog.Use` bugün ayardaki palet **zaten yürürlükteki palet olsa da** tam turu
koşuyor: iki palet dosyasını ayrıştırıyor, renk tablosu çıkarıyor, birleşmiş sözlüğün ilk
sırasını yeni bir `ResourceInclude` ile değiştiriyor ve kurulmuş bütün fırçaları geziyor.
Ölçümde `cerceve` → `palet` arası **194,1 ms**. İstenen palet yürürlüktekiyle aynıysa
yapılacak iş yok.

### Sıra

C1 ve C3 ucuz ve görünür davranışa dokunmuyor, önce onlar. C2 davranışı değiştiriyor,
sonra o. Üçü bittikten sonra tek bir eşleşik ölçüm; hüküm `kabuk-ilk-kare` ve `perde`
sütunlarından okunur.

**C dalgası yapıldı ve ölçüldü (16 Eylül 2026).** Eşleşik sıcak, 14 tekrar: dış saatte
(`kabuk-ilk-kare`) ortanca fark **−2603,3 ms**, **14 çiftin 14'ü** yeni yapı lehine. Yeni
`perde` adımı ortanca **211,4 ms** — bu oturumda tabanın ilk karesi 6506,3 ms'te geliyordu.
Makine bu oturumda ~3,6 kat kaymış durumda; mutlak sayılar oturumlar arası
karşılaştırılmaz, tam tablo ve okuma tuzakları
[acilis-hizi.md](olcumler/acilis-hizi.md).

Sırada **A2** (yedi sekmeyi ayrı `UserControl`lere bölmek) ve **B1-B4** (mpv seçenekleri,
render yolu) var. Perde algı saatini hedefe getirdi; gerçek ilk kareyi 100 ms'e indirmek
hâlâ bu iki dalgadan geçiyor.

## D dalgası — Sondayla ölçülmüş gerçek kalemler (16 Eylül 2026)

Fable'ın beş sorusu ([017](netlestirme/017-c-dalgasindan-sonra-cift-tik-ile-ilk-kar.md)) burada
cevaplanıyor.

1. **Hedef hangi saatte?** `perde` saatinde. Boşta koşan makinede `perde` **78,1 ms**;
   100 ms hedefi orada tutuyor. `kabuk-ilk-kare` için hedef yok, yalnız "her dalgada daha
   az" var: süreç doğumu + CLR + Avalonia çerçevesi tek başına ~320 ms ve bu taban
   `PublishAot` yasakken inmiyor.
2. **Hangi açılış senaryosu?** Çift tık senaryosu. Ölçüm zaten kabuktan dosya vererek
   koşuyor; boş açılış ayrı ölçülmüyor.
3. **Motor anahtarları nereye uygulanır?** Yalnız oynatıcının kendi motoruna
   (`PlayerView.EngineFactory`). Karşılaştırma paneli ve önizleme sesi kendi
   seçenekleriyle kuruluyor, bayt eşleme ölçüsü onlardan okunuyor.
4. **Görünür davranış değişikliği?** Kullanıcı "dalga dalga iznimi isteme" dedi; yapılıyor
   ve turun sonunda adıyla bildiriliyor. Bu dalgada bir tane var: oynatıcı çözmeyi
   donanıma veriyor.
5. **Kabul ölçüsü?** Eşleşik fark ortancası, 14 tekrar, aynı 6,2 MB klip. Mutlak sayı
   oturumlar arası kıyaslanmıyor.

### Sonda: `InitializeComponent`'in içi

`AcilisIsareti` iliştirilmiş özelliği XAML ağacının içine iz noktası koyuyor; derlenmiş
XAML'de öğe kurulurken yazıldığı için iki işaret arasındaki fark aradaki ağacın bedeli.
8 tekrar, boşta makine, ortanca ms:

| Aralık | Pay | Ne kuruluyor |
| --- | --- | --- |
| pencere-yapici → xaml-sekmeler | 50,0 | pencere kabuğu, başlık çubuğu |
| xaml-oynatici → xaml-kucultme | 18,4 | oynatıcı sekmesi (`PlayerView`) |
| xaml-kucultme → xaml-donusturme | 21,5 | küçültme sekmesi, 605 satır |
| xaml-donusturme → xaml-hakkinda | 4,8 | dönüştürme sekmesi |
| xaml-hakkinda → xaml-kaydedici | 1,4 | hakkında sekmesi |
| **xaml-kaydedici → xaml-gelismis** | **89,4** | **kaydedici sekmesi (`RecorderView`)** |
| xaml-gelismis → xaml-ayarlar | 1,2 | gelişmiş sekmesi |
| xaml-ayarlar → xaml-sekmeler-bitti | 3,0 | ayarlar sekmesi |

Yedi sekmeyi `UserControl`'lere bölmek (A2) **gereksiz**: altı sekmenin toplamı 50 ms,
tek başına kaydedici 89,4 ms. Ölçü A2'yi kapattı.

### Boşta makinede bugünkü tablo (8 tekrar, ortanca ms)

| Adım | Birikimli | Pay |
| --- | --- | --- |
| baslatici | 45,2 | 45,2 |
| perde | 78,1 | — |
| main | 100,2 | 44,6 |
| libmpv-hazir | 116,8 | 16,6 |
| cerceve | 319,8 | 203,0 |
| pencere-yapici | 361,4 | 37,5 |
| xaml | 576,5 | 215,1 |
| yapici-bitti | 647,4 | 70,9 |
| pencere-kuruldu | 819,6 | 172,2 |
| pencere-yuklendi | 921,0 | 101,4 |
| sekme | 942,0 | 21,0 |
| kare-kaynagi | 1214,8 | 272,8 |
| ilk-kare | 1216,6 | 1,8 |

### D1 — Kaydedici sekmesi tembel

`RecorderView` XAML'den çıktı; sekme ilk seçildiğinde kuruluyor
(`MainWindow.TembelSekme.cs`). Kenar payı `SectionMargin` belirtecinden okunuyor.

### D2 — Donanım çözme: ölçüldü, geri alındı

`hwdec=auto-copy` denendi. Eşleşik ölçümde motor adımı 272,8 ms yerine 297,0 ms oldu,
yaklaşık +24 ms. Kazanç değil kayıp; `EngineFactory` yazılımsal çözmeye döndürüldü.
Pim: `OynaticiYazilimsalCozuyor_DonanimOlculdu_GeriAlindi`.

### D3 — Motor pencere kurulurken açılıyor

`AcilisMotoru` (App/Playback): kabuktan dosya geldiğinde libmpv ısındıktan hemen sonra
motoru açıp bekletiyor; `PlayerView.OpenAsync` aynı yolu isterse hazır motoru devralıyor,
istemezse kendi motorunu kuruyor ve bekleyen motor `Birak` ile atılıyor.

Ölçü: `sekme → kare-kaynagi` 268,8 ms → 52,7 ms; eşleşik `kabuk-ilk-kare` farkı
−213,7 ms, 14 çiftin 13'ü lehine. Tablo
[docs/olcumler/acilis-hizi.md](olcumler/acilis-hizi.md) D dalgası.

### D dalgasından sonra kalanlar

`cerceve` ~216 ms (Avalonia çerçeve kurulumu), `Show()` ~173 ms, `Loaded` ~99 ms.
Üçü de çerçeve düzeyinde; kendi kodumuzda kesilecek büyük kalem kalmadı.

## E dalgası — Oynatıcı şeridi ve üst menü (16 Eylül 2026)

Dal `t0/oynatici-serit`. Dokunulan dosyalar: `PlayerView.axaml`, `PlayerView.Serit.cs`,
`Themes/Playback.axaml`, `Themes/Controls.axaml`, `MainWindow.axaml.cs`, iki test.

1. Medya yokken üst şerit de açık: `ChromeHidesItself` oynatıcı sekmesi **ve** yüklü medya ister.
2. Sessiz simgesi `_muted`'ı da okuyor (ham fare ölçüsünde bulundu: simge sessizde değişmiyordu).
3. Ses/hız okuması düz metin değil: mavi çerçeveli değer çipi, `100%` ve `1.00×`.
4. `PlaybackSlider` kendi şablonu: zaman çubuğunun yolu, dolgusu ve tutamacı — pembe dolgu yok.
5. Üst sekmeler sağdaki başlık düğmeleriyle aynı yüz: dolgu yok, mavi yazı, pembe üzerine gelme.
6. Şeridin üst anahattı güçlü mavi (`NeonBlueBorderStrong`), perde üst kenarda soluklaştırmasın.
7. Doğrulama: `OynaticiGercekGirdiTests` ham fareyle mute/ses/hız/oynat; `PencereKabuguTests` pinleri güncellenir.
