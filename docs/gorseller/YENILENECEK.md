# Yenilenecek ekran görüntüleri

Tarih: 2026-09-30. Sürüm 1.2.2 (40fd9c50) için tazelendi.

En taze takım **T201**: dokuz ekran × TR/EN, on sekiz kare. Oynatıcı, Küçült ve düzenleyici
kareleri `sunum-prototip.mp4`'ün rastgele anlarından 12 sn'lik kesitlerle çekildi. Küçült iki
kare: `kucult-onizleme` (hedef 0,15 MB, karşılaştırma paneli ve bölme çizgisi görünür) ve
`kucult-yakin` (aynı panel uygulama içinde %196). Eski `T201-kucult-*` ve `T201-onizleme-*`
`trash/gorseller-T201-eski/` altında. T200 takımı `trash/gorseller-T200/`, T191 takımı
`trash/gorseller-T191/` altında.

## Hiç görüntüsü olmayan yerler

| Eksik | Neden gerekli | Ne çekilmeli |
|---|---|---|
| **Oynatıcı karşılaştırma paneli** | `T201-oynatici-*.png` yalnız oynatıcıyı gösteriyor, öncesi-sonrası paneli yok. | Karşılaştırma paneli açık, iki kaynak yüklü hâlde. TR ve EN. |
| **Sağ tık menüsü** | Windows 11 birincil menüsündeki girdi hiç belgelenmemiş. | Explorer'da bir videoya sağ tık, "Bu videoyu VidShrink ile aç" birincil menüde görünür hâlde. |

## Çekim kuralları

- Uygulama **Türkçe** açılıyor; İngilizce kare için pencereyi `EN` ile çevirip çekin.
  Başsız/pinli ölçüm düzeneği İngilizce pencereyi görür, elle çekim görmez.
- Dosya adı takım önekiyle başlasın (`T<sözleşme>-<ekran>-<dil>.png`), böylece hangi
  turdan geldiği adından okunur.
- Eski dosyayı **silmeyin**: yeni kare geldiğinde eskisi `trash/` altına taşınır ve
  buradaki satır silinir.
- Görüntüler yalnız `docs/gorseller/` altında durur ve depoya göreli yolla bağlanır.
