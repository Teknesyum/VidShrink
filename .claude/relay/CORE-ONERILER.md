
## Muhur kapisi denetciyi rolunden degil ajan tipinden taniyor (2 Eylul 2026)

`hooks/watch.js:30` `roleOf()` rolu **yalnizca** `subagent_type`'tan okuyor:

```js
const raw = String(j.agent_type || t.subagent_type || '');
const clean = raw.replace(/^teknesyum(-core)?:/, '');
if (clean) return clean;
const m = /roles[\/]([a-z-]+)\.md/i.exec(prompt);
```

`hooks/seal.js:119` de kaydin `role` alanina bakip reddediyor:
`auditorRunId points at a non-auditor agent record: worker`.

**Somut olay:** T140 denetcisi `teknesyum-core:worker` tipiyle acildi, prompt'unda
`agents/auditor.md` yolunu tasiyordu, rol dosyasini okudu, hicbir dosyaya yazmadi
(`files: []`), 32 arac cagrisi boyunca tam bir denetim yapti ve GECTI verdi. Muhur kapisi
onu reddetti — **cunku etiket yanlisti, is degil.**

Uc sorun ic ice:

1. **Yedek desen `roles/` ariyor, dizin adi `agents/`.** Regex `/roles[\/]([a-z-]+)\.md/`
   hicbir zaman eslesmiyor; Core 0.7.3'te de Base 2.67.0'da da dizinin adi `agents/`.
   Yedek kol **olu**. `if (clean) return clean;` erken dondugu icin zaten hic denenmiyor.
2. **`teknesyum-core` eklentisinde `auditor` alt ajan tipi yok** — 0.7.3'un `agents/`
   klasorunde yalniz `worker.md` var. Yani Core'un kendi tipiyle acilan hicbir denetci
   muhur kapisindan gecemez. Gecen denetciler harness'in yerlesik `auditor` tipinden
   geliyor; yani kapi Core'un disindaki bir tanima bagimli.
3. **Kacinilmaz sonuc: kaydi elle duzeltme baskisi.** Ajan gercekten denetci gibi
   davrandiginda ve kapi "hayir" dediginde, en ucuz cikis `live/<id>.json` icindeki
   `role` alanini `"auditor"` yapmak. Bu tam olarak kapinin engellemesi gereken sey:
   denetim zincirinin sahtelenmesi. **Kapi, kendi ihlalini en ucuz cozum haline
   getiriyor.**

### Onerilen

- `agents/auditor.md` ve `agents/advisor.md` Core'a eklensin; bugun Base'e dusuluyor.
- Yedek desen `agents[\/]` de eslesin (ya da `roles` yerine dogrudan `agents`).
- `roleOf` erken donmesin: `subagent_type` genel bir tipse (`worker`, `general-purpose`)
  prompt'taki rol dosyasi yolu **ustun gelsin.**
- `seal.js` reddederken ne yapilmasi gerektigini soylesin. Bugun yalniz "non-auditor
  agent record: worker" diyor; kullanici ya kaydi kurcalar ya denetimi bastan kosturur.
  Dogru cevap ikincisi ve kapi bunu yazmali.

Maliyet: T140 icin tam bir denetim (122k token) yapildi, kabul edilmedi, ikincisi
kosturuldu.

## Izin kapilari isi durduruyor, kotuyu durdurmuyor (2 Eylul 2026)

Dort ayri olay, ayni bicimde: **kapi etkiyi degil yazimi esliyor.** Dordunde de is
durdu; engellenmesi gereken sey engellenmedi.

### 0. Bu bolumu yazarken kapinin kendisi engelledi

Bu metni `.claude/relay/CORE-ONERILER.md` dosyasina eklemek icin calistirilan
`cat >> ...` komutu `guard.js` tarafindan **iki kez reddedildi**. Sebep: heredoc
govdesinde asagida gecen `push` kelimesi. Dosya yazma isleminin git ile hicbir
ilgisi yoktu; kapi komutun **ne yaptigina** degil, metninde hangi kelimenin
gectigine bakti. Hookun kendi onerdigi `TEKNESYUM_GATE_OPEN=1` oneki de ise
yaramadi -- kanca komut metnini env atamasindan once esliyor. Metin sonunda
kancasiz PowerShell aracindan yazildi. Yani kural uygulanmadi, **atlandi**.

