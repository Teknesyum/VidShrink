# Kullanılabilir Kanıtı

Soru: ekranlar okunuyor, hiçbir şey kesilmiyor ve yeni düzen göze düzgün görünüyor mu.

| Ölçü | Sonuç | Kaynak |
|---|---|---|
| Canlı kontrast, 36 palet × 12 ekran × 7 durum, metin 7:1, simge 3:1 | yeni hata 0; 8 ertelenmiş taban 0,01-0,06 düştü, renk aynı | `kontrast-sonra.log`, rapor "Canlı Kontrast" |
| Şablon kontrastı (`scaffold.js denetim Teknesyum`) | 2 KALIR, ikisi de yanlış pozitif (dolgu ölçülüyor, görünen kontur) | `sablon-denetim.log` |
| Kırpma (şablon `KabukTests`) | 38 bulgu; hepsi `:pointerover`/`:pressed` parıltı katmanı, yazı ve çerçeve kırpılmıyor | `sablon-denetim.log` |
| Yazı boyu fs-2 (16) altı | 43 yazı 14 px | `sablon-denetim.log` |
| Ekran görüntüleri | 15 ekran × %100/%125/%150 × önce/sonra; 9 yan yana (önce, sonra, önizleme) | bu klasördeki `*.png` |
| Bağımsız göz | taşma ya da kesik yazı görmedi; köşe ve başlık dili önizlemeyle örtüşüyor; yoğunluk ve etkin sekme biçimi farklı | `bagimsiz-goz.md` |

Açık kalanlar sahibin kararı: fs-2 tip ölçeği, parıltının kırpılması, etkin sekme biçimi, sekme şeridindeki `FontMono`.
