# Anahtar kare tavani

T133. Soru: **kisa `-g` hangi kaynakta kazandiriyor? Kazandirmiyorsa harita kolu
neden var?**

Raporun sayi tasiyan her cumlesi `tools/anahtar-kare/rapor.py` tarafindan
izgara dosyalarindan uretildi. Elle yazilan bolumlerde sayi yoktur; sayi gecen
her yer ureticiden gelir ve yaninda hangi tablodan/kolondan alindigi yazar.

## Cevap

**Zorluk esitlenmis calisma noktasinda cevap ({{CEVAP_ZORLUK}}), es bit/piksel
calisma noktasinda cevap ({{CEVAP_BPP}}).**

Iki calisma noktasi da ayni izgarada, ayni kaynaklarda, ayni karar kuraliyla
degerlendirildi. Karar kurali K4'te olcum kosulmadan once yazildi ve
commit'lendi (`59011c6`, 2026-09-02T11:06:12+03:00); izgara commit'leri bundan
sonradir. Sonradan hangisinin "asil" nokta oldugunu secmedim: **ikisi de
raporlanir**, ayrilirlarsa ayrilik bulgunun kendisidir.

**Ikisinin ayrilmadigi yer, sozlesmenin sordugu yer.** Sozlesme "kisa `-g` hangi
kaynakta kazandiriyor" diye soruyor; **(a) hicbir calisma noktasinda cikmadi.**
Haritanin `-g` uzerindeki butun erisim alani `[5,0 ; 10,0]` ve o araligin kisa
ucu (5 sn) sekiz kaynak-nokta hucresinin yedisinde 10 sn'ye gore **kaybediyor**;
tek pozitif hucre `+0,109` p10 ile esigin (0,20) altinda kaliyor. Iki hucre
(`s2-durgun`in ikisi) boyut bandi disinda damgali, geri kalan alti hucre temiz.
Iki nokta yalnizca **ne kadar uzun** sorusunda ayriliyor:
es bit/piksel noktasi 10 sn'nin ustunu isaret ediyor (c), zorluk esitlenmis nokta
10 sn'nin ustunu de ayirt edemiyor (b). Ayni isaret bagimsiz olarak `libsvtav1`
ve `av1_nvenc` izgaralarinda da var.

Yani sozlesmenin **(b)** secenegi — "harita kolu `-g` uzerinde zarardir,
kaldirilir ya da tavan tek degere sabitlenir" — iki noktanin da destekledigi tek
sonuc; ayrilik yalnizca sabitlenecek degerin 10 sn mi daha uzun mu olacagi
uzerinde. K5'in recetesi bu ayrimi tasiyor: kolun kaldirilmasi kapisiz, tavanin
yukseltilmesi kapili.

{{OZET_ZORLUK}}

{{OZET_BPP}}

## K3 — haritanin kolu izgaranin neresine dusuyor

Bu tablo sozlesmenin cevabidir: bugunku harita her kaynakta hangi tavani
secerdi, o secim izgaranin neresine dusuyor.

{{K3_ZORLUK}}

{{K3_BPP}}

Haritanin secimini ureten hesap uretim koduyla ayni: `SceneDetector`in taramasi
(`select='gte(scene,0.012)'`), `ThresholdRule.Measured` ile turetilen esik,
`MinSceneSeconds = 1,0`, sonra sahne surelerinin medyani `SceneMapMergeFactor`e
bolunup `[5,0 ; 10,0]` araligina kisiliyor.

{{HARITA}}

**Uyari — mahalle genisligi klipten buyuk.** `ThresholdRule.Measured`in
`NeighbourhoodSeconds` degeri 40,0 sn, olcum klipleri 20 sn. Yani turetilen
esik bu kliplerde tek bir mahalleden hesaplaniyor; tam uzunluktaki bir kaynakta
esik yer yer farkli cikabilir. Bu, haritanin **sectigi tavani** kaydirabilir;
izgaranin kendisini etkilemez.

## Sozlesmenin oncululundeki hata

