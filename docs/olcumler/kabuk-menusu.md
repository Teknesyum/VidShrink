# Kabuk Menüsü: "VidShrink ile Küçült" Girdisi ve Hızlı Hedef Alt Menüsü

T169, tur 4. Ölçüm makinesi: Windows 11 Pro 10.0.26100, .NET 8, Release.

Kayıt defteri ağacını yalnız `Install-VidShrink.ps1` yazar. Ölçüm gerçek
`HKCU\Software\Classes` ağacına dokunmaz; betiğe `-RegistryRoot` ile geçici bir kök
verilir, okunur ve silinir.

## K1 — İkinci Girdi Yazılıyor, Birincisi Bozulmuyor

`.mp4` için üretilen tam anahtar dökümü (`reg.exe export` ham çıktısı; test kökü ve
kurulum yolu kısaltıldı):

```
Windows Registry Editor Version 5.00
[HKEY_CURRENT_USER\<TEST-KOKU>\SystemFileAssociations\.mp4]
[HKEY_CURRENT_USER\<TEST-KOKU>\SystemFileAssociations\.mp4\shell]
[HKEY_CURRENT_USER\<TEST-KOKU>\SystemFileAssociations\.mp4\shell\VidShrink]
"MUIVerb"="Bu Videoyu VidShrink ile Aç"
"Icon"="<KURULUM>\\VidShrink.exe"
[HKEY_CURRENT_USER\<TEST-KOKU>\SystemFileAssociations\.mp4\shell\VidShrink\command]
@="\"<KURULUM>\\VidShrink.exe\" \"%1\""
[HKEY_CURRENT_USER\<TEST-KOKU>\SystemFileAssociations\.mp4\shell\VidShrinkKucult]
"MUIVerb"="VidShrink ile Küçült"
"Icon"="<KURULUM>\\VidShrink.exe"
"SubCommands"=""
"MultiSelectModel"="Player"
[HKEY_CURRENT_USER\<TEST-KOKU>\SystemFileAssociations\.mp4\shell\VidShrinkKucult\shell]
[HKEY_CURRENT_USER\<TEST-KOKU>\SystemFileAssociations\.mp4\shell\VidShrinkKucult\shell\100]
"MUIVerb"="100 MB"
"MultiSelectModel"="Player"
[HKEY_CURRENT_USER\<TEST-KOKU>\SystemFileAssociations\.mp4\shell\VidShrinkKucult\shell\100\command]
@="\"<KURULUM>\\VidShrink.exe\" --kucult 100 \"%1\""
[HKEY_CURRENT_USER\<TEST-KOKU>\SystemFileAssociations\.mp4\shell\VidShrinkKucult\shell\1024]
"MUIVerb"="1 GB"
"MultiSelectModel"="Player"
[HKEY_CURRENT_USER\<TEST-KOKU>\SystemFileAssociations\.mp4\shell\VidShrinkKucult\shell\1024\command]
@="\"<KURULUM>\\VidShrink.exe\" --kucult 1024 \"%1\""
[HKEY_CURRENT_USER\<TEST-KOKU>\SystemFileAssociations\.mp4\shell\VidShrinkKucult\shell\2048]
"MUIVerb"="2 GB"
"MultiSelectModel"="Player"
[HKEY_CURRENT_USER\<TEST-KOKU>\SystemFileAssociations\.mp4\shell\VidShrinkKucult\shell\2048\command]
@="\"<KURULUM>\\VidShrink.exe\" --kucult 2048 \"%1\""
[HKEY_CURRENT_USER\<TEST-KOKU>\SystemFileAssociations\.mp4\shell\VidShrinkKucult\shell\250]
"MUIVerb"="250 MB"
"MultiSelectModel"="Player"
[HKEY_CURRENT_USER\<TEST-KOKU>\SystemFileAssociations\.mp4\shell\VidShrinkKucult\shell\250\command]
@="\"<KURULUM>\\VidShrink.exe\" --kucult 250 \"%1\""
[HKEY_CURRENT_USER\<TEST-KOKU>\SystemFileAssociations\.mp4\shell\VidShrinkKucult\shell\500]
"MUIVerb"="500 MB"
"MultiSelectModel"="Player"
[HKEY_CURRENT_USER\<TEST-KOKU>\SystemFileAssociations\.mp4\shell\VidShrinkKucult\shell\500\command]
@="\"<KURULUM>\\VidShrink.exe\" --kucult 500 \"%1\""
```

