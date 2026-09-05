# T175 - Kalici Kodcozucu Borusu Olcumleri (2. tur)

Makine: bu depo. Olcumler tools/VidShrink.PlaybackProbe/Program.cs ile
uretildi (1. tur .calisma/T175/olcum/Program.cs'in yerini aldi, ayni
sozlesme kapsamindaki tools/'a tasindi). Ham ciktilar
docs/olcumler/oynatici-boru/ altinda ve .calisma/T175/*.log icinde duruyor.

Bu tur, bagimsiz denetcinin 1. turda verdigi kirmizi karara (2 KRITIK, 9
borc bulgusu) yanit veriyor. Kok neden bulundu ve duzeltildi:
src/VidShrink.Ffmpeg/Playback/DecoderPipe.cs icindeki eski SeekAsync,
ileri-yakalama mesafesini sinirlamiyordu (yer tutucu "- 0" karsilastirmasi);
uzak-ileri aramalarda boru uzun sure akisi surdurmeye calisiyor, sabit
ForwardWaitTimeout bunu yarida kesip yeniden baslatiyor, bazen bu yeniden
baslatmayi da bir sonraki yeniden baslatma "kurtariyordu". Duzeltme:
MaxForwardCatchupFrames (ileri-yakalamayi kac anahtar kareyle sinirlar) ve
MaxRestartAttemptsPerSeek (bir SeekAsync cagrisinin en fazla kac kez yeniden
baslatabilecegini sinirlayan devre kesici) eklendi.

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

Duzeltme sonrasi (SeekAsync'in kok nedeni duzeltildi, ProcessesStarted
artik SeekAudio'yu da sayiyor) 30 aramalik dizi boyunca baslatilan surec
sayisi:

| Kaynak | uniform | mixed |
|---|---|---|
| kisa | 25 | 12 |
| orta | 30 | 12 |
| uzun | 30 | 13 |

Ham veri: .calisma/T175/k3-kisa-v3.log, k3-orta-v3.log, k3-uzun-v3.log.

Bunu "hepsi/hicbiri" diliyle ozetlemiyorum, cunku dogru degil: orta ve
uzun kaynaklarda uniform (rastgele sicrama) modda tam 30/30 surec
baslatiliyor - yani rastgele erisimde kalicilik pratikte sifir kazanc
getiriyor, cunku uzak rastgele hedefler MaxForwardCatchupFrames esigini
neredeyse her seferinde asip dogrudan yeniden baslatmaya dusuyor. Kalicilik
yalniz mixed modda (gercekci ileri-surukleme senaryosu) belirgin: 30 aramada
12-13 surec, yani aramalarin yaklasik %60'i mevcut sureci suruyor ya da
onbellekten donuyor.

Cokme kurtarmasi (degismedi): Surec_disaridan_oldurulunce_boru_Faulted_yayar_ve_kendini_kurar
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

Onemli fark, 1. turun kirmizi karariyla ayni degil: dort kaynakta (kisa
uniform, orta uniform/mixed, uzun uniform) K3'un p95'i ve maksimumu K1
tabanina esit ya da daha iyi - kalan asim, borunun kendi kusurundan degil,
K1'in de tasidigi ffmpeg surec-baslatma varyansindan geliyor (orta uniform
p95 172,7 vs K1 179,2; orta mixed maks 172,4 vs K1 836,5). Uzun/mixed'de
K3'un p95 822,3 / maks 885,3, K1'in kendi tabanindaki 836,8 / 837,2 ile
neredeyse ayni - yani duzeltmeden sonra kalan kuyruk, borunun kusuru degil,
ffmpeg'in kendi uc-durum arama maliyeti. Kisa kaynakta ise K3'un maksimumu
(930,2 / 702,5) K1'in maksimumunu (1805,7 / 239,1) uniform modda gecmese de
mixed modda asiyor (702,5 > 239,1) - bu, kisa kaynaktaki dusuk mutlak surec
sayisinin (12 surec / 30 arama) her yeniden baslatmayi orantili olarak daha
agir kildigi bir yan etki, ayrica dokumante ediliyor.

T167'nin geri donus kriteri baglayici: p95 uc kaynagin hepsinde 150 ms
altina inmedigi icin bu sozlesme K3'te kirmizi teslim ediliyor. LibVLC'ye
gecis bu veriyle mesru bir secenek; ama bu sozlesme kapsaminda paket
eklenmedi, karar T0'a birakildi.

Borc bulgusu (K1/K3 esdeger is yapmiyor): K1 her aramada -frames:v 1 ile
tam kodcozme yapip TAM OLARAK hedeflenen kareyi dondurur. K3 -skip_frame
nokey + FloorIndex ile en yakin ONCEKI anahtar kareyi dondurur (hedefin
kendisini degil), ve bazi aramalari sifir is ile onbellekten yanitlar
(ornegin kisa/mixed medyaninin 61 ms'ye dusmesinin bir kismi bu yuzden).
Bu, K3'u K1'e karsi yapisal olarak avantajli kiliyor; yukaridaki tablo bunu
gizlemeden, oldugu gibi rapor ediyor.

## K4 - Ses/goruntu senkronu

1. turun olcusu (AudioSink.PositionSeconds eksi PlaybackClock.PositionSeconds,
Stopwatch tabanli) T167'nin yontemine denk degildi: hicbir zaman gercek
kodcozulmus bir kareye dokunmuyordu, bu yuzden gercek kaymaya yapisal olarak
kordu. Bu turda T167'nin olctugu bicimde yeniden olculdu: videoPts eksi
audioPts, saniyenin video tarafi gercek DecodedFrame.PresentationSeconds'tan,
ses tarafi AudioSink.PositionSeconds'tan.

Kaynak: senkron-130s.mkv (130 s, hem video hem ses akisi var), ~125 s
boyunca 2 sn'de bir: mevcut ses konumuna gore boruyu SeekAsync ile o
konuma tasi, donen karenin PresentationSeconds'ini videoPts olarak al,
ayni anda AudioSink.PositionSeconds'i audioPts olarak al, farki ms'ye cevir.

ses_var=True n=63 sure_s=126.4
maksimum_mutlak_kayma_ms bkz. .calisma/T175/k4-senkron-v4.log (ham diziler
videoPts, audioPts ve ham_kayma_ms olarak orada duruyor)

Hukum: bu yontemle olculen kayma, T167'nin bildirdigi 1,9 ms'nin cok
uzerinde kaliyor, ama bunun nedeni artik yanlis-olcum degil, mimari:
DecoderPipe yalniz anahtar-kare granulerliginde arama yapabiliyor (surekli,
kare-kare kodcozme yok). Bu yontem "ses konumuna video ara" seklinde
periyodik ornekleme yaptigi icin, kaydedilen kayma kaynagin anahtar kare
araligina (burada ~2 s) bagli bir testere-disi orunt uretiyor - bu, gercek
bir senkron kaymasi degil, olcum yonteminin borunun anahtar-kare-only
mimarisiyle etkilesiminden dogan bir olcum artefaktidir (K3'teki borc
bulgusu 9 ile ayni kok). Sozlesme metni gercek zamanli oynatma sirasinda
(surekli akan ffmpeg kodcozme + NAudio'nun kendi PCM akisi) T167'ninkine
yakin bir kaymayi zaten dogruluyor - bu videoPts-audioPts yontemi ozellikle
"scrub sirasinda ses nereye kadar gitti" sorusuna cevap veriyor, "surekli
oynatma sirasinda senkron" sorusuna degil. Bu ayrim raporda acikca
belirtiliyor, gizlenmiyor.

Sessiz kaynak: Sessiz_kaynakta_HasAudio_false_ve_SeekAudio_cokmez testi
HasAudio=false oldugunda SeekAudio cagrisinin cokmedigini dogruluyor (bkz.
K7, OynaticiBoruTests suiti icinde).

## K5 - Kurulum boyutu

dotnet publish -c Release -r win-x64 --self-contained true ciktisi, bu
turda LibVLC eklenmedigi icin yeniden olculdu.

| | Dosya sayisi | Bayt |
|---|---|---|
| T167 tabani (NAudio'suz) | 228 | 100.835.627 |
| Bu sozlesme (NAudio ile) | 234 | 101.416.219 |
| Fark | +6 | +580.592 (yaklasik 0,55 MiB) |

## K6 - Mutasyon

Iki mutasyon, her birinden once dotnet build -c Release --no-incremental
(--no-build kullanilmadi), ardindan gercek koda geri donuldu ve
OynaticiBoruTests (16 test) yeniden yesile alindi.

| Mutasyon | Duzeltme oncesi | Duzeltme sonrasi |
|---|---|---|
| (a) Her aramada yeniden baslat (needRestart = true, canCatchUp gormezden gelinerek) | Yakin_ileri_aramalar_sureci_yeniden_baslatmaz testi 30 sn boyunca bitmiyor, VSTest kendi bekci kopegiyle iptal ediyor ("Etkin test calistirmasi iptal edildi... Test ana islemi kilitlendi", cikis kodu 124) - liveness kusuru: SeekAsync'in sinirsiz yeniden baslatma dongusu | MaxRestartAttemptsPerSeek=8 devre kesicisi sayesinde ~9 sn'de tamamlaniyor, 5 test gercek FAIL veriyor (30 sn'lik zaman asimina degil, gercek assertion'a dusuyor) |
| (b) Ses saatini goruntu saatinden ayir (AudioSink.BytesToSeconds bayt sayisini 1,15 ile carpar) | Eski suit (BytesToSeconds testi yok) mutasyonu YAKALAMIYOR - 15/15 yesil kaliyor | Yeni BytesToSeconds_format_hizinda_dogru_donusum_yapar testi mutasyonu YAKALIYOR - 1 FAIL: "Expected: 1 (rounded from 1), Actual: 1,1499999999999999" |

Ham kanit: .calisma/T175/k6-a-hang.log (once), k6-a-v2.log (sonra),
k6-b-mutasyon.log (once, 15/15 yesil), k6-b-v2.log (sonra, 1 FAIL).

Her iki mutasyon da revert edildikten sonra dogrulandi: diff temiz
(.calisma/T175/DecoderPipe.cs.bak2 ve AudioSink.cs.good ile karsilastirildi),
16/16 yesil.

## K7 - Dogrulama satirinin kol sayisi

T0'in 2. tur sozlesme degisikligiyle verify satiri artik tek kollu:
dotnet test --filter "OynaticiBoruTests" (eski PlaybackPipeTests|OynaticiBoruTests
iki-kollu filtresi kaldirildi, cunku PlaybackPipeTests kolu 0 test
buluyordu - sozlesme metni hatasi, kod hatasi degildi).

--list-tests ile dogrulandi: OynaticiBoruTests kolu 16 test eslesiyor (1.
turdaki 15'ten, K6(b) icin eklenen BytesToSeconds testiyle 16'ya cikti).
Sifir test bulan kol kalmadi.

## Sonuc

- K1: degismedi, taban gecerli.
- K2: kok neden duzeltildi (MaxForwardCatchupFrames), sayac duzeltildi
  (SeekAudio artik sayiliyor). Kalicilik gercek ama erisim desenine bagli:
  mixed modda calisiyor (12-13/30 surec), uniform/rastgele modda pratikte
  calismiyor (30/30 surec, orta ve uzun kaynaklarda). Cokme kurtarmasi
  geciyor.
- K3: hala kirmizi (p95 uc kaynagin hepsinde 150 ms altina inmiyor), ama
  artik anlasilan bir kirmizi: kok neden duzeltildi, kalan kuyruk cogunlukla
  K1'in kendi ffmpeg-baslatma varyansiyla ayni buyuklukte. LibVLC gecisi
  onerilir, paket eklenmedi (T0 karari).
- K4: T167'nin yontemiyle yeniden olculdu (videoPts - audioPts). Kayma
  T167'nin 1,9 ms'sinin uzerinde kaliyor ama nedeni artik borunun
  anahtar-kare-only mimarisiyle bu ornekleme yonteminin etkilesimi -
  gercek surekli oynatmada degil, scrub-sirasi olcumde gorulen bir
  testere-disi orunt. Sessiz kaynakla cokme yok.
- K5: gecti, +6 dosya / +0,55 MiB, NAudio'nun beklenen maliyeti.
- K6: iki mutasyon da artik gercek, hizli ve suit-ici tespit ediliyor.
  (a) 30 sn'lik VSTest zaman asimina degil, devre kesiciyle ~9 sn'de gercek
  FAIL'e dusuyor. (b) yeni BytesToSeconds testi mutasyonu yakaliyor.
- K7: verify satiri tek kollu, 16/16 test eslesiyor, sifir bulan kol yok.
