# Linux Bulut Ölçüm Planı

Tarih: 15 Ekim 2026 · Karar: `docs/piyasa/2026-10-13/karar-sorulari.md`, "Sahibin Cevabı" 2. madde (Linux ölçümleri Claude bulut oturumunda, bütçe 125 dolar).
Kaynak paketler: `docs/later.md` 135 (PLT-P8: VAAPI ve sistem sesi), 136 (PLT-P9), 113 (EKA-P14), 146 (EKB-P17).

## Bulut Ortamı Ne Veriyor

Kaynak, 15 Ekim 2026'da okundu: [Claude Code on the web](https://code.claude.com/docs/en/claude-code-on-the-web) ve [Cloud environments](https://code.claude.com/docs/en/cloud-environments).

| Konu | Belgede yazan |
|---|---|
| İşletim sistemi | Ubuntu 24.04, x86_64, her oturumda taze sanal makine |
| Kaynak | yaklaşık 4 vCPU, 16 GB bellek, 30 GB disk ("değişebilir") |
| Yetki | kurulum betikleri root koşar, `apt install` çalışır |
| Ağ | varsayılan "Trusted" listede `archive.ubuntu.com`, `github.com`, `nuget.org`, `dot.net`, `packages.microsoft.com` var |
| Hazır araçlar | Python, Node, Ruby, PHP, Java, Go, Rust, C/C++, Docker, PostgreSQL, Redis, git, gh. **.NET SDK hazır değil** (belge: kurulum betiğiyle kurulur). ffmpeg listede yok |
| Süreler | komut varsayılan 2 dk, en çok 10 dk; kurulum betiği ~5 dk'yı aşarsa önbelleğe girmez; boşta oturum birkaç dakikada duraklar |
| Ücret | belge: bulut sanal makinesi için ayrı ücret yok, kullanım plan limitine sayılır |

**Belgenin söylemediği (doğrulanamadı):** ekran kartı, `/dev/dri`, ses sunucusu, X11/Wayland ekranı hakkında tek cümle yok.
Kaynak listesinde yalnız vCPU, bellek, disk var. "GPU yok" bu yüzden **çıkarımdır, belge hükmü değil**.
Oturumun ilk işi bunu komutla yoklamaktır (aşağıda).

**Bütçe notu:** Belgeye göre bulut oturumunun makine ücreti ayrı faturalanmıyor. 125 doların bulut oturumunda neye harcanacağı
(plan limiti mi, ek kullanım mı) belgeden çıkmıyor; sahibe sorulmalı. Aşağıdaki maliyet tablosu kiralık donanım seçenekleri için.

## Oturumun İlk Yoklaması

Ortamın kurulum betiği (root koşar):

```
apt-get update && apt-get install -y dotnet-sdk-8.0 ffmpeg pulseaudio xvfb vainfo pciutils xdotool
```

Paket adları Ubuntu 24.04 için bu turda **denenmedi**; ilk koşuda hata verirse düzeltilir. Sonra çıktıları ölçüm belgesine aynen gir:

```
ls -la /dev/dri; lspci | grep -i -E "vga|display|3d"; vainfo
```

```
ffmpeg -hide_banner -devices; ffmpeg -hide_banner -hwaccels; ffmpeg -hide_banner -encoders | grep -E "vaapi|libx26|svtav1"
```

`/dev/dri` yoksa ve `vainfo` aygıt açamıyorsa aşağıdaki "Bulutta Ölçülemez" listesi kesinleşir.

## Bulutta Ölçülebilir

