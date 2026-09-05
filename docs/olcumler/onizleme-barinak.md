# Onizleme Barinak Olcusu (T174, tur 2)

Konu: buyultulmus onizleme paneli disari tiklaninca kuculmuyordu. Cozum: `TopLevel`
uzerine tunel `PointerPressedEvent` dinleyicisi (`ComparisonPanel.axaml.cs:633`,
kaldirma `:821`), karsilastirma `TryDismissOnOutsideClick` (`:872-888`).

Tur 1 teslimi `db150ad` denetimden KALDI (B1-B7). Bu belge tur 2'nin duzeltmelerini ve
tazelenmis ham ciktisini tasir; asagidaki her bolum hangi bulguyu kapattigini soyluyor.

## K1 - Buyuten / kuculten yol sayimi (B1 kapatildi)

Tur 1'in aramasi `"Click +=\|PointerWheelChanged\|OnTopLevelKey\|OnTopLevelPointerPressed"`
idi -- tabloda zaten yazan isimleri ariyordu, kendini dogrulayan olcu. Bu tur yapisal
desenle tarandi (`+= On`, `+= (_`, `AddHandler`), ad bilmeden:

```
grep -n "+= On\|+= (_\|AddHandler" src/VidShrink.App/Playback/ComparisonPanel.axaml.cs
```

Ham cikti:

```
84:        Shell.PointerWheelChanged += OnWheel;
87:        BtnZoomIn.Click += (_, _) => Zoom(1, StageCentre());
88:        BtnZoomOut.Click += (_, _) => Zoom(-1, StageCentre());
89:        BtnPanelMaximize.Click += (_, _) => ToggleMaximize();
90:        BtnPanelFullScreen.Click += (_, _) => ToggleFullScreen();
632:            top.AddHandler(KeyDownEvent, OnTopLevelKey, RoutingStrategies.Tunnel);
633:            top.AddHandler(PointerPressedEvent, OnTopLevelPointerPressed, RoutingStrategies.Tunnel);
```

Bu liste `Shell.KeyDown += OnShellKey` gibi olay-body'si disinda tanimli abonelikleri
yakalamaz; onlar icin ayrica field/ctor taramasi:

```
grep -n "KeyDown +=" src/VidShrink.App/Playback/ComparisonPanel.axaml.cs
```

```
85:        Shell.KeyDown += OnShellKey;
```

`OnShellKey`in Escape dali (`:859-862`) da `Leave()`e gidiyor -- tur 1'in atladigi yedinci
yol budur (B1). `OnTopLevelKey` (tunel, `:865-869`) her zaman once yakalayip
`e.Handled = true` yaptigi icin calisma anda `OnShellKey`in Escape dali pratikte golgede
kalir, ama yapisal olarak ayri bir giris noktasi ve sayima giriyor.

| Yol | Satir | Yon |
|---|---|---|
| `Shell.PointerWheelChanged` (tekerlek yukari, `Zoom(1,..)`) | 84 | buyuten |
| `BtnZoomIn.Click` | 87 | buyuten |
| `BtnPanelMaximize.Click` -> `ToggleMaximize()`, `!_enlarged` dali | 89, 432-434 | buyuten |
| `BtnPanelFullScreen.Click` -> `ToggleFullScreen()`, `!_enlarged`/`Mid` dali | 90, 450-453 | buyuten |
| `Shell.PointerWheelChanged` (tekerlek asagi, `Zoom(-1,..)`) | 84 | kuculten |
| `BtnZoomOut.Click` | 88 | kuculten |
| `BtnPanelMaximize.Click` -> `ToggleMaximize()`, `_enlarged && Full` dali (`Restore()`) | 89, 425-429 | kuculten |
| `BtnPanelFullScreen.Click` -> `ToggleFullScreen()`, `_enlarged && Full` dali (`Restore()`) | 90, 444-448 | kuculten |
| `Shell.KeyDown` -> `OnShellKey` (Esc) -> `Leave()` | 85, 859-862 | kuculten |
| `OnTopLevelKey` (Esc, tunel, calisma anda oncelikli) -> `Leave()` | 632, 865-869 | kuculten |
| `OnTopLevelPointerPressed` -> `TryDismissOnOutsideClick` -> `Descend()` (yeni) | 633, 872-888 | kuculten |

