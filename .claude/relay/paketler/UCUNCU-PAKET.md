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

## İş 2 — T88 tur 3 (birinciden hemen sonra) — DEĞİŞTİ

**Bu madde 1 Eylül akşamı değişti. Önceki hali T89'du; T89 ertelendi.**

Sebep: T88 tur 2'nin bağımsız denetimi KALDI verdi ve iki KRİTİK buldu. T89 tam
olarak T88'in kurduğu zeminin üzerine oturuyor; zemin bozukken T89 yazmak yanlış
sayının üzerine plan kurmak olur.

Sözleşme: `.claude/relay/contracts/T88.md` — sondaki "# Düzeltme turu 3" (J1–J5).
Dal: `T88-ornekte-kalite-olcumu` sürdürülür.

**J1 KRİTİK:** konteyner asimetrisi kapanmadı, **eksen değiştirdi**. Tur 2 tam ölçek
ile yarım ölçeği eşitledi (ikisi de matroska, ikisi de `FileInfo.Length`) ama üçüncü
taraf eşitlenmedi — `ComplexityProbe.cs:124`'teki `motion.Bytes` hâlâ `-f null -`
üzerinden **ham akış** baytı, `reference.FullBytes` ise artık **Matroska dosya**
baytı. `main`de ikisi de hamdı; bu dal payı bozdu.

Ölçülen (720p60, 2 sn, CRF 23, veryfast, düşük karmaşıklık): ham 5430 B, mkv 6871 B
→ payda %26,5 şişiyor → `MotionExponent` ≈ −0,34 kayıyor, alt kelepçe 0'a çakıyor,
`PlanCalculator.cs:182` eşiği yanlış tarafa düşüyor: **"burada kare düşürmek ucuz"
denip FPS yarılanıyor.** Ekran kaydı, sunum, animasyon, statik konuşan kafa — hepsi
bu rejimde.

**J2 KRİTİK:** E1'i sabitlediği söylenen test davranış bağlamıyor.
`ComplexityProbeTests.cs:69-70` iki **sabiti** karşılaştırıyor. Denetçi gerçek kusuru
geri koydu (yarım örneğin formatını `"h264"` yaptı) ve 12/12 test yeşil kaldı.

Kalanlar sözleşmede: J3 (uygulama yolu testle sabitlenmemiş — `RunAsync`'in
varsayılanı `true` yapılınca hiçbir test kırmızıya dönmüyor), J4 (rapor borçları),
J5 (borç, bu turda kapanması beklenmiyor).

---

## T89 nerede kaldı

T89 (`.claude/relay/contracts/T89.md`) duruyor ve sırada. T88 tur 3 mühürlendikten
sonra dağıtılacak; şimdi başlama.

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
