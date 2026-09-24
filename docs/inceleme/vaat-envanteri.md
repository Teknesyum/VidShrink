# VidShrink vaat envanteri

Kod salt okundu, hiçbir dosya değiştirilmedi. Rapor dosyaya yazılmadı, tamamı burada. Dosya yolları `src/` altına göredir. Metin anahtarları `Locales/tr/*.json`, CLI metinleri `VidShrink.Cli/Locales/tr.json` içindedir.

Özet: 60 vaat incelendi. 36'sı tutarlı, 18'i şüpheli, 6'sı yalan. En ağır bulgular:
- `--aci` bayrağı okunuyor ama hiçbir yerde kullanılmıyor.
- `--kodek h264 --hizli` kullanıcıya haber vermeden AV1/HEVC donanım kodlayıcısı seçiyor.
- MKV'de seçilen AAC ses notsuz biçimde Opus oluyor.
- İki kodlayıcı-yedek metni gerçekte olmayan bir "yazılıma düşme"den söz ediyor.
- Kayıt iptalinde "dosya silindi", silme başarısız olsa da yazılıyor.

## 1. Kap (container) ve uzantı

1. `CmbContainer` (Dönüştür sekmesi) | `App/MainWindow.axaml.cs:4448-4497, 4581-4622` | Çıktı yolu `BuildUniqueOutputPath(..., plan.Container)` ile kuruluyor, uzantı seçilen etiketle aynı | durum: TUTARLI
2. Dönüştürmede uyumsuz kap/kodek | `Core/ConversionArguments.cs` Validate | AVI+x265, AVI+AAC gibi eşleşmeler görünür hatayla reddediliyor, sessiz MOV→MP4 yok | durum: TUTARLI
3. Ön ayar kabı (MKV/MOV) → küçültme uzantısı | `App/MainWindow.axaml.cs:3842-3843`, `Core/PresetLibrary.cs:285-294` | Uzantı ön ayar kabından, yoksa `plan.Streams.Extension`'dan geliyor | durum: TUTARLI
4. Kaynak zaten hedefin altında (kopyalama / pass-through) | `Ffmpeg/EncodeRunner.cs:539-563` | Çıktının uzantısı kaynağın uzantısına çevriliyor. Ön ayarın ya da KeepAllTracks'in MKV/MOV seçimi yok sayılıyor ve bunu söyleyen not yok | durum: ŞÜPHELİ
   - Kanıt: PassThroughAsync `deliveredPath`'i kaynağın uzantısıyla kuruyor.
5. VP9'a kilitli plan yedeğe düşünce | `Core/StreamMapping.cs:314-315`, `Core/PlanCalculator.cs:1619-1628` | libvpx-vp9 çalışmazsa libx264'e düşülüyor ve kap sessizce webm→mp4 oluyor. Kullanıcı yalnız EncoderFallback notunu görüyor | durum: ŞÜPHELİ
   - Kanıt: `ContainerFor(request, codec)` yalnız `IsVp9(codec)` iken WebM veriyor.
6. `main.reason.stream.extra-audio-dropped` ("…'İzleri koru'yu açın") | `Core/StreamMapping.cs:461, 475-476`; `main.json:438` | MOV/MP4 kaplı ön ayarda "İzleri koru" açıkken de fazla izler düşüyor. Not, zaten açık olan kutuyu açmayı öğütlüyor | durum: ŞÜPHELİ
   - Kanıt: `var keepAll = request.KeepAllTracks && !request.PlatformDelivery && !IsMp4Family(container);`
7. Ses kodeği AAC (`CmbAdvAudioCodec` / `--ses-kodek aac`) + MKV | `Core/StreamMapping.cs:570`, `Core/PlanCalculator.cs:1786-1792` | AAC seçildiği halde MKV'ye Opus yazılıyor. Not eklenmiyor | durum: YALAN
   - Kanıt: `else if (!IsMp4Family(container) && codec == "aac") codec = "libopus";` satırında `notes.Add` yok.
   - Karşılaştırma: WebM kolu (:565-569) `WebmAudioOpus` notunu ekliyor.
