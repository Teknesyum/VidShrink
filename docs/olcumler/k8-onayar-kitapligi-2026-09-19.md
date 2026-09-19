# Ön Ayar Kütüphanesi Kullanıcıya Açıldı (19 Eylül 2026)

## Bulgu

Gömülü kütüphanede (`src/VidShrink.Core/Presets/platformlar.json`) on sekiz profil var:
5 General, 9 Platform, 4 Device. Yalnız sekizinin `chip` alanı olduğu için arayüz şeridi
sekizini çiziyordu; kalan **on profil** depoda duruyor ve hiçbir kullanıcı hiçbir yoldan
seçemiyordu:

`discord-free`, `discord-nitro-basic`, `discord-nitro`, `telegram`, `email-gmail`,
`email-outlook`, `device-chromecast-gen1-2`, `device-chromecast-gen3`, `device-nest-hub`,
`device-apple-tv-hd`.

`PresetKind`'ın dört üyesi de bu yüzden ölü üye pimindeydi; `PresetLibrary.Find` üretimde
hiç çağrılmıyordu.

## Karar

Kütüphane **CLI'dan** açıldı; arayüz şeridi sekiz yongada kaldı. On sekiz yonga şeridi
boğardı, liste ise CLI'ın doğal biçimi.

- `profiller` / `presets` komutu kütüphaneyi türüne göre bölümlüyor: Genel, Platform,
  Cihaz, sonra kullanıcının kendi profilleri. Boş bölüm hiç yazılmıyor.
- `--profil <kimlik>` / `--profile` profili plan seçeneğine taban yapıyor: hedef boyut,
  niyet, doldurma siyaseti, kısa kenar sınırı ve ses bit hızı.
- **Öncelik tek yönlü:** elle verilen bayrak kazanır. `--profil telegram --hedef 100MB`
  hedefi 100 MB yapar, profilin öbür alanları durur.
- Profil hedef taşımıyorsa (dört cihaz profili) `--hedef` ya da `--kalite` isteniyor;
  ayrı hata kolu (`error.profile-no-target`).

## Kapsam dışı kalan tek alan

Profilin `container` alanı plana inmiyor: `PlanOptions`'ta kap alanı yok, kap E2'den beri
**çıktı uzantısından** türüyor. Cihaz profillerinin `"container": "Mp4"` satırı bu yüzden
şimdilik okunmuyor; kullanıcı `--cikti ad.mp4` ile aynı sonuca varıyor.

## Ölçüm sırasında çıkan şey

Ölü üye tarayıcısı üç `PresetKind` üyesini pimden düşürmedi, **biçim değiştirdi**:
`hic-gorunmeyen`/`yalniz-disarida` iken `varsayilan-kol` oldular. Sebep: listeleme dört
türü bir tablodan geziyor ve adıyla karşılaştıran tek kol `User` (kullanıcı profilleri
dosyadan gelir, kütüphaneden değil). Üç üye döngü değişkeniyle tüketiliyor.

Pim bu yüzden silinmedi, **borçtan meşruya** çevrildi — `AudioSourceRole.SystemAudio` ve
`PresetSourceStatus.Code` ile aynı biçim. Ayrıca adlandırmak aynı tabloyu iki kez yazardı.

`CropDetection.Rect` pimi de bu turda düştü: `--kirp` işi onu üretimde okuttu.

M5 kesiminin ilk yazımı (`if (bulunan is null && false)`) derlenmedi — derleyici
`bulunan`'ı null kabul edip CS8602 verdi. Kesim "bilinmeyen kimlik sessizce ilk profile
düşüyor" biçimine çevrildi; **derlenmeyen kesim kırmızı değil ölçümsüzdür.**

## Mutasyon turu

| # | Kesim | Kırmızı |
| --- | --- | --- |
| Taban | — | 0/10 |
| M1 | Cihaz bölümü listeden düşüyor | 3/10 |
| M2 | Boş bölüm de başlığını yazıyor | 1/10 |
| M3 | Profilin kısa kenar sınırı plana inmiyor | 1/10 |
| M4 | Elle verilen hedef profilinkini ezmiyor | 1/10 |
| M5 | Tanınmayan kimlik sessizce ilk profile düşüyor | 2/10 |
| M6 | tr kısa kenar kalıbında yer tutucu yok | 1/10 |
| Geri | — | 0/10 |

Altı kesimin altısı kırmızı; taban ve geri 0/10.

Sürücü `.calisma/kitaplik/mutasyon.py`, ham çıktı `.calisma/kitaplik/sonuc.txt`
(M5 satırı ayrı koşumla yenilendi).
