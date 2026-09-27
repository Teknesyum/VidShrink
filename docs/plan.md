# Plan: Uc 0.20.0, Arayüzün Düzene Bağlanması

2026-09-27. İstek: `uc`. Eklenti 0.20.0 uc'yi dönüştürme emri sayıyor: renk, ölçü, köşe, süre,
eğri ve yazı değerleri `teknesyum-ui/` altındaki üretilmiş kaynaklardan gelir; marka, destek,
site, güncelleme, eşitleme yazıları ve pencere başlığı `labels.*.json`'dan okunur.

## Başlangıç Ölçümü

- `setup.js --apply --template benim`: 23 dosya, "düzen eşleşmesi 0 fark".
- Kurulum `.claude/teknesyum-ui.json`'ı yeniden yazıp 18 muafiyeti düşürdü; geri kondu.
- `scan.js`: 0 hata, 6 uyarı (`.calisma/t0-uc2/scan-0.txt`): 4 elle yazılmış imza yazısı,
  1 `fixed-window-no-shrink`, 1 `tools/ikon/README.md` sayı etiketi.
- Üretilen `teknesyum-ui/avalonia/Theme.axaml` 178 anahtar, proje `Themes/Theme.axaml` 151;
  ortak 10. Proje ölçüleri kendi adlarıyla elle yazılmış (SpaceXs, RadiusControl, MotionFast…).
- Yazı tipi iki kopya: `src/VidShrink.App/Fonts` (eski) ve `Assets/Fonts` (kurulumun).

## Karar

- Proje anahtarları kalır (çağıranlar kırılmasın), değerleri üretilmiş anahtarlara bağlanır:
  `Themes/Theme.axaml` üretilmiş Theme.axaml'ı birleştirir, kendi anahtarı üretilene
  `StaticResource` takma adı olur. Eşi olmayan ölçü (pencere, oynatıcı parçaları) üretilen
  ölçekten türetilir ya da gerekçesiyle rapora yazılır.
- 36 palet kalır. Teknesyum paleti rengini `teknesyum-ui/theme.tokens.json`'dan alır;
  diğer 35 palet aynı rol şemasıyla (Renk1-3, On*, Danger, Warning…) yazılır.
- Yazı tipi tek kopya: `Assets/Fonts` (kurulum her `--apply`'da orayı yazar); eski `Fonts/` `trash/`'e.
- İmza yazıları (`Teknesyum`, "Buy me a coffee") `labels.<dil>.json`'dan okunur.
- Kısayol simgesi: kullanıcının masaüstü kurulumuna dokunulmaz (güvenlik kuralı); 0.10.0
  kurulumu simgeyi exe'den taşıyor. Rapora "uygulanmayan" olarak yazılır.

## İşler

| # | İş | Sahip | Bağımlılık |
|---|---|---|---|
| 1 | Token bağlama, palet rol şeması, yazı tipi tekleme, etiketler, ekran envanteri, canlı kontrast, görüntüler, rapor | ajan (opus) | — |
| 2 | Birleşim, main CI, `uc.js --bitti`, sürüm | T0 | 1 |

## Bitiş

`esle.js --denetle` 0 fark, `scan.js` 0 hata, canlı kontrast 0 hata (36 palet), envanterde
düzene geçmemiş ekran yok, başsız test ve yan yana görüntü ayrı kanıt, main CI yeşil.
