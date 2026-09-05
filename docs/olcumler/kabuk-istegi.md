# Kabuk İsteği: Argüman Çözümü ve Tek Kuyruk

T170, tur 1. Ölçüm makinesi: Windows 11 Pro 10.0.26100, .NET 8, Release.
Kaynak: `src/VidShrink.Core/ShrinkRequest.cs`, testler: `tests/VidShrink.Tests/ShrinkRequestTests.cs`.

## K1 — Beş Argüman Biçimi, İkisi de Çözülüyor

Beş biçim, `ShrinkRequestResolver.Resolve` ile çözülüp hem hedef hem yol doğrulandı:

- `SimpleUnquotedPath` — tek token yol, hedef 500 MB.
- `QuotedPathWithSpaces` — tırnaklı boşluklu yol, hedef 500 MB.
- `UnquotedPathBrokenIntoTwoPieces` — tırnaksız, iki parçaya bölünmüş boşluklu yol, hedef 500 MB.
- `UnquotedPathBrokenIntoThreePieces` — tırnaksız, üç parçaya bölünmüş boşluklu yol
  (`tatil cekimi 2160p.mp4`), hedef 100 MB.
- `GigabyteTarget` — GB etiketli hedef, 2048 MB.

Ham koşum (`dotnet test -c Release --no-build --filter "FullyQualifiedName~ShrinkRequestTests"`):

```
[xUnit.net ...]
Toplam 1 test dosyası belirtilen desenle eşleşti.
Başarılı!  - Başarısız:     0, Başarılı:    15, Atlanan:     0, Toplam:    15, Süre: 465 ms
```

`--list-tests` çıktısındaki beş K1 kolu:

```
VidShrink.Tests.ShrinkRequestTests+ResolverTests.K1_five_argument_formats_resolve_both_target_and_path(shape: "SimpleUnquotedPath")
VidShrink.Tests.ShrinkRequestTests+ResolverTests.K1_five_argument_formats_resolve_both_target_and_path(shape: "QuotedPathWithSpaces")
VidShrink.Tests.ShrinkRequestTests+ResolverTests.K1_five_argument_formats_resolve_both_target_and_path(shape: "UnquotedPathBrokenIntoTwoPieces")
VidShrink.Tests.ShrinkRequestTests+ResolverTests.K1_five_argument_formats_resolve_both_target_and_path(shape: "UnquotedPathBrokenIntoThreePieces")
VidShrink.Tests.ShrinkRequestTests+ResolverTests.K1_five_argument_formats_resolve_both_target_and_path(shape: "GigabyteTarget")
```

## K2 — Altı Geçersiz Biçim (en az dört istenmişti), Hiçbiri Sessiz Geçmiyor

- `MissingTarget` (`--kucult` tek başına) → `ShrinkArgumentProblem.NoTarget`.
- `TargetNotANumber` (`--kucult abuk <yol>`) → `TargetNotANumber`.
- `NegativeTarget` (`--kucult -5 <yol>`) → `TargetNotPositive`.
- `ZeroTarget` (`--kucult 0 <yol>`) → `TargetNotPositive`.
- `TargetOutsideQuickList` (`--kucult 777 <yol>`, hızlı hedef listesi `100,250,500,1024,2048`) → `TargetNotInQuickList`.
- `MissingPath` (`--kucult 500`, yol yok) → `NoPath`.

`--list-tests` çıktısındaki altı K2 kolu:

```
VidShrink.Tests.ShrinkRequestTests+ResolverTests.K2_invalid_targets_are_rejected_with_a_named_problem(shape: "MissingTarget", expected: NoTarget)
VidShrink.Tests.ShrinkRequestTests+ResolverTests.K2_invalid_targets_are_rejected_with_a_named_problem(shape: "TargetNotANumber", expected: TargetNotANumber)
VidShrink.Tests.ShrinkRequestTests+ResolverTests.K2_invalid_targets_are_rejected_with_a_named_problem(shape: "NegativeTarget", expected: TargetNotPositive)
VidShrink.Tests.ShrinkRequestTests+ResolverTests.K2_invalid_targets_are_rejected_with_a_named_problem(shape: "ZeroTarget", expected: TargetNotPositive)
VidShrink.Tests.ShrinkRequestTests+ResolverTests.K2_invalid_targets_are_rejected_with_a_named_problem(shape: "TargetOutsideQuickList", expected: TargetNotInQuickList)
VidShrink.Tests.ShrinkRequestTests+ResolverTests.K2_invalid_targets_are_rejected_with_a_named_problem(shape: "MissingPath", expected: NoPath)
```

