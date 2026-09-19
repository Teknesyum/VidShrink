# E9: Ölçek Modülü (Defter 23)

HandBrake'in `--modulus`'u bizde yoktu; ölçeklenen kenarı çift sayıya indiren kural
**üç dosyada ayrı ayrı** `private static int EvenDown` olarak yazılıydı ve çarpan üçünde
de 2'ye gömülüydü: `PlanCalculator`, `ComplexityProbe`, `FrameGrabber`.

Kural tek yere taşındı (`VidShrink.Core/Olcek.cs`) ve çarpan seçilebilir oldu: 2, 4, 8, 16.
Eski donanım kodlayıcıları 16'nın katını ister; 4:2:0 kroma yüzünden tek sayılı kenar zaten
kabul edilmez, o yüzden küme 2'den başlıyor ve varsayılan 2 — bugünkü davranış değişmedi.

## Değişen yüzey

| Ne | Nerede |
|---|---|
| `Olcek.Modul` / `Moduller` / `GecerliModul` | `src/VidShrink.Core/Olcek.cs` (yeni) |
| Üç `EvenDown` kopyası silindi | `PlanCalculator`, `ComplexityProbe`, `FrameGrabber` |
| `PlanOptions.ScaleModulus` | `PlanCalculator.cs` |
| `--modul` / `--modulus`, `error.bad-modulus` | `CliRequest.cs`, CLI `tr.json`/`en.json` |
| `CmbAdvModulus`, `AppSettings.AdvModulus` | Gelişmiş panel, 42 dil |

## Yolda çıkan üç kusur

Ölçü yazılırken çarpanın plana hiç ulaşmadığı görüldü. Sebep tek değildi:

1. **`effective`** — merdiveni besleyen ikinci seçenek nesnesi (`PlanCalculator.cs:492`)
   alanları elle sayıyor ve çarpanı taşımıyordu. Asıl sebep buydu.
2. **`WithTarget`** — seçenek kopyası da çarpanı düşürüyordu.
3. Aynı kopya **`Trim`, `KeepAllTracks`, `PlatformDelivery`, `PreferredLanguage`,
   `Filters`, `DetectedCrop`** alanlarını da düşürüyordu. Bu kopya kaliteden hedef MB arayan
   ikili aramayı besliyor (`TargetMbForQuality`); yani kesilmiş bir klibin hedefi **tam
   süreden**, izleri korunan bir kaynağın hedefi **tek ses izinden** hesaplanıyordu.

Üçüncüsü E9'un işi değildi, ölçü yakaladı. Tek alan yerine kural pimlendi:
`KopyaHicbirSecenegiDusurmuyor` yazılabilir her `PlanOptions` özelliğinin kopya
başlatıcısında adıyla geçtiğini sınar.

## Mutasyonlar

Filtre: `OlcekModuluTests|CliTests|GelismisAyarGidisDonusTests` — taban 0/112.

| # | Kesim | Kırmızı |
|---|---|---|
| M1 | `Olcek.Modul` aşağı yerine yukarı yuvarlıyor | 10 |
| M2 | Sıfır koruması (`deger <= modul`) kaldırıldı | 3 |
| M3 | Küme dışı çarpanın varsayılana düşmesi kaldırıldı | 2 |
| M4 | `Dimensions` çarpan parametresini yok sayıyor | 4 |
| M5 | `effective` çarpanı taşımıyor | 4 |
| M6 | `WithTarget` çarpanı taşımıyor | 1 |
| M7 | CLI'ın `GecerliModul` denetimi kaldırıldı | 3 |
| M8 | Gelişmiş paneldeki kutu seçeneğe yazılmıyor | 1 |

Kör nokta yok; sekiz kesimin sekizi kırmızı.

## Ölçünün kolları

`OlcekModuluTests` (20): aşağı yuvarlama tablosu, yukarı yuvarlamama, sıfır dönmeme,
varsayılanın bugünkü davranışı, kümenin tam olarak dört sayı olması, küme dışı sayının
elenmesi ve `Modul`'de varsayılana düşmesi; plan kenarlarının dört çarpanda da çarpanın
katına oturması ve olumsuz kontrol olarak 2 ile 16'nın **farklı** kenar vermesi (1918x1078
ikisi de 2'nin katı olduğu için bu kontrol olmadan ölçü çarpan hiç okunmasa da yeşil
kalırdı); CLI'ın iki yazımı, bayraksız koşumda varsayılanın kalması, küme dışı ve bozuk
değerin `error.bad-modulus` ile reddi; Gelişmiş panelin kaynak pimi; kopya bütünlüğü pimi.

## Yolda düzelen iki pim

- `ManualOverrideTests.K5_PlanOptionsKapaliSabitleriDisaAcmiyor` — yeni `ScaleModulus`
  yüzeye girdiği için listeye gerekçesiyle eklendi.
- `QualityHintTests.TheScorePathStartsNoProcess` — **E9'dan önce kırıktı.** Gövdeyi kesen
  son işaret `internal sealed record ShareTarget(` idi; o ikiz 5fa188de'de silinince pim
  gövdeyi bulamıyordu. İşaret bugünkü sınıra (`internal sealed class ShareFlow`) çekildi.
