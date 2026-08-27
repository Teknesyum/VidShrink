# v0.1.1 sürüm doğrulaması

T45 kurulum betiklerini kaynaktan derlemekten indirmeye çevirdi ve mühürlendi. Mühür
anında **ad eşleşmesi** metinden doğrulanmıştı; indirilen baytların içine bakılmamıştı.
Bu belge o boşluğu kapatır.

Ölçüm tarihi: 2026-08-26 · Yayın: `v0.1.1` (`latest`, 13 varlık) · Commit: `df1c89d`

## 1. Sağlama listesi ve dosya bütünlüğü

`checksums-win-x64.txt` üç satır, `sha256sum` biçiminde:

    529bee7fa1c922f31ddc2fee6dd45f1aef6932c59c6fad26718f1f7f1238fa81  vidshrink-win-x64.zip
    eb574c45f02160668ee8f5bd010d436964f519e9b7e100a65ee5720de1cef28c  manifest-win-x64.json
    5fb6246317cf6b6d7146f13eca460635eafc21c454ffc4a06729d0e8116d3862  vidshrink-launcher-win-x64.zip

İndirilen üç dosyanın özeti listeyle karşılaştırıldı:

    vidshrink-win-x64.zip: OK
    manifest-win-x64.json: OK
    vidshrink-launcher-win-x64.zip: OK

`Install-VidShrink.ps1` `Assert-Checksum` ile tam bunu yapıyor; liste biçimi betiğin
beklediği biçim.

## 2. Başlatıcı arşivi — fazladan katman yok

`vidshrink-launcher-win-x64.zip`, 28,5 MB, **üç girdi**, hepsi kökte:

    VidShrink.Core.pdb
    VidShrink.exe
    VidShrink.pdb

Betik bu arşivi kurulum kökünün köküne açıyor. `VidShrink.exe` kökte olduğu için
`Install-VidShrink.ps1:184` denetimi geçer. Kısayolların gösterdiği dosya budur.

## 3. Uygulama arşivi — düzen manifestle birebir

`vidshrink-win-x64.zip`, 42,0 MB, **222 girdi**. Kökte `VidShrink.App.exe`,
`VidShrink.App.dll` ve `manifest.json` duruyor. Tek alt klasör `Fonts/`.

222 = 220 dosya + `manifest.json` + `Fonts/` klasör girdisi.

Manifest ile arşiv karşılaştırıldı:

    manifest yol sayisi                          220
    arsiv dosya sayisi (manifest.json haric)     220
    manifestte olup arsivde olmayan              yok
    arsivde olup manifestte olmayan              yok
    sha dogrulanan                               220
    tutmayan                                     0

Yani manifestteki 220 yolun 220'si arşivde var, 220'sinin de SHA-256'sı ve boyutu
tutuyor. Yol kayması yok — güncelleyicinin `Diff`i kurulu klasörle birebir eşleşecek.

## 4. Sürüm işareti

Manifest `version: 0.1.1`, `rid: win-x64`, `commit: df1c89d`.
Betik `.update-version` dosyasına `$tag.TrimStart('v')` yazıyor → `0.1.1`.

İkisi aynı. Taze kurulumdan sonraki ilk açılış kendini güncel sayar; T45'in kapattığı
"191/220 dosya farklı, 44 MB yeniden iner" kusuru gerçekten kapanmış oluyor.

## 5. Kapanan açık

T45 mühürlenirken K1'in tek açığı şuydu: başlatıcı `net8.0-windows` hedefliyor ve
`publish` işi `ubuntu-latest`'ta koşuyor, çapraz yayım **ölçülmemişti**. v0.1.1 koşusu
bunu kapattı — dört hedefin dördü de başarılı, `vidshrink-launcher-win-x64.zip` üretildi
ve içinde çalışabilir bir `VidShrink.exe` var.

## Ölçülmeyen

Kurulum betiği bu makinede **çalıştırılmadı**; WinGet'ten ffmpeg alma, kısayol oluşturma
ve süreç durdurma adımları canlı görülmedi. Doğrulanan şey betiğin indirdiği ve açtığı
şeyin doğru olduğu, betiğin kendisinin uçtan uca koştuğu değil.
