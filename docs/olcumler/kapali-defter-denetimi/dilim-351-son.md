# Kapalı Defter 351-son ve Açık Defter Denetimi

Kapsam: `.claude/kapali.md` satır 351-366 (16 satır, [x]) ve `.claude/acik.md`'nin tamamı (24 satır: 21 açık, 3 kapalı).

## Sayılar

- kapali.md 351-366: 16 satır, **16 doğrulandı**, 0 şüpheli.
- acik.md [x]: 3 satır, **3 doğrulandı**, 0 şüpheli.
- acik.md [ ]: 21 satır, gerekçe geçerliliği kontrol edildi → **20 geçerli, 1 eskimiş** (satır 13/14, aynı konu iki satırda).

## kapali.md 351-366 — doğrulama detayı

| Satır | İddia (özet) | Bulgu | Sonuç |
|---|---|---|---|
| 351-352 | E4 başlık/kaynak tarama: SourceTitles, --tarama/--baslik/--ana-icerik/--aci/--asgari-sure | `src/VidShrink.Core/SourceTitles.cs` var; `CliRequest.cs:362,365,373,377,384` beş bayrağın hepsi eşleniyor; `docs/olcumler/e4-baslik-tarama.md` var | Doğru |
| 353 | 42 dile main.title.label | `grep -rl main.title.label` → 133 eşleşme (42 dil dosyası dahil) | Doğru |
| 354-357 | Çeviri grup 1-4, commit 03355927 | 03355927 main'in atası | Doğru |
| 358-362 | HandBrake 154 bayrak hükmü (64/35/55) | `docs/handbrake/bayrak-hukumleri-2026-09-19.md` (39.6K) var | Doğru |
| 363 | E8 süzgeç: VideoFilterChain.Parse, commit 8b171a80 | `VideoFilterChain.cs`, `PlanCalculator.cs`, `FfmpegArguments.cs`, `ConversionArguments.cs` içinde kullanılıyor; 8b171a80 main'in atası | Doğru |
| 364 | Anamorfik PAR: MediaInfo.ParNum/ParDen/DisplayWidth, FfprobeClient.PikselOrani | `MediaInfo.cs:52,55,61,68-69`, `FfprobeClient.cs:76-77,307` mevcut; `docs/olcumler/e9-anamorfik.md` var | Doğru |
| 365 | TimestampAlignment.Note → CLI metin/JSON raporu | `QualityMeter.cs:22,24,209` alignment üretiyor; `CliApp.cs:562,591,596` result.alignment'a yazıyor; `docs/olcumler/hizalama-raporu-2026-09-19.md` var | Doğru |
| 366 | CropProbe → --kirp/--crop, CliServices.DetectCrop | `CliApp.cs:17,208` DetectCrop enjeksiyonu; `CliRequest.cs:359,84` --kirp/--crop; `docs/olcumler/k8-kirpma-2026-09-19.md` var | Doğru |

Commit hash doğrulaması (`git merge-base --is-ancestor <hash> main`): cb09221e, feb55507, 0666989b, a94d3b48, 9c16cbb5, 03355927, 8b171a80, 8f09dc60, 0de17ff5, b0f187d5, 1bfad1fe — hepsi ANCESTOR.

## acik.md [x] satırları

| Satır | İddia | Bulgu | Sonuç |
|---|---|---|---|
| 15 | "Arayuz: kesilen yazilar, ust uste binen cerceveler" düzeltildi | commit 62eac5eb + f4851f4a (319 kusur→0) main'in atası | Doğru |
| 17 | "ajan: Arayüz taşma/çakışma taraması" | Aynı iş, f4851f4a main'in atası | Doğru |
| 21 | "ajan: C1-2/4 çeviri betiği" | commit e72bfbae ("Kuyruk duzenleme ve bitince eylemi (C1-2, C1-4)") main'in atası | Doğru |

## Şüpheli tablo

Kapali.md 351-366 ve acik.md [x] satırlarında **0 şüpheli** bulundu — hepsi kod/commit/belge ile eşleşiyor.

## Açık ([ ]) satırların gerekçe geçerliliği

