# Düzenleyici — V2 İzi (Resim İçinde Resim) ve Yeşil Perde

Later 110 (DZB-02 + DZB-29). Çizelgeye V2 izi gelir: her üst klip başlangıcını, boyunu ve yerini
taşır, teslim `overlay` ile bindirir. Yeşil perde aynı klipte tek seçenek: `chromakey`
(`RecorderArguments.GreenScreenKey`) bindirmeden önce. Canlı önizleme 109'un yolu: kısa ön-işleme
ve `edl://` parçası.

| Parça | Dosyalar | İş |
| --- | --- | --- |
| Model | yeni `Core/Editing/VideoLayer.cs`, `EditTimeline.cs`, `EditCommands.cs` | katman kaydı (yol, süre, kırpım, başlangıç, boy, yer, yeşil perde), ekle/taşı/güncelle/sil tek adım, `ToOutput` |
| Teslim | `EditExport.cs` | katman Tam kipi zorlar; Full ve Merge'de `-ss/-t -i`, `scale`, `chromakey`, `overlay=eof_action=pass`, metnin altında |
| Proje | `EditProject.cs`, `Otio.cs` | `layers` yaz/oku, `Restore` katmanla |
| Önizleme | `EdlPreview.cs`, yeni `Core/Editing/LayerPreview.cs` | klip parçasını bindirme aralığında bölen yama, yamanın ön-işleme argümanı |
| Arayüz | yeni `EditorTimeline.Katman.cs`, yeni `EditorView.Katman.cs`, `EditorTimeline.cs`, `EditorView.Gecis.cs`, `EditorView.Teslim.cs`, `EditorView.Proje.cs`, `EditorView.axaml`, `Locales/*/editor.json` | V2 şeridi, sürükle, menü (ekle, boy, yer, yeşil perde, sil), yama kuyruğu, 42 dil |
| Test | yeni `DuzenleyiciV2IzTests.cs`, `BiciminTests.cs` pinleri, `test-katalogu.md`, `docs/olcumler/v2-izi.md` | argüman, yama, gidiş-dönüş, geri al, teslim karesinde bölge ölçümü, mutasyon |
