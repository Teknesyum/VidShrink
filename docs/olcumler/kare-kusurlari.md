# T192 - Karelerde Gorunen Arayuz Kusurlari

Kaynak kare: `docs/gorseller/T189-kucult-en.png`. Olcum basiz pencerede, uygulamanin
kendi yerlesim motoruyla (`AppHost.Run` + `Measure`/`Arrange`/`UpdateLayout`) alindi.
Ham ciktilarin tamami `.calisma/T192/` altinda; her sayinin yaninda dosya adi var.

## 1. Sayi bicimi Turkce kalmis - kusur, duzeltildi

Iki ayri yanlis vardi, ikisi de ayni karede gorunuyordu.

`MainWindow.Num` sabit `CultureInfo.InvariantCulture` kullaniyordu; yani Turkce arayuzde
de nokta yaziyordu. Buna karsilik ekranin bir bolumu `Num`'dan hic gecmiyor, C# ara deger
dizgesiyle yaziliyordu - ve o dizge **makinenin** kulturunu kullandigi icin Ingilizce
arayuzde `15,6 MB` cikiyordu. Karedeki `15,6 MB`, `15,4 - 15,8 MB` ve `%3,7` ucu de bu
ikinci yoldan geliyordu.

Yuzde ayri bir kusurdu: isaret elle yazilarak sayinin **soluna** konuyordu, oysa
isaretin yeri kulturle degisir.

| Nerede | Satir |
|---|---|
| `src/VidShrink.App/MainWindow.axaml.cs` | 574 - `Num`, artik `Strings.Culture` |
| `src/VidShrink.App/MainWindow.axaml.cs` | 581 - `Percent`, yeni gecit (`P1`) |
| `src/VidShrink.App/MainWindow.axaml.cs` | 2924 - tahmin araligi satiri `Num`/`Percent`'ten geciyor |

Ayni desen `HumanDuration`, `TxtSize`, `TxtOutSize`, `TxtEstimateNote` ve kalite
satirlarinda da vardi; hepsi ayni gecide baglandi.

Olcu sabiti sabitle karsilastirmiyor: gercek bicimlendiriciyi iki dilde cagirip
ciktilarin birbirinden **farkli** oldugunu da tutuyor
(`BiciminTests.IkiDilinBicimiAyniDegildir`), boylece kulturu yeniden sabitleyen bir
mutasyon kirmizi doner.

Ham cikti: `.calisma/T192/k2k3k4-yesil-liste.txt`

| Cagri | en | tr |
|---|---|---|
| `Num(15.6, "0.0")` | `15.6` | `15,6` |
| `Percent(0.037)` | `3.7%` | `%3,7` |
| tahmin araligi satiri | `15.4 - 15.8 MB` + `3.7%` | `15,4 - 15,8 MB` + `%3,7` |

## 2. Baslik kurali govde cumlesine uygulaniyor - kusur, duzeltildi

`fd6fe0c1` `LanguageCatalog.ReadsAsProse`'u kurmustu ama sinav yalniz **noktalamaya**
bakiyordu: nokta, noktali virgul, unlem, soru isareti. Karedeki uc satirin hicbiri
noktalama tasimiyor, dolayisiyla ucu de baslik kolundan geciyordu.

Ayirt eden sey noktalama degil dilbilgisiydi. Ayni mekanizma genisletildi, ikincisi
kurulmadi: `ReadsAsProse` artik noktalama bulamazsa `CarriesFunctionWord`'e sorar -
baglac, ilgec, tanimlik, adil, yardimci fiil ya da soru sozcugu tasiyan metin
cumledir. Sinav **butun sozcuge** bakar, sozcugun icindeki harf obegine degil
(`storage.to` govde sayilmaz).

| Nerede | Satir |
|---|---|
| `src/VidShrink.App/LanguageCatalog.cs` | 101 - `FunctionWords` listesi |
| `src/VidShrink.App/LanguageCatalog.cs` | 124 - `ReadsAsProse`, noktalama + islev sozcugu |
| `src/VidShrink.App/LanguageCatalog.cs` | 145 - `CarriesFunctionWord` |

| Dil dosyasindaki metin | Once (kare) | Simdi |
|---|---|---|
| `Load a file to see the two sides` | `Load A File To See The Two Sides` | `Load a file to see the two sides` |
| `What it will do` | `What It Will Do` | `What it will do` |
| `Quality and compatibility` | `Quality And Compatibility` | `Quality and compatibility` |
| `Cropping and resolution` | `Cropping And Resolution` | `Cropping and resolution` |

Karsi yon de olculuyor: gercek basliklar kelime kelime buyutulmeye devam ediyor
(`Video Codec`, `FPS`, `Current Output Size`, `Video Kodegi`). Bu olcu olmasa
`ReadsAsProse`'u her zaman dogru dondurmek yesil kalirdi.

## 3. Kesilen satir - kusur, duzeltildi

Katlanmis bolum basligindaki ozet yatay bir `StackPanel` icindeydi. Yatay yigin
cocuguna sonsuz genislik verir; ozet kendi istedigi genislikte olculuyor, panel
kenarinda **dumduz** kesiliyordu. Karedeki `May Lower Resolution / May Low...` bunun
gorunusu.

Satir `Grid ColumnDefinitions="Auto,Auto,*"`e cevrildi; ozet yildiz sutunda duruyor ve
`TextTrimming="CharacterEllipsis"` ile kisaliyor. **Ucnokta tasarim geregi**: ozet
katlanmis basligin ikincil bilgisidir, bolum acilinca tam metin gorunur. Yeni renk ya
da olcu belirteci eklenmedi; `Themes/Theme.axaml`'dan `SpaceSm` ve `ButtonPaddingSm`
kullaniliyor.

