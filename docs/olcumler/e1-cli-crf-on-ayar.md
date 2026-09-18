# CLI'da `--crf` ve `--on-ayar` — Gerçek Koşum

Kaynak `.calisma/dalga2/parca-2ses-1altyazi.mkv`, komut `vidshrink plan ... --hedef 25 --json`,
okunan alan `plan`. Karar `docs/netlestirme/024-ince-ayar-yuzeyi.md`.

| Bayrak | kodek | ön ayar | crf |
| --- | --- | --- | --- |
| yok | h264 | copy | — |
| `--on-ayar veryslow` | libx264 | veryslow | 20 |
| `--on-ayar ultrafast` | libx264 | ultrafast | 36 |
| `--crf 26 --on-ayar slow` | libx264 | slow | 26 |
| `--on-ayar p5` (uyumsuz) | libx264 | slow | 22 |

Son satır kararın kendisi: `p5` NVENC merdiveninde, seçilen kodlayıcı `libx264`. Koşum
çakmıyor, ad düşüyor ve plan motorun seçtiği `slow` ile çıkıyor. Bu satırdan önce
`PlanCalculator` aynı durumda `ArgumentException` atıyordu — arayüzde kodek değiştirilince
de aynı çakma vardı, birlikte kapandı.

Sınır kolu:

```
> vidshrink plan ... --hedef 25 --crf 99
CRF 0 ile 63 arasında bir sayı olmalı: 99
Kullanım için 'vidshrink --help' yazın.
```
