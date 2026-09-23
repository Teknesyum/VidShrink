# Plan — Media Foundation Kodlayıcıları (HB #15)

Kapsam: `h264_mf`, `hevc_mf`, `av1_mf` nvenc/qsv/amf ailesinin yanına, yalnız Windows'ta ve
yalnız elle seçilir. Ölçüler `docs/olcumler/hb15-media-foundation.md`.

1. `CodecModel` — `EncoderVendor.MediaFoundation` (`_mf` soneki), `IsHardware` kapısına girer
   (tek geçiş, taban, pay, tepe), `TakesPreset` yok, `HasQualityScale` yok, `QualityArgs`
   açıkça patlar, bit hızı kolu `-rate_control pc_vbr`, piksel biçimi `nv12`,
   `IsOfferedOn(codec, windows)` Windows dışında MF'yi hiç önermez.
2. `FfmpegArguments` — ön ayar tablosunda tek basamak `default`; `SpeedArgs` MF'ye
   `-hw_encoding 1` yazar (donanım MFT'si, yazılım MFT'sine düşmesin); `OfferedCodecs`.
3. `PlanParser.AllowedCodecs`, `PlanCalculator.KnownLockableCodecs` — MF girer;
   kilit Windows dışında reddedilir. `FastHardwareOrder`'a **girmez**.
4. `PlanCalculator` — MF planı CRF kipine düşerse 2pass'e çevrilir (VP9 emsali),
   `ReasonCode.MfQualityUnmeasuredBitrate`, anahtar 42 dilde.
5. `EncoderCapabilities.RunProbe` — MF yoklaması `-pix_fmt nv12 -hw_encoding 1` ile.
6. `CalibrationProbe.QualityArgs` — MF için `-crf` yerine patlamaz; örnekleme MF'de koşmaz.
7. Arayüz — `CmbAdvCodecLock` `OfferedCodecs`'ten dolar; `LanguageCatalog.Verbatim`.
8. Testler — `MediaFoundationTests.cs`: argüman, Windows dışı olumsuz kontrol, canlı kol.
9. Belgeler — ölçüm belgesi, durum belgesi §1/§2/§3, tests AGENTS.md maddesi.
