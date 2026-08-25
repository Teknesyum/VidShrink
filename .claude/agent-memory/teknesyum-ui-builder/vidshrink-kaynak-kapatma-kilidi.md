---
name: vidshrink-kaynak-kapatma-kilidi
description: PipeComparisonFrameSource.Dispose arayüz kuyruğunda bekletilirse program donar; kapatma Task.Run ile havuza alınır
metadata:
  type: project
---

`PipeComparisonFrameSource.Dispose` içinde `StopProcessAsync().GetAwaiter().GetResult()`
var ve `WaitForExitAsync` sürdürmesi arayüz kuyruğuna dönmek istiyor. Arayüz kuyruğundan
doğrudan `Dispose` çağırmak deadlock: `bekci ... ui TIKALI` böyle bulundu.

**Why:** T43 doğrulamasında program kapanışta ve akış yeniden kurulurken sonsuza kadar dondu.

**How to apply:** Kaynağı kapatan taraf (`PanelHost.Teardown`) `Task.Run(source.Dispose)`
döndürür; arayüz tarafı gerekiyorsa bu Task'i sınırla bekler (`Wait(3sn)`), asla süresiz
beklemez. Ayrıca yarışan iki kurulum kaybedenin ffmpeg'ini öksüz bırakır — kurulumlar
`_restart` zinciriyle sıraya dizildi, bkz. [[vidshrink-panel-terfi-deseni]].
