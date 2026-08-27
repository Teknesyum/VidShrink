---
name: powershell-utf8-tuzagi
description: Bu projedeki Türkçe metin dosyalarını Windows PowerShell 5.1 ile okuyup yazma dosyayı bozar; sözleşme, rapor ve LOG için Bash cat/sed kullan
metadata:
  type: feedback
---

Bu projedeki metin dosyaları (sözleşmeler, `LOG.md`, `.ps1`, `.sh`) BOM'suz UTF-8 ve
içleri Türkçe. Windows PowerShell 5.1 ile `Get-Content` + `Set-Content`/`Add-Content`
üzerinden bunlara dokunma.

**Why:** PS 5.1 BOM'suz bir dosyayı sistem ANSI kod sayfasında okur. `Get-Content -Raw`
ile okuyup `Set-Content -Encoding utf8` ile geri yazmak dosyayı iki kez bozar: metin
mojibake olur (`betiği` → `betiÄŸi`) ve başına BOM eklenir. T45'te sözleşmenin tamamı bu
şekilde bozuldu; `git checkout HEAD -- <dosya>` ile geri alınıp Bash'ten yeniden yazıldı.
Aynı tuzak `Out-File`, `>` ve `>>` için de geçerli.

**How to apply:** Rapor eklemek, frontmatter alanı değiştirmek, `LOG.md`'ye satır atmak —
hepsi Bash tarafından: `cat parca.md >> hedef.md`, `sed -i 's/^status: active$/status:
submitted/' hedef.md`. Uzun metni önce scratchpad'e `Write` ile yaz, sonra `cat` ile
ekle; heredoc'lar bu ortamda kırılabiliyor. PowerShell yalnız gerçekten PowerShell
gereken iş için: `Parser::ParseFile` ile `.ps1` sözdizimi denetimi gibi. Yazdıktan sonra
`grep -c 'Ã\|Ä\|Å' <dosya>` ile doğrula — 0 dönmeli.

Çok satırlı commit mesajı için `git commit -m @'...'@` **yazma**: PowerShell here-string
sözdizimi Bash'te düz metindir, `@` işaretleri mesajın ilk ve son satırı olarak commit'e
girer ve konu satırı `@` olur. Mesajı scratchpad'e `Write` ile yazıp
`git commit -F <dosya>` kullan.
