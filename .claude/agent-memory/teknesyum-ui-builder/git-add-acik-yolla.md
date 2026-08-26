---
name: git-add-acik-yolla
description: Paralel ajanlar aynı git indeksini paylaşıyor; git add -A başka sözleşmenin dosyalarını süpürür
metadata:
  type: feedback
---

`git add -A` ve `git commit -a` kullanma. Her zaman açık yolla ekle ve yalnız sözleşmenin
`owns` kümesindeki dosyaları (artı kendi sözleşme dosyanı).

**Why:** T44 turu 1'de iki commit'im (`f6f1f62`, `cf157cd`) aynı anda çalışan başka bir
sözleşmenin üç dosyasını süpürdü. Bu depoda ajanlar worktree açmadan `main` üstünde
çalışabiliyor, yani git indeksi ortak.
Kayıt: `~/.claude/teknesyum/openlogs/HATA-paralel-ajanlar-ayni-git-indeksini-paylasti.md`

**How to apply:** commit'ten önce `git status --short` ile bak; sana ait olmayan değişiklik
varsa dokunma, `git add <yol> <yol>` ile yalnız kendi dosyalarını ekle. Aynı sebeple test
sayısı da tur içinde kayabilir — taban sayıyı kendi eklediğinle karşılaştırmadan önce
`git log`'a bak.
