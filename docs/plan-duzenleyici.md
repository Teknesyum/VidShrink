# Düzenleyici — Lightworks/Premiere tarzı kesme şeridi

> "50+repo tara lightworks premiere pro vb tarzında bir video edit panelimizin olmasını
> istiyorum adam recorderle kayıt yaptı videosunu istediği kısmını kesip birleştirip
> istediği kısmın hızını değiştirebilmeli istediği kısmı kaldırabilmeli gerekirse -100
> hızda oynatabilmeli vb vb ayrı bir sekmede olacak / oynatıcı shrink editor vb hepsi aynı
> videoya odaklanmış olacak recorder yapıldıysa odak record edilen videoda"

Araştırma üç rapor + bir ölçüm kütüğü:
[NLE mimarisi](arastirma/video-duzenleme-nle-mimarisi-2026-09-15.md) (30 proje),
[ffmpeg/mpv teknikleri](arastirma/video-duzenleme-ffmpeg-mpv-2026-09-15.md),
[çizelge arayüzü](arastirma/video-duzenleme-cizelge-arayuzu-2026-09-15.md) (20 proje),
[ölçüm kütüğü](olcumler/video-duzenleme-olcum-kutugu-2026-09-15.txt).
Fable'ın netleştirmesi: [015](netlestirme/015-vidshrink-e-lightworks-premiere-tarzi-bi.md).

## Bugün nerede duruyoruz

**Ortak "geçerli video" nesnesi yok.** Her sekme dosyayı kendi açıyor:

| Yer | Bugünkü hal |
| --- | --- |
| [MainWindow.axaml:65](../src/VidShrink.App/MainWindow.axaml:65) | `TabControl x:Name="Tabs"`, yedi sekme (`TabPlayer` :137, `TabShrink` :142, `TabAdvanced` :1028 gizli) |
| [MainWindow.axaml.cs:2760](../src/VidShrink.App/MainWindow.axaml.cs:2760) | `OpenInShrinkAsync` — sekmeyi değiştirip `LoadAsync(path)`, **dosyayı baştan yükler** |
| [:2767](../src/VidShrink.App/MainWindow.axaml.cs:2767) | `OpenInPlayerAsync` — `Player.OpenAsync(path)`, ayrı bir motor örneği |
| [RecorderView.axaml.cs:150](../src/VidShrink.App/Recorder/RecorderView.axaml.cs:150) | Kaydedicinin sonucu iki `Func<string, Task>` kapısından elle geçiyor |
| [IPlaybackEngine.cs:80](../src/VidShrink.Player/IPlaybackEngine.cs:80) | `OpenAsync(string path)` — **tek dosya**, çizelge kavramı yok |

Elimizde olan: [PreviewTimeline](../src/VidShrink.Core/PreviewTimeline.cs:104) kaynak↔çıktı
zaman dönüşümünü zaten saf bir `record` olarak tutuyor (`SourceStartSeconds`,
`SourceDurationSeconds`, fps düşüşü). Çizelge modelinin tohumu burada; tek parçadan çok
parçaya genişleyecek.

## Ölçülmüş kısıtlar

Rapordaki sayılar ölçümden, kaynağı kütükte.

| Bulgu | Sonuç |
| --- | --- |
| mpv `--speed` aralığı 0,01–100 | **Negatif hız yok.** "-100 hız" canlı önizlemede imkânsız |
| `--play-direction=backward` mpv belgesinde "extremely fragile" | Canlı geri oynatma kapalı |
| mpv `edl://` her parçayı `timeline_part` yapıyor (`start`, `end`, `source_start`) | Kesilmiş/sıralanmış çizelge **kusursuz** önizlenir; **hız alanı yok** |
| ffmpeg `reverse`/`areverse` klibi tamamen RAM'e alıyor, 1080p30'da ~99 MB/s | Geri çevirme yalnız teslimde, süre sınırlı |
| Kayıpsız kopya kesimi anahtar kareye oturuyor | Hassasiyet GOP'a bağlı; smart cut kenar GOP'u yeniden kodluyor |

**Karar:** hız ve geri oynatma **teslimde** var, çizelgede **simgesel** gösterilir.
Önizlemede hız `speed` özelliğiyle, geri yön periyodik `seek` ile taklit edilir
([ffmpeg/mpv raporu §5.3](arastirma/video-duzenleme-ffmpeg-mpv-2026-09-15.md)). Bunu
kullanıcıdan saklamak yerine arayüzde söyleyeceğiz.

