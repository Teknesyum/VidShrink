# WebP Bit Hızı Kipi

Tarih: 2026-10-07. Soru: Dönüştür sekmesinde kap WebP, kalite kipi "bit hızı" iken yazılan
değer çıktıyı değiştiriyor mu? Cevap: hayır. Test: `tests/VidShrink.Tests/DonusturEksikleriTests.cs`.

## Düzenek

Yerel ffmpeg `ffmpeg version 9.0-full_build-www.gyan.dev` (kurucunun ve CI'ın pinlediği ikili,
bkz. `hareketli-webp-avif.md`). Kaynak `testsrc2=size=320x180:rate=15:duration=2`, tek süreç,
`-threads 2`. Her koşumda yalnız aşağıdaki sütun değişti:

```
ffmpeg -hide_banner -y -f lavfi -i testsrc2=size=320x180:rate=15:duration=2
       -c:v libwebp_anim -loop 0 -an -threads 2 <ek> cikti.webp
```

## Ölçüm

| Ek | Bayt | sha256 (ilk 12) |
| --- | ---: | --- |
| (yok) | 120066 | D19EA7CBF014 |
| `-b:v 50k` | 120066 | D19EA7CBF014 |
| `-b:v 500k` | 120066 | D19EA7CBF014 |
| `-b:v 5000k` | 120066 | D19EA7CBF014 |
| `-quality 75` | 120066 | D19EA7CBF014 |
| `-quality 20` | 72306 | 7210E69C3B83 |
| `-quality 90` | 167156 | 9FCE1C89F8AA |

Üç ayrı `-b:v` değeri bayraksız koşumla bayt bayt aynı dosyayı verdi; o dosya `-quality 75`
ile de aynı. Yani kodlayıcı bit hızını okumuyor, varsayılan kalitesi 75. `-quality` ise boyutu
değiştiriyor (olumlu kontrol: ölçü kör değil).

`ffmpeg -h encoder=libwebp_anim` yalnız `lossless`, `preset`, `cr_threshold`, `cr_size` ve
`quality` seçeneklerini bildiriyor; bit hızı seçeneği yok.

## Uygulamanın Yaptığı

`ConversionArguments.AnimatedImageArgs` bit hızı kipinde WebP'ye hiçbir kalite argümanı
yazmıyordu; kullanıcı bit hızı yazıp varsayılan kaliteyi alıyor, bunu söyleyen bir satır da
yoktu. Artık `ConversionArguments.Notes` bu bileşimde `ConversionNote.WebpBitrateIgnored`
döndürür; Dönüştür'ün durum satırı "Hazır." yazısının altına notu ekler. Argüman değişmedi.

AVIF aynı kipte `-b:v` yazıyor (SVT-AV1 bit hızını okur), not almaz.

## Mutasyonlar

`DonusturEksikleriTests` + `HareketliWebpAvifTests`, 25 kol, taban 0 kırmızı. Her mutasyon
dosyanın özgün baytlarıyla geri kondu.

| Mutasyon | Kırmızı |
| --- | ---: |
| GIF'in ses muafiyetini kaldırmak (`CarriesAudio`) | 1 |
| Kabı kodlayıcıya bakmadan sunmak | 4 |
| Notu her kipte vermek | 2 |
| Gizlenen seçimi yerinde bırakmak | 1 |
| Notu durum satırına yazmamak | 1 |
| Kodlayıcısız kabı görünür bırakmak | 2 |

## Ölçülmeyenler

- Kodlayıcısı gerçekten eksik bir ffmpeg derlemesiyle bakılmadı; yoklama testte sahte.
- Yoklamanın okunamadığı durum (`EncoderCapabilities.Loaded` false, kaplar açık kalır) testle
  ölçülmedi.
- Gerçek pencerede gözle bakılmadı.
