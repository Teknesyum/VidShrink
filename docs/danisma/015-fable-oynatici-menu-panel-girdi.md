# Danışma 015 girdi: VidShrink oynatıcı — sağ menü ve üst/alt panel danışması

Ajana giden metin:

---

[[danisma:015]]

# VidShrink oynatıcı — sağ menü ve üst/alt panel danışması

## Kullanıcının cümleleri (aynen)
1. "oynaytılan dosyanın konumunu aç seçeneği olsun sağ menüde sağ menümüzü fable ve sen düzenle çok fazla element var daha küçük olmalı gruplandırma yaparak sorunu çöz"
2. "oynatıcının alt paneli daha ince olsun üst tarafta iki panel var alt panel fare çıkmasıyla anlık kapanıcak üst panelde 2 tane değil farenin gelmesiyle açılan tek bir panel olacak üstbar için teknesyum ui da anahat kullanılmayacak butonlarda demiştik neden uygulamadın rapor ver ui a da rapor yaz Teknesyum un <> tarzı saçma sapan bir simgesi var kaldır onu alt panelin yarı saydam tasarımı üst panelde de olsun yarı saydam zone daha kısa olsun iki panelde de"

## Bugünkü sağ menü (ekrandan okundu, yukarıdan aşağı; — ayırıcı)
Ayarlar ▸ (uygulama ayarları, ekran görüntüsü klasörü, Gelişmiş ▸, Oynatıcı kısayolları ▸ [tüm kısayol tablosu])
Oynat/duraklat Space · Durdur Ctrl+Space · Başa git Backspace · Tam ekran F · Yakınlaştırmayı sıfırla
—
Sesi kapat/aç M
—
Ses parçası ▸ · Altyazı ▸
—
Hızlandır C · Yavaşlat X · Normal hız Z
—
Sonraki kare . · Önceki kare ,
—
En-boy oranı Ctrl+F5 · 90° döndür Ctrl+Shift+S · Aynala Ctrl+H (işaretli) · Her zaman üstte Ctrl+A (işaretli) · Medya bilgisi Ctrl+F1 (işaretli) · Ekran görüntüsü kaydet Ctrl+E
—
Önceki dosya PageUp · Sonraki dosya PageDown · Karıştır Ctrl+Shift+Alt+F (işaretli) · Tekrar kipi Ctrl+Shift+Alt+B
—
Tekrar başını işaretle (A) [ · Tekrar sonunu işaretle (B) ] · A-B tekrarını kaldır /
—
Yer imi ekle N · Sonraki yer imine git B · Önceki yer imine git Shift+PageUp
—
Son dosyalar ▸
—
Araçlar ▸ (Klip kes, GIF, — , Mini kip (işaretli), URL aç)

Toplam üst düzey satır: 34 (5 alt menü). Menü 1456x819 ekranda neredeyse tüm yüksekliği kaplıyor.
Kod: Keymap.cs'te her eylemin MenuGroup numarası var (1..8); BuildMenu bunları sırayla dizer, sonra Ses/Altyazı, Son dosyalar, Araçlar eklenir. Kısayolların hepsi klavyede de var, alt şeritte oynat/ses/hız/tam ekran düğmeleri var.

## Üst/alt panel olguları (kodda ölçüldü)
- Alt şerit: 12 px dış boşluklu, 1 px mavi kenarlı yuvarlak kart; iki satır (40 px arama çubuğu + 48 px düğme satırı), toplam ≈122 px + 12 alt boşluk. Arkasında alttan yukarı %80 siyah → saydam gradyan (scrim), bölge yüzey yüksekliğinin %25'i.
- Alt şerit gizleme gecikmesi 360 ms; kullanıcı "anlık" istiyor. Önizleme küçük resmi (ThumbChip) şerit gizlenince ekranda kalıyor (ekran görüntüsünde görüldü).
- Üst: pencere başlık çubuğu 28 px, opak yatay gradyan arka plan + alt kenarında mavi çizgi. Aynı satırda solda sekmeler (Oynatıcı, Küçült, Dönüştür, Kaydedici, Ayarlar — sekme şablonundan, TitleBar katmanından ayrı bir öğe), sağda dil düğmeleri, Destek Ol, Teknesyum, pencere düğmeleri (ayrı bir katman: TitleBarLayer). Yani üstte üç ayrı öğe tek satırda üst üste bindirilmiş: TitleBar (zemin), sekme sunucusu, TitleBarLayer. Oynatmada fare pencerenin üst %25'ine girince üçü birlikte açılıyor. Oynatıcı yüzeyinin üstünde ayrıca videonun üst siyah şeridi (letterbox) duruyor.
- Başlık düğmelerinin hepsinde (sekmeler, dil, Destek Ol, Teknesyum) 1 px kenarlık var (DisabledColor %30). Teknesyum UI önizlemesinde "Anahatsız Düğmeler" bloğu diyor ki: sekme, çip ve pencere düğmelerinde anahat yoktur; üzerine gelince yazı Renk 2 yazı kesimine döner, metnin altında ortadan açılan çizgi belirir; seçili sekmede yazı gövde rengine döner ve gösterge çizgisi sabit durur. Tek istisna çerçeveli çip. Bu kural raf kitabında (ui-duzeni), tarayıcı kurallarında ve `uc` akışında yok; yalnız önizleme sayfasında.
- Teknesyum düğmesinin simgesi </> (IconCode).

## Sorular
S1. Sağ menüyü yeniden kur: en fazla ~12 üst düzey satır. Hangi gruplar/alt menüler, hangi eylemler üst düzeyde kalır, hangileri alt menüye iner, hangileri menüden tümden çıkar (klavyede ve şeritte zaten olanlar)? "Dosya konumunu aç" nereye? Somut ağaç ver (Türkçe etiketlerle).
S2. "üst tarafta iki panel var ... üst panelde 2 tane değil farenin gelmesiyle açılan tek bir panel olacak" cümlesi yukarıdaki olgulara göre en olası neyi anlatıyor ve tek panel nasıl kurulmalı (sekmeler + sağ düğmeler tek yarı saydam şeritte mi)? Yarı saydam tasarımı üstte nasıl uygularız (alt şeritteki scrim'in aynası mı, kart mı)?
S3. "yarı saydam zone daha kısa olsun iki panelde de": %25 bölge ve scrim yüksekliği için makul yeni değer?
S4. Alt şerit "daha ince": iki satırı tek satıra mı indirelim, yoksa ölçüleri mi küçültelim? Somut ölçü önerisi (hedef toplam yükseklik).
Kısa, maddeli cevap ver; her kararın gerekçesi bir satır.
