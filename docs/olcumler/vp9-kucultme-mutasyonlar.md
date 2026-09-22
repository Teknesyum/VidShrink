# VP9 Küçültme Mutasyonları

Tarih: 23 Eylül 2026. Dal: `worktree-agent-a67ad4bb287086d58`.

Her mutasyon dosya yedeklenip tek satır değiştirilerek kuruldu, `dotnet build VidShrink.sln -c Release
-warnaserror -m:2` ile derlendi ve aşağıdaki dokuz sınıfın filtresiyle koşuldu; ardından dosya yedekten
geri kopyalandı (git checkout kullanılmadı).

Filtre: `Vp9KucultmeTests`, `PlanParserTests`, `FfmpegArgumentsTests`, `StreamMappingTests`,
`HandBrakeOnAyarCeviriTests`, `CliTests`, `MovKabiTests`, `FlacSesTests`, `CodecLockTests` — 217 test.
Taban: 0/217 kırmızı.

| # | Mutasyon | Dosya | Kırmızı | Kırmızıya dönen ölçü |
|---|---|---|---|---|
| M1 | `AllowedCodecs`'ten `libvpx-vp9` çıkarıldı | `Core/PlanParser.cs` | 1/217 | `PlanAyristiricisiVp9uKabulEder(libvpx-vp9)` |
| M2 | `-pass` yalnız 1. geçişte yazılıyor (2. geçişte yok) | `Core/FfmpegArguments.cs` | 1/217 | `IkiGecisArgumanlari` |
| M3 | WebM'de opus yerine aac kodlanıyor | `Core/StreamMapping.cs` | 5/217 | `WebmSesiOpusOluyor`, `KilitliVp9WebmVeIkiGecisVerir`, `IkiGecisArgumanlari`, `CanliIkiGecisWebm` (ffmpeg WebM'e aac yazamadı), `StreamMappingTests.GecisYalnizKapTasirsa…` |
| M4 | `ContainerFor`'un vp9 → WebM kolu kaldırıldı | `Core/StreamMapping.cs` | 2/217 | `KilitliVp9WebmVeIkiGecisVerir`, `CanliIkiGecisWebm` |
| M5 | `SpeedArgs`'ın vp9 kolu kaldırıldı (`-deadline/-cpu-used/-row-mt` yazılmıyor) | `Core/FfmpegArguments.cs` | 1/217 | `IkiGecisArgumanlari` |
| M6 | HandBrake çevirisinde `VideoEncoder: VP9` kilide dönmüyor | `Core/PresetLibrary.cs` | 1/217 | `Vp9KodlayicisiKilitOluyor` |

Altı mutasyonun altısı kırmızı. M5'te canlı kol yeşil kaldı: hız bayrakları yokken libvpx-vp9 kendi
varsayılanıyla yine kodluyor, o yüzden bu mutasyonu yalnız argüman ölçüsü yakalıyor.

Canlı kol (`CanliIkiGecisWebm`): 3 sn 320x240 `testsrc` + 48 kHz sinüs, hedef 0,4 MB, `-threads 2`, iki geçiş;
ffprobe `vp9` + `opus` ve biçim `webm` okuyor. Tabanda süre ~1 sn.