`.mkv` dökümü, aynı koşumun ikinci `reg.exe export` çıktısı. Blok kısaltılmadı; iki döküm
satır satır karşılaştırıldığında tek fark uzantı adıdır:

```
Windows Registry Editor Version 5.00
[HKEY_CURRENT_USER\<TEST-KOKU>\SystemFileAssociations\.mkv]
[HKEY_CURRENT_USER\<TEST-KOKU>\SystemFileAssociations\.mkv\shell]
[HKEY_CURRENT_USER\<TEST-KOKU>\SystemFileAssociations\.mkv\shell\VidShrink]
"MUIVerb"="Bu Videoyu VidShrink ile Aç"
"Icon"="<KURULUM>\\VidShrink.exe"
[HKEY_CURRENT_USER\<TEST-KOKU>\SystemFileAssociations\.mkv\shell\VidShrink\command]
@="\"<KURULUM>\\VidShrink.exe\" \"%1\""
[HKEY_CURRENT_USER\<TEST-KOKU>\SystemFileAssociations\.mkv\shell\VidShrinkKucult]
"MUIVerb"="VidShrink ile Küçült"
"Icon"="<KURULUM>\\VidShrink.exe"
"SubCommands"=""
"MultiSelectModel"="Player"
[HKEY_CURRENT_USER\<TEST-KOKU>\SystemFileAssociations\.mkv\shell\VidShrinkKucult\shell]
[HKEY_CURRENT_USER\<TEST-KOKU>\SystemFileAssociations\.mkv\shell\VidShrinkKucult\shell\100]
"MUIVerb"="100 MB"
"MultiSelectModel"="Player"
[HKEY_CURRENT_USER\<TEST-KOKU>\SystemFileAssociations\.mkv\shell\VidShrinkKucult\shell\100\command]
@="\"<KURULUM>\\VidShrink.exe\" --kucult 100 \"%1\""
[HKEY_CURRENT_USER\<TEST-KOKU>\SystemFileAssociations\.mkv\shell\VidShrinkKucult\shell\1024]
"MUIVerb"="1 GB"
"MultiSelectModel"="Player"
[HKEY_CURRENT_USER\<TEST-KOKU>\SystemFileAssociations\.mkv\shell\VidShrinkKucult\shell\1024\command]
@="\"<KURULUM>\\VidShrink.exe\" --kucult 1024 \"%1\""
[HKEY_CURRENT_USER\<TEST-KOKU>\SystemFileAssociations\.mkv\shell\VidShrinkKucult\shell\2048]
"MUIVerb"="2 GB"
"MultiSelectModel"="Player"
[HKEY_CURRENT_USER\<TEST-KOKU>\SystemFileAssociations\.mkv\shell\VidShrinkKucult\shell\2048\command]
@="\"<KURULUM>\\VidShrink.exe\" --kucult 2048 \"%1\""
[HKEY_CURRENT_USER\<TEST-KOKU>\SystemFileAssociations\.mkv\shell\VidShrinkKucult\shell\250]
"MUIVerb"="250 MB"
"MultiSelectModel"="Player"
[HKEY_CURRENT_USER\<TEST-KOKU>\SystemFileAssociations\.mkv\shell\VidShrinkKucult\shell\250\command]
@="\"<KURULUM>\\VidShrink.exe\" --kucult 250 \"%1\""
[HKEY_CURRENT_USER\<TEST-KOKU>\SystemFileAssociations\.mkv\shell\VidShrinkKucult\shell\500]
"MUIVerb"="500 MB"
"MultiSelectModel"="Player"
[HKEY_CURRENT_USER\<TEST-KOKU>\SystemFileAssociations\.mkv\shell\VidShrinkKucult\shell\500\command]
@="\"<KURULUM>\\VidShrink.exe\" --kucult 500 \"%1\""
```

