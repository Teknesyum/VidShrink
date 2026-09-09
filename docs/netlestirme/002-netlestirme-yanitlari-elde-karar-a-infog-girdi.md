[[netlestirme:002]]

# Netleştirme: Netlestirme yanitlari elde. Karar: (a) InfoGrid'i dar pencerede 4 sutundan 2 sut

İşe başlamadan önce soruyu keskinleştir. Görüş verme, plan yazma, kod yazma.
Yalnız şunu döndür: soruda belirsiz kalan yerler, her biri için tek satırlık bir netleştirme sorusu, en fazla beş. Belirsizlik yoksa "net" yaz.

## Soru

Netlestirme yanitlari elde. Karar: (a) InfoGrid'i dar pencerede 4 sutundan 2 sutuna dusur ve testi oldugu gibi birak, (b) testin dar kolunu kaldirip ayri T sozlesmesine yaz ve main'i bugun yesile dondur, (c) kirmiziyi birak ve T ac. Bir cumlelik karar, gerekce, ve secilen yolun ilk adimi.

## Elde olan olgular

# Olgular — KareYerlesimTests kirmizisi

Test: `tests/VidShrink.Tests/BiciminTests.cs` → `KareYerlesimTests.KaynakBilgiEtiketleriKendiHucresindeKalir`.
Dort kol: (tr|en) x (1600x1000 | 1040x720). Etiketlerin `TextWrapping` gecici olarak
kapatilip metin genisligi kendi hucresinin genisligiyle karsilastiriliyor.

## Bugun bulunan sey

Olcu makineye bagimliydi. Bassiz pencerede `Width/Height` atamak ise yaramiyor:
`window.Bounds` bu makinede 2576x1416 (ekran boyutu) kaliyor, `Measure(olcu)` cagrisina
ragmen. Bu yuzden ayni commit bu makinede yesil, CI kosucusunda kirmizi olusuyordu
(hucre 195 px vs 66 px). Etiket genislikleri iki makinede birebir ayni (72,6 / 90,9 /
105,6 px) — font farki yok.

Duzeltme: pencerenin `Content`'ine (`Layoutable`) `Width/Height` verilerek yerlesim
ekrandan koparildi. Simdi olcu her makinede ayni:

- 1600x1000 → InfoGrid 455 px, 4 sutun, hucre ~113 px → **dort kol da yesil**
- 1040x720  → hucre **67 px** → tr'de 4 etiket, en'de 3 etiket tasiyor:
  `Video Kodegi 112,6`, `Cozunurluk 94,6`, `HDR Araligi 99,1`, `Kare Hizi 78,5`;
  `Video Codec 105,6`, `Resolution 90,9`, `Duration 72,6`.

Yani kirmizi artik gercek: 1040x720'de dort sutunlu `InfoGrid` hucreleri 67 px'e
duşuyor, etiketler sigmiyor. Uygulamada `TextWrapping="Wrap"` acik oldugu icin metin
gercekte sariliyor (kirpilmiyor), test sarmayi kapatarak olcuyor.

## Kisit

`main`e yalniz T0 birlestirir. `main` su an bu testten dolayi kirmizi ve en az uc
kosumdur kirmizi. Elimde bekleyen isler: kareleri yenile, T190 README, 0.3.1 surumu.
Arayuz degisikligi `src/VidShrink.App/MainWindow.axaml` icinde `InfoGrid`in sutun
sayisina dokunmak demek.

## Netlestirme turunun yanitlari

1. **1040x720 gercek urun kisiti.** `MainWindow.axaml:8` → `Width="1560" Height="1060"
   MinWidth="1040" MinHeight="720"`. Dar kol, desteklenen **en kucuk** pencere boyutunun
   tam kendisi.
2. **Kabul edilen davranis yazili degil.** Uygulamada `TextWrapping="Wrap"` acik, metin
   gercekte sariliyor, kirpilmiyor. Test sarmayi kapatarak "tek satirda sigmali"
   olcutunu dayatiyor.
3. **Esik yok.** `InfoGrid` `Columns="4" Rows="2"` sabit (`MainWindow.axaml:270`); kodda
   sutun sayisina dokunan satir yok. Esik secilirse `Theme.axaml`'e yeni belirtec gerek.
4. **Kirmizi main teknik olarak engellemiyor**, ama proje kurali "dotnet test tamami
   yesil olmadan teslim yok"; 0.3.1 bu kapidan geciyor. Siralama serbest.
5. **Kapsam olculmedi.** 1040x720'de baska ogelerin tasip tasmadigi bilinmiyor; olcen
   tek test bu.
