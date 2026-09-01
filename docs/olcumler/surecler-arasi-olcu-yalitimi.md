# Süreçler arası ölçü yalıtımı

Durum: **tamamlandı — 2026-09-01.**

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

## Mutasyon denetimleri

Dört ölçünün denetlediği üretim davranışı ayrı ayrı bozuldu; her kırmızı koşumdan sonra değişiklik geri alındı.

| Ölçü | Geçici mutasyon | Kırmızı kanıtı |
|---|---|---|
| `PerformanceCheckTests.OlcumArtikBirakmiyor` | `PerformanceProbe.Cleanup` dizini silmedi | `Expected: 0, Actual: 1` |
| `ShellMenuTests.Every_command_calls_the_installed_launcher_with_the_path` | Kabuk komutundan `"%1"` kaldırıldı | Beklenen komut ile kayıt defteri değeri ayrıldı |
| `Windows11ShellMenuTests.Sparse_package_really_registers_and_removes_on_Windows_11` | AppX kimliği geçici olarak başka ada alındı | Kayıt sorgusu `0`, beklenen `1` |
| `UpdaterTests.TheDeletionStepWaitsOutATransientLock` | Silme deneme sayısı `6`dan `1`e indirildi | Çıkış `3`, beklenen `0`; kilitli klasör silinemedi |

Mutasyon çıktıları geçici olarak `.calisma/t86-mutasyon/` altında tutuldu ve mühürden önce temizlendi.

## Eşzamanlı tam süit

Tek bir temiz Release derlemesinden sonra iki `dotnet test -c Release --no-build --no-restore` süreci aynı anda başlatıldı; çift tamamlanmadan sonraki çifte geçilmedi. Bu işlem arka arkaya üç kez tekrarlandı.

| Çift | Süreç | Sonuç satırı |
|---|---|---|
| 1 | A | `Başarısız: 0, Başarılı: 960, Atlanan: 23, Toplam: 983, Süre: 669,1 sn` |
| 1 | B | `Başarısız: 0, Başarılı: 960, Atlanan: 23, Toplam: 983, Süre: 716,6 sn` |
| 2 | A | `Başarısız: 0, Başarılı: 960, Atlanan: 23, Toplam: 983, Süre: 601,8 sn` |
| 2 | B | `Başarısız: 0, Başarılı: 960, Atlanan: 23, Toplam: 983, Süre: 558,8 sn` |
| 3 | A | `Başarısız: 0, Başarılı: 960, Atlanan: 23, Toplam: 983, Süre: 637,7 sn` |
| 3 | B | `Başarısız: 0, Başarılı: 960, Atlanan: 23, Toplam: 983, Süre: 603,0 sn` |

Altı sürecin altısı sıfır çıkış koduyla tamamlandı; hiçbirinde yarım koşum veya başarısız ölçü yoktu.

## Son mühür

Kesintisiz tam Release süiti:

`Başarısız: 0, Başarılı: 960, Atlanan: 23, Toplam: 983, Süre: 7 m 52 s`

Mühürden hemen önce `gh run list --branch t86-olcu-yalitimi` çalıştırıldı; dal için CI koşumu yoktu (`[]`). Yalnız bu işin oluşturduğu `.calisma/t86-mutasyon`, `.calisma/t86-eszamanli` ve `.calisma/t86-final` çıktıları temizlendi.
