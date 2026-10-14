# Akıllı Kesme: AC-3, E-AC-3 Ve MP3 Sesli Kaynak

2026-10-14. ffmpeg 9.0 (gyan.dev full), Windows 11. Ölçer `bench smart-audio-drift`
(`tools/VidShrink.Bench/AkilliSesKaymasi.cs`). Her komut `-threads 2`, tek süreç.

## Düzenek

Kaynak 12 sn, 320x240 24 fps libx264 (`-g 24`), Matroska. Görüntü siyah; her tam saniyede bir
beyaz kare. Ses 48 kHz, her tam saniyede 50 ms 1 kHz ton, 192k. Çizelge üç kesim:
0,5–3,25, 4,625–7,25, 8,125–11,625 sn (kare sınırında, ses çerçevesi sınırında değil).
Akıllı kip planı `EditExport.Build` ile kurulur, ses kapısı ölçüm için atlanır.

Kayma: çıktıdaki her beyaz karenin zamanı (`signalstats` YAVG > 128) ile aynı saniyenin tonunun
başı (`silencedetect` -30 dB) arasındaki fark, kaynaktaki farktan (0,0 ms) çıkarılır. Eşik bir
görüntü karesi: 41,7 ms; mevcut canlı kolların "±1 kare" ölçütüyle aynı.

Ara ses parçası MPEG-TS (`AudioCopy`, `-c copy -f mpegts`). AC-3, E-AC-3 ve MP3'ü MPEG-TS
taşıyor; Matroska ara kap bu üç kodekte gerekmedi.

## Sonuç

| Kodek | Kap | İşaret | Kayma ms (üç kesim, üçer işaret) | En büyük | Çözme hatası | Sonuç |
|---|---|---|---|---|---|---|
| aac (kıyas) | mkv, mp4, mov, m4v | 9/9 | −12,0 / −4,2 / −3,0 | 12,0 | 0 | geçti |
| ac3 | mkv, mp4, mov, m4v | 9/9 | −6,7 / −9,7 / −29,7 | 29,7 | 0 | geçti |
| eac3 | mkv, mp4, mov | 9/9 | −6,7 / −9,7 / −29,7 | 29,7 | 0 | geçti |
| eac3 | m4v | — | — | — | — | teslim düştü (ffmpeg −22) |
| mp3 | mkv, mp4, mov | 9/9 | −5,0 / −8,0 / −12,0 | 12,0 | 0 | geçti |
| mp3 | m4v | — | — | — | — | teslim düştü (ffmpeg −22) |
| opus (kıyas) | mkv, mp4 | 9/9 | −13,5 / −8,5 / −8,5 | 13,5 | 0 | geçti |
| opus (kıyas) | mov, m4v | — | — | — | — | teslim düştü (ffmpeg −22) |

- Kayma birikmiyor: her parçanın yeri concat listesindeki `duration` ile konuyor, hata klip başına
  bir ses çerçevesiyle sınırlı (AC-3 1536 örnek = 32 ms, MP3 1152 örnek = 24 ms). AC-3'ün
  −29,7 ms'si bu sınırın içinde.
- Opus satırı `akilli-kesme-opus.md`'deki −13,5 ms'yi tekrar üretti.

## Karar

`EditExport.CopyOnlyAudio`: ac3 → mkv, mp4, mov, m4v; eac3 ve mp3 → mkv, mp4, mov; opus eskisi gibi
mkv, mp4. Bu kodekler yalnız hız/ters klip yokken kopyalanır: o klipte ses `AudioEncode` ile AAC'ye
kodlanır ve kopyalanan parçayla aynı akışta birleşmez. Öteki her durumda Tam kipe düşülür ve
`ExportPlan.AudioForcedFull` işaretlenir. Eşiği aşan kodek çıkmadı.

## Testler Ve Mutasyonlar

`DuzenleyiciAkilliKodekTests`, 78 kol, 78 geçti. Yeni canlı kollar (6 sn lavfi, 44,1 kHz ses,
4 sn tek kesim): h264 + ac3 mkv → mkv (115 paket), h264 + eac3 mkv → mp4 (115), h264 + mp3
mp4 → mov (153); çıktı sesi kaynakla aynı kodek, hiçbir adımda `-c:a` yok.

| Mutasyon | Kırmızı |
|---|---|
| Tablodan ac3'ü çıkarmak | 6 / 78 |
| Hız/ters korumasını (`!motion`) kaldırmak | 1 / 78 |
| mp3'e `.m4v` eklemek | 1 / 78 |

## Ölçülmeyenler

- DTS, TrueHD, Vorbis, FLAC, PCM ölçülmedi; kapının dışında kaldı.
- Gerçek film ya da TV kaydı (5.1 AC-3, değişken kare hızı) denenmedi; kaynak lavfi üretimi, mono.
- Üçten çok kesim ölçülmedi; 12 sn kaynakta üç kesim koştu.
- Kare sınırına oturmayan kesim (ör. 4,7 sn) bu ölçümün dışında; görüntüde ayrı bir kusur
  gösteriyor, ses kodeğinden bağımsız (AAC'de de).
