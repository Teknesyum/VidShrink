# Görev paketi — macOS kendini güncellesin, kurulum yolu uçtan uca koşsun

Mac'te çalışan ajan için yazıldı. Depo: `github.com/Teknesyum/VidShrink`, dal `main`.
Önceki iki paketin (`GOREV-macos-ilk-kosum.md`, `GOREV-macos-paket.md`) devamı;
bulguların `docs/macos-ilk-kosum.md` ve `docs/macos-paket.md` içinde.

## Nereden devam ediyorsun

Uygulama artık macOS'ta açılıyor, süit orada koşuyor ve kurulum gerçek bir
`~/Applications/VidShrink.app` paketi bırakıyor. Geriye tek büyük boşluk kaldı:
`UpdateCheck.CanSelfUpdate` hâlâ `OperatingSystem.IsWindows()` — macOS'ta uygulama yeni
sürümü **haber veriyor ama kendini güncelleyemiyor**. Kullanıcı her sürümde kurulum
betiğini elle koşmak zorunda.

Yolu geçen turda sen çıkardın: paket imzası dosya bazlı güncellemeye kapalı, birim
paketin tamamı, takas `renamex_np` / `RENAME_SWAP` ile. Bu turda o yolu **uygula**.

## İş 1 — macOS'ta kendini güncelleme

Kabul kriteri:

1. `CanSelfUpdate` macOS'ta, **yalnız çalışan ikili bir `.app` paketinin içindeyken**
   açık. Düz kurulumda (paketsiz, `~/.local/share` altındaki eski yol) kapalı kalsın ve
   kullanıcı yine haberi görsün. Hangi koşula baktığını ölçü tutsun.
2. Takas atomik. Yeni paket yanına indirilir, imzası **takastan önce** doğrulanır, sonra
   `renamex_np`/`RENAME_SWAP` ile yer değiştirir. Yarım kalırsa kullanıcıda **çalışan eski
   paket** kalır — hiçbir ara durumda ne eksik ne bozuk paket olsun.
3. Takas sonrası imza geçerli: `codesign --verify` çıktısını raporda göster. Karantina
   yazılmadığını `xattr -p com.apple.quarantine` ile göster.
4. Eski paket ne zaman siliniyor, yazılı olsun. Çalışan sürecin altından silmek yok.
5. **Windows ve Linux yolları değişmiyor.** Windows'ta bugünkü yan-ada-indir + çıkışta
   `File.Move` yolu olduğu gibi durur; platform dalı `OperatingSystem.IsWindows()` /
   `IsMacOS()` ile ayrılır ve Windows dalının değişmediğini bir ölçü tutar — sözle değil.
6. Ölçü mutasyonla sınansın: imza doğrulama adımını kaldır, ölçünün kırmızıya döndüğünü
   göster, geri al.

## İş 2 — tek komutluk kurulum uçtan uca

Geçen turda paket yolunun tam koşumu mümkün değildi: `macos-app-bundle.sh` yayın arşivinin
içinden geliyor ve `v0.2.4` onu taşımıyordu. **`v0.2.5` bu betiği taşıyor** — artık koşuyor.

Kabul kriteri:

1. Temiz bir makine durumundan başla (`sh install-vidshrink.sh --uninstall` ile izleri
   sil), sonra tek komutla kur: `curl -fsSL .../main/install-vidshrink.sh | sh`.
2. Kurulum gerçekten **paket** bırakıyor: `~/Applications/VidShrink.app`. Finder'dan çift
   tıkla açılıyor, Dock'ta kendi simgesiyle duruyor. Ekran görüntüsü koy.
3. **Simge bu kez geliyor.** `v0.2.4` arşivinde görsel yoktu, paket simgesiz kalmıştı;
   `v0.2.5` arşivinde `VidShrink.png` var. Paketin `.icns`i üretilmiş mi, doğrula.
4. Kurulan sürüm gerçekten `0.2.5`: `plutil -p .../Info.plist` çıktısını göster.
5. Bir video yükleyip küçültme uçtan uca yürüyor. Kaç saniye, hangi boyut — sayı yaz.
6. İş 1 bittiyse: `0.2.5` kurulu paketin daha yeni bir sürümü kendi kendine aldığını
   göster. Yayın yoksa yerelde sahte bir manifest ile koştur ve öyle olduğunu söyle.

## Sınırlar

- Senin olanlar: `src/VidShrink.Core/UpdateCheck.cs`, `src/VidShrink.Launcher/**`,
  `install-vidshrink.sh`, `macos-app-bundle.sh`, `docs/macos-*.md`,
  `tests/VidShrink.Tests/` içinde yalnız kendi eklediğin dosya.
- `src/VidShrink.App/MainWindow.*`, `Locales/**`, `Themes/**`, `Playback/**` **senin
  değil** — orada arayüz işi yürüyor.
- `UpdateCheck.cs` içindeki `UpdateSettings` 25 ayarı diske yazıyor; o bölüme dokunma,
  yalnız güncelleme yoluna dokun.
- Yorum yazma; mevcut yorumları koru. Yeni renk ya da ölçü uydurma.
- Kendi dalında çalış (`serkan/macos-guncelleme`), bitince **it**. `main`e sen birleştirme.
