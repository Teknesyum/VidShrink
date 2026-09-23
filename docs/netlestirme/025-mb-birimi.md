# 025 — Hedef Boyutun Birimi (O2, MB/MiB): Fable'ın Yanıtı

**Karar: (a) — motor ondalık MB'a (1000²) geçsin, etiket "MB" kalsın; uguu yongası 128 → 134 MB.**

Gerekçe:
1. Sınırı aşmama en ağır şart. Ondalık MB, MiB'den %4,9 küçük; platform sınırı hangi birimdeyse hedef her iki okumada da altında kalır. (b) tersini yapar: Gmail/Discord sınırı ondalıksa 25 "MiB" = 26,2 M bayt reddedilir.
2. Kaynaklarda hiçbir platformun bayt değeri yok — hepsi "MB" yazıyor (`docs/olcumler/onayar-kaynaklari.md` Discord "20MB", Gmail "25 MB", Outlook "20 megabytes"), WhatsApp 16 doğrulanmamış. Ölçülmüş tek sınır uguu.se `maxBytes 134217728` = 128 MiB; o da 134 000 000 bayt < tavan olduğundan 134 MB yongayla korunur.
3. ffmpeg `-b:v Nk` zaten 1000 tabanlı (model-strateji §2.1); tek katsayı `8000` iki gizli düzeltmeyi (8192→8388,608, ContainerOverhead) sadeleştirir, hedefin birimi ilk kez tanımlı olur.
4. Gezgin farkı hangi seçenekte olursa olsun bir tarafta kalır: (a)'da Windows kullanıcısı 25 hedefine "23,8 MB" görür (güvenli tarafta, açıklanabilir), (b)'de macOS kullanıcısı "26,2 MB" görür ve sınırı aşmış olur. Güvenli yön (a).
5. (b) yalnız etiket değiştirir ama 42 dilde "MB"→"MiB" + kullanıcı ayarında kayıtlı sayının anlamı değişmez; (a) kayıtlı hedef sayısını değiştirmez, yalnız üretilen baytı küçültür — geriye uyumlu.

Değişecek yerler:
- `src/VidShrink.Core/PlanCalculator.cs:194` `KbitPerMib = 8388.608` → `KbitPerMb = 8000` (kullanımlar :454, :655, :1046, :1208, :1240, :1246 adla gelir)
- `src/VidShrink.Core/MediaInfo.cs:87` `FileSizeMb` `/1024/1024` → `/1e6`
- `src/VidShrink.Ffmpeg/EncodeRunner.cs:333` actualMb, `:453` targetBytes; `src/VidShrink.Ffmpeg/DiskSpaceGuard.cs:9`
- `src/VidShrink.Core/RecorderArguments.cs:1095` `LimitBytes`, `src/VidShrink.Core/RecorderBudget.cs:52`
- `src/VidShrink.Core/Presets/platformlar.json` share-uguu `targetMb 128→134`, chip adı `Chip128`; `main.chip.128.tip` 42 dilde "128 MiB"→"134 MB"
- `docs/olcumler/onayar-kaynaklari.md:11` "MB'ı MiB'dir" cümlesi ve share-uguu satırı
- Testler: `AdvancedPanelTests`, `AyarKaliciligiTests`, `B1KalanGirdi`, `BicimTests`, `BiciminTests` (1024²/MiB pimleri), `YongaDegerleriDegismedi`, `BelgeTablosuVeriyleAyni`
- `tools/VidShrink.Bench` aynı katsayıya geçmeli; `docs/olcumler` altındaki eski MiB sayılarıyla kıyas geçersiz (ölçerin sürüm ofseti notu)
- Dokunulmayacak: `CalibrationProbe.cs:305` (ffmpeg çıktı sonekini ayrıştırıyor, ffmpeg'in birimi), `Gunluk.cs:19`, `UpdateCheck.cs`

Olumsuz kontrol: (1) `PlanCalculator.SizeMb(8000k toplam, 1 s)` tam `1.0` döner; eski katsayıyla 0,954 verip kırmızı olmalı. (2) `EncodeRunner` 25 hedefinde `targetBytes == 25_000_000`, `26_214_400` ise kırmızı. (3) Sentetik kodlamada çıktı `FileInfo.Length ≤ targetMb*1e6`. (4) 42 dilin `main.json`'unda "MiB" geçmiyor; `share-uguu targetMb*1e6 < 134217728`.

Belirsiz: Discord/Gmail/WhatsApp sınırının gerçek bayt değeri hiçbir kaynakta ölçülmedi (Discord'un eskiden 8 388 608 bayt = 8 MiB uyguladığını hatırlıyorum, doğrulanmadı). (a) bu belirsizlikte güvenli taraftır; bedeli MiB uygulayan platformlarda %4,9 kalite payı.
