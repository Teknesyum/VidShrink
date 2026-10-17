# Kaydedici HDR Yakalama (EKA-P15)

Tarih: 2026-10-15, güncelleme 2026-10-17. ffmpeg 9.0 (gyan). Ölçü sınıfları:
`tests/VidShrink.Tests/KaydediciHdrTests.cs`, `tests/VidShrink.Tests/KaydediciOzSinamaTests.cs`.

## Karar

Hedef: HDR ekranda kayıt, ekranda görüneni versin. HDR çıkışta (DXGI `IDXGIOutput6::GetDesc1`, `ColorSpace` 12)
varsayılan yol SDR'ye çevirmektir; Windows'un SDR'yi HDR masaüstüne koyuşunun tersi uygulanır:

```
ddagrab=...:output_fmt=10bit,hwdownload,format=x2bgr10,
zscale=tin=smpte2084:pin=bt2020:rin=full:t=linear:p=bt709:npl={W},format=gbrpf32le,
tonemap=mobius:param=0.99:desat=0:peak={tepe/W},
zscale=tin=linear:pin=bt709:rin=full:t=iec61966-2-1:p=bt709:r=full,format=gbrp,format=bgra
```

ardından 8 bit yolla aynı `scale=out_color_matrix=bt709:out_range=tv,format=yuv420p,setparams=...bt709...tv`.

- `W`: Windows'un SDR beyazı, `DisplayConfigGetDeviceInfo(DISPLAYCONFIG_DEVICE_INFO_GET_SDR_WHITE_LEVEL)`,
  nit = değer / 1000 × 80. Okunamazsa 203 nit varsayılır, bildirim satırı (`recorder.capture.sdr-white-assumed`),
  `recorder-last.log` ve uygulama günlüğü söyler.
- `npl=W` dönüşümü W nitte beyazı 1,0'a koyar: 80/W ölçeği. `mobius` 0,99'a kadar doğrusal, üstünü ekranın tepe
  parlaklığına (`MaxLuminance`, yoksa 1000 nit) yumuşak dizle sıkıştırır.
- Genel ton eşleme (`hable`) bırakıldı: SDR pikseli de değiştiriyordu.
- "HDR koru" ayrı seçim: HDR10 zinciri + `limiter` (Y 64–940, renk 64–960) + kodlayıcıya statik veri
  (x265 `master-display`/`max-cll`, x264 `mastering-display`/`cll`, SVT-AV1 kesirli `mastering-display`/`content-light`).
  H.264 seçiliyse çalışan ilk HEVC kodlayıcısına geçilir, yoksa SDR'ye çevrilir.
- Varsayılan açılış göçü: `hdrToSdrDefaultOn` işareti taşımayan eski dosyanın `false`'u bir kez `true` olur.

## Ölçülenler

| ölçü | sonuç |
|---|---|
| `rgbaf16` (scRGB) yolu: swscale `rgbaf16le` → `gbrpf32le` | 2,537 girdi 1,0'a kırpılıyor; zscale de araya `gbrap16le` koyup kırpıyor. Yol bırakıldı |
| libplacebo `rgbaf16` | 2,537 kırpılmadan geçiyor, ama Vulkan istiyor; ürüne alınmadı |
| PQ 10 bit yolu, 18³ sRGB ızgarası (W 80/203/480) | en büyük fark 15–16 kod, ±1 içinde %83–87; büyük farklar parlak doygun kanalın yanındaki karanlık kanalda, 10 bit PQ nicemlemesi |
| Renk kartı, sentetik PQ (W 80/203/480) | griler ve beyaz ±1 kod; renkli yamalarda en büyük Delta E 1,21 (sarı, W 480), en büyük kod farkı 4 (cyan R) |
| Tepe: W 200 nit, 1000 nit yama | 255; 200 nit 254–255; 100 nit 185–189 |
| `limiter` | Y 1010 / U 1000 / V 30 → 940 / 960 / 64; limiter'sız aynı kare 1010/1000/30 |
| Beyaz `x2bgr10` eski zincirde | Y 935: zincir tek başına 940'ı aşmıyor; Serkan'ın dosyasındaki 1023 büyük olasılıkla kodlayıcı taşması (ölçülmedi) |
| Statik veri, ffprobe kare yan verisi | x265, x264, SVT-AV1: `Mastering display metadata`, `max_content=1015`, `max_average=600` |
| `setparams`'sız zincir | ffprobe renk alanları `unknown`: `-color_*` tek başına akışa yazılmıyor |
| SDR 8 bit yol, `0x00FF00` | BT.709 tv'de Y ≈ 173 (170–176), etiketler bt709/tv |

## Öz Sınama

