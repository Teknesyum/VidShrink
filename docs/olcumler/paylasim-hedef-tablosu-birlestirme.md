# Paylaşım Hedef Tablosu Birleştirildi (Defter 33)

`paylasim-hedefleri.json` iki ayrı tür tarafından okunuyordu: şeridi kuran
`VidShrink.App.ShareTargetTable` ve yüklemeyi yapan `VidShrink.Core.Share.ShareTargetTable`.
Şema T35'te sabitlenmişti, arama sırası T36'da eşitlenmişti — ama iki ayrıştırıcı duruyordu.

App kopyası yalnız tek bir şey için vardı: Core'un `Load`'u dosya yoksa `FileNotFoundException`
atıyor, arayüz ise dosya olmadan da açılmak zorunda. Karşılığı Core'a taşındı.

## Değişen yüzey

| Ne | Nereden | Nereye |
|---|---|---|
| `ShareTarget`, `ShareTargetTable` | `MainWindow.axaml.cs` (125 satır) | silindi |
| `Fallback`, `UguuMaxBytes`, `StorageToMaxBytes` | App | `Core.Share.ShareTargetTable` |
| `LoadOrFallback(Func<string?>?)` | — | `Core.Share.ShareTargetTable` (yeni) |
| Uç nokta şekli kuralı | `SelectedShareEndpoint` | `ShareProviderFactory.CanCreate` |

`Load(string?)` dokunulmadı: yükleme yolunda dosya yoksa iş durmalı, sessizce uç noktasız
bir tabloya düşmemeli.

## Mutasyonlar

Filtre: `PaylasimAramaSirasiTests|SettingsTabTests|ShareProviderTests|PaylasimHataDiliTests|KayitTeslimTests`

| # | Kesim | Kırmızı |
|---|---|---|
| taban | — | 0/89 |
| M1 | App'e `ShareTargetTable` adlı ikinci tür (aynı ad alanı) | derleme hatası — geçerli mutasyon değil |
| M1b | App'e ayrı ad alanında `ShareTargetTable` ikizi | 1 |
| M2 | `LoadOrFallback` boş tabloyu varsayılana düşürmüyor | 1 |
| M3 | `LoadOrFallback` okuma hatasını yakalamıyor | 1 |
| M4 | Varsayılan tablonun `Default`'u `uguu.se` | 1 |
| M5 | `CanCreate` her hedef için `true` | 1 |
| M6 | Varsayılan uguu tavanı storage.to tavanına eşitlendi | 1 |
| M7 | Varsayılan uguu sabit ömrü 3 → 9 saat | 1 |

## İki kör nokta

**M6 ve M7 ilk turda yeşil döndü.** Varsayılan tablonun sayıları hiçbir ölçüde okunmuyordu:
yalnız hedef sayısı ve varsayılanın kimliği pimliydi. Dosyasız açılan pencere gerçekte kabul
edilmeyecek bir boyutu kabul eder gibi görünebilirdi ve kullanıcı bunu yüklemeye kalkışana
kadar görmezdi. Ölçü artık varsayılanın her hedefini depodaki gerçek dosyanın aynı kimlikli
satırıyla karşılaştırıyor (`defaultRetentionDays` dışarıda — o bir yeğleme, tavan değil).

**M3 ilk turda yanlışlıkla yeşil okundu.** Kesimi uygulayan betik `\n` ile eşleştiriyordu,
dosya `\r\n` taşıyor; mutasyon hiç uygulanmamıştı. Tek satırlık kesimle tekrarlandı, 1 kırmızı.
Sıfır kırmızı veren her kesimde önce kesimin gerçekten uygulandığına bakılır.
