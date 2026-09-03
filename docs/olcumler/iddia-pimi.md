# T160 — Üç bayat iddia ve sınıfı yakalayan pim

**Sonuç önden:** K1'in üç cümlesi düzeltildi. Tur 1'de yazılan K2 pimi denetçi tarafından
bağımsız ölçüldü ve **KRİTİK** bulundu: cümle-içi toplam-pay aritmetiği (`A+B==N`) bu
sözleşmenin kendi açılış örneğini ("18'i tuttu, 6'sı bayattı", 18+6=24) **tutarlı**
buluyordu — kusur toplamda değil kategori kaymasındaydı (gerçek dağılım 17+6+1=24).
Tur 2'de pim K2' ile değiştirildi: artık aynı `## ` bölümündeki `Durum` sütunlu tabloya
karşı da okuyor. Aşağıda hem KRİTİK'in ham kanıtı hem de düzeltmeden sonraki kırmızı→yeşil
geçişi var.

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
Bu sayılar `docs/olcumler/x264-turbo-acilis.md`'de aranıp bulunamadı (`git log a10743a`
diff'i de dosyaya bunları eklememiş — dosya o commit'ten bugüne 220 satır ve klip 5/35
için yalnız toplam-süre tabloları var, geçiş-başına ayrıştırma yok). Sayılar kopyalanmadı;
düzeltme yalnız doğrulanabilir klip 35 verisine dayandırıldı, klip 5 için iddia atlandı
(bkz. `.claude/relay/live/_sorun.log`, T160 satırı).

## Denetim turu 1 — KRİTİK ve K2' düzeltmesi

Denetçi tur 1'i bağımsız ölçtü: K1, K4, K5, `owns`, `CodecModel` davranışı sağlam;
**tek KRİTİK**, K3'ün istediği kırmızı→yeşil geçişinin oluşmaması.

```
dotnet test -c Release --no-build --filter "IddiaPimiTests"  (kusur commit'i 96e0a05)
  -> Başarılı! Toplam: 1, Başarılı: 1   (KIRMIZI DEĞİL, YEŞİL)
  incelenen=790 cozumlenen=2 atlanan=788
  tutarli uc-kucuk-borc.md:127 N=24 A=18 B=6
```

**Neden.** Tur 1'in pimi yalnız cümle-içi aritmetik kuruyordu: `A + B == N`. "18'i tuttu,
6'sı bayattı" cümlesinde 18+6=24 — cümle kendi içinde tutarlı. Kusur toplamda değil,
kategori kaymasında: gerçek dağılım 17+6+1=24, cümle 18+6 diyordu (yanıltıcı iddiayı
tuttu sepetine katmış).

