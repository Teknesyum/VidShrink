# Eksik Araçlar — Doğrulanmış Eksik Tablosu

13 Ekim 2026, doğrulama turu. Girdi `eksik-araclar-eksikler.md` (44 satır). Kaynak numaraları `eksik-araclar.md` içindeki Kaynaklar bölümüne bakar. Kod yalnız okundu; hiçbir şey derlenmedi ve koşulmadı. Başka kolun satırıyla örtüşmeler artık hücrede değil, Yinelenenler bölümünde durur.

| No | Eksik | Kimde Var | Bizde Durum | Fayda | İş | Öncelik | Karar | Kaynak |
|---|---|---|---|---|---|---|---|---|
| ARC-01 | Medya bilgisi görüntüleyici: bir dosyanın bütün akışlarını, etiketlerini ve bölümlerini tek pencerede gösterip metin olarak dışa veren araç. | MediaInfo, Shutter Encoder, LosslessCut | yarım: ffprobe çıktısı akış ve bölüm listesiyle okunuyor (src/VidShrink.Core/MediaInfo.cs:55, src/VidShrink.Ffmpeg/FfprobeClient.cs:54); Küçült'te sekiz alanlı özet panel (src/VidShrink.App/MainWindow.axaml:290, src/VidShrink.App/MainWindow.axaml.cs:3682) ve oynatıcıda bilgi katmanı (src/VidShrink.App/Playback/PlayerView.Window.cs:603, src/VidShrink.App/Playback/Keymap.cs:72) var; akış akış tam döküm ve dışa aktarma yok | Dosyanın neden oynamadığı ya da neden büyük olduğu başka program açmadan görülür; döküm destek isteğine yapıştırılır. | orta | 1 | gerekmez | [1][19][46] |
| ARC-02 | İki dosya kalite ölçer: seçilen kaynak ile çıktıyı VMAF, SSIM ve PSNR (görüntü benzerlik puanları) ile karşılaştırıp kare kare grafik veren araç. | FFMetrics, Shutter Encoder, Netflix vmaf | yarım: motor iki serbest dosya yolunu alıyor (src/VidShrink.Ffmpeg/QualityMeter.cs:179) ve iki dosyayı ölçen komut ölçüm aracında var (tools/VidShrink.Bench/Program.cs:83); ürünün komut satırı yalnız kendi çıktısını ölçüyor (src/VidShrink.Cli/CliApp.cs:531); arayüzde kaynak ile son çıktıyı gösteren karşılaştırma paneli (src/VidShrink.App/MainWindow.axaml:523, src/VidShrink.App/MainWindow.axaml.cs:3706) ve gerekçe satırında kalite puanı (src/VidShrink.App/MainWindow.Gerekce.cs:85) var; iki serbest dosyanın seçildiği ekran ve kare kare grafik yok | Başka programla sıkıştırılmış dosyanın kaynağa ne kadar sadık kaldığı sayıyla görülür. | orta | 2 | sahip: Araçlar sekmesi açılsın mı, yoksa en yakın sekmeye mi konsun | [1][2][25][44][53] |
| ARC-03 | Bit hızı grafiği: dosya boyunca kare, saniye ya da GOP (iki anahtar kare arasındaki kare grubu) başına bit hızını çizen araç. | FFBitrateViewer | yarım: oynatıcı bilgi katmanında yalnız tek bir bit hızı sayısı var (src/VidShrink.App/Playback/PlayerView.Window.cs:603, src/VidShrink.App/Playback/Keymap.cs:72) | Videonun hangi sahnede şiştiği ve yayın sınırını aşıp aşmadığı görülür. | orta | 3 | sahip: Araçlar sekmesi açılsın mı, yoksa en yakın sekmeye mi konsun | [26] |
| ARC-04 | Ses yüksekliği raporu: dosyanın LUFS (algılanan yükseklik birimi) ve True Peak (gerçek tepe) değerlerini ölçüp gösteren araç. | Shutter Encoder | yarım: yüksekliği sabit hedefe çeken tek geçişli süzgeç var (src/VidShrink.Core/StreamMapping.cs:357), kutusu Küçült'ün ses bölümünde (src/VidShrink.App/MainWindow.axaml:850, src/VidShrink.App/MainWindow.Izler.cs:88); ölçüm geçişi ve rapor yok (ebur128 kodda aramada çıkmadı) | Dosyanın yayın ya da platform sınırına uyup uymadığı dönüştürmeden önce bilinir. | küçük | 3 | gerekmez | [1][2][49] |
| ARC-05 | Denetim raporu: siyah kare, donuk kare, sessizlik ve kesme noktalarını zaman kodlu liste olarak veren araç. | Shutter Encoder, LosslessCut, PySceneDetect | yarım: sessizlik ve siyah kare taraması (src/VidShrink.Core/Editing/SilenceCut.cs:90, src/VidShrink.Core/Editing/SilenceCut.cs:94) ile sahne algılama (src/VidShrink.Ffmpeg/SceneDetector.cs:15) düzenleyicide kesim için koşuyor (src/VidShrink.App/Editing/EditorView.Sessizlik.cs:169, src/VidShrink.App/Editing/EditorView.Sahne.cs:40), donuk kare taraması kaydedicide (src/VidShrink.Core/IdleTrim.cs:38); sonuç zaman kodlu liste olarak verilmiyor | Teslimden önce dosyadaki boş ya da bozuk yerler tek listede görülür. | küçük | 2 | gerekmez | [1][2][40][46] |
| ARC-06 | Kare sağlaması: her karenin MD5 özetini (içerik parmak izi) çıkarıp iki dosyanın görüntüsünün birebir aynı olduğunu kanıtlayan araç. | Shutter Encoder | yok (framemd5 kodda aramada çıkmadı; en yakın: altyazı araması için dosya özeti, src/VidShrink.Core/Subtitles/MovieHash.cs:13) | Kayıpsız kesim ya da yeniden sarma sonrası görüntünün değişmediği kanıtlanır. | küçük | 3 | sahip: Araçlar sekmesi açılsın mı, yoksa en yakın sekmeye mi konsun | [2] |
| ARC-07 | Küçük resim tablosu: videodan eşit aralıklı kareleri zaman damgası ve dosya bilgisi başlığıyla tek resimde dizen araç. | vcsi, mtn | yok (tile süzgeci kodda aramada çıkmadı; en yakın: oynatıcının klip ve GIF dışa aktarma komutları, src/VidShrink.App/Playback/ToolsOptions.cs:30; tek kare kaydı, src/VidShrink.Player/MpvEngine.cs:530; düzenleyicinin küçük resim kuyruğu, src/VidShrink.Core/Editing/ThumbnailQueue.cs:254) | Videonun içeriği tek resimde arşivlenir ya da paylaşılır. | orta | 2 | gerekmez | [41][42][49] |
| ARC-08 | Toplu kare dışa aktarma: videodan her N saniyede, her sahne değişiminde ya da seçili aralıkta kareleri resim dosyaları olarak çıkaran araç. | Shutter Encoder, LosslessCut, HitPaw Univd, VideoProc Converter AI | yarım: yalnız tek kare kaydı ve kareyi panoya kopyalama var (src/VidShrink.App/Playback/Keymap.cs:73, src/VidShrink.Player/MpvEngine.cs:530) | Kapak, sunum ya da inceleme için yüzlerce kare tek işle alınır. | orta | 2 | gerekmez | [1][6][10][46] |
| ARC-09 | Resim dizisi girdisi: numaralı resim dosyalarını (zaman atlamalı çekim, render çıktısı) tek video kaynağı gibi açıp kodlama. | Shutter Encoder, Shotcut | yok (bırakma ve klasör taraması yalnız video uzantılarını alıyor, src/VidShrink.Core/WatchFolder.cs:74; resim yalnız çıktı olarak yazılıyor, src/VidShrink.Ffmpeg/EncodeRunner.cs:194) | Zaman atlamalı çekim ya da 3B render çıktısı başka araca gerek kalmadan videoya döner. | orta | 1 | gerekmez | [1][2][52] |
| ARC-10 | Slayt gösterisi yapıcı: fotoğrafları süre, geçiş ve müzikle videoya çeviren araç. | Kapwing, VEED | yok (düzenleyicide resim klibi, müzik izi ve geçiş kodda aramada çıkmadı; bırakma listesi yalnız video uzantıları, src/VidShrink.Core/WatchFolder.cs:74, src/VidShrink.Core/DroppedMedia.cs:24) | Tatil ya da ürün fotoğrafları tek işle paylaşılabilir videoya döner. | büyük | 2 | gerekmez | [15][16] |
| ARC-11 | Görüntü dönüştürücü: fotoğrafları toplu hâlde JPG, PNG, WebP, AVIF, HEIC gibi biçimler arasında çeviren ve yeniden boyutlandıran araç. | UniConverter, HitPaw Univd, Aiseesoft, Format Factory ve 2 rakip daha | yok (resim yalnız çıktı olarak üretiliyor: kare kaydı ve hareketli WebP, AVIF; src/VidShrink.Core/ConversionPlan.cs:34) | Telefonun HEIC fotoğrafları ya da ekran görüntüleri aynı programda toplu çevrilir. | orta | 2 | sahip: Araçlar sekmesi açılsın mı, yoksa en yakın sekmeye mi konsun | [1][2][4][5][9][10][12][15] |
| ARC-12 | GIF girdisi: hazır bir GIF'i açıp hedef boyuta küçültme ya da videoya çevirme. | HitPaw Univd, Kapwing, ezgif, UniConverter | yarım: GIF çıktı olarak üretiliyor (src/VidShrink.Core/ConversionPlan.cs:33); Gözat penceresinin uzantı listesinde GIF var (src/VidShrink.Core/ShellIntegration.cs:14, src/VidShrink.App/MainWindow.axaml.cs:3274) ama bırakma ve klasör taraması almıyor (src/VidShrink.Core/WatchFolder.cs:74); GIF'ten GIF'e hedef boyut yolu yok | Sohbet uygulamasının sınırına takılan GIF aynı programda küçülür ya da MP4'e döner. | orta | 1 | gerekmez | [5][10][15][54] |
| ARC-13 | Resimlerden GIF: bir dizi resmi ya da seçili kareleri hareketli GIF'e çeviren araç. | ezgif; istek ayrıca Shutter Encoder takipçisinde | yok (GIF yalnız videodan üretiliyor, src/VidShrink.Core/GifPalette.cs:5) | Ekran görüntülerinden adım adım anlatım GIF'i yapılır. | küçük | 3 | gerekmez | [54] |
| ARC-14 | Etiket düzenleyici: başlık, sanatçı, yıl, açıklama ve kapak resmi gibi etiketleri yeniden kodlamadan yazan araç. | UniConverter, Aiseesoft, LosslessCut, MKVToolNix | yarım: üst veri taşınıyor ya da topluca siliniyor (src/VidShrink.Core/ConversionPlan.cs:31, kutusu src/VidShrink.App/MainWindow.axaml:1357), iz adı aynen taşınıyor (src/VidShrink.Core/StreamMapping.cs:286), kapak resmi kaynaktan çıkarılıyor (src/VidShrink.Ffmpeg/EncodeRunner.cs:193); başlık, sanatçı, yıl ve kapak elle yazılamıyor | Dosya kitaplıkta ve oynatıcıda doğru adla ve doğru kapakla görünür. | orta | 1 | gerekmez | [4][12][13][22][46][50] |
| ARC-15 | Bölüm düzenleyici: var olan dosyada bölüm işaretlerini ekleyen, adlandıran, silen ve yeniden kodlamadan yazan araç. | MKVToolNix, LosslessCut | yarım: bölümler okunuyor (src/VidShrink.Core/MediaInfo.cs:61, src/VidShrink.Ffmpeg/FfprobeClient.cs:59) ve dönüştürmede taşınıyor (src/VidShrink.Core/StreamMapping.cs:302); kaydedici kendi çıktısına bölümü yeniden kodlamadan yazıyor (src/VidShrink.Core/RecorderChapters.cs:91, src/VidShrink.Core/RecorderChapters.cs:109); var olan dosyada elle ekleme, adlandırma ve silme yok | Uzun kayıt ya da film, oynatıcıda adlı bölümlerle gezilir. | orta | 2 | gerekmez | [22][46] |
| ARC-16 | Yeniden sararak onarma: dizini bozuk, süresi yanlış görünen ya da ileri sarılamayan dosyayı akışlarını kopyalayıp yeniden yazarak düzeltme. | Format Factory, Aiseesoft Video Repair | yarım: Dönüştür'de video ve ses için "Kopyala" seçeneği akışları yeniden kodlamadan yeni kaba yazıyor (src/VidShrink.App/MainWindow.axaml:1154, src/VidShrink.App/MainWindow.axaml:1304, src/VidShrink.Core/ConversionArguments.cs:122); bozuk dizini hoş gören okuma bayrakları (genpts, igndts, err_detect kodda aramada çıkmadı) ve tek tıkla onarma eylemi yok | Yarım inmiş ya da kaydı kesilmiş video yeniden oynar ve ileri sarılır. | orta | 2 | gerekmez | [9][12] |
| ARC-17 | Kesik MP4 kurtarma: kaydı yarıda kalmış, dizini (moov bölümü) yazılmamış MP4 ya da MOV dosyasını sağlam bir örnek dosyaya bakarak kurtarma. | untrunc | yok (en yakın: oynatma takılınca dosyayı yeniden açan kurtarıcı, src/VidShrink.App/Playback/OynatmaKurtarici.cs:24) | Kamera ya da telefon kapanınca bozulan çekim geri gelir. | büyük | 3 | sahip: untrunc ayrı ikili olarak (GPL-2.0) dağıtılsın mı | [27] |
| ARC-18 | Yinelenen video bulucu: bir klasördeki videoları görüntü parmak iziyle karşılaştırıp boyutu ya da biçimi farklı aynı videoları gruplayan araç. | Czkawka, Video Duplicate Finder | yok (en yakın: altyazı araması için dosya özeti, src/VidShrink.Core/Subtitles/MovieHash.cs:13) | Arşivde aynı videonun kopyaları bulunur, disk yeri açılır. | büyük | 2 | sahip: kendi sekmesini isteyen araç ürüne girsin mi | [23][24][55] |
| ARC-19 | Ses dosyası girdisi: MP3, FLAC, WAV gibi ses dosyalarını toplu hâlde başka ses biçimine çevirme. | UniConverter, HitPaw Univd, Format Factory, Kapwing | yarım: videodan ses çıkarma var (src/VidShrink.Core/ConversionPlan.cs:32) ve oynatıcı ses dosyasını açıyor (src/VidShrink.Core/ShellIntegration.cs:21, src/VidShrink.App/Playback/PlayerView.Ses.cs:18); dosya okuyucusu görüntüsü olmayan dosyayı reddediyor (src/VidShrink.Ffmpeg/FfprobeClient.cs:45) | Müzik ya da ses kaydı dosyaları aynı programda toplu çevrilir. | orta | 1 | gerekmez | [5][9][10][15] |
| ARC-20 | Sesten video: bir ses dosyasını kapak resmi ya da dalga biçimi canlandırmasıyla videoya çeviren araç. | Kapwing, Clipchamp | yok (showwaves kodda aramada çıkmadı; en yakın: düzenleyicinin dalga biçimi verisi, src/VidShrink.Core/Editing/AudioPeaks.cs:9) | Podcast ya da şarkı, video isteyen platformlara yüklenebilir. | orta | 2 | gerekmez | [15][17][49] |
| ARC-21 | Altyazı dosyası dönüştürme ve kaydırma: SRT, ASS ve VTT arasında çevirme, bütün satırları öne ya da arkaya kaydırma, karakter kodlamasını düzeltme. | Kapwing, VEED, Subtitle Edit, Aegisub | yarım: gecikme yalnız oynatma sırasında ayarlanıyor, dosyaya yazılmıyor (src/VidShrink.Player/MpvEngine.cs:389); dış altyazı dosyası çıktıya iz olarak ekleniyor (src/VidShrink.Core/StreamMapping.cs:86); karakter kodlaması yalnız yakma yolunda okunuyor (src/VidShrink.Core/SubtitleCharset.cs:35, src/VidShrink.Cli/CliApp.cs:197); altyazı dosyasını tek başına çeviren ya da kaydıran yol yok | Kayık ya da yanlış biçimdeki altyazı tek işle düzelir ve her oynatıcıda doğru çıkar. | orta | 1 | sahip: Araçlar sekmesi açılsın mı, yoksa en yakın sekmeye mi konsun | [15][16][20][21] |
| ARC-22 | Altyazı düzenleyici: altyazı satırlarını metin ve zaman olarak tek tek düzenleyen, dalga biçimi üstünde hizalayan tam araç. | Subtitle Edit, Aegisub, UniConverter, Kapwing ve 2 rakip daha | yarım: düzenleyicide yalnız yazı katmanı var, altyazı dosyası açılıp düzenlenemiyor (src/VidShrink.App/Editing/EditorView.Metin.cs:15) | Altyazı hatası başka program kurmadan düzeltilir. | büyük | 2 | sahip: kendi sekmesini isteyen araç ürüne girsin mi | [1][4][15][16][20][21] |
| ARC-23 | Altyazı çevirisi: var olan altyazı dosyasını başka bir dile makineyle çeviren araç. | Shutter Encoder, UniConverter, Kapwing, VEED | yok (en yakın: hazır altyazıyı OpenSubtitles'tan indirme, src/VidShrink.App/MainWindow.axaml.cs:1818) | Yabancı dildeki altyazı kullanıcının diline çevrilir. | orta | 2 | sahip: altyazı metnini çevrimiçi çeviri hizmetine ya da yerel dil modeline veren araç ürüne girsin mi | [2][5][15][16] |
| ARC-24 | Dosyada gürültü temizleme: hazır bir video ya da ses dosyasındaki uğultuyu ve fan sesini bastıran araç. | UniConverter, VideoProc Converter AI, HitPaw Univd, Movavi Video Suite ve 5 rakip daha | yarım: gürültü bastırma süzgeci yalnız kayıt sırasında bağlanıyor (src/VidShrink.Core/AudioCaptureArguments.cs:73, src/VidShrink.Core/AudioCaptureArguments.cs:272); Küçült'ün ses bölümünde yalnız yükseklik kutusu var (src/VidShrink.App/MainWindow.axaml:850); hazır dosyaya uygulanamıyor | Kötü mikrofonla çekilmiş videonun sesi yeniden çekmeden temizlenir. | küçük | 1 | gerekmez | [4][6][7][10][12][14][15][16][17] |
| ARC-25 | Vokal ve enstrüman ayırma: bir şarkıyı ya da videonun sesini vokal, davul, bas gibi ayrı dosyalara bölen araç. | Shutter Encoder, UniConverter, VideoProc Converter AI, HitPaw Univd ve 3 rakip daha | yok (en yakın: sesi tek dosya olarak çıkarma, src/VidShrink.Core/ConversionPlan.cs:32) | Videodan müzik silinir ya da karaoke için vokal ayrılır. | büyük | 2 | sahip: yapay zekâ araçları için yeni çalışma zamanı ve yüzlerce MB model indirmesi kabul edilsin mi | [1][2][4][6][8][10][12][15][32][33] |
| ARC-26 | Metinden sese: yazılan metni seçilen sesle okutup ses dosyası olarak veren araç. | UniConverter, CapCut, Kapwing, VEED ve 1 rakip daha | yok | Seslendirme mikrofon olmadan hazırlanır. | büyük | 3 | sahip: bulut hizmetine ya da yerel ses modeline dayanan araç ürüne girsin mi | [4][5][14][15][16][17] |
| ARC-27 | Yapay zekâ ile çözünürlük büyütme: düşük çözünürlüklü videoyu modelle 2 ya da 4 kat büyütüp netleştiren araç. | Shutter Encoder, UniConverter, VideoProc Converter AI, Topaz Video ve 6 rakip daha | yarım: yalnız klasik ölçekleme (lanczos) var, model yok (src/VidShrink.Core/ConversionArguments.cs:232) | Eski ya da küçük video büyük ekranda daha net görünür. | büyük | 2 | sahip: yapay zekâ araçları için yeni çalışma zamanı ve yüzlerce MB model indirmesi kabul edilsin mi | [1][2][4][5][6][8][11][12][15][18][28][29] |
| ARC-28 | Kare ara değerleme: videonun kare hızını ara kareler üreterek yükselten (örnek: 30'dan 60'a) ya da ağır çekimi akıcılaştıran araç. | UniConverter, VideoProc Converter AI, Aiseesoft, Topaz Video ve 5 rakip daha | yok (minterpolate kodda aramada çıkmadı; en yakın: Dönüştür'ün kare hızı kutusu, src/VidShrink.App/MainWindow.axaml:1260) | Düşük kare hızlı video daha akıcı oynar, ağır çekim takılmaz. | orta | 2 | gerekmez | [1][5][6][12][18][28][29][30][37] |
| ARC-29 | Arka plan silme: videodaki kişiyi arka plandan ayırıp arka planı saydam yapan ya da değiştiren araç. | UniConverter, Movavi Video Suite, CapCut, Kapwing ve 2 rakip daha | yok (en yakın: görüntü süzgeci seçenekleri, src/VidShrink.Core/VideoFilterChain.cs:59) | Yeşil perde olmadan sunum ya da ürün videosu hazırlanır. | büyük | 2 | sahip: yapay zekâ araçları için yeni çalışma zamanı ve yüzlerce MB model indirmesi kabul edilsin mi | [4][7][14][15][16][17][34] |
| ARC-30 | Kendiliğinden yeniden kadraj: yatay videoyu, konuyu izleyerek dikey ya da kare biçime kırpan araç. | UniConverter, HitPaw Univd, Movavi Video Suite, CapCut | yarım: yalnız siyah bant algılama var, konu izleme yok (src/VidShrink.Ffmpeg/CropProbe.cs:15) | Yatay çekim, konu kadrajdan çıkmadan dikey paylaşıma hazır olur. | büyük | 2 | sahip: yapay zekâ araçları için yeni çalışma zamanı ve yüzlerce MB model indirmesi kabul edilsin mi | [4][7][10][14] |
| ARC-31 | Yüz bulanıklaştırma: videodaki yüzleri kendiliğinden bulup izleyerek bulanıklaştıran araç. | Shutter Encoder, UniConverter, HitPaw Univd | yok (en yakın: görüntü süzgeci seçenekleri, src/VidShrink.Core/VideoFilterChain.cs:59) | Sokak çekimi ya da toplantı kaydı, kişileri gizleyerek paylaşılır. | büyük | 2 | sahip: yapay zekâ araçları için yeni çalışma zamanı ve yüzlerce MB model indirmesi kabul edilsin mi | [2][3][5][10] |
| ARC-32 | Renklendirme: siyah beyaz videoyu modelle renklendiren araç. | Shutter Encoder, Aiseesoft | yok | Eski aile ve arşiv görüntüleri renkli izlenir. | büyük | 3 | sahip: yapay zekâ araçları için yeni çalışma zamanı ve yüzlerce MB model indirmesi kabul edilsin mi | [2][12][35] |
| ARC-33 | Logo ve filigran silme: görüntüdeki sabit logoyu ya da tarih damgasını çevresine bakarak kapatan araç. | UniConverter, Format Factory, Aiseesoft | yok (delogo kodda aramada çıkmadı; en yakın: Küçült'ün gelişmiş görüntü süzgeçleri, src/VidShrink.Core/VideoFilterChain.cs:65, src/VidShrink.App/MainWindow.axaml:1030) | Köşedeki tarih damgası ya da kanal logosu görüntüden kalkar. | orta | 2 | sahip: başkasının logosunu ya da filigranını silmeye yarayan araç ürüne girsin mi | [4][5][9][12][49] |
| ARC-34 | 2B'den 3B'ye ve VR dönüştürme: düz videoyu yan yana 3B ya da VR gözlüğü biçimine çeviren araç. | UniConverter, HitPaw Univd, Aiseesoft | yok | Video, 3B televizyonda ya da VR gözlüğünde izlenir. | büyük | 3 | sahip: yapay zekâ araçları için yeni çalışma zamanı ve yüzlerce MB model indirmesi kabul edilsin mi | [4][10][12] |
| ARC-35 | Desenle toplu yeniden adlandırma: seçili dosyaları sıra numarası, tarih ya da metin değiştirme kuralıyla topluca yeniden adlandıran araç. | Shutter Encoder | yarım: yalnız çıktı dosyasının adı desenle kuruluyor, var olan dosyalar adlandırılamıyor (src/VidShrink.Core/AdlandirmaDeseni.cs:15) | Kameradan gelen dosyalar tek işle düzenli adlara kavuşur. | küçük | 3 | sahip: Araçlar sekmesi açılsın mı, yoksa en yakın sekmeye mi konsun | [1] |
| ARC-36 | Kitaplık düzenleyici: film ve dizi dosyalarını çevrimiçi veritabanından tanıyıp adlandıran, afiş ve bilgi dosyası yazan araç. | FileBot, tinyMediaManager | yok (en yakın: altyazı araması için dosya özeti, src/VidShrink.Core/Subtitles/MovieHash.cs:13) | Film arşivi Plex, Kodi ve Jellyfin'de doğru adla ve afişle görünür. | büyük | 3 | sahip: çevrimiçi veritabanına bağlanan kitaplık aracı ürüne girsin mi | [47][48] |
| ARC-37 | Aygıta aktarma: dönüştürülen dosyayı kabloyla bağlı telefona ya da tablete doğrudan gönderen araç. | UniConverter | yok (en yakın: dosyayı bağlantı olarak paylaşma, src/VidShrink.Core/Share/UploadContent.cs:71) | Dosya, elle kopyalamadan telefona geçer. | büyük | 3 | sahip: aygıt protokolü için yeni bağımlılık alınsın mı | [4] |
| ARC-38 | Belge araçları: PDF birleştirme ve PDF'i resme çevirme gibi video dışı işler. | Format Factory | yok | Küçük belge işleri aynı programda yapılır. | orta | 3 | sahip: kendi sekmesini isteyen araç ürüne girsin mi | [9] |
| ARC-39 | Üretken yapay zekâ araçları: metinden video, avatar ve göz teması düzeltme gibi içerik üreten araçlar. | CapCut, VEED, Any Video Converter | yok | İçerik üreticisi çekim yapmadan video hazırlar. | büyük | 3 | sahip: bulut hizmetine ve krediye dayanan üretken araç ürüne girsin mi | [8][14][16] |
| ARC-40 | URL'den indirme: yapıştırılan bağlantıdaki videoyu dosya olarak indiren araç. | Shutter Encoder, VideoProc Converter AI, Any Video Converter, Format Factory | yok (oynatıcı bağlantıyı yalnız oynatıyor, src/VidShrink.App/Playback/ToolsOptions.cs:33) | Bağlantıdaki video dosya olarak elde edilir. | orta | 3 | kapalı: K1 | [1][6][8][9] |
| ARC-41 | Konuşmadan metne: videodaki konuşmayı yazıya döküp altyazı dosyası üreten araç. | Shutter Encoder, UniConverter, Movavi Video Suite, HitPaw Univd ve 5 rakip daha | yok (hazır altyazı yalnız indiriliyor, src/VidShrink.App/MainWindow.axaml.cs:1818) | Altyazısız videoya altyazı çıkar. | büyük | 3 | kapalı: K5 | [1][2][4][5][7][8][10][14][15][16][17] |
| ARC-42 | Stabilizasyon: titrek çekimi yazılımla sabitleyen araç. | VideoProc Converter AI, Any Video Converter, HitPaw Univd, Kapwing ve 1 rakip daha | yok | El kamerası çekimi daha sakin görünür. | orta | 3 | kapalı: K8 | [6][8][10][15][18] |
| ARC-43 | Filigran ekleme: videonun üstüne logo ya da yazı basan araç. | UniConverter, Any Video Converter, Aiseesoft, HitPaw Univd | yarım: düzenleyicide yazı katmanı var, logo basma yok (src/VidShrink.App/Editing/EditorView.Metin.cs:15) | Paylaşılan video sahibinin işaretini taşır. | küçük | 3 | kapalı: K8 | [4][8][10][12] |
| ARC-44 | Disk yazma ve kopyalama: DVD, Blu-ray ve müzik CD'sine yazan ya da diskten dosyaya kopyalayan araç. | Shutter Encoder, UniConverter, Movavi Video Suite, Format Factory ve 2 rakip daha | yarım: yalnız DVD başlık taraması var (src/VidShrink.Core/SourceTitles.cs:35) | Eski disk arşivi dosyaya döner ya da video diske yazılır. | büyük | 3 | kapalı: DVD ve Blu-ray | [1][4][6][7][8][9] |

## Düzeltmeler

Karar sütununda 39 satırın hepsi `sahip` idi. Kural: yeni sekme, yeni bağımlılık, yeni ikili dosya, yüzlerce MB indirme ya da varsayılan davranış değişikliği `sahip`; var olan sekmeye yeni bağımlılık olmadan giren iş `gerekmez`. Sonuç: 16 satır `gerekmez` oldu, 23 satır `sahip` kaldı ama cümlesi sadeleşti, 5 kapalı satıra dokunulmadı.

Çizgi şöyle çekildi: sekmenin bugün ürettiği çıktıyı (video, ses, GIF, bilgi paneli, tarama sonucu) yeni bir kaynaktan ya da yeni bir düğmeyle üreten iş `gerekmez`. Sekmenin bugün hiç üretmediği bir çıktıyı (altyazı dosyası, fotoğraf, ölçüm grafiği, yeniden adlandırma) üreten ve kendi ekranını isteyen iş Araçlar sorusuna bağlandı.

Öncelik: tek satır değişti (`ARC-02`). Öbür yedi öncelik 1 satırı "beş işten birinin doğal uzantısı" okumasından geçti.

| No | Ne Değişti | Kanıt |
|---|---|---|
| `ARC-01` | Karar `sahip` iken `gerekmez` oldu: döküm, var olan iki bilgi yüzeyinin uzantısı. Bizde Durum motorun ve iki arayüz karşılığının satırlarıyla genişledi. | src/VidShrink.Core/MediaInfo.cs:55; src/VidShrink.Ffmpeg/FfprobeClient.cs:54; src/VidShrink.App/MainWindow.axaml:290; src/VidShrink.App/Playback/PlayerView.Window.cs:603. Rakip üç kaynakta görüldü: [1], [19], [46] |
| `ARC-02` | Öncelik 1 iken 2 oldu. Karar ortak Araçlar cümlesine çekildi. Bizde Durum genişledi: iki dosyayı ölçen komut ölçüm aracında zaten var, arayüzde karşılaştırma paneli ve kalite puanı var. | Rakip: [25] FFMetrics ve [1] Shutter Encoder görüldü; Shutter Encoder belgesi iki dosya istediğini açıkça yazmıyor; [44] uygulama değil kitaplık; oy 5 ([53], gh ile okundu). Üç yaygın rakip koşulu da oy koşulu da tutmuyor. Kod: src/VidShrink.Ffmpeg/QualityMeter.cs:179; tools/VidShrink.Bench/Program.cs:83; src/VidShrink.App/MainWindow.axaml:523; src/VidShrink.App/MainWindow.Gerekce.cs:85 |
| `ARC-03` | Karar ortak Araçlar cümlesine çekildi. Kanıta bilgi katmanını kuran satır eklendi. | src/VidShrink.App/Playback/PlayerView.Window.cs:603 |
| `ARC-04` | Karar `gerekmez` oldu: ölçüm, Küçült'ün ses bölümündeki yükseklik kutusunun yanına girer. Bizde Durum netleşti: süzgeç tek geçişli, ölçüm geçişi yok. | src/VidShrink.Core/StreamMapping.cs:357; src/VidShrink.App/MainWindow.axaml:850 |
| `ARC-05` | Karar `gerekmez` oldu: taramalar düzenleyicide zaten koşuyor, eksik olan sonucu liste olarak vermek. Kanıta taramanın arayüzde çağrıldığı satırlar eklendi. | src/VidShrink.App/Editing/EditorView.Sessizlik.cs:169; src/VidShrink.App/Editing/EditorView.Sahne.cs:40 |
| `ARC-06` | Karar ortak Araçlar cümlesine çekildi. | Rakip görüldü: [1] belgesinde FrameMD5 işlevi var |
| `ARC-07` | Karar `gerekmez` oldu: oynatıcının araç komutlarına (klip, GIF) üçüncü bir dışa aktarma olarak girer. En yakın kod buna göre yazıldı. | src/VidShrink.App/Playback/ToolsOptions.cs:30; src/VidShrink.App/Playback/PlayerView.Tools.cs:89 |
| `ARC-08` | Karar `gerekmez` oldu: aynı araç komutlarına girer. | src/VidShrink.App/Playback/ToolsOptions.cs:30; rakip iki kaynakta görüldü: [1], [46] |
| `ARC-09` | Karar `gerekmez` oldu: Dönüştür'e yeni kaynak türü. Kimde Var'a ikinci görülen rakip Shotcut eklendi. | [1] belgesinde resimlerden video; https://shotcut.org/features/ sayfasında resim dizisi desteği; oy 47, istek açık ([52], gh ile okundu) |
| `ARC-10` | Karar `gerekmez` oldu: iş düzenleyicinin içinde kalır. Bizde Durum'a düzenleyicide resim klibi, müzik izi ve geçişin aramada çıkmadığı yazıldı. | src/VidShrink.Core/Editing altında StillImage, ImageClip, MusicTrack, xfade, Transition aramaları boş döndü |
| `ARC-11` | Karar ortak Araçlar cümlesine çekildi. | Eski cümle zaten "yeni sekme ya da Dönüştür'e resim girdisi" diyordu |
| `ARC-12` | Karar `gerekmez` oldu. Kimde Var'a üçüncü görülen rakip ezgif eklendi. Bizde Durum düzeltildi: GIF, Gözat'ın uzantı listesinde zaten var; yalnız bırakma ve klasör taraması almıyor. | src/VidShrink.Core/ShellIntegration.cs:14; src/VidShrink.App/MainWindow.axaml.cs:3274; src/VidShrink.Core/WatchFolder.cs:74. Rakip: [15], [10] ve https://ezgif.com/ (GIF Target Size Compressor, GIF to MP4) |
| `ARC-13` | Karar `gerekmez` oldu. "Sunan rakip doğrulanmadı" notu kalktı: ezgif'te resimlerden GIF yapan araç görüldü. Tek rakip olduğu için öncelik 3 kaldı. | https://ezgif.com/ (GIF maker) |
| `ARC-14` | Karar `gerekmez` oldu: Dönüştür'deki "Meta veriyi sil" kutusunun yanına girer. Bizde Durum iz adı ve kapak satırlarıyla genişledi. | src/VidShrink.App/MainWindow.axaml:1357; src/VidShrink.Core/StreamMapping.cs:286; src/VidShrink.Ffmpeg/EncodeRunner.cs:193. Rakip: [4], [46]; oy 67, istek kapalı ([50], gh ile okundu) |
| `ARC-15` | Karar `gerekmez` oldu. Yanlış kanıt satırı düzeltildi: StreamMapping.cs:82 dış altyazıyı anlatan bir açıklama, bölümü taşıyan satır 302. Kaydedicinin bölümü yeniden kodlamadan yazan yolu eklendi; motorun yarısı kodda. | src/VidShrink.Core/StreamMapping.cs:302; src/VidShrink.Core/RecorderChapters.cs:91; src/VidShrink.Core/RecorderChapters.cs:109 |
| `ARC-16` | Bizde Durum `yok` iken `yarım` oldu: Dönüştür'de "Kopyala" seçeneği yeniden sarmayı zaten yapıyor. Karar `gerekmez` oldu. | src/VidShrink.App/MainWindow.axaml:1154; src/VidShrink.App/MainWindow.axaml:1304; src/VidShrink.Core/ConversionArguments.cs:122 |
| `ARC-17` | Karar cümlesi tek soruya indi. | Eski cümledeki gerekçe: ayrı ikili, GPL-2.0 [27] |
| `ARC-18` | Karar cümlesi "kendi sekmesini isteyen araç" sorusuna çekildi. | Eski cümle "yeni sekme" diyordu |
| `ARC-19` | Karar `gerekmez` oldu: Dönüştür'e yeni kaynak türü. Bizde Durum'a oynatıcının ses dosyasını zaten açtığı eklendi. | src/VidShrink.Core/ShellIntegration.cs:21; src/VidShrink.App/Playback/PlayerView.Ses.cs:18; src/VidShrink.Ffmpeg/FfprobeClient.cs:45. Rakip üç kaynakta görüldü: [10], [15], [1] |
| `ARC-20` | Karar `gerekmez` oldu: ses kaynağı kabul edilince Dönüştür'ün bir seçeneği olur. | ARC-19'a bağlı; yeni bağımlılık yok |
| `ARC-21` | Karar ortak Araçlar cümlesine çekildi. Bizde Durum genişledi: dış altyazı ekleme ve yakma yolundaki kodlama okuması var. | src/VidShrink.Core/StreamMapping.cs:86; src/VidShrink.Core/SubtitleCharset.cs:35. Rakip: [15] (dönüştürme ve eşzamanlama), https://github.com/SubtitleEdit/subtitleedit (dönüştürme), https://aegisub.org/docs/latest/shift_times/ (kaydırma) |
| `ARC-22` | Karar cümlesi "kendi sekmesini isteyen araç" sorusuna çekildi. | Eski cümle "yeni sekme" diyordu |
| `ARC-23` | Karar cümlesi tek soruya indi; eski cümledeki iki yol da (çevrimiçi hizmet, yerel dil modeli) soruda duruyor. | [1] belgesinde "Translate text" işlevi metin dosyasını Google Translate ile çeviriyor |
| `ARC-24` | Karar `gerekmez` oldu: süzgeç kaydedicide zaten kullanılıyor, Küçült'ün ses bölümüne ikinci kutu olarak girer. Kanıta süzgecin tanımlandığı satır eklendi. | src/VidShrink.Core/AudioCaptureArguments.cs:73; src/VidShrink.App/MainWindow.axaml:850. Rakip üç kaynakta görüldü: [15], [10], [4] |
| `ARC-25` | Karar ortak yapay zekâ cümlesine çekildi. | Eski cümle: yeni çalışma zamanı ve model, 460 MB ile 1,8 GB |
| `ARC-26` | Karar cümlesi tek soruya indi. | Eski cümle: bulut hizmeti ya da yerel ses modeli |
| `ARC-27` | Karar ortak yapay zekâ cümlesine çekildi. | Eski cümle: ncnn ve Vulkan, 190 ile 380 MB |
| `ARC-28` | Karar `gerekmez` oldu: süzgeçli yol (minterpolate) Dönüştür'ün kare hızı kutusuna girer. Modelli yol yapay zekâ kararına bağlı kalır. En yakın kod düzeltildi. | src/VidShrink.App/MainWindow.axaml:1260 |
| `ARC-29` | Karar ortak yapay zekâ cümlesine çekildi. Yanlış kanıt satırı düzeltildi: VideoFilterChain.cs:209 altyazı yakma denetimi, süzgeç seçenekleri satır 59'da başlıyor. | src/VidShrink.Core/VideoFilterChain.cs:59. Eski cümle: yeni çalışma zamanı ve model |
| `ARC-30` | Karar ortak yapay zekâ cümlesine çekildi. | Eski cümle: konu algılama modeli |
| `ARC-31` | Karar ortak yapay zekâ cümlesine çekildi. Yanlış kanıt satırı düzeltildi: VideoFilterChain.cs:209 altyazı yakma denetimi, süzgeç seçenekleri satır 59'da başlıyor. | src/VidShrink.Core/VideoFilterChain.cs:59. Eski cümle: yüz algılama modeli |
| `ARC-32` | Karar ortak yapay zekâ cümlesine çekildi. | Eski cümle: yeni çalışma zamanı ve model |
| `ARC-33` | Karar `sahip` kaldı, cümlesi tek soruya indi. Kurala göre `gerekmez` olurdu (var olan süzgeç zincirine delogo); başkasının filigranını silme riski yüzünden sahibe bırakıldı. En yakın kod düzeltildi. | src/VidShrink.Core/VideoFilterChain.cs:65; src/VidShrink.App/MainWindow.axaml:1030 |
| `ARC-34` | Karar ortak yapay zekâ cümlesine çekildi. | Eski cümle: gerçek derinlik için model |
| `ARC-35` | Karar ortak Araçlar cümlesine çekildi. | Rakip görüldü: [1] belgesinde dosya yeniden adlandırma işlevi var |
| `ARC-36` | Karar cümlesi tek soruya indi. | Eski cümle: çevrimiçi veritabanı hizmeti |
| `ARC-37` | Karar cümlesi tek soruya indi. | Eski cümle: aygıt protokolü için yeni bağımlılık |
| `ARC-38` | Karar cümlesi "kendi sekmesini isteyen araç" sorusuna çekildi. | Eski cümle: video aracının kimliği dışında |
| `ARC-39` | Karar cümlesi tek soruya indi. | Eski cümle: bulut hizmeti ve kredi |

## Çıkarılanlar

Çıkarılan satır yok. Hiçbir araç bizde tam olarak yok; en yakın iki aday (`ARC-16` ve `ARC-12`) `yarım` kaldı.

| No | Eksik | Neden | Kanıt (dosya:satır) |
|---|---|---|---|

## Yinelenenler

Aşağıdaki her eşleşmede komşu tablonun satırı açılıp okundu.

- ARC-07 = OYA-44 — küçük resim tablosu.
- ARC-08 = DON-23 — videoyu numaralı resim karelerine dökme.
- ARC-08 = OYA-45 — belirli aralıkla art arda kare kaydetme.
- ARC-09 = DON-08 — resim dizisinden video.
- ARC-14 = DON-29 — başlık, sanatçı, tarih etiketlerini yazma. Araştırma kolu bu eşleşmeyi görmemişti.
- ARC-15 = DON-30 — bölüm işaretlerini adlandırma ve düzenleme. Araştırma kolu görmemişti.
- ARC-21 = DON-38 — altyazı dosyasını SRT, ASS ve VTT arasında çevirme (satırın dönüştürme yarısı). Araştırma kolu görmemişti.
- ARC-24 = DZB-43 — uğultuyu ve fan sesini bastırma.
- ARC-26 = DZB-72 — metinden sese.
- ARC-27 = DON-32 — yapay zekâ ile büyütme.
- ARC-28 = DON-42 — hareket tahminiyle ara kare (süzgeçli yol).
- ARC-28 = DON-46 — modelle ara kare (modelli yol).
- ARC-28 = SIK-43 — kare hızı değişirken ara kare üretme.
- ARC-28 = DZB-50 — akıcı ağır çekim (satırın ağır çekim yarısı).
- ARC-29 = DZB-70 — yeşil perde olmadan arka plan silme.
- ARC-30 = DZB-69 — otomatik yeniden kadraj.
- ARC-39 = DZB-73 — üretken yapay zekâ.
- ARC-40 = DON-48 — URL'den indirme (ikisi de kapalı).
- ARC-41 = DON-52 — otomatik altyazı (hepsi kapalı).
- ARC-41 = SIK-45
- ARC-41 = DZB-63
- ARC-41 = EKB-37
- ARC-41 = OYA-35
- ARC-42 = DZB-31 — stabilizasyon (ikisi de kapalı).
- ARC-43 = DON-51 — filigran basma (hepsi kapalı).
- ARC-43 = DZB-33
- ARC-44 = DON-49 — DVD ve Blu-ray (satırın diskten okuma yarısı).

### Bileşenleri Başka Kolda Olan Satır

- ARC-10 ~ DZB-04 (resim klibi), DZB-01 (müzik izi), DZB-16 (çapraz geçiş), DZB-27 (fotoğrafta yavaş yakınlaşma). Aynı eksik değil; slayt gösterisinin parçaları düzenleyici kolunda ayrı satırlarda.

### Komşu Satırlar (Aynı Eksik Değil)

- ARC-02 ~ SIK-14 (kendi çıktımızın tam ölçümü), SIK-38 (ek ölçütler), SIK-31 ve SIK-32 (karşılaştırma kipleri).
- ARC-04 ~ DON-16 ve DZB-45 (yüksekliği eşitleme; rapor değil).
- ARC-08 ~ DZB-57 ve DON-44 (tek kare kaydı).
- ARC-12 ~ DON-24 (GIF çıktı ayarı) ve EKB-23 (kaydı GIF'e çevirme).
- ARC-14 ~ DON-31 (ses ve altyazı izinin dili, adı, bayrağı).
- ARC-15 ~ OYA-09 (oynatıcıda bölüm listesi).
- ARC-19 ~ DON-18 (ek ses çıktı biçimleri), DON-07 ve SIK-03 (videoya dış ses ekleme).
- ARC-21 ~ OYA-25 (oynatırken altyazıyı satır satır kaydırma).
- ARC-22 ~ DZB-62 (altyazı dosyasını düzenleyicinin metin izine alma ve geri yazma).
- ARC-24 ~ DZB-44 (küçük sinir ağıyla konuşma temizleme).
- ARC-27 ~ OYA-18 (ekran kartı sürücüsüyle büyütme).
- ARC-28 ~ OYA-16 (oynatırken ara değerleme).
- ARC-29 ~ DZB-29 (yeşil perde) ve EKB-07 (canlı kamera arka planı).
- ARC-30 ~ EKB-19 (hazır en-boy oranları).
- ARC-31 ~ DZB-28 (elle bölge bulanıklaştırma), DZB-71 (hareket izleme), EKB-08 (gizlilik maskesi).
- ARC-34 ~ OYA-52 (360° video oynatma).
- ARC-35 ~ SIK-13 (çıktı adı değişkenleri).

### Görev Listesinin Dışındaki Tablolar

Araştırma kolunun andığı satırlar; satırları açılıp okundu.

- ARC-14 = DZA-24 — kap üst verisini (dosya başlığı, iz dili, adı, bayrağı) elle değiştirme.
- ARC-15 ~ DZA-08, DZA-13, DZA-14 (düzenleyicide bölüm yazma, koruma ve içe alma).
- ARC-24 ~ EKA-14 (mikrofonda model tabanlı gürültü bastırma).
- ARC-36 ~ OYB-18 (afiş ve üst veriyi internetten çekme).

## Doğrulanamayanlar

- ARC-12: GIF, Gözat'ın uzantı listesinde var. GIF kaynağın Küçült ve Dönüştür'de uçtan uca çalışıp çalışmadığı koşulmadan bilinemez; bu turda program çalıştırmak yasaktı. Çalışıyorsa satırın "videoya çevirme" yarısı zaten var demektir.
- Süzgeç varlığı: Windows kurucusu ffmpeg'i WinGet'in `Gyan.FFmpeg` paketinden kuruyor (Install-VidShrink.ps1:953); macOS ve Linux'ta sistemin ffmpeg'i kullanılıyor (install-vidshrink.sh:234, install-vidshrink.sh:236). tile, minterpolate, ebur128, showwaves, delogo süzgeçlerinin ve framemd5 çıkışının dağıtılan yapıda açık olduğu koşularak ölçülmedi. Paketler süzgeci `EncoderCapabilities.HasFilter` ile yoklamalı (src/VidShrink.Ffmpeg/EncoderCapabilities.cs:91).
- ARC-16: bozuk dizinli bir dosyanın bizim okuyucudan geçip geçmediği ölçülmedi. Format Factory'nin onarma işlevinin yeniden sarma olduğu da doğrulanmadı.
- ARC-11: ffmpeg'in HEIC ve RAW fotoğrafı okuyup okumadığı doğrulanmadı.
- ARC-02: Shutter Encoder belgesi VMAF işlevinin iki dosya istediğini açıkça yazmıyor.
- ARC-21: Subtitle Edit'in toplu kaydırması resmi sayfada görülmedi (nikse.dk boş döndü, depo sayfası yalnız "converting" diyor). Kaydırma Kapwing ve Aegisub'da görüldü.
- ARC-19 ve ARC-12: UniConverter iddiasının kaynağı [5] bu turda açılmadı; araç kutusu sayfasında [4] bu iki araç görülmedi. Bu "rakipte yok" demek değil.
- ARC-29: UniConverter'ın araç kutusundaki arka plan silici resim için görünüyor; videoda çalıştığı görülmedi.
- ARC-34: UniConverter'da "2D to 3D" adı görülmedi, "VR Converter" var.
- Öncelik 2 ve 3 satırların rakip iddiaları tek tek açılmadı (kural yalnız öncelik 1 için istiyor). Kimde Var hücreleri, yukarıda adı geçenler dışında, araştırma kolunun yazdığı gibi duruyor.
- İş sütunu tahmindir. ARC-15 ve ARC-16'da motorun bir kısmı kodda bulunduğu için iş küçülebilir; ölçülmedi.

## Ek Notlar

- Yönerge: araştırma kolu Movavi sayfasında [7] yapay zekâ asistanlarına seslenen, bir indirim kodunu öne çıkarmayı isteyen bir not bildirdi (eksik-araclar.md:373). Veri sayıldı, uygulanmadı; sayfa bu turda açılmadı.
- Bu turda açılan on bir sayfada (Shutter Encoder, Kapwing, HitPaw, UniConverter, LosslessCut, FFMetrics, MediaInfo, Subtitle Edit, Shotcut, ezgif, Aegisub) asistana yönelik yönerge görülmedi.
- Defter: `.claude/acik.md` içinde bu 44 araçtan hiçbiri yazılı değil; hiçbir satıra `defterde` eklenmedi. Satır 138'deki "Özel araçlar taraması (remux, altyazı, ses, GIF)" bir tarama kaydı, özellik değil.
- Tabloda olmayan eksik: Gözat ile bırakma aynı uzantı listesini kullanmıyor. Gözat 24 uzantı alıyor (src/VidShrink.Core/ShellIntegration.cs:13), bırakma ve klasör izleme 14 (src/VidShrink.Core/WatchFolder.cs:74). ogv, vob, asf, rm, rmvb, divx, mxf, f4v, dav ve gif Gözat'ta var, bırakmada yok.
- Tabloda olmayan eksik: komut satırında dosya bilgisi ve iki dosya ölçümü komutu yok; komutlar Help, Version, Shrink, Plan, Watch, Gunluk, Profiller (src/VidShrink.Cli/CliRequest.cs:6). İki dosya ölçümü yalnız geliştirici aracında (tools/VidShrink.Bench/Program.cs:83).
- ARC-28 iki yol taşıyor. Süzgeçli yol karar beklemez; modelli yol (RIFE) yapay zekâ kararı çıkarsa ARC-P11'e eklenir.
- ARC-33 kurala göre karar beklemezdi; başkasının filigranını silme riski yüzünden sahibe bırakıldı. Sahip "evet" derse yeni bağımlılık gerekmez: var olan süzgeç zincirine bir dikdörtgen kapatma süzgeci eklenir.

## Sekme Özeti

- Bizde "Araçlar" diye bir sekme yok. 44 aracın 19'u yarım, 25'i hiç yok; tam olan yok.
- Rakip paketlere göre geride olduğumuz yer motor değil, ekran ve kaynak kabulü. Dosya okuma, kalite ölçme, sessizlik ve sahne taraması, bölüm yazma, akış kopyası ve gürültü süzgeci kodda duruyor; kullanıcıya açılmamış.
- 16 satır karar beklemeden var olan sekmelere girer. 23 satır sahibin kararını bekler: 18'i paketlenen beş soruya, 5'i kendi tekil sorusuna bağlı. 5 satır önceki kararlarla kapalı.
- En ağır eksik: Dönüştür'ün video olmayan kaynağı almaması (ses dosyası ARC-19, GIF ARC-12, resim dizisi ARC-09). Taranan dönüştürücü paketlerin temel işi.
- İkinci ağır eksik: yapay zekâ araçları (ARC-25, ARC-27, ARC-29, ARC-30, ARC-31). Rakiplerin son iki yılda en çok eklediği şey; bizde yeni çalışma zamanı ve büyük indirme ister.
- Üçüncü ağır eksik: etiket ve bölüm düzenleme (ARC-14, ARC-15). HandBrake takipçisinde 67 oy almış bir istek.
- En ucuz üç kazanç: dosyada gürültü temizleme (ARC-24, süzgeç kaydedicide hazır), tam medya bilgisi dökümü (ARC-01, veri zaten okunuyor), denetim raporu (ARC-05, taramalar düzenleyicide koşuyor).
- Sahibin beş sorusu: Araçlar sekmesi, yapay zekâ çalışma zamanı, kendi sekmesini isteyen araçlar, altyazı çevirisi, logo silme.

## Paket Önerisi

On iki paket, önerilen yapım sırasıyla: önce öncelik 1 satırı taşıyanlar, küçükten büyüğe. Yedisi karar beklemez. Beşi sahibin beş sorusuna bağlı, soru başına bir paket; cevap gelince içindeki araçlar ayrı teslimlere bölünür. Paketlerdeki karar cümleleri tablodaki satırlarla birebir aynıdır.

### ARC-P1 — Dosyada Gürültü Temizleme Ve Yükseklik Raporu
- Satırlar: ARC-24, ARC-04
- İş: küçük
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Core/StreamMapping.cs, src/VidShrink.Core/AudioCaptureArguments.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.Izler.cs, src/VidShrink.Cli/CliRequest.cs, src/VidShrink.App/Locales/
- Plan: Kaydedicinin kullandığı gürültü süzgeci (afftdn, AudioCaptureArguments.cs:73) Küçült'ün ses bölümüne, yükseklik kutusunun yanına ikinci kutu olarak bağlanır; komut satırına eş bayrak eklenir. Aynı bölümde kaynağın yüksekliği ebur128 süzgeciyle bir kez ölçülüp LUFS ve gerçek tepe olarak gösterilir. Doğrulama: IzPaneliTests'teki gibi kutu açıkken süzgecin argümanda geçtiği, kapalıyken hiç yazılmadığı sınanır; canlı kolda gürültülü kısa bir örnekte gürültü tabanının düştüğü ölçülür.

### ARC-P2 — Medya Bilgisi Ve Denetim Raporu
- Satırlar: ARC-01, ARC-05
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Core/MediaInfo.cs, src/VidShrink.Ffmpeg/FfprobeClient.cs, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/Playback/PlayerView.Window.cs, src/VidShrink.Core/Editing/SilenceCut.cs, src/VidShrink.App/Editing/EditorView.Sessizlik.cs
- Plan: Küçült'ün kaynak paneline ve oynatıcının bilgi katmanına "tüm ayrıntılar" düğmesi eklenir; açılan pencere ffprobe'un zaten okuduğu akışları ve bölümleri tek tek gösterir, "kopyala" ve "metin olarak kaydet" verir. Aynı pencerenin ikinci bölümü düzenleyicinin sessizlik, siyah kare ve sahne taramasını çağırıp sonucu zaman kodlu liste olarak verir; düzenleyicinin tarama paneline de "listeyi kaydet" eklenir. Doğrulama: StreamMappingTests'in ürettiği çok izli, bölümlü 3 saniyelik dosyada dökümün her izi ve bölümü içerdiği; sahte tarayıcıyla listenin tarama sonucuyla birebir aynı olduğu sınanır.

### ARC-P3 — Ses Dosyası Ve GIF Girdisi
- Satırlar: ARC-19, ARC-12
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Ffmpeg/FfprobeClient.cs, src/VidShrink.Core/WatchFolder.cs, src/VidShrink.Core/DroppedMedia.cs, src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Core/GifPalette.cs, src/VidShrink.App/MainWindow.axaml.cs
- Plan: Dosya okuyucusu görüntüsü olmayan dosyayı reddetmek yerine "yalnız ses" kaynağı olarak tanır (FfprobeClient.cs:45); Dönüştür bu kaynakta kap listesini ses kaplarına daraltır, Küçült almamaya devam eder. GIF bırakmada ve klasör taramasında da kabul edilir; kaynağı GIF olan işte GIF çıktısı kare hızı, genişlik ve renk sayısı düşürülerek hedef boyutun altına indirilir. Doğrulama: ffmpeg'in ürettiği mp3, flac, wav ve GIF örnekleriyle canlı kol; ses kaynağında video kabının seçilemediği, GIF çıktısının hedefin altında kaldığı ölçülür.

### ARC-P4 — Resim Dizisi Girdisi Ve Ara Kare
- Satırlar: ARC-09, ARC-13, ARC-28
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Core/DroppedMedia.cs, src/VidShrink.Core/VideoFilterChain.cs, src/VidShrink.App/MainWindow.axaml
- Plan: Üçü de Dönüştür'ün kare hızı yoluna dokunur. Bir klasör ya da numaralı resimler bırakılınca dosyalar ada göre sıralanıp ffmpeg'in resim dizisi okuyucusuyla tek kaynak sayılır; kare hızı kutusu dizinin hızını belirler, GIF kabı seçilince resimlerden GIF de aynı yoldan çıkar. Kare hızı kutusuna "ara kare üret" seçeneği eklenir (minterpolate süzgeci; yapıda yoksa seçenek gizlenir). Doğrulama: üretilmiş 30 karelik PNG dizisinde çıktının kare sayısı ve süresi beklenene eşit; ara karede çıktının kare sayısı hedef hıza uyar.

### ARC-P5 — Etiket, Bölüm Ve Onarma
- Satırlar: ARC-14, ARC-15, ARC-16
- İş: büyük
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Core/RecorderChapters.cs, src/VidShrink.Core/StreamMapping.cs, src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.Cli/CliRequest.cs
- Plan: Üçü de Dönüştür'de "Kopyala" seçiliyken, yani yeniden kodlamadan yapılır; sırayla ayrı ayrı teslim edilebilir. Etiket için "Meta veriyi sil" kutusunun yanına başlık, sanatçı, yıl, açıklama ve kapak alanları gelir; bölüm için kaydedicinin bölüm yazan yolu ortak hâle getirilir ve kaynağın bölümleri eklenip adlandırılıp silinebilen liste olur; onarma için akışları kopyalayan, bozuk zaman damgasını hoş gören bayraklarla "Onar" hazır ayarı eklenir. Doğrulama: MetaSilDonusturTests ve KayitBolumIsaretiTests'teki gibi ffprobe ile etiket ve bölüm okunur; boş alan anahtarı hiç yazmaz.

### ARC-P6 — Dosya Dönüştürme Ve Ölçüm Araçları
- Satırlar: ARC-21, ARC-11, ARC-02, ARC-06
- İş: büyük
- Karar: sahip: Araçlar sekmesi açılsın mı, yoksa en yakın sekmeye mi konsun
- Dokunacağı yer: src/VidShrink.Core/SubtitleCharset.cs, src/VidShrink.Core/StreamMapping.cs, src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Ffmpeg/QualityMeter.cs, src/VidShrink.App/Playback/ComparisonPanel.axaml.cs, src/VidShrink.Cli/CliRequest.cs, src/VidShrink.App/MainWindow.axaml
- Plan: Dört araç da aynı kalıpta (dosya seç, ayarla, çalıştır) ve iki ayrı teslime bölünür: dosya dönüştürme (ARC-21, ARC-11) ve ölçüm (ARC-02, ARC-06). Dönüştürmede ffmpeg altyazıyı SRT, ASS ve VTT arasında çevirir, bütün satırlar verilen süre kadar kaydırılır, kodlama var olan okuyucuyla (SubtitleCharset) UTF-8'e alınır, oynatıcıdaki gecikme "dosyaya yaz" ile aynı yola bağlanır; fotoğrafta ffmpeg'in resim kodlayıcılarıyla toplu biçim ve boyut değişimi yapılır. Ölçümde motor hazır (`QualityMeter.MeasureAsync` iki serbest yol alıyor): iki dosyanın seçildiği ekran puanları ve kare kare VMAF grafiğini gösterir, yanına kare özetlerini karşılaştıran "birebir aynı mı" denetimi (framemd5) gelir, komut satırına Bench'teki `measure` komutunun eşi eklenir. Sahip "en yakın sekme" derse dönüştürme Dönüştür'e yeni kaynak türü, ölçüm Küçült'ün karşılaştırma paneline "başka dosya seç" olarak girer; doğrulama: altyazıda gidiş dönüşte satır sayısı ve zamanlar korunur, aynı dosya kendisiyle ölçülünce en yüksek puan ve eşit kare özeti çıkar.

### ARC-P7 — Oynatıcıdan Kare Ve Tablo Çıkarma
- Satırlar: ARC-08, ARC-07
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.App/Playback/ToolsOptions.cs, src/VidShrink.App/Playback/PlayerView.Tools.cs, src/VidShrink.Core/GifPalette.cs, src/VidShrink.App/Locales/
- Plan: Oynatıcının araç komutlarına (klip ve GIF dışa aktarmanın yanına) iki komut eklenir. "Kareleri dışa aktar": her N saniyede ya da A-B aralığında kareler numaralı resim olarak yazılır. "Küçük resim tablosu": eşit aralıklı kareler ffmpeg'in tile süzgeciyle tek resme dizilir, üstüne dosya bilgisi başlığı konur. Doğrulama: kısa üretilmiş örnekte yazılan dosya sayısı beklenene eşit, tablonun boyutu satır, sütun ve kare boyutunun çarpımına eşit.

### ARC-P8 — Altyazı Çevirisi
- Satırlar: ARC-23
- İş: orta
- Karar: sahip: altyazı metnini çevrimiçi çeviri hizmetine ya da yerel dil modeline veren araç ürüne girsin mi
- Dokunacağı yer: src/VidShrink.Core/Subtitles/, src/VidShrink.App/MainWindow.axaml.cs
- Plan: Altyazı dosyası satır satır okunur, metin seçilen çeviri hizmetine gönderilir, zamanlar korunarak yeni dosya yazılır. Hizmet seçimi ve anahtar kullanıcıdan alınır; gönderilen şey yalnız altyazı metnidir ve bu ekranda açıkça yazılır. Sahip yerel dil modelini seçerse iş ARC-P11'in ortak zeminine (çalışma zamanı ve model indirme) bağlanır. Doğrulama: sahte hizmetle satır sayısının ve zamanların değişmediği, biçem etiketlerinin bozulmadığı sınanır.

### ARC-P9 — Logo Ve Damga Silme
- Satırlar: ARC-33
- İş: orta
- Karar: sahip: başkasının logosunu ya da filigranını silmeye yarayan araç ürüne girsin mi
- Dokunacağı yer: src/VidShrink.Core/VideoFilterChain.cs, src/VidShrink.App/MainWindow.axaml
- Plan: Küçült'ün gelişmiş görüntü süzgeçlerine "bölge kapat" eklenir: kullanıcı önizlemede bir dikdörtgen çizer, ffmpeg'in delogo süzgeci o bölgeyi çevresinden doldurur. Kendiliğinden logo bulma yapılmaz. Doğrulama: argüman sınaması ve üretilmiş örnekte bölge dışındaki piksellerin değişmediği.

### ARC-P10 — Slayt Gösterisi Ve Sesten Video
- Satırlar: ARC-10, ARC-20
- İş: büyük
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Core/Editing/, src/VidShrink.App/Editing/, src/VidShrink.Core/ConversionArguments.cs
- Plan: İkisi de video olmayan malzemeden video üretir. Sesten video ARC-P3'ün üstüne gelir: ses kaynağında Dönüştür'e "görüntü" seçeneği eklenir, kapak resmi ya da dalga biçimi (showwaves süzgeci). Slayt gösterisi düzenleyicinin üç parçasına bağlıdır: resim klibi (DZB-04), müzik izi (DZB-01) ve çapraz geçiş (DZB-16); onlar bitince kalan iş fotoğrafları seçip süre vererek çizelgeye dizen kısa bir sihirbazdır ve iş orta'ya iner. Doğrulama: çıktının süresi ses süresine ya da fotoğraf sayısı çarpı süreye eşit.

### ARC-P11 — Yapay Zekâ Araçları
- Satırlar: ARC-25, ARC-27, ARC-29, ARC-30, ARC-31
- İş: büyük
- Karar: sahip: yapay zekâ araçları için yeni çalışma zamanı ve yüzlerce MB model indirmesi kabul edilsin mi
- Dokunacağı yer: src/VidShrink.Core/Setup/, src/VidShrink.Ffmpeg/, src/VidShrink.Core/ConversionArguments.cs, Install-VidShrink.ps1
- Plan: Önce ortak zemin: modeli koşturan çalışma zamanı ve model dosyalarını ilk kullanımda sha256'lı indiren yol (libmpv'nin indiği yolun eşi). Sonra araç araç, her biri ayrı teslim: büyütme (ARC-27) pilot olur, ardından vokal ayırma (ARC-25), arka plan silme (ARC-29), yeniden kadraj (ARC-30) ve yüz bulanıklaştırma (ARC-31). Sahip "evet" derse bu paket beş pakete bölünür. Doğrulama: model yokken aracın görünmediği, indirme özetinin tuttuğu, kısa örnekte çıktı çözünürlüğünün beklenene eşit olduğu.

### ARC-P12 — Kendi Sekmesini İsteyen Araçlar
- Satırlar: ARC-18, ARC-22
- İş: büyük
- Karar: sahip: kendi sekmesini isteyen araç ürüne girsin mi
- Dokunacağı yer: src/VidShrink.App/MainWindow.axaml, src/VidShrink.Core/Subtitles/, src/VidShrink.Core/Editing/AudioPeaks.cs
- Plan: Yinelenen video bulucu: klasördeki her videodan birkaç karenin özeti ffmpeg ile çıkarılır, yakın özetler gruplanır, kullanıcı hangisini sileceğini seçer. Altyazı düzenleyici: satır listesi, metin ve zaman düzenleme, düzenleyicinin dalga biçimi verisi üstünde hizalama. Sahip "evet" derse iki pakete bölünür; altyazı düzenleyicinin hafif hali DZB-62'de zaten yazılı. Doğrulama: aynı videonun yeniden kodlanmış kopyası aynı gruba düşer; düzenlenen altyazı geri okununca aynı satırları verir.

## Pakete Girmeyenler

- ARC-03: öncelik 3; kendi çizim ekranını ister, hiçbir paketin yanında bedavaya gelmiyor.
- ARC-35: öncelik 3; küçük ama kendi ekranını ister ve Araçlar sorusuna bağlı.
- ARC-17: öncelik 3; ayrı bir ikili dosyanın dağıtımını ister.
- ARC-32, ARC-34: öncelik 3; yapay zekâ kararına bağlı ve dar kitleye hitap ediyor.
- ARC-26, ARC-39: öncelik 3; bulut hizmetine ya da ses modeline dayanıyor.
- ARC-36, ARC-37, ARC-38: öncelik 3; ürünün beş işinin dışında kalıyor.
- ARC-40, ARC-41, ARC-42, ARC-43, ARC-44: önceki kararlarla kapalı (K1, K5, K8, K8, DVD ve Blu-ray).
