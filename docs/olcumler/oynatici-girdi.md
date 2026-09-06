# T176 — Oynatıcı sekmesi ve girdi haritası

Tur 1 ve tur 2. Bütün sayılar `.calisma/T176/` altındaki ham dosyalardan geliyor; her başlıkta
dosya adı yazılı. Ölçülmeyen yere "ölçülmedi" yazıldı.

Girdi haritası kullanıcının cümlesinin dökümüdür. **Tablodaki dokuz satır değiştirilmedi.**
Tablonun dışında iki klavye tuşu eklendi (`PlayerInputMap.cs:88-94`): `Apps` bağlam menüsünü
açar (K5'in seçtiği ikinci yol), `Escape` tam ekrandan çıkar. İkisi de haritadaki bir satırın
anlamını değiştirmiyor, ona ek geliyor. Tur 1 raporu "değiştirilmedi, eklenmedi" diyordu;
bu cümle yanlıştı ve Escape'ten hiç söz etmiyordu.

## K1 — Dokuz girdinin dokuzu

İki ayrı koşum var: biri süreç içinde gerçek yönlendirilmiş olaylarla, biri Windows 11'in
kendi girdi yığınından `mouse_event`/`keybd_event` ile.

### Süreç içi ızgara — `.calisma/T176/k1-izgara.txt`

Ölçüm: `OynaticiGirdiTests.DokuzGirdininDokuzuIcinOncesiSonrasiOlculur`.
Dosyadaki satır sayısı **9**, aşağıdaki tablo o dosyanın birebir dökümü.

| girdi | öncesi → sonrası |
|---|---|
| tekerlek | konum 0 → 1 sn |
| ctrl+tekerlek | konum 1 → 11 sn |
| shift+tekerlek | konum 11 → 71 sn |
| ctrl+shift+tekerlek | konum 71 → 371 sn |
| alt+tekerlek | yakınlaştırma 1 → 1.24 (konum 371 → 371 sn) |
| sağ tık | oynatma False → True |
| boşluk | oynatma True → False |
| orta tık | tam ekran False → True |
| sol tık | durum 371\|1.24\|False\|True → 371\|1.24\|False\|True (tıklama sayacı 0 → 1) |

Sol tık satırı: dört durum alanı (konum, yakınlaştırma, oynatma, tam ekran) tıklamadan
önce ve sonra karakter karakter aynı. Olayın geldiği ayrı bir sayaçla doğrulandı (0 → 1),
yani girdi düştüğü için değil, haritada karşılığı olmadığı için hiçbir şey olmuyor.

### Pencere yöneticisinden geçen koşum — `.calisma/T176/k1-pencere-yoneticisi.txt`

Kapalı programdan açılan gerçek `VidShrink.App.exe`, imleç pencerenin ortasında, on jest
Windows'un girdi yığınına gönderildi. Uygulamanın kendi yazdığı ham iz:

```
startup-tab=5|header=Oynatıcı
seek 1 -> 1
seek 10 -> 11
seek 60 -> 20
seek 300 -> 20
zoom 1 -> 1,24
play -> True
play -> False
fullscreen -> True
fullscreen -> False
none
menu
```

Gönderilen on bir jest, izdeki on bir satır: tekerlek, ctrl, shift, ctrl+shift, alt, sağ tık,
boşluk, orta tık, ikinci orta tık, sol tık, menu tuşu (Apps). Kaybolan jest yok. `seek 60` ve `seek 300`
20 saniyede duruyor çünkü ölçüm klibi 20 saniye — bu kırpma, kaybolma değil.

### Sekme — `.calisma/T176/k1-sekme.txt`

`PlayerTabTests.OynaticiSekmesiSekmeSeridindeVeIkiDildeBasligiVar`: sekme sayısı 6,
oynatıcı 5. sırada; başlık her iki dilde de pencerenin kendi dilinden okundu
(en `Player`, tr `Oynatıcı`); altı sekmenin teması tek değer (`TabItem`).

## K2 — Alt + tekerlek gerçekten bize geliyor mu

**Geliyor.** Windows 11 Pro 10.0.22631, Win32 pencere, uygulama ön planda, imleç
pencerenin ortasında: ALT basılıyken gönderilen tekerlek çentiği uygulamaya ulaştı ve
`zoom 1 -> 1,24` satırını yazdırdı (`.calisma/T176/k1-pencere-yoneticisi.txt`).

Denetim kolu aynı koşumda: modifiye edilmemiş tekerlek `seek 1`, ctrl `seek 10`,
shift `seek 60` yazdı. Yani "olay ulaşmadı" ile "düzenek çalışmadı" ayrımı yapılabilir
durumdaydı ve alt kolu olayı aldı. Yedek tuş önerisine gerek kalmadı.

Sınır: ölçüm bu makinede, bu masaüstü kabuğunda yapıldı. Alt+tekerleği kapan bir üçüncü
parti araç (pencere yöneticisi eklentisi, fare sürücüsü profili) kurulu bir makinede sonuç
değişebilir; ölçüm başka makinede tekrarlanmadı.

## K3 — Ara isabetli ve birikmeli

### Sahte arama ile — `.calisma/T176/k3-birikme.txt`

10 hızlı tik, tik başı 1 sn: hedef **10 sn**, tetiklenen arama **2**, arama hedefleri
`1, 10`.

### Gerçek `DecoderPipe` ile — `.calisma/T176/k3-gercek-boru.txt`

```
klip: girdi-20sn.mkv sure 20 sn
10 hizli tik (tik basi 1 sn) -> hedef 10 sn, ulasilan konum 10 sn
tetiklenen arama sayisi: 2
arama hedefleri: 1, 10
arama gecikmeleri (ms): 63.4, 76.7
toplam sure: 141,7 ms
ffmpeg surec sayisi: 0 -> 2
T175 150 ms sinirini asan arama: 0
null donen arama (T175 yeniden baslatma tavani): 0
arama hatalari: yok
```

Konum tam 10 sn, "~10" değil. İki arama da T175'in 150 ms sınırının altında (63.4 ve
76.7 ms). On tik on ayrı arama açmıyor: ilk tik hemen gider, kalan dokuz tik uçuştaki
arama bitene kadar tek hedefte birikir ve tek arama olarak kapanır.