Mutasyon (a) bu denetimi kaldırdığında `TargetOutsideQuickList` kolunun düştüğü K6'da gösteriliyor.

## K3 — `ResolveStartupPath` Değişmedi

Dosya `ShellIntegration.cs`'e hiç yazılmadı (yalnız okundu); önce/sonra karşılaştırması
`ShrinkRequestTests.K3_before_after_resolve_startup_path_is_unchanged_by_this_contract`
testinde dört biçimle yapıldı — bu sözleşmenin `ShrinkRequestResolver.Resolve` çağrısından
**önce** ve **sonra** aynı dört argüman `ShellIntegration.ResolveStartupPath`'e verildi:

- Tek yol (`kayit.mp4`) → önce/sonra aynı yol.
- Tırnaklı boşluklu yol (`baska.mp4`, tırnaklı) → önce/sonra aynı yol.
- Tırnaksız üç parçaya bölünmüş yol (`tatil cekimi 2160p.mp4`) → önce/sonra aynı yol.
- Var olmayan dosya → önce/sonra `null`.

Ayrıca mevcut `ShellIntegrationTests` kolu (bu sözleşmenin `verify:` listesinde değil,
ek kanıt olarak ayrıca koşuldu) hâlâ dokuz testin dokuzuyla da yeşil:

```
dotnet test -c Release --no-build --filter "FullyQualifiedName~ShellIntegrationTests"
Toplam 1 test dosyası belirtilen desenle eşleşti.
Başarılı!  - Başarısız:     0, Başarılı:     9, Atlanan:     0, Toplam:     9, Süre: 2 s
```

## K4 — Tek Kuyruk: Adlandırılmış Mutex + Adlandırılmış Boru

**Seçilen yol.** `ShrinkRequestQueue`: ilk açılan örnek, adı kanal bazlı bir `Mutex`'i
(`initiallyOwned: true`, dönüşte `createdNew`) ele geçirir ve **sahip** olur; sahip aynı
adı taşıyan bir `NamedPipeServerStream` açıp tek bir tüketici döngüsüyle (`ProcessLoop`,
`BlockingCollection<T>.GetConsumingEnumerable`) istekleri **sırayla** işler. Sahip
olmayan her örnek aynı boruya istemci olarak bağlanır (`NamedPipeClientStream`), isteğini
yazar ve çıkar.

