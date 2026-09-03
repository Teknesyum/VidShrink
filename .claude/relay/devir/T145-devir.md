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
- **K5 (kol başına test sayısı + CI) — bitti.** Verify kolları yeşil (74/76,
  2 atlanan ortam kapılı), CI yeşil. Ek olarak tam süit de (bu sözleşmenin
  verify'ı değil, kendi inisiyatifimle) artık temiz koşuyor — aşağıya bak.

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

### Tam süit — bu makinede, VIDSHRINK_LAUNCHER_EXE kurulu — ARAŞTIRILDI VE ÇÖZÜLDÜ

İlk koşum (`.calisma/T145/tam-suit.txt`) 23 satırda kesilmiş, "Toplam:" yok,
xUnit iç saatinde 00:05:06 → 04:08:40 arası ~4 saatlik açıklanamayan bir sıçrama
vardı, `cikis=1`.

**Araştırma:** aynı komut `--logger "console;verbosity=detailed"` ile, makine
tamamen sakinken (`tasklist` içinde testhost/ffmpeg/VidShrink sıfır), başka
hiçbir eşzamanlı iş yokken tekrar koşuldu:
`.calisma/T145/tam-suit-detay.txt`.

```
Test Çalıştırması Başarılı.
Toplam test sayısı: 1479
     Geçti: 1464
    Atlandı: 15
 Toplam süre: 18,9454 Dakika
cikis=0
```

**Sıçrama tekrarlanmadı.** 18,9 dakikada, sıfır kırmızı, sıfır durgunluk
(180 sn eşikli bir izleyici tüm koşum boyunca dosyayı izledi, hiç tetiklenmedi).
15 atlanan test tek tek sayıldı, hepsi `Live*`/donanım ortam kapılı
(`CalibrationProbeTests` 3, `ExtremeCompressionTests` 3, `FillBandTests` 1,
`HardwareEncoderTests`/`HardwareFlagTests`/`HardwareRateControlTests`/
`HardwareVerdictTests` toplam 7, `PerformanceCheckTests` 1,
`PlaybackFrameSourceTests` 2 — üstü kapalı ilan, T145'in bandlarıyla ilgisi yok).

**Sıçramanın en olası sebebi — dolaylı kanıt, kesin kanıt değil.** İlk koşumla
tam eşzamanlı olarak bağımsız denetçi ajanı (`ac5d20c451764e6b9`) açılmıştı ve
kendi sonuç metninde şunu bırakmıştı: *"The machine is busy — another agent's
testhost holds the Release binaries. I'll build to an isolated output path
instead of disturbing it."* — yani iki ayrı süreç aynı anda aynı Release
ikililerine (`bin/Release/net8.0`) erişmeye çalışıyordu. Bu, MSBuild/testhost
dosya kilidi çekişmesiyle uzun bloklanmalar üretebilecek bilinen bir desendir.
Ayrıca o oturum tam bu sırada devir protokolüyle kesilip model değişimi
(`/model claude-sonnet-5`) yapıldı — arka plan sürecinin bu kesinti sırasında
işletim sistemi tarafından askıya alınmış olması da mümkün, dışlanamadı.

**Kesin teşhis yok.** Ne bir çökme kaydı (event log, crash dump) ne de hangi
testin sıçramadan hemen önce/sonra çalıştığına dair güvenilir bir iz var —
sıçramadan hemen önceki satır bir `[SKIP]`'ti (anlık, süre almaz), yani asıl
tıkanma bir testin İÇİNDE değil, `dotnet test`/MSBuild seviyesinde bir yerde
olmalı. Temiz tekrar koşum (yalnız süreç, başka rakip iş yok) sorunu hiç
göstermedi; bu da eşzamanlı-erişim teşhisini destekliyor ama kanıtlamıyor.

**Sonuç:** T145'in kendi kodunda ya da testlerinde bir hata değil. Verify
zaten temiz koşuyordu (yukarı bak), şimdi tam süit de temiz koşuyor. Riskli
olan şey — iki ajanın aynı makinede aynı Release ikililerine eşzamanlı
erişmesi — bu sözleşmenin kapsamı dışında bir altyapı/orkestrasyon konusu.

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
- 4 saatlik sıçramanın **kesin** sebebi — bulunamadı, dolaylı kanıtla en olası
  açıklama yazıldı (yukarı bak). Tahmin bile üretmedim, sadece gözlenen
  eşzamanlılığı ve tekrarlanmadığını kaydettim.

## Güvenilmeyecek şeyler

- `docs/olcumler/kalan-alti-bant.md` içindeki "T0'a madde 2" (üç bandın CI'da
  hiç değerlendirilmediği) hâlâ doğru ve güncel — bu bir hata değil, bilerek
  yazılmış bir açık.
- `.calisma/T145/tam-suit.txt` yarım kalmış bir dosya, "Toplam:" satırı yok.
  Sonuç çıkarmak için kullanma; sadece "bir yerde ~4 saat süren bir şey oldu"
  bilgisini taşıyor. Geçerli olan `.calisma/T145/tam-suit-detay.txt`
  (Toplam 1479, Geçti 1464, Atlandı 15, cikis=0).
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

4 saatlik sıçrama araştırıldı ve tekrarlanmadı (yukarı bak); T145'in kendi
işiyle ilgisi yok göründüğü için sözleşmeyi bu belirsizlikle bloke etmedim.
Ben olsam şimdi bağımsız denetimi **tekrar** açardım — ilk deneme makine
meşgulken (tam süit + kendisi aynı anda) başladığı için stalled oldu ve hiç
bulgu üretmedi. Makine şu an sakin, denetim şimdi temiz koşabilir. Denetim
geçerse T0'a teslime hazır; geçmezse bulguları bu dosyaya ekleyip commit et.
