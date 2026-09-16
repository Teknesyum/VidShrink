# B4 — HDR10: PQ'da Kıyas ve Yan Veri Eşliği

Durum: **ölçüldü, kod değişmedi.** Açık lisanslı HDR10 kaynakta ürün ile HandBrake 1.11.2 (x265 10 bit) aynı bayta
oturtuldu; PQ transferi, BT.2020 renk uzayı ve mastering/CLL yan verisi çıktıda kontrol edildi.

## Düzenek

- Koşum **35158725446**, etiket `olcum-hb-handbrake+dusuk+social+bantlasma+turbo+hdr+ekranbant+vt--1`, commit
  `02993179`, işler `olcum (hdr, CosmosCaterpillar)` ve `olcum (hdr, CosmosTreeTrunk)`.
- Kaynak: xiph AOM CTC `hdr2_2k`, Cosmos Laundromat, **CC BY 3.0** (Blender Foundation, HDR derecelendirme Netflix).
  - `https://media.xiph.org/video/aomctc/test_set/hdr2_2k/CosmosCaterpillar_2048x858p24_hdr10.y4m` md5
    `793c7f42b657ca2b5755a46c853ce4a8`, sha256 `CC7962F623FE5305AD4644873643A049E045902196BCA0263FCC83FF19B2F1D6`,
    685302597 bayt.
  - `https://media.xiph.org/video/aomctc/test_set/hdr2_2k/CosmosTreeTrunk_2048x858p24_hdr10.y4m` md5
    `7bc5d9a7c8b56ef62dd8ea9afcc50110`, sha256 `DE1FDBC7624EDC993311C9D7FBA350951A8A1ABDFCE66B200EEA53EFE09EBDDC`,
    685302597 bayt.
