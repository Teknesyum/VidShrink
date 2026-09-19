# Kod Borçları Denetimi — 18 Eylül 2026

Kapsam: `src/` (211 dosya, `obj/` ve `bin/` hariç), `tests/` (224 dosya), son 20 commit.
Her bulgu grep çıktısına dayanır; hüküm vermeyen iki madde "şüpheli" işaretlidir.

`TODO/FIXME/HACK` deseni `src/` altında **sıfır** eşleşme verdi. 24 "geçici" eşleşmesinin
19'u gerçekten geçici dosya/durum anlatan docstring; yalnız `PlanCalculator.cs:146`
açıkça "bu kod kalkacak" diyor.

## En ağır

1. ~~Pim "MacUpdate.DownloadTimeout hiçbir yerde okunmuyor" diyor, `UpdateCheck.cs:1645`
   okuyor.~~ **Kapandı:** kök neden tarayıcının yalnız `Tür.Üye` görünümünü aramasıydı;
   bir alan kendi sınıfının içinden nitelenmeden okunuyor (`Timeout = DownloadTimeout`).
   `MemberScan` artık üyenin **kendi türünü bildiren dosyada** çıplak adı da arıyor;
   bildirim satırı okuma sayılmıyor. Ölçü üç alanı da kümeden düşürdü ve başka hiçbir
   satır kaymadı — yani kör nokta kapandı, yanlış pozitif gelmedi.
   `OluUyeTests.NitelenmemisOkumaGoruluyor` dosya ve satırı pimliyor.
2. ~~Aynı kör noktanın ikincisi: `UpdateCheck.ManifestTimeout`.~~ **Kapandı:** aynı ölçüyle.
   Üçüncüsü `DeveloperUnlock.Window` idi; gerekçesinde kör nokta zaten itiraf edilmişti.
   Üç pim de kaldırıldı: düzenek artık yanlış bir iddiayı pimlemiyor.
3. ~~`src/VidShrink.Core/PlanCalculator.cs:146-149` — `IEncoderMeasurementState` kendini
   geçici ilan ediyor ama dört üretim yerinde tüketiliyor; T129 birleşince sökülecek iş
   her yeni çağrı yeriyle büyüyor.~~ **Kapandı — öncülü yanlıştı:** arayüz yalnız durum
   taşımıyordu, ertelenmiş yoklamayı kuyruğa alan **tetik** de oydu; Core çağrı yerlerini
   silmek yoklamayı tümden durdururdu. Tetik üç durumlu yüze taşındı, arayüz kalktı.
   Ölçüm `docs/olcumler/k8-yoklama-tetigi-2026-09-19.md` (taban 0/86, beş kesimin beşi
   kırmızı).
4. ~~`src/VidShrink.App/MainWindow.axaml.cs` — 4980 satır, depodaki en büyük dosya
   (ikincisi 1883). Yoklama siyaseti, donanım hükmü ve `ReasonCode` kolları Core yerine
   pencerede duruyor. **Şüpheli:** nicel gözlem, hüküm değil.~~
   **Kapandı 19 Eylül 2026 — öncülü kısmen yanlıştı:** donanım hükmü zaten Core'da
   (`Core/HardwareVerdict.cs`), `ReasonCode` da öyle (`Core/EncodePlan.cs`); pencerede
   duran şey hükmü çağıran yol ve kod → dil anahtarı eşlemesiydi, ikisi de arayüz işi.
   Doğru olan tek şey boydu: üç kapalı bölük kendi dosyasına ayrıldı ve dosya
   5081 → 4481 satıra indi (`MainWindow.Yoklama.cs` 415, `MainWindow.Gerekce.cs` 209).
   Taşıma kaynaktan okuyan bir ölçüyü kırdı ve ölçü yeni dosyaya bağlandı.
   Döküm `docs/olcumler/k8-pencere-boyu-2026-09-19.md`.