Sozlesmenin Baglam bolumu soyle diyor: "`-g 300` = 10,0 s @ 30 fps = tavanin
ust ucu". **Bu yanlis.** `auto-mod.md`nin olctugu kaynak 60 fps'tir; ayni
belge kendi tablosunda `-g 300` ciktisinin gerceklesen araligini **5,00 sn**
olarak sayiyor (`docs/olcumler/auto-mod.md`, "anahtar kare / en kisa aralik /
en uzun aralik" tablosu) ve baska bir yerde bunu acikca yaziyor: "T111'in
olctugu `-g 300` (60 fps'te 5 s) T98'in kelepcesinin **alt ucu**".

Sonuc, sozlesmenin kaygisini ters cevirir:

- Olculmus tek yonlu kazanc `-g 120` -> `-g 300`, yani **2,0 sn -> 5,0 sn**.
- 5,0 sn kelepcenin **alt** ucudur, ust ucu degil.
- Yani haritanin ulasabildigi butun aralik `[5,0 ; 10,0]`, olculmus kazancin
  **kazanan tarafinda** durur. Harita `-g`'yi olculmus optimumdan uzaklastiran
  bir kol degil; olculmus optimumun **ustunde** bir aralikta oynayan bir koldur.

Ayni yon `FfmpegArguments.cs`in kendi yazili taramasinda da var: 2 sn -> 5 sn
adimi p10'da +0,708, 5 sn -> 10 sn adimi +0,033, 10 sn -> 20 sn adimi +0,077.
Kazancin neredeyse tamami 2 sn ile 5 sn arasindadir.

Sozlesmenin dogru olan yari: **harita `-g`'yi yalnizca kisaltabilir.**
`Math.Clamp(medyan / SceneMapMergeFactor, 5,0 , 10,0)` ust siniri haritasiz
varsayilanla ayni oldugu icin harita hicbir kaynakta `-g`'yi uzatamaz. Bu
kodda dogrudan okunur.

## Yontem

### Dort kaynak

Hepsi paylasilan havuzdaki tek kaynaktan kesildi
(`.calisma/kaynak/kaynak-1080p60-hdr-17dk-yalniz-video.mkv`, 1920x1080@60 HDR PQ;
`s4` icin `parca-1.mkv`). Pencereler once tarandi: her 20 sn'lik pencerede
`vmafmotion` ortalamasi ve `gt(scene,0.105)` kesme sayisi olculdu
(`tools/anahtar-kare/tara-hareket.sh`), sinif ona gore secildi.

| kaynak | sinif | kaynak | yerlesim | hazirlik |
|---|---|---|---|---|
| `s1-kesikli` | kesik cok | 520-540 sn | 1280x720@30 | tonemap -> bt709, libx264 CRF 12 |
| `s2-durgun` | durgun | 360-380 sn | 1280x720@30 | tonemap -> bt709, libx264 CRF 12 |
| `s3-hareketli` | hareketli | 680-700 sn | 1280x720@30 | tonemap -> bt709, libx264 CRF 12 |
| `s4-yuksek` | yuksek cozunurluklu | `parca-1.mkv` ilk 20 sn | 1920x1080@60 HDR PQ | akis kopyasi, yeniden kodlama yok |

Uctu 720p30'a indirildi, dorduncusu kaynak duzeninde birakildi; cozunurluk ve
kare hizi ekseni boylece izgarada gercekten var.

### Hizalama kapisi

Izgara kosulmadan once her kaynak kayipsiz (`-qp 0`) kodlandi ve olcu zinciri
kendi uzerinde sinandi. Kapi: PSNR **sonsuz** olmali ve VMAF kare sayisi kaynak
kare sayisina **esit** olmali.

{{HIZALAMA}}

Bu kapi bosuna degil — **kurulmasaydi butun izgara yanlis olurdu.** Kilitsiz
zincirde (`settb=AVTB,setpts=N` yokken) ayni kayipsiz kodlama `s1-kesikli`de
ortalama 82,394 / p10 11,377 veriyordu; PSNR yine sonsuzdu, yani dosyalar
birebir ayniydi ve **puan tamamen olcu zincirinin kendi hatasiydi.** Kilit
`docs/olcumler/auto-mod.md`de T111'in kurdugu kilidin aynisi
(`tools/auto-mod-olcumu/t111-settb.sh`).

Ikinci ayrinti: bos muxer zincirin sonunda **son kareyi cogaltiyor**. Cogaltilan
kare VMAF gunlugune 601. satir olarak giriyor ve puani asagi cekiyor —
`s2-durgun` 9600k hucresinde p10 97,229 yerine 97,116, en kotu kare 97,208
yerine 95,295 okunuyordu. `-fps_mode passthrough` bunu kapatiyor. Izgaranin
tamami kapali halde kosuldu.

Kayipsiz kodlamada ortalamanin 100'e degil ~97-99'a oturmasi olcunun kendi
tavani: VMAF'in hareket ozniteligi durgun karelerde 0'a inince model 100 degil
~97,4 uretiyor. Bu tavan her hucrede ayni oldugu icin karsilastirmayi bozmaz,
ama **mutlak puanlar bu tavana gore okunmali.**

### Iki calisma noktasi

Ayni izgara iki bitrate rejiminde kosuldu. Ikisi de iki gecis, sabit `-b:v`,
`libx264 -preset medium -threads 8`.

- **es bit/piksel** — pinli taramanin calisma noktasi (20 MiB / 20 sn @
  1920x1080@60) piksel hizina bolunup her kaynaga tasindi. Kullanicinin
  gercekten aldigi rejime yakin olan budur.
- **zorluk esitlenmis** — her kaynak icin hedef ortalama, **o kaynagin kendi
  kayipsiz tavaninin 10,0 puan altidir**; bitrate kalibrasyon merdiveninden
  log-bitrate interpolasyonuyla secilir (`tools/anahtar-kare/kalibre-sec.py`).
  Boylece dort kaynak da olcunun ayirt edebildigi bolgede kosar.

Ikinci noktaya ihtiyac vardi: es bit/piksel noktasinda `s2-durgun` **doygun**.
Merdiven o kaynakta 2400k'dan sonra kipirdamiyor (ortalama 97,218 -> 97,247,
dosya 5,95 MB -> 6,04 MB) ve kayipsiz tavani 97,438. Doygun bir hucrede `-g`
hicbir sey yapamaz; bu bir bulgudur ama izgaranin o satirini karar icin
kullanilamaz hale getirir.

## K1 — izgara

Boyut esitligi kaynak icinde denetlenir: her hucrenin sapmasi **o kaynagin
kendi bes hucresinin ortalama boyutuna** gore hesaplanir, band **%0,5**.
Bandin disina cikan hucre atilmaz, `es boyut degil` damgasiyla tabloda kalir ve
"en iyi" secilemez.

{{K1_ZORLUK}}

{{K1_BPP}}

### Izgaranin kendi soyledigi iki sey

**Bir: I-kare yerlesimi bitrate'ten bagimsiz.** Ayni kaynagin ayni `-g`
hucresinde iki calisma noktasi **ayni I-kare sayisini ve ayni gerceklesen
araligi** veriyor (iki K1 tablosunun `I-kare` ve `gerc. aralik` kolonlarini yan
yana koy; yirmi hucrenin yirmisinde ayni). Anahtar kare dosyalarindan
dogrulandi: `bpp-s1-kesikli-g10` ile `zorluk-s1-kesikli-g10` ayni yedi zamani
tasiyor, bitrate 1864k ile 4250k arasinda degismesine ragmen. x264'un sahne
kesme karari kaynak icerigine bakiyor, bit butcesine degil. Bunun sonucu:
**K2'nin yapisal kolonu iki calisma noktasi icin de ayni**, ve iki nokta arasinda
gorulen butun puan farki bit butcesinden geliyor.

**Iki: cok dusuk bit butcesinde kisa `-g` felaket.** Zorluk esitlenmis noktada
`s2-durgun` 80 kbit/s'te kosuyor (kaynagin kayipsiz tavaninin 10 puan altini
tutturmak icin merdivenin sectigi deger). Orada `-g` 2 sn hucresi p10 **51,521**,
10 sn hucresi **82,238** — **-30,717 p10**, izgaradaki en buyuk tek etki ve
esigin 150 katindan buyuk. Sebep aritmetik: 20 sn'lik klipte 10 anahtar kare, ve
anahtar karelerin butun bit butcesini yemesi.

