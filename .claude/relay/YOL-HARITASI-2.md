# Yol haritası 2 — HandBrake'i geçmek

2026-09-02. Birinci yol haritası (T1–T94) motoru ve ölçüm disiplinini kurdu.
Ürün hedefi hâlâ karşılanmadı, bu yüzden harita yeniden çiziliyor.

## Nerede duruyoruz

Eş boyutta: HandBrake VMAF-NEG **48,96**, VidShrink **40,17**. **8,79 puan
gerideyiz.** Kaynak `docs/olcumler/handbrake-acigi.md`.

Aynı belgede bizi önde gösteren eski tablo **GEÇERSİZ DÜZENEK** damgalı.
Bu, haritanın birinci maddesini belirliyor: önce alet, sonra rakam.

## Elimizdeki düzenek (2026-09-02 doğrulandı)

- `.calisma/kaynak/kaynak-1080p60-hdr-17dk.mp4` — 1920x1080 hevc yuv420p10le,
  bt2020nc / smpte2084 / bt2020, 60 fps, 1036,17 sn, 1.729.085.563 bayt.
- `ffmpeg`, `ffprobe`, `HandBrakeCLI` PATH'te.
- Kodlayıcılar: libsvtav1, libx265, av1_nvenc, av1_qsv, av1_amf,
  hevc_nvenc, hevc_amf, h264_nvenc, h264_qsv, h264_amf.

Belgede "kaynak yoktu" diye park edilmiş her ölçüm artık koşulabilir.

## Açığın aday kaynakları

8,79 **ortalamadır**. Harmonikte açık 10,18, p10'da 14,60 — tek sayı kuyruk
hasarını olduğundan küçük gösteriyor. Hedef ortalamayı değil kuyruğu kapatmak.

| # | Aday | Ölçülen | Durum |
|---|---|---|---|
| A | Kodlayıcı — libx265 slow 2-pass, aynı yerleşimde | ort **-0,63**, harmonik **+8,65**, p10 **+13,76**; süre 216 → 1599 sn | kuyruk açığının ana sahibi |
| B | Çözünürlük tabanı — 882x496 seçiliyor, 720p60 hiç aday olmuyor | ölçülmedi | **T99** |
| C | Tepe/VBV tavanı — 1,02x → 1,50x | ort **+1,69**, harmonik **+5,87**, p10 **+7,22**, süre ~0 | **T98** |
| D | GOP — biz 2 sn (`-g fps*2`), HandBrake tarafı çok daha uzun | ölçülmedi | **T98** |
| E | Sahne bazlı bit dağıtımı yok | ölçülmedi | T96 |
| F | psy-rd / AQ | ort +0,095 | kapandı (T87, T92) |
| G | HDR yıkımı | karşılaştırma dışı; iki taraf da tonemap-hizalı puanlandı | kod düzeltildi (`28637a4`, `main`'de) |

Payların doğrulanması T95'in aletine bağlı. Sıralama tahmindir, ölçümle değişir.

## Düzeltilen öncüller (2026-09-02)

Üç varsayımım kaynağa bakınca çürüdü. Yazıyorum ki tekrar edilmesin:

- **HandBrake çıktısı 1080p değil, 1280x720@60.** Çözünürlük açığı 720p'ye
  karşı hesaplanmalı.
- **Varsayılanımız donanım değil.** `SpeedMode` varsayılanı Quality
  (`PlanCalculator.cs:16`), Compatible → libx264. `av1_nvenc` yalnız Fast
  kutusundan ve WhatsApp yolundan geliyor; şikâyet koşusu `--speed fast`'ti.
- **HDR sessiz düşüşü kod tarafında zaten düzeltilmiş** — `28637a4`,
  `main`'de. Kalan soru "neden düşüyor" değil, "düzeltme gerçek kaynakta
  çalışıyor mu ve ne kazandırdı".

## Taban neden yanlış yerde duruyor

`CodecModel.FloorBppf` av1 = 0,020, donanımda x1,25 = **0,025**
(`CodecModel.cs:58-67`). `PlanCalculator.cs:612` bu tabanın altındaki
yerleşimleri eliyor. 790k'da 720p60 = 0,0143 bppf → elenir; 882x496 = 0,030
→ geçer. **HandBrake'in kazanan dosyası 0,0116 bppf'te koşuyor.** Yani
tabanımız, rakibin kazandığı yerleşimi aramaya bile başlamadan dışlıyor.

## Sözleşmeler

| # | İş | Durum |
|---|---|---|
| T89 | plan hesabı, ölçülen kaliteyle + K13 | teslim edildi, denetimde |
| T92 | yanlışlanamayan ölçü | denetimden GEÇTİ, T89 ile birleşir |
| T95 | A/B ölçüm düzeneği (`tools/VidShrink.Ab`) | koşuyor |
| T96 | sahne haritası ve kestirim değeri | koşuyor |
| T97 | algı ölçüsünün doğruluğu | koşuyor |
| T94 | HDR düzeltmesinin gerçek kaynakta doğrulanması | `depends: [T89, T95]` |
| T98 | tepe tavanı + GOP — iki ucuz kaldıraç | `depends: [T89, T95]` |
| T99 | bppf tabanı kazanan yerleşimi aramıyor | `depends: [T89, T95]` |
| T100 | ölçülen kalitenin kazancı kullanıcıya ulaşmıyor | `depends: [T89]` |

## T89'un çıkardığı kopukluk (T100)

Plan, HDR kaynakta 40 MB bütçenin 15,5 MB'ını bilerek harcamadan bıraktı
(crf 22 → 24,483 MB). Teslim edilen dosya yine de 38,404 MB oldu: `EncodeRunner`
çıktıyı band altı sanıp yeniden kodluyor ve bütçeyi dolduruyor. Planlayıcının
"burada durmak daha iyi" kararını koşucu kaza sayıyor.

İki sonucu var: durdurma kısıtının teslim edilen dosyaya etkisi sıfıra yakın,
ve T89'un ölçtüğü %78–%193 süre artışının tamamı bu gereksiz yeniden
denemelerden geliyor. Ayrıca `MainWindow.axaml.cs` ölçülen kaliteyi çağırıp
atıyor — ölçülen yol uygulamada uyuyor.

## Sonraki basamak

1. T89 + T92 birleşir ve mühürlenir; T100 hemen açılır.
2. T95 teslim edince T94, T98, T99 aynı anda açılır — üçü de aletin ölçümüne
   dayanıyor.
3. `SceneMap` `PlanCalculator`a bağlanır (T99 mühürlendikten sonra).
4. Kodlayıcı seçim kuralı ölçülen veriye göre yeniden yazılır — kuyruk
   açığının ana sahibi orada.

## Değişmeyen kurallar

- Sabit karşılaştıran ölçü davranış ölçmez.
- Ölçmediğin şey için "ölçülmedi" yazılır, iddia edilmez.
- Aynı anda tek ağır kodlama; paralel koşum ölçüyü kararsız yapar.
- Mühürden önce `gh run list`.
- `main`e yalnız T0 birleştirir.