Sayim: **buyuten N = 4** (tekerlek-yukari, ZoomIn, Maximize-buyume dali, FullScreen-buyume
dali), **kuculten M = 7** (tekerlek-asagi, ZoomOut, Maximize-kuculme dali,
FullScreen-kuculme dali, `OnShellKey` Esc, `OnTopLevelKey` Esc, disari-tiklama). Tur 1'de
M = 5 idi (disari-tiklama ve `OnShellKey`in Esc dali eksikti). Cift tiklama ve surukleme
yolu yok (`DoubleTapped` grep'i dosyada sifir sonuc verdi).

## K2 - Disari tiklama kuculur (B2, B4 kapatildi)

Karar: **tiklama yutulur** (`e.Handled = true`, `OnTopLevelPointerPressed:874`).
Gerekce: kapatma ve arkadaki denetimin eylemi iki ayri niyet; ayni tiklamanin ikisini de
tetiklemesi istenmeyen yan etki olur.

D0 geregi disari tiklama daima `Band`e iner -- bir asama geri degil, tam kapanma
(`TryDismissOnOutsideClick` -> `Descend()`, `:887`). `Esc` ise D0'dan sonra da bir asama
kucultuyor (K4).

Ham kosum (`dotnet test --filter OnizlemeBarinakTests --no-build -c Release --logger
"console;verbosity=detailed"`, ciktilar `ITestOutputHelper` ile -- kalici, `.calisma/T174/
onizleme_run.txt`):

```
K2FULL before=Full after=Band
K2MID before=Mid after=Band shellRect=0, 0, 492, 617
K2MID_ICERI before=Mid after=Band shellRect=0, 0, 492, 617 nokta=1000, 500
K2BAND before=Band dismissed=False after=Band promoted=False
```

| Kosum | Once (Shelter) | Sonra (Shelter) |
|---|---|---|
| Full + disari tiklama (-5,-5) | Full | Band |
| Mid + disari tiklama (-5,-5) | Mid | Band |
| Mid + pencere-ici/panel-disi tiklama (1000,500) (B2, yeni) | Mid | Band |
| Band + `panel.TryDismissOnOutsideClick(5000,5000)` dogrudan cagri (B4, yeniden yazildi) | Band | Band (`dismissed=False`) |

B4: `Band`de `AddHandler` zaten calismadigindan (`:632-633` yalniz terfide baglanir),
"disari tiklama Band'da hicbir sey yapmaz" onceden hicbir mutasyonla kirilamayan bir sus
olcuydu. Simdi `TryDismissOnOutsideClick`i dogrudan cagirip `false` donusunu ve
`Shelter`in `Band` kaldigini assert ediyor -- guard'in `if (!_promoted) return false;`
satirini (`:874`) gercekten test ediyor.

## K3 - Panel icine tiklama kucultmez (B3 kapatildi)

Tur 1'in `IndependentShellRect`i (adi farkli olsa da) beklenen dikdortgeni
`TryDismissOnOutsideClick` ile **birebir ayni formulden** (`Shell.TranslatePoint(0,0,top)`
+ `Shell.Bounds.Size`) uretiyordu -- formul yanlis olsaydi test yine yesil kalirdi (B3).

Bagimsiz kaynak arayisinda iki alternatif denendi ve headless ortamin sinirlari olcu
belgesine not edildi:

- `panel.StageTarget` (uretim merkezinin/olceginin kendi hesap yolu -- `StageBounds()`,
  farkli formul) **dogru** deger uretiyor (`OverlayArea` -> `VisualLayerManager.Bounds`
  = `0,0,1560,1060`, tam pencere), ama `Shell` yukseltilince `OverlayLayer`e tasindigi ve
  bu headless test kosucusunda `OverlayLayer` hicbir zaman gercek bir arrange gecisi
  almadigi icin (`OverlayLayer.Bounds` daima `0,0,0,0`) `Shell.Bounds`/`Canvas.Left`-`Top`
  o hedefe hicbir zaman senkronlanmiyor -- iki kaynak headless'ta yapisal olarak
  uyusmuyor, uretim kodunun kendisi de ayni `Shell.Bounds` degerini okuyor.
- Bu yuzden nihai `IndependentShellRect`, `Shell.TranslatePoint`in kisayolunu degil,
  Avalonia'nin alt seviye `Visual.TransformToVisual(window)` matrisini kullanip
  dikdortgenin dort kosesini kendi elimizle donusturuyor (`OnizlemeBarinakTests.cs:78-99`).
  Bu, uretim kodunun cagirdigi kisayol yerine matrisi dogrudan isleyen, gercekten farkli
  bir Avalonia API yolu; iki sonuc headless'ta ayni alttaki `Shell.Bounds`i okudugu icin
  sayisal olarak eslesiyor, ama formul artik `TryDismissOnOutsideClick` ile satir satir
  ayni degil.

Bes nokta (Mid asamasinda, kabuk dikdortgeni `0,0,492,617`): dort kose + merkez.

Ham kosum:

```
K3PT point=(1,1) stage=Mid
K3PT point=(491,1) stage=Mid
K3PT point=(1,616) stage=Mid
K3PT point=(491,616) stage=Mid
K3PT point=(246,308,5) stage=Mid
```

| Nokta | Once | Sonra |
|---|---|---|
| Sol-ust (1,1) | Mid | Mid |
| Sag-ust (491,1) | Mid | Mid |
| Sol-alt (1,616) | Mid | Mid |
| Sag-alt (491,616) | Mid | Mid |
| Merkez (246,308.5) | Mid | Mid |

5/5 nokta asamayi degistirmedi.

## K4 - Odak ve klavye (B5 kapatildi)

D0'dan sonra `Esc` ile disari-tiklama ayri yollar: `Esc` -> `Leave()` (dugmeyle
buyutulmusse `Restore()`, tekerlekle buyutulmusse `Descend()`), disari-tiklama -> daima
`Descend()`. Tur 1 yalniz `Mid`den kosulmus tek bir `Esc` kosumuyle bu ayrimi olcmuyordu
(B5). Simdi uc ayri kosum var: tekerlekle `Full`e ulasilmis `Esc`, dugmeyle `Full`e
ulasilmis `Esc`, ve karsilastirma icin dugmeyle `Full`e ulasilmis disari-tiklama.