Bu hucre boyut bandi disinda (`-1,959%`) ve kural geregi "kazanan" secilemez,
ama **kaybeden olmasi damgadan etkilenmiyor**: hucre hem daha **kucuk** hem
belirgin daha **kotu**. Damganin koruyabilecegi tek sey fazladan bit alarak
kazanmak olurdu; burada tersi var. Ayni kaynagin es bit/piksel hucresinde
(1864 kbit/s) ayni fark yalnizca `-0,928` p10 — yani etki bit butcesi daraldikca
buyuyor.

Bu, uygulamanin gercek calisma bolgesini ilgilendiriyor: VidShrink hedef boyuta
sikistiran bir arac ve dar hedeflerde tam bu bolgede kosuyor. Kisa `-g` orada
yalniz "kazandirmiyor" degil, **agir zarar veriyor**.

## K2 — aramanin maliyeti

Iki olcu, ikisi de ayni 24 hedefte (tohum 1733, `[0,5 ; 19,5]` sn duzgun
dagilim):

- **yapisal mesafe** — hedeften geriye en yakin anahtar kareye kadarki mesafe.
  Dosyadan tam hesaplanir, saat gurultusu tasimaz. Oynaticinin hedefe varmak
  icin cozmek zorunda oldugu is budur.
- **net p50** — `ffmpeg -ss T -frames:v 1` sureleri p50'si eksi ayni hedeflerde
  `-c:v copy` sureleri p50'si. Surec baslatma maliyeti boyle ayiklanir. Tanim
  T113'un `harita-baglantisi.md`de kullandigi tanimla ayni; oradaki gurultu
  tabani 0,5-4,0 ms.

