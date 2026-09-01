# Tepe hızı tavanı ve psiko-görsel bayraklar

Tarih: 2026-09-01. Ortam: Windows 11, ffmpeg 9.0-full, `av1_nvenc` p5. Bütün sayılar `tools/VidShrink.Bench shrink` JSON çıktısıdır. Kaynaklar aynı hareketli içeriğin kayıpsız hazırlanmış 20 saniyelik iki klibidir: 1280×720@60 ve 1920×1080@30. Ses kapalı, çözünürlük ve FPS düşürme kapalı, kalibrasyon kapalıdır.

## Tepe çarpanı ve teslim boyutu

`Eğri` sütununda mevcut diz eğrisinin ürettiği gerçek tepe çarpanı, `1,50` sütununda bağımsız geniş-tepe koşumu vardır. Taban oranı, planın istediği video bit hızının `CodecModel.MinBitrateK` değerine bölümüdür. Teslim oranı gerçek MiB / hedef MiB'dir.

| Kaynak | Taban | İstenen kbit/sn | Hedef MiB | Eğri tepe | Eğri teslim MiB | Eğri oran | 1,50 teslim MiB | 1,50 oran |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| 720p60 | 2,920680 | 1031 | 2,6 | 1,02 | 2,5313377380371094 | 0,9735914377065805 | 2,547816276550293 | 0,9799293371347281 |
| 720p60 | 7,798867 | 2753 | 6,9 | 1,046498 | 6,794994354248047 | 0,9847817904707313 | 6,785617828369141 | 0,9834228736766870 |
| 720p60 | 11,898017 | 4200 | 10,35 | 1,10 | 10,032888412475586 | 0,9693611992730034 | 10,018817901611328 | 0,9680017296242830 |
| 1080p30 | 3,055328 | 1491 | 3,75 | 1,02 | 3,6605911254882812 | 0,9761576334635417 | 3,516530990600586 | 0,9377415974934897 |
| 1080p30 | 7,776639 | 3795 | 9,5 | 1,046115 | 9,398728370666504 | 0,9893398284912110 | 9,368837356567383 | 0,9861934059544613 |
| 1080p30 | 11,901639 | 5808 | 14,3 | 1,10 | 14,216007232666016 | 0,9941263799067144 | 14,200657844543457 | 0,9930529961219200 |

Bu iki klipte 1,50 hiçbir hedefi aşmadı; buna karşın eski 882×496@60, 11,4× ölçümü 1,50 ile 1,056 teslim oranını göstermişti. Dolayısıyla çelişki kaynak/yerleşim bağımlılığıdır: gerçek HDR koşumundaki kalite kazancı doğrudur, fakat 1,50 genel boyut garantisi değildir. `HardwarePeakCeiling=1.10` ve 6,0–11,4 diz eğrisi korunacaktır; yeni ölçüm eski aşma kanıtını geçersiz kılmıyor.

## Psy/AQ ablasyonu

Kaynak `clip-720p60.mkv`, hedef 6,9 MiB, plan 1280×720@60 `av1_nvenc` p5 ve 2753 kbit/sn'dir. Tek fark, açık koşumda ölçülerek desteklendiği doğrulanan `-spatial-aq 1 -temporal-aq 1`; kapalı koşumda aynı seçeneklerin son argüman olarak `0` verilmesidir. Kalite, `tools/VidShrink.Bench measure` ile klibin tamamında ölçüldü.

| AQ | Teslim MiB | Hedef oranı | VMAF-NEG mean | Harmonic | p10 |
|---|---:|---:|---:|---:|---:|
| Kapalı | 6,787405014038086 | 0,9836818860924762 | 75,19705973749988 | 63,44009931105794 | 36,7723875 |
| Açık | 6,794994354248047 | 0,9847817904707313 | 75,29163206000004 | 63,59703711920521 | 36,9102289 |
| Açık − kapalı | 0,007589340209961 | 0,0010999043782551 | 0,09457232250016 | 0,15693780814727 | 0,1378414 |

Üç VMAF özeti de pozitif ve iki çıktı da hedefin altında kaldı; NVENC AQ bayrakları korunuyor. `libx265` ve `libsvtav1` eşdeğerlerinin kalite etkisi bu sözleşmede ölçülmedi; bunlar yalnız gerçek seçenek yoklaması başarılıysa üretiliyor. Özellikle SVT-AV1 yoklaması yalnız çıkış koduna güvenmiyor: stderr'deki `Error parsing option`, `Option not found` ve `Unrecognized option` tanılarını da başarısız sayıyor.
