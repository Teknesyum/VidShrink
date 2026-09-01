# Süreçler arası ölçü yalıtımı

Durum: **tur 2 tamamlandı — 2026-09-01.** Kriter 2 tam olarak ölçülmedi; nedeni
"Eşzamanlı koşum" bölümünde yazılı.

Bu rapordaki her sayı bir araç çıktısından olduğu gibi alınmıştır. Ham günlükler koşum
sırasında `.calisma/t86/` altında tutuldu; her bölüm hangi dosyadan alındığını söylüyor.
`.calisma/` git'e girmez, teslimden sonra silinir — dosya adları neyin nereden geldiğini
izlemek için yazılıdır.

## Hangi durum paylaşılıyordu, nasıl yalıtıldı

| Ölçü | Paylaşılan durum | Yalıtma |
|---|---|---|
| `PerformanceCheckTests.OlcumArtikBirakmiyor` | Sistemin `%TEMP%`'i; aynı anda koşan öteki süitin bıraktığı dizinleri de sayıyordu | Geçici kök `.calisma/test-ciktilari/performance-temp/<PID>/<GUID>`; `TEMP`/`TMP` yalnız ölçüm boyunca oraya bakıyor ve sonunda eski değerine dönüyor |
| `ShellMenuTests.Every_command_calls_the_installed_launcher_with_the_path` | Yalıtım `main`de zaten vardı: kök `HKCU:\Software\VidShrink-Test-<GUID>` | Değişiklik yapılmadı sayılır — ayrıntı aşağıda, "ShellMenuTests: kozmetik değişiklik" |
| `Windows11ShellMenuTests.Sparse_package_really_registers_and_removes_on_Windows_11` | Makine genelinde AppX kaydı; paket kimliği `AppxManifest.template.xml`de sabit | Yalıtılamıyor: kimliği değiştirmek `src/`e dokunmak demek ve COM sunucu kaydı kimliğe bağlı. Bunun yerine `Global\VidShrink-Sparse-Package-Test` adlı kilitle sıraya alındı |
| `UpdaterTests.TheDeletionStepWaitsOutATransientLock` | Paylaşılan durum değil, kendi kurduğu yarış | Aşağıda, "Zamanlama yerine durum" |

Updater test kökü de `.calisma/test-ciktilari/updater/<PID>/<GUID>` altına taşındı.

## Zamanlama yerine durum

Ek madde ve H1 aynı kusuru gösteriyordu: kilidi bırakan iş parçacığı `.basladi`
işaretini görüp `Thread.Sleep(300)` bekliyordu. İşaret `Remove-InstallRoot`
çağrılmadan **önce** yazıldığı için, yüklü bir koşucuda ilk `Remove-Item` denemesi
300 ms'yi aşınca kilit serbest kalıyor, silme ilk denemede başarıyor ve
`Assert.Contains("yeniden denenecek", log)` düşüyordu.

Şimdi kilidi bırakan iş parçacığı **süreye değil günlüğe** bakıyor. `RemovalProbe`
betiğine bir `Write-Host` vekili kondu; vekil her satırı `<gunluk>.akis` yan dosyasına
anında yazıyor, sonra gerçek `Write-Host`'a devrediyor. Test iş parçacığı bu yan
dosyada `yeniden denenecek` görünene kadar kilidi tutuyor, ancak ondan sonra
bırakıyor. `.basladi` işareti ve `Thread.Sleep(300)` payı kalktı.

Yeniden deneme yolu gevşetilmedi: eski iddialar duruyor, üstüne
`Assert.True(releasedOnNotice, ...)` eklendi — kilit duyuru görülmeden bırakıldıysa
ölçü kırmızıya döner.

`rg "Sleep\(3" tests/VidShrink.Tests/UpdaterTests.cs` boş dönüyor.

### Yük benzetimi: eski düzenek düşüyor, yeni düzenek düşmüyor

CI'daki düşüşün kod değil yük kaynaklı olduğunu göstermek için `Remove-InstallRoot`
ilk `Remove-Item` çağrısından önce 1500 ms geciktirildi; bu tek değişiklikle iki
düzenek arka arkaya koşturuldu.

