# 7a — Fare Etkileşimi Ölçümü

Ölçüm kanalı oynatıcının `Trace` dizisi ve sahte motorsuz görünüm durumu; her satır için
önce/sonra okunur. Ham çıktı: `.calisma/dalga7a/f1..f6-*.txt`.

## Eşikler

| Eşik | Değer | Kaynağı |
| --- | --- | --- |
| Sürükleme eşiği (`ClickArbiter.DragThresholdDip`) | 4 dip | Windows `SM_CXDRAG` / `SM_CYDRAG` varsayılanı |
| Çift tık penceresi (`ClickArbiter.DoubleWindowMs`) | 500 ms | Windows `GetDoubleClickTime` varsayılanı |
| Merkez mıknatısı (`SurfacePan.SnapDip`) | 12 dip | `Themes/Playback.axaml` → `PlaybackBadgeMargin` |

## Tek tık / çift tık ayrımı

```
tek tik   : oynatma False -> basili False -> birakis False -> 499 ms False -> 500 ms True
cift tik  : tam ekran False -> True, oynatma True -> True
cift tikin urettigi iz: fullscreen -> True
```

Tek tık işini basışta değil, çift tık penceresi dolunca yapıyor; çift tıkın ilk yarısı
duraklatmayı hiç tetiklemiyor (üretilen tek iz satırı `fullscreen -> True`).

## Tıklama / sürükleme ayrımı

```
esik alti (3 dip): kip [] birakis Click bekleyen tik True oynatma True
esik ustu (4 dip): kip [window] birakis Drag bekleyen tik False oynatma True -> True
```

3 dip oynayan basış tıklamadır ve duraklat/başlat üretir; 4 dip oynayan basış sürüklemedir
ve bırakışta hiçbir tık bırakmaz.

## Taşıma kipi

```
normal pencere : tam ekran False -> surukleme kipi [window]
tam ekran      : tam ekran True  -> surukleme kipi [pan]
miknatis esigi : 12 dip (PlaybackBadgeMargin)
```

## Merkez mıknatısı ve olumsuz denetim

```
sinir: 200 x 200 dip, miknatis 12 dip
esik icinde surukleme (10 dip): X 0 Y 0 ortada True
esik disinda surukleme (24 dip): X 24 Y 0 ortada False     <- olumsuz denetim
merkeze donus: X 0 Y 0 ortada True
sinir disina surukleme: X 200 Y 200 (sinir 200,200)
```

## Menü konumu

```
sag tik      : yaslanma [pointer]
menu dugmesi : yaslanma [button]
ilk satir    : Settings
```

## Yerleşim pimleri (yeniden ölçüldü)

| Pim | Eski | Yeni |
| --- | --- | --- |
| `BaslikKapsamiTests` gezilen kalem | 27133 (43 × 631) | 27219 (43 × 633) |
| Kol değiştiren anahtar toplamı | 985 | 985 |
| `en` / `tr` kolu | 104 / 43 | 104 / 43 |

İki yeni anahtar: `main.player.input.left`, `main.player.menu.settings` — 42 dil klasörünün
tamamına eklendi, ikisi de `Keymap.cs` içinde okunuyor, ölü anahtar bırakmıyor.
