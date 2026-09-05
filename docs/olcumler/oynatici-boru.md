# T175 - Kalici Kodcozucu Borusu Olcumleri

Makine: bu depo. Olcumler .calisma/T175/olcum/Program.cs ile uretildi,
ham ciktilar .calisma/T175/*.log altinda duruyor.

## K1 - Taban cizgisi (eski: her aramada yeni ffmpeg sureci)

Uc kaynak, 30 arama, uniform (rastgele) ve mixed (%70 ileri surukleme /
%30 rastgele sicrama) modlari.

| Kaynak | Mod | Medyan (ms) | p95 (ms) |
|---|---|---|---|
| kisa (60,4 s) | uniform | 147,8 | 802,9 |
| kisa (60,4 s) | mixed | 144,2 | 228,9 |
| orta (180 s) | uniform | 138,2 | 179,2 |
| orta (180 s) | mixed | 143,8 | 183,0 |
| uzun (600 s) | uniform | 146,1 | 179,2 |
| uzun (600 s) | mixed | 138,3 | 836,8 |

Ham gecikme dizileri: .calisma/T175/k1-kisa.log, k1-orta.log, k1-uzun.log.

## K2 - Kalici boru: surec sayisi ve cokme kurtarmasi

30 aramalik dizi boyunca baslatilan ffmpeg surec sayisi (K3 v2 kosumlarindan,
ProcessesStarted sayaci - ham veri .calisma/T175/k3-*-v2.log icinde):

| Kaynak | uniform | mixed |
|---|---|---|
| kisa | 23 | 7 |
| orta | 32 | 7 |
| uzun | 36 | 11 |

30 aramanin hicbirinde 30 yeni surec baslamiyor; cogu arama var olan sureci
surdruyor, onbellekten donuyor ya da yakin-ileri aramada sureci yeniden
kullaniyor.

Cokme kurtarmasi: Surec_disaridan_oldurulunce_boru_Faulted_yayar_ve_kendini_kurar
testi TestOnly_KillVideoProcess() ile video surecini disaridan olduruyor,
borunun Faulted olayini 3 sn icinde yaydigini ve bir sonraki SeekAsync
cagrisinin sureci yeniden kurup kare teslim ettigini dogruluyor:

Basarili!  - Basarisiz:     0, Basarili:     1, Atlanan:     0, Toplam:     1

(.calisma/T175/k2-crash-recovery.log)
## K3 - Yeni boru gecikmesi (150 ms baglayici olcut)

ForwardWaitTimeout 4 sn'den 250 ms'ye cekildikten sonraki olcum
(src/VidShrink.Ffmpeg/Playback/DecoderPipe.cs, satir 14).

| Kaynak | Mod | Medyan oncesi-sonrasi (ms) | p95 oncesi-sonrasi (ms) | Kazanc (medyan / p95, ms) |
|---|---|---|---|---|
| kisa | uniform | 147,8 - 139,1 | 802,9 - 970,7 | +8,7 / -167,8 |
| kisa | mixed | 144,2 - 43,1 | 228,9 - 393,4 | +101,1 / -164,5 |
| orta | uniform | 138,2 - 163,2 | 179,2 - 984,4 | -25,0 / -805,2 |
| orta | mixed | 143,8 - 70,8 | 183,0 - 390,8 | +73,0 / -207,8 |
| uzun | uniform | 146,1 - 160,0 | 179,2 - 1066,4 | -13,9 / -886,4 |
| uzun | mixed | 138,3 - 58,1 | 836,8 - 976,6 | +80,2 / -139,8 |

Ham diziler: .calisma/T175/k3-kisa-v2.log, k3-orta-v2.log, k3-uzun-v2.log.

Hukum: K3 gecmiyor. Medyan mixed modda uc kaynagin hepsinde 150 ms'nin
belirgin sekilde altinda (43-70 ms); gercekci kullanim deseninde boru
kazandiriyor. Ama p95 hicbir kaynakta, hicbir modda 150 ms'nin altina
inmiyor (390-1066 ms). Kaynak: seyrek uzak-ileri/geri sicramalarda tetiklenen
tam yeniden baslatmanin kendisi (yeni ffmpeg sureci + ilk kareye kadar
kodcozme), K1 tabaninin kendisinde de gorulen 800 ms'lik uc degerlerle ayni
kokten (ffmpeg'in kendi arama+ilk-kare maliyeti) besleniyor, borunun yeniden
baslatma mekanigi bunu ortadan kaldirmiyor.

T167'nin geri donus kriteri baglayici: bu sozlesme kirmizi teslim ediliyor.
Urun ici arama gecikmesi p95'te 150 ms altina inmiyor; LibVLC'ye gecis bu
veriyle mesru bir secenek; ama bu sozlesme kapsaminda paket eklenmedi, karar
ayri birakildi.
## K4 - Ses/goruntu senkronu

Kaynak: senkron-130s.mkv (130 s, hem video hem ses akisi var), 125 s boyunca
2 sn'de bir ornek, AudioSink.PositionSeconds eksi PlaybackClock.PositionSeconds.

Ilk kosumda kayma +124798 ms'den baslayip +3591 ms'ye dusen bir orunt verdi;
bozuk olcum. Kok neden: DecoderPipe.SeekAudio'nun ffmpeg cagrisinda gercek
zaman hiz sinirlamasi (-re) yoktu, ses sureci ~125 s'lik PCM'i saniyeler
icinde AudioSink.Write'a bosaltiyordu; PositionSeconds'in
bytesWritten eksi BufferedBytes hesabi bu durumda gercek calma ilerlemesinden
kopuyor. Duzeltme: SeekAudio'nun ffmpeg argumanlarina -re eklendi
(src/VidShrink.Ffmpeg/Playback/DecoderPipe.cs, satir 336-337).

Duzeltme sonrasi olcum (ham cikti):

ses_var=True n=63 sure_s=126.3
ham_kayma_ms=[-121.01,-124.04,-127.11,-130.42,-135.97,-137.40,-100.64,-103.63,-107.25,-110.64,-114.06,-117.42,-124.29,-125.02,-134.64,-106.69,-107.88,-112.16,-116.33,-117.89,-121.41,-125.26,-128.19,-131.77,-135.08,-112.02,-115.42,-119.01,-126.76,-127.63,-129.02,-133.28,-135.49,-138.17,-103.29,-114.17,-116.11,-119.56,-127.73,-133.66,-136.83,-100.08,-103.37,-106.77,-110.44,-114.26,-116.89,-127.03,-130.21,-133.75,-98.95,-100.62,-104.23,-109.66,-111.96,-114.15,-118.40,-133.44,-105.07,-111.54,-122.90,-132.88,-134.62]
maksimum_mutlak_kayma_ms=138.17

(.calisma/T175/k4-senkron-v3.log)

Hukum: sinirli ama T167'nin rakamini tutturamiyor. Kayma -138,17 ms ile
-98,95 ms arasinda salaniyor (testere disi orunt, ~30 s periyotla); birikip
buyumuyor, ama T167'nin kendi hattinda olctugu 1,9 ms'nin cok uzerinde. Bu
fark muhtemelen AudioSink'in tampon-tabanli PositionSeconds hesabinin
NAudio'nun gercek cihaz konumunu degil, yazilan/tamponlanan bayt farkini
kullanmasindan geliyor; bu, mevcut sozlesme butcesinde daha derin
arastirilmadi.

Sessiz kaynak: Sessiz_kaynakta_HasAudio_false_ve_SeekAudio_cokmez testi
HasAudio=false oldugunda SeekAudio cagrisinin cokmedigini dogruluyor
(15 testin biri, bkz. K7).
## K5 - Kurulum boyutu

dotnet publish -c Release -r win-x64 --self-contained true ciktisi.

| | Dosya sayisi | Bayt |
|---|---|---|
| T167 tabani (NAudio'suz) | 228 | 100.835.627 |
| Bu sozlesme (NAudio ile) | 234 | 101.415.623 |
| Fark | +6 | +579.996 (yaklasik 0,55 MiB) |

## K6 - Mutasyon

Iki mutasyon, her birinden once dotnet build -c Release --no-incremental
(--no-build kullanilmadi), ardindan gercek koda geri donuldu ve
OynaticiBoruTests yeniden yesile alindi.

| Mutasyon | Kirilan olcu | Ham kanit |
|---|---|---|
| (a) Her aramada yeniden baslat (needRestart her zaman true, onbellek denetiminden sonra) | Yakin_ileri_aramalar_sureci_yeniden_baslatmaz testi tamamlanmiyor; VSTest kendi bekci kopegiyle iptal ediyor: "Etkin test calistirmasi iptal edildi. Nedeni: Test ana islemi kilitlendi" (30 sn zaman asimiyla kesildi, cikis kodu 124) | .calisma/T175/k6-a-hang.log |
| (b) Ses saatini goruntu saatinden ayir (PlaybackClock.PositionLocked'a carpi 1.15 hiz sapmasi) | K4 kaymasi sinirli salinim yerine dogrusal buyuyor: 40 s'de -431 ms'den -6155 ms'ye | .calisma/T175/k6-b-mutasyon.log |

Mutasyon (a) sonrasi dotnet test normalde 1 sn'nin altinda biten testi 30 sn'de
bile bitirmedi; gercek koddaki davranisin tam tersi (bkz. K2 tablosu: 30
aramada 23-36 surec, asla 30'un tamami degil).

Mutasyon (b) sonrasi gercek koda (.calisma/T175/PlaybackClock.cs.bak'tan)
geri donuldu, fark git'te sifir (git diff bos).
## K7 - Dogrulama satirinin kol sayisi

verify satiri: dotnet test --filter "OynaticiBoruTests|PlaybackPipeTests" iki
alternatifli bir filtre. Her kolu ayri ayri --list-tests ile kontrol edildi:

- OynaticiBoruTests kolu: 15 test eslesiyor.
- PlaybackPipeTests kolu: 0 test eslesiyor; ".. icinde sunulan test calismasi
  filtresi 'PlaybackPipeTests' ile eslesen test yok".

Bu depoda dorduncu kez olan hata burada da var: verify satirinin bir kolu
sifir test buluyor. PlaybackPipeTests adinda bir test sinifi bu sozlesme
kapsaminda hic yazilmadi (owns listesinde yalniz OynaticiBoruTests.cs var).
Sozlesme metni dogrulamayi OynaticiBoruTests|PlaybackPipeTests olarak
tanimliyor ama gercek testler tek isim altinda (OynaticiBoruTests_*)
toplandi; PlaybackPipeTests muhtemelen sozlesme yazilirken dusunulen ama hic
var olmamis bir sinif adi. Teslim engellenmiyor cunku birinci kol (asil
testler) 15/15 yesil; ama bu bulgu gizlenmiyor, burada acikca yaziliyor.

## Sonuc

- K1, K2, K4 (sinirli), K5 geciyor.
- K3 kirmizi: p95 hicbir kaynakta 150 ms altina inmiyor. T167'nin geri
  donus kriteri geregi LibVLC'ye gecis onerilir; bu sozlesme kapsaminda
  paket eklenmedi.
- K4 sinirli (T167'nin 1,9 ms'sinin cok uzerinde, -138..-99 ms bandinda);
  AudioSink.PositionSeconds'in tampon-bayt tabanli hesabi ayri bir
  incelemeyi hak ediyor.
- K7: PlaybackPipeTests kolu sifir test buluyor, verify satiri bu haliyle
  yanaltici.