Ham kosum:

```
K4ESC first=Band second=Band
K4ESCFULL_WHEEL before=Full after=Band enlarged=False
K4ESCFULL_BUTTON viaButton=Full enlarged=True afterEscape=Mid
K2FULL_BUTTON viaButton=Full enlarged=True afterOutsideClick=Band
K4FOCUS onPromote=True released=True
```

| Kosum | Once | Sonra |
|---|---|---|
| Esc (Mid'den) | Mid | Band |
| Esc (Band'da, tekrar) | Band | Band (degisim yok) |
| Esc, tekerlekle ulasilmis `Full` | Full (`enlarged=False`) | Band |
| Esc, dugmeyle ulasilmis `Full` (`ToggleFullScreen`) | Full (`enlarged=True`) | Mid (`_restore` parametresine doner) |
| Disari tiklama, dugmeyle ulasilmis `Full` | Full (`enlarged=True`) | Band (Esc'ten farkli -- hep tam kapanma) |

| Kosum | Odak sahibi (once) | Odak sahibi (sonra) |
|---|---|---|
| Terfi (Mid) | `panel.Shell` degil | `panel.Shell` |
| Disari tiklama | `panel.Shell` | `panel.Shell` degil (`FocusManager.ClearFocus()`) |

Son iki satir D0'in ayrimini dogruluyor: ayni `Full` durumundan `Esc` (dugmeyle
ulasilmissa) saklanan boya donuyor, disari tiklama ise davranistan bagimsiz hep `Band`e
kapaniyor.

## K5 - Mutasyon (denetci notu kapatildi -- bu worktree'de yeniden kosuldu)

Her mutasyondan once `dotnet build -c Release --no-incremental` (0 hata), sonra
`dotnet test --filter OnizlemeBarinakTests --no-build -c Release`. Her iki mutasyon
uygulanip test kosulduktan hemen sonra `.calisma/T174/ComparisonPanel.axaml.cs.bak`
yedeginden geri alindi ve `diff` ile birebir ayniligi dogrulandi.

**(a) Disari tiklama dinleyicisi kaldirildi** -- `ComparisonPanel.axaml.cs:633`
(`top.AddHandler(PointerPressedEvent, OnTopLevelPointerPressed, RoutingStrategies.Tunnel);`)
satiri yorum satiri yapildi.

```
Basarisiz VidShrink.Tests.OnizlemeBarinakTests.K2_Full_kademede_disari_tiklama_bandin_iner
Basarisiz VidShrink.Tests.OnizlemeBarinakTests.K2_Mid_kademede_disari_tiklama_bandin_iner
Basarisiz VidShrink.Tests.OnizlemeBarinakTests.K2_Mid_kademede_pencere_ici_panel_disi_noktayla_disari_tiklama_bandin_iner
Basarisiz VidShrink.Tests.OnizlemeBarinakTests.K2_dugmeyle_buyutulmus_Full_kademede_disari_tiklama_da_bandin_iner
Basarisiz VidShrink.Tests.OnizlemeBarinakTests.K4_Terfi_odagi_tutar_disari_tiklama_odagi_birakir
Basarisiz! - Basarisiz:     5, Basarili:     5, Atlanan:     0, Toplam:    10, Sure: 1 s
```

**(b) Panel-ici tiklama korumasi kaldirildi** -- `ComparisonPanel.axaml.cs:885`
`if (rect.Contains(pointOnTopLevel)) return false;` -> `if (false && rect.Contains(pointOnTopLevel)) return false;`.

```
K3PT point=(1,1) stage=Band
K3PT point=(491,1) stage=Band
K3PT point=(1,616) stage=Band
K3PT point=(491,616) stage=Band
K3PT point=(246,308,5) stage=Band
Basarisiz VidShrink.Tests.OnizlemeBarinakTests.K3_Panel_icine_tiklama_asamayi_degistirmez
Basarisiz! - Basarisiz:     1, Basarili:     9, Atlanan:     0, Toplam:    10, Sure: 1 s
```

| Mutasyon | Kirilan olcu(ler) | Ham sonuc |
|---|---|---|
| (a) Disari tiklama dinleyicisi kaldirildi | `K2_Full_...`, `K2_Mid_...`, `K2_Mid_..._pencere_ici_panel_disi_...`, `K2_dugmeyle_buyutulmus_Full_...`, `K4_Terfi_odagi_tutar_...` | Basarisiz: 5, Basarili: 5, Toplam: 10 |
| (b) Panel-ici tiklama korumasi kaldirildi | `K3_Panel_icine_tiklama_asamayi_degistirmez` (5/5 nokta) | Basarisiz: 1, Basarili: 9, Toplam: 10 |

Her iki mutasyon geri alindi (`diff` ile dogrulandi), `dotnet build -c Release
--no-incremental` tekrar 0 hata, tam filtreli kosum yeniden 82/82 yesil (asagida).

## K6 - Kol sayisi

`dotnet test --filter "<kol>" --list-tests --no-build -c Release` her kol icin:

| Kol | Test sayisi |
|---|---|
| `OnizlemeBarinakTests` | 10 |
| `ComparisonPanelTests` | 44 |
| `ZoomGestureTests` | 28 |

Hicbir kol sifir donmedi. Toplam 10+44+28 = 82, asagidaki tam kosumla eslesiyor.

## Ham cikti kalicilik notu (B7 kapatildi)

Tur 1'in `K2FULL`/`K3PT`/`K4ESC` satirlari, teslim edilen kodda olmayan gecici
`Console.WriteLine`lardan geliyordu -- yeniden uretilemezdi. Bu turda tum ham cikti
`ITestOutputHelper._output.WriteLine(...)` ile yaziliyor (kod tabaninin kendi konvansiyonu,
`ComparisonPanelTests.cs`de de kullanilan yontem); yukaridaki her blok
`OnizlemeBarinakTests.cs`in teslim edilen haliyle birebir yeniden uretilebilir:

```
dotnet build -c Release --no-incremental
dotnet test --filter OnizlemeBarinakTests --no-build -c Release --logger "console;verbosity=detailed"
```

## Sonuc

`dotnet test --filter "OnizlemeBarinakTests|ComparisonPanelTests|ZoomGestureTests" --no-build -c Release`:

```
Basarili!  - Basarisiz:     0, Basarili:    82, Atlanan:     0, Toplam:    82, Sure: 7 s
```

**Basarili -- Basarisiz: 0, Basarili: 82, Toplam: 82.**