{{K2_BPP}}

{{K2_ZORLUK}}

### Tavanin yazili gerekcesi olculmus mu, varsayim mi

`FfmpegArguments.cs`de tavanin gerekcesi uc ayri iddiaya dayaniyor. Uc de ayni
sinifta degil:

1. **"5 s already carries 87% of the p10 gain between 2 s and 20 s"** —
   **olculmus.** Kaynak: ayni yorumdaki tarama tablosu. Tek kaynakta
   (`parca-1-20sn`), tek bitrate'te.
2. **"Above 10 s the ceiling stops binding at all - 10 s and 20 s produce the
   same three I-frames at the same places"** — **olculmus, ama tek kaynakta ve
   kosula bagli.** Bu bir icerik gozlemidir: o klipte 10 sn'lik tavan zaten
   baglamiyordu. Baska bir icerikte baglayabilir; bu izgara tam da bunu
   gosteriyor (I-kare kolonuna bak).
3. **"it agrees with HandBrake's keyint = 10*fps"** — **alinti, olcum degil.**
   Ayni deger bagimsiz olarak dogrulanmis olmaz; yalniz ayni degeri secen bir
   ikinci uygulama vardir.
4. **Atlama maliyeti** — **tavanin ucu icin olculmemis.** Ayni dosyada bir
   atlama sayisi var (`FfmpegArguments.cs:184-185`, `origin/main` `55f245a`: ortalama kuralinda 202,6 ms,
   medyan kuralinda 154,9 ms, "%24 less seek"), ama o sayi **baska bir secimi**
   temellendiriyor — tavanin ortalamadan mi medyandan mi okunacagini. Kiskacin
   ust ucunun neden 10,0 oldugunu anlatan paragrafta (`:249-252`) atlama gecmiyor;
   orada gerekce "5 s zaten kazancin %87'sini tasiyor" ve "10 s'in ustunde tavan
   artik baglamiyor". `HardwareKeyframeCeilingSeconds`in "the seek budget itself"
   cumlesinin (`:255-260`) altinda ise hic sayi yok.

   T113 atlamayi olctu ama sordugu soru bu degildi (haritali/haritasiz cift), ve o
   olcum bu sabitlerden **sonra** yapildi.

**Kisa cevap: tavanin kalite tarafi olculmus (tek kaynakta), ust ucunun atlama
tarafi varsayim.** Bu sozlesme atlama tarafini ilk kez dort kaynakta ve bes `-g`
degerinde olcuyor.

### Ayni olcunun iki kosumu

Yapisal kolon dosyadan hesaplandigi icin deterministik: iki kosumda hucre hucre
ayni cikiyor. `net p50` degil — bu paylasilan makine sayisidir ve tekrar
edilmiyor. Asagidaki tablo ayni hucrelerin iki bagimsiz kosumunu yan yana koyar;
`net p50 orani` kolonu 1,00'dan ne kadar saptigini gosterir.

**Es bit/piksel noktasi:**

{{TEKRAR_BPP}}

