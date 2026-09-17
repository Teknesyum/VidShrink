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

## Sonuç — Kapıdan Kaldı

Koşum **35249123754** (`handbrake-kiyas`, `isler=vthizli`, `vt=false`), `macos-15`, commit `4e65f579`. Ham çıktı
artefakt `hb-sonuc-vthizli` (`vthizli.json`, `vthizli-hukum.json`). ffmpeg listesinde `hevc_videotoolbox` var.

| Hücre | VMAF-NEG vt-hizli | yazılım (kodek) | HB VT | K2 fark | K5 fark | vt sn / HB sn | Bant (kbps) | XPSNR fark yaz. / HB |
|---|---|---|---|---|---|---|---|---|
| karanlık 2000 | 91,70 | 95,97 (x265) | 88,60 | **−4,27** | +3,10 | 3,7 / 6,0 = 0,62 | evet (1953) | −1,33 / −0,09 |
| karanlık 5500 | 99,06 | 99,36 (x265) | 98,71 | **−0,300** (−0,30026) | +0,35 | 6,4 / 7,1 = 0,90 | evet (5351) | −0,69 / −0,12 |
| parlak 2000 | 87,11 | 91,59 (svt-av1) | 85,83 | **−4,48** | +1,28 | 7,0 / 6,4 = 1,09 | evet (1923) | −1,47 / −0,25 |
| parlak 5500 | 94,86 | 94,99 (svt-av1) | 94,11 | −0,13 | +0,75 | 6,8 / 5,7 = 1,19 | **hayır** (4792) | −0,05 / −0,07 |
| hareketli 2000 | 94,32 | 97,50 (svt-av1) | 91,74 | **−3,17** | +2,58 | 3,5 / 7,1 = 0,49 | evet (1948) | −1,31 / +0,07 |
| hareketli 5500 | 99,27 | 99,48 (svt-av1) | 99,10 | −0,22 | +0,17 | 4,3 / 6,9 = 0,62 | evet (5305) | +0,18 / −0,16 |
| ekran 2000 | 92,67 | 97,26 (svt-av1) | 84,99 | **−4,59** | +7,68 | 2,5 / 3,9 = 0,64 | **hayır** (1276) | −17,57 / +7,99 |
| ekran 5500 | 93,59 | 97,32 (svt-av1) | 84,86 | **−3,73** | +8,74 | 3,0 / 3,7 = 0,81 | **hayır** (1330) | −18,21 / +9,24 |

| # | Sonuç | Hüküm |
|---|---|---|
| K1 | 8/8 `hevc_videotoolbox` | geçti |
| K2 | 2/8 ≥ −0,3 (en kötü −4,59) | **kaldı** |
| K3 | 8/8 ≤ 1,5 (en yüksek 1,19) | geçti |
| K4 | 5/8 bantta, tavan aşımı 0 | **kaldı** |
| K5 | 8/8 ≥ −0,3 (en düşük +0,17) | geçti |
| N1 | `-foo 1` çıkış 8, `Unrecognized option 'foo'` | geçti |

VT, HandBrake'in VT'sinden her hücrede iyi ve ondan hızlı; ama bugünkü yazılım yoluna göre düşük bit hızında 3-4,6
VMAF-NEG geride ve üç hücrede banttan düşüyor. Ekran kesitinde kodlayıcı 5,4 Mbit isteğine ~1,3 Mbit veriyor
(`the encoder did not answer the bitrate`, doygun). Yazılım kolunun çıplak kodlama süresi 23-152 sn.

**Geri alındı:** plan yolu bağlantısı (`PlanCalculator` mac aday sırası ve CRF→tek geçiş, `CodecModel.IsFastHardware`,
`HardwareVerdict`, `PlanParser` VT kapısı, `MainWindow.HardwareAvailableFrom` tek satırı, `VideoToolboxHizliTests`)
`4c161f31` haline döndü. `PlanParserTests.ParserStillRejectsVideoToolboxEncoders` ve `OluUyeTests.TheGateStaysClosed`
yeniden kapının kapalı olduğunu pimliyor. Ölçüm düzeneği kalıyor: `hb.ps1 -Is vthizli`.

Bağlantı açıkken birim testleri ve mutasyonlar (13 mutasyonun 13'ü en az bir testi kırmızı yaptı, hepsi geri alındı)
`4e65f579` commit'inde duruyor. Yan gözlem: bench komutunda VT için `-pass 2 -passlogfile` yazılıyor; VT tek geçiş,
bayrak sonucu değiştirmedi ama argüman üretiminde temizlenmesi ayrı iş.
