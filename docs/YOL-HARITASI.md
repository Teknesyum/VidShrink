# Yol Haritası

Sıra kullanıcının. Biten madde silinmez, `[x]` ile işaretlenir ve nedeni satırda kalır.

## Sıra

- [x] 8. dalga — ekran kaydı motoru, ses girdisi, Kaydedici sekmesi (0.4.1)
- [x] Güncelleme senkronunun yakınsaması — kurulu 0.3.0 kendi senkronuyla ilerleyebiliyor (0.4.2)
- [ ] Ekran kaydedici modülünün tamamlanması — kullanıcının bir sonraki işi
- [ ] WhatsApp'a özel azami kalite — karanlık videoda törpüleme ölçümü, `.claude/sonra.md`
- [ ] Başlık çubuğu düğmelerinin keskin köşesi — kullanıcı çalışan bir yapıyı açtıktan sonra bakacak

## Kararlar

### 12 işlik turun kesit sırası (12 Eylül 2026)

Kullanıcının verdiği 12 iş dört kesite bölündü ve bu sırayla kuruluyor:
**A** simge takımı ve Ayarlar sekmesi → **B** oynatıcı yerleşimi →
**C** kaydedicinin ffmpeg kolu (worktree ajanı) → **D** kaydedicinin otomatik kipi.
A ve B arayüzün aynı belirteçlerine dokunduğu için ardışık; C kendi dalında koştuğu
için B ile çakışmıyor; D, C'nin ürettiği ayar yüzeyi olmadan ölçülemediği için sonda.

### Tek sürüm numarası, üç işletim sistemi (12 Eylül 2026)

Windows, macOS ve Linux ayrı sürüm numarası taşımaz. `Directory.Build.props` içindeki tek
`<Version>` üçünü de etiketler; `release.yml` aynı etiketten `win-x64`, `osx-x64`,
`osx-arm64` ve `linux-x64` üretir. Bir platformda bir iş eksik kalıyorsa sürüm numarası
değil o işin kendisi geride kalır.

### macOS alt sürümü: 15 (12 Eylül 2026)

osx-arm64 paketinin libmpv'si Homebrew şişelerinden geliyor ve 48 dylib'in 47'si
`minos=15.0` taşıyor (`docs/olcumler/libmpv-macos-gomme.md:160`, librubberband 11.0). Alt sürümü 13'e indirmek libmpv'yi kendimiz
derlemek ya da MPVKit'e geçmek demek; ikisi de kendi yayın zincirini getiriyor. Oynatıcı
libmpv'ye bağlı olduğu için alt sürüm macOS 15 ilan edilir. Daha düşük bir taban istenirse
iş `.claude/sonra.md`'deki libmpv derleme maddesinden açılır.