İki dökümü üreten koşumda test kökü `HKCU:\Software\VidShrinkDokum-5356`, kurulum yolu
`%TEMP%\vidshrink-dokum-5356`; ikisi de koşum sonunda silindi, `Test-Path` → `False`.

"Aç" girdisinin bozulmadığını gösteren ölçü, üç alanı birden karşılaştırır:

| Ölçü | Neyi karşılaştırır |
| --- | --- |
| `Open_entry_survives_the_second_entry_untouched` | `MUIVerb`, `Icon`, `command` — her uzantı için |
| `Shrink_entry_is_written_for_every_media_extension` | Küçült girdisi olan uzantı kümesi = `ShellIntegration.MediaExtensions` |
| `Two_extension_dump_shows_the_parent_verb_and_its_subcommands` | `.mp4` ve `.mkv` için üst fiil etiketi ve boş `SubCommands` |

## K2 — Alt Menü ve Hedef Listesi

Alt menü klasik `SubCommands` deseniyle geliyor: üst fiilde boş `SubCommands` değeri,
altında `shell` anahtarı, onun altında her hedef için bir alt fiil.

| Hedef (MB) | Alt fiil anahtarı | Etiket | Üretilen komut satırı |
| --- | --- | --- | --- |
| 100 | `...\VidShrinkKucult\shell\100` | `100 MB` | `"<KURULUM>\VidShrink.exe" --kucult 100 "%1"` |
| 250 | `...\VidShrinkKucult\shell\250` | `250 MB` | `"<KURULUM>\VidShrink.exe" --kucult 250 "%1"` |
| 500 | `...\VidShrinkKucult\shell\500` | `500 MB` | `"<KURULUM>\VidShrink.exe" --kucult 500 "%1"` |
| 1024 | `...\VidShrinkKucult\shell\1024` | `1 GB` | `"<KURULUM>\VidShrink.exe" --kucult 1024 "%1"` |
| 2048 | `...\VidShrinkKucult\shell\2048` | `2 GB` | `"<KURULUM>\VidShrink.exe" --kucult 2048 "%1"` |

Liste tek kopya `ShellIntegration.QuickShrinkTargetsMegabytes` içinde durur. Eşitliği
üç ölçü birden korur:

| Ölçü | Neyi karşılaştırır |
| --- | --- |
| `Installer_target_list_is_the_application_target_list` | Betikteki `$shellShrinkTargets` dizisi = uygulamanın listesi; dizinin betikte **tam bir** kez geçtiği de aranır |
| `Written_targets_are_the_application_target_list` | Kayıt defterine yazılan hedef kümesi = uygulamanın listesi |
| `Target_labels_come_from_the_shared_formatter` | Yazılan `MUIVerb` = `ShellIntegration.FormatQuickShrinkLabel` |

Etiket biçimlendirmesi de tek yerdedir: 1024'ün tam katları GB, kalanı MB.

## K3 — Geri Alma Simetrik

Aynı geçici kökte yazma öncesi / sonrası / geri alma sonrası sayımlar:

| Aşama | Özyinelemeli anahtar | Uzantı anahtarı |
| --- | --- | --- |
| Yazma öncesi | 0 | 0 |
| Yazma sonrası | 384 | 24 |
| Geri alma sonrası | 0 | 0 |

Ham çıktı:

```
TEST KOKU: HKCU:\Software\VidShrinkDokum-26500
YAZMA ONCESI ozyinelemeli=0 uzanti=0
YAZMA SONRASI ozyinelemeli=384 uzanti=24
GERI ALMA SONRASI ozyinelemeli=0 uzanti=0
```

24 uzantının her biri 16 anahtar taşır: uzantının kendisi, `shell`, `VidShrink`,
onun `command`'ı, `VidShrinkKucult`, onun `shell`'i, 5 hedef ve 5 hedef komutu.

`Remove-ShellMenu` iki üst girdiyi de siler ve boşalan `shell` ile uzantı anahtarlarını
temizler; `SystemFileAssociations` kökü — gerçek ağaçta Windows'un kendi anahtarı —
bugünkü davranışta olduğu gibi silinmez, boş bırakılır.

## K6 — Gerçek Köke Dokunulmadı

Ölçümün kullandığı kök çalışma başına yeni üretilir:

