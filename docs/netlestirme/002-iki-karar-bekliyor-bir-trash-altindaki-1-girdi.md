[[netlestirme:002]]

# Netleştirme: Iki karar bekliyor. BIR: trash altindaki 136,6 MB git disi tek kopya kalinti sil

İşe başlamadan önce soruyu keskinleştir. Görüş verme, plan yazma, kod yazma.
Yalnız şunu döndür: soruda belirsiz kalan yerler, her biri için tek satırlık bir netleştirme sorusu, en fazla beş. Belirsizlik yoksa "net" yaz.

## Soru

Iki karar bekliyor. BIR: trash altindaki 136,6 MB git disi tek kopya kalinti silinsin mi, tamami mi, bir kismi mi, yoksa durmali mi? IKI: claude/tema-paleti dali main'e nasil girmeli — main'den rebase mi, PR mi, yoksa baska bir yol mu? Her iki soruya da tek bir net eylem onerisi ver, gerekcesini iki cumleyle sinirla.

## Elde olan olgular

# Olgular — VidShrink devir turu kapanisi, 9 Eylul 2026

## Karar 1: git disi 136,6 MB kalinti

`trash/VidShrink-devir-2026-09-09/` (1,9 MB) ve `trash/VidShrink-calisma-2026-09-09/` (134,7 MB)
USB'den depoya tasindi. Ikisi de `.gitignore` kapsaminda, git'e girmez.

Git'te birebir blob karsiligi dogrulanan 26 dosya silindi. Kalanlar tek kopya:

- t57/hareketli-1080p30-120s.mp4  81,7 MB — ham olcum kaynak videosu
- t57/hareketli-720p60-120s.mp4   51,8 MB — ham olcum kaynak videosu
- T176/ 0,8 MB (kanit txt'leri, girdi-20sn.mkv 424 KB, kalip-kanit-20sn.mkv 424 KB)
- test-ciktilari/ 0,2 MB (settings.json'lar, olcum.txt'ler)
- t58/t60/t61/t63 olcum.txt (toplam ~18 KB)
- devir klasoru: OKU.md, calisma-envanteri.txt, handoff-ui-denetim-2026-09-08.md,
  10 elenen ekran karesi (1,8 MB), adim.ps1, is.ps1, kirp.ps1 (git'tekinden farkli eski surum)

Ek olgular:
- Depoda ayrica `.calisma/` dizini var; ayni tur adlarini (t57 t58 t60 t61 t63 T176
  test-ciktilari) tasiyor ama dosya icerikleri farkli (bunlar laptop kosumunun kalintisi).
  Depodaki `.calisma/` toplami bu makinede ayrica ~6 GB.
- Rapora giren sayilarin hepsi `docs/olcumler/` altinda git'te.
- `git grep` ile dogrulandi: src, tools, tests altinda hicbir canli kod yolu bu
  klasorlere bakmiyor.
- Videolar yeniden uretilebilir (onceki devir notu ayni sinif icin "pahali ama yeniden
  uretilebilir" demis; uretim komutu belgelenmemis).
- Proje kurali: isi bitmis dosya silinmez, `trash/` altina tasinir; `trash/` bosaltmak
  kullanicinin karari. Bu dosyalar zaten `trash/` altinda.

## Karar 2: `claude/tema-paleti` dali

- Icerik: 40 yeni dil (42 dil klasoru, her biri 488 anahtar), 20 tema paleti,
  dil secici, sagdan sola destegi. Uc: `097b141`.
- origin/main'e gore: 57 commit ileri, 21 commit geri.
- Ortak ata: `44f45933`.
- Dal tarafinda degisen 259 dosya, main tarafinda 99 dosya, ikisinde de degisen 7 dosya.
- `git merge-tree --write-tree origin/main origin/claude/tema-paleti` sonucu 3 catisma:
  - `.claude/handoff.md` (icerik catismasi — devir notu, onemsiz, ustune yazilabilir)
  - `docs/plan.md` (dalda silinmis, main'de degismis)
  - `src/VidShrink.App/LanguageCatalog.cs` (gercek icerik catismasi)
  - `MainWindow.axaml`, `MainWindow.axaml.cs`, `CasingTests.cs`, `LanguageTests.cs`
    otomatik birlesiyor.
- Testler (dal uzerinde, laptopta): dil/yerellestirme suiti 138/138 yesil.
  Tam kosum 1935 basarili / 18 atlanan / 1 basarisiz. Tek kirmizi
  `OynaticiBoruTests_DecoderPipe.Oldurulemeyen_surec_icin_KillTree_basarisiz_bildirir`,
  bu dalla ilgisiz, dil isinden once de vardi.
- Proje kurali: `main`e yalniz T0 birlestirir; herkes kendi dalinda calisir.
- Su anki calisma dali: `codex/astra-motor-inceleme`. Calisma agacinda commit edilmemis
  degisiklikler var (.claude/handoff.md, relay sozlesmesi T176, docs/astra.md, docs/plan.md,
  docs/danisma/, docs/netlestirme/).
- Depoda ayrica birlestirilmemis 12 dal daha var (T177..T193).
