# E8 — Kap Uyumluluğu: `--align-av` ve `--ipod-atom`

HandBrake'in iki kap bayrağının VidShrink karşılığı. Her sayı gerçek
`ffmpeg 9.0-full_build-www.gyan.dev` koşumundan; betikler `.calisma/e8/olcum*.sh`,
ham çıktılar aynı klasörde `olcum1.out`, `olcum2.out`, `olcum3.out`.

Ölçüm tarihi: 2026-09-19.

## 1. Değişen yüzey

| Yer | Önce | Sonra |
| --- | --- | --- |
| `StreamMapping.SesHizalama` | yok | `aresample=async=1:first_pts=0` |
| `StreamPlan.AudioCodecArguments` | `-c:a -b:a [-ac]` | yeniden kodlanan ize ayrıca `-filter:a<N>` |
| kopyalanan ses izi | `-c:a copy` | değişmedi (kopyada süzgeç kurulamaz) |
| `FfmpegArguments.BuildTrimCopy` | damga yok | `-avoid_negative_ts make_zero` |
| `FfmpegArguments.Build` (yeniden kodlama) | damga yok | değişmedi |
| `FfmpegArguments.BuildSegment` | damga yok | değişmedi |
| `ipod` muxer | yok | **uygulanmadı**, gerekçe §4 |

## 2. `--align-av` — ses videoyla aynı anda başlıyor mu

Kaynak: `itsoffset 0.4` ile sesi 0,4 sn geç başlayan mkv (`olcum2.out`, son bölüm).

| Koşum | video başlangıcı | ses başlangıcı |
| --- | --- | --- |
| kaynak | 0,000000 | 0,400000 |
| bugünkü teslim (süzgeçsiz yeniden kodlama) | 0,000000 | 0,376780 |
| `aresample=async=1:first_pts=0` ile | 0,000000 | 0,000000 |

Kayma kabın içinde taşınıyordu: kopyalamada 0,377007, yeniden kodlamada 0,376780.
Süzgeç başa sessizlik doldurup sesi sıfıra çekiyor. Ses akışının süresi de kabın
süresine oturuyor (`olcum3.out`): süzgeçsiz 4,023220 / kap 4,400000, süzgeçli 4,400000.

**Hizalı kaynakta bedeli yok.** Zaten 0/0 başlayan kaynakta çıktı bayt bayt aynı kalıyor;
süzgeç yalnız bozuk kaynakta iş yapıyor. Bu yüzden koşul aranmadan her yeniden kodlanan
ize yazılıyor, kopyalanan ize yazılamıyor (ffmpeg kopya izde filtre kabul etmez).

## 3. `-avoid_negative_ts` — hangi değer ne yapıyor

`-ss 1.2 -copyts` ile kesilen mp4, 0,5 sn'de bir anahtar kare (`olcum2.out`).

| Değer | video | ses | kap süresi |
| --- | --- | --- | --- |
| `disabled` | 1,000000 | 0,998458 | 0,998458 → 3,001542 |
| `auto` | 1,000000 | 0,998458 | 0,998458 → 3,001542 |
| `make_non_negative` | 1,000000 | 0,998458 | 0,998458 → 3,001542 |
| `make_zero` | 0,001563 | 0,000000 | 0,000000 → 3,001563 |

Yalnız `make_zero` sıfıra oturtuyor; `auto` ancak damga gerçekten negatifse kaydırıyor.
MKV kabında da aynı: `disabled` 1,000000/1,021000, `make_zero` 0,000000/0,021000.

VidShrink'in gerçek argüman biçimleriyle (`olcum3.out`):

| Yol | bugünkü | `make_zero` ile | karar |
| --- | --- | --- | --- |
| `BuildTrimCopy` (kesit kopyası) | 0,300000 / 0,007438 | 0,292578 / 0,000000 | **eklendi** |
| `Build` + kesit (yeniden kodlama) | 0,000000 / 0,000000 | — | eklenmedi, zaten sıfır |
| `BuildSegment` (parça kopyası) | — | — | eklenmedi, parça süresini uzatıyor |

## 4. `--ipod-atom` — uygulanmadı

`ipod` muxer ffmpeg 9.0'da var (`E  ipod  iPod H.264 MP4 (MPEG-4 Part 14)`), ama
düz `mp4` muxer'ından yalnız marka etiketiyle ayrılıyor (`olcum1.out`):

```
-- ipod:  TAG:major_brand=M4V   TAG:compatible_brands=M4V isomiso2avc1
-- mp4 :  TAG:major_brand=isom  TAG:compatible_brands=isomiso2avc1mp41
```

Buna karşılık HEVC'yi düpedüz reddediyor ve sıfır baytlık dosya bırakıyor:

```
[ipod @ ...] Could not find tag for codec hevc in stream #0, codec not currently supported in container
[out#0/ipod @ ...] Could not write header (incorrect codec parameters ?): Invalid argument
-rw-r--r-- 1 Administrator 197121 0 Sep 19 12:34 cikti/ipod-hevc.m4v
```

VidShrink kodeği hedefe göre kendisi seçiyor; `ipod` kabı açılsaydı kodek seçicinin
HEVC'ye düştüğü her koşum sessizce boş dosya verirdi. Kazanç marka etiketi, bedeli
bozuk teslim — yüzey açılmadı.

## 5. Mutasyon turu

Taban **0 kırmızı / 94 ölçü**. Filtre:
`KapUyumluluguTests|StreamMappingTests|KucultmeAraligiTests|MovKabiTests|IzAdiBayragiTests`.
Sürücü `.calisma/e8/mutasyon.py`, ham çıktı `.calisma/e8/mutasyon.out`.

| # | Kesim | Kırmızı |
| --- | --- | --- |
| M1 | süzgeç hiç yazılmıyor | 4 |
| M2 | kopyalanan ize de süzgeç | 1 |
| M3 | `first_pts=0` düşüyor | 2 |
| M4 | `async=1` düşüyor | 1 |
| M5 | iz numarası düşüyor (`-filter:a`) | 1 |
| M6 | kesit kopyası damgayı sıfırlamıyor | 2 |
| M7 | damga kesitli yeniden kodlamaya da giriyor | 1 |

Geri alındıktan sonra yine 0/94.

**Turun kendi tuzağı:** geri alma `shutil.copy2` kullanıyordu; eski zaman damgasını
koruduğu için MSBuild `VidShrink.Core`'u güncel sayıp bir önceki mutasyonun ikilisini
bırakıyordu. Kaynakta hiç bulunmayan bir bayrak testte görünüyordu. Betik artık geri
aldıktan sonra `os.utime` ile dosyaya dokunuyor.
