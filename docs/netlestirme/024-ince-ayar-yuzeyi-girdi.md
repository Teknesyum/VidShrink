# 024 — Küçültmede İnce Ayar Yüzeyi (E1/E5): Danışma Girdisi

Fable'a verilen metin, birebir:

---

VidShrink'in küçültme sekmesinde "Gelişmiş" paneli var ve bugün şunları açıyor: kip
(otomatik / CRF / iki geçiş), CRF değeri, kodlayıcı ön ayarı (`ultrafast..veryslow`,
`p1..p7`, SVT-AV1 `0..13`), kodlayıcı yolu (otomatik/yazılım/donanım), kodek kilidi, ses
kbit'i, ses kanalı. Bunlar plan seçeneklerine `LockedCrf`, `LockedPreset`, `LockedCodec`,
`LockedAudioKbps` olarak geçiyor.

HandBrake'in Video sekmesine göre kalan boşluk üç kol: `-tune` (film/animation/grain...),
`-level:v` (4.0, 4.1...), ve `--encopts` benzeri serbest kodlayıcı seçeneği dizesi. Bunlar
bugün yalnız LLM plan süzgecinde tanınıyor (`PlanParser.cs:147-151`), kullanıcıya kapalı.

Ses tarafında HandBrake'in `--arate` (örnekleme hızı) ve `--aq` (kalite tabanlı ses)
karşılığı yok: `-ar` ve `-q:a` kaynakta hiç geçmiyor. `--ab` (bit hızı) ve `--mixdown`
(kanal) zaten panelde var.

Ayrıca bir asimetri var: CLI'da (`vidshrink kucult`) bu kilitlerin hiçbiri yok — `--hedef`,
`--kalite`, `--hizli`, `--kodek` var, ama `LockedCrf`/`LockedPreset` komut satırından
verilemiyor. Arayüzde olan CLI'da yok.

Proje kısıtı: kullanıcı kolaylığı ön planda, yeni kutu açmak son çare; uydurma ölçü/renk
yok; her eklenen kolun testle ve mutasyonla pimlenmesi gerekiyor.

Sorular:

1. `-tune`, `-level:v` ve serbest kodlayıcı seçeneği dizesi Gelişmiş panele eklenmeli mi?
   Eklenecekse üçü de mi, yoksa bir kısmı mı?
2. Ses örnekleme hızı (`-ar`) ve kalite tabanlı ses (`-q:a`) kullanıcıya açılmalı mı,
   yoksa hedef bit hızından otomatik mi türemeli, yoksa hiç dokunulmamalı mı?
3. Arayüz-CLI asimetrisi kapatılmalı mı — yani `--crf`, `--on-ayar` gibi bayraklar CLI'a
   eklenmeli mi? Yoksa CLI'nın daha dar kalması doğru mu?
