# Sahibin Adımları: winget, Scoop, Weblate

Depo hazır: winget bildirimleri `packaging/winget/`, Scoop bildirimi `docs/dagitim/scoop/`,
Weblate ayarı `.weblate` ve `docs/dagitim/weblate.md`. Yayın iş akışı (`release.yml`) her
sürümde winget ve Scoop'u tazeleyen iki iş taşır; sır yoksa ikisi de yeşil geçip "atlandı"
yazar. Aşağıdaki adımlar hesap, onay ve sır ister; bunları yalnız sahip yapar.

İmza bu listede yok: SignPath başvurusu 2026-10'da reddedildi, öteki yollar ve fiyatları
`docs/imzalama.md`'de.

Sır eklemek her adımda aynı yerden: GitHub'da depo → **Settings** → **Secrets and
variables** → **Actions** → **New repository secret**.

## 1. winget

Önce koşul: winget kurulu sürümü Windows'un "Yüklü uygulamalar" kaydından tanır.
`VidShrinkPlayer-Setup.exe` bugün o kaydı yazmıyor ve sessiz kurulumda hata olursa Enter
bekleyebilir. Bunlar `docs/later.md`'deki DAG-P1 paketinde (DAG-16, DAG-17); o paket
bitmeden winget'e göndermek, kurulup "yüklü değil" görünen bir paket demek. Sıra: önce
DAG-P1, sonra aşağısı.

1. GitHub'da https://github.com/microsoft/winget-pkgs deposunu kendi hesabına çatalla
   (sağ üstte **Fork**). wingetcreate çekme isteğini bu çataldan açar.
2. İlk gönderim elle yapılır, çünkü iş akışındaki `wingetcreate update` yalnız var olan
   paketi tazeler. Bir Windows makinede:
   - Son sürümün `VidShrinkPlayer-Setup.exe`'sinin sha256'sını `checksums-win-x64.txt`'den al.
   - `packaging/winget/Teknesyum.VidShrinkPlayer.installer.yaml` içindeki 64 sıfırı bu değerle
     değiştir; üç dosyadaki `PackageVersion` ve `Custom: --tag` o sürümle aynı olsun.
   - `winget validate --manifest packaging\winget` ile denetle.
   - `winget install --manifest packaging\winget` ile kendi makinende kurmayı dene.
     Bunun için bir kez yönetici olarak `winget settings --enable LocalManifestFiles` gerekir.
   - Gönder: `wingetcreate submit --token <PAT> packaging\winget`
     (wingetcreate: https://github.com/microsoft/winget-create/releases).
3. winget-pkgs ekibi çekme isteğini inceler; birleşince paket `winget install
   Teknesyum.VidShrinkPlayer` ile kurulur.
4. Sonraki sürümler için sır: https://github.com/settings/tokens → **Generate new token
   (classic)** → yalnız `public_repo` kutusu. Değeri depoya `WINGET_TOKEN` adıyla ekle.
   Bundan sonra her yayın winget-pkgs'e kendiliğinden çekme isteği açar.

## 2. Scoop

1. Kova için boş, herkese açık bir depo aç: `Teknesyum/scoop-bucket`. Başka ad
   seçersen depo ayarında **Variables** sekmesine `SCOOP_BUCKET_REPO` değişkenini
   `sahip/depo-adi` biçiminde ekle.
2. Yalnız o depoya yazabilen bir anahtar üret: https://github.com/settings/personal-access-tokens
   → **Generate new token** → **Only select repositories** → kova deposu →
   **Repository permissions** → **Contents: Read and write**.
3. Değeri VidShrink deposuna `SCOOP_BUCKET_TOKEN` adıyla ekle.
4. Bir sonraki yayında iş, `docs/dagitim/scoop/vidshrink-player.json`'ı sürüm adresi ve gerçek
   sha256 ile kovanın `bucket/vidshrink-player.json` dosyasına yazar.
5. Kullanıcının kuracağı komut:
   `scoop bucket add teknesyum https://github.com/Teknesyum/scoop-bucket` ve
   `scoop install teknesyum/vidshrink`.

Scoop paketi başlatıcısız: uygulama kendini güncellemez, `scoop update vidshrink` günceller.
Uygulamanın kendi güncelleyicisinin Scoop klasöründe nasıl davrandığı ölçülmedi; ilk
kurulumdan sonra bakılmalı.

## 3. Weblate

1. https://hosted.weblate.org/ adresinde hesap aç (GitHub ile giriş olur).
2. Ücretsiz Libre planına başvur: https://hosted.weblate.org/hosting/ → **Libre** →
   proje adı `vidshrink`, depo `https://github.com/Teknesyum/VidShrink-Player.git`, lisans
   AGPL-3.0-or-later. Libre yalnız açık kaynak ve herkese açık projeye verilir; onay
   Weblate ekibinden gelir.
3. Onaydan sonra bileşenleri `docs/dagitim/weblate.md`'deki tablolarla kur: uygulama için
   tek "bileşen keşfi" ayarı on bileşeni açar, komut satırı ayrı bileşen.
4. GitHub'da depo → **Settings** → **Webhooks** → **Add webhook** → adres
   `https://hosted.weblate.org/hooks/github/`, içerik türü `application/json`, yalnız
   **push** olayı. Weblate böylece her itmede depoyu çeker.
5. Weblate'in çeviriyi geri yazabilmesi için bileşen ayarında **push branch** `weblate`
   olsun; Weblate kendi GitHub kullanıcısıyla çekme isteği açar, CI yeşilse birleştirirsin.
6. İlk çekme isteğinde `git diff`'e bak: anahtarlar `"main.chip.archive.tip"` gibi düz
   kalmalı. İç içe nesneye döndüyse bileşenin biçimi yanlış seçilmiştir (`json` olmalı,
   `json-nested` değil).

## Hangi Sır Nereye

| Ad | Tür | Nereden | Ne için |
|----|-----|---------|---------|
| `WINGET_TOKEN` | Secret | Klasik PAT, `public_repo` | winget-pkgs'e çekme isteği |
| `SCOOP_BUCKET_TOKEN` | Secret | İnce ayarlı PAT, kova deposuna Contents yazma | Kovaya itme |
| `SCOOP_BUCKET_REPO` | Variable (isteğe bağlı) | — | Kova `Teknesyum/scoop-bucket` değilse |
