Tema: hedef dosya boyutu kodlayicilari (ek arama) · kaynak: ek-arama.md

# Ek arama — hedef dosya boyutu kodlayıcıları

Seçilenler: **cyroz1/vidcord** ve **zfleeman/ffmpeg4discord**. İkisi de VidShrink'in tam
problemini çözüyor: kullanıcı MB verir, araç plan kurar, çıktıyı ölçer, aşarsa düzeltir.
vidcord masaüstü GUI + donanım kodlayıcı + turlu düzeltmeyle en yakın eş; ff4d'nin formülü ve
döngüsü en açık yazılmış olan. Elenenler: KickerMix/kennethprose DiscordVideoCompressor (tek
atış), MyloBishop/discompress (arşivli script).

## Ne yapıyor

**ff4d** — CLI + isteğe bağlı Flask arayüzü, iki geçişli VBR. `twopass.py`:
`br = floor((hedef_MiB*8192)/süre_sn - ses_kbps/1000)*1000` bps, `minrate = 0.5×br`,
`maxrate = 1.45×br`. `__main__.py` döngüsü üst sınırsız: çıktı hedefin üstündeyse
`hedef *= hedef/ölçülen` (taban 0.1 MB) ile küçültülüp baştan iki geçiş. `--approx` döngüyü
kapatır; girdi zaten hedeften küçükse uyarır.

## Depo

- **cyroz1/vidcord** — Rust + React (Tauri 2), MIT, 70 yıldız, 0 açık issue,
  son push 2026-08-19, son sürüm v7.3 (2026-08-14).
- **zfleeman/ffmpeg4discord** — Python (ffmpeg-python + Flask), GPL-3.0, 110 yıldız,
  3 açık issue, son push 2026-08-19, son sürüm v0.2.3 (2026-08-19). Rakamlar `gh api` çıktısı (2026-08-22).

## Alınacak fikir

1. **Belirsizlik bandına göre prob uzatma.** Projeksiyon/hedef oranı bandın içindeyse örneği
   uzat, dışındaysa tek örnekle yetin. VidShrink'in probu sabit 2–3 pencere; koşullu pencere
   ekleme kolay içerikte maliyeti düşürür. Maliyet: `MaxWindows` etrafında tek karar fonksiyonu.
2. **Ölçülen aşımdan oran düzeltmesi + emniyet payı, tur kapaklı.** `yeni = mevcut ×
   (hedef/ölçülen) × 0.90`, kodlayıcı başına sabit tur, sonra CPU'ya düşüş, en sonda en küçük
   aşan sonucu bildir. Düzeltmeye deterministik üst sınır verir. Maliyet: tur sayacı + düşüş sırası.
3. **Hedef bitrate'i kaynak bitrate'iyle `min`'lemek.** Hedef kaynaktan yüksek bitrate ister hâle
   geldiğinde yeniden kodlama boşuna; ff4d uyarıyor, vidcord clamp'liyor. Maliyet: tek clamp +
   "kopyala/atla" yolu.

## Alınmayacak

- **ff4d'nin sınırsız döngüsü.** Tur kapağı yok, her turda hedefin kendisi çarpılarak
  küçültülüyor; düşüş kümülatif, zor içerikte bitrate dibe iner, süre öngörülemez.
- **vidcord'un sabit %10 payı ve tek geçiş ABR'si.** Pay karmaşıklıktan bağımsız, kolay
  içerikte doluluk kaybettirir; `CrfFitMargin`/`TwoPassUncertainty` ölçüme bağlı, geri adım olur.
- İki depoda da MB/MiB ayrımı gevşek; VidShrink tek birim tanımını korumalı.

## VidShrink'te nereye dokunur

- Fikir 1 → `src/VidShrink.Ffmpeg/CalibrationProbe.cs` (`MaxWindows`, `MinWindows`).
- Fikir 2 → `EncodeRunner.cs` (tur sayacı), `PlanCalculator.cs` (`HardwareUncalibratedBias`),
  düşüş sırası `EncoderCapabilities.cs`.
- Fikir 3 → `src/VidShrink.Core/PlanCalculator.cs` clamp'i, uyarı `MainWindow.xaml.cs`.
