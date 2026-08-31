# Görev paketi — macOS kurulumu bir `.app` paketi olsun, testler Mac'te koşsun

Mac'te çalışan ajan için yazıldı. Depo: `github.com/Teknesyum/VidShrink`, dal `main`.
Önceki paketin (`GOREV-macos-ilk-kosum.md`) devamı; bulguların `docs/macos-ilk-kosum.md`de.

## Nereden devam ediyorsun

İlk koşum paketi uygulamanın macOS'ta hiç açılmadığını bulup düzeltti: adı `.App` ile
biten noterlenmemiş ikiliyi çekirdek `exec` anında öldürüyordu. Artık açılıyor, motor
uçtan uca çalışıyor. Kendi önerin: kurulum betiği indirdiğini yerelde bir
`~/Applications/VidShrink.app` paketine sarsın ve ad-hoc imzalasın — yerelde üretilen
paket karantina almıyor, sen ölçtün.

## İş 1 — testler Mac'te koşsun (önce bu)

`tests/VidShrink.Tests/AppHost.cs:37` Avalonia'yı sabit `UseWin32()` ile kuruyor;
macOS'ta `kernel32.dll` bulunamayınca **tüm süit** düşüyor. Sen `UsePlatformDetect()`
denemiş, Avalonia Native'in AppKit ana iş parçacığı kuralına takılmışsın.

Bu turda o kural senin: pencere kuran ölçüler macOS'ta ya gerçekten koşsun ya da
**açıkça atlansın**; süitin tamamının düşmesi kabul değil.

Kabul kriteri:

1. `dotnet test -c Release` macOS'ta **tamamlanıyor** — sıfır başarısız. Pencere kuran
   ölçüler koşuyorsa koşsun, koşamıyorsa `Skip` gerekçesiyle atlansın ve gerekçe
   "macOS'ta pencere ana iş parçacığı gerektiriyor" gibi somut olsun.
2. **Windows bozulmuyor.** Bugün Windows'ta 939 ölçüden 922'si geçiyor, 17'si atlanıyor.
   Değişiklikten sonra Windows'ta atlanan sayısı **artmamalı**. Bu sayıyı Windows'ta
   koşturamıyorsun; bu yüzden platform dalını `OperatingSystem.IsWindows()` /
   `IsMacOS()` ile ayır ve Windows dalının davranışının değişmediğini bir ölçüyle
   kanıtla, sözle değil.
3. Kaç ölçünün macOS'ta koştuğunu, kaçının atlandığını ve neden atlandığını raporda yaz.

## İş 2 — `~/Applications/VidShrink.app`

`install-vidshrink.sh` indirdiğini bir uygulama paketine sarsın ve ad-hoc imzalasın.

Kabul kriteri:

1. Betik çalıştıktan sonra `~/Applications/VidShrink.app` var, **Finder'dan çift tıkla
   açılıyor**, Dock'ta kendi adı ve simgesiyle görünüyor, menü çubuğunda
   "Avalonia Application" değil **VidShrink** yazıyor. Ekran görüntüsü koy.
2. `Info.plist` sürüm alanları (`CFBundleShortVersionString`, `CFBundleVersion`)
   `Directory.Build.props` içindeki `<Version>` ile aynı. Sabit yazma; üretimde oku.
   Bir ölçü ikisinin eşitliğini doğrulasın.
3. Simge: depoda bugün macOS için bir `.icns` yok. Varsa uygulamanın kendi simgesinden
   üret, yoksa **üretme ve raporda söyle** — dışarıdan görsel getirme, depo
   AGPL-3.0-or-later.
4. Kaldırma: betik `--uninstall` (ya da eşdeğeri) ile paketi ve bıraktığı her şeyi
   siler; sonrasında `~/Applications` altında iz kalmaz. Ölçü doğrulasın.
5. Karantina: `curl` ile indirilip çalıştırılan betiğin ürettiği paket **karantinasız**
   açılıyor. `xattr -p com.apple.quarantine` çıktısını raporda göster.
6. Windows ve Linux kurulum yolları değişmiyor. `Install-VidShrink.ps1` ilk üç baytı
   hâlâ `70 61 72` — BOM `irm | iex` yolunu kırıyor, ölçü baytları okusun.

## İş 3 — yalnız rapor, uygulama yok

Kendini güncelleme: paket imzası dosya bazlı güncellemeye kapalı, birim paketin tamamı
olmalı ve takas `renamex_np`/`RENAME_SWAP` ile yapılmalı — bunu sen bulmuştun.
Paket yerine oturduktan sonra bu yolun **adım adım** nasıl işleyeceğini yaz: yarım
kalırsa ne olur, eski paket ne zaman silinir, `CanSelfUpdate` hangi koşulda açılır.
Kod yazma.

## Sınırlar

- Senin olanlar: `install-vidshrink.sh`, `tests/VidShrink.Tests/AppHost.cs`,
  `tests/VidShrink.Tests/MacOsStartupTests.cs`, `src/VidShrink.App/VidShrink.App.csproj`,
  yeni bir paketleme betiği, `docs/macos-*.md`, `.github/workflows/release.yml`.
- `src/VidShrink.App/MainWindow.*`, `Locales/**`, `LanguageCatalog.cs`, `Playback/**`
  **senin değil.**
- Yorum yazma; mevcut yorumları koru. Yeni renk ya da ölçü uydurma.
- Kendi dalında çalış (`serkan/macos-paket`), bitince **it**. `main`e sen birleştirme.
