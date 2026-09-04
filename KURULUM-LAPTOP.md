# Laptop kurulumu — `D:\!Tmp\VidShrink`

Bu dosya devir notunun (`DEVIR.md`) eşidir: o **ne yapılacağını**, bu **makinenin nasıl
hazırlanacağını** anlatır. İkisi de iş bitince `trash/`'a gider.

Kabuk **Windows PowerShell**. Komutlar Run düğmesinden koşacak şekilde yazıldı.

---

## 1. Depoyu al

```powershell
if (-not (Test-Path 'D:\!Tmp')) { New-Item -ItemType Directory 'D:\!Tmp' | Out-Null }
git clone https://github.com/Teknesyum/VidShrink.git 'D:\!Tmp\VidShrink'
cd 'D:\!Tmp\VidShrink'
git log --oneline -1
```

Son commit `c72ba30 Devir notu ve kodek kalibrasyon olcumleri agaca alindi` olmalı.

Depo zaten varsa:

```powershell
cd 'D:\!Tmp\VidShrink'; git pull; git log --oneline -1
```

---

## 2. Zorunlu araçlar

| araç | masaüstündeki sürüm | niçin |
|---|---|---|
| .NET SDK | 9.0.316 (hedef `net8.0`) | derleme ve test |
| ffmpeg | 9.0-full_build (gyan.dev) | kodlama ve ölçüm |
| ffprobe | ffmpeg ile gelir | künye, paket boyutu |
| SVT-AV1 | v4.2.0-68-gc1e79b04f | AV1 kodlayıcı, ffmpeg içinde |
| HandBrakeCLI | 1.11.2 | A/B düzeneğinin karşı tarafı |
| git | 2.55.0 | — |

ffmpeg şu üçüyle derlenmiş olmalı, yoksa ölçüm hiç koşmaz:
`--enable-libvmaf --enable-libzimg --enable-libsvtav1` (ayrıca `libx264`, `libx265`).

**Sürüm uyuşmazlığı sessiz bir tuzaktır.** Aynı bayta iki farklı VMAF puanı çıkar; bu
varyans değil sabit ofsettir, tekrarla küçülmez. Laptopta farklı sürüm varsa oradan çıkan
sayılar `docs/olcumler/` altındaki tabloyla **aynı sütunda kıyaslanamaz** — ya sürümü
eşitle ya yeni ölçümü ayrı tablo olarak aç.

### Denetim

```powershell
dotnet --version
ffmpeg -hide_banner -version | Select-Object -First 1
ffmpeg -hide_banner -buildconf | Select-String 'libvmaf|libzimg|libsvtav1|libx264|libx265'
HandBrakeCLI --version 2>&1 | Select-String '^HandBrake'
```

SVT sürümünü görmek için (ekrana `SVT [version]` satırı basar):

```powershell
ffmpeg -hide_banner -y -f lavfi -i testsrc=d=0.1:s=64x64 -c:v libsvtav1 -f null - 2>&1 | Select-String 'SVT \[version\]'
```

### Yoksa kurulum

```powershell
winget install --id Gyan.FFmpeg.Full -e
winget install --id HandBrake.HandBrake.CLI -e
winget install --id Microsoft.DotNet.SDK.8 -e
```

`winget` ffmpeg'i `Gyan.FFmpeg` (kısa) olarak da sunar; **`Full` olanı seç** — `libvmaf`
yalnız full derlemede var. Kurulumdan sonra PowerShell'i kapat aç, `PATH` tazelensin.

---

## 3. Derle ve testi koştur

```powershell
cd 'D:\!Tmp\VidShrink'; dotnet build -c Release
```

```powershell
cd 'D:\!Tmp\VidShrink'; dotnet test
```

Tamamı yeşil olmadan teslim yok. **`--no-build` kullanma** — eski ikiliyi koşturup yeşil
okuma üretiyor; ölçtüğü commit yazdığın commit olmuyor.

Uygulamayı çalıştırmak (pencere açar, ekran kapısı gerekir — `/ekran <dakika>`):

```powershell
cd 'D:\!Tmp\VidShrink'; dotnet run --project src\VidShrink.App -c Release
```

---

## 4. Ölçüm kaynakları — git'te yok, elle taşınacak

`.calisma/` `.gitignore`'da. Ölçüme devam edilecekse kaynaklar masaüstünden kopyalanmalı.