**Zorluk esitlenmis nokta:**

{{TEKRAR_ZORLUK}}

Bu yuzden **K2'nin karara giren kolonu `yapisal mesafe`dir**. `net p50`'nin
mutlak degeri rapor edilmiyor; isareti de yirmi hucrenin cogunda yapisal kolonla
ayni yone bakiyor ama iki kolda tersine donuyor (yukaridaki borc kalemi), yani
bagimsiz bir dogrulama olarak sayilmiyor.

## K5 — recete

Uretim degisikligi bu sozlesmede yapilmadi (`src` ve `tests` kapsam disi,
T108 ayni dosyalarda). Asagisi uygulama sozlesmesinin alacagi recetedir.
Recetedeki her sayi bu raporun izgarasindan gelir.

{{RECETE}}

## K6 — donanim kolu

Donanim kolu **kapsam disi birakilmadi, olculdu.** Gerekce: makinede
`av1_nvenc` var, kol tek sabitten ibaret (`HardwareKeyframeCeilingSeconds = 5,0`)
ve harita donanimda hic devreye girmiyor (`fromMap` yalniz `!hardware` iken
true), yani orada sorulacak tek soru "5,0 dogru yerde mi".

Kodlayici `av1_nvenc` preset p5 — projenin donanim olcumlerinin yapildigi
kodlayici ve preset (`FfmpegArguments.cs`, `HardwareBitrateYield` yorumu). Ayni
dort kaynak, ayni bes `-g` degeri, es bit/piksel calisma noktasi. Tek gecis;
donanim VBR boyutu yazilim kadar siki tutmuyor, bu yuzden bazi hucreler
`es boyut degil` damgasi aliyor ve damgali hucre "en iyi" secilemiyor.

{{NVENC}}

{{NVENC_CUMLE}}

`s2-durgun`da bes hucrenin besi de bandin disinda kaldigi icin o kaynak karara
girmiyor; kalan uc kaynakta bugunku 5,0 hicbirinde en iyi hucre degil.

Donanimda yorumun yazdigi mekanizma **dogrulandi**: gerceklesen aralik tavanin
tam kendisi (2 / 5 / 10 / 15 sn hucrelerinde `gerc. aralik` kolonu sirasiyla
2,000 / 5,000 / 10,000 / 15,000 sn, dort kaynakta da ayni; 20 sn hucresinde tek
I-kare kaldigi icin aralik diye bir sey yok). Yani sahne kesme yok ve tavan
yerlesim kuralinin tamami. Yazilim tarafinda ayni sey dogru
degil — orada gerceklesen aralik tavandan kisa cikiyor, cunku x264 kendi sahne
kesmesini calistiriyor.

**K6 karari: kapsam disi birakilmadi, olculdu, ve sabit yanlis yerde cikti.**
Degisiklik onerisi K5'in R3 kaleminde; uygulama T108 muhurlendikten sonra ayri
sozlesmede yapilir. Donanim kolunda tavani yukseltmenin atlama bedeli yazilim
tarafindaki gibi icerige bagli degil — gerceklesen aralik tavanin kendisi
oldugu icin bedel dogrudan ve tam olarak tavanla orantili. Kalite kazanci
olculdu, atlama bedeli aritmetik; ikisini tartan karar uygulama sozlesmesinindir.

## Ikinci kodlayici — isaret dogrulamasi

Ana izgara `libx264`. Olculmus kazancin geldigi belge (`auto-mod.md`) ise
`libsvtav1` preset 4 uzerinde calisiyordu. Isaretin kodlayiciya bagli olup
olmadigini gormek icin tek kaynakta ayni izgara `libsvtav1` preset 4 ile
tekrarlandi. Bu ana izgaranin karari degildir.

{{SVTAV1}}

{{SVTAV1_CUMLE}}

## Kalan borclar

- **Tek kaynak havuzu.** Dort kaynagin dordu de ayni 17 dakikalik ceviminden
  kesildi. Sinif farki gercek (kesme sayisi, hareket, cozunurluk), ama kodlayan
  kamera, kodek ve sahne dili ortak. Farkli bir cevimde isaretin ayni cikacagi
  **olculmedi.**
- **Yirmi saniyelik pencere.** Uzun kaynakta hem harita medyani hem atlama
  dagilimi degisir; `NeighbourhoodSeconds = 40,0` bu pencereden buyuk.
