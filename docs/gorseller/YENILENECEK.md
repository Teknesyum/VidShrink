# Yenilenecek ekran görüntüleri

Tarih: 2026-09-13. README yeniden yazılırken çıkarıldı, aynı gün iki kez tazelendi.

En taze takım **T191**: sekiz ekran × TR/EN, on altı kare. T190 takımı aynı gün üretilmişti
ama üst şerit karelerde hiç görünmüyordu — şerit artık gizli başlayıp fare üste gelince
beliren bir katman, başsız çekimde fare yok. `VidShrink.Shot` kareyi almadan önce
`chrome-hidden` sınıfını kaldırıyor; T191'de başlık çubuğu ve sekme şeridi görünüyor.
T190 takımı `trash/gorseller-T190/` altına taşındı.

## Hiç görüntüsü olmayan yerler

| Eksik | Neden gerekli | Ne çekilmeli |
|---|---|---|
| **Kaydedici sekmesi** | README artık kaydediciyi küçültmeyle eşit görünürlükte duyuruyor, ama T189 takımında kaydedici karesi yok. | Kaydedici sekmesi, **otomatik kip kutucuğu işaretliyken**: seçilen kodlayıcı, kare hızı ve kayıt boyutu kutucuğun altındaki gerekçe satırında görünsün. TR ve EN. |
| **Oynatıcı karşılaştırma paneli** | `T191-oynatici-*.png` yalnız oynatıcıyı gösteriyor, öncesi-sonrası paneli yok. | Karşılaştırma paneli açık, iki kaynak yüklü hâlde. TR ve EN. |
| **Sağ tık menüsü** | Windows 11 birincil menüsündeki girdi hiç belgelenmemiş. | Explorer'da bir videoya sağ tık, "Bu videoyu VidShrink ile aç" birincil menüde görünür hâlde. |

## Çekim kuralları

- Uygulama **Türkçe** açılıyor; İngilizce kare için pencereyi `EN` ile çevirip çekin.
  Başsız/pinli ölçüm düzeneği İngilizce pencereyi görür, elle çekim görmez.
- Dosya adı takım önekiyle başlasın (`T<sözleşme>-<ekran>-<dil>.png`), böylece hangi
  turdan geldiği adından okunur.
- Eski dosyayı **silmeyin**: yeni kare geldiğinde eskisi `trash/` altına taşınır ve
  buradaki satır silinir.
- Görüntüler yalnız `docs/gorseller/` altında durur ve depoya göreli yolla bağlanır.
