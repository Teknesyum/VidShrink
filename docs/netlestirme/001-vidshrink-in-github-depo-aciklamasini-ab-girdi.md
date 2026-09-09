[[netlestirme:001]]

# Netleştirme: VidShrink'in GitHub depo aciklamasini (About alani, tek satir, 350 karakter sini

İşe başlamadan önce soruyu keskinleştir. Görüş verme, plan yazma, kod yazma.
Yalnız şunu döndür: soruda belirsiz kalan yerler, her biri için tek satırlık bir netleştirme sorusu, en fazla beş. Belirsizlik yoksa "net" yaz.

## Soru

VidShrink'in GitHub depo aciklamasini (About alani, tek satir, 350 karakter siniri) yeniden yaz. Su an 'Shrink any video to a target file size' diyor ve bu urunu eksik anlatiyor: proje ayni zamanda video oynatici ve donusturucu. Ingilizce yaz. Iki-uc aday ver ve hangisini neden onerdigini soyle. Kurallar: yalniz olgu dosyasindaki sevk edilen ozellikler girebilir; 'ekran kaydedici' ASLA girmez, cunku sevk edilmiyor. Ayrica onerdigin etiket (topic) eklemesi/cikarmasi varsa yaz.

## Elde olan olgular

# VidShrink — GitHub depo aciklamasi icin olgular (7 Eylul 2026, surum 0.3.0)

Bunlar olculmus, kaynaktan dogrulanmis olgulardir. Uydurma ozellik ekleme.

## Bugun gercekten sevk edilen sekmeler (src/VidShrink.App/Locales/en/main.json)

- **Oynatici (player)** — en soldaki sekme. Video oynatir, kisayollari var.
- **Kucult (shrink)** — hedef dosya boyutuna sikistirir. Varsayilan acilis sekmesi.
- **Donustur (convert)** — kapsayici (MP4/MKV/WebM/MOV/AVI/MP3/M4A/WAV), video kodegi
  (H.264/H.265/VP9/AV1/copy), kalite modu (CRF ya da sabit bit hizi), cozunurluk,
  kare hizi, ses kodegi (AAC/Opus/MP3/PCM/copy/drop). Yani ayni zamanda ses
  cikarici/donusturucu.
- **Ayarlar (settings)**, **Gelismis (advanced)**, **Hakkinda (about)**.

## Motor

- .NET 8 + Avalonia + ffmpeg. Cevrimdisi calisir, dosya buluta gitmez.
- Kodekler: SVT-AV1 (varsayilan tercih), H.265/x265, H.264/x264. Ses: Opus, AAC.
- Hedef boyutu tutturmak icin uyarlamali kalibrasyon yoklamasi (CalibrationProbe):
  kisa ornek kodlamalar kosup bit hizi/CRF tahminini duzeltiyor.
- Kalite VMAF ile olculuyor; depo `tools/VidShrink.Bench` ile kendi sayilarini uretiyor.
- Onizleme paneli: islenmis ve orijinal kareyi yan yana gosteriyor.
- Windows kabuk entegrasyonu: sag tik menusu (hizli hedefler 100/250/500/1024/2048 MB)
  ve "Birlikte ac" listesinde gorunmek icin ProgID kaydi.
- Capraz platform hedefi var (Avalonia), ama sevk edilen ve olculen platform Windows.

## SEVK EDILMEYEN — aciklamaya girmez

- **Ekran kaydedici YOK.** Kaynakta `gdigrab`, `ScreenCapture`, ekran kaydi kodu
  sifir eslesme veriyor. Kullanici bunu hedef olarak soyledi, urun olarak degil.

## Mevcut depo aciklamasi

"Shrink any video to a target file size. .NET 8 + Avalonia + ffmpeg, offline."

## Mevcut depo etiketleri

av1, avalonia, cross-platform, dotnet, ffmpeg, h265, offline-tool, opus,
reduce-video-size, svt-av1, video-compression, video-compressor, video-encoding,
vmaf, windows
