---
name: project-ffmpeg-sessiz-hata
description: ffmpeg -svtav1-params tanınmayan anahtarda hata basar ama çıkış kodu 0 kalır; yeteneklik sondası stderr okumalı
metadata:
  type: project
---

ffmpeg, `-svtav1-params` içindeki tanınmayan anahtarda `Error parsing option <ad>: <deger>.` satırını stderr'e basar ama **çıkış kodu 0** döner. Uydurma anahtar için de aynı. 2026-08-22'de yerelde ölçüldü (ffmpeg 9.0-full, SVT-AV1 lib v4.2.0).

**Why:** VidShrink `CodecModel.cs` ve `FfmpegArguments.cs` sayılarını "uydurma, ffmpeg'e sor" kuralıyla besliyor. Sonda çıkış koduna bakarsa desteklenmeyen bayrağı desteklenmiş sanır ve tablo sessizce yanlışlanır.

**How to apply:** Kodlayıcı yeteneği sınanırken exit code yetmez; stderr'de `Error parsing option` aranmalı. Aynı şüphe diğer `-<codec>-params` sözlüklerinde de geçerli — libx265'te tanınmayan anahtar test edilmedi, varsayma.

İlgili: [[reference-taramalar]]
