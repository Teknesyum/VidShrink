# Kabuk Menüsü: "VidShrink ile Küçült" Girdisi ve Hızlı Hedef Alt Menüsü

T169, tur 3. Ölçüm makinesi: Windows 11 Pro 10.0.26100, .NET 8, Release.

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

`.mkv` dökümü aynı biçimde çıkar; tek fark uzantı adıdır:

```
[HKEY_CURRENT_USER\<TEST-KOKU>\SystemFileAssociations\.mkv\shell\VidShrink]
"MUIVerb"="Bu Videoyu VidShrink ile Aç"
"Icon"="<KURULUM>\\VidShrink.exe"
[HKEY_CURRENT_USER\<TEST-KOKU>\SystemFileAssociations\.mkv\shell\VidShrink\command]
@="\"<KURULUM>\\VidShrink.exe\" \"%1\""
[HKEY_CURRENT_USER\<TEST-KOKU>\SystemFileAssociations\.mkv\shell\VidShrinkKucult]
"MUIVerb"="VidShrink ile Küçült"
"SubCommands"=""
"MultiSelectModel"="Player"
[HKEY_CURRENT_USER\<TEST-KOKU>\SystemFileAssociations\.mkv\shell\VidShrinkKucult\shell\500\command]
@="\"<KURULUM>\\VidShrink.exe\" --kucult 500 \"%1\""
```

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
| `Measurement_root_is_never_the_real_shell_root` | Kök adı `HKCU:\Software\VidShrinkKucult-Test-` ile başlar, içinde `Classes` geçmez |
| `Installer_still_defaults_to_the_real_shell_root` | Betiğin varsayılan `-RegistryRoot` değeri değişmedi |
| Koşum sonu | Kök silindi; `Test-Path` → `False` |
| Gerçek `HKCU\Software\Classes\SystemFileAssociations` | `VidShrinkKucult` adlı anahtar sayısı: 0 |

Gerçek ağaç yalnız okundu, hiç yazılmadı.

## K7 — Mutasyon Izgarası

Her mutasyondan önce `dotnet build -c Release --no-incremental`; `--no-build`
kullanılmadı.

| Mutasyon | Kırılan ölçü | Kalan / toplam |
| --- | --- | --- |
| (a) `Remove-ShellMenu`'den `$shellShrinkMenuKeyName` çıkarıldı | `Key_count_goes_from_zero_up_and_back_to_zero`, `Removal_erases_both_entries_and_the_emptied_parents` | 11 / 13 |
| (b) `$shellShrinkTargets` betiğe ikinci kez yazıldı | `Installer_target_list_is_the_application_target_list` | 12 / 13 |

Ham çıktı:

```
--- mutasyon (a) ---
[xUnit.net]     ShellShrinkMenuTests.Key_count_goes_from_zero_up_and_back_to_zero [FAIL]
[xUnit.net]     ShellShrinkMenuTests.Removal_erases_both_entries_and_the_emptied_parents [FAIL]
Başarısız! - Başarısız: 2, Başarılı: 11, Atlanan: 0, Toplam: 13

--- mutasyon (b) ---
[xUnit.net]     ShellShrinkMenuTests.Installer_target_list_is_the_application_target_list [FAIL]
Başarısız! - Başarısız: 1, Başarılı: 12, Atlanan: 0, Toplam: 13

--- ikisi de geri alındıktan sonra ---
Başarılı!  - Başarısız: 0, Başarılı: 13, Atlanan: 0, Toplam: 13
```

## K8 — Kol Başına Test Sayısı

| Filtre kolu | Bulunan test |
| --- | --- |
| `FullyQualifiedName~ShellShrinkMenuTests` | 13 |
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
