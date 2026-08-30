# Görev paketi — VidShrink macOS'ta hiç çalıştırılmadı

Mac'te çalışan ajan için yazıldı. Depo: `github.com/Teknesyum/VidShrink`, dal `main`.

## Durum

Yayın iş akışı dört hedef için kendi kendine yeten paket üretiyor: `win-x64`, `linux-x64`,
`osx-x64`, `osx-arm64` (`.github/workflows/release.yml`). Yani macOS paketi **her sürümde
çıkıyor** ama bugüne kadar **bir kez bile çalıştırılmadı**. Bilinen boşluklar:

- `UpdateCheck.CanSelfUpdate => OperatingSystem.IsWindows()`
  (`src/VidShrink.Core/UpdateCheck.cs`). macOS'ta kendini güncelleme kapalı; uygulama
  yalnız yeni sürümü haber veriyor.
- Kurucu yalnız PowerShell: `Install-VidShrink.ps1`. macOS'ta karşılığı yok.
- ffmpeg pakete girmiyor (GPLv3, depo AGPL-3.0-or-later). Windows'ta kurucu onu
  `tools/ffmpeg` altına indiriyor ve başlatıcı `PATH`e ekliyor
  (`src/VidShrink.Launcher/Program.cs`). macOS'ta bu yolun karşılığı tanımlı değil.
- Başlatıcı (`VidShrink.Launcher`) Windows varsayımlarıyla yazıldı: `.exe` adları,
  `MessageBoxW` ile uyarı, kendi ikilisini değiştiren geçiş süreci.

Bu paket **önce teşhis** istiyor, sonra yalnız açılışı engelleyen şeyleri düzeltmeyi.
Kapsamı kendiliğinden büyütme.

## Adım 1 — çalıştır ve raporla

`osx-arm64` paketini son sürümden indir
(`https://github.com/Teknesyum/VidShrink/releases/latest`) ya da yerelde üret:

```
dotnet publish src/VidShrink.App -c Release -r osx-arm64 --self-contained
```

Sonra şunları tek tek yaz:

1. Uygulama açılıyor mu? Açılmıyorsa tam hata metni.
2. Gatekeeper ne diyor? İmzasız paket karantinaya takılıyor mu, kullanıcı ne görüyor?
3. Pencere çiziliyorsa: yazı tipleri düşüyor mu, düzen bozuk mu, Türkçe harfler doğru mu?
   Ekran görüntüsü koy.
4. ffmpeg bulunuyor mu? `PATH`te Homebrew'un ffmpeg'i varsa uygulama onu görüyor mu?
5. Bir video yükleyip küçültme denemesi yürüyor mu, yoksa nerede duruyor?

## Adım 2 — yalnız açılışı engelleyenleri düzelt

Adım 1'de bulduklarından **uygulamanın hiç açılmasına ya da hiç iş yapmasına engel
olanları** düzelt. Kozmetik farkları düzeltme, listele.

## Adım 3 — iki karar için ölçüm getir, uygulama

1. **macOS kurulum yolu.** `.app` paketi mi, `brew` formülü mü, düz bir `install.sh` mi?
   Üçünün de imza/noter (notarization) gereksinimini ve kullanıcıya maliyetini yaz.
   Hangisini önerdiğini bir cümleyle söyle. **Kurma, öner.**
2. **macOS'ta kendini güncelleme.** Windows'taki yol çalışan ikiliyi yan ada indirip
   çıkışta tek bir atomik `File.Move` ile değiştiriyor
   (`src/VidShrink.Core/UpdateCheck.cs`, `LauncherUpdate`). macOS'ta `.app` paketinin
   yerine geçmenin karşılığı ne, imza bozulur mu? Yalnız rapor.

## Kabul kriteri

1. Adım 1'in beş sorusunun beşi de cevaplanmış, ekran görüntüsü var.
2. Açılışı engelleyen her şey düzeltilmiş ya da neden düzeltilemediği yazılmış.
3. `dotnet test -c Release` tamamı yeşil — `--no-build` yok.
4. Düzeltmelerin Windows'u bozmadığı gösterilmiş: platforma bağlı her dal
   `OperatingSystem.IsWindows()` / `IsMacOS()` ile ayrılmış, Windows yolu değişmemiş.
   Ölçü bunu doğrulasın.
5. Adım 3'ün iki sorusu için birer paragraf öneri.

## Sınırlar

- `src/VidShrink.App/MainWindow.*`, `src/VidShrink.App/Locales/**`,
  `src/VidShrink.App/LanguageCatalog.cs` ve `src/VidShrink.App/Performance/**`
  **senin değil** — orada paralel bir birleştirme yürüyor.
- Senin olanlar: `src/VidShrink.Launcher/**`, `src/VidShrink.Core/UpdateCheck.cs`,
  `src/VidShrink.Ffmpeg/**`, `src/VidShrink.App/Platform.cs`,
  `src/VidShrink.App/Program.cs`, yeni bir macOS kurulum betiği,
  `tests/VidShrink.Tests/` içinde yalnız kendi eklediğin dosya.
- Yeni renk ya da ölçü uydurma; `Themes/Theme.axaml` belirteçlerinden çık.
- Yorum yazma; mevcut yorumları koru.
- Kendi dalında çalış (`serkan/macos-ilk-kosum`), bitince **it**. `main`e sen birleştirme.
