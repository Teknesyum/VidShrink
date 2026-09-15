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

Sırada **A2** (oynatıcıyı `MainWindow`'dan önce açmak) ve **B** dalgası var. Her adımın
kazancı eşleşik A/B ile ölçülüp `docs/olcumler/acilis-hizi.md`'ye yazılacak.
