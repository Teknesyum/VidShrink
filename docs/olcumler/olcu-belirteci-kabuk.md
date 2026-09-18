# Kabuk Denetimlerinin Ölçüsü Belirtece Çekildi

Proje kuralı: renk yalnız palet dosyasından, **ölçü yalnız `Themes/Theme.axaml` belirteçlerinden**.
`Themes/Controls.axaml` iki düz ölçü taşıyordu; tarama sırasında beş tane daha çıktı.

| Yer | Eski | Yeni | Belirteç |
|-----|------|------|----------|
| Slider `Root` | `Padding="10,0"` | `{StaticResource SliderTrackPadding}` | `SliderTrackPadding` = 10,0 |
| ScrollBar `ThumbVisual` | `Margin="1"` | `{StaticResource ScrollBarThumbMargin}` | `ScrollBarThumbMargin` = 1 |
| CheckBox `Grid` | `MinHeight="24"` | `{StaticResource TargetMinSize}` | vardı |
| CheckBox sütunları | `ColumnDefinitions="24,8,*"` | üç `ColumnDefinition` | `CheckColumnWidth` = 24, `CheckGapWidth` = 8 |
| CheckBox `Panel` | `Width/Height="24"` | `{StaticResource TargetMinSize}` | vardı |
| `CheckOutline`, `CheckMark` | `Width/Height="20"` | `{StaticResource CheckGlyphSize}` | vardı |
| `FocusRing` | `Width/Height="24"` | `{StaticResource TargetMinSize}` | vardı |

`10,0` uydurma bir sayı değil: `SliderThumbSize` 20, yarısı 10 — başlık iki uçta da izin içinde
kalsın diye. `1` ise 10 kalınlığındaki şeridin içine çekilen tek birimlik pay.

## Ölçü iki taraftan pimli

`OlcuBelirteciTests` hem kaynağı tarıyor hem denetimi gerçekten çizip okuyor:

- `TemaDosyasindaDuzOlcuYok` — `Controls.axaml`'da `Padding/Margin/Width/Height/MinWidth/MinHeight/
  MaxWidth/MaxHeight/Spacing/CornerRadius/BorderThickness` niteliklerinden hiçbiri rakamla başlamıyor.
- `TaramaDuzOlcuyuGercektenGoruyor` — pozitif kontrol: aynı desen `Padding="10,0"` ve `Margin="1"`
  metinlerini görüyor, `{StaticResource ...}` biçimini görmüyor. Tarayıcının kör olmadığının pimi.
- `KaydiriciDolgusuBelirtectenGelir`, `SeritBasliginKenariBelirtectenGelir`,
  `OnayKutusuKarelerininOlcusuBelirtectenGelir` — pencere içinde çizilen denetimin okunan ölçüsü
  belirtecin değeriyle eşit.

Çizen kol gerekliydi ve daha yazıldığı gün işe yaradı. `ColumnDefinition.Width` `GridLength`
istiyor; oraya `x:Double` belirteci konunca **derleme geçti**, çalışma anında patladı:

```
System.InvalidCastException : Specified cast is not valid.
   at CompiledAvaloniaXaml.XamlDynamicSetters.<>XamlDynamicSetter_53(ColumnDefinition, BindingPriority, Object)
   at CompiledAvaloniaXaml.!AvaloniaResources.XamlClosure_1.Build_46(IServiceProvider) in ...\Themes/Controls.axaml:line 721
```

Yalnız tarama koşsaydı bu yeşil görünürdü. Belirteçler `GridLength`e çevrilince düzeldi.

## Koşum

```
dotnet test ... --filter "OlcuBelirteciTests|BiciminTests|KareYerlesimTests|PaletteApplyTests|WindowLayoutTests|PencereKabuguTests|ThemeBackdropTests|BulletPaintingTests|ComparisonPanelTests|IkonKutusuTests|UstSeritTikTests|MiniKipOlcusuTests"
Başarılı!  - Başarısız:     0, Başarılı:   209, Atlanan:     0, Toplam:   209, Süre: 1 m 33 s
```
