# CI maliyeti — T121

## K1 — once olcum (degisiklik oncesi taban)

Pencere: `2026-09-01T05:11:21Z` .. `2026-09-02T05:11:21Z` (24 saat).

Ureten komut:

```
gh run list --workflow ci.yml \
  --json conclusion,createdAt,databaseId,event,headBranch,headSha,status,updatedAt,workflowName \
  --created "2026-09-01T05:11:21Z..2026-09-02T05:11:21Z" --limit 1000
```

210 kosum dondu (bu komut, ayni pencereyle, bu rapor yazilirken tekrar
calistirildi ve yine 210 verdi — pencere kapali oldugu icin sonuc sabit).

Siniflandirma, her kosumun `headSha`sina bakarak: `git diff --name-only
<sha>~1 <sha>` (ilk ebeveyne karsi — hem duz commit hem merge commit'inde
dogru calisir) `src/`, `tests/`, `.github/` altinda **tek satir bile**
degistirmiyorsa `docsonly`, degistiriyorsa `meaningful`.

### Kosum-seviyesi sayim (K1'in sordugu soru budur — "kosumlari say")

| kova | kosum sayisi | tamamlanan | toplam dakika | ort. dakika |
|---|---|---|---|---|
| meaningful | 38 | 38 | 549.7 | 14.47 |
| docsonly | 172 | 156 | 2007.9 | 12.87 |
| **toplam** | **210** | **194** | **2557.6** | **13.18** |

Dakika, `updatedAt - createdAt` farkindan (kosum basina, yalniz
tamamlananlar icin) — `gh run list` suresi dogrudan vermiyor, bu yaklasim
kullanildi.

**docsonly payi (kosum sayisinda): %81.9**
**docsonly payi (tamamlanan kosum dakikasinda): %78.5** (2007.9 / 2557.6)

### Benzersiz commit sayimi ile fark — not, hata degil

`classify2.pkl` icindeki `meaningful`/`docsonly` kumeleri **benzersiz
commit SHA'si** tutuyor: 37 meaningful + 171 docsonly = 208 SHA. Bu, 210
kosumdan **az** — cunku 2 SHA iki kosum uretti:

- `68cb3c93...` (docsonly): `T115-ci-ffmpeg` dalina push (`33582206982`) ve
  `main`e push (`33582057955`) — ayni commit iki dala girdi, her ikisi de
  kendi `on: push` tetiklemesini calistirdi.
- `a52b4a4e...` (meaningful): `main`e iki ayri push kosumu
  (`33552853969`, `33552841265`) — ayni SHA, iki kosum kaydi.

