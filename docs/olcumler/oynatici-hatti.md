# Oynatici hatti olcumu — kendi borumuz mu, LibVLC mi

T167. Duzenek: `tools/VidShrink.PlayerProbe`. Bu bir olcum belgesidir, urun kodu
degistirmez (K6).

## Olcum saglayicisi — hangi sayi hangi turda uretildi

Bu belgenin K1 ve K3 sayilari **tur 3'te bastan olculdu**; tur 2'nin K1-vlc olcusu
gosterilen kareye degil LibVLC'nin periyodik saat raporuna baglanmisti ve K3-own
olcusu uretimin pacing'ini kurmadan kosuyordu. Ikisi de duzeltildi, sonuclar degisti.

| bolum | kaynak |
|---|---|
| K1 (own, vlc, taban) | tur 3'te yeniden olculdu, yeni yontem |
| K2 (paket boyutlari) | tur 2, bagimsiz denetimde yeniden uretildi |
| K3 (senkron) | tur 3'te yeniden olculdu, own tarafi `-re` ile |
| K4 taban satiri | tur 3'te yeniden olculdu (228 dosya / 100.835.627 bayt) |
| K4 delta satirlari | K2'nin deltalariyla toplanarak turetildi (asagida acik) |

## Onemli sapma — kaynakta ses yok

Sozlesme `parca-1.mkv` icin "88 MB, ses var" diyor. `ffprobe` ile dogrulandi:

```
$ ffprobe -show_streams .calisma/kaynak/parca-1.mkv
[STREAM] index=0 codec_type=video codec_name=hevc ...
(baska stream yok)
```

`parca-1.mkv` (92.577.316 bayt) ve `genis-3-hareket.mkv` (47.594.016 bayt) **ikisi de
tek akisli, yalniz video**. Ses akisi yok, yani K3'un varsaydigi kosul kaynakta yok.

Bunu bildirip gecistirmek yerine K3'u **sentetik bir klip** uzerinde kurdum:
640x360, 30 fps, 130 sn, video karesine saniye sayaci basiliyor (`drawtext`), ses her
saniyenin ilk 50 ms'sinde 1 kHz bip (`aeval`). Ureten komut:

```
VidShrink.PlayerProbe.exe gen-sync-clip <cikis.mkv> 130
```

Zorunlu kaynaklar **okundu, yazilmadi, silinmedi**; yalniz K1'de kullanildi (K1 ses
gerektirmiyor). K3'un sayilari bu sentetik klibe aittir, gercek footage'a degil.

## K1 — Sar gecikmesi (seek'ten ilk dogru karenin GORUNMESINE kadar)

### Yontem — iki sutun ayni olayi olcuyor

Onceki turun kusuru buydu: own sutunu bir kare olayini, vlc sutunu LibVLC'nin ~250 ms
periyotlu saat raporunu olcuyordu. Tur 3'te iki sutun da **gosterilen karenin kendi
icerigine** baglandi.

- **own**: `-ss <hedef> -i <kaynak> -frames:v 1 -f rawvideo -pix_fmt bgra -`. Kronometre
  hedef karenin butun baytlari okunduğunda durur (`Program.cs`, `K1Own`).
- **vlc**: `SetVideoFormat("BGRA", w, h, pitch)` + `SetVideoCallbacks`. Sar isteginden
  once ffmpeg hedefin etrafindaki 3 saniyelik pencereyi (hedeften onceki 45 kare,
  hedeften sonraki 45 kare) ayrik karelere acar; her kare 32x18'lik yesil-kanal parmak
  izine cevrilip ortalamasi cikarilip birim uzunluga normallenir. `Display` geri
  cagrisinda ekrana gelen karenin parmak izi ayni sekilde uretilir; **"sonra" kumesiyle
  korelasyonu hem 0,90 esigini asip hem de "once" kumesiyle korelasyonunu gecerse**
  kronometre durur. Bu, saat okumasi degil, dogru karenin ekranda oldugunun tespitidir.
- **Bayat kare korumasi**: sar oncesi ekranda duran karenin parmak izi saklanir; ona
  0,9995'ten fazla benzeyen kare artik-cizim sayilip atlanir. Ham ciktida `bayat=`
  sutunu bu korumanin kac kez calistigini soyler.

