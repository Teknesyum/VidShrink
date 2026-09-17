# VideoToolbox Hızlı Kip — Plan Yolu Kapısı

**Tarih:** 17.09.2026 · **Dal:** `t0/vt-hizli` · **Karar:** `fable-kararlar-2026-09-17.md` soru 1

Bu bölüm ölçümden **önce** commit'lendi; sonuç bölümü ölçümden sonra eklenir, kapı değişmez.

## Ne Değişiyor

`hevc_videotoolbox` plan yoluna yalnız NVENC'in girdiği yerden girer: **Hızlı kip** (`SpeedMode.Fast`),
**macOS**, yoklama kodlayıcıyı buluyor. Otomatik/Dengeli/Kaliteli (`SpeedMode.Quality`) yazılımda kalır.
`h264_videotoolbox` girmez. Windows/Linux'ta aday listesinde yok.

## Kapı

Koşucu: GitHub `macos-15` (Apple M1 sanal, `hevc_videotoolbox` koşum 35166699260 `vt (vtara)` işinde listelendi).
İş: `handbrake-kiyas.yml`, `vthizli`. Hücreler: `karanlik`, `parlak`, `hareketli` (film) ve `ekran` × 2000, 5500 kbit
= **8 hücre** (6 film + 2 ekran). Hedef MB = kbit × süre.

Her hücrede üç kol, aynı kesit, aynı hedef MB:

- **vt-hizli** — `bench shrink --speed fast`, kodek zorlaması **yok**. Plan yolunun seçtiği kodek okunur.
- **yazilim** — `bench shrink --speed quality` (bugün macOS kullanıcısının aldığı yazılım yolu), kodek zorlaması yok.
- **handbrake-vt** — HandBrakeCLI 1.11.2 `H.265 Apple VideoToolbox 1080p`, vt-hizli çıktısıyla eş bayt.

Hüküm, hepsi birlikte:

| # | Ölçüt | Eşik |
|---|---|---|
| K1 | vt-hizli kolunun kodeki `hevc_videotoolbox` | 8/8 |
| K2 | VMAF-NEG (ortalama) vt-hizli − yazilim | ≥ −0,3, 8/8 |
| K3 | Hız: vt-hizli çıplak kodlama sn / handbrake-vt kodlama sn | ≤ 1,5, 8/8 |
| K4 | Hedef bant isabeti: vt-hizli `InBand` | 8/8, tavan aşımı 0 |
| K5 | VMAF-NEG vt-hizli − handbrake-vt (karar 1 (c)) | ≥ −0,3, 8/8 |
| N1 | Negatif: `hevc_videotoolbox -foo 1` reddedilir | çıkış ≠ 0 |

XPSNR(ii) tabloya girer, hükme girmez. Bir ölçüt kalırsa: plan yolu bağlantısı geri alınır (VT aday listesinden
çıkar), sonuç burada yazılır.

K2 notu: T0'ın görev tanımı "yazılım yoluna göre, eş hedef"; karar 1 (c) "HB VT'ye karşı". İkisi de hükme girer.
