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

`concurrency` bloğu asagida.

## K3 — sonra olcum

(Degisiklik sonrasi doldurulacak.)
K2 adim 1 kaniti: bu commit, yukaridaki kosumu iptal etmeli.
### K2 adim 1 kaniti (varsayilan cancel-in-progress)

`c356f4f` itildi -> kosum `33602997828` `in_progress`e girdi. Hemen ardindan
`2b5ef68` itildi -> `33602997828` `cancelled` oldu (durum `completed`,
sonuc `cancelled`), yeni kosum `33603041850` `in_progress`e gecti.
Ayni dala arka arkaya itilen commit'lerde yalniz sonuncusu kosuyor,
dogrulandi.

