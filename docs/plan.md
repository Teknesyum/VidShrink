# Düzenleyici — Ses Ekleri Ve İz Denetimleri (Later 139)

Sahibin kararı (21 Ekim 2026): ses ekleri ve iz denetimleri evet, boşluklu çizelge hayır.
Otomatik müzik kısma (DZB-46) daha önce `EditMusic.Duck` ile teslim edildi; bu turda yalnız
ses izi onu da kısar hale gelir (müzik, A3'ün de karıştığı yatağa göre kısılır).

| Parça | Dosyalar | İş |
| --- | --- | --- |
| Model | yeni `Core/Editing/EditAudioClip.cs`, yeni `Core/Editing/EditTrack.cs`, yeni `Core/Editing/EditTimeline.Audio.cs`, `EditTimeline.cs` (partial) | A3 ses izi parçaları (yol, kaynak aralığı, çizelge başı, dB), ekle/taşı/kırp/sil/düzey tek adım geri alınır; `DetachAudio` klibi susturup sesini A3'e indirir; iz durumu (sustur, gizle, kilitle, tek dinle) ve teslim kopyası `ForExport` |
| Teslim | `Core/Editing/EditExport.cs`, `MergeCompatibility.cs` | A3 parçaları `atrim` + `adelay` ile `[aout]`'a karışır, müzikten önce; A3 Tam kipi zorlar |
| Proje | `Core/Editing/EditProject.cs` | `audioClips` ve `tracks` alanları; eski proje alansız açılır |
| Seslendirme | yeni `Core/Editing/NarrationArguments.cs`, yeni `Ffmpeg/NarrationRecorder.cs` | kaydedicinin mikrofon kolu (`AudioCaptureArguments`) ile m4a; durdurma stdin `q`, stderr boşaltılır |
| Arayüz | `EditorTimeline.cs`, yeni `EditorTimeline.Ses.cs`, yeni `EditorView.Ses.cs`, `EditorView.axaml`, `EditorView.Teslim.cs`, `EditorView.Proje.cs`, `EditorView.Metin.cs`, `Locales/*/editor.json` | A3 şeridi, iz başlığında durum simgesi ve tıkla-değiştir, menüde "İzler", "Sesi ayır", "Seslendirme kaydet"; kilitli iz sürükleme ve kesmeyi reddeder; gizli T1 önizlemede de görünmez, susturulan V1 önizlemede susar |
| Test | yeni `DuzenleyiciSesIziTests.cs` | model, teslim argümanı, proje gidiş-dönüş, seslendirme argümanı, en az bir mutasyon |

Önizleme: müzik gibi A3 de önizlemede çalmaz (EDL tek ses yolu taşıyor); bu belgelenir.
V1 gizleme sunulmaz: tek video izinde gizlemek siyah teslim demek; V2 gelince (Later 110) gizleme o ize girer.
