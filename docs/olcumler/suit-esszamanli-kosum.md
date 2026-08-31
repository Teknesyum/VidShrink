# T85 — Süit eşzamanlı koşumda, arayüz ölçümlerinin maliyeti

Soru iki taneydi: süit makinede ikinci bir koşum varken neden yarıda duruyor, ve
`WindowLayoutTests` neden ölçü başına dokuz saniye harcıyor.

Makine: 63,6 GB RAM · sayfa dosyası 4 GB · **ayrılabilir bellek tavanı 67,6 GB** ·
Windows 11 · net8.0 Release · Avalonia 11.3.20. Bütün koşumlar
`agent-a31d0405185eb7b5b` worktree'sinde, `origin/main` = `26cf834` üzerinde.
Bu ağaçta `.calisma/kaynak/` yok, o yüzden atlanan taban **23**.

## Teşhis

**Ölçüm kapatmadığı her `MainWindow`'u `Strings.Changed` statik olayının ucunda
canlı tutuyor; her dil geçişi o ana kadar kurulmuş bütün pencerelerin arayüzünü
baştan kuruyor, dolayısıyla sınıfın hem süresi hem belleği ölçü sayısının karesiyle
büyüyor.**

Zinciri kapatan iki satır uygulama tarafında:

- `src/VidShrink.App/MainWindow.axaml.cs:126` — yapıcıda `Strings.Changed += OnLanguageChanged;`
- `src/VidShrink.App/MainWindow.axaml.cs:422` — abonelik yalnız `OnClosing` içinde bırakılıyor.

`AppHost.Run` her ölçüden önce dili `"en"`e çeviriyor, `WindowLayoutTests` ardından
`UseTurkish()` ile `"tr"`ye geçiyor: ölçü başına iki dil geçişi. `Strings.Use` yalnız
dil gerçekten değiştiğinde `Changed`'i ateşliyor, yani ikisi de ateşliyor. Her ateşleme
biriken **bütün** pencerelerde `OnLanguageChanged`'i koşturuyor; o gövde panelleri,
sekmeleri, plan alanını yeniden kuruyor ve sonunda `SaveSettings()` çağırıyor.

Tükenen kaynağın ne **olmadığı** da ölçüldü: iki eşzamanlı koşum sırasında testhost
süreçlerinin GDI ve USER tutamağı 219'da kaldı, yani masaüstü öbeği ya da pencere
tutamağı tükenmiyor. Tükenen şey ayrılabilir bellek: aynı anda üç süit koşarken
ayrılan bellek 60 GB'a çıktı, tavan 67,6 GB.

Kapatmanın süreyi neden düşürdüğü ayrı bir sonda ile de görüldü: beş pencere kurup
`GC.GetTotalMemory(true)` okuyan geçici bir ölçü, kapatmadan **824 ms**, kapatarak
**444 ms** verdi. Yönetilen öbekteki artış iki hâlde de pencere başına ~4 MB; süreç
belleğinin gigabaytları yönetilmeyen tarafta (Skia yüzeyleri, yazı tipi önbellekleri).

## Çözüm

`WindowLayoutTests` içindeki dokuz pencere kurma noktası tek bir kapıdan geçiyor:

```csharp
private static T Fresh<T>(Func<MainWindow, T> use) =>
    AppHost.Run(() =>
    {
        var window = new MainWindow();
        try { return use(window); }
        finally { window.Close(); }
    });
```

Hiçbir ölçünün iddiası değişmedi: ölçü silinmedi, `Skip` eklenmedi, eşik gevşetilmedi,
sınıf 36 ölçüyle duruyor ve hepsi yeşil.

## Altı koşumun sonuç satırı

İki `dotnet test -c Release` süreci aynı anda, arka arkaya üç kez.

