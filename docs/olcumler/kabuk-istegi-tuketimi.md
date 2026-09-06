# Kabuk Küçültme İsteğinin Tüketimi

T171. Dal `T171-kabuk-istegi-tuketimi`. Bütün ham çıktılar `.calisma/T171/` altında.
Tur 2'de üretilen dosyalar `t2-`, tur 3'ünkiler `t3-` önekiyle durur; tur 1'in dosyaları
eski adlarıyla yerinde.

Ölçüm makinesi: Windows 11 Pro 26100, .NET 8, Release yapı
(`src/VidShrink.App/bin/Release/net8.0/VidShrink.App.exe`). Kurulu uygulama kullanılmadı.

**Tur 1 denetiminin düzelttiği sayılar bu belgede yerinde düzeltildi**; hangisinin
değiştiği "K10 — Düzeltilen borçlar" başlığında satır satır yazıyor.

## K1 — Düşen isteğin ölçüsü

### Giriş noktası sayımı

Gerçek yükleyici (`Install-VidShrink.ps1`) tek kullanımlık bir kayıt defteri köküne
(`HKCU:\Software\VidShrinkKucult-Test-<pid>-<guid>`) koşuldu, yazdığı her komut satırı okundu
ve sayıldı. Betik: `.calisma/T171/k1-giris-say.ps1`. Ham çıktı:
`.calisma/T171/k1-giris-say.txt`. Tam döküm (120 satır):
`.calisma/T171/k1-kayit-defteri-tam.tsv`. Bu tablo tur 1'de üretildi ve denetçi tarafından
kendi ayrıştırmasıyla bağımsız doğrulandı; tur 2'de değişmedi.

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

120 = 24 × 5, 144 = 120 + 24. Tek şablon:

```
"<kurulum>\VidShrink.exe" --kucult <HEDEF> "%1"
```

Kod tarafında `--kucult` taşıyan argümanı alan üç ayrı süreç girişi var; **tüketen tek
giriş** `src/VidShrink.App/Program.cs`:

| giriş | dosya | bugünkü davranışı |
|---|---|---|
| uygulama `Main` | `src/VidShrink.App/Program.cs:49` | isteği tüketiyor — bu sözleşmenin yazdığı kol |
| başlatıcı `Main` | `src/VidShrink.Launcher/Program.cs:29` | argümanları `ArgumentList` ile aynen iletiyor, tüketmiyor |
| Win11 kabuk uzantısı | `src/VidShrink.ShellExtension/VidShrink.ShellExtension.cpp:90` | `GetItemAt(0)` ile tek yol alıp `ShellExecuteW`e veriyor; küçültme alt menüsü yok (`EnumSubCommands:125-130` → `E_NOTIMPL`), `--kucult` hiç üretmiyor |

Yani düzeltilecek yer bir tane. T165'te aynı kusurun yedi girişe dağıldığı görülmüştü;
burada üç giriş sayıldı, ikisi bayrağı üretmiyor ya da yalnızca aktarıyor.

### T171 öncesi davranışın ham çıktısı

Ham çıktı: `.calisma/T171/t2-k1-bugun.txt`. Koşum güncel `gozle.ps1` ile yapıldı — tur 1'in
K1 koşumu artık üretilemeyen eski bir betik sürümüyle alınmıştı (**borç 3**). T171 öncesi
davranış, tüketen kol kapatılarak birebir üretildi: `Program.StartupFor(...) => null`
(mutasyon (a) yapısı, öncesinde `dotnet build -c Release --no-incremental`).
Komut: `VidShrink.App.exe --kucult 250 buyuk-1.mp4`.

```
t= 2s  PENCERE  pid=3044  sinif=Avalonia-632496cc-...  baslik=VidShrink  olcu=1936x1056
--- ozet ---
acilan farkli gorunur pencere (baslik+olcu cifti): 1
pencere tasiyan farkli pid: 1
en cok es zamanli ffmpeg sureci (bizim): 9
en cok es zamanli ffprobe sureci: 0
kalan VidShrink.App sureci: 1
--- cikti dokumu ---
cikti dosyasi: 0
```

Açılan pencere **1936x1056 pikseldir — ana penceredir** (`MainWindow.axaml:8-9`
`Width="1560" Height="1060" WindowState="Maximized"`). 25 saniyenin sonunda süreç hâlâ
ayakta; dokuz eş zamanlı ffmpeg ana pencerenin yoklama/önizleme düzeneğinden geliyor
(bu koşumda 46 farklı VidShrink ffmpeg komut satırı görüldü). **Üretilen çıktı dosyası
sıfır**: seçilen 250 MB hiçbir yere gitmiyor.

## K2/K7 — İstek tüketiliyor, ana pencere açılmıyor

Gözlemci `.calisma/T171/gozle.ps1`: pencereleri `EnumWindows` ile sayar, her görünür
pencerenin pid/sınıf/başlık/ölçüsünü yazar, saniyede bir canlı `VidShrink.App`, `ffmpeg` ve
`ffprobe` sayısını kaydeder, sonda çıktı klasörünü döker.

Tur 2'de gözlemciye üç şey eklendi: (1) `Win32_Process.CommandLine` ile her ffmpeg
sürecinin komut satırı, (2) komut satırında koşum klasörü geçen süreçleri `BIZIM`,
geçmeyenleri `YABANCI` sayan ayrım, (3) sonda `*_shrunk*.mp4` adı ve boyutu dökümü.

**Ayrım şart oldu:** bu makinede aynı anda başka projeler de ffmpeg koşuyor
(`Desktop\Projeler\VideoEdit\...`, `Temp\pytest-of-Teknesyum\...`). Tur 1'in
"2 eş zamanlı ffmpeg" sayısı bu yabancı süreçleri içeriyordu — bkz. K10, borç 1.

### Kaynak dosyalar

Tur 1'in kaynak dosyaları silinmişti ve sayıları hiçbir ham çıktıda yoktu (**borç 2**).
Tur 2 kaynakları yeniden üretti; `ffprobe` dökümü: `.calisma/T171/t2-kaynak-ffprobe.txt`.

