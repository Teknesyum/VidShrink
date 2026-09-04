# Anahtar kare tavani

T133. Soru: **kisa `-g` hangi kaynakta kazandiriyor? Kazandirmiyorsa harita kolu
neden var?**

Raporun sayi tasiyan her cumlesi `tools/anahtar-kare/rapor.py` tarafindan
izgara dosyalarindan uretildi. Elle yazilan bolumlerde sayi yoktur; sayi gecen
her yer ureticiden gelir ve yaninda hangi tablodan/kolondan alindigi yazar.

## Cevap

**Zorluk esitlenmis calisma noktasinda cevap (b), es bit/piksel
calisma noktasinda cevap (c).**

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

### Uretilen ozet cumleler (zorluk)

Bu bolumun her cumlesi `tools/anahtar-kare/tablo.py` tarafindan asagidaki
tablolardan uretildi, elle yazilmadi. Her sayinin yaninda hangi tablodan ve
hangi kolondan geldigi yazili.

- **Cevap: (b).** Karar kurali K4'te olcumden once yazildi, esik **0.20 p10**. Kisa uc kazanan kaynak sayisi **0/4**, uzun uc kazanan **0/4**, isaret karisan **0/4** (kaynak: K1 izgarasi, `VMAF-NEG p10` kolonu; karsilastirma tabani `-g` = 10 sn hucresi).
- `s1-kesikli` (kesik cok): en iyi hucre **20 sn** (p10 84.691), 10 sn hucresi p10 84.597, fark **+0.094 p10 / +0.044 ort.** - esigin altinda. (kaynak: K1 izgarasi, `VMAF-NEG p10` ve `VMAF-NEG ort.` kolonlari.)
- `s2-durgun` (durgun): en iyi hucre **10 sn** (p10 82.238), 10 sn hucresi p10 82.238, fark **+0.000 p10 / +0.000 ort.** - esigin altinda. (kaynak: K1 izgarasi, `VMAF-NEG p10` ve `VMAF-NEG ort.` kolonlari.)
- `s3-hareketli` (hareketli): en iyi hucre **20 sn** (p10 86.519), 10 sn hucresi p10 86.384, fark **+0.135 p10 / +0.092 ort.** - esigin altinda. (kaynak: K1 izgarasi, `VMAF-NEG p10` ve `VMAF-NEG ort.` kolonlari.)
- `s4-yuksek` (yuksek cozunurluklu): en iyi hucre **10 sn** (p10 85.183), 10 sn hucresi p10 85.183, fark **+0.000 p10 / +0.000 ort.** - esigin altinda. (kaynak: K1 izgarasi, `VMAF-NEG p10` ve `VMAF-NEG ort.` kolonlari.)
- `s1-kesikli`: bugunku harita **5.000 sn** tavani secerdi; izgarada en yakin hucre **5 sn**, en iyi hucre **20 sn**, aradaki fark **+0.106 p10**. (kaynak: K3 tablosu, `p10 harita-hucresi` ve `p10 en-iyi` kolonlari.)
- `s2-durgun`: bugunku harita **10.000 sn** tavani secerdi; izgarada en yakin hucre **10 sn**, en iyi hucre **10 sn**, aradaki fark **+0.000 p10**. (kaynak: K3 tablosu, `p10 harita-hucresi` ve `p10 en-iyi` kolonlari.)
- `s3-hareketli`: bugunku harita **10.000 sn** tavani secerdi; izgarada en yakin hucre **10 sn**, en iyi hucre **20 sn**, aradaki fark **+0.135 p10**. (kaynak: K3 tablosu, `p10 harita-hucresi` ve `p10 en-iyi` kolonlari.)
- `s4-yuksek`: bugunku harita **5.349 sn** tavani secerdi; izgarada en yakin hucre **5 sn**, en iyi hucre **10 sn**, aradaki fark **+0.139 p10**. (kaynak: K3 tablosu, `p10 harita-hucresi` ve `p10 en-iyi` kolonlari.)
- `s1-kesikli`: `-g` 2 sn -> 20 sn arasinda yapisal atlama mesafesi **0.813 -> 2.685 sn** (3.3 kat, deterministik); ayni ucta net p50 45.8 -> 45.8 ms, IQR 18.7 / 47.9 ms (paylasilan makine sayisi, karara girmez). (kaynak: K2 tablosu, `yapisal mesafe (sn)`, `net p50 (ms)` ve `net IQR (ms)` kolonlari.)
- `s2-durgun`: `-g` 2 sn -> 20 sn arasinda yapisal atlama mesafesi **1.122 -> 10.622 sn** (9.5 kat, deterministik); ayni ucta net p50 28.9 -> 68.2 ms, IQR 7.6 / 35.2 ms (paylasilan makine sayisi, karara girmez). (kaynak: K2 tablosu, `yapisal mesafe (sn)`, `net p50 (ms)` ve `net IQR (ms)` kolonlari.)
- `s3-hareketli`: `-g` 2 sn -> 20 sn arasinda yapisal atlama mesafesi **1.005 -> 10.622 sn** (10.6 kat, deterministik); ayni ucta net p50 46.7 -> 303.3 ms, IQR 14.4 / 222.1 ms (paylasilan makine sayisi, karara girmez). (kaynak: K2 tablosu, `yapisal mesafe (sn)`, `net p50 (ms)` ve `net IQR (ms)` kolonlari.)
- `s4-yuksek`: `-g` 2 sn -> 20 sn arasinda yapisal atlama mesafesi **0.869 -> 3.087 sn** (3.6 kat, deterministik); ayni ucta net p50 301.6 -> 560.1 ms, IQR 129.8 / 438.3 ms (paylasilan makine sayisi, karara girmez). (kaynak: K2 tablosu, `yapisal mesafe (sn)`, `net p50 (ms)` ve `net IQR (ms)` kolonlari.)


### Uretilen ozet cumleler (bpp)

Bu bolumun her cumlesi `tools/anahtar-kare/tablo.py` tarafindan asagidaki
tablolardan uretildi, elle yazilmadi. Her sayinin yaninda hangi tablodan ve
hangi kolondan geldigi yazili.