Kaynak makine: `C:\Users\Administrator\Desktop\Projeler\Vidshrink\.calisma\`

### Öncelikli küme (~420 MB) — bunlar olmadan hiçbir ölçüm koşmaz

| dosya | boyut | hedef klasör |
|---|---|---|
| `parca-1.mkv` | 88 MB | `.calisma\kaynak\` |
| `parca-2-yalniz-video.mkv` | 109 MB | `.calisma\kaynak\` |
| `parca-3-yalniz-video.mkv` | 94 MB | `.calisma\kaynak\` |
| `genis-1-animasyon.mkv` | 38 MB | `.calisma\kaynak-genis\` |
| `genis-3-hareket.mkv` | 45 MB | `.calisma\kaynak-genis\` |

`genis-2-gren.mkv`'yi taşımaya değmez — Fable gren sınıfını temsil etmediğini söyledi
(bkz. `DEVIR.md` §4).

### İkincil (~1,2 GB) — gren sınıfı gerektiğinde

`genis-4-gren2.mkv` (1.158 MB). Gren sınıfını tek başına taşıyan klip bu; merdiven
koşulacaksa gerekli, önce gelmesi şart değil.

### Tam arşiv (~5,3 GB)

`kaynak-1080p60-hdr-17dk.mp4` ve `-yalniz-video.mkv` — parçaların kesildiği ham kaynak.
Yeni parça kesilmeyecekse taşıma.

### Klasörleri açan komut

```powershell
cd 'D:\!Tmp\VidShrink'; New-Item -ItemType Directory -Force '.calisma\kaynak', '.calisma\kaynak-genis', '.calisma\kodek-matris' | Out-Null; Get-ChildItem '.calisma'
```

**Not:** `.calisma\kaynak-genis` klipleri Blender (BBB, Tears of Steel) ve Xiph
(`old_town_cross_1080p50`) açık kaynaklarından türetildi, ama **kesme komutları kayda
geçmedi**. Yeniden indirmek yerine dosyaları kopyala; aksi halde farklı kesim farklı
sayı üretir ve mevcut tabloyla kıyaslanamaz.

---

## 5. Ölçüm düzeneğini koşturma

Betikler bash. Windows'ta Git Bash ile koşar, **proje kökünden**:

```powershell
cd 'D:\!Tmp\VidShrink'; bash tools/kodek-matris/kos-tavan.sh
```

Ne hangi betikte, `tools/kodek-matris/AGENTS.md` söylüyor. Çıktı
`.calisma/kodek-matris/` altına yazılır.

Sıradaki koşum çözünürlük tavanı (`kos-tavan.sh`) — masaüstünde başlatıldı, bitmedi.
Kodlama yapmaz, yalnız ölçekleyip VMAF koşar; ucuzdur.

**İki kodlamayı aynı anda başlatma.** ffmpeg sıralı koşar; eşzamanlı iki kodlama hem
süre hem kalite sayılarını bozar.

A/B düzeneği (rapora giren `harm` sütununu üreten araç):

```powershell
cd 'D:\!Tmp\VidShrink'; dotnet run --project tools\VidShrink.Ab -c Release -- denetle <referans> <aday>
```

---

## 6. Devam etmeden önce oku

1. `DEVIR.md` — nerede kaldık, ne yanlış çıktı, sırada ne var
2. `docs/olcumler/kodek-matris.md` — ölçüm kaydı
3. `docs/danisma/003-fable-taban-kucultme-vb.md` — son danışma turu
4. `AGENTS.md` — proje düzeni ve dal kuralı

Kullanıcı "devam" dediğinde sıra `DEVIR.md` §5'tir: statik/dinamik kodek seçimi,
önizleme panelindeki baştan başlama kusuru, README yenilemesi. Ölçüm ve
mükemmelleştirme sonraya bırakıldı.

---

## 7. Dal kuralı

`main`e yalnız T0 birleştirir. Laptopta iş dalı aç:

```powershell
cd 'D:\!Tmp\VidShrink'; git switch -c serkan/laptop-devam
```

Masaüstünde `main`, ana ağaçta değil `.claude\worktrees\T0` altında duruyor. Laptopta
öyle bir kurulum yoksa `main` doğrudan çalışma ağacında olur — o zaman `main` üstünde
yazma, yukarıdaki gibi dal aç.
