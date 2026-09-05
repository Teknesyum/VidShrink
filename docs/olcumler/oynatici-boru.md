# T175 - Kalici Kodcozucu Borusu Olcumleri (4. tur)

Makine: bu depo. Olcumler tools/VidShrink.PlaybackProbe/Program.cs ile
uretildi (1. tur .calisma/T175/olcum/Program.cs'in yerini aldi, ayni
sozlesme kapsamindaki tools/'a tasindi). Ham ciktilar
docs/olcumler/oynatici-boru/ altinda ve .calisma/T175/*.log icinde duruyor.

1. turda kok neden bulundu ve duzeltildi: src/VidShrink.Ffmpeg/Playback/DecoderPipe.cs
icindeki eski SeekAsync, ileri-yakalama mesafesini sinirlamiyordu (yer
tutucu "- 0" karsilastirmasi); uzak-ileri aramalarda boru uzun sure akisi
surdurmeye calisiyor, sabit ForwardWaitTimeout bunu yarida kesip yeniden
baslatiyor, bazen bu yeniden baslatmayi da bir sonraki yeniden baslatma
"kurtariyordu". Duzeltme: MaxForwardCatchupFrames (ileri-yakalamayi kac
anahtar kareyle sinirlar) ve MaxRestartAttemptsPerSeek (bir SeekAsync
cagrisinin en fazla kac kez yeniden baslatabilecegini sinirlayan devre
kesici) eklendi.

2. turda denetci K4'un yapisal olarak olcemedigini (videoPts audioPts'tan
turetiliyordu) ve K2'nin surec sayiminin video-only kaldigini buldu (3
KRITIK). 3. turde bunlar icin surekli oynatma yolu yazildi
(`DecoderPipe.StartContinuousPlayback`), K4 o yolla yeniden olculdu, K2
icin ayri bir olcum komutu (video+ses beraber tetiklenerek) yazildi ve
K2 kirmizi ilan edildi - asagida K2 ve K4 bolumleri.

## K1 - Taban cizgisi (eski: her aramada yeni ffmpeg sureci)

Degismedi, 1. turdan aynen tasindi. Uc kaynak, 30 arama, uniform (rastgele)
ve mixed (%70 ileri surukleme / %30 rastgele sicrama) modlari.

| Kaynak | Mod | Medyan (ms) | p95 (ms) | Maks (ms) |
|---|---|---|---|---|
| kisa (60,4 s) | uniform | 147,8 | 802,9 | 1805,7 |
| kisa (60,4 s) | mixed | 144,2 | 228,9 | 239,1 |
| orta (180 s) | uniform | 138,2 | 179,2 | 733,1 |
| orta (180 s) | mixed | 143,8 | 183,0 | 836,5 |
| uzun (600 s) | uniform | 146,1 | 179,2 | 226,3 |
| uzun (600 s) | mixed | 138,3 | 836,8 | 837,2 |

Ham diziler: .calisma/T175/k1-kisa.log, k1-orta.log, k1-uzun.log.

## K2 - Kalici boru: surec sayisi ve cokme kurtarmasi

1. turun KRITIK bulgusu: ozet cumlesi "30 aramanin hicbirinde 30 yeni
surec baslamiyor" diyordu ama kendi ham verisi orta ve uzun kaynaklarda
32 ve 36 surec gosteriyordu - naif tek-surec-per-arama tabanindan bile
kotu, ve ProcessesStarted sayaci SeekAudio'nun actigi ikinci ffmpeg
surecini saymiyordu (DecoderPipe.cs, SeekAudio metodu).

2. turda bu duzeltildi ama olcum hala yalniz video tarafiniydi: K3Async
ne AttachAudioSink ne SeekAudio cagiriyordu, yani "surec_baslatma" sayaci
sesi hic tetiklemiyordu (2. tur borc bulgusu 6). 3. turde bunun icin ayri
bir K2 komutu yazildi (`tools/VidShrink.PlaybackProbe/Program.cs`,
`K2Async`): once `AttachAudioSink` cagriliyor, sonra her arama hem
`SeekAsync` hem `SeekAudio` ile tetikleniyor, ikisi de ayni
`ProcessesStarted` sayacina yaziyor.

Ses tasiyan tek kaynagimiz `senkron-130s.mkv` (130 s, 1 kHz sine + testsrc,
diger uc kaynak parca-1.mkv'den turetildigi icin ses akisi yok - bkz.
docs/olcumler/oynatici-hatti.md). 30 aramalik dizi (video+ses beraber
tetiklenerek):

| Mod | n | surec_baslatma | ses_var |
|---|---|---|---|
| uniform (rastgele) | 30 | 37 | True |
| mixed (%70 ileri surukleme / %30 rastgele) | 30 | 30 | True |

Ham cikti: docs/olcumler/oynatici-boru/k2-senkron-v1.log.

SeekAudio her cagrildiginda kosulsuz yeniden baslar (StopAudioOnly +
yeni surec, DecoderPipe.cs SeekAudio metodu) - yani 30 aramalik bir dizi
ses tarafinda **her zaman** en az 30 surec baslatir. mixed modda toplam
tam 30 cikmasinin anlami: video tarafinda **sifir** ek yeniden baslatma
oldu (video kalicilik burada tam calisti, hepsi ses sayaci). uniform
modda 37 = 30 (ses) + 7 (video) - yani rastgele erisimde bile video
tarafinin bir kismi kalici kaldi, ama sifir degil.

**K2 hukmu: kirmizi ilan ediyorum, kalicilik ucuncu bir orta yol degil.**
Sozlesme metni "arama surece yeniden baslatmadan yapilir" diyor - bu, ses
tarafinda hicbir zaman dogru degil (SeekAudio her cagrida yeniden baslar,
kalicilik mekanizmasi yok), video tarafinda ise yalniz mixed/ardisik
erisimde dogru, rastgele erisimde degil. Rastgele hedeflerde video
tarafinin da neden restart'a dustugunu acikliyorum: MaxForwardCatchupFrames=3
sinirini uzak rastgele bir hedef pratikte her seferinde asiyor (hedef ile
mevcut kodcozme imleci arasindaki mesafe onbellek disinda kaliyor), bu
yapisal - process tabanli tek yonlu bir ffmpeg kodcozucuye rastgele
erisimde tam kalicilik saglamak, butun akisi bastan sona bellekte tutmadan
mumkun degil. Sesin kalicilik mekanizmasi hic yazilmadi (SeekAudio zaten
yeniden baslatmayi hep yapiyor); bu, T176'ya devredilecek ayri bir is.

Cokme kurtarmasi (degismedi, 1. ve 2. turdan tasindi):
Surec_disaridan_oldurulunce_boru_Faulted_yayar_ve_kendini_kurar
testi TestOnly_KillVideoProcess() ile video surecini disaridan olduruyor,
borunun Faulted olayini 3 sn icinde yaydigini ve bir sonraki SeekAsync
cagrisinin sureci yeniden kurup kare teslim ettigini dogruluyor:

Basarili!  - Basarisiz:     0, Basarili:     1, Atlanan:     0, Toplam:     1

(.calisma/T175/k2-crash-recovery.log)

## K3 - Yeni boru gecikmesi (150 ms baglayici olcut)

Kok neden duzeltmesi sonrasi olcum (MaxForwardCatchupFrames=3,
MaxRestartAttemptsPerSeek=8, src/VidShrink.Ffmpeg/Playback/DecoderPipe.cs).

| Kaynak | Mod | Medyan (ms) | p95 (ms) | Maks (ms) |
|---|---|---|---|---|
| kisa | uniform | 137,8 | 178,9 | 930,2 |
| kisa | mixed | 61,1 | 172,8 | 702,5 |
| orta | uniform | 143,0 | 172,7 | 174,7 |
| orta | mixed | 69,2 | 170,1 | 172,4 |
| uzun | uniform | 146,4 | 194,4 | 206,0 |
| uzun | mixed | 61,5 | 822,3 | 885,3 |

Ham diziler: .calisma/T175/k3-kisa-v3.log, k3-orta-v3.log, k3-uzun-v3.log.

Karsilastirma (K1 taban -> K3 duzeltilmis boru, medyan / p95 / maks, ms):

| Kaynak | Mod | Medyan | p95 | Maks |
|---|---|---|---|---|
| kisa | uniform | 147,8 -> 137,8 | 802,9 -> 178,9 | 1805,7 -> 930,2 |
| kisa | mixed | 144,2 -> 61,1 | 228,9 -> 172,8 | 239,1 -> 702,5 |
| orta | uniform | 138,2 -> 143,0 | 179,2 -> 172,7 | 733,1 -> 174,7 |
| orta | mixed | 143,8 -> 69,2 | 183,0 -> 170,1 | 836,5 -> 172,4 |
| uzun | uniform | 146,1 -> 146,4 | 179,2 -> 194,4 | 226,3 -> 206,0 |
| uzun | mixed | 138,3 -> 61,5 | 836,8 -> 822,3 | 837,2 -> 885,3 |

Hukum: K3 hala kirmizi, ama 1. turdaki "kotu nedeni bilinmiyor" halinden
farkli bir kirmizi. Medyan altimi kaynagin altisinda da 150 ms'nin altinda
(61-147 ms), mixed modda K1'e gore ciddi iyilesme (61-69 ms vs 138-144 ms,
onbellek/ileri-yakalama isliyor). Ama p95 altimi kaynagin altisinda da hala
150 ms'yi asiyor (170-822 ms).

Onemli fark, 1. turun kirmizi karariyla ayni degil, ama 2. turda buraya
yazilan "dort kaynakta p95 ve maksimum esit ya da daha iyi" cumlesi kendi
tablosuyla celisiyordu; 3. turde "uc" olarak duzeltildi ama bu da yanlisti
(iki kaynak+mod atlanmis, borc bulgusu 2). Karsilastirma tablosundaki alti
kaynak+modu tek tek sayarsan **bes** kaynak+modda K3'un p95'i K1'e esit ya
da daha iyi (kisa/uniform 802,9 -> 178,9; kisa/mixed 228,9 -> 172,8;
orta/uniform 179,2 -> 172,7; orta/mixed 183,0 -> 170,1; uzun/mixed
836,8 -> 822,3), ve **yalniz uzun/uniform'da p95 kotulesiyor**: K1 179,2
-> K3 194,4 (tablo, K3 - K1 - Yeni boru gecikmesi bolumu). Bunu
gizlemiyorum: uzun/uniform bu turde K1'e gore daha kotu bir p95 veriyor,
iyilesme alti kaynak+moddan besinde, evrensel degil.

Kalan kuyrugun cogu yine de borunun kendi kusurundan degil, K1'in de
tasidigi ffmpeg surec-baslatma varyansindan geliyor (orta uniform p95
172,7 vs K1 179,2; orta mixed maks 172,4 vs K1 836,5). Uzun/mixed'de K3'un
p95 822,3 / maks 885,3, K1'in kendi tabanindaki 836,8 / 837,2 ile neredeyse
ayni - yani duzeltmeden sonra kalan kuyruk, borunun kusuru degil, ffmpeg'in
kendi uc-durum arama maliyeti. Kisa kaynakta ise K3'un maksimumu (930,2 /
702,5) K1'in maksimumunu (1805,7 / 239,1) uniform modda gecmese de mixed
modda asiyor (702,5 > 239,1) - bu, kisa kaynaktaki dusuk mutlak surec
sayisinin (12 surec / 30 arama) her yeniden baslatmayi orantili olarak daha
agir kildigi bir yan etki, ayrica dokumante ediliyor.

T167'nin geri donus kriteri baglayici: p95 uc kaynagin hepsinde 150 ms
altina inmedigi icin bu sozlesme K3'te kirmizi teslim ediliyor. Bu bolum yazildiginda LibVLC'ye gecis mesru bir
secenek gorunuyordu; **karar sonradan verildi ve gecilmiyor** - gerekcesi
asagida "T0 karari - LibVLC'ye gecilmiyor" basliginda, sayilariyla.

Borc bulgusu (K1/K3 esdeger is yapmiyor): K1 her aramada -frames:v 1 ile
tam kodcozme yapip TAM OLARAK hedeflenen kareyi dondurur. K3 -skip_frame
nokey + FloorIndex ile en yakin ONCEKI anahtar kareyi dondurur (hedefin
kendisini degil), ve bazi aramalari sifir is ile onbellekten yanitlar
(ornegin kisa/mixed medyaninin 61 ms'ye dusmesinin bir kismi bu yuzden).
Bu, K3'u K1'e karsi yapisal olarak avantajli kiliyor; yukaridaki tablo bunu
gizlemeden, oldugu gibi rapor ediyor.

## K4 - Ses/goruntu senkronu

2. turun kok kusuru (denetci KRITIK 1+2+3): `videoPts` `audioPts`'tan
turetiliyordu (`stamps[FloorIndex(stamps, audioPts)]`), bu yuzden fark
yapisal olarak `(-anahtar_kare_araligi, 0]` araligina kapaniyordu - gercek
kayma ne olursa olsun. Ustelik olcum yalniz `-skip_frame nokey` (arama
icin yazilmis, yalniz anahtar kare cozen) yolunu kullaniyordu; surekli
oynatma yolu hic yoktu.

3. turde ikisi de duzeltildi:

1. **Surekli oynatma yolu yazildi**: `DecoderPipe.StartContinuousPlayback`
   (src/VidShrink.Ffmpeg/Playback/DecoderPipe.cs) `-skip_frame nokey`
   OLMADAN, `-re` ile gercek zamanli hizda, butun kareleri cozen ayri bir
   ffmpeg sureci baslatiyor ve `ContinuousPlayback.LatestVideoPts`'i
   **decode edilen kare sayisindan** turetiyor (`fromSeconds + (n-1)/fps`,
   fps `FfprobeClient`'tan gelen kaynak fps'i) - `audioPts`'a hicbir
   bagimliligi yok.
2. K4 olcusu artik bu yolu kullaniyor: `pipe.StartContinuousPlayback(0)` +
   `pipe.SeekAudio(0)`, her yeni kare decode edildiginde
   `(videoPts - audioPts) * 1000` orneklemi aliniyor.

Kaynak: senkron-130s.mkv (130 s, 1 kHz sine + testsrc, 30 fps). Kosum:

```
dotnet tools/VidShrink.PlaybackProbe/bin/Release/net8.0/VidShrink.PlaybackProbe.dll k4 senkron-130s.mkv 126
```

Ham cikti: docs/olcumler/oynatici-boru/k4-senkron-v5.log.

```
ses_var=True n=3778 sure_s=126.0 kare_sayisi=3779
maksimum_mutlak_kayma_ms=100.00
baslangic_kayma_ms=-40.00 son_kayma_ms=-60.00 degisim_ms=-20.00
```

**Yontem T167 ile ayni bicimde**: 3778 ornek, 126,0 saniye, sonuc
mutlak ofset degil **ofsetin degisimi** (T167: 3701 ornek, 123 s, 1,9 ms
degisim - docs/olcumler/oynatici-hatti.md:252-254,287).

4. turde bu bolumun mansetini bozan asil kusur bulundu: `baslangic_kayma_ms`
ve `son_kayma_ms` ham dizinin **ilk ve son tek ornegi**, ve deger dagilimi 6,67 ms
adimli 12 ayrik seviyeye oturuyor (video 33,33 ms ile ses 40,00 ms kuantasinin
farki; ses tarafi `AudioSink.cs:29` `DesiredLatency = 80` ve NAudio'nun iki
tamponundan geliyor). Dagilim duzenli bir testere disi degil, bu 12 seviye
uzerinde can bicimli: -100,00 ve -26,67 birer ornek, kutle -73,33 ile -46,67
arasinda. Tek orneklik uc deger yontemi bu veride gecersiz. T167'de ayni yontem tutuyordu cunku
gurultu tabani ~12 ms'ti; burada gurultu tabani (maks-min) **73,33 ms**
(-26,67 ms ile -100,00 ms arasi), yani tek ornek uc deger farki (-20,00 ms) gercek
egilimden degil, hangi fazda yakalandigindan geliyor.

Ham diziden (`docs/olcumler/oynatici-boru/k4-senkron-v5.log`, n=3778) ucu de
kendim yeniden hesapladim. Asagidaki blok **ham cikti degil**, ham diziden
turetilmis hesaptir (logun `kayma_ms=` alanlari ayristirilip en kucuk kareler
egimi, 300'luk pencere ortalamasi ve 12 kova ortalamasi hesaplandi; kova bolmesi
`n//12`, kalan son kovaya eklenir):

```
n=3778   min=-100,00  maks=-26,67  ortalama=-57,63  sigma=13,41
en kucuk kareler egimi -> tum kosumda toplam degisim  = -0,52 ms
ilk 300 ornek ort. -58,40  ->  son 300 ornek ort. -60,13  = -1,73 ms
12 kova (~10,5 s) ortalamasi: -58,1 -55,4 -58,0 -59,8 -55,3 -59,0
                              -57,9 -56,9 -58,2 -57,0 -55,8 -60,2   (trend yok)
```

Bu sayilar bagimsiz denetimin ayni logdan hesapladigi sayilarla min/maks/
ortalama/sigma/egim/pencere kolonlarinda **birebir** tutuyor. Tek fark 12. kova
ortalamasi: burada -60,2, onceki denetim raporunda -60,5. Fark kova bolmesinin
seciminden geliyor - `n//12` + kalan son kovada -60,2, esit 314 / esit 315 /
`array_split` / zaman tabanli 10,5 s bolmelerinin hepsi -60,4 veriyor. **-60,5
degeri denetimde yeniden uretilemedi**; 250-420 orneklik hicbir kuyruk penceresi
onu vermiyor. Sapma 0,3 ms ve 12 kovanin hicbirinde egilim yok, yani "trend yok"
hukmu etkilenmiyor - ama uzlastirma dogrulanmis degil, oldugu gibi yaziliyor. Uc yontem de (egim, 300'luk pencere, 12 kova) ayni sonucu veriyor: kayma
**birikmiyor**, 12 kovada gorunur bir trend yok. Toplam degisim -0,52 ms ile -1,73
ms arasinda, T167'nin kendi hattindaki 1,9 ms'nin ayni mertebesinde, LibVLC'nin
2,1 ms'sinin de icinde.

**K4 hukmu: GECTI.** Onceki turde "10 kat kotu" diyen manset, ilk/son tek ornek
yonteminin bu sinyalde gecersiz olmasindan kaynaklaniyordu; dogru yontemle (egim/
pencere/kova) olculdugunde senkron T167'nin kendi hattiyla ayni yerde duruyor.

2. turun K4 bolumunde T0'a bildirilen "n=63, -138,17..-98,95 ms" sayilari
bu turde **kullanilmadi**: onlar tur 1'in reddedilen Stopwatch tabanli
olcusunden geliyordu (denetci KRITIK 3), bu belgede artik yer almiyor.

Sessiz kaynak: Sessiz_kaynakta_HasAudio_false_ve_SeekAudio_cokmez testi
HasAudio=false oldugunda SeekAudio cagrisinin cokmedigini dogruluyor (bkz.
K7, OynaticiBoruTests suiti icinde); StartContinuousPlayback de HasAudio
kontrolune bakmadan calisir, cagiran taraf `pipe.HasAudio` ile driftMs'i
sifirlar (K4Async, Program.cs).

## K5 - Kurulum boyutu

dotnet publish -c Release -r win-x64 --self-contained true ciktisi, bu
turda kod DecoderPipe'a ContinuousPlayback eklendigi icin yeniden olculdu.
Ham cikti bu turde ilk kez commit edildi: 2. tur K5 sayisi doguydu ama
kanit dosyasi eksikti (2. tur borc bulgusu 7). Kosum:

```
dotnet publish src/VidShrink.App/VidShrink.App.csproj -c Release -r win-x64 --self-contained true -o .calisma/T175/publish-k5
```

Ham cikti: docs/olcumler/oynatici-boru/k5-v3.log.

| | Dosya sayisi | Bayt |
|---|---|---|
| T167 tabani (NAudio'suz) | 228 | 100.835.627 |
| 2. tur (NAudio ile) | 234 | 101.416.219 |
| 3. tur (NAudio + surekli oynatma yolu) | 234 | 101.418.259 |
| Fark (T167 tabanina gore) | +6 | +582.632 (yaklasik 0,56 MiB) |

Dosya sayisi degismedi, bayt sayisi 3. turde 2. tura gore +2.040 bayt
buyudu (ContinuousPlayback kodu VidShrink.Ffmpeg.dll'e eklendi).

## K6 - Mutasyon

Iki mutasyon, her birinden once dotnet build -c Release --no-incremental
(--no-build kullanilmadi), ardindan gercek koda geri donuldu ve
OynaticiBoruTests (17 test) yeniden yesile alindi.

2. turun iki kaniti gecersizdi (borc bulgusu 8-9): K6(a) kaniti 15
testlik bir suitten geliyordu (agac K6(b)'nin testi olmadan kosulmustu),
K6(b)'nin "oncesi" dosyasi bir K4 kayma olcusuydu, icinde tek test sayisi
yoktu. 3. turde ikisi de suit **17 testin hepsiyle** ve dogru
dosyalarla yeniden uretildi. Ayrica mutasyona ozel gomulu sabit
(`Assert.NotEqual(1.15, ...)`, eski OynaticiBoruTests.cs:189) silindi;
onun yerine `Surekli_oynatmada_ses_goruntu_kaymasi_zamanla_buyumez`
adinda gercek bir senkron-koruma testi eklendi: `StartContinuousPlayback`
+ `AudioSink` ile 3 sn boyunca kayma ornekleniyor, ilk 5 ve son 5
ornegin ortalamasi arasindaki fark 200 ms'yi asarsa test FAIL veriyor -
bu, herhangi bir gercek saat-ayirma mutasyonunu yakalar, tek bir sabit
degil.

| Mutasyon | Duzeltme oncesi (17 test) | Duzeltme sonrasi (17 test) |
|---|---|---|
| (a) Her aramada yeniden baslat (`needRestart = true`, canCatchUp gormezden gelinerek) | 5 FAIL, 12 basarili (docs/olcumler/oynatici-boru/k6-a-v3.log) - Yakin_ileri_aramalar_sureci_yeniden_baslatmaz (8 beklenirken 32), Ilk_arama_tek_surec_baslatir_ve_kare_teslim_eder (kare null), Onbellek_disina_dusen_uzak_geri_atlama_sureci_yeniden_baslatir (kare null), ve 2 test daha | 17/17 yesil |
| (b) Ses saatini goruntu saatinden ayir (`AudioSink.BytesToSeconds` bayt sayisini 1,15 ile carpar) | 2 FAIL, 15 basarili (docs/olcumler/oynatici-boru/k6-b-v3.log) - BytesToSeconds_format_hizinda_dogru_donusum_yapar (1 yerine 1,15) VE yeni Surekli_oynatmada_ses_goruntu_kaymasi_zamanla_buyumez ("kayma 3 sn'de asiri buyudu: erken=-80,5ms gec=-497,1ms") | 17/17 yesil |

Ham kanit: docs/olcumler/oynatici-boru/k6-a-v3.log (mutasyon a, 17 testin
5'i FAIL), k6-b-v3.log (mutasyon b, 17 testin 2'si FAIL). Eski 2. tur
loglari (k6-a-v2.log, k6-b-v2.log, k6-a-hang.log, k6-b-mutasyon.log)
dizinde kaldi ama artik gecerli kanit degil, 1./2. tur tarihi olarak
duruyor.

Her iki mutasyon da revert edildikten sonra dogrulandi: git diff temiz,
17/17 yesil (docs/olcumler/oynatici-boru/k7-run-final-v2.log).

## K7 - Dogrulama satirinin kol sayisi

Tek kollu: dotnet test --filter "OynaticiBoruTests".

--list-tests ile dogrulandi (docs/olcumler/oynatici-boru/k7-list-v2.log):
OynaticiBoruTests kolu **17** test eslesiyor (2. turdeki 16'dan, K6(b)
icin mutasyon-sabiti silinip yerine gercek senkron testi eklendigi icin
+1). Sifir test bulan kol yok.

## SeekAsync'in null donme sozlesmesi (T176 icin)

`DecoderPipe.SeekAsync` `MaxRestartAttemptsPerSeek` (8) kadar yeniden
baslatma denendikten sonra hala kare teslim edemiyorsa **sessizce `null`
doner** ve `Faulted` olayini yayar (DecoderPipe.cs, SeekAsync metodu, iki
`RaiseFault(...); return null;` noktasi). Bu, istisna firlatmaz - cagiran
taraf (T176'nin arayuzu) her `SeekAsync` sonucunu `null` icin kontrol
etmek **zorunda**; kontrol etmezse `NullReferenceException` ile cokecek
kod yazmis olur. `Faulted` olayina abone olmak kullaniciya "neden" bilgisi
verir ama `null`'u yakalamanin yerini tutmaz - olay asenkron yayilir,
`SeekAsync`'in donus degeriyle ayni anda gelmeyebilir.

## Sonuc

- K1: degismedi, taban gecerli.
- K2: **kirmizi ilan edildi.** Video tarafi mixed/ardisik erisimde tam
  kalici (30/30 = 0 ek restart, senkron-130s.mkv), rastgele erisimde
  kismi (37 surecin 7'si video, senkron-130s.mkv uniform). Ses tarafi
  (`SeekAudio`) kalicilik mekanizmasina hic sahip degil, her cagrida
  yeniden baslar - 30 aramada her zaman 30 ses sureci. Sozlesmenin
  "arama surece yeniden baslatmadan yapilir" cumlesi bu haliyle
  karsilanmadi. Cokme kurtarmasi geciyor.
- K3: hala kirmizi (p95 uc kaynagin hepsinde 150 ms altina inmiyor).
  Ozet cumle artik kendi tablosuyla tutarli: p95 alti kaynak+moddan
  **besinde** iyilesti, **uzun/uniform'da kotulesti** (179,2 -> 194,4).
  LibVLC gecisi bu sozlesme kapsaminda oneriliyordu; senkron olcumu
  3. turde alindi (`k4-senkron-v5.log`), 4. turde **yeniden cozumlendi**
  ve manseti tersine cevirdi - T0 karari LibVLC'ye gecmemek yonunde
  (asagida "T0 karari" basligi).
- K4: T167'nin yontemiyle (surekli oynatma, videoPts kare sayisindan
  bagimsiz ilerliyor) yeniden olculdu: n=3778, 126,0 s. En kucuk kareler
  egimiyle toplam degisim **-0,52 ms**, ilk/son 300 ornek penceresiyle
  **-1,73 ms**, 12 zaman kovasinda trend yok - T167'nin kendi hattindaki
  1,9 ms ile ayni mertebede. Ilk/son tek ornekten hesaplanan onceki
  "20 ms degisim" mansetti; o yontem 73,33 ms genlikli testere disi
  sinyalde gecersiz (bkz. K4 bolumu). **K4: GECTI.** Sessiz kaynakla
  cokme yok.
- K5: gecti, +6 dosya / +0,56 MiB (T167 tabanina gore), ham cikti bu
  turde commit edildi.
- K6: iki mutasyon da 17 testin tam suitiyle, dogru "once/sonra"
  dosyalariyla yeniden uretildi; mutasyona ozel gomulu sabit silindi,
  yerine gercek bir senkron-koruma testi eklendi.
- K7: verify satiri tek kollu, 17/17 test eslesiyor, sifir bulan kol yok.

## Borc

1. K2'nin "37 = 30 ses + 7 video" ayristirmasi olculmemis, cikarim:
   `k2-senkron-v1.log` yalniz toplami basiyor, video/ses kirilimi ayri
   sayilmiyor. Ayrica `K2Async` (`tools/VidShrink.PlaybackProbe/Program.cs`)
   boruyu bir kez acip iki modu (uniform, mixed) **ayni `pipe` uzerinde**
   art arda kosuyor; `DecoderPipe._cache` yalniz `OpenAsync`'te
   temizleniyor, yani mixed kosumu onundeki 30 uniform aramanin isittigi
   onbellekten yararlaniyor. Bu belgenin daha onceki "video kalicilik
   burada tam calisti" cumlesi (K2 bolumu basi) bu kosumdan **cikarilamaz**
   - isaretliyorum.
2. `ContinuousPlayback.Pump` govdesi (`DecoderPipe.cs`) `catch { }` ile
   bitiyor: ffmpeg sureci olurse akis sessizce donuyor, `Faulted` olayi
   yayilmiyor. K2'nin baglayici sarti olan "surec olurse boru sessizce
   olmez" arama yoluna (`SeekAsync`) uygulanmis, 3. turde eklenen yeni
   surekli oynatma yoluna tasinmamis.
3. `ContinuousPlayback.LatestVideoPts` CFR (sabit kare hizi) varsayiyor
   (`_fromSeconds + (n-1)/_fps`); VFR (degisken kare hizi) kaynakta bu
   hesap yanlis ilerler ve K4 sessizce bozulur - varsayim raporun onceki
   turlerinde yazili degildi, burada isaretliyorum. Ayrica
   `StartContinuousPlayback` `_processesStarted` sayacini artiriyor
   (`DecoderPipe.cs:128`), yani oynatma baslatmalari K2'nin "arama basina
   surec" sayacina karisiyor - K2 tablosundaki sayilar saf arama-baslatma
   degil, oynatma baslatmalarini da iceriyor olabilir.

## T176 devri

T176'nin (arayuz) devraldigi bilinen kusurlar:

- K2 ayristirmasinin (video/ses) olculmemis oldugu ve mixed kosumunun
  onbellek isitmasindan yararlandigi - T176 gercek kullanicida ayri
  olcum yapmadan bu sayilara guvenmemeli.
- `ContinuousPlayback.Pump`'in `catch { }` ile sessiz kaldigi, yani
  ffmpeg cokmesinde oynatma donuyor ve arayuz bunu fark etmiyor - T176
  bu yolu `Faulted` yayacak sekilde tamamlamali.
- `LatestVideoPts`'in CFR varsaydigi, VFR kaynakta K4'un sessizce
  gecersizlesecegi, ve `_processesStarted` sayacinin arama ile oynatma
  baslatmalarini ayirt etmedigi.
- (Onceki turlerden tasinan, hala acik) `SeekAudio` kalicilik
  mekanizmasindan yoksun - her cagrida yeni ses sureci acar.
- (Onceki turlerden tasinan, hala acik) `SeekAsync`'in `MaxRestartAttemptsPerSeek`
  asildiginda `null` donmesi - cagiran taraf `null` kontrolu yapmak
  zorunda (yukarida "SeekAsync'in null donme sozlesmesi" basligi).
- (3. tur denetiminden tasinan) Surekli oynatma senkron testinin esigi
  (`OynaticiBoruTests.cs:344-348`, 200 ms) olculen gurultu tabaninin
  (73,33 ms) ancak **2,7 kati**. Kaba saat ayrilmasini yakalar, asamali
  kaymayi yakalamaz; testin duyarliligi bu oranla sinirlidir.
- (3. tur denetiminden tasinan) `k5-v3.log` "ham cikti" degil, komut ve
  turetilmis sayidan olusan iki satirlik ozet. Sonuc bagimsiz olarak
  yeniden yayimlanip dogrulandi, ama kanit dosyasi ham degil.

## T0 karari - LibVLC'ye gecilmiyor

Karari ceviren sutun senkrondu (K4). Olcum 3. turde alindi, 4. turde
dogru cozumlendi: 3778
ornek, 126 s, en kucuk kareler egimiyle toplam **-0,52 ms**, ilk/son 300
ornek penceresiyle **-1,7 ms**, 12 zaman kovasinda trend yok - T167'nin
kendi hattindaki **1,9 ms** ile ayni mertebede, LibVLC'nin **2,1 ms**'siyle
de esdeger. Diger sutunlar zaten kendi hattimizin lehineydi:

- Kurulum bedeli: kendi hattimiz **+6 dosya / +0,56 MiB** (K5), LibVLC
  **+100-280 MB** (T167, `docs/olcumler/oynatici-hatti.md`).
- Gercekci erisim deseninde (mixed) K3 medyanlari **61,1 / 69,2 / 61,5
  ms** (kisa/orta/uzun), T167'nin LibVLC icin olctugu 38,9-57,2 ms
  bandinin hemen ustunde.
- p95'in 150 ms'i asmasi mimari bir kusurdan degil, ffmpeg'in kendi
  surec-baslatma varyansindan geliyor: kaynak+mod eslestirilerek
  >250 ms ornek sayimi **taban 7/180, boru 4/180** - kuyruk K1 tabanina
  eslenmis, boru onu buyutmuyor.
- **LibVLC'nin p95'i bu depoda hic olculmedi** (T167'de n=5); p95
  olcutuyle LibVLC'ye gecmek, olculmemis bir alternatife gecmek olurdu.

Kendi hattimizda kapatilacak iki somut kusur duruyor, ikisi de p95'in
kaynagi ve T176'nin degil, ayri bir sozlesmenin isi:

- (a) `SeekAudio` kalicilik mekanizmasindan tumuyle yoksun - 30 aramada
  her zaman 30 ses sureci; K2'nin kirmizi olmasinin asil nedeni bu.
- (b) rastgele erisimde `MaxForwardCatchupFrames=3` sinirini asan uzak
  hedefler video tarafini restart'a dusuruyor (uniform'da 7/30).

**Karar: LibVLC paketine gecilmiyor, kendi ffmpeg + NAudio boru hattimiz
kaliyor.** T167'nin karari bu sekilde teyit edildi; K2 ve K3'un kalan
kirmizilari LibVLC gecisini degil, `SeekAudio` kaliciligi ve ileri-yakalama
sinirinin genisletilmesini gerektiriyor - ayri bir sozlesme konusu.