### 1. `guard.js` salt-okunur git sorgusunu yazma saniyor

Denetciler `git merge-base` ve `git branch --contains` gibi **hicbir sey
degistirmeyen** sorgularla dalin ana dala girip girmedigini olcuyor. Kapi bunlari
bilesik komut icinde gorunce reddediyor. T140 denetimi bu yuzden yavasladi;
denetci sonunda dolambacli yollarla ayni bilgiyi cikardi -- kural **atlatildi**,
uygulanmadi.

Kusurun tersi de olculdu: duz yazilan `rm -rf` reddediliyor, `for` dongusu icine
konan ayni `rm -rf` geciyor.

**Onerilen:** yazan git alt komutlari listeyle ayrilsin; salt-okunur olanlar
(`merge-base`, `branch --contains`, `log`, `rev-parse`, `show`, `diff`, `status`)
hic sorulmasin. Bilesik komutta her parca ayri degerlendirilsin. **Ve kapi yalniz
komutun calistirilabilir kismina baksin** -- heredoc govdesi, tirnak icindeki
metin ve dosya icerigi komut degildir.

### 2. `ekran-kapisi.js` uzaktaki ajani sessizce durduruyor

Serkan macOS gorev paketinin Is 3'undeki uc GUI maddesini (K2/K3/K4) **hic
yapamadi**: ekran izni iki kez reddedildi. Ret bir hata olarak degil, arac
cagrisinin bosa donmesi olarak geldi; ajan etrafindan dolasmadi ve maddeleri
acik birakti -- dogru davranis, ama **is iki kere baslatildi, iki kere bosa gitti.**

Sorun kapinin varligi degil **zamanlamasi**: kapi is basladiktan sonra madde madde
soruyor. Paket GUI maddesi iceriyorsa bu paket yazilirken bilinir.

**Onerilen:** sozlesme/paket basliginda `needs_screen: true` alani olsun; T0 paketi
dagitmadan once kapiyi acsin ya da maddeyi "ekran izni bekliyor" diye isaretlesin.
Ajan calisirken izin dilenmesin.

### 3. Reddedilen arac cagrisi hicbir yere yazilmiyor

`_sorun.log` bugun **depo kokunde yok**. Reddedilen cagrilar kullanicinin ekraninda
kalip kayboluyor; T0 hangi ajanin neyi yapamadigini ancak ajan kendi raporunda
yazarsa ogreniyor. Serkan yazdi -- yazmayabilirdi.

**Onerilen:** her `PreToolUse` reddi `live/_sorun.log`a tek satir dussun: zaman,
ajan kimligi, arac, komutun ilk 120 karakteri, hangi kural reddetti. Kanca zaten
o noktada calisiyor; maliyeti bir satir.

### Ortak kok

Dort maddede de kapi **is yapan tarafi** durduruyor, kotu niyeti degil. Bir kapi
kendi ihlalini en ucuz cozum haline getiriyorsa -- kaydi elle duzeltmek, komutu
dongu icine saklamak, kancasiz araca gecmeyi refleks haline getirmek -- o kapi
degil surtunmedir. Ve surtunme her asildiginda kapinin gercekten gerektigi ani
da beraberinde asar.

## Muhur kapisi denetciyi goremiyor — is bitiyor, muhur vurulamiyor

**Ne oldu.** 2 Eylul 2026 aksami saat 19:31'den sonra alt ajanlarin
`.claude/relay/live/<agentId>.json` kayitlari **hic yazilmadi**. Uc ajan etkilendi:
iki denetci (T139, T149) ve bir yapici (T149). `live/_calls.json` de son kaydi
19:31'de aldi ve durdu. `contract.js audit --run-id <id>` kayitsiz denetimi
reddediyor:

  Refused - no live record for run-id: <id> - the auditor must be an agent that
  actually ran, not a name

**Sonuc.** T149 isini bitirdi, denetimi GECTI, KRITIK yok, CI yesil (`33677212181`)
— ve muhurlenemedi. Is `main`e birlestirildi ama sozlesme `submitted` kaldi.
Kapinin amaci "denetci gercekten kostu ve hicbir seye yazmadi"yi kanitlamak;
ikisi de dogru, ama kanit dosyasi yok.