| Çift | Süreç | Sonuç satırı |
|---|---|---|
| 1 | A | `Başarısız: 0, Başarılı: 935, Atlanan: 23, Toplam: 958, Süre: 20 m 19 s` |
| 1 | B | `Başarısız: 0, Başarılı: 935, Atlanan: 23, Toplam: 958, Süre: 12 m 30 s` |
| 2 | A | `Başarısız: 0, Başarılı: 935, Atlanan: 23, Toplam: 958, Süre: 14 m 30 s` |
| 2 | B | `Başarısız: 0, Başarılı: 935, Atlanan: 23, Toplam: 958, Süre: 14 m 7 s` |
| 3 | A | `Başarısız: 1, Başarılı: 934, Atlanan: 23, Toplam: 958, Süre: 15 m 18 s` |
| 3 | B | `Başarısız: 1, Başarılı: 934, Atlanan: 23, Toplam: 958, Süre: 16 m 27 s` |

**Altı koşumun altısı da sonuna kadar gitti** — `Toplam: 958`, yarıda durma yok.
Şikâyet edilen belirti bu koşumların hiçbirinde görülmedi.

Üçüncü çiftteki iki düşen ölçü aynı ölçü değil ve ikisi de aynı cinsten: süreçler
arası paylaşılan **işletim sistemi durumuna** bakıyorlar.

- `PerformanceCheckTests.OlcumArtikBirakmiyor` — sistemin `%TEMP%` klasöründe
  `PerformanceProbe.TempPrefix*` dizinlerini sayıyor. Öteki sürecin canlı sondası da
  o klasörde: `Expected: 1, Actual: 2`.
- `ShellMenuTests.Every_command_calls_the_installed_launcher_with_the_path` —
  `Install-VidShrink.ps1` HKCU altındaki kabuk anahtarlarını yazıyor. İki süreç aynı
  anahtarı yazınca `New-Item ... IOException`.

Mutasyon koşumunda üçüncü bir örnek daha çıktı:
`Windows11ShellMenuTests.Sparse_package_really_registers_and_removes_on_Windows_11`
(makine genelinde paket kaydı).

Üçü de bu sözleşmenin sahiplendiği dosyaların dışında ve üçü de aynı düzeltmeyi
istiyor: süreç kimliğiyle yalıtılmış bir geçici klasör / kayıt defteri kökü.

## Süre ve bellek tablosu

Sınıf tek başına, tek süreç, arka arkaya iki ölçüm:

| Ölçüm | Önce (kapatmadan) | Sonra (kapatarak) |
|---|---|---|
| `WindowLayoutTests` 1. koşum | 5 m 13 s | 3 m 25 s |
| `WindowLayoutTests` 2. koşum | 5 m 25 s | 3 m 27 s |
| Ölçü başına | ~8,8 sn | ~5,7 sn |
| Süreç tepe belleği | 8,2 GB | 3,5 GB |

Tam süit:

| Ölçüm | Önce | Sonra |
|---|---|---|
| Tek başına, tek süreç | 13 m 49 s (T85 bağlamındaki taban, boş makine) | 15 m 47 s (bu makine, boş değil) |
| Tek başına tepe süreç belleği | — | 5,5 GB |
| İki eşzamanlı süreçte tepe süreç belleği | 10,4 GB | 6,2 GB |

Sondaki koşumun satırı:
`Başarısız: 0, Başarılı: 935, Atlanan: 23, Toplam: 958, Süre: 15 m 47 s`.

Tam süitin **süresi düşmedi; ölçüldüğü kadarıyla arttı, ama o sayı karşılaştırılabilir
değil.** Taban boş bir makinede alınmıştı; buradaki koşumların tamamı boyunca aynı
makinede başka bir ajan çalışıyordu ve gürültü kazancın kat kat üstünde: aynı ikili
arka arkaya 12 m 30 s ile 20 m 19 s arasında değişti. Karşılaştırılabilir olan tek
şey sınıfın kendi A/B'si (yukarıdaki ilk tablo, dört koşum, aynı oturum) ve bellek:
eşzamanlı çiftte süreç başına tepe 10,4 GB'dan 6,2 GB'a indi.

