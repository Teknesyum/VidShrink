# Düzenleyici — Otomatik Sessizlik ve Siyah Kare Kesme

Tek düğme kaynağı tarar (`silencedetect` ve/veya `blackdetect`), bulunan aralıklar çizelgede
kesilecek diye boyanır; eşik (dB), en kısa süre (sn), kenar payı (sn) değişince önizleme
yeniden taranmadan tazelenir (eşik ve tür değişimi yeniden tarama ister). "Aralıkları kes"
tek geri alma adımıyla klipleri böler ve o kaynak aralıklarını siler.

| Parça | Dosyalar | İş |
| --- | --- | --- |
| Çekirdek | yeni `Core/Editing/SilenceCut.cs` | argüman (`-threads 2`, `-progress pipe:1`, yalnız gereken akış), saf ayrıştırıcı (`silence_*`, `black_*`, `out_time_us`), saf plan (birleştir → en kısa → kenar payı, dosya kenarında pay yok), BelowNormal + iptalli süreç; aralıklar `IdleSpan` |
| Model | `Core/Editing/EditTimeline.cs`, `EditClip.cs`, `EditCommands.cs` | `RemoveSource` tek adım (hız/ters/solma korunur, hepsini silmez), `SourceToTimeline` önizleme eşlemesi, `InsertCommand` |
| Arayüz | `EditorView.axaml`, yeni `EditorView.Sessizlik.cs`, `EditorView.axaml.cs`, `EditorTimeline.cs`, `EditorPlayhead.cs`, `Locales/*/editor.json` | panel, ilerleme + iptal, çizelgede `NeonPinkFill` boyası, 42 dil |
| Test | yeni `DuzenleyiciSessizlikTests.cs`, `tests/.../AGENTS.md`, `docs/olcumler/duzenleyici-sessizlik-kesme.md` | pimli stderr, her kolun olumsuz kontrolü, canlı ≤6 sn lavfi ±0,1 sn, ≥3 mutasyon |
