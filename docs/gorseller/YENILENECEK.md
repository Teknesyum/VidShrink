# Yenilenecek ekran görüntüleri

Tarih: 2026-09-13. README yeniden yazılırken çıkarıldı.

`docs/gorseller/` altında 72 dosya var. En taze takım **T189** (8 Eylül 2026); onun
dışındaki her arayüz görüntüsü 22–31 Ağustos tarihli ve arayüz o gün bugün değişti.
README yeniden yazılırken bağlantısı olan her yer mümkün olduğunca T189'a çevrildi;
aşağıda kalan bayatlar ve hiç görüntüsü olmayan yerler duruyor.

**Yeni görüntü bu turda üretilmedi** — pencere açma yetkisi yoktu. Aşağıdaki tablo, kim
ölçüm makinesine oturursa onun çekeceği listedir.

## Hâlâ bayat olan, kullanımda duran dosyalar

| Eski dosya | Tarih | Nerede kullanılıyor | Ne çekilmeli |
|---|---|---|---|
| `t27-kodek-en.png` | 24 Ağu | `docs/kullanim.md:102` | Küçült sekmesinde kodek ipucu açıkken, İngilizce pencere. İpucu metni T27'den beri değişti; yeni metinle çekilmeli. |
| `t27-kodek-tr.png` | 24 Ağu | `docs/kullanim.tr.md:100` | Aynı kare, Türkçe pencere. |
| `macos-paket-uygulama.png` | 30 Ağu | `docs/kurulum.md:156`, `docs/kurulum.tr.md` | macOS'ta kendi uygulama paketinden açılmış pencere, altında Dock. macOS alt sürümü 15'e çıktı; 15 ya da üstünde yeniden çekilmeli. |

## Hiç görüntüsü olmayan yerler

| Eksik | Neden gerekli | Ne çekilmeli |
|---|---|---|
| **Kaydedici sekmesi** | README artık kaydediciyi küçültmeyle eşit görünürlükte duyuruyor, ama T189 takımında kaydedici karesi yok. | Kaydedici sekmesi, **otomatik kip kutucuğu işaretliyken**: seçilen kodlayıcı, kare hızı ve kayıt boyutu kutucuğun altındaki gerekçe satırında görünsün. TR ve EN. |
| **Oynatıcı karşılaştırma paneli** | `T190-oynatici-*.png` yalnız oynatıcıyı gösteriyor, öncesi-sonrası paneli yok. | Karşılaştırma paneli açık, iki kaynak yüklü hâlde. TR ve EN. |
| **Sağ tık menüsü** | Windows 11 birincil menüsündeki girdi hiç belgelenmemiş. | Explorer'da bir videoya sağ tık, "Bu videoyu VidShrink ile aç" birincil menüde görünür hâlde. |

## Çekim kuralları

- Uygulama **Türkçe** açılıyor; İngilizce kare için pencereyi `EN` ile çevirip çekin.
  Başsız/pinli ölçüm düzeneği İngilizce pencereyi görür, elle çekim görmez.
- Dosya adı takım önekiyle başlasın (`T<sözleşme>-<ekran>-<dil>.png`), böylece hangi
  turdan geldiği adından okunur.
- Eski dosyayı **silmeyin**: yeni kare geldiğinde eskisi `trash/` altına taşınır ve
  buradaki satır silinir.
- Görüntüler yalnız `docs/gorseller/` altında durur ve depoya göreli yolla bağlanır.
