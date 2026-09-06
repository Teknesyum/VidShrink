# Kabuk Küçültme İsteğinin Tüketimi

T171, tur 1. Dal `T171-kabuk-istegi-tuketimi`. Bütün ham çıktılar `.calisma/T171/` altında;
her sayının yanında onu üreten dosyanın yolu var.

Ölçüm makinesi: Windows 11 Pro 26100, .NET 8, Release yapı
(`src/VidShrink.App/bin/Release/net8.0/VidShrink.App.exe`). Kurulu uygulama kullanılmadı.

## K1 — Düşen isteğin ölçüsü

### Giriş noktası sayımı

Gerçek yükleyici (`Install-VidShrink.ps1`) tek kullanımlık bir kayıt defteri köküne
(`HKCU:\Software\VidShrinkKucult-Test-<pid>-<guid>`) koşuldu, yazdığı her komut satırı okundu
ve sayıldı. Betik: `.calisma/T171/k1-giris-say.ps1`. Ham çıktı:
`.calisma/T171/k1-giris-say.txt`. Tam döküm (120 satır):
`.calisma/T171/k1-kayit-defteri-tam.tsv`.

| ölçü | sayı |
|---|---|
| kayıt defterindeki toplam komut satırı | 144 |
| `--kucult` taşıyan komut satırı | 120 |
| `--kucult` taşımayan (düz aç) komut satırı | 24 |
| farklı uzantı | 24 |
| farklı hedef boyut | 5 (100, 250, 500, 1024, 2048) |
| farklı komut şablonu | 1 |
| `%1` sayısı her komutta | 1 |
| `%*` içeren komut | 0 |
| `MultiSelectModel` değeri | hepsinde `Player` |

120 = 24 × 5. Tek şablon:

```
"<kurulum>\VidShrink.exe" --kucult <HEDEF> "%1"
```

Kod tarafında `--kucult` taşıyan argümanı alan üç ayrı süreç girişi var; **tüketen tek
giriş** `src/VidShrink.App/Program.cs`:

| giriş | dosya | bugünkü davranışı |
|---|---|---|
| uygulama `Main` | `src/VidShrink.App/Program.cs:11` | `ResolveStartupPath` — hedef boyutu okumuyor |
| başlatıcı `Main` | `src/VidShrink.Launcher/Program.cs` | argümanları aynen iletiyor, tüketmiyor |
| Win11 kabuk uzantısı | `src/VidShrink.ShellExtension/VidShrink.ShellExtension.cpp` | küçültme alt menüsü yok (`EnumSubCommands` → `E_NOTIMPL`), `--kucult` hiç geçirmiyor |

Yani düzeltilecek yer bir tane: `Program.cs`. Diğer ikisi bayrağı üretmiyor ya da
yalnızca aktarıyor.

### Bugünkü davranışın ham çıktısı

`.calisma/T171/k1-bugun.txt` — `VidShrink.App.exe --kucult 250 ornek-1.mp4`:

```
t= 0s  PENCERE  pid=16100  sinif=Avalonia-...  baslik=VidShrink
--- ozet ---
acilan farkli gorunur pencere: 1
en cok es zamanli ffmpeg sureci: 6
kalan VidShrink.App sureci: 1
```

Açılan pencere **ana penceredir**: süreç 20 saniye sonra hâlâ ayakta, altı ffmpeg süreci
ana pencerenin önizleme/yoklama düzeneğinden geliyor. Seçilen 250 MB hiçbir yere gitmiyor;
`ShrinkArgumentResult.Problem` alanını `src/**` altında okuyan satır yoktu — bunu `OluUyeTests`
pimi beş satırla borç olarak yazıyordu (aşağıda K3).

## K2 — İstek tüketiliyor, ana pencere açılmıyor

Gözlemci `.calisma/T171/gozle.ps1`: pencereleri `EnumWindows` ile sayar, her görünür
pencerenin pid/başlık/ölçüsünü yazar, saniyede bir canlı `VidShrink.App` ve `ffmpeg` sayısını
kaydeder.

Kaynak dosyalar: `buyuk-1/2/3.mp4`, her biri 129,735 MB, 40 s, 1920x1080. Hedef 100 MB —
yani gerçek bir kodlama koşuyor, geçişli (pass-through) plan değil.

