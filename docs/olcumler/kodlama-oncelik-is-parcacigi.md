# Kodlama Önceliği Ve Kodlayıcı İş Parçacığı

Tarih: 2026-10-14. Paket: `docs/later.md` "## 6. Kodlama Süreç Önceliği Ayarı" (SIK-P1).

## Düzenek

- Kaynak: ffmpeg'in ürettiği 2 sn `testsrc2=1280x720:r=30:d=2` (lavfi), tek süreç.
- Süreç iki çekirdeğe bağlı (affinity), öncelik BelowNormal. Tüm çekirdekleri döndüren ölçüm yapılmadı.
- Karşılaştırma yalnız `-threads 1` ile `-threads 2` arasında; çıktı `-f null`.
- Okunan: sürecin işletim sistemi iş parçacığı sayısı (en yüksek) ve ffmpeg'in `-benchmark` `rtime` değeri.

## Sonuç

| Kodlayıcı | `-threads 1` (iş parçacığı / rtime) | `-threads 2` | Hüküm |
|---|---|---|---|
| libx264 | 11 / 0,679 sn | 15 / 0,532 sn | sınıra uyar |
| libvpx-vp9 | 11 / 1,245 sn | 12 / 0,954 sn | sınıra uyar |
| libx265 | 28 / 0,554 sn | 30 / 0,359 sn | tavan yok, işçi havuzu ~17'de kalır |
| libsvtav1 | 89 / 0,628 sn | 90 / 0,690 sn | bayrağı yok sayar |

Ek ölçüm: libx265 `-x265-params pools=1` ve `pools=2` ikisi de 13 iş parçacığı. libsvtav1 `lp=1` 11, `lp=2` 57,
uydurma anahtar 88 (SVT-AV1 tanımadığı anahtarı sessizce yutar).

## Karar

- `-threads` yalnız libx264 ve libvpx-vp9'a yazılır (`FfmpegArguments.HonoursThreadLimit`). x265 ve SVT-AV1'de
  bayrak söz verdiği sınırı vermiyor; `pools`/`lp` ayrı bir ölçek, bu pakete alınmadı.
- Donanım kodlayıcıları (nvenc, qsv, amf, mf): **ölçülmedi**. Seçimin gizlenmesi koddaki kanıta dayanır:
  `ffmpeg -h encoder=<ad>` bu kodlayıcılar için "Threading capabilities: none" bildiriyor ve `CodecModel.IsHardware`
  doğru. Donanım yolunda ve sınıra uymayan kilitli kodekte Ayarlar'daki satır gizlenir.
- VideoToolbox ve VAAPI: **ölçülmedi** (bu makinenin platformu değil); donanım sayıldıkları için satır gizlenir.
- Çözücü sınırlanmaz; yalnız çıkış tarafı bayrağı yazılır.
- Öncelik Normal / Normalin altı / Düşük → `ProcessPriorityClass.Normal` / `BelowNormal` / `Idle`, süreç başlar
  başlamaz `EncodeRunner.RunCommandAsync` içinde atanır.