- **Cevap: (c).** Karar kurali K4'te olcumden once yazildi, esik **0.20 p10**. Kisa uc kazanan kaynak sayisi **0/4**, uzun uc kazanan **2/4**, isaret karisan **0/4** (kaynak: K1 izgarasi, `VMAF-NEG p10` kolonu; karsilastirma tabani `-g` = 10 sn hucresi).
- `s1-kesikli` (kesik cok): en iyi hucre **15 sn** (p10 70.825), 10 sn hucresi p10 70.591, fark **+0.234 p10 / +0.048 ort.** - esigin ustunde. (kaynak: K1 izgarasi, `VMAF-NEG p10` ve `VMAF-NEG ort.` kolonlari.)
- `s2-durgun` (durgun): **kazanan secilemedi** - bes hucrenin tamami boyut bandinin (%0.5) disinda, damgali hucre "en iyi" olamaz. (kaynak: K1 izgarasi, `boyut sapmasi (%)` kolonu.)
- `s3-hareketli` (hareketli): en iyi hucre **20 sn** (p10 82.258), 10 sn hucresi p10 81.869, fark **+0.389 p10 / +0.195 ort.** - esigin ustunde. (kaynak: K1 izgarasi, `VMAF-NEG p10` ve `VMAF-NEG ort.` kolonlari.)
- `s4-yuksek` (yuksek cozunurluklu): en iyi hucre **20 sn** (p10 74.181), 10 sn hucresi p10 74.081, fark **+0.100 p10 / +0.025 ort.** - esigin altinda. (kaynak: K1 izgarasi, `VMAF-NEG p10` ve `VMAF-NEG ort.` kolonlari.)
- `s1-kesikli`: bugunku harita **5.000 sn** tavani secerdi; izgarada en yakin hucre **5 sn**, en iyi hucre **15 sn**, aradaki fark **+0.125 p10**. (kaynak: K3 tablosu, `p10 harita-hucresi` ve `p10 en-iyi` kolonlari.)
- `s2-durgun`: bugunku harita **10.000 sn** tavani secerdi; izgarada en yakin hucre **10 sn** (p10 96.838). Es boyut damgasi temiz hucre olmadigi icin "en iyi" hucre yok, fark hesaplanamaz. (kaynak: K3 tablosu.)
- `s3-hareketli`: bugunku harita **10.000 sn** tavani secerdi; izgarada en yakin hucre **10 sn**, en iyi hucre **20 sn**, aradaki fark **+0.389 p10**. (kaynak: K3 tablosu, `p10 harita-hucresi` ve `p10 en-iyi` kolonlari.)
- `s4-yuksek`: bugunku harita **5.349 sn** tavani secerdi; izgarada en yakin hucre **5 sn**, en iyi hucre **20 sn**, aradaki fark **+0.290 p10**. (kaynak: K3 tablosu, `p10 harita-hucresi` ve `p10 en-iyi` kolonlari.)
- `s1-kesikli`: `-g` 2 sn -> 20 sn arasinda yapisal atlama mesafesi **0.813 -> 2.685 sn** (3.3 kat, deterministik); ayni ucta net p50 37.7 -> 43.3 ms, IQR 6.0 / 35.8 ms (paylasilan makine sayisi, karara girmez). (kaynak: K2 tablosu, `yapisal mesafe (sn)`, `net p50 (ms)` ve `net IQR (ms)` kolonlari.)
- `s2-durgun`: `-g` 2 sn -> 20 sn arasinda yapisal atlama mesafesi **1.122 -> 10.622 sn** (9.5 kat, deterministik); ayni ucta net p50 56.1 -> 140.0 ms, IQR 20.4 / 100.0 ms (paylasilan makine sayisi, karara girmez). (kaynak: K2 tablosu, `yapisal mesafe (sn)`, `net p50 (ms)` ve `net IQR (ms)` kolonlari.)
- `s3-hareketli`: `-g` 2 sn -> 20 sn arasinda yapisal atlama mesafesi **1.005 -> 10.622 sn** (10.6 kat, deterministik); ayni ucta net p50 51.4 -> 124.3 ms, IQR 36.5 / 80.5 ms (paylasilan makine sayisi, karara girmez). (kaynak: K2 tablosu, `yapisal mesafe (sn)`, `net p50 (ms)` ve `net IQR (ms)` kolonlari.)
- `s4-yuksek`: `-g` 2 sn -> 20 sn arasinda yapisal atlama mesafesi **0.869 -> 3.087 sn** (3.6 kat, deterministik); ayni ucta net p50 109.0 -> 190.9 ms, IQR 34.7 / 194.7 ms (paylasilan makine sayisi, karara girmez). (kaynak: K2 tablosu, `yapisal mesafe (sn)`, `net p50 (ms)` ve `net IQR (ms)` kolonlari.)


## K3 — haritanin kolu izgaranin neresine dusuyor

Bu tablo sozlesmenin cevabidir: bugunku harita her kaynakta hangi tavani
secerdi, o secim izgaranin neresine dusuyor.

### K3 - haritanin secimi izgaranin neresine dusuyor (zorluk)

| kaynak | harita medyani (sn) | haritanin sectigi tavan (sn) | izgarada en yakin hucre | en iyi hucre (sn) | p10 harita-hucresi | p10 en-iyi | fark (p10) | fark (ort.) |
|---|---|---|---|---|---|---|---|---|
| `s1-kesikli` | 2.400 | 5.000 | 5 sn | 20 | 84.585 | 84.691 | +0.106 | +0.111 |
| `s2-durgun` | 20.000 | 10.000 | 10 sn | 10 | 82.238 | 82.238 | +0.000 | +0.000 |
| `s3-hareketli` | 10.000 | 10.000 | 10 sn | 20 | 86.384 | 86.519 | +0.135 | +0.092 |
| `s4-yuksek` | 5.349 | 5.349 | 5 sn | 10 | 85.044 | 85.183 | +0.139 | +0.059 |


### K3 - haritanin secimi izgaranin neresine dusuyor (bpp)

| kaynak | harita medyani (sn) | haritanin sectigi tavan (sn) | izgarada en yakin hucre | en iyi hucre (sn) | p10 harita-hucresi | p10 en-iyi | fark (p10) | fark (ort.) |
|---|---|---|---|---|---|---|---|---|
| `s1-kesikli` | 2.400 | 5.000 | 5 sn | 15 | 70.700 | 70.825 | +0.125 | +0.181 |
| `s2-durgun` | 20.000 | 10.000 | 10 sn | - | 96.838 | - | - | - |
| `s3-hareketli` | 10.000 | 10.000 | 10 sn | 20 | 81.869 | 82.258 | +0.389 | +0.195 |
| `s4-yuksek` | 5.349 | 5.349 | 5 sn | 20 | 73.891 | 74.181 | +0.290 | +0.114 |


Haritanin secimini ureten hesap uretim koduyla ayni: `SceneDetector`in taramasi
(`select='gte(scene,0.012)'`), `ThresholdRule.Measured` ile turetilen esik,
`MinSceneSeconds = 1,0`, sonra sahne surelerinin medyani `SceneMapMergeFactor`e
bolunup `[5,0 ; 10,0]` araligina kisiliyor.

| kaynak | sahne kesme adayi | esikten gecen kesme | sahne | sahne suresi medyani (sn) | haritanin sectigi tavan (sn) |
|---|---|---|---|---|---|
| `s1-kesikli` | 212 | 4 | 5 | 2.400 | 5.000 |
| `s2-durgun` | 0 | 0 | 1 | 20.000 | 10.000 |
| `s3-hareketli` | 185 | 1 | 2 | 10.000 | 10.000 |
| `s4-yuksek` | 365 | 3 | 4 | 5.349 | 5.349 |

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

| kaynak | kaynak karesi | kayipsiz PSNR | kayipsiz VMAF-NEG ort. | p10 | en kotu kare |
|---|---|---|---|---|---|
| `s1-kesikli` | 600 | inf | 98.837 | 97.486 | 97.421 |
| `s2-durgun` | 600 | inf | 97.438 | 97.429 | 97.425 |
| `s3-hareketli` | 600 | inf | 99.435 | 98.165 | 97.426 |
| `s4-yuksek` | 1200 | inf | 99.232 | 97.571 | 97.426 |

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

### K1 izgarasi - zorluk

