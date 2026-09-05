# Oynatici hatti olcumu — kendi borumuz mu, LibVLC mi

T167. Duzenek: `tools/VidShrink.PlayerProbe`. Bu bir olcum belgesidir, urun kodu
degistirmez (K6).

## Onemli sapma — kaynakta ses yok

Sozlesme `parca-1.mkv` icin "88 MB, ses var" diyor. `ffprobe` ile dogrulandi:

```
$ ffprobe -show_streams .calisma/kaynak/parca-1.mkv
[STREAM] index=0 codec_type=video codec_name=hevc ...
(baska stream yok)
```

`parca-1.mkv` (92.577.316 bayt, iddia edilen 88 MB'a yakin — boyut dogru) ve
`genis-3-hareket.mkv` (47.594.016 bayt) **ikisi de tek akisli, yalniz video**. Ses
akisi yok. Bu, sozlesmenin K3 icin varsaydigi kosulu geciyor: mandatory kaynaklarla
ses/goruntu senkronu **olculemez** — sifir ses demek karsilastirilacak ikinci eksen yok
demek.

Bunu bildirip gecistirmek yerine K3'u **sentetik bir klip** uzerinde kurdum:
`.calisma/T167/senkron-klip.mkv` — 640x360, 30 fps, 130 sn, video karesine saniye
sayaci basiliyor (`drawtext`), ses her saniyenin ilk 50 ms'sinde 1 kHz bip
(`aeval`). Mandatory kaynaklar **okunmadi/degistirilmedi**, yalniz K1 icin kullanildi
(K1 ses gerektirmiyor). K3'un sayilari bu sentetik klibe aittir, gercek footage'a
degil — asagida her yerde acikca isaretli.

## K1 — Sar gecikmesi (seek'ten ilk dogru kareye kadar)

`parca-1.mkv` 60,399 sn (1 ve 10 sn araliklari icin yeterli). 60 ve 300 sn araliklari
icin kaynak cok kisa kaliyor (`baseT+aralik` dosya sonunu asiyor, olcum "dosya sonuna
sar" olurdu, istenen "N sn ileri sar" degil) — bu yuzden 60 ve 300 sn satirlari,
`parca-1.mkv`'nin `-stream_loop 9 -c copy` ile uretilen 603,99 sn'lik turevi
`.calisma/T167/parca-1-uzun.mkv` uzerinde olculdu (kopya kodek, kaynak degismedi).
Her aralik icin 5 tekrar, ortanca ve en kotu deger.

| yol | aralik | ortanca ms | en kotu ms | kaynak |
|---|---|---|---|---|
| own (ffmpeg pipe) | 1 sn | 187,8 | 754,5 | parca-1.mkv |
| own (ffmpeg pipe) | 10 sn | 169,0 | 835,6 | parca-1.mkv |
| own (ffmpeg pipe) | 60 sn | 159,3 | 238,2 | parca-1-uzun.mkv |
| own (ffmpeg pipe) | 300 sn | 146,2 | 813,3 | parca-1-uzun.mkv |
| LibVLCSharp | 1 sn | 266,2 | 274,4 | parca-1.mkv |
| LibVLCSharp | 10 sn | 257,1 | 277,7 | parca-1.mkv |
| LibVLCSharp | 60 sn | 269,6 | 272,0 | parca-1-uzun.mkv |
| LibVLCSharp | 300 sn | 261,4 | 272,0 | parca-1-uzun.mkv |

Ham cikti (own, `k1-own`, 20 satir = 4 aralik x 5 tekrar):

```
own	1	0	145,5	bayt=8294400
own	1	1	187,8	bayt=8294400
own	1	2	211,5	bayt=8294400
own	1	3	754,5	bayt=8294400
own	1	4	169,3	bayt=8294400
own	10	0	139,2	bayt=8294400
own	10	1	185,6	bayt=8294400
own	10	2	169,0	bayt=8294400
own	10	3	835,6	bayt=8294400
own	10	4	151,9	bayt=8294400
own	60	0	238,2	bayt=8294400
own	60	1	147,7	bayt=8294400
own	60	2	201,8	bayt=8294400
own	60	3	159,3	bayt=8294400
own	60	4	135,7	bayt=8294400
own	300	0	146,2	bayt=8294400
own	300	1	149,5	bayt=8294400
own	300	2	132,9	bayt=8294400
own	300	3	813,3	bayt=8294400
own	300	4	125,5	bayt=8294400
```

Ham cikti (vlc, `k1-vlc`, 20 satir = 4 aralik x 5 tekrar):

```
vlc	1	0	257,6	istenen=40016	gozlenen=40233
vlc	1	1	253,0	istenen=9228	gozlenen=9402
vlc	1	2	270,8	istenen=8330	gozlenen=8537
vlc	1	3	266,2	istenen=31528	gozlenen=31737
vlc	1	4	274,4	istenen=10836	gozlenen=11053
vlc	10	0	253,4	istenen=43003	gozlenen=43202
vlc	10	1	262,9	istenen=16960	gozlenen=17137
vlc	10	2	277,7	istenen=16200	gozlenen=16421
vlc	10	3	256,1	istenen=35824	gozlenen=36018
vlc	10	4	257,1	istenen=18320	gozlenen=18517
vlc	60	0	270,0	istenen=422775	gozlenen=422979
vlc	60	1	265,4	istenen=136511	gozlenen=136714
vlc	60	2	261,2	istenen=128155	gozlenen=128350
vlc	60	3	272,0	istenen=343855	gozlenen=344064
vlc	60	4	269,6	istenen=151458	gozlenen=151666
vlc	300	0	252,4	istenen=502429	gozlenen=502626
vlc	300	1	257,3	istenen=342693	gozlenen=342879
vlc	300	2	261,4	istenen=338030	gozlenen=338229
vlc	300	3	266,3	istenen=458392	gozlenen=458596
vlc	300	4	272,0	istenen=351033	gozlenen=351245
```

Okuma: **own hat 4 aralikta da daha hizli medyan** veriyor (own 140-190 ms, vlc
250-270 ms) — vlc'nin sabit ~250 ms tabani surec/decoder isinma maliyeti. own'un
en kotu degeri 5 tekrarin 1'inde (her aralikta tam olarak 1 tekrar) 750-850 ms'e
sicriyor — muhtemelen ffmpeg surec baslatma isinmasi (ilk seek'te I-frame'e kadar
demux). vlc'nin en kotusu medyanina cok yakin (fark 5-20 ms) — kalici surec, isinma
maliyeti yok.

## K2 — Ses cikisi: aday, boyut, native yuk

Aday own-hat: **NAudio** (surum 2.2.1 — 3.0.1 net9.0 istiyor, bu depo net8.0,
onceki surum secildi). Meta paket 6 alt paketi cekiyor: Core, Wasapi, WinMM, Midi,
Asio, WinForms.

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

`dotnet publish -c Release -r win-x64 --self-contained false` deltasi:

| aday | dosya | bayt |
|---|---|---|
| taban (paketsiz `VidShrink.PlayerProbe`) | 9 | 887.968 |
| NAudio | +6 | +518.848 |
| LibVLCSharp+VideoLAN.LibVLC.Windows (RID trim yok, 3 mimari) | +1.263 | +293.052.266 |
| LibVLCSharp+VideoLAN.LibVLC.Windows (yalniz `libvlc/win-x64/` tutulsa) | +426 | +105.774.201 (native) + 230.910 (yonetilen dll) |

Native yuk NAudio'da **yok** (tamami yonetilen kod, WASAPI COM interop P/Invoke ile);
LibVLC'de paket varsayilani **294 MB** (3 mimari birden kopyalaniyor), elle RID
filtreleme (yalniz win-x64 klasorunu tutmak) denenmedi/dogrulanmadi ama klasor
boyutu olculdu — teorik taban ~106 MB native.

## K3 — Ses/goruntu senkronu (sentetik klip, 125 sn)

**Kaynak uyarisi yukarida**: bu bolum `senkron-klip.mkv` (sentetik, ses var) uzerinde,
mandatory kaynaklar uzerinde degil.

### own hat

`k3-own`, video pipe (rawvideo bgra) + audio pipe (`NAudio.Wave.WasapiOut` +
`BufferedWaveProvider`) ile en kucuk calisan ornek kuruldu. Sonuc: **olculebilir bir
sayi yok, cunku own hat'ta hicbir es zamanlama (scheduler) yok** — video pipe'i
ffmpeg'in verebildigi hizda (CPU sinirli, gercek zamanin cok onunde) okuyor, audio
WASAPI'de gercek zamanda caliyor. 2 saniyelik duvar saatinde video pts 130 sn'e
(dosyanin sonuna) ulasti, audio pts 1,94 sn'deydi:

```
own	sync-ham	wall=0,55	videoPts=0,000	audioPts=0,533	drift_ms=-532,8
own	sync-ham	wall=0,55	videoPts=0,033	audioPts=0,534	drift_ms=-500,5
own	sync-ham	wall=0,55	videoPts=0,067	audioPts=0,534	drift_ms=-467,6
own	sync-ham	wall=1,95	videoPts=129,900	audioPts=1,942	drift_ms=127957,9
own	sync-ham	wall=1,95	videoPts=129,933	audioPts=1,942	drift_ms=127990,9
own	sync-ham	wall=1,96	videoPts=129,967	audioPts=1,943	drift_ms=128023,9
```

Bu 128 saniyelik "kayma" gercek bir senkron kusuru degil, **hic kurulmamis bir
bilesenin** (kare hizini gercek zamana bagliyacak scheduler) kanitidir — danisma
turunun "ses hic yok" tespitiyle birebir orusuyor, simdi olcumle dogrulandi: ses de
eklense, video pipe'i frenlemeden calan bir "en kucuk ornek" is gormez. Calisan
ornek `NAudio.GetPosition()`in dogru bir audio-saat kaynagi verdigini gosterdi (audio
pts wall-clock ile 1:1 buyudu, 0,55→1,95 sn duvar saatinde 0,53→1,94 sn audio) —
gelecek sozleşmenin video tarafini bu saate gore hizlandirmasi/yavaslatmasi gerekiyor.

### LibVLC hat

`k3-vlc`, `SetVideoCallbacks`/`SetAudioCallbacks` ile ham donus (native cikis
kapali) uzerinden video-goster ve audio-oynat olaylarini zaman damgaliyor. Audio
`pts`'i `vlc_tick_now()` mutlak saatinde geldigi icin ilk gozlenen deger sifir
noktasi alinip video ekseniyle (medya basi = 0) hizalandi.

```
vlc	sync-ham	wall=1,82	videoPts=0,000	audioPts=0,000	drift_ms=0,0
vlc	sync-ham	wall=1,82	videoPts=0,033	audioPts=1,115	drift_ms=-1081,7
vlc	sync-ham	wall=1,86	videoPts=0,067	audioPts=1,184	drift_ms=-1117,3
vlc	sync-ham	wall=124,93	videoPts=123,133	audioPts=124,250	drift_ms=-1116,7
vlc	sync-ham	wall=124,96	videoPts=123,167	audioPts=124,273	drift_ms=-1106,3
vlc	sync-ham	wall=125,00	videoPts=123,200	audioPts=124,320	drift_ms=-1120,0
```

Baslangicta (ilk 2 sn ortalamasi) −948,1 ms, sonda (son 2 sn ortalamasi) −1.109,5 ms
— 123 saniyelik oynatmada kayma **38 ms buyudu**, yani birikmiyor (LibVLC'nin ic
saati sabit tutuyor). Sabit ~1,1 sn'lik ofset muhtemelen bu olcum yonteminin kendi
artefakti: native cikisi kapatip ham callback'e gectigimizde audio'nun decode/kuyruk
sirasi video'dan once baslayabiliyor; gercek hoparlor/ekran cikisinda LibVLC'nin
kendi av-sync mekanizmasi bu ofseti telafi ediyor olabilir — bu belge bunu iddia
etmiyor, yalniz olculebilen 38 ms'lik **birikmeme** bulgusunu net veriyor.

**Ozet:** own hat icin bu olcum "olculemedi" degil, "olculdu ve sonuc: scheduler
yok" — LibVLC icin "olculdu ve sonuc: kayma sabit, birikmiyor".

## K4 — Kurulum bedeli (`dotnet publish -c Release -r win-x64 --self-contained`)

| satir | dosya | MB |
|---|---|---|
| bugun (oynaticisiz) | 228 | 96,16 |
| own hat (+ NAudio) | 234 | 96,65 |
| LibVLC hat (paket varsayilani, 3 mimari) | 1.491 | 375,66 |
| LibVLC hat (yalniz win-x64 tutulsa, dogrulanmamis) | 654 | 197,26 |

Ham bayt: bugun 100.835.623; own 101.354.471; LibVLC (varsayilan) 393.887.889;
LibVLC (win-x64 sadece) 206.840.734.

`.calisma/T167/publish-today` (App'in kendisi), `.calisma/T167/publish-probe-none`,
`.calisma/T167/publish-probe-naudio`, `.calisma/T167/publish-probe-libvlc` bu
sayilarin ureten publish ciktilaridir (`.calisma/`, git'e girmez).

## K5 — Tavsiye

**Own hat (ffmpeg pipe + NAudio) onerilir.**

Dayanak, sutun sutun:

- **K1**: own hat 4 aralikta da daha hizli medyan (own 140-190 ms, vlc 250-270 ms).
  Danisma turunun kosulu ("LibVLC'yi ancak sar gecikmesi olcumde basarisiz cikarsa
  ac") burada **tetiklenmiyor** — own hat sar gecikmesinde kaybetmiyor, kazaniyor.
- **K4**: own hat 96,65 MB / 234 dosya; LibVLC en iyi ihtimalle (dogrulanmamis trim)
  197,26 MB / 654 dosya, trim'siz 375,66 MB / 1.491 dosya. Own hat 2,0-3,9 kat kucuk.
- **K2**: own hat'in ses bedeli 518.848 bayt, tamami yonetilen kod. LibVLC 105-293
  MB native yuk tasiyor, depoya **ilk** harici oynatici bagimliligini sokuyor.

LibVLC'nin ustun oldugu tek sutun **K3**: bugun gercekten calisan, olcculen
kaymasi birikmeyen bir av-sync motoru var. Own hat'ta bu motor **yok** — K3'un
kendi olcumu bunu kanitladi (128 saniyelik kontrolsuz kayma). Ama ayni olcum,
gerekli saat kaynaginin (`NAudio.GetPosition()`) calisir durumda oldugunu da
gosterdi; eksik olan, video pipe'ini bu saate gore hizlandirip/dusuren bir
pacing katmani — bu bir sonraki sozlesmenin isi, LibVLC'ye gecmeyi gerektiren
bir olcum basarisizligi degil.

**Sonuc**: own hat'i genislet (danismanin A karari), pacing katmanini kur; LibVLC'yi
K2'nin native-yuk maliyeti ve K4'un dosya/boyut farki karsisinda simdi acma.

## K6 — Urun koduna dokunulmadi

```
$ git diff --stat -- src/
(bos)
$ git status --short
?? tools/VidShrink.PlayerProbe/
```

`src/` altinda **0 dosya** degisti; `?? tools/VidShrink.PlayerProbe/` bu sozlesmenin
kendi yeni dizini. `docs/olcumler/oynatici-hatti.md` de yeni (bu dosya).