Eski düzenek (ham günlük `.calisma/t86/yuk-eski.txt`):

    [xUnit.net 00:00:02.60]       Assert.Contains() Failure: Sub-string not found
    [xUnit.net 00:00:02.60]       String:    "BITTI\r\n"
    [xUnit.net 00:00:02.60]       Not found: "yeniden denenecek"

Bu, sözleşmedeki CI koşumu 33525962057'nin düşüşünün birebir aynısı.

Yeni düzenek, aynı 1500 ms gecikmeyle (ham günlük `.calisma/t86/yuk-yeni.txt`):

      Başarılı VidShrink.Tests.UpdaterTests.TheDeletionStepWaitsOutATransientLock [2 s]
     geçici kilit: çıkış 0, 2734 ms
    Test Çalıştırması Başarılı.

Gecikme geri alındı.

### `UpdaterTests` — arka arkaya üç koşum

`dotnet test -c Release --filter "UpdaterTests"`, ham günlükler
`.calisma/t86/updater-son-{1,2,3}.txt`; her koşum ayrıca koşum kapısından geçirildi
(`-MinimumTotal 54`, üçünde de kapı çıkışı 0):

    Başarılı!  - Başarısız:     0, Başarılı:    51, Atlanan:     3, Toplam:    54, Süre: 11 s - VidShrink.Tests.dll (net8.0)
    Başarılı!  - Başarısız:     0, Başarılı:    51, Atlanan:     3, Toplam:    54, Süre: 11 s - VidShrink.Tests.dll (net8.0)
    Başarılı!  - Başarısız:     0, Başarılı:    51, Atlanan:     3, Toplam:    54, Süre: 11 s - VidShrink.Tests.dll (net8.0)

Atlanan üçü `TheIncomingBinaryRenamesItselfOntoTheTargetName`,
`EveryLaunchChecksAndStaysWithinTheTimeout`,
`SwitchedOffLauncherMakesNoNetworkRequestAtAll`. Bu raporda hiçbir iddia bu üçüne
dayanmıyor. Atlanan sayısı bu turda artmadı.

## ShellMenuTests: kozmetik değişiklik, gerçek neden başka yerde

Tur 1'de `ShellMenuTests`e PID eklenmişti. `main`de kök zaten GUID taşıyordu
(`HKCU:\Software\VidShrink-Test-<GUID>`), yani süreçler arası çakışma zaten
olanaksızdı. **PID eklemek yalıtımı değiştirmedi; düzeltme değildi.**

Sözleşmenin bağlamındaki `New-Item` `IOException`'ının nedeni arandı ve bulundu:
paylaşılan kaynak testin kökü değil, **kurulum betiğinin kayıt defteri yazma
sırası**. `Install-VidShrink.ps1:271` (`Remove-ShellMenu`) boşalan üst anahtarları
buduyor, `:281-286` (`Write-ShellMenu`) aynı anahtarları kuruyor. İki koşum **aynı
kökü** paylaşırsa biri budarken öteki yazıyor.

Deney: 12 eşzamanlı kurulum yazımı, hepsi tek bir paylaşılan
`HKCU:\Software\VidShrink-Test-H3-PAYLASILAN` köküne (ham günlük
`.calisma/t86/h3-cakisma.txt`). Onikinin ikisi sıfırdan farklı çıkışla düştü:

    Remove-Item : Nesne başvurusu bir nesnenin örneğine ayarlanmadı.
    ...Install-VidShrink.ps1:271 char:13

    Set-Item : The registry key at the specified path does not exist.
    ...Install-VidShrink.ps1:286 char:9

    TOPLAM 12 ESZAMANLI YAZIM, SIFIRDAN FARKLI CIKIS: 2

**Bugünkü ölçü bu durumu yakalamıyor** ve yakalayamaz: `ShellMenuTests` her koşumda
GUID'li ayrı bir kök kullandığı için iki koşum hiçbir zaman aynı kökü paylaşmaz.
Yani ölçü tarafında yapılacak bir şey kalmadı; kusur `Install-VidShrink.ps1`'in
kendi yazma sırasında ve o dosya bu sözleşmenin `owns` listesinde değil.