Her aralik icin 5 tekrar, tekrarlarin hedefleri iki yolda da ayni tohumdan
(`new Random(42)`) uretildi.

`parca-1.mkv` 60,399 sn; 60 ve 300 sn araliklari icin cok kisa kaliyor. O iki satir
`-stream_loop 9 -c copy` ile uretilen 603,99 sn'lik turev uzerinde olculdu (kopya
kodek, kaynak degismedi).

| yol | aralik | ortanca ms | en kotu ms | kaynak |
|---|---|---|---|---|
| own (ffmpeg pipe) | 1 sn | 167,9 | 212,6 | parca-1.mkv |
| own (ffmpeg pipe) | 10 sn | 166,6 | 215,0 | parca-1.mkv |
| own (ffmpeg pipe) | 60 sn | 192,7 | 819,2 | parca-1-uzun.mkv |
| own (ffmpeg pipe) | 300 sn | 142,3 | 181,0 | parca-1-uzun.mkv |
| LibVLCSharp | 1 sn | 57,2 | 68,4 | parca-1.mkv |
| LibVLCSharp | 10 sn | 48,2 | 67,9 | parca-1.mkv |
| LibVLCSharp | 60 sn | 54,5 | 84,5 | parca-1-uzun.mkv |
| LibVLCSharp | 300 sn | 38,9 | 51,2 | parca-1-uzun.mkv |

Ham cikti (own, `k1-own`): 20 olcum satiri (4 aralik x 5 tekrar) + 4 GRID ozeti.

```
own	1	0	140,2	bayt=8294400
own	1	1	194,7	bayt=8294400
own	1	2	212,6	bayt=8294400
own	1	3	167,9	bayt=8294400
own	1	4	164,8	bayt=8294400
own	GRID	1	medyan_ms=167,9	en_kotu_ms=212,6	n=5
own	10	0	135,2	bayt=8294400
own	10	1	215,0	bayt=8294400
own	10	2	191,5	bayt=8294400
own	10	3	156,0	bayt=8294400
own	10	4	166,6	bayt=8294400
own	GRID	10	medyan_ms=166,6	en_kotu_ms=215,0	n=5
own	60	0	192,7	bayt=8294400
own	60	1	160,9	bayt=8294400
own	60	2	220,8	bayt=8294400
own	60	3	159,9	bayt=8294400
own	60	4	819,2	bayt=8294400
own	GRID	60	medyan_ms=192,7	en_kotu_ms=819,2	n=5
own	300	0	142,3	bayt=8294400
own	300	1	181,0	bayt=8294400
own	300	2	158,8	bayt=8294400
own	300	3	131,1	bayt=8294400
own	300	4	133,6	bayt=8294400
own	GRID	300	medyan_ms=142,3	en_kotu_ms=181,0	n=5
```

Ham cikti (vlc, `k1-vlc`): 20 olcum satiri (4 aralik x 5 tekrar) + 4 GRID ozeti. `kare=` sar isteginden sonra
degerlendirilen kare sayisi, `bayat=` atlanan artik-cizim sayisi, `r_sonra`/`r_once`
duran karenin iki referans kumesiyle korelasyonu:

```
vlc	1	0	47,9	istenen=40016	kare=2	bayat=0	r_sonra=0,9997	r_once=0,9967
vlc	1	1	68,4	istenen=9228	kare=1	bayat=0	r_sonra=0,9987	r_once=0,5553
vlc	1	2	57,2	istenen=8330	kare=1	bayat=0	r_sonra=0,9986	r_once=0,9926
vlc	1	3	34,5	istenen=31528	kare=2	bayat=1	r_sonra=0,9998	r_once=0,9933
vlc	1	4	59,2	istenen=10836	kare=1	bayat=0	r_sonra=0,9995	r_once=0,9062
vlc	GRID	1	medyan_ms=57,2	en_kotu_ms=68,4	n=5
vlc	10	0	25,9	istenen=43003	kare=1	bayat=0	r_sonra=0,9997	r_once=0,9977
vlc	10	1	67,9	istenen=16960	kare=1	bayat=0	r_sonra=0,9978	r_once=0,9940
vlc	10	2	45,8	istenen=16200	kare=1	bayat=0	r_sonra=0,9991	r_once=0,8736
vlc	10	3	57,6	istenen=35824	kare=1	bayat=0	r_sonra=0,9998	r_once=0,9959
vlc	10	4	48,2	istenen=18320	kare=1	bayat=0	r_sonra=0,9997	r_once=0,9982
vlc	GRID	10	medyan_ms=48,2	en_kotu_ms=67,9	n=5
vlc	60	0	61,6	istenen=422775	kare=2	bayat=0	r_sonra=0,9996	r_once=0,9984
vlc	60	1	41,5	istenen=136511	kare=1	bayat=0	r_sonra=0,9991	r_once=0,8135
vlc	60	2	84,5	istenen=128155	kare=1	bayat=0	r_sonra=0,9987	r_once=0,7405
vlc	60	3	54,5	istenen=343855	kare=1	bayat=0	r_sonra=0,9997	r_once=0,9978
vlc	60	4	38,6	istenen=151458	kare=1	bayat=0	r_sonra=0,9998	r_once=0,9919
vlc	GRID	60	medyan_ms=54,5	en_kotu_ms=84,5	n=5
vlc	300	0	38,9	istenen=502429	kare=1	bayat=0	r_sonra=0,9998	r_once=0,9982
vlc	300	1	51,2	istenen=342693	kare=2	bayat=0	r_sonra=0,9997	r_once=0,9972
vlc	300	2	46,0	istenen=338030	kare=2	bayat=0	r_sonra=0,9998	r_once=0,9983
vlc	300	3	28,5	istenen=458392	kare=1	bayat=0	r_sonra=0,9998	r_once=0,9967
vlc	300	4	30,2	istenen=351033	kare=1	bayat=0	r_sonra=0,9997	r_once=0,9945
vlc	GRID	300	medyan_ms=38,9	en_kotu_ms=51,2	n=5
```

### K1 taban — own hattinin surec baslatma bedeli

own kolu her sar isteginde yeni bir ffmpeg sureci aciyor. O bedeli izgaraya haksiz
yazmamak icin ayri olculdu: kaynak yok, kodcozme yok, yalniz surecin acilip tek 16x16
kare uretip kapanmasi (`k1-taban`).

```
taban	0	0	61,4	bayt=1024
taban	0	1	50,7	bayt=1024
taban	0	2	46,6	bayt=1024
taban	0	3	50,8	bayt=1024
taban	0	4	48,6	bayt=1024
taban	0	5	52,6	bayt=1024
taban	0	6	51,6	bayt=1024
taban	0	7	51,1	bayt=1024
taban	0	8	53,8	bayt=1024
taban	0	9	55,8	bayt=1024
taban	GRID	0	medyan_ms=51,6	en_kotu_ms=61,4	n=10
```

### Okuma — sayilarla

- **LibVLC dort aralikta da daha hizli ortanca veriyor** (4/4): 57,2 < 167,9;
  48,2 < 166,6; 54,5 < 192,7; 38,9 < 142,3. Oranlar sirasiyla 2,94 / 3,46 / 3,54 / 3,66
  kat.
- **LibVLC dort aralikta da daha iyi en kotu deger veriyor** (4/4): 68,4 < 212,6;
  67,9 < 215,0; 84,5 < 819,2; 51,2 < 181,0.
- own'un ortancasindan 51,6 ms'lik surec baslatma tabani dusulse bile LibVLC dort
  aralikta da onde kalir: 2,03 / 2,39 / 2,59 / 2,33 kat. Yani fark yalniz surec
  baslatmadan gelmiyor; **kalici bir kodcozucu boru kurulsa da LibVLC iki katin uzerinde
  onde**.
- own'un 700 ms'i asan tek orneklemi var: 20 orneklemden **1 tanesi** (60 sn araligi,
  4 numarali tekrar, 819,2 ms). Diger uc aralikta boyle bir sicrama yok.
