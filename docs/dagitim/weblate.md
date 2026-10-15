# Weblate Yapılandırması

Proje henüz Hosted Weblate'te açılmadı. Bu belge, sahip projeyi açtığında bileşenlere
girilecek değerleri tutar; hesap ve başvuru adımları `docs/sahip-adimlari.md`'de.

## Depodaki Dosyalar

- `.weblate` — `wlc` komut satırı aracının ayarı: sunucu `https://hosted.weblate.org/api/`,
  varsayılan bileşen `vidshrink/main`. API anahtarı depoya girmez; `wlc` onu kullanıcının
  kendi `~/.config/weblate` dosyasından okur.
- Kaynak dil İngilizce (`en`). Gözden geçmiş diller `en` ve `tr`
  (`Strings.ReviewedLanguages`); geri kalan 40 dil dil seçicide "makine çevirisi"
  işaretiyle görünür. Bir dil gözden geçince koddaki listeye eklenir, işaret kalkar.

## Bileşenler

Uygulamanın on alanı ayrı dosyada, her biri ayrı bileşen olur. Weblate'in "bileşen keşfi"
(component discovery) eklentisi tek ayarla hepsini açar:

| Alan | Değer |
|------|-------|
| Eşleşme ifadesi | `src/VidShrink.App/Locales/(?P<language>[^/]+)/(?P<component>[^/]+)\.json` |
| Bileşen adı | `{{ component }}` |
| Tek dilli temel dosya | `src/VidShrink.App/Locales/en/{{ component }}.json` |
| Yeni çeviri şablonu | `src/VidShrink.App/Locales/en/{{ component }}.json` |
| Dosya biçimi | `json` (JSON file) |
| Dil kodu biçimi | BCP-47 (`zh-Hans`, `nb`) |

Keşfin bulacağı on bileşen: `advanced`, `editor`, `main`, `mediainfo`, `performance`,
`playback`, `recorder`, `settings`, `tools`, `tracks`.

Komut satırı ayrı bileşendir, yalnız İngilizce ve Türkçe var:

| Alan | Değer |
|------|-------|
| Dosya maskesi | `src/VidShrink.Cli/Locales/*.json` |
| Tek dilli temel dosya | `src/VidShrink.Cli/Locales/en.json` |
| Dosya biçimi | `json` |

## Depo Bağlantısı

- Kaynak depo: `https://github.com/Teknesyum/VidShrink.git`, dal `main`.
- Weblate değişikliği doğrudan `main`e itmez; "push branch" `weblate` olur ve çeviri
  oradan çekme isteğiyle gelir. CI yeşil olmadan birleşmez.
- GitHub'da depo ayarına Weblate'in webhook adresi eklenir:
  `https://hosted.weblate.org/hooks/github/`.

## Ölçülmeyenler

- Anahtarlar noktalı ve düz (`"main.chip.archive.tip"`). Weblate belgesi `json`
  biçiminin düz anahtarı aynen yazdığını söylüyor, `json-nested` ise noktadan iç içe
  nesne kurar; noktalı anahtarın Weblate'ten geçip aynı biçimde geri yazıldığı ölçülmedi.
  İlk çekme isteğinde `git diff` ile bakılır: anahtarlar iç içe olduysa biçim yanlıştır.
- Weblate'in JSON girintisi ve satır sonu bizimkinden farklı olabilir (depo LF, iki boşluk).
  Fark varsa bileşen ayarındaki "JSON girintisi" 2 yapılır.
