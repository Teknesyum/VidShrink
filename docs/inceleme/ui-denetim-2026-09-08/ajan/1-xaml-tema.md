# Ajan 1 — MainWindow.axaml + tema denetimi (ham çıktı, 152.742 token, 199 s)

## Kırık

- `Themes/Controls.axaml:356` — `PinkText` fırçası `#FFFF54EB` sabit renkle Controls içinde tanımlı; renk yalnız Theme.axaml'den gelmeli — `Color`+`Brush` çiftini Theme.axaml'e taşı (`PinkTextColor`/`PinkText`).
- `MainWindow.axaml:238` — `Effect="drop-shadow(0 0 6 #FF00F3FF)"` sabit renk ve ölçü — Theme'e `DropIconGlow` (ya da `GlowBlue` benzeri) belirteci ekle, `{StaticResource}` ile bağla.
- `MainWindow.axaml:421-443,537-539,562-564` — 13 `RadioButton` için hiçbir ControlTheme yok (`rg RadioButton Themes/` boş); görünüm FluentTheme'in vurgu renginden geliyor, belirteçlerden değil — `CheckStyle` gibi bir `RadioStyle` ControlTheme yaz.
- `Themes/Controls.axaml:700-703,717` — CheckBox şablonunda `24,8,*`, `Width/Height="24"`, `"20"` sabit; `CheckGlyphSize`(20) belirteci Theme'de tanımlı ve **hiç kullanılmıyor** — 20'leri `CheckGlyphSize`, 24'leri `TargetMinSize`, 8'i `SpaceSm` yap.
- `MainWindow.axaml:8` — `Width="1560" Height="1060"` Theme'deki `WindowPreferredWidth/Height` ile kopya; `MinWidth="1040" MinHeight="720"` için belirteç yok — cs zaten `WindowPreferred*` okuyor (`MainWindow.axaml.cs:299`); `WindowMinWidth/Height` ekle, XAML'de `{StaticResource}` kullan.

## Tutarsız

- `MainWindow.axaml:309,871` — `TxtTarget`/`TxtQuality` `Width="96"` sabit — `FieldWidthSm` belirteci.
- `MainWindow.axaml:1044` — `TxtDefaultTargetMb` `Width="120"` sabit — aynı sayı kutusu, 309 ile farklı genişlik; tek belirtece bağla.
- `MainWindow.axaml:1052,1069` — `TxtOutputFolder`/`TxtFfmpegPath` `Width="360"` sabit, yatay StackPanel içinde; uzun yol kırpılır — `Grid ColumnDefinitions="*,Auto"` + `HorizontalAlignment="Stretch"`.
- `MainWindow.axaml:47` — `BorderThickness="0,0,0,1"` sabit — `BorderThinBottom` belirteci ya da `PanelRule` deseni.
- `MainWindow.axaml:55,76,93` — logo `24x24`, kahve ikonu `16x16`, küçültme çizgisi `10x2` sabit — `IconSizeSm/Md`, `GlyphBarSize` belirteçleri.
- `MainWindow.axaml:77,234; Themes/Controls.axaml:721,963,1126` — `StrokeThickness` 1.5 / 2 / 2.5 / 1.5 / 2 beş farklı sabit — `StrokeThin/StrokeBold` belirteçleri (`DropOutlineThickness` gibi).
- `Themes/Controls.axaml:1151` — Slider kökü `Padding="10,0"` sabit — `SliderThumbSize/2` niyeti; `SliderEdgePadding` belirteci.
- `Themes/Controls.axaml:793,1235` — `Margin="0,0,0,2"` ve `Margin="1"` sabit — `SpaceXxs` ya da `BorderThin`.
- `Themes/Controls.axaml:489,564` — `ToolTip.ShowDelay="120"` sabit ms; Motion belirteçleri TimeSpan — `TooltipShowDelayMs` x:Int32 belirteci.
- `Themes/Controls.axaml:332,338,671` — hover/pressed `Opacity` 0.85/0.7 sabit; Theme'de opaklıklar belirteçli (`PanelSurfaceOpacity`) — `OpacityHover/OpacityPressed`.
- `Themes/Theme.axaml:339` — `TabPadding` tanımlı, kullanılmıyor; `NeonTabItem` `ChipPadding` kullanıyor (`Controls.axaml:815`) — ya sekmeye bağla ya sil.
- `Themes/Controls.axaml:134,157,169,379,421` — `HeroValue`, `StatusSuccess`, `SignatureText`, `SupportButton`, `LinkButton` hiçbir yerde kullanılmıyor (rg: yalnız tanım) — sil ya da `trash/`e.
- `MainWindow.axaml:23` — `Window.reduced-motion Border.enter` seçicisi ölü: azaltılmış harekette cs `enter-flat` sınıfını ekliyor (`MainWindow.axaml.cs:312`), `enter` hiç oluşmuyor — seçiciyi kaldır.
- `MainWindow.axaml:785,1021` — `BtnStart`/`BtnConvert` `IsDefault` yok; `BtnCancel`/`BtnConvertCancel` `IsCancel` yok; pencerede Enter/Escape işleyicisi de yok (`rg Key\. MainWindow.axaml.cs` boş) — `IsDefault="True"` / `IsCancel="True"`.
- `MainWindow.axaml:1138-1143` — sıfırlama onayında `BtnConfirmResetSettings` yıkıcı eylem, `BtnCancelResetSettings` `IsCancel` değil — Escape iptal etmeli.
- `MainWindow.axaml:80,84` — `TxtSponsor "Buy Me a Coffee"`, `TxtGitHub "Teknesyum"` sabit metin, `main.json`'da anahtar yok; marka adı olsa da diğer tüm metin `loc:Text` — `main.titlebar.sponsor`/`main.titlebar.author` anahtarı ekle (TR'de "Kahve Ismarla" olabilir).
- `MainWindow.axaml:310,393` — `"MB"`, `"/100"` birim etiketleri sabit; `Label` temasıyla büyük harf/izlemeli — `main.unit.mb`, `main.unit.per-hundred`.
- `MainWindow.axaml:904,934,965` — `TxtCustomResolution "1280x720"`, `TxtCustomFps "25"`, `TxtAudioBitrate "128"` varsayılanlar XAML'de sabit; hedef `TxtTarget "16"` ise ayarlardan geliyor — varsayılanları cs/ayar katmanına al.