```
HKCU:\Software\VidShrinkKucult-Test-<PID>-<GUID>
HKCU:\Software\VidShrinkDokum-<PID>          (yukarıdaki reg export dökümü için)
```

| Kanıt | Sonuç |
| --- | --- |
| `Registry_writers_never_hard_code_the_real_shell_root` | `Write-ShellMenu`, `Write-ShellShrinkMenu`, `Remove-ShellMenu` gövdelerinde `HKCU:` geçmiyor; üçü de yolu `$Root`'tan kuruyor |
| `Installer_still_defaults_to_the_real_shell_root` | Betiğin varsayılan `-RegistryRoot` değeri değişmedi |
| Gerçek `HKCU\Software\Classes\SystemFileAssociations` | Bu turun bütün koşumları bittikten sonra `VidShrink*` adlı anahtar sayısı: 0 |
| `HKCU:\Software` | Bu turun sonunda `VidShrink*` desenli kök sayısı: 0 |

Gerçek ağaç yalnız okundu, hiç yazılmadı.

### Düşen Bir Koşumun Bıraktığı Kök

Tur 3'ün *"geçici kökler silindi"* cümlesi ölçülmüş değildi ve yanlıştı: `Dispose()` en
iyi çabadır, süreç yazma ile silme arasında düşerse kök kalır. Denetim anında makinede
297 anahtarlık bir artık kök duruyordu.

Silmek bunu çözmez, çünkü bir sonraki düşüş aynı izi bırakır. Koşum **başında** aynı
desene (`VidShrinkKucult-Test-<pid>-<guid>`) uyan ve **ölü PID** taşıyan kökler
toplanıyor; canlı PID taşıyan kök — makinede eşzamanlı koşan başka bir ölçüm — elde
tutuluyor.

Ölçü yapay bir artık kök kurup topluyor:

| Ölçü | Ne kuruyor | Ne bekliyor |
| --- | --- | --- |
| `Abandoned_root_from_a_dead_process_is_reaped_at_run_start` | Ölü PID'li kök, altında `SystemFileAssociations\.mp4\shell\VidShrinkKucult` | Toplananlar listesinde adı var, koşum sonrası `Test-Path` → `False` |
| `Reaping_leaves_the_root_of_a_live_process_alone` | Kendi PID'imizle ikinci bir kök | Toplananlar listesinde adı **yok**, kök yerinde duruyor |

Ölü PID `Process.GetProcesses()` ile canlı kümesi çıkarılarak seçilir; ilk kolun
`AbandonedRootKeysBeforeReap > 0` iddiası yapay kökün gerçekten kurulduğunu da tutar.

## K7 — Mutasyon Izgarası

Her mutasyondan önce `dotnet build -c Release --no-incremental`; `--no-build`
kullanılmadı.

| Mutasyon | Kırılan ölçü | Kalan / toplam |
| --- | --- | --- |
| (a) `Remove-ShellMenu`'den `$shellShrinkMenuKeyName` çıkarıldı | `Key_count_goes_from_zero_up_and_back_to_zero`, `Removal_erases_both_entries_and_the_emptied_parents` | 13 / 15 |
| (b) `$shellShrinkTargets` betiğe ikinci kez yazıldı | `Installer_target_list_is_the_application_target_list` | 14 / 15 |
| (c) Toplayıcıdan `Remove-Item` satırı çıkarıldı | `Abandoned_root_from_a_dead_process_is_reaped_at_run_start` | 14 / 15 |
| (d) Toplayıcıdan canlı PID koruması (`Get-Process ... { continue }`) çıkarıldı | `Reaping_leaves_the_root_of_a_live_process_alone` | 14 / 15 |
| (e) `Write-ShellShrinkMenu` yolunu `$Root` yerine gömülü bir `HKCU:` kökünden kurdu | `Registry_writers_never_hard_code_the_real_shell_root` + 6 kol | 8 / 15 |

(e) gömülü kökü gerçek `HKCU:\Software\Classes` değil, zararsız bir
`HKCU:\Software\VidShrinkMutasyon-<pid>` seçildi; ölçünün aradığı `HKCU:` deseni
ikisinde de aynı, gerçek ağaç ise mutasyon sırasında da açılmadı. O kök koşumdan
sonra silindi.

