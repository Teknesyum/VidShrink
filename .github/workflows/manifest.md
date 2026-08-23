# Manifest biçimi ve yayın varlıkları

Bu dosya, `release.yml`'in ürettiği varlıkların adlarını ve `manifest.json`'un biçimini
tanımlar. Güncelleyici tarafı (indiren istemci) bu dosyayı okur; iş akışı dosyasını
okumak zorunda değildir. Burada yazmayan bir alan yoktur — tahmin gerekmez.

## Varlık adları

Bir GitHub Release'in varlık adları düz bir isim uzayındadır, aynı ad iki kez
kullanılamaz. Bu yüzden her varlık adı hedef `rid` ile biter. Dört `rid` vardır:
`win-x64`, `osx-arm64`, `osx-x64`, `linux-x64`.

| Varlık | Ad | İçerik |
|---|---|---|
| Arşiv | `vidshrink-<rid>.zip` | Yayın klasörünün tamamı, `manifest.json` dahil |
| Manifest | `manifest-<rid>.json` | Arşivin kökündeki `manifest.json` ile **birebir aynı baytlar** |
| Sağlama | `checksums-<rid>.txt` | Yukarıdaki iki varlığın SHA-256'sı |

Manifest hem arşivin içinde hem de ayrı bir varlık olarak durur. Güncelleyici, hangi
dosyaların değiştiğine karar vermek için 95 MB'lık arşivi indirmeden yalnız
`manifest-<rid>.json` dosyasını çeker.

İndirme adresi kalıbı (yayın taslak değilken):

```
https://github.com/Teknesyum/VidShrink/releases/download/<tag>/manifest-<rid>.json
https://github.com/Teknesyum/VidShrink/releases/download/<tag>/vidshrink-<rid>.zip
https://github.com/Teknesyum/VidShrink/releases/download/<tag>/checksums-<rid>.txt
```

`<tag>` her zaman `v` ile başlar ve `v` düşünce sürüm numarasını verir: `v1.2.0` →
`1.2.0`. Aynı sürüm `Directory.Build.props` içindeki `<Version>` ile ve `CHANGELOG.md`
içindeki `## [1.2.0]` başlığıyla aynıdır; `release.yml` üçü uyuşmazsa yayını yapmadan
durur. En güncel sürümü öğrenmek için GitHub'ın
`/repos/Teknesyum/VidShrink/releases/latest` uç noktası kullanılır — taslak yayınlar
oraya düşmez.

`checksums-<rid>.txt`, `sha256sum` çıktısı biçimindedir: küçük harf onaltılık özet, iki
boşluk, varlık adı. Örnek:

```
9f2c...  vidshrink-win-x64.zip
4ab1...  manifest-win-x64.json
```

## manifest.json

UTF-8, BOM yok, iki boşluk girintili, sonunda tek satır sonu var.

```json
{
  "version": "1.2.0",
  "commit": "abc1234",
  "built": "2026-08-23T18:00:00Z",
  "rid": "win-x64",
  "files": [
    { "path": "VidShrink.App.dll", "sha256": "e3b0c442...", "size": 123456 }
  ]
}
```

| Alan | Tür | Anlamı |
|---|---|---|
| `version` | metin | Sürüm numarası, `v` öneki olmadan |
| `commit` | metin | Yapının alındığı commit'in kısa özeti, 7 karakter. Yerel yapıda boş dizedir |
| `built` | metin | Yapı zamanı, UTC, `yyyy-MM-ddTHH:mm:ssZ` |
| `rid` | metin | `win-x64`, `osx-arm64`, `osx-x64`, `linux-x64` |
| `files` | dizi | Yayın klasöründeki her dosya için bir nesne |

`files` içindeki her nesnenin üç alanı vardır, hepsi zorunludur:

| Alan | Tür | Anlamı |
|---|---|---|
| `path` | metin | Yayın klasörüne göreli yol |
| `sha256` | metin | Dosyanın SHA-256'sı, küçük harf onaltılık, 64 karakter |
| `size` | tamsayı | Dosyanın bayt cinsinden boyutu |

Kurallar:

- `path` ayracı **her zaman `/`**, üretildiği ve okunduğu işletim sistemi ne olursa olsun.
  Windows'ta üretilen manifestte de `/` vardır. Karşılaştırma yapan taraf ayraç
  dönüştürmesi yapmamalı, yalnız kendi dosya sistemine yazarken çevirmeli.
- `path` başında `./` ya da `/` yoktur ve `..` içermez.
- `files`, `path` alanına göre **sıralı**dır. Sıralama ordinal'dır (bayt değerine göre,
  kültüre bağlı değil). Böylece iki yapının manifesti satır satır karşılaştırılabilir.
- Karşılaştırma büyük/küçük harfe duyarlıdır; `path` alanları olduğu gibi eşleşmelidir.

## Manifestte olmayanlar

İki şey bilerek dışarıda bırakılır:

- `tools/ffmpeg/**` — `ffmpeg.exe` ve `ffprobe.exe` 424 MB'tır, GPLv3'tür ve yayın
  paketine hiç girmez. Kullanıcının makinesine kurulum sırasında kendi lisansıyla iner ve
  orada kalır. Güncelleyici bu yolları **eksik dosya saymamalı, silmemeli**.
- `manifest.json` — kendi kendini özetleyemez. Yayın klasörünün köküne yazılır ama
  listelenmez. Güncelleyici bu dosyayı da eksik saymamalıdır.

Bunların dışında yayın klasöründeki her dosya manifestte listelenir.

## Güncelleyicinin izleyeceği yol

1. `manifest-<rid>.json` indirilir, kurulu klasördeki `manifest.json` ile karşılaştırılır.
2. `path` eşleşen ve `sha256` aynı olan dosyalar atlanır.
3. `sha256` farklı olan ve yenide olup eskide olmayan dosyalar indirilir.
4. Eskide olup yenide olmayan dosyalar silinir — yukarıdaki iki istisna hariç.

Ölçüm: 1.0.0'dan 1.0.1'e geçişte 220 dosyanın 8'i değişir, 1,68 MB eder. Geri kalan
212 dosya (93 MB .NET ve Avalonia çalışma zamanı) .NET ya da Avalonia yükseltilmedikçe
aynı kalır. Yapı deterministiktir: aynı kaynağın iki ayrı yapısı bayt bayt aynı
manifesti verir, yani sürüm değişmediyse indirilecek dosya sayısı sıfırdır.

Tek tek dosya indirmenin şu an bir yolu yoktur: yayında dosya başına varlık yoktur, yalnız
arşiv vardır. Manifest, arşivi indirmeye değip değmeyeceğini ve indikten sonra hangi
dosyaların üzerine yazılacağını söyler; değişmeyen 212 dosya diske yeniden yazılmaz.