- LibVLC 20 orneklemin **hepsinde** (20/20) sar isteginden sonra degerlendirilen 1. veya
  2. karede dogru kareyi gosterdi: 15 orneklemde `kare=1`, 5 orneklemde `kare=2`. Bir
  orneklemde (1 sn / 3) o iki kareden biri sar oncesinden kalan artik cizimdi ve bayat
  korumasi onu atladi (`bayat=1`); diger 19'unda `bayat=0`.

## K2 — Ses cikisi: aday, boyut, native yuk

Aday own-hat: **NAudio** (surum 2.2.1 — 3.0.1 net9.0 istiyor, bu depo net8.0, onceki
surum secildi). Meta paket 6 alt paketi cekiyor: Core, Wasapi, WinMM, Midi, Asio,
WinForms.

Aday LibVLC-hat: **LibVLCSharp** 3.10.1 + **VideoLAN.LibVLC.Windows** 3.0.23.1
(native libvlc + eklentiler).

NuGet onbellek boyutu (gelistirme makinesi, sevkiyat degil):

| paket | onbellek bayt |
|---|---|
| naudio | 237.763 |
| naudio.asio | 135.021 |
| naudio.core | 673.786 |
| naudio.midi | 176.684 |
| naudio.wasapi | 1.665.135 |
| naudio.winforms | 204.811 |
| naudio.winmm | 239.908 |
| **NAudio toplam** | **3.333.108** |
| libvlcsharp | 42.382.352 |
| videolan.libvlc.windows | 726.867.021 |

`videolan.libvlc.windows` onbellegi win-x86+win-x64+win-arm64+tum TFM'leri birden
tasiyor; bu **sevkiyat** boyutu degil, K4'te sevkiyat olculuyor.

Sevkiyat deltasi — `VidShrink.PlayerProbe` uzerinde, her aday tek basina referansken
`dotnet publish -c Release -r win-x64 --self-contained false`:

| aday | dosya | bayt |
|---|---|---|
| taban (paketsiz `VidShrink.PlayerProbe`) | 9 | 887.968 |
| NAudio | +6 | +518.848 |
| LibVLCSharp+VideoLAN.LibVLC.Windows (RID trim yok, 3 mimari) | +1.263 | +293.052.266 |
| LibVLCSharp+VideoLAN.LibVLC.Windows (yalniz `libvlc/win-x64/` tutulsa) | +426 | +105.774.201 (native) + 230.910 (yonetilen dll) |

Native yuk NAudio'da **yok** (tamami yonetilen kod, WASAPI COM interop P/Invoke ile);
LibVLC'de paket varsayilani **294 MB** (3 mimari birden kopyalaniyor). Elle RID
filtreleme (yalniz win-x64 klasorunu tutmak) **denenmedi/dogrulanmadi**; asagidaki
197 MB satiri bu yuzden bir olcum degil, klasor boyutundan turetilmis bir tahmindir.

## K3 — Ses/goruntu senkronu (sentetik klip, 125 sn)

**Kaynak uyarisi yukarida**: bu bolum sentetik klip uzerinde, zorunlu kaynaklar
uzerinde degil.

### Yontem duzeltmesi — own kolu artik uretimin yaptigini yapiyor

Tur 2'nin `k3-own`'u video borusunu `-re` olmadan, siki dongude okuyordu ve ciktisini
"own hat'ta scheduler yok" diye yorumluyordu. **Bu iddia yanlisti.** Uretim kodunda
kaynak tarafi pacing var:

```
src/VidShrink.App/Playback/PanelHost.cs:460         Realtime = true
src/VidShrink.Ffmpeg/Playback/ComparisonGraph.cs:87 if (request.Realtime) args.Add("-re")
src/VidShrink.App/Playback/PanelHost.cs:694-708     RequestAnimationFrame ile vsync'e bagli drain
src/VidShrink.App/Playback/PanelHost.cs:902-914     TakeNewestReady, CatchUpCeiling ile bayat kare dusurme
```

Probe artik uretim gibi `-re` ile akiyor. Uretimin **ikinci** pacing katmani (vsync
drain ve bayat kare dusurme) probe'ta kurulmadi ve **olculmedi**; asagidaki sayilar
yalniz kaynak tarafi pacing'in sonucudur.