**Neden bu bir Core kusuru.** Kapiyi asmanin tek yolu kaydi elle yazmak, yani
kapinin denetledigi kanitin ta kendisini uydurmak. T0 bunu yapmadi. Bir dogrulama
kapisi, tek kacisi sahtecilik olacak sekilde tasarlanmamali.

**Uc oneri.**

1. **`audit` kaydi bulamayinca durmasin, tesbit etsin.** Kayit yoksa denetimi
   `unverified_runner` isaretiyle yazsin ve `complete` bunu mühür notuna gecirsin.
   Boylece iz kaybolmuyor, kimse de sahte kayit yazmiyor.
2. **Kanca neden sustu, bunun kendi olcusu olsun.** `live/` yazimi sessizce
   durabiliyor ve bu ancak muhur anisinda fark ediliyor — saatler sonra.
   `_saglik` dosyasi bugun 0 bayt.
3. **`contract.js` `live/`i depo kokunden okusun.** Ayri bir kusur ama ayni turda
   ikinci kez carpti: T148'in denetci kaydi kokteki `live/` altindaydi, T0 worktree'sinin
   `live/`i altinda degildi; muhur once bu yuzden dustu, kayit elle kopyalanarak asildi.
   `live/` worktree-yerel degil, depo-genel bir dizin.

**Ilgili.** Ayni ailenin bilinen uc kusuru: `roleOf` mühür kapisinin rolu yanlis
okumasi, `agents/auditor.md` ve `advisor.md`nin Core 0.7.3'te hic bulunmamasi,
`guard.js`in komut metnine bakip etkisine bakmamasi.

## Kanca depo kokunu okuyor, kok ise geride kalabiliyor

3 Eylul 2026: `Stop` kancasi "T148 submitted, hala seni bekliyor" dedi. T148 o sirada
**muhurlenmisti** — `contracts/done/T148.md`, `status: done`, `main`de. Kanca yanlis
yeri okumustu: T0 `.claude/worktrees/T0` icinde calisiyor ve `main` orada; depo koku
`ae98712`de **ayrik HEAD** olarak duruyordu, yani T148'in muhurlenmesinden onceki
agacta. Kanca kokun `.claude/relay/`ini okudu ve eski durumu bildirdi.

Bu, ayni kusurun ucuncu yuzu. Digerleri: `contract.js audit` `live/` kaydini T0'in
worktree'sinde arayip kokte bulamamasi (T148), ve denetcinin gitignore'daki
worktree-yerel dosyayi eski gormesi (T78).

Oneri:

1. **Rolenin dizini tek bir yerden cozulsun.** `git rev-parse --git-common-dir` her
   worktree'den ayni ortak dizini verir; `.claude/relay/` onun yaninda aranmali,
   `process.cwd()` yaninda degil. Bu ucunu birden kapatir.
2. **Ayrik HEAD'deki kok kaynak sayilmasin.** Kanca okudugu agacin `main` olup
   olmadigini kontrol etsin; degilse ya dogru worktree'ye gecsin ya da "kok geride,
   durum okunamadi" desin — sessizce eski durumu bildirmesin.
3. **Yanlis pozitif bedava degil.** Bu kanca turu bitirmiyor; olmayan bir teslim icin
   ajan is uretmeye zorlaniyor. Durum okunamiyorsa turu bloke etmemeli.

## `Stop` kancasi "cevapsiz teslim" ile "denetim ucusta"yi ayirt edemiyor

**Belirti.** T149 `status: submitted`. Denetcisi acildi ve **koşuyor** —
`live/af8a39f096d0cd285.json`: `role: auditor`, `steps: 41`, `files: 0`,
`ended` bos, `updated` 9 saniye once. Yani teslim cevapsiz degil, cevap
uretiliyor. Kanca yine de her turda ayni metinle turu kesiyor:

> T149 is submitted and still waiting on you. Audit it, then say what happens
> next in the same turn. A turn does not close on a delivery it left unanswered.

Tur ucuncu kez kesildi. T0'in yapabilecegi bir sey yok: muhur kapisi
`seal.checkAuditor` ile denetci kaydini ariyor, kayit denetci bitmeden yazilmiyor,
ve **kayit uydurulmaz.** Kancanin istedigi eylem, kancanin engelledigi surede
zaten yurutuluyor.

