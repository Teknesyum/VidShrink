# T161 — Önizleme durdur/baslat yapinca bastan basliyor

Belirti: oynatirken durdur/baslat yapilinca, ayar degismedigi halde is bastan basliyor.

Butun satir numaralari **bu belgeyi tasiyan commit'teki** dosyalara aittir.

## K1 — Iki kosum izgarasi

Olcum `tests/VidShrink.Tests/PlaybackResumeTests.cs`, `Durdur_baslat_onden_hazirligi_bastan_baslatmaz`.
Iki kosum: A durdur/baslat yapiyor, B (kontrol) hicbir sey yapmiyor. Sayilan cagri:
onden hazirligin (`PanelHost.PrepareAheadAsync`) `SegmentEncoder.StartedEncodes` uzerinden
BASTAN baslatilma sayisi.

Hata duzeltilmeden once, ham cikti (`.calisma/T161/K1-baseline-bug-var.txt`):

```
T161 K1 durdur/baslat izgarasi -- onden hazirligin bastan baslama sayisi
  kontrol (durdur/baslat YOK): 1
  durdur/baslat kosumu       : 2
  fark (fazladan cagri)      : 1
```

Fazladan kosan cagri: `SegmentEncoder`in kodlama baslatmasi (bir tam ffmpeg parca kodlamasi),
onden hazirlanmis pencerenin durdur/baslat sirasinda atilip yeniden kodlanmasi.

## K2 — Neden

Kod yolu: `src/VidShrink.App/Playback/PanelHost.cs:955` `ApplyPlayState()`.

Duzeltmeden onceki hali:

```csharp
private void ApplyPlayState()
{
    var source = _source;
    if (source is null) return;
    if (_panel.Controls.IsPlaying) source.Play();
    else
    {
        source.Pause();
        // Duraklatıldığında ileri hazırlık durur; duran oynatma için parça kodlanmaz.
        if (_aheadRunning) _segments.Cancel();
    }
}
```

Duraklatma (`IsPlaying=false`), onden hazirlik (`PrepareAheadAsync`) surerken
`_segments.Cancel()` cagiriyordu. `SegmentEncoder` tek bir `_inflight` iptal kaynagi
tasir (`SegmentEncoder.cs:93`); `Cancel()` o an kosan ffmpeg surecini durdurup atiyor.
Baslat'a basildiginda `_ahead` hala `null` oldugu icin bir sonraki `PrepareAheadAsync`
cagrisi (ya da pencere dolup `AdvanceClip()` cagirdiginda) ayni pencereyi **sifirdan**
yeniden kodluyordu — ayar hic degismemesine ragmen.

Duraklatma kendi isini hala yapiyor: `Follow` (`PanelHost.cs:766`) ilk satirinda
`if (!_panel.Controls.IsPlaying) return;` ile donuyor, yani duraklamisken **yeni** bir
onden hazirlik hic baslamiyor. `Cancel()` mesru yerlerinde (dosya degisimi, plan
gecersizlesmesi, atlama, kapanma) duruyor.

## K3 — Duzeltme, iki yarisi da olculdu

Kriter iki sey soyluyor: durdur/baslat **kodlamayi yeniden tetiklemez** ve **oynatma
kaldigi konumdan surer**. Uc olcu var, ucu de ayri sey sayiyor.

### K3.1 — Kodlama yeniden tetiklenmiyor

`ApplyPlayState()` duraklatmada artik `_segments.Cancel()` cagirmiyor; onden hazirlik
arka planda bitmeye birakiliyor, boylece Baslat'a basildiginda pencere zaten hazir olur.
Ayni K1 izgarasi, duzeltme sonrasi:

```
T161 K1 durdur/baslat izgarasi -- onden hazirligin bastan baslama sayisi
  kontrol (durdur/baslat YOK): 1
  durdur/baslat kosumu       : 1
  fark (fazladan cagri)      : 0
```

### K3.2 — Konum: konak boruyu yeniden kurmuyor

`Durdur_baslat_boruyu_yeniden_kurmaz`. "Kaldigi konumdan surer"in konak icin olculebilir
karsiligi: durdur/baslat sonrasi **yeni kaynak ornegi uretilmedi** (yani `Teardown` +
`_factory()` kosmadi), ayakta duran boruya **ikinci bir `StartAsync` gitmedi**,
**`SeekAsync` hic cagrilmadi** ve `ActiveClip` **ayni ornek**, ayni baslangic aninda.