## Fable'ın beş sorusu

Üçünü burada cevaplıyorum, ikisi kullanıcının.

1. **Kapsam** — tek kaynak, tek şerit. Kaydedilen ya da açılan **bir** video kesilir,
   parçalar sıralanır, silinir, hızlanır. Çok kaynak / ikinci şerit / geçiş / başlık
   kapsam dışı; kullanıcının cümlesi tek videoyu tarif ediyor.
2. **Kesim hassasiyeti** — üç kip açıkça sunulur, varsayılan **smart cut**: gövde
   `-c copy`, yalnız kenar GOP yeniden kodlanır. Hızlı kip (kayıpsız, anahtar kareye
   oturur) ve tam kip (her şey yeniden kodlanır) yanında durur. Kullanıcı hangi kipte
   olduğunu görür.
3. **Geri oynatma** — yalnız teslimde. Gerekçe yukarıdaki tabloda.
4. **Odak sahipliği** — kullanıcının kararı, aşağıda çatal 1.
5. **Sıra** — kullanıcının kararı, aşağıda çatal 2.

## D0 — Ortak odak nesnesi

Kullanıcının ikinci cümlesinin tamamı bu madde: *"oynatıcı shrink editor vb hepsi aynı
videoya odaklanmış olacak"*.

`CurrentMedia` diye tek bir nesne: yol ve `MediaInfo`.
Sekmeler ondan okur, ona yazar. Kaydedici bittiğinde odak kendiliğinden kaydedilen dosyaya
geçer ([RecorderView.axaml.cs:117](../src/VidShrink.App/Recorder/RecorderView.axaml.cs:117)
zaten yolu elinde tutuyor).

Kazanç ölçülebilir: bugün oynatıcıdan küçültmeye geçiş dosyayı **ikinci kez** ffprobe'luyor
([MainWindow.axaml.cs:2726](../src/VidShrink.App/MainWindow.axaml.cs:2726)); ortak nesne
bu yoklamayı bir kereye indiriyor.

D0 olmadan düzenleyici sekmesi yedincinin yanına sekizinci bir ada olur.

## D1 — Kesim listesi modeli (Core)

Arayüz yok, süreç yok, saf `record`. Sprocket'in deseni: zaman `long` **tick**, saniyede
240.000 — 24/25/30/50/60 fps'in hepsini tam bölen tek taban, kayan nokta birikmesi yok
([NLE raporu](arastirma/video-duzenleme-nle-mimarisi-2026-09-15.md)).

- `EditClip` — `SourceStart`, `SourceEnd`, `Speed`, `Reversed`.
- `EditTimeline` — sıralı `EditClip` listesi; kesme, silme, taşıma, hız yazma.
- Komut yığını ile geri al / ileri al. Anlık görüntü değil komut — 4 GB'lık kaynakta anlık
  görüntü modeli belleği yiyor.
- `PreviewTimeline` tek parçalı özel hal olarak bunun üstüne oturur.

Bu dalga tamamen sınanabilir: süreç açmaz, dosya okumaz.

## D2 — `edl://` ile önizleme

Kesim listesi mpv'nin `edl://` metnine çevrilir ve mevcut motor onu tek dosya gibi açar
([MpvEngine.cs:227](../src/VidShrink.Player/MpvEngine.cs:227) `loadfile`). Yeni altyapı
yok. Kesilmiş ve sıralanmış çizelge kesintisiz oynar.

