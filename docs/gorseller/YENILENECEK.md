# Yenilenecek ekran görüntüleri

Tarih: 2026-09-29. Sürüm 1.1.0 README'si yeniden yazılırken tazelendi.

En taze takım **T200**: dokuz ekran × TR/EN, on sekiz kare. T191'e göre yeni olanlar
düzenleyici karesi (iki kesimli zaman çizelgesi) ve kontrolleri görünen oynatıcı karesi.
T191 takımı `trash/gorseller-T191/`, T190 takımı `trash/gorseller-T190/` altında.

## Hiç görüntüsü olmayan yerler

| Eksik | Neden gerekli | Ne çekilmeli |
|---|---|---|
| **Oynatıcı karşılaştırma paneli** | `T200-oynatici-*.png` yalnız oynatıcıyı gösteriyor, öncesi-sonrası paneli yok. | Karşılaştırma paneli açık, iki kaynak yüklü hâlde. TR ve EN. |
| **Sağ tık menüsü** | Windows 11 birincil menüsündeki girdi hiç belgelenmemiş. | Explorer'da bir videoya sağ tık, "Bu videoyu VidShrink ile aç" birincil menüde görünür hâlde. |

## Çekim kuralları

- Uygulama **Türkçe** açılıyor; İngilizce kare için pencereyi `EN` ile çevirip çekin.
  Başsız/pinli ölçüm düzeneği İngilizce pencereyi görür, elle çekim görmez.
- Dosya adı takım önekiyle başlasın (`T<sözleşme>-<ekran>-<dil>.png`), böylece hangi
  turdan geldiği adından okunur.
- Eski dosyayı **silmeyin**: yeni kare geldiğinde eskisi `trash/` altına taşınır ve
  buradaki satır silinir.
- Görüntüler yalnız `docs/gorseller/` altında durur ve depoya göreli yolla bağlanır.
