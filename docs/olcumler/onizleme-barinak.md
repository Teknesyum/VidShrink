# Onizleme Barinak Olcusu (T174)

Konu: buyultulmus onizleme paneli disari tiklaninca kuculmuyordu. Cozum: `TopLevel`
uzerine tunel `PointerPressedEvent` dinleyicisi (`ComparisonPanel.axaml.cs:633`,
kaldirma `:821`), karsilastirma `TryDismissOnOutsideClick` (`:895`).

## K1 - Buyuten / kuculten yol sayimi

Kaynak: `src/VidShrink.App/Playback/ComparisonPanel.axaml.cs`, grep ile bulunan tum
asama-degistiren giris noktalari:

| Yol | Satir | Yon |
|---|---|---|
| `Shell.PointerWheelChanged` (tekerlek yukari, `Zoom(1,..)`) | 84 | buyuten |
| `BtnZoomIn.Click` | 87 | buyuten |
| `BtnPanelMaximize.Click` -> `ToggleMaximize()`, `!_enlarged` dali | 89, 432-436 | buyuten |
| `BtnPanelFullScreen.Click` -> `ToggleFullScreen()`, `!_enlarged`/`Mid` dali | 90, 450-453 | buyuten |
| `Shell.PointerWheelChanged` (tekerlek asagi, `Zoom(-1,..)`) | 84 | kuculten |
| `BtnZoomOut.Click` | 88 | kuculten |
| `BtnPanelMaximize.Click` -> `ToggleMaximize()`, `_enlarged && Mid` dali (`Restore()`) | 89, 426-430 | kuculten |
| `BtnPanelFullScreen.Click` -> `ToggleFullScreen()`, `_enlarged && Full` dali (`Restore()`) | 90, 444-448 | kuculten |
| `OnTopLevelKey` (Esc) -> `Leave()` | 865-869 | kuculten |
| `OnTopLevelPointerPressed` -> `TryDismissOnOutsideClick` -> `Leave()` (yeni) | 884-905 | kuculten |

Ham grep ciktisi (komut: `grep -n "Click +=\|PointerWheelChanged\|OnTopLevelKey\|OnTopLevelPointerPressed" src/VidShrink.App/Playback/ComparisonPanel.axaml.cs`):

```
84:        Shell.PointerWheelChanged += OnWheel;
87:        BtnZoomIn.Click += (_, _) => Zoom(1, StageCentre());
88:        BtnZoomOut.Click += (_, _) => Zoom(-1, StageCentre());
89:        BtnPanelMaximize.Click += (_, _) => ToggleMaximize();
90:        BtnPanelFullScreen.Click += (_, _) => ToggleFullScreen();
632:            top.AddHandler(KeyDownEvent, OnTopLevelKey, RoutingStrategies.Tunnel);
633:            top.AddHandler(PointerPressedEvent, OnTopLevelPointerPressed, RoutingStrategies.Tunnel);
865:    private void OnTopLevelKey(object? sender, KeyEventArgs e)
884:    private void OnTopLevelPointerPressed(object? sender, PointerPressedEventArgs e)
```

Sayim: **buyuten N = 4** (tekerlek-yukari, ZoomIn, Maximize-buyume dali, FullScreen-buyume
dali), **kuculten M = 6** (tekerlek-asagi, ZoomOut, Maximize-kuculme dali,
FullScreen-kuculme dali, Esc, disari-tiklama). Bu degisiklikten once M = 5 idi;
disari-tiklama eklenince M = 6 oldu. Cift tiklama ve surukleme yolu yok (repo genelinde
`DoubleTapped` grep'i `ComparisonPanel.axaml.cs` icinde sifir sonuc verdi).

## K2 - Disari tiklama kuculur

Karar: **tiklama yutulur** (`e.Handled = true`, `OnTopLevelPointerPressed:887`).
Gerekce: kapatma ve arkadaki denetimin eylemi iki ayri niyet; ayni tiklamanin ikisini de
tetiklemesi istenmeyen yan etki olur (bkz. `TryDismissOnOutsideClick` uzerindeki
XML-yorum, `:892-901`).

Ham kosum (test: `OnizlemeBarinakTests`, gecici `Console.WriteLine` ile, `dotnet test
--filter OnizlemeBarinakTests --logger "console;verbosity=detailed"`):

```
K2FULL before=Full after=Band
K2MID before=Mid after=Band shellRect=0, 0, 492, 617
K2BAND before=Band after=Band promoted=False
```

| Kosum | Once (Shelter) | Sonra (Shelter) |
|---|---|---|
| Full + disari tiklama (-5,-5) | Full | Band |
| Mid + disari tiklama (-5,-5) | Mid | Band |
| Band + disari tiklama (0,0) | Band | Band (degisim yok) |

## K3 - Panel icine tiklama kucultmez

Karsilastirma ekran/`TopLevel` koordinatinda (`Shell.TranslatePoint(.., top)`), mantiksal
agac ebeveynligine bakilmiyor — panel yukseltildiginde `OverlayLayer`e tasindigi icin
o iliski yok.

Bes nokta (Mid asamasinda, kabuk dikdortgeni `0,0,492,617`): dort kose + merkez.

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

## K4 - Odak ve klavye

```
K4ESC first=Band second=Band
K4FOCUS onPromote=True released=True
```

| Kosum | Once | Sonra |
|---|---|---|
| Esc (Mid'den) | Mid | Band |
| Esc (Band'da, tekrar) | Band | Band (degisim yok) |

| Kosum | Odak sahibi (once) | Odak sahibi (sonra) |
|---|---|---|
| Terfi (Mid) | `panel.Shell` degil | `panel.Shell` |
| Disari tiklama | `panel.Shell` | `panel.Shell` degil (`FocusManager.ClearFocus()`) |

## K5 - Mutasyon

Her mutasyondan once `dotnet build -c Release --no-incremental` (0 hata), sonra
`dotnet test --filter OnizlemeBarinakTests --no-build -c Release`.

| Mutasyon | Kirilan olcu(ler) | Ham sonuc |
|---|---|---|
| (a) Disari tiklama dinleyicisi kaldirildi (`AddHandler(PointerPressedEvent,..)` yorum satiri yapildi) | `K2_Full_kademede_disari_tiklama_bandin_iner`, `K2_Mid_kademede_disari_tiklama_bandin_iner`, `K4_Terfi_odagi_tutar_disari_tiklama_odagi_birakir` | `Basarisiz: 3, Basarili: 3, Toplam: 6` |
| (b) Panel-ici tiklama korumasi kaldirildi (`if (rect.Contains(..)) return false;` -> `if (false && ...)`) | `K3_Panel_icine_tiklama_asamayi_degistirmez` (5/5 nokta) | `Basarisiz: 1, Basarili: 5, Toplam: 6` |

Her iki mutasyon geri alindi, `dotnet build -c Release --no-incremental` tekrar 0 hata,
tam filtreli kosum yeniden 78/78 yesil.

## K6 - Kol sayisi

`dotnet test --filter "<kol>" --list-tests --no-build -c Release` her kol icin:

| Kol | Test sayisi |
|---|---|
| `OnizlemeBarinakTests` | 6 |
| `ComparisonPanelTests` | 44 |
| `ZoomGestureTests` | 28 |

Hicbir kol sifir donmedi.

## Sonuc

`dotnet test --filter "OnizlemeBarinakTests|ComparisonPanelTests|ZoomGestureTests"`:
**Basarili — Basarisiz: 0, Basarili: 78, Toplam: 78.**