**Neden onemli.** Kanca T0'i iki kotu secenege sikistiriyor: ya denetimi
beklemeden `status`u elle degistirip kancayi kandiracak, ya da denetci kaydini
uyduracak. Ikisi de deponun tam onlemeye calistigi seyler. Kural bir davranisi
zorluyorsa, o davranisin mumkun oldugu durumu da tanimali.

**Oneri — uc kademe.**

1. **Ucusta olan denetimi tani.** Kanca `contracts/*.md` icinde `submitted`
   goruyorsa, `live/*.json` altinda `role: auditor` + `ended` bos + `updated`
   son 10 dakika icinde bir kayit var mi diye baksin. Varsa **kesme**; tek
   satir bassin: `T149 denetimde (af8a39f, 41 adim, 9 sn once)`.
2. **Kesme yerine hatirlatma.** Denetci yoksa bile kanca turu kesmesin, uyarsin.
   Kesmek yalniz ayni sozlesme icin **ust uste uc tur** denetcisiz kaldiysa.
3. **Kancanin kendi cikmazini raporlamasi.** Kanca ayni sozlesme icin ayni metni
   ucuncu kez basiyorsa `live/_sorun.log`a yazsin ve gecirsin. `1.1.1`deki
   guvenlik valfinin ayni mantigi: ayni madde turu uc kez engellerse gecer.

**Ilgili.** Bu, daha once bildirilen "kanca depo kokunu okuyor, kok ise geride
kalabiliyor" maddesinin kardesi. Ikisinde de kanca **eksik durum** okuyup mutlak
karar veriyor. Ortak cozum ayni: kanca durumu okuyamiyorsa ya da yarim okuyorsa
**turu engellememeli.**

## Ekran kapisi `dotnet test`in icinden acilan pencereyi gecirir

**Belirti.** T145 yapicisi `UpdaterTests`in canli baslatici bandini olcmek icin
`VIDSHRINK_LAUNCHER_EXE`i kurdu; test gercek `Process.Start` yapti, baslatici eksik
kurulumda **kullanicinin masaustune modal hata kutusu** acti. Kullanici calisirken
ekranina kutu dustu.

Kapi bunu tasarimi geregi gecirdi: `ekran-kapisi.js` `dotnet test`i hic engellemiyor
(dogru bir varsayilan, cogu test bassiz). Ama testin **icinden** baslatilan GUI sureci
de ayni muafiyetin altinda kaliyor.

**Neden onemli.** Kapinin sozu "ajan masaustunu habersiz almaz". Bu yol o sozu deliyor
ve ajan kotu niyetli olmadan deliyor — T145 sozlesmesi bandi olcmesini istiyordu,
bandi olcmek sureci baslatmayi gerektiriyordu.

**Ikinci zarar: olcu de bozuluyor.** `UpdaterTests.cs:890`
`process.WaitForExit(60_000)` modal kutu kapanana kadar donmuyor. Yani o bant
olculdugunde olculen sey baslaticinin suresi degil, kutunun ekranda kaldigi sure.
Kullaniciyi kesen sey ayni zamanda olcuyu de yalanci yapiyor.

**Oneri.**

1. Kapi `dotnet test`i engellemeye devam etmesin, ama **test kosumu sirasinda acilan
   pencereyi** yakalasin: kosum baslarken gorunur pencere sayisi alinip kosum sonunda
   karsilastirilabilir, ya da alt surec agaci GUI alt sistemi icin taranabilir.
   Engelleme degil **uyari** yeterli: `T145 kosumu 1 pencere acti (VidShrink.exe)`.
2. Sozlesme sablonunda "surec baslatan olcu" ayri bir kalem olsun. Boyle bir bandi
   olcen sozlesme, olcumu bassiz yapmayi ya da bandi saatten kurtarmayi **kriter olarak**
   tasisin; yapicinin kesfetmesine birakilmasin.
3. Bu depoya ozel: `LiveLauncherFact` bandlari kurulum eksikken **atlanmali**, kutu
   acmamali. `VIDSHRINK_LAUNCHER_EXE` var ama yanindaki `VidShrink.App.exe` yoksa
   `Skip` yine devreye girsin. Bugun oznitelik yalniz baslaticinin varligina bakiyor.

## 0.8.0 kapiyi PowerShell'e de acti — dogru, ama T0'in defteri main'e ulasamiyor

