Uc kalem var. **R1 uygulanabilir** — iki calisma noktasi da ayni yone bakiyor.
**R2 kapili** — tek calisma noktasinda esigi geciyor ve izgaranin ust ucu klip
uzunluguna carpiyor; kapiyi acan olcum yazili. **R3 donanim kolu**, kendi
izgarasindan cikiyor.

Her satirdaki sayi yukaridaki tablolarin bir hucresidir; recete kendi sayisini
uretmiyor.

### R1 — haritanin `-g` kolu kaldirilir, tavan tek degere sabitlenir

| ne | deger |
|---|---|
| dosya | `src/VidShrink.Core/FfmpegArguments.cs` |
| satir | 233-253 (`KeyframeCeilingSeconds(SceneMap?)` govdesi) |
| eski | `Math.Clamp(mappedMedianSeconds / SceneMapMergeFactor, KeyframeCeilingMinSeconds, KeyframeCeilingMaxSeconds)` |
| yeni | `KeyframeCeilingDefaultSeconds` (govde tek satira iner, `scenes` parametresi dusler) |
| birlikte olen sabitler | 218 `SceneMapThresholdOfRecord`, 219 `SceneMapGroundTruthCutsInWindow`, 220 `SceneMapMappedCutsInWindow`, 221 `SceneMapMergeFactor`, 222 `KeyframeCeilingMinSeconds`, 223 `KeyframeCeilingMaxSeconds` |
| birlikte olen dal | 263 `fromMap` her zaman `false` olur; `KeyframeRange.FromSceneMap` yalniz donanim/yazilim ayrimini tasir hale gelir |

**Dayanak.** Kolun butun erisim alani `[5,0 ; 10,0]`. Izgarada 5 sn ile 10 sn
arasindaki fark sekiz hucrenin sekizinde de esigin (0,20 p10) altinda kaliyor,
ve sekizin yedisinde **negatif**: es bit/piksel `+0,109 / -0,347 / -0,138 /
-0,190`, zorluk esitlenmis `-0,012 / -6,115 / -0,148 / -0,139` (K1 izgaralari,
`VMAF-NEG p10` kolonu; 10 sn hucresine gore). Tek pozitif hucre `s1-kesikli`
es bit/piksel, `+0,109` — esigin yarisindan az.

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

Isaret ikinci bir kodlayicida da ayni: `libsvtav1` preset 4'te 5 sn'nin 10 sn'ye
gore farki **-1,297 p10**; `av1_nvenc` p5'te **-1,421** (`s1-kesikli`) ve
**-1,424** (`s3-hareketli`) p10 (ikinci kodlayici ve donanim tablolari).

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

| olcu | yer |
|---|---|
| `Kiskacin_alt_ucu_bes_saniyede_bagliyor` | `FfmpegArgumentsTests.cs:763` |
| `Kiskacin_ust_ucu_on_saniyede_bagliyor` | `FfmpegArgumentsTests.cs:778` |
| `Duzeltme_olculen_pencerede_gercek_cekim_uzunlugunu_uretir` | `FfmpegArgumentsTests.cs:747` |
| harita/haritasiz `-g` cifti | `EncodeRunnerTests.cs:237-238`, `:382-383` |
| onizleme parcasinin haritali araligi | `PreviewSegmentTests.cs:270` |

### R2 — yazilim tavani 10 sn'nin ustune, **kapili**

| ne | deger |
|---|---|
| dosya | `src/VidShrink.Core/FfmpegArguments.cs` |
| satir | 217 |
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
(`FfmpegArgumentsTests.cs:812`) donanim tavaninin yazilim varsayilanindan
**kisa** oldugunu iddia ediyor. R2 ile R3 birlikte uygulanirsa 10,0 < 15,0
olarak dogru kalir; yalniz R3 uygulanirsa (10,0 vs 10,0) bu olcu kizarir.

### R3 — donanim tavani 5 sn'den 10 sn'ye

| ne | deger |
|---|---|
| dosya | `src/VidShrink.Core/FfmpegArguments.cs` |
| satir | 224 |
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
(`FfmpegArgumentsTests.cs:793`) yeniden temellendirilir: uzun sahneli harita
60 fps'te `-g 600` almali, 300 degil. **Kirmasi gereken mutasyon:** sabiti
5,0'da birakmak `-g 300` verir ve olcu kizarir.

**Bedeli yazili.** Donanimda tavani ikiye katlamak beklenen atlama mesafesini de
ikiye katlar (`av1_nvenc`de gerceklesen aralik = tavan, K2'nin yazilim
tablosundaki gibi icerige bagli degil). Bu bedel puanda gorunmez; uygulama
sozlesmesi kararini bu iki sayiya birlikte bakarak verir.

### Degistirilmeyecekler

- `KeyframeFloorSeconds = 1.0` (satir 216). Izgara alt ucu 2 sn; 2 sn'nin 10
  sn'ye gore farki sekiz hucrenin sekizinde de negatif ve buyuk
  (`-0,481` ile `-30,717` arasi). Kisa uc icin degisiklik onerilmiyor.
- Kiskacin **alt** ucunun 2 sn'ye indirilmesi. Ayni sayilar bunu dogrudan
  reddediyor.