Ölçülmedi: sesli kaynakta `SeekAudio`'nun süreç maliyeti. Ölçüm klibi sessiz üretildi,
bu yüzden yukarıdaki iki ffmpeg süreci yalnız görüntü tarafı. Sesli kaynakta T175'in
bilinen kusuru (`SeekAudio` her çağrıda yeni süreç açıyor) bu sayıyı büyütecektir.

## K4 — Tam ekran geçişi — `.calisma/T176/k4-tam-ekran.txt`

```
once      : durum 0 dikdortgen 40,60 900x700 sekme 2
tam ekran : durum 3 dikdortgen 40,60 900x700 sekme 5
geri donus: durum 0 dikdortgen 40,60 900x700 sekme 2
tam ekranda tekerlek: konum 0 -> 1 sn, bosluk: oynatma True
ikinci orta tik: tam ekran False, secilen sekmeler 5,2
```

`durum 3` = `WindowState.FullScreen`. Geri dönüşte üç alan da (boyut, konum, sekme)
girişteki değere birebir eşit; ölçüm kaydın tamamını tek karşılaştırmada denetliyor.
Tam ekrandayken tekerlek ve boşluk çalışmaya devam ediyor. Pencere yöneticisinden geçen
koşumda da iki orta tık `fullscreen -> True` ve `fullscreen -> False` yazdı.

Tur 1'de ölçülmemişti, **tur 2'de ölçüldü:** fiziksel pencere dikdörtgeni ve geri yazma kolu —
aşağıda K16.

## K5 — Bağlam menüsüne yol — `.calisma/T176/k5-menu.txt`

Seçilen yol: **oynatıcı başlığının sağındaki ⋮ düğmesi** (`BtnPlayerMenu`), ve klavyeden
`Apps` (menü) tuşu.

