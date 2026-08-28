---
name: avalonia-kirpilma-olcutu
description: Avalonia'da DesiredSize>Bounds kirpilma olcutu hicbir kosulda kirmiziya donmez; sekme degisince yerlesim RelayoutAt ister
metadata:
  type: project
---

Bu projede iki Avalonia yerlesim tuzagi ucuncu kez cikti.

**Kirpilma olcutu olu.** `DesiredSize.Width > Bounds.Width` ile kirpilma aranan her olcu
sessizce yesildir. Avalonia bir denetimi istediginden dar yerlestirmiyor (`Bounds` en az
`DesiredSize` kadar), sarmayan uzun metnin istegi de olcum sirasinda verilen genislige
kirpiliyor. `MinWidth` hem olcumu hem yerlestirmeyi yukseltiyor, `WrapPanel` tasan cocugu
istedigi genislikte yerlestiriyor — dort tasma denemesinin dordunde de iki sayi esit.
Gercek tasma taşiyicinin gorus alaninda gorunur: blogun sag kenarini `TranslatePoint` ile
sayfa `ScrollViewer` uzayina cevirip `Viewport.Width` ile karsilastir.

**Sekme degisince tek olcum yetmiyor.** `tabs.SelectedIndex = index` sonrasi
`window.UpdateLayout()` yeni sekmeyi pencerenin `ClientSize`'i (bassiz kosumda sifir) ile
olcup temiz isaretliyor; ardindan gelen kok gecisi ona hic ugramiyor ve sinirlar sifir
kaliyor. Cozum: agacin tamamini `InvalidateMeasure()` edip yalnizca kok gorsel cocugu
olcup yerlestiren ikinci bir gecis — pencere hic surulmez.

**Why:** T65'te olculdu; kirpilma olcusu ilk sekme disinda hicbir seyi denetlemiyordu ve
duzeltildikten sonra da olcut yuzunden kirmiziya donemeyecegi ortaya cikti.

**How to apply:** Bir yerlesim olcusu "hicbir sey bulamadi" diyorsa once kac blok
gordugunu say. Sifir ya da yalniz pencere susu (bu projede 15 blok) goruyorsa olcu oludur.

Ilgili: [[vidshrink-pencere-testleri-kaynak-metinden]], [[silinen-tani-kosumu-kanit-degil]]
