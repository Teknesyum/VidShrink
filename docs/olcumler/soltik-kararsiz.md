# SolTikOynatmayiDegistirmez Kararsızlığı

2026-10-10. `OynaticiMedyaAdiTests.SolTikOynatmayiDegistirmez`, v1.2.12 yayın koşumunda
(run 38042086524, ilk deneme, iş 114184212972, `windows-2025-vs2026`) satır 170'te düştü;
aynı commit'in (0fa4b543) main koşumu yeşildi.

## Hüküm

Ürün kusuru değil, test ön koşulu eksikti. Windows'ta testler gerçek Win32 penceresiyle
koşuyor (`AppHost.Backend`). Avalonia'nın isabet sınaması (tıkın hangi denetime düştüğünü
bulan adım) düzenden değil, çizim iş parçacığının son bitirdiği kareden okuyor. Pencere
kare çizmeden gelen ham tıkta isabet boş dönüyor, basış hiçbir denetime gitmiyor ve tık
iz bırakmadan kayboluyor: `LeftClicks` artmıyor, `IsPlaying` değişmiyor.

Testin ilk yarısı (başlığa tık hiçbir şeyi değiştirmez) bu durumda da geçiyordu, çünkü
kaybolan tık da hiçbir şeyi değiştirmez. Yalnız ikinci yarı (boş zemine tık oynatmayı
değiştirir) düşüyordu. CI'daki belirti tam olarak bu.

## Elenen Adaylar

- **Çift tık:** `PlayerView` `ClickCount`'a hiç bakmıyor (`PlayerView.Fare.cs`: basış yalnız
  kurulur, iş bırakışta yapılır).
- **Tek tık gecikme zamanlayıcısı:** yok. Bırakış `FareRelease` içinde eşzamanlı
  `TogglePlay` çağırıyor; 2 sn'lik bekleme hiçbir şeyi beklemiyor.
- **Gerçek imlecin araya girmesi:** basış ile bırakış arasında pencereye `PostMessage` ile
  40 dip ötede `WM_MOUSEMOVE` kondu, 2/2 koşumda tık yine sayıldı; 13 koşumda sıfır yabancı
  hareket olayı. `DenetimSurucu.Pump` Win32 ileti kuyruğunu boşaltmıyor.
- **Giriş canlandırmasında `TranslatePoint`:** 90 koşumda nokta hep aynı (başlık 480,62;
  zemin 480,270), isabet hep `Image#Frame`.

## Ölçüm

Test gövdesi 90 kez (78 düz, 6'sı tıktan önce 1,5 sn uykulu, 6'sı beklemesiz): 90/90 geçti.
Yani yerelde, ısınmış süreçte kendiliğinden çıkmıyor.

Pencere açılıp dosya yüklendikten hemen sonra, hiç beklemeden zemine tık (12 pencere).
`hemenVurus`: o an isabet var mı; `erkenTik`: o tık sayıldı mı; `ilkVurusMs`: açılıştan
isabetin ilk dolu döndüğü ana kadar geçen süre; `sonraDegisti`: isabet dolunca atılan tık
oynatmayı değiştirdi mi.

```
 0 duzen=True bos=480,270 hemenVurus=False erkenTik=0 erkenDegisti=False ilkVurusMs=158,1 tur=54 sonraDegisti=True
 1 duzen=True bos=480,270 hemenVurus=False erkenTik=0 erkenDegisti=False ilkVurusMs=7,0 tur=1 sonraDegisti=True
 2 duzen=True bos=480,270 hemenVurus=True erkenTik=1 erkenDegisti=True ilkVurusMs=5,0 tur=0 sonraDegisti=True
 3 duzen=True bos=480,270 hemenVurus=False erkenTik=0 erkenDegisti=False ilkVurusMs=6,8 tur=1 sonraDegisti=True
 4 duzen=True bos=480,270 hemenVurus=False erkenTik=0 erkenDegisti=False ilkVurusMs=6,7 tur=1 sonraDegisti=True
 5 duzen=True bos=480,270 hemenVurus=False erkenTik=0 erkenDegisti=False ilkVurusMs=5,3 tur=1 sonraDegisti=True
 6 duzen=True bos=480,270 hemenVurus=False erkenTik=0 erkenDegisti=False ilkVurusMs=6,2 tur=1 sonraDegisti=True
 7 duzen=True bos=480,270 hemenVurus=False erkenTik=0 erkenDegisti=False ilkVurusMs=5,5 tur=2 sonraDegisti=True
 8 duzen=True bos=480,270 hemenVurus=False erkenTik=0 erkenDegisti=False ilkVurusMs=5,9 tur=1 sonraDegisti=True
 9 duzen=True bos=480,270 hemenVurus=False erkenTik=0 erkenDegisti=False ilkVurusMs=5,0 tur=2 sonraDegisti=True
10 duzen=True bos=480,270 hemenVurus=False erkenTik=0 erkenDegisti=False ilkVurusMs=4,9 tur=1 sonraDegisti=True
11 duzen=True bos=480,270 hemenVurus=False erkenTik=0 erkenDegisti=False ilkVurusMs=3,9 tur=1 sonraDegisti=True
```

11/12 pencerede erken tık kayboldu; isabet dolar dolmaz atılan tık 12/12 çalıştı. Satır 2
işin bir yarış olduğunu gösteriyor: çizim iş parçacığı yetiştiğinde erken tık da tutuyor.
Süreçteki ilk pencerede bekleme 158 ms, sonrakilerde 4-7 ms.

## Ölçülmeyen

Düşen CI koşumundan iz yok; o koşumda isabetin boş döndüğü doğrudan görülmedi. Testte ikinci
tık pencere açıldıktan yaklaşık 0,85 sn sonra geliyor; koşucuda çizimin o kadar geciktiği
çıkarımdır, ölçüm değil. Mekanizma ve belirti birebir örtüşüyor, başka aday kalmadı.

## Düzeltme

`OynaticiListeTests.SolTik` ve `SagTik` tıktan önce noktanın bir denetime vurmasını bekliyor
(`Cizilsin`). `SolTikOynatmayiDegistirmez` iki tıktan önce de `Vurulur` ile isabetin
başlığa ve zemine düştüğünü doğruluyor; böylece ilk yarı artık kaybolan tıkla boş yere
geçemiyor. Bekleme koşula bağlı, süre eşiği büyütülmedi. İsabet yine yanlış yere düşerse
hata iletisi nereye vurduğunu yazıyor.

CI günlüğü: `gh run view 38042086524 --attempt 1 --job 114184212972 --log`
