---
name: reference-taramalar
description: VidShrink depo tarama raporlarının yeri ve doğrulama yöntemi (docs/taramalar, gh api + yerel ffmpeg ölçümü)
metadata:
  type: reference
---

Depo tarama raporları `docs/taramalar/<kisa-ad>.md` altında. Türkçe, iç belge, git'e giden README'lerden ayrı.

Birincil kaynak sırası: `gh api repos/<owner>/<repo>` ve `/releases/latest` → depo içindeki `Docs/`/`CHANGELOG` → **yerel ffmpeg ile ölçüm**. Makinede ffmpeg PATH'te (`ffmpeg -h encoder=<ad>` ve `testsrc2` üzerinde küçük deneme encode çalışıyor), yani kodlayıcı bayrak iddiaları doğrudan sınanabilir — blog ya da dokümanla yetinme.

İlgili: [[project-ffmpeg-sessiz-hata]]