**Gereken (bu sözleşmenin dışında):** `Write-ShellMenu`/`Remove-ShellMenu` çifti aynı
kök üzerinde çakışmaya karşı korunmalı — ya adlandırılmış bir kilitle, ya budama
adımı `Test-Path` ile `Remove-Item` arasındaki yarışı kabul eden bir yeniden
denemeyle. Burada durduruldu.

## `TEMP`/`TMP` bağımlılığı artık testte yazılı

`OlcumArtikBirakmiyor` ortam değişkenlerini süreç genelinde değiştiriyor; bu yalnız
`LanguageTests.cs:13`'teki `[assembly: CollectionBehavior(DisableTestParallelization = true)]`
durduğu sürece güvenli. Bağımlılık artık ölçünün ilk iddiası: özniteliğin varlığı
doğrulanıyor ve iddia mesajı neden gerektiğini söylüyor.

Mutasyon — `LanguageTests.cs`'ten o satır geçici olarak kaldırıldı (ham günlük
`.calisma/t86/mutasyon-h5.txt`):

      Başarısız VidShrink.Tests.PerformanceCheckTests.OlcumArtikBirakmiyor [2 ms]
       bu ölçü TEMP/TMP'yi süreç genelinde değiştiriyor; süit içi paralellik açıkken aynı süreçteki öteki ölçüler yönlendirilmiş %TEMP% görür. ...
    Test Çalıştırması Başarısız.

Satır geri kondu; `git status` temiz.

## AppX kilidi: `Global\` ve terk edilen kilit

