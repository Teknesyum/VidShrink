# Duzenek — ne olcer, nasil olcer

Kod yorumu yasak; duzenegin gerekceleri burada durur.

## Kollar

`Kollar` uretimin kodek tercihlerini tasir.

| Kol | Tercih | Cikan kodlayici | Neden var |
|---|---|---|---|
| `maks` | `MaxCompression` | `libsvtav1` | Uretim varsayilani |
| `uyumlu` | `Compatible` | `libx264` | Uyumluluk kolu |
| `yedek` | `MaxCompression`, `libsvtav1` gizli | `libx265` | K4 `libsvtav1`in `zones`u sessizce yok saydigini gosterdi; sikistirma ailesinde zone denenebilen tek kodlayici bu yoldan cikar |

`SvtavYok` sarmalayicisi yalniz `IEncoderAvailability`yi ezer; argüman uretimi
gercek yeteneklerle calisir.

## Referans (hak edilen)

Her sahne ayri ayri, sabit `-crf 26` ile, **planin cozunurlugunde** kodlanir.
Olceklendirme sart: `p1` penceresinde plan `806x454`e dusuyor, referans
`1920x1080` kalirsa hak-edilen ile verilen farkli cozunurlukten okunur.

`verilen` sutunu plan ciktisinin sahne araligina dusen paket bitleridir —
kodlayicinin kendi karari. `harita` sutunu `Scene.Bits` paylaridir.
Uc sutun da paydir; toplam boyut farki karsilastirmayi bozmaz.

## Is parcacigi sabitleme

`IsParcacigiArgs` kodlayici basina dogru bayragi secer: x265 `pools`,
x264 `threads`, SVT-AV1 `lp`. Yanlis bayrak ffmpeg tarafindan **uyariyla**
atilir, hata vermez — pinleme sessizce kaybolur.

## K4: cikis kodu destek demek degil

SVT-AV1 ve x264/x265 parametre ayristiricilari tanimadiklari anahtari uyariyla
gecer, cikis kodu 0 doner. Bu yuzden her hucre **iki farkli degerle** kodlanir
ve cikti baytlari karsilastirilir; ayrica ayni parametreyle iki kosumdan
kodlayici basina bir **tekrar gurultusu** olculur. Destek ancak
`fark > gurultu x 2` ve `fark > cikti/100` iken yazilir.

## Zone carpani

`Butce.ZoneCarpanlari` = `Complexity^gamma`, sure agirlikli ortalamasi 1,0'a
normalize, `[0,25 – 4,0]` kiskacinda. Normalizasyon K6'nin sarti: dagitim
bitleri yeniden bolusturur, toplami degistirmez.

`gamma = 1 - qcomp` (varsayilan `qcomp = 0,60`, yani `gamma = 0,40`).
x264/x265 iki gecis hiz denetimi bitleri karmasikliga `qcomp` ussuyle dagitir;
harita tam oranli dagitim onerir (us 1,0). Zone carpani aradaki `1 - qcomp`
farkini kapatir. **Telafi sabiti degil** — kodlayicinin belgelenmis ussuyle
haritanin onerisi arasindaki fark.

## Bozuk harita

`KesimDusur(n)` her n'inci kesimi atar (eksik kesim). `KesimEkle` her sahneyi
ortasindan ikiye boler (fazla kesim). Ikisinde de sahne ici bit yogunlugu duz
kabul edilir; sahne parcalandiginda bit sureyle bolunur.

## Rapor

`Rapor.cs` sayfayi tumuyle olculen JSON/CSV dosyalarindan uretir. Bu projenin
on sekiz kez tekrarlayan kusuru "tablo dogru, onu ozetleyen cumle yanlis"tir;
sayfadaki her sayi ve her karar sozcugu hesaplanir, elle yazilmaz.

## Kosum

    bash tools/sahne-butcesi/01-olcumu-kos.sh

## Ara ciktilar atlanir

