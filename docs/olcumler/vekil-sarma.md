# Vekil Kopyada Sarma Süresi

Later 140. Düzenleyicinin önizlemesi ağır klipte asıl dosyadan değil vekilden oynayınca kesin
sarmanın (exact seek) ne kadar kısaldığı. Dışa aktarım vekili hiç görmez; bu ölçü yalnız önizlemeyi
kapsar.

## Düzenek

Komut: `dotnet tools/VidShrink.Bench/bin/Release/net8.0/VidShrink.Bench.dll proxy-seek --runs 3`
(`tools/VidShrink.Bench/VekilSarma.cs`). 2026-10-21, AMD Ryzen 7 9700X, ffmpeg 9.0 (gyan.dev),
`VIDSHRINK_BENCH_CEKIRDEK=4` (süreç 4 çekirdeğe bağlı, öncelik BelowNormal).

- Kaynak: `testsrc2` 3840x2160, 30 fps, 8 sn, HEVC 10 bit (`libx265 ultrafast`, `keyint=60`),
  13,8 MB; üretimi `-threads 2` ile 11,5 sn.
- Vekil: uygulamanın kendi yolu `ProxyRunner.CreateAsync`: 960x540, H.264 8 bit, GOP 12, B karesiz,
  `-threads 2`; 0,7 MB, üretimi 2,1 sn.
- Oynatıcı: `MpvEngine`, ses kapalı, `vd-lavc-threads=2`, `hwdec=no`, yazılım çizimi doğal boyutta.
  Her dosya önizlemenin açtığı gibi tek klipli `edl://` adresiyle açılır.
- Tur başına 12 sabit konuma `SeekPrecision.Exact` sarma; süre komuttan, sarmanın RESTART'ı ve
  komuttan sonraki ilk yeni karenin çizilmesine kadar. Üç tur, asıl ve vekil sırası turdan tura
  değişir; her motor ilk ısınma sarmasını sayıya katmaz.

## Sonuç

| dosya | n | düşen | ortanca | p90 | ortalama | en küçük | en büyük |
|---|---|---|---|---|---|---|---|
| asıl 4K HEVC 10 bit | 36 | 0 | 245,4 ms | 443,0 ms | 247,9 ms | 66,3 ms | 455,3 ms |
| vekil 540p H.264 | 36 | 0 | 1,6 ms | 2,4 ms | 1,7 ms | 0,9 ms | 3,2 ms |

Asıl dosyada süre konuma göre değişiyor: anahtar kareden uzaklık arttıkça (en çok 59 kare) çözülecek
kare sayısı artıyor. Vekilde en çok 11 kare çözülüyor ve her biri 16 kat küçük.

## Sınırlar

- Asıl dosyanın süresine 4K karenin yazılım çizimi de giriyor; önizleme gerçekte de böyle çizdiği
  için ölçüye dahil.
- Donanım çözme (`auto-copy`) açıkken asıl dosyanın süresi farklı olur; bu ölçülmedi.
- Sentetik kaynak gerçek çekimden daha kolay sıkışıyor; oran kayabilir, sıra değişmez.
