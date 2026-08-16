# VidShrink — Plan

## Ne yapıyor
Kullanıcı bir video atar, **hedef boyutu** (MB) söyler. Program minimum kalite kaybıyla o boyutun
altına indirir. İki mod:

1. **Otomatik (AI'sız, varsayılan)** — dahili hesap motoru bitrate/CRF/çözünürlük kararını verir.
2. **AI destekli** — program bir prompt üretir, kullanıcı bunu herhangi bir sohbet AI'sına yapıştırır,
   dönen JSON cevabı programa geri yapıştırır, program ayarları uygular. Gömülü API yok.

## Mimari
```
VidShrink.sln
├── VidShrink.Core        # ffprobe analiz, bitrate matematiği, encode profilleri, preset JSON şeması
├── VidShrink.Ffmpeg      # ffmpeg process sarmalayıcı, ilerleme parse (-progress pipe:1)
├── VidShrink.App         # WPF arayüz (teknesyum neon)
└── tools/ffmpeg          # yanına gömülü ffmpeg.exe + ffprobe.exe (bağımlılık aramasın)
```

## Otomatik motorun mantığı (AI olmadan da tam çalışır)
1. `ffprobe` → süre, çözünürlük, fps, kaynak bitrate, ses kodeği/bitrate, HDR mi, kaynak kodek.
2. **Ses bütçesi**: konuşma ağırlıklı → 96k Opus/AAC; müzik/stereo → 128k. Bütçeden düşülür.
3. **Video bütçesi**: `videoBitrate = (hedefMB * 8192 / süreSn) * 0.97 - sesBitrate`
   (%3 konteyner payı).
4. **Karar tablosu** (bits-per-pixel = bitrate / (w*h*fps)):
   | BPP | Karar |
   |---|---|
   | ≥ 0.10 | Çözünürlük korunur, CRF modu (kalite öncelikli) |
   | 0.05 – 0.10 | Çözünürlük korunur, 2-pass VBR |
   | 0.03 – 0.05 | Bir kademe düşür (1080→720), 2-pass |
   | < 0.03 | İki kademe düşür ve/veya fps 60→30, 2-pass |
5. **Kodek seçimi**: varsayılan `libx264 High` (uyumluluk). "Maks sıkıştırma" seçeneği → `libx265`
   veya `libsvtav1` (~%30-50 daha küçük, aynı kalite; daha yavaş). Donanım kodlayıcı (NVENC/QSV)
   sadece "hızlı mod"da — kalite/boyut oranı daha kötü, varsayılan değil.
6. **Sabit kalite kuralları**: `preset slow`, `tune film`, `-pix_fmt yuv420p`, `-movflags +faststart`,
   `-g fps*2`, ses `-c:a libopus` (mp4'te AAC-LC).
7. **Doğrulama döngüsü**: çıktı hedefi %5'ten fazla aşarsa bitrate düzeltilip tek seferlik yeniden
   encode (max 2 deneme).

## AI modu akışı
- Program `ffprobe` çıktısı + hedef boyut + kullanıcı niyetini (arşiv / paylaşım / sosyal medya)
  tek bir **kopyalanabilir prompt**a gömer. Prompt AI'dan katı JSON ister:
  ```json
  { "codec":"libx265", "mode":"2pass", "videoBitrateK":1450, "audioCodec":"libopus",
    "audioBitrateK":96, "width":1280, "height":720, "fps":30, "preset":"slow",
    "extraArgs":[], "reason":"..." }
  ```
- Kullanıcı cevabı yapıştırır → şema doğrulaması → **fark ekranı** (otomatik motorun kararı vs
  AI'ın kararı yan yana) → onay → encode.
- Bozuk/eksik JSON gelirse otomatik motorun değerlerine düşer, uyarı gösterir.
- Bu tasarım ileride gömülü AI eklenirse aynı JSON sözleşmesini kullanır — sadece taşıma katmanı değişir.

## Arayüz (tek pencere)
- Sürükle-bırak alanı → dosya bilgi kartı (süre, boyut, çözünürlük, kodek).
- Hedef boyut: kaydırıcı + kutu (ayrıca hazır düğmeler: 8 MB / 25 MB / 50 MB / 100 MB / %50).
- Mod anahtarı: **Otomatik** ⟷ **AI ayarı yapıştır**.
- "Ne yapacağını göster" paneli — uygulanacak tam ffmpeg komutu, şeffaf.
- İlerleme çubuğu + kalan süre + canlı tahmini çıktı boyutu.
- Bitince: öncesi/sonrası boyut, VMAF skoru (opsiyonel, `libvmaf` varsa), klasörde göster.

## Yol haritası
- **v0.1** — ffprobe analiz + otomatik motor + tek dosya encode + ilerleme. AI yok.
- **v0.2** — prompt üretici + JSON yapıştır + fark ekranı.
- **v0.3** — toplu kuyruk, sürükle-bırak çoklu dosya, profil kaydetme.
- **v0.4** — VMAF kalite raporu, x265/AV1 seçeneği, donanım hızlı mod.
- **v1.0** — kurulum paketi, ffmpeg gömülü, sağ tık menüsü ("VidShrink ile küçült").

## İsim önerisi (SEO)
**Öneri: `VidShrink`** — depo adı `vidshrink`.

Neden: "video shrink", "shrink video", "shrink video file size" yüksek hacimli ve niyeti tam
karşılayan aramalar; isim doğrudan bu sorguyu içeriyor, kısa, alan adı/paket adı olarak akılda kalıyor.

Depo tanımı (İngilizce, arama sinyalleri için):
> Shrink video file size to a target MB with minimal quality loss — free offline ffmpeg-based
> video compressor for Windows, with optional AI-assisted encoding settings.

Alternatifler: `TargetSize`, `SizeFit`, `VideoDietPro`, `ShrinkRay`.
