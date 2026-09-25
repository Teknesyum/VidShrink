# Plan — Kaydedici Arayüzü (2026-09-25)

Kullanıcı isteği: seçeneklerdeki kutular küçük yazıyla ekranın yarısını kaplıyor; mini
kaydediciye geçiş düğmesi öne çıksın; bölge seçimi işaretlendikten sonra kaybolmasın,
Bandicam gibi büyütülüp küçültülsün, üstünde ince bir düğme paneli olsun.

## A. Bölge Düzenleyici (alt ajan, kendi worktree'si)

- Yeni `Recorder/RecorderRegionEditor.axaml(.cs)`: çizimden sonra kalan pencere. Kırmızı
  çerçeve + 8 tutamak + kenardan sürükleyerek taşıma; üstünde (yer yoksa altında) ince araç
  paneli: ölçü (G × Y), Kaydı başlat, Ayarlar (ana pencereyi öne getir), Kapat.
- Windows'ta pencere biçimi `SetWindowRgn` ile çerçeve halkası + panel: iç alan tıklamayı
  alttaki uygulamaya geçirir, masaüstü kilitlenmez. Diğer sistemlerde eski seçici.
- Kayıt başlayınca düzenleyici gizlenir (`RecorderFrame` zaten var), kayıt bitince geri gelir.
- Bölge değişince `TxtRegionX/Y/Width/Height` ve `StoreChoices` güncellenir; çift sayıya iner.
- Saf hesaplar (tutamak sürükleme → yeni dikdörtgen, oran kilidi, ekran sınırı, panel yeri)
  `RegionDraw`'a ya da yeni saf sınıfa, testleriyle.

## B. Seçenek Genişlikleri (T0)

- Kaydedici panellerindeki açılır kutular tam genişlik yerine belirteçli sabit genişlikte
  alan hücrelerine; hücreler `WrapPanel` ile dizilir, dar pencerede alta kayar.
- Ölçü yalnız `Themes/Theme.axaml` belirteçlerinden.

## C. Mini Kaydedici Teklifi (T0)

- `BtnMini` simgeden etiketli, vurgulu (NeonBlue çerçeve/dolgu) düğmeye döner.
- Mini şeride "Bölge seç" düğmesi eklenir (kayıt yokken görünür).

## Doğrulama

- Dokunulan test sınıfları yerelde; `YerlesimDenetimiTests`, `KontrastTests`, `MiniKipOlcusuTests`.
- Görüntü: `tools/VidShrink.Shot` ya da başsız çekim `.calisma/` altına.
- main'e itme, CI yeşili.
