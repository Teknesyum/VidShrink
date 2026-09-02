
## Muhur kapisi denetciyi rolunden degil ajan tipinden taniyor (2 Eylul 2026)

`hooks/watch.js:30` `roleOf()` rolu **yalnizca** `subagent_type`'tan okuyor:

```js
const raw = String(j.agent_type || t.subagent_type || '');
const clean = raw.replace(/^teknesyum(-core)?:/, '');
if (clean) return clean;
const m = /roles[\/]([a-z-]+)\.md/i.exec(prompt);
```

`hooks/seal.js:119` de kaydin `role` alanina bakip reddediyor:
`auditorRunId points at a non-auditor agent record: worker`.

**Somut olay:** T140 denetcisi `teknesyum-core:worker` tipiyle acildi, prompt'unda
`agents/auditor.md` yolunu tasiyordu, rol dosyasini okudu, hicbir dosyaya yazmadi
(`files: []`), 32 arac cagrisi boyunca tam bir denetim yapti ve GECTI verdi. Muhur kapisi
onu reddetti — **cunku etiket yanlisti, is degil.**

Uc sorun ic ice:

1. **Yedek desen `roles/` ariyor, dizin adi `agents/`.** Regex `/roles[\/]([a-z-]+)\.md/`
   hicbir zaman eslesmiyor; Core 0.7.3'te de Base 2.67.0'da da dizinin adi `agents/`.
   Yedek kol **olu**. `if (clean) return clean;` erken dondugu icin zaten hic denenmiyor.
2. **`teknesyum-core` eklentisinde `auditor` alt ajan tipi yok** — 0.7.3'un `agents/`
   klasorunde yalniz `worker.md` var. Yani Core'un kendi tipiyle acilan hicbir denetci
   muhur kapisindan gecemez. Gecen denetciler harness'in yerlesik `auditor` tipinden
   geliyor; yani kapi Core'un disindaki bir tanima bagimli.
3. **Kacinilmaz sonuc: kaydi elle duzeltme baskisi.** Ajan gercekten denetci gibi
   davrandiginda ve kapi "hayir" dediginde, en ucuz cikis `live/<id>.json` icindeki
   `role` alanini `"auditor"` yapmak. Bu tam olarak kapinin engellemesi gereken sey:
   denetim zincirinin sahtelenmesi. **Kapi, kendi ihlalini en ucuz cozum haline
   getiriyor.**

### Onerilen

- `agents/auditor.md` ve `agents/advisor.md` Core'a eklensin; bugun Base'e dusuluyor.
- Yedek desen `agents[\/]` de eslesin (ya da `roles` yerine dogrudan `agents`).
- `roleOf` erken donmesin: `subagent_type` genel bir tipse (`worker`, `general-purpose`)
  prompt'taki rol dosyasi yolu **ustun gelsin.**
- `seal.js` reddederken ne yapilmasi gerektigini soylesin. Bugun yalniz "non-auditor
  agent record: worker" diyor; kullanici ya kaydi kurcalar ya denetimi bastan kosturur.
  Dogru cevap ikincisi ve kapi bunu yazmali.

Maliyet: T140 icin tam bir denetim (122k token) yapildi, kabul edilmedi, ikincisi
kosturuldu.
