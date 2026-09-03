# T160 — Üç bayat iddia ve sınıfı yakalayan pim

**Sonuç önden:** K1'in üç cümlesi düzeltildi. K2'nin pimi yazıldı ve K4'ün üç mutasyonunun
üçünü de öldürüyor — ama bu sözleşmenin kendi Bağlam'ının bir iddiasını **doğrulayamadı**:
"18'i tuttu, 6'sı bayattı" cümlesi, K3'ün varsaydığının aksine, K2'nin pimini **kırmızıya
düşürmüyor**. Aşağıda ölçüldüğü gibi anlatılıyor; gizlenmedi.

## K1 — Üç cümle düzeltildi

### 1. `docs/olcumler/uc-kucuk-borc.md:127-129`

```diff
-iddiası** tek tek sayıldı: 18'i tuttu, 6'sı bayattı. Ayrıca 3 iddia tarihsel — bugünkü
+iddiası** tek tek sayıldı: 17'si tuttu, 7'si sorunluydu. Sorunlu yedinin altısı bayat,
+biri yanıltıcı (aşağıdaki tablo, `docs/olcumler/uc-kucuk-borc.md:142-167`; 17 + 7 = 24,
+6 + 1 = 7). Ayrıca 3 iddia tarihsel — bugünkü
```

Dayanak: aynı dosyanın kendi tablosu, satır 142-167 (24 satır, `Durum` sütunu). Elle sayım:
6 satır `bayat` (368, 385, 399, 407, 407, 423 numaralı iddialar), 1 satır `yanıltıcı` (363),
kalan 17 satır `tuttu`. 17 + 6 + 1 = 24; eski cümlenin "18 + 6 = 24"ü kendi içinde toplanıyor
ama yanıltıcı satırı tuttu'ya katıyordu.

### 2. `tests/VidShrink.Tests/UretimYoluTests.cs:267-271`

```diff
-    /// K4. Turbo libx264'te acilmiyor: ikinci gecis birinci gecisin <c>weightp</c> ayarina
-    /// uymak zorundadir, <c>veryfast</c> ile <c>slow</c> farkli deger kosar ve x264 ikinci
-    /// gecisi hic acmaz. Olculdu: iki klipte de cikti sifir bayt.
+    /// K4. Turbo libx264'te <c>weightp</c> esitlenmeden acilmiyor: ikinci gecis birinci
+    /// gecisin <c>weightp</c> ayarina uymak zorundadir, <c>veryfast</c> ile <c>slow</c>
+    /// farkli deger kosar ve x264 ikinci gecisi acmaz. Olculdu
+    /// (<c>docs/olcumler/x264-turbo-acilis.md:43-49</c>): weightp esitlenmeyen kolda cikti
+    /// sifir bayt, esitlenen dort kolun dordu de ~3,7 MB uretti.
```