### Koşum 1 — tek dosya

Ham çıktı: `.calisma/T171/k2-tek-dosya.txt`

```
baslat: VidShrink.App.exe --kucult 100 "...\buyuk-1.mp4"
t= 0s  PENCERE  pid=7460  baslik=VidShrink  olcu=436x272
t=37s  PENCERE  pid=7460  baslik=VidShrink  olcu=436x290
t=44s  butun surecler cikti
--- ozet ---
acilan farkli gorunur pencere: 2
en cok es zamanli ffmpeg sureci: 2
kalan VidShrink.App sureci: 0
```

| ölçü | değer |
|---|---|
| açılan pencere (ayrı pencere tutamağı) | 1 — tek pid 7460 |
| gözlemcinin saydığı ayrı satır | 2 — aynı pencere, kodlama bitince 272 → 290 piksel yükseldi |
| ana pencere açıldı mı | hayır — 436 piksel genişlik, ana pencere ekranı kaplar |
| en çok eş zamanlı `ffmpeg` | 2 (ffprobe + kodlayıcı) |
| süreç sonunda ayakta kalan | 0 — pencere kendini kapattı (44 s) |
| üretilen çıktı | `buyuk-1_shrunk.mp4`, 98,4 MB |

Gözlemcinin özet satırındaki "2" pencere sayısı değil, **farklı başlık+ölçü çifti** sayısıdır;
her iki satırda da pid aynı. Tek pencere açıldı.

### Koşum 2 — aynı anda üç dosya

Ham çıktı: `.calisma/T171/k2-uc-dosya.txt`. Üç ayrı süreç 900 ms arayla başlatıldı.

```
baslat: VidShrink.App.exe --kucult 100 "...\buyuk-1.mp4"
baslat: VidShrink.App.exe --kucult 100 "...\buyuk-2.mp4"
baslat: VidShrink.App.exe --kucult 100 "...\buyuk-3.mp4"
t= 0s  PENCERE  pid=29636  baslik=VidShrink  olcu=436x272
t= 0s  VidShrink.App sureci: 1  ffmpeg: 1  gorunur pencere: 1
...
t=112s  butun surecler cikti
--- ozet ---
acilan farkli gorunur pencere: 2
en cok es zamanli ffmpeg sureci: 2
kalan VidShrink.App sureci: 0
```

| ölçü | değer |
|---|---|
| başlatılan süreç | 3 |
| ilk saniyeden itibaren ayakta kalan `VidShrink.App` süreci | 1 (pid 29636) — diğer ikisi isteği boruyla verip çıktı |
| açılan pencere | 1 — tek pid, yine 436 piksel |
| en çok eş zamanlı `ffmpeg` | 2 — sıra sıra kodlandı, üç kodlama aynı anda koşmadı |
| üretilen çıktı | 3: `buyuk-1_shrunk.mp4` 98,5 MB, `buyuk-2_shrunk.mp4` 98,5 MB, `buyuk-3_shrunk.mp4` 98,4 MB |
| toplam süre | 112 s |

Üç istek de tam bir kez işlendi, hiçbiri düşmedi, ikinci ve üçüncü süreç kendi kodlamasını
başlatmadı.

### `MultiSelectModel=Player` — ne doğrulandı, ne doğrulanmadı

**Doğrulanan:** kayıt defterindeki 120 komut satırının hepsinde `MultiSelectModel` değeri
`Player`, `%1` sayısı 1, `%*` yok (`.calisma/T171/k1-giris-say.txt`). Yani komut şablonu tek
dosya yolu almak üzere yazılmış.

**Ölçülmedi:** Explorer'ın `Player` kipinde gerçekten dosya başına ayrı süreç açtığı bu
ortamda doğrulanamadı — sağ menüyü betikle sürmek gerekiyordu, yapılmadı. Bu yüzden Windows'un
davranışına güvenmek yerine ters şeklin ne yaptığı da ölçüldü.

**Ters şekil ölçümü** (`.calisma/T171/k2-player-tek-surec-uc-yol.txt`): tek sürece üç yol
tek argv'de verildi.

```
komut: VidShrink.App.exe --kucult 100 buyuk-1.mp4 buyuk-2.mp4 buyuk-3.mp4
cikis kodu: 0
uretilen: buyuk-1_shrunk.mp4  98,4 MB   (buyuk-2 ve buyuk-3 icin cikti yok)
```

