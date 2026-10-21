# V2 İzi Ve Yeşil Perde Ölçümü

Later 110 (DZB-02, DZB-29). Düzenleyicinin üstüne ikinci bir video izi gelir. Her katman
başlangıcını, kırpımını, boyunu (genişliğin oranı) ve yerini (köşe ya da orta) taşır. Teslim
`overlay` ile bindirir; yeşil perde açıksa bindirmeden önce `chromakey`
(`RecorderArguments.GreenScreenKey`) uygulanır. Metin katmanların üstünde kalır.

Önizleme 109'un yolundan gider: katmanın düştüğü aralık kısa bir ön-işlemeyle küçük bir dosyaya
yazılır (`LayerPreview`), `edl://` listesine 1:1 süreli bir yama parçası olarak girer.

## Ölçüm

Kaynak: `DuzenleyiciV2IzTests.TeslimKaresindeKatmanKutusuYerindeVeYesilPerdeDelinir`, ffmpeg 9.0
(gyan.dev), her süreçte `-threads 2`, 2026-10-21.

- Ana video 320x180, 4 sn, düz kırmızı.
- Katman 320x180, 2 sn, düz yeşil (`0x00FF00`), ortada 80x60 mavi kutu.
- Katman 1. saniyede başlar, boy 0,5, sağ üst köşe (160x90, x 160-320, y 0-90).
- Teslim Tam kipte. Bölge rengi `crop` + `scale=1:1:flags=area` ile tek piksele indirilip okundu.

| dosya | an | bölge | RGB | beklenen |
|---|---|---|---|---|
| yeşil perde kapalı | 0,5 sn | katman kenarı (168,8 16x16) | 254, 0, 0 | katman henüz yok |
| yeşil perde kapalı | 2,0 sn | mavi kutunun ortası (232,38 16x14) | 0, 0, 255 | katman yerinde |
| yeşil perde kapalı | 2,0 sn | katman kenarı | 0, 255, 1 | yeşil zemin görünür |
| yeşil perde kapalı | 2,0 sn | katman dışı (40,120 40x40) | 254, 0, 0 | ana video |
| yeşil perde kapalı | 3,5 sn | katman kenarı | 254, 0, 0 | katman bitti, ana video donmaz |
| yeşil perde açık | 2,0 sn | mavi kutunun ortası | 0, 0, 255 | kutu kalır |
| yeşil perde açık | 2,0 sn | katman kenarı | 254, 0, 0 | yeşil delinir, ana video görünür |

Mutasyon: `EditExport.LayerGraph`'tan `chromakey` dalı çıkarıldı. Yeşil perde açıkken katman
kenarı 0, 255, 1 okundu; ölçüm testi ve argüman testi kırmızı oldu. Dosya düzenlenip
`--no-incremental` ile derlenince ikisi yeniden yeşil.

## Açık

- Önizlemede geçiş bölgesine düşen katman görünmez. Teslimde görünür.
- Katmanın sesi alınmaz.
- Parça parça teslim ve yalnız ses teslimi katmanı yok sayar.
- OTIO aktarımı V2'yi taşımaz.
- Yamanın canlı önizlemedeki görüntüsü başsız testle değil, argüman testiyle ölçüldü.
