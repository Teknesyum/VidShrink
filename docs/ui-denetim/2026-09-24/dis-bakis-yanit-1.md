## VidShrink UI Denetim Notları — 2026-09-24

**Not:** RecorderRegionPicker, RecorderFrame, RecorderKeyCaption, RecorderMagnifier ekranlarının 3 görüntüsü de (once/sonra/150-sonra) boş/beyaz ya da tek renkli çerçeve — bu overlay'ler ekran görüntüsünde yakalanamamış, içerik değerlendirilemedi.

### Ekran ekran notlar

- **ana-oynatici**: 100-önce/sonra fark yok. 150-sonra'da sağ üstteki dil/bağlantı çubuğu ("English", "Türkçe", "Buy Me a Coffee", "Teknesyum", pencere düğmeleri) kesik, sadece "Engl" görünüyor; alt transport çubuğu (oynat/ses/hız) 150-sonra görüntüsünde hiç görünmüyor.
- **ana-kucult**: 100-önce/sonra fark yok. 150-sonra'da sağdaki "Output" paneli (Stage, Current Output Size, Estimated Time/Output) tamamen kesilmiş, yerine sadece iki küçük simge görünüyor.
- **ana-donustur**: 100-önce/sonra fark yok. 150-sonra'da sağ üst dil menüsü kesik ("Engl") ama "FFmpeg Command" / "Progress" panelleri görünür kalmış.
- **ana-kaydedici**: 100-önce/sonra fark yok. 150-sonra'da birincil eylem düğmeleri ("Start", "Start Buffer") tamamen ekran dışında — görünmüyor.
- **ana-gelismis**: 100-önce/sonra fark yok. 150-sonra'da "Close" düğmesi ve accordion ok (chevron) simgeleri sağ kenardan kesik.
- **ana-ayarlar**: 100-önce/sonra fark yok. 150-sonra'da sağ sütun açıklama metinleri kırpılmış: "Changes every colour in the program and is..." ve "Show Advanced Options Expande..." yarım kalmış, alt paragraf da kesik.
- **kucultme-isi** (iş bildirim kartı): 100-önce/sonra fark yok. 150-sonra'da "Kalan" etiketi ve ilerleme çubuğunun sağ ucu kesik; "Hiçbir Şey Yapma" açılır kutusu ve "İptal" düğmesi ekran dışında, hiç görünmüyor.
- **acilir-liste**: Üç aşamada da metinler tam okunur, kırpılma yok.
- **RecorderMini**: 100-önce/sonra fark yok, düğmeler (tam ekran, ayarlar, duraklat, durdur) görünür. 150-sonra'da şerit tamamen boş — hiçbir düğme görünmüyor.
- **RecorderClickRing**: 100-önce/sonra'da tam daire görünüyor. 150-sonra'da halka sağ ve alt kenardan kırpılmış, tam daire değil.

### Sorun

- Pencere 150% ölçekte sağ kenardan taşma/kırpılma var: ana-oynatici (dil/link çubuğu + alt transport bar kayıp), ana-kucult (Output paneli kayıp), ana-kaydedici (**Start / Start Buffer düğmeleri — birincil eylem — tamamen kayıp**), ana-gelismis (Close + chevron kesik), ana-ayarlar (açıklama cümleleri yarım), kucultme-isi (Kalan etiketi + dropdown + İptal düğmesi kayıp), RecorderMini (tüm araç çubuğu düğmeleri kayıp), RecorderClickRing (halka kırpılmış).
- En kritik: **ana-kaydedici** ve **RecorderMini**'de 150% ölçekte birincil eylem düğmeleri hiç görünmüyor — kullanıcı kaydı başlatamaz/durduramaz durumda kalabilir.

### Sorun Değil

- 100-önce ile 100-sonra arasında hiçbir ekranda görsel fark yok (piksel piksel aynı görünüyor).
- acilir-liste bileşeninde taşma/kırpılma yok, tüm metinler okunur.
- ana-donustur'da 150 ölçekte panel içerikleri (FFmpeg Command, Progress) görünür kalmış, sadece üst menü çubuğu etkilenmiş.

### Değerlendirilemedi

- RecorderRegionPicker, RecorderFrame, RecorderKeyCaption, RecorderMagnifier: görüntüler boş/tek renk, içerik yakalanamamış — bu overlay pencerelerin ekran görüntüsü alma yöntemiyle uyumsuz olabileceğini düşündürüyor, ayrı incelenmeli.