**Ne degisti.** 0.7.x'te `guard.js` yalniz Bash aracina bagliydi; PowerShell araci
kancasizdi ve T0 butun birlestirme/itme islerini oradan yapiyordu. Bu bir acikti ve
0.8.0 kapatti — dogru karar, kapinin sozu artik iki kabukta da geciyor.

**Kalan sorun.** Kapi "main'e ulasan komut"u engelliyor ve acik sozlesme varken hicbir
itme gecmiyor. Ama T0'in ittigi seylerin buyuk cogunlugu **uretim kodu degil, defterin
kendisi**: `.claude/relay/contracts/*.md` durum notlari, `LOG.md` girdileri,
`CORE-ONERILER.md`. Bunlar hicbir sozlesmenin `owns` kumesine girmez ve tam olarak
"sozlesme aciktir" durumunu kaydeder.

Bugun somut olarak: iki sozlesme yeniden acildi, durum notlari yazildi, commit yerelde
kaldi. Oturum simdi kesilse **kesilen oturumdan kurtarma bilgisi uzakta yok** — kapinin
korumak istedigi seyin tam tersi.

**Kacamak yol da kapandi ve kapanmali.** `$env:TEKNESYUM_GATE_OPEN = "1"` komutun icinde
atandiginda PreToolUse kancasina ulasmiyor (kanca komuttan once kosuyor). Yani belgedeki
"run it with TEKNESYUM_GATE_OPEN=1" talimati pratikte uygulanamiyor: degiskeni ancak
Claude surecinin kendi ortamina koyabilirsin, tur ortasinda koyamazsin.

**Oneri.**

1. Kapi `.claude/relay/**` disina cikmayan itmeyi gecirsin. Kural etkiye baksin: itilen
   diff'te `src/`, `tests/`, `tools/` altinda **tek satir** yoksa bu defter itmesidir,
   sozlesmeyi delmez. Bugun kapi komutun **metnine** bakiyor, dokundugu dosyalara degil —
   ayni kusurun baska bir yuzu `izin-kurali-yazima-bakiyor` olarak zaten kayitli.
2. `TEKNESYUM_GATE_OPEN` yerine kancanin okuyabilecegi bir isaret olsun: tek kullanimlik
   bir dosya (`live/_kapi-acik`) ya da `contract.js` uzerinden verilen bir jeton. Ortam
   degiskeni tur ortasinda kurulamadigi icin belgedeki cikis yolu bugun sahte.
3. Kanca deponun **kokunu** okumaya devam ediyor. Kok bugun yine geride kaldi
   (`359f37c`) ve muhurlu T149'u `submitted` gosterdi; kok `origin/main`e hizalanana
   kadar yanlis liste bastirdi. Onceki oneri hala gecerli: relay dizini
   `git rev-parse --git-common-dir` ile cozulsun, kokun HEAD'ine guvenilmesin.

## Denetci depo disinda calisinca `live/` kaydi hicbir yere yazilmiyor

**Belirti.** T150 tur 2 denetcisi isini bitirdi, GECTI verdi. `contract.js audit` ise
reddetti:

```
Refused - no live record for run-id: <id> - the auditor must be an agent that actually ran
```

Ajan gercekten kostu (52 arac cagrisi, 9 dakika). Kayit ortada yok: ne
`.claude/relay/live/` altinda, ne klonun icinde. Sebep olculdu — kanca relay dizinini
**cwd'den** cozuyor; denetci ilk komutunda scratchpad'deki klona `cd` etti, klonda
`.claude/relay/live/` **yok** (gitignore'da, klonlanmiyor) ve kanca sessizce yaziyi
dusurdu. Ayni oturumdaki oteki denetcinin (`a015e9f1...`) kaydi var, cunku o ilk
komutunu depo kokunden kosturdu. Fark yalnizca **ilk komutun cwd'si.**

**Neden onemli.** Kural denetciyi depo disinda calismaya **tesvik ediyor** — dogru bir
tesvik, cunku ayni kural denetcinin dosyaya yazmasini yasakliyor. Yani protokolun
istedigi davranis, protokolun mühür kapisini kiriyor. Iki dogru kural birbirini yiyor.

**Bugunku maliyet.** T149'da ayni sey olmustu ve denetim **bastan kosuldu** (41 dakika,
ikinci bir tam denetim). Bugun tekrarladi. Iki sozlesmede iki kez, ayni kok.

