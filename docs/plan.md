# Yol haritasi — 0.3.1 arayuz turu

Kaynak: kullanicinin son 30 girdisi (`.calisma/son-girdiler.md`), 7 Eylul 2026 mesaji,
ve iki danisman raporu:
[008 UI tasarimci](danisma/008-ui-tasarimci.md) · [009 UI denetcisi](danisma/009-ui-denetci.md).

Denetcinin karari **HOLD**. Iki danisman bagimsiz olarak ayni seyi soyledi: arayuz
sikistirmanin ne yapacagini dugmeye basmadan once soylemiyor, ve yerlesim pimi
testleri bu isle birlikte yeniden temellendirilmeli.

## Sira ve durum

| # | Is | Sozlesme | Durum |
|---|---|---|---|
| F1 | Yerlesim testi ikili arama (132,16 sn → 7,76 sn) | T183 | **muhurlendi** |
| A1 | Onizleme sesi — `AttachAudioSink` baglanmis degil | T184 | **muhurlendi** |
| A2 | Tekerlek zoom %100→200 olu, zoom'da kararma | T184 | **muhurlendi** |
| A3 | Rozet: sol ORIJINAL / sag ISLENMIS · CRF x, ustte | T184 | **muhurlendi** |
| A4 | Duraklat/devam basa sariyor | T184 | **muhurlendi** |
| B | Oynatici sekmesi en sola tasinir (sekme ve kisayollar zaten var) | T185 | acilacak |
| D | Ayar arayuzu yeniden tasarimi (danismanlarin asil isi) | T186 | acilacak |
| E | Tasma teklifi — %3, dort secenek, sayfa ici serit | T187 | acilacak |
| C | Varsayilan program onerisi + sag menu kisayolu | T188 | acilacak |
| F2 | Yerlesim pimlerinin yeniden temellendirilmesi | D ile ayni dalda | acilacak |
| F3 | README ekran goruntuleri (T177 sonrasi) | — | acilacak |

## A — Onizleme (T184)

Kullanici bunu uc ayri turda soyledi; en gorunur eksik bu.

**A1 ses.** Ses yolu `VidShrink.Ffmpeg` icinde bastan sona yazilmis — `AudioSink.cs`
NAudio ile 48 kHz/16 bit/stereo bir cikis kuruyor, `DecoderPipe.SeekAudio` ffmpeg'den
ayri bir PCM borusu aciyor. Kullanicinin tarif ettigi "ses dosyasini eszamanli calsan
bile cozulur" yaklasimi zaten kodlanmis.

> **Duzeltme (7 Eylul, T184 yapicisinin bulgusu).** Bu bolumun ilk hali "VidShrink.App
> hicbir yerde AttachAudioSink cagirmiyor, grep bos donuyor" diyordu. **Yanlisti.**
> `PlayerView.axaml.cs:292` zaten cagiriyordu. Gercek bosluk oynaticida degil
> **karsilastirma panelindeydi**: `PipeComparisonFrameSource` yalniz ham video tasiyor,
> ses hic gecmiyordu. Cozum kaynak dosyaya ayri bir `DecoderPipe` acip `AudioSink`'i
> ona takmak oldu (`PreviewAudio.cs`).

**A2 zoom.** Tekerlek olayi %100 ile %200 arasinda bir sey yapmiyor; ayrica zoom
girip cikarken kare kararip geliyor. Ikisi ayni sozlesmede.

**A3 rozet.** `playback.approximate-preview` ("Yaklasik onizleme") kalkiyor. Yerine
orta panelin **solunda ustte** `ORIJINAL`, **saginda ustte** `ISLENMIS · CRF <x>`.
Rozetler perde hareket ederken sabit durur, ortulen tarafin etiketi soner.
Denetci bunu bagimsiz olarak dogruladi: bugunku rozet "bir ozur cumlesi".

**A4 basa sarma.** 4 Eylul'den kalma borc: ayar degismeden durdur/baslat yapilinca
onizleme en bastan isleme giriyor.

## B — Oynatici sekmesi (T185)

> **Duzeltme (7 Eylul).** Bu bolumun ilk hali "sekme bugun hic yok, sifirdan acilacak"
> diyordu. **Yanlisti.** Sekme `MainWindow.axaml:1253` icinde `TabPlayer` adiyla duruyor,
> icinde `PlayerView` mounted. Kisayollar da calisiyor: T176 dokuz girdinin dokuzunu
> olcup pinledi (`docs/olcumler/oynatici-girdi.md`) ve "birlikte ac" ile acilan dosya
> zaten Oynatici sekmesini seciyor (`startup-tab=5|header=Oynatici` izi).

Geriye kalan **tek** is sira: sekme bugun **en sagda** (indeks 5), kullanici **en solda**
istiyor. Acilis varsayilani **Kucult** kalmali — yani sekme en solda durur ama secili
gelmez. Tasarimcinin uyarisi tam burada: serit basinda durup varsayilan olmayan bir sekme
celiskili okunuyor; cozum bir ayirici ve ikonla Oynatici'yi **mod** gibi okutmak.

Sirasi degisince `startup-tab=5` izi ve ona bagli pimler yeniden temellendirilecek.

Tasarimcinin uyarisi: sekme seridinde en solda durup varsayilan olmayan bir sekme
celiskili okunuyor. Cozum sekme sirasini degistirmek degil, Oynatici'yi bir ayirici ve
ikonla **mod** gibi okutmak (tasarimcinin A plani). Kullanici "en solda olsun" dedigi
icin B plani (serit disi ayri mod dugmesi) uygulanmiyor.

## D — Ayar arayuzu (T186)

Tasarimcinin dort bolgesi: A baglam (birakma alani + tek satira saran bilgi seridi,
bugunku dokuz satir yerine), B **Hedef** (birincil: sayi kutusu + Doldurma modu ona
yapisik bir operator + cip seridi), C **Kalite ve uyumluluk** (kodek, HDR yalniz HDR
kaynakta, izinler, GPU), D **Gelismis** kapali.

Bilesen kurali: adlandirilmis durum secimi → **segment kontrolu** (Kodek 3, HDR 2,
Doldurma 2); gercek ac/kapa → **anahtar** (Izinler, GPU). Iki ve uc secenek icin ayni
bilesen. Etiket ustte, tam genislikte segment seridi altta — cunku Turkce etiketler
sariyor. En fazla uc segment.

**Amac kontrol olmaktan cikiyor**, cip seridinin basinda `( Arsiv ) ( Paylasim )`
hazir ayarina donuyor. Motorda karsiligi duruyor ve olculebilir:
`CompressionStrategy.cs:91` Arsiv icin -6.0, Paylasim icin -3.0 CRF ofseti suruyor.
Yani secim gercekten plani degistiriyor; bugun eksik olan bu etkinin ekranda hic
gorunmemesi.

Denetcinin tek onerisi ayni yeri gosteriyor: Kucult'un ilk okumasi **plan + onizleme**
olsun, ayarlar etrafina dizilsin, ayar degisince ikisi de canli guncellensin.

Izinler iki bagimsiz onay kutusu degil, bir **feda sirasi** olarak sunulur.
Bilgi satirlari **once → sonra** bicimine gecer.

## E — Tasma teklifi (T187)

Esik **%3**. Asla sessiz yapilmaz. Dort secenek: tekrar render, iptal, buyuk
surumu kabul, ve sondan / bastan / her ikisinden ~%3 kesintiyi kabul.

