# HandBrake Karşılaştırma Belgeleri

Bu klasör 17 Eylül 2026 HandBrake açık envanterinin kaynak belgeleridir. Daha önce
`.calisma/hb3/` ve `.calisma/handbrake/` altındaydı, yani git'in dışındaydı; `docs/plan.md`
dört bölümünde bu belgelere atıf yaptığı için 18 Eylül 2026'da buraya taşındı.

| dosya | ne |
|---|---|
| `acik-analizi.md` | ilk açık analizi, 63 satırlık madde listesi |
| `acik-durumu-2026-09-17.md` | açıkların 17 Eylül'deki durumu; `docs/plan.md` bunun satır numaralarına atıf yapar |
| `yol-haritasi-kalanlar-2026-09-17.md` | yol haritasından kalanlar, bölüm/satır numaralarıyla |
| `handbrake-yanit.md` | HandBrake tarafının yüzeyleri |
| `fable-kararlar-2026-09-17.md` | fable danışmasının kararları (K/B numaraları) |
| `ortak-kurallar.md` | dalgaya giren ajanların ortak kuralları |

~~**Bilinen borç.** HandBrake tarafı ham `HandBrakeCLI --help` çıktısından değil, WebFetch
özetinden tarandı ve karşılaştırılan HandBrake sürümü bir commit'e sabitlenmedi.~~
**Kapandı 19 Eylül 2026:** döküm alındı ve envanter onunla karşılaştırıldı —
`envanter-tamlik-2026-09-18.md`, kaynak `HandBrakeCLI 1.11.2`'nin kendi `--help` çıktısı
(737 satır, ağa çıkılmadan). Bu belgedeki bu paragraf dökümden sonra güncellenmemişti.
