# HandBrake Dalga 2 — Danışma Sorusu

Tarih: 17 Eylül 2026. Soran: T0 (dal `t0/hb-2-aciklar`). Danışman: fable.
Dayanak: koşum 35158725446, `docs/olcumler/handbrake-kiyas-b1..b6*.md`, önceki yanıt `C:\Users\Administrator\Desktop\Projeler\VidShrink\.calisma\danisma\handbrake-yanit.md`.
Yerelde ağır kodlama yok; her aday CI'da eşit baytta ölçülecek. Senden kod değil, ölçülecek kolların ve karar kurallarının seçimi isteniyor.

## Soru 1 — SVT-AV1 karanlık bantlaşma
Ürün: libsvtav1 preset 6, 2pass, 8 bit yuv420p, `tune=1:enable-variance-boost=0`, `keyint=max:scd=1`.
B3/B2: karanlık kesitte SVT-AV1 CAMBI ~8,3–9,3, HB x265 (8 bit) ~6,0–7,2. karanlik 1200 kbit: e0 CAMBI 9,241, e1 (variance boost açık) 8,833 ama VMAF-NEG −2,74.
rampa (dither'lı düz gradyan) CAMBI tüm kollarda 23,3–23,7: kodlayıcı dither'ı siliyor.
Önerdiğim kollar (eşit bayt, karanlik+rampa, 300/1200 kbit): 8 bit taban; 10 bit (yuv420p10le); film-grain=8 + film-grain-denoise=0 (8/10 bit); 10 bit enable-qm=1; 10 bit variance-boost-strength=1. Negatif kontrol: uydurma anahtar taban ile bayt-eş.
Sorular: (a) kol listesi eksik/fazla mı (qm-min, sharpness, ac-bias, tf, psy-rd)? (b) 10 bit ölçerken ölçer 8 bite indiriyor, CAMBI hükmü geçerli mi? (c) seçim kuralı: CAMBI düşüşü hangi VMAF-NEG/XPSNR kaybına kadar kabul?

## Soru 2 — libx265 turbo ilk geçiş
Ürün turbo: ilk geçiş veryfast. HB turbo'ya göre −1,84 (HB ≤0,29). Kollar: 2pass slow, ilk geçiş veryfast/faster/fast/medium/slow ve slow + `slow-firstpass=0`; süre kaydı. Negatif kontrol: uydurma x265 anahtarı "Unknown option" basar.
Soru: güvenli ilk geçiş seçimi için eşik (kalite kaybı ≤? ve süre kazancı ≥?) ne olmalı?

## Soru 3 — Düşük hedefler
(a) SVT-AV1 karanlik 100 kbit hedef 0,117 MB: e0 üç denemede 0,137/0,134/0,134 MB, e1 98k/29k/12k istekte 0,392 MB; dosya teslim edilmedi (CeilingExceeded). Bitrate düşüşü boyutu küçültmüyor (doygunluk).
Önerim: 2. deneme hâlâ üstteyse ve verim < 0,5 ise sonraki deneme çözünürlük basamağı iner (sonra ses bütçesi); daha küçük düzen varken dosyasız bitmez. Önceki yanıtın karar 10'u: verim < 0,5 → yeniden deneme yok, doygun teslim. Tavan üstü doygun teslim ile çözünürlük basamağı çelişiyor: hangisi?
(b) rampa e1 300 kbit: 116k istek → ~10 kbit gerçekleşme, %3,3 doluluk "bant altı kabul" ile teslim. Önerim: doluluk < %50 ise bir yeniden deneme (bitrate yukarı, CBR/VBV ya da CRF kolu). Taban eşiği?

## Soru 4 — FPS düşürme
hareketli 300 kbit: otomatik plan 16 fps → HB'ye göre −5,78 VMAF-NEG; düşürmeyen kol +10,99 (düşürmeye göre +16,77). Önceki karar 3: otomatik plandan çıkar, yalnız kullanıcı isterse ya da kaynak fps'te kodlayıcı açılamıyorsa (RunnableVideoBitrateK altı). Uygulama: kaynak fps'te hiçbir ölçek çalışabilir değilse aday listesine düşük fps girer. Onay?

## Soru 5 — Social bütçe doldurma
Social 25 MB 30 sn 1080p60: ürün HB'den %3–4 az bayt, parlak −0,64 VMAF-NEG / −0,45 XPSNR, karanlik XPSNR −0,25.
Kök neden adayı: FillBand hedefi bant merkezi (10–50 MB arası alt 0,95 → merkez 0,975; 10 MB altı alt 0,92 → merkez 0,96), üstelik ölçülmemiş verimde `target/(1+0,012)` ile kırpılıyor.
Soru: ilk denemede hedef merkez yerine `target/(1+0,012)` mi olmalı, yoksa yalnız verim ölçüldüğünde üst yarı mı? Tavan aşımı riski ile doluluk arasında kural?

## Soru 6 — VideoToolbox
Ürün: `hevc_videotoolbox -preset slow -b:v 1956k -maxrate 2934k -bufsize 3912k -pass 2 -passlogfile`, 8 bit; B6'da XPSNR −0,54…−1,17 dB, karanlik CAMBI 9,06 vs HB 2,84; ekranda bant altı %62,2/%23,9.
HB preset: `vt_h265_10bit`, Preset speed, ABR, tek geçiş. VT yazılım gibi planlanıyor (NeedsTwoPasses true, -preset yazılıyor, ffmpeg VT'de -pass'ı yok sayar). T149: VT IsHardware dışında kalmalı (NVENC sabitleri taşınmasın).
Kollar (macos-15, 4 kesit, 2000/5500 kbit, HB VT ile eşit bayt): bugünkü ürün; 8 bit tek geçiş -b:v; 10 bit `-profile:v main10 -pix_fmt p010le`; 10 bit + `-prio_speed 0/1`; spatial_aq yalnız `ffmpeg -h encoder=hevc_videotoolbox` listeliyorsa. Negatif kontrol: uydurma VT seçeneği hata verir.
Sorular: (a) maxrate/bufsize kalmalı mı (VT'de ABR sınırı)? (b) ekran bant altı için VT'ye ayrı verim düzeltmesi mi, yoksa yalnız ölçülmüş verimle Correct mi? (c) eksik kol?

Her cevap için: karar, dayanak (hangi ölçüm), CI'da nasıl doğrulanacağı, negatif kontrol.