5. ~~`tests/VidShrink.Tests/OluUyeTests.cs:513-534` — `ShareFailure`'ın 11 üyesinden 8'i
   üretiliyor, dördü okunuyor; sınıflandırma mı fazla arayüz mü eksik, ölçülmemiş.~~
   **Kapandı — öncülü yanlıştı:** sınıflandırma fazla değil, kullanıcıya görünen ayrım
   enum'dan daha ince yapılıyor (11 üyeye karşılık 22 anahtar, 42 dilde pimli). Ölü olan
   `RetryAfter` ile `SuggestedTargetId`'ydi: Core hesaplıyor, üç gösterim yüzeyi de atıyordu.
   `Core/Share/ShareRetry.cs` tek karar gövdesi oldu, `ShareStep` tanıya taşındı ve yeniden
   deneme düğmesi üç yüzeyde aynı gövdeden çiziliyor. Ölçü baytların akıp akmadığı: ağ ve
   sunucu hatası yalnız `Prepare`/`Init` adımlarında denenir, yoksa kullanıcı ikinci kopya
   yükler. `NetworkFailure`/`RateLimited`/`ServiceError` pimleri düştü, kalan beşi
   `varsayilan-kol`a döndü. Süit 59/59; beş kesimin beşi kırmızı, taban ve geri 0/99
   (`docs/olcumler/k8-paylasim-tekrar-2026-09-19.md`).
6. ~~`tests/VidShrink.Tests/OluUyeTests.cs:549-556` — `PresetKind`'ın dört üyesi de ölü:
   ön ayar kütüphanesi motorda duruyor, türe göre ayıran üretim kolu yok.~~ **Kapandı:**
   kütüphane CLI'dan açıldı (`profiller`/`presets` komutu, `--profil`/`--profile` bayrağı),
   `PresetLibrary.Find` ve `OfKind` artık üretimde koşuyor ve chip'i olmayan on profil
   seçilebiliyor. `User` pimi düştü; kalan üçü `varsayilan-kol` biçimine döndü ve borçtan
   meşruya çevrildi — listeleme dört türü tek tablodan geziyor, adıyla karşılaştıran tek
   kol `User`. `OnAyarKitapligiTests` 10/10; altı kesimin altısı kırmızı
   (`docs/olcumler/k8-onayar-kitapligi-2026-09-19.md`).
7. ~~`src/VidShrink.Ffmpeg/CropProbe.cs:10` — Kırpma yoklaması üretimde hiç koşmuyor,
   yalnız iki test dosyasından çağrılıyor; `EncodePlan.SuggestedCrop` da okunmuyor.~~
   **Kapandı:** `--kirp` bayrağı yoklamayı üretime soktu; `CropDetection.Rect` pimi de
   düştü. `OtomatikKirpmaTests` 6/6, beş kesimin beşi kırmızı
   (`docs/olcumler/k8-kirpma-2026-09-19.md`).
8. ~~`tests/VidShrink.Tests/OluUyeTests.cs:495-508` — Önizleme türlerinin beş üyesi
   üretiliyor, okuma tarafında adı geçmiyor.~~ **Kısmen kapandı:** üç `PreviewState` pimi
   madde 21 ile birlikte düştü (türün kendisi kaldırıldı). **Artanı da kapandı:** kalan iki
   pim ölçülünce altında canlı bir kusur çıktı — kodlayıcının kalite ölçeği modellenmiyorsa
   rozet sayı üretemiyor ve lafza uyulunca parça nihai çıktıdan en çok saptığı yerde hiç
   uyarı çıkmıyordu. `Desteklenmiyor` haline sayısız ayrı bir rozet kondu
   (`main.preview.temsili`, 42 dil); o pim düştü, `Yaklasik` meşruya çevrildi. Beş kesimin
   beşi kırmızı, taban ve geri 0/12 (`docs/olcumler/k8-onizleme-rozeti-2026-09-19.md`).
9. ~~`src/VidShrink.App/CurrentMedia.cs:52-63`, `MainWindow.OdakTakibi.cs:93-95`,
   `src/VidShrink.Cli/CliApp.cs:446-447` — Aynı yol eşitliği üç gövdede ve davranışları
   farklı: biri `ArgumentException`'ı yutuyor, ikisi yutmuyor.~~ **Kapandı:** gövde
   `Core/PathEquality.Same`'e indi. Bulgu üçü sayıyordu, ölçünün tarayıcısı **beş**
   buldu — `ShrinkEngine.cs:108` ve `EncodeRunner.cs:492` de aynı deseni yazıyordu.
   Harf duyarlılığı artık sabit değil, `WatchFolder.PathComparison` dikişinden geliyor.
   `YolEsitligiTests`; iki mutasyon (sabit `Ordinal`, `ArgumentException` korumasının
   kalkması) 1'er kırmızı.
