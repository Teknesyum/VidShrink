# Sabit Çıktı Klasörü Gerçekten Çalışıyor (Defter 28 Sınıfı)

Ayarlardaki "Sabit klasör" kipi kaydediliyor, geri yükleniyor ve klasör seçme satırını
gösterip gizliyordu — ama çıktı yolu yalnız kaynağın klasöründen kuruluyordu
(`ShrinkEngine.UniqueOutputPath`, `Path.GetDirectoryName(inputPath)`). Dört çağrı yerinin
hiçbiri klasörü geçirmiyordu.

Arayüzdeki ipucu ise şunu söylüyordu: **"Sabit klasör her çıktıyı kaynağın yanı yerine hep
aynı yere gönderir."** Söylenen ile olan farklıydı; bu, kullanıcıya yalan söyleyen ayar
sınıfına giriyor.

## Değişen yüzey

| Ne | Nerede |
|---|---|
| `outputDirectory` parametresi | `ShrinkEngine.UniqueOutputPath` |
| `UsableFixedFolder(mode, folder)` | `MainWindow.axaml.cs` (yeni, `internal static`) |
| `FixedFolderUnusable` | `MainWindow.axaml.cs` (yeni) |
| `BuildUniqueOutputPath` artık klasörü geçiriyor | dört çağrı yeri |
| `settings-tab.output-folder.unusable` | 42 dil |

**Sessiz geri düşüş yok.** Klasör yoksa ya da yazılamıyorsa çıktı kaynağın yanına düşer,
ama kullanıcıya söylenir. Var olmak yetmez: sıfır baytlık bir sonda yazılıp silinir, çünkü
yazma hakkı olmayan klasöre çıktı gitmez.

CLI'ın `--cikti` kolu dokunulmadı: `CliApp.cs:227-229` klasörü zaten kendisi birleştiriyor.

## Mutasyonlar

Filtre: `SabitCiktiKlasoruTests`

| # | Kesim | Kırmızı |
|---|---|---|
| taban | — | 0/14 |
| M1 | `UniqueOutputPath` `outputDirectory`'yi yok sayıyor | 2 |
| M2 | `UsableFixedFolder` kipe bakmıyor | 1 |
| M3 | Yazma sondası kaldırıldı | 1 |
| M4 | `Directory.Exists` denetimi kaldırıldı | **0 — davranış eşdeğeri** |
| M5 | `OnStart`'taki uyarı satırı kaldırıldı | 1 |
| M6 | `BuildUniqueOutputPath` klasörü geçirmiyor | 1 |

**M4 bir kör nokta değil, eşdeğer mutasyon.** `Directory.Exists` düşünce sonda çalışıyor ve
olmayan klasöre yazma `DirectoryNotFoundException` atıyor; o da `IOException`'dan türüyor,
aynı `catch` yakalıyor ve `null` dönüyor. Denetim ucuz ve niyeti okunur kıldığı için
duruyor, ama tek başına bir davranış taşımıyor.

## Ölçünün kolları

`SabitCiktiKlasoruTests` (14):

- Seçilen klasöre yazılıyor; klasör verilmezse kaynağın yanında kalıyor (üç olumsuz kontrol:
  `null`, boş, boşluk).
- Çakışma sayacı **hedef** klasörde işliyor: kaynaktaki aynı adlı dosya sayacı tetiklemiyor,
  hedeftekı tetikliyor (`klip_shrunk_2.mp4`).
- `Kaynağın yanı` kipinde sabit klasör hiç okunmuyor.
- Yazılabilir klasör kullanılıyor ve sonda arkasında dosya bırakmıyor.
- Olmayan klasör, boş klasör adı kullanılamıyor.
- Yazma hakkı açıkça reddedilmiş klasör (Windows ACL, `CreateFiles` Deny) kullanılamıyor;
  hak geri verilince aynı klasör kullanılabiliyor. Linux'ta atlanıyor — konteynerde kök
  kullanıcı izni geçersiz kılıyor.
- Uyarı metni 42 dilde var ve `{0}` yer tutucusunu koruyor.
- Pencereye bağlayan iki satırın kaynak pimi.

Geçici klasörler her koşumda sıfırlanıyor: ilk turda `CakismaSayaciHedefKlasordeIsliyor`
kendi bıraktığı dosyadan kırmızı oldu.