| kaynak | sinif | `-g` (sn) | `-g` (kare) | teslim (bayt) | boyut sapmasi (%) | I-kare | gerc. aralik (sn) | VMAF-NEG ort. | VMAF-NEG p10 | en kotu kare | damga |
|---|---|---|---|---|---|---|---|---|---|---|---|
| `s1-kesikli` | kesik cok | 2 | 60 | 10651046 | -0.045 | 12 | 1.791 | 88.460 | 83.912 | 72.916 | es boyut |
| `s1-kesikli` | kesik cok | 5 | 150 | 10662068 | +0.059 | 8 | 2.705 | 88.977 | 84.585 | 74.747 | es boyut |
| `s1-kesikli` | kesik cok | 10 | 300 | 10661163 | +0.050 | 7 | 3.156 | 89.044 | 84.597 | 75.244 | es boyut |
| `s1-kesikli` | kesik cok | 15 | 450 | 10648699 | -0.067 | 7 | 3.156 | 89.081 | 84.673 | 75.824 | es boyut |
| `s1-kesikli` | kesik cok | 20 | 600 | 10656088 | +0.003 | 6 | 3.787 | 89.088 | 84.691 | 75.067 | es boyut - en iyi |
| `s2-durgun` | durgun | 2 | 60 | 207305 | -1.959 | 10 | 2.000 | 53.179 | 51.521 | 48.373 | **es boyut degil** |
| `s2-durgun` | durgun | 5 | 150 | 218938 | +3.543 | 4 | 5.000 | 79.793 | 76.123 | 76.033 | **es boyut degil** |
| `s2-durgun` | durgun | 10 | 300 | 211652 | +0.097 | 2 | 10.000 | 86.836 | 82.238 | 82.142 | es boyut - en iyi |
| `s2-durgun` | durgun | 15 | 450 | 221457 | +4.734 | 2 | 15.000 | 84.912 | 80.663 | 80.549 | **es boyut degil** |
| `s2-durgun` | durgun | 20 | 600 | 197881 | -6.416 | 1 | tek I-kare | 87.136 | 84.049 | 83.947 | **es boyut degil** |
| `s3-hareketli` | hareketli | 2 | 60 | 7158975 | +0.058 | 11 | 1.987 | 88.842 | 85.545 | 83.491 | es boyut |
| `s3-hareketli` | hareketli | 5 | 150 | 7163406 | +0.120 | 5 | 4.717 | 89.582 | 86.236 | 84.162 | es boyut |
| `s3-hareketli` | hareketli | 10 | 300 | 7151533 | -0.046 | 3 | 6.933 | 89.775 | 86.384 | 84.256 | es boyut |
| `s3-hareketli` | hareketli | 15 | 450 | 7152134 | -0.037 | 2 | 13.867 | 89.841 | 86.514 | 84.667 | es boyut |
| `s3-hareketli` | hareketli | 20 | 600 | 7147932 | -0.096 | 1 | tek I-kare | 89.867 | 86.519 | 84.824 | es boyut - en iyi |
| `s4-yuksek` | yuksek cozunurluklu | 2 | 120 | 33942988 | +0.138 | 12 | 1.750 | 89.176 | 84.702 | 75.040 | es boyut |
| `s4-yuksek` | yuksek cozunurluklu | 5 | 300 | 33885033 | -0.033 | 8 | 2.429 | 89.409 | 85.044 | 75.329 | es boyut |
| `s4-yuksek` | yuksek cozunurluklu | 10 | 600 | 33888325 | -0.024 | 6 | 3.400 | 89.468 | 85.183 | 75.514 | es boyut - en iyi |
| `s4-yuksek` | yuksek cozunurluklu | 15 | 900 | 33882536 | -0.041 | 6 | 3.400 | 89.466 | 85.179 | 75.621 | es boyut |
| `s4-yuksek` | yuksek cozunurluklu | 20 | 1200 | 33882693 | -0.040 | 5 | 4.250 | 89.467 | 85.154 | 75.549 | es boyut |


### K1 izgarasi - bpp

| kaynak | sinif | `-g` (sn) | `-g` (kare) | teslim (bayt) | boyut sapmasi (%) | I-kare | gerc. aralik (sn) | VMAF-NEG ort. | VMAF-NEG p10 | en kotu kare | damga |
|---|---|---|---|---|---|---|---|---|---|---|---|
| `s1-kesikli` | kesik cok | 2 | 60 | 4688172 | -0.091 | 12 | 1.791 | 78.373 | 69.235 | 55.692 | es boyut |
| `s1-kesikli` | kesik cok | 5 | 150 | 4695533 | +0.066 | 8 | 2.705 | 79.450 | 70.700 | 57.752 | es boyut |
| `s1-kesikli` | kesik cok | 10 | 300 | 4691206 | -0.026 | 7 | 3.156 | 79.583 | 70.591 | 58.181 | es boyut |
| `s1-kesikli` | kesik cok | 15 | 450 | 4692718 | +0.006 | 7 | 3.156 | 79.631 | 70.825 | 57.804 | es boyut - en iyi |
| `s1-kesikli` | kesik cok | 20 | 600 | 4694531 | +0.045 | 6 | 3.787 | 79.672 | 70.791 | 57.806 | es boyut |
| `s2-durgun` | durgun | 2 | 60 | 5018555 | -0.671 | 10 | 2.000 | 96.416 | 95.910 | 95.633 | **es boyut degil** |
| `s2-durgun` | durgun | 5 | 150 | 5112843 | +1.195 | 4 | 5.000 | 96.897 | 96.491 | 96.292 | **es boyut degil** |
| `s2-durgun` | durgun | 10 | 300 | 5079476 | +0.534 | 2 | 10.000 | 97.078 | 96.838 | 96.739 | **es boyut degil** |
| `s2-durgun` | durgun | 15 | 450 | 5085039 | +0.645 | 2 | 15.000 | 97.051 | 96.821 | 96.713 | **es boyut degil** |
| `s2-durgun` | durgun | 20 | 600 | 4966459 | -1.702 | 1 | tek I-kare | 97.140 | 97.053 | 96.760 | **es boyut degil** |
| `s3-hareketli` | hareketli | 2 | 60 | 4708960 | +0.090 | 11 | 1.987 | 83.573 | 80.441 | 77.243 | es boyut |
| `s3-hareketli` | hareketli | 5 | 150 | 4714220 | +0.202 | 5 | 4.717 | 84.811 | 81.731 | 78.402 | es boyut |
| `s3-hareketli` | hareketli | 10 | 300 | 4700929 | -0.081 | 3 | 6.933 | 85.053 | 81.869 | 79.429 | es boyut |
| `s3-hareketli` | hareketli | 15 | 450 | 4700384 | -0.092 | 2 | 13.867 | 85.260 | 82.191 | 79.705 | es boyut |
| `s3-hareketli` | hareketli | 20 | 600 | 4699139 | -0.119 | 1 | tek I-kare | 85.248 | 82.258 | 79.451 | es boyut - en iyi |
| `s4-yuksek` | yuksek cozunurluklu | 2 | 120 | 20514190 | +0.192 | 12 | 1.750 | 80.766 | 73.311 | 60.833 | es boyut |
| `s4-yuksek` | yuksek cozunurluklu | 5 | 300 | 20471666 | -0.015 | 8 | 2.429 | 81.103 | 73.891 | 61.171 | es boyut |
| `s4-yuksek` | yuksek cozunurluklu | 10 | 600 | 20463825 | -0.054 | 6 | 3.400 | 81.192 | 74.081 | 61.229 | es boyut |
| `s4-yuksek` | yuksek cozunurluklu | 15 | 900 | 20462161 | -0.062 | 6 | 3.400 | 81.236 | 74.059 | 60.987 | es boyut |
| `s4-yuksek` | yuksek cozunurluklu | 20 | 1200 | 20462208 | -0.062 | 5 | 4.250 | 81.217 | 74.181 | 60.732 | es boyut - en iyi |


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