`VidShrink.exe --recorder-selftest`: 4×3 renk kartı (beyaz, %75 ve %50 gri, siyah, saf renkler, ten, gök) bir saniye
ekranda kalır; aynı bölge kaydın HDR→SDR yolundan ve 8 bit `bgra` yoldan birer kare yakalanır, her yamanın iç yarısı
çizilen değerle karşılaştırılır. Eşik: gride kanal başına ±1 kod, renkte Delta E ≤ 2. Sonuç `recorder-last.log`'a,
çıkış kodu 0 geçti / 1 eşik aşıldı / 2 koşamadı. SDR ekranda yalnız 8 bit yol koşar.

## Ölçülmedi

- Gerçek HDR ekranda kayıt ve öz sınama: bu makinede HDR ekran yok. Kart ölçüsü sentetik PQ karesiyle; gerçek
  masaüstünde Windows'un kendi dönüşümünün bu modele uyduğu Serkan'ın öz sınamasıyla görülecek.
- `SdrWhiteLevel.Read`'in gerçek ekranda okuduğu değer: SDR ekranda yol hiç sorulmuyor; yalnız 1000 = 80 nit ölçeği pimli.
- 60 fps'de zscale + tonemap zincirinin işlemci payı.
- Donanım kodlayıcıları (nvenc/amf/qsv) statik veri yazmıyor; ffmpeg'de anahtarı yok.
- Auto HDR ya da HDR oyunun W'nin üstündeki içeriği sıkıştırılır; ekrandaki parlak vurgu kayıtta düzleşir.
- `VidShrink.Cli`'da öz sınama yok: kart penceresi ve DXGI okuması uygulamada; komut uygulamanın kendisi.
- HDR kipinde webcam bindirmesinin rengi.

## Mutasyonlar

| mutasyon | sonuç |
|---|---|
| `HdrFrameChain`'den `setparams` kaldırmak | kırmızı (`ZincirDosyayaHdr10EtiketiYazar`, `HdrCikistaKayitOnBitHdr10Kalir`) |
| `HdrCapture`'dan `!HdrToSdr` kaldırmak | kırmızı (`SdryeCevirmeSecenegiHdrCikistaBugunkuYolu`) |

Renk turunun 20 mutasyonu; her biri sınıf süzgeciyle koştu, hepsi kırmızı:

| # | mutasyon | kıran test |
|---|---|---|
| M1 | göç işareti | `EskiDosyaninKapaliDegeriBirKezAcilir`, `GoctenSonraKullanicininKapatmasiKorunur` |
| M2 | HEVC profili | `HevceGecisProfiliSilerVeHdr10Kalir` |
| M3 | bildirim sırası | `SdrBeyaziOkunamazsaVarsayilanSoylenir` |
| M4 | SDR `setparams` | `SdrKayitBt709MatrisiyleCevrilirVeEtiketlenir` ve 5 test daha |
| M5 | `npl` | `BeyazVeTepeHedefCikistanGelir`, `SdrIcerikHdrMasaustundenAyniDegereDoner`, `TepeParlaklikBeyazaIner` |
| M6 | tepe tabanı 1,05 W | `BeyazVeTepeHedefCikistanGelir` |
| M7 | `limiter` | `HdrCikistaKayitOnBitHdr10Kalir`, `HdrZinciriSuperBeyaziTvAraligindaKirpar` |
| M8 | x264 `cll` | `StatikVeriDosyaninKaresindeGorunur` |
| M9 | `SdrWhiteAssumed` | 5 test |
| M10 | beyaz alt sınırı 80 | `VarsayilanBeyazYalnizCevrilenHdrKayittaSayilir` |
| M11 | gdigrab HDR bildirimi | `GdigrabaDusenHdrEkranSoylenir` |
| M12 | gif yolu | `GifYakalamasiTonEslemeVeSdrEtiketiAlmaz` |
| M13 | tanı kaynağı | `TaniMetniSdrBeyaziniVeKaynaginiSoyler` |
| M14 | kenar örneklemesi | `KenarKaymasiOrneklemeyeKarismaz` |
| M15 | gri toleransı | `GriYamaKanalBasinaBirKodTolerir` |
| M16 | renk eşiği | `SdrIcerikHdrMasaustundenAyniDegereDoner` |
| M17 | SDR ekranda iki yol | `SdrEkrandaYalnizSekizBitYolKosar`, `FfmpegDuserseVeKartCikistaDegilseHataKodu` |
| M18 | nit ölçeği (/1000×80) | `WindowsBeyazDegeriBinde80NitOlcegindedir` |
| M19 | günlük satırı | `VarsayilanBeyazGunlugeDeYazilir` |
| M20 | mobius → hable | `SdrIcerikHdrMasaustundenAyniDegereDoner`, `SdryeCevirmeSecenegiHdrCikistaBugunkuYolu`, `TepeParlaklikBeyazaIner` |
