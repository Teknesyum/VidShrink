# Alıntı denetimi — bugünün sayısı ve aracın ölçüsü

T124. Belge bir şeyi ters tırnak içinde "birebir" diye veriyorsa, o dizge kaynak
dosyada gerçekten var mı? Bu soru deterministik olarak yanıtlanır. Araç:
`tools/alinti-denetimi/alinti-denetimi.py`, Python 3, bağımlılık yok, model yok.

Ağaç: `T124-alinti-denetimi`, taban `origin/main` `9b37fc5`. Bütün sayılar bu
belge `docs/` altına konmadan önceki ağaçtan; belge eklenince bulgu değişmiyor.

---

## 1. Bugünün sayısı (K1)

`docs/` altında **97 markdown dosyası** tarandı. Bulunan: **8 kayma.**

| # | Belge | Künye | Verilen dizge | Kaynakta ne var |
|---|---|---|---|---|
| 1 | `docs/inceleme/argumanlar.md:56` | `ConversionArguments.cs:56` | `palettePath = outputPath` | Dizge doğru, satır `:62`. |
| 2 | `docs/inceleme/handbrake-motoru.md:347` | `docs/olcumler/auto-mod.md:209` | `HandBrakeCLI -e x265_10bit … -b 1900`, iki satır, `\` ile devam | Komut `auto-mod.md:231`'de, tek satır, `\` yok. `:209` boş satır. |
| 3 | `docs/inceleme/model-strateji.md:46` | `CompressionStrategy.cs:40` | `targetMb <= 0` | Dizge doğru, satır `:48`. |
| 4 | `docs/inceleme/plancalculator.md:94` | `EncodeRunner.cs:62` | `actual < LowerMb` | `actualMb < band.LowerMb`, satır `:92`. |
| 5 | `docs/inceleme/uygulama-katmani.md:54` | `EncodeRunner.cs:185` | `ct.Register(TryKill)` | `ct.Register(() => TryKill(process))`, satır `:269`. |
| 6 | `docs/inceleme/uygulama-katmani.md:90` | `LanguageCatalog.cs:7` | `"Target Size Media Compression & Media Converter"` | Dizge `src/` altında hiç yok. `:7` bir yorum satırı. |
| 7 | `docs/olcumler/ab-duzenegi.md:556` | `QualityMeter.cs:147` | `var harmonic = scores.Count / scores.Sum(x => 1.0 / Math.Max(x, 1.0));` | Dizge doğru, satır `:241`. |
| 8 | `docs/olcumler/surecler-arasi-olcu-yalitimi.md:116` | `LanguageTests.cs:13` | `[assembly: CollectionBehavior(DisableTestParallelization = true)]` | `[assembly: Xunit.CollectionBehavior(…)]`, satır `:17`. |

Üçü (1, 3, 7) yalnız satır numarası bayatlaması. Beşi (2, 4, 5, 6, 8) dizgenin
kendisi de kaymış — kelime düşmüş, sarmalayıcı kırpılmış, ön ek yok olmuş ya da
dizge hiç kalmamış. Sekizi de kaynağı açılarak elle doğrulandı; "Kaynakta ne
var" sütunu o doğrulamadır.

**Düzeltme bu sözleşmenin işi değil** — `docs/` altına yazılmadı. Sekiz satır
ilgili sözleşmelere devredilir.

## 2. Ne denetlendi, ne atlandı (K2)

Tanım koda dökülmeden önce yazıldı ve `tools/alinti-denetimi/AGENTS.md`'de
duruyor. Denetlenebilir iddia: künye **satır numarası taşıyacak**, dizge künyenin
**hemen ardından** gelecek — ayıraç (`—`, `-`, `:`), parantez, bulunma eki
(`'deki`, `'de`) ya da çit bloğu ile.

| Katman | Sayı |
|---|---|
| `docs/` içinde ters tırnaklı dosya adı | 2174 |
| bunlardan satır numarası taşıyan | 524 |
| yanına birebir dizge konmuş, yani **iddia** olan | 48 |
| denetlenebilen | 9 |
| bulgu | 8 |

484 künye hiçbir dizge iddia etmiyor — "şuraya bak" demekten ibaret, denetlenecek
bir şey yok. Denetlenebilen 9 iddianın 8'i kaymış.

Atlanan 39 iddianın sebebi:

| Sebep | Sayı |
|---|---|
| `kisa` (12 karakterden kısa dizge) | 9 |
| `depoda-yok` (HandBrake klonu, `.calisma/` altında) | 7 |
| `kunye-satirsiz-ya-da-coklu` (çit bloğunun öncülü) | 6 |
| `tek-simge` | 5 |
| `sozdizim-yok` | 4 |
| `belirsiz-yol` (aynı adda birden çok dosya) | 4 |
| `kunye` (dizgenin kendisi bir künye) | 2 |
| `duzyazi-formulu` (`×`, `·`, `−` gibi işaret) | 2 |

**Hiç denetlenmeyen iddia sınıfı — konum iddiası.** "Betiğin ilk 6 satırı",
"aşağıdaki üç satır" gibi düz yazıyla verilen yer iddiaları sayılmıyor bile;
bunlar künye biçiminde olmadıkları için tabloya girmiyor. T118'in üçüncü örneği
tam buydu. Araç onu yakalamaz.

## 3. Yanlış pozitif (K3)

**Sıkı kip (varsayılan): 8 bulgunun 8'i gerçek, yanlış pozitif 0.**

İlk sürüm süzgeçsizdi: 15 iddia denetledi, **11 bulgu** verdi, **3'ü yanlış
pozitifti.** Üçü de düz yazı formülüydü — belgenin alıntı diye değil açıklama
diye yazdığı satırlar:

    -g = max(2, round(fps × 2))
    bppf = reference · 2^((refCrf − crf)/step)
    hedef*3 + 200 MB

İki deterministik süzgeç eklendi:

1. Kaynak kodda hiç görülmeyen tipografik işaret (`×`, `·`, `−`, `≤`, `⇒`, …)
   taşıyan dizge atlanır.
2. Kaynak sözdizimi işareti (`;{}()[]"'$<>\|`), bayrak (`-x`, `--x`) ya da
   `ad op ad` biçiminde bir atama/karşılaştırma hiç yoksa atlanır.

