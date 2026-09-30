# VidShrink.Shot

README görsellerini üreten çekim düzeneği. T189'da kuruldu.

    dotnet run --project tools/VidShrink.Shot                      # docs/gorseller/ altına
    dotnet run --project tools/VidShrink.Shot -- <çıkış klasörü>
    dotnet run --project tools/VidShrink.Shot -- <çıkış klasörü> <klip.mp4>
    dotnet run --project tools/VidShrink.Shot -- bolge-paneli <çıkış klasörü> <etiket>

`bolge-paneli` kaydedicinin bölge düzenleyicisindeki araç panelini kendi ölçüsünde, her
evre için ayrı çizer (`bolge-paneli-<etiket>-<evre>.png`) ve boyutu stdout'a yazar.

**Pencere masaüstünde açılmaz.** Uygulama Avalonia'nın başsız platformunda (Skia çizimi
açık) kurulur, `MainWindow` 1600x1000 görüş alanında ölçülüp yerleştirilir, kök görsel
`RenderTargetBitmap` üzerine çizilir. Ekran kapısı gerekmez, masaüstü ölçeklemesi ve
pencere yöneticisi sonucu değiştirmez.

Ad kalıbı `docs/gorseller/T201-<konu>-<dil>.png`; diller `en` ve `tr`, konular
`kucult`, `donustur`, `kaydedici`, `ayarlar`, `gelismis`, `hakkinda`, `onizleme`,
`oynatici`, `duzenleyici`.
Tam pencere kareleri 1600x1000; `onizleme` panelin kendi ölçüsünde (EN 506x546, TR 482x546).

## Elle kalan adım

**Yok.** Klip verilmezse ffmpeg'in `testsrc2` deseninden `.calisma/T189/klip.mp4`
üretilir; oynatıcı ve önizleme kareleri onu sürer. Daha güzel bir kare isteniyorsa
gerçek bir video ikinci argüman olarak verilir — o zaman kare o videodan gelir.

`onizleme`, `oynatici` ve `duzenleyici` libmpv ister: `VIDSHRINK_LIBMPV` kurulu
kopyayı göstermeli (`%LOCALAPPDATA%\Programs\VidShrink\tools\libmpv\libmpv-2.dll`),
yoksa önizleme borusu 60 saniyede düşer. T200 klibi ffmpeg `mandelbrot` + `sine`
kaynağından 12 sn 1920x1080 üretildi. T201'de her video ekranı için gerçek videonun rastgele bir anından
12 sn 1280x720 30 fps kesit çıkarıldı (`ClipInfo` bu değerleri varsayar) ve araç kesit başına bir kez koşturuldu.

Oynatıcı karesinde şerit ve üst çubuk kendiliğinden gizleniyor; `OpenInPlayer`
`RevealSerit(true)` ve `ShowChrome(true)` çağırır, pencereye `reduced-motion` ekler ve
`TopOverlay`'in `reveal` sınıfını siler — başsız koşumda açılış animasyonu 0 opaklıkta
takılı kalıyordu. `duzenleyici` klibi düzenleyicide açar ve iki kesim yapar.

## Başsız koşumun elle oturttuğu üç geçiş

Masaüstünde saniyenin üçte biri süren geçişler başsız koşumda hiç bitmiyor; üçü de
`Settle`/`SelectTab` içinde elle kapatılıyor, yoksa kare yanlış çıkar:

- Giriş sınıfı (`enter`) panelleri saydam bırakıyor → `Transitions` boşaltılır, sınıf
  silinir, `Opacity` 1'e çekilir.
- `Fade` yavaşlaması posta kuyruğunda bekliyor → `Dispatcher.UIThread.RunJobs()`.
- Sekme geçişi (`TransitioningContentControl.PageTransition`) eskiyi yeninin altında
  bırakıyor → geçiş `null`lanır.

`MainWindow`'un `LoadWithoutProbing`, `SettleFades`, `UseLanguage`, `EntrancePanels`
üyeleri `internal`/`private` ve görünürlük yalnız `VidShrink.Tests`'e açık; üretim kodunu
bu araç için genişletmemek adına yansımayla çağrılıyor.

## Önizleme karesi belirlenimli

Karşılaştırma paneli canlı bir borudan besleniyor; "kare gelir gelmez çiz" koşumdan
koşuma farklı alt-kare yakalıyordu. `FreezePreview` bunun yerine oynatmayı durdurup
pencerenin **son** karesini bekliyor: son kare halkanın en yenisi olduğu için
düşürülmüyor. Yakalanan kare her koşumda aynı — on dört koşumluk sha256 tablosu
`docs/olcumler/ekran-goruntusu.md` içinde.

Statik kare de öyle değildi: `MainWindow.Recalculate` tahmin metni değişince `Pulse`
vuruyor, opaklığı 160 ms `0,35`te tutuyor ve kare atımın ortasına denk gelebiliyordu.
`StillPulses` çizimden hemen önce `TxtEstimateValue` ile `TxtDurationValue`'nun geçişini
silip opaklığı `1` yazar; çizimden önceki değer stderr'e `iz atim` satırı olarak düşer.

Çekim yük altında kırılabiliyor (`Oynaticinin ilk karesi: 60 saniyede gelmedi`). Her kare
kendi taze penceresinde en çok üç kez çizilir (`DrawAttempts`); denemeler stderr'e
`iz yeniden` satırı olarak düşer, üçü de düşerse çekim durur. Kısmi çıktı silinmez:
çıkış klasörü çoğu zaman `docs/gorseller`.

Bekleme süreleri de sessiz değil: süre dolarsa `Await` `TimeoutException` atar,
yarım kare teslim edilmez.

`.sln`e eklenmedi; CI'a Avalonia.Headless taşımaz. Sonucu: `dotnet build
tools/VidShrink.Shot` hiçbir otomatik koşumda çalışmıyor, düzenek yalnız elle
derleniyor.

Ayar yolu: `Main` ilk iş `VIDSHRINK_SETTINGS_PATH`'i `.calisma/shot-ayar/settings.json`'a çeker; ayar, oynatıcı geçmişi, son dosyalar ve kaydedici ayarı oraya düşer, kullanıcının `%APPDATA%\VidShrink`'ine değil.