8. WebM'de sesin Opus olması | `Core/StreamMapping.cs:484-490, 565-569` | `WebmAudioOpus` notuyla bildiriliyor | durum: TUTARLI
9. FLAC/Dolby ses yedeği | `Core/StreamMapping.cs:535-558` | `FlacFellBack`, `DolbyCodecNotInContainer` ve `DolbyCodecBelowChannelFloor` notlarıyla bildiriliyor | durum: TUTARLI
10. HandBrake içe aktarma: webm + VP9 olmayan kodlayıcı | `Core/PresetLibrary.cs:535-540` | Approximated/NoEquivalent notu raporlanıyor | durum: TUTARLI
11. CLI `--cikti YOL` uzantısı | `Cli/CliRequest.cs:231-232`, `Core/StreamMapping.cs:322-328, 678-687`, `Core/FfmpegArguments.cs` (`-f` yok) | Uzantı doğrulanmıyor. `.avi` gibi bilinmeyen bir uzantıda akış planı MP4 kurallarıyla kuruluyor, muxer'ı ise ffmpeg uzantıdan seçiyor | durum: ŞÜPHELİ
    - Kanıt: `ContainerOf` bilinmeyen uzantıda `_ => OutputContainer.Mp4` döndürüyor. Çıktı argümanlarında `-f` yalnız null-geçişte var (:605).
12. CLI yardımı: "Varsayılan girdinin yanında <girdi>_shrunk.mp4" | `Cli/CliApp.cs:368-372` | Uzantı profil kabına ya da kodeğe göre mkv/webm/mov olabiliyor | durum: ŞÜPHELİ
    - Kanıt: `extension = DeliveredExtension(Profile.Container) ?? plan.Streams.Extension ?? "mp4"`
13. Kayıt kabı GIF | `Ffmpeg/RecorderSession.cs:293-302` | Kayıt önce MKV olarak alınıp sonra çevriliyor. Çevirme başarısız olursa MKV yolu ve yalnız "ffmpeg N koduyla bitti." gösteriliyor, GIF'in olmadığı söylenmiyor | durum: ŞÜPHELİ
14. Kayıt tamponu açıkken seçili kap GIF | `App/Recorder/RecorderView.Tampon.cs:159-173` | Sessizce MKV kaydediliyor. Bildirim yolu gösteriyor ama nedenini söylemiyor | durum: ŞÜPHELİ
15. Kayıt kabı ile uzantının eşleşmesi | `Core/RecorderArguments` Validate (:626-629) | Uyumsuzluk hatayla reddediliyor | durum: TUTARLI

## 2. Kodek etiketi ve gerçek kodlayıcı

16. Plan paneli "kodlayıcı" satırı | `App/MainWindow.axaml.cs:3680-3718` | `plan.Codec` yani gerçekten planlanan kodlayıcı gösteriliyor | durum: TUTARLI
17. `CodecLabel` (otomatik / uyumlu / en küçük) | `App/MainWindow.axaml.cs:2174-2179` | Bir tercih etiketi, kodlayıcı adı olduğunu iddia etmiyor. Gerçek kodlayıcı 16. satırda | durum: TUTARLI
18. `CmbConvertCodec` (Dönüştür) | `Ffmpeg/EncodeRunner.cs:565-593` | `plan.VideoCodec` birebir kullanılıyor, yedek yok | durum: TUTARLI
19. `main.advice.encoder-fallback-gpu` ("…yazılım kodlayıcısına düştü ve hız kazancı yok") | `Core/PlanCalculator.cs:433-444, 1735-1754`; `App/MainWindow.Gerekce.cs:196-226` | Hızlı GPU açık ve av1_nvenc yokken hevc_nvenc gibi başka bir donanım kodlayıcı seçiliyor. Öneri yine "yazılıma düştü" diyor | durum: YALAN
    - Kanıt: `preferredCodec = … fast ? FastHardwareOrder[0]` olduğundan hevc_nvenc ≠ av1_nvenc farkı EncoderFallback üretiyor.
    - Kanıt: `AdviceCode.EncoderFallback => fastGpu ? "main.advice.encoder-fallback-gpu"` kodlayıcının donanım olup olmadığına bakmıyor.
20. `main.reason.encoder-fallback-not-working` + "yazılım karşılığına düşüldü" (EncoderPath=Donanım) | `Core/PlanCalculator.cs:348-367, 433-444` | Kullanıcı donanımı seçti ve donanıma geçildi. Metin ise "libx264 bu makinede kullanılamadı, X'e düşüldü" diyor | durum: YALAN
    - Kanıt: :355 `codec = PickFastCodec(...)`. Ardından :433'te `preferredCodec = PreferredCodecFor(preference)` = libx264 ≠ codec, bu da EncoderFallback'i tetikliyor. Oysa ManualEncoderPathOverride notu zaten eklenmiş.