### K2 - atlama maliyeti (bpp)

| kaynak | `-g` (sn) | I-kare | yapisal mesafe (sn) | yapisal mesafe (kare) | net p50 (ms) | net IQR (ms) | ham coz p50 (ms) | ham kopya p50 (ms) |
|---|---|---|---|---|---|---|---|---|
| `s1-kesikli` | 2 | 12 | 0.813 | 24.4 | 37.7 | 6.0 | 92.6 | 56.1 |
| `s1-kesikli` | 5 | 8 | 1.170 | 35.1 | 40.1 | 10.1 | 98.6 | 58.6 |
| `s1-kesikli` | 10 | 7 | 2.004 | 60.1 | 41.2 | 14.4 | 96.9 | 58.1 |
| `s1-kesikli` | 15 | 7 | 1.966 | 59.0 | 39.2 | 14.5 | 96.1 | 56.8 |
| `s1-kesikli` | 20 | 6 | 2.685 | 80.6 | 43.3 | 35.8 | 99.6 | 56.0 |
| `s2-durgun` | 2 | 10 | 1.122 | 33.6 | 56.1 | 20.4 | 123.6 | 67.7 |
| `s2-durgun` | 5 | 4 | 2.080 | 62.4 | 61.0 | 14.2 | 131.6 | 69.9 |
| `s2-durgun` | 10 | 2 | 4.372 | 131.1 | 73.1 | 31.3 | 145.0 | 71.1 |
| `s2-durgun` | 15 | 2 | 5.622 | 168.6 | 109.2 | 51.4 | 200.8 | 99.1 |
| `s2-durgun` | 20 | 1 | 10.622 | 318.6 | 140.0 | 100.0 | 211.1 | 71.4 |
| `s3-hareketli` | 2 | 11 | 1.005 | 30.1 | 51.4 | 36.5 | 137.8 | 79.5 |
| `s3-hareketli` | 5 | 5 | 2.472 | 74.1 | 48.1 | 9.7 | 105.5 | 58.3 |
| `s3-hareketli` | 10 | 3 | 3.522 | 105.6 | 51.1 | 31.9 | 115.3 | 59.2 |
| `s3-hareketli` | 15 | 2 | 5.422 | 162.6 | 56.2 | 54.0 | 113.4 | 58.3 |
| `s3-hareketli` | 20 | 1 | 10.622 | 318.6 | 124.3 | 80.5 | 184.3 | 57.4 |
| `s4-yuksek` | 2 | 12 | 0.869 | 52.2 | 109.0 | 34.7 | 175.1 | 67.8 |
| `s4-yuksek` | 5 | 8 | 1.402 | 84.1 | 102.9 | 50.2 | 171.8 | 64.2 |
| `s4-yuksek` | 10 | 6 | 2.806 | 168.4 | 151.4 | 176.9 | 215.4 | 67.5 |
| `s4-yuksek` | 15 | 6 | 2.891 | 173.5 | 152.3 | 169.2 | 223.4 | 67.3 |
| `s4-yuksek` | 20 | 5 | 3.087 | 185.2 | 190.9 | 194.7 | 260.9 | 68.1 |


### K2 - atlama maliyeti (zorluk)

| kaynak | `-g` (sn) | I-kare | yapisal mesafe (sn) | yapisal mesafe (kare) | net p50 (ms) | net IQR (ms) | ham coz p50 (ms) | ham kopya p50 (ms) |
|---|---|---|---|---|---|---|---|---|
| `s1-kesikli` | 2 | 12 | 0.813 | 24.4 | 45.8 | 18.7 | 101.7 | 58.6 |
| `s1-kesikli` | 5 | 8 | 1.170 | 35.1 | 53.5 | 34.5 | 117.1 | 64.8 |
| `s1-kesikli` | 10 | 7 | 2.004 | 60.1 | 58.5 | 65.5 | 137.4 | 62.5 |
| `s1-kesikli` | 15 | 7 | 1.966 | 59.0 | 47.4 | 20.8 | 102.1 | 52.5 |
| `s1-kesikli` | 20 | 6 | 2.685 | 80.6 | 45.8 | 47.9 | 102.2 | 55.2 |
| `s2-durgun` | 2 | 10 | 1.122 | 33.6 | 28.9 | 7.6 | 79.7 | 48.5 |
| `s2-durgun` | 5 | 4 | 2.080 | 62.4 | 30.4 | 8.3 | 81.7 | 51.1 |
| `s2-durgun` | 10 | 2 | 4.372 | 131.1 | 40.9 | 13.0 | 90.5 | 51.1 |
| `s2-durgun` | 15 | 2 | 5.622 | 168.6 | 43.3 | 23.5 | 102.0 | 57.6 |
| `s2-durgun` | 20 | 1 | 10.622 | 318.6 | 68.2 | 35.2 | 122.6 | 55.2 |
| `s3-hareketli` | 2 | 11 | 1.005 | 30.1 | 46.7 | 14.4 | 100.6 | 56.4 |
| `s3-hareketli` | 5 | 5 | 2.472 | 74.1 | 52.6 | 20.7 | 111.2 | 56.5 |
| `s3-hareketli` | 10 | 3 | 3.522 | 105.6 | 63.2 | 45.0 | 122.1 | 59.4 |
| `s3-hareketli` | 15 | 2 | 5.422 | 162.6 | 99.4 | 144.6 | 197.8 | 91.2 |
| `s3-hareketli` | 20 | 1 | 10.622 | 318.6 | 303.3 | 222.1 | 415.5 | 120.0 |
| `s4-yuksek` | 2 | 12 | 0.869 | 52.2 | 301.6 | 129.8 | 545.6 | 216.8 |
| `s4-yuksek` | 5 | 8 | 1.402 | 84.1 | 382.9 | 299.9 | 582.8 | 191.9 |
| `s4-yuksek` | 10 | 6 | 2.806 | 168.4 | 563.9 | 293.6 | 803.1 | 239.5 |
| `s4-yuksek` | 15 | 6 | 2.891 | 173.5 | 508.6 | 369.4 | 749.1 | 201.6 |
| `s4-yuksek` | 20 | 5 | 3.087 | 185.2 | 560.1 | 438.3 | 879.4 | 263.5 |


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

