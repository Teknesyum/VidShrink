# Devir notu - 2026-09-06, dizustunden masaustune

Bu dosya "devam" yazildigi anda okunacak. Kaybolmamasi gereken her sey burada.
Son guncelleme: T176 tur 1 teslimi geldikten sonra.

## "devam" DENDIGINDE - once bunlari yap, sorma

Kullanici masaustunde yalnizca `git pull` yapip **devam** yazacak. Gerisi senin.
Asagidaki dort adim sirayla, onay istemeden:

**1) Kaniti geri getir.** Butun ham olcum dosyalari `.calisma/` altinda ve gitignore'lu;
gizli depoda duruyorlar. Depo yoksa klonla, varsa cek, sonra `.calisma/`ye ac:

    git -C %USERPROFILE%\.claude	eknesyum-ozel pull
    (yoksa: git clone https://github.com/Teknesyum/teknesyum-ozel.git %USERPROFILE%\.claude	eknesyum-ozel)
    (sparse ise: git -C ... sparse-checkout add vidshrink)
    robocopy %USERPROFILE%\.claude	eknesyum-ozelidshrink\calisma .calisma /E

**2) T176 worktree'sini kur.** Dizustundeki `..\VidShrink-T176` bu makinede yok, ama dal
uzakta duruyor:

    git worktree add ..\VidShrink-T176 T176-oynatici-girdi

**3) Yapiyi ve olcuyu bir kez dogrula** (yesil degilse denetciyi dagitma, once sebebi bul):

    dotnet test -c Release --filter "OynaticiGirdiTests|PlayerTabTests|LanguageTests"

**4) T176 tur 1 denetcisini dagit.** Yapici teslim etti, sira denetcide. `auditor` ajanina
Turkce, soyle bir dagitim ver - ozetleme, sozlesmeyi ve raporu kendisi okusun:

> T176 sozlesmesinin tur 1 teslimini denetle. Sozlesme `.claude/relay/contracts/T176.md`,
> teslim `origin/T176-oynatici-girdi` = d2e9937, rapor `docs/olcumler/oynatici-girdi.md`,
> ham kanit `.calisma/T176/`. Her K maddesinin sayisini ham dosyaya karsi dogrula; en az
> uc olcumu repo disi temiz bir kopyada kendin kos. Bu deponun imza kusuru "tablo dogru,
> onu ozetleyen cumle yanlis" - rapordaki her mutlak nicelik cumlesini ayri dene.
> Kod yazma, duzeltme. GECTI/KALDI hukmu, kritik sayisi ve borc listesi ver.
> Bash cagrilarinda timeout: 600000 kullan, run_in_background kullanma.

