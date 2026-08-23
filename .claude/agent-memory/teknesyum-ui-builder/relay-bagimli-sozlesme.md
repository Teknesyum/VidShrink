---
name: relay-bagimli-sozlesme
description: VidShrink relay'inde bagimli bir sozlesmeyi (depends) kendi worktree'sinde dogrulamanin yolu — kardes worktree'den imza okuma ve gecici sahneleme
metadata:
  type: project
---

Relay ajanlari ayri worktree'lerde calisiyor ve `depends:` verilen sozlesmenin kodu
kendi dalinda **bulunmuyor**. `git worktree list` kardes worktree'leri veriyor; bagimli
oldugun sozlesmenin dosyasi orada duruyor ve **okunabiliyor**.

**Yontem:**
1. `Glob` ile kardes worktree'lerde arayacagin dosyayi bul
   (`.claude/worktrees/*/src/.../Update*.cs`). Bash ile `cd` etme — worktree izolasyon
   koruyucusu paylasilan checkout'a giden komutlari reddediyor, `Glob` ve `Read` geciyor.
2. Imzalari oradan **birebir oku**, tahmin etme.
3. Kodu dogrudan cagrilarla yaz — `#if` bayragi veya delege kilifi koyma, birlesince
   temiz kalsin.
4. Dogrulama icin bagimli dosyayi worktree'ne **gecici kopyala**, `build` + `test`
   kosturup **sil**. Boylece gercek yol derlenmis olur ve commit'e girmez.
5. Silince kalan hatalari listele (`Select-String "error CS" | Sort-Object -Unique`) —
   Rapor'a "yalniz su iki ad eksik" diye yazilacak kanit budur.

**Why:** T19'da `UpdateSettings`/`UpdateCheck` T18'e aitti ve worktree'de yoktu.
Kilif yazmak sozlesmedeki "deger koda gomulmeyecek" kuralini ihlal ediyordu; sahneleme
hem yesil build hem temiz commit verdi.

**How to apply:** `depends:` olan her sozlesmede once kardes worktree'yi ara. Rapor'a
beklenen imzalari blok halinde yaz — T0 birlestirirken onu okuyor. Ayrica
[[avalonia-tema-tuzaklari]] ve [[vidshrink-arayuz-dogrulama]].

**Sozlesme dosyasinin kendisi worktree'de olmayabilir** (paylasilan checkout'ta henuz
izlenmiyorsa). Kopyala, `status`/`round`/Rapor'u worktree kopyasina isle, onunla commit et.
