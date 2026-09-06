# Devir notu - 2026-09-06, dizustunden masaustune

Bu dosya "devam" yazildigi anda okunacak. Kaybolmamasi gereken her sey burada.

## Nerede duruyoruz

Surum **v0.3.0** (`Directory.Build.props:7`), etiket `v0.3.0` itildi, `main` = `fd6fe0c`.
Uzakta yalnizca iki dal var: `main` ve `T176-oynatici-girdi`. 23 yerel + 16 uzak dal
silindi; sapan alti dal (T117, T140, T141, T145, T147, T150) kullanici onayiyla silindi -
birlestirilseydi bayat test esikleri ve derlenmeyen bir agac (T147) geri gelecekti.

`dotnet test` en son yesildi: `LanguageTests|LocalizationTests` 84/84,
`KabukIstegiTests|OluUyeTests` 40/40, `WindowLayoutTests` 21/21.

## Su anda kosan is - T176 (oynatici sekmesi)

Sozlesme `.claude/relay/contracts/T176.md`, durum `active`, model opus.
Yapici ajan **hala calisiyordu** ve worktree'si burada:

    C:\Users\Teknesyum\Desktop\Projeler\VidShrink-T176   (dal T176-oynatici-girdi)

Dalda itilmis son commit `c159e23`; worktree yerelde `1392350`'e kadar ilerlemis ve
`MainWindow.axaml.cs`, `PlayerView.axaml(.cs)`, `OynaticiGirdiTests.cs` uzerinde
commit'lenmemis degisiklik birakmis olabilir. **Masaustunde ilk is bu worktree'nin
durumuna bakmak**: ya ajanin isini toplayip teslimi tamamlat, ya sozlesmeyi tur 1
kaldi sayip yeniden dagit.

Girdi haritasi (kullanicinin verdigi, degistirilemez):
tekerlek 1 sn, ctrl+tekerlek 10 sn, shift+tekerlek 60 sn, ctrl+shift+tekerlek 300 sn,
alt+tekerlek yerel yakinlastirma, sag tik ve bosluk oynat/duraklat,
orta tik tam ekran <-> onceki, sol tik simdilik bos.

T176'nin devraldigi kusurlar `docs/olcumler/oynatici-boru.md` icindeki "T176 devri"
bolumunde yaziyor - dagitmadan once oku.

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

`.calisma/` gitignore'lu ve **3.5 GB** (T171 tek basina 3.0 GB video). Git'e girmez.
Kucuk metin kaniti ve ekran goruntuleri (98 dosya, 4.4 MB) gizli depoya kopyalandi:

    ~/.claude/teknesyum-ozel/vidshrink/calisma/

Buyuk medya (`.calisma/kaynak`, `kaynak-genis`, `t57`, T171 video klasorleri) **yalniz bu
makinede**. Masaustunde bir olcum tekrarlanacaksa kaynak videolar yeniden uretilmeli.

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