## Kozmetik

- `Themes/Controls.axaml:472-610` — `InfoButton` ile `InfoButtonBesideLabel` şablon dahil ~%95 kopya; fark yalnız `Width/Height/Margin` — ikinciyi `BasedOn="{StaticResource InfoButton}"` + 3 setter yap.
- `Themes/Controls.axaml:388-419` — `TitleBarSupportButton` ile `TitleBarLinkButton` aynı 6 setter; fark renk ve hover — ortak `TitleBarButton` tabanı.
- `Themes/Controls.axaml:196-352` — `GhostButton` ve `PrimaryButton` ControlTemplate'i satır satır aynı (Root/Panel/ContentPresenter/FocusRing) — tek şablonu kaynak olarak paylaş.
- `Themes/Controls.axaml:1303-1351` — `ToolTip` ve `FlyoutPresenter` temaları birebir kopya — ortak setter'ları paylaş.
- `MainWindow.axaml:1204,1222` — `main.ai.tip` metni hem `TxtAiHint` hem balon (1209) hem `AiDetails` içinde (1222) üç kez gösteriliyor — birini bırak.
- `MainWindow.axaml:311-316,320,387` — Grid içindeki StackPanel girintisi bozuk (satır 316 ve 388 üst öğeyle aynı sütunda) — biçimlendir.
- `MainWindow.axaml:1177` — `TabItem` girintisi diğer sekmelerden 2 boşluk fazla.
- `MainWindow.axaml:338-341,362-365,372-375` — balon içinde iç içe iki `StackPanel` (aynı Spacing), dış katman işlevsiz — tekini kaldır.
- `MainWindow.axaml:200-201,781-782,1265-1266` — çift boş satırlar.
- `MainWindow.axaml:179-184` — `LangSwitch` boş StackPanel, `ZIndex="1"`, cs dolduruyor; ayrıca `SettingsLangSwitch` (1038) ikinci kopya — tek yer yeter mi, karar sana.

## Kanıtı olmadığı için yazılmayanlar

- Tanımsız `StaticResource`/`DynamicResource` anahtarı: **yok** (Themes/*.axaml tanımları ile axaml+cs kullanımı `comm` farkı boş).
- `TabIndex`: hiç kullanılmamış, dolayısıyla tutarsızlık yok; sıra görsel ağaçla aynı.
- İkon düğmelerinde `AutomationProperties.Name`: XAML'deki tüm `?`/`×`/`▾`/pencere düğmelerinde var; cs'de üretilen dil düğmelerinde de var (`MainWindow.axaml.cs:596`).
- Uzun panellerde `ScrollViewer`: beş sekmenin hepsi `ScrollViewer` içinde; `PlanPanel` kendi `PlanScroll`'unu taşıyor. Yalnız `TabPlayer` içeriği (198) sarılı değil — oynatıcı için kasıtlı görünüyor.
- Grid sütun tanımları: `306` (3 sütun × 5 satır, ColumnSpan 3), `260` (UniformGrid 4×2, 8 çocuk), `670` (2×3) tutarlı; hata bulunmadı.
- `EmberAtmosphere*`/`Phoenix*Opacity` belirteçleri ilk taramada "kullanılmıyor" çıktı; Theme.axaml içinde `WorkspaceBackground` çizimi kullanıyor — kusur değil.
