[[netlestirme:004]]

# Netleştirme: Karar ver: VidShrink oynatıcısı için (1) rakip özelliklerinin Standart / Gelişmi

İşe başlamadan önce soruyu keskinleştir. Görüş verme, plan yazma, kod yazma.
Yalnız şunu döndür: soruda belirsiz kalan yerler, her biri için tek satırlık bir netleştirme sorusu, en fazla beş. Belirsizlik yoksa "net" yaz.

## Soru

Karar ver: VidShrink oynatıcısı için (1) rakip özelliklerinin Standart / Gelişmiş / Alınmayacak sınıflandırması, her biri tek cümle gerekçeyle; (2) mimari temel seçimi ve özelliklerin dalgalara bölünmesi, her dalga için kapsam, önkoşul, kabul ölçütü; (3) Gelişmiş başlığının arayüzdeki yeri; (4) kısayol şeması: GOM varsayılanları mı, mevcut teker/tık haritasıyla çakışmalar nasıl çözülür.

## Elde olan olgular

Netleştirme 003'ün beş sorusuna T0'ın cevapları (kullanıcının cümlelerine dayanarak; kullanıcı "bana sorma", "fable karar versin" dedi):
1. Standart'ın ölçütü günlük medya oynatıcı olmak. Kullanıcı: "tüm özelliklerini istiyorum playerimde", "daha oynatıcı olarak bile kullanamadım vidshrink i".
2. Ana hedef PlayerView (Oynatıcı sekmesi). Ortak çekirdek gerekiyorsa sen karar ver; karşılaştırma panelinin çekirdeğe bağlanması ayrı ve sonraki bir dalga olabilir.
3. Yeniden yazım kabul edilebilir. İki-boru + saat mi, tek süreç mi, başka bir yol mu (örn. libmpv/LibVLCSharp gömme) — gerekçesiyle sen seç. Kısıt: ffmpeg zaten paketle geliyor; Windows birincil, macOS ikincil.
4. Kabul: dotnet test tam yeşil + kanıt dosyası; hız doğruluğu, arama gecikmesi, A/V kayması gibi sayılar test içinde ölçülüp eşikle pimlenir (mevcut örnek: arama gecikmesi 150 ms eşiği). Bench gerekmez.
5. 43 dil zorunlu: LocalizationTests bir anahtar herhangi bir dilde eksikse kırmızı döner. Çeviriler alt ajana yaptırılır, maliyeti kabul.

Olgular: bkz. netleştirme 003 (rakip envanteri ve mevcut envanter özeti).

Özet (netleştirme 003'ten):
Proje: .NET 8 + Avalonia + ffmpeg (ayrı süreç, rawvideo BGRA borusu). Renk yalnız palet, ölçü yalnız Theme.axaml belirteçleri. Uygulamanın asıl işi hedef boyuta video sıkıştırma; üst barda Oynatıcı/Küçült/Dönüştür/Ayarlar sekmeleri.
Rakip envanteri (GOM, PotPlayer, VLC, MPC-HC/BE, mpv, KMPlayer), 11 kategori: oynatma denetimi (hız, kare kare, A-B tekrar, atlama adımları, kaldığı yerden devam), arama (önizleme küçük resmi, bölüm işaretleri, yer imleri), görüntü (en-boy, kırpma, zoom/pan, döndür/aynala, parlaklık/kontrast/doygunluk/ton, keskinlik/deinterlace, tam ekran, her zaman üstte, mini mod), ses (%100 üstü güçlendirme, ses parçası seçimi, ses gecikmesi, ekolayzer, normalleştirme, sessiz), altyazı (dış srt/ass/vtt, gömülü parça, gecikme, boyut/renk/konum, kodlama, otomatik bulma/indirme), çalma listesi (klasörde sonraki, sürükle-bırak, karıştır/tekrar), ekran görüntüsü ve klip/GIF dışa aktarma, bilgi paneli (codec/bitrate/çözünürlük/fps), fare (teker ses/arama, çift tık tam ekran, sağ tık), klavye (GOM ~120 varsayılan kısayol: Boşluk oynat, C/X/Z hız, F kare, [ ] / A-B, M sessiz, A ses parçası, S altyazı, Ctrl+E ekran görüntüsü, Ctrl+F1 bilgi, Ctrl+A her zaman üstte, Enter tam ekran, PgUp/PgDn dosya gezme, N/B yer imi, oklar ses/arama), dosya ilişkilendirme, son açılanlar, URL açma. GOM'a özgü: AB atlama, bitince bilgisayarı kapat, TTS, 360° video, DVD menüsü.
VidShrink oynatıcısı bugün: VAR — oynat/duraklat, arama çubuğu, teker ile arama (1/10/60/300 sn adımlar, Ctrl/Shift), Alt+teker zoom/pan, tam ekran (orta tık), sağ tık menüsü (3 satır), takılma tespiti, karşılaştırma (orijinal/işlenmiş yan yana) paneli, segment geçişi. KISMİ — klavye (yalnız Space/Menu/Escape), bilgi (yalnız konum/durum/zoom), sürükle-bırak (ana pencerede), klip (yalnız geçici önizleme). YOK — ses seviyesi, sessiz, hız, kare kare, altyazı, ses parçası seçimi, ekran görüntüsü, son açılanlar, kaldığı yerden devam.
Mimari sınırlar: video borusu ffmpeg `-re` sabit (hız değişimi süreç yeniden başlatma ister); uzak aramada yeni ffmpeg süreci (~105 ms), yakın aramada 128 MB anahtar kare önbelleği; ses ayrı ffmpeg süreci (PCM s16le 48k stereo) + NAudio WaveOutEvent (80 ms gecikme); A/V senkronu ortak saatle değil, iki borunun aynı noktaya aranmasıyla; PlaybackClock (Rate destekli) tanımlı ama kullanılmıyor; üç ayrı oynatıcı yolu (PlayerView+DecoderPipe, PanelHost+PipeComparisonFrameSource hstack, PreviewAudio) ortak çekirdeksiz. Testler gerçek Avalonia + gerçek olaylarla, bir kısmı gerçek ffmpeg ile.
