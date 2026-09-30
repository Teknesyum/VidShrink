# temiz-kurulum

Kurulum yollarını hiçbir şey yüklü olmayan Windows'ta dener.

- `Temiz-Kos.ps1` — konukta koşar. 2. yol: `VidShrink-Setup.exe --console --no-launch`, sonra `--uninstall`.
  3. yol: README'deki tek satır birebir, TEMP ve LOCALAPPDATA boşluklu/Türkçe klasörde; panele `PostMessage`
  ile Enter ("Kur"), bitince yine Enter ("Programı Aç"). Her yolda kurulu exe, ffmpeg ile 1 sn kodlama,
  libmpv + vulkan-1 yüklemesi, uygulamanın `-AyaktaSaniye` sonra pencereyle ayakta olduğu, ekran görüntüsü.
  Sonuç `-Sonuc` klasöründe `bitti.json` + `gunluk.txt`.
- `Baslat.ps1` — ana makinede: `.wsb`'yi `.calisma/temiz-kurulum/<koşu>` ile doldurur, Sandbox'ı açar,
  `bitti.json`'u bekler, özeti yazar, Sandbox'ı kapatır (`-AcikBirak` ile açık kalır).
  Komut: `powershell -NoProfile -ExecutionPolicy Bypass -File tools\temiz-kurulum\Baslat.ps1`
- Sandbox Windows 11 imajıdır ve vGPU kapalı açılır (GPU sürücüsü yok, sistemde vulkan-1.dll yok).
  Windows 10 22H2 için temiz bir VM'de: `powershell -NoProfile -ExecutionPolicy Bypass -File Temiz-Kos.ps1 -Sonuc C:\sonuc`.
- CI: `.github/workflows/temiz-kurulum.yml` aynı betiği koşucularda koşturur (koşucu temiz değildir).
- Betikler UTF-8 BOM'lu: Windows PowerShell 5.1 BOM'suz betiği ANSI okur ve Türkçe yolu bozar.
