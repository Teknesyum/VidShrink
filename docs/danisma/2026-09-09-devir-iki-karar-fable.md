# Fable görüşü — 9 Eylül 2026, iki karar (maliyet: 51993 belirteç, 20 sn)

## Görüş

**Karar 1 — `trash/` iki klasör: sil, iki istisnayla.**

Eylem (T0): `trash/VidShrink-devir-2026-09-09/` tamamını ve `trash/VidShrink-calisma-2026-09-09/` içinden t57 iki ham videoyu (133,5 MB) sil; kalan 1,2 MB'ı (T176 kanıtları, test-ciktilari, dört olcum.txt) `docs/olcumler/` altında git'e taşı, ardından `trash/` boşalt.

Gerekçe: Videoların üretim komutu depoda yok, yani yeniden üretilemez ama rapora giren sayılar zaten git'te — ham veri artık yalnızca yer kaplıyor ve "elenen" karelerin de karar değeri bitmiş. T176 kanıtları ve olcum.txt'ler ise git'te eşdeğersiz tek kopya; 1 MB için kaybetme riskini almaya gerek yok, `docs/olcumler/` zaten bu iş için var.

**Karar 2 — `claude/tema-paleti`: T0 main'i dala rebase etmesin, dalı main üstüne birleştirsin; üç çatışmayı T0 elle çözsün.**

Eylem (T0): `git switch claude/tema-paleti; git merge origin/main` — `.claude/handoff.md` main'i al, `docs/plan.md` main sürümünü geri getir (dal silmişti), `LanguageCatalog.cs` elle birleştir; dil suiti 138/138 yeşilse dalı main'e birleştir ve dalla ilgisiz kırmızıyı ayrı bir T'ye yaz.

Gerekçe: 57 ileri / 259 dosya değişmiş bir dalı yeniden yazmak çatışmayı çoğaltır; ters yönde (main→dal) tek birleştirme çatışmayı üç noktaya kilitler ve doğrulama dalda kalır. Dalla ilgisiz kırmızı bu birleştirmenin kapısı değil; kapıya koyarsan 12 bağımsız dal (T177..T193) da aynı kırmızının arkasında bekler.