Gerekçe: sağ tık haritada oynat/duraklat olduğu için serbest değil. Uzun basma haritadaki
bir tuşu ikinci bir anlamla yüklerdi — sol tık "şimdilik boş" olduğu için oraya uzun basma
koymak da o satırı sessizce doldurmak olurdu. Düğme haritadaki hiçbir satıra dokunmuyor,
görünür ve erişilebilirlik adı taşıyor.

```
en: Play / pause | Full screen | Reset zoom
tr: Oynat / duraklat | Tam ekran | Yakınlaştırmayı sıfırla
menu izi: menu
```

## K6 — Sekme ve kabuk birlikte — `.calisma/T176/k6-kabuk.txt`

Kapalı programdan koşum (`.calisma/T176/k1-pencere-yoneticisi.txt`): `VidShrink.App.exe`
tek argümanla (dosya yolu) başlatıldı, açılan pencerenin ilk yazdığı satır
`startup-tab=5|header=Oynatıcı`. Yani kabuk yolundan gelen dosya oynatıcı sekmesini açıyor.

Süreç içi koşum aynı yolu ayrıca ölçüyor:

```
argv: ...\.calisma\T176\kabuk-ornek.mp4
ResolveStartupPath: ...\.calisma\T176\kabuk-ornek.mp4
oynatici sekme sirasi: 5
acilistaki sekme: 0 -> 5
sekme basligi: Player
```

Ölçülmedi: kabuk kaydının kendisi (Explorer'da gerçek çift tıklama). Kayıt defteri girdisi
T169'un işi; burada ölçülen, o girdinin uygulamaya geçirdiği argümanın tüketilmesi.

## K7 — Mutasyon — `.calisma/T176/k7-mutasyon.txt`

| mutasyon | kırılan ölçü | sonuç |
|---|---|---|
| (a) ctrl adımı 10 → 1 | `DokuzGirdininDokuzuIcinOncesiSonrasiOlculur`, `TekerlekAdimlariHaritadakiDortSayidir` | 2 kaldı, 6 geçti |
| (b) tekerlek birikmesi kaldırıldı | `OnHizliTikTekHedefteBirikirVeOnAramaAcmaz`, `OnHizliTikGercekBoruyaKarsiBirikirVeAramalarSinirdaKalir` | 2 kaldı, 6 geçti |
| (c) orta tıkın "önceki hale dön" kolu kaldırıldı | `OrtaTikTamEkranaGecerIkincisiOncekiHaleDoner` | 1 kaldı, 7 geçti |

Her mutasyondan önce `dotnet build -c Release --no-incremental`; koşumlarda `--no-build`
kullanılmadı. Mutasyonlar geri alındıktan sonra sözleşmenin `verify` filtresi
69/69 yeşil.

## K8 — Kol sayısı — `.calisma/T176/k8-kol-sayisi.txt`

`verify` satırı üç kol taşıyor; her kol ayrı ayrı `--list-tests` ile sayıldı:

| filtre kolu | test sayısı |
|---|---|
| `OynaticiGirdiTests` | 7 |
| `PlayerTabTests` | 1 |
| `LanguageTests` | 61 |

Sıfır bulan kol yok. `OynaticiGirdiTests` kolunun 7'sinden biri
`OynaticiGirdiTestsGercekBoru` sınıfından geliyor — vstest'in `~` operatörü alt dize
eşlediği için sınıf adı bilerek bu ön eki taşıyor; ham liste dosyada.

Bütün koşum: `dotnet test --filter "OynaticiGirdiTests|PlayerTabTests|LanguageTests"`
→ Başarısız 0, Başarılı 69, Toplam 69.

## `VidShrink.Ffmpeg` tarafında bulunan, düzeltilmeyen kusurlar

Sözleşme hattı okunur ilan ettiği için yalnız bildiriliyor.

1. `DecoderPipe.ContinuousPlayback.Pump` hâlâ `catch { }` ile sessiz; ffmpeg çökünce
   `Faulted` yayılmıyor, oynatma donuyor. Arayüz tarafında kurulan `StallWatch` bu donmayı
   500 ms'de bir kare sayacına bakarak fark ediyor ve `main.player.stalled` metnini
   gösteriyor — ama bu bir örtü, kaynak `DecoderPipe.cs`de duruyor.
2. `SeekAudio` kalıcılık mekanizmasından yoksun. K3 ölçümü sessiz klip üzerinde yapıldığı
   için bu maliyet sayılara girmedi; sesli kaynakta arama başına yeni ses süreci açılacak.
3. `LatestVideoPts` CFR varsayıyor; VFR kaynakta konum sessizce kayar. Bu turda VFR
   kaynakla ölçüm yapılmadı.
4. `ProcessesStarted` arama ile oynatma başlatmalarını ayırmıyor; K3'teki `0 -> 2` sayısı
   bu yüzden yalnız "kaç süreç açıldı" diyor, hangisinin ne için açıldığını demiyor.

## Bu turda ölçülmeyenler

- Sesli kaynakta arama maliyeti ve ses/görüntü senkronu.
- VFR kaynak.
- Explorer'da gerçek çift tıklama (kabuk kaydı T169'un alanı).
- Alt+tekerlek ölçümünün başka bir makinede tekrarı.

