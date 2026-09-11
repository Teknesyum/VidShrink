[[netlestirme:006]]

# Netleştirme: Netlestirme 005 cevaplandi, simdi karar ver: VidShrink oynatici motoru icin on a

İşe başlamadan önce soruyu keskinleştir. Görüş verme, plan yazma, kod yazma.
Yalnız şunu döndür: soruda belirsiz kalan yerler, her biri için tek satırlık bir netleştirme sorusu, en fazla beş. Belirsizlik yoksa "net" yaz.

## Soru

Netlestirme 005 cevaplandi, simdi karar ver: VidShrink oynatici motoru icin on adayi AGPL uyumu, ikili boyutu, hiz, Avalonia 11/airspace, basiz test, hazir ozellik, bakim, risk sutunlariyla puanlayan tablo ve en mantikli secim; kendi oynaticimizi yazmanin Standart 30 + Gelismis 13 kapsamina maliyeti (satir, ajanli tur, insan is gunu, risk) libmpv ile karsilastirmali; 004'teki libmpv + SW render kararini koru ya da degistir, degisirse dalga planini yeniden ver.

## Elde olan olgular

Netleştirme 005'in beş sorusuna T0'ın cevapları (kullanıcı "bana sorma", "fable karar versin" dedi; cevaplar kullanıcının cümlelerine dayanıyor):
1. AGPL uyumu eleme kriteri değil, avantaj sütunu. Kullanıcı: "apgl 3.0 ile sorunsuz olması bi avantaj". GPL ikilili libmpv elenmez; uyum durumu tabloda derecelenir.
2. Ölçülmemiş değerler tahmin olarak kabul, tabloda "tahmin" diye işaretlenir. Karar ölçümü beklemez; seçilen yolun hız eşiğini (örn. 1080p SW render kare hızı) sen koy, 0. dalganın ilk adımı o ölçüm olur ve eşik tutmazsa hangi yola dönüleceğini de söyle.
3. Kapsam Standart 30 + Gelişmiş 13. Kullanıcı: "tüm özelliklerini istiyorum playerimde".
4. İki birim birden: bu projedeki ajanlı sözleşme turu sayısı (bir tur ≈ bir dalga parçası, yapıcı + denetçi + tam süit) ve tek insan geliştirici iş günü karşılığı.
5. 004 değişirse dalga planı da yeniden yazılır (docs/plan.md "Oynatici — GOM paritesi" bölümü); değişen dalgaları kapsam/önkoşul/kabul ile ver.

Olgular (netleştirme 005 ile aynı, kısaltılmadan):

Kullanıcının cümlesi (aynen): "diğer kütüphaneleri de listele en mantıklısı nedir görmek istiyorum apgl 3.0 ile sorunsuz olması bi avantaj bunun gibi detayları da içeren tabloyu istiyorum / ne kadar büyüklükte ve ne kadar hızda vb uyumları güçleri vb kendimizinkini yazmak ne kadar maliyetli bunu da analiz yap fable a danış"

Önceki karar (netleştirme 004, fable): libmpv, yazılım render (MPV_RENDER_API_TYPE_SW) ile PlayerView'a; ffmpeg Küçült/Dönüştür/karşılaştırma için kalır. Dalgalar 0 çekirdek (VidShrink.Player projesi, IPlaybackEngine, SW render, parite; kabul arama ≤150 ms, avsync ≤40 ms, başsız kare), 1 günlük denetim, 2 altyazı+ses parçası, 3 görüntü/pencere/liste, 4 gelişmiş, 5 ortak çekirdek (DecoderPipe+NAudio trash'e), 6 dosya ilişkilendirme. Standart 30, Gelişmiş 13, Alınmayacak 6 özellik.

Proje: .NET 8, Avalonia 11.3.20 (Avalonia 12 değil), AGPL-3.0-or-later, Windows birincil, macOS ikincil. Testler Avalonia.Headless ile gerçek olaylarla, bir kısmı gerçek ffmpeg ile; CI GitHub Actions. ffmpeg bugün Gyan GPL derlemesi, ayrı süreç olarak çağrılıyor.

Bugünkü oynatıcı kodu: 26 dosya, 6.683 satır üretim kodu (DecoderPipe 571, PlayerView 394+41, PanelHost 930, ComparisonPanel 846+180, ComparisonSurface 442, PipeComparisonFrameSource 432, SegmentEncoder 352, ControlStrip 338, ZoomGesture 252, PlayerInputMap 234, HoverZone 214, AudioSink 87, PreviewAudio 125, PlaybackClock 71 ölü kod, FrameRing 116, FramePool 73, ...). Oynatıcı testleri 3.097 satır. Bu kod bugün yalnız: oynat/duraklat, arama, teker arama, zoom/pan, tam ekran, 3 satır sağ tık, karşılaştırma paneli yapıyor. Ses, hız, kare kare, altyazı, ses parçası, ekran görüntüsü yok. Sınırlar: video borusu ffmpeg -re sabit (hız = süreç yeniden başlatma), uzak aramada yeni süreç ~105 ms, ses ayrı ffmpeg + NAudio WaveOutEvent 80 ms, A/V ortak saat yok.

Web karşılaştırması (sonnet ajanı, 11 Eylül 2026, kaynaklı; "tahmin" işaretliler tahmin):

