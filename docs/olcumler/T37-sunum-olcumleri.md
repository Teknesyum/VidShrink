# T37 — Ö4: Avalonia sunum yolu ölçümü

Soru: `WriteableBitmap` + `Image` yolu iki 1080p karesini yan yana taşıyan kareyi
saniyede kaç kez ekrana koyabiliyor? T33 turu duvarın boruda olmadığını gösterdi;
geriye sunum tarafı kalmıştı.

Makine: RTX 5070 Ti · ekran 2560x1440 **180 Hz** · Avalonia 11.3.20 · net8.0 Release.
Ölçüm aracı: `tools/VidShrink.PresentBench`. Sayaç `TopLevel.RequestAnimationFrame` —
yani gerçekten bestelenen kare, döngü turu değil. İlk 1,5 saniye ısınma sayılmıyor.

## Ö4a — sunum tavanı, sentetik kare

Kod çözme yok. Sekiz hazır tampon dönüşümlü olarak `WriteableBitmap`'e kopyalanıyor.
Kaynak tamponlar sıcak, yani bu **sunum yolunun tek başına tavanı**.

| Kare | Kare boyu | Sunulan fps (3 koşu) | Kopya süresi |
|---|---|---|---|
| 1920x1080 | 7,91 MiB | 170,0 · 170,2 (·122,5 ısınma) | 0,42 ms |
| **3840x1080** (2x1080p) | **15,82 MiB** | **165,8 · 166,0 · 166,4** | **0,77 ms** |
| 3840x2160 | 31,64 MiB | 90,0 · 90,2 · 89,8 | 1,47 ms |

180 Hz ekranda 166 fps, dikey eşitlemeye yapışık demektir — 2x1080p'de sunum yolu
zorlanmıyor. 3840x2160 tam yarım hızda (90 = 180/2) kilitleniyor: kare başına maliyet
5,5 ms'i geçip bir eşitleme aralığını kaçırıyor, ama hâlâ 60'ın 1,5 katı.

## Ö4b — uçtan uca, gerçek boru

Tek ffmpeg süreci, iki girdi, `fps=60,scale,hstack`, `-f rawvideo -pix_fmt bgra` boruya.
Okuma 64 KB parçalarla kare tamponuna toplanıyor (T33'ün bulduğu doğru şekil).
Kaynak: `gothic2026-08-15 14-01-29.mp4` iki kez.

**Serbest hız — tavan:**

| Koşu | Sunulan fps | Boru besleme fps | Yeni kare/s | Kopya süresi |
|---|---|---|---|---|
| 1 | 92,2 | 129,5 | 88,9 | 1,478 ms |
| 2 | 88,7 | 128,6 | 86,0 | 1,464 ms |
| 3 | 92,2 | 127,5 | 88,4 | 1,470 ms |

**Uçtan uca tavan 2x1080p'de ~90 fps.** Hedef 60 fps'in **1,5 katı pay** var.

Sentetikte 166 olan sayının burada 90'a düşmesinin sebebi kopya süresinin 0,77'den
1,47 ms'e çıkması: boru her kareyi **yeni bir tampona** yazıyor, kaynak tampon soğuk
geliyor. Tampon havuzu bunu geri kazanır; ölçüm havuzsuz hâli, yani kötümser taraf.

**Gerçek zamanlı 60 fps — hedef koşul (`-re`):**

| Koşu | Boru besleme | Ekrana konan yeni kare/s | Boşa dönen RAF turu | Sunulan fps |
|---|---|---|---|---|
| 1 | 60,1 | 57,6 | 552 / 898 | 149,5 |
| 2 | 60,1 | 57,7 | 546 / 893 | 148,7 |

Sunum tarafı turlarının **%61'inde elinde yeni kare bulamıyor** — beklediği şey boru,
kendisi değil. Aradığımız cevap bu.

## Ölçümün yakaladığı kusur — tek gözlü tampon

Beslenen 361 kareden 346'sı ekrana kondu, **15'i (%4) düştü.** Sebep ölçüm aracının
tek gözlü "en yenisi kazanır" tamponu: okuyucu yeni kareyi yazarken önceki hiç
alınmamışsa üzerine biniyor. Üretimde bu 2-3 gözlü halka tamponla kapanır; ölçüm
sonucunu etkilemiyor, çünkü düşen kare sunum yavaş olduğu için değil, tampon dar
olduğu için düşüyor.

## Sonuç

- Sunum yolu 2x1080p60'ta duvar **değil**. Tavan ~90 fps, hedef 60.
- 3840x2160'a çıkılsa bile 90 fps sunuluyor; kısıt kod çözmede olurdu, sunumda değil.
- **libmpv gerekmiyor.** T33 boruyu, T37 sunumu temizledi; iki ucu da ölçülen yol
  hedefi taşıyor.
- Planın §9'undaki platform ayrışması (Linux'a sessiz/1080p sınırı) bu ölçümlerle
  dayanaksız kaldı. Tek yol, üç platform.
- Üretimde uygulanacak iki şey: kare tamponu **havuzdan** gelecek (1,47 -> 0,77 ms),
  boru ile sunum arasında **çok gözlü halka** olacak (%4 düşme kapanır).