Hız ve geri yön burada **yok** (`timeline_part`'ın oran alanı yok). Hızlı parçada `speed`
özelliği ([MpvEngine.cs:260](../src/VidShrink.Player/MpvEngine.cs:260)) parça sınırında
yazılır; geri parçada periyodik `seek`.

## D3 — Çizelge denetimi (App)

FramePFX'in melez yolu: şerit ve klip kutuları Avalonia denetimi, yoğun kısım (dalga formu,
küçük resim şeridi, cetvel) tek `DrawingContext`. Cap'in kuralı: **yalnız görünen aralık
çizilir**. Küçük resimler diskte önbelleklenir (Pitivi'nin deseni).

- Cetvel 1-2-5-10 merdiveni.
- Yakalama eşiği piksel cinsinden, **saklanan değer zaman** (Pitivi `edgeSnapDeadband`).
- Oynatma başı ↔ mpv eşitlemesi mevcut nesil sayacını kullanır; sürükleme birikmesi bu
  depoda zaten çözülmüş.
- `PlayheadAutomationPeer : RangeBaseAutomationPeer` — ekran okuyucu.

**Bu dalga yeni belirteç ve palet rengi ister** (klip gövdesi, seçim, oynatma başı, şerit
zemini). Ölçü `Themes/Theme.axaml`'a, renk 26 palet dosyasının hepsine girer. Sayı
uydurmuyorum; belirteç adları ve değerleri dalga başlarken ayrıca sorulacak.

## D4 — Teslim

Üç kip (fable sorusu 2) ffmpeg'e çevrilir: kayıpsız parçalar concat demuxer'la, kenar
GOP'lar yeniden kodlanıp araya, hız `setpts`/`atempo`, geri `reverse`/`areverse` — süre
sınırı ve bellek uyarısıyla.

Bir ek: **VidShrink kendi çıktısını kayıpsız kesilebilir yapabilir.** Küçültme ve kayıt
argümanlarına `-force_key_frames`/`-g` konursa kendi ürettiğimiz dosya sık anahtar kareli
olur ve hızlı kip neredeyse kare hassasiyetine çıkar. Kaydedicinin argümanında `-g` kolu
zaten var (`KayitFfmpegKoluTests`).

## D5 — Kısayollar

Altı NLE'nin ortak çekirdeği: J/K/L, I/O, M, Space, Ctrl+Z, Ctrl+A, Home/End
([çizelge raporu](arastirma/video-duzenleme-cizelge-arayuzu-2026-09-15.md)). Yedi tuş
bugünkü `Keymap.cs` ile çakışıyor (S, X, Z, A, B, M, C); çakışma tablosu raporda. Çözüm
sekmeye bağlı kapsam: düzenleyici sekmesi etkinken NLE haritası, diğerlerinde bugünkü
harita.

## Sıra

1. **D0** — ortak odak. Tek başına bugünkü altı sekmeye değer katıyor.
2. **D1** — kesim listesi. Arayüzsüz, tamamen sınanabilir.
3. **D2** — `edl://` önizleme. D1'in doğruluğunu gözle görünür yapıyor.
4. **D3** — çizelge denetimi. En büyük kalem, belirteç kararı gerektiriyor.
5. **D4** — teslim.
6. **D5** — kısayollar.

Her dalga `dotnet test` yeşiliyle kapanır. Süre tahmini yazmıyorum; bu depoda tahminin
hükmünü ölçüm verir.

## Çatal 2 — cevaplandı (16 Eylül 2026)

**Sıra: önce hipersürüş.** [docs/plan.md](plan.md) bu işin önünde koşuyor; düzenleyici
dalgaları hipersürüş kapandıktan sonra başlıyor.

## Çatal 1 — cevaplandı (16 Eylül 2026)

**Varsayılan düğmeyle.** Kayıt biter, sonuç panelinde "Düzenleyicide Aç" durur; basmazsan
hiçbir sekme değişmez. Küçültme sekmesinde başka bir dosyayla uğraşıyorsan o dosya altından
kaymaz.

**Kendiliğinden Ayarlar'da bir seçenek.** Açık olduğunda kaydı durdurduğun anda yeni dosya
geçerli video olur ve bütün sekmeler ona döner. Aynı anahtar oynatıcıda açılan dosya için de
geçerli: seçenek kapalıyken küçültme sekmesi kendi dosyasında kalır.

D0 bu yüzden iki şey taşıyor: ortak `CurrentMedia` nesnesi ve onu **kimin** değiştirebildiğini
söyleyen tek bir ayar. **Kuruldu.** Ayar T7'de yerleşen `ChkFollowRecording`
(`settings.json`'da `followRecording`, metinler `settings-tab.follow-recording.{label,hint}`);
yeni kullanıcı metni gerekmedi. Nesne `src/VidShrink.App/CurrentMedia.cs`: yol ve
`MediaInfo`. Başlangıçtaki süre / kaynak fps / son konum / sahip alanları 18 Eylül 2026'da
kaldırıldı — üçü `MediaInfo`'nun ya da `PlaybackHistory`'nin izdüşümüydü, sahip ise hiç
okunmuyordu ve "kim değiştirir" kapısı zaten bu onay kutusu
(`docs/danisma/2026-09-18-fable-currentmedia-olu-yuzey.md`). Çift yoklama
`MainWindow.OdakTakibi.cs`'in `Prober` dikişi ve uçuş paylaşımıyla bire indi; ölçü
`docs/olcumler/k19-d0-ortak-odak.md`.
