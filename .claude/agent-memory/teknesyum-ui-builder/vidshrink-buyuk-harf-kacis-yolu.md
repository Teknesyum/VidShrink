---
name: vidshrink-buyuk-harf-kacis-yolu
description: Koşulardan (Run) kurulu TextBlock büyük harf geçidine uğramaz; kodlayıcı adı ve birim ölçüldüğü yazımla kalsın diye satırı Run ile kur
metadata:
  type: project
---

`LanguageCatalog.Title` **her sözcüğün** ilk harfini büyütüyor ve ekrandaki her metin
`WalkText`/`Localize` üzerinden oradan geçiyor. Prozada bu evin tarzı, ama veri
bozuluyor: `libx264` -> `Libx264`, `h264_nvenc` -> `H264_nvenc`, `ms` -> `Ms`.
`Names` sözlüğü bunları koruyabilirdi ama `LanguageCatalog.cs` çoğu sözleşmede `owns`
dışında.

**Why:** `WalkText` bir `TextBlock`'u yalnız `Inlines is not { Count: > 0 }` iken
yazıyor. Koşulardan kurulu bir blok (ipucu gövdeleri gibi) geçide hiç uğramıyor.

**How to apply:** Ölçülen değeri gösteren satırı `Run` ile kur; etiketi geçitten
geçir, değeri ham bırak:

```csharp
row.Inlines.Add(new Run(Localize(label) + ": "));
row.Inlines.Add(new Run(value));   // "0.53 cores", "16× realtime", "libx264"
```

`Inlines` null gelebilir (`PaintBullets` de bunu kontrol ediyor), düşüş yolu bırak.
Cümleleri geçitten geçirmeye devam et — yalnız kod, kimlik ve birim kaçar. Bu aynı
zamanda arayüz standardının "her sayı, kod, ID mono" kuralıyla örtüşüyor: satır
`MonoValue` temasında durur.

Uyarı: `CasingTests` `.cs` dosyalarında `ToUpper` çağrısı arıyor ve `.axaml` içinde beş
harf ve üzeri tümü büyük sözcük arıyor — kaçış yolu bu ölçümlerin ikisini de kırmaz.

İlgili: [[vidshrink-metin-geciti]], [[vidshrink-buyuk-harf-servis-adlari]]
