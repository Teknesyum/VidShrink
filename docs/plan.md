# Plan: Uc Denetimi, Tema Birleşimi, Yeni İkon

2026-09-27. İstek: "uc denetimini çalıştır; 27 temayı silmeden iki tema çeşidini birleştir (32 ya da 36);
her ekran her panel; ikonu daha yumuşak çizimle ve yeni temanın renkleriyle yeniden oluştur."

## Başlangıç Ölçümü

- Projede 26 palet (`Themes/Palette/*`, `PaletteCatalog.Names`), 31 renk anahtarı, varsayılan `Neon`.
- Standardın temaları: `teknesyum-ui/templates/temalar/` altında 9 tema (4 koyu, 5 açık) ve
  `benim.tokens.json` (Teknesyum Neon, standardın kendi teması).
- `scan.js` başlangıcı: 18 açık, 12 hata (`.calisma/t0-uc/scan-0.txt`).
- Raf: 10 kitap hiç uygulanmadı.

## Karar

- **36 palet:** 26 mevcut + 9 standart tema + standardın kendi teması (`Teknesyum`). Hiçbiri silinmez.
- **Tek şema:** her palet standardın token şemasıyla (renk-1..3, metin kesimleri, surface, text,
  disabled, success, warning) yazılır; projenin 31 anahtarı bu kaynaktan türetilir. Palet kendi değerini
  taşımaz, standardın rollerine bağlanır.
- **Yeni varsayılan:** `Teknesyum`. Kayıtlı palet seçimi olan kullanıcıda seçim korunur.
- **İkon:** vektör kaynaktan (SVG) yeniden çizilir, `Teknesyum` renkleriyle, bütün ico boyları üretilir.
- `ui-duzeni`'nin "yalnız koyu tema" kuralı açık paletlere uygulanmaz: sahip 27'yi korumamızı istedi.
  Açık paletler de aynı 7:1 ölçüsünden geçer. Rapora "uygulanmayan kural" olarak yazılır.

## İşler

| # | İş | Sahip | Bağımlılık |
|---|---|---|---|
| 1 | Tema birleşimi: şema, 10 yeni palet, türetme, varsayılan | ajan (opus) | — |
| 2 | İkon yeniden çizimi | ajan (sonnet) | renkler 1'in `Teknesyum`'undan, değerler bu planda sabit |
| 3 | `scan.js` bulguları: kök yazı boyu, hareket, sabit pencere, şablon imzası | ajan (opus) | — |
| 4 | Raf kitapları (10) | ajan (sonnet) | `.gitignore` bu işin |
| 5 | Canlı denetim: ekran envanteri, 36 palet × her ekran × her durum kontrast, %100/125/150 görüntü, körlemesine bakış, düzeltme, tekrar | ajan (opus) | 1 ve 3 birleşmiş olmalı |
| 6 | Rapor `docs/ui-denetim/2026-09-27.md`, sürüm | T0 | 5 |

## Bitiş

Sıfır kontrast hatası (canlı, her durum, 36 palet), sıfır `scan.js` hatası, envanterde denetlenmemiş ekran
yok, "çalışıyor" başsız testle ve "kullanılabilir" görüntüyle ayrı kanıt, main CI yeşil.
