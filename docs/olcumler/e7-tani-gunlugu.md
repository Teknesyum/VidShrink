# E7 — Tanı Günlüğü (19 Eylül 2026)

HandBrake'in Activity Log'unun karşılığı. Kullanıcı "dönüştürme başarısız oldu" dediğinde
elinde tek bir dosya olacak; "önce günlüğü aç, sonra tekrar dene" demek kullanıcıyı ikinci
kez çalıştırmaktır, o yüzden günlük her koşumda kendiliğinden yazılıyor.

Tek sert kural gizlilik: **hiçbir satırda tam yol bulunmuyor.** Günlüğü paylaşan kullanıcı
kullanıcı adını, klasör ağacını ve sürücü harfini paylaşmış olmuyor.

## Değişen yüzeyler

| Yüzey | Dosya | Ne yapıyor |
| --- | --- | --- |
| Günlük motoru | `src/VidShrink.Core/Gunluk.cs` | Blok üretimi, yol indirgeme, yazma, 1 MB'da devirme |
| Koşum kancası | `src/VidShrink.Ffmpeg/EncodeRunner.cs:653` | Her ffmpeg koşumundan sonra blok yazılıyor |
| ffmpeg sürümü | `src/VidShrink.Ffmpeg/ToolLocator.cs` | `FfmpegVersion` artık `Lazy<string>`, her koşumda süreç açmıyor |
| Komut satırı | `src/VidShrink.Cli/CliRequest.cs`, `CliApp.cs` | `--gunluk` / `--log` / `gunluk` kolu günlüğü basıyor |
| Ayarlar sekmesi | `src/VidShrink.App/MainWindow.axaml`, `.axaml.cs` | "Günlüğü aç" düğmesi, sıfırlama panelinin komşusu |
| Veri sıfırlama | `src/VidShrink.App/AppDataReset.cs` | Günlük klasörü de siliniyor, boş kalırsa klasör de gidiyor |
| Diller | `src/VidShrink.App/Locales/<dil>/settings.json` | 42 dile beş `settings.log.*` anahtarı |

Günlük klasörü ayar dosyasını izliyor: `VIDSHRINK_SETTINGS_PATH` verilmişse onun yanında,
yoksa `%APPDATA%\VidShrink\gunluk`. Dosya `vidshrink.log`, devirmede tek yedek
`vidshrink.1.log`. Eşik 1 MB, stderr penceresi son 15 satır.

## Blok biçimi

```
=== 2026-09-19 12:57:15 +03:00 ===
vidshrink: 1.4.0
ffmpeg: ffmpeg version 9.0-full_build
komut: ffmpeg -i kaynak.mp4 -c:v libx264 cikti.mp4
cikis: 0  sure: 1,2 sn
stderr:
  ...son 15 satır...
```

Yol indirgeme yalnız yol gibi görünen metne dokunuyor: `-vf scale=1280:-2 -c:v libx264`
olduğu gibi kalıyor, çünkü ffmpeg'in kendi anahtarları da `:` taşıyor.

## Mutasyon tablosu

Ölçü `tests/VidShrink.Tests/TaniGunluguTests.cs` + `VeriSifirlamaTests.cs`,
filtre `FullyQualifiedName~TaniGunlugu|FullyQualifiedName~VeriSifirlama`, sürücü
`.calisma/e7/mutasyon.py`. Her tur `--no-incremental` ile yeniden derleniyor.

| Kol | Kırılan | Kırmızı |
| --- | --- | --- |
| taban | — | 0 / 16 |
| M1 | `YoluIndirge` metni olduğu gibi döndürüyor | 3 |
| M2 | Kuyruk sınırı 15 → 100 | 1 |
| M3 | Devirme eşiği 1 MB → 1 GB | 1 |
| M4 | Devirme eskiyi silmiyor | 1 |
| M5 | Blok ffmpeg sürümünü yazmıyor | 1 (2 asert) |
| M6 | Yazamama başarı sayılıyor | 1 |
| M7 | `EncodeRunner` günlüğü hiç çağırmıyor | 1 |
| M8 | Sıfırlama günlük klasörüne dokunmuyor | 1 |
| M9 | CLI `--gunluk` kolu tanınmıyor | 1 |
| geri | — | 0 / 16 |

**İlk turda iki kör nokta çıktı ve ikisi de kapatıldı.** M2 hiç kırmızı vermiyordu: ölçü
kuyruk penceresini `Gunluk.KuyrukSatiri` ile sayıyordu, yani sabit değişince beklenti de
onunla kayıyordu (aynı tuzak: `sabit-karsilastiran-test-davranis-olcmez`). Pencere artık
`15` sayısıyla pimli. M8 de kırmızı vermiyordu: veri sıfırlamanın günlük klasörünü silmesi
hiç ölçülmemişti; `VeriSifirlamaTests.SifirlamaTaniGunluguDeSiler` eklendi.

`Gunluk.Yaz` ikinci bir imza kazandı (`Yaz(klasor, blok)`): devirme ölçüsü kendi klasörünü
kuruyor, böylece paralel koşan başka bir sınıfın aynı dosyaya yazması ölçüyü yarışa
çevirmiyor.

## Canlı kol

`GercekKosumGunlugeDuserVeYolTasimaz` gerçek ffmpeg 9.0 ile 160x120 iki saniyelik bir
kaynak üretip `EncodeRunner.RunAsync` koşuyor, sonra günlüğün büyüdüğünü, `komut:` ve
`ffmpeg:` alanlarının dolduğunu, `kaynak.mp4` adının geçtiğini ve **çalışma klasörünün tam
yolunun hiçbir satırda bulunmadığını** doğruluyor. ffmpeg yoksa kol atlanıyor.
