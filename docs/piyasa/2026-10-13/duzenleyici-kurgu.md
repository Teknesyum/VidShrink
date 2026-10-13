# Düzenleyici Sekmesi — Kurgu (NLE) Yönü

13 Ekim 2026. Kod okuması `origin/main` 9de48d8b.

NLE, parçaları bir zaman çizelgesinde istediğin sırada dizip kestiğin kurgu programıdır
(Premiere, DaVinci Resolve, Shotcut gibi). Bu belge düzenleyicimizin o yönünü pazarla kıyaslar.

## Envanter

Düzenleyici bugün **tek sıralı bir kesim listesi** üstünde çalışıyor. Bir video izi (V1), ona
bağlı bir ses izi (A1) ve bir metin izi (T1) var. İkinci bir video ya da ses izi yok; parçalar
üst üste binemez, yalnız art arda dizilir.

Yollar kısaltıldı: `Core/` = `src/VidShrink.Core/Editing/`, `App/` = `src/VidShrink.App/Editing/`.

### Kodda Olanlar

**Sekme ve model**

- Sekme: `src/VidShrink.App/MainWindow.axaml:218` (`TabEditor`).
- Zaman tabanı saniyede 240 000 adım: `Core/EditClip.cs:7-9`.
- Klip kaydı kaynak aralığını, hızı (0,01–100), ters oynatmayı, klip ayarlarını ve kaynak
  sırasını tutar: `Core/EditClip.cs:26`, `:28-29`, `:52`, `:54`, `:59`.
- Çizelge işlemleri `Core/EditTimeline.cs`: kaynak ekle `:53`, böl `:115`, sil `:127`, taşı `:134`,
  aralık sil `:148`, kaynağı kaldır `:192`, çoklu sil `:362`, geri al `:548`, yinele `:558`.

**İzler**

- Üç sabit iz: T1 metin, V1 video, A1 ses. Adlar `App/EditorTimeline.cs:21-22`, başlık çizimi
  `:539-541`. İz ekleme, kilitleme, susturma, tek dinleme (solo) yok.

**Araçlar ve kırpma**

- Üç araç var: Seçim, Jilet, Kaydırmalı Kırpma. `App/EditorTool.cs:3-8`.
- Kaydırmalı kırpma (ripple: kırpınca sonraki parçalar boşluğu kapatacak biçimde kayar) baştan
  ve sondan: `Core/EditTimeline.cs:302`, `:313`. Düz kenar kırpma `:346`.
- Yakalama (parça kenarına mıknatıs gibi oturma) `App/EditorTimeline.cs:444`; işaretçiler `:155`,
  `:402`; jilet `:762`.
- Roll, slip ve slide kırpma araçları yok (açıklamaları eksik tablosunda).

**Klip ayarları**

- Kırpma, 90 derece döndürme, yatay ve dikey çevirme, ses kazancı (−60…+20 dB), sessize alma,
  giriş ve çıkış solması, oran kırpması: `Core/ClipEffects.cs:15-35`, `:96`.
- ffmpeg karşılıkları: geometri `Core/ClipFilters.cs:38`, görüntü solması `:69`, ses `:79`,
  önizleme için zaman pencereli sürüm `:95`.
- Renk ayarı (parlaklık, kontrast, doygunluk), bulanıklık, yeşil perde gibi efekt yok.

**Hız**

- Klip başına sabit hız ve ters oynatma: `Core/EditTimeline.cs:402`, `:424`; `Core/EditClip.cs:52`.
- Klibin içinde değişen hız (hız rampası) ve kare dondurma yok.

**Metin**

- Model: yazı tipi adı, boyut, renk, kalın, italik, giriş ve çıkış solması, konum için
  canlandırma anahtar kareleri (iki nokta arası doğrusal): `Core/TextLayer.cs:7`, `:15`,
  `:43-57`, `:92`.
- Tek `.ass` dosyası (bir altyazı biçimi) hem önizlemeye hem teslime gider:
  `Core/AssWriter.cs:113-126`, `:197`, `:240-253`.
- Arayüzde yalnız içerik, boyut ve renk var: `App/EditorView.axaml:98`, `:102`, `:106`.
  Yazı tipi seçimi, kalın ve italik, konum, anahtar kare denetimi arayüzde yok. Model
  destekliyor ama `App/EditorView.Metin.cs` içinde yalnız yazı tipi adı okunuyor (`:191`, `:205`).
- Metin klibini çizelgede sürükleme ve kırpma var: `App/EditorTimeline.Metin.cs:29`, `:135`.

**Önizleme**

- libmpv (oynatma kitaplığı) `edl://` adresiyle sanal bir zaman çizelgesi oynatır:
  `Core/EdlPreview.cs:17`, `:19`. EDL, hangi kaynağın hangi aralığının hangi sırada
  oynayacağını söyleyen kesim listesidir.

**Teslim**

- Üç kip: Hızlı (kopyalar), Akıllı (yalnız kenarları yeniden kodlar), Tam (her şeyi yeniden
  kodlar). `Core/EditExport.cs:6-10`.
- Metin, klip ayarı, ses ayarı ve çok kaynak Tam kipi zorlar: `Core/EditExport.cs:36-45`, `:213`.
- Tam kipte kodlama sabit: `libx264`, `-crf 18`, AAC 192k. `Core/EditExport.cs:68-70`,
  argümanlar `:366-367`, `:496-497`. Kodek, çözünürlük, kare hızı, kalite ya da ön ayar seçimi yok.
- Çok kaynak `concat` süzgeciyle uç uca eklenir: `Core/EditExport.cs:252`, `:305`, `:350`, `:482`.
- Bölümleri ayrı dosyalara yazma: `App/EditorView.Teslim.cs:69`, `App/EditorView.axaml:83`.
- Metadata silme: `App/EditorView.axaml:84`.

**Proje**

- `.vsproj.json` proje dosyası ve otomatik kayıt: `Core/EditProject.cs:39-43`, `:362`, `:377`;
  `App/EditorView.Proje.cs:20`, `:143`, `:152`.

**Şerit ve yardımcılar**

- Küçük resim şeridi ve kodek anahtar karesi çentikleri: `App/EditorTimeline.Serit.cs:28`,
  `:114`, `:133`; `Core/TimelineStrip.cs:27`, `:88`.
- Ses dalga biçimi: `Core/AudioPeaks.cs:9`, çizimi `App/EditorTimeline.cs:1098`.
- Sahne değişimiyle bölme: `Core/SceneSplit.cs:9`, `App/EditorView.Sahne.cs:22`.
- Kısayollar yeniden atanabilir; komut listesi `App/EditorKeymap.cs:9-47`.

### Plan Adımlarının Durumu

| Belge | Adım | Durum |
|---|---|---|
| `docs/plan-duzenleyici.md` | D0 ortak odak, D1 kesim listesi, D2 `edl://` önizleme, D3 çizelge denetimi, D5 kısayollar | Bitti |
| `docs/plan-duzenleyici.md` | D4 teslim | Bitti; tek açık madde `-force_key_frames` ve `-g` ile kendi çıktımızı kayıpsız kesilebilir yapmak (komşu kol) |
| `docs/plan-metin-katmani.md` | T1 izi, "Metin ekle", özellik paneli (içerik, boyut, renk) | Bitti |
| `docs/rapor-metin-katmani.md` | Monitörde sürüklenebilir çerçeve, oynatma başında anahtar kare yazma | Açık: model var, arayüzde yok |
| `docs/plan-k7-cok-kaynak.md` | Çok kaynak ve birleştirme | Bitti |
| `docs/plan-k7-cok-kaynak.md` | Geçiş, çok izli çizelge, ses karıştırma | Planın dışında bırakıldı; hiçbiri başlamadı |

