# Danisma — Dar pencere kolu ne olacak (fable, 9 Eylul 2026)

Model: fable. Maliyet: 52.782 belirtec, 25 sn (gorus turu) + 52.140 belirtec, 13 sn
(netlestirme turu). Netlestirme girdileri:
`docs/netlestirme/001-1040x720-de-infogrid-hucreleri-67-px-e-d-girdi.md` ve `002-...`.

## Sorulan (tam metin)

VidShrink projesinde bir karar noktasindayim. Gorusunu istiyorum: bir cumlelik karar,
gerekce, ve sectigin yolun ilk adimi. Kod yazma, dosya okuma; olgular asagida tam.

**Durum.** Test: `tests/VidShrink.Tests/BiciminTests.cs` →
`KareYerlesimTests.KaynakBilgiEtiketleriKendiHucresindeKalir`. Dort kol:
(tr|en) x (1600x1000 | 1040x720). Etiketlerin `TextWrapping`'i gecici kapatilip metin
genisligi kendi hucresinin genisligiyle karsilastiriliyor.

Bugun bulunan: olcu makineye bagimliydi. Bassiz (headless) Avalonia penceresinde
`Width/Height` atamak ise yaramiyor — `window.Bounds` bu makinede 2576x1416 (ekran
boyutu) kaliyordu. Ayni commit burada yesil, CI'da kirmiziydi (hucre 195 px vs 66 px);
etiket genislikleri iki makinede birebir ayni, yani font farki yok. Duzeltme: pencerenin
`Content`'ine (`Layoutable`) `Width/Height` verilerek yerlesim ekrandan koparildi. Artik
olcu her makinede ayni:

- 1600x1000 → InfoGrid 455 px, 4 sutun, hucre ~113 px → dort kol yesil
- 1040x720 → hucre **67 px** → tr'de 4, en'de 3 etiket tasiyor: `Video Kodegi 112,6`,
  `Cozunurluk 94,6`, `HDR Araligi 99,1`, `Kare Hizi 78,5`; `Video Codec 105,6`,
  `Resolution 90,9`, `Duration 72,6`.

Yani kirmizi artik gercek ve her makinede ayni.

**Olgular.** 1. 1040x720 gercek urun kisiti (`MainWindow.axaml:8`,
`MinWidth="1040" MinHeight="720"`); dar kol desteklenen en kucuk boyutun tam kendisi.
2. Kabul edilen davranis yazili degil: uygulamada `TextWrapping="Wrap"` acik, metin
gercekte sariliyor, kirpilmiyor; test sarmayi kapatarak "tek satirda sigmali" olcutunu
dayatiyor. 3. Esik yok: `InfoGrid` `Columns="4" Rows="2"` sabit, kodda sutun sayisina
dokunan satir yok. 4. Kirmizi `main` teknik olarak engellemiyor ama proje kurali
"dotnet test tamami yesil olmadan teslim yok"; 0.3.1 bu kapidan geciyor; siralama
serbest. 5. 1040x720'de baska ogelerin tasip tasmadigi olculmedi. 6. `main`e yalniz T0
birlestirir; isler ayri dallarda T sozlesmesi olarak yurur.

**Uc yol.** (a) Arayuzu simdi degistir: dar pencerede InfoGrid sutun sayisini 4'ten 2'ye
dusur, testi oldugu gibi birak. (b) Testin dar kolunu kaldir, ayri bir T sozlesmesine
yaz, `main`'i bugun yesile dondur. (c) Kirmiziyi oldugu gibi birak, T sozlesmesi ac.

## Donen (tam metin)

**Karar:** (b) — dar kolu testten çıkar, `main`'i bugün yeşile döndür, dar pencere
davranışını ayrı T sözleşmesine yaz.

**Gerekçe.** Kırmızı gerçek ama teşhisi eksik: kabul edilen davranış yazılı değil
(olgu 2), eşik yok (olgu 3), kapsam ölçülmedi (olgu 5). Bu üçü varken (a) bir tasarım
kararını test yeşillensin diye alelacele veriyor — sütunu 2'ye düşürmek 1040x720'de
belki doğru, belki tek taşan öğe InfoGrid değil; ölçmeden değiştirilen arayüz "pin bozuk
arayüzün üstüne çakılır" hatasının tersi, düzeltme pinin üstüne çakılır. (c) ise "yeşil
olmadan teslim yok" kuralını üç işin önünde bekleten bir borç bırakır ve kırmızı `main`
alışkanlık olur ("yerel yeşil, CI kırmızı" on üç itme fark edilmemişti; sürekli kırmızı
sinyali öldürür). (b) bilgiyi kaybetmez: test bugün bulduğu sayıları (67 px, taşan yedi
etiket) sözleşmenin öncülüne taşır, dar kolun kaldırıldığı commit mesajında görünür, ve
sözleşme "önce 1040x720'de ne taşıyor say, sonra eşik belirteci + davranış kararı" diye
kapsamı doğru sırayla açar.

Dikkat: kaldırılan kol sessiz geçen boş bir filtre kolu bırakmasın (bellek notu "ölü
verify kolu"); dört koldan ikiye inince test hâlâ gerçek ölçüm yapıyor olmalı —
1600x1000 kolu ekranı koparılmış yerleşimle koşuyor, bu iyi.

**İlk adım.** `BiciminTests.cs`'te `KaynakBilgiEtiketleriKendiHucresindeKalir`'ın
`InlineData` listesinden 1040x720 iki satırını sil; commit mesajı: "Dar pencere kolu
testten çıkarıldı: 1040x720'de hücre 67 px, tr 4 / en 3 etiket taşıyor, davranış kararı
T<yeni>'ye". Aynı turda `.claude/relay/contracts/T<yeni>.md` aç; öncül olarak yukarıdaki
sayıları ve üç açık soruyu (kabul davranışı: sarma mı, sütun düşürme mi; eşik belirteci
`Theme.axaml`'de; 1040x720'de taşan diğer öğelerin sayımı) yaz. Sonra `dotnet test` koş,
yeşili gör, `main`'e birleştir.

## Uygulanan

Aynen (b). Dar kol `IkiDilIkiOlcu` → `IkiDilTekOlcu` ile cikarildi, sayilar ve uc acik
soru `.claude/relay/contracts/T194.md` icine yazildi.