Bu şekilde **ilk dosya küçültülüyor, kalan ikisi sessizce düşüyor**. Sebep
`ShrinkRequestResolver.Resolve`'un tek yol döndürmesi (`src/VidShrink.Core/ShrinkRequest.cs`),
o dosya bu sözleşmede **okunacak, yazılmayacak** listesinde. Düzeltmesi çekirdek değişikliği
gerektirdiği için yazılmadı, **bildiriliyor**: `Player` kipi bozulursa ya da başka bir kabuk
tek sürece çoklu yol geçirirse kusur bu giriş noktasına kayar.

## K3 — Beş gerekçenin beşi de kullanıcıya ulaşıyor

`ShrinkJobWindow.ShowProblem` her üyeyi adıyla okuyup bir yerelleştirme anahtarına çeviriyor
(`ShrinkProblemText.Key`). Pencere kapanmıyor; tek satır gerekçe ve "Uygulamada aç" düğmesi
kalıyor. Ham çıktı: `.calisma/T171/k3-bes-cumle.txt`.

| üye | anahtar | en | tr |
|---|---|---|---|
| `NoTarget` | `main.shrink-job.reason.no-target` | The shell menu did not pass a target size after the shrink flag, so there is nothing to shrink to. | Kabuk menüsü küçültme bayrağından sonra bir hedef boyut geçirmedi; küçültülecek bir ölçü yok. |
| `TargetNotANumber` | `main.shrink-job.reason.not-a-number` | The target size that came from the shell menu is not a number, so it cannot be used as a size budget. | Kabuk menüsünden gelen hedef boyut sayı değil; boyut bütçesi olarak kullanılamıyor. |
| `TargetNotPositive` | `main.shrink-job.reason.not-positive` | The target size that came from the shell menu is zero or negative, and a file cannot be shrunk to that. | Kabuk menüsünden gelen hedef boyut sıfır ya da negatif; bir dosya o ölçüye küçültülemez. |
| `TargetNotInQuickList` | `main.shrink-job.reason.not-in-quick-list` | The target size that came from the shell menu is not one of the quick sizes (100 MB, 250 MB, 500 MB, 1 GB, 2 GB); pick one of those from the menu. | Kabuk menüsünden gelen hedef boyut hızlı boyutlardan biri değil (100 MB, 250 MB, 500 MB, 1 GB, 2 GB); menüden bunlardan birini seçin. |
| `NoPath` | `main.shrink-job.reason.no-path` | No existing file was found in the shell request, so there is nothing to open. | Kabuk isteğinde var olan bir dosya bulunamadı; açılacak bir şey yok. |

Beş üye, beş ayrı anahtar, iki dilde on ayrı cümle. Sayım ölçünün kendi çıktısından:

```
uye: 5  anahtar: 5
  NoTarget -> main.shrink-job.reason.no-target
  TargetNotANumber -> main.shrink-job.reason.not-a-number
  TargetNotPositive -> main.shrink-job.reason.not-positive
  TargetNotInQuickList -> main.shrink-job.reason.not-in-quick-list
  NoPath -> main.shrink-job.reason.no-path
```

Cümlelerin ekrana ulaştığı ayrıca pencere kurularak ölçüldü
(`KabukIstegiTests.GerekcePencereyeYazilir`, beş kol): pencere durumu `Gerekce`, `TxtMessage`
boş değil. Aynı dosyadaki çıktıda beş cümle Title Case biçimiyle basılıyor.

### Pim

Sözleşmenin dediği oldu: beş satır tüketilmeye başlayınca `OluUyeTests` kırmızıya döndü.
Düzeltmeden önceki hata: `.calisma/T171/k3-pim-once.txt`

```
uye: 162  bu dosyada adi gecmeyen: 111  pimlenen: 37
Basarisiz VidShrink.Tests.OluUyeTests.TheZeroConsumerSetIsThePinnedSet
  Assert.Equal() Failure: Collections differ  (pos 29)
  Expected: [..., "ShrinkArgumentProblem.NoPath  hic-okunmayan-tur", ...]
  Actual:   [..., "SpeedMode.Quality  varsayilan-kol", ...]
```

