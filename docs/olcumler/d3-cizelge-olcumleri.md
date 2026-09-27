# D3 Çizelge Ölçümleri — 028 Kabul 1, 4 Ve 5

Tarih: 28 Eylül 2026. Makine: geliştirme makinesi (Windows 11, yerel), Release derlemesi,
gerçek libmpv (`tools\libmpv\libmpv-2.dll`). Kaynak:
`tests/VidShrink.Tests/DuzenleyiciOlcumTests.cs`. Koşum:

```
dotnet test tests/VidShrink.Tests -c Release --no-build --filter "FullyQualifiedName~DuzenleyiciOlcum"
```

Üç test de yeşil. Kabul 1 ve 5 `[HedefMakineFact]`: CI'da atlanır, yerelde sessiz makinede koşar.
Kabul 4 `[Fact]`, her yerde koşar.

## Kabul 1 — Çizim Süresi Kaynak Uzunluğuna Bağlı Değil (±%20)

Tek başına `EditorTimeline`, 1136 px. Her adımda görünüm başı rastgele bir klip sınırına kayar,
ardından yerleşim yapılır ve `RenderTargetBitmap.Render` çağrılır. Süre bu üçünün toplamıdır.
20 ısınma ve 150 ölçüm alındı; kollar sırayla serpiştirildi.

| Kol | Medyan | p95 | En çok kurulan klip | Oran |
|---|---|---|---|---|
| 1 dk, 10 klip × 6 sn, 60 sn görünüm | 0,237 ms | 0,432 ms | 10 | 1 |
| 60 dk, 600 klip × 6 sn, 60 sn görünüm | 0,230 ms | 0,445 ms | 12 | 0,967 |
| 60 dk, 10 klip × 6 dk, tamamı görünür | 0,249 ms | 0,450 ms | 10 | 1,049 |

Sonuç: fark ±%5 içinde, şart ±%20. 600 klipli modelde en çok 12 klip kuruldu, 600 değil;
test 10–12 aralığını şart koşar. Fazladan iki klibin nedeni ölçülmedi.

## Kabul 4 — Bırakışta Oynatma Başı Motorla Aynı Karede (≤1 Kare)

Kaynak 20 sn, 640x360, 30 fps, `-g 30`. Model 5 sn ve 12 sn'de kesildi, ortadaki parça silindi.
Beş hedef çizelge süresinin 0,13 / 0,37 / 0,52 / 0,71 / 0,94'üne düştü. Her hedefte:

1. Sürükleme taklidi: `ScrubTo(x - 6, false)`, `ScrubTo(x, false)`, ardından bırakış `ScrubTo(x, true)`.
   Bırakış anındaki `Playhead` hedef alındı.
2. `EdlPreviewDriver.TimelinePosition` 300 ms değişmeyene dek beklendi (en çok 5 sn).

`TimelinePosition` motorun `PositionSeconds` değerinden türer, sürücünün istediği değer değildir.

| Bırakış | Motor | Fark |
|---|---|---|
| 1,700 sn | 1,700 sn | 0 kare |
| 4,800 sn | 4,800 sn | 0 kare |
| 6,767 sn | 6,767 sn | 0 kare |
| 9,233 sn | 9,233 sn | 0 kare |
| 12,233 sn | 12,233 sn | 0 kare |

## Kabul 5 — Düzenlemeden Sonra İlk Kare ≤ 1 sn (10 dk 1080p, 5 Tekrar Medyanı)

Kaynak 10 sn'lik 1920x1080 30 fps libx264 parçasından (`ultrafast`, `-crf 30`, `-g 60`)
yapıldı. Parça concat demuxer ve `-c copy` ile 60 kez birleştirildi; sonuç 10 dakikalık tek dosya.

Süre, işlem çağrısından `PlayerView.DrawnFrames`'in artmasına kadar ölçüldü. Bu sayaç motorun kare
geri çağrısında artar. Her ölçümden önce 0,5 sn beklendi ve her işlemin tam bir önizleme yeniden
yüklemesi yaptığı (`Reloads` +1) doğrulandı. Önizleme duraklatılmış haldeydi.

| İşlem | Medyan | En çok | Tekrarlar (ms) |
|---|---|---|---|
| Böl (ortadan) | 31,2 ms | 61,6 ms | 61,6 · 31,2 · 31,6 · 30,9 · 30,2 |
| Taşı (0 → 1) | 31,3 ms | 32,1 ms | 31,3 · 31,1 · 32,1 · 30,8 · 31,6 |
| Hız (2× / 1,5×) | 31,5 ms | 62,5 ms | 31,5 · 30,9 · 62,5 · 30,4 · 45,9 |
| Sil (ilk klip) | 30,8 ms | 41,2 ms | 41,2 · 32,2 · 30,7 · 30,5 · 30,8 |

Her işlemin en kötü tekrarı bile 1 sn şartının çok altında.

## Açık Kalanlar

- **Kabul 5'te çizelge küçülüyor.** Beş tekrar aynı modelde art arda koştu. Her turdaki silme ve
  hız işlemi çizelgeyi kısalttı: ilk tur 10 dakikalık çizelgede, son tur 18,75 sn'lik çizelgede
  ölçüldü. Kaynak dosya her turda 10 dakikalık 1080p dosyaydı; arama o dosyanın içine yapıldı.
- **Kaynak, gerçek çekim değil.** `testsrc2` ile üretilen yapay bir görüntü. Anahtar kare aralığı
  2 sn. Kamera dosyalarında (uzun GOP, yüksek bit hızı) kod çözme daha uzun sürebilir; bu
  ölçülmedi.
- **Kabul 1 CPU süresini ölçüyor.** `RenderTargetBitmap` yazılım çizimidir; ekrana basış
  (GPU birleştirme, vsync) ölçüye girmiyor.
- **Tek makine, tek koşum.** Sayılar yük altında okunmamalı.