21. NotMeasured nedenli yedek önerisi ("yazılım karşılığına düşüldü") | `Core/PlanCalculator.cs:1744-1754`; `App/MainWindow.Gerekce.cs:196-226` | `PickFastCodec` ölçülmemiş bir donanım adayı döndürdüğünde de metin "yazılım" diyor | durum: ŞÜPHELİ
22. CLI `--kodek h264` ile `--hizli` birlikte | `Cli/CliRequest.cs:207, 215`; `Core/PlanCalculator.cs:337, 433, 1735-1754` | `PickFastCodec` tercihe bakmadan av1_nvenc/hevc_nvenc seçiyor. `preferredCodec` da `FastHardwareOrder[0]` olduğu için hiç not çıkmıyor. Kullanıcı H.264 isteyip AV1 alıyor | durum: YALAN
    - Kanıt: `: fast ? PickFastCodec(preference, …)`. Tercih yalnız hiçbir aday yokken `FallbackCodecFor(pref)` içinde kullanılıyor.
23. CLI `--kodek av1` | `Cli/CliRequest.cs:208`; `Core/PlanCalculator.cs:1523-1535` | Kilit değil, yalnız MaxCompression tercihi. libsvtav1 çalışmazsa libx265 kullanılıyor. `kucult` metin çıktısı kodeği ve gerekçeyi hiç yazmıyor (`Cli/CliApp.cs:550-569`) | durum: ŞÜPHELİ
24. CLI `--kodek hevc` / `--kodek vp9` | `Cli/CliRequest.cs:240-241`; `Core/PlanCalculator.cs:1638-1656` | Kilitleniyor, ama kodlayıcı çalışmazsa libx265/libx264'e düşülüyor. `plan` bunu yalnız "Gerekçe: EncoderFallback" kod adıyla gösteriyor, `kucult` metninde hiç yok | durum: ŞÜPHELİ

## 3. İlerleme, süre ve boyut göstergeleri

25. Yüzde | `Ffmpeg/EncodeRunner.cs:686-739` | `out_time_ms` / süre oranı, geçiş (pass) aralığına yayılıyor | durum: TUTARLI
26. `main.output.remaining` "Kalan" (ve CLI `progress.encode` "{2} kaldı") | `Ffmpeg/EncodeRunner.cs:681-684`; `App/MainWindow.axaml.cs:4651-4659`; `Cli/CliApp.cs:691-701` | Formül `elapsed/fraction − elapsed` ve yalnız o anki denemeyi kapsıyor. Yeniden deneme ve doldurma turlarında sıfırlanıyor. HDR10+ çıkarma aşamasını hesaba katmıyor | durum: ŞÜPHELİ
27. `main.output.current-size` "Güncel çıktı boyutu" | `App/MainWindow.axaml.cs:4651-4659` | ffmpeg'in `total_size` değerinden (`p.OutputMb`) okunuyor | durum: TUTARLI
28. `main.output.estimated-time` | `Core/ComplexityProfile.cs:369-385` | Yalnız plana uyan ölçülmüş hız varsa yazılıyor, yoksa "-". Kopyalamada Copy gösteriliyor | durum: TUTARLI
29. `main.output.estimated-output` / CLI `plan.estimate` | `Core/PlanCalculator.cs:283-285, 1013-1035` | Kesit uygulanmış kaynak bilgisiyle hesaplanıyor, kopyalamada kaynak boyutu | durum: TUTARLI
    - Özetteki "kesitsiz süre" kuşkusu çürüdü: `trim.Apply(info)` kullanılıyor.
30. Sonuç boyutu (`TxtOutSize`, `main.run.done`) | `Ffmpeg/EncodeRunner.cs:283, 333, 560, 589`; `App/MainWindow.axaml.cs:4264-4269` | Gerçek FileInfo boyutu | durum: TUTARLI
31. `main.run.done` içindeki "{0} denemede" | `Ffmpeg/EncodeRunner.cs` FillUpAsync | Teslim edilen önceki sonuç olsa bile sayaç artıyor | durum: ŞÜPHELİ
32. CLI `result.size` | `Cli/CliApp.cs:554` | `result.OutputMb` gerçek boyut | durum: TUTARLI

## 4. Kaydedildi / kopyalandı / paylaşıldı / silindi / tamamlandı bildirimleri

