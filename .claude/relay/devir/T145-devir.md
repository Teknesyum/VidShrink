sozlesme: T145
dal: T145-kalan-alti-bant
son-commit: e162d31f664f3cec4bb3dd06f685ff350552ef6a

## Nerede kaldım

- **K1 (altı bandın karar tablosu) — bitti.** Altı satır: A `:890` daraltıldı
  (60sn→5sn), B `:915` ölçülmüş gerekçeyle bırakıldı (3sn), C `:916` daraltıldı
  (3sn→2sn), D `:1141` daraltıldı (10sn→2sn), E `PerformanceCheckTests.cs:462`
  `YonPayi` daraltıldı (0,8→0,9), F `:542` ölçülmüş gerekçeyle bırakıldı (katsayısız).
  Rapor: `docs/olcumler/kalan-alti-bant.md`.
- **K2 (mutasyon ızgarası) — bitti.** Dört daraltılan bant için (A, C, D, E) her
  biri eski-bant/yeni-bant iki kolla mutasyona uğratıldı, ham çıktı raporda.
  `--no-build` hiç kullanılmadı.
- **K3 (`:462`/`:542` özel dikkat) — bitti.** `YonPayi` dayanağı 10 turluk sakin
  koşumla ölçüldü, F bandı için katsayı denemesi (k=1,5) 10 turun 4'ünü kırdığı
  gösterildi ve katsayı konmadı.
- **K4 (sayım ölçüsü) — bitti.** `SaatTureviIddiaSayisi = 23` değişmedi,
  `SaatTureviIddialarinSayisiBelgedekiyleAyni` yeşil.
- **K5 (kol başına test sayısı + CI) — büyük ölçüde bitti, bir açık var.**
  Aşağıya bak.

## Ölçtüğüm sayılar

### Verify kolları — test sayısı

```
PerformanceCheckTests = 22
UpdaterTests = 54
PerformanceCheckTests|UpdaterTests birlikte = 76
```

### Verify koşumu (VIDSHRINK_LAUNCHER_EXE kurulu, bu makinede)

```
Başarılı!  - Başarısız: 0, Başarılı: 74, Atlanan: 2, Toplam: 76, Süre: 6 m 15 s
```

Atlanan ikisi ortam kapılı (`[QuietMachineFact]` makine sakin değildi,
donanım kodlayıcı yok) — kırmızı değil.

### CI — bu dalın son push'u

```
run id: 33729387252
commit: e162d31 (rapor commit'i, dalın son commit'i)
status: completed
conclusion: success
url: https://github.com/Teknesyum/VidShrink/actions/runs/33729387252
```

