# Kanıt Kapanışı: Kalan İki Borç

Dal: `t0/kanit-borclari`. Makine: Windows 11, .NET 8, `VIDSHRINK_LIBMPV` =
`.calisma\libmpv\libmpv-2.dll`.

Kural: kanıt dosyasını silen çağrı **son asertten sonra** durur. Yeşil koşum kendi
bıraktığını siler, kırmızı koşum kanıtını korur (düşen asert o satıra gelmez), klasör
boşalınca o da gider. `finally` kullanılmadı.

## Borç A — `.calisma/T176` kapanışı

`OynaticiGirdiTests.cs` içinde aynı `T176` klasörüne yazan ama süpürmenin listesine
girmemiş sınıflara kural uygulandı:

| Sınıf | Kapatılan kanıt |
| --- | --- |
| `PlayerTabTests` | `k1-sekme.txt` |
| `OynaticiGirdiTestsKlipUretimi` | `k4-klip-uretimi.txt`, `kalip-kanit-20sn.mkv` |
| `OynaticiGirdiTestsMenuSatirlari` | `k9-menu-oynat.txt`, `k9-menu-tam-ekran.txt`, `k9-menu-sifirla.txt`, `k9-menu-satirlari.txt`, `k13-olcum-kancasi.txt` |
| `OynaticiGirdiTestsPencereYazma` | `k16-pencere-geri-yazma.txt` |
| `OynaticiGirdiTestsKabukHatasi` | `k12-acilis-hatasi.txt`, `bozuk-ornek.mp4` |

`GirdiKlipFixture`'ın önbelleğe aldığı `girdi-20sn.mkv`'ye dokunulmadı: onu başka
sınıflar (`OynaticiMotorTestsGirdi`) tüketiyor.

Aynı dosyada, aynı kusurun ikinci yüzü de kapatıldı: `FareKanit` (`.calisma/dalga7a`)
altı kanıt yazıyor, hiç silmiyordu. `FareKanit.Kapat` eklendi, altı ölçüye çağrısı
kondu.

## Borç B — `Kapat` gövdesi tek yerde

Ortak gövde `tests/VidShrink.Tests/KanitKapanisi.cs`. Sayım (`grep "static void Kapat"`,
`KanitKapanisi.Kapat` satırları ayrılarak):

| | `main` | `t0/kanit-borclari` |
| --- | --- | --- |
| kendi gövdesini yazan `Kapat` | 26 | 2 |
| tek satırlık sarmalayıcı | 16 | 41 |

26'nın biri `KanitKapanisi`'nin kendi bildirimi, yani 25 kopya vardı; 24'ü çekildi, biri
kaldı. Sarmalayıcı 16 → 41: çekilen 24 artı yeni eklenen `FareKanit.Kapat`.

Kalan tek kopya `StreamMappingTests` (`AkisGirdisi`): kendi dosyaları gidince klasörde
yalnız **paylaşılan girdiler** kaldıysa onları da siliyor. Ortak gövdede böyle bir kural
yok, bu yüzden çekilmedi.

Çekilen 24 kopya: `AltyaziIndirmeTests`, `BiciminTests`, `FiltreYoklamaTests`,
`MiniKipOlcusuTests`, `OrtakOdakTests`, `OynaticiAracTests`, `OynaticiDalga3GirdiTests`,
`OynaticiDenetimTests`, `OynaticiGelismisTests`, `OynaticiGercekGirdiTests`,
`OynaticiGirdiTests` (`GirdiKanit`), `OynaticiGorunumTests` (iki kanıt sınıfı),
`OynaticiKarsilastirmaTests`, `OynaticiKisayolTests`, `OynaticiMotorTests`,
`OynaticiParcaTests`, `OynaticiYolHaritasiTests`, `PaletteApplyTests`,
`QualityTargetTests`, `QualityTargetUiTests`, `TestAyarYoluTests`, `UstSeritTikTests`,
`WindowLayoutTests`.

Davranış farkları korundu:

- **Ağaçla silme** — `YolKanit` ve `KisayolKanit` bir klasör adı verildiğinde ağacıyla
  siliyordu; ortak gövde `Directory.Delete(yol, true)` ile aynısını yapıyor.