| hucre | yapisal mesafe kosum 1 (sn) | yapisal mesafe kosum 2 (sn) | net p50 kosum 1 (ms) | net p50 kosum 2 (ms) | net p50 orani |
|---|---|---|---|---|---|
| `bpp-s1-kesikli-g2` | 0.813 | 0.813 | 35.2 | 37.7 | 0.93 |
| `bpp-s1-kesikli-g5` | 1.170 | 1.170 | 35.5 | 40.1 | 0.89 |
| `bpp-s1-kesikli-g10` | 2.004 | 2.004 | 34.5 | 41.2 | 0.84 |
| `bpp-s1-kesikli-g15` | 1.966 | 1.966 | 34.3 | 39.2 | 0.87 |
| `bpp-s1-kesikli-g20` | 2.685 | 2.685 | 35.6 | 43.3 | 0.82 |
| `bpp-s2-durgun-g2` | 1.122 | 1.122 | 65.1 | 56.1 | 1.16 |
| `bpp-s2-durgun-g5` | 2.080 | 2.080 | 133.0 | 61.0 | 2.18 |
| `bpp-s2-durgun-g10` | 4.372 | 4.372 | 409.5 | 73.1 | 5.60 |
| `bpp-s2-durgun-g15` | 5.622 | 5.622 | 536.5 | 109.2 | 4.91 |
| `bpp-s2-durgun-g20` | 10.622 | 10.622 | 861.0 | 140.0 | 6.15 |
| `bpp-s3-hareketli-g2` | 1.005 | 1.005 | 551.5 | 51.4 | 10.73 |
| `bpp-s3-hareketli-g5` | 2.472 | 2.472 | 452.0 | 48.1 | 9.40 |
| `bpp-s3-hareketli-g10` | 3.522 | 3.522 | 464.6 | 51.1 | 9.09 |
| `bpp-s3-hareketli-g15` | 5.422 | 5.422 | 339.8 | 56.2 | 6.05 |
| `bpp-s3-hareketli-g20` | 10.622 | 10.622 | 454.9 | 124.3 | 3.66 |
| `bpp-s4-yuksek-g2` | 0.869 | 0.869 | 372.0 | 109.0 | 3.41 |
| `bpp-s4-yuksek-g5` | 1.402 | 1.402 | 412.3 | 102.9 | 4.01 |
| `bpp-s4-yuksek-g10` | 2.806 | 2.806 | 628.9 | 151.4 | 4.15 |
| `bpp-s4-yuksek-g15` | 2.891 | 2.891 | 363.5 | 152.3 | 2.39 |
| `bpp-s4-yuksek-g20` | 3.087 | 3.087 | 439.9 | 190.9 | 2.30 |


**Zorluk esitlenmis nokta:**

| hucre | yapisal mesafe kosum 1 (sn) | yapisal mesafe kosum 2 (sn) | net p50 kosum 1 (ms) | net p50 kosum 2 (ms) | net p50 orani |
|---|---|---|---|---|---|
| `zorluk-s1-kesikli-g2` | 0.813 | 0.813 | 159.0 | 45.8 | 3.47 |
| `zorluk-s1-kesikli-g5` | 1.170 | 1.170 | 121.7 | 53.5 | 2.27 |
| `zorluk-s1-kesikli-g10` | 2.004 | 2.004 | 124.6 | 58.5 | 2.13 |
| `zorluk-s1-kesikli-g15` | 1.966 | 1.966 | 113.6 | 47.4 | 2.40 |
| `zorluk-s1-kesikli-g20` | 2.685 | 2.685 | 164.6 | 45.8 | 3.59 |
| `zorluk-s2-durgun-g2` | 1.122 | 1.122 | 109.5 | 28.9 | 3.79 |
| `zorluk-s2-durgun-g5` | 2.080 | 2.080 | 89.9 | 30.4 | 2.96 |
| `zorluk-s2-durgun-g10` | 4.372 | 4.372 | 84.1 | 40.9 | 2.06 |
| `zorluk-s2-durgun-g15` | 5.622 | 5.622 | 45.3 | 43.3 | 1.05 |
| `zorluk-s2-durgun-g20` | 10.622 | 10.622 | 69.2 | 68.2 | 1.01 |
| `zorluk-s3-hareketli-g2` | 1.005 | 1.005 | 46.4 | 46.7 | 0.99 |
| `zorluk-s3-hareketli-g5` | 2.472 | 2.472 | 56.5 | 52.6 | 1.07 |
| `zorluk-s3-hareketli-g10` | 3.522 | 3.522 | 60.3 | 63.2 | 0.95 |
| `zorluk-s3-hareketli-g15` | 5.422 | 5.422 | 65.5 | 99.4 | 0.66 |
| `zorluk-s3-hareketli-g20` | 10.622 | 10.622 | 141.3 | 303.3 | 0.47 |
| `zorluk-s4-yuksek-g2` | 0.869 | 0.869 | 120.6 | 301.6 | 0.40 |
| `zorluk-s4-yuksek-g5` | 1.402 | 1.402 | 123.6 | 382.9 | 0.32 |
| `zorluk-s4-yuksek-g10` | 2.806 | 2.806 | 164.0 | 563.9 | 0.29 |
| `zorluk-s4-yuksek-g15` | 2.891 | 2.891 | 168.5 | 508.6 | 0.33 |
| `zorluk-s4-yuksek-g20` | 3.087 | 3.087 | 190.9 | 560.1 | 0.34 |


Bu yuzden **K2'nin karara giren kolonu `yapisal mesafe`dir**. `net p50`'nin
mutlak degeri rapor edilmiyor; isareti de yirmi hucrenin cogunda yapisal kolonla
ayni yone bakiyor ama iki kolda tersine donuyor (yukaridaki borc kalemi), yani
bagimsiz bir dogrulama olarak sayilmiyor.

## K5 — recete

Uretim degisikligi bu sozlesmede yapilmadi (`src` ve `tests` kapsam disi,
T108 ayni dosyalarda). Asagisi uygulama sozlesmesinin alacagi recetedir.
Recetedeki her sayi bu raporun izgarasindan gelir.

Uc kalem var. **R1 uygulanabilir** — iki calisma noktasi da ayni yone bakiyor.
**R2 kapili** — tek calisma noktasinda esigi geciyor ve izgaranin ust ucu klip
uzunluguna carpiyor; kapiyi acan olcum yazili. **R3 donanim kolu**, kendi
izgarasindan cikiyor.

Her satirdaki sayi yukaridaki tablolarin bir hucresidir; recete kendi sayisini
uretmiyor.

**Satir numaralari `origin/main` `55f245a`ta okundu.** Bu olcumun dali daha eski
bir tabandan ayrildigi icin ayni sabitler burada 65 satir yukarida duruyor;
uygulama sozlesmesi once **sembol adiyla** arasin, satir numarasi yalniz
kolaylik icindir. Sabitlerin **degerleri** iki agacta da ayni, yani olcum
gecerli.

### R1 — haritanin `-g` kolu kaldirilir, tavan tek degere sabitlenir

| ne | deger |
|---|---|
| dosya | `src/VidShrink.Core/FfmpegArguments.cs` |
| satir | 298-318 (`KeyframeCeilingSeconds(SceneMap?)` govdesi) |
| eski | `Math.Clamp(mappedMedianSeconds / SceneMapMergeFactor, KeyframeCeilingMinSeconds, KeyframeCeilingMaxSeconds)` |
| yeni | `KeyframeCeilingDefaultSeconds` (govde tek satira iner, `scenes` parametresi dusler) |
| birlikte olen sabitler | 284 `SceneMapGroundTruthCutsInWindow`, 285 `SceneMapMappedCutsInWindow`, 286 `SceneMapMergeFactor`, 287 `KeyframeCeilingMinSeconds`, 288 `KeyframeCeilingMaxSeconds` |
| **olmeyen** sabit | 283 `SceneMapRuleOfRecord` — sahne **tarama** esigi, tavan koluyla ilgisi yok; bes olcu ve `OluUyeTests.cs:428` ona bakiyor |
| birlikte olen dal | 328 `fromMap` her zaman `false` olur; `KeyframeRange.FromSceneMap` yalniz donanim/yazilim ayrimini tasir hale gelir |