---

# Tur 2 — denetim borçları (K9-K17)

Tur 1 denetimi bir KRİTİK ve on bir borç bıraktı. Bu bölümdeki her sayı yine
`.calisma/T176/` altındaki ham dosyalardan; her başlıkta dosya adı yazılı.
Ölçülen ikili `b410bb8a` commit'inden derlendi, `--no-build` kullanılmadı.

## K9 — Üç menü satırının üçü de etki üretiyor

Tur 1'de `BuildMenu` üç `MenuItem` ekliyordu; üçünde de `Click` ve `Command` yoktu.
Şimdi her satır `Tag` olarak bir `PlayerMenuRow` taşıyor, ortak `OnMenuRow` işleyicisine
bağlı, ve o işleyici `PlayerInputMap.MenuRow(row)` komutunu `Apply`'a veriyor.

Ölçüler menü öğesinin **gerçek `Click` yönlendirilmiş olayını** kaldırıyor
(`MenuItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent))`), yani başlığa değil
bağlantıya bakıyor.

| satır | başlık (en) | ölçü | ham dosya | ölçülen etki |
|---|---|---|---|---|
| 0 | Play / pause | `MenuSatiriOynatDuraklatOynatmayiCevirir` | `k9-menu-oynat.txt` | oynatma False → True → False |
| 1 | Full screen | `MenuSatiriTamEkranIkiYondeCalisir` | `k9-menu-tam-ekran.txt` | tam ekran False → True → False, seçilen sekmeler 5,2 |
| 2 | Reset zoom | `MenuSatiriYakinlastirmayiSifirlar` | `k9-menu-sifirla.txt` | yakınlaştırma 1 → 1.72 → 1 |

Dördüncü bir ölçü üç satırı tek döngüde geziyor ve hiçbirinin izsiz kalmadığını
sayıyor — `k9-menu-uc-satir.txt`:

```
satir 0 'Play / pause' -> play -> True
satir 1 'Full screen' -> fullscreen -> True
satir 2 'Reset zoom' -> zoomreset -> 1
satir sayisi: 3
```

Üç satırın üçü de listede, üçünün de karşısında "ETKI YOK" değil bir iz satırı var;
dosyadaki satır sayısı 3.

Menünün açık hâlinin görüntüsü: aşağıda K17.

## K9 mutasyon ızgarası — `.calisma/T176/k9-mutasyon.txt`

Sekiz mutasyon. Her birinden önce `dotnet build -c Release --no-incremental`, sonra
`verify` filtresi Release'te koşuldu; `--no-build` kullanılmadı.

