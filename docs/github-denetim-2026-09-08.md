# GitHub Denetimi — 2026-09-08

Merkezi GitHub denetimi bu depoda şunları değiştirdi:

- `KURULUM-LAPTOP.md` ve `DEVIR.md` kökten `trash/`a taşındı; canlı kod yolu referans vermiyordu. `paylasim-hedefleri.json` yerinde kaldı: `src/VidShrink.Core/Share/ShareTargets.cs` ve testler kök dizinden okuyor.
- `trash/` ve `.claude/agent-memory/` `.gitignore`a eklendi, izlemeden çıkarıldı; dosyalar yerel klonlarda duruyor.
- `kanit/*` tag'leri iç kanıt işaretidir; uzağa itilmez. `git push --tags` yerine yalnız `v*` tag'i itin (`.github/workflows/release.yml` de yalnız `v*` dinler). Var olan iki `kanit/T115-*` tag'inin silinmesi onay bekliyor.
- README.md ile README.tr.md başlık başlık karşılaştırıldı; eşit, kurulum komutları aynı.
- GitHub wiki kapatıldı. Koda dokunulmadı, test koşulmadı.

## Kalan (kullanıcı onayı gerekir)

- Uzak dallar: `git push --delete origin kanit/T115-f2f05f5f kanit/T115-0a56868f`.
- `.claude/relay/` public'te; kapsam dışı bırakıldı.
- Git'ten çıkan trash/ ve .claude/agent-memory/ kopyası: Teknesyum-Private `vidshrink/git-disi-2026-09-08/`.