CI `VIDSHRINK_LAUNCHER_EXE` set etmiyor, yani A/B/C bantları CI'da atlanır
(rapordaki T0 maddesi 2'de bu zaten yazılı). CI `kosum-kapisi.ps1
-MinimumTotal 1134 -MaximumSkipped 30` ile tam süiti koşturuyor ve yeşil döndü.

### Tam süit — bu makinede, VIDSHRINK_LAUNCHER_EXE kurulu — TAMAMLANMADI

```
$ VIDSHRINK_LAUNCHER_EXE=... dotnet test -c Release > .calisma/T145/tam-suit.txt 2>&1
cikis=1
```

`.calisma/T145/tam-suit.txt` 23 satırda kesiliyor, "Toplam:" satırı yok.
Dosyanın içindeki xUnit iç saati şurada bir sıçrama gösteriyor:

```
[xUnit.net 00:05:06.90]  DonanimKodlayiciIslemciZamaniniOlculebilirYaziyorMu [SKIP]
[xUnit.net 04:08:40.17]  ExtremeCompressionTests.LiveQualityCurveShowsWhereTheCodecStopsCarryingThePicture [SKIP]
```

00:05 ile 04:08 arası ~4 saat. Ardından üç `ExtremeCompressionTests.Live*` SKIP
satırı ve dosya orada kesiliyor. `cikis=1` — `dotnet test` başarısız döndü.
Süreç şu an makinede koşmuyor (`tasklist` içinde testhost/VidShrink/ffmpeg yok),
yani ya çöktü ya da bu oturumun kesilmesiyle birlikte öldürüldü — hangisi
olduğunu **ayırt edemedim**.

Bu koşum T145'in `verify` alanındaki komut **değil** — sözleşmenin verify'ı
sadece `PerformanceCheckTests|UpdaterTests` filtresi ve o yeşil (yukarıya bak).
Tam süiti ben kendi inisiyatifimle, ekstra güvence için başlatmıştım.

## Ölçtüklerim ile varsaydıklarım

**Gerçekten ölçülen (rapordaki her sayı gerçek koşumdan):**
- A/C/D bantlarının boş+yüklü dağılımları (docs/olcumler/kalan-alti-bant.md içinde)
- E bandının 10 turluk `YonPayi` dağılımı
- F bandının 10 turluk oran dağılımı + k=1,5 geriye dönük aritmetiği
- Dört mutasyonun sekiz kolu (hepsi gerçek `dotnet build --no-incremental` + `dotnet test`)
- Verify kolu test sayıları ve verify koşumunun 74/76 sonucu
- CI'ın success dönmesi

**Ölçülmeyen / varsayılan:**
- Tam süitin bu dalda tam olarak yeşil olup olmadığı — CI yeşil ama CI
  `VIDSHRINK_LAUNCHER_EXE` set etmiyor, yani A/B/C bantlarını hiç koşturmuyor.
  Bu makinedeki `VIDSHRINK_LAUNCHER_EXE` kurulu tam süit koşumu tamamlanmadı.
- 4 saatlik sıçramanın hangi testten kaynaklandığı — dosyada o testin adı yok,
  sadece ondan sonraki üç SKIP satırı var. Tahmin bile üretmedim.

## Güvenilmeyecek şeyler

- `docs/olcumler/kalan-alti-bant.md` içindeki "T0'a madde 2" (üç bandın CI'da
  hiç değerlendirilmediği) hâlâ doğru ve güncel — bu bir hata değil, bilerek
  yazılmış bir açık.
- `.calisma/T145/tam-suit.txt` yarım kalmış bir dosya, "Toplam:" satırı yok.
  Sonuç çıkarmak için kullanma; sadece "bir yerde ~4 saat süren bir şey oldu"
  bilgisini taşıyor.
- Bağımsız denetçi ajanı (`ac5d20c451764e6b9`) makine meşgulken derleme yapmaya
  çalışıp **stalled** oldu (600 sn ilerleme yok), hiçbir bulgu üretmedi. Onun
  adına "denetlendi" deme — denetim hiç tamamlanmadı.

## Dokunduğum dosyalar

Hepsi `owns` içinde:
- `tests/VidShrink.Tests/PerformanceCheckTests.cs`
- `tests/VidShrink.Tests/UpdaterTests.cs`
- `docs/olcumler/kalan-alti-bant.md`

`owns` dışına yazılmadı. `src/**` bu dalda değişmedi (mutasyonlar sırasında
geçici olarak değiştirilip her seferinde `git checkout --` ile geri alındı;
`git diff main..HEAD -- src/` boş).

## Sıradaki adım

Ben olsam önce tam süitin neden ~4 saatte tıkandığını netleştirirdim: makineyi
tamamen sakinleştirip (`tasklist` sıfır testhost/ffmpeg/VidShrink), tek başına
`VIDSHRINK_LAUNCHER_EXE=... dotnet test -c Release --logger "console;verbosity=detailed" > tam-suit-2.txt 2>&1`
koşturur ve hangi testin sıçramadan hemen önce başladığını (log'daki son
timestamp'ten önceki satır) kaydederdim. Bu T145'in verify'ını etkilemiyor
(o zaten yeşil ve CI de yeşil), ama sözleşmeyi T0'a teslim etmeden önce bu
belirsizliği raporun T0'a bölümüne madde olarak eklemek gerekir — şu an
raporda yok, sadece bu devir dosyasında var.

Bağımsız denetimi de yeniden açmak gerekir; ilk deneme makine meşgulken
başladığı için stalled oldu, bulgu yok.
