# Plan — `-tune` Gelişmiş Panele Açılıyor

Defter satırı E1 (kodlayıcı ayar yüzeyi). Karar:
[024-ince-ayar-yuzeyi.md](netlestirme/024-ince-ayar-yuzeyi.md) — fable yalnız `-tune`'un
açılmasını, `-level:v` ile serbest `encoder-opts`'un kapalı kalmasını söyledi. Aynı kararın
CLI parçası `77cbf7c2` ile kapandı.

## Karar

Liste dar tutuluyor: küçültmede işe yarayan üç x264/x265 değeri (`film`, `animation`,
`grain`) ve SVT-AV1'in `0..3` tune numarası. Kaydedicinin `TunesFor` merdiveni
(`zerolatency`, `ull`, `psnr`) küçültmede kullanılmaz, o yüzden yeniden kullanılmıyor —
küçültme kolunun kendi merdiveni yazılıyor.

Kodeğe uymayan değer ön ayarda olduğu gibi **düşer**: koşum çakmaz, gerekçeye satır düşer.

## Adımlar

1. `FfmpegArguments.TunesFor(codec)` ve `IsValidTune(codec, tune)` — küçültme kolunun
   merdiveni. x264/x265 → `film|animation|grain`, libsvtav1 → `0|1|2|3`, geri kalan boş.
2. `EncodePlan.Tune` alanı, `PlanOptions.LockedTune`, `PlanCalculator`'da uygulama +
   düşme gerekçesi + `ReasonCode.ManualTuneOverride`.
3. `FfmpegArguments`: x264/x265 için `-tune <ad>`; libsvtav1 için var olan
   `-svtav1-params tune=1` yerine kullanıcının numarası. İki yerde birden yazılmaz.
4. Arayüz: `CmbAdvTune` + `TxtAdvTuneNow`, ön ayar satırının hemen altına; `LockedTune`'a
   bağlanır, "Otomatik" ilk sıra.
5. Metin: `main.advanced.tune.label` 42 dile.
6. Ölçü: `TuneYuzeyiTests` — merdiven, düşme, argümana yazılma, svtav1 çakışması,
   panelin kilidi kurması. Gerçek ffmpeg ile tek kısa koşum, `docs/olcumler/`e.

## Kapsam dışı

- `-level:v` ve serbest `encoder-opts` (fable kapalı dedi).
- NVENC `-tune hq/ll` — küçültmede kalite kipi zaten `p`-ön ayarından türüyor, ölçülmedi.