10. ~~On ayrı yerde elle süre biçimlendirme~~ **Kapandı:** gövde `Core/Saat`'e indi
    (`Ekran`, `Kalan`, `Kesit`, `Ffmpeg`, `DosyaAdi`); tarama on iki değil **on dört**
    çağrı yeri buldu. Plan ve mutasyon dökümü `docs/plan.md`. Eski satır: on ayrı yerde
    elle süre biçimlendirme (`CliApp.cs:443`, `MainWindow.axaml.cs:3238`,
    `:4255`, `:4342`, `:4404`, `PlayerView.Tools.cs:299`, `RecorderView.Serit.cs:48`,
    `ShrinkJobWindow.axaml.cs:272`, `ConversionArguments.cs:157`) — biçimler tutarsız,
    üç yerde `InvariantCulture` yok. `PlayerView.Serit.cs:69`'un "ikinci kopya yok"
    iddiası yanlış.

## Orta

11. ~~`src/VidShrink.Core/HdrResolver.cs:5-10`, `PlanCalculator.cs:135-139`,
    `MainWindow.axaml.cs:2709-2718` — "ölçülmedi" üçüncü durumu üç katmanda ayrı
    taşınıyor.~~ **Borç değil, hüküm:** iki bayrak iç içe bir küme, kopya değil.
    `PlanCalculator.cs:1539/1566/1599/1641`'in dördü de `NotMeasured` ile
    `CodecNotMeasured`'ı birlikte kuruyor; ayrıştıkları tek yer `:370`, HDR yolu yalnız
    geniş olanı kuruyor — çünkü ölçülmemiş bir HDR kararı kodlayıcı seçimini geçici
    yapmaz. Ayrım `:292-295`'te yazılı ve `CodecLockTests` ile
    `EncoderStateConsumptionTests` iki bayrağı ayrı ayrı pimliyor. Birleştirmek HDR
    ayrımını kaybettirirdi.
12. ~~`OluUyeTests.cs:543-544` — `FfmpegArguments.SceneMapRuleOfRecord` üretimde sıfır,
    ölçüm tarafında beş görünüm.~~ **Borç değil, şart:** alan üretimin varsayılan
    kuralının altın kopyası. Üretime indirilse ya da `ThresholdRule.Measured`'dan okusa
    ölçü sabiti kendisiyle karşılaştırır ve kural kaysa bile yeşil kalırdı. Bağımsızlığın
    yük taşıdığı ölçüldü (`docs/olcumler/k8-kayitli-kural-2026-09-19.md`); pim meşruya
    çevrildi.
13. ~~`IdetCounts.Progressive` / `.Undetermined` ayrıştırılıyor, karar kuralı okumuyor.~~
    **Borç değil, üçüncü kör nokta:** karar kuralı `counts.Total`'ı okuyor,
    `Total => Tff + Bff + Progressive + Undetermined` ise iki kolonu da **nitelenmeden**
    topluyor. Özellik tarayıcısı da yalnız `.Üye` görünümünü arıyordu; aynı kural ona da
    verildi. Ölçü **elli** pimi kümeden düşürdü (altısı elle doğrulandı, hepsi gerçekten
    okunuyordu) ve tek satır bile yeni gelmedi. `OzellikteNitelenmemisOkumaGoruluyor`;
    okuma önekleri listesini boşaltmak 1 kırmızı.
14. ~~`OluUyeTests.cs:485-494` — `ArchitectureOutcome.Assumed` ve
    `HardwareVerdictReason.BitrateFloorTooHigh` hiçbir kolda ayrılmıyor.~~ **Kapandı:**
    bit hızı tabanı `_` jokerindeydi; adıyla yazıldı, tanınmayan sebep artık cümle
    kurmuyor. `Assumed` borç değil, iki üyeli türün `else` kolu — meşruya çevrildi.
    Üç kesimin üçü kırmızı; ilk turda bir kör nokta çıkıp kapandı
    (`docs/olcumler/k8-gerekce-kolu-2026-09-19.md`).
15. ~~`OluUyeTests.cs:487-488` — `ComparisonSourceState.Duraklatildi` hesaplanıp
    atılıyor.~~ **Kapandı:** ayırmak gerekiyormuş. Kaynak kendi kararıyla duraklıyor,
    şerit düğmesi "oynuyor" demeye devam ediyordu; durum artık şeride ve sese iniyor.
    Üç kesimin üçü kırmızı (`docs/olcumler/k8-duraklama-seride-2026-09-19.md`).