### Iki yolun sonucu

Sifir noktasi olarak ilk audio callback'in pts'i alindigi icin ilk orneklem tanimi
geregi drift=0 verir; iki kolda da atiliyor.

| yol | baslangic kayma ms | son kayma ms | 123 sn'de degisim |
|---|---|---|---|
| own (ffmpeg `-re` + NAudio WASAPI) | −1.645,6 | −1.647,5 | 1,9 ms |
| LibVLCSharp (ham callback) | −1.107,4 | −1.109,5 | 2,1 ms |

Ham cikti:

```
own	sync	ornek=3701	baslangic_ms=-1645,6	son_ms=-1647,5
own	sync-ham	wall=1,69	videoPts=0,033	audioPts=1,675	drift_ms=-1641,4
own	sync-ham	wall=1,72	videoPts=0,067	audioPts=1,706	drift_ms=-1639,2
own	sync-ham	wall=1,77	videoPts=0,100	audioPts=1,752	drift_ms=-1651,7
own	sync-ham	wall=124,96	videoPts=123,300	audioPts=124,947	drift_ms=-1646,6
own	sync-ham	wall=124,99	videoPts=123,333	audioPts=124,979	drift_ms=-1645,7
own	sync-ham	wall=125,02	videoPts=123,367	audioPts=125,010	drift_ms=-1643,7
vlc	sync	ornek=3696	baslangic_ms=-1107,4	son_ms=-1109,5
vlc	sync-ham	wall=1,83	videoPts=0,033	audioPts=1,115	drift_ms=-1081,7
vlc	sync-ham	wall=1,87	videoPts=0,067	audioPts=1,184	drift_ms=-1117,3
vlc	sync-ham	wall=1,90	videoPts=0,100	audioPts=1,207	drift_ms=-1107,0
vlc	sync-ham	wall=124,94	videoPts=123,133	audioPts=124,250	drift_ms=-1116,7
vlc	sync-ham	wall=124,97	videoPts=123,167	audioPts=124,273	drift_ms=-1106,3
vlc	sync-ham	wall=125,01	videoPts=123,200	audioPts=124,320	drift_ms=-1120,0
```

Okuma: **iki yolda da kayma birikmiyor** — 123 saniyede own 1,9 ms, LibVLC 2,1 ms
degisiyor. Iki koldaki sabit ofset (own −1,65 sn, vlc −1,11 sn) bu olcum duzeneginin
kendi artefaktidir, hattin ozelligi degil: ses borusu video dongusunden once dolmaya
basliyor. **Kaniti**: ayni receteyle iki ayri own kosumu:

```
own	sync	ornek=3701	baslangic_ms=-1645,6	son_ms=-1647,5
own	sync	ornek=3734	baslangic_ms=-529,4	son_ms=-530,2
```

Sabit ofset kosumdan kosuma 1,1 saniye oynuyor, oysa **birikmeme bulgusu iki kosumda
da ayni**: 1,9 ms ve 0,8 ms. Ofsetin degeri bir sonuc degil, birikmemesi sonuc.

Bu bolumun **olcmedigi** sey: gercek hoparlor/ekran cikisinda iki yolun ofseti nasil
oturuyor. Iki kol da ham callback uzerinden olculdu.

## K4 — Kurulum bedeli

Taban satiri olculdu:

```
$ dotnet publish src/VidShrink.App/VidShrink.App.csproj -c Release -r win-x64 --self-contained true -o <dizin>
$ (Get-ChildItem <dizin> -Recurse -File | Measure-Object Length -Sum)
dosya=228   bayt=100.835.627
```

Delta satirlari **olculmedi, turetildi**: taban + K2'nin ilgili deltasi. Deltalar
paketin sevkiyata ekledigi dosyalardir ve self-contained olup olmamasindan bagimsizdir.

