# Kaydedici Ses Seçenekleri

Later 64: yalnız ses kaydı, ses kodeği seçimi, `acompressor` ve `alimiter`. Ölçüm 2026-10-15, ffmpeg 9.0,
`-threads 2`, tek süreç, lavfi kaynak (ses çalınmadı).

## Opus Hangi Kapta

| Kap | `-c:a libopus` doğrudan | Matroska yakalamadan `-c copy` |
|---|---|---|
| mkv | açılır | — |
| mp4 | açılır | açılır |
| mov | -22 ile düşer | -22 ile düşer |

Sonuç: `RecorderArguments.AudioCodecsFor` MP4 ve MKV'de AAC + Opus, MOV'da yalnız AAC verir. MOV seçilince
kutu tek ögeye iner ve pasifleşir; kayıtlı Opus seçimi isteğe AAC olarak iner.

## Zincir Sırası

Girdi: 440 Hz sinüs, +24 dB kazanç (tepe +5,94 dBFS), `volumedetect` ile okunan tepe.

| Zincir | Tepe (dBFS) |
|---|---|
| süzgeçsiz | +5,94 |
| `alimiter` | 0,000 |
| `acompressor` | +0,83 |
| `acompressor,alimiter` | 0,000 |

Sıkıştırıcı tek başına tepeyi sıfırın altına indirmiyor; sınırlayıcı son halkada olunca tepe 0 dBFS'te
kalıyor. Zincir sırası: kazanç, gürültü kapısı, gürültü bastırma, `acompressor`, `alimiter`.

## Ölçülmeyenler

- Gerçek mikrofonla yalnız ses kaydı (testler sahte cihaz listesi ve argüman dizisiyle).
- macOS ve Linux yalnız ses argümanı koşturulmadı.
- Tıklama halkası, tuş gösterimi ve büyüteç bindirmeleri yalnız ses hedefinde de açılabilir; etkisi ölçülmedi.
