# Yenilenecek ekran görüntüleri

Tarih: 2026-10-20. VidShrink Player 0.0.1 için tazelendi.

En taze takım **T300**: video gerektirmeyen üç ekran × TR/EN, altı kare (`donustur`,
`kaydedici`, `ayarlar`). Başlıkta "VidShrink Player" yazıyor; README'nin "What The Program
Shows" / "Program Ne Yaptığını Gösterir" bölümü bunlara bağlı. Dönüştür karesi ffmpeg
`testsrc2` deseninden üretilen 12 sn'lik `sample.mp4`'ü `C:\Videos\sample.mp4` yoluyla yükler.

T201 takımından `donustur`, `kaydedici`, `gelismis` ve `kucult-onizleme` kareleri
`docs/kullanim.md` ve `docs/kullanim.tr.md` bağlı olduğu için `docs/gorseller/` altında
duruyor; başlıklarında eski "VidShrink" adı var. README'den düşen `ayarlar`, `hakkinda`,
`oynatici`, `duzenleyici` ve `kucult-yakin` kareleri `trash/gorseller-T201/` altında.

## Bekleyen kareler

| Eksik | Neden bekliyor | Ne çekilmeli |
|---|---|---|
| **Oynatıcı** (`oynatici`) | Gerçek video gerekiyor; hangi video kullanıcının kararı. | Pencereyi dolduran video, üstte sekme çubuğu, altta kontrol şeridi. TR ve EN. |
| **Düzenleyici** (`duzenleyici`) | Aynı. | Kliplere bölünmüş zaman çizelgesi, üstünde monitör, dışa aktarma kipi seçicisi. TR ve EN. |
| **Küçült** (`kucult-onizleme`) | Aynı. | Öncesi-sonrası bölmeli panel, plan, öngörülen boyut. TR ve EN. |
| **Kullanım belgesi kareleri** | `docs/kullanim*.md` hâlâ eski adlı T201 karelerine bağlı. | `donustur`, `kaydedici`, `gelismis` için T300 kareleri `tools/VidShrink.Shot` ile çekilip bağlantılar çevrilir; `kucult-onizleme` video kararını bekler. |
| **Oynatıcı karşılaştırma paneli** | Hiç görüntüsü yok. | Karşılaştırma paneli açık, iki kaynak yüklü hâlde. TR ve EN. |
| **Sağ tık menüsü** | Windows 11 birincil menüsündeki girdi hiç belgelenmemiş. | Explorer'da bir videoya sağ tık, "Bu Videoyu VidShrink Player ile Aç" birincil menüde görünür hâlde. |

Video kareleri için düzenek hazır: `dotnet run --project tools/VidShrink.Shot -- docs/gorseller
<klip.mp4> oynatici,duzenleyici,kucult-onizleme`, `VIDSHRINK_LIBMPV` libmpv-2.dll'i göstermeli.

## Çekim kuralları

- Uygulama **Türkçe** açılıyor; İngilizce kare için pencereyi `EN` ile çevirip çekin.
  Başsız/pinli ölçüm düzeneği İngilizce pencereyi görür, elle çekim görmez.
- Dosya adı takım önekiyle başlasın (`T<sözleşme>-<ekran>-<dil>.png`), böylece hangi
  turdan geldiği adından okunur.
- Eski dosyayı **silmeyin**: yeni kare geldiğinde eskisi `trash/` altına taşınır ve
  buradaki satır silinir.
- Görüntüler yalnız `docs/gorseller/` altında durur ve depoya göreli yolla bağlanır.
