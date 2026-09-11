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
