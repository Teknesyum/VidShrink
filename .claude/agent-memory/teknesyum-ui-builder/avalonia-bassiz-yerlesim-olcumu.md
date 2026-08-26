---
name: avalonia-bassiz-yerlesim-olcumu
description: Pencere gösterilmeden yerleşim ölçerken zamanlayıcı/geçiş koşmaz, DesiredSize kenar boşluğunu içerir, seçili olmayan sekme ağaçta yoktur
metadata:
  type: project
---

`AppHost.Run` ile pencereyi gösterilmeden `Measure`/`Arrange`/`UpdateLayout` ile sürerken
dört tuzak var:

- **`DispatcherTimer` ve geçişler ateşlenmiyor.** `Fade(x, false)` opaklığı sıfırlıyor ama
  `IsVisible=false` kararını 240 ms sonra zamanlayıcı veriyor; o koşmadığı için gizli
  denetim yer yemeye devam ediyor (`DropZone` 215 px). Çözüm: gizlenmeyi bekleyenleri bir
  kümede tutup `SettleFades()` gibi bir iç kapıyla oturmuş hali uygulamak.
- **`DesiredSize` kenar boşluğunu içeriyor, `Bounds` içermiyor.** Kırpma ölçerken boşluğu
  düşmezsen yanı boşluklu her denetim (plan madde imi: want=17, got=9) kırpılmış görünür.
- **Seçili olmayan sekmenin içeriği görsel ağaçta yok.** Bütün sekmeleri görmek için
  `TabControl.SelectedIndex`'i sırayla değiştirip her adımda yeniden ölçmek gerekiyor.
- **`GetVisualDescendants` ilk `Measure`'dan önce boş.** Şablon uygulanmadığı için
  `TabControl`'ü bulmaya çalışan `Single()` "Sequence contains no elements" ile düşer.

Ayrıca `MainWindow.LoadAsync` yoklamayı (`FfprobeClient`) ve karmaşıklık ölçümünü
bekliyor; başsız ölçüm için yükleme yolunu ikiye ayır ve `MediaInfo`'yu doğrudan veren bir
`internal` kapı aç — böylece ölçüm ne ffmpeg'e ne diskteki bir dosyaya bağlı kalır.

İlgili: [[vidshrink-pencere-ici-olcum]], [[vidshrink-maximized-olcum]]
