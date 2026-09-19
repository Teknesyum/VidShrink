# HandBrakeCLI Bayraklarının Hükmü — 19 Eylül 2026

`docs/handbrake/envanter-tamlik-2026-09-18.md` envanterde adı hiç geçmeyen 154 bayrağı
saymıştı ama hüküm vermemişti. Bu belge her birine bakıyor: karşılığı var mı, yoksa
gerekli mi, yoksa ürüne uymuyor mu.

Ölçüm dört ayrı koşumda yapıldı, her KAPALI satır `dosya:satır` kanıtı taşıyor. Yedi
KAPALI satırı bağımsız denetledim, yedisi de gösterdiği satırda duruyordu.

## Sayı

| Hüküm | Sayı |
| --- | --- |
| KAPALI — karşılığı var | 64 |
| AÇIK — yok, kullanıcıya değer katar | 35 |
| KAPSAM DIŞI — ürüne uymuyor | 55 |
| Toplam | 154 |

## Hükmü belirleyen üç bulgu

**Süzgeç motoru var, yüzeyi yok.** `src/VidShrink.Core/VideoFilterChain.cs` kırpma,
keskinleştirme, gürültü giderme, deblock, deband, döndürme, dolgu ve gri tonlamayı tam
üretiyor; `Parse` (`:270`) bunları metin belirtiminden okuyor. Ama `src/` altında `Parse`'ı
çağıran tek satır yok, `new PlanOptions` kuran üç yerin hiçbiri `Filters` vermiyor. Canlı
olan tek süzgeç otomatik çözgü (`EncodeRunner.cs:116`). On kadar AÇIK satırın sebebi tek
ve aynı: uç açılmamış.

**Piksel en-boy oranı hiç okunmuyor.** Depoda `sample_aspect_ratio` ya da `setsar` geçen
satır yok. E4 ile DVD kolu açıldı; anamorfik 720x480 bir kaynak kare piksel sayılıp yanlış
orana ölçeklenir. `--non-anamorphic`, `--auto-anamorphic`, `--itu-par`,
`--keep-display-aspect` bu tek eksikte birleşiyor.

**`--no-<süzgeç>` ailesinin çoğu bizde anlamsız.** HandBrake'te bu bayraklar ön ayarın
açtığını geri kapatır; VidShrink'in ön ayar kartında süzgeç alanı yok, süzgeçler varsayılan
kapalı. Tek istisna çözgü: kendiliğinden açılıyor ve kapatma ucu yok.

## Açık kalan 35 bayrak

`--arate`, `--auto-anamorphic`, `--cfr`, `--chroma-smooth`, `--crop`, `--crop-mode`, `--deblock`, `--denoise`, `--detelecine`, `--drc`, `--encoder-level-list`, `--encoder-profile-list`, `--first-subtitle`, `--grayscale`, `--hdr-dynamic-metadata`, `--hqdn3d`, `--max-duration`, `--nlmeans`, `--no-bwdif`, `--no-deinterlace`, `--no-metadata`, `--non-anamorphic`, `--normalize-mix`, `--pad`, `--pfr`, `--preset-import-file`, `--preset-list`, `--rotate`, `--srt-burn`, `--srt-file`, `--ssa-burn`, `--ssa-file`, `--subtitle-burned`, `--subtitle-lang-list`, `--unsharp`

## Tam tablo

