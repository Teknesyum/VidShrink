# 025 — Hedef Boyutun Birimi (O2, MB/MiB): Danışma Girdisi

Türkçe yanıtla. Kod yazma, dosya düzenleme; yalnız oku ve karar ver.

Soru (O2): Arayüz hedefi "MB" diye etiketliyor ama motor 1024² ile (MiB) hesaplıyor (`KbitPerMib = 8388.608`). Hedef, kullanıcının beklediği ondalık MB'dan %4,9 büyük çıkıyor; `main.chip.128.tip` ise MiB diyor, yani arayüz kendi içinde tutarsız. Önce şunları oku:
- docs/inceleme/yalan-yok-2026-09-23.md, O2 bölümü
- docs/inceleme/model-strateji.md §3 (MediaInfo birimleri) ve §2.1
- `rg -n "KbitPerMib|1024 \* 1024|1048576" src` ile motorun birimi
- src/VidShrink.App/Locales/tr/main.json ve en/main.json içinde `main.unit.mb`, `main.chip.*.tip`, `settings-tab.default-target.label`
- Platform sınırları (Discord 10/25/50 MB, Gmail 25 MB, WhatsApp vb.) ön ayar tablosu: src/VidShrink.Core/Presets/platformlar.json ve docs/olcumler/onayar-kaynaklari.md

Karar ver: (a) motor ondalık MB'a geçsin, (b) etiket MiB olsun, (c) başka. Platform sınırlarının gerçek birimini (Discord/Gmail/WhatsApp sınırı bayt olarak ne) kaynaklarda ne yazıyorsa ona göre değerlendir; hedefin sınırı aşmaması en önemli şart. Windows Gezgini MB'ı 1024² gösterir, macOS 1000² — bunu da tart.

Çıktı: en fazla 25 satır. Karar, gerekçe (3-5 madde), değişecek yerlerin listesi (dosya:satır), test edilecek olumsuz kontrol. Belirsiz kalan varsa açıkça yaz.