Tasarimcinin bicimi: kalici pencere degil, sonuc kartinin icinde **sayfa ici serit**.
Bir birincil oneri + iki guvenli secenek + kapali bir yikici grup. Ayirt edici bes
katman: konum, belirtec, somut sonuc metni ("4:12 → 4:05, sondan ~7 sn"), klavye
varsayilani **asla** yikici secenek olmaz, ve yerinde iki adimli onay. Kesim once
zaman cizgisinde onizlenir.

## C — Kabuk entegrasyonu (T188)

**Olculdu, kok neden bulundu** — `docs/olcumler/kabuk-entegrasyonu.md` (T188 tur 1).

Uzantinin kendisi **saglam**: derleniyor, kayit oluyor, paketli COM uzerinden aktive
oluyor (`CoCreateInstance` `0x00000000`) ve basligini donduruyor. Calismayan sey
**teslimat**: kurulu agacta `shell/` klasoru hic yok, kurulum betigi de dosya yoksa
sessizce donuyor. Sebep `release.yml` icindeki `map(select(.path == "VidShrink.exe"))`
suzgeci — `shell/` otomatik guncellemeye hic girmiyor.

Uretim yolu ayrica **imza istiyor**: imzasiz `.msix` kurulumu `0x800B0100` ile
reddediliyor. Bu makinede calisan `-Register` yolu yalniz gelistirici kipi acik oldugu
icin acildi.

Varsayilan program tarafinda ProgID ve `OpenWithProgids` kaydi yazildi (24 medya
uzantisi, hepsi HKCU); `UserChoice`'a dokunulmadi, dokunulamaz.

### C borclari

1. **Imzalama.** Uretimde sag menu icin `.msix` imzalanmali. Imzasiz kurulum
   `0x800B0100` ile reddediliyor.
2. **Teslimat.** `release.yml` suzgeci `shell/` klasorunu otomatik guncellemeden
   disari birakiyor.
3. **Gelistirici kipi kapali makine.** Olcemedim.
4. **Explorer'da gorsel dogrulama.** Olcemedim.
5. **Oneri seridi olu kod.** `DefaultAppSuggestionBar` yazildi ama hicbir yerden
   cagrilmiyor; bagli olacagi `MainWindow.axaml.cs` T188'in `owns` listesi disindaydi.
   T188 tur 2'de baglanir — `owns` genisletildi.
6. **ProgID kaydi da olu.** Denetcinin buldugu, K4'ten daha buyuk delik:
   `FileAssociation.Register` uretim kodunda hicbir yerden cagrilmiyor. Tur 1'in canli
   `HKCU` kaydi yalnizca olcumun yansima ile elle tetiklemesiyle olustu; gercek kullanici
   uygulamayi kurup calistirdiginda "Birlikte ac" listesinde hala gorunmez. Ayni kok
   neden: `owns` bagla noktasini kapsamiyordu. Tur 2 K8'de baglanir.

## F2 — Pim yeniden temellendirmesi

Iki danisman da bagimsiz uyardi: D isi yerlesim pimi testlerini kirmizya dondurur.
Bu bir surpriz degil, isin parcasi. Pimler D ile **ayni dalda** yeniden temellendirilir;
ayri sozlesmeye birakilirsa main iki kosum kirmizi kalir — bu depoda daha once oldu.

## Raftakiler

- **Test paralelligi (B secenegi)** — asagida.
- **Test paralelligi (B secenegi)** — kullanici 0.3.0 sonrasina erteledi. Onunde
  dokuz olculmemis yerel `Call from invalid thread` hatasi duruyor.


## Max sikistirma modu — acilis (7 Eylul 2026)

Kullanici 6 Eylul'de rafa kaldirmis ve hatirlatilmasini istemisti; "yavastan giriselim"
dedigi is budur. Once **ne oldugunu degil, motorun bugun nerede durdugunu** yazdim,
cunku rafa kalkan tarifin dayandigi varsayimlarin bir kismi bu arada **olculup elendi**.

### Elenmis olan: sahne basina bit dagitimi

T114 bunu olctu ve **koda girmemesine** karar verdi (`docs/olcumler/sahne-butcesi.md`,
"Sonuc"). Uc bagimsiz olcu ayni yone bakiyor:

- K2 — olculen 8 hucrenin 5'inde kodlayicinin kendi dagitimi haritanin onerisi kadar
  ya da ondan daha dogru.
- K5/K6 — kalite kapisi **gecmedi**: p10 esigini gecen hucre 0, en kotu sahne esigini
  gecen 0.
- K7 — harita kasten bozuldugunda sonuc **kotulesmedi**; en iyi bozuk kol dogru haritayi
  0,050 puan gecti, dogru haritanin tabana kazanci ise +0,007 puandi.

Ustune: dagitimi tasiyan tek parametre `zones` ve denenen bes kodlayicinin yalniz
ikisinde (`libx265`, `libx264`) calisiyor. **Uretimin varsayilani `libsvtav1` onu sessizce
yok sayiyor.** Yani bu yoldan gidilen max modu, kullanicinin varsayilan ayarina hic
dokunmazdi.