- **Tek preset.** `libx264 -preset medium`. Preset ile `-g` etkilesimi
  olculmedi.
- **`-g` 20 sn'nin ustu olculmedi.** Izgaranin ust ucu 20 sn.
- **Ses yok.** Butun kaynaklar ses akisi tasimiyor; teslim boyutu yalniz video.
- **Tek makine, tek kosum.** Her hucre bir kez kosuldu. Kalite ve boyut
  sayilari deterministik (sabit `-threads 8`, iki gecis), atlama sayilari
  degildir — atlama icin yapisal kolon deterministik olani, `net p50` olani
  degil. Ilk `net p50` kosumu paralel kodlamayla ayni anda alinmisti ve
  kullanilamaz cikti (ayni hucrede 861,0 ms'e karsi 140,0 ms, ve bir hucrede
  cozmenin kopyalamadan hizli gorunmesi); o kosum atildi, yerine tek basina
  alinan ikinci kosum kondu. Ikisi de `.calisma/t133/atlama-*-kosum*.txt`
  altinda duruyor.
- **`net p50` mutlak degeri tasinmaz.** Iki kosum arasindaki oran hucreye gore
  **0,29 ile 10,73 kat** arasinda degisti (yukaridaki tekrar tablolari,
  `net p50 orani` kolonu). Isaretin kendisi de tam tekrar etmiyor: sekiz
  kaynak-kosum cifti icin `-g 2`'den `-g 20`'ye giden uctan uca degisim on alti
  kolun **on ucunde** artiyor, `bpp-s3-hareketli` kosum 1'de (551,5 -> 454,9
  ms) ve `zorluk-s2-durgun` kosum 1'de (109,5 -> 69,2 ms) **azaliyor**,
  `zorluk-s1-kesikli` kosum 2'de basladigi yere donuyor (45,8 -> 45,8 ms).
- **`s2-durgun` iki noktada da karar disinda.** Es bit/piksel noktasinda bes
  hucrenin besi boyut bandinin disinda ve p10 zaten 95,9-97,1 arasinda doygun;
  zorluk esitlenmis noktada bes hucrenin dordu bandin disinda (yalniz 10 sn
  temiz). Karar veren izgara bu yuzden dort degil, uc kaynak tasiyor.
- **Ikinci kodlayici tek kaynakta.** `libsvtav1` yalniz `s1-kesikli` uzerinde
  kosuldu; isaret dogrulamasidir, izgara degil.

## Duzenek

| dosya | is |
|---|---|
| `tools/anahtar-kare/tara-hareket.sh` | pencere taramasi: hareket ve kesme sayisi |
| `tools/anahtar-kare/hazirla.sh` | dort kaynagi kurar |
| `tools/anahtar-kare/hizalama.sh` | kayipsiz kapi: PSNR sonsuz + kare sayisi |
| `tools/anahtar-kare/ortak.sh` | ortak kodlama/olcum parcalari (tek yerden) |
| `tools/anahtar-kare/kalibre.sh`, `kalibre-s4.sh` | bitrate merdiveni |
| `tools/anahtar-kare/kalibre-sec.py` | zorluk esitlenmis bitrate secimi |
| `tools/anahtar-kare/izgara.sh` | K1 izgarasi (kodlayici ve bitrate dosyasi disaridan) |
| `tools/anahtar-kare/atlama.sh`, `atlama-hepsi.sh` | K2 atlama olcusu |
| `tools/anahtar-kare/harita.py`, `harita-hepsi.sh` | bugunku haritanin sectigi tavan |
| `tools/anahtar-kare/oz.py` | VMAF gunlugunden ortalama/p10/en kotu kare |
| `tools/anahtar-kare/tablo.py` | tablolar ve ozet cumleleri |
| `tools/anahtar-kare/rapor-govde.md` | bu dosyanin yer tutuculu govdesi |
| `tools/anahtar-kare/rapor-k4.md` | onceden yazilan karar esigi (K4) |
| `tools/anahtar-kare/rapor-recete.md` | recete (K5) |
| `tools/anahtar-kare/rapor.py` | bu dosyayi uretir |

Raporu yeniden uretmek: `python tools/anahtar-kare/rapor.py`.

{{K4}}
