---
name: reference-upstream-repos
description: Doğru depo adresleri ve GitHub metadata tuzakları — shotcut, kdenlive, openshot taramalarında karşılaşıldı
metadata:
  type: reference
---

Video düzenleyici depolarını `gh api` ile sorgularken bilinmesi gerekenler:

- `shotcut/shotcut` yok, 404 döner. Gerçek depo `mltframework/shotcut`.
- `KDE/kdenlive` yalnızca ayna: issue takibi kapalı olduğu için `open_issues_count` her
  zaman 0 görünür ve `/releases/latest` 404 verir. Sürüm için `/tags`, issue için
  invent.kde.org'a bakılır.
- `OpenShot/openshot-qt` lisans alanı `NOASSERTION` döner; depodaki `COPYING` ve kaynak
  başlıkları GPL'dir. GitHub'ın lisans tespitine güvenme, dosyaya bak.

Tarama çıktıları `docs/taramalar/` altında toplanıyor.