Kilit `Local\`dan `Global\`a alındı ve `AbandonedMutexException` yakalanıp sıraya
devam ediliyor. Sessiz erken dönüşler kalktı: Windows 11 altı sürüm, MSBuild
yokluğu ve "paket zaten kayıtlı" durumlarının üçü de çıktıya `ATLANDI: ...` yazıyor.

Kilit davranışı ayrıca doğrudan ölçüldü (ham günlük `.calisma/t86/h4-kilit-kanit.txt`):

    --- A) Terk edilen kilit: kilidi tutan surec oldurulur, sonraki WaitOne ne yapar?
      kilidi tutan surec olduruluyor (PID 14048)
      AbandonedMutexException firladi: True
      yakalanmasaydi olcu dusecekti; yakalandi, sahiplik kabul edildi: sahip=True
    --- B) Global ad alani: kilit gercekten siraya sokuyor mu?
      kilit bizdeyken rakip cikis kodu: 20  (20 = siraya girdi/zaman asimi, 10 = kilit tutmadi)
      kilit birakildiktan sonra rakip cikis kodu: 10  (10 = aldi)

A şıkkı, terk edilen kilidin gerçekten `AbandonedMutexException` fırlattığını
gösteriyor — eski kod bu noktada sıraya girmek yerine düşerdi.

## Mutasyon denetimleri

Her düzeltme için ölçtüğü üretim davranışı bozuldu, ölçünün kırmızıya döndüğü
görüldü, değişiklik geri alındı. Geri alma sonrası `git status` her seferinde temiz.

| Ölçü | Geçici mutasyon | Kırmızı kanıtı | Ham günlük |
|---|---|---|---|
| `PerformanceCheckTests.OlcumArtikBirakmiyor` | `PerformanceProbe.Cleanup` dizini silmiyor | `Assert.Equal() Failure: Values differ` — `Başarısız: 1, Toplam: 1` | `.calisma/t86/mutasyon-k3a.txt` |
| `ShellMenuTests.Every_command_calls_the_installed_launcher_with_the_path` | Kabuk komutundan `"%1"` kaldırıldı | `Assert.Equal() Failure: Strings differ` — `Başarısız: 1, Toplam: 1` | `.calisma/t86/mutasyon-k3b.txt` |
| `Windows11ShellMenuTests.Sparse_package_really_registers_and_removes_on_Windows_11` | AppX kimliği `Teknesyum.VidShrink.ShellMUTASYON` yapıldı | `Assert.EndsWith() Failure: String end does not match` — `Başarısız: 1, Toplam: 1` | `.calisma/t86/mutasyon-k3c.txt` |
| `UpdaterTests.TheDeletionStepWaitsOutATransientLock` | Silme deneme sayısı `6`dan `1`e indirildi | `kilit yeniden deneme duyurusu görülmeden bırakıldı:` / `geçici kilit: çıkış 3, 1055 ms` | `.calisma/t86/mutasyon-h1.txt` |
| `PerformanceCheckTests.OlcumArtikBirakmiyor` (paralellik iddiası) | `DisableTestParallelization` satırı kaldırıldı | `Test Çalıştırması Başarısız.` | `.calisma/t86/mutasyon-h5.txt` |

AppX mutasyonunun kırmızıya dönmesi ayrıca şunu gösteriyor: bu ölçü bu makinede
gerçekten koşuyor, atlanmıyor.

## Eşzamanlı koşum

**Ölçülen bu değil.** Kriter 2 "iki **tam süit** aynı anda" diyor. Bu turda tam süit
koşturulmadı: makinede aynı anda üç ajan daha çalışıyor ve paralel tam süit ölçüyü
kendisi kararsız yapıyor. Onun yerine sözleşmenin kendi `verify:` filtresi
(`PerformanceCheckTests|ShellMenuTests|Windows11ShellMenuTests|UpdaterTests`,
`Toplam: 88`) iki süreçte aynı anda, arka arkaya üç kez koşturuldu.

Yani **kriter 2 kısmen kapandı**: sözleşmenin konusu olan dört ölçü eşzamanlı
koşumda altı kez üst üste yeşil; süitin geri kalanının eşzamanlı davranışı bu turda
ölçülmedi.

Tek bir Release derlemesinden sonra iki `dotnet test -c Release --no-build --filter ...`
süreci aynı anda başlatıldı; çift bitmeden sonrakine geçilmedi. Ham günlükler
`.calisma/t86/esszamanli2-{1A,1B,2A,2B,3A,3B}.txt`:

    Başarılı!  - Başarısız:     0, Başarılı:    85, Atlanan:     3, Toplam:    88, Süre: 3 m 6 s - VidShrink.Tests.dll (net8.0)
    Başarılı!  - Başarısız:     0, Başarılı:    85, Atlanan:     3, Toplam:    88, Süre: 3 m 7 s - VidShrink.Tests.dll (net8.0)
    Başarılı!  - Başarısız:     0, Başarılı:    85, Atlanan:     3, Toplam:    88, Süre: 3 m 15 s - VidShrink.Tests.dll (net8.0)
    Başarılı!  - Başarısız:     0, Başarılı:    85, Atlanan:     3, Toplam:    88, Süre: 3 m 9 s - VidShrink.Tests.dll (net8.0)
    Başarılı!  - Başarısız:     0, Başarılı:    85, Atlanan:     3, Toplam:    88, Süre: 3 m 6 s - VidShrink.Tests.dll (net8.0)
    Başarılı!  - Başarısız:     0, Başarılı:    85, Atlanan:     3, Toplam:    88, Süre: 3 m 7 s - VidShrink.Tests.dll (net8.0)

Sırasıyla 1A, 1B, 2A, 2B, 3A, 3B. Altısı da koşum kapısından geçirildi
(`-MinimumTotal 88`), altısında da kapı çıkışı 0.

### Bundan önceki altı koşum — beşi yeşil, biri kırmızı

Bu turda alınan **ilk** altı koşumda 3A düştü. Ham günlükler
`.calisma/t86/esszamanli-{1A..3B}.txt`; kapı 3A'yı 66 ile reddetti, diğer beşi 0:

    Başarısız! - Başarısız:     1, Başarılı:    84, Atlanan:     3, Toplam:    88, Süre: 3 m 20 s - VidShrink.Tests.dll (net8.0)

Düşen ölçü sözleşmenin dördü arasında değildi ama aynı dosyada:
`UpdaterTests.TheProbeCatchesATwoStepSwapSoTheMeasureHasTeeth`, `UpdaterTests.cs:570`,
`IOException: The process cannot access the file because it is being used by another
process`. `NameProbe` dosyayı tutmuyor (yalnız `File.Exists` çağırıyor); tutan,
Windows'un tazeyken yazılmış dosyaya kısa süreli erişimi. T85'te temizlik adımına
eklenen sınırlı yeniden deneme iki `File.Move` çağrısına da uygulandı
(`MoveOverTransientLock`). İddia değişmedi: ölçü hâlâ "adın boşaldığı görüldü mü"
diye soruyor. Yukarıdaki altı yeşil koşum bu düzeltmeden sonra alındı.

## Koşum kapısı

İki kör nokta kapatıldı, ikisi de mutasyonla doğrulandı (ham günlük
`.calisma/t86/mutasyon-h6.txt`):

| Kör nokta | Mutasyon | Mutasyonlu kapı | Onarılmış kapı |
|---|---|---|---|
| Son eşleşme semantiği: iki özet satırından ilki `Başarısız: 5`, sonuncusu `Başarısız: 0` | Kontrol son eşleşmeye geri döndürüldü | `ikili-ozet-tr.txt` → çıkış 0 (geçiriyor) | çıkış 66 |
| Kesinti listesi konak çökmesini tanımıyor | Bu turda eklenen üç desen kaldırıldı (`main` hali) | `konak-cokmesi-tr.txt` → çıkış 0 (geçiriyor) | çıkış 65 |

Ayrıca kapı düştüğü şartı ve kodu artık kendisi yazıyor
(`KOSUM KAPISI DUSTU: kod=<n> sart=<...>`), böylece CI adımı çıkış kodunu yutsa bile
hangi şartın düştüğü çıktıdan okunuyor. CI iş akışının kendisi bu sözleşmenin
`owns` listesinde değil; adımın kapı kodunu koruyacak biçimde çağrılması yapılmadı.

Fixture'lar: `gercek-kosum-tr.txt` bu turda alınan eşzamanlı koşumlardan birinin
(`esszamanli-1A`) kaydedilmiş **gerçek** `dotnet test` çıktısı.
`konak-cokmesi-tr.txt` birinci şartı yalıtıyor — `Toplam:` alt sınırın üstünde,
`Başarısız: 0`, düşüren tek şey kesinti satırı. `ikili-ozet-tr.txt` iki özet satırı
taşıyor. `test-kapi.ps1` artık fixture başına alt sınır tutuyor ve sekiz durumu
birden koşuyor:

    KOŞUM KAPISI GEÇTİ: başarısız=0 toplam=974 alt-sınır=974
    KOŞUM KAPISI GEÇTİ: başarısız=0 toplam=974 alt-sınır=974
    KOŞUM KAPISI GEÇTİ: başarısız=0 toplam=88 alt-sınır=80
    KOSUM KAPISI DUSTU: kod=65 sart=Kosum kesinti/iptal satiri iceriyor.
    KOSUM KAPISI DUSTU: kod=65 sart=Kosum kesinti/iptal satiri iceriyor.
    KOSUM KAPISI DUSTU: kod=66 sart=Basarisiz/Failed ozeti sifir degil: Failed: 1.
    KOSUM KAPISI DUSTU: kod=66 sart=Basarisiz/Failed ozeti sifir degil: Başarısız: 5.
    KOSUM KAPISI DUSTU: kod=68 sart=Toplam test sayisi alt sinirin altinda: 500 < 974.
    kosum-kapisi fixture testleri geçti

## Ölçülmeyenler

- **Tam süit.** Bu turda hiç koşturulmadı; ne tek başına ne eşzamanlı. Sebep yukarıda.
  Süitin bütünü üzerine bu turdan çıkan bir iddia yok.
- **Farklı Windows oturumlarında AppX çakışması.** Kilit `Global\` ad alanına alındı
  ve ad alanının sıraya soktuğu ölçüldü; iki ayrı oturumdan eşzamanlı koşum
  denenmedi.
- **CI adımının kapı çıkış kodunu koruması.** `.github/workflows/ci.yml` `owns`
  dışında; kapı kendi kodunu çıktıya yazacak hale getirildi, adım değiştirilmedi.
- **`Install-VidShrink.ps1`'in kayıt defteri yarışı.** Bulundu ve ölçüldü,
  düzeltilmedi — dosya `owns` dışında.
