Tema: Windows medya yigini · kaynak: windows-medya-yigini.md

# Windows medya araçlarının paketlenmesi ve dağıtımı

Tarama 2026-08-22. Rakamlar `gh api repos/<owner>/<repo>` ve `/releases/latest` çıktısıdır.

| Depo | Lisans | Yıldız | Açık issue | Son push | Son etiketli sürüm |
|---|---|---|---|---|---|
| clsid2/mpc-hc | GPL-3.0 | 15.505 | 4 | 2026-08-22 | `2.8.0` (2026-08-10) |
| m-ab-s/media-autobuild_suite | GPL-3.0 | 1.811 | 219 | 2026-08-18 | **release/tag yok** |
| shinchiro/mpv-winbuild-cmake | **lisans dosyası yok** (`license: null`) | 1.713 | 58 | 2026-08-14 | `20260814` |

## Depo · clsid2/mpc-hc

**Ne yapıyor.** Sürdürülen MPC-HC çatalı. `distrib/mpc-hc_setup.iss` tek Inno Setup betiği; `build.bat` hem kurucu hem taşınabilir `.zip` üretiyor.

**Kurulum ayrıcalığı.** `.iss` içinde `PrivilegesRequired` **yok** → Inno varsayılanı `admin`, hedef `{pf}\MPC-HC`. Yöneticisiz yol taşınabilir `.zip` (x64 zip 15.424 / x64 exe 97.526 indirme, GitHub asset sayaçları). Ayarlar `HKCU` altında, taşınabilir kipte yanına `mpc-hc64.ini`.

**İmza.** `SignTool = MySignTool` satırı `#ifexist "..\signinfo.txt"` ile sarılı; `contrib/sign.bat` de o dosya yoksa çıkıyor. Sertifika argümanları depo dışında, imzasız derleme sessizce çalışıyor. **Yayınlanan ikili imzasız:** 2.8.0 x64 kurucuyu indirip `Get-AuthenticodeSignature` ile denetledim → `NotSigned` (22.902.187 bayt, asset boyutuyla birebir). SmartScreen imzayla değil indirme itibarıyla geçiliyor.

**Güncelleme.** `UpdateChecker.cpp` uzak sürüm dosyasını okuyor, başarısızsa yedek URL ile bir kez daha. Otomatik kurulum yok; kullanıcı indirme sayfasına yönlendiriliyor.
**Alınacak fikir.** İmza yapılandırmasını depo dışı tek dosyaya bağlayıp yokluğunda derlemenin imzasız devam etmesi. VidShrink'te sertifika bugün yok, ileride olabilir; bu desen tek anahtarlı geçiş sağlar.

**Alınmayacak.** `{pf}` + admin varsayılanı. VidShrink zaten `LOCALAPPDATA\Programs` altına kuruluyor ve betik bu kökü zorunlu tutuyor; admin'e geçmek kazanılmışı geri verir. Inno Setup'a taşınmak da bugün gereksiz.

**VidShrink'te nereye dokunur.** `Install-VidShrink.ps1` (imza doğrulama adımı), `README.md` (SmartScreen bölümü), ileride `.github/workflows/release.yml`.

## Ortak sonuç

Üçü de imzasız dağıtıyor; ikisinde imza altyapısı bile yok. Bu nişte SmartScreen "bilinmeyen yayıncı" uyarısı istisna değil varsayılan. Karşı hamle kod imzalama değil: tek ve sabit indirme adresi, varlığın yanında hash, ve uyarının geleceğini kullanıcıya önceden söylemek. README'de SmartScreen adımının yazılı olması sertifika almaktan ucuz ve bugün yapılabilir.

## Kaynaklar

- `gh api repos/clsid2/mpc-hc`, `/releases/latest`; `distrib/mpc-hc_setup.iss`, `contrib/sign.bat`, `build.bat`, `src/mpc-hc/UpdateChecker.cpp`
- MPC-HC 2.8.0 x64 kurucusu indirilip `Get-AuthenticodeSignature` ile denetlendi (yerel doğrulama)
- `gh api repos/m-ab-s/media-autobuild_suite`; `media-autobuild_suite.bat`
- `gh api repos/shinchiro/mpv-winbuild-cmake`, `/releases/latest`; `.github/workflows/mpv_clang.yml`, `README.md`
