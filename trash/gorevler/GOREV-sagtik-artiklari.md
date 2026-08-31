# Görev paketi — sağ tık işinden kalan üç artık

Sole için yazıldı. Depo dalı: `main` (kendi dalını aç: `sole/sagtik-artiklari`).

Sparse paket + `IExplorerCommand` yolu doğru kurulmuş, süit yeşil: `main`de 958 ölçü,
941 geçiyor, 17 atlanıyor, 0 başarısız. Üç artık kaldı.

## Artık 1 — kurucu, paketi taşımayan yayında düşüyordu

`Write-Windows11ShellMenu`, `shell/AppxManifest.template.xml` ya da
`VidShrink.ShellExtension.dll` bulunamayınca `throw` ediyordu. O iki dosyayı **yalnız bu
işten sonraki yayınlar** taşıyacak; yayında olan `v0.2.5` ve bütün eski sürümler
taşımıyor. Yani `main`deki kurucu, Windows 11'de bugünkü yayını kurmaya kalkınca
kurulumu düşürüyordu.

En az kapsamlı düzeltmeyi ben uyguladım: `throw` yerine sarı bir satır yazılıp klasik
menüye düşülüyor. **Düzeltmeyi bir ölçüye bağlamak sende.**

Kabul kriteri:

1. Bir ölçü, kabuk dosyaları yokken kurulumun **düşmediğini** ve klasik menünün
   yazıldığını doğrulasın. Metin araması yetmez — betiği gerçekten koştur.
2. Ölçü mutasyonla sınansın: düşüşü geri getir, ölçünün kırmızıya döndüğünü göster,
   geri al.
3. `v0.2.5`i Windows 11'de gerçekten kur ve kurulumun sonuna vardığını göster.

## Artık 2 — ölçüler betiğin yazısını tutuyor, davranışını değil

`Windows11ShellMenuTests` içindeki yedi ölçünün yedisi de `Assert.Contains` ile betikte
ve manifestte dize arıyor. Bu, dizeyi yeniden yazan her düzenlemede kırılır ama gerçek
bozulmayı yakalamaz — paketin kaydı, kaldırması ve menüde görünmesi hiçbir ölçüyle
tutulmuyor. Raporunda gerçekten kurup kaldırdığını yazmışsın; **onu ölçüye bağla.**

Kabul kriteri:

1. Paketin kaydı ve kaldırılması gerçekten koşularak doğrulansın: kurulumdan sonra
   `Get-AppxPackage` paketi görüyor, kaldırmadan sonra sayı `0`. Windows 11 gerektiren
   ölçü Windows 10'da ve CI'da **açık gerekçeyle** atlanabilir — ama atlanan sayısı
   bugünkü 17'nin üstüne çıkmasın; erken dönüp geçmek de kabul.
2. Kalan metin aramalarından hangilerinin gerçekten bir şey tuttuğunu tek tek söyle;
   tutmayanları sil. Ölçü sayısını korumak diye tutma.

## Artık 3 — araç takımı iki yerde iki türlü yazılı

`VidShrink.ShellExtension.vcxproj` `<PlatformToolset>v145</PlatformToolset>` diyor,
`release.yml` ise `/p:PlatformToolset=v143` ile eziyor. İki yerde iki değer duruyor;
biri sessizce eskir ve yayın koşumunda anlaşılır.

Kabul kriteri: değer **tek yerde** dursun ve yayın iş akışı onu ezmesin. Hangi tarafın
kaldığını ve neden onu seçtiğini yaz. Yayın iş akışının bu değişiklikle koştuğunu
göster — `shell-extension` işi düşerse **hiç yayın çıkmıyor**, `publish` ona bağlı.

## Sınırlar

- `dotnet test -c Release` tamamı yeşil, `--no-build` yok. Taban: 958 ölçü, 941 geçiyor,
  17 atlanıyor, 0 başarısız. **Atlanan sayısı artmasın.**
- Senin olanlar: `Install-VidShrink.ps1`, `src/VidShrink.ShellExtension/**`,
  `.github/workflows/release.yml`, `tests/VidShrink.Tests/Windows11ShellMenuTests.cs`.
- `src/VidShrink.App/**`, `src/VidShrink.Core/UpdateCheck.cs`, `install-vidshrink.sh` ve
  `macos-app-bundle.sh` **senin değil** — macOS'ta paralel bir iş yürüyor.
- Yorum yazma; mevcut yorumları koru. Yeni renk ya da ölçü uydurma.
- Kendi dalında çalış (`sole/sagtik-artiklari`), bitince **it**. `main`e sen birleştirme.

## Not

Raporunda "tam süit: 941 geçti, 0 uyarı, **0 atlanan**" yazıyordu. Aynı ağaçta ölçtüm:
941 geçiyor ama atlanan **17**, taban da 17. Geçen sayı doğruydu, atlanan satırı veriden
kaymış. Sayıyı koşumun çıktısından al, hatırından değil.
