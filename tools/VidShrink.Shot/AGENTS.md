# VidShrink.Shot

README görsellerini üreten çekim düzeneği. T189'da kuruldu.

    dotnet run --project tools/VidShrink.Shot                      # docs/gorseller/ altına
    dotnet run --project tools/VidShrink.Shot -- <çıkış klasörü>
    dotnet run --project tools/VidShrink.Shot -- <çıkış klasörü> <klip.mp4>

**Pencere masaüstünde açılmaz.** Uygulama Avalonia'nın başsız platformunda (Skia çizimi
açık) kurulur, `MainWindow` 1600x1000 görüş alanında ölçülüp yerleştirilir, kök görsel
`RenderTargetBitmap` üzerine çizilir. Ekran kapısı gerekmez, masaüstü ölçeklemesi ve
pencere yöneticisi sonucu değiştirmez.

Ad kalıbı `docs/gorseller/T189-<konu>-<dil>.png`; diller `en` ve `tr`, konular
`kucult`, `donustur`, `ayarlar`, `gelismis`, `hakkinda`, `onizleme`, `oynatici`.
Tam pencere kareleri 1600x1000; `onizleme` panelin kendi ölçüsünde (506x512).

## Elle kalan adım

**Yok.** Klip verilmezse ffmpeg'in `testsrc2` deseninden `.calisma/T189/klip.mp4`
üretilir; oynatıcı ve önizleme kareleri onu sürer. Daha güzel bir kare isteniyorsa
gerçek bir video ikinci argüman olarak verilir — o zaman kare o videodan gelir.

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
düşürülmüyor. Yakalanan kare her koşumda aynı — üç koşumluk sha256 tablosu
`docs/olcumler/ekran-goruntusu.md` içinde.

Bekleme süreleri de sessiz değil: süre dolarsa `Await` `TimeoutException` atar,
yarım kare teslim edilmez.

`.sln`e eklenmedi; CI'a Avalonia.Headless taşımaz. Sonucu: `dotnet build
tools/VidShrink.Shot` hiçbir otomatik koşumda çalışmıyor, düzenek yalnız elle
derleniyor.
