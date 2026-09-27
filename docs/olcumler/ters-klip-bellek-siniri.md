# Ters Klip Bellek Sınırı

Düzenleyici D4 teslimi `reverse` filtresini kullanır; filtre klibin bütün karelerini bellekte
tutar. Sınır `EditExport.ReverseBytesPerSecond` ile hesaplanır:

`genişlik × yükseklik × piksel başına bayt (8 bit 4:2:0 için 1,5; 8 biti aşan için 3) × fps × 1,08`

Sınır süresi = bellek bütçesi / bu hız. Uygulama bütçe olarak toplam belleğin yarısını verir
(`GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 2`); klibin kaynak süresi sınırı aşarsa
teslimden önce uyarı çıkar, ikinci basışta teslim sürer.

## Kaynaklar

| Ölçüm | Kuram (w×h×1,5×fps) | Ölçülen | Oran |
| --- | --- | --- | --- |
| 1080p30, 5/10/20/40 sn (`video-duzenleme-olcum-kutugu-2026-09-15.txt`, satır 9) | 93,3 MB/sn | ~99 MB/sn | 1,065 |
| 640x360p30, 4/8 sn, `-threads 1`, `-benchmark maxrss` (2026-09-28) | 10,37 MB/sn | 11,17 MB/sn | 1,077 |

1,08 katsayısı bu iki oranın büyüğünün yukarı yuvarlanmışıdır. 1080p30'da formül 100,8 MB/sn
verir; kütükteki 99 MB/sn'ye yüzde 2 yakın.

## 2026-09-28 Kısa Ölçüm

ffmpeg 9.0-full_build (gyan.dev), Windows 11 Pro 22631. Tek iş parçacığı, kısa, küçük örnek.

```
640x360p30 vf=reverse 4 sn: bench: maxrss=64244KiB
640x360p30 vf=reverse 8 sn: bench: maxrss=107856KiB
640x360p30 vf=null 4 sn: bench: maxrss=21664KiB
640x360p30 vf=null 8 sn: bench: maxrss=22032KiB
```

Eğim (107856 − 64244) KiB / 4 sn = 10903 KiB/sn = 11,17 MB/sn. `null` satırları taban
belleğin süreyle büyümediğini gösterir; büyüyen kısım yalnız `reverse`in tamponu.

Komut: `ffmpeg -hide_banner -nostdin -benchmark -f lavfi -i testsrc2=size=640x360:rate=30:duration=<s> -vf reverse -threads 1 -f null -`