**Düzeltme (K2').** Pim artık özet cümlesini yalnız kendine karşı değil, **aynı `## `
bölümündeki yer gerçeğine** karşı da okuyor: bölüm içinde başlığı `Durum` olan bir tablo
varsa, o sütunun değerleri tallilenir; cümledeki `A`/`B` sayılarının hemen yanındaki
kategori kelimesi (örn. "17'si **tuttu**") bu talliyle bir ön-ek eşleşmesiyle bağlanır
(Türkçe çekim ekleri için, örn. "bayattı" → "bayat"); eşleşen kategoride sayı tutmuyorsa
cümle **kırmızı**, cümle-içi aritmetik tutarlı olsa bile. Etiket tabloda hiçbir kategoriye
eşleşmiyorsa (örn. "sorunluydu" — tabloda yalnız `tuttu`/`bayat`/`yanıltıcı` var) o kategori
sessizce atlanır, kırmızı üretmez. Tablo hiç bulunamazsa eski aritmetik-yalnız davranışa
düşülür ve ayrı sayılır (`YerGercegiYok`) — "çözümlenemedi" değil.

Kod: `tests/VidShrink.Tests/IddiaPimiTests.cs`, `IddiaTarama.DurumTablosu` (bölüm sınırlarını
`^\s*#{1,2}\s` ile bulur, tabloyu `|` satırlarından ayıklar) ve `Iddia.Tutarli`
(`YerGercegiVar ? AritmetikTutarli && YerGercegiTutarli : AritmetikTutarli`).

## K2' — Ham döküm (bugünkü ağaç)

```
incelenen=795 cozumlenen=2 yerGercegiOlan=1 yerGercegiYok=1 atlanan=793
tutarli kalibre-pencere.md:11 N=21 A=3(kırmızı) B=18(yeşil) :: yer gercegi yok, yalniz aritmetik
tutarli uc-kucuk-borc.md:127 N=24 A=17(tuttu) B=7(sorunluydu) :: yer gercegi: ayni '## ' bolumu, basligi 'Durum' olan tablo (satir 142)
UYARI: atlama orani yuksek (793/795) — olcunun degeri dusuk, gizlenmiyor.
```

**Atlama oranı %99,8 (793/795).** Tur 1'deki gibi düşük değerli bir ölçü, açıkça yazılıyor.
`incelenen` sayısı tur 1'e göre arttı (790→795) — eşzamanlı koşan T157/T159 sözleşmeleri
`docs/olcumler/` altına yeni belge ekledi, bu ölçünün kendi tasarımından kaynaklanmıyor.

**Çözümlenen 2 cümleden yalnız biri yer gerçeğine sahip.** `kalibre-pencere.md:11`
("21 ölçü: 3 kırmızı, 18 yeşil") aynı `## K1` bölümünde `Durum` başlıklı bir tablo
taşımıyor (bölüm bir kod bloğu ve düz metin) — bu yüzden `YerGercegiYok`, eski
aritmetik-yalnız kontrole düşüyor. `uc-kucuk-borc.md:127` ise `## K3` bölümündeki
tabloyu buluyor ve `tuttu` etiketini eşliyor; `sorunluydu` etiketi tabloda karşılık
bulamadığı için atlanıyor (mismatch değil).

## K3 — Kırmızı/yeşil çiftleri (K2' ile yeniden ölçüldü)

Kusur commit'i `96e0a05` (`IddiaPimiTests.cs` var, `uc-kucuk-borc.md` henüz K1 öncesi)
üzerine **bugünkü K2' kodu** bindirilip koşuldu; ardından K1'in düzeltmesi geri getirildi.

**Kırmızı** (`96e0a05`'in `uc-kucuk-borc.md`'si, K2' koduyla; `.calisma/T160/k3-kirmizi-yeni.txt`):

```
incelenen=795 cozumlenen=2 yerGercegiOlan=1 yerGercegiYok=1 atlanan=793
CELISKILI uc-kucuk-borc.md:127 N=24 A=18(tuttu) B=6(bayattı) ::
  yer gercegi: ayni '## ' bolumu, basligi 'Durum' olan tablo (satir 140)
  [tuttu=18 ama tabloda tuttu=17]
Başarısız! - Başarısız: 1, Başarılı: 0, Toplam: 1
```

**Yeşil** (`431faa7`'ın düzeltilmiş `uc-kucuk-borc.md`'si, aynı K2' kodu;
`.calisma/T160/k3-yesil-yeni.txt`):

```
incelenen=795 cozumlenen=2 yerGercegiOlan=1 yerGercegiYok=1 atlanan=793
tutarli uc-kucuk-borc.md:127 N=24 A=17(tuttu) B=7(sorunluydu) ::
  yer gercegi: ayni '## ' bolumu, basligi 'Durum' olan tablo (satir 142)
Test Çalıştırması Başarılı. Toplam test sayısı: 1  Geçti: 1
```

Fark tek satır: `tuttu=18 ama tabloda tuttu=17` notu, sözleşmenin açılış örneğinin tam
kendisi — artık K2' bunu görüyor. Her iki koşumdan önce `dotnet build -c Release
--no-incremental` çalıştırıldı (`--no-build` yasağına uyuldu).

## K4 — Mutasyon ızgarası (K2' sonrası yeniden doğrulandı)

Her satırdan önce `dotnet build -c Release --no-incremental` çalıştırıldı.

| # | Mutasyon | Beklenen | Kırılan ölçü(ler) | Ham çıktı |
|---|---|---|---|---|
| a | `IddiaPimiTests.cs`: `Iddia.AritmetikTutarli` içindeki `A + B == N` → `A + B != N` | ölmeli | `IddiaPimiTests.ToplamPayCelisenCumleYoktur` | `.calisma/T160/k4a-mutasyon-yeni.txt` — `Assert.Empty()` 2 elemanlı koleksiyonla düştü (`kalibre-pencere.md:11` ve `uc-kucuk-borc.md:127`, ikisi de `Tutarli=False`) |
| b | `uc-kucuk-borc.md`'de düzeltilmiş sayı tekrar bozuldu (`96e0a05`'in metnine dönüş — K3'ün kırmızı koşumuyla aynı mutasyon) | ölmeli | `IddiaPimiTests.ToplamPayCelisenCumleYoktur` | `.calisma/T160/k3-kirmizi-yeni.txt` — `CELISKILI uc-kucuk-borc.md:127 ... [tuttu=18 ama tabloda tuttu=17]`, `Assert.Empty()` düştü |
| c | `CodecModel.cs`: `["libx264"] = new("veryfast", Safe: false)` → `Safe: true` | ölmeli (T155'in ölçüleri) | `TurboTavanTests` (4/4) | `.calisma/T160/k4c-mutasyon-yeni.txt` — `Uretim_yolunun_actigi_turbo_kumesi_guvenli_kumeyle_ayni`: `Assert.Equal()` `["acik"]` beklenirken `["kapali"]`; 4 testin 4'ü de düştü |

Üçü de öldü, üçü de geri alındı ve `dotnet build -c Release --no-incremental` ile
doğrulandı (0 hata). (b) artık K3'ün kırmızı koşumuyla aynı kanıt — K2' öncesinde bu
mutasyon aritmetik açıdan da (17+8≠24 gibi) test edilebiliyordu, K2' sonrasında zaten
K3'ün asıl bulduğu şey (kategori kayması) bunu daha güçlü kapsıyor.

## K5 — Doğrulama kollarının test sayıları

```
dotnet test -c Release --no-build --filter "IddiaPimiTests" --list-tests   → 1 test
dotnet test -c Release --no-build --filter "UretimYoluTests" --list-tests  → 13 test
dotnet test -c Release --no-build --filter "TurboTavanTests" --list-tests  → 4 test
dotnet test -c Release --no-build --filter "IddiaPimiTests|UretimYoluTests|TurboTavanTests" --list-tests
  → 18 test (1 + 13 + 4, çakışma yok)
```

Sıfır bulan kol yok. Birleşik filtreyle koşum (`.calisma/T160/k5-verify-final-tur2.txt`):

```
Başarılı!  - Başarısız: 0, Başarılı: 18, Toplam: 18
```

CI kosum kimligi (K2' sonrasi, `904cd35` push'u): `33764744027` -- `gh run view 33764744027
--json status,conclusion` -> `{"status":"completed","conclusion":"success"}` (22dk 13sn, tek is,
`-warnaserror` ve kosum-kapisi.ps1 ikisi de yesil). Onceki tur (K1-K5 ilk teslim, `987129f`):
`33760325360`, ayni sekilde `completed success`.

## Borçlar / açık uçlar

- **Etiket eşleştirmesi ön-ek sezgisiyle çalışıyor, tam morfolojik çözümleme değil.**
  `EslesenKategori`, "bayattı" → "bayat" gibi Türkçe çekim eklerini `StartsWith` ile
  toleranslıyor (üç harften kısa etiketleri hiç denemiyor). Bu depoda tek somut örnek
  üzerinde (`uc-kucuk-borc.md`) doğru sonuç veriyor; başka bir belgede farklı bir çekim
  şekli (örn. ek başa gelen bir yapı) yanlış eşleşme ya da kaçırma üretebilir mi —
  ölçülmedi, çünkü kapsamda başka `Durum` tablolu özet cümlesi yok.
- **`Durum` sinyali tek bir bulgu şekline özel.** "Aynı `## ` bölümünde başlığı 'Durum'
  olan tablo" kuralı, bu depodaki gerçek örnekten (`uc-kucuk-borc.md`in K3 bölümü)
  genellendi. Başka bir yer gerçeği şekli (örn. `Durum` yerine `Sonuç`/`Kategori`
  başlıklı bir tablo, ya da tablo yerine madde işaretli liste) bu sinyalle yakalanmaz —
  ayrı bir sözleşme konusu.
- Atlama oranı %99,8 — ölçü hâlâ yalnız 2 cümleyi çözümleyebiliyor, bunlardan biri yer
  gerçeğine sahip. Bandı genişletmek ayrı kapsam (tur 1'den taşınan borç, değişmedi).