K1'in sorusu "kosumlari say" oldugu icin bu rapordaki rakam **kosum
seviyesi** (38/172/210), commit seviyesi degil. Sozlesmenin kendi ornek
kosumlariyla (`33592853416`, `33592907133`, `33592994397`, `33593233394`,
`33593273426` — hepsi `main`, hepsi bes ayri sozlesme-metni commit'i)
capraz kontrol edildi: bes kosumun bes SHA'si de `docsonly` kumesinde
cikti, siniflandirma tutarli.

## K2 — degisiklik

```yaml
concurrency:
  group: ${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: ${{ github.ref != 'refs/heads/main' }}
```

### Adim 1 kaniti (varsayilan cancel-in-progress)

`c356f4f` itildi -> kosum `33602997828` `in_progress`e girdi. Hemen ardindan
`2b5ef68` itildi -> `33602997828` `cancelled` oldu (durum `completed`,
sonuc `cancelled`), yeni kosum `33603041850` `in_progress`e gecti.
Ayni dala arka arkaya itilen commit'lerde yalniz sonuncusu kosuyor,
dogrulandi.

### Adim 3 kaniti (main istisnasi ifadesi — gecici ikame ile)

Gercek `main`e sinama commit'i itmeden, `cancel-in-progress` ifadesindeki
`refs/heads/main` yerine gecici olarak kendi dalim (`refs/heads/T121-ci-maliyeti`)
konuldu: `cancel-in-progress: ${{ github.ref != 'refs/heads/T121-ci-maliyeti' }}`.
Bu dalda calisirken ifade `false` degerlendirmeli — yani ayni davranis
`main`de beklenen davranisla ayni: kosum iptal edilmeyip kuyruklanmali.

`bcee47e` itildi (commit `2b5ef68` hala `in_progress`ken). Onceki cift
(`c356f4f` -> `2b5ef68`) aninda `cancelled` uretmisti; bu ciftte
`bcee47e` (`33603335567`) `in_progress` olan `2b5ef68`'i (`33603041850`)
**iptal etmedi** — `pending` durumunda kuyruga girdi. Bu, ifadenin
`concurrency` blogunda gercekten degerlendirildigini ve `false` sonucunun
`cancel-in-progress`i etkisiz biraktigini kanitlar. Kosumun kendisinin
tamamlanmasi beklenmedi — kanit kuyruk durumundan zaten okunuyor.

Ispat sonrasi ifade gercek `refs/heads/main`e geri cevrildi ve o haliyle
commitlendi.

## K3 — sonra olcum

Dal `main`e henuz birlesmedi (sozlesme geregi birlesmeyecek), o yuzden
`concurrency` blogu su an yalniz `T121-ci-maliyeti` dalinda calisiyor.
Bu, iki ayri sonuc uretiyor: (a) dogrudan gozlenen, kucuk olcekli bir
kanit — kendi dalimdaki gercek iptaller; (b) K1'in ayni penceresine
kuralı geriye donuk uygulayarak cikan bir **projeksiyon** — depo genelinde
`main`e birlestikten sonra ne olacaginin tahmini. Ikisi karistirilmadan
ayri yazilir.

### (a) Dogrudan gozlenen kazanc (kendi dalim, gercek)

Ayni K1 komutu, degisiklik sonrasi bir pencerede tekrar calistirildi:

```
gh run list --workflow ci.yml \
  --json conclusion,createdAt,databaseId,event,headBranch,headSha,status,updatedAt,workflowName \
  --created "2026-09-01T05:11:21Z..2026-09-02T05:11:21Z" --limit 1000
```

Ayni pencere icin yine 210 kosum dondu — beklenen, cunku pencere kapandi
ve `main`e henuz birlesme yok; bu komutun tekrari K1 sayisinin
tekrar-uretilebilir oldugunu dogruluyor, tek basina "sonra"yi olcmuyor.
Asil "sonra" `T121-ci-maliyeti` dalindaki kosumlarin kendisinde:

- `33602997828` (`c356f4f`): 07:19:33'te basladi, `2b5ef68` itilince
  07:20:12'de `cancelled` oldu — **39 saniye** calisti. K1'in ortalama
  tamamlanan-kosum suresi (13.18 dk) ile kiyaslanirsa, iptal edilmeseydi
  yaklasik 12-19 dk suren bir kosumun sadece 39 saniyesi harcandi.
- `33603041850` (`2b5ef68`): 07:20:05'te basladi, `bcee47e` `main`
  istisnasi (gecici ikame) ile kuyruklandiginda iptal edilmedi (kanit,
  K2 bolumunde); ref gercek `main`e geri cevrilip `e1f0b27` itilince
  07:32:05'te `cancelled` oldu — yaklasik **12 dakika** calismisti.
- `33603335567` (`bcee47e`): hic `in_progress`e gecmeden, kuyrukta
  beklerken `e1f0b27` tarafindan iptal edildi.

Bu ucu, mekanizmanin calistiginin dogrudan kaniti — ama tek basina
"repo genelinde X dakika/yuzde kazanildi" demek icin yeterli olcek
degil, cunku degisiklik henuz `main`e birlesmedi.

### (b) Geriye donuk projeksiyon (depo genelinde, K1 verisine kural uygulanarak)

K1'in 210 kosumluk verisine concurrency kurali (ayni is akisi + ayni dal
= grup; `main` haric grup icinde ust uste binen kosumun oncekisi iptal
edilir) geriye donuk uygulandi: ayni dalda ardisik iki kosumun zaman
araligi ortusuyorsa (`onceki.updatedAt > sonraki.createdAt`), onceki
kosum kurami altinda iptal edilmis olurdu; kazanc, ortusen sureden
hesaplandi.

- **`main`-disi dallarda 42 kosum cifti** ortusuyordu — kural olsaydi
  bunlarin oncekileri iptal edilir, toplam **396.6 dakika** tasarruf
  edilirdi. Bu, K1'in toplam tamamlanan-kosum dakikasinin (2557.6)
  **%15.5**'i — **`%81.9` degil.** `%81.9`, kosumlarin docs-only *payi*;
  tasarruf yalniz **ayni dalda art arda, ortusen** kosumlardan gelir —
  yalniz basina itilen (arkasindan hemen yeni bir itme gelmeyen) bir
  docs-only kosum tam suresince calismaya devam eder, concurrency onu
  kesmez.
- **`main` dalinda 74 kosum cifti** de ortusuyordu — bunlar istisna
  sayesinde iptal **edilmeyecek** (mühür kaniti korunuyor). Bu sayi,
  sozlesmenin `Baglam` bolumundeki gozlemi (5 ust uste `main` kosumu,
  4 kosumluk kuyruk) dogruluyor: kuyruklanma sorunu gercek ve sik, ve
  `main` istisnasi onu tam da hedefledigi yerde etkisiz birakmiyor.

Ureten komut (K1 dosyasindan, `event == "push"`, dal bazinda sirali
zaman araligi kesisimi): `.calisma/T121/` altindaki gecici Python
betiginde — betik `docs/`e tasinmadi, tek seferlik olcum, `tools/`a
girecek kadar tekrar kullanilan bir arac degil.

**Ozet cumlesi:** degisiklik `main`e birlestiginde, K1 penceresindeki
gibi bir gunde, docs-only kosumlarin **hepsi** degil, **ortusenleri**
kesilir; bu olculen pencerede o **%15.5** (396.6/2557.6 dk) demek,
kosum sayisinin %81.9'unun docs-only olmasi degil.