### Önceki Taramanın Eskiyen Satırları

`docs/piyasa/tarama-2026-10-05.md` Düzenleyici tablosunda (satır 116–149) beş satır eskidi:

- "Birden çok kaynağı birleştirme: Yok" → artık var (`Core/EditTimeline.cs:53`, `Core/EditExport.cs:252`).
- "Bölümleri ayrı dosyalara yazma: Kodda bulunamadı" → var (`App/EditorView.Teslim.cs:69`).
- "Sahne değişimiyle bölme: düzenleyiciye bağlı değil" → bağlı (`App/EditorView.Sahne.cs:22`).
- "Küçük resim şeridi, anahtar kare işaretleri: Kodda bulunamadı" → var (`App/EditorTimeline.Serit.cs:28`).
- "Proje kaydetme: Kodda bulunamadı" → var (`Core/EditProject.cs:39`).

Hâlâ geçerli olanlar: geçiş, müzik ekleme ve görsel katman yok; hedef boyutla dışa aktarım yok;
döküm üzerinden düzenleme yok.

### Karar Gerektirmeyenler Listesinden Alana Düşenler

| Kalem | Bugünkü Durum | Kanıt |
|---|---|---|
| #3 Proje kaydı ve otomatik kayıt | Yapıldı | `Core/EditProject.cs:39`, `:377` |
| #12 Metadata silme | Düzenleyicide var | `App/EditorView.axaml:84` |
| #13 Bölümleri ayrı dosyalara yazma | Yapıldı | `App/EditorView.Teslim.cs:69` |
| #14 Küçük resim şeridi ve anahtar kare işaretleri | Yapıldı | `App/EditorTimeline.Serit.cs:28`, `:114` |
| #15 Sahne değişimiyle bölme | Yapıldı | `Core/SceneSplit.cs:9` |
| #16 Akıllı kesmeyi diğer kodeklere açma | Komşu kolun alanı | `Core/SmartCutCodec.cs` |

### "Bizde Yok" Aramaları

Her satırda ad, düzenleyici klasörlerinde ve bütün `src` altında arandı. Sayı eşleşen dosya
sayısıdır; sıfır olmayanlarda en yakın kod yazıldı.

| Aranan Ad | Düzenleyicide | Bütün `src` | En Yakın Kod |
|---|---|---|---|
| `xfade`, `acrossfade` | 0 | 0 | Yok |
| `Gecis` | 0 | 2 | Düzenleyici dışı |
| `overlay=` | 0 | 2 | Kaydedici: `RecorderArguments.cs:944`, `VideoFilterChain.cs:298` |
| `amix` | 0 | 4 | Kaydedici: `AudioCaptureArguments.cs:123` |
| `sidechaincompress`, `Ducking` | 0 | 0 | Yok |
| `colorbalance`, `curves`, `colortemperature`, `Parlaklik` | 0 | 0 | Yok |
| `eq=` | 0 | 1 | Oynatıcı: `MpvEngine.Advanced.cs:81` |
| `chromakey` | 0 | 1 | Kaydedici web kamerası: `RecorderArguments.cs:928` |
| `zoompan`, `KenBurns` | 0 | 0 | Yok |
| `Proxy`, `Vekil` | 0 | 2 | Yalnız ProRes 422 Proxy kodek adı: `IntermediateCodecs.cs:33` |
| `Multicam`, `CokluKamera` | 0 | 0 | Yok |
| `afftdn` | 0 | 3 | Kaydedici: `AudioCaptureArguments.cs:135` |
| `arnndn`, `anlmdn`, `acompressor`, `highpass` | 0 | 0 | Yok |
| `equalizer` | 0 | 175 | Oynatıcı: `PlayerView.Advanced.cs`, `MpvEngine.Advanced.cs` |
| `loudnorm` | 0 | 52 | Yalnız dil dosyaları (Küçült tarafı) |
| `Whisper`, `Transcript`, `Reframe`, `rembg`, `Segmentation` | 0 | 0 | Yok |
| `Sablon`, `Template` | 0 | 3 | Yalnız arayüz kalıbı |
| `ExportPreset` | 0 | 2 | Küçült ön ayarları: `MainWindow.OnAyar.cs:401` |
| `MediaBin`, `MediaPool` | 0 | 0 | Yok |
| `SubtitleTrack` | 0 | 30 | Oynatıcı |
| `FreezeFrame`, `PiP`, `Voiceover`, `Seslendirme` | 0 | 0 | Yok |
| `TimeRemap`, `SpeedRamp`, `HizRampa` | 0 | 0 | Yok |
| `Nested`, `Unlink`, `AddTrack`, `IzEkle` | 0 | 0 | Yok |
| `gblur`, `boxblur`, `minterpolate` | 0 | 0 | Yok |
| `Roll`, `Slip` | 0 | Yalnız dil sözcükleri | Araç listesi `App/EditorTool.cs:3-8` |

`docs/olcumler/` altında kurgu eksiklerine (geçiş, çok iz, renk) dair ölçüm yok. Düzenleyiciyle
ilgili ölçümler başka konularda: `d3-cizelge-olcumleri.md`, `duzenleyici-klip-ozellik.md`,
`ters-klip-bellek-siniri.md`.

`.claude/acik.md` defterinde bu belgedeki kurgu eksiklerinden hiçbiri yazılı değil.

### Envantere Ekler

Tablo yazılırken koddan çıkan, yukarıdaki listede olmayan satırlar:

- Görüntüsü olmayan ya da süresi okunamayan dosya kaynak olarak alınmıyor:
  `App/EditorView.Kaynak.cs:89-93`. Yani bugün yalnız ses dosyası (müzik) eklenemez.
- Yeni kaynak hep çizelgenin sonuna eklenir: `App/EditorView.Kaynak.cs:96-97`.
- Çıktı kabı yalnız MP4 ve MKV: `App/EditorView.Teslim.cs:392`.
- Tam kip her şeyi 8 bit `yuv420p` yapıyor: `Core/EditExport.cs:334`, `:366`, `:496`. HDR (geniş
  parlaklık aralıklı) kaynak bu yoldan HDR olarak çıkmaz.
- Hız değişince ses `atempo` süzgeciyle uyduruluyor, yani ses perdesi korunuyor:
  `Core/EditExport.cs:139`.
- Metin kenarlığı boyuta göre kendiliğinden hesaplanıyor, kullanıcı ayarı yok:
  `Core/AssWriter.cs:108-128`.
- Klip kopyala, kes, yapıştır yok. `Copy`, `Paste`, `Kopyala`, `Cogalt` aramaları yalnız paylaşım
  bağlantısını kopyalayan kodu buldu: `App/EditorView.Teslim.cs:314-319`.