| # | İş | Nasıl | Ne kanıtlar | Sınır |
|---|---|---|---|---|
| L1 | VAAPI **yokluğunda** doğru davranış (PLT-38'in yarısı) | GPU'suz VM'de `bench shrink ... --speed fast --plan-only`, VAAPI'yi açık seçimle zorlamayı da dene | Aygıt yokken planın VAAPI'yi seçmediği, hata satırının anlaşılır olduğu | Hız ve kalite sayısı **vermez** |
| L2 | pulse `.monitor` kaynağı (PLT-43'ün yarısı) | Pulse'u kullanıcı kipinde başlat, `pactl load-module module-null-sink sink_name=test`, `paplay` ile ton çal, `ffmpeg -f pulse -i test.monitor -t 5 ...` ile kaydet, `volumedetect` ile ölç | `CaptureDevices.cs:239-264` sınıflaması ve ffmpeg pulse girişi gerçek pulse sunucusunda çalışıyor | Yazılım sink'i; gerçek hoparlör yolu değil |
| L3 | X11 ekran yakalama (EKB-P17'nin X11 kolu) | `Xvfb :99 &`, `DISPLAY=:99`, `ffmpeg -f x11grab -i :99 -t 3 ...` | `x11grab` yolu ve bölge kırpma argümanları gerçek X sunucusunda koşar | Sanal ekran; gerçek masaüstü, pencere yöneticisi yok |
| L4 | X11 genel tuş dinleme (PLT-P9'un X11 kolu) | Xvfb üstünde `xdotool key` ile sahte tuş gönder, dinleyicinin yakaladığını ölç | Atama mantığı gerçek X olayıyla | Dinleyici kodu henüz yok; yazılınca |
| L5 | Linux yapısı ve kurucu | `dotnet build`, ilgili sınıflara `dotnet test --filter`, `install-vidshrink.sh` kuru koşum | Linux yayını derleniyor, kurucu çalışıyor | Tam süit değil; sınıf-arası yarışı yalnız CI görür |

L2'de "ses geldi" yalnız `volumedetect` çıktısıyla (`mean_volume` sessizlik değerinden farklı) kabul edilir.

## Bulutta Ölçülemez

| # | İş | Neden | Seçenek |
|---|---|---|---|
| Ö1 | VAAPI hız ve kalite (PLT-38, later 135'in asıl ölçümü) | Ekran kartı yok (çıkarım); VAAPI Intel ya da AMD kartı ister | A, B, C |
| Ö2 | Gerçek ses kartı ve hoparlör yolu | Ses donanımı yok | A, B, C |
| Ö3 | Wayland'da ekran yakalama (EKA-P14, later 113) | Wayland oturumu ve PipeWire portal onay penceresi ister; kapsayıcıda masaüstü yok | B, C |
| Ö4 | GlobalShortcuts portalı (PLT-P9, later 136) | Portalı sunan gerçek masaüstü şart (later 136 planı böyle diyor) | B, C |
| Ö5 | Gözle doğrulama (kayıt görüntüsü, mini pencere düzeni) | Ekran yok; görüntü dosyası alınabilir, pencere izlenemez | B, C |
| Ö6 | GNOME ve KDE farkları | Tek tür sanal makine | C |

## Ölçülemeyenler İçin Seçenekler

| Seçenek | Ne | Kapsadığı | Kaba maliyet | Kaynak ve güven |
|---|---|---|---|---|
| A | AMD ekran kartlı bulut sunucusu, AWS g4ad.xlarge (Radeon Pro V520), saatlik | Ö1, Ö2 | $0,3785/saat; 20 saat ≈ $7,6; 40 saat ≈ $15,1 (disk ve trafik hariç) | [Holori](https://calculator.holori.com/aws/ec2/g4ad.xlarge/us-east-1): aracı site, AWS'den doğrulanmadı. VAAPI'nin bu sanal kartta çalıştığı **doğrulanmadı** |
| B | Intel ya da AMD iGPU'lu kiralık adanmış sunucu. Hetzner EX44 (Intel i5-13500) ve AX42 (Ryzen 7 PRO 8700GE, Radeon 780M) | Ö1-Ö5 (masaüstü kurulursa) | EX44: 2023 duyurusunda €52,36/ay + €46,41 kurulum. AX42: lansmanda ~€49/ay + €39 kurulum. Sonraki bildirimlerde kurulum €99-107. Güncel fiyat **doğrulanmadı** | [Hetzner duyurusu](https://www.hetzner.com/de/press-release/neue-dedicated-server-2023), [LowEndTalk özeti](https://scour.ing/p/https://lowendtalk.com/discussion/214154/hetzner-increases-setup-fees-order-on-spacecore-at-the-old-price-while-you-can), [AX42 sayfası](https://www.hetzner.com/de/dedicated-rootserver/ax42). EX44'ün iGPU'su UHD 770 diye varsayıldı, kaynak doğrulamadı |
| C | Gönüllü: Intel/AMD kartlı Linux makinesi olan biri | Ö1-Ö6 | $0 (zaman ve güven bedeli) | Ölçüm betiğini ve komutları hazır veririz |
| D | Hetzner GEX45 GPU sunucusu | - | €214/ay + €209 kurulum: bütçeyi aşar | [DataCenterNews](https://datacenternews.asia/story/hetzner-launches-gex45-gpu-server-for-ai-workloads). Kartın VAAPI'ye uygunluğu doğrulanmadı |

125 dolara oturan yollar: **A** (onlarca dolar) ve **C**. **B**'nin ilk ayı kira artı kurulumla €150 civarına çıkar
(EUR/USD kuru bu turda alınmadı); AX42 sayfasında saatlik fatura var görünüyor ama tutarı bulunamadı, bütçeye sığıp sığmadığı
sipariş sayfasında görülmeden söylenemez.

**Önerim:** önce bulutta L1-L5'i bitir (ek maliyet yok), sonra yalnız Ö1 ve Ö2 için A'yı 10-20 saatlik pencereyle aç.
Wayland ve portal işleri (Ö3, Ö4) kod yazıldıktan sonra C ile ya da masaüstü kurulu bir B örneğiyle yapılır.
Hangisinin seçileceği sahibin kararıdır.

## Sıra

1. Ortam kurulum betiğiyle hazırlanır, "İlk Yoklama" çıktısı `docs/olcumler/linux-bulut-yoklama.md` olarak commit edilir.
2. L1, L2, L3, L5 ölçülür; sonuç `docs/olcumler/linux-bulut-olcum.md`.
3. Yoklama GPU ya da ses aygıtı gösterirse (çıkarım yanlış çıkarsa) Ö1 ve Ö2 aynı oturumda ölçülür, seçenekler gerekmez.
4. Ölçülemeyenler için sahibin seçimi (A, B ya da C) alınır; seçilen makine için `VidShrink.Bench` komutları bu belgeye eklenir.

## Ölçülmeyenler

Bu belge bir plandır; hiçbir Linux ölçümü yapılmadı. Bulut ortamı bilgisi Anthropic belgelerinden, fiyatlar aracı sitelerden alındı.