| mutasyon | kalan ölçü sayısı | kırılan ölçüler | koşum |
|---|---|---|---|
| (d) satır 0'ın komutu None yapıldı | 2 | `MenuSatiriOynatDuraklatOynatmayiCevirir`, `UcMenuSatirininUcuDeAyriBirEtkiUretir` | 2 kaldı, 87 geçti |
| (e) satır 1'in komutu None yapıldı | 2 | `MenuSatiriTamEkranIkiYondeCalisir`, `UcMenuSatirininUcuDeAyriBirEtkiUretir` | 2 kaldı, 87 geçti |
| (f) satır 2'nin komutu None yapıldı | 2 | `MenuSatiriYakinlastirmayiSifirlar`, `UcMenuSatirininUcuDeAyriBirEtkiUretir` | 2 kaldı, 87 geçti |
| (g) `item.Click += OnMenuRow;` söküldü | 4 | dört menü ölçüsünün dördü | 4 kaldı, 85 geçti |
| (h) `Conditional("DEBUG")` niteliği kaldırıldı | 1 | `OlcumKancasiUrunIkilisindeDerlenmez` | 1 kaldı, 88 geçti |
| (i) kabuk açılış istisnası yeniden yutuldu | 1 | `OynaticiAcilisHatasiSessizceYutulmaz` | 1 kaldı, 88 geçti |
| (j) her aramaya 200 ms eklendi | 2 | `OnHizliTikTekHedefteBirikirVeOnAramaAcmaz`, `OnHizliTikGercekBoruyaKarsiBirikirVeAramalarSinirdaKalir` | 2 kaldı, 87 geçti |
| (k) tam ekrandan dönüşte dikdörtgen geri yazılmıyor | 3 | `TamEkranGercekPencereDikdortgeniniGeriYazar` + iki `LanguageTests` | 3 kaldı, 86 geçti |

Mutasyonlar geri alındıktan sonra Release koşumu: Başarısız 0, Başarılı 89, Toplam 89.

**İlk turda (i) ve (k) hayatta kaldı** — yani yazdığım ilk iki ölçü hedefledikleri kolu
ölçmüyordu:

- (i): ölçü `ReportPlayerOpenFailure`'ı doğrudan çağırıyordu, `catch` kolunu değil. Ölçü
  gerçek `LoadStartupFileAsync` yoluna bağlandı.
- (k): Win32 tam ekrandan Normal'a dönerken **işletim sistemi zaten eski dikdörtgeni
  geri veriyor**, bu yüzden kolun kaldırılması görünmüyordu. Ölçü artık tam ekrandayken
  pencereyi bilerek 500,480 400x300'e taşıyor; geri yazma kolu olmadan pencere orada
  kalır.

Yukarıdaki (i) ve (k) satırları ölçüler bağlandıktan **sonraki** ikinci koşumdur.

(k) mutasyonu ayrıca iki `LanguageTests` ölçüsünü düşürdü; mutasyon pencereyi tam ekranda
bıraktığı için yan hasar. Hedeflenen ölçü de düştü, mutasyon amacına ulaştı.

## K10 — Girdi haritası cümlesi düzeltildi

Bu raporun 6. satırı düzeltildi (yukarıda). Tablodaki dokuz satır değişmedi; tablonun
dışında `Key.Apps` ve `Key.Escape` eklendi. Escape artık raporda geçiyor.

## K11 — 150 ms tavanı pimlendi — `.calisma/T176/k3-gercek-boru.txt`

Tur 1'de `LatenciesMs` hiçbir assert taşımıyordu. Şimdi
`OnHizliTikGercekBoruyaKarsiBirikirVeAramalarSinirdaKalir` içinde: dizi boş olmayacak, ve
150 ms'yi aşan hiçbir arama olmayacak — aşan varsa hata iletisi aşanların listesini yazıyor.

Bu turun ölçümü:

```
arama gecikmeleri (ms): 32.1, 51.2
T175 150 ms sinirini asan arama: 0
```

Pim (j) mutasyonuyla doğrulandı: `SeekCoalescer.PumpAsync` içine her aramadan önce
200 ms konunca ölçü kalıyor.

## K12 — Kabuk açılış istisnası artık yutulmuyor — `.calisma/T176/k12-acilis-hatasi.txt`

`MainWindow.LoadStartupFileAsync`'teki boş `catch` yerine `ReportPlayerOpenFailure(ex)`:
istisna `PlayerOpenFailure`'a yazılıyor, izleme satırı düşüyor, ve kaynak hata alanı
mevcut `main.error.unusable` + `DescribeFailure` yolundan kullanıcıya gösteriliyor.
Yeni dil anahtarı eklenmedi.

