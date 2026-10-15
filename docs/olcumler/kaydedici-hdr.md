# Kaydedici HDR Yakalama (EKA-P15)

Tarih: 2026-10-15. ffmpeg 9.0 (gyan). Ölçü sınıfı: `tests/VidShrink.Tests/KaydediciHdrTests.cs`.

## Karar

HDR çıkışta (DXGI `IDXGIOutput6::GetDesc1`, `ColorSpace` 12 = G2084_P2020) ddagrab kaynağı 10 bit ister ve
kare HDR10 olarak yazılır:

```
ddagrab=...:output_fmt=10bit,hwdownload,format=x2bgr10,
scale=out_color_matrix=bt2020:out_range=tv,format=yuv444p10le,
setparams=color_primaries=bt2020:color_trc=smpte2084:colorspace=bt2020nc:range=tv
```

Kodlayıcıya 10 bit biçim (yazılımda `yuv420p10le`/`yuv444p10le`, nvenc/amf/qsv'de `p010le`), 10 bit profil
(x264 `high10`/`high444`, x265 `main10`, vp9 `2`) ve `-color_primaries bt2020 -color_trc smpte2084
-colorspace bt2020nc -color_range tv` gider. "SDR'ye çevir" seçeneği (varsayılan kapalı) ve HDR olmayan çıkış
bugünkü `format=bgra` yolunda kalır. 10 bit taşıyamayan kodlayıcı ya da GIF kabı seçiliyse kayıt SDR sürer ve
`recorder.capture.hdr-lost` satırı söyler.

## Ölçülenler

| ölçü | sonuç |
|---|---|
| Bu makinede `ddagrab=output_fmt=10bit` | açılmıyor, ffmpeg -129 döner (SDR ekran) |
| `setparams`'sız zincir (testsrc2 → aynı süzgeç, libx264) | ffprobe renk alanları `unknown`: `-color_*` çıkış seçenekleri tek başına akışa yazılmıyor |
| `setparams`'lı zincir | ffprobe: profil High 10, `yuv420p10le`, `tv`, `bt2020nc`, `smpte2084`, `bt2020` |

Canlı kol `ZincirDosyayaHdr10EtiketiYazar` ddagrab önekini `testsrc2=s=640x480:r=30:d=1` ile değiştirip zincirin
geri kalanını ürünün argümanıyla koşar (`-threads 2`, 1 sn), etiketleri ffprobe'la okur.

## Ölçülmedi

- Gerçek HDR ekranda kayıt: bu makinede HDR ekran yok. Renk doğruluğu, ton ve parlaklık ölçülmedi.
- Donanım kodlayıcılarında (nvenc/amf/qsv) `p010le` ile gerçek HDR kodlaması ölçülmedi; yalnız argüman pimli.
- HDR kipinde webcam bindirmesinin rengi: bindirme SDR kaynaktan BT.2020 tuvaline dönüşümsüz biniyor, yaklaşık.

## Notlar

- Varsayılan libx264 yolu H.264 High 10 verir; birçok oynatıcı ve donanım çözücü bunu oynatmaz. HDR kayıt için
  x265 ya da donanım HEVC daha uyumlu.
- Zincir `yuv444p10le` ara biçiminden geçer, sonra kodlayıcının biçimine iner.

## Mutasyonlar

| mutasyon | kırmızı |
|---|---|
| `HdrFrameChain`'den `setparams` kaldırmak | 2/16 (`ZincirDosyayaHdr10EtiketiYazar`, `HdrCikistaKayitOnBitHdr10Kalir`) |
| `HdrCapture`'dan `!HdrToSdr` kaldırmak | 1/16 (`SdryeCevirmeSecenegiHdrCikistaBugunkuYolu`) |
