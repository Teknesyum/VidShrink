## UI Denetim Bulguları — VidShrink (2026-09-24)

**Sorun**

- **ana-oynatici** (125% ve 150%, hem önce hem sonra): Alt taşıma çubuğu (play, -10/+10, ses, hız, tam ekran) tamamen boş görünüyor — hiçbir kontrol/simge çizilmiyor. Birincil eylem (play) bu iki ölçekte görünmüyor. 100%'de sorun yok.
- **ana-kucult** (125% ve 150%, hem önce hem sonra): Karşılaştırma Paneli'nin sağ üst köşesindeki iki simge düğmesi (bölünmüş görünüm / tam ekran) boş kutu — içindeki simge glifi görünmüyor. 100%'de simgeler net.
- **ana-kaydedici** (125% ve 150%, hem önce hem sonra): Birincil "Start" düğmesi metinsiz — boş bir hap şeklinde görünüyor, "Start" yazısı yok. Ayrıca renk 100%'dekinden farklı: 100%'de düz camgöbeği zemin + siyah "Start" yazısı, 125/150'de mor-mavi degrade ve metin yok.
- **RecorderMini** (125% ve 150%, hem önce hem sonra): Çubuğun sol ucunda duraklat/durdur simgeleri fazladan tekrarlanıyor — asıl kontroller sağda duruyor ama solda da bir kopyası beliriyor (düzen taşması/çoğalma).

**Sorun Değil**

- **ana-donustur, ana-gelismis, ana-ayarlar, acilir-liste**: Tüm ölçeklerde (100/125/150) ve önce/sonra arasında kırpılmış yazı, taşma veya okunmayan metin yok; 125/150 içerik 100'ün düzgün büyütülmüş hali.
- **RecorderClickRing**: Basit kırmızı halka, metin içermiyor, tüm ölçek ve önce/sonra'da aynı.
- **kucultme-isi**: Metin ve düğmeler tüm ölçeklerde okunur, taşma/kırpma yok. Önce/sonra arasında fark var: "Sırayı Duraklat" ve "İptal" düğmeleri önce'de dolu gri zeminli, sonra'da kontur/şeffaf stiline dönüşmüş — okunabilirlik ve kontrast etkilenmemiş, görünüşe göre kasıtlı bir stil güncellemesi.
- Genel gözlem: "Start" (ana-kaydedici) ve kayıt düğmesi (RecorderMini, 100%) rengi önce'de mor-mavi degrade, sonra'da düz camgöbeği — tutarlı bir tema/renk değişikliği izlenimi veriyor, 100% ölçekte okunabilirlik korunmuş.

**Değerlendirilemedi**

- **RecorderRegionPicker**: Tüm dosyalar (100/125/150, önce/sonra) tamamen boş/beyaz — içerik yakalanmamış, incelenemedi.
- **RecorderFrame**: Sadece kırmızı çerçeve görünüyor, iç içerik boş — bu bir tam ekran overlay olduğu için yakalama aracı içeriği görüntülememiş olabilir.
- **RecorderMagnifier**: Sadece kırmızı çerçeve, içi boş — aynı sebeple değerlendirilemedi.
- **RecorderKeyCaption**: Boş bir kapsül/hap görünüyor, tuş etiketi metni yok — muhtemelen tuşa basılı değilken yakalanmış, okunabilirlik test edilemedi.

Not: 125/150 ölçeklerdeki dört "Sorun" da hem önce hem sonra dosyalarında aynı şekilde mevcut — yani bu turdaki değişiklikten kaynaklanan bir gerileme değil, önceden var olan bir arayüz kusuru olarak görünüyor.
