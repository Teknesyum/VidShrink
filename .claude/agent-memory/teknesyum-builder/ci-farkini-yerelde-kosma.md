---
name: ci-farkini-yerelde-kosma
description: VidShrink'te CI'ın gördüğü hali yerelde üretmek — ffmpeg'i PATH'ten çıkar; ayrıca pencere boyutu yerelde ve CI'da farklı
metadata:
  type: project
---

CI'da `dotnet test` yerelden farklı sonuç veriyorsa iki sebep var ve ikisi de yerelde
üretilebilir.

**ffmpeg yokluğu.** `ToolLocator.IsAvailable` ffmpeg/ffprobe'u önce
`AppContext.BaseDirectory/tools/ffmpeg` altında, sonra PATH'te arıyor. Bu makinede
ikisi de yalnız PATH'te (WinGet Links). PATH'ten o dizini çıkarıp koşunca atlanan
sayısı CI'ınkiyle birebir aynı çıkıyor:

    PATH=$(printf '%s' "$PATH" | tr ':' '\n' | grep -v -i 'WinGet' | paste -sd: -)
    export PATH
    dotnet test -c Release

**Pencere boyutu.** Başsız koşumda `window.Arrange` verilen boyutu taşımıyor
(`Window.ArrangeSetBounds` `ClientSize` döndürüyor), yani pencere ölçüsü konağın
platform penceresinden geliyor ve CI'da bu makinedekinden çok küçük. Bir yerleşim
ölçüsü kendi boyutunu kurmuyorsa yerelde yeşil CI'da kırmızı olabilir. Çözüm
`WindowLayoutTests.LayOutAt` kalıbı: boyutu pencereye değil pencerenin tek kök görsel
çocuğuna `Measure`/`Arrange` ile ver. O kök aynı zamanda `VisualLayerManager`, yani
kök katmana terfi eden panelin ölçüldüğü alan da oradan geliyor.

İlgili: [[avalonia-kirpilma-olcutu]], [[vidshrink-pencere-testleri-kaynak-metinden]]
