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

## Neden (20 Ekim 2026)

Sahibi: later 60, dal `worktree-agent-later-60`. Aynı komut, aynı ffmpeg 9.0. `color-frame` artık ikinci bir bölüm
koşar: kaynak testsrc2 kayıpsız (`-qp 0`) kodlanır, her kol da kayıpsız kodlanır; kodlayıcı payı dışarıda kalır.
Sayılar 24. karenin ortak BT.709 RGB'deki PSNR'ı (dB).

| Kol | Ne ayırıyor | Düzeltmeden önce | Sonra |
|---|---|---|---|
| ayni-dosya | Ölçerin kendisi | 99.00 | 99.00 |
| bt709-444 | Matris+primer değişir, çıktı 4:4:4, yeniden alt örnekleme yok | 53.25 | 53.25 |
| bt709-bilinear | Aynı çeviri, çıktı 4:2:0, zscale varsayılanı | 31.69 | 31.69 |
| bt709-bicubic | Aynı, `filter=bicubic` | 34.17 | 34.17 |
| bt709-spline36 | Aynı, `filter=spline36` | 34.86 | 34.86 |
| bt709-lanczos | Aynı, `filter=lanczos` | 35.12 | 35.12 |
| bt709-cekirdek | Core'un `ConversionColorFilter` metni | 31.69 | 35.12 |
| gidis-donus-bilinear | Matris değişmez; 4:2:0 → 4:4:4 → 4:2:0 | 32.91 | 32.91 |
| gidis-donus-lanczos | Aynı gidiş-dönüş, lanczos | 40.90 | 40.90 |
| matris-zscale | Yalnız matris (primer/aktarım 601'de), bilinear | 32.21 | 32.21 |
| matris-zscale-lanczos | Yalnız matris, lanczos | 40.03 | 40.03 |
| matris-sws-varsayilan | Yalnız matris, swscale varsayılan bayraklar | 38.48 | 38.48 |
| matris-sws-dogru | Yalnız matris, `accurate_rnd+full_chroma_int+full_chroma_inp` | 33.46 | 33.46 |

Dönüştür kolları (CRF 12, ilk bölüm), düzeltmeden sonra: bt709 31.57 → 34.84, bt2020-tam 28.04 → 29.16. `koru`
48.10, `bt601-tam` 47.32 ve `yalniz-etiket` 18.83 değişmedi.

### Okuma

- **Ölçer yanılmıyor.** Aynı dosya 99 verir; aynı çeviri 4:4:4 çıktıda 53.25 verir. Karşılaştırma matrisi, primeri ve
  aralığı etiketten okuyor; okumasaydı 4:4:4 kolu da düşerdi. `yalniz-etiket` 18.83 de etiketin okunduğunu gösterir.
- **Aralık değil.** Yalnız aralığı değiştiren `bt601-tam` 47.32'de kalır; aralık çevirisi kromayı yeniden örneklemez.
- **Kodlayıcı değil.** Kayıpsız kodlamada da düşüş aynı: 4:2:0 çıktı 31.69, 4:4:4 çıktı 53.25.
- **Neden kroma yeniden örneklemesi.** Matris değişince zscale kroma düzlemini 4:4:4'e açar, çevirir, yeniden 4:2:0'a
  indirir. Matris değişmeden yalnız bu gidiş-dönüş bile 32.91'e indirir. testsrc2'nin keskin renk kenarları en kötü
  durumdur.
- **Kusur: zscale'in varsayılan süzgeci bilinear.** Core süzgeç seçmiyordu. `filter=lanczos` aynı çeviride 31.69'u
  35.12'ye çıkarır; spline36 34.86, bicubic 34.17. Core'un iki zscale metnine (`ConversionColorFilter`,
  `ColorMatrixFilter`) `filter=lanczos` eklendi.
- **swscale bayrakları ayrı bir yol.** Core renk çevirisinde swscale kullanmıyor. Yalnız matris kolunda swscale
  varsayılanı 38.48, `full_chroma_int` ile 33.46; zscale+lanczos 40.03 ile ikisinden de iyi.
- **Kalan düşüş doğal.** Primer de değişince lanczos'la 35.12 (yalnız matris 40.03). Primer çevirisi doğrusal ışıkta
  yapılır; yeni kroma artık eski kroma ve lumanın doğrusal bir karışımı değil, alt örnekleme bunu tam geri veremez.
  4:2:0 çıktıda bunun önü kapalı; 4:4:4 çıktıda 53.25.

## Açık

Yok. Ölçüm doğal içerikte yapılmadı; testsrc2 keskin kenarlarıyla en kötü durumu ölçer.