Ölçü gerçek kolu koşuyor: 4 baytlık bozuk `.mp4` ile `MainWindow` açılıyor,
`LoadStartupFileAsync` dispatcher pompalanarak bitiriliyor.

```
dosya: bozuk-ornek.mp4 (4 bayt)
LoadStartupFileAsync bitti mi: True (87 ms)
once  : istisna yok metin ''
sonra : istisna InvalidOperationException: ffprobe failed (1): [mov,mp4,m4a,3gp,3g2,mj2] moov atom not found
        bozuk-ornek.mp4: Invalid data found when processing input
kaynak durumu gorunur True, metin 'This File Cannot Be Used: The source file could not be
decoded; it may be damaged, or this ffmpeg build does not know the format. ...'
```

## K13 — Ölçüm kancası üründen çıktı — `k13-olcum-kancasi.txt`, `k1-pencere-yoneticisi.txt`

İki değişiklik:

1. `PlayerView.Echo` artık `Conditional("DEBUG")` niteliği taşıyor. Aynı derleme
   birimindeki bütün çağrı yerleri Release derlemesinde kayboluyor; çağrı yerleri Release'te
   derlenmiyor, `VIDSHRINK_T176_TRACE` yolu asla koşulmuyor.
2. `PlayerView.OpenedMenu` kaldırıldı — yalnız testin okuduğu ölü alandı. K5 ölçüsü artık
   `Trace`'in son satırına bakıyor.

Yansıma ölçüsü (`k13-olcum-kancasi.txt`):

```
PlayerView.Echo kosullari: DEBUG
OpenedMenu ozelligi duruyor mu: False
```

Deneysel doğrulama, aynı çevre değişkeniyle **Release** ikilisi çalıştırıldı
(`k1-pencere-yoneticisi.txt` son bölümü):

```
--- K13: Release ikilisi, ayni VIDSHRINK_T176_TRACE cevre degiskeni ---
release pencere tutamaci: 1771578
gonderildi: release: tekerlek
gonderildi: release: sag tik
release iz dosyasi olustu mu: False
```

Debug ikilisi aynı değişkenle 11 satır yazdı, Release ikilisi dosyayı hiç oluşturmadı.

## K14 — Ham kayıt HEAD'in ikilisinden yeniden alındı — `.calisma/T176/k1-pencere-yoneticisi.txt`

Tur 1'in kaydı `startup tab=5` biçimindeydi ve `f5ea51cf` dönemine aitti; biçim
`09b7d8c2` ile değişmişti. Yeniden alınan kayıt HEAD `b410bb8a`'nın Debug ikilisinden:

```
startup-tab=5|header=Oynatıcı
seek 1 -> 1
seek 10 -> 11
seek 60 -> 20
seek 300 -> 20
zoom 1 -> 1,24
play -> True
play -> False
fullscreen -> True
fullscreen -> False
none
menu

gonderilen jest sayisi: 11; ize dusen olay satiri: 11
iz beklenen 11 satirla birebir mi: True
toplam jest kolu denemesi: 1, temiz kosum elde edildi mi: True
```

On bir jest gönderildi, ize on bir olay satırı düştü, ve **karşılaştırmayı ben değil betik
yaptı**: beklenen on bir satırlık dizi betiğin içinde yazılı, koşum ancak birebir eşitse
kabul ediliyor.

### Bu kolun neden kendini denetlemesi gerekti — `.calisma/T176/k14-desktop-gurultu.txt`

İlk üç jest koşumunda ize betiğin **göndermediği** girdi karıştı: yüzlerce ardışık
`seek -1`, üst üste `play` satırları. Bu masaüstü paylaşımlı ve ölçüm sırasında başka
bir el pencereye girdi gönderiyor.

Denetim kolu — uygulama açılır, hiçbir jest gönderilmez, 30 sn beklenir:

