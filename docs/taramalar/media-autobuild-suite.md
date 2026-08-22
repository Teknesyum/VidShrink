Tema: Windows medya yigini · kaynak: windows-medya-yigini.md

# Windows medya araçlarının paketlenmesi ve dağıtımı

Tarama 2026-08-22. Rakamlar `gh api repos/<owner>/<repo>` ve `/releases/latest` çıktısıdır.

| Depo | Lisans | Yıldız | Açık issue | Son push | Son etiketli sürüm |
|---|---|---|---|---|---|
| clsid2/mpc-hc | GPL-3.0 | 15.505 | 4 | 2026-08-22 | `2.8.0` (2026-08-10) |
| m-ab-s/media-autobuild_suite | GPL-3.0 | 1.811 | 219 | 2026-08-18 | **release/tag yok** |
| shinchiro/mpv-winbuild-cmake | **lisans dosyası yok** (`license: null`) | 1.713 | 58 | 2026-08-14 | `20260814` |

## Depo · m-ab-s/media-autobuild_suite

**Ne yapıyor.** Tek `media-autobuild_suite.bat` (2.174 satır) MSYS2 + MinGW-w64 ortamı kurup ffmpeg ve yan araçları kaynaktan derliyor. Sürüm etiketi yok, `master` = ürün.

**Problemi nasıl çözüyor.** Ağırlık derlemede değil, ortamın önden reddedilmesinde. Hiçbir şey indirmeden: 32-bit OS (çıkış), boşluklu yol (çıkış), 32 karakterden uzun kurulum yolu (uyarı + devam), PowerShell < 4 (çıkış), ortamda `cl.exe`/`lib.exe` yani MSVC kabuğu (çıkış). Her mesaj "hatalı yol" değil, "şuraya taşı" diyor.

**Antivirüs.** Çekirdek sayısı sorulurken Defender gerçek zamanlı korumanın işlemciyi yiyeceği ve dizinin taramadan muaf tutulmasının önerildiği yazılıyor. Muafiyet **otomatik eklenmiyor** — admin gerektiren ve onay isteyen bir işi üstlenmemişler. Sınır doğru çizilmiş.
**Alınacak fikir.** İş başlamadan çalışan, her maddesi "ne yap" ile biten önkoşul kapısı. VidShrink kurulumu bugün WinGet + .NET 8 SDK indirmesi + kaynak derlemesi zincirini baştan başlatıyor; ortam uygunsuzsa hata zincirin ortasında, yarım `LOCALAPPDATA` dizini bırakarak çıkıyor.

**Alınmayacak.** Kullanıcının makinesinde kaynaktan derlemek. 219 açık issue büyük ölçüde bunun bedeli. VidShrink aynı hataya yakın: `Install-VidShrink.ps1` main.zip indirip yerelde `publish` ediyor. Doğru yön hazır ikili indirmek.

**VidShrink'te nereye dokunur.** `Install-VidShrink.ps1` (başına önkoşul bloğu, `Install-DotNetSdk8` çağrısının kaldırılması), `README.md` (bilinen ortam engelleri).

## Ortak sonuç

Üçü de imzasız dağıtıyor; ikisinde imza altyapısı bile yok. Bu nişte SmartScreen "bilinmeyen yayıncı" uyarısı istisna değil varsayılan. Karşı hamle kod imzalama değil: tek ve sabit indirme adresi, varlığın yanında hash, ve uyarının geleceğini kullanıcıya önceden söylemek. README'de SmartScreen adımının yazılı olması sertifika almaktan ucuz ve bugün yapılabilir.

## Kaynaklar

- `gh api repos/clsid2/mpc-hc`, `/releases/latest`; `distrib/mpc-hc_setup.iss`, `contrib/sign.bat`, `build.bat`, `src/mpc-hc/UpdateChecker.cpp`
- MPC-HC 2.8.0 x64 kurucusu indirilip `Get-AuthenticodeSignature` ile denetlendi (yerel doğrulama)
- `gh api repos/m-ab-s/media-autobuild_suite`; `media-autobuild_suite.bat`
- `gh api repos/shinchiro/mpv-winbuild-cmake`, `/releases/latest`; `.github/workflows/mpv_clang.yml`, `README.md`
