# T192 - Karelerde Gorunen Arayuz Kusurlari

Kaynak kare: `docs/gorseller/T189-kucult-en.png`. Olcum basiz pencerede, uygulamanin
kendi yerlesim motoruyla (`AppHost.Run` + `Measure`/`Arrange`/`UpdateLayout`) alindi.
Ham ciktilarin tamami `.calisma/T192/` altinda; her sayinin yaninda dosya adi var.

**Tur 2 (8 Eylul 2026).** Denetim tur 1'i iki kritikle geri cevirdi. Bu belgede tur 2'de
degisenler: 2. maddeye "Kuralin kapsami - dort satir degil, 124 anahtar" ve "Testlerde
yeniden temellendirilen yedi beklenen deger" bolumleri; 1. maddeye "Sabit invariant kalan
yerler" karar tablosu; 5. maddeye olcunun mutasyona olu olmasinin duzeltilmesi ve 1040 px
kosumunun parametrelenmesi; "Tam kosu"ya kirmizi/yesil tabaninin ne oldugu. K6 cumlesi
kapsamiyla sinirlandi.

**Tur 3 (8 Eylul 2026).** Denetim tur 2'yi bir kritikle geri cevirdi: tur 2'nin butun
sayilari tuttu, ama "124 kalem gozden gecirildi" denip 28'i okunmustu ve okunmayan 96'nin
icinde uc gerileme vardi. Bu turda degisenler: 2. maddedeki gozden gecirme bolumu 124'un
**tamamini** kapsayacak sekilde yeniden yazildi, iki gerileme kalibi (ad yazimi ve yer
tutucu) bulunup kapatildi, `LanguageTests` pimi gerekcesiyle yeniden temellendirildi;
1. maddedeki `InvariantCulture` sayisi bayattan (36) olculmuse (33) cevrildi ve karar
tablosuna eksik iki satir (570, 3742) eklendi; "Tam kosu" bolumune kirpilmamis cikti ve
kirmizilarin adlari yazildi. Tur 3'un ham ciktilari `.calisma/T192-tur3/` altinda.

**Tur 4 (8 Eylul 2026).** Denetim tur 3'u **gecirdi** (0 kritik); bu tur iki borcu kapatir.
Birincisi olcunun kendisiydi: yer tutucu/rakam duzeltmesini hicbir test tutmuyordu, cunku
`LanguageTests.cs:1110` uretimin biraktigi yuklemi kullaniyordu - test duzeltildi ve
mutasyon artik kirmizi. Ikincisi bir sayi: "44" aslinda **49**; dokumu ureten
`BiciminTests.TumCiktiDokulur` `ReplaceLineEndings` cagirmadigi icin cok satirli degerlerin
yalniz ilk satiri sayiliyordu. Ayrica iki cumle kapsamina cekildi (rakam kaynakli alti
kalem, `.title`/`.label` ile biten alti kalem). Tur 4'un ham ciktilari
`.calisma/T192-tur4/` altinda.

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
satirlarinda da vardi; **tur 1'de bunlar** ayni gecide baglandi. Tur 1'de baglanmayan
uc yer kaldi ve tur 2'de kapandi - asagidaki "Sabit invariant kalan yerler" bolumu
her `InvariantCulture` satirinin kararini tek tek yaziyor.

Olcu sabiti sabitle karsilastirmiyor: gercek bicimlendiriciyi iki dilde cagirip
ciktilarin birbirinden **farkli** oldugunu da tutuyor
(`BiciminTests.IkiDilinBicimiAyniDegildir`), boylece kulturu yeniden sabitleyen bir
mutasyon kirmizi doner.

Ham cikti: `.calisma/T192/k2k3k4-yesil-liste.txt`

| Cagri | en | tr |
|---|---|---|
| `Num(15.6, "0.0")` | `15.6` | `15,6` |
| `Percent(0.037)` | `3.7%` | `%3,7` |
| aralik satirinin bilesenleri (`Num`+`Percent`, ekran yolu degil) | `15.4 - 15.8 MB` + `3.7%` | `15,4 - 15,8 MB` + `%3,7` |
| `TxtEstimateRange` - **ekranin kendi satiri**, `RefreshEstimateView` kosarak | nokta, virgul yok | virgul, nokta yok |
| `TxtFps` - kaynak bilgi kutusu, `LoadWithoutProbing` yolundan | `59.94` | `59,94` |

Ilk satir bicim dizesini test icinde kuruyor, dolayisiyla `RefreshEstimateView`'deki bir
degisikligi yakalamaz; adi tur 2'de `AralikSatirininBilesenleriDileUyar` oldu. Alt iki
satir gercek ekran yolundan geciyor (`KareYerlesimTests.TahminAraligiSatiriEkrandaDileUyar`,
`KareYerlesimTests.KaynakBilgiKareHiziDileUyar`).

### Sabit invariant kalan yerler - satir basina karar (T192 tur 2, K9)

`grep -n InvariantCulture src/VidShrink.App/MainWindow.axaml.cs` **otuz uc** satir
veriyor; ham cikti `.calisma/T192-tur3/k12-invariant-grep.txt`.

Tur 2 buraya **otuz alti** yazmisti. Sayi bayatti: grep duzeltmelerden **once**
alinmisti ve kulture baglanan uc satir (1655, 2586, 3305-3306) o dokumde hala
`InvariantCulture` geciyordu. Satir numaralari dogruydu, sayi degil; tur 3'te teslim
ucunde yeniden sayildi. Karar alti kumede toplaniyor ve **otuz ucun her satiri** bir
kumede duruyor:

| Kume | Satirlar | Karar |
|---|---|---|
| **Baglandi (tur 2'de)** `TxtFps`, `DescribeBytes`, kalite siniri uyarisi | 2586, 1655, 3305-3306 | ekrana **cumle/deger** olarak cikiyor, kultur gecidine (`Num`) alindi |
| **Invariant kalir - ayristirma simetrisi.** `TxtTarget`, `TxtQualityTarget`, `TxtQuality`, gelismis ayar acilir listeleri | 837, 838, 848, 1176, 1182, 1191, 1194, 1299, 1303, 2476, 3191, 3230, 3272, 3293, 3751 | ayni metin geri **okunuyor**: 1368-1391, 2257, 3251, 3685-3686, 3704-3706, 3772 hepsi `NumberStyles` + `InvariantCulture` ile `TryParse` ediyor. Yaziyi kulture cevirip okumayi invariant birakmak Turkce arayuzde kutuyu bozardi; ikisini birden cevirmek bu sozlesmenin isi degil, ayri bir is |
| **Invariant kalir - kulture duyarli ogesi yok.** `TxtAdvCrfNow`, `TxtAdvAudioKbpsNow`, ses kanali, `mm:ss` | 1418, 1420, 1422, 3662 | tam sayi ve zaman bicimi; ondalik ayirici da grup ayirici da cikmiyor. Ayrica ilk ucu yukaridaki kutularin **yansimasi** |
| **Invariant kalir - ekrana cikmiyor.** ayar JSON'u | 2132 | diske yazilan bicim; kulture baglanirsa dosya makineden makineye degisir |
| **Cagri degil, belge.** `Num` gecidinin `<summary>`'sindeki `<see cref>` | 570 | grep'in yakaladigi tek dokumantasyon satiri; bir bicimlendirme yapmiyor, gecidin **neden** kuruldugunu anlatiyor |
| **Invariant kalir - yazma/okuma simetrisi.** `ParseTime` | 3742 | 3662'de `mm\:ss` diye **invariant yazilan** metnin geri okunmasi; yazan taraf invariant oldugu icin okuyan taraf da oyle olmak zorunda. Kulture duyarli ogesi yok |

`DescribeBytes` icin pim var (`SettingsTabTests`, 128 MiB / 25 GiB / 1 GiB); ucu de tam
ikilik kat, ondalik tasimiyor, dolayisiyla degisiklikten etkilenmedi ve **yeniden
temellendirilmedi**. Ondalikli tavan olcusu yeni: `BiciminTests.PaylasimTavaniDileUyar`
(`1,5 GiB` / `1.5 GiB`).

**Olcemedigim:** kalite siniri uyarisinin (`main.quality.below-floor`) ekrandaki hali
gercek yoldan kosturulmadi; bu satir bir planin sinira dayanmasini gerektiriyor.
Degisiklik `Num` gecidini kullaniyor ve gecidin kendisi olculu, ama satirin kendisi degil.

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

### Kuralin kapsami - dort satir degil, 124 anahtar (T192 tur 2, K8)

Yukaridaki dort satirlik tablo **karede gorunen** satirlar. Kuralin kendisi dort satira
degil, dil dosyasindaki her metne uygulaniyor. Tur 1 bu yan etkiyi olcmemisti; tur 2
olctu.

**Nasil olculdu.** `BaslikKapsamiTests` (`tests/VidShrink.Tests/BiciminTests.cs`) her dil
dosyasindaki her degeri gercek `LanguageCatalog.Title` uzerinden geziyor. Simulasyon yok;
gecit gercek. "Kol degistiren" tanimi: `fd6fe0c1`'in kuralina (yalniz cumle isareti) gore
**baslik**, bugunku kurala gore **govde**.

| Olcu | Sayi | Ham cikti |
|---|---|---|
| Dil dosyasindaki toplam anahtar | 950 (en 475, tr 475) | `.calisma/T192/k8-yeni-dokum.txt` |
| Kol degisteren | **124** | `.calisma/T192/k8-supurme-ham.txt` |
| bunlardan Ingilizce | 88 | ayni dosya, `SAYIM` satiri |
| bunlardan Turkce | 36 | ayni dosya, `SAYIM` satiri |
| **Ekrandaki ciktisi gercekten degisen** | **123** | `.calisma/T192/k8-fark.txt` |
| bunlardan uzunlugu <=3 sozcuk olan | 27 | `.calisma/T192/k8-kisa-kalemler.txt` |

Son satirdaki 123 bir tahmin degil, **iki kosunun farki**: ayni dokum testi bir de
`origin/main`'in `LanguageCatalog.cs`'siyle kosuldu ve iki dokum `diff`lendi. Yani
"gorunur cikti farki" bir olcut degil, olculmus bir sayi. Tekrarlanmasi:

```
git checkout origin/main -- src/VidShrink.App/LanguageCatalog.cs
dotnet test -c Release --filter "FullyQualifiedName~BaslikKapsamiTests.TumCiktiDokulur" --logger "console;verbosity=detailed"
git checkout HEAD -- src/VidShrink.App/LanguageCatalog.cs
```

Kol degistirdigi halde ciktisi **ayni kalan** tek anahtar var (124 - 123):
`tr / main.convert.drop`, degeri `At`. Tek sozcuk ve bas harfi zaten buyuk, iki kol da
ayni dizgeyi uretiyor.

Sayi **pimli**: `BaslikKapsamiTests.KolDegistirenAnahtarlarSayilir` 124/88/36'yi tutuyor.
Dil dosyasina metin eklenince pim kirilir; kirilinca yapilacak sey susturmak degil, yeni
sayiyi buraya yazmak.

### Istenmeyen var mi - 124'un **tamaminin** gozden gecirilmesi (tur 3)

Tur 2 bu basligi tasiyordu ama govdesi yalnizca kisa kalemleri okumustu. Basligin kapsami
govdenin kapsamina esit degildi ve **okunmayanin icinde uc gerileme vardi**.

Tur 3'te 124 kalemin **tamami** ham deger ve ekran degeriyle yan yana dokuldu, satir satir
okundu. Dokum `BaslikKapsamiTests.KolDegistirenAnahtarlarSayilir`in kendi ciktisindan
geliyor, elle yazilmadi: `.calisma/T192-tur3/kol-124-tur3.txt`, 124 satir, her satiri
`dil / anahtar / HAM / CIKTI`. Kapsam artik basliga esit.

#### Iki kume ayrilir

Tur 2 "27 kalemin tamami okundu" dedikten sonra bes kalem listeliyordu; **ucu o kumenin
icinde degildi**. Sinir ham degerin sozcuk sayisidir ve simdi olculdu:

| Kume | Sayi | Nerede okundu |
|---|---|---|
| `<=3` sozcuklu ham deger | **28** | tur 2 (`main.action.show-in-folder`, `main.chip.whatsapp.label` bu kumede) |
| `4+` sozcuklu ham deger | **96** | tur 3 (`main.drop.title` 5, `main.chip.128.label` 4, `main.chip.180.label` 5 sozcuk - bu kumede) |
| Toplam | **124** | |

Tur 2'nin "27" sayisi da bir eksikti; ayrim ham degerin bosluga gore bolunmesiyle yeniden
sayildi (`awk -F'	' '{n=split($4,a," ")...}'`, ham dosya yukarida).

#### Gerileme: ad yazimi cumle ortasinda kayboluyordu

96'nin icinde uc kalem, ekranda **adin yazimini kaybediyordu**:

| Anahtar | Tur 2 ciktisi | Tur 3 ciktisi |
|---|---|---|
| `en / main.drop.hint` | `Any format ffmpeg can open` | `Any format FFmpeg can open` |
| `en / main.reason.encoder-fallback-not-in-build` | `...part of this ffmpeg build...` | `...part of this FFmpeg build...` |
| `tr / main.reason.encoder-fallback-not-in-build` | `...bu ffmpeg derlemesinde yok...` | `...bu FFmpeg derlemesinde yok...` |

Kok neden `LanguageCatalog.cs`: `Names` sozlugu (`["ffmpeg"] = "FFmpeg"`) yalniz
`CapitaliseWord` icinde uygulaniyordu, `Sentence()` ise onu **sadece satir basi sozcugu**
icin cagiriyordu:

```
:179   builder.Append(lineStart ? CapitaliseWord(word, culture, true) : word);
```

Sonuc, ayni sozcugun cumlenin neresinde durduguna gore farkli yazilmasiydi:
`tr / main.error.generic` icindeki `Ffmpeg'in` satir basinda oldugu icin `FFmpeg'in`
oluyordu, `main.reason.encoder-fallback-not-in-build` icindeki ayni sozcuk ortada
oldugu icin `ffmpeg` kaliyordu. Bu, `CasingTests.UnitsAndEncoderNamesKeepTheirSpelling`in
belgelenmis amacina dogrudan aykiriydi.

**Cozum, `CarriesFunctionWord`u daraltmak degil.** Daraltmanin tek makul olcutu anahtar
adi olurdu (`*.title` haric tut) ve o zaman T0'in kusur dedigi `What It Will Do`
(`main.plan.title`) geri gelirdi. Bunun yerine `Names` gecidi `KnownSpelling` adiyla
disari alindi ve **her sozcukte** gecerli oldu - baslik kolunda da govde kolunda da:

| Nerede | Satir |
|---|---|
| `src/VidShrink.App/LanguageCatalog.cs` | `KnownSpelling` cikarildi; `CapitaliseWord` onunla basliyor, `Sentence` govde sozcugunde onu cagiriyor |

#### Ikinci gerileme: yer tutucu satir basini tuketmiyordu

96'yi okurken cikan ikinci kalip: `Sentence` satir basi sayacini yalnizca **harfe** gore
dusuruyordu (`if (word.Any(char.IsLetter)) lineStart = false;`). `{0}` harf tasimadigi
icin satir basi olmaya devam ediyor, ondan **sonraki** sozcuk buyuyordu:

| Anahtar | Dil dosyasinda | Tur 2 ciktisi | Tur 3 ciktisi |
|---|---|---|---|
| `en / main.section.advanced.overrides` | `{0} set by hand` | `{0} Set by hand` | `{0} set by hand` |
| `en / main.fast-gpu.bitrate-floor` | `{0} only follows...` | `{0} Only follows...` | `{0} only follows...` |
| `tr / main.reason.encoder-fallback-not-working` | `{0} kodlayicisi bu makinede...` | `{0} Kodlayicisi bu makinede...` | `{0} kodlayicisi bu makinede...` |
| `en / settings.share.can-delete` | `{0} hands out a delete token...` | `{0} Hands out a delete token...` | `{0} hands out a delete token...` |

Yer tutucunun yerine kodlayici adi, servis adi ya da sayi geliyor; o bir **icerik
sozcugudur**. Sayac harfe degil harf ya da rakama bakacak sekilde duzeltildi. Noktalama
(madde imi `•`, tirnak) satir basini hala tuketmiyor, cunku gercekten sozcuk degil - madde
imiyle baslayan `settings.share.tip` satirlarinda ilk gercek sozcuk buyumeye devam ediyor.

#### Iki duzeltmenin ekrandaki toplam etkisi

950 anahtarin ciktisi tur 2 ve tur 3 kodlariyla ayri ayri dokuldu ve `diff`lendi. Dokum
tur 4'te duzeltilmis olcuyle **yeniden** alindi
(`.calisma/T192-tur4/dokum-taban.txt`, `dokum-yeni.txt`, fark `fark.txt`):

| Degisen | Sayi |
|---|---|
| Ad yazimi geri geldi (`ffmpeg` -> `FFmpeg`, `crf` -> `CRF`) | **15** (en 8, tr 7) |
| Yer tutucudan sonraki sozcuk kucuk kaldi | **29** (en 10, tr 19) |
| Ciplak sayidan sonraki sozcuk kucuk kaldi | **6** (en 2, tr 4) |
| Ayni anahtar iki sinifta birden (`tr / main.reason.encoder-fallback-not-in-build`) | **-1** |
| **Toplam ayri anahtar** | **49** |

Tablonun ucuncu satiri ayri duruyor cunku duzeltme yalnizca yer tutucuyu degil **ciplak
sayiyi** da satir basi sayiyor (`char.IsLetterOrDigit`); `• 128 Kompakt` -> `• 128 kompakt`
kalemlerinin kaynagi rakam, yer tutucu degil.

Bu tablo tur 3'te **44** diyordu ve yanlisti. Sayim degil, sayimi besleyen dokum bozuktu:
`BiciminTests.TumCiktiDokulur` `ReplaceLineEndings` cagirmiyordu, cok satirli her degerin
yalniz **ilk satiri** dosyaya dusuyordu; devam satirlarindaki bes degisiklik farkta hic
gorunmedi. Ayni kusur `KolDegistirenAnahtarlarSayilir`de zaten duzeltilmisti, dokumde
duruyordu. Dokum duzeltildi (`BiciminTests.cs:557`), fark yeniden alindi.

Onemli olan: **ad yazimi duzeltmesinin 15'inden yalnizca 3'u 124'un icinde.** Kalan 12
kalem cumle isareti tasiyor, yani `fd6fe0c1`'den **once de** govde kolundaydi; kusur
T192'den eskiydi ve ayni duzeltmeyle kapandi. Kesisim ham olarak olculdu
(`comm -12 kol124-anahtar.txt degisen16.txt`).

Iki duzeltmenin **istenmeyen** bir yan etkisi yok: 49 satirin tamami elle okundu, hepsinde
yeni cikti dil dosyasindaki yazima daha yakin. Dokum duzeltilince gorunur olan bes kalem
de ayni yonde:

| Dil | Anahtar | Once | Sonra |
|---|---|---|---|
| en | `main.convert.audio-bitrate.tip` | `• 128 Is a compact...` | `• 128 is a compact...` |
| en | `main.convert.frame-rate.tip` | `• 60 Is smoother`, `• 30 Suits`, `• 24 Gives` | `• 60 is smoother`, `• 30 suits`, `• 24 gives` |
| tr | `main.convert.audio-bitrate.tip` | `• 128 Kompakt genel ayardir` | `• 128 kompakt genel ayardir` |
| tr | `main.convert.custom-fps.tip` | `• 25 Veya 29.97 gibi` | `• 25 veya 29.97 gibi` |
| tr | `main.convert.frame-rate.tip` | `• 60 Daha akicidir`, `• 30 Cogu videoya`, `• 24 Sinema benzeri` | `• 60 daha akicidir`, `• 30 cogu videoya`, `• 24 sinema benzeri` |

Besi de madde iminden sonra gelen ciplak sayinin ardindaki sozcuk; hepsinde yeni cikti dil
dosyasindaki yazimin kendisi.

#### Kalan 124'un karari

Tur 2'nin dort surpriz kalemi ve ayni ailenin besincisi (karar degismedi):

| Anahtar | Once | Simdi | Karar |
|---|---|---|---|
| `main.action.show-in-folder` (en) | `Show In Folder` | `Show in folder` | **istenen.** Dil dosyasinda `Show in folder` yaziyor; K3'un istedigi sey de tam olarak "dil dosyasindaki gibi kalir" |
| `main.drop.title` (en/tr) | `Drop A Media File Here` | `Drop a media file here` | **istenen.** Acik bir cumle, baslik degil |
| `main.chip.whatsapp.label` (tr) | `WhatsApp Icin Onerilen` | `WhatsApp icin onerilen` | **istenen.** Bir yonga etiketi, tamlama degil cumle parcasi |
| `main.chip.128.label` (tr) | `Paylasim Icin En Fazla` | `Paylasim icin en fazla` | **istenen.** Ayni aile |
| `main.chip.180.label` (tr) | `WhatsApp Web Icin En Fazla` | `WhatsApp Web icin en fazla` | ayni ailenin **ucuncu** uyesi |

Sinirdaki en acik kalem `main.retry.title` (en): `Over The Target` -> `Over the target`. Bir
iletisim basligi ve artik cumle bicimde; kural yine de daraltilmadi, gerekcesi yukarida.
Yaninda iki taban etiketi de sinirda sayilabilirdi: `main.advanced.min-fps.label`
(`Frame-rate floor (at least)`) ve `main.advanced.min-resolution.label`
(`Resolution floor (at least)`). Ikisi de alan etiketi, ikisinde de yeni cikti dil
dosyasindaki yazimin kendisi; ayni gerekceyle kural daraltilmadi.

Bolum basliklarinin (`main.section.*`) iki dilde de cumle bicimine gecmesi ayni sekilde
istenen sonuc: T0 `Quality And Compatibility` ve `Cropping And Resolution` satirlarini
kusur diye isaretlemisti.

Geri kalanin buyuk cogunlugu ayni desende: `main.reason.*` gerekce cumleleri,
`main.error.*` hata cumleleri, `playback.panel.*` bos panel metinleri. Bu uc ailenin
**disinda** alti kalem var ve adlari `.title` / `.label` ile bitiyor:

| Anahtar | Yeni cikti |
|---|---|
| `en\|tr / main.about.engine.title` | `How the engine thinks` / `Motor nasil dusunuyor` |
| `en / main.about.loss.title` | `Where the loss goes` |
| `en / main.advanced.min-fps.label` | `Frame-rate floor (at least)` |
| `en / main.advanced.min-resolution.label` | `Resolution floor (at least)` |
| `en / settings-tab.advanced-default-open.label` | `Show advanced options expanded by default` |

Altisi da tek tek okundu: adi `.title` olsa da metnin kendisi cumle bicimindedir (`How the
engine thinks` bir tamlama degil, bir cumledir). Yani hukum degismiyor - hicbiri baslik
gibi yazilmasi gereken bir metin degil - ama "hepsi `main.reason.*`, `main.error.*`,
`playback.panel.*`" cumlesi kapsam olarak eksikti; duzeltildi.

#### Pim

Uc olcu birlikte tutuyor:

| Olcu | Ne tutuyor |
|---|---|
| `BaslikKapsamiTests.KolDegistirenAnahtarlarSayilir` | 124 / 88 / 36 |
| `BaslikKapsamiTests.AdVeBirimYazimiCumleOrtasindaDaKorunur` | 950 anahtarin **tamamini** gezer; `Names`'de bildirilmis bir ad kendi yazimiyla cikmazsa kirmizi. Bugun: 950 gezildi, **0 kayip** |
| `LanguageTests.DilDosyasindakiButunGovdeCumleleriOlduguGibiKalir` | govde sozcukleri dil dosyasindaki gibi kalir, **ad duzeltmesi haric** |

Ucuncu olcunun pimi **tur 3'te calismiyordu**. `LanguageTests.cs:1110` kendi satir-basi
atlamasinda uretimin biraktigi yuklemi kullaniyordu:

```csharp
if (!basAtlandi && kaynakSozcukler[i].Any(char.IsLetter))
```

`{0}` ve ciplak sayi harf tasimadigi icin atlanmiyor, onlardan **sonraki** sozcuk
atlaniyordu - yani duzeltmenin dokundugu tek sozcuk olcunun kor noktasindaydi.
`LanguageCatalog.cs:190`i `IsLetterOrDigit` -> `IsLetter` yapan mutasyonda 135 testin
135'i yesil kaliyordu. Testin yuklemi de `IsLetterOrDigit` yapildi; ayni mutasyon artik
iki dilde de kirmizi:

```
Dil 'en': 276 govde degeri, 4327 sozcuk; 14 sozcuk bozuldu
Dil 'tr': 232 govde degeri, 3074 sozcuk; 25 sozcuk bozuldu
```

Toplam **39 sozcuk**. Dogru uretimde ayni olcu yesil (2/2). Ham ciktilar:
`.calisma/T192-tur4/k14-mutasyon.txt` ve `k14-dogru-uretim.txt`.

### Testlerde yeniden temellendirilen yedi beklenen deger

Uc test dosyasinda yedi beklenen deger degisti. Hicbiri pim susturmasi degil; hepsi
**eski degerin yanlis oldugu** kalemler. `Once` sutunu eski beklenen degerdir.

| Dosya | Girdi | Once | Simdi | Gerekce |
|---|---|---|---|---|
| `CasingTests` | `the probe took 120 ms` | `The Probe Took 120 ms` | `The probe took 120 ms` | `the` tanimlik; metin cumle |
| `CasingTests` | `budget of 20000 ms` | `Budget Of 20000 ms` | `Budget of 20000 ms` | `of` ilgec; eski deger Ingilizce baslik kuralina gore de yanlisti (`Of` hicbir kuralda buyuk yazilmaz) |
| `CasingTests` | `target is 16 MB` | `Target Is 16 MB` | `Target is 16 MB` | `is` yardimci fiil; metin cumle |
| `CasingTests` | `halve the fps` | `Halve The fps` | `Halve the fps` | `the` tanimlik |
| `CasingTests` | `hevc_qsv beats libsvtav1 on aac` | `hevc_qsv Beats libsvtav1 On aac` | `hevc_qsv beats libsvtav1 on aac` | `on` ilgec; satir basindaki `hevc_qsv` zaten `Verbatim` |
| `ChipTests` | `Why these choices` + sayi | `Why These Choices - 7` | `Why these choices - 7` | `why` soru sozcugu; T0'in kusur dedigi `What It Will Do` ile ayni desen |
| `LanguageTests` | `Back to the start` | `Back To The Start` | `Back to the start` | `to` ve `the` |

#### Tur 3'te yeniden temellendirilen sekizinci olcu: `DilDosyasindakiButunGovdeCumleleriOlduguGibiKalir`

Bu bir beklenen deger degil, bir **kural** degisikligi; K6 disiplini geregi susturulmadi,
gerekcesiyle yeniden temellendirildi.

Olcu once soyle diyordu: cumle isaretli her degerde, satir basi disinda hicbir sozcuk
degismeyecek. Satir basini disarida birakiyordu, cunku `Names` yalniz orada uygulaniyordu.
K11'in duzeltmesi `Names`i her sozcuge yaydigi icin olcu iki dilde de kirmiziya dondu -
**dogru sekilde**, cunku eski kural tam da kusuru kodluyordu:

```
Dil 'en': 276 govde degeri, 4327 sozcuk; 8 sozcuk bozuldu:
main.drop.hint: 'ffmpeg' -> 'FFmpeg'
...
Dil 'tr': 232 govde degeri, 3074 sozcuk; 7 sozcuk bozuldu:
main.error.generic: 'Ffmpeg'in' -> 'FFmpeg'in'
```

Yeni kural: govde sozcugu dil dosyasindaki gibi kalir, **bildirilmis ad yazimi haric**.
Ayrim olcunun icinde `AdYazimiDuzeltmesi` ile yapiliyor ve dar tutuldu - iki sozcuk
yalnizca buyuk/kucuk harfte ayrilacak **ve** ekrandaki sozcugun bas tarafi `Names`'deki
yazimla baslayacak. Baska her fark hala bozulmadir. Olcu iki sayiyi da basiyor: kac sozcuk
bozuldu, kac sozcuk ad olarak duzeldi (bugun 0 ve 8/7).

Testin **tuttugu sey** degismedi. `CasingTests.UnitsAndEncoderNamesKeepTheirSpelling`
birim ve kodlayici yazimini olcuyor (`ms`, `fps`, `libx264`, `h264_nvenc`, `libsvtav1`,
`aac`); o yazimlarin hicbiri degismedi ve iki satir (`software encoder libx264`,
`hardware encoder h264_nvenc`) **oldugu gibi** kaldi. `LanguageTests`'teki ayni onermede
`Control Strip` ve `Denetim Seridi` de degismeden duruyor - yani karsi yon hala olculu.

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

Sirasinda "Turkce etiket kendi hucresinden tasiyor" diye bir suphe vardi ve olcusu
yarim birakilmisti: sinav `.Where(pair => true)` yaziyordu, yani hucre genisligine hic
bakmadan her etiketi tasmis sayiyordu ve iki dilde de kirmizi doner, hicbir sey
soylemezdi. Gercek kosul (`metin genisligi > hucre genisligi`) yazildi ve olculdu:
tasmiyor. `InfoGrid` bir `UniformGrid` ve hucre genisligi **her iki pencere olcusunde
de 140 px** - ayar paneli sabit genislikte, pencereyle buyuyup kuculmuyor. En genis
etiket `Video Kodegi` 112,6 px; 27,4 px bosluk kaliyor.

Olculen iki pencere: 1600x1000 ve izin verilen en dar 1040x720
(`MainWindow.axaml:8`, `MinWidth="1040"`). Ikisinde de ayni sayilar cikti.

**Tur 2 duzeltmesi - olcu mutasyona oluydu.** Tur 1'de kosul
`label.TextLayout.Width > cell.Bounds.Width` idi, ama etiketlerde `TextWrapping="Wrap"`
var (`MainWindow.axaml:262-291`): sarilan metnin yerlesim genisligi hucreyi **hic
asamaz**, dolayisiyla kosul her girdide yanlis donerdi. Karar dogruydu, korumasi yoktu.
Olcu artik sarmayi kapatip (`TextWrapping.NoWrap`) yeniden yerlestiriyor. Yanina bir
mutasyon sinavi kondu: `KareYerlesimTests.OlcuUzunEtiketiYakalar` hucreye sigmayan bir
etiket verir ve olcunun **kirmizi** dondugunu tutar. Iki testten biri olmadan digeri bir
sey soylemiyor.

**Tur 2 duzeltmesi - 1040 px artik tekrarlanabilir.** Tur 1'de dar olcu dosya elle
degistirilip bir kez kosulmustu; iddia kosuya bagli degildi. Pencere olcusu parametreye
cevrildi (`KareYerlesimTests.IkiDilIkiOlcu`), olcu dort kosumda kosuyor:
tr/en x 1600x1000 / 1040x720. Dordu de yesil.

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

Etiketlerdeki `TextWrapping="Wrap"` arayuzde yerinde birakildi. Olculen bir kusuru
kapatmiyor - daha uzun bir cevirinin ileride tasmasina karsi onlem; boyle isaretlendi ki
sonradan "bu bir duzeltmeydi" diye okunmasin. Sarma yalniz **olcunun** icinde, o da tek
bir yerlesim kosumu icin kapatiliyor.

**Yukaridaki px tablosu tur 1 kosumundan geliyor** (`.calisma/T192/infogrid-*.txt`,
sarma acikken). Tur 2'de sayilar yeniden dokulmedi; yeniden kosulan sey **kosulun
kendisi**, dort kosumda da tasma yok. Tablodaki sayilarin sarma acik/kapali ayni olmasi
beklenir cunku hicbir etiket sarmiyor - ama bu bir cikarim, tur 2'de olculmedi.

## K7 - temizlik

T192 hicbir `//` satir yorumu eklemedi (`git diff origin/main...HEAD -- src tests |
grep '^\+\s*//'` bos). `MainWindow.axaml` ve `MainWindow.axaml.cs` BOM tasiyor ama bu
BOM `origin/main`den geliyor, T192 getirmedi; ayni dosyalar main'de de ayni sekilde
duruyor, silmek bu sozlesmenin isi degil. `owns` disina yazilmadi.

## Kalan

**K5 - kareler yenilenmedi, T0'a birakildi.** `tools/VidShrink.Shot` ve
`docs/gorseller/**` bu sozlesmenin `owns` listesinde degil; T189 ayri bir dalda ve
henuz birlesmedi. Duzeltmeler yazildi, kareler tazelenmedi.

**Olcemedigim.** Yukaridaki sayilarin hepsi basiz yerlesimden geliyor. Duzeltilmis
arayuzun gercek bir ekran goruntusune bakilmadi; onu K5 ile birlikte T0 gorecek.

Tur 2'de olcemediklerim, tek tek:

- Kalite siniri uyarisinin (`main.quality.below-floor` / `above-ceiling`) ekrandaki hali.
  Kultur gecidine baglandi, gecit olculu, satirin kendisi degil.
- `InfoGrid` px tablosunun sarma kapaliyken yeniden dokulmesi. Kosul dort kosumda yesil,
  ama tablodaki 112,6 / 140 px sayilari tur 1 kosumundan.
- Ayar kutularinin (`TxtTarget`, `TxtQualityTarget`, `TxtQuality`) kulture cevrilmesi.
  Yazma ve okuma birlikte cevrilmeli; ayri bir is olarak birakildi, gerekcesi 1. maddede.

## Tam kosu

`dotnet build -c Release`: 0 uyari, 0 hata.

### Kirmizi/yesil tabani - ne uzerinde kosuldu (T192 tur 2 duzeltmesi)

Tur 1'de "duzeltmelerden **once**" diye etiketlenen kosu `origin/main` uzerinde
degildi; WIP'in kismen geri alinmis hali uzerinde kosmustu. Etiket yanlisti.

**`origin/main` bir taban olamaz** ve bu bir tercih degil, bir olgu: T192'nin olculeri
(`BiciminTests`, `KareYerlesimTests`, `BaslikKapsamiTests`) `main`de **yok**, ustelik
`MainWindow.Num`/`Percent` orada `private`. Depoyu `main`e alip bu olculeri kosmak
derlenmiyor bile:

```
error CS0117: 'MainWindow' bir 'Num' tanimi icermiyor
```

Dogru taban su: **olculer T192'de kalir, davranis `main`e geri alinir.** Tur 2'de bu
kosuldu ve tekrarlanabilir:

```
git checkout origin/main -- src/VidShrink.App/LanguageCatalog.cs src/VidShrink.App/MainWindow.axaml src/VidShrink.App/MainWindow.axaml.cs
```

sonra `MainWindow.axaml.cs`'teki `private static string Num` satiri `internal` yapilir ve
yanina eski yuzde davranisi eklenir (eski kodda `Percent` diye bir gecit yoktu, isaret
elle sola yaziliyordu):

```csharp
internal static string Percent(double ratio) => "%" + (ratio * 100).ToString("0.#");
```

| Kosu | Toplam | Gecti | Kaldi | Ham cikti |
|---|---|---|---|---|
| T192 olculeri, davranis `main`de | 35 | 16 | **19** | `.calisma/T192/k10-kirmizi-taban.txt` |
| T192 olculeri, davranis T192'de | 35 | **35** | 0 | ayni kosu, yesil |

Kirmizi donen 19'un icinde K9'un iki yeni olcusu de var
(`PaylasimTavaniDileUyar(tr)`, `DescribeBytes` gecidi)
(`KaynakBilgiKareHiziDileUyar(tr)` kirmizi, `(en)` yesil - kusur tam olarak buydu).
`KaynakBilgiEtiketleriKendiHucresindeKalir` iki tabanda da **yesil**: orada bir kusur
yoktu, madde 5'te yazdigi gibi.

### Tam suit

| Tam kosu | Toplam | Gecti | Kaldi | Atlandi | Ham cikti |
|---|---|---|---|---|---|
| Tur 1 sonu | 1949 | 1930 | 1 | 18 | `.calisma/T192/test-son.txt` |
| Tur 2 sonu | 1960 | 1941 | 1 | 18 | `.calisma/T192/test-son-tur2.txt` |
| Denetcinin kosusu (`4c8246d`) | 1960 | 1939 | 2 | 19 | `.calisma/T192-denetim-tur2/tam-kosu-benim.txt` |
| Tur 3 sonu | 1961 | 1942 | 1 | 18 | `.calisma/T192-tur3/test-son-tur3.txt` - kirpilmamis |
| **Tur 4 sonu** | **1961** | **1942** | **1** | **18** | `.calisma/T192-tur4/test-tam-tur4.txt` - **kirpilmamis** |

Tur 4'un kosusu `dotnet test -c Release`, 21 dk 52 sn surdu; tek kirmizi yine
`OynaticiBoruTests_DecoderPipe.Oldurulemeyen_surec_icin_KillTree_basarisiz_bildirir`
(adi, `Win32Exception: Erisim engellendi` iletisi ve yigin izi dosyanin 44-56
satirlarinda). Tur 4'un dokundugu iki dosya da test dosyasi; toplam degismedi.

Tur 3'un kosusu `dotnet test -c Release --logger "console;verbosity=normal"`, ciktisi 2042
satir ve **tail'lenmedi**; basarisiz testin adi, hata iletisi ve yigin izi dosyanin 237-247
satirlarinda duruyor. Toplam 1961; tur 2'den bir fazla olmasinin sebebi K11'in yeni olcusu
(`AdVeBirimYazimiCumleOrtasindaDaKorunur`).

**Bu sayilar makineye ve o andaki yuke bagli.** Denetci ayni `a843711` uzerinde
1928/3 aldi; fazladan iki kirmizi ortam kaynakliydi (tek baslarina kosunca yesil,
gecici klasorde dosya kilidi). Ayni makinede baska ajanlarin ffmpeg kodlamasi kosarken
bu kume buyuyebilir. Sayiya bakarken kosul sudur: **T192'nin dokundugu hicbir olcu
kirmizi degil**; K6 pimleri ve T192 olculeri ayri ayri kosuldugunda 55/55 ve 35/35
yesil (`.calisma/T192/k10-yesil-tur2.txt`).

Tur 3'te bu kosul yeniden olculdu: K6 pimleri + T192'nin dokundugu metin olculeri birlikte
(`WindowLayoutTests`, `AyarYuzeyiTests`, `QualityTargetUiTests`, `CasingTests`, `ChipTests`,
`LanguageTests`, `BiciminTests`, `KareYerlesimTests`) **201/201 yesil** -
`.calisma/T192-tur3/k6-pimler.txt`.

Kapanan alti kirmizinin hepsi ayni sebepten degildi; ikisi yerlesim kusuru degil,
olcunun kendi kusuruydu:

| Kirmizi olcu | Neden kirmiziydi | Nasil kapandi |
|---|---|---|
| `KatlanmisBolumOzetiSatirinIcindeKalir` (3 ozet) | gercek kusur, madde 3 | satir `Grid`e cevrildi |
| `TuretmeSatiriHedefKutusunuIzler` | gercek kusur, madde 4 | erken donus kaldirildi |
| `KaynakBilgiEtiketleriKendiHucresindeKalir` (2 dil) | **olcu yarim kalmisti**: sinav `.Where(pair => true)` idi, yani her hucreyi tasmis sayiyordu | gercek tasma kosulu yazildi; madde 5'te goruldugu gibi ortada tasma yok |

Tur 2'nin tam kosusunda da kirmizi sayisi 1 idi ama **adi okunamiyordu**: cikti gunluk
kuyruguyla kaydedilmis, basarisiz satiri kirpilmisti. Tur 3'te bu borc kapandi - kosu
kirpilmadan yazildi ve **iki kirmizinin da adi artik olculmus**, cikarim degil:

| Kirmizi | Tur 3 kosusunda | Neden | T192 ile ilgisi |
|---|---|---|---|
| `OynaticiBoruTests_DecoderPipe.Oldurulemeyen_surec_icin_KillTree_basarisiz_bildirir` | **kirmizi** (`test-son-tur3.txt:237-247`) | `Win32Exception: Erisim engellendi` - test Windows'un `System` surecini (PID 4) aliyor, yukseltilmemis oturumda `HasExited` firlatiyor | **yok** |
| `PanelHostTests.Parca_hazir_olunca_sag_yari_parcayi_gosterir` | **yesil** | denetcinin kosusunda kirmiziydi, tek basina yesil; yuk kaynakli | **yok** |

Ikisi de T192'nin dokunmadigi yollarda; `git diff origin/main...HEAD` o dosyalarda bos.
Tur 2'nin "cikarim, olcum degil" kaydi dogru cikti, ama artik cikarim degil.

**`OynaticiBoruTests_DecoderPipe.Oldurulemeyen_surec_icin_KillTree_basarisiz_bildirir`
- T192 disi, duzeltilmedi.** Test Windows'un `System` surecini (PID 4) aliyor ve
`HasExited` okuyor; yukseltilmemis oturumda `Win32Exception: Erisim engellendi`
firlatiyor. Bu oturum yukseltilmemis - `WindowsPrincipal.IsInRole(Administrator)`
`False` donuyor (`.calisma/T192/yukseltilmis-mi.txt`). Ne test dosyasina ne
`DecoderPipe`'a T192 dokundu; `git diff origin/main...HEAD` o yollarda bos. Test
dosyasina en son T182 dokunmus (`9bb92ba`, `main` uzerinde). Makine ve izin kosuluna
bagli, ayri bir is.

**K6 pimleri** (`WindowLayoutTests`, `AyarYuzeyiTests`, `QualityTargetUiTests`): 55/55
yesil, **bu uc sinifta** hicbir pim yeniden temellendirilmedi - ham cikti
`.calisma/T192/k6-pimler.txt` ve tur 2 icin `.calisma/T192/k6-pimler-tur2.txt`.

Cumle yalniz bu uc sinif icin gecerlidir; genele yayilmaz. **Suitin baska yerinde yedi
beklenen deger yeniden temellendirildi** (`CasingTests` 5, `ChipTests` 1,
`LanguageTests` 1); tek tek gerekcesi 2. maddedeki "Testlerde yeniden temellendirilen
yedi beklenen deger" tablosunda.

Tur 3'te bunlara **bir sekizincisi** eklendi ve o bir beklenen deger degil bir kural:
`LanguageTests.DilDosyasindakiButunGovdeCumleleriOlduguGibiKalir`. Gerekcesi ayni
bolumun "Tur 3'te yeniden temellendirilen sekizinci olcu" alt basliginda; K6'nin uc
sinifina yine dokunulmadi.
