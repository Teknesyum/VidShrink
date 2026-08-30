# VidShrink

Hedef boyuta sıkıştıran video aracı. .NET 8 + Avalonia + ffmpeg.

- `src/VidShrink.Core` — plan hesabı, argüman üretimi. Motorun kararları burada.
- `src/VidShrink.Ffmpeg` — süreç çağrıları, yoklama, oynatma borusu.
- `src/VidShrink.App` — Avalonia arayüzü. Renk ve ölçü yalnız `Themes/Theme.axaml` belirteçlerinden.
- `tests/VidShrink.Tests` — tek test projesi. `dotnet test` tamamı yeşil olmadan teslim yok.
- `tools/VidShrink.Bench` — ölçüm aracı. Rapora giren her sayı buradan çıkar.

## Geçici dosyalar

Sonda programı, ölçüm günlüğü, ekran görüntüsü, deneme betiği — hepsi **`.calisma/`**
altına. Sistemin `%TEMP%`'ine ya da proje köküne dağıtma; `.gitignore`'da, git'e sızmaz.

**İş bitince kendi bıraktığını sil.** Rapora giren sayı `docs/`e, ölçümü üreten
düzenek `tools/`a taşınır; geri kalan `.calisma/` ile birlikte gider.

Ajan worktree'si de birikinti: dalı `main`e birleştikten sonra
`git worktree remove` ile kaldır.

## Dallar

**`main`e yalnız T0 birleştirir.** Kim olursan ol — alt ajan, Sole, uzak makinedeki bir
ajan — kendi dalında çalış ve oraya it: `git switch -c <sahip>/<is>`. `main`in çalışma
ağacında doğrudan yazma; iki taraf aynı dosyayı yazınca yarım iş commit'e karışıyor.

Dal adı sahibi ve işi söyler: `sole/sagtik-win11`, `serkan/macos-ilk-kosum`,
`worktree-agent-<id>` (alt ajanlar bunu kendi kurar).
