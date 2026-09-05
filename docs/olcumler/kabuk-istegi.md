# Kabuk İsteği: Argüman Çözümü ve Tek Kuyruk

T170, tur 2. Ölçüm makinesi: Windows 11 Pro 10.0.26100, .NET 8, Release.
Kaynak: `src/VidShrink.Core/ShrinkRequest.cs`, testler: `tests/VidShrink.Tests/ShrinkRequestTests.cs`.

Tur 1'in bağımsız denetimi dört bulguyla döndü; dördü de bu turda kapatıldı. Kapatılan
kusurlar ve nasıl kapatıldıkları L1–L4 bölümlerinde, ölçüleri öldüren mutasyonlar K6
ızgarasındadır. Kol sayısı 15'ten **19**'a çıktı.

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
Toplam 1 test dosyası belirtilen desenle eşleşti.
Başarılı!  - Başarısız:     0, Başarılı:    19, Atlanan:     0, Toplam:    19, Süre: 941 ms
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

`ShellIntegration.cs` bu sözleşmenin diff'inde **hiç yok**; dosyaya yazılmadı, yalnız okundu.
Tur 1'deki "önce/sonra" karşılaştırması silindi (bulgu L4): `ResolveStartupPath` durumu
olmayan saf statik bir fonksiyon, aynı girdiyle iki kez çağırmak yapısal olarak **hep**
geçen bir ölçüydü — kanıt değil gürültüydü.

Ağırlığı gerçekten taşıyan iki kanıt kaldı.

**(1) Beş biçimin somut çözümü** — `K3_resolve_startup_path_still_resolves_every_known_shape`:

| Girdi | Beklenen | Sonuç |
|---|---|---|
| Tek yol (`kayit.mp4`) | yolun kendisi | geçti |
| Tırnaklı yol (`"…/baska.mp4"`) | tırnaksız yol | geçti |
| Tırnaksız üç parçaya bölünmüş (`tatil cekimi 2160p.mp4`) | birleşmiş yol | geçti |
| Var olmayan dosya | `null` | geçti |
| Boş argüman dizisi | `null` | geçti |

Bu beş satırın her biri `ResolveStartupPath`'in davranışını değiştiren bir mutasyonla düşer
(ör. tırnak soyma adımının kaldırılması ikinci satırı, en uzun birleşim aramasının
kaldırılması üçüncüyü öldürür).

**(2) Mevcut `ShellIntegrationTests` kolu** (bu sözleşmenin `verify:` listesinde değil, ek
kanıt olarak ayrıca koşuldu) hâlâ dokuz testin dokuzuyla da yeşil:

```
dotnet test -c Release --no-build --filter "FullyQualifiedName~ShellIntegrationTests"
Toplam 1 test dosyası belirtilen desenle eşleşti.
Başarılı!  - Başarısız:     0, Başarılı:     9, Atlanan:     0, Toplam:     9, Süre: 1 s
```

## K4 — Tek Kuyruk: Adlandırılmış Mutex + Adlandırılmış Boru

**Seçilen yol.** `ShrinkRequestQueue`: ilk açılan örnek, adı kanal bazlı bir `Mutex`'i
(`initiallyOwned: true`, dönüşte `createdNew`) ele geçirir ve **sahip** olur; sahip aynı
adı taşıyan bir `NamedPipeServerStream` açıp tek bir tüketici döngüsüyle (`ProcessLoop`,
`BlockingCollection<T>.GetConsumingEnumerable`) istekleri **sırayla** işler. Sahip
olmayan her örnek aynı boruya istemci olarak bağlanır (`NamedPipeClientStream`), isteğini
yazar, **onayı bekler** ve çıkar.