## Arka uç: Win32 gerekli mi

`AppHost.Backend` başsıza alınıp `WindowLayoutTests` yeniden koşturuldu.

| | Win32 | Headless |
|---|---|---|
| Sonuç | 36/36 yeşil | 36/36 yeşil |
| Süre | 4 m 49 s | 4 m 40 s |
| Ölçümlerin bastığı sekiz sayı satırı | — | **birebir aynı** (`diff` boş) |

Yani gerçek pencere açmanın ölçülen hiçbir karşılığı yok; ne sayılar değişiyor ne süre.
**Buna rağmen `Win32` kaldı**, çünkü başsıza geçirmek sahiplenmediğim bir ölçüyü
kırıyor:

```
VidShrink.Tests.MacOsStartupTests.TheWindowingBackendIsWin32OnWindowsAndHeadlessElsewhere
Assert.Equal(OperatingSystem.IsWindows() ? "HWND" : "STUB", descriptor);
```

Bu ölçü `AppHost`'un seçimini çalışma zamanından okuyup Windows'ta `HWND` bekliyor.
Sözleşme `tests/VidShrink.Tests/` altındaki öteki ölçü dosyalarına dokunmayı yasakladığı
ve K5 hiçbir iddianın zayıflamamasını istediği için arka uç değiştirilmedi. Çökme
zaten arka uçtan gelmiyor, dolayısıyla değiştirmek bir şey de kazandırmıyor.

**Sahibinden istenen:** `MacOsStartupTests.cs` başsız arka uca uyarlanırsa
`AppHost.Backend` tek satırla `"Headless"` olur ve masaüstünde hiç pencere açılmaz.

## Mutasyon

Taşıyan adım (`Fresh` ile pencere kapatma) geri alındı, ikili aynı biçimde koşturuldu:

```
mutasyon A: Başarısız: 1, Başarılı: 934, Atlanan: 23, Toplam: 958, Süre: 12 m 54 s
mutasyon B: Başarısız: 0, Başarılı: 935, Atlanan: 23, Toplam: 958, Süre: 14 m 42 s
```

Düşen ölçü yine paylaşılan makine durumundan geliyordu
(`Windows11ShellMenuTests.Sparse_package_really_registers_and_removes_on_Windows_11`),
kilitlenme yok.

Basınç artırıldı — **üç** eşzamanlı süit, yine düzeltmesiz:

```
uclu-oncesi A: Başarısız: 0, Başarılı: 935, Atlanan: 23, Toplam: 958, Süre: 14 m 12 s
uclu-oncesi B: Başarısız: 0, Başarılı: 935, Atlanan: 23, Toplam: 958, Süre: 16 m 14 s
uclu-oncesi C: Başarısız: 0, Başarılı: 935, Atlanan: 23, Toplam: 958, Süre: 17 m 55 s
```

Üç süreç birlikte 28,9 GB tuttu, ayrılan bellek 60 GB'a çıktı — tavana 7,6 GB kaldı —
ama üçü de sonuna kadar gitti.

**Mutasyon sonucu: çökme geri gelmedi.** Bu ayrı bir worktree'de, makinede yalnız
`dotnet test` süreçleri varken çökmenin **hiç** üretilemediği anlamına geliyor;
düzeltmeli de düzeltmesiz de. Şikâyet edilen dört koşum paylaşılan çalışma ağacında,
aynı depoda **başka bir ajan derleme ve ffmpeg koşarken** alınmıştı; buradaki düzenek
o yükü taşımıyor.

Dolayısıyla bu rapor şunu **kanıtlamıyor**: sızıntının çökmenin nedeni olduğu.
Kanıtladığı şey, sızıntının ölçülmüş olduğu ve kapatıldığında sınıfın %35 hızlandığı,
eşzamanlı çiftte tepe süreç belleğinin 10,4 GB'dan 6,2 GB'a düştüğü. Çökmenin
tekrarlanabilir bir düzeneği hâlâ yok; sırada bunu üretmek var.