- Y4M başlığı `YUV4MPEG2 W2048 H858 F24:1 Ip A0:0 C420p10 XYSCSS=420P10`: renk ve HDR yan verisi taşımıyor. Ara
  dosya kayıpsız x265 ile mkv'ye yazıldı ve **sentetik** etiketlendi: PQ, BT.2020, `master-display=G(13250,34500)
  B(7500,3000)R(34000,16000)WP(15635,16450)L(10000000,50):max-cll=1000,400`. ffmpeg bu yolda akış düzeyi
  transfer/primaries'i "unknown" bıraktığı için `-c copy` ile renk bayraklarıyla yeniden paketlendi (`kaynak-ara`
  satırı smpte2084/bt2020 okuyor).
- Kollar: `urun-otomatik`, `handbrake` (`-e x265_10bit --encoder-preset slow --multi-pass --turbo -f av_mkv`, bayta
  ±%2), `negatif-handbrake-yarim-bit`. 1000 ve 3000 kbit.
- CAMBI `eotf=pq` ile; TreeTrunk'ın yedi satırında anahtar `cambi_ench_858_encw_2048_eotf_pq`, yani PQ kabul edildi.

## Sonuç

- 4 satırın 3'ü **önde**: Caterpillar 1000 +0,88 / +0,73 dB, TreeTrunk 1000 +4,60 / +0,15, TreeTrunk 3000 +2,75 /
  −0,03. Caterpillar 3000 **geride**: VMAF-NEG −0,19 (gürültü içinde), XPSNR −0,25 (eşiğin 0,05 dışında).
- kbps sapması −%1,89 ile +%0,23; ürünün kodlama saniyesi HandBrake'in 0,20–0,48 katı.
- **PQ korundu**: ürün ve HandBrake kollarının dört çıktısında da transfer `smpte2084`, primaries `bt2020`, CLL `1000,400`.
- **Yan veri**: ürün mastering değerlerini kaynaktaki paydalarla aynen yazdı (`yan_veri_es` evet). HandBrake sadeleşmiş
  kesirlerle yazdı: R(17/25,8/25) = 0,68/0,32, G(53/200,69/100) = 0,265/0,69, B(3/20,3/50) = 0,15/0,06,
  WP(3127/10000,329/1000) = 0,3127/0,329, L(1000/1,1/200) = 1000/0,005 cd/m². Bunlar kaynaktaki
  34000/50000, 16000/50000, … 10000000/10000, 50/10000 ile **sayısal olarak eşit**. Özet betiğindeki "hayır" metin
  karşılaştırmasından; iki taraf da yan veriyi koruyor.
- CAMBI ürünün aleyhine ama eşiğin altında: +0,08 ile +0,41 (TreeTrunk 0,74–0,77'ye karşı 0,34–0,45).
- Negatif kontrol dört satırda VMAF-NEG'i −2,48 ile −13,44 düşürdü.

Karar: HDR10'da ürün aynı baytta 3/4 önde, PQ ve yan veri eşit korunuyor. Kod değişmedi.
**Ölçülmedi:** gerçek HDR10 mastering yan verisi taşıyan kaynak (xiph dosyaları taşımıyor), Dolby Vision/HDR10+,
HDR→SDR ton eşleme, PQ'ya duyarlı VMAF modeli.

## Tablo

| Kesit | kbit | Kol | Kodlayıcı | Geometri | kbps | Bayt | VMAF-NEG ort | VMAF-NEG harm | XPSNR | SSIM | CAMBI | Karanlık PSNR | Kodlama sn | Hata | Transfer | Primaries | Mastering | CLL | Yan veri eş | PQ korundu |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| CosmosCaterpillar | — | kaynak-ara | — | — | — | — | — | — | — | 1,0000 | 0,000 | — | — | — | smpte2084 | bt2020 | R(34000/50000,16000/50000) G(13250/50000,34500/50000) B(7500/50000,3000/50000) WP(15635/50000,16450/50000) L(10000000/10000,50/10000) | 1000,400 | — | — |
| CosmosCaterpillar | 1000 | urun-otomatik | libsvtav1 | 2048x858 | 977,9 | 662117 | 89,91 | 89,88 | 35,79 | 0,9947 | 0,101 | 41,41 | 24,9 | — | smpte2084 | bt2020 | R(34000/50000,16000/50000) G(13250/50000,34500/50000) B(7500/50000,3000/50000) WP(15635/50000,16450/50000) L(10000000/10000,50/10000) | 1000,400 | evet | evet |
| CosmosCaterpillar | 1000 | handbrake | HandBrakeCLI 1.11.2 x265_10bit slow 2 gecis turbo | 2048x858 | 959,4 | 649628 | 89,04 | 89,01 | 35,06 | 0,9924 | 0,007 | 40,58 | 51,4 | — | smpte2084 | bt2020 | R(17/25,8/25) G(53/200,69/100) B(3/20,3/50) WP(3127/10000,329/1000) L(1000/1,1/200) | 1000,400 | hayır | evet |
| CosmosCaterpillar | 1000 | negatif-handbrake-yarim-bit | — | 2048x858 | 501,9 | 339857 | 79,59 | 79,54 | 32,52 | 0,9833 | 0,025 | 38,56 | 46,0 | — | — | — | — | — | — | — |
| CosmosCaterpillar | 3000 | urun-otomatik | libsvtav1 | 2048x858 | 2995,8 | 2028378 | 94,35 | 94,31 | 37,42 | 0,9970 | 0,093 | 42,51 | 12,9 | — | smpte2084 | bt2020 | R(34000/50000,16000/50000) G(13250/50000,34500/50000) B(7500/50000,3000/50000) WP(15635/50000,16450/50000) L(10000000/10000,50/10000) | 1000,400 | evet | evet |
| CosmosCaterpillar | 3000 | handbrake | HandBrakeCLI 1.11.2 x265_10bit slow 2 gecis turbo | 2048x858 | 3002,7 | 2033224 | 94,54 | 94,50 | 37,66 | 0,9967 | 0,009 | 42,14 | 64,1 | — | smpte2084 | bt2020 | R(17/25,8/25) G(53/200,69/100) B(3/20,3/50) WP(3127/10000,329/1000) L(1000/1,1/200) | 1000,400 | hayır | evet |
| CosmosCaterpillar | 3000 | negatif-handbrake-yarim-bit | — | 2048x858 | 1450,7 | 982303 | 92,06 | 92,03 | 36,24 | 0,9949 | 0,006 | 41,37 | 55,0 | — | — | — | — | — | — | — |
| CosmosTreeTrunk | — | kaynak-ara | — | — | — | — | — | — | — | 1,0000 | 0,559 | — | — | — | smpte2084 | bt2020 | R(34000/50000,16000/50000) G(13250/50000,34500/50000) B(7500/50000,3000/50000) WP(15635/50000,16450/50000) L(10000000/10000,50/10000) | 1000,400 | — | — |
| CosmosTreeTrunk | 1000 | urun-otomatik | libsvtav1 | 2048x858 | 962,4 | 651631 | 60,89 | 60,58 | 32,28 | 0,9561 | 0,743 | 30,78 | 23,0 | — | smpte2084 | bt2020 | R(34000/50000,16000/50000) G(13250/50000,34500/50000) B(7500/50000,3000/50000) WP(15635/50000,16450/50000) L(10000000/10000,50/10000) | 1000,400 | evet | evet |
| CosmosTreeTrunk | 1000 | handbrake | HandBrakeCLI 1.11.2 x265_10bit slow 2 gecis turbo | 2048x858 | 959,2 | 649482 | 56,28 | 55,94 | 32,13 | 0,9426 | 0,336 | 29,61 | 49,5 | — | smpte2084 | bt2020 | R(17/25,8/25) G(53/200,69/100) B(3/20,3/50) WP(3127/10000,329/1000) L(1000/1,1/200) | 1000,400 | hayır | evet |
| CosmosTreeTrunk | 1000 | negatif-handbrake-yarim-bit | — | 2048x858 | 480,5 | 325382 | 42,84 | 42,41 | 30,43 | 0,9103 | 0,377 | 28,43 | 44,1 | — | — | — | — | — | — | — |
| CosmosTreeTrunk | 3000 | urun-otomatik | libsvtav1 | 2048x858 | 2872,3 | 1944768 | 78,83 | 78,70 | 34,78 | 0,9802 | 0,768 | 32,61 | 23,5 | — | smpte2084 | bt2020 | R(34000/50000,16000/50000) G(13250/50000,34500/50000) B(7500/50000,3000/50000) WP(15635/50000,16450/50000) L(10000000/10000,50/10000) | 1000,400 | evet | evet |
| CosmosTreeTrunk | 3000 | handbrake | HandBrakeCLI 1.11.2 x265_10bit slow 2 gecis turbo | 2048x858 | 2861,8 | 1937800 | 76,08 | 75,89 | 34,81 | 0,9758 | 0,450 | 31,70 | 62,5 | — | smpte2084 | bt2020 | R(17/25,8/25) G(53/200,69/100) B(3/20,3/50) WP(3127/10000,329/1000) L(1000/1,1/200) | 1000,400 | hayır | evet |
| CosmosTreeTrunk | 3000 | negatif-handbrake-yarim-bit | — | 2048x858 | 1394,2 | 944045 | 63,61 | 63,32 | 33,00 | 0,9565 | 0,379 | 30,30 | 53,7 | — | — | — | — | — | — | — |

| Kesit | kbit | Δ VMAF-NEG ort | Δ VMAF-NEG harm | Δ XPSNR | Δ SSIM | Δ CAMBI (ürün−HB) | Δ karanlık PSNR | Ürün sn / HB sn | kbps sapma % | Hüküm |
|---|---|---|---|---|---|---|---|---|---|---|
| CosmosCaterpillar | 1000 | 0,88 | 0,87 | 0,73 | 0,0023 | 0,094 | 0,82 | 0,48 | -1,89 | önde |
| CosmosCaterpillar | 3000 | -0,19 | -0,19 | -0,25 | 0,0003 | 0,084 | 0,37 | 0,20 | 0,23 | geride |
| CosmosTreeTrunk | 1000 | 4,60 | 4,64 | 0,15 | 0,0135 | 0,407 | 1,17 | 0,46 | -0,33 | önde |
| CosmosTreeTrunk | 3000 | 2,75 | 2,81 | -0,03 | 0,0044 | 0,319 | 0,91 | 0,38 | -0,37 | önde |
