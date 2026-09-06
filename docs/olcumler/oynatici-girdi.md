# T176 — Oynatıcı sekmesi ve girdi haritası

Tur 1. Bütün sayılar `.calisma/T176/` altındaki ham dosyalardan geliyor; her başlıkta
dosya adı yazılı. Ölçülmeyen yere "ölçülmedi" yazıldı.

Girdi haritası kullanıcının cümlesinin dökümüdür; bu turda değiştirilmedi, eklenmedi.

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
startup tab=5 header=Oynatıcı
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
```

Gönderilen on jest, izdeki on satır: tekerlek, ctrl, shift, ctrl+shift, alt, sağ tık,
boşluk, orta tık, ikinci orta tık, sol tık. Kaybolan jest yok. `seek 60` ve `seek 300`
20 saniyede duruyor çünkü ölçüm klibi 20 saniye — bu kırpma, kaybolma değil.

### Sekme — `.calisma/T176/k1-sekme.txt`

`PlayerTabTests.OynaticiSekmesiSekmeSeridindeVeIkiDildeBasligiVar`: sekme sayısı 6,
oynatıcı 5. sırada; başlık her iki dilde de pencerenin kendi dilinden okundu
(en `Player`, tr `Oynatıcı`); altı sekmenin teması tek değer (`TabItem`).

## K2 — Alt + tekerlek gerçekten bize geliyor mu

**Geliyor.** Windows 11 Pro 10.0.26100, Win32 pencere, uygulama ön planda, imleç
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

Ölçülmedi: tam ekranda **fiziksel** pencere dikdörtgeninin ekran boyutuna eşitlendiği.
Ölçüm pencere durumunu ve geri dönen kaydı ölçüyor, işletim sisteminin verdiği son
dikdörtgeni değil.

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
menu acildi: True
```

## K6 — Sekme ve kabuk birlikte — `.calisma/T176/k6-kabuk.txt`

Kapalı programdan koşum (`.calisma/T176/k1-pencere-yoneticisi.txt`): `VidShrink.App.exe`
tek argümanla (dosya yolu) başlatıldı, açılan pencerenin ilk yazdığı satır
`startup tab=5 header=Oynatıcı`. Yani kabuk yolundan gelen dosya oynatıcı sekmesini açıyor.

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
- Tam ekranda işletim sisteminin verdiği fiziksel pencere dikdörtgeni.
- Explorer'da gerçek çift tıklama (kabuk kaydı T169'un alanı).
- Alt+tekerlek ölçümünün başka bir makinede tekrarı.
