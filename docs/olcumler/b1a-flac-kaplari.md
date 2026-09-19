# flac Hangi Kapta Duruyor

19 Eylül 2026. ffmpeg 9.0-full_build (gyan.dev). Kaynak: `anoisesrc` pembe gürültü 1 sn, 48 kHz.
Betik ve çıktılar `.calisma/b1a-flac/`.

B1a planı flac'ı **küçültme kolunun dışında** bıraktı: kayıpsız ses hedef boyutu anlamsız kılar,
yeri dönüştürücü. Dönüştürücüye girmeden önce hangi kapların flac'ı gerçekten taşıdığı ölçüldü.
Çıkış kodu tek başına ölçü sayılmadı; kabul edilen her dosya ffprobe ile geri okundu.

## Kodlama (`-c:a flac`)

| kap | çıkış | ffprobe | hüküm |
|---|---|---|---|
| mp4 | kabul | flac, 134 KB | **girer** |
| mkv | kabul | flac, 134 KB | **girer** |
| flac | kabul | flac, 141 KB | **girer**, yeni ses kabı |
| wav | kabul | flac, 133 KB | girmez — WAVE içinde flac etiketi standart dışı, oynatıcıların çoğu açmaz |
| avi | kabul | flac, 140 KB | girmez — kopyalama kolunda aynı ses 1,29 MB'a şişti (10 kat), kap taşımıyor |
| m4a | RED | — | `Could not find tag for codec flac in stream #0` |
| mov | RED | — | `flac only supported in MP4.` |
| webm | RED | — | yalnız Vorbis/Opus |
| mp3 | RED | — | yalnız tek mp3 akışı |

## Kopyalama (`-c:a copy`, kaynak .flac)

Kabul/red kümesi kodlamayla birebir aynı. Tek fark avi: 133 KB'lık kaynak 1,29 MB olarak
yeniden sarıldı. Bu, "çıkış kodu 0" ile "kap bu kodeği taşıyor"un aynı şey olmadığının ölçüsü.

## Ürüne giren hüküm

- flac dönüştürücüde üç kapta seçilebilir: **mp4, mkv, flac**.
- **flac yeni bir ses kabı**: mp3/m4a/wav gibi yalnız ses çıkarır, kodeği kaptan gelir.
- wav ve avi ffmpeg'in reddetmediği ama taşımadığı kaplar; listeye alınmadı. Bu iki satır
  uydurma değil, yukarıdaki ölçümün sonucu.
- Küçültme kolu değişmedi: flac orada hâlâ yok.
