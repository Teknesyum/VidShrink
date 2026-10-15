# Bas Konuş Komut Gecikmesi

Bas konuş, kayıt sürecine `volume@ptt` süzgecini koyar (başlangıç `volume=0`) ve tuş
basılınca ffmpeg'in standart girdisine `cvolume@ptt -1 volume 1`, bırakılınca
`volume 0` yazar. Bu ölçüm komutun kayda ne kadar geç girdiğini gösterir.

Düzenek: `tools/bas-konus-olcumu/olc.ps1`. Gerçek mikrofon yok; 440 Hz `sine` kaynağı
`-re` ile gerçek zamanlı akar, `-threads 2`, 6 sn. Komut zamanı, komuttan hemen önceki
`-progress` satırının `out_time_us` değeridir (0,1 sn aralıkla gelir). Kayıttaki açılma ve
kapanma anı `silencedetect=n=-60dB:d=0.01` ile okunur.

Makine: bu geliştirme makinesi, `ffmpeg version 9.0-full_build-www.gyan.dev`.

| Olay | Komut anı (out_time) | Kayıtta görülen an | Fark |
| --- | ---: | ---: | ---: |
| Aç (`volume 1`) | 2,475 sn | 2,688 sn (`silence_end`) | 213 ms |
| Kapat (`volume 0`) | 4,565 sn | 4,800 sn (`silence_start`) | 235 ms |

Ham çıktı:

```
open at out_time_us=2474667
close at out_time_us=4565333
silence_start: 0
silence_end: 2.688 | silence_duration: 2.688
silence_start: 4.8
silence_end: 6 | silence_duration: 1.2
```

Fark, `-progress` aralığı yüzünden en çok 100 ms kadar fazla okunabilir. Tek koşum;
tekrar ve dağılım ölçülmedi. Gerçek mikrofon (dshow) yolunda, kısayol yoklamasının
(`GetAsyncKeyState`, 30 ms zamanlayıcı) eklediği süre ve macOS/X11 bırakma olayı ölçülmedi.