Beş satır ve `ShrinkProblemDebt` sabiti düşürüldü, docstring sayıları ölçünün kendi
çıktısından güncellendi. Düzeltmeden sonra: `.calisma/T171/k3-pim-sonra.txt`

```
uye: 162  bu dosyada adi gecmeyen: 116  pimlenen: 32
mesru: 10  borc: 22
Test Calistirmasi Basarili.  Toplam test sayisi: 13
```

| ölçü | önce | sonra |
|---|---|---|
| pimlenen satır | 37 | 32 |
| sıfır üretim tüketicili üye | 32 | 27 |
| hiç kullanılmayan üye | 5 | 5 |
| bu dosyada adı geçmeyen üye | 111 | 116 |
| taranan üye | 162 | 162 |

"Bu dosyada adı geçmeyen" sayısının 111'den 116'ya çıkmasının sebebi beş `ShrinkArgumentProblem`
adının artık `OluUyeTests.cs` içinde geçmemesi.

## K4 — Mutasyon

Her mutasyondan önce `dotnet build -c Release --no-incremental` koşuldu, `--no-build`
kullanılmadı. Ölçü kolu: `dotnet test --filter "ShrinkRequestTests|ShellIntegrationTests|KabukIstegiTests"`
(44 ölçü).

| mutasyon | değişiklik | sonuç | ölen ölçü | ham çıktı |
|---|---|---|---|---|
| (a) tüketen kol kaldırıldı | `Program.StartupFor(...) => null` | 2 başarısız / 42 başarılı | `KabukIstegiTests.BayrakliBaslangicIstegiCozer`, `KabukIstegiTests.BayrakliBaslangicGerekceyiTasir` | `.calisma/T171/k4-mutasyon-a.txt` |
| (b) gerekçe cümlesi susturuldu | `ShowProblem` içinde `TargetNotInQuickList` için erken `return` | 1 başarısız / 43 başarılı | `KabukIstegiTests.GerekcePencereyeYazilir(problem: TargetNotInQuickList)` | `.calisma/T171/k4-mutasyon-b.txt` |
| (c) sahiplik kapısı hep açık | `Program.OwnsQueue(...) => true` | 1 başarısız / 43 başarılı | `KabukIstegiTests.IkinciSurecKuyrugunSahibiDegil` | `.calisma/T171/k4-mutasyon-c.txt` |

Üç mutasyonun üçü de ölçü öldürdü. Mutasyonlar geri alındıktan sonra kol 44/44 yeşil.

## K5 — Kol sayısı

`--list-tests` ile sayıldı, sıfır bulan kol yok.

| filtre kolu | test sayısı | liste |
|---|---|---|
| `ShrinkRequestTests\|ShellIntegrationTests\|KabukIstegiTests` | 44 | `.calisma/T171/k5-kol1-liste.txt` |
| `OluUyeTests` | 13 | `.calisma/T171/k5-kol2-liste.txt` |

Birinci kolun dağılımı: `KabukIstegiTests` 16, `ShellIntegrationTests` 9,
`ShrinkRequestTests` 19 (`+ResolverTests` 12, `+QueueTests` 7). 16 + 9 + 19 = 44.

Ek olarak yerelleştirme ve tema koruyucuları koşuldu — yeni anahtarlar ve yeni `.axaml`
onların kapsamına giriyor: `LanguageTests|LocalizationTests|CasingTests|VisibleTextTests|ThemeTokenTests`
→ 90/90 başarılı.

## Ölçülmeyenler

- Explorer'ın `MultiSelectModel=Player` kipinde dosya başına ayrı süreç açtığı **ölçülmedi**;
  kayıt defteri şablonu doğrulandı, kabuğun kendi davranışı sürülmedi.
- Tek sürece çoklu yol gelirse ilk dosya dışındakilerin düştüğü ölçüldü ve düzeltilmedi:
  düzeltmesi `src/VidShrink.Core/ShrinkRequest.cs` değişikliği gerektiriyor, bu sözleşme
  çekirdeğe yazmıyor.
- Kurulu uygulamayla (gerçek sağ menü tıklaması) uçtan uca koşum yapılmadı; bütün koşumlar
  Release yapı ikilisi doğrudan çalıştırılarak yapıldı.