16. ~~`src/VidShrink.App/Playback/PlayerView.axaml.cs:232-238` — Bitmiş bir turun
    (`VIDSHRINK_T176_TRACE`) hata ayıklama iskelesi kalıcı yüzeyde.~~ **Kapandı:**
    `Echo` ve iki çağrı yeri (`MainWindow.axaml.cs`) kaldırıldı; ortam değişkenini
    hiçbir test ya da üretim yolu okumuyordu. `_trace` duruyor, `view.Trace` üzerinden
    ölçüm okuyor.
17. ~~`src/VidShrink.App/App.axaml.cs:58-66` — Geçici klasör temizliği çıplak `catch { }`
    ile susturuluyor; iz başarıyı değil yalnız denemeyi kanıtlıyor.~~ **Kapandı:**
    yakalanan tür ize düşüyor (`gecici-temizlik-hata=<Tür>`), başarılı yolun izi
    değişmedi.
18. ~~`OluUyeTests.cs:781-784` — `InstallProgress.Bar` / `.Sentence` yalnız testler okusun
    diye ayakta; açılış paneli silindiği için `Sentence`'ın ekranı da yok.~~ **Kapandı:**
    kök neden panelin silinmesi değil, iki okuma yoluydu. `Advance` artık `void`, çubuk
    her yerde `Bar`'dan okunuyor; `Sentence` silindi, son cümle `History`'nin son satırı.
    Altı kesimin altısı kırmızı (`docs/olcumler/k8-ilerleme-yuzeyi-2026-09-19.md`).

## Hafif

19. ~~`src/VidShrink.App/CurrentMedia.cs`, `MainWindow.OdakTakibi.cs` — Son üç commit'te
    yüzeyi değişen bu iki dosyanın ölçü zarfı en dar (iki test dosyası; karşılaştırma:
    `MediaInfo` 69, `EncodePlan` 42). **Şüpheli:** incelik, yokluk değil.~~
    **Kapandı — şüphe ikiye ayrıldı:** `CurrentMedia` gerçekten inceydi, sekiz ölçü
    damgayı ve tazeliği zaten pimliyor. `MainWindow.OdakTakibi.cs`'te iki koruma
    ölçüsüzdü (geri dönen oynatıcı olayı, patlayan açılış); ikisi de pimlendi.
    Ölçüm `docs/olcumler/k8-odak-korumalari-2026-09-19.md` (taban 0/14, üç kesimin üçü
    kırmızı).
20. ~~`docs/handbrake/README.md:16-22` — Envanter borcu; ölçümü
    `docs/handbrake/envanter-tamlik-2026-09-18.md`'de.~~ **Kapandı:** borç zaten
    ödenmişti (döküm HandBrakeCLI 1.11.2'nin kendi `--help` çıktısından alınmıştı),
    yalnız README'deki itiraf paragrafı güncellenmemişti. Paragraf kapanışı gösteriyor.

## Sonradan çıkan (19 Eylül 2026)

21. **En ağır.** `src/VidShrink.Ffmpeg/FrameGrabber.cs` (520 satır) üretimde **hiç
    kurulmuyor**: `new FrameGrabber` yalnız testlerde geçiyor, `GrabPairAsync`'in üretimde
    tek çağıranı yok. Onu besleyen `PreviewState` / `PreviewStatus` makinesi de
    (`PreviewTimeline.cs`) aynı durumda — `Derive` yalnız testlerden çağrılıyor, oysa
    kendi belgesi "arayüz kendi koşullarından durum uydurmaz, bunu çağırır" diyor.
    Karşılaştırma paneli T176'dan beri libmpv yolundan besleniyor
    (`EngineComparisonFrameSource`), ffmpeg'den kare kesen bu yol onun altında kaldı.
    Testleriyle birlikte 1416 satır. Madde 8'in beş pimi bu kökün yaprağı: üyeler
    üretilmiyor değil, üreten sınıfın kendisi üretimde koşmuyor.
    **Kapandı:** üç dosya `trash/`'a taşındı, 21 düz pim ve 3 borç pimi düştü, başka satır
    kaymadı. Taşıma ikinci bir ölü tür ortaya çıkardı (`KeyframeIndex`, tek okuyucusu ölü
    sınıftı); o da taşındı. Ölçüm `docs/olcumler/k8-olu-kare-yolu-2026-09-19.md`.