```
T161 K3 durdur/baslat -- boru ayakta mi
  kaynak ornegi (fabrika cagrisi): once 2, sonra 2
  ayakta duran boru StartAsync   : once 1, sonra 1
  Pause/Play/Seek                : 1/1/0
  pencere baslangici (sn)        : once 2, sonra 2
```

### K3.3 — Konum: borunun kare damgasi devam ediyor

`Duraklatilan_boru_kaldigi_karenin_ardindan_surer`. Gercek `PipeComparisonFrameSource`
uzerinde, konak hic isin icinde degil. Kareler alinir; `Pause()` sonrasi halka bosaltilir
(bekleyen kare kalsaydi "devam etti" sonucu `Play()` hic calismadan da cikardi); `Play()`
sonrasi gelen ilk karenin damgasi duraklamadaki son damgayla karsilastirilir.

```
T161 K3 duraklatilan boru -- kare damgasi (sn)
  duraklamadaki son kare : 0,133
  devam eden ilk kare    : 0,167
  fark                   : 0,033
```

Fark tam bir kare (30 fps'te 0,033 sn). Esik iki yonlu: geriye dusen damga bastan
baslamadir, bir saniyeden fazla ileri atlayan damga icerik atlamasidir.

**Olculmeyen:** ekrandaki serit konumu (`ComparisonPanel.Controls.Position`). O deger
`PanelHost.Drain` icinde, gercek `RequestAnimationFrame` dongusu akarken yaziliyor;
basli olmayan bir pencerede o dongu hic donmuyor. Yukaridaki iki olcu, seridi besleyen
iki girdinin (ayakta kalan boru + ilerleyen kare damgasi) korundugunu gosteriyor,
seridin kendi metnini degil.

## K4 — Mesru yeniden kurulumlar bozulmadi

Sozlesmenin istedigi uc yol — boyut degisimi, dosya degisimi, plan degisimi — ucu de
kendi olcusuyle asagida. Dorduncu bir kol (ilk parca hazir olunca) tur 2'den kaldi ve
kendi adiyla duruyor; sozlesmenin uc yolundan biri degil, ayri bir yol.

```
T161 K4 dosya degisimi -- fabrika cagrisi: acilista 1, dosya degisince 2

T161 K4 boyut degisimi -- yerlesme sayaci ve fabrika cagrisi
  acilista       : fabrika 1, yerlesme sayaci kurulu False
  pano kuculunce : yerlesme sayaci kurulu True
  tik sonrasi    : fabrika 2

T161 K4 plan degisimi -- imza zinciri
  baslangic         : sira 1, fabrika 2, kodlama 1
  ayni plan verildi : sira 1, gecikme kurulu False
  yeni plan verildi : sira 2, gecikme kurulu True
  gecikme sonrasi   : fabrika 3, kodlama 2

T161 K4 ilk parca hazir -- fabrika cagrisi: parca oncesi 1, parca sonrasi 2
```

**Dosya degisimi** — `Dosya_degisimi_hala_yeniden_kurar`. Zincir `PanelHost.SetFiles`
(`PanelHost.cs:297`) `if (_open) { RefreshRight(); Restart(); }` (`:327`). Kaynak
fabrikasi acilista 1, yeni dosya verilince 2 kez cagrildi.

**Boyut degisimi** — `Boyut_degisimi_hala_yeniden_kurar`. Zincir:

```
PanelHost.cs:123  _panel.Frames.SizeChanged += (_, _) => OnResized();
PanelHost.cs:506  OnResized  -> terfi / MinPanelEdge / ResizeTolerance kapilari -> _settle.Start()  (:519)
PanelHost.cs:116  _settle.Tick -> SettleElapsed()
PanelHost.cs:136  SettleElapsed -> _settle.Stop(); Restart();
```

`OnResized` `Restart`i **dogrudan cagirmiyor**, yerlesme sayaci (`_settle`) uzerinden
cagiriyor. Olcum pencereyi 640x480'den 320x240'a gercekten kucultuyor, `SizeChanged`
olayi elle atilmiyor. Iki ayri sey olculuyor: kapilarin gecildigi (sayac acilista kurulu
degil, kucultmeden sonra kurulu) ve tik gelince akisin yeniden kuruldugu (fabrika 1 -> 2).

**Plan degisimi** — `Plan_degisimi_hala_yeniden_kurar`. Ekranda calisan bir pencere
varken **ikinci bir plan** veriliyor (bit hizi 300 -> 900) ve imza zinciri gercekten
kosuyor:

```
PanelHost.cs:212  SetPlan -> ClipSignature(...) == TargetSignature ise erken don  (:232)
PanelHost.cs:235  ScheduleClip(_clipStart) -> _segmentDelay.Start()  (:570)
PanelHost.cs:150  SegmentDelayElapsed -> LoadClipAsync(_clipStart)
PanelHost.cs:577  LoadClipAsync -> _segments.RequestAsync -> if (_open) { RefreshRight(); Restart(); }  (:609)
```

Olcu iki yonlu: **ayni** plan yeniden verilince sira 1'de kaldi ve gecikme kurulmadi
(imza esitligi calisiyor); **farkli** plan verilince sira 2'ye cikti, gecikme kuruldu,
gecikme surunce kodlama 1 -> 2 ve fabrika 2 -> 3.

**Ilk parca hazir olunca** — `Ilk_parca_hazir_olunca_yeniden_kurar`. Ayri bir yol, ayri
bir olcu: ilk pencere kodlanip bitince `LoadClipAsync`in basari dalindaki `Restart`
kosuyor mu. Tur 2'de bu kol `Plan_degisimi_hala_yeniden_kurar` adiyla duruyordu ve
**adi olctugu seyi soylemiyordu** — `SetPlan` bir kez ve `Open()`tan once cagriliyordu,
imza zinciri hic kosmuyordu. Kol yeniden adlandirildi, plan degisimi ayrica olculdu.

### Zamanlayici tikleri hakkinda

`_settle` ve `_segmentDelay` birer `DispatcherTimer`. Olcum konagi
(`tests/VidShrink.Tests/AppHost.cs`) kendi is parcaciginda ileti dongusu calistirmiyor,
bu yuzden **hicbir `DispatcherTimer` bu olcumlerde atesenmiyor** (ayni sinirlama
`PanelHostTests` belgesinde de yazili). Tikin yaptigi is bu turda iki adlandirilmis
yonteme alindi — `PanelHost.SettleElapsed` ve `PanelHost.SegmentDelayElapsed` — ve olcum
onlari cagiriyor. Sayaclarin **kurulmus oldugu** ayrica olculuyor
(`ResizeSettling`, `ClipScheduled`), yoksa yalniz tiki surmek `OnResized`in ve `SetPlan`in
kapilari tumden kaldirilsa bile yesil kalirdi.

### Yedi kolun gecmedigi uretim satirlari

Onceki turda burada *"olculmeyen tek halka `DispatcherTimer`'in kendi OS tiki"* yaziyordu.
**Yanlisti.** Olculmeyen halka bundan buyuk ve asagidaki liste sondayla sayildi: her satira
`throw new InvalidOperationException("KAPSAM <satir>")` konup `dotnet build -c Release
--no-incremental` sonrasi yedi kol kosuldu; **hicbiri dusmediyse** o satir hicbir kol
tarafindan gecilmiyor demektir. Duzenek: `.calisma/T161/kapsam.py` + `.calisma/T161/sonda.sh`.

| sonda | satir | sonuc |
|---|---|---|
| `116` | `_settle.Tick += (_, _) => SettleElapsed();` | 7/7 yesil — **gecilmiyor** |
| `121` | `_segmentDelay.Tick += (_, _) => { _ = SegmentDelayElapsed(); };` | 7/7 yesil — **gecilmiyor** |
| `709` | `Drain()`in ilk satiri | Basarisiz 6 / Basarili 1 — **geciliyor** |
| `743` | `Drain()`in kare-konuldu kuyrugu (`_submitted++`den itibaren) | 7/7 yesil — **gecilmiyor** |
| `766` | `Follow()` govdesinin ilk satiri | 7/7 yesil — **gecilmiyor** |

Yani sunum dongusu (`Tick` -> `Drain`) bu olcum konaginda **donuyor**, ama `SessizKaynak`
hic kare vermedigi icin `Drain` kareyi alamadan geri donuyor. Gecilmeyen uretim satirlari,
tek tek:

- `PanelHost.cs:116` — `_settle` tikinin abonelik satiri (1 satir).
- `PanelHost.cs:121` — `_segmentDelay` tikinin abonelik satiri (1 satir).
- `PanelHost.cs:743-758` — `Drain`in kare panoya kondugunda kosan kuyrugu: `_submitted++`,
  `Controls.Position` yazimi (`:747-749`), `Follow` cagrisi (`:750`), bos-durum kaldirma
  bloku (`:752-757`), `SampleRate()` (`:758`).
- Yalniz o kuyruktan ulasilan bes yontemin **tamami**: `Follow` (`:766-780`),
  `AdvanceClip` (`:658-676`), `BeginHandover` (`:805-815`), `OpenStandbyAsync`
  (`:817-845`), `SwapToStandby` (`:859-880`). Bunlarin uretimdeki tek cagirani
  `Follow` zinciridir (`AdvanceClip` yalniz `:779`, `BeginHandover` yalniz `:773`,
  `SwapToStandby` yalniz `:778`, `OpenStandbyAsync` yalniz `:814`).

`PrepareAheadAsync`in **kendisi** olculuyor (K1 kolu onu dogrudan cagiriyor); olculmeyen,
onu oynatma sirasinda tetikleyen `:772` satiridir.

`Drain`in kuyrugunun hic kosmamasi, bu belgede geri cekilen iddiayi
(`Controls.Position` olculmedi) sondayla bir kez daha dogruluyor.

## K5 — Mutasyon izgarasi

Sekiz mutasyon. Her birinden **once** `dotnet build -c Release --no-incremental`, sonra
`dotnet test -c Release --no-build --filter "FullyQualifiedName~PlaybackResumeTests"`.
Duzenek: `.calisma/T161/mutasyon.py` + `.calisma/T161/kos.sh`.

| # | mutasyon | beklenen | sonuc (7 kol) |
|---|---|---|---|
| a | `ApplyPlayState`teki duzeltme geri alindi (`_segments.Cancel()` yeniden eklendi) | `Durdur_baslat_onden_hazirligi_bastan_baslatmaz` FAIL | Basarisiz 1 / Basarili 6 — dusen: `Durdur_baslat_onden_hazirligi_bastan_baslatmaz` |
| b | `SetFiles`ta dosya degisince `Restart()` kaldirildi | `Dosya_degisimi_hala_yeniden_kurar` FAIL | Basarisiz 1 / Basarili 6 — dusen: `Dosya_degisimi_hala_yeniden_kurar` |
| 1 | `OnResized`daki `_settle.Start()` kaldirildi | `Boyut_degisimi_hala_yeniden_kurar` FAIL | Basarisiz 1 / Basarili 6 — dusen: `Boyut_degisimi_hala_yeniden_kurar` |
| 2 | `SetPlan`in son satiri `ScheduleClip(_clipStart)` kaldirildi | `Plan_degisimi_hala_yeniden_kurar` FAIL | Basarisiz 1 / Basarili 6 — dusen: `Plan_degisimi_hala_yeniden_kurar` |
| 3 | `ApplyPlayState` devam ederken `Restart()` cagiriyor (bastan baslatma) | `Durdur_baslat_boruyu_yeniden_kurmaz` FAIL | Basarisiz 1 / Basarili 6 — dusen: `Durdur_baslat_boruyu_yeniden_kurmaz` |
| 4 | `PipeComparisonFrameSource.Play()` `_resume.Set()` yerine `SeekAsync(TimeSpan.Zero)` | `Duraklatilan_boru_kaldigi_karenin_ardindan_surer` FAIL | Basarisiz 1 / Basarili 6 — dusen: `Duraklatilan_boru_kaldigi_karenin_ardindan_surer` |
| 5 | `LoadClipAsync`in basari dalindaki `Restart()` kaldirildi | `Ilk_parca_hazir_olunca_yeniden_kurar` FAIL | Basarisiz **2** / Basarili 5 — dusen: `Ilk_parca_hazir_olunca_yeniden_kurar` **ve** `Plan_degisimi_hala_yeniden_kurar` |
| 6 | `SetPlan`deki imza esitlik kapisi (`PanelHost.cs:232-233`) kaldirildi — ayni plan da `ScheduleClip`e dusuyor | `Plan_degisimi_hala_yeniden_kurar`in **negatif** yarisi FAIL | Basarisiz 1 / Basarili 6 — dusen: `Plan_degisimi_hala_yeniden_kurar`, `Assert.Equal() Expected: 1, Actual: 2` (`:370`) |

6 numara **negatif kontrolu** hedefliyor: kapi kalkinca ayni plan da siraya giriyor,
`ayni plan verildi : sira 2, gecikme kurulu True` olarak kayda dusuyor ve
`Assert.Equal(oncekiSira, ayniPlanSira)` (`PlaybackResumeTests.cs:370`) dusuyor. 2 numara
yalniz pozitif yariyi dusurdugu icin bu iki mutasyon ayni seyi olcmuyor.

5 numarada iki kolun birden dusmesi beklenen: plan degisimi zinciri de son adiminda
ayni `Restart`tan geciyor ve fabrika cagrisini o sayiyor. Kollar ayni satiri paylasiyor
ama ayni sey**i** olcmuyor — 2 numarali mutasyon (imza zinciri kesildi) yalniz plan
kolunu dusuruyor, ilk-parca kolu yesil kaliyor.

Her mutasyondan sonra iki uretim dosyasi da yedekten geri alindi
(`.calisma/T161/PanelHost.cs.saglam`, `.calisma/T161/Pipe.cs.saglam`), `diff` ile
`SAME` dogrulandi, yeniden build edilip yedi kol da yesile dondu.

## K6 — Kol sayisi

```
dotnet test -c Release --no-build --filter "FullyQualifiedName~PlaybackResumeTests" --list-tests
```

```
    VidShrink.Tests.PlaybackResumeTests.Durdur_baslat_onden_hazirligi_bastan_baslatmaz
    VidShrink.Tests.PlaybackResumeTests.Dosya_degisimi_hala_yeniden_kurar
    VidShrink.Tests.PlaybackResumeTests.Ilk_parca_hazir_olunca_yeniden_kurar
    VidShrink.Tests.PlaybackResumeTests.Plan_degisimi_hala_yeniden_kurar
    VidShrink.Tests.PlaybackResumeTests.Boyut_degisimi_hala_yeniden_kurar
    VidShrink.Tests.PlaybackResumeTests.Durdur_baslat_boruyu_yeniden_kurmaz
    VidShrink.Tests.PlaybackResumeTests.Duraklatilan_boru_kaldigi_karenin_ardindan_surer
```

7 kol; sifir eslesen kol yok. Son kosum:

```
Başarılı!  - Başarısız: 0, Başarılı: 7, Atlanan: 0, Toplam: 7, Süre: 7 s
```

## Test altyapisinda bulunan 3 ayri kusur

`tests/VidShrink.Tests/PlaybackResumeTests.cs` bu sozlesmenin ilk turunda yazilmis ama
hic derlenip kosturulmamisti. Calisir hale getirirken bulunan (ve dosyanin kendisinde
duzeltilen) 3 test-altyapisi kusuru, olcumlerin dogru cikmasi icin gerekliydi:

1. `Yerlestir()` cıplak `panel.Measure/Arrange` cagiriyordu; StaticResource/tema cozumu
   gercek bir `Window` koku olmadan calismiyor. Panel artik bir `Window` icine konup
   oradan Measure/Arrange ediliyor (bkz. `ComparisonPanelTests.LayOutAt`).
2. Ikinci ve sonraki `Restart()` cagrisi `Teardown()` icinde `Task.Run(source.Dispose)`
   ile gercekten asenkron oluyor; `AppHost.Run` icinden cagrilirsa Avalonia'nin
   senkronizasyon baglami hic pompalanmadigi icin sonsuza dek asili kaliyor, konak
   disindan cagrilirsa panel dokunuslari yanlis is parcacigindan
   `InvalidOperationException: Call from invalid thread` ile sessizce dusuyordu.
   `LoadClipAsync` artik konak is parcacigindan baslatiliyor ve tamamlanmasi
   `Dispatcher.UIThread.RunJobs()` pompasiyla bekleniyor.
3. K1/K3 testi parcayi 4 sn'den baslatiyordu: pencere `[4,9]`, ardili istek 9 sn, ama
   `SegmentEncoder.Clamp` son baslangici `12 - WindowSeconds = 7`ye kirpiyor, boylece
   `PrepareAheadAsync`in tekillik kontrolu (`prepared.StartSeconds == ActiveClip.EndSeconds`)
   her cagrida sasiyordu. **Duzeltme sureyi buyutmek degil, baslangici indirmektir:** parca
   artik 2 sn'den basliyor, pencere `[2,7]`, ardil `Clamp(12, 7) = 7` — dosyanin icinde.
   `MediaInfo.DurationSeconds` gercek dosyanin suresine (12 sn) esit.

   Tur 3 bunun yerine 12 sn'lik dosyaya 30 sn sure vermisti; sayilan pencere dosyanin
   disina tasiyordu. O sunum kaldirildi: dosyayla celisen bir `MediaInfo` uzerinden
   olcum alinmiyor.