Hukum GECTI ise: yapici dalini `TEKNESYUM_GATE_OPEN=1 git merge --no-ff d2e9937` ile
`main`e al, elle muhurle (`contracts/done/`, `status: done` + `result: passed`,
`ledger.jsonl`'a `coreVersion: manual-0.16-no-contract-js`), it, worktree'yi kaldir.
KALDI ise tur 2 kapsamini sozlesmeye yaz ve yapiciyi yeniden dagit.

Bunlar bitmeden yeni sozlesme acma. T176'dan sonraki sira asagidaki borc listesi.

## Nerede duruyoruz

Surum **v0.3.0** (`Directory.Build.props:7`), etiket `v0.3.0` itildi, `main` = `fd6fe0c`.
Uzakta yalnizca iki dal var: `main` ve `T176-oynatici-girdi`. 23 yerel + 16 uzak dal
silindi; sapan alti dal (T117, T140, T141, T145, T147, T150) kullanici onayiyla silindi -
birlestirilseydi bayat test esikleri ve derlenmeyen bir agac (T147) geri gelecekti.

`dotnet test` en son yesildi: `LanguageTests|LocalizationTests` 84/84,
`KabukIstegiTests|OluUyeTests` 40/40, `WindowLayoutTests` 21/21.

## Bekleyen is - T176 (oynatici sekmesi), tur 1 TESLIM EDILDI

Sozlesme `.claude/relay/contracts/T176.md`, durum `active`, model opus.
Yapici ajan tur 1'i bitirdi ve isini itti: `origin/T176-oynatici-girdi` = **d2e9937**,
worktree `..\VidShrink-T176` temiz, commit'lenmemis is yok.

**Siradaki adim denetci.** Bilerek dagitmadim - kullanici masaustune geciyordu ve bir
denetim turu yeni bir kosum demek. Masaustunde ilk is: `auditor` ajanini T176 tur 1'e
dagit, sonra hukume gore muhurle ya da tur 2 kapsami yaz.

Yapicinin K -> ham dosya -> sayi tablosu (hepsi `.calisma/T176/` altinda, gitignore'lu;
gizli depoya kopyalandi, yukaridaki 1. adim geri getiriyor - iki kucuk ornek klip dahil):

| K | ham dosya | sayi |
|---|---|---|
| K1 izgara | `k1-izgara.txt` | 9 satir, dokuz girdinin dokuzu oncesi->sonrasi |
| K1 sekme | `k1-sekme.txt` | sekme sayisi 6, oynatici 5. sirada, en `Player` / tr `Oynatici` |
| K1+K2 | `k1-pencere-yoneticisi.txt` | 10 jest gonderildi, 10 iz satiri |
| K3 sahte | `k3-birikme.txt` | 10 tik -> hedef 10 sn, 2 arama |
| K3 gercek boru | `k3-gercek-boru.txt` | konum 10 sn, 2 arama, 63,4 + 76,7 ms, toplam 141,7 ms, 150 ms asan 0 |
| K4 | `k4-tam-ekran.txt` | once/tam ekran/geri donus uc satir birebir |
| K5 | `k5-menu.txt` | iki dilde uc satir, `menu acildi: True` |
| K6 | `k6-kabuk.txt` | `startup tab=5`, acilistaki sekme 0 -> 5 |
| K7 | `k7-mutasyon.txt` | (a) 2 kaldi/6 gecti, (b) 2/6, (c) 1/7; geri alinca 69/69 yesil |
| K8 | `k8-kol-sayisi.txt` | `OynaticiGirdiTests` 7, `PlayerTabTests` 1, `LanguageTests` 61; sifir bulan kol yok |

Rapor `docs/olcumler/oynatici-girdi.md` (211 satir, d2e9937).

Girdi haritasi degistirilmedi (kullanicinin verdigi, degistirilemez):
tekerlek 1 sn, ctrl+tekerlek 10 sn, shift+tekerlek 60 sn, ctrl+shift+tekerlek 300 sn,
alt+tekerlek yerel yakinlastirma, sag tik ve bosluk oynat/duraklat,
orta tik tam ekran <-> onceki, sol tik simdilik bos (olay geliyor, durum degismiyor - sayacla kanitli).

**Yapicinin duzeltmedigi, bildirdigi Ffmpeg kusurlari** - denetciye ve sonraki tura girdi:

1. `ContinuousPlayback.Pump` hala `catch { }`; ffmpeg cokunce `Faulted` yayilmiyor.
   Arayuz tarafinda `StallWatch` ile ortuldu, kaynak duruyor.
2. `SeekAudio` arama basina yeni surec aciyor. K3 sessiz klipte olculdu, bu maliyet sayiya girmedi.
3. `LatestVideoPts` CFR varsayiyor; VFR kaynakta konum sessizce kayar (VFR olculmedi).
4. `ProcessesStarted` arama ile oynatma baslatmalarini ayirmiyor; `0 -> 2` sayisi hangi
   surecin ne icin acildigini soylemiyor.

**Olculmedi**: sesli kaynakta arama maliyeti ve senkron; VFR kaynak; tam ekranda isletim
sisteminin verdigi fiziksel dikdortgen; Explorer'da gercek cift tiklama (T169'un alani);
alt+tekerlegin ikinci makinede tekrari.

T176'nin devraldigi kusurlar ayrica `docs/olcumler/oynatici-boru.md` "T176 devri"nde.

## Kapanmamis borclar