Her asama hedef dosyasi varsa atlar. Yeniden olcmek icin o dosyalari silin.
**Uretim kaynagi degistiginde hepsini silin**: `src/` degisirse plan degisir
ve eski cikti yanlis commit'i olcer. T114'te bir kez oldu — `origin/main`
birlestirilince (T107) ayni pencerenin plan cozunurlugu `806x454`ten
`1458x820` / `1728x972` / `1920x1080`e cikti; birlestirme oncesi olculen
butun K1 sayilari silinip bastan kosuldu.

## Kapilarin koda gecirilisinde duzeltilen iki nokta

`ESIKLER.md` olcumden once yazildi ve degismedi. Kapilari hesaplayan kod
sonradan yazildigi icin iki yerde metinden sapmisti; ikisi de ilk `k5-*.json`
uretilmeden once duzeltildi.

1. **"Uc kaynagin en az ikisinde"** — kaynak penceredir. Kollar (`maks`,
   `uyumlu`, `yedek`) esik metninden sonra eklendi. Ilk kod butun kol x pencere
   ciftlerini tek havuzda sayiyordu; boylece iki ayri koldan birer pencere
   "iki kaynak" gibi gorunuyordu. Sayim kol icine alindi: bir kolun kendi uc
   penceresinin en az ikisi esigi gecmelidir.
2. **K7'nin olcusu sabit degil** — esik metni "bozuk haritanin p10 kaybi,
   K5'te olculen p10 kazancindan buyukse" der. Ilk kod sabit `-0,30` puanlik
   bir kayip esigi kullaniyordu; bu esik metinde yok. Kayip artik kendi
   hucresinin (ayni kol, ayni pencere) K5 kazanciyla karsilastiriliyor.

Kapilarin sayisal esikleri degismedi; degisen yalniz sayimin metne uymasidir.

## `.calisma/` icindeki eski csv basliklari

`k1-*.csv`'nin ucuncu sutunu `s.End` yaziyordu ama basligi `bit` idi. Baslik
duzeltildi (`son`). Duzeltmeden **once** yazilmis csv'ler eski basligi tasir;
rapor csv'yi okumaz, `k1-*.json`'dan uretilir. Tam kosum tekrarlandiginda
(`01-olcumu-kos.sh`) basliklar da duzelir.

## VMAF'in reddetmeyecegi dogrulandi

T113'te A/B, `QualityMeter`'in renk uzayi reddi yuzunden olculememisti. Burada
kaynak ve plan ciktisi ayni renk uzayinda oldugu icin ret gelmiyor; kontrol:

    ffprobe -v error -select_streams v:0 \
      -show_entries stream=width,height,pix_fmt,color_transfer,color_primaries \
      -of default=nw=1 <dosya>

Kaynak ve uc kolun plan ciktisi da `yuv420p10le / smpte2084 / bt2020` dondu.
Cozunurluk farki sorun degil: `QualityMeter.RunFilterAsync` testi referansin
cozunurlugune olceklendiriyor. Cozunurluk **kollar arasinda** farkli oldugu
icin puanlar yalniz kol icinde karsilastirilabilir; A/B kol icindedir.

## Ayni cikan bozulma kosulmaz

`KesimDusur` iki sahneli haritada hicbir kesim atamaz, `KesimEkle` her zaman
boler. Bozulma haritayi degistirmediyse o kol kosulmaz: `AbAsync` `bilinmiyor`
yazar. Aksi halde tabloda "bozuk harita" diye duran satir aslinda dogru
haritanin kendisi olur ve K7'nin kaybi yapay olarak sifir cikardi.

## K4'un ikinci adayi neden `qcomp = 1,0`

Kabul kriteri iki aday istiyor: `zones` ve "iki gecis yanliligi". Ikincisi bir
serbest katsayi degil. Iki gecis hiz denetimi biti kabaca `karmasiklik^qcomp`
ile dagitir; harita `karmasiklik^1` (tam oranli) onerir. Ikisini esitleyen
deger `qcomp = 1,0`dir — haritanin onerisinin ayni denklemdeki karsiligi,
uydurulmus bir sabit degil.