**Neden bu yol.** Alt süreç yok (saf C#), işletim sistemi düzeyinde tek örnek deseni;
kanal adı çağırana bırakıldı ve testte her koşum için `Guid.NewGuid()` kullanıldı — T169'un
ölü PID taramasındaki hatadan (aynı anda iki koşum aynı PID'i seçebilmesi) kaçınmak için
kimlik üretiminde PID **kullanılmadı**.

**Hangi durumda bozulur.** Üç bozulma noktası var; tur 1 yalnız birincisini yazmıştı.

1. **Sahip süreç çöker.** Boru sunucusu ortadan kalkar; bağlanmayı deneyen istemciler
   zaman aşımına uğrar (varsayılan 5 sn) ve `Submit` `false` döner — istek işlenmez ama
   **kayıp sessiz değildir**, çağırana bildirilir. Ölçü:
   `L2_submit_reports_failure_when_the_owner_is_not_listening`.

   Mutex mekanizması hakkında tur 1'de yanlış yazılmış cümle **düzeltildi**: bu sınıf
   Mutex üzerinde hiçbir zaman `WaitOne` çağırmıyor; sahiplik yalnız kurucudaki
   `createdNew` bayrağıyla belirleniyor. Bu yüzden `AbandonedMutexException` bu kod
   yolunda **hiç doğmaz**. Sahip süreç ölünce son tanıtıcı kapanır, adlandırılmış nesne
   yok olur ve **bundan sonra kurulan** bir örnek `createdNew == true` alıp sahip olur.
   Zaten kurulmuş ve `createdNew == false` almış örnekler sahipliği **devralmaz** — bir
   daha bakmazlar, ömürleri boyunca istemci kalırlar.

2. **Bozuk ya da yarım boru mesajı.** Sahip mesajı ayrıştıramazsa artık **sessizce
   düşürmüyor**: boruya `HATA` yazıyor ve istemci `Submit`'ten `false` alıyor (bulgu L2).

3. **Sahip kapanmak üzereyken gelen istek.** `Dispose` sırasında `CompleteAdding` çağrılır;
   bu andan sonra gelen mesaj kuyruğa alınamaz ve yine `HATA` ile yanıtlanır
   (`Accept` `false` döner). `Submit` `true` dönmez.

**Ölçü — eş zamanlılık yok.** Beş "örnek" (`ShrinkRequestQueue`, aynı kanal adı) eş
zamanlı olarak `Task.Run` ile başlatıldı; ilki sahip oldu, diğer dördü boruyla teslim etti.
İşleyicide art arda gelen çağrılar arasında üst üste binme sayacı tutuldu (`maxConcurrent`);
orijinal kodda `maxConcurrent == 1`. Mutasyon (b) altındaki ham çıktı:

```
K4_second_instance_hands_off_and_never_runs_concurrently_with_the_owner [FAIL]
Assert.Equal() Failure: Values differ
Expected: 1
Actual:   5
```

## K5 — Beş Dosya, Kayıpsız; Sıra Yalnız Tek Gönderici İçin Belirli

Tur 1 bu bölümde **veremediği bir şeyi iddia ediyordu** (bulgu L3): başlık "belirli sırada"
diyordu, ölçü ise iki tarafı da `OrderBy` ile sıralayıp sıra bilgisini siliyordu. İddia
kısmen **geri çekildi**, kalan kısmı **gerçekten ölçülür hale getirildi**.

**Geri çekilen.** Beş ayrı örnek eş zamanlı gönderirken (`Task.Run` ile yarışan beş
istemci) boruya varış sırası **belirsizdir** ve kod bunu belirli yapamaz — işletim sistemi
hangi istemcinin bağlantısını önce kabul edeceğini garanti etmez. Bu senaryoda ölçülen tek
şey **kayıpsızlıktır**: beşi de işlenir, hiçbiri yinelenmez, hiçbiri düşmez.

```
K5_five_files_are_all_processed_exactly_once_no_loss
  Assert.Equal(5, seen.Length)                              — adet
  Assert.Equal(expected.OrderBy(…), seen.OrderBy(…))        — küme eşitliği
  Assert.Equal(seen.Length, seen.Distinct().Count())        — tekillik
```

**Ölçülür hale getirilen.** Sıranın belirli olduğu tanım **tek gönderici**dir: bir istemci
beş isteği ardışık gönderdiğinde (her biri onay alarak, bkz. L2), kuyruğun tek
`BlockingCollection<T>` yapısı FIFO olduğu için işleyici de aynı sırada görür. Ölçü
`K5_single_sender_requests_are_handled_in_submission_order`: işleyici ilk isteği aldıktan
sonra bir kapıda bekletilir, böylece kalan dördü kuyrukta birikir; beş gönderim de
onaylandıktan sonra kapı açılır ve çıkan dizi tam beklenen sırayla karşılaştırılır.

Bu kol boştan uzak: mutasyon (f) kuyruğun arka deposunu `ConcurrentStack`'e çevirdiğinde
düşüyor (K6 ızgarası).

## L1 — Tek Tüketici Garantisi (yeni)

**Kusur.** `StartOwning` yeniden giriş korumasızdı. İkinci kez çağrılınca `_cancel`,
`_listenTask` ve `_processTask` üzerine yazılıyor, eski `ProcessLoop` iptal edilmeden
**aynı** `BlockingCollection` üzerinde çalışmaya devam ediyordu. Sonuç: aynı anda iki
işleyici, yani **iki kodlama**. Sözleşmenin bütün gerekçesi buydu (DEVIR.md §6:
"ffmpeg sıralı koşar; iki eşzamanlı kodlama hem süreyi hem kaliteyi bozar"). Mutex süreci
**dışarıdan** tek örnekle sınırlıyordu, ama süreç **içinde** tek tüketici garantisi yoktu.

**Düzeltme.** `Interlocked.Exchange(ref _started, 1)` muhafızı; ikinci çağrı
`InvalidOperationException` atıyor ve ikinci tüketici hiç açılmıyor.

**Ölçü.** `L1_second_StartOwning_is_refused_so_a_single_consumer_remains`: ilk işleyici
kurulur (çağrı başına 100 ms), ikinci `StartOwning` denenir, sonra beş istek gönderilir.

```
Assert.Throws<InvalidOperationException>(() => owner.StartOwning(ikinci))
Assert.Empty(ikinciIsleyicininGordukleri)
Assert.Equal(5, ilkIsleyicininGordukleri.Count)
```

Mutasyon (d) altındaki ham çıktı:

```
L1_second_StartOwning_is_refused_so_a_single_consumer_remains [FAIL]
Assert.Throws() Failure: No exception was thrown
```

## L2 — Onaylı Teslim (yeni)

**Kusur.** `Submit` boruya yazıp **onay almadan** `true` dönüyordu. Sahip tarafta bozuk
mesaj için `else` yoktu; `line is null` dalı da sessizdi. Her iki durumda da çağırana
"başarılı" denmiş oluyordu — karşılanmamış isteği karşılanmış gibi geçirmek.

**Düzeltme.** Boru `PipeDirection.InOut`'a çevrildi. Sahip mesajı kuyruğa alabildiyse
`TAMAM`, alamadıysa (ayrıştırılamayan mesaj, ya da kapanmakta olan kuyruk) `HATA` yazıyor.
İstemci yanıtı okuyup yalnız `TAMAM` gördüğünde `true` dönüyor; yanıt gelmezse, bağlantı
kopmuşsa ya da zaman aşarsa `false`.

**Ölçü.** `L2_malformed_pipe_message_is_answered_with_a_refusal_not_a_silent_drop`, boruya
ham istemci olarak bağlanıp üç mesaj gönderiyor:

| Ham mesaj | Beklenen yanıt | Sonuç |
|---|---|---|
| `sekmesiz-bozuk-mesaj` | `HATA` | geçti |
| `sayidegil\tc:/yol/a.mp4` | `HATA` | geçti |
| `500\tc:/yol/gecerli.mp4` | `TAMAM` | geçti |

Ardından işleyicinin gördüğü tek öğenin `c:/yol/gecerli.mp4` olduğu doğrulanıyor: iki bozuk
mesaj kuyruğa **girmedi** ve gönderene de girmiş gibi gösterilmedi.

İkinci kol `L2_submit_reports_failure_when_the_owner_is_not_listening`: sahip var ama
`StartOwning` çağrılmamış, yani boru sunucusu yok. 300 ms zaman aşımıyla `Submit` `false`
dönüyor.

Mutasyon (e) — onay kaldırılıp tur 1'in sessiz düşürmesi geri getirildiğinde ham çıktı:

```
L2_malformed_pipe_message_is_answered_with_a_refusal_not_a_silent_drop [FAIL]
System.IO.IOException : Pipe is broken.
   at System.IO.Pipes.PipeStream.CheckWriteOperations()
```

Yani sahip hiçbir yanıt yazmadan bağlantıyı kapatıyor; istemcinin "karşılandı mı?"
sorusuna verecek cevabı kalmıyor.

## L3 — Sıra İddiası

Kapatma yolu **(b): iddiayı geri çekmek**, üstüne (a)'nın uygulanabilir kısmını ölçmek.
Gerekçe ve ölçü K5 bölümünde. Kısaca: beş eş zamanlı gönderici için "belirli sıra" kodun
veremeyeceği bir iddiaydı, başlıktan ve metinden çıkarıldı; sıranın gerçekten belirli
olduğu tanım (tek gönderici, FIFO) ayrı bir kolla ölçüldü.

## L4 — Boş Ölçü

Tur 1'in `K3_before_after_…` kolundaki `Assert.Equal(before, after)` satırı **silindi**.
Durumu olmayan saf statik bir fonksiyona aynı girdiyle yapılan iki çağrı yapısal olarak hep
eşit çıkar; bu satır hiçbir mutasyonla düşmezdi. Belgenin K3 bölümü artık ağırlığı gerçekten
taşıyan kanıta (beş somut çözüm + `ShellIntegrationTests` 9/9 + boş diff) yönlendiriyor.

## K6 — Altı Mutasyon Izgarası

Her mutasyondan önce `dotnet build -c Release --no-incremental` çalıştırıldı (`--no-build`
kullanılmadı); ardından `dotnet test -c Release --no-build --filter "FullyQualifiedName~ShrinkRequestTests"`.

| Mutasyon | Beklenen ölen ölçü | Sonuç |
|---|---|---|
| (a) `Resolve` içindeki `quickTargets.Contains(target)` denetimi silindi | K2 (`TargetOutsideQuickList` kolu) | **Öldü** |
| (b) `ProcessLoop`'taki `handler(request)` çağrısı `Task.Run(() => handler(request), token)` ile eş zamanlı hale getirildi | K4 (`maxConcurrent`) | **Öldü** |
| (c) `ListenLoop`'a bir bağlantı kabul ettikten sonra `return` eklendi | K5 (kayıpsızlık) | **Öldü** |
| (d) `StartOwning`'deki `Interlocked.Exchange(ref _started, 1)` muhafızı silindi | L1 (tek tüketici) | **Öldü** |
| (e) Sahibin `TAMAM`/`HATA` yanıtı kaldırıldı, `Submit` koşulsuz `true` döndürüldü | L2 (onaylı teslim) | **Öldü** |
| (f) `_pending` arka deposu `new BlockingCollection<…>(new ConcurrentStack<…>())` yapıldı | K5 (tek gönderici sırası) | **Öldü** |

Ham çıktılar:

```
(a) — 19 testten 1'i başarısız
[xUnit.net] …ResolverTests.K2_invalid_targets_are_rejected_with_a_named_problem(shape: "TargetOutsideQuickList", expected: TargetNotInQuickList) [FAIL]
Başarısız! - Başarısız: 1, Başarılı: 18, Toplam: 19

(b) — 19 testten 1'i başarısız
[xUnit.net] …QueueTests.K4_second_instance_hands_off_and_never_runs_concurrently_with_the_owner [FAIL]
  Assert.Equal() Failure: Values differ / Expected: 1 / Actual: 5
Başarısız! - Başarısız: 1, Başarılı: 18, Toplam: 19

(c) — 19 testten 4'ü başarısız
[xUnit.net] …QueueTests.K5_single_sender_requests_are_handled_in_submission_order [FAIL]
[xUnit.net] …QueueTests.K5_five_files_are_all_processed_exactly_once_no_loss [FAIL]
[xUnit.net] …QueueTests.L2_malformed_pipe_message_is_answered_with_a_refusal_not_a_silent_drop [FAIL]
[xUnit.net] …QueueTests.K4_second_instance_hands_off_and_never_runs_concurrently_with_the_owner [FAIL]
Başarısız! - Başarısız: 4, Başarılı: 15, Toplam: 19

(d) — 19 testten 1'i başarısız
[xUnit.net] …QueueTests.L1_second_StartOwning_is_refused_so_a_single_consumer_remains [FAIL]
  Assert.Throws() Failure: No exception was thrown
Başarısız! - Başarısız: 1, Başarılı: 18, Toplam: 19

(e) — 19 testten 1'i başarısız
[xUnit.net] …QueueTests.L2_malformed_pipe_message_is_answered_with_a_refusal_not_a_silent_drop [FAIL]
  System.IO.IOException : Pipe is broken.
Başarısız! - Başarısız: 1, Başarılı: 18, Toplam: 19

(f) — 19 testten 1'i başarısız
[xUnit.net] …QueueTests.K5_single_sender_requests_are_handled_in_submission_order [FAIL]
  Assert.Equal() Failure: Collections differ
  Expected: ["sira-0.mp4", "sira-1.mp4", "sira-2.mp4", "sira-3.mp4", "sira-4.mp4"]
  Actual:   ["sira-0.mp4", "sira-4.mp4", "sira-3.mp4", "sira-2.mp4", "sira-1.mp4"]
Başarısız! - Başarısız: 1, Başarılı: 18, Toplam: 19
```

Her mutasyondan sonra orijinal dosya geri yüklendi ve doğrulandı (`diff` boş çıktı verdi);
son derleme ve koşum tekrar 19/19 yeşil:

```
dotnet build -c Release --no-incremental → Oluşturma başarılı oldu. 0 Uyarı, 0 Hata.
dotnet test -c Release --no-build --filter "FullyQualifiedName~ShrinkRequestTests"
Başarılı!  - Başarısız:     0, Başarılı:    19, Atlanan:     0, Toplam:    19, Süre: 941 ms
```

Boru üzerinde koşan kollar zamanlamaya duyarlı olabildiği için filtreli koşum arka arkaya
**üç kez** tekrarlandı; üçü de 19/19:

```
Başarılı!  - Başarısız: 0, Başarılı: 19, Toplam: 19, Süre: 1 s
Başarılı!  - Başarısız: 0, Başarılı: 19, Toplam: 19, Süre: 1 s
Başarılı!  - Başarısız: 0, Başarılı: 19, Toplam: 19, Süre: 956 ms
```

## K7 — Kol Sayısı

```
dotnet test -c Release --filter "FullyQualifiedName~ShrinkRequestTests" --list-tests
Şu Testler kullanılabilir: (19 satır)
```

On dokuz kol bulundu, sıfır bulan kol yok. `QueueTests` altındaki yedi kol:

```
VidShrink.Tests.ShrinkRequestTests+QueueTests.First_instance_becomes_owner_second_does_not
VidShrink.Tests.ShrinkRequestTests+QueueTests.K4_second_instance_hands_off_and_never_runs_concurrently_with_the_owner
VidShrink.Tests.ShrinkRequestTests+QueueTests.K5_five_files_are_all_processed_exactly_once_no_loss
VidShrink.Tests.ShrinkRequestTests+QueueTests.K5_single_sender_requests_are_handled_in_submission_order
VidShrink.Tests.ShrinkRequestTests+QueueTests.L1_second_StartOwning_is_refused_so_a_single_consumer_remains
VidShrink.Tests.ShrinkRequestTests+QueueTests.L2_malformed_pipe_message_is_answered_with_a_refusal_not_a_silent_drop
VidShrink.Tests.ShrinkRequestTests+QueueTests.L2_submit_reports_failure_when_the_owner_is_not_listening
```

Kalan on iki kol `ResolverTests` altında: K1'in beş, K2'nin altı biçimi ve K3.
