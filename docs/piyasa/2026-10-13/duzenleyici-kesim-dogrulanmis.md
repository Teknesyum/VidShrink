# Düzenleyici Sekmesi — Kesim, Birleştirme Ve Otomatik Kesim Eksikleri (Doğrulanmış)

13 Ekim 2026. Kod okuması `main` eb0a5db0; `src` klasörü 9de48d8b ile aynı. Kaynak numaraları `duzenleyici-kesim.md` dosyasındaki listeye bakar. Yollar depo köküne göredir.

Bu dosya `duzenleyici-kesim-eksikler.md` tablosunun ölçülmüş hâlidir. Araştırma kolunun kendi koyduğu "15 oy" eşiği ORTAK.md'de yok; öncelikler yalnız ORTAK.md ölçütüne göre yeniden tartıldı. Değişen her hücre Düzeltmeler bölümünde yazılı.

| No | Eksik | Kimde Var | Bizde Durum | Fayda | İş | Öncelik | Karar | Kaynak |
|---|---|---|---|---|---|---|---|---|
| DZA-01 | Kayıpsız birleştirme: aynı kodek ve ayarlardaki dosyaları yeniden kodlamadan uç uca ekleme. | LosslessCut, SolveigMM Video Splitter, Bandicut, Machete ve 5 rakip daha | yarım: kaynak eklenir ama çok kaynakta teslim her kipte yeniden kodlanır (src/VidShrink.Core/Editing/EditExport.cs:252) | Aynı kameradan ya da aynı kayıttan çıkan parçalar kalite kaybı olmadan, saniyeler içinde tek dosya olur. | orta | 1 | sahip: K7 planı çok kaynakta her zaman yeniden kodlamayı seçti; kopya yolu Hızlı ve Akıllı kipin çok kaynaktaki davranışını değiştirir | [1][25][34][31][29][44][46][48][42] |
| DZA-02 | Tüm izleri taşıma: teslimde ek ses izlerini, altyazı izlerini ve veri izlerini kesilmiş hâliyle koruma. | LosslessCut, SolveigMM Video Splitter, smartcut, VidCutter ve 2 rakip daha | yarım: yalnız ilk görüntü ve ilk ses izi eşlenir, altyazı ve ek ses düşer (src/VidShrink.Core/Editing/EditExport.cs:541-542); çok sesli ve altyazılı iz planı yalnız Küçült tarafında var (src/VidShrink.Core/StreamMapping.cs:201) | İkinci dil sesi ve altyazı kesimden sonra kaybolmaz. | orta | 1 | sahip: teslimin varsayılan iz eşlemesi değişir | [1][6][25][36][42][46][29] |
| DZA-03 | İz seçim paneli: teslimden önce hangi görüntü, ses ve altyazı izinin çıkacağını tek tek açıp kapatma. | LosslessCut, MKVToolNix, SolveigMM Video Splitter, MP4Box | yok (en yakın kod sabit eşleme: src/VidShrink.Core/Editing/EditExport.cs:541-542) | Gereksiz ses izi atılır, istenen dil ve altyazı seçilerek teslim edilir. | orta | 1 | gerekmez | [1][6][46][25][48] |
| DZA-04 | Akıllı kipte öbür ses kodekleri: AC-3, MP3, DTS gibi sesli kaynakta görüntüyü akıllı kesip sesi olduğu gibi kopyalama. | SolveigMM Video Splitter, TMPGEnc MPEG Smart Renderer, smartcut | yarım: ses kapısı AAC, sessiz kaynak ve koşullu Opus dışını Tam kipe düşürür (src/VidShrink.Core/Editing/EditExport.cs:116-121) | AC-3 ya da MP3 sesli film ve TV kaydı kesilirken bütün görüntü baştan kodlanmaz. | orta | 1 | gerekmez | [25][29][36] |
| DZA-05 | Hızlı kipte kayma bildirimi: kesim başı anahtar kareye denk gelmiyorsa çıktının kaç saniye erken başlayacağını teslimden önce gösterme. | LosslessCut, Avidemux, SolveigMM Video Splitter | yarım: kayma hesaplanır ama kullanıcıya gösterilmez (src/VidShrink.Core/Editing/EditExport.cs:533; teslim notlarında satırı yok: src/VidShrink.App/Editing/EditorView.Teslim.cs:359-373) | Çıktının neden birkaç saniye erken başladığı teslimden önce görülür, kip ona göre seçilir. | küçük | 1 | gerekmez | [2][5][40][27][10] |
| DZA-06 | Parçalara otomatik bölme: çizelgeyi eşit sayıda parçaya, sabit süreye ya da hedef dosya boyutuna göre bölme. | LosslessCut, SolveigMM Video Splitter, Bandicut, MKVToolNix | yok (en yakın kod verilen sınır listesinden bölen SplitSource: src/VidShrink.Core/Editing/EditTimeline.cs:235; elle bölme aynı dosyada :115) | Uzun kayıt, yükleme sınırına ya da istenen süreye uyan parçalara tek adımda ayrılır. | küçük | 1 | gerekmez | [1][25][33][46] |
| DZA-07 | Bulunan aralıkları tek tek seçme: sessizlik ve siyah kare taramasının önerilerini kesmeden önce ayrı ayrı açıp kapatma ya da kenarını düzeltme. | LosslessCut, TimeBolt, Clipchamp, Gling | yarım: aralıklar çizelgede boyanır ama yalnız topluca kesilir (src/VidShrink.App/Editing/EditorView.Sessizlik.cs:125-131) | Yanlış yakalanan duraklama ya da bilerek bırakılan sessizlik kesilmeden kurtarılır. | orta | 1 | gerekmez | [1][3][16][24][18] |
| DZA-08 | Parçalardan bölüm yazma: çizelgedeki parçaları ya da birleştirilen kaynakları çıktı dosyasına adlı bölüm (chapter) olarak gömme. | LosslessCut, VidCutter, MKVToolNix | yok: düzenleyici bölüm yazmaz (src/VidShrink.Core/Editing/EditExport.cs:387); en yakın kod Kaydedici'nin bölüm yazıcısı (src/VidShrink.Core/RecorderChapters.cs:91) | Birleştirilen ya da parçalanan video oynatıcıda bölüm bölüm gezilir. | orta | 2 | gerekmez | [1][42][46] |
| DZA-09 | Toplu teslim: birden çok dosyayı ya da projeyi sıraya koyup art arda işleme. | SolveigMM Video Splitter, TMPGEnc MPEG Smart Renderer, Shutter Encoder, MKVToolNix ve 2 rakip daha | yok (en yakın kod Küçült'ün iş pompası: src/VidShrink.Core/JobPump.cs:8; düzenleyicide yalnız küçük resim kuyruğu var: src/VidShrink.Core/Editing/ThumbnailQueue.cs:19) | Birden çok kayıt sıraya konur, başında beklemeden art arda kesilir. | büyük | 1 | gerekmez | [25][29][44][47][16][35][10] |
| DZA-10 | Önizlemede iz değiştirme: düzenlerken dinlenecek ses izini ve görünecek altyazıyı seçme. | LosslessCut, SolveigMM Video Splitter | yarım: monitör uygulamanın oynatıcısıdır, sağ tık menüsünde ses izi ve altyazı seçimi var (src/VidShrink.App/Playback/PlayerView.Menu.cs:23-24, src/VidShrink.App/Playback/Keymap.cs:112); görünür düğme yok, seçim teslime yansımaz | Çok dilli ya da çok mikrofonlu dosyada doğru ses dinlenerek kesilir. | küçük | 2 | gerekmez | [1][3][26][10] |
| DZA-11 | İz kaydırma: bir ses ya da altyazı izini yeniden kodlamadan öne ya da arkaya alma. | LosslessCut, MKVToolNix | yok (en yakın kod oynatıcıda yalnız çalarken geçerli ses ve altyazı gecikmesi: src/VidShrink.App/Playback/PlayerView.Tracks.cs:104-108; teslimde iz eşlemesi sabit: src/VidShrink.Core/Editing/EditExport.cs:541-542) | Sesi görüntüden kaymış kayıt kayıpsız düzelir. | küçük | 2 | gerekmez | [2][10][46] |
| DZA-12 | Anahtar kareye atlama: önceki ve sonraki anahtar kareye giden komut ve kısayol. | LosslessCut, Avidemux | yok: çentikler çizilir ama komut yok (src/VidShrink.App/Editing/EditorTimeline.Serit.cs:36-44) | Hızlı kipte kesim tam bir anahtar kareye konur, çıktı istenen yerden başlar. | küçük | 2 | gerekmez | [1][3][41] |
| DZA-13 | Kaynak bölümlerini koruma: kaynak dosyadaki bölüm işaretlerini kesim ve birleştirme sonrası kaymış zamanlarıyla çıktıya yazma. | LosslessCut, MKVToolNix | yok: düzenleyici bölüm yazmaz (src/VidShrink.Core/Editing/EditExport.cs:387) | Filmin ya da kaydın bölüm işaretleri kesimden sonra da oynatıcıda durur. | orta | 2 | gerekmez | [2][3][46] |
| DZA-14 | Bölümleri kesim noktası olarak içe alma: dosyadaki bölümleri açılışta parça ya da işaretçi olarak çizelgeye getirme. | LosslessCut, MKVToolNix | yarım: ffprobe bölümleri okur (src/VidShrink.Core/MediaInfo.cs:61), komut satırı bölüm aralığını kesebilir (src/VidShrink.Cli/CliRequest.cs:436-443), düzenleyici kullanmaz | Kayıtta F6 ile konan bölüm işaretleri düzenleyicide hazır kesim noktası olarak gelir. | küçük | 2 | gerekmez | [1][46] |
| DZA-15 | Kesim listesini dışa aktarma: parça başlangıç ve bitişlerini CSV, EDL ya da YouTube bölüm metni olarak kaydetme veya panoya kopyalama. | LosslessCut, TimeBolt, Shutter Encoder | yok (en yakın kod mpv önizleme listesi: src/VidShrink.Core/Editing/EdlPreview.cs:17) | YouTube açıklamasına yapıştırılacak bölüm listesi ya da tabloya girecek zamanlar elle yazılmaz. | küçük | 2 | gerekmez | [1][9][16][44] |
| DZA-16 | Kesim listesini içe aktarma: CSV, EDL ya da CUE dosyasındaki zamanları parça olarak açma. | LosslessCut, SolveigMM Video Splitter, VidCutter, auto-editor | yok (en yakın kod kendi proje dosyamız: src/VidShrink.Core/Editing/EditProject.cs:43) | Başka araçta ya da tabloda hazırlanan kesim zamanları yeniden girilmez. | orta | 2 | gerekmez | [9][25][42][12] |
| DZA-17 | Kurgu programına aktarma: kesilmiş çizelgeyi Premiere Pro, DaVinci Resolve ya da Final Cut Pro'nun açtığı XML olarak verme. | auto-editor, Recut, TimeBolt, Gling | yok (en yakın kod kendi proje dosyamız: src/VidShrink.Core/Editing/EditProject.cs:43) | Sessizliği atılmış kaba kurgu, asıl kurgunun yapılacağı programda klip klip açılır. | orta | 2 | gerekmez | [11][14][16][18] |
| DZA-18 | Çıktı adı şablonu: çıktı ve parça adlarını değişkenlerle (dosya adı, parça numarası, etiket, kesim zamanı) kurma. | LosslessCut, MKVToolNix, MP4Box, Shutter Encoder | yarım: değişkenli ad deseni yalnız Küçült çıktısında var (src/VidShrink.Core/AdlandirmaDeseni.cs:18); düzenleyici sabit `-duzenlenmis` soneki ve sıra numarası yazar (src/VidShrink.Core/Editing/EditOutputName.cs:9) | Çok parçalı teslimde dosyalar numara, etiket ya da zamanla anlamlı adlandırılır. | küçük | 2 | gerekmez | [7][46][48][44] |
| DZA-19 | Parça etiketi: her parçaya ad verme; ad dosya adında ve bölüm başlığında kullanılır. | LosslessCut, VidCutter | yok (klip modelinde ad alanı yok: src/VidShrink.Core/Editing/EditTimeline.cs:115) | Parçalar "giriş", "soru 3" gibi adlarla ayırt edilir. | küçük | 2 | gerekmez | [1][6][42] |
| DZA-20 | Sessizi hızlandırma: sessiz yeri silmek yerine hızlandırarak tutma. | auto-editor, TimeBolt | yok: tarama sonucu yalnız silinir (src/VidShrink.App/Editing/EditorView.Sessizlik.cs:125-131) | Ders ve oyun kayıtlarında boşluklar atılmaz, hızlı geçer; bağlam kaybolmaz. | orta | 2 | gerekmez | [11][12][16] |
| DZA-21 | Kenar payını ayrı verme: sessizliğin önünde ve arkasında bırakılacak payı ayrı ayrı ayarlama. | auto-editor, TimeBolt | yarım: tek kenar payı var (src/VidShrink.Core/Editing/SilenceCut.cs:12-43) | Cümle sonu kesilmeden cümle başı sıkı tutulur. | küçük | 2 | gerekmez | [11][16] |
| DZA-22 | Taramada ses izi seçme: sessizlik taramasının hangi ses izine bakacağını seçme. | LosslessCut, auto-editor | yarım: yalnız ilk ses izi taranır (src/VidShrink.Core/Editing/SilenceCut.cs:90) | Mikrofonu ayrı ize kaydeden, oyun sesine değil kendi sesine göre keser. | küçük | 2 | gerekmez | [58][1][11] |
| DZA-23 | Döndürme bayrağı: videoyu yeniden kodlamadan, kap üst verisiyle döndürme. | LosslessCut | yarım: döndürme yalnız süzgeçle yapılır, yeniden kodlar (src/VidShrink.Core/Editing/ClipFilters.cs:50-51) | Yan çekilmiş telefon videosu saniyede ve kayıpsız düzelir. | küçük | 3 | gerekmez | [1][43][10] |
| DZA-24 | Kap üst verisini düzenleme: dosya başlığını ve izlerin dilini, adını, varsayılan bayrağını elle değiştirme. | LosslessCut, MKVToolNix, TMPGEnc MPEG Smart Renderer | yarım: yalnız üst veriyi topluca silme kutusu var (src/VidShrink.App/Editing/EditorView.Teslim.cs:81-85) | Dosya başlığı ve ses izinin dili doğru yazılır; oynatıcı doğru izi seçer. | orta | 2 | gerekmez | [1][46][47][29] |
| DZA-25 | Başka dosyadan iz ekleme: videoya yeniden kodlamadan dışarıdan ses ya da altyazı izi ekleme, sesi değiştirme. | LosslessCut, Shutter Encoder, Machete, MKVToolNix | yok (en yakın kod Küçült'ün sıkıştırma yolunda dış altyazı dosyası ekleme: src/VidShrink.Core/StreamMapping.cs:805-808; düzenleyicide çok kaynak yalnız uç uca eklenir: src/VidShrink.Core/Editing/EditTimeline.cs:53) | Videonun sesi temiz kayıtla değiştirilir, görüntüye dokunulmaz. | orta | 2 | gerekmez | [1][44][31][46] |
| DZA-26 | İzleri ayrı dosyalara çıkarma: ses, altyazı ve görüntü izlerini yeniden kodlamadan ayrı dosya olarak kaydetme. | LosslessCut, Shutter Encoder | yok (en yakın kod Dönüştür sekmesinin yalnız ses kabı, sesi hep yeniden kodlar: src/VidShrink.App/MainWindow.axaml.cs:4782-4788; argüman üreticisi ses kopyasını tanır: src/VidShrink.Core/ConversionArguments.cs:244) | Videonun sesi ya da altyazısı kalite kaybı olmadan ayrı dosya olur. | küçük | 2 | gerekmez | [1][44] |
| DZA-27 | Akıllı kipte MPEG-2: TV kaydı ve eski kamera dosyalarını kenarları yeniden kodlayarak kesme. | SolveigMM Video Splitter, TMPGEnc MPEG Smart Renderer, smartcut | yok: akıllı kip dört kodekle sınırlı (src/VidShrink.Core/Editing/SmartCutCodec.cs:43) | TV kayıtları tam yeniden kodlama olmadan kare hassasiyetinde kesilir. | orta | 2 | gerekmez | [25][26][29][36] |
| DZA-28 | Komut satırında çok parçalı kesim: proje dosyasını ya da kesim listesini komut satırından teslim etme. | LosslessCut, auto-editor, Recut, SolveigMM Video Splitter | yarım: komut satırı tek aralık keser, proje ya da parça listesi koşmaz (src/VidShrink.Cli/CliRequest.cs:425) | Aynı kesim betikle, pencere açmadan yinelenir. | orta | 2 | gerekmez | [1][11][15][25] |
| DZA-29 | İşaretçileri kaydetme ve adlandırma: işaretçileri proje dosyasına yazma ve ad verme. | LosslessCut | yarım: işaretçi yalnız bellekte durur, projeye yazılmaz (src/VidShrink.App/Editing/EditorTimeline.cs:43) | Proje yeniden açılınca işaretler yerinde durur. | küçük | 3 | gerekmez | [3][6] |
| DZA-30 | Anahtar karede olduğunu gösterme: oynatma başı bir anahtar karedeyse bunu monitörde belirtme. | LosslessCut | yarım: çizelgede çentik var, anlık gösterge yok (src/VidShrink.App/Editing/EditorTimeline.Serit.cs:36-44) | Kesim noktasının kayıpsız kesilebilir olup olmadığı bakınca anlaşılır. | küçük | 3 | gerekmez | [3] |
| DZA-31 | Sahne eşiği ayarı: sahne değişimi taramasının duyarlılığını arayüzden değiştirme. | LosslessCut | yarım: eşik sabit (src/VidShrink.Ffmpeg/SceneDetector.cs:85), defterde | Çok az ya da çok fazla bölünen videoda sonuç düzeltilebilir. | küçük | 3 | gerekmez | [58] |
| DZA-32 | Anahtar kare dışını atma: yalnız anahtar kareleri tutarak uzun kaydı yeniden kodlamadan hızlandırılmış videoya çevirme. | LosslessCut | yok (en yakın kod hız ayarı, yeniden kodlar: src/VidShrink.Core/Editing/EditTimeline.cs:402) | Saatlerce süren güvenlik kamerası ya da zaman atlamalı kayıt saniyeler içinde kısa bir özete iner. | küçük | 3 | gerekmez | [1][2] |
| DZA-33 | Hareketsizlik algılama: görüntüde hareket olmayan yerleri bulup kesme. | auto-editor | yarım: hareketsiz yeri bulup kesme Kaydedici'de kayıt sonrası var (src/VidShrink.Core/IdleTrim.cs:29, src/VidShrink.App/Recorder/RecorderView.Kirpma.cs:16); düzenleyici taramasında yok | Ekran kaydında hiçbir şeyin olmadığı beklemeler atılır. | küçük | 3 | gerekmez | [11] |
| DZA-34 | Reklam adayı algılama: TV kaydında reklam kuşaklarını aday olarak işaretleme. | TMPGEnc MPEG Smart Renderer | yok (en yakın kod sahne taraması: src/VidShrink.Core/Editing/SceneSplit.cs:9) | TV kaydından reklamlar elle aranmadan çıkarılır. | büyük | 3 | gerekmez | [29] |
| DZA-35 | Akıllı kipte 4:2:2 ve 4:4:4: 4:2:0 dışı renk örneklemeli kaynakta akıllı kesim. | TMPGEnc MPEG Smart Renderer, smartcut | yok: kapı 4:2:0 ister (src/VidShrink.Core/Editing/SmartCutCodec.cs:40-41) | Profesyonel kamera ve 4:4:4 ekran kaydı dosyaları da kenardan kesilir. | orta | 3 | gerekmez | [29][36] |
| DZA-36 | Akıllı kipte HDR üst verisi: HDR kaynakta yeniden kodlanan kenarın renk bilgisini gövdeyle aynı yazma. | TMPGEnc MPEG Smart Renderer | yarım: 10 bit geçer ama renk ve HDR üst veri eşitliği ölçülmedi (src/VidShrink.Core/Editing/SmartCutCodec.cs:49), defterde | HDR video kesim noktasında renk sıçraması olmadan oynar. | orta | 3 | gerekmez | [29] |
| DZA-37 | Uyumsuz klibi tek başına yeniden kodlama: birleştirmede yalnız ayarı uymayan klibi yeniden kodlayıp öbürlerini kopyalama. | TMPGEnc MPEG Smart Renderer | yarım: çok kaynakta bütün parçalar yeniden kodlanır (src/VidShrink.Core/Editing/EditExport.cs:252) | Tek bir farklı klip yüzünden bütün birleştirme baştan kodlanmaz. | büyük | 3 | sahip: K7'nin çok kaynakta tam yeniden kodlama kararını değiştirir | [29] |
| DZA-38 | Kayıpsız kırpma: görüntünün kenarlarını yeniden kodlamadan, bit akışı üst verisiyle kırpma. | LosslessCut | yarım: kırpma süzgeçle yapılır, yeniden kodlar (src/VidShrink.Core/Editing/ClipFilters.cs:46-48) | Kenardaki siyah bant ya da istenmeyen şerit kalite kaybı olmadan gizlenir. | orta | 3 | gerekmez | [2][10] |
| DZA-39 | Düzenleyiciden kare kaydetme: oynatma başındaki kareyi resim olarak, bir aralığı kare dizisi olarak kaydetme. | LosslessCut | yarım: monitörün sağ tık menüsünde oynatıcının kare kaydet ve kareyi kopyala satırları var (src/VidShrink.App/Playback/PlayerView.Menu.cs:29-34); düzenleyici kısayolu ve aralığı kare dizisi olarak kaydetme yok | Kesim yaparken küçük resim ya da kapak karesi aynı yerden alınır. | küçük | 3 | gerekmez | [1] |
| DZA-40 | Proje dosyasını çift tıkla açma: proje dosyasını işletim sisteminde uygulamayla ilişkilendirme. | Recut | yarım: proje dosyası var ve yalnız düzenleyicinin menüsünden açılır (src/VidShrink.App/Editing/EditorView.Proje.cs:202-211); uzantısı kabuk uzantı listelerinde yok (src/VidShrink.Core/ShellIntegration.cs:32-35) | Kaydedilen proje dosya gezgininden çift tıkla açılır. | küçük | 3 | sahip: Windows dosya türünü son uzantıdan (.json) tanır; yeni bir uzantı ve kurucuda yeni dosya türü kaydı gerekir | [15] |
| DZA-41 | Zaman kodu ofseti: çizelge zamanını kaynağın gömülü zaman koduna ya da verilen bir başlangıca göre gösterme. | LosslessCut | yok (en yakın kod zaman kodu kutusu: src/VidShrink.Core/Editing/EditTimecode.cs:34-41) | Kamera zaman koduyla not alınmış kesim yerleri doğrudan bulunur. | küçük | 3 | gerekmez | [1][3] |
| DZA-42 | Konuşma metninden kesim: dolgu sözcükleri ve tekrar çekimleri konuşmayı yazıya döküp bulma. | TimeBolt, Gling, Clipchamp | yok (en yakın kod ses düzeyine bakan tarama: src/VidShrink.Core/Editing/SilenceCut.cs:12-43) | "Iıı" gibi dolgu sözleri ve bozuk çekimler elle aranmadan atılır. | büyük | 3 | kapalı: K5 | [16][18][24] |
| DZA-43 | Akış adresinden kayıpsız indirme: HLS adresindeki videoyu yeniden kodlamadan dosyaya alma. | LosslessCut | yok (oynatıcı uzak adresi yalnız çalar, dosyaya almaz: src/VidShrink.Core/MediaAddress.cs:15) | Yayın adresindeki video indirilip aynı pencerede kesilir. | orta | 3 | kapalı: K1 | [1] |
| DZA-44 | Kapak resmi ekleme: videoya dış resim dosyasından ya da çizelgedeki bir kareden kapak resmi iliştirme. | LosslessCut | yok (en yakın kod: kaynakta zaten duran kapağı kodlama çıktısına taşıyan eşleme, src/VidShrink.Core/StreamMapping.cs:815-828; yeni kapak ekleyen kod yok) | Dosya, dosya gezgininde ve oynatıcıda seçilen kareyle görünür. | küçük | 3 | gerekmez | [1] |

## Düzeltmeler

Toplam 15 satıra dokunuldu; kalan 29 satır birebir duruyor. Üç satırın önceliği değişti (DZA-10, DZA-11, DZA-23); DZA-11'de Kimde Var ve Kaynak da düzeltildi. Öbür on iki satırda yalnız Bizde Durum düzeltildi; DZA-33'te İş de değişti.

| No | Ne Değişti | Kanıt |
|---|---|---|
| `DZA-10` | Öncelik 1 → 2. Bizde Durum `yok` → `yarım`. | ORTAK ölçütünün iki kolu ayrı tartıldı. Rakip kolu, iki rakip görüldü: LosslessCut README'si çalma için görüntü ve ses izi seçmeyi sayıyor [1], SolveigMM sürüm geçmişi 5.0'da önizleme için akış seçme penceresini yazıyor [26]. Üçüncü rakip görülmedi, yani "üç rakip" kolu tutmuyor. Oy kolu: ORTAK yalnız "oyla kanıtlı" der, eşik vermez; 15'i araştırma kolu kendi koydu. #256 açık ve 28 oy (`gh api` ile okundu), yani talep kanıtlı; notlara göre LosslessCut ses ve görüntü izi değiştirmeyi 3.61'de ekledi. Ama istenen şey bizde büyük ölçüde var: monitör uygulamanın oynatıcısı (src/VidShrink.App/Editing/EditorView.axaml:13); sağ tık menüyü açar (src/VidShrink.App/Playback/Keymap.cs:112, PlayerView.axaml.cs:165-166 ve :417-422); menüde ses izi ve altyazı var (PlayerView.Menu.cs:23-24, PlayerView.Tracks.cs:257-270). Düzenleyici monitörde yalnız klavyeyi kapatıyor (EditorView.axaml.cs:42; kapı yalnız tuş yolunda: PlayerView.axaml.cs:446), fareyi değil. Monitör kendi satırında tek başına duruyor (EditorView.axaml:13) ve EditorKeymap.cs'te varsayılan olarak fare düğmesine bağlı komut yok (dosyada `PlayerButton.` araması boş; bakan yer :165-166). Kalan eksik görünür düğme ve seçimin teslime yansıması; bu kalan için oy kanıtı yok. İki rakip ve ana işin kenarı, yani 2. Menü düzenleyicide açılmıyorsa (gözle görülmedi) satır `yok`a, öncelik oy koluyla 1'e döner. |
| `DZA-11` | Öncelik 1 → 2. Kimde Var'a MKVToolNix, Kaynak'a [46] eklendi. En yakın kod düzeltildi. | Rakip kolu: tablo tek rakip sayıyordu, tek rakipte olan 1 olamaz (DOGRULAMA kural 5); ikinci rakip bulundu, üçüncüsü görülmedi. mkvmerge iz başına `--sync` seçeneği taşıyor [46]. LosslessCut 3.69 sürüm notu izlerin dosya başına kaydırılmasını (`-itsoffset`, #216) ekledi diyor (`gh api repos/mifi/lossless-cut/releases/tags/v3.69.0`). Oy kolu: #216 açık ve 16 oy. Bu sayı kolun kendi 15 eşiğini bir oyla geçiyor; ORTAK'ta eşik yok. Aynı listede akıllı kesim isteği 119 oyda (#126), yani 16 oy alanın öne çıkan talebi değil; istek tek üründe ve o ürün 3.69 ile karşıladı. İki rakip ve kap içi yan iş (ana işin kenarı), yani 2. Bizde gecikme yalnız çalarken var: src/VidShrink.App/Playback/PlayerView.Tracks.cs:104-108; `src` altında `itsoffset` araması boş. |
| `DZA-23` | Öncelik 2 → 3. | Tek rakip: LosslessCut. Kaynak [43] ikinci bir rakip değil, VidCutter'ın açık istek listesi (notlar dosyası satır 378). ORTAK ölçütünde tek rakip 3 alır. İş küçük olduğu için DZA-P11'e ek olarak girdi. |
| `DZA-33` | Bizde Durum `yok` → `yarım`. İş `orta` → `küçük`. | Hareketsiz yeri bulma ve kesme çekirdekte var: src/VidShrink.Core/IdleTrim.cs:29-41 ffmpeg'in `freezedetect` süzgecini kuruyor, :43-63 çıktısını okuyor. Kaydedici kayıt sonrası çağırıyor: src/VidShrink.App/Recorder/RecorderView.Kirpma.cs:16, src/VidShrink.Ffmpeg/IdleTrimmer.cs:10-36. Sessizlik taraması aynı `IdleSpan` türünü kullanıyor (IdleTrim.cs:6), yani düzenleyiciye üçüncü tarama türü olarak eklemek küçük iş. |
| `DZA-39` | Bizde Durum düzeltildi: "düzenleyicide yok" yerine "monitör menüsünde var". | Monitör aynı oynatıcı bileşeni (EditorView.axaml:13); menüsünde kare kaydet ve kareyi kopyala satırları var (src/VidShrink.App/Playback/PlayerView.Menu.cs:29-34). Eksik kalan: düzenleyici kısayolu (klavye kapalı: EditorView.axaml.cs:42) ve aralığı kare dizisi olarak kaydetme. |
| `DZA-18` | Bizde Durum düzeltildi: ad deseni uygulamada var, düzenleyici kullanmıyor. | src/VidShrink.Core/AdlandirmaDeseni.cs:18 (varsayılan desen), src/VidShrink.App/AppSettings.cs:49 (ayar), src/VidShrink.App/ShrinkJobWindow.axaml.cs:872 (Küçült'te uygulanıyor). Düzenleyici sabit sonek yazıyor: src/VidShrink.Core/Editing/EditOutputName.cs:9. |
| `DZA-02` | Bizde Durum'a Küçült'teki çok izli plan eklendi. | src/VidShrink.Core/StreamMapping.cs:201 (iz planı), :165 (ses izi), :174 (altyazı izi). Düzenleyici teslimi bu planı çağırmıyor, eşlemeyi elle yazıyor (EditExport.cs:541-542, :580-581, :656-657). |
| `DZA-05` | Bizde Durum'a ikinci kanıt eklendi; öncelik değişmedi. | Kayma EditExport.cs:533 ve :572'de hesaplanıyor; teslim notlarını kuran liste (src/VidShrink.App/Editing/EditorView.Teslim.cs:359-373) altı not taşıyor, kayma notu yok. |
| `DZA-06` | En yakın kod düzeltildi. | src/VidShrink.Core/Editing/EditTimeline.cs:235 verilen sınır listesinden bölüyor; otomatik bölme bu işleve sınır üretmekten ibaret. |
| `DZA-08` | En yakın kod eklendi. | Kaydedici bölüm yazıyor: src/VidShrink.Core/RecorderChapters.cs:91 üst veri dosyasını, :109 yeniden paketleme argümanlarını kuruyor; çağıran src/VidShrink.Ffmpeg/RecorderSession.cs:370-394. |
| `DZA-09` | En yakın kod düzeltildi. | Küçült'ün iş pompası genel yazılmış: src/VidShrink.Core/JobPump.cs:8. Tablo yalnız küçük resim kuyruğunu göstermişti. |
| `DZA-14` | Bizde Durum'a komut satırı eklendi. | src/VidShrink.Cli/CliRequest.cs:436-443 `--bolum` ile bölüm aralığını kesiyor; düzenleyicide karşılığı yok. |
| `DZA-25` | En yakın kod eklendi. | Küçült dış altyazı dosyasını çıktıya iz olarak ekliyor: src/VidShrink.Core/StreamMapping.cs:84 ve :805-808. Dış ses ekleme ve kayıpsız yol yok. |
| `DZA-26` | En yakın kod netleşti. | Dönüştür'ün yalnız ses çıktısı kodlayıcıyı kaba göre seçiyor, kopya seçeneği yok: src/VidShrink.App/MainWindow.axaml.cs:4782-4788. Argüman üreticisi ses kopyasını zaten tanıyor: src/VidShrink.Core/ConversionArguments.cs:244. |
| `DZA-40` | Kanıt eklendi. | Proje yalnız menüden açılıyor: src/VidShrink.App/Editing/EditorView.Proje.cs:202-211. `src` genelinde `vsproj` araması yalnız düzenleyici ve veri sıfırlama kodunda çıkıyor; kabuk kaydı yok. |

**Öncelik 1 Satırlarının Kaynak Doğrulaması**

Sekiz satır 1'de kaldı; her biri iki bağımsız kaynakta görüldü. Sayfalar WebFetch ile, sürüm notu ve oy sayıları `gh api` ile okundu.

- `DZA-01`: LosslessCut README [1] aynı ayarlı dosyaların kayıpsız birleştirilmesini sayıyor; mkvmerge [46] `+` ve `--append-to` ile ekliyor; SolveigMM [25], Bandicut [33] ve Shutter Encoder [44] birleştirmeyi yazıyor. Öncelik 1 kaldı.
- `DZA-02`: mkvmerge [46] seçim verilmezse o türün bütün izlerini kopyalıyor; smartcut [36] bütün ses izlerini yeniden kodlamadan taşıyor; SolveigMM sürüm geçmişi [26] 5.0'da çok sesli kesim ve birleştirmeyi yazıyor. Öncelik 1 kaldı.
- `DZA-03`: LosslessCut [1] gereksiz izleri çıkarmayı sayıyor; mkvmerge [46] `--audio-tracks`, `--subtitle-tracks` taşıyor; Shutter Encoder [44] ses izlerini tek tek kapatıyor. Öncelik 1 kaldı.
- `DZA-04`: smartcut [36] sesi yeniden kodlamadan geçiriyor (testlerinde MP3 ve Vorbis var); SolveigMM sürüm geçmişi [26] AC-3, E-AC-3, DTS, MP3, PCM ve Opus sesli dosya kesimini yazıyor. Öncelik 1 kaldı.
- `DZA-05`: Avidemux kaynağı [40] kopya kipinde kesim noktası anahtar karede değilse kaydetmeden önce uyarıp soruyor; LosslessCut belgesi [5] kesim başının önceki anahtar kareye yuvarlandığını yazıyor, 3.69 [2] kesim noktası çevresinde anahtar kare azsa uyarı ekledi. Öncelik 1 kaldı.
- `DZA-06`: LosslessCut [1] süreye, boyuta ve parça sayısına göre bölüyor; SolveigMM [25] parça, süre ya da boyuta göre dilimliyor; mkvmerge [46] `--split` taşıyor; Bandicut [33] eşit parçalara bölüyor. Öncelik 1 kaldı.
- `DZA-07`: TimeBolt [16] bulunan sahneyi tek tek açıp kapatıyor; Clipchamp [24] sessizlikleri tek tek silmeye izin veriyor. Öncelik 1 kaldı.
- `DZA-09`: TMPGEnc [29] projeleri toplu araca kaydedip art arda çıkarıyor; SolveigMM [25] toplu kipi yalnız Business sürümünde veriyor. LosslessCut README'si [1] kendisi "no mass/batch export yet" diyor; #89 açık ve 20 oy. Öncelik 1 kaldı.

## Çıkarılanlar

Çıkarılan satır yok. 44 satırın hiçbiri bizde tam olarak bulunmadı.

| No | Eksik | Neden | Kanıt (dosya:satır) |
|---|---|---|---|

## Yinelenenler

Kardeş tablo `duzenleyici-kurgu-eksikler.md` (DZB, 77 satır) ile karşılaştırıldı.

- DZA-17 = DZB-59 — kurguyu XML ya da EDL ile başka programa taşıma. Öncelikler ayrı düşmüş: burada 2, kardeş tabloda 3.
- DZA-39 = DZB-57 — düzenleyiciden kare kaydetme. Kardeş tablo `yok` diyor; monitör menüsündeki kare kaydet bulgusu (Düzeltmeler, DZA-39) o satır için de geçerli.
- DZA-42 = DZB-68 — konuşma dökümünden kesim; ikisi de K5 kararına bağlı.

Aynı eksik değil ama aynı koda dokunan üç yer daha var:

- DZA-15 ve DZA-16'nın EDL kolu DZB-59 ile aynı okuyucu ve yazıcıyı ister.
- DZA-25'in dış ses ekleme kolu DZB-01 (müzik izi) ile komşudur; biri kayıpsız iz ekler, öbürü sesi karıştırır.
- DZA-26'nın ses çıkarma kolu DZB-58 (yalnız ses dışa aktarma) ile komşudur.

## Doğrulanamayanlar

- DZA-10 ve DZA-39: monitörün sağ tık menüsü kodda bağlı ve düzenleyici onu kapatan bir ayar yazmıyor (monitöre yalnız üç ayar veriliyor: EditorView.Monitor.cs:24, EditorView.axaml.cs:42 ve :46). Ama uygulama çalıştırılmadı (yasak): menünün düzenleyicide açıldığı ve kesimden sonra önizleme yeniden yüklenince seçilen ses izinin korunduğu gözle görülmedi. Açılmıyorsa iki satır da `yok`a döner.
- DZA-13: Tam kipte kaynağın bölümlerinin çıktıya kaymamış zamanla geçip geçmediği ölçülemedi (ffmpeg koşmak yasak). `yok` hükmü EditExport.cs:387'deki açıklamaya ve teslim argümanlarında bölüm satırı bulunmamasına dayanıyor.
- DZA-04: TMPGEnc'te hangi ses biçimlerinin yeniden kodlanmadan geçtiği ürün ve özellik sayfalarında görülmedi; yalnız "en az kodlama" seçeneği ve Vorbis ile Opus için akıllı çıktı olmadığı yazıyor. İddia smartcut ve SolveigMM'de görüldü.
- DZA-05: SolveigMM'de uygulama içi bildirim görülmedi; [27] yalnız anahtar kare hassasiyetini anlatan belge.
- DZA-07: LosslessCut README'sinde bulunan parçaların tek tek kapatılabildiği yazılı görülmedi; Gling açılmadı.
- DZA-03: SolveigMM'de iz seçimi ürün sayfasında yok, sürüm geçmişinde var; MP4Box açılmadı.
- DZA-09: Bandicut ürün sayfasında toplu iş görülmedi; MKVToolNix'in iş kuyruğu [47] açılmadı.
- DZA-15: TimeBolt sayfasında EDL dışa aktarma görülmedi; XML var.
- DZA-27: Akıllı kip taramalı (interlaced, kareyi iki yarım alan olarak taşıyan) kaynağı dışarıda tutuyor (SmartCutCodec.cs:47). TV kayıtlarının çoğu taramalı; `orta` tahmini bu kapı açılmadan gerçekçi mi bilinmiyor.
- DZA-23: yeniden kodlamadan döndüren ikinci bir rakip aranmadı; bulunursa satır 2'ye döner.
- Öncelik 2 ve 3 satırlarının rakip iddiaları tek tek açılmadı; kural yalnız öncelik 1 için iki kaynak istiyor.

## Ek Notlar

- Otomatik kesim çok kaynakta yalnız ilk kaynağı kesiyor: `RemoveSource` kaynak sırasını varsayılan 0 ile alıyor (src/VidShrink.Core/Editing/EditTimeline.cs:192) ve panel sıra vermeden çağırıyor (src/VidShrink.App/Editing/EditorView.Sessizlik.cs:125-133).
- Teslim kutusu çıktı kabı olarak yalnız MP4 ve MKV sunuyor (src/VidShrink.App/Editing/EditorView.Teslim.cs:382-395); TS, MOV ya da WebM kaynağı kendi kabında kayıpsız teslim etme yolu görülmedi.
- Akıllı kip taramalı kaynağı kabul etmiyor (src/VidShrink.Core/Editing/SmartCutCodec.cs:47); tabloda bunun satırı yok. Rakip tarafı bu turda doğrulanmadı.
- Çok kaynakta "parçaları ayrı yaz" her parçayı kendi kaynağından istenen kiple çıkarıyor (EditExport.cs:263-298); yeniden kodlama yalnız tek dosyada birleştirirken zorunlu. DZA-P6 bu ayrımı kullanabilir.
- Notlardaki açık soru kapandı: a115 dalının "Premiere uyumu dalga 1" commit'i (680e6403) `main`de; içinde DZA satırlarından biri yok.

## Sekme Özeti

- Kesim çekirdeği pazarın ortasında: üç teslim kipi, dört kodekte akıllı kesim, sessizlik, siyah kare ve sahne taraması, çok kaynak var.
- Geride kaldığımız yer izler ve kap (dosyanın görüntü, ses ve altyazıyı bir arada tutan dış biçimi): teslim yalnız ilk görüntü ve ilk sesi taşıyor.
- En ağır üç eksik: izlerin düşmesi ve seçilememesi (DZA-02, DZA-03), çok kaynağın her kipte yeniden kodlanması (DZA-01), toplu teslimin olmaması (DZA-09).
- En ucuz üç kazanç: kayma bildirimi (DZA-05, sayı zaten hesaplanıyor), otomatik bölme (DZA-06, bölen kod var), önizlemede iz düğmesi (DZA-10, seçim menüde zaten var).
- Kodda ölçülünce hiçbir satır tam çıkmadı; üç satırda durumumuz tablonun dediğinden iyi (DZA-10, DZA-33, DZA-39).
- Üç satırın önceliği düştü (DZA-10, DZA-11, DZA-23); kalan sekiz öncelik 1 satırının hepsi iki bağımsız kaynakta görüldü.
- On iki paketten ikisi sahibin kararını bekliyor (DZA-P6, DZA-P7); on paket karar beklemeden başlayabilir.

## Paket Önerisi

Sıra önerilen yapım sırasıdır. Üç paket `büyük`; her birinin planında ikiye nereden bölüneceği yazılı.

### DZA-P1 — Hızlı Kip Kayma Bildirimi
- Satırlar: DZA-05, DZA-12, DZA-30
- İş: küçük
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.App/Editing/EditorView.Teslim.cs, src/VidShrink.App/Editing/EditorKeymap.cs, src/VidShrink.App/Editing/EditorView.Kisayol.cs, src/VidShrink.App/Editing/EditorView.Monitor.cs, src/VidShrink.App/Locales/
- Plan: Hızlı kip kesim başını zaten önceki anahtar kareye (kendi başına çözülebilen tam kare) çekiyor (EditExport.cs:533, :572); aradaki fark plana yazılır ve teslim notlarına "çıktı şu kadar saniye erken başlar" satırı olarak eklenir. Çizelgenin elindeki anahtar kare listesiyle (EditorTimeline.Serit.cs:36-46) önceki ve sonraki anahtar kareye giden iki komut ve monitörde "anahtar karede" göstergesi gelir. Doğrulama: bilinen bir anahtar kare listesiyle plandaki sayıyı ve komutların vardığı kareyi ölçen birim testleri.

### DZA-P2 — Otomatik Bölme Ve Parça Adları
- Satırlar: DZA-06, DZA-18, DZA-19, DZA-29, DZA-31
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Core/Editing/EditTimeline.cs, src/VidShrink.Core/Editing/EditClip.cs, src/VidShrink.Core/Editing/EditOutputName.cs, src/VidShrink.Core/Editing/EditProject.cs, src/VidShrink.Core/AdlandirmaDeseni.cs, src/VidShrink.App/Editing/EditorView.Sahne.cs, src/VidShrink.App/Editing/EditorView.axaml
- Plan: Eşit sayı, sabit süre ya da hedef boyut için sınır listesi üretilir ve var olan bölme işlevine verilir (EditTimeline.cs:235); boyut, kaynağın ortalama bit hızından hesaplanır. Klibe etiket alanı eklenir; etiket ve işaretçiler proje dosyasına yazılır (EditProject.cs:94-135), parça adları Küçült'ün ad deseniyle kurulur (AdlandirmaDeseni.cs:18). Sahne eşiği aynı bölme alanına sayı kutusu olarak girer. Doğrulama: sınır hesabı, proje gidiş dönüşü ve ad üretimi birim testleri.

### DZA-P3 — Tarama Aralıklarını Tek Tek Yönetme
- Satırlar: DZA-07, DZA-20, DZA-21, DZA-22
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Core/Editing/SilenceCut.cs, src/VidShrink.App/Editing/EditorView.Sessizlik.cs, src/VidShrink.App/Editing/EditorTimeline.cs, src/VidShrink.App/Editing/EditorView.axaml
- Plan: Tarama sonucu bugün topluca kesiliyor (EditorView.Sessizlik.cs:125-133); her aralık çizelgede tıklanır olur ve üç durumdan birini alır: kes, tut, hızlandır. Hızlandır, aralığı bölüp o parçanın hızını yükseltir; kenar payı ön ve arka diye ikiye ayrılır (SilenceCut.cs:29, :171-172), taranacak ses izi seçilir (SilenceCut.cs:90'daki `-map 0:a:0` yerine seçilen iz). Doğrulama: aralık durumlarından çıkan kesim listesini ölçen birim testleri ve başsız pencere testi.

### DZA-P4 — Akıllı Kipte Ses Kodekleri
- Satırlar: DZA-04
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Core/Editing/EditExport.cs, tools/VidShrink.Bench
- Plan: Ses kapısı AAC ve koşullu Opus dışını Tam kipe düşürüyor (EditExport.cs:116-124). AC-3, E-AC-3 ve MP3 için bugünkü parça yolu (MPEG-TS ara dosyası, EditExport.cs:719-729) açılır; MPEG-TS'in (yayın akışı kabı) taşımadığı kodeklerde ara kap Matroska olur. Her kodek için üç ve daha çok kesimli örnekte ses ile görüntü arasındaki kayma `tools/VidShrink.Bench` ile ölçülür; eşiği aşan kodek kapının dışında kalır.

### DZA-P5 — İz Seçim Paneli
- Satırlar: DZA-03, DZA-10
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.Core/Editing/EditProject.cs, src/VidShrink.App/Editing/EditorView.Teslim.cs, src/VidShrink.App/Editing/EditorView.Monitor.cs, src/VidShrink.App/Playback/PlayerView.Tracks.cs
- Plan: Teslim kutusuna kaynağın izlerini listeleyen panel gelir; varsayılan bugünkü gibi ilk görüntü ve ilk ses kalır, bu yüzden karar gerekmez. Seçim teslimin elle yazılmış `-map` satırlarına taşınır (EditExport.cs:541-542, :580-581, :656-657; ses parçası :725) ve projeye yazılır. Monitöre dinlenen iz için görünür düğme eklenir; oynatıcının var olan seçimi kullanılır (PlayerView.Tracks.cs:111-113) ve önizleme yeniden yüklenince korunur. Doğrulama: seçimden çıkan argümanları ölçen birim testleri ve çıktıda ffprobe ile iz sayımı.

### DZA-P6 — Kayıpsız Birleştirme Yolu
- Satırlar: DZA-01
- İş: orta
- Karar: sahip: çok kaynakta Hızlı ve Akıllı kip, kaynaklar uyumluysa yeniden kodlamak yerine kopyalasın mı (K7 her zaman yeniden kodlamayı seçmişti)
- Dokunacağı yer: src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.Core/Editing/SmartCutCodec.cs, src/VidShrink.App/Editing/EditorView.Teslim.cs, src/VidShrink.App/Locales/
- Plan: Çok kaynak bugün kipe bakmadan yeniden kodlama yoluna gidiyor (EditExport.cs:252). Kaynaklar arası uyum denetimi eklenir: kodek, profil, çözünürlük, piksel biçimi, kare hızı, ses kodeği ve örnekleme hızı. Hepsi tutuyorsa parçalar ffmpeg'in concat girişiyle (dosyaları yeniden kodlamadan uç uca okuyan liste) kopyalanır; tutmuyorsa bugünkü yol ve notu kalır. Doğrulama: uyum denetimi birim testleri, örnek çıktıda kare sayısı ve süre.

### DZA-P7 — Tüm İzleri Varsayılan Taşıma
- Satırlar: DZA-02
- İş: orta
- Karar: sahip: teslim varsayılanı "ilk görüntü ve ilk ses" yerine "bütün izler" olsun mu
- Dokunacağı yer: src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.App/Editing/EditorView.Teslim.cs
- Plan: DZA-P5'in paneli üstüne gelir: varsayılan seçim bütün ses, altyazı ve veri izleri olur. Altyazı ve ek ses izleri üç kipte de kesim aralığına göre kırpılır; Akıllı kipte her ses izi kendi parça dosyasıyla taşınır. Doğrulama: çok sesli ve altyazılı örnekte ffprobe ile iz sayısı ve süre eşitliği.

### DZA-P8 — Bölüm Yazma Ve Koruma
- Satırlar: DZA-08, DZA-13, DZA-14
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.Core/RecorderChapters.cs, src/VidShrink.Core/MediaInfo.cs, src/VidShrink.Ffmpeg/FfprobeClient.cs, src/VidShrink.App/Editing/EditorView.Kaynak.cs
- Plan: Çizelgeden bölüm listesi üreten saf bir işlev yazılır: parça sınırları (ad, DZA-P2 yapıldıysa parça etiketinden gelir) ve kaynağın kesimle kaymış bölümleri. Liste, Kaydedici'nin kullandığı yoldan (ffmpeg'in üst veri dosyası, RecorderChapters.cs:91 ve :109) teslimin son adımına girer. Açılışta kaynağın bölümleri (MediaInfo.cs:61) işaretçi ya da kesim noktası olarak önerilir; önce defterdeki Türkçe bölüm adı kusuru (.claude/acik.md:14) kapanır. Doğrulama: kaymış zaman hesabı birim testleri ve çıktıda ffprobe ile bölüm sayımı.

### DZA-P9 — Komut Satırı Ve Toplu Teslim
- Satırlar: DZA-28, DZA-09
- İş: büyük
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Cli/CliRequest.cs, src/VidShrink.Core/Editing/EditProject.cs, src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.Core/JobPump.cs, src/VidShrink.App/Editing/
- Plan: Önce pencere açmadan "proje dosyası girer, teslim çıkar" koşan çekirdek işlev ayrılır ve komut satırına bağlanır; bugün komut satırı yalnız tek aralık kesiyor (CliRequest.cs:425-435). Sonra düzenleyiciye proje ve dosya sırası gelir; sıra Küçült'ün iş pompasıyla (JobPump.cs:8) art arda koşar, bir işin düşmesi ötekileri durdurmaz. Bölünecekse önce komut satırı (orta), sonra sıra. Doğrulama: komut satırı argüman testleri ve sahte teslimle sıra testi.

### DZA-P10 — Kesim Listesi Ve Kurgu Aktarımı
- Satırlar: DZA-15, DZA-16, DZA-17
- İş: büyük
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Core/Editing/, src/VidShrink.App/Editing/EditorView.Proje.cs
- Plan: Parça listesini dış biçimlere çeviren tek bir okuyucu ve yazıcı modülü yazılır: dışa CSV, EDL (kurgu programlarının düz metin kesim listesi) ve YouTube bölüm metni; içe CSV, EDL ve CUE. Aynı modül kesilmiş çizelgeyi Premiere Pro ile DaVinci Resolve'un açtığı Final Cut XML'ine de yazar. Bölünecekse önce liste biçimleri (orta), sonra XML (orta). Doğrulama: her biçim için gidiş dönüş birim testi; XML'in hedef programda açıldığını sahip gözle dener.

### DZA-P11 — Kap İçi İz İşleri
- Satırlar: DZA-24, DZA-11, DZA-25, DZA-26, DZA-23, DZA-44
- İş: büyük
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Core/Editing/, src/VidShrink.Core/StreamMapping.cs, src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.App/Editing/EditorView.Teslim.cs
- Plan: Görüntüye dokunmadan dosyayı yeniden paketleyen (`-c copy`) tek bir argüman üreticisi yazılır; DZA-P5'in iz paneli onun arayüzü olur. Üretici iz başına dil, ad ve varsayılan bayrağını, gecikmeyi (`-itsoffset`), döndürme bayrağını (`-display_rotation`), başka dosyadan iz ve kapak resmi eklemeyi, seçili izi ayrı dosyaya çıkarmayı kurar. Bölünecekse önce üst veri, kaydırma ve döndürme; sonra iz ekleme, çıkarma ve kapak. Doğrulama: argüman birim testleri ve çıktıda ffprobe ile üst veri okuması.

### DZA-P12 — Akıllı Kipte MPEG-2
- Satırlar: DZA-27
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Core/Editing/SmartCutCodec.cs, src/VidShrink.Core/Editing/EditExport.cs, tools/VidShrink.Bench
- Plan: Akıllı kipin kodek kapısına (SmartCutCodec.cs:43) MPEG-2 eklenir; kenar parçası kaynağın profiline, kare hızına ve bit hızına uyan `mpeg2video` kodlayıcısıyla yazılır. TV kayıtlarının çoğu taramalı olduğundan taramalı kaynak kapısı (SmartCutCodec.cs:47) birlikte ele alınmazsa kazanç dar kalır; bu kayıtların sesi de genellikle AC-3 ya da MP2 (yayıncılığın eski ses kodeği) olduğundan paket DZA-P4'ten sonra anlam kazanır. Doğrulama: birleşim noktasında kare sayısı ve bozulma ölçümü `tools/VidShrink.Bench` ile.

## Pakete Girmeyenler

- DZA-32: tek rakip ve dar kullanım; DZA-P11'in yeniden paketleme üreticisi çıktıktan sonra küçük bir ek olur.
- DZA-33: çekirdeği hazır ama DZA-P3 zaten dolu; o paket bittikten sonra yarım günlük ek.
- DZA-34: büyük iş, tek rakip, yalnız TV kaydına yarıyor.
- DZA-35: Akıllı kipin renk kapısını açmak ölçüm ister, kitle dar.
- DZA-36: defterde zaten açık; önce ölçülmesi gerekiyor, kod işi sonra belli olur.
- DZA-37: büyük iş ve sahip kararı; DZA-P6'nın kararı çıkmadan anlamı yok.
- DZA-38: tek rakip; bit akışı süzgeci kodek başına ayrı iş ister.
- DZA-39: büyük kısmı monitör menüsünde zaten var, kalan dar.
- DZA-40: sahip kararı (yeni uzantı ve kurucu kaydı) ister, tek rakip.
- DZA-41: tek rakip, dar kitle.
- DZA-42: kapalı, K5.
- DZA-43: kapalı, K1.