**Dayanak.** Kolun butun erisim alani `[5,0 ; 10,0]`. Izgarada bu araligin iki
ucu arasindaki fark sekiz hucrenin sekizinde de esigi gecemiyor, ve sekizin
yedisinde **negatif** (K1 izgaralari, `VMAF-NEG p10` kolonu):

| kaynak | calisma noktasi | 5 sn p10 | 10 sn p10 | fark (p10) | esigi (0,20 p10) geciyor mu |
|---|---|---|---|---|---|
| `s1-kesikli` | es bit/piksel | 70,700 | 70,591 | +0,109 | hayir |
| `s2-durgun` | es bit/piksel | 96,491 | 96,838 | -0,347 | hayir |
| `s3-hareketli` | es bit/piksel | 81,731 | 81,869 | -0,138 | hayir |
| `s4-yuksek` | es bit/piksel | 73,891 | 74,081 | -0,190 | hayir |
| `s1-kesikli` | zorluk esitlenmis | 84,585 | 84,597 | -0,012 | hayir |
| `s2-durgun` | zorluk esitlenmis | 76,123 | 82,238 | -6,115 | hayir |
| `s3-hareketli` | zorluk esitlenmis | 86,236 | 86,384 | -0,148 | hayir |
| `s4-yuksek` | zorluk esitlenmis | 85,044 | 85,183 | -0,139 | hayir |

Tek pozitif hucre `s1-kesikli` es bit/piksel, `+0,109` — esigin yarisi kadar.

Iki hucre boyut bandinin disindaki `s2-durgun`dan geliyor (`-0,347` ve
`-6,115`); onlari atsak bile kalan alti hucrenin besi negatif ve altisi da
esigin altinda — sonuc degismiyor.

Kolun bugun ne sectigi K3 tablosunda. Es bit/piksel noktasinda kayip
`+0,125 / - / +0,389 / +0,290 p10`, zorluk esitlenmis noktasinda
`+0,106 / +0,000 / +0,135 / +0,139 p10` (K3 tablolari, `fark (p10)` kolonu).
Sekiz kaynak-nokta cifti icin: **altisinda** harita hucresi en iyi hucre degil,
**birinde** (zorluk esitlenmis `s2-durgun`) en iyi hucreyle ayni hucre,
**birinde** (es bit/piksel `s2-durgun`) es boyut damgasi temiz hucre olmadigi
icin kiyas kurulamiyor. Kolun **kazandirdigi** tek bir hucre yok: fark kolonu
hicbir satirda negatif degil.

Isaret ikinci bir kodlayicida da ayni — 5 sn'nin 10 sn'ye gore farki (ikinci
kodlayici ve donanim tablolari, `VMAF-NEG p10` kolonu):

| kodlayici | kaynak | 5 sn p10 | 10 sn p10 | fark (p10) |
|---|---|---|---|---|
| `libsvtav1` preset 4 | `s1-kesikli` | 64,495 | 65,792 | -1,297 |
| `av1_nvenc` p5 | `s1-kesikli` | 63,324 | 64,745 | -1,421 |
| `av1_nvenc` p5 | `s3-hareketli` | 82,222 | 83,646 | -1,424 |

**Sabitleyecek olcu.** `tests/VidShrink.Tests/FfmpegArgumentsTests.cs`e yeni bir
olcu: medyan sahne uzunlugu **2,4 sn** olan bir harita (`s1-kesikli`in bugun
olculen haritasi, K3 tablosu `harita medyani` kolonu) 30 fps'te `-g 300`
uretmeli.

```
var args = FfmpegArguments.KeyframeArgs("libx264", 30, CarpikHarita(2.4, 2.4, 2.4)).ToList();
Assert.Equal("300", args[args.IndexOf("-g") + 1]);
```

**Kirmasi gereken mutasyon.** Kaldirilan kiskaci geri koymak —
`Math.Clamp(medyan / 1.0, 5.0, 10.0)` — ayni cagriyi `-g 150` yapar ve olcu
kizarir. Ikinci mutasyon: `KeyframeCeilingDefaultSeconds`i 5,0'a cekmek de ayni
olcuyu kizartir, cunku iddia sabiti kendi modulunden okumuyor.

**Once kirilacak, yeniden temellendirilecek olculer.** Bunlar bugunku davranisi
pimliyor; R1 onlari yapisi geregi kizartir, uygulama sozlesmesi bunlari
**silmez, yeniden temellendirir** ve her birinin ustundeki gerekce cumlesini de
yeniler:

| olcu | yer | R1'den sonra ne olur |
|---|---|---|
| `Kiskacin_alt_ucu_bes_saniyede_bagliyor` | `FfmpegArgumentsTests.cs:894` | 60 fps'te `-g 300` yerine `600` gelir; ikinci iddia (`KeyframeCeilingMinSeconds == 5.0`) derlenmez |
| `Kiskacin_ust_ucu_on_saniyede_bagliyor` | `FfmpegArgumentsTests.cs:909` | deger dogru kalir (`600`) ama `KeyframeCeilingMaxSeconds` iddiasi derlenmez |
| `Duzeltme_olculen_pencerede_gercek_cekim_uzunlugunu_uretir` | `FfmpegArgumentsTests.cs:878` | `SceneMapGroundTruthCutsInWindow` ustunden kuruluyor; kol olunce iddiasi kalmaz |
| harita/haritasiz `-g` cifti | `EncodeRunnerTests.cs:237`, `:382` | ikisi de `KeyframeCeilingMinSeconds`i okuyor |
| onizleme parcasinin haritali araligi | `PreviewSegmentTests.cs:270`, `:342` | ayni sabiti okuyor |

### R2 — yazilim tavani 10 sn'nin ustune, **kapili**

| ne | deger |
|---|---|
| dosya | `src/VidShrink.Core/FfmpegArguments.cs` |
| satir | 282 |
| sabit | `KeyframeCeilingDefaultSeconds` |
| eski | `10.0` |
| aday yeni | `15.0` |
| durum | **uygulanmaz** — kapi asagida |

**Neden aday.** Es bit/piksel noktasinda 15 sn hucresi 10 sn'ye gore
`s1-kesikli` **+0,234**, `s3-hareketli` **+0,322** p10; ikisi de es boyut
damgasi temiz ve ikisi de esigin ustunde. K4 kurali bu noktada **(c)** veriyor.

**Neden kapili.** Ayni izgara zorluk esitlenmis noktada ayni hucrelerde
**+0,076** ve **+0,130** veriyor — ikisi de esigin altinda, ve o noktada karar
**(b)**. Iki calisma noktasi ayni yone bakiyor ama yalniz biri esigi geciyor;
onceden yazilan kural hangi noktanin "asil" oldugunu soylemiyor ve sonradan
secmek K4 kilidini bozar.

Ikinci kapi olcunun kendi siniri: klipler 20 sn. 15 sn ve 20 sn tavanlar bu
klipte artik neredeyse baglamiyor — `s1-kesikli`de 10 sn ile 15 sn **ayni yedi
I-kareyi** veriyor ve yalniz ikisinin yeri 67-233 ms kayiyor (K1 izgarasi,
`I-kare` ve `gerc. aralik` kolonlari). Yani olculen sey "15 saniyelik tavan"
degil, "tavanin kalkmasi". 20 sn hucresinde iki kaynak **tek I-kareye** dusuyor;
orada aralik diye bir sey kalmiyor.

