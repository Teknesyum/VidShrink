---
name: olcum-dosyasi-buyur
description: VidShrink olcum testleri .calisma/test-ciktilari altina EKLEYEREK yaziyor; her dotnet test kosumu dosyayi buyutuyor, rapor once yazilirsa sayilar ve satir numaralari bayatliyor
metadata:
  type: project
---

Olcum testleri (`PanelHostTests.Record`, `SegmentEncoderTests` vb.)
`.calisma/test-ciktilari/<sozlesme>/olcum.txt` dosyasina `File.AppendAllText` ile
yaziyor. Dosya hicbir zaman sifirlanmiyor: filtreli her kosum, tam `dotnet test` her
kosumu ve denetcinin tekrar kosumu ayni dosyaya bir blok daha ekliyor.

**Why:** Sozlesmelerin degismez kurali "rapora SON kosumun sayisi girer, en iyisi
degil". Raporu yazip sonra bir tam `dotnet test` daha kosturursan, aktardigin satir
numaralari kayiyor ve alintiladigin blok artik son blok olmuyor — raporun kendisi
dogru olsa bile kaynak gostermesi yanlis oluyor. T50'de tam bu oldu: rapor 5. ve 7.
satiri gosteriyordu, iki tam kosum sonra son blok 15. ve 17. satira kaymisti.

**How to apply:** Sirayi tersine cevir — once son `dotnet build` + son tam
`dotnet test`, sonra `cat -n <olcum.txt>` ile dosyayi oku, **en son bloktan** kopyala,
sonra raporu yaz. Rapora satir numarasini ve "dosya birden cok kosum tasiyor, bu son
kosum" cumlesini birlikte koy. Raporu yazdiktan sonra test kosturmak zorunda kalirsan
sayilari yeniden kontrol et. Baska bir sozlesmenin klasorune yazma; kendine ayri klasor
ac ve onunkini silme.

Ilgili: [[vidshrink-dogrulama]], [[rapor-ozeti-veriden-kayiyor]]