33. `main.run.done` "tamamlandı" | `App/MainWindow.axaml.cs:4264-4281` | Yalnız `result.Success` iken. Hedefin üstü kabul edildiyse ek açıklama geliyor | durum: TUTARLI
34. `main.run.converted` | `App/MainWindow.axaml.cs:4581-4622` | ConvertAsync başarılıysa, FileInfo boyutuyla | durum: TUTARLI
35. `main.run.over-ceiling` ("…hedeften büyük dosya asla verilmez") | `App/MainWindow.axaml.cs:4282-4287`; `App/ShrinkJobWindow.axaml.cs:714-719`; `Ffmpeg/EncodeRunner.cs:431-435, 445` | Bu işte doğru, çünkü yalnız `!Success` iken gösteriliyor. Ama "asla" mutlak bir söz: kullanıcı kabul ederse (AcceptLarger) ve CLI'da (`askBeforeRetry` null → `EndRun(deliverSmallestOver: true)`) hedeften büyük dosya teslim ediliyor | durum: ŞÜPHELİ
36. `settings.share.shared` / `shared-until` (3 pencere) | `App/MainWindow.axaml.cs:2556-2567`; `App/ShrinkJobWindow.Paylas.cs:125-135`; `App/Recorder/RecorderView.Paylas.cs:115-125` | Yalnız `result.Ok && result.Link` iken | durum: TUTARLI
37. `settings.share.closed` | `App/MainWindow.axaml.cs:2593-2611` | Yalnız `DeleteAsync` sonucu Ok iken, değilse `close-failed` | durum: TUTARLI
38. Bağlantıyı "Kopyala" düğmesi | `App/MainWindow.axaml.cs:2580-2591`; `App/ShrinkJobWindow.Paylas.cs:147-157` | Başarı iletisi yok, hata `catch {}` ile yutuluyor. Pano yoksa düğme hiçbir şey yapmıyor ve bunu söylemiyor | durum: ŞÜPHELİ
39. `main.ai.prompt-copied` | `App/MainWindow.axaml.cs:4164-4183` | `SetTextAsync`'ten sonra, hatada `clipboard-failed` | durum: TUTARLI
40. `main.preset.deleted` | `App/MainWindow.OnAyar.cs:251-270` | Export başarılı olduktan sonra, IO hatasında erken dönüş | durum: TUTARLI
41. `recorder.snapshot.saved` | `App/Recorder/RecorderView.Serit.cs:73-95`; `Ffmpeg/RecorderSession.cs:215-216` | Koşul: `run.Ok && File.Exists && Length > 0` | durum: TUTARLI
42. `recorder.output.mp4-saved` | `App/Recorder/RecorderView.axaml.cs:245-267` | Yalnız ffmpeg başarılıysa | durum: TUTARLI
43. `recorder.replay.saved` | `App/Recorder/RecorderView.Tampon.cs:173`; `Ffmpeg/ReplayRecorder.cs:128-130` | Koşul: `run.Ok && File.Exists(target)` | durum: TUTARLI
44. `recorder.discarded` ("Kayıt iptal edildi, dosya silindi.") | `App/Recorder/RecorderView.Serit.cs:213-222` | Silme hataları yutuluyor, ileti koşulsuz yazılıyor | durum: YALAN
    - Kanıt: `catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }` → ardından `ShowNotice(Say("recorder.discarded"))`.
45. `recorder.output.done` ("Kayıt bitti: …") | `App/Recorder/RecorderView.axaml.cs:167-170` | `result.Ok == false` iken de yazılıyor. Hata satırı altına ekleniyor ama başlık "bitti" diyor | durum: ŞÜPHELİ
46. Kayıt teslimi (tek parça ya da bölünmüş) | `Ffmpeg/RecorderSession.cs:505-507, 524, 548-553` | `File.Move` hatası `catch { }` ile yutuluyor. Sonuç yine `Ok = true` ve gösterilen yol diskte olmayabilir | durum: ŞÜPHELİ
47. `recorder.output.partial` ("Dosya kaydedildi ve oynatılabilir") | `App/Recorder/RecorderView.axaml.cs:172-178, 196-198` | "Oynatılabilir" sözü dosya denetlenmeden, yalnız kap türünden (`SurvivesKill`) çıkarılıyor | durum: ŞÜPHELİ
48. `player.tools.saved` | `App/Playback/PlayerView.Tools.cs:357-365`; `App/Playback/ClipExport.cs:90-98` | Dosya yazıldıysa (`written`) | durum: TUTARLI
49. `player.view.screenshot-saved` | `App/Playback/PlayerView.Window.cs:229-245`; `Player/MpvEngine.cs:452-473` | mpv tamamlandı dedikten sonra `File.Exists` ile kontrol ediliyor | durum: TUTARLI

## 5. CLI bayrakları

50. `--aci N` ("DVD açısı") | `Cli/CliRequest.cs:385-391` | Değer doğrulanıp `Angle`'a yazılıyor ama hiçbir yerde okunmuyor. Bayrak etkisiz | durum: YALAN
    - Kanıt: `src/` altında `.Angle` için tek eşleşme yazıldığı satır (`CliRequest.cs:390`).
