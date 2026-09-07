# Danışma 001 — GitHub depo açıklaması

**Tarih:** 7 Eylül 2026 · **Danışılan:** fable (general-purpose, model fable)
**Maliyet:** 53.676 token, 20 sn · **Araç çağrısı:** 0

## Gönderilen istem (tam metin)

Kod yazma, dosya okuma, araç çağırma. Yalnız aşağıdaki olgulara bakıp metin yaz.

### Görev

VidShrink'in GitHub depo açıklamasını (About alanı, tek satır, 350 karakter sınırı) yeniden yaz. Şu an "Shrink any video to a target file size. .NET 8 + Avalonia + ffmpeg, offline." diyor ve bu ürünü eksik anlatıyor: proje aynı zamanda video oynatıcı ve dönüştürücü.

Açıklama **İngilizce** olacak. İki-üç aday ver, hangisini neden önerdiğini söyle. Ayrıca önerdiğin etiket (GitHub topic) eklemesi/çıkarması varsa yaz.

**Kural:** yalnız aşağıda "sevk edilen" diye yazılan özellikler açıklamaya girebilir. "Ekran kaydedici" ASLA girmez — sevk edilmiyor.

Cevabın Türkçe olsun (aday açıklama metinleri İngilizce, gerekçeler Türkçe).

### Olgular (7 Eylül 2026, sürüm 0.3.0, kaynaktan doğrulanmış)

#### Sevk edilen sekmeler
- **Oynatıcı** — en soldaki sekme. Video oynatır, klavye kısayolları var.
- **Küçült** — hedef dosya boyutuna sıkıştırır. Varsayılan açılış sekmesi.
- **Dönüştür** — kapsayıcı (MP4/MKV/WebM/MOV/AVI/MP3/M4A/WAV), video kodeği (H.264/H.265/VP9/AV1/copy), kalite modu (CRF ya da sabit bit hızı), çözünürlük, kare hızı, ses kodeği (AAC/Opus/MP3/PCM/copy/drop). Yani aynı zamanda ses çıkarıcı/dönüştürücü.
- Ayarlar, Gelişmiş, Hakkında.

#### Motor
- .NET 8 + Avalonia + ffmpeg. Çevrimdışı çalışır, dosya buluta gitmez.
- Kodekler: SVT-AV1 (varsayılan tercih), H.265/x265, H.264/x264. Ses: Opus, AAC.
- Hedef boyutu tutturmak için uyarlamalı kalibrasyon yoklaması: kısa örnek kodlamalar koşup bit hızı/CRF tahminini düzeltiyor.
- Kalite VMAF ile ölçülüyor; depo kendi ölçüm aracıyla sayılarını üretiyor.
- Önizleme paneli: işlenmiş ve orijinal kareyi yan yana gösteriyor.
- Windows kabuk entegrasyonu: sağ tık menüsü (hızlı hedefler 100/250/500/1024/2048 MB) ve "Birlikte aç" listesinde görünmek için ProgID kaydı.
- Çapraz platform hedefi var (Avalonia), ama sevk edilen ve ölçülen platform Windows.

#### Sevk EDİLMEYEN
- Ekran kaydedici yok. Kaynakta gdigrab / ScreenCapture sıfır eşleşme.

#### Mevcut etiketler
av1, avalonia, cross-platform, dotnet, ffmpeg, h265, offline-tool, opus, reduce-video-size, svt-av1, video-compression, video-compressor, video-encoding, vmaf, windows

---

## Dönen cevap (tam metin)

### Aday açıklamalar

**1 — önerdiğim**
`Play, convert, and shrink videos to an exact target size. Offline, adaptive calibration, VMAF-measured. .NET 8 + Avalonia + ffmpeg, Windows.`
(143 karakter)

**2 — kısa**
`Video player, converter, and target-size compressor. Offline, .NET 8 + Avalonia + ffmpeg.`
(88 karakter)

**3 — kodek vurgulu**
`Shrink videos to a target size with SVT-AV1/H.265/H.264, convert between MP4/MKV/WebM/MOV, extract audio, play them back. Offline. .NET 8 + Avalonia + ffmpeg.`
(158 karakter)

### Gerekçe

Aday 1'i öneriyorum. Üç sekmeyi (Oynatıcı, Dönüştür, Küçült) tek fiil dizisiyle veriyor; sıra ürünün ayırt edici yönünü ("exact target size") cümlenin sonuna koyarak vurguluyor. Motorun iki kanıtlanabilir farkını da anıyor: uyarlamalı kalibrasyon ve VMAF ölçümü — depoda ölçüm aracı ve sayılar var, iddia boş değil. "Windows" sevk edilen platformu dürüstçe söylüyor; "cross-platform" etiketi hedefi taşımaya devam eder.

Aday 2 en güvenli ama ürünü rakiplerinden ayırmıyor. Aday 3 arama görünürlüğü için kodek ve kapsayıcı adlarını satıra taşıyor, ama okunması ağır; etiketler zaten bu işi görüyor.

Hiçbir adayda ekran kaydedici geçmiyor.

### Etiket önerileri

Ekle: `video-converter`, `video-player`, `audio-extraction`, `x265`, `x264`, `avalonia-ui`, `shell-extension`

Çıkar: yok. `cross-platform` kalabilir — Avalonia hedefi gerçek, yalnız sevk edilen platform Windows; açıklama satırı bunu netleştiriyor.

---

## T0 kararı

Aday 1 uygulandı. Etiketlerden `avalonia-ui` uygulanmadı — mevcut `avalonia` ile aynı şeyi
söylüyor. Kalan altısı eklendi.
