# VidShrink

Hedef boyuta sıkıştıran video aracı. .NET 8 + Avalonia + ffmpeg.

- `src/VidShrink.Core` — plan hesabı, argüman üretimi. Motorun kararları burada.
- `src/VidShrink.Ffmpeg` — süreç çağrıları, yoklama.
- `src/VidShrink.Player` — oynatıcı sekmesinin ve karşılaştırma panelinin motoru (`IPlaybackEngine`, libmpv). libmpv
  pakete girmez; Windows kurucusu `tools\libmpv`'ye sha256'lı indirir, macOS kurucusu
  MPVKit'ten bağlanmış dylib'i (`deps-libmpv-macos-mpvkit-1.0.0`, `tools/mpvkit-macos`) sha256'lı
  `tools/libmpv`'ye indirir, tutmazsa brew komutunu söyler, Linux kurucusu paket komutunu söyler, yerelde `VIDSHRINK_LIBMPV`, CI'da sha256'lı indirme.
- `src/VidShrink.App` — Avalonia arayüzü. Renk yalnız `Themes/Palette/<Ad>/Theme.axaml`
  dosyasından, ölçü yalnız `Themes/Theme.axaml` belirteçlerinden. Açılış paletini
  `App.axaml` bildirir, seçimi `PaletteCatalog.Use` çalışırken uygular; palet ikinci bir
  yerde merge edilmez, yoksa o kapsam seçimden kopar. Durum metni: `StatusError` hatada,
  `StatusWarning` "iş bitti ama eksik" durumunda — uyarı renkle değil simge ve ağırlıkla
  ayrılır, paletlerde uyarı hue'su yok (`docs/netlestirme/018-uyari-rengi-27-palette-yok.md`).
- Güncelleme "Yükle"si önce yerinde takas dener (`Core/InPlaceUpdate`, `App/YerindeGuncelleme`): değişen dosya
  `<ad>.old` olur, sahne adını alır, uygulama kendini yeniden açar, `.old` açılışta silinir. Yuva, kapı ya da güncelleme
  kilidi tutuluyorsa ya da takas düşerse geri alınır ve başlatıcının `--update-now` yoluna düşülür.
- `tests/VidShrink.Tests` — tek test projesi. Ölçüsü **dokunulan alan**: değişen her dosyanın
  testleri yerelde yeşil olmadan teslim yok. Tam süit yerelde koşulmaz; itmeden sonra CI
  koşar ve `gh run list` yeşili teslimin şartıdır. `tools/kosum-kapisi` yalnız **majör**
  sürümden (`x.0.0`) önce koşar; minörde ve yamada gerekmiyor.
- `tools/VidShrink.Bench` — ölçüm aracı. Rapora giren her sayı buradan çıkar.

## Geçici dosyalar

Sonda programı, ölçüm günlüğü, ekran görüntüsü, deneme betiği — hepsi **`.calisma/`**
altına. Sistemin `%TEMP%`'ine ya da proje köküne dağıtma; `.gitignore`'da, git'e sızmaz.

**İş bitince kendi bıraktığını sil.** Rapora giren sayı `docs/`e, ölçümü üreten
düzenek `tools/`a taşınır; geri kalan `.calisma/` ile birlikte gider.

Ajan worktree'si de birikinti: dalı `main`e birleştikten sonra
`git worktree remove` ile kaldır.

## Dallar

**`main`e yalnız T0 birleştirir.** Kim olursan ol — alt ajan, Sole, uzak makinedeki bir
ajan — kendi dalında çalış ve oraya it: `git switch -c <sahip>/<is>`. `main`in çalışma
ağacında doğrudan yazma; iki taraf aynı dosyayı yazınca yarım iş commit'e karışıyor.

Dal adı sahibi ve işi söyler: `sole/sagtik-win11`, `serkan/macos-ilk-kosum`,
`worktree-agent-<id>` (alt ajanlar bunu kendi kurar).

## Alt ajanlar

**Gelen rapor bir iddiadır, bulgu değil.** Ajanın "şöyle çalışıyor" dediği şey kodda
yerinde ölçülmeden iş alınmaz. Bir tur "tablo yoksa pencere açılmıyor" raporuna göre kod
düzenlendi; iddia yanlıştı (App'in kendi `Load`'u her istisnayı yutuyor) ve derleme hatası
olmasa doğrulanmadan teslim edilecekti. Ölçü: iddianın geçtiği satırı aç, oku, sonra karar ver.

**Her ajan `.calisma/` altında kendi klasörüne yazar:** `.calisma/<ajan-dalı>/`. Aynı adı
paylaşan iki paralel ajan birbirinin betiğini eziyor; çeviri turunda bir ajan betiğini
yeniden yazmak zorunda kaldı. Klasör adı ajanın dal adıyla aynı olur, tahmin edilmez.
