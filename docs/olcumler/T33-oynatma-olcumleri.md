# T33 — Oynatma mimarisi ölçümleri

**Tarih:** 24.08.2026 · **Sözleşme:** `.claude/relay/contracts/T33.md` · **Dayanak:** T30, T32

İki aday ölçüldü: **Aday A**, tek ffmpeg süreci + `hstack` + BGRA boru. **Aday B**, libmpv
render API. Karar kuralı sözleşmede; bu belge yalnız sayı üretir. `src/` altına tek satır
yazılmadı.

## Ortam

| Alan | Değer |
|---|---|
| Makine | DESKTOP-630ME6G, Windows 11 Pro 26100, 16 mantıksal çekirdek |
| ffmpeg | 9.0-full_build (gyan.dev), WinGet `Gyan.FFmpeg` |
| .NET | 8.0 (`%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe`) |
| Ölçüm komutu | `bench play <klipA,klipB> --only k2,p1,p2,p3,p5,p6` |

### Test klipleri

T30/T32 klipleri 30 fps'ti; oynatma kapısı 60 fps sorduğu için **60 fps kaynak** üretildi.
Aynı `testsrc2` yolu, `%TEMP%\vidshrink-play` altına:

```
ffmpeg -f lavfi -i "testsrc2=size=1920x1080:rate=60:duration=20" -c:v libx264 -preset veryfast -crf 23 -pix_fmt yuv420p 1080p_h264.mp4
ffmpeg -f lavfi -i "testsrc2=size=1920x1080:rate=60:duration=20" -c:v libx265 -preset veryfast -crf 25 -tag:v hvc1 -pix_fmt yuv420p 1080p_hevc.mp4
ffmpeg -f lavfi -i "testsrc2=size=1920x1080:rate=60:duration=20" -c:v av1_amf -b:v 6M -pix_fmt yuv420p 1080p_av1.mp4
```

4K karşılıkları `size=3840x2160`, av1 için `-b:v 20M`. Altı klip: 1080p ve 4K × h264,
hevc, av1. `testsrc2` yüksek entropili, yani kod çözme maliyeti kötümser tarafta.

## K2 — Bu makinedeki ffmpeg'de gerçekten ne var

Liste sorgusuyla değil, her yetenek için küçük bir girdiyle grafik kurup stderr'de
`Error parsing option` / `No such filter` aranarak sınandı.

| Yetenek | Deneme | Sonuç | stderr |
|---|---|---|---|
| `hstack` | iki lavfi girdi, `hstack=inputs=2` | **var** | — |
| `scale` + `format=bgra` | `scale=64:36,format=bgra` | **var** | — |
| `fps` | `fps=60` | **var** | — |
| `zscale` | `zscale=w=32:h=32` | **var** | — |
| `tonemap` | `setparams=HDR` → `zscale=t=linear,tonemap=hable,zscale=t=bt709` | **var** | — |
| rawvideo/bgra boru | `-f rawvideo -pix_fmt bgra -` | **var** | — |
| `-hwaccel d3d11va` | gerçek dosyada tek kare | **var** | — |
| `-hwaccel dxva2` | gerçek dosyada tek kare | **var** | — |
| `-hwaccel qsv` | gerçek dosyada tek kare | yok | `Failed to find d3d11va adapter by vendor id 0x8086` |
| `-hwaccel cuda` | gerçek dosyada tek kare | **var** | — |
| `-hwaccel vulkan` | gerçek dosyada tek kare | **var** | — |
| `d3d11va` + `hwdownload` | `-hwaccel_output_format d3d11 -vf hwdownload,format=nv12` | **var** | — |

Grafiğin ihtiyaç duyduğu her filtre bu makinede mevcut. `qsv` yok, çünkü makinede Intel
grafik yok — kalan dört hızlandırıcı çalışıyor.

**Tuzak:** `tonemap` ilk denemede "yok" göründü. Sebep filtre eksikliği değildi;
`testsrc2` SDR olduğu için `zscale` `no path between colorspaces` dedi. T32'nin bulgusuna
uyup girdi `setparams=color_primaries=bt2020:color_trc=smpte2084:colorspace=bt2020nc` ile
HDR etiketlendiğinde filtre çalıştı. Çıkış seçeneği olarak verilen
`-color_trc`/`-color_primaries` libx264/libx265 üzerinden sessizce düşüyor; HDR sınaması
`setparams` olmadan sahte negatif verir.
