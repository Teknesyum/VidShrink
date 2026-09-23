# HB28: SVT-AV1'e HDR10 Statik Verisi

Tarih 2026-09-23. Dal `worktree-agent-a8772fe995cc2357c`. Araç: yerel ffmpeg 9.0 (gyan.dev full),
SVT-AV1 Encoder Lib v4.2.0-68-gc1e79b04f. Kaynaklar lavfi `testsrc2` 160x96, 10 fps, 0,5-1 sn;
`lp=2`, `-threads 2`, preset 12. İnternetten dosya indirilmedi.

Kapattığı satır: `docs/handbrake/durum-2026-09-23.md` §3 satır 28 ("HDR10 statik metadata
svtav1-params'ta elle yazılmıyor").

## Anahtar Biçimi

`ffmpeg -h encoder=libsvtav1` yalnız `-svtav1-params <dictionary>` bildiriyor, anahtar listesi yok.
Biçim bu yüzden kodlama çıktısında ölçüldü: yan verisiz 10 bit kaynak, dört kol, çıktının ilk
karesi `ffprobe -show_frames -show_entries frame_side_data`.

| Kol | `-svtav1-params` eki | Paket | Çıktıdaki yan veri |
|---|---|---:|---|
| taban | — | 1493 B | yok |
| kesirli | `mastering-display=G(0.2650,0.6900)B(0.1500,0.0600)R(0.6800,0.3200)WP(0.3127,0.3290)L(1234.5,0.0123):content-light=987,321` | 1529 B | green_x 17367/65536, max_luminance 316032/256 (= 1234,5), min_luminance 202/16384 (= 0,0123), max_content 987, max_average 321 |
| x265 tamsayı | `mastering-display=G(13250,34500)...L(12345000,123):content-light=987,321` | 1529 B | SVT uyarısı "Invalid mastering display info will be clipped to 0.0 to 1.0"; bütün renk alanları 65535/65536, max_luminance -1134647296/256 (taşma) |
| uydurma | `mastering-displayz=...:content-lightz=...` | 1493 B | yok — hata da uyarı da yok, çıktı tabanla aynı |

Sonuç: `mastering-display=G(x,y)B(x,y)R(x,y)WP(x,y)L(max,min)` **kesirli** (renk 0-1, parlaklık
cd/m²), `content-light=MaxCLL,MaxFALL` tamsayı. x265'in 1/50000 ve 1/10000 birimli biçimi aynen
verilemez; `HdrResolver.SvtAv1MasteringDisplay` böler (renk /50000, parlaklık /10000).

Uydurma anahtar sessizce yutuluyor: "anahtar kabul edildi" ölçüsü çıkış kodu ya da stderr ile
yapılamaz, yalnız çıktıdaki değerle.

## ffmpeg 9'un Kendi Aktarımı

Kaynağında akış düzeyinde HDR10 yan verisi olan (x265 → ffv1 ikinci kuşak) dosya anahtarsız
libsvtav1'e verildiğinde değerler çıktıya geçiyor (max_luminance 1000, max_content 1000). Açık
anahtar verilince bit akışında **anahtar kazanıyor** (1234,5 / 987), mkv akış düzeyindeki yan veri
ise kaynaktan kalıyor (1000). Kodun yazdığı değer kaynaktan türediği için iki katman aynı kalır;
açık anahtar aktarımın olmadığı eski ffmpeg sürümlerinde de bit akışını kilitler.

x265'in doğrudan yazdığı mkv'de veri yalnız SEI'de, akış düzeyinde değil; `FfprobeClient`
akış `side_data_list`'ini okuduğu için böyle bir kaynakta yoklama `null` döner (canlı kolun ilk
koşumu bu yüzden kırmızıydı; kaynak ffv1 ikinci kuşağıyla üretiliyor).

## Kod

- `HdrResolver.Resolve`: korunan HDR + `libsvtav1` → `-svtav1-params mastering-display=...:content-light=...`;
  veri yoksa anahtar yazılmaz, yalnız biri varsa yalnız o. Ton eşlemede ve x265'te yok.
- Birleşme `FfmpegArguments.MergeEncoderParams` üzerinden: psy/AQ (`tune=`), kilitli tune, `keyint`,
  kullanıcının `ExtraArgs`'ı ile tek `-svtav1-params` dizgesine iner. HDR10+ kaynak SVT-AV1'de
  statik katmanı yine yazar, `dhdr10-info` yazılmaz.

## Testler

`tests/VidShrink.Tests/SvtAv1Hdr10StatikTests.cs`, 13 ölçü (Release, `-warnaserror`): 13/13 yeşil.
Komşu sınıflarla (`HdrArgumentsTests`, `HdrDinamikTests`, `Hdr10ArtiKopruTests`, `TuneYuzeyiTests`,
`EncoderStateConsumptionTests`, `PlanCalculatorProbeTests`, `CodecModelTests`) 172/172.

Canlı kol 1 (yan verisiz kaynak, yoklamanın bulmuş gibi davrandığı bilgi):

| Çıktı | max_luminance | min_luminance | green_x | max_content / max_average |
|---|---:|---:|---:|---|
| anahtarlı | 1234,5 | 0,01233 | 0,26500 | 987 / 321 |
| uydurma anahtar | — | — | — | — |
| anahtarsız (gerçek yoklama) | — | — | — | — |

Canlı kol 2 (gerçek HDR10 kaynağı, yoklama → plan → argüman): kaynak G(0,17;0,797) B(0,131;0,046)
R(0,708;0,292) WP(0,3127;0,329) L(4000;0,0001), 1234/567 → çıktı her renk alanı < 1e-4 farkla
aynı (0,169998 / 0,796997 / 0,130997 / 0,046005 / 0,707993 / 0,292007 / 0,312698 / 0,328995),
L 4000 / 0,000122, 1234/567.

## Mutasyonlar

Her mutasyon commit `241fbba3`'ün üstüne uygulandı, derlendi, süzgeçli koşuldu, geri alındı.

| # | Mutasyon | Kırmızı |
|---|---|---:|
| M1 | SVT-AV1 kolu anahtarı yazmıyor | 5/13 |
| M2 | Renk ölçeği 1 (x265 tamsayıları aynen) | 7/13 |
| M3 | `-svtav1-params` birleşme listesinden çıktı | 3/13 |
| M4 | `content-light` düşürüldü | 4/13 |

M1'de canlı kol 2 yeşil kalıyor: ffmpeg 9'un kendi aktarımı değeri yine taşıyor. Anahtarın
varlığını canlı kol 1 (yan verisiz kaynak) ölçüyor, M1'de o kırmızı. M3'te iki ayrı
`-svtav1-params` kalıyor ve ffmpeg sonuncuyu (`lp=2`) alıp mastering'i atıyor; canlı kol 1 bunu da
görüyor.
