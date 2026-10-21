# Tek Tık Gecikmesi: Eski Ve Yeni Sol Tık Yolu

2026-10-21, dal `worktree-agent-later-95`. Soru: Later 95'in çift tık ve basılı tutma
eklemesi, oynatıcı yüzeyinde tek tıkla duraklat/oynat komutunun motora gidişini yavaşlattı mı.

## Düzenek

`tests/VidShrink.Tests/TekTikGecikmesiTests.cs`, sahte saat yok, gerçek zaman (`Stopwatch`).

- Gösterilen 640x360 pencerede `PlayerView`, sahte motor `ZamanliMotor` (`Play`/`Pause` çağrısının
  anını `Stopwatch.GetTimestamp()` ile yazar). Açılışta motora giden komut ölçüme girmez.
- Her tık: Win32 arka ucunun ham girdisiyle `Move` + `LeftButtonDown`, kuyruk boşaltılır;
  `t0` alınır, ham `LeftButtonUp` verilir. Süre = motor komutunun anı − `t0`. Yani girdi
  yönlendirmesi, `OnFarePointerReleased`, `FareRelease`, `Apply`, `TogglePlay` dahil.
- Tıklar yüzey ortasının 40 dip solu ve sağı arasında gidip gelir; böylece yeni kodda ikinci
  tık çift tık sayılmaz (4 dip sınırı), iki kodda da her tık tek tıktır.
- Koşum başına 30 ısınma + 240 ölçülen tık; eski ve yeni dönüşümlü üçer koşum
  (eski1, yeni1, eski2, yeni2, eski3, yeni3), toplam 720'şer tık. Release derlemesi.
- Eski kod: `origin/main` (`d4084bf8c`) ayrı worktree'de, aynı test dosyası kopyalanarak.
- Her tıkta motora tam bir komut gitti (kayıp 0), komutlar oynat/duraklat sırasıyla dönüşümlü.

## Sonuç

| Kod | Tık | Ortanca (ms) | p95 (ms) | p99 (ms) | En çok (ms) |
|---|---|---|---|---|---|
| Eski (`origin/main`) | 720 | 0,0103 | 0,0357 | 0,0520 | 2,8008 |
| Yeni (Later 95) | 720 | 0,0158 | 0,0396 | 0,0497 | 0,3843 |

Fark: ortancada +0,0055 ms, p95'te +0,0039 ms. 1 ms eşiğinin yaklaşık iki yüzde biri;
kapatılacak bir fark yok. Koşumlar arası oynama farkın kendisi kadar: eski koşumların ortancası
0,0086–0,0153 ms, yeni koşumlarınki 0,0121–0,0183 ms arasında.

| Koşum | Ortanca (ms) | p95 (ms) |
|---|---|---|
| eski1 | 0,0153 | 0,0488 |
| yeni1 | 0,0121 | 0,0334 |
| eski2 | 0,0086 | 0,0153 |
| yeni2 | 0,0178 | 0,0407 |
| eski3 | 0,0088 | 0,0171 |
| yeni3 | 0,0183 | 0,0414 |

## Tek Tıkta Bekleme Yolu Yok

`src/VidShrink.App/Playback/PlayerView.Fare.cs`:

- 297–314: `OnFarePointerReleased` bırakışta doğrudan `FareRelease()` çağırır.
- 160–168: `FareRelease` tutma saatini durdurur (162), `Sol.Birak` sonucunu alır (163) ve
  `Tik` ise aynı çağrıda `Apply(Keymap.ForPress(PlayerButton.Left))` yapar (167–168).
  Erteleme, `Dispatcher.Post` ya da zamanlayıcı yok.
- 74: tek zamanlayıcı (`TutmaSaatiKur`) basışta kurulur ve yalnız basılı tutmayı açar;
  bırakış onu bekletmez, durdurur.

`src/VidShrink.App/Playback/SolTikHareketi.cs` 73–102: `Birak` saf hesaptır; tek tık 102'de
`Tik` döner. Yeni yolun eski yola eklediği iş bu hesap ve bir `DispatcherTimer.Stop()`;
ölçülen +0,005 ms bu.

## Çift Tıkta Bir An Durma

Görülür. İlk tık bırakışta motora `Pause` gönderir; ikinci tık bırakışta tam ekranla birlikte
`Play` gönderir (`PlayerView.Fare.cs` 171). Arada görüntü donar ve ses kesilir; süresi iki
bırakış arasındaki süre kadardır, yani kullanıcının çift tık hızı (üst sınır platformun çift
tık süresi, Windows varsayılanı 500 ms). Duraklat göstergesi de (`FlashPause`) ilk tıkta
duraklat, ikincide oynat olarak yanıp söner. Bu donmayı kaldırmanın tek yolu tek tıkı çift tık
süresi kadar bekletmek; sahibin kararı tek tıka gecikme eklememek. Donma süresi bu koşumda
ölçülmedi, yukarıdaki kod yolundan çıkarımdır.

## Tekrar

```
$env:VIDSHRINK_TIK_ETIKETI='yeni1'
dotnet test tests/VidShrink.Tests/VidShrink.Tests.csproj -c Release --filter "FullyQualifiedName~TekTikGecikmesiTests"
```

Etiket verilince ham süreler `.calisma/tek-tik-gecikmesi/<etiket>.txt`'ye kalır; etiketsiz
koşumda (CI) test klasörü siler.