| koşum | iz satırı | gönderilmemiş girdi | ham dosya |
|---|---|---|---|
| 1 (15:36:25) | 6 | **5** | dosya üzerine yazıldı, alıntı oturum dökümünden |
| 2 (15:37:08) | 1 | 0 | `k14-bos-kol-temiz.txt` |

Gürültü aralıklı. Bu yüzden ölçüm betiğine, izi beklenen diziyle karşılaştıran ve
eşleşmezse denemeyi atan bir döngü kondu (en fazla 6 deneme). Rapora giren koşum
ilk denemede temiz çıktı.

**Sınır:** pencere yöneticisi düzeyindeki bu koşum bu masaüstünde her seferinde
tekrarlanabilir değil; tekrarlanabilir olan, süreç içi ızgara (K1) ve ölçü paketidir.

## K15 — Makine iddiası ölçüye indirildi — `.calisma/T176/k1-pencere-yoneticisi.txt`

Tur 1 raporu "Windows 11 Pro 10.0.26100" diyordu; bu sayı hiçbir ham dosyada yoktu ve
kanıt yolları başka bir kullanıcı klasörünü gösteriyordu. Ölçüm betiği artık kökü kendi
konumundan türetiyor ve makine olgularını dosyanın başına yazıyor:

```
olcum zamani          : 2026-09-06 15:39:06
isletim sistemi       : Microsoft Windows 11 Pro 10.0.22631 yapi 22631
arayuz dili / bicim   : tr-TR / tr-TR
makine / kullanici    : DESKTOP-0J80KVV / Administrator
depo koku             : ...\Desktop\Projeler\VidShrink-T176
dal / HEAD            : T176-oynatici-girdi / b410bb8a
src+tests kirli mi    : hayir
Debug   ikilisi       : 221696 bayt, 2026-09-06 15:33:25, ...\bin\Debug\net8.0\VidShrink.App.exe
Release ikilisi       : 221696 bayt, 2026-09-06 15:33:22, ...\bin\Release\net8.0\VidShrink.App.exe
```

Düzeltme: sürüm **10.0.22631**, 10.0.26100 değil. Tur 1'in K2 bölümündeki sürüm cümlesi
o ölçümün yapıldığı makineye ait olabilir ama bu depoda kanıtı yok; bu turun ölçümü
yukarıdaki makinede yapıldı.

`header=Oynatıcı` satırındaki Türkçe başlık **makinenin dilinden** geliyor: arayüz
kültürü `tr-TR`. Koddan gelen bir varsayılan değil; `LanguageTests` iki dili ayrıca
ölçüyor.

## K16 — Tam ekranın gerçek pencere kolu — `.calisma/T176/k16-pencere-geri-yazma.txt`

Tur 1'in `40,60 900x700` dikdörtgeni testin elle kurduğu `WindowSnapshot`'tı;
`ToggleFullscreen`'in `window.Position/Width/Height` geri yazma kolu hiç ölçülmemişti.
Yeni ölçü gerçek Win32 penceresi kuruyor ve dikdörtgeni işletim sisteminden okuyor:

```
once     : durum 0 dikdortgen 140,90 900x700
tam ekran: durum 3 dikdortgen 0,0 2560x1440
kaydirildi: durum 0 dikdortgen 500,480 400x300
geri     : durum 0 dikdortgen 140,90 900x700
arka uc: Win32
```

`kaydirildi` satırı pimin kendisi: tam ekrana geçtikten sonra pencere bilerek başka bir
yere ve boya taşınıyor. İkinci orta tık onu 140,90 900x700'e **geri yazıyor**. Bu adım
olmadan (k) mutasyonu hayatta kalıyordu — işletim sistemi tam ekrandan dönerken eski
dikdörtgeni kendisi geri veriyor ve kolun varlığı görünmüyordu.

Tam ekranda fiziksel dikdörtgen ekranı kaplıyor: 0,0 2560x1440.

## K17 — Menünün görüntüsü — `.calisma/T176/k17-menu.png`

Kapalı programdan açılan gerçek `VidShrink.App.exe`, `Apps` (menü) tuşu gönderildi, menü
açıkken pencerenin tamamı yakalandı: `k17-menu.png`, 2576x1416, 131399 bayt.
Sağ üst kırpması `k17-menu-sag-ust.png`.

