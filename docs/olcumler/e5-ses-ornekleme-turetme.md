# Düşük Bit Hızında `-ar` Düşürmek — Ölçüm

fable 024 kararında "düşük kbit'te (≤48) 48 kHz'i 24-32'ye indirmek deterministik bir
kural" demişti. Kural uygulanmadan önce ölçüldü; bu ffmpeg 9.0 yapısının aac kodlayıcısında
tutmuyor.

## Düzenek

Geniş bantlı deneme sinyali (1 kHz sinüs + pembe gürültü, stereo, 48 kHz, 8 s) aac ile
üç bit hızında, iki örnekleme hızında kodlandı. Ölçülen: yüksek geçiren süzgeçten sonra
`volumedetect`'in `mean_volume` değeri (dB) — bant genişliğinin doğrudan okuması.

| bit hızı | örnekleme | >8 kHz | >12 kHz | >15 kHz | boyut |
| --- | --- | --- | --- | --- | --- |
| 32k | 48 kHz | -53.1 | -62.6 | -69.6 | 34679 |
| 32k | 32 kHz | -55.9 | -71.2 | **-91.0** | 33870 |
| 48k | 48 kHz | -42.4 | -50.9 | -57.8 | 50464 |
| 48k | 32 kHz | -43.8 | -58.0 | **-82.5** | 49853 |
| 64k | 48 kHz | -36.6 | -42.1 | -47.9 | 66588 |
| 64k | 32 kHz | -37.0 | -45.2 | **-68.8** | 66041 |

## Hüküm

Örneklemeyi düşürmek 15 kHz üstünü siliyor (-69.6 → -91.0 dB) ve karşılığında hiçbir bandı
yükseltmiyor: 8 kHz altı bile 2.8 dB **düşüyor**. Boyut kazancı yok, çünkü bit hızı zaten
sabit. 32 kbit'te bile kodlayıcı 15 kHz içeriği taşıyabiliyor — "düşük kbit'te bant zaten
gitmiş, örneklemeyi indir" varsayımı bu kodlayıcıda doğru değil.

## Gerçek kaynakla tekrar

`.calisma/dalga2/parca-2ses-1altyazi.mkv` (mono aac 48 kHz), aynı düzenek:

| bit hızı | örnekleme | >8 kHz | >12 kHz | boyut |
| --- | --- | --- | --- | --- |
| 32k | 48 kHz | -72.9 | -82.5 | 26201 |
| 32k | 32 kHz | -75.5 | **-90.3** | 25839 |
| 48k | 48 kHz | -73.1 | -82.5 | 38416 |
| 48k | 32 kHz | -75.5 | **-90.3** | 38057 |

Yön aynı: iki bit hızında da örnekleme düşünce 12 kHz bandı 7.8 dB kayboluyor, boyut %1
değişiyor. 32k ile 48k'nın bant ölçüleri birbirinin aynı — kaynağın kendisi dar bantlı, yine
de örnekleme düşürmek bir şey kazandırmıyor.

Karar: `-ar` türetilmiyor, kullanıcıya da açılmıyor. E5'in bu parçası ölçümle kapandı.
