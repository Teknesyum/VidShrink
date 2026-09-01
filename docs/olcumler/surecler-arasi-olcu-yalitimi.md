# Süreçler arası ölçü yalıtımı

Durum: **yarım — kullanıcı isteğiyle 2026-09-01 tarihinde durduruldu.**

## Tamamlanan değişiklikler

- `PerformanceCheckTests.OlcumArtikBirakmiyor`: geçici kök `.calisma/test-ciktilari/performance-temp/<PID>/<GUID>` altına alındı; `TEMP` ve `TMP` yalnız ölçüm boyunca bu kökü gösteriyor ve sonunda eski değerlerine dönüyor.
- `ShellMenuTests`: kayıt defteri kökü PID ve GUID ile koşuma özel hale getirildi.
- `Windows11ShellMenuTests`: manifestte sabit olan `Teknesyum.VidShrink.Shell` paket kimliği yalıtılamadığı için `Local\VidShrink-Sparse-Package-Test` adlı süreçler arası kilitle sıraya alındı.
- `UpdaterTests.TheDeletionStepWaitsOutATransientLock`: eski başlangıç işaretinin kilidi erken bıraktırmaması için işaret her koşumdan önce siliniyor. Updater test kökü de `.calisma/test-ciktilari/updater/<PID>/<GUID>` altına taşındı.
- Aynı dosyadaki stres ölçüsünün temizlik adımı, Windows'un kısa süreli dosya tutmasına karşı iddiayı değiştirmeyen sınırlı yeniden denemeyle sağlamlaştırıldı.

## Şu ana kadarki doğrulama

- `TheDeletionStepWaitsOutATransientLock`, art arda üç koşum: `1 başarılı / 0 başarısız` × 3.
- Birleşik sözleşme filtresi: `85 başarılı / 0 başarısız`, 145,5 saniye.
- Bundan önceki ilk birleşik koşumda sözleşme dışındaki `TheNameIsNeverAbsentWhileTheSwapItselfRuns` temizlik adımı bir `IOException` ile düştü; sınırlı temizlik yeniden denemesi eklendikten sonra birleşik filtre yeşil oldu.

## Kalan işler

- Dört ölçünün üretim davranışını ayrı ayrı bozup kırmızıya döndüğünü gösterecek mutasyon denetimleri yapılmadı.
- İki tam süiti aynı anda, arka arkaya üç kez çalıştıran altı koşum yapılmadı; altı sonuç satırı henüz yok.
- Tam süit ve `gh run list --branch t86-olcu-yalitimi` mühür denetimi yapılmadı.
- Dal son teslim olarak itilmedi; bu ara kayıt tamamlanmış teslim değildir.