Üç yanlış pozitif de sustu, sekiz gerçek bulgunun hiçbiri kaybolmadı. 2. süzgecin
ilk hâli `palettePath = outputPath`'i (tablo 1, bulgu 1) düşürüyordu; atama
kalıbı o yüzden kaçış olarak eklendi.

Örneklem 8 ile küçük: sıfır gözlem, oranın küçük olduğunu söyler, sıfır olduğunu
söylemez. Üç kuralıyla %95 üst sınır ≈ %31.

**Gevşek kip (`--supheli`, kapalı): 126 iddia, 117 bulgu.** Rastgele 20'lik
örneklem elle sınıflandırıldı: **18 yanlış pozitif, 2 gerçek** — yani %90.
Yakalananlar ffmpeg bayrakları, denetçinin kendi koşturduğu komutlar, üçüncü
parti depoların API'leri (`docs/taramalar/` altındaki tarama belgeleri), yol
haritasındaki *henüz yazılmamış* imzalar, `<hedef>` gibi yer tutucular. İki
gerçeğin ikisi de sıkı kipin zaten yakaladığı bulgular (tablo 1'de 6 ve 8);
biri (6) o örneklemde görüldüğü için parantez biçimi sıkı kipe eklendi. Bu kip
üretime uygun değil ve varsayılan olarak kapalı.

Örneklem, parantez biçimi eklenmeden önceki koşumun 104 bulgusundan çekildi;
son koşum 117 veriyor. Oran yeniden ölçülmedi.

Boşluk, satır sonuna sarılmış alıntı ve `...` ile kırpılmış alıntı yanlış alarm
üretmiyor; üçü de gömülü sınamada ayrı vaka olarak duruyor.

## 4. Bayatlamış künye ne oluyor (K4)

Araç ikisini ayırıyor:

- Dizge dosyada **hiç yoksa** → `KAYMA`.
- Dizge **var ama künyedeki aralıkta değilse** → `SATIR KAYDI`, doğru satırı
  yazar: `QualityMeter.cs icinde var ama :241, kunye :147`.

Yani T118'in çapa yaklaşımının makine karşılığı: içeriği çapa alıp numarayı
yeniden buluyor, "hedef bulunamadı" demiyor. Tablo 1'de 1, 3 ve 7 bu sınıftan;
üçü de doğru yeni satırı gösteriyor.

## 5. Kendini sınama (K5)

`--self-test` gömülü bir örnek ağacı geçici klasöre yazar ve mutasyonu **iki
yönde** koşar:

1. Doğru belge → 5 iddia denetlenir, 0 bulgu.
2. Belge bozulur (bir sabit değiştirilir, bir künye bayatlatılır, çit bloğunda
   bir çağrı kırpılır) → 2 `KAYMA` + 1 `SATIR KAYDI`.
3. Belge düzeltilir → yine 0 bulgu.
4. Belge doğruyken **kaynak** değiştirilir → 1 bulgu.

4. adım iki sabiti karşılaştıran ölçü olmamasını sağlıyor: yalnız belge değil,
kaynak tarafı da mutasyona uğruyor ve araç kırmızıya dönüyor. Altı iddianın
altısı geçiyor. Örnek belgede kırpılmış alıntı ve satır sonuna sarılmış alıntı
da var; ikisi de 1. adımda sessiz kalmak zorunda.

## 6. CI önerisi — bağlanmadı (K6)

`.github/workflows/ci.yml` ve `tools/kosum-kapisi/` bu turda ellenmedi. Öneri:

- Bağlanacaksa **yalnız sıkı kip**, yalnız `docs/` üzerinde, `--supheli` olmadan.
- **Sözleşme klasörüne bağlanmasın.** `.claude/relay` üzerinde ölçüldü: 130 belge,
  21 iddia, **18 bulgu**. Mekanizma doğru çalışıyor, kapsam yanlış — sözleşmeler
  henüz yazılmamış kodu alıntılıyor, "yok" demek doğru ama işe yaramaz.
- Bugünün 8 bulgusu **kapatılmadan** kapıya bağlanamaz; bağlanırsa kapı ilk gün
  kırmızıya döner ve devre dışı bırakılır.
- Yanlış pozitif örneklemi 8 bulguyla küçük. Karar için iki üç sözleşme daha
  bulgusuz koşum görmek gerekir; şu anki veri kapı kararını vermeye yetmez.

## 7. Ölçülmeyenler

- **Süre ölçülmedi.** Aynı iş için 214 ms ile 2910 ms arası okundu; aynı
  koşullarda boş `python -c pass` bile 250–432 ms sürdü. Makine paylaşımlı (on
  beş ajan), ölçü makine yükünün ölçüsü oldu, aracın değil. Kapıya maliyetinin
  ihmal edilebilir olduğu **gösterilmedi**.
- **Konum iddiaları** ("ilk 6 satır") — hiç denetlenmiyor.
- **Künyesiz alıntı** — belge dosya adı vermeden birebir dizge veriyorsa araç
  onu görmez. T118'in birinci örneği (`CI TEMSILI: …` satırı) tam bu sınıfta;
  sıkı kipte de gevşek kipte de yakalanmadı. Bilinen üç örnekten yalnız biri
  (`2>/dev/null` düşmesi) yakalanıyor, o da gevşek kipte.
- **Gevşek kipin gerçek pozitif oranı** — kaç gerçeği kaçırdığı ölçülmedi.
- **`docs/` dışı** — `README`, `src/` içi yorumlar taranmadı.
- Aracın **başka bir depoda** çalışıp çalışmadığı ölçülmedi.
