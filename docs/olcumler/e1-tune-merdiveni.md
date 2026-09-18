# `-tune` Merdiveni — Gerçek ffmpeg Ölçümü

fable 024 kararında merdiveni "x264/x265 `film|animation|grain`, SVT-AV1 `0..3`" diye
vermişti. İkisi de bu ffmpeg 9.0 (gyan.dev) yapısında tutmadı; merdiven ölçüme göre
yazıldı. Kaynak `.calisma/dalga2/parca-2ses-1altyazi.mkv`, her koşum 1-2 saniyelik kesit.

## x264 / x265

| kodlayıcı | tune | sonuç |
| --- | --- | --- |
| libx264 | film | kabul (13806 bayt) |
| libx264 | animation | kabul (14227 bayt) |
| libx264 | grain | kabul (17603 bayt) |
| libx265 | film | **red** — `Error setting preset/tune veryfast/film.` |
| libx265 | animation | kabul (12967 bayt) |
| libx265 | grain | kabul (19495 bayt) |

x265'in tune listesinde `film` yok. Merdiven ikiye ayrıldı.

## SVT-AV1

```
> ffmpeg ... -c:v libsvtav1 -preset 10 -crf 45 -svtav1-params tune=N
tune=0 -> kabul (19139 bayt)
tune=1 -> kabul (19143 bayt)
tune=2 -> kabul (16606 bayt)
tune=3 -> Svt[error]: Tune IQ only supports all-intra and low delay (experimental) prediction structures
```

`tune=3` kodlayıcıyı hiç açmıyor (`Error setting encoder parameters: bad parameter`),
çıktı 0 bayt. Küçültme sıradan yapıyı kullandığı için merdiven `0|1|2`.

Aynı koşumda `-tune 3` ayrı bayrak olarak da denendi: SVT-AV1 `-tune`'u ayrı bayrak
kabul etmiyor, değer `-svtav1-params` içine giriyor — kodun yazdığı yer burası.