| Nerede | Satir |
|---|---|
| `src/VidShrink.App/MainWindow.axaml` | 414, 461, 493, 529 - dort ozet `Grid.Column="2"` + ucnokta |

Olcu: ozete uzun bir metin verilir, sonra metnin bittigi nokta satirin genisligiyle
karsilastirilir. Kirmizi cikti `.calisma/T192/k4-kirmizi.txt`, yesil
`.calisma/T192/k2k3k4-yesil-liste.txt`.

| Ozet | Kirmizi: metin nerede bitiyor | Satir genisligi | Yesil |
|---|---|---|---|
| `TxtQualitySummary` | 659,5 px | 529 px | satirin icinde |
| `TxtFrameSummary` | 672,5 px | 529 px | satirin icinde |
| `TxtAdvancedSummary` | 630,5 px | 529 px | satirin icinde |
| `TxtAudioSummary` | tasmiyordu | 529 px | satirin icinde |

`TxtAudioSummary` kirmizi halde de tasmiyordu; icerigi kisa. Olcu yine de onu tutuyor,
cunku tasima duzeni dordunde de ayni.

## 4. Ozet ile kutu uyusmuyor - kusur, duzeltildi

T0 bunu olcmemisti; olculdu ve **gercek cikti**.

`RestoreSizeCap` basinda `if (_chipSizeCapped) return;` vardi. Turetme satiri
`TxtTarget.Text`'i okuyor, dolayisiyla tavan **zaten acikken de** yenilenmesi
gerekiyordu; erken donus yuzunden kutuya 24 yazildiginda satir onceki degerde kaliyordu.
Ayrica onerilen hedef atandiktan sonra satir hic yenilenmiyordu.

| Nerede | Satir |
|---|---|
| `src/VidShrink.App/MainWindow.axaml.cs` | 3219 - `RestoreSizeCap`, erken donus kaldirildi |
| `src/VidShrink.App/MainWindow.axaml.cs` | 2478 - onerilen hedeften sonra `RefreshChipDerivation()` |

| Kutu | Kirmizi: turetme satiri | Yesil |
|---|---|---|
| `24` | `Target 16 MB / Automatic / Fill Target` | `24` iceriyor, `16` icermiyor |

Ham cikti: `.calisma/T192/k4-kirmizi.txt` (`TuretmeSatiriHedefKutusunuIzler`).

## 5. Kaynak bilgi etiketleri - kusur degil

Sirasinda "Turkce etiket kendi hucresinden tasiyor" diye bir suphe vardi. Olculdu:
tasmiyor. `InfoGrid` bir `UniformGrid` ve hucre genisligi **her iki pencere olcusunde
de 140 px** - ayar paneli sabit genislikte, pencereyle buyuyup kuculmuyor. En genis
etiket `Video Kodegi` 112,6 px; 27,4 px bosluk kaliyor.

Olculen iki pencere: 1600x1000 ve izin verilen en dar 1040x720
(`MainWindow.axaml:8`, `MinWidth="1040"`). Ikisinde de ayni sayilar cikti.

Ham cikti: `.calisma/T192/infogrid-genislikler.txt` ve
`.calisma/T192/infogrid-genislikler-1040.txt`

| Etiket | Metin | Hucre | Bosluk |
|---|---|---|---|
| `Video Kodegi` (tr) | 112,6 px | 140 px | 27,4 px |
| `Video Codec` (en) | 105,6 px | 140 px | 34,4 px |
| `HDR Araligi` (tr) | 99,1 px | 140 px | 40,9 px |
| `Cozunurluk` (tr) | 94,6 px | 140 px | 45,4 px |
| `Resolution` (en) | 90,9 px | 140 px | 49,1 px |
| `Kare Hizi` (tr) | 78,5 px | 140 px | 61,5 px |

Etiketlerdeki `TextWrapping="Wrap"` yerinde birakildi. Olculen bir kusuru kapatmiyor -
daha uzun bir cevirinin ileride tasmasina karsi onlem; boyle isaretlendi ki sonradan
"bu bir duzeltmeydi" diye okunmasin.

## Kalan

**K5 - kareler yenilenmedi, T0'a birakildi.** `tools/VidShrink.Shot` ve
`docs/gorseller/**` bu sozlesmenin `owns` listesinde degil; T189 ayri bir dalda ve
henuz birlesmedi. Duzeltmeler yazildi, kareler tazelenmedi.

**Olcemedigim.** Yukaridaki sayilarin hepsi basiz yerlesimden geliyor. Duzeltilmis
arayuzun gercek bir ekran goruntusune bakilmadi; onu K5 ile birlikte T0 gorecek.

## Tam kosu

`dotnet build -c Release`: 0 uyari, 0 hata.

Duzeltmelerden onceki tam kosu (`.calisma/T192/test-taban.txt`): 1949 testten 7'si
kirmizi - 6'si bu sozlesmenin gosterim icin bilerek geri alinmis duzeltmeleri, 1'i
asagidaki.

**`OynaticiBoruTests_DecoderPipe.Oldurulemeyen_surec_icin_KillTree_basarisiz_bildirir`
- T192 disi, duzeltilmedi.** Test Windows'un `System` surecini (PID 4) aliyor ve
`HasExited` okuyor; yukseltilmemis oturumda `Win32Exception: Erisim engellendi`
firlatiyor. Ne test dosyasina ne `DecoderPipe`'a T192 dokundu; `git diff
origin/main...HEAD` o yollarda bos. Makine ve izin kosuluna bagli, ayri bir is.

K6 pimleri (`WindowLayoutTests`, `AyarYuzeyiTests`, `QualityTargetUiTests`): 55/55
yesil, hicbiri yeniden temellendirilmedi - ham cikti `.calisma/T192/k6-pimler.txt`.
