Tema: Windows medya yigini · kaynak: windows-medya-yigini.md

# Windows medya araçlarının paketlenmesi ve dağıtımı

Tarama 2026-08-22. Rakamlar `gh api repos/<owner>/<repo>` ve `/releases/latest` çıktısıdır.

| Depo | Lisans | Yıldız | Açık issue | Son push | Son etiketli sürüm |
|---|---|---|---|---|---|
| clsid2/mpc-hc | GPL-3.0 | 15.505 | 4 | 2026-08-22 | `2.8.0` (2026-08-10) |
| m-ab-s/media-autobuild_suite | GPL-3.0 | 1.811 | 219 | 2026-08-18 | **release/tag yok** |
| shinchiro/mpv-winbuild-cmake | **lisans dosyası yok** (`license: null`) | 1.713 | 58 | 2026-08-14 | `20260814` |

## Depo · shinchiro/mpv-winbuild-cmake

**Ne yapıyor.** Linux'ta çapraz derleyip Windows için mpv ve ffmpeg ikilileri üretiyor, `.github/workflows/mpv_clang.yml` ile yayınlıyor.

**Dağıtım.** Etiket tarih: `20260814`. Yayın işi Sourceforge (sftp) ve GitHub release'e paralel yüklüyor, ikisi de `continue-on-error: true` — biri düşerse diğeri sürümü kurtarıyor. Ardından "Pruning tags" adımı **son 30 etiketi tutup** eskilerini API ile siliyor; gecelik yayın deposu şişmiyor. `mpv-dev` ayrı varlık. İmzalama adımı yok, SmartScreen'e dair hiçbir şey yok.

**Alınacak fikir.** Tarih etiketli gecelik yayın + son N sürümü tutup gerisini silen budama. VidShrink'in bugün release'i yok; kurucu doğrudan `main.zip` çekiyor, yani kullanıcı her an yarım birleştirilmiş bir ağacı kurabiliyor. Sabit etiketli varlık bunu tek hamlede kapatır.

**Alınmayacak.** Lisanssız yayın. `gh api` `license: null` döndürüyor, depoda LICENSE yok. GPL bileşenlerden üretilen ikilileri lisans beyanı olmadan dağıtmak VidShrink'in taşıyamayacağı bir risk; ffmpeg tarafı için yayın varlığının yanına lisans metni konmalı.

**VidShrink'te nereye dokunur.** Yeni `.github/workflows/release.yml` (depoda `.github` klasörü yok), `Install-VidShrink.ps1` (main.zip yerine etiketli varlık + SHA-256 doğrulaması), `CHANGELOG.md`.

## Ortak sonuç

Üçü de imzasız dağıtıyor; ikisinde imza altyapısı bile yok. Bu nişte SmartScreen "bilinmeyen yayıncı" uyarısı istisna değil varsayılan. Karşı hamle kod imzalama değil: tek ve sabit indirme adresi, varlığın yanında hash, ve uyarının geleceğini kullanıcıya önceden söylemek. README'de SmartScreen adımının yazılı olması sertifika almaktan ucuz ve bugün yapılabilir.

## Kaynaklar

- `gh api repos/clsid2/mpc-hc`, `/releases/latest`; `distrib/mpc-hc_setup.iss`, `contrib/sign.bat`, `build.bat`, `src/mpc-hc/UpdateChecker.cpp`
- MPC-HC 2.8.0 x64 kurucusu indirilip `Get-AuthenticodeSignature` ile denetlendi (yerel doğrulama)
- `gh api repos/m-ab-s/media-autobuild_suite`; `media-autobuild_suite.bat`
- `gh api repos/shinchiro/mpv-winbuild-cmake`, `/releases/latest`; `.github/workflows/mpv_clang.yml`, `README.md`
