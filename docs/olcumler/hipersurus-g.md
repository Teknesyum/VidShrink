# Hipersürüş G Dalgası Ölçümleri

Makine DESKTOP-0J80KVV, 17 Eylül 2026. Araç `tools/acilis-hizi/EkranSaati`, ayrı Win32
masaüstünde, her süreçte `KayitKalkani`, ayar `.calisma` altında (`autoUpdate: false`).
Her ölçüm aynı oturumda eşleşik: her tekrarda iki taraf da koşar, sıra tekrardan tekrara
döner. Sıcak kip, 10 tekrar, yük testi yok. Klip `klip1080.mp4` (1080p, 6,8 MB).

Saat ms, uygulamanın kendi izi. Sıfır noktası çift tıkın doğurduğu sürecin
`Process.StartTime`'ı: önce başlatıcı (`VidShrink.exe`), sonra doğrudan
`app\VidShrink.App.exe`. Kabuğun `CreateProcess` öncesi payı iki tarafta da girmez.

- **önce** — `da09b690` (0.8.4 + G5 ölçüm işaretleri, davranış değişikliği yok), giriş başlatıcı.
- **sonra** — `98b77bea` (G2-G4), giriş `app\VidShrink.App.exe`, ilişki kaydı da app'i gösteriyor
  (yeniden yazma denemesi yok). Arkadaki `--bakim` başlatıcısı ölçüme dahil: ilk karenin
  süresine CPU payıyla girer.

## G6: Dosyayla Açılış (Çift Tık, `--bitis ilk-kare`)

| adım | önce ortanca | sonra ortanca | eşleşik fark ortancası | en az / en çok | sonra lehine |
|---|---:|---:|---:|---:|---:|
| main | 184,4 | 90,3 | -92,5 | -119,8 / -65,6 | 10/10 |
| pencere (cerceve) | 450,6 | 371,8 | -85,8 | -110,7 / -38,5 | 10/10 |
| pencere-yuklendi | 816,5 | 746,2 | -74,8 | -122,1 / -11,8 | 10/10 |
| ilk-kare | 897,7 | 832,1 | -71,6 | -102,9 / -5,1 | 10/10 |
| ilk-boya | 1110,0 | 1049,7 | -61,3 | -123,2 / +47,1 | 9/10 |

Tekrar başına ham (ms, ilk-boya / ilk-kare):

| # | önce | sonra |
|---:|---|---|
| 1 | 1154 / 953 | 1122 / 871 |
| 2 | 1033 / 860 | 1080 / 855 |
| 3 | 1102 / 894 | 1055 / 826 |
| 4 | 1086 / 873 | 1045 / 839 |
| 5 | 1163 / 927 | 1054 / 834 |
| 6 | 1133 / 933 | 1058 / 830 |
| 7 | 1118 / 902 | 1009 / 844 |
| 8 | 1085 / 885 | 1021 / 812 |
| 9 | 1149 / 909 | 1026 / 822 |
| 10 | 1083 / 890 | 1024 / 821 |

Önce tarafında başlatıcının payı `app-dogdu` ortancası 104,1 ms. Kazanç bundan küçük
kalıyor çünkü sonra tarafında app ~117 ms'de `--bakim` başlatıcısını doğuruyor
(`bakim-basladi` ortancası 116,8) ve o süreç açılışla aynı anda çekirdek kullanıyor.

## G6: Boş Açılış (`--klip -`, `--bitis ilk-boya`)

| adım | önce ortanca | sonra ortanca | eşleşik fark ortancası | en az / en çok | sonra lehine |
|---|---:|---:|---:|---:|---:|
| main | 178,2 | 87,1 | -97,6 | -115,5 / -74,6 | 10/10 |
| pencere (cerceve) | 447,8 | 356,8 | -88,9 | -118,2 / -56,9 | 10/10 |
| pencere-yuklendi | 868,8 | 782,0 | -86,1 | -154,2 / -19,5 | 10/10 |
| ilk-boya | 945,6 | 855,8 | -86,9 | -374,5 / -18,8 | 10/10 |

Ham ilk-boya (ms), önce: 942, 924, 951, 931, 949, 933, 1227, 930, 969, 952.
Sonra: 884, 818, 868, 839, 859, 850, 853, 911, 811, 875.

## G1: Yazılım Çizimi A/B (Uygulanmadı)

Aynı yayın (`da09b690`), A varsayılan GPU, B `VIDSHRINK_CIZIM=yazilim`. İlk kareden sonra
7 sn süreç CPU'su, çizim turu ve kare sayısı (`VIDSHRINK_CIZIM_OLCUMU`), 10 tekrar.
Fark yazılım eksi GPU; "yazılım lehine" negatif farkın sayısı.

**Oynatıcı sekmesi** (1080p oynarken):

| ölçü | GPU ortanca | yazılım ortanca | fark ortancası | yazılım lehine |
|---|---:|---:|---:|---:|
| cizim-cpu-ms-sn | 361,2 | 582,2 | +209,5 | 0/10 |
| cizim-cpu-yuzde | 2,3 | 3,6 | +1,3 | 0/10 |
| cizim-kare-sn | 27,7 | 28,4 | +1,0 | 4/10 |
| cizim-tur-sn | 26,3 | 40,4 | +14,2 | 0/10 |
| cerceve | 366,7 | 235,7 | -132,8 | 10/10 |
| ilk-kare | 775,3 | 589,7 | -168,0 | 7/10 |
| ilk-boya | 929,7 | 673,1 | -237,1 | 10/10 |

**Karşılaştırma paneli** (küçültme yüklendikten sonra):

| ölçü | GPU ortanca | yazılım ortanca | fark ortancası | yazılım lehine |
|---|---:|---:|---:|---:|
| cizim-cpu-ms-sn | 154,0 | 314,7 | +175,2 | 2/10 |
| cizim-cpu-yuzde | 1,0 | 2,0 | +1,1 | 2/10 |
| cizim-kare-sn | 12,0 | 15,8 | +3,9 | 0/10 |
| cizim-tur-sn | 28,2 | 50,7 | +22,8 | 0/10 |
| cerceve | 390,1 | 268,8 | -119,7 | 10/10 |
| ilk-kare | 830,5 | 662,2 | -231,6 | 8/10 |
| ilk-boya | 994,1 | 734,5 | -274,5 | 8/10 |

Panel GPU tarafında iki uç tekrar var (ilk-kare 7900 ve 6641 ms); ortanca etkilenmiyor.

**Karar:** yazılım çizimi açılışı 130-275 ms öne alıyor ama iki kipte de çizim CPU'su
yükseliyor: oynatıcıda saniyede +209,5 ms (yaklaşık %60, 10 tekrarın hiçbirinde lehine değil),
panelde +175,2 ms (iki katı). Kural "kötüyse uygulama" olduğu için varsayılan GPU kaldı;
`VIDSHRINK_CIZIM=yazilim` elle seçim olarak duruyor.

## Yan Bulgu

Karşılaştırma panelinde `kucultme-yuklendi` 1080p klipte 9-11 sn sürüyor
(`cizim-basladi` ortancası 11 080 ms). Bu dalganın kapsamı dışında.
