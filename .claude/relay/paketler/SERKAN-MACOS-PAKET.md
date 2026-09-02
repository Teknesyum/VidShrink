# Serkan — macOS, üç iş sırayla. Aralarında bana dönme.

Kural: birincisini bitirip ittikten sonra **beklemeden** ikincisine geç. Denetimi ben
paralel koşturacağım; sonucu sana ayrıca gelir, beklemek zorunda değilsin.

Dal: `serkan/macos-olcum`. `main`den aç, oraya it. **`main`e birleştirme** — onu ben
yaparım.

Ortam: gerçek bir Mac. Bu paketin tamamının sebebi bu; Windows'ta yapılabilecek
hiçbir şey burada yok.

---

## Neden bu paket var

Depoda macOS kodu var ve derleniyor, ama **hiç gerçek Mac'te ölçülmedi.**
Ölçülen kanıtlar:

- `docs/olcumler/macos-paket.md:84` — macOS'a özgü testler Windows'ta erken dönüp
  **geçiyor**, atlanmıyor. Yani yeşil süit macOS hakkında hiçbir şey söylemiyor.
- `docs/olcumler/macos-guncelleme.md:346` — atlanan 18 test, bir önceki paketin
  atladığı 18'in aynısı. İki paket boyunca aynı 18 ölçü hiç koşmadı.
- `.claude/relay/LOG.md:25` — T13 mühür notu: *"betik hâlâ gerçek macOS/Linux
  makinede koşturulmadı."* 23 Ağustos'tan beri açık.
- `docs/olcumler/ci-benzetimi.md:225` — betiğin macOS altındaki davranışı ölçülmedi.

Ve asıl boşluk: **donanım kodlayıcı yolumuz yalnız NVENC/Windows'ta ölçüldü.**
Apple Silicon'ın VideoToolbox'ı hiç ölçülmedi. "Piyasadaki en iyi auto mode"
iddiasının Mac tarafında hiçbir sayısal dayanağı yok.

---

## İş 1 — 18 atlanan ölçüyü gerçekten koştur

**Amaç.** Windows'ta sessizce geçen macOS ölçülerinin Mac'te ne dediğini öğrenmek.

**Yap:**

1. `dotnet test -c Release` tam süiti koştur. Çıktının **tamamını** sakla.
2. Atlanan ve geçen testleri ayır. `macos-paket.md:84`'ün söylediği "Windows'ta
   erken dönüp geçiyor" davranışının Mac'te ne olduğunu tek tek yaz: gerçekten
   koştu mu, yine mi erken döndü, kırmızı mı.
3. Kırmızı çıkan her test için: **tek başına** yeniden koştur (yük yapıntısı bu
   depoda ölçülmüş bir kusur), sonra nedenini bul.

**Kabul kriteri:**

- K1 — Tam süit çıktısı raporda. Başarılı/başarısız/atlanan sayıları ham hâliyle.
- K2 — Windows'ta geçip Mac'te koşan her testin adı listelenmiş, sayısı verilmiş.
  "18" sayısını tekrar etme; **kendin say ve saydığın listeyi yaz.**
- K3 — Her kırmızı için: tek başına koşum sonucu + kök neden + düzeltme ya da
  "düzeltilmedi, sebebi şu" satırı.

**Çıktı:** `docs/olcumler/macos-gercek-kosum.md`

---

## İş 2 — VideoToolbox donanım kodlayıcısını ölç *(paketin asıl işi)*

**Amaç.** Mac'te donanım yolunun kaliteyi ve süreyi ne yaptığını ölçmek. Bugün
sıfır veri var.

**Bağlam.** Windows'ta ölçtüğümüz: donanım kodlayıcı hızlı ama kaliteyi düşürüyor,
bu yüzden `auto` modu donanımı yalnız belirli koşullarda seçiyor. Karar mantığı
`src/VidShrink.Core/PlanCalculator.cs` içinde ve **NVENC ölçümlerine göre
ayarlanmış.** Mac'te aynı eşiklerin doğru olup olmadığı bilinmiyor.

**Yap:**

1. Ortak ölçüm parçalarını kullan. **Kendi kaynağını üretme** — bu depoda "aynı
   yerden kesilmiş ama aynı içerikte olmayan" parçalar ölçümü bir kez haksız yaptı.
   Hangi dosyayı kullandığını yaz.

   Parçalar `.gitignore`da, yani sende yok. Depo kökünde indir:

   ```
   mkdir -p .calisma/kaynak
   gh release download olcum-kaynak-v1      --repo Teknesyum/VidShrink-olcum-kaynak      --dir .calisma/kaynak
   shasum -a 256 .calisma/kaynak/parca-*.mkv
   ```

   Beklenen sha256:
   ```
   89CBDE4012ED6220243C973F1BA1D657C984695FD1A935742DFED9511BBD9492  parca-1.mkv
   18F9B8E578285705F67BD4324687D2DA8A5E6DCC59A3A541EE060354ACD8A7BA  parca-2.mkv
   B69C00C589D60CBF0B2A4199408B5B22E6C417913762CCBD03A711F7E60B104D  parca-3.mkv
   ```

   Üçü de 1920x1080, 60 fps, hevc, yuv420p10le, bt2020/smpte2084, ~60 sn. İndirme
   sürerken bekleme — İş 1 ve İş 3 kaynak dosyaya bağlı değil, onları bitir.

   **Havuzda bilinen kusur:** `parca-1.mkv`de ses YOK, `parca-2` ve `parca-3`te AAC
   ses VAR; süreler de birebir eşit değil (60,399 / 60,442 / 60,432 sn). Ses hedef
   boyuttan yer, parçalar arası kıyası haksız yapar. **Her kolda `-an` kullan**,
   VMAF'ı video üzerinden ölç, üç parçanın da video-only koştuğunu raporda belge.
