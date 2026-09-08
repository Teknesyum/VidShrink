# Ajan 3 — Oynatıcı + ShrinkJobWindow denetimi (ham çıktı, 177.454 token, 197 s)

## Kırık

- `Playback/PlayerView.axaml.cs:267` + `Ffmpeg/Playback/DecoderPipe.cs:178-190` — "Oynat" hiçbir kare çizmiyor: `ContinuousPlayback.Pump` kareleri okuyup yalnız sayıyor, `Draw` sadece `RunSeekAsync`'ten (338) çağrılıyor; görüntü seek karesinde donuk kalır, `_seek.Target` ilerlemez — kare geri çağrısı ekle, `Draw`'ı oynatma döngüsüne bağla.
- `Playback/PlayerView.axaml.cs:93-96,359-363` + `PlayerView.axaml:27` — zoom kırık: tekerlek yalnız `_zoom`'u günceller, `Frame` boyutu bir sonraki `Draw`'a (seek) kadar değişmez; `Draw` `Scale*PanelScale` ile ölçeği çift sayar (`ZoomGesture.Scale` Band'da zaten `FitScale*PanelScale`, ZoomGesture.cs:157); `OffsetX/Y` hiç uygulanmıyor (pan yok); `Image Stretch="None"` Width/Height'ı yok sayar — zoom sonrası yeniden yerleş, `RenderTransform`/`Stretch=Fill` ve offset kullan.
- `Playback/ControlStrip.axaml.cs:150,265,273` — sürükleme sırasında `Position` setter `_position`'ı ezmeye devam ediyor (`Refresh` atlanıyor ama yazı yazılıyor); `PanelHost.Drain` her karede yazıyor (PanelHost.cs:756), bırakışta `SeekRequested` scrub noktası yerine oynatma konumunu gönderir — scrub sürerken dış yazımı yok say ya da ayrı `_scrubPosition` tut.
- `ShrinkJobWindow.axaml.cs:214-218,334-340` — pencere kapanınca yalnız aktif `_cts` iptal ediliyor; `PumpAsync` döngüsü kuyruktaki sonraki isteği kapalı pencerede yeni `EncodeRunner` ile başlatıyor — `OnClosing`'de `_pending.Clear()` + kapalı bayrağıyla döngüyü kes.
- `MainWindow.axaml.cs:490-498` — `Player.Close()` çağrılmıyor; `PlayerView` içindeki `DecoderPipe`/`AudioSink`/`ContinuousPlayback` (ffmpeg süreci) pencere kapanınca öksüz — `OnClosing`'e `Player.Close()` ekle.

## Tutarsız

- `Playback/PanelHost.cs:949-963` — boru dosya sonunda bitince (`Durdu`, `_submitted>0`) `Controls.IsPlaying` true kalır; şerit "❚❚" gösterir, ses/kaynak durmuştur — `Durdu` her durumda `IsPlaying=false`.
- `Playback/PlayerView.axaml.cs:261-262` — `_playing` boru yokken de terslenir, durum metni "oynatılıyor" der — `_pipe is null` ise erken dön.
- `Playback/PlayerView.axaml.cs:171-172,185-198` — TopLevel'a tünel `KeyDown` ekleniyor; oyuncu sekmesi görünürken her Space/Esc pencere genelinde yutulur (odaklı TextBox dâhil), üstelik 43'teki kendi handler'ıyla çift kayıt — odak `PlayerView` içindeyken sınırla.
- `Playback/ComparisonPanel.axaml.cs:504-512,662,870-878` — Space yalnız `Shell` odaklıyken çalışır; panoya tıklamak `Shell.Focus()` çağırmıyor, odak yalnız terfi ile geliyor — `OnStagePressed`'de `Shell.Focus()`.
- `Playback/PanelHost.cs:204-205` + `ComparisonPanel.axaml:124-139` — "yaklaşıklık rozeti" metni `PROCESSED · CRF n`; "yaklaşık/approximate" sözcüğü sözlükte yok (Locales grep boş), rozet yalnız yan etiketi tekrarlar — `playback.badge.approx` anahtarı ekle.
- `Playback/PlayerView.axaml.cs:305` — dil sınaması `== "tr"`; başka yerde `StartsWith("tr")` (ShrinkJobWindow.cs:360) — `tr-TR` altında İngilizce hata çıkar; ortak yardımcıyı kullan.
- `ShrinkJobWindow.axaml.cs:255` — ETA `mm\:ss`; 60 dk üstü kalan süre saat düşürülerek basılır — `ControlStrip.Clock` gibi saat dalı ekle.
- `ShrinkJobWindow.axaml:73-75` — `BtnClose` Bitti/Hata durumunda hâlâ "İptal" yazıyor — duruma göre "Kapat"a çevir.
- `ShrinkJobWindow.axaml.cs:284-288` — `ex.Message` ham (İngilizce/ffmpeg) olarak kullanıcıya iner — `main.run.ended` etiketi + ayrıntı.
- `Playback/ComparisonPanel.axaml.cs:973-984` — terfi hâlinde ağaçtan kopulursa `TopLevel` `KeyDown/PointerPressed` ve `overlay.SizeChanged` abonelikleri (652-653, 648) kalır; yalnız `Settle`'da (832-842) sökülüyor — `OnDetached`'da `Settle()` çağır.
- `Playback/ComparisonSurface.cs:297` + `PlayerView.axaml.cs:348` — bitmap DPI sabit `96`; `PlayerView` `Stretch=None` ile 150% ölçekte kare DIP=piksel sayılıp bulanık büyür — `RenderScaling` ile oluştur ya da hedef dikdörtgenle çiz.

## Kozmetik

- `Themes/Playback.axaml:134-135` — `#CC050507`, `#00050507` literal; `AppBgColor` (Theme.axaml:10) kopyası, tema değişince kayar — `Color` yerine `Opacity` taşıyan `SolidColorBrush`/`GradientStop` belirteçten türet.
- `Themes/Playback.axaml:87-113` — 20 ölçü Theme.axaml değerlerinin elle kopyası (yorum kabul ediyor); `StaticResource` ile bağla.
- `Playback/ComparisonPanel.axaml.cs:76,173,291,333,956` + `ComparisonSurface.cs:387` — belirteç yedekleri literal (`4`, `2`, `256`, `24`, `16`, `0xFF00F3FF` neon mavi) — yedeği sıfır/istisna yap, sayıyı tekrarlama.
- `Playback/ControlStrip.axaml.cs:52-54,96` — `0.25`, `360`, `160` yedekleri belirteç kopyası.
- `Playback/ComparisonPanel.axaml:100,108,158,159,174` — `BtnPanelMaximize/FullScreen/ZoomIn/ZoomOut` ve `SeparatorGrip` için `AutomationProperties.Name` yok; simge/`-`/`+` okuyucuya anlamsız.
- `Playback/ControlStrip.axaml.cs:201,311,334` + `PlayerView.axaml:17` — `|◀`, `❚❚`, `▶`, `--:-- / --:--`, `⋮` glif literalleri; erişilebilirlik adı `PlayPause/Restart`'ta var, `BtnPlayerMenu`'de var; yalnız merkezileştir.
- `ShrinkJobWindow.axaml.cs:113-114` — `"-"` yer tutucu literal.
- `Playback/ControlStrip.axaml.cs:277-298` — ok tuşları yalnız `Timeline` odaklıyken; Shell/pano odağında ok/PageUp yok — `OnShellKey`'e ilet.

## Kanıt bulunamayan / temiz
- İptal akışı: `EncodeRunner.cs:370` süreç `ct.Register→Kill(entireProcessTree)`, `:225` yarım dosya silinir — doğru.
- İlerleme: `Progress<T>` UI SynchronizationContext'te; `Fraction`/`Remaining` doğrudan basılıyor, hesap Ffmpeg katmanında.
- Etiketler: `LeftBadge`=ORİJİNAL sol, `RightBadge`=İŞLENMİŞ sağ; ayırıcıyla söner (`RefreshBadgeFade`) — tutarlı.
- Çözünürlük farkı: iki girdi ffmpeg `hstack`'te aynı `PanelWidth/Height`'a ölçeklenir (PanelHost 451-465) — hizalama sorunu yok.
- Çift tamponlama: `ComparisonSurface` 3 gözlü halka + tek `WriteableBitmap`; titreme kanıtı yok.
- `ComparisonSurface`/`SyntheticFrameSource`/`PreviewAudio` dispose yolları eksiksiz; `Strings.Changed` PlayerView'da detach'ta sökülüyor.