- Düzenleyici klasörlerinde şu aramalar boş döndü: ses düzeyi göstergesi (`Meter`, `Gosterge`),
  düz renk klibi (`ColorClip`, `SolidColor`, `color=c=`), klip gruplama (`Group`, `Grup`), kare
  dışa aktarma (`Snapshot`, `ExportFrame`, `KareKaydet`).

## Rakipler

19 program tarandı. Altı ücretsiz programın özellik sayfaları ve sürüm notları tek tek okundu;
on üç ücretli programda sürüm, tarih ve son iki yılın sürüm notlarına bakıldı.

Tarih sütununda **(Ö)**, sayfanın kendisi açılamadığı için bilginin arama özetinden geldiğini
gösterir.

| Ad | Sürüm | Tarih | Tür | Kaynak |
|---|---|---|---|---|
| DaVinci Resolve | 21.1 | 8 Eylül 2026 (Ö) | Ücretsiz sürüm ve ücretli Studio | [26][27][62] |
| Shotcut | 26.9.27 | 28 Eylül 2026 | Açık kaynak, MLT tabanlı | [6][7] |
| Kdenlive | 26.08.2 (depo etiketi); duyurulan son sürüm 26.08.1 | Etiket 6 Ekim 2026 (depodaki sürüm kaydında 8 Ekim, alt ajan raporu); 26.08.1 duyurusu 11 Eylül 2026 | Açık kaynak, MLT tabanlı | [14][17][69] |
| OpenShot | 4.0.1 | 27 Eylül 2026 | Açık kaynak | [9][11] |
| Olive | Kararlı sürüm yok; son yapı 0.2.0-nightly | 5 Aralık 2024 | Açık kaynak, durgun | [25] |
| Lightworks | 2025.2 (yapı 56356); 2026.1 açık beta | Ocak 2026 | Ücretsiz katman ve ücretli | [32] |
| CapCut masaüstü | Sürüm numarası resmi sayfada görülmedi | Tanıtım yazıları 20 Ağustos ve 10 Eylül 2026 | Ücretsiz ve Pro abonelik | [22][23][24] |
| Clipchamp | Sürüm numarası yok (web ve Windows uygulaması) | Tarihli son resmi özet 16 Ocak 2026 | Ücretsiz ve premium; Windows ile gelen araç | [19][20][21][67] |
| Premiere Pro | 26.5 | Eylül 2026 (Ö) | Abonelik | [29][30][63] |
| Premiere Elements | 2027 | 17 Eylül 2026 | Ücretli | [31][64] |
| Final Cut Pro | 12.4 | 29 Eylül 2026 | Ücretli, yalnız macOS | [28] |
| iMovie | 10.4.5 | 29 Eylül; yıl sayfada yok (Ö) | Ücretsiz, macOS ile gelen araç | [40] |
| Filmora | 16.0 | Eylül 2026 | Ücretli | [33] |
| VSDC | 11.3 | Tarih yok; 11.2 sürümü 30 Haziran 2026 | Ücretsiz ve Pro | [34] |
| Movavi Video Editor | 27.1.0 | 12 Ekim 2026 (Ö) | Ücretli | [35] |
| PowerDirector | 365 ve 2027 | Son güncelleme Eylül 2026 (Ö) | Ücretli | [36] |
| VEGAS Pro | 2026 | Tarih yok | Ücretli; Boris FX hesabıyla giriş | [37] |
| Descript | Sürüm numarası yok | Son sürüm notu 12 Ekim 2026 | Ücretsiz katman ve abonelik; bulut tabanlı | [38] |
| Camtasia | Windows 2026.2.2; Mac 2026.2.4 | 22 Eylül 2026; 6 Ekim 2026 | Ücretli | [39] |

Listede olmayan iki açık kaynak programın yalnız sürümüne bakıldı, özellikleri taranmadı:
Flowblade 2.24.2 (29 Mayıs 2026) [71] ve Pitivi (depoda görülen etiket `RELEASE-0_95_0`; tarihi
ve daha yeni sürüm olup olmadığı doğrulanmadı).

**MLT** rakip değil, motor: Shotcut ve Kdenlive'ın altındaki çok izli bileşim kitaplığı. Son
sürümü 7.42.0, 3 Ekim 2026 [5]. Ayrıntısı Motor Ayrımı başlığında.

## Özellik Matrisi

`var` resmi sayfada benim gördüğüm; `var*` alt ajan raporundan ya da arama özetinden gelen
(iddia, yerinde okunmadı); `—` doğrulanmadı demektir. Hiçbir hücre "rakipte yok" anlamına gelmez.

| Özellik | Shotcut | Kdenlive | OpenShot | Clipchamp | CapCut | Bizde |
|---|---|---|---|---|---|---|
| Çok izli çizelge | var [6] | var* [14] | var [9] | var* [20] | var* [24] | yok; üç sabit iz |
| Ripple kırpma | var [6] | var [12] | var [11] | — | — | var |
| Roll kırpma | var [6][7] | — | — | — | — | yok |
| Slip kırpma | — (yol haritasında [8]) | var [12] | — | — | — | yok |
| Slide kırpma | — (yol haritasında [8]) | — | — | — | — | yok |
| Klipler arası geçiş | var [6] | var [13] | var [9] | var [19] | var [23] | yarım; yalnız solma |
| Metin ve başlık | var [6] | var* [14] | var [9] | var [19] | var [23] | yarım; kısıtlı panel |
| Hazır başlık şablonu | var* [7] | var* [16] | var [9] | var* [20] | var [23] | yok |
| Anahtar kare | var [6] | var* [16] | var [9] | — | var [22] | yarım; yalnız metin konumu, modelde |
| Bulanıklık | var [6] | var* [17] | var* [61] | — | — | yok |
| Yeşil perde | var [6] | var* [17] | var [9] | var [19] | var [23] | yok |
| Temel renk ayarı | var [6] | var [13] | var [9] | var [19] | var [23] | yok |
| Eğri ya da renk tekeri | var [6] | var* [14] | var [11] | — | var [22] | yok |
| Renk skopları | var [6] | var* [17] | var [11] | — | — | yok |
| Müzik, ek ses izi | var [6] | var* [14] | var [9] | var [19] | var [23] | yok |
| Ses karıştırıcı | var [6] | var* [16] | — | — | — | yok |
| Ekolayzır, kompresör | var [6] | var* [17] | var* [61] | — | — | yok |
| Gürültü azaltma | var [6] | — | — | var [19] | var* [24] | yok |
| Sabit hız | var [6] | var* [14] | var [9] | var* [20] | var [23] | var |
| Hız rampası | var [6] | var* [17] | var [9] | — | var [23] | yok |
| Vekil (proxy) | var [6] | var* [18] | var [11] | — | var* [24] | yok |
| Proje şablonu | — | — (yol haritasında [15]) | — | var [19] | var [22] | yok |
| Dışa aktarımda çözünürlük seçimi | var [6] | var [13] | var [10] | var [19] | var [23] | yok; sabit |
| Dışa aktarım ön ayarı | var [6] | var* [17] | var* [61] | — | — | yok; yalnız Küçült'te |
| Çoklu kamera | — (yol haritasında [8]) | var [12] | — | — | var* [58] | yok |
| Altyazı içe ve dışa aktarma | var [6] | var* [17] | var* [61] | var* [60] | — | yarım |
| Otomatik altyazı | var [6][7] | var* [57] | — | var [19] | var [22] | yok; K5 kapalı |
| Medya havuzu | var [6] | var [13] | var* [61] | var* [67] | var* [22] | yok |
| Sessizlik silme | — (yol haritasında [8]) | var* [17] | — | var [19] | — | var; komşu kol |
| Metin üzerinden kurgu | — | var* [17] | — | var* [21] | var* [59] | yok; K5'e bağlı |
| Otomatik yeniden kadraj | — | — | — | — | var [22] | yok |
| Arka plan silme | — (yol haritasında [8]) | var* [16] | var [11] | — | var [23] | yok |