Iki adayin farki hedefleme cozunurlugu: `zones` sahne araligina ayri carpan
verir, `qcomp` butun klip icin tek egri degistirir. Bu yuzden ikisi ayni
olcuyle — `MAE(verilen, hak edilen)` — ve ayni hedef boyutta karsilastirilir;
kazanan tabana gore MAE'yi daha cok dusurendir. Kalite olcusu (K5) degil,
K1'in kendi olcusudur: soru "kazandiriyor mu" degil, "K1'deki farki hangisi
kapatiyor".

## K4 ekinde "kazanan" cumlesi tek basina yaniltir

Iki adaydan hangisinin MAE'si dusuk diye sormak, farkin buyuklugunu gizler.
`uyumlu/p1-karisik`'te taban 1,552, `zones` 1,553, `qcomp` 1,546 pp cikti:
`qcomp` "kazandi" ama kazanci 0,006 pp, ayni hucredeki K1 acigi 0,262 pp.
Bu yuzden K4 ekine ikinci bir tablo eklendi — acik, kazanc ve acigin kapanan
orani. Ucu de tablodan hesaplanir; ozet cumlesi hucreleri sayar.

## Sifir cikis koduyla sessiz dusen kosum

Ilk `k4b` toplu kosumu `arac3` dizinindeki ikiliyi cagiriyordu; o dizin bir
sonraki derlemede uzerine yazilinca `hostpolicy.dll` kayboldu. Alti cagrinin
besi "Failed to run as a self-contained app" ile dustu, dongu devam etti,
toplam cikis kodu 0 kaldi. Kosumun bittigini cikis kodundan degil, beklenen
cikti dosyasinin varligindan anla. Ayri kosumlara ayri `-o` dizini verilir;
calisan bir ikilinin dizinine derleme yapilmaz.

## K8 — elle yeniden hesaplanan hucre

Ozet cumlelerin tablodan turemesi yetmez; en az bir hucre elle yeniden
hesaplanip tutuyor mu diye bakildi. `maks/p1-karisik`, rapordaki 28 satirlik
tablodan:

- `MAE verilen` — `|verilen-hak|` toplami 14,11 pp / 28 = 0,504 pp. Rapor 0,504.
- `MAE harita` — `|harita-hak|` toplami 17,64 pp / 28 = 0,630 pp. Rapor 0,630.
- `ters dusen` — iki fark sutununun isareti zit oldugu satirlar: 0, 2, 4, 5, 6,
  7, 8, 9, 10, 13, 15, 17, 19, 23, 25, 27 — 16 satir. Rapor 16/28.

Ucu de tutuyor. K4 izgarasinin ozet cumlesi de ayni sekilde sayildi: `zones`
satiri 5 kodlayicida var, `destek=evet` olan 2 (`libx265`, `libx264`).

## Karari veren kodu da kirmayi dene

`03-duzenek-mutasyonu.sh` dagitim **kuralini** denetliyor; kurali dogru bulup
karari yanlis veren bir rapor programi yine de yanlis sonuc yazardi. Kapi kodu
(`Rapor.Sonuc`, `K5K6`, `K7`) uydurma girdiyle kosuluyor:
`04-kapi-denemesi.sh` once dort sartin da saglandigi bir girdi verir — karar
"koda girer" cikmali, yani kapinin **gecirebildigi** gorulur — sonra sirayla
p10 kaybi, band asimi ve K7 bedeli sartlarini tek tek bozar; her birinde karar
"girmez"e donmelidir. Bes senaryonun sonuncusu dosyalari hic yazmaz: karar
"karar verilemedi" olmali, sessizce "gecmedi"ye dusmemeli.

Fikstur sayilari uydurmadir ve rapora girmez; olculen sey kapinin ayirt edip
etmedigidir. Senaryo tablosu `.calisma/T114/kapi-denemesi.csv`, rapora
"Karari veren kodun kendisi olculdu" basligi altinda girer.
