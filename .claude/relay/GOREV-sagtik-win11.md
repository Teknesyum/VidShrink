# Görev paketi — sağ tık girdisi Windows 11'in yeni menüsünde görünmüyor

Windows makinesinde çalışan ajan için yazıldı — sınama Windows 11 gerektiriyor.
Depo: `github.com/Teknesyum/VidShrink`, dal `main`.

## Ölçülen durum

Kurucu (`Install-VidShrink.ps1:187-240`) sağ tık girdisini yazıyor ve **doğru yazıyor**.
Kullanıcının makinesinde okundu:

```
HKCU:\Software\Classes\SystemFileAssociations\.mp4\shell\VidShrink
  MUIVerb = "Bu Videoyu VidShrink ile Aç"
  Icon    = ...\Programs\VidShrink\VidShrink.exe
  command\(default) = "...\VidShrink.exe" "%1"
```

`.mkv` de aynı. Anahtar yerinde, komut yerinde, simge yerinde.

Buna rağmen kullanıcı menüde göremiyor. Makine **Windows 11 Pro 22631**.

## Hipotez

Windows 11'in kısa bağlam menüsü kayıt defteri fiillerini göstermiyor; klasik fiiller
yalnız **"Daha fazla seçenek göster"** (Shift+F10) altındaki eski menüde çıkıyor. Yeni
menüde görünmek için uygulamanın `IExplorerCommand` gerçekleyen bir kabuk uzantısı sunması
ve bunun bir **sparse MSIX paketi** ile kaydedilmesi gerekiyor.

Hipotezi **doğrulamadan çözüm yazma.** Önce Windows 11'de kısa menü ile genişletilmiş
menüyü ayrı ayrı gözle ve hangisinde göründüğünü söyle.

## İstenen

Sağ tık girdisi Windows 11'in **birincil** menüsünde görünsün, Windows 10'da da çalışmaya
devam etsin.

## Kabul kriteri

1. **Teşhis yazılı.** Girdinin bugün hangi menüde göründüğü, hangisinde görünmediği ve
   nedeni. Kaynağı bir belge ya da ölçüm olsun, tahmin değil.
2. **Windows 11'in kısa menüsünde görünüyor.** Çözüm sparse paket + `IExplorerCommand` ise
   paket kurucudan kurulup kaldırılabilmeli; kurulum yönetici hakkı istememeli (kullanıcı
   kurulumu `%LOCALAPPDATA%` altına iniyor).
3. **Windows 10 bozulmuyor.** Kayıt defteri yolu kalır; yeni yol yalnız 11'de devreye girer.
   Sürüm ayrımını nasıl yaptığını yaz.
4. **Kaldırma temiz.** Kurucunun kaldırma yolu hem kayıt defteri anahtarını hem yeni paketi
   siler; kaldırdıktan sonra menüde iz kalmaz. Ölçü kaldırmadan sonra anahtarların
   yokluğunu doğrulasın.
5. **Kurucu hâlâ BOM'suz.** `Install-VidShrink.ps1` ilk üç baytı `70 61 72` olmalı; BOM
   `irm | iex` yolunu kırıyor. Ölçü baytları okusun.
6. **Genişletme listesi tek yerde.** Bugün uzantılar `Install-VidShrink.ps1:189` içinde bir
   dizi. Yeni yol da aynı listeyi okusun; iki yerde iki liste tutma.
7. `dotnet test -c Release` tamamı yeşil.

## Çıktı

Teşhis; seçilen yol ve neden; Windows 11 ve Windows 10'da menünün ekran görüntüsü ya da
kayıt defteri/paket okuması; kaldırma sonrası doğrulama; tam süit sayıları.

## Sınırlar

- `src/VidShrink.App/**` ve `src/VidShrink.Core/**` **senin değil** — o dosyalarda paralel
  bir birleştirme yürüyor. Sana ait olanlar: `Install-VidShrink.ps1`,
  `src/VidShrink.Launcher/**`, yeni bir paket/uzantı projesi, `tests/VidShrink.Tests/`
  içinde yalnız kendi eklediğin dosya, `.github/workflows/release.yml` (yeni bir varlık
  yayınlanacaksa).
- Yeni renk ya da ölçü uydurma.
- Yorum yazma; mevcut yorumları koru.
- Kendi dalında çalış (`sole/sagtik-win11`), bitince **it**. `main`e sen birleştirme.
