# Anamorfik Ölçekleme — Gösterim Oranı Korunuyor Mu (23 Eylül 2026)

HandBrake açık durumu #40: "Ölçekleme — setsar yok, anamorfik koruma ölçülmedi"
(`docs/handbrake/acik-durumu-2026-09-17.md:65`). Soru: SAR≠1 kaynak küçültülünce
gösterim oranı (DAR) korunuyor mu, yoksa çıktı yassı/esnetilmiş mi çıkıyor?

## Bulgu

İki bağımsız argüman üreticisi var, ikisi de farklı durumda:

- **Küçültme yolu** (`PlanCalculator` → `VideoFilterChain` → `FfmpegArguments`):
  doğru. `PlanCalculator.Dimensions` (`src/VidShrink.Core/PlanCalculator.cs:1411-1417`)
  hedef genişliği `info.DisplayWidth`'ten kurar (`MediaInfo.cs:78-85`,
  SAR düzeltmeli gösterim genişliği); `VideoFilterChain.Filters`
  (`src/VidShrink.Core/VideoFilterChain.cs:218-247`) sonra `setsar=1` ekler —
  zincir zaten doğru orana ölçeklenmiş kareyi kare piksele çeviriyor, esnetme yok.
- **Dönüştür yolu** (`ConversionArguments.VideoFilters`,
  `src/VidShrink.Core/ConversionArguments.cs:103-112`, düzeltmeden önceki hâl):
  **bozuktu**. Kullanıcı çözünürlük seçmediğinde (`Width`/`Height` ikisi de null —
  bu, `MainWindow.axaml.cs:4377-4426` `ReadConversionPlan()`'ın varsayılan hâli,
  "kaynak çözünürlüğü" seçiliyken) hiçbir `scale` süzgeci eklenmiyordu ama
  `info.IsAnamorphic` ise `setsar=1` yine ekleniyordu — kaynağın doğru SAR'ını
  sıfırlayıp depolanan (yassı) orana düşürüyordu. Aynı kusur, yükseklik seçilip
  genişlik seçilmediği dalda da vardı (`scale=-2:{h}` — ffmpeg'in `-2`'si depolanan
  oranı korur, gösterim oranını değil).

`PassThroughResult` (`PlanCalculator.cs:900-943`) kasıtlı olarak ham `Width`/`Height`
taşıyor ama zarasız: bu plan hiç süzgeç zincirinden geçmiyor, `EncodeRunner.cs:113-114`
→ `FfmpegArguments.cs:776` düz `-c:v copy` yapıyor, video akışına dokunulmuyor.

## Kanıt: Ham Ffprobe Ölçümü

Kaynaklar `ffmpeg -f lavfi -i testsrc2=size=<ölçü>:rate=30 -t 2 -vf setsar=<SAR> ...`
ile üretildi (`tests/VidShrink.Tests/AnamorfikTests.cs`, `Uret()`). Ölçüm komutu:
`ffprobe -v error -select_streams v:0 -show_entries stream=width,height,sample_aspect_ratio,display_aspect_ratio -of json <dosya>`.

Düzeltmeden önce (`ConversionArguments.VideoFilters`, hiçbir dal SAR'ı telafi etmiyor):

| Kaynak | Kaynak SAR | Kol | Çıktı W×H | Çıktı SAR | Çıktı DAR | Oran | Beklenen |
| --- | --- | --- | --- | --- | --- | --- | --- |
| dvd-ntsc 720×480 | 32:27 | ölçeksiz (Width/Height null) | 720×480 | 1:1 | 3:2 | 1,500 | 1,778 |
| dvd-ntsc 720×480 | 32:27 | yükseklik-sadece (Height=240) | 360×240 | 1:1 | 3:2 | 1,500 | 1,778 |

Düzeltmeden sonra (`scale=trunc(iw*sar/2)*2:ih` / `scale=trunc(iw*sar*{h}/ih/2)*2:{h}` eklendi):

| Kaynak | Kaynak SAR | Kol | Çıktı W×H | Çıktı SAR | Çıktı DAR | Oran | Beklenen |
| --- | --- | --- | --- | --- | --- | --- | --- |
| dvd-ntsc 720×480 | 32:27 | ölçekli (Width=426,Height=240) | 426×240 | 1:1 | 71:40 | 1,775 | 1,778 |
| dvd-ntsc 720×480 | 32:27 | ölçeksiz (Width/Height null) | 852×480 | 1:1 | 71:40 | 1,775 | 1,778 |
| dvd-ntsc 720×480 | 32:27 | yükseklik-sadece (Height=240) | 426×240 | 1:1 | 71:40 | 1,775 | 1,778 |
| dvd-pal 720×576 | 64:45 | ölçekli (Width=512,Height=288) | 512×288 | 1:1 | 16:9 | 1,778 | 1,778 |
| dvd-pal 720×576 | 64:45 | ölçeksiz | 1024×576 | 1:1 | 16:9 | 1,778 | 1,778 |
| dvd-pal 720×576 | 64:45 | yükseklik-sadece (Height=288) | 512×288 | 1:1 | 16:9 | 1,778 | 1,778 |
| kare-kontrol 640×480 | 1:1 (kontrol) | ölçekli/ölçeksiz/yükseklik-sadece | 320×240 / 640×480 / 320×240 | 1:1 | 4:3 | 1,333 | 1,333 |

Ölçüm test çıktısından alındı: `dotnet test tests/VidShrink.Tests -c Release --no-build
--filter "FullyQualifiedName~KucultulmusAnamorfikCiktiGosterimOraniniKoruyor"
--logger "console;verbosity=detailed"`.

## Karar

`src/VidShrink.Core/ConversionArguments.cs:103-112`, `VideoFilters`: iki yeni dal
eklendi — `plan.Height` verilip `plan.Width` verilmediğinde ve ikisi de verilmediğinde,
kaynak anamorfikse ffmpeg'in `sar` değişkeniyle gösterim genişliği hesaplanıyor
(`scale=trunc(iw*sar*{h}/ih/2)*2:{h}` / `scale=trunc(iw*sar/2)*2:ih`), C# tarafında
`DisplayWidth` matematiği tekrarlanmadı. Kullanıcı hem genişlik hem yükseklik verdiğinde
(`Width` ve `Height` ikisi de dolu) dal değişmedi — o zaten kullanıcının kasıtlı ölçü
seçimi, dokunulmadı.

## Açılan Yüzey

| Yüzey | Yer |
| --- | --- |
| Dönüştür yolu ölçek dalları | `src/VidShrink.Core/ConversionArguments.cs` (`VideoFilters`) |
| Ölçü | `tests/VidShrink.Tests/AnamorfikTests.cs` (`KucultulmusAnamorfikCiktiGosterimOraniniKoruyor`, 3 kol) |

## Mutasyon Turu

| # | Kesim | Kırmızı |
| --- | --- | --- |
| Taban (düzeltmeli) | — | 0/1 |
| M1 | "ikisi de null" dalı silindi (özgün kusur) | 1/1 |
| M2 | "yükseklik-sadece" dalı `scale=-2:{h}`'ye geri döndü (özgün kusur) | 1/1 |
| Geri (düzeltmeli) | — | 0/1 |

İki kesim de kırmızı: her iki dal da testle pimlendi.