**Sonuc: max modu sahne butcesi uzerine kurulmaz.** Rafa kaldirilan tarifin bes
kalibrasyon stratejisinden ucu (sabit 10/80/10, uzatilmis ilk %10, her %10'da bir) zaten
bugunku motorun gerisinde — `CalibrationProbe.Windows` pencere sayisini kaynagin
heterojenligine gore seciyor ve `SceneMap` alabiliyor.

### Ayakta kalan tek olculmus isaret: `qcomp`

Ayni izgarada tabani gecen 4 hucrenin **3'unde kazanan `zones` degil `qcomp` oldu.
`qcomp` tek bir kuresel skaler; `SceneMap` istemiyor ve `libsvtav1`'de calisiyor.**
T114 raporunun kendi cumlesi: kazandigi hucre "iki gecis yanliliginin bugunku
varsayilaninin bu icerikte en iyi olmadiginin kaniti".

Yani elde, varsayilan kodlayicida gecerli, olculmus ve **hic pesine dusulmemis** bir
ayar var.

### Ikinci acik: `maks` kolunun kalite kapisi hic kosulmadi

Ayni raporun "Olculemeyenler" tablosu: `maks/p1-karisik`, `maks/p2-durgun`,
`maks/p3-hareketli` icin K5/K6 hucreleri **kosulmadi** (`k5-*.json` yok). Duzenegin
`maks` kolu var ama max modunun kalite kazanci bu depoda **hic olculmedi**.

### Acilis adimi — tek sozlesme, yeni duzenek yok

`tools/sahne-butcesi/` zaten bu isi yapiyor; kurulacak bir sey yok, **kosulacak** bir sey
var. Tek sozlesme iki soruyu kapatir:

1. `maks` kolunun K5/K6 hucrelerini kos — max modunun kalite kazanci ilk kez sayiya doner.
2. `libsvtav1` uzerinde `qcomp` taramasi — T114'un tek hucrelik isareti gercek mi, hangi
   deger, ve varsayilan yolda gorunuyor mu.

ffmpeg **sirayla** kosar; bu depoda iki es zamanli kodlama sure ve kalite sayilarini
bozdu. Duzenek `--no-build` kullanmaz.

**Fiyat.** Model tarafi ucuz — bir sozlesme, bir denetim, ~60-90k token. Pahali olan
duvar saati: kodlama kosumlari **4-8 saat**, makine bu sure boyunca baska olcum
kosturamaz. Onceki 400-700k'lik tahmin **butun modun** fiyatiydi; bu adim onun onunde
duran ve sonrakini gereksiz kilabilecek olan olcum.

Bu adim bitmeden mod tasarlanmaz. Cikan sayi "kazanc yok" derse mod **acilmaz** ve bu da
bir sonuctur.

**7 Eylul 2026 — ikinci kez ertelendi.** Kullanicinin cumlesi: "bekle 4-8 saat olmaz gece
calismani istemiyorum sonra musait zamanda bakalim hatirlatta". Olcum gunduz musait bir
saatte kosulur ve **hatirlatmayi T0 yapar**, kullanici sormaz. Hazirlik bitti; kosulacak
sey `tools/sahne-butcesi/`, yeni duzenek yok.

### Acilis olcumu kosuldu — T193 (8 Eylul 2026)

Rapor: `docs/olcumler/max-mod-acilis.md` (sayfanin butun sayilari `.calisma/T193/`
altindaki ham dosyalardan `tools/sahne-butcesi/09-max-mod-raporu.py` ile uretilir).

**Hukum: max sikistirma modu acilmaz.**

Iki acik da kapandi:

1. **`maks` kolunun kalite kapisi ilk kez kosuldu.** T114'te kol sessizce atlaniyordu
   (kapi `ZonesFlag` uzerindeydi, `libsvtav1` icin `null` donuyordu); kapi
   `ParamsFlag`e cevrildi ve uc pencerede de kosuldu. p10 esigini gecen pencere
   **0/3**, en kotu sahne esigini gecen pencere **0/3**. Kazanclar sifirin altinda,
   mutlak degerlerin en buyugu 0,021 puan; esik +0,50.

2. **`qcomp` `libsvtav1`'de yok — iki ad alaninda da.** ffmpeg anahtari
   ayristiramiyor (`Error parsing option qcomp`, ham dosya
   `.calisma/T193/k2kapi/qcomp-a.p1.err`) ama cikis kodu 0 donuyor. Fark
   `-svtav1-params qcomp=...` yolunda **4.312 bayt**, ffmpeg'in kendi `-qcomp`
   secenegiyle **22.686 bayt**; ikisi de destek esiginin (tekrar gurultusu x 2 =
   **66.614 bayt**) altinda. Bu yuzden deger taramasi (`0,40 / 0,50 / 0,60 / 0,75`)
   **kosulmadi**; kosulsa olculecek bir sey olmayacakti.

Ayakta kalan isaretin nereden geldigi de anlasildi: T114 izgarasinin
`libsvtav1 / qcomp` satiri `qcomp` degil **`qp-scale-compress-strength`** deniyordu,
ve K4 ekinde "qcomp kazandi" denen hucreler `libx264` / `libx265` kollarindaydi.
Yani "varsayilan kodlayicida gecerli, olculmus bir ayar" hic olmadi.

Bunun tersi de olculdu: SVT-AV1'in kendi karsiligi `qp-scale-compress-strength`
**calisiyor** (175.735 bayt fark, gurultunun cok ustunde; stderr'inde ayristirma
hatasi yok, SVT'nin kendi dokumu `QP scale compress strength` degerini basiyor).

Kalite kazanci da olculdu (T193 tur 2, raporun K12 bolumu) ve sonuc **belirsiz**:
`=3` kolu `p1-karisik`te 61,50 MB ile hedef bandin (58,3-60,0 MB) disina cikti, yani
iki kol esit boyda karsilastirilmadi. `p3-hareketli`de iki kol da band icinde ama p10
kazanci +0,471 — esik +0,50 — ve en kotu sahne kazanci -0,248. Bu anahtar icin mod ne
acilir ne kapanir; onunde duran soru kalite degil hedef boyuttur.

Yan bulgu, duzenegin kendisinde: T114 tekrar gurultusunu **tek kontrol ciftinden**
oluyordu. Ayni cift bu kosumda 456 bayt verdi; dort kosumun araligi 13.423 bayt,
**sekiz** kosumunki 33.307 bayt. Tek cift olculen gurultuyu 73 kat, dort kosum 2,5 kat
kucuk gosteriyor. Gurultu artik sekiz kosumdan olculur (`08-svtav1-kapisi.sh`,
`KONTROL=8`); kosum 5-8 denetcinin bagimsiz kosumudur.

**Sirada ne var.** Sahne butcesi yolu kapandi, `qcomp` yolu kapandi.
`qp-scale-compress-strength` icin kalite olculdu ama hukum **belirsiz** kaldi: once o
anahtarin ciktisini hedef banda oturtan bir plan gerekiyor (iki kolu esit boyda
karsilastirmak icin), sonra kalite sorusu tekrar sorulabilir. Ondan once mod
tasarlanmaz. Karar kullanicinin.

**Kosum sapmasi.** T114'un 17 dakikalik kaynagi bu makinede yok; pencereler elde olan
uc 60 sn'lik parcadan uretildi (~189 sn yerine ~60 sn, plan cozunurlugu `806x454`
yerine `1920x1080`). Bu kosumun sayilari T114'un hucreleriyle dogrudan
karsilastirilamaz; taban bu kosumda yeniden olculdu. Ayrinti raporun K0 bolumunde.

## Oynatici — GOM paritesi (11 Eylul 2026)

Kullanicinin cumlesi: "gom ve benzeri playerleri incele tüm özelliklerini istiyorum
playerimde". Siniflandirma ve sira fable'in karari: `docs/netlestirme/004-...ra.md`.
Envanterler: `docs/oynatici/rakip-envanter.md`, `docs/oynatici/mevcut-envanter.md`.

**Siniflandirma:** Standart 30 (denetim cubugu + sag tik ust bolumu), Gelismis 13
(sag tik en altta "Gelismis" alt menusu + Ayarlar > Oynatici > Gelismis katlanir grubu),
Alinmayacak 6 (AB atlama, 360°, DVD, TTS, altyazi indirme, bitince kapat).

**Mimari:** oynatici sekmesi libmpv'ye gecer, yazilim render (`MPV_RENDER_API_TYPE_SW`)
BGRA tamponu bugunku WriteableBitmap yoluna verir. ffmpeg pakette kalir; Kucult,
Donustur ve karsilastirma paneli dokunulmadan. Lisans: VidShrink AGPL-3.0, GPL libmpv
derlemesi uyumlu. Tedarik ffmpeg gibi: yayin arsivine girmez, kurucu indirir
(release.yml basligi), CI indirir ve sha256 dogrular; ikili yoksa olcu **kirmizi**.

**Kisayollar:** GOM varsayilanlari; teker ses, Ctrl/Shift/Ctrl+Shift+teker arama
10/60/300 sn, Alt+teker zoom, orta ve cift tik tam ekran. Tek `Keymap` tablosu; menu,
ayarlar sayfasi ve test ayni tablodan okur.

| # | Dalga | Kapsam | Kabul |
|---|---|---|---|
| 0 | Cekirdek | `VidShrink.Player`: P/Invoke, `IPlaybackEngine`, SW render → PlayerView, bugunku parite | Mevcut oynatici testleri yeni motorla yesil; arama ≤150 ms; avsync 10 sn sonra ≤40 ms; basliksiz kare cozulur |
| 1 | Gunluk denetim | Ses/sessiz, hiz, kare kare, atlama, A-B, kaldigi yerden devam, yer imi, tam klavye/fare | 2× hizda 2 sn → 4 sn ±%5; kare adimi 1/fps ±%10; devam ±1 sn; her kisayol bir komuta bagli |
| 2 | Altyazi, ses parcasi | Dis/gomulu altyazi, gecikme, boyut/konum, kodlama; aid, ses gecikmesi | 2 ses + 1 altyazili uretilmis dosyada gecis; gecikmeler yazilip okunur; cp1254 srt duzgun |
| 3 | Goruntu, pencere, liste | En-boy, dondur/aynala, her zaman ustte, bilgi paneli, ekran goruntusu, bolum/yer imi cubugu, son acilanlar, klasorde sonraki, surukle-birak, karistir/tekrar | Ekran goruntusu kaynak cozunurlugunde; PgDn siradaki dosya; son acilanlar 10 ve kalici |
| 4 | Gelismis | Renk ayarlari, keskinlik/deinterlace, ekolayzer, normallestirme, %200, kirpma, kucuk resim, klip/GIF, mini mod, URL, altyazi bicemi | Her ayar yazilir/okunur/sifirlanir; kucuk resim ≤300 ms |
| 5 | Ortak cekirdek | Karsilastirma paneli ve PreviewAudio motora; DecoderPipe/NAudio → trash | Iki ornek arasi fark ≤1 kare; eski yola canli basvuru yok |
| 6 | Sistem | Dosya iliskilendirme, tek ornek | Cift tiklanan dosya acik pencereye iletilir |

Her dalga: kendi dali, 43 dil ayni dalgada, `dotnet test` tam yesil, `gh run list` yesil.

**Sira (11 Eylul 2026, 0. dalga main'de 476b5d24):** 1, 5 ve 6 paralel; dosya alanlari ayri,
motor arayuzune yalniz ekleme. 2, 3 ve 4 oynatici yuzeyini paylastigi icin 1 birlestikten
sonra sirayla; 2 ve 3'ten ayrisani paralel acilir.

### Motor secimi dogrulandi (netlestirme 005-007)

On aday puanlandi (`docs/oynatici/kutuphane-karsilastirma.md`, karar
`docs/netlestirme/007-...`): libmpv + kendi P/Invoke 115/140, LibVLCSharp 95,
HanumanInstitute 91, boru genisletme 88 (Gelismis 13'un ~5'i erisilmez), FFmpeg.AutoGen 80.
Kendi yazim tahmini: boru ~22 tur, AutoGen ~36 tur; libmpv ~14 tur. Karar 004 korunur.

**0. dalga, adim 1 — olcum:** libmpv SW render, basiz, 1080p60 ve 4K30. Esik: 1080p ≥55
kare/s, 4K ≥24 kare/s. 4K tutmazsa GPU yolu (OpenGlControlBase + ANGLE) 4. dalgaya
istege bagli kalem. 1080p tutmazsa ikili tasarim: arayuzde OpenGL/ANGLE, basizda SW, 0.
dalgaya +1 tur. ANGLE Avalonia 11'de baglanamazsa LibVLCSharp `libvlc_video_set_callbacks`
yolu, 0. dalga bastan. GPL hazir ikiliyle baslanir; `-Dgpl=false` LGPL derlemesi 6. dalga
sonrasi istege bagli.

**Duzeltme:** fable'a verilen olgularda T167'nin LibVLC olcumu yoktu
(`docs/olcumler/oynatici-hatti.md`: bellek geri cagrisi yolu bu depoda calisti, arama
medyani 38,9-57,2 ms, senkron 2,1 ms, kurulum +106-293 MB). Fable'in agirliklariyla
LibVLC 95 → ~109; libmpv 115 onde ama fark olculmemis iki sayida (libmpv SW hizi, acilmis
DLL boyutu). 0. dalga adim 1 bu iki sayiyi LibVLC'nin olculmus degerleriyle kiyaslar:
libmpv arama medyani ≤60 ms ve kurulum deltasi <106 MB tutmazsa LibVLC geri cagri yolu.

**Olcum sonucu (pilot 2, netlestirme 011):** `docs/olcumler/libmpv-sw-render.md`. SW render
esigin 9-28 kati, timed oynatmada 0 dusme, islemci %3-8. Arama medyani H.264 1080p 36 ms,
HEVC 1080p 58-62 ms (sinirda), 2160p 78-195 ms. Kurulum: `libmpv-2.dll` acilmis 120 MB,
zip 48 MB; 007'deki "~34 MB" indirilen 7z'nin boyutuydu. Karar: **libmpv kalir, ek olcum
yok.** Boyut kolu berabere (LibVLC'nin 106 MB'i dogrulanmamis, dogrulanmis 3 mimari 293 MB);
4K arama kaybi kod cozucu/GOP siniri, motor degistirmek cevirmez. Kurallar:
- arama esigi ayrilir: 1080p medyan ≤60 ms; 2160p `exact` medyani ≤ tam GOP cozum suresi
  (bu duzenekte ≤200 ms);
- surukleme sirasinda `seek absolute+keyframes`, birakinca `exact`;
- varsayilan `hwdec=no`, `auto-copy` secenek;
- HEVC 1080p arama 0. dalganin kendi olcumunde ayni aracla tekrar alinir;
- kirpilmis libmpv derlemesi v1 sonrasi;
- osx-arm64 gomme kapisi (010) surer.

**Avalonia 12 gecisi tamam (pilot 1):** main'de 12.1.2. 8 derleme hatasi / 4 dosya, basiz
kurulum `UseHarfBuzz`, 35 test kirmizisi kok nedenden duzeltildi (beklenti gevsetilmedi),
4 RID publish cikti, CI yesil. Motor onerisine etkisi yok: oynatici yolu SW render →
WriteableBitmap, surumden bagimsiz; Avalonia'ya bagli iki oynatici paketi kullanilmiyor.

### Avalonia 12, v1 kapsami, tahmin yontemi (netlestirme 008-010)

Kanit: `docs/netlestirme/010-...`; olgular kirilma analizi ve gecmis tahmin isabeti.

**Avalonia 12:** paket guncellemesi, bastan insa degil (code-behind, `{Binding}` 0; resmi
kirilma listesinin depo karsiligi 1 dosya/1 satir + dogrulanamayan 3 madde). Tahmin 1
sozlesme, P50 2 tur, P90 4 tur. Sonda: dal `t0/avalonia-12`, 4 projede 12.1.2, derleme hata
sayisi, tam suit, 4 RID publish, Windows'ta ac + bir dosya kucult. Kabul: hata ≤15 dosya,
yeni kirmizi 0, 4 RID publish cikar, ≤3 tur. Tutmazsa 11.3.20'de kalinir, oynatici yuzeyi
bitmap tabanli (surumden bagimsiz) kurulur, gecis v1 sonrasina.

**Gelecek major'lar:** her biri ≤1 sozlesme butcesi. Yeni major'in .1.x'i cikinca ve oynatici
kutuphanesi destekleyince gecilir, .0'a gecilmez. Kacinilacak: `internal`/`Unstable` API,
`OpenGlControlBase`/`ICustomDrawOperation` dogrudan arayuzde (gerekirse tek adaptor dosyasi),
Fluent ic stil anahtarlari, reflection tabanli Binding.

**v1 kesiti:** win-x64 Standart 30 + Gelismis 13 tam; osx-arm64 Standart 30 + motordan bagimsiz
Gelismisler; osx-x64 ve linux-x64 derlenir, "deneysel" etiketiyle, sinanmaz. ~19-20 tur / 8-12
gun. Hepsi 4 RID'de sinanmis senaryosu ~24-30 tur / 12-20 gun. Ertelenen: macOS Gelismis 13,
linux/osx-x64 sinanmis destek (v1.1-1.2); macOS CI testi v1'den hemen sonraki ilk is.
0. dalga adim 1'e eklenir: osx-arm64 bundle'a libmpv gomme 1 turda calismali; calismazsa tek
motor kurali ile her platformda LibVLC.

**Tahmin yontemi:** 007'nin "1 tur = 3 gun" birimi gecersiz; olculen 0,28-0,64 gun/tur,
standart 0,5 (P50) / 0,7 (P90). Tur tahminleri ×1,5 (P50) / ×3 (P90) okunur. Pilot 1 Avalonia
12 sondasi, pilot 2 0. dalga libmpv olcumu. Her sozlesme acilista tahmin tur/gun/token yazar,
kapanista gercegi `docs/olcumler/tahmin-isabet.md`'ye girer; katsayi iki pilottan sonra,
sonra her 5 sozlesmede yenilenir.

**Kendi kutuphane tetigi** (biri olculup yazilinca acilir): libmpv ve LibVLC ikisi de SW
esigini tutmaz ve HW yolu 1 sozlesmede kapanmaz; v1 zorunlu ozelliklerden ≥2'si iki
kutuphanede de yapilamaz; ikisi de win-x64 veya osx-arm64'te 2 turda paketlenemez; AGPL
uyumsuzlugu ya da 12 ay commit'siz kutuphane; kurulum >300 MB ve kullanici sikayeti. Acilirsa
once 1 sozlesmelik boru pilotu, karar kullanicinin.

## Baslik duzeni (11 Eylul 2026, dal t0/baslik-duzeni)

Kullanici istegi: is penceresinde sistem basligi yok; ust tuslarin anahati durgunken
soluk gri, fare ustundeyken NeonBlueBorderStrong; koseler RadiusChip (6, tek yaricap);
butun baslik tuslari ayni yukseklikte (TargetMinSize); Ayarlar sekmesi baslik
seridinde dil tuslarinin sagina, sponsor tusunun soluna tasinir.

- `Theme.axaml`: `HeaderRestBorder` = TextDisabledColor, border-decorative alfasi (0.3);
  `HeaderButtonPadding` 12,0; `TabMargin` alta BorderThin (sekmeler cizginin ustunde ortalanir).
- `Controls.axaml`: `HeaderButton` temasi; LanguageButton, TitleBarSupportButton,
  TitleBarLinkButton ve yeni `TitleBarTabButton` ondan turer; NeonTabItem ayni kurala gecer.
- `MainWindow.axaml(.cs)`: `BtnSettings` ekle, `TabSettings` basligini gizle, secili sinifini izle.
- `ShrinkJobWindow.axaml(.cs)`: BorderOnly + istemci alani genisletme, govdeden surukleme.
- Olcu: WindowLayout, VisibleText, SettingsTab, Language, OynaticiGirdi, KabukIstegi testleri;
  once/sonra kareleri `.calisma/baslik/`.

## 2. dalga: altyazi ve ses parcasi

Kabul: iki ses ve bir gomulu altyazili klipte `aid`/`sid` motor uzerinden degisir ve geri
okunur; `sub-delay`/`audio-delay` yazilip okunur; cp1254 Turkce .srt bozulmadan gorunur,
yanlis kod sayfasi bozar (negatif kontrol).

- `Player/IPlaybackEngine.cs`: yalniz varsayilan govdeli ekler (`Tracks`, `AudioTrack`,
  `SubtitleTrack`, gecikmeler, `SubtitleScale/Position/Codepage`, `AddSubtitle`).
  `MpvEngine`: `track-list`, `current-tracks/*/id`, `sub-add ... select` (senkron),
  kod sayfasi degisince dis altyazi `sub-reload`.
- `App/Playback/SubtitleOptions.cs`: gecikme, boyut, konum, kod sayfasi durumu ve parca
  dongusu; dosya acilinca motora yeniden yazilir.
- `App/Playback/PlayerView.Tracks.cs` (partial): A/S dongusu, gecikme komutlari, dosya
  secici, oynaticiya birakilan .srt/.ass/.ssa/.vtt (pencereye gecmez), sag tik ve dugme
  menulerindeki parca listeleri. `TrackButtons.axaml`: baslik seridinde Altyazi ve Ses.
- `Keymap`: A, S, `>`/`<` altyazi gecikmesi ±0,5 sn, Ctrl+./Ctrl+, ses gecikmesi ±0,1 sn.
- Dil: yeni alan dosyasi `Locales/<dil>/tracks.json` (`player.tracks.*`,
  `player.subtitle.*`), 43 dil; 3. dalgayla `main.json` cakismasin diye ayri dosya.
- Olcu: `OynaticiParcaTests`, `KeymapTests` (yeni komutlarin iz onekleri ve etkileri),
  `BiciminTests` pinleri; kanit `.calisma/dalga2/`.

## 6. dalga: sistem (dosya iliskilendirme, tek ornek)

Kabul: cift tiklanan dosya acik pencereye iletilir; iki gercek surecle kanitlanir.

- `Core/SingleInstanceChannel.cs`: adli mutex + adli boru (kullanici basina kanal,
  `VIDSHRINK_INSTANCE_CHANNEL` ile degisir). Ikinci surec yolu JSON satiri olarak yollar,
  TAMAM gelirse 0 ile cikar; HATA ya da zaman asimi gelirse kendi penceresini acar.
- `Program.cs`: `Main` int doner; `--kucult` kolu degismez. `Integration/ForwardedFiles.cs`
  pencere hazir olana kadar yolu bekletir, sonra UI is parcacigina verir.
  `MainWindow.SingleInstance.cs`: kodlama suruyorsa (`_cts`) reddeder, yoksa pencereyi
  one getirip `LoadStartupFileAsync` yolundan yukler. `PlayerView`e dokunulmaz.
- macOS: `IActivatableLifetime.Activated` + `FileActivatedEventArgs` ayni kuyruga duser;
  `macos-app-bundle.sh` `CFBundleDocumentTypes` yazar (Viewer, Alternate: varsayilani almaz).
- Linux: `install-vidshrink.sh` `~/.local/share/applications/vidshrink.desktop` yazar
  (`Exec=... %F`, 24 uzantinin MIME turleri, `.dav` icin kullanici MIME paketi);
  `--uninstall` ikisini de siler.
- Windows: `Install-VidShrink.ps1` HKCU'ya ProgID, `OpenWithProgids`, `Applications`,
  `Capabilities` + `RegisteredApplications` yazar; `-RemoveFileAssociation` ve `-Uninstall`
  yalniz kendi degerini siler. Sag tik menusu (`SystemFileAssociations`) ayri agacta kalir.
  ProgID komutu baslaticiyi gosterir (`FileAssociation.LaunchTarget`). `UserChoice` yazilmaz.
- Olcu: `TekOrnekTests`, `DosyaIliskiTests`; negatif kontroller `.calisma/dalga6/`.
- Unix kapama: `install-vidshrink.sh`'daki `write_desktop_entry` artik `exec_argument_escape`
  ile Desktop Entry Exec kacisini uyguluyor (ters bolu/ters tirnak/dolar/cift tirnak + `%%`);
  ozel karakterli yol testi `DosyaIliskiTests`e, negatif kontrol `.calisma/dalga6-unix/`e.
- `MainWindow.SingleInstance.cs`: macOS kolunda kodlama surerken artik reddetmiyor, yolu
  bekletip kodlama bitince (`FlushPendingMacFile`) `main.instance.waiting` durumuyla yukluyor
  (40 dil + en); test `IsMacOSPlatformForTest` ile ayni kuyruk cagrisindan geciyor, negatif
  kontrol `.calisma/dalga6-unix/`e.

## 3. dalga: goruntu, pencere, liste

Kabul: ekran goruntusu kaynak cozunurlugunde (ffprobe, pencere boyutlu yakalama negatif
kontrol); bilgi paneli motordan dort alan; PgDn ad sirasinda sonraki, tekrar kapaliyken
sonda durur; son acilanlar 10 ve kalici; dondur/aynala motor ozelligine yazilip okunur.

- `IPlaybackEngine`: yalniz varsayilan govdeli ekleme — `Rotation`, `Mirrored`,
  `AspectOverride`, `RepeatFile`, `Details` (`MediaDetails`), `ChapterTimes`, setter'lar,
  `SaveScreenshotAsync`. `MpvEngine`: `video-rotate`, `vf @vsmirror:hflip`,
  `video-aspect-override`, `loop-file`, `screenshot-to-file <yol> video`, `track-list`.
  `dwidth/dheight` donmeyi icermez; 90/270'te render tamponu en/boy degistirir. Ayna
  `vf remove @vsmirror` ile kalkar (etiket `@` ister).
- JSON `Utf8JsonWriter` ile yazilir: uygulamada yansimali serilestirme kapali,
  `JsonNode.ToJsonString` calisma aninda atar.
- `App/Playback`: `PlayerSettings` (goruntu klasoru/adi, tekrar, karistir;
  `player-settings.json`), `RecentFiles` (10, `player-recent.json`), `FolderNavigator`
  (dogal ad sirasi, tohumlu karistirma), `SeekMarks`; ikisi de gecmis dosyasinin klasorunde,
  gecmis yolu yoksa yazilmaz. Mantik `PlayerView.Window.cs`'te; ana dosyada tek satirlik kancalar.
- `Keymap`: Ctrl+F5 oran, Ctrl+Shift+S dondur, Ctrl+H aynala, Ctrl+A ustte, Ctrl+F1 bilgi,
  Ctrl+E goruntu, PgUp/PgDn dosya, Ctrl+Alt+Shift+F karistir, Ctrl+Alt+Shift+B tekrar.
  Menude isaretli satirlar, son dosyalar alt menusu, goruntu klasoru secimi.
- Arayuz: zaman cubugu (tik ile arama, bolum ve yer imi isaretleri), bilgi rozeti,
  durum satiri. Anahtarlar `player.view.*`, `player.list.*`, `player.info.*`, 43 dil.
- Olcu: `OynaticiGorunumTests`, `KeymapTests`; kanit `.calisma/dalga3/`.

## 4a. dalga: gelismis goruntu ve ses

4. dalganin A yarisi. B yarisi (kucuk resim, klip/GIF, mini mod, URL) ayri dalda paralel
kosuyor; ortak dosyalara (`Keymap`, `PlayerView.axaml.cs`, `IPlaybackEngine`, `MpvEngine`,
dil dosyalari) iki taraf da yalniz **ekleme** yapar, birlestirmeyi T0 yapar.

Kapsam: renk (parlaklik, karsitlik, doygunluk, gama, ton), keskinlik, deinterlace, kirpma,
on bant ekolayzer, ses normallestirme, %200 ses tavani, altyazi bicemi (yazi tipi, renk,
anahat, golge, arka plan).

Kabul: her ayar motora yazilir, **motordan** geri okunur ve sifirlanir; negatif kontrol
kareden gelir — renk ayari altinda kare baytlari referanstan farklidir, sifirlamadan sonra
referansla ayni olur. Ayarlar 3. dalganin ayar dosyasi duzeniyle kalicidir.

- `Player/AdvancedSettings.cs`: `PictureAdjust`, `SoundAdjust`, `SubtitleStyle` kayitlari.
  Arayuz olcusu -100..100; suzgec birimine cevirme yalniz `MpvEngine` icinde.
- `IPlaybackEngine`: yalniz varsayilan govdeli ekler — `Picture`, `Sound`, `SubtitleLook`,
  `VolumeCeiling`, `SetPicture`, `SetSound`, `SetSubtitleStyle`.
- `Player/MpvEngine.Advanced.cs`: etiketli libavfilter halkalari (`@vscolor:eq`, `@vshue:hue`,
  `@vssharp:unsharp`, `@vscrop:crop`, `@vseq:lavfi=[equalizer..]`, `@vsnorm:lavfi=[dynaudnorm]`),
  `deinterlace`, `volume-max`, altyazi bicemi ozellikleri; sifirlama `option-info/<ad>/default-value`.
  SW render gpu-only renk ozelliklerini yok saydigi icin renk suzgecten gecer.
- `App/Playback/PlayerAdvanced.cs` + `player-advanced.json`: kalicilik, `PlayerSettings` duzeni.
  `PlayerView.Advanced.cs` (partial): sag tik menusunun **sonuna** "Gelismis" alt menusu
  (`AppendAdvancedMenu`), acilista motora yeniden yazma.
- Arayuz: Ayarlar > Oynatici sayfasinda katlanir "Gelismis" kumesi (`PlayerAdvancedPanel`),
  bicim var olan kumelerden kopyalanir; renk paletten, olcu `Theme.axaml` belirteclerinden.
- Dil: yeni alan dosyasi `Locales/<dil>/advanced.json` (`player.advanced.*`), 43 dil;
  B yarisiyla cakismasin diye ayri dosya. `BaslikKapsamiTests` pinleri yeniden olculur.
- Olcu: `OynaticiGelismisTests`; sag tik menu beklentileri `OynaticiDenetimTests` ve
  `OynaticiGirdiTests`'te guncellenir. Kanit `.calisma/dalga4a/`.

**Acilis tahmini** (yontem: tur ×1,5 P50 / ×3 P90, 0,5 gun/tur P50, 0,7 gun/tur P90):
cekirdek is 5 tur; P50 8 tur / 4 gun / 3 itme, P90 15 tur / 10,5 gun / 8 itme.
Dosya: ~13 kod ve belge dosyasi + 42 dil dosyasi = ~55.

## 4b. dalga: araclar (kucuk resim, klip/GIF, mini mod, URL)

4. dalganin B yarisi. A yarisi (renk, keskinlik, ekolayzer, %200, kirpma, altyazi bicemi)
ayri dalda ve sag tik menusunun en altina "Gelismis" alt menusu koyuyor; bu yari menuye
"Araclar" alt menusunu ekler. Ortak dosyalarda (Keymap, PlayerView.axaml(.cs),
IPlaybackEngine, MpvEngine, dil dosyalari) yalniz ekleme yapilir, birlestirmeyi T0 yapar.

Kabul: zaman cubugunda farenin durdugu anin karesi cubugun ustunde gorunur ve **kucuk
resim ≤300 ms** (medyan ve p95 olculur, kanit dosyasina yazilir); A-B isaretlerinden ya da
bulunulan konumdan kesilen klip ile GIF'in suresi ve boyutu ffprobe ile dogrulanir; mini
mod cercevesiz + hep ustte kucuk pencereye gecer ve cikista onceki boyut/konum aynen geri
gelir; http/https/rtsp adresi acilir; her durum yazilir, okunur, sifirlanir.

- `Player/IPlaybackEngine.cs`: yalniz varsayilan govdeli ekleme. `MpvEngine`: `loadfile`
  hedefi artik kosulsuz `Path.GetFullPath` degil — sema tanindiginda (http, https, rtsp,
  rtmp, srt, udp) adres oldugu gibi verilir, yerel yol eskisi gibi tam yola cevrilir.
  Kucuk resim ikinci bir motor ornegi: `Audio=false`, kucuk `RenderWidth/Height`, arama
  `SeekPrecision.Keyframe`. Karsilastirma paneli ve onizleme sesi ornekleri degismez.
- `App/Playback/PlayerView.Tools.cs` (partial): kucuk resim onizlemesi (fare zaman
  cubugunda gezerken), klip ve GIF cikarma, mini mod gecisi, URL ac penceresi ve
  `AppendToolsMenu(flyout)`. `App/Playback/ToolsOptions.cs`: mini mod boyu, klip suresi,
  GIF kare hizi/genisligi ve son adres; kendi dosyasinda (`player-tools.json`) durur,
  `PlayerSettings`e dokunulmaz — A yarisiyla ayni dosyaya yazmamak icin.
- `App/Playback/ClipExport.cs`: `VidShrink.Ffmpeg`in surec deseni (`FfmpegRunner.RunAsync`,
  stdout **ve** stderr ayni anda bosaltilir; bosalmayan boru surece kilit atar). Klip
  akis kopyasi (`-c copy`), GIF iki gecisli palet (`palettegen`/`paletteuse`).
- `Keymap`: Araclar satirlari tek tabloya eklenir (GOM varsayilanlarina yakin); menu,
  ayarlar sayfasi ve test yine ayni tablodan okur.
- Dil: yeni alan dosyasi `Locales/<dil>/tools.json` (`player.tools.*`), 43 dil; A yarisiyla
  ayni dosyada bulusmamak icin ayri alan dosyasi, 2. dalganin `tracks.json` karariyla ayni.
  `BiciminTests` pinleri (kalem sayisi ve kol) testi kosarak yeniden olculur.

Olculen (`worktree-agent-aab0e51a79169b7f8`, `baae77f9`, kanit `.calisma/dalga4b/`):
kucuk resim medyan **10.35 ms**, p95 **10.43 ms** (10 olcum, esik 300 ms); gosterimin
kendisi elle surulen sahte motorla ayrica olculur, o kol CI'da da kosar. Klip 3 sn
istendiginde akis kopyasi anahtar kareye hizalar ve 4.011 sn verir (kaynak `-g 60` @30 fps,
2 sn anahtar kare araligi) — kabul araligi bu hizalamaya gore. GIF 2.0 sn, 160x90.
Mini mod 900x600/`Full` → 480x270/`None`+ustte → 900x600/`Full`. Dil sayimi
43 x 592 = 25456, kol 967 (en 103, tr 43). Katalogda kalan ama kod yolunda karsiligi
olmayan uc anahtar (`state-mini`, `preview`, `length`) 42 dilden silindi;
`novideo` kaynaksiz klip/GIF isteginde durum satirinda gosteriliyor —
`LocalizationTests.KatalogdaBirikenOluCeviriListesiBuyumuyor` olu anahtar biriktirmiyor.
- Arayuz: onizleme yongasi `Surface` uzerinde, rengi paletten, olcusu `Theme.axaml`
  belirtecinden turetilmis `PlaybackThumbnail*` adlariyla; yeni renk/olcu uydurulmaz.
- Olcu: `OynaticiAracTests` — kucuk resim medyan/p95 (esik `[HedefMakineFact]`, gosterimin
  kendisi CI'da da kosar), yanlis konumun karesi farkli (negatif kontrol), klip/GIF sure ve
  boyut, gecersiz A-B araligi dosya uretmez, mini moddan cikista pencere olculeri birebir,
  mini moda girmeden cikis bir sey yapmaz, testin icindeki `HttpListener`dan URL acilir,
  kapali kapiya istek hata verir. Menu beklentileri `OynaticiGirdiTests` ve
  `OynaticiDenetimTests`te guncellenir. Kanit ve gecici cikti `.calisma/dalga4b/`.

**Acilis tahmini (12 Eylul 2026):** taban 6 tur; yontem geregi ×1,5 / ×3 → **P50 9 tur,
P90 18 tur**; 0,5 (P50) / 0,7 (P90) gun/tur ile **P50 ~4,5 gun, P90 ~12,6 gun**. Itme
P50 5, P90 12. Dokunulan dosya ~55: kaynak 8, tema 2, dil 43, test 3, belge 3. Gercegi
kapanista `docs/olcumler/tahmin-isabet.md`ye girer.

## 7. dalga: oynatici etkileşimi (12 Eylul 2026)

Kullanicinin istegi: ekranin neredeyse tamami oynaticiya; mp4'e cift tik sonrasi hicbir
gecikme olmadan oynatma; sol tik duraklat/baslat; sol basili tutarak pencere tasima
(maksimizede video tasinir, ortaya yaklasinca otomatik hizalanir); sag tik menu/ayarlar;
altta kolay erisilen oynat, -10 sn, +10 sn, ses, hiz; gecen ve kalan sure; genis cubuga
tiklayinca aninda o ana gitme; cubukta suruklerken akici kucuk resim onizlemesi.

Uc kol, dosya sahipligi ayrik:

### 7a. Fare: sol tik, tasima, sag tik menu

Sahip dosyalar: `Playback/PlayerView.axaml.cs`, yeni `Playback/PlayerView.Fare.cs`,
`Playback/Keymap.cs`, `Playback/PlayerInputMap.cs`.

- Sol tik (ClickCount 1) duraklat/baslat; cift tik tam ekran kalir, ikisi cakismaz.
- Sol basili tutup surukleme: pencere kipinde `BeginMoveDrag`, maksimize/tam ekran kipinde
  video yuzeyi kaydirilir (pan); merkez esigine girilince otomatik hizalanir.
- Sag tik menuyu **fare konumunda** acar (bugun sag ustteki `⋮` dugmesine tutturuluyor);
  menunun ilk satiri ayarlar sayfasini acar.
- Kabul: her giris icin oncesi/sonrasi olculur; tasima ile tiklama esigi ayrilir
  (surukleme baslamadan birakilan basis duraklat/baslat sayilir).

### 7b. Alt denetim seridi ve zaman cubugu

Sahip dosyalar: `Playback/PlayerView.axaml`, yeni `Playback/PlayerView.Serit.cs`,
`Playback/PlayerView.Window.cs` (zaman cubugu bolumu), `Themes/Playback.axaml`,
`Playback/PlayerView.Tools.cs` (kucuk resim baglama).

- Altta otomatik gizlenen serit: oynat/duraklat, -10 sn, +10 sn, ses, hiz. Olculer
  `Playback.axaml` belirteclerinden; yeni renk/olcu uydurulmaz.
- Sure etiketi `00:12 / 01:30` bicimi ve **kalan** sure; bugunku dort metin satiri kalkar,
  video dikeyde buyur.
- Genis zaman cubugu: basista aninda o ana gider (bugunku davranis korunur), suruklerken
  kucuk resim onizlemesi fareyi takip eder (4b altyapisi `ShowThumbnailAsync`, olculen
  medyan ~10 ms, esik 300 ms).
- Kabul: serit gosterme gecikmesi 0 / gizleme 360 ms olculur, ±10 sn motora ulasir, ses ve
  hiz motordan geri okunur, sure etiketi iki dilde bicimlenir.

### 7c. Acilis hizi: cift tikdan ilk kareye

Sahip dosyalar: `Program.cs`, `MainWindow.axaml.cs` (acilis yolu), `ShellIntegration`,
`docs/olcumler/acilis-hizi.md`.

- Uctan uca olcum yok; once olculur (cift tik → ilk kare), sonra yol kisaltilir: gereksiz
  bekleme, sekme gecisi sirasi, tek ornek kanali zaman asimlari, ilk kare gelmeden
  yapilan is.
- Kabul: olcum dosyasi medyan/p95 verir, iyilestirme oncesi ve sonrasi ayni makinede
  karsilastirilir; acilis sekmesi kabuk yolunda Oynatici kalir.

## 8. dalga: ekran kaydedici (12 Eylul 2026)

Kullanicinin istegi: oynatici isi bitince ekran kaydedici moduluna gecmek.

Envanter (ajan raporu, `docs/envanter/ekran-kaydedici.md`): depoda yakalama kodu **sifir**
— `gdigrab`, `x11grab`, `avfoundation`, `dshow`, `pipewire` icin `src/` altinda hic eslesme
yok; `Locales/en/performance.json:25` "VidShrink does not capture video" diye aciktan sinir
ciziyor, `QualityTargetTests.cs:51` `LongScreenCapture()` yalnizca bir girdi profili.
Mikrofon/sistem sesi yakalama da yok: `FfmpegArguments.cs:387` tek girdi kuruyor, NAudio
yalnizca `.sln` disindaki `tools/VidShrink.PlayerProbe`'ta ve
`OynaticiKarsilastirmaTests.cs:448,493` canli NAudio basvurusunu kirmiziya cevirmek uzere
pimli.

Iki tasiyici bosluk, plani bunlar sekillendiriyor:

1. **Nazik durdurma yolu yok.** Her ffmpeg cagrisi "baslat, bitmesini bekle" kalibinda;
   iptal `TryKill` (`FfmpegRunner.cs:78-81`), yani surec oldurulur. Argumanlarin nerdeyse
   tumu `-nostdin` (`ClipExport.cs:58,67`, `SegmentEncoder.cs:175`, `FrameGrabber.cs:259`,
   `ComplexityProbe.cs:878`). Oldurulen bir kayit yarim mux birakir. Tutunacak tek yer
   `FfmpegRunner.cs:97` `RedirectStandardInput = true` — acik ama kullanilmiyor.
2. **Suresi bilinmeyen iste ilerleme okunamiyor.** `EncodeRunner.RunCommandAsync` kesri
   `durationSeconds`'a boluyor (`EncodeRunner.cs:391`); canli kayitta sure yok, yeni bir
   ilerleme kipi gerekiyor.

Uc kol, dosya sahipligi ayrik:

### 8a. Yakalama motoru: baslat, nazik durdur, canli ilerleme

Sahip dosyalar: yeni `src/VidShrink.Core/RecorderArguments.cs`, yeni
`src/VidShrink.Ffmpeg/RecorderSession.cs`, `src/VidShrink.Ffmpeg/FfmpegRunner.cs` (stdin kolu).

- Argumanlar `Core`da uretilir, `Ffmpeg` yalnizca kosturur — deponun kurulu ayrimi
  (`AGENTS.md`) bozulmaz. Platform basina girdi: Windows `gdigrab`, macOS `avfoundation`,
  Linux `x11grab`/`pipewire`; ekran, pencere ve bolge secimi ayni arguman ureticisinden.
- `RecorderSession`: `StartAsync` / `PauseAsync` / `StopAsync`. Durdurma **stdin'e `q`**
  yazar (`-nostdin` bu kolda verilmez), surec kendi mux'unu kapatir; `TryKill` yalnizca
  zaman asiminda ve o zaman dosya "yarim" isaretlenir.
- Ilerleme: `-progress pipe:1 -nostats` okunur ama kesir uretilmez; gecen sure,
  yazilan boyut ve dusen kare sayisi raporlanir (`EncodeProgress` yerine yeni
  `RecordProgress`).
- Kabul: 5 sn'lik gercek kayit alinir, `ffprobe` ile suresi ve akislari okunur; `q` ile
  durdurulan dosya oynatilabilir, `TryKill` ile durdurulan negatif kontrolde bozuk cikar.

### 8b. Arayuz: Kaydedici sekmesi ve serit

Sahip dosyalar: `src/VidShrink.App/MainWindow.axaml` (yeni `TabItem`),
`MainWindow.axaml.cs` (indeks alani), yeni `src/VidShrink.App/Recorder/RecorderView.axaml(.cs)`,
yeni `Themes/Recorder.axaml`, `Locales/*/recorder.json`.

- Duzen `Playback/` desenini birebir izler: gorunum + konuya bolunmus kismi siniflar,
  ayar kaliciligi `PlayerSettings.cs` ornegi, girdi `Keymap.cs` ornegi.
- `Themes/Recorder.axaml` **yeni sayi uretmez**: her belirtec `Theme.axaml`'daki bir
  belirtecten turetilir ve turetme dosya basindaki yorumda yazilir (`Themes/Playback.axaml:5-20`
  kalibi). Yukleme `App.axaml`'a, Theme.axaml'dan **sonra**.
- Sekme sirasi: Oynatici 0'da kalir (`WindowLayoutTests.cs:184,191` pimli), Kaydedici
  Gelismis'ten once eklenir; acilis sekmesi Kucultme kalir (`WindowLayoutTests.cs:202`).
- Dil: yeni `recorder` alani `LanguageTests.cs:30` ve `LocalizationTests.cs:171`
  listelerine eklenmezse `Locales.Read` alani hic gormez — iki liste de guncellenir.
  Anahtar bicimi `recorder.<grup>.<ad>` (`LocalizationTests.cs:119` regex).
- Kabul: sekme 42 dilde basliksiz kalmaz, `Recorder.axaml` belirtecleri Theme.axaml'dan
  turetilmis olarak okunur, serit dugmeleri motora ulasir.

### 8c. Ses girisi ve cihaz listesi

Sahip dosyalar: yeni `src/VidShrink.Ffmpeg/CaptureDevices.cs`,
`src/VidShrink.Core/RecorderArguments.cs` (ikinci girdi kolu).

- Cihaz listesi ffmpeg'in kendisinden: Windows `-list_devices true -f dshow -i dummy`,
  macOS `-f avfoundation -list_devices true -i ""`, Linux `pactl`/`pipewire`. Ayristirma
  `EncoderCapabilities.cs` kalibinda, sonuc onbelleklenir.
- Mikrofon ve sistem sesi ayri girdi; ikisi birlikte secilince `amix`. NAudio
  kullanilmaz — `OynaticiKarsilastirmaTests.cs:543` `Assert.Empty(naudio)` pimi duser.
- Kabul: listelenen her cihaz icin arguman uretilir, bilinmeyen cihaz adi sessizce
  yutulmaz (negatif kontrol), sesli kayitta `ffprobe` iki akis gorur.

**Acilis tahmini:** yazilacak (kol sahipleri atanirken). Gercegi kapanista
`docs/olcumler/tahmin-isabet.md`ye girer.
