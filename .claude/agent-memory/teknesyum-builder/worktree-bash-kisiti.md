---
name: worktree-bash-kisiti
description: Worktree'ye izole ajanda Bash aracı bileşik komutları reddediyor; betiği worktree içine Write ile yaz, tek satır komutla koştur
metadata:
  type: feedback
---

Worktree'ye izole çalışan bir ajanda Bash aracı, worktree dışına çıkıp çıkmadığını
doğrulayamadığı komutları koşmadan reddeder. Reddedilenler: heredoc ile worktree
dışına dosya yazma, `{ komut; komut; } >> dosya` blok yönlendirmesi, birden çok
`cd`/yönlendirme içeren zincirler.

**Why:** Araç, izole ajanın git işlemlerinin kendi worktree'sinde kalmasını
garanti etmek istiyor; ayrıştıramadığı komutu güvenli sayamıyor.

**How to apply:** Ölçüm betiğini `Write` ile doğrudan worktree içine
(`.calisma/<sozlesme>/probe.sh`) yaz, sonra `bash <mutlak yol>` tek satırıyla
koştur. Çıktıyı biriktireceksen her `>>` yönlendirmesini ayrı Bash çağrısı yap;
başlık satırını `echo ... >> olcum.txt` ile tek başına ekle. Scratchpad klasörüne
yazmaya çalışma — o worktree dışında, reddedilir. Bkz. [[vidshrink-build-and-probe]].
