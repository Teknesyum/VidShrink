[[netlestirme:016]]

# Netleştirme: A dalgasi olculdu: cift tik -> ilk kare 1796,5 ms'ten 1747,7 ms'e indi, yalnizca

İşe başlamadan önce soruyu keskinleştir. Görüş verme, plan yazma, kod yazma.
Yalnız şunu döndür: soruda belirsiz kalan yerler, her biri için tek satırlık bir netleştirme sorusu, en fazla beş. Belirsizlik yoksa "net" yaz.

## Soru

A dalgasi olculdu: cift tik -> ilk kare 1796,5 ms'ten 1747,7 ms'e indi, yalnizca -71 ms. Hedef ~100 ms. Kalan 1,7 saniyenin nerede oldugunu adim adim olctuk. Bu hedefe hangi sirayla ve hangi tekniklerle varilir? Kullanici algiyi kullanmaya acikca izin verdi: once sahte/iskelet bir arayuz gosterip icini sonradan doldurmak serbest.

## Elde olan olgular

# Hipersürüş olguları — 16 Eylül 2026

Hepsi bu depoda ölçülmüş ya da kaynaktan okunmuş. Tahmin yok.

## Ürün

VidShrink: .NET 8 + Avalonia 12.1.2, Windows masaüstü. Kullanıcı bir mp4'e çift
tıklıyor; kabuk `"<KurulumKoku>\VidShrink.exe" "%1"` komutunu koşuyor. `VidShrink.exe`
bir **başlatıcı**: güncellemeyi uygulayıp `app\VidShrink.App.exe`'yi doğuruyor ve kendi
çıkıyor. Oynatma motoru libmpv (115 MB yerel kitaplık, `LibMpvLocator.EnsureLoaded`).

Yayın kendi kendine yeten (`--self-contained -r win-x64`). Uygulama `.dll` 221 KB,
başlatıcı exe 9,7 MB.

## Ölçüm (eşleşik sıcak, 14 tekrar, 720p30 20 sn 6,2 MB klip)

Dış saat: ölçerin kendi kronometresi, `Start-Process` çağrısından ilk kare
işaretine kadar. Makine DESKTOP-0J80KVV / Windows 11 22631.

| Sütun | Taban (1a1385c1) | A dalgası sonrası (d2ea8bd5) |
| --- | --- | --- |
| dış saat, çift tık → ilk kare | 1796,5 ms | 1747,7 ms |

Eşleşik farkın ortancası −71,0 ms, 14 çiftin 9'u yeni yapı lehine, en az −267,5,
en çok +101,5.

## Yeni yapının adım tablosu (ortanca, ms, başlatıcının doğumundan itibaren)

| Adım | Ortanca | Bir öncekinden pay |
| --- | --- | --- |
| baslatici | 58,8 | 58,8 |
| app-dogdu | 67,4 | 8,6 |
| main | 127,2 | 59,8 |
| tek-ornek | 133,0 | 5,8 |
| libmpv-hazir | 147,2 | 14,2 |
| cerceve (Avalonia OnFrameworkInitializationCompleted) | 450,3 | 303,1 |
| ayar-okundu | 454,6 | 4,3 |
| palet | 648,7 | 194,1 |
| pencere-yapici (MainWindow ctor girişi) | 726,0 | 77,3 |
| xaml (InitializeComponent döndü) | 1030,6 | 304,6 |
| yapici-bitti | 1120,2 | 89,6 |
| pencere-kuruldu | 1227,2 | 107,0 |
| pencere-yuklendi (Loaded) | 1424,4 | 197,2 |
| ayarlar | 1454,0 | 29,6 |
| giris-canlandirmasi | 1456,2 | 2,2 |
| sekme | 1458,3 | 2,1 |
| motor-acildi | 1741,8 | 283,5 |
| kare-kaynagi | 1747,8 | 6,0 |
| ilk-kare | 1750,5 | 2,7 |

İşaretler birikimli. `libmpv-hazir` 147 ms'te: kitaplık ilk karenin **çok önünde**
hazır, kuyruğun gövdesi orada değil.

## Kaynaktaki olgular

- `MainWindow.axaml` **1349 satır**, `MainWindow.axaml.cs` **4601 satır**.
- Pencerede tek bir `TabControl` var (`MainWindow.axaml:65`, `SelectedIndex="1"`) ve
  **yedi `TabItem`'ın içeriği aynı XAML dosyasında satır içi**: oynatıcı (:137),
  küçültme (:142-747), dönüştürme (:749-994), hakkında (:996-1020), kaydedici
  (:1022-1026), gelişmiş (:1028-1120, `IsVisible=False`), ayarlar (:1122-1273).
  Yani `InitializeComponent` yedi sekmenin **hepsini** kuruyor; açılışta görünen bir
  tanesi.
- `App.OnFrameworkInitializationCompleted` sırası: geçici temizlik (arka plana
  alınmış), ayar okuma, `PaletteCatalog.Use(tema)`, `RegisterFileTypes()`,
  `MacUpdate.Begin()`, `StartupWindow()` → `new MainWindow(path)`.
- `csproj` ve `Directory.Build.props` içinde **`PublishReadyToRun`, `PublishAot`,
  `TieredCompilation`, `TieredPGO`, `InvariantGlobalization`, `PublishSingleFile`
  anahtarlarının hiçbiri yok**. Yayın komutu yalnız `-c Release -r win-x64
  --self-contained`. Yani her açılışta bütün IL JIT'leniyor.
- `Program.WarmPlayback()` libmpv'yi `Task.Run` ile önden yüklüyor; yalnız kabuktan
  dosya geldiğinde.
- Oynatma motoru zaten `Task.Run(EngineFactory)` ile arayüz ipliğinin dışında
  kuruluyor, ayar yazımları oynatmanın arkasında, çizim saati ilk kareyi beklemiyor.
- `sekme` → `motor-acildi` arası 283,5 ms: `mpv_create` + `loadfile` + ilk çözme.
  mpv `edl://` ve `--hwdec` bu depoda ölçülmedi; `hwdec` bugün `no`
  (`MpvEngine.cs:143`), `vo=libmpv` BGRA yazılımsal kopya (`:142`).

## Kısıtlar

- Renk yalnız `Themes/Palette/<Ad>/Theme.axaml`'dan, ölçü yalnız
  `Themes/Theme.axaml` belirteçlerinden gelir. Yeni belirteç uydurulmaz, sorulur.
- 26 palet dosyası var; her yeni renk 26 yere girer.
- Kurulum iki süreçli: `VidShrink.exe` (başlatıcı) + `app\VidShrink.App.exe`.
  Kabuğun çift tık komutu başlatıcıyı gösteriyor, bu değişmiyor.
- Test projesi tek: `tests/VidShrink.Tests`. Her karar kaynağı okuyan ya da davranışı
  koşturan bir pimle sabitleniyor.

## Kullanıcının bu turdaki cümlesi

> "şimdi hipersürüş için ne gerekiyorsa yap fable a danış testler yap gerekirse
> kullanıcının algısını kullan sahte 1 sn lik bir arayüz oluştur sonra içini doldur
> vb ne gerekiyorsa yap 100ms cıvarında hedefimiz var daha kısa olursa daha iyi"