1. libmpv + kendi P/Invoke: mpv GPLv2+ / LGPLv2.1+ karışık; -Dgpl=false ile LGPL derlenebilir ama hazır LGPL Windows ikilisi yok (shinchiro issue #586 çözümsüz), hazır ikili GPL. GPLv2-or-later + AGPLv3: or-later sayesinde GPLv3 olarak birleşebilir (FSF görüşü, hukuki kesinlik yok). Windows x64 shinchiro 20260903: dev 7z 31,4 MB, runtime 7z 33,8 MB; açılmış libmpv-2.dll boyutu doğrulanamadı. macOS: Homebrew -Dlibmpv=true ile libmpv.dylib; app bundle'a paketleme resmi değil. Render: OpenGL (Windows'ta ANGLE gerekir) veya sw. SW başsızda çalışır; OpenGL Avalonia.Headless'ta çalışmaz. hwdec zengin (d3d11va, nvdec, videotoolbox); SW render'da kare CPU'ya kopyalanır. Hazır: speed + perde koruma (scaletempo2 varsayılan), frame-step/frame-back-step, ab-loop-a/b, libass ile srt/ass/vtt, aid/sid, audio-delay/sub-delay, screenshot/screenshot-raw, deinterlace, video-rotate/zoom, brightness/contrast/saturation/gamma/hue property, codec/bitrate property. Yerleşik ekolayzer yok (af=lavfi superequalizer ile kurulur). mpv 36,9k yıldız, v0.41.0 (2025-12-21), çok aktif. SW render hızı: "extremely simple (but slow)", tek iş parçacığı, 4K'da yetişmeyebilir (render.h belgesi); ölçüm yok.

2. libmpv + HanumanInstitute.LibMpv(.Avalonia) 0.10.1: sarmalayıcı MIT; repo mysteryx93/LibMpv-OpenGL, 56 yıldız, son push 2026-07-10, tek geliştirici, 1.0 öncesi. NuGet açıklaması "Avalonia 12 implementation" diyor (bizim sürüm 11.3.20 — uyum doğrulanmadı). OpenGL/ANGLE + SW yedek, airspace yok. Başsızda SW yolunun çalıştığı doğrulanamadı. Native ikili madde 1 ile aynı.

3. LibVLCSharp 3.10.1 + LibVLCSharp.Avalonia: LGPL-2.1+ (AGPL ile sorunsuz), 1.809 yıldız, son push 2026-09-08, VideoLAN resmi. VideoLAN.LibVLC.Windows 3.0.23.1 NuGet paketi 128,06 MB. Resmi Avalonia VideoView yüzen native pencere açıyor: airspace sorunu var, UserControl içine konamıyor; topluluk fork'ları NativeControlHost ile uğraşıyor. libvlc_video_set_callbacks ile bellek kopyası yolu da var (araştırmada ayrıca ölçülmedi). Başsızda native pencere yolu muhtemelen çalışmaz. Hazır: hız, altyazı, ses parçası, gecikmeler, ekran görüntüsü, deinterlace, döndür/zoom, yerleşik ekolayzer (libvlc_audio_equalizer_*); A-B ve kare adımı ince sarmalama ister (geri kare adımı libvlc'de zayıf — tahmin).

4. FFmpeg.AutoGen (MIT, 1.608 yıldız, v8.0.0.1 2026-03-14) veya Sdcb.FFmpeg (LGPL-3.0, 2025-03'ten beri durgun; runtime paketi 53,64 MB) ile süreç içi kendi oynatıcımız: airspace yok, başsız test en uygun, hwaccel elle. Hiçbir oynatıcı özelliği hazır değil: A/V saat, ses çıkışı (NAudio/miniaudio), hız+perde (atempo/rubberband elle), kare geri adımı, libass P/Invoke ile altyazı render, ses parçası, ekolayzer, filtre grafikleri — hepsi elle. Araştırmanın tahmini: GOM kapsamı aylar.

5. Bugünkü yol (ffmpeg ayrı süreç + boru), genişletme: lisans değişmez, airspace yok, başsız test en temiz. Canlı ayarların çoğu (ses parçası, altyazı aç/kapa, hız, eq, filtreler) süreç yeniden başlatma ister; altyazı yalnız burn-in filtresiyle; tavanı düşük.

6. Flyleaf (LGPL-3.0, v3.11.3 2026-08-21, 906 yıldız): yalnız Windows (WPF/WinUI/WinForms), Avalonia yok, D3D11 — elendi.
7. Windows Media Foundation (Vortice MIT): yalnız Windows, altyazı/A-B/ekolayzer elle COM — elendi.
8. GStreamer + gstreamer-sharp (LGPL-2.1, 2021'den beri bakımsız, 33 yıldız) — elendi.
9. Avalonia Accelerate MediaPlayerControl (Avalonia.Controls.MediaPlayer 12.0.2): ticari Pro/Enterprise lisans (€89/yıl'dan), Avalonia 12, AGPL ile gerilimli, alt mekanizma belgelenmemiş — elendi.
10. SDL2/SDL3 (zlib) + ffmpeg: hazır .NET/Avalonia köprüsü yok, native pencere airspace, oynatıcı mantığı elle — elendi.

Açık teknik sorular: SW render'ın 1080p/4K'da gerçek kare hızı bu makinede ölçülmedi. libmpv render API'sini Avalonia 11'in OpenGlControlBase'i ile (Windows'ta ANGLE üzerinden) bağlamak mümkün, ama başsız testte çalışmaz — GPU yolu + başsızda SW yolu ikili tasarımı bir seçenek.