**Neden bu yol.** Alt süreç yok (saf C#), işletim sistemi düzeyinde tek örnek deseni;
kanal adı çağırana bırakıldı ve testte her koşum için `Guid.NewGuid()` kullanıldı — T169'un
ölü PID taramasındaki hatadan (aynı anda iki koşum aynı PID'i seçebilmesi) kaçınmak için
kimlik üretiminde PID **kullanılmadı**.

**Hangi durumda bozulur.** Mutex'i tutan sahip süreç çökerse (görev yöneticisinden
sonlandırılırsa) Mutex "terk edilmiş" sayılır ve bir sonraki örnek sahipliği devralır,
ama o ana kadar boruya bağlanmayı deneyen istemciler zaman aşımına uğrar (varsayılan 5 sn)
ve `Submit` `false` döner — istek **kaybolur**. Bu sınıfın çökme kurtarma mekanizması yok;
sahibin ayakta kaldığı varsayılıyor.

**Ölçü — eş zamanlılık yok.** Beş "örnek" (`ShrinkRequestQueue`, aynı kanal adı) eş
zamanlı olarak `Task.Run` ile başlatıldı; ilki sahip oldu, diğer dördü boruyla teslim etti.
İşleyicide art arda gelen çağrılar arasında üst üste binme sayacı tutuldu (`maxConcurrent`):

```
K4_second_instance_hands_off_and_never_runs_concurrently_with_the_owner [FAIL] (mutasyon b altında)
Assert.Equal() Failure: Values differ
Expected: 1
Actual:   5
```

Bu, mutasyon (b) altındaki (eş zamanlı sürece çevrilmiş) çıktı — orijinal kodda aynı ölçü
`maxConcurrent == 1` ile **geçiyor** (bkz. aşağıdaki K6 ızgarası, satır "önce"). Beş çağrının
hiçbiri diğeriyle üst üste binmeden, sırayla işlendi.

## K5 — Beş Dosya, Kayıpsız, Belirli Sırada

Beş farklı dosya adı (`dosya-0.mp4` … `dosya-4.mp4`) beş ayrı `ShrinkRequestQueue`
örneğinden eş zamanlı gönderildi (biri sahip, dördü boru istemcisi). İşleyicinin gördüğü
kümenin tam beş öğeden oluştuğu ve hiçbirinin yinelenmediği doğrulandı:

```
K5_five_files_are_all_processed_exactly_once_no_loss [PASS] (orijinal kod)
Başarılı!  - Başarısız:     0, Başarılı:    15, Toplam:    15
```

Sıra, kuyruğun tek `BlockingCollection<T>` yapısından gelen doğal FIFO'dur: sahip kendi
isteğini doğrudan ekler, boru istemcilerinin istekleri `ListenLoop`'un tek bağlantı kabul
eden döngüsünden geldikleri sırayla eklenir — hiçbir yeniden sıralama adımı yok.

## K6 — Üç Mutasyon Izgarası

Her mutasyondan önce `dotnet build -c Release --no-incremental` çalıştırıldı (`--no-build`
kullanılmadı); ardından `dotnet test -c Release --no-build --filter "FullyQualifiedName~ShrinkRequestTests"`.

| Mutasyon | Beklenen ölen ölçü | Sonuç |
|---|---|---|
| (a) `ShrinkRequestResolver.Resolve` içindeki `quickTargets.Contains(target)` denetimi silindi | K2 (`TargetOutsideQuickList` kolu) | **Öldü** |
| (b) `ProcessLoop`'taki `handler(request)` çağrısı `Task.Run(() => handler(request), token)` ile eş zamanlı hale getirildi | K4 (`maxConcurrent`) | **Öldü** |
| (c) `ListenLoop`'a bir bağlantı kabul ettikten sonra `return` eklendi (kuyruk ilk dosyadan sonra dosya düşürüyor) | K5 (kayıpsızlık) | **Öldü** |

Ham çıktılar:

```
(a) — 15 testten 1'i başarısız
[xUnit.net] VidShrink.Tests.ShrinkRequestTests+ResolverTests.K2_invalid_targets_are_rejected_with_a_named_problem(shape: "TargetOutsideQuickList", expected: TargetNotInQuickList) [FAIL]
  Hata İletisi: TargetOutsideQuickList: sessizce kabul edildi.
Başarısız! - Başarısız: 1, Başarılı: 14, Toplam: 15

(b) — 15 testten 1'i başarısız
[xUnit.net] VidShrink.Tests.ShrinkRequestTests+QueueTests.K4_second_instance_hands_off_and_never_runs_concurrently_with_the_owner [FAIL]
  Assert.Equal() Failure: Values differ / Expected: 1 / Actual: 5
Başarısız! - Başarısız: 1, Başarılı: 14, Toplam: 15

(c) — 15 testten 2'si başarısız
[xUnit.net] VidShrink.Tests.ShrinkRequestTests+QueueTests.K5_five_files_are_all_processed_exactly_once_no_loss [FAIL]
  Assert.All() Failure: 4 out of 5 items in the collection did not pass. (Istek sahibe ulasamadi, dosya kayboldu.)
[xUnit.net] VidShrink.Tests.ShrinkRequestTests+QueueTests.K4_second_instance_hands_off_and_never_runs_concurrently_with_the_owner [FAIL]
  Assert.All() Failure: 4 out of 5 items in the collection did not pass. (Istek sahibe ulasamadi.)
Başarısız! - Başarısız: 2, Başarılı: 13, Toplam: 15
```

Her mutasyondan sonra orijinal dosya geri yüklendi ve doğrulandı (`diff` boş çıktı verdi);
son derleme ve koşum tekrar 15/15 yeşil:

```
dotnet build -c Release --no-incremental → Oluşturma başarılı oldu. 0 Uyarı, 0 Hata.
dotnet test -c Release --no-build --filter "FullyQualifiedName~ShrinkRequestTests"
Başarılı!  - Başarısız:     0, Başarılı:    15, Atlanan:     0, Toplam:    15, Süre: 518 ms
```

## K7 — Kol Sayısı

```
dotnet test -c Release --filter "FullyQualifiedName~ShrinkRequestTests" --list-tests
Şu Testler kullanılabilir: (15 satır)
```

On beş kol bulundu, sıfır bulan kol yok (tam liste K1/K2 bölümlerinde yukarıda).