**Kapiyi acan olcum.** Ayni izgara, **en az 60 sn**lik kliplerle ve `-g`
degerleri 10 / 15 / 20 / 30 / 45 sn. Tavanin gercekten bagladigi (I-kare
sayisinin `-g` ile degistigi) hucrelerde 15 sn'nin 10 sn'ye gore farki
**iki calisma noktasinda da** ve **dort kaynagin en az ikisinde** 0,20 p10'u
gecerse R2 uygulanir.

**Sabitleyecek olcu (kapi acilirsa).** `Kiskacin_ust_ucu_on_saniyede_bagliyor`
yeniden temellendirilir: medyani 30 sn olan harita 60 fps'te `-g 900` uretmeli.
**Kirmasi gereken mutasyon:** sabiti 10,0'da birakmak `-g 600` verir ve olcu
kizarir.

**Yan etki uyarisi.** `Donanimda_ust_sinir_haritadan_etkilenmez`
(`FfmpegArgumentsTests.cs:942`) donanim tavaninin yazilim varsayilanindan
**kisa** oldugunu iddia ediyor. R2 ile R3 birlikte uygulanirsa 10,0 < 15,0
olarak dogru kalir; yalniz R3 uygulanirsa (10,0 vs 10,0) bu olcu kizarir.

### R3 — donanim tavani 5 sn'den 10 sn'ye

| ne | deger |
|---|---|
| dosya | `src/VidShrink.Core/FfmpegArguments.cs` |
| satir | 289 |
| sabit | `HardwareKeyframeCeilingSeconds` |
| eski | `5.0` |
| yeni | `10.0` |

**Dayanak.** Donanim izgarasi (`av1_nvenc` preset p5, es bit/piksel, ayni dort
kaynak) asagidaki donanim tablosunda. Bugunku 5 sn'nin 10 sn'ye gore farki:
`s1-kesikli` **-1,421**, `s3-hareketli` **-1,424**, `s4-yuksek` **+0,031** p10;
`s2-durgun`da bes hucrenin besi de boyut bandinin disinda, o kaynak karara
girmiyor. Iki kaynakta kayip esigin (0,20 p10) **yedi katindan** buyuk.

Ikisinin boyut damgasi ayni degil: `s3-hareketli`de 5 sn hucresi **temiz**
(sapma %-0,470) — orada 1,424 p10'luk kayip boyut farkiyla aciklanamaz.
`s1-kesikli`de 5 sn hucresi banda girmiyor (sapma **%-1,171**, yani dosya daha
**kucuk**), o yuzden oradaki 1,421'in bir kismi boyuttan geliyor olabilir;
karari tasiyan kaynak `s3-hareketli`dir, `s1-kesikli` isaret dogrulamasidir.

Donanimda sahne kesimi yok, gerceklesen aralik tam olarak tavanin kendisi
(tabloda `gerc. aralik` kolonu 2 / 5 / 10 / 15 sn hucrelerinde `-g`'nin kendisi;
20 sn hucresinde tek I-kare kaliyor). Ust siniri secmek
dogrudan atlama butcesini secmek demek; bugunku 5,0 o butceyi kalitenin
belirgin zararina daraltiyor.

**Sabitleyecek olcu.** `Donanim_ust_siniri_bes_saniyede_sabit`
(`FfmpegArgumentsTests.cs:924`) yeniden temellendirilir: uzun sahneli harita
60 fps'te `-g 600` almali, 300 degil. **Kirmasi gereken mutasyon:** sabiti
5,0'da birakmak `-g 300` verir ve olcu kizarir.

**Bedeli yazili.** Donanimda tavani ikiye katlamak beklenen atlama mesafesini de
ikiye katlar (`av1_nvenc`de gerceklesen aralik = tavan, K2'nin yazilim
tablosundaki gibi icerige bagli degil). Bu bedel puanda gorunmez; uygulama
sozlesmesi kararini bu iki sayiya birlikte bakarak verir.

### Degistirilmeyecekler

- `KeyframeFloorSeconds = 1.0` (satir 281). Izgara alt ucu 2 sn; 2 sn'nin 10
  sn'ye gore farki sekiz hucrenin sekizinde de negatif ve buyuk
  (`-0,481` ile `-30,717` arasi). Kisa uc icin degisiklik onerilmiyor.
- Kiskacin **alt** ucunun 2 sn'ye indirilmesi. Ayni sayilar bunu dogrudan
  reddediyor.

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

### Donanim kolu - `av1_nvenc` preset p5, es bit/piksel

| kaynak | `-g` (sn) | `-g` (kare) | teslim (bayt) | boyut sapmasi (%) | I-kare | gerc. aralik (sn) | VMAF-NEG ort. | VMAF-NEG p10 | en kotu kare | damga |
|---|---|---|---|---|---|---|---|---|---|---|
| `s1-kesikli` | 2 | 60 | 4439151 | +0.094 | 10 | 2.000 | 78.591 | 60.648 | 45.591 | es boyut |
| `s1-kesikli` | 5 | 150 | 4383048 | -1.171 | 4 | 5.000 | 79.683 | 63.324 | 50.078 | **es boyut degil** |
| `s1-kesikli` | 10 | 300 | 4456117 | +0.477 | 2 | 10.000 | 80.084 | 64.745 | 51.666 | es boyut - en iyi |
| `s1-kesikli` | 15 | 450 | 4435072 | +0.002 | 2 | 15.000 | 80.092 | 64.731 | 51.145 | es boyut |
| `s1-kesikli` | 20 | 600 | 4461452 | +0.597 | 1 | tek I-kare | 80.214 | 65.121 | 51.145 | **es boyut degil** |
| `s2-durgun` | 2 | 60 | 4687164 | +5.219 | 10 | 2.000 | 96.089 | 95.866 | 94.177 | **es boyut degil** |
| `s2-durgun` | 5 | 150 | 4507369 | +1.182 | 4 | 5.000 | 96.319 | 95.995 | 94.181 | **es boyut degil** |
| `s2-durgun` | 10 | 300 | 4367290 | -1.962 | 2 | 10.000 | 96.355 | 96.058 | 94.183 | **es boyut degil** |
| `s2-durgun` | 15 | 450 | 4420757 | -0.762 | 2 | 15.000 | 96.351 | 96.058 | 94.183 | **es boyut degil** |
| `s2-durgun` | 20 | 600 | 4290884 | -3.677 | 1 | tek I-kare | 96.363 | 96.058 | 94.183 | **es boyut degil** |
| `s3-hareketli` | 2 | 60 | 4582306 | +0.040 | 10 | 2.000 | 85.893 | 78.972 | 65.717 | es boyut |
| `s3-hareketli` | 5 | 150 | 4558982 | -0.470 | 4 | 5.000 | 87.025 | 82.222 | 69.256 | es boyut |
| `s3-hareketli` | 10 | 300 | 4585178 | +0.102 | 2 | 10.000 | 87.644 | 83.646 | 73.216 | es boyut |
| `s3-hareketli` | 15 | 450 | 4606597 | +0.570 | 2 | 15.000 | 87.804 | 83.684 | 72.807 | **es boyut degil** |
| `s3-hareketli` | 20 | 600 | 4569388 | -0.242 | 1 | tek I-kare | 87.993 | 84.133 | 72.807 | es boyut - en iyi |
| `s4-yuksek` | 2 | 120 | 20088536 | +1.658 | 10 | 2.000 | 81.453 | 67.722 | 54.200 | **es boyut degil** |
| `s4-yuksek` | 5 | 300 | 19737145 | -0.120 | 4 | 5.000 | 81.421 | 68.310 | 55.031 | es boyut |
| `s4-yuksek` | 10 | 600 | 19609467 | -0.766 | 2 | 10.000 | 81.475 | 68.279 | 55.031 | **es boyut degil** |
| `s4-yuksek` | 15 | 900 | 19706455 | -0.275 | 2 | 15.000 | 81.494 | 68.408 | 55.031 | es boyut - en iyi |
| `s4-yuksek` | 20 | 1200 | 19662476 | -0.498 | 1 | tek I-kare | 81.503 | 68.408 | 55.031 | es boyut |


