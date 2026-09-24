# E4 — Başlık ve Kaynak Tarama (19 Eylül 2026)

HandBrake'in "Title" açılır kutusunun karşılığı. Bugüne kadar kaynak ne olursa olsun tek
başlık varsayılıyordu: `FfprobeClient` `attached_pic` olmayan **ilk** video akışını alıyor,
çok programlı bir yayında yanlış programa düşebiliyordu.

## Ölçülen ffmpeg yeteneği (9.0-full_build)

`dvdvideo` demuxer var ve `-title`, `-angle`, `-chapter_start/-chapter_end`, `-pgc`,
`-preindex` seçeneklerini taşıyor. Blu-ray için demuxer **yok** — `-demuxers` listesinde
`bluray` geçmiyor, bu yüzden BD bu turda kapsam dışı. ffprobe `-show_programs` taşıyor.

Yerelde iki programlı bir MPEG-TS üretilip doğrulandı; ölçü aynı komutu kendisi kuruyor:

```
program 1  Kisa Baslik  [(0, video, 3.000000), (1, audio, 3.004089)]
program 2  Uzun Baslik  [(2, video, 5.000000)]
```

## Değişen yüzeyler

| Yüzey | Dosya | Ne yapıyor |
| --- | --- | --- |
| Başlık modeli | `src/VidShrink.Core/SourceTitles.cs` | `SourceTitle`, `DiscSource`, eleme/seçme/daraltma kuralları |
| Yoklama | `src/VidShrink.Ffmpeg/FfprobeClient.cs` | `-show_programs`, programlar başlığa çevriliyor |
| Kaynak bilgisi | `src/VidShrink.Core/MediaInfo.cs` | `Titles` listesi ve `HasMultipleTitles` |
| Girdi argümanı | `src/VidShrink.Core/FfmpegArguments.cs` | `plan.Disc` doluysa `-i`'den önce demuxer seçimi |
| Komut satırı | `src/VidShrink.Cli/CliRequest.cs`, `CliApp.cs` | Beş bayrak, `--tarama` envanteri basıyor |
| Arayüz | `src/VidShrink.App/MainWindow.axaml`, `.axaml.cs` | Başlık seçici yalnız birden çok başlıkta görünüyor |
| Diller | `src/VidShrink.Cli/Locales/*.json`, `App/Locales/<dil>/main.json` | Yardım satırları, altı hata anahtarı, `main.title.label` |

## Program seçimi eşlemeyi değil envanteri daraltıyor

`StreamMapping` eşlemeleri zaten **mutlak** indeksle yazıyor (`StreamMapping.cs:368`,
`"0:" + stream.Index`). Bu yüzden bir programı seçmek `-map 0:p:N` sözdizimine geçmek değil,
`MediaInfo.Streams`'i o programın indekslerine indirmek demek: `SourceTitles.Uygula`.
Eşleme katmanı hiç değişmedi, eski kolların hepsi olduğu gibi kaldı.

DVD ayrı: orada demuxer'in kendisi değişiyor, o yüzden `EncodePlan.Disc` dolu olduğunda
`-i`'den **önce** `-f dvdvideo -title N [-angle A]` yazılıyor.

## CLI yüzeyi

| Bayrak | Karşılığı | Ne yapar |
| --- | --- | --- |
| `--tarama` | `--scan` | Envanteri basar, hiçbir şey kodlamaz |
| `--baslik N` | `--title N` | Numarayla başlık seçer |
| `--ana-icerik` | `--main-feature` | En uzun başlığı seçer |
| ~~`--aci N`~~ | ~~`--angle N`~~ | Kaldırıldı: CLI disk kaynağını hiç kurmuyordu, açı hiçbir koşuma ulaşmıyordu |
| `--asgari-sure SN` | `--min-duration SEC` | Kısa başlıkları envanterden düşer |

`--baslik` ile `--ana-icerik` birlikte verilemiyor; iki sırada da reddediliyor.

## Mutasyon tablosu

Ölçü `tests/VidShrink.Tests/BaslikTaramaTests.cs`, filtre
`FullyQualifiedName~BaslikTarama`, sürücü `.calisma/e4/mutasyon.py`. Her tur
`--no-incremental` ile yeniden derleniyor.

Taban 13/13 yesil, geri alma sonrasi yine 13/13 yesil.

| Mutasyon | Ne bozuldu | Kirmizi |
| --- | --- | --- |
| M1 | `-show_programs` bayragi kaldirildi | 1 |
| M2 | `AnaIcerik` en kisa basligi seciyor | 1 |
| M3 | esitlikte `ThenBy(Number)` yok | 1 |
| M4 | `Ele` asgari sureyi uygulamiyor | 2 |
| M5 | esik `>` oldu, tam esikteki baslik eleniyor | 1 |
| M6 | olmayan numara `error.bad-title` vermiyor | 3 |
| M7 | `Uygula` envanteri daraltmiyor | 1 |
| M8 | `--tarama` kodlamayi durdurmuyor | 2 |
| M9 | `--baslik` kolu adi bozuldu | 2 |
| M10 | `--baslik` + `--ana-icerik` reddi kaldirildi | 1 |
| M11 | `plan.Disc` girdi argumani yazilmiyor | 1 |
| M12 | `-angle` argumani yazilmiyor | 1 |
| M13 | bos envanterde baslik secimi yine de calisiyor | 1 |

On uc mutasyonun on ucu de kirmizi verdi; sifir kirmizi kalan kol yok.

M13 sonradan eklendi: ilk surumde baslik envanteri bos gelen bir yoklama (sahte servis ya da
`Titles` tasimayan eski yol) `error.no-titles` ile 64 donduruyordu ve
`CliTests.PlanJsonuStdoutaIlerlemeStderreGidiyor` kirmiziya dustu. Artik secim yalnizca
envanter doluyken ya da kullanici acikca baslik isterken calisiyor; kol
`BaslikTasimayanYoklamaKucultmeyiDurdurmuyor` ile pimlendi.
