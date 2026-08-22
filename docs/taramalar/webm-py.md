Tema: hedef boyut GUI · kaynak: hedef-boyut-gui.md

# Hedef dosya boyutuna kodlayan masaüstü araçları

## Kagami/webm.py

**Künye** — CC0-1.0, son push 2020-08-02 (terk edilmiş sayılır), 145 yıldız, 4 açık issue, GitHub'da etiketli sürüm yok (releases API 404); PyPI'da `webm` paketi var, sürüm tarihi doğrulanamadı.

**Ne yapıyor** — Tek dosyalık CLI, imageboard yükleme limitine sığdırmak için WebM üretir. `-l 10` = 10 MiB limit. Formül tek satır: `video_kbit = limit_MiB*8*1024/süre_sn - ses_kbit`, sonuç 0,1 kbit'e yuvarlanıyor, negatifse anlaşılır bir hata metniyle duruyor. **Hiç deneme yapmıyor** — tek 2 geçişli kodlama, bitti. Sonunda çıktıyı ölçüp `OVERWEIGHT: N B` veya `underweight: N B` basıyor, düzeltmeyi kullanıcıya bırakıyor. İlk geçişte `speed = max(4, speed)` (turbo eşdeğeri). Vorbis VBR'de sesin payını çıkarabilmek için sabit bir `q → kbps` eşleme tablosu tutuyor.

**Alınacak fikir** — (1) Ses bitrate'i sabit değilse bile plan hesabı için **ölçülmüş bir tahmin tablosu** gerekir; VidShrink Opus/AAC VBR seçeneği eklerse aynı sorun çıkar. (2) Hedefe sığmayan durumu kodlamadan *önce* yakalayıp "limit çok düşük / süre çok uzun / ses bitrate'i çok yüksek" diye üç somut nedenle reddetmek — bizim hata metnimizden daha kullanışlı bir kalıp.

**Alınmayacak** — Kuru bölme + tek atış. Konteyner overhead'i, karmaşıklık ve kodlayıcı sapması hesaba katılmadığı için sonuç sistematik olarak limitin bir yanına düşüyor; "underweight" durumunda kalite bedava bırakılıyor. VidShrink'in doluluk bandı tam olarak bu boşluğu kapatıyor, geri adım atmayalım.

**VidShrink'te nereye dokunur** — `src/VidShrink.Core/PlanCalculator.cs` (fizibilite ön kontrolü), `src/VidShrink.Core/MediaInfo.cs` (ses bitrate tahmini), `src/VidShrink.App/MainWindow.xaml.cs` (ret mesajı).

## Kaynaklar

- `gh api repos/HandBrake/HandBrake` + `/releases/latest`; `NEWS.markdown` (0.9.6 bölümü); `libhb/preset.c`, `gtk/src/videohandler.c`, `macosx/HBVideo.m`
- `gh api repos/Kagami/webm.py` + `/releases/latest` (404); `webm.py` — `_calc_video_bitrate`, `_vorbisq2bitrate`, `_encode`, `print_stats`
- `gh api repos/staxrip/staxrip` + `/releases/latest`; `Source/General/Misc.vb` (`Calc`), `Source/General/Project.vb` (varsayılanlar), `Source/General/GlobalClass.vb` (`GetAutoSize`), `Source/Encoding/VideoEncoder.vb` (`RunCompCheck`, `AutoSetImageSize`)
- VidShrink karşılaştırması: `src/VidShrink.Core/PlanCalculator.cs` (satır 24-43)