51. `izle` komutuyla `--crf`, `--on-ayar`, `--modul`, `--kes`, `--bolum`, `--profil`, `--suzgec`, `--ses-*`, `--altyazi`, `--yak`… | `Cli/CliRequest.cs:309-447` (`when command != CliCommand.Watch`) → :461 | Yardım yalnız `--aralik` ve `--bir-kez` için "izle:" kısıtı yazıyor. Diğerleri izle'de "Bilinmeyen seçenek" hatası alıyor, bu da seçeneğin var olmadığını ima ediyor | durum: ŞÜPHELİ
52. `--json` (izle'de NDJSON) | `Cli/CliApp.cs:320, 665` | `JsonLines = request.Json` ile girintisiz, satır başına bir nesne | durum: TUTARLI
53. `--dil` (öntanımlı sistem dili) | `Cli/Program.cs:13`; `Cli/CliText.cs:46-82` | `CurrentUICulture` kullanılıyor. Geçersiz değer yutulmuyor, 64 ile çıkılıyor | durum: TUTARLI
54. `--hizli` | `Cli/CliRequest.cs:215` | SpeedMode.Fast, penceredeki GPU kutusuyla aynı | durum: TUTARLI (kodek etkileşimi için bkz. 22)
55. `--olcumsuz` | `Cli/CliApp.cs:218-220` | Profil olmadan Decide; plan kaynak bit hızından kuruluyor | durum: TUTARLI
56. `--vmaf` ("libvmaf varsa") | `Cli/CliApp.cs:418-422, 557-559` | Ölçüm yoksa "VMAF: ölçülmedi" yazılıyor | durum: TUTARLI
57. `--kes` / `--bolum` ("hedef kesite uygulanır; bölüm işaretleri düşer") | `Core/PlanCalculator.cs:283-285`; `Core/StreamMapping.cs:224`; `Core/FfmpegArguments.cs:498` | TrimWindow plana giriyor, `-map_chapters -1` ekleniyor | durum: TUTARLI
58. `--profil` + elle verilen `--kodek` ("elle verilen üstüne yazar") | `Cli/CliRequest.cs:218-229` | Profilin kodeği yalnız `Codec == Auto` iken alınıyor | durum: TUTARLI
59. Çıkış kodu 3 ("boyut tavanı aşıldı") | `Cli/CliApp.cs:396-400, 563-567` | CeilingExceeded olunca 3. En küçük dosya teslim edildiyse bu `result.ceiling-kept` ile açıkça söyleniyor | durum: TUTARLI
60. `plan.stream.extra-audio-dropped` ("CLI'da bu seçenek yok") | `Cli/CliRequest.cs` | CLI'da KeepAllTracks bayrağı gerçekten yok | durum: TUTARLI

## Doğrulanmayanlar

Bunlar tabloya girmedi ve sayılmadı:
- `main.run.ended` iletisindeki "dosya yazılmadı" sözünün tüm hata yollarında geçerli olup olmadığı.
- `--on-ayar`'da başka kodeğe ait bir adın plan tarafında nasıl düşürüldüğü.
- CLI `plan` metnindeki "Ses: aac" satırının MKV'de Opus'a dönüp dönmediği. Çıktı `plan.AudioCodec`'ten basılıyor. Bu alan güncellenmiyorsa 7. maddedeki yalan CLI plan metninde de görünür.

## Sayım

| | Adet |
|---|---|
| Toplam | 60 |
| TUTARLI | 36 |
| ŞÜPHELİ | 18 |
| YALAN | 6 |

YALAN olanlar: 7 (MKV'de AAC→Opus), 19 (GPU yedek metni), 20 (EncoderPath=Donanım yedek metni), 22 (`--kodek h264 --hizli`), 44 (`recorder.discarded`), 50 (`--aci`).
## Kapanış (2026-09-24)

Altı YALAN ve on sekiz ŞÜPHELİ satır dört dalda ele alındı: CLI (dea75fd2), plan ve kodek (0c363026), kaydedici (5cf13277), kalan uçlar (2b3f3656, `docs/olcumler/yalan-yok-kalan-uclar.md`).

Doğrulanmayan üç madde:
- `main.run.ended`: yalnız `EncodeRunner.cs:534` kolunda doğuyor; o kolda `outputPath`'e yazılmıyor, cümle doğru. Döngüden her `continue` `attempt < attemptLimit` iken çıktığı için kol pratikte erişilmez.
- `--on-ayar` başka kodeğin adı: plan kurulurken düşüyor ve gerekçeye satır bırakıyor (`CliCrfOnAyarTests`).
- CLI "Ses:" satırı: `.mkv`'de doğru, `.webm`'de yanlıştı; 2b3f3656 düzeltti.