2. Her parça için üç kol koştur, aynı hedef boyutta:
   - `libx265` (yazılım)
   - `hevc_videotoolbox` (donanım)
   - `h264_videotoolbox` (donanım)
3. Her koşum için ölç: **VMAF ortalama, VMAF p10, çıktı boyutu, duvar saati süresi.**
   p10'u atlama — projenin tek gerçek üstünlüğü orada (ortalamada HandBrake ile
   başabaşız, p10'da öndeyiz).
4. `EncoderCapabilities` yoklamasının Mac'te ne kadar sürdüğünü ölç. Windows'ta
   3 625–14 855 ms ölçüldü ve arayüzü dondurma riski buradan geliyor.

**Kabul kriteri:**

- K1 — Üç kolun her biri için tablo: parça × kodek × VMAF ort × VMAF p10 × boyut ×
  süre. Her satırın üreten komutu yazılı.
- K2 — "Donanım yolunun kalite bedeli Mac'te şu kadar" cümlesi **tablodan
  türetilmiş** olmalı ve cümledeki her sayı tabloda birebir geçmeli. Bu deponun
  en sık kusuru budur: tablo doğru, onu özetleyen cümle uydurma.
- K3 — Yoklama süresi dağılımı: en az 10 ölçüm, min/medyan/maks.
- **Mutlak VMAF sayılarını Windows'unkilerle karşılaştırma.** Ölçüldü: aynı bayta
  iki farklı libvmaf sürümü iki farklı puan veriyor; sabit ofset, tekrarla
  küçülmüyor. Sürüm sınırını geçen kıyas geçersiz. Onun yerine **farkı**
  karşılaştır: Mac'te `libx265 p10 − hevc_videotoolbox p10`, Windows'ta
  `libx265 p10 − hevc_nvenc p10`. İki farkı yan yana koy; ofset büyük ölçüde
  sadeleşir. Rapora `ffmpeg -version` çıktısının ilk iki satırını yaz.
- K4 — Windows eşiklerinin Mac'te geçerli olup olmadığı hakkında **hüküm**:
  geçerli / geçerli değil / ölçü yetmiyor. Üçünden biri, gerekçesiyle.
  Kod değiştirme — hüküm yeter, kararı ben veririm.

**Çıktı:** `docs/olcumler/videotoolbox.md`

---

## İş 3 — Kurulum betiği ve paket, gerçek Mac'te

**Amaç.** 23 Ağustos'tan beri açık olan "betik gerçek Mac'te koşturulmadı"
maddesini kapatmak.

**Yap:**

1. `install-vidshrink.sh`'i temiz bir Mac hesabında koştur. Her adımın çıktısını sakla.
2. Kurulan `.app` paketini aç, motoru uçtan uca çalıştır (bir dosya sıkıştır).
3. Kendi kendini güncelleme yolunu dene (`macos-guncelleme.md`'nin anlattığı takas).
4. `macos-paket.md:140` diyor ki depoda macOS için `.icns` yok. Ekranda ne
   görünüyor, ekran görüntüsü al.

**Kabul kriteri:**

- K1 — Betiğin tam çıktısı ve çıkış kodu. Başarısızsa hangi satırda.
- K2 — `.app` açıldı ve bir dosya sıkıştırıldı: girdi, çıktı boyutu, süre.
- K3 — Güncelleme takası koştu mu: evet/hayır + kanıt.
- K4 — Simge durumu: ekran görüntüsü `docs/olcumler/gorseller/` altına.

**Çıktı:** `docs/olcumler/macos-kurulum-gercek.md`

---

## Üçü için ortak kurallar

- **Kod yorumu yazma.** İstenmedikçe.
- **Rapora giren her sayının üreten komutu yazılı olsun.** "Şu kadar ölçtüm" yetmez;
  komut satırı raporda dursun.
- **N madde/ölçü/kat diyen her cümlenin altındaki listeyi kendin say.** Bu depoda
  18'den fazla kez tablo doğru çıkıp onu özetleyen cümle yanlış çıktı.
- Geçici dosya, günlük, ekran görüntüsü → **`.calisma/`** altına. `%TEMP%`'e ya da
  proje köküne dağıtma.
- **`.calisma/kaynak/` ortak ve 3,5 GB. Silme, taşıma, üstüne yazma.**
- Ölçüm koşarken başka ağır iş çalıştırma; duvar saati ölçülerini yük bozuyor.
- İşin bitince dalını it. `main`e birleştirme.

Takıldığın yeri `.claude/relay/live/_sorun.log` dosyasına yaz — ekran görüntüsüyle
değil o dosyayla öğreniyorum.
