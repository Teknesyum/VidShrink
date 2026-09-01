# Paket — iki iş, arada bekleme yok

İki iş var. Birincisini bitirip ittikten sonra **beklemeden** ikincisine geç.
T0'a dönüp onay bekleme; iki iş de bitince tek raporla dön.

`main`e **birleştirme**. Her iş kendi dalında kalır ve `origin`e itilir.

---

## İş 1 — T90 (yeni tur, şimdi başla)

Sözleşme: `.claude/relay/contracts/T90.md`
Dal: `T90-ceviri-borclari`

T83 mühürlendi ve geçti, ama denetim yedi borç bıraktı ve üçü aynı şeyi söylüyor:
**T83'ün kurduğu ölçüler bugün ihlal bulmuyor çünkü ihlal yok; ileride koruma
vermiyorlar çünkü yanlış yere bakıyorlar.** Sözleşmenin sekiz kabul kriterinin hepsi
geçerli; kısaca:

- Gömülü iki dilli metin (`LanguageCatalog.cs` `EncodeMarker`) anahtara taşınır.
- Yetim docstring kalkar (iki `<summary>` üst üste gelmiş).
- C# ölçüsü interpolasyonun **sabit parçalarını** görür; taranan küme
  `src/VidShrink.App/**/*.cs` olur. Muafiyet desenle değil **adıyla** yazılır.
- XAML ölçüsü öğe gövdesini de tarar (`<TextBlock>düz metin</TextBlock>`).
- İki mutasyon: biri interpolasyon sabit parçasına, biri XAML gövdesine gömülü metin
  ekler; ölçünün kırmızıya döndüğü gösterilir.
- Oynatma paneli `Say("Başa dön", "Back to the start")` çiftlerinden anahtara geçer;
  `Locales/*/playback.json` zaten var, panel kullanmıyor.
- Tanınmayan girdide sessiz İngilizce düşürme **bilinçli karara** bağlanır ve testle
  sabitlenir.

Çıktı: `docs/olcumler/ceviri-olcusu-mutasyonu.md` + kod ve testler.

---

## İş 2 — T89 (birinciden hemen sonra)

Sözleşme: `.claude/relay/contracts/T89.md`
Dal: `T89-olculen-kaliteyle-plan`

**Başlamadan önce tek adım:** `git fetch origin; git log --oneline origin/main -5`.
T88 (`T88 tur 2: ornek muhasebesini ve kalite varsayilanini duzelt`) `main`e
birleşmiş olmalı; dalını `origin/main`den aç. Birleşmemişse T0'a tek satır yaz ve
`origin/T88-ornekte-kalite-olcumu` üzerinden aç — bekleme.

T88 köprüyü kurdu: örnek pencerelerde algılanan kalite ölçülüyor ve `ProbeResult`
üzerinden planlayıcıya ulaşıyor. T89 onu **kullanır**.

Bugün `PlanCalculator` kaliteyi tahmin ediyor ve sabitleri elle seçilmiş:
`QualityPerHalving = 6.0`, `ScalePenaltyScale = 10.0`, `FpsPenaltyPerHalving = 5.0`.
Motorun kurucu tezi "sabit merdiven değil gerçek ölçüm" — tam burada tahmine düşüyor.

Yön: sabitleri global kalibre etme; **klip-başına ölçülen noktalarla değiştir.**
Sabitler yalnız ölçüm yokken geriye dönüş (prior) olarak kalır ve bunu söyleyen bir
isim taşır.

**Konumlandırma korunur: hedef boyut birincil.** Kalite ikinci bir hedef değil,
bir **durdurma kısıtıdır** — hangisi önce dolarsa orada durulur. Bu sözleşme
VidShrink'i hedef-kalite aracı yapmaz.

Kalan kabul kriterleri sözleşme dosyasında; hepsi geçerli.

---

## İki işte de geçerli kurallar

- Kendi worktree'nde çalış. Paylaşılan çalışma ağacına (`Desktop/Projeler/Vidshrink`)
  yazma, orada `dotnet test` koşturma — orada başka koşumlar var.
- Hiçbir assertion gevşetilmez, hiçbir test `Skip`e alınmaz, hiçbir beklenti ölçümün
  kendi çıktısından türetilmez.
- Her yeni davranış için test; her düzeltme için mutasyon denetimi.
- Kod yorumu yazma. Mevcut yorumları koru; kod değişirse üstündeki cümleyi ona uydur.
- Ara dosyalar `.calisma/` altına; iş bitince kendi bıraktığını sil.
- **Tam süiti kendi yazdığın kapıdan geçir:**
  `pwsh -File tools/kosum-kapisi/kosum-kapisi.ps1` — çağrı biçimi
  `docs/olcumler/suit-esszamanli-kosum.md` içinde. Çıktıda "kilitlendi",
  "iptal edildi", "Durduruldu" gibi bir kesinti satırı varsa çıkış kodu 0 ve
  `Başarısız: 0` olsa bile koşum yarımdır; raporda toplam test sayısını yaz.
- **İtmeden önce `gh run list --branch <dal>` koş.** Yerel yeşil CI yeşili değildir.
- Rapordaki her sayı ölçümden gelir. Ölçmediğin şey için "ölçülmedi" yaz.
