# Akıllı Kesme: Opus Sesli Kaynak

2026-10-07. ffmpeg 9.0, Windows 11. Kaynak `testsrc2` 320x240 24 fps, 6 sn, libvpx-vp9
(`-g 24`) + libopus 96k, WebM. Her komut `-threads 2`, tek süreç.

## Neden Tam Kipe Düşüyordu

`EditExport.SmartEncoding` görüntü kapısından önce `AudioCopies`'e bakıyordu ve o yalnız AAC'yi
geçiriyordu. Görüntü (VP9, AV1) ve kap engel değildi; tek engel ses kapısıydı. Düşüş de
sessizdi: kullanıcı yalnız genel "bu kaynakta kullanılamıyor" notunu görüyordu.

## Ölçüm

Ses parçası akıllı kesimin kendi adımıyla kesildi: `-map 0:a:0 -vn -c copy
-avoid_negative_ts make_zero -f mpegts`. Birleşim iki concat listesiyle, `-c copy`.

| Kol | Çıktı | Süre | Video paket/kare | Ses paketi | Çözme stderr |
|---|---|---|---|---|---|
| Tek klip 1,5–4,5 sn | mkv | 3,000 | 72 / 72 | 150 | boş |
| Tek klip 1,5–4,5 sn | mp4 | 3,000 | 72 / 72 | 150 | boş |
| Üç düz klip (3 + 2 + 3 sn) | mkv, mp4 | 8,000 | — | 400 | boş |
| Düz + 2x hız klibi (libopus ile kodlanan 1 sn) | mkv | — | — | 451 (beklenen 450) | `non monotonically increasing dts … 192000 >= 192000` |

- Eş zamanlama: kaynakta 3,000–3,200 sn'deki 1 kHz ton çıktıda 1,4865–1,6865 sn'de
  (beklenen 1,500–1,700). Kayma −13,5 ms, bir kareden (41,7 ms) küçük.
- Ara kap mpegts kaldı. Matroska ara parçayla mkv çıktıda süre 0,007 sn kaydı.
- Birleşimde "frame size not set" uyarısı çıkıyor; çıkış kodu 0, çıktı temiz çözülüyor.
- Kodlanan Opus parçası 1 sn için 51 paket yazıyor (bir fazla); birleşimde DTS geri gidiyor.
  Bu yüzden hız ya da ters klip varken Opus kodlanmaz, teslim Tam kipe düşer.

## Karar

`EditExport.SmartAudioCopies`: AAC eskisi gibi. Opus yalnız hız/ters klip yokken ve çıktı
`.mkv` ya da `.mp4` iken Akıllı kipte kalır (ses aynen kopyalanır). Öteki her durumda
(mp3, ac3, vorbis, flac; Opus + hız/ters; Opus + `.mov`/`.m4v`) teslim Tam kipe düşer ve
`ExportPlan.AudioForcedFull` işaretlenir; düzenleyici `editor.export.audio-full` notunu yazar.
WebM kaynak düzenleyicide `.mkv`'ye teslim edilir (`EditOutputName.Extension`).

Hızlı kip (`SupportsSegments`) dokunulmadı, AAC'de kaldı.

## Testler Ve Mutasyonlar

`DuzenleyiciAkilliKodekTests`, 59 kol. İki canlı kol (`libvpx-vp9` + `libopus` varsa koşar):
WebM → mkv ve WebM → mp4; dört eski ölçüte ek olarak çıktı sesi `opus`, hiçbir adımda `-c:a`
yok, 4 sn için 199–201 paket, süre ±1 kare.

| Mutasyon | Kırmızı |
|---|---|
| Hız/ters korumasını (`!motion`) kaldırmak | 1 / 59 |
| Kap denetimini kaldırmak | 3 / 59 |
| `Notes` içindeki ses satırını kaldırmak | 1 / 59 |
| `AudioForcedFull`'u hep `false` yazmak | 6 / 59 |

## Ölçülmeyenler

- AV1 + Opus canlı koşulmadı; yalnız saf karar kolu var.
- Gerçek (kamera, YouTube indirmesi) WebM dosyası denenmedi; kaynak lavfi üretimi.
- Opus'un `.mov` ve `.m4v` kopyası ölçülmedi, o yüzden kapalı.
- Beşten uzun, çok klipli çizelgede biriken ses kayması ölçülmedi (üç klipte 0).
- Notun penceredeki görünümü elle denenmedi; sıra kaynak pimiyle, metin 42 dilde testle.
