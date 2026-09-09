[[netlestirme:001]]

# Netleştirme: 1040x720'de InfoGrid hucreleri 67 px'e dusuyor ve kaynak bilgi etiketleri sigmiy

İşe başlamadan önce soruyu keskinleştir. Görüş verme, plan yazma, kod yazma.
Yalnız şunu döndür: soruda belirsiz kalan yerler, her biri için tek satırlık bir netleştirme sorusu, en fazla beş. Belirsizlik yoksa "net" yaz.

## Soru

1040x720'de InfoGrid hucreleri 67 px'e dusuyor ve kaynak bilgi etiketleri sigmiyor. Uc yol var: (a) arayuzu simdi degistir (dar pencerede sutun sayisini 4'ten 2'ye dusur) ve testi oldugu gibi birak, (b) testin dar kolunu kaldirip ayri bir T sozlesmesine yaz, main'i yesile dondur, (c) kirmiziyi oldugu gibi birak ve T sozlesmesi ac. Hangisi? Karar gerekcesiyle.

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
