# T161 — Önizleme durdur/baslat yapinca bastan basliyor

Belirti: oynatirken durdur/baslat yapilinca, ayar degismedigi halde is bastan basliyor.

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

Kod yolu: `src/VidShrink.App/Playback/PanelHost.cs:927` `ApplyPlayState()`.

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

## K3 — Duzeltme

`ApplyPlayState()`, duraklatmada artik `_segments.Cancel()` cagirmiyor; onden hazirlik
arka planda bitmeye birakiliyor, boylece Baslat'a basildiginda pencere zaten hazir olur.

Duzeltme sonrasi ayni izgara (`.calisma/T161/K3-K4-duzeltme-sonrasi.txt`):

```
T161 K1 durdur/baslat izgarasi -- onden hazirligin bastan baslama sayisi
  kontrol (durdur/baslat YOK): 1
  durdur/baslat kosumu       : 1
  fark (fazladan cagri)      : 0
```

## K4 — Mesru yeniden kurulumlar bozulmadi

Ayni ham ciktida (`.calisma/T161/K3-K4-duzeltme-sonrasi.txt`) iki yol da olculdu:

```
T161 K4 dosya degisimi -- fabrika cagrisi: acilista 1, dosya degisince 2
T161 K4 plan degisimi -- fabrika cagrisi: parca oncesi 1, parca sonrasi 2
```

- Dosya degisimi: `PanelHost.SetFiles` (`PanelHost.cs:299`) — kaynak fabrikasi acilista
  1, yeni dosya verilince 2 kez cagrildi (`Restart` kosun).
- Plan degisimi (ilk parca hazir olunca): `PanelHost.LoadClipAsync` (`PanelHost.cs:581`)
  — fabrika parca hazir olmadan once 1, hazir olunca 2 kez cagrildi.
- Panel yeniden boyutlanma yolu (`OnResized` -> `Restart`, `PanelHost.cs:385`) koda
  dokunulmadi; K3'un duzeltmesi yalniz `ApplyPlayState` icinde.

## K5 — Mutasyon izgarasi

Her mutasyondan once `dotnet build -c Release --no-incremental` calistirildi, sonra
`dotnet test -c Release --filter "FullyQualifiedName~PlaybackResumeTests"` kosturuldu.

| mutasyon | beklenen | sonuc |
|---|---|---|
| (a) `ApplyPlayState`teki duzeltme geri alindi (`_segments.Cancel()` yeniden eklendi) | `Durdur_baslat_onden_hazirligi_bastan_baslatmaz` FAIL | FAIL — `Assert.Equal() Expected:1 Actual:2` |
| (b) `SetFiles`ta dosya degisince `Restart()` cagrisi kaldirildi | yalniz `Dosya_degisimi_hala_yeniden_kurar` FAIL, digerleri yesil | FAIL yalniz o test (`dosya degisince Restart kosmadi`); `Plan_degisimi_hala_yeniden_kurar` ve `Durdur_baslat_onden_hazirligi_bastan_baslatmaz` yesil |

Her iki mutasyondan sonra kod geri alindi (`.calisma/T161/PanelHost.cs.fixed` ile
karsilastirilip `SAME` dogrulandi), yeniden build edilip uc test de yesile dondu.

## K6 — Kol sayisi

```
dotnet test -c Release --filter "FullyQualifiedName~PlaybackResumeTests" --list-tests
```

3 test buluyor: `Durdur_baslat_onden_hazirligi_bastan_baslatmaz`,
`Dosya_degisimi_hala_yeniden_kurar`, `Plan_degisimi_hala_yeniden_kurar`. Sifir eslesen
kol yok.

## Test altyapisinda bulunan 3 ayri kusur

`tests/VidShrink.Tests/PlaybackResumeTests.cs` bu sozlesmenin ilk turunda yazilmis ama
hic derlenip kosturulmamisti. Calisir hale getirirken bulunan (ve dosyanin kendisinde
duzeltilen) 3 test-altyapisi kusuru, K1-K4 olcumlerinin dogru cikmasi icin gerekliydi:

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
3. K1/K3 testi 12 saniyelik gercek dosyaya varsayilan 12 sn `MediaInfo.DurationSeconds`
   veriyordu; `SegmentEncoder.Clamp` pencereyi kaynagin son `WindowSeconds`ina (5 sn)
   kadar geri cekince onden hazirligin kendi tekillik kontrolu her cagrida bozuluyordu.
   Test artik 30 sn sure veriyor, kirpilma devre disi.
