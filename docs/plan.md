# Plan — Süre Biçimlendirmesinin Tek Gövdeye İnmesi (Kod Borcu 10)

Kaynak: `docs/inceleme/kod-borclari-2026-09-18.md` madde 10. Dokunulan dosya sayısı yediyi
geçtiği için K0 gereği önce bu plan.

## Ölçülen durum

On iki çağrı yeri sayıldı, dört ayrı biçim ailesi çıktı:

| Yer | Biçim | `InvariantCulture` |
| --- | --- | --- |
| `MainWindow.axaml.cs:3236` | `hh\:mm\:ss` | **yok** |
| `MainWindow.axaml.cs:4255` | `mm\:ss` | **yok** |
| `ShrinkJobWindow.axaml.cs:272` | `mm\:ss` | **yok** |
| `MainWindow.axaml.cs:4342` | `mm\:ss` | var |
| `MainWindow.axaml.cs:4400` | `h\:mm\:ss\.f` / `m\:ss\.f` | var |
| `CliApp.cs:445` | `h\:mm\:ss` / `mm\:ss` | var |
| `RecorderView.Serit.cs:48` | `hh\:mm\:ss` / `mm\:ss` | var |
| `PlayerView.Tools.cs:299` | `hh\:mm\:ss` | var |
| `PlayerView.Serit.cs:74` | şeritten devralıyor | var |
| `ControlStrip.axaml.cs:349` | elle `{00}:{00}:{00}` | yok (tamsayı) |
| `PlayerSettings.cs:99` | dosya adı için `00-00-00-000` | var |
| `ConversionArguments.cs:157` | ffmpeg için `hh\:mm\:ss\.fff` | var |

Aileler gerçekten ayrı: **ekran saati** (kullanıcıya gösterilen), **kalan süre**,
**ffmpeg argümanı** ve **dosya adı**. Hepsini tek biçime indirmek yanlış olurdu —
ffmpeg `hh:mm:ss.fff` ister, dosya adında iki nokta kullanılamaz.

## Yapılacak

1. `src/VidShrink.Core/Saat.cs`: beş adlandırılmış yüzey — `Ekran(TimeSpan, TimeSpan olcek)`
   (ölçek bir saati geçiyorsa `hh:mm:ss`, geçmiyorsa `mm:ss`), `Kalan(TimeSpan?)`
   (boşsa `-`), `Kesit(TimeSpan)` (ondalık saniyeli kırpma saati), `Ffmpeg(TimeSpan)`, `DosyaAdi(TimeSpan)`. Hepsi `InvariantCulture`.
2. On iki çağrı yerini bu dörde bağla. `ControlStrip` ve `PlayerView.Serit` ölçekten
   karar veren `Ekran`'ı kullanır; elle kurulan `{00}` birleştirmesi kalkar.
3. `SaatTests`: her yüzeyin sınırı (59:59 → 1:00:00 geçişi), negatif ve sonsuz girdi,
   boş kalan süre, ffmpeg biçiminin milisaniyesi, dosya adında iki nokta olmaması.
   İkinci gövde tarayıcısı — `YolEsitligiTests.IkinciGovdeYok` deseninin eşi — kaynakta
   kalan elle `\:mm\:ss` yazımı bırakmadığını okur.
4. Mutasyon: ölçek kolunu sabitlemek ve `InvariantCulture`'ı düşürmek.

## Yürütme sonucu

1-3 yapıldı, `SaatTests` 21/21. Bulgu on iki çağrı yeri saymıştı, kaynak taraması
**on dört** buldu (`MainWindow.axaml.cs:4340` deneme süresi ve `:4398` kesit saati de
aynı deseni yazıyordu); yüzey sayısı dörtten beşe çıktı.

Mutasyon ölçümü:

- Ölçek kolunu değere sabitlemek → `OlcekBicimiSeciyorDegerDegil` kırmızı (1/21).
- `InvariantCulture`'ın on üç geçişini `CurrentCulture` yapmak → **21/21 yeşil kaldı.**
  Dürüst hüküm: bu kod yolunda `InvariantCulture` bugün yük taşımıyor, çünkü .NET
  tamsayı biçimlerinde yerel rakam kullanmıyor ve TimeSpan'in kaçışlı ayraçları
  kültürsüz. Kültür kolu ileride kültüre duyarlı bir biçim sızarsa diye bekçi olarak
  bırakıldı, bulgunun "üç yerde InvariantCulture yok" kısmı ise gerçek bir tutarsızlıktı
  ve kapandı.

## Sırası

Bu plan, main CI 749ea1d8 hükmü okunduktan **sonra** yürütülür; kırmızı bir main
üstünde yedi dosyaya dokunmak teşhisi bulandırır.
