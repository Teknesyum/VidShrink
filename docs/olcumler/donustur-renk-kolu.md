# Dönüştür Renk Kolu Ölçümü

Tarih: 2026-10-18. Sahibi: later 60 (DON-26), dal `yan/donustur-kodlayici`.

## Ne Ölçüldü

Kaynak: `testsrc2=s=320x240:r=24:d=2`, libx264 CRF 12, `yuv420p`, BT.601 (`smpte170m`) sınırlı aralık.
Her kol Dönüştür planıyla (`EncodeRunner.ConvertAsync`, `-threads 2`) kodlandı; 24. kare iki biçimde okundu:

- **RGB:** her dosya kendi etiketiyle zscale'den ortak BT.709 RGB'ye (`gbrp`) çözülür. Fark küçükse renk korunmuştur.
- **Ham YUV:** düzlemler dönüşümsüz okunur. Fark büyükse düzlemler gerçekten yeni matrise ve aralığa çevrilmiştir.

`yalniz-etiket` kolu denetim kolu. Düzlemlere dokunmadan yalnız etiketi BT.709 yapar
(`-c copy -bsf:v h264_metadata`). Yalnız etiket değişseydi sonuç bu kol gibi çıkardı.

Komut: `dotnet run --project tools/VidShrink.Bench -c Release -- color-frame <klasör>`. ffmpeg 9.0 (gyan.dev).

## Sonuç

| Kol | Çıktı etiketi (matris/primer/aralık) | RGB ort. fark | RGB PSNR | YUV ort. fark | YUV PSNR |
|---|---|---|---|---|---|
| koru | smpte170m / – / tv | 0.26 | 48.10 | 0.19 | 51.16 |
| bt709 | bt709 / bt709 / tv | 2.33 | 31.57 | 17.91 | 21.45 |
| bt2020-tam | bt2020nc / bt2020 / pc | 3.56 | 28.04 | 35.99 | 15.87 |
| bt601-tam | smpte170m / smpte170m / pc | 0.44 | 47.32 | 8.61 | 28.13 |
| yalniz-etiket (denetim) | bt709 / bt709 / tv | 18.19 | 18.83 | 0.00 | 99.00 |

## Okuma

- Üç dönüşüm kolunda da ham YUV değişti (ort. fark 8.61 ile 35.99 arası). Etiket tek başına değişmedi, düzlemler çevrildi.
- RGB farkı dönüşüm kollarında 0.44 ile 3.56 arasında kaldı. Denetim kolu 18.19 verdi. Renk korunuyor.
- Yalnız aralık değişince (bt601-tam) RGB, `koru` koluna yakın kaldı: 47.32 dB.
- Matris ya da primer değişince RGB PSNR 28 ile 32 dB arasına indi. `koru` kolunda 48 dB.

## Açık

Matris değişen kollardaki 16 ile 20 dB'lik düşüşün nedeni ölçülmedi. Aday neden, keskin renk kenarlarında zscale'in
renk düzlemini 4:4:4'e açıp yeniden alt örneklemesi; testsrc2 bu tür kenarlarla dolu. Bu ölçümle kanıtlanmış değil.