**T171** (muhurlu, `.claude/relay/contracts/done/T171.md`, 9 borctan 4'u acik):

- b3: kurulu uygulamayla gercek sag menu tiklamasi / uctan uca sonda **yok**.
- b4: mutasyon (i) yalniz `GerekceOlcusuSurecDilindenEtkilenmez(tr)` kolunu dusuruyor;
  `en` kolu makineden bagimsiz gosterilmedi.
- b5: `tests/VidShrink.Tests/WindowLayoutTests.cs:867` ayni is-parcacigi desenini tasiyor;
  bugun yesil ama seam duruyor.
- b6: `src/VidShrink.App/ShrinkJobWindow.axaml.cs:103` hala `Strings.Use(_language)` ile
  surec genelindeki dili degistiriyor.

**T172**: `PlanCalculator.cs:868` `QualityFloorTargetMb` hala kodek-kor;
`CodecModel.cs:68-74`'un kayitli iddiasi curutuldu, ortada iki paralel taban modeli var.

**DEVIR.md 5.3**: README tazelemesi yapilmadi.

`VidShrink.PlanBaseline`, `VidShrink.PlayerProbe`, `VidShrink.PlaybackProbe` bilerek
`VidShrink.sln` disinda birakildi - eksik degil, karar.

## Arayuz bulgulari (kullanicinin "hicbiri eklenmemis" sikayeti)

Derin kodek ayarlari **zaten var** ve calisiyor: Kodlama Kipi, CRF, On Ayar/Hiz,
Ses Hedefi (kbps), Ses Kanali, Cozunurluk Tabani, Kare Hizi Tabani, Kodlayici Yolu,
Kodek Kilidi - hepsinin varsayilani "Otomatik". Gorunmuyorlar cunku Kucult sekmesinde
tek uzun kaydirmanin en dibindeler. **Acik is: bu sekmeyi bolumlere ayirmak.**

"Okunmayan yazilar" tek satira indi ve duzeltildi (`fd6fe0c`): `Text.cs:27` her arayuz
metnini `LanguageCatalog.Display` -> `Title`'dan geciriyordu, baslik kurali govde
cumlelerini de kelime kelime buyutuyordu. Artik `ReadsAsProse` gecidi var; govde cumlesi
dil dosyasindaki gibi kaliyor, yalniz satir basi `CapitaliseWord`'den geciyor.
Dort sabitleme eklendi, biri iki dilde tum dil dosyasini suzuyor (tr 199, en 191 govde degeri).

**Kalan arayuz isteri**: karsilastirma/onizleme alani hala fazla buyuk.

## Kanit nerede

`.calisma/` gitignore'lu ve bu makinede **3.5 GB** (T171 tek basina 3.0 GB video).
Git'e girmez. Kucuk metin kaniti, olcum betikleri ve ekran goruntuleri (112 dosya, 4.8 MB)
gizli depoda:

    ~/.claude/teknesyum-ozel/vidshrink/calisma/

Icinde: T171 ve T158 ham kosumlari + `devralinan/`, T176 tur 1'in on ham dosyasi ve iki
kucuk ornek klibi, `gorunum/` alti dokuz ekran goruntusu ve yakalama betikleri,
`test-ciktilari/`. Yukaridaki 1. adim bunu `.calisma/`ye geri aciyor.

Gitmeyen tek sey buyuk medya: `.calisma/kaynak`, `kaynak-genis`, `t57` ve T171'in video
klasorleri **yalniz dizustunde**. Bunlara dayanan bir olcum tekrarlanacaksa kaynak
videolar yeniden uretilmeli; T176'nin ihtiyaci olan klip kopyada var.

## Makine kurallari (unutulmasin)

- `contract.js` 0.16'da kaldirildi; muhur **elle**: sozlesme `contracts/done/`e tasinir,
  `status: done` + `result: passed` eklenir, `ledger.jsonl`'a
  `coreVersion: manual-0.16-no-contract-js` satiri yazilir.
- Muhur sirasi: yapici dalini itsin -> `TEKNESYUM_GATE_OPEN=1 git merge --no-ff <sha>`
  (tek basina, boru/`&&`/`;`/yeni satir/`cd` yok) -> muhur -> `TEKNESYUM_GATE_OPEN=1 git push origin main`.
- `guard.js` cwd'yi ana agaca sabitliyor: **worktree icinde Edit/Write calismaz**,
  ajanlar `python - <<'PY'` ya da `sed` kullanmali.
- Alt ajana verilen Bash cagrilarinda `timeout: 600000` sart; 120 sn'yi asan komut arka
  plana atiliyor ve ajanin turunu oldururuyor. `run_in_background` yasak.
- Once teslim, sonra denetci. Ters sira turu kilitliyor.
- vstest filtresi alt-dizge esler ve **sifir eslesen kol sessizce exit 0 doner** -
  her kolu `--list-tests` ile say.
- `Strings.Use(...)` surec genelinde dil degistirir; farkli is parcaciklarindan okuma
  kirilgan olcu uretir.
- Yerel dil JSON'lari BOM tasir: `encoding='utf-8-sig'`.

Bu deponun imza kusuru: **tablo dogru, onu ozetleyen cumle yanlis** (26 kayitli ornek).