- av1_nvenc `s1-kesikli`: en iyi es-boyut hucresi **10 sn** (p10 64.745). 5 sn hucresi p10 63.324, 10 sn hucresi p10 64.745; 5 sn'nin 10 sn'ye gore farki **-1.421 p10**. (kaynak: av1_nvenc tablosu, `VMAF-NEG p10` kolonu.)
- av1_nvenc `s3-hareketli`: en iyi es-boyut hucresi **20 sn** (p10 84.133). 5 sn hucresi p10 82.222, 10 sn hucresi p10 83.646; 5 sn'nin 10 sn'ye gore farki **-1.424 p10**. (kaynak: av1_nvenc tablosu, `VMAF-NEG p10` kolonu.)
- av1_nvenc `s4-yuksek`: en iyi es-boyut hucresi **15 sn** (p10 68.408). 5 sn hucresi p10 68.310, 10 sn hucresi p10 68.279; 5 sn'nin 10 sn'ye gore farki **+0.031 p10**. (kaynak: av1_nvenc tablosu, `VMAF-NEG p10` kolonu.)

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

### Ikinci kodlayici - `libsvtav1` preset 4, es bit/piksel

| kaynak | `-g` (sn) | `-g` (kare) | teslim (bayt) | boyut sapmasi (%) | I-kare | gerc. aralik (sn) | VMAF-NEG ort. | VMAF-NEG p10 | en kotu kare | damga |
|---|---|---|---|---|---|---|---|---|---|---|
| `s1-kesikli` | 2 | 60 | 4315205 | -5.228 | 10 | 2.000 | 81.511 | 65.163 | 54.311 | **es boyut degil** |
| `s1-kesikli` | 5 | 150 | 4453686 | -2.186 | 4 | 5.000 | 81.986 | 64.495 | 44.723 | **es boyut degil** |
| `s1-kesikli` | 10 | 300 | 4575984 | +0.499 | 2 | 10.000 | 82.404 | 65.792 | 54.172 | es boyut - en iyi |
| `s1-kesikli` | 15 | 450 | 4811808 | +5.679 | 2 | 15.000 | 82.036 | 65.407 | 52.454 | **es boyut degil** |
| `s1-kesikli` | 20 | 600 | 4609524 | +1.236 | 1 | tek I-kare | 82.423 | 65.508 | 55.393 | **es boyut degil** |


- libsvtav1 `s1-kesikli`: en iyi es-boyut hucresi **10 sn** (p10 65.792). 5 sn hucresi p10 64.495, 10 sn hucresi p10 65.792; 5 sn'nin 10 sn'ye gore farki **-1.297 p10**. (kaynak: libsvtav1 tablosu, `VMAF-NEG p10` kolonu.)

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

## K4 — Onceden yazilan karar esigi

### Olcut

- **Birincil olcu:** VMAF-NEG **p10**. Bu depoda kuyruk kalemi kararlari p10'a
  baglandi (`docs/olcumler/auto-mod.md`, K4 ayristirmasi).
- **Ikincil olcu:** VMAF-NEG **ortalama**. Isaret birincil olcuyle ayni degilse
  hucre "karisik" damgasi alir, kazanan sayilmaz.
- **Boyut esitligi:** iki pass, sabit `-b:v`, teslim edilen boyut kaynak icindeki
  butun `-g` hucrelerinde **%0,5** bandinda kalmali. Bandin disina cikan hucre
  atilmaz, **"es boyut degil"** damgasiyla tabloda kalir ve kazanan secilemez.
- **En kotu kare:** raporlanir, karara girmez (tek kare gurultuye acik).

### Esik degeri: **0,20 p10**

Iki uctan temellendirildi, ikisi de bu depoda olculmus sayilar:

- Bu depoda "gercek ama ikinci derece" diye kabul edilen en kucuk etki
  **+0,179 p10** (kesime hizalama, `FfmpegArguments.cs:172-174`). Esik bunun
  hemen ustunde.
- Bugunku tavan araliginin icindeki bilinen adim **5 s -> 10 s = +0,033 p10**
  (`FfmpegArguments.cs:184-187`). Esik bunun alti kat ustunde, yani bugunku
  kelepcenin ic gurultusuyle karistirilamaz.

### Karar kurali (olcumden once sabit)

Her kaynak icin `en_iyi_hucre` = p10'u en yuksek, es boyut damgasi temiz hucre.
`h10` = 10,0 s hucresi (bugunku haritasiz varsayilan, HandBrake ile ayni).

1. **(a) Kolu korurum** — dort kaynagin **en az ikisinde** `en_iyi_hucre`
   suresi **10,0 s'nin altinda** ve `p10(en_iyi) - p10(h10) >= 0,20` ise. O
   zaman kazanan kaynak sinifi yazilir ve esik olcuye baglanir.
2. **(b) Kolu kaldiririm / tavani tek degere sabitlerim** — hicbir kaynakta
   1. madde saglanmiyorsa. Kisa `-g` hicbir sinifta 0,20 p10 kazandirmiyorsa
   harita kolunun `-g` uzerindeki tek etkisi optimumdan uzaklasmaktir.
3. **(c) Tavani yukseltirim** — dort kaynagin **en az ikisinde**
   `en_iyi_hucre` suresi **10,0 s'nin ustunde** ve
   `p10(en_iyi) - p10(h10) >= 0,20` ise. (a) ve (c) ayni anda cikarsa kaynak
   sinifina gore ayrisma yazilir, tek sayiya indirgenmez.
4. Hicbiri saglanmiyorsa sonuc **(b)**: `-g`'nin 2 s ustundeki secimi olcunun
   ayirt edemedigi bir bolgede duruyor demektir, ve ayirt edilemeyen bir eksende
   dallanan kol tasinmaz.

### Kosum kosullari (olcumden once sabit)

- Kodlayici: `libx264` (`FfmpegArguments.cs:179`'daki pinli taramayla ayni
  kodlayici), iki pass, `-preset medium`, sabit `-threads 8`.
- `-g` degerleri: 2, 5, 10, 15, 20 saniye karsiliklari (kaynagin kendi fps'i ile
  kareye cevrilir). `-keyint_min` uretim koduyla ayni kural: `round(fps * 1,0)`.
- Dort kaynak, her biri 20 sn: kesik cok / durgun / hareketli / yuksek cozunurluklu.
- VMAF-NEG (`vmaf_v0.6.1neg`), tam klip, kaynak cozunurlugunde.
- Ek olarak `libsvtav1` preset 4 ile tek kaynakta isaret dogrulamasi yapilir;
  bu ana izgaranin karari degildir, yalniz kodlayici bagimliligini isaretler.