- **Boşalan üst klasörler** — kaydedici öbeğinin gövdesi `paket-2b` gibi boşalan üst
  klasörleri de siliyordu; ortak gövde `.calisma`'ya kadar yukarı yürüyor, `.calisma`'nın
  kendisine dokunmuyor.
- **Desen eşleme** — `AltyaziIndirmeTests` gövdesi `GetFileSystemEntries(Kok, ad)` ile
  desen eşliyordu. `AltyaziKanit.Kapat` 38 yerden çağrılıyor ve hiçbirinde joker yok
  (`grep -c '\*'` → 0), bu yüzden tam adla eşleyen ortak gövde eşdeğer.

## Teslim derlemesi

```
dotnet build VidShrink.sln -c Release -warnaserror -m:2

Oluşturma başarılı oldu.
    0 Uyarı
    0 Hata

Geçen Süre 00:00:11.47
```

## (a) Yeşil koşum

Filtre: `OynaticiGirdiTests`, `PlayerTabTests`, `OynaticiFareTests`,
`OynaticiKisayolTests`, `OynaticiYolHaritasiTests`, `TestAyarYoluTests`,
`OrtakOdakTests`, `MiniKipOlcusuTests`.

```
C:\...\t0-kanit-borclari\tests\VidShrink.Tests\bin\Release\net8.0\VidShrink.Tests.dll (.NETCoreApp,Version=v8.0) için test çalıştırması
VSTest sürümü 17.14.1 (x64)

Test yürütmesi başlatılıyor, lütfen bekleyin...
Toplam 1 test dosyası belirtilen desenle eşleşti.

Başarılı!  - Başarısız:     0, Başarılı:   113, Atlanan:     0, Toplam:   113, Süre: 1 m 12 s - VidShrink.Tests.dll (net8.0)
```

Koşumdan sonra `.calisma` (libmpv ve koşum günlükleri hariç):

```
.calisma\dalga3
.calisma\oynatici-kisayol
.calisma\oynatici-motor
.calisma\oynatici-yol-haritasi
.calisma\test-ciktilari
.calisma\dalga3\gecici
.calisma\dalga3\gecici\ayar-yolu-62b0be60
.calisma\dalga3\gecici\ayar-yolu-62b0be60\son-dosya.mp4
.calisma\oynatici-kisayol\gecmis
.calisma\oynatici-kisayol\liste
.calisma\oynatici-kisayol\tuslar
.calisma\oynatici-kisayol\kirmizi-sol-mavi-sag-320x180.mp4
.calisma\oynatici-kisayol\uzun.srt
.calisma\oynatici-kisayol\gecmis\player-recent.json
.calisma\oynatici-kisayol\gecmis\player-settings.json
.calisma\oynatici-kisayol\liste\a-uzun-700sn.mkv
.calisma\oynatici-kisayol\liste\b-iki-renk.mp4
.calisma\oynatici-motor\kucuk-320x180-25sn.mp4
.calisma\oynatici-yol-haritasi\p14
.calisma\oynatici-yol-haritasi\p14-esik
.calisma\oynatici-yol-haritasi\p14\player-history.json
.calisma\oynatici-yol-haritasi\p14-esik\player-history.json
.calisma\test-ciktilari\appdata
.calisma\test-ciktilari\appdata\10828
.calisma\test-ciktilari\appdata\10828\player-history.json
.calisma\test-ciktilari\appdata\10828\player-recent.json
.calisma\test-ciktilari\appdata\10828\recorder-settings.json
.calisma\test-ciktilari\appdata\10828\settings.json
```

### Ne gitti

- **`.calisma\T176` tamamen gitti.** Listede yok. Bu koşumda `girdi-20sn.mkv`
  üretilmedi — onu isteyen `OynaticiMotorTestsGirdi` filtrenin dışında kaldı. Borcun
  uyarısı yine de geçerli: motor sınıflarıyla birlikte koşan bir süitte `T176` bu
  önbellek yüzünden ayakta kalır, geriye yalnız o dosya kalmalıdır.
- **`.calisma\dalga7a` gitti** — `FareKanit`'in altı kanıt dosyası kapandı.
- `oynatici-kisayol`, `oynatici-yol-haritasi` ve `dalga3` altındaki kanıt `.txt`
  dosyalarının hiçbiri kalmadı.

