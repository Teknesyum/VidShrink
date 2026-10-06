# Kare Hızı Kipi: Sabit Ve Tavanlı — 7 Ekim 2026

`--sabit-kare` (HandBrake `--cfr`) ve `--tavan-kare` (`--pfr`) için ffmpeg'in hangi bayrağı
gerçekten ne yaptığı ölçüldü. ffmpeg 9.0 (gyan full), tek süreç, `-threads 2`, libx264 ultrafast.

## Kaynak

`testsrc=size=320x240:rate=50:duration=2,select='lt(t,1)+not(mod(n,5))'`, `-fps_mode vfr`.
İlk saniye 50 kare, ikinci saniye 10 kare: toplam 60 kare, kare aralığı 0,02 ile 0,10 sn arası.
MP4'te `avg_frame_rate` 31,25, `r_frame_rate` 50.

## Ölçüm

Kare anları `ffprobe -show_entries packet=pts_time` ile okundu.

| ffmpeg argümanı | Kare | En kısa aralık | En uzun aralık | Sonuç |
|---|---|---|---|---|
| bayrak yok | 60 | 0,020 | 0,100 | değişken hız korunuyor |
| `-fps_mode cfr` | 96 | 0,020 | 0,020 | sabit, ama 50 kareye çoğaltıyor |
| `-r 30 -fps_mode cfr` | 59 | 0,0333 | 0,0333 | sabit, istenen hızda |
| `-r 18 -fps_mode cfr` | 36 | 0,0556 | 0,0556 | sabit, istenen hızda |
| `-fps_mode zzz` | — | — | — | reddedildi (`Invalid value zzz`) |
| `-fpsmax 25 -fps_mode vfr` | — | — | — | reddedildi (ffmpeg "çelişkili" diyor) |
| `-r 25 -fps_mode vfr` | — | — | — | reddedildi, aynı ileti |
| `-fpsmax 25` | 50 | 0,040 | 0,040 | sabit hıza dönüyor, tavan değil |
| `-vf fps=25` (`-fps_mode vfr` ile de) | 48 | 0,040 | 0,040 | sabit hıza dönüyor, tavan değil |
| `select` ile en az 0,04 sn aralık, `-fps_mode vfr` | 29 | 0,040 | 0,100 | tavan tutuyor, ama aşağıya bak |
| `-enc_time_base 1/25 -fps_mode vfr` | 36 | 0,040 | 0,120 | tavan tutuyor: ilk saniye 25, ikinci 10 kare |
| `-enc_time_base 1/23.976 -fps_mode vfr` | 35 | 0,0417 | — | kesirli tavan kabul |
| `-enc_time_base 1/60 -fps_mode vfr` | 60 | 0,020 | 0,100 | tavan kaynaktan yüksekse dokunmuyor |
| `-enc_time_base zzz` | — | — | — | reddedildi (`Invalid time base`) |

## Karar

**Sabit kip:** `-r <planın hızı> -fps_mode cfr`. Yalnız `-fps_mode cfr` değişken kaynakta en yüksek
anlık hıza (50) çoğaltıyor ve dosyayı büyütüyor; `-r` hızı planın kararına bağlıyor.

**Tavanlı kip:** `-enc_time_base 1/<tavan> -fps_mode vfr`. Görevde önerilen iki yol tutmadı:
`-fpsmax` değişken kiple birlikte reddediliyor, tek başına ve `fps` süzgeci çıktıyı sabit hıza
çeviriyor. `select` süzgeci tavanı tutuyor ama zaman damgası 1 ms olan sabit 30 karelik MKV'de
tavan 30 iken 60 kareden 20'sini atıyor (33 ms'lik aralık 0,0333'ün altında kalıyor);
`-enc_time_base 1/30` aynı kaynakta 60 kareyi de bırakıyor. Tavanlı kipte `fps=` süzgeci yazılmaz.

Motor bütçe için tavanın da altına inerse ızgara planın hızı olur: yine değişken, daha seyrek.

## Bilinen Sınırlar

- Zaman damgaları 1/tavan ızgarasına yuvarlanır; seyrek kısımda kaynağın 0,10'luk
  aralığı 0,12'ye kadar okunur.
- İlk geçişte (çıktı `null`) aynı bayraklar aynı kareleri atıyor: tavanlı kipte 36 kare, 24 atılan.
- Donanım kodlayıcılarıyla (NVENC, Quick Sync, AMF, Media Foundation) ölçülmedi.
- `--sabit-kare --kare-hizi N`, N kaynaktan yüksekse hızı yükseltmez; motor kaynağın üstüne çıkmaz.

## Mutasyonlar

`KareHiziKipiTests` (21 kol), her mutasyondan sonra yeniden derlenip koşuldu, elle geri yazıldı.

| Mutasyon | Kırmızı |
|---|---|
| `FfmpegArguments.Build` kip argümanlarını yazmıyor | 4 |
| İki bayrak birlikte denetimi kalkıyor | 4 |
| Tavan `-enc_time_base` yerine `-fpsmax`'a yazılıyor | 3 |
| `fps=` süzgecinin kip koşulu bozuluyor | 3 |
| Planlayıcı tavanı okumuyor | 2 |
| Kopya yolunu yalnız tavanlı kip kapatıyor | 1 |