Ücretli programlarda resmi sayfadan görülenler:

- **DaVinci Resolve** Edit sayfası [26]: ripple, roll, slip ve slide arasında kendiliğinden geçen
  kırpma aracı, yüzden fazla geçiş, metin üreteci ve başlık şablonları, anahtar kare, hız denetimi,
  çoklu kamera, altyazı araçları, medya havuzu, üç nokta kurgu, karıştırıcı, ayar klibi, dökümden
  kurgu.
- **Çoklu kamera:** Resolve, Final Cut Pro, Filmora, VEGAS, Descript (alt ajan raporu).
- **Anahtar kare editörü:** Resolve, VSDC, VEGAS, Filmora, Movavi (alt ajan raporu).
- **Vekil dosya:** ücretliler arasında yalnız Final Cut Pro'da doğrulandı [28].
- Premiere Pro, Lightworks ve iMovie'nin temel kurgu özellikleri resmi sayfadan okunamadı
  (Doğrulanamayanlar'a bakın).

### Ortak Asgari Küme

Soru: ücretsiz beş rakibin (Shotcut, Kdenlive, OpenShot, Clipchamp, CapCut) **hepsinde** olan ne,
bizde hangisi var?

| Kalem | Beşlide Kanıt | Bizde | Eksik No |
|---|---|---|---|
| Kes, böl, kırp | 5/5 | Var | — |
| Hız değiştirme | 5/5 | Var | — |
| Klipler arası geçiş | 5/5 | Yarım: yalnız siyaha solma | DZB-16, DZB-17 |
| Metin ve başlık | 5/5 | Yarım: tek katman, panel kısıtlı | DZB-19, DZB-20 |
| Bir tür altyazı | 5/5 (OpenShot'ta efekt olarak) | Yarım: metin izi var, dosya alınmıyor | DZB-62 |
| Müzik ya da ek ses | 5/5 | Yok | DZB-01 |
| Üst üste katman, bindirme | 5/5 (Clipchamp'te dolaylı) | Yok | DZB-02 |
| Yeşil perde | 5/5 (Kdenlive'da dosya adı düzeyinde) | Yok | DZB-29 |
| Bir tür renk ayarı | 5/5 | Yok | DZB-35 |
| Çözünürlük seçerek dışa aktarım | 5/5 | Yok: ayar sabit | DZB-54 |
| Medya paneli | 5/5 | Yok | DZB-64 |

On bir kalemin ikisi bizde tam, üçü yarım, altısı yok.

Beşte dört kalanlar: anahtar kare ve vekil dosya. İkisinde de eksik olan Clipchamp
(doğrulanmadı). Hız rampası, renk tekerleri, skoplar, çoklu kamera ve adlı kırpma araçları ortak
kümede değil; bunlar "basit kurgu"nun değil, ileri kurgunun parçası.

### Motor Ayrımı

Soru: hangi eksik bugünkü düzenle (ffmpeg süzgeçleri ve libmpv önizlemesi) yapılır, hangisi ayrı
bir çizim ve bileşim motoru ister?

Kısa cevap: **teslimde ffmpeg hemen her şeye yetiyor; zor olan canlı önizleme.** Önizleme bugün
mpv'nin `edl://` kesim listesiyle çalışıyor ve bu biçimde parça başına hız, geçiş ya da süzgeç
alanı yok [2]. Klip ayarları önizlemeye zaman pencereli süzgeçle veriliyor
(`Core/ClipFilters.cs:111-113`, `:132`); tek şeritte kalan her şey bu yolu kullanabilir.

**1. Bugünkü düzen yeter (tek şerit içinde kalır):**

- Renk: `eq`, `curves`, `colorbalance`, `colortemperature` [1] → DZB-35, DZB-36, DZB-30.
- Bulanıklık: `gblur`, `boxblur` [1] → DZB-28.
- Ses: `afftdn`, `loudnorm`, `equalizer`, `acompressor` [1] → DZB-42, DZB-43, DZB-45.
- Metin: bütün biçim işleri `.ass` dosyasında → DZB-19 – DZB-24.
- Kırpma araçları yalnız çizelge modelinde hesap → DZB-13, DZB-14, DZB-15.
- Dışa aktarım ayarları yalnız argüman üretimi → DZB-54 – DZB-58.
- Dönüşüm: `scale`, `pad`, `rotate` [1] → DZB-03.
- Hız rampası, kare dondurma ve anahtar kareli klip ayarı da tek şeritte kalır: teslimde `setpts`
  ifadesi, `tpad` ve süzgeç ifadeleri [1] → DZB-48, DZB-49, DZB-25. Ayrı motor ya da karar
  istemezler; emek isteyen kısım önizlemede hızı ve süzgeç değerini oynarken adım adım değiştirmek.
  Tabloda işleri bu yüzden `büyük` ve `orta`.

**2. Teslim ffmpeg ile kolay, önizleme yeni bir yol ister:**

- Geçiş: teslimde `xfade` ve `acrossfade` [1]; mpv kesim listesinde karşılığı yok → DZB-16 – DZB-18.
- Müzik izi: teslimde `amix` [1]; önizlemede mpv'ye ek ses dosyası verip karıştırmak gerekir → DZB-01.
- Bindirme, yeşil perde, logo: teslimde `overlay` ve `chromakey` [1] → DZB-02, DZB-29, DZB-33.

Önizleme için üç yol var; hangisi seçileceği sahibin kararı:

- **mpv `lavfi-complex`**: mpv'ye birden çok kaynağı tek süzgeç grafiğinde birleştirten seçenek.
  Yeni bağımlılık istemez; tek görüntü ve tek ses çıkışı verir. `edl://` ile birlikte çalışıp
  çalışmadığı belgede yazmıyor (doğrulanmadı).
- **Kısa ön-işleme**: yalnız geçiş ya da bindirme olan birkaç saniyeyi arka planda ffmpeg ile
  küçük bir dosyaya çevirip kesim listesine parça olarak koymak. Yeni bağımlılık istemez; kullanıcı
  o bölge hazırlanana dek bekler.
- **Ayrı bileşim motoru**: MLT gibi bir kitaplık. Çok iz, geçiş, bileşim, anahtar kare ve ses
  karışımını canlı yapar [3].

**3. Ayrı motor ancak şunlar birlikte istenirse anlamlı:** serbest sayıda iz, iz başına efekt,
her değere anahtar kare, çoklu kamera (DZB-60), ayar klibi (DZB-67), iç içe dizi (DZB-66).

MLT notu: çekirdeği LGPL 2.1, bazı modülleri GPL lisanslı [3]. Kaynak ağacında C# bağlaması var
(`src/swig/csharp`) [4] ama hazır NuGet paketi yok. Önceki iç rapordaki ".NET bağlaması yok"
cümlesi böyle düzeltilmeli. MLT'yi almak yeni ikili dosya dağıtımı demek; karar sahibin.

**4. Model ya da bulut ister:** DZB-44, DZB-63, DZB-68 – DZB-75, DZB-77.

## Kullanıcı Talepleri

Oy birimleri aynı değil, yalnız sıra için okunmalı:

- **GitHub** ve **invent.kde.org**: kişi başı bir oy.
- **Shotcut forumu**: oy sayacı yok. "79/10" konunun toplam beğenisi ve ilk iletinin beğenisi.
- **bugs.kde.org** (KDE'nin hata takip sitesi): ağırlıklı oy; bir kişi birden çok oy verebiliyor.

| İstek | Yer | Oy | Kaynak | Eksik No |
|---|---|---|---|---|
| Anahtar kareleri klip içinde ileri geri taşıma | OpenShot | 25 (kapalı) | [41] | DZB-25 |
| Komut satırından dışa aktarım | OpenShot | 17 | [42] | Alan dışı |
| GIF olarak dışa aktarım | OpenShot | 13 | [42] | Başka kol |
| İzin tamamını susturma | OpenShot | 7 | [42] | DZB-06 |
| Altyazı | OpenShot | 3 (açık) | [42] | DZB-62 |
| Sessizliği kendiliğinden kesme | Kdenlive | 11 (açık) | [43] | Komşu kol |
| Gelişmiş kırpma araçları | Kdenlive | 6 (açık; oy sayısı alt ajan raporundan) | [44] | DZB-13 – DZB-15 |
| Anlatım altında müziği kendiliğinden kısma | Kdenlive (bugs.kde.org) | 30 ağırlıklı | [45] | DZB-46 |
| Hız rampası | Kdenlive (bugs.kde.org) | 22 ağırlıklı | [46] | DZB-48 |
| Hazır alt bant kitaplığı | Shotcut forumu | 79/10 | [47] | DZB-22 |
| Gelişmiş ekolayzır | Shotcut forumu | 45/10 | [50] | DZB-42 |
| Kare dondurma kolaylaşsın | Shotcut forumu | 33/2 | [48] | DZB-49 |
| İzleri yukarı aşağı taşıma | Shotcut forumu | 23/5 (kapalı) | [54] | DZB-06 |
| Sessizliği kendiliğinden silme | Shotcut forumu | 23/5 | [53] | Komşu kol |
| Hareketli yakınlaştırma ve konum | Shotcut forumu | 16/7 | [51] | DZB-25, DZB-27 |
| Yerleşik vekil dosya üretimi | Shotcut forumu | 11/3 | [49] | DZB-51 |
| Bütün kliplere standart çapraz geçiş | Shotcut forumu | 5/4 | [52] | DZB-16 |
| Stabilizasyon | Olive | 9 | [55] | DZB-31 |

Benim yerinde doğruladıklarım: OpenShot ve Olive sayıları (`gh api`), Kdenlive 11 oyu ve iki
kaydın açık olduğu. Forum beğenileri ve bugs.kde.org oyları alt ajan raporundan.

**Yol haritaları (oy yok, geliştiricinin kendi listesi):**

- **Shotcut** [8]: müzik kısma, sessizlik algılama ve silme, yarım ya da bir saniyelik geçişi
  kolay ekleme, çok izli geçiş, slip ve slide, çoklu kamera, proje medya yönetimi, renk için eğri
  arayüzü, arka plan silme, EDL ve OpenTimelineIO alışverişi.
- **Kdenlive** [15]: kısa vadede 10 ve 12 bit renk, altyazı yenilemesi, gelişmiş kırpma araçları,
  GPU ile efekt, OpenFX. Orta vadede yapay zekâ efektleri, LV2 ve VST, proje şablonları. Uzun
  vadede hız eşleme iyileştirmesi, müzik kısma.

**Okuma:** en çok oy anahtar kare kullanımına, sese (müzik kısma, ekolayzır), kırpma araçlarına ve
hazır başlıklara gidiyor. Vekil dosya isteği yalnız Shotcut forumunda, çoklu kamera yalnız Shotcut'ın yol haritasında görüldü.
Sessizlik silme iki programda da üst sıralarda ve bizde zaten var; bu bir üstünlük (komşu kol).

Alt ajanın konu başına toplamı (oy birimleri karışık, yalnız sıra için): başarım ve kararlılık 82,
arayüz 79, efekt 77, anahtar kare 71, ses 52, dışa aktarım 47, başlık ve şablon 33, hız 17, çok iz
17, altyazı 14, medya havuzu 11. İlk iki konu bu kolun değil, uygulamanın genel işi.

## Son İki Yılın Yenilikleri

**Açık kaynak programlar yerel yapay zekâyı ve ileri araçları ekliyor.**

- **Shotcut** [7]: konuşmadan yazıya (Ekim 2024), kutular ve arama (Ocak 2025), kare dondurma
  (Mayıs 2025), çizelgede roll kırpma ve Whisper model indirici (Temmuz 2025), metinden sese ve
  ekran kaydı (Ekim 2025), 10 bit işleme (Aralık 2025), konuşma tanımada GPU ve dışa aktarımda
  kapak resmi (Nisan 2026), OpenFX, VST2 ve LV2 eklentileri ile HDR (Haziran 2026), yaklaşık 22 MB'lık
  hazır varlık paneli "Elements" (Ağustos 2026). Whisper, OpenAI'nin açık konuşma tanıma modeli.
- **OpenShot** [11]: ripple kurgusu (Aralık 2024), tutamaçla görüntü kırpma ve sürüklenebilir anahtar
  kare (Aralık 2025), yeni çizelge ve anahtar kare paneli (Mart 2026), vekil dosya (Nisan 2026),
  renk görünümü, kayıt görünümü ve yerele inen modelle nesne maskesi (4.0, Ağustos 2026).
- **Kdenlive** [14][15][16]: çoklu altyazı izi (24.12), yerel modelle nesne ayırma ve
  OpenTimelineIO alışverişi (25.04), yeni karıştırıcı ve başlık şablonları (25.08), süreli işaretçi
  (25.12), geçişlerde önizleme küçük resmi (26.04), iz sıralama, çoklu klip hız değişimi, eğrilerde
  kanal sekmeleri ve çoklu kamerada zaman koduyla ekleme (26.08). İki yılın hemen öncesinde:
  yumuşatma kipleri (24.02) ve eğri editörü (24.08).

**Ücretsiz kapalı programlar:**

- **Clipchamp** [21][67]: dökümden kırpma, gruplama, hız düzenleme, şablon paylaşma, yalnız ses
  dışa aktarım, gürültü bastırma. Girdiler tarihsiz.
- **CapCut** [22][23]: tarihli masaüstü sürüm notu bulunamadı. Bugünkü sayfalarda anahtar kare
  eğrileri, hız eğrisi, otomatik altyazı, özneyi kadrajda tutma, arka plan silme ve video üretimi var.

**Ücretli programlar üretken ve sohbetli yapay zekâya gidiyor** (alt ajan raporu):

- **DaVinci Resolve** 20 ve 21 [27][62]: yapay zekâ ile senaryodan kurgu, çoklu kamerada açı
  seçimi, ses yardımcısı, akıllı arama, konuşma üretici, dikey ve kare proje, harici yapay zekâ
  yardımcısı bağlantısı.
- **Premiere Pro** [29][30][63]: Generative Extend (klibi üreterek uzatma), Media Intelligence
  (içeriğe göre arama), Object Mask (nesne maskesi).
- **Final Cut Pro** [28]: Magnetic Mask, dökümde arama, görsel arama, vuruş algılama, altyazı
  üretme, renk eşleme.
- **Filmora** [33]: çoklu kamera, müzik kısma, hareket izleme, altyazı izi, XML alışverişi, video
  üretimi, sohbet yardımcısı.
- **VEGAS Pro 2026** [37]: çevrimdışı konuşmadan yazıya, metin üzerinden kurgu, çevrimdışı
  metinden sese.
- **Descript** [38]: sohbetle kurgu, 16 geçiş, etkin konuşmacıya göre açı seçimi.
- **PowerDirector** [36], **VSDC** [34], **Movavi** [35], **Camtasia** [39]: sohbetle kurgu ve
  nesne silme; dış hizmetle video üretimi; izlemeli kırpma ve gürültü giderme; VTT altyazı ve sese
  göre eşleme.
- **iMovie** [40]: iki yılda yeni kurgu özelliği görülmedi. **Lightworks** [32]: yeni çalışma
  düzenleri ve GPU motoru. **Olive** [25]: yeni özellik yok.

### Yapay Zekâ: Yerelde mi, Bulutta mı

| Yardımcı | Yerelde Çalışan | Bulut, Hesap ya da Kredi İsteyen |
|---|---|---|
| Otomatik altyazı | Shotcut [7], Kdenlive [57], Final Cut Pro 12.3 [28], VEGAS 2026 (abonelik şartıyla) [37] | Clipchamp (ses Azure'da işleniyor) [60], Descript [38] |
| Arka plan ve nesne maskesi | OpenShot 4.0 [11], Kdenlive [16], Premiere Pro 26.0 [30] | CapCut (nerede çalıştığı doğrulanmadı) |
| Gürültü temizleme | Shotcut (RNNoise) [6] | Clipchamp, CapCut (doğrulanmadı) |
| Metinden sese | VEGAS 2026 [37]; Shotcut [7] (yerelde çalıştığı sürüm notundan okunmadı) | Clipchamp (doğrulanmadı) |
| İçeriğe göre arama | Premiere Pro [29]; Final Cut Pro [28] ve DaVinci Resolve [27] (nerede çalıştığı doğrulanmadı) | — |
| Üretken uzatma, görüntü, video | — | Premiere Pro (kredili) [29], Premiere Elements [64], Filmora [33], CapCut [22], VSDC (kullanıcının API anahtarı) [34] |
| Sohbetle kurgu | — | Descript [38], PowerDirector [36], Filmora [33], DaVinci Resolve 21.1 [62] |

Özet: algılama ve ayıklama işleri (altyazı, maske, gürültü, arama) yerele iniyor; üretme ve
sohbet işleri bulutta ve çoğu krediyle çalışıyor.

### Kapalı Kararlara Denk Gelenler

- **K5 otomatik altyazı** (DZB-63, DZB-68): iki yılda Shotcut konuşma tanımaya GPU ekledi, Final
  Cut Pro ve VEGAS aynı işi çevrimdışı yapar oldu; metin üzerinden kurgu Clipchamp, VEGAS ve
  Descript'te bunun üstüne kuruldu.
- **K8 stabilizasyon, LUT, filigran** (DZB-31, DZB-32, DZB-33): OpenShot 3.4 LUT efekti ve 4.0
  renk görünümünde LUT ekledi, Shotcut 26.4 klip özelliklerine LUT dosyası koydu. Stabilizasyon ve
  filigranda iki yılda yeni bir şey görülmedi.

Kararı yeniden açmak sahibin işi; burada yalnız not edildi.

## Sıra Önerisi

**Öncelik ölçütüm.** Ortak kuraldan bilerek daha sıkı tuttum; tablo okunurken bilinmeli:

- **1:** yalnız ortak asgari kümede olan, ana işe dokunan ve rakip iddiası iki ayrı resmi
  kaynakta görülen kalemler. Yedi kalem: DZB-01, 02, 16, 17, 19, 35, 54.
- **2:** en az iki rakipte olup ana işin kenarında kalanlar.
- **3:** tek rakipte doğrulananlar ve uzman işi sayılanlar (skop, çoklu kamera, iç içe dizi, proje
  alışverişi, eklenti, HDR, slide, yumuşatma eğrisi, ayar klibi). Bir kısmı üç ve daha çok rakipte
  var; basit kurgunun parçası olmadıkları için yine de 3 verdim.
- **Yapay zekâ:** yerelde çalışabilenler 2 (DZB-69 – DZB-72). Bulut, hesap ve kredi isteyenler 3
  (DZB-73 altı, DZB-74 dört rakipte olmasına karşın): çevrimdışı çalışan bir araçta ana işin
  dışındalar. Bu, kuraldan sapmadır; sahip başka düşünürse ikisi 2 olur.
- Kapalı kararlara denk gelen beş kalem kural gereği 3.

**Birinci dalga — karar gerektirmez, tek şeridin içinde kalır:**

1. **DZB-54 dışa aktarım ayarları** (orta). Bugün her Tam teslim sabit ayarla çıkıyor; kodek ve
   kalite seçicileri Küçült ve Dönüştür'de zaten var.
2. **DZB-19 metin biçimi**, ardından **DZB-20 konum** ve **DZB-21 kenarlık** (küçük, orta, küçük).
   Model hazır, eksik olan yalnız panel.
3. **DZB-35 temel renk ayarı** (orta). `eq` süzgeci; önizleme yolu hazır.
4. **DZB-43 gürültü azaltma** ve **DZB-45 yükseklik eşitleme** (ikisi küçük). `afftdn`
   kaydedicide zaten kullanılıyor.
5. **DZB-13 roll** ve **DZB-14 slip** (ikisi küçük). Yalnız çizelge modelinde hesap.
6. Küçük tamamlayıcılar: **DZB-11** kopyala-yapıştır, **DZB-05** düz renk klibi, **DZB-57** kare
   dışa aktarma.

Bu dalga bitince ortak asgari kümenin on bir kaleminden beşi tam olur (bugün iki).

**İkinci dalga — sahip kararı ister, kapsam tek şeritten çıkar:**

7. **DZB-01 müzik izi** (orta). Ortak kümenin en görünür eksiği; önizlemesi en kolay olan çok iz
   adımı.
8. **DZB-16 çapraz geçiş teslimi** (orta), sonra **DZB-17 önizlemesi** (büyük). Önizleme yolu
   burada seçilir; seçim sonraki bütün çok iz işlerini belirler.
9. **DZB-02 ikinci video izi** (büyük), üstüne **DZB-29 yeşil perde**. Öncesinde **DZB-04 resim
   klibi** yapılırsa bindirilecek logo ve fotoğraf da hazır olur (DZB-04 karar gerektirmez).

**Üçüncü dalga — ileri kurgu:** DZB-25 anahtar kare, DZB-48 hız rampası, DZB-51 vekil dosya,
DZB-62 altyazı dosyası alma, DZB-64 medya havuzu.

**Yapay zekâ:** en ucuz adım DZB-44 (RNNoise küçük bir model dosyasıdır). Gerisi yüzlerce MB model
ya da bulut ister.

## Doğrulanamayanlar

**Rakipler**

- **Premiere Pro:** Adobe'nin sürüm notu ve kılavuz sayfaları 403 döndü. Temel kurgu özellikleri
  ve 25.0–25.6 ara sürümleri resmi sayfadan okunamadı.
- **Lightworks:** özellik sayfası 404; temel özellikler ve 2026.1'in çıkış tarihi doğrulanmadı.
- **iMovie:** sürüm yılı ve temel özellikler doğrulanmadı.
- **CapCut:** sürüm numarası, hangi özelliğin Pro olduğu, giriş şartı, yapay zekânın nerede çalıştığı.
- **Clipchamp:** giriş şartı (sayfada giriş düğmesi var, şart cümlesi yok); anahtar kare, vekil
  dosya, hız rampası ve kırpma araçları; yenilik girdilerinin tarihleri.
- **Kdenlive:** özellik sayfası üç adreste 404. Roll ve slide kılavuzda görülmedi. `var*` yazan
  hücrelerin çoğu yalnız depodaki dosya adına dayanıyor; dosyanın varlığı özelliğin çalıştığını
  göstermez.
- **Shotcut 26.9.27:** ben bu sürümün notunda güvenlik düzeltmelerini okudum; alt ajan aynı
  sürümde müzik kısma ve ayar klibi okudu. Müzik kısma özellik sayfasında var [6]; hangi sürümde
  geldiği doğrulanmadı.
- **Ücretli programlarda** özellik başına yerel ya da bulut ayrımı çoğunda doğrulanmadı (Filmora,
  Movavi, PowerDirector, Camtasia, Resolve). Fiyatlar doğrulanmadı.

**Talepler**

- bugs.kde.org oyları (30 ve 22): arayüz oy alanını döndürmedi; sayılar alt ajan raporundan.
- Kdenlive "gelişmiş kırpma araçları" kaydının 6 oyu [44]: kaydın açık olduğu görüldü, oy sayısı
  alt ajan raporundan.
- Shotcut forum beğenileri alt ajan raporundan; tek tek sayılmadı. Konuların çoğu 2016–2022 arası.
- Altı forum talebinin adresi bu belgeye taşınamadı: tek süzgeci kopyalama (23/5), klip gruplama
  (19/4), gölge efekti (20/3), VST (10/7), sesi videodan ayırma (9/3), dışa aktarım bitince
  bildirim (13/5).

**Teknik**

- `xfade` geçiş adlarının tam listesi: ffmpeg wiki sayfası erişimi engelledi. `xfade`'in varlığı [1]'de.
- mpv'de `lavfi-complex` ile `edl://` birlikte çalışır mı, kesim listesine resim parçası girer mi:
  belgede açık cümle yok, denenmedi (program çalıştırmak yasaktı).

**Bizim Taraf**

- Resim dosyası kaynak olarak alınıyor mu: kod süreye bakıyor, denenmedi.
- Dışa aktarım bitince bildirim: düzenleyici klasörlerinde `Notify`, `Bildirim`, `Toast` araması
  boş; uygulamanın ortak bir bildirimi var mı bakılmadı. Tabloya alınmadı.

**Kararsız Kalınan Yerler**

- **Metin konumu (DZB-20)** ve **kopyala-yapıştır (DZB-11)** rakiplerde açık cümleyle az kaynakta
  görüldü; ikisine de 2 verdim. İkinci bir resmi kaynak bulunursa 1 olmaları gerekir.
- **Resim klibi (DZB-04)** üç rakipte belgeli, **medya havuzu (DZB-64)** ortak kümede; ikisine de
  2 verdim, çünkü asıl ihtiyaç (birden çok kaynak) bugün karşılanıyor.
- **Yeşil perde (DZB-29)** ve **altyazı dosyası alma (DZB-62)** de ortak kümede ama 2: yeşil perde
  ikinci video izi olmadan işe yaramaz; altyazı dosyası alma Clipchamp ve CapCut'ta doğrulanmadı.
- Önceki taramanın "hedef boyutla dışa aktarım" satırı hiçbir rakipte doğrulanmadı (OpenShot'ın
  dışa aktarım sayfasında böyle bir seçenek yok [10]); tabloya almadım.

## Kaynaklar

İşaretler: **(B)** sayfayı ben açtım ya da `gh` ile okudum; **(A)** alt ajan açtı, bana rapor
olarak geldi; **(Ö)** yalnız arama özeti.

- [1] https://ffmpeg.org/ffmpeg-filters.html — ffmpeg süzgeç belgesi (B)
- [2] https://raw.githubusercontent.com/mpv-player/mpv/master/DOCS/edl-mpv.rst — mpv kesim listesi biçimi (B)
- [3] https://www.mltframework.org/features/ — MLT özellikleri ve lisansı (B)
- [4] https://github.com/mltframework/mlt/tree/master/src/swig — MLT dil bağlamaları (B)
- [5] https://github.com/mltframework/mlt/releases — MLT sürümü (B)
- [6] https://shotcut.org/features/ — Shotcut özellik listesi (B)
- [7] https://github.com/mltframework/shotcut/releases — Shotcut sürüm notları (B; 2024–2025 notları A)
- [8] https://shotcut.org/roadmap/ — Shotcut yol haritası (A)
- [9] https://www.openshot.org/features/ — OpenShot özellikleri (B)
- [10] https://www.openshot.org/static/files/user-guide/export.html — OpenShot dışa aktarım (B)
- [11] https://github.com/OpenShot/openshot-qt/releases — OpenShot sürüm notları (B)
- [12] https://docs.kdenlive.org/en/user_interface/menu/tool_menu.html — Kdenlive araç menüsü (B)
- [13] https://docs.kdenlive.org/en/ — Kdenlive kılavuz dizini (B)
- [14] https://kdenlive.org/news/releases/26.08.0/ — Kdenlive 26.08.0 (Ö, A)
- [15] https://kdenlive.org/roadmap/ — Kdenlive yol haritası (A)
- [16] https://kdenlive.org/news/2026/state-2026/ — Kdenlive 2026 durum yazısı (A)
- [17] https://github.com/KDE/kdenlive — Kdenlive depo aynası: sürüm etiketi (B), kaynak ağacı (A)
- [18] https://docs.kdenlive.org/en/project_and_asset_management/project_settings/proxy_settings.html — Kdenlive vekil ayarları (A, Ö)
- [19] https://clipchamp.com/en/video-editor/ — Clipchamp özellikleri (B)
- [20] https://clipchamp.com/en/features/ — Clipchamp özellik dizini (A)
- [21] https://techcommunity.microsoft.com/blog/microsoft365insiderblog/microsoft-clipchamp-a-year-in-review-cohesive-ai-powered-video-creation-and-edit/4474506 — Clipchamp 2025 değerlendirmesi (A)
- [22] https://www.capcut.com/tools/desktop-video-editor — CapCut masaüstü (B)
- [23] https://www.capcut.com/resource/how-to-use-capcut — CapCut kullanım yazısı (B)
- [24] https://www.capcut.com/resource/pc-professional-video-editor — CapCut PC yazısı (A)
- [25] https://github.com/olive-editor/olive — Olive deposu ve yayınları (B)
- [26] https://www.blackmagicdesign.com/products/davinciresolve/edit — DaVinci Resolve Edit sayfası (B)
- [27] https://www.blackmagicdesign.com/products/davinciresolve/whatsnew — DaVinci Resolve yenilikleri (A)
- [28] https://support.apple.com/en-us/102825 — Final Cut Pro sürüm notları (A)
- [29] https://blog.adobe.com/en/publish/2025/04/02/introducing-new-ai-powered-features-workflow-enhancements-premiere-pro-after-effects — Premiere Pro 25.2 (A)
- [30] https://blog.adobe.com/en/publish/2026/01/20/new-ai-powered-video-editing-tools-premiere-major-motion-design-upgrades-after-effects — Premiere Pro 26.0 (A)
- [31] https://www.adobe.com/products/premiere-elements/faq.html — Premiere Elements 2027 (A)
- [32] https://lwks.com/releases — Lightworks sürümleri (A)
- [33] https://filmora.wondershare.com/whats-new-in-filmora-video-editor.html — Filmora yenilikleri (A)
- [34] https://www.videosoftdev.com/vsdc-11-3-ai-video-generator — VSDC 11.3 (A)
- [35] https://www.movavi.com/video-editor-plus/whats-new.html — Movavi yenilikleri (A, Ö)
- [36] https://www.cyberlink.com/products/powerdirector-video-editing-software/whats-new_en_US.html — PowerDirector yenilikleri (A, Ö)
- [37] https://www.vegascreativesoftware.com/vegas-pro/whats-new/ — VEGAS Pro yenilikleri (A)
- [38] https://feedback.descript.com/changelog — Descript sürüm notları (A)
- [39] https://support.techsmith.com/hc/en-us/articles/41261973472269-Camtasia-Windows-2026-Version-History — Camtasia sürüm geçmişi (A)
- [40] https://apps.apple.com/us/app/imovie/id408981434?mt=12 — iMovie sürümü (A, Ö)
- [41] https://github.com/OpenShot/openshot-qt/issues/689 — anahtar kare taşıma isteği (B)
- [42] https://github.com/OpenShot/openshot-qt/issues — OpenShot kayıtları 438, 280, 784, 4744, 5435 (B)
- [43] https://invent.kde.org/multimedia/kdenlive/-/work_items/247 — sessizlik kesme isteği (B)
- [44] https://invent.kde.org/multimedia/kdenlive/-/work_items/1069 — gelişmiş kırpma araçları isteği (B)
- [45] https://bugs.kde.org/show_bug.cgi?id=392623 — müzik kısma isteği (durum B, oy A)
- [46] https://bugs.kde.org/show_bug.cgi?id=453881 — hız rampası isteği (durum B, oy A)
- [47] https://forum.shotcut.org/t/should-we-start-a-lower-thirds-library/24941 — alt bant kitaplığı (A)
- [48] https://forum.shotcut.org/t/hold-frame-in-flow-easier/48029 — kare dondurma (A)
- [49] https://forum.shotcut.org/t/built-in-proxy-generation/7610 — vekil dosya (A)
- [50] https://forum.shotcut.org/t/advanced-audio-equaliser/9191 — ekolayzır (A)
- [51] https://forum.shotcut.org/t/dynamic-zoom-position-of-images/60 — hareketli yakınlaştırma (A)
- [52] https://forum.shotcut.org/t/apply-a-standard-transition-cross-fade-with-standard-length-e-g-18-frames-to-all-clips/552 — standart geçiş (A)
- [53] https://forum.shotcut.org/t/automatic-silence-removing/32775 — sessizlik silme (A)
- [54] https://forum.shotcut.org/t/rearrage-tracks-move-tracks-up-down/821 — iz taşıma (A)
- [55] https://github.com/olive-editor/olive/issues/190 — Olive stabilizasyon isteği (B)
- [56] https://docs.kdenlive.org/en/cutting_and_assembling/editing.html — Kdenlive kurgu araçları (A, Ö)
- [57] https://docs.kdenlive.org/en/effects_and_filters/speech_to_text.html — Kdenlive konuşma tanıma (A, Ö)
- [58] https://www.capcut.com/resource/multicam-editing — CapCut çoklu kamera (A, Ö)
- [59] https://www.capcut.com/tools/video-transcript-editing — CapCut dökümden kurgu (A, Ö)
- [60] https://support.microsoft.com/en-us/clipchamp/how-to-use-autocaptions-in-clipchamp — Clipchamp otomatik altyazı (A, Ö)
- [61] https://www.openshot.org/static/files/user-guide/ — OpenShot kılavuzu: clips, effects, animation, color, ai, main_window sayfaları (A)
- [62] https://www.blackmagicdesign.com/media/partial/release/20260908-03 — DaVinci Resolve 21.1 duyurusu (A, Ö)
- [63] https://community.adobe.com/announcements-727/what-s-new-in-adobe-premiere-26-5-september-2026-1641187 — Premiere Pro 26.5 (A, Ö)
- [64] https://community.adobe.com/announcements-724/adobe-premiere-elements-2027-is-here-ai-powered-creativity-smarter-workflows-and-faster-editing-1642366 — Premiere Elements 2027 duyurusu (A)
- [65] https://support.microsoft.com/en-us/clipchamp/how-to-combine-videos — Clipchamp birleştirme ve geçiş adımları (Ö)
- [66] https://clipchamp.com/en/blog/save-4K-video-in-Clipchamp/ — Clipchamp çözünürlük seçimi (Ö)
- [67] https://clipchamp.com/en/whats-new/ — Clipchamp yenilikleri (A)
- [68] https://linuxiac.com/kdenlive-26-08-adds-track-reordering-better-timeline-editing/ — Kdenlive 26.08 haberi, ikincil (Ö)
- [69] https://kdenlive.org/news/releases/26.08.1/ — Kdenlive 26.08.1 duyurusu (A)
- [70] Depo içi: `docs/plan-duzenleyici.md`, `docs/piyasa/tarama-2026-10-05.md` satır 137, 142, 144 — yazılı kapsam ve önceki tarama (B)
- [71] https://github.com/jliljebl/flowblade/releases — Flowblade sürümü (B)

## Komşu Kola Not

- Sessizlik silme rakiplerde hâlâ açık istek: Kdenlive 11 oy [43], Shotcut forumu 23/5 [53], Shotcut yol haritasında [8]; Clipchamp'te var [19]; bizde `Core/SilenceCut.cs` var.
- Sessizlik ve sahne taraması yalnız ilk kaynağı tarıyor (çok kaynak planında not edilmiş).
- Akıllı kesmenin öteki kodeklere açılması açık: `Core/SmartCutCodec.cs`, `.claude/acik.md:28`.
- Düzenleyici planındaki D4 maddesi (`-force_key_frames`, `-g`) açık duruyor.
- Shotcut işaretçileri metin ya da gömülü bölüm olarak dışa aktarıyor [6]; bizde işaretçi yalnız arayüzde (`App/EditorTimeline.cs:155`).
- Shotcut altyazıyı kapatılabilir ayrı iz olarak dosyaya gömebiliyor [6]; bizde metin hep görüntüye yakılıyor.