Dayanak: `docs/olcumler/x264-turbo-acilis.md:43-49` (T155'in K1 ham çıktısı) — kolon A
(weightp eşitlenmemiş) `p2exit=127`, çıktı 0 bayt; kolon B/C/D/E (dört farklı `weightp`
eşitleme yazımı) hepsi exit 0, çıktı 3 712 615 – 3 717 749 bayt arası. Test gövdesi
değişmedi.

### 3. `src/VidShrink.Core/CodecModel.cs:170-174`

```diff
-    /// <c>libx265</c> turbosu %29,6 - %33,5 kazandirip VMAF'i dusurmuyor. x264'te ikinci gecis
-    /// toplamin buyuk yarisi ve ilk gecisin suresini cozme ile olcekleme belirliyor, on ayar
-    /// degil. Olcum: <c>docs/olcumler/x264-turbo-acilis.md</c>.
+    /// <c>libx265</c> turbosu %29,6 - %33,5 kazandirip VMAF'i dusurmuyor. x264'te ikinci
+    /// gecis toplamin buyuk yarisidir (2,38 - 2,80 sn); klip 35'te ilk gecisin suresini
+    /// agirlikli olarak cozme ve olcekleme belirliyor, on ayar farki yalniz 39,7 ms / toplamin
+    /// %1,0'i (<c>docs/olcumler/x264-turbo-acilis.md:127-131</c>). Bu oran yalniz olculen
+    /// parca icin gecerli; kazanc parca basina %0,58 - %4,44 arasinda degisiyor, tek yonlu
+    /// bir genelleme kurulamaz. Olcum: <c>docs/olcumler/x264-turbo-acilis.md</c>.
```

Dayanak: `docs/olcumler/x264-turbo-acilis.md:127-131`, klip 35 verisi (tek ölçülen ayrıştırma).
`Safe: false` kararı ve `TurboFirstPassCeilings` sözlüğü davranışça **değişmedi**.

**Kullanılmayan iddia — sözleşmenin kendi Bağlam'ı.** Bu sözleşmenin Bağlam bölümü,
düzeltme #3 için "T155'in klip 5 verisi" diye şu sayıları veriyordu: `p1 slow ort.
(2003+1735+1759)/3 = 1832,3 ms`, `p1 veryfast ort. (1638+1638+1659)/3 = 1645,0 ms`.
Bu sayılar `docs/olcumler/x264-turbo-acilis.md`'de aranmadı bulunamadı (`git log
a10743a` diff'i de dosyaya bunları eklememiş — dosya o commit'ten bugüne 220 satır ve
klip 5/35 için yalnız toplam-süre tabloları var, geçiş-başına ayrıştırma yok). Sayılar
kopyalanmadı; düzeltme yalnız doğrulanabilir klip 35 verisine dayandırıldı, klip 5 için
iddia atlandı (bkz. `.claude/relay/live/_sorun.log`, T160 satırı).

## K2 — Sınıfı yakalayan pim: `IddiaPimiTests.cs`

Tarayıcı (`IddiaTarama.Tara`, aynı dosyada) `docs/olcumler/*.md`'yi okur, tablo satırlarını
(`|` ile başlayan) ve kod bloklarını (` ``` `) çıkarır, kalan düz metni cümlelere böler.
İki tarafında da rakam olan her `:` içeren cümle **incelenen** sayılır. Bunların arasında
`N ... : A ..., B ...` kalıbına (rakamlar ondalık virgül veya binlik boşluk grubu
taşımıyorsa) uyanlar **çözümlenen**, uymayanlar **atlanan** sayılır.

### Ham döküm (bugünkü ağaç, `dotnet test --filter IddiaPimiTests`)

```
incelenen=790 cozumlenen=2 atlanan=788
tutarli kalibre-pencere.md:11 N=21 A=3 B=18
tutarli uc-kucuk-borc.md:127 N=24 A=17 B=7
UYARI: atlama orani yuksek (788/790) — olcunun degeri dusuk, gizlenmiyor.
```

**Atlama oranı %99,7 (788/790).** Bu düşük değerli bir ölçü — sözleşmenin kendi kuralı
gereği bu açıkça yazılıyor, gizlenmiyor. Sebebi tasarım: iki nokta üstüste'den sonra tam
iki virgüllü öğe ve aradaki metinde başka rakam olmaması şartı, ölçüm tablolarıyla dolu bu
belge kümesinde neredeyse hiç eşleşmiyor — kasıtlı olarak sıkı tutuldu ki "ilişki
kurulamadı" durumunda yanlış pozitif üretmesin (sözleşmenin kendi uyarısı: "3 kaynakta 7
çarpan" gibi ilişkisiz cümleleri kırmamalı).

### Sınıfın kendisi ne yakalanıyor, ne yakalanmıyor — ölçülen bulgu

Sözleşmenin Bağlam'ı, düzeltilmeden önceki cümleyi ("18'i tuttu, 6'sı bayattı") "bu, üç
öncelin birincisinin tam şekli ve elle sayılmadan yakalanabilir" diyordu. **Ölçüldü, bu
iddia tutmuyor.** Yukarıdaki dökümde görüldüğü gibi, düzeltmeden önceki metinle koşulan
tarama da (`.calisma/T160/k3-once.txt`) aynı `uc-kucuk-borc.md:127` cümlesini
**`tutarli`** buluyor — çünkü `18 + 6 = 24` kendi içinde doğru. Bozukluk toplamda değil,
**kategori kaymasında**: gerçek dağılım 17 tuttu + 6 bayat + 1 yanıltıcı = 24, ama cümle
yanıltıcı satırı tuttu'ya katıp "18" yazmıştı. Bir cümlenin kendi bildirdiği N/A/B'nin
toplaması, kategorilerin tabloyla eşleşip eşleşmediğini göremez. Bu, K2'nin sözleşmede
tarif edilen kalıbının (yalnız cümle-içi toplam-pay aritmetiği) doğası gereği bir sınırı;
sessizce gizlenmedi, aşağıda K3'te ham kanıtla gösteriliyor.

## K3 — Kırmızı/yeşil çiftleri

**Sıra, sözleşmenin istediğinin tersine bir bulgu üretti: pim düzeltmeden önce de sonra
da yeşil kaldı.** Ölçüldüğü gibi raporlanıyor.

1. Commit `12c8f35` — K1'in kod-dosyası iki cümlesi (CodecModel, UretimYoluTests).
2. Commit `96e0a05` — `IddiaPimiTests.cs` eklendi, **`uc-kucuk-borc.md` henüz düzeltilmemiş**
   (`18'i tuttu, 6'sı bayattı`). Koşum: `.calisma/T160/k3-once.txt`.

   ```
   incelenen=790 cozumlenen=2 atlanan=788
   tutarli uc-kucuk-borc.md:127 N=24 A=18 B=6 :: ... 18'i tuttu, 6'sı bayattı.
   Test Çalıştırması Başarılı. Toplam test sayısı: 1  Geçti: 1
   ```

   **Kırmızı değil.** Az önce K2'de açıklandığı gibi 18+6=24 kendi içinde tutarlı.

3. Commit `431faa7` — `uc-kucuk-borc.md` düzeltildi (17'si tuttu, 7'si sorunluydu). Koşum:
   `.calisma/T160/k3-sonra.txt`.

   ```
   incelenen=790 cozumlenen=2 atlanan=788
   tutarli uc-kucuk-borc.md:127 N=24 A=17 B=7 :: ... 17'si tuttu, 7'si sorunluydu.
   Test Çalıştırması Başarılı. Toplam test sayısı: 1  Geçti: 1
   ```

   Yeşil — ama zaten öncesinde de yeşildi. Gerçek bir kırmızı→yeşil geçişi **yok**; K4(b)
   bunun yerine ölçünün sahiden bir bozulmayı yakalayabildiğini kontrollü biçimde gösteriyor
   (aşağıda).

## K4 — Mutasyon ızgarası

Her satırdan önce `dotnet build -c Release --no-incremental` çalıştırıldı.

| # | Mutasyon | Beklenen | Kırılan ölçü(ler) | Ham çıktı |
|---|---|---|---|---|
| a | `IddiaPimiTests.cs`: `Iddia.Tutarli` içindeki `A + B == N` → `A + B != N` | ölmeli | `IddiaPimiTests.ToplamPayCelisenCumleYoktur` | `.calisma/T160/k4a-mutasyon.txt` — `Assert.Empty()` 2 elemanlı koleksiyonla düştü (iki `tutarli` kayıt artık `Tutarli=False`) |
| b | `uc-kucuk-borc.md`: düzeltilmiş `7'si sorunluydu` → `8'si sorunluydu` (17+8=25≠24) | ölmeli | `IddiaPimiTests.ToplamPayCelisenCumleYoktur` | `.calisma/T160/k4b-mutasyon.txt` — `CELISKILI uc-kucuk-borc.md:127 N=24 A=17 B=8`, `Assert.Empty()` düştü |
| c | `CodecModel.cs`: `["libx264"] = new("veryfast", Safe: false)` → `Safe: true` | ölmeli (T155'in ölçüleri) | `TurboTavanTests` (4/4) | `.calisma/T160/k4c-mutasyon.txt` — `Vaat_edilen_tavan_kumesi_guvenli_kumeden_genis_ve_fark_libx264`, `X264_icin_tavan_vaat_ediliyor_ama_o_tavan_guvenli_degil`, `Uretim_yolunun_actigi_turbo_kumesi_guvenli_kumeyle_ayni`, ve dördüncüsü düştü |

Üçü de öldü, üçü de geri alındı ve `dotnet build -c Release --no-incremental` ile
doğrulandı (0 hata).

## K5 — Doğrulama kollarının test sayıları

```
dotnet test -c Release --no-build --filter "IddiaPimiTests" --list-tests   → 1 test
dotnet test -c Release --no-build --filter "UretimYoluTests" --list-tests  → 13 test
dotnet test -c Release --no-build --filter "TurboTavanTests" --list-tests  → 4 test
dotnet test -c Release --no-build --filter "IddiaPimiTests|UretimYoluTests|TurboTavanTests" --list-tests
  → 18 test (1 + 13 + 4, çakışma yok)
```

Sıfır bulan kol yok. Birleşik filtreyle koşum (`.calisma/T160/k5-verify-final.txt`):

```
Test Çalıştırması Başarılı.
Toplam test sayısı: 18
     Geçti: 18
```

CI koşum kimliği: `.claude/relay/LOG.md`e T0 tarafından dal itildikten sonra eklenecek —
bu ajan `origin`e itmeyi ve `gh run list` sonucunu Çıktı'ya ekledi (aşağıya bakınız).

## Borçlar / açık uçlar

- **K2'nin pimi, sözleşmenin hedeflediği "sınıfın kendisi"ni tam kapsamıyor.** Yakaladığı
  şey yalnız cümle-içi toplam-pay aritmetik hatası (K4a/K4b ile kanıtlı); yakalamadığı şey
  toplamı koruyan kategori kayması (T160 Bağlam #1'in kendisi, K3'te ölçüldü). Gerçek bir
  kategori-kayması pimi, cümledeki iddiayı tablo satırlarının kendisiyle çapraz saymayı
  gerektirir — bu, tek bir belge yapısına (bu dosyanın `Durum` sütunu) özel bir çözümleyici
  ister ve "docs/olcumler geneli" kapsamıyla genellenmesi ayrı bir sözleşme konusu.
- Atlama oranı %99,7 — ölçü şu an yalnız 2 cümleyi çözümleyebiliyor, 67 belgenin geri kalanı
  neredeyse hiç bu kalıba girmiyor. Bandı genişletip daha çok cümle yakalamak, iddiayı
  zayıflatmadan (yanlış pozitif üretmeden) yapılabilir mi — ölçülmedi, bu sözleşmenin
  kapsamı dışında.