Ham çıktı:

```
--- mutasyon (a) ---
[xUnit.net]  VidShrink.Tests.ShellShrinkMenuTests.Key_count_goes_from_zero_up_and_back_to_zero [FAIL]
[xUnit.net]  VidShrink.Tests.ShellShrinkMenuTests.Removal_erases_both_entries_and_the_emptied_parents [FAIL]
Başarısız! - Başarısız: 2, Başarılı: 13, Atlanan: 0, Toplam: 15

--- mutasyon (b) ---
[xUnit.net]  VidShrink.Tests.ShellShrinkMenuTests.Installer_target_list_is_the_application_target_list [FAIL]
Başarısız! - Başarısız: 1, Başarılı: 14, Atlanan: 0, Toplam: 15

--- mutasyon (c) ---
[xUnit.net]  VidShrink.Tests.ShellShrinkMenuTests.Abandoned_root_from_a_dead_process_is_reaped_at_run_start [FAIL]
Başarısız! - Başarısız: 1, Başarılı: 14, Atlanan: 0, Toplam: 15

--- mutasyon (d) ---
[xUnit.net]  VidShrink.Tests.ShellShrinkMenuTests.Reaping_leaves_the_root_of_a_live_process_alone [FAIL]
Başarısız! - Başarısız: 1, Başarılı: 14, Atlanan: 0, Toplam: 15

--- mutasyon (e) ---
[xUnit.net]  VidShrink.Tests.ShellShrinkMenuTests.Registry_writers_never_hard_code_the_real_shell_root [FAIL]
[xUnit.net]  VidShrink.Tests.ShellShrinkMenuTests.Shrink_entry_is_written_for_every_media_extension [FAIL]
[xUnit.net]  VidShrink.Tests.ShellShrinkMenuTests.Written_targets_are_the_application_target_list [FAIL]
[xUnit.net]  VidShrink.Tests.ShellShrinkMenuTests.Removal_erases_both_entries_and_the_emptied_parents [FAIL]
[xUnit.net]  VidShrink.Tests.ShellShrinkMenuTests.Two_extension_dump_shows_the_parent_verb_and_its_subcommands [FAIL]
[xUnit.net]  VidShrink.Tests.ShellShrinkMenuTests.Both_menu_levels_declare_the_single_process_multi_select_model [FAIL]
[xUnit.net]  VidShrink.Tests.ShellShrinkMenuTests.Every_target_calls_the_launcher_with_its_size_and_the_path [FAIL]
Başarısız! - Başarısız: 7, Başarılı: 8, Atlanan: 0, Toplam: 15

--- hepsi geri alındıktan sonra ---
Başarılı!  - Başarısız: 0, Başarılı: 15, Atlanan: 0, Toplam: 15
```

## K8 — Kol Başına Test Sayısı

| Filtre kolu | Bulunan test |
| --- | --- |
| `FullyQualifiedName~ShellShrinkMenuTests` | 15 |
| `FullyQualifiedName~ShellMenuTests` | 15 |
| `FullyQualifiedName~Windows11ShellMenuTests` | 7 |

Sıfır bulan kol yok. Orta kolun sayısı iki sınıfı birden kapsar: `ShellMenuTests`
adı `Windows11ShellMenuTests` içinde de geçtiği için o kol 8 kendi testine 7 Windows 11
testini ekler.

## Kapsam Dışı Kalan

Argüman çözümü (`--kucult <hedef> <yol>`) ve çoklu seçimin tek kuyruğa inmesi bu
sözleşmeden çıkarıldı, T170'e alındı. Bu belge yalnız menünün kayıt defteri ayağını
ölçer: alt fiillerin ürettiği komut satırı yazılıdır, ama komutu karşılayan uygulama
tarafı henüz yoktur.

Kayıt defterindeki `MultiSelectModel="Player"` değeri iki düzeyde de yazılır ve
ölçülür; çoklu seçimde kabuğun tek çağrı üretmesini bu değer ister. Uygulama tarafının
o çağrıyı tek kuyruğa indirmesi T170'in işidir.