| Bayrak | HandBrake işi | VidShrink karşılığı | Hüküm |
| --- | --- | --- | --- |
| `--ab` | Ses izinin bit hızını kbit/s cinsinden belirler. | `src/VidShrink.App/MainWindow.axaml:495` (CmbAdvAudioKbps), `src/VidShrink.Core/PlanCalculator.cs:442`, `src/VidShrink.Core/StreamMapping.cs:126` | KAPALI |
| `--ac` | Kodlayıcıya özgü ses sıkıştırma ölçütünü (ör. FLAC sıkıştırma düzeyi) ayarlar. | yok | KAPSAM DIŞI — ölçüt yalnız kayıpsız/özel kodlayıcılarda anlamlı; VidShrink hedef boy için aac/opus/mp3'ü sabit bit hızıyla sürüyor (`src/VidShrink.Core/StreamMapping.cs:126`), sıkıştırma düzeyi boy hesabını değiştirmiyor. |
| `--adither` | Ses kodlamadan önce uygulanacak dither türünü seçer (yalnız flac16/alac16/pcm16). | yok | KAPSAM DIŞI — destekleyen kodlayıcılar HandBrake yardımında flac16/alac16/pcm16 ile sınırlı; VidShrink'in küçültme kolu bu üçünü hiç üretmiyor (`src/VidShrink.Core/StreamMapping.cs:154-157`). |
| `--aencoder` | Çıkıştaki ses kodlayıcısını (av_aac, ac3, copy:…) seçer. | `src/VidShrink.App/MainWindow.axaml:1021-1027` (CmbConvertAudio), `src/VidShrink.Core/PlanCalculator.cs:1684`, `src/VidShrink.Core/StreamMapping.cs:298` | KAPALI |
| `--align-av` | Akışların tam aynı anda başlaması için başa sessizlik/siyah kare ekler. | `src/VidShrink.Core/StreamMapping.cs:143` (`SesHizalama`), uygulandığı yer `src/VidShrink.Core/StreamMapping.cs:127` | KAPALI |
| `--all-subtitles` | Dil listesine uyan bütün altyazı izlerini seçer. | `src/VidShrink.Core/StreamMapping.cs:303` — kaynaktaki her altyazı izi üzerinde dönülüyor, süzme yok | KAPALI |
| `--aname` | Ses izlerinin adını elle belirler. | kaynak adı taşınıyor: `src/VidShrink.Core/StreamMapping.cs:105` | KAPSAM DIŞI — kaynağın iz adı zaten çıkışa yazılıyor; adı elle yazdırmak çok izli kurgu/yeniden paketleme aracının işi, hedef boya sıkıştıran üründe karar noktası değil. |
| `--angle` | DVD/Blu-ray'de video açısını seçer. | `src/VidShrink.Cli/CliRequest.cs:259` (`--aci`/`--angle`), `src/VidShrink.Core/SourceTitles.cs:37` (`-angle`) | KAPALI |
| `--aq` | Ses kalite ölçütünü (VBR kalite) ayarlar. | yok | KAPSAM DIŞI — hedef boy hesabı sesin bit hızını bilmek üzerine kurulu (`src/VidShrink.Core/PlanCalculator.cs:833`, `src/VidShrink.Core/EncodePlan.cs:127`); değişken kaliteli ses bütçeyi belirsizleştirir. |
| `--arate` | Ses örnekleme frekansını belirler (8–192 kHz veya auto). | yok — küçültme kolunda hiçbir yerde `-ar` üretilmiyor | AÇIK |
| `--audio` | Hangi ses izlerinin alınacağını seçer ("none", "1,2,3"). | `src/VidShrink.App/MainWindow.axaml:506` (ChkAdvKeepTracks), `src/VidShrink.Core/StreamMapping.cs:247-250` (hepsi ya da birincil iz), `src/VidShrink.Core/PlanParser.cs:112` (sesi düşürme) | KAPALI |
| `--audio-fallback` | Kopyalanamayan izde kullanılacak yedek kodlayıcıyı belirler. | `src/VidShrink.Core/StreamMapping.cs:364` (kopya/opus yerine aac), `src/VidShrink.Core/StreamMapping.cs:150` (`NeverPassedThrough`), `src/VidShrink.Core/StreamMapping.cs:298` (kaba göre kodlayıcı) | KAPALI |
| `--auto-anamorphic` | Depolama çözünürlüğünü azamiye çıkaran piksel en-boy oranını saklar. | yok — depoda `sample_aspect_ratio` okuyan ya da yazan tek satır yok (tarama: `src/**/*.cs`) | AÇIK |
| `--automatic-naming-behaviour` | Ses izlerinin otomatik adlandırma davranışını seçer (off/unnamed/all). | yok | KAPSAM DIŞI — HandBrake ön ayarının kendi adlandırma kuralı; VidShrink iz adını üretmiyor, yalnız kaynaktakini taşıyor (`src/VidShrink.Core/StreamMapping.cs:105`). |
| `--bwdif` | Videoyu FFmpeg bwdif ile tarama giderir. | `src/VidShrink.Core/VideoFilterChain.cs:87` (`idet,bwdif=mode=send_frame:parity=auto:deint=interlaced`) | KAPALI |
| `--cfr` | Çıkışı sabit kare hızına oturtur (vfr/pfr alternatifleriyle birlikte). | kısmî: `src/VidShrink.Core/VideoFilterChain.cs:198` yalnız kare hızı **düşürülürken** `fps=` ekliyor; sabit/değişken seçimi hiçbir yerde sunulmuyor, `-fps_mode`/`-vsync` üretilmiyor | AÇIK |
| `--chapters` | Kodlanacak bölüm aralığını seçer. | `src/VidShrink.Cli/CliRequest.cs:228` (`--bolum`/`--chapters`) | KAPALI |
| `--chroma-smooth` | Renk (chroma) düzleştirme süzgecini ön ayarla uygular. | yok — süzgeç zincirinde chroma smooth yok (`src/VidShrink.Core/VideoFilterChain.cs:174-200`) | AÇIK |
| `--chroma-smooth-tune` | chroma smooth ön ayarlarını ince ayarlar (tiny…verywide). | yok | KAPSAM DIŞI — yalnız `--chroma-smooth` ön ayarlarına etki eden alt ayar; ana süzgeç açılmadan bağımsız bir işi yok, açılırsa onunla birlikte gelir. |
| `--color-matrix` | Çıkışın bildirdiği renk uzayını (2020/709/601) dönüşüm yapmadan işaretler. | `src/VidShrink.Core/HdrResolver.cs:35` ve `:45-47` (`-colorspace`/`-color_primaries`/`-color_trc`), yazıldığı yer `src/VidShrink.Core/FfmpegArguments.cs:525`; SDR karşılığı `src/VidShrink.Core/VideoFilterChain.cs:207` | KAPALI |
| `--color-range` | Çıkışın renk aralığını seçer (auto/limited/full). | tonemap kolunda sabit: `src/VidShrink.Core/HdrResolver.cs:22` (`…:r=limited`) | KAPSAM DIŞI — küçültme kolu her zaman limited üretiyor; full aralık seçimi yayın/üretim zinciri kararı, hedef boya sıkıştıran üründe kullanıcı kararı değil. |
| `--colorspace` | Renk uzayı/transfer/primaries dönüştürür, tonemap içerir. | `src/VidShrink.Core/HdrResolver.cs:22` (zscale+tonemap zinciri), karar `src/VidShrink.Core/PlanCalculator.cs:377`, uygulama `src/VidShrink.Core/VideoFilterChain.cs:189` | KAPALI |
| `--comb-detect` | Karelerdeki tarama izlerini saptar; decomb/deinterlace ile birlikte seçici çalıştırır. | `src/VidShrink.Core/VideoFilterChain.cs:87` (zincirin başındaki `idet`), yoklama `src/VidShrink.Core/VideoFilterChain.cs:105` + `src/VidShrink.Ffmpeg/InterlaceProbe.cs:32`, çağrı `src/VidShrink.Ffmpeg/EncodeRunner.cs:116-119` | KAPALI |
| `--crop` | Görüntüyü piksel cinsinden kırpar (varsayılanı siyah bantları otomatik atmak). | motorda var, kullanıcı yolu yok: `src/VidShrink.Core/VideoFilterChain.cs:181` (`crop=`), yoklama `src/VidShrink.Ffmpeg/CropProbe.cs:15`; üretimde çağıran yok — `tests/VidShrink.Tests/OluUyeTests.cs:876` | AÇIK |
| `--crop-mode` | Kırpma kipini seçer: auto / conservative / none / custom. | yok — `src/VidShrink.Core/VideoFilterChain.cs:181` yalnız verilen dikdörtgeni uyguluyor, kip kavramı yok | AÇIK |
| `--crop-threshold-frames` | Akıllı kırpmayı tetiklemek için kaç karenin farklı olması gerektiğini belirler. | `src/VidShrink.Ffmpeg/CropProbe.cs:12-13` (SamplePoints=10, FramesPerPoint=2 sabit) | KAPSAM DIŞI — otomatik kırpma yoklamasının iç eşiği; kullanıcıya ayar olarak sunulacak bir karar değil, kırpma bağlandığında sabitleriyle gelir. |
| `--crop-threshold-pixels` | En-boy farkı sayılması için gereken piksel farkını belirler. | `src/VidShrink.Ffmpeg/CropProbe.cs:14` (`Limit = 24`) | KAPSAM DIŞI — aynı gerekçe: `cropdetect` eşiği motorun iç sabiti. |
| `--custom-anamorphic` | Piksel en-boy oranını akışta saklayıp tüm değişkenleri elle denetler. | yok | KAPSAM DIŞI — elle PAR kurgusu uzman kodlama aracının işi; VidShrink çözünürlüğü hedef boydan kendisi türetiyor (`src/VidShrink.Core/PlanCalculator.cs:1213`). |
| `--deblock` | avfilter deblock ile blok izlerini giderir. | motorda var, kullanıcı yolu yok: `src/VidShrink.Core/VideoFilterChain.cs:90` + `:183`; kuran tek yer `src/VidShrink.Core/VideoFilterChain.cs:298` (`Parse`) ve `Parse` üretimde çağrılmıyor | AÇIK |
| `--deblock-tune` | deblock ön ayarını small/medium/large olarak ayarlar. | yok — güç sabit: `src/VidShrink.Core/VideoFilterChain.cs:90` (`filter=strong:block=8`) | KAPSAM DIŞI — ana süzgecin alt ayarı; deblock kullanıcıya açıldığında onunla birlikte ele alınır. |
| `--decomb` | yadif/blend/cubic/EEDI2 karışımıyla yalnız taramalı kareleri düzeltir. | `src/VidShrink.Core/VideoFilterChain.cs:87` — `idet` + `deint=interlaced`, yani yalnız taramalı işaretli kareler işleniyor | KAPALI |
| `--deinterlace` | Videoyu FFmpeg yadif ile tarama giderir. | `src/VidShrink.Core/VideoFilterChain.cs:87`, otomatik karar `src/VidShrink.Core/VideoFilterChain.cs:53` (varsayılan `Auto`) + `src/VidShrink.Ffmpeg/EncodeRunner.cs:116-119` | KAPALI |
| `--denoise` | `--hqdn3d`'nin eski adı: hqdn3d ile gürültü giderir. | motorda var, kullanıcı yolu yok: `src/VidShrink.Core/VideoFilterChain.cs:224-226` (hqdn3d light/medium/strong), kuran yer `src/VidShrink.Core/VideoFilterChain.cs:289` (`Parse`, üretimde çağrılmıyor) | AÇIK |
| `--detelecine` | pullup ile telecine'i geri alır (ivtc). | motorda var, kullanıcı yolu yok: `src/VidShrink.Core/VideoFilterChain.cs:88` (`fieldmatch,yadif,decimate`) + `:180`; varsayılan kapalı (`src/VidShrink.Core/VideoFilterChain.cs:54`), açan üretim yolu yok | AÇIK |
| `--disable-hw-decoding` | Donanım çözmeyi kapatır, yazılım çözmeye zorlar. | `src/VidShrink.Core/FfmpegArguments.cs:462-463` + `:437` — kodek listede değilse `-hwaccel` hiç yazılmıyor (motorun otomatik kararı) | KAPALI |
| `--display-width` | Custom anamorphic için gösterim genişliğini belirler. | yok | KAPSAM DIŞI — `--custom-anamorphic`'in alt ayarı; PAR kurgusu ürünün kapsamı dışında (yukarıdaki satırla aynı gerekçe). |
| `--drc` | Sese ek dinamik aralık sıkıştırması uygular (kısık sesleri yükseltir). | yok — küçültme kolunda ses süzgeci yalnız hizalama (`src/VidShrink.Core/StreamMapping.cs:127`) | AÇIK |
| `--enable-hw-decoding` | nvdec/qsv ile donanım çözmeyi açar. | `src/VidShrink.Core/FfmpegArguments.cs:462-463` (`-hwaccel auto`), kodek listesi `:434` | KAPALI |
| `--encoder` | Video kodlayıcısını seçer. | `src/VidShrink.Cli/CliRequest.cs:189` (`--kodek`/`--codec`), arayüz `src/VidShrink.App/MainWindow.axaml:624` (CmbAdvCodecLock) ve `:911` (CmbConvertCodec) | KAPALI |
| `--encoder-level` | Kodlayıcıyı istenen kodek seviyesine (ör. 4.1) uydurur (help:181-183). | `src/VidShrink.Core/PlanParser.cs:150` — `-level:v` beyaz listede, `src/VidShrink.Core/EncodePlan.cs:116` `ExtraArgs`'a girer, `src/VidShrink.Core/FfmpegArguments.cs:543` argümana yazar; kullanıcı yüzeyi plan JSON kutusu (`src/VidShrink.App/MainWindow.axaml:1204`, `src/VidShrink.App/MainWindow.axaml.cs:4353`) | KAPALI |
| `--encoder-level-list` | Seçilen kodlayıcının geçerli seviyelerini listeler (help:184-186). | yok — geçerli küme yalnız kodda bir regex (`src/VidShrink.Core/PlanParser.cs:150`), kullanıcıya gösterilen liste yok | AÇIK |
| `--encoder-preset` | Hız/verim ön ayarı (help:160-162). | `src/VidShrink.Cli/CliRequest.cs:41`, `src/VidShrink.Cli/CliRequest.cs:200-204` (`--on-ayar`), `src/VidShrink.Core/FfmpegArguments.cs:489`, arayüz `src/VidShrink.App/MainWindow.axaml:592` | KAPALI |
| `--encoder-preset-list` | Kodlayıcının ön ayar merdivenini listeler (help:163-165). | `src/VidShrink.Core/FfmpegArguments.cs:366` (`PresetLadder`), açılır kutu `src/VidShrink.App/MainWindow.axaml.cs:1688` ve aday listesi `src/VidShrink.App/MainWindow.axaml.cs:141-145` | KAPALI |
| `--encoder-profile` | Çıktıyı istenen kodek profiline (main, high10…) uydurur (help:175-177). | `src/VidShrink.Core/PlanParser.cs:149` — `-profile:v` beyaz listede (baseline/main/high/high10/main10); ayrıca motorun otomatik kararı `src/VidShrink.Core/CodecModel.cs:183-184` ve yazımı `src/VidShrink.Core/FfmpegArguments.cs:522-523` | KAPALI |
| `--encoder-profile-list` | Kodlayıcının geçerli profillerini listeler (help:178-180). | yok — beş değerlik küme yalnız `src/VidShrink.Core/PlanParser.cs:149`'da, kullanıcıya gösterilmiyor | AÇIK |
| `--encoder-tune` | Kaynak türüne göre ince ayar (help:166-168). | `src/VidShrink.Core/FfmpegArguments.cs:415-421` (`TunesFor`), yazımı `src/VidShrink.Core/FfmpegArguments.cs:490-492`, arayüz `src/VidShrink.App/MainWindow.axaml:599` | KAPALI |
| `--encoder-tune-list` | Kodlayıcının tune merdivenini listeler (help:169-171). | `src/VidShrink.Core/FfmpegArguments.cs:415-421`, açılır kutu `src/VidShrink.App/MainWindow.axaml.cs:1691` ve aday listesi `src/VidShrink.App/MainWindow.axaml.cs:147-151` | KAPALI |
| `--encopts` | Kodlayıcıya `a=1:b=2` biçiminde ileri seçenek geçirir (help:172-174). | `src/VidShrink.Core/PlanParser.cs:142-153` + `src/VidShrink.Core/FfmpegArguments.cs:543` — **kısmi**: yalnız beş anahtar (`-tune`, `-profile:v`, `-level:v`, `-aq-mode`, `-aq-strength`), serbest dizge değil | KAPALI |
| `--first-audio` | Dil listesine uyan ilk ses izini seçer (help:238-240). | `src/VidShrink.Core/StreamMapping.cs:214-221` (`PrimaryAudio`) ve `src/VidShrink.Core/StreamMapping.cs:250` — tek ses izi, tercih edilen dil önde; motorun otomatik kararı | KAPALI |
| `--first-subtitle` | Dil listesine uyan ilk altyazıyı seçer (help:659-662). | yok — `src/VidShrink.Core/StreamMapping.cs:303-340` kapsayıcıya sığan **bütün** metin altyazılarını taşıyor; sayıyı ya da dili sınırlayan seçenek yok | AÇIK |
| `--format` | Kapsayıcı seçer, verilmezse çıktı adından türetir (help:110-115). | `src/VidShrink.Core/StreamMapping.cs:186-199` (uzantıdan kapsayıcı) ve arayüz `src/VidShrink.App/MainWindow.axaml:889-896` (MP4/MKV/WebM/MOV/AVI/GIF/MP3) | KAPALI |
| `--gain` | Kodlamadan önce sesi dB olarak yükseltir/kısar (help:318-322). | yok | KAPSAM DIŞI — ürün hedef boya sıkıştırır; ses seviyesini **düzenlemek** ayrı bir iş. Oynatıcıdaki ses düzeyi (`src/VidShrink.App/Playback/PlayerView.axaml.cs`, `VolumeLevel`/`SetVolume`) yalnız dinlemeyi etkiler, kodlanan akışa girmez |
| `--grayscale` | Gri tonlamalı kodlama (help:642). | `src/VidShrink.Core/VideoFilterChain.cs:89` ve `:193` — motor tarafı hazır, ama **yüzey yok**: `VideoFilterChain.Parse` yalnız testlerden çağrılıyor, CLI'da bayrak, arayüzde kutu yok | AÇIK |
| `--hdr-dynamic-metadata` | HDR10+/Dolby Vision dinamik verisini taşır (help:212-216). | **kısmi ve yetmiyor**: `src/VidShrink.Core/HdrResolver.cs:44-57` yalnız statik HDR10'u (master-display, max-cll) taşıyor; dinamik veri yolu yok | AÇIK |
| `--height` | Depolama yüksekliğini piksel olarak belirler (help:350). | `src/VidShrink.Core/PlanCalculator.cs:93-98` (`FixedResolution`, kısa kenar) ve arayüz `src/VidShrink.App/MainWindow.axaml:959-979` (2160/1440/1080/720/480/özel) | KAPALI |
| `--hqdn3d` | hqdn3d ile gürültü azaltma, dört güç kademesi (help:479-491). | `src/VidShrink.Core/VideoFilterChain.cs:222-226` (`hqdn3d=…`, üç kademe) — motorda var, **yüzey yok** (grayscale ile aynı boşluk) | AÇIK |
| `--inline-parameter-sets` | Her IDR öncesine SPS/PPS gömerek uyarlamalı yayına uygun çıktı üretir (help:130-132). | yok | KAPSAM DIŞI — ürün tek dosya teslim ediyor; DASH/HLS paketleme yolu yok, MP4 tarafında yapılan tek şey `+faststart` (`src/VidShrink.Core/FfmpegArguments.cs:542`) |
| `--input` | Kaynak dosya/aygıt (help:73). | `src/VidShrink.Cli/CliRequest.cs:284-288` (konumsal girdi) ve arayüzde bırakma alanı `src/VidShrink.App/MainWindow.axaml:223-230` | KAPALI |
| `--ipod-atom` | MP4'e iPod 5G uyum atomu ekler (help:121). | yok | KAPSAM DIŞI — 2005 donanımı için uyum atomu; ürünün uyum yüzeyi platform teslimi (`src/VidShrink.App/MainWindow.axaml:439`, `ChkWhatsAppCompatible`) |
| `--itu-par` | Gevşek/özel anamorfikte daha geniş ITU piksel oranı (help:388-390). | yok | KAPSAM DIŞI — üründe anamorfik PAR yüzeyi hiç yok; çıktı kare piksel ve `src/VidShrink.Core/PlanParser.cs:78` en-boy oranının korunmasını zorunlu kılıyor |
| `--keep-aname` | Kaynak ses izi adlarını geçirir (help:335). | `src/VidShrink.Core/StreamMapping.cs:264` ve `:299` (`source.Title`), yazımı `src/VidShrink.Core/StreamMapping.cs:103-105` — varsayılan davranış | KAPALI |
| `--keep-display-aspect` | Özel anamorfikte kaynağın görüntüleme oranını korur (help:380-381). | yok | KAPSAM DIŞI — anamorfik yolu yok (bkz. `--itu-par`); oran zaten kare pikselde korunuyor (`src/VidShrink.Core/PlanParser.cs:78`) |
| `--keep-duplicate-titles` | Taramada yinelenen başlıkları tutar, yalnız Blu-ray (help:82-83). | yok | KAPSAM DIŞI — başlık envanteri Blu-ray yapısından değil ffprobe akışlarından kuruluyor (`src/VidShrink.Core/SourceTitles.cs:49-78`); "yinelenen başlık" kavramı bu kaynakta oluşmuyor |
| `--keep-metadata` | Kaynağın ortak üstverisini korur (help:126). | `src/VidShrink.Core/StreamMapping.cs:115` — `-map_metadata 0` her çıktıda yazılıyor, varsayılan | KAPALI |
| `--keep-subname` | Kaynak altyazı izi adlarını geçirir (help:675). | `src/VidShrink.Core/StreamMapping.cs:316` (`source.Title` taşınıyor), yazımı `src/VidShrink.Core/StreamMapping.cs:107-112` | KAPALI |
| `--lapsharp` | lapsharp çekirdeğiyle keskinleştirme (help:572-585). | `src/VidShrink.Core/VideoFilterChain.cs:232-237` (unsharp) | KAPSAM DIŞI — keskinleştirme motorda unsharp ile karşılanıyor; ikinci bir keskinleştirici çekirdeği küçültme aracına değer katmıyor (açık olan yüzey eksiği unsharp'ın kendisinde, `--hqdn3d` satırında sayıldı) |
| `--lapsharp-tune` | lapsharp'ı içerik türüne göre ayarlar (help:587-596). | yok | KAPSAM DIŞI — dayandığı filtre kapsam dışı (üstteki satır) |
| `--loose-anamorphic` | Kaynağa en yakın PAR'ı saklar (help:372-373). | yok | KAPSAM DIŞI — anamorfik PAR yüzeyi yok (bkz. `--itu-par`) |
| `--main-feature` | Ana içerik başlığını bulup seçer (help:81). | `src/VidShrink.Cli/CliRequest.cs:67-68`, `src/VidShrink.Cli/CliRequest.cs:255-258` (`--ana-icerik`), `src/VidShrink.Core/SourceTitles.cs:66` | KAPALI |
| `--markers` | Bölüm imlerini ekler (help:116). | `src/VidShrink.Core/StreamMapping.cs:115` — `-map_chapters 0` varsayılan; kesitli kodlamada bilerek düşürülür (`src/VidShrink.Core/FfmpegArguments.cs:540`) | KAPALI |
| `--max-duration` | Bu süreden uzun başlıkları envanterden düşürür (help:78-79). | yok — `src/VidShrink.Core/SourceTitles.cs:55` yalnız asgari süreyi eliyor, üst sınır kolu yok | AÇIK |
| `--maxHeight` | Yükseklik tavanı (help:365-366). | `src/VidShrink.Core/PlanCalculator.cs:93-98` — `FixedResolution` kısa kenarı sabitler ve yukarı ölçekleme yapmaz, yani tavan gibi davranır; arayüz `src/VidShrink.App/MainWindow.axaml:959` | KAPALI |
| `--maxWidth` | Genişlik tavanı (help:367-368). | `src/VidShrink.Core/PlanCalculator.cs:93-98` (dikey videoda kısa kenar genişliktir) + `src/VidShrink.Core/Olcek.cs:31-37`; arayüz `src/VidShrink.App/MainWindow.axaml:979` (özel `1280x720`) | KAPALI |
| `--min-duration` | Bu süreden kısa başlıkları atlar (help:76-77). | `src/VidShrink.Cli/CliRequest.cs:73-74`, `src/VidShrink.Cli/CliRequest.cs:266-271` (`--asgari-sure`), `src/VidShrink.Core/SourceTitles.cs:55` | KAPALI |
| `--mixdown` | Ses karışım düzeni: mono, stereo, dpl2, 5point1… (help:294-305). | `src/VidShrink.Core/EncodePlan.cs:13` (`AudioChannelOverride`), `src/VidShrink.Core/PlanCalculator.cs:84`, `src/VidShrink.Core/StreamMapping.cs:129` (`-ac`), arayüz `src/VidShrink.App/MainWindow.axaml:502` — **kısmi**: kanal sayısı var (oto/stereo/mono/sessiz), dpl1/dpl2 matris karışımları yok | KAPALI |
| `--modulus` | Depolama kenarlarını bu sayının katına indirir (help:391-393). | `src/VidShrink.Core/Olcek.cs:16-37`, `src/VidShrink.Cli/CliRequest.cs:205-212` (`--modul`), arayüz `src/VidShrink.App/MainWindow.axaml:606` | KAPALI |
| `--multi-pass` | Çok geçişli kodlama (help:189-190). | `src/VidShrink.Core/FfmpegArguments.cs:364` (`NeedsTwoPasses`), `src/VidShrink.Core/FfmpegArguments.cs:513-517` (`-pass`/`-passlogfile`), koşum `src/VidShrink.Ffmpeg/EncodeRunner.cs:196-199` — motorun otomatik kararı | KAPALI |
| `--native-dub` | `--native-language` ile: ses izi seçilmemişse ana dile uyan ilk sesi seçer (help:711-716). | `src/VidShrink.Core/StreamMapping.cs:218-219` — dil eşleşen ses izi önce seçiliyor; dil `src/VidShrink.Cli/CliApp.cs:60` ve `src/VidShrink.App/MainWindow.axaml.cs:1951` ile arayüz dilinden geliyor | KAPALI |
| `--native-language` | Tercih edilen dili söyler; ilk ses izi o dilde değilse o dildeki ilk altyazıyı seçer. | `src/VidShrink.Core/StreamMapping.cs:206-219` (dil eşleşmesi, `PrimaryAudio`); dil otomatik geliyor: `src/VidShrink.Cli/CliApp.cs:60`, `src/VidShrink.App/MainWindow.axaml.cs:1951` | KAPALI |
| `--nlmeans` | NLMeans ile gürültü giderir (ultralight/light/medium/strong ön ayarları). | `src/VidShrink.Core/VideoFilterChain.cs:217-219` — motor üç kademeyi üretiyor, çağıran yok | AÇIK |
| `--nlmeans-tune` | NLMeans'i içerik türüne göre ayarlar (film, grain, animation, tape…). | yok | KAPSAM DIŞI — VidShrink gürültü gidermeyi üç güç kademesine indirgemiş (`VideoFilterChain.cs:213-230`); içerik türü ayarı hedef-boy planına girmiyor, ürünün sadelik hattının dışında. |
| `--no-bwdif` | Ön ayarın açtığı bwdif çözgüsünü kapatır. | yok — VidShrink'in çözgüsü zaten bwdif (`VideoFilterChain.cs:87`) ve otomatik açılıyor (`EncodeRunner.cs:116-119`); `deinterlace=off` yalnız `VideoFilterChain.cs:286`'da, ulaşılamıyor | AÇIK |
| `--no-chroma-smooth` | Ön ayarın açtığı chroma smooth süzgecini kapatır. | yok | KAPSAM DIŞI — `chroma_smooth` ffmpeg'de yok (yerel `ffmpeg -filters` ölçümü); VidShrink'te açılabilen bir kroma süzgeci olmadığı için kapatılacak bir şey de yok. |
| `--no-comb-detect` | Ön ayarın açtığı tarak tespitini kapatır. | yok — `idet` ayrı bir kalem değil, çözgü zincirinin başında (`VideoFilterChain.cs:87`) | KAPSAM DIŞI — VidShrink'te comb-detect kullanıcıya açılan bir süzgeç değil, çözgü kararının iç adımı; tek başına kapatılabilir bir kalem değil. |
| `--no-deblock` | Ön ayarın açtığı deblock süzgecini kapatır. | yok | KAPSAM DIŞI — deblock varsayılan kapalı (`VideoFilterChain.cs:59`) ve hiçbir ön ayar onu açmıyor (`PresetLibrary.cs:20-34`); kapatılacak bir şey yok. |
| `--no-decomb` | Ön ayarın açtığı decomb süzgecini kapatır. | yok | KAPSAM DIŞI — decomb HandBrake'in kendi yadif/EEDI2 bileşimi; VidShrink hiç kullanmıyor. |
| `--no-deinterlace` | Ön ayarın açtığı yadif çözgüsünü kapatır. | yok — çözgü kaynak taraklıysa ya da idet öyle derse **otomatik** açılıyor (`VideoFilterChain.cs:121-135`, `EncodeRunner.cs:116-119`); kullanıcının kapatma ucu yok | AÇIK |
| `--no-detelecine` | Ön ayarın açtığı detelecine'i kapatır. | yok | KAPSAM DIŞI — detelecine varsayılan kapalı (`VideoFilterChain.cs:55`), hiçbir ön ayar açmıyor; kapatılacak bir şey yok. |
| `--no-dvdnav` | DVD okumada dvdnav kitaplığını kullanmaz. | yok — VidShrink DVD'yi ffmpeg'in `dvdvideo` demuxer'ıyla okuyor (`src/VidShrink.Core/SourceTitles.cs:35`) | KAPSAM DIŞI — dvdnav/libdvdread seçimi HandBrake'in kendi okuma yoluna ait; VidShrink'te böyle bir kol yok. |
| `--no-grayscale` | Ön ayarın açtığı gri kodlamayı kapatır. | yok | KAPSAM DIŞI — gri varsayılan kapalı (`VideoFilterChain.cs:62`), hiçbir ön ayar açmıyor. |
| `--no-hdr-dynamic-metadata` | HDR10+/Dolby Vision dinamik meta veri taşımasını kapatır. | `src/VidShrink.Core/HdrResolver.cs:51` — yalnız statik HDR10 (`hdr10-opt=1`) korunuyor, dinamik meta veri zaten taşınmıyor | KAPSAM DIŞI — bayrağın istediği davranış VidShrink'in hâlihazırdaki davranışı; açılıp kapatılacak bir kol yok. |
| `--no-hqdn3d` | Ön ayarın açtığı hqdn3d'yi kapatır. | yok | KAPSAM DIŞI — hqdn3d varsayılan kapalı (`VideoFilterChain.cs:56`), hiçbir ön ayar açmıyor. |
| `--no-ipod-atom` | Ön ayarın eklediği iPod 5G uyumluluk atomunu eklemez. | yok | KAPSAM DIŞI — iPod 5G atomu VidShrink'in hedef kitlesi değil; kap kararı mp4/mkv/webm ile sınırlı. |
| `--no-itu-par` | Loose/custom anamorfikte geniş ITU PAR değerlerini kullanmaz. | yok | KAPSAM DIŞI — VidShrink PAR/anamorfiğe hiç dokunmuyor; kaynak taraması `setsar`/`sample_aspect` geçen tek satır bulamadı. |
| `--no-keep-aname` | Kaynak ses izi adlarının taşınmasını kapatır. | `src/VidShrink.Core/StreamMapping.cs:104-105` — ad her zaman taşınıyor | KAPSAM DIŞI — taşıma koşulsuz; adı düşürmek hedef-boya ya da uyumluluğa hiçbir şey katmıyor. |
| `--no-keep-display-aspect` | Custom anamorfikte kaynak görüntü en-boyunu korumayı kapatır. | yok | KAPSAM DIŞI — anamorfik kip yok (bkz. `--no-itu-par`). |
| `--no-keep-subname` | Kaynak altyazı izi adlarının taşınmasını kapatır. | `src/VidShrink.Core/StreamMapping.cs:110-111` — ad her zaman taşınıyor | KAPSAM DIŞI — taşıma koşulsuz; düşürmenin karşılığı yok. |
| `--no-lapsharp` | Ön ayarın açtığı lapsharp keskinleştirmesini kapatır. | yok | KAPSAM DIŞI — `lapsharp` ffmpeg'de yok (yerel `ffmpeg -filters` ölçümü), HandBrake'in kendi süzgeci. |
| `--no-markers` | Ön ayarın eklediği bölüm işaretlerini eklemez. | `src/VidShrink.Core/StreamMapping.cs:115` (`-map_chapters 0`), kesitte otomatik düşüyor: `src/VidShrink.Core/FfmpegArguments.cs:540` (`dropChapters: plan.Trim is not null`) | KAPSAM DIŞI — bölüm işaretleri tam kodlamada korunuyor, kesitte zaten düşüyor; ayrı bir kapatma kolu kullanıcıya değmiyor. |
| `--no-metadata` | Ön ayarın koruduğu kaynak meta verisini taşımaz. | yok — `src/VidShrink.Core/StreamMapping.cs:115` kaynak meta verisini koşulsuz kopyalıyor (`-map_metadata 0`) | AÇIK |
| `--no-multi-pass` | Çok geçişli kipi kapatır. | `src/VidShrink.App/MainWindow.axaml:577` + `src/VidShrink.App/MainWindow.axaml.cs:1921` — Gelişmiş'te Otomatik/CRF/İki geçiş; CRF seçimi tek geçiş demek | KAPALI |
| `--no-nlmeans` | Ön ayarın açtığı NLMeans'i kapatır. | yok | KAPSAM DIŞI — nlmeans varsayılan kapalı (`VideoFilterChain.cs:56`), hiçbir ön ayar açmıyor. |
| `--no-optimize` | Ön ayarın açtığı mp4 hızlı başlatmasını kapatır. | `src/VidShrink.Core/FfmpegArguments.cs:541-542` — mp4 ailesinde `+faststart` koşulsuz açık | KAPSAM DIŞI — hızlı başlatma paylaşım odaklı çıktıda her zaman doğru karar; kapatmanın kullanıcıya karşılığı yok. |
| `--no-turbo` | İki geçişte turbo ilk geçişi kapatır. | `src/VidShrink.Core/PlanCalculator.cs:839` — turbo yalnız `SpeedMode.Fast`'te açılıyor; hızlı kip seçilmediğinde kapalı (CLI `--hizli/--fast`, arayüzde GPU kutusu) | KAPALI |
| `--no-unsharp` | Ön ayarın açtığı unsharp keskinleştirmesini kapatır. | yok | KAPSAM DIŞI — unsharp varsayılan kapalı (`VideoFilterChain.cs:58`), hiçbir ön ayar açmıyor. |
| `--non-anamorphic` | Piksel en-boy oranını 1:1'e sabitler. | yok — depoda `setsar`/`sample_aspect` geçen satır yok; SAR'a hiç dokunulmuyor, ffmpeg kaynağınkini taşıyor | AÇIK |
| `--normalize-mix` | Ses karışım seviyelerini kırpılmayı önleyecek şekilde normalleştirir. | yok — VidShrink surround'u stereoya indiriyor (`src/VidShrink.Core/StreamMapping.cs:295`, `:129` `-ac`) ama normalizasyon uygulamıyor; `loudnorm`/`dynaudnorm` kaynakta geçmiyor | AÇIK |
| `--optimize` | mp4'ü HTTP akışı için optimize eder (moov atomu başa). | `src/VidShrink.Core/FfmpegArguments.cs:541-542` (`-movflags +faststart`), ayrıca `:457` ve `:712` | KAPALI |
| `--output` | Hedef dosya adını belirler. | `src/VidShrink.Cli/CliRequest.cs:213-215` (`--cikti` / `--output` / `-o`) | KAPALI |
| `--pad` | Görüntüye kenarlık ekler (letterbox); renk ve konum ayarlanabilir. | `src/VidShrink.Core/VideoFilterChain.cs:194-196` — motor `pad=` üretiyor (siyah sabit), çağıran yok | AÇIK |
| `--pfr` | Tepe sınırlı kare hızı: `-r`'nin üstüne çıkmaz ama altında kaynak zamanlamasını bozmaz. | yok — kare hızı yalnız bütçe gerektirince düşüyor ve `fps=` ile CFR'e çevriliyor (`src/VidShrink.Core/VideoFilterChain.cs:197-198`, karar `src/VidShrink.Core/PlanCalculator.cs:556-560`) | AÇIK |
| `--pixel-aspect` | Custom anamorfik için PAR'ı elle belirler. | yok | KAPSAM DIŞI — elle PAR yazımı bir yazarlık kalemi; VidShrink'in hedef-boy planı kare piksel üstüne kurulu (`PlanCalculator` yalnız Width/Height ile çalışıyor). |
| `--preset` | Ön ayarı adıyla seçer. | `src/VidShrink.Core/PresetLibrary.cs:100` (`Find`), arayüz çipleri `src/VidShrink.App/MainWindow.axaml:302-376`, kullanıcı ön ayarları `src/VidShrink.App/MainWindow.OnAyar.cs:58`. Not: CLI'deki `--on-ayar/--preset` (`CliRequest.cs:200-203`) ffmpeg **hız** ön ayarıdır, HandBrake'in ön ayarı değil. | KAPALI |
| `--preset-export` | Komut satırı seçeneklerinden yeni bir ön ayar üretir. | `src/VidShrink.App/MainWindow.OnAyar.cs:205` (`CurrentPreset` → `SaveUser`), düğme `src/VidShrink.App/MainWindow.axaml:376-391` | KAPALI |
| `--preset-export-description` | Üretilen ön ayara açıklama ekler. | yok — `PresetProfile`'da açıklama alanı yok (`src/VidShrink.Core/PresetLibrary.cs:20-34`: Id, Name, Kind, Chip, …) | KAPSAM DIŞI — ön ayar kartı ad ve çip etiketiyle sınırlı tutulmuş; serbest açıklama alanı ürünün ön ayar modelinde yok. |
| `--preset-export-file` | Üretilen ön ayarı verilen dosyaya yazar. | `src/VidShrink.Core/PresetLibrary.cs:214` (`Export`) + `:120` (`DefaultUserPath`), çağrı `src/VidShrink.App/MainWindow.OnAyar.cs:205,237`. Dosya adı kullanıcı seçimi değil, sabit kullanıcı ön ayar dosyası. | KAPALI |
| `--preset-import-file` | Bir json ön ayar dosyasından ön ayar içe aktarır. | `src/VidShrink.Core/PresetLibrary.cs:206` (`Import`) ve `:330` (`HandBrakePresetImport.TranslateFile`) — ikisinin de `src/` altında çağıranı yok; açılışta yalnız sabit yoldaki dosya okunuyor (`MainWindow.OnAyar.cs:58`), HandBrake dosyası hata döndürüyor (`PresetLibrary.cs:138`) | AÇIK |
| `--preset-import-gui` | GUI'nin ön ayar yapılandırma dosyasından ön ayarları içe alır. | yok (ön ayar yalnız kodlayıcı hız adı: `src/VidShrink.Cli/CliRequest.cs:200`) | KAPSAM DIŞI — VidShrink'te taşınabilir ön ayar dosyası kavramı yok; `--on-ayar` tek bir kodlayıcı hız adıdır, içe alınacak bir GUI ön ayar deposu bulunmuyor. |
| `--preset-list` | Kullanılabilir ön ayarları listeler. | yok (yardım metni yalnız örnek ad veriyor: `src/VidShrink.Cli/Locales/en.json:2`) | AÇIK |
| `--previews` | Kaç önizleme resmi üretileceğini ve diske yazılıp yazılmayacağını seçer. | `src/VidShrink.Core/PreviewSegment.cs:42`, `src/VidShrink.App/MainWindow.axaml:667` | KAPALI |
| `--quality` | Video kalite değerini (CRF) belirler. | `src/VidShrink.Cli/CliRequest.cs:194` (`--crf`), kalite puanı `src/VidShrink.Cli/CliRequest.cs:183` | KAPALI |
| `--queue-import-file` | GUI'nin ürettiği kodlama kuyruğu dosyasını içe alır. | yok (toplu iş klasör izleyicisiyle: `src/VidShrink.Cli/CliRequest.cs:165`) | KAPSAM DIŞI — VidShrink'in toplu işi kuyruk dosyası değil `izle` komutu; dışarıdan alınacak bir kuyruk biçimi üretmiyor. |
| `--rate` | Çıktı kare hızını belirler. | `src/VidShrink.Core/VideoFilterChain.cs:198`, arayüzde `src/VidShrink.App/MainWindow.axaml:991`, izin kutusu `src/VidShrink.App/MainWindow.axaml:527` | KAPALI |
| `--rotate` | Görüntüyü döndürür ya da eksenlerinde çevirir. | motorda var, kullanıcı yüzeyi yok: `src/VidShrink.Core/VideoFilterChain.cs:240`, ayrıştırıcı `src/VidShrink.Core/VideoFilterChain.cs:301`; `VideoFilterChain.Parse` yalnız testlerden çağrılıyor | AÇIK |
| `--scan` | Yalnız seçili başlığı tarar, kodlamaz. | `src/VidShrink.Cli/CliRequest.cs:244`, `src/VidShrink.Core/SourceTitles.cs` | KAPALI |
| `--srt-burn` | Seçili dış SRT altyazıyı video karesine gömer. | yok | AÇIK |
| `--srt-codeset` | Dış SRT dosyalarının karakter kod sayfasını bildirir. | yok (dış altyazı yalnız oynatıcıya yükleniyor: `src/VidShrink.App/Playback/PlayerView.Subtitles.cs:154`) | KAPSAM DIŞI — dış altyazı dosyası kodlama borusuna hiç girmiyor; kod sayfası, yazılacak bir çıktı izi olmadığı için karşılık gelecek kol bulmuyor. |
| `--srt-default` | Seçili dış SRT'yi varsayılan altyazı olarak işaretler. | yok (aynı gerekçe: `src/VidShrink.App/Playback/PlayerView.Subtitles.cs:154`) | KAPSAM DIŞI — dış SRT çıktıya yazılmadığı için üstüne konacak varsayılan bayrağı da yok. |
| `--srt-file` | Dış SRT dosyalarını kodlamaya iz olarak ekler. | yok; oynatıcıda yükleme var (`src/VidShrink.App/Playback/PlayerView.Subtitles.cs:154`), çıktıya yazma yok | AÇIK |
| `--srt-lang` | Dış SRT izinin ISO 639-2 dil etiketini verir. | yok (aynı gerekçe) | KAPSAM DIŞI — çıktıda etiketlenecek dış altyazı izi üretilmiyor. |
| `--srt-offset` | Dış SRT'ye milisaniye kayması uygular. | oynatıcıda gecikme var: `src/VidShrink.App/Playback/PlayerView.Tracks.cs:87`; kodlamada yok | KAPSAM DIŞI — kayma oynatma tarafında zaten ayarlanıyor; çıktıya gömülen bir dış altyazı olmadığı için kodlama tarafında yeri yok. |
| `--ssa-burn` | Seçili dış SSA/ASS altyazıyı video karesine gömer. | yok | AÇIK |
| `--ssa-default` | Seçili dış SSA'yı varsayılan altyazı yapar. | yok | KAPSAM DIŞI — `--srt-default` ile aynı gerekçe. |
| `--ssa-file` | Dış SSA/ASS dosyalarını kodlamaya iz olarak ekler. | yok | AÇIK |
| `--ssa-lang` | Dış SSA izinin dil etiketini verir. | yok | KAPSAM DIŞI — `--srt-lang` ile aynı gerekçe. |
| `--ssa-offset` | Dış SSA'ya milisaniye kayması uygular. | oynatıcıda gecikme: `src/VidShrink.App/Playback/PlayerView.Tracks.cs:87` | KAPSAM DIŞI — `--srt-offset` ile aynı gerekçe. |
| `--start-at` | Saniye/kare/pts cinsinden verilen noktadan kodlamaya başlar. | `src/VidShrink.Cli/CliRequest.cs:217` (`--kes FROM-TO`, saniye, saat biçimi ya da `300f`) | KAPALI |
| `--start-at-preview` | Kodlamayı belirli bir önizleme resminden başlatır. | yok; kesit saniye/kare (`src/VidShrink.Cli/CliRequest.cs:217`) ya da bölüm (`src/VidShrink.Cli/CliRequest.cs:228`) ile veriliyor | KAPSAM DIŞI — numaralanmış önizleme resmi kümesi VidShrink'te hiç üretilmiyor; başlangıç noktası zaman/kare/bölümle zaten adreslenebiliyor. |
| `--stop-at` | Verilen süre/kare/pts sonra kodlamayı durdurur. | `src/VidShrink.Cli/CliRequest.cs:217` (aralığın bitiş ucu) | KAPALI |
| `--subname` | Altyazı izlerine ad yazar. | kaynağın iz adı olduğu gibi taşınıyor: `src/VidShrink.Core/StreamMapping.cs:335` | KAPSAM DIŞI — ürün sıkıştırıyor, etiket düzenlemiyor; kaynaktaki ad korunuyor, yeni ad yazma yüzeyi yok. |
| `--subtitle` | Kodlanacak altyazı izlerini virgüllü listeyle seçer. | `src/VidShrink.Core/StreamMapping.cs:303` (metin altyazıların tümü taşınıyor), MKV'de tüm izler `src/VidShrink.App/MainWindow.axaml:506` | KAPALI |
| `--subtitle-burned` | Seçili altyazıyı video karesine gömer. | yok; platform teslimde altyazı düşüyor (`src/VidShrink.Core/PlanCalculator.cs:1696`) | AÇIK |
| `--subtitle-default` | Seçili altyazıyı varsayılan olarak işaretler. | `src/VidShrink.Core/StreamMapping.cs:55` (kaynağın `default` bayrağı `-disposition`'a yazılıyor) | KAPALI |
| `--subtitle-forced` | Yalnız forced bayraklı altyazıyı gösterir/işaretler. | `src/VidShrink.Core/StreamMapping.cs:57` (`forced` bayrağı korunuyor) | KAPALI |
| `--subtitle-lang-list` | Kaynaktan hangi dillerin seçileceğini listeler. | yok (dil süzgeci hiçbir katmanda yok; hepsi taşınıyor `src/VidShrink.Core/StreamMapping.cs:303`) | AÇIK |
| `--title` | Kodlanacak başlığı numarayla seçer. | `src/VidShrink.Cli/CliRequest.cs:247`, `src/VidShrink.Core/SourceTitles.cs` | KAPALI |
| `--turbo` | Çok geçişli kodlamada ilk geçişi hızlı seçeneklerle koşar. | `src/VidShrink.Core/PlanCalculator.cs:839` (hızlı kipte açılıyor), `src/VidShrink.Core/FfmpegArguments.cs:532`, güvenlik kapısı `src/VidShrink.Core/CodecModel.cs:251` | KAPALI |
| `--unsharp` | Unsharp süzgeciyle görüntüyü keskinleştirir. | motorda var, kullanıcı yüzeyi yok: `src/VidShrink.Core/VideoFilterChain.cs:234`; `VideoFilterChain.Parse` yalnız testlerden çağrılıyor | AÇIK |
| `--unsharp-tune` | Unsharp ön ayarını ince tonlar (ultrafine…verycoarse). | keskinlik üç kademe: `src/VidShrink.Core/VideoFilterChain.cs:232` | KAPSAM DIŞI — VidShrink kolları kademeli tutuyor; süzgeç katsayısına ikinci bir ince ayar ekseni ürünün ayar dilinde karşılıksız. |
| `--vb` | Video bit hızını kbit/s olarak belirler. | motorun otomatik kararı: `src/VidShrink.Core/FfmpegArguments.cs:503` (`-b:v`), hedef boydan hesap `src/VidShrink.Core/PlanCalculator.cs` (`VideoKbitFor`) | KAPALI |
| `--verbose` | Günlük ayrıntı düzeyini açar. | `src/VidShrink.Cli/CliRequest.cs:158` (`--gunluk/--log`), `src/VidShrink.Core/Gunluk.cs:89` | KAPALI |
| `--version` | Sürümü yazar. | `src/VidShrink.Cli/CliRequest.cs:156` | KAPALI |
| `--vfr` | Değişken kare hızını seçer (kaynak zamanlaması korunur). | `src/VidShrink.Core/VideoFilterChain.cs:197` — kare hızı düşürülmedikçe `fps=` eklenmiyor, kaynak zamanlaması olduğu gibi kalıyor | KAPALI |
| `--width` | Depolama genişliğini piksel olarak belirler. | `src/VidShrink.App/MainWindow.axaml:531` (sabit çözünürlük), ölçekleme `src/VidShrink.Core/VideoFilterChain.cs:187`, modül `src/VidShrink.Cli/CliRequest.cs:205` | KAPALI |