| satir | dosya | bayt | MiB | nasil |
|---|---|---|---|---|
| bugun (oynaticisiz) | 228 | 100.835.627 | 96,16 | olculdu |
| own hat (+ NAudio) | 234 | 101.354.475 | 96,66 | taban + 6 dosya / 518.848 B |
| LibVLC hat (paket varsayilani, 3 mimari) | 1.491 | 393.887.893 | 375,64 | taban + 1.263 dosya / 293.052.266 B |
| LibVLC hat (yalniz win-x64 tutulsa, **dogrulanmamis**) | 654 | 206.840.738 | 197,26 | taban + 426 dosya / 106.005.111 B |

MiB = bayt / 1.048.576. own hat, LibVLC'nin en iyi (dogrulanmamis) haline gore
**2,04 kat**, paket varsayilanina gore **3,89 kat** kucuk.

Build nondeterminizmi: ayni recete tur 2'de 100.835.623, bagimsiz denetimde
100.835.035, tur 3'te 100.835.627 bayt verdi — yayilim ~600 bayt, tablodaki MiB
degerlerini degistirmiyor.

## K5 — Tavsiye

**Own hat (ffmpeg pipe + NAudio) onerilir** — ama tur 2'nin gerekcesiyle degil.

Sutun sutun, duzeltilmis sayilarla:

| sutun | kazanan | ne kadar |
|---|---|---|
| K1 sar gecikmesi | **LibVLC** | 4/4 aralikta 2,9-3,7 kat hizli ortanca; surec tabani dusulse bile 2,0-2,6 kat |
| K2 ses bedeli | **own** | 518.848 B yonetilen kod / 106-293 MB native |
| K3 senkron birikmesi | **berabere** | own 1,9 ms, LibVLC 2,1 ms (123 sn) |
| K4 kurulum boyutu | **own** | 96,66 MB / 234 dosya; LibVLC 197-376 MB / 654-1.491 dosya |

Sayim: dort sutunun **biri** LibVLC'nin, **ikisi** own hattin, **biri** berabere.

Danisma turunun kosulu — *"LibVLCSharp'i ancak ses ve sar gecikmesi olcumde basarisiz
cikarsa ac"* — **sar gecikmesi sutununda tetikleniyor**, tur 2'nin sandigi gibi
tetiklenmiyor degil. LibVLC K1'de aciktir. Karar bu yuzden "own hat her sutunda
kazaniyor" degil, bir tercih:

- own hattin sar gecikmesi ortancasi 142-193 ms. Tekerlekle art arda atlanan bir
  oynatici icin bu **kullanilabilir ama LibVLC kadar akici degil**; kullanicinin
  istedigi 1/10/60/300 sn adimlarinda her adim 142-193 ms bekletir.
- Bunun karsiligi K2+K4: 106-293 MB native yuk ve depodaki **ilk** harici oynatici
  bagimliligi. LibVLC'nin ortancada kazandigi 103-138 ms (167,9−57,2 / 166,6−48,2 /
  192,7−54,5 / 142,3−38,9) icin odenen bedel, kurulum boyutunun 2,04-3,89 katina
  cikmasi.
- K3 artik bir ayrim sutunu degil: `-re` eklendiginde own hat da kaymayi biriktirmiyor.
  Tur 2'nin "own hat'ta scheduler yok" iddiasi geri cekildi — uretimde `-re`, vsync
  drain ve bayat kare dusurme zaten var.

**Sonuc**: own hat'i genislet, ses icin NAudio'yu ekle. Sar gecikmesi kabul edilebilir
sinirdadir ama **serbest degildir** — sonraki sozlesme, sar basina yeni ffmpeg sureci
acmayi birakip kalici bir kodcozucu boru uzerinden sar yapmali; K1 tabani bunun 51,6
ms'lik bir kazanc oldugunu soyluyor ve kalan 91-141 ms'i de dusurmek gerekir.

**Bu karar geri alinabilir olmali.** Sar gecikmesi urunde 150 ms'in altina inmezse
LibVLC'yi acmak, K1'in sayilariyla **mesru** bir secenektir; bu belge onu kapatmiyor.

## K6 — Urun koduna dokunulmadi

```
$ git diff --stat main -- src/
(bos)
```

`src/` altinda **0 dosya** degisti. Bu turun dokundugu iki dosya:
`tools/VidShrink.PlayerProbe/Program.cs` ve bu belge.
