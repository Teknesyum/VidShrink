# Plan — `--kes`'in Bölüm ve Kare Kolları

Defter satırı: teslim edilen `--kes` yalnız saniye ve zaman damgası alıyor. Kurulu
HandBrake 1.11.2 `--help` ile doğrulandı (`docs/olcumler/handbrake-cli-yetenek.md:84-104`):
`-c/--chapters 1-3` ve `--start-at/--stop-at` üç birimde (`seconds:`, `frames:`, `pts:`).
Bizde ikisi de yok. `pts:` bu turda kapsam dışı — kullanıcıya bir şey kazandırmıyor,
saniye ile aynı bilgiyi 90 kHz sayacıyla söylüyor.

## Kapsam

1. **Kare kolu** — `--kes 300f-900f`. Kare numarası ayrıştırmada kabul edilir, saniyeye
   çevirme çalışma zamanında `MediaInfo.Fps` ile yapılır (ayrıştırıcı kaynağı görmez).
   Karışık yazım (`10-900f`) da geçerli: iki uç bağımsız.
2. **Bölüm kolu** — `--bolum 2` ve `--bolum 2-4`. Bölüm sınırları kaynaktan gelir;
   ffprobe zaten `-show_chapters` çağırıyor, ama `MediaInfo` yalnız `ChapterCount`
   tutuyor. Başlangıç/bitiş saniyeleri taşınacak.
3. `--kes` ile `--bolum` birlikte verilemez: ikisi de kesit penceresi kurar.

## Dokunulan dosyalar

- `src/VidShrink.Core/MediaInfo.cs` — `ChapterMark` kaydı ve `Chapters` listesi
  (`ChapterCount` listeden türer, alan silinmez ki çağıranlar kırılmasın).
- `src/VidShrink.Ffmpeg/FfprobeClient.cs` — `start_time`/`end_time`/`tags.title` okunur.
- `src/VidShrink.Cli/CliRequest.cs` — `TryParseRange` kare ekini tanır, yeni
  `--bolum`/`--chapters` durumu, `ToPlanOptions`'ta pencere çözümü.
- `src/VidShrink.Cli/Locales/{en,tr}.json` — `error.bad-chapter`, `error.chapter-and-cut`,
  yardım metnine iki satır.
- `README.md` / `README.tr.md` — takma ad satırı ve anlatım.
- `tests/VidShrink.Tests/KesBolumKareTests.cs` — yeni.

## Ölçü

Ayrıştırma kolları sürece çıkmaz. Bölüm çözümü için gerçek bir bölümlü mkv
`StreamMappingTests`'in ürettiği kaynakla aynı yoldan üretilir (iki bölüm zaten var).
Kanıt `docs/olcumler/e-kes-bolum-kare.md`: gerçek CLI koşumunun `-ss`/`-t` değerleri.

Negatif kontroller: bölümsüz kaynakta `--bolum 1` hata, aralık dışı bölüm hata,
kare eki olmayan sayı eskisi gibi saniye sayılır, `--kes` ile `--bolum` birlikte hata.