| dosya | bayt | süre | çözünürlük | kare hızı | kodek | bit hızı |
|---|---|---|---|---|---|---|
| `buyuk-1.mp4` | 130 627 831 | 40,000 s | 1920x1080 | 30/1 | h264 | 26 125 566 |
| `buyuk-2.mp4` | 130 641 603 | 40,000 s | 1920x1080 | 30/1 | h264 | 26 128 320 |
| `buyuk-3.mp4` | 130 637 498 | 40,000 s | 1920x1080 | 30/1 | h264 | 26 127 499 |

Hedef 100 MB; kaynak hedefin yaklaşık 1,25 katı, yani gerçek bir kodlama koşuyor,
geçişli (pass-through) plan değil.

### Koşum 1 — tek dosya

Ham çıktı: `.calisma/T171/t2-k7-tek-dosya.txt` (74 satır).
Komut: `VidShrink.App.exe --kucult 100 "...\kosum-tek\buyuk-1.mp4"`.

| ölçü | değer | ham çıktıdaki satır |
|---|---|---|
| pencere taşıyan farklı pid | 1 (pid 10124) | `pencere tasiyan farkli pid: 1` |
| gözlemcinin saydığı ayrı başlık+ölçü çifti | 2 — `436x272` ve `436x290`, aynı pid | `acilan farkli gorunur pencere ...: 2` |
| en çok eş zamanlı `ffmpeg` (VidShrink'in kendi) | 1 | `en cok es zamanli ffmpeg sureci (bizim): 1` |
| en çok eş zamanlı `ffmpeg` (yabancı, bu ölçümle ilgisiz) | 2 | `... (yabanci, bu olcumle ilgisiz): 2` |
| en çok eş zamanlı `ffprobe` | 1 | `en cok es zamanli ffprobe sureci: 1` |
| süreç sonunda ayakta kalan | 0, t=39s'de çıktı | `kalan VidShrink.App sureci: 0` |
| üretilen çıktı | 1 — `buyuk-1_shrunk.mp4`, 103 080 918 bayt (98,31 MiB) | `--- cikti dokumu ---` |

Gözlemcinin "2" satırı pencere sayısı değil, **farklı başlık+ölçü çifti** sayısıdır;
pencere taşıyan pid sayısı ayrı basılıyor ve **1**. Yükseklik 272 → 290, kodlama bitip
`BtnReveal` görünür olduğu saniyede değişiyor.

### Koşum 2 — aynı anda üç dosya, üç ayrı süreç

Ham çıktı: `.calisma/T171/t2-k7-uc-dosya.txt` (149 satır). Üç ayrı süreç 900 ms arayla
başlatıldı.

| ölçü | değer | ham çıktıdaki satır |
|---|---|---|
| başlatılan süreç | 3 | üç `baslat:` satırı |
| aynı anda görülen `VidShrink.App` süreci | 100 örnekte 99 kez 1, 1 kez 0; hiç 2 görülmedi | `VidShrink.App sureci:` satırları |
| pencere taşıyan farklı pid | 1 (pid 16548) | `pencere tasiyan farkli pid: 1` |
| en çok eş zamanlı `ffmpeg` (kendi) | 1 — sıra sıra kodlandı | `... (bizim): 1` |
| toplam süre | 99 s | `t=99s butun surecler cikti` |
| üretilen çıktı | **3** | `--- cikti dokumu ---` |

```
cikti dosyasi: 3
  buyuk-1_shrunk.mp4  103083019 bayt  98,31 MB
  buyuk-2_shrunk.mp4  103151996 bayt  98,37 MB
  buyuk-3_shrunk.mp4  103090835 bayt  98,32 MB
```

**Üç istek de tek bir süreçte işlendi.** Bu cümlenin dayanağı süreç sayımı değil — süreç
sayımı "isteği boruyla verip çıktı" ile "çöktü"yü ayırt etmez — **üç çıktı dosyasıdır**:
ayakta kalan tek süreç (pid 16548) üç dosyanın üçünü de üretti.

Kuyruğa giren istek sayısı ayrıca bir ölçüye bağlandı
(`ShrinkJobWindow.AcceptedCount`); bkz. K6.

### `MultiSelectModel=Player` — ne doğrulandı, ne doğrulanmadı

**Doğrulanan:** kayıt defterindeki 120 komut satırının hepsinde `MultiSelectModel` değeri
`Player`, `%1` sayısı 1, `%*` yok (`.calisma/T171/k1-giris-say.txt`).

**Hâlâ ölçülmedi:** Explorer'ın `Player` kipinde gerçekten dosya başına ayrı süreç açtığı
bu ortamda doğrulanamadı — sağ menüyü betikle sürmek gerekiyordu, yapılmadı.

Tur 1 bunun tersini (tek sürece çoklu yol) ölçmüş ve kusuru bulmuştu; tur 2 kusuru
kapattı — K6.

## K6 — Tek argv'deki her yol için bir istek

Tur 1'de tek sürece `--kucult 100 buyuk-1.mp4 buyuk-2.mp4 buyuk-3.mp4` verilince ilk dosya
küçülüyor, kalan ikisi sessizce düşüyordu. T171'in kapatmak için yazıldığı kusur sınıfının
kendisiydi. **Çözüm `owns` içinde bitti, çekirdeğe yazılmadı.**

`src/VidShrink.App/Program.cs`, `ShellShrinkStartup.From`: bayraktan sonra **var olan her
yol** için ayrı bir `ShrinkRequest` üretiliyor. `ShrinkRequestResolver.Resolve` saf bir
fonksiyon olduğu için hedef bir kez ondan alınıyor; yolları toplayan `ExistingPaths` her
konumda en uzun birleşimi önce deneyip eşleşen parçaları tüketerek ilerliyor — tırnağı
kaybolmuş boşluklu yol bu yüzden bölünmüyor. `ShrinkJobWindow.Begin` gelen isteklerin
hepsini `Accept` ediyor; kuyruk (`:75`, `:155-160`, `:175-193`) zaten vardı.
`Program.Main`in sahip olmayan kolu da artık **tek değil, gelen bütün istekleri** boruyla
sahibe veriyor.

`src/VidShrink.Core/ShrinkRequest.cs`, `ShellIntegration.cs`, `EncodeRunner.cs`,
`Install-VidShrink.ps1`, `MainWindow.axaml` ve `MainWindow.axaml.cs` el değmedi.

### Gerçek koşum

Ham çıktı: `.calisma/T171/t2-k6-tek-surec-uc-yol.txt` (152 satır). **Tek süreç, tek argv,
üç yol.**

| ölçü | değer |
|---|---|
| başlatılan süreç | 1 |
| pencere taşıyan farklı pid | 1 (pid 6964) |
| en çok eş zamanlı `ffmpeg` (kendi) | 1 |
| toplam süre | 103 s |
| üretilen çıktı | **3** |

```
cikti dosyasi: 3
  buyuk-1_shrunk.mp4  103074177 bayt  98,3 MB
  buyuk-2_shrunk.mp4  103063054 bayt  98,29 MB
  buyuk-3_shrunk.mp4  103098372 bayt  98,32 MB
```

**Ölçüler.** İkisi de `tests/VidShrink.Tests/KabukIstegiTests.cs` içinde; ham çıktı
`.calisma/T171/t2-k3-bes-cumle.txt`:

| ölçü | ne tutuyor | çıktısı |
|---|---|---|
| `TekArgvdekiUcYolUcIstekUretir` | üç yol → üç istek; sıra ve hedef korunur | `argv yolu: 3  uretilen istek: 3` |
| `PencereUcIstegiDeKabulEder` | pencerenin saydığı `AcceptedCount` | `AcceptedCount: 3` |

## K3 — Beş gerekçenin beşi de kullanıcıya ulaşıyor

`ShrinkJobWindow.ShowProblem` her üyeyi adıyla okuyup bir yerelleştirme anahtarına çeviriyor
(`ShrinkProblemText.Key`). Pencere kapanmıyor; tek satır gerekçe ve "Uygulamada aç" düğmesi
kalıyor. Ham çıktı: `.calisma/T171/t2-k3-bes-cumle.txt`.

| üye | anahtar | en | tr |
|---|---|---|---|
| `NoTarget` | `main.shrink-job.reason.no-target` | The shell menu did not pass a target size after the shrink flag, so there is nothing to shrink to. | Kabuk menüsü küçültme bayrağından sonra bir hedef boyut geçirmedi; küçültülecek bir ölçü yok. |
| `TargetNotANumber` | `main.shrink-job.reason.not-a-number` | The target size that came from the shell menu is not a number, so it cannot be used as a size budget. | Kabuk menüsünden gelen hedef boyut sayı değil; boyut bütçesi olarak kullanılamıyor. |
| `TargetNotPositive` | `main.shrink-job.reason.not-positive` | The target size that came from the shell menu is zero or negative, and a file cannot be shrunk to that. | Kabuk menüsünden gelen hedef boyut sıfır ya da negatif; bir dosya o ölçüye küçültülemez. |
| `TargetNotInQuickList` | `main.shrink-job.reason.not-in-quick-list` | The target size that came from the shell menu is not one of the quick sizes (100 MB, 250 MB, 500 MB, 1 GB, 2 GB); pick one of those from the menu. | Kabuk menüsünden gelen hedef boyut hızlı boyutlardan biri değil (100 MB, 250 MB, 500 MB, 1 GB, 2 GB); menüden bunlardan birini seçin. |
| `NoPath` | `main.shrink-job.reason.no-path` | No existing file was found in the shell request, so there is nothing to open. | Kabuk isteğinde var olan bir dosya bulunamadı; açılacak bir şey yok. |

Beş üye, beş ayrı anahtar. Sayım ölçünün kendi çıktısından
(`BesGerekceninHepsiAyriAnahtaraGider`):

```
uye: 5  anahtar: 5
  NoTarget -> main.shrink-job.reason.no-target
  TargetNotANumber -> main.shrink-job.reason.not-a-number
  TargetNotPositive -> main.shrink-job.reason.not-positive
  TargetNotInQuickList -> main.shrink-job.reason.not-in-quick-list
  NoPath -> main.shrink-job.reason.no-path
```

### K9 — Cümlelerin ekrana ulaştığı nasıl tutuluyor

Tur 1'in ölçüsü `Assert.False(string.IsNullOrWhiteSpace(...))` idi: **beş gerekçenin hepsi
aynı cümleyi bassa ölmüyordu** (borç 5). Tur 2'de iki yere sıkılaştı:

- `GerekcePencereyeYazilir` artık **eşitlik** kuruyor. Beklenen anahtar ölçünün kendi
  içinde harfi harfine yazılı (`BeklenenAnahtar`), böylece üretimdeki
  `ShrinkProblemText.Key` değişirse beklenti onunla birlikte kaymıyor; pencerenin bastığı
  metin o anahtarın karşılığına eşit olmalı.
- Yeni `BesGerekceBesAyriCumleBasar`: beş pencere kurulup basılan cümleler toplanıyor,
  **ayrı cümle sayısı 5** olmalı. Çıktısı:

```
gerekce: 5  ayri cumle: 5
  NoTarget -> Kabuk Menüsü Küçültme Bayrağından Sonra Bir Hedef Boyut Geçirmedi; Küçültülecek Bir Ölçü Yok.
  TargetNotANumber -> Kabuk Menüsünden Gelen Hedef Boyut Sayı Değil; Boyut Bütçesi Olarak Kullanılamıyor.
  TargetNotPositive -> Kabuk Menüsünden Gelen Hedef Boyut Sıfır Ya da Negatif; Bir Dosya O Ölçüye Küçültülemez.
  TargetNotInQuickList -> Kabuk Menüsünden Gelen Hedef Boyut Hızlı Boyutlardan Biri Değil (100 MB, 250 MB, 500 MB, 1 GB, 2 GB); Menüden Bunlardan Birini Seçin.
  NoPath -> Kabuk İsteğinde Var Olan Bir Dosya Bulunamadı; Açılacak Bir Şey Yok.
```

Sıkılaşmanın işe yaradığı mutasyonla gösterildi — aşağıdaki ızgarada (g) satırı.

### Pim

Beş satır tüketilmeye başlayınca `OluUyeTests` kırmızıya dönmüştü; tur 1 pimi düzeltti.
Pim tur 3'te ayrıntılı günlükle yeniden koşuldu ve sayılar aynı çıktı —
`.calisma/T171/t3-k15-pim.txt`, satır 45, 104-105 ve 776 (13/13 geçti):

```
uye: 162  bu dosyada adi gecmeyen: 116  pimlenen: 32
dosya: 76  uye: 162
sifir tuketici: 27  hic kullanilmayan: 5
mesru: 10  borc: 22
```

| ölçü | T171 öncesi | bugün |
|---|---|---|
| pimlenen satır | 37 | 32 |
| sıfır üretim tüketicili üye | 32 | 27 |
| hiç kullanılmayan üye | 5 | 5 |
| bu dosyada adı geçmeyen üye | 111 | 116 |
| taranan üye | 162 | 162 |

Tur 2'nin kod değişikliği (`ShellShrinkStartup.Items`, `App.StartupWindow`) hiçbir tür
üyesi eklemedi ya da düşürmedi; pim satırı birebir aynı kaldı.

## K8 — Ana pencerenin açılmadığını tutan ölçü

Borç 4: tur 1 "ana pencere açılmadı" gerekçesini yalnızca 436 piksel genişliğe
dayandırmıştı ve `App.axaml.cs`teki kolu tutan hiçbir ölçü yoktu.

`App.axaml.cs`te başlangıç penceresini seçen kol ayrı bir üyeye çıkarıldı:

```csharp
internal Window StartupWindow()
    => _shrink is not null
        ? new ShrinkJobWindow(_shrink, _queue)
        : new MainWindow(_startupFile);
```

Ölçü `KabukIstegiTests.KabukIstegiAnaPencereyiAcmaz`: kabuk isteğiyle kurulan `App`in
döndürdüğü pencerenin **türü** `ShrinkJobWindow` olmalı, `MainWindow` olmamalı. Piksele
bakmıyor. Çıktısı (`.calisma/T171/t2-k3-bes-cumle.txt`):

```
acilan pencere turu: ShrinkJobWindow
```

Kolu bozan mutasyon (e) bu ölçüyü düşürdü.

## K4 — Mutasyon

Her mutasyondan önce `dotnet build -c Release --no-incremental` koşuldu; `--no-build`
kullanılmadı. Ölçü kolu:
`dotnet test --filter "ShrinkRequestTests|ShellIntegrationTests|KabukIstegiTests"` (48 ölçü).

| mutasyon | değişiklik | sonuç | ölen ölçü | ham çıktı |
|---|---|---|---|---|
| (a) tüketen kol kaldırıldı | `Program.StartupFor(...) => null` | 5 başarısız / 43 başarılı | `BayrakliBaslangicIstegiCozer`, `BayrakliBaslangicGerekceyiTasir`, `TekArgvdekiUcYolUcIstekUretir`, `PencereUcIstegiDeKabulEder`, `KabukIstegiAnaPencereyiAcmaz` | `t2-k4-mutasyon-a.txt` |
| (b) gerekçe cümlesi susturuldu | `ShowProblem` içinde `TargetNotInQuickList` için erken `return` | 1 / 47 | `GerekcePencereyeYazilir(TargetNotInQuickList)` | `t2-k4-mutasyon-b.txt` |
| (c) sahiplik kapısı hep açık | `Program.OwnsQueue(...) => true` | 1 / 47 | `IkinciSurecKuyrugunSahibiDegil` | `t2-k4-mutasyon-c.txt` |
| (d) yalnız ilk yol tüketilsin | `ExistingPaths` döngüsüne `break` | 2 / 46 | `TekArgvdekiUcYolUcIstekUretir`, `PencereUcIstegiDeKabulEder` | `t2-k4-mutasyon-d.txt` |
| (e) kabuk kolu ana pencereye dönsün | `StartupWindow() => new MainWindow(...)` | 1 / 47 | `KabukIstegiAnaPencereyiAcmaz` | `t2-k4-mutasyon-e.txt` |
| (f) iki gerekçe aynı anahtara | `TargetNotPositive => NoTarget` | 3 / 45 | `BesGerekceninHepsiAyriAnahtaraGider`, `GerekcePencereyeYazilir(TargetNotPositive)`, `BesGerekceBesAyriCumleBasar` | `t2-k4-mutasyon-f.txt` |
| (g) beş gerekçe de aynı cümleyi bassın | `ShowProblem` içinde `TxtMessage.Text = Say(ShrinkProblemText.NoTarget)` | 5 / 43 | `GerekcePencereyeYazilir` dört kolu (`TargetNotANumber`, `TargetNotPositive`, `TargetNotInQuickList`, `NoPath`) ve `BesGerekceBesAyriCumleBasar` | `t2-k4-mutasyon-g.txt` |

Yedi mutasyonun yedisi de ölçü öldürdü. Mutasyonlar geri alınıp temiz Release yapı
kurulduktan sonra kol **48/48** yeşil (`.calisma/T171/t2-k5-kol1-kosum.txt`) — **bu tek bir
koşumun ifadesidir**. Tur 2 denetimi aynı kolun kararsız olduğunu gösterdi (aşağıda K11);
bu ızgaranın sayıları o gürültüyü taşıyor, ızgara tur 3'te yeniden koşuldu.

(d) tur 2'nin kapattığı kusuru, (e) borç 4'ü, (g) borç 5'i doğrudan tutuyor. **(g) tur 1'in
gevşek ölçüsünde hiçbir şey öldürmezdi**: beş cümle de boş olmayan bir metin basıyor ve
durum yine `Gerekce` oluyordu.

## K5 — Kol sayısı

`--list-tests` ile sayıldı, sıfır bulan kol yok.

| filtre kolu | test sayısı | liste | koşum |
|---|---|---|---|
| `ShrinkRequestTests\|ShellIntegrationTests\|KabukIstegiTests` | 48 | `t2-k5-kol1-liste.txt` | 48/48 (`t2-k5-kol1-kosum.txt`) |
| `OluUyeTests` | 13 | `t2-k5-kol2-liste.txt` | 13/13 (`t2-k5-kol2-kosum.txt`) |

Birinci kolun dağılımı, listenin kendi sayımından: `KabukIstegiTests` 20,
`ShellIntegrationTests` 9, `ShrinkRequestTests+ResolverTests` 12,
`ShrinkRequestTests+QueueTests` 7. 20 + 9 + 12 + 7 = 48. Tur 1'de 44'tü; tur 2'nin eklediği
dört ölçü (`TekArgvdekiUcYolUcIstekUretir`, `PencereUcIstegiDeKabulEder`,
`KabukIstegiAnaPencereyiAcmaz`, `BesGerekceBesAyriCumleBasar`) farkı veriyor.

Ek olarak yerelleştirme ve tema koruyucuları koşuldu:
`LanguageTests|LocalizationTests|CasingTests|VisibleTextTests|ThemeTokenTests`
→ **90/90** başarılı (`.calisma/T171/t2-koruyucular.txt`).

## K10 — Düzeltilen borçlar

**Borç 1 — "2 (ffprobe + kodlayıcı)" açıklaması yanlıştı.** `gozle.ps1` `ffmpeg` süreçlerini
sayıyor; `ffprobe` ayrı bir süreç adıdır ve o sayıya hiç girmez. Tur 2 komut satırlarını da
kaydedince gerçek sebep çıktı ve **tahmin edilenden de başkaydı**: ikinci ffmpeg
`ComplexityProbe`/`CalibrationProbe` değil, **bu makinede koşan başka projelerin ffmpeg
süreçleriydi** (`Desktop\Projeler\VideoEdit\...`, `Temp\pytest-of-Teknesyum\...`).
`.calisma/T171/t2-k7-tek-dosya.txt` bunu satır satır gösteriyor: 6 farklı ffmpeg komut
satırının **1'i** `BIZIM`, **5'i** `YABANCI`. VidShrink'in kendi eş zamanlı ffmpeg sayısı
tur 2'nin üç koşumunda da **1**; ama o koşumlardaki `gozle.ps1` komut satırı okunamayan bir
ffmpeg'i sessizce `YABANCI` kovasına atıyordu, bu yüzden **tur 2'nin bu sayısı bir alt
sınırdır**. Betik tur 3'te ayrı bir `BILINMEYEN` kovası kazandı ve koşumlar yenilendi;
kesin/alt sınır ayrımı artık ham çıktının kendisinde yazıyor (aşağıda K15 borç 6).

Statik sayım da yapıldı (`.calisma/T171/t2-borc1-ffmpeg-cagri-satirlari.txt`): `src/**`
altında `ToolLocator.StartInfo(ToolLocator.Ffmpeg, ...)` ile ffmpeg başlatan **20 satır**
var, 12 dosyaya dağılmış — `EncoderCapabilities` 4, `ComplexityProbe` 3,
`Playback/DecoderPipe` 3, `QualityMeter` 2, ve `CalibrationProbe`, `EncodeRunner`,
`FfmpegRunner`, `FrameGrabber`, `PerformanceProbe`, `Playback/ComparisonGraph`,
`Playback/PipeComparisonFrameSource`, `SceneDetector` 1'er. Kabuk isteği koşumunda
bunlardan **çalıştığı görülen tek satır** `EncodeRunner.cs:365`
(`-progress pipe:1 -nostats -hide_banner -y -hwaccel auto`).

**Bu ölçümün bir sınırı:** komut satırları günlüğe 260 karaktere kırpılarak yazılıyor. Üç
kaynağın yolu 260. karakterden sonra ayrıştığı için üç ayrı kodlama günlükte **tek** komut
satırı anahtarına düşüyor. Yani "1 farklı `BIZIM` komut satırı" cümlesi üç kodlamanın tek
olduğunu söylemez; üç kodlamanın ayırt edici delili **üç çıktı dosyasıdır**.

**Borç 2 — kaynak dosya sayıları kaynaksızdı.** Yukarıdaki `ffprobe` tablosu ve
`.calisma/T171/t2-kaynak-ffprobe.txt`. Tur 1'in kaynakları silinmiş olduğu için o koşumun
`129,735 MB` sayısı **geri getirilemedi**; tur 2 kendi kaynaklarını üretip ölçtü.

**Borç 3 — K1'in gözlemci betiği korunmamıştı.** K1 koşumu güncel `gozle.ps1` ile yeniden
koşuldu: `.calisma/T171/t2-k1-bugun.txt`. Artık K1 ile K2/K6/K7 aynı betiğin aynı
sürümünden çıkıyor.

**Borç 4 — ana pencere ölçüsü yoktu.** K8, yukarıda.

**Borç 5 — `KabukIstegiTests` gerekçe ölçüsü gevşekti.** K9, yukarıda; mutasyon (g) ile
gösterildi.

**Borç 6 — artık dosyalar.** `.calisma/T171/cikti.txt` (0 bayt) ve `hata.txt` (bugünkü koda
karşılığı olmayan bir geliştirme çökmesi) `trash/T171/` altına taşındı.

## Tur 3 — K11-K15

Bu turun bütün ham çıktıları `t3-` önekli. Kod tur 3'e `c6604cb` ile geldi; K11-K14'ün
kodu önceki (durdurulan) yapıcı tarafından yazılmış ama teslim edilen bir ölçüsü yoktu.
Aşağıdaki sayıların **hepsi bu turda yeniden üretildi**; hiçbiri devralınan bir dosyadan
alınmadı.

**Devralınan ham dosyalar.** Durdurulan yapıcı `.calisma/T171/` altında on tane `t3-`
dosyası bırakmıştı. Hiçbiri bu belgede kaynak gösterilmiyor ve karışmasınlar diye
`.calisma/T171/devralinan/` altına alındılar. En az biri kendi künyesiyle çelişiyor —
`devralinan/t3-k6-k13-tek-surec.txt` başlığında *"uc var olan yol + bir bulunamayan yol"*
diyor, başlatma satırı ise `baslat (tek surec, 1 yol)` ve dökümde tek çıktı var. Ayrıca
`devralinan/t3-k4-taban-son.txt` `git status` satırında ağacın kirli olduğunu gösteriyor
(`M tests/VidShrink.Tests/KabukIstegiTests.cs`), yani o ızgaranın tabanı teslim ağacı
değil. Bu yüzden o koşumların hiçbiri kabul edilmedi.

### K11 — Kararsız ölçü kapandı

Kusur: `GerekcePencereyeYazilir` beklenen metni test iş parçacığında süreç geneli
`Strings.Get` ile hesaplıyor, görülen metni arayüz iş parçacığında okuyordu. Pencere
dili (teslim commit'i `3618cf8`te `ShrinkJobWindow.axaml.cs:96`) süreç genelindeki dili
değiştirdiği için iki taraf farklı dile düşebiliyordu.

Çözüm: pencere dili kuruluşta bir kez çözülüp `ShrinkJobWindow.Language`e sabitlendi
(`ShrinkJobWindow.axaml.cs:98,103,125`), `Say` artık `Strings.GetIn(_language, ...)` okuyor
(`:353-357`). Ölçü beklenen metni **o örneğin dilinden** hesaplıyor
(`KabukIstegiTests.cs:127-150` ve `:156-169`).

Pencere dilinin koşumdan koşuma gerçekten değiştiği ham çıktıda görülüyor: temiz ağaçta
`pencere dili: tr` (`.calisma/T171/t3-k13-k14-sonda.txt`), mutasyon (i) koşumunda
`pencere dili: en` (`.calisma/T171/t3-k4-mutasyon-i.txt:39`). Sebep `UpdateSettings.Load()`
— sıra bağımlı. Ölçü artık buna bakmadığı için bu değişkenlik onu düşürmüyor.

**CHECK — 10 koşum.** `dotnet test -c Release --no-build --filter
"KabukIstegiTests|LocalizationTests|LanguageTests"`, arka arkaya, her biri ayrı çağrıda.
Onunun da ham çıktısı tek dosyada: `.calisma/T171/t3-k11-kararlilik.txt`.

| koşum | sonuç | dosyadaki satır |
|---|---|---|
| 1 | 98/98 başarılı, 0 başarısız | 8 |
| 2 | 98/98 | 17 |
| 3 | 98/98 | 26 |
| 4 | 98/98 | 35 |
| 5 | 98/98 | 44 |
| 6 | 98/98 | 53 |
| 7 | 98/98 | 62 |
| 8 | 98/98 | 71 |
| 9 | 98/98 | 80 |
| 10 | 98/98 | 89 |

On koşumun onunda da `exit=0`. Izgara koşulduktan sonra temiz yapıyla bir **11. doğrulama
koşumu** daha yapıldı, o da 98/98 (aynı dosya, satır 115). Bu yüzden dosyadaki
`grep -c "^exit=0"` **11** verir: 10 kararlılık koşumu + 1 ızgara sonrası doğrulama.

Kolun dolu olduğu `--list-tests` ile ayrıca sayıldı (`.calisma/T171/t3-k11-list.txt`):
`KabukIstegiTests` 27, `LanguageTests` 56, `LocalizationTests` 15 — toplam **98**, sıfır
eşleşen kol yok.

**Aynı desen başka ölçülerde.** `Strings.Get`/`Strings.Use`i süreç genelinden okuyan test
kolları arandı. `tests/` altında kalan tek benzer yer `WindowLayoutTests.cs:867`
(`Strings.Get("main.tab.advanced")` test iş parçacığında, başlıklar arayüz iş parçacığında
toplanmışken). Bugün kırmızıya dönmüyor — paralellik kapalı (`LanguageTests.cs:17`,
`[assembly: CollectionBehavior(DisableTestParallelization = true)]`) ve `MainWindow`
kuruluşta dili değiştirmiyor. **`WindowLayoutTests.cs` bu sözleşmenin `owns` listesinde
değil; dokunulmadı, T0'a bildiriliyor.**

### K12 — Kısmî teslimde çifte kodlama

`Program.Handoff` (`Program.cs:155-171`, gövde `:161-171`) eklendi: bayrak istek başına. Teslim edilen istek
geri dönmez, geriye yalnız teslim edilemeyenler kalır; hepsi teslim edildiyse `null` döner
ve süreç pencere açmadan çıkar.

**CHECK — önce/sonra.** Mutasyon (h) `Handoff`u eski tek-bayrak anlamına geri çevirir
(`startup with { Requests = leftovers }` → `startup`). Ham çıktı
`.calisma/T171/t3-k4-mutasyon-h.txt`:

| | denenen | geri kalan |
|---|---|---|
| eski davranış (mutasyon h, dosya satır 39-40) | `a.mp4, b.mp4, c.mp4` | `a.mp4, b.mp4, c.mp4` |
| bugünkü davranış (`t3-k13-k14-sonda.txt`) | `a.mp4, b.mp4, c.mp4` | `b.mp4` |

Eski kolda `a.mp4` sahibe teslim edilmiş **ve** bu süreçte yeniden kodlanacaktı. Ölçü adı:
`KabukIstegiTests.TeslimEdilenIstekGeriDonmez`; mutasyon (h) altında 1/54 ile tek başına
düşüyor.

### K13 — Bulunamayan yol sessizce düşmüyor

`ShellShrinkStartup.ScanPaths` artık iki kova döndürüyor (`Program.cs:69-70` kayıt tipi,
`:73-104` tarama); bulunamayan
parçalar `Missing` içinde durur ve `ShrinkJobWindow.ShowMissing`
(`ShrinkJobWindow.axaml.cs:175-186`) bunları `TxtNotice` satırına yazar. Docstring de
gerçekle hizalandı (`Program.cs:31-39`).

**CHECK — sonda.** Ham çıktı `.calisma/T171/t3-k13-k14-sonda.txt`. Gerçek dosyalarla
kurulan geçici dizinde `--kucult 100 <a.mp4> <yok.mp4> <b.mp4>`:

```
istek: 2  bulunamayan: 1
  eksik: C:\Users\Teknesyum\AppData\Local\Temp\vidshrink-t171-b25ef53094e34a30bfc01295df265947\yok.mp4
```

Kullanıcıya giden tam metin, iki dilde (aynı dosyadan):

```
en: 1 of the paths in the shell request were not found on disk and were skipped: yok.mp4
tr: Kabuk isteğindeki 1 yol diskte bulunamadı ve atlandı: yok.mp4
```

Pencereye gerçekten basılan hali (Title Case, depo geneli biçim — borç 7 hâlâ açık):

```
dil: tr
  Kabuk İsteğindeki 1 Yol Diskte Bulunamadı ve Atlandı: C:\...\yok.mp4
```

Ölçü adları: `BulunamayanYolSessizceDusmez`, `BulunamayanYolPencereyeYazilir`,
`BulunamayanYolCumlesiIkiDilde`. Mutasyon (j) `Missing` kovasını boşaltır ve
`BulunamayanYolSessizceDusmez` tek başına düşer (1/54,
`.calisma/T171/t3-k4-mutasyon-j.txt`).

**Ölçülmedi:** kurulu uygulamayla, gerçek sağ menü tıklamasıyla uçtan uca sonda yine
yapılmadı. Sonda `Program.StartupFor` ve `ShrinkJobWindow` seviyesindedir.

### K14 — Sezgiselin bedeli

En-uzun-eşleşme sezgiseli göreli adlarda yanlış dosya seçebiliyor. Bedel artık ölçüyle
**yazılı**: `KabukIstegiTests.SezgiselGoreliAdlardaYanlisDosyayiSecebilir`. Ham çıktı
`.calisma/T171/t3-k13-k14-sonda.txt`:

```
goreli adlar -> bulunan: a.mp4 b.mp4  eksik: 0
mutlak yollar -> bulunan: C:\Klip\a.mp4 | C:\Klip\b.mp4
```

Yani dizinde `a.mp4`, `b.mp4` ve adı `a.mp4 b.mp4` olan üçüncü bir dosya varken göreli
adlarla gelen iki istek tek yanlış isteğe çöküyor; mutlak yollarla gelince iki istek de
doğru çıkıyor.

**Üretimde erişilemez olduğunun kanıtı.** Kayıt defteri şablonu her zaman tırnaklı `"%1"`
veriyor — `Install-VidShrink.ps1:331`:

```
Set-Item -LiteralPath $command -Value ('"{0}" {1} {2} "%1"' -f $Executable, $shellShrinkFlag, $target)
```

Bu makinede kurulu gerçek değerler de okundu (`.calisma/T171/t3-k14-uretim-yolu.txt`),
beş hedefin beşi de mutlak yol taşıyor:

```
HKCU\...\SystemFileAssociations\.mp4\shell\VidShrinkKucult\shell\100\command
  (Default) = "C:\Users\Teknesyum\AppData\Local\Programs\VidShrink\VidShrink.exe" --kucult 100 "%1"
```

`%1` Explorer tarafından tam nitelikli yola açılır; birleştirilen aday o zaman
`C:\Klip\a.mp4 C:\Klip\b.mp4` olur ve Windows'ta böyle bir dosya adı kurulamaz (içinde
`:` ve dizin ayıracı var). **Karar: sezgisel düzeltilmedi, bedeli yazıldı** — tırnağı
kaybolmuş boşluklu yolu geri toplamak (K1'in `UnquotedPathBrokenIntoThreePieces` kolu)
üretimde gerçekten oluyor; yanlış seçim üretimde oluşamıyor.

### K15 — Borç temizliği

**Borç 1 (pim künyesi).** Pim tur 3'te gerçekten koşuldu, kendi ham dosyasına yazıldı:
`.calisma/T171/t3-k15-pim.txt` (13/13 geçti). Yukarıdaki "Pim" bloğunun künyesi bu dosyaya
ve satır numaralarına çevrildi.

**Borç 2 (yapı kanıtı).** On mutasyon dosyasının **onunda da** `dotnet build -c Release
--no-incremental` çıktısı var; her birinde `=== dotnet build -c Release --no-incremental ===`
başlığı ve `Oluşturma başarılı oldu` satırı bulunuyor (dosya başına birer kez).

**Borç 6 (`gozle.ps1` kovası).** Betik artık komut satırı okunamayan ffmpeg'i ayrı bir
`BILINMEYEN` kovasında sayıyor ve özet satırı kesin mi alt sınır mı olduğunu **kendisi**
söylüyor. K7 ve K6 yeni betikle yeniden koşuldu:

| koşum | bizim | yabancı | bilinmeyen | özetin dediği | ham çıktı |
|---|---|---|---|---|---|
| K7 tek dosya | 1 | 0 | 0 | `'bizim' sayisi KESIN` | `t3-k7-tek-dosya.txt` |
| K6 tek süreç tek argv üç yol | 1 | 0 | 1 | `'bizim' sayisi ALT SINIR` | `t3-k6-tek-surec-uc-yol.txt` |

Çıktı baytları (aynı dosyalardan):

| koşum | çıktı | bayt |
|---|---|---|
| K7 | `buyuk-1_shrunk.mp4` | 103082515 |
| K6 | `buyuk-1_shrunk.mp4` | 103034935 |
| K6 | `buyuk-2_shrunk.mp4` | 103234937 |
| K6 | `buyuk-3_shrunk.mp4` | 103130742 |

K6'da tek görünür pencere pid'i (`pid: 9336`), üç çıktı, en çok eş zamanlı kendi ffmpeg'i
**1**, süre `t=100s`.

**Borç 7 (`trash/T171/` içindeki iki dosya).** `owns` dışında; **dokunulmadı**, T0'a
bırakıldı.

### K4 ızgarası — tur 3'te yeniden

Ölçü kolu: `dotnet test -c Release --no-build --filter
"ShrinkRequestTests|ShellIntegrationTests|KabukIstegiTests"` — bugün **55** ölçü
(`.calisma/T171/t3-verify-kollar.txt`: `KabukIstegiTests` 27, `ShellIntegrationTests` 9,
`ShrinkRequestTests+ResolverTests` 12, `ShrinkRequestTests+QueueTests` 7; 27+9+12+7 = 55).
Temiz ağaçta kol **55/55** yeşil, ikinci `verify` kolu `OluUyeTests` **13/13** (aynı dosya).

Her mutasyon ayrı bir çağrıda uygulandı, öncesinde `dotnet build -c Release
--no-incremental` koşuldu ve **yapı çıktısı ham dosyaya girdi**; sonrasında `git checkout
-- src/` ile geri alındı. Mutasyon betiği: `.calisma/T171/mutasyon.py` (desen tam olarak
bir kez bulunmazsa uygulamayı reddeder).

| mutasyon | değişiklik | sonuç | ölen ölçü | ham çıktı |
|---|---|---|---|---|
| (a) tüketen kol kaldırıldı | `Program.StartupFor(...) => null` | 6 / 49 | `BayrakliBaslangicIstegiCozer`, `BayrakliBaslangicGerekceyiTasir`, `TekArgvdekiUcYolUcIstekUretir`, `PencereUcIstegiDeKabulEder`, `KabukIstegiAnaPencereyiAcmaz`, `BulunamayanYolSessizceDusmez` | `t3-k4-mutasyon-a.txt` |
| (b) gerekçe cümlesi susturuldu | `ShowProblem` içinde `TargetNotInQuickList` için erken `return` | 3 / 52 | `GerekcePencereyeYazilir(TargetNotInQuickList)`, `GerekceOlcusuSurecDilindenEtkilenmez` iki kolu (`en`, `tr`) | `t3-k4-mutasyon-b.txt` |
| (c) sahiplik kapısı hep açık | `Program.OwnsQueue(...) => true` | 1 / 54 | `IkinciSurecKuyrugunSahibiDegil` | `t3-k4-mutasyon-c.txt` |
| (d) yalnız ilk yol tüketilsin | `ScanPaths` döngüsünde `if (advanced) continue` → `break` | 4 / 51 | `TekArgvdekiUcYolUcIstekUretir`, `PencereUcIstegiDeKabulEder`, `BulunamayanYolSessizceDusmez`, `SezgiselGoreliAdlardaYanlisDosyayiSecebilir` | `t3-k4-mutasyon-d.txt` |
| (e) kabuk kolu ana pencereye dönsün | `StartupWindow() => new MainWindow(...)` | 1 / 54 | `KabukIstegiAnaPencereyiAcmaz` | `t3-k4-mutasyon-e.txt` |
| (f) iki gerekçe aynı anahtara | `TargetNotPositive => NoTarget` | 3 / 52 | `BesGerekceninHepsiAyriAnahtaraGider`, `GerekcePencereyeYazilir(TargetNotPositive)`, `BesGerekceBesAyriCumleBasar` | `t3-k4-mutasyon-f.txt` |
| (g) beş gerekçe de aynı cümleyi bassın | `ShowProblem` içinde `TxtMessage.Text = Say(ShrinkProblemText.NoTarget)` | 7 / 48 | `GerekcePencereyeYazilir` dört kolu (`TargetNotANumber`, `TargetNotPositive`, `TargetNotInQuickList`, `NoPath`), `GerekceOlcusuSurecDilindenEtkilenmez` iki kolu, `BesGerekceBesAyriCumleBasar` | `t3-k4-mutasyon-g.txt` |
| (h) teslim bayrağı yine tek | `startup with { Requests = leftovers }` → `startup` | 1 / 54 | `TeslimEdilenIstekGeriDonmez` | `t3-k4-mutasyon-h.txt` |
| (i) pencere dili yine süreç geneli | `Say` → `LanguageCatalog.Display(Strings.Get(...))` | 1 / 54 | `GerekceOlcusuSurecDilindenEtkilenmez(tr)` | `t3-k4-mutasyon-i.txt` |
| (j) bulunamayan yol yine sessiz | `ScanPaths` içinde `missing.Add(token)` kaldırıldı | 1 / 54 | `BulunamayanYolSessizceDusmez` | `t3-k4-mutasyon-j.txt` |

On mutasyonun onu da ölçü öldürdü ve **hiçbirinde rastgele düşen kol görülmedi** — tur 2'nin
ızgarasındaki gürültü kapandı. (a)-(g) tur 2'nin ızgarasının aynısı; (h), (i), (j) sırasıyla
K12, K11 ve K13'ün gerileme kolları.

Sayı farkları tur 2'ye göre iki sebepten: kolun toplamı 48 → 55 (yedi yeni ölçü) ve tur 2'de
kararsız olan `GerekcePencereyeYazilir` artık deterministik.

## Kapanmayanlar

- **Borç 7 (kapsam dışı, sessizce geçilmiyor):** beş gerekçe cümlesi
  `LanguageCatalog.Display` (`LanguageCatalog.cs:171-172`) üzerinden geçtiği için **iki
  dilde de** tam cümle Title Case basılıyor — yukarıdaki Türkçe çıktı bunu gösteriyor
  (*"Kabuk Menüsü Küçültme Bayrağından Sonra..."*). Depo geneli böyle ve `CasingTests`
  yeşil; ama gövde metni için beş uzun cümle bu biçimi ilk kez görünür kılıyor. Bu
  sözleşme `LanguageCatalog`a dokunmuyor.
- Explorer'ın `MultiSelectModel=Player` kipinde dosya başına ayrı süreç açtığı
  **ölçülmedi**; kayıt defteri şablonu doğrulandı, kabuğun kendi davranışı sürülmedi.
  Kusurun bu varsayıma bağımlılığı artık yok: tek argv'de çoklu yol gelse de hiçbir istek
  düşmüyor (K6).
- Kurulu uygulamayla (gerçek sağ menü tıklaması) uçtan uca koşum yapılmadı; bütün koşumlar
  Release yapı ikilisi doğrudan çalıştırılarak yapıldı.
- Tur 1'in K2 koşumlarının çıktı dosyaları geri getirilemedi. Bu yüzden `98,5 / 98,5 /
  98,4 MB` üçlüsü ve tek dosya koşumundaki `98,4 MB` bu belgeden **çıkarıldı**; yerlerine
  tur 2'nin ham dökümünden gelen sayılar kondu.