Görüntüde okunan: başlığın sağındaki ⋮ düğmesi ve altında açılmış menü, üç satır —
`Oynat / duraklat`, `Tam ekran`, `Yakınlaştırmayı sıfırla`. Makinenin dili `tr-TR`
olduğu için başlıklar Türkçe.

## Kol sayısı — `.calisma/T176/k8-kol-sayisi.txt`

`verify` satırının üç kolu, `--list-tests` ile ayrı ayrı sayıldı:

| filtre kolu | test sayısı |
|---|---|
| `OynaticiGirdiTests` | 14 |
| `PlayerTabTests` | 1 |
| `LanguageTests` | 74 |

Sıfır bulan kol yok. `OynaticiGirdiTests` kolu 7'den 14'e çıktı: tur 2'nin yedi yeni
ölçüsü. Yeni sınıflar `OynaticiGirdiTestsMenuSatirlari`, `OynaticiGirdiTestsPencereYazma`,
`OynaticiGirdiTestsKabukHatasi` — vstest'in filtresi alt dize eşlediği için üçü de bu ön
eki taşıyor. `LanguageTests` 61'den 74'e `main`den gelen birleşmeyle çıktı.

Bütün koşum: `dotnet test --filter "OynaticiGirdiTests|PlayerTabTests|LanguageTests"`
→ Başarısız 0, Başarılı 89, Toplam 89. Aynı filtre üst üste üç kez koşuldu, üçünde de
89/89.

## Tur 2'de eklenen ölçüler

Yedi tane; `OynaticiGirdiTests` kolunun 7 → 14 farkı bu yedi:

1. `OynaticiGirdiTestsMenuSatirlari.MenuSatiriOynatDuraklatOynatmayiCevirir`
2. `OynaticiGirdiTestsMenuSatirlari.MenuSatiriTamEkranIkiYondeCalisir`
3. `OynaticiGirdiTestsMenuSatirlari.MenuSatiriYakinlastirmayiSifirlar`
4. `OynaticiGirdiTestsMenuSatirlari.UcMenuSatirininUcuDeAyriBirEtkiUretir`
5. `OynaticiGirdiTestsMenuSatirlari.OlcumKancasiUrunIkilisindeDerlenmez`
6. `OynaticiGirdiTestsPencereYazma.TamEkranGercekPencereDikdortgeniniGeriYazar`
7. `OynaticiGirdiTestsKabukHatasi.OynaticiAcilisHatasiSessizceYutulmaz`

## Tur 2'de de ölçülmeyenler

- Sesli kaynakta arama maliyeti ve ses/görüntü senkronu (T175'in alanı).
- VFR kaynak.
- Explorer'da gerçek çift tıklama (kabuk kaydı T169'un alanı).
- Alt+tekerlek ölçümünün başka bir makinede tekrarı.
- Menü satırlarının fare ile tıklanması: ölçü `Click` yönlendirilmiş olayını kaldırıyor,
  gerçek fare tıklaması değil. Menünün açık hâli ekran görüntüsüyle var (K17), ama
  görüntüdeki bir satıra fareyle tıklama ölçülmedi.

## Bilinen sınır — 150 ms tavanı

`OnHizliTikGercekBoruyaKarsiBirikirVeAramalarSinirdaKalir` içindeki 150 ms tavanı bir
türetilmiş sabit değil, `docs/olcumler/oynatici-boru.md`'deki bir cümleden geliyor.
Yapıcı koşumu 32.1/51.2 ms, denetçi koşumu aynı makinede 77.2/110.4 ms ölçtü — aynı pim,
aynı makinede iki kat fark. `docs/olcumler/oynatici-boru.md:347` T175'in kendi ölçümünde bu
metriğin gerçek kaynaklarda 170-822 ms olduğunu söylüyor. Bu turun assert'i tek bir 20
saniyelik sessiz sentetik klibe dayanıyor; daha yavaş bir makinede ya da gerçek bir
kaynakla kırmızıya dönebilir.
