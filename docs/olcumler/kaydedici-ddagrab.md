# Kaydedici Ddagrab Yakalama Yolu

Tarih 2026-09-30. Makine: RTX 5070 Ti, birincil ekran 2560x1440 180 Hz. ffmpeg 9.0 (Gyan full_build).

Düzenek: birincil ekranın sol üstünde 640x480 bölge, 60 fps istek, 5 sn, `libx264 -preset ultrafast -threads 2
-pix_fmt yuv420p`. Koşumlar sırayla, tek süreç; ekran içeriği durağan masaüstü (hareketli kaynak için ikinci süreç
açılmadı). ffmpeg CPU'su `-benchmark` utime+stime, kare sayısı ffprobe `nb_read_packets`.

ddagrab kaynağı uygulamanın ürettiği biçimde: `-f lavfi -i "ddagrab=output_idx=0:framerate=60:draw_mouse=1:offset_x=0:offset_y=0:video_size=640x480,hwdownload,format=bgra"`.

| Koşum | Yakalama | Gelen kare | Süre | fps | ffmpeg CPU |
|---|---|---|---|---|---|
| 1 | gdigrab | 118 | 4,88 s | 24,2 | 0,34 s |
| 1 | ddagrab | 299 | 5,00 s | 59,8 | 0,06 s |
| 2 | gdigrab | 224 | 5,00 s | 44,8 | 0,28 s |
| 2 | ddagrab | 299 | 5,00 s | 59,8 | 0,03 s |

Ek ölçüler:

- Uygulamanın açılış yoklaması (`BuildDdagrabProbe`, tek kare, `null` çıkış) 0,96 s sürdü, çıkış kodu 0.
- 3 sn'lik aynı ddagrab kaydı 180 kare, `avg_frame_rate=60/1`; canlı test (`KaydediciDdagrabTests`) 50 fps altını kırmızı sayar.
- Durağan ekranda `dup_frames` varsayılanı (açık) kareyi tekrarlıyor; `dup_frames=0` ile durağan ekranda gelen kare belirgin düşüyor (sayı tutulmadı).
- Bellek (maxrss): gdigrab ~42 MB, ddagrab ~108 MB.

## Bırakılan Yol: NVENC'e Doğrudan D3D11

`ddagrab` D3D11 yüzeyi veriyor; NVENC bu yüzeyi indirmeden alabiliyor. Üç biçim denendi:

- `-pix_fmt yuv420p` ile: kodlayıcı açılmıyor (hata kodu -40).
- `-pix_fmt`'siz: dosya yazılıyor ama `colorspace=gbr`, `color_range=pc` etiketli; oynatıcılar renkleri yanlış gösterir.
- `scale_d3d11=format=nv12` ile: süzgeç zinciri açılmadı.

Bu yüzden her kodlayıcıda `hwdownload,format=bgra` kullanılıyor: `-vf`, kamera bindirmesi, önizleme ve piksel biçimi
kolları gdigrab'daki bgra kareyi aynen görüyor.

## Okuma

60 istenince gdigrab bu bölgede 24 ile 45 arasında kare verdi, ddagrab iki koşumda da 59,8. ffmpeg CPU'su ddagrab'da
beşte bir ile onda bir arası; ekran kartındaki kopya bu sayıya girmiyor. Durağan içerikte ddagrab'ın karesi tekrar
karesi, hareketli içerikte önceki ölçüm (`kaydedici-gdigrab-kare-tavani.md`, 1280x720) 59,3 fps vermişti.

ddagrab tek DXGI çıkışı okur. İki monitöre yayılan bölge, ana ekran kartına bağlı olmayan monitör, uzak masaüstü ve
yoklaması geçmeyen makine gdigrab'a düşer; düşüş kaydedici bildirim satırında söylenir. Yoklama geçince otomatik kipin
30 kare tavanı kalkar.