### Ne kaldı, neden

- `oynatici-kisayol\*.mp4`, `*.srt`, `liste\*`, `oynatici-motor\*.mp4`,
  `dalga3\gecici\...` — **önbelleğe alınmış ffmpeg girdileri**. Kanıt değiller; her
  koşumda yeniden üretilmesinler diye kasten bırakılıyorlar.
- `test-ciktilari\appdata\<pid>\*` — `TestAyarYolu`'nun süreç başına ayar klasörü.
  Gerçek `%APPDATA%\VidShrink`'ten uzak durmanın bedeli; kanıt dosyası değil.
- `oynatici-kisayol\gecmis\*.json`, `oynatici-yol-haritasi\p14\player-history.json`,
  `p14-esik\player-history.json` — **bunlar gerçekten artık.** Ama kapanış kuralının
  kusuru değil: `OynaticiYolHaritasiTests` P14 ölçüleri `Kapat("...", "p14")` ile ağacı
  siliyor ve **yalnız başına koşulduğunda klasör gerçekten gidiyor**:

```
dotnet test --filter '...OynaticiYolHaritasiTests.UstBar|...OynaticiYolHaritasiTests.P14'
Başarılı!  - Başarısız:     0, Başarılı:     2, Atlanan:     0, Toplam:     2, Süre: 6 s
--- kalanlar ---
(bos)
```

  Aynı iki ölçü 113'lük filtreyle koşunca `p14\player-history.json` geri doğuyor:
  `MainWindow`'un `SettingsPathOverride` ile kurduğu geçmiş yazımı, `Kapat`'tan sonra
  bir kez daha diske düşüyor. Sınıf arası bir yarış; bu turdan önce de aynıydı (eski
  gövde de ağacı aynı şekilde siliyordu), yani bu çalışmanın getirdiği bir gerileme
  değil. Ayrı bir iş olarak kaydı düşülüyor.

## (b) Kırmızı koşum — kanıt yerinde duruyor

`PlayerTabTests.OynaticiSekmesiSekmeSeridindeVeIkiDildeBasligiVar`'ın **son asertine**
tutmayacak bir metin konuldu (`Assert.Contains("MUTASYON-BU-SATIR-GERI-YAZILACAK", rapor)`).

```
Test yürütmesi başlatılıyor, lütfen bekleyin...
Toplam 1 test dosyası belirtilen desenle eşleşti.
[xUnit.net 00:00:06.44]     VidShrink.Tests.PlayerTabTests.OynaticiSekmesiSekmeSeridindeVeIkiDildeBasligiVar [FAIL]
  Başarısız VidShrink.Tests.PlayerTabTests.OynaticiSekmesiSekmeSeridindeVeIkiDildeBasligiVar [614 ms]
  Hata İletisi:
   Assert.Contains() Failure: Sub-string not found
String:    "sekme sayisi: 7\r\noynatici sirasi: 0\r\nen: "···
Not found: "MUTASYON-BU-SATIR-GERI-YAZILACAK"
  Yığın İzleme:
     at VidShrink.Tests.PlayerTabTests.OynaticiSekmesiSekmeSeridindeVeIkiDildeBasligiVar() in C:\...\tests\VidShrink.Tests\OynaticiGirdiTests.cs:line 382

Başarısız! - Başarısız:     1, Başarılı:     0, Atlanan:     0, Toplam:     1, Süre: 614 ms - VidShrink.Tests.dll (net8.0)
--- T176 kalanlari ---
C:\...\t0-kanit-borclari\.calisma\T176\k1-sekme.txt
```

Kanıt duruyor: düşen asert `GirdiKanit.Kapat("k1-sekme.txt")` satırına hiç gelmedi.

Mutasyon **elle** geri yazıldı (`git checkout` kullanılmadı), aynı ölçü yeniden koşuldu
ve kanıt bu kez kendiliğinden kalktı:

```
Başarılı!  - Başarısız:     0, Başarılı:     1, Atlanan:     0, Toplam:     1, Süre: 626 ms - VidShrink.Tests.dll (net8.0)
--- T176 kalanlari ---
(bos — T176 gitti)
```
