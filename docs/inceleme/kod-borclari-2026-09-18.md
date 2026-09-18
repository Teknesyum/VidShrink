# Kod Borçları Denetimi — 18 Eylül 2026

Kapsam: `src/` (211 dosya, `obj/` ve `bin/` hariç), `tests/` (224 dosya), son 20 commit.
Her bulgu grep çıktısına dayanır; hüküm vermeyen iki madde "şüpheli" işaretlidir.

`TODO/FIXME/HACK` deseni `src/` altında **sıfır** eşleşme verdi. 24 "geçici" eşleşmesinin
19'u gerçekten geçici dosya/durum anlatan docstring; yalnız `PlanCalculator.cs:146`
açıkça "bu kod kalkacak" diyor.

## En ağır

1. `tests/VidShrink.Tests/OluUyeTests.cs:559-560` — Pim "MacUpdate.DownloadTimeout hiçbir
   yerde okunmuyor" diyor, `src/VidShrink.Core/UpdateCheck.cs:1645` okuyor
   (`Timeout = DownloadTimeout`). Denetim düzeneğinin kendisi yanlış iddiayı pimliyor;
   kök neden tarayıcının nitelenmemiş okumayı görmemesi.
2. `tests/VidShrink.Tests/OluUyeTests.cs:561-562` — Aynı kör noktanın ikincisi:
   `UpdateCheck.ManifestTimeout` üretimde `UpdateCheck.cs:205`'te okunuyor.
3. `src/VidShrink.Core/PlanCalculator.cs:146-149` — `IEncoderMeasurementState` kendini
   geçici ilan ediyor ama dört üretim yerinde tüketiliyor; T129 birleşince sökülecek iş
   her yeni çağrı yeriyle büyüyor.
4. `src/VidShrink.App/MainWindow.axaml.cs` — 4980 satır, depodaki en büyük dosya
   (ikincisi 1883). Yoklama siyaseti, donanım hükmü ve `ReasonCode` kolları Core yerine
   pencerede duruyor. **Şüpheli:** nicel gözlem, hüküm değil.
5. `tests/VidShrink.Tests/OluUyeTests.cs:513-534` — `ShareFailure`'ın 11 üyesinden 8'i
   üretiliyor, dördü okunuyor; sınıflandırma mı fazla arayüz mü eksik, ölçülmemiş.
6. `tests/VidShrink.Tests/OluUyeTests.cs:549-556` — `PresetKind`'ın dört üyesi de ölü:
   ön ayar kütüphanesi motorda duruyor, türe göre ayıran üretim kolu yok.
7. `src/VidShrink.Ffmpeg/CropProbe.cs:10` — Kırpma yoklaması üretimde hiç koşmuyor,
   yalnız iki test dosyasından çağrılıyor; `EncodePlan.SuggestedCrop` da okunmuyor.
8. `tests/VidShrink.Tests/OluUyeTests.cs:495-508` — Önizleme türlerinin beş üyesi
   üretiliyor, okuma tarafında adı geçmiyor.
9. `src/VidShrink.App/CurrentMedia.cs:52-63`, `MainWindow.OdakTakibi.cs:93-95`,
   `src/VidShrink.Cli/CliApp.cs:446-447` — Aynı yol eşitliği üç gövdede ve davranışları
   farklı: biri `ArgumentException`'ı yutuyor, ikisi yutmuyor.
10. On ayrı yerde elle süre biçimlendirme (`CliApp.cs:443`, `MainWindow.axaml.cs:3238`,
    `:4255`, `:4342`, `:4404`, `PlayerView.Tools.cs:299`, `RecorderView.Serit.cs:48`,
    `ShrinkJobWindow.axaml.cs:272`, `ConversionArguments.cs:157`) — biçimler tutarsız,
    üç yerde `InvariantCulture` yok. `PlayerView.Serit.cs:69`'un "ikinci kopya yok"
    iddiası yanlış.

## Orta

11. `src/VidShrink.Core/HdrResolver.cs:5-10`, `PlanCalculator.cs:135-139`,
    `MainWindow.axaml.cs:2709-2718` — "ölçülmedi" üçüncü durumu üç katmanda ayrı taşınıyor.
12. `OluUyeTests.cs:543-544` — `FfmpegArguments.SceneMapRuleOfRecord` üretimde sıfır,
    ölçüm tarafında beş görünüm.
13. `OluUyeTests.cs:775-778` — `IdetCounts.Progressive` / `.Undetermined` ayrıştırılıyor,
    karar kuralı okumuyor.
14. `OluUyeTests.cs:485-494` — `ArchitectureOutcome.Assumed` ve
    `HardwareVerdictReason.BitrateFloorTooHigh` hiçbir kolda ayrılmıyor.
15. `OluUyeTests.cs:487-488` — `ComparisonSourceState.Duraklatildi` hesaplanıp atılıyor.
16. ~~`src/VidShrink.App/Playback/PlayerView.axaml.cs:232-238` — Bitmiş bir turun
    (`VIDSHRINK_T176_TRACE`) hata ayıklama iskelesi kalıcı yüzeyde.~~ **Kapandı:**
    `Echo` ve iki çağrı yeri (`MainWindow.axaml.cs`) kaldırıldı; ortam değişkenini
    hiçbir test ya da üretim yolu okumuyordu. `_trace` duruyor, `view.Trace` üzerinden
    ölçüm okuyor.
17. ~~`src/VidShrink.App/App.axaml.cs:58-66` — Geçici klasör temizliği çıplak `catch { }`
    ile susturuluyor; iz başarıyı değil yalnız denemeyi kanıtlıyor.~~ **Kapandı:**
    yakalanan tür ize düşüyor (`gecici-temizlik-hata=<Tür>`), başarılı yolun izi
    değişmedi.
18. `OluUyeTests.cs:781-784` — `InstallProgress.Bar` / `.Sentence` yalnız testler okusun
    diye ayakta; açılış paneli silindiği için `Sentence`'ın ekranı da yok.

## Hafif

19. `src/VidShrink.App/CurrentMedia.cs`, `MainWindow.OdakTakibi.cs` — Son üç commit'te
    yüzeyi değişen bu iki dosyanın ölçü zarfı en dar (iki test dosyası; karşılaştırma:
    `MediaInfo` 69, `EncodePlan` 42). **Şüpheli:** incelik, yokluk değil.
20. `docs/handbrake/README.md:16-22` — Envanter borcu; ölçümü
    `docs/handbrake/envanter-tamlik-2026-09-18.md`'de.