| Satır | Gerekçe | Durum | Neden |
|---|---|---|---|
| 1-5 | osx-arm64/x64 libmpv, macOS dalgası bekliyor | Geçerli | Dalga başlamadı, iş yapılmamış |
| 6 | AlternativeTo e-posta doğrulanmamış | Geçerli | Doğrulama kullanıcı elinde, değişmedi |
| 7 | Avast yanlış pozitifi, SignPath kullanıcının işi | Geçerli | Kullanıcı eylemi bekleniyor |
| 8 | HandBrake her alanda geçilsin, fable kararları geldi | Geçerli | Not kendi içinde "denetim 19 Eylül'de yapıldı" diyor, satır kapanmamış görünse de üstündeki iş listesi (B1a-d vb.) hâlâ eksik — bkz. kapali.md:63 |
| 9 | NVENC 2: kalite üstünlüğü karşılanmadı | Geçerli | 2026-09-22 ölçümü de üstünlüğün hâlâ karşılanmadığını doğruluyor (hevc/av1 gürültü mertebesinde) |
| 10 | WhatsApp gerçek gönderim ölçümü — ajan yapamaz | Geçerli | Fiziksel telefon eylemi, ajan kapsamı dışı |
| 11 | HandBrake 1.12.0 yayımlanmadı | Geçerli | Not "2026-09-22 yoklandı" diyor, bugünün tarihiyle aynı gün — taze |
| 12 | "Kullaniciya yalan yok" denetimi — kullanıcı kararıyla sona bırakıldı | Geçerli | Başlanmamış, karar hâlâ geçerli |
| **13** | **main CI kırmızı: B1a AudioCodec kopya kusuru** | **ESKİMİŞ** | `PlanCalculator.cs:1106` `WithTarget` içinde `AudioCodec = options.AudioCodec` artık var (commit ad7ddaea "B1a: kalite hedefi kopyasi ses kodegini tasiyor, dort pim tazelendi", ve 89ee7f86 "Denetim: effective kopyasi ses kodegini tasiyor" — ikisi de main'in atası). `OlcekModuluTests.KopyaHicbirSecenegiDusurmuyor` artık `PlanOptions`'ın tüm yazılabilir özelliklerini regex ile tarıyor ve AudioCodec kopyada geçiyor. Kusur kapanmış, satır kapatılmamış. |
| **14** | **main CI kırmızı: AudioCodec kopya kusuru + dört pim — jobs.md** | **ESKİMİŞ** | Aynı kusurun tekrarı (13 ile aynı konu); aynı gerekçeyle geçersiz |
| 16 | Defteri tek tek gözden geçir — jobs.md | Geçerli | Bu denetim serisi (18,19,22,23,24 dahil) bu işin parçası, tamamlanmadı |
| 17,18,19,22,23,24 | Ajan denetim/tarama görevleri, "sonucu aktarılacak" | Geçerli (17 hariç, o [x] işaretli ve doğrulandı yukarıda) | 18,19,22,23,24 hâlâ açık denetim görevleri; 24 bu raporun kendisi |
| 20 | B1 arayüz yüzeyi, C1-6 ile birlikte | Geçerli | `git log --all` içinde "C1-6" geçen commit yok; C1-6 henüz yapılmamış (kapali.md:63 notuyla uyumlu) |

`gh run list -L 10 --branch main`: en güncel 10 koşumdan biri `completed failure` (19:29:31Z, "Acilir bolumler yerine suzuluyor" — konusu farklı, animasyon/reduced-motion, AudioCodec ile ilgisiz), geri kalanı `cancelled` (art arda push nedeniyle) veya `pending`/`in_progress`. AudioCodec kusuruna bağlı bir kırmızı koşum şu an görünmüyor; kusur zaten koda düzeltilmiş durumda.

## Özet

16 kapalı satır (351-366) ve 3 kapalı-işaretli açık-defter satırı incelendi, tamamı kod/commit/belge ile örtüşüyor; şüpheli bulunmadı. Açık defterin 21 açık satırından 20'si hâlâ geçerli, 1 konu (satır 13 ve 14, aynı AudioCodec kusuru) **eskimiş**: kusur `PlanCalculator.cs`'deki `WithTarget` kopyasına `AudioCodec = options.AudioCodec` eklenerek (commit ad7ddaea, doğrulama 89ee7f86) main'de zaten kapatılmış, ama acik.md'de hâlâ açık görünüyor.