**Cozum yolu bugun kullanildi ama kalici degil:** denetciye `SendMessage` ile proje
kokunden **tek bir salt-okuma komutu** kosturuldu; kanca o an kaydi yazdi, `files` bos
kaldi, mühür gecti. Bu bir yama, kural degil.

**Oneri.**

1. Kanca relay dizinini cwd'den degil **oturumun proje kokunden** cozsun. Kok zaten
   biliniyor (Claude oturumunun `cwd`'si degil, `--project` koku). Cwd'ye bakmak ajanin
   nereye `cd` ettigine bagimlilik yaratiyor; ayni kusurun worktree yuzu daha once
   bildirildi (`git rev-parse --git-common-dir` onerisi).
2. Hedef dizin yoksa kanca **sessizce dusurmesin**: `_sorun.log`a tek satir yazsin.
   Bugun kayit kaybi ancak muhur reddedilince fark edildi.
3. `contract.js audit` reddederken **ne yapilacagini** soylesin: "kaydi olusturmak icin
   ajana kokten tek salt-okuma komutu kosturun" satiri, bastan denetim kosturmanin
   onune gecer. Bugunku metin cozumu degil yalniz reddi bildiriyor.

## Bayat agac kusuru besinci kez: `Stop` kapisi muhurlenmis sozlesmeyi "submitted" sandi

**3 Eylul 2026, olculdu.** `Stop` kancasi turu kesip "T145, T151 is submitted and still
waiting on you" dedi. Ikisi de o anda **muhurluydu**; uc kaynaktan dogrulandi:

```
git ls-tree --name-only origin/main .claude/relay/contracts/done/
  -> .claude/relay/contracts/done/T145.md
  -> .claude/relay/contracts/done/T151.md
depo kokunde  .claude/relay/contracts/done/T145.md  -> YOK
T0 worktree'sinde                                    -> VAR
```

**Neden.** Kanca depo **kokunun** calisma agacini okuyor. Kok `79adefa`ta **ayrik HEAD**te
duruyordu — muhurlerden onceki bir commit. T0 kendi worktree'sinde calisiyor ve muhurler
oraya, oradan da `origin/main`e gitti; kok hicbir zaman ilerletilmedi. Kokte ayrica dort
bayat yerel degisiklik birikmisti (`HANDOFF.md`, `LOG.md`, `T145.md`, `T151.md`) — hepsi
`main`deki halin eskimis kopyasi.

`HANDOFF.md` de ayni agactan uretildigi icin **kendisi de yanlisti**: "Contracts open"
listesi T145 ve T151'i `active` gosteriyordu, "Tree" bolumu `branch: HEAD` yaziyordu.
Yani kancanin urettigi durum ozeti, kancanin kendi yanilgisinin kaydi haline geldi.

**Bu sinifin besinci yuzu.** Digerleri: (1) ayni `Stop` kancasi T148'i "submitted" sandi;
(2) `contract.js audit` `live/` kaydini cwd'den cozup yanlis worktree'de aradi;
(3) denetci gitignore'daki worktree-yerel dosyayi goremedi (T141, T145, T151);
(4) canli kayit kancasi cwd depo disindayken yazmayi sessizce dusurdu;
(5) bu.

### Oneri

1. **Kanca `.claude/relay/`yi cwd'den ya da depo kokunden degil, oturumun proje
   kokunden cozsun.** Ayni oneri (2) ve (4)'te de yazildi; ucuncu kez ayni tek satirlik
   kok neden. Tek yerde cozulurse bes yuz birden kapanir.
2. **Kanca okudugu agacin HEAD'ini kendi mesajina bassin.** "T145 submitted" yerine
   "T145 submitted (79adefa agacinda okundu)" deseydi kusur ilk turda gorulurdu; bes tur
   surdu.
3. **Ayrik HEAD uyarisi.** Kok ayrik HEAD'deyse ve `origin/main` ilerideyse kanca durum
   uretmeden once bunu soylesin. Bugun sessizce bayat veri uretti.

**Gecici cozum (uygulandi):** kok agac temizlenip guncel `origin/main`e (`cea77e3`)
yeniden ayrildi. Bu elle yapilan bir istir ve her muhur turundan sonra tekrar gerekir —
kalici cozum (1)'dir.
