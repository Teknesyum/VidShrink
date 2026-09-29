# Danışma 016 girdi: VidShrink tasarım güncellemesinin değerlendirmesi

Ajana giden metin:

---

[[danisma:016]]

# VidShrink tasarım güncellemesi — değerlendirme

## Kullanıcının cümlesi (aynen)
"tasarım update i atmıştık bunu fable da değerlendirsin önerileri olursa söyle"

## Ne değerlendirilecek
1.1.0 (2026-09-29) arayüz turu ve ardından gelen değişiklikler. Kaynak: `CHANGELOG.md`, `[Unreleased]` ve `[1.1.0]` bölümleri.
Ana başlıklar:
- Küçült, Dönüştür, Kaydedici ve Ayarlar 1920x1080'de %100 ölçekte kaydırmasız sığıyor; kartlar boş panel bırakmak yerine sütunlara akıyor (`izi:SutunIzgara`).
- Kaydedici sıkı kartlara bölündü; tampon kendi Gelişmiş kartında; 1024 px'te Gelişmiş sayfa kaydırmasız, ipuçları tooltip'e taşındı.
- Oynatıcı: duraklatma çubukları açmıyor, çubuklar yalnız fare kenara gelince; arama önizlemesi çerçevesiz; orta tuş ekranın 1/3'ü kadar ortalı pencere / tam ekran; duraklatma simgesi palet mavisi.
- Üst sekmeler: seçili olmayan beyaz, seçili mavi.
- Sağ tık menüsü gruplandı, yanında oynatma listesi açılıyor.

## Kanıt
Ekran görüntüleri (T200 takımı, TR ve EN): `docs/gorseller/T200-*.png` — kucult, onizleme, kaydedici, gelismis, hakkinda, duzenleyici, oynatici ve diğerleri. Hepsini aç ve bak.
Tasarım kuralları: `docs/tasarim/kabuk-standardi.md`, `docs/tema.md`. Renk yalnız `src/VidShrink.App/Themes/Palette/<Ad>/Theme.axaml`'dan, ölçü yalnız `src/VidShrink.App/Themes/Theme.axaml` belirteçlerinden; yeni renk ya da ölçü uydurma.
Önceki danışma: `docs/danisma/015-fable-oynatici-menu-panel.md`.

## İstenen
- Kod yazma, dosya düzenleme. Yalnız oku ve değerlendir.
- En fazla 10 öneri, önem sırasıyla. Her biri: hangi ekran, ne görülüyor (kareden), ne önerilir, hangi belirteç/dosya, tahmini iş (küçük/orta/büyük).
- İyi olan ve dokunulmaması gerekenleri 3 maddede say.
- Kareden görmediğin şeyi iddia etme; emin olmadığını "doğrulanmalı" diye işaretle.
- Türkçe yaz, kısa.